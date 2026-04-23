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
    public void Write_EmitsBeginEndIonsPerSpectrum()
    {
        string mgf = new MgfSerializer().Write(BuildMsnDoc(2));
        Assert.AreEqual(2, mgf.Split("BEGIN IONS").Length - 1);
        Assert.AreEqual(2, mgf.Split("END IONS").Length - 1);
    }

    [TestMethod]
    public void Write_IncludesExpectedHeaderLines()
    {
        string mgf = new MgfSerializer().Write(BuildMsnDoc(1));
        StringAssert.Contains(mgf, "TITLE=spec-0");
        StringAssert.Contains(mgf, "PEPMASS=500");
        StringAssert.Contains(mgf, "CHARGE=2+");
        StringAssert.Contains(mgf, "RTINSECONDS=1.5");
    }

    [TestMethod]
    public void Write_EmitsMzIntensityPeaks()
    {
        string mgf = new MgfSerializer().Write(BuildMsnDoc(1));
        // Expect "100.5 50" (or close round-trip rendering).
        Assert.IsTrue(mgf.Contains("100.5", StringComparison.Ordinal));
        Assert.IsTrue(mgf.Contains("200.7", StringComparison.Ordinal));
        Assert.IsTrue(mgf.Contains("300.2", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Write_SkipsMs1Spectra()
    {
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
    public void RoundTrip_PreservesBasicFields()
    {
        string mgf = new MgfSerializer().Write(BuildMsnDoc(1));
        var reparsed = new MgfSerializer().Read(mgf);

        Assert.IsNotNull(reparsed.Run.SpectrumList);
        Assert.AreEqual(1, reparsed.Run.SpectrumList.Count);

        var spec = reparsed.Run.SpectrumList.GetSpectrum(0);
        Assert.AreEqual("spec-0", spec.Params.CvParam(CVID.MS_spectrum_title).Value);
        Assert.AreEqual(2, spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0));
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_centroid_spectrum));
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_positive_scan));
    }

    [TestMethod]
    public void RoundTrip_PreservesPrecursor()
    {
        string mgf = new MgfSerializer().Write(BuildMsnDoc(1));
        var reparsed = new MgfSerializer().Read(mgf);

        var spec = reparsed.Run.SpectrumList!.GetSpectrum(0);
        var si = spec.Precursors[0].SelectedIons[0];
        Assert.AreEqual(500.0, si.CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>(), 1e-6);
        Assert.AreEqual(2, si.CvParam(CVID.MS_charge_state).ValueAs<int>());
    }

    [TestMethod]
    public void RoundTrip_PreservesPeaks()
    {
        string mgf = new MgfSerializer().Write(BuildMsnDoc(1));
        var reparsed = new MgfSerializer().Read(mgf);

        var spec = reparsed.Run.SpectrumList!.GetSpectrum(0);
        var mz = spec.GetMZArray()!.Data;
        var intensity = spec.GetIntensityArray()!.Data;
        CollectionAssert.AreEqual(new[] { 100.5, 200.7, 300.2 }, mz);
        CollectionAssert.AreEqual(new[] { 50.0, 200.0, 80.0 }, intensity);
    }

    [TestMethod]
    public void RoundTrip_PreservesRetentionTime()
    {
        string mgf = new MgfSerializer().Write(BuildMsnDoc(1));
        var reparsed = new MgfSerializer().Read(mgf);

        var scan = reparsed.Run.SpectrumList!.GetSpectrum(0).ScanList.Scans[0];
        var rt = scan.CvParam(CVID.MS_scan_start_time);
        Assert.AreEqual(1.5, rt.ValueAs<double>(), 1e-9);
        Assert.AreEqual(CVID.UO_second, rt.Units);
    }

    [TestMethod]
    public void Read_ComputesDerivedStatistics()
    {
        // Base peak / TIC / min/max m/z come from the peak list, not the header.
        string mgf = new MgfSerializer().Write(BuildMsnDoc(1));
        var reparsed = new MgfSerializer().Read(mgf);
        var spec = reparsed.Run.SpectrumList!.GetSpectrum(0);

        Assert.AreEqual(100.5, spec.Params.CvParam(CVID.MS_lowest_observed_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(300.2, spec.Params.CvParam(CVID.MS_highest_observed_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(330.0, spec.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(), 1e-9);
        Assert.AreEqual(200.0, spec.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>(), 1e-9);
        Assert.AreEqual(200.7, spec.Params.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>(), 1e-9);
    }

    [TestMethod]
    public void Read_HandwrittenInput_ParsesCorrectly()
    {
        const string mgf = """
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
        var msd = new MgfSerializer().Read(mgf);
        Assert.AreEqual(1, msd.Run.SpectrumList!.Count);
        var spec = msd.Run.SpectrumList.GetSpectrum(0);
        Assert.AreEqual("hand-written spectrum", spec.Params.CvParam(CVID.MS_spectrum_title).Value);
        Assert.AreEqual(42.5, spec.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time).ValueAs<double>(), 1e-9);
    }

    [TestMethod]
    public void Read_NegativeCharge_SetsNegativePolarity()
    {
        const string mgf = "BEGIN IONS\nTITLE=neg\nPEPMASS=500\nCHARGE=1-\nEND IONS\n";
        var msd = new MgfSerializer().Read(mgf);
        var spec = msd.Run.SpectrumList!.GetSpectrum(0);
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_negative_scan));
    }

    [TestMethod]
    public void Read_DefaultFileContent_IsMsnCentroided()
    {
        var msd = new MgfSerializer().Read("BEGIN IONS\nTITLE=x\nPEPMASS=500\nEND IONS\n");
        Assert.IsTrue(msd.FileDescription.FileContent.HasCVParam(CVID.MS_MSn_spectrum));
        Assert.IsTrue(msd.FileDescription.FileContent.HasCVParam(CVID.MS_centroid_spectrum));
    }
}
