using Pwiz.Analysis.Filters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.Filters;

[TestClass]
public class SpectrumListFilterTests
{
    private static SpectrumListSimple BuildList(int count, Func<int, Spectrum> factory)
    {
        var list = new SpectrumListSimple();
        for (int i = 0; i < count; i++) list.Spectra.Add(factory(i));
        return list;
    }

    private static Spectrum MakeSpectrum(int index, int msLevel, double scanTimeSec)
    {
        var s = new Spectrum { Index = index, Id = $"scan={index + 1}" };
        s.Params.Set(CVID.MS_ms_level, msLevel);
        s.Params.Set(msLevel == 1 ? CVID.MS_positive_scan : CVID.MS_positive_scan);
        var scan = new Scan();
        scan.Set(CVID.MS_scan_start_time, scanTimeSec, CVID.UO_second);
        s.ScanList.Scans.Add(scan);
        return s;
    }

    // ---- IndexSet ----

    [TestMethod]
    public void IndexSet_KeepsMatchingIndices()
    {
        var inner = BuildList(5, i => MakeSpectrum(i, 1, 10.0 + i));

        var set = new IntegerSet();
        set.Insert(1, 3); // keep indices 1, 2, 3

        var filtered = new SpectrumListFilter(inner, new IndexSetPredicate(set));
        Assert.AreEqual(3, filtered.Count);
        Assert.AreEqual(1, filtered.SpectrumIdentity(0).Index);
        Assert.AreEqual(3, filtered.SpectrumIdentity(2).Index);
    }

    // ---- ScanNumberSet ----

    [TestMethod]
    public void ScanNumberSet_MatchesScanAttribute()
    {
        var inner = BuildList(5, i => MakeSpectrum(i, 1, 0)); // id "scan=1", "scan=2", ...

        var set = new IntegerSet();
        set.Insert(2);
        set.Insert(4);

        var filtered = new SpectrumListFilter(inner, new ScanNumberSetPredicate(set));
        Assert.AreEqual(2, filtered.Count);
        Assert.AreEqual("scan=2", filtered.SpectrumIdentity(0).Id);
        Assert.AreEqual("scan=4", filtered.SpectrumIdentity(1).Id);
    }

    // ---- IdSet ----

    [TestMethod]
    public void IdSet_ExactMatch()
    {
        var inner = BuildList(3, i => MakeSpectrum(i, 1, 0));
        var filtered = new SpectrumListFilter(inner,
            new IdSetPredicate(new[] { "scan=1", "scan=3" }));
        Assert.AreEqual(2, filtered.Count);
    }

    // ---- MsLevel ----

    [TestMethod]
    public void MsLevel_KeepsOnlyMatchingLevel()
    {
        var inner = BuildList(4, i => MakeSpectrum(i, (i % 2) + 1, 0)); // alternating MS1, MS2
        var filtered = new SpectrumListFilter(inner, new MsLevelPredicate(2));
        Assert.AreEqual(2, filtered.Count);
        foreach (var idx in new[] { 0, 1 })
        {
            var spec = filtered.GetSpectrum(idx);
            Assert.AreEqual(2, spec.Params.CvParam(CVID.MS_ms_level).ValueAs<int>());
        }
    }

    // ---- ScanTimeRange ----

    [TestMethod]
    public void ScanTimeRange_InclusiveBounds()
    {
        var inner = BuildList(5, i => MakeSpectrum(i, 1, 10.0 + i * 5)); // times 10, 15, 20, 25, 30
        var filtered = new SpectrumListFilter(inner, new ScanTimeRangePredicate(15, 25));
        Assert.AreEqual(3, filtered.Count);
    }

    [TestMethod]
    public void ScanTimeRange_ShortcircuitsPastUpperBound()
    {
        // A range covering only the first 2 spectra, with AssumeSorted=true — iteration stops early.
        var inner = BuildList(10_000, i => MakeSpectrum(i, 1, i));
        var filtered = new SpectrumListFilter(inner, new ScanTimeRangePredicate(0, 1));
        Assert.AreEqual(2, filtered.Count);
    }

    // ---- DefaultArrayLength ----

    [TestMethod]
    public void DefaultArrayLength_FiltersByArraySize()
    {
        var inner = BuildList(5, i =>
        {
            var s = MakeSpectrum(i, 1, 0);
            s.DefaultArrayLength = (i + 1) * 10; // 10, 20, 30, 40, 50
            return s;
        });

        var set = new IntegerSet();
        set.Insert(20, 40);
        var filtered = new SpectrumListFilter(inner, new DefaultArrayLengthPredicate(set));
        Assert.AreEqual(3, filtered.Count);
    }

    // ---- Polarity ----

    [TestMethod]
    public void Polarity_PositiveOnly()
    {
        var inner = new SpectrumListSimple();
        var pos = MakeSpectrum(0, 1, 0);
        var neg = new Spectrum { Index = 1, Id = "scan=2" };
        neg.Params.Set(CVID.MS_ms_level, 1);
        neg.Params.Set(CVID.MS_negative_scan);
        inner.Spectra.Add(pos);
        inner.Spectra.Add(neg);

        var filtered = new SpectrumListFilter(inner, new PolarityPredicate(CVID.MS_positive_scan));
        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual(0, filtered.SpectrumIdentity(0).Index);
    }

    // ---- Composition ----

    [TestMethod]
    public void AndPredicate_BothMustMatch()
    {
        var inner = BuildList(6, i => MakeSpectrum(i, (i % 2) + 1, 10.0 + i));
        // MS2 AND time in [11, 14] → indices 1 (MS2, 11) and 3 (MS2, 13)
        var pred = new AndPredicate(
            new MsLevelPredicate(2),
            new ScanTimeRangePredicate(11, 14));
        var filtered = new SpectrumListFilter(inner, pred);
        Assert.AreEqual(2, filtered.Count);
        CollectionAssert.AreEqual(
            new[] { 1, 3 },
            Enumerable.Range(0, filtered.Count).Select(i => filtered.SpectrumIdentity(i).Index).ToList());
    }

    [TestMethod]
    public void NegatedPredicate_Invert()
    {
        var inner = BuildList(4, i => MakeSpectrum(i, (i % 2) + 1, 0));
        var filtered = new SpectrumListFilter(inner, new NegatedPredicate(new MsLevelPredicate(2)));
        // Keeps MS1 only (indices 0, 2).
        Assert.AreEqual(2, filtered.Count);
        CollectionAssert.AreEqual(new[] { 0, 2 }, Enumerable.Range(0, filtered.Count).Select(i => filtered.SpectrumIdentity(i).Index).ToList());
    }

    [TestMethod]
    public void Filter_PreservesOrdering()
    {
        var inner = BuildList(10, i => MakeSpectrum(i, 1, i));
        var set = new IntegerSet();
        set.Insert(3, 7);
        var filtered = new SpectrumListFilter(inner, new IndexSetPredicate(set));
        CollectionAssert.AreEqual(
            new[] { 3, 4, 5, 6, 7 },
            Enumerable.Range(0, filtered.Count).Select(i => filtered.SpectrumIdentity(i).Index).ToList());
    }
}
