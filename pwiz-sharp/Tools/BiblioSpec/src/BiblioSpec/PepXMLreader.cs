// Port of pwiz_tools/BiblioSpec/src/PepXMLreader.{h,cpp}
//
// Reads Trans-Proteomic Pipeline pepXML (.pep.xml / .pepXML) — search results
// from PeptideProphet, iProphet, X! Tandem, Mascot, MSFragger, MSGF+, Crux, Comet,
// Morpheus, OMSSA, PEAKS, Protein Prospector, Spectrum Mill, and Proteome Discoverer.
//
// cpp uses Expat (SAXHandler) for parsing; the C# port uses System.Xml.XmlReader
// directly, walking elements in document order. Stage 2 of the port deliberately
// skipped SAXHandler, so each reader inlines its own XmlReader loop.

using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Reads pepXML search-result files into <see cref="PSM"/>s.
/// </summary>
/// <remarks>
/// Port of <c>BiblioSpec::PepXMLreader</c> (PepXMLreader.h:44 / PepXMLreader.cpp).
/// The cpp class is a SAXHandler subclass driven by Expat; in C# we walk the document
/// with <see cref="XmlReader"/>, calling the same <c>startElement</c> / <c>endElement</c>
/// shaped private helpers so cpp parity is easy to verify.
/// </remarks>
public class PepXMLreader : BuildParser
{
    // cpp parity: PepXMLreader.cpp:69 — ParserState enum, controls which <search_hit> we treat as "the best".
    private enum ParserState
    {
        Init = -1,
        Root = 0,
        ProphetSummary = 1,
        AnalysisSummary = 2,
        SearchHitBest = 5,
        SearchHitBestSeen = 6,
    }

    // cpp parity: PepXMLreader.h:61 — analysisType_ enum.
    private enum Analysis
    {
        Unknown,
        PeptideProphet,
        InterProphet,
        SpectrumMill,
        Omssa,
        ProteinProspector,
        Morpheus,
        Msgf,
        Peaks,
        ProteomeDiscoverer,
        XTandem,
        Crux,
        Comet,
        MsFragger,
    }

    // cpp parity: PepXMLreader.h:85 — 1 mono, 0 avg.
    private int _massType = 1;

    // cpp parity: PepXMLreader.h:86-87.
    private Analysis _analysisType = Analysis.Unknown;
    private Analysis _parentAnalysisType = Analysis.Unknown;

    // cpp parity: PepXMLreader.h:88-89.
    private WorkflowType _workflowType = WorkflowType.Dda;
    private PsmScoreType _scoreType = PsmScoreType.PeptideProphetSomething;

    // cpp parity: PepXMLreader.h:92.
    private string _fileroot = string.Empty;

    // cpp parity: PepXMLreader.h:93 — when true, we're called from GetScoreTypes and stop after first <search_summary>.
    private bool _isScoreLookup;

    // cpp parity: PepXMLreader.h:94 — true if the chosen spectrum file is MGF.
    private bool _isMgf;

    // cpp parity: PepXMLreader.h:83 — probability / cutoff for the current analysis.
    private double _probCutOff;

    // cpp parity: PepXMLreader.h:84 — 128-element residue mass table, populated by AminoAcidMasses.
    private readonly double[] _aaMass = new double[128];

    // cpp parity: PepXMLreader.h:79 — per-AA modification mass cache: aa -> (declared mass -> deltaMass).
    private readonly Dictionary<char, Dictionary<double, double>> _aaModMasses = new();

    // cpp parity: PepXMLreader.h:77-78 — directories / extensions for spectrum-file resolution.
    private readonly List<string> _dirs = new();
    private readonly List<string> _extensions = new();

    // cpp parity: PepXMLreader.h:76 — mods being collected for the current <search_hit>.
    private readonly List<SeqMod> _mods = new();

    // cpp parity: PepXMLreader.h:91 — precursor m/z lookup for SpectrumMill (used by findScanIndexFromName).
    private readonly Dictionary<PSM, double> _precursorMap = new();

    // cpp parity: per-spectrum_query / per-search_hit accumulators (PepXMLreader.h:97-108).
    private double _pepProb;
    private int _scanIndex;
    private int _scanNumber;
    private double _precursorMz;
    private int _charge;
    private double _ionMobility;
    private string _spectrumName = string.Empty;
    private string _pepSeq = string.Empty;
    private ParserState _state = ParserState.Init;
    private int _numFiles;

    // cpp parity: PepXMLreader.cpp:335 — strip "0..." padding from MSFragger MGF scan-number components.
    private static readonly Regex _msfraggerPaddingRegex =
        new(@"(.*?\.)0*(\d+\.)0*(\d+\.\d+)", RegexOptions.Compiled);

    // The active XmlReader, captured during ParseFile so the element handlers can fetch attributes.
    // cpp's SAXHandler hides this behind getAttrValue helpers; we keep the reader in a field and
    // expose equivalent helpers below.
    private XmlReader? _reader;

