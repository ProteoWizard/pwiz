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
    public void ZeroSampleFiller_FillsInteriorGapsAndTermini()
    {
        // ZeroSampleFiller_PadsOpenFlanks covers one data run at zeroSampleCount 1. These add
        // wider windows and, from case 3 on, TWO data runs per spectrum - the case where the
        // insertions made for the trailing run change the array length seen while the leading
        // run is still being processed. Expected outputs are the reference implementation's.

        // raw data are preserved when nothing is missing
        CheckFill(1,
            new double[] { 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 },
            new double[] { 0, 10, 20, 30, 40, 50, 40, 30, 20, 10, 0 },
            new double[] { 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 },
            new double[] { 0, 10, 20, 30, 40, 50, 40, 30, 20, 10, 0 });

        // array termini are filled out to the full window
        CheckFill(5,
            new double[] { 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 },
            new double[] { 0, 10, 20, 30, 40, 50, 40, 30, 20, 10, 0 },
            new double[] { 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34 },
            new double[] { 0, 0, 0, 0, 0, 10, 20, 30, 40, 50, 40, 30, 20, 10, 0, 0, 0, 0, 0 });

        // two runs, interior gap wider than the window
        CheckFill(2,
            new double[] { 20, 21, 22, 23, 24, 27, 28, 29, 30 },
            new double[] { 0, 10, 20, 10, 0, 0, 10, 10, 0 },
            new double[] { 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 },
            new double[] { 0, 0, 10, 20, 10, 0, 0, 0, 0, 10, 10, 0, 0 });

        // two runs, interior gap exactly the window
        CheckFill(2,
            new double[] { 20, 21, 22, 23, 24, 26, 27, 28, 29 },
            new double[] { 0, 10, 20, 10, 0, 0, 10, 10, 0 },
            new double[] { 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 },
            new double[] { 0, 0, 10, 20, 10, 0, 0, 0, 10, 10, 0, 0 });

        // two runs, no interior gap to fill
        CheckFill(2,
            new double[] { 20, 21, 22, 23, 24, 25, 26, 27 },
            new double[] { 0, 10, 20, 10, 0, 10, 10, 0 },
            new double[] { 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 },
            new double[] { 0, 0, 10, 20, 10, 0, 10, 10, 0, 0 });

        // two runs far apart, so the sample rate rather than the gap decides the fill
        CheckFill(2,
            new double[] { 1.000001, 1.000002, 1.000003, 1000.001, 1000.002, 1000.003 },
            new double[] { 0, 1, 0, 0, 1, 0 },
            new double[] { 1.000000, 1.000001, 1.000002, 1.000003, 1.000004, 1000.000, 1000.001, 1000.002, 1000.003, 1000.004 },
            new double[] { 0, 0, 1, 0, 0, 0, 0, 1, 0, 0 });

        // same, with a second-order delta in the sample spacing
        CheckFill(2,
            new double[] { 1.000001, 1.000002, 1.0000035, 1000.00001, 1000.00002, 1000.000035 },
            new double[] { 0, 1, 0, 0, 1, 0 },
            new double[] { 1.000000, 1.000001, 1.000002, 1.0000035, 1.0000050, 1000.000, 1000.00001, 1000.00002, 1000.000035, 1000.000050 },
            new double[] { 0, 0, 1, 0, 0, 0, 0, 1, 0, 0 });
    }

    private static void CheckFill(int zeroSampleCount, double[] xRaw, double[] yRaw,
                                  double[] xExpected, double[] yExpected)
    {
        Assert.AreEqual(xRaw.Length, yRaw.Length, "x and y raw lengths must match");
        Assert.AreEqual(xExpected.Length, yExpected.Length, "x and y expected lengths must match");

        var xFilled = new List<double>();
        var yFilled = new List<double>();
        ZeroSampleFiller.Fill(xRaw, yRaw, xFilled, yFilled, zeroSampleCount);

        string what = "zeroSampleCount=" + zeroSampleCount + ", raw length " + xRaw.Length;
        Assert.AreEqual(xExpected.Length, xFilled.Count, "filled length: " + what);
        Assert.AreEqual(xFilled.Count, yFilled.Count, "filled x/y length mismatch: " + what);
        for (int i = 0; i < xFilled.Count; i++)
        {
            Assert.AreEqual(xExpected[i], xFilled[i], 1e-5, "x[" + i + "]: " + what);
            Assert.AreEqual(yExpected[i], yFilled[i], 1e-5, "y[" + i + "]: " + what);
        }
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
