// Port of pwiz_tools/BiblioSpec/src/mzTabReader.{h,cpp}
//
// Parses the PSM section of a PSI mzTab file. mzTab is a tab-delimited result format with
// up to four record types — MTD (metadata), PRH/PRT (proteins), PEH/PEP (peptides),
// PSH/PSM (psms), SMH/SML (small molecules). This reader scans for the PSM table only:
// the PSH row gives the column layout, every PSM row is one identification, and MTD rows
// supply the spectrum-file map (ms_run[#]-location) plus the search-engine score type
// (psm_search_engine_score[#]).
//
// Score-type priority (high-to-low): percolator Q value, X!Tandem expect, Mascot
// expectation, MaxQuant PEP, MS-GF+ q-value, then a generic PSM-level q-value fallback.
// The first acceptable score declared in the MTD block wins.
//
// Spectra come from a separate spectrum file (per BuildParser); the PSM row's
// `spectra_ref` column points at one of the registered ms_runs and carries a nativeID
// that lets BuildParser look up the peaks at flush time.

using System.Globalization;
using System.Xml;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Parses BiblioSpec mzTab (<c>.mzTab</c> / <c>mztab.txt</c>) files. Reads the PSM table
/// (PSH header + PSM rows), groups identifications by spectrum-file name (from the
/// metadata <c>ms_run[#]-location</c> declarations), then flushes each group via
/// <see cref="BuildParser.BuildTables(PsmScoreType, string, bool, WorkflowType)"/>.
/// </summary>
/// <remarks>
/// Port of <c>BiblioSpec::mzTabReader</c> at
/// <c>pwiz_tools/BiblioSpec/src/mzTabReader.{h,cpp}</c>.
/// </remarks>
public sealed class MzTabReader : BuildParser
{
    // cpp parity: mzTabReader.cpp:32-37 — magic strings used across the parser.
    private const string NullField = "null";
    private const string PsmChargeField = "charge";
    private const string PsmSeqField = "sequence";
    private const string PsmModsField = "modifications";
    private const string PsmSpecField = "spectra_ref";
    private const string PsmScoreField = "search_engine_score"; // search_engine_score[1-n]

    /// <summary>
    /// One acceptable search-engine score type and its mapping back to the BiblioSpec
    /// score-type enum + the input-classifier bucket used to fetch its threshold.
    /// </summary>
    /// <remarks>cpp parity: mzTabReader.h:44 — <c>boost::tuple&lt;string, bool, PSM_SCORE_TYPE, BUILD_INPUT&gt;</c>.</remarks>
    private readonly struct ScoreTypeSpec
    {
        public string Cv { get; }
        public bool HigherIsBetter { get; }
        public PsmScoreType ScoreType { get; }
        public BuildInput BuildInput { get; }

        public ScoreTypeSpec(string cv, bool higherIsBetter, PsmScoreType scoreType, BuildInput buildInput)
        {
            Cv = cv;
            HigherIsBetter = higherIsBetter;
            ScoreType = scoreType;
            BuildInput = buildInput;
        }
    }

    private readonly string _filename;
    private int _lineNum;

    // cpp parity: mzTabReader.h:44 — the prioritised list of acceptable score types.
    // High-to-low priority; first hit wins when multiple are declared in the MTD block.
    private readonly List<ScoreTypeSpec> _scoreTypes;

    // cpp parity: mzTabReader.h:45 — the index of the chosen score type in `_scoreTypes`.
    // Sentinel = `_scoreTypes.Count` ("no acceptable score type yet").
    private int _scoreIdxVector;

    // cpp parity: mzTabReader.h:46 — the [#] index used in `psm_search_engine_score[#]`,
    // i.e. which column in the PSH header carries the chosen score.
    private int _scoreIdxFile;

    // cpp parity: mzTabReader.h:47 — run number -> filename.
    private readonly Dictionary<int, string> _runs = new();

