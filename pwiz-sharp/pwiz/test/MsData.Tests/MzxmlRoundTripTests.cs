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

    /// <summary>
    /// Some converters (ReAdW / old TPP) emit indexed mzXML with NO <c>&lt;/scan&gt;</c> end
    /// tags — each <c>&lt;scan&gt;</c> opens straight after the prior scan's <c>&lt;peaks&gt;</c>,
    /// so as pure XML every scan is nested inside the one before it. The lazy reader must serve
    /// each scan from its own indexed byte offset in O(scan size), NOT descend through the entire
    /// remaining nesting to EOF on every read (which made a real 55k-scan MRM file take ~50 min
    /// and silently stall Skyline's import). This builds such a file at a scale where the old
    /// O(N^2) behavior blows a generous time budget, and checks each scan still decodes its own
    /// peaks under out-of-order access.
    /// </summary>
    [TestMethod]
    public void Lazy_MzxmlWithNoScanEndTags_ReadsEachScanBounded()
    {
        // n is chosen large enough that the old O(N^2) "descend to EOF on every read" behavior
        // blows the time budget on any machine, while the bounded read stays well under a second.
        const int n = 12000;
        string path = Path.Combine(Path.GetTempPath(), $"noclose-mzxml-{System.Guid.NewGuid():N}.mzXML");
        try
        {
            File.WriteAllBytes(path, BuildUnclosedIndexedMzxml(n));

            var footer = MzxmlIndexFooter.TryRead(path);
            Assert.IsNotNull(footer, "Index footer of the no-</scan> file must validate (probed offsets point at <scan num=...>).");
            Assert.AreEqual(n, footer.Value.ScanOffsets.Length);

            var rt = new MSData();
            new Pwiz.Data.MsData.Readers.MzxmlReaderAdapter().Read(path, rt);
            Assert.IsInstanceOfType(rt.Run.SpectrumList, typeof(SpectrumList_Mzxml),
                "Adapter didn't take the lazy path on the indexed no-</scan> mzXML");
            var sl = rt.Run.SpectrumList!;
            Assert.AreEqual(n, sl.Count);

            // A single early-scan read must not walk the whole file: on the old code this one call
            // alone read ~all N scans. Also verify it returns THIS scan's peaks, not a later one's.
            var s0 = sl.GetSpectrum(0, getBinaryData: true);
            Assert.AreEqual("scan=1", s0.Id);
            Assert.AreEqual(100.0, s0.GetMZArray()!.Data[0], 1e-3);
            Assert.AreEqual(1000.0, s0.GetIntensityArray()!.Data[0], 1e-3);

            // Out-of-order spot checks across the file.
            foreach (int i in new[] { n - 1, n / 2, 7, 1 })
            {
                var s = sl.GetSpectrum(i, getBinaryData: true);
                Assert.AreEqual($"scan={i + 1}", s.Id);
                Assert.AreEqual(100.0 + i, s.GetMZArray()!.Data[0], 1e-3, $"m/z mismatch at scan {i}");
                Assert.AreEqual(1000.0 + i, s.GetIntensityArray()!.Data[0], 1e-3, $"intensity mismatch at scan {i}");
            }

            // Full sequential walk must stay linear. Budget is deliberately generous (the fix does
            // this in well under a second); the old O(N^2) path took tens of seconds at this scale.
            var swWalk = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < n; i++)
                _ = sl.GetSpectrum(i, getBinaryData: true);
            swWalk.Stop();
            Assert.IsTrue(swWalk.ElapsedMilliseconds < 10_000,
                $"Full walk of {n} scans took {swWalk.ElapsedMilliseconds} ms — expected O(N), suspect the per-scan read is over-reading to EOF again.");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    /// <summary>
    /// Builds a minimal indexed mzXML with <paramref name="n"/> single-peak MRM scans and, crucially,
    /// NO <c>&lt;/scan&gt;</c> end tags, with an <c>&lt;index&gt;</c>/<c>&lt;indexOffset&gt;</c> footer
    /// whose offsets point at each <c>&lt;scan num="i"&gt;</c>. Scan i carries one m/z-int pair
    /// (100+i, 1000+i) as a big-endian 32-bit peaks payload. All markup is ASCII, so string index
    /// equals byte offset.
    /// </summary>
    private static byte[] BuildUnclosedIndexedMzxml(int n)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\n");
        sb.Append("<mzXML xmlns=\"http://sashimi.sourceforge.net/schema_revision/mzXML_2.0\">\n");
        sb.Append(" <msRun scanCount=\"").Append(n).Append("\">\n");

        var offsets = new long[n];
        for (int i = 0; i < n; i++)
        {
            offsets[i] = sb.Length;            // byte offset of the '<' in "<scan"
            int num = i + 1;
            string peaks = EncodeBigEndian32Pair(100.0f + i, 1000.0f + i);
            sb.Append("  <scan num=\"").Append(num).Append('"')
              .Append(" msLevel=\"2\" peaksCount=\"1\" scanType=\"MRM\"")
              .Append(" retentionTime=\"PT").Append((i * 0.1).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("S\">\n")
              .Append("        <precursorMz>").Append((500.0 + i).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("</precursorMz>\n")
              .Append("        <peaks precision=\"32\" byteOrder=\"network\" pairOrder=\"m/z-int\">")
              .Append(peaks).Append("</peaks>\n");   // NOTE: no </scan>
        }
        sb.Append("</msRun>\n");

        long indexOffset = sb.Length;
        sb.Append("  <index name=\"scan\">\n");
        for (int i = 0; i < n; i++)
            sb.Append("    <offset id=\"").Append(i + 1).Append("\">").Append(offsets[i]).Append("</offset>\n");
        sb.Append("  </index>\n");
        sb.Append("  <indexOffset>").Append(indexOffset).Append("</indexOffset>\n");
        sb.Append("  <sha1>0000000000000000000000000000000000000000</sha1>\n");
        sb.Append("</mzXML>\n");

        return System.Text.Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static string EncodeBigEndian32Pair(float mz, float intensity)
    {
        var bytes = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteSingleBigEndian(bytes.AsSpan(0, 4), mz);
        System.Buffers.Binary.BinaryPrimitives.WriteSingleBigEndian(bytes.AsSpan(4, 4), intensity);
        return System.Convert.ToBase64String(bytes);
    }
}
