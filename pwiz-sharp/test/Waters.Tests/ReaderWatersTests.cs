using Pwiz.Data.Common.Chemistry;
using Pwiz.TestHarness;

namespace Pwiz.Vendor.Waters.Tests;

/// <summary>
/// End-to-end tests modeled on pwiz C++ <c>Reader_Waters_Test.cpp</c>: each vendor
/// <c>.raw</c> directory is read through <see cref="Reader_Waters"/>, the in-memory
/// <see cref="Pwiz.Data.MsData.MSData"/> is normalized via <see cref="VendorReaderTestHarness"/>,
/// and the result is diffed against the sibling reference mzML shipped with the pwiz test tree.
/// </summary>
/// <remarks>
/// Organized per-fixture (one <c>[TestMethod]</c> per <c>.raw</c> directory) — each method
/// runs every config variant we have a reference mzML for and aggregates per-call results
/// into a single <see cref="FixtureRunContext"/>.
/// </remarks>
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
    public void Reader_Waters_NfdmRaw()
    {
        // Smallest fixture (~ 41 spectra) — basic (function, scan) flow without IMS or DDA.
        var ctx = SetUp("091204_NFDM_008.raw");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Waters_Mix1CalCurve()
    {
        // 24-transition MRM acquisition; SRM chromatogram path (1 TIC + 24 SRM SIC chromatograms).
        var ctx = SetUp("160109_Mix1_calcurve_070.raw");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Waters_AtehlstlsekProfile()
    {
        // 8 spectra of profile-mode TOF MS — IsContinuum=true path. Centroid variant exercises
        // Waters vendor centroid via MassLynx ScanProcessor + base-peak / TIC / m/z metadata recompute.
        var ctx = SetUp("ATEHLSTLSEK_profile.raw");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { PeakPicking = true });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Waters_AtehlstlsekLm684()
    {
        // 8 spectra with lockmass=684.3469. Reference is uncorrected path; ddaProcessing variant
        // exercises MassLynx's DDA processor (GetDDAScanCount / Info / Scan) and produces 2
        // spectra: MS1 with raw centroids + one merged MS2 (id "merged=1 function=2 process=0 scans=1-5").
        var ctx = SetUp("ATEHLSTLSEK_LM_684.3469.raw");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { DdaProcessing = true });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Waters_AtehlstlsekLm785()
    {
        var ctx = SetUp("ATEHLSTLSEK_LM_785.8426.raw");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Waters_DdaIsolationWindow()
    {
        // DDA isolation-window-offset path. Non-ddaProcessing exercises plain reading; the
        // ddaProcessing variant exercises per-file isolation-window offsets pushed onto the
        // precursor element.
        var ctx = SetUp("DDA_IsolationWindow.raw");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { DdaProcessing = true });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Waters_HdddaShort()
    {
        // 400 spectra (2 IMS functions × 200 drift bins). Exercises the most variants of any
        // Waters fixture: base, combineIMS, globalMs1OnlyChromatograms, centroidCwt,
        // combineIMS+centroidCwt, combineIMS+mzMobilityFilter, combineIMS+centroidCwt+ignoreCalibrationScans.
        var ctx = SetUp("HDDDA_Short_noLM.raw");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { CombineIonMobilitySpectra = true });
        ctx.Run(new ReaderTestConfig { GlobalChromatogramsAreMs1Only = true, IndexRange = (0, 0) });
        ctx.Run(new ReaderTestConfig { PeakPickingCWT = true });
        ctx.Run(new ReaderTestConfig { CombineIonMobilitySpectra = true, PeakPickingCWT = true });
        ctx.Run(new ReaderTestConfig
        {
            CombineIonMobilitySpectra = true,
            HasIsolationMzFilter = true,
            IsolationMzAndMobilityFilter = { new MzMobilityWindow(4.0, 0.2) },
        });
        ctx.Run(new ReaderTestConfig
        {
            CombineIonMobilitySpectra = true,
            PeakPickingCWT = true,
            IgnoreCalibrationScans = true,
        });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Waters_HdmseShort()
    {
        // 400 spectra MSe acquisition (function 0 = MS1, function 1 = high-energy MSn).
        var ctx = SetUp("HDMSe_Short_noLM.raw");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { CombineIonMobilitySpectra = true });
        ctx.Run(new ReaderTestConfig
        {
            CombineIonMobilitySpectra = true,
            HasIsolationMzFilter = true,
            IsolationMzAndMobilityFilter = { new MzMobilityWindow(4.0, 0.2) },
        });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Waters_HdmrmShort()
    {
        // 600 spectra HD-MRM acquisition.
        var ctx = SetUp("HDMRM_Short_noLM.raw");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { CombineIonMobilitySpectra = true });
        ctx.Run(new ReaderTestConfig { GlobalChromatogramsAreMs1Only = true, IndexRange = (0, 0) });
        ctx.Run(new ReaderTestConfig
        {
            CombineIonMobilitySpectra = true,
            HasIsolationMzFilter = true,
            IsolationMzAndMobilityFilter = { new MzMobilityWindow(4.0, 0.2) },
        });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Waters_MseShort()
    {
        // Non-IMS MSe acquisition — exercises the same MSe heuristic as HDMSe but without the drift dimension.
        var ctx = SetUp("MSe_Short.raw");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { GlobalChromatogramsAreMs1Only = true, IndexRange = (0, 0) });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Waters_MinimalDda()
    {
        var ctx = SetUp("Minimal_DDA.raw");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { DdaProcessing = true });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Waters_QcLcms2()
    {
        // pwiz C++ tests this one with indexRange = (0, 9): only the first 10 of 2360 spectra.
        // Exercises the analog channel path (TIC + System Pressure + 3 detector channels).
        var ctx = SetUp("QC_LCMS2-2_23_268-1-1.raw");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig { IndexRange = (0, 9) });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Waters_SonarShort()
    {
        // SONAR (scanning quadrupole) — non-combine emits scanning_quadrupole_position
        // lower/upper userParams per scan; combine emits the corresponding bound-array CVs
        // instead of the raw_ion_mobility array. The mzMobilityFilter variant is a no-op on
        // the data (SONAR has no mobility) but the reference filename gets the suffix.
        var ctx = SetUp("SONAR_Short.raw");
        if (ctx is null) return;

        ctx.Run(new ReaderTestConfig());
        ctx.Run(new ReaderTestConfig { CombineIonMobilitySpectra = true });
        ctx.Run(new ReaderTestConfig
        {
            CombineIonMobilitySpectra = true,
            HasIsolationMzFilter = true,
            IsolationMzAndMobilityFilter = { new MzMobilityWindow(4.0, 0.2) },
        });

        ctx.Check();
    }

    private static FixtureRunContext? SetUp(string fixtureFolderName)
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("Waters test data tree not found."); return null; }
        if (!Directory.Exists(Path.Combine(root, fixtureFolderName)))
        {
            Assert.Inconclusive($"{fixtureFolderName} not present under test data.");
            return null;
        }
        return new FixtureRunContext(new Reader_Waters(), root, new IsNamedRawFile(fixtureFolderName), fixtureFolderName);
    }
}
