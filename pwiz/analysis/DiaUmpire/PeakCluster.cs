// Public fields on PeakOverlapRegion / PrecursorFragmentPairEdge match cpp parity (the algorithm
// assigns by-name dozens of times). CA1822: AddScore is instance-shaped in cpp; preserve here.
#pragma warning disable CA1051 // Do not declare visible instance fields
#pragma warning disable CA1822 // Member does not access instance data and can be marked as static

namespace Pwiz.Analysis.DiaUmpire;

/// <summary>
/// Overlap edge between two peak curves. Base class of
/// <see cref="PrecursorFragmentPairEdge"/>. Port of cpp <c>DiaUmpire::PeakOverlapRegion</c>.
/// </summary>
public class PeakOverlapRegion
{
    /// <summary>Index of the precursor-side peak curve.</summary>
    public int PeakCurveIndexA;
    /// <summary>Index of the fragment-side peak curve.</summary>
    public int PeakCurveIndexB;
    /// <summary>Correlation between the two curves.</summary>
    public float Correlation;
}

/// <summary>
/// Edge connecting an MS1 precursor cluster to an MS2 fragment curve. Port of cpp
/// <c>DiaUmpire::PrecursorFragmentPairEdge</c>. Mutated repeatedly by the
/// scoring / boosting stages, hence the public mutable fields.
/// </summary>
public class PrecursorFragmentPairEdge : PeakOverlapRegion
{
    /// <summary>m/z of the fragment peak.</summary>
    public float FragmentMz;
    /// <summary>Intensity of the fragment peak.</summary>
    public float Intensity;
    /// <summary>Delta between the precursor and fragment apex RTs.</summary>
    public float DeltaApex;
    /// <summary>RT-overlap fraction with the precursor curve.</summary>
    public float RTOverlapP;
    /// <summary>Rank of this fragment relative to other co-eluting fragments (1-based).</summary>
    public int FragmentMS1Rank;
    /// <summary>Score derived from <see cref="FragmentMS1Rank"/>.</summary>
    public float FragmentMS1RankScore = 1f;
    /// <summary>Intensity after quality-score-based adjustment.</summary>
    public float AdjustedFragInt;
    /// <summary>True if the fragment is a complementary-ion partner.</summary>
    public bool ComplementaryFragment;
    /// <summary>m/z of the matched theoretical fragment.</summary>
    public float MatchedFragMz;
}

/// <summary>
/// Peak isotope cluster. Holds the mono-isotopic peak plus 1..N isotopologues, with
/// per-isotope SNR / area / height / RT. Port of cpp <c>DiaUmpire::PeakCluster</c>.
/// Original algorithm by Chih-Chiang Tsou.
/// </summary>
public class PeakCluster
{
    /// <summary>Proton mass constant used by isotope-mass math (shared with the clustering helpers).</summary>
    public const float ProtonMassExternal = 1.00727646677f;

    private readonly float[] _snr;
    private readonly SortedSet<float> _matchScores = new();
    private float _conflictCorr = -1;
    private float _mass;
    private readonly ChiSquareGOF _chiSquaredGof;

    /// <summary>Index for diagnostics and cross-referencing.</summary>
    public int Index { get; set; }
    /// <summary>One curve per isotope index (0 = mono).</summary>
    public PeakCurve?[] IsoPeaksCurves { get; }
    /// <summary>The mono-isotopic curve (typically = IsoPeaksCurves[0]).</summary>
    public PeakCurve? MonoIsotopePeak { get; set; }
    /// <summary>Per-isotope curve index (mirrors <see cref="IsoPeaksCurves"/>).</summary>
    public int[] IsoPeakIndex { get; }
    /// <summary>Inter-isotope correlations.</summary>
    public float[] Corrs { get; }
    /// <summary>Per-isotope peak height.</summary>
    public float[] PeakHeight { get; }
    /// <summary>RT of the per-isotope peak heights.</summary>
    public float[] PeakHeightRT { get; }
    /// <summary>Per-isotope peak area (sum within startRT..endRT).</summary>
    public float[] PeakArea { get; }
    /// <summary>Per-isotope target m/z.</summary>
    public float[] Mz { get; }
    /// <summary>Start RT of the cluster (recomputed during area calc).</summary>
    public float StartRT { get; set; } = float.MaxValue;
    /// <summary>End RT of the cluster (recomputed during area calc).</summary>
    public float EndRT { get; set; } = float.MinValue;
    /// <summary>First raw scan number contributing to this cluster.</summary>
    public int StartScan { get; set; }
    /// <summary>Last raw scan number contributing to this cluster.</summary>
    public int EndScan { get; set; }
    /// <summary>Charge state.</summary>
    public int Charge { get; set; }
    /// <summary>Chi-squared probability from isotope-map lookup (lazy).</summary>
    public float IsoMapProb { get; set; } = -1;
    /// <summary>Inter-isotope intensity ratios (relative to mono).</summary>
    public float[] PeakDis { get; set; } = System.Array.Empty<float>();
    /// <summary>Number of ridge points contained inside the cluster RT envelope.</summary>
    public int NoRidges { get; set; }
    /// <summary>Mono-iso RT overlap fraction with the +1 isotope.</summary>
    public float OverlapP { get; set; }
    /// <summary>Per-isotope RT-overlap fraction with the mono.</summary>
    public float[] OverlapRT { get; }
    /// <summary>Intensity at the start RT of the mono-iso curve.</summary>
    public float LeftInt { get; set; }
    /// <summary>Intensity at the end RT of the mono-iso curve.</summary>
    public float RightInt { get; set; }
    /// <summary>True once a peptide identification has been assigned.</summary>
    public bool Identified { get; set; }
    /// <summary>Assigned peptide-ion string (after ID assignment).</summary>
    public string AssignedPepIon { get; set; } = string.Empty;
    /// <summary>Fragment pair edges for downstream MS2 pairing.</summary>
    public List<PrecursorFragmentPairEdge> GroupedFragmentPeaks { get; } = new();
    /// <summary>MS1 quality score.</summary>
    public float MS1Score { get; set; }
    /// <summary>Local probability from MS1 score.</summary>
    public float MS1ScoreLocalProb { get; set; }
    /// <summary>Calibrated probability from MS1 score.</summary>
    public float MS1ScoreProbability { get; set; }
    /// <summary>Spectrum-key for the emitted pseudo-MS/MS.</summary>
    public string SpectrumKey { get; set; } = string.Empty;
    /// <summary>RT variance (set externally).</summary>
    public float RTVar { get; set; }

