using Pwiz.TestHarness;

namespace Pwiz.Vendor.Shimadzu.Tests;

/// <summary>
/// End-to-end harness tests modeled on pwiz cpp <c>Reader_Shimadzu_Test.cpp</c>: each
/// <c>.lcd</c> fixture is read through <see cref="Reader_Shimadzu"/>, normalized via
/// <see cref="VendorReaderTestHarness"/>, and diffed against the sibling reference mzML
/// shipped under <c>pwiz/data/vendor_readers/Shimadzu/Reader_Shimadzu_Test.data</c>.
/// </summary>
/// <remarks>
/// Test config mirrors cpp Reader_Shimadzu_Test.cpp's main():
///   - default config for every .lcd (line 97)
///   - peakPicking=true for every .lcd (line 100)
///   - srmAsSpectra=true + indexRange=(1240,1260) on the unicode-named file (lines 102-104)
/// The globalChromatogramsAreMs1Only / first-spectrum-only tier is referenced in cpp
/// (lines 107-110) but commented out — we mirror that by leaving it out here too.
///
/// Initial Shimadzu port: until SDK plumbing is verified end-to-end against the SDK's
/// COM/CLI surface under .NET 8, every test will likely fall through the harness's
/// VendorSupportNotEnabled identify-only branch (when built without the license flag) or
/// surface a parity diff (when built with it). Keeping the methods declared so a future
/// run-against-SDK pass produces meaningful pass/fail reports.
/// </remarks>
[TestClass]
public class ReaderShimadzuTests
{
    private static string? FindTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "Shimadzu",
                "Reader_Shimadzu_Test.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static FixtureRunContext? SetUp(string fixtureFileName)
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("Shimadzu test data tree not found."); return null; }
        if (!File.Exists(Path.Combine(root, fixtureFileName)))
        {
            Assert.Inconclusive($"{fixtureFileName} not present under test data.");
            return null;
        }
        return new FixtureRunContext(new Reader_Shimadzu(), root, new IsNamedRawFile(fixtureFileName), fixtureFileName);
    }

    [TestMethod]
    public void Reader_Shimadzu_10nmol_Negative_MS_ID_ON_055()
    {
        // Negative-mode LCMS QqTOF. cpp Reader_Shimadzu_Test.cpp:97-100 runs default + peakPicking.
        // References: 10nmol_Negative_MS_ID_ON_055.mzML, ...-centroid.mzML.
        // Flaky on TC at ~65% pass rate: the SDK's QtflRawDataMain backend init is racy under
        // .NET 8; ShimadzuRawData retries on the documented signature. The test relies on that
        // retry; if it surfaces this fixture's failure mode here, regression in retry handling.
        var ctx = SetUp("10nmol_Negative_MS_ID_ON_055.lcd");
        if (ctx is null) return;

        // Shimadzu cpp reference mzMLs encode the cpp build's SampleInfo.AnalysisDate value;
        // the C# SDK leaves that property at DateTime.MinValue under .NET 8, so the diff
        // ignores startTimeStamp. ShimadzuRawData still falls back to FilePropTag.GeneratedDateTime
        // for production runs. IgnoreSourceFileChecksum tolerates the .lcd having been
        // regenerated since the reference was written (different SHA-1 from cpp's baseline).
        var baseConfig = new ReaderTestConfig
        {
            IgnoreStartTimeStamp = true,
            IgnoreSourceFileChecksum = true,
        };
        ctx.Run(baseConfig);
        ctx.Run(baseConfig with { PeakPicking = true });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Shimadzu_20140312_unicode_mix_column_scheduled()
    {
        // Triple-quad SRM acquisition with a Unicode (kanji + Chinese) filename — exercises
        // the path-encoding round-trip plus srmAsSpectra promotion. cpp test runs:
        //   1. default config (SRM as chromatograms)
        //   2. peakPicking=true (still chromatograms; centroid path)
        //   3. srmAsSpectra=true + indexRange=(1240,1260) + peakPicking=true
        // Reference mzMLs ship under the same Unicode filename: <name>.mzML, <name>-centroid.mzML,
        // <name>-srmSpectra-centroid.mzML.
        var ctx = SetUp("20140312_六mix_column_1 (scheduled) 一个试.lcd");
        if (ctx is null) return;

        var baseConfig = new ReaderTestConfig { IgnoreStartTimeStamp = true };
        ctx.Run(baseConfig);
        ctx.Run(baseConfig with { PeakPicking = true });
        ctx.Run(baseConfig with
        {
            SrmAsSpectra = true,
            PeakPicking = true,
            IndexRange = (1240, 1260),
        });

        ctx.Check();
    }
}
