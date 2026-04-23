using Pwiz.Util.Numerics;

namespace Pwiz.Util.Tests.Numerics;

[TestClass]
public class ParabolaTests
{
    [TestMethod]
    public void Coefficients_EvaluateCorrectly()
    {
        // y = x² − 2x + 1 = (x − 1)²; at x=2 → 1, at x=0 → 1, at x=1 → 0.
        var p = new Parabola(1, -2, 1);
        Assert.AreEqual(1.0, p.Evaluate(2), 1e-12);
        Assert.AreEqual(1.0, p.Evaluate(0), 1e-12);
        Assert.AreEqual(0.0, p.Evaluate(1), 1e-12);
    }

    [TestMethod]
    public void Center_ReturnsVertexX()
    {
        // y = x² − 2x + 1 has vertex at x = 1.
        var p = new Parabola(1, -2, 1);
        Assert.AreEqual(1.0, p.Center, 1e-12);
    }

    [TestMethod]
    public void Center_LinearCase_ReturnsNaN()
    {
        // a == 0 → undefined (division by zero)
        var p = new Parabola(0, 1, 0);
        Assert.IsTrue(double.IsNaN(p.Center));
    }

    [TestMethod]
    public void Fit_ThreePointsExactly_RecoversCoefficients()
    {
        // Points on y = 2x² − 3x + 5: (0,5), (1,4), (2,7).
        var samples = new List<(double, double)> { (0, 5), (1, 4), (2, 7) };
        var p = new Parabola(samples);
        Assert.AreEqual(2, p.Coefficients[0], 1e-9);
        Assert.AreEqual(-3, p.Coefficients[1], 1e-9);
        Assert.AreEqual(5, p.Coefficients[2], 1e-9);
    }

    [TestMethod]
    public void Fit_ManyPoints_LeastSquaresMinimizes()
    {
        // Noisy samples around y = x² + x + 1; coefficients should still be close.
        var samples = new List<(double, double)>();
        for (int x = -3; x <= 3; x++) samples.Add((x, x * x + x + 1.0));
        var p = new Parabola(samples);
        Assert.AreEqual(1.0, p.Coefficients[0], 1e-9);
        Assert.AreEqual(1.0, p.Coefficients[1], 1e-9);
        Assert.AreEqual(1.0, p.Coefficients[2], 1e-9);
    }

    [TestMethod]
    public void Fit_TooFewSamples_Throws()
    {
        var samples = new List<(double, double)> { (0, 0), (1, 1) };
        Assert.ThrowsException<ArgumentException>(() => new Parabola(samples));
    }

    [TestMethod]
    public void Fit_WeightedLeastSquares_RespectsWeights()
    {
        // Three points; weight third one heavily so it dominates.
        var samples = new List<(double, double)> { (0, 0), (1, 1), (2, 10) };
        var weights = new List<double> { 1, 1, 1000 };
        var p = new Parabola(samples, weights);
        // Heavy-weight sample must be (almost) exactly on the curve.
        Assert.AreEqual(10.0, p.Evaluate(2), 1e-3);
    }
}
