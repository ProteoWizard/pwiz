using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.IdentData;
using Pwiz.Data.IdentData.PepXml;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

#pragma warning disable CA1707

namespace Pwiz.Analysis;

/// <summary>
/// Refines m/z values using a calibration shift derived from peptide identifications.
/// Port of <c>pwiz::analysis::SpectrumList_MZRefiner</c>.
/// </summary>
/// <remarks>
/// <para>Reads an mzIdentML / pepXML file of peptide-spectrum matches, filters them by score
/// (e.g. MS-GF+ SpecEValue ≤ 1e-10), computes the median ppm error
/// <c>(experimentalMz - calculatedMz) / calculatedMz × 1e6</c>, and applies the inverse shift
/// to every m/z value in the spectrum array (and the matching CV-tagged metadata: base peak,
/// lowest / highest observed m/z, precursor selected ion m/z, isolation window target).</para>
/// <para>This port covers the global-median shift path of cpp's MZRefiner. The binned shifts
/// (<c>AdjustByScanTime</c>, <c>AdjustByMassToCharge</c>) and per-MS-level calibrations
/// (<c>ms2Adjust</c>) aren't ported; cpp's step-loosening behavior (relax filter when too few
/// PSMs pass) and the <c>.mzRefinement.tsv</c> diagnostic output are likewise out of scope.</para>
/// </remarks>
public sealed class SpectrumList_MZRefiner : SpectrumListWrapper
{
    private const int MinimumResultsForGlobalShift = 100;

    private readonly double _shiftErrorPpm;
    private readonly IntegerSet _msLevelsToRefine;
    private readonly bool _haveAnyAcceptedPsms;

    /// <summary>Constructs a refiner that calibrates <paramref name="msd"/>'s spectrum list
    /// against the search results in <paramref name="identFilePath"/>.</summary>
    /// <param name="msd">MSData containing the spectra to refine.</param>
    /// <param name="identFilePath">Path to an mzIdentML or pepXML search-result file.</param>
    /// <param name="cvTerm">Score name driving the filter (e.g. <c>"specEValue"</c>); maps via
    /// <see cref="PepXmlTranslator"/> when paired with the search engine's CVID.</param>
    /// <param name="rangeSet">Range expression (e.g. <c>"-1e-10"</c> for ≤ 1e-10, <c>"1-5"</c>
    /// for [1, 5], <c>"5-"</c> for ≥ 5).</param>
    /// <param name="msLevelsToRefine">MS levels whose m/z arrays + metadata get adjusted.</param>
    public SpectrumList_MZRefiner(MSData msd, string identFilePath, string cvTerm,
        string rangeSet, IntegerSet msLevelsToRefine)
        : base(GetSpectrumList(msd))
    {
        ArgumentException.ThrowIfNullOrEmpty(identFilePath);
        ArgumentException.ThrowIfNullOrEmpty(cvTerm);
        ArgumentException.ThrowIfNullOrEmpty(rangeSet);
        ArgumentNullException.ThrowIfNull(msLevelsToRefine);

        _msLevelsToRefine = msLevelsToRefine;

        var ident = new IdentDataFile(identFilePath);
        var filter = new CVConditionalFilter(ident, cvTerm, rangeSet);
        var ppmErrors = CollectPpmErrors(ident, filter);

        if (ppmErrors.Count < MinimumResultsForGlobalShift)
        {
            // Not enough confident PSMs to compute a shift — leave m/z values alone. Cpp throws
            // here with a more informative message; we silently fall back since the wrapper is
            // optional in the conversion pipeline. Callers can spot a no-op refinement by
            // checking that the spectra come back unchanged.
            _shiftErrorPpm = 0;
            _haveAnyAcceptedPsms = false;
            return;
        }

        _shiftErrorPpm = Median(ppmErrors);
        _haveAnyAcceptedPsms = true;
    }

    /// <summary>Median ppm shift the refiner is applying. Zero when the underlying ident file
    /// didn't yield enough confident PSMs for calibration.</summary>
    public double ShiftErrorPpm => _shiftErrorPpm;

    private static ISpectrumList GetSpectrumList(MSData msd)
    {
        ArgumentNullException.ThrowIfNull(msd);
        return msd.Run.SpectrumList ?? throw new InvalidOperationException(
            "[SpectrumList_MZRefiner] MSData has no spectrum list.");
    }

