namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// <c>MzTabReader</c> resolves <c>UNIMOD:</c> modification ids against the unimod.obo that
/// pwiz-sharp embeds (Pwiz.Data.Common.Unimod), like the DIA-NN and TSV readers, rather than a
/// separately shipped unimod.xml. The golden inputs hold no mzTab file, so this pins the lookup.
/// </summary>
[TestClass]
public class MzTabReaderTests
{
    [TestMethod]
    public void MzTab_UnimodIds_ResolveFromObo()
    {
        AssertUnimodMass(4, 57.021464);    // Carbamidomethyl
        AssertUnimodMass(35, 15.994915);   // Oxidation
        // iST-NHS specific cysteine modification: in the obo, absent from the 2023 unimod.xml
        AssertUnimodMass(2086, 113.084064);
        Assert.IsFalse(MzTabReader.TryGetUnimodMonoMass(999999, out _));
    }

    private static void AssertUnimodMass(int id, double expected)
    {
        Assert.IsTrue(MzTabReader.TryGetUnimodMonoMass(id, out var mass), $"UNIMOD:{id} not found");
        Assert.AreEqual(expected, mass, 1e-9, $"UNIMOD:{id}");
    }
}
