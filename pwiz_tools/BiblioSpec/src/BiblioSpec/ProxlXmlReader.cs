// Port of pwiz_tools/BiblioSpec/src/ProxlXmlReader.{h,cpp}
//
// Reads Proxl XML peptide-crosslink result files (.proxl.xml). The cpp reader is a
// SAXHandler driven by Expat; this C# port walks the document with XmlReader and uses
// the same element state-machine layout as the cpp class.

using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Reads Proxl XML peptide-crosslink search-result files into <see cref="PSM"/>s.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::ProxlXmlReader</c>
/// (<c>pwiz_tools/BiblioSpec/src/ProxlXmlReader.{h,cpp}</c>). The cpp reader inherits
/// SAXHandler and uses an explicit state stack; this port walks the document with
/// <see cref="XmlReader"/> while preserving the same <see cref="ParserState"/> stack so
/// cpp parity is straightforward.</para>
/// <para>Peaks come from a referenced spectrum file (mzML / .ms2 / vendor formats),
/// resolved via <see cref="BuildParser.SetSpecFileName(string, IList{string}, IList{string})"/>,
/// mirroring the PepXML / MzIdentML dispatch.</para>
/// </remarks>
public sealed class ProxlXmlReader : BuildParser
{
    /// <summary>cpp parity: ProxlXmlReader.h:41 — <c>STATE</c> enum.</summary>
    private enum ParserState
    {
        InvalidState,
        RootState,
        ReportedPeptidesState,
        ReportedPeptideState,
        PeptidesState,
        PeptideState,
        ModificationsState,
        LinkedPositionsState,
        PsmsState,
        PsmState,
        FilterablePsmAnnotationsState,
        StaticModificationsState,
    }

    /// <summary>cpp parity: ProxlXmlReader.h:49 — <c>LinkType</c> enum class.</summary>
    private enum LinkType
    {
        Unlinked,
        Crosslink,
        Looplink,
        Other,
    }

    /// <summary>cpp parity: ProxlXmlReader.h:80 — <c>ANALYSIS</c> enum.</summary>
    private enum Analysis
    {
        Unknown,
        Percolator,
        Byonic,
        Plink,
        Merox,
        PeptideProphet,
    }

    /// <summary>cpp parity: ProxlXmlReader.h:57 — <c>ProxlPeptide</c>.</summary>
    private sealed class ProxlPeptide
    {
        public string Sequence { get; set; } = string.Empty;
        public List<SeqMod> Mods { get; } = new();
        public List<int> Links { get; } = new();

        public ProxlPeptide() { }
        public ProxlPeptide(string sequence) { Sequence = sequence; }

        // cpp parity: ProxlXmlReader.h:62 — peptide.mass() uses static calcMass.
        public double Mass(double[] aaMasses) => CalcMass(Sequence, Mods, aaMasses);
    }

    /// <summary>cpp parity: ProxlXmlReader.h:69 — <c>ProxlPsm : PSM</c>.</summary>
    private sealed class ProxlPsm : PSM
    {
        public double LinkerMass { get; set; }
    }

    /// <summary>cpp parity: ProxlXmlReader.h:74 — <c>ProxlMatches</c> aggregate.</summary>
    private sealed class ProxlMatches
    {
        public List<ProxlPeptide> Peptides { get; } = new();
        public Dictionary<string, List<ProxlPsm>> Psms { get; } = new(StringComparer.Ordinal);
        public LinkType LinkType { get; set; }
    }

    /// <summary>cpp parity: ProxlXmlReader.h:89 — static 128-element residue mass table.</summary>
    private static readonly double[] _aaMasses = AminoAcidMasses.BuildMassTable(monoisotopic: true);

    // cpp parity: ProxlXmlReader.h:97 — state stack.
    private readonly List<ParserState> _state = new();
    // cpp parity: ProxlXmlReader.h:98 — accumulated PSMs by spec-filename.
    private readonly Dictionary<string, List<PSM?>> _fileToPsms = new(StringComparer.Ordinal);
    // cpp parity: ProxlXmlReader.h:99 — static mods (amino acid -> [delta]).
    private readonly Dictionary<char, List<double>> _staticMods = new();
    // cpp parity: ProxlXmlReader.h:100 — when true, return early after the score type is known.
    private bool _isScoreLookup;

