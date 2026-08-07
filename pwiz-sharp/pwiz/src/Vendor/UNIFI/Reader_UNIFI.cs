using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Sources;

#pragma warning disable CA1707

namespace Pwiz.Vendor.UNIFI;

/// <summary>
/// <see cref="IReader"/> for Waters UNIFI and waters_connect sample-result URLs. C# port of
/// pwiz cpp <c>Reader_UNIFI</c>. Identifies the input by URL shape:
/// <list type="bullet">
///   <item><c>https://&lt;host&gt;:&lt;port&gt;/unifi/v1/sampleresults(&lt;guid&gt;)?...</c> → UNIFI</item>
///   <item><c>https://&lt;host&gt;:&lt;port&gt;/?sampleSetId=…&amp;injectionId=…&amp;...</c> → waters_connect</item>
/// </list>
/// </summary>
/// <remarks>
/// Both endpoint families are served by the same Waters backend internally and share most of
/// the response payload (protobuf), so cpp builds <c>UnifiData</c> as a polymorphic class with
/// runtime <c>isWatersConnect()</c> branches. The C# port mirrors that with an abstract base
/// (<c>AbstractWatersHttpReader</c>, follow-on commit) and two concrete implementations.
///
/// Initial scope of this commit: just <see cref="Identify"/>. The full read pipeline (OAuth
/// token exchange, protobuf-net spectrum decode, parallel chunked download, spectrum/chromatogram
/// list construction) follows in a sequence of commits matching the cpp file structure
/// (<c>UnifiData</c> + <c>ParallelDownloadQueue</c> first, then <c>SpectrumList_UNIFI</c> +
/// <c>ChromatogramList_UNIFI</c>).
/// </remarks>
public sealed class Reader_UNIFI : IReader
{
    /// <inheritdoc/>
    public string TypeName => "Waters UNIFI";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_Waters_raw_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = Array.Empty<string>();

    /// <inheritdoc/>
    /// <remarks>
    /// Mirrors cpp <c>Reader_UNIFI::identify</c> (Reader_UNIFI.cpp:35-45): require an http(s)
    /// scheme, then route by query-shape. The two formats both return <see cref="CvType"/>
    /// (<c>MS_Waters_raw_format</c>) — cpp uses the same CV term and distinguishes the two
    /// only by the human-readable string returned from <c>identify</c> (UNIFI vs waters_connect).
    /// </remarks>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        if (!filename.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !filename.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return CVID.CVID_Unknown;

        if (filename.Contains("/sampleresults", StringComparison.OrdinalIgnoreCase))
            return CvType;
        // sampleSetId + injectionId are both required for waters_connect; the reader will
        // surface a clear error when one is missing. cpp matches on either to keep Identify
        // forgiving (and to let typo'd URLs reach Read with a useful message).
        if (filename.Contains("sampleSetId=", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("injectionId=", StringComparison.OrdinalIgnoreCase))
            return CvType;
        return CVID.CVID_Unknown;
    }

