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
        // [Typedef] stanzas don't become terms — only the two [Term] stanzas count.
        Assert.AreEqual(2, obo.Terms.Count, "only [Term] stanzas count");
        Assert.IsTrue(obo.Terms.ContainsKey(1000031));
        Assert.IsTrue(obo.Terms.ContainsKey(1000032));

        // Term fields decoded from the keyed lines.
        var instrumentModel = obo.Terms[1000031];
        Assert.AreEqual("MS", instrumentModel.Prefix);
        Assert.AreEqual("instrument model", instrumentModel.Name);
        Assert.IsTrue(instrumentModel.Def.Contains("Instrument model", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Parse_RelationsAndFlags()
    {
        var obo = ObOntology.Parse(new StringReader(SampleObo));

        // is_a parent CVIDs and EXACT synonyms collected.
        var instrumentModel = obo.Terms[1000031];
        CollectionAssert.Contains(instrumentModel.ParentsIsA, 1000463u);
        CollectionAssert.Contains(instrumentModel.ExactSynonyms, "instrument_name");

        // is_obsolete:true honored; absent line defaults to false.
        Assert.IsFalse(instrumentModel.IsObsolete);
        Assert.IsTrue(obo.Terms[1000032].IsObsolete);
    }
}
