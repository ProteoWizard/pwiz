using System.Globalization;
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

namespace Pwiz.Data.MsData.Mzml;

/// <summary>
/// Deserializes mzML 1.1 XML into an in-memory <see cref="MSData"/>.
/// </summary>
/// <remarks>
/// Port of pwiz::msdata::Serializer_mzML (read path). Eager: loads all spectra into a
/// <see cref="SpectrumListSimple"/>. File-backed lazy loading via index offsets is a
/// separate follow-up (see <c>SpectrumList_mzML</c> in the C++ source).
/// Skips the <c>indexedmzML</c> wrapper and scanSettings for now; parses chromatogramList.
/// </remarks>
public sealed class MzmlReader
{
    // Lookup maps populated during parse so refs can be resolved back to object references.
    private readonly Dictionary<string, Software> _softwareById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InstrumentConfiguration> _instrumentById = new(StringComparer.Ordinal);
    private InstrumentConfiguration? _runDefaultIc;
    private readonly Dictionary<string, DataProcessing> _dpById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceFile> _sourceFileById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Sample> _sampleById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ParamGroup> _paramGroupById = new(StringComparer.Ordinal);

    /// <summary>
    /// Optional source for external binary arrays referenced by
    /// <see cref="CVID.MS_external_HDF5_dataset"/> cvParams. Set by
    /// <c>Reader_MzMlb</c> before calling <see cref="Read(Stream)"/>; null for
    /// plain mzML.
    /// </summary>
    public IExternalBinarySource? ExternalBinarySource { get; set; }

    /// <summary>
    /// When true, <see cref="ReadSpectrumList"/> skips the spectrum bodies — it
    /// records the spectrumList's <c>count</c> + <c>defaultDataProcessingRef</c>
    /// attributes but leaves <see cref="MSData.Run"/>.<c>SpectrumList</c> unset.
    /// Used by <see cref="SpectrumList_Mzml"/> to bypass the eager full-file load
    /// while still populating the ref maps needed for later per-spectrum parses.
    /// </summary>
    internal bool LazyMode { get; set; }

    /// <summary>Spectrum count from the <c>count</c> attribute on <c>&lt;spectrumList&gt;</c>.
    /// Only populated when <see cref="LazyMode"/> is true.</summary>
    internal int LazySpectrumCount { get; private set; }

    /// <summary>Resolved default <see cref="DataProcessing"/> for the spectrum list.
    /// Only populated when <see cref="LazyMode"/> is true.</summary>
    internal DataProcessing? LazyDefaultDataProcessing { get; private set; }

    /// <summary>Set by <see cref="ReadSpectrumList"/> in <see cref="LazyMode"/> after
    /// reading the <c>&lt;spectrumList&gt;</c> opening tag's attributes. Outer parse loops
    /// (<see cref="ReadRun"/>, <see cref="ReadMzmlBody"/>, <see cref="ReadDocument"/>)
    /// check this and bail out so we never invoke <see cref="XmlReader.Skip"/> over the
    /// spectrum bodies — that walk is O(file size) and dominates the open phase on large
    /// files. The caller resumes parsing chromatogramList + closing tags via a separate
    /// XmlReader positioned past <c>&lt;/spectrumList&gt;</c> using the byte offset from
    /// the indexList footer.</summary>
    internal bool Halted { get; private set; }

    /// <summary>
    /// Per-call flag set by <see cref="ReadOneSpectrum"/> to make
    /// <see cref="ReadBinaryDataArray"/> skip the actual base64 / HDF5 decode
    /// while still recording the binary-array cv params (encoding precision,
    /// compression, array kind). Matches the cpp <c>SpectrumList::spectrum(i, false)</c>
    /// contract — caller gets all the metadata but no peak values.
    /// </summary>
    private bool _skipBinaryData;

    /// <summary>Parses mzML from a string.</summary>
    public MSData Read(string mzml)
    {
        using var sr = new StringReader(mzml);
        using var xr = XmlReader.Create(sr, new XmlReaderSettings { IgnoreWhitespace = true });
        return ReadDocument(xr);
    }

