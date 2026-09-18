using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// Mirrors cpp's SpectrumList_ChargeFromIsotopeTest.cpp: for each fixture, one profile MS1
/// survey scan plus one MS2 whose precursor falls inside it, run through the turbocharger and
/// checked against the charge, refined monoisotopic m/z, and CV param units cpp produces.
/// </summary>
[TestClass]
public class SpectrumList_ChargeFromIsotopeTests
{
    /// <summary>One row of cpp's testChargeStateCalculators[] table, plus the charge and refined
    /// monoisotopic m/z cpp actually produces. The m/z and intensity arrays are cpp's string
    /// literals, moved to files under SpectrumList_ChargeFromIsotopeTest.data/ because they run
    /// to hundreds of peaks each.</summary>
    /// <param name="ExpectedCharge">Charge cpp assigns, or 0 where cpp assigns none.</param>
    /// <param name="ExpectedMz">Monoisotopic m/z cpp writes back to the selected ion. Ignored
    /// when <paramref name="ExpectedCharge"/> is 0, since cpp leaves the precursor m/z alone.</param>
    private sealed record Case(
        string Name, double PrecursorMz, int TrueCharge, double Ms1Rtime, double Ms2Rtime,
        int MinCharge, int MaxCharge, int ParentsBefore, int ParentsAfter,
        double HalfIsolationWidth, int DefaultMinCharge, int DefaultMaxCharge,
        int ExpectedCharge, double ExpectedMz);

    // Thermo Q-Exactive Jurkat cell lysate data, verbatim from cpp. Cases 5 and 8 put the MS2
    // ahead of its survey scan in time, which is what exercises the parentsAfter path.
    //
    // ExpectedCharge/ExpectedMz were read out of the cpp test binary (an added printf; not
    // committed) rather than assumed, because cpp's own assertions live *inside* its loop over
    // the emitted charge CV params and so assert nothing at all when none are emitted. Case 10
    // is exactly that: cpp emits no charge for it either, so its trueCharge=2 has never been
    // checked there and is pinned here as "no assignment" instead, to hold the parity.
    // Why case 10 declines: its survey scan holds only three CWT peaks, the weakest of which is
    // dropped as the running minimum, leaving one 2-peak chain at the right charge and m/z whose
    // K-L p-value (0.39) misses the 0.30 significance gate. Both implementations agree.
    private static readonly Case[] CASES =
    {
        //        name        precursor  z  MS1 rt  MS2 rt  min max before after halfIso dMin dMax  cpp z  cpp m/z
        new("case01", 352.189, 2,  10.0,  20.0,  2, 6, 1, 0, 1.25, 0, 0,  2, 352.188),
        new("case02", 347.679, 2,  10.0,  10.1,  2, 6, 1, 0, 1.25, 0, 0,  2, 347.679),
        new("case03", 332.66,  2,   5.0,   6.0,  2, 6, 1, 0, 1.25, 0, 0,  2, 332.661),
        new("case04", 324.681, 4,   1.0,   1.01, 2, 6, 1, 0, 1.25, 0, 0,  4, 324.681),
        new("case05", 318.169, 3,   5.0,   4.0,  2, 6, 0, 1, 1.25, 0, 0,  3, 317.841),
        new("case06", 415.888, 3,  50.0, 102.6,  2, 6, 1, 0, 1.25, 0, 0,  3, 415.888),
        new("case07", 301.665, 2,  12.99, 13.01, 2, 6, 1, 0, 1.25, 0, 0,  2, 301.665),
        new("case08", 452.463, 4,  50.1,  49.6,  2, 6, 0, 1, 1.25, 0, 0,  4, 452.464),
        new("case09", 308.849, 3, 120.2, 121.2,  2, 6, 1, 0, 1.25, 0, 0,  3, 308.849),
        new("case10", 507.744, 2,  10.8,  10.9,  2, 6, 1, 0, 1.25, 0, 0,  0, 0.0),
    };

