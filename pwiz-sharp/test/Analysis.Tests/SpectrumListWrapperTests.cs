using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests;

/// <summary>
/// Tests for the wrapper-style filters in Tier 1: <see cref="SpectrumListMzWindow"/>,
/// <see cref="SpectrumListMzShift"/>, <see cref="SpectrumListSorter"/>,
/// <see cref="SpectrumListTitleMaker"/>. These don't fit the cpp-style "one builder + many
/// predicate methods" pattern (they don't go through <see cref="SpectrumListFilter"/>), so
/// each gets its own focused test method here with a small inline list.
/// </summary>
[TestClass]
public class SpectrumListWrapperTests
{
    private static Spectrum MakeSpectrum(int index, int msLevel, double scanTimeSec, double[] mz, double[] intensity)
    {
        var s = new Spectrum { Index = index, Id = $"scan={index + 1}", DefaultArrayLength = mz.Length };
        s.Params.Set(CVID.MS_ms_level, msLevel);
        s.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
        var scan = new Scan();
        scan.Set(CVID.MS_scan_start_time, scanTimeSec, CVID.UO_second);
        s.ScanList.Scans.Add(scan);
        return s;
    }

    [TestMethod]
    public void MzWindow_DropsPeaksOutsideRange()
    {
        var inner = new SpectrumListSimple();
        inner.Spectra.Add(MakeSpectrum(0, 1, 10,
            mz: new[] { 50.0, 100.0, 150.0, 200.0, 250.0 },
            intensity: new[] { 1.0, 2.0, 3.0, 4.0, 5.0 }));
        var wrapped = new SpectrumListMzWindow(inner, 100, 200);
        var spec = wrapped.GetSpectrum(0, getBinaryData: true);
        CollectionAssert.AreEqual(new[] { 100.0, 150.0, 200.0 }, spec.GetMZArray()!.Data);
        CollectionAssert.AreEqual(new[] { 2.0, 3.0, 4.0 }, spec.GetIntensityArray()!.Data);
        Assert.AreEqual(3, spec.DefaultArrayLength);
    }

    [TestMethod]
    public void MzShift_AbsoluteShiftAffectsMzAndScanWindow()
    {
        var inner = new SpectrumListSimple();
        var s = MakeSpectrum(0, 1, 10, new[] { 100.0, 200.0 }, new[] { 1.0, 2.0 });
        s.ScanList.Scans[0].ScanWindows.Add(new ScanWindow(50, 1000, CVID.MS_m_z));
        s.Params.Set(CVID.MS_base_peak_m_z, 200.0, CVID.MS_m_z);
        inner.Spectra.Add(s);

        var wrapped = new SpectrumListMzShift(inner, new MZTolerance(1, MZToleranceUnits.Mz));
        var shifted = wrapped.GetSpectrum(0, getBinaryData: true);
        // m/z array shifted by +1.
        CollectionAssert.AreEqual(new[] { 101.0, 201.0 }, shifted.GetMZArray()!.Data);
        // Scan-window endpoints shifted.
        var window = shifted.ScanList.Scans[0].ScanWindows[0];
        Assert.AreEqual(51.0, window.CvParam(CVID.MS_scan_window_lower_limit).ValueAs<double>(), 1e-9);
        Assert.AreEqual(1001.0, window.CvParam(CVID.MS_scan_window_upper_limit).ValueAs<double>(), 1e-9);
        // Base peak m/z shifted.
        Assert.AreEqual(201.0, shifted.Params.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>(), 1e-9);
    }

    [TestMethod]
    public void MzShift_PpmShiftScalesWithValue()
    {
        // 10 ppm shift on a 1000 m/z peak = +0.01 m/z.
        var inner = new SpectrumListSimple();
        inner.Spectra.Add(MakeSpectrum(0, 1, 10, new[] { 1000.0 }, new[] { 1.0 }));
        var wrapped = new SpectrumListMzShift(inner, new MZTolerance(10, MZToleranceUnits.Ppm));
        var shifted = wrapped.GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(1000.0 + 0.01, shifted.GetMZArray()!.Data[0], 1e-9);
    }

    [TestMethod]
    public void Sorter_OrdersByScanStartTime()
    {
        // Inner is in time-descending order; sorter should re-emit ascending.
        var inner = new SpectrumListSimple();
        inner.Spectra.Add(MakeSpectrum(0, 1, scanTimeSec: 30, new[] { 100.0 }, new[] { 1.0 }));
        inner.Spectra.Add(MakeSpectrum(1, 1, scanTimeSec: 10, new[] { 100.0 }, new[] { 1.0 }));
        inner.Spectra.Add(MakeSpectrum(2, 1, scanTimeSec: 20, new[] { 100.0 }, new[] { 1.0 }));

        var sorted = new SpectrumListSorter(inner, SpectrumListSorter.ByScanStartTimeKey);
        // Sorted order: original indices 1 (10s), 2 (20s), 0 (30s).
        Assert.AreEqual("scan=2", sorted.SpectrumIdentity(0).Id);
        Assert.AreEqual("scan=3", sorted.SpectrumIdentity(1).Id);
        Assert.AreEqual("scan=1", sorted.SpectrumIdentity(2).Id);
        // Visible Index is the new position (0..2), not the original.
        Assert.AreEqual(0, sorted.SpectrumIdentity(0).Index);
        Assert.AreEqual(2, sorted.SpectrumIdentity(2).Index);
    }

    [TestMethod]
    public void TitleMaker_SubstitutesPlaceholdersFromMsdAndSpectrum()
    {
        var msd = new MSData { Id = "myrun" };
        msd.Run.Id = "myrun";
        msd.FileDescription.SourceFiles.Add(new SourceFile("sf", "data.raw", "file:///c:/data.raw"));
        var inner = new SpectrumListSimple();
        var s = MakeSpectrum(0, 2, scanTimeSec: 60, new[] { 100.0 }, new[] { 1.0 });
        var precursor = new Precursor(500.0, 2);
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, 500.0, CVID.MS_m_z);
        s.Precursors.Add(precursor);
        inner.Spectra.Add(s);

        var wrapped = new SpectrumListTitleMaker(msd, inner,
            "<RunId>.<ScanNumber>.<ScanNumber>.<ChargeState>");
        var got = wrapped.GetSpectrum(0).Params.CvParam(CVID.MS_spectrum_title);
        Assert.AreEqual("myrun.1.1.2", got.Value);

        // Time placeholders: 60 s = 1 minute.
        var withTime = new SpectrumListTitleMaker(msd, inner,
            "<MsLevel>:<ScanStartTimeInMinutes>");
        Assert.AreEqual("2:1", withTime.GetSpectrum(0).Params.CvParam(CVID.MS_spectrum_title).Value);
    }
}
