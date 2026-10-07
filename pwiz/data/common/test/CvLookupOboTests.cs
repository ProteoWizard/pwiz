using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.Common.Tests.Cv;

/// <summary>
/// Verifies that the CV term info is populated from the embedded OBO files, not derived from enum names.
/// </summary>
[TestClass]
public class CvLookupOboTests
{
    [TestMethod]
    public void CvTermInfo_AccessionsAndDefinitions()
    {
        // MS, UO, and UNIMOD CVs all expose ID + Name from the OBO files (not derived from enum names).
        var msSoftware = CvLookup.CvTermInfo(CVID.MS_software);
        Assert.AreEqual("software", msSoftware.Name);
        Assert.AreEqual("MS:1000531", msSoftware.Id);
        StringAssert.Contains(msSoftware.Def, "Software", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(msSoftware.Def.Length > 20, "OBO def should be descriptive, not just the name");

        var uoSecond = CvLookup.CvTermInfo(CVID.UO_second);
        Assert.AreEqual("UO:0000010", uoSecond.Id);
        Assert.AreEqual("second", uoSecond.Name);
        Assert.IsTrue(uoSecond.Def.Length > 10);

        var unimodAcetyl = CvLookup.CvTermInfo(CVID.UNIMOD_Acetyl);
        Assert.AreEqual("UNIMOD:1", unimodAcetyl.Id);
        Assert.AreEqual("Acetyl", unimodAcetyl.Name);
    }

    [TestMethod]
    public void CvTermInfo_Relations_IsAAndPartOf()
    {
        // UO_second has is_a UO_base_unit per unit.obo; the harness can traverse it.
        var uoSecond = CvLookup.CvTermInfo(CVID.UO_second);
        Assert.IsTrue(uoSecond.ParentsIsA.Count > 0, "UO_second should have is_a parents");
        Assert.IsTrue(CvLookup.CvIsA(CVID.UO_second, uoSecond.ParentsIsA[0]),
            "CvIsA should follow direct is_a edges");

        // MS_software has part_of (not is_a) per psi-ms.obo 4.1.232.
        Assert.IsTrue(CvLookup.CvTermInfo(CVID.MS_software).ParentsPartOf.Count > 0,
            "MS_software should have part_of parents");
    }

    [TestMethod]
    public void GetCv_VersionFromOboHeader()
    {
        var cv = CvLookup.GetCv("MS");
        Assert.AreEqual("MS", cv.Id);
        Assert.IsFalse(string.IsNullOrEmpty(cv.Version),
            "version should be extracted from psi-ms.obo header");
    }

    [TestMethod]
    public void CvTermInfo_ExactSynonyms_PopulatedForSomeTerms()
    {
        // Sweep MS terms and confirm at least a few have non-empty synonyms (so we know the
        // synonym block is parsed at all). Exits as soon as enough are found.
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
