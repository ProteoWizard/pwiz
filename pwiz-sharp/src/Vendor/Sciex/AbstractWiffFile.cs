#pragma warning disable CA1707

namespace Pwiz.Vendor.Sciex;

/// <summary>
/// Polymorphic wiff/wiff2 reader, mirroring pwiz cpp's <c>WiffFile</c> abstract class.
/// Two concrete impls: <see cref="WiffFile"/> wraps the .NET-Framework-era
/// <c>Clearcore2.Data.AnalystDataProvider</c> for <c>.wiff</c> files (default ALC); the wiff2
/// plugin assembly's <c>Wiff2File</c> wraps the modern <c>SCIEX.Apis.Data.v1</c> SDK in a
/// side-by-side <see cref="Wiff2LoadContext"/>. Callers (<c>SpectrumList_Sciex</c>,
/// <c>ChromatogramList_Sciex</c>, <c>Reader_Sciex</c>) work against this abstraction so a
/// single code path covers both formats.
/// </summary>
public abstract class AbstractWiffFile : IDisposable
{
    /// <summary>Path of the .wiff or .wiff2 file backing this reader.</summary>
    public abstract string WiffPath { get; }

    /// <summary>1-based sample index used to open this run.</summary>
    public abstract int SampleNumber { get; }

    /// <summary>Total samples in the file (per-file index).</summary>
    public abstract int SampleCount { get; }

    /// <summary>Sample name for the open sample, or empty if the SDK doesn't expose one.</summary>
    public abstract string SampleName { get; }

    /// <summary>Number of experiments in the selected sample.</summary>
    public abstract int ExperimentCount { get; }

    /// <summary>Returns the experiment at <paramref name="experimentIndex"/> (0-based).</summary>
    public abstract AbstractWiffExperiment GetExperiment(int experimentIndex);

    /// <summary>Acquisition timestamp pre-formatted as <c>yyyy-MM-ddTHH:mm:ssZ</c>, or null.</summary>
    public abstract string? StartTimestampUtc { get; }

    /// <summary>Instrument model name from the first MS device, or null.</summary>
    public abstract string? InstrumentModelName { get; }

    /// <summary>Number of ADC channels (legacy only — wiff2 returns 0).</summary>
    public abstract int AdcChannelCount { get; }

    /// <summary>ADC channel name (legacy only).</summary>
    public abstract string GetAdcChannelName(int channelIndex);

    /// <summary>(Times, intensities) pair for an ADC channel (legacy only).</summary>
    public abstract (double[] Times, double[] Intensities) GetAdcTrace(int channelIndex);

    /// <summary>Whether the sample has UV/PDA wavelength data (legacy only — wiff2 returns false).</summary>
    public abstract bool HasDadData { get; }

    /// <summary>(Times, intensities) for the DAD total-wavelength chromatogram (legacy only).</summary>
    public abstract (double[] Times, double[] Intensities) GetTotalWavelengthChromatogram();

    /// <inheritdoc/>
    public abstract void Dispose();

    /// <summary>Opens <paramref name="path"/>; dispatches to the legacy or wiff2 implementation
    /// based on extension. The wiff2 path lives in the <c>Pwiz.Vendor.Sciex.Wiff2</c> plugin
    /// assembly which is loaded into the side-by-side <see cref="Wiff2LoadContext"/>.</summary>
    public static AbstractWiffFile Open(string path, int sampleIndex0 = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.EndsWith(".wiff2", StringComparison.OrdinalIgnoreCase))
            return OpenWiff2Plugin(path, sampleIndex0);
        return new WiffFile(path, sampleIndex0);
    }

    private static AbstractWiffFile OpenWiff2Plugin(string wiff2Path, int sampleIndex0)
    {
        var alc = Wiff2LoadContext.Instance;
        string pluginPath = Path.Combine(AppContext.BaseDirectory, "Pwiz.Vendor.Sciex.Wiff2.dll");
        if (!File.Exists(pluginPath))
            throw new FileNotFoundException(
                "Pwiz.Vendor.Sciex.Wiff2 plugin assembly not found next to the executable.", pluginPath);
        var pluginAsm = alc.LoadFromAssemblyPath(pluginPath);
        var implType = pluginAsm.GetType("Pwiz.Vendor.Sciex.Wiff2.Wiff2File", throwOnError: true)!;
        return (AbstractWiffFile)Activator.CreateInstance(implType, wiff2Path, sampleIndex0)!;
    }
}

/// <summary>One experiment within a sample. Mirrors cpp <c>Experiment</c>.</summary>
public abstract class AbstractWiffExperiment
{
    /// <summary>Experiment kind (full-scan MS, product, MRM, SIM, ...).</summary>
    public abstract WiffExperimentType ExperimentType { get; }

    /// <summary>Acquisition polarity.</summary>
    public abstract WiffPolarity Polarity { get; }

    /// <summary>Acquisition mass range start (m/z).</summary>
    public abstract double StartMass { get; }

    /// <summary>Acquisition mass range end (m/z).</summary>
    public abstract double EndMass { get; }

    /// <summary>Number of cycles (spectra) in this experiment.</summary>
    public abstract int CycleCount { get; }

    /// <summary>Retention time (minutes) for the spectrum at <paramref name="cycle1Based"/>.</summary>
    public abstract double GetRetentionTime(int cycle1Based);

    /// <summary>Per-cycle ms level. cpp <c>ExperimentImpl::getMsLevel</c> equivalent — for SRM
    /// MRM cycles the value is typically 1, even though the experiment-type heuristic might
    /// suggest 2.</summary>
    public abstract int GetMsLevelForCycle(int cycle1Based);

