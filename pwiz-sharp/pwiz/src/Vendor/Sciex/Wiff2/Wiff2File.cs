using Pwiz.Util.Misc;
using SCIEX.Apis.Data.v1;
using SCIEX.Apis.Data.v1.Contracts;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Sciex.Wiff2;

/// <summary>
/// The one <see cref="ISampleDataApi"/> for the process, and the per-path reader counts that
/// decide who closes a file.
/// </summary>
/// <remarks>
/// <para><b>One api.</b> cpp's <c>WiffFile2.ipp</c> keeps a function-local
/// <c>static gcroot&lt;ISampleDataApi^&gt;</c> and we do the same, because an api per reader
/// leaks: every <c>CreateSampleDataApi</c> leaves a
/// <c>Clearcore2.RFLight.SampleDataProvider.SampleDataProviderServer</c> rooted by its own
/// periodic timer, and the SDK offers no shutdown - neither <see cref="ISampleDataApi"/> nor
/// <c>DataApiFactory</c> is <see cref="IDisposable"/>, and <c>CloseFile</c> closes a file, not
/// the server. Measured at ~34 KB per open, linear, with nothing able to release it.</para>
/// <para><b>Counts per path.</b> Sharing the api makes arbitration necessary: the SDK keeps one
/// pooled storage location (a SQLite connection) per path, and <c>CloseFile</c> purges it for
/// every reader on that path at once - which a reader with a request in flight sees as an
/// <c>ObjectDisposedException</c> or a SQLite misuse error, and the catch blocks in
/// <see cref="Wiff2File"/> would turn into empty spectra rather than a failure. Counting readers
/// per path, and closing only when the last one goes, removes that: when the count reaches zero
/// there is by definition no one left mid-request. Skyline reaches the dangerous shape whenever
/// it imports a multi-sample file as one replicate per sample, because this reader is one per
/// SAMPLE where cpp's is one per FILE.</para>
/// <para>Readers on different paths are deliberately NOT serialized against each other - they
/// share the api concurrently, as cpp's have for years.</para>
/// </remarks>
internal static class Wiff2Sdk
{
    // The cpp WiffFile2.ipp ships this license key in source; we re-use it.
    private const string LICENSE_KEY =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
        + "<license_key>"
        + "<company_name>Proteowizard</company_name>"
        + "<product_name>Sciex Data API</product_name>"
        + "<features />"
        + "<key_data>t6QaoUk9a7EedqZ/V/WAE98aSv1Z0tgvmnYXSveHSvLNChvDdMXh3A==</key_data>"
        + "</license_key>";

    /// <summary>
    /// Guards <see cref="s_paths"/> only, and is held for a dictionary operation and nothing
    /// else. Separate from <see cref="s_apiGate"/> on purpose: a reader's finalizer releases its
    /// claim under this lock, and the finalizer thread must not be able to stall behind the SDK
    /// initialization that creating the api runs.
    /// </summary>
    private static readonly object s_gate = new();

    /// <summary>Guards creating <see cref="s_api"/>, which can be slow.</summary>
    private static readonly object s_apiGate = new();

    private static ISampleDataApi? s_api;

    /// <summary>
    /// Per-path reader count and the sources to close when it reaches zero. Entries are never
    /// removed: the state for a path has to survive a count returning to zero, since the next
    /// reader on it starts the cycle again. The residue is one small entry per distinct path
    /// opened in the process.
    /// </summary>
    private static readonly Dictionary<string, PathState> s_paths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The shared api, created on first use. Deliberately not a <see cref="Lazy{T}"/> with
    /// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>: that caches the factory's
    /// exception forever, so one transient failure - and this SDK's initialization is known to
    /// fail transiently on assembly resolution - would disable .wiff2 for the rest of the
    /// process. The api-per-open code this replaced recovered on the next attempt, and so does
    /// this: a throw leaves <see cref="s_api"/> null.
    /// </summary>
    internal static ISampleDataApi Api
    {
        get
        {
            lock (s_apiGate)
            {
                return s_api ??= new DataApiFactory { LicenseKey = LICENSE_KEY }.CreateSampleDataApi()
                    ?? throw new InvalidOperationException("CreateSampleDataApi returned null");
            }
        }
    }

