// Port of pwiz_tools/BiblioSpec/src/MaxQuantReader.{h,cpp}
//
// Parses the PSMs from a MaxQuant msms.txt file and (optionally) the corresponding
// mqpar.xml parameter file. Records are grouped by raw-file name and flushed one
// raw-file at a time via BuildParser.BuildTables.
//
// Each msms.txt row carries both PSM metadata AND a peak list (semicolon-separated
// Masses / Intensities) so the reader doubles as the spectrum-file reader when the
// user passes -E (or when mqpar.xml says lcmsRunType=TIMS-DDA) — in that case the
// peaks come from the msms.txt row itself rather than from a separate spectrum file.
//
// The cpp file uses Boost.Tokenizer with an escaped_list_separator; on every test
// input we've seen those rules collapse to a plain tab split (no embedded tabs in
// quoted cells), so the C# port uses a simple Split('\t').

using System.Globalization;
using System.Xml;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// PSM subclass for rows of a MaxQuant msms.txt file. Carries the peak list straight
/// off the row so the reader can also serve as the spectrum-file reader.
/// </summary>
/// <remarks>cpp parity: MaxQuantReader.h:41 <c>struct MaxQuantPSM</c>.</remarks>
internal sealed class MaxQuantPSM : PSM
{
    public double Mz { get; set; }
    public double RetentionTime { get; set; }
    public List<double> Mzs { get; } = new();
    public List<double> Intensities { get; } = new();

    public override void Clear()
    {
        base.Clear();
        Mz = 0;
        RetentionTime = 0;
        Mzs.Clear();
        Intensities.Clear();
    }
}

/// <summary>
/// One row of a MaxQuant msms.txt file (all the columns we care about, ungrouped).
/// </summary>
/// <remarks>cpp parity: MaxQuantReader.h:63 <c>class MaxQuantLine</c>.</remarks>
internal sealed class MaxQuantLine
{
    public string RawFile = string.Empty;
    public int ScanNumber;
    public string Sequence = string.Empty;
    public double Mz;
    public int Charge;
    public string Modifications = string.Empty;
    public string ModifiedSequence = string.Empty;
    public double RetentionTime;
    public double Pep;
    public double Score;
    public int LabelingState = -1;
    public int EvidenceID = -1;
    public string Masses = string.Empty;
    public string Intensities = string.Empty;

    public static void InsertRawFile(MaxQuantLine le, string v) => le.RawFile = v;
    public static void InsertScanNumber(MaxQuantLine le, string v) => le.ScanNumber = ParseIntOrZero(v);
    public static void InsertSequence(MaxQuantLine le, string v) => le.Sequence = v;
    public static void InsertMz(MaxQuantLine le, string v) => le.Mz = ParseDoubleOrZero(v);
    public static void InsertCharge(MaxQuantLine le, string v) => le.Charge = ParseIntOrZero(v);
    public static void InsertModifications(MaxQuantLine le, string v) => le.Modifications = v;
    public static void InsertModifiedSequence(MaxQuantLine le, string v) => le.ModifiedSequence = v;
    public static void InsertRetentionTime(MaxQuantLine le, string v) => le.RetentionTime = ParseDoubleOrZero(v);
    public static void InsertPep(MaxQuantLine le, string v) => le.Pep = ParseDoubleOrZero(v);
    public static void InsertScore(MaxQuantLine le, string v) => le.Score = ParseDoubleOrZero(v);
    public static void InsertLabelingState(MaxQuantLine le, string v) => le.LabelingState = string.IsNullOrEmpty(v) ? -1 : ParseIntOrZero(v);
    public static void InsertEvidenceID(MaxQuantLine le, string v) => le.EvidenceID = string.IsNullOrEmpty(v) ? -1 : ParseIntOrZero(v);
    public static void InsertMasses(MaxQuantLine le, string v) => le.Masses = v;
    public static void InsertIntensities(MaxQuantLine le, string v) => le.Intensities = v;

    private static int ParseIntOrZero(string v) =>
        string.IsNullOrEmpty(v) ? 0 : int.Parse(v, NumberStyles.Integer, CultureInfo.InvariantCulture);
    private static double ParseDoubleOrZero(string v) =>
        string.IsNullOrEmpty(v) ? 0 : double.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture);
}

/// <summary>
/// One target column: its case-insensitive name, its 0-based file position
/// (-1 = "not yet located"), and the setter that copies a tokenised cell value
/// into a <see cref="MaxQuantLine"/>.
/// </summary>
/// <remarks>cpp parity: MaxQuantReader.h:149 <c>class MaxQuantColumnTranslator</c>.</remarks>
internal sealed class MaxQuantColumnTranslator
{
    public string Name { get; }
    public int Position { get; set; }
    public Action<MaxQuantLine, string> Inserter { get; }

    public MaxQuantColumnTranslator(string name, int position, Action<MaxQuantLine, string> inserter)
    {
        Name = name;
        Position = position;
        Inserter = inserter;
    }
}

/// <summary>
/// Thrown when a modification abbreviation in a modified-sequence string cannot be matched
/// against the modifications list — usually a sign of a MaxQuant bug where the "second-best"
/// peptide was reported instead of the best.
/// </summary>
/// <remarks>cpp parity: MaxQuantReader.h:232 <c>MaxQuantWrongSequenceException</c>.</remarks>
internal sealed class MaxQuantWrongSequenceException : Exception
{
    public MaxQuantWrongSequenceException(string mod, string seq, int line)
        : base($"No matching mod for {mod} in sequence {seq} (line {line.ToString(CultureInfo.InvariantCulture)}). "
               + "Make sure you have provided the correct modifications[.local].xml file.")
    { }
}

