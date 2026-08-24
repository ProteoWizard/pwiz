// Port of pwiz_tools/BiblioSpec/src/PrideXmlReader.{h,cpp}
//
// Parses PRIDE XML files. PRIDE XML mixes mzData-style spectra (with base64-encoded
// peak arrays in <mzArrayBinary> / <intenArrayBinary>) with PSM info in <PeptideItem>
// elements that point back at spectra via <SpectrumReference>. The reader doubles as
// its own SpecFileReader — spectra come from an in-memory map keyed by spectrum id.

using System.Buffers;
using System.Globalization;
using System.Xml;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Parses PRIDE XML files (mzData-style spectra + PRIDE-style PSMs).
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::PrideXmlReader</c>
/// (<c>pwiz_tools/BiblioSpec/src/PrideXmlReader.{h,cpp}</c>). The cpp reader is a
/// SAXHandler parsing with Expat; this port walks the document with
/// <see cref="XmlReader"/>, with the same element-state state machine
/// (<see cref="ParserState"/> mirroring cpp's <c>STATE</c> enum at
/// PrideXmlReader.h:62).</para>
/// <para>The cpp reader also implements <c>SpecFileReader</c> directly (peaks live in
/// the same .pride.xml file as the PSMs). The C# port mirrors that by installing an
/// inner <see cref="PrideSpecFileReader"/> on <see cref="BuildParser.SpecReader"/>
/// that hands back peaks from an in-memory map keyed by spectrum integer id.</para>
/// </remarks>
public sealed class PrideXmlReader : BuildParser
{
    /// <summary>
    /// cpp parity: PrideXmlReader.h:62 — <c>enum STATE</c>. Drives the
    /// startElement/endElement state machine.
    /// </summary>
    private enum ParserState
    {
        Root,
        IonSelection,
        PeaksMz,
        PeaksMzData,
        PeaksIntensity,
        PeaksIntensityData,
        PeptideItem,
        PeptideSequence,
        SpectrumReference,
        ModLocation,
        ModMonoDelta,
    }

    /// <summary>
    /// cpp parity: PrideXmlReader.h:111 — current <c>BinaryDataEncoder::Config</c>.
    /// Tracks endianness + precision for the next base64 blob.
    /// </summary>
    private struct BinaryConfig
    {
        public bool BigEndian;     // cpp parity: PrideXmlReader.cpp:327
        public bool Precision32;   // cpp parity: PrideXmlReader.cpp:328
    }

    // cpp parity: PrideXmlReader.h:101-117 — per-parse state.
    private ParserState _state = ParserState.Root;
    private readonly Stack<ParserState> _stateHistory = new();
    private double _threshold = -1;
    private bool _thresholdIsMax;
    private double _foundMz;
    private PsmScoreType _scoreType = PsmScoreType.UnknownScoreType;
    private bool _isScoreLookup;

    // cpp parity: per-spectrum holders.
    private SpecData? _curSpec;
    private SeqMod _curMod;
    private BinaryConfig _curBinaryConfig;
    private int _numMzs;
    private int _numIntensities;
    private string _charBuf = string.Empty;
    private readonly Dictionary<int, SpecData> _spectra = new();
    private readonly Dictionary<int, int> _spectraChargeStates = new();

    // Active XmlReader during parse, used by the GetAttrValue helpers below.
    private XmlReader? _reader;

    // cpp parity: PrideXmlReader.cpp:219 — "PT<seconds>S" terminators. SearchValues cached to
    // satisfy CA1870/CA1861 (cached IndexOfAny over a fixed character set).
    private static readonly SearchValues<char> _ptDurationTerminators = SearchValues.Create("Ss");

    /// <summary>
    /// True if <paramref name="filename"/> ends in <c>.pride.xml</c>. Used by
    /// <see cref="BlibBuilder"/>'s reader-factory dispatch.
    /// </summary>
    public static bool AcceptsExtension(string filename) =>
        BlibBuilder.HasExtensionCi(filename, ".pride.xml");

    /// <summary>
    /// Construct a PrideXmlReader bound to <paramref name="maker"/> and the file at
    /// <paramref name="xmlFileName"/>.
    /// </summary>
    /// <remarks>cpp parity: PrideXmlReader.cpp:32.</remarks>
    public PrideXmlReader(BlibBuilder maker, string xmlFileName, ProgressIndicator? parentProgress)
        : base(maker, xmlFileName, parentProgress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlFileName);

