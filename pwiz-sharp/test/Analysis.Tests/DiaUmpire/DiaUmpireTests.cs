using System.IO;
using System.Linq;
using Pwiz.Analysis.DiaUmpire;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;

// Avoid the type/namespace collision between Pwiz.Analysis.DiaUmpire (namespace)
// and Pwiz.Analysis.DiaUmpire.DiaUmpire (class).
using DiaUmpireProcessor = Pwiz.Analysis.DiaUmpire.DiaUmpire;

namespace Pwiz.Analysis.Tests.DiaUmpire;

/// <summary>
/// End-to-end behavior tests for <see cref="DiaUmpire"/>. Most leaf-level coverage
/// (peak curves, clusters, math, scan data) lives implicitly here and in
/// <see cref="DiaUmpireParityTests"/> — the synthetic SWATH pipeline below
/// exercises those code paths. The tests in this file are explicit about the
/// gaps that the parity tests don't naturally hit:
///
/// <list type="bullet">
///   <item>Algorithm error paths (no-MS2-with-isolation, profile spectra).</item>
///   <item>Config filesystem errors (missing params file).</item>
///   <item>cpp-bug pins on leaf types where the cpp behavior is documented-but-wrong
///         and we preserve it; these tests fail if a future cleanup pass accidentally
///         "fixes" them and breaks bit parity with cpp.</item>
/// </list>
/// </summary>
[TestClass]
public class DiaUmpireTests
{
    [TestMethod]
    public void Pipeline_HappyPathOnSyntheticSwath()
    {
        var (msd, sl) = BuildTwoWindowSwath(numCycles: 6, ms2PerCycle: 2,
            ms1Peaks: new[] { (mz: 500.0, intensity: 1000.0), (mz: 700.0, intensity: 800.0) });
        var cfg = new Config { DiaTargetWindowScheme = TargetWindowScheme.SwathFixed, DiaFixedWindowSize = 100 };

        var dia = new DiaUmpireProcessor(msd, sl, cfg);

        AssertPseudoMsMsKeysShapeAndIntegrity(dia);
        AssertPseudoMsMsKeysSortedByScanTimeMzCharge(dia);
        AssertSpillFilesCoverEmittedKeys(dia);
    }

    [TestMethod]
    public void Ctor_RejectsInvalidInputs()
    {
        AssertThrowsOnNoMs2WithIsolation();
        AssertThrowsOnProfileSpectra();
    }

    [TestMethod]
    public void Config_FilesystemErrors_Throw()
    {
        Assert.ThrowsException<FileNotFoundException>(() => new Config("doesntexist.params"));
    }

    [TestMethod]
    public void CppBugsPinnedForParity()
    {
        AssertAddPointKeepMaxIfCloseValueExisted_TracksMaxY();
        AssertCalculateMzVar_UsesRtNotMz();
    }

    // ---------------- submethods (called from the [TestMethod]s above) ----------------

    private static void AssertPseudoMsMsKeysShapeAndIntegrity(DiaUmpireProcessor dia)
    {
        Assert.IsNotNull(dia.PseudoMsMsKeys);
        Assert.IsTrue(dia.PseudoMsMsKeys.Count >= 0);
        Assert.IsNotNull(dia.SpillFileByWindow);
        // Every key's spill-file token must be in the per-window map, and the
        // SpillFileIndex must point inside that spill's spectrum list.
        foreach (var key in dia.PseudoMsMsKeys)
        {
            Assert.IsNotNull(key.SpillFileToken);
            Assert.IsTrue(dia.SpillFileByWindow.Values.Contains(key.SpillFileToken!));
            var list = key.SpillFileToken!.Data.Run.SpectrumList;
            Assert.IsNotNull(list);
            Assert.IsTrue(key.SpillFileIndex >= 0 && key.SpillFileIndex < list!.Count);
        }
    }

    private static void AssertPseudoMsMsKeysSortedByScanTimeMzCharge(DiaUmpireProcessor dia)
    {
        for (int i = 1; i < dia.PseudoMsMsKeys.Count; ++i)
        {
            var prev = dia.PseudoMsMsKeys[i - 1];
            var cur = dia.PseudoMsMsKeys[i];
            if (prev.ScanTime != cur.ScanTime) { Assert.IsTrue(prev.ScanTime < cur.ScanTime); continue; }
            if (prev.TargetMz != cur.TargetMz) { Assert.IsTrue(prev.TargetMz < cur.TargetMz); continue; }
            Assert.IsTrue(prev.Charge <= cur.Charge);
        }
    }

