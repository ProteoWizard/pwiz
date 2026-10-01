using Pwiz.Data.Common.Obo;

namespace Pwiz.Data.Common.Tests.Obo;

[TestClass]
public class OboParserTests
{
    private const string SampleObo = """
        format-version: 1.2
        data-version: test-1.0
        default-namespace: MS

        [Term]
        id: MS:1000031
        name: instrument model
        def: "Instrument model name not including the vendor's name." [PSI:MS]
        synonym: "instrument_name" EXACT []
        is_a: MS:1000463 ! instrument

        [Term]
        id: MS:1000032
        name: customizable instrument
        def: "A customizable instrument." [PSI:MS]
        is_a: MS:1000031 ! instrument model
        is_obsolete: true

        [Term]
        id: MS:1001957
        name: (?<=[ALIV])(?\!P)
        def: "Regular expression for leukocyte elastase." [PSI:PI]
        synonym: "elastase \(cut after ALIV\)" EXACT []
        is_a: MS:1001180 ! Cleavage agent regular expression

        [Term]
        id: MS:1000040
        name: m/z
        def: "Three-character symbol." [PSI:MS]
        is_a: UO:0000000 ! unit
        is_a: MS:1000441 ! scan attribute
        relationship: has_units xsd:float
        relationship: has_order MS:1000042

        [Term]
        id: MS:1001152
        name: param: y ion-H2O DEPRECATED
        comment: This term was made obsolete - use MS:1001262 instead.

        [Typedef]
        id: has_units
        name: has_units
        """;

    [TestMethod]
    public void Parse_HeaderAndPrefixes()
    {
        var obo = ObOntology.Parse(new StringReader(SampleObo));
        Assert.IsTrue(obo.Header.Count >= 2, "header lines collected");
        CollectionAssert.Contains(obo.Header, "format-version: 1.2");
        Assert.IsTrue(obo.Prefixes.Contains("MS"), "MS prefix collected");
    }

    [TestMethod]
    public void Parse_TermsAndFields()
    {
        var obo = ObOntology.Parse(new StringReader(SampleObo));
        // [Typedef] stanzas don't become terms - only the five [Term] stanzas count.
        Assert.AreEqual(5, obo.Terms.Count, "only [Term] stanzas count");
        Assert.IsNotNull(obo.FindTerm("MS", 1000031));
        Assert.IsNotNull(obo.FindTerm("MS", 1000032));
        Assert.IsNull(obo.FindTerm("UO", 1000031), "prefix is part of the identity");

        // Term fields decoded from the keyed lines.
        var instrumentModel = obo.FindTerm("MS", 1000031)!;
        Assert.AreEqual("MS", instrumentModel.Prefix);
        Assert.AreEqual("instrument model", instrumentModel.Name);
        Assert.IsTrue(instrumentModel.Def.Contains("Instrument model", StringComparison.Ordinal));

        // OBO escapes of its own syntax characters are undone in names and synonyms, as cpp
        // obo.cpp does: "\!" would otherwise be a truncated name (the '!' starts a comment).
        var elastaseRegex = obo.FindTerm("MS", 1001957)!;
        Assert.AreEqual("(?<=[ALIV])(?!P)", elastaseRegex.Name);
        CollectionAssert.Contains(elastaseRegex.ExactSynonyms, "elastase (cut after ALIV)");
    }

    [TestMethod]
    public void Parse_RelationsAndFlags()
    {
        var obo = ObOntology.Parse(new StringReader(SampleObo));

        // is_a parent CVIDs and EXACT synonyms collected.
        var instrumentModel = obo.FindTerm("MS", 1000031)!;
        CollectionAssert.Contains(instrumentModel.ParentsIsA, 1000463u);
        CollectionAssert.Contains(instrumentModel.ExactSynonyms, "instrument_name");

        // is_obsolete:true honored; absent line defaults to false.
        Assert.IsFalse(instrumentModel.IsObsolete);
        Assert.IsTrue(obo.FindTerm("MS", 1000032)!.IsObsolete);
    }

    [TestMethod]
    public void Parse_IsAKeepsOnlySamePrefixParents()
    {
        // A parent id is stored bare and CvLookup re-homes it into the CHILD's value block, so
        // a cross-prefix parent would invent a relation rather than record one. cpp parse_is_a
        // rejects those outright; psi-ms.obo has five ("is_a: UO:0000000 ! unit" on the
        // unit-valued MS terms), and keeping them made CvIsA(MS_m_z, <the MS root>) true.
        var mz = ObOntology.Parse(new StringReader(SampleObo)).FindTerm("MS", 1000040)!;
        CollectionAssert.AreEqual(new[] { 1000441u }, mz.ParentsIsA.ToArray());
    }

    [TestMethod]
    public void Parse_SkipsXsdValueTypeRelationships()
    {
        // "xsd:float" names a value type, not a term. Letters in an accession are digits to
        // TryParseId (NCIT:C25330), so without an explicit guard psi-ms.obo's 1366
        // has_value_type lines turn into fabricated relations - xsd:float as term 61215120 -
        // for every spelling that happens to fit in a uint.
        var mz = ObOntology.Parse(new StringReader(SampleObo)).FindTerm("MS", 1000040)!;
        CollectionAssert.AreEqual(new[] { "has_order" }, mz.Relations.Select(r => r.Name).ToArray());
        Assert.AreEqual(1000042u, mz.Relations[0].TargetId);
    }

    [TestMethod]
    public void Parse_UsesCommentAsDefinitionAndHonorsItsObsoleteWording()
    {
        // cpp parse_comment_as_def: a term with no def: takes its comment as the definition,
        // and the word "obsolete" there marks it obsolete with no is_obsolete: line - which is
        // why cv.hpp calls MS:1001152 MS_param__y_ion_H2O_DEPRECATED_OBSOLETE. Reading the
        // comment as plain text left the C# side disagreeing with its own enum identifier.
        var deprecated = ObOntology.Parse(new StringReader(SampleObo)).FindTerm("MS", 1001152)!;
        Assert.IsTrue(deprecated.IsObsolete);
        StringAssert.Contains(deprecated.Def, "made obsolete");
    }

    [TestMethod]
    public void Parse_HeaderStopsAtTheFirstBlankLine()
    {
        // cpp parse() bounds the header at the first blank line, and the header is where the
        // "remark: namespace:" declarations that number the CVID value blocks come from. Lines
        // after the blank are not header, however tag-like they look.
        const string obo = """
            format-version: 1.2
            data-version: test-1.0

            saved-by: someone
            remark: namespace: LATE

            [Term]
            id: MS:1000001
            name: sample number
            def: "A sample." [PSI:MS]
            """;
        var parsed = ObOntology.Parse(new StringReader(obo));
        CollectionAssert.AreEqual(new[] { "format-version: 1.2", "data-version: test-1.0" }, parsed.Header);
        Assert.IsFalse(parsed.Prefixes.Contains("LATE"), "a namespace remark past the blank line is not header");
    }
}
