using Pwiz.TestHarness;

namespace Pwiz.Vendor.Thermo.Tests;

/// <summary>
/// End-to-end harness tests modeled on pwiz C++ <c>Reader_Thermo_Test.cpp</c>: each
/// <c>.raw</c> fixture is read through <see cref="Reader_Thermo"/>, normalized via
/// <see cref="VendorReaderTestHarness"/>, and diffed against the sibling reference mzML
/// shipped with the pwiz test tree.
/// </summary>
[TestClass]
public class ReaderThermoHarnessTests
{
    private static string? FindTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "Thermo",
                "Reader_Thermo_Test.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    /// <summary>Matches any <c>.raw</c> file in the test data directory.</summary>
    private sealed class IsRawFile : TestPathPredicate
    {
        public override bool Matches(string rawPath) =>
            Path.GetExtension(rawPath.TrimEnd('/', '\\')).Equals(".raw", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Harness_ThermoRawFiles_MatchReferenceMzMl()
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("Thermo test data tree not found."); return; }

        var reader = new Reader_Thermo();
        var result = VendorReaderTestHarness.TestReader(
            reader,
            rootPath: root,
            predicate: new IsRawFile());

        if (result.FailedTests > 0)
            Assert.Fail(string.Join('\n', result.FailureMessages));
        Assert.IsTrue(result.TotalTests > 0, "harness did not find any .raw fixtures");
    }

    [TestMethod]
    public void Harness_ThermoRawFiles_Centroid_MatchReferenceMzMl()
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("Thermo test data tree not found."); return; }

        var reader = new Reader_Thermo();
        var config = new ReaderTestConfig { PeakPicking = true };
        var result = VendorReaderTestHarness.TestReader(
            reader,
            rootPath: root,
            predicate: new IsRawFile(),
            config: config);

        if (result.FailedTests > 0)
            Assert.Fail(string.Join('\n', result.FailureMessages));
        Assert.IsTrue(result.TotalTests > 0, "harness did not find any .raw fixtures");
    }
}