    /// <summary>Parses mzML from a stream.</summary>
    public MSData Read(Stream stream)
    {
        using var xr = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = true });
        return ReadDocument(xr);
    }

    private MSData ReadDocument(XmlReader r)
    {
        var msd = new MSData();

        while (r.Read())
        {
            if (r.NodeType != XmlNodeType.Element) continue;

            // Skip the indexedmzML wrapper if present; everything we need is inside <mzML>.
            if (r.LocalName == "indexedmzML") continue;

            if (r.LocalName == "mzML")
            {
                msd.Accession = r.GetAttribute("accession") ?? string.Empty;
                // cpp IO.cpp:3442 reads msd.id raw ("not an XML:ID"). Mirror that so a round-trip
                // read/write of a cpp-written mzML keeps the original id bytes.
                msd.Id = r.GetAttribute("id") ?? string.Empty;
                msd.Version = r.GetAttribute("version") ?? string.Empty;
                ReadMzmlBody(r, msd);
                break;
            }
        }
        return msd;
    }

    private void ReadMzmlBody(XmlReader r, MSData msd)
    {
        // Empty/self-closing parent: helper leaves reader on the start element (for
        // <foo/>) or on </foo> (for <foo></foo>). Either way, one more Read() is
        // needed to advance past the element; otherwise the caller's loop sees the
        // same start element forever (cvList="0" infinite loop was traced to this).
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }

        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (Halted) return;
            if (r.NodeType != XmlNodeType.Element) { r.Read(); continue; }

            switch (r.LocalName)
            {
                case "cvList":                        ReadCvList(r, msd); break;
                case "fileDescription":               ReadFileDescription(r, msd); break;
                case "referenceableParamGroupList":   ReadParamGroupList(r, msd); break;
                case "sampleList":                    ReadSampleList(r, msd); break;
                case "softwareList":                  ReadSoftwareList(r, msd); break;
                case "instrumentConfigurationList":   ReadInstrumentConfigList(r, msd); break;
                case "dataProcessingList":            ReadDataProcessingList(r, msd); break;
                case "run":                           ReadRun(r, msd); break;
                default:                              MzmlXml.SkipElement(r); break;
            }
        }
    }

    private static void ReadCvList(XmlReader r, MSData msd)
    {
        // See ReadMzmlBody — empty/self-closing parent needs a Read() to advance.
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "cv" && r.NodeType == XmlNodeType.Element)
            {
                msd.CVs.Add(new CV
                {
                    Id = XmlIdEncoding.Decode(r.GetAttribute("id") ?? string.Empty) ?? string.Empty,
                    FullName = r.GetAttribute("fullName") ?? string.Empty,
                    Version = r.GetAttribute("version") ?? string.Empty,
                    Uri = r.GetAttribute("URI") ?? string.Empty,
                });
                MzmlXml.SkipElement(r);
            }
            else r.Read();
        }
        r.Read();
    }

    private void ReadFileDescription(XmlReader r, MSData msd)
    {
        // See ReadMzmlBody — empty/self-closing parent needs a Read() to advance.
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }

        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.NodeType != XmlNodeType.Element) { r.Read(); continue; }

            switch (r.LocalName)
            {
                case "fileContent":
                    if (MzmlXml.MoveToFirstChildElement(r))
                        MzmlXml.ReadParams(r, msd.FileDescription.FileContent, _paramGroupById);
                    r.Read(); // past </fileContent>
                    break;

                case "sourceFileList":
                    ReadSourceFileList(r, msd);
                    break;

                case "contact":
                    var contact = new Contact();
                    if (MzmlXml.MoveToFirstChildElement(r))
                        MzmlXml.ReadParams(r, contact, _paramGroupById);
                    msd.FileDescription.Contacts.Add(contact);
                    r.Read();
                    break;

                default: MzmlXml.SkipElement(r); break;
            }
        }
        r.Read();
    }

    private void ReadSourceFileList(XmlReader r, MSData msd)
    {
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "sourceFile" && r.NodeType == XmlNodeType.Element)
            {
                var sf = new SourceFile
                {
                    Id = XmlIdEncoding.Decode(r.GetAttribute("id") ?? string.Empty) ?? string.Empty,
                    Name = r.GetAttribute("name") ?? string.Empty,
                    Location = r.GetAttribute("location") ?? string.Empty,
                };
                if (MzmlXml.MoveToFirstChildElement(r))
                    MzmlXml.ReadParams(r, sf, _paramGroupById);
                msd.FileDescription.SourceFiles.Add(sf);
                _sourceFileById[sf.Id] = sf;
                r.Read();
            }
            else r.Read();
        }
        r.Read();
    }

    private void ReadParamGroupList(XmlReader r, MSData msd)
    {
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "referenceableParamGroup" && r.NodeType == XmlNodeType.Element)
            {
                var pg = new ParamGroup(XmlIdEncoding.Decode(r.GetAttribute("id") ?? string.Empty) ?? string.Empty);
                if (MzmlXml.MoveToFirstChildElement(r))
                    MzmlXml.ReadParams(r, pg, _paramGroupById);
                msd.ParamGroups.Add(pg);
                _paramGroupById[pg.Id] = pg;
                r.Read();
            }
            else r.Read();
        }
        r.Read();
    }

    private void ReadSampleList(XmlReader r, MSData msd)
    {
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "sample" && r.NodeType == XmlNodeType.Element)
            {
                var s = new Sample(XmlIdEncoding.Decode(r.GetAttribute("id") ?? string.Empty) ?? string.Empty, r.GetAttribute("name") ?? string.Empty);
                if (MzmlXml.MoveToFirstChildElement(r))
                    MzmlXml.ReadParams(r, s, _paramGroupById);
                msd.Samples.Add(s);
                _sampleById[s.Id] = s;
                r.Read();
            }
            else r.Read();
        }
        r.Read();
    }

    private void ReadSoftwareList(XmlReader r, MSData msd)
    {
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "software" && r.NodeType == XmlNodeType.Element)
            {
                var sw = new Software(XmlIdEncoding.Decode(r.GetAttribute("id") ?? string.Empty) ?? string.Empty)
                {
                    Version = r.GetAttribute("version") ?? string.Empty,
                };
                if (MzmlXml.MoveToFirstChildElement(r))
                    MzmlXml.ReadParams(r, sw, _paramGroupById);
                msd.Software.Add(sw);
                _softwareById[sw.Id] = sw;
                r.Read();
            }
            else r.Read();
        }
        r.Read();
    }

    private void ReadInstrumentConfigList(XmlReader r, MSData msd)
    {
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "instrumentConfiguration" && r.NodeType == XmlNodeType.Element)
            {
                var ic = new InstrumentConfiguration(XmlIdEncoding.Decode(r.GetAttribute("id") ?? string.Empty) ?? string.Empty);
                ReadInstrumentConfiguration(r, ic);
                msd.InstrumentConfigurations.Add(ic);
                _instrumentById[ic.Id] = ic;
            }
            else r.Read();
        }
        r.Read();
    }

    private void ReadInstrumentConfiguration(XmlReader r, InstrumentConfiguration ic)
    {
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }

        MzmlXml.ReadParams(r, ic, _paramGroupById);

        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.NodeType != XmlNodeType.Element) { r.Read(); continue; }
            switch (r.LocalName)
            {
                case "componentList":
                    ReadComponentList(r, ic.ComponentList);
                    break;
                case "softwareRef":
                    string? swRef = XmlIdEncoding.Decode(r.GetAttribute("ref") ?? string.Empty);
                    if (swRef is not null && _softwareById.TryGetValue(swRef, out var sw))
                        ic.Software = sw;
                    MzmlXml.SkipElement(r);
                    break;
                default: MzmlXml.SkipElement(r); break;
            }
        }
        r.Read();
    }

    private void ReadComponentList(XmlReader r, ComponentList list)
    {
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.NodeType != XmlNodeType.Element) { r.Read(); continue; }

            var type = r.LocalName switch
            {
                "source" => ComponentType.Source,
                "analyzer" => ComponentType.Analyzer,
                "detector" => ComponentType.Detector,
                _ => ComponentType.Unknown,
            };
            int order = int.Parse(r.GetAttribute("order") ?? "0", CultureInfo.InvariantCulture);
            var c = new Component(type, order);

            if (MzmlXml.MoveToFirstChildElement(r))
                MzmlXml.ReadParams(r, c, _paramGroupById);
            list.Add(c);
            r.Read();
        }
        r.Read();
    }

    private void ReadDataProcessingList(XmlReader r, MSData msd)
    {
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "dataProcessing" && r.NodeType == XmlNodeType.Element)
            {
                var dp = new DataProcessing(XmlIdEncoding.Decode(r.GetAttribute("id") ?? string.Empty) ?? string.Empty);
                if (MzmlXml.MoveToFirstChildElement(r))
                {
                    while (r.NodeType != XmlNodeType.EndElement)
                    {
                        if (r.LocalName == "processingMethod" && r.NodeType == XmlNodeType.Element)
                        {
                            var pm = new ProcessingMethod
                            {
                                Order = int.Parse(r.GetAttribute("order") ?? "0", CultureInfo.InvariantCulture),
                            };
                            string? swRef = XmlIdEncoding.Decode(r.GetAttribute("softwareRef") ?? string.Empty);
                            if (swRef is not null && _softwareById.TryGetValue(swRef, out var sw))
                                pm.Software = sw;
                            if (MzmlXml.MoveToFirstChildElement(r))
                                MzmlXml.ReadParams(r, pm, _paramGroupById);
                            dp.ProcessingMethods.Add(pm);
                            r.Read();
                        }
                        else r.Read();
                    }
                }
                msd.DataProcessings.Add(dp);
                _dpById[dp.Id] = dp;
                r.Read();
            }
            else r.Read();
        }
        r.Read();
    }

    private void ReadRun(XmlReader r, MSData msd)
    {
        msd.Run.Id = XmlIdEncoding.Decode(r.GetAttribute("id") ?? string.Empty) ?? string.Empty;
        msd.Run.StartTimeStamp = r.GetAttribute("startTimeStamp") ?? string.Empty;

        string? icRef = XmlIdEncoding.Decode(r.GetAttribute("defaultInstrumentConfigurationRef") ?? string.Empty);
        if (icRef is not null && _instrumentById.TryGetValue(icRef, out var ic))
        {
            msd.Run.DefaultInstrumentConfiguration = ic;
            _runDefaultIc = ic;
        }

        string? sampleRef = XmlIdEncoding.Decode(r.GetAttribute("sampleRef") ?? string.Empty);
        if (sampleRef is not null && _sampleById.TryGetValue(sampleRef, out var sample))
            msd.Run.Sample = sample;

        string? sfRef = XmlIdEncoding.Decode(r.GetAttribute("defaultSourceFileRef") ?? string.Empty);
        if (sfRef is not null && _sourceFileById.TryGetValue(sfRef, out var sf))
            msd.Run.DefaultSourceFile = sf;

        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }

        MzmlXml.ReadParams(r, msd.Run, _paramGroupById);

        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (Halted) return;
            if (r.NodeType != XmlNodeType.Element) { r.Read(); continue; }
            switch (r.LocalName)
            {
                case "spectrumList":
                    ReadSpectrumList(r, msd);
                    break;
                case "chromatogramList":
                    ReadChromatogramList(r, msd);
                    break;
                default: MzmlXml.SkipElement(r); break;
            }
        }
        if (Halted) return;
        r.Read();
    }

    /// <summary>Resumes parsing from an arbitrary byte position in an mzML file — picks up
    /// any <c>&lt;chromatogramList&gt;</c> and then returns. Used by
    /// <c>MzmlReaderAdapter</c> after lazy-mode header read short-circuited at
    /// <c>&lt;spectrumList&gt;</c>: the caller seeks past <c>&lt;/spectrumList&gt;</c> using
    /// the indexList byte offset and calls us to handle whatever's left of <c>&lt;/run&gt;</c>.</summary>
    internal void ResumeAfterSpectrumList(Stream stream, MSData msd)
    {
        // Byte-scan a small window for "<chromatogramList". XmlReader in Fragment mode
        // would throw "Unexpected end tag" on </run> / </mzML> closing tags (no matching
        // start is in scope mid-document), so we can't just open it and Read() blindly.
        // If no chromatogramList is in scope, we're done.
        long resumeStart = stream.Position;
        const int scanBytes = 8192;
        var probe = new byte[scanBytes];
        int n = stream.Read(probe, 0, probe.Length);
        if (n <= 0) return;
        int idx = System.Text.Encoding.ASCII.GetString(probe, 0, n)
            .IndexOf("<chromatogramList", System.StringComparison.Ordinal);
        if (idx < 0) return;

        stream.Position = resumeStart + idx;
        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            CloseInput = false,
            ConformanceLevel = ConformanceLevel.Fragment,
        };
        using var r = XmlReader.Create(stream, settings);
        if (!r.ReadToFollowing("chromatogramList")) return;
        ReadChromatogramList(r, msd, advancePastEnd: false);
    }

    private void ReadSpectrumList(XmlReader r, MSData msd)
    {
        string? dpRef = XmlIdEncoding.Decode(r.GetAttribute("defaultDataProcessingRef") ?? string.Empty);
        DataProcessing? dp = null;
        if (dpRef is not null) _dpById.TryGetValue(dpRef, out dp);

        if (LazyMode)
        {
            // Record metadata and bail out — DO NOT call XmlReader.Skip here. Skip walks
            // every spectrum element to find the matching </spectrumList>, which is the
            // 2.5-second-per-open hit on a 90k-spectrum file. The caller knows the byte
            // position of </spectrumList> from the indexList footer and will resume
            // parsing past it with a fresh XmlReader (see MzmlReaderAdapter.Read).
            if (int.TryParse(r.GetAttribute("count"), NumberStyles.Integer,
                             CultureInfo.InvariantCulture, out int cnt))
                LazySpectrumCount = cnt;
            LazyDefaultDataProcessing = dp;
            Halted = true;
            return;
        }

        var list = new SpectrumListSimple { Dp = dp };

        if (!MzmlXml.MoveToFirstChildElement(r)) { msd.Run.SpectrumList = list; r.Read(); return; }

        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "spectrum" && r.NodeType == XmlNodeType.Element)
            {
                var spec = ReadSpectrum(r);
                list.Spectra.Add(spec);
            }
            else r.Read();
        }
        msd.Run.SpectrumList = list;
        r.Read();
    }

    /// <summary>Parses a single <c>&lt;spectrum&gt;</c> element under the same context the
    /// instance currently holds (ref maps, external binary source). Used by
    /// <see cref="SpectrumList_Mzml"/> for per-spectrum lazy reads.</summary>
    /// <param name="r">An XmlReader positioned at a <c>&lt;spectrum&gt;</c> start element.</param>
    /// <param name="getBinaryData">When false, <see cref="BinaryDataArray.Data"/> is left empty
    /// but the cv params describing precision / compression / array type are still populated.</param>
    internal Spectrum ReadOneSpectrum(XmlReader r, bool getBinaryData)
    {
        bool prev = _skipBinaryData;
        _skipBinaryData = !getBinaryData;
        try
        {
            var spec = ReadSpectrum(r, advancePastEnd: false);
            return spec;
        }
        finally { _skipBinaryData = prev; }
    }

    private Spectrum ReadSpectrum(XmlReader r) => ReadSpectrum(r, advancePastEnd: true);

    /// <summary>Parses one <c>&lt;spectrum&gt;</c> element. When
    /// <paramref name="advancePastEnd"/> is true (the eager <see cref="ReadSpectrumList"/>
    /// caller's contract) the reader is left positioned after <c>&lt;/spectrum&gt;</c>;
    /// when false (the lazy <see cref="ReadOneSpectrum"/> caller) the reader is left ON
    /// the <c>&lt;/spectrum&gt;</c> end element. The lazy caller can't advance past — in
    /// Fragment mode it would try to consume <c>&lt;/spectrumList&gt;</c> with no
    /// matching start tag in scope and throw "Unexpected end tag".</summary>
    private Spectrum ReadSpectrum(XmlReader r, bool advancePastEnd)
    {
        var spec = new Spectrum
        {
            Index = int.Parse(r.GetAttribute("index") ?? "0", CultureInfo.InvariantCulture),
            // spectrum.id is raw per cpp IO.cpp:2716 ("not an XML:ID"). Free-form scan strings.
            Id = r.GetAttribute("id") ?? string.Empty,
            SpotId = r.GetAttribute("spotID") ?? string.Empty,
            DefaultArrayLength = int.Parse(r.GetAttribute("defaultArrayLength") ?? "0", CultureInfo.InvariantCulture),
        };

        string? sfRef = XmlIdEncoding.Decode(r.GetAttribute("sourceFileRef") ?? string.Empty);
        if (sfRef is not null && _sourceFileById.TryGetValue(sfRef, out var sf)) spec.SourceFile = sf;

        string? dpRef = XmlIdEncoding.Decode(r.GetAttribute("dataProcessingRef") ?? string.Empty);
        if (dpRef is not null && _dpById.TryGetValue(dpRef, out var dp)) spec.DataProcessing = dp;

        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return spec; }

        MzmlXml.ReadParams(r, spec.Params, _paramGroupById);

        BinaryEncoderConfig? pendingArrayConfig = null;

        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.NodeType != XmlNodeType.Element) { r.Read(); continue; }

            switch (r.LocalName)
            {
                case "scanList":           ReadScanList(r, spec.ScanList); break;
                case "precursorList":      ReadPrecursorList(r, spec); break;
                case "binaryDataArrayList": ReadBinaryDataArrayList(r, spec); break;
                default:                   MzmlXml.SkipElement(r); break;
            }
            _ = pendingArrayConfig;
        }
        if (advancePastEnd) r.Read();
        return spec;
    }

    private void ReadScanList(XmlReader r, ScanList sl)
    {
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        MzmlXml.ReadParams(r, sl, _paramGroupById);

        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "scan" && r.NodeType == XmlNodeType.Element)
            {
                var scan = new Scan();
                string? icRef = XmlIdEncoding.Decode(r.GetAttribute("instrumentConfigurationRef") ?? string.Empty);
                if (icRef is not null && _instrumentById.TryGetValue(icRef, out var ic))
                    scan.InstrumentConfiguration = ic;
                else
                    // mzML semantics: omitted ref means the run's default IC applies.
                    scan.InstrumentConfiguration = _runDefaultIc;
                // Bruker combined-IMS spectra carry one <scan spectrumRef="frame=N scan=M">
                // per merged scan; preserve the link for round-trip parity.
                scan.SpectrumId = XmlIdEncoding.Decode(r.GetAttribute("spectrumRef") ?? string.Empty) ?? string.Empty;

                if (MzmlXml.MoveToFirstChildElement(r))
                {
                    MzmlXml.ReadParams(r, scan, _paramGroupById);
                    while (r.NodeType != XmlNodeType.EndElement)
                    {
                        if (r.LocalName == "scanWindowList" && r.NodeType == XmlNodeType.Element)
                        {
                            if (MzmlXml.MoveToFirstChildElement(r))
                            {
                                while (r.NodeType != XmlNodeType.EndElement)
                                {
                                    if (r.LocalName == "scanWindow" && r.NodeType == XmlNodeType.Element)
                                    {
                                        var sw = new ScanWindow();
                                        if (MzmlXml.MoveToFirstChildElement(r))
                                            MzmlXml.ReadParams(r, sw, _paramGroupById);
                                        scan.ScanWindows.Add(sw);
                                        r.Read();
                                    }
                                    else r.Read();
                                }
                            }
                            r.Read();
                        }
                        else r.Read();
                    }
                }
                sl.Scans.Add(scan);
                r.Read();
            }
            else r.Read();
        }
        r.Read();
    }

    private void ReadPrecursorList(XmlReader r, Spectrum spec)
    {
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "precursor" && r.NodeType == XmlNodeType.Element)
            {
                var p = new Precursor
                {
                    ExternalSpectrumId = r.GetAttribute("externalSpectrumID") ?? string.Empty,
                    SpectrumId = XmlIdEncoding.Decode(r.GetAttribute("spectrumRef") ?? string.Empty) ?? string.Empty,
                };
                string? sfRef = XmlIdEncoding.Decode(r.GetAttribute("sourceFileRef") ?? string.Empty);
                if (sfRef is not null && _sourceFileById.TryGetValue(sfRef, out var sf)) p.SourceFile = sf;

                if (MzmlXml.MoveToFirstChildElement(r))
                {
                    while (r.NodeType != XmlNodeType.EndElement)
                    {
                        if (r.NodeType != XmlNodeType.Element) { r.Read(); continue; }
                        switch (r.LocalName)
                        {
                            case "isolationWindow":
                                if (MzmlXml.MoveToFirstChildElement(r))
                                    MzmlXml.ReadParams(r, p.IsolationWindow, _paramGroupById);
                                r.Read();
                                break;
                            case "selectedIonList":
                                ReadSelectedIonList(r, p);
                                break;
                            case "activation":
                                if (MzmlXml.MoveToFirstChildElement(r))
                                    MzmlXml.ReadParams(r, p.Activation, _paramGroupById);
                                r.Read();
                                break;
                            default: MzmlXml.SkipElement(r); break;
                        }
                    }
                }
                spec.Precursors.Add(p);
                r.Read();
            }
            else r.Read();
        }
        r.Read();
    }

    private void ReadSelectedIonList(XmlReader r, Precursor p)
    {
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "selectedIon" && r.NodeType == XmlNodeType.Element)
            {
                var si = new SelectedIon();
                if (MzmlXml.MoveToFirstChildElement(r))
                    MzmlXml.ReadParams(r, si, _paramGroupById);
                p.SelectedIons.Add(si);
                r.Read();
            }
            else r.Read();
        }
        r.Read();
    }

    private void ReadBinaryDataArrayList(XmlReader r, Spectrum spec) =>
        ReadBinaryDataArrayListImpl(r, spec.BinaryDataArrays, spec.IntegerDataArrays);

    private void ReadBinaryDataArrayList(XmlReader r, Chromatogram chrom) =>
        ReadBinaryDataArrayListImpl(r, chrom.BinaryDataArrays, chrom.IntegerDataArrays);

    private void ReadBinaryDataArrayListImpl(XmlReader r,
        List<BinaryDataArray> doubleArrays,
        List<IntegerDataArray> integerArrays)
    {
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "binaryDataArray" && r.NodeType == XmlNodeType.Element)
            {
                ReadBinaryDataArray(r, doubleArrays, integerArrays);
            }
            else r.Read();
        }
        r.Read();
    }

    private void ReadBinaryDataArray(XmlReader r,
        List<BinaryDataArray> doubleArrays,
        List<IntegerDataArray> integerArrays)
    {
        // Parse cvParams first (keep them on a temp ParamContainer) so we can decide int vs double.
        var tempParams = new ParamContainer();
        string? base64 = null;
        string? dpRef = XmlIdEncoding.Decode(r.GetAttribute("dataProcessingRef") ?? string.Empty);
        DataProcessing? dp = null;
        if (dpRef is not null) _dpById.TryGetValue(dpRef, out dp);

        // Capture encodedLength from the <binaryDataArray> opening tag before we
        // advance into its children. mzMLb numpress uses this to know how many
        // opaque bytes to read from the HDF5 dataset (the array_length cvParam
        // is the decoded-element count, not the byte count).
        int encodedLengthAttr = 0;
        if (int.TryParse(r.GetAttribute("encodedLength"), NumberStyles.Integer,
                         CultureInfo.InvariantCulture, out int parsedEncLen))
            encodedLengthAttr = parsedEncLen;

        if (MzmlXml.MoveToFirstChildElement(r))
        {
            MzmlXml.ReadParams(r, tempParams, _paramGroupById);
            while (r.NodeType != XmlNodeType.EndElement)
            {
                if (r.LocalName == "binary" && r.NodeType == XmlNodeType.Element)
                {
                    if (_skipBinaryData) MzmlXml.SkipElement(r); else base64 = r.ReadElementContentAsString();
                }
                else r.Read();
            }
        }

        bool isInteger = tempParams.HasCVParam(CVID.MS_32_bit_integer) || tempParams.HasCVParam(CVID.MS_64_bit_integer);
        var encoderConfig = new BinaryEncoderConfig();
        ConfigureFromParams(tempParams, encoderConfig, isInteger);

        // mzMLb branch: the binary element is empty and three cvParams point at
        // a named dataset in the surrounding HDF5 file. Route through the
        // injected ExternalBinarySource instead of decoding base64.
        //
        // For non-numpress arrays HDF5 handles type conversion at read time so
        // we always pull as double / long regardless of on-disk precision
        // (matches cpp IO.cpp:2553).
        //
        // For numpress arrays cpp stores the encoded bytes as an opaque dataset
        // and records the byte count in encodedLength; we read those bytes via
        // ReadBytes and decode through BinaryDataEncoder.DecodeDoublesFromRawBytes
        // configured with the same numpress / compression / precision params we
        // already parsed off the binaryDataArray's cvParams (matches cpp
        // IO.cpp:2539-2548).
        string? externalDataset = tempParams.CvParam(CVID.MS_external_HDF5_dataset).Value;
        if (!string.IsNullOrEmpty(externalDataset) && ExternalBinarySource is not null)
        {
            long externalOffset = tempParams.CvParamValueOrDefault(CVID.MS_external_offset, 0L);
            long externalLength = tempParams.CvParamValueOrDefault(CVID.MS_external_array_length, 0L);
            if (isInteger)
            {
                var arr = new IntegerDataArray { DataProcessing = dp };
                CopyParams(tempParams, arr);
                if (!_skipBinaryData && externalLength > 0)
                {
                    var buf = new long[externalLength];
                    int got = ExternalBinarySource.ReadInt64(externalDataset!, externalOffset, buf);
                    arr.Data.AddRange(buf.AsSpan(0, got).ToArray());
                }
                integerArrays.Add(arr);
            }
            else
            {
                var arr = new BinaryDataArray { DataProcessing = dp };
                CopyParams(tempParams, arr);
                if (!_skipBinaryData)
                {
                    if (encoderConfig.Numpress != BinaryNumpress.None && encodedLengthAttr > 0)
                    {
                        // Numpress: read encodedLengthAttr opaque bytes, decode in-process.
                        var bytes = new byte[encodedLengthAttr];
                        ExternalBinarySource.ReadBytes(externalDataset!, externalOffset, bytes);
                        arr.Data.AddRange(new BinaryDataEncoder(encoderConfig).DecodeDoublesFromRawBytes(bytes));
                    }
                    else if (externalLength > 0)
                    {
                        var buf = new double[externalLength];
                        int got = ExternalBinarySource.ReadDoubles(externalDataset!, externalOffset, buf);
                        arr.Data.AddRange(buf.AsSpan(0, got).ToArray());
                    }
                }
                doubleArrays.Add(arr);
            }
            r.Read();
            return;
        }

        if (isInteger)
        {
            var arr = new IntegerDataArray { DataProcessing = dp };
            CopyParams(tempParams, arr);
            if (!_skipBinaryData && base64 is not null && base64.Length > 0)
                arr.Data.AddRange(new BinaryDataEncoder(encoderConfig).DecodeIntegers(base64));
            integerArrays.Add(arr);
        }
        else
        {
            var arr = new BinaryDataArray { DataProcessing = dp };
            CopyParams(tempParams, arr);
            if (!_skipBinaryData && base64 is not null && base64.Length > 0)
                arr.Data.AddRange(new BinaryDataEncoder(encoderConfig).DecodeDoubles(base64));
            doubleArrays.Add(arr);
        }
        r.Read();
    }

    private static void CopyParams(ParamContainer src, ParamContainer dst)
    {
        dst.CVParams.AddRange(src.CVParams);
        dst.UserParams.AddRange(src.UserParams);
        foreach (var pg in src.ParamGroups) dst.ParamGroups.Add(pg);
    }

    private static void ConfigureFromParams(ParamContainer arr, BinaryEncoderConfig cfg, bool isInteger = false)
    {
        if (isInteger)
        {
            if (arr.HasCVParam(CVID.MS_32_bit_integer)) cfg.Precision = BinaryPrecision.Bits32;
            else if (arr.HasCVParam(CVID.MS_64_bit_integer)) cfg.Precision = BinaryPrecision.Bits64;
        }
        else
        {
            if (arr.HasCVParam(CVID.MS_32_bit_float)) cfg.Precision = BinaryPrecision.Bits32;
            else if (arr.HasCVParam(CVID.MS_64_bit_float)) cfg.Precision = BinaryPrecision.Bits64;
        }

        if (arr.HasCVParam(CVID.MS_zlib_compression)) cfg.Compression = BinaryCompression.Zlib;
        else if (arr.HasCVParam(CVID.MS_no_compression)) cfg.Compression = BinaryCompression.None;

        // Numpress: each algorithm has a standalone CV and a "followed by zlib" variant. The
        // zlib variant implies Compression = Zlib (the numpress bytes themselves are zlib-compressed).
        if (arr.HasCVParam(CVID.MS_MS_Numpress_linear_prediction_compression))
        { cfg.Numpress = BinaryNumpress.Linear; cfg.Compression = BinaryCompression.None; }
        else if (arr.HasCVParam(CVID.MS_MS_Numpress_linear_prediction_compression_followed_by_zlib_compression))
        { cfg.Numpress = BinaryNumpress.Linear; cfg.Compression = BinaryCompression.Zlib; }
        else if (arr.HasCVParam(CVID.MS_MS_Numpress_positive_integer_compression))
        { cfg.Numpress = BinaryNumpress.Pic; cfg.Compression = BinaryCompression.None; }
        else if (arr.HasCVParam(CVID.MS_MS_Numpress_positive_integer_compression_followed_by_zlib_compression))
        { cfg.Numpress = BinaryNumpress.Pic; cfg.Compression = BinaryCompression.Zlib; }
        else if (arr.HasCVParam(CVID.MS_MS_Numpress_short_logged_float_compression))
        { cfg.Numpress = BinaryNumpress.Slof; cfg.Compression = BinaryCompression.None; }
        else if (arr.HasCVParam(CVID.MS_MS_Numpress_short_logged_float_compression_followed_by_zlib_compression))
        { cfg.Numpress = BinaryNumpress.Slof; cfg.Compression = BinaryCompression.Zlib; }
    }

    // ---------- chromatogramList ----------

    private void ReadChromatogramList(XmlReader r, MSData msd) => ReadChromatogramList(r, msd, advancePastEnd: true);

    /// <summary>Parses a <c>&lt;chromatogramList&gt;</c>. When <paramref name="advancePastEnd"/>
    /// is false (the Fragment-mode <see cref="ResumeAfterSpectrumList"/> caller's contract)
    /// the reader is left on <c>&lt;/chromatogramList&gt;</c> — advancing past would try to
    /// consume the document's <c>&lt;/run&gt;</c> end tag, which Fragment mode rejects
    /// because no matching <c>&lt;run&gt;</c> start is in scope.</summary>
    private void ReadChromatogramList(XmlReader r, MSData msd, bool advancePastEnd)
    {
        var list = new ChromatogramListSimple();
        string? dpRef = XmlIdEncoding.Decode(r.GetAttribute("defaultDataProcessingRef") ?? string.Empty);
        if (dpRef is not null && _dpById.TryGetValue(dpRef, out var dp))
            list.Dp = dp;

        if (!MzmlXml.MoveToFirstChildElement(r))
        {
            msd.Run.ChromatogramList = list;
            if (advancePastEnd) r.Read();
            return;
        }

        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "chromatogram" && r.NodeType == XmlNodeType.Element)
                list.Chromatograms.Add(ReadChromatogram(r));
            else r.Read();
        }
        msd.Run.ChromatogramList = list;
        if (advancePastEnd) r.Read();
    }

    private Chromatogram ReadChromatogram(XmlReader r)
    {
        var chrom = new Chromatogram
        {
            Index = int.Parse(r.GetAttribute("index") ?? "0", CultureInfo.InvariantCulture),
            // chromatogram.id is raw per cpp ("not an XML:ID"). E.g. "TIC", "- SRM SIC Q1=309.0 ...".
            Id = r.GetAttribute("id") ?? string.Empty,
            DefaultArrayLength = int.Parse(r.GetAttribute("defaultArrayLength") ?? "0", CultureInfo.InvariantCulture),
        };
        string? dpRef = XmlIdEncoding.Decode(r.GetAttribute("dataProcessingRef") ?? string.Empty);
        if (dpRef is not null && _dpById.TryGetValue(dpRef, out var dp)) chrom.DataProcessing = dp;

        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return chrom; }
        MzmlXml.ReadParams(r, chrom.Params, _paramGroupById);

        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.NodeType != XmlNodeType.Element) { r.Read(); continue; }
            switch (r.LocalName)
            {
                case "precursor":            ReadChromatogramPrecursor(r, chrom.Precursor); break;
                case "product":              ReadChromatogramProduct(r, chrom.Product); break;
                case "binaryDataArrayList":  ReadBinaryDataArrayList(r, chrom); break;
                default:                     MzmlXml.SkipElement(r); break;
            }
        }
        r.Read();
        return chrom;
    }

    private void ReadChromatogramPrecursor(XmlReader r, Precursor p)
    {
        p.ExternalSpectrumId = r.GetAttribute("externalSpectrumID") ?? string.Empty;
        p.SpectrumId = XmlIdEncoding.Decode(r.GetAttribute("spectrumRef") ?? string.Empty) ?? string.Empty;
        string? sfRef = XmlIdEncoding.Decode(r.GetAttribute("sourceFileRef") ?? string.Empty);
        if (sfRef is not null && _sourceFileById.TryGetValue(sfRef, out var sf)) p.SourceFile = sf;

        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.NodeType != XmlNodeType.Element) { r.Read(); continue; }
            switch (r.LocalName)
            {
                case "isolationWindow":
                    if (MzmlXml.MoveToFirstChildElement(r))
                        MzmlXml.ReadParams(r, p.IsolationWindow, _paramGroupById);
                    r.Read();
                    break;
                case "selectedIonList":
                    ReadSelectedIonList(r, p);
                    break;
                case "activation":
                    if (MzmlXml.MoveToFirstChildElement(r))
                        MzmlXml.ReadParams(r, p.Activation, _paramGroupById);
                    r.Read();
                    break;
                default: MzmlXml.SkipElement(r); break;
            }
        }
        r.Read();
    }

    private void ReadChromatogramProduct(XmlReader r, Product prod)
    {
        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "isolationWindow" && r.NodeType == XmlNodeType.Element)
            {
                if (MzmlXml.MoveToFirstChildElement(r))
                    MzmlXml.ReadParams(r, prod.IsolationWindow, _paramGroupById);
                r.Read();
            }
            else r.Read();
        }
        r.Read();
    }
}
