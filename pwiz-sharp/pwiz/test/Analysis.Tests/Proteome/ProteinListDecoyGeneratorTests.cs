using Pwiz.Analysis.Proteome;
using Pwiz.Data.Common.Proteome;

namespace Pwiz.Analysis.Tests.Proteome;

/// <summary>
/// Port of cpp <c>ProteinList_DecoyGeneratorTest</c>. Mirrors cpp's pattern: one entry
/// point (<see cref="DecoyGenerator"/>) that calls helpers for the reversed and shuffled
/// predicates, plus two sharp-only sub-tests covering shuffle determinism and the
/// prefix-stripping Find(id) lookup (cpp's test doesn't exercise either).
/// </summary>
[TestClass]
public class ProteinListDecoyGeneratorTests
{
    [TestMethod]
    public void DecoyGenerator()
    {
        var pl = BuildTinyList();
        TestReversedList(pl);
        TestShuffledList(pl);
        TestShuffleDeterminism(pl);
        TestFindByPrefix(pl);
    }

    // cpp's test uses examples::initializeTiny(pd) — a 3-protein fixture. We don't ship
    // a sharp ProteomeData.examples equivalent (it would be test-only fixture code),
    // so build the same shape inline.
    private static ProteinListSimple BuildTinyList() => new()
    {
        Proteins =
        {
            new Protein("Pro1", 0, "first",  "MKWVTFISLLFLFSSAY"),
            new Protein("Pro2", 1, "second", "VLSPADKTNVKAAWGKV"),
            new Protein("Pro3", 2, "third",  "VHLTPEEKSAVTALWGK"),
        },
    };

    // cpp testReversedList: prepends "reversed_" and reverses each sequence.
    private static void TestReversedList(ProteinList pl)
    {
        Assert.AreEqual(3, pl.Count, "reversed: source list size");
        var decoyList = new ProteinList_DecoyGenerator(pl, new DecoyGeneratorPredicate_Reversed("reversed_"));
        Assert.AreEqual(6, decoyList.Count, "reversed: doubled list size");
        for (int i = 0; i < pl.Count; i++)
        {
            var target = decoyList.GetProtein(i);
            var decoy = decoyList.GetProtein(i + pl.Count);
            Assert.AreEqual($"reversed_{target.Id}", decoy.Id, $"reversed[{i}].Id");
            Assert.AreEqual(string.Empty, decoy.Description, $"reversed[{i}].Description");
            Assert.AreEqual(Reverse(target.Sequence), decoy.Sequence, $"reversed[{i}].Sequence");
            Assert.AreEqual(i + pl.Count, decoy.Index, $"reversed[{i}].Index");
        }
    }

    // cpp testShuffledList: cpp asserts byte-for-byte mt19937(0) output. Sharp uses
    // System.Random (xoshiro on .NET 6+), so the bit-exact match doesn't carry over —
    // assert the multiset of residues is preserved and the length is unchanged.
    private static void TestShuffledList(ProteinList pl)
    {
        Assert.AreEqual(3, pl.Count, "shuffled: source list size");
        var decoyList = new ProteinList_DecoyGenerator(pl, new DecoyGeneratorPredicate_Shuffled("shuffled_", seed: 0));
        Assert.AreEqual(6, decoyList.Count, "shuffled: doubled list size");
        for (int i = 0; i < pl.Count; i++)
        {
            var target = decoyList.GetProtein(i);
            var decoy = decoyList.GetProtein(i + pl.Count);
            Assert.AreEqual($"shuffled_{target.Id}", decoy.Id, $"shuffled[{i}].Id");
            Assert.AreEqual(string.Empty, decoy.Description, $"shuffled[{i}].Description");
            Assert.AreEqual(target.Sequence.Length, decoy.Sequence.Length, $"shuffled[{i}].Sequence length");
            CollectionAssert.AreEquivalent(target.Sequence.ToCharArray(), decoy.Sequence.ToCharArray(),
                $"shuffled[{i}].Sequence must be a permutation of the target");
        }
    }

    // Sharp-only: confirm that two generators with the same seed produce identical output.
    // Important for reproducible search-database generation; cpp's mt19937 -> sharp's
    // xoshiro means cpp <-> sharp aren't bit-compatible, but seed -> output IS stable
    // within sharp itself.
    private static void TestShuffleDeterminism(ProteinList pl)
    {
        var a = new ProteinList_DecoyGenerator(pl, new DecoyGeneratorPredicate_Shuffled("d_", seed: 42));
        var b = new ProteinList_DecoyGenerator(pl, new DecoyGeneratorPredicate_Shuffled("d_", seed: 42));
        for (int i = 0; i < pl.Count; i++)
            Assert.AreEqual(a.GetProtein(i + pl.Count).Sequence, b.GetProtein(i + pl.Count).Sequence,
                $"seeded shuffle must be deterministic @ {i}");
    }

    // Sharp-only: Find(id) strips the decoy prefix and looks up the inner list, so
    // callers can pass either the target id or the decoy id.
    private static void TestFindByPrefix(ProteinList pl)
    {
        var decoyList = new ProteinList_DecoyGenerator(pl, new DecoyGeneratorPredicate_Reversed("rev_"));
        Assert.AreEqual(0, decoyList.Find("Pro1"), "find: target Pro1");
        Assert.AreEqual(2, decoyList.Find("Pro3"), "find: target Pro3");
        Assert.AreEqual(3, decoyList.Find("rev_Pro1"), "find: decoy rev_Pro1");
        Assert.AreEqual(5, decoyList.Find("rev_Pro3"), "find: decoy rev_Pro3");
        Assert.AreEqual(6, decoyList.Find("rev_Pro999"), "find: decoy miss → Count sentinel");
        Assert.AreEqual(6, decoyList.Find("nope"), "find: bare miss → Count sentinel");
    }

    private static string Reverse(string s)
    {
        var c = s.ToCharArray();
        Array.Reverse(c);
        return new string(c);
    }
}