    /// <summary>Builds an empty cluster with capacity for <paramref name="isotopicNum"/> isotopes.</summary>
    public PeakCluster(int isotopicNum, int charge, ChiSquareGOF chiSquaredGof)
    {
        _chiSquaredGof = chiSquaredGof;
        IsoPeaksCurves = new PeakCurve?[isotopicNum];
        Corrs = new float[isotopicNum - 1];
        _snr = new float[isotopicNum];
        OverlapRT = new float[isotopicNum - 1];
        PeakHeight = new float[isotopicNum];
        PeakHeightRT = new float[isotopicNum];
        PeakArea = new float[isotopicNum];
        IsoPeakIndex = new int[isotopicNum];
        Mz = new float[isotopicNum];
        for (int i = 0; i < isotopicNum; i++) _snr[i] = -1;
        Charge = charge;
    }

    /// <summary>Records a match score (used by the ranking pass).</summary>
    public void AddScore(float score) => _matchScores.Add(score);

    /// <summary>Returns the rank (1 = best) of <paramref name="score"/>, or -1 if no scores recorded.</summary>
    public int GetScoreRank(float score)
    {
        if (_matchScores.Count == 0) return -1;
        // cpp: size - distance(begin, upper_bound(score)) + 1
        int aboveCount = 0;
        foreach (float s in _matchScores)
            if (s > score) aboveCount++;
        return _matchScores.Count - (_matchScores.Count - aboveCount) + 1;
    }

    /// <summary>Quality bucket (1 if +2 isotope present, else 2).</summary>
    public int GetQualityCategory()
    {
        if ((IsoPeaksCurves.Length == 0 || IsoPeaksCurves[2] == null) && Mz[2] == 0f) return 2;
        return 1;
    }

    /// <summary>Sets an explicit m/z for the given isotope index.</summary>
    public void SetMz(int pkidx, float value) => Mz[pkidx] = value;

    /// <summary>Conflict-correlation accumulator (lazy from <see cref="IsoPeaksCurves"/>[0]).</summary>
    public float GetConflictCorr()
    {
        if (_conflictCorr == -1 && IsoPeaksCurves[0] is not null)
            _conflictCorr = IsoPeaksCurves[0]!.ConflictCorr;
        return _conflictCorr;
    }

    /// <summary>Sets the conflict-correlation value explicitly.</summary>
    public void SetConflictCorr(float conflictCorr) => _conflictCorr = conflictCorr;

    /// <summary>Mono-isotopic target m/z (lazy from the mono curve).</summary>
    public float TargetMz()
    {
        if (Mz[0] == 0 && IsoPeaksCurves[0] is not null) Mz[0] = IsoPeaksCurves[0]!.TargetMz;
        return Mz[0];
    }

    /// <summary>Sets the SNR for a given isotope index.</summary>
    public void SetSNR(int pkidx, float snr) => _snr[pkidx] = snr;

    /// <summary>SNR for a given isotope index (lazy from the underlying curve).</summary>
    public float GetSNR(int pkidx)
    {
        if (_snr[pkidx] == -1)
        {
            if (IsoPeaksCurves.Length > 0 && IsoPeaksCurves[pkidx] is not null)
                _snr[pkidx] = IsoPeaksCurves[pkidx]!.GetRawSNR();
        }
        return _snr[pkidx];
    }

    /// <summary>Neutral mass (charge × (m/z − proton)).</summary>
    public float NeutralMass()
    {
        if (_mass == 0)
        {
            if (MonoIsotopePeak is not null)
                _mass = Charge * (MonoIsotopePeak.TargetMz - ProtonMassExternal);
            else
                _mass = Charge * (Mz[0] - ProtonMassExternal);
        }
        return _mass;
    }

    /// <summary>Lazy chi^2 probability against the isotope-pattern table.</summary>
    public void UpdateIsoMapProb(IsotopePatternMap isotopePatternMap)
    {
        if (IsoMapProb == -1) IsoMapProb = GetChiSquareProbByIsoMap(isotopePatternMap);
    }