    private static void AssertSpillFilesCoverEmittedKeys(DiaUmpireProcessor dia)
    {
        if (dia.PseudoMsMsKeys.Count == 0) return;
        int totalSpectra = 0;
        foreach (var spill in dia.SpillFileByWindow.Values)
            totalSpectra += spill.Data.Run.SpectrumList?.Count ?? 0;
        Assert.IsTrue(totalSpectra >= dia.PseudoMsMsKeys.Count);
    }

    private static void AssertThrowsOnNoMs2WithIsolation()
    {
        // MS1-only — cpp throws "no MS2 spectra with isolation window target m/z".
        var msd = new MSData { Id = "no-ms2" };
        msd.Run.Id = msd.Id;
        var sl = new SpectrumListSimple();
        for (int i = 0; i < 5; ++i)
        {
            var s = new Spectrum { Index = i, Id = $"scan={i + 1}" };
            s.Params.Set(CVID.MS_ms_level, 1);
            s.Params.Set(CVID.MS_centroid_spectrum);
            s.SetMZIntensityArrays(new[] { 400.0, 600.0 }, new[] { 1000.0, 800.0 }, CVID.MS_number_of_detector_counts);
            var scan = new Scan();
            scan.Set(CVID.MS_scan_start_time, i * 30.0, CVID.UO_second);
            s.ScanList.Scans.Add(scan);
            sl.Spectra.Add(s);
        }
        msd.Run.SpectrumList = sl;
        var cfg = new Config { DiaTargetWindowScheme = TargetWindowScheme.SwathFixed, DiaFixedWindowSize = 100 };
        Assert.ThrowsException<System.InvalidOperationException>(() => new DiaUmpireProcessor(msd, sl, cfg));
    }

    private static void AssertThrowsOnProfileSpectra()
    {
        var msd = new MSData { Id = "profile" };
        msd.Run.Id = msd.Id;
        var sl = new SpectrumListSimple();
        var s = new Spectrum { Index = 0, Id = "scan=1" };
        s.Params.Set(CVID.MS_ms_level, 1);
        s.Params.Set(CVID.MS_profile_spectrum);
        s.SetMZIntensityArrays(new[] { 400.0 }, new[] { 1000.0 }, CVID.MS_number_of_detector_counts);
        var scan = new Scan();
        scan.Set(CVID.MS_scan_start_time, 0.0, CVID.UO_second);
        s.ScanList.Scans.Add(scan);
        sl.Spectra.Add(s);
        msd.Run.SpectrumList = sl;

        var cfg = new Config { DiaTargetWindowScheme = TargetWindowScheme.SwathFixed, DiaFixedWindowSize = 100 };
        Assert.ThrowsException<System.InvalidOperationException>(() => new DiaUmpireProcessor(msd, sl, cfg));
    }

    private static void AssertAddPointKeepMaxIfCloseValueExisted_TracksMaxY()
    {
        // cpp XYPointCollection::AddPointKeepMaxIfCloseValueExisted: when a new point is
        // within ppm of an existing one, the cpp code takes the `if (y < pt.getY())` branch
        // (despite the method name) — we preserve that. MaxY does track the running max
        // across all calls regardless. This assertion pins the MaxY contract; the inline
        // xmldoc on the C# port documents the keep-smaller-Y branch quirk.
        var c = new XYPointCollection();
        c.AddPoint(100f, 5f);
        c.AddPointKeepMaxIfCloseValueExisted(100.0001f, 10f, ppm: 10);
        Assert.AreEqual(10f, c.MaxY);
    }

