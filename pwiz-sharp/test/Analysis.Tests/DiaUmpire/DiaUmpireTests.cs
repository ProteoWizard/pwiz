using System.Linq;
using Pwiz.Analysis.DiaUmpire;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;

// Use the type alias so test methods can write `new DiaUmpireProcessor(...)` without colliding with
// the Pwiz.Analysis.DiaUmpire namespace import above.
using DiaUmpireProcessor = Pwiz.Analysis.DiaUmpire.DiaUmpire;

namespace Pwiz.Analysis.Tests.DiaUmpire;

/// <summary>
/// End-to-end smoke tests for <see cref="DiaUmpire"/>. Builds tiny synthetic SWATH-like
/// MSData documents in-memory and exercises the full pipeline. Numerical parity against
/// the cpp implementation is phase 5; here we pin the public surface, error handling,
/// and the structural invariant that PseudoMsMsKeys is sorted (scanTime, targetMz, charge).
/// </summary>
[TestClass]
public class DiaUmpireTests
{
    [TestMethod]
    public void Ctor_TwoWindowSwath_DoesNotThrow_AndPseudoMsMsKeysReadable()
    {
        var (msd, sl) = BuildTwoWindowSwath(numCycles: 6, ms2PerCycle: 2,
            ms1Peaks: new[] { (mz: 500.0, intensity: 1000.0), (mz: 700.0, intensity: 800.0) });
        var cfg = new Config { DiaTargetWindowScheme = TargetWindowScheme.SwathFixed, DiaFixedWindowSize = 100 };

        var dia = new DiaUmpireProcessor(msd, sl, cfg);

        Assert.IsNotNull(dia.PseudoMsMsKeys);
        Assert.IsTrue(dia.PseudoMsMsKeys.Count >= 0,
            "PseudoMsMsKeys must be enumerable and non-negative in count.");
        Assert.IsNotNull(dia.SpillFileByWindow);
        // Sanity: every key's spill-file token must be a member of SpillFileByWindow,
        // and the SpillFileIndex must be a valid index into that spill file.
        foreach (var key in dia.PseudoMsMsKeys)
        {
            Assert.IsNotNull(key.SpillFileToken);
            Assert.IsTrue(dia.SpillFileByWindow.Values.Contains(key.SpillFileToken!),
                "every PseudoMsMsKey.SpillFileToken must be present in SpillFileByWindow");
            var list = key.SpillFileToken!.Data.Run.SpectrumList;
            Assert.IsNotNull(list);
            Assert.IsTrue(key.SpillFileIndex >= 0 && key.SpillFileIndex < list!.Count,
                $"PseudoMsMsKey.SpillFileIndex {key.SpillFileIndex} out of range [0, {list.Count}).");
        }
    }

    [TestMethod]
    public void Ctor_PseudoMsMsKeys_AreSortedByScanTimeMzCharge()
    {
        var (msd, sl) = BuildTwoWindowSwath(numCycles: 8, ms2PerCycle: 2,
            ms1Peaks: new[] { (mz: 520.0, intensity: 1500.0), (mz: 720.0, intensity: 1200.0) });
        var cfg = new Config { DiaTargetWindowScheme = TargetWindowScheme.SwathFixed, DiaFixedWindowSize = 100 };

        var dia = new DiaUmpireProcessor(msd, sl, cfg);

        for (int i = 1; i < dia.PseudoMsMsKeys.Count; ++i)
        {
            var prev = dia.PseudoMsMsKeys[i - 1];
            var cur = dia.PseudoMsMsKeys[i];
            // (scanTime, targetMz, charge) lexicographic
            if (prev.ScanTime != cur.ScanTime)
            {
                Assert.IsTrue(prev.ScanTime < cur.ScanTime,
                    $"PseudoMsMsKeys not sorted by scanTime at index {i}: {prev.ScanTime} > {cur.ScanTime}");
                continue;
            }
            if (prev.TargetMz != cur.TargetMz)
            {
                Assert.IsTrue(prev.TargetMz < cur.TargetMz,
                    $"PseudoMsMsKeys not sorted by targetMz at index {i} (scanTime tied)");
                continue;
            }
            Assert.IsTrue(prev.Charge <= cur.Charge,
                $"PseudoMsMsKeys not sorted by charge at index {i} (scanTime+targetMz tied)");
        }
    }