    /// <summary>Propagates strong-correlation conflict scores back to overlapping curves.</summary>
    public void AssignConflictCorr()
    {
        for (int i = 1; i < IsoPeaksCurves.Length; i++)
            if (IsoPeaksCurves[i] is not null && Corrs[i - 1] > 0.6f)
                IsoPeaksCurves[i]!.AddConflictScore(Corrs[i - 1]);
    }

    /// <summary>Recomputes per-isotope area/height inside the union of mono and +1 RT ranges.</summary>
    public void CalcPeakAreaV2()
    {
        if (MonoIsotopePeak is null) return;
        int noOfIsotopic = IsoPeaksCurves.Length;
        StartRT = MonoIsotopePeak.StartRT();
        EndRT = MonoIsotopePeak.EndRT();

        if (IsoPeaksCurves.Length > 1 && IsoPeaksCurves[1] is not null)
        {
            StartRT = System.Math.Min(MonoIsotopePeak.StartRT(), IsoPeaksCurves[1]!.StartRT());
            EndRT = System.Math.Max(MonoIsotopePeak.EndRT(), IsoPeaksCurves[1]!.EndRT());
        }

        if (EndRT == StartRT)
        {
            StartRT = MonoIsotopePeak.GetSmoothedList().Data[0].GetX();
            EndRT = MonoIsotopePeak.GetSmoothedList().Data[^1].GetX();
        }

        NoRidges = 0;
        if (MonoIsotopePeak.RegionRidge.Count > 0)
            foreach (float ridge in MonoIsotopePeak.RegionRidge)
                if (ridge >= StartRT && ridge <= EndRT) NoRidges++;

        for (int i = 0; i < noOfIsotopic; i++)
        {
            var peak = IsoPeaksCurves[i];
            if (peak is null) break;
            foreach (var pt in peak.GetSmoothedList().Data)
            {
                if (pt.GetX() >= StartRT && pt.GetX() <= EndRT)
                {
                    PeakArea[i] += pt.GetY();
                    if (pt.GetY() > PeakHeight[i])
                    {
                        PeakHeight[i] = pt.GetY();
                        PeakHeightRT[i] = pt.GetX();
                    }
                }
            }
            Mz[i] = peak.TargetMz;
            IsoPeakIndex[i] = peak.Index;
        }
    }

    /// <summary>Builds the per-isotope intensity-ratio vector (relative to mono).</summary>
    public void GeneratePeakDis()
    {
        if (PeakDis.Length > 0) return;
        PeakDis = new float[PeakHeight.Length];
        float firstPeak = PeakHeight[0];
        for (int i = 0; i < PeakDis.Length; i++)
            if (PeakHeight[i] > 0) PeakDis[i] = PeakHeight[i] / firstPeak;
    }

    /// <summary>Returns the per-isotope MzRange envelope (one row per isotope index).</summary>
    public List<XYData> GetPatternRange(IsotopePatternMap isotopePatternMap)
    {
        var patternRange = new List<XYData>(isotopePatternMap.IsotopeCount);
        for (int i = 0; i < isotopePatternMap.IsotopeCount; i++)
        {
            MzRange range = LookupRange(isotopePatternMap, i, NeutralMass());
            patternRange.Add(new XYData(range.Begin, range.End));
        }
        return patternRange;
    }

    /// <summary>Chi-squared probability of the observed isotope pattern given the lookup table.</summary>
    public float GetChiSquareProbByIsoMap(IsotopePatternMap isotopePatternMap)
    {
        GeneratePeakDis();
        var patternRange = new MzRange[isotopePatternMap.IsotopeCount];
        for (int i = 0; i < isotopePatternMap.IsotopeCount; i++)
            patternRange[i] = LookupRange(isotopePatternMap, i, NeutralMass());

        var theoIso = new float[isotopePatternMap.IsotopeCount];
        theoIso[0] = 1;

        for (int i = 1; i < isotopePatternMap.IsotopeCount; i++)
        {
            // Note: cpp swaps Y/X semantics here vs. MzRange.Begin/End (XYData.Y = MzRange.Begin = lower).
            float lo = patternRange[i - 1].End;
            float hi = patternRange[i - 1].Begin;
            if (PeakDis[i] >= lo && PeakDis[i] <= hi)
                theoIso[i] = PeakDis[i];
            else if (System.Math.Abs(PeakDis[1] - lo) > System.Math.Abs(PeakDis[i] - hi))
                theoIso[i] = hi;
            else
                theoIso[i] = lo;
        }
        return _chiSquaredGof.GetGoodNessOfFitProb(theoIso, PeakDis);
    }

    /// <summary>True once enough isotopologues are present for the cluster to pass.</summary>
    public bool IsotopeComplete(int minIsoNum)
    {
        for (int i = 0; i < minIsoNum; i++)
            if ((IsoPeaksCurves.Length == 0 || IsoPeaksCurves[i] == null) && Mz[i] == 0f)
                return false;
        return true;
    }

    /// <summary>Returns the largest non-zero Mz over the isotope vector (falls back to mono).</summary>
    public float GetMaxMz()
    {
        for (int i = Mz.Length - 1; i > 0; i--)
            if (Mz[i] > 0f) return Mz[i];
        return Mz[0];
    }

