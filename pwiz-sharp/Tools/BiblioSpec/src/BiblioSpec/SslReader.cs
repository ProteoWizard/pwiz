// Port of pwiz_tools/BiblioSpec/src/SslReader.{h,cpp}
//
// Parses BiblioSpec SSL (Skyline Search List) files: tab-delimited PSM declarations with
// a header row that names the columns. Each row may carry its own score-type, so PSMs are
// grouped by source spectrum-file name and a per-file score-type is recorded for the
// BuildTables flush.
//
// cpp uses a DelimitedFileReader<sslPSM> template that drives setter callbacks per column;
// the C# port collapses that into a single ParseFile loop with a column-name -> setter map
// because the BiblioSpec port doesn't need DelimitedFileReader anywhere else.

using System.Globalization;
using Pwiz.Util.Proteome;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Per-row parsed-RT info carried alongside an SSL PSM. cpp parity: SslReader.h:30
/// <c>struct RTINFO</c>. All times are in minutes; an override of 0 is treated as
/// "not set" (cpp parity: SslReader.cpp:255).
/// </summary>
internal struct SslRtInfo
{
    public double RetentionTime;
    public double StartTime;
    public double EndTime;
}

/// <summary>
/// PSM subclass for rows of an SSL file. Adds the source filename, per-row score-type, and
/// optional RT overrides that get applied at <see cref="SslReader.ApplyPsmOverrideValues"/>
/// time.
/// </summary>
/// <remarks>cpp parity: SslReader.h:39 <c>class sslPSM</c>.</remarks>
internal sealed class SslPSM : PSM
{
    /// <summary>Source spectrum-file name (the "file" column).</summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>cpp parity: SslReader.h:42 — score type per row.</summary>
    public PsmScoreType ScoreType { get; set; } = PsmScoreType.UnknownScoreType;

    /// <summary>cpp parity: SslReader.h:43 — RT overrides parsed from per-row columns.</summary>
    public SslRtInfo RtInfo;
}

/// <summary>
/// Parses BiblioSpec <c>.ssl</c> (Skyline Search List) files. Each row declares a single PSM
/// with file / scan / charge / sequence and an optional score / RT / ion-mobility set. PSMs are
/// grouped by source filename and flushed one file at a time via
/// <see cref="BuildParser.BuildTables(PsmScoreType, string, bool, WorkflowType)"/>.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::SslReader</c> at <c>pwiz_tools/BiblioSpec/src/SslReader.{h,cpp}</c>.</para>
/// <para>The cpp class also implements <c>PwizReader</c> directly (cpp parity: SslReader.h:243) so it
/// can serve as both the result-file parser and the spectrum-file reader. The C# port keeps the two
/// concerns separate — <see cref="PwizSharpSpecFileReader"/> handles spectrum lookup and is wired in
/// via <see cref="BuildParser.SpecReader"/>.</para>
/// </remarks>
public sealed class SslReader : BuildParser
{
    private readonly string _sslName;
    private readonly string _sslDir;
    private readonly bool _hasHeader;

    // cpp parity: SslReader.h:266 — vector of PSMs for each spec file.
    private readonly Dictionary<string, List<PSM?>> _fileMap = new(StringComparer.Ordinal);
    // cpp parity: SslReader.h:267 — score type for each file.
    private readonly Dictionary<string, PsmScoreType> _fileScoreTypes = new(StringComparer.Ordinal);

    // cpp parity: SslReader.cpp:109 — column name -> sslPSM setter map. Populated once in
    // the ctor; cpp's DelimitedFileReader keeps a parallel "required" set, which we model
    // as a static array of required column names.
    private static readonly string[] _requiredColumns = { "file", "scan", "charge" };

    // cpp parity: SslReader.cpp:332 — `find_first_of("-,]")` terminator set for crosslink
    // position parsing. Hoisted to static readonly to avoid a per-call allocation inside the
    // hot parsing loop (CA1861).
    private static readonly char[] _crosslinkPositionTerminators = { '-', ',', ']' };

