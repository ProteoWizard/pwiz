using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.Common.Tests.Cv;

[TestClass]
public class CVTranslatorTests
{
    [TestMethod]
    public void Translate_ExactNameAndCaseAndWhitespace()
    {
        // Exact display-name match, plus case-insensitive and whitespace-tolerant lookup.
        var t = new CVTranslator();
        Assert.AreEqual(CVID.MS_software, t.Translate("software"), "exact");
        Assert.AreEqual(CVID.MS_software, t.Translate("SOFTWARE"), "uppercase");
        Assert.AreEqual(CVID.MS_software, t.Translate("Software"), "title case");
        Assert.AreEqual(CVID.MS_software, t.Translate("  software  "), "leading/trailing whitespace");
    }

    [TestMethod]
    public void Translate_DefaultAliases_AndUnknown()
    {
        // Hard-coded built-in aliases for legacy mzXML-style instrument tokens.
        var t = new CVTranslator();
        Assert.AreEqual(CVID.MS_ion_trap, t.Translate("ITMS"));
        Assert.AreEqual(CVID.MS_FT_ICR, t.Translate("FTMS"));

        // Unknown terms return the sentinel rather than throwing.
        Assert.AreEqual(CVID.CVID_Unknown, t.Translate("this is not a CV term"));
    }

    [TestMethod]
    public void Insert_CustomAlias_AvailableOnLookup()
    {
        var t = new CVTranslator();
        t.Insert("my_alias", CVID.MS_software);
        Assert.AreEqual(CVID.MS_software, t.Translate("my_alias"));
    }
}
