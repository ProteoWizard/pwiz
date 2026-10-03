// Port of pwiz_tools/BiblioSpec/src/IdpXMLreader.{h,cpp}
//
// Reads IDPicker .idpXML files — a post-search-processor's curated PSM list. Because
// IDPicker keeps only the PSMs it deemed correct, the reader admits every result that
// maps to a non-decoy peptide (probability threshold = 0; see BlibBuilder.cs:263).
//
// The cpp reader is a SAXHandler/Expat driver; the C# port walks the document with
// System.Xml.XmlReader and dispatches start/end-element events into the same six-state
// machine cpp uses (IdpXMLreader.h:152 — START → ROOT → {PROT|PEP|SPEC → SPEC_RESULT}).

using System.Globalization;
using System.Xml;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Reads IDPicker <c>.idpXML</c> result files into <see cref="PSM"/>s for one or more
/// spectrum sources listed inside the file.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::IdpXMLreader</c>
/// (<c>pwiz_tools/BiblioSpec/src/IdpXMLreader.{h,cpp}</c>). The cpp class is a SAXHandler
/// subclass; this port walks the file with <see cref="XmlReader"/> and dispatches the same
/// element callbacks. The state machine (<see cref="ParserState"/>) mirrors cpp's
/// <c>STATE</c> enum at IdpXMLreader.h:152.</para>
/// <para>Schema (cpp comment block at IdpXMLreader.h:39):
/// <code>
/// idPickerPeptides
///   proteinIndex / protein
///   peptideIndex / peptide / locus
///   spectraSources / spectraSource / spectrum / result / id
/// </code>
/// As each <c>spectraSource</c> closes, the accumulated PSMs are flushed via
/// <see cref="BuildParser.BuildTables"/>; the spectrum file (mzML/mzXML) is looked up next to
/// the .idpXML.</para>
/// </remarks>
public sealed class IdpXMLreader : BuildParser
{
    // cpp parity: IdpXMLreader.h:152 — STATE enum.
    private enum ParserState
    {
        Start,
        Root,
        Prot,
        Pep,
        Spec,
        SpecResult,
    }

    /// <summary>
    /// cpp parity: IdpXMLreader.h:61 — <c>PeptideEntry</c>. One row of the
    /// <c>&lt;peptideIndex&gt;</c> table; the id maps to a 1-based peptide number.
    /// </summary>
    private sealed class PeptideEntry
    {
        public int Id = -1;
        public string Seq = string.Empty;
        public double Mass = -1;
    }

    /// <summary>
    /// cpp parity: IdpXMLreader.h:86 — <c>SpectrumEntry</c>. Carries everything we need to
    /// emit a <see cref="PSM"/> once the closing <c>&lt;spectrum&gt;</c> tag arrives.
    /// </summary>
    private sealed class SpectrumEntry
    {
        public string IdStr = string.Empty;
        public int Key = -1;
        public int Charge = -1;
        public double Score = -1;
        public int PepId = -1;
        public PeptideEntry? Peptide;
        public List<SeqMod> Mods = new();

        public SpectrumEntry() { }

        public SpectrumEntry(SpectrumEntry other)
        {
            // cpp parity: IdpXMLreader.h:106 — copy ctor used when a spectrum has multiple
            // peptide ids and we need to clone before pushing each.
            IdStr = other.IdStr;
            Key = other.Key;
            Charge = other.Charge;
            Score = other.Score;
            PepId = other.PepId;
            Peptide = other.Peptide;
            Mods = new List<SeqMod>(other.Mods);
        }
    }

    // cpp parity: IdpXMLreader.h:155-164 — per-parse state.
    private readonly Dictionary<int, PeptideEntry> _peptides = new();
    private readonly List<int> _proteins = new(); // non-decoy protein ids; kept sorted to allow binary search
    private ParserState _state = ParserState.Start;
    private PeptideEntry? _curPeptide;
    private bool _curPeptideIsDecoy = true; // cpp parity: IdpXMLreader.cpp:268 — reset to true after each addPeptide
    private SpectrumEntry? _curSpectrumEntry;
    private int _curPepIdCount;