    private static MzRange LookupRange(IsotopePatternMap map, int isotopeIndex, double mass)
    {
        // upper_bound: first key > mass; if none, use the last entry.
        var byIso = map[isotopeIndex];
        MzRange? best = null;
        foreach (var kv in byIso)
        {
            if (kv.Key > mass) { best = kv.Value; break; }
        }
        if (best is null && byIso.Count > 0)
            best = byIso.Reverse().First().Value;
        return best ?? new MzRange(0, 0);
    }
}

/// <summary>
/// MS1 isotope-cluster builder. Walks the search-window of <see cref="PeakCurve"/>s
/// around a target curve and assembles <see cref="PeakCluster"/>s for each candidate
/// charge state. Port of cpp <c>DiaUmpire::PeakCurveClusteringCorrKDtree</c>.
/// </summary>
/// <remarks>
/// Cpp uses a boost geometry R-tree to range-query (apexRT × targetMz). C# port uses
/// a simple linear scan over a pre-sorted list — fine while we're testing individual
/// units; phase 5 will revisit if profiling shows it as a hotspot.
/// </remarks>
public class PeakCurveClusteringCorrKDtree
{
    private readonly IReadOnlyList<PeakCurve> _peakCurves;
    private readonly int _targetCurveIndex;
    private readonly IReadOnlyList<PeakCurve> _searchablePeakCurves;
    private readonly IsotopePatternMap _isotopePatternMap;
    private readonly ChiSquareGOF _chiSquaredGof;
    private readonly int _maxNoOfClusters;
    private readonly int _minNoOfClusters;
    private readonly int _startCharge;
    private readonly int _endCharge;
    private readonly object _mx;
    private readonly InstrumentParameter _parameter;

    /// <summary>Successfully-built clusters (one per accepted charge state).</summary>
    public List<PeakCluster> ResultClusters { get; } = new();

    /// <summary>
    /// Constructs the builder. <paramref name="searchablePeakCurves"/> is a flat list
    /// of all curves to consider for isotope partners — phase 5 will substitute an
    /// R-tree if/when needed.
    /// </summary>
    public PeakCurveClusteringCorrKDtree(
        IReadOnlyList<PeakCurve> peakCurves,
        int targetCurveIndex,
        IReadOnlyList<PeakCurve> searchablePeakCurves,
        InstrumentParameter parameter,
        IsotopePatternMap isotopePatternMap,
        ChiSquareGOF chiSquaredGof,
        int startCharge, int endCharge,
        int maxNoClusters, int minNoClusters,
        object lockObject)
    {
        _peakCurves = peakCurves;
        _targetCurveIndex = targetCurveIndex;
        _searchablePeakCurves = searchablePeakCurves;
        _parameter = parameter;
        _isotopePatternMap = isotopePatternMap;
        _chiSquaredGof = chiSquaredGof;
        _maxNoOfClusters = maxNoClusters;
        _minNoOfClusters = minNoClusters;
        _startCharge = startCharge;
        _endCharge = endCharge;
        _mx = lockObject;
    }

