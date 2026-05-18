using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Proteome;
using Pwiz.Util.Proteome;

namespace Pwiz.Data.Common.Tests;

/// <summary>
/// Covers the proteome port (ProteomeData / FASTA I/O / Digestion / Diff / ProteinListCache).
/// Mirrors the spirit of pwiz cpp's <c>ProteomeDataTest</c>, <c>Serializer_FASTA_Test</c>,
/// and <c>DigestionTest</c>: tight per-feature methods with named asserts.
/// </summary>
[TestClass]
public class ProteomeTests
{
    // ---------------------------------------------------------------------------
    // ProteomeData / ProteinListSimple
    // ---------------------------------------------------------------------------

    [TestMethod]
    public void ProteinListSimple_FindAndFindKeyword_LinearDefaults()
    {
        var list = new ProteinListSimple();
        list.Proteins.Add(new Protein("P00001", 0, "Bovine serum albumin", "MKWVTFISLLL"));
        list.Proteins.Add(new Protein("P00002", 1, "Hemoglobin alpha chain", "VLSPADKTNV"));
        list.Proteins.Add(new Protein("P00003", 2, "Hemoglobin beta chain", "VHLTPEEKSAV"));

        Assert.AreEqual(3, list.Count);
        Assert.AreEqual(1, list.Find("P00002"));
        Assert.AreEqual(list.Count, list.Find("MISSING"), "not-found returns Count (cpp sentinel)");

        // Case-insensitive keyword search across descriptions.
        var hits = list.FindKeyword("hemoglobin", caseSensitive: false);
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, hits);
        Assert.AreEqual(0, list.FindKeyword("hemoglobin", caseSensitive: true).Count,
            "case-sensitive search misses lowercase needle");
    }

    [TestMethod]
    public void ProteomeData_IsEmpty_ReflectsListState()
    {
        var pd = new ProteomeData { Id = "doc1" };
        Assert.IsTrue(pd.IsEmpty, "no list → empty");
        pd.ProteinList = new ProteinListSimple();
        Assert.IsTrue(pd.IsEmpty, "list with 0 entries → empty");
        ((ProteinListSimple)pd.ProteinList).Proteins.Add(new Protein("P00001", 0, "x", "MK"));
        Assert.IsFalse(pd.IsEmpty);
    }

    // ---------------------------------------------------------------------------
    // FASTA round-trip
    // ---------------------------------------------------------------------------

    [TestMethod]
    public void Fasta_RoundTrips_ParsesIdAndDescriptionAndSequenceAcrossWrappedLines()
    {
        // Multi-line sequence (wrap at 12 chars), comment lines, and a trailing blank.
        const string fasta =
            ">P00001 albumin precursor\n" +
            "MKWVTFISLLL\n" +
            "FSSAYSRGVF\n" +
            "\n" +
            ">P00002 hemoglobin alpha\n" +
            "VLSPADKTNVKAAW\n";

        var pd = Fasta.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(fasta)));
        var list = (ProteinListSimple)pd.ProteinList!;
        Assert.AreEqual(2, list.Count);
        Assert.AreEqual("P00001", list.Proteins[0].Id);
        Assert.AreEqual("albumin precursor", list.Proteins[0].Description);
        Assert.AreEqual("MKWVTFISLLLFSSAYSRGVF", list.Proteins[0].Sequence,
            "wrapped sequence lines must concatenate without separators");
        Assert.AreEqual("P00002", list.Proteins[1].Id);
        Assert.AreEqual("hemoglobin alpha", list.Proteins[1].Description);

        // Write back to bytes and confirm the readback matches.
        using var ms = new MemoryStream();
        Fasta.Write(ms, pd);
        ms.Position = 0;
        var rt = Fasta.Read(ms);
        Assert.IsTrue(ProteomeDataDiff.IsEqual(pd, rt, out string reason),
            $"round-trip mismatch: {reason}");
    }

    [TestMethod]
    public void Fasta_RejectsDuplicateIds()
    {
        const string fasta =
            ">P00001 first\n" +
            "MKWV\n" +
            ">P00001 second\n" +
            "VLSP\n";
        Assert.ThrowsException<FormatException>(() =>
            Fasta.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(fasta))),
            "duplicate id should throw (cpp parity)");
    }

    [TestMethod]
    public void ProteomeDataFile_DetectsFastaBySniff_AndByExtension()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"proteome-{System.Guid.NewGuid():N}.fasta");
        File.WriteAllText(tmp, ">P1 desc\nMKWVTFI\n");
        try
        {
            var pd = ProteomeDataFile.Read(tmp);
            Assert.AreEqual(1, pd.ProteinList!.Count);

            // Round-trip via the format-selecting Write.
            string outPath = Path.ChangeExtension(tmp, ".out.fasta");
            ProteomeDataFile.Write(pd, outPath);
            try
            {
                var rt = ProteomeDataFile.Read(outPath);
                // pd.Id and rt.Id differ (each is derived from the on-disk filename); compare
                // the protein lists only.
                Assert.IsTrue(ProteomeDataDiff.IsEqual(pd.ProteinList!, rt.ProteinList!, out string reason),
                    reason);
            }
            finally { try { File.Delete(outPath); } catch { } }
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    // ---------------------------------------------------------------------------
    // Digestion
    // ---------------------------------------------------------------------------

    [TestMethod]
    public void Digestion_Trypsin_FullySpecific_NoMissedCleavages()
    {
        // Trypsin cuts after K or R unless followed by P. So MAKMKR|GHRP|K|GG splits at the
        // sites after K (pos 2), K (pos 4), R (pos 5), and R/P junction is suppressed by (?!P).
        var poly = new Peptide("MAKMKRGHRPKGG");
        var digestion = new Digestion(poly, CVID.MS_Trypsin,
            new DigestionConfig { MinimumLength = 1, MaximumLength = 50, MaximumMissedCleavages = 0 });
        var peptides = digestion.Enumerate().ToList();
        // Expected with 0 missed cleavages: cuts after K at index 2, K at index 4, R at index 5,
        // and after K at index 10. RP at index 7-8 is NOT a cut (the (?!P) suppresses it).
        // The polypeptide starts with M, so the clip-N-terminal-Met rule allows MAKMK
        // (which raw-counts as 1 missed cleavage) to be emitted at 0 missed cleavages
        // — biologically reasonable since initiator Met is cleaved post-translationally.
        CollectionAssert.AreEqual(
            new[] { "MAK", "MAKMK", "MK", "R", "GHRPK", "GG" },
            peptides.Select(p => p.Sequence).ToArray(),
            "trypsin fully-specific 0-missed-cleavage peptides (clip-N-term-Met active)");

        // Offset / specificity metadata on the GHRPK peptide.
        var ghrpk = peptides[4];
        Assert.AreEqual(6, ghrpk.Offset);
        Assert.AreEqual(0, ghrpk.MissedCleavages);
        Assert.IsTrue(ghrpk.NTerminusIsSpecific);
        Assert.IsTrue(ghrpk.CTerminusIsSpecific);
        Assert.AreEqual("R", ghrpk.NTerminusPrefix);
        Assert.AreEqual("G", ghrpk.CTerminusSuffix);
    }

    [TestMethod]
    public void Digestion_Trypsin_MissedCleavages_GrowsResultSet()
    {
        var poly = new Peptide("MAKMKRGHRPKGG");
        int countAt0 = new Digestion(poly, CVID.MS_Trypsin,
            new DigestionConfig { MaximumMissedCleavages = 0 }).Enumerate().Count();
        int countAt1 = new Digestion(poly, CVID.MS_Trypsin,
            new DigestionConfig { MaximumMissedCleavages = 1 }).Enumerate().Count();
        int countAt2 = new Digestion(poly, CVID.MS_Trypsin,
            new DigestionConfig { MaximumMissedCleavages = 2 }).Enumerate().Count();
        Assert.IsTrue(countAt1 > countAt0, "+1 missed cleavage adds peptides");
        Assert.IsTrue(countAt2 > countAt1, "+2 missed cleavage adds further peptides");
    }

    [TestMethod]
    public void Digestion_LengthFilter_DropsTooShortAndTooLong()
    {
        var poly = new Peptide("MAKMKRGHRPKGG");
        var peptides = new Digestion(poly, CVID.MS_Trypsin,
            new DigestionConfig { MinimumLength = 3, MaximumLength = 4, MaximumMissedCleavages = 0 })
            .Enumerate().ToList();
        foreach (var p in peptides)
            Assert.IsTrue(p.Sequence.Length is >= 3 and <= 4, $"length-violating peptide '{p.Sequence}'");
        Assert.IsTrue(peptides.Count > 0, "some peptides should survive the length filter");
    }

    [TestMethod]
    public void Digestion_CleavageAgent_NameAndRegexLookup()
    {
        Assert.AreEqual(CVID.MS_Trypsin, Digestion.GetCleavageAgentByName("Trypsin"));
        Assert.AreEqual(CVID.MS_Trypsin, Digestion.GetCleavageAgentByName("trypsin"),
            "name lookup is case-insensitive");
        Assert.AreEqual(CVID.MS_Trypsin_P, Digestion.GetCleavageAgentByName("Trypsin/P"));
        Assert.AreEqual(CVID.MS_Lys_C, Digestion.GetCleavageAgentByName("Lys-C"));
        Assert.AreEqual(CVID.CVID_Unknown, Digestion.GetCleavageAgentByName("NotAnEnzyme"));

        Assert.AreEqual(@"(?<=[KR])(?!P)", Digestion.GetCleavageAgentRegex(CVID.MS_Trypsin));
        Assert.AreEqual(CVID.MS_Trypsin,
            Digestion.GetCleavageAgentByRegex(@"(?<=[KR])(?!P)"),
            "regex round-trip lookup");
    }

    // ---------------------------------------------------------------------------
    // ProteinListCache
    // ---------------------------------------------------------------------------

    [TestMethod]
    public void ProteinListCache_HitsAfterFirstLookup_AndEvictsBeyondCapacity()
    {
        var inner = new ProteinListSimple();
        for (int i = 0; i < 5; i++) inner.Proteins.Add(new Protein($"P{i}", i, "desc", "MKWV"));
        var cache = new ProteinListCache(inner, ProteinListCacheMode.MetaDataAndSequence, cacheSize: 2);

        _ = cache.GetProtein(0);
        _ = cache.GetProtein(1);
        Assert.AreEqual(2, cache.CacheCount);

        // Re-access 0 → still in cache.
        _ = cache.GetProtein(0);
        Assert.AreEqual(2, cache.CacheCount);

        // Add 2, 3 — should evict 1 then 2 (MRU keeps 0 since we just touched it, plus the
        // newest). After 4 accesses the cache holds the 2 most recently touched.
        _ = cache.GetProtein(2);
        _ = cache.GetProtein(3);
        Assert.AreEqual(2, cache.CacheCount);

        // Off mode disables caching: GetProtein hits Inner every time.
        cache.Mode = ProteinListCacheMode.Off;
        Assert.AreEqual(0, cache.CacheCount, "mode change clears cache");
        _ = cache.GetProtein(0);
        Assert.AreEqual(0, cache.CacheCount, "Off mode doesn't cache");
    }

    // ---------------------------------------------------------------------------
    // Diff
    // ---------------------------------------------------------------------------

    [TestMethod]
    public void Diff_DetectsSequenceAndDescriptionMismatches()
    {
        var a = new ProteomeData { Id = "doc" };
        var aList = new ProteinListSimple();
        aList.Proteins.Add(new Protein("P1", 0, "first protein", "MKWV"));
        aList.Proteins.Add(new Protein("P2", 1, "second protein", "VLSP"));
        a.ProteinList = aList;

        var b = new ProteomeData { Id = "doc" };
        var bList = new ProteinListSimple();
        bList.Proteins.Add(new Protein("P1", 0, "first protein", "MKWV"));
        bList.Proteins.Add(new Protein("P2", 1, "second protein", "VLSP"));
        b.ProteinList = bList;

        Assert.IsTrue(ProteomeDataDiff.IsEqual(a, b, out string _), "identical docs match");

        // Description differs.
        bList.Proteins[1] = new Protein("P2", 1, "DIFFERENT", "VLSP");
        Assert.IsFalse(ProteomeDataDiff.IsEqual(a, b, out string reasonDesc));
        StringAssert.Contains(reasonDesc, "description", StringComparison.Ordinal);

        // Same docs ignoring metadata → match.
        Assert.IsTrue(ProteomeDataDiff.IsEqual(a, b, out _, ignoreMetadata: true));

        // Sequence diff is reported even with ignoreMetadata.
        bList.Proteins[1] = new Protein("P2", 1, "second protein", "VVVV");
        Assert.IsFalse(ProteomeDataDiff.IsEqual(a, b, out string reasonSeq, ignoreMetadata: true));
        StringAssert.Contains(reasonSeq, "sequence", StringComparison.Ordinal);
    }
}
