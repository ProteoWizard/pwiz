using SCIEX.Apis.Data.v1;
using SCIEX.Apis.Data.v1.Contracts;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Sciex.Wiff2;

/// <summary>
/// <see cref="AbstractWiffFile"/> implementation backed by the modern <see cref="ISampleDataApi"/> SDK
/// for <c>.wiff2</c> files. C# equivalent of pwiz cpp <c>WiffFile2Impl</c>. Lives in the
/// side-by-side <see cref="Wiff2LoadContext"/> so its compile-time references to bundled
/// (PKT=null) <c>SCIEX.Apis.Data.v1.Contracts</c> resolve correctly without conflicting with
/// the legacy <c>.wiff</c> path's signed Clearcore2 dlls in the default ALC.
/// </summary>
internal sealed class Wiff2File : AbstractWiffFile
{
    // The cpp WiffFile2.ipp ships this license key in source; we re-use it.
    private const string LicenseKey =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
        + "<license_key>"
        + "<company_name>Proteowizard</company_name>"
        + "<product_name>Sciex Data API</product_name>"
        + "<features />"
        + "<key_data>t6QaoUk9a7EedqZ/V/WAE98aSv1Z0tgvmnYXSveHSvLNChvDdMXh3A==</key_data>"
        + "</license_key>";

    private readonly ISampleDataApi _api;
    private readonly List<ISample> _allSamples;
    private readonly ISample _msSample;
    private readonly Wiff2Experiment[] _experiments;
    private bool _disposed;

    public override string WiffPath { get; }
    public override int SampleNumber { get; }
    public override int SampleCount => _allSamples.Count;
    public override string SampleName => _msSample.SampleName ?? string.Empty;
    public override int ExperimentCount => _experiments.Length;
    public override AbstractWiffExperiment GetExperiment(int experimentIndex) => _experiments[experimentIndex];

    public override string? StartTimestampUtc
    {
        get
        {
            try
            {
                if (string.IsNullOrEmpty(_msSample.StartTimestamp)) return null;
                if (DateTime.TryParse(_msSample.StartTimestamp,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                        out var dt))
                    return dt.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { }
            return null;
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

        var factory = new DataApiFactory { LicenseKey = LicenseKey };
        _api = factory.CreateSampleDataApi()
            ?? throw new InvalidOperationException("CreateSampleDataApi returned null");

        var sampleRequest = _api.RequestFactory.CreateSamplesReadRequest();
        sampleRequest.AbsolutePathToWiffFile = Path.GetFullPath(wiff2Path);

        _allSamples = new List<ISample>();
        var sampleReader = _api.GetSamples(sampleRequest);
        while (sampleReader.MoveNext()) _allSamples.Add(sampleReader.GetCurrent());
        if (_allSamples.Count == 0) throw new InvalidDataException($"WIFF2 reports zero samples: {wiff2Path}");
        if (sampleIndex0 < 0 || sampleIndex0 >= _allSamples.Count)
            throw new ArgumentOutOfRangeException(nameof(sampleIndex0),
                $"sample index {sampleIndex0} out of [0, {_allSamples.Count})");
        SampleNumber = sampleIndex0 + 1;
        _msSample = _allSamples[sampleIndex0];

        var experimentRequest = _api.RequestFactory.CreateExperimentsReadRequest(_msSample.Id, true);
        var sdkExperiments = new List<IExperiment>();
        var experimentReader = _api.GetExperiments(experimentRequest);
        while (experimentReader.MoveNext()) sdkExperiments.Add(experimentReader.GetCurrent());
        _experiments = new Wiff2Experiment[sdkExperiments.Count];
        for (int i = 0; i < _experiments.Length; i++)
            _experiments[i] = new Wiff2Experiment(_api, _msSample, sdkExperiments[i]);
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_msSample.Sources is { Length: > 0 } sources) _api.CloseFile(sources[0]);
        }
        catch { }
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

    public Wiff2Experiment(ISampleDataApi api, ISample sample, IExperiment exp)
    {
        _api = api;
        _sample = sample;
        _exp = exp;
    }

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
        return sdkSpec is null ? null : new Wiff2Spectrum(sdkSpec, _exp);
    }

    public override (double[] Times, double[] Intensities) GetBpc()
    {
        // wiff2 SDK doesn't expose BPC (cpp WiffFile2.ipp:700-710 returns empty).
        return (Array.Empty<double>(), Array.Empty<double>());
    }

    public override (double[] Times, double[] Intensities) GetTic()
    {
        try
        {
            var req = _api.RequestFactory.CreateExperimentTicReadRequest();
            req.SampleId = _sample.Id;
            req.ExperimentId = _exp.Id;
            var tic = _api.GetExperimentTic(req);
            return (tic.XValues ?? Array.Empty<double>(),
                    tic.YValues ?? Array.Empty<double>());
        }
        catch { return (Array.Empty<double>(), Array.Empty<double>()); }
    }

    // wiff2 has no MRM/SIM transitions (cpp WiffFile2 returns 0).
    public override IReadOnlyList<WiffMrmTarget> SrmTransitions => Array.Empty<WiffMrmTarget>();
    public override IReadOnlyList<WiffSimTarget> SimTransitions => Array.Empty<WiffSimTarget>();
    public override (double[] Times, double[] Intensities) GetSic(int transitionIndex)
        => (Array.Empty<double>(), Array.Empty<double>());

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

    public Wiff2Spectrum(ISpectrum sdk, IExperiment exp)
    {
        _sdk = sdk;
        _exp = exp;
        _precursor = sdk.Precursor;
        _iso = _precursor?.IsolationWindow;
    }

    // wiff2 always reports profile data in our pipeline; SDK-side centroiding is opt-in via
    // ConvertToCentroid (we currently never set it true), so treat the cached spectrum as profile.
    public override bool CentroidMode => false;
    public override double[] XValues => _sdk.XValues ?? Array.Empty<double>();
    public override double[] YValues => _sdk.YValues ?? Array.Empty<double>();

    public override bool HasPrecursorInfo => _iso is not null && _iso.IsolationWindowTarget != 0;
    public override double PrecursorMz => _iso?.IsolationWindowTarget ?? 0;
    public override int PrecursorCharge => _precursor?.PrecursorChargeState ?? 0;

    public override double CollisionEnergy
    {
        get
        {
            var ce = _precursor?.CollisionEnergy;
            if (ce is null) return 0;
            double rampStart = ce.CollisionEnergyRampStart;
            double rampEnd = ce.CollisionEnergyRampEnd;
            double collisionEnergy;
            if (rampStart == 0) collisionEnergy = rampEnd;
            else if (rampEnd == 0) collisionEnergy = rampStart;
            else collisionEnergy = (rampEnd + rampStart) / 2;
            return Math.Abs(collisionEnergy);
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

    public override double IsolationLowerOffset => _iso?.LowerOffset ?? 0;
    public override double IsolationUpperOffset => _iso?.UpperOffset ?? 0;
    public override double ElectronKineticEnergy => _exp.ElectronKe ?? 0;
}
