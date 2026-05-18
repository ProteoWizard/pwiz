using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.MzMlb;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Samples;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

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

    /// <summary>When true (and <see cref="Indexed"/> is false), capture each spectrum's
    /// byte position + id but DO NOT wrap the output in an <c>&lt;indexedmzML&gt;</c>
    /// envelope. Used by <c>MzMlbWriter</c>, which stores the offsets in separate HDF5
    /// datasets (<c>mzML_spectrumIndex</c> / <c>mzML_spectrumIndex_idRef</c>) instead —
    /// matching cpp's <c>Serializer_mzML.cpp</c> mzMLb-detection path. After the call to
    /// <see cref="Write(MSData, Stream)"/> returns, the captures are exposed via
    /// <see cref="CapturedSpectrumOffsets"/> and <see cref="CapturedEndOfSpectrumList"/>.</summary>
    public bool TrackSpectrumOffsets { get; set; }

    /// <summary>Per-spectrum (id, byte-position) pairs captured when
    /// <see cref="TrackSpectrumOffsets"/> is on, valid only between the last
    /// <see cref="Write(MSData, Stream)"/> call's return and the next one's entry.
    /// Empty when no tracking was requested or no spectra were written.</summary>
    public IReadOnlyList<(string Id, long Offset)> CapturedSpectrumOffsets =>
        _capturedSpectrumOffsets ?? (IReadOnlyList<(string, long)>)System.Array.Empty<(string, long)>();

    /// <summary>Byte position right after <c>&lt;/spectrumList&gt;</c> closed, captured
    /// in the same conditions as <see cref="CapturedSpectrumOffsets"/>. Use as the
    /// "end of spectrum list" anchor when writing the mzMLb HDF5 index dataset.</summary>
    public long CapturedEndOfSpectrumList { get; private set; }

    private List<(string Id, long Offset)>? _capturedSpectrumOffsets;

    /// <summary>Optional listener registry that receives <see cref="IterationUpdate"/> messages
    /// once per spectrum during the spectrumList write loop. Drives msconvert's <c>-v</c>
    /// progress output and any future progress UI.</summary>
    public IterationListenerRegistry? IterationListenerRegistry { get; set; }

    /// <summary>
    /// Optional sink for external binary array data. When set, the writer
    /// routes each <c>BinaryDataArray</c> / <c>IntegerDataArray</c> through
    /// the sink (which stores the values in named HDF5 datasets) and emits
    /// <c>external_HDF5_dataset</c> + <c>external_offset</c> +
    /// <c>external_array_length</c> cvParams in place of inline base64. Used
    /// by <c>Serializer_mzMLb</c>; null for plain mzML.
    /// </summary>
    public IExternalBinarySink? ExternalBinarySink { get; set; }

    // "spectrum_" / "chromatogram_" — used as a dataset-name prefix when
    // ExternalBinarySink is set. Toggled by WriteSpectrumList /
    // WriteChromatogramList around their per-array call sites so
    // WriteBinaryDataArray / WriteIntegerDataArray know which context they're in.
    private string _externalArrayContextPrefix = "spectrum_";

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

        // Clear any leftover captures from a previous Write call.
        _capturedSpectrumOffsets = null;
        CapturedEndOfSpectrumList = 0;

        if (!Indexed && !TrackSpectrumOffsets)
        {
            using var w = CreateWriter(stream);
            WriteMzmlDocument(msd, w);
            return;
        }

        // Either indexed mode (full <indexedmzML> envelope + fileChecksum) or mzMLb's
        // track-offsets-only mode. Both need byte-position tracking, so they share the
        // HashingCountingStream wrapper — but the offsets-only path leaves the document
        // as plain <mzML> and exposes the captures via CapturedSpectrumOffsets so
        // MzMlbWriter can store them in HDF5 datasets.
        _stream = new HashingCountingStream(stream);
        if (!Indexed) _stream.StopHashing(); // no checksum in offsets-only mode
        _spectrumOffsets = new List<(string, long)>();
        _chromatogramOffsets = new List<(string, long)>();
        try
        {
            using var w = CreateWriter(_stream);
            if (Indexed)
            {
                WriteIndexedEnvelope(msd, w);
            }
            else
            {
                // Plain <mzML> document, but with spectrum-offset capture enabled inside.
                WriteMzmlDocument(msd, w);
                _capturedSpectrumOffsets = _spectrumOffsets;
            }
        }
        finally
        {
            _stream.Dispose();
            _stream = null;
            // _capturedSpectrumOffsets stays set on the instance until the next Write.
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
        // cpp IO.cpp:3383 emits msd.id raw with the comment "not an XML:ID" — the mzML element
        // id is a free-form document identifier per spec, not constrained to NCName, and cpp's
        // reader at IO.cpp:3442 likewise skips decode_xml_id. Encoding here would make our
        // output diverge from cpp msconvert on every input where the run name starts with a digit.
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
            "http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML1.1.2_idx.xsd");

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
            // Index idRef references the spectrum/chromatogram element's raw id (which cpp
            // also writes raw — see WriteSpectrum / WriteChromatogram). Encoding it here
            // would break the indexedmzML guarantee that idRef byte-for-byte matches the
            // target element's id attribute.
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
    /// Arms the underlying counting stream to capture the byte position of the next
    /// <c>&lt;</c> character. Call this immediately before <see cref="XmlWriter.WriteStartElement(string)"/>
    /// of a spectrum or chromatogram; once the start tag has been flushed, call
    /// <see cref="CaptureOffset"/> to retrieve the position.
    /// </summary>
    /// <remarks>
    /// The earlier implementation injected the indent whitespace manually via
    /// <see cref="XmlWriter.WriteWhitespace(string?)"/> so it could Flush + read
    /// <c>BytesWritten</c> at the tag boundary. That worked for the byte position, but it put
    /// XmlWriter into mixed-content mode at the spectrumList / chromatogramList level, which
    /// cascades to suppress indentation for every child of every spectrum (cvParam, scanList,
    /// binaryDataArray, …). The fix is to let XmlWriter do its own indent + start-tag emission
    /// and observe the <c>&lt;</c> byte position via the stream wrapper instead.
    /// </remarks>
    private void ArmOffsetCapture(XmlWriter w)
    {
        if (_stream is null) return;
        w.Flush(); // commit any bytes buffered from prior elements so they don't pollute the search
        _stream.ResetLastLt();
    }

    /// <summary>Records the byte position of the first <c>&lt;</c> written since the most recent
    /// <see cref="ArmOffsetCapture"/>. Call after the element (and all its children) have been
    /// written — the start tag is guaranteed to be in the stream by then.</summary>
    private void CaptureOffset(XmlWriter w, string id, List<(string Id, long Offset)>? offsets)
    {
        if (offsets is null || _stream is null) return;
        w.Flush();
        if (_stream.LastLtPos < 0) return;
        offsets.Add((id, _stream.LastLtPos));
    }

    // ---------- cvList ----------

    private static void WriteCvList(XmlWriter w, List<CV> cvs)
    {
        w.WriteStartElement("cvList");
        MzmlXml.WriteCountAttr(w, cvs.Count);
        foreach (var cv in cvs)
        {
            w.WriteStartElement("cv");
            w.WriteAttributeString("id",XmlIdEncoding.Encode(cv.Id));
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
                w.WriteAttributeString("id",XmlIdEncoding.Encode(sf.Id));
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
            w.WriteAttributeString("id",XmlIdEncoding.Encode(g.Id));
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
            w.WriteAttributeString("id",XmlIdEncoding.Encode(s.Id));
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
            w.WriteAttributeString("id",XmlIdEncoding.Encode(sw.Id));
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
            w.WriteAttributeString("id",XmlIdEncoding.Encode(ic.Id));
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
                w.WriteAttributeString("ref", XmlIdEncoding.Encode(ic.Software.Id));
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
            w.WriteAttributeString("id",XmlIdEncoding.Encode(dp.Id));
            foreach (var pm in dp.ProcessingMethods)
            {
                w.WriteStartElement("processingMethod");
                w.WriteAttributeString("order", pm.Order.ToString(CultureInfo.InvariantCulture));
                if (pm.Software is not null) w.WriteAttributeString("softwareRef", XmlIdEncoding.Encode(pm.Software.Id));
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
        w.WriteAttributeString("id",XmlIdEncoding.Encode(run.Id));
        _runDefaultIcId = run.DefaultInstrumentConfiguration?.Id;
        if (run.DefaultInstrumentConfiguration is not null)
            w.WriteAttributeString("defaultInstrumentConfigurationRef", XmlIdEncoding.Encode(run.DefaultInstrumentConfiguration.Id));
        if (run.Sample is not null) w.WriteAttributeString("sampleRef", XmlIdEncoding.Encode(run.Sample.Id));
        if (!string.IsNullOrEmpty(run.StartTimeStamp)) w.WriteAttributeString("startTimeStamp", run.StartTimeStamp);
        if (run.DefaultSourceFile is not null)
            w.WriteAttributeString("defaultSourceFileRef", XmlIdEncoding.Encode(run.DefaultSourceFile.Id));

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
            w.WriteAttributeString("defaultDataProcessingRef", XmlIdEncoding.Encode(list.DataProcessing.Id));

        for (int i = 0; i < list.Count; i++)
        {
            var chrom = list.GetChromatogram(i, getBinaryData: true);
            WriteChromatogram(w, chrom);
        }
        w.WriteEndElement();
    }

    private void WriteChromatogram(XmlWriter w, Chromatogram chrom)
    {
        // Chromatogram binary arrays sit in datasets prefixed "chromatogram_"
        // (vs. "spectrum_" for spectra) when an mzMLb sink is wired up.
        // Toggle in/out so nested spectrum-time-array writes don't accidentally
        // pick the wrong prefix.
        string previousPrefix = _externalArrayContextPrefix;
        _externalArrayContextPrefix = "chromatogram_";
        try
        {
            WriteChromatogramInner(w, chrom);
        }
        finally
        {
            _externalArrayContextPrefix = previousPrefix;
        }
    }

    private void WriteChromatogramInner(XmlWriter w, Chromatogram chrom)
    {
        ArmOffsetCapture(w);
        w.WriteStartElement("chromatogram");
        w.WriteAttributeString("index", chrom.Index.ToString(CultureInfo.InvariantCulture));
        // cpp IO.cpp:2833 emits chromatogram.id raw ("not an XML:ID"); reader IO.cpp doesn't
        // decode it either. Chromatogram ids are free-form ("TIC", "- SRM SIC Q1=309.0 ...").
        w.WriteAttributeString("id", chrom.Id);
        w.WriteAttributeString("defaultArrayLength", chrom.DefaultArrayLength.ToString(CultureInfo.InvariantCulture));
        if (chrom.DataProcessing is not null)
            w.WriteAttributeString("dataProcessingRef", XmlIdEncoding.Encode(chrom.DataProcessing.Id));

        MzmlXml.WriteParams(w, chrom.Params);

        if (!chrom.Precursor.IsEmpty)
            WritePrecursor(w, chrom.Precursor);

        if (!chrom.Product.IsEmpty)
            WriteProduct(w, chrom.Product);

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
        CaptureOffset(w, chrom.Id, _chromatogramOffsets);
    }

    private void WriteIntegerDataArray(XmlWriter w, IntegerDataArray arr)
    {
        if (ExternalBinarySink is not null)
        {
            WriteIntegerDataArrayMzMlb(w, arr);
            return;
        }

        var encoder = new BinaryDataEncoder(_encoderConfig);
        string base64 = encoder.EncodeInt64(CollectionsMarshal.AsSpan(arr.Data), out _);

        w.WriteStartElement("binaryDataArray");
        w.WriteAttributeString("arrayLength", arr.Data.Count.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("encodedLength", base64.Length.ToString(CultureInfo.InvariantCulture));
        if (arr.DataProcessing is not null) w.WriteAttributeString("dataProcessingRef", XmlIdEncoding.Encode(arr.DataProcessing.Id));

        MzmlXml.WriteCvParam(w, new CVParam(CVID.MS_64_bit_integer));
        MzmlXml.WriteCvParam(w, new CVParam(_encoderConfig.Compression switch
        {
            BinaryCompression.Zlib => CVID.MS_zlib_compression,
            _ => CVID.MS_no_compression,
        }));
        WriteArrayParamsExcludingEncoding(w, arr);

        w.WriteStartElement("binary");
        w.WriteString(base64);
        w.WriteEndElement();
        w.WriteEndElement();
    }

    /// <summary>
    /// mzMLb variant of <see cref="WriteIntegerDataArray"/>. Dispatches on the
    /// per-array precision config: 64-bit -&gt; <c>AppendInt64</c> into a
    /// "_int64"-suffixed dataset, 32-bit -&gt; narrow long[] to int[],
    /// <c>AppendInt32</c> into a "_int32"-suffixed dataset (cpp IO.cpp's
    /// writeMzMLbExtra&lt;IntegerDataArray&gt;). cpp doesn't define
    /// numpress for integer arrays — we mirror that.
    /// </summary>
    private void WriteIntegerDataArrayMzMlb(XmlWriter w, IntegerDataArray arr)
    {
        var globalCfg = _encoderConfig;
        // IntegerDataArray doesn't carry a binary-array CVID per se, so the
        // PrecisionOverrides keyed by m/z / intensity won't apply; honor the
        // global Precision setting only.
        bool use32Bit = globalCfg.Precision == BinaryPrecision.Bits32;

        CVID typeCv = ArrayTypeCvidInt(arr);
        string suffix = use32Bit ? "_int32" : "_int64";
        string dataset = $"{_externalArrayContextPrefix}MS_{(int)typeCv}{suffix}";
        var data = CollectionsMarshal.AsSpan(arr.Data);
        long offset;
        if (use32Bit)
        {
            var int32s = new int[data.Length];
            for (int i = 0; i < data.Length; i++) int32s[i] = (int)data[i];
            offset = ExternalBinarySink!.AppendInt32(dataset, int32s);
        }
        else
        {
            offset = ExternalBinarySink!.AppendInt64(dataset, data);
        }

        w.WriteStartElement("binaryDataArray");
        w.WriteAttributeString("arrayLength", arr.Data.Count.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("encodedLength", "0");
        if (arr.DataProcessing is not null) w.WriteAttributeString("dataProcessingRef", XmlIdEncoding.Encode(arr.DataProcessing.Id));

        // cpp parity (mzMLb): emit a single precision cvParam matching the HDF5 dataset's
        // native type. The source array can carry a stale precision cvParam from upstream
        // (e.g. a TIC "ms level" array tagged 32-bit in the input mzML even though our
        // sink writes int64) — emit the writer's chosen precision and filter the input's
        // out of arr.CVParams to avoid a binaryDataArray with conflicting precision tags.
        // Debatable: the precision cvParam is arguably redundant in mzMLb mode because the
        // HDF5 dataset already declares its native type, but cpp Serializer_mzML emits it
        // for schema compliance and we follow suit.
        MzmlXml.WriteCvParam(w, new CVParam(use32Bit ? CVID.MS_32_bit_integer : CVID.MS_64_bit_integer));
        MzmlXml.WriteCvParam(w, new CVParam(CVID.MS_no_compression));
        WriteArrayParamsExcludingEncoding(w, arr);
        MzmlXml.WriteCvParam(w, new CVParam(CVID.MS_external_HDF5_dataset, dataset));
        MzmlXml.WriteCvParam(w, new CVParam(CVID.MS_external_offset, offset));
        MzmlXml.WriteCvParam(w, new CVParam(CVID.MS_external_array_length, arr.Data.Count));

        // Empty <binary/> element keeps mzML schema compliance — pwiz cpp does
        // the same. Readers see the external-dataset cvParams and skip base64
        // decoding.
        w.WriteStartElement("binary");
        w.WriteEndElement();
        w.WriteEndElement();
    }

    /// <summary>IntegerDataArray-specific equivalent of <see cref="ArrayTypeCvid"/>.
    /// Identifies which "is_a MS_binary_data_array" child the array carries
    /// (typically <c>MS_charge_array</c> in centroid spectra). Falls back to
    /// <c>MS_non_standard_data_array</c>'s child if no known accession is
    /// present.</summary>
    private static CVID ArrayTypeCvidInt(IntegerDataArray arr)
    {
        foreach (var cv in arr.CVParams)
        {
            if (CvLookup.CvIsA(cv.Cvid, CVID.MS_binary_data_array))
                return cv.Cvid;
        }
        return CVID.CVID_Unknown;
    }

    private void WriteSpectrumList(XmlWriter w, ISpectrumList list)
    {
        w.WriteStartElement("spectrumList");
        MzmlXml.WriteCountAttr(w, list.Count);
        if (list.DataProcessing is not null)
            w.WriteAttributeString("defaultDataProcessingRef", XmlIdEncoding.Encode(list.DataProcessing.Id));

        int count = list.Count;
        var registry = IterationListenerRegistry;
        for (int i = 0; i < count; i++)
        {
            var spec = list.GetSpectrum(i, getBinaryData: true);
            WriteSpectrum(w, spec);

            // Broadcast progress before moving on. Throttling is handled by the registry's
            // per-listener period/timer settings; we just push every spectrum.
            registry?.Broadcast(new IterationUpdate(i, count, "writing spectra"));
        }

        w.WriteEndElement();

        // Record the byte position just after </spectrumList>, used by mzMLb's HDF5 index
        // as the "end of spectrumList" anchor (caller seeks here and byte-scans forward
        // for <chromatogramList> in the lazy read path).
        if (_stream is not null && TrackSpectrumOffsets)
        {
            w.Flush();
            CapturedEndOfSpectrumList = _stream.BytesWritten;
        }
    }

    private void WriteSpectrum(XmlWriter w, Spectrum spec)
    {
        ArmOffsetCapture(w);
        w.WriteStartElement("spectrum");
        w.WriteAttributeString("index", spec.Index.ToString(CultureInfo.InvariantCulture));
        // cpp IO.cpp:2611 emits spectrum.id raw ("not an XML:ID"); reader IO.cpp:2716 skips
        // decode_xml_id. Vendor scan IDs (e.g. "scan=1", "merged=0 frame=0", "controllerType=0
        // controllerNumber=1 scan=1") are free-form strings, not constrained to NCName.
        w.WriteAttributeString("id", spec.Id);
        if (!string.IsNullOrEmpty(spec.SpotId)) w.WriteAttributeString("spotID", spec.SpotId);
        w.WriteAttributeString("defaultArrayLength", spec.DefaultArrayLength.ToString(CultureInfo.InvariantCulture));
        if (spec.SourceFile is not null) w.WriteAttributeString("sourceFileRef", XmlIdEncoding.Encode(spec.SourceFile.Id));
        if (spec.DataProcessing is not null) w.WriteAttributeString("dataProcessingRef", XmlIdEncoding.Encode(spec.DataProcessing.Id));

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
                    w.WriteAttributeString("instrumentConfigurationRef", XmlIdEncoding.Encode(scan.InstrumentConfiguration.Id));
                if (scan.SourceFile is not null) w.WriteAttributeString("sourceFileRef", XmlIdEncoding.Encode(scan.SourceFile.Id));
                if (!string.IsNullOrEmpty(scan.ExternalSpectrumId))
                    w.WriteAttributeString("externalSpectrumID", scan.ExternalSpectrumId);
                if (!string.IsNullOrEmpty(scan.SpectrumId))
                    // cpp IO.cpp:1431 emits scan.spectrumRef raw ("not an XML:IDREF"). Same
                    // story as precursor.spectrumRef: it points at a <spectrum id=...> that
                    // pwiz also writes raw.
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

        if (spec.Products.Count > 0)
        {
            w.WriteStartElement("productList");
            MzmlXml.WriteCountAttr(w, spec.Products.Count);
            foreach (var p in spec.Products)
                WriteProduct(w, p);
            w.WriteEndElement();
        }

        int totalArrays = spec.BinaryDataArrays.Count + spec.IntegerDataArrays.Count;
        if (totalArrays > 0)
        {
            w.WriteStartElement("binaryDataArrayList");
            MzmlXml.WriteCountAttr(w, totalArrays);
            foreach (var arr in spec.BinaryDataArrays)
                WriteBinaryDataArray(w, arr);
            foreach (var arr in spec.IntegerDataArrays)
                WriteIntegerDataArray(w, arr);
            w.WriteEndElement();
        }

        w.WriteEndElement(); // spectrum
        CaptureOffset(w, spec.Id, _spectrumOffsets);
    }

    private static void WritePrecursor(XmlWriter w, Precursor p)
    {
        w.WriteStartElement("precursor");
        if (p.SourceFile is not null) w.WriteAttributeString("sourceFileRef", XmlIdEncoding.Encode(p.SourceFile.Id));
        if (!string.IsNullOrEmpty(p.ExternalSpectrumId))
            w.WriteAttributeString("externalSpectrumID", p.ExternalSpectrumId);
        if (!string.IsNullOrEmpty(p.SpectrumId))
            // cpp emits the precursor.spectrumRef raw (matches the corresponding
            // <spectrum id=...> attribute, which is also written raw — pwiz treats spectrum
            // ids as free-form strings even though that violates XML's ID type spec). Don't
            // run XmlIdEncoding here: it would expand "=" and " " into _x003d_/_x0020_ and
            // make the ref unresolvable by msdiff and by every cpp pwiz reader.
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

    private static void WriteProduct(XmlWriter w, Product p)
    {
        w.WriteStartElement("product");
        w.WriteStartElement("isolationWindow");
        MzmlXml.WriteParams(w, p.IsolationWindow);
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private void WriteBinaryDataArray(XmlWriter w, BinaryDataArray arr)
    {
        if (ExternalBinarySink is not null)
        {
            WriteBinaryDataArrayMzMlb(w, arr);
            return;
        }

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
        if (arr.DataProcessing is not null) w.WriteAttributeString("dataProcessingRef", XmlIdEncoding.Encode(arr.DataProcessing.Id));

        // Emit precision / compression / array-type CV params before user-added ones,
        // matching pwiz's canonical ordering so byte-level diffs line up.
        // EmitEncodingCvParams handles precision + compression + numpress; the array's other
        // params (m/z array, intensity array, units, ...) come from MzmlXml.WriteParams. We
        // FILTER the encoding-related ones out of arr.CVParams so we don't double-emit them
        // (e.g. an m/z array round-tripped from a 64-bit input would pick up its source
        // "64-bit float" CV plus the writer's chosen "32-bit float" — and a downstream reader
        // would then decode using the wrong precision and report 2× the peak count).
        EmitEncodingCvParams(w, cfg);
        WriteArrayParamsExcludingEncoding(w, arr);

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
        bool hasTrunc = global.TruncationOverrides.TryGetValue(typeCv, out var trunc);
        bool hasPred = global.PredictionOverrides.TryGetValue(typeCv, out var pred);
        if (!hasNp && !hasComp && !hasPrec && !hasTrunc && !hasPred) return global;

        var cfg = global.Clone();
        if (hasNp) cfg.Numpress = np;
        if (hasComp) cfg.Compression = comp;
        if (hasPrec) cfg.Precision = prec;
        if (hasTrunc) cfg.Truncation = trunc;
        if (hasPred) cfg.Prediction = pred;
        return cfg;
    }

    /// <summary>
    /// Applies mantissa-truncation + delta/linear prediction to <paramref name="data"/> in
    /// place, matching cpp <c>writeMzMLbExtra</c> (IO.cpp:1660-1820). 32-bit floats and 64-bit
    /// doubles take separate paths because the bit-mask width differs.
    /// </summary>
    /// <remarks>
    /// Truncation modes:
    ///   <c>Truncation == 0</c> → no-op
    ///   <c>Truncation &gt; 0</c> → zero out the bottom N bits of each mantissa
    ///   <c>Truncation == -1</c> → round each value to the nearest integer
    /// Prediction modes:
    ///   <c>Delta</c>     → each value becomes <c>v[i] - v[i-1]</c> (sample 0 unchanged)
    ///   <c>Linear</c>    → each value becomes <c>v[i] - (2*v[i-1] - v[i-2])</c> (samples 0-1 unchanged)
    /// </remarks>
    private static void ApplyTruncationAndPredictionDouble(double[] data, BinaryEncoderConfig cfg)
    {
        if (cfg.Truncation > 0)
        {
            ulong bitmask = ~(((ulong)1 << cfg.Truncation) - 1);
            for (int i = 0; i < data.Length; i++)
                data[i] = System.BitConverter.UInt64BitsToDouble(
                    System.BitConverter.DoubleToUInt64Bits(data[i]) & bitmask);
        }
        else if (cfg.Truncation == -1)
        {
            for (int i = 0; i < data.Length; i++) data[i] = System.Math.Round(data[i]);
        }

        if (cfg.Prediction == BinaryPrediction.Delta && data.Length > 0)
        {
            // Capture each original before overwriting, since the next iteration's residual
            // depends on the previous original (the loop tracks the decoded running value,
            // which equals the original by construction). cpp computes the same value via
            // algebraic update of `previous` using the just-encoded data[i]; this form is
            // mathematically equivalent and simpler.
            double prev = data[0];
            for (int i = 1; i < data.Length; i++)
            {
                double original = data[i];
                data[i] = data[0] + original - prev;
                prev = original;
            }
        }
        else if (cfg.Prediction == BinaryPrediction.Linear && data.Length > 1)
        {
            double prev2 = data[0];
            double prev1 = data[1];
            for (int i = 2; i < data.Length; i++)
            {
                double original = data[i];
                data[i] = data[1] + original - 2.0 * prev1 + prev2;
                prev2 = prev1;
                prev1 = original;
            }
        }
    }

    private static void ApplyTruncationAndPredictionFloat(float[] data, BinaryEncoderConfig cfg)
    {
        if (cfg.Truncation > 0)
        {
            uint bitmask = ~(((uint)1 << cfg.Truncation) - 1);
            for (int i = 0; i < data.Length; i++)
                data[i] = System.BitConverter.UInt32BitsToSingle(
                    System.BitConverter.SingleToUInt32Bits(data[i]) & bitmask);
        }
        else if (cfg.Truncation == -1)
        {
            for (int i = 0; i < data.Length; i++) data[i] = (float)System.Math.Round(data[i]);
        }

        if (cfg.Prediction == BinaryPrediction.Delta && data.Length > 0)
        {
            float prev = data[0];
            for (int i = 1; i < data.Length; i++)
            {
                float original = data[i];
                data[i] = data[0] + original - prev;
                prev = original;
            }
        }
        else if (cfg.Prediction == BinaryPrediction.Linear && data.Length > 1)
        {
            float prev2 = data[0];
            float prev1 = data[1];
            for (int i = 2; i < data.Length; i++)
            {
                float original = data[i];
                data[i] = data[1] + original - 2.0f * prev1 + prev2;
                prev2 = prev1;
                prev1 = original;
            }
        }
    }

    /// <summary>
    /// mzMLb variant of <see cref="WriteBinaryDataArray"/>. Appends the array's
    /// values to a per-array-type HDF5 dataset via <see cref="ExternalBinarySink"/>
    /// and emits external-dataset cvParams in place of inline base64.
    /// Dispatches on the per-array config selected by <see cref="ResolvePerArrayConfig"/>:
    /// <list type="bullet">
    ///   <item>Numpress -&gt; encode to raw bytes (zlib applied if configured),
    ///         <c>AppendBytes</c> into a "_numpress_linear|_pic|_slof"-suffixed
    ///         dataset; reader pulls bytes back and decodes via BinaryDataEncoder.</item>
    ///   <item>64-bit -&gt; <c>AppendDoubles</c> into "_double"-suffixed dataset.</item>
    ///   <item>32-bit -&gt; narrow to float[], <c>AppendFloats</c> into
    ///         "_float"-suffixed dataset.</item>
    /// </list>
    /// Matches cpp IO.cpp:1623-1822.
    /// </summary>
    private void WriteBinaryDataArrayMzMlb(XmlWriter w, BinaryDataArray arr)
    {
        var cfg = ResolvePerArrayConfig(_encoderConfig, arr);
        CVID typeCv = ArrayTypeCvid(arr);
        var data = CollectionsMarshal.AsSpan(arr.Data);

        string suffix;
        long offset;
        int encodedLength;
        BinaryNumpress actualNumpress = cfg.Numpress;

        if (cfg.Numpress != BinaryNumpress.None)
        {
            // Encode to raw bytes (post-zlib) and store as an opaque dataset.
            // The encoder may downgrade to None if tolerance fails; that's
            // reflected in actualNumpress so the cvParam tells the truth.
            byte[] bytes = new BinaryDataEncoder(cfg).EncodeToBytes(data, out actualNumpress);
            if (actualNumpress != BinaryNumpress.None)
            {
                suffix = actualNumpress switch
                {
                    BinaryNumpress.Linear => "_numpress_linear",
                    BinaryNumpress.Pic    => "_numpress_pic",
                    BinaryNumpress.Slof   => "_numpress_slof",
                    _                     => "_numpress_linear", // unreachable
                };
                string dsName = $"{_externalArrayContextPrefix}MS_{(int)typeCv}{suffix}";
                offset = ExternalBinarySink!.AppendBytes(dsName, bytes);
                encodedLength = bytes.Length;
            }
            else
            {
                // Numpress was rejected (tolerance exceeded). Fall back to the regular
                // typed-dataset path so the reader can pull the array back without
                // having to know about opaque uint8 byte storage. cpp behaves the
                // same way — mzML carries no numpress cvParam, _double / _float dataset
                // has native float/double type.
                if (cfg.Precision == BinaryPrecision.Bits32)
                {
                    var floats = new float[data.Length];
                    for (int i = 0; i < data.Length; i++) floats[i] = (float)data[i];
                    suffix = "_float";
                    string dsName = $"{_externalArrayContextPrefix}MS_{(int)typeCv}{suffix}";
                    offset = ExternalBinarySink!.AppendFloats(dsName, floats);
                }
                else
                {
                    suffix = "_double";
                    string dsName = $"{_externalArrayContextPrefix}MS_{(int)typeCv}{suffix}";
                    offset = ExternalBinarySink!.AppendDoubles(dsName, data);
                }
                encodedLength = 0;
            }
        }
        else if (cfg.Precision == BinaryPrecision.Bits32)
        {
            var floats = new float[data.Length];
            for (int i = 0; i < data.Length; i++) floats[i] = (float)data[i];
            // Apply mantissa truncation + delta/linear prediction. Matches cpp
            // writeMzMLbExtra (IO.cpp:1655-1729) — applied to the float-narrowed payload
            // before HDF5 write so the prediction-residual is what hits disk.
            ApplyTruncationAndPredictionFloat(floats, cfg);
            suffix = "_float";
            string dsName = $"{_externalArrayContextPrefix}MS_{(int)typeCv}{suffix}";
            offset = ExternalBinarySink!.AppendFloats(dsName, floats);
            encodedLength = 0; // for non-numpress paths cpp emits encodedLength=0
        }
        else
        {
            // ApplyTruncationAndPrediction mutates in place; copy `data` (which is a span over
            // the user's BinaryDataArray.Data) to keep the source intact for any subsequent
            // reads on the same in-memory document.
            double[] doubles = data.ToArray();
            ApplyTruncationAndPredictionDouble(doubles, cfg);
            suffix = "_double";
            string dsName = $"{_externalArrayContextPrefix}MS_{(int)typeCv}{suffix}";
            offset = ExternalBinarySink!.AppendDoubles(dsName, doubles);
            encodedLength = 0;
        }

        string dataset = $"{_externalArrayContextPrefix}MS_{(int)typeCv}{suffix}";

        w.WriteStartElement("binaryDataArray");
        w.WriteAttributeString("encodedLength", encodedLength.ToString(CultureInfo.InvariantCulture));
        if (arr.DataProcessing is not null) w.WriteAttributeString("dataProcessingRef", XmlIdEncoding.Encode(arr.DataProcessing.Id));

        EmitEncodingCvParams(w, cfg, actualNumpress);
        WriteArrayParamsExcludingEncoding(w, arr);
        MzmlXml.WriteCvParam(w, new CVParam(CVID.MS_external_HDF5_dataset, dataset));
        MzmlXml.WriteCvParam(w, new CVParam(CVID.MS_external_offset, offset));
        MzmlXml.WriteCvParam(w, new CVParam(CVID.MS_external_array_length, arr.Data.Count));

        w.WriteStartElement("binary");
        w.WriteEndElement();
        w.WriteEndElement();
    }

    /// <summary>Local helper: emit the same precision / compression / numpress
    /// CV params <c>EmitEncodingCvParams</c> does, but with the
    /// numpress field overridden to reflect what the encoder actually used
    /// (may have downgraded from the configured value).</summary>
    private static void EmitEncodingCvParams(XmlWriter w, BinaryEncoderConfig cfg, BinaryNumpress actualNumpress)
    {
        var c = cfg.Clone();
        c.Numpress = actualNumpress;
        EmitEncodingCvParams(w, c);
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

    /// <summary>Emits all CV / user / referenceableParamGroup entries on <paramref name="arr"/>
    /// EXCEPT the precision / compression / numpress CV params — those are emitted from the
    /// resolved <see cref="BinaryEncoderConfig"/> by <c>EmitEncodingCvParams</c>. The
    /// array may carry stale encoding CVs from its source (e.g. a 64-bit float term inherited
    /// from the input mzML); writing both that and the writer's chosen precision produces an
    /// XML element with two conflicting precision params, and the reader picks the second one
    /// — yielding wrongly-decoded binary data.</summary>
    private static void WriteArrayParamsExcludingEncoding(XmlWriter w, ParamContainer arr)
    {
        foreach (var pg in arr.ParamGroups)
        {
            w.WriteStartElement("referenceableParamGroupRef");
            w.WriteAttributeString("ref", pg.Id);
            w.WriteEndElement();
        }
        foreach (var cv in arr.CVParams)
            if (!IsEncodingCv(cv.Cvid)) MzmlXml.WriteCvParam(w, cv);
        foreach (var u in arr.UserParams) MzmlXml.WriteUserParam(w, u);
    }

    private static bool IsEncodingCv(CVID cv) => cv switch
    {
        CVID.MS_64_bit_float or CVID.MS_32_bit_float => true,
        CVID.MS_64_bit_integer or CVID.MS_32_bit_integer => true,
        CVID.MS_zlib_compression or CVID.MS_no_compression => true,
        CVID.MS_MS_Numpress_linear_prediction_compression => true,
        CVID.MS_MS_Numpress_positive_integer_compression => true,
        CVID.MS_MS_Numpress_short_logged_float_compression => true,
        _ => false,
    };

    private static void EmitEncodingCvParams(XmlWriter w, BinaryEncoderConfig cfg)
    {
        // Precision CV is always emitted — for numpress arrays it describes the semantic precision
        // of the decoded array (pwiz C++ writes 32-bit float alongside the numpress CV).
        MzmlXml.WriteCvParam(w, new CVParam(
            cfg.Precision == BinaryPrecision.Bits64 ? CVID.MS_64_bit_float : CVID.MS_32_bit_float));

        // Truncation + prediction CV (mzMLb-only in cpp). Emitted before the compression CV
        // so the order matches cpp IO.cpp:1955-1968. The cvParam's name implies zlib so we
        // skip the separate zlib CV when prediction is set.
        if (cfg.Prediction == BinaryPrediction.Linear)
            MzmlXml.WriteCvParam(w, new CVParam(CVID.MS_truncation__linear_prediction_and_zlib_compression));
        else if (cfg.Prediction == BinaryPrediction.Delta)
            MzmlXml.WriteCvParam(w, new CVParam(CVID.MS_truncation__delta_prediction_and_zlib_compression));

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
