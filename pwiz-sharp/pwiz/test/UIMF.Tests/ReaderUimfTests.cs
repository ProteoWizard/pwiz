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
            // Sole UIMF fixture, so also the coverage rep: round-trips run under TC dotCover.
            RunRoundTripUnderProfiler = true,
        };
        ctx.Run(baseConfig);

        ctx.Check();
    }

    /// <summary>
    /// Rewrites every UIMF reference mzML in this class from the current reader output.
    ///
    /// <para>The <c>[TestMethod]</c> attribute below is COMMENTED OUT deliberately. An
    /// undiscovered test cannot run in "Run All Tests", and MSTest has no attribute that
    /// excludes a test from a run-all while leaving it runnable on demand: <c>[Ignore]</c> is
    /// skipped even when selected explicitly, and a runsettings <c>TestCaseFilter</c> removes it
    /// from discovery altogether, so it never appears to click on. Commenting the attribute is
    /// the only mechanism that gives both.</para>
    ///
    /// <para>To regenerate: uncomment the attribute, run this one test, then comment it back.
    /// It ends in <c>Assert.Fail</c>, so a run that completes is never green and the message is
    /// the reminder. That is NOT a CI guard: if this vendor's data is absent, <c>SetUp</c> throws
    /// <c>Assert.Inconclusive</c>, which aborts before the <c>Assert.Fail</c> and reports Skipped -
    /// and <c>dotnet test</c> exits 0 on skips. Afterwards run the suite again WITHOUT it - a
    /// regenerated reference that
    /// does not then compare equal means the generate and compare paths disagree, which is the
    /// one failure this mode can hide.</para>
    /// </summary>
    //[TestMethod]
    public void Regenerate_UIMF_References()
    {
        VendorReaderTestHarness.GenerateReferences = true;
        try
        {
            Reader_UIMF_BSA_10ugml_CID();
        }
        finally
        {
            VendorReaderTestHarness.GenerateReferences = false;
        }

        Assert.Fail("Reference mzMLs regenerated. Comment out this [TestMethod] and run the normal tests.");
    }
}
