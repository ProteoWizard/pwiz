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

    /// <summary>
    /// Every sample name in the file, in sample order and already disambiguated - the array cpp
    /// returns from <c>WiffFile::getSampleNames()</c>. <see cref="SampleName"/> is this indexed
    /// by <see cref="SampleNumber"/>.
    /// </summary>
    /// <remarks>
    /// Needed because the two WIFF generations enumerate through different SDKs: the .wiff path
    /// can do it without opening a sample (<see cref="WiffFile.EnumerateSampleNames"/>), but
    /// .wiff2 only reaches its sample list through the side-by-side plugin, so a caller that
    /// wants the count or the names for a .wiff2 has to go through an opened instance.
    /// </remarks>
    public abstract string[] AllSampleNames { get; }

    /// <summary>
    /// Makes duplicate sample names unique by appending the duplicate count, in place:
    /// <c>foo, bar, foo (2), foobar, bar (2), foo (3)</c>.
    /// </summary>
    /// <remarks>
    /// Port of the identical loop in cpp <c>WiffFileImpl::getSampleNames</c>
    /// (<c>WiffFile.cpp:314-333</c>) and <c>WiffFile2Impl::getSampleNames</c>
    /// (<c>WiffFile2.ipp:373-392</c>). Both wiff generations do this, so it lives here.
    /// It matters because the run id is <c>&lt;wiff-stem&gt;-&lt;sampleName&gt;</c> and msconvert
    /// names the output file after the run id: two samples sharing a name would otherwise
    /// produce one output that overwrites the other. Counting is over the RAW name, so the
    /// suffix a name receives does not depend on suffixes handed out earlier.
    /// </remarks>
    /// <remarks>
    /// <c>protected</c> rather than <c>internal</c> because <see cref="WiffFile"/> and the wiff2
    /// plugin's <c>Wiff2File</c> live in different assemblies; both reach it as subclasses.
    /// </remarks>
    protected static string[] DisambiguateSampleNames(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var result = new string[names.Count];
        var duplicateCount = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i] ?? string.Empty;
            duplicateCount.TryGetValue(name, out int seen);
            duplicateCount[name] = seen + 1;
            result[i] = seen == 0 ? name : $"{name} ({seen + 1})";
        }
        return result;
    }

    /// <summary>Number of experiments in the selected sample.</summary>
    public abstract int ExperimentCount { get; }

    /// <summary>Returns the experiment at <paramref name="experimentIndex"/> (0-based).</summary>
    public abstract AbstractWiffExperiment GetExperiment(int experimentIndex);

    /// <summary>Acquisition timestamp pre-formatted as <c>yyyy-MM-ddTHH:mm:ssZ</c>, or null.</summary>
    /// <summary>
    /// Acquisition time as the SDK reports it, with no time zone applied and no formatting.
    /// cpp shifts this to the host's zone only when <c>adjustUnknownTimeZonesToHostTimeZone</c>
    /// is set (Reader_ABI.cpp), so the decision belongs to the reader, not here.
    /// <see cref="DateTime.MinValue"/> when unavailable.
    /// </summary>
    public abstract DateTime StartTimestampRaw { get; }

    /// <summary>Instrument model name from the first MS device, or null.</summary>
    public abstract string? InstrumentModelName { get; }

    /// <summary>Instrument serial number from the first MS device, or null/empty
    /// when the SDK doesn't expose one (e.g. legacy WIFF, where cpp also returns "").</summary>
    public abstract string? InstrumentSerialNumber { get; }

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

