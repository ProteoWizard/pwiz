using Pwiz.Analysis.PeakFilters;
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

/// <summary>Which m/z value on the precursor to test against the predicate's set.</summary>
public enum PrecursorMzTarget
{
    /// <summary>Use the selected-ion m/z (cpp <c>MS_selected_ion_m_z</c>) — the inferred precursor.</summary>
    Selected,

    /// <summary>Use the isolation-window target m/z (cpp <c>MS_isolation_window_target_m_z</c>) — the
    /// nominal isolation center, which can differ from the selected-ion m/z when the SDK
    /// recentered the precursor inside the window.</summary>
    Isolated,
}

/// <summary>
/// Accepts spectra whose precursor m/z is within <see cref="Tolerance"/> of any value in <see cref="MzSet"/>.
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

    /// <summary>Which precursor m/z value to test (selected vs. isolated).</summary>
    public PrecursorMzTarget Target { get; }

    /// <summary>Creates a predicate that matches spectra with any precursor m/z near a value in <paramref name="mzSet"/>.</summary>
    public PrecursorMzPredicate(IEnumerable<double> mzSet, MZTolerance tolerance,
        FilterMode mode = FilterMode.Include, PrecursorMzTarget target = PrecursorMzTarget.Selected)
    {
        ArgumentNullException.ThrowIfNull(mzSet);
        MzSet = new HashSet<double>(mzSet);
        Tolerance = tolerance;
        Mode = mode;
        Target = target;
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
            // cpp SpectrumList_Filter.cpp:415-441: target=Selected scans precursor.selectedIons
            // for MS_selected_ion_m_z; target=Isolated reads MS_isolation_window_target_m_z from
            // the isolation window. Returns the FIRST hit per precursor and breaks.
            if (Target == PrecursorMzTarget.Selected)
            {
                foreach (var si in precursor.SelectedIons)
                {
                    var mzParam = si.CvParam(CVID.MS_selected_ion_m_z);
                    if (mzParam.IsEmpty) continue;
                    double mz = mzParam.ValueAs<double>();
                    if (MatchesAny(mz)) { matched = true; goto done; }
                    break;
                }
            }
            else
            {
                var iwParam = precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z);
                if (iwParam.IsEmpty) continue;
                double mz = iwParam.ValueAs<double>();
                if (MatchesAny(mz)) { matched = true; goto done; }
            }
        }
        done:
        return Mode == FilterMode.Include ? matched : !matched;
    }

    private bool MatchesAny(double mz)
    {
        foreach (var target in MzSet)
            if (MZTolerance.IsWithinTolerance(mz, target, Tolerance))
                return true;
        return false;
    }

    /// <inheritdoc/>
    public string Describe() => $"set of precursor m/z values (target={Target}, tolerance={Tolerance})";
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

/// <summary>
/// Accepts spectra whose precursor isolation window matches any (low, high) pair in the set,
/// within the configured tolerance. Port of
/// <c>SpectrumList_FilterPredicate_IsolationWindowSet</c>.
/// </summary>
public sealed class IsolationWindowPredicate : ISpectrumPredicate
{
    private readonly List<(double Low, double High)> _windows;
    private readonly MZTolerance _tolerance;
    private readonly FilterMode _mode;

    /// <summary>Creates an isolation-window predicate.</summary>
    public IsolationWindowPredicate(IEnumerable<(double Low, double High)> windows,
        MZTolerance tolerance, FilterMode mode = FilterMode.Include)
    {
        ArgumentNullException.ThrowIfNull(windows);
        _windows = windows.ToList();
        _tolerance = tolerance;
        _mode = mode;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FastMetadata;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        // cpp SpectrumList_Filter.cpp:481-494: window = (target - lowerOffset, target + upperOffset)
        // from the first precursor that has all three CV params set. Empty isolation info on an
        // MS2 is indeterminate (treated as no match). The (0, 0) special case for MS1 spectra
        // pre-filters here: no precursor → nothing to match against → not in set.
        var window = GetIsolationWindow(spectrum);
        bool found = false;
        if (window.Low + window.High > 0)
        {
            foreach (var (lo, hi) in _windows)
            {
                if (Math.Abs(lo - window.Low) < _tolerance.Value
                    + (_tolerance.Units == MZToleranceUnits.Ppm ? Math.Abs(window.Low) * _tolerance.Value * 1e-6 : 0)
                    && Math.Abs(hi - window.High) < _tolerance.Value
                    + (_tolerance.Units == MZToleranceUnits.Ppm ? Math.Abs(window.High) * _tolerance.Value * 1e-6 : 0))
                {
                    found = true;
                    break;
                }
            }
        }
        return _mode == FilterMode.Include ? found : !found;
    }