    /// <summary>Runs the clustering and accumulates results in <see cref="ResultClusters"/>.</summary>
    public void Run()
    {
        var peakA = _peakCurves[_targetCurveIndex];
        float lowrt = peakA.ApexRT - _parameter.DeltaApex - 1e-4f;
        float highrt = peakA.ApexRT + _parameter.DeltaApex + 1e-4f;
        float lowmz = InstrumentParameter.GetMzByPPM(peakA.TargetMz - 1e-4f, 1, _parameter.MS1PPM);
        float highmz = InstrumentParameter.GetMzByPPM(peakA.TargetMz + 1e-4f + ((float)_maxNoOfClusters / _startCharge), 1, -_parameter.MS1PPM);

        // Candidate isotope partners lie in a narrow m/z window above the target and a narrow RT
        // window around its apex. _searchablePeakCurves is pre-sorted by TargetMz (shared across all
        // jobs), so binary-search the [lowmz, highmz] slice and scan only that instead of every curve
        // -- the flat O(N) scan per target curve made the whole step O(N^2) (cpp uses an R-tree). The
        // slice is already m/z-sorted, so the result matches the old collect-then-sort exactly.
        var peakCurveListMZ = new List<PeakCurve>();
        for (int mzScan = LowerBoundByTargetMz(_searchablePeakCurves, lowmz); mzScan < _searchablePeakCurves.Count; mzScan++)
        {
            var pc = _searchablePeakCurves[mzScan];
            if (pc.TargetMz > highmz) break;
            if (pc.ApexRT < lowrt || pc.ApexRT > highrt) continue;
            peakCurveListMZ.Add(pc);
        }

        float aRange = peakA.EndRT() - peakA.StartRT();

        for (int charge = _endCharge; charge >= _startCharge; charge--)
        {
            float mass = charge * (peakA.TargetMz - PeakCluster.ProtonMassExternal);
            if (mass < _parameter.MinPrecursorMass || mass > _parameter.MaxPrecursorMass
                || (_parameter.MassDefectFilter && !default(MassDefect).InMassDefectRange(mass, _parameter.MassDefectOffset)))
                continue;

            var peakCluster = new PeakCluster(_maxNoOfClusters, charge, _chiSquaredGof);
            peakCluster.IsoPeaksCurves[0] = peakA;
            peakCluster.MonoIsotopePeak = peakA;

            var ranges = new MzRange[_maxNoOfClusters - 1];
            for (int i = 0; i < _maxNoOfClusters - 1; i++)
            {
                if (_isotopePatternMap[i].Count == 0)
                    throw new System.InvalidOperationException("empty isotopePatternMap");
                ranges[i] = LookupRange(_isotopePatternMap, i, peakCluster.NeutralMass());
            }

            for (int pkidx = 1; pkidx < _maxNoOfClusters; pkidx++)
            {
                float ppmThreshold = _parameter.MS1PPM + (_parameter.MS1PPM * pkidx * 0.5f);
                float lowTheoMz = InstrumentParameter.GetMzByPPM(
                    peakA.TargetMz + pkidx * (PeakCluster.ProtonMassExternal / charge), charge, ppmThreshold);
                float upTheoMz = InstrumentParameter.GetMzByPPM(
                    peakA.TargetMz + pkidx * (PeakCluster.ProtonMassExternal / charge), charge, -ppmThreshold);

                float theoMz = peakA.TargetMz + pkidx * (PeakCluster.ProtonMassExternal / charge);
                float maxscore = 0;
                float maxcorr = 0;
                float maxoverlap = 0;
                PeakCurve? closestPeak = null;

                int startMzIdx = LowerBoundByTargetMz(peakCurveListMZ, lowTheoMz);

                for (int mzidx = startMzIdx; mzidx < peakCurveListMZ.Count; mzidx++)
                {
                    var peakB = peakCurveListMZ[mzidx];
                    if (peakB.TargetMz <= peakA.TargetMz) continue;
                    if (peakB.TargetMz > upTheoMz) break;

                    float bRange = peakB.EndRT() - peakB.StartRT();
                    float overlapP = 0;
                    if (peakA.StartRT() >= peakB.StartRT() && peakA.StartRT() <= peakB.EndRT() && peakA.EndRT() >= peakB.EndRT())
                        overlapP = (peakB.EndRT() - peakA.StartRT()) / bRange;
                    else if (peakA.EndRT() >= peakB.StartRT() && peakA.EndRT() <= peakB.EndRT() && peakA.StartRT() <= peakB.StartRT())
                        overlapP = (peakA.EndRT() - peakB.StartRT()) / bRange;
                    else if (peakA.StartRT() <= peakB.StartRT() && peakA.EndRT() >= peakB.EndRT())
                        overlapP = 1;
                    else if (peakA.StartRT() >= peakB.StartRT() && peakA.EndRT() <= peakB.EndRT())
                        overlapP = aRange / bRange;

                    if (_parameter.TargetIDOnly
                        || (overlapP > _parameter.MiniOverlapP
                            && (!_parameter.CheckMonoIsotopicApex
                                || (peakA.ApexRT >= peakB.StartRT() && peakA.ApexRT <= peakB.EndRT()
                                    && peakB.ApexRT >= peakA.StartRT() && peakB.ApexRT <= peakA.EndRT()))))
                    {
                        float ppm = InstrumentParameter.CalcPPM(theoMz, peakB.TargetMz);
                        if (ppm < ppmThreshold)
                        {
                            float corr = PeakCurveCorrCalc.CalPeakCorr(peakA, peakB, _parameter.NoPeakPerMin);
                            if (float.IsNaN(corr)) corr = 0;

                            float peakIntA = peakA.ApexInt;
                            float peakIntB = peakB.GetMaxIntensityByRegionRange(
                                System.Math.Max(peakA.StartRT(), peakB.StartRT()),
                                System.Math.Min(peakB.EndRT(), peakA.EndRT()));

                            if ((_parameter.TargetIDOnly && corr > 0.2f) || corr > _parameter.IsoCorrThreshold)
                            {
                                float intscore;
                                float intRatio = peakIntB / peakIntA;

                                // cpp swap of Y/X same as in PeakCluster.GetChiSquareProbByIsoMap.
                                float lo = ranges[pkidx - 1].End;
                                float hi = ranges[pkidx - 1].Begin;
                                if (intRatio > lo && intRatio <= hi) intscore = 1;
                                else if (System.Math.Abs(intRatio - lo) > System.Math.Abs(intRatio - hi))
                                    intscore = 1 - System.Math.Abs(intRatio - hi);
                                else
                                    intscore = 1 - System.Math.Abs(intRatio - lo);
                                if (intscore < 0) intscore = 0;

                                float score = ((ppmThreshold - ppm) / ppmThreshold) + corr + intscore;
                                if (maxscore < score)
                                {
                                    maxscore = score;
                                    closestPeak = peakB;
                                    maxcorr = corr;
                                    maxoverlap = overlapP;
                                }
                            }
                        }
                    }
                }

                if (closestPeak is not null)
                {
                    peakCluster.Corrs[pkidx - 1] = maxcorr;
                    peakCluster.IsoPeaksCurves[pkidx] = closestPeak;
                    peakCluster.OverlapRT[pkidx - 1] = maxoverlap;
                    _ = peakCluster.GetSNR(pkidx - 1);
                    if (pkidx == 1) peakCluster.OverlapP = maxoverlap;
                }
                else break;
            }

            if (peakCluster.IsotopeComplete(_minNoOfClusters))
            {
                peakCluster.CalcPeakAreaV2();
                peakCluster.UpdateIsoMapProb(_isotopePatternMap);
                lock (_mx) { peakCluster.AssignConflictCorr(); }
                peakCluster.LeftInt = peakA.GetSmoothedList().Data[0].GetY();
                peakCluster.RightInt = peakA.GetSmoothedList().Data[peakA.GetSmoothedList().PointCount() - 1].GetY();

                if (_parameter.TargetIDOnly || peakCluster.IsoMapProb > _parameter.IsoPattern)
                {
                    ResultClusters.Add(peakCluster);
                    if (!_parameter.TargetIDOnly
                        || (_parameter.RemoveGroupedPeaks
                            && peakCluster.Corrs[0] > _parameter.RemoveGroupedPeaksCorr
                            && peakCluster.OverlapP > _parameter.RemoveGroupedPeaksRTOverlap))
                    {
                        for (int i = 1; i < peakCluster.IsoPeaksCurves.Length; i++)
                        {
                            var peak = peakCluster.IsoPeaksCurves[i];
                            if (peak is not null
                                && peakCluster.Corrs[i - 1] > _parameter.RemoveGroupedPeaksCorr
                                && peakCluster.OverlapRT[i - 1] > _parameter.RemoveGroupedPeaksRTOverlap)
                            {
                                lock (_mx) { peak.ChargeGrouped.Add(charge); }
                            }
                        }
                    }
                }
            }
        }
    }

