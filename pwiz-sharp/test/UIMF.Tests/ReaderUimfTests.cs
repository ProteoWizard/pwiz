using Pwiz.TestHarness;

namespace Pwiz.Vendor.UIMF.Tests;

/// <summary>
/// End-to-end harness modeled on pwiz cpp <c>Reader_UIMF_Test.cpp</c>: reads the
/// <c>BSA_10ugml_CID.UIMF</c> fixture through <see cref="Reader_UIMF"/>, normalizes via
/// <see cref="VendorReaderTestHarness"/>, and diffs against the sibling reference mzML
/// shipped under <c>pwiz/data/vendor_readers/UIMF/Reader_UIMF_Test.data</c>.
/// </summary>
[TestClass]
public class ReaderUimfTests
{
    private static string? FindTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "UIMF",
                "Reader_UIMF_Test.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static FixtureRunContext? SetUp(string fixtureFileName)
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("UIMF test data tree not found."); return null; }
        if (!File.Exists(Path.Combine(root, fixtureFileName)))
        {
            Assert.Inconclusive($"{fixtureFileName} not present under test data.");
            return null;
        }
        return new FixtureRunContext(new Reader_UIMF(), root, new IsNamedRawFile(fixtureFileName), fixtureFileName);
    }

    [TestMethod]
    public void Reader_UIMF_BSA_10ugml_CID()
    {
        // cpp Reader_UIMF_Test.cpp:50 runs default config, no extra tiers.
        var ctx = SetUp("BSA_10ugml_CID.UIMF");
        if (ctx is null) return;

        // The reference mzML ships from a cpp build agent and bakes its build-host path
        // into sourceFile.location ("file:///C:/proteowizard-git/..."). We ignore the
        // checksum + start-timestamp metadata that the harness already normalizes; the
        // location mismatch is expected and benign.
        var baseConfig = new ReaderTestConfig
        {
            IgnoreSourceFileChecksum = true,
            IgnoreStartTimeStamp = true,
        };
        ctx.Run(baseConfig);

        ctx.Check();
    }
}
