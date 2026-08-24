// Port of pwiz_tools/BiblioSpec/src/PercolatorXmlReader.{h,cpp}
//
// Reader for Percolator v1.15+ XML output (percolator_output / psms / psm). Each PSM carries
// a q-value (the score we keep), a peptide_seq with sequence + optional inline modifications
// (either as marker characters like '*' or as bracketed deltas like "[15.9949]"), and a
// per-file psm_id whose underscore-delimited tokens encode "<filename>_<scan>_<charge>_<idx>".
// Peaks come from the matching .ms2 / .cms2 / .bms2 / .pms2 spectrum file and the per-file
// .sqt is also consulted to translate marker characters into mod masses.

using System.Globalization;
using System.Text;
using System.Xml;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Reader for Percolator XML output (<c>.perc.xml</c>) into <see cref="PSM"/>s.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::PercolatorXmlReader</c>
/// (<c>pwiz_tools/BiblioSpec/src/PercolatorXmlReader.{h,cpp}</c>). The cpp reader is a
/// SAXHandler driven by Expat; this port walks the document with
/// <see cref="XmlReader"/>, with the same element-state state machine
/// (<see cref="ParserState"/> mirroring cpp's <c>STATE</c> enum at
/// PercolatorXmlReader.h:68).</para>
/// <para>PSMs are bucketed by source filename (parsed from <c>p:psm_id</c>) and after the
/// parse each bucket is flushed via the matching <c>.sqt</c> for mod-marker translation,
/// then handed to <see cref="BuildParser.BuildTables(PsmScoreType, string, bool, WorkflowType)"/>
/// against the bucket's spectrum file.</para>
/// </remarks>
public sealed class PercolatorXmlReader : BuildParser
{
    /// <summary>
    /// cpp parity: PercolatorXmlReader.h:68 — <c>enum STATE</c>. Drives the
    /// startElement/endElement state machine.
    /// </summary>
    private enum ParserState
    {
        Start,
        Root,            // inside <percolator_output>
        Psms,            // inside <psms>
        IgnorePsm,       // inside <psm> when decoy or q-value above threshold
        QValue,          // inside <q_value> — collect characters
        Peptides,        // inside <peptides> (we don't consume anything here)
    }

    private ParserState _state = ParserState.Start;

    // cpp parity: PercolatorXmlReader.h:74 — buffer for the characters() callback while we're
    // inside <q_value>.
    private readonly StringBuilder _qvalueBuffer = new(64);

    // cpp parity: PercolatorXmlReader.h:77 — std::map<string, vector<PSM*>>. cpp's std::map
    // iterates keys in ascending order; we use SortedDictionary so per-source flush order
    // (and the resulting library RefSpectraIDs) match cpp byte-for-byte.
    private readonly SortedDictionary<string, List<PSM>> _fileMap = new(StringComparer.Ordinal);

    // cpp parity: PercolatorXmlReader.h:78 — q-value threshold. cpp pulls from BuildInput.Sqt.
    private readonly double _qvalueThreshold;

    // cpp parity: PercolatorXmlReader.h:81 — 128-entry monoisotopic mass table for residue
    // lookups when interpreting non-delta bracketed modifications (e.g. "[15.99]" -> mass-residue).
    private readonly double[] _masses = AminoAcidMasses.BuildMassTable(monoisotopic: true);

    // Active XmlReader during parse — used by the GetAttrValue helpers. cpp's SAXHandler hides
    // this; we keep it in a field and expose the same helpers.
    private XmlReader? _reader;