    private readonly Dictionary<string, Action<SslPSM, string, int>> _columnSetters;

    /// <summary>
    /// Returns true if <paramref name="path"/> is a Skyline Search List file (<c>.ssl</c>).
    /// Used by <see cref="BlibBuilder"/>'s reader-factory dispatch — each reader declares
    /// its own accepted extensions in one place.
    /// </summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith(".ssl", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Construct an SslReader bound to <paramref name="builder"/> and the .ssl file at
    /// <paramref name="sslFilename"/>.
    /// </summary>
    /// <remarks>cpp parity: SslReader.cpp:36. The cpp ctor wires <c>specReader_</c> to
    /// <c>this</c> (because cpp SslReader also implements <c>PwizReader</c>); in C# the caller
    /// assigns <see cref="BuildParser.SpecReader"/> separately.</remarks>
    public SslReader(BlibBuilder builder, string sslFilename, ProgressIndicator? parentProgress)
        : base(builder, sslFilename, parentProgress)
    {
        _sslName = sslFilename;
        _sslDir = BlibUtils.GetPath(_sslName);
        _hasHeader = true;

        // cpp parity: SslReader.cpp:109 — column -> setter table. Required cols throw if
        // missing values; optional cols accept "". Match cpp setter names verbatim.
        _columnSetters = new Dictionary<string, Action<SslPSM, string, int>>(StringComparer.OrdinalIgnoreCase)
        {
            ["file"] = SetFile,
            ["scan"] = SetScanNumber,
            ["charge"] = SetCharge,
            ["sequence"] = SetModifiedSequence,
            ["score-type"] = SetScoreType,
            ["score"] = SetScore,
            ["retention-time"] = SetRetentionTime,
            ["start-time"] = SetStartTime,
            ["end-time"] = SetEndTime,
            ["ion-mobility"] = SetIonMobility,
            ["ion-mobility-units"] = SetIonMobilityUnits,
            ["ccs"] = SetCcs,
            ["inchikey"] = SetInchiKey,
            ["adduct"] = SetPrecursorAdduct,
            ["chemicalformula"] = SetChemicalFormula,
            ["moleculename"] = SetMoleculeName,
            ["otherkeys"] = SetOtherKeys,
            ["precursormz"] = SetPrecursorMzDeclared,
        };
    }

    /// <summary>
    /// Score types this reader produces. cpp parity: SslReader.cpp:206 — parses the whole file
    /// just to collect the set of score types seen across all rows, then returns them sorted.
    /// </summary>
    public override IList<PsmScoreType> GetScoreTypes()
    {
        Parse();

        var seen = new SortedSet<PsmScoreType>();
        foreach (var kv in _fileScoreTypes)
            seen.Add(kv.Value);
        return new List<PsmScoreType>(seen);
    }

    /// <summary>
    /// Apply any RT override values that the SSL row carried (retention-time, start-time,
    /// end-time). cpp parity: SslReader.cpp:252 — only applied when retentionTime != 0.
    /// </summary>
    public override void ApplyPsmOverrideValues(PSM psm, SpecData specData)
    {
        ArgumentNullException.ThrowIfNull(psm);
        ArgumentNullException.ThrowIfNull(specData);

        if (psm is SslPSM ssl && ssl.RtInfo.RetentionTime != 0)
        {
            // cpp parity: SslReader.cpp:255 — RT override gate is retentionTime != 0; all three
            // values then transfer regardless of whether they are individually zero.
            specData.RetentionTime = ssl.RtInfo.RetentionTime;
            specData.StartTime = ssl.RtInfo.StartTime;
            specData.EndTime = ssl.RtInfo.EndTime;
        }
    }