    // cpp parity: mzTabReader.h:48 — store psms by filename. SortedDictionary so iteration
    // is stable across runs (cpp uses std::map, also ascending key order).
    private readonly SortedDictionary<string, List<PSM?>> _fileMap = new(StringComparer.Ordinal);

    // cpp parity: mzTabReader.h:49 — PSH column label -> column index.
    private readonly Dictionary<string, int> _psh = new(StringComparer.Ordinal);

    // cpp parity: mzTabReader.cpp:66 — Unimod records, loaded once from <exeDir>/unimod.xml.
    private readonly Dictionary<int, double> _unimodMonoMass = new();

    /// <summary>Returns true if <paramref name="path"/> is recognised as an mzTab file.</summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith(".mzTab", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith("mztab.txt", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Construct an MzTabReader bound to <paramref name="maker"/> and the mzTab file at
    /// <paramref name="filename"/>.
    /// </summary>
    /// <remarks>cpp parity: mzTabReader.cpp:39.</remarks>
    public MzTabReader(BlibBuilder maker, string filename, ProgressIndicator? parentProgress)
        : base(maker, filename, parentProgress)
    {
        _filename = filename;
        _lineNum = 0;
        _scoreIdxFile = 0;

        // cpp parity: mzTabReader.cpp:43 — initialise acceptable score types. Priority is
        // high to low; the lowest-index match wins.
        _scoreTypes = new List<ScoreTypeSpec>
        {
            new("MS:1001491, percolator:Q value", false, PsmScoreType.PercolatorQValue, BuildInput.Sqt),
            new("MS:1001330, X!Tandem:expect", false, PsmScoreType.TandemExpectationValue, BuildInput.Tandem),
            new("MS:1001172, Mascot:expectation value", false, PsmScoreType.MascotIonsScore, BuildInput.Mascot),
            new("MS:1001901, MaxQuant:PEP", false, PsmScoreType.MaxQuantScore, BuildInput.MaxQuant),
            new("MS:1002054, MS-GF:QValue", false, PsmScoreType.MsgfScore, BuildInput.Msgf),
            new("MS:1002354, PSM-level q-value", false, PsmScoreType.GenericQValue, BuildInput.GenericQValueInput),
        };
        // cpp parity: mzTabReader.cpp:57 — sentinel = "no acceptable score found yet".
        _scoreIdxVector = _scoreTypes.Count;
    }

    /// <summary>
    /// Score types this reader produces. cpp parity: mzTabReader.cpp:99 — parses the file
    /// to learn which score column was chosen, then returns that score's enum value.
    /// </summary>
    public override IList<PsmScoreType> GetScoreTypes()
    {
        if (!Parse())
            return new List<PsmScoreType> { PsmScoreType.UnknownScoreType };

        // cpp parity: mzTabReader.cpp:103 — iterate fileMap; every entry yields the chosen
        // score type. Use a SortedSet to dedupe (cpp uses std::set<PSM_SCORE_TYPE>).
        var allScoreTypes = new SortedSet<PsmScoreType>();
        if (_fileMap.Count > 0 && _scoreIdxVector < _scoreTypes.Count)
        {
            allScoreTypes.Add(_scoreTypes[_scoreIdxVector].ScoreType);
        }
        return new List<PsmScoreType>(allScoreTypes);
    }

    /// <summary>
    /// Parse the mzTab file, group its PSMs by source spectrum-file name, and flush each
    /// group to the library.
    /// </summary>
    /// <remarks>cpp parity: mzTabReader.cpp:82 <c>parseFile()</c>.</remarks>
    public override bool ParseFile()
    {
        Parse();
        Verbosity.Debug("Building tables.");

        InitSpecFileProgress(_fileMap.Count);
        foreach (var fileEntry in _fileMap)
        {
            Psms.Clear();
            foreach (var p in fileEntry.Value)
                Psms.Add(p);

            // cpp parity: mzTabReader.cpp:88 — try the file as-is; if missing, fall back to
            // the bare filename (drop any path).
            if (File.Exists(fileEntry.Key))
            {
                SetSpecFileName(fileEntry.Key, checkFile: false);
            }
            else
            {
                SetSpecFileName(Path.GetFileName(fileEntry.Key), checkFile: true);
            }

            // cpp parity: mzTabReader.cpp:94 — flush using the chosen score type.
            BuildTables(_scoreTypes[_scoreIdxVector].ScoreType, fileEntry.Key, showSpecProgress: false);
        }
        return true;
    }

