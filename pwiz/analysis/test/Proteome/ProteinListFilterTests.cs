using Pwiz.Analysis.Proteome;
using Pwiz.Data.Common.Proteome;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.Proteome;

/// <summary>
/// Port of cpp <c>ProteinList_FilterTest</c>. Mirrors cpp's pattern: one entry point
/// (<c>test()</c> in cpp, <see cref="Filter"/> here) that calls the three sub-test
/// helpers — selected-index ad-hoc predicate, IndexSet predicate, IdSet predicate —
/// plus one sharp-only sub-test covering the indeterminate (tribool null) retry path.
/// </summary>
[TestClass]
public class ProteinListFilterTests
{
    [TestMethod]
    public void Filter()
    {
        var pl = BuildTestList();
        TestSelectedIndices(pl);
        TestIndexSet(pl);
        TestIdSet(pl);
        TestIndeterminatePredicate();
    }

    // Mirrors cpp's createProteinList(): 10 proteins "Pro1".."Pro10" at indices 0..9,
    // each with a 16-char single-residue sequence (A..J) so the tests can fingerprint
    // identity without separately constructing a sequence model.
    private static ProteinListSimple BuildTestList()
    {
        var pl = new ProteinListSimple();
        for (int i = 0; i < 10; i++)
        {
            string seq = new string((char)('A' + i), 16);
            pl.Proteins.Add(new Protein($"Pro{i + 1}", i, string.Empty, seq));
        }
        return pl;
    }

    // cpp testSelectedIndices: ad-hoc predicate keeps indices 1/3/5 and trips done()
    // once it's seen anything past index 5, short-circuiting the iteration.
    private static void TestSelectedIndices(ProteinList pl)
    {
        var filter = new ProteinList_Filter(pl, new SelectedIndexPredicate());
        Assert.AreEqual(3, filter.Count, "selectedIndices.Count");
        Assert.AreEqual("Pro2", filter.GetProtein(0).Id, "selectedIndices[0]");
        Assert.AreEqual("Pro4", filter.GetProtein(1).Id, "selectedIndices[1]");
        Assert.AreEqual("Pro6", filter.GetProtein(2).Id, "selectedIndices[2]");
        // Each filtered protein's Index is rewritten to its position in the sub-list.
        Assert.AreEqual(0, filter.GetProtein(0).Index, "selectedIndices[0].Index");
        Assert.AreEqual(1, filter.GetProtein(1).Index, "selectedIndices[1].Index");
        Assert.AreEqual(2, filter.GetProtein(2).Index, "selectedIndices[2].Index");
    }

    // cpp testIndexSet: IntegerSet covering [3,5], 7, 9 -> Pro4..Pro6, Pro8, Pro10.
    private static void TestIndexSet(ProteinList pl)
    {
        var indexSet = new IntegerSet();
        indexSet.Insert(3, 5);
        indexSet.Insert(7);
        indexSet.Insert(9);
        var filter = new ProteinList_Filter(pl, new ProteinFilterPredicate_IndexSet(indexSet));
        Assert.AreEqual(5, filter.Count, "indexSet.Count");
        Assert.AreEqual("Pro4", filter.GetProtein(0).Id, "indexSet[0]");
        Assert.AreEqual("Pro5", filter.GetProtein(1).Id, "indexSet[1]");
        Assert.AreEqual("Pro6", filter.GetProtein(2).Id, "indexSet[2]");
        Assert.AreEqual("Pro8", filter.GetProtein(3).Id, "indexSet[3]");
        Assert.AreEqual("Pro10", filter.GetProtein(4).Id, "indexSet[4]");
    }

    // cpp testIdSet: keep Pro2/Pro3/Pro4/Pro7. Sharp extra: confirm the predicate's
    // done() short-circuits iteration once the set is drained (cpp doesn't assert this
    // explicitly but the implementation relies on it).
    private static void TestIdSet(ProteinList pl)
    {
        var counting = new CountingProteinList(pl);
        var filter = new ProteinList_Filter(counting, new ProteinFilterPredicate_IdSet(new[] { "Pro2", "Pro3", "Pro4", "Pro7" }));
        Assert.AreEqual(4, filter.Count, "idSet.Count");
        Assert.AreEqual("Pro2", filter.GetProtein(0).Id, "idSet[0]");
        Assert.AreEqual("Pro3", filter.GetProtein(1).Id, "idSet[1]");
        Assert.AreEqual("Pro4", filter.GetProtein(2).Id, "idSet[2]");
        Assert.AreEqual("Pro7", filter.GetProtein(3).Id, "idSet[3]");
        // 7 inner reads at construction (indices 0..6) — Pro7 found at ord 6, done() trips.
        // Plus 4 reads to satisfy filter.GetProtein(0..3). Total <= 11.
        Assert.IsTrue(counting.GetProteinCalls <= 11,
            $"IdSet predicate should short-circuit; saw {counting.GetProteinCalls} GetProtein calls.");
    }

    // Sharp-only sub-test: predicates can return null (cpp tribool indeterminate) when
    // they need the sequence to decide; the filter retries with getSequence=true.
    private static void TestIndeterminatePredicate()
    {
        // BuildTestList()'s sequences are AA…/BB…/CC… — predicate accepts where the
        // first residue is 'C' → only Pro3 (index 2) matches.
        var pl = BuildTestList();
        var pred = new SequenceFirstCharPredicate('C');
        var filter = new ProteinList_Filter(pl, pred);
        Assert.AreEqual(1, filter.Count, "indeterminate.Count");
        Assert.AreEqual("Pro3", filter.GetProtein(0).Id, "indeterminate[0]");
        Assert.IsTrue(pred.SequenceWasRequested,
            "predicate must observe at least one call where the sequence was loaded");
    }

    private sealed class SelectedIndexPredicate : IProteinFilterPredicate
    {
        private bool _pastMaxIndex;
        public bool? Accept(Protein protein)
        {
            if (protein.Index > 5) _pastMaxIndex = true;
            return protein.Index == 1 || protein.Index == 3 || protein.Index == 5;
        }
        public bool Done => _pastMaxIndex;
    }

    private sealed class SequenceFirstCharPredicate : IProteinFilterPredicate
    {
        private readonly char _wanted;
        public bool SequenceWasRequested { get; private set; }

        public SequenceFirstCharPredicate(char wanted) { _wanted = wanted; }

        public bool? Accept(Protein protein)
        {
            if (protein.Sequence.Length == 0) return null; // need the sequence first
            SequenceWasRequested = true;
            return protein.Sequence[0] == _wanted;
        }
        public bool Done => false;
    }

    private sealed class CountingProteinList : ProteinListWrapper
    {
        public int GetProteinCalls { get; private set; }
        public CountingProteinList(ProteinList inner) : base(inner) { }
        public override Protein GetProtein(int index, bool getSequence = true)
        {
            GetProteinCalls++;
            return base.GetProtein(index, getSequence);
        }
    }
}
