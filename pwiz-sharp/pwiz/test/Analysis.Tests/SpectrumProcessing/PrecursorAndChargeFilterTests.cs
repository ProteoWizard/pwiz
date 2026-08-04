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
    /// All 43 rows of cpp's testChargeStateCalculators[] table, in cpp's order.
    /// </summary>
    /// <remarks>
    /// <para>The expected column holds what cpp's binary actually <em>emits</em> - captured from an
    /// instrumented run - not cpp's own expected column, which is a permissive superset that its
    /// subset-only assertion never tightens. Rows 06/07 are the clearest example: cpp's table says
    /// "3 4 5" and cpp emits only p3 p4. Terms are prefixed (z = charge state, p = possible charge
    /// state) and order is significant, because cpp emits p1 last in the no-override rows and emits
    /// p2 twice in row 31.</para>
    /// <para>Rows whose expectation is prefixed <c>SVM:</c> are the ETD cases cpp resolves with a
    /// libsvm model the port deliberately does not carry (see
    /// SpectrumList_ChargeStateCalculator's remarks). The value after the prefix is what cpp
    /// emits; the assertion checks the port's documented fall-through instead, so the gap stays
    /// visible and this table already holds the answer if the SVM path is ever ported.</para>
    /// </remarks>
    [TestMethod]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "1", "CID", 5, true, 2, 3, 0.9, "z1", 0, false, DisplayName = "case01")]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "1 2 3", "CID", 5, true, 2, 3, 0.9, "z1", 0, false, DisplayName = "case02")]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "2 3", "CID", 5, true, 2, 3, 0.9, "z1", 0, false, DisplayName = "case03")]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "", "CID", 2.5, true, 2, 3, 0.9, "p2 p3", 0, false, DisplayName = "case04")]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "0", "CID", 2.5, true, 2, 3, 0.9, "p2 p3", 0, false, DisplayName = "case05")]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "2", "CID", 2.5, true, 3, 4, 0.9, "p3 p4", 0, false, DisplayName = "case06")]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "3 4 5", "CID", 2.5, true, 3, 4, 0.9, "p3 p4", 0, false, DisplayName = "case07")]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "3", "CID", 2.5, true, 2, 2, 0.9, "z2", 0, false, DisplayName = "case08")]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "", "CID", 5, false, 2, 3, 0.9, "z1", 0, false, DisplayName = "case09")]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "", "CID", 2.5, false, 2, 3, 0.9, "p2 p3", 0, false, DisplayName = "case10")]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "0", "CID", 2.5, false, 2, 3, 0.9, "p2 p3", 0, false, DisplayName = "case11")]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "1", "CID", 2.5, false, 2, 3, 0.9, "z1", 0, false, DisplayName = "case12")]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "2 3", "CID", 5, false, 2, 3, 0.9, "p2 p3 p1", 0, false, DisplayName = "case13")]
    [DataRow("", "1 2 3 4 5", "10 20 30 40 50", "2 3", "CID", 2.5, false, 2, 4, 0.9, "p2 p3 p4", 0, false, DisplayName = "case14")]
    [DataRow("spectrum_bebfc4e8", "", "", "7", "ETD", 529.7, true, 2, 4, 0.9, "SVM: z7", 0, false, DisplayName = "case15")]
    [DataRow("spectrum_3463efdc", "", "", "6", "ETD", 695.04, true, 2, 4, 0.9, "SVM: p3 p6", 0, false, DisplayName = "case16")]
    [DataRow("spectrum_2d9b08d4", "", "", "5", "ETD", 771.98, true, 2, 4, 0.9, "SVM: z5", 0, false, DisplayName = "case17")]
    [DataRow("spectrum_cd3da363", "", "", "3", "ETD", 754.26, true, 2, 4, 0.9, "SVM: p2 p3 p4 p6", 0, false, DisplayName = "case18")]
    [DataRow("spectrum_e6830e14", "", "", "4", "ETD", 896.93, true, 2, 4, 0.9, "SVM: z4", 0, false, DisplayName = "case19")]
    [DataRow("spectrum_5224d69c", "", "", "2", "CID ETD", 617.86, true, 2, 4, 0.9, "SVM: z2", 0, false, DisplayName = "case20")]
    [DataRow("spectrum_e95ad20a", "", "", "2", "CID ETD", 828.69, true, 2, 4, 0.9, "SVM: z2", 0, false, DisplayName = "case21")]
    [DataRow("spectrum_51f826c3", "", "", "3", "CID ETD", 515.67, true, 2, 4, 0.9, "SVM: z3", 0, false, DisplayName = "case22")]
    [DataRow("spectrum_2df0cd62", "", "", "4", "CID ETD", 665.96, true, 2, 4, 0.9, "SVM: p2 p3 p4 p6", 0, false, DisplayName = "case23")]
    [DataRow("spectrum_8a704408", "", "", "2", "CID ETD", 1066.72, true, 2, 4, 0.9, "SVM: p2 p3 p4 p6", 0, false, DisplayName = "case24")]
    [DataRow("spectrum_000a1bff", "", "", "1", "CID", 429.03, false, 2, 4, 0.2, "z1", 0, true, DisplayName = "case25")]
    [DataRow("spectrum_000a1bff", "", "", "2 3", "CID", 429.03, false, 2, 4, 0.2, "p2 p3 p1", 0, true, DisplayName = "case26")]
    [DataRow("spectrum_000a1bff", "", "", "2 3", "ETD", 429.03, false, 2, 4, 0.2, "p2 p3 p1", 0, true, DisplayName = "case27")]
    [DataRow("spectrum_000a1bff", "", "", "2 3", "CID", 429.03, false, 2, 2, 0.2, "p2 p3 p1", 0, true, DisplayName = "case28")]
    [DataRow("spectrum_000a1bff", "", "", "2 3", "CID", 429.03, false, 2, 4, 0.00001, "p2 p3 p4", 0, true, DisplayName = "case29")]
    [DataRow("spectrum_000a1bff", "", "", "2 3 4", "CID", 429.03, false, 2, 3, 0.00001, "p2 p3 p4", 0, true, DisplayName = "case30")]
    [DataRow("spectrum_000a1bff", "", "", "2 3", "CID", 429.03, false, 2, 2, 0.00001, "p2 p3 p2", 0, true, DisplayName = "case31")]
    [DataRow("spectrum_000a1bff", "", "", "1", "CID", 429.03, false, 2, 4, 0.00001, "z1", 0, true, DisplayName = "case32")]
    [DataRow("spectrum_000a1bff", "", "", "1", "ETD", 429.03, false, 2, 4, 0.00001, "z1", 0, true, DisplayName = "case33")]
    [DataRow("spectrum_000a1bff", "", "", "", "CID", 429.03, false, 2, 3, 0.2, "z1", 0, true, DisplayName = "case34")]
    [DataRow("spectrum_000a1bff", "", "", "", "ETD", 429.03, true, 2, 3, 0.2, "z1", 0, true, DisplayName = "case35")]
    [DataRow("spectrum_000a1bff", "", "", "2 3", "CID", 429.03, true, 2, 4, 0.2, "z1", 0, true, DisplayName = "case36")]
    [DataRow("spectrum_000a1bff", "", "", "", "CID", 429.03, false, 2, 4, 0.2, "z1", 0, true, DisplayName = "case37")]
    [DataRow("spectrum_000a1bff", "", "", "", "CID", 429.03, false, 2, 4, 0.00001, "p2 p3 p4", 0, true, DisplayName = "case38")]
    [DataRow("spectrum_000a1bff", "", "", "", "CID", 429.03, true, 2, 3, 0.00001, "p2 p3", 0, true, DisplayName = "case39")]
    [DataRow("spectrum_000a1bff", "", "", "", "ETD", 429.03, true, 2, 3, 0.00001, "p2 p3", 0, true, DisplayName = "case40")]
    [DataRow("spectrum_000a1bff", "", "", "1", "CID", 429.03, true, 2, 4, 0.00001, "p2 p3 p4", 0, true, DisplayName = "case41")]
    [DataRow("", "1218.258 1244.477 1354.132 1391.253", "29.83101 15.71422 9.135175 6.936273", "", "CID", 1390.47, false, 2, 3, 0.2, "z1", 0, true, DisplayName = "case42")]
    [DataRow("", "1218.258 1244.477 1354.132 1391.253", "29.83101 15.71422 9.135175 6.936273", "", "CID", 1390.47, false, 2, 3, 0.00001, "z1", 0, true, DisplayName = "case43")]
    public void ChargeStatePredictor(
        string spectrumKey, string mzArray, string intensityArray, string inputCharges,
        string activationTypes, double precursorMz,
        bool overrideExisting, int minCharge, int maxCharge,
        double singleChargeFraction, string expectedEmission,
        int maxKnownCharge, bool makeMs2)
    {
        double[] mz, intensity;
        if (spectrumKey.Length > 0)
            (mz, intensity) = LoadChargeStateSpectrum(spectrumKey);
        else
            (mz, intensity) = (ParseDoubleArray(mzArray), ParseDoubleArray(intensityArray));

        var inner = new MemorySpectrumList();
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

        foreach (var token in activationTypes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            s.Precursors[0].Activation.Set(token switch
            {
                "CID" => CVID.MS_collision_induced_dissociation,
                "ETD" => CVID.MS_electron_transfer_dissociation,
                _ => throw new ArgumentException($"unhandled activation '{token}'"),
            });
        }
        inner.Add(s);

        var calc = new SpectrumList_ChargeStateCalculator(inner, overrideExisting, maxCharge,
            minCharge, singleChargeFraction, maxKnownCharge, makeMs2);
        var result = calc.GetSpectrum(0, getBinaryData: true);

        var actual = result.Precursors[0].SelectedIons[0].CVParams
            .Where(p => p.Cvid is CVID.MS_charge_state or CVID.MS_possible_charge_state)
            .Select(p => (p.Cvid == CVID.MS_charge_state ? "z" : "p") + p.ValueAs<int>())
            .ToList();

        if (expectedEmission.StartsWith("SVM:", StringComparison.Ordinal))
        {
            // cpp picks a single charge here via its libsvm model; the port has no SVM and falls
            // through to enumerating [minCharge, maxCharge]. Assert the fall-through, and keep
            // cpp's answer in the row so the divergence is documented rather than invisible.
            string cppAnswer = expectedEmission["SVM:".Length..].Trim();
            var fallThrough = Enumerable.Range(minCharge, maxCharge - minCharge + 1)
                .Select(z => "p" + z).ToList();
            CollectionAssert.AreEqual(fallThrough, actual,
                $"SVM-dependent row: expected the port's fall-through [{string.Join(" ", fallThrough)}], "
                + $"got [{string.Join(" ", actual)}]. cpp emits [{cppAnswer}] via libsvm.");
            return;
        }

        // Exact sequence, not a set: cpp emits p1 last in the no-override rows and emits p2 twice
        // in case 31, and both are behaviours a set comparison would silently accept.
        CollectionAssert.AreEqual(
            expectedEmission.Split(' ', StringSplitOptions.RemoveEmptyEntries), actual,
            $"expected [{expectedEmission}], got [{string.Join(" ", actual)}]");
    }

    private static (double[] mz, double[] intensity) LoadChargeStateSpectrum(string key)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string d = Path.Combine(dir, "test", "Analysis.Tests", "SpectrumProcessing",
                "SpectrumList_ChargeStateCalculatorTest.data");
            if (Directory.Exists(d))
                return (LoadDoubleFile(Path.Combine(d, key + ".mz.txt")),
                        LoadDoubleFile(Path.Combine(d, key + ".intensity.txt")));
            dir = Path.GetDirectoryName(dir);
        }
        Assert.Inconclusive("SpectrumList_ChargeStateCalculatorTest.data not found");
        throw new InvalidOperationException("unreachable");
    }

    private static double[] LoadDoubleFile(string path) =>
        File.ReadAllText(path)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => double.Parse(t, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

    [TestMethod]
    public void ChargeStatePredictor_FactoryDispatch()
    {
        var inner = new MemorySpectrumList();
        inner.Add(MakeMs2(id: "scan=1", precursorMz: 500.0,
            mz: new[] { 100.0 }, intensity: new[] { 100.0 }));
        var wrapped = SpectrumListFactory.Wrap(inner,
            "chargeStatePredictor maxMultipleCharge=4 minMultipleCharge=2 singleChargeFractionTIC=0.95");
        Assert.IsInstanceOfType<SpectrumList_ChargeStateCalculator>(wrapped);
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
