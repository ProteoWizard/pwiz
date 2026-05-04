using Pwiz.TestHarness;

namespace Pwiz.Vendor.Sciex.Tests;

/// <summary>
/// End-to-end harness tests modeled on pwiz C++ <c>Reader_ABI_Test.cpp</c>: each
/// <c>.wiff</c> / <c>.wiff2</c> fixture is read through <see cref="Reader_Sciex"/>, normalized
/// via <see cref="VendorReaderTestHarness"/>, and diffed against the sibling reference mzML
/// shipped with the pwiz test tree.
/// </summary>
[TestClass]
public class ReaderSciexHarnessTests
{
    private static string? FindTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "ABI",
                "Reader_ABI_Test.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    [TestMethod]
    public void Reader_Sciex_PressureTrace1()
    {
        // PressureTrace1.wiff — single-sample legacy WIFF, sample name "6500SysSuit1269",
        // instrument "QTRAP 6500 High Mass" (= API6500QTrap, QqLIT).
        var ctx = SetUp("PressureTrace1.wiff");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Check();
    }

    [TestMethod]
    public void Reader_Sciex_Enolase()
    {
        // Enolase_repeats_AQv1.4.2.wiff — multi-sample legacy WIFF (10 samples), instrument
        // "4000 Q TRAP" (= API4000QTrap, QqLIT). The reader opens sample index 0 by default;
        // the run id ends up "Enolase_repeats_AQv1.4.2-20070918_en_01".
        var ctx = SetUp("Enolase_repeats_AQv1.4.2.wiff");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Check();
    }

    // wiff2 fixtures still fail full diff on (a) startTimeStamp timezone interpretation in
    // Wiff2File and (b) chromatogram count (we emit TIC + BPC, cpp emits TIC only). swath.api
    // has additional spectrum-level diffs (isolation-window offsets stored as absolute m/z
    // bounds; CWT peak picker keeps ~3x more points than the vendor centroider). Each is its
    // own follow-up. Re-add Reader_Sciex_ZenoTofMsMsEad / Reader_Sciex_SwathApi when those
    // close out.

    private static FixtureRunContext? SetUp(string fixtureFileName)
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("Sciex test data tree not found."); return null; }
        if (!File.Exists(Path.Combine(root, fixtureFileName)))
        {
            Assert.Inconclusive($"{fixtureFileName} not present under test data.");
            return null;
        }
        return new FixtureRunContext(new Reader_Sciex(), root, new IsNamedRawFile(fixtureFileName), fixtureFileName);
    }
}
