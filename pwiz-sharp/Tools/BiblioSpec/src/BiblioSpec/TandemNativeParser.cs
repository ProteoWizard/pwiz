// Port of pwiz_tools/BiblioSpec/src/TandemNativeParser.{h,cpp}
//
// Reads X! Tandem native output (.xtan.xml). Unlike pepXML readers, the .xtan.xml file
// carries both the PSMs and the MS/MS peaks inline — the peaks live in nested
// <group type="support" label="fragment ion mass spectrum"> blocks with
// <GAML:Xdata>/<GAML:Ydata> children whose <GAML:values> elements contain whitespace-
// separated number lists. The reader therefore doubles as its own SpecFileReader,
// returning peaks straight off an in-memory map keyed by scan id.

using System.Globalization;
using System.Xml;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// BiblioSpec reader for X! Tandem native <c>.xtan.xml</c> output.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::TandemNativeParser</c>
/// (<c>pwiz_tools/BiblioSpec/src/TandemNativeParser.{h,cpp}</c>). The cpp reader is a
/// SAXHandler driven by Expat; this port walks the document with
/// <see cref="XmlReader"/>, using the same state-machine and per-element handlers so
/// cpp parity is easy to verify line-by-line.</para>
/// <para>The cpp reader also implements <c>SpecFileReader</c> directly (peaks come from
/// the same .xtan.xml). The C# port mirrors that by installing an inner
/// <see cref="TandemSpecFileReader"/> on <see cref="BuildParser.SpecReader"/> that hands
/// back peaks out of an in-memory map keyed by spectrum key.</para>
/// </remarks>
public sealed class TandemNativeParser : BuildParser
{
    /// <summary>
    /// cpp parity: TandemNativeParser.h:66 — STATE enum drives the
    /// startElement/endElement state machine.
    /// </summary>
    private enum ParserState
    {
        RootState,
        PsmGroupState,             // highest level group
        DescriptionState,          // filename, scan, charge
        DomainState,
        NestedGroupState,          // within psm_group
        PeaksState,                // fragment ion mass spectrum nested group
        PeaksMzState,              // Xdata of peaks_state
        PeaksIntensityState,       // Ydata of peaks_state
        ResidueMassParametersState,
    }

    // cpp parity: TandemNativeParser.h:105 — score cutoff for X! Tandem expect values.
    private readonly double _probCutOff;

    // cpp parity: TandemNativeParser.h:106-107 — state stack + current state.
    private readonly Stack<ParserState> _stateHistory = new();
    private ParserState _curState = ParserState.RootState;

    // cpp parity: TandemNativeParser.h:108-113 — per-PSM transient state.
    private double _mass;             // precursor m/z doesn't appear to be stored in file
    private int _seqStart;            // mod positions are given relative to the protein
    private double _retentionTime;
    private string _retentionTimeStr = string.Empty;
    private string _descriptionStr = string.Empty; // filename, scan, charge
    private string _curFilename = string.Empty;    // taken from description

    // cpp parity: TandemNativeParser.h:114 — psms stored by spec filename so a single
    // .xtan.xml feeding multiple raw files (rare but legal) flushes them as separate
    // SpectrumSourceFiles rows.
    private readonly Dictionary<string, List<PSM?>> _fileMap = new(StringComparer.Ordinal);

    // cpp parity: TandemNativeParser.h:117-122 — peaks accumulators. cpp uses raw
    // double*/float* arrays; here we accumulate the value strings during PEAKS_*_STATE
    // and parse them when the spectrum saves.
    private string _mzStr = string.Empty;
    private string _intensityStr = string.Empty;
    private int _numMzs;
    private int _numIntensities;

    // cpp parity: TandemNativeParser.h:123 — spectra map keyed by specKey (scan id).
    private readonly Dictionary<int, SpecData> _spectra = new();

    // cpp parity: TandemNativeParser.h:126-127 — residue mass table + per-residue mod table
    // populated from a <group type="parameters" label="residue mass parameters"> block.
    private readonly double[] _aaMasses = new double[128];
    private readonly Dictionary<char, double> _aaMods = new();

    // Active XmlReader during parse, used by the GetAttrValue helpers below.
    private XmlReader? _reader;

