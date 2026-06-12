// Port of pwiz_tools/BiblioSpec/src/ProteinPilotReader.{h,cpp}
//
// Parses ProteinPilot .group.xml files (the output of ABI's group2xml tool, part of
// Protein Pilot). The XML carries both the PSM list and the MS/MS peaks inline as
// CDATA inside <MSMSPEAKS> elements, so the reader doubles as its own SpecFileReader —
// spectra are pulled straight from an in-memory map keyed by spectrum xml:id.

using System.Globalization;
using System.Xml;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Parses ProteinPilot <c>.group.xml</c> files produced by ABI's group2xml tool.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::ProteinPilotReader</c>
/// (<c>pwiz_tools/BiblioSpec/src/ProteinPilotReader.{h,cpp}</c>). The cpp reader is
/// a SAXHandler parsing with Expat; this port walks the document with
/// <see cref="XmlReader"/>, with the same element-state state machine
/// (<see cref="ParserState"/> mirroring cpp's <c>STATE</c> enum at
/// ProteinPilotReader.h:67).</para>
/// <para>The cpp reader also implements <c>SpecFileReader</c> directly (peaks live in
/// the same .group.xml file as the PSMs). The C# port mirrors that by installing an
/// inner <see cref="GroupSpecFileReader"/> on
/// <see cref="BuildParser.SpecReader"/> that hands back peaks from an in-memory map.</para>
/// </remarks>
public sealed class ProteinPilotReader : BuildParser
{
    /// <summary>
    /// cpp parity: ProteinPilotReader.h:67 — <c>enum STATE</c>. Drives the
    /// startElement/endElement state machine.
    /// </summary>
    private enum ParserState
    {
        RootState,
        SearchState,
        ElementState,
        ModState,
        SpectrumState,
        MatchState,
        PeaksState,
    }

    /// <summary>cpp parity: ProteinPilotReader.h:70 <c>struct MOD</c>.</summary>
    private sealed class ModEntry
    {
        public string Name = string.Empty;
        public double DeltaMass;

        public void Reset()
        {
            Name = string.Empty;
            DeltaMass = 0;
        }
    }

    // cpp parity: ProteinPilotReader.h:74-97 — per-parse state.
    private ParserState _state = ParserState.RootState;
    private PSM? _curPsm;
    private double _retentionTime;
    private readonly List<(double Mz, float Intensity)> _curPeaks = new();
    private string _peaksStr = string.Empty;
    private int _expectedNumPeaks;
    private double _curSpecMz;
    private readonly double _probCutOff;
    private bool _skipMods = true;
    private bool _skipNTermMods;
    private bool _skipCTermMods;
    private readonly Dictionary<string, SpecData> _spectrumMap = new(StringComparer.Ordinal);
    private string _nextWord = string.Empty;            // temp holder for element values
    private readonly Dictionary<string, double> _elementTable = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _modTable = new(StringComparer.Ordinal);
    private readonly ModEntry _curMod = new();
    private string _curElement = string.Empty;
    private string _curSearchId = string.Empty;
    private readonly Dictionary<string, string> _searchIdFileMap = new(StringComparer.Ordinal); // filename per search id
    private readonly Dictionary<string, List<PSM>> _searchIdPsmMap = new(StringComparer.Ordinal); // PSMs per search id
    private List<PSM>? _curSearchPsms; // pointer to map element for current match

    // Active XmlReader during parse, used by the GetAttrValue helpers below. cpp
    // SAXHandler hides this; we keep it in a field and expose the same helpers.
    private XmlReader? _reader;

    // cpp parity: ProteinPilotReader.cpp:395 — peak triples are whitespace-separated.
    // Cached separator array satisfies CA1861 ("prefer static readonly over constant arrays").
    private static readonly char[] _peakSeparators = { ' ', '\t', '\r', '\n' };

    /// <summary>
    /// True if <paramref name="filename"/> is a ProteinPilot group export
    /// (<c>.group.xml</c> or <c>.group</c>). Used by <see cref="BlibBuilder"/>'s
    /// reader-factory dispatch — matches <c>ClassifyInput</c> at BlibBuilder.cs:760.
    /// </summary>
    public static bool AcceptsExtension(string filename) =>
        BlibBuilder.HasExtensionCi(filename, ".group.xml") ||
        BlibBuilder.HasExtensionCi(filename, ".group");