    // cpp parity: ProxlXmlReader.h:102-105.
    private readonly List<string> _searchPrograms = new();
    private Analysis _analysisType = Analysis.Unknown;
    private ProxlPsm? _curProxlPsm;
    private readonly List<ProxlMatches> _proxlMatches = new();

    // cpp parity: ProxlXmlReader.h:106-107 — spec-file lookup helpers.
    private readonly List<string> _dirs = new();
    private readonly List<string> _extensions = new();

    // Active XmlReader during parse; mirrors the SAXHandler pattern used by PepXMLreader.cs.
    private XmlReader? _reader;

    /// <summary>
    /// True if <paramref name="path"/> is a Proxl XML file (<c>.proxl.xml</c>).
    /// Used by <see cref="BlibBuilder"/>'s reader-factory dispatch.
    /// </summary>
    public static bool AcceptsExtension(string path) =>
        BlibBuilder.HasExtensionCi(path, ".proxl.xml");

    /// <summary>Construct a ProxlXmlReader bound to <paramref name="maker"/>.</summary>
    /// <remarks>cpp parity: ProxlXmlReader.cpp:30.</remarks>
    public ProxlXmlReader(BlibBuilder maker, string filename, ProgressIndicator? parentProgress)
        : base(maker, filename, parentProgress)
    {
        // cpp parity: ProxlXmlReader.cpp:35 — look in parent + grandparent directories as well.
        _dirs.Add("../");
        _dirs.Add("../../");

        // cpp parity: ProxlXmlReader.cpp:37 — supported spectrum-file extensions, priority order.
        _extensions.Add(".mz5");
        _extensions.Add(".mzML");
        _extensions.Add(".mzXML");
        // cpp parity: ProxlXmlReader.cpp:40 — VENDOR_READERS guarded set. In the C# port we
        // always include them, matching the other ported readers (PepXMLreader, etc.).
        _extensions.Add(".raw");   // Waters/Thermo
        _extensions.Add(".wiff");  // Sciex
        _extensions.Add(".wiff2"); // Sciex
        _extensions.Add(".d");     // Bruker/Agilent
        _extensions.Add(".lcd");   // Shimadzu
        _extensions.Add(".ms2");
        _extensions.Add(".cms2");
        _extensions.Add(".bms2");
        _extensions.Add(".pms2");
        _extensions.Add(".mgf");
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: ProxlXmlReader.cpp:60.</remarks>
    public override bool ParseFile()
    {
        Parse();
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: ProxlXmlReader.cpp:64.</remarks>
    public override IList<PsmScoreType> GetScoreTypes()
    {
        _isScoreLookup = true;
        try
        {
            Parse();
        }
        catch (EndEarlyException)
        {
            // cpp parity: SAXHandler::EndEarlyException short-circuits the parse.
        }
        return new List<PsmScoreType> { AnalysisToScoreType(_analysisType) };
    }

    /// <summary>Internal exception used to short-circuit GetScoreTypes once the score type is known.</summary>
    /// <remarks>cpp parity: SAXHandler::EndEarlyException (referenced from ProxlXmlReader.cpp:69).</remarks>
    private sealed class EndEarlyException : Exception { }

    /// <summary>cpp parity: ProxlXmlReader.cpp:74 — map analysis program to library score type.</summary>
    private static PsmScoreType AnalysisToScoreType(Analysis analysisType) => analysisType switch
    {
        Analysis.Byonic => PsmScoreType.ByonicPep,
        Analysis.Percolator => PsmScoreType.PercolatorQValue,
        Analysis.Plink => PsmScoreType.GenericQValue,
        Analysis.PeptideProphet => PsmScoreType.GenericQValue,
        // cpp returns UNKNOWN for MeroX and Unknown alike (the switch has no MEROX case).
        _ => PsmScoreType.UnknownScoreType,
    };

    // ----------------------------------------------------------------------------------
    // XmlReader driver. cpp's SAXHandler::parse() walks with Expat; we mirror it with
    // XmlReader, dispatching to StartElement/EndElement.
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
                        // SAXHandler emits a matching endElement for self-closing elements too.
                        EndElement(name);
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    EndElement(reader.LocalName);
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
            // cpp parity: SAXHandler wraps Expat errors with filename + line; do the same for XmlException.
            throw new BlibException(true,
                $"XML parse error in {GetFileName()} (line {ex.LineNumber}, position {ex.LinePosition}): {ex.Message}");
        }
        finally
        {
            _reader = null;
        }
    }

