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
        msd.FileDescription.SourceFiles.Add(new SourceFile("sf1", "data.raw", "file:///."));
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
    public void RoundTrip_MetadataAndRefs()
    {
        // Round-trip both the top-level metadata (CVs, sources, software, IC, DP, samples) and
        // the cross-references (defaultInstrumentConfigurationRef, softwareRef inside IC and PM,
        // defaultDataProcessingRef on spectrumList, instrumentConfigurationRef on scan).
        var original = BuildSynthetic();
        var xml = new MzmlWriter().Write(original);

        // Sanity: the written XML is a well-formed mzML document with the expected envelope and
        // a known CV accession. Folded in here rather than its own MSTest entry — failure here
        // means RoundTrip below will also fail with a less localized message.
        StringAssert.Contains(xml, "<mzML", StringComparison.Ordinal);
        StringAssert.Contains(xml, "</mzML>", StringComparison.Ordinal);
        StringAssert.Contains(xml, "MS:1000511", StringComparison.Ordinal); // MS_ms_level accession

        var reparsed = new MzmlReader().Read(xml);

        Assert.AreEqual(original.Id, reparsed.Id);
        Assert.AreEqual(2, reparsed.CVs.Count, "CV list");
        Assert.AreEqual(1, reparsed.FileDescription.SourceFiles.Count);
        Assert.AreEqual("data.raw", reparsed.FileDescription.SourceFiles[0].Name);
        Assert.AreEqual(1, reparsed.Samples.Count);
        Assert.AreEqual(1, reparsed.Software.Count);
        Assert.AreEqual("0.1", reparsed.Software[0].Version);
        Assert.AreEqual(1, reparsed.InstrumentConfigurations.Count);
        Assert.AreEqual(3, reparsed.InstrumentConfigurations[0].ComponentList.Count);
        Assert.AreEqual(1, reparsed.DataProcessings.Count);

        // Cross-refs.
        Assert.IsNotNull(reparsed.Run.DefaultInstrumentConfiguration);
        Assert.AreEqual("IC1", reparsed.Run.DefaultInstrumentConfiguration!.Id);
        Assert.IsNotNull(reparsed.InstrumentConfigurations[0].Software);
        Assert.AreEqual("pwiz-sharp", reparsed.InstrumentConfigurations[0].Software!.Id);
        var pm = reparsed.DataProcessings[0].ProcessingMethods[0];
        Assert.IsNotNull(pm.Software);
        Assert.AreEqual("pwiz-sharp", pm.Software!.Id);
        Assert.IsNotNull(reparsed.Run.SpectrumList);
        Assert.IsNotNull(reparsed.Run.SpectrumList.DataProcessing);
        Assert.AreEqual("dp1", reparsed.Run.SpectrumList.DataProcessing!.Id);
        var spec = reparsed.Run.SpectrumList.GetSpectrum(0);
        Assert.IsNotNull(spec.ScanList.Scans[0].InstrumentConfiguration);
        Assert.AreEqual("IC1", spec.ScanList.Scans[0].InstrumentConfiguration!.Id);
    }

    [TestMethod]
    public void RoundTrip_SpectrumBinaryAndUserParamsAndUnits()
    {
        var xml = new MzmlWriter().Write(BuildSynthetic());
        var reparsed = new MzmlReader().Read(xml);
        var spec = reparsed.Run.SpectrumList!.GetSpectrum(0);

        // Binary arrays preserved bit-for-bit at default 64-bit precision.
        Assert.AreEqual(4, spec.DefaultArrayLength);
        Assert.AreEqual("controllerType=0 controllerNumber=1 scan=1", spec.Id);
        CollectionAssert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0 }, spec.GetMZArray()!.Data);
        CollectionAssert.AreEqual(new[] { 50.0, 100.0, 150.0, 200.0 }, spec.GetIntensityArray()!.Data);

        // UserParam round-trips through scan.
        var scan = spec.ScanList.Scans[0];
        Assert.AreEqual("round-trip test", scan.UserParam("comment").Value);

        // CV unit reference preserved (UO_minute) and TimeInSeconds applies the conversion.
        var rt = scan.CvParam(CVID.MS_scan_start_time);
        Assert.AreEqual(CVID.UO_minute, rt.Units);
        Assert.AreEqual(90.0, rt.TimeInSeconds(), 1e-9, "1.5 min * 60");
    }

    [TestMethod]
    public void RoundTrip_BinaryEncodingVariants()
    {
        // Zlib: lossless, plus the zlib_compression CV is stamped on the binary array.
        var zlibXml = new MzmlWriter(new BinaryEncoderConfig { Compression = BinaryCompression.Zlib })
            .Write(BuildSynthetic());
        var zlibMz = new MzmlReader().Read(zlibXml).Run.SpectrumList!.GetSpectrum(0).GetMZArray()!;
        CollectionAssert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0 }, zlibMz.Data, "zlib lossless");
        Assert.IsTrue(zlibMz.HasCVParam(CVID.MS_zlib_compression), "zlib CV stamped");

        // 32-bit precision: lossy at ~1e-4 Da; MS_32_bit_float CV is stamped.
        var bits32Xml = new MzmlWriter(new BinaryEncoderConfig { Precision = BinaryPrecision.Bits32 })
            .Write(BuildSynthetic());
        var bits32Mz = new MzmlReader().Read(bits32Xml).Run.SpectrumList!.GetSpectrum(0).GetMZArray()!;
        Assert.AreEqual(4, bits32Mz.Data.Count);
        for (int i = 0; i < bits32Mz.Data.Count; i++)
            Assert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0 }[i], bits32Mz.Data[i], 1e-4);
        Assert.IsTrue(bits32Mz.HasCVParam(CVID.MS_32_bit_float));

        // Numpress Linear alone -> linear_prediction_compression CV.
        var numpressXml = new MzmlWriter(new BinaryEncoderConfig { Numpress = BinaryNumpress.Linear })
            .Write(BuildSynthetic());
        var numpressMz = new MzmlReader().Read(numpressXml).Run.SpectrumList!.GetSpectrum(0).GetMZArray()!;
        for (int i = 0; i < numpressMz.Data.Count; i++)
            Assert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0 }[i], numpressMz.Data[i], 5e-6);
        Assert.IsTrue(numpressMz.HasCVParam(CVID.MS_MS_Numpress_linear_prediction_compression));

        // Numpress Linear + zlib -> combined CV term.
        var combinedXml = new MzmlWriter(new BinaryEncoderConfig
        {
            Numpress = BinaryNumpress.Linear,
            Compression = BinaryCompression.Zlib,
        }).Write(BuildSynthetic());
        var combinedMz = new MzmlReader().Read(combinedXml).Run.SpectrumList!.GetSpectrum(0).GetMZArray()!;
        Assert.AreEqual(4, combinedMz.Data.Count);
        Assert.IsTrue(combinedMz.HasCVParam(CVID.MS_MS_Numpress_linear_prediction_compression_followed_by_zlib_compression));
    }

    [TestMethod]
    public void IndexedMzml_EnvelopeOffsetsChecksum_AndPlainNoIndexFallback()
    {
        // No-index variant first: writer with Indexed=false skips the indexedmzML envelope and
        // emits a plain <mzML> document. Folded in here so the indexed-vs-not contrast is visible
        // on a single MSTest entry.
        var plainXml = new MzmlWriter { Indexed = false }.Write(BuildSynthetic());
        Assert.IsFalse(plainXml.Contains("<indexedmzML", StringComparison.Ordinal));
        Assert.IsFalse(plainXml.Contains("<fileChecksum>", StringComparison.Ordinal));
        Assert.IsTrue(plainXml.Contains("<mzML", StringComparison.Ordinal));

        var ms = new MemoryStream();
        new MzmlWriter { Indexed = true }.Write(BuildSynthetic(), ms);
        byte[] bytes = ms.ToArray();
        string xml = System.Text.Encoding.UTF8.GetString(bytes);

        // Envelope tags present.
        StringAssert.Contains(xml, "<indexedmzML", StringComparison.Ordinal);
        StringAssert.Contains(xml, "</indexedmzML>", StringComparison.Ordinal);
        StringAssert.Contains(xml, "<indexList", StringComparison.Ordinal);
        StringAssert.Contains(xml, "<indexListOffset>", StringComparison.Ordinal);
        StringAssert.Contains(xml, "<fileChecksum>", StringComparison.Ordinal);

        // First spectrum offset points at the start of <spectrum.
        int offsetStart = xml.IndexOf("<offset idRef=", StringComparison.Ordinal);
        int valueStart = xml.IndexOf('>', offsetStart) + 1;
        int valueEnd = xml.IndexOf('<', valueStart);
        long offset = long.Parse(xml.AsSpan(valueStart, valueEnd - valueStart),
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual((byte)'<', bytes[offset], "offset points at '<'");
        var slice = System.Text.Encoding.UTF8.GetString(bytes, (int)offset, Math.Min(10, bytes.Length - (int)offset));
        Assert.IsTrue(slice.StartsWith("<spectrum", StringComparison.Ordinal),
            $"offset {offset} does not point to <spectrum; saw '{slice}'");

        // fileChecksum SHA-1 covers everything up to and including "<fileChecksum" (excludes
        // the closing '>' of that tag).
        int hashBoundary = xml.IndexOf("<fileChecksum", StringComparison.Ordinal) + "<fileChecksum".Length;
        int checksumStart = xml.IndexOf("<fileChecksum>", StringComparison.Ordinal) + "<fileChecksum>".Length;
        int checksumEnd = xml.IndexOf("</fileChecksum>", checksumStart, StringComparison.Ordinal);
        string digest = xml[checksumStart..checksumEnd];
#pragma warning disable CA5350 // SHA-1 is the digest the mzML indexedmzML spec mandates.
        byte[] expected = System.Security.Cryptography.SHA1.HashData(bytes.AsSpan(0, hashBoundary));
#pragma warning restore CA5350
        var sb = new System.Text.StringBuilder(expected.Length * 2);
        foreach (byte b in expected)
            sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        Assert.AreEqual(sb.ToString(), digest, "fileChecksum SHA-1");
    }

}