    // --- top-level parse driver ----------------------------------------------------------

    /// <summary>
    /// Load Unimod once, then read every line of the mzTab file into <see cref="_fileMap"/>.
    /// </summary>
    /// <remarks>cpp parity: mzTabReader.cpp:66 <c>parse()</c>.</remarks>
    private bool Parse()
    {
        // cpp parity: mzTabReader.cpp:67 — Unimod lookup table for UNIMOD: mod IDs.
        Verbosity.Debug("Parsing Unimod");
        var unimodFile = Path.Combine(BlibUtils.GetExeDirectory(), "unimod.xml");
        LoadUnimod(unimodFile);
        Verbosity.Debug($"Successfully parsed {_unimodMonoMass.Count.ToString(CultureInfo.InvariantCulture)} Unimod records");

        Verbosity.Debug("Opening file");
        if (!File.Exists(_filename))
            return false;

        Verbosity.Debug("Collecting PSMs");

        // cpp parity: mzTabReader.cpp:110 — count lines for the progress indicator before
        // walking the file a second time.
        var lines = File.ReadAllLines(_filename);
        var progress = new ProgressIndicator(lines.Length + 1);

        _fileMap.Clear();
        _runs.Clear();
        _psh.Clear();
        _lineNum = 0;

        foreach (var line in lines)
        {
            _lineNum++;
            ParseLine(line);
            progress.Increment();
        }

        return true;
    }

    // --- line dispatcher ----------------------------------------------------------------

    /// <summary>cpp parity: mzTabReader.cpp:125 <c>parseLine</c>.</summary>
    private void ParseLine(string line)
    {
        if (line.Length == 0) return;

        // cpp parity: split on '\t'. boost::split + is_any_of("\t") returns at least one
        // field (the whole string) even on an empty input — guard the empty case above.
        var fields = SplitTabs(line);
        if (fields.Length == 0) return;

        var fieldType = fields[0];
        if (fieldType == "MTD")
        {
            ParseMetadataLine(fields);
        }
        else if (fieldType == "PSH")
        {
            ParsePshLine(fields);
        }
        else if (fieldType == "PSM")
        {
            ParsePsmLine(fields);
        }
    }

    /// <summary>
    /// Parse an <c>MTD</c> (metadata) line. Two MTD keys are interesting to us:
    /// <c>ms_run[#]-location</c> (register the spectrum file) and
    /// <c>psm_search_engine_score[#]</c> (declare the score column to use).
    /// </summary>
    /// <remarks>cpp parity: mzTabReader.cpp:132.</remarks>
    private void ParseMetadataLine(string[] fields)
    {
        if (fields.Length != 3) return;

        const string filePrefix = "ms_run[";
        const string fileSuffix = "]-location";
        const string scoreNumPrefix = "psm_search_engine_score[";
        const string scoreNumSuffix = "]";

        if (fields[1].StartsWith(filePrefix, StringComparison.Ordinal)
            && fields[1].EndsWith(fileSuffix, StringComparison.Ordinal))
        {
            // cpp parity: mzTabReader.cpp:139 — extract the [#] index and the file path.
            var runStr = fields[1].Substring(
                filePrefix.Length,
                fields[1].Length - filePrefix.Length - fileSuffix.Length);
            var runFile = fields[2];

            // cpp parity: mzTabReader.cpp:142 — strip the "file://" scheme.
            const string fileLocationPrefix = "file://";
            if (runFile.StartsWith(fileLocationPrefix, StringComparison.Ordinal))
                runFile = runFile.Substring(fileLocationPrefix.Length);

            if (!int.TryParse(runStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var runIdx))
            {
                throw new BlibException(false,
                    $"Invalid file number '{runStr}' at line {_lineNum.ToString(CultureInfo.InvariantCulture)}");
            }
            _runs[runIdx] = runFile;
        }
        else if (fields[1].StartsWith(scoreNumPrefix, StringComparison.Ordinal)
                 && fields[1].EndsWith(scoreNumSuffix, StringComparison.Ordinal))
        {
            // cpp parity: mzTabReader.cpp:151 — pick the highest-priority match.
            var searchEngine = fields[2];
            for (var i = 0; i < _scoreIdxVector; i++)
            {
                var spec = _scoreTypes[i];
                if (searchEngine.IndexOf(spec.Cv, StringComparison.Ordinal) < 0)
                    continue;

                _scoreIdxVector = i;
                var scoreNum = fields[1].Substring(
                    scoreNumPrefix.Length,
                    fields[1].Length - scoreNumPrefix.Length - scoreNumSuffix.Length);
                if (!int.TryParse(scoreNum, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scoreIdx))
                {
                    throw new BlibException(false,
                        $"Invalid search engine score number '{scoreNum}' at line {_lineNum.ToString(CultureInfo.InvariantCulture)}");
                }
                _scoreIdxFile = scoreIdx;
                break;
            }
        }
    }

