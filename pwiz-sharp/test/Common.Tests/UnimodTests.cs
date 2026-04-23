using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Unimod;

namespace Pwiz.Data.Common.Tests.Unimod;

[TestClass]
public class UnimodTests
{
    [TestMethod]
    public void SiteFromSymbol_CommonResidues_MapCorrectly()
    {
        Assert.AreEqual(UnimodSite.Alanine, global::Pwiz.Data.Common.Unimod.Unimod.SiteFromSymbol('A'));
        Assert.AreEqual(UnimodSite.Lysine, global::Pwiz.Data.Common.Unimod.Unimod.SiteFromSymbol('K'));
        Assert.AreEqual(UnimodSite.Any, global::Pwiz.Data.Common.Unimod.Unimod.SiteFromSymbol('x'));
        Assert.AreEqual(UnimodSite.NTerminus, global::Pwiz.Data.Common.Unimod.Unimod.SiteFromSymbol('n'));
        Assert.AreEqual(UnimodSite.CTerminus, global::Pwiz.Data.Common.Unimod.Unimod.SiteFromSymbol('c'));
    }

    [TestMethod]
    public void SiteFromSymbol_UnknownChar_Throws()
    {
        Assert.ThrowsException<ArgumentException>(
            () => global::Pwiz.Data.Common.Unimod.Unimod.SiteFromSymbol('Z'));
    }

    [TestMethod]
    public void PositionFromCvid_DefaultUnknown_Anywhere()
    {
        Assert.AreEqual(UnimodPosition.Anywhere, global::Pwiz.Data.Common.Unimod.Unimod.PositionFromCvid());
    }

    [TestMethod]
    public void PositionFromCvid_PeptideNTerm_MapsCorrectly()
    {
        Assert.AreEqual(UnimodPosition.AnyNTerminus,
            global::Pwiz.Data.Common.Unimod.Unimod.PositionFromCvid(CVID.MS_modification_specificity_peptide_N_term));
    }
}
