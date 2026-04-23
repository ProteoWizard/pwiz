using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Filters;

/// <summary>
/// Accepts spectra whose precursor charge state is in the given set.
/// Port of pwiz::analysis::SpectrumList_FilterPredicate_ChargeStateSet.
/// </summary>
public sealed class ChargeStatePredicate : ISpectrumPredicate
{
    private readonly IntegerSet _set;

    /// <summary>Creates a predicate over the given set of charge states.</summary>
    public ChargeStatePredicate(IntegerSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        _set = set;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FastMetadata;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        // MS1 spectra have no precursor, so no charge — exclude them.
        if (spectrum.Precursors.Count == 0) return false;

        foreach (var precursor in spectrum.Precursors)
        {
            foreach (var si in precursor.SelectedIons)
            {
                var cs = si.CvParam(CVID.MS_charge_state);
                if (cs.IsEmpty) continue;
                if (_set.Contains(cs.ValueAs<int>())) return true;
            }
        }
        return false;
    }

    /// <inheritdoc/>
    public string Describe() => "set of charge states";
}

/// <summary>
/// Accepts spectra whose activation (dissociation) method is in a set of CV terms (e.g. CID, HCD, ETD).
/// Uses CV hierarchy (is_a), so passing <see cref="CVID.MS_collision_induced_dissociation"/> matches
/// any child term.
/// Port of pwiz::analysis::SpectrumList_FilterPredicate_ActivationType.
/// </summary>
public sealed class ActivationTypePredicate : ISpectrumPredicate
{
    private readonly HashSet<CVID> _terms;
    private readonly bool _hasNoneOf;

    /// <summary>
    /// Creates an activation-type predicate.
    /// </summary>
    /// <param name="activationTerms">Activation CV terms to accept.</param>
    /// <param name="hasNoneOf">If true, accept spectra whose activation has none of the terms (inversion).</param>
    public ActivationTypePredicate(IEnumerable<CVID> activationTerms, bool hasNoneOf = false)
    {
        ArgumentNullException.ThrowIfNull(activationTerms);
        _terms = new HashSet<CVID>(activationTerms);
        _hasNoneOf = hasNoneOf;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FastMetadata;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        // Only MSn spectra have activation.
        if (spectrum.Precursors.Count == 0) return _hasNoneOf;

        foreach (var precursor in spectrum.Precursors)
        {
            foreach (var p in precursor.Activation.CVParams)
            {
                foreach (var target in _terms)
                {
                    if (CvLookup.CvIsA(p.Cvid, target))
                        return !_hasNoneOf;
                }
            }
        }
        return _hasNoneOf;
    }

    /// <inheritdoc/>
    public string Describe() => "set of activation types";
}

/// <summary>
/// Accepts spectra whose selected-ion m/z is within <see cref="Tolerance"/> of any value in <see cref="MzSet"/>.
/// Port of pwiz::analysis::SpectrumList_FilterPredicate_PrecursorMzSet.
/// </summary>
public sealed class PrecursorMzPredicate : ISpectrumPredicate
{
    /// <summary>Target m/z values.</summary>
    public IReadOnlySet<double> MzSet { get; }

    /// <summary>Matching tolerance (Da or ppm).</summary>
    public MZTolerance Tolerance { get; }

    /// <summary>Whether to include or exclude matching spectra.</summary>
    public FilterMode Mode { get; }

    /// <summary>Creates a predicate that matches spectra with any precursor m/z near a value in <paramref name="mzSet"/>.</summary>
    public PrecursorMzPredicate(IEnumerable<double> mzSet, MZTolerance tolerance, FilterMode mode = FilterMode.Include)
    {
        ArgumentNullException.ThrowIfNull(mzSet);
        MzSet = new HashSet<double>(mzSet);
        Tolerance = tolerance;
        Mode = mode;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FastMetadata;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);

        bool matched = false;
        foreach (var precursor in spectrum.Precursors)
        {
            foreach (var si in precursor.SelectedIons)
            {
                var mzParam = si.CvParam(CVID.MS_selected_ion_m_z);
                if (mzParam.IsEmpty) continue;
                double mz = mzParam.ValueAs<double>();
                foreach (var target in MzSet)
                {
                    if (MZTolerance.IsWithinTolerance(mz, target, Tolerance))
                    {
                        matched = true;
                        goto done;
                    }
                }
            }
        }
        done:
        return Mode == FilterMode.Include ? matched : !matched;
    }

    /// <inheritdoc/>
    public string Describe() => $"set of precursor m/z values (tolerance={Tolerance})";
}

/// <summary>
/// Accepts spectra whose scan event number (from "scanEvent=N" native id) is in the given set.
/// Port of pwiz::analysis::SpectrumList_FilterPredicate_ScanEventSet.
/// </summary>
public sealed class ScanEventPredicate : ISpectrumPredicate
{
    private readonly IntegerSet _set;

    /// <summary>Creates a predicate over the given set of scan event numbers.</summary>
    public ScanEventPredicate(IntegerSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        _set = set;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FastMetadata;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        if (spectrum.ScanList.Scans.Count == 0) return false;

        var preset = spectrum.ScanList.Scans[0].CvParam(CVID.MS_preset_scan_configuration);
        if (preset.IsEmpty) return false;
        return _set.Contains(preset.ValueAs<int>());
    }

    /// <inheritdoc/>
    public string Describe() => "set of scan events";
}
