// Port of pwiz_tools/BiblioSpec/src/Spectrum.{h,cpp}

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// A single peak: m/z + intensity + drift-time offset. Port of <c>BiblioSpec::PEAK_T</c>.
/// </summary>
/// <remarks>
/// <para>cpp Spectrum.h:46 — <c>driftTime</c> is the Waters Mse high-energy product-ion offset
/// (product ions are accelerated after the drift tube and reach the detector slightly faster).</para>
/// <para>Defined as a mutable struct (not <c>record</c>) because cpp code mutates individual peaks
/// in-place during operations like circular shift; public fields preserve the cpp POD shape.</para>
/// </remarks>
#pragma warning disable CA1051 // public fields on this struct mirror cpp PEAK_T's POD layout
public struct PeakT : IEquatable<PeakT>
{
    /// <summary>Peak m/z (Th).</summary>
    public double Mz;

    /// <summary>Peak intensity (cps or absorbance, units depend on source).</summary>
    public float Intensity;

    /// <summary>Waters Mse high-energy product-ion drift-time offset.</summary>
    public float DriftTime;

    /// <summary>Constructs a peak with the given mz/intensity/drift-time.</summary>
    public PeakT(double mz, float intensity, float driftTime = 0)
    {
        Mz = mz;
        Intensity = intensity;
        DriftTime = driftTime;
    }

    /// <inheritdoc/>
    public readonly bool Equals(PeakT other) =>
        Mz == other.Mz && Intensity == other.Intensity && DriftTime == other.DriftTime;

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is PeakT p && Equals(p);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(Mz, Intensity, DriftTime);

    /// <summary>Value-equality operator.</summary>
    public static bool operator ==(PeakT left, PeakT right) => left.Equals(right);

    /// <summary>Value-inequality operator.</summary>
    public static bool operator !=(PeakT left, PeakT right) => !left.Equals(right);
}
#pragma warning restore CA1051

/// <summary>Coarse spectrum-type tag. Port of <c>BiblioSpec::SPEC_TYPE</c>.</summary>
public enum SpecType
{
    /// <summary>Undefined / unset.</summary>
    Undef = 0,
    /// <summary>Library reference spectrum.</summary>
    Reference = 1,
    /// <summary>MS2 fragment spectrum.</summary>
    Ms2 = 2,
    /// <summary>MS1 / precursor scan.</summary>
    Ms1 = 3,
}

/// <summary>
/// Base spectrum class used by BiblioSpec: m/z array, intensity array, precursor metadata,
/// retention time, scan number, ion mobility, and basic peak statistics.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::Spectrum</c>. This is the BiblioSpec spectrum — lean and
/// flat, separate from the MsData / mzML spectrum in <see cref="Pwiz.Data.MsData.Spectra.Spectrum"/>.</para>
/// <para>Lazy / sentinel fields preserved from cpp: <c>totalIonCurrentRaw</c>,
/// <c>totalIonCurrentProcessed</c>, <c>basePeakIntensityRaw</c>, <c>basePeakIntensityProcessed</c>
/// default to <c>-1</c> meaning "not set — compute from peaks on demand".</para>
/// </remarks>
public class Spectrum
{
    // Backing fields. cpp uses trailing underscore on member names and exposes them as
    // protected to subclasses (RefSpectrum mutates rawPeaks_/scanNumber_/etc. directly).
    // We preserve the cpp shape so the subclass port stays readable; CA1051 is suppressed
    // for the field block because the underscore-suffixed names are the port-of-record.
#pragma warning disable CA1051, IDE1006 // mirror cpp protected-field shape; underscore suffix matches cpp
    /// <summary>Scan number; protected so RefSpectrum can poke it during decoy creation.</summary>
    protected int scanNumber_;
    /// <summary>Spectrum-type tag.</summary>
    protected SpecType type_;
    /// <summary>Precursor m/z.</summary>
    protected double mz_;
    /// <summary>Ion mobility value (units depend on <see cref="ionMobilityType_"/>).</summary>
    protected double ionMobility_;
    /// <summary>Collisional cross section.</summary>
    protected double collisionalCrossSection_;
    /// <summary>Waters Mse high-energy product-ion drift-time offset.</summary>
    protected double ionMobilityHighEnergyOffset_;
    /// <summary>Ion-mobility units.</summary>
    protected IonMobilityType ionMobilityType_;
    /// <summary>Retention time of this spectrum.</summary>
    protected double retentionTime_;
    /// <summary>Start of the RT window for this spectrum (used by DIA).</summary>
    protected double startTime_;
    /// <summary>End of the RT window for this spectrum (used by DIA).</summary>
    protected double endTime_;
    /// <summary>Cached TIC over raw peaks, or -1 to indicate "compute on demand".</summary>
    protected double totalIonCurrentRaw_;
    /// <summary>Cached TIC over processed peaks, or -1 to indicate "compute on demand".</summary>
    protected double totalIonCurrentProcessed_;
    /// <summary>Cached base-peak intensity over raw peaks, or -1 to indicate "compute on demand".</summary>
    protected double basePeakIntensityRaw_;
    /// <summary>Cached base-peak intensity over processed peaks, or -1 to indicate "compute on demand".</summary>
    protected double basePeakIntensityProcessed_;
    /// <summary>Possible precursor charges. Empty when unknown.</summary>
    protected List<int> possibleCharges_;
    /// <summary>Raw (unfiltered) peak list.</summary>
    protected List<PeakT> rawPeaks_;
    /// <summary>Processed (filtered / normalised) peak list.</summary>
    protected List<PeakT> processedPeaks_;
#pragma warning restore CA1051, IDE1006