    /// <summary>
    /// Construct a ProteinPilotReader bound to <paramref name="maker"/> and the file at
    /// <paramref name="xmlFileName"/>.
    /// </summary>
    /// <remarks>cpp parity: ProteinPilotReader.cpp:43.</remarks>
    public ProteinPilotReader(BlibBuilder maker, string xmlFileName, ProgressIndicator? parentProgress)
        : base(maker, xmlFileName, parentProgress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlFileName);

        _probCutOff = GetScoreThreshold(BuildInput.ProtPilot);
        // cpp parity: ProteinPilotReader.cpp:60 — default lookUpBy_ to NAME_ID.
        LookUpBy = SpecIdType.NameId;

        // cpp parity: ProteinPilotReader.cpp:63 — `delete specReader_; specReader_ = this;`.
        // We mirror that with an inner reader that pulls from _spectrumMap.
        SpecReader = new GroupSpecFileReader(this);

        InitReadAddProgress();
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: ProteinPilotReader.cpp:82.</remarks>
    public override bool ParseFile()
    {
        var filename = GetFileName();
        // cpp parity: ProteinPilotReader.cpp:85 — register the .group.xml as the spec file.
        // The reader-as-SpecFileReader returns peaks straight out of _spectrumMap.
        SetSpecFileName(filename);

        Verbosity.Debug($"ProteinPilotReader is parsing {filename}.");
        Parse();
        Verbosity.Debug($"ProteinPilotReader finished parsing {filename}.");

        // cpp parity: ProteinPilotReader.cpp:104 — add all the psms to the library, one
        // search at a time. Every call to buildTables clears the curSpecFileName_ parameter,
        // but for ProteinPilot all spectra are read from the same file, so we save the file
        // name and re-set it before each buildTables call.
        var specFileName = GetSpecFileName();
        foreach (var kvp in _searchIdPsmMap)
        {
            Psms.Clear();
            foreach (var psm in kvp.Value)
                Psms.Add(psm);

            // cpp parity: ProteinPilotReader.cpp:112 — searchIdFileMap_ maps the search id
            // to the original wiff (or other) filename pulled from <PEAKLIST>. That string
            // is what we record in SpectrumSourceFiles.
            var fileForSearch = _searchIdFileMap.TryGetValue(kvp.Key, out var f) ? f : string.Empty;
            SetSpecFileName(specFileName, checkFile: false);
            BuildTables(PsmScoreType.ProteinPilotConfidence, fileForSearch);
        }
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: ProteinPilotReader.cpp:119.</remarks>
    public override IList<PsmScoreType> GetScoreTypes() => GetScoreTypesHelper();

    /// <summary>
    /// Constructor-free score-types lookup. Mirrors cpp's static
    /// <c>ProteinPilotReader::getScoreTypesHelper()</c> (ProteinPilotReader.h),
    /// called from <see cref="BlibBuilder.DispatchReader"/>'s bare-.group short-circuit
    /// where we can't instantiate a reader because the binary .group file isn't parseable.
    /// </summary>
    public static IList<PsmScoreType> GetScoreTypesHelper() =>
        new[] { PsmScoreType.ProteinPilotConfidence };

    // ----------------------------------------------------------------------------------
    // XmlReader driver. cpp's SAXHandler::parse() walks with Expat; we mirror it with
    // XmlReader, dispatching to StartElement/EndElement and feeding text into
    // Characters for the inline data we care about.
    // ----------------------------------------------------------------------------------

    private void Parse()
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = false, // we need CDATA / text for peaks + element symbols
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
    // Element handlers — direct port of ProteinPilotReader.cpp:127 startElement /
    // ProteinPilotReader.cpp:175 endElement.
    // ----------------------------------------------------------------------------------

    // cpp parity: ProteinPilotReader.cpp:127.
    private void StartElement(string name)
    {
        // clear buffer for characters()
        _nextWord = string.Empty;

        if (IsElement("SEARCH", name))
        {
            _state = ParserState.SearchState;
            ParseSearchId();
        }
        else if (IsElement("PEAKLIST", name))
        {
            ParseSpectrumFilename();
        }
        else if (IsElement("El", name))
        {
            _state = ParserState.ElementState; // as in chemical element, not xml element
        }
        else if (IsElement("Mod", name))
        {
            _state = ParserState.ModState;
        }
        else if (IsElement("SPECTRUM", name))
        {
            _state = ParserState.SpectrumState;
            ParseSpectrumElement();
        }
        else if (IsElement("MATCH", name))
        {
            ParseMatchElement();
        }
        else if (IsElement("MOD_FEATURE", name))
        {
            // cpp parity: ProteinPilotReader.cpp:149 — two kinds of mod-feature elements,
            // in params and out. For now only do the ones inside <SPECTRUM>.
            if (_state == ParserState.SpectrumState)
            {
                ParseMatchModElement(termMod: false);
            }
        }
        else if (IsElement("TERM_MOD_FEATURE", name))
        {
            // cpp parity: ProteinPilotReader.cpp:155 — same gate as MOD_FEATURE.
            if (_state == ParserState.SpectrumState)
            {
                ParseMatchModElement(termMod: true);
            }
        }
        else if (IsElement("MSMSPEAKS", name))
        {
            _state = ParserState.PeaksState;
            // cpp parity: ProteinPilotReader.cpp:164 — HACK workaround for variable casing
            // produced by Protein Pilot: try "Size" then fall back to "size".
            var sizeStr = GetAttrValue("Size");
            if (string.IsNullOrEmpty(sizeStr))
                sizeStr = GetRequiredAttrValue("size");
            if (!int.TryParse(sizeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out _expectedNumPeaks))
            {
                throw new BlibException(true,
                    $"The value '{sizeStr}' on MSMSPEAKS Size attribute is not a valid integer (file {GetFileName()}).");
            }
            _curPeaks.Clear();
            _peaksStr = string.Empty;
        }
    }

    // cpp parity: ProteinPilotReader.cpp:175.
    private void EndElement(string name)
    {
        // chemical element and modification info stored as element values not attributes,
        // so save the info after the value(s) have been read.
        if (_state == ParserState.SearchState)
        {
            _state = ParserState.RootState;
        }
        else if (_state == ParserState.ElementState)
        {
            if (IsElement("Sym", name))
            {
                GetElementName();
            }
            else if (IsElement("Mss", name))
            {
                GetElementMass();
            }
            else if (IsElement("El", name))
            {
                _state = ParserState.RootState;
            }
        }
        else if (_state == ParserState.ModState)
        {
            // cpp parity: ProteinPilotReader.cpp:191 — Use Nme or DisplayName, whichever shows up last.
            if (IsElement("Nme", name) || IsElement("DisplayName", name))
            {
                GetModName();
            }
            else if (IsElement("Fma", name))
            {
                GetModFormula(add: true);
            }
            else if (IsElement("RpF", name))
            {
                GetModFormula(add: false);
            }
            else if (IsElement("Mod", name))
            {
                AddMod();
                _state = ParserState.RootState;
            }
        }

        // go back to spectrum state at the end of matches and peaks; go back to root state at end of spectrum.
        if (IsElement("SPECTRUM", name))
        {
            SaveMatch();
            _state = ParserState.RootState;
        }
        else if (IsElement("MATCH", name))
        {
            _state = ParserState.SpectrumState;
        }
        else if (IsElement("MSMSPEAKS", name))
        {
            SaveSpectrum();
            _state = ParserState.SpectrumState;
        }
    }

    // cpp parity: ProteinPilotReader.cpp:225.
    private void ParseSearchId()
    {
        _curSearchId = GetRequiredAttrValue("xml:id");
    }

    // cpp parity: ProteinPilotReader.cpp:229.
    private void ParseSpectrumFilename()
    {
        var filename = GetRequiredAttrValue("originalfilename");
        _searchIdFileMap[_curSearchId] = filename;
    }

    // cpp parity: ProteinPilotReader.cpp:235.
    private void ParseSpectrumElement()
    {
        _curPsm = new PSM
        {
            SpecName = GetRequiredAttrValue("xml:id"),
        };
        _retentionTime = GetDoubleRequiredAttrValue("elution");
    }

    // cpp parity: ProteinPilotReader.cpp:243.
    private void ParseMatchElement()
    {
        if (_curPsm is null || string.IsNullOrEmpty(_curPsm.SpecName))
        {
            throw new BlibException(false,
                $"Cannot find spectrum associated with match {GetAttrValue("xml:id")}, sequence {GetAttrValue("seq")}.");
        }

        // find out what spectrum file this came from
        var searchID = GetRequiredAttrValue("searches");

        // get confidence and skip if doesn't pass cutoff or if ranked lower than first
        var score = GetDoubleRequiredAttrValue("confidence");
        if (score < _probCutOff || score < _curPsm.Score)
        {
            _skipMods = true;
            FilteredOutPsmCount++;
            // cpp parity: ProteinPilotReader.cpp:262 — register an empty vector for this search.
            if (!_searchIdPsmMap.ContainsKey(searchID))
                _searchIdPsmMap[searchID] = new List<PSM>();
            return;
        }

        // create a vector of PSMs for this search/file if not present
        bool isNewSearch = !_searchIdPsmMap.TryGetValue(searchID, out var bucket);
        if (isNewSearch)
        {
            bucket = new List<PSM>();
            _searchIdPsmMap[searchID] = bucket;
        }
        _curSearchPsms = bucket;

        // cpp parity: ProteinPilotReader.cpp:277 — if this is not the first match for this
        // spectrum, push the previous match and start a new PSM. The replacement PSM's
        // specName comes from either the spectrum (when this is a new search bucket) or
        // from the last PSM pushed onto the existing bucket.
        if (_curPsm.Score != 0)
        {
            var specName = _curPsm.SpecName;
            _curSearchPsms!.Add(_curPsm);
            _curPsm = new PSM
            {
                SpecName = isNewSearch
                    ? specName
                    : _curSearchPsms[_curSearchPsms.Count - 1].SpecName,
            };
        }
        _curPsm.Score = score;

        // get charge, m/z, seq
        _curPsm.Charge = GetIntRequiredAttrValue("charge");
        _curPsm.UnmodSeq = GetRequiredAttrValue("seq");
        _curSpecMz = GetDoubleRequiredAttrValue("mz");

        _skipMods = false;
        _skipNTermMods = string.IsNullOrEmpty(GetAttrValue("nt"));
        _skipCTermMods = string.IsNullOrEmpty(GetAttrValue("ct"));
    }

    // cpp parity: ProteinPilotReader.cpp:299.
    private void SaveMatch()
    {
        if (_curPsm is null)
            return;

        if (string.IsNullOrEmpty(_curPsm.UnmodSeq))
        {
            _curPsm = null;
        }
        else
        {
            _curSearchPsms!.Add(_curPsm);
            _curPsm = null;
        }
    }

    // cpp parity: ProteinPilotReader.cpp:314.
    private void ParseMatchModElement(bool termMod)
    {
        if (_skipMods)
            return;

        var position = GetIntRequiredAttrValue("pos");

        if (termMod)
        {
            // cpp parity: ProteinPilotReader.cpp:323 — check for internal consistency,
            // since group2xml can sometimes write bogus TERM_MOD_FEATURE tags.
            if (_skipNTermMods && position == 1)
                return;
            if (_skipCTermMods && _curPsm != null && position == _curPsm.UnmodSeq.Length)
                return;
        }

        var name = GetRequiredAttrValue("mod");

        // cpp parity: ProteinPilotReader.cpp:333 — skip the absence-of-modification annotations.
        if (name.StartsWith("No ", StringComparison.Ordinal) ||
            name.StartsWith("no ", StringComparison.Ordinal))
        {
            return;
        }

        var deltaMass = GetModMass(name);
        _curPsm!.Mods.Add(new SeqMod(position, deltaMass));
    }

    // cpp parity: ProteinPilotReader.cpp:346.
    private double GetModMass(string name)
    {
        if (!_modTable.TryGetValue(name, out var mass))
        {
            throw new BlibException(false, $"PSM has an unrecognized mod, {name}.");
        }
        return mass;
    }

    /// <summary>
    /// cpp parity: ProteinPilotReader.cpp:361 — Handler for all characters between tags.
    /// We are only interested in the peaks data in <c>MSMSPEAKS</c> elements and the values
    /// for chemical elements and modifications. Use the state to determine if we are there.
    /// </summary>
    private void Characters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (_state == ParserState.PeaksState
            && _curPsm != null
            && !string.IsNullOrEmpty(_curPsm.UnmodSeq))
        {
            _peaksStr += text;
        }
        else if (_state == ParserState.ModState || _state == ParserState.ElementState)
        {
            _nextWord += text;
        }
    }

