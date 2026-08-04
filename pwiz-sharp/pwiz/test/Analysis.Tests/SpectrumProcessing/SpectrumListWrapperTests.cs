using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

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

    // ============================================================================
    //   cpp SpectrumList_SorterTest, ported. It drives the sorter over the tiny example with
    //   two custom predicates (defaultArrayLength and msLevel), in stable and unstable modes
    //   and nested one inside the other, and asserts the original list is left alone.
    // ============================================================================

    private static IComparable ByDefaultArrayLength(ISpectrumList list, int index) =>
        list.GetSpectrum(index, getBinaryData: false).DefaultArrayLength;

    private static IComparable ByMsLevel(ISpectrumList list, int index) =>
        list.GetSpectrum(index, getBinaryData: false).Params.CvParam(CVID.MS_ms_level).ValueAs<int>();

    private static (MSData msd, ISpectrumList list) TinyList()
    {
        var msd = new MSData();
        Examples.InitializeTiny(msd);
        return (msd, msd.Run.SpectrumList!);
    }

    private static string[] IdsOf(ISpectrumList list) =>
        Enumerable.Range(0, list.Count).Select(i => list.SpectrumIdentity(i).Id).ToArray();

    /// <summary>cpp: "assert that the original list is unmodified". Enumerating a sorted view
    /// must not renumber the spectra of the list underneath it.</summary>
    [TestMethod]
    public void Sorter_LeavesOriginalListUnmodified()
    {
        var (_, original) = TinyList();
        var sorted = new SpectrumListSorter(original, ByDefaultArrayLength);
        for (int i = 0; i < sorted.Count; i++) _ = sorted.GetSpectrum(i);

        CollectionAssert.AreEqual(
            new[] { "scan=19", "scan=20", "scan=21", "scan=22", "sample=1 period=1 cycle=23 experiment=1" },
            IdsOf(original), "original ids");
        for (int i = 0; i < original.Count; i++)
        {
            Assert.AreEqual(i, original.SpectrumIdentity(i).Index, $"original identity {i}");
            Assert.AreEqual(i, original.GetSpectrum(i).Index, $"original spectrum {i}");
        }
    }

    /// <summary>cpp's defaultArrayLength scenario: ascending order, renumbered, and the
    /// monotonicity check it closes with.</summary>
    [TestMethod]
    public void Sorter_ByDefaultArrayLength_AscendingAndRenumbered()
    {
        var (_, original) = TinyList();
        var sorted = new SpectrumListSorter(original, ByDefaultArrayLength);
        Assert.AreEqual(original.Count, sorted.Count);

        // The two positions cpp pins by id; the rest it checks only for the ordering property,
        // since spectra with equal lengths are interchangeable under an unstable sort.
        Assert.AreEqual("scan=21", sorted.SpectrumIdentity(0).Id);
        Assert.AreEqual("scan=20", sorted.SpectrumIdentity(1).Id);

        for (int i = 0; i < sorted.Count; i++)
        {
            Assert.AreEqual(i, sorted.SpectrumIdentity(i).Index, $"identity {i} renumbered");
            Assert.AreEqual(i, sorted.GetSpectrum(i).Index, $"spectrum {i} renumbered");
        }
        for (int i = 1; i < sorted.Count; i++)
            Assert.IsTrue(sorted.GetSpectrum(i).DefaultArrayLength >= sorted.GetSpectrum(i - 1).DefaultArrayLength,
                $"defaultArrayLength not ascending at {i}");
    }

    /// <summary>cpp's stable-vs-unstable pair. Only the stable run pins an order, because cpp
    /// notes the equal-msLevel spectra are interchangeable when sorting unstably.</summary>
    [TestMethod]
    public void Sorter_ByMsLevel_StableKeepsOriginalOrderAmongEquals()
    {
        var (_, original) = TinyList();

        var stable = new SpectrumListSorter(original, ByMsLevel, stable: true);
        Assert.AreEqual(original.Count, stable.Count);
        // msLevels are 1,2,1,2,1, so stable sorting gives the three MS1s in their original
        // relative order followed by the two MS2s in theirs. cpp pins the first four positions
        // (its own comment about scan=22 being interchangeable is stale - scan=22 is MS2).
        CollectionAssert.AreEqual(
            new[] { "scan=19", "scan=21", "sample=1 period=1 cycle=23 experiment=1", "scan=20", "scan=22" },
            IdsOf(stable), "stable msLevel order");

        var unstable = new SpectrumListSorter(original, ByMsLevel);
        Assert.AreEqual(original.Count, unstable.Count);
        // Unstable only guarantees the ordering property, not which equal element lands where.
        for (int i = 1; i < unstable.Count; i++)
            Assert.IsTrue(MsLevelOf(unstable, i) >= MsLevelOf(unstable, i - 1),
                $"msLevel not ascending at {i}");
    }

    /// <summary>cpp's "silly (nested) sorted list": a sorter wrapping a sorter.</summary>
    [TestMethod]
    public void Sorter_NestedSorters_ApplyOuterOrdering()
    {
        var (_, original) = TinyList();
        var byMsLevel = new SpectrumListSorter(original, ByMsLevel, stable: true);
        var nested = new SpectrumListSorter(byMsLevel, ByDefaultArrayLength);

        Assert.AreEqual(original.Count, nested.Count);
        for (int i = 0; i < nested.Count; i++)
            Assert.AreEqual(i, nested.SpectrumIdentity(i).Index, $"identity {i} renumbered");
        for (int i = 1; i < nested.Count; i++)
            Assert.IsTrue(nested.GetSpectrum(i).DefaultArrayLength >= nested.GetSpectrum(i - 1).DefaultArrayLength,
                $"defaultArrayLength not ascending at {i}");
    }

    private static int MsLevelOf(ISpectrumList list, int index) =>
        list.GetSpectrum(index, getBinaryData: false).Params.CvParam(CVID.MS_ms_level).ValueAs<int>();

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
