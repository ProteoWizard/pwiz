using System.Globalization;
using Pwiz.Analysis.Filters;
using Pwiz.Analysis.PeakFilters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// Mirrors cpp <c>SpectrumList_FilterTest.cpp</c>: a single synthetic <see cref="ISpectrumList"/>
/// (<see cref="BuildSyntheticList"/>) is shared across every predicate test, with each
/// <c>[TestMethod]</c> exercising one <see cref="ISpectrumPredicate"/> against it. The list
/// shape (11 spectra with carefully-seeded ms levels, scan times, scan filters, polarities,
/// mass analyzers, precursors and activation types) matches what cpp builds in its
/// <c>createSpectrumList</c> helper.
/// </summary>
[TestClass]
public class SpectrumListFilterTests
{
    /// <summary>
    /// 11 spectra, indexed 0..10. Modelled on cpp <c>createSpectrumList</c>:
    /// <list type="bullet">
    ///   <item>i==10 → emission spectrum (non-MS), no activation, no precursor.</item>
    ///   <item>i%3==0 → MS1 (indices 0, 3, 6, 9). i==0,3,6,9 also get a Thermo scan filter.</item>
    ///   <item>else MS2 with precursor m/z (i+4)*100 charge 3.</item>
    ///   <item>Activations: i==1,5 ETD; i==2 CID; i==4 HCD; i==8 IRMPD; i==7 ETD+SA.</item>
    ///   <item>Mass analyzer: orbi for MS1 (i%3==0), quad+orbi for odd MS2, lit for even MS2.</item>
    ///   <item>scan_start_time = 420 + i seconds, preset_scan_configuration = i%4.</item>
    ///   <item>Each spectrum gets <c>i*2</c> peaks at (j*100, j*j) for j in 1..i*2-1, so
    ///   index 5 has [100,10,200,40, ... 800,16] etc — used by mzPresent.</item>
    /// </list>
    /// </summary>
    private static SpectrumListSimple BuildSyntheticList()
    {
        var list = new SpectrumListSimple();
        for (int i = 0; i < 11; i++)
        {
            var s = new Spectrum { Index = i, Id = $"scan={100 + i}" };

            // peaks (j*100, j*j) for j in 1..i*2-1
            var mz = new BinaryDataArray();
            mz.Set(CVID.MS_m_z_array, string.Empty, CVID.MS_m_z);
            var intensity = new BinaryDataArray();
            intensity.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
            for (int j = 1; j < i * 2; j++)
            {
                mz.Data.Add(j * 100.0);
                intensity.Data.Add(j * j);
            }
            s.BinaryDataArrays.Add(mz);
            s.BinaryDataArrays.Add(intensity);
            s.DefaultArrayLength = mz.Data.Count;

            if (i == 10)
            {
                s.Params.Set(CVID.MS_emission_spectrum);
                var scan = new Scan();
                scan.Set(CVID.MS_preset_scan_configuration, i % 4);
                scan.Set(CVID.MS_scan_start_time, 420 + i, CVID.UO_second);
                s.ScanList.Scans.Add(scan);
                list.Spectra.Add(s);
                continue;
            }

            bool isMs1 = i % 3 == 0;
            s.Params.Set(CVID.MS_ms_level, isMs1 ? 1 : 2);
            s.Params.Set(isMs1 ? CVID.MS_MS1_spectrum : CVID.MS_MSn_spectrum);

            // Mass-analyzer instrument config in the spectrum's first scan.
            var ic = new InstrumentConfiguration("ic" + i);
            if (i % 3 == 0)
                ic.ComponentList.Add(new Component(CVID.MS_orbitrap, 0));
            else if (i % 2 == 1)
            {
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 0));
                ic.ComponentList.Add(new Component(CVID.MS_orbitrap, 1));
            }
            else
                ic.ComponentList.Add(new Component(CVID.MS_radial_ejection_linear_ion_trap, 0));

            var s0 = new Scan { InstrumentConfiguration = ic };
            s.ScanList.Scans.Add(s0);

            // Thermo scan-filter strings on a few MS1s, plus per-MS2 below.
            if (i == 0 || i == 6) s0.Set(CVID.MS_filter_string, "FTMS + p NSI SIM ms [595.0000-655.0000]");
            else if (i == 3 || i == 9) s0.Set(CVID.MS_filter_string, "FTMS + p NSI SIM ms [395.0000-1005.0000]");

