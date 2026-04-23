using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Samples;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests.Mzml;

[TestClass]
public class MzmlRoundTripTests
{
    private static MSData BuildSynthetic()
    {
        var msd = new MSData { Id = "roundtrip_doc" };
        msd.CVs.AddRange(MSData.DefaultCVList);

        msd.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum);
        msd.FileDescription.SourceFiles.Add(new SourceFile("sf1", "data.raw", "file:///.")
        {
            // Tag the source file with a vendor format CV so round-trip covers CV param reads.
        });
        msd.FileDescription.SourceFiles[0].Set(CVID.MS_Thermo_RAW_format);

        msd.Samples.Add(new Sample("samp1", "sample-1"));

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
        dp.ProcessingMethods.Add(new ProcessingMethod { Order = 1, Software = sw });
        dp.ProcessingMethods[0].Set(CVID.MS_Conversion_to_mzML);
        msd.DataProcessings.Add(dp);

        var list = new SpectrumListSimple { Dp = dp };

        var spec = new Spectrum
        {
            Index = 0,
            Id = "controllerType=0 controllerNumber=1 scan=1",
            DefaultArrayLength = 4,
        };
        spec.Params.Set(CVID.MS_ms_level, 1);
        spec.Params.Set(CVID.MS_MSn_spectrum);
        spec.Params.Set(CVID.MS_positive_scan);

