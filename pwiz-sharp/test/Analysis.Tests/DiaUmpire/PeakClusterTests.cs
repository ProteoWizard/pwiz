using Pwiz.Analysis.DiaUmpire;

namespace Pwiz.Analysis.Tests.DiaUmpire;

/// <summary>Unit tests for <see cref="PeakCluster"/>, <see cref="PeakCurveClusteringCorrKDtree"/>, and friends.</summary>
[TestClass]
public class PeakClusterTests
{
    private static PeakCurve MakeCurve(InstrumentParameter p, float mz, float apexRt, float apexInt, int index)
    {
        var curve = new PeakCurve(p) { Index = index, MsLevel = 1 };
        for (int i = 0; i < 21; i++)
        {
            float rt = apexRt - 1f + i * 0.1f;
            float dist = System.Math.Abs(rt - apexRt);
            float intensity = System.Math.Max(1f, apexInt - dist * apexInt);
            curve.AddPeak(new XYZData(rt, mz, intensity));
        }
        curve.DoInterpolation();
        return curve;
    }

    [TestMethod]
    public void PeakCluster_Construction_AllocatesIsotopeBuffers()
    {
        var gof = new ChiSquareGOF(5);
        var cluster = new PeakCluster(isotopicNum: 4, charge: 2, gof);
        Assert.AreEqual(4, cluster.IsoPeaksCurves.Length);
        Assert.AreEqual(3, cluster.Corrs.Length);
        Assert.AreEqual(4, cluster.Mz.Length);
        Assert.AreEqual(2, cluster.Charge);
        Assert.AreEqual(-1f, cluster.GetSNR(0));
    }

    [TestMethod]
    public void PeakCluster_NeutralMass_FromMonoCurve()
    {
        var gof = new ChiSquareGOF(5);
        var p = new Config().InstrumentParameters;
        var mono = MakeCurve(p, mz: 500.25f, apexRt: 2f, apexInt: 1000, index: 1);

        var cluster = new PeakCluster(isotopicNum: 4, charge: 2, gof) { MonoIsotopePeak = mono };
        cluster.IsoPeaksCurves[0] = mono;
        // (500.25 - 1.00727) * 2 ≈ 998.485
        Assert.AreEqual(998.485f, cluster.NeutralMass(), 0.02);
    }

    [TestMethod]
    public void PeakCluster_CalcPeakAreaV2_PopulatesHeightAndArea()
    {
        var gof = new ChiSquareGOF(5);
        var p = new Config().InstrumentParameters;
        var mono = MakeCurve(p, mz: 500.5f, apexRt: 2f, apexInt: 1000, index: 1);
        var iso1 = MakeCurve(p, mz: 501.0f, apexRt: 2f, apexInt: 500, index: 2);
        var cluster = new PeakCluster(isotopicNum: 3, charge: 2, gof) { MonoIsotopePeak = mono };
        cluster.IsoPeaksCurves[0] = mono;
        cluster.IsoPeaksCurves[1] = iso1;
        cluster.CalcPeakAreaV2();
        Assert.IsTrue(cluster.PeakHeight[0] > 0);
        Assert.IsTrue(cluster.PeakHeight[1] > 0);
        Assert.IsTrue(cluster.PeakArea[0] > 0);
        Assert.AreEqual(500.5f, cluster.Mz[0], 1e-3);
        Assert.AreEqual(501.0f, cluster.Mz[1], 1e-3);
    }

    [TestMethod]
    public void PeakCluster_IsotopeComplete_ChecksByCurveOrMz()
    {
        var gof = new ChiSquareGOF(5);
        var p = new Config().InstrumentParameters;
        var mono = MakeCurve(p, mz: 500.25f, apexRt: 2f, apexInt: 1000, index: 1);
        var cluster = new PeakCluster(isotopicNum: 4, charge: 2, gof) { MonoIsotopePeak = mono };
        cluster.IsoPeaksCurves[0] = mono;
        // Only mono present — at minIso=2 should fail.
        Assert.IsFalse(cluster.IsotopeComplete(2));
        // Add second isotope via Mz array (matches cpp "Mz[i] != 0" fallback).
        cluster.SetMz(1, 500.75f);
        Assert.IsTrue(cluster.IsotopeComplete(2));
    }

    [TestMethod]
    public void PeakCluster_GetMaxMz_ReturnsLargestNonzero()
    {
        var gof = new ChiSquareGOF(5);
        var cluster = new PeakCluster(isotopicNum: 4, charge: 2, gof);
        cluster.SetMz(0, 500f);
        cluster.SetMz(1, 500.5f);
        cluster.SetMz(2, 501f);
        // Mz[3] left zero -> GetMaxMz walks back to 501.
        Assert.AreEqual(501f, cluster.GetMaxMz());
    }

