using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;

namespace Pwiz.Analysis.PeakFilters;

/// <summary>
/// ETD/ECD post-acquisition spectral cleanup. Port of pwiz cpp's <c>PrecursorMassFilter</c>
/// (<c>pwiz/analysis/spectrum_processing/PrecursorMassFilter.cpp</c>) wired up via the
/// <c>etdFilter</c> dispatch entry.
/// </summary>
/// <remarks>
/// For each MSn precursor selected ion, optionally removes
/// <list type="bullet">
///   <item>the unfragmented precursor m/z (<see cref="RemovePrecursor"/>);</item>
///   <item>charge-reduced precursors at every charge from <c>charge - 1</c> down to 1
///   (<see cref="RemoveChargeReducedPrecursors"/>);</item>
///   <item>neutral-loss species offset from each charge-reduced precursor by the formulae in
///   <see cref="DefaultNeutralLossFormulae"/> (when <see cref="UseBlanketFiltering"/> is false),
///   OR a charge-scaled 60-Da window below each charge-reduced precursor (when
///   <see cref="UseBlanketFiltering"/> is true).</item>
/// </list>
/// Finally, drops every fragment above (max precursor neutral mass) - 60 Da.
/// </remarks>
public sealed class EtdPrecursorMassFilter : ISpectrumDataFilter
{
    private const double LeftWindow = 60.0;

    /// <summary>m/z matching tolerance for fragment-vs-reference equality (cpp default 0.1 m/z).</summary>
    public MZTolerance MatchingTolerance { get; }

    /// <summary>Remove the unfragmented precursor m/z.</summary>
    public bool RemovePrecursor { get; }

    /// <summary>Remove charge-reduced precursors at every reduced charge state.</summary>
    public bool RemoveChargeReducedPrecursors { get; }

    /// <summary>Use a charge-scaled 60-Da blanket window below each charge-reduced precursor
    /// instead of explicit neutral-loss formulae. When true, <see cref="NeutralLossSpecies"/> is
    /// empty.</summary>
    public bool UseBlanketFiltering { get; }

    /// <summary>Neutral-loss formulae offset from each charge-reduced precursor. Empty when
    /// <see cref="UseBlanketFiltering"/> is true.</summary>
    public IReadOnlyList<Formula> NeutralLossSpecies { get; }

    /// <summary>Default neutral-loss formulae from the literature (PUB 1 / PUB 2 / PUB 3 — see
    /// the cpp header comments). Used when no override list is supplied.</summary>
    public static IReadOnlyList<string> DefaultNeutralLossFormulae { get; } = new[]
    {
        // (PUB 2)
        "H1", "N1H2",
        // PUB 1 Table 1
        "N1H3", "H2O1", "C1O1", "C1H4O1",
        "N2H6", "H5N1O1", "H4O2",
        "C1H3N2", "C1H4N2", "C1H3N1O1", "C1H2O2",
        "C2H6O1", "C2H5N1O1", "C1H5N3", "C2H4O2",
        // PUB 3
        "C4H11N1", "C3H6S1", "C4H6N2", "C3H8N3",
        "C4H9N3", "C4H11N3", "C7H8O1", "C9H9N1",
    };

    /// <summary>Creates the filter.</summary>
    public EtdPrecursorMassFilter(
        MZTolerance? matchingTolerance = null,
        bool removePrecursor = true,
        bool removeChargeReducedPrecursors = true,
        bool useBlanketFiltering = false,
        IEnumerable<string>? neutralLossFormulae = null)
    {
        MatchingTolerance = matchingTolerance ?? new MZTolerance(0.1);
        RemovePrecursor = removePrecursor;
        RemoveChargeReducedPrecursors = removeChargeReducedPrecursors;
        UseBlanketFiltering = useBlanketFiltering;

        if (useBlanketFiltering)
        {
            NeutralLossSpecies = Array.Empty<Formula>();
        }
        else
        {
            var formulae = neutralLossFormulae ?? DefaultNeutralLossFormulae;
            var list = new List<Formula>();
            foreach (var s in formulae) list.Add(new Formula(s));
            NeutralLossSpecies = list;
        }
    }

    /// <inheritdoc/>
    public void Apply(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);

        if (!IsEtdMsn(spectrum)) return;

        var mzArr = spectrum.GetMZArray();
        var intArr = spectrum.GetIntensityArray();
        if (mzArr is null || intArr is null) return;

        var mz = mzArr.Data;
        var inten = intArr.Data;
        int n = System.Math.Min(mz.Count, inten.Count);
        if (n == 0) return;

        double upperMassRange = spectrum.Params.CvParamValueOrDefault(CVID.MS_highest_observed_m_z, 10_000.0);
        double maxPrecursorNeutralMass = 0;
        var chargeStates = new List<int>();
        var filterMasses = new List<ReferenceMass>();

