using Pwiz.Util.Chemistry;

namespace Pwiz.Util.Tests.Chemistry;

[TestClass]
public class FormulaTests
{
    // ---- Parsing ----

    [TestMethod]
    public void Parse_Water_PartsToH2O1()
    {
        var f = new Formula("H2 O1");
        Assert.AreEqual(2, f[ElementType.H]);
        Assert.AreEqual(1, f[ElementType.O]);
    }

    [TestMethod]
    public void Parse_WaterNoWhitespace_Works()
    {
        var f = new Formula("H2O");
        Assert.AreEqual(2, f[ElementType.H]);
        Assert.AreEqual(1, f[ElementType.O]);
    }

    [TestMethod]
    public void Parse_Glucose_AllElementsRead()
    {
        var f = new Formula("C6 H12 O6");
        Assert.AreEqual(6, f[ElementType.C]);
        Assert.AreEqual(12, f[ElementType.H]);
        Assert.AreEqual(6, f[ElementType.O]);
    }

    [TestMethod]
    public void Parse_IsotopeLabel_2H_Recognized()
    {
        var f = new Formula("_2H2 O");
        Assert.AreEqual(2, f[ElementType._2H]);
        Assert.AreEqual(1, f[ElementType.O]);
        Assert.AreEqual(0, f[ElementType.H]);
    }

    [TestMethod]
    public void Parse_IsotopeLabel_13C_Recognized()
    {
        var f = new Formula("_13C6 H12 O6");
        Assert.AreEqual(6, f[ElementType._13C]);
        Assert.AreEqual(0, f[ElementType.C]);
    }

    [TestMethod]
    public void Parse_ElementWithoutCount_AssumedOne()
    {
        var f = new Formula("Fe");
        Assert.AreEqual(1, f[ElementType.Fe]);
    }

    [TestMethod]
    public void Parse_MultiLetterSymbol_ReadWholly()
    {
        // He vs H + e — the parser must consume both letters as "He".
        var f = new Formula("He2");
        Assert.AreEqual(2, f[ElementType.He]);
        Assert.AreEqual(0, f[ElementType.H]);
    }

    [TestMethod]
    public void Parse_DeuteriumSynonym_MapsTo_2H()
    {
        // "D" is an IUPAC synonym for _2H in the element table.
        var f = new Formula("D2 O");
        Assert.AreEqual(2, f[ElementType._2H]);
        Assert.AreEqual(1, f[ElementType.O]);
    }

    [TestMethod]
    public void Parse_UnknownSymbol_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() => new Formula("Xy2"));
    }

    [TestMethod]
    public void Parse_Empty_YieldsEmptyFormula()
    {
        var f = new Formula("");
        Assert.AreEqual(0.0, f.MonoisotopicMass);
        Assert.AreEqual(0.0, f.MolecularWeight);
    }

    // ---- Masses ----

    [TestMethod]
    public void MonoisotopicMass_Water_MatchesReference()
    {
        // H2O monoisotopic mass: 2*1.0078250321 + 15.9949146221 = 18.0105646863
        var f = new Formula("H2 O");
        Assert.AreEqual(18.0105646863, f.MonoisotopicMass, 1e-6);
    }

    [TestMethod]
    public void MolecularWeight_Water_MatchesReference()
    {
        // H2O: 2*1.00794 + 15.9994 ~ 18.01528
        var f = new Formula("H2 O");
        Assert.AreEqual(18.01528, f.MolecularWeight, 1e-3);
    }

    [TestMethod]
    public void MonoisotopicMass_Glucose_MatchesReference()
    {
        // C6H12O6: 6*12 + 12*1.0078250321 + 6*15.9949146221 = 180.0633881
        var f = new Formula("C6 H12 O6");
        Assert.AreEqual(180.0633881, f.MonoisotopicMass, 1e-4);
    }

    // ---- Arithmetic ----

    [TestMethod]
    public void Addition_CombinesCounts()
    {
        var a = new Formula("H2 O");
        var b = new Formula("H2 O");
        var sum = a + b;
        Assert.AreEqual(4, sum[ElementType.H]);
        Assert.AreEqual(2, sum[ElementType.O]);
    }

    [TestMethod]
    public void Subtraction_RemovesCounts()
    {
        var a = new Formula("C6 H12 O6");
        var b = new Formula("H2 O");
        var diff = a - b;
        Assert.AreEqual(10, diff[ElementType.H]);
        Assert.AreEqual(5, diff[ElementType.O]);
    }

    [TestMethod]
    public void ScalarMultiply_MultipliesAllCounts()
    {
        var a = new Formula("H2 O");
        var tripled = a * 3;
        Assert.AreEqual(6, tripled[ElementType.H]);
        Assert.AreEqual(3, tripled[ElementType.O]);
    }

    [TestMethod]
    public void ScalarMultiply_LeftAndRight_AreEquivalent()
    {
        var a = new Formula("H2 O");
        Assert.AreEqual(a * 3, 3 * a);
    }

    // ---- Equality / ToString ----

    [TestMethod]
    public void Equality_SameCounts_Equal()
    {
        Assert.AreEqual(new Formula("H2 O"), new Formula("O H2"));
    }

    [TestMethod]
    public void Equality_DifferentCounts_NotEqual()
    {
        Assert.AreNotEqual(new Formula("H2 O"), new Formula("H2 O2"));
    }

    [TestMethod]
    public void ToString_CanonicalAlphabeticalOrder()
    {
        var f = new Formula("O H2");
        // Alphabetical concatenation of "H2" + "O1"
        Assert.AreEqual("H2O1", f.ToString());
    }

    [TestMethod]
    public void Indexer_SetsCountAndUpdatesMass()
    {
        var f = new Formula();
        f[ElementType.H] = 2;
        f[ElementType.O] = 1;
        Assert.AreEqual(18.0105646863, f.MonoisotopicMass, 1e-6);
    }

    [TestMethod]
    public void Copy_IsIndependent()
    {
        var a = new Formula("H2 O");
        var b = new Formula(a);
        b[ElementType.H] = 0;
        Assert.AreEqual(2, a[ElementType.H]);
        Assert.AreEqual(0, b[ElementType.H]);
    }
}
