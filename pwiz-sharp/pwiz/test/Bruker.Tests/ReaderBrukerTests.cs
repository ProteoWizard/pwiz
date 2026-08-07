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
/// one (predicate, config) and many invocations roll up to a single pass/fail. Method names
/// preserve the fixture filename's casing, with <c>.</c> / <c>-</c> normalized to <c>_</c>.
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
    public void Reader_Bruker_20percLaser_100fold_1_0_H6_MS()
    {
        // MALDI TSF fixture. Smallest Bruker .d — designated coverage rep, so the
        // HDF5-backed round-trips run for at least one Bruker fixture under TC dotCover.
        // Coverage: base + ms1-centroid. (cpp also has ms2-centroid; not currently exercised.)
        var ctx = SetUp("20percLaser_100fold_1_0_H6_MS.d");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig { RunRoundTripUnderProfiler = true });
        ctx.Run(new ReaderTestConfig { PeakPicking = true, PreferOnlyMsLevel = 1 });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Bruker_timsTOF_autoMSMS_Urine_50s_neg()
    {
        // auto-MSMS TSF fixture.
        // Coverage: base + ms1-centroid + ms2-centroid.
        var ctx = SetUp("timsTOF_autoMSMS_Urine_50s_neg.d");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { PeakPicking = true, PreferOnlyMsLevel = 1 });
        ctx.Run(new ReaderTestConfig { PeakPicking = true, PreferOnlyMsLevel = 2 });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Bruker_Hela_QC_PASEF_Slot1_first_6_frames()
    {
        // PASEF TDF fixture.
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

    [TestMethod]
    public void Reader_Bruker_ThyroglobMRM000003()
    {
        // PASEF TDF fixture acquired with an MRM method.
        //
        // Runs exactly the configs C++ still runs for a TDF, and no others. Every narrowed config
        // here carries PeakPicking because Reader_Bruker_Test.cpp:131 sets config.peakPicking and
        // never clears it, so the only narrowed references C++ writes today are the -centroid
        // ones. Those six are also precisely the Thyroglob references that were force-added to
        // git; the rest of the data directory is gitignored (.gitignore:409).
        //
        // The archive additionally contains -ms1, -ms2, -combineIMS, -combineIMS-ms1 and
        // -combineIMS-ms2 references. Those are leftovers from an older revision of the C++ test
        // - nothing generates or checks them now, which is why C++ master stays green despite
        // their contents disagreeing with their own tracked siblings. Asserting against them
        // would be asserting our own output back at us, so they are deliberately not run.
        var ctx = SetUp("ThyroglobMRM000003.d");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { PreferOnlyMsLevel = 1, PeakPicking = true });
        ctx.Run(new ReaderTestConfig { PreferOnlyMsLevel = 2, PeakPicking = true });

        var combineIms = new ReaderTestConfig { CombineIonMobilitySpectra = true, PeakPicking = true };
        ctx.Run(combineIms);
        ctx.Run(combineIms with { PreferOnlyMsLevel = 1 });
        ctx.Run(combineIms with { PreferOnlyMsLevel = 2 });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Bruker_CsI_Pos_0_G1_000003()
    {
        // BAF fixture (analysis.baf), read through baf2sql rather than timsdata - the only
        // coverage of the Baf2SqlData path, and the only Bruker fixture that does not need
        // the timsdata native library.
        // Coverage: base. (No centroid reference mzMLs ship for this fixture.)
        var ctx = SetUp("CsI_Pos_0_G1_000003.d");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());

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
