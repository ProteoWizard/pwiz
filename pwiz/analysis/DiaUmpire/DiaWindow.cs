// Per-window pipeline + state. Port of cpp DiaUmpire::DiaWindow (a nested
// struct in DiaUmpire.cpp that extends TargetWindow). Split out for clarity
// in C# — keeps DiaUmpire.cs focused on orchestration and this file focused
// on the per-window precursor/fragment pairing logic.

namespace Pwiz.Analysis.DiaUmpire;

/// <summary>
/// Per-DIA-window state + the precursor/fragment-pair-building stage.
/// Port of cpp <c>DiaUmpire::DiaWindow</c>. cpp embeds DiaWindow as a nested
/// struct in <c>DiaUmpire.cpp</c>; pwiz-sharp keeps it in its own file because
/// the pairing methods are 200+ LOC and the file becomes unwieldy otherwise.
/// </summary>
/// <remarks>
/// Inherits the m/z range and spectra-in-range list from <see cref="TargetWindow"/>;
/// adds the per-window peak curves / clusters / pair-ranking maps that the
/// MS2 pipeline mutates.
/// </remarks>
internal sealed class DiaWindow
{
    /// <summary>The m/z range of this window (cpp parity: copied from the source TargetWindow).</summary>
    public MzRange MzRange { get; }

    /// <summary>Indices into the inner SpectrumList for spectra that fall in this window.</summary>
    public List<int> SpectraInRange { get; }

    /// <summary>The m/z range of the next-higher-m/z window, or <see cref="MzRange.Empty"/> if this is the last.</summary>
    public MzRange NextWindowMzRange { get; }

    /// <summary>MS2 peak curves built within this window.</summary>
    public List<PeakCurve> PeakCurves { get; } = new();

    /// <summary>MS2 peak clusters built from <see cref="PeakCurves"/>.</summary>
    public List<PeakCluster> PeakClusters { get; } = new();

    /// <summary>MS1 ranking: PeakCurveIndexB → sorted-desc correlations across all matching pairs.</summary>
    public Dictionary<int, List<float>> FragmentMS1Ranking { get; } = new();

    /// <summary>Same shape as <see cref="FragmentMS1Ranking"/> but for unfragmented-ion pairing.</summary>
    public Dictionary<int, List<float>> FragmentUnfragRanking { get; } = new();

    /// <summary>MS1-cluster index → list of fragment-pair edges for the MS1 pairing pass.</summary>
    public Dictionary<int, List<PrecursorFragmentPairEdge>> FragmentsClu2Cur { get; } = new();

    /// <summary>MS2-cluster index → list of fragment-pair edges for the unfragmented-ion pairing pass.</summary>
    public Dictionary<int, List<PrecursorFragmentPairEdge>> UnFragIonClu2Cur { get; } = new();

    /// <summary>Creates a DiaWindow from a source <see cref="TargetWindow"/>.</summary>
    public DiaWindow(TargetWindow target, TargetWindow? next)
    {
        System.ArgumentNullException.ThrowIfNull(target);
        MzRange = target.MzRange;
        SpectraInRange = target.SpectraInRange;
        NextWindowMzRange = next is null ? MzRange.Empty : next.MzRange;
    }

    /// <summary>
    /// MS1-side fragment pairing: walks every MS1 PeakCluster in this window's m/z range
    /// and scores its co-elution against every MS2 fragment curve. Survivors land in
    /// <see cref="FragmentsClu2Cur"/>. Port of cpp <c>DiaWindow::PrecursorFragmentPairBuildingForMS1</c>.
    /// </summary>
    public void PrecursorFragmentPairBuildingForMS1(DiaUmpire.Impl diaUmpire)
    {
        var ms1Clusters = diaUmpire.Ms1PeakClusters;
        var p = diaUmpire.Config.InstrumentParameters;
        var byRT = SortByApexRt(PeakCurves);

        var jobs = new List<CorrCalcCluster2Curve>(ms1Clusters.Count);
        foreach (var cluster in ms1Clusters)
        {
            if (cluster.GetMaxMz() < MzRange.Begin || cluster.TargetMz() > MzRange.End) continue;
            var job = new CorrCalcCluster2Curve(cluster, byRT, p);
            job.Run();
            jobs.Add(job);
        }

        foreach (var unit in jobs)
        {
            if (unit.GroupedFragmentList.Count == 0) continue;
            FragmentsClu2Cur[unit.MS1PeakCluster.Index] = unit.GroupedFragmentList;
        }

        BuildRankings(FragmentsClu2Cur, FragmentMS1Ranking);
        FilterByCriteria(FragmentsClu2Cur, p);
    }