    // The active XmlReader during parsing — used by the GetAttrValue helpers below.
    // cpp's SAXHandler hides this behind getAttrValue; we keep it in a field.
    private XmlReader? _reader;

    /// <summary>
    /// True if <paramref name="path"/> is an IDPicker file (<c>.idpXML</c>).
    /// Used by <see cref="BlibBuilder"/>'s reader-factory dispatch.
    /// </summary>
    public static bool AcceptsExtension(string path) =>
        BlibBuilder.HasExtensionCi(path, ".idpXML");

    /// <summary>
    /// Construct an IdpXMLreader bound to a BlibBuilder.
    /// </summary>
    /// <remarks>cpp parity: IdpXMLreader.cpp:34.</remarks>
    public IdpXMLreader(BlibBuilder maker, string idpFileName, ProgressIndicator? parentProgress)
        : base(maker, idpFileName, parentProgress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idpFileName);
        // cpp parity: IdpXMLreader.cpp:44 — cpp calls setFileName again here for the SAX handler;
        // BuildParser already records the filename so nothing extra is needed in C#.
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: IdpXMLreader.cpp:72.</remarks>
    public override bool ParseFile()
    {
        Parse();
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: IdpXMLreader.cpp:76.</remarks>
    public override IList<PsmScoreType> GetScoreTypes() =>
        new[] { PsmScoreType.IdPickerFdr };

    // ----------------------------------------------------------------------------------
    // XmlReader driver. cpp's SAXHandler::parse() walks with Expat; we mirror it with
    // XmlReader, dispatching to StartElement / EndElement.
    // ----------------------------------------------------------------------------------

    private void Parse()
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = true,
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
                if (reader.NodeType == XmlNodeType.Element)
                {
                    var isEmpty = reader.IsEmptyElement;
                    var name = reader.LocalName;
                    StartElement(name);
                    if (isEmpty)
                    {
                        // SAXHandler emits matching endElement for self-closing elements too.
                        EndElement(name);
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    EndElement(reader.LocalName);
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

    // cpp parity: SAXHandler.h:96 — isElement (case-sensitive).
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
    // Element handlers — direct port of IdpXMLreader.cpp:80 startElement /
    // IdpXMLreader.cpp:154 endElement.
    // ----------------------------------------------------------------------------------

    // cpp parity: IdpXMLreader.cpp:80.
    private void StartElement(string name)
    {
        switch (_state)
        {
            case ParserState.Start:
                if (IsElement("idPickerPeptides", name))
                    _state = ParserState.Root;
                // else error / missing idpicker element (cpp silently ignores)
                break;

            case ParserState.Root:
                if (IsElement("proteinIndex", name))
                {
                    _state = ParserState.Prot;
                }
                else if (IsElement("peptideIndex", name))
                {
                    _state = ParserState.Pep;
                }
                else if (IsElement("spectraSources", name))
                {
                    _state = ParserState.Spec;
                    var numSpecFiles = GetIntRequiredAttrValue("count");
                    if (numSpecFiles > 1)
                        InitSpecFileProgress(numSpecFiles);
                }
                break;

            case ParserState.Prot:
                if (IsElement("protein", name))
                    ParseProtein();
                break;

            case ParserState.Pep:
                if (IsElement("peptide", name))
                {
                    ParsePeptide();
                }
                else if (IsElement("locus", name))
                {
                    var locusId = GetIntRequiredAttrValue("id");
                    // cpp parity: IdpXMLreader.cpp:122 — if any locus is a non-decoy protein,
                    // mark the current peptide as non-decoy. Don't reset back to true once
                    // we've seen a real protein.
                    if (_proteins.BinarySearch(locusId) >= 0)
                        _curPeptideIsDecoy = false;
                }
                break;

            case ParserState.Spec:
                if (IsElement("spectraSource", name))
                {
                    SetSpecFilename();
                }
                else if (IsElement("spectrum", name))
                {
                    ParseSpectrum();
                }
                else if (IsElement("result", name))
                {
                    ParseResult();
                    _state = ParserState.SpecResult;
                }
                break;

            case ParserState.SpecResult:
                if (IsElement("result", name))
                {
                    _state = ParserState.SpecResult;
                }
                else if (IsElement("id", name))
                {
                    ParseId();
                }
                break;
        }
    }

    // cpp parity: IdpXMLreader.cpp:154.
    private void EndElement(string name)
    {
        switch (_state)
        {
            case ParserState.Start:
                break;

            case ParserState.Root:
                // cpp emits a (commented-out) warning if the closing tag isn't idPickerPeptides.
                break;

            case ParserState.Prot:
                if (IsElement("proteinIndex", name))
                    _state = ParserState.Root;
                break;

            case ParserState.Pep:
                if (IsElement("peptideIndex", name))
                {
                    _state = ParserState.Root;
                }
                else if (IsElement("peptide", name))
                {
                    AddPeptide();
                }
                break;

            case ParserState.Spec:
                if (IsElement("spectraSources", name))
                {
                    _state = ParserState.Root;
                }
                else if (IsElement("spectraSource", name))
                {
                    // cpp parity: IdpXMLreader.cpp:189 — flush PSMs for this spectrum source.
                    BuildTables(PsmScoreType.IdPickerFdr);
                }
                else if (IsElement("spectrum", name))
                {
                    AddSpectrum();
                }
                break;

            case ParserState.SpecResult:
                if (IsElement("result", name))
                    _state = ParserState.Spec;
                break;
        }
    }

    // cpp parity: IdpXMLreader.cpp:211 — add the protein id to the collection if it's not a decoy.
    private void ParseProtein()
    {
        var isDecoy = GetIntRequiredAttrValue("decoy");
        if (isDecoy == 0)
        {
            var curProtId = GetIntRequiredAttrValue("id");
            _proteins.Add(curProtId);
            // cpp comment (IdpXMLreader.cpp:225): "TODO: are proteins always in sorted order by id???"
            // BinarySearch on _proteins downstream assumes sorted. The IDPicker outputs we've seen
            // emit proteins in id-ascending order, but defend against an out-of-order id so the
            // search at locus-time stays correct.
            if (_proteins.Count >= 2 && _proteins[_proteins.Count - 1] < _proteins[_proteins.Count - 2])
                _proteins.Sort();
            Verbosity.Comment(VerbosityLevel.Detail, $"Parsing protein id {curProtId}");
        }
        else
        {
            Verbosity.Comment(VerbosityLevel.Detail, "Parsing decoy protein");
        }
    }

    // cpp parity: IdpXMLreader.cpp:234.
    private void ParsePeptide()
    {
        _curPeptide = new PeptideEntry
        {
            Id = GetIntRequiredAttrValue("id"),
            Seq = GetRequiredAttrValue("sequence"),
            Mass = GetDoubleRequiredAttrValue("mass"),
        };
        Verbosity.Comment(VerbosityLevel.Detail, $"Parsing peptide {_curPeptide.Seq}.");
    }

    // cpp parity: IdpXMLreader.cpp:255 — at </peptide>, add if any locus was non-decoy.
    private void AddPeptide()
    {
        if (_curPeptide == null) return;
        if (!_curPeptideIsDecoy)
        {
            _peptides[_curPeptide.Id] = _curPeptide;
        }
        // cpp parity: IdpXMLreader.cpp:268 — reset the decoy flag (so it's only false again
        // after the next peptide explicitly sees a non-decoy locus).
        _curPeptideIsDecoy = true;
        _curPeptide = null;
    }

    // cpp parity: IdpXMLreader.cpp:277 — set spectrum filename from the spectraSource@name.
    private void SetSpecFilename()
    {
        var name = GetAttrValue("name");
        var extensions = new List<string> { ".mzML", ".mzXML" };
        SetSpecFileName(name, extensions);
    }

    // cpp parity: IdpXMLreader.cpp:294 — start a SpectrumEntry from the spectrum element.
    private void ParseSpectrum()
    {
        if (_curSpectrumEntry != null)
        {
            throw new BlibException(true,
                $"Cannot parse spectrum {_curSpectrumEntry.Key} in {GetFileName()}.");
        }
        Verbosity.Comment(VerbosityLevel.Detail, "Parsing spectrum");

        _curSpectrumEntry = new SpectrumEntry
        {
            Charge = GetIntRequiredAttrValue("z"),
        };

        var tmpId = GetAttrValue("id");
        // cpp parity: IdpXMLreader.cpp:307 — fall back to 'scan' if 'id' is missing.
        if (string.IsNullOrEmpty(tmpId))
        {
            tmpId = GetAttrValue("scan");
            if (string.IsNullOrEmpty(tmpId))
            {
                throw new BlibException(true,
                    $"Spectrum tag contains neither id nor scan attribute (file {GetFileName()}).");
            }
        }
        _curSpectrumEntry.IdStr = tmpId;

        // cpp parity: IdpXMLreader.cpp:318 — find the last '=' and verify "scan=" precedes it.
        // If there's no '=' at all, try interpreting tmpId itself as an int.
        var lastEq = tmpId.LastIndexOf('=');
        string scanText;
        if (lastEq >= 0)
        {
            // confirm that "scan=" precedes the int after the last '='.
            const string scanTag = "scan=";
            if (lastEq < scanTag.Length - 1 ||
                string.Compare(tmpId, lastEq - (scanTag.Length - 1), scanTag, 0, scanTag.Length, StringComparison.Ordinal) != 0)
            {
                throw new BlibException(true,
                    $"Cannot find scan in spectrum id '{tmpId}' (file {GetFileName()}).");
            }
            scanText = tmpId.Substring(lastEq + 1);
        }
        else
        {
            // cpp parity: IdpXMLreader.cpp:326 — try parsing tmpId itself as an int.
            if (!int.TryParse(tmpId, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                throw new BlibException(true,
                    $"The spectrum id '{tmpId}' cannot be parsed (file {GetFileName()}).");
            }
            scanText = tmpId;
        }

        if (!int.TryParse(scanText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scan))
        {
            throw new BlibException(true,
                $"The spectrum id '{tmpId}' does not end with a valid scan number (file {GetFileName()}).");
        }
        _curSpectrumEntry.Key = scan;

        Verbosity.Comment(VerbosityLevel.Detail,
            $"Parsing spectrum id {_curSpectrumEntry.Key} z {_curSpectrumEntry.Charge}");
    }

    // cpp parity: IdpXMLreader.cpp:347 — capture FDR for the rank-1 result.
    private void ParseResult()
    {
        var rank = GetIntRequiredAttrValue("rank");
        var fdr = GetDoubleRequiredAttrValue("FDR");

        if (_curSpectrumEntry == null)
        {
            throw new BlibException(true,
                $"Result (rank {rank} FDR {fdr.ToString(CultureInfo.InvariantCulture)}) could not be parsed. "
                + $"No associated spectrum (file {GetFileName()}).");
        }

        if (rank == 1)
            _curSpectrumEntry.Score = fdr;
    }

    // cpp parity: IdpXMLreader.cpp:372 — capture peptide id + mods for the current spectrum.
    private void ParseId()
    {
        var pepId = GetIntRequiredAttrValue("peptide");

        if (_curSpectrumEntry == null)
        {
            throw new BlibException(true,
                $"No spectrum associated with peptide id {pepId} (file {GetFileName()}).");
        }

        // cpp parity: IdpXMLreader.cpp:382 — if pep not in the map, assume it was a decoy and skip.
        if (!_peptides.ContainsKey(pepId))
            return;

        _curPepIdCount++;

        if (_curPepIdCount > 1)
        {
            // cpp parity: IdpXMLreader.cpp:387 — multiple ids for one spectrum: clone the SE,
            // push the current one (with the previous pep_id), then keep parsing into the clone.
            var copySE = new SpectrumEntry(_curSpectrumEntry);
            AddSpectrum();
            _curSpectrumEntry = copySE;
        }

        _curSpectrumEntry.PepId = pepId;

        // cpp parity: IdpXMLreader.cpp:397 — get any mods.
        ParseModifications();
    }

    // cpp parity: IdpXMLreader.cpp:408 — translate the mods attribute into SeqMod entries.
    // Format: "int|c|n:float", space-separated.
    private void ParseModifications()
    {
        var mods = GetAttrValue("mods");
        if (string.IsNullOrEmpty(mods))
            return;

        if (_curSpectrumEntry == null) return; // defensive; only called after a spectrum is open

        // cpp parses each space-separated token in order.
        var tokens = mods.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            // Each token is of the form "<pos>:<delta>" where <pos> is int, 'n', or 'c'.
            var colon = token.IndexOf(':');
            if (colon <= 0 || colon == token.Length - 1)
            {
                throw new BlibException(true,
                    $"Malformed mods token '{token}' in spectrum mods '{mods}' (file {GetFileName()}).");
            }

            var posTok = token.Substring(0, colon);
            var deltaTok = token.Substring(colon + 1);

            if (!double.TryParse(deltaTok, NumberStyles.Float, CultureInfo.InvariantCulture, out var deltaMass))
            {
                throw new BlibException(true,
                    $"Bad delta mass in mods token '{token}' (file {GetFileName()}).");
            }

            int position;
            if (posTok == "n")
            {
                position = 1;
            }
            else if (posTok == "c")
            {
                // cpp parity: IdpXMLreader.cpp:430 — c-term = peptide length.
                if (!_peptides.TryGetValue(_curSpectrumEntry.PepId, out var pep))
                {
                    throw new BlibException(true,
                        $"Cannot resolve peptide id {_curSpectrumEntry.PepId} for c-term mod (file {GetFileName()}).");
                }
                position = pep.Seq.Length;
            }
            else if (!int.TryParse(posTok, NumberStyles.Integer, CultureInfo.InvariantCulture, out position))
            {
                throw new BlibException(true,
                    $"Bad position in mods token '{token}' (file {GetFileName()}).");
            }

            _curSpectrumEntry.Mods.Add(new SeqMod(position, deltaMass));
        }
    }

    // cpp parity: IdpXMLreader.cpp:455 — at </spectrum>, emit a PSM for the spectrum.
    private void AddSpectrum()
    {
        if (_curSpectrumEntry == null) return;

        if (_peptides.TryGetValue(_curSpectrumEntry.PepId, out var peptide))
        {
            _curSpectrumEntry.Peptide = peptide;

            CurPsm = new PSM
            {
                Charge = _curSpectrumEntry.Charge,
                Score = _curSpectrumEntry.Score,
                SpecKey = _curSpectrumEntry.Key,
                UnmodSeq = peptide.Seq,
            };
            foreach (var mod in _curSpectrumEntry.Mods)
                CurPsm.Mods.Add(mod);

            Psms.Add(CurPsm);
            CurPsm = null;
        }
        // cpp parity: IdpXMLreader.cpp:463 — don't emit a PSM if the peptide was a decoy
        // (i.e. wasn't added to the peptides map).

        _curSpectrumEntry = null;
        _curPepIdCount = 0;
    }
}
