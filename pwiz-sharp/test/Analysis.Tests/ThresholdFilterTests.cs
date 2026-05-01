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
    public void Count_AndCountAfterTies()
    {
        // Top 3 distinct intensities: drop everything from rank 3 down (ties at the cutoff also dropped).
        var byCount = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 }, new[] { 100.0, 90, 80, 70, 60 });
        new ThresholdFilter(ThresholdingBy.Count, 3).Apply(byCount);
        CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0 }, byCount.GetMZArray()!.Data, "Count: top-3 distinct");

        // Ties at the count cutoff drop ALL tied peaks (Count) but are KEPT (CountAfterTies).
        double[] tieMz = { 1.0, 2.0, 3.0, 4.0 };
        double[] tieInt = { 100.0, 50, 50, 10 };
        var withTiesDropped = MakeSpectrum((double[])tieMz.Clone(), (double[])tieInt.Clone());
        new ThresholdFilter(ThresholdingBy.Count, 3).Apply(withTiesDropped);
        CollectionAssert.AreEqual(new[] { 1.0 }, withTiesDropped.GetMZArray()!.Data,
            "Count: cutoff ties dropped");

        var withTiesKept = MakeSpectrum((double[])tieMz.Clone(), (double[])tieInt.Clone());
        new ThresholdFilter(ThresholdingBy.CountAfterTies, 3).Apply(withTiesKept);
        CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0 }, withTiesKept.GetMZArray()!.Data,
            "CountAfterTies: cutoff ties kept");
    }

    [TestMethod]
    public void AbsoluteIntensity_BothOrientations()
    {
        // MostIntense: keep peaks ≥ threshold.
        var most = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 5.0, 50, 500, 5000 });
        new ThresholdFilter(ThresholdingBy.AbsoluteIntensity, 100).Apply(most);
        CollectionAssert.AreEqual(new[] { 3.0, 4.0 }, most.GetMZArray()!.Data, "most intense");

        // LeastIntense: keep peaks ≤ threshold.
        var least = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 5.0, 50, 500, 5000 });
        new ThresholdFilter(ThresholdingBy.AbsoluteIntensity, 100, ThresholdingOrientation.LeastIntense)
            .Apply(least);
        CollectionAssert.AreEqual(new[] { 1.0, 2.0 }, least.GetMZArray()!.Data, "least intense");
    }

    [TestMethod]
    public void FractionalThresholds_BasePeakAndTotal()
    {
        // FractionOfBasePeakIntensity: base = 1000, threshold 0.1 → keep peaks ≥ 100.
        var ofBase = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 50.0, 200, 500, 1000 });
        new ThresholdFilter(ThresholdingBy.FractionOfBasePeakIntensity, 0.1).Apply(ofBase);
        CollectionAssert.AreEqual(new[] { 2.0, 3.0, 4.0 }, ofBase.GetMZArray()!.Data);

        // FractionOfTotalIntensity: TIC=100, threshold 0.2 → keep peaks ≥ 20.
        var ofTotal = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 5.0, 20, 30, 45 });
        new ThresholdFilter(ThresholdingBy.FractionOfTotalIntensity, 0.2).Apply(ofTotal);
        CollectionAssert.AreEqual(new[] { 2.0, 3.0, 4.0 }, ofTotal.GetMZArray()!.Data);

        // FractionOfTotalIntensityCutoff: keep top peaks until cumulative reaches target.
        // Sorted desc: 45, 30, 20, 5; target 0.8 × 100 = 80; cumulative reaches 95 at 3rd peak.
        var cumulative = MakeSpectrum(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 5.0, 20, 30, 45 });
        new ThresholdFilter(ThresholdingBy.FractionOfTotalIntensityCutoff, 0.8).Apply(cumulative);
        CollectionAssert.AreEqual(new[] { 2.0, 3.0, 4.0 },
            cumulative.GetMZArray()!.Data.OrderBy(x => x).ToList());
    }

    [TestMethod]
    public void MsLevels_RestrictsApplication_AndListWrapperApplies()
    {
        // MS1 spectrum + filter restricted to MS level 2 → no-op.
        var s = MakeSpectrum(new[] { 1.0, 2.0, 3.0 }, new[] { 10.0, 20, 30 }, msLevel: 1);
        new ThresholdFilter(
            ThresholdingBy.AbsoluteIntensity, 100,
            msLevels: new Pwiz.Util.Misc.IntegerSet(2)).Apply(s);
        CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0 }, s.GetMZArray()!.Data);

        // SpectrumListPeakFilter applies the threshold to every spectrum in the list.
        var inner = new SpectrumListSimple();
        for (int i = 0; i < 3; i++)
        {
            var spec = MakeSpectrum(new[] { 1.0, 2.0, 3.0 }, new[] { 10.0, 100, 1000 });
            spec.Index = i; spec.Id = $"scan={i + 1}";
            inner.Spectra.Add(spec);
        }
        var filtered = new SpectrumListPeakFilter(inner,
            new ThresholdFilter(ThresholdingBy.AbsoluteIntensity, 50));
        Assert.AreEqual(3, filtered.Count);
        for (int i = 0; i < 3; i++)
            Assert.AreEqual(2, filtered.GetSpectrum(i, getBinaryData: true).GetMZArray()!.Data.Count);
    }
}
