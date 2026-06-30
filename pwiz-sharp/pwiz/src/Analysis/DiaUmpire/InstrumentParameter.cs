namespace Pwiz.Analysis.DiaUmpire;

/// <summary>
/// DIA-Umpire instrument-parameter struct. Port of cpp
/// <c>DiaUmpire::InstrumentParameter</c> in <c>InstrumentParameter.hpp</c>.
/// All fields are mutable; <see cref="Config"/> populates them from a
/// <c>.params</c> file and then hands the populated <c>InstrumentParameter</c>
/// to the <c>DiaUmpire</c> algorithm.
/// </summary>
/// <remarks>
/// Defaults below match cpp <c>InstrumentParameter.hpp</c> field initializers
/// (the TTOF5600 defaults). <see cref="Config"/>'s ctor overrides a subset of
/// these (MS1PPM=30, MS2PPM=40, etc.) before applying the user's params file.
/// </remarks>
public sealed class InstrumentParameter
{
    /// <summary>Instrument resolution at m/z 200 (used as the resolution-vs-m/z model anchor).</summary>
    public int Resolution { get; set; }
    /// <summary>MS1 peak-detection ppm tolerance.</summary>
    public float MS1PPM { get; set; }
    /// <summary>MS2 peak-detection ppm tolerance.</summary>
    public float MS2PPM { get; set; }
    /// <summary>Signal-to-noise threshold for MS1 peaks.</summary>
    public float SN { get; set; }
    /// <summary>Minimum peak intensity required to consider an MS1 peak.</summary>
    public float MinMSIntensity { get; set; }
    /// <summary>Minimum peak intensity required to consider an MS2 peak.</summary>
    public float MinMSMSIntensity { get; set; }
    /// <summary>Cap on the number of peaks emitted per minute of run time.</summary>
    public int NoPeakPerMin { get; set; } = 150;
    /// <summary>Minimum retention-time span (minutes) for a peak curve to be kept.</summary>
    public float MinRTRange { get; set; }
    /// <summary>Lowest precursor charge state considered (MS1 deisotoping).</summary>
    public int StartCharge { get; set; } = 2;
    /// <summary>Highest precursor charge state considered (MS1 deisotoping).</summary>
    public int EndCharge { get; set; } = 5;
    /// <summary>Lowest fragment charge state considered (MS2 deisotoping).</summary>
    public int MS2StartCharge { get; set; } = 2;
    /// <summary>Highest fragment charge state considered (MS2 deisotoping).</summary>
    public int MS2EndCharge { get; set; } = 4;
    /// <summary>Maximum retention-time range (minutes) for a single peak curve.</summary>
    public float MaxCurveRTRange { get; set; } = 2;
    /// <summary>Retention-time tolerance (minutes) when linking MS1↔MS2 features.</summary>
    public float RTtol { get; set; }
    /// <summary>Signal-to-noise threshold for MS2 peaks.</summary>
    public float MS2SN { get; set; }
    /// <summary>Maximum number of peaks per MS1 isotope cluster.</summary>
    public int MaxNoPeakCluster { get; set; } = 4;
    /// <summary>Minimum number of peaks per MS1 isotope cluster.</summary>
    public int MinNoPeakCluster { get; set; } = 2;
    /// <summary>Maximum number of peaks per MS2 isotope cluster.</summary>
    public int MaxMS2NoPeakCluster { get; set; } = 3;
    /// <summary>Minimum number of peaks per MS2 isotope cluster.</summary>
    public int MinMS2NoPeakCluster { get; set; } = 2;
    /// <summary>Run background-noise denoising pass before peak detection.</summary>
    public bool Denoise { get; set; } = true;
    /// <summary>Estimate background noise empirically (vs. fixed threshold).</summary>
    public bool EstimateBG { get; set; }
    /// <summary>Determine background level from identified spectra.</summary>
    public bool DetermineBGByID { get; set; }
    /// <summary>Remove peak clusters that share many peaks with stronger neighbors.</summary>
    public bool RemoveGroupedPeaks { get; set; } = true;
    /// <summary>Apply deisotoping to merge isotopologue peaks.</summary>
    public bool Deisotoping { get; set; }
    /// <summary>Boost intensity of complementary-fragment ion pairs (b/y).</summary>
    public bool BoostComplementaryIon { get; set; } = true;
    /// <summary>Apply quality-score-based fragment intensity adjustment.</summary>
    public bool AdjustFragIntensity { get; set; } = true;
    /// <summary>Maximum peaks-per-region budget (precursor side).</summary>
    public int RPmax { get; set; } = 25;
    /// <summary>Maximum peaks-per-region budget (fragment side).</summary>
    public int RFmax { get; set; } = 300;
    /// <summary>Minimum fraction of overlapping retention time for MS1↔MS2 pairing.</summary>
    public float RTOverlap { get; set; } = 0.3f;
    /// <summary>Minimum cross-correlation for MS1↔MS2 pairing.</summary>
    public float CorrThreshold { get; set; } = 0.2f;
    /// <summary>Maximum allowed apex-time delta (minutes) for MS1↔MS2 pairing.</summary>
    public float DeltaApex { get; set; } = 0.6f;
    /// <summary>Symmetry score threshold for peak-curve filtering.</summary>
    public float SymThreshold { get; set; } = 0.3f;
    /// <summary>Tolerance (in scans) for gap-filling within a peak curve.</summary>
    public int NoMissedScan { get; set; } = 1;
    /// <summary>Minimum peaks per peak curve (otherwise discarded).</summary>
    public int MinPeakPerPeakCurve { get; set; } = 1;
    /// <summary>Lower m/z bound (Da) — peaks below are ignored.</summary>
    public float MinMZ { get; set; } = 200;
    /// <summary>Minimum number of fragment peaks required for a pseudo-MS/MS.</summary>
    public int MinFrag { get; set; } = 10;
    /// <summary>Minimum fragment-curve overlap fraction for pairing.</summary>
    public float MiniOverlapP { get; set; } = 0.2f;
    /// <summary>Reject clusters whose mono-isotopic peak isn't at the curve apex.</summary>
    public bool CheckMonoIsotopicApex { get; set; }
    /// <summary>Use continuous wavelet transform for peak detection (vs. simple max).</summary>
    public bool DetectByCWT { get; set; } = true;
    /// <summary>Fill peak-curve gaps via baseline kernel.</summary>
    public bool FillGapByBK { get; set; } = true;
    /// <summary>Cross-correlation threshold for isotope-pattern verification.</summary>
    public float IsoCorrThreshold { get; set; } = 0.2f;
    /// <summary>Threshold for grouped-peak removal.</summary>
    public float RemoveGroupedPeaksCorr { get; set; } = 0.3f;
    /// <summary>RT-overlap threshold for grouped-peak removal.</summary>
    public float RemoveGroupedPeaksRTOverlap { get; set; } = 0.3f;
    /// <summary>Threshold above which a pair counts as "high correlation".</summary>
    public float HighCorrThreshold { get; set; } = 0.7f;
    /// <summary>Minimum count of high-corr fragments required to emit a pseudo-MS/MS.</summary>
    public int MinHighCorrCnt { get; set; } = 10;
    /// <summary>Top-N local fragments kept per pseudo-MS/MS.</summary>
    public int TopNLocal { get; set; } = 6;
    /// <summary>Local-range window for the top-N filter (Da).</summary>
    public int TopNLocalRange { get; set; } = 100;
    /// <summary>Minimum isotope-pattern correlation.</summary>
    public float IsoPattern { get; set; } = 0.3f;
    /// <summary>Retention-time window start (minutes; 0 = no clip).</summary>
    public float StartRT { get; set; }
    /// <summary>Retention-time window end (minutes; 9999 = no clip).</summary>
    public float EndRT { get; set; } = 9999;
    /// <summary>Restrict pseudo-MS/MS generation to target-only mode.</summary>
    public bool TargetIDOnly { get; set; }
    /// <summary>Apply mass-defect filter to precursor candidates.</summary>
    public bool MassDefectFilter { get; set; } = true;
    /// <summary>Minimum precursor monoisotopic mass (Da).</summary>
    public float MinPrecursorMass { get; set; } = 600;
    /// <summary>Maximum precursor monoisotopic mass (Da).</summary>
    public float MaxPrecursorMass { get; set; } = 15000;
    /// <summary>Use older DIA-Umpire scoring (compat flag from Java port).</summary>
    public bool UseOldVersion { get; set; }
    /// <summary>RT window for targeted-only mode (-1 = no override).</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
        Justification = "Matches cpp DiaUmpire::InstrumentParameter::RT_window_Targeted field name for parity")]
    public float RT_window_Targeted { get; set; } = -1;
    /// <summary>Smoothing window size for peak-curve smoothing (scans).</summary>
    public int SmoothFactor { get; set; } = 5;
    /// <summary>Only pair MS1↔MS2 clusters of the same charge.</summary>
    public bool DetectSameChargePairOnly { get; set; }
    /// <summary>Mass-defect offset tolerance.</summary>
    public float MassDefectOffset { get; set; } = 0.1f;
    /// <summary>Top-N fragment-cluster candidates per MS1 precursor.</summary>
    public int MS2PairTopN { get; set; } = 5;
    /// <summary>Enable MS1↔MS2 cluster pairing.</summary>
    public bool MS2Pairing { get; set; } = true;

    /// <summary>Returns the parameter set as a string-keyed map, used to emit
    /// DataProcessing userParams in the output mzML.</summary>
    public Dictionary<string, string> GetParameterMap() => new()
    {
        ["Resolution"] = Resolution.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["MS1PPM"] = MS1PPM.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["MS2PPM"] = MS2PPM.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["SN"] = SN.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["MS2SN"] = MS2SN.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["MinMSIntensity"] = MinMSIntensity.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["MinMSMSIntensity"] = MinMSMSIntensity.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["MinRTRange"] = MinRTRange.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["MaxNoPeakCluster"] = MaxNoPeakCluster.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["MinNoPeakCluster"] = MinNoPeakCluster.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["MaxMS2NoPeakCluster"] = MaxMS2NoPeakCluster.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["MinMS2NoPeakCluster"] = MinMS2NoPeakCluster.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["MaxCurveRTRange"] = MaxCurveRTRange.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["RTtol"] = RTtol.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["Denoise"] = Denoise.ToString(),
        ["EstimateBG"] = EstimateBG.ToString(),
        ["RemoveGroupedPeaks"] = RemoveGroupedPeaks.ToString(),
        ["RPmax"] = RPmax.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["RFmax"] = RFmax.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["RTOverlap"] = RTOverlap.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["CorrThreshold"] = CorrThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["DeltaApex"] = DeltaApex.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["MinFrag"] = MinFrag.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>m/z ppm delta between two masses, unsigned.</summary>
    public static float CalcPPM(float valueA, float valueB) =>
        System.Math.Abs(valueA - valueB) * 1_000_000f / valueB;

    /// <summary>m/z ppm delta between two masses, signed (A relative to B).</summary>
    public static float CalcSignedPPM(float valueA, float valueB) =>
        (valueA - valueB) * 1_000_000f / valueB;

    /// <summary>Returns the lower m/z bound of the ppm window around <paramref name="valueA"/>.</summary>
    public static float GetMzByPPM(float valueA, int charge, float ppm)
    {
        const float ProtonMass = 1.00727f;
        float mwA = valueA * charge - charge * ProtonMass;
        float premass = mwA - (ppm * mwA / 1_000_000f);
        return (premass + charge * ProtonMass) / charge;
    }
}