    /// <summary>Counts a reader as open on <paramref name="sdkPath"/>.</summary>
    internal static void AddReader(string sdkPath)
    {
        lock (s_gate)
            StateFor(sdkPath).OpenCount++;
    }

    /// <summary>
    /// Records sources for the last reader on <paramref name="sdkPath"/> to close.
    /// </summary>
    /// <remarks>
    /// The count is per PATH but <c>ISample.Sources</c> is per SAMPLE, and a multi-sample file is
    /// read as one reader per sample - so a reader that is not the last one out would otherwise
    /// close nothing and its sample's sources would never be released at all. Collecting them
    /// here hands every one of them to whoever does close.
    /// </remarks>
    internal static void AddSources(string sdkPath, ISourceFile[]? sources)
    {
        if (sources is null || sources.Length == 0)
            return;
        lock (s_gate)
        {
            var known = StateFor(sdkPath).Sources;
            foreach (var source in sources)
            {
                if (source is not null && !known.Contains(source))
                    known.Add(source);
            }
        }
    }

    /// <summary>
    /// Drops a reader from <paramref name="sdkPath"/>, returning the sources to close when it was
    /// the last one there, and null when other readers remain or there is nothing left to close.
    /// </summary>
    internal static List<ISourceFile>? RemoveReader(string sdkPath)
    {
        lock (s_gate)
        {
            // A path with no state, or a count already at zero, means nothing is open: a reader
            // whose constructor never claimed, or a second Dispose racing the first. Closing on
            // that basis would purge the pool under whoever opened the path next.
            if (!s_paths.TryGetValue(sdkPath, out var state) || state.OpenCount == 0)
                return null;
            if (--state.OpenCount > 0)
                return null;
            var sources = state.Sources;
            state.Sources = new List<ISourceFile>();
            return sources.Count > 0 ? sources : null;
        }
    }

    /// <summary>The path's state, created on first use. Caller holds <see cref="s_gate"/>.</summary>
    private static PathState StateFor(string sdkPath)
    {
        if (!s_paths.TryGetValue(sdkPath, out var state))
            s_paths[sdkPath] = state = new PathState();
        return state;
    }

    private sealed class PathState
    {
        internal int OpenCount;
        internal List<ISourceFile> Sources = new();
    }
}

/// <summary>
/// <see cref="AbstractWiffFile"/> implementation backed by the modern <see cref="ISampleDataApi"/> SDK
/// for <c>.wiff2</c> files. C# equivalent of pwiz cpp <c>WiffFile2Impl</c>. Lives in the
/// side-by-side <see cref="Wiff2LoadContext"/> so its compile-time references to bundled
/// (PKT=null) <c>SCIEX.Apis.Data.v1.Contracts</c> resolve correctly without conflicting with
/// the legacy <c>.wiff</c> path's signed Clearcore2 dlls in the default ALC.
/// </summary>
internal sealed class Wiff2File : AbstractWiffFile
{
    /// <summary>
    /// The path handed to the SDK, which is what the SDK pools its storage location under and so
    /// what <see cref="Wiff2Sdk"/> counts readers by. Not <see cref="WiffPath"/>, which keeps the
    /// caller's spelling for metadata. Note this is a normalized full path, not a canonical file
    /// identity: two spellings that <c>Path.GetFullPath</c> does not reconcile (a
    /// long-path prefix, a subst drive, a junction) still count separately.
    /// </summary>
    private readonly string _sdkPath;
    private readonly List<ISample> _allSamples;
    private readonly ISample _msSample;
    private readonly Wiff2Experiment[] _experiments;
    private int _disposed;

