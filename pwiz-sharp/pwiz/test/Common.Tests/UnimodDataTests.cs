using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Unimod;
using UnimodApi = Pwiz.Data.Common.Unimod.Unimod;

namespace Pwiz.Data.Common.Tests.Unimod;

/// <summary>
/// Verifies that the Unimod modification list is populated from the embedded unimod.obo.
/// </summary>
[TestClass]
public class UnimodDataTests
{
    [TestMethod]
    public void Modifications_DatabaseShapeAndPerEntryLookup()
    {
        // unimod.obo has ~1400 terms; we should load a substantial portion.
        Assert.IsTrue(UnimodApi.Modifications.Count > 100,
            $"expected >100 modifications, got {UnimodApi.Modifications.Count}");

        // Lookup by CVID + masses + composition + specificity + approval flag in one go.
        var acetyl = UnimodApi.Modification(CVID.UNIMOD_Acetyl);
        Assert.IsNotNull(acetyl);
        Assert.AreEqual("Acetyl", acetyl.Name);
        Assert.AreEqual(42.010565, acetyl.DeltaMonoisotopicMass, 1e-5, "delta mono");
        Assert.AreEqual(42.0367, acetyl.DeltaAverageMass, 1e-3, "delta avg");
        Assert.AreEqual("H(2) C(2) O", acetyl.DeltaComposition);
        Assert.IsTrue(acetyl.Approved, "Acetyl is an approved modification");
        Assert.IsTrue(acetyl.Specificities.Count > 0, "specificity entries populated");
        Assert.IsTrue(acetyl.Specificities.Any(s => s.Site == UnimodSite.Lysine),
            "Acetyl should have a Lysine specificity");

        // Lookup by name (string) works alongside lookup by CVID.
        var phospho = UnimodApi.Modification("Phospho");
        Assert.IsNotNull(phospho);
        Assert.AreEqual("Phospho", phospho.Name);
        Assert.AreEqual(79.966331, phospho.DeltaMonoisotopicMass, 1e-5);
    }

    [TestMethod]
    public void Modification_Unknown_ReturnsNull()
    {
        Assert.IsNull(UnimodApi.Modification("ThisIsNotAMod"), "unknown name");
        Assert.IsNull(UnimodApi.Modification(CVID.CVID_Unknown), "unknown CVID");
    }
}
