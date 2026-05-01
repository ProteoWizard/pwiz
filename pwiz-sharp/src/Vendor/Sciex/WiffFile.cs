using Clearcore2.Data;
using Clearcore2.Data.AnalystDataProvider;
using Clearcore2.Data.DataAccess.SampleData;
using Pwiz.Data.Common.Params;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Sciex;

/// <summary>
/// <see cref="WiffFile"/> implementation backed by the .NET-Framework-era
/// <see cref="AnalystWiffDataProvider"/> for <c>.wiff</c> files. C# equivalent of pwiz cpp
/// <c>WiffFileImpl</c>. Lives in the default ALC alongside the signed Clearcore2 dlls.
/// </summary>
internal sealed class WiffFile : AbstractWiffFile
{
    private readonly AnalystWiffDataProvider _provider;
    private readonly Sample _sample;
    private readonly MassSpectrometerSample _msSample;
    private readonly WiffExperiment[] _experiments;
    private readonly string _sampleName = string.Empty;
    private bool _disposed;

    public override string WiffPath { get; }
    public override int SampleNumber { get; }
    public override int SampleCount { get; }
    public override string SampleName => _sampleName;
    public override int ExperimentCount => _experiments.Length;
    public override AbstractWiffExperiment GetExperiment(int experimentIndex) => _experiments[experimentIndex];

    public override string? StartTimestampUtc
    {
        get
        {
            try
            {
                return _sample.Details.AcquisitionDateTime
                    .ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }
    }

    public override string? InstrumentModelName
    {
        get
        {
            try { return _msSample.InstrumentName; }
            catch { return null; }
        }
    }

    public override int AdcChannelCount
    {
        get
        {
            if (!_sample.HasADCData) return 0;
            var adc = _sample.ADCSample;
            return adc is null ? 0 : adc.ChannelCount;
        }
    }

    public override string GetAdcChannelName(int channelIndex)
    {
        try { return _sample.ADCSample?.GetChannelNameAt(channelIndex) ?? string.Empty; }
        catch { return string.Empty; }
    }

    public override (double[] Times, double[] Intensities) GetAdcTrace(int channelIndex)
    {
        try
        {
            var data = _sample.ADCSample?.GetADCData(channelIndex);
            if (data is null) return (Array.Empty<double>(), Array.Empty<double>());
            return (data.GetActualXValues() ?? Array.Empty<double>(),
                    data.GetActualYValues() ?? Array.Empty<double>());
        }
        catch { return (Array.Empty<double>(), Array.Empty<double>()); }
    }

    public override bool HasDadData => _sample.HasDADData && _sample.DADSample is not null;

    public override (double[] Times, double[] Intensities) GetTotalWavelengthChromatogram()
    {
        try
        {
            var dad = _sample.DADSample;
            if (dad is null) return (Array.Empty<double>(), Array.Empty<double>());
            var twc = dad.GetTotalWavelengthChromatogram();
            return (twc.GetActualXValues() ?? Array.Empty<double>(),
                    twc.GetActualYValues() ?? Array.Empty<double>());
        }
        catch { return (Array.Empty<double>(), Array.Empty<double>()); }
    }

    public WiffFile(string wiffPath, int sampleIndex0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wiffPath);
        if (!File.Exists(wiffPath)) throw new FileNotFoundException("WIFF not found", wiffPath);
        WiffPath = wiffPath;

        _provider = new AnalystWiffDataProvider();
        SampleCount = _provider.GetNumberOfSamples(wiffPath);
        if (SampleCount == 0) throw new InvalidDataException($"WIFF reports zero samples: {wiffPath}");
        if (sampleIndex0 < 0 || sampleIndex0 >= SampleCount)
            throw new ArgumentOutOfRangeException(nameof(sampleIndex0),
                $"sample index {sampleIndex0} out of [0, {SampleCount})");
        SampleNumber = sampleIndex0 + 1;

        _sample = AnalystDataProviderFactory.CreateSample(wiffPath, sampleIndex0, _provider)
            ?? throw new InvalidDataException($"AnalystDataProviderFactory.CreateSample returned null for {wiffPath}");
        if (!_sample.HasMassSpectrometerData)
            throw new InvalidDataException($"WIFF sample {sampleIndex0} has no MS data: {wiffPath}");
        _msSample = _sample.MassSpectrometerSample;

        // cpp emits run id as "<wiff_base>-<sampleName>" for multi-sample wiffs.
        try
        {
            int idx = 0;
            foreach (var info in _provider.GetBasicSampleInfos(wiffPath))
            {
                if (idx == sampleIndex0) { _sampleName = info.SampleName ?? string.Empty; break; }
                idx++;
            }
        }
        catch { }

        _experiments = new WiffExperiment[_msSample.ExperimentCount];
        for (int i = 0; i < _experiments.Length; i++)
            _experiments[i] = new WiffExperiment(_msSample.GetMSExperiment(i));
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _provider.Close(); } catch { }
    }
}

