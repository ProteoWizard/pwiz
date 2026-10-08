using Pwiz.Util.Misc;

namespace Pwiz.Util.Tests.Misc;

[TestClass]
public class FloatingPointTests
{
    [TestMethod]
    public void AlmostEqual_BehaviorAcrossInputsAndMultipliers()
    {
        const double machineEps = 2.2204460492503131e-16;

        // Identity and gross inequality.
        Assert.IsTrue(FloatingPoint.AlmostEqual(1.0, 1.0), "identical");
        Assert.IsFalse(FloatingPoint.AlmostEqual(1.0, 2.0), "vastly different");

        // Multiplier scales tolerance: values separated by ~5× ε fail at multiplier=1, pass at 10.
        double a = 1.0;
        double b = 1.0 + 5 * machineEps;
        Assert.IsFalse(FloatingPoint.AlmostEqual(a, b, multiplier: 1));
        Assert.IsTrue(FloatingPoint.AlmostEqual(a, b, multiplier: 10));

        // When a == 0, scale defaults to 1, so (0, ε/2) passes for any multiplier ≥ 1.
        Assert.IsTrue(FloatingPoint.AlmostEqual(0.0, machineEps / 2));
    }
}