    /// <summary>Spectrum at <paramref name="cycle1Based"/> (1-based to mirror cpp), with
    /// optional profile-zero padding and SDK-side centroiding. Returns null on failure.</summary>
    public abstract AbstractWiffSpectrum? GetSpectrum(int cycle1Based, bool addZeros, bool centroid);

    /// <summary>BPC (times, intensities) for this experiment, or empty if the SDK doesn't
    /// expose one (cpp <c>WiffFile2</c> always returns empty for wiff2).</summary>
    public abstract (double[] Times, double[] Intensities) GetBpc();

    /// <summary>TIC (times, intensities) for this experiment.</summary>
    public abstract (double[] Times, double[] Intensities) GetTic();

    /// <summary>SRM transitions for an MRM experiment (empty for wiff2 / non-MRM).</summary>
    public abstract IReadOnlyList<WiffMrmTarget> SrmTransitions { get; }

    /// <summary>SIM transitions for a SIM experiment (empty for wiff2 / non-SIM).</summary>
    public abstract IReadOnlyList<WiffSimTarget> SimTransitions { get; }

    /// <summary>Selected ion chromatogram for the SRM/SIM transition at
    /// <paramref name="transitionIndex"/>. Empty for wiff2.</summary>
    public abstract (double[] Times, double[] Intensities) GetSic(int transitionIndex);
}

/// <summary>One mass spectrum (one cycle of one experiment). Mirrors cpp <c>Spectrum</c>.</summary>
public abstract class AbstractWiffSpectrum
{
    /// <summary>Whether the SDK reports this spectrum as centroided.</summary>
    public abstract bool CentroidMode { get; }

    /// <summary>m/z values (sorted ascending).</summary>
    public abstract double[] XValues { get; }

    /// <summary>Intensity values, parallel to <see cref="XValues"/>.</summary>
    public abstract double[] YValues { get; }

    /// <summary>Whether the SDK exposes precursor / isolation info for this spectrum.</summary>
    public abstract bool HasPrecursorInfo { get; }

    /// <summary>Selected ion m/z (precursor target).</summary>
    public abstract double PrecursorMz { get; }

    /// <summary>Charge state (0 if unknown).</summary>
    public abstract int PrecursorCharge { get; }

    /// <summary>Collision energy (eV; 0 if not set).</summary>
    public abstract double CollisionEnergy { get; }

    /// <summary>Activation method (CID by default, EAD when wiff2's FragmentationMode is EAD).</summary>
    public abstract WiffActivation Activation { get; }

    /// <summary>Isolation-window lower offset (m/z; 0 if unspecified).</summary>
    public abstract double IsolationLowerOffset { get; }

    /// <summary>Isolation-window upper offset (m/z; 0 if unspecified).</summary>
    public abstract double IsolationUpperOffset { get; }

    /// <summary>Electron kinetic energy for EAD spectra (eV; 0 if not set).</summary>
    public abstract double ElectronKineticEnergy { get; }
}

/// <summary>Sciex experiment kind, normalized across the legacy and wiff2 SDKs.</summary>
public enum WiffExperimentType
{
    /// <summary>Full-scan MS1.</summary>
    MS,
    /// <summary>Product-ion scan (MS/MS).</summary>
    Product,
    /// <summary>Precursor-ion scan.</summary>
    Precursor,
    /// <summary>Neutral-gain or neutral-loss scan.</summary>
    NeutralGainOrLoss,
    /// <summary>Selected ion monitoring.</summary>
    SIM,
    /// <summary>Multiple reaction monitoring.</summary>
    MRM,
}

/// <summary>Acquisition polarity.</summary>
public enum WiffPolarity
{
    /// <summary>Polarity not provided by the SDK.</summary>
    Unknown,
    /// <summary>Positive mode.</summary>
    Positive,
    /// <summary>Negative mode.</summary>
    Negative,
}

/// <summary>Activation method for an MSn spectrum.</summary>
public enum WiffActivation
{
    /// <summary>Default — collision-induced dissociation (or beam-type CID).</summary>
    CID,
    /// <summary>Electron-activated dissociation.</summary>
    EAD,
}

/// <summary>SRM transition descriptor (legacy MRM experiments).</summary>
public sealed class WiffMrmTarget
{
    /// <summary>Q1 mass.</summary>
    public required double Q1Mass { get; init; }
    /// <summary>Q3 mass.</summary>
    public required double Q3Mass { get; init; }
    /// <summary>Per-transition dwell time (ms).</summary>
    public required double DwellTimeMs { get; init; }
    /// <summary>Per-transition collision energy (eV); 0 if missing.</summary>
    public required double CollisionEnergy { get; init; }
    /// <summary>Scheduled-MRM start time (minutes); 0 if unscheduled.</summary>
    public required double StartTime { get; init; }
    /// <summary>Scheduled-MRM end time (minutes); 0 if unscheduled.</summary>
    public required double EndTime { get; init; }
    /// <summary>Compound name from the method.</summary>
    public string? CompoundName { get; init; }
}

/// <summary>SIM transition descriptor (legacy SIM experiments).</summary>
public sealed class WiffSimTarget
{
    /// <summary>Selected mass.</summary>
    public required double Mass { get; init; }
    /// <summary>Per-transition dwell time (ms).</summary>
    public required double DwellTimeMs { get; init; }
    /// <summary>Per-transition collision energy (eV); 0 if missing.</summary>
    public required double CollisionEnergy { get; init; }
    /// <summary>Scheduled-SIM start time (minutes); 0 if unscheduled.</summary>
    public required double StartTime { get; init; }
    /// <summary>Scheduled-SIM end time (minutes); 0 if unscheduled.</summary>
    public required double EndTime { get; init; }
    /// <summary>Compound name from the method.</summary>
    public string? CompoundName { get; init; }
}