    /// <summary>Constructs an empty spectrum with cpp-default sentinel values.</summary>
    public Spectrum()
    {
        scanNumber_ = 0;
        type_ = SpecType.Undef;
        mz_ = 0;
        ionMobility_ = 0;
        collisionalCrossSection_ = 0;
        ionMobilityHighEnergyOffset_ = 0;
        ionMobilityType_ = IonMobilityType.None;
        retentionTime_ = 0;
        startTime_ = 0;
        endTime_ = 0;
        totalIonCurrentRaw_ = -1;
        totalIonCurrentProcessed_ = -1;
        basePeakIntensityRaw_ = -1;
        basePeakIntensityProcessed_ = -1;
        possibleCharges_ = new List<int>();
        rawPeaks_ = new List<PeakT>();
        processedPeaks_ = new List<PeakT>();
    }

    /// <summary>Copy constructor: deep-copies peak lists and charges.</summary>
    public Spectrum(Spectrum other)
    {
        ArgumentNullException.ThrowIfNull(other);
        scanNumber_ = other.scanNumber_;
        mz_ = other.mz_;
        ionMobility_ = other.ionMobility_;
        ionMobilityType_ = other.ionMobilityType_;
        collisionalCrossSection_ = other.collisionalCrossSection_;
        ionMobilityHighEnergyOffset_ = other.ionMobilityHighEnergyOffset_;
        retentionTime_ = other.retentionTime_;
        startTime_ = other.startTime_;
        endTime_ = other.endTime_;
        type_ = other.type_;
        totalIonCurrentRaw_ = other.totalIonCurrentRaw_;
        totalIonCurrentProcessed_ = other.totalIonCurrentProcessed_;
        basePeakIntensityRaw_ = other.basePeakIntensityRaw_;
        basePeakIntensityProcessed_ = other.basePeakIntensityProcessed_;
        possibleCharges_ = new List<int>(other.possibleCharges_);
        rawPeaks_ = new List<PeakT>(other.rawPeaks_);
        processedPeaks_ = new List<PeakT>(other.processedPeaks_);
    }

    /// <summary>Reset the spectrum to the cpp-default empty state.</summary>
    public virtual void Clear()
    {
        scanNumber_ = 0;
        mz_ = 0;
        ionMobility_ = 0;
        ionMobilityType_ = IonMobilityType.None;
        collisionalCrossSection_ = 0;
        ionMobilityHighEnergyOffset_ = 0;
        retentionTime_ = 0;
        startTime_ = 0;
        endTime_ = 0;
        type_ = SpecType.Undef;
        possibleCharges_.Clear();
        rawPeaks_.Clear();
        processedPeaks_.Clear();
    }

    /// <summary>Scan number for this spectrum.</summary>
    public int ScanNumber
    {
        get => scanNumber_;
        set => scanNumber_ = value;
    }