    /// <summary>
    /// True if <paramref name="path"/> is a Percolator XML output file
    /// (<c>.perc.xml</c>). Used by <see cref="BlibBuilder"/>'s reader-factory dispatch.
    /// </summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith(".perc.xml", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Construct a PercolatorXmlReader bound to a <see cref="BlibBuilder"/>.
    /// </summary>
    /// <remarks>cpp parity: PercolatorXmlReader.cpp:39.</remarks>
    public PercolatorXmlReader(BlibBuilder maker, string filename, ProgressIndicator? parentProgress)
        : base(maker, filename, parentProgress)
    {
        // cpp parity: PercolatorXmlReader.cpp:44 — qvalueThreshold_ comes from the SQT default,
        // because Percolator was originally an SQT-postprocessor and BlibBuilder's cutoff
        // semantics are inherited from there.
        _qvalueThreshold = GetScoreThreshold(BuildInput.Sqt);
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: PercolatorXmlReader.cpp:63.</remarks>
    public override bool ParseFile()
    {
        // cpp parity: PercolatorXmlReader.cpp:65 — saxhandler walks the XML; state machine in
        // StartElement / EndElement populates _fileMap.
        Parse();

        // cpp parity: PercolatorXmlReader.cpp:67 — spectrum-file extensions, priority order.
        var extensions = new List<string> { ".ms2", ".cms2", ".bms2", ".pms2" };

        // cpp parity: PercolatorXmlReader.cpp:73 — extra search dirs (relative to the result file).
        var dirs = new List<string> { "../sequest/", "../", "../../" };

        // cpp parity: PercolatorXmlReader.cpp:78 — a separate BlibBuilder so the modsReader
        // doesn't write into the real library.
        var tmpBuilder = new BlibBuilder();

        if (_fileMap.Count > 1)
            InitSpecFileProgress(_fileMap.Count);

        // cpp parity: PercolatorXmlReader.cpp:85 — iterate every (filename -> psms) bucket.
        foreach (var kvp in _fileMap)
        {
            // cpp parity: PercolatorXmlReader.cpp:87-105 — split the bucket name into
            // parent path + filename; if it has a parent path, prepend that to the search dirs.
            var filenameInput = kvp.Key;
            var spectrumParentPath = Path.GetDirectoryName(filenameInput) ?? string.Empty;
            var spectrumLeaf = Path.GetFileName(filenameInput);
            var hadParent = !string.IsNullOrEmpty(spectrumParentPath);

            if (hadParent)
                dirs.Add(spectrumParentPath);

            SetSpecFileName(spectrumLeaf, extensions, dirs);

            if (hadParent)
                dirs.RemoveAt(dirs.Count - 1);

            var psms = kvp.Value;

            // cpp parity: PercolatorXmlReader.cpp:109 — try the .sqt alongside the spectrum file,
            // then alongside the .perc.xml, then in the current directory.
            var fullFilename = BlibUtils.GetPath(GetSpecFileName()) + spectrumLeaf + ".sqt";
            if (!File.Exists(fullFilename))
            {
                fullFilename = BlibUtils.GetPath(GetFileName()) + spectrumLeaf + ".sqt";
                if (!File.Exists(fullFilename))
                    fullFilename = spectrumLeaf + ".sqt";
            }

            // cpp parity: PercolatorXmlReader.cpp:133 — open the SQT to learn the mod table;
            // the only failure we care about is "couldn't open" (rethrown with a hint about
            // why an SQT is needed). All other SQT parse errors are swallowed — the cpp
            // comment says "ignore warning that perc wasn't run on the sqt".
            var modsReader = new SQTreader(tmpBuilder, fullFilename, parentProgress: null);
            try
            {
                modsReader.OpenRead(warnIfNotPercolated: false);
            }
            catch (BlibException e)
            {
                if (e.Message.StartsWith("Couldn't open", StringComparison.Ordinal))
                {
                    e.AddMessage(" SQT file required for reading modifications.");
                    throw;
                }
                // ignore — perc wasn't run on the sqt
            }

            ApplyModifications(psms, modsReader);

            // cpp parity: PercolatorXmlReader.cpp:146 — transfer to BuildParser list, flush.
            Psms.Clear();
            foreach (var psm in psms)
                Psms.Add(psm);

            BuildTables(PsmScoreType.PercolatorQValue);
            Psms.Clear();
        }

        return true;
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: PercolatorXmlReader.cpp:155.</remarks>
    public override IList<PsmScoreType> GetScoreTypes() =>
        new[] { PsmScoreType.PercolatorQValue };

    // ----------------------------------------------------------------------------------
    // XmlReader driver. cpp's SAXHandler::parse() walks the document with Expat; we mirror
    // that with XmlReader, dispatching to StartElement / EndElement / Characters.
    // ----------------------------------------------------------------------------------

    private void Parse()
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = false, // we need text inside <q_value>
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
                            // SAXHandler emits matching endElement for self-closing elements too.
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

    // cpp parity: SAXHandler.h:96 — isElement (case-sensitive).
    private static bool IsElement(string expected, string actual) =>
        string.Equals(expected, actual, StringComparison.Ordinal);

    // cpp parity: SAXHandler.h:107 — getAttrValue, returns null/empty if missing.
    private string? GetAttrValue(string name) => _reader!.GetAttribute(name);

    // cpp parity: SAXHandler.h:121 — getRequiredAttrValue, throws if missing.
    private string GetRequiredAttrValue(string name)
    {
        var v = _reader!.GetAttribute(name);
        if (string.IsNullOrEmpty(v))
            throw new BlibException(true,
                $"Missing required attribute '{name}' in {GetFileName()} (element '{_reader.LocalName}').");
        return v;
    }

    // ----------------------------------------------------------------------------------
    // Element handlers — direct port of PercolatorXmlReader.cpp:162 startElement /
    // PercolatorXmlReader.cpp:202 endElement / PercolatorXmlReader.cpp:370 characters.
    // ----------------------------------------------------------------------------------

    // cpp parity: PercolatorXmlReader.cpp:162.
    private void StartElement(string name)
    {
        switch (_state)
        {
            case ParserState.Start:
                if (IsElement("percolator_output", name))
                    _state = ParserState.Root;
                break;

            case ParserState.Root:
                if (IsElement("psms", name))
                    _state = ParserState.Psms;
                else if (IsElement("peptides", name))
                    _state = ParserState.Peptides;
                break;

            case ParserState.Psms:
                if (IsElement("psm", name))
                {
                    ParseId();
                }
                else if (IsElement("q_value", name))
                {
                    _state = ParserState.QValue;
                    _qvalueBuffer.Clear();
                }
                else if (IsElement("peptide_seq", name))
                {
                    ParseSequence();
                }
                break;

            case ParserState.IgnorePsm:
            case ParserState.QValue:
            case ParserState.Peptides:
            default:
                return;
        }
    }

    // cpp parity: PercolatorXmlReader.cpp:202.
    private void EndElement(string name)
    {
        switch (_state)
        {
            case ParserState.Psms:
                if (IsElement("psms", name))
                {
                    _state = ParserState.Root;
                }
                else if (IsElement("psm", name))
                {
                    AddCurPsm();
                }
                break;

            case ParserState.IgnorePsm:
                if (IsElement("psm", name))
                {
                    _state = ParserState.Psms;
                }
                break;

            case ParserState.QValue:
                if (IsElement("q_value", name))
                {
                    // cpp parity: PercolatorXmlReader.cpp:220 — set the q-value on the curPSM_;
                    // if it's above threshold, drop into IgnorePsm for the rest of this <psm>.
                    if (!double.TryParse(_qvalueBuffer.ToString(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var qvalue))
                    {
                        qvalue = 0; // cpp atof returns 0 on garbage
                    }
                    if (CurPsm != null)
                        CurPsm.Score = qvalue;
                    _state = CurPsm != null && CurPsm.Score > _qvalueThreshold
                        ? ParserState.IgnorePsm
                        : ParserState.Psms;
                }
                break;
        }
    }

    /// <summary>
    /// cpp parity: PercolatorXmlReader.cpp:370 — characters handler. We only want the
    /// q-value text, so we ignore everything outside <c>QValue</c> state.
    /// </summary>
    private void Characters(string text)
    {
        if (_state != ParserState.QValue || string.IsNullOrEmpty(text))
            return;
        _qvalueBuffer.Append(text);
    }

    /// <summary>
    /// cpp parity: PercolatorXmlReader.cpp:238 — psm start. Decides whether to keep this PSM
    /// based on the p:decoy attribute, and parses filename / scan / charge out of the
    /// underscore-delimited psm_id.
    /// </summary>
    private void ParseId()
    {
        // cpp parity: PercolatorXmlReader.cpp:240 — getAttrValue returns "" (not null) for
        // missing attributes, so the "p:decoy not set" path falls through. Only an explicit
        // "true" attribute trips the IgnorePsm transition.
        var isDecoy = GetAttrValue("p:decoy") ?? string.Empty;
        if (string.Equals(isDecoy, "true", StringComparison.Ordinal))
        {
            _state = ParserState.IgnorePsm;
            return;
        }

        CurPsm ??= new PSM();

        var idStr = GetRequiredAttrValue("p:psm_id");

        // cpp parity: PercolatorXmlReader.cpp:254 — split on '_' and interpret right-to-left
        // so a filename with embedded underscores still rebuilds correctly. The last token is
        // some ordinal we ignore (cpp comment: "whose meaning I've never known"), then come
        // charge, scan, and the remaining tokens form the filename.
        var tokens = idStr.Split('_');
        if (tokens.Length < 4)
            throw new BlibException(false, $"Error parsing psm_id '{idStr}'.");

        // tokens arranged: <name parts>... <scan> <charge> <ordinal>.
        // Last token is an ordinal cpp calls "whose meaning I've never known" and we ignore.
        var charge = int.TryParse(tokens[tokens.Length - 2], NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var c) ? c : 0;
        var scan = int.TryParse(tokens[tokens.Length - 3], NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var s) ? s : 0;

        CurPsm.Charge = charge;
        CurPsm.SpecKey = scan;

        // cpp parity: PercolatorXmlReader.cpp:275 — reconstruct the filename from the remaining
        // tokens (everything except the last three).
        var filenameTokens = tokens.Length - 3;
        var sb = new StringBuilder();
        for (var i = 0; i < filenameTokens; i++)
        {
            if (i > 0) sb.Append('_');
            sb.Append(tokens[i]);
        }
        // cpp parity: PercolatorXmlReader.cpp:284 — hijack specName to carry the filename
        // until addCurPSM moves the PSM into the file-keyed map.
        CurPsm.SpecName = sb.ToString();
    }

    /// <summary>
    /// cpp parity: PercolatorXmlReader.cpp:291 — peptide_seq handler. Strips inline bracketed
    /// mods into <see cref="PSM.Mods"/> and stores the un-bracketed sequence (still containing
    /// marker characters like '*') in <see cref="PSM.UnmodSeq"/> for later translation by
    /// <see cref="SQTreader.ParseModifiedSeq"/>.
    /// </summary>
    private void ParseSequence()
    {
        if (CurPsm is null)
        {
            throw new BlibException(false,
                "Encountered a peptide sequence with no spectrum to assign it to.");
        }

        var seq = GetRequiredAttrValue("seq");
        var sb = new StringBuilder(seq.Length);
        var aaCount = 0;

        for (var i = 0; i < seq.Length; i++)
        {
            var cur = seq[i];
            if (cur == '[')
            {
                // cpp parity: PercolatorXmlReader.cpp:308 — consume "[...]" as a bracketed mod.
                i++; // skip past '['
                var modClose = seq.IndexOf(']', i);
                if (modClose < 0)
                {
                    throw new BlibException(false,
                        $"Sequence '{seq}' has opening bracket with no closing bracket.");
                }

                var modSeq = seq.Substring(i, modClose - i);
                if (!double.TryParse(modSeq, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var modMass))
                {
                    throw new BlibException(false,
                        $"Sequence '{seq}' has an unreadable modification.");
                }

                double deltaMass;
                if (modSeq.Length > 0 && (modSeq[0] == '-' || modSeq[0] == '+'))
                {
                    // signed -> assume it's already a delta
                    deltaMass = modMass;
                }
                else
                {
                    // unsigned -> cpp PercolatorXmlReader.cpp:335 treats this as monoisotopic
                    // residue mass + modification mass and subtracts the residue mass to get the
                    // delta. cpp passes seq[i-1] which, after `++i` skipped past '[', is the
                    // '[' character itself — a latent cpp bug. cpp's getPeptideMass on "["
                    // silently returns 0 (string::find/substr accept npos), so the net effect
                    // is `deltaMass = modMass - 0`. The bracket goldens encode that. We mirror
                    // exactly: any non-A-Z residue contributes 0.
                    if (i == 1)
                    {
                        throw new BlibException(false,
                            $"Error assigning modification to amino acid in sequence {seq}");
                    }
                    var residueChar = seq[i - 1]; // cpp parity: '[' itself, not the residue
                    var residueMass = char.IsAsciiLetterUpper(residueChar)
                        ? _masses[residueChar]
                        : 0.0;
                    deltaMass = modMass - residueMass;
                }

                CurPsm.Mods.Add(new SeqMod(aaCount, deltaMass));
                i = modClose; // for-loop's ++i skips past ']'
            }
            else if (cur == ']')
            {
                throw new BlibException(false,
                    $"Sequence '{seq}' has closing bracket with no opening bracket.");
            }
            else
            {
                if (char.IsAsciiLetter(cur))
                    aaCount++;
                sb.Append(char.ToUpperInvariant(cur));
            }
        }

        CurPsm.UnmodSeq = sb.ToString();
    }

    /// <summary>
    /// cpp parity: PercolatorXmlReader.cpp:386 — psm end. If the score passed threshold,
    /// route the PSM into <see cref="_fileMap"/> keyed by its (hijacked) filename; otherwise
    /// drop it and record an empty bucket so the file is still visited by ParseFile's loop.
    /// </summary>
    private void AddCurPsm()
    {
        if (CurPsm is null)
            throw new BlibException(false, "No PSM was read for this 'psm' tag.");

        var filename = CurPsm.SpecName;

        if (CurPsm.Score < _qvalueThreshold)
        {
            // cpp parity: PercolatorXmlReader.cpp:392 — clear the hijacked field now that
            // we're filing it under the map key.
            CurPsm.SpecName = string.Empty;

            Verbosity.Comment(VerbosityLevel.Detail,
                $"For file {filename} adding PSM: scan {CurPsm.SpecKey}, charge {CurPsm.Charge}, sequence '{CurPsm.UnmodSeq}'.");

            if (!_fileMap.TryGetValue(filename, out var bucket))
            {
                bucket = new List<PSM>();
                _fileMap[filename] = bucket;
            }
            bucket.Add(CurPsm);
            CurPsm = null;
        }
        else
        {
            FilteredOutPsmCount++;
            // cpp parity: PercolatorXmlReader.cpp:413 — register the filename even though
            // we're dropping this PSM, so ParseFile still iterates the bucket.
            if (!_fileMap.ContainsKey(filename))
                _fileMap[filename] = new List<PSM>();
            CurPsm = null;
        }

        _state = ParserState.Psms;
    }

    /// <summary>
    /// cpp parity: PercolatorXmlReader.cpp:422 — translate the marker-character sequences
    /// stored in <see cref="PSM.UnmodSeq"/> into clean <see cref="PSM.UnmodSeq"/> + a list
    /// of mods, using the SQT file's DiffMod / StaticMod tables.
    /// </summary>
    private static void ApplyModifications(List<PSM> psms, SQTreader modsReader)
    {
        foreach (var psm in psms)
        {
            var modSeq = psm.UnmodSeq;
            psm.UnmodSeq = string.Empty;
            modsReader.ParseModifiedSeq(modSeq, out var cleanSeq, psm.Mods, hasFlankingAa: false);
            psm.UnmodSeq = cleanSeq;
        }
    }
}
