using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mgf;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests.Mgf;

[TestClass]
public class MgfRoundTripTests
{
    private static MSData BuildMsnDoc(int numSpectra = 2)
    {
        var msd = new MSData();
        var list = new SpectrumListSimple();
        for (int i = 0; i < numSpectra; i++)
        {
            var spec = new Spectrum
            {
                Index = i,
                Id = $"controllerType=0 controllerNumber=1 scan={i + 1}",
                DefaultArrayLength = 3,
            };
            spec.Params.Set(CVID.MS_ms_level, 2);
            spec.Params.Set(CVID.MS_MSn_spectrum);
            spec.Params.Set(CVID.MS_positive_scan);
            spec.Params.Set(CVID.MS_spectrum_title, $"spec-{i}");

            var scan = new Scan();
            scan.Set(CVID.MS_scan_start_time, 1.5 + i, CVID.UO_second);
            spec.ScanList.Scans.Add(scan);

            var si = new SelectedIon();
            si.Set(CVID.MS_selected_ion_m_z, 500.0 + i, CVID.MS_m_z);
            si.Set(CVID.MS_peak_intensity, 10000.0, CVID.MS_number_of_detector_counts);
            si.Set(CVID.MS_charge_state, 2);
            var precursor = new Precursor();
            precursor.SelectedIons.Add(si);
            spec.Precursors.Add(precursor);

            spec.SetMZIntensityArrays(
                new[] { 100.5, 200.7, 300.2 },
                new[] { 50.0, 200.0, 80.0 },
                CVID.MS_number_of_detector_counts);

            list.Spectra.Add(spec);
        }
        msd.Run.SpectrumList = list;
        return msd;
    }

    [TestMethod]
    public void Write_ProducesExpectedShape()
    {
        // Two spectra → two BEGIN/END IONS pairs; per-spectrum header lines + peak list emitted.
        string mgf = new MgfSerializer().Write(BuildMsnDoc(2));
        Assert.AreEqual(2, mgf.Split("BEGIN IONS").Length - 1, "BEGIN IONS count");
        Assert.AreEqual(2, mgf.Split("END IONS").Length - 1, "END IONS count");

        // Header tags and peak m/z values appear in the output.
        StringAssert.Contains(mgf, "TITLE=spec-0");
        StringAssert.Contains(mgf, "PEPMASS=500");
        StringAssert.Contains(mgf, "CHARGE=2+");
        StringAssert.Contains(mgf, "RTINSECONDS=1.5");
        Assert.IsTrue(mgf.Contains("100.5", StringComparison.Ordinal), "peak m/z 100.5");
        Assert.IsTrue(mgf.Contains("200.7", StringComparison.Ordinal), "peak m/z 200.7");
        Assert.IsTrue(mgf.Contains("300.2", StringComparison.Ordinal), "peak m/z 300.2");
    }

    [TestMethod]
    public void Write_SkipsMs1Spectra()
    {
        // MGF can only carry MSn peak lists; an MS1-only document produces no BEGIN IONS at all.
        var msd = new MSData();
        var list = new SpectrumListSimple();
        var ms1 = new Spectrum { Index = 0, Id = "ms1" };
        ms1.Params.Set(CVID.MS_ms_level, 1);
        list.Spectra.Add(ms1);
        msd.Run.SpectrumList = list;

        string mgf = new MgfSerializer().Write(msd);
        Assert.IsFalse(mgf.Contains("BEGIN IONS", StringComparison.Ordinal),
            "MS1 spectra should not be written in MGF");
    }

    [TestMethod]
    public void RoundTrip_PreservesFields_AndComputesDerivedStatistics()
    {
        // One round-trip exercises basic fields, precursor info, peaks, RT, and derived stats.
        string mgf = new MgfSerializer().Write(BuildMsnDoc(1));
        var reparsed = new MgfSerializer().Read(mgf);

        Assert.IsNotNull(reparsed.Run.SpectrumList);
        Assert.AreEqual(1, reparsed.Run.SpectrumList.Count);

        var spec = reparsed.Run.SpectrumList.GetSpectrum(0);
        Assert.AreEqual("spec-0", spec.Params.CvParam(CVID.MS_spectrum_title).Value, "title");
        Assert.AreEqual(2, spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0), "ms level");
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_centroid_spectrum), "centroid flag");
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_positive_scan), "positive polarity");

        // Precursor.
        var si = spec.Precursors[0].SelectedIons[0];
        Assert.AreEqual(500.0, si.CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>(), 1e-6, "precursor m/z");
        Assert.AreEqual(2, si.CvParam(CVID.MS_charge_state).ValueAs<int>(), "precursor charge");

        // Peaks.
        CollectionAssert.AreEqual(new[] { 100.5, 200.7, 300.2 }, spec.GetMZArray()!.Data, "m/z array");
        CollectionAssert.AreEqual(new[] { 50.0, 200.0, 80.0 }, spec.GetIntensityArray()!.Data, "intensity array");

        // Retention time + units.
        var rt = spec.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time);
        Assert.AreEqual(1.5, rt.ValueAs<double>(), 1e-9, "RT value");
        Assert.AreEqual(CVID.UO_second, rt.Units, "RT units");

        // Base peak / TIC / min/max m/z come from the peak list, not header lines.
        Assert.AreEqual(100.5, spec.Params.CvParam(CVID.MS_lowest_observed_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(300.2, spec.Params.CvParam(CVID.MS_highest_observed_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(330.0, spec.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(), 1e-9);
        Assert.AreEqual(200.0, spec.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>(), 1e-9);
        Assert.AreEqual(200.7, spec.Params.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>(), 1e-9);
    }

    [TestMethod]
    public void Read_HandwrittenInput_AndSpecialCases()
    {
        // Comments and blank lines are skipped; pepmass with intensity is parsed; defaults are applied.
        const string handwritten = """
            # a comment to be ignored

            BEGIN IONS
            TITLE=hand-written spectrum
            RTINSECONDS=42.5
            PEPMASS=805.4 12345
            CHARGE=2+
            100.1 500
            200.2 1000
            END IONS
            """;
        var msd = new MgfSerializer().Read(handwritten);
        Assert.AreEqual(1, msd.Run.SpectrumList!.Count);
        var spec = msd.Run.SpectrumList.GetSpectrum(0);
        Assert.AreEqual("hand-written spectrum", spec.Params.CvParam(CVID.MS_spectrum_title).Value);
        Assert.AreEqual(42.5, spec.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time).ValueAs<double>(), 1e-9);

        // Negative charge → MS_negative_scan polarity.
        var neg = new MgfSerializer().Read("BEGIN IONS\nTITLE=neg\nPEPMASS=500\nCHARGE=1-\nEND IONS\n");
        Assert.IsTrue(neg.Run.SpectrumList!.GetSpectrum(0).Params.HasCVParam(CVID.MS_negative_scan));

        // Default fileContent reflects MGF semantics: MSn + centroid.
        var defaults = new MgfSerializer().Read("BEGIN IONS\nTITLE=x\nPEPMASS=500\nEND IONS\n");
        Assert.IsTrue(defaults.FileDescription.FileContent.HasCVParam(CVID.MS_MSn_spectrum));
        Assert.IsTrue(defaults.FileDescription.FileContent.HasCVParam(CVID.MS_centroid_spectrum));
    }
}
