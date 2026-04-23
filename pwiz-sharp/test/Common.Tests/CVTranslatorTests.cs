using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.Common.Tests.Cv;

[TestClass]
public class CVTranslatorTests
{
    [TestMethod]
    public void Translate_ExactMsTerm_ReturnsCvid()
    {
        var t = new CVTranslator();
        // "software" is the display name of MS_software
        Assert.AreEqual(CVID.MS_software, t.Translate("software"));
    }

    [TestMethod]
    public void Translate_CaseInsensitive()
    {
        var t = new CVTranslator();
        Assert.AreEqual(CVID.MS_software, t.Translate("SOFTWARE"));
        Assert.AreEqual(CVID.MS_software, t.Translate("Software"));
    }

    [TestMethod]
    public void Translate_WithExtraWhitespace_Matches()
    {
        var t = new CVTranslator();
        Assert.AreEqual(CVID.MS_software, t.Translate("  software  "));
    }

    [TestMethod]
    public void Translate_DefaultExtra_ITMS_MatchesIonTrap()
    {
        var t = new CVTranslator();
        Assert.AreEqual(CVID.MS_ion_trap, t.Translate("ITMS"));
    }

    [TestMethod]
    public void Translate_DefaultExtra_FTMS_MatchesFtIcr()
    {
        var t = new CVTranslator();
        Assert.AreEqual(CVID.MS_FT_ICR, t.Translate("FTMS"));
    }

    [TestMethod]
    public void Translate_Unknown_ReturnsCvidUnknown()
    {
        var t = new CVTranslator();
        Assert.AreEqual(CVID.CVID_Unknown, t.Translate("this is not a CV term"));
    }

    [TestMethod]
    public void Insert_CustomAlias_WorksOnLookup()
    {
        var t = new CVTranslator();
        t.Insert("my_alias", CVID.MS_software);
        Assert.AreEqual(CVID.MS_software, t.Translate("my_alias"));
    }
}
