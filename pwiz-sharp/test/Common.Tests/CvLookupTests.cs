using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.Common.Tests.Cv;

[TestClass]
public class CvLookupTests
{
    [TestMethod]
    public void CvTermInfo_UnknownCvid_ReturnsUnknownSentinel()
    {
        var info = CvLookup.CvTermInfo(CVID.CVID_Unknown);
        Assert.AreEqual(CVID.CVID_Unknown, info.Cvid);
        Assert.AreEqual("??:0000000", info.Id);
    }

    [TestMethod]
    public void CvTermInfo_MsTerm_ReturnsMsAccession()
    {
        var info = CvLookup.CvTermInfo(CVID.MS_software);
        Assert.AreEqual("MS", info.Prefix);
        Assert.IsTrue(info.Id.StartsWith("MS:", StringComparison.Ordinal));
        Assert.AreEqual("software", info.Name);
    }

    [TestMethod]
    public void CvTermInfo_UoTerm_ReturnsUoAccession()
    {
        var info = CvLookup.CvTermInfo(CVID.UO_second);
        Assert.AreEqual("UO", info.Prefix);
        Assert.AreEqual("second", info.Name);
    }

    [TestMethod]
    public void CvTermInfo_ByAccession_RoundTrips()
    {
        var byEnum = CvLookup.CvTermInfo(CVID.MS_software);
        var byId = CvLookup.CvTermInfo(byEnum.Id);
        Assert.AreEqual(byEnum.Cvid, byId.Cvid);
    }

    [TestMethod]
    public void CvTermInfo_UnknownAccession_ReturnsUnknown()
    {
        var info = CvLookup.CvTermInfo("XX:9999999");
        Assert.AreEqual(CVID.CVID_Unknown, info.Cvid);
    }

    [TestMethod]
    public void CvIsA_Identity_IsTrue()
    {
        Assert.IsTrue(CvLookup.CvIsA(CVID.MS_software, CVID.MS_software));
    }

    [TestMethod]
    public void CvIsA_Unrelated_IsFalse()
    {
        Assert.IsFalse(CvLookup.CvIsA(CVID.MS_software, CVID.UO_second));
    }

    [TestMethod]
    public void AllCvids_IncludesKnownTerms()
    {
        var all = CvLookup.AllCvids;
        Assert.IsTrue(all.Count > 1000, "generated enum should have thousands of terms");
        CollectionAssert.Contains((List<CVID>)all.ToList(), CVID.MS_software);
        CollectionAssert.Contains((List<CVID>)all.ToList(), CVID.UO_second);
    }

    [TestMethod]
    public void ObsoleteTerm_IsFlagged()
    {
        var info = CvLookup.CvTermInfo(CVID.MS_second_OBSOLETE);
        Assert.IsTrue(info.IsObsolete);
    }
}