    private static (double Low, double High) GetIsolationWindow(Spectrum spectrum)
    {
        foreach (var precursor in spectrum.Precursors)
        {
            var upper = precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_upper_offset);
            var lower = precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_lower_offset);
            var target = precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z);
            if (upper.IsEmpty || lower.IsEmpty || target.IsEmpty) continue;
            double mz = target.ValueAs<double>();
            return (mz - Math.Abs(lower.ValueAs<double>()), mz + upper.ValueAs<double>());
        }
        return (0, 0);
    }

    /// <inheritdoc/>
    public string Describe() => $"set of isolation windows (tolerance={_tolerance})";
}

/// <summary>
/// Accepts spectra whose precursor isolation width (lower+upper offset) matches any value in the set,
/// within the configured tolerance. Port of
/// <c>SpectrumList_FilterPredicate_IsolationWidthSet</c>.
/// </summary>
public sealed class IsolationWidthPredicate : ISpectrumPredicate
{
    private readonly List<double> _widths;
    private readonly MZTolerance _tolerance;
    private readonly FilterMode _mode;

    /// <summary>Creates an isolation-width predicate.</summary>
    public IsolationWidthPredicate(IEnumerable<double> widths, MZTolerance tolerance,
        FilterMode mode = FilterMode.Include)
    {
        ArgumentNullException.ThrowIfNull(widths);
        _widths = widths.ToList();
        _tolerance = tolerance;
        _mode = mode;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FastMetadata;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        double width = GetIsolationWidth(spectrum);
        bool found = false;
        if (width > 0)
        {
            foreach (var target in _widths)
                if (MZTolerance.IsWithinTolerance(width, target, _tolerance))
                {
                    found = true;
                    break;
                }
        }
        return _mode == FilterMode.Include ? found : !found;
    }

    private static double GetIsolationWidth(Spectrum spectrum)
    {
        foreach (var precursor in spectrum.Precursors)
        {
            var upper = precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_upper_offset);
            var lower = precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_lower_offset);
            if (upper.IsEmpty || lower.IsEmpty) continue;
            return upper.ValueAs<double>() + Math.Abs(lower.ValueAs<double>());
        }
        return 0;
    }

    /// <inheritdoc/>
    public string Describe() => $"set of isolation widths (tolerance={_tolerance})";
}

/// <summary>
/// Accepts spectra that contain at least one peak (within the configured tolerance) at one of
/// the m/z values in the set, after a <see cref="ThresholdFilter"/> is applied to the spectrum.
/// Port of <c>SpectrumList_FilterPredicate_MzPresent</c>.
/// </summary>
public sealed class MzPresentPredicate : ISpectrumPredicate
{
    private readonly MZTolerance _tolerance;
    private readonly List<double> _mzSet;
    private readonly ThresholdFilter _thresholdFilter;
    private readonly FilterMode _mode;