    private static int LowerBoundByTargetMz(IReadOnlyList<PeakCurve> sorted, float value)
    {
        int lo = 0, hi = sorted.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (sorted[mid].TargetMz < value) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private static MzRange LookupRange(IsotopePatternMap map, int isotopeIndex, double mass)
    {
        var byIso = map[isotopeIndex];
        MzRange? best = null;
        foreach (var kv in byIso)
        {
            if (kv.Key > mass) { best = kv.Value; break; }
        }
        if (best is null && byIso.Count > 0)
            best = byIso.Reverse().First().Value;
        return best ?? new MzRange(0, 0);
    }
}

/// <summary>
/// Computes correlation between an MS1 <see cref="PeakCluster"/> and a list of MS2
/// <see cref="PeakCurve"/>s. Port of cpp <c>DiaUmpire::CorrCalcCluster2Curve</c>.
/// </summary>
public class CorrCalcCluster2Curve
{
    private readonly IReadOnlyList<(float Key, PeakCurve Value)> _peakCurveSortedListApexRT;
    private readonly InstrumentParameter _parameter;

    /// <summary>The MS1 cluster being scored.</summary>
    public PeakCluster MS1PeakCluster { get; }

    /// <summary>Output: fragment-peak edges that passed the correlation threshold.</summary>
    public List<PrecursorFragmentPairEdge> GroupedFragmentList { get; } = new();

    /// <summary>
    /// Constructs the calculator. <paramref name="peakCurveSortedListApexRT"/> must be sorted
    /// ascending by the float key (= apex RT) — cpp uses <c>std::multimap&lt;float, PeakCurvePtr&gt;</c>.
    /// </summary>
    public CorrCalcCluster2Curve(PeakCluster ms1PeakCluster,
        IReadOnlyList<(float Key, PeakCurve Value)> peakCurveSortedListApexRT,
        InstrumentParameter parameter)
    {
        MS1PeakCluster = ms1PeakCluster;
        _peakCurveSortedListApexRT = peakCurveSortedListApexRT;
        _parameter = parameter;
    }

    /// <summary>Runs the correlation calculation and populates <see cref="GroupedFragmentList"/>.</summary>
    public void Run()
    {
        float lowKey = MS1PeakCluster.PeakHeightRT[0] - _parameter.DeltaApex;
        float highKey = MS1PeakCluster.PeakHeightRT[0] + _parameter.DeltaApex;
        int startIdx = LowerBoundByKey(_peakCurveSortedListApexRT, lowKey);
        int endIdx = LowerBoundByKey(_peakCurveSortedListApexRT, highKey);

        PeakCurve targetMS1Curve = MS1PeakCluster.MonoIsotopePeak!;
        float ms1RtRange = targetMS1Curve.EndRT() - targetMS1Curve.StartRT();
        int highCorrCnt = 0;

        for (int idx = startIdx; idx < endIdx; idx++)
        {
            var peakCurve = _peakCurveSortedListApexRT[idx].Value;
            if (peakCurve.TargetMz > MS1PeakCluster.NeutralMass()) continue;

            float peakCurveRtRange = peakCurve.EndRT() - peakCurve.StartRT();
            float overlapP = 0;
            if (targetMS1Curve.StartRT() >= peakCurve.StartRT() && targetMS1Curve.StartRT() <= peakCurve.EndRT() && targetMS1Curve.EndRT() >= peakCurve.EndRT())
                overlapP = (peakCurve.EndRT() - targetMS1Curve.StartRT()) / ms1RtRange;
            else if (targetMS1Curve.EndRT() >= peakCurve.StartRT() && targetMS1Curve.EndRT() <= peakCurve.EndRT() && targetMS1Curve.StartRT() <= peakCurve.StartRT())
                overlapP = (targetMS1Curve.EndRT() - peakCurve.StartRT()) / ms1RtRange;
            else if (targetMS1Curve.StartRT() <= peakCurve.StartRT() && targetMS1Curve.EndRT() >= peakCurve.EndRT())
                overlapP = peakCurveRtRange / ms1RtRange;
            else if (targetMS1Curve.StartRT() >= peakCurve.StartRT() && targetMS1Curve.EndRT() <= peakCurve.EndRT())
                overlapP = 1;

            if (overlapP > _parameter.RTOverlap
                && targetMS1Curve.ApexRT >= peakCurve.StartRT() && targetMS1Curve.ApexRT <= peakCurve.EndRT()
                && peakCurve.ApexRT >= targetMS1Curve.StartRT() && peakCurve.ApexRT <= targetMS1Curve.EndRT())
            {
                float apexDiff = System.Math.Abs(targetMS1Curve.ApexRT - peakCurve.ApexRT);
                float corr = PeakCurveCorrCalc.CalPeakCorr(targetMS1Curve, peakCurve, _parameter.NoPeakPerMin);

                if (!float.IsNaN(corr) && corr > _parameter.CorrThreshold)
                {
                    var pair = new PrecursorFragmentPairEdge
                    {
                        Correlation = corr,
                        PeakCurveIndexA = MS1PeakCluster.Index,
                        PeakCurveIndexB = peakCurve.Index,
                        FragmentMz = peakCurve.TargetMz,
                        Intensity = peakCurve.ApexInt,
                        RTOverlapP = overlapP,
                        DeltaApex = apexDiff,
                    };
                    GroupedFragmentList.Add(pair);
                    if (pair.Correlation > _parameter.HighCorrThreshold) highCorrCnt++;
                }
            }
        }

        if (highCorrCnt < _parameter.MinHighCorrCnt) GroupedFragmentList.Clear();
    }