/// <summary><see cref="AbstractWiffExperiment"/> backed by Clearcore2's <see cref="MSExperiment"/>.</summary>
internal sealed class WiffExperiment : AbstractWiffExperiment
{
    private readonly MSExperiment _exp;
    private readonly MSExperimentInfo _info;
    private readonly Lazy<IReadOnlyList<WiffMrmTarget>> _srm;
    private readonly Lazy<IReadOnlyList<WiffSimTarget>> _sim;

    public WiffExperiment(MSExperiment exp)
    {
        _exp = exp;
        _info = exp.Details;
        _srm = new Lazy<IReadOnlyList<WiffMrmTarget>>(BuildSrmTargets);
        _sim = new Lazy<IReadOnlyList<WiffSimTarget>>(BuildSimTargets);
    }

    public override WiffExperimentType ExperimentType => MapExperimentType(_info.ExperimentType);

    public override WiffPolarity Polarity => _info.Polarity switch
    {
        MSExperimentInfo.PolarityEnum.Positive => WiffPolarity.Positive,
        MSExperimentInfo.PolarityEnum.Negative => WiffPolarity.Negative,
        _ => WiffPolarity.Unknown,
    };

    public override double StartMass => _info.StartMass;
    public override double EndMass => _info.EndMass;

    public override int CycleCount
    {
        get
        {
            try
            {
                var tic = _exp.GetTotalIonChromatogram();
                return tic.GetActualXValues()?.Length ?? 0;
            }
            catch { return 0; }
        }
    }

    public override double GetRetentionTime(int cycle1Based)
    {
        try { return _exp.GetRTFromExperimentCycle(cycle1Based); }
        catch { return 0; }
    }

    public override int GetMsLevelForCycle(int cycle1Based)
    {
        // Mirrors cpp ExperimentImpl::getMsLevel: ask the SDK directly so MRM cycles report
        // msLevel=1 (matching the instrument's actual setting), not the experiment-type default.
        try
        {
            int level = _exp.GetMassSpectrumInfo(cycle1Based - 1).MSLevel;
            if (level > 0) return level;
        }
        catch { }
        return _info.ExperimentType switch
        {
            Clearcore2.Data.DataAccess.SampleData.ExperimentType.MS => 1,
            Clearcore2.Data.DataAccess.SampleData.ExperimentType.SIM => 1,
            Clearcore2.Data.DataAccess.SampleData.ExperimentType.Product => 2,
            Clearcore2.Data.DataAccess.SampleData.ExperimentType.Precursor => 2,
            Clearcore2.Data.DataAccess.SampleData.ExperimentType.NeutralGainOrLoss => 2,
            Clearcore2.Data.DataAccess.SampleData.ExperimentType.MRM => 1,
            _ => 1,
        };
    }

    public override AbstractWiffSpectrum? GetSpectrum(int cycle1Based, bool addZeros, bool centroid)
    {
        try
        {
            var ms = _exp.GetMassSpectrum(cycle1Based - 1);
            // cpp WiffFile.cpp:872-873 calls AddZeros(spectrum, 1) for profile (continuous)
            // data; flanking zero-intensity points give consumers explicit baselines.
            if (addZeros && !ms.Info.CentroidMode)
            {
                try { _exp.AddZeros(ms, 1); }
                catch { /* AddZeros isn't always applicable */ }
            }
            return new WiffSpectrum(ms);
        }
        catch { return null; }
    }

    public override (double[] Times, double[] Intensities) GetBpc()
    {
        // cpp WiffFile.cpp:488-530 has a two-tier fetch: first GetBasePeakChromatogram with no
        // time range; if that throws, retry with a constrained time range up to the
        // second-to-last cycle. Mirror that here so MRM-only experiments still emit a BPC.
        try
        {
            var bpc = _exp.GetBasePeakChromatogram(new BasePeakChromatogramSettings(0, null, null));
            return (bpc.GetActualXValues() ?? Array.Empty<double>(),
                    bpc.GetActualYValues() ?? Array.Empty<double>());
        }
        catch { /* fall through to constrained retry */ }
        try
        {
            var tic = _exp.GetTotalIonChromatogram();
            var ticTimes = tic.GetActualXValues() ?? Array.Empty<double>();
            if (ticTimes.Length > 0)
            {
                int lastUsable = ticTimes.Length > 10 ? ticTimes.Length - 1 : ticTimes.Length;
                var settings = new BasePeakChromatogramSettings(0, null, null, 0, ticTimes[lastUsable - 1]);
                var bpc = _exp.GetBasePeakChromatogram(settings);
                return (bpc.GetActualXValues() ?? Array.Empty<double>(),
                        bpc.GetActualYValues() ?? Array.Empty<double>());
            }
        }
        catch { }
        return (Array.Empty<double>(), Array.Empty<double>());
    }

    public override (double[] Times, double[] Intensities) GetTic()
    {
        try
        {
            var tic = _exp.GetTotalIonChromatogram();
            return (tic.GetActualXValues() ?? Array.Empty<double>(),
                    tic.GetActualYValues() ?? Array.Empty<double>());
        }
        catch { return (Array.Empty<double>(), Array.Empty<double>()); }
    }