/// <summary>Inclusive m/z range used by DIA window targeting.</summary>
public readonly struct MzRange : System.IEquatable<MzRange>, System.IComparable<MzRange>
{
    /// <summary>Begin m/z (inclusive).</summary>
    public float Begin { get; }
    /// <summary>End m/z (inclusive).</summary>
    public float End { get; }

    /// <summary>Creates a range.</summary>
    public MzRange(float begin, float end) { Begin = begin; End = end; }

    /// <summary>Sentinel empty range.</summary>
    public static MzRange Empty { get; } = new MzRange(0, 0);

    /// <inheritdoc/>
    public bool Equals(MzRange other) => Begin == other.Begin && End == other.End;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is MzRange r && Equals(r);
    /// <inheritdoc/>
    public override int GetHashCode() => System.HashCode.Combine(Begin, End);
    /// <inheritdoc/>
    public int CompareTo(MzRange other) =>
        Begin == other.Begin ? End.CompareTo(other.End) : Begin.CompareTo(other.Begin);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(MzRange a, MzRange b) => a.Equals(b);
    /// <summary>Inequality operator.</summary>
    public static bool operator !=(MzRange a, MzRange b) => !a.Equals(b);
    /// <summary>Less-than operator (Begin first, then End).</summary>
    public static bool operator <(MzRange a, MzRange b) => a.CompareTo(b) < 0;
    /// <summary>Greater-than operator.</summary>
    public static bool operator >(MzRange a, MzRange b) => a.CompareTo(b) > 0;
    /// <summary>Less-or-equal operator.</summary>
    public static bool operator <=(MzRange a, MzRange b) => a.CompareTo(b) <= 0;
    /// <summary>Greater-or-equal operator.</summary>
    public static bool operator >=(MzRange a, MzRange b) => a.CompareTo(b) >= 0;
}