        // cpp parity: PrideXmlReader.cpp:43 — register the .pride.xml as the spec file.
        SetSpecFileName(xmlFileName, checkFile: false);

        // cpp parity: PrideXmlReader.cpp:46 — find spectra by integer index.
        LookUpBy = SpecIdType.IndexId;

        // cpp parity: PrideXmlReader.cpp:48 — no compression on the base64 blobs.
        // (We don't carry a separate config since we always assume no compression.)

        // cpp parity: PrideXmlReader.cpp:51 — `delete specReader_; specReader_ = this;`.
        // Mirror with an inner reader that pulls from _spectra.
        SpecReader = new PrideSpecFileReader(this);
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: PrideXmlReader.cpp:504.</remarks>
    public override bool ParseFile()
    {
        Parse();
        // cpp parity: PrideXmlReader.cpp:511 — add psms of the scoring type.
        BuildTables(_scoreType, string.Empty);
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: PrideXmlReader.cpp:517.</remarks>
    public override IList<PsmScoreType> GetScoreTypes()
    {
        _isScoreLookup = true;
        try
        {
            Parse();
        }
        catch (EndEarlyException)
        {
            // cpp parity: parseCvParam throws EndEarlyException once the score type is known.
        }
        return new List<PsmScoreType> { _scoreType };
    }

    /// <summary>Internal exception used to short-circuit GetScoreTypes once the score type is known.</summary>
    /// <remarks>cpp parity: SAXHandler::EndEarlyException (referenced from PrideXmlReader.cpp:301).</remarks>
    private sealed class EndEarlyException : Exception { }

    // ----------------------------------------------------------------------------------
    // XmlReader driver. cpp's SAXHandler::parse() walks with Expat; mirror it with
    // XmlReader, dispatching to StartElement/EndElement and feeding text into
    // Characters for the inline data we care about.
    // ----------------------------------------------------------------------------------

    private void Parse()
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = false, // we need text for base64 peaks + scalar element bodies
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            CloseInput = true,
        };

