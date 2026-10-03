using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

[TestClass]
public class SpectrumListMzRefinerTests
{
    /// <summary>Direct port of cpp's testShift — loads the MZRefiner test fixture
    /// (JD_06232014_sample4_C.mzML + matching MS-GF+ .mzid), runs the refiner with
    /// specEValue ≤ 1e-10 over MS levels 1-2, and asserts the same metadata + sampled
    /// m/z values cpp asserts on for 7 spectra (3 MS1, 4 MS2). Tolerances mirror cpp:
    /// 1e-4 pre-refine, 1e-2 post-refine.</summary>
    [TestMethod]
    public void RefinesMzAgainstMzGfPlusIdentifications()
    {
        string mzmlPath = FindFixture("JD_06232014_sample4_C.mzML");
        string mzidPath = FindFixture("JD_06232014_sample4_C.mzid");

        MSData msd;
        using (var fs = File.OpenRead(mzmlPath))
            msd = new MzmlReader().Read(fs);
        Assert.AreEqual(610, msd.Run.SpectrumList!.Count);

        // ---- pre-refine baseline (sanity-check the mzML reader). ----
        const double epsOrig = 1e-4;
        VerifyScanInfo(msd.Run.SpectrumList.GetSpectrum(  0, true), epsOrig, 371.09958, 300.14306, 1568.55126,   30, 303.64633, 1200, 416.24838);
        VerifyScanInfo(msd.Run.SpectrumList.GetSpectrum(224, true), epsOrig, 558.30688, 301.05908, 1522.72473,  200, 407.26425, 1500, 724.32824);
        VerifyScanInfo(msd.Run.SpectrumList.GetSpectrum( 10, true), epsOrig, 530.32782,  74.06039,  887.42852,   41, 188.11117,   93, 442.22839);
        VerifyPrecursorInfo(msd.Run.SpectrumList.GetSpectrum( 10, true), epsOrig, 530.26684, 530.27);
        VerifyScanInfo(msd.Run.SpectrumList.GetSpectrum(173, true), epsOrig, 141.10162,  87.05542, 1187.53137,   63, 248.15817,  116, 887.44793);
        VerifyPrecursorInfo(msd.Run.SpectrumList.GetSpectrum(173, true), epsOrig, 629.30160, 629.3);
        VerifyScanInfo(msd.Run.SpectrumList.GetSpectrum(346, true), epsOrig, 848.45895, 116.00368, 1454.73327,   16, 185.16418,   95, 862.43109);
        VerifyPrecursorInfo(msd.Run.SpectrumList.GetSpectrum(346, true), epsOrig, 840.45480, 840.45);
        VerifyScanInfo(msd.Run.SpectrumList.GetSpectrum(470, true), epsOrig, 249.15857, 119.04895, 1402.77331,   23, 217.08113,  102, 1154.59863);
        VerifyPrecursorInfo(msd.Run.SpectrumList.GetSpectrum(470, true), epsOrig, 838.96706, 838.97);
        VerifyScanInfo(msd.Run.SpectrumList.GetSpectrum(551, true), epsOrig, 1048.55047, 155.08105, 1321.67761,   50, 368.19134,  104, 941.96954);
        VerifyPrecursorInfo(msd.Run.SpectrumList.GetSpectrum(551, true), epsOrig, 739.69935, 740.03);

        // ---- run the refinement ----
        // Cpp's test deletes the stats TSV the refiner writes alongside the .mzid. Do the
        // same so repeated runs don't accumulate stale rows in the fixture directory.
        string statsPath = Path.Combine(Path.GetDirectoryName(mzidPath)!,
            Path.GetFileNameWithoutExtension(mzidPath) + ".mzRefinement.tsv");
        if (File.Exists(statsPath)) File.Delete(statsPath);
        var refiner = new SpectrumList_MZRefiner(msd, mzidPath, cvTerm: "specEValue",
            rangeSet: "-1e-10", msLevelsToRefine: new IntegerSet(1, 2));
        Assert.AreEqual(610, refiner.Count);
        Assert.IsTrue(File.Exists(statsPath), $"expected stats TSV at {statsPath}");
        File.Delete(statsPath);
        Assert.AreNotEqual(0, refiner.GlobalShiftPpm,
            "expected a non-zero ppm shift from MS-GF+ identifications");
        // Cpp picks the m/z-binned shift for this fixture (pct improvement > 3%, beats scan-
        // time-binned). The C# port — once it applies the same PSM filters (isotope window,
        // ppm-error limit, best-by-score) — should reach the same selection.
        var dp = refiner.DataProcessing!;
        var calibMethod = dp.ProcessingMethods.Single(m =>
            m.CVParams.Any(p => p.Cvid == CVID.MS_m_z_calibration));
        Assert.AreEqual("Using mass to charge dependency",
            calibMethod.UserParams.Single(u => u.Name == "Shift dependency").Value);
        Assert.AreEqual("Using mass to charge dependency", refiner.ChosenShiftDescription);

        // ---- post-refine assertions ----
        const double epsRefined = 1e-2; // cpp's tolerance for refined results
        VerifyScanInfo(refiner.GetSpectrum(  0, true), epsRefined, 371.10060, 300.14388, 1568.55631,   30, 303.64715, 1200, 416.24951);
        VerifyScanInfo(refiner.GetSpectrum(224, true), epsRefined, 558.30841, 301.05990, 1522.72962,  200, 407.26538, 1500, 724.33007);
        VerifyScanInfo(refiner.GetSpectrum( 10, true), epsRefined, 530.32928,  74.06059,  887.43126,   41, 188.11169,   93, 442.22961);
        VerifyPrecursorInfo(refiner.GetSpectrum( 10, true), epsRefined, 530.26830, 530.27145);
        VerifyScanInfo(refiner.GetSpectrum(173, true), epsRefined, 141.10200,  87.05566, 1187.53519,   63, 248.15885,  116, 887.45068);
        VerifyPrecursorInfo(refiner.GetSpectrum(173, true), epsRefined, 629.30333, 629.30172);
        VerifyScanInfo(refiner.GetSpectrum(346, true), epsRefined, 848.46155, 116.00400, 1454.73795,   16, 185.16468,   95, 862.43371);
        VerifyPrecursorInfo(refiner.GetSpectrum(346, true), epsRefined, 840.45738, 840.45257);
        VerifyScanInfo(refiner.GetSpectrum(470, true), epsRefined, 249.15926, 119.04927, 1402.77782,   23, 217.08172,  102, 1154.60229);
        VerifyPrecursorInfo(refiner.GetSpectrum(470, true), epsRefined, 838.96963, 838.97257);
        VerifyScanInfo(refiner.GetSpectrum(551, true), epsRefined, 1048.55384, 155.08147, 1321.68186,   50, 368.19235,  104, 941.97253);
        VerifyPrecursorInfo(refiner.GetSpectrum(551, true), epsRefined, 739.70141, 740.03206);
    }