/// <summary>
/// One "encoded bin" of a Sciex ZT Scan quadrupole sweep.
///
/// ZT Scan (ZenoTOF 8600) scans Q1 continuously across the precursor range instead of stepping
/// it. The whole sweep is a single acquisition — the method stores one MS/MS experiment whose
/// accumulation time is the entire sweep (e.g. 0.86 s) — and the instrument ramps the collision
/// energy in hardware from one end of the precursor range to the other as the quadrupole moves.
/// The sweep is then digitized into hundreds of narrow bins, and both SDKs surface each bin as
/// its own experiment.
///
/// Neither SDK stores a per-bin collision energy: both report the ramp MIDPOINT on every bin
/// (30 V for an 18 -> 43 V ramp), which is off by roughly half the ramp at either end. This
/// carries the endpoints so the per-bin value can be reconstructed.
///
/// Interpolation is on the bin ORDINAL rather than the bin's quadrupole m/z. The quadrupole
/// scans at a constant Da/s, so the two axes are equivalent, but the ordinal needs no extra
/// method parameters — the precursor start/stop masses are exposed by only one of the two APIs.
/// The ordinal also lands exactly on the stated endpoints at the first and last bin.
///
/// NOTE: that the hardware ramp is LINEAR is an assumption. The files state only the endpoints,
/// never the shape. If Sciex confirms a different profile, <see cref="CollisionEnergy"/> is the
/// only place that needs to change.
/// </summary>
public sealed class ZtScanBin
{
    /// <summary>Builds one bin of a sweep of <paramref name="binCount"/> bins.</summary>
    /// <param name="ceRampStart">Collision energy at the low-m/z end of the sweep (eV).</param>
    /// <param name="ceRampEnd">Collision energy at the high-m/z end of the sweep (eV).</param>
    /// <param name="binIndex">0-based ordinal of this bin within the sweep.</param>
    /// <param name="binCount">Total bins in the sweep.</param>
    public ZtScanBin(double ceRampStart, double ceRampEnd, int binIndex, int binCount)
    {
        CeRampStart = ceRampStart;
        CeRampEnd = ceRampEnd;
        BinIndex = binIndex;
        BinCount = binCount;
    }

    /// <summary>Collision energy at the low-m/z end of the sweep (eV).</summary>
    public double CeRampStart { get; }

    /// <summary>Collision energy at the high-m/z end of the sweep (eV).</summary>
    public double CeRampEnd { get; }

    /// <summary>0-based ordinal of this bin within the sweep.</summary>
    public int BinIndex { get; }

    /// <summary>Total bins in the sweep.</summary>
    public int BinCount { get; }

    /// <summary>
    /// This bin's collision energy, linearly interpolated between the ramp endpoints. A sweep of
    /// fewer than two bins has no ramp to interpolate, so it falls back to the midpoint — the
    /// same value the SDKs report.
    /// </summary>
    public double CollisionEnergy
    {
        get
        {
            if (BinCount < 2) return (CeRampStart + CeRampEnd) / 2;
            if (BinIndex <= 0) return CeRampStart;
            if (BinIndex >= BinCount - 1) return CeRampEnd;
            return CeRampStart + (CeRampEnd - CeRampStart) * BinIndex / (BinCount - 1);
        }
    }
}

/// <summary>One experiment within a sample. Mirrors cpp <c>Experiment</c>.</summary>
public abstract class AbstractWiffExperiment
{
    /// <summary>Experiment kind (full-scan MS, product, MRM, SIM, ...).</summary>
    public abstract WiffExperimentType ExperimentType { get; }

    /// <summary>
    /// Non-null when this experiment is one encoded bin of a ZT Scan quadrupole sweep, in which
    /// case its collision energy must be interpolated rather than read from the SDK. See
    /// <see cref="ZtScanBin"/>.
    /// </summary>
    public virtual ZtScanBin? ZtScan => null;

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

    /// <summary>Per-cycle TIC value as cpp <c>spectrum->getSumY()</c> reports it
    /// (sourced from <c>experiment->cycleIntensities()[cycle-1]</c>). Used to fill
    /// the per-spectrum <c>MS_total_ion_current</c> cvParam without summing the
    /// post-centroid YValues, which lose intensity relative to the raw cycle TIC.
    /// Returns 0 when the SDK doesn't expose cycle intensities.</summary>
    public virtual double GetCycleTic(int cycle1Based)
    {
        var (_, intensities) = GetTic();
        int idx = cycle1Based - 1;
        return idx >= 0 && idx < intensities.Length ? intensities[idx] : 0;
    }

    /// <summary>SRM transitions for an MRM experiment (empty for wiff2 / non-MRM).</summary>
    public abstract IReadOnlyList<WiffMrmTarget> SrmTransitions { get; }

