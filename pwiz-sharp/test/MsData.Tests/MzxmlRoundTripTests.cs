using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.MzXml;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests.MzXml;

[TestClass]
public class MzxmlRoundTripTests
{
    private static MSData BuildSynthetic()
    {
        var msd = new MSData { Id = "roundtrip_doc" };
        msd.CVs.AddRange(MSData.DefaultCVList);

        msd.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum);
        var sf = new SourceFile("data.raw", "data.raw", "file:///./");
        sf.Set(CVID.MS_Thermo_RAW_format);
        sf.Set(CVID.MS_Thermo_nativeID_format);
        msd.FileDescription.SourceFiles.Add(sf);
        msd.Run.DefaultSourceFile = sf;

        var sw = new Software("pwiz-sharp") { Version = "0.1" };
        sw.Set(CVID.MS_pwiz);
        msd.Software.Add(sw);

        var ic = new InstrumentConfiguration("IC1");
        ic.ComponentList.Add(new Component(CVID.MS_electrospray_ionization, 1));
        ic.ComponentList.Add(new Component(CVID.MS_orbitrap, 2));
        ic.ComponentList.Add(new Component(CVID.MS_inductive_detector, 3));
        ic.Software = sw;
        msd.InstrumentConfigurations.Add(ic);

        var dp = new DataProcessing("dp1");
        var pm = new ProcessingMethod { Order = 0, Software = sw };
        pm.Set(CVID.MS_Conversion_to_mzML);
        dp.ProcessingMethods.Add(pm);
        msd.DataProcessings.Add(dp);

        var list = new SpectrumListSimple { Dp = dp };

        var spec = new Spectrum
        {
            Index = 0,
            Id = "controllerType=0 controllerNumber=1 scan=1",
        };
        spec.Params.Set(CVID.MS_ms_level, 1);
        spec.Params.Set(CVID.MS_MS1_spectrum);
        spec.Params.Set(CVID.MS_positive_scan);
        spec.Params.Set(CVID.MS_centroid_spectrum);
        spec.Params.Set(CVID.MS_total_ion_current, 500);
        spec.Params.Set(CVID.MS_base_peak_m_z, 200.0, CVID.MS_m_z);
        spec.Params.Set(CVID.MS_base_peak_intensity, 100.0, CVID.MS_number_of_detector_counts);
        spec.Params.Set(CVID.MS_lowest_observed_m_z, 100.0, CVID.MS_m_z);
        spec.Params.Set(CVID.MS_highest_observed_m_z, 400.0, CVID.MS_m_z);

        var scan = new Scan { InstrumentConfiguration = ic };
        scan.Set(CVID.MS_scan_start_time, 90.0, CVID.UO_second);
        scan.ScanWindows.Add(new ScanWindow(50.0, 1000.0, CVID.MS_m_z));
        spec.ScanList.Scans.Add(scan);
        spec.ScanList.Set(CVID.MS_no_combination);

        spec.SetMZIntensityArrays(
            new[] { 100.0, 200.0, 300.0, 400.0 },
            new[] { 50.0, 100.0, 150.0, 200.0 },
            CVID.MS_number_of_detector_counts);

        list.Spectra.Add(spec);
        msd.Run.Id = "run1";
        msd.Run.DefaultInstrumentConfiguration = ic;
        msd.Run.SpectrumList = list;

        return msd;
    }

    [TestMethod]
    public void Writer_ProducesWellFormedXml()
    {
        var xml = new MzxmlWriter().Write(BuildSynthetic());
        StringAssert.Contains(xml, "<mzXML", StringComparison.Ordinal);
        StringAssert.Contains(xml, "</mzXML>", StringComparison.Ordinal);
        StringAssert.Contains(xml, "<scan ", StringComparison.Ordinal);
        StringAssert.Contains(xml, "<peaks", StringComparison.Ordinal);
    }

    [TestMethod]
    public void RoundTrip_64BitZlibPreservesPeaks()
    {
        var original = BuildSynthetic();
        var encoderConfig = new BinaryEncoderConfig
        {
            Precision = BinaryPrecision.Bits64,
            Compression = BinaryCompression.Zlib,
        };
        var xml = new MzxmlWriter(encoderConfig).Write(original);

        var reparsed = new MSData();
        using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)))
            MzxmlReader.Read(ms, reparsed);

        Assert.AreEqual(1, reparsed.Run.SpectrumList!.Count);
        var spec = reparsed.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(4, spec.DefaultArrayLength);

        var mz = spec.GetMZArray()!.Data;
        var intensity = spec.GetIntensityArray()!.Data;
        CollectionAssert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0 }, mz);
        CollectionAssert.AreEqual(new[] { 50.0, 100.0, 150.0, 200.0 }, intensity);

        // Spectrum-level attributes round-trip.
        Assert.AreEqual(1, spec.Params.CvParam(CVID.MS_ms_level).ValueAs<int>());
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_centroid_spectrum));
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_positive_scan));
        Assert.AreEqual(500, spec.Params.CvParam(CVID.MS_total_ion_current).ValueAs<int>());

        // Scan-level retention time and window.
        var rtScan = spec.ScanList.Scans[0];
        Assert.AreEqual(90.0, rtScan.CvParam(CVID.MS_scan_start_time).ValueAs<double>(), 1e-6);
        Assert.AreEqual(50.0, rtScan.ScanWindows[0].CvParam(CVID.MS_scan_window_lower_limit).ValueAs<double>(), 1e-6);
        Assert.AreEqual(1000.0, rtScan.ScanWindows[0].CvParam(CVID.MS_scan_window_upper_limit).ValueAs<double>(), 1e-6);
    }

    [TestMethod]
    public void RoundTrip_32BitNoCompressionPreservesPeaks()
    {
        var original = BuildSynthetic();
        var encoderConfig = new BinaryEncoderConfig
        {
            Precision = BinaryPrecision.Bits32,
            Compression = BinaryCompression.None,
        };
        var xml = new MzxmlWriter(encoderConfig).Write(original);

        var reparsed = new MSData();
        using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)))
            MzxmlReader.Read(ms, reparsed);

        var spec = reparsed.Run.SpectrumList!.GetSpectrum(0, getBinaryData: true);
        var mz = spec.GetMZArray()!.Data;
        var intensity = spec.GetIntensityArray()!.Data;
        // 32-bit precision introduces small float-cast errors.
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0 }[i], mz[i], 1e-3);
            Assert.AreEqual(new[] { 50.0, 100.0, 150.0, 200.0 }[i], intensity[i], 1e-3);
        }
    }

    [TestMethod]
    public void RoundTrip_PreservesRunIdFromParentFile()
    {
        var xml = new MzxmlWriter().Write(BuildSynthetic());
        var reparsed = new MSData();
        using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)))
            MzxmlReader.Read(ms, reparsed);

        // FillInMetadata derives run id from "data.raw" → "data".
        Assert.AreEqual("data", reparsed.Id);
        Assert.AreEqual("data", reparsed.Run.Id);

        // Source file got its CV terms re-stamped.
        var sf = reparsed.FileDescription.SourceFiles[0];
        Assert.IsTrue(sf.HasCVParam(CVID.MS_Thermo_RAW_format));
        Assert.IsTrue(sf.HasCVParam(CVID.MS_Thermo_nativeID_format));
    }
}
