using Pwiz.TestHarness;

namespace Pwiz.Vendor.Waters.Tests;

/// <summary>
/// End-to-end tests modeled on pwiz C++ <c>Reader_Waters_Test.cpp</c>: each vendor
/// <c>.raw</c> directory is read through <see cref="Reader_Waters"/>, the in-memory
/// <see cref="Pwiz.Data.MsData.MSData"/> is normalized via <see cref="VendorReaderTestHarness"/>,
/// and the result is diffed against the sibling reference mzML shipped with the pwiz test tree.
/// </summary>
[TestClass]
public class ReaderWatersTests
{
    private static string? FindTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "Waters",
                "Reader_Waters_Test.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    [TestMethod]
    public void Harness_NfdmRaw_MatchesReferenceMzMl()
    {
        // Smallest fixture (~ 41 spectra in the reference mzML) — exercises the basic
        // (function, scan) flow without ion mobility or DDA. Good first parity target.
        RunHarness("091204_NFDM_008.raw");
    }

    [TestMethod]
    public void Harness_Mix1CalCurveRaw_MatchesReferenceMzMl()
    {
        // 24-transition MRM acquisition; exercises the SRM chromatogram path
        // (1 TIC + 24 SRM SIC chromatograms).
        RunHarness("160109_Mix1_calcurve_070.raw");
    }

    [TestMethod]
    public void Harness_AtehlstlsekProfile_MatchesReferenceMzMl()
    {
        // 8 spectra of profile-mode TOF MS — exercises the IsContinuum=true path.
        RunHarness("ATEHLSTLSEK_profile.raw");
    }

    [TestMethod]
    public void Harness_AtehlstlsekLm684_MatchesReferenceMzMl()
    {
        // 8 spectra with lockmass=684.3469 set on the file. The reference mzML is the
        // *uncorrected* path (no msconvert --lockmass override applied), so we just need
        // the basic flow to work — lockmass *correction* (which would require ApplyLockMass)
        // is exercised in the -ddaProcessing variant only.
        RunHarness("ATEHLSTLSEK_LM_684.3469.raw");
    }

    [TestMethod]
    public void Harness_AtehlstlsekLm785_MatchesReferenceMzMl()
    {
        RunHarness("ATEHLSTLSEK_LM_785.8426.raw");
    }

    [TestMethod]
    public void Harness_DdaIsolationWindow_MatchesReferenceMzMl()
    {
        // Exercises the DDA isolation-window-offset code path (lower/upper offsets recorded
        // in the file are non-zero); the non-ddaProcessing reference is the simpler path
        // where we don't actually invoke the DDA processor.
        RunHarness("DDA_IsolationWindow.raw");
    }

    [TestMethod]
    public void Harness_AtehlstlsekProfileCentroid_MatchesReferenceMzMl()
    {
        // Profile data with PeakPicking enabled — exercises Waters vendor centroid via
        // MassLynx ScanProcessor + calculatePeakMetadata recompute of base peak / TIC /
        // lowest+highest m/z.
        RunHarness("ATEHLSTLSEK_profile.raw", config => config.PeakPicking = true);
    }

    [TestMethod]
    public void Harness_AtehlstlsekLm684DdaProcessing_MatchesReferenceMzMl()
    {
        // Exercises MassLynx's DDA processor (GetDDAScanCount / Info / Scan) — produces 2
        // spectra: one MS1 with raw centroids (~26k peaks) and one MS2 merging scans 1-5
        // with id "merged=1 function=2 process=0 scans=1-5".
        RunHarness("ATEHLSTLSEK_LM_684.3469.raw", config => config.DdaProcessing = true);
    }

    [TestMethod]
    public void Harness_DdaIsolationWindowDdaProcessing_MatchesReferenceMzMl()
    {
        // Exercises the per-file DDA isolation window offsets (LowerOffset, UpperOffset)
        // pushed onto the precursor isolation window when present.
        RunHarness("DDA_IsolationWindow.raw", config => config.DdaProcessing = true);
    }

    private static void RunHarness(string fixtureFolderName, Action<ReaderTestConfig>? configure = null)
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("Waters test data tree not found."); return; }
        if (!Directory.Exists(Path.Combine(root, fixtureFolderName)))
        {
            Assert.Inconclusive($"{fixtureFolderName} not present under test data.");
            return;
        }

        var reader = new Reader_Waters();
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
