using Pwiz.Analysis.Proteome;
using Pwiz.Data.Common.Proteome;

namespace Pwiz.Analysis.Tests.Proteome;

/// <summary>
/// Port of cpp <c>ProteinListFactoryTest</c>. Mirrors cpp's pattern: one entry point
/// (<see cref="Factory"/>) calling sub-tests for usage, wrap, and (sharp-only) the
/// decoyGenerator command, id-from-file, error paths, and the multi-string overload.
/// </summary>
[TestClass]
public class ProteinListFactoryTests
{
    [TestMethod]
    public void Factory()
    {
        TestUsage();
        TestWrap();
        TestWrapDecoyGenerator();
        TestWrapIdFromFile();
        TestWrapErrors();
        TestWrapMultipleStrings();
    }

    private static ProteomeData NewPd(int count)
    {
        var pl = new ProteinListSimple();
        for (int i = 0; i < count; i++)
            pl.Proteins.Add(new Protein($"Pro{i + 1}", i, string.Empty, new string((char)('A' + i), 8)));
        return new ProteomeData { Id = "doc", ProteinList = pl };
    }

    // cpp testUsage: prints the help text. Sharp version: assert it mentions every
    // documented command so a reader of the failing test knows what's missing.
    private static void TestUsage()
    {
        string usage = ProteinListFactory.Usage();
        StringAssert.Contains(usage, "index", System.StringComparison.Ordinal);
        StringAssert.Contains(usage, "id", System.StringComparison.Ordinal);
        StringAssert.Contains(usage, "decoyGenerator", System.StringComparison.Ordinal);
        StringAssert.Contains(usage, "int_set", System.StringComparison.Ordinal);
    }

    // cpp testWrap: id-then-index. Uses initializeTiny which we replace with a 3-protein
    // inline fixture matching the cpp ids ("ZEBRA", "PRO1", "DEFCON42") so the cpp
    // assertions ("ZEBRA", "DEFCON42") translate one-for-one.
    private static void TestWrap()
    {
        // cpp examples::initializeTiny creates 3 proteins; sharp inlines them with the
        // same ids so the cpp asserts translate directly.
        var pd = new ProteomeData
        {
            ProteinList = new ProteinListSimple
            {
                Proteins =
                {
                    new Protein("ZEBRA",    0, string.Empty, "MKWV"),
                    new Protein("PRO1",     1, string.Empty, "VLSP"),
                    new Protein("DEFCON42", 2, string.Empty, "VHLT"),
                },
            },
        };
        Assert.AreEqual(3, pd.ProteinList!.Count, "wrap: initial count");

        // Keep two ids — order in the result is the iteration order through the source
        // list (ZEBRA at 0, DEFCON42 at 2), NOT the order of the id list argument.
        ProteinListFactory.Wrap(pd, "id DEFCON42;ZEBRA");
        Assert.AreEqual(2, pd.ProteinList.Count, "wrap: after id filter");
        Assert.AreEqual("ZEBRA",    pd.ProteinList.GetProtein(0).Id, "wrap: id[0]");
        Assert.AreEqual("DEFCON42", pd.ProteinList.GetProtein(1).Id, "wrap: id[1]");

        // Then keep ordinal 1 (now DEFCON42 in the 2-element filtered list).
        ProteinListFactory.Wrap(pd, "index 1");
        Assert.AreEqual(1, pd.ProteinList.Count, "wrap: after index filter");
        Assert.AreEqual("DEFCON42", pd.ProteinList.GetProtein(0).Id, "wrap: index[0]");
    }

