using System.Globalization;
using Pwiz.Analysis.PeakPicking;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

#pragma warning disable CA1707

namespace Pwiz.Analysis;

/// <summary>
/// Assigns precursor charge states to MSn spectra from the isotope pattern in the
/// surrounding survey (MS1) scans. Port of <c>pwiz::analysis::SpectrumList_ChargeFromIsotope</c>
/// — exposed by msconvert as the <c>turbocharger</c> filter.
/// </summary>
/// <remarks>
/// <para>For every MSn spectrum the filter looks at <c>parentsBefore</c> + <c>parentsAfter</c>
/// surrounding MS1 scans, extracts peaks inside the isolation window, enumerates "isotope
/// chains" (peaks separated by ~<c>massNeutron / z</c> for some z), scores each chain on
/// three independent axes (m/z spacing SSE, Kullback-Leibler vs a Poisson isotope envelope,
/// and intensity rank sum), pools the chains across all parents, ranks by the sum of the
/// three rank positions, and picks the lowest-sum-of-ranks chain whose K-L p-value is below
/// <c>sigVal = 0.30</c>. The first peak in that chain becomes the new monoisotopic m/z, and
/// the chain's spacing implies the charge state.</para>
/// <para>Each score axis is evaluated against a Monte-Carlo distribution that's
/// pre-simulated in the constructor (10,000 samples per parameter combination). The cpp
/// implementation seeds the C runtime's RNG with <c>1234</c> for reproducibility; we mirror
/// that with <see cref="MsvcRand"/> (MSVC's LCG) so the simulated distributions are
/// bit-identical to cpp's.</para>
/// </remarks>
public sealed class SpectrumList_ChargeFromIsotope : SpectrumListWrapper
{
    // Algorithm constants (cpp SpectrumList_ChargeFromIsotope.cpp:75-86).
    private const bool DefaultOverride = true;
    private const double SigVal = 0.30;
    private const int NChainsCheck = 8;
    private const double MzTol = 0.06;
    private const int MaxIsotopePeaks = 5;
    private const int MinIsotopePeaks = 2;
    private const int MaxNumberPeaks = 40;
    private const int MinNumberPeaks = 2;
    private const double MassNeutron = 1.00335;
    private const int NSamples = 10000;
    private const double UpperLimitPadding = 1.25;
    private const int NumberIntensePeaks = 200;
    private const double Lambda = 1.0 / 1800.0; // Poisson model (msInspect paper)

    private readonly int _maxCharge;
    private readonly int _minCharge;
    private readonly int _parentsBefore;
    private readonly int _parentsAfter;
    private readonly int _defaultChargeMax;
    private readonly int _defaultChargeMin;
    private readonly double _defaultIsolationWidth;

    // LRU cache for full-data parent MS1 spectra: every MSn in a DDA Top-N batch points
    // at the same parents, so loading once and reusing saves ~N-1 binary-data decodes per
    // batch. Capacity 16 covers Top-30 DDA with parentsBefore+parentsAfter ≤ 4 (each MSn
    // touches ≤ 4 unique parents, batches share parents). Reads/writes are protected by
    // the cache's internal lock so concurrent GetSpectrum calls (test harness, msconvert
    // parallel flag) stay safe.
    private readonly ParentSpectrumCache _parentCache = new(capacity: 16);

    // Pre-simulated distributions (sorted ascending so upper_bound → p-value index).
    private readonly double[] _simulatedSSEs;
    private readonly double[] _simulatedKLs;
    private readonly int[] _simulatedIntensityRankSum;
    private readonly List<RtimeMap> _ms1RetentionTimes = new();

    /// <summary>Constructs a turbocharger over the spectra in <paramref name="msd"/>.</summary>
    /// <param name="msd">Source MSData; the MS1 retention-time index is built here.</param>
    /// <param name="maxCharge">Highest charge to consider when matching isotope spacings.</param>
    /// <param name="minCharge">Lowest charge to consider when matching isotope spacings.</param>
    /// <param name="parentsBefore">Number of MS1 scans before the MSn to use as survey.</param>
    /// <param name="parentsAfter">Number of MS1 scans after the MSn to use as survey.</param>
    /// <param name="isolationWidth">Half-width of the precursor isolation window (Th), used
    /// when the spectrum doesn't carry an explicit upper/lower isolation offset.</param>
    /// <param name="defaultChargeMax">When no isotope is found, possible-charge-state CV
    /// params for [<paramref name="defaultChargeMin"/>, <paramref name="defaultChargeMax"/>]
    /// are emitted. Set both to 0 to leave the precursor uncharged.</param>
    /// <param name="defaultChargeMin">See <paramref name="defaultChargeMax"/>.</param>
    public SpectrumList_ChargeFromIsotope(MSData msd, int maxCharge = 3, int minCharge = 1,
        int parentsBefore = 2, int parentsAfter = 0, double isolationWidth = 1.25,
        int defaultChargeMax = 0, int defaultChargeMin = 0)
        : base(GetSpectrumList(msd))
    {
        if (minCharge < 1) throw new ArgumentException("minCharge must be ≥ 1", nameof(minCharge));
        if (maxCharge < minCharge) throw new ArgumentException("maxCharge must be ≥ minCharge", nameof(maxCharge));
        if (isolationWidth <= 0) throw new ArgumentException("isolationWidth must be > 0", nameof(isolationWidth));

        _maxCharge = maxCharge;
        _minCharge = minCharge;
        _parentsBefore = parentsBefore;
        _parentsAfter = parentsAfter;
        _defaultIsolationWidth = isolationWidth;
        _defaultChargeMax = defaultChargeMax;
        _defaultChargeMin = defaultChargeMin;

        // Cpp does srand(1234) on every construction. We use a fresh deterministic LCG;
        // the simulate*() routines pass it around so all three simulations consume the same
        // sequence as MSVC's rand() after a single srand(1234) call.
        var rng = new MsvcRand(1234);

        BuildMs1RetentionTimeIndex();
        int surveyCnt = _ms1RetentionTimes.Count;
        if (surveyCnt == 0)
            throw new InvalidOperationException("[SpectrumList_chargeFromIsotope] No survey scan found!");
        double finalRetentionTime = _ms1RetentionTimes[surveyCnt - 1].Rtime;

        int nCharges = _maxCharge - _minCharge + 1;
        int nIsotopePeakPossibilities = MaxIsotopePeaks - MinIsotopePeaks + 1;

        _simulatedSSEs = SimulateSse(rng, nCharges, nIsotopePeakPossibilities);
        _simulatedKLs = SimulateKL(rng, finalRetentionTime, nIsotopePeakPossibilities,
            System.Math.Min(9, surveyCnt));
        _simulatedIntensityRankSum = SimulateTotIntensity(rng, nIsotopePeakPossibilities);
    }

