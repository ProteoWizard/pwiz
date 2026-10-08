using Pwiz.Util.Numerics;

namespace Pwiz.Util.Tests.Numerics;

[TestClass]
public class ParabolaTests
{
    [TestMethod]
    public void Coefficients_EvaluateAndCenter()
    {
        // y = x² − 2x + 1 = (x − 1)²; vertex at x = 1.
        var p = new Parabola(1, -2, 1);
        Assert.AreEqual(1.0, p.Evaluate(2), 1e-12, "Evaluate(2)");
        Assert.AreEqual(1.0, p.Evaluate(0), 1e-12, "Evaluate(0)");
        Assert.AreEqual(0.0, p.Evaluate(1), 1e-12, "Evaluate(1) = vertex y");
        Assert.AreEqual(1.0, p.Center, 1e-12, "vertex x");

        // a == 0 → linear, no parabolic vertex defined.
        Assert.IsTrue(double.IsNaN(new Parabola(0, 1, 0).Center), "linear case Center");
    }

    [TestMethod]
    public void Fit_RecoversCoefficients_AndRespectsWeights()
    {
        // Three exact points on y = 2x² − 3x + 5 must recover (2, -3, 5).
        var exact = new List<(double, double)> { (0, 5), (1, 4), (2, 7) };
        var pExact = new Parabola(exact);
        Assert.AreEqual(2, pExact.Coefficients[0], 1e-9);
        Assert.AreEqual(-3, pExact.Coefficients[1], 1e-9);
        Assert.AreEqual(5, pExact.Coefficients[2], 1e-9);

        // Many points on y = x² + x + 1; least-squares recovers exactly.
        var many = new List<(double, double)>();
        for (int x = -3; x <= 3; x++) many.Add((x, x * x + x + 1.0));
        var pMany = new Parabola(many);
        Assert.AreEqual(1.0, pMany.Coefficients[0], 1e-9);
        Assert.AreEqual(1.0, pMany.Coefficients[1], 1e-9);
        Assert.AreEqual(1.0, pMany.Coefficients[2], 1e-9);

        // Weighted fit: heavily-weighted sample dominates the curve.
        var weighted = new List<(double, double)> { (0, 0), (1, 1), (2, 10) };
        var weights = new List<double> { 1, 1, 1000 };
        var pWeighted = new Parabola(weighted, weights);
        Assert.AreEqual(10.0, pWeighted.Evaluate(2), 1e-3, "heavy-weight sample on the curve");

        // Fewer than three samples can't fit a parabola.
        Assert.ThrowsException<ArgumentException>(
            () => new Parabola(new List<(double, double)> { (0, 0), (1, 1) }));
    }
}
