using Pwiz.Analysis.PeakFilters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests.PeakFilters;

[TestClass]
public class ThresholdFilterTests
{
    private static Spectrum MakeSpectrum(double[] mz, double[] intensity, int msLevel = 2)
    {
        var s = new Spectrum { Index = 0, Id = "test", DefaultArrayLength = mz.Length };
        s.Params.Set(CVID.MS_ms_level, msLevel);
        s.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
        return s;
    }

    [TestMethod]
    public void Count_KeepsTopN_DropsTiesAtCutoff()
    {
        // Intensities 100, 90, 80, 70, 60. Top 3 → 100, 90, 80.
        var s = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 }, new[] { 100.0, 90, 80, 70, 60 });
        new ThresholdFilter(ThresholdingBy.Count, 3).Apply(s);
        CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0 }, s.GetMZArray()!.Data);
    }

    [TestMethod]
    public void Count_TiesAtCutoff_AreAllDropped()
    {
        // Top 3 requested, but rank 3 has a tie → all tied peaks dropped.
        var s = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 100.0, 50, 50, 10 });
        new ThresholdFilter(ThresholdingBy.Count, 3).Apply(s);
        // Only the 100 survives (50s all dropped because they're at the cutoff).
        CollectionAssert.AreEqual(new[] { 1.0 }, s.GetMZArray()!.Data);
    }

    [TestMethod]
    public void CountAfterTies_TiesAtCutoff_AreAllKept()
    {
        // Top 3 requested; ties at rank 3 are preserved.
        var s = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 100.0, 50, 50, 10 });
        new ThresholdFilter(ThresholdingBy.CountAfterTies, 3).Apply(s);
        // Keeps 100 + both 50s.
        CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0 }, s.GetMZArray()!.Data);
    }

    [TestMethod]
    public void AbsoluteIntensity_DropsBelowThreshold()
    {
        var s = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 5.0, 50, 500, 5000 });
        new ThresholdFilter(ThresholdingBy.AbsoluteIntensity, 100).Apply(s);
        CollectionAssert.AreEqual(new[] { 3.0, 4.0 }, s.GetMZArray()!.Data);
    }

    [TestMethod]
    public void AbsoluteIntensity_LeastIntense_DropsAboveThreshold()
    {
        var s = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 5.0, 50, 500, 5000 });
        new ThresholdFilter(ThresholdingBy.AbsoluteIntensity, 100, ThresholdingOrientation.LeastIntense).Apply(s);
        CollectionAssert.AreEqual(new[] { 1.0, 2.0 }, s.GetMZArray()!.Data);
    }

    [TestMethod]
    public void FractionOfBasePeak_KeepsPeaksAboveFraction()
    {
        // Base peak = 1000. Threshold 0.1 → keep peaks ≥ 100.
        var s = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 50.0, 200, 500, 1000 });
        new ThresholdFilter(ThresholdingBy.FractionOfBasePeakIntensity, 0.1).Apply(s);
        CollectionAssert.AreEqual(new[] { 2.0, 3.0, 4.0 }, s.GetMZArray()!.Data);
    }

    [TestMethod]
    public void FractionOfTotalIntensity_KeepsPeaksAboveFraction()
    {
        // Sum = 100. Threshold 0.2 → keep peaks ≥ 20.
        var s = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 5.0, 20, 30, 45 });
        new ThresholdFilter(ThresholdingBy.FractionOfTotalIntensity, 0.2).Apply(s);
        CollectionAssert.AreEqual(new[] { 2.0, 3.0, 4.0 }, s.GetMZArray()!.Data);
    }

    [TestMethod]
    public void FractionOfTotalIntensityCutoff_KeepsTopUntilCumulativeReachesTarget()
    {
        // Intensities sorted desc: 45, 30, 20, 5. TIC=100. Target 0.8 × 100 = 80.
        // Cumulative: 45 (<80), 45+30=75 (<80), 75+20=95 (≥80 — stop).
        // So we keep the peaks with intensities 45, 30, 20 → m/z 4, 3, 2.
        var s = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 5.0, 20, 30, 45 });
        new ThresholdFilter(ThresholdingBy.FractionOfTotalIntensityCutoff, 0.8).Apply(s);
        CollectionAssert.AreEqual(new[] { 2.0, 3.0, 4.0 }, s.GetMZArray()!.Data.OrderBy(x => x).ToList());
    }

    [TestMethod]
    public void MsLevels_RestrictsApplication()
    {
        // MS1 spectrum; filter only targets MS level 2 → no-op.
        var s = MakeSpectrum(new[] { 1.0, 2.0, 3.0 }, new[] { 10.0, 20, 30 }, msLevel: 1);
        var filter = new ThresholdFilter(
            ThresholdingBy.AbsoluteIntensity, 100,
            msLevels: new Pwiz.Util.Misc.IntegerSet(2));
        filter.Apply(s);

        // Nothing should change.
        CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0 }, s.GetMZArray()!.Data);
    }

    [TestMethod]
    public void SpectrumListPeakFilter_AppliesToAllSpectra()
    {
        var inner = new SpectrumListSimple();
        for (int i = 0; i < 3; i++)
        {
            var s = MakeSpectrum(new[] { 1.0, 2.0, 3.0 }, new[] { 10.0, 100, 1000 });
            s.Index = i; s.Id = $"scan={i + 1}";
            inner.Spectra.Add(s);
        }

        var filtered = new SpectrumListPeakFilter(
            inner,
            new ThresholdFilter(ThresholdingBy.AbsoluteIntensity, 50));
        Assert.AreEqual(3, filtered.Count);

        for (int i = 0; i < 3; i++)
        {
            var spec = filtered.GetSpectrum(i, getBinaryData: true);
            Assert.AreEqual(2, spec.GetMZArray()!.Data.Count);
        }
    }
}