    public override string WiffPath { get; }
    public override int SampleNumber { get; }
    public override int SampleCount => _allSamples.Count;
    /// <summary>
    /// The open sample's name, with cpp's duplicate-count suffix applied when another sample in
    /// the file shares it (<c>WiffFile2.ipp:373-392</c> builds the same list). Taken from the
    /// whole sample list rather than <c>_msSample.SampleName</c>, since a duplicate's " (2)"
    /// can only be known from the names preceding it. Cached: the list does not change.
    /// </summary>
    public override string SampleName => _uniqueSampleName ??= ResolveUniqueSampleName();
    private string? _uniqueSampleName;

    public override string[] AllSampleNames
    {
        get
        {
            var names = new List<string>(_allSamples.Count);
            foreach (var s in _allSamples)
                names.Add(s.SampleName ?? string.Empty);
            return DisambiguateSampleNames(names);
        }
    }

    private string ResolveUniqueSampleName()
    {
        var unique = AllSampleNames;
        int index0 = SampleNumber - 1;
        return index0 >= 0 && index0 < unique.Length
            ? unique[index0]
            : _msSample.SampleName ?? string.Empty;
    }
    public override int ExperimentCount => _experiments.Length;
    public override AbstractWiffExperiment GetExperiment(int experimentIndex) => _experiments[experimentIndex];

    public override DateTime StartTimestampRaw
    {
        get
        {
            try
            {
                if (string.IsNullOrEmpty(_msSample.StartTimestamp)) return default;
                // cpp WiffFile2.ipp:484-500 parses the SDK timestamp with DateTime::Parse —
                // which for TZ-tagged input converts to system local — and then takes those
                // LOCAL components as a zone-less ptime. Whether that gets shifted to the host
                // zone afterwards is the caller's decision (Reader_ABI passes the config flag),
                // so this returns the parsed value and does not format or shift it.
                if (DateTime.TryParse(_msSample.StartTimestamp,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out var dt))
                    return dt;
            }
            catch { }
            return default;
        }
    }

    public override string? InstrumentModelName
    {
        get
        {
            try
            {
                foreach (var det in _msSample.InstrumentDetails ?? Array.Empty<IInstrumentDetail>())
                {
                    if (det.DeviceType == 0) return det.DeviceModelName;
                }
            }
            catch { }
            return null;
        }
    }

    public override string? InstrumentSerialNumber
    {
        get
        {
            try
            {
                foreach (var det in _msSample.InstrumentDetails ?? Array.Empty<IInstrumentDetail>())
                {
                    if (det.DeviceType == 0) return det.SerialNumber;
                }
            }
            catch { }
            return null;
        }
    }

    // wiff2 has no ADC traces or DAD data — cpp WiffFile2 always returns 0 / empty.
    public override int AdcChannelCount => 0;
    public override string GetAdcChannelName(int channelIndex) => string.Empty;
    public override (double[] Times, double[] Intensities) GetAdcTrace(int channelIndex)
        => (Array.Empty<double>(), Array.Empty<double>());
    public override bool HasDadData => false;
    public override (double[] Times, double[] Intensities) GetTotalWavelengthChromatogram()
        => (Array.Empty<double>(), Array.Empty<double>());

    public Wiff2File(string wiff2Path, int sampleIndex0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wiff2Path);
        if (!File.Exists(wiff2Path)) throw new FileNotFoundException("WIFF2 not found", wiff2Path);
        WiffPath = wiff2Path;
        // The SCIEX.Apis SDK derives the sample/experiment/storage-location ids from this path
        // and resolves them through an ANSI native file layer (Clearcore2.SampleData
        // FileIdGenerator). A path with characters outside the current code page mangles that
        // lookup -> FileNotFoundException on the first GetExperiments/GetSpectra call. Feed it the
        // Windows 8.3 short name for any non-ASCII component (cpp's WiffFile2 sidesteps this by
        // handing the SDK a narrow ANSI-code-page std::string). WiffPath keeps the original for
        // metadata / SourceFile emission.
        _sdkPath = Filesystem.GetNonUnicodePath(Path.GetFullPath(wiff2Path));