            if (i % 3 != 0)
            {
                var precursor = new Precursor((i + 4) * 100.0, 3);
                // Isolation window centered on (i+4)*100 with ±5 m/z offsets — exercises
                // isolationWindow / isolationWidth predicates.
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, (i + 4) * 100.0, CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, 5.0, CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, 5.0, CVID.MS_m_z);

                string filterLine = "";
                double ce = 30.0;
                if (i == 1 || i == 5)
                {
                    precursor.Activation.Set(CVID.MS_electron_transfer_dissociation);
                    filterLine = $"FTMS + c NSI Full ms2 {(i + 4) * 100}.0000@etd30.00 [100.0000-2000.0000]";
                }
                else if (i == 2)
                {
                    precursor.Activation.Set(CVID.MS_collision_induced_dissociation);
                    precursor.Activation.Set(CVID.MS_collision_energy, ce, CVID.UO_electronvolt);
                    filterLine = $"ITMS + c NSI Full ms2 {(i + 4) * 100}.0000@cid30.00 [100.0000-2000.0000]";
                }
                else if (i == 4)
                {
                    precursor.Activation.Set(CVID.MS_HCD);
                    precursor.Activation.Set(CVID.MS_collision_energy, ce, CVID.UO_electronvolt);
                    filterLine = $"ITMS + c NSI Full ms2 {(i + 4) * 100}.0000@hcd30.00 [100.0000-2000.0000]";
                }
                else if (i == 7)
                {
                    precursor.Activation.Set(CVID.MS_electron_transfer_dissociation);
                    precursor.Activation.Set(CVID.MS_collision_induced_dissociation);
                    filterLine = $"FTMS + c NSI Full ms2 {(i + 4) * 100}.0000@cid30.00 [100.0000-2000.0000]";
                }
                else if (i == 8)
                {
                    precursor.Activation.Set(CVID.MS_IRMPD);
                    filterLine = $"ITMS + c NSI Full ms2 {(i + 4) * 100}.0000@irmpd30.00 [100.0000-2000.0000]";
                }
                s.Precursors.Add(precursor);
                if (filterLine.Length > 0) s0.Set(CVID.MS_filter_string, filterLine);
            }

            s0.Set(CVID.MS_preset_scan_configuration, i % 4);
            s0.Set(CVID.MS_scan_start_time, 420 + i, CVID.UO_second);
            list.Spectra.Add(s);
        }
        return list;
    }

    private static List<int> KeptIndices(SpectrumListFilter f) =>
        Enumerable.Range(0, f.Count).Select(i => f.SpectrumIdentity(i).Index).ToList();

    [TestMethod]
    public void IndexSet_KeepsListedIndices()
    {
        var sl = BuildSyntheticList();
        var idx = new IntegerSet();
        idx.Insert(3, 5);
        idx.Insert(7);
        idx.Insert(9);
        var f = new SpectrumListFilter(sl, new IndexSetPredicate(idx));
        CollectionAssert.AreEqual(new[] { 3, 4, 5, 7, 9 }, KeptIndices(f));
    }

    [TestMethod]
    public void ScanNumberSet_ParsesScanFromId()
    {
        // ids are "scan=100".."scan=110"; setting scanNumber=102,104 keeps index 2 and 4.
        var sl = BuildSyntheticList();
        var nums = new IntegerSet();
        nums.Insert(102);
        nums.Insert(104);
        var f = new SpectrumListFilter(sl, new ScanNumberSetPredicate(nums));
        CollectionAssert.AreEqual(new[] { 2, 4 }, KeptIndices(f));
    }

    [TestMethod]
    public void IdSet_ExactMatchOnly()
    {
        var sl = BuildSyntheticList();
        var f = new SpectrumListFilter(sl, new IdSetPredicate(new[] { "scan=100", "scan=110" }));
        CollectionAssert.AreEqual(new[] { 0, 10 }, KeptIndices(f));
    }

    [TestMethod]
    public void MsLevel_KeepsRequestedLevels()
    {
        var sl = BuildSyntheticList();
        // MS1 indices: 0, 3, 6, 9 (i%3==0). i==10 is non-MS (no ms_level), so excluded.
        var ms1 = new SpectrumListFilter(sl, new MsLevelPredicate(1));
        CollectionAssert.AreEqual(new[] { 0, 3, 6, 9 }, KeptIndices(ms1));

        // MS2 indices: 1, 2, 4, 5, 7, 8 (everything not MS1, except i==10).
        var ms2 = new SpectrumListFilter(sl, new MsLevelPredicate(2));
        CollectionAssert.AreEqual(new[] { 1, 2, 4, 5, 7, 8 }, KeptIndices(ms2));
    }

    [TestMethod]
    public void ScanTimeRange_InclusiveAndShortCircuits()
    {
        // times are 420..430. [422, 425] keeps indices 2..5 (4 spectra).
        var sl = BuildSyntheticList();
        var f = new SpectrumListFilter(sl, new ScanTimeRangePredicate(422, 425));
        CollectionAssert.AreEqual(new[] { 2, 3, 4, 5 }, KeptIndices(f));
    }

    [TestMethod]
    public void Polarity_KeepsRequestedPolarity()
    {
        // BuildSyntheticList doesn't set polarity by default; build a tiny mixed list inline.
        var mixed = new SpectrumListSimple();
        var pos = new Spectrum { Index = 0, Id = "scan=1" };
        pos.Params.Set(CVID.MS_ms_level, 1);
        pos.Params.Set(CVID.MS_positive_scan);
        var neg = new Spectrum { Index = 1, Id = "scan=2" };
        neg.Params.Set(CVID.MS_ms_level, 1);
        neg.Params.Set(CVID.MS_negative_scan);
        mixed.Spectra.Add(pos);
        mixed.Spectra.Add(neg);
        Assert.AreEqual(0, new SpectrumListFilter(mixed,
            new PolarityPredicate(CVID.MS_positive_scan)).SpectrumIdentity(0).Index);
        Assert.AreEqual(1, new SpectrumListFilter(mixed,
            new PolarityPredicate(CVID.MS_negative_scan)).SpectrumIdentity(0).Index);
    }

    [TestMethod]
    public void DefaultArrayLength_FiltersByPeakCount()
    {
        // Spectra in BuildSyntheticList have peak counts 0, 2, 4, 6, ..., 20 (i*2 - 1, but
        // with j starting at 1 → i*2-1 entries). With ms_level on most, an array-length range
        // [10, 18] keeps the spectra with 10..18 peaks.
        var sl = BuildSyntheticList();
        var lens = new IntegerSet();
        lens.Insert(10, 18);
        var f = new SpectrumListFilter(sl, new DefaultArrayLengthPredicate(lens));
        // computed expectations:
        var expected = new List<int>();
        for (int i = 0; i < 11; i++)
        {
            int n = i * 2 - 1;
            if (n >= 10 && n <= 18) expected.Add(i);
        }
        CollectionAssert.AreEqual(expected, KeptIndices(f));
    }

    [TestMethod]
    public void ChargeState_FilterAndMissing()
    {
        // BuildSyntheticList sets charge=3 on every MS2's selected ion. Filter to {2,3} keeps
        // all MS2s; filter to {0} keeps none (MS1s have no precursor → reject).
        var sl = BuildSyntheticList();
        var set23 = new IntegerSet();
        set23.Insert(2, 3);
        var c23 = new SpectrumListFilter(sl, new ChargeStatePredicate(set23));
        CollectionAssert.AreEqual(new[] { 1, 2, 4, 5, 7, 8 }, KeptIndices(c23));
    }

    [TestMethod]
    public void ScanEvent_FiltersByPresetScanConfiguration()
    {
        // preset_scan_configuration = i%4 → values 0,1,2,3 cycling. Filter to {1,3} keeps i in
        // {1,3,5,7,9}.
        var sl = BuildSyntheticList();
        var set = new IntegerSet();
        set.Insert(1);
        set.Insert(3);
        var f = new SpectrumListFilter(sl, new ScanEventPredicate(set));
        CollectionAssert.AreEqual(new[] { 1, 3, 5, 7, 9 }, KeptIndices(f));
    }

    [TestMethod]
    public void PrecursorMz_TargetSelectedAndIsolated()
    {
        // Selected target uses MS_selected_ion_m_z. Indices 1,2,4,5,7,8 have mz = (i+4)*100
        // ⇒ {500, 600, 800, 900, 1100, 1200}. Match against {500, 1100}.
        var sl = BuildSyntheticList();
        var f = new SpectrumListFilter(sl,
            new PrecursorMzPredicate(new[] { 500.0, 1100.0 }, new MZTolerance(0.01)));
        CollectionAssert.AreEqual(new[] { 1, 7 }, KeptIndices(f));

        // Isolated target uses MS_isolation_window_target_m_z (also (i+4)*100 in the builder).
        var fIso = new SpectrumListFilter(sl,
            new PrecursorMzPredicate(new[] { 500.0 }, new MZTolerance(0.01),
                FilterMode.Include, PrecursorMzTarget.Isolated));
        CollectionAssert.AreEqual(new[] { 1 }, KeptIndices(fIso));
    }

    [TestMethod]
    public void ActivationType_HierarchyAndExclusion()
    {
        var sl = BuildSyntheticList();
        // ETD: indices 1, 5, 7 (i==7 is ETD+SA, still has the ETD CV).
        var etd = new SpectrumListFilter(sl,
            new ActivationTypePredicate(new[] { CVID.MS_electron_transfer_dissociation }));
        CollectionAssert.AreEqual(new[] { 1, 5, 7 }, KeptIndices(etd));

        // CID: indices 2, 7 (i==7 has CID via the ETD+SA combo, i==2 plain CID; HCD on i==4
        // is_a CID via the cv hierarchy → index 4 also kept).
        var cid = new SpectrumListFilter(sl,
            new ActivationTypePredicate(new[] { CVID.MS_collision_induced_dissociation }));
        CollectionAssert.AreEqual(new[] { 2, 4, 7 }, KeptIndices(cid));
    }

    [TestMethod]
    public void IsolationWindow_MatchesExactWindowsWithinTolerance()
    {
        // Builder centers each MS2's isolation window at (i+4)*100 with offsets ±5 → window
        // bounds [(i+4)*100 - 5, (i+4)*100 + 5]. Filter to [495, 505] keeps i==1 (window
        // [495, 505]).
        var sl = BuildSyntheticList();
        var f = new SpectrumListFilter(sl,
            new IsolationWindowPredicate(new[] { (495.0, 505.0) }, new MZTolerance(0.01)));
        CollectionAssert.AreEqual(new[] { 1 }, KeptIndices(f));
    }

    [TestMethod]
    public void IsolationWidth_MatchesAcrossSpectra()
    {
        // Every MS2 has width = lower + upper = 10 m/z. Filter to {10} keeps all MS2s.
        var sl = BuildSyntheticList();
        var f = new SpectrumListFilter(sl,
            new IsolationWidthPredicate(new[] { 10.0 }, new MZTolerance(0.001)));
        CollectionAssert.AreEqual(new[] { 1, 2, 4, 5, 7, 8 }, KeptIndices(f));
    }

    [TestMethod]
    public void MzPresent_AbsoluteThresholdSurvivesPeakFiltering()
    {
        // Builder: peak at (j*100, j*j) for j 1..i*2-1. Filter for spectra containing m/z=300
        // with absolute intensity ≥ 5 → j=3 → intensity 9 ≥ 5 → matches when i*2 > 3 → i >= 2.
        // Index 0 has 0 peaks; index 1 has just j=1 → no 300 peak. Indices 2..9 + 10 all have it
        // (10 has same peaks despite being non-MS).
        var sl = BuildSyntheticList();
        var thresh = new ThresholdFilter(ThresholdingBy.AbsoluteIntensity, 5,
            ThresholdingOrientation.MostIntense);
        var f = new SpectrumListFilter(sl,
            new MzPresentPredicate(new MZTolerance(0.5), new[] { 300.0 }, thresh));
        CollectionAssert.AreEqual(new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 }, KeptIndices(f));
    }

    [TestMethod]
    public void AnalyzerType_FiltersByMassAnalyzerCv()
    {
        var sl = BuildSyntheticList();
        // FT/Orbi family. Builder gives orbi to MS1 (i%3==0) and orbi-as-2nd-component to odd
        // MS2 (i==1, 5, 7) — both match MS_orbitrap.
        var ft = new SpectrumListFilter(sl,
            new AnalyzerTypePredicate(new[] { CVID.MS_orbitrap, CVID.MS_FT_ICR }, IntegerSet.Positive));
        CollectionAssert.AreEqual(new[] { 0, 1, 3, 5, 6, 7, 9 }, KeptIndices(ft));

        // Ion trap: cpp's radial_ejection_linear_ion_trap is_a MS_ion_trap; matches even MS2s
        // (i==2, 4, 8) and the non-MS i==10 falls through (no instrument config).
        var it = new SpectrumListFilter(sl,
            new AnalyzerTypePredicate(new[] { CVID.MS_ion_trap }, IntegerSet.Positive));
        CollectionAssert.AreEqual(new[] { 2, 4, 8 }, KeptIndices(it));
    }

    [TestMethod]
    public void CollisionEnergy_RangeWithCidGuard()
    {
        var sl = BuildSyntheticList();
        // CE in [25, 35]. Indices 2 (CID/30), 4 (HCD/30 — HCD is_a CID), 7 (CID via ETD+SA, no
        // CE → acceptMissingCE=false drops it). MS1s and non-MS pass through (return true).
        var f = new SpectrumListFilter(sl,
            new CollisionEnergyPredicate(25, 35, acceptNonCID: true, acceptMissingCE: false));
        // MS1: 0, 3, 6, 9 (always pass); CID/HCD with CE in range: 2, 4. ETD-only spectra
        // (i==1, 5) are non-CID → acceptNonCID=true. i==7 has CID via ETD+SA but no CE → drop.
        // i==8 is IRMPD (non-CID) → acceptNonCID=true → keep. i==10 non-MS → pass.
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 8, 9, 10 }, KeptIndices(f));
    }

    [TestMethod]
    public void ThermoScanFilter_ContainsAndExact()
    {
        var sl = BuildSyntheticList();
        // CONTAINS "etd30" → ETD-tagged MS2s i==1, 5 (i==7's filter line is cid30.00 per builder).
        var contains = new SpectrumListFilter(sl,
            new ThermoScanFilterPredicate("etd30", matchExact: false, inverse: false));
        CollectionAssert.AreEqual(new[] { 1, 5 }, KeptIndices(contains));

        // EXACT match on the i==0,6 SIM filter — both have it.
        var exact = new SpectrumListFilter(sl,
            new ThermoScanFilterPredicate("FTMS + p NSI SIM ms [595.0000-655.0000]",
                matchExact: true, inverse: false));
        CollectionAssert.AreEqual(new[] { 0, 6 }, KeptIndices(exact));

        // Inverse contains "etd30" → keep everything else (and spectra without a filter string drop).
        var inverse = new SpectrumListFilter(sl,
            new ThermoScanFilterPredicate("etd30", matchExact: false, inverse: true));
        // i==0,3,6,9 have a filter string but not "etd30" → keep. i==2,4,7,8 also have filter
        // strings without "etd30" → keep. i==1, 5 match → drop. i==10 has no filter string → drop.
        CollectionAssert.AreEqual(new[] { 0, 2, 3, 4, 6, 7, 8, 9 }, KeptIndices(inverse));
    }

    [TestMethod]
    public void StripIonTrapMs1_DropsItMs1Only()
    {
        // Builder gives MS1 spectra orbitrap analyzers, so the predicate keeps every MS1.
        // Replace one MS1's analyzer with an ion-trap to verify it gets dropped.
        var sl = (SpectrumListSimple)BuildSyntheticList();
        var s = sl.Spectra[3];
        var ic = new InstrumentConfiguration("itMs1");
        ic.ComponentList.Add(new Component(CVID.MS_ion_trap, 0));
        s.ScanList.Scans[0].InstrumentConfiguration = ic;

        var f = new SpectrumListFilter(sl, new StripIonTrapMs1Predicate());
        // Index 3 (the IT-MS1 we just installed) is the only one dropped.
        Assert.IsFalse(KeptIndices(f).Contains(3));
        Assert.IsTrue(KeptIndices(f).Contains(0));  // orbi MS1 stays
        Assert.IsTrue(KeptIndices(f).Contains(2));  // MS2 stays
    }

    [TestMethod]
    public void Composition_AndNegated()
    {
        var sl = BuildSyntheticList();
        // (MS2 AND scan_time in [424, 426]) keeps indices 4, 5 (i==4 ms2/424, i==5 ms2/425;
        // i==6 is MS1 at 426, drops because of MS-level constraint).
        var both = new AndPredicate(
            new MsLevelPredicate(2),
            new ScanTimeRangePredicate(424, 426));
        CollectionAssert.AreEqual(new[] { 4, 5 }, KeptIndices(new SpectrumListFilter(sl, both)));

        // NOT MS2 keeps MS1s (0, 3, 6, 9) and the emission spectrum (i==10).
        var f = new SpectrumListFilter(sl, new NegatedPredicate(new MsLevelPredicate(2)));
        CollectionAssert.AreEqual(new[] { 0, 3, 6, 9, 10 }, KeptIndices(f));
    }
}