    /// <summary>Precursor m/z (Th).</summary>
    public double Mz
    {
        get => mz_;
        set => mz_ = value;
    }

    /// <summary>Ion-mobility value (units in <see cref="IonMobilityType"/>).</summary>
    public double IonMobility => ionMobility_;

    /// <summary>Ion-mobility units.</summary>
    public IonMobilityType IonMobilityType => ionMobilityType_;

    /// <summary>Collisional cross section.</summary>
    public double CollisionalCrossSection
    {
        get => collisionalCrossSection_;
        set => collisionalCrossSection_ = value;
    }

    /// <summary>Retention time of this spectrum.</summary>
    public double RetentionTime
    {
        get => retentionTime_;
        set => retentionTime_ = value;
    }

    /// <summary>Start of the RT window for this spectrum.</summary>
    public double StartTime
    {
        get => startTime_;
        set => startTime_ = value;
    }

    /// <summary>End of the RT window for this spectrum.</summary>
    public double EndTime
    {
        get => endTime_;
        set => endTime_ = value;
    }

    /// <summary>Spectrum type tag.</summary>
    public SpecType SpectrumType => type_;

    /// <summary>Number of peaks in the raw peak list.</summary>
    public int NumRawPeaks => rawPeaks_.Count;

    /// <summary>Number of peaks in the processed peak list.</summary>
    public int NumProcessedPeaks => processedPeaks_.Count;

    /// <summary>
    /// Sum of intensities of all raw peaks. If a cached TIC was supplied via
    /// <see cref="TotalIonCurrentRaw"/> setter, returns that instead.
    /// </summary>
    /// <remarks>cpp Spectrum.cpp:172 — uses a sentinel of -1 to mean "compute".</remarks>
    public double TotalIonCurrentRaw
    {
        get
        {
            if (totalIonCurrentRaw_ < 0)
            {
                double sum = 0;
                foreach (var p in rawPeaks_) sum += p.Intensity;
                return sum;
            }
            return totalIonCurrentRaw_;
        }
        set => totalIonCurrentRaw_ = value;
    }

    /// <summary>
    /// Sum of intensities of all processed peaks. If a cached TIC was supplied via
    /// <see cref="TotalIonCurrentProcessed"/> setter, returns that instead.
    /// </summary>
    public double TotalIonCurrentProcessed
    {
        get
        {
            if (totalIonCurrentProcessed_ < 0)
            {
                double sum = 0;
                foreach (var p in processedPeaks_) sum += p.Intensity;
                return sum;
            }
            return totalIonCurrentProcessed_;
        }
        set => totalIonCurrentProcessed_ = value;
    }

    /// <summary>
    /// Waters Mse high-energy product-ion drift-time offset. When unset, computed from
    /// raw-peak drift-time values minus the precursor's ion mobility.
    /// </summary>
    /// <remarks>cpp Spectrum.cpp:185 — only computes when offset is zero and at least one peak
    /// has a non-zero drift time.</remarks>
    public double IonMobilityHighEnergyOffset
    {
        get
        {
            if (ionMobilityHighEnergyOffset_ == 0)
            {
                double sum = 0;
                foreach (var p in rawPeaks_) sum += p.DriftTime;
                if (sum > 0 && rawPeaks_.Count > 0)
                    return (sum / rawPeaks_.Count) - IonMobility;
            }
            return ionMobilityHighEnergyOffset_;
        }
        set => ionMobilityHighEnergyOffset_ = value;
    }

    /// <summary>Base-peak intensity over raw peaks. Computed on demand if not cached.</summary>
    public double BasePeakIntensityRaw
    {
        get
        {
            if (basePeakIntensityRaw_ < 0)
            {
                return FindBasePeak(rawPeaks_).Intensity;
            }
            return basePeakIntensityRaw_;
        }
    }

    /// <summary>Base-peak intensity over processed peaks. Computed on demand if not cached.</summary>
    public double BasePeakIntensityProcessed
    {
        get
        {
            if (basePeakIntensityProcessed_ < 0)
            {
                return FindBasePeak(processedPeaks_).Intensity;
            }
            return basePeakIntensityProcessed_;
        }
    }

    /// <summary>m/z of the base peak in the raw peak list.</summary>
    public double BasePeakMzRaw => FindBasePeak(rawPeaks_).Mz;