    /// <summary>SIM transitions for a SIM experiment (empty for wiff2 / non-SIM).</summary>
    public abstract IReadOnlyList<WiffSimTarget> SimTransitions { get; }

    /// <summary>Selected ion chromatogram for the SRM/SIM transition at
    /// <paramref name="transitionIndex"/>. Empty for wiff2.</summary>
    public abstract (double[] Times, double[] Intensities) GetSic(int transitionIndex);

    /// <summary>cpp <c>experiment->basePeakIntensities()[cycle-1]</c> /
    /// <c>basePeakMZs()[cycle-1]</c>: per-cycle base peak. Returns null when the SDK
    /// can't supply it (wiff2; or when initialization fails). Implementations should
    /// cache the BPC since each spectrum asks for one cycle's worth.</summary>
    public abstract (double Mz, double Intensity)? GetBasePeak(int cycle1Based);

    /// <summary>Releases SDK objects held by this experiment. WiffFile.Dispose
    /// cascades through here before closing the provider. No-op on wiff2.</summary>
    public virtual void Dispose() { }
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

    /// <summary>Whether the SDK exposes precursor info for this spectrum (cpp
    /// <c>Spectrum::getHasPrecursorInfo</c>).</summary>
    public abstract bool HasPrecursorInfo { get; }

    /// <summary>cpp <c>Spectrum::getHasIsolationInfo</c> (WiffFile.cpp:759 /
    /// WiffFile2.ipp:732). Gates the whole <c>getIsolationInfo</c> payload — isolation
    /// window, collision energy, fragmentation mode, electron kinetic energy — so a
    /// spectrum whose experiment isn't a Product/Precursor scan emits none of them.
    /// Legacy WIFF additionally requires the experiment to carry MassRangeInfo.</summary>
    public abstract bool HasIsolationInfo { get; }

    /// <summary>Selected ion m/z (precursor target).</summary>
    public abstract double PrecursorMz { get; }

    /// <summary>Charge state (0 if unknown).</summary>
    public abstract int PrecursorCharge { get; }

    /// <summary>Collision energy (eV; 0 if not set or if <see cref="HasIsolationInfo"/>
    /// is false — cpp only fills this in from <c>getIsolationInfo</c>).</summary>
    public abstract double CollisionEnergy { get; }

    /// <summary>Activation method (CID by default, EAD when wiff2's FragmentationMode is EAD).</summary>
    public abstract WiffActivation Activation { get; }

    /// <summary>Isolation-window lower offset (m/z; 0 if unspecified).</summary>
    public abstract double IsolationLowerOffset { get; }

    /// <summary>Isolation-window upper offset (m/z; 0 if unspecified).</summary>
    public abstract double IsolationUpperOffset { get; }

    /// <summary>Electron kinetic energy for EAD spectra (eV; 0 if not set).</summary>
    public abstract double ElectronKineticEnergy { get; }

    /// <summary>cpp <c>spectrumInfo->StartRT</c> — retention time at the start of this
    /// spectrum's cycle, in minutes. Differs from the experiment-level
    /// <c>GetRTFromExperimentCycle</c> by one cycle on legacy WIFF (the experiment
    /// returns the RT at the cycle's measurement; this returns the RT at the cycle's
    /// start). Reference mzMLs were generated with <c>StartRT</c>; matching it is
    /// required for harness parity. Returns 0 if the SDK can't report it.</summary>
    public abstract double StartTimeMinutes { get; }

    /// <summary>cpp <c>spectrumInfo->BasePeak{MZ,Intensity}</c> — per-cycle base-peak
    /// metadata exposed by the legacy WIFF SDK (and not by wiff2). Returns null when
    /// the SDK doesn't surface them; the spectrum-list emits base-peak CV params
    /// only when a value comes back.</summary>
    public abstract (double Mz, double Intensity)? BasePeak { get; }

    /// <summary>cpp <c>experiment->getExperimentType()</c> reflected onto the spectrum.
    /// SpectrumList_Sciex needs this to override XValues / set centroid for MRM/SIM
    /// without going back through the experiment-list lookup it already did once.</summary>
    public abstract WiffExperimentType ExperimentType { get; }
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
