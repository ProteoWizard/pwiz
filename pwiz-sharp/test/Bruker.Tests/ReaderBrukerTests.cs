using Pwiz.TestHarness;

namespace Pwiz.Vendor.Bruker.Tests;

/// <summary>
/// End-to-end tests modeled on pwiz C++ <c>Reader_Bruker_Test.cpp</c>: each vendor <c>.d</c>
/// directory is read through <see cref="Reader_Bruker"/>, the in-memory <see cref="Pwiz.Data.MsData.MSData"/>
/// is normalized via <see cref="VendorReaderTestHarness"/>, and the result is diffed against the
/// sibling reference mzML shipped with the pwiz test tree.
/// </summary>
[TestClass]
public class ReaderBrukerTests
{
    private static string? FindTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "Bruker",
                "Reader_Bruker_Test.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    [TestMethod]
    public void Harness_MaldiTsf_MatchesReferenceMzMl()
    {
        RunHarness("20percLaser_100fold_1_0_H6_MS.d");
    }

    [TestMethod]
    public void Harness_AutoMsMsTsf_MatchesReferenceMzMl()
    {
        RunHarness("timsTOF_autoMSMS_Urine_50s_neg.d");
    }

    [TestMethod]
    public void Harness_MaldiTsf_Ms1Centroid_MatchesReferenceMzMl()
    {
        // Covers PeakPicking + PreferOnlyMsLevel=1 — reference is "*-ms1-centroid.mzML".
        RunHarness("20percLaser_100fold_1_0_H6_MS.d", config =>
        {
            config.PeakPicking = true;
            config.PreferOnlyMsLevel = 1;
        });
    }

    [TestMethod]
    public void Harness_UrineTsf_Ms2Centroid_MatchesReferenceMzMl()
    {
        RunHarness("timsTOF_autoMSMS_Urine_50s_neg.d", config =>
        {
            config.PeakPicking = true;
            config.PreferOnlyMsLevel = 2;
        });
    }

    [TestMethod]
    public void Harness_UrineTsf_Ms1Centroid_MatchesReferenceMzMl()
    {
        RunHarness("timsTOF_autoMSMS_Urine_50s_neg.d", config =>
        {
            config.PeakPicking = true;
            config.PreferOnlyMsLevel = 1;
        });
    }

    [TestMethod]
    public void Harness_HelaPasefTdf_MatchesReferenceMzMl()
    {
        RunHarness("Hela_QC_PASEF_Slot1-first-6-frames.d");
    }

    [TestMethod]
    public void Harness_HelaPasefTdf_CombineIMS_MatchesReferenceMzMl()
    {
        RunHarness("Hela_QC_PASEF_Slot1-first-6-frames.d", config =>
        {
            config.CombineIonMobilitySpectra = true;
        });
    }

    private static void RunHarness(string fixtureFolderName, Action<ReaderTestConfig>? configure = null)
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("Bruker test data tree not found."); return; }
        if (!Directory.Exists(Path.Combine(root, fixtureFolderName)))
        {
            Assert.Inconclusive($"{fixtureFolderName} not present under test data.");
            return;
        }

        var reader = new Reader_Bruker();
        var config = new ReaderTestConfig();
        configure?.Invoke(config);
        var result = VendorReaderTestHarness.TestReader(
            reader,
            rootPath: root,
            predicate: new IsNamedRawFile(fixtureFolderName),
            config: config);

        if (result.FailedTests > 0)
            Assert.Fail(string.Join('\n', result.FailureMessages));
        Assert.AreEqual(1, result.TotalTests, "harness did not find the fixture");
    }
}
