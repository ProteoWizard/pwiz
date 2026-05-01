using Pwiz.TestHarness;

namespace Pwiz.Vendor.Bruker.Tests;

/// <summary>
/// End-to-end tests modeled on pwiz C++ <c>Reader_Bruker_Test.cpp</c>: each vendor <c>.d</c>
/// directory is read through <see cref="Reader_Bruker"/>, the in-memory <see cref="Pwiz.Data.MsData.MSData"/>
/// is normalized via <see cref="VendorReaderTestHarness"/>, and the result is diffed against the
/// sibling reference mzML shipped with the pwiz test tree.
/// </summary>
/// <remarks>
/// Organized per-fixture (one <c>[TestMethod]</c> per <c>.d</c> directory) — each method runs
/// every config variant we have a reference mzML for and aggregates per-call results into a
/// single <see cref="TestResult"/>. Mirrors the cpp shape where one harness invocation tests
/// one (predicate, config) and many invocations roll up to a single pass/fail.
/// </remarks>
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
    public void Reader_Bruker_MaldiTsf()
    {
        // 20percLaser_100fold_1_0_H6_MS.d — MALDI TSF fixture.
        // Coverage: base + ms1-centroid. (cpp also has ms2-centroid; not currently exercised.)
        var ctx = SetUp("20percLaser_100fold_1_0_H6_MS.d");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { PeakPicking = true, PreferOnlyMsLevel = 1 });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Bruker_AutoMsMsTsf()
    {
        // timsTOF_autoMSMS_Urine_50s_neg.d — auto-MSMS TSF fixture.
        // Coverage: base + ms1-centroid + ms2-centroid.
        var ctx = SetUp("timsTOF_autoMSMS_Urine_50s_neg.d");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { PeakPicking = true, PreferOnlyMsLevel = 1 });
        ctx.Run(new ReaderTestConfig { PeakPicking = true, PreferOnlyMsLevel = 2 });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Bruker_HelaPasefTdf()
    {
        // Hela_QC_PASEF_Slot1-first-6-frames.d — PASEF TDF fixture.
        // Coverage: base + 6 combineIMS variants (combineIMS, +ms1, +ms2, +centroid,
        // +ms1-centroid, +ms2-centroid). The mobility-array multiset diff lets us run with
        // SortAndJitter=false even though the cpp references were generated with it on.
        // NOTE: Reference mzMLs also exist for non-combineIMS ms1/ms2/centroid variants and
        // globalChromatogramsAreMs1Only/ms2-noMsMsWithoutPrecursor-centroid; not yet covered.
        var ctx = SetUp("Hela_QC_PASEF_Slot1-first-6-frames.d");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());

        var combineIms = new ReaderTestConfig { CombineIonMobilitySpectra = true };
        ctx.Run(combineIms);
        ctx.Run(combineIms with { PreferOnlyMsLevel = 1 });
        ctx.Run(combineIms with { PreferOnlyMsLevel = 2 });
        // CombineIMS + PeakPicking variants: pwiz cpp takes a vendor-centroid path that
        // preserves per-scan mobility arrays + emits CCS / collision_energy userParams; our
        // SpectrumList_PeakPicker reduces the merged profile to CWT centroids and drops
        // mobility. Tracked separately; harness scaffolding kept so the variants stay visible.
        ctx.Run(combineIms with { PeakPicking = true });
        ctx.Run(combineIms with { PreferOnlyMsLevel = 1, PeakPicking = true });
        ctx.Run(combineIms with { PreferOnlyMsLevel = 2, PeakPicking = true });

        ctx.Check();
    }

    /// <summary>
    /// Locates the fixture and returns a per-test <see cref="FixtureRunContext"/>; records an
    /// Inconclusive on the test (and returns null) when the fixture isn't on disk.
    /// </summary>
    private static FixtureRunContext? SetUp(string fixtureFolderName)
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("Bruker test data tree not found."); return null; }
        if (!Directory.Exists(Path.Combine(root, fixtureFolderName)))
        {
            Assert.Inconclusive($"{fixtureFolderName} not present under test data.");
            return null;
        }
        return new FixtureRunContext(new Reader_Bruker(), root, new IsNamedRawFile(fixtureFolderName), fixtureFolderName);
    }
}