    /// <summary>
    /// Parse the SSL file, group its PSMs by source filename, and flush each group to the
    /// library via <see cref="BuildParser.BuildTables(PsmScoreType, string, bool, WorkflowType)"/>.
    /// </summary>
    /// <returns>True on success. Throws <see cref="BlibException"/> on parse errors.</returns>
    /// <remarks>cpp parity: SslReader.cpp:153 <c>parseFile()</c>.</remarks>
    public override bool ParseFile()
    {
        Parse();

        // cpp parity: SslReader.cpp:157 — only report file-level progress if there's more than one.
        if (_fileMap.Count > 1)
        {
            InitSpecFileProgress(_fileMap.Count);
        }

        // cpp parity: SslReader.cpp:162 — iterate the file map and flush per file.
        foreach (var fileEntry in _fileMap)
        {
            var filename = fileEntry.Key;

            try
            {
                // cpp parity: SslReader.cpp:167 — try the filename as-is first.
                SetSpecFileName(filename);
            }
            catch (BlibException)
            {
                // cpp parity: SslReader.cpp:168 — fall back to the ssl-dir-relative path.
                SetSpecFileName(_sslDir + filename);
            }

            // cpp parity: SslReader.cpp:174 — move PSMs from the map into psms_.
            Psms.Clear();
            foreach (var p in fileEntry.Value)
                Psms.Add(p);

            // cpp parity: SslReader.cpp:177 — pick the lookup style by inspecting the first
            // non-precursor-only PSM.
            LookUpBy = SpecIdType.Unknown;
            for (var i = 0; i < Psms.Count; i++)
            {
                if (Psms[i] is not SslPSM psm) continue;
                if (psm.SpecIndex >= 0)
                {
                    LookUpBy = SpecIdType.IndexId;
                    break;
                }
                else if (psm.SpecKey < 0)
                {
                    LookUpBy = SpecIdType.NameId;
                    break;
                }
                else if (!psm.IsPrecursorOnly())
                {
                    LookUpBy = SpecIdType.ScanNumberId;
                    break;
                }
            }
            if (LookUpBy == SpecIdType.Unknown)
                LookUpBy = SpecIdType.ScanNumberId;

            BuildTables(_fileScoreTypes[filename]);
        }

        return true;
    }