    /// <summary>cpp parity: mzTabReader.cpp:168 — handle the PSH (PSM header) row.</summary>
    private void ParsePshLine(string[] fields)
    {
        if (_psh.Count > 0)
        {
            throw new BlibException(false,
                $"Multiple PSH lines found at line {_lineNum.ToString(CultureInfo.InvariantCulture)}");
        }
        if (_scoreIdxVector >= _scoreTypes.Count)
        {
            throw new BlibException(false, "No acceptable score type found");
        }

        for (var i = 0; i < fields.Length; i++)
            _psh[fields[i]] = i;

        // cpp parity: mzTabReader.cpp:177 — required columns.
        string[] required = { PsmChargeField, PsmSeqField, PsmModsField, PsmSpecField };
        foreach (var col in required)
        {
            if (!_psh.ContainsKey(col))
            {
                throw new BlibException(false,
                    $"PSH line missing required field '{col}' at line {_lineNum.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        Verbosity.Status($"Using score '{_scoreTypes[_scoreIdxVector].Cv}'");
    }

    /// <summary>cpp parity: mzTabReader.cpp:188 — handle a PSM row.</summary>
    private void ParsePsmLine(string[] fields)
    {
        if (_psh.Count == 0)
        {
            throw new BlibException(false,
                $"No PSH line found before PSM at line {_lineNum.ToString(CultureInfo.InvariantCulture)}");
        }
        if (_psh.Count != fields.Length)
        {
            throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                "PSH line had {0} fields, but PSM at line {1} had {2}",
                _psh.Count, _lineNum, fields.Length));
        }

        var charge = ParseCharge(fields);
        if (!TryParseSequence(fields, out var seq, out var mods))
            return;

        var spectra = ParseSpectrum(fields);
        var passedThreshold = TryParseScore(fields, out var score);
        if (!passedThreshold)
            FilteredOutPsmCount++;

        const string scanPrefix = "scan=";
        const string indexPrefix = "index=";

        foreach (var (specFile, specRef) in spectra)
        {
            if (!passedThreshold)
            {
                // cpp parity: mzTabReader.cpp:214 — still register the file so the spec-file
                // progress count is right, but don't add a PSM for it.
                if (!_fileMap.ContainsKey(specFile))
                    _fileMap[specFile] = new List<PSM?>();
                continue;
            }

            var psm = new PSM
            {
                Charge = charge,
                UnmodSeq = seq,
                Score = score,
            };
            foreach (var m in mods)
                psm.Mods.Add(m);

            if (specRef.StartsWith(scanPrefix, StringComparison.Ordinal))
            {
                var scanStr = specRef.Substring(scanPrefix.Length);
                if (!int.TryParse(scanStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scan))
                {
                    throw new BlibException(false,
                        $"Invalid scan '{scanStr}' on line {_lineNum.ToString(CultureInfo.InvariantCulture)}");
                }
                psm.SpecKey = scan;
            }
            else if (specRef.StartsWith(indexPrefix, StringComparison.Ordinal))
            {
                var indexStr = specRef.Substring(indexPrefix.Length);
                if (!int.TryParse(indexStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                {
                    throw new BlibException(false,
                        $"Invalid index '{indexStr}' on line {_lineNum.ToString(CultureInfo.InvariantCulture)}");
                }
                psm.SpecIndex = idx;
            }
            else
            {
                psm.SpecName = specRef;
            }
            CurPsm = psm;

            if (!_fileMap.TryGetValue(specFile, out var list))
            {
                list = new List<PSM?>();
                _fileMap[specFile] = list;
            }
            list.Add(psm);
        }
    }

    // --- per-field parsers --------------------------------------------------------------

    /// <summary>cpp parity: mzTabReader.cpp:249.</summary>
    private int ParseCharge(string[] fields)
    {
        var raw = fields[_psh[PsmChargeField]];
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var charge))
        {
            throw new BlibException(false,
                $"Invalid charge '{raw}' on line {_lineNum.ToString(CultureInfo.InvariantCulture)}");
        }
        return charge;
    }

    /// <summary>
    /// Parse the modification column into a list of <see cref="SeqMod"/>. Returns false if
    /// the PSM should be skipped (positionless mod, unknown Unimod id, etc.).
    /// </summary>
    /// <remarks>cpp parity: mzTabReader.cpp:258 — mod grammar is
    /// <c>{position}{parameter}-[{identifier}|{neutralLoss}]</c>, separated by commas.</remarks>
    private bool TryParseSequence(string[] fields, out string outSequence, out List<SeqMod> outMods)
    {
        outSequence = fields[_psh[PsmSeqField]];
        outMods = new List<SeqMod>();

        var modStr = fields[_psh[PsmModsField]];
        if (modStr == NullField) // no modifications
            return true;

        const string unimodPrefix = "UNIMOD:";
        const string psimodPrefix = "MOD:";

        for (var i = 0; i < modStr.Length; i++)
        {
            // cpp parity: mzTabReader.cpp:275 — positionless mod inside [...] = skip PSM.
            if (modStr[i] == '[')
            {
                Verbosity.Warn("Ignoring PSM containing positionless modification");
                return false;
            }
            if (modStr[i] < '0' || modStr[i] > '9')
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "Invalid modification format on line {0}, expected position ({1})",
                    _lineNum, modStr));
            }