    public override IReadOnlyList<WiffMrmTarget> SrmTransitions => _srm.Value;
    public override IReadOnlyList<WiffSimTarget> SimTransitions => _sim.Value;

    public override (double[] Times, double[] Intensities) GetSic(int transitionIndex)
    {
        try
        {
            var sic = _exp.GetExtractedIonChromatogram(new ExtractedIonChromatogramSettings(transitionIndex));
            return (sic.GetActualXValues() ?? Array.Empty<double>(),
                    sic.GetActualYValues() ?? Array.Empty<double>());
        }
        catch { return (Array.Empty<double>(), Array.Empty<double>()); }
    }

    private IReadOnlyList<WiffMrmTarget> BuildSrmTargets()
    {
        if (_info.ExperimentType != Clearcore2.Data.DataAccess.SampleData.ExperimentType.MRM
            || _info.MassRangeInfo is not { Length: > 0 } ranges)
            return Array.Empty<WiffMrmTarget>();
        var targets = new List<WiffMrmTarget>(ranges.Length);
        foreach (var range in ranges)
        {
            if (range is not MRMMassRange mrm) continue;
            double ce = ReadCompoundParameter(mrm.CompoundDepParameters, "CE");
            // cpp: target.startTime = ExpectedRT - RTWindow/2; target.endTime = ExpectedRT + RTWindow/2.
            double start = mrm.ExpectedRT - mrm.RTWindow / 2.0;
            double end = mrm.ExpectedRT + mrm.RTWindow / 2.0;
            targets.Add(new WiffMrmTarget
            {
                Q1Mass = mrm.Q1Mass,
                Q3Mass = mrm.Q3Mass,
                DwellTimeMs = mrm.DwellTime,
                CollisionEnergy = ce,
                StartTime = start,
                EndTime = end,
                CompoundName = mrm.Name,
            });
        }
        return targets;
    }

    private IReadOnlyList<WiffSimTarget> BuildSimTargets()
    {
        if (_info.ExperimentType != Clearcore2.Data.DataAccess.SampleData.ExperimentType.SIM
            || _info.MassRangeInfo is not { Length: > 0 } ranges)
            return Array.Empty<WiffSimTarget>();
        var targets = new List<WiffSimTarget>(ranges.Length);
        foreach (var range in ranges)
        {
            if (range is not SIMMassRange sim) continue;
            double ce = ReadCompoundParameter(sim.CompoundDepParameters, "CE");
            double start = sim.ExpectedRT - sim.RTWindow / 2.0;
            double end = sim.ExpectedRT + sim.RTWindow / 2.0;
            targets.Add(new WiffSimTarget
            {
                Mass = sim.Mass,
                DwellTimeMs = sim.DwellTime,
                CollisionEnergy = ce,
                StartTime = start,
                EndTime = end,
                CompoundName = sim.Name,
            });
        }
        return targets;
    }

    private static double ReadCompoundParameter(Dictionary<string, Parameter>? parameters, string name)
    {
        if (parameters is null) return 0;
        return parameters.TryGetValue(name, out var p) && p is not null ? p.Start : 0;
    }

    private static WiffExperimentType MapExperimentType(Clearcore2.Data.DataAccess.SampleData.ExperimentType t) => t switch
    {
        Clearcore2.Data.DataAccess.SampleData.ExperimentType.MS => WiffExperimentType.MS,
        Clearcore2.Data.DataAccess.SampleData.ExperimentType.Product => WiffExperimentType.Product,
        Clearcore2.Data.DataAccess.SampleData.ExperimentType.Precursor => WiffExperimentType.Precursor,
        Clearcore2.Data.DataAccess.SampleData.ExperimentType.NeutralGainOrLoss => WiffExperimentType.NeutralGainOrLoss,
        Clearcore2.Data.DataAccess.SampleData.ExperimentType.SIM => WiffExperimentType.SIM,
        Clearcore2.Data.DataAccess.SampleData.ExperimentType.MRM => WiffExperimentType.MRM,
        _ => WiffExperimentType.MS,
    };
}

/// <summary><see cref="AbstractWiffSpectrum"/> backed by Clearcore2's <see cref="MassSpectrum"/>.</summary>
internal sealed class WiffSpectrum : AbstractWiffSpectrum
{
    private readonly MassSpectrum _ms;
    private readonly MassSpectrumInfo _info;

    public WiffSpectrum(MassSpectrum ms)
    {
        _ms = ms;
        _info = ms.Info;
    }

    public override bool CentroidMode => _info.CentroidMode;
    public override double[] XValues => _ms.GetActualXValues() ?? Array.Empty<double>();
    public override double[] YValues => _ms.GetActualYValues() ?? Array.Empty<double>();
    public override bool HasPrecursorInfo => _info.ParentMZ > 0;
    public override double PrecursorMz => _info.ParentMZ;
    public override int PrecursorCharge => _info.ParentChargeState;
    public override double CollisionEnergy => Math.Abs(_info.CollisionEnergy);
    public override WiffActivation Activation => WiffActivation.CID;
    public override double IsolationLowerOffset => 0;
    public override double IsolationUpperOffset => 0;
    public override double ElectronKineticEnergy => 0;
}
