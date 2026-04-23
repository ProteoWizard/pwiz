using Pwiz.Analysis.PeakPicking;

namespace Pwiz.Analysis.Tests;

[TestClass]
public class PeakPickingTests
{
    [TestMethod]
    public void ZeroSampleFiller_AddsFlankingZeros_AroundSingletonPeak()
    {
        // Single non-zero at index 2 in a dense profile; expect one zero padded on each side
        // at the same sample rate.
        var x = new double[] { 100.0, 100.1, 100.2, 100.3, 100.4 };
        var y = new double[] {   0.0,   0.0,   5.0,   0.0,   0.0 };
        var xOut = new List<double>();
        var yOut = new List<double>();
        ZeroSampleFiller.Fill(x, y, xOut, yOut, zeroSampleCount: 1);

        // With a peak already flanked by zeros, no inserts are needed.
        CollectionAssert.AreEqual(x, xOut);
        CollectionAssert.AreEqual(y, yOut);
    }

    [TestMethod]
    public void ZeroSampleFiller_InsertsZero_AtOpenRightFlank()
    {
        // Peak at the last index with no trailing zero — filler must append one.
        var x = new double[] { 100.0, 100.1, 100.2 };
        var y = new double[] {   0.0,   0.0,   5.0 };
        var xOut = new List<double>();
        var yOut = new List<double>();
        ZeroSampleFiller.Fill(x, y, xOut, yOut, zeroSampleCount: 1);

        Assert.AreEqual(4, xOut.Count);
        Assert.AreEqual(100.3, xOut[3], 1e-9);
        Assert.AreEqual(0.0, yOut[3]);
    }

    [TestMethod]
    public void LocalMaximumPeakDetector_FindsSinglePeak()
    {
        var x = new double[] { 100.0, 100.1, 100.2, 100.3, 100.4 };
        var y = new double[] {   1.0,   2.0,   5.0,   2.0,   1.0 };
        var xPeaks = new List<double>();
        var yPeaks = new List<double>();
        new LocalMaximumPeakDetector(3).Detect(x, y, xPeaks, yPeaks);

        Assert.AreEqual(1, xPeaks.Count);
        Assert.AreEqual(100.2, xPeaks[0], 1e-9);
        Assert.AreEqual(5.0, yPeaks[0], 1e-9);
    }

    [TestMethod]
    public void LocalMaximumPeakDetector_FindsTwoSeparatedPeaks()
    {
        // Two distinct peaks separated by a zero gap — both should be detected.
        var x = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }.Select(d => (double)d).ToArray();
        var y = new double[] { 0, 1, 5, 1, 0, 1, 7, 1, 0 };
        var xPeaks = new List<double>();
        var yPeaks = new List<double>();
        new LocalMaximumPeakDetector(3).Detect(x, y, xPeaks, yPeaks);

        Assert.AreEqual(2, xPeaks.Count);
        Assert.AreEqual(3.0, xPeaks[0], 1e-9);
        Assert.AreEqual(7.0, xPeaks[1], 1e-9);
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
