using Pwiz.Analysis.DiaUmpire;

namespace Pwiz.Analysis.Tests.DiaUmpire;

/// <summary>Unit tests for the math helpers in <c>DiaUmpireMath.cs</c>.</summary>
[TestClass]
public class DiaUmpireMathTests
{
    [TestMethod]
    public void FpCompare_BehavesAsCpp()
    {
        Assert.IsTrue(FpCompare.IsApproximatelyEqual(1.0f, 1.0f + 1e-7f, 1e-5f));
        Assert.IsFalse(FpCompare.IsApproximatelyEqual(1.0f, 2.0f, 1e-5f));
        Assert.IsTrue(FpCompare.IsApproximatelyZero(1e-10f, 1e-9f));
        Assert.IsFalse(FpCompare.IsApproximatelyZero(0.1f, 1e-9f));
        // The "definitely less than" branch with default orEqualTo=false returns false for tiny diffs (cpp parity).
        Assert.IsFalse(FpCompare.IsDefinitelyLessThan(1.0f, 1.0f + 1e-9f, 1e-7f));
        Assert.IsTrue(FpCompare.IsDefinitelyLessThan(1.0f, 1.0f + 1e-9f, 1e-7f, orEqualTo: true));
        Assert.IsFalse(FpCompare.IsDefinitelyGreaterThan(1.0f, 1.0f, 1e-7f));
    }

    [TestMethod]
    public void BSpline_ReducesNoise()
    {
        // Build a sinusoid + noise; B-spline smoothing should reduce the RMS deviation from
        // the underlying signal.
        var noisy = new XYPointCollection();
        var clean = new XYPointCollection();
        var rng = new System.Random(42);
        for (int i = 0; i < 50; i++)
        {
            float x = i * 0.1f;
            float y = (float)System.Math.Sin(x);
            float n = (float)((rng.NextDouble() - 0.5) * 0.4);
            noisy.AddPoint(x, y + n);
            clean.AddPoint(x, y);
        }
        var smoothed = new BSpline().Run(noisy, ptNum: 100, smoothDegree: 2, logId: 0);
        Assert.IsTrue(smoothed.PointCount() > 0);
        // RMS error vs. clean signal: smoothed should be closer than the raw noisy signal.
        double rmsNoisy = RmsAgainstSine(noisy);
        double rmsSmoothed = RmsAgainstSine(smoothed);
        Assert.IsTrue(rmsSmoothed < rmsNoisy,
            $"smoothed RMS ({rmsSmoothed:F4}) should be less than noisy RMS ({rmsNoisy:F4})");
    }

    private static double RmsAgainstSine(XYPointCollection c)
    {
        double sum = 0;
        int n = 0;
        foreach (var p in c.Data)
        {
            double truth = System.Math.Sin(p.X);
            sum += (p.Y - truth) * (p.Y - truth);
            n++;
        }
        return System.Math.Sqrt(sum / n);
    }

    [TestMethod]
    public void BSpline_PassesThroughSmallInputUnchanged()
    {
        // If data.size() <= p (degree), cpp returns data verbatim.
        var data = new XYPointCollection();
        data.AddPoint(1, 1);
        data.AddPoint(2, 2);
        var output = new BSpline().Run(data, 100, smoothDegree: 5, logId: 0);
        Assert.AreSame(data, output);
    }

    [TestMethod]
    public void LinearInterpolation_FillsAcrossThreePoints()
    {
        // 3 source points: (0,0), (5, 100), (10, 0). 11 output samples; midpoint must be filled.
        var input = new XYPointCollection();
        input.AddPoint(0, 0);
        input.AddPoint(5, 100);
        input.AddPoint(10, 0);
        var output = new LinearInterpolation().Run(input, ptNum: 11);
        Assert.AreEqual(11, output.PointCount());
        // The high-intensity bin should land somewhere near the middle of the output.
        int maxIdx = 0;
        for (int i = 1; i < output.PointCount(); i++)
            if (output.Data[i].Y > output.Data[maxIdx].Y) maxIdx = i;
        Assert.IsTrue(maxIdx >= 3 && maxIdx <= 7, $"max found at idx {maxIdx}, expected near middle");
    }

