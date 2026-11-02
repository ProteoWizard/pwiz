using Pwiz.Util.Chemistry;

namespace Pwiz.Util.Tests.Chemistry;

[TestClass]
public class FormulaTests
{
    [TestMethod]
    public void Parse_HandlesAllSyntaxAndEdgeCases()
    {
        // Whitespace, no-whitespace, multi-element, optional-count, multi-letter symbols.
        var basicCases = new[]
        {
            ("H2 O1",     new[] { (ElementType.H, 2), (ElementType.O, 1) }),
            ("H2O",       new[] { (ElementType.H, 2), (ElementType.O, 1) }),
            ("C6 H12 O6", new[] { (ElementType.C, 6), (ElementType.H, 12), (ElementType.O, 6) }),
            ("Fe",        new[] { (ElementType.Fe, 1) }),
            ("He2",       new[] { (ElementType.He, 2), (ElementType.H, 0) }), // multi-letter, not H+e
        };
        foreach (var (input, expected) in basicCases)
        {
            var f = new Formula(input);
            foreach (var (element, count) in expected)
                Assert.AreEqual(count, f[element], $"Parse(\"{input}\")[{element}]");
        }

        // Isotope labels: _2H, _13C, and the "D" synonym for _2H.
        var d2o = new Formula("_2H2 O");
        Assert.AreEqual(2, d2o[ElementType._2H]);
        Assert.AreEqual(1, d2o[ElementType.O]);
        Assert.AreEqual(0, d2o[ElementType.H], "D label must not bleed into regular H");

        var c13Glucose = new Formula("_13C6 H12 O6");
        Assert.AreEqual(6, c13Glucose[ElementType._13C]);
        Assert.AreEqual(0, c13Glucose[ElementType.C]);

        var deuteriumSynonym = new Formula("D2 O");
        Assert.AreEqual(2, deuteriumSynonym[ElementType._2H]);
        Assert.AreEqual(1, deuteriumSynonym[ElementType.O]);

        // Error path: unknown symbol throws.
        Assert.ThrowsException<ArgumentException>(() => new Formula("Xy2"));

        // Empty input is a zero-mass formula (not an error).
        var empty = new Formula("");
        Assert.AreEqual(0.0, empty.MonoisotopicMass);
        Assert.AreEqual(0.0, empty.MolecularWeight);
    }

    [TestMethod]
    public void Masses_AreCorrectAtKnownReferenceValues()
    {
        // H2O mono: 2*1.0078250321 + 15.9949146221 = 18.0105646863
        // H2O average: 2*1.00794 + 15.9994 ~ 18.01528
        // C6H12O6 mono: 6*12 + 12*1.0078250321 + 6*15.9949146221 = 180.0633881
        var water = new Formula("H2 O");
        Assert.AreEqual(18.0105646863, water.MonoisotopicMass, 1e-6, "water mono");
        Assert.AreEqual(18.01528, water.MolecularWeight, 1e-3, "water avg");

        var glucose = new Formula("C6 H12 O6");
        Assert.AreEqual(180.0633881, glucose.MonoisotopicMass, 1e-4, "glucose mono");
    }

    [TestMethod]
    public void Arithmetic_AddSubtractMultiply()
    {
        var water = new Formula("H2 O");
        var sum = water + water;
        Assert.AreEqual(4, sum[ElementType.H], "H2O + H2O = H4O2");
        Assert.AreEqual(2, sum[ElementType.O]);

        var diff = new Formula("C6 H12 O6") - water;
        Assert.AreEqual(10, diff[ElementType.H], "C6H12O6 - H2O");
        Assert.AreEqual(5, diff[ElementType.O]);

        var tripled = water * 3;
        Assert.AreEqual(6, tripled[ElementType.H], "H2O * 3");
        Assert.AreEqual(3, tripled[ElementType.O]);
        Assert.AreEqual(tripled, 3 * water, "left- and right-scalar multiply equivalent");
    }

    [TestMethod]
    public void Equality_ToString_IndexerAndCopySemantics()
    {
        // Equality is by element-count multiset, not by source-string ordering.
        Assert.AreEqual(new Formula("H2 O"), new Formula("O H2"));
        Assert.AreNotEqual(new Formula("H2 O"), new Formula("H2 O2"));

        // ToString emits canonical alphabetical order with explicit "1" counts.
        Assert.AreEqual("H2O1", new Formula("O H2").ToString());

        // Indexer mutates count and recomputes mass.
        var built = new Formula();
        built[ElementType.H] = 2;
        built[ElementType.O] = 1;
        Assert.AreEqual(18.0105646863, built.MonoisotopicMass, 1e-6);

        // Copy is independent — mutating the copy must not bleed into the original.
        var original = new Formula("H2 O");
        var copy = new Formula(original);
        copy[ElementType.H] = 0;
        Assert.AreEqual(2, original[ElementType.H], "original unchanged");
        Assert.AreEqual(0, copy[ElementType.H], "copy mutated");
    }
}