    [TestMethod]
    public void PeakCluster_AddScore_BuildsRankableMatchSet()
    {
        var gof = new ChiSquareGOF(5);
        var cluster = new PeakCluster(isotopicNum: 4, charge: 2, gof);
        cluster.AddScore(0.5f);
        cluster.AddScore(0.7f);
        cluster.AddScore(0.9f);
        // 0.9 is the highest; rank for 0.9 should be 1.
        Assert.AreEqual(1, cluster.GetScoreRank(0.9f));
    }

    [TestMethod]
    public void PeakCurveClusteringCorrKDtree_BuildsClusterFromMonoPlusIso()
    {
        var p = new Config().InstrumentParameters;
        p.CheckMonoIsotopicApex = false;
        p.MassDefectFilter = false; // synthetic mass may fail real filter
        p.IsoPattern = -1; // bypass chi-square gate
        p.IsoCorrThreshold = 0.05f;
        p.MS1PPM = 200; // generous to align cleanly with the synthetic peaks

        // Build mono + +1 isotope. Charge = 2 → spacing = ProtonMass / 2 ≈ 0.5036.
        const float proton = 1.00727646677f;
        const float mz0 = 500.5f;
        const int charge = 2;
        float mz1 = mz0 + proton / charge;

        var mono = MakeCurve(p, mz: mz0, apexRt: 5f, apexInt: 1000, index: 1);
        var iso1 = MakeCurve(p, mz: mz1, apexRt: 5f, apexInt: 400, index: 2);
        var curves = new List<PeakCurve> { mono, iso1 };

        var isotopeMap = new IsotopePatternMap(p);
        var gof = new ChiSquareGOF(p.MaxNoPeakCluster);
        var lockObj = new object();
        var clusterer = new PeakCurveClusteringCorrKDtree(
            peakCurves: curves,
            targetCurveIndex: 0,
            searchablePeakCurves: curves,
            parameter: p,
            isotopePatternMap: isotopeMap,
            chiSquaredGof: gof,
            startCharge: charge, endCharge: charge,
            maxNoClusters: p.MaxNoPeakCluster,
            minNoClusters: 2,
            lockObject: lockObj);
        clusterer.Run();

        Assert.IsTrue(clusterer.ResultClusters.Count >= 1,
            $"expected at least one cluster, got {clusterer.ResultClusters.Count}");
        var built = clusterer.ResultClusters[0];
        Assert.AreEqual(charge, built.Charge);
        Assert.AreSame(mono, built.MonoIsotopePeak);
        Assert.AreSame(iso1, built.IsoPeaksCurves[1]);
    }

    [TestMethod]
    public void PseudoMSMSProcessing_EmptyFragmentsDoesNothing()
    {
        var gof = new ChiSquareGOF(5);
        var p = new Config().InstrumentParameters;
        var cluster = new PeakCluster(isotopicNum: 4, charge: 2, gof);
        var proc = new PseudoMSMSProcessing(cluster, new List<PrecursorFragmentPairEdge>(), p, QualityLevel.Q1IsotopeComplete);
        proc.Run(); // no-op when fragments < 2
        proc.GetScan(out var mz, out var intens);
        Assert.AreEqual(0, mz.Length);
        Assert.AreEqual(0, intens.Length);
    }

    [TestMethod]
    public void PseudoMSMSProcessing_GetScan_EmitsSortedMzIntensity()
    {
        var gof = new ChiSquareGOF(5);
        var p = new Config().InstrumentParameters;
        p.BoostComplementaryIon = false; // keep the fragments untouched
        p.AdjustFragIntensity = false;
        var cluster = new PeakCluster(isotopicNum: 4, charge: 2, gof);
        cluster.SetMz(0, 600);

        var fragments = new List<PrecursorFragmentPairEdge>
        {
            new() { FragmentMz = 150, Intensity = 50, Correlation = 0.8f },
            new() { FragmentMz = 250, Intensity = 80, Correlation = 0.9f },
            new() { FragmentMz = 350, Intensity = 30, Correlation = 0.5f },
        };
        var proc = new PseudoMSMSProcessing(cluster, fragments, p, QualityLevel.Q1IsotopeComplete);
        proc.Run();
        proc.GetScan(out var mzArr, out var intensityArr);
        Assert.AreEqual(3, mzArr.Length);
        Assert.AreEqual(150d, mzArr[0]);
        Assert.AreEqual(250d, mzArr[1]);
        Assert.AreEqual(350d, mzArr[2]);
        Assert.AreEqual(50d, intensityArr[0]);
    }
}
