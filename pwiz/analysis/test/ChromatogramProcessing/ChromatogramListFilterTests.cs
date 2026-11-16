using Pwiz.Analysis.Filters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.ChromatogramProcessing;

/// <summary>
/// Mirrors cpp <c>ChromatogramList_FilterTest.cpp</c>: a synthetic
/// <see cref="ChromatogramListSimple"/> with 5 chromatograms is shared across each test,
/// and each <c>[TestMethod]</c> exercises the filter against an
/// <see cref="IChromatogramPredicate"/>.
/// </summary>
[TestClass]
public class ChromatogramListFilterTests
{
    internal static ChromatogramListSimple Build(int count = 5)
    {
        var list = new ChromatogramListSimple();
        for (int i = 0; i < count; i++)
        {
            var c = new Chromatogram { Index = i, Id = $"chrom_{i}", DefaultArrayLength = 16 };
            // Time + intensity arrays — minimal payload. Single peak at index i*2+1.
            var time = new BinaryDataArray();
            time.Set(CVID.MS_time_array, string.Empty, CVID.UO_second);
            var inten = new BinaryDataArray();
            inten.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
            for (int j = 0; j < 16; j++)
            {
                time.Data.Add(j * 0.5);
                inten.Data.Add(j == i * 2 + 1 ? (i + 1) * 100.0 : 0.0);
            }
            c.BinaryDataArrays.Add(time);
            c.BinaryDataArrays.Add(inten);
            list.Chromatograms.Add(c);
        }
        return list;
    }

    [TestMethod]
    public void IndexSet_AcceptsRequestedIndices_AndRenumbers()
    {
        var inner = Build();
        var set = new IntegerSet(); set.Insert(1); set.Insert(3);
        var filtered = new ChromatogramListFilter(inner, new ChromatogramIndexSetPredicate(set));

        Assert.AreEqual(2, filtered.Count);
        // Indices renumbered to 0..N-1 on the filtered view (matches cpp).
        Assert.AreEqual(0, filtered.ChromatogramIdentity(0).Index);
        Assert.AreEqual("chrom_1", filtered.ChromatogramIdentity(0).Id);
        Assert.AreEqual(1, filtered.ChromatogramIdentity(1).Index);
        Assert.AreEqual("chrom_3", filtered.ChromatogramIdentity(1).Id);

        // GetChromatogram round-trips the index renumbering too.
        var c = filtered.GetChromatogram(0, getBinaryData: true);
        Assert.AreEqual(0, c.Index);
        Assert.AreEqual("chrom_1", c.Id);
    }

    [TestMethod]
    public void IndexSet_EmptySet_ReturnsZeroChromatograms()
    {
        var inner = Build();
        var set = new IntegerSet();
        var filtered = new ChromatogramListFilter(inner, new ChromatogramIndexSetPredicate(set));
        Assert.AreEqual(0, filtered.Count);
    }

    [TestMethod]
    public void IndexSet_PastUpperBound_StopsEarlyViaDone()
    {
        // After the upper bound has been observed, the predicate's Done becomes true and the
        // filter loop breaks — verify by feeding a predicate that throws if it sees more than
        // upperBound+1 inputs.
        var inner = Build(count: 100);
        var set = new IntegerSet(); set.Insert(0, 2); // [0, 2] — upper bound 2
        var pred = new TrackingPredicate(new ChromatogramIndexSetPredicate(set));
        var filtered = new ChromatogramListFilter(inner, pred);
        Assert.AreEqual(3, filtered.Count);
        // Predicate was consulted only up to index 2 + 1-after upper bound check.
        Assert.IsTrue(pred.MaxIndexSeen <= 2, $"expected early termination at index 2, saw {pred.MaxIndexSeen}");
    }

    private sealed class TrackingPredicate : IChromatogramPredicate
    {
        private readonly IChromatogramPredicate _inner;
        public int MaxIndexSeen { get; private set; } = -1;
        public TrackingPredicate(IChromatogramPredicate inner) { _inner = inner; }
        public PredicateDecision Accept(ChromatogramIdentity identity)
        {
            if (identity.Index > MaxIndexSeen) MaxIndexSeen = identity.Index;
            return _inner.Accept(identity);
        }
        public bool Done => _inner.Done;
        public string Describe() => _inner.Describe();
    }
}
