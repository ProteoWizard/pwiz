using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.Common.Tests.Cv;

[TestClass]
public class CvLookupTests
{
    [TestMethod]
    public void CvTermInfo_LookupAndAccessionRoundTrip()
    {
        // Known MS / UO terms decode to the expected prefix + name.
        var ms = CvLookup.CvTermInfo(CVID.MS_software);
        Assert.AreEqual("MS", ms.Prefix);
        Assert.IsTrue(ms.Id.StartsWith("MS:", StringComparison.Ordinal));
        Assert.AreEqual("software", ms.Name);

        var uo = CvLookup.CvTermInfo(CVID.UO_second);
        Assert.AreEqual("UO", uo.Prefix);
        Assert.AreEqual("second", uo.Name);

        // CVID -> Id -> CVID round-trips for known terms.
        var byId = CvLookup.CvTermInfo(ms.Id);
        Assert.AreEqual(ms.Cvid, byId.Cvid);

        // CVID_Unknown returns a sentinel rather than throwing; same for unknown accession.
        var unknown = CvLookup.CvTermInfo(CVID.CVID_Unknown);
        Assert.AreEqual(CVID.CVID_Unknown, unknown.Cvid);
        Assert.AreEqual("??:0000000", unknown.Id);
        Assert.AreEqual(CVID.CVID_Unknown, CvLookup.CvTermInfo("XX:9999999").Cvid);
    }

    [TestMethod]
    public void CvIsA_AndAllCvidsTable()
    {
        // CvIsA: identity is true; unrelated namespaces are false.
        Assert.IsTrue(CvLookup.CvIsA(CVID.MS_software, CVID.MS_software), "identity");
        Assert.IsFalse(CvLookup.CvIsA(CVID.MS_software, CVID.UO_second), "unrelated");

        // AllCvids: bulk shape — thousands of terms generated, covers the namespaces we use.
        var all = CvLookup.AllCvids;
        Assert.IsTrue(all.Count > 1000, "generated enum should have thousands of terms");
        CollectionAssert.Contains(all.ToList(), CVID.MS_software);
        CollectionAssert.Contains(all.ToList(), CVID.UO_second);

        // Terms marked obsolete in the OBO are flagged on CvTermInfo.
        Assert.IsTrue(CvLookup.CvTermInfo(CVID.MS_second_OBSOLETE).IsObsolete);
    }
}