    [TestMethod]
    public void PearsonCorr_PerfectlyCorrelated()
    {
        // y = 2x: positive linear relationship → R^2 ≈ 1
        var a = new XYPointCollection();
        var b = new XYPointCollection();
        for (int i = 0; i < 50; i++)
        {
            a.AddPoint(i * 0.1f, i);
            b.AddPoint(i * 0.1f, i);
        }
        float r2 = PearsonCorr.CalcCorr(a, b, noPointPerInterval: 10);
        Assert.IsTrue(r2 > 0.95f, $"expected R^2 > 0.95 for identical signals, got {r2}");
    }

    [TestMethod]
    public void PearsonCorr_AntiCorrelated_ReturnsZero()
    {
        // The CalcCorr math requires Mvalue > 0 to compute R^2; anticorrelated input means slope < 0 → 0.
        var a = new XYPointCollection();
        var b = new XYPointCollection();
        for (int i = 0; i < 50; i++)
        {
            a.AddPoint(i * 0.1f, i);
            b.AddPoint(i * 0.1f, 50 - i);
        }
        float r2 = PearsonCorr.CalcCorr(a, b, noPointPerInterval: 10);
        Assert.AreEqual(0f, r2);
    }

    [TestMethod]
    public void Regression_FitsLine()
    {
        // y = 2x + 1 — exact fit; slope ~ 2, intercept ~ 1.
        var pts = new XYPointCollection();
        for (int i = 0; i < 10; i++)
            pts.AddPoint(i, 2 * i + 1);
        var reg = new Regression(pts);
        Assert.AreEqual(2.0f, reg.EquationResult.Mvalue, 1e-4);
        Assert.AreEqual(1.0f, reg.EquationResult.Bvalue, 1e-4);
        Assert.AreEqual(1.0f, reg.GetR2(), 1e-4);
        Assert.IsTrue(reg.Valid());
    }

    [TestMethod]
    public void Regression_GetXGetY_AreInverse()
    {
        var pts = new XYPointCollection();
        for (int i = 0; i < 5; i++) pts.AddPoint(i, 3 * i + 7);
        var reg = new Regression(pts);
        Assert.AreEqual(7f, reg.GetY(0), 1e-4);
        Assert.AreEqual(0f, reg.GetX(7), 1e-4);
    }

    [TestMethod]
    public void ChiSquareGOF_ReturnsZeroForEmptyOrSingleObservation()
    {
        var gof = new ChiSquareGOF(10);
        // No nonzero observations → 0.
        Assert.AreEqual(0f, gof.GetGoodNessOfFitProb(new float[] { 1, 1, 1 }, new float[] { 0, 0, 0 }));
        // Single nonzero observation → 0 (nopeaks < 2).
        Assert.AreEqual(0f, gof.GetGoodNessOfFitProb(new float[] { 1, 1, 1 }, new float[] { 0.5f, 0, 0 }));
    }

    [TestMethod]
    public void ChiSquareGOF_PerfectMatch_HighProbability()
    {
        // expected == observed → chi^2 statistic = 0 → 1 - CDF(0) = 1.
        var gof = new ChiSquareGOF(10);
        float prob = gof.GetGoodNessOfFitProb(new float[] { 0.7f, 0.5f, 0.3f }, new float[] { 0.7f, 0.5f, 0.3f });
        Assert.IsTrue(prob > 0.99f, $"expected near-1.0 probability for perfect fit, got {prob}");
    }

    [TestMethod]
    public void MassDefect_RangeAcceptanceCheck()
    {
        var md = default(MassDefect);
        // Mass 1000.5 → defect = 0.5; lies inside [~0.32, ~0.59] for d=0.1.
        Assert.IsTrue(md.InMassDefectRange(1000.5f, 0.1f));
        // Mass 1000.0 → defect = 0; lies below the envelope [~0.42, ~0.60] for d=0.01.
        Assert.IsFalse(md.InMassDefectRange(1000.0f, 0.01f));
    }
}
