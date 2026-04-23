using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.Common.Tests.Cv;

/// <summary>
/// Verifies that the CV term info is populated from the embedded OBO files, not derived from enum names.
/// </summary>
[TestClass]
public class CvLookupOboTests
{
    [TestMethod]
    public void CvTermInfo_MsSoftware_HasRealDefinition()
    {
        var info = CvLookup.CvTermInfo(CVID.MS_software);
        Assert.AreEqual("software", info.Name);
        Assert.AreEqual("MS:1000531", info.Id);
        StringAssert.Contains(info.Def, "Software", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(info.Def.Length > 20, "real OBO def is descriptive, not just the name");
    }

    [TestMethod]
    public void CvTermInfo_UoSecond_HasIsAParents()
    {
        // UO:0000010 (second) is_a UO:0000045 (base unit) per unit.obo.
        var info = CvLookup.CvTermInfo(CVID.UO_second);
        Assert.IsTrue(info.ParentsIsA.Count > 0, "UO_second should have is_a parents from OBO");
    }

    [TestMethod]
    public void CvIsA_RealHierarchy_Works()
    {
        // Picks a term we know has a direct is_a from the OBO and verifies traversal.
        var info = CvLookup.CvTermInfo(CVID.UO_second);
        Assert.IsTrue(info.ParentsIsA.Count > 0);
        var someParent = info.ParentsIsA[0];
        Assert.IsTrue(CvLookup.CvIsA(CVID.UO_second, someParent),
            "CvIsA should follow the is_a chain for direct parents");
    }

    [TestMethod]
    public void CvTermInfo_MsSoftware_HasPartOfRelation()
    {
        // MS:1000531 (software) has part_of MS:0000000 but no is_a in psi-ms.obo 4.1.232.
        var info = CvLookup.CvTermInfo(CVID.MS_software);
        Assert.IsTrue(info.ParentsPartOf.Count > 0, "MS_software should have part_of parents from OBO");
    }

    [TestMethod]
    public void CvTermInfo_UoSecond_HasUnitDef()
    {
        var info = CvLookup.CvTermInfo(CVID.UO_second);
        Assert.AreEqual("UO:0000010", info.Id);
        Assert.AreEqual("second", info.Name);
        Assert.IsTrue(info.Def.Length > 10, "UO_second def should be populated");
    }

    [TestMethod]
    public void CvTermInfo_UnimodAcetyl_HasRealName()
    {
        var info = CvLookup.CvTermInfo(CVID.UNIMOD_Acetyl);
        Assert.AreEqual("UNIMOD:1", info.Id);
        Assert.AreEqual("Acetyl", info.Name);
    }

    [TestMethod]
    public void GetCv_Ms_HasVersionFromObo()
    {
        var cv = CvLookup.GetCv("MS");
        Assert.AreEqual("MS", cv.Id);
        Assert.IsFalse(string.IsNullOrEmpty(cv.Version), "version should be extracted from psi-ms.obo header");
    }

    [TestMethod]
    public void CvTermInfo_ExactSynonyms_PopulatedForTermsThatHaveThem()
    {
        // Look at ALL MS terms; at least some should have non-empty synonyms.
        int synCount = 0;
        foreach (var cvid in CvLookup.AllCvids)
        {
            var info = CvLookup.CvTermInfo(cvid);
            if (info.Prefix == "MS" && info.ExactSynonyms.Count > 0) synCount++;
            if (synCount > 5) break;
        }
        Assert.IsTrue(synCount > 0, "at least some MS terms should have exact synonyms populated");
    }
}