            // walk the digits of the position
            var start = i;
            while (i < modStr.Length && modStr[i] >= '0' && modStr[i] <= '9')
                i++;
            if (i >= modStr.Length)
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "Invalid modification format on line {0} ({1})", _lineNum, modStr));
            }

            var posStr = modStr.Substring(start, i - start);
            if (!int.TryParse(posStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pos))
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "Invalid mod position '{0}' on line {1} ({2})", posStr, _lineNum, modStr));
            }
            // cpp parity: mzTabReader.cpp:293 — clamp to [1, sequence-length].
            if (pos < 1)
                pos = 1;
            else if (pos > outSequence.Length)
                pos = outSequence.Length;

            // cpp parity: mzTabReader.cpp:298 — optional [parameter] block, skip past it.
            if (modStr[i] == '[')
            {
                while (i < modStr.Length && modStr[i] != ']')
                    i++;
                if (i++ >= modStr.Length)
                {
                    throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                        "Invalid modification format on line {0} ({1})", _lineNum, modStr));
                }
            }

            double mass;
            if (i >= modStr.Length || modStr[i] != '-')
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "Invalid modification format on line {0}, expected '-' ({1})", _lineNum, modStr));
            }
            if (++i >= modStr.Length)
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "Invalid modification format on line {0}, expected identifier ({1})", _lineNum, modStr));
            }

            if (modStr[i] == '[')
            {
                // cpp parity: mzTabReader.cpp:309 — neutral-loss cvParam block.
                while (i < modStr.Length && modStr[i] != ']')
                    i++;
                if (i++ >= modStr.Length)
                {
                    throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                        "Invalid modification format on line {0} ({1})", _lineNum, modStr));
                }
                if (i >= modStr.Length || modStr[i] != ',')
                {
                    throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                        "Invalid modification format on line {0}, expected ',' or end ({1})",
                        _lineNum, modStr));
                }
                // cpp parity: mzTabReader.cpp:317 — neutral losses are not implemented yet.
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "Neutral losses not yet supported. Line {0} ({1})", _lineNum, modStr));
            }
            else if (i + unimodPrefix.Length <= modStr.Length
                     && string.CompareOrdinal(modStr, i, unimodPrefix, 0, unimodPrefix.Length) == 0)
            {
                i += unimodPrefix.Length;
                start = i;
                while (i < modStr.Length && modStr[i] != ',')
                    i++;
                var idStr = modStr.Substring(start, i - start);
                if (!int.TryParse(idStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                {
                    throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                        "Invalid Unimod ID '{0}' on line {1} ({2})", idStr, _lineNum, modStr));
                }
                if (!_unimodMonoMass.TryGetValue(id, out mass))
                {
                    Verbosity.Warn(string.Format(CultureInfo.InvariantCulture,
                        "Unrecognized Unimod ID {0} on line {1}", id, _lineNum));
                    return false;
                }
            }
            else if (i + psimodPrefix.Length <= modStr.Length
                     && string.CompareOrdinal(modStr, i, psimodPrefix, 0, psimodPrefix.Length) == 0)
            {
                i += psimodPrefix.Length;
                start = i;
                while (i < modStr.Length && modStr[i] != ',')
                    i++;
                var idStr = modStr.Substring(start, i - start);
                if (!int.TryParse(idStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                        "Invalid PSI-MOD ID '{0}' on line {1} ({2})", idStr, _lineNum, modStr));
                }
                // cpp parity: mzTabReader.cpp:346 — PSI-MOD is not implemented yet.
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "PSI-MOD modifications not yet supported. Line {0} ({1})", _lineNum, modStr));
            }
            else
            {
                // cpp parity: mzTabReader.cpp:347 — falls through with `mass` uninitialised,
                // but in practice we should never reach here (cpp behaviour is undefined too).
                continue;
            }

            outMods.Add(new SeqMod(pos, mass));
        }
        return true;
    }

    /// <summary>
    /// Parse the <c>spectra_ref</c> column into (specFile, nativeID) pairs. Multiple refs
    /// are pipe-separated; each ref has the form <c>ms_run[#]:{nativeID}</c>.
    /// </summary>
    /// <remarks>cpp parity: mzTabReader.cpp:353.</remarks>
    private List<KeyValuePair<string, string>> ParseSpectrum(string[] fields)
    {
        const string runPrefix = "ms_run[";
        const string runSuffix = "]:";
        var specStr = fields[_psh[PsmSpecField]];

        var result = new List<KeyValuePair<string, string>>();
        var specs = specStr.Split('|');
        foreach (var spec in specs)
        {
            if (!spec.StartsWith(runPrefix, StringComparison.Ordinal))
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "Invalid spectrum reference '{0}' on line {1}, must begin with '{2}'",
                    spec, _lineNum, runPrefix));
            }
            var rest = spec.Substring(runPrefix.Length);
            var sep = rest.IndexOf(runSuffix, StringComparison.Ordinal);
            if (sep < 0)
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "Invalid spectrum reference '{0}' on line {1}", spec, _lineNum));
            }
            var runStr = rest.Substring(0, sep);
            if (!int.TryParse(runStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var run))
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "Invalid file number '{0}' in spectrum reference '{1}' on line {2}",
                    runStr, spec, _lineNum));
            }
            if (!_runs.TryGetValue(run, out var runFile))
            {
                throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                    "Unknown ms_run {0} in spectrum reference '{1}' on line {2}",
                    run, spec, _lineNum));
            }
            result.Add(new KeyValuePair<string, string>(runFile, rest.Substring(sep + runSuffix.Length)));
        }
        return result;
    }

    /// <summary>
    /// Parse the chosen score column. Returns false if the PSM was filtered out by the
    /// threshold (the caller should still bump <see cref="BuildParser.FilteredOutPsmCount"/>).
    /// </summary>
    /// <remarks>cpp parity: mzTabReader.cpp:397.</remarks>
    private bool TryParseScore(string[] fields, out double outScore)
    {
        var key = PsmScoreField + "[" + _scoreIdxFile.ToString(CultureInfo.InvariantCulture) + "]";
        if (!_psh.TryGetValue(key, out var col))
        {
            throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                "Missing score {0} on line {1}", _scoreIdxFile, _lineNum));
        }
        var raw = fields[col];
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out outScore))
        {
            throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                "Invalid score '{0}' on line {1}", raw, _lineNum));
        }

        var spec = _scoreTypes[_scoreIdxVector];
        var threshold = GetScoreThreshold(spec.BuildInput);
        if (spec.HigherIsBetter)
            return outScore >= threshold;
        return outScore <= threshold;
    }

    // --- helpers ------------------------------------------------------------------------

    /// <summary>
    /// Tab-split a line; strip a trailing CR for CRLF tolerance. mzTab is a plain TSV with
    /// no quoting rules, so no escape handling here.
    /// </summary>
    private static string[] SplitTabs(string line)
    {
        if (line.Length > 0 && line[^1] == '\r')
            line = line.Substring(0, line.Length - 1);
        return line.Split('\t');
    }

    /// <summary>
    /// Stream the unimod.xml file and populate <see cref="_unimodMonoMass"/> with one entry
    /// per <c>umod:mod</c> element (keyed by <c>record_id</c>, valued by the <c>umod:delta</c>
    /// child's <c>mono_mass</c>).
    /// </summary>
    /// <remarks>cpp parity: UnimodParser.cpp:61. We only need the (id -> mono_mass) map, so
    /// instead of a full SAX state machine we walk the elements with <see cref="XmlReader"/>:
    /// every <c>umod:mod</c> opens a record, the first nested <c>umod:delta</c> closes it.</remarks>
    private void LoadUnimod(string unimodPath)
    {
        if (!File.Exists(unimodPath))
        {
            Verbosity.Warn($"Unimod file not found at '{unimodPath}'. Unimod IDs cannot be resolved.");
            return;
        }

        try
        {
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                DtdProcessing = DtdProcessing.Ignore,
            };
            using var reader = XmlReader.Create(unimodPath, settings);

            int currentId = -1;
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;

                // cpp uses isElement which compares the QName verbatim; we match LocalName
                // to be namespace-agnostic ("umod:mod" -> LocalName "mod").
                if (reader.LocalName == "mod")
                {
                    var idAttr = reader.GetAttribute("record_id");
                    if (int.TryParse(idAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                        currentId = id;
                    else
                        currentId = -1;
                }
                else if (reader.LocalName == "delta" && currentId >= 0)
                {
                    var mass = reader.GetAttribute("mono_mass");
                    if (double.TryParse(mass, NumberStyles.Float, CultureInfo.InvariantCulture, out var m))
                        _unimodMonoMass[currentId] = m;
                    // Each umod:mod has exactly one umod:delta child; clear the id so any
                    // later delta inside this mod (none expected) doesn't overwrite the value.
                    currentId = -1;
                }
            }
        }
        catch (XmlException ex)
        {
            Verbosity.Warn($"Failed to parse unimod.xml: {ex.Message}");
        }
        catch (IOException ex)
        {
            Verbosity.Warn($"Failed to read unimod.xml: {ex.Message}");
        }
    }
}