    [TestMethod]
    public void ParseDoubleRange_HandlesAllSyntaxes()
    {
        var (min, max) = CallParse("-1e-10");
        Assert.AreEqual(double.MinValue, min);
        Assert.AreEqual(1e-10, max, 1e-15);

        (min, max) = CallParse("5-");
        Assert.AreEqual(5.0, min);
        Assert.AreEqual(double.MaxValue, max);

        (min, max) = CallParse("1-5");
        Assert.AreEqual(1.0, min);
        Assert.AreEqual(5.0, max);

        (min, max) = CallParse("[1,5]");
        Assert.AreEqual(1.0, min);
        Assert.AreEqual(5.0, max);

        (min, max) = CallParse("[,5]");
        Assert.AreEqual(double.MinValue, min);
        Assert.AreEqual(5.0, max);

        (min, max) = CallParse("[1,]");
        Assert.AreEqual(1.0, min);
        Assert.AreEqual(double.MaxValue, max);

        // Invoke via reflection — internal API.
        static (double, double) CallParse(string s)
        {
            var type = typeof(SpectrumList_MZRefiner).Assembly
                .GetType("Pwiz.Analysis.CVConditionalFilter")!;
            var method = type.GetMethod("ParseDoubleRange",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            var result = method.Invoke(null, new object[] { s })!;
            var minProp = result.GetType().GetField("Item1")!;
            var maxProp = result.GetType().GetField("Item2")!;
            return ((double)minProp.GetValue(result)!, (double)maxProp.GetValue(result)!);
        }
    }

    /// <summary>Mirrors cpp's verifyScanInfo: checks base peak, lowest / highest observed
    /// m/z, plus two sampled m/z array values at specific indices.</summary>
    private static void VerifyScanInfo(Spectrum spec, double epsilon,
        double basePeakMz, double lowestObservedMz, double highestObservedMz,
        int mzArrayIndex1, double mzArrayValue1,
        int mzArrayIndex2, double mzArrayValue2)
    {
        Assert.AreEqual(basePeakMz, spec.Params.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>(), epsilon, $"basePeakMz @ {spec.Index}");
        Assert.AreEqual(lowestObservedMz, spec.Params.CvParam(CVID.MS_lowest_observed_m_z).ValueAs<double>(), epsilon, $"lowestObservedMz @ {spec.Index}");
        Assert.AreEqual(highestObservedMz, spec.Params.CvParam(CVID.MS_highest_observed_m_z).ValueAs<double>(), epsilon, $"highestObservedMz @ {spec.Index}");

        var mz = spec.GetMZArray();
        Assert.IsNotNull(mz, $"no m/z array on spectrum {spec.Index}");
        Assert.AreEqual(mzArrayValue1, mz!.Data[mzArrayIndex1], epsilon, $"mzArray[{mzArrayIndex1}] @ {spec.Index}");
        Assert.AreEqual(mzArrayValue2, mz.Data[mzArrayIndex2], epsilon, $"mzArray[{mzArrayIndex2}] @ {spec.Index}");
    }

    /// <summary>Mirrors cpp's verifyPrecursorInfo: checks selected-ion m/z and isolation
    /// window target m/z on the first precursor.</summary>
    private static void VerifyPrecursorInfo(Spectrum spec, double epsilon,
        double precursorMz, double isolationWindowTarget)
    {
        Assert.IsTrue(spec.Precursors.Count > 0, $"no precursors on spectrum {spec.Index}");
        var p = spec.Precursors[0];
        Assert.IsTrue(p.SelectedIons.Count > 0, $"no selected ions on spectrum {spec.Index}");
        Assert.AreEqual(precursorMz, p.SelectedIons[0].CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>(), epsilon, $"selectedIonMz @ {spec.Index}");
        Assert.AreEqual(isolationWindowTarget, p.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>(), epsilon, $"isolationWindowTarget @ {spec.Index}");
    }

    private static string FindFixture(string name)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string c = Path.Combine(dir, "pwiz", "analysis", "spectrum_processing",
                "SpectrumList_MZRefinerTest.data", name);
            if (File.Exists(c)) return c;
            dir = Path.GetDirectoryName(dir);
        }
        Assert.Inconclusive($"test fixture not found: {name}");
        throw new InvalidOperationException("unreachable");
    }
}