/// <summary>
/// Parses MaxQuant <c>msms.txt</c> files. Reads PSMs from the TSV, groups them by raw-file
/// name, then flushes each group via <see cref="BuildParser.BuildTables(PsmScoreType, string, bool, WorkflowType)"/>.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::MaxQuantReader</c> at
/// <c>pwiz_tools/BiblioSpec/src/MaxQuantReader.{h,cpp}</c>.</para>
/// <para>Like the cpp version, this class also implements <see cref="ISpecFileReader"/> so
/// the embedded-spectra mode (cpp <c>preferEmbeddedSpectra_</c> = true) can pull peaks
/// straight off the parsed PSM. External-spectra mode delegates to the default pwiz reader
/// the same way other BuildParser subclasses do.</para>
/// </remarks>
public sealed class MaxQuantReader : BuildParser
{
    // Cached separator array for CA1861 (prefer static readonly over constant arrays).
    private static readonly char[] _modParenChars = { '(', ')' };

    private readonly string _tsvName;
    private readonly string _modsPath;
    private readonly string _paramsPath;
    private readonly double _scoreThreshold;
    private int _lineNum = 1;
    private int _lineCount;

    // cpp parity: map filename -> list of PSMs. SortedDictionary so iteration is stable
    // across runs (cpp uses std::map, also ascending key order).
    private readonly SortedDictionary<string, List<MaxQuantPSM>> _fileMap =
        new(StringComparer.Ordinal);

    private MaxQuantPSM? _curMaxQuantPSM;
    private readonly List<MaxQuantColumnTranslator> _targetColumns = new();
    private readonly HashSet<string> _optionalColumns = new(StringComparer.Ordinal);

    private readonly Dictionary<string, MaxQuantModification> _modBank = new(StringComparer.Ordinal);
    private readonly Dictionary<MaxQuantModification.MaxQuantModPosition, List<MaxQuantModification>> _fixedModBank = new();
    private readonly List<MaxQuantLabels> _labelBank = new();

    // optional ion-mobility table parsed from evidence.txt
    private readonly List<double> _inverseK0 = new();
    private readonly List<double> _ccs = new();

    /// <summary>Returns true if <paramref name="path"/> ends with <c>msms.txt</c> (case-insensitive).</summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith("msms.txt", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Construct a MaxQuantReader bound to <paramref name="maker"/> and the msms.txt file at
    /// <paramref name="tsvName"/>.
    /// </summary>
    /// <remarks>cpp parity: MaxQuantReader.cpp:41.</remarks>
    public MaxQuantReader(BlibBuilder maker, string tsvName, ProgressIndicator? parentProgress)
        : base(maker, tsvName, parentProgress)
    {
        Verbosity.Debug("Creating MaxQuantReader.");

        _tsvName = tsvName;
        _scoreThreshold = GetScoreThreshold(BuildInput.MaxQuant);

        // cpp parity: MaxQuantReader.cpp:51 — defaults to FALSE (BuildParser default was true).
        PreferEmbeddedSpectra = maker.PreferEmbeddedSpectra ?? false;

        // cpp parity: optional ion-mobility table from evidence.txt next to msms.txt.
        InitEvidence();

        _modsPath = maker.MaxQuantModsPath;
        _paramsPath = maker.MaxQuantParamsPath;

        InitTargetColumns();

        // Initialise modifications (mods table + fixed mods from mqpar.xml). This also
        // toggles PreferEmbeddedSpectra if mqpar.xml declares lcmsRunType=TIMS-DDA, and
        // chooses INDEX_ID vs SCAN_NUM_ID lookup for WIFF inputs.
        InitModifications();
    }

    /// <summary>cpp parity: MaxQuantReader.cpp:508 — returns just MAXQUANT_SCORE.</summary>
    public override IList<PsmScoreType> GetScoreTypes() =>
        new[] { PsmScoreType.MaxQuantScore };