    // cpp parity: ProteinPilotReader.cpp:384.
    private void SaveSpectrum()
    {
        if (string.IsNullOrEmpty(_peaksStr))
            return;

        if (_curPsm is null)
        {
            throw new BlibException(false, "Found MS/MS peaks but no spectrum information.");
        }

        // cpp parity: ProteinPilotReader.cpp:395 — translate peaksStr into a vector of peaks.
        // Format is whitespace-separated triples: mz charge intensity.
        var tokens = _peaksStr.Split(_peakSeparators, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 2 < tokens.Length; i += 3)
        {
            if (!double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var mz))
                break;
            if (!double.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var charge))
                break;
            if (!float.TryParse(tokens[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out var intensity))
                break;

            if (mz == 0 && intensity == 0)
                break;

            // cpp parity: ProteinPilotReader.cpp:403 — peak location is actually M+H if charge > 0; adjust.
            if (charge > 0)
            {
                mz = (mz + (charge - 1) * AminoAcidMasses.ProtonMass) / charge;
            }
            _curPeaks.Add((mz, intensity));
        }

        // cpp parity: ProteinPilotReader.cpp:410 — verify peak count.
        if (_expectedNumPeaks != _curPeaks.Count)
        {
            throw new BlibException(false,
                $"Spectrum {_curPsm.SpecName} should have {_expectedNumPeaks} peaks but {_curPeaks.Count} were read.");
        }

