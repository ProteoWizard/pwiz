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

    private static MSData ReparseMzxml(string xml)
    {
        var reparsed = new MSData();
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        MzxmlReader.Read(ms, reparsed);
        return reparsed;
    }

    [TestMethod]
    public void Writer_ProducesWellFormedMzxml()
    {
        var xml = new MzxmlWriter().Write(BuildSynthetic());
        StringAssert.Contains(xml, "<mzXML", StringComparison.Ordinal);
        StringAssert.Contains(xml, "</mzXML>", StringComparison.Ordinal);
        StringAssert.Contains(xml, "<scan ", StringComparison.Ordinal);
        StringAssert.Contains(xml, "<peaks", StringComparison.Ordinal);
    }

    [TestMethod]
    public void RoundTrip_PrecisionAndCompressionVariants()
    {
        var original = BuildSynthetic();

        // 64-bit + zlib: lossless peaks + spectrum-level params + scan-level RT/window all preserved.
        var zlib64 = new MzxmlWriter(new BinaryEncoderConfig
        {
            Precision = BinaryPrecision.Bits64,
            Compression = BinaryCompression.Zlib,
        }).Write(original);
        var reparsedZlib = ReparseMzxml(zlib64);
        Assert.AreEqual(1, reparsedZlib.Run.SpectrumList!.Count);

        var spec = reparsedZlib.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(4, spec.DefaultArrayLength);
        CollectionAssert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0 }, spec.GetMZArray()!.Data);
        CollectionAssert.AreEqual(new[] { 50.0, 100.0, 150.0, 200.0 }, spec.GetIntensityArray()!.Data);

        Assert.AreEqual(1, spec.Params.CvParam(CVID.MS_ms_level).ValueAs<int>());
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_centroid_spectrum));
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_positive_scan));
        Assert.AreEqual(500, spec.Params.CvParam(CVID.MS_total_ion_current).ValueAs<int>());

        var scan = spec.ScanList.Scans[0];
        Assert.AreEqual(90.0, scan.CvParam(CVID.MS_scan_start_time).ValueAs<double>(), 1e-6);
        Assert.AreEqual(50.0, scan.ScanWindows[0].CvParam(CVID.MS_scan_window_lower_limit).ValueAs<double>(), 1e-6);
        Assert.AreEqual(1000.0, scan.ScanWindows[0].CvParam(CVID.MS_scan_window_upper_limit).ValueAs<double>(), 1e-6);

        // 32-bit, no compression: lossy at ~1e-3 due to float cast.
        var bits32 = new MzxmlWriter(new BinaryEncoderConfig
        {
            Precision = BinaryPrecision.Bits32,
            Compression = BinaryCompression.None,
        }).Write(original);
        var spec32 = ReparseMzxml(bits32).Run.SpectrumList!.GetSpectrum(0, getBinaryData: true);
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0 }[i], spec32.GetMZArray()!.Data[i], 1e-3);
            Assert.AreEqual(new[] { 50.0, 100.0, 150.0, 200.0 }[i], spec32.GetIntensityArray()!.Data[i], 1e-3);
        }
    }

    [TestMethod]
    public void RoundTrip_DerivesRunIdFromParentFile()
    {
        // FillInMetadata derives the run id from the parentFile name ("data.raw" → "data").
        var reparsed = ReparseMzxml(new MzxmlWriter().Write(BuildSynthetic()));
        Assert.AreEqual("data", reparsed.Id);
        Assert.AreEqual("data", reparsed.Run.Id);

        // Source file CV terms re-stamped on read.
        var sf = reparsed.FileDescription.SourceFiles[0];
        Assert.IsTrue(sf.HasCVParam(CVID.MS_Thermo_RAW_format));
        Assert.IsTrue(sf.HasCVParam(CVID.MS_Thermo_nativeID_format));
    }

    [TestMethod]
    public void Lazy_RoundTripUsesSpectrumListMzxml_AndServesSpectraOnDemand()
    {
        // Build a small multi-spectrum doc, write to a temp file (writer emits an
        // <indexOffset> footer by default), re-read via the adapter. Expect lazy path.
        var msd = BuildSynthetic();
        // Add a second spectrum so we exercise the per-scan seek.
        var sl = (SpectrumListSimple)msd.Run.SpectrumList!;
        var spec2 = new Spectrum { Index = 1, Id = "controllerType=0 controllerNumber=1 scan=2" };
        spec2.Params.Set(CVID.MS_ms_level, 2);
        spec2.Params.Set(CVID.MS_MSn_spectrum);
        spec2.Params.Set(CVID.MS_positive_scan);
        spec2.ScanList.Set(CVID.MS_no_combination);
        spec2.ScanList.Scans.Add(new Scan());
        spec2.SetMZIntensityArrays(new[] { 250.0, 350.0 }, new[] { 11.0, 22.0 },
            CVID.MS_number_of_detector_counts);
        sl.Spectra.Add(spec2);

        string path = Path.Combine(Path.GetTempPath(), $"lazy-mzxml-{System.Guid.NewGuid():N}.mzXML");
        try
        {
            using (var fs = File.Create(path))
            {
                byte[] xml = System.Text.Encoding.UTF8.GetBytes(new MzxmlWriter().Write(msd));
                fs.Write(xml, 0, xml.Length);
            }

            // The lazy footer must be parseable on its own.
            var footer = MzxmlIndexFooter.TryRead(path);
            Assert.IsNotNull(footer, "Indexed mzXML footer not found in writer output — lazy path will fall back to eager");
            Assert.AreEqual(2, footer.Value.ScanOffsets.Length);
            Assert.AreEqual("scan=1", footer.Value.ScanIds[0]);
            Assert.AreEqual("scan=2", footer.Value.ScanIds[1]);

            var rt = new MSData();
            new Pwiz.Data.MsData.Readers.MzxmlReaderAdapter().Read(path, rt);
            Assert.IsInstanceOfType(rt.Run.SpectrumList, typeof(SpectrumList_Mzxml),
                "Adapter didn't take the lazy path on an indexed mzXML");
            Assert.AreEqual(2, rt.Run.SpectrumList!.Count);

            // Out-of-order access exercises the seek path properly.
            var s1 = rt.Run.SpectrumList.GetSpectrum(1, getBinaryData: true);
            Assert.AreEqual("scan=2", s1.Id);
            Assert.AreEqual(2, s1.Params.CvParam(CVID.MS_ms_level).ValueAs<int>());
            CollectionAssert.AreEqual(new[] { 250.0, 350.0 }, s1.GetMZArray()!.Data);

            var s0 = rt.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
            Assert.AreEqual("scan=1", s0.Id);
            Assert.AreEqual(4, s0.DefaultArrayLength);
            CollectionAssert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0 }, s0.GetMZArray()!.Data);

            // Metadata-only (getBinaryData=false) returns the spectrum but skips peak decoding.
            var s0meta = rt.Run.SpectrumList.GetSpectrum(0, getBinaryData: false);
            Assert.AreEqual("scan=1", s0meta.Id);
            Assert.AreEqual(0, s0meta.GetMZArray()!.Data.Count);
        }
        finally { try { File.Delete(path); } catch { } }
    }
}
