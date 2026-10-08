using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Unimod;
using UnimodHelper = Pwiz.Data.Common.Unimod.Unimod;

namespace Pwiz.Data.Common.Tests.Unimod;

[TestClass]
public class UnimodTests
{
    [TestMethod]
    public void SiteFromSymbol_KnownAndUnknown()
    {
        // One-letter residue codes, plus the special 'x'/'n'/'c' wildcards.
        Assert.AreEqual(UnimodSite.Alanine, UnimodHelper.SiteFromSymbol('A'));
        Assert.AreEqual(UnimodSite.Lysine, UnimodHelper.SiteFromSymbol('K'));
        Assert.AreEqual(UnimodSite.Any, UnimodHelper.SiteFromSymbol('x'));
        Assert.AreEqual(UnimodSite.NTerminus, UnimodHelper.SiteFromSymbol('n'));
        Assert.AreEqual(UnimodSite.CTerminus, UnimodHelper.SiteFromSymbol('c'));

        // Unknown one-letter codes throw rather than silently returning a default.
        Assert.ThrowsException<ArgumentException>(() => UnimodHelper.SiteFromSymbol('Z'));
    }

    [TestMethod]
    public void PositionFromCvid_DefaultAndKnown()
    {
        // No CVID supplied -> Anywhere.
        Assert.AreEqual(UnimodPosition.Anywhere, UnimodHelper.PositionFromCvid());

        // Known CVID -> mapped position constraint.
        Assert.AreEqual(UnimodPosition.AnyNTerminus,
            UnimodHelper.PositionFromCvid(CVID.MS_modification_specificity_peptide_N_term));
    }
}