    /// <summary>Creates a predicate testing whether thresholded peaks contain any of <paramref name="mzSet"/>.</summary>
    public MzPresentPredicate(MZTolerance tolerance, IEnumerable<double> mzSet,
        ThresholdFilter thresholdFilter, FilterMode mode = FilterMode.Include)
    {
        ArgumentNullException.ThrowIfNull(mzSet);
        ArgumentNullException.ThrowIfNull(thresholdFilter);
        _tolerance = tolerance;
        _mzSet = mzSet.ToList();
        _thresholdFilter = thresholdFilter;
        _mode = mode;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FullData;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        var mzArray = spectrum.GetMZArray();
        var intensityArray = spectrum.GetIntensityArray();
        if (mzArray is null || intensityArray is null) return false;

        // cpp clones the Spectrum and runs the threshold filter on the copy, then walks
        // the surviving m/z values (SpectrumList_Filter.cpp:705-720). Mirror by building a
        // minimal Spectrum carrying just the binary arrays + ms level — that's what the
        // ThresholdFilter touches — so we don't deep-clone the full structure.
        var copy = new Spectrum
        {
            DefaultArrayLength = Math.Min(mzArray.Data.Count, intensityArray.Data.Count),
        };
        copy.Params.Set(CVID.MS_ms_level, spectrum.Params.CvParamValueOrDefault(CVID.MS_ms_level, 1));
        var copyMz = new BinaryDataArray();
        copyMz.Set(CVID.MS_m_z_array, string.Empty, CVID.MS_m_z);
        copyMz.Data.AddRange(mzArray.Data);
        var copyInt = new BinaryDataArray();
        copyInt.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
        copyInt.Data.AddRange(intensityArray.Data);
        copy.BinaryDataArrays.Add(copyMz);
        copy.BinaryDataArrays.Add(copyInt);

        _thresholdFilter.Apply(copy);

        bool matched = false;
        var survivingMz = copy.GetMZArray();
        if (survivingMz is not null)
        {
            foreach (var mz in survivingMz.Data)
            {
                foreach (var target in _mzSet)
                    if (MZTolerance.IsWithinTolerance(mz, target, _tolerance))
                    {
                        matched = true;
                        goto done;
                    }
            }
        }
        done:
        return _mode == FilterMode.Include ? matched : !matched;
    }

    /// <inheritdoc/>
    public string Describe() => $"presence of m/z values (tolerance={_tolerance})";
}

/// <summary>
/// Accepts spectra whose first scan's instrument configuration includes a mass analyzer matching
/// any CV in the configured set, restricted to MS levels in the configured set.
/// Port of <c>SpectrumList_FilterPredicate_AnalyzerType</c>.
/// </summary>
public sealed class AnalyzerTypePredicate : ISpectrumPredicate
{
    private readonly HashSet<CVID> _terms;
    private readonly IntegerSet _msLevels;

    /// <summary>Creates an analyzer-type predicate.</summary>
    public AnalyzerTypePredicate(IEnumerable<CVID> cvIDs, IntegerSet msLevels)
    {
        ArgumentNullException.ThrowIfNull(cvIDs);
        ArgumentNullException.ThrowIfNull(msLevels);
        _terms = new HashSet<CVID>(cvIDs);
        _msLevels = msLevels;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FastMetadata;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);

        // cpp SpectrumList_Filter.cpp:635-674: filter out-of-scope ms levels with `true`
        // (don't affect non-matching levels), need full metadata for the analyzer lookup.
        int msLevel = spectrum.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
        if (msLevel == 0) return false;
        if (!_msLevels.Contains(msLevel)) return true;

        if (spectrum.ScanList.Scans.Count == 0) return false;
        var ic = spectrum.ScanList.Scans[0].InstrumentConfiguration;
        if (ic is null) return false;

        foreach (var component in ic.ComponentList)
        {
            var cvParam = component.CvParamChild(CVID.MS_mass_analyzer_type);
            if (cvParam.IsEmpty) continue;
            foreach (var target in _terms)
                if (CvLookup.CvIsA(cvParam.Cvid, target))
                    return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public string Describe() => "mass analyzer type";
}

/// <summary>
/// Accepts MS2+ CID spectra whose collision energy is within <c>[low, high]</c>. Non-MS spectra,
/// MS1 spectra, and (optionally) non-CID activations are pass-through-accepted.
/// Port of <c>SpectrumList_FilterPredicate_CollisionEnergy</c>.
/// </summary>
public sealed class CollisionEnergyPredicate : ISpectrumPredicate
{
    private readonly double _low;
    private readonly double _high;
    private readonly bool _acceptNonCID;
    private readonly bool _acceptMissingCE;
    private readonly FilterMode _mode;