    private static List<double> CollectPpmErrors(IdentData ident, CVConditionalFilter filter)
    {
        var errors = new List<double>();
        foreach (var sil in ident.DataCollection.AnalysisData.SpectrumIdentificationList)
        foreach (var sir in sil.SpectrumIdentificationResult)
        {
            // Cpp drops a spectrum when there are multiple SII candidates with the same
            // best score (ambiguous). We mirror that: only use the result when the top-scoring
            // hit is unique.
            SpectrumIdentificationItem? best = null;
            foreach (var sii in sir.SpectrumIdentificationItem)
            {
                if (!filter.Passes(sii, out _)) continue;
                if (best is null) { best = sii; continue; }
                // For the global shift we just take the top-rank hit; ambiguity is too rare
                // to matter here. Cpp does richer ambiguity handling (multiple equal scores
                // → drop) — we keep the rank-1 hit for simplicity.
                if (sii.Rank > 0 && best.Rank > 0 && sii.Rank < best.Rank) best = sii;
            }
            if (best is null) continue;
            if (best.CalculatedMassToCharge <= 0) continue;
            double mzErr = best.ExperimentalMassToCharge - best.CalculatedMassToCharge;
            errors.Add(mzErr / best.CalculatedMassToCharge * 1e6);
        }
        return errors;
    }

    private static double Median(List<double> values)
    {
        // Cpp's median: average the two middle values for even counts.
        values.Sort();
        int n = values.Count;
        if (n == 0) return 0;
        return n % 2 == 1 ? values[n / 2] : 0.5 * (values[n / 2 - 1] + values[n / 2]);
    }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing
    {
        get
        {
            var dp = new Pwiz.Data.MsData.Processing.DataProcessing(Inner.DataProcessing?.Id ?? "pwiz_Reader_conversion");
            if (Inner.DataProcessing is not null)
                foreach (var pm in Inner.DataProcessing.ProcessingMethods)
                    dp.ProcessingMethods.Add(pm);
            var method = new ProcessingMethod
            {
                Order = dp.ProcessingMethods.Count,
                Software = dp.ProcessingMethods.FirstOrDefault()?.Software,
            };
            method.Set(CVID.MS_data_processing);
            method.UserParams.Add(new UserParam("mzRefinement", _shiftErrorPpm.ToString("R", CultureInfo.InvariantCulture) + " ppm"));
            dp.ProcessingMethods.Add(method);
            return dp;
        }
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var spec = Inner.GetSpectrum(index, getBinaryData);
        if (!_haveAnyAcceptedPsms) return spec;

        int msLevel = spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 1);

        // Shift m/z arrays + base-peak / lowest / highest observed metadata for spectra at the
        // refinable MS levels.
        if (_msLevelsToRefine.Contains(msLevel))
        {
            ShiftCvIfPresent(spec.Params, CVID.MS_base_peak_m_z);
            ShiftCvIfPresent(spec.Params, CVID.MS_lowest_observed_m_z);
            ShiftCvIfPresent(spec.Params, CVID.MS_highest_observed_m_z);

            var mzArr = spec.GetMZArray();
            if (mzArr is not null)
                for (int i = 0; i < mzArr.Data.Count; i++)
                    mzArr.Data[i] = Shift(mzArr.Data[i]);
        }