        var scan = new Scan { InstrumentConfiguration = ic };
        scan.Set(CVID.MS_scan_start_time, 1.5, CVID.UO_minute);
        scan.UserParams.Add(new Pwiz.Data.Common.Params.UserParam("comment", "round-trip test"));
        scan.ScanWindows.Add(new ScanWindow(100.0, 1000.0, CVID.MS_m_z));
        spec.ScanList.Scans.Add(scan);

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
        var xml = new MzmlWriter().Write(BuildSynthetic());
        StringAssert.Contains(xml, "<mzML", StringComparison.Ordinal);
        StringAssert.Contains(xml, "</mzML>", StringComparison.Ordinal);
        StringAssert.Contains(xml, "MS:1000511", StringComparison.Ordinal); // MS_ms_level accession
    }

    [TestMethod]
    public void RoundTrip_PreservesTopLevelMetadata()
    {
        var original = BuildSynthetic();
        var xml = new MzmlWriter().Write(original);
        var reparsed = new MzmlReader().Read(xml);

        Assert.AreEqual(original.Id, reparsed.Id);
        Assert.AreEqual(2, reparsed.CVs.Count);
        Assert.AreEqual(1, reparsed.FileDescription.SourceFiles.Count);
        Assert.AreEqual("data.raw", reparsed.FileDescription.SourceFiles[0].Name);
        Assert.AreEqual(1, reparsed.Samples.Count);
        Assert.AreEqual(1, reparsed.Software.Count);
        Assert.AreEqual("0.1", reparsed.Software[0].Version);
        Assert.AreEqual(1, reparsed.InstrumentConfigurations.Count);
        Assert.AreEqual(3, reparsed.InstrumentConfigurations[0].ComponentList.Count);
        Assert.AreEqual(1, reparsed.DataProcessings.Count);
    }

    [TestMethod]
    public void RoundTrip_PreservesRefs()
    {
        var xml = new MzmlWriter().Write(BuildSynthetic());
        var reparsed = new MzmlReader().Read(xml);

        // defaultInstrumentConfigurationRef → InstrumentConfiguration
        Assert.IsNotNull(reparsed.Run.DefaultInstrumentConfiguration);
        Assert.AreEqual("IC1", reparsed.Run.DefaultInstrumentConfiguration.Id);

        // softwareRef inside InstrumentConfiguration → Software
        Assert.IsNotNull(reparsed.InstrumentConfigurations[0].Software);
        Assert.AreEqual("pwiz-sharp", reparsed.InstrumentConfigurations[0].Software!.Id);

        // softwareRef inside processingMethod → Software
        var pm = reparsed.DataProcessings[0].ProcessingMethods[0];
        Assert.IsNotNull(pm.Software);
        Assert.AreEqual("pwiz-sharp", pm.Software!.Id);

        // defaultDataProcessingRef inside spectrumList → DataProcessing
        Assert.IsNotNull(reparsed.Run.SpectrumList);
        Assert.IsNotNull(reparsed.Run.SpectrumList.DataProcessing);
        Assert.AreEqual("dp1", reparsed.Run.SpectrumList.DataProcessing!.Id);

        // instrumentConfigurationRef inside scan → InstrumentConfiguration
        var spec = reparsed.Run.SpectrumList.GetSpectrum(0);
        Assert.IsNotNull(spec.ScanList.Scans[0].InstrumentConfiguration);
        Assert.AreEqual("IC1", spec.ScanList.Scans[0].InstrumentConfiguration!.Id);
    }

    [TestMethod]
    public void RoundTrip_PreservesSpectrumBinary()
    {
        var xml = new MzmlWriter().Write(BuildSynthetic());
        var reparsed = new MzmlReader().Read(xml);

        var spec = reparsed.Run.SpectrumList!.GetSpectrum(0);
        Assert.AreEqual(4, spec.DefaultArrayLength);
        Assert.AreEqual("controllerType=0 controllerNumber=1 scan=1", spec.Id);

        var mz = spec.GetMZArray();
        var intensity = spec.GetIntensityArray();
        Assert.IsNotNull(mz);
        Assert.IsNotNull(intensity);
        CollectionAssert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0 }, mz.Data);
        CollectionAssert.AreEqual(new[] { 50.0, 100.0, 150.0, 200.0 }, intensity.Data);
    }

    [TestMethod]
    public void RoundTrip_PreservesUserParams()
    {
        var xml = new MzmlWriter().Write(BuildSynthetic());
        var reparsed = new MzmlReader().Read(xml);

        var scan = reparsed.Run.SpectrumList!.GetSpectrum(0).ScanList.Scans[0];
        var up = scan.UserParam("comment");
        Assert.AreEqual("round-trip test", up.Value);
    }

    [TestMethod]
    public void RoundTrip_ScanStartTime_UnitsPreserved()
    {
        var xml = new MzmlWriter().Write(BuildSynthetic());
        var reparsed = new MzmlReader().Read(xml);

        var scan = reparsed.Run.SpectrumList!.GetSpectrum(0).ScanList.Scans[0];
        var p = scan.CvParam(CVID.MS_scan_start_time);
        Assert.AreEqual(CVID.UO_minute, p.Units);
        Assert.AreEqual(90.0, p.TimeInSeconds(), 1e-9); // 1.5 min * 60
    }

    [TestMethod]
    public void RoundTrip_WithZlibCompression_PreservesBinaryExactly()
    {
        var writerCfg = new BinaryEncoderConfig { Compression = BinaryCompression.Zlib };
        var xml = new MzmlWriter(writerCfg).Write(BuildSynthetic());
        var reparsed = new MzmlReader().Read(xml);

        var spec = reparsed.Run.SpectrumList!.GetSpectrum(0);
        var mz = spec.GetMZArray();
        Assert.IsNotNull(mz);
        CollectionAssert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0 }, mz.Data);

        // Verify the round-tripped spectrum reports zlib (the writer stamped the CV, reader honored it).
        Assert.IsTrue(mz.HasCVParam(CVID.MS_zlib_compression));
    }

    [TestMethod]
    public void RoundTrip_With32BitPrecision_LossyButCloseToOriginal()
    {
        var writerCfg = new BinaryEncoderConfig { Precision = BinaryPrecision.Bits32 };
        var xml = new MzmlWriter(writerCfg).Write(BuildSynthetic());
        var reparsed = new MzmlReader().Read(xml);

        var spec = reparsed.Run.SpectrumList!.GetSpectrum(0);
        var mz = spec.GetMZArray()!;
        Assert.AreEqual(4, mz.Data.Count);
        for (int i = 0; i < mz.Data.Count; i++)
            Assert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0 }[i], mz.Data[i], 1e-4);
        Assert.IsTrue(mz.HasCVParam(CVID.MS_32_bit_float));
    }

    [TestMethod]
    public void RoundTrip_WithNumpressLinear_PreservesMzWithinTolerance()
    {
        var writerCfg = new BinaryEncoderConfig { Numpress = BinaryNumpress.Linear };
        var xml = new MzmlWriter(writerCfg).Write(BuildSynthetic());
        var reparsed = new MzmlReader().Read(xml);

        var spec = reparsed.Run.SpectrumList!.GetSpectrum(0);
        var mz = spec.GetMZArray()!;
        Assert.AreEqual(4, mz.Data.Count);
        for (int i = 0; i < mz.Data.Count; i++)
            Assert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0 }[i], mz.Data[i], 5e-6);
        Assert.IsTrue(mz.HasCVParam(CVID.MS_MS_Numpress_linear_prediction_compression));
    }

    [TestMethod]
    public void RoundTrip_WithNumpressLinearPlusZlib_EmitsCombinedCvTerm()
    {
        var writerCfg = new BinaryEncoderConfig
        {
            Numpress = BinaryNumpress.Linear,
            Compression = BinaryCompression.Zlib,
        };
        var xml = new MzmlWriter(writerCfg).Write(BuildSynthetic());
        var reparsed = new MzmlReader().Read(xml);

        var spec = reparsed.Run.SpectrumList!.GetSpectrum(0);
        var mz = spec.GetMZArray()!;
        Assert.AreEqual(4, mz.Data.Count);
        Assert.IsTrue(mz.HasCVParam(CVID.MS_MS_Numpress_linear_prediction_compression_followed_by_zlib_compression));
    }

    [TestMethod]
    public void RoundTrip_IndexedMzml_WrapsInIndexedEnvelope()
    {
        var xml = new MzmlWriter { Indexed = true }.Write(BuildSynthetic());
        StringAssert.Contains(xml, "<indexedmzML", StringComparison.Ordinal);
        StringAssert.Contains(xml, "</indexedmzML>", StringComparison.Ordinal);
        StringAssert.Contains(xml, "<indexList", StringComparison.Ordinal);
        StringAssert.Contains(xml, "<indexListOffset>", StringComparison.Ordinal);
        StringAssert.Contains(xml, "<fileChecksum>", StringComparison.Ordinal);
    }

    [TestMethod]
    public void RoundTrip_IndexedMzml_OffsetsPointAtStartTag()
    {
        var ms = new MemoryStream();
        new MzmlWriter { Indexed = true }.Write(BuildSynthetic(), ms);
        byte[] bytes = ms.ToArray();

        // Parse the first <offset> entry.
        string xml = System.Text.Encoding.UTF8.GetString(bytes);
        int offsetStart = xml.IndexOf("<offset idRef=", StringComparison.Ordinal);
        int valueStart = xml.IndexOf('>', offsetStart) + 1;
        int valueEnd = xml.IndexOf('<', valueStart);
        long offset = long.Parse(xml.AsSpan(valueStart, valueEnd - valueStart),
            System.Globalization.CultureInfo.InvariantCulture);

        // The byte at that offset must be the '<' of <spectrum>.
        Assert.AreEqual((byte)'<', bytes[offset]);
        var slice = System.Text.Encoding.UTF8.GetString(bytes, (int)offset, Math.Min(10, bytes.Length - (int)offset));
        Assert.IsTrue(slice.StartsWith("<spectrum", StringComparison.Ordinal),
            $"offset {offset} does not point to <spectrum; saw '{slice}'");
    }

    [TestMethod]
    public void RoundTrip_IndexedMzml_FileChecksumMatchesContent()
    {
        var ms = new MemoryStream();
        new MzmlWriter { Indexed = true }.Write(BuildSynthetic(), ms);
        byte[] bytes = ms.ToArray();

        string xml = System.Text.Encoding.UTF8.GetString(bytes);
        // The mzML indexedmzML spec says the checksum is computed from file start up to and
        // including the opening "<fileChecksum" tag — specifically, the hash stops before the
        // closing '>' of that open tag (XmlWriter flushes the "<fileChecksum" bytes but defers
        // writing '>' until the content starts).
        int hashBoundary = xml.IndexOf("<fileChecksum", StringComparison.Ordinal) + "<fileChecksum".Length;
        int checksumStart = xml.IndexOf("<fileChecksum>", StringComparison.Ordinal) + "<fileChecksum>".Length;
        int checksumEnd = xml.IndexOf("</fileChecksum>", checksumStart, StringComparison.Ordinal);
        string digest = xml[checksumStart..checksumEnd];

#pragma warning disable CA5350 // SHA-1 is the digest the mzML indexedmzML spec mandates.
        byte[] expected = System.Security.Cryptography.SHA1.HashData(bytes.AsSpan(0, hashBoundary));
#pragma warning restore CA5350
        var sb = new System.Text.StringBuilder(expected.Length * 2);
        foreach (byte b in expected) sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        Assert.AreEqual(sb.ToString(), digest);
    }

    [TestMethod]
    public void RoundTrip_NoIndex_ProducesPlainMzml()
    {
        var xml = new MzmlWriter { Indexed = false }.Write(BuildSynthetic());
        Assert.IsFalse(xml.Contains("<indexedmzML", StringComparison.Ordinal));
        Assert.IsFalse(xml.Contains("<fileChecksum>", StringComparison.Ordinal));
        Assert.IsTrue(xml.Contains("<mzML", StringComparison.Ordinal));
    }
}