    /// <summary>
    /// MS2-side unfragmented-precursor pairing: same shape as
    /// <see cref="PrecursorFragmentPairBuildingForMS1"/> but using this window's own
    /// MS2 clusters as the "precursor" side. Port of cpp
    /// <c>DiaWindow::PrecursorFragmentPairBuildingForUnfragmentedIon</c>.
    /// </summary>
    public void PrecursorFragmentPairBuildingForUnfragmentedIon(DiaUmpire.Impl diaUmpire)
    {
        if (PeakClusters.Count == 0) return;

        var p = diaUmpire.Config.InstrumentParameters;
        var byRT = SortByApexRt(PeakCurves);

        var jobs = new List<CorrCalcCluster2Curve>(PeakClusters.Count);
        foreach (var cluster in PeakClusters)
        {
            if (cluster.GetMaxMz() < MzRange.Begin || cluster.TargetMz() > MzRange.End) continue;
            var job = new CorrCalcCluster2Curve(cluster, byRT, p);
            job.Run();
            jobs.Add(job);
        }

        foreach (var unit in jobs)
        {
            if (unit.GroupedFragmentList.Count == 0) continue;
            UnFragIonClu2Cur[unit.MS1PeakCluster.Index] = unit.GroupedFragmentList;
        }

        // cpp parity bug we preserve: the unfragmented-ion path reads/writes
        // FragmentUnfragRanking but also has a stray accumulation INTO
        // FragmentMS1Ranking inside the same loop. We preserve cpp's behaviour
        // because phase 5 will validate against cpp output — see DiaUmpire.cpp
        // lines ~1480-1505. The first inner foreach below writes to
        // FragmentMS1Ranking, then the outer sort + lookup uses FragmentUnfragRanking.
        // (cpp does:
        //   for (auto& clusterCurvePair : UnFragIonClu2Cur)
        //       for (auto& fcu : clusterCurvePair.second)
        //           FragmentMS1Ranking[fcu.PeakCurveIndexB].push_back(fcu.Correlation);
        //   for (auto& fragmentRankingPair : FragmentUnfragRanking)
        //       sort(... descending ...);
        // -- so the rank lookup later uses an empty FragmentUnfragRanking. Replicated here.)
        foreach (var kvp in UnFragIonClu2Cur)
            foreach (var fragmentClusterUnit in kvp.Value)
            {
                if (!FragmentMS1Ranking.TryGetValue(fragmentClusterUnit.PeakCurveIndexB, out var list))
                {
                    list = new List<float>();
                    FragmentMS1Ranking[fragmentClusterUnit.PeakCurveIndexB] = list;
                }
                list.Add(fragmentClusterUnit.Correlation);
            }

        foreach (var kvp in FragmentUnfragRanking)
            kvp.Value.Sort((a, b) => b.CompareTo(a));

        foreach (var kvp in UnFragIonClu2Cur)
        {
            foreach (var fragmentClusterUnit in kvp.Value)
            {
                if (!FragmentUnfragRanking.TryGetValue(fragmentClusterUnit.PeakCurveIndexB, out var scorelist))
                    continue;
                for (int intidx = 0; intidx < scorelist.Count; ++intidx)
                {
                    if (scorelist[intidx] <= fragmentClusterUnit.Correlation)
                    {
                        fragmentClusterUnit.FragmentMS1Rank = intidx + 1;
                        fragmentClusterUnit.FragmentMS1RankScore = (float)fragmentClusterUnit.FragmentMS1Rank / scorelist.Count;
                        break;
                    }
                }
            }
        }

        FilterByCriteria(UnFragIonClu2Cur, p);
    }