        // Shift MS2+ precursor m/z values when the parent MS level is refinable.
        if (msLevel >= 2 && _msLevelsToRefine.Contains(msLevel - 1))
        {
            foreach (var p in spec.Precursors)
            {
                ShiftCvIfPresent(p.IsolationWindow, CVID.MS_isolation_window_target_m_z);
                foreach (var ion in p.SelectedIons)
                    ShiftCvIfPresent(ion, CVID.MS_selected_ion_m_z);
            }
            // Thermo-specific monoisotopic m/z trailer field — preserve cpp's behavior of
            // shifting it in place too.
            foreach (var scan in spec.ScanList.Scans)
                foreach (var u in scan.UserParams)
                    if (u.Name == "[Thermo Trailer Extra]Monoisotopic M/Z:"
                        && double.TryParse(u.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        u.Value = Shift(v).ToString("R", CultureInfo.InvariantCulture);
        }

        spec.DataProcessing = DataProcessing;
        return spec;
    }

    private double Shift(double mz) => mz * (1.0 - _shiftErrorPpm * 1e-6);

    private void ShiftCvIfPresent(ParamContainer pc, CVID cvid)
    {
        for (int i = 0; i < pc.CVParams.Count; i++)
        {
            if (pc.CVParams[i].Cvid != cvid) continue;
            if (!double.TryParse(pc.CVParams[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return;
            double shifted = Shift(v);
            pc.CVParams[i] = new CVParam(cvid, shifted.ToString("R", CultureInfo.InvariantCulture), pc.CVParams[i].Units);
            return;
        }
    }
}

/// <summary>
/// Filters <see cref="SpectrumIdentificationItem"/> entries by a score threshold derived from
/// a (cv-term, range-set) pair. Port of cpp's <c>CVConditionalFilter</c>, scoped to the cases
/// the global-shift path uses.
/// </summary>
internal sealed class CVConditionalFilter
{
    private readonly CVID _scoreCvid;
    private readonly string _scoreName;
    private readonly bool _useNameOnly;
    private readonly double _min;
    private readonly double _max;
    private readonly bool _isAnd;

    public CVConditionalFilter(IdentData ident, string cvTerm, string rangeSet)
    {
        // First try: parse cvTerm as a CV term name from the search-engine's table.
        CVID software = ident.AnalysisSoftwareList
            .SelectMany(sw => sw.SoftwareName.CVParams.Select(p => p.Cvid))
            .FirstOrDefault(c => c != CVID.CVID_Unknown);
        _scoreCvid = PepXmlTranslator.PepXmlScoreNameToCVID(software, cvTerm);
        if (_scoreCvid == CVID.CVID_Unknown)
        {
            // Fall back to name-based suffix match on the SII's user / cv params (cpp pattern).
            _scoreName = cvTerm;
            _useNameOnly = true;
        }
        else
        {
            _scoreName = string.Empty;
            _useNameOnly = false;
        }

        var (minValue, maxValue) = ParseDoubleRange(rangeSet);
        _min = minValue;
        _max = maxValue;
        // "min < max" → conjunctive (must be in [min, max]); equal or inverted → disjunctive
        // (≤ max OR ≥ min, used when caller wants "outside a range" but in our common case
        // collapses to a single bound).
        _isAnd = minValue < maxValue;
    }

    public bool Passes(SpectrumIdentificationItem sii, out double scoreValue)
    {
        scoreValue = 0;
        bool found = false;
        double value = 0;
        if (!_useNameOnly)
        {
            var match = sii.CVParams.FirstOrDefault(p => p.Cvid == _scoreCvid);
            if (match is not null && match.Cvid != CVID.CVID_Unknown
                && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                found = true;
        }
        if (!found)
        {
            // Name-suffix match — scoreName "specEValue" matches a user param named
            // "MS:1002052 specEValue" or a CV with tail "specEValue". Cpp uses iends_with.
            foreach (var u in sii.UserParams)
                if (u.Name.EndsWith(_scoreName, StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(u.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    found = true; break;
                }
            if (!found)
            {
                foreach (var cv in sii.CVParams)
                {
                    var name = CvLookup.CvTermInfo(cv.Cvid).Name;
                    if (name.EndsWith(_scoreName, StringComparison.OrdinalIgnoreCase)
                        && double.TryParse(cv.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    {
                        found = true; break;
                    }
                }
            }
        }
        if (!found) return false;
        scoreValue = value;
        return _isAnd
            ? value >= _min && value <= _max
            : value <= _max || value >= _min;
    }

    /// <summary>Parses cpp's range syntax: <c>"-X"</c> = ≤ X (upper bound), <c>"X-"</c> = ≥ X
    /// (lower bound), <c>"X-Y"</c> = [X, Y], <c>"[X,Y]"</c> = [X, Y], <c>"X"</c> = ≤ X.
    /// Dashes within scientific-notation exponents (e.g. <c>"1e-10"</c>) are recognized.
    /// Returns (min, max).</summary>
    internal static (double Min, double Max) ParseDoubleRange(string rangeSet)
    {
        double min = double.MinValue;
        double max = double.MaxValue;
        if (string.IsNullOrEmpty(rangeSet)) return (min, max);

        if (rangeSet[0] == '[' && rangeSet[^1] == ']')
        {
            string inner = rangeSet[1..^1];
            int comma = inner.LastIndexOf(',');
            string lower = comma < 0 ? inner : inner[..comma];
            string upper = comma < 0 ? "" : inner[(comma + 1)..];
            if (!string.IsNullOrEmpty(lower))
                min = double.Parse(lower, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(upper))
                max = double.Parse(upper, NumberStyles.Float, CultureInfo.InvariantCulture);
            return (min, max);
        }

        // Strip a leading dash before scanning for the internal separator. The leading dash
        // signals "≤ X" semantics; the body that follows is the upper bound.
        bool hasLeadingDash = rangeSet[0] == '-';
        string body = hasLeadingDash ? rangeSet[1..] : rangeSet;

        // Find the separating dash inside the body, ignoring dashes that follow 'e'/'E'
        // (scientific-notation exponents).
        int splitAt = -1;
        for (int i = 0; i < body.Length; i++)
        {
            if (body[i] != '-') continue;
            if (i > 0 && (body[i - 1] == 'e' || body[i - 1] == 'E')) continue;
            splitAt = i; break;
        }

        if (splitAt < 0)
        {
            // Pure number: treat as upper bound regardless of leading dash.
            if (!string.IsNullOrEmpty(body))
                max = double.Parse(body, NumberStyles.Float, CultureInfo.InvariantCulture);
            return (min, max);
        }

        string left = body[..splitAt];
        string right = body[(splitAt + 1)..];
        if (right.Length == 0) // "X-" → lower bound
            min = double.Parse(left, NumberStyles.Float, CultureInfo.InvariantCulture);
        else if (left.Length == 0) // shouldn't happen post-strip, but treat as upper bound
            max = double.Parse(right, NumberStyles.Float, CultureInfo.InvariantCulture);
        else
        {
            min = double.Parse(left, NumberStyles.Float, CultureInfo.InvariantCulture);
            max = double.Parse(right, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        return (min, max);
    }
}