    /// <summary>
    /// cpp parity: MaxQuantReader.cpp:428 <c>parseFile()</c>. Opens the TSV, reads the
    /// header, collects every PSM, then flushes one raw-file group at a time.
    /// </summary>
    public override bool ParseFile()
    {
        Verbosity.Debug("Parsing file.");

        // Read the header + the rest of the file.
        using var stream = new FileStream(_tsvName, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);

        var headerLine = reader.ReadLine()
            ?? throw new BlibException(true, $"Could not read header from tsv file '{_tsvName}'.");
        ParseHeader(headerLine);

        // First pass: count lines and collect the set of raw files referenced. cpp parity:
        // MaxQuantReader.cpp:581. We materialise the rest of the file once so the second
        // pass doesn't need a seek-back-to-start.
        var dataLines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) != null)
            dataLines.Add(line);
        _lineCount = dataLines.Count + 1;

        CollectFilenames(dataLines);

        // cpp parity: MaxQuantReader.cpp:442 — look in parent / grandparent / etc + a fixed
        // extension priority list when resolving raw-file names.
        var dirs = new List<string> { "../", "../../", "../../../", "../../../../" };
        var extensions = new List<string>
        {
            ".mz5",
            ".mzML",
            // cpp wraps these in #ifdef VENDOR_READERS; we always try them in the C# port
            // because the pwiz-CLI-backed reader claims them at runtime.
            ".raw",   // Waters/Thermo
            ".wiff",  // Sciex
            ".d",     // Bruker/Agilent
            ".lcd",   // Shimadzu
            ".mzXML",
            ".cms2",
            ".ms2",
            ".mgf",
        };

        // cpp parity: MaxQuantReader.cpp:464 — pre-flight check that every raw-file
        // referenced by the msms.txt can be found, BEFORE the (much slower) PSM parse.
        // When -E is set / TIMS-DDA / mqpar says so, embedded spectra are used and this
        // check is skipped.
        if (!PreferEmbeddedSpectra)
        {
            var missingFiles = new List<string>();
            foreach (var kv in _fileMap)
            {
                try
                {
                    SetSpecFileName(kv.Key, extensions, dirs);
                }
                catch (BlibException ex)
                {
                    if (!ex.Message.Contains("searching for spectrum file", StringComparison.Ordinal))
                        throw;
                    missingFiles.Add(kv.Key);
                }
            }
            if (missingFiles.Count > 0)
            {
                throw new BlibException(false,
                    FilesNotFoundMessage(missingFiles, extensions, dirs)
                    + "\n\nRun with the -E flag to allow MaxQuant to use deisotoped/deconvoluted embedded spectra");
            }
        }

        Verbosity.Debug("Collecting PSMs.");
        CollectPsms(dataLines);

        Verbosity.Debug("Building tables.");
        InitSpecFileProgress(_fileMap.Count);
        foreach (var kv in _fileMap)
        {
            Psms.Clear();
            foreach (var p in kv.Value)
                Psms.Add(p);

            string specFileName = kv.Key;
            if (PreferEmbeddedSpectra)
            {
                SetSpecFileName(kv.Key, checkFile: false);
            }
            else
            {
                SetSpecFileName(kv.Key, extensions, dirs);
                specFileName = Path.GetFileName(GetSpecFileName());
            }

            BuildTables(PsmScoreType.MaxQuantScore, specFileName, showSpecProgress: false);
        }

        return true;
    }

    // --- column setup -----------------------------------------------------------------

    private void InitTargetColumns()
    {
        _targetColumns.Add(new MaxQuantColumnTranslator("Raw File", -1, MaxQuantLine.InsertRawFile));
        _targetColumns.Add(new MaxQuantColumnTranslator("Scan Number", -1, MaxQuantLine.InsertScanNumber));
        _targetColumns.Add(new MaxQuantColumnTranslator("Sequence", -1, MaxQuantLine.InsertSequence));
        _targetColumns.Add(new MaxQuantColumnTranslator("m/z", -1, MaxQuantLine.InsertMz));
        _targetColumns.Add(new MaxQuantColumnTranslator("Charge", -1, MaxQuantLine.InsertCharge));
        _targetColumns.Add(new MaxQuantColumnTranslator("Modifications", -1, MaxQuantLine.InsertModifications));
        _targetColumns.Add(new MaxQuantColumnTranslator("Modified Sequence", -1, MaxQuantLine.InsertModifiedSequence));
        _targetColumns.Add(new MaxQuantColumnTranslator("Retention Time", -1, MaxQuantLine.InsertRetentionTime));
        _targetColumns.Add(new MaxQuantColumnTranslator("PEP", -1, MaxQuantLine.InsertPep));
        _targetColumns.Add(new MaxQuantColumnTranslator("Score", -1, MaxQuantLine.InsertScore));
        _targetColumns.Add(new MaxQuantColumnTranslator("Masses", -1, MaxQuantLine.InsertMasses));
        _targetColumns.Add(new MaxQuantColumnTranslator("Intensities", -1, MaxQuantLine.InsertIntensities));
        _targetColumns.Add(new MaxQuantColumnTranslator("Labeling State", -1, MaxQuantLine.InsertLabelingState));
        _targetColumns.Add(new MaxQuantColumnTranslator("Evidence ID", -1, MaxQuantLine.InsertEvidenceID));

        _optionalColumns.Add("Labeling State");
        _optionalColumns.Add("Evidence ID");
    }

    // --- modifications + mqpar handling ----------------------------------------------

    private void InitModifications()
    {
        var parentPath = BlibUtils.GetPath(_tsvName);
        if (parentPath.Length == 0) parentPath = ".";

        var modFile = _modsPath ?? string.Empty;
        var localModFile = string.Empty;
        if (modFile.Contains(".local.xml", StringComparison.Ordinal))
        {
            localModFile = modFile;
            modFile = string.Empty;
        }

        // 1) main modifications file
        if (string.IsNullOrEmpty(modFile) || !File.Exists(modFile))
        {
            modFile = CheckForModificationsFile(parentPath, "modifications.xml");
            if (string.IsNullOrEmpty(modFile))
                modFile = CheckForModificationsFile(parentPath, "modification.xml");
            if (string.IsNullOrEmpty(modFile))
            {
                Verbosity.Comment(VerbosityLevel.Detail, "Loading default modifications");
                modFile = Path.Combine(BlibUtils.GetExeDirectory(), "modifications.xml");
            }
        }

        if (!ParseModificationsFile(modFile, _modBank))
        {
            _modBank.Clear();
            return;
        }

        // 2) optional modifications.local.xml
        if (string.IsNullOrEmpty(localModFile))
            localModFile = CheckForModificationsFile(parentPath, "modifications.local.xml");
        if (!string.IsNullOrEmpty(localModFile))
        {
            if (!ParseModificationsFile(localModFile, _modBank))
            {
                _modBank.Clear();
                return;
            }
        }

        InitFixedModifications();
    }

    private static string CheckForModificationsFile(string parentPath, string filename)
    {
        var modFile = Path.Combine(parentPath, filename);
        Verbosity.Comment(VerbosityLevel.Detail, $"Checking for modification file {modFile}");
        if (!File.Exists(modFile)) return string.Empty;
        return modFile;
    }

    private static bool ParseModificationsFile(string modFile, Dictionary<string, MaxQuantModification> modBank)
    {
        Verbosity.Comment(VerbosityLevel.Detail, $"Parsing modification file {modFile}");
        var modReader = new MaxQuantModReader(modFile, modBank);
        int initialSize = modBank.Count;
        try
        {
            modReader.Parse();
            Verbosity.Comment(VerbosityLevel.Detail,
                string.Format(CultureInfo.InvariantCulture,
                    "Done parsing {0}, {1} modifications found", modFile, modBank.Count - initialSize));
            return true;
        }
        catch (BlibException e)
        {
            Verbosity.Error($"Error parsing modifications file: {e.Message}");
            return false;
        }
        catch (Exception e)
        {
            Verbosity.Error($"Unknown error while parsing modifications file: {e.Message}");
            return false;
        }
    }

    private void InitFixedModifications()
    {
        var tsvDir = BlibUtils.GetPath(_tsvName);
        if (tsvDir.Length == 0) tsvDir = ".";

        var mqparFile = _paramsPath ?? string.Empty;
        if (string.IsNullOrEmpty(mqparFile))
        {
            // cpp parity: MaxQuantReader.cpp:215 — check same folder, then two folders up,
            // then one folder up. (cpp's comments and order disagree; we mirror the cpp
            // code order exactly.)
            var tryPath = Path.Combine(tsvDir, "mqpar.xml");
            Verbosity.Comment(VerbosityLevel.Detail,
                "Checking for mqpar file two folders up from msms.txt file.");
            if (!File.Exists(tryPath))
            {
                tryPath = Path.Combine(tsvDir, "..", "..", "mqpar.xml");
                Verbosity.Comment(VerbosityLevel.Detail,
                    "Checking for mqpar file in same folder as msms.txt file.");
                if (!File.Exists(tryPath))
                {
                    tryPath = Path.Combine(tsvDir, "..", "mqpar.xml");
                    Verbosity.Comment(VerbosityLevel.Detail,
                        "Checking for mqpar file in parent folder of msms.txt file.");
                    if (!File.Exists(tryPath))
                    {
                        string canonical;
                        try { canonical = Path.GetFullPath(tsvDir); }
                        catch { canonical = tsvDir; }
                        Verbosity.Error(string.Format(CultureInfo.InvariantCulture,
                            "mqpar.xml file not found. Please move it to the directory {0} with the msms.txt file.",
                            canonical));
                    }
                }
            }
            mqparFile = tryPath;
        }
        else if (!File.Exists(mqparFile))
        {
            Verbosity.Error($"specfied MaxQuant params file not found ({mqparFile})");
        }

        Verbosity.Comment(VerbosityLevel.Detail, $"Parsing mqpar file {mqparFile}");
        var fixedMods = new HashSet<string>(StringComparer.Ordinal);
        var modReader = new MaxQuantModReader(mqparFile, fixedMods, _labelBank);
        try
        {
            modReader.Parse();
            Verbosity.Comment(VerbosityLevel.Detail,
                string.Format(CultureInfo.InvariantCulture,
                    "Done parsing {0}, {1} fixed modifications found", mqparFile, fixedMods.Count));
        }
        catch (BlibException e)
        {
            Verbosity.Error($"Error parsing mqpar file: {e.Message}");
            return;
        }
        catch (Exception e)
        {
            Verbosity.Error($"Unknown error while parsing mqpar file: {e.Message}");
            return;
        }

        // cpp parity: MaxQuantReader.cpp:261 — read mqpar contents to decide TIMS-DDA / WIFF.
        string mqparXml = File.ReadAllText(mqparFile);

        if (mqparXml.Contains("<lcmsRunType>TIMS-DDA</lcmsRunType>", StringComparison.Ordinal))
            PreferEmbeddedSpectra = true;

        if (PreferEmbeddedSpectra)
        {
            SetSpecFileName(_tsvName, checkFile: false);

            // Point the spec reader at ourselves so peaks come from the PSM rows.
            SpecReader = new MaxQuantEmbeddedSpecFileReader();
        }
        else
        {
            // HACK: if mqpar analyzed WIFF file, use index lookup, else use scan number.
            if (mqparXml.Contains(".wiff</string>", StringComparison.Ordinal)
                || mqparXml.Contains(".wiff2</string>", StringComparison.Ordinal))
                LookUpBy = SpecIdType.IndexId;
            else
                LookUpBy = SpecIdType.ScanNumberId;
        }

        // initialise the supported position buckets
        _fixedModBank[MaxQuantModification.MaxQuantModPosition.Anywhere] = new();
        _fixedModBank[MaxQuantModification.MaxQuantModPosition.AnyNTerm] = new();
        _fixedModBank[MaxQuantModification.MaxQuantModPosition.AnyCTerm] = new();
        _fixedModBank[MaxQuantModification.MaxQuantModPosition.NotNTerm] = new();
        _fixedModBank[MaxQuantModification.MaxQuantModPosition.NotCTerm] = new();

        // add all fixed mods to _fixedModBank
        foreach (var fmName in fixedMods)
        {
            var lookup = MaxQuantModification.Find(_modBank, fmName);
            if (lookup is null)
            {
                Verbosity.Error(string.Format(CultureInfo.InvariantCulture,
                    "Unknown modification {0} in mqpar file. Add a modifications.xml file to "
                    + "the same directory as msms.txt which contains this modification.",
                    fmName));
                return;
            }
            Verbosity.Debug(string.Format(CultureInfo.InvariantCulture,
                "Adding fixed mod '{0}' (position {1}).", fmName, (int)lookup.Position));

            if (!_fixedModBank.TryGetValue(lookup.Position, out var bucket))
            {
                bucket = new List<MaxQuantModification>();
                _fixedModBank[lookup.Position] = bucket;
            }
            bucket.Add(lookup);
        }

        // add all labels to _labelBank
        foreach (var labels in _labelBank)
        {
            foreach (var state in labels.LabelingStates)
            {
                var mods = new List<MaxQuantModification>();
                foreach (var labelName in state.ModsStrings)
                {
                    if (string.IsNullOrEmpty(labelName)) continue;
                    var lookup = MaxQuantModification.Find(_modBank, labelName);
                    if (lookup is null)
                    {
                        Verbosity.Error(string.Format(CultureInfo.InvariantCulture,
                            "Unknown label {0} in mqpar file. Add a modifications.xml file to "
                            + "the same directory as msms.txt which contains this modification.",
                            labelName));
                        return;
                    }
                    mods.Add(lookup);
                }
                MaxQuantLabels.AddMods(state, mods);
            }
        }
    }

    // --- evidence.txt parsing --------------------------------------------------------

    private void InitEvidence()
    {
        // cpp parity: MaxQuantReader.cpp:359 — strip "msms.txt" then append "evidence.txt".
        // The actual cpp strips length-8, which matches "msms.txt".
        if (_tsvName.Length < 8) return;
        var tryPath = string.Concat(_tsvName.AsSpan(0, _tsvName.Length - 8), "evidence.txt");
        Verbosity.Comment(VerbosityLevel.Detail,
            "Checking for ion mobility information in evidence.txt file in same folder as msms.txt file.");
        if (!File.Exists(tryPath))
        {
            Verbosity.Comment(VerbosityLevel.Detail,
                "Did not find evidence.txt file in same folder as msms.txt file. No ion mobility values.");
            return;
        }

        var evidenceFile = tryPath;
        Verbosity.Comment(VerbosityLevel.Detail, $"Parsing evidence file {evidenceFile}");

        try
        {
            using var evidence = new StreamReader(evidenceFile);
            var line = evidence.ReadLine();
            if (line is null) return;

            var columns = line.Split('\t');
            int col = 0, colInvK0 = -1, colCCS = -1;
            foreach (var c in columns)
            {
                if (c == "K0" || c == "1/K0") colInvK0 = col;
                else if (c == "CCS") colCCS = col;
                col++;
            }
            if (colInvK0 < 0 && colCCS < 0)
            {
                Verbosity.Comment(VerbosityLevel.Detail,
                    "Did not find any ion mobility data in evidence.txt file.");
                return;
            }

            while ((line = evidence.ReadLine()) != null)
            {
                if (line.Length == 0) break;
                var cols = line.Split('\t');
                if (colInvK0 >= 0 && colInvK0 < cols.Length)
                {
                    if (double.TryParse(cols[colInvK0], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        _inverseK0.Add(v);
                    else
                        _inverseK0.Add(0);
                }
                // cpp parity: MaxQuantReader.cpp:407 — CCS column intentionally NOT loaded
                // because some MaxQuant versions emit incorrect CCS values.
            }
            Verbosity.Comment(VerbosityLevel.Detail, $"Done parsing {evidenceFile}");
        }
        catch (Exception e)
        {
            Verbosity.Error($"Error parsing evidence.txt file: {e.Message}");
        }
    }

    // --- header / first-pass scan ----------------------------------------------------

    private void ParseHeader(string line)
    {
        // cpp parity: MaxQuantReader.cpp:533 — strip UTF-8 BOM if present.
        // StreamReader normally swallows the BOM but in case it didn't, both forms —
        // the BOM code point and the literal 3-byte sequence — are handled.
        if (line.Length > 0 && line[0] == '﻿')
            line = line.Substring(1);
        if (line.Length >= 3 && line[0] == (char)0xEF && line[1] == (char)0xBB && line[2] == (char)0xBF)
            line = line.Substring(3);

        var tokens = SplitTabs(line);
        for (int colNumber = 0; colNumber < tokens.Length; colNumber++)
        {
            var token = tokens[colNumber];
            foreach (var tc in _targetColumns)
            {
                if (string.Equals(token, tc.Name, StringComparison.OrdinalIgnoreCase))
                {
                    tc.Position = colNumber;
                    break;
                }
            }
        }

        // verify all required columns were found; drop optionals that weren't
        for (var i = _targetColumns.Count - 1; i >= 0; i--)
        {
            if (_targetColumns[i].Position < 0)
            {
                if (_optionalColumns.Contains(_targetColumns[i].Name))
                {
                    _optionalColumns.Remove(_targetColumns[i].Name);
                    _targetColumns.RemoveAt(i);
                }
                else
                {
                    throw new BlibException(false,
                        $"Did not find required column '{_targetColumns[i].Name}'.");
                }
            }
        }

        _targetColumns.Sort((a, b) => a.Position.CompareTo(b.Position));
    }

    private void CollectFilenames(IReadOnlyList<string> dataLines)
    {
        // cpp parity: MaxQuantReader.cpp:581. Walk every row, pull just the Raw File column,
        // bucket into _fileMap keys.
        int rawFilePos = _targetColumns[0].Position;
        string lastFilename = string.Empty;

        for (int i = 0; i < dataLines.Count; i++)
        {
            var line = dataLines[i];
            var cells = SplitTabs(line);
            if (rawFilePos >= cells.Length)
                throw new BlibException(false,
                    $"unable to find raw file column in getFilenamesAndLineCount for line:\n{line}");
            var filename = cells[rawFilePos];
            if (lastFilename.Length == 0 || lastFilename != filename)
            {
                lastFilename = filename;
                if (!_fileMap.ContainsKey(filename))
                    _fileMap[filename] = new List<MaxQuantPSM>();
            }
        }
    }

    private void CollectPsms(IReadOnlyList<string> dataLines)
    {
        var progress = new ProgressIndicator(_lineCount);

        foreach (var line in dataLines)
        {
            ++_lineNum;
            int colListIdx = 0;
            int lineColNumber = 0;
            var entry = new MaxQuantLine();

            try
            {
                var tokens = SplitTabs(line);
                foreach (var token in tokens)
                {
                    if (colListIdx < _targetColumns.Count
                        && lineColNumber == _targetColumns[colListIdx].Position)
                    {
                        _targetColumns[colListIdx].Inserter(entry, token);
                        colListIdx++;
                        if (colListIdx == _targetColumns.Count) break;
                    }
                    lineColNumber++;
                }
                if (colListIdx != _targetColumns.Count)
                {
                    Verbosity.Warn(string.Format(CultureInfo.InvariantCulture,
                        "Skipping invalid line {0}", _lineNum));
                    continue;
                }
            }
            catch (BlibException e)
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "{0} caught at line {1}, column {2}",
                    e.Message, _lineNum, lineColNumber + 1));
            }
            catch (Exception e)
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "{0} caught at line {1}, column {2}",
                    e.Message, _lineNum, lineColNumber + 1));
            }

            StoreLine(entry);
            progress.Increment();
        }
    }

    private void StoreLine(MaxQuantLine entry)
    {
        if (entry.Pep > _scoreThreshold)
        {
            Verbosity.Comment(VerbosityLevel.Detail, string.Format(CultureInfo.InvariantCulture,
                "Not saving PSM {0} with PEP {1} (line {2})",
                entry.ScanNumber, entry.Pep, _lineNum));
            FilteredOutPsmCount++;
            return;
        }
        if (entry.Masses.Length == 0 || entry.Intensities.Length == 0)
        {
            Verbosity.Warn(string.Format(CultureInfo.InvariantCulture,
                "Not saving PSM {0} with no spectrum (line {1})",
                entry.ScanNumber, _lineNum));
            return;
        }

        _curMaxQuantPSM = new MaxQuantPSM
        {
            SpecKey = entry.ScanNumber,
            // cpp parity: for WIFF, "scan number" is actually a 0-based index when all spectra
            // are enumerated in cycle-major order.
            SpecIndex = entry.ScanNumber,
            UnmodSeq = entry.Sequence,
            Mz = entry.Mz,
            Charge = entry.Charge,
        };

        if (entry.EvidenceID >= 0)
        {
            if (_inverseK0.Count > entry.EvidenceID)
            {
                _curMaxQuantPSM.IonMobility = _inverseK0[entry.EvidenceID];
                if (_curMaxQuantPSM.IonMobility != 0)
                    _curMaxQuantPSM.IonMobilityType = IonMobilityType.InverseReducedVsecPerCm2;
            }
            if (_ccs.Count > entry.EvidenceID)
                _curMaxQuantPSM.Ccs = _ccs[entry.EvidenceID];
        }

        try
        {
            AddModsToVector(_curMaxQuantPSM.Mods, entry.Modifications, entry.ModifiedSequence, entry.Sequence);
        }
        catch (MaxQuantWrongSequenceException e)
        {
            Verbosity.Error(e.Message);
            return;
        }

        AddLabelModsToVector(_curMaxQuantPSM.Mods, entry.RawFile, entry.Sequence, entry.LabelingState);
        _curMaxQuantPSM.RetentionTime = entry.RetentionTime;
        _curMaxQuantPSM.Score = entry.Pep;
        AddDoublesToVector(_curMaxQuantPSM.Mzs, entry.Masses);
        AddDoublesToVector(_curMaxQuantPSM.Intensities, entry.Intensities);

        if (!_fileMap.TryGetValue(entry.RawFile, out var list))
        {
            list = new List<MaxQuantPSM>();
            _fileMap[entry.RawFile] = list;
        }
        list.Add(_curMaxQuantPSM);
    }

    private static void AddDoublesToVector(List<double> v, string valueList)
    {
        var parts = valueList.Split(';');
        try
        {
            foreach (var p in parts)
                v.Add(double.Parse(p, NumberStyles.Float, CultureInfo.InvariantCulture));
        }
        catch (FormatException)
        {
            Verbosity.Error($"Could not cast \"{valueList}\" to doubles");
        }
    }

    // --- modification application ---------------------------------------------------

    private static void AddFixedMods(List<SeqMod> v, string sequence,
        IReadOnlyDictionary<MaxQuantModification.MaxQuantModPosition, List<MaxQuantModification>> modsByPosition)
    {
        var modsAnywhere = modsByPosition[MaxQuantModification.MaxQuantModPosition.Anywhere];
        var modsAnyNTerm = modsByPosition[MaxQuantModification.MaxQuantModPosition.AnyNTerm];
        var modsAnyCTerm = modsByPosition[MaxQuantModification.MaxQuantModPosition.AnyCTerm];
        var modsNotNTerm = modsByPosition[MaxQuantModification.MaxQuantModPosition.NotNTerm];
        var modsNotCTerm = modsByPosition[MaxQuantModification.MaxQuantModPosition.NotCTerm];

        // cpp parity: MaxQuantReader.cpp:819 — site-less anyNTerm mods go at the very
        // front (position 1, pre-pended).
        foreach (var mod in modsAnyNTerm)
        {
            if (mod.Sites.Count == 0)
                v.Insert(0, new SeqMod(1, mod.MassDelta));
        }

        for (int i = 0; i < sequence.Length; i++)
        {
            char aa = sequence[i];
            int pos = i + 1;
            v.AddRange(GetFixedMods(aa, pos, modsAnywhere));
            if (i == 0)
                v.AddRange(GetFixedMods(aa, pos, modsAnyNTerm));
            else if (i + 1 == sequence.Length)
                v.AddRange(GetFixedMods(aa, pos, modsAnyCTerm));
            if (i > 0)
                v.AddRange(GetFixedMods(aa, pos, modsNotNTerm));
            if (i + 1 < sequence.Length)
                v.AddRange(GetFixedMods(aa, pos, modsNotCTerm));
        }

        // cpp parity: MaxQuantReader.cpp:836 — site-less anyCTerm mods go at the very
        // end (position = sequence.Length, appended).
        foreach (var mod in modsAnyCTerm)
        {
            if (mod.Sites.Count == 0)
                v.Add(new SeqMod(sequence.Length, mod.MassDelta));
        }
    }

    private void AddModsToVector(List<SeqMod> v, string modifications, string modSequence, string sequence)
    {
        // cpp parity: MaxQuantReader.cpp:846 — pre-translate the abbreviated phospho marks.
        modSequence = modSequence.Replace("pS", "S(ph)", StringComparison.Ordinal);
        modSequence = modSequence.Replace("pT", "T(ph)", StringComparison.Ordinal);
        modSequence = modSequence.Replace("pY", "Y(ph)", StringComparison.Ordinal);

        var modNames = new List<string>();
        if (!string.Equals(modifications, "Unmodified", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var m in modifications.Split(','))
                modNames.Add(m);
        }

        // strip leading / trailing underscores
        if (modSequence.Length > 0 && modSequence[0] == '_')
            modSequence = modSequence.Substring(1);
        int sequenceLength = modSequence.Length;
        if (sequenceLength > 0 && modSequence[sequenceLength - 1] == '_')
        {
            modSequence = modSequence.Substring(0, sequenceLength - 1);
            --sequenceLength;
        }

        // cpp parity: MaxQuantReader.cpp:870 — handle "...X(mod)_" form by also stripping
        // the underscore immediately before the final '('.
        if (sequenceLength > 0 && modSequence[sequenceLength - 1] == ')')
        {
            var openPos = modSequence.LastIndexOf('(');
            if (openPos > 0 && modSequence[openPos - 1] == '_')
            {
                modSequence = modSequence.Remove(openPos - 1, 1);
                --sequenceLength;
            }
        }

        var modsAnyCTerm = _fixedModBank[MaxQuantModification.MaxQuantModPosition.AnyCTerm];

        int modsFound = 0;
        for (int i = 0; i < sequenceLength; i++)
        {
            char ch = modSequence[i];
            if (ch == '(')
            {
                ++modsFound;
                int posOpenParen = i;
                int newI = i;
                var seqMod = SearchForMod(modNames, modSequence, ref newI, posOpenParen);
                i = newI;

                // skip the mod if it's actually a fixed anyCTerm mass
                bool isFixedCTerm = false;
                foreach (var mod in modsAnyCTerm)
                {
                    if (mod.MassDelta == seqMod.DeltaMass)
                    {
                        isFixedCTerm = true;
                        break;
                    }
                }
                if (!isFixedCTerm)
                    v.Add(seqMod);
            }
            else if (ch == ')')
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "Unexpected closing parentheses found in sequence {0} (line {1})",
                    modSequence, _lineNum));
            }
            else if (ch < 'A' || ch > 'Z')
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "Illegal character {0} found in sequence {1} (line {2})",
                    ch, modSequence, _lineNum));
            }
        }

        AddFixedMods(v, sequence, _fixedModBank);

        if (modsFound < modNames.Count)
        {
            Verbosity.Warn(string.Format(CultureInfo.InvariantCulture,
                "Found {0} modifications but expected at least {1} in sequence {2} ({3})",
                modsFound, modNames.Count, modSequence, _lineNum));
        }
    }

    private void AddLabelModsToVector(List<SeqMod> v, string rawFile, string sequence, int labelingState)
    {
        var labels = MaxQuantLabels.FindLabels(_labelBank, rawFile);
        if (labelingState < 0)
        {
            if (labels is null || labels.LabelingStates.Count != 1
                || !_optionalColumns.Contains("Labeling State"))
            {
                return;
            }
            labelingState = 0;
        }

        if (labels is null)
        {
            throw new BlibException(false,
                $"Required raw file '{rawFile}' was not found in mqpar file.");
        }
        if (labels.LabelingStates.Count <= labelingState)
        {
            throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                "Labeling state was {0} but mqpar file only had {1} labeling states for raw file '{2}'.",
                labelingState, labels.LabelingStates.Count, rawFile));
        }

        AddFixedMods(v, sequence, labels.LabelingStates[labelingState].ModsByPosition);
    }

    private SeqMod SearchForMod(List<string> modNames, string modSequence, ref int posOpenParen, int posFirstParen)
    {
        // cpp parity: MaxQuantReader.cpp:963 — walk to matching close paren, may be nested.
        int nestDepth = 1;
        int posNextParen = posOpenParen;
        while (nestDepth > 0)
        {
            posNextParen = modSequence.IndexOfAny(_modParenChars, posNextParen + 1);
            if (posNextParen < 0)
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "Closing parentheses expected but not found in sequence {0} (line {1})",
                    modSequence, _lineNum));
            }
            nestDepth += modSequence[posNextParen] == ')' ? -1 : 1;
        }

        int modStart = posOpenParen + 1;
        string modAbbreviation = modSequence.Substring(modStart, posNextParen - modStart);
        posOpenParen = posNextParen; // advance to the closing parenthesis

        // first pass — direct prefix match
        foreach (var modName in modNames)
        {
            if (modAbbreviation.Length <= modName.Length
                && string.Equals(modAbbreviation,
                                 modName.Substring(0, modAbbreviation.Length),
                                 StringComparison.OrdinalIgnoreCase))
            {
                var lookup = MaxQuantModification.Find(_modBank, modName);
                if (lookup != null)
                    return new SeqMod(GetModPosition(modSequence, posFirstParen), lookup.MassDelta);
            }
        }

        // second pass — strip leading digits + space (the "2 Oxidation (M)" case)
        foreach (var modName in modNames)
        {
            int newStart = 0;
            while (newStart < modName.Length && char.IsDigit(modName[newStart]))
                ++newStart;
            if (newStart + 1 < modName.Length && modName[newStart] == ' ')
            {
                ++newStart;
                if (newStart + modAbbreviation.Length <= modName.Length
                    && string.Equals(modAbbreviation,
                                     modName.Substring(newStart, modAbbreviation.Length),
                                     StringComparison.OrdinalIgnoreCase))
                {
                    var lookup = MaxQuantModification.Find(_modBank, modName.Substring(newStart));
                    if (lookup != null)
                        return new SeqMod(GetModPosition(modSequence, posFirstParen), lookup.MassDelta);
                }
            }
        }

        throw new MaxQuantWrongSequenceException(modAbbreviation, modSequence, _lineNum);
    }

    private static int GetModPosition(string modSeq, int posOpenParen)
    {
        int modPosition = 0;
        int inMod = 0;
        for (int i = 0; i < posOpenParen; i++)
        {
            char c = modSeq[i];
            if (c == '(') ++inMod;
            else if (c == ')') --inMod;
            else if (inMod == 0) modPosition++;
        }
        return Math.Max(1, modPosition);
    }

    private static List<SeqMod> GetFixedMods(char aa, int aaPosition, List<MaxQuantModification> mods)
    {
        var result = new List<SeqMod>();
        foreach (var mod in mods)
        {
            if (mod.Sites.Contains(aa))
                result.Add(new SeqMod(aaPosition, mod.MassDelta));
        }
        return result;
    }

    // --- helpers --------------------------------------------------------------------

    /// <summary>
    /// Tab-split <paramref name="line"/> honouring double-quoted cells. cpp parity:
    /// MaxQuantReader.h:173 — the cpp reader uses boost::escaped_list_separator
    /// ('\\', '\t', '"'), which (a) splits on tab, (b) treats anything inside a
    /// matched pair of double-quotes as a literal field with the quotes stripped,
    /// and (c) treats backslash as an escape character.
    /// </summary>
    /// <remarks>
    /// Without quote handling, MaxQuant rows like <c>"Acetyl (Protein N-term),Oxidation (M)"</c>
    /// in the Modifications column would leak the literal '"' into the mod-name lookup
    /// and fail to find the modification in the mod bank.
    /// </remarks>
    private static string[] SplitTabs(string line)
    {
        // strip trailing CR (StreamReader on a stray CRLF leaves the CR behind)
        if (line.Length > 0 && line[line.Length - 1] == '\r')
            line = line.Substring(0, line.Length - 1);

        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\\' && i + 1 < line.Length)
            {
                // cpp boost escape: backslash + next char emits the next char literally.
                current.Append(line[i + 1]);
                i++;
            }
            else if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == '\t' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    /// <summary>
    /// Inner spec-file reader used when MaxQuant uses embedded spectra (the peaks come straight
    /// off the parsed PSM rows rather than a separate spectrum file).
    /// </summary>
    /// <remarks>cpp parity: MaxQuantReader.cpp:1059 — the cpp class itself is the spec reader;
    /// the C# port keeps the two responsibilities separate.</remarks>
    private sealed class MaxQuantEmbeddedSpecFileReader : SpecFileReaderBase
    {
        public override void OpenFile(string path, bool mzSort = false) { /* no-op */ }
        public override SpecIdType IdType { set { /* no-op */ } }

        public override bool GetSpectrum(PSM psm, SpecIdType findBy, SpecData returnData, bool getPeaks)
        {
            ArgumentNullException.ThrowIfNull(psm);
            ArgumentNullException.ThrowIfNull(returnData);
            if (psm is not MaxQuantPSM mq) return false;

            returnData.Id = mq.SpecKey;
            returnData.RetentionTime = mq.RetentionTime;
            returnData.Mz = mq.Mz;
            returnData.NumPeaks = mq.Mzs.Count;
            returnData.IonMobility = (float)mq.IonMobility;
            returnData.IonMobilityType = mq.IonMobilityType;
            returnData.Ccs = (float)mq.Ccs;

            if (getPeaks)
            {
                returnData.Mzs = new double[returnData.NumPeaks];
                returnData.Intensities = new float[returnData.NumPeaks];
                for (int i = 0; i < returnData.NumPeaks; i++)
                {
                    returnData.Mzs[i] = mq.Mzs[i];
                    returnData.Intensities[i] = (float)mq.Intensities[i];
                }
            }
            else
            {
                returnData.Mzs = null;
                returnData.Intensities = null;
            }
            return true;
        }

        public override bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true)
            => false;
        public override bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true)
            => false;
        public override bool GetNextSpectrum(SpecData returnData, bool getPeaks = true)
            => false;
    }
}