    private static void AssertCalculateMzVar_UsesRtNotMz()
    {
        // cpp PeakCurve::CalculateMzVar accesses PeakList[j].getX() (== RT, not mz) when
        // computing "m/z variance". The comment says m/z, the code uses RT — we preserve
        // that. This test fails if a cleanup pass accidentally "fixes" it; if cpp ever
        // fixes the bug too, update both at once.
        var p = new Config().InstrumentParameters;
        var curve = new PeakCurve(p) { Index = 1, MsLevel = 1 };
        const float apexRt = 5f, apexMz = 500.5f;
        for (int i = 0; i < 21; i++)
        {
            float rt = apexRt - 1f + i * 0.1f;
            float dist = System.Math.Abs(rt - apexRt);
            float intensity = System.Math.Max(0f, 1000f - dist * 1000f);
            if (intensity == 0) intensity = 1;
            curve.AddPeak(new XYZData(rt, apexMz, intensity));
        }

        curve.CalculateMzVar();
        var pts = curve.GetPeakList();
        double sum = 0;
        foreach (var pt in pts) sum += (pt.X - curve.TargetMz) * (pt.X - curve.TargetMz);
        sum /= pts.Count;
        Assert.AreEqual(sum, curve.MzVar, 1.0);
        // NOT zero — that's the cpp-bug signature.
        Assert.IsTrue(curve.MzVar > 100);
    }

    // ----------- synthetic fixture builder (shared with sibling test classes) -----------

    /// <summary>Default small SWATH MSData for sibling tests. 6 cycles, 2 MS2/cycle, 4 MS1 peaks.</summary>
    internal static (MSData Msd, SpectrumListSimple Sl) BuildTinySwathMsd() =>
        BuildTwoWindowSwath(numCycles: 6, ms2PerCycle: 2,
            new[] { (430.0, 50000.0), (460.0, 30000.0), (640.0, 20000.0), (660.0, 15000.0) });

    private static (MSData Msd, SpectrumListSimple Sl) BuildTwoWindowSwath(int numCycles, int ms2PerCycle,
        (double mz, double intensity)[] ms1Peaks)
    {
        var msd = new MSData { Id = "synthetic-swath" };
        msd.Run.Id = msd.Id;
        var sl = new SpectrumListSimple();
        int idx = 0;

        var ms2Windows = new[] { (center: 450.0, half: 50.0), (center: 650.0, half: 50.0) };

        for (int cycle = 0; cycle < numCycles; ++cycle)
        {
            // 1 MS1 per cycle.
            var ms1 = new Spectrum { Index = idx++, Id = $"scan={idx}" };
            ms1.Params.Set(CVID.MS_ms_level, 1);
            ms1.Params.Set(CVID.MS_centroid_spectrum);
            var mzs = new double[ms1Peaks.Length];
            var ints = new double[ms1Peaks.Length];
            for (int i = 0; i < ms1Peaks.Length; ++i)
            {
                mzs[i] = ms1Peaks[i].mz;
                double envelope = System.Math.Exp(-System.Math.Pow(cycle - numCycles / 2.0, 2) / (numCycles * 0.6));
                ints[i] = ms1Peaks[i].intensity * (0.5 + envelope);
            }
            ms1.SetMZIntensityArrays(mzs, ints, CVID.MS_number_of_detector_counts);
            var ms1Scan = new Scan();
            ms1Scan.Set(CVID.MS_scan_start_time, cycle * 1.0, CVID.UO_minute);
            ms1.ScanList.Scans.Add(ms1Scan);
            sl.Spectra.Add(ms1);

            for (int j = 0; j < ms2PerCycle; ++j)
            {
                var win = ms2Windows[j % ms2Windows.Length];
                var s = new Spectrum { Index = idx++, Id = $"scan={idx}" };
                s.Params.Set(CVID.MS_ms_level, 2);
                s.Params.Set(CVID.MS_centroid_spectrum);

                var p = new Precursor();
                p.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, win.center, CVID.MS_m_z);
                p.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, win.half, CVID.MS_m_z);
                p.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, win.half, CVID.MS_m_z);
                p.SelectedIons.Add(new SelectedIon(win.center));
                s.Precursors.Add(p);

                double envelope = System.Math.Exp(-System.Math.Pow(cycle - numCycles / 2.0, 2) / (numCycles * 0.6));
                s.SetMZIntensityArrays(
                    new[] { 220.0, 360.0, 480.0 },
                    new[] { 600.0 * (0.5 + envelope), 800.0 * (0.5 + envelope), 400.0 * (0.5 + envelope) },
                    CVID.MS_number_of_detector_counts);
                var ms2Scan = new Scan();
                ms2Scan.Set(CVID.MS_scan_start_time, cycle * 1.0 + (j + 1) * 0.05, CVID.UO_minute);
                s.ScanList.Scans.Add(ms2Scan);
                sl.Spectra.Add(s);
            }
        }

        msd.Run.SpectrumList = sl;
        return (msd, sl);
    }
}