    /// <summary>
    /// Returns true if <paramref name="path"/> is a TPP pepXML file
    /// (<c>.pep.xml</c> / <c>.pepXML</c>). Used by <see cref="BlibBuilder"/>'s
    /// reader-factory dispatch — each reader declares its own accepted extensions
    /// in one place.
    /// </summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith(".pep.xml", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".pepXML", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Construct a PepXMLreader bound to a BlibBuilder. The cpp ctor also calls
    /// <c>setFileName</c> on the SAXHandler — in C# the filename is already carried on
    /// <see cref="BuildParser"/> so there's nothing extra to do.
    /// </summary>
    /// <remarks>cpp parity: PepXMLreader.cpp:78.</remarks>
    public PepXMLreader(BlibBuilder maker, string xmlfilename, ProgressIndicator? parentProgress)
        : base(maker, xmlfilename, parentProgress)
    {
        // cpp parity: PepXMLreader.cpp:91 — initial values.
        _numFiles = 0;
        _pepProb = 0;
        _probCutOff = GetScoreThreshold(BuildInput.PepXml);

        // cpp parity: PepXMLreader.cpp:94 — look in parent + grandparent directories as well.
        _dirs.Add("../");
        _dirs.Add("../../");

        // cpp parity: PepXMLreader.cpp:96 — supported spectrum-file extensions, priority order.
        _extensions.Add(".mz5");
        _extensions.Add(".mzML");
        _extensions.Add(".mzXML");
        // cpp parity: PepXMLreader.cpp:99 — VENDOR_READERS guarded set. In the C# port we always
        // include them — the pwiz-CLI-backed spec reader handles all of these uniformly.
        _extensions.Add(".raw");   // Waters/Thermo
        _extensions.Add(".wiff");  // Sciex
        _extensions.Add(".wiff2"); // Sciex
        _extensions.Add(".d");     // Bruker/Agilent
        _extensions.Add(".lcd");   // Shimadzu
        _extensions.Add(".ms2");
        _extensions.Add(".cms2");
        _extensions.Add(".bms2");
        _extensions.Add(".pms2");

        // cpp parity: BuildParser.cpp:39 — initialise the residue mass table (mono).
        AminoAcidMasses.InitializeMass(_aaMass, monoisotopic: true);
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: PepXMLreader.cpp:577 — wraps SAXHandler::parse().</remarks>
    public override bool ParseFile()
    {
        Parse();
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// cpp parity: PepXMLreader.cpp:582. cpp uses EndEarlyException from SAXHandler;
    /// we throw an internal exception type used only by this method.
    /// </remarks>
    public override IList<PsmScoreType> GetScoreTypes()
    {
        _isScoreLookup = true;
        try
        {
            Parse();
        }
        catch (EndEarlyException)
        {
            // cpp parity: setScoreType throws EndEarlyException once the engine is identified.
        }
        return new List<PsmScoreType> { _scoreType };
    }

    /// <summary>Internal exception used to short-circuit GetScoreTypes once the score type is known.</summary>
    /// <remarks>cpp parity: SAXHandler::EndEarlyException (referenced from PepXMLreader.cpp:591).</remarks>
    private sealed class EndEarlyException : Exception { }

    // ----------------------------------------------------------------------------------
    // XmlReader driver. cpp's SAXHandler::parse() walks the document with Expat. We mirror
    // that with XmlReader, dispatching element starts / ends to StartElement / EndElement.
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
                        // cpp's SAXHandler emits matching endElement for self-closing elements too.
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

    // cpp parity: SAXHandler.h:96 — isElement (case-sensitive in cpp, mirroring).
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

    // cpp parity: SAXHandler.h:157 — getIntRequiredAttrValue. cpp uses atoi+startsWithZero check;
    // we use int.TryParse + InvariantCulture.
    private int GetIntRequiredAttrValue(string name)
    {
        var s = GetRequiredAttrValue(name);
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
        {
            throw new BlibException(true,
                $"The value '{s}' in attribute '{name}' is not a valid integer value (element '{_reader!.LocalName}', file {GetFileName()}).");
        }
        return v;
    }

    // cpp parity: SAXHandler.h:172 — bounded variant.
    private int GetIntRequiredAttrValue(string name, int min, int max)
    {
        var v = GetIntRequiredAttrValue(name);
        if (v < min || v > max)
        {
            throw new BlibException(true,
                $"The value '{v}' in the attribute '{name}' is not between {min} and {max} " +
                $"(element '{_reader!.LocalName}', file {GetFileName()}).");
        }
        return v;
    }

    // cpp parity: SAXHandler.h:190 — getDoubleRequiredAttrValue.
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

    // cpp parity: SAXHandler.h:205 — default-falling variant.
    private double GetDoubleAttrValueOr(string name, double defaultValue)
    {
        var s = GetAttrValue(name);
        if (string.IsNullOrEmpty(s))
            return defaultValue;
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            throw new BlibException(true,
                $"The value '{s}' in attribute '{name}' could not be cast to a number (element '{_reader!.LocalName}', file {GetFileName()}).");
        }
        return v;
    }

    // ----------------------------------------------------------------------------------
    // Element handlers. Direct port of PepXMLreader.cpp:115 startElement /
    // PepXMLreader.cpp:449 endElement.
    // ----------------------------------------------------------------------------------

    // cpp parity: PepXMLreader.cpp:115.
    private void StartElement(string name)
    {
        // cpp parity: PepXMLreader.cpp:119 — must see msms_pipeline_analysis first.
        if (_state == ParserState.Init)
        {
            if (!IsElement("msms_pipeline_analysis", name))
            {
                throw new BlibException(false,
                    $"Invalid pepXML root tag '{name}' must be 'msms_pipeline_analysis'.");
            }
            _state = ParserState.Root;
        }
        else if (IsElement("peptideprophet_summary", name))
        {
            _analysisType = Analysis.PeptideProphet;
            _state = ParserState.ProphetSummary;
        }
        else if (IsElement("analysis_summary", name))
        {
            var analysis = GetAttrValue("analysis");
            if (analysis.StartsWith("interprophet", StringComparison.Ordinal))
            {
                _analysisType = Analysis.InterProphet;
                // cpp parity: PepXMLreader.cpp:136 — initialise byte-based progress, in bytes / 1000.
                try
                {
                    var size = new FileInfo(GetFileName()).Length;
                    InitSpecFileProgress((int)(size / 1000));
                }
                catch (IOException) { /* progress is best-effort */ }
            }
            _state = ParserState.AnalysisSummary;
        }
        else if (_state == ParserState.AnalysisSummary)
        {
            // cpp parity: PepXMLreader.cpp:140 — ignore anything nested inside <analysis_summary>.
            return;
        }
        else if (_state == ParserState.ProphetSummary && IsElement("inputfile", name))
        {
            // cpp parity: PepXMLreader.cpp:146 — count files for percent-complete reporting.
            _numFiles++;
        }
        else if (IsElement("msms_run_summary", name))
        {
            _fileroot = GetRequiredAttrValue("base_name");
            Verbosity.Comment(VerbosityLevel.Debug, $"PepXML base_name is {_fileroot}");

            // cpp parity: PepXMLreader.cpp:153 — Proteome Discoverer signals via raw_data_type=".msf".
            var rawType = GetAttrValue("raw_data_type");
            if (rawType == ".msf")
            {
                Verbosity.Comment(VerbosityLevel.Debug, "Pepxml file is from Proteome Discoverer.");
                _analysisType = Analysis.ProteomeDiscoverer;
            }
        }
        else if (IsElement("parameter", name))
        {
            // cpp parity: PepXMLreader.cpp:158 — parameter name/value pairs, lowercased.
            var paramName = GetAttrValue("name").ToLowerInvariant();
            var paramValue = GetAttrValue("value").ToLowerInvariant();
            if (paramName == "post-processor" && paramValue == "percolator")
            {
                _analysisType = Analysis.Crux;
                SetScoreType(PsmScoreType.PercolatorQValue);
                _probCutOff = GetScoreThreshold(BuildInput.Sqt);
            }
            else if (_analysisType == Analysis.MsFragger && paramName == "data_type")
            {
                _workflowType = paramValue == "0" ? WorkflowType.Dda : WorkflowType.Dia;
            }
        }
        else if (IsElement("search_summary", name))
        {
            HandleSearchSummary();
        }
        else if (IsElement("spectrum_query", name))
        {
            HandleSpectrumQueryStart();
        }
        else if (IsElement("search_hit", name))
        {
            HandleSearchHitStart();
        }
        else if (IsElement("modification_info", name) && _state == ParserState.SearchHitBest)
        {
            HandleModificationInfo();
        }
        else if (IsElement("mod_aminoacid_mass", name) && _state == ParserState.SearchHitBest)
        {
            HandleModAminoacidMass();
        }
        else if ((_analysisType == Analysis.PeptideProphet && IsElement("peptideprophet_result", name)) ||
                 (_analysisType == Analysis.InterProphet && IsElement("interprophet_result", name)))
        {
            // cpp parity: PepXMLreader.cpp:416 — probability is the score for these analyses.
            _pepProb = GetDoubleRequiredAttrValue("probability");
        }
        else if (_state == ParserState.SearchHitBest && IsElement("search_score", name))
        {
            HandleSearchScore();
        }
        else if (IsElement("aminoacid_modification", name))
        {
            HandleAminoacidModification();
        }
        // cpp parity: PepXMLreader.cpp:446 — "mascot score is ??" comment, no handling.
    }

    // cpp parity: PepXMLreader.cpp:449.
    private void EndElement(string name)
    {
        if (IsElement("analysis_summary", name))
        {
            _state = ParserState.Root;
        }
        else if (IsElement("peptideprophet_summary", name))
        {
            _state = ParserState.Root;
            // cpp parity: PepXMLreader.cpp:457 — now we know the number of files.
            if (_numFiles > 1)
                InitSpecFileProgress(_numFiles);
        }
        else if (IsElement("msms_run_summary", name))
        {
            HandleRunSummaryEnd();
        }
        else if (IsElement("spectrum_query", name))
        {
            HandleSpectrumQueryEnd();
        }
        else if (IsElement("search_hit", name))
        {
            if (_state == ParserState.SearchHitBest)
                _state = ParserState.SearchHitBestSeen;
        }
    }

    // ----------------------------------------------------------------------------------
    // Per-element helpers — broken out for readability but each block tracks the cpp source.
    // ----------------------------------------------------------------------------------

    // cpp parity: PepXMLreader.cpp:173 — search_summary handler (search engine detection).
    private void HandleSearchSummary()
    {
        // cpp parity: PepXMLreader.cpp:174 — lowercase + strip spaces.
        var searchEngineVersion = GetAttrValue("search_engine_version")
            .ToLowerInvariant().Replace(" ", string.Empty);

        if (_analysisType == Analysis.Unknown || _analysisType == Analysis.ProteomeDiscoverer)
        {
            var searchEngine = GetAttrValue("search_engine")
                .ToLowerInvariant().Replace(" ", string.Empty);

            if (searchEngine.StartsWith("spectrummill", StringComparison.Ordinal))
            {
                Verbosity.Comment(VerbosityLevel.Debug, "Pepxml file is from Spectrum Mill.");
                _analysisType = Analysis.SpectrumMill;
                SetScoreType(PsmScoreType.SpectrumMill);
                _probCutOff = 0; // accept all psms (cpp parity: PepXMLreader.cpp:189)
                LookUpBy = SpecIdType.IndexId;
                if (SpecReader != null) SpecReader.IdType = SpecIdType.IndexId;
            }
            else if (searchEngine.StartsWith("omssa", StringComparison.Ordinal))
            {
                Verbosity.Debug("Pepxml file is from OMSSA.");
                _analysisType = Analysis.Omssa;
                SetScoreType(PsmScoreType.OmssaExpectationScore);
                _probCutOff = GetScoreThreshold(BuildInput.Omssa);
            }
            else if (searchEngine.Contains("proteinprospector", StringComparison.Ordinal))
            {
                Verbosity.Comment(VerbosityLevel.Debug, "Pepxml file is from Protein Prospector.");
                _analysisType = Analysis.ProteinProspector;
                SetScoreType(PsmScoreType.ProteinProspectorExpect);
                _probCutOff = GetScoreThreshold(BuildInput.ProtProspect);
            }
            else if (searchEngine.StartsWith("morpheus", StringComparison.Ordinal))
            {
                Verbosity.Comment(VerbosityLevel.Debug, "Pepxml file is from Morpheus.");
                _analysisType = Analysis.Morpheus;
                SetScoreType(PsmScoreType.MorpheusScore);
                _probCutOff = GetScoreThreshold(BuildInput.Morpheus);
                LookUpBy = SpecIdType.IndexId;
                if (SpecReader != null) SpecReader.IdType = SpecIdType.IndexId;
            }
            else if (searchEngine.StartsWith("ms-gfdb", StringComparison.Ordinal))
            {
                Verbosity.Comment(VerbosityLevel.Debug, "Pepxml file is from MS-GFDB.");
                _analysisType = Analysis.Msgf;
                SetScoreType(PsmScoreType.MsgfScore);
                _probCutOff = GetScoreThreshold(BuildInput.Msgf);
                LookUpBy = SpecIdType.NameId;
                if (SpecReader != null) SpecReader.IdType = SpecIdType.NameId;
            }
            else if (searchEngine.StartsWith("peaksdb", StringComparison.Ordinal) ||
                     searchEngine.StartsWith("peaks_db", StringComparison.Ordinal))
            {
                Verbosity.Comment(VerbosityLevel.Debug, "Pepxml file is from PEAKS");
                _analysisType = Analysis.Peaks;
                SetScoreType(PsmScoreType.PeaksConfidenceScore);
                _probCutOff = GetScoreThreshold(BuildInput.Peaks);
            }
            else if (searchEngine.StartsWith("sequest", StringComparison.Ordinal) &&
                     _analysisType == Analysis.ProteomeDiscoverer)
            {
                Verbosity.Comment(VerbosityLevel.Debug, "Pepxml file is from SEQUEST Proteome Discoverer.");
                SetScoreType(PsmScoreType.PercolatorQValue);
                _probCutOff = GetScoreThreshold(BuildInput.Sqt);
            }
            else if (searchEngine.StartsWith("mascot", StringComparison.Ordinal) &&
                     _analysisType == Analysis.ProteomeDiscoverer)
            {
                Verbosity.Comment(VerbosityLevel.Debug, "Pepxml file is from Mascot Proteome Discoverer.");
                SetScoreType(PsmScoreType.MascotIonsScore);
                _probCutOff = GetScoreThreshold(BuildInput.Mascot);
            }
            else if (searchEngine.StartsWith("x!tandem", StringComparison.Ordinal) &&
                     _analysisType != Analysis.PeptideProphet)
            {
                if (searchEngineVersion.StartsWith("msfragger", StringComparison.Ordinal))
                {
                    Verbosity.Comment(VerbosityLevel.Debug, "Pepxml file is from MSFragger.");
                    _analysisType = Analysis.MsFragger;
                }
                else
                {
                    Verbosity.Comment(VerbosityLevel.Debug, "Pepxml file is from X! Tandem.");
                    _analysisType = Analysis.XTandem;
                }
                SetScoreType(PsmScoreType.TandemExpectationValue);
                _probCutOff = GetScoreThreshold(BuildInput.Tandem);
            }
            else if (searchEngine.StartsWith("crux", StringComparison.Ordinal))
            {
                Verbosity.Comment(VerbosityLevel.Debug, "Pepxml file is from Crux.");
                _analysisType = Analysis.Crux;
            }
            else if (searchEngine.StartsWith("comet", StringComparison.Ordinal))
            {
                Verbosity.Comment(VerbosityLevel.Debug, "Pepxml file is from Comet.");
                _analysisType = Analysis.Comet;
                SetScoreType(PsmScoreType.TandemExpectationValue); // expect values compatible with X!Tandem
                _probCutOff = GetScoreThreshold(BuildInput.Tandem);
            }
            // else assume peptide prophet or inter prophet (cpp parity: PepXMLreader.cpp:253).

            if (_analysisType == Analysis.ProteomeDiscoverer &&
                _scoreType != PsmScoreType.PercolatorQValue &&
                _scoreType != PsmScoreType.MascotIonsScore)
            {
                throw new BlibException(false,
                    "The .pep.xml file appears to be from Proteome Discoverer but not from one " +
                    "of the supported search engines (SEQUEST, Mascot).");
            }

            // cpp parity: PepXMLreader.cpp:264 — SpectrumMill requires mzXML / mzML only.
            if (_analysisType == Analysis.SpectrumMill)
            {
                _extensions.Clear();
                _extensions.Add(".mzML");
                _extensions.Add(".mzXML");
            }
        }

        // cpp parity: PepXMLreader.cpp:273 — MSFragger-specific source extensions (timsTOF MGF preference).
        if (searchEngineVersion.StartsWith("msfragger", StringComparison.Ordinal))
        {
            _extensions.Insert(0, "_calibrated.mgf");
            _extensions.Insert(0, "_calibrated.mzML");
            _extensions.Insert(0, "_uncalibrated.mgf");
            _extensions.Insert(0, "_uncalibrated.mzML");
            if (_analysisType != Analysis.MsFragger)
                _parentAnalysisType = Analysis.MsFragger;
        }

        SetSpecFileName(_fileroot, _extensions, _dirs);
        _isMgf = GetSpecFileName().EndsWith(".mgf", StringComparison.OrdinalIgnoreCase);

        // cpp parity: PepXMLreader.cpp:289 — MSFragger MGFs use name-based lookup.
        if ((_analysisType == Analysis.MsFragger || _parentAnalysisType == Analysis.MsFragger) && _isMgf)
        {
            LookUpBy = SpecIdType.NameId;
            if (SpecReader != null) SpecReader.IdType = SpecIdType.NameId;
        }

        // cpp parity: PepXMLreader.cpp:294 — average if "average", else monoisotopic.
        _massType = string.Equals(GetAttrValue("fragment_mass_type"), "average", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        AminoAcidMasses.InitializeMass(_aaMass, monoisotopic: _massType == 1);
    }

    // cpp parity: PepXMLreader.cpp:296 — spectrum_query start.
    private void HandleSpectrumQueryStart()
    {
        _scanIndex = -1;
        _scanNumber = -1;
        _charge = 0;
        _precursorMz = 0;
        _pepProb = 0;
        _pepSeq = string.Empty;
        _mods.Clear();
        _ionMobility = 0;

        var minCharge = 1;

        if (_analysisType == Analysis.SpectrumMill)
        {
            _spectrumName = GetRequiredAttrValue("spectrum");
            minCharge = 0;
            // cpp parity: PepXMLreader.cpp:314 — atof: tolerate missing precursor_m_over_z.
            var precStr = GetAttrValue("precursor_m_over_z");
            if (!string.IsNullOrEmpty(precStr) &&
                double.TryParse(precStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var prec))
            {
                _precursorMz = prec;
            }
        }
        else if (_analysisType == Analysis.Morpheus || _analysisType == Analysis.MsFragger ||
                 _parentAnalysisType == Analysis.MsFragger)
        {
            _spectrumName = GetAttrValue("spectrumNativeID");
            _scanNumber = GetIntRequiredAttrValue("start_scan");
            _scanIndex = _scanNumber - 1;

            if (!_isMgf && !string.IsNullOrEmpty(_spectrumName))
            {
                LookUpBy = _spectrumName.Contains('=') ? SpecIdType.NameId : SpecIdType.ScanNumberId;
            }
            else
            {
                _spectrumName = GetRequiredAttrValue("spectrum");
                // cpp parity: PepXMLreader.cpp:331 — HACK strip "0..." padding from MSFragger MGF scan-numbers.
                if (LookUpBy == SpecIdType.NameId &&
                    (_analysisType == Analysis.MsFragger || _parentAnalysisType == Analysis.MsFragger))
                {
                    _spectrumName = _msfraggerPaddingRegex.Replace(_spectrumName, "$1$2$3");
                }
            }
        }
        else if (_analysisType == Analysis.Unknown)
        {
            // cpp parity: PepXMLreader.cpp:341 — should never happen.
            throw new BlibException(false, "The .pep.xml file is not from one of the recognized sources");
        }
        else
        {
            _scanNumber = GetIntRequiredAttrValue("start_scan");
            if (LookUpBy == SpecIdType.NameId)
            {
                _spectrumName = GetRequiredAttrValue("spectrumNativeID");
            }
            else if (Psms.Count == 0 && _analysisType != Analysis.InterProphet)
            {
                // cpp parity: PepXMLreader.cpp:347 — if the file has spectrumNativeIDs, switch to name lookup.
                var nativeId = GetAttrValue("spectrumNativeID");
                if (!string.IsNullOrEmpty(nativeId))
                {
                    _spectrumName = nativeId;
                    LookUpBy = SpecIdType.NameId;
                }
            }
        }

        _charge = GetIntRequiredAttrValue("assumed_charge", minCharge, 20);
        _ionMobility = GetDoubleAttrValueOr("ion_mobility", 0.0);

        // cpp parity: PepXMLreader.cpp:355 — MSFragger + ion_mobility requires *calibrated/uncalibrated MGF/mzML.
        if (_analysisType == Analysis.MsFragger || _parentAnalysisType == Analysis.MsFragger)
        {
            var specName = GetSpecFileName();
            if (_ionMobility > 0)
            {
                var stem = Path.GetFileNameWithoutExtension(specName);
                if (!stem.EndsWith("calibrated", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BlibException(false,
                        "To import an MSFragger search of timsTOF data (with ion_mobility attribute), the " +
                        "corresponding *_uncalibrated or *_calibrated MGF or mzML file is required. The " +
                        "*_uncalibrated file is preferred because the peaks have not been deisotoped.");
                }
            }
        }
    }

    // cpp parity: PepXMLreader.cpp:359 — search_hit start. Only top-ranked hit goes in.
    private void HandleSearchHitStart()
    {
        // cpp parity: PepXMLreader.cpp:361 — hit_rank < 2 (defaulting to 0 if missing) and we're at the root level.
        var rankStr = GetAttrValue("hit_rank");
        var rank = int.TryParse(rankStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : 0;
        if (rank < 2 && _state == ParserState.Root)
        {
            _pepSeq = GetRequiredAttrValue("peptide");
            if (_charge == 0)
            {
                var parentChargeStr = GetAttrValue("parentCharge");
                if (_precursorMz == 0 || !string.IsNullOrEmpty(parentChargeStr))
                {
                    _charge = GetIntRequiredAttrValue("parentCharge", 1, 10);
                }
                else
                {
                    // cpp parity: PepXMLreader.cpp:369 — fall back to neutralMass / precursorMz.
                    var neutralMass = GetDoubleRequiredAttrValue("calc_neutral_pep_mass");
                    _charge = (int)((neutralMass / _precursorMz) + 0.5);
                }
            }
            _state = ParserState.SearchHitBest;
        }
    }

    // cpp parity: PepXMLreader.cpp:375 — modification_info handler (N-term / C-term shifts).
    private void HandleModificationInfo()
    {
        var ntermStr = GetAttrValue("mod_nterm_mass");
        if (!string.IsNullOrEmpty(ntermStr))
        {
            // cpp parity: PepXMLreader.cpp:378-381 — position 1, delta = ntermMass - H.
            var ntermMass = GetDoubleRequiredAttrValue("mod_nterm_mass");
            _mods.Add(new SeqMod(1, ntermMass - _aaMass['h']));
        }
        var ctermStr = GetAttrValue("mod_cterm_mass");
        if (!string.IsNullOrEmpty(ctermStr))
        {
            // cpp parity: PepXMLreader.cpp:383-388 — position = peptide length, delta = ctermMass - OH.
            var ctermMass = GetDoubleRequiredAttrValue("mod_cterm_mass");
            _mods.Add(new SeqMod(_pepSeq.Length, ctermMass - _aaMass['o'] - _aaMass['h']));
        }
    }

    // cpp parity: PepXMLreader.cpp:390 — mod_aminoacid_mass handler.
    private void HandleModAminoacidMass()
    {
        var modPosition = GetIntRequiredAttrValue("position"); // 1-based.
        var modMass = GetDoubleRequiredAttrValue("mass");

        if (_pepSeq.Length < modPosition || modPosition < 1)
        {
            throw new BlibException(false,
                $"Cannot modify sequence {_pepSeq} at position {modPosition - 1} which is beyond its length ({_pepSeq.Length}).");
        }
        var aa = _pepSeq[modPosition - 1];
        var deltaMass = modMass - _aaMass[aa];

        // cpp parity: PepXMLreader.cpp:404 — if this AA+mass pair was registered earlier via
        // aminoacid_modification, prefer the registered massdiff.
        if (_aaModMasses.TryGetValue(aa, out var byMass))
        {
            var nearest = FindNearest(byMass, modMass, 1e-2);
            if (nearest.HasValue)
                deltaMass = nearest.Value;
        }

        _mods.Add(new SeqMod(modPosition, deltaMass));
    }

    // cpp parity: PepXMLreader.cpp:417 — search_score handler. Engine-conditional score capture.
    private void HandleSearchScore()
    {
        var scoreName = GetAttrValue("name").ToLowerInvariant();

        // cpp parity: PepXMLreader.cpp:421 — the giant OR. Names → analysis-engine conditions.
        var capture =
            (_analysisType != Analysis.Crux && scoreName == "expect") ||
            (_analysisType == Analysis.SpectrumMill && scoreName == "smscore") ||
            (_analysisType == Analysis.ProteomeDiscoverer && _scoreType == PsmScoreType.SequestXCorr && scoreName == "q-value") ||
            (_analysisType == Analysis.ProteomeDiscoverer && _scoreType == PsmScoreType.MascotIonsScore && scoreName == "exp-value") ||
            (_analysisType == Analysis.Morpheus && scoreName == "psm q-value") ||
            (_analysisType == Analysis.Msgf && scoreName == "qvalue") ||
            (_analysisType == Analysis.Crux && scoreName == "percolator_qvalue");

        if (capture)
        {
            _pepProb = GetDoubleRequiredAttrValue("value");
        }
        else if (_analysisType == Analysis.Peaks && scoreName == "-10lgp")
        {
            // cpp parity: PepXMLreader.cpp:429 — reverse the -10 log p transform.
            _pepProb = GetDoubleRequiredAttrValue("value");
            _pepProb = Math.Pow(10, _pepProb / -10.0);
        }
    }

    // cpp parity: PepXMLreader.cpp:434 — aminoacid_modification registration.
    private void HandleAminoacidModification()
    {
        var aaStr = GetAttrValue("aminoacid");
        var massDiffStr = GetAttrValue("massdiff");
        var massStr = GetAttrValue("mass");
        if (aaStr.Length != 1) return;

        // cpp parity: PepXMLreader.cpp:439 — atof returns 0 on failure; we use TryParse with default 0.
        _ = double.TryParse(massDiffStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var massDiff);
        _ = double.TryParse(massStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var mass);

        if (massDiff != 0 && mass != 0)
        {
            if (!_aaModMasses.TryGetValue(aaStr[0], out var inner))
            {
                inner = new Dictionary<double, double>();
                _aaModMasses[aaStr[0]] = inner;
            }
            inner[mass] = massDiff;
        }
    }

    // cpp parity: PepXMLreader.cpp:462 — msms_run_summary end (flush PSMs).
    private void HandleRunSummaryEnd()
    {
        if (_analysisType == Analysis.Unknown)
        {
            throw new BlibException(false,
                "The .pep.xml file is not from one of the recognized sources (PeptideProphet, " +
                "iProphet, SpectrumMill, OMSSA, Protein Prospector, X! Tandem, Proteome Discoverer, " +
                "Morpheus, MSGF+, Comet, Crux, MSFragger).");
        }

        // cpp parity: PepXMLreader.cpp:473 — SpectrumMill hack: derive scan from name.
        if (_analysisType == Analysis.SpectrumMill)
        {
            FindScanIndexFromName(_precursorMap);
        }

        // cpp parity: PepXMLreader.cpp:479 — InterProphet byte-based progress.
        // (getCurrentByteIndex not available with XmlReader; we leave this as a no-op stub.)

        // cpp parity: PepXMLreader.cpp:487-501 — when the spectrum file is .mzXML AND the
        // PSM lookup key is scan-number or index, cpp sorts psms_ by that key before
        // BuildTables. The MzXMLParser substitution that follows in cpp is a SpectrumMill-
        // specific path we haven't ported yet (still no-op'd below), but the SORT is needed
        // for any .mzXML input — BiblioSpec's bad-index test exercises exactly this case.
        if (GetSpecFileName().EndsWith(".mzXML", StringComparison.OrdinalIgnoreCase))
        {
            if (LookUpBy == SpecIdType.ScanNumberId)
                Psms.Sort(static (a, b) => (a?.SpecKey ?? int.MaxValue).CompareTo(b?.SpecKey ?? int.MaxValue));
            else if (LookUpBy == SpecIdType.IndexId)
                Psms.Sort(static (a, b) => (a?.SpecIndex ?? int.MaxValue).CompareTo(b?.SpecIndex ?? int.MaxValue));
        }
        // (SpectrumMill's MzXMLParser substitution not yet ported.)

        if (!_isScoreLookup)
        {
            BuildTables(_scoreType, string.Empty, showSpecProgress: true, _workflowType);
        }

        // cpp parity: PepXMLreader.cpp:510 — reset for the next msms_run_summary.
        _massType = 1;
        for (var i = 0; i < _aaMass.Length; i++)
            _aaMass[i] = 0;
    }

    // cpp parity: PepXMLreader.cpp:516 — spectrum_query end (commit the PSM if score passes).
    private void HandleSpectrumQueryEnd()
    {
        var pepSeqLen = _pepSeq.Length;
        if (ScorePasses(_pepProb) && pepSeqLen > 0)
        {
            CurPsm = new PSM
            {
                Charge = _charge,
            };

            // cpp parity: PepXMLreader.cpp:530 — strip non-[A-Z] from pepSeq.
            var sb = new StringBuilder(pepSeqLen);
            for (var i = 0; i < pepSeqLen; i++)
            {
                var c = _pepSeq[i];
                if (c >= 'A' && c <= 'Z')
                    sb.Append(c);
            }
            CurPsm.UnmodSeq = sb.ToString();

            if (_scanIndex >= 0)
                CurPsm.SpecIndex = _scanIndex;
            CurPsm.SpecKey = _scanNumber;
            CurPsm.Score = _pepProb;
            foreach (var mod in _mods)
                CurPsm.Mods.Add(mod);
            CurPsm.SpecName = _spectrumName;
            CurPsm.IonMobility = _ionMobility;
            if (_ionMobility > 0)
            {
                // cpp parity: PepXMLreader.cpp:545 — only MSFragger sets ion_mobility, and only timsTOF.
                CurPsm.IonMobilityType = IonMobilityType.InverseReducedVsecPerCm2;
            }

            Verbosity.Comment(VerbosityLevel.Detail,
                $"Adding psm.  Scan {_scanNumber}, charge {_charge}, score {_pepProb:F2}, seq {_pepSeq}, name {_spectrumName}.");
            Psms.Add(CurPsm);
            if (_analysisType == Analysis.SpectrumMill)
                _precursorMap[CurPsm] = _precursorMz;
            CurPsm = null;
        }

        // cpp parity: PepXMLreader.cpp:561 — reset.
        _charge = 0;
        _precursorMz = 0;
        _pepSeq = string.Empty;
        _scanIndex = -1;
        _scanNumber = -1;
        _pepProb = 0;
        _spectrumName = string.Empty;
        _mods.Clear();
        _state = ParserState.Root;
    }

    // cpp parity: PepXMLreader.cpp:588 — record the score type. If we're in GetScoreTypes mode,
    // stop the parse early since we've answered the question.
    private void SetScoreType(PsmScoreType scoreType)
    {
        _scoreType = scoreType;
        if (_isScoreLookup)
            throw new EndEarlyException();
    }

    // cpp parity: PepXMLreader.cpp:601 — does the score pass the analysis-specific threshold?
    private bool ScorePasses(double score)
    {
        bool pass;
        switch (_analysisType)
        {
            case Analysis.PeptideProphet:
            case Analysis.InterProphet:
                pass = score >= _probCutOff;
                break;
            case Analysis.SpectrumMill:
                return true;
            case Analysis.Omssa:
            case Analysis.ProteinProspector:
            case Analysis.ProteomeDiscoverer:
            case Analysis.XTandem:
            case Analysis.Morpheus:
            case Analysis.Msgf:
            case Analysis.Peaks:
            case Analysis.Crux:
            case Analysis.Comet:
            case Analysis.MsFragger:
                pass = score <= _probCutOff;
                break;
            case Analysis.Unknown:
                return false;
            default:
                throw new InvalidOperationException(
                    $"analysis type {_analysisType} is not handled by PepXMLreader.ScorePasses (bug)");
        }
        if (pass) return true;
        FilteredOutPsmCount++;
        return false;
    }

    // cpp parity: PepXMLreader.cpp:38 — find_nearest helper. Returns the value whose key
    // is closest to the query within tolerance, or null if none.
    private static double? FindNearest(Dictionary<double, double> map, double query, double tolerance)
    {
        // cpp uses std::map (sorted). C# Dictionary is unordered; we just linear-scan, which
        // is fine because per-AA mod tables are tiny (at most a handful of entries).
        var bestKeyDiff = double.MaxValue;
        double? bestValue = null;
        foreach (var (key, value) in map)
        {
            var diff = Math.Abs(query - key);
            if (diff > tolerance) continue;
            if (diff < bestKeyDiff)
            {
                bestKeyDiff = diff;
                bestValue = value;
            }
        }
        return bestValue;
    }
}