    /// <summary>m/z of the base peak in the processed peak list.</summary>
    public double BasePeakMzProcessed => FindBasePeak(processedPeaks_).Mz;

    /// <summary>Possible precursor charges (often empty if unknown).</summary>
    public IReadOnlyList<int> PossibleCharges => possibleCharges_;

    /// <summary>Raw (unfiltered) peak list.</summary>
    public IReadOnlyList<PeakT> RawPeaks => rawPeaks_;

    /// <summary>Processed (filtered / normalised) peak list.</summary>
    public IReadOnlyList<PeakT> ProcessedPeaks => processedPeaks_;

    /// <summary>
    /// Heuristic signal-to-noise estimate: mean intensity of the top 5 raw peaks divided by
    /// the median raw-peak intensity.
    /// </summary>
    /// <remarks>
    /// <para>cpp Spectrum.cpp:257 — note that this method <em>sorts the raw peak list in place</em>
    /// (by decreasing intensity). That side-effect is preserved here.</para>
    /// <para>cpp quirk: signal loop runs <c>for (i=1; i&lt;size &amp;&amp; i&lt;6; ++i)</c>, so it skips
    /// the very top peak and averages peaks 1..5 (zero-based), giving at most 5 entries.
    /// Preserved verbatim.</para>
    /// </remarks>
    public double GetSignalToNoise()
    {
        var size = rawPeaks_.Count;
        if (size == 0) return 0;

        // Sort by decreasing intensity (mz as tiebreaker, matching cpp compPeakInt).
        rawPeaks_.Sort(static (p1, p2) =>
        {
            if (p1.Intensity > p2.Intensity) return -1;
            if (p1.Intensity < p2.Intensity) return 1;
            // cpp Spectrum.h:75 compPeakInt has a bug — it checks intensity twice and never
            // hits the mz branch in practice. Preserve the observable effect (tied intensities
            // produce a stable-ish order; we fall back to mz desc to match the intent).
            if (p1.Mz > p2.Mz) return -1;
            if (p1.Mz < p2.Mz) return 1;
            return 0;
        });

        double signal = 0.0;
        var signalPeaks = 0;
        for (var i = 1; i != size && i < 6; i++)
        {
            signal += rawPeaks_[i].Intensity;
            signalPeaks++;
        }
        if (signalPeaks > 0) signal /= signalPeaks;

        double noise = (size % 2 == 0)
            ? (rawPeaks_[size / 2 - 1].Intensity + rawPeaks_[size / 2].Intensity) / 2.0
            : rawPeaks_[size / 2].Intensity;

        return noise == 0 ? 0 : signal / noise;
    }

    /// <summary>Replace the raw peak list.</summary>
    public void SetRawPeaks(IEnumerable<PeakT> newPeaks)
    {
        ArgumentNullException.ThrowIfNull(newPeaks);
        rawPeaks_ = new List<PeakT>(newPeaks);
    }

    /// <summary>Replace the processed peak list.</summary>
    public void SetProcessedPeaks(IEnumerable<PeakT> newPeaks)
    {
        ArgumentNullException.ThrowIfNull(newPeaks);
        processedPeaks_ = new List<PeakT>(newPeaks);
    }

    /// <summary>Set the precursor m/z.</summary>
    public void SetMz(double mz) => mz_ = mz;

    /// <summary>Set the ion-mobility value and units in one call.</summary>
    public void SetIonMobility(double im, IonMobilityType type)
    {
        ionMobility_ = im;
        ionMobilityType_ = type;
    }

    /// <summary>Add a possible precursor charge state. Subclasses may override to constrain.</summary>
    public virtual void AddCharge(int charge) => possibleCharges_.Add(charge);

    /// <summary>
    /// Find the peak with the largest intensity (ties broken by larger m/z). Returns
    /// <c>default</c> for an empty list.
    /// </summary>
    private static PeakT FindBasePeak(IReadOnlyList<PeakT> peaks)
    {
        if (peaks.Count == 0) return default;
        var best = peaks[0];
        for (var i = 1; i < peaks.Count; i++)
        {
            var p = peaks[i];
            if (p.Intensity > best.Intensity ||
                (p.Intensity == best.Intensity && p.Mz > best.Mz))
            {
                best = p;
            }
        }
        return best;
    }
}