    private static ISpectrumList GetSpectrumList(MSData msd)
    {
        ArgumentNullException.ThrowIfNull(msd);
        return msd.Run.SpectrumList ?? throw new InvalidOperationException(
            "[SpectrumList_ChargeFromIsotope] MSData has no spectrum list.");
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var s = Inner.GetSpectrum(index, true);

        // Skip non-MS/MS, MS1, peakless, and precursorless spectra.
        var spectrumType = s.Params.CvParamChild(CVID.MS_spectrum_type);
        if (spectrumType.Cvid != CVID.MS_MSn_spectrum) return s;
        if (!s.Params.HasCVParam(CVID.MS_ms_level)
            || s.Params.CvParam(CVID.MS_ms_level).ValueAs<int>() < 2) return s;
        if (s.DefaultArrayLength == 0) return s;
        if (s.Precursors.Count == 0 || s.Precursors[0].SelectedIons.Count == 0) return s;

        var precursor = s.Precursors[0];
        var selectedIon = precursor.SelectedIons[0];

        // Erase existing charge-state CV params per cpp's override logic; collect any
        // pre-existing "possible charge state" values so we can preserve them when not
        // overriding.
        var possibleChargeStates = new SortedSet<int>();
        for (int i = selectedIon.CVParams.Count - 1; i >= 0; i--)
        {
            var cv = selectedIon.CVParams[i];
            if (cv.Cvid != CVID.MS_charge_state && cv.Cvid != CVID.MS_possible_charge_state) continue;
            if (DefaultOverride || cv.Value == "0")
            {
                selectedIon.UserParams.Add(new UserParam("old charge state", cv.Value));
                selectedIon.CVParams.RemoveAt(i);
            }
            else if (cv.Cvid == CVID.MS_possible_charge_state)
                possibleChargeStates.Add(cv.ValueAs<int>());
            else if (cv.Cvid == CVID.MS_charge_state)
                return s; // existing charge, not overriding → leave alone
        }

        double precursorMz = selectedIon.CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>();
        int nPossibleChargeStates = _maxCharge - _minCharge + 1;
        int nIsotopePeakPossibilities = MaxIsotopePeaks - MinIsotopePeaks + 1;

        double upperIsoWidth = precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_upper_offset).ValueAs<double>();
        upperIsoWidth = upperIsoWidth > 0.0 ? upperIsoWidth : _defaultIsolationWidth;
        double lowerIsoWidth = precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_lower_offset).ValueAs<double>();
        lowerIsoWidth = lowerIsoWidth > 0.0 ? lowerIsoWidth : _defaultIsolationWidth;
        double targetIsoMz = precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>();
        if (targetIsoMz <= 0.0) targetIsoMz = precursorMz;

        var parentIndices = GetParentIndices(s);
        var allMzs = new List<double[]>(parentIndices.Count);
        var allIntensities = new List<double[]>(parentIndices.Count);
        GetParentPeaks(parentIndices, targetIsoMz, lowerIsoWidth, upperIsoWidth, allMzs, allIntensities);

        var allChains = new List<IsotopeChain>();
        var allScores = new List<ScoreChain>();
        int allChainsCnt = 0;
        int assignedCharge = 0;
        double assignedMz = targetIsoMz;

        for (int parentIdx = 0; parentIdx < parentIndices.Count; parentIdx++)
        {
            var peakMzs = allMzs[parentIdx];
            var peakIntensities = allIntensities[parentIdx];
            int nPeaks = peakMzs.Length;
            if (nPeaks > MaxNumberPeaks)
                throw new InvalidOperationException("[SpectrumList_chargeFromIsotope] nPeaks exceeds maxNumberPeaks for scoring.");

            var sortedPeakIntensities = (double[])peakIntensities.Clone();
            System.Array.Sort(sortedPeakIntensities);

            var chains = BuildIsotopeChains(peakMzs, targetIsoMz, upperIsoWidth, parentIdx);
            if (chains.Count == 0) continue;

            // Score every chain on the three axes.
            foreach (var chain in chains)
            {
                var score = new ScoreChain { ChainIndex = allChainsCnt++ };

                // (1) K-L score on relative intensity vs Poisson envelope.
                double klScore = GetKLScore(chain, peakMzs, peakIntensities);
                int klStart = (chain.IndexList.Count - MinIsotopePeaks) * NSamples;
                int klIdx = UpperBound(_simulatedKLs, klStart, klStart + NSamples - 1, klScore);
                score.IntensityPvalue = (double)(klIdx + 1) / (NSamples + 1);

                // (2) m/z spacing SSE vs the chain's implied monoisotope.
                double avg = 0;
                for (int k = 0; k < chain.IndexList.Count; k++)
                    avg += peakMzs[chain.IndexList[k]] - k * MassNeutron / chain.Charge;
                avg /= chain.IndexList.Count;
                double sse = 0;
                for (int k = 0; k < chain.IndexList.Count; k++)
                {
                    double d = avg - (peakMzs[chain.IndexList[k]] - k * MassNeutron / chain.Charge);
                    sse += d * d;
                }
                int sseStart = (chain.IndexList.Count - MinIsotopePeaks) * nPossibleChargeStates * NSamples
                    + (chain.Charge - _minCharge) * NSamples;
                int sseIdx = UpperBound(_simulatedSSEs, sseStart, sseStart + NSamples - 1, sse);
                score.MzPvalue = (double)(sseIdx + 1) / (NSamples + 1);

                // (3) Intensity rank sum.
                int rankSum = 0;
                foreach (int idx in chain.IndexList)
                {
                    double inten = peakIntensities[idx];
                    int r = UpperBound(sortedPeakIntensities, 0, sortedPeakIntensities.Length, inten);
                    rankSum += nPeaks - (r - 1);
                }
                int combos = (nPeaks > 40 && chain.IndexList.Count > 2)
                    ? NSamples
                    : System.Math.Min(NSamples, NChoosek(nPeaks, chain.IndexList.Count));
                int rsStart = (nPeaks - MinNumberPeaks) * nIsotopePeakPossibilities * NSamples
                    + (chain.IndexList.Count - MinIsotopePeaks) * NSamples;
                int rsIdx = UpperBound(_simulatedIntensityRankSum, rsStart, rsStart + combos - 1, rankSum);
                score.IntensitySumPvalue = (double)rsIdx / combos;

                score.OverallPvalue = score.MzPvalue * score.IntensityPvalue * score.IntensitySumPvalue;
                allScores.Add(score);
            }
            allChains.AddRange(chains);
        }

        if (allScores.Count > 0)
        {
            // Rank by each score axis, then pick the chain with the lowest sum-of-ranks
            // (works better empirically than overall p-value per cpp comment).
            int n = allScores.Count;
            allScores.Sort((a, b) => a.MzPvalue.CompareTo(b.MzPvalue));
            for (int j = 0; j < n; j++) allScores[j].MzRank = j + 1;
            allScores.Sort((a, b) => a.IntensityPvalue.CompareTo(b.IntensityPvalue));
            for (int j = 0; j < n; j++) allScores[j].IntensityRank = j + 1;
            allScores.Sort((a, b) => a.IntensitySumPvalue.CompareTo(b.IntensitySumPvalue));
            for (int j = 0; j < n; j++) allScores[j].IntensitySumRank = j + 1;
            for (int j = 0; j < n; j++)
                allScores[j].SumRanks = allScores[j].MzRank + allScores[j].IntensityRank + allScores[j].IntensitySumRank;
            allScores.Sort((a, b) => a.SumRanks.CompareTo(b.SumRanks));

            int scoreListLen = allScores.Count;
            int bestChainIndex = -1;
            int j2 = 0;
            int jend = System.Math.Min(scoreListLen, NChainsCheck);
            for (; j2 < jend; j2++)
            {
                if (allScores[j2].IntensityPvalue < SigVal) { bestChainIndex = allScores[j2].ChainIndex; break; }
            }

            if (bestChainIndex != -1)
            {
                // Try to walk to a longer (super-)chain at the same charge that shares the
                // first two m/z values — cpp extends 3-peak chains into 4-peak ones when
                // possible.
                const double mzTolPpm = 100.0;
                const double mzTolParts = mzTolPpm / 1_000_000.0;
                bool update = true;
                while (update)
                {
                    update = false;
                    int kend = System.Math.Min(scoreListLen, NChainsCheck);
                    for (int k = j2 + 1; k < kend; k++)
                    {
                        if (allScores[k].IntensityPvalue > SigVal) continue;
                        int mapK = allScores[k].ChainIndex;
                        if (allChains[bestChainIndex].Charge != allChains[mapK].Charge) continue;
                        int largeSize = allChains[mapK].IndexList.Count;
                        if (largeSize < 3) continue;

                        bool related = true;
                        double epsilon = mzTolParts * allMzs[allChains[mapK].ParentIndex][allChains[mapK].IndexList[0]];
                        for (int w = 0; w < 2; w++)
                        {
                            double smallMz = allMzs[allChains[bestChainIndex].ParentIndex][allChains[bestChainIndex].IndexList[w]];
                            double largeMz = allMzs[allChains[mapK].ParentIndex][allChains[mapK].IndexList[w + 1]];
                            if (System.Math.Abs(smallMz - largeMz) > epsilon) { related = false; break; }
                        }
                        if (related)
                        {
                            update = true;
                            bestChainIndex = mapK;
                            j2 = k;
                            break;
                        }
                    }
                }

                assignedCharge = allChains[bestChainIndex].Charge;
                assignedMz = allMzs[allChains[bestChainIndex].ParentIndex][allChains[bestChainIndex].IndexList[0]];
            }
        }

        // Strip any preserved possible-charge-state params when overriding.
        if (DefaultOverride && possibleChargeStates.Count > 0)
            selectedIon.CVParams.RemoveAll(p => p.Cvid == CVID.MS_possible_charge_state);

        if (assignedCharge != 0)
        {
            selectedIon.CVParams.Add(new CVParam(
                DefaultOverride ? CVID.MS_charge_state : CVID.MS_possible_charge_state,
                assignedCharge));
            selectedIon.Set(CVID.MS_selected_ion_m_z, assignedMz, CVID.MS_m_z);
        }
        else if (_defaultChargeMin > 0)
        {
            for (int z = _defaultChargeMin; z <= _defaultChargeMax; z++)
                if (!possibleChargeStates.Contains(z) && z != 1)
                    selectedIon.CVParams.Add(new CVParam(CVID.MS_possible_charge_state, z));
        }

        return s;
    }

    // ---- helpers ----

    private List<IsotopeChain> BuildIsotopeChains(double[] peakMzs, double targetIsoMz, double upperIsoWidth, int parentIdx)
    {
        var chains = new List<IsotopeChain>();
        int nPeaks = peakMzs.Length;
        if (nPeaks <= 1) return chains;

        for (int j = 0; j < nPeaks; j++)
        {
            if (peakMzs[j] > targetIsoMz + upperIsoWidth) break;
            for (int k = j + 1; k < nPeaks; k++)
            {
                double mzDiff = peakMzs[k] - peakMzs[j];
                if (mzDiff > MassNeutron + MzTol) break;
                double recip = MassNeutron / mzDiff;
                int possibleCharge = (int)(recip + 0.5);
                if (possibleCharge > _maxCharge || possibleCharge < _minCharge) continue;
                if (System.Math.Abs(MassNeutron / possibleCharge - mzDiff) >= MzTol) continue;

                // Extend any existing chain at this charge whose last peak is j.
                int chainsAtEntry = chains.Count;
                for (int w = 0; w < chainsAtEntry; w++)
                {
                    var existing = chains[w];
                    if (existing.Charge != possibleCharge) continue;
                    if (existing.IndexList.Count >= MaxIsotopePeaks) continue;
                    if (existing.IndexList[^1] != j) continue;
                    // Save a copy of the previous-size chain, then extend the original.
                    chains.Add(new IsotopeChain
                    {
                        Charge = existing.Charge,
                        IndexList = new List<int>(existing.IndexList),
                        ParentIndex = existing.ParentIndex,
                    });
                    existing.IndexList.Add(k);
                }

                // Also start a fresh length-2 chain (j, k).
                chains.Add(new IsotopeChain
                {
                    Charge = possibleCharge,
                    IndexList = new List<int>(2) { j, k },
                    ParentIndex = parentIdx,
                });
            }
        }

        // Cpp filter: charge > 4 must have ≥ 3 peaks to count.
        chains.RemoveAll(c => c.Charge > 4 && c.IndexList.Count < 3);
        return chains;
    }

    private List<int> GetParentIndices(Spectrum s)
    {
        var parents = new List<int>();
        if (_ms1RetentionTimes.Count == 0 || s.ScanList.Scans.Count == 0) return parents;

        double rTime = s.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time).TimeInSeconds();
        // upper_bound: first index with Rtime > rTime.
        int upper = UpperBoundRtime(rTime);
        int near = upper == 0 ? 0 : upper - 1;

        // parentsBefore moving toward index 0.
        int cnt = 0;
        for (int i = near; i >= 0 && cnt < _parentsBefore; i--, cnt++)
            parents.Add(_ms1RetentionTimes[i].IndexMap);

        // parentsAfter moving forward from upper (cpp resets the iterator to `i1`).
        cnt = 0;
        for (int i = upper; i < _ms1RetentionTimes.Count && cnt < _parentsAfter; i++, cnt++)
            parents.Add(_ms1RetentionTimes[i].IndexMap);

        return parents;
    }

    private int UpperBoundRtime(double rTime)
    {
        int lo = 0, hi = _ms1RetentionTimes.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (_ms1RetentionTimes[mid].Rtime <= rTime) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private void GetParentPeaks(List<int> parents, double targetIsoMz, double lowerIsoWidth, double upperIsoWidth,
        List<double[]> mzs, List<double[]> intensities)
    {
        foreach (int parentIdx in parents)
        {
            // Cached load. The cached entry holds (m/z[], intensity[], isCentroid) — the
            // metadata-only fetch path cpp uses gets folded into this single lookup since
            // FullData covers everything FullMetadata would have answered.
            var cached = _parentCache.GetOrLoad(parentIdx, LoadParent);

            double[] peakMzs;
            double[] peakInt;
            if (cached.IsCentroid)
            {
                var (mz, inten) = FilterByMz(cached.Mz, cached.Intensity,
                    targetIsoMz - lowerIsoWidth, targetIsoMz + upperIsoWidth + UpperLimitPadding);
                (peakMzs, peakInt) = TrimByMin(mz, inten, MaxNumberPeaks);
            }
            else
            {
                // CWT-pick the parent around the isolation window so we have centroided peaks.
                var picker = InstantiatePeakPickerForWindowFromCached(cached, targetIsoMz, lowerIsoWidth, upperIsoWidth + UpperLimitPadding);
                var sPicked = picker.GetSpectrum(0, true);
                var mzArr = sPicked.GetMZArray()?.Data ?? new List<double>();
                var intArr = sPicked.GetIntensityArray()?.Data ?? new List<double>();
                var (mz, inten) = FilterByMinIntensity(mzArr, intArr);
                (peakMzs, peakInt) = TrimByMin(mz, inten, MaxNumberPeaks);
            }

            mzs.Add(peakMzs);
            intensities.Add(peakInt);
        }
    }

    private static (double[] mz, double[] inten) FilterByMz(IList<double> mzArr, IList<double> intArr, double lo, double hi)
    {
        var mzOut = new List<double>(mzArr.Count);
        var intOut = new List<double>(intArr.Count);
        for (int i = 0; i < mzArr.Count; i++)
        {
            if (mzArr[i] < lo || mzArr[i] > hi) continue;
            mzOut.Add(mzArr[i]);
            intOut.Add(intArr[i]);
        }
        return (mzOut.ToArray(), intOut.ToArray());
    }

    private static (double[] mz, double[] inten) FilterByMinIntensity(IList<double> mzArr, IList<double> intArr)
    {
        // cpp removes peaks whose intensity is < 5% of max or equals the running min.
        if (intArr.Count == 0) return (System.Array.Empty<double>(), System.Array.Empty<double>());
        double max = double.MinValue, min = double.MaxValue;
        foreach (var v in intArr) { if (v > max) max = v; if (v < min) min = v; }
        var mzOut = new List<double>(intArr.Count);
        var intOut = new List<double>(intArr.Count);
        for (int i = 0; i < intArr.Count; i++)
        {
            if (intArr[i] < max * 0.05) continue;
            if (intArr[i] == min) continue;
            mzOut.Add(mzArr[i]);
            intOut.Add(intArr[i]);
        }
        return (mzOut.ToArray(), intOut.ToArray());
    }

    /// <summary>Trim peak list down to <paramref name="maxPeaks"/> by repeatedly removing the
    /// minimum-intensity peaks. Port of cpp's "remove peaks equal to min until size ≤ max".</summary>
    private static (double[] mz, double[] inten) TrimByMin(double[] mz, double[] inten, int maxPeaks)
    {
        if (mz.Length <= maxPeaks) return (mz, inten);
        var mzList = new List<double>(mz);
        var intList = new List<double>(inten);
        while (intList.Count > maxPeaks)
        {
            double minVal = intList[0];
            for (int i = 1; i < intList.Count; i++) if (intList[i] < minVal) minVal = intList[i];
            for (int i = intList.Count - 1; i >= 0; i--)
                if (intList[i] == minVal) { intList.RemoveAt(i); mzList.RemoveAt(i); }
        }
        return (mzList.ToArray(), intList.ToArray());
    }

    private void BuildMs1RetentionTimeIndex()
    {
        int nScans = Inner.Count;
        for (int i = 0; i < nScans; i++)
        {
            var s = Inner.GetSpectrum(i, DetailLevel.FullMetadata);
            if (s.ScanList.Scans.Count == 0)
                throw new InvalidOperationException("SpectrumList_chargeFromIsotope: no scanEvent present in raw data!");
            int scanConfig = s.ScanList.Scans[0].CvParam(CVID.MS_preset_scan_configuration).ValueAs<int>();
            if (scanConfig == 0)
            {
                if (s.Params.CvParam(CVID.MS_ms_level).ValueAs<int>() != 1) continue;
            }
            else if (scanConfig != 1) continue;

            double rTime = s.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time).TimeInSeconds();
            _ms1RetentionTimes.Add(new RtimeMap(rTime, i));
        }
        _ms1RetentionTimes.Sort((a, b) => a.Rtime.CompareTo(b.Rtime));
    }

    // ---- simulations ----

    private double[] SimulateSse(MsvcRand rng, int nCharges, int nIsotopePeakPossibilities)
    {
        var sse = new double[NSamples * nCharges * nIsotopePeakPossibilities];
        var spacings = new double[NSamples];
        for (int i = 0; i < nIsotopePeakPossibilities; i++)
        {
            int chainLength = i + MinIsotopePeaks;
            for (int w = 0; w < nCharges; w++)
            {
                int charge = _minCharge + w;
                for (int j = 0; j < NSamples; j++)
                {
                    double avg = 0;
                    var mzPoints = new double[chainLength];
                    for (int k = 1; k < chainLength; k++)
                    {
                        double theo = k * MassNeutron / charge;
                        double rnd = -MzTol + (double)rng.Next() / MsvcRand.RandMax * 2 * MzTol;
                        mzPoints[k] = theo + rnd;
                        avg += rnd;
                    }
                    avg /= chainLength;
                    double localSse = 0;
                    for (int k = 0; k < chainLength; k++)
                    {
                        double d = avg - (mzPoints[k] - k * MassNeutron / charge);
                        localSse += d * d;
                    }
                    spacings[j] = localSse;
                }
                System.Array.Sort(spacings);
                int start = i * nCharges * NSamples + w * NSamples;
                System.Array.Copy(spacings, 0, sse, start, NSamples);
            }
        }
        return sse;
    }

    private double[] SimulateKL(MsvcRand rng, double finalRetentionTime, int nIsotopePeakPossibilities, int nMs1Sims)
    {
        // Pick nMs1Sims MS1 indices evenly distributed across the run.
        var sampledIndices = new int[nMs1Sims];
        for (int i = 1; i <= nMs1Sims; i++)
        {
            double rTime = (double)i * finalRetentionTime / (nMs1Sims + 1);
            int upper = UpperBoundRtime(rTime);
            int near = upper == 0 ? 0 : upper - 1;
            sampledIndices[i - 1] = _ms1RetentionTimes[near].IndexMap;
        }

        // CWT-pick each of the sampled MS1 spectra to get its peaks.
        var picker = InstantiateBasicPeakPicker(sampledIndices);
        var allMz = new List<double[]>();
        var allInt = new List<double[]>();
        var mostIntense = new List<int[]>();
        for (int i = 0; i < sampledIndices.Length; i++)
        {
            var s = picker.GetSpectrum(i, true);
            var pInt = s.GetIntensityArray()?.Data;
            var pMz = s.GetMZArray()?.Data;
            if (pInt is null || pMz is null) { allMz.Add(System.Array.Empty<double>()); allInt.Add(System.Array.Empty<double>()); mostIntense.Add(System.Array.Empty<int>()); continue; }
            var intArr = pInt.ToArray();
            var mzArr = pMz.ToArray();
            allMz.Add(mzArr);
            allInt.Add(intArr);

            var sortedI = (double[])intArr.Clone();
            System.Array.Sort(sortedI);
            double cutoff = 0;
            if (sortedI.Length >= NumberIntensePeaks)
                cutoff = sortedI[sortedI.Length - NumberIntensePeaks];
            var hi = new List<int>(intArr.Length);
            for (int j = 0; j < intArr.Length; j++)
                if (intArr[j] >= cutoff) hi.Add(j);
            mostIntense.Add(hi.ToArray());
        }

        var simulatedKLs = new double[nIsotopePeakPossibilities * NSamples];
        for (int j = 0; j < nIsotopePeakPossibilities; j++)
        {
            int chainLength = j + MinIsotopePeaks;
            var klScores = new double[NSamples];

            for (int k = 0; k < NSamples; k++)
            {
                int randSpec = rng.Next() % nMs1Sims;
                var peakMz = allMz[randSpec];
                var peakInt = allInt[randSpec];
                int peakCnt = peakMz.Length;

                if (mostIntense[randSpec].Length == 0) { klScores[k] = 100; continue; }
                int randPeakIdx = rng.Next() % mostIntense[randSpec].Length;
                int randMzPoint = mostIntense[randSpec][randPeakIdx];
                double targetMz = peakMz[randMzPoint];

                int lower = LowerBound(peakMz, targetMz - _defaultIsolationWidth);
                if (lower >= peakMz.Length) lower = 0;
                int upper = LowerBound(peakMz, targetMz + _defaultIsolationWidth + UpperLimitPadding);
                if (upper >= peakMz.Length) upper = peakMz.Length - 1;

                if (upper <= lower) { klScores[k] = 100; continue; }
                var winMz = new List<double>(upper - lower);
                var winInt = new List<double>(upper - lower);
                for (int w = lower; w < upper; w++)
                {
                    if (peakInt[w] == 0.0) continue; // would give NaN in K-L
                    winMz.Add(peakMz[w]); winInt.Add(peakInt[w]);
                }
                if (winMz.Count == 0) { klScores[k] = 100; continue; }

                // Trim down to MaxNumberPeaks like the per-spectrum path.
                while (winInt.Count > MaxNumberPeaks)
                {
                    double maxI = double.MinValue, minI = double.MaxValue;
                    foreach (var v in winInt) { if (v > maxI) maxI = v; if (v < minI) minI = v; }
                    for (int w = winInt.Count - 1; w >= 0; w--)
                    {
                        if (winInt[w] < maxI * 0.05 || winInt[w] == minI)
                        { winInt.RemoveAt(w); winMz.RemoveAt(w); }
                    }
                }

                int windowSize = winInt.Count;
                if (windowSize == 0) { klScores[k] = 100; continue; }
                var chain = new IsotopeChain
                {
                    Charge = rng.Next() % (_maxCharge - _minCharge + 1) + _minCharge,
                };
                var picks = new int[chainLength];
                for (int w = 0; w < chainLength; w++) picks[w] = rng.Next() % windowSize;
                System.Array.Sort(picks);
                chain.IndexList = new List<int>(picks);

                klScores[k] = GetKLScore(chain, winMz.ToArray(), winInt.ToArray());
            }
            System.Array.Sort(klScores);
            System.Array.Copy(klScores, 0, simulatedKLs, j * NSamples, NSamples);
        }
        return simulatedKLs;
    }

    private static int[] SimulateTotIntensity(MsvcRand rng, int nIsotopePeakPossibilities)
    {
        int peakNumber = MaxNumberPeaks - MinNumberPeaks + 1;
        var simulated = new int[nIsotopePeakPossibilities * NSamples * peakNumber];

        for (int nPeaks = MinNumberPeaks; nPeaks <= MaxNumberPeaks; nPeaks++)
        {
            int maxRandomRank = nPeaks;
            for (int w = 0; w < nIsotopePeakPossibilities; w++)
            {
                int chainLength = w + MinIsotopePeaks;
                if (chainLength > nPeaks) break;

                int combinations = (nPeaks > 40 && chainLength > 2) ? NSamples : NChoosek(nPeaks, chainLength);
                int sampleSize = System.Math.Min(combinations, NSamples);
                var rankSums = new int[sampleSize];

                if (combinations >= NSamples)
                {
                    for (int j = 0; j < NSamples; j++)
                    {
                        int sum = 0;
                        for (int k = 0; k < chainLength; k++) sum += rng.Next() % maxRandomRank + 1;
                        rankSums[j] = sum;
                    }
                }
                else
                {
                    // Enumerate all combinations via next_permutation on a bool vector
                    // [false × chainLength, true × (nPeaks-chainLength)] — matches cpp's
                    // `fill(begin+chainLength, end, true); do {...} while (next_permutation(...))`.
                    var v = new bool[nPeaks];
                    for (int i = chainLength; i < nPeaks; i++) v[i] = true;
                    int cnt = 0;
                    do
                    {
                        int sum = 0;
                        for (int j = 0; j < nPeaks; j++) if (!v[j]) sum += j + 1;
                        rankSums[cnt++] = sum;
                    } while (NextPermutation(v));
                }
                System.Array.Sort(rankSums);
                int start = (nPeaks - MinNumberPeaks) * nIsotopePeakPossibilities * NSamples + w * NSamples;
                System.Array.Copy(rankSums, 0, simulated, start, sampleSize);
            }
        }
        return simulated;
    }

    // ---- peak picker construction ----

    private SpectrumList_PeakPicker InstantiateBasicPeakPicker(int[] indices)
    {
        var simple = new SpectrumListSimple();
        foreach (var idx in indices) simple.Spectra.Add(Inner.GetSpectrum(idx, true));
        return new SpectrumList_PeakPicker(simple, new CwtPeakDetector(0.0, 0, 0.05),
            preferVendorPeakPicking: false, new IntegerSet(1));
    }

    private CachedParent LoadParent(int parentIdx)
    {
        var s = Inner.GetSpectrum(parentIdx, true);
        var mz = s.GetMZArray()?.Data;
        var inten = s.GetIntensityArray()?.Data;
        return new CachedParent(
            mz?.ToArray() ?? System.Array.Empty<double>(),
            inten?.ToArray() ?? System.Array.Empty<double>(),
            s.Params.HasCVParam(CVID.MS_centroid_spectrum));
    }

    private static SpectrumList_PeakPicker InstantiatePeakPickerForWindowFromCached(
        CachedParent cached, double targetIsoMz, double lowerIsoWidth, double upperIsoWidth)
    {
        // Build a one-spectrum SpectrumListSimple with the windowed parent data so the CWT
        // detector only fires on the isolation neighborhood. Cpp also "re-samples via linear
        // interpolation" via summedIntensity/summedMZ — those names are misleading; cpp
        // doesn't actually resample, it just assigns the windowed slice. We do the same.
        int lo = LowerBound(cached.Mz, targetIsoMz - lowerIsoWidth);
        if (lo >= cached.Mz.Length) lo = 0;
        int hi = LowerBound(cached.Mz, targetIsoMz + upperIsoWidth);
        if (hi >= cached.Mz.Length) hi = cached.Mz.Length - 1;

        var spec = new Spectrum { Index = 0 };
        spec.Params.Set(CVID.MS_ms_level, 1);
        spec.Params.Set(CVID.MS_profile_spectrum);
        if (hi > lo)
        {
            var mzArr = new BinaryDataArray();
            mzArr.Params.Set(CVID.MS_m_z_array, "", CVID.MS_m_z);
            var intArr = new BinaryDataArray();
            intArr.Params.Set(CVID.MS_intensity_array, "", CVID.MS_number_of_detector_counts);
            for (int i = lo; i < hi; i++) { mzArr.Data.Add(cached.Mz[i]); intArr.Data.Add(cached.Intensity[i]); }
            spec.BinaryDataArrays.Add(mzArr);
            spec.BinaryDataArrays.Add(intArr);
            spec.DefaultArrayLength = mzArr.Data.Count;
        }

        var simple = new SpectrumListSimple();
        simple.Spectra.Add(spec);
        return new SpectrumList_PeakPicker(simple, new CwtPeakDetector(0.0, MaxNumberPeaks, 0.05),
            preferVendorPeakPicking: false, new IntegerSet(1));
    }

    // ---- math helpers ----

    private static double GetKLScore(IsotopeChain chain, double[] mzs, double[] intensities)
    {
        if (mzs.Length != intensities.Length)
            throw new InvalidOperationException("[SpectrumList_chargeFromIsotope, getKLscore] m/z and intensity arrays must be equal in size.");

        double mStar = Lambda * mzs[chain.IndexList[0]] * chain.Charge;
        double mExp = System.Math.Exp(-mStar);

        double poissonSum = 0;
        double observedSum = 0;
        var poissonVals = new double[chain.IndexList.Count];
        for (int k = 0; k < chain.IndexList.Count; k++)
        {
            double poisson = mExp * System.Math.Pow(mStar, k);
            for (int w = k; w > 1; w--) poisson /= w;
            poissonVals[k] = poisson;
            poissonSum += poisson;
            observedSum += intensities[chain.IndexList[k]];
        }

        double kl = 0;
        for (int k = 0; k < chain.IndexList.Count; k++)
        {
            poissonVals[k] /= poissonSum;
            double normObs = intensities[chain.IndexList[k]] / observedSum;
            kl += normObs * System.Math.Log10(normObs / poissonVals[k]);
        }
        return kl;
    }

    private static int NChoosek(int n, int k)
    {
        long num = 1, den = 1;
        for (int i = 0; i < k; i++) { num *= n - i; den *= i + 1; }
        return (int)(num / den);
    }

    private static int UpperBound(double[] arr, int lo, int hi, double target)
    {
        // Returns first index in [lo, hi) where arr[i] > target.
        int origLo = lo;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (arr[mid] <= target) lo = mid + 1;
            else hi = mid;
        }
        return lo - origLo;
    }

    private static int UpperBound(int[] arr, int lo, int hi, int target)
    {
        int origLo = lo;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (arr[mid] <= target) lo = mid + 1;
            else hi = mid;
        }
        return lo - origLo;
    }

    private static int LowerBound(IList<double> arr, double target)
    {
        int lo = 0, hi = arr.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (arr[mid] < target) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private static int LowerBound(double[] arr, double target)
    {
        int lo = 0, hi = arr.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (arr[mid] < target) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>In-place lex next permutation. Mirrors C++ <c>std::next_permutation</c>.</summary>
    private static bool NextPermutation(bool[] arr)
    {
        // Compare false < true (cpp's bool ordering).
        int i = arr.Length - 1;
        while (i > 0 && !(BoolLt(arr[i - 1], arr[i]))) i--;
        if (i <= 0)
        {
            System.Array.Reverse(arr);
            return false;
        }
        int j = arr.Length - 1;
        while (!BoolLt(arr[i - 1], arr[j])) j--;
        (arr[i - 1], arr[j]) = (arr[j], arr[i - 1]);
        System.Array.Reverse(arr, i, arr.Length - i);
        return true;
        static bool BoolLt(bool a, bool b) => !a && b;
    }

    // ---- inner types ----

    private sealed class IsotopeChain
    {
        public int Charge;
        public List<int> IndexList = new();
        public int ParentIndex;
    }

    private sealed class ScoreChain
    {
        public double MzPvalue;
        public double IntensityPvalue;
        public double IntensitySumPvalue;
        public double OverallPvalue;
        public int MzRank;
        public int IntensityRank;
        public int IntensitySumRank;
        public int SumRanks;
        public int ChainIndex;
    }

    private readonly struct RtimeMap
    {
        public RtimeMap(double rt, int idx) { Rtime = rt; IndexMap = idx; }
        public double Rtime { get; }
        public int IndexMap { get; }
    }

    /// <summary>Bit-exact port of MSVC's CRT <c>rand()</c> seeded by <c>srand(1234)</c>. The
    /// turbocharger algorithm pre-simulates three large distributions whose values affect the
    /// final charge assignment; using .NET's <see cref="Random"/> would give a different
    /// sequence and silently diverge from cpp's choices on borderline spectra.</summary>
    internal sealed class MsvcRand
    {
        public const int RandMax = 0x7FFF;
        private uint _state;
        public MsvcRand(uint seed) { _state = seed; }
        public int Next()
        {
            _state = _state * 214013u + 2531011u;
            return (int)((_state >> 16) & 0x7FFF);
        }
    }

    /// <summary>Snapshot of a parent MS1's binary data needed to build isotope chains:
    /// sorted m/z + intensity arrays and whether the source was already centroided.</summary>
    private readonly struct CachedParent
    {
        public CachedParent(double[] mz, double[] intensity, bool isCentroid)
        { Mz = mz; Intensity = intensity; IsCentroid = isCentroid; }
        public double[] Mz { get; }
        public double[] Intensity { get; }
        public bool IsCentroid { get; }
    }

    /// <summary>Small LRU keyed on spectrum index. Built specifically for the turbocharger
    /// access pattern: every MSn in a DDA Top-N batch reads the same <c>parentsBefore</c> +
    /// <c>parentsAfter</c> MS1 parents; without caching each MSn would re-decode binary data
    /// from disk N times in a row.</summary>
    private sealed class ParentSpectrumCache
    {
        private readonly int _capacity;
        private readonly LinkedList<(int Key, CachedParent Value)> _list = new();
        private readonly Dictionary<int, LinkedListNode<(int Key, CachedParent Value)>> _map = new();
        private readonly object _lock = new();

        public ParentSpectrumCache(int capacity) { _capacity = capacity; }

        public CachedParent GetOrLoad(int key, Func<int, CachedParent> loader)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var hit))
                {
                    _list.Remove(hit);
                    _list.AddFirst(hit);
                    return hit.Value.Value;
                }
            }
            // Load outside the lock so concurrent loads of different keys don't serialize.
            var loaded = loader(key);
            lock (_lock)
            {
                // Re-check in case another thread just loaded the same key.
                if (_map.TryGetValue(key, out var existing)) return existing.Value.Value;
                var node = _list.AddFirst((key, loaded));
                _map[key] = node;
                if (_list.Count > _capacity)
                {
                    var last = _list.Last!;
                    _list.RemoveLast();
                    _map.Remove(last.Value.Key);
                }
                return loaded;
            }
        }
    }
}