    [TestMethod]
    public void AssignsChargeFromParentIsotopePattern()
    {
        string dataDir = FindFixtureDir();
        var failures = new List<string>();

        foreach (var c in CASES)
        {
            var mz = LoadDoubles(Path.Combine(dataDir, c.Name + ".mz.txt"));
            var intensity = LoadDoubles(Path.Combine(dataDir, c.Name + ".intensity.txt"));
            Assert.AreEqual(mz.Length, intensity.Length, $"{c.Name}: m/z and intensity length mismatch");

            var msd = BuildMsd(c, mz, intensity);
            var turbocharger = new SpectrumList_ChargeFromIsotope(msd, c.MaxCharge, c.MinCharge,
                c.ParentsBefore, c.ParentsAfter, c.HalfIsolationWidth, c.DefaultMaxCharge, c.DefaultMinCharge);

            // Index 1 is the MS2.
            var ion = turbocharger.GetSpectrum(1, getBinaryData: true).Precursors[0].SelectedIons[0];
            var chargeParams = ion.CVParams
                .Where(p => p.Cvid is CVID.MS_charge_state or CVID.MS_possible_charge_state)
                .ToList();

            int expectedCount = c.ExpectedCharge == 0 ? 0 : 1;
            if (chargeParams.Count != expectedCount)
            {
                failures.Add($"{c.Name}: expected {expectedCount} charge CV param(s), got {chargeParams.Count}"
                    + $" [{string.Join(", ", chargeParams.Select(p => $"{p.Cvid}={p.Value}"))}]");
                continue;
            }

            if (c.ExpectedCharge == 0)
            {
                // No isotope chain cleared the significance gate, so the precursor m/z is left
                // exactly as the instrument reported it - units included.
                AssertMz(failures, c, ion, c.PrecursorMz, CVID.MS_m_z);
                continue;
            }

            if (chargeParams[0].Cvid != CVID.MS_charge_state)
            {
                failures.Add($"{c.Name}: expected {CVID.MS_charge_state}, got {chargeParams[0].Cvid}");
                continue;
            }

            int assigned = chargeParams[0].ValueAs<int>();
            if (assigned != c.ExpectedCharge)
                failures.Add($"{c.Name}: charge {assigned}, expected {c.ExpectedCharge}");
            if (assigned != c.TrueCharge)
                failures.Add($"{c.Name}: charge {assigned} is not the known true charge {c.TrueCharge}");

            // The chain's first peak replaces the reported precursor m/z, so this pins the whole
            // chain-selection path, not just the spacing that implies the charge. cpp rewrites it
            // with no units argument, which drops the MS_m_z unit the value arrived with; that is
            // what lands in the mzML, so the port has to do the same.
            AssertMz(failures, c, ion, c.ExpectedMz, CVID.CVID_Unknown);
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    /// <summary>The default-charge fallback, which only runs when no isotope chain is found -
    /// so case10, the one fixture the algorithm declines, is the only one that reaches it.</summary>
    [TestMethod]
    public void EmitsDefaultChargesWhenNoIsotopeFound()
    {
        var c = CASES.Single(x => x.Name == "case10") with { DefaultMinCharge = 2, DefaultMaxCharge = 3 };
        string dataDir = FindFixtureDir();
        var msd = BuildMsd(c,
            LoadDoubles(Path.Combine(dataDir, c.Name + ".mz.txt")),
            LoadDoubles(Path.Combine(dataDir, c.Name + ".intensity.txt")));

        var turbocharger = new SpectrumList_ChargeFromIsotope(msd, c.MaxCharge, c.MinCharge,
            c.ParentsBefore, c.ParentsAfter, c.HalfIsolationWidth, c.DefaultMaxCharge, c.DefaultMinCharge);
        var ion = turbocharger.GetSpectrum(1, getBinaryData: true).Precursors[0].SelectedIons[0];

        // cpp emits possible-charge-state 2 and 3, and leaves the precursor m/z untouched.
        CollectionAssert.AreEqual(new[] { 2, 3 },
            ion.CVParams.Where(p => p.Cvid == CVID.MS_possible_charge_state)
                        .Select(p => p.ValueAs<int>()).ToArray());
        Assert.AreEqual(0, ion.CVParams.Count(p => p.Cvid == CVID.MS_charge_state));
        var failures = new List<string>();
        AssertMz(failures, c, ion, c.PrecursorMz, CVID.MS_m_z);
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void FactoryDispatchesTurbocharger()
    {
        var msd = BuildMsd(CASES[0], new[] { 400.0, 400.5 }, new[] { 100.0, 50.0 });
        var wrapped = SpectrumListFactory.Wrap(msd.Run.SpectrumList!,
            "turbocharger minCharge=2 maxCharge=6 precursorsBefore=1 precursorsAfter=0 halfIsoWidth=1.25",
            msd);
        Assert.IsInstanceOfType<SpectrumList_ChargeFromIsotope>(wrapped);
    }

    // ============================================================================
    //   Helpers
    // ============================================================================

    private static void AssertMz(List<string> failures, Case c, SelectedIon ion, double expectedMz, CVID expectedUnits)
    {
        var param = ion.CvParam(CVID.MS_selected_ion_m_z);
        double actual = param.ValueAs<double>();
        if (System.Math.Abs(actual - expectedMz) > 1e-6)
            failures.Add($"{c.Name}: selected ion m/z {actual:R}, expected {expectedMz:R}");
        if (param.Units != expectedUnits)
            failures.Add($"{c.Name}: selected ion m/z units {param.Units}, expected {expectedUnits}");
    }

    /// <summary>Builds cpp's two-spectrum MSData: a profile MS1 survey holding the fixture's
    /// peaks, then an MS2 whose only meaningful content is its precursor m/z and scan time.</summary>
    private static MSData BuildMsd(Case c, double[] mz, double[] intensity)
    {
        var msd = new MSData();
        var sl = new SpectrumListSimple();
        msd.Run.SpectrumList = sl;

        var ms1 = new Spectrum { Index = 0, Id = "scan=1" };
        ms1.Params.Set(CVID.MS_MSn_spectrum);
        ms1.Params.Set(CVID.MS_ms_level, 1);
        ms1.Params.Set(CVID.MS_profile_spectrum);
        var ms1Scan = new Scan();
        ms1Scan.Set(CVID.MS_scan_start_time, c.Ms1Rtime, CVID.UO_second);
        ms1Scan.Set(CVID.MS_preset_scan_configuration, 1);
        ms1.ScanList.Scans.Add(ms1Scan);
        ms1.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
        ms1.DefaultArrayLength = mz.Length;
        sl.Spectra.Add(ms1);

        var ms2 = new Spectrum { Index = 1, Id = "scan=2" };
        ms2.Params.Set(CVID.MS_MSn_spectrum);
        ms2.Params.Set(CVID.MS_ms_level, 2);
        ms2.Params.Set(CVID.MS_profile_spectrum);
        var ms2Scan = new Scan();
        ms2Scan.Set(CVID.MS_scan_start_time, c.Ms2Rtime, CVID.UO_second);
        ms2Scan.Set(CVID.MS_preset_scan_configuration, 2);
        ms2.ScanList.Scans.Add(ms2Scan);
        ms2.Precursors.Add(new Precursor(c.PrecursorMz));
        ms2.DefaultArrayLength = 10; // arbitrary non-zero so the filter doesn't skip it
        sl.Spectra.Add(ms2);

        return msd;
    }

    private static string FindFixtureDir()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string c = Path.Combine(dir, "test", "Analysis.Tests", "SpectrumProcessing",
                "SpectrumList_ChargeFromIsotopeTest.data");
            if (Directory.Exists(c)) return c;
            dir = Path.GetDirectoryName(dir);
        }
        Assert.Inconclusive("SpectrumList_ChargeFromIsotopeTest.data not found");
        throw new InvalidOperationException("unreachable");
    }

    private static double[] LoadDoubles(string path) =>
        File.ReadAllText(path)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => double.Parse(t, CultureInfo.InvariantCulture))
            .ToArray();
}