    /// <summary>
    /// Returns true if <paramref name="path"/> is an X! Tandem native <c>.xtan.xml</c>
    /// file. Used by <see cref="BlibBuilder"/>'s reader-factory dispatch.
    /// </summary>
    public static bool AcceptsExtension(string path) =>
        BlibBuilder.HasExtensionCi(path, ".xtan.xml");

    /// <summary>
    /// Construct a TandemNativeParser bound to <paramref name="maker"/> and the file at
    /// <paramref name="xmlFilename"/>.
    /// </summary>
    /// <remarks>cpp parity: TandemNativeParser.cpp:43.</remarks>
    public TandemNativeParser(BlibBuilder maker, string xmlFilename, ProgressIndicator? parentProgress)
        : base(maker, xmlFilename, parentProgress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlFilename);

        _probCutOff = GetScoreThreshold(BuildInput.Tandem);

        // cpp parity: TandemNativeParser.cpp:54 — set up the spec filename as the .xtan.xml
        // itself (we won't go looking for an external spec file).
        SetSpecFileName(xmlFilename, checkFile: false);

        // cpp parity: TandemNativeParser.cpp:60 — `delete specReader_; specReader_ = this`.
        // We mirror that with an inner reader bound to _spectra.
        SpecReader = new TandemSpecFileReader(this);

        // cpp parity: TandemNativeParser.cpp:63 — initialise monoisotopic residue masses.
        AminoAcidMasses.InitializeMass(_aaMasses, monoisotopic: true);
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: TandemNativeParser.cpp:125.</remarks>
    public override bool ParseFile()
    {
        Parse();

        // cpp parity: TandemNativeParser.cpp:131 — flush by spectrum filename.
        foreach (var kvp in _fileMap)
        {
            Psms.Clear();
            foreach (var psm in kvp.Value)
                Psms.Add(psm);
            BuildTables(PsmScoreType.TandemExpectationValue, kvp.Key);
        }
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: TandemNativeParser.cpp:141.</remarks>
    public override IList<PsmScoreType> GetScoreTypes() =>
        new[] { PsmScoreType.TandemExpectationValue };

    // ----------------------------------------------------------------------------------
    // XmlReader driver. cpp's SAXHandler::parse() walks the document with Expat. We
    // mirror that with XmlReader, dispatching to StartElement/EndElement and feeding
    // text into Characters for the GAML:values content + Description note.
    // ----------------------------------------------------------------------------------

    private void Parse()
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = false, // need text inside <GAML:values> + <note>
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
                        // cpp parity: SAXHandler delivers Name (qualified) to startElement, e.g.
                        // "GAML:Xdata" — XmlReader's Name property preserves the prefix the same
                        // way, so we use Name rather than LocalName here.
                        var name = reader.Name;
                        StartElement(name);
                        if (isEmpty)
                        {
                            // SAXHandler emits a matching endElement for self-closing elements too.
                            EndElement(name);
                        }
                        break;
                    }
                    case XmlNodeType.EndElement:
                        EndElement(reader.Name);
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

    // ----------------------------------------------------------------------------------
    // SAXHandler-style helpers (mirror cpp SAXHandler.h:96+).
    // ----------------------------------------------------------------------------------

    private static bool IsElement(string expected, string actual) =>
        string.Equals(expected, actual, StringComparison.Ordinal);

    private string GetAttrValue(string name)
    {
        var v = _reader!.GetAttribute(name);
        return v ?? string.Empty;
    }

    private string GetRequiredAttrValue(string name)
    {
        var v = _reader!.GetAttribute(name);
        if (string.IsNullOrEmpty(v))
            throw new BlibException(true,
                $"Missing required attribute '{name}' in {GetFileName()} (element '{_reader.Name}').");
        return v;
    }

    private int GetIntRequiredAttrValue(string name)
    {
        var s = GetRequiredAttrValue(name);
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
        {
            throw new BlibException(true,
                $"The value '{s}' in attribute '{name}' is not a valid integer (element '{_reader!.Name}', file {GetFileName()}).");
        }
        return v;
    }