    /// <summary>
    /// Read the SSL file and dispatch each row through the column setters. cpp parity:
    /// SslReader.cpp:141 <c>parse()</c>.
    /// </summary>
    private void Parse()
    {
        Verbosity.Debug("Parsing File.");

        // cpp parity: SslReader.cpp:142 — reset state because Parse() is called twice
        // (once by GetScoreTypes, once by ParseFile).
        _fileMap.Clear();
        _fileScoreTypes.Clear();

        using var reader = new StreamReader(_sslName);

        string? rawHeader = null;
        if (_hasHeader)
        {
            rawHeader = reader.ReadLine();
            if (rawHeader is null)
            {
                throw new BlibException(true, $"SSL file is empty: {_sslName}");
            }
        }

        // cpp parity: SslReader.cpp:137 — separator is tab. Build the header-name -> column-index map.
        var columnIndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (rawHeader is not null)
        {
            // Trim trailing CR if present (handle CRLF endings even on StreamReader-default
            // newline detection cases).
            var headerCols = SplitTabs(rawHeader);
            for (var c = 0; c < headerCols.Length; c++)
            {
                // Strip surrounding double-quotes from header tokens too — the data-row token
                // loop at line 305 does the same. Without this, an SSL with a quoted header
                // (e.g. "file"\t"scan"\t"charge") populates columnIndexByName with quoted keys
                // and every setter lookup misses → required-column check throws.
                var name = StripQuotes(headerCols[c].Trim());
                if (name.Length == 0) continue;
                columnIndexByName[name] = c;
            }
        }

        // cpp parity: addRequiredColumn (SslReader.cpp:112-115) — verify the 3 required columns
        // are present in the header.
        foreach (var required in _requiredColumns)
        {
            if (!columnIndexByName.ContainsKey(required))
            {
                throw new BlibException(true,
                    $"Required column '{required}' is missing from header in '{_sslName}'.");
            }
        }

        // Pre-resolve each known column to (index, setter) so the per-row loop is a tight
        // walk over only the columns that actually exist.
        var activeColumns = new List<(int Index, Action<SslPSM, string, int> Setter)>();
        foreach (var kv in _columnSetters)
        {
            if (columnIndexByName.TryGetValue(kv.Key, out var idx))
                activeColumns.Add((idx, kv.Value));
        }

        // cpp parity: SslReader.cpp:150 — parseFile reads each line and calls addDataLine.
        // Skip blank lines (cpp's DelimitedFileReader does the same when stream returns "").
        var lineNumber = _hasHeader ? 1 : 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            if (line.Length == 0) continue;

            var cols = SplitTabs(line);

            var newPsm = new SslPSM();
            foreach (var (idx, setter) in activeColumns)
            {
                var value = idx < cols.Length ? StripQuotes(cols[idx].Trim()) : string.Empty;
                try
                {
                    setter(newPsm, value, lineNumber);
                }
                catch (BlibException ex)
                {
                    // Annotate the message with line number / file context, like cpp.
                    throw new BlibException(true,
                        $"{_sslName} line {lineNumber.ToString(CultureInfo.InvariantCulture)}: {ex.Message}");
                }
            }

            AddDataLine(newPsm);
        }
    }

    /// <summary>
    /// Add a freshly-parsed row to <see cref="_fileMap"/>. Parses the modified-sequence syntax
    /// (or crosslink syntax if the sequence contains '@') into <see cref="PSM.Mods"/> and
    /// rewrites <see cref="PSM.UnmodSeq"/> to the bare amino acid sequence.
    /// </summary>
    /// <remarks>cpp parity: SslReader.cpp:61 <c>addDataLine</c>.</remarks>
    private void AddDataLine(SslPSM newPsm)
    {
        // cpp parity: SslReader.cpp:63 — keep the precursor-only id rich (re-build the full id
        // string now that the small-mol fields are populated).
        if (newPsm.IsPrecursorOnly())
        {
            newPsm.SetPrecursorOnly();
        }

        Verbosity.Comment(VerbosityLevel.Detail,
            $"Adding new psm (scan {newPsm.IdAsString()}) from delim file reader.");

        // cpp parity: SslReader.cpp:72 — clone (cpp uses new + copy ctor); in C# the SslPSM IS
        // the parsed row, so just mutate its mod state and clear its raw modified-seq.
        var curPsm = newPsm;
        curPsm.ModifiedSeq = string.Empty;
        curPsm.Mods.Clear();

        // cpp parity: SslReader.cpp:76 — if no '@' in the unmodSeq, treat as standard modified
        // peptide sequence with bracketed deltas; otherwise treat as crosslinked syntax.
        if (curPsm.UnmodSeq.IndexOf('@', StringComparison.Ordinal) < 0)
        {
            ParseModSeq(curPsm.Mods, curPsm.UnmodSeq);
            curPsm.UnmodSeq = UnmodifySequence(curPsm.UnmodSeq);
        }
        else
        {
            curPsm.ModifiedSeq = curPsm.UnmodSeq;
            curPsm.UnmodSeq = ParseCrosslinkedSequence(curPsm.Mods, curPsm.ModifiedSeq);
        }

        // cpp parity: SslReader.cpp:89 — completeness gate (must have an identifier + content).
        if (!curPsm.IsCompleteEnough())
        {
            throw new BlibException(false, "Incomplete description: " + curPsm.IdAsString());
        }

        // cpp parity: SslReader.cpp:96 — first PSM for this file establishes the score type.
        if (!_fileMap.TryGetValue(newPsm.Filename, out var list))
        {
            list = new List<PSM?> { curPsm };
            _fileMap[newPsm.Filename] = list;
            _fileScoreTypes[newPsm.Filename] = newPsm.ScoreType;
        }
        else
        {
            list.Add(curPsm);
        }
    }

    // ----- column setters (cpp parity: SslReader.h:50-225) ------------------------------

    // cpp parity: SslReader.h:50 — setFile. Required, throws on empty.
    private static void SetFile(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0)
            throw new BlibException(false, "Missing filename.");
        psm.Filename = value;
    }

    // cpp parity: SslReader.h:57 — setScanNumber. Empty value means precursor-only record.
    private static void SetScanNumber(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0)
        {
            Verbosity.Comment(VerbosityLevel.Detail,
                "Missing MS2 scan ID. Treating this as a precursor-only record.");
            psm.SetPrecursorOnly();
            return;
        }

        // cpp parity: SslReader.h:63 — first try as plain int, then "index=" / "scan=" prefixes,
        // else treat as a free-form spectrum name. Leading zeros are trimmed (cpp lexical_cast
        // would accept them but trimLeadingZeros normalizes ids in case "00042" is later compared
        // against "42" downstream).
        if (TryParseTrimmedInt(value, out var asInt))
        {
            psm.SpecKey = asInt;
            psm.SpecIndex = -1;
            return;
        }

        if (value.StartsWith("index=", StringComparison.OrdinalIgnoreCase))
        {
            var rest = value.Substring("index=".Length);
            if (!int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                throw new BlibException(false, $"Non-numeric index value: {value}.");
            psm.SpecIndex = idx;
        }
        else if (value.StartsWith("scan=", StringComparison.OrdinalIgnoreCase))
        {
            var rest = value.Substring("scan=".Length);
            if (!TryParseTrimmedInt(rest, out var sk))
                throw new BlibException(false, $"Non-numeric scan value: {value}.");
            psm.SpecKey = sk;
            psm.SpecIndex = -1;
        }
        else
        {
            psm.SpecName = value;
        }
    }

    // cpp parity: SslReader.h:78 — setCharge. Empty => 0; non-numeric => throw.
    private static void SetCharge(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0)
        {
            psm.Charge = 0;
            return;
        }
        if (!TryParseTrimmedInt(value, out var z))
            throw new BlibException(false, $"Non-numeric charge value: {value}.");
        psm.Charge = z;
    }

    // cpp parity: SslReader.h:93 — setModifiedSequence. Stashed in UnmodSeq until AddDataLine
    // parses out the mods.
    private static void SetModifiedSequence(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0)
            throw new BlibException(false, "Missing peptide sequence.");
        psm.UnmodSeq = value;
    }

    // cpp parity: SslReader.h:100 — setScoreType uses BlibUtils.stringToScoreType (case-sensitive
    // in cpp; we mirror with the ordinal lookup baked into BlibUtils.StringToScoreType).
    private static void SetScoreType(SslPSM psm, string value, int lineNumber)
    {
        psm.ScoreType = BlibUtils.StringToScoreType(value);
    }

    // cpp parity: SslReader.h:104 — setScore. Empty => 0; non-numeric => throw.
    private static void SetScore(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0)
        {
            psm.Score = 0;
            return;
        }
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            throw new BlibException(false, $"Non-numeric score: {value}");
        psm.Score = d;
    }

    // cpp parity: SslReader.h:116 — setRetentionTime.
    private static void SetRetentionTime(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            throw new BlibException(false, $"Non-numeric retention time: {value}");
        psm.RtInfo.RetentionTime = d;
    }

    // cpp parity: SslReader.h:126 — setStartTime.
    private static void SetStartTime(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            throw new BlibException(false, $"Non-numeric start time: {value}");
        psm.RtInfo.StartTime = d;
    }

    // cpp parity: SslReader.h:137 — setEndTime.
    private static void SetEndTime(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            throw new BlibException(false, $"Non-numeric end time: {value}");
        psm.RtInfo.EndTime = d;
    }

    // cpp parity: SslReader.h:148 — setIonMobility.
    private static void SetIonMobility(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            throw new BlibException(false, $"Non-numeric ion mobility value: {value}");
        psm.IonMobility = d;
    }

    // cpp parity: SslReader.h:159 — setIonMobilityUnits.
    private static void SetIonMobilityUnits(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0) return;
        psm.IonMobilityType = BlibUtils.ParseIonMobilityType(value);
    }

    // cpp parity: SslReader.h:164 — setCCS.
    private static void SetCcs(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            throw new BlibException(false, $"Non-numeric CCS: {value}");
        psm.Ccs = d;
    }

    // cpp parity: SslReader.h:175 — setPrecursorAdduct. Required-when-present (throws on empty).
    private static void SetPrecursorAdduct(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0)
            throw new BlibException(false, "Missing precursor adduct.");
        psm.SmallMolMetadata.PrecursorAdduct = value;
    }

    // cpp parity: SslReader.h:183 — setChemicalFormula.
    private static void SetChemicalFormula(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0)
            throw new BlibException(false, "Missing chemical formula.");
        psm.SmallMolMetadata.ChemicalFormula = value;
    }

    // cpp parity: SslReader.h:191 — setInchiKey.
    private static void SetInchiKey(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0)
            throw new BlibException(false, "Missing InChiKey.");
        psm.SmallMolMetadata.InchiKey = value;
    }

    // cpp parity: SslReader.h:199 — setMoleculeName.
    private static void SetMoleculeName(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0)
            throw new BlibException(false, "Missing molecule name.");
        psm.SmallMolMetadata.MoleculeName = value;
    }

    // cpp parity: SslReader.h:207 — setotherKeys.
    private static void SetOtherKeys(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0)
            throw new BlibException(false, "Missing otherKeys.");
        psm.SmallMolMetadata.OtherKeys = value;
    }

    // cpp parity: SslReader.h:215 — setPrecursorMzDeclared.
    private static void SetPrecursorMzDeclared(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            throw new BlibException(false, $"Non-numeric precursorMZ: {value}");
        psm.SmallMolMetadata.PrecursorMzDeclared = d;
    }

    // ----- modification parsing helpers --------------------------------------------------

    /// <summary>
    /// Walk a modified peptide sequence of the form <c>PEPM[+15.99]TIDE</c>, appending one
    /// <see cref="SeqMod"/> per bracketed mass and tracking the 1-based residue position.
    /// </summary>
    /// <remarks>cpp parity: SslReader.cpp:267 <c>parseModSeq</c>. Mirrors cpp's allowed
    /// character set: <c>A-Z</c>, <c>[</c>, <c>]</c>, <c>.</c>, and digits — anything else
    /// throws.</remarks>
    private static void ParseModSeq(List<SeqMod> mods, string modSeq)
    {
        int pos = 0; // cpp parity: SeqMod pos is 1-based; pos++ when we see an uppercase letter.
        for (int i = 0; i < modSeq.Length; i++)
        {
            char c = modSeq[i];
            if (c >= 'A' && c <= 'Z')
            {
                pos++;
            }
            else if (c != '[' && c != ']' && c != '.' && !char.IsDigit(c))
            {
                throw new BlibException(false,
                    "Only uppercase letters (amino acids) and bracketed modifications ('[123.4]') are allowed in peptide sequences: " + modSeq);
            }
            else if (c == '[')
            {
                int closePos = modSeq.IndexOf(']', ++i);
                if (closePos < 0)
                {
                    throw new BlibException(false,
                        "Sequence had opening bracket without closing bracket: " + modSeq);
                }
                var mass = modSeq.Substring(i, closePos - i);
                // cpp parity: SslReader.cpp:282 — atof, max(1, pos). atof stops at first
                // non-numeric character and returns 0 on parse failure; mirror by accepting
                // a TryParse-failure as 0 (rather than throwing).
                _ = double.TryParse(mass, NumberStyles.Float, CultureInfo.InvariantCulture, out var deltaMass);
                mods.Add(new SeqMod(Math.Max(1, pos), deltaMass));
                i = closePos;
            }
        }
    }

    /// <summary>
    /// Parse a crosslinked peptide sequence of the form
    /// <c>KC[+57.021464]DDK-EC[+57.021464]PKC[+57.021464]HEK-[+138.06808@1,4]</c>. Adds the
    /// modifications of the first peptide (plus the crosslinker placed at its anchor residue)
    /// to <paramref name="mods"/>, and returns the unmodified sequence joined by '-'.
    /// </summary>
    /// <remarks>cpp parity: SslReader.cpp:309 <c>parseCrosslinkedSequence</c>.</remarks>
    private static string ParseCrosslinkedSequence(List<SeqMod> mods, string crosslinkedSequence)
    {
        var peptideSequences = new List<string>();
        var currentPeptideSequence = new System.Text.StringBuilder();
        double massOfCrosslinkedPeptides = 0;
        int positionOfFirstCrosslinkerInFirstPeptide = -1;

        for (int i = 0; i < crosslinkedSequence.Length; i++)
        {
            char c = crosslinkedSequence[i];
            if (c >= 'A' && c <= 'Z')
            {
                currentPeptideSequence.Append(c);
            }
            else if (c == '-')
            {
                peptideSequences.Add(currentPeptideSequence.ToString());
                currentPeptideSequence.Clear();
            }
            else if (c == '[')
            {
                int closePos = crosslinkedSequence.IndexOf(']', ++i);
                if (closePos < 0)
                {
                    throw new BlibException(false,
                        "Sequence had opening bracket without closing bracket: " + crosslinkedSequence);
                }

                if (currentPeptideSequence.Length == 0)
                {
                    // cpp parity: SslReader.cpp:327 — crosslinker spec inside trailing []
                    int atSignPos = crosslinkedSequence.IndexOf('@', i);
                    if (atSignPos < 0)
                    {
                        throw new BlibException(false,
                            "Unable to find crosslinker mass in sequence: " + crosslinkedSequence);
                    }

                    var massStr = crosslinkedSequence.Substring(i, atSignPos - i);
                    if (!double.TryParse(massStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var addMass))
                    {
                        throw new BlibException(false,
                            "Unable to interpret crosslinker mass in sequence: " + crosslinkedSequence);
                    }
                    massOfCrosslinkedPeptides += addMass;

                    int numberEnd = crosslinkedSequence.IndexOfAny(_crosslinkPositionTerminators, atSignPos + 1);
                    if (numberEnd < 0)
                    {
                        throw new BlibException(false,
                            "Unable to interpret crosslink positions in sequence: " + crosslinkedSequence);
                    }

                    if (positionOfFirstCrosslinkerInFirstPeptide == -1)
                    {
                        if (crosslinkedSequence[atSignPos + 1] != '*')
                        {
                            // cpp parity: SslReader.cpp:338 — boost::lexical_cast<int> of
                            // crosslinkedSequence.substr(atSignPos+1, numberEnd). Note this
                            // substr is *length=numberEnd* in cpp, which means it slurps WAY
                            // past the actual digits — but lexical_cast<int> requires the
                            // entire string to parse, so this is actually a cpp bug. We
                            // mirror cpp by reading from atSignPos+1 to numberEnd (the first
                            // delimiter) which is what cpp semantically intended.
                            var posStr = crosslinkedSequence.Substring(
                                atSignPos + 1, numberEnd - (atSignPos + 1));
                            if (!int.TryParse(posStr, NumberStyles.Integer,
                                    CultureInfo.InvariantCulture, out var pos))
                            {
                                throw new BlibException(false,
                                    "Unable to interpret crosslink positions in sequence: " + crosslinkedSequence);
                            }
                            positionOfFirstCrosslinkerInFirstPeptide = pos;
                        }
                    }
                }
                else
                {
                    // cpp parity: SslReader.cpp:342 — modification mass on an in-peptide residue.
                    var massStr = crosslinkedSequence.Substring(i, closePos - i);
                    if (!double.TryParse(massStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var modMass))
                    {
                        throw new BlibException(false,
                            "Unable to interpret modification mass in sequence: " + crosslinkedSequence);
                    }
                    if (peptideSequences.Count == 0)
                    {
                        mods.Add(new SeqMod(currentPeptideSequence.Length, modMass));
                    }
                    else
                    {
                        massOfCrosslinkedPeptides += modMass;
                    }
                }
                i = closePos;
            }
            else
            {
                throw new BlibException(false,
                    string.Format(CultureInfo.InvariantCulture,
                        "Unexpected character '{0}' at position {1} in crosslinked peptide: {2}",
                        c, i + 1, crosslinkedSequence));
            }
        }

        if (positionOfFirstCrosslinkerInFirstPeptide == -1)
        {
            throw new BlibException(false,
                "Crosslinked peptide is not connected: " + crosslinkedSequence);
        }

        // cpp parity: SslReader.cpp:357 — add monoisotopic masses of the other linked peptides.
        for (int iPeptide = 1; iPeptide < peptideSequences.Count; iPeptide++)
        {
            var p = new Peptide(peptideSequences[iPeptide]);
            massOfCrosslinkedPeptides += p.MonoisotopicMass();
        }
        mods.Add(new SeqMod(positionOfFirstCrosslinkerInFirstPeptide, massOfCrosslinkedPeptides));

        return string.Join("-", peptideSequences);
    }

    /// <summary>
    /// Strip all <c>[...]</c> bracketed modifications from the sequence and remove leading /
    /// trailing dashes (cpp parity: SslReader.cpp:367 <c>unmodifySequence</c>).
    /// </summary>
    private static string UnmodifySequence(string seq)
    {
        // Strip bracketed mods.
        var sb = new System.Text.StringBuilder(seq.Length);
        for (int i = 0; i < seq.Length; i++)
        {
            char c = seq[i];
            if (c == '[')
            {
                int close = seq.IndexOf(']', i);
                if (close < 0) break;
                i = close;
                continue;
            }
            sb.Append(c);
        }
        var result = sb.ToString();

        // cpp parity: SslReader.cpp:375 — trim a single leading and trailing '-'.
        if (result.Length > 0 && result[0] == '-')
            result = result.Substring(1);
        if (result.Length > 0 && result[result.Length - 1] == '-')
            result = result.Substring(0, result.Length - 1);

        return result;
    }

    // ----- helpers -----------------------------------------------------------------------

    /// <summary>
    /// Parse <paramref name="value"/> as an int after trimming leading zeros. cpp parity:
    /// SslReader.h:228 <c>trimLeadingZeros</c> + <c>boost::lexical_cast&lt;int&gt;</c>.
    /// </summary>
    private static bool TryParseTrimmedInt(string value, out int result)
    {
        result = 0;
        if (value.Length == 0) return false;
        // cpp parity: a string of all zeros maps to "0", not "".
        int firstNonZero = 0;
        while (firstNonZero < value.Length && value[firstNonZero] == '0') firstNonZero++;
        var trimmed = firstNonZero == value.Length
            ? "0"
            : value.Substring(firstNonZero);
        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Split a line on tab, stripping a single trailing CR if present (cpp's istream-based
    /// line read swallows the LF but not the CR on mixed CRLF input).
    /// </summary>
    /// <summary>
    /// Strip a matched pair of surrounding double-quotes from a token. SSL files use quoted
    /// values for cells containing spaces (e.g. <c>"PERCOLATOR QVALUE"</c> in score-type
    /// columns). cpp's DelimitedFileReader handles this internally; we do it inline.
    /// </summary>
    private static string StripQuotes(string s)
    {
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
            return s[1..^1];
        return s;
    }

    private static string[] SplitTabs(string line)
    {
        if (line.Length > 0 && line[line.Length - 1] == '\r')
            line = line.Substring(0, line.Length - 1);
        return line.Split('\t');
    }
}