    // cpp parity: SAXHandler.h:96 — isIElement (case-insensitive); proxl uses isIElement throughout.
    private static bool IsIElement(string expected, string actual) =>
        string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

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
    // Element handlers — direct port of ProxlXmlReader.cpp:88 startElement /
    // ProxlXmlReader.cpp:229 endElement.
    // ----------------------------------------------------------------------------------

    // cpp parity: ProxlXmlReader.cpp:88.
    private void StartElement(string name)
    {
        if (_state.Count == 0)
        {
            if (IsIElement("proxl_input", name))
                _state.Add(ParserState.RootState);
            return;
        }

        switch (_state[^1])
        {
            case ParserState.RootState:
                if (IsIElement("reported_peptides", name))
                {
                    _state.Add(ParserState.ReportedPeptidesState);
                }
                else if (IsIElement("static_modifications", name))
                {
                    _state.Add(ParserState.StaticModificationsState);
                }
                else if (IsIElement("search_program", name))
                {
                    var program = GetRequiredAttrValue("name");
                    _searchPrograms.Add(program);
                    var lower = program.ToLowerInvariant();
                    if (lower == "percolator")
                        _analysisType = Analysis.Percolator;
                    else if (lower == "byonic")
                        _analysisType = Analysis.Byonic;
                    else if (lower == "plink")
                        _analysisType = Analysis.Plink;
                    else if (lower == "merox")
                        _analysisType = Analysis.Merox;
                    else if (lower == "peptideprophet")
                        _analysisType = Analysis.PeptideProphet;
                }
                break;

            case ParserState.ReportedPeptidesState:
                if (IsIElement("reported_peptide", name))
                {
                    _state.Add(ParserState.ReportedPeptideState);

                    var matches = new ProxlMatches();
                    var type = GetRequiredAttrValue("type");
                    matches.LinkType = type switch
                    {
                        "unlinked" => LinkType.Unlinked,
                        "crosslink" => LinkType.Crosslink,
                        "looplink" => LinkType.Looplink,
                        _ => LinkType.Other,
                    };
                    _proxlMatches.Add(matches);
                }
                break;

            case ParserState.ReportedPeptideState:
                if (_analysisType == Analysis.Unknown)
                {
                    throw new BlibException(false,
                        "only Byonic, MeroX, Peptide Prophet, Percolator, and pLink ProxlXML files are supported; " +
                        "cannot handle search program: " + string.Join(", ", _searchPrograms));
                }
                if (_isScoreLookup)
                    throw new EndEarlyException();

                if (IsIElement("peptides", name))
                    _state.Add(ParserState.PeptidesState);
                else if (IsIElement("psms", name))
                    _state.Add(ParserState.PsmsState);
                break;

            case ParserState.PeptidesState:
                if (IsIElement("peptide", name))
                {
                    _state.Add(ParserState.PeptideState);
                    _proxlMatches[^1].Peptides.Add(new ProxlPeptide(GetRequiredAttrValue("sequence")));
                }
                break;

            case ParserState.PeptideState:
                if (IsIElement("modifications", name))
                    _state.Add(ParserState.ModificationsState);
                else if (IsIElement("linked_positions", name))
                    _state.Add(ParserState.LinkedPositionsState);
                break;

            case ParserState.ModificationsState:
                if (IsIElement("modification", name))
                {
                    // cpp parity: ProxlXmlReader.cpp:163 — 1-based positions.
                    _proxlMatches[^1].Peptides[^1].Mods.Add(
                        new SeqMod(GetIntRequiredAttrValue("position"), GetDoubleRequiredAttrValue("mass")));
                }
                break;

            case ParserState.LinkedPositionsState:
                if (IsIElement("linked_position", name))
                {
                    _proxlMatches[^1].Peptides[^1].Links.Add(GetIntRequiredAttrValue("position"));
                }
                break;

            case ParserState.PsmsState:
                if (IsIElement("psm", name))
                {
                    _state.Add(ParserState.PsmState);

                    _curProxlPsm = new ProxlPsm();
                    var filename = GetRequiredAttrValue("scan_file_name");
                    var bucket = _proxlMatches[^1].Psms;
                    if (!bucket.TryGetValue(filename, out var list))
                    {
                        list = new List<ProxlPsm>();
                        bucket[filename] = list;
                    }
                    list.Add(_curProxlPsm);

                    // Sequence/mods are populated at the reported_peptide end tag.
                    _curProxlPsm.Charge = GetIntRequiredAttrValue("precursor_charge");
                    _curProxlPsm.SpecKey = GetIntRequiredAttrValue("scan_number");
                    _curProxlPsm.Score = 1;
                    _curProxlPsm.LinkerMass = _proxlMatches[^1].LinkType != LinkType.Unlinked
                        ? GetDoubleRequiredAttrValue("linker_mass")
                        : 0;
                }
                break;

            case ParserState.PsmState:
                if (IsIElement("filterable_psm_annotations", name))
                    _state.Add(ParserState.FilterablePsmAnnotationsState);
                break;

            case ParserState.FilterablePsmAnnotationsState:
                if (IsIElement("filterable_psm_annotation", name) && _curProxlPsm != null)
                {
                    // cpp uses search_program + annotation_name; only annotation_name is consulted
                    // in the switch below, but parity preserves the read of both.
                    _ = GetRequiredAttrValue("search_program");
                    var score = GetRequiredAttrValue("annotation_name").ToLowerInvariant();
                    var capture =
                        (_analysisType == Analysis.Percolator && score == "q-value") ||
                        (_analysisType == Analysis.Byonic && score == "peptide abslogprob2d") ||
                        (_analysisType == Analysis.Plink && score == "score") ||
                        (_analysisType == Analysis.Merox && score == "qvalue") ||
                        (_analysisType == Analysis.PeptideProphet && score == "pprophet fdr");
                    if (capture)
                        _curProxlPsm.Score = GetDoubleRequiredAttrValue("value");

                    // cpp parity: ProxlXmlReader.cpp:208 — Byonic stores -log10(prob); invert it.
                    // NOTE: cpp applies this on every filterable_psm_annotation seen while Byonic
                    // is active, which is a latent cpp bug if multiple annotations precede the
                    // PEP one. We mirror exactly so library outputs match.
                    if (_analysisType == Analysis.Byonic)
                        _curProxlPsm.Score = Math.Pow(10, -1 * _curProxlPsm.Score);
                }
                break;

            case ParserState.StaticModificationsState:
                if (IsIElement("static_modification", name))
                {
                    var aa = GetRequiredAttrValue("amino_acid");
                    var mass = GetDoubleRequiredAttrValue("mass_change");
                    foreach (var c in aa)
                    {
                        if (!_staticMods.TryGetValue(c, out var list))
                        {
                            list = new List<double>();
                            _staticMods[c] = list;
                        }
                        list.Add(mass);
                    }
                }
                break;
        }
    }

