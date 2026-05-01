using Pwiz.Util.Chemistry;

namespace Pwiz.Util.Tests.Chemistry;

[TestClass]
public class IsotopeCalculatorTests
{
    private static IsotopeCalculator Calc() => new(abundanceCutoff: 1e-8, massPrecision: 0.01);

    [TestMethod]
    public void Distribution_NeutralAndChargedAndEdgeCases()
    {
        var calc = Calc();

        // Neutral, small molecules: monoisotopic peak dominates, abundances sum to 1.
        var water = calc.Distribution(new Formula("H2 O"));
        Assert.IsTrue(water.Count > 0);
        Assert.AreEqual(18.01056, water.OrderByDescending(m => m.Abundance).First().Mass, 1e-3, "water mono");

        var glucose = calc.Distribution(new Formula("C6 H12 O6"));
        var sortedGlucose = glucose.OrderByDescending(m => m.Abundance).ToList();
        Assert.AreEqual(180.0634, sortedGlucose[0].Mass, 1e-3, "glucose mono");
        Assert.AreEqual(1.0, glucose.Sum(m => m.Abundance), 1e-3, "abundances sum to 1");

        // [M+H]+ shifts the top peak to (M + proton)/1 = 181.0707 for glucose.
        var charged = calc.Distribution(new Formula("C6 H12 O6"), chargeState: 1)
            .OrderByDescending(m => m.Abundance).First();
        Assert.AreEqual(181.0707, charged.Mass, 1e-3);

        // Empty formula → empty distribution.
        Assert.AreEqual(0, calc.Distribution(new Formula("")).Count);

        // C100 has a substantial M+1 peak from 13C; spacing ≈ 1.00336 u (13C − 12C).
        var c100 = calc.Distribution(new Formula("C100")).OrderBy(m => m.Mass).ToList();
        Assert.IsTrue(c100.Count >= 2, "C100 should produce isotopologues beyond M");
        Assert.AreEqual(1.00336, c100[1].Mass - c100[0].Mass, 0.01, "13C mass delta");
    }

    [TestMethod]
    public void Distribution_NormalizationModes()
    {
        var calc = Calc();

        // Mass normalization shifts the first (lightest) peak to mass 0.
        var massNorm = calc.Distribution(new Formula("H2 O"), normalization: IsotopeNormalization.Mass);
        Assert.AreEqual(0, massNorm[0].Mass, 1e-6, "mass normalization zeroes first peak");

        // Abundance normalization makes the L2 norm of abundances equal 1.
        var abundNorm = calc.Distribution(new Formula("H2 O"), normalization: IsotopeNormalization.Abundance);
        double sumSq = abundNorm.Sum(m => m.Abundance * m.Abundance);
        Assert.AreEqual(1.0, sumSq, 1e-6, "abundance L2 norm == 1");
    }
}
