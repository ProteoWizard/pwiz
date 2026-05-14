using System.IO;
using Pwiz.Analysis.DiaUmpire;
using Pwiz.Data.MsData;

namespace Pwiz.Analysis.Tests.DiaUmpire;

/// <summary>
/// Parser tests for <see cref="Config"/> against the same <c>.params</c> files cpp ships
/// under <c>pwiz/analysis/spectrum_processing/SpectrumList_DiaUmpireTest.data/</c>. Pins
/// the TTOF5600 default block and the user-overlay logic so any reorder/typo in the param
/// list shows up here rather than only at end-to-end test time.
/// </summary>
[TestClass]
public class ConfigTests
{
    [TestMethod]
    public void Default_AppliesTtof5600Baseline()
    {
        var cfg = new Config();
        var p = cfg.InstrumentParameters;
        Assert.AreEqual(30f, p.MS1PPM);
        Assert.AreEqual(40f, p.MS2PPM);
        Assert.AreEqual(2f, p.SN);
        Assert.AreEqual(2f, p.MS2SN);
        Assert.AreEqual(5f, p.MinMSIntensity);
        Assert.AreEqual(1f, p.MinMSMSIntensity);
        Assert.AreEqual(17000, p.Resolution);
        Assert.AreEqual(0.1f, p.RTtol);
        Assert.IsTrue(p.Denoise);
        Assert.IsTrue(p.EstimateBG);
        Assert.IsTrue(p.RemoveGroupedPeaks);
        // Threading defaults applied
        Assert.IsTrue(cfg.MaxThreads > 0);
        Assert.IsTrue(cfg.MaxNestedThreads > 0);
    }

    [TestMethod]
    public void Parse_FixedSwathParamsFile()
    {
        // Write a minimal fixed-SWATH params file to a temp path and parse it.
        string tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".params");
        File.WriteAllText(tmp, """
            # comment
            Thread = 2
            RPmax = 30
            SE.MS1PPM = 10
            SE.MS2PPM = 20
            SE.SN = 3.5
            WindowType = SWATH
            WindowSize = 25
            SE.MaxNoPeakCluster = 5
            """);
        try
        {
            var cfg = new Config(tmp);
            Assert.AreEqual(2, cfg.MaxThreads);
            Assert.AreEqual(30, cfg.InstrumentParameters.RPmax);
            Assert.AreEqual(10f, cfg.InstrumentParameters.MS1PPM);
            Assert.AreEqual(20f, cfg.InstrumentParameters.MS2PPM);
            Assert.AreEqual(3.5f, cfg.InstrumentParameters.SN);
            Assert.AreEqual(TargetWindowScheme.SwathFixed, cfg.DiaTargetWindowScheme);
            Assert.AreEqual(25, cfg.DiaFixedWindowSize);
            // MaxNoPeakCluster propagates to MaxMS2NoPeakCluster (matches cpp).
            Assert.AreEqual(5, cfg.InstrumentParameters.MaxNoPeakCluster);
            Assert.AreEqual(5, cfg.InstrumentParameters.MaxMS2NoPeakCluster);
        }
        finally { File.Delete(tmp); }
    }

    [TestMethod]
    public void Parse_VariableSwathParamsFile_LoadsWindowList()
    {
        string tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".params");
        File.WriteAllText(tmp, """
            WindowType = V_SWATH
            ==window setting begin
            400.0	425.0
            425.0	450.0
            450.0	475.0
            ==window setting end
            SE.MS1PPM = 15
            """);
        try
        {
            var cfg = new Config(tmp);
            Assert.AreEqual(TargetWindowScheme.SwathVariable, cfg.DiaTargetWindowScheme);
            Assert.AreEqual(3, cfg.DiaVariableWindows.Count);
            Assert.AreEqual(400.0f, cfg.DiaVariableWindows[0].MzRange.Begin);
            Assert.AreEqual(425.0f, cfg.DiaVariableWindows[0].MzRange.End);
            Assert.AreEqual(450.0f, cfg.DiaVariableWindows[2].MzRange.Begin);
            Assert.AreEqual(475.0f, cfg.DiaVariableWindows[2].MzRange.End);
            Assert.AreEqual(15f, cfg.InstrumentParameters.MS1PPM);
        }
        finally { File.Delete(tmp); }
    }

    [TestMethod]
    public void Parse_BoolValues_AcceptsTrueFalseAndZeroOne()
    {
        string tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".params");
        File.WriteAllText(tmp, """
            SE.Denoise = true
            SE.EstimateBG = false
            SE.RemoveGroupedPeaks = 1
            ExportPrecursorPeak = 0
            BoostComplementaryIon = TRUE
            """);
        try
        {
            var cfg = new Config(tmp);
            Assert.IsTrue(cfg.InstrumentParameters.Denoise);
            Assert.IsFalse(cfg.InstrumentParameters.EstimateBG);
            Assert.IsTrue(cfg.InstrumentParameters.RemoveGroupedPeaks);
            Assert.IsFalse(cfg.ExportMs1ClusterTable);
            Assert.IsTrue(cfg.InstrumentParameters.BoostComplementaryIon);
        }
        finally { File.Delete(tmp); }
    }

    [TestMethod]
    public void Parse_MissingFile_Throws()
    {
        Assert.ThrowsException<FileNotFoundException>(() => new Config("doesntexist.params"));
    }

    [TestMethod]
    public void Parse_RealCppFixturesFile_DoesntCrash()
    {
        // Walks up to the cpp test-data root to verify both .params files cpp ships
        // parse without exception. Skips if the cpp tree isn't present.
        string? root = FindCppParamsDir();
        if (root is null) { Assert.Inconclusive("cpp DiaUmpire test-data dir not found"); return; }

        foreach (string file in Directory.EnumerateFiles(root, "*.params"))
        {
            var cfg = new Config(file);
            Assert.IsTrue(cfg.InstrumentParameters.MS1PPM > 0, $"MS1PPM zero after parsing {Path.GetFileName(file)}");
            // 6600_64var ships a variable window list; 5600_32fix ships fixed.
            if (Path.GetFileName(file).Contains("var"))
            {
                Assert.AreEqual(TargetWindowScheme.SwathVariable, cfg.DiaTargetWindowScheme);
                Assert.IsTrue(cfg.DiaVariableWindows.Count > 0);
            }
            else
            {
                Assert.AreEqual(TargetWindowScheme.SwathFixed, cfg.DiaTargetWindowScheme);
                Assert.IsTrue(cfg.DiaFixedWindowSize > 0);
            }
        }
    }

    private static string? FindCppParamsDir()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "analysis", "spectrum_processing",
                "SpectrumList_DiaUmpireTest.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
