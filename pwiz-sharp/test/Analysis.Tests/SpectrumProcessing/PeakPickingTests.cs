using Pwiz.Analysis.PeakPicking;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

[TestClass]
public class PeakPickingTests
{
    [TestMethod]
    public void SavitzkyGolaySmoother_GeneratesTextbookCoefficients()
    {
        // Reference values from Savitzky & Golay 1964 Table I (smoothing column). Exact rationals
        // expressed as small integers / denominator; coefficient generation is least-squares
        // exact so equality to ~1e-12 is appropriate.
        // Window=5, order=2: [-3, 12, 17, 12, -3] / 35
        var c5o2 = SavitzkyGolaySmoother.GenerateCoefficients(2, 2, 2);
        var expected5o2 = new[] { -3, 12, 17, 12, -3 }.Select(v => v / 35.0).ToArray();
        for (int i = 0; i < 5; i++) Assert.AreEqual(expected5o2[i], c5o2[i], 1e-12);

        // Window=7, order=2: [-2, 3, 6, 7, 6, 3, -2] / 21
        var c7o2 = SavitzkyGolaySmoother.GenerateCoefficients(3, 3, 2);
        var expected7o2 = new[] { -2, 3, 6, 7, 6, 3, -2 }.Select(v => v / 21.0).ToArray();
        for (int i = 0; i < 7; i++) Assert.AreEqual(expected7o2[i], c7o2[i], 1e-12);

        // Window=9, order=2: [-21, 14, 39, 54, 59, 54, 39, 14, -21] / 231
        var c9o2 = SavitzkyGolaySmoother.GenerateCoefficients(4, 4, 2);
        var expected9o2 = new[] { -21, 14, 39, 54, 59, 54, 39, 14, -21 }.Select(v => v / 231.0).ToArray();
        for (int i = 0; i < 9; i++) Assert.AreEqual(expected9o2[i], c9o2[i], 1e-12);

        // Window=11, order=4: [18, -45, -10, 60, 120, 143, 120, 60, -10, -45, 18] / 429
        var c11o4 = SavitzkyGolaySmoother.GenerateCoefficients(5, 5, 4);
        var expected11o4 = new[] { 18, -45, -10, 60, 120, 143, 120, 60, -10, -45, 18 }.Select(v => v / 429.0).ToArray();
        for (int i = 0; i < 11; i++) Assert.AreEqual(expected11o4[i], c11o4[i], 1e-12);
    }

    [TestMethod]
    public void SavitzkyGolaySmoother_ConstructorRejectsBadInputs()
    {
        Assert.ThrowsException<ArgumentException>(() => new SavitzkyGolaySmoother(1, 5));    // order < 2
        Assert.ThrowsException<ArgumentException>(() => new SavitzkyGolaySmoother(21, 25));  // order > 20
        Assert.ThrowsException<ArgumentException>(() => new SavitzkyGolaySmoother(2, 4));    // even window
        Assert.ThrowsException<ArgumentException>(() => new SavitzkyGolaySmoother(2, 3));    // window < 5
        Assert.ThrowsException<ArgumentException>(() => new SavitzkyGolaySmoother(7, 5));    // order > window
    }

    [TestMethod]
    public void SavitzkyGolaySmoother_Smooth()
    {
        string root = FindSgFixtureDir();
        var xRaw = LoadDoubles(Path.Combine(root, "case00_o2_w11.xRaw.txt"));
        var yRaw = LoadDoubles(Path.Combine(root, "case00_o2_w11.yRaw.txt"));
        var xSmoothedExpected = LoadDoubles(Path.Combine(root, "case00_o2_w11.xSmoothed.txt"));
        var ySmoothedExpected = LoadDoubles(Path.Combine(root, "case00_o2_w11.ySmoothed.txt"));

        Assert.AreEqual(xRaw.Length, yRaw.Length, "xRaw / yRaw length mismatch");
        Assert.AreEqual(xSmoothedExpected.Length, ySmoothedExpected.Length);

        var xOut = new List<double>();
        var yOut = new List<double>();
        new SavitzkyGolaySmoother(polynomialOrder: 2, windowSize: 11).Smooth(xRaw, yRaw, xOut, yOut);

        Assert.AreEqual(xSmoothedExpected.Length, xOut.Count);
        Assert.AreEqual(ySmoothedExpected.Length, yOut.Count);
        for (int j = 0; j < yOut.Count; j++)
            Assert.AreEqual(ySmoothedExpected[j], yOut[j], 1e-5, $"smoothed[{j}] diverged");
    }