    private double GetDoubleRequiredAttrValue(string name)
    {
        var s = GetRequiredAttrValue(name);
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            throw new BlibException(true,
                $"The value '{s}' in attribute '{name}' is not a valid floating point value (element '{_reader!.Name}', file {GetFileName()}).");
        }
        return v;
    }

    // ----------------------------------------------------------------------------------
    // Element dispatch — direct port of TandemNativeParser.cpp:80 startElement /
    // TandemNativeParser.cpp:108 endElement.
    // ----------------------------------------------------------------------------------

    private void StartElement(string name)
    {
        if (IsElement("group", name))
        {
            ParseGroup();
        }
        else if (IsElement("file", name))
        {
            ParseSpectraFile();
        }
        else if (IsElement("note", name))
        {
            ParseNote();
        }
        else if (IsElement("domain", name))
        {
            NewState(ParserState.DomainState);
            ParseDomain();
        }
        else if (IsElement("aa", name))
        {
            ParseMod();
        }
        else if (IsElement("GAML:Xdata", name) && _curState == ParserState.PeaksState)
        {
            NewState(ParserState.PeaksMzState);
        }
        else if (IsElement("GAML:Ydata", name) && _curState == ParserState.PeaksState)
        {
            NewState(ParserState.PeaksIntensityState);
        }
        else if (IsElement("GAML:values", name))
        {
            ParseValues();
        }
    }

    private void EndElement(string name)
    {
        if (IsElement("group", name))
        {
            EndGroup();
        }
        else if (IsElement("note", name))
        {
            EndNote();
        }
        else if (IsElement("domain", name))
        {
            EndDomain();
        }
        else if (IsElement("GAML:Xdata", name) && _curState == ParserState.PeaksMzState)
        {
            _curState = GetLastState();
        }
        else if (IsElement("GAML:Ydata", name) && _curState == ParserState.PeaksIntensityState)
        {
            _curState = GetLastState();
        }
    }

    // ----------------------------------------------------------------------------------
    // Per-element handlers. cpp parity comments cite line numbers in TandemNativeParser.cpp.
    // ----------------------------------------------------------------------------------

    // cpp parity: TandemNativeParser.cpp:150 — three possible group types: model, support, parameter.
    private void ParseGroup()
    {
        var type = GetRequiredAttrValue("type");
        var label = GetAttrValue("label");

        if (string.Equals(type, "model", StringComparison.Ordinal))
        {
            _curState = ParserState.PsmGroupState;
            ParsePsm();
        }
        else if (string.Equals(type, "support", StringComparison.Ordinal))
        {
            if (string.Equals(label, "fragment ion mass spectrum", StringComparison.Ordinal))
            {
                NewState(ParserState.PeaksState);
            }
            else
            {
                NewState(ParserState.NestedGroupState);
            }
        }
        else if (string.Equals(type, "parameters", StringComparison.Ordinal))
        {
            if (string.Equals(label, "residue mass parameters", StringComparison.Ordinal))
            {
                NewState(ParserState.ResidueMassParametersState);
            }
        }
        // type == something else, no-op (cpp parity)
    }

    // cpp parity: TandemNativeParser.cpp:178 — extract filename from a <file> element.
    private void ParseSpectraFile()
    {
        var typeAttr = GetAttrValue("type");
        if (_curState == ParserState.PeaksState && string.Equals(typeAttr, "spectra", StringComparison.Ordinal))
        {
            _curFilename = GetAttrValue("URL");
        }
    }

    // cpp parity: TandemNativeParser.cpp:189 — handle a <note> element.
    private void ParseNote()
    {
        var label = GetAttrValue("label");
        var description = string.Equals(label, "Description", StringComparison.Ordinal);
        if (description)
        {
            _retentionTimeStr = string.Empty;
            _descriptionStr = string.Empty;
            if (string.IsNullOrEmpty(_curFilename) && _curState == ParserState.PeaksState)
            {
                NewState(ParserState.DescriptionState);
            }
        }
    }

    // cpp parity: TandemNativeParser.cpp:203 — domain holds the matched sequence.
    private void ParseDomain()
    {
        if (CurPsm is null)
        {
            throw new BlibException(false,
                "TandemNativeParser encountered a domain without an accompanying model group.");
        }
        if (string.IsNullOrEmpty(CurPsm.UnmodSeq))
        {
            CurPsm.UnmodSeq = GetRequiredAttrValue("seq");
            ApplyResidueMassParameters(CurPsm);
            _seqStart = GetIntRequiredAttrValue("start");
        }
        else
        {
            // cpp parity: TandemNativeParser.cpp:214 — assume sequences match across domains.
            var seq = GetRequiredAttrValue("seq");
            if (!string.Equals(seq, CurPsm.UnmodSeq, StringComparison.Ordinal))
            {
                throw new BlibException(false,
                    $"Two different sequences given for id {CurPsm.SpecKey.ToString(CultureInfo.InvariantCulture)}, " +
                    $"{CurPsm.UnmodSeq} and {seq}.");
            }
        }
    }

    // cpp parity: TandemNativeParser.cpp:227 — <aa> element either describes residue mass
    // parameters or a modification on the current PSM.
    private void ParseMod()
    {
        var aa = GetRequiredAttrValue("type");
        if (string.IsNullOrEmpty(aa))
            return;
        var aaChar = aa[0];

        if (_curState == ParserState.ResidueMassParametersState)
        {
            var mass = GetDoubleRequiredAttrValue("mass");
            var diff = mass - _aaMasses[aaChar];
            if (Math.Abs(diff) > 0.1)
            {
                _aaMods[aaChar] = diff;
            }
            return;
        }

        if (CurPsm is null)
        {
            throw new BlibException(false,
                "TandemNativeParser encountered a modification without an accompanying model group.");
        }

        var protPosition = GetIntRequiredAttrValue("at");
        var deltaMass = GetDoubleRequiredAttrValue("modified");

        Verbosity.Debug(
            $"Found modified {aa} at position {protPosition.ToString(CultureInfo.InvariantCulture)} " +
            $"with delta mass {deltaMass.ToString(CultureInfo.InvariantCulture)}.");

        // change the position to be relative to the seq start, not the protein start
        var seqPosition = protPosition - _seqStart; // + 1?

        // confirm that the modified aa is present in that position in the seq
        if (seqPosition < 0 || seqPosition >= CurPsm.UnmodSeq.Length ||
            CurPsm.UnmodSeq[seqPosition] != aaChar)
        {
            var actual = (seqPosition >= 0 && seqPosition < CurPsm.UnmodSeq.Length)
                ? CurPsm.UnmodSeq[seqPosition].ToString()
                : "<out of range>";
            throw new BlibException(false,
                $"Specified modification does not match sequence. Given a modified {aaChar} at position " +
                $"{seqPosition.ToString(CultureInfo.InvariantCulture)} which is a {actual} in {CurPsm.UnmodSeq}.");
        }

        // mods are 1-based
        CurPsm.Mods.Add(new SeqMod(seqPosition + 1, deltaMass));
    }

    // cpp parity: TandemNativeParser.cpp:274 — pull id/mass/charge/score from a model group.
    private void ParsePsm()
    {
        CurPsm = new PSM
        {
            Charge = GetIntRequiredAttrValue("z"),
            SpecKey = GetIntRequiredAttrValue("id"),
            Score = GetDoubleRequiredAttrValue("expect"),
        };
        _mass = GetDoubleRequiredAttrValue("mh");

        var timeStr = GetAttrValue("rt");
        if (!string.IsNullOrEmpty(timeStr))
        {
            // cpp parity: TandemNativeParser.cpp:283 — sscanf(timeStr, "PT%lfS", &rt) -> seconds; else bare minutes.
            if (timeStr.StartsWith("PT", StringComparison.Ordinal))
            {
                var inner = timeStr.AsSpan(2);
                var endIdx = inner.IndexOf('S');
                if (endIdx >= 0)
                    inner = inner.Slice(0, endIdx);
                if (double.TryParse(inner, NumberStyles.Float, CultureInfo.InvariantCulture, out var rtSec))
                {
                    _retentionTime = rtSec / 60.0;
                }
            }
            else if (double.TryParse(timeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var rtMin))
            {
                _retentionTime = rtMin;
            }
        }
    }

    // cpp parity: TandemNativeParser.cpp:294 — <GAML:values numvalues="..."> opens an array.
    private void ParseValues()
    {
        var numValues = GetIntRequiredAttrValue("numvalues");
        if (_curState == ParserState.PeaksMzState)
        {
            _numMzs = numValues;
        }
        else if (_curState == ParserState.PeaksIntensityState)
        {
            _numIntensities = numValues;
        }
        // else values for some other kind of data we don't care about
    }

    // cpp parity: TandemNativeParser.cpp:319 — text content handler.
    private void Characters(string s)
    {
        if (string.IsNullOrEmpty(s))
            return;

        if (_curState == ParserState.PeaksMzState)
        {
            _mzStr += s;
        }
        else if (_curState == ParserState.PeaksIntensityState)
        {
            _intensityStr += s;
        }
        else if (_curState == ParserState.DescriptionState)
        {
            _retentionTimeStr += s;
            _descriptionStr += s;
        }
    }

    // cpp parity: TandemNativeParser.cpp:365 — closes either a NESTED, PEAKS, PSM_GROUP, or
    // RESIDUE_MASS_PARAMETERS state.
    private void EndGroup()
    {
        if (_curState == ParserState.NestedGroupState)
        {
            _curState = GetLastState();
        }
        else if (_curState == ParserState.PeaksState)
        {
            _curState = GetLastState();
        }
        else if (_curState == ParserState.PsmGroupState)
        {
            _curState = ParserState.RootState;
            if (CurPsm != null)
            {
                Verbosity.Debug(
                    $"Cur psm has id {CurPsm.SpecKey.ToString(CultureInfo.InvariantCulture)}, " +
                    $"charge {CurPsm.Charge.ToString(CultureInfo.InvariantCulture)}, " +
                    $"score {CurPsm.Score.ToString(CultureInfo.InvariantCulture)}, " +
                    $"mass {_mass.ToString(CultureInfo.InvariantCulture)}, seq {CurPsm.UnmodSeq}");

                // keep psm if score passes threshold
                if (CurPsm.Score <= _probCutOff)
                {
                    SaveSpectrum();
                }

                // move psm(s) being temporarily held into the file map
                if (!_fileMap.TryGetValue(_curFilename, out var bucket))
                {
                    bucket = new List<PSM?>();
                    _fileMap[_curFilename] = bucket;
                }
                foreach (var psm in Psms)
                {
                    bucket.Add(psm);
                }
                Psms.Clear();

                // cpp parity: TandemNativeParser.cpp:398 — we kept a copy of the current
                // PSM around for the spectrum-save step; drop it now.
                CurPsm = null;
                ClearCurPeaks();
                _curFilename = string.Empty;
            }
        }
        else if (_curState == ParserState.ResidueMassParametersState)
        {
            _curState = GetLastState();

            // cpp parity: TandemNativeParser.cpp:406 — apply residue mass mods to every PSM
            // we've already collected.
            foreach (var kvp in _fileMap)
            {
                foreach (var psm in kvp.Value)
                {
                    if (psm != null)
                        ApplyResidueMassParameters(psm);
                }
            }
        }
    }

    // cpp parity: TandemNativeParser.cpp:417 — transition out of Description state.
    private void EndNote()
    {
        if (_curState != ParserState.DescriptionState)
            return;

        _curState = GetLastState();

        // cpp parity: TandemNativeParser.cpp:421 — pull RTINSECONDS=<float> out of the note text.
        const string rtStr = "RTINSECONDS=";
        var rtStart = _retentionTimeStr.IndexOf(rtStr, StringComparison.Ordinal);
        if (rtStart >= 0)
        {
            rtStart += rtStr.Length;
            var rtEnd = rtStart;
            while (rtEnd < _retentionTimeStr.Length &&
                   (char.IsDigit(_retentionTimeStr[rtEnd]) || _retentionTimeStr[rtEnd] == '.'))
            {
                rtEnd++;
            }
            var rtSpan = _retentionTimeStr.AsSpan(rtStart, rtEnd - rtStart);
            if (double.TryParse(rtSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out var rtSec))
            {
                _retentionTime = rtSec / 60.0;
            }
            // cpp atof returns 0 on bad input — we leave _retentionTime alone on parse failure,
            // which is closer to a real Tandem file (it'd skip-then-default-to-0 anyway).
        }

        // cpp parity: TandemNativeParser.cpp:431 — `File: "F:\...\foo.raw"; SpectrumID: "287"; ...`
        var fileStart = _descriptionStr.IndexOf("File:", StringComparison.Ordinal);
        if (fileStart >= 0)
        {
            fileStart += 5;
            while (fileStart < _descriptionStr.Length && _descriptionStr[fileStart] != '"')
            {
                fileStart++;
            }
            if (fileStart >= _descriptionStr.Length)
            {
                _curFilename = string.Empty;
                return;
            }
            fileStart++; // skip the opening quote
            var fileEnd = _descriptionStr.IndexOf('"', fileStart);
            if (fileEnd < 0)
            {
                _curFilename = string.Empty;
                return;
            }
            _curFilename = _descriptionStr.Substring(fileStart, fileEnd - fileStart);
            return;
        }

        // cpp parity: TandemNativeParser.cpp:451 — fall back to "<filename> scan ..."
        var spaceIdx = _descriptionStr.IndexOf(' ');
        _curFilename = spaceIdx < 0 ? _descriptionStr : _descriptionStr.Substring(0, spaceIdx);
    }

    // cpp parity: TandemNativeParser.cpp:465 — at the end of a domain, save the PSM
    // (a single PSM_GROUP may carry multiple domains for ambiguous-sequence cases).
    private void EndDomain()
    {
        _curState = GetLastState();
        if (CurPsm is null)
            return;

        if (CurPsm.Score <= _probCutOff)
        {
            Psms.Add(CurPsm);

            // create a copy of the current psm — same charge/score/specKey, fresh seq+mods.
            var tmpPsm = new PSM
            {
                Charge = CurPsm.Charge,
                SpecKey = CurPsm.SpecKey,
                Score = CurPsm.Score,
            };
            CurPsm = tmpPsm;
        }
        else
        {
            // if we aren't going to accept the PSM, just clear the seq+mods so the
            // next domain element can re-fill them.
            CurPsm.UnmodSeq = string.Empty;
            CurPsm.Mods.Clear();
        }
    }

    // cpp parity: TandemNativeParser.cpp:510 — push current state onto history, set new.
    private void NewState(ParserState nextState)
    {
        _stateHistory.Push(_curState);
        _curState = nextState;
    }

    // cpp parity: TandemNativeParser.cpp:519 — pop the previous state.
    private ParserState GetLastState() =>
        _stateHistory.Pop();

    // cpp parity: TandemNativeParser.cpp:529 — reset peak accumulators.
    private void ClearCurPeaks()
    {
        _numMzs = 0;
        _numIntensities = 0;
        _mzStr = string.Empty;
        _intensityStr = string.Empty;
    }

    // cpp parity: TandemNativeParser.cpp:545 — parse the peak strings + commit to _spectra.
    private void SaveSpectrum()
    {
        if (CurPsm is null)
            return;

        // cpp parity: TandemNativeParser.cpp:547 — convert mz/intensity strings into arrays.
        var mzs = StringsToPeaks(_mzStr, _numMzs);
        var intensities = StringsToFloatPeaks(_intensityStr, _numIntensities);

        if (mzs.Length != intensities.Length)
        {
            throw new BlibException(false,
                $"Different numbers of peaks. Spectrum {CurPsm.SpecKey.ToString(CultureInfo.InvariantCulture)} " +
                $"has {mzs.Length.ToString(CultureInfo.InvariantCulture)} fragment m/z values and " +
                $"{intensities.Length.ToString(CultureInfo.InvariantCulture)} intensities.");
        }

        // cpp parity: TandemNativeParser.cpp:558 — compute m/z from mh + charge.
        var charge = CurPsm.Charge == 0 ? 1 : CurPsm.Charge;
        var mz = _mass / charge;

        var spec = new SpecData
        {
            Mz = mz,
            NumPeaks = mzs.Length,
            Mzs = mzs,
            Intensities = intensities,
        };

        // cpp parity: TandemNativeParser.cpp:564 — MGF stores RT in *seconds* even though
        // cpp's parseNote scaled them once already to minutes; the extra divide-by-60 is
        // an explicit cpp workaround. Preserved verbatim.
        var rt = _retentionTime;
        if (BlibBuilder.HasExtensionCi(_curFilename, ".mgf"))
        {
            rt /= 60.0;
        }
        spec.RetentionTime = rt;

        _spectra[CurPsm.SpecKey] = spec;
    }

    // cpp parity: TandemNativeParser.cpp:580 — re-apply residue mass mods to a PSM after the
    // <residue mass parameters> block is read (it may show up after PSMs were collected).
    private void ApplyResidueMassParameters(PSM psm)
    {
        if (_aaMods.Count == 0)
            return;

        var seq = psm.UnmodSeq;
        for (var i = 0; i < seq.Length; i++)
        {
            if (_aaMods.TryGetValue(seq[i], out var deltaMass))
            {
                psm.Mods.Add(new SeqMod(i + 1, deltaMass));
            }
        }
    }

    // cpp parity: TandemNativeParser.cpp:331 — split a "1.2 3.4 5.6\n7.8 9.0" string into
    // a double[]. cpp uses istringstream + operator>>; the BCL equivalent is whitespace-split.
    private static double[] StringsToPeaks(string str, int expectedCount)
    {
        if (string.IsNullOrEmpty(str))
            return Array.Empty<double>();

        var tokens = str.Split(' ', '\t', '\n', '\r', '\f', '\v');
        var values = new List<double>(expectedCount > 0 ? expectedCount : 16);
        foreach (var tok in tokens)
        {
            if (tok.Length == 0) continue;
            if (!double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                continue;
            values.Add(v);
        }
        return values.ToArray();
    }

    private static float[] StringsToFloatPeaks(string str, int expectedCount)
    {
        if (string.IsNullOrEmpty(str))
            return Array.Empty<float>();

        var tokens = str.Split(' ', '\t', '\n', '\r', '\f', '\v');
        var values = new List<float>(expectedCount > 0 ? expectedCount : 16);
        foreach (var tok in tokens)
        {
            if (tok.Length == 0) continue;
            if (!float.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                continue;
            values.Add(v);
        }
        return values.ToArray();
    }

    // ----------------------------------------------------------------------------------
    // Inner spec reader. cpp's TandemNativeParser implements SpecFileReader directly
    // (TandemNativeParser.cpp:597); we mirror that with an inner reader bound to the
    // parent's _spectra map.
    // ----------------------------------------------------------------------------------

    /// <summary>
    /// Spec-file reader that returns peaks straight off the parent reader's
    /// <see cref="TandemNativeParser._spectra"/> map. cpp parity:
    /// TandemNativeParser.cpp:597 (the cpp class is its own SpecFileReader).
    /// </summary>
    private sealed class TandemSpecFileReader : SpecFileReaderBase
    {
        private readonly TandemNativeParser _parent;

        public TandemSpecFileReader(TandemNativeParser parent)
        {
            _parent = parent;
        }

        // cpp parity: TandemNativeParser.cpp:598 — no-op (file is already open).
        public override void OpenFile(string path, bool mzSort = false)
        {
        }

        public override SpecIdType IdType
        {
            set { /* cpp parity: TandemNativeParser.cpp:602 — no-op */ }
        }

        // cpp parity: TandemNativeParser.cpp:608 — int-keyed lookup (scan number / specKey).
        public override bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true)
        {
            ArgumentNullException.ThrowIfNull(returnData);

            if (!_parent._spectra.TryGetValue(identifier, out var found))
                return false;

            if (!getPeaks)
            {
                returnData.CopyFrom(found);
                returnData.NumPeaks = 0;
                returnData.Mzs = null;
                returnData.Intensities = null;
            }
            else
            {
                returnData.CopyFrom(found);
            }
            return true;
        }

        // cpp parity: TandemNativeParser.cpp:622 — warn and return false (TandemNative only
        // supports scan-number lookup).
        public override bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true)
        {
            Verbosity.Warn(
                "TandemNativeParser cannot fetch spectra by string identifier, only by scan number.");
            return false;
        }

        // cpp parity: TandemNativeParser.cpp:634 — sequential read not supported.
        public override bool GetNextSpectrum(SpecData returnData, bool getPeaks = true)
        {
            Verbosity.Warn("TandemNativeParser does not support sequential file reading.");
            return false;
        }
    }
}