        foreach (var precursor in spectrum.Precursors)
        {
            foreach (var ion in precursor.SelectedIons)
            {
                double precMz = ion.CvParamValueOrDefault(CVID.MS_selected_ion_m_z, 0.0);
                if (precMz == 0) precMz = ion.CvParamValueOrDefault(CVID.MS_m_z, 0.0);
                if (precMz == 0) continue;

                if (RemovePrecursor)
                    filterMasses.Add(new ReferenceMass(ReferenceMassType.Precursor, precMz, 0));

                int charge = ion.CvParamValueOrDefault(CVID.MS_charge_state, 0);
                if (charge == 0) continue;

                chargeStates.Add(charge);
                double neutralMass = Ion.NeutralMass(precMz, charge);
                if (neutralMass > maxPrecursorNeutralMass) maxPrecursorNeutralMass = neutralMass;

                if (!RemoveChargeReducedPrecursors) continue;

                for (int reducedCharge = charge - 1; reducedCharge > 0; reducedCharge--)
                {
                    int electronDelta = charge - reducedCharge;
                    double reducedMz = Ion.Mz(neutralMass, charge, electronDelta);
                    if (reducedMz < upperMassRange)
                        filterMasses.Add(new ReferenceMass(ReferenceMassType.ChargeReduced, reducedMz, reducedCharge));

                    foreach (var nl in NeutralLossSpecies)
                    {
                        double nlMz = Ion.Mz(neutralMass - nl.MonoisotopicMass, charge, electronDelta);
                        if (nlMz < upperMassRange)
                            filterMasses.Add(new ReferenceMass(ReferenceMassType.NeutralLoss, nlMz, reducedCharge));
                    }
                }
            }
        }

        // cpp: only proceed if we built any filter masses AND there's a single precursor charge
        // state in the spectrum (multi-charge MS/MS gets passed through unchanged).
        if (filterMasses.Count == 0 || chargeStates.Count >= 2) return;

        filterMasses.Sort();

        var keep = new bool[n];
        for (int i = 0; i < n; i++) keep[i] = true;

        foreach (var rm in filterMasses)
        {
            // Optional left-side blanket: when filtering charge-reduced precursors with blanket
            // mode, the left tolerance balloons to (60 / charge) Da to catch arbitrary neutral losses.
            MZTolerance leftTol = MatchingTolerance;
            MZTolerance rightTol = MatchingTolerance;
            if (UseBlanketFiltering && rm.Type == ReferenceMassType.ChargeReduced && rm.Charge > 0)
                leftTol = new MZTolerance(LeftWindow / rm.Charge);

            for (int i = 0; i < n; i++)
            {
                if (!keep[i]) continue;
                if (mz[i] < rm.Mass)
                {
                    if (rm.Mass - mz[i] <= AbsTol(leftTol, rm.Mass)) keep[i] = false;
                }
                else
                {
                    if (mz[i] - rm.Mass <= AbsTol(rightTol, rm.Mass)) keep[i] = false;
                }
            }
        }

        // Drop everything above maxPrecursorNeutralMass - 60.
        if (maxPrecursorNeutralMass > 0)
        {
            double upperBound = maxPrecursorNeutralMass - 60;
            for (int i = 0; i < n; i++)
                if (mz[i] >= upperBound - AbsTol(MatchingTolerance, upperBound)) keep[i] = false;
        }

        Compact(spectrum, keep, n, mz, inten, intArr);
    }

    private static double AbsTol(MZTolerance t, double anchor) => t.Units switch
    {
        MZToleranceUnits.Mz => t.Value,
        MZToleranceUnits.Ppm => System.Math.Abs(anchor) * t.Value * 1e-6,
        _ => t.Value,
    };

    private static bool IsEtdMsn(Spectrum spectrum)
    {
        if (spectrum.DefaultArrayLength == 0) return false;
        if (spectrum.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) <= 1) return false;
        if (!spectrum.Params.HasCVParam(CVID.MS_MSn_spectrum)) return false;
        if (spectrum.Precursors.Count == 0) return false;
        if (spectrum.Precursors[0].SelectedIons.Count == 0) return false;
        if (spectrum.Precursors[0].SelectedIons[0].IsEmpty) return false;
        if (!spectrum.Precursors[0].Activation.HasCVParam(CVID.MS_electron_transfer_dissociation)) return false;
        return true;
    }

    private static void Compact(Spectrum spectrum, bool[] keep, int n,
        IReadOnlyList<double> mz, IReadOnlyList<double> inten, BinaryDataArray intArr)
    {
        int kept = 0;
        for (int i = 0; i < n; i++) if (keep[i]) kept++;
        var newMz = new double[kept];
        var newInt = new double[kept];
        int j = 0;
        for (int i = 0; i < n; i++)
        {
            if (!keep[i]) continue;
            newMz[j] = mz[i];
            newInt[j] = inten[i];
            j++;
        }
        CVID intensityUnits = CVID.MS_number_of_detector_counts;
        foreach (var p in intArr.CVParams)
            if (p.Units != CVID.CVID_Unknown) { intensityUnits = p.Units; break; }
        spectrum.SetMZIntensityArrays(newMz, newInt, intensityUnits);
    }

    private enum ReferenceMassType { Precursor, ChargeReduced, NeutralLoss }

    private readonly record struct ReferenceMass(ReferenceMassType Type, double Mass, int Charge)
        : IComparable<ReferenceMass>
    {
        public int CompareTo(ReferenceMass other) => Mass.CompareTo(other.Mass);
    }
}
