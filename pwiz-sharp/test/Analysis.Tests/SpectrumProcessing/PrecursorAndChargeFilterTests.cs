using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

[TestClass]
public class PrecursorAndChargeFilterTests
{
    // ============================================================================
    //   SpectrumList_PrecursorRefine
    // ============================================================================

    [TestMethod]
    public void PrecursorRefine_PassesThroughOnUnsupportedAnalyzer()
    {
        // Quadrupole isn't FT-ICR / orbitrap / TOF — so the filter pass-throughs unchanged.
        var msd = BuildMsdWithAnalyzer(CVID.MS_quadrupole);
        msd.Run.SpectrumList = BuildList_Ms1Ms2Ms1(precursorMz: 500.500);

        var refiner = new SpectrumList_PrecursorRefine(msd);
        var ms2 = refiner.GetSpectrum(1, getBinaryData: true);
        Assert.AreEqual(500.500,
            ms2.Precursors[0].SelectedIons[0].CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>(),
            1e-9);
    }

    [TestMethod]
    public void PrecursorRefine_OrbitrapMs2_RefinesPrecursorMz()
    {
        string fixture = FindFixture("PrecursorRefineOrbi.mzML");
        MSData msd;
        using (var fs = File.OpenRead(fixture))
            msd = new Pwiz.Data.MsData.Mzml.MzmlReader().Read(fs);

        Assert.AreEqual(51, msd.Run.SpectrumList!.Count);

        // Original (un-refined) precursor m/z values from the fixture.
        AssertPrecursorMz(msd.Run.SpectrumList.GetSpectrum(21, true), 747.37225);
        AssertPrecursorMz(msd.Run.SpectrumList.GetSpectrum(22, true), 614.867065);
        AssertPrecursorMz(msd.Run.SpectrumList.GetSpectrum(24, true), 547.2510);
        AssertPrecursorMz(msd.Run.SpectrumList.GetSpectrum(25, true), 533.2534);
        AssertPrecursorMz(msd.Run.SpectrumList.GetSpectrum(26, true), 401.22787);

        var refiner = new SpectrumList_PrecursorRefine(msd);
        Assert.AreEqual(51, refiner.Count);

        // Expected refined m/z values.
        AssertPrecursorMz(refiner.GetSpectrum(21, true), 747.37078);
        AssertPrecursorMz(refiner.GetSpectrum(22, true), 614.86648);
        AssertPrecursorMz(refiner.GetSpectrum(24, true), 547.2507);
        AssertPrecursorMz(refiner.GetSpectrum(25, true), 533.2534);
        AssertPrecursorMz(refiner.GetSpectrum(26, true), 401.226957);
    }

    private static void AssertPrecursorMz(Spectrum s, double expectedMz)
    {
        Assert.IsTrue(s.Precursors.Count > 0, $"index {s.Index} missing precursors");
        var ion = s.Precursors[0].SelectedIons[0];
        double actual = ion.CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>();
        Assert.AreEqual(expectedMz, actual, 1e-4,
            $"index {s.Index}: precursor m/z {actual} differs from expected {expectedMz}");
    }