    // Sharp-only: the cpp testWrap doesn't exercise the decoyGenerator command at all.
    private static void TestWrapDecoyGenerator()
    {
        // reverse: doubles the list with reversed sequences and the supplied prefix.
        var pdRev = NewPd(3);
        ProteinListFactory.Wrap(pdRev, "decoyGenerator reverse rev_");
        Assert.AreEqual(6, pdRev.ProteinList!.Count, "decoy.reverse: count");
        var target = pdRev.ProteinList.GetProtein(0);
        var decoy = pdRev.ProteinList.GetProtein(3);
        Assert.AreEqual($"rev_{target.Id}", decoy.Id, "decoy.reverse: id");
        Assert.AreEqual(new string(target.Sequence.Reverse().ToArray()), decoy.Sequence,
            "decoy.reverse: sequence");

        // shuffle=N seeds the RNG; identical seed -> identical output across runs.
        var pdShufA = NewPd(3);
        ProteinListFactory.Wrap(pdShufA, "decoyGenerator shuffle=42 sh_");
        var pdShufB = NewPd(3);
        ProteinListFactory.Wrap(pdShufB, "decoyGenerator shuffle=42 sh_");
        Assert.AreEqual(6, pdShufA.ProteinList!.Count, "decoy.shuffle: count");
        for (int i = 3; i < 6; i++)
            Assert.AreEqual(pdShufA.ProteinList.GetProtein(i).Sequence,
                            pdShufB.ProteinList!.GetProtein(i).Sequence,
                            $"decoy.shuffle: deterministic @ {i}");
    }

    // Sharp-only: the id command accepts a filepath whose lines are ids.
    private static void TestWrapIdFromFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"protein-ids-{System.Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "Pro1\nPro3\n");
        try
        {
            var pd = NewPd(5);
            ProteinListFactory.Wrap(pd, $"id {path}");
            Assert.AreEqual(2, pd.ProteinList!.Count, "id.file: count");
            Assert.AreEqual("Pro1", pd.ProteinList.GetProtein(0).Id, "id.file: [0]");
            Assert.AreEqual("Pro3", pd.ProteinList.GetProtein(1).Id, "id.file: [1]");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    // Sharp-only: error paths. cpp throws on these (user_error / runtime_error) but
    // doesn't have a unit test that asserts the throw; assert the throw here so
    // regressions surface at test time.
    private static void TestWrapErrors()
    {
        var pdMissingPrefix = NewPd(3);
        Assert.ThrowsException<ArgumentException>(() =>
            ProteinListFactory.Wrap(pdMissingPrefix, "decoyGenerator reverse"),
            "errors: missing decoy prefix must throw");

        var pdBadMode = NewPd(3);
        Assert.ThrowsException<ArgumentException>(() =>
            ProteinListFactory.Wrap(pdBadMode, "decoyGenerator badmode pre_"),
            "errors: invalid decoy mode must throw");

        // Unknown command: cpp warns to stderr and leaves the list untouched. Assert both.
        var pd = NewPd(5);
        var originalList = pd.ProteinList;
        var stderr = new StringWriter();
        var prev = Console.Error;
        Console.SetError(stderr);
        try { ProteinListFactory.Wrap(pd, "doesNotExist foo bar"); }
        finally { Console.SetError(prev); }
        Assert.AreSame(originalList, pd.ProteinList, "errors: unknown command must not replace the list");
        StringAssert.Contains(stderr.ToString(), "Ignoring wrapper", System.StringComparison.Ordinal);
    }

    // Sharp-only: the IEnumerable<string> overload — stack wrappers in order.
    private static void TestWrapMultipleStrings()
    {
        // Filter to indices 0..4, then generate reversed decoys → 5 originals + 5 decoys.
        var pd = NewPd(10);
        ProteinListFactory.Wrap(pd, new[] { "index 0-4", "decoyGenerator reverse rev_" });
        Assert.AreEqual(10, pd.ProteinList!.Count, "multi: count");
        Assert.AreEqual("Pro1",     pd.ProteinList.GetProtein(0).Id, "multi: target[0]");
        Assert.AreEqual("rev_Pro1", pd.ProteinList.GetProtein(5).Id, "multi: decoy[0]");
    }
}
