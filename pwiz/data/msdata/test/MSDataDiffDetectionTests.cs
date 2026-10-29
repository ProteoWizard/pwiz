using Pwiz.Data.Common;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Diff;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Diff;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Samples;
using Pwiz.Data.MsData.Sources;
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
        // cpp testSpectrum: index, defaultArrayLength, sourceFile and dataProcessing are all
        // fields of the spectrum it expects to see reported.
        ("spectrum index", m => Spec(m).Index = 4),
        ("defaultArrayLength", m => Spec(m).DefaultArrayLength = 22),
        ("spectrum source file", m => Spec(m).SourceFile = new SourceFile("sf", "test.raw", "file:///test.raw")),
        ("spectrum data processing", m => Spec(m).DataProcessing = new DataProcessing("msdata 2")),
        ("product isolation window", m =>
        {
            var product = new Product();
            product.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, 420.0, CVID.MS_m_z);
            Spec(m).Products.Add(product);
        }),
        ("binary data array count", m => Spec(m).BinaryDataArrays.RemoveAt(1)),
        // cpp testFileDescription / testSample / testSoftware, at the document level.
        ("contact", m => m.FileDescription.Contacts[0].Set(CVID.MS_contact_name, "Isabelle Lynn")),
        ("source file location", m => m.FileDescription.SourceFiles[0].Location = "location2"),
        ("sample", m => m.Samples[0].Set(CVID.MS_peak_intensity, 1.0)),
        ("software version", m => m.Software[0].Version = "4.21"),
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

    /// <summary>
    /// The other half of cpp's DiffTest, and the half a detection-only test cannot see: the cases
    /// where the diff must stay quiet. A comparison that called everything different would satisfy
    /// every assertion above and still be useless.
    /// </summary>
    [TestMethod]
    public void Diff_StaysQuietWhereCppDoes()
    {
        // cpp testSpectrum / testChromatogram: at precision 1e-6, a 1e-12 perturbation is noise.
        var a = Build();
        var b = Build();
        Spec(a).GetMZArray()!.Data[0] = 420;
        Spec(b).GetMZArray()!.Data[0] = 420 + 1e-12;
        Assert.AreEqual(string.Empty, MSDataDiff.Describe(a, b, new DiffConfig { Precision = 1e-6 }),
            "a 1e-12 difference is inside a 1e-6 precision");

        Spec(b).GetMZArray()!.Data[0] += 1e-3;
        Assert.AreNotEqual(string.Empty, MSDataDiff.Describe(a, b, new DiffConfig { Precision = 1e-6 }),
            "a 1e-3 difference is not");

        // cpp testFileDescription: the same two contacts in the other order are the same contacts.
        var orderedA = Build();
        var orderedB = Build();
        AddContact(orderedA, "Darren");
        AddContact(orderedA, "Laura Jane");
        AddContact(orderedB, "Laura Jane");
        AddContact(orderedB, "Darren");
        Assert.AreEqual(string.Empty, MSDataDiff.Describe(orderedA, orderedB),
            "contacts compare as a set, not a sequence");

        // cpp's testScanList swaps two scans and expects no diff, so cpp compares scans as a set
        // too. The port compares them position by position and reports the swap. That is the safe
        // direction for an oracle - it can raise a difference that does not matter, but it cannot
        // hide one that does - so the divergence is pinned here rather than papered over. Note
        // cpp's fixture reaches its conclusion partly by accident: it populates a1 twice and
        // leaves a2 empty, so the swap it checks is between a populated scan and an empty one.
        var scansA = Build();
        var scansB = Build();
        AddScan(Spec(scansA), "booger", 4.20);
        AddScan(Spec(scansA), "goober", 6.66);
        AddScan(Spec(scansB), "goober", 6.66);
        AddScan(Spec(scansB), "booger", 4.20);
        Assert.AreNotEqual(string.Empty, MSDataDiff.Describe(scansA, scansB),
            "the port compares scans positionally where cpp compares them as a set");
    }

    private static void AddContact(MSData msd, string name)
    {
        var contact = new Contact();
        contact.Set(CVID.MS_contact_name, name);
        msd.FileDescription.Contacts.Add(contact);
    }

    private static void AddScan(Spectrum s, string filterString, double startTimeMinutes)
    {
        var scan = new Scan();
        scan.Set(CVID.MS_filter_string, filterString);
        scan.Set(CVID.MS_scan_start_time, startTimeMinutes, CVID.UO_minute);
        s.ScanList.Scans.Add(scan);
    }

    private static Spectrum Spec(MSData msd) => ((SpectrumListSimple)msd.Run.SpectrumList!).Spectra[0];

    private static MSData Build()
    {
        var msd = new MSData { Id = "diff-test" };
        msd.Run.Id = "run1";

        var contact = new Contact();
        contact.Set(CVID.MS_contact_name, "Emma Lee");
        msd.FileDescription.Contacts.Add(contact);
        msd.FileDescription.SourceFiles.Add(new SourceFile("id1", "name1", "location1"));
        msd.Samples.Add(new Sample("id1", "name1"));
        msd.Software.Add(new Software("msdata", new CVParam(CVID.MS_ionization_type), "4.20"));

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
