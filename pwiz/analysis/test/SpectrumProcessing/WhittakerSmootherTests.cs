using MathNet.Numerics.LinearAlgebra;
using Pwiz.Analysis.PeakPicking;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// Pins <see cref="WhittakerSmoother"/> to the system cpp solves, (I + λDᵀD) z = y with D the
/// first-difference matrix, which cpp inverts densely. Same input series as cpp's
/// WhittakerSmootherTest (a 14-point pattern repeated four times, smoothed with λ = 10).
/// </summary>
[TestClass]
public class WhittakerSmootherTests
{
    private static readonly double[] Pattern = { 1, 15, 29, 20, 10, 40, 1, 50, 3, 40, 3, 25, 23, 90 };

    [TestMethod]
    public void Smooth_MatchesDenseSolveOfCppSystem()
    {
        double[] y = Enumerable.Repeat(Pattern, 4).SelectMany(p => p).ToArray();
        double[] x = Enumerable.Range(1, y.Length).Select(i => (double) i).ToArray();
        const double lambda = 10;

        var xSmoothed = new List<double>();
        var ySmoothed = new List<double>();
        new WhittakerSmoother(lambda).Smooth(x, y, xSmoothed, ySmoothed);

        int n = y.Length;
        var d = Matrix<double>.Build.Dense(n - 1, n);
        for (int row = 0; row < n - 1; row++)
        {
            d[row, row] = -1;
            d[row, row + 1] = 1;
        }
        var system = Matrix<double>.Build.DenseIdentity(n) + lambda * d.TransposeThisAndMultiply(d);
        var expected = system.Inverse() * Vector<double>.Build.DenseOfArray(y);

        CollectionAssert.AreEqual(x, xSmoothed);
        Assert.AreEqual(n, ySmoothed.Count);
        for (int i = 0; i < n; i++)
            Assert.AreEqual(expected[i], ySmoothed[i], 1e-9, $"index {i}");
        // 1ᵀDᵀD = 0, so smoothing moves intensity between samples without adding or losing any.
        Assert.AreEqual(y.Sum(), ySmoothed.Sum(), 1e-9);
    }

    [TestMethod]
    public void Constructor_RejectsLambdaBelowTwo()
    {
        Assert.ThrowsException<ArgumentException>(() => new WhittakerSmoother(1));
        _ = new WhittakerSmoother(100001); // valid up to numeric limits
    }
}