    private static string FindSgFixtureDir()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string c = Path.Combine(dir, "test", "Analysis.Tests", "SpectrumProcessing", "SavitzkyGolayTest.data");
            if (Directory.Exists(c)) return c;
            dir = Path.GetDirectoryName(dir);
        }
        Assert.Inconclusive("SavitzkyGolayTest.data not found");
        throw new InvalidOperationException("unreachable");
    }

    private static double[] LoadDoubles(string path)
    {
        // Match cpp parseDoubleArray: clamp negatives to 0.
        return File.ReadAllText(path)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => System.Math.Max(0.0, double.Parse(t, System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
    }

    [TestMethod]
    public void ZeroSampleFiller_PadsOpenFlanks()
    {
        // A peak already flanked by zeros needs no insert.
        var x = new double[] { 100.0, 100.1, 100.2, 100.3, 100.4 };
        var y = new double[] {   0.0,   0.0,   5.0,   0.0,   0.0 };
        var xOut = new List<double>();
        var yOut = new List<double>();
        ZeroSampleFiller.Fill(x, y, xOut, yOut, zeroSampleCount: 1);
        CollectionAssert.AreEqual(x, xOut);
        CollectionAssert.AreEqual(y, yOut);

        // A peak at the last index (no trailing zero) gets a zero appended at the next sample step.
        var openX = new double[] { 100.0, 100.1, 100.2 };
        var openY = new double[] {   0.0,   0.0,   5.0 };
        var openXOut = new List<double>();
        var openYOut = new List<double>();
        ZeroSampleFiller.Fill(openX, openY, openXOut, openYOut, zeroSampleCount: 1);
        Assert.AreEqual(4, openXOut.Count);
        Assert.AreEqual(100.3, openXOut[3], 1e-9);
        Assert.AreEqual(0.0, openYOut[3]);
    }

    [TestMethod]
    public void LocalMaximumPeakDetector_SinglePeakAndTwoPeaks()
    {
        // Single triangular peak → one detected peak at the apex.
        var triangleX = new double[] { 100.0, 100.1, 100.2, 100.3, 100.4 };
        var triangleY = new double[] {   1.0,   2.0,   5.0,   2.0,   1.0 };
        var trianglePeaksX = new List<double>();
        var trianglePeaksY = new List<double>();
        new LocalMaximumPeakDetector(3).Detect(triangleX, triangleY, trianglePeaksX, trianglePeaksY);
        Assert.AreEqual(1, trianglePeaksX.Count);
        Assert.AreEqual(100.2, trianglePeaksX[0], 1e-9);
        Assert.AreEqual(5.0, trianglePeaksY[0], 1e-9);

        // Two peaks separated by zero gap → both detected at their apexes.
        var twoX = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }.Select(d => (double)d).ToArray();
        var twoY = new double[] { 0, 1, 5, 1, 0, 1, 7, 1, 0 };
        var twoPeaksX = new List<double>();
        var twoPeaksY = new List<double>();
        new LocalMaximumPeakDetector(3).Detect(twoX, twoY, twoPeaksX, twoPeaksY);
        Assert.AreEqual(2, twoPeaksX.Count);
        Assert.AreEqual(3.0, twoPeaksX[0], 1e-9);
        Assert.AreEqual(7.0, twoPeaksX[1], 1e-9);
    }

    [TestMethod]
    public void LocalMaximumPeakDetector_EmptyInput_ReturnsNoPeaks()
    {
        var xPeaks = new List<double>();
        var yPeaks = new List<double>();
        new LocalMaximumPeakDetector(3).Detect(
            Array.Empty<double>(), Array.Empty<double>(), xPeaks, yPeaks);
        Assert.AreEqual(0, xPeaks.Count);
    }
}
