using Pwiz.Analysis.DiaUmpire;

namespace Pwiz.Analysis.Tests.DiaUmpire;

/// <summary>Unit tests for <see cref="PeakCurve"/> and <see cref="PeakCurveCorrCalc"/>.</summary>
[TestClass]
public class PeakCurveTests
{
    private static PeakCurve MakeSyntheticCurve(InstrumentParameter p, float apexRt = 5f, float apexMz = 500.5f)
    {
        // Triangular XIC peaked at apexRt. 21 raw points across [apexRt-1, apexRt+1].
        var curve = new PeakCurve(p) { Index = 1, MsLevel = 1 };
        for (int i = 0; i < 21; i++)
        {
            float rt = apexRt - 1f + i * 0.1f;
            float dist = System.Math.Abs(rt - apexRt);
            float intensity = System.Math.Max(0f, 1000f - dist * 1000f); // triangle from 0 to 1000
            if (intensity == 0) intensity = 1; // avoid degenerate "all zero" tails
            curve.AddPeak(new XYZData(rt, apexMz, intensity));
        }
        return curve;
    }

    [TestMethod]
    public void AddPeak_TracksApexAndTargetMz()
    {
        var p = new Config().InstrumentParameters;
        var curve = MakeSyntheticCurve(p);
        Assert.AreEqual(1000f, curve.ApexInt);
        Assert.AreEqual(5f, curve.ApexRT, 1e-3);
        Assert.AreEqual(500.5f, curve.TargetMz, 1e-3);
        Assert.AreEqual(21, curve.GetPeakList().Count);
    }

    [TestMethod]
    public void AddPeak_RejectsOutOfOrderTime()
    {
        var p = new Config().InstrumentParameters;
        var curve = new PeakCurve(p);
        curve.AddPeak(new XYZData(1f, 500, 10));
        Assert.ThrowsException<System.InvalidOperationException>(
            () => curve.AddPeak(new XYZData(0.5f, 500, 5)));
    }

    [TestMethod]
    public void RTWidth_IsLastMinusFirst()
    {
        var p = new Config().InstrumentParameters;
        var curve = MakeSyntheticCurve(p);
        Assert.AreEqual(2.0f, curve.RTWidth(), 1e-3);
    }

    [TestMethod]
    public void DoInterpolation_ProducesSmoothedXIC()
    {
        var p = new Config().InstrumentParameters;
        var curve = MakeSyntheticCurve(p);
        curve.DoInterpolation();
        Assert.IsTrue(curve.GetSmoothedList().PointCount() > 0);
        // smoothed apex should still be near the raw apex.
        float maxY = 0;
        float maxX = 0;
        foreach (var pt in curve.GetSmoothedList().Data)
            if (pt.Y > maxY) { maxY = pt.Y; maxX = pt.X; }
        Assert.IsTrue(System.Math.Abs(maxX - 5f) < 0.3f);
    }

    [TestMethod]
    public void DoBspline_ProducesSmoothedXIC()
    {
        var p = new Config().InstrumentParameters;
        var curve = MakeSyntheticCurve(p);
        curve.DoBspline();
        Assert.IsTrue(curve.GetSmoothedList().PointCount() > 0);
    }

    [TestMethod]
    public void DetectPeakRegion_FindsAtLeastOneRegion()
    {
        var p = new Config().InstrumentParameters;
        // make the curve wider/longer so wavelet has scales to work with
        var curve = new PeakCurve(p) { Index = 1, MsLevel = 1 };
        for (int i = 0; i < 81; i++)
        {
            float rt = i * 0.05f; // 0..4 min @ 0.05 min spacing
            float dist = System.Math.Abs(rt - 2f);
            float intensity = System.Math.Max(1f, 1000f - dist * 500f);
            curve.AddPeak(new XYZData(rt, 500.5f, intensity));
        }
        curve.DoInterpolation();
        curve.DetectPeakRegion();
        Assert.IsTrue(curve.GetPeakRegionList().Count >= 1,
            $"expected at least one region; got {curve.GetPeakRegionList().Count}");
    }

    [TestMethod]
    public void CalculateMzVar_MatchesCppRtMinusTargetMzVariance()
    {
        // cpp's CalculateMzVar uses PeakList[j].getX() (= RT, not mz) in the sum (the cpp
        // comment says m/z variance but the field accessed is rt — preserve that here).
        var p = new Config().InstrumentParameters;
        var curve = MakeSyntheticCurve(p);
        curve.CalculateMzVar();
        // Expected: variance of RT samples around TargetMz (which is constant at apexMz).
        var pts = curve.GetPeakList();
        double sum = 0;
        foreach (var pt in pts) sum += (pt.X - curve.TargetMz) * (pt.X - curve.TargetMz);
        sum /= pts.Count;
        Assert.AreEqual(sum, curve.MzVar, 1.0);
        // It is NOT zero — that confirms cpp parity bug is preserved.
        Assert.IsTrue(curve.MzVar > 100);
    }

    [TestMethod]
    public void GetSmoothPeakCollection_ClipsToRange()
    {
        var p = new Config().InstrumentParameters;
        var curve = MakeSyntheticCurve(p);
        curve.DoInterpolation();
        var clipped = curve.GetSmoothPeakCollection(4.5f, 5.5f);
        Assert.IsTrue(clipped.PointCount() > 0);
        foreach (var pt in clipped.Data)
        {
            Assert.IsTrue(pt.X >= 4.5f && pt.X <= 5.5f);
        }
    }

    [TestMethod]
    public void PeakCurveCorrCalc_IdenticalCurves_HighCorrelation()
    {
        var p = new Config().InstrumentParameters;
        var a = MakeSyntheticCurve(p);
        var b = MakeSyntheticCurve(p);
        a.DoInterpolation();
        b.DoInterpolation();
        float corr = PeakCurveCorrCalc.CalPeakCorr(a, b, p.NoPeakPerMin);
        Assert.IsTrue(corr > 0.9f, $"expected high correlation for identical curves, got {corr}");
    }
}
