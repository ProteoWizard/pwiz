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
    public void Parse_ReadsHeader()
    {
        var obo = ObOntology.Parse(new StringReader(SampleObo));
        Assert.IsTrue(obo.Header.Count >= 2);
        CollectionAssert.Contains(obo.Header, "format-version: 1.2");
    }

    [TestMethod]
    public void Parse_CollectsPrefixes()
    {
        var obo = ObOntology.Parse(new StringReader(SampleObo));
        Assert.IsTrue(obo.Prefixes.Contains("MS"));
    }

    [TestMethod]
    public void Parse_ReadsTerms()
    {
        var obo = ObOntology.Parse(new StringReader(SampleObo));
        Assert.AreEqual(2, obo.Terms.Count);
        Assert.IsTrue(obo.Terms.ContainsKey(1000031));
        Assert.IsTrue(obo.Terms.ContainsKey(1000032));
    }

    [TestMethod]
    public void Parse_ExtractsTermFields()
    {
        var obo = ObOntology.Parse(new StringReader(SampleObo));
        var term = obo.Terms[1000031];
        Assert.AreEqual("MS", term.Prefix);
        Assert.AreEqual("instrument model", term.Name);
        Assert.IsTrue(term.Def.Contains("Instrument model", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Parse_CapturesIsAParents()
    {
        var obo = ObOntology.Parse(new StringReader(SampleObo));
        var term = obo.Terms[1000031];
        CollectionAssert.Contains(term.ParentsIsA, 1000463u);
    }

    [TestMethod]
    public void Parse_CapturesExactSynonyms()
    {
        var obo = ObOntology.Parse(new StringReader(SampleObo));
        var term = obo.Terms[1000031];
        CollectionAssert.Contains(term.ExactSynonyms, "instrument_name");
    }

    [TestMethod]
    public void Parse_DetectsObsoleteFlag()
    {
        var obo = ObOntology.Parse(new StringReader(SampleObo));
        Assert.IsFalse(obo.Terms[1000031].IsObsolete);
        Assert.IsTrue(obo.Terms[1000032].IsObsolete);
    }

    [TestMethod]
    public void Parse_SkipsNonTermStanzas()
    {
        // Typedef stanza should not become a term.
        var obo = ObOntology.Parse(new StringReader(SampleObo));
        Assert.AreEqual(2, obo.Terms.Count);
    }
}
