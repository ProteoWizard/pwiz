using Pwiz.Util.Chemistry;

namespace Pwiz.Util.Tests.Chemistry;

/// <summary>
/// Port of cpp's <c>IsotopeCalculatorTest.cpp::testProbabilites()</c>. cpp checks the calculator
/// against an INDEPENDENT computation rather than against recorded output: the abundance of the
/// C100 isotopologue carrying k heavy carbons is the multinomial
/// <c>C(100; 100-k, k) * p0^(100-k) * p1^k</c> over carbon's own isotope abundances. Agreement to
/// 1e-10 says the calculator's convolution and pruning are right, which no assertion about the
/// dominant peak's mass can show.
/// </summary>
[TestClass]
public class IsotopeCalculatorCppParityTests
{
    [TestMethod]
    public void C100Abundances_MatchIndependentMultinomial()
    {
        var carbon = ElementInfo.Record(ElementType.C).Isotopes;
        double p0 = carbon[0].Abundance;
        double p1 = carbon[1].Abundance;

        // cpp checks the first five isotopologues: 0 through 4 heavy carbons.
        var expected = Enumerable.Range(0, 5)
            .Select(k => Multinomial(100, k) * System.Math.Pow(p0, 100 - k) * System.Math.Pow(p1, k))
            .ToArray();

        var calculator = new IsotopeCalculator(abundanceCutoff: 1e-8, massPrecision: 0.01);
        var actual = calculator.Distribution(new Formula("C100"));

        Assert.IsTrue(actual.Count >= expected.Length,
            $"expected at least {expected.Length} isotopologues, got {actual.Count}");
        for (int k = 0; k < expected.Length; k++)
            Assert.AreEqual(expected[k], actual[k].Abundance, 1e-10,
                $"abundance of the isotopologue with {k} heavy carbon(s)");
    }

    /// <summary>C(n; n-k, k), i.e. the number of ways to choose which k of the n atoms are heavy.</summary>
    private static double Multinomial(int n, int k)
    {
        double result = 1;
        for (int i = 0; i < k; i++)
            result = result * (n - i) / (i + 1);
        return result;
    }
}