    private static string FindFixture(string name)
    {
        // The PrecursorRefineOrbi.mzML fixture lives in the cpp tree under
        // SpectrumList_PrecursorRecalculatorTest.data/ (same input feeds both filters' tests).
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string c = Path.Combine(dir, "pwiz", "analysis", "spectrum_processing",
                "SpectrumList_PrecursorRecalculatorTest.data", name);
            if (File.Exists(c)) return c;
            dir = Path.GetDirectoryName(dir);
        }
        Assert.Inconclusive($"test fixture not found: {name}");
        throw new InvalidOperationException("unreachable");
    }

    // ============================================================================
    //   SpectrumList_ChargeStateCalculator
    // ============================================================================

    /// <summary>
    /// Table-driven charge-state prediction across a representative grid of
    /// (input mz/intensity, existing charges, precursor position, override flag, charge range,
    /// fraction threshold, expected charges, makeMS2 flag).
    /// </summary>
    [TestMethod]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "1",     5.0,   true,  2, 3, 0.9, "1",     0, false, DisplayName = "case01_overrideExistingSinglyChargedKeepsSinglyCharged")]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "1 2 3", 5.0,   true,  2, 3, 0.9, "1",     0, false, DisplayName = "case02_overridePossibleChargesAllBelowPrecursor")]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "2 3",   5.0,   true,  2, 3, 0.9, "1",     0, false, DisplayName = "case03_overrideSubsetPossiblesAllBelowPrecursor")]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "",      2.5,   true,  2, 3, 0.9, "2 3",   0, false, DisplayName = "case04_emptyMultiplyCharged")]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "0",     2.5,   true,  2, 3, 0.9, "2 3",   0, false, DisplayName = "case05_bogusZeroTreatedAsNoCharge")]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "2",     2.5,   true,  3, 4, 0.9, "3 4 5", 0, false, DisplayName = "case06_overrideRaisesChargeRange")]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "3 4 5", 2.5,   true,  3, 4, 0.9, "3 4 5", 0, false, DisplayName = "case07_overrideAllPossibles")]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "3",     2.5,   true,  2, 2, 0.9, "2",     0, false, DisplayName = "case08_singleMultiplyCharge")]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "",      5.0,   false, 2, 3, 0.9, "1",     0, false, DisplayName = "case09_noOverrideEmptySinglyCharged")]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "",      2.5,   false, 2, 3, 0.9, "2 3",   0, false, DisplayName = "case10_noOverrideEmptyMultiplyCharged")]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "0",     2.5,   false, 2, 3, 0.9, "2 3",   0, false, DisplayName = "case11_noOverrideBogusZero")]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "1",     2.5,   false, 2, 3, 0.9, "1",     0, false, DisplayName = "case12_noOverrideKeepsExistingCharge")]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "2 3",   5.0,   false, 2, 3, 0.9, "1 2 3", 0, false, DisplayName = "case13_noOverrideAddsToPossibleCharges")]
    [DataRow("1 2 3 4 5", "10 20 30 40 50", "2 3",   2.5,   false, 2, 4, 0.9, "2 3 4", 0, false, DisplayName = "case14_noOverrideExtendsPossibleRange")]
    [DataRow("1218.258 1244.477 1354.132 1391.253", "29.83101 15.71422 9.135175 6.936273",
             "", 1390.47, false, 2, 3, 0.2, "1", 0, true, DisplayName = "case15_makeMs2_singlyCharged")]
    [DataRow("1218.258 1244.477 1354.132 1391.253", "29.83101 15.71422 9.135175 6.936273",
             "", 1390.47, false, 2, 3, 0.00001, "1", 0, true, DisplayName = "case16_makeMs2_singlyChargedTinyFraction")]
    public void ChargeStatePredictor(
        string mzArray, string intensityArray, string inputCharges,
        double precursorMz,
        bool overrideExisting, int minCharge, int maxCharge,
        double singleChargeFraction, string expectedCharges,
        int maxKnownCharge, bool makeMs2)
    {
        var inner = new MemorySpectrumList();
        var mz = ParseDoubleArray(mzArray);
        var intensity = ParseDoubleArray(intensityArray);
        var s = MakeMs2(id: "scan=1", precursorMz: precursorMz, mz: mz, intensity: intensity);

        // Existing charge CVs — pick possible-vs-single by input count: >1 → possible.
        var inputZ = ParseDoubleArray(inputCharges).Select(v => (int)v).ToList();
        if (inputZ.Count > 0)
        {
            var inputTerm = inputZ.Count > 1 ? CVID.MS_possible_charge_state : CVID.MS_charge_state;
            foreach (var z in inputZ)
                s.Precursors[0].SelectedIons[0].CVParams.Add(new CVParam(inputTerm,
                    z.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        s.Precursors[0].Activation.Set(CVID.MS_collision_induced_dissociation);
        inner.Add(s);

        var calc = new SpectrumList_ChargeStateCalculator(inner, overrideExisting, maxCharge,
            minCharge, singleChargeFraction, maxKnownCharge, makeMs2);
        var result = calc.GetSpectrum(0, getBinaryData: true);

        var expectedZ = new HashSet<int>(ParseDoubleArray(expectedCharges).Select(v => (int)v));
        var expectedTerm = expectedZ.Count > 1 ? CVID.MS_possible_charge_state : CVID.MS_charge_state;

        // Every produced charge CV must use the expected term and its int value must appear in
        // the expected list. (Permissive: subset is fine.)
        foreach (var cv in result.Precursors[0].SelectedIons[0].CVParams)
        {
            if (cv.Cvid != CVID.MS_charge_state && cv.Cvid != CVID.MS_possible_charge_state)
                continue;
            Assert.AreEqual(expectedTerm, cv.Cvid,
                $"CV term mismatch — got {cv.Cvid} value={cv.Value}, expected {expectedTerm}.");
            int actualZ = cv.ValueAs<int>();
            Assert.IsTrue(expectedZ.Contains(actualZ),
                $"Produced charge {actualZ} not in expected set {{{string.Join(", ", expectedZ)}}}.");
        }
    }

    [TestMethod]
    public void ChargeStatePredictor_FactoryDispatch()
    {
        var inner = new MemorySpectrumList();
        inner.Add(MakeMs2(id: "scan=1", precursorMz: 500.0,
            mz: new[] { 100.0 }, intensity: new[] { 100.0 }));
        var wrapped = SpectrumListFactory.Wrap(inner,
            "chargeStatePredictor maxMultipleCharge=4 minMultipleCharge=2 singleChargeFractionTIC=0.95");
        Assert.IsInstanceOfType(wrapped, typeof(SpectrumList_ChargeStateCalculator));
    }

    private static double[] ParseDoubleArray(string s) =>
        string.IsNullOrWhiteSpace(s)
            ? Array.Empty<double>()
            : s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
               .Select(t => double.Parse(t, System.Globalization.CultureInfo.InvariantCulture))
               .ToArray();

    // ============================================================================
    //   Helpers
    // ============================================================================

    private static MSData BuildMsdWithAnalyzer(CVID analyzerCvid)
    {
        var msd = new MSData();
        var ic = new InstrumentConfiguration("IC1");
        var c = new Component(ComponentType.Analyzer, 2);
        c.CVParams.Add(new CVParam(analyzerCvid));
        ic.ComponentList.Add(c);
        msd.InstrumentConfigurations.Add(ic);
        return msd;
    }

    private static MemorySpectrumList BuildList_Ms1Ms2Ms1(double precursorMz)
    {
        var sl = new MemorySpectrumList();
        sl.Add(MakeMs1(id: "scan=1", peakMz: precursorMz, peakIntensity: 1000));
        sl.Add(MakeMs2(id: "scan=2", precursorMz: precursorMz,
            mz: new[] { 100.0, 200.0 }, intensity: new[] { 50.0, 50.0 }));
        sl.Add(MakeMs1(id: "scan=3", peakMz: precursorMz, peakIntensity: 1000));
        return sl;
    }

    private static Spectrum MakeMs1(string id, double peakMz, double peakIntensity)
    {
        var s = new Spectrum { Id = id };
        s.Params.Set(CVID.MS_ms_level, 1);
        s.SetMZIntensityArrays(new[] { peakMz - 0.001, peakMz, peakMz + 0.001 },
            new[] { peakIntensity * 0.5, peakIntensity, peakIntensity * 0.5 },
            CVID.MS_number_of_detector_counts);
        s.DefaultArrayLength = 3;
        return s;
    }

    private static Spectrum MakeMs2(string id, double precursorMz, double[] mz, double[] intensity)
    {
        var s = new Spectrum { Id = id };
        s.Params.Set(CVID.MS_ms_level, 2);
        s.Params.Set(CVID.MS_centroid_spectrum);
        var precursor = new Precursor();
        var ion = new SelectedIon();
        ion.Set(CVID.MS_selected_ion_m_z, precursorMz);
        precursor.SelectedIons.Add(ion);
        s.Precursors.Add(precursor);
        s.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
        s.DefaultArrayLength = mz.Length;
        return s;
    }

    private sealed class MemorySpectrumList : Pwiz.Data.MsData.Spectra.SpectrumListBase
    {
        private readonly List<Spectrum> _spectra = new();
        public void Add(Spectrum s) { s.Index = _spectra.Count; _spectra.Add(s); }
        public override int Count => _spectra.Count;
        public override SpectrumIdentity SpectrumIdentity(int index) => new() { Index = index, Id = _spectra[index].Id };
        public override Spectrum GetSpectrum(int index, bool getBinaryData = false) => _spectra[index];
        public override Pwiz.Data.MsData.Processing.DataProcessing? DataProcessing => null;
    }
}
