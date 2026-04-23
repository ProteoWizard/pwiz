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
    public void Modifications_HasManyEntries()
    {
        // unimod.obo has ~1400 terms; verify we loaded a substantial portion.
        Assert.IsTrue(UnimodApi.Modifications.Count > 100,
            $"expected >100 modifications, got {UnimodApi.Modifications.Count}");
    }

    [TestMethod]
    public void Modification_Acetyl_HasCorrectMass()
    {
        var acetyl = UnimodApi.Modification(CVID.UNIMOD_Acetyl);
        Assert.IsNotNull(acetyl);
        Assert.AreEqual("Acetyl", acetyl.Name);
        // delta_mono_mass "42.010565"
        Assert.AreEqual(42.010565, acetyl.DeltaMonoisotopicMass, 1e-5);
        Assert.AreEqual(42.0367, acetyl.DeltaAverageMass, 1e-3);
        Assert.AreEqual("H(2) C(2) O", acetyl.DeltaComposition);
    }

    [TestMethod]
    public void Modification_Acetyl_HasSpecificities()
    {
        var acetyl = UnimodApi.Modification(CVID.UNIMOD_Acetyl);
        Assert.IsNotNull(acetyl);
        Assert.IsTrue(acetyl.Specificities.Count > 0, "Acetyl should have specificity entries");

        // Acetyl commonly has a spec on Lysine (spec_1_site "K").
        bool hasLysine = acetyl.Specificities.Any(s => s.Site == UnimodSite.Lysine);
        Assert.IsTrue(hasLysine, "Acetyl should have a Lysine specificity");
    }

    [TestMethod]
    public void Modification_Acetyl_Approved()
    {
        var acetyl = UnimodApi.Modification(CVID.UNIMOD_Acetyl);
        Assert.IsNotNull(acetyl);
        Assert.IsTrue(acetyl.Approved);
    }

    [TestMethod]
    public void Modification_Phospho_LookupByTitle()
    {
        var phospho = UnimodApi.Modification("Phospho");
        Assert.IsNotNull(phospho);
        Assert.AreEqual("Phospho", phospho.Name);
        // Phospho delta_mono_mass ~ 79.966331
        Assert.AreEqual(79.966331, phospho.DeltaMonoisotopicMass, 1e-5);
    }

    [TestMethod]
    public void Modification_Unknown_ReturnsNull()
    {
        Assert.IsNull(UnimodApi.Modification("ThisIsNotAMod"));
        Assert.IsNull(UnimodApi.Modification(CVID.CVID_Unknown));
    }
}