    // cpp parity: ProxlXmlReader.cpp:229.
    private void EndElement(string name)
    {
        if (_state.Count == 0)
            return;

        switch (_state[^1])
        {
            case ParserState.RootState:
                if (IsIElement("proxl_input", name))
                {
                    _state.RemoveAt(_state.Count - 1);

                    // cpp parity: ProxlXmlReader.cpp:238 — convert collected matches into PSMs,
                    // then BuildTables once per spec-file group.
                    CalcPsms();
                    foreach (var kvp in _fileToPsms)
                    {
                        Psms.Clear();
                        foreach (var psm in kvp.Value)
                            Psms.Add(psm);

                        SetSpecFileName(kvp.Key, _extensions, _dirs);
                        BuildTables(PsmScoreType.PercolatorQValue);
                    }
                }
                break;

            case ParserState.ReportedPeptidesState:
                if (IsIElement("reported_peptides", name))
                    _state.RemoveAt(_state.Count - 1);
                break;

            case ParserState.ReportedPeptideState:
                if (IsIElement("reported_peptide", name))
                    _state.RemoveAt(_state.Count - 1);
                break;

            case ParserState.PeptidesState:
                if (IsIElement("peptides", name))
                    _state.RemoveAt(_state.Count - 1);
                break;

            case ParserState.PeptideState:
                if (IsIElement("peptide", name))
                    _state.RemoveAt(_state.Count - 1);
                break;

            case ParserState.ModificationsState:
                if (IsIElement("modifications", name))
                    _state.RemoveAt(_state.Count - 1);
                break;

            case ParserState.LinkedPositionsState:
                if (IsIElement("linked_positions", name))
                    _state.RemoveAt(_state.Count - 1);
                break;

            case ParserState.PsmsState:
                if (IsIElement("psms", name))
                    _state.RemoveAt(_state.Count - 1);
                break;

            case ParserState.PsmState:
                if (IsIElement("psm", name))
                    _state.RemoveAt(_state.Count - 1);
                break;

            case ParserState.FilterablePsmAnnotationsState:
                if (IsIElement("filterable_psm_annotations", name))
                    _state.RemoveAt(_state.Count - 1);
                break;

            case ParserState.StaticModificationsState:
                if (IsIElement("static_modifications", name))
                    _state.RemoveAt(_state.Count - 1);
                break;
        }
    }