        // cpp parity: ProteinPilotReader.cpp:418 — sort by m/z ascending.
        _curPeaks.Sort((a, b) => a.Mz.CompareTo(b.Mz));

        // cpp parity: ProteinPilotReader.cpp:421 — create a SpecData and fill it.
        var specD = new SpecData
        {
            RetentionTime = _retentionTime,
            Mz = _curSpecMz,
            NumPeaks = _curPeaks.Count,
        };
        specD.Mzs = new double[specD.NumPeaks];
        specD.Intensities = new float[specD.NumPeaks];
        for (int i = 0; i < specD.NumPeaks; i++)
        {
            specD.Mzs[i] = _curPeaks[i].Mz;
            specD.Intensities[i] = _curPeaks[i].Intensity;
        }

        // cpp parity: ProteinPilotReader.cpp:434 — save it keyed by spec name.
        _spectrumMap[_curPsm.SpecName] = specD;
    }

    // cpp parity: ProteinPilotReader.cpp:437.
    private void GetElementName()
    {
        // only fill the element table once, even though there may be multiple copies in the file
        if (!_elementTable.ContainsKey(_nextWord))
        {
            _elementTable[_nextWord] = -1; // init entry
            _curElement = _nextWord;
        }
        else
        {
            _curElement = string.Empty;
        }
    }

    // cpp parity: ProteinPilotReader.cpp:453.
    // ASSUMPTIONS: Always uses the first mass listed for the element. The example file we
    // have only contains monoisotopic masses and always lists the most common one first.
    private void GetElementMass()
    {
        if (!string.IsNullOrEmpty(_curElement)
            && _elementTable.TryGetValue(_curElement, out var existing)
            && existing == -1)
        {
            if (double.TryParse(_nextWord, NumberStyles.Float, CultureInfo.InvariantCulture, out var mass))
                _elementTable[_curElement] = mass;
            else
                _elementTable[_curElement] = 0; // cpp atof returns 0 on bad input
        }
    }

    // cpp parity: ProteinPilotReader.cpp:460.
    private void GetModName()
    {
        _curMod.Name = _nextWord;
    }

    // cpp parity: ProteinPilotReader.cpp:467 — TODO: some elements have multi letter codes (duh)
    // don't store the list of elements, just add up the mass.
    private void GetModFormula(bool add)
    {
        var formula = _nextWord;
        var element = string.Empty;
        int sign = add ? 1 : -1;

        for (int i = 0; i < formula.Length; i++)
        {
            var c = formula[i];
            if (c >= 'A' && c <= 'Z')
            {
                // add the last element we found
                AddElement(_curMod, element, sign);
                element = c.ToString();
            }
            else if (c >= 'a' && c <= 'z')
            {
                element += c;
            }
            else if (c > '0' && c <= '9')
            {
                // cpp parity: ProteinPilotReader.cpp:481 — atoi from this position onward.
                int count = 0;
                int j = i;
                while (j < formula.Length && formula[j] >= '0' && formula[j] <= '9')
                {
                    count = count * 10 + (formula[j] - '0');
                    j++;
                }
                AddElement(_curMod, element, sign * count);
                element = string.Empty;
            }
        }
        // now add the last element (if the formula didn't end with a number)
        AddElement(_curMod, element, sign);
    }

    // cpp parity: ProteinPilotReader.cpp:490.
    private void AddElement(ModEntry mod, string element, int count)
    {
        if (string.IsNullOrEmpty(element))
            return;
        if (!_elementTable.TryGetValue(element, out var elementMass))
        {
            throw new BlibException(false,
                $"The formula for modification '{mod.Name}' has an unrecognzied element, {element}.");
        }
        mod.DeltaMass += count * elementMass;
    }

    // cpp parity: ProteinPilotReader.cpp:512.
    private void AddMod()
    {
        // first check to see if we already have one for this mod
        if (_modTable.TryGetValue(_curMod.Name, out var existing) && existing != _curMod.DeltaMass)
        {
            throw new BlibException(false,
                $"Two entries for a modification named {_curMod.Name}, one with delta mass {existing.ToString(CultureInfo.InvariantCulture)} "
                + $"and one with {_curMod.DeltaMass.ToString(CultureInfo.InvariantCulture)}.");
        }
        _modTable[_curMod.Name] = _curMod.DeltaMass;
        _curMod.Reset();
    }

    // ----------------------------------------------------------------------------------
    // Inner spec reader. cpp's ProteinPilotReader implements SpecFileReader directly
    // (ProteinPilotReader.cpp:529); we mirror that with an inner reader bound to the
    // parent's _spectrumMap.
    // ----------------------------------------------------------------------------------

    /// <summary>
    /// Spec-file reader that returns peaks straight off the parent reader's
    /// <see cref="ProteinPilotReader._spectrumMap"/>. cpp parity:
    /// ProteinPilotReader.cpp:529 (the cpp class is its own SpecFileReader).
    /// </summary>
    private sealed class GroupSpecFileReader : SpecFileReaderBase
    {
        private readonly ProteinPilotReader _parent;

        public GroupSpecFileReader(ProteinPilotReader parent)
        {
            _parent = parent;
        }

        // cpp parity: ProteinPilotReader.cpp:548 — debug log, no-op otherwise.
        public override void OpenFile(string path, bool mzSort = false)
        {
            Verbosity.Debug($"ProteinPilotReader is reading spectra from {_parent.GetFileName()}");
        }

        public override SpecIdType IdType
        {
            set { /* no-op, cpp parity: ProteinPilotReader.cpp:553 */ }
        }

        // cpp parity: ProteinPilotReader.cpp:529 — string-name lookup is the only working overload.
        public override bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true)
        {
            ArgumentNullException.ThrowIfNull(identifier);
            ArgumentNullException.ThrowIfNull(returnData);

            Verbosity.Comment(VerbosityLevel.Detail, $"Looking for spectrum {identifier}");
            if (!_parent._spectrumMap.TryGetValue(identifier, out var found))
                return false;

            if (!getPeaks)
            {
                // cpp parity: ProteinPilotReader.cpp:540 — null the count so peaks don't get copied.
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

        // cpp parity: ProteinPilotReader.cpp:555 — warn and return false; ProteinPilot only
        // supports string-identifier lookup.
        public override bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true)
        {
            Verbosity.Warn("ProteinPilotReader cannot fetch spectra by scan number, only by string identifier.");
            return false;
        }

        // cpp parity: ProteinPilotReader.cpp:563.
        public override bool GetNextSpectrum(SpecData returnData, bool getPeaks = true)
        {
            Verbosity.Warn("Sequential retrivial of spectra not implemented for ProteinPilotReader.");
            return false;
        }
    }
}
