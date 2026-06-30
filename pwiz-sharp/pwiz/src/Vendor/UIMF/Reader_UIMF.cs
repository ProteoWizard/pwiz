using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Sources;

#pragma warning disable CA1707

namespace Pwiz.Vendor.UIMF;

/// <summary>
/// <see cref="IReader"/> for PNNL UIMF files (.uimf — SQLite-backed ion-mobility raw data).
/// C# port of cpp <c>Reader_UIMF</c> (Reader_UIMF.cpp).
/// </summary>
/// <remarks>
/// UIMFLibrary is open-source (Apache 2.0), so this reader builds and runs without the
/// <c>--i-agree-to-the-vendor-licenses</c> gate the encrypted vendor SDKs sit behind.
/// </remarks>
public sealed class Reader_UIMF : IReader
{
    /// <inheritdoc/>
    public string TypeName => "UIMF";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_UIMF_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".uimf" };

    /// <inheritdoc/>
    /// <remarks>cpp Reader_UIMF.cpp:30-33: regular file with `.uimf` extension (case-insensitive).</remarks>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        if (!File.Exists(filename)) return CVID.CVID_Unknown;
        return filename.EndsWith(".uimf", StringComparison.OrdinalIgnoreCase)
            ? CvType
            : CVID.CVID_Unknown;
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

        if (Identify(filename, head: null) == CVID.CVID_Unknown)
            throw new InvalidDataException($"Not a UIMF file: {filename}");

        var effectiveConfig = config ?? new ReaderConfig();
        var data = new UimfData(filename);
        try
        {
            FillMetadata(filename, data, result, effectiveConfig);
        }
        catch
        {
            data.Dispose();
            throw;
        }
    }

    private static void FillMetadata(string filename, UimfData data, MSData result, ReaderConfig config)
    {
        result.CVs.AddRange(MSData.DefaultCVList);

        // cpp Reader_UIMF.cpp:60-76: fileContent CV terms gated on which frame types were
        // observed in the SQLite index. Calibration / Prescan don't add MS1 (they're
        // already-MS1, but cpp still calls .set(MS_MS1_spectrum) for Prescan; mirror that).
        var fc = result.FileDescription.FileContent;
        if (data.FrameTypes.Contains(UimfFrameType.MS1)) fc.Set(CVID.MS_MS1_spectrum);
        if (data.FrameTypes.Contains(UimfFrameType.Calibration)) fc.Set(CVID.MS_calibration_spectrum);
        if (data.FrameTypes.Contains(UimfFrameType.Prescan)) fc.Set(CVID.MS_MS1_spectrum);
        if (data.FrameTypes.Contains(UimfFrameType.MS2)) fc.Set(CVID.MS_MSn_spectrum);

        fc.Set(CVID.MS_profile_spectrum);
        fc.Set(CVID.MS_TIC_chromatogram);

        // cpp Reader_UIMF.cpp:81-89: sourceFile.id and .name = filename, .location = parent
        // dir as `file:///` URI. UIMF files don't have a vendor-specific native ID format
        // beyond the SQLite row, so the format CV is just `MS_UIMF_format` /
        // `MS_UIMF_nativeID_format`.
        var fileName = Path.GetFileName(filename);
        var parentDir = Path.GetDirectoryName(Path.GetFullPath(filename)) ?? string.Empty;
        var sourceFile = new SourceFile(fileName, fileName,
            "file:///" + parentDir.Replace('\\', '/'));
        sourceFile.Set(CVID.MS_UIMF_nativeID_format);
        sourceFile.Set(CVID.MS_UIMF_format);
        result.FileDescription.SourceFiles.Add(sourceFile);
        // Intentionally NOT setting Run.DefaultSourceFile: cpp Reader_UIMF.cpp:83-89 omits
        // the `msd.run.defaultSourceFilePtr` assignment, so the reference mzML's <run>
        // element has no `defaultSourceFileRef`. Mirror that.

        result.Id = Path.GetFileNameWithoutExtension(filename);
        result.Run.Id = result.Id;

        var pwizSoftware = new Software("pwiz") { Version = MSData.PwizVersion };
        pwizSoftware.Set(CVID.MS_pwiz);
        result.Software.Add(pwizSoftware);

        var dp = new DataProcessing("pwiz_Reader_UIMF_conversion");
        var pm = new ProcessingMethod { Order = 0, Software = pwizSoftware };
        pm.Set(CVID.MS_Conversion_to_mzML);
        dp.ProcessingMethods.Add(pm);
        result.DataProcessings.Add(dp);

        // cpp Reader_UIMF.cpp:112-115: emit a placeholder InstrumentConfiguration. The UIMF
        // format doesn't carry instrument-model metadata, so cpp uses the bare `instrument
        // model` CV without a specific child.
        var ic = new InstrumentConfiguration("IC");
        ic.Set(CVID.MS_instrument_model);
        result.InstrumentConfigurations.Add(ic);
        result.Run.DefaultInstrumentConfiguration = ic;

        // Acquisition timestamp via the shared FormatStartTimeStamp helper so the
        // adjustUnknownTimeZonesToHostTimeZone flag is honored. cpp passes a
        // local_date_time with no zone (UIMFReader.cpp:223-224), which on encoding becomes
        // a naive timestamp; FormatStartTimeStamp handles the equivalent here.
        var acq = data.GetAcquisitionTimeUtc();
        if (acq is DateTime utc)
        {
            string? startTime = config.FormatStartTimeStamp(utc);
            if (startTime is not null) result.Run.StartTimeStamp = startTime;
        }

        var spectrumList = new SpectrumList_UIMF(data, defaultInstrumentConfiguration: ic,
            ignoreZeroIntensityPoints: config.IgnoreZeroIntensityPoints)
        { Dp = dp };
        var chromatogramList = new ChromatogramList_UIMF(data) { Dp = dp };

        result.Run.SpectrumList = spectrumList;
        result.Run.ChromatogramList = chromatogramList;
    }
}
