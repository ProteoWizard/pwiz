using Pwiz.Data.Common.Chemistry;
using Pwiz.TestHarness;

namespace Pwiz.Vendor.Waters.Tests;

/// <summary>
/// End-to-end tests modeled on pwiz C++ <c>Reader_Waters_Test.cpp</c>: each vendor
/// <c>.raw</c> directory is read through <see cref="Reader_Waters"/>, the in-memory
/// <see cref="Pwiz.Data.MsData.MSData"/> is normalized via <see cref="VendorReaderTestHarness"/>,
/// and the result is diffed against the sibling reference mzML shipped with the pwiz test tree.
/// </summary>
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
    public void Harness_NfdmRaw_MatchesReferenceMzMl()
    {
        // Smallest fixture (~ 41 spectra in the reference mzML) — exercises the basic
        // (function, scan) flow without ion mobility or DDA. Good first parity target.
        RunHarness("091204_NFDM_008.raw");
    }

    [TestMethod]
    public void Harness_Mix1CalCurveRaw_MatchesReferenceMzMl()
    {
        // 24-transition MRM acquisition; exercises the SRM chromatogram path
        // (1 TIC + 24 SRM SIC chromatograms).
        RunHarness("160109_Mix1_calcurve_070.raw");
    }

    [TestMethod]
    public void Harness_AtehlstlsekProfile_MatchesReferenceMzMl()
    {
        // 8 spectra of profile-mode TOF MS — exercises the IsContinuum=true path.
        RunHarness("ATEHLSTLSEK_profile.raw");
    }

    [TestMethod]
    public void Harness_AtehlstlsekLm684_MatchesReferenceMzMl()
    {
        // 8 spectra with lockmass=684.3469 set on the file. The reference mzML is the
        // *uncorrected* path (no msconvert --lockmass override applied), so we just need
        // the basic flow to work — lockmass *correction* (which would require ApplyLockMass)
        // is exercised in the -ddaProcessing variant only.
        RunHarness("ATEHLSTLSEK_LM_684.3469.raw");
    }

    [TestMethod]
    public void Harness_AtehlstlsekLm785_MatchesReferenceMzMl()
    {
        RunHarness("ATEHLSTLSEK_LM_785.8426.raw");
    }

    [TestMethod]
    public void Harness_DdaIsolationWindow_MatchesReferenceMzMl()
    {
        // Exercises the DDA isolation-window-offset code path (lower/upper offsets recorded
        // in the file are non-zero); the non-ddaProcessing reference is the simpler path
        // where we don't actually invoke the DDA processor.
        RunHarness("DDA_IsolationWindow.raw");
    }

    [TestMethod]
    public void Harness_AtehlstlsekProfileCentroid_MatchesReferenceMzMl()
    {
        // Profile data with PeakPicking enabled — exercises Waters vendor centroid via
        // MassLynx ScanProcessor + calculatePeakMetadata recompute of base peak / TIC /
        // lowest+highest m/z.
        RunHarness("ATEHLSTLSEK_profile.raw", config => config.PeakPicking = true);
    }

    [TestMethod]
    public void Harness_AtehlstlsekLm684DdaProcessing_MatchesReferenceMzMl()
    {
        // Exercises MassLynx's DDA processor (GetDDAScanCount / Info / Scan) — produces 2
        // spectra: one MS1 with raw centroids (~26k peaks) and one MS2 merging scans 1-5
        // with id "merged=1 function=2 process=0 scans=1-5".
        RunHarness("ATEHLSTLSEK_LM_684.3469.raw", config => config.DdaProcessing = true);
    }

    [TestMethod]
    public void Harness_DdaIsolationWindowDdaProcessing_MatchesReferenceMzMl()
    {
        // Exercises the per-file DDA isolation window offsets (LowerOffset, UpperOffset)
        // pushed onto the precursor isolation window when present.
        RunHarness("DDA_IsolationWindow.raw", config => config.DdaProcessing = true);
    }

    [TestMethod]
    public void Harness_HdddaShortRaw_MatchesReferenceMzMl()
    {
        // 400 spectra (2 IMS functions × 200 drift bins). Non-combine path.
        RunHarness("HDDDA_Short_noLM.raw");
    }

    [TestMethod]
    public void Harness_HdddaShortCombineIMS_MatchesReferenceMzMl()
    {
        // 2 spectra (one combined per IMS block).
        RunHarness("HDDDA_Short_noLM.raw", config => config.CombineIonMobilitySpectra = true);
    }

    [TestMethod]
    public void Harness_HdmseShortRaw_MatchesReferenceMzMl()
    {
        // 400 spectra MSe acquisition (function 0 = MS1, function 1 = high-energy MSn).
        RunHarness("HDMSe_Short_noLM.raw");
    }

    [TestMethod]
    public void Harness_HdmrmShortRaw_MatchesReferenceMzMl()
    {
        // 600 spectra HD-MRM acquisition.
        RunHarness("HDMRM_Short_noLM.raw");
    }

    [TestMethod]
    public void Harness_HdmseShortCombineIMS_MatchesReferenceMzMl()
    {
        RunHarness("HDMSe_Short_noLM.raw", config => config.CombineIonMobilitySpectra = true);
    }

    [TestMethod]
    public void Harness_HdmrmShortCombineIMS_MatchesReferenceMzMl()
    {
        RunHarness("HDMRM_Short_noLM.raw", config => config.CombineIonMobilitySpectra = true);
    }

    [TestMethod]
    public void Harness_HdddaShortGlobalMs1OnlyChromatograms_MatchesReferenceMzMl()
    {
        // pwiz C++ writes this reference with indexRange = (0, 0): only the first spectrum
        // is in the reference, and the global TIC excludes function 1 (MSe high-energy).
        RunHarness("HDDDA_Short_noLM.raw", config =>
        {
            config.GlobalChromatogramsAreMs1Only = true;
            config.IndexRange = (0, 0);
        });
    }

    [TestMethod]
    public void Harness_HdmrmShortGlobalMs1OnlyChromatograms_MatchesReferenceMzMl()
    {
        RunHarness("HDMRM_Short_noLM.raw", config =>
        {
            config.GlobalChromatogramsAreMs1Only = true;
            config.IndexRange = (0, 0);
        });
    }

    [TestMethod]
    public void Harness_HdddaShortCentroidCwt_MatchesReferenceMzMl()
    {
        RunHarness("HDDDA_Short_noLM.raw", config => config.PeakPickingCWT = true);
    }

    [TestMethod]
    public void Harness_HdddaShortCombineIMSCentroidCwt_MatchesReferenceMzMl()
    {
        RunHarness("HDDDA_Short_noLM.raw", config =>
        {
            config.CombineIonMobilitySpectra = true;
            config.PeakPickingCWT = true;
        });
    }

    [TestMethod]
    public void Harness_MseShortRaw_MatchesReferenceMzMl()
    {
        // Non-IMS MSe acquisition — exercises the same MSe heuristic as HDMSe but without
        // the drift dimension.
        RunHarness("MSe_Short.raw");
    }

    [TestMethod]
    public void Harness_MseShortGlobalMs1OnlyChromatograms_MatchesReferenceMzMl()
    {
        RunHarness("MSe_Short.raw", config =>
        {
            config.GlobalChromatogramsAreMs1Only = true;
            config.IndexRange = (0, 0);
        });
    }

    [TestMethod]
    public void Harness_MinimalDdaRaw_MatchesReferenceMzMl()
    {
        RunHarness("Minimal_DDA.raw");
    }

    [TestMethod]
    public void Harness_MinimalDdaDdaProcessing_MatchesReferenceMzMl()
    {
        RunHarness("Minimal_DDA.raw", config => config.DdaProcessing = true);
    }

    [TestMethod]
    public void Harness_QcLcms2Raw_MatchesReferenceMzMl()
    {
        // pwiz C++ tests this one with indexRange = (0, 9): only the first 10 spectra are
        // diffed (the file is too large to include all 2360 in the reference). Also
        // exercises the analog channel path (TIC + System Pressure + 3 detector channels).
        RunHarness("QC_LCMS2-2_23_268-1-1.raw", config => config.IndexRange = (0, 9));
    }

    [TestMethod]
    public void Harness_HdddaShortCombineIMSMzMobilityFilter_MatchesReferenceMzMl()
    {
        // mzMobilityFilter test config: single window centered at mobility=4 with tolerance
        // 0.2 → bounds (3.8, 4.2). pwiz C++ writes the reference with this exact filter.
        RunHarness("HDDDA_Short_noLM.raw", config =>
        {
            config.CombineIonMobilitySpectra = true;
            config.HasIsolationMzFilter = true;
            config.IsolationMzAndMobilityFilter.Add(new MzMobilityWindow(4.0, 0.2));
        });
    }

    [TestMethod]
    public void Harness_HdmrmShortCombineIMSMzMobilityFilter_MatchesReferenceMzMl()
    {
        RunHarness("HDMRM_Short_noLM.raw", config =>
        {
            config.CombineIonMobilitySpectra = true;
            config.HasIsolationMzFilter = true;
            config.IsolationMzAndMobilityFilter.Add(new MzMobilityWindow(4.0, 0.2));
        });
    }

    [TestMethod]
    public void Harness_HdmseShortCombineIMSMzMobilityFilter_MatchesReferenceMzMl()
    {
        RunHarness("HDMSe_Short_noLM.raw", config =>
        {
            config.CombineIonMobilitySpectra = true;
            config.HasIsolationMzFilter = true;
            config.IsolationMzAndMobilityFilter.Add(new MzMobilityWindow(4.0, 0.2));
        });
    }

    [TestMethod]
    public void Harness_SonarShortRaw_MatchesReferenceMzMl()
    {
        // SONAR (scanning quadrupole) acquisition — non-combine: 600 spectra, each gets
        // scanning_quadrupole_position lower/upper bound userParams on its scan element.
        RunHarness("SONAR_Short.raw");
    }

    [TestMethod]
    public void Harness_SonarShortCombineIMS_MatchesReferenceMzMl()
    {
        // Combine path emits MS_scanning_quadrupole_position_lower/upper_bound_m_z_array
        // (instead of MS_raw_ion_mobility_array) since the drift dimension is repurposed.
        RunHarness("SONAR_Short.raw", config => config.CombineIonMobilitySpectra = true);
    }

    [TestMethod]
    public void Harness_SonarShortCombineIMSMzMobilityFilter_MatchesReferenceMzMl()
    {
        // SONAR doesn't have mobility, so the filter is a no-op on the data itself but the
        // reference filename includes the suffix.
        RunHarness("SONAR_Short.raw", config =>
        {
            config.CombineIonMobilitySpectra = true;
            config.HasIsolationMzFilter = true;
            config.IsolationMzAndMobilityFilter.Add(new MzMobilityWindow(4.0, 0.2));
        });
    }

    [TestMethod]
    public void Harness_HdddaShortCombineIMSCentroidCwtIgnoreCalibrationScans_MatchesReferenceMzMl()
    {
        // ignoreCalibrationScans skips the lockmass function from the spectrum list.
        // Combined with combineIMS + centroid-cwt to exercise three filters at once.
        RunHarness("HDDDA_Short_noLM.raw", config =>
        {
            config.CombineIonMobilitySpectra = true;
            config.PeakPickingCWT = true;
            config.IgnoreCalibrationScans = true;
        });
    }

    private static void RunHarness(string fixtureFolderName, Action<ReaderTestConfig>? configure = null)
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("Waters test data tree not found."); return; }
        if (!Directory.Exists(Path.Combine(root, fixtureFolderName)))
        {
            Assert.Inconclusive($"{fixtureFolderName} not present under test data.");
            return;
        }

        var reader = new Reader_Waters();
        var config = new ReaderTestConfig();
        configure?.Invoke(config);
        var result = VendorReaderTestHarness.TestReader(
            reader,
            rootPath: root,
            predicate: new IsNamedRawFile(fixtureFolderName),
            config: config);

        if (result.FailedTests > 0)
            Assert.Fail(string.Join('\n', result.FailureMessages));
        Assert.AreEqual(1, result.TotalTests, "harness did not find the fixture");
    }
}