    /// <summary>True when <paramref name="url"/> is a UNIFI sample-result endpoint.</summary>
    public static bool IsUnifiUrl(string url)
        => !string.IsNullOrEmpty(url)
           && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
           && url.Contains("/sampleresults", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="url"/> is a waters_connect sample-result endpoint.</summary>
    public static bool IsWatersConnectUrl(string url)
        => !string.IsNullOrEmpty(url)
           && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
           && (url.Contains("sampleSetId=", StringComparison.OrdinalIgnoreCase)
               || url.Contains("injectionId=", StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

        if (Identify(filename, head: null) == CVID.CVID_Unknown)
            throw new InvalidDataException($"Not a UNIFI / waters_connect URL: {filename}");

#if NO_VENDOR_SUPPORT
        throw new VendorSupportNotEnabledException(
            "UNIFI / waters_connect reading requires the vendor data layer. Rebuild pwiz-sharp with --i-agree-to-the-vendor-licenses to enable.");
#else
        var effectiveConfig = config ?? new ReaderConfig();
        var connectionConfig = UnifiConnectionConfig.Parse(filename);

        // OAuth + sample metadata fetch happens in the client ctor.
        AbstractWatersHttpClient client = connectionConfig.Api == RemoteApiType.WatersConnect
            ? new WatersConnectHttpClient(connectionConfig)
            : new UnifiHttpClient(connectionConfig);

        try
        {
            IUnifiDataSource source = connectionConfig.Api == RemoteApiType.WatersConnect
                ? new WatersConnectDataSource(client, effectiveConfig.CombineIonMobilitySpectra, ownsClient: true)
                : new HttpUnifiDataSource(client, effectiveConfig.CombineIonMobilitySpectra, ownsClient: true);
            FillMetadata(result, source, client, effectiveConfig, originalUrl: filename);
        }
        catch
        {
            client.Dispose();
            throw;
        }
#endif
    }

    /// <summary>Removes the <c>username:password@</c> userinfo from <paramref name="url"/>
    /// so credentials don't leak into <see cref="SourceFile.Location"/> on disk. Operates on
    /// the raw string rather than <see cref="Uri"/> so the result preserves whatever the
    /// caller passed (the cpp reference URLs aren't <see cref="Uri"/>-canonicalized either).</summary>
    private static string StripUserInfo(string url)
    {
        int schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0) return url;
        int authStart = schemeEnd + 3;
        int at = url.IndexOf('@', authStart);
        if (at < 0) return url;
        // Make sure '@' is in the authority, not in the query/path that came after.
        int slash = url.IndexOf('/', authStart);
        int qmark = url.IndexOf('?', authStart);
        int authorityEnd = slash < 0 ? (qmark < 0 ? url.Length : qmark)
                          : qmark < 0 ? slash : Math.Min(slash, qmark);
        if (at >= authorityEnd) return url;
        return url[..authStart] + url[(at + 1)..];
    }

#if !NO_VENDOR_SUPPORT
    private static void FillMetadata(MSData result, IUnifiDataSource source, AbstractWatersHttpClient client, ReaderConfig config, string originalUrl)
    {
        result.CVs.AddRange(MSData.DefaultCVList);

        // cpp Reader_UNIFI.cpp:86-90: location is `sampleResultUrl.substr(0, queryStrBegin+1)`
        // where queryStrBegin = `rfind(")?")`. That's UNIFI-specific — it slices the query off
        // a `…(GUID)?...` URL but leaves a waters_connect URL (`/?sampleSetId=…`) intact since
        // there is no `)?` substring. Mirror that quirk byte-for-byte so reference mzMLs match.
        string apiName = source.RemoteApi == RemoteApiType.WatersConnect ? "waters_connect" : "UNIFI";
        string sampleName = string.IsNullOrEmpty(client.SampleName) ? client.Config.FriendlyName ?? "" : client.SampleName;
        if (string.IsNullOrEmpty(sampleName))
            sampleName = client.Config.SampleResultUrl.AbsolutePath;

        string runId = sampleName;
        if (!string.IsNullOrEmpty(client.WellPosition))
            runId += "_" + client.WellPosition;
        runId += "_" + client.ReplicateNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

        result.Id = runId;
        result.Run.Id = runId;

        // cpp Reader_UNIFI.cpp:86-90 strips the query string for UNIFI URLs (where the
        // `…(GUID)?…` shape lets `rfind(")?")` find a clean cut point) but leaves
        // waters_connect URLs intact (no `)?` substring). We mirror the conditional but
        // additionally strip any `username:password@` userinfo — credentials should never
        // land in an output mzML, regardless of whether cpp's reference originally had them.
        int queryStrBegin = originalUrl.LastIndexOf(")?", StringComparison.Ordinal);
        string locationNoQuery = queryStrBegin >= 0 ? originalUrl[..(queryStrBegin + 1)] : originalUrl;
        string location = StripUserInfo(locationNoQuery);
        var sourceFile = new SourceFile(apiName, runId, location);
        if (!string.IsNullOrEmpty(client.WellPosition))
            sourceFile.UserParams.Add(new UserParam("well position", client.WellPosition, "xsd:string"));
        sourceFile.UserParams.Add(new UserParam(
            "replicate number",
            client.ReplicateNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "xsd:positiveInteger"));
        result.FileDescription.SourceFiles.Add(sourceFile);
        result.Run.DefaultSourceFile = sourceFile;

        var acquisitionSoftware = new Software(apiName) { Version = "1.0" };
        acquisitionSoftware.Set(source.RemoteApi == RemoteApiType.WatersConnect
            ? CVID.MS_waters_connect : CVID.MS_UNIFY);
        result.Software.Add(acquisitionSoftware);

        var pwizSoftware = new Software("pwiz_Reader_UNIFI") { Version = MSData.PwizVersion };
        pwizSoftware.Set(CVID.MS_pwiz);
        result.Software.Add(pwizSoftware);

        var dp = new DataProcessing("pwiz_Reader_UNIFI_conversion");
        var pm = new ProcessingMethod { Order = 0, Software = pwizSoftware };
        pm.Set(CVID.MS_Conversion_to_mzML);
        dp.ProcessingMethods.Add(pm);
        result.DataProcessings.Add(dp);

        // Acquisition timestamp via the shared FormatStartTimeStamp helper so the
        // adjustUnknownTimeZonesToHostTimeZone flag is honored.
        string? startTime = config.FormatStartTimeStamp(client.AcquisitionStartTimeUtc);
        if (startTime is not null) result.Run.StartTimeStamp = startTime;

        // cpp Reader_UNIFI.cpp:147-151: a single placeholder InstrumentConfiguration with the
        // Waters family CV term. Emit before the spectrum list ctor so we can pass it as the
        // default-instrument hint (used by the per-scan instrumentConfigurationRef).
        var ic = new InstrumentConfiguration("IC1");
        ic.Set(CVID.MS_Waters_instrument_model);
        ic.Software = acquisitionSoftware;
        result.InstrumentConfigurations.Add(ic);
        result.Run.DefaultInstrumentConfiguration = ic;

        var spectrumList = new SpectrumList_UNIFI(source, defaultInstrumentConfiguration: ic,
                                                  config.CombineIonMobilitySpectra)
        { Dp = dp };
        var chromatogramList = new ChromatogramList_UNIFI(source, config.GlobalChromatogramsAreMs1Only)
        { Dp = dp };

        result.Run.SpectrumList = spectrumList;
        result.Run.ChromatogramList = chromatogramList;

        // cpp Reader_UNIFI.cpp:136-145: fileContent classification depends on what the lists
        // produced. MS1+MSn for any non-empty spectrum list; SRM chromatogram if any MRM
        // chromatogram was discovered.
        if (spectrumList.Count > 0)
        {
            result.FileDescription.FileContent.Set(CVID.MS_MS1_spectrum);
            result.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum);
        }
        bool hasMrm = source.ChromatogramInfo.Any(ch => ch.Type == ChromatogramType.MRM);
        if (hasMrm)
            result.FileDescription.FileContent.Set(CVID.MS_SRM_chromatogram);
    }
#endif
}
