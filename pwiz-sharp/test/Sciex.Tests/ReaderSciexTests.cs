using Pwiz.TestHarness;

namespace Pwiz.Vendor.Sciex.Tests;

/// <summary>
/// End-to-end harness tests modeled on pwiz C++ <c>Reader_ABI_Test.cpp</c>: each
/// <c>.wiff</c> / <c>.wiff2</c> fixture is read through <see cref="Reader_Sciex"/>, normalized
/// via <see cref="VendorReaderTestHarness"/>, and diffed against the sibling reference mzML
/// shipped with the pwiz test tree. Method names preserve the fixture filename's casing
/// (with <c>.</c> / <c>-</c> normalized to <c>_</c>) so grepping a fixture name lands on
/// the test.
/// </summary>
[TestClass]
public class ReaderSciexTests
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
        // single-sample legacy WIFF, sample name "6500SysSuit1269",
        // instrument "QTRAP 6500 High Mass" (= API6500QTrap, QqLIT).
        var ctx = SetUp("PressureTrace1.wiff");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Check();
    }

    [TestMethod]
    public void Reader_Sciex_Enolase_repeats_AQv1_4_2()
    {
        // multi-sample legacy WIFF (10 samples), instrument
        // "4000 Q TRAP" (= API4000QTrap, QqLIT). The reader opens sample index 0 by default;
        // the run id ends up "Enolase_repeats_AQv1.4.2-20070918_en_01".
        var ctx = SetUp("Enolase_repeats_AQv1.4.2.wiff");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Check();
    }

    [TestMethod]
    public void Reader_Sciex_7600ZenoTOFMSMS_EAD_TestData()
    {
        // wiff2 single-sample file, ZenoTOF 7600 (Q-ToF + Zeno trap).
        // Mirrors cpp Reader_ABI_Test.cpp:125 — indexRange = (0, 20) keeps the test
        // fast and matches the 21-spectrum reference mzML.
        var ctx = SetUp("7600ZenoTOFMSMS_EAD_TestData.wiff2");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig { IndexRange = (0, 20) });
        ctx.Check();
    }

    [TestMethod]
    public void Reader_Sciex_swath_api()
    {
        // wiff2 SWATH/DIA fixture — exercises the broad isolation window path.
        // Mirrors cpp Reader_ABI_Test.cpp:118-121 — peakPicking + indexRange = (0, 200).
        var ctx = SetUp("swath.api.wiff2");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig { PeakPicking = true, IndexRange = (0, 200) });
        ctx.Check();
    }

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
