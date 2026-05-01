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
        s.Params.Set(CVID.MS_positive_scan);
        var scan = new Scan();
        scan.Set(CVID.MS_scan_start_time, scanTimeSec, CVID.UO_second);
        s.ScanList.Scans.Add(scan);
        return s;
    }

    private static List<int> Indices(SpectrumListFilter f) =>
        Enumerable.Range(0, f.Count).Select(i => f.SpectrumIdentity(i).Index).ToList();

    [TestMethod]
    public void IndexAndIdBasedPredicates_KeepExpectedSpectra()
    {
        // IndexSet: range [3, 7] keeps indices 3..7 in order.
        var inner = BuildList(10, i => MakeSpectrum(i, 1, i));
        var idxSet = new IntegerSet();
        idxSet.Insert(3, 7);
        var byIndex = new SpectrumListFilter(inner, new IndexSetPredicate(idxSet));
        Assert.AreEqual(5, byIndex.Count);
        CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 7 }, Indices(byIndex));

        // ScanNumberSet: inspects "scan=N" in the spectrum id; keeps only matching numbers.
        var smaller = BuildList(5, i => MakeSpectrum(i, 1, 0));
        var scanNumSet = new IntegerSet();
        scanNumSet.Insert(2);
        scanNumSet.Insert(4);
        var byScanNumber = new SpectrumListFilter(smaller, new ScanNumberSetPredicate(scanNumSet));
        Assert.AreEqual(2, byScanNumber.Count);
        Assert.AreEqual("scan=2", byScanNumber.SpectrumIdentity(0).Id);
        Assert.AreEqual("scan=4", byScanNumber.SpectrumIdentity(1).Id);

        // IdSet: exact id match against a fixed set.
        var triple = BuildList(3, i => MakeSpectrum(i, 1, 0));
        var byId = new SpectrumListFilter(triple, new IdSetPredicate(new[] { "scan=1", "scan=3" }));
        Assert.AreEqual(2, byId.Count);
    }

    [TestMethod]
    public void MsLevel_KeepsOnlyMatchingLevel()
    {
        // Alternating MS1/MS2 → MsLevel(2) keeps the two MS2s.
        var inner = BuildList(4, i => MakeSpectrum(i, (i % 2) + 1, 0));
        var filtered = new SpectrumListFilter(inner, new MsLevelPredicate(2));
        Assert.AreEqual(2, filtered.Count);
        for (int i = 0; i < filtered.Count; i++)
            Assert.AreEqual(2, filtered.GetSpectrum(i).Params.CvParam(CVID.MS_ms_level).ValueAs<int>());
    }

    [TestMethod]
    public void ScanTimeRange_InclusiveAndShortCircuits()
    {
        // Inclusive bounds: [15, 25] of times {10, 15, 20, 25, 30} keeps 3 spectra.
        var inclusive = BuildList(5, i => MakeSpectrum(i, 1, 10.0 + i * 5));
        Assert.AreEqual(3, new SpectrumListFilter(inclusive, new ScanTimeRangePredicate(15, 25)).Count);

        // Short-circuit: a 10k-element list with sorted times stops iterating once past upper bound.
        var bigList = BuildList(10_000, i => MakeSpectrum(i, 1, i));
        Assert.AreEqual(2, new SpectrumListFilter(bigList, new ScanTimeRangePredicate(0, 1)).Count);
    }

    [TestMethod]
    public void ContentBasedPredicates_FilterByArrayLengthAndPolarity()
    {
        // DefaultArrayLength: values 10, 20, 30, 40, 50; window [20, 40] keeps 3.
        var byLen = BuildList(5, i =>
        {
            var s = MakeSpectrum(i, 1, 0);
            s.DefaultArrayLength = (i + 1) * 10;
            return s;
        });
        var lenSet = new IntegerSet();
        lenSet.Insert(20, 40);
        Assert.AreEqual(3, new SpectrumListFilter(byLen, new DefaultArrayLengthPredicate(lenSet)).Count);

        // Polarity: keep only positive-scan spectra.
        var mixed = new SpectrumListSimple();
        mixed.Spectra.Add(MakeSpectrum(0, 1, 0)); // positive (set in factory)
        var neg = new Spectrum { Index = 1, Id = "scan=2" };
        neg.Params.Set(CVID.MS_ms_level, 1);
        neg.Params.Set(CVID.MS_negative_scan);
        mixed.Spectra.Add(neg);
        var positiveOnly = new SpectrumListFilter(mixed, new PolarityPredicate(CVID.MS_positive_scan));
        Assert.AreEqual(1, positiveOnly.Count);
        Assert.AreEqual(0, positiveOnly.SpectrumIdentity(0).Index);
    }

    [TestMethod]
    public void Composition_AndAndNegated()
    {
        // 6 alternating MS1/MS2 with times 10..15.
        var inner = BuildList(6, i => MakeSpectrum(i, (i % 2) + 1, 10.0 + i));

        // (MS2 AND time in [11,14]) keeps indices 1 and 3.
        var both = new AndPredicate(
            new MsLevelPredicate(2),
            new ScanTimeRangePredicate(11, 14));
        CollectionAssert.AreEqual(new[] { 1, 3 }, Indices(new SpectrumListFilter(inner, both)));

        // NOT MS2 keeps indices 0, 2, 4 (the MS1s).
        CollectionAssert.AreEqual(
            new[] { 0, 2, 4 },
            Indices(new SpectrumListFilter(inner, new NegatedPredicate(new MsLevelPredicate(2)))));
    }
}