    private static int LowerBoundByKey(IReadOnlyList<(float Key, PeakCurve Value)> sorted, float value)
    {
        int lo = 0, hi = sorted.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (sorted[mid].Key < value) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}

/// <summary>Quality bucket emitted alongside a pseudo-MS/MS spectrum.</summary>
public enum QualityLevel
{
    /// <summary>Isotope-complete cluster.</summary>
    Q1IsotopeComplete,
    /// <summary>MS1 cluster but missing isotopologues.</summary>
    Q2Ms1Group,
    /// <summary>Unfragmented precursor only.</summary>
    Q3UnfragmentedPrecursor,
}

/// <summary>
/// Preprocesses a peak cluster + its grouped fragments into a pseudo-MS/MS spectrum.
/// Port of cpp <c>DiaUmpire::PseudoMSMSProcessing</c>.
/// </summary>
public class PseudoMSMSProcessing
{
    private readonly InstrumentParameter _parameter;
    private List<PrecursorFragmentPairEdge> _fragments;

    /// <summary>The MS1 precursor cluster.</summary>
    public PeakCluster Precursorcluster { get; }

    /// <summary>The quality bucket for the output spectrum.</summary>
    public QualityLevel QualityLevel { get; }

    /// <summary>Constructs the preprocessor.</summary>
    public PseudoMSMSProcessing(PeakCluster ms1Cluster,
        IList<PrecursorFragmentPairEdge> groupedFragmentPeaks,
        InstrumentParameter parameter, QualityLevel qualityLevel)
    {
        Precursorcluster = ms1Cluster;
        _fragments = new List<PrecursorFragmentPairEdge>(groupedFragmentPeaks);
        _parameter = parameter;
        QualityLevel = qualityLevel;
    }

    private void SortFragmentByMZ() => _fragments.Sort((a, b) => a.FragmentMz.CompareTo(b.FragmentMz));

    /// <summary>
    /// Walks the sorted fragments and merges isotopologues at charges 2 and 1 — survivors are
    /// promoted to singly-charged m/z if they passed the charge-2 test.
    /// </summary>
    public void DeisotopingForPeakClusterFragment()
    {
        var newFragments = new List<PrecursorFragmentPairEdge>();
        var fragmentMarked = new bool[_fragments.Count];
        for (int k = 0; k < fragmentMarked.Length; k++) fragmentMarked[k] = true;

        PrecursorFragmentPairEdge currentMax = _fragments[0];
        int currentMaxIndex = 0;
        for (int i = 1; i < _fragments.Count; i++)
        {
            if (InstrumentParameter.CalcPPM(_fragments[i].FragmentMz, currentMax.FragmentMz) > _parameter.MS2PPM)
            {
                fragmentMarked[currentMaxIndex] = false;
                currentMaxIndex = i;
                currentMax = _fragments[i];
            }
            else if (_fragments[i].Intensity > currentMax.Intensity)
            {
                currentMaxIndex = i;
                currentMax = _fragments[i];
            }
        }
        fragmentMarked[currentMaxIndex] = false;

        for (int i = 0; i < _fragments.Count; i++)
        {
            if (fragmentMarked[i]) continue;
            fragmentMarked[i] = true;
            var startFrag = _fragments[i];

            bool grouped = false;
            for (int charge = 2; charge >= 1; charge--)
            {
                float lastInt = startFrag.Intensity;
                // cpp parity: `found` is declared outside the pkidx loop and NEVER reset
                // (PeakCluster.hpp:772). Once any pkidx finds a partner, `found` stays
                // true for the rest of the pkidx iterations, even if later iterations
                // find nothing. This means a single-isotope match (e.g. pkidx=1 only) is
                // enough to trigger charge-state collapse. The behavior is a cpp logic
                // oversight (it conflates "found at this pkidx" with "found at all"); we
                // preserve it to keep bit-parity with cpp's pseudo-MS/MS output.
                bool found = false;
                for (int pkidx = 1; pkidx < 5; pkidx++)
                {
                    float targetMz = startFrag.FragmentMz + (float)pkidx / charge;
                    for (int j = i + 1; j < _fragments.Count; j++)
                    {
                        if (fragmentMarked[j]) continue;
                        var targetFrag = _fragments[j];
                        if (InstrumentParameter.CalcPPM(targetFrag.FragmentMz, targetMz) < _parameter.MS2PPM * (pkidx * 0.5f + 1))
                        {
                            if (targetFrag.Intensity < lastInt)
                            {
                                fragmentMarked[j] = true;
                                lastInt = targetFrag.Intensity;
                                found = true;
                                break;
                            }
                        }
                        else if (targetFrag.FragmentMz > targetMz) break;
                    }
                    if (!found) break;
                }
                if (found)
                {
                    grouped = true;
                    startFrag.FragmentMz = startFrag.FragmentMz * charge - (charge - 1) * PeakCluster.ProtonMassExternal;
                    if (startFrag.FragmentMz <= Precursorcluster.NeutralMass())
                        newFragments.Add(startFrag);
                }
            }
            if (!grouped) newFragments.Add(startFrag);
        }

        _fragments = newFragments;
        SortFragmentByMZ();
    }