        try
        {
            using var fs = new FileStream(GetFileName(), FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = XmlReader.Create(fs, settings);
            _reader = reader;

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                    {
                        var isEmpty = reader.IsEmptyElement;
                        var name = reader.LocalName;
                        StartElement(name);
                        if (isEmpty)
                        {
                            // SAXHandler emits a matching endElement for self-closing elements too.
                            EndElement(name);
                        }
                        break;
                    }
                    case XmlNodeType.EndElement:
                        EndElement(reader.LocalName);
                        break;

                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                    case XmlNodeType.SignificantWhitespace:
                    case XmlNodeType.Whitespace:
                        Characters(reader.Value);
                        break;
                }
            }
        }
        catch (EndEarlyException)
        {
            throw;
        }
        catch (BlibException)
        {
            throw;
        }
        catch (XmlException ex)
        {
            throw new BlibException(true,
                $"XML parse error in {GetFileName()} (line {ex.LineNumber}, position {ex.LinePosition}): {ex.Message}");
        }
        finally
        {
            _reader = null;
        }
    }

    // cpp parity: SAXHandler.h:96 — isElement (case-sensitive in cpp; mirroring).
    private static bool IsElement(string expected, string actual) =>
        string.Equals(expected, actual, StringComparison.Ordinal);

    // cpp parity: SAXHandler.h:107 — getAttrValue, returns "" if missing.
    private string GetAttrValue(string name)
    {
        var v = _reader!.GetAttribute(name);
        return v ?? string.Empty;
    }

    // cpp parity: SAXHandler.h:121 — getRequiredAttrValue, throws if missing/empty.
    private string GetRequiredAttrValue(string name)
    {
        var v = _reader!.GetAttribute(name);
        if (string.IsNullOrEmpty(v))
            throw new BlibException(true,
                $"Missing required attribute '{name}' in {GetFileName()} (element '{_reader.LocalName}').");
        return v;
    }

    private int GetIntRequiredAttrValue(string name)
    {
        var s = GetRequiredAttrValue(name);
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
        {
            throw new BlibException(true,
                $"The value '{s}' in attribute '{name}' is not a valid integer (element '{_reader!.LocalName}', file {GetFileName()}).");
        }
        return v;
    }

    private double GetDoubleRequiredAttrValue(string name)
    {
        var s = GetRequiredAttrValue(name);
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            throw new BlibException(true,
                $"The value '{s}' in attribute '{name}' is not a valid floating point value (element '{_reader!.LocalName}', file {GetFileName()}).");
        }
        return v;
    }

    // ----------------------------------------------------------------------------------
    // Element handlers — direct port of PrideXmlReader.cpp:71 startElement /
    // PrideXmlReader.cpp:129 endElement.
    // ----------------------------------------------------------------------------------

    // cpp parity: PrideXmlReader.cpp:71.
    private void StartElement(string name)
    {
        if (IsElement("spectrum", name))
        {
            ParseSpectrum();
        }
        else if (IsElement("ionSelection", name))
        {
            NewState(ParserState.IonSelection);
        }
        else if (IsElement("cvParam", name))
        {
            ParseCvParam();
        }
        else if (IsElement("mzArrayBinary", name))
        {
            NewState(ParserState.PeaksMz);
        }
        else if (IsElement("intenArrayBinary", name))
        {
            NewState(ParserState.PeaksIntensity);
        }
        else if (IsElement("data", name))
        {
            ParseData();
        }
        else if (IsElement("PeptideItem", name))
        {
            ParsePeptideItem();
        }
        else if (IsElement("Sequence", name))
        {
            PrepareCharRead(ParserState.PeptideSequence);
        }
        else if (IsElement("SpectrumReference", name))
        {
            PrepareCharRead(ParserState.SpectrumReference);
        }
        else if (IsElement("ModificationItem", name))
        {
            ParseModificationItem();
        }
        else if (IsElement("ModLocation", name))
        {
            PrepareCharRead(ParserState.ModLocation);
        }
        else if (IsElement("ModMonoDelta", name))
        {
            PrepareCharRead(ParserState.ModMonoDelta);
        }
    }

    // cpp parity: PrideXmlReader.cpp:129.
    private void EndElement(string name)
    {
        if (IsElement("spectrum", name))
        {
            SaveSpectrum();
        }
        else if (IsElement("ionSelection", name))
        {
            LastState();
        }
        else if (IsElement("mzArrayBinary", name))
        {
            LastState();
        }
        else if (IsElement("intenArrayBinary", name))
        {
            LastState();
        }
        else if (IsElement("data", name) &&
                 (_state == ParserState.PeaksMzData || _state == ParserState.PeaksIntensityData))
        {
            EndData();
        }
        else if (IsElement("PeptideItem", name))
        {
            EndPeptideItem();
        }
        else if (IsElement("Sequence", name))
        {
            EndSequence();
        }
        else if (IsElement("SpectrumReference", name))
        {
            EndSpectrumReference();
        }
        else if (IsElement("ModificationItem", name))
        {
            // cpp parity: PrideXmlReader.cpp:166.
            CurPsm!.Mods.Add(_curMod);
        }
        else if (IsElement("ModLocation", name))
        {
            EndModLocation();
        }
        else if (IsElement("ModMonoDelta", name))
        {
            EndModMonoDelta();
        }
    }

    // cpp parity: PrideXmlReader.cpp:178.
    private void ParseSpectrum()
    {
        _numMzs = 0;
        _numIntensities = 0;

        _curSpec = new SpecData
        {
            Id = GetIntRequiredAttrValue("id"),
        };
    }

    // cpp parity: PrideXmlReader.cpp:189.
    private void ParseCvParam()
    {
        // cpp parity: PrideXmlReader.cpp:192 — lower-case for case-insensitive comparison.
        var nameAttr = GetRequiredAttrValue("name").ToLowerInvariant();

        if (_state == ParserState.IonSelection)
        {
            // cpp parity: PrideXmlReader.cpp:198 — selected ion m/z (PSI:1000040 or MS:1000744).
            if (nameAttr == "mass to charge ratio" || nameAttr == "selected ion m/z")
            {
                _curSpec!.Mz = GetDoubleRequiredAttrValue("value");
            }
            else if (nameAttr == "charge state") // PSI:1000041 ; MS:1000041
            {
                var chargeState = GetIntRequiredAttrValue("value");
                _spectraChargeStates[_curSpec!.Id] = chargeState;
            }
            else if (nameAttr == "parent ion retention time") // PRIDE:0000203
            {
                _curSpec!.RetentionTime = GetDoubleRequiredAttrValue("value");
            }
            else if (nameAttr == "retention time") // PSI:RETENTION TIME ; MS:1000894
            {
                // cpp parity: PrideXmlReader.cpp:217 — try "PT<seconds>S" first; on failure parse
                // as a bare number. cpp converts to minutes either way.
                var rtStr = GetRequiredAttrValue("value");
                _curSpec!.RetentionTime = ParseRetentionTime(rtStr);
            }
        }
        else if (_state == ParserState.PeptideItem)
        {
            // cpp parity: PrideXmlReader.cpp:228 — m/z found in PeptideItem, save for later.
            if (nameAttr == "mass to charge ratio" || nameAttr == "selected ion m/z")
            {
                _foundMz = GetDoubleRequiredAttrValue("value");
            }
            // cpp parity: PrideXmlReader.cpp:237 — charge state on the PSM itself.
            if (nameAttr == "charge state")
            {
                CurPsm!.Charge = GetIntRequiredAttrValue("value");
            }
            else
            {
                // cpp parity: PrideXmlReader.cpp:244 — determine score type, if any.
                var curType = PsmScoreType.UnknownScoreType;
                if (nameAttr == "x correlation" || nameAttr == "sequest:xcorr")
                {
                    curType = PsmScoreType.SequestXCorr;
                }
                else if (nameAttr == "mascot score" || nameAttr == "mascot:score")
                {
                    curType = PsmScoreType.MascotIonsScore;
                }
                else if (nameAttr == "expect" || nameAttr == "x!tandem:expect")
                {
                    curType = PsmScoreType.TandemExpectationValue;
                    if (_scoreType == PsmScoreType.UnknownScoreType) SetThreshold(BuildInput.Tandem, isMax: true);
                }
                else if (nameAttr == "spectrum mill peptide score" || nameAttr == "spectrummill:score")
                {
                    curType = PsmScoreType.SpectrumMill;
                }
                else if (nameAttr == "percolator:q value")
                {
                    curType = PsmScoreType.PercolatorQValue;
                }
                else if (nameAttr == "peptideprophet probability score")
                {
                    curType = PsmScoreType.PeptideProphetSomething;
                    if (_scoreType == PsmScoreType.UnknownScoreType) SetThreshold(BuildInput.PepXml, isMax: false);
                }
                else if (nameAttr == "scaffold:peptide probability")
                {
                    curType = PsmScoreType.ScaffoldSomething;
                    if (_scoreType == PsmScoreType.UnknownScoreType) SetThreshold(BuildInput.Scaffold, isMax: false);
                }
                else if (nameAttr == "omssa e-value" || nameAttr == "omssa:evalue")
                {
                    curType = PsmScoreType.OmssaExpectationScore;
                    if (_scoreType == PsmScoreType.UnknownScoreType) SetThreshold(BuildInput.Omssa, isMax: true);
                }
                else if (nameAttr == "proteinprospector:expectation value")
                {
                    curType = PsmScoreType.ProteinProspectorExpect;
                    if (_scoreType == PsmScoreType.UnknownScoreType) SetThreshold(BuildInput.ProtProspect, isMax: true);
                }

                // cpp parity: PrideXmlReader.cpp:295 — recognised score type.
                if (curType != PsmScoreType.UnknownScoreType)
                {
                    if (_scoreType == PsmScoreType.UnknownScoreType)
                    {
                        _scoreType = curType;
                        if (_isScoreLookup)
                        {
                            throw new EndEarlyException();
                        }
                    }

                    if (_scoreType == curType)
                    {
                        CurPsm!.Score = GetDoubleRequiredAttrValue("value");
                    }
                    else
                    {
                        Verbosity.Warn(
                            $"Skipping unexpected score type, expected {BlibUtils.ScoreTypeToString(_scoreType)} " +
                            $"but was {BlibUtils.ScoreTypeToString(curType)}");
                    }
                }
            }
        }
    }

    // cpp parity: PrideXmlReader.cpp:219 — `sscanf(rtStr, "PT%lfS", &rt)`; on success rt is in
    // seconds and we convert to minutes. Otherwise atof the whole string and divide.
    private static double ParseRetentionTime(string rtStr)
    {
        if (rtStr.Length >= 4 && rtStr.StartsWith("PT", StringComparison.Ordinal))
        {
            var inner = rtStr.Substring(2);
            // cpp parity: PrideXmlReader.cpp:219 — accepts trailing 'S' or anything matching the
            // %lf format conversion. Use the prefix up to (but not including) the 'S' if present.
            var sIdx = inner.AsSpan().IndexOfAny(_ptDurationTerminators);
            if (sIdx >= 0)
                inner = inner.Substring(0, sIdx);
            if (double.TryParse(inner, NumberStyles.Float, CultureInfo.InvariantCulture, out var rtSec))
                return rtSec / 60.0;
        }
        if (double.TryParse(rtStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var rtBare))
            return rtBare / 60.0;
        return 0;
    }

    // cpp parity: PrideXmlReader.cpp:321.
    private void ParseData()
    {
        var endian = GetRequiredAttrValue("endian");
        var length = GetIntRequiredAttrValue("length");
        var precision = GetRequiredAttrValue("precision");

        _curBinaryConfig.BigEndian = string.Equals(endian, "big", StringComparison.Ordinal);
        _curBinaryConfig.Precision32 = string.Equals(precision, "32", StringComparison.Ordinal);

        if (_state == ParserState.PeaksMz)
        {
            _numMzs = length;
            PrepareCharRead(ParserState.PeaksMzData);
        }
        else if (_state == ParserState.PeaksIntensity)
        {
            _numIntensities = length;
            PrepareCharRead(ParserState.PeaksIntensityData);
        }
    }

    // cpp parity: PrideXmlReader.cpp:342.
    private void EndData()
    {
        if (_state == ParserState.PeaksMzData)
        {
            var decoded = DecodeBase64Doubles(_charBuf, _curBinaryConfig);

            if (decoded.Length != _numMzs)
            {
                // cpp parity: PrideXmlReader.cpp:353 — check if the length attribute was the
                // number of bytes (i.e. raw byte count, not element count).
                var decodedBytes = GetDecodedNumBytes(_charBuf);
                if (decodedBytes != _numMzs)
                {
                    Verbosity.Warn(
                        $"Length attribute ({_numMzs}) did not match number of m/zs ({decoded.Length}) " +
                        $"or bytes ({decodedBytes})");
                }
                _numMzs = decoded.Length;
            }

            _curSpec!.Mzs = decoded;
            LastState();
        }
        else if (_state == ParserState.PeaksIntensityData)
        {
            var decoded = DecodeBase64Doubles(_charBuf, _curBinaryConfig);

            if (decoded.Length != _numIntensities)
            {
                var decodedBytes = GetDecodedNumBytes(_charBuf);
                if (decodedBytes != _numIntensities)
                {
                    Verbosity.Warn(
                        $"Length attribute ({_numIntensities}) did not match number of intensities ({decoded.Length}) " +
                        $"or bytes ({decodedBytes})");
                }
                _numIntensities = decoded.Length;
            }

            // cpp parity: PrideXmlReader.cpp:387 — narrow doubles to float for intensities.
            var intens = new float[decoded.Length];
            for (int i = 0; i < decoded.Length; i++)
                intens[i] = (float)decoded[i];
            _curSpec!.Intensities = intens;

            LastState();
        }
    }

    // cpp parity: PrideXmlReader.cpp:396.
    private static long GetDecodedNumBytes(string base64)
    {
        // strip whitespace mentally — but we mirror the cpp arithmetic which doesn't.
        if (base64.Length % 4 != 0)
            return -1;
        int padding = 0;
        for (int i = base64.Length - 1; i >= 0 && base64[i] == '='; i--)
            padding++;
        return base64.Length / 4 * 3 - padding;
    }

    /// <summary>
    /// Decode a base64-encoded binary blob into a double[] respecting endianness and 32/64-bit
    /// precision. cpp parity: <c>pwiz::msdata::BinaryDataEncoder::decode</c> as configured at
    /// PrideXmlReader.cpp:327. The cpp encoder also handles zlib decompression, but the cpp
    /// constructor at PrideXmlReader.cpp:48 disables compression — so do we.
    /// </summary>
    private static double[] DecodeBase64Doubles(string base64, BinaryConfig config)
    {
        // cpp's encoder tolerates internal whitespace because base64 round-trips it; we strip it
        // explicitly. The C# Convert.FromBase64String DOES tolerate base64 whitespace (it skips
        // whitespace internally), so we can pass the string straight through.
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new BlibException(true, $"Failed to decode base64 peak data: {ex.Message}");
        }

        var bytesPerValue = config.Precision32 ? 4 : 8;
        var count = bytes.Length / bytesPerValue;
        var result = new double[count];

        // BitConverter is little-endian on every supported platform. Swap if big-endian source
        // (cpp's ByteOrder_BigEndian path; PRIDE files in our tests are big-endian).
        var swap = config.BigEndian == BitConverter.IsLittleEndian;

        // Hoist a single 8-byte scratch buffer out of the loop (CA2014).
        Span<byte> scratch = stackalloc byte[8];
        for (int i = 0; i < count; i++)
        {
            var offset = i * bytesPerValue;
            if (config.Precision32)
            {
                if (swap)
                {
                    scratch[0] = bytes[offset + 3];
                    scratch[1] = bytes[offset + 2];
                    scratch[2] = bytes[offset + 1];
                    scratch[3] = bytes[offset + 0];
                    result[i] = BitConverter.ToSingle(scratch[..4]);
                }
                else
                {
                    result[i] = BitConverter.ToSingle(bytes, offset);
                }
            }
            else
            {
                if (swap)
                {
                    scratch[0] = bytes[offset + 7];
                    scratch[1] = bytes[offset + 6];
                    scratch[2] = bytes[offset + 5];
                    scratch[3] = bytes[offset + 4];
                    scratch[4] = bytes[offset + 3];
                    scratch[5] = bytes[offset + 2];
                    scratch[6] = bytes[offset + 1];
                    scratch[7] = bytes[offset + 0];
                    result[i] = BitConverter.ToDouble(scratch);
                }
                else
                {
                    result[i] = BitConverter.ToDouble(bytes, offset);
                }
            }
        }

        return result;
    }

    // cpp parity: PrideXmlReader.cpp:414.
    private void ParsePeptideItem()
    {
        CurPsm = new PSM();
        _foundMz = 0;
        NewState(ParserState.PeptideItem);
    }

    // cpp parity: PrideXmlReader.cpp:421.
    private void EndPeptideItem()
    {
        // cpp parity: PrideXmlReader.cpp:424 — m/z found inside PeptideItem, save it to the
        // spectrum it points at if that spectrum hadn't supplied its own.
        if (_foundMz > 0)
        {
            if (!_spectra.TryGetValue(CurPsm!.SpecIndex, out var found))
            {
                Verbosity.Warn($"Failed saving m/z to spectrum {CurPsm.SpecIndex}");
            }
            else if (found.Mz <= 0)
            {
                found.Mz = _foundMz;
            }
        }

        // cpp parity: PrideXmlReader.cpp:439 — clamp mod positions to [1, seq length].
        for (int i = 0; i < CurPsm!.Mods.Count; i++)
        {
            var m = CurPsm.Mods[i];
            var clamped = Math.Min(CurPsm.UnmodSeq.Length, Math.Max(m.Position, 1));
            if (clamped != m.Position)
                CurPsm.Mods[i] = new SeqMod(clamped, m.DeltaMass);
        }

        // cpp parity: PrideXmlReader.cpp:447 — save psm if above threshold.
        if ((CurPsm.Score >= _threshold && !_thresholdIsMax) ||
            (CurPsm.Score <= _threshold && _thresholdIsMax))
        {
            Psms.Add(CurPsm);
        }
        else
        {
            FilteredOutPsmCount++;
        }

        LastState();
    }

    // cpp parity: PrideXmlReader.cpp:459.
    private void EndSequence()
    {
        CurPsm!.UnmodSeq = _charBuf;
        LastState();
    }

    // cpp parity: PrideXmlReader.cpp:466.
    private void EndSpectrumReference()
    {
        if (!int.TryParse(_charBuf.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var specRef))
            specRef = 0;
        CurPsm!.SpecIndex = specRef;

        // cpp parity: PrideXmlReader.cpp:471 — pull charge from the spectrum's charge state, if any.
        if (_spectraChargeStates.TryGetValue(specRef, out var charge))
            CurPsm.Charge = charge;

        LastState();
    }

    // cpp parity: PrideXmlReader.cpp:476.
    private void ParseModificationItem()
    {
        _curMod = new SeqMod(-1, 0);
    }

    // cpp parity: PrideXmlReader.cpp:482.
    private void EndModLocation()
    {
        if (!int.TryParse(_charBuf.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var loc))
            loc = 0;
        _curMod = new SeqMod(loc, _curMod.DeltaMass);
        LastState();
    }

    // cpp parity: PrideXmlReader.cpp:490.
    private void EndModMonoDelta()
    {
        if (!double.TryParse(_charBuf.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var delta))
            delta = 0;
        _curMod = new SeqMod(_curMod.Position, delta);
        LastState();
    }

    // cpp parity: PrideXmlReader.cpp:498.
    private void PrepareCharRead(ParserState dataState)
    {
        _charBuf = string.Empty;
        NewState(dataState);
    }

    /// <summary>Handler for all characters between tags.</summary>
    /// <remarks>cpp parity: PrideXmlReader.cpp:526.</remarks>
    private void Characters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (_state == ParserState.PeaksMzData ||
            _state == ParserState.PeaksIntensityData ||
            _state == ParserState.PeptideSequence ||
            _state == ParserState.SpectrumReference ||
            _state == ParserState.ModLocation ||
            _state == ParserState.ModMonoDelta)
        {
            _charBuf += text;
        }
    }

    // cpp parity: PrideXmlReader.cpp:544.
    private void NewState(ParserState next)
    {
        _stateHistory.Push(_state);
        _state = next;
    }

    // cpp parity: PrideXmlReader.cpp:554.
    private void LastState()
    {
        _state = _stateHistory.Pop();
    }

    /// <summary>
    /// Saves the current data as a new spectrum in the spectra map. cpp parity:
    /// PrideXmlReader.cpp:566.
    /// </summary>
    private void SaveSpectrum()
    {
        if (_curSpec is null)
            return;

        if (_numIntensities != _numMzs)
        {
            throw new BlibException(false,
                $"Different numbers of peaks. Spectrum {_curSpec.Id} has " +
                $"{_numMzs} fragment m/z values and {_numIntensities} intensities.");
        }
        _curSpec.NumPeaks = _numMzs;

        Verbosity.Debug($"Saving spectrum id {_curSpec.Id}.");
        _spectra[_curSpec.Id] = _curSpec;
        _curSpec = null;
    }

    // cpp parity: PrideXmlReader.cpp:583.
    private void SetThreshold(BuildInput type, bool isMax)
    {
        _threshold = GetScoreThreshold(type);
        _thresholdIsMax = isMax;
    }

    // ----------------------------------------------------------------------------------
    // Inner spec reader. cpp's PrideXmlReader implements SpecFileReader directly
    // (PrideXmlReader.cpp:589); mirror with an inner reader bound to _spectra.
    // ----------------------------------------------------------------------------------

    /// <summary>
    /// Spec-file reader that returns peaks straight off the parent reader's
    /// <see cref="PrideXmlReader._spectra"/> map. cpp parity: PrideXmlReader.cpp:589
    /// (the cpp class is its own SpecFileReader).
    /// </summary>
    private sealed class PrideSpecFileReader : SpecFileReaderBase
    {
        private readonly PrideXmlReader _parent;

        public PrideSpecFileReader(PrideXmlReader parent)
        {
            _parent = parent;
        }

        // cpp parity: PrideXmlReader.cpp:594 — no-op, peaks are already in memory.
        public override void OpenFile(string path, bool mzSort = false)
        {
        }

        public override SpecIdType IdType
        {
            set { /* no-op, cpp parity: PrideXmlReader.cpp:599 */ }
        }

        // cpp parity: PrideXmlReader.cpp:605 — integer-key lookup is the only working overload.
        public override bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true)
        {
            ArgumentNullException.ThrowIfNull(returnData);

            if (!_parent._spectra.TryGetValue(identifier, out var found))
                return false;

            returnData.CopyFrom(found);
            return true;
        }

        // cpp parity: PrideXmlReader.cpp:621 — string-name lookup not supported.
        public override bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true)
        {
            Verbosity.Warn("PrideXmlReader cannot fetch spectra by string identifier, only by spectrum index.");
            return false;
        }

        // cpp parity: PrideXmlReader.cpp:634 — sequential reading not supported.
        public override bool GetNextSpectrum(SpecData returnData, bool getPeaks = true)
        {
            Verbosity.Warn("PrideXmlReader does not support sequential file reading.");
            return false;
        }
    }
}
