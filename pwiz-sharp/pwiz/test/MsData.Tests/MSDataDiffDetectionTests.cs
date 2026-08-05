using Pwiz.Data.Common;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Diff;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Diff;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Port of the mutation half of cpp's <c>pwiz/data/msdata/DiffTest.cpp</c>: for each level of the
/// MSData model, build two copies that differ in exactly one field and require the diff to say so.
/// </summary>
/// <remarks>
/// Every other use of <see cref="MSDataDiff"/> in this suite asserts the diff comes back EMPTY -
/// the mzML round-trip, DiaUmpire parity, the lockmass refiner, and every vendor reader through
/// VendorReaderTestHarness. That makes the diff an oracle nothing was checking: an implementation
/// that reported "no differences" unconditionally would leave all of those green. cpp spends 1392
/// lines guarding against exactly that; these are the cases the vendor comparisons lean on.
/// </remarks>
[TestClass]
public class MSDataDiffDetectionTests
{
    private delegate void Mutation(MSData msd);

    private static readonly (string What, Mutation Apply)[] Mutations =
    {
        ("spectrum cvParam value", m => Spec(m).Params.Set(CVID.MS_base_peak_m_z, 999.9, CVID.MS_m_z)),
        ("spectrum userParam", m => Spec(m).Params.UserParams.Add(new UserParam("extra", "1"))),
        ("spectrum id", m => Spec(m).Id = "scan=999"),
        ("ms level", m => Spec(m).Params.Set(CVID.MS_ms_level, 7)),
        ("m/z array value", m => Spec(m).GetMZArray()!.Data[1] = 500.5),
        ("intensity array value", m => Spec(m).GetIntensityArray()!.Data[1] = 12345.0),
        ("array length", m => Spec(m).GetMZArray()!.Data.Add(400.0)),
        ("scan start time", m => Spec(m).ScanList.Scans[0].Set(CVID.MS_scan_start_time, 99.0, CVID.UO_second)),
        ("scan window", m => Spec(m).ScanList.Scans[0].ScanWindows[0]
            .Set(CVID.MS_scan_window_lower_limit, 42.0, CVID.MS_m_z)),
        ("precursor selected ion m/z", m => Spec(m).Precursors[0].SelectedIons[0]
            .Set(CVID.MS_selected_ion_m_z, 777.7, CVID.MS_m_z)),
        ("precursor isolation window", m => Spec(m).Precursors[0].IsolationWindow
            .Set(CVID.MS_isolation_window_target_m_z, 777.7, CVID.MS_m_z)),
        ("precursor activation", m => Spec(m).Precursors[0].Activation.Set(CVID.MS_electron_transfer_dissociation)),
        ("spectrum count", m => ((SpectrumListSimple)m.Run.SpectrumList!).Spectra.RemoveAt(1)),
        ("run id", m => m.Run.Id = "different-run"),
        ("file description", m => m.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum)),
    };

    [TestMethod]
    public void Diff_ReportsADifferenceAtEveryLevel()
    {
        var missed = new List<string>();
        foreach (var (what, apply) in Mutations)
        {
            var a = Build();
            var b = Build();
            Assert.AreEqual(string.Empty, MSDataDiff.Describe(a, b),
                $"the two unmutated copies must compare equal before testing '{what}'");

            apply(b);
            if (MSDataDiff.Describe(a, b).Length == 0)
                missed.Add($"  {what}");
        }

        Assert.AreEqual(0, missed.Count,
            $"MSDataDiff reported no difference for {missed.Count} of {Mutations.Length} mutations:" +
            Environment.NewLine + string.Join(Environment.NewLine, missed));
    }

    /// <summary>
    /// cpp's testBinaryDataArray pins <c>DiffConfig.precision</c> as a RELATIVE tolerance: at
    /// precision 1e-4, 1.00001e10 vs 1.00000e10 differ by 1e5 in absolute terms but only 1e-5
    /// relative, so they compare equal - while 1.0002e10 (2e-4 relative) does not. Reading it as
    /// an absolute tolerance would make every large-m/z comparison in the vendor suite meaningless.
    /// </summary>
    [TestMethod]
    public void Precision_IsRelative_NotAbsolute()
    {
        var config = new DiffConfig { Precision = 1e-4 };

        var a = Build();
        var b = Build();
        a.Run.SpectrumList!.GetSpectrum(0, true).GetMZArray()!.Data[0] = 1.00001e10;
        b.Run.SpectrumList!.GetSpectrum(0, true).GetMZArray()!.Data[0] = 1.00000e10;
        Assert.AreEqual(string.Empty, MSDataDiff.Describe(a, b, config),
            "1e-5 relative difference is inside a 1e-4 relative precision");

        var c = Build();
        var d = Build();
        c.Run.SpectrumList!.GetSpectrum(0, true).GetMZArray()!.Data[0] = 1.00001e10;
        d.Run.SpectrumList!.GetSpectrum(0, true).GetMZArray()!.Data[0] = 1.0002e10;
        Assert.AreNotEqual(string.Empty, MSDataDiff.Describe(c, d, config),
            "2e-4 relative difference is outside a 1e-4 relative precision");
    }

    private static Spectrum Spec(MSData msd) => ((SpectrumListSimple)msd.Run.SpectrumList!).Spectra[0];

    private static MSData Build()
    {
        var msd = new MSData { Id = "diff-test" };
        msd.Run.Id = "run1";

        var list = new SpectrumListSimple();
        for (int i = 0; i < 2; i++)
        {
            var s = new Spectrum { Index = i, Id = $"scan={i + 1}" };
            s.Params.Set(CVID.MS_ms_level, 2);
            s.Params.Set(CVID.MS_base_peak_m_z, 200.0, CVID.MS_m_z);
            s.SetMZIntensityArrays(new[] { 100.0, 200.0, 300.0 }, new[] { 10.0, 20.0, 30.0 },
                CVID.MS_number_of_detector_counts);

            var scan = new Scan();
            scan.Set(CVID.MS_scan_start_time, 10.0 * (i + 1), CVID.UO_second);
            scan.ScanWindows.Add(new ScanWindow(50, 1000, CVID.MS_m_z));
            s.ScanList.Scans.Add(scan);

            var precursor = new Precursor();
            precursor.Activation.Set(CVID.MS_collision_induced_dissociation);
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, 200.0, CVID.MS_m_z);
            var ion = new SelectedIon();
            ion.Set(CVID.MS_selected_ion_m_z, 200.0, CVID.MS_m_z);
            precursor.SelectedIons.Add(ion);
            s.Precursors.Add(precursor);

            list.Spectra.Add(s);
        }

        msd.Run.SpectrumList = list;
        return msd;
    }
}