    /// <summary>Creates a CE-range predicate.</summary>
    public CollisionEnergyPredicate(double low, double high,
        bool acceptNonCID = true, bool acceptMissingCE = false, FilterMode mode = FilterMode.Include)
    {
        if (low < 0 || high < 0) throw new ArgumentException("low and high must be ≥ 0.");
        _low = Math.Min(low, high);
        _high = Math.Max(low, high);
        _acceptNonCID = acceptNonCID;
        _acceptMissingCE = acceptMissingCE;
        _mode = mode;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FastMetadata;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        int msLevel = spectrum.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
        if (msLevel == 0) return true; // non-MS or unknown — pass through
        if (msLevel == 1) return true; // MS1 — pass through
        if (spectrum.Precursors.Count == 0) return false;

        var activation = spectrum.Precursors[0].Activation;
        bool hasCID = false;
        foreach (var p in activation.CVParams)
            if (CvLookup.CvIsA(p.Cvid, CVID.MS_collision_induced_dissociation))
            {
                hasCID = true;
                break;
            }
        if (!hasCID) return _acceptNonCID;

        var ce = activation.CvParam(CVID.MS_collision_energy);
        if (ce.IsEmpty) return _acceptMissingCE;
        double v = ce.ValueAs<double>();
        bool inRange = v >= _low && v <= _high;
        return _mode == FilterMode.Include ? inRange : !inRange;
    }

    /// <inheritdoc/>
    public string Describe() => $"collision energy in [{_low}, {_high}]";
}

/// <summary>
/// Accepts spectra whose first scan's <c>MS_filter_string</c> CV value matches the configured
/// search string (Thermo-only). Port of <c>SpectrumList_FilterPredicate_ThermoScanFilter</c>.
/// </summary>
public sealed class ThermoScanFilterPredicate : ISpectrumPredicate
{
    private readonly string _matchString;
    private readonly bool _matchExact;
    private readonly bool _inverse;

    /// <summary>Creates a Thermo scan-filter predicate.</summary>
    public ThermoScanFilterPredicate(string matchString, bool matchExact, bool inverse)
    {
        ArgumentNullException.ThrowIfNull(matchString);
        _matchString = matchString;
        _matchExact = matchExact;
        _inverse = inverse;
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
        var p = spectrum.ScanList.Scans[0].CvParam(CVID.MS_filter_string);
        if (p.IsEmpty) return false;
        bool pass = _matchExact ? p.Value == _matchString : p.Value.Contains(_matchString, StringComparison.Ordinal);
        return _inverse ? !pass : pass;
    }

    /// <inheritdoc/>
    public string Describe() => $"thermo scan filter '{_matchString}' (exact={_matchExact}, inverse={_inverse})";
}

/// <summary>
/// Drops MS1 spectra acquired in an ion trap (cpp <c>StripIonTrapSurveyScans</c>). Port of the
/// inline predicate in <c>SpectrumListFactory.cpp</c>.
/// </summary>
public sealed class StripIonTrapMs1Predicate : ISpectrumPredicate
{
    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FastMetadata;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        int msLevel = spectrum.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
        if (msLevel != 1) return true;  // not an MS1 — leave alone

        if (spectrum.ScanList.Scans.Count == 0) return true;
        var ic = spectrum.ScanList.Scans[0].InstrumentConfiguration;
        if (ic is null) return true;

        foreach (var component in ic.ComponentList)
        {
            var cv = component.CvParamChild(CVID.MS_mass_analyzer_type);
            if (!cv.IsEmpty && CvLookup.CvIsA(cv.Cvid, CVID.MS_ion_trap))
                return false;  // ion-trap MS1 — drop
        }
        return true;
    }

    /// <inheritdoc/>
    public string Describe() => "stripping ion trap MS1s";
}