    /// <summary>Drops fragments that already appear in <paramref name="matchedFragmentMap"/>.</summary>
    public void RemoveMatchedFrag(Dictionary<int, List<PrecursorFragmentPairEdge>> matchedFragmentMap)
    {
        var newList = new List<PrecursorFragmentPairEdge>();
        foreach (var f in _fragments)
            if (!matchedFragmentMap.ContainsKey(f.PeakCurveIndexB))
                newList.Add(f);
        _fragments = newList;
    }

    /// <summary>
    /// Boosts complementary-ion partners to the brightest member of each pair group.
    /// </summary>
    /// <remarks>
    /// <b>cpp parity quirk — this method is effectively a no-op on the persisted state.</b>
    /// cpp uses <c>std::vector&lt;PrecursorFragmentPairEdge&gt;</c> (value-type storage) for
    /// the grouping buffer; the final loop mutates references-to-copies-in-the-vector and
    /// those mutations are discarded when the vector goes out of scope. The cpp
    /// <c>fragmentmarked</c> array is also local. So everything cpp's BoostComplementaryIon
    /// "does" is silently dropped at function exit. <see cref="PrecursorFragmentPairEdge"/>
    /// is a class in C# (reference semantics), so a literal port would persist the boost —
    /// we preserve cpp's behavior by keeping this method body empty. (See PeakCluster.hpp:828
    /// in cpp for the original.)
    /// </remarks>
    public void BoostComplementaryIon()
    {
        // intentionally empty — see <remarks>.
    }

    /// <summary>Marks complementary-ion pairs without boosting (used for ID-only mode).</summary>
    public void IdentifyComplementaryIon(float totalMass)
    {
        var fragmentMarked = new bool[_fragments.Count];
        for (int i = 0; i < _fragments.Count; i++)
        {
            var unit = _fragments[i];
            if (fragmentMarked[i]) continue;
            fragmentMarked[i] = true;

            var grouped = new List<PrecursorFragmentPairEdge> { unit };
            float complefrag1 = totalMass - unit.FragmentMz + 2 * PeakCluster.ProtonMassExternal;

            if (complefrag1 >= unit.FragmentMz)
            {
                for (int j = i + 1; j < _fragments.Count; j++)
                {
                    if (fragmentMarked[j]) continue;
                    var unit2 = _fragments[j];
                    if (InstrumentParameter.CalcPPM(complefrag1, unit2.FragmentMz) < _parameter.MS2PPM)
                    {
                        grouped.Add(unit2);
                        fragmentMarked[j] = true;
                    }
                    else if (unit2.FragmentMz > complefrag1) break;
                }
            }
            foreach (var f in grouped) f.ComplementaryFragment = true;
        }
    }

    /// <summary>Emits the m/z + intensity arrays for the pseudo-MS/MS scan.</summary>
    public void GetScan(out double[] mzArray, out double[] intensityArray)
    {
        var scan = new XYPointCollection();
        foreach (var unit in _fragments)
        {
            float intensity = _parameter.AdjustFragIntensity
                ? unit.Intensity * unit.Correlation * unit.Correlation
                : unit.Intensity;
            scan.AddPointKeepMaxIfCloseValueExisted(unit.FragmentMz, intensity, _parameter.MS2PPM);
        }
        mzArray = new double[scan.Data.Count];
        intensityArray = new double[scan.Data.Count];
        for (int i = 0; i < scan.Data.Count; ++i)
        {
            mzArray[i] = scan.Data[i].Mz;
            intensityArray[i] = scan.Data[i].Intensity;
        }
    }

    /// <summary>Runs the full preprocessing chain (deisotope + boost) if enabled.</summary>
    public void Run()
    {
        if (_fragments.Count < 2) return;
        SortFragmentByMZ();
        if (_parameter.BoostComplementaryIon)
        {
            DeisotopingForPeakClusterFragment();
            BoostComplementaryIon();
        }
    }

    /// <summary>Read-only access to the current fragment list (post-Run output).</summary>
    public IReadOnlyList<PrecursorFragmentPairEdge> Fragments => _fragments;
}
