using Pwiz.Util.Chemistry;

namespace Pwiz.Util.Tests.Chemistry;

[TestClass]
public class IsotopeCalculatorTests
{
    [TestMethod]
    public void Distribution_Water_StartsWithMonoisotopic()
    {
        var calc = new IsotopeCalculator(abundanceCutoff: 1e-8, massPrecision: 0.01);
        var dist = calc.Distribution(new Formula("H2 O"));

        Assert.IsTrue(dist.Count > 0);
        // The most abundant peak should be the monoisotopic mass (~18.01056).
        double maxAbundance = dist.Max(m => m.Abundance);
        var mono = dist.First(m => m.Abundance == maxAbundance);
        Assert.AreEqual(18.01056, mono.Mass, 1e-3);
    }

    [TestMethod]
    public void Distribution_Glucose_TopPeakIsMonoisotopic()
    {
        // C6H12O6: monoisotopic ~180.0634
        var calc = new IsotopeCalculator(abundanceCutoff: 1e-8, massPrecision: 0.01);
        var dist = calc.Distribution(new Formula("C6 H12 O6"));

        // Monoisotopic peak should dominate for small molecules.
        var sorted = dist.OrderByDescending(m => m.Abundance).ToList();
        Assert.AreEqual(180.0634, sorted[0].Mass, 1e-3);

        // Abundances should sum to approximately 1.
        double sum = dist.Sum(m => m.Abundance);
        Assert.AreEqual(1.0, sum, 1e-3);
    }

    [TestMethod]
    public void Distribution_Chargestate_ConvertsToMz()
    {
        // [C6H12O6 + 1H]+: m/z = (180.0634 + proton) / 1 = 181.0707
        var calc = new IsotopeCalculator(1e-8, 0.01);
        var dist = calc.Distribution(new Formula("C6 H12 O6"), chargeState: 1);
        var top = dist.OrderByDescending(m => m.Abundance).First();
        Assert.AreEqual(181.0707, top.Mass, 1e-3);
    }

    [TestMethod]
    public void Distribution_MassNormalization_ShiftsFirstToZero()
    {
        var calc = new IsotopeCalculator(1e-8, 0.01);
        var dist = calc.Distribution(
            new Formula("H2 O"),
            normalization: IsotopeNormalization.Mass);
        Assert.AreEqual(0, dist[0].Mass, 1e-6);
    }

    [TestMethod]
    public void Distribution_AbundanceNormalization_L2NormEqualsOne()
    {
        var calc = new IsotopeCalculator(1e-8, 0.01);
        var dist = calc.Distribution(
            new Formula("H2 O"),
            normalization: IsotopeNormalization.Abundance);
        double sumSq = dist.Sum(m => m.Abundance * m.Abundance);
        Assert.AreEqual(1.0, sumSq, 1e-6);
    }

    [TestMethod]
    public void Distribution_EmptyFormula_EmptyResult()
    {
        var calc = new IsotopeCalculator(1e-8, 0.01);
        var dist = calc.Distribution(new Formula(""));
        Assert.AreEqual(0, dist.Count);
    }

    [TestMethod]
    public void Distribution_Carbon_HasSecondPeakAtPlusOne()
    {
        // C has two significant isotopes: 12C (98.93%) and 13C (1.07%).
        // For C100, the M+1 peak should be present and substantial.
        var calc = new IsotopeCalculator(1e-8, 0.01);
        var dist = calc.Distribution(new Formula("C100"));
        var sorted = dist.OrderBy(m => m.Mass).ToList();
        Assert.IsTrue(sorted.Count >= 2);
        // M+1 should be ~1.003 u higher than M (13C − 12C = ~1.00336)
        double delta = sorted[1].Mass - sorted[0].Mass;
        Assert.AreEqual(1.00336, delta, 0.01);
    }
}
