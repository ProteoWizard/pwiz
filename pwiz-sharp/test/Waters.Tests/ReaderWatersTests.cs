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
