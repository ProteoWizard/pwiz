using Clearcore2.Data;
using Clearcore2.Data.AnalystDataProvider;
using Clearcore2.Data.DataAccess.SampleData;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Sciex;

/// <summary>
/// Thin wrapper over Sciex's <see cref="AnalystWiffDataProvider"/>. C# equivalent of pwiz C++
/// <c>WiffFileImpl</c>. Initial scope: single-sample <c>.wiff</c> reads — opens the first sample,
/// exposes its experiments and per-cycle mass spectra. Multi-sample wiff support, ADC traces,
/// and wiff2 / SCIEX.Apis-only formats are follow-ups.
/// </summary>
public sealed class WiffData : IDisposable
{
    private readonly AnalystWiffDataProvider _provider;
    private readonly Sample _sample;
    private readonly MassSpectrometerSample _msSample;
    private bool _disposed;

    /// <summary>Path to the .wiff file.</summary>
    public string WiffPath { get; }

    /// <summary>1-based sample index used to open this run.</summary>
    public int SampleNumber { get; }

    /// <summary>Underlying SDK Sample (file metadata, sample info, etc).</summary>
    public Sample Sample => _sample;

    /// <summary>MS data root for the selected sample.</summary>
    public MassSpectrometerSample MsSample => _msSample;

    /// <summary>Number of experiments (scan-mode "tracks") in the selected sample.</summary>
    public int ExperimentCount => _msSample.ExperimentCount;

    /// <summary>Total samples in the wiff (the SDK's per-wiff index).</summary>
    public int SampleCount { get; }

    /// <summary>Constructor: opens <paramref name="wiffPath"/> at sample <paramref name="sampleIndex0"/> (0-based).</summary>
    public WiffData(string wiffPath, int sampleIndex0 = 0)
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

        _sample = AnalystDataProviderFactory.CreateSample(wiffPath, sampleIndex0, _provider);
        if (_sample is null) throw new InvalidDataException($"AnalystDataProviderFactory.CreateSample returned null for {wiffPath}");
        if (!_sample.HasMassSpectrometerData)
            throw new InvalidDataException($"WIFF sample {sampleIndex0} has no MS data: {wiffPath}");
        _msSample = _sample.MassSpectrometerSample;
    }

    /// <summary>Returns the experiment at <paramref name="experimentIndex"/> (0-based).</summary>
    public MSExperiment GetExperiment(int experimentIndex) => _msSample.GetMSExperiment(experimentIndex);

    /// <summary>Number of ADC channels on this sample (0 when the sample has no ADC trace data).</summary>
    public int AdcChannelCount
    {
        get
        {
            if (!_sample.HasADCData) return 0;
            var adc = _sample.ADCSample;
            return adc is null ? 0 : adc.ChannelCount;
        }
    }

    /// <summary>Channel name for ADC trace <paramref name="index"/>, or empty when unavailable.</summary>
    public string GetAdcChannelName(int index)
    {
        try { return _sample.ADCSample?.GetChannelNameAt(index) ?? string.Empty; }
        catch { return string.Empty; }
    }

    /// <summary>Returns the (times, intensities) pair for ADC channel <paramref name="index"/>.</summary>
    public (double[] Times, double[] Intensities) GetAdcTrace(int index)
    {
        try
        {
            var data = _sample.ADCSample?.GetADCData(index);
            if (data is null) return (Array.Empty<double>(), Array.Empty<double>());
            return (data.GetActualXValues() ?? Array.Empty<double>(),
                    data.GetActualYValues() ?? Array.Empty<double>());
        }
        catch { return (Array.Empty<double>(), Array.Empty<double>()); }
    }

    /// <summary>True when the sample has UV/PDA wavelength data (DADSample exposed).</summary>
    public bool HasDadData => _sample.HasDADData && _sample.DADSample is not null;

    /// <summary>Returns the (times, intensities) pair for the DAD total-wavelength chromatogram.</summary>
    public (double[] Times, double[] Intensities) GetTotalWavelengthChromatogram()
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

    /// <summary>Detects whether <paramref name="path"/> is a Sciex WIFF (.wiff or .wiff2).</summary>
    public static bool IsWiffFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return path.EndsWith(".wiff", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".wiff2", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _provider.Close(); } catch { }
    }
}