        // Claim the path before the first SDK call rather than after the reads below have
        // succeeded, so that another reader's Dispose cannot close the file while this
        // constructor is between GetSamples and GetExperiments.
        Wiff2Sdk.AddReader(_sdkPath);
        try
        {
            var api = Wiff2Sdk.Api;
            var sampleRequest = api.RequestFactory.CreateSamplesReadRequest();
            sampleRequest.AbsolutePathToWiffFile = _sdkPath;

            _allSamples = new List<ISample>();
            var sampleReader = api.GetSamples(sampleRequest);
            while (sampleReader.MoveNext()) _allSamples.Add(sampleReader.GetCurrent());
            if (_allSamples.Count == 0) throw new InvalidDataException($"WIFF2 reports zero samples: {wiff2Path}");
            // Register before the range check: GetSamples has already opened the file, so a throw
            // from here on still has something to release. cpp closes allSamples[0]'s first
            // source for the same reason - it is the file-level handle.
            Wiff2Sdk.AddSources(_sdkPath, _allSamples[0].Sources);
            if (sampleIndex0 < 0 || sampleIndex0 >= _allSamples.Count)
                throw new ArgumentOutOfRangeException(nameof(sampleIndex0),
                    $"sample index {sampleIndex0} out of [0, {_allSamples.Count})");
            SampleNumber = sampleIndex0 + 1;
            _msSample = _allSamples[sampleIndex0];
            Wiff2Sdk.AddSources(_sdkPath, _msSample.Sources);

            var experimentRequest = api.RequestFactory.CreateExperimentsReadRequest(_msSample.Id, true);
            var sdkExperiments = new List<IExperiment>();
            var experimentReader = api.GetExperiments(experimentRequest);
            while (experimentReader.MoveNext()) sdkExperiments.Add(experimentReader.GetCurrent());
            var ztBins = BuildZtScanBins(sdkExperiments);
            _experiments = new Wiff2Experiment[sdkExperiments.Count];
            for (int i = 0; i < _experiments.Length; i++)
                _experiments[i] = new Wiff2Experiment(api, _msSample, sdkExperiments[i], ztBins?[i]);
        }
        catch
        {
            // Release the claim, and close the file if that leaves nobody on this path. The
            // sources registered above are exactly what makes this possible: a constructor that
            // throws never gets a Dispose, so this is the only chance to unlock the file.
            CloseIfLastReader();
            throw;
        }
    }

    /// <summary>
    /// Detects a ZT Scan acquisition and maps each TOFMSMS experiment to its bin within the
    /// quadrupole sweep; null for anything that is not a ZT Scan. See <see cref="ZtScanBin"/>.
    ///
    /// Unlike the legacy container, wiff2 stores the CE ramp endpoints verbatim
    /// (CERampStart / CERampStop) alongside a "ZTScan" group name, so no reconstruction from the
    /// lossy CE / CES pair is needed here. Both are programmatic method keys rather than rendered
    /// labels, so this detection is not sensitive to the acquisition software's language.
    /// </summary>
    private ZtScanBin?[]? BuildZtScanBins(List<IExperiment> sdkExperiments)
    {
        try
        {
            var api = Wiff2Sdk.Api;
            var method = api.GetMsMethodParameters(api.RequestFactory.CreateMethodParametersReadRequest(_msSample.Id));
            if (method?.Experiments is null) return null;

            double rampStart = 0, rampEnd = 0;
            bool isZtScan = false, haveRamp = false;
            foreach (var methodExperiment in method.Experiments)
            {
                if (!string.Equals(methodExperiment.GroupName, "ZTScan", StringComparison.OrdinalIgnoreCase)) continue;
                isZtScan = true;
                if (TryReadParameter(methodExperiment, "CERampStart", out rampStart)
                    && TryReadParameter(methodExperiment, "CERampStop", out rampEnd))
                {
                    haveRamp = true;
                    break;
                }
            }
            if (!isZtScan || !haveRamp || rampStart == rampEnd) return null;

            var productIndices = new List<int>();
            for (int i = 0; i < sdkExperiments.Count; i++)
                if (sdkExperiments[i].ScanType == "TOFMSMS") productIndices.Add(i);
            // One bin is not a sweep; leave such a file on the SDK's value.
            if (productIndices.Count < 2) return null;

            var bins = new ZtScanBin?[sdkExperiments.Count];
            for (int bin = 0; bin < productIndices.Count; bin++)
                bins[productIndices[bin]] = new ZtScanBin(rampStart, rampEnd, bin, productIndices.Count);
            return bins;
        }
        catch { return null; }
    }

    /// <summary>Reads a named MS-method parameter as a double; false when absent or unparsable.</summary>
    private static bool TryReadParameter(
        SCIEX.Apis.Data.v1.Contracts.MethodParameters.IExperiment methodExperiment, string key, out double value)
    {
        value = 0;
        if (methodExperiment.Parameters is null) return false;
        foreach (var parameter in methodExperiment.Parameters)
        {
            if (!string.Equals(parameter.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
            if (parameter.Values is null) continue;
            foreach (var raw in parameter.Values)
            {
                if (raw is null) continue;
                if (raw is IConvertible)
                {
                    try
                    {
                        value = Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture);
                        return true;
                    }
                    catch { }
                }
                if (double.TryParse(raw.ToString(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out value))
                    return true;
            }
        }
        return false;
    }

    public override void Dispose()
    {
        CloseIfLastReader();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A reader dropped without <see cref="Dispose"/> would pin this path's reader count above
    /// zero forever, and no later reader on the path could ever close the file. The legacy
    /// <c>WiffFile</c> carries the same guard for the same reason.
    /// </summary>
    ~Wiff2File()
    {
        CloseIfLastReader();
    }

    /// <summary>
    /// Drops this reader's claim on the path and, if it was the last one, closes every source any
    /// reader on that path reported. Safe to call twice and from the finalizer.
    /// </summary>
    private void CloseIfLastReader()
    {
        // Interlocked rather than a bool: Dispose can race itself (a cancelled import unwinding
        // while the owning document is torn down), and a double release would drop the count
        // twice and purge the SDK's pooled storage location under a live sibling reader.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        var sources = Wiff2Sdk.RemoveReader(_sdkPath);
        if (sources is null)
            return;
        // Close every source, not just the first — multi-source wiff2 files (rare but the SDK
        // reports them) leave handles dangling otherwise.
        foreach (var src in sources)
        {
            try { Wiff2Sdk.Api.CloseFile(src); } catch { }
        }
    }
}

/// <summary><see cref="AbstractWiffExperiment"/> backed by the modern SCIEX <see cref="IExperiment"/>.</summary>
internal sealed class Wiff2Experiment : AbstractWiffExperiment
{
    private readonly ISampleDataApi _api;
    private readonly ISample _sample;
    private readonly IExperiment _exp;
    private (int[] Cycles, double[] RetentionTimes)? _cyclesCache;
    private bool _retentionFetchFailed;

    // Static flags mirroring cpp's framingZerosThrowsError / doCentroidThrowsError: once a
    // particular SDK option throws, disable it for the rest of the run rather than retrying.
    private static bool s_framingZerosThrowsError;
    private static bool s_doCentroidThrowsError;

    public Wiff2Experiment(ISampleDataApi api, ISample sample, IExperiment exp, ZtScanBin? ztScan = null)
    {
        _api = api;
        _sample = sample;
        _exp = exp;
        ZtScan = ztScan;
    }

    /// <inheritdoc/>
    public override ZtScanBin? ZtScan { get; }

    public override WiffExperimentType ExperimentType => _exp.ScanType switch
    {
        "TOFMS" => WiffExperimentType.MS,
        "TOFMSMS" => WiffExperimentType.Product,
        "MRM" => WiffExperimentType.MRM,
        "SIM" => WiffExperimentType.SIM,
        _ => WiffExperimentType.MS,
    };

    public override WiffPolarity Polarity => _exp.IsPositivePolarityScan ? WiffPolarity.Positive : WiffPolarity.Negative;

    public override double StartMass
    {
        get
        {
            if (_exp.MassRanges is { Length: > 0 } ranges && ranges[0]?.SelectionWindow is IScanWindow sw)
                return sw.Start;
            return 0;
        }
    }

    public override double EndMass
    {
        get
        {
            if (_exp.MassRanges is { Length: > 0 } ranges && ranges[0]?.SelectionWindow is IScanWindow sw)
                return sw.End;
            return 0;
        }
    }

    public override int CycleCount
    {
        get
        {
            EnsureCyclesLoaded();
            return _cyclesCache?.Cycles.Length ?? 0;
        }
    }

    public override double GetRetentionTime(int cycle1Based)
    {
        EnsureCyclesLoaded();
        if (_cyclesCache is { } c && cycle1Based >= 1 && cycle1Based <= c.RetentionTimes.Length)
            return c.RetentionTimes[cycle1Based - 1];
        return 0;
    }

    public override int GetMsLevelForCycle(int cycle1Based) => _exp.MsLevel <= 0 ? 1 : _exp.MsLevel;

    public override AbstractWiffSpectrum? GetSpectrum(int cycle1Based, bool addZeros, bool centroid)
    {
        double scanTime = GetRetentionTime(cycle1Based);
        var sdkSpec = FetchSpectrumWithRetry(scanTime, addZeros, centroid);
        return sdkSpec is null ? null : new Wiff2Spectrum(sdkSpec, _exp, scanTime, ZtScan);
    }

    public override (double[] Times, double[] Intensities) GetBpc()
    {
        // wiff2 SDK doesn't expose BPC (cpp WiffFile2.ipp:700-710 returns empty).
        return (Array.Empty<double>(), Array.Empty<double>());
    }

    private (double[] Times, double[] Intensities)? _ticCache;
    public override (double[] Times, double[] Intensities) GetTic()
    {
        if (_ticCache is { } cached) return cached;
        try
        {
            var req = _api.RequestFactory.CreateExperimentTicReadRequest();
            req.SampleId = _sample.Id;
            req.ExperimentId = _exp.Id;
            var tic = _api.GetExperimentTic(req);
            _ticCache = (tic.XValues ?? Array.Empty<double>(),
                         tic.YValues ?? Array.Empty<double>());
            return _ticCache.Value;
        }
        catch
        {
            _ticCache = (Array.Empty<double>(), Array.Empty<double>());
            return _ticCache.Value;
        }
    }

    // wiff2 has no MRM/SIM transitions (cpp WiffFile2 returns 0).
    public override IReadOnlyList<WiffMrmTarget> SrmTransitions => Array.Empty<WiffMrmTarget>();
    public override IReadOnlyList<WiffSimTarget> SimTransitions => Array.Empty<WiffSimTarget>();
    public override (double[] Times, double[] Intensities) GetSic(int transitionIndex)
        => (Array.Empty<double>(), Array.Empty<double>());

    // wiff2 SDK doesn't surface per-cycle base-peak metadata; cpp's WiffFile2 path
    // skips emitting MS_base_peak_* on wiff2 spectra. Returning null lets the
    // SpectrumList omit those CV params (matching the cpp wiff2 references).
    public override (double Mz, double Intensity)? GetBasePeak(int cycle1Based) => null;

    private void EnsureCyclesLoaded()
    {
        if (_cyclesCache is not null || _retentionFetchFailed) return;
        try
        {
            var req = _api.RequestFactory.CreateExperimentCyclesReadRequest();
            req.SampleId = _sample.Id;
            req.ExperimentId = _exp.Id;
            var resp = _api.GetExperimentCycles(req);
            _cyclesCache = (resp.Cycles ?? Array.Empty<int>(),
                            resp.RetentionTimes ?? Array.Empty<double>());
        }
        catch
        {
            _retentionFetchFailed = true;
            _cyclesCache = (Array.Empty<int>(), Array.Empty<double>());
        }
    }

    /// <summary>cpp <c>Spectrum2Impl::getSpectrumWithOptions</c> port: fall back when
    /// AddFramingZeros / ConvertToCentroid throw and disable that option for the rest of the run.</summary>
    private ISpectrum? FetchSpectrumWithRetry(double scanTime, bool addZeros, bool centroid)
    {
        addZeros = addZeros && !s_framingZerosThrowsError;
        centroid = centroid && !s_doCentroidThrowsError;
        try
        {
            return FetchSpectrum(scanTime, addZeros, centroid);
        }
        catch (Exception)
        {
            if (addZeros) { s_framingZerosThrowsError = true; return FetchSpectrumWithRetry(scanTime, false, centroid); }
            if (centroid) { s_doCentroidThrowsError = true; return FetchSpectrumWithRetry(scanTime, addZeros, false); }
            throw;
        }
    }

    private ISpectrum? FetchSpectrum(double scanTime, bool addZeros, bool centroid)
    {
        var req = _api.RequestFactory.CreateSpectraReadRequest();
        req.SampleId = _sample.Id;
        req.ExperimentId = _exp.Id;
        req.Range.Start = scanTime;
        req.Range.End = scanTime;
        req.AddFramingZeros = addZeros ? 1 : 0;
        req.ConvertToCentroid = centroid;
        req.CentroidOption = SCIEX.Apis.Data.v1.Types.CentroidOptions.IntensitySumAbove50Percent;
        var reader = _api.GetSpectra(req);
        return reader.MoveNext() ? reader.GetCurrent() : null;
    }
}

/// <summary><see cref="AbstractWiffSpectrum"/> backed by the modern SCIEX <see cref="ISpectrum"/>.</summary>
internal sealed class Wiff2Spectrum : AbstractWiffSpectrum
{
    private readonly ISpectrum _sdk;
    private readonly IExperiment _exp;
    private readonly IPrecursor? _precursor;
    private readonly IIsolationWindow? _iso;
    private readonly double _scanTime;

    private readonly ZtScanBin? _ztScan;

    public Wiff2Spectrum(ISpectrum sdk, IExperiment exp, double scanTime, ZtScanBin? ztScan = null)
    {
        _sdk = sdk;
        _exp = exp;
        _scanTime = scanTime;
        _precursor = sdk.Precursor;
        _iso = _precursor?.IsolationWindow;
        _ztScan = ztScan;
    }

    // wiff2 always reports profile data in our pipeline; SDK-side centroiding is opt-in via
    // ConvertToCentroid (we currently never set it true), so treat the cached spectrum as profile.
    public override bool CentroidMode => false;
    public override double[] XValues => _sdk.XValues ?? Array.Empty<double>();
    public override double[] YValues => _sdk.YValues ?? Array.Empty<double>();

    public override bool HasPrecursorInfo => _iso is not null && _iso.IsolationWindowTarget != 0;
    public override double PrecursorMz => _iso?.IsolationWindowTarget ?? 0;
    public override int PrecursorCharge => _precursor?.PrecursorChargeState ?? 0;

    // cpp WiffFile2.ipp:732 — `Spectrum2Impl::getHasIsolationInfo()` is
    // `experiment->experimentType == Product`, i.e. only a "TOFMSMS" scan carries isolation
    // info. cpp's SpectrumList_ABI.cpp:172 gates the whole getIsolationInfo call on it, so a
    // non-TOFMSMS experiment emits no collision energy even when the SDK's ISpectrum happens to
    // carry a Precursor with a CE ramp (which is exactly what a CE-optimization acquisition
    // looks like — see CEOptPGMOG_redo.wiff2, where C# used to emit CE and cpp emitted none).
    public override bool HasIsolationInfo => _exp.ScanType == "TOFMSMS";

    // cpp WiffFile2.ipp:734-757 (Spectrum2Impl::getIsolationInfo). Note cpp's early returns:
    // a null Precursor, a null IsolationWindow, or a null CollisionEnergy all leave
    // collisionEnergy at its 0 initializer. cpp does NOT take fabs here (unlike the legacy
    // path), and SpectrumList_ABI.cpp:223 then drops any non-positive value.
    public override double CollisionEnergy
    {
        get
        {
            if (!HasIsolationInfo || _precursor is null || _iso is null) return 0;
            // A ZT Scan bin's CE is a point on a hardware ramp the SDK does not record per bin —
            // ISpectrum.Precursor.CollisionEnergy reports the ramp MIDPOINT on every bin of the
            // sweep. Reconstruct it from the method's endpoints instead. See ZtScanBin.
            if (_ztScan is not null) return _ztScan.CollisionEnergy;
            var ce = _precursor.CollisionEnergy;
            if (ce is null) return 0;
            double rampStart = ce.CollisionEnergyRampStart;
            double rampEnd = ce.CollisionEnergyRampEnd;
            if (rampStart == 0) return rampEnd;
            if (rampEnd == 0) return rampStart;
            return (rampEnd + rampStart) / 2;
        }
    }

    public override WiffActivation Activation
    {
        get
        {
            var fragMode = _exp.FragmentationMode;
            if (fragMode.HasValue
                && (fragMode.Value == SCIEX.Apis.Data.v1.Types.FragmentationMode.EAD
                    || fragMode.Value == SCIEX.Apis.Data.v1.Types.FragmentationMode.EAD_Conventional_Trapping))
                return WiffActivation.EAD;
            return WiffActivation.CID;
        }
    }

    // The wiff2 SDK's IIsolationWindow.LowerOffset/UpperOffset are misnamed — they're absolute
    // m/z bounds of the isolation window, not offsets from the target m/z. mzML expects
    // half-window-widths from the target. cpp's SpectrumList_ABI guards on both bounds being
    // > 0 before emitting offsets (ABI/SpectrumList_ABI.cpp:203); mirror that here so
    // unset/zero SDK values (which signal "no isolation window specified") don't produce
    // a bogus offset == target_m_z.
    public override double IsolationLowerOffset =>
        _iso is null || _iso.LowerOffset <= 0 || _iso.UpperOffset <= 0
            ? 0
            : Math.Max(0, _iso.IsolationWindowTarget - _iso.LowerOffset);
    public override double IsolationUpperOffset =>
        _iso is null || _iso.LowerOffset <= 0 || _iso.UpperOffset <= 0
            ? 0
            : Math.Max(0, _iso.UpperOffset - _iso.IsolationWindowTarget);
    public override double ElectronKineticEnergy => _exp.ElectronKe ?? 0;

    // cpp WiffFile2.ipp:194 — `Spectrum2Impl::getStartTime() { return scanTime; }`, where
    // scanTime is the cycle retention time the spectra read request was issued with. The wiff2
    // SDK has no per-spectrum StartRT of its own, so this IS the spectrum's start time; report
    // it here rather than leaving SpectrumList_Sciex to fall back to the experiment-cycle RT
    // (cpp has no such fallback — see the note there).
    public override double StartTimeMinutes => _scanTime;

    // Per the existing comment in SpectrumList_Sciex, the wiff2 SDK doesn't expose
    // per-cycle base-peak metadata; cpp emits these only for legacy WIFF.
    public override (double Mz, double Intensity)? BasePeak => null;

    // wiff2 doesn't go through SrmAsSpectra/SimAsSpectra (the wiff2 reader rejects
    // MRM/SIM experiments outright per cpp SpectrumList_ABI.cpp:288-292), so a
    // wiff2 spectrum's experiment type is never MRM or SIM here. The MS / Product
    // / Precursor distinction doesn't change XValues handling, so default to MS.
    public override WiffExperimentType ExperimentType => WiffExperimentType.MS;
}
