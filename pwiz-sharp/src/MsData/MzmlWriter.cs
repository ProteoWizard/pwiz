using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Samples;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Mzml;

/// <summary>
/// Serializes an <see cref="MSData"/> to mzML 1.1 XML.
/// </summary>
/// <remarks>
/// Port of pwiz::msdata::Serializer_mzML (write path).
/// Covers the subset supported by <see cref="MSData"/> in this port: cvList, fileDescription,
/// referenceableParamGroupList, sampleList, softwareList, instrumentConfigurationList,
/// dataProcessingList, run (with spectrumList). Chromatograms, indexedmzML wrapper, and
/// scanSettingsList are deferred to follow-up tasks.
/// </remarks>
#pragma warning disable CA1001 // _stream is a per-call wrapper disposed inside Write; no class-level disposable lifetime.
public sealed class MzmlWriter
#pragma warning restore CA1001
{
    private readonly BinaryEncoderConfig _encoderConfig;

    // Mutable state only during Write(). An instance may be reused for successive Write calls.
    private HashingCountingStream? _stream;
    private List<(string Id, long Offset)>? _spectrumOffsets;
    private List<(string Id, long Offset)>? _chromatogramOffsets;
    private string? _runDefaultIcId;

    /// <summary>When true, wrap the mzML in an <c>&lt;indexedmzML&gt;</c> envelope with byte-offset
    /// indexes and an SHA-1 fileChecksum (matches pwiz C++ msconvert's default output).</summary>
    public bool Indexed { get; set; } = true;

    /// <summary>Creates a writer that encodes binary arrays with the given config.</summary>
    public MzmlWriter(BinaryEncoderConfig? encoderConfig = null)
    {
        _encoderConfig = encoderConfig ?? new BinaryEncoderConfig();
    }

    /// <summary>Writes <paramref name="msd"/> as mzML XML to a new string.</summary>
    public string Write(MSData msd)
    {
        // Route through the stream overload so the emitted string declares utf-8 (matching the bytes).
        // Going through StringBuilder would stamp encoding="utf-16" because of how XmlWriter reflects
        // the sink's char type, which then mismatches any UTF-8 file the caller writes the string into.
        using var ms = new MemoryStream();
        Write(msd, ms);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>Writes <paramref name="msd"/> as mzML XML to <paramref name="stream"/> in UTF-8 (no BOM).</summary>
    public void Write(MSData msd, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(msd);
        ArgumentNullException.ThrowIfNull(stream);

        if (!Indexed)
        {
            using var w = CreateWriter(stream);
            WriteMzmlDocument(msd, w);
            return;
        }

        // Indexed mode: wrap the stream so we can record byte offsets + SHA-1, and emit an
        // <indexedmzML> envelope around <mzML> with the index, indexListOffset, and fileChecksum.
        _stream = new HashingCountingStream(stream);
        _spectrumOffsets = new List<(string, long)>();
        _chromatogramOffsets = new List<(string, long)>();
        try
        {
            using var w = CreateWriter(_stream);
            WriteIndexedEnvelope(msd, w);
        }
        finally
        {
            _stream.Dispose();
            _stream = null;
            _spectrumOffsets = null;
            _chromatogramOffsets = null;
        }
    }

    private static XmlWriter CreateWriter(Stream s) =>
        XmlWriter.Create(s, new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false,
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            ConformanceLevel = ConformanceLevel.Document,
            CloseOutput = false,
        });

    private void WriteMzmlDocument(MSData msd, XmlWriter w)
    {
        w.WriteStartDocument();
        WriteMzmlElement(msd, w);
        w.WriteEndDocument();
    }

    private void WriteMzmlElement(MSData msd, XmlWriter w)
    {
        w.WriteStartElement("mzML", MzmlXml.MzmlNamespace);
        w.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        w.WriteAttributeString("xsi", "schemaLocation", null,
            "http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML1.1.0.xsd");
        if (!string.IsNullOrEmpty(msd.Accession)) w.WriteAttributeString("accession", msd.Accession);
        if (!string.IsNullOrEmpty(msd.Id)) w.WriteAttributeString("id", msd.Id);
        w.WriteAttributeString("version", string.IsNullOrEmpty(msd.Version) ? "1.1.0" : msd.Version);

        WriteCvList(w, msd.CVs);
        WriteFileDescription(w, msd.FileDescription);
        WriteReferenceableParamGroupList(w, msd.ParamGroups);
        WriteSampleList(w, msd.Samples);
        WriteSoftwareList(w, msd.Software);
        WriteInstrumentConfigurationList(w, msd.InstrumentConfigurations);
        WriteDataProcessingList(w, msd.DataProcessings);
        WriteRun(w, msd.Run);

        w.WriteEndElement(); // mzML
    }

    private void WriteIndexedEnvelope(MSData msd, XmlWriter w)
    {
        w.WriteStartDocument();
        w.WriteStartElement("indexedmzML", MzmlXml.MzmlNamespace);
        w.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        w.WriteAttributeString("xsi", "schemaLocation", null,
            "http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/mzML1.1.2_idx.xsd");

        WriteMzmlElement(msd, w);

        // Capture the position where <indexList> begins so we can emit it as <indexListOffset>.
        w.Flush();
        long indexListOffset = _stream!.BytesWritten;

        WriteIndexList(w);

        w.WriteStartElement("indexListOffset");
        w.WriteString(indexListOffset.ToString(CultureInfo.InvariantCulture));
        w.WriteEndElement();

        // fileChecksum: the SHA-1 is of every byte up to (and including) the open tag
        // <fileChecksum>. Flush right after writing the start tag, snapshot the running hash,
        // stop hashing, then emit the digest as element content.
        w.WriteStartElement("fileChecksum");
        w.Flush();
        byte[] digest = _stream.GetCurrentHash();
        _stream.StopHashing();
        w.WriteString(ToHex(digest));
        w.WriteEndElement();

        w.WriteEndElement(); // indexedmzML
        w.WriteEndDocument();
    }

    private void WriteIndexList(XmlWriter w)
    {
        w.WriteStartElement("indexList");
        MzmlXml.WriteCountAttr(w, 2);

        WriteIndex(w, "spectrum", _spectrumOffsets!);
        WriteIndex(w, "chromatogram", _chromatogramOffsets!);

        w.WriteEndElement();
    }

    private static void WriteIndex(XmlWriter w, string name, List<(string Id, long Offset)> offsets)
    {
        w.WriteStartElement("index");
        w.WriteAttributeString("name", name);
        foreach (var (id, pos) in offsets)
        {
            w.WriteStartElement("offset");
            w.WriteAttributeString("idRef", id);
            w.WriteString(pos.ToString(CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    /// <summary>
    /// Records the byte offset where the next element's start tag begins — the <c>&lt;</c> char,
    /// not the preceding indent whitespace (matching pwiz C++'s indexedmzML convention).
    /// </summary>
    /// <remarks>
    /// XmlWriter buffers the indent until it emits the next element, so a plain <c>Flush</c>
    /// captures only the content up to the previous element's close. To get a position that
    /// points at the start tag, we emit the indent ourselves via <see cref="XmlWriter.WriteWhitespace"/>,
    /// flush it, record the position, and let <see cref="XmlWriter.WriteStartElement(string)"/>
    /// emit the tag body without re-indenting (XmlWriter only re-indents when its last action was
    /// closing an element, not when whitespace was written).
    /// </remarks>
    private void RecordOffset(XmlWriter w, string id, List<(string Id, long Offset)>? offsets, int indentDepth)
    {
        if (offsets is null || _stream is null) return;
        // Write newline + indent to match the surrounding pretty-print, then Flush so the position
        // we read is exactly at the '<' of the upcoming start tag.
        w.WriteWhitespace("\r\n" + new string(' ', indentDepth * 2));
        w.Flush();
        offsets.Add((id, _stream.BytesWritten));
    }

    // ---------- cvList ----------

    private static void WriteCvList(XmlWriter w, List<CV> cvs)
    {
        w.WriteStartElement("cvList");
        MzmlXml.WriteCountAttr(w, cvs.Count);
        foreach (var cv in cvs)
        {
            w.WriteStartElement("cv");
            w.WriteAttributeString("id", cv.Id);
            w.WriteAttributeString("fullName", cv.FullName);
            w.WriteAttributeString("version", cv.Version);
            w.WriteAttributeString("URI", cv.Uri);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    // ---------- fileDescription ----------

    private static void WriteFileDescription(XmlWriter w, FileDescription fd)
    {
        w.WriteStartElement("fileDescription");

        w.WriteStartElement("fileContent");
        MzmlXml.WriteParams(w, fd.FileContent);
        w.WriteEndElement();

        if (fd.SourceFiles.Count > 0)
        {
            w.WriteStartElement("sourceFileList");
            MzmlXml.WriteCountAttr(w, fd.SourceFiles.Count);
            foreach (var sf in fd.SourceFiles)
            {
                w.WriteStartElement("sourceFile");
                w.WriteAttributeString("id", sf.Id);
                w.WriteAttributeString("name", sf.Name);
                w.WriteAttributeString("location", sf.Location);
                MzmlXml.WriteParams(w, sf);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        foreach (var c in fd.Contacts)
        {
            w.WriteStartElement("contact");
            MzmlXml.WriteParams(w, c);
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    // ---------- referenceableParamGroupList ----------

    private static void WriteReferenceableParamGroupList(XmlWriter w, List<ParamGroup> groups)
    {
        if (groups.Count == 0) return;
        w.WriteStartElement("referenceableParamGroupList");
        MzmlXml.WriteCountAttr(w, groups.Count);
        foreach (var g in groups)
        {
            w.WriteStartElement("referenceableParamGroup");
            w.WriteAttributeString("id", g.Id);
            MzmlXml.WriteParams(w, g);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    // ---------- sampleList ----------

    private static void WriteSampleList(XmlWriter w, List<Sample> samples)
    {
        if (samples.Count == 0) return;
        w.WriteStartElement("sampleList");
        MzmlXml.WriteCountAttr(w, samples.Count);
        foreach (var s in samples)
        {
            w.WriteStartElement("sample");
            w.WriteAttributeString("id", s.Id);
            if (!string.IsNullOrEmpty(s.Name)) w.WriteAttributeString("name", s.Name);
            MzmlXml.WriteParams(w, s);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    // ---------- softwareList ----------

    private static void WriteSoftwareList(XmlWriter w, List<Software> software)
    {
        if (software.Count == 0) return;
        w.WriteStartElement("softwareList");
        MzmlXml.WriteCountAttr(w, software.Count);
        foreach (var sw in software)
        {
            w.WriteStartElement("software");
            w.WriteAttributeString("id", sw.Id);
            w.WriteAttributeString("version", sw.Version);
            MzmlXml.WriteParams(w, sw);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    // ---------- instrumentConfigurationList ----------

    private static void WriteInstrumentConfigurationList(XmlWriter w, List<InstrumentConfiguration> ics)
    {
        if (ics.Count == 0) return;
        w.WriteStartElement("instrumentConfigurationList");
        MzmlXml.WriteCountAttr(w, ics.Count);
        foreach (var ic in ics)
        {
            w.WriteStartElement("instrumentConfiguration");
            w.WriteAttributeString("id", ic.Id);
            MzmlXml.WriteParams(w, ic);

            if (ic.ComponentList.Count > 0)
            {
                w.WriteStartElement("componentList");
                MzmlXml.WriteCountAttr(w, ic.ComponentList.Count);
                foreach (var c in ic.ComponentList)
                {
                    string tag = c.Type switch
                    {
                        ComponentType.Source => "source",
                        ComponentType.Analyzer => "analyzer",
                        ComponentType.Detector => "detector",
                        _ => "component",
                    };
                    w.WriteStartElement(tag);
                    w.WriteAttributeString("order", c.Order.ToString(CultureInfo.InvariantCulture));
                    MzmlXml.WriteParams(w, c);
                    w.WriteEndElement();
                }
                w.WriteEndElement();
            }

            if (ic.Software is not null)
            {
                w.WriteStartElement("softwareRef");
                w.WriteAttributeString("ref", ic.Software.Id);
                w.WriteEndElement();
            }

            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    // ---------- dataProcessingList ----------

    private static void WriteDataProcessingList(XmlWriter w, List<DataProcessing> dps)
    {
        if (dps.Count == 0) return;
        w.WriteStartElement("dataProcessingList");
        MzmlXml.WriteCountAttr(w, dps.Count);
        foreach (var dp in dps)
        {
            w.WriteStartElement("dataProcessing");
            w.WriteAttributeString("id", dp.Id);
            foreach (var pm in dp.ProcessingMethods)
            {
                w.WriteStartElement("processingMethod");
                w.WriteAttributeString("order", pm.Order.ToString(CultureInfo.InvariantCulture));
                if (pm.Software is not null) w.WriteAttributeString("softwareRef", pm.Software.Id);
                MzmlXml.WriteParams(w, pm);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    // ---------- run ----------

    private void WriteRun(XmlWriter w, Run run)
    {
        w.WriteStartElement("run");
        w.WriteAttributeString("id", run.Id);
        _runDefaultIcId = run.DefaultInstrumentConfiguration?.Id;
        if (run.DefaultInstrumentConfiguration is not null)
            w.WriteAttributeString("defaultInstrumentConfigurationRef", run.DefaultInstrumentConfiguration.Id);
        if (run.Sample is not null) w.WriteAttributeString("sampleRef", run.Sample.Id);
        if (!string.IsNullOrEmpty(run.StartTimeStamp)) w.WriteAttributeString("startTimeStamp", run.StartTimeStamp);
        if (run.DefaultSourceFile is not null)
            w.WriteAttributeString("defaultSourceFileRef", run.DefaultSourceFile.Id);

        MzmlXml.WriteParams(w, run);

        if (run.SpectrumList is not null)
            WriteSpectrumList(w, run.SpectrumList);

        if (run.ChromatogramList is not null && run.ChromatogramList.Count > 0)
            WriteChromatogramList(w, run.ChromatogramList);

        w.WriteEndElement();
    }

    private void WriteChromatogramList(XmlWriter w, IChromatogramList list)
    {
        w.WriteStartElement("chromatogramList");
        MzmlXml.WriteCountAttr(w, list.Count);
        if (list.DataProcessing is not null)
            w.WriteAttributeString("defaultDataProcessingRef", list.DataProcessing.Id);

        for (int i = 0; i < list.Count; i++)
        {
            var chrom = list.GetChromatogram(i, getBinaryData: true);
            WriteChromatogram(w, chrom);
        }
        w.WriteEndElement();
    }

    private void WriteChromatogram(XmlWriter w, Chromatogram chrom)
    {
        RecordOffset(w, chrom.Id, _chromatogramOffsets, indentDepth: Indexed ? 4 : 3);
        w.WriteStartElement("chromatogram");
        w.WriteAttributeString("index", chrom.Index.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("id", chrom.Id);
        w.WriteAttributeString("defaultArrayLength", chrom.DefaultArrayLength.ToString(CultureInfo.InvariantCulture));
        if (chrom.DataProcessing is not null)
            w.WriteAttributeString("dataProcessingRef", chrom.DataProcessing.Id);

        MzmlXml.WriteParams(w, chrom.Params);

        if (!chrom.Precursor.IsEmpty)
            WritePrecursor(w, chrom.Precursor);

        int totalArrays = chrom.BinaryDataArrays.Count + chrom.IntegerDataArrays.Count;
        if (totalArrays > 0)
        {
            w.WriteStartElement("binaryDataArrayList");
            MzmlXml.WriteCountAttr(w, totalArrays);
            foreach (var arr in chrom.BinaryDataArrays)
                WriteBinaryDataArray(w, arr);
            foreach (var arr in chrom.IntegerDataArrays)
                WriteIntegerDataArray(w, arr);
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    private void WriteIntegerDataArray(XmlWriter w, IntegerDataArray arr)
    {
        var encoder = new BinaryDataEncoder(_encoderConfig);
        string base64 = encoder.EncodeInt64(CollectionsMarshal.AsSpan(arr.Data), out _);

        w.WriteStartElement("binaryDataArray");
        w.WriteAttributeString("arrayLength", arr.Data.Count.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("encodedLength", base64.Length.ToString(CultureInfo.InvariantCulture));
        if (arr.DataProcessing is not null) w.WriteAttributeString("dataProcessingRef", arr.DataProcessing.Id);

        MzmlXml.WriteCvParam(w, new CVParam(CVID.MS_64_bit_integer));
        MzmlXml.WriteCvParam(w, new CVParam(_encoderConfig.Compression switch
        {
            BinaryCompression.Zlib => CVID.MS_zlib_compression,
            _ => CVID.MS_no_compression,
        }));
        MzmlXml.WriteParams(w, arr);

        w.WriteStartElement("binary");
        w.WriteString(base64);
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private void WriteSpectrumList(XmlWriter w, ISpectrumList list)
    {
        w.WriteStartElement("spectrumList");
        MzmlXml.WriteCountAttr(w, list.Count);
        if (list.DataProcessing is not null)
            w.WriteAttributeString("defaultDataProcessingRef", list.DataProcessing.Id);

        for (int i = 0; i < list.Count; i++)
        {
            var spec = list.GetSpectrum(i, getBinaryData: true);
            WriteSpectrum(w, spec);
        }

        w.WriteEndElement();
    }

    private void WriteSpectrum(XmlWriter w, Spectrum spec)
    {
        // Depth: indexedmzML → mzML → run → spectrumList → spectrum = 4 indents deep.
        RecordOffset(w, spec.Id, _spectrumOffsets, indentDepth: Indexed ? 4 : 3);
        w.WriteStartElement("spectrum");
        w.WriteAttributeString("index", spec.Index.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("id", spec.Id);
        if (!string.IsNullOrEmpty(spec.SpotId)) w.WriteAttributeString("spotID", spec.SpotId);
        w.WriteAttributeString("defaultArrayLength", spec.DefaultArrayLength.ToString(CultureInfo.InvariantCulture));
        if (spec.SourceFile is not null) w.WriteAttributeString("sourceFileRef", spec.SourceFile.Id);
        if (spec.DataProcessing is not null) w.WriteAttributeString("dataProcessingRef", spec.DataProcessing.Id);

        MzmlXml.WriteParams(w, spec.Params);

        if (!spec.ScanList.IsEmpty)
        {
            w.WriteStartElement("scanList");
            MzmlXml.WriteCountAttr(w, spec.ScanList.Scans.Count);
            MzmlXml.WriteParams(w, spec.ScanList);
            foreach (var scan in spec.ScanList.Scans)
            {
                w.WriteStartElement("scan");
                // Only emit the explicit instrumentConfigurationRef when it differs from the
                // run's defaultInstrumentConfigurationRef — matches pwiz C++'s serializer.
                if (scan.InstrumentConfiguration is not null &&
                    scan.InstrumentConfiguration.Id != _runDefaultIcId)
                    w.WriteAttributeString("instrumentConfigurationRef", scan.InstrumentConfiguration.Id);
                if (scan.SourceFile is not null) w.WriteAttributeString("sourceFileRef", scan.SourceFile.Id);
                if (!string.IsNullOrEmpty(scan.ExternalSpectrumId))
                    w.WriteAttributeString("externalSpectrumID", scan.ExternalSpectrumId);
                if (!string.IsNullOrEmpty(scan.SpectrumId))
                    w.WriteAttributeString("spectrumRef", scan.SpectrumId);

                MzmlXml.WriteParams(w, scan);

                if (scan.ScanWindows.Count > 0)
                {
                    w.WriteStartElement("scanWindowList");
                    MzmlXml.WriteCountAttr(w, scan.ScanWindows.Count);
                    foreach (var sw in scan.ScanWindows)
                    {
                        w.WriteStartElement("scanWindow");
                        MzmlXml.WriteParams(w, sw);
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                }

                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        if (spec.Precursors.Count > 0)
        {
            w.WriteStartElement("precursorList");
            MzmlXml.WriteCountAttr(w, spec.Precursors.Count);
            foreach (var p in spec.Precursors)
                WritePrecursor(w, p);
            w.WriteEndElement();
        }

        if (spec.BinaryDataArrays.Count > 0)
        {
            w.WriteStartElement("binaryDataArrayList");
            MzmlXml.WriteCountAttr(w, spec.BinaryDataArrays.Count);
            foreach (var arr in spec.BinaryDataArrays)
                WriteBinaryDataArray(w, arr);
            w.WriteEndElement();
        }

        w.WriteEndElement(); // spectrum
    }

    private static void WritePrecursor(XmlWriter w, Precursor p)
    {
        w.WriteStartElement("precursor");
        if (p.SourceFile is not null) w.WriteAttributeString("sourceFileRef", p.SourceFile.Id);
        if (!string.IsNullOrEmpty(p.ExternalSpectrumId))
            w.WriteAttributeString("externalSpectrumID", p.ExternalSpectrumId);
        if (!string.IsNullOrEmpty(p.SpectrumId))
            w.WriteAttributeString("spectrumRef", p.SpectrumId);

        w.WriteStartElement("isolationWindow");
        MzmlXml.WriteParams(w, p.IsolationWindow);
        w.WriteEndElement();

        if (p.SelectedIons.Count > 0)
        {
            w.WriteStartElement("selectedIonList");
            MzmlXml.WriteCountAttr(w, p.SelectedIons.Count);
            foreach (var si in p.SelectedIons)
            {
                w.WriteStartElement("selectedIon");
                MzmlXml.WriteParams(w, si);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        w.WriteStartElement("activation");
        MzmlXml.WriteParams(w, p.Activation);
        w.WriteEndElement();

        w.WriteEndElement();
    }

    private void WriteBinaryDataArray(XmlWriter w, BinaryDataArray arr)
    {
        // Resolve per-array config using the Numpress/Compression/Precision overrides (keyed by array type).
        var cfg = ResolvePerArrayConfig(_encoderConfig, arr);
        var encoder = new BinaryDataEncoder(cfg);
        string base64 = encoder.Encode(CollectionsMarshal.AsSpan(arr.Data), out _, out var actualNumpress);

        // If numpress was dropped due to error tolerance, reflect that in the CV params.
        if (actualNumpress != cfg.Numpress)
        {
            cfg = cfg.Clone();
            cfg.Numpress = actualNumpress;
        }

        w.WriteStartElement("binaryDataArray");
        // mzML's encodedLength = the length of the base64 string, NOT the raw/compressed byte count.
        // Readers (including pwiz's msdiff) use this to slice the base64 blob out of the XML,
        // so a mismatch makes decoding fail at that spectrum.
        w.WriteAttributeString("encodedLength", base64.Length.ToString(CultureInfo.InvariantCulture));
        if (arr.DataProcessing is not null) w.WriteAttributeString("dataProcessingRef", arr.DataProcessing.Id);

        // Emit precision / compression / array-type CV params before user-added ones,
        // matching pwiz's canonical ordering so byte-level diffs line up.
        EmitEncodingCvParams(w, cfg);
        MzmlXml.WriteParams(w, arr);

        w.WriteStartElement("binary");
        w.WriteString(base64);
        w.WriteEndElement();
        w.WriteEndElement();
    }

    /// <summary>
    /// Builds a per-array <see cref="BinaryEncoderConfig"/> by applying the Numpress/Compression/
    /// Precision overrides for the CV term that identifies <paramref name="arr"/>.
    /// </summary>
    private static BinaryEncoderConfig ResolvePerArrayConfig(BinaryEncoderConfig global, BinaryDataArray arr)
    {
        CVID typeCv = ArrayTypeCvid(arr);
        if (typeCv == CVID.CVID_Unknown) return global;

        bool hasNp = global.NumpressOverrides.TryGetValue(typeCv, out var np);
        bool hasComp = global.CompressionOverrides.TryGetValue(typeCv, out var comp);
        bool hasPrec = global.PrecisionOverrides.TryGetValue(typeCv, out var prec);
        if (!hasNp && !hasComp && !hasPrec) return global;

        var cfg = global.Clone();
        if (hasNp) cfg.Numpress = np;
        if (hasComp) cfg.Compression = comp;
        if (hasPrec) cfg.Precision = prec;
        return cfg;
    }

    private static CVID ArrayTypeCvid(BinaryDataArray arr)
    {
        foreach (var cv in arr.CVParams)
        {
            // The "array type" cvParam is the one that is_a MS_binary_data_array (m/z, intensity, time, etc.).
            if (cv.Cvid == CVID.MS_m_z_array
                || cv.Cvid == CVID.MS_intensity_array
                || cv.Cvid == CVID.MS_time_array
                || cv.Cvid == CVID.MS_wavelength_array
                || cv.Cvid == CVID.MS_non_standard_data_array
                || CvLookup.CvIsA(cv.Cvid, CVID.MS_binary_data_array))
                return cv.Cvid;
        }
        return CVID.CVID_Unknown;
    }

    private static void EmitEncodingCvParams(XmlWriter w, BinaryEncoderConfig cfg)
    {
        // Precision CV is always emitted — for numpress arrays it describes the semantic precision
        // of the decoded array (pwiz C++ writes 32-bit float alongside the numpress CV).
        MzmlXml.WriteCvParam(w, new CVParam(
            cfg.Precision == BinaryPrecision.Bits64 ? CVID.MS_64_bit_float : CVID.MS_32_bit_float));

        if (cfg.Numpress == BinaryNumpress.None)
        {
            MzmlXml.WriteCvParam(w, new CVParam(cfg.Compression switch
            {
                BinaryCompression.Zlib => CVID.MS_zlib_compression,
                _ => CVID.MS_no_compression,
            }));
            return;
        }

        // Numpress: single CV term also encodes whether zlib is layered on top. When zlib is
        // stacked we use the "followed_by_zlib" variant instead of emitting a separate zlib CV.
        bool zlib = cfg.Compression == BinaryCompression.Zlib;
        CVID numpressCv = (cfg.Numpress, zlib) switch
        {
            (BinaryNumpress.Linear, false) => CVID.MS_MS_Numpress_linear_prediction_compression,
            (BinaryNumpress.Linear, true)  => CVID.MS_MS_Numpress_linear_prediction_compression_followed_by_zlib_compression,
            (BinaryNumpress.Pic,    false) => CVID.MS_MS_Numpress_positive_integer_compression,
            (BinaryNumpress.Pic,    true)  => CVID.MS_MS_Numpress_positive_integer_compression_followed_by_zlib_compression,
            (BinaryNumpress.Slof,   false) => CVID.MS_MS_Numpress_short_logged_float_compression,
            (BinaryNumpress.Slof,   true)  => CVID.MS_MS_Numpress_short_logged_float_compression_followed_by_zlib_compression,
            _ => throw new InvalidOperationException($"Unsupported numpress combo: {cfg.Numpress}, zlib={zlib}"),
        };
        MzmlXml.WriteCvParam(w, new CVParam(numpressCv));
    }
}