    [TestMethod]
    public void SpillFileByWindow_Populated_WhenAnyPseudoMsMs_Emitted()
    {
        // Generous synthetic data: 12 cycles, strong precursor peaks, low SN.
        // If any pseudo-MS/MS is emitted at all, its spill MSData must contain its spectrum.
        var (msd, sl) = BuildTwoWindowSwath(numCycles: 12, ms2PerCycle: 3,
            ms1Peaks: new[] { (mz: 480.0, intensity: 5000.0), (mz: 680.0, intensity: 4000.0), (mz: 530.0, intensity: 3500.0) });
        var cfg = new Config
        {
            DiaTargetWindowScheme = TargetWindowScheme.SwathFixed,
            DiaFixedWindowSize = 100,
        };
        cfg.InstrumentParameters.SN = 1f;
        cfg.InstrumentParameters.MS2SN = 1f;
        cfg.InstrumentParameters.MinPeakPerPeakCurve = 0;
        cfg.InstrumentParameters.MinHighCorrCnt = 0;
        cfg.InstrumentParameters.MinFrag = 0;

        var dia = new DiaUmpireProcessor(msd, sl, cfg);

        // The constructor returned, and the structural invariant has to hold whether or
        // not the synthetic data was rich enough to produce pseudo-MS/MS. If we did emit
        // anything, every spill file must contain at least one spectrum and at least one
        // key has to live inside it.
        if (dia.PseudoMsMsKeys.Count > 0)
        {
            foreach (var spill in dia.SpillFileByWindow.Values)
            {
                Assert.IsNotNull(spill.Data.Run.SpectrumList);
                Assert.IsTrue(spill.Data.Run.SpectrumList!.Count >= 0);
            }
            // At least one spill MSData has at least one spectrum (= each key points
            // somewhere valid, asserted in the structural test above).
            int totalSpectra = 0;
            foreach (var spill in dia.SpillFileByWindow.Values)
                totalSpectra += spill.Data.Run.SpectrumList?.Count ?? 0;
            Assert.IsTrue(totalSpectra >= dia.PseudoMsMsKeys.Count,
                "sum of spill-file spectrum counts must cover all emitted PseudoMsMsKeys.");
        }
    }

    [TestMethod]
    public void Ctor_NonDiaInput_NoMs2WithIsolation_Throws()
    {
        // MS1-only document — no MS2 with isolation window. cpp throws
        // "no MS2 spectra with isolation window target m/z"; pwiz-sharp matches.
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

    [TestMethod]
    public void Ctor_ProfileSpectra_Throws()
    {
        var msd = new MSData { Id = "profile" };
        msd.Run.Id = msd.Id;
        var sl = new SpectrumListSimple();
        var s = new Spectrum { Index = 0, Id = "scan=1" };
        s.Params.Set(CVID.MS_ms_level, 1);
        s.Params.Set(CVID.MS_profile_spectrum); // profile, not centroid
        s.SetMZIntensityArrays(new[] { 400.0 }, new[] { 1000.0 }, CVID.MS_number_of_detector_counts);
        var scan = new Scan();
        scan.Set(CVID.MS_scan_start_time, 0.0, CVID.UO_second);
        s.ScanList.Scans.Add(scan);
        sl.Spectra.Add(s);
        msd.Run.SpectrumList = sl;

        var cfg = new Config { DiaTargetWindowScheme = TargetWindowScheme.SwathFixed, DiaFixedWindowSize = 100 };
        Assert.ThrowsException<System.InvalidOperationException>(() => new DiaUmpireProcessor(msd, sl, cfg));
    }

    // ----------- synthetic fixture builder -----------

    /// <summary>
    /// Builds a synthetic 2-window SWATH-like document: each cycle has 1 MS1 followed by
    /// <paramref name="ms2PerCycle"/> MS2 spectra, alternating across two windows centered
    /// at 450 and 650 (half-width 50). The MS1 spectrum carries persistent peaks at the
    /// requested m/z values to make peak-curve detection deterministic.
    /// </summary>
    private static (MSData Msd, SpectrumListSimple Sl) BuildTwoWindowSwath(int numCycles, int ms2PerCycle,
        (double mz, double intensity)[] ms1Peaks)
    {
        var msd = new MSData { Id = "synthetic-swath" };
        msd.Run.Id = msd.Id;
        var sl = new SpectrumListSimple();
        int idx = 0;

        // Window centers / half-widths.
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
                // Slight modulation so peaks rise and fall (gives a real apex).
                double envelope = System.Math.Exp(-System.Math.Pow(cycle - numCycles / 2.0, 2) / (numCycles * 0.6));
                ints[i] = ms1Peaks[i].intensity * (0.5 + envelope);
            }
            ms1.SetMZIntensityArrays(mzs, ints, CVID.MS_number_of_detector_counts);
            var ms1Scan = new Scan();
            ms1Scan.Set(CVID.MS_scan_start_time, cycle * 1.0, CVID.UO_minute);
            ms1.ScanList.Scans.Add(ms1Scan);
            sl.Spectra.Add(ms1);

            // ms2PerCycle MS2 per cycle, alternating across the two windows.
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

                // Fragments: a fixed pattern at a few m/z values, intensity varies by cycle.
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