    // ---------- helpers ----------

    /// <summary>Builds a per-curve apex-RT-sorted list of (Key=apexRT, Value=curve) tuples.</summary>
    private static List<(float Key, PeakCurve Value)> SortByApexRt(List<PeakCurve> peakCurves)
    {
        var list = new List<(float Key, PeakCurve Value)>(peakCurves.Count);
        foreach (var pc in peakCurves) list.Add((pc.ApexRT, pc));
        list.Sort((a, b) => a.Key.CompareTo(b.Key));
        return list;
    }

    /// <summary>
    /// Mirrors cpp's "BuildFragmentMS1ranking" + per-fragment rank-lookup steps for the
    /// MS1 path. Populates <paramref name="ranking"/> with descending-sorted correlations
    /// per fragment curve index, then sets <c>FragmentMS1Rank</c> / <c>FragmentMS1RankScore</c>
    /// on every edge in <paramref name="cluCur"/>.
    /// </summary>
    private static void BuildRankings(Dictionary<int, List<PrecursorFragmentPairEdge>> cluCur,
                                       Dictionary<int, List<float>> ranking)
    {
        foreach (var kvp in cluCur)
            foreach (var edge in kvp.Value)
            {
                if (!ranking.TryGetValue(edge.PeakCurveIndexB, out var list))
                {
                    list = new List<float>();
                    ranking[edge.PeakCurveIndexB] = list;
                }
                list.Add(edge.Correlation);
            }

        foreach (var kvp in ranking) kvp.Value.Sort((a, b) => b.CompareTo(a)); // descending

        foreach (var kvp in cluCur)
        {
            foreach (var edge in kvp.Value)
            {
                if (!ranking.TryGetValue(edge.PeakCurveIndexB, out var scorelist)) continue;
                for (int intidx = 0; intidx < scorelist.Count; ++intidx)
                {
                    if (scorelist[intidx] <= edge.Correlation)
                    {
                        edge.FragmentMS1Rank = intidx + 1;
                        edge.FragmentMS1RankScore = (float)edge.FragmentMS1Rank / scorelist.Count;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Drops edges that fail (CorrRank ≤ RFmax) AND (FragmentMS1Rank ≤ RPmax) AND
    /// (Correlation ≥ CorrThreshold) AND (DeltaApex ≤ DeltaApex). Mutates
    /// <paramref name="cluCur"/> in place. Port of cpp's "FilterByCriteria" block.
    /// </summary>
    private static void FilterByCriteria(Dictionary<int, List<PrecursorFragmentPairEdge>> cluCur, InstrumentParameter p)
    {
        var templist = new Dictionary<int, List<PrecursorFragmentPairEdge>>();
        foreach (var kvp in cluCur)
        {
            var corrArrayList = new List<float>(kvp.Value.Count);
            var scoreList = new Dictionary<(int A, int B), float>();
            foreach (var edge in kvp.Value)
            {
                float score = edge.Correlation * edge.Correlation * (float)System.Math.Log(edge.Intensity);
                scoreList[(edge.PeakCurveIndexA, edge.PeakCurveIndexB)] = score;
                corrArrayList.Add(score);
            }

            corrArrayList.Sort((a, b) => b.CompareTo(a)); // descending
            var newlist = new List<PrecursorFragmentPairEdge>();
            templist[kvp.Key] = newlist;

            foreach (var edge in kvp.Value)
            {
                int corrRank = 0;
                float thisScore = scoreList[(edge.PeakCurveIndexA, edge.PeakCurveIndexB)];
                for (int intidx = 0; intidx < corrArrayList.Count; ++intidx)
                {
                    if (corrArrayList[intidx] <= thisScore)
                    {
                        corrRank = intidx + 1;
                        break;
                    }
                }
                if (edge.Correlation >= p.CorrThreshold
                    && corrRank <= p.RFmax
                    && edge.FragmentMS1Rank <= p.RPmax
                    && edge.DeltaApex <= p.DeltaApex)
                {
                    newlist.Add(edge);
                }
            }
        }
        cluCur.Clear();
        foreach (var kvp in templist) cluCur[kvp.Key] = kvp.Value;
    }
}
