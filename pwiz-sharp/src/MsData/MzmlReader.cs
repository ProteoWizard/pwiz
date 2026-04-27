using System.Globalization;
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
        if (!MzmlXml.MoveToFirstChildElement(r)) return;

        while (r.NodeType != XmlNodeType.EndElement)
        {
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
        if (!MzmlXml.MoveToFirstChildElement(r)) return;
        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "cv" && r.NodeType == XmlNodeType.Element)
            {
                msd.CVs.Add(new CV
                {
                    Id = r.GetAttribute("id") ?? string.Empty,
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
        if (!MzmlXml.MoveToFirstChildElement(r)) return;

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
                    Id = r.GetAttribute("id") ?? string.Empty,
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
                var pg = new ParamGroup(r.GetAttribute("id") ?? string.Empty);
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
                var s = new Sample(r.GetAttribute("id") ?? string.Empty, r.GetAttribute("name") ?? string.Empty);
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
                var sw = new Software(r.GetAttribute("id") ?? string.Empty)
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
                var ic = new InstrumentConfiguration(r.GetAttribute("id") ?? string.Empty);
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
                    string? swRef = r.GetAttribute("ref");
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
                var dp = new DataProcessing(r.GetAttribute("id") ?? string.Empty);
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
                            string? swRef = r.GetAttribute("softwareRef");
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
        msd.Run.Id = r.GetAttribute("id") ?? string.Empty;
        msd.Run.StartTimeStamp = r.GetAttribute("startTimeStamp") ?? string.Empty;

        string? icRef = r.GetAttribute("defaultInstrumentConfigurationRef");
        if (icRef is not null && _instrumentById.TryGetValue(icRef, out var ic))
        {
            msd.Run.DefaultInstrumentConfiguration = ic;
            _runDefaultIc = ic;
        }

        string? sampleRef = r.GetAttribute("sampleRef");
        if (sampleRef is not null && _sampleById.TryGetValue(sampleRef, out var sample))
            msd.Run.Sample = sample;

        string? sfRef = r.GetAttribute("defaultSourceFileRef");
        if (sfRef is not null && _sourceFileById.TryGetValue(sfRef, out var sf))
            msd.Run.DefaultSourceFile = sf;

        if (!MzmlXml.MoveToFirstChildElement(r)) { r.Read(); return; }

        MzmlXml.ReadParams(r, msd.Run, _paramGroupById);

        while (r.NodeType != XmlNodeType.EndElement)
        {
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
        r.Read();
    }

    private void ReadSpectrumList(XmlReader r, MSData msd)
    {
        var list = new SpectrumListSimple();
        string? dpRef = r.GetAttribute("defaultDataProcessingRef");
        if (dpRef is not null && _dpById.TryGetValue(dpRef, out var dp))
            list.Dp = dp;

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

    private Spectrum ReadSpectrum(XmlReader r)
    {
        var spec = new Spectrum
        {
            Index = int.Parse(r.GetAttribute("index") ?? "0", CultureInfo.InvariantCulture),
            Id = r.GetAttribute("id") ?? string.Empty,
            SpotId = r.GetAttribute("spotID") ?? string.Empty,
            DefaultArrayLength = int.Parse(r.GetAttribute("defaultArrayLength") ?? "0", CultureInfo.InvariantCulture),
        };

        string? sfRef = r.GetAttribute("sourceFileRef");
        if (sfRef is not null && _sourceFileById.TryGetValue(sfRef, out var sf)) spec.SourceFile = sf;

        string? dpRef = r.GetAttribute("dataProcessingRef");
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
        r.Read();
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
                string? icRef = r.GetAttribute("instrumentConfigurationRef");
                if (icRef is not null && _instrumentById.TryGetValue(icRef, out var ic))
                    scan.InstrumentConfiguration = ic;
                else
                    // mzML semantics: omitted ref means the run's default IC applies.
                    scan.InstrumentConfiguration = _runDefaultIc;
                // Bruker combined-IMS spectra carry one <scan spectrumRef="frame=N scan=M">
                // per merged scan; preserve the link for round-trip parity.
                scan.SpectrumId = r.GetAttribute("spectrumRef") ?? string.Empty;

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
                    SpectrumId = r.GetAttribute("spectrumRef") ?? string.Empty,
                };
                string? sfRef = r.GetAttribute("sourceFileRef");
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
        string? dpRef = r.GetAttribute("dataProcessingRef");
        DataProcessing? dp = null;
        if (dpRef is not null) _dpById.TryGetValue(dpRef, out dp);

        if (MzmlXml.MoveToFirstChildElement(r))
        {
            MzmlXml.ReadParams(r, tempParams, _paramGroupById);
            while (r.NodeType != XmlNodeType.EndElement)
            {
                if (r.LocalName == "binary" && r.NodeType == XmlNodeType.Element)
                    base64 = r.ReadElementContentAsString();
                else r.Read();
            }
        }

        bool isInteger = tempParams.HasCVParam(CVID.MS_32_bit_integer) || tempParams.HasCVParam(CVID.MS_64_bit_integer);
        var encoderConfig = new BinaryEncoderConfig();
        ConfigureFromParams(tempParams, encoderConfig, isInteger);

        if (isInteger)
        {
            var arr = new IntegerDataArray { DataProcessing = dp };
            CopyParams(tempParams, arr);
            if (base64 is not null && base64.Length > 0)
                arr.Data.AddRange(new BinaryDataEncoder(encoderConfig).DecodeIntegers(base64));
            integerArrays.Add(arr);
        }
        else
        {
            var arr = new BinaryDataArray { DataProcessing = dp };
            CopyParams(tempParams, arr);
            if (base64 is not null && base64.Length > 0)
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

    private void ReadChromatogramList(XmlReader r, MSData msd)
    {
        var list = new ChromatogramListSimple();
        string? dpRef = r.GetAttribute("defaultDataProcessingRef");
        if (dpRef is not null && _dpById.TryGetValue(dpRef, out var dp))
            list.Dp = dp;

        if (!MzmlXml.MoveToFirstChildElement(r)) { msd.Run.ChromatogramList = list; r.Read(); return; }

        while (r.NodeType != XmlNodeType.EndElement)
        {
            if (r.LocalName == "chromatogram" && r.NodeType == XmlNodeType.Element)
                list.Chromatograms.Add(ReadChromatogram(r));
            else r.Read();
        }
        msd.Run.ChromatogramList = list;
        r.Read();
    }

    private Chromatogram ReadChromatogram(XmlReader r)
    {
        var chrom = new Chromatogram
        {
            Index = int.Parse(r.GetAttribute("index") ?? "0", CultureInfo.InvariantCulture),
            Id = r.GetAttribute("id") ?? string.Empty,
            DefaultArrayLength = int.Parse(r.GetAttribute("defaultArrayLength") ?? "0", CultureInfo.InvariantCulture),
        };
        string? dpRef = r.GetAttribute("dataProcessingRef");
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
        p.SpectrumId = r.GetAttribute("spectrumRef") ?? string.Empty;
        string? sfRef = r.GetAttribute("sourceFileRef");
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
