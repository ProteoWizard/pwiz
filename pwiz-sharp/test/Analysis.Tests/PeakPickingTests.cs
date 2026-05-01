using Pwiz.Analysis.PeakPicking;

namespace Pwiz.Analysis.Tests;

[TestClass]
public class PeakPickingTests
{
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
