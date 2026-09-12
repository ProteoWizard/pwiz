using Pwiz.TestHarness;

namespace Pwiz.Vendor.Agilent.Tests;

/// <summary>
/// End-to-end harness tests modeled on pwiz cpp <c>Reader_Agilent_Test.cpp</c>: each
/// <c>.d</c> fixture is read through <see cref="Reader_Agilent"/>, normalized via
/// <see cref="VendorReaderTestHarness"/>, and diffed against the sibling reference mzML
/// shipped with the pwiz test tree. Method names preserve the fixture filename's casing
/// (with <c>+</c> / <c>.</c> / <c>-</c> / spaces normalized to <c>_</c>) so grepping a
/// fixture name lands on the test.
///
/// Test config mirrors cpp Reader_Agilent_Test.cpp's main():
///   - default config for every .d (line 61, IsDirectory predicate)
///   - combineIonMobilitySpectra = true on IM-only fixtures (ImsSynth*, GFb_4Scan_TimeSegs)
///   - + globalChromatogramsAreMs1Only + indexRange=(0,0) on a subset (lines 67-71)
///   - + ignoreZeroIntensityPoints on IM (line 73)
/// The last cpp tier (+ isolationMzAndMobilityFilter=(40,1), lines 76-77) is not run here.
/// </summary>
[TestClass]
public class ReaderAgilentTests
{
    private static string? FindTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "Agilent",
                "Reader_Agilent_Test.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static FixtureRunContext? SetUp(string fixtureDirName)
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("Agilent test data tree not found."); return null; }
        if (!Directory.Exists(Path.Combine(root, fixtureDirName)))
        {
            Assert.Inconclusive($"{fixtureDirName} not present under test data.");
            return null;
        }
        return new FixtureRunContext(new Reader_Agilent(), root, new IsNamedRawFile(fixtureDirName), fixtureDirName);
    }

    // -------------------- non-IM fixtures (default config) --------------------

    [TestMethod]
    public void Reader_Agilent_RS080806_APCI_PIscan_CE35()
    {
        var ctx = SetUp("RS080806_APCI_PIscan_CE35.d");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Check();
    }

    [TestMethod]
    public void Reader_Agilent_RS080906_MMI_PIscan_CE35_250ms()
    {
        var ctx = SetUp("RS080906_MMI_PIscan_CE35_250ms.d");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Check();
    }

    [TestMethod]
    public void Reader_Agilent_RS080806_NL_448_2_001()
    {
        // Neutral loss scan — exercises analyzer_scan_offset + sentinel selected ion.
        var ctx = SetUp("RS080806_NL_448.2_001.d");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Check();
    }

    [TestMethod]
    public void Reader_Agilent_TOFsulfas_DADSpectra_UVSignal272_NoProfile()
    {
        // Q-TOF + DAD spectra + UV signal channel, all centroided. Exercises
        // "centroided min/max" userParam + per-spectrum lowest/highest m/z.
        var ctx = SetUp("TOFsulfasMS4GHzDualMode+DADSpectra+UVSignal272-NoProfile.d");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Check();
    }

    [TestMethod]
    public void Reader_Agilent_reserpine_MS2sim_010()
    {
        // SIM (selected-ion-monitoring) acquisition. Spectrum list is empty by
        // default; chromatogram list has TIC + one SIM transition.
        // Designated coverage rep — smallest Agilent .d, exercises the HDF5-backed
        // round-trips for at least one Agilent fixture under TC dotCover.
        var ctx = SetUp("reserpine-MS2sim-010.d");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig { RunRoundTripUnderProfiler = true });
        ctx.Check();
    }

    [TestMethod]
    public void Reader_Agilent_MRM_Neg_C5()
    {
        // MRM acquisition. Spectrum list empty; chromatogram list has TIC + 4
        // SRM transitions, each with Q1/Q3 in the id and collision energy on the
        // precursor activation.
        var ctx = SetUp("MRM Neg C5.d");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Check();
    }

    [TestMethod]
    public void Reader_Agilent_Thyrxox_5_TS_Diff_Scan_B()
    {
        // Q-TOF with differential scan, 233 spectra + TIC + 16 SRM transitions.
        var ctx = SetUp("Thyrxox 5 TS Diff Scan B.d");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Check();
    }

    /// <summary>
    /// A fixture that makes MHDAC centroid ON THE FLY, which is a distinct code path
    /// from every other Agilent fixture here.
    /// </summary>
    /// <remarks>
    /// <para>Two properties have to hold together and nothing else in the corpus has both:
    /// the file stores profile data ONLY (no <c>mspeak.bin</c>), so <c>PeakElseProfile</c> has
    /// nothing stored to return and the SDK must centroid on demand; and the device is a Q-TOF,
    /// which is the family MHDAC will centroid at all.</para>
    /// <para>Subset of the 77.8 MB corpus file <c>Neg_MS_002.d</c>, cut to its first scan
    /// (0.43 MB) so it can be tracked.</para>
    /// </remarks>
    [TestMethod]
    public void Reader_Agilent_Neg_MS_002_1scan_ProfileOnlyVendorCentroid()
    {
        var ctx = SetUp("Neg_MS_002_1scan.d");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { PeakPicking = true });
        ctx.Check();
    }

    // -------------------- IM fixtures --------------------
    //
    // cpp Reader_Agilent_Test.cpp runs the DEFAULT config over every .d (line 62,
    // IsDirectory()) and only then adds the combineIonMobilitySpectra tiers (lines 64-77). For an
    // IM fixture the default tier is not a duplicate of the combine tier: it is the one that
    // exercises the uncombined drift-bin spectrum list — ~1200 spectra (one per non-empty drift
    // bin) against <run>.mzML, versus ~100 combined frames against <run>-combineIMS.mzML.
    // Both tiers run here for the same reason cpp runs both.

    // cpp Reader_Agilent_Test.cpp:73 adds ignoreZeroIntensityPoints on top of the combine tier
    // (msconvert --ignoreMissingZeroSamples). It steers the reference filename to
    // *-ignoreZeros-combineIMS.mzML, which the pwiz test tree ships for all three ImsSynth
    // fixtures. Kept as a separate ReaderTestConfig rather than a field mutation so each tier
    // reads like its cpp counterpart.
    private static ReaderTestConfig CombineIgnoreZeros() => new()
    {
        CombineIonMobilitySpectra = true,
        IgnoreZeroIntensityPoints = true,
    };

    [TestMethod]
    public void Reader_Agilent_ImsSynthAllIons()
    {
        var ctx = SetUp("ImsSynthAllIons.d");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { CombineIonMobilitySpectra = true });
        ctx.Run(CombineIgnoreZeros());
        ctx.Check();
    }

    [TestMethod]
    public void Reader_Agilent_ImsSynthCCS()
    {
        var ctx = SetUp("ImsSynthCCS.d");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { CombineIonMobilitySpectra = true });
        ctx.Run(CombineIgnoreZeros());
        ctx.Check();
    }

    [TestMethod]
    public void Reader_Agilent_ImsSynth_Chrom()
    {
        var ctx = SetUp("ImsSynth_Chrom.d");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { CombineIonMobilitySpectra = true });
        ctx.Run(CombineIgnoreZeros());
        ctx.Check();
    }

    /// <summary>
    /// cpp <c>SpectrumList_Agilent.cpp:678</c>: on the IM, non-combined index path the flag
    /// swaps MIDAC's non-empty-drift-bin list (plus the last-bin readback probe) for a plain
    /// walk of every bin, so the empty bins come through as zero-length spectra. Measured
    /// against cpp msconvert on this fixture: 701 spectra by default, 2832 with the flag, of
    /// which 2131 are zero-length.
    /// </summary>
    [TestMethod]
    public void Reader_Agilent_AcceptZeroLengthSpectra_EmitsEveryDriftBin()
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("Agilent test data tree not found."); return; }
        string d = Path.Combine(root, "ImsSynthCCS.d");
        if (!Directory.Exists(d)) { Assert.Inconclusive("ImsSynthCCS.d not present."); return; }

        static (int Count, int ZeroLength) Read(string path, bool acceptZeroLength)
        {
            var msd = new Pwiz.Data.MsData.MSData();
            new Reader_Agilent().Read(path, msd, new Pwiz.Data.MsData.Readers.ReaderConfig
            {
                AcceptZeroLengthSpectra = acceptZeroLength,
            });
            var list = msd.Run.SpectrumList!;
            int zeroLength = 0;
            for (int i = 0; i < list.Count; i++)
                if (list.GetSpectrum(i).DefaultArrayLength == 0) zeroLength++;
            return (list.Count, zeroLength);
        }

        // Reads directly rather than through VendorReaderTestHarness, so the harness's
        // identify-only fallback does not apply and the SDK-less build would see the
        // exception escape. Counting drift bins needs the real reader either way.
        (int Count, int ZeroLength) off, on;
        try
        {
            off = Read(d, acceptZeroLength: false);
            on = Read(d, acceptZeroLength: true);
        }
        catch (Pwiz.Data.MsData.Readers.VendorSupportNotEnabledException)
        {
            Assert.Inconclusive("Agilent SDK not compiled into this build; direct-read coverage is Windows-only.");
            return;
        }

        Assert.AreEqual(701, off.Count, "default spectrum count changed");
        Assert.AreEqual(0, off.ZeroLength, "the default path must not emit zero-length spectra");
        Assert.AreEqual(2832, on.Count, "--acceptZeroLengthSpectra spectrum count changed");
        Assert.AreEqual(2131, on.ZeroLength, "empty drift bins should come through as zero-length spectra");
    }

    [TestMethod]
    public void Reader_Agilent_GFb_4Scan_TimeSegs_1530_100ng()
    {
        // cpp Reader_Agilent_Test.cpp:67-71 adds the globalChromatogramsAreMs1Only +
        // indexRange=(0,0) tier for GFb on top of the default one — those flags steer the
        // reference filename to *-combineIMS-globalChromatogramsAreMs1Only.mzML.
        // The default (uncombined) tier is deliberately NOT run here, unlike the three
        // ImsSynth* fixtures. GFb is a TandemQuadrupole acquisition whose AcqData has an
        // IMSFrame.bin but no actual IM data (MidacFileAccess.FileHasImsData is false), so it
        // reads through the plain scan-record path and adds nothing to drift-bin coverage. It
        // also fails that tier on a pre-existing float-rendering residual unrelated to this
        // reader: 4 of its 182 spectra have a TIC that is an exact 6-significant-digit tie
        // (e.g. 2851615, confirmed identical from both MHDAC sources), which cpp's karma
        // float5 policy renders "2.85161e06" and PwizFloat renders "2.85162e06". That is the
        // shared-formatter difference tracked as the msconvert-parity "cvParam value differs"
        // class, not something SpectrumList_Agilent controls.
        var ctx = SetUp("GFb_4Scan_TimeSegs_1530_100ng.d");
        if (ctx is null) return;
        ctx.Run(new ReaderTestConfig
        {
            CombineIonMobilitySpectra = true,
            GlobalChromatogramsAreMs1Only = true,
            IndexRange = (0, 0),
        });
        ctx.Check();
    }
}
