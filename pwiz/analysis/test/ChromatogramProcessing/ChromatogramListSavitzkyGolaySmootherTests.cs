using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests.ChromatogramProcessing;

/// <summary>
/// Verifies the chromatogram-flavor Savitzky-Golay smoother matches cpp's hardcoded
/// quartic 9-point convolution: <c>[-21, 14, 39, 54, 59, 54, 39, 14, -21] / 231</c>.
/// </summary>
[TestClass]
public class ChromatogramListSavitzkyGolaySmootherTests
{
    [TestMethod]
    public void Smooth_ImpulseAtCenter_MatchesCppCoefficients()
    {
        // Single impulse at index 8 (the center of a 17-sample series). The smoothed value
        // at index 8 is 59/231; at indices 8±k it's coefficient_k/231.
        var inner = new ChromatogramListSimple();
        var c = new Chromatogram { Index = 0, Id = "impulse", DefaultArrayLength = 17 };
        var time = new BinaryDataArray();
        time.Set(CVID.MS_time_array, string.Empty, CVID.UO_second);
        var inten = new BinaryDataArray();
        inten.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
        for (int j = 0; j < 17; j++) { time.Data.Add(j * 0.1); inten.Data.Add(j == 8 ? 231.0 : 0.0); }
        c.BinaryDataArrays.Add(time);
        c.BinaryDataArrays.Add(inten);
        inner.Chromatograms.Add(c);

        var smoothed = new ChromatogramListSavitzkyGolaySmoother(inner).GetChromatogram(0, getBinaryData: true);
        var I = smoothed.GetIntensityArray()!.Data;

        // With 231-scaled impulse, expect raw integer coefficients on the kernel.
        Assert.AreEqual(-21.0, I[4], 1e-9);
        Assert.AreEqual(14.0, I[5], 1e-9);
        Assert.AreEqual(39.0, I[6], 1e-9);
        Assert.AreEqual(54.0, I[7], 1e-9);
        Assert.AreEqual(59.0, I[8], 1e-9);
        Assert.AreEqual(54.0, I[9], 1e-9);
        Assert.AreEqual(39.0, I[10], 1e-9);
        Assert.AreEqual(14.0, I[11], 1e-9);
        Assert.AreEqual(-21.0, I[12], 1e-9);
        // Edges (first 4, last 4) pass through unchanged.
        for (int j = 0; j < 4; j++) Assert.AreEqual(0.0, I[j], 1e-9, $"edge at {j}");
        for (int j = 13; j < 17; j++) Assert.AreEqual(0.0, I[j], 1e-9, $"edge at {j}");
    }

    [TestMethod]
    public void Smooth_TooShort_PassesThroughUnchanged()
    {
        // Chromatograms with fewer than 9 points are returned as-is.
        var inner = new ChromatogramListSimple();
        var c = new Chromatogram { Index = 0, Id = "short", DefaultArrayLength = 5 };
        var time = new BinaryDataArray();
        time.Set(CVID.MS_time_array, string.Empty, CVID.UO_second);
        var inten = new BinaryDataArray();
        inten.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
        var expected = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
        for (int j = 0; j < 5; j++) { time.Data.Add(j); inten.Data.Add(expected[j]); }
        c.BinaryDataArrays.Add(time);
        c.BinaryDataArrays.Add(inten);
        inner.Chromatograms.Add(c);

        var smoothed = new ChromatogramListSavitzkyGolaySmoother(inner).GetChromatogram(0, getBinaryData: true);
        CollectionAssert.AreEqual(expected, smoothed.GetIntensityArray()!.Data);
    }

    [TestMethod]
    public void NoBinaryData_DoesNotTouchInnerArrays()
    {
        // getBinaryData=false should bypass smoothing entirely — inner.GetChromatogram
        // is asked for metadata-only and returned as-is.
        var inner = new ChromatogramListSimple();
        var c = new Chromatogram { Index = 0, Id = "x" };
        inner.Chromatograms.Add(c);
        var s = new ChromatogramListSavitzkyGolaySmoother(inner).GetChromatogram(0, getBinaryData: false);
        Assert.AreSame(c, s); // ChromatogramListSimple's GetChromatogram returns the same instance
    }

    [TestMethod]
    public void DataProcessing_GainsSmoothingMethod()
    {
        var inner = ChromatogramListFilterTests.Build();
        var sg = new ChromatogramListSavitzkyGolaySmoother(inner);
        Assert.IsNotNull(sg.DataProcessing);
        Assert.IsTrue(sg.DataProcessing!.ProcessingMethods.Any(pm => pm.HasCVParam(CVID.MS_smoothing)),
            "expected MS_smoothing CVParam in the wrapper's DataProcessing chain");
    }
}
