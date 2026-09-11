using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Index;
using Pwiz.Data.Common.Proteome;
using Pwiz.Util.Proteome;

namespace Pwiz.Data.Common.Tests;

/// <summary>
/// Covers the proteome port (ProteomeData / FASTA I/O / Digestion / Diff / ProteinListCache,
/// plus the lazy <see cref="Fasta.OpenLazy"/> / <see cref="FastaProteinList"/> path and its
/// .index sidecar). Mirrors the spirit of pwiz cpp's <c>ProteomeDataTest</c>,
/// <c>Serializer_FASTA_Test</c>, and <c>DigestionTest</c>.
/// </summary>
[TestClass]
public class ProteomeTests
{
    private static string WriteSampleFasta(int count)
    {
        // Variable-length sequences across multiple wrapped lines, so byte offsets aren't
        // a multiple of any single record stride.
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < count; i++)
        {
            sb.Append('>').Append('P').Append(i.ToString("D5")).Append(" description for protein ").Append(i).Append('\n');
            // Sequence length = 50 + (i * 7) % 80 chars, wrapped at 60.
            int n = 50 + (i * 7) % 80;
            var seq = new char[n];
            for (int j = 0; j < n; j++) seq[j] = "ACDEFGHIKLMNPQRSTVWY"[(i + j) % 20];
            for (int off = 0; off < n; off += 60)
            {
                int len = System.Math.Min(60, n - off);
                sb.Append(new string(seq, off, len)).Append('\n');
            }
        }
        string path = Path.Combine(Path.GetTempPath(), $"fasta-lazy-{System.Guid.NewGuid():N}.fasta");
        File.WriteAllText(path, sb.ToString());
        return path;
    }

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
        // Cuts after K at index 2, K at index 4, R at index 5, and K at index 10. RP at index
        // 7-8 is NOT a cut (the (?!P) suppresses it). The polypeptide starts with M, so
        // clip-N-terminal-Met adds offset 0 as a real cleavage site - which is what makes "M"
        // and "AK" fully-specific products, and makes MAKMK span one missed cleavage (sites 0
        // and 2) so it is excluded at MaximumMissedCleavages = 0.
        // Verified against cpp: Digestion(MAKMKRGHRPKGG, MS_Trypsin, Config(0, 1, 50)).
        CollectionAssert.AreEqual(
            new[] { "M", "MAK", "AK", "MK", "R", "GHRPK", "GG" },
            peptides.Select(p => p.Sequence).ToArray(),
            "trypsin fully-specific 0-missed-cleavage peptides (clip-N-term-Met active)");
        foreach (var p in peptides)
        {
            Assert.AreEqual(0, p.MissedCleavages, $"{p.Sequence}: missed cleavages");
            Assert.AreEqual(2, p.SpecificTermini, $"{p.Sequence}: specific termini");
        }

        // Offset / specificity metadata on the GHRPK peptide.
        var ghrpk = peptides[5];
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

    /// <summary>
    /// Semi- and non-specific digestion, ported from cpp's <c>testBSADigestion</c>
    /// (DigestionTest.cpp:481) and the three scenario functions it drives. Until this test
    /// existed, nothing in pwiz-sharp or the Skyline tree ever built a <see cref="Digestion"/>
    /// with anything but <see cref="Specificity.FullySpecific"/>, so the entire
    /// semi/non-specific enumeration path had never run.
    ///
    /// Each scenario runs over both cpp forms - the predefined cleavage agent and the
    /// equivalent hand-written regex - because they take different paths to the same site set.
    /// </summary>
    [TestMethod]
    public void Digestion_SemiAndNonSpecific_BSA()
    {
        var bsa = new Peptide(BSA);

        // cpp Config(maximumMissedCleavages, minimumLength, maximumLength, minimumSpecificity).
        var semi = new DigestionConfig
        {
            MaximumMissedCleavages = 1, MinimumLength = 5, MaximumLength = 20,
            MinimumSpecificity = Specificity.SemiSpecific,
        };
        var nonSpecific = semi with { MinimumSpecificity = Specificity.NonSpecific };

        AssertSemitrypticBsa(new Digestion(bsa, CVID.MS_Trypsin_P, semi).Enumerate().ToList());
        AssertSemitrypticBsa(new Digestion(bsa, "(?<=[KR])", semi).Enumerate().ToList());

        AssertNontrypticBsa(new Digestion(bsa, CVID.MS_Trypsin_P, nonSpecific).Enumerate().ToList());
        AssertNontrypticBsa(new Digestion(bsa, "(?<=[KR])", nonSpecific).Enumerate().ToList());

        // N-terminal methionine clipping, expressed as an extra cut site after a leading M.
        // Regex-only in cpp: no predefined agent encodes it.
        AssertSemitrypticMethionineClippingBsa(
            new Digestion(bsa, "(?<=^M)|(?<=[KR])", semi).Enumerate().ToList());
        AssertSemitrypticMethionineClippingBsa(
            new Digestion(bsa, "(?<=(^M)|([KR]))", semi).Enumerate().ToList());
    }

    /// <summary>Port of cpp <c>testSemitrypticBSA</c> (DigestionTest.cpp:200).</summary>
    private static void AssertSemitrypticBsa(List<DigestedPeptide> peptides)
    {
        Assert.IsTrue(peptides.Count > 3, "semi-specific digest produced too few peptides");

        // Enumeration order at the N terminus.
        Assert.AreEqual("MKWVT", peptides[0].Sequence);
        Assert.AreEqual("MKWVTF", peptides[1].Sequence);
        Assert.AreEqual("MKWVTFI", peptides[2].Sequence);

        // Enumeration order at the C terminus (cpp indexes these off rbegin()).
        AssertFromEnd(peptides, 0, "QTALA");
        AssertFromEnd(peptides, 1, "TQTALA");
        AssertFromEnd(peptides, 2, "STQTALA");
        AssertFromEnd(peptides, 5, "LVVSTQTALA");
        AssertFromEnd(peptides, 6, "LVVSTQTAL");
        AssertFromEnd(peptides, 10, "LVVST");

        AssertMetadata(peptides[0], offset: 0, missedCleavages: 1, nSpecific: true, cSpecific: false);

        AssertMetadata(Find(peptides, "MKWVTFISLLLLFSSAYSR"), offset: 0, missedCleavages: 1, nSpecific: true, cSpecific: true);
        AssertMetadata(Find(peptides, "KWVTFISLLLLFSSAYSR"), offset: 1, missedCleavages: 1, nSpecific: true, cSpecific: true);
        AssertMetadata(Find(peptides, "WVTFISLLLLFSSAYSR"), offset: 2, missedCleavages: 0, nSpecific: true, cSpecific: true);
        AssertMetadata(Find(peptides, "WVTFISLLLLFSSAYSRG"), offset: 2, missedCleavages: 1, nSpecific: true, cSpecific: false);

        AssertAbsent(peptides, "KWVTFISLLLLFSSAYSRG");  // 2 missed cleavages
        AssertAbsent(peptides, "VTFISLLLLFSSAYSRG");     // non-tryptic

        // Specificity boundary: tryptic and semi-tryptic in, non-tryptic out.
        AssertPresent(peptides, "WVTFISLLLLFSSAYSR");    // tryptic
        AssertPresent(peptides, "VTFISLLLLFSSAYSR");     // semi-tryptic
        AssertAbsent(peptides, "VTFISLLLLFSSAYS");       // non-tryptic

        // Same boundary at the C terminus.
        AssertPresent(peptides, "FAVEGPKLVVSTQTALA");    // semi-tryptic
        AssertAbsent(peptides, "FAVEGPKLVVSTQTAL");      // non-tryptic
    }

    /// <summary>Port of cpp <c>testNontrypticBSA</c> (DigestionTest.cpp:290).</summary>
    private static void AssertNontrypticBsa(List<DigestedPeptide> peptides)
    {
        Assert.IsTrue(peptides.Count > 3, "non-specific digest produced too few peptides");

        Assert.AreEqual("MKWVT", peptides[0].Sequence);
        Assert.AreEqual("MKWVTF", peptides[1].Sequence);
        Assert.AreEqual("MKWVTFI", peptides[2].Sequence);

        AssertMetadata(peptides[0], offset: 0, missedCleavages: 1, nSpecific: true, cSpecific: false);

        AssertMetadata(Find(peptides, "MKWVTFISLLLLFSSAYSR"), offset: 0, missedCleavages: 1, nSpecific: true, cSpecific: true);
        AssertMetadata(Find(peptides, "KWVTFISLLLLFSSAYSR"), offset: 1, missedCleavages: 1, nSpecific: true, cSpecific: true);
        AssertMetadata(Find(peptides, "WVTFISLLLLFSSAYSR"), offset: 2, missedCleavages: 0, nSpecific: true, cSpecific: true);
        AssertMetadata(Find(peptides, "WVTFISLLLLFSSAYSRG"), offset: 2, missedCleavages: 1, nSpecific: true, cSpecific: false);
        // The one the semi-specific digest rejects: neither terminus at a cut site.
        AssertMetadata(Find(peptides, "VTFISLLLLFSSAYSRG"), offset: 3, missedCleavages: 1, nSpecific: false, cSpecific: false);

        AssertAbsent(peptides, "KWVTFISLLLLFSSAYSRG");   // 2 missed cleavages

        // All three specificities are admitted here.
        AssertPresent(peptides, "WVTFISLLLLFSSAYSR");    // tryptic
        AssertPresent(peptides, "VTFISLLLLFSSAYSR");     // semi-tryptic
        AssertPresent(peptides, "VTFISLLLLFSSAYS");      // non-tryptic

        AssertPresent(peptides, "FAVEGPKLVVSTQTALA");    // semi-tryptic
        AssertPresent(peptides, "FAVEGPKLVVSTQTAL");     // non-tryptic
        Assert.AreEqual("QTALA", peptides[^1].Sequence, "last peptide enumerated");

        // Maximum missed cleavages still applies.
        AssertPresent(peptides, "KWVTFISLLLLFSSAYSR");
        AssertAbsent(peptides, "KWVTFISLLLLFSSAYSRG");

        // Length bounds still apply.
        AssertAbsent(peptides, "LR");                    // below MinimumLength
        AssertAbsent(peptides, "QRLR");                  // below MinimumLength
        AssertPresent(peptides, "VLASSAR");
        AssertAbsent(peptides, "EYEATLEECCAKDDPHACYSTVFDK"); // above MaximumLength
    }

    /// <summary>Port of cpp <c>testSemitrypticMethionineClippingBSA</c>
    /// (DigestionTest.cpp:390).</summary>
    private static void AssertSemitrypticMethionineClippingBsa(List<DigestedPeptide> peptides)
    {
        Assert.IsTrue(peptides.Count > 3, "methionine-clipping digest produced too few peptides");

        // Even with the extra cut after the leading M, MKWVT still spans one missed cleavage.
        Assert.AreEqual("MKWVT", peptides[0].Sequence);
        Assert.AreEqual("MKWVTF", peptides[1].Sequence);
        Assert.AreEqual("MKWVTFI", peptides[2].Sequence);

        AssertFromEnd(peptides, 0, "QTALA");
        AssertFromEnd(peptides, 1, "TQTALA");
        AssertFromEnd(peptides, 2, "STQTALA");
        AssertFromEnd(peptides, 5, "LVVSTQTALA");
        AssertFromEnd(peptides, 6, "LVVSTQTAL");
        AssertFromEnd(peptides, 10, "LVVST");

        AssertMetadata(peptides[0], offset: 0, missedCleavages: 1, nSpecific: true, cSpecific: false);

        // Clipped-methionine peptides: the N terminus is specific because of the ^M cut site.
        var clippedSemi = Find(peptides, "KWVTFISLLLLFSSAYS");
        AssertMetadata(clippedSemi, offset: 1, missedCleavages: 1, nSpecific: true, cSpecific: false);

        AssertMetadata(Find(peptides, "KWVTFISLLLLFSSAYSR"), offset: 1, missedCleavages: 1, nSpecific: true, cSpecific: true);
        AssertMetadata(Find(peptides, "WVTFISLLLLFSSAYSR"), offset: 2, missedCleavages: 0, nSpecific: true, cSpecific: true);
        AssertMetadata(Find(peptides, "WVTFISLLLLFSSAYSRG"), offset: 2, missedCleavages: 1, nSpecific: true, cSpecific: false);

        AssertAbsent(peptides, "KWVTFISLLLLFSSAYSRG");   // 2 missed cleavages
        AssertAbsent(peptides, "VTFISLLLLFSSAYSRG");     // non-tryptic

        AssertPresent(peptides, "WVTFISLLLLFSSAYSR");    // tryptic
        AssertPresent(peptides, "KWVTFISLLLLFSSAYSR");   // semi-tryptic
        AssertPresent(peptides, "KWVTFISLLLLFSSAYS");    // clipped methionine + semi-specific
        AssertAbsent(peptides, "VTFISLLLLFSSAYS");       // non-specific

        AssertPresent(peptides, "FAVEGPKLVVSTQTALA");    // semi-tryptic
        AssertAbsent(peptides, "FAVEGPKLVVSTQTAL");      // non-tryptic
    }

    // cpp looks peptides up in a std::set ordered by sequence, which keeps the first of any
    // duplicates - so "first in enumeration order" is the matching C# semantic.
    private static DigestedPeptide Find(List<DigestedPeptide> peptides, string sequence)
    {
        var hit = peptides.FirstOrDefault(p => p.Sequence == sequence);
        Assert.IsNotNull(hit, $"expected peptide '{sequence}' was not produced");
        return hit;
    }

    private static void AssertPresent(List<DigestedPeptide> peptides, string sequence) =>
        Assert.IsTrue(peptides.Any(p => p.Sequence == sequence),
            $"expected peptide '{sequence}' was not produced");

    private static void AssertAbsent(List<DigestedPeptide> peptides, string sequence) =>
        Assert.IsFalse(peptides.Any(p => p.Sequence == sequence),
            $"peptide '{sequence}' should have been filtered out");

    private static void AssertFromEnd(List<DigestedPeptide> peptides, int fromEnd, string expected) =>
        Assert.AreEqual(expected, peptides[peptides.Count - 1 - fromEnd].Sequence,
            $"peptide {fromEnd} back from the end");

    private static void AssertMetadata(DigestedPeptide peptide, int offset, int missedCleavages,
        bool nSpecific, bool cSpecific)
    {
        Assert.AreEqual(offset, peptide.Offset, $"{peptide.Sequence}: offset");
        Assert.AreEqual(missedCleavages, peptide.MissedCleavages, $"{peptide.Sequence}: missed cleavages");
        Assert.AreEqual(nSpecific, peptide.NTerminusIsSpecific, $"{peptide.Sequence}: N-terminus specificity");
        Assert.AreEqual(cSpecific, peptide.CTerminusIsSpecific, $"{peptide.Sequence}: C-terminus specificity");
        Assert.AreEqual((nSpecific ? 1 : 0) + (cSpecific ? 1 : 0), peptide.SpecificTermini,
            $"{peptide.Sequence}: specific termini");
    }

    // >P02769|ALBU_BOVIN Serum albumin - Bos taurus (Bovine). Verbatim from
    // cpp DigestionTest.cpp:486.
    private const string BSA =
        "MKWVTFISLLLLFSSAYSRGVFRRDTHKSEIAHRFKDLGEEHFKGLVLIAFSQYLQQCPF" +
        "DEHVKLVNELTEFAKTCVADESHAGCEKSLHTLFGDELCKVASLRETYGDMADCCEKQEP" +
        "ERNECFLSHKDDSPDLPKLKPDPNTLCDEFKADEKKFWGKYLYEIARRHPYFYAPELLYY" +
        "ANKYNGVFQECCQAEDKGACLLPKIETMREKVLASSARQRLRCASIQKFGERALKAWSVA" +
        "RLSQKFPKAEFVEVTKLVTDLTKVHKECCHGDLLECADDRADLAKYICDNQDTISSKLKE" +
        "CCDKPLLEKSHCIAEVEKDAIPENLPPLTADFAEDKDVCKNYQEAKDAFLGSFLYEYSRR" +
        "HPEYAVSVLLRLAKEYEATLEECCAKDDPHACYSTVFDKLKHLVDEPQNLIKQNCDQFEK" +
        "LGEYGFQNALIVRYTRKVPQVSTPTLVEVSRSLGKVGTRCCTKPESERMPCTEDYLSLIL" +
        "NRLCVLHEKTPVSEKVTKCCTESLVNRRPCFSALTPDETYVPKAFDEKLFTFHADICTLP" +
        "DTEKQIKKQTALVELLKHKPKATEEQLKTVMENFVAFVDKCCAADDKEACFAVEGPKLVV" +
        "STQTALA";

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

    // ---------------------------------------------------------------------------
    // Lazy FASTA reader (Fasta.OpenLazy / FastaProteinList + .index sidecar)
    // ---------------------------------------------------------------------------

    [TestMethod]
    public void OpenLazy_GetProteinByIndex_ReturnsCorrectRecord()
    {
        string path = WriteSampleFasta(50);
        try
        {
            // Eager read for a reference: gives us the canonical answers to compare against.
            var eager = Fasta.ReadFile(path);
            using var pd = Fasta.OpenLazy(path);
            var lazyList = (FastaProteinList)pd.ProteinList!;

            Assert.AreEqual(eager.ProteinList!.Count, lazyList.Count, "Count must match eager read");

            // Probe a few indices out of order to exercise the seek path.
            foreach (int i in new[] { 0, 25, 49, 7, 33, 1 })
            {
                var lazyP = lazyList.GetProtein(i, getSequence: true);
                var eagerP = eager.ProteinList.GetProtein(i, getSequence: true);
                Assert.AreEqual(eagerP.Id, lazyP.Id, $"Id[{i}]");
                Assert.AreEqual(eagerP.Description, lazyP.Description, $"Description[{i}]");
                Assert.AreEqual(eagerP.Sequence, lazyP.Sequence, $"Sequence[{i}]");
                Assert.AreEqual(i, lazyP.Index);
            }

            // getSequence: false drops the sequence load (metadata-only path).
            var metaOnly = lazyList.GetProtein(10, getSequence: false);
            Assert.AreEqual("P00010", metaOnly.Id);
            Assert.AreEqual(string.Empty, metaOnly.Sequence,
                "getSequence=false should return an empty sequence");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [TestMethod]
    public void OpenLazy_FindById_UsesIndexNoFullScan()
    {
        string path = WriteSampleFasta(20);
        try
        {
            using var pd = Fasta.OpenLazy(path);
            var list = pd.ProteinList!;
            Assert.AreEqual(0, list.Find("P00000"));
            Assert.AreEqual(15, list.Find("P00015"));
            Assert.AreEqual(list.Count, list.Find("does-not-exist"),
                "missing-id sentinel must be Count (cpp parity)");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [TestMethod]
    public void OpenLazy_DiskIndex_PersistsAcrossOpens()
    {
        string path = WriteSampleFasta(15);
        string sidecar = path + ".index";
        try
        {
            // First open: builds + persists the sidecar.
            using (var pd1 = Fasta.OpenLazy(path, useDiskIndex: true))
            {
                Assert.AreEqual(15, pd1.ProteinList!.Count);
            }
            Assert.IsTrue(File.Exists(sidecar), "disk index sidecar should have been written");
            long sidecarSizeAfterFirst = new FileInfo(sidecar).Length;
            Assert.IsTrue(sidecarSizeAfterFirst > 0);

            // Second open: should reuse the sidecar (no rebuild). We can't directly observe
            // "no rebuild" through public API, but we can verify the sidecar wasn't truncated
            // and the contents are still readable.
            using (var pd2 = Fasta.OpenLazy(path, useDiskIndex: true))
            {
                Assert.AreEqual(15, pd2.ProteinList!.Count);
                var p7 = pd2.ProteinList.GetProtein(7);
                Assert.AreEqual("P00007", p7.Id);
            }
            Assert.AreEqual(sidecarSizeAfterFirst, new FileInfo(sidecar).Length,
                "sidecar size should be unchanged on the second open");
        }
        finally
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(sidecar); } catch { }
        }
    }

    [TestMethod]
    public void OpenLazy_DiskIndex_HasCppCompatibleFormat()
    {
        // cpp-format sidecar invariants the sharp writer must satisfy so that
        // a cpp pwiz binary can read sharp-written .index files (and vice versa):
        //   * 48-byte prelude = int64 file size + 40-byte lowercase-hex SHA-1
        //   * stored ids are 40-char SHA-1 hex of the raw FASTA id (cpp Serializer_FASTA.cpp:100)
        //   * maxIdLength = 41 (40 hex chars + 1 space terminator)
        string path = WriteSampleFasta(5);
        string sidecar = path + ".index";
        try
        {
            using (var pd = Fasta.OpenLazy(path, useDiskIndex: true))
            {
                Assert.AreEqual(5, pd.ProteinList!.Count);
            }
            // Reopen the sidecar directly to inspect the prelude and maxIdLength.
            using var fs = new FileStream(sidecar, FileMode.Open, FileAccess.Read, FileShare.Read);
            var idx = new BinaryIndexStream(fs, leaveOpen: false);

            Assert.AreEqual(new FileInfo(path).Length, idx.SourceFileSize, "file size in prelude");
            Assert.AreEqual(40, idx.SourceFileSha1Hex.Length, "SHA-1 hex must be 40 lowercase hex chars");
            foreach (char c in idx.SourceFileSha1Hex)
                Assert.IsTrue((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'),
                    $"SHA-1 hex has non-hex/non-lowercase char '{c}'");

            // The stored id (raw key inside an entry) must be the SHA-1 hex of the FASTA id.
            // We can't read entries directly here, but Find(hashOfId) must produce the right ordinal
            // since cpp's lookup path does exactly `index.find(sha1.hash(id))`.
            string expected = FastaProteinList.HashId("P00003");
            Assert.AreEqual(40, expected.Length);
            var hit = idx.Find(expected);
            Assert.IsNotNull(hit, "BinaryIndexStream must contain the hashed id, not the raw id");
            Assert.AreEqual(3UL, hit!.Index);

            // And the raw id must NOT be present in the sidecar.
            Assert.IsNull(idx.Find("P00003"),
                "Raw ids must not be stored — only their SHA-1 hashes (cpp compat).");
        }
        finally
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(sidecar); } catch { }
        }
    }

    [TestMethod]
    public void OpenLazy_DiskIndex_RebuildsWhenFastaSizeChanged()
    {
        // If the FASTA file grows / shrinks between opens, the cached sidecar must be
        // discarded and rebuilt (otherwise byte offsets are stale).
        string path = WriteSampleFasta(5);
        string sidecar = path + ".index";
        try
        {
            using (var pd1 = Fasta.OpenLazy(path, useDiskIndex: true))
                Assert.AreEqual(5, pd1.ProteinList!.Count);

            // Append another protein to the FASTA — this changes both size and SHA-1.
            using (var w = new StreamWriter(path, append: true))
            {
                w.NewLine = "\n";
                w.WriteLine(">P99999 freshly appended");
                w.WriteLine("ACDEFGHIKLMNPQRSTVWY");
            }

            using var pd2 = Fasta.OpenLazy(path, useDiskIndex: true);
            Assert.AreEqual(6, pd2.ProteinList!.Count, "sidecar must rebuild — new protein should be visible");
            Assert.AreEqual(5, pd2.ProteinList.Find("P99999"), "newly appended id must be findable");
        }
        finally
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(sidecar); } catch { }
        }
    }

    [TestMethod]
    public void OpenLazy_Dispose_ReleasesFileHandle()
    {
        string path = WriteSampleFasta(3);
        try
        {
            var pd = Fasta.OpenLazy(path);
            ((FastaProteinList)pd.ProteinList!).Dispose();
            // After dispose, the file must be deletable — no lingering FileStream.
            File.Delete(path);
            Assert.IsFalse(File.Exists(path));
        }
        catch
        {
            try { File.Delete(path); } catch { }
            throw;
        }
    }
}