    // cpp parity: ProxlXmlReader.cpp:303 — calcMass: H2O + residue + mod deltas.
    private static double CalcMass(string sequence, IReadOnlyList<SeqMod> mods, double[] aaMasses)
    {
        var sum = 2 * aaMasses['h'] + aaMasses['o'];
        foreach (var c in sequence)
            sum += aaMasses[c];
        foreach (var mod in mods)
            sum += mod.DeltaMass;
        return sum;
    }

    // cpp parity: ProxlXmlReader.cpp:312 — analysis-type-specific score threshold.
    private double GetScoreThreshold()
    {
        return _analysisType switch
        {
            Analysis.Byonic => GetScoreThreshold(BuildInput.Byonic),
            Analysis.Percolator => GetScoreThreshold(BuildInput.GenericQValueInput),
            Analysis.Plink => GetScoreThreshold(BuildInput.GenericQValueInput),
            Analysis.Merox => GetScoreThreshold(BuildInput.GenericQValueInput),
            Analysis.PeptideProphet => GetScoreThreshold(BuildInput.GenericQValueInput),
            _ => throw new BlibException(false,
                $"no case for analysisType_: {(int)_analysisType}"),
        };
    }

    // cpp parity: ProxlXmlReader.cpp:329 — turn ProxlMatches into PSMs grouped by spec-file.
    private void CalcPsms()
    {
        var scoreThreshold = GetScoreThreshold();

        foreach (var match in _proxlMatches)
        {
            // cpp parity: ProxlXmlReader.cpp:335 — apply static mods per peptide.
            foreach (var peptide in match.Peptides)
            {
                ApplyStaticMods(peptide.Sequence, peptide.Mods,
                    peptide.Links.Count == 0 ? -1 : peptide.Links[0]);
            }

            foreach (var psmPair in match.Psms)
            {
                if (!_fileToPsms.TryGetValue(psmPair.Key, out var bucket))
                {
                    bucket = new List<PSM?>();
                    _fileToPsms[psmPair.Key] = bucket;
                }

                foreach (var proxlPsm in psmPair.Value)
                {
                    if (proxlPsm.Score <= scoreThreshold)
                    {
                        switch (match.LinkType)
                        {
                            case LinkType.Unlinked:
                            {
                                if (match.Peptides.Count != 1)
                                    throw new BlibException(false,
                                        "[calcPsms] unexpected number of peptides in unlinked peptide: " +
                                        match.Peptides.Count.ToString(CultureInfo.InvariantCulture));

                                var pepA = match.Peptides[0];
                                var psm = ClonePsm(proxlPsm);
                                psm.UnmodSeq = pepA.Sequence;
                                foreach (var mod in pepA.Mods)
                                    psm.Mods.Add(mod);
                                bucket.Add(psm);
                                break;
                            }

                            case LinkType.Crosslink:
                            {
                                if (match.Peptides.Count != 2)
                                    throw new BlibException(false,
                                        "[calcPsms] unexpected number of peptides in crosslink: " +
                                        match.Peptides.Count.ToString(CultureInfo.InvariantCulture));

                                var pepA = match.Peptides[0];
                                var pepB = match.Peptides[1];
                                if (pepA.Links.Count != 1 || pepB.Links.Count != 1)
                                    throw new BlibException(false,
                                        "[calcPsms] unexpected number of links on crosslink: " +
                                        pepA.Sequence + "/" + pepB.Sequence + " " +
                                        pepA.Links.Count.ToString(CultureInfo.InvariantCulture) + "/" +
                                        pepB.Links.Count.ToString(CultureInfo.InvariantCulture));

                                var psm = ClonePsm(proxlPsm);
                                psm.UnmodSeq = pepA.Sequence + "-" + pepB.Sequence;
                                var modifiedPepA = BlibMaker.GenerateModifiedSeq(pepA.Sequence, pepA.Mods);
                                var modifiedPepB = BlibMaker.GenerateModifiedSeq(pepB.Sequence, pepB.Mods);
                                // cpp parity: ProxlXmlReader.cpp:378 — boost::format "%s-%s-[%+.4f@%d,%d]".
                                psm.ModifiedSeq = string.Format(
                                    CultureInfo.InvariantCulture,
                                    "{0}-{1}-[{2:+0.0000;-0.0000}@{3},{4}]",
                                    modifiedPepA, modifiedPepB,
                                    proxlPsm.LinkerMass, pepA.Links[0], pepB.Links[0]);
                                foreach (var mod in pepA.Mods)
                                    psm.Mods.Add(mod);
                                // cpp parity: ProxlXmlReader.cpp:380 — sum pepB.mass + linker as a mod at pepA's link.
                                psm.Mods.Add(new SeqMod(pepA.Links[0], pepB.Mass(_aaMasses) + proxlPsm.LinkerMass));
                                bucket.Add(psm);
                                break;
                            }

                            case LinkType.Looplink:
                            {
                                if (match.Peptides.Count != 1)
                                    throw new BlibException(false,
                                        "[calcPsms] unexpected number of peptides in looplink: " +
                                        match.Peptides.Count.ToString(CultureInfo.InvariantCulture));

                                var pepA = match.Peptides[0];
                                if (pepA.Links.Count != 2)
                                    throw new BlibException(false,
                                        "[calcPsms] unexpected number of links on looplink: " +
                                        pepA.Sequence + " " +
                                        pepA.Links.Count.ToString(CultureInfo.InvariantCulture));

                                var psm = ClonePsm(proxlPsm);
                                psm.UnmodSeq = pepA.Sequence;
                                // cpp parity: ProxlXmlReader.cpp:397-398 — overwritten by the looplink format string.
                                psm.ModifiedSeq = string.Format(
                                    CultureInfo.InvariantCulture,
                                    "{0}-[{1:+0.0000;-0.0000}@{2}-{3}]",
                                    pepA.Sequence, proxlPsm.LinkerMass, pepA.Links[0], pepA.Links[1]);
                                foreach (var mod in pepA.Mods)
                                    psm.Mods.Add(mod);
                                psm.Mods.Add(new SeqMod(pepA.Links[0], proxlPsm.LinkerMass));
                                bucket.Add(psm);
                                break;
                            }

                            case LinkType.Other:
                            default:
                                break;
                        }
                    }
                    else
                    {
                        FilteredOutPsmCount++;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Copy the per-spectrum fields of a <see cref="ProxlPsm"/> into a fresh <see cref="PSM"/>.
    /// cpp parity: ProxlXmlReader.cpp:355 <c>*psm = *proxlPsm</c> (slice-copy of the base).
    /// </summary>
    private static PSM ClonePsm(ProxlPsm source)
    {
        // We deliberately copy only the fields the cpp slice-assignment would carry: charge,
        // specKey, score (the ProxlPsm.linkerMass_ is stored separately and not part of PSM).
        // Mods/UnmodSeq/ModifiedSeq are set by the caller for each link-type case.
        return new PSM
        {
            Charge = source.Charge,
            SpecKey = source.SpecKey,
            Score = source.Score,
        };
    }

    // cpp parity: ProxlXmlReader.cpp:418 — append static-mod deltas for residues that carry them,
    // then re-sort by position when anything was added.
    private void ApplyStaticMods(string sequence, List<SeqMod> mods, int crosslinkPosition)
    {
        _ = crosslinkPosition; // cpp parity: kept in the signature; cpp comment notes the
                                // crosslink-skip is intentionally commented out.

        var varModCount = mods.Count;
        for (var i = 0; i < sequence.Length; i++)
        {
            if (!_staticMods.TryGetValue(sequence[i], out var deltas))
                continue;
            foreach (var d in deltas)
                mods.Add(new SeqMod(i + 1, d));
        }

        if (mods.Count > varModCount)
        {
            mods.Sort((lhs, rhs) => lhs.Position.CompareTo(rhs.Position));
        }
    }
}
