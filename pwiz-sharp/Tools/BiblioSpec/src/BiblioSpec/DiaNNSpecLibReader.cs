// Port of pwiz_tools/BiblioSpec/src/DiaNNSpecLibReader.{h,cpp}.
//
// DIA-NN ".speclib" is a custom little-endian binary serialization written by
// Vadim Demichev's diann.cpp. The format is:
//
//   header:
//     int32 version-or-genDecoys  (if value >= 0 it is gen_decoys and version=0,
//                                  else version<0 and the next int32 is gen_decoys)
//     [int32 gen_decoys if version<0]
//     int32 gen_charges
//     int32 infer_proteotypicity
//     string name           (length-prefixed: int32 byte-count + bytes)
//     string fasta_names
//     array<Isoform> proteins
//     array<PG>      protein_ids
//     strings        precursors      (vector<string>)
//     strings        names
//     strings        genes
//     double         iRT_min
//     double         iRT_max
//     array<Entry>   entries         (entries.size()==precursors.size())
//     [vector<int>   elution_groups] (if version <= -1 and bytes remain)
//
// Each Entry holds:
//     Peptide target
//     int32 has-decoy flag, optional decoy Peptide
//     int32 entry_flags
//     int32 proteotypic
//     int32 pid_index
//     string name           (precursor id)
//     [if version <= -3: float pg_qvalue, float ptm_qvalue, float site_conf]
//
// Each Peptide holds:
//     int32 index, charge, length
//     float mz, iRT, sRT
//     [if version <= -2: float lib_qvalue, iIM, sIM]
//     vector<Product> fragments        (Product = {float mz, float height, int8 charge,type,index,loss})
//
// "vector<T>" uses int32 length + raw bytes for POD; strings use int32 length + bytes;
// "array<T>" uses int32 length + per-element T.read(in, version).
//
// After loading the binary library, the reader looks for a sibling DIA-NN report
// (TSV or Parquet) sharing a leading filename prefix and matching the report's
// required headers ("Run", "File.Name", "Protein.Group", "Precursor.Id",
// "Global.Q.Value", "Q.Value", "RT", "RT.Start", "RT.Stop", "IM"). Best-PSM
// selection and the RetentionTimes table population happen at TSV read time.
//
// Notes on the C# port vs cpp:
//   - The DiaNN reader is one of two BiblioSpec readers that builds a non-redundant
//     library directly (cpp <c>NonRedundantPSM</c>); a custom <c>RetentionTimes</c>
//     SQLite table is created up-front and populated row-by-row from the report.
//   - Parquet sibling files are NOT supported in this port (no Apache Arrow
//     dependency). Speclibs that need a .parquet report throw a clear error.
//   - Modifications are resolved via the C# Unimod port (Pwiz.Data.Common.Unimod)
//     for "(UniMod:N)" tags; non-UniMod bracketed deltas are parsed as numbers.

using System.Data.SQLite;
using System.Globalization;
using System.Text;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Unimod;
using Pwiz.Util.Chemistry;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Parses DIA-NN <c>.speclib</c> binary library files and their companion DIA-NN report TSVs.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::DiaNNSpecLibReader</c> at
/// <c>pwiz_tools/BiblioSpec/src/DiaNNSpecLibReader.{h,cpp}</c>.</para>
/// <para>The cpp reader implements <see cref="ISpecFileReader"/> directly (peaks come
/// off each speclib entry, not an external file). The C# port mirrors that by setting
/// <see cref="BuildParser.SpecReader"/> to an inner <see cref="DiaNNSpecLibSpecReader"/>
/// that pulls peaks straight off the parsed entry.</para>
/// </remarks>
public sealed class DiaNNSpecLibReader : BuildParser
{
    // cpp parity: DiaNNSpecLibReader.cpp:34 — earliest format version this reader understands.
    // Lower (more negative) values are NEWER. The cpp logic supports up to and including -3.
    private const int LatestSupportedVersion = -3;

    // cpp parity: DiaNNSpecLibReader.cpp:274 — entry flag bits. Only fFromFasta is referenced by
    // the cpp parseFile but we keep all three names for parity with the cpp source.
    [Flags]
    internal enum EntryFlags
    {
        None = 0,
        FromFasta = 1 << 0,
        // cpp values for reference (unused by parseFile):
        // PredictedSpectrum = 1 << 1,
        // PredictedRT = 1 << 2,
    }

    private readonly string _specLibFile;
    private readonly Library _specLib = new();
    private SQLiteCommand? _insertRtCmd;
    private IonMobilityType _ionMobilityType = IonMobilityType.None;

    /// <summary>Returns true if <paramref name="path"/> ends with <c>.speclib</c> (case-insensitive).</summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith(".speclib", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Construct a DiaNNSpecLibReader bound to <paramref name="maker"/> and the speclib file at
    /// <paramref name="specLibFile"/>.
    /// </summary>
    /// <remarks>cpp parity: DiaNNSpecLibReader.cpp:954.</remarks>
    public DiaNNSpecLibReader(BlibBuilder maker, string specLibFile, ProgressIndicator? parentProgress)
        : base(maker, specLibFile, parentProgress)
    {
        Verbosity.Debug("Creating DiaNNSpecLibReader.");
        _specLibFile = specLibFile;

        if (!File.Exists(specLibFile))
            Verbosity.Error($"speclib {specLibFile} does not exist");

        SetSpecFileName(specLibFile, checkFile: false);
        LookUpBy = SpecIdType.IndexId;

        // cpp parity: DiaNNSpecLibReader.cpp:961 — point the spec reader at ourselves so peaks
        // come from the parsed speclib entry rather than an external spectrum file.
        SpecReader = new DiaNNSpecLibSpecReader(_specLib);

        // cpp parity: DiaNNSpecLibReader.cpp:606 — create the RetentionTimes table. The cpp
        // Impl ctor does this in score-lookup mode too (bailing out earlier); we follow the
        // cpp guard exactly.
        if (!maker.IsScoreLookupMode)
        {
            const string createRetentionTimes =
                "CREATE TABLE RetentionTimes (RefSpectraID INTEGER, " +
                "RedundantRefSpectraID INTEGER, " +
                "SpectrumSourceID INTEGER, " +
                "ionMobility REAL, " +
                "collisionalCrossSectionSqA REAL, " +
                "ionMobilityHighEnergyOffset REAL, " +
                "ionMobilityType TINYINT, " +
                "retentionTime REAL, " +
                "startTime REAL, " +
                "endTime REAL, " +
                "score REAL, " +
                "bestSpectrum INTEGER, " +
                "FOREIGN KEY(RefSpectraID) REFERENCES RefSpectra(id) )";
            maker.SqlStmt(createRetentionTimes);

            _insertRtCmd = maker.Db.CreateCommand();
            _insertRtCmd.CommandText =
                "INSERT INTO RetentionTimes (RefSpectraID, RedundantRefSpectraID, " +
                "SpectrumSourceID, ionMobility, collisionalCrossSectionSqA, " +
                "ionMobilityHighEnergyOffset, ionMobilityType, retentionTime, " +
                "startTime, endTime, score, bestSpectrum) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
            for (int i = 0; i < 12; i++)
                _insertRtCmd.Parameters.Add(_insertRtCmd.CreateParameter());
            _insertRtCmd.Prepare();
        }

        // cpp parity: DiaNNSpecLibReader.cpp:967 — must re-prepare after adding RetentionTimes.
        PrepareInsertSpectrumStatement();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _insertRtCmd?.Dispose();
            _insertRtCmd = null;
        }
        base.Dispose(disposing);
    }

    /// <summary>Score types this reader produces. cpp parity: DiaNNSpecLibReader.cpp:1297.</summary>
    public override IList<PsmScoreType> GetScoreTypes() =>
        new[] { PsmScoreType.GenericQValue };

    /// <summary>
    /// Parse the speclib binary, then the companion DIA-NN report TSV, then commit to the
    /// library. cpp parity: DiaNNSpecLibReader.cpp:975.
    /// </summary>
    public override bool ParseFile()
    {
        // --- 1) load the binary speclib ---------------------------------------------------
        using (var stream = new FileStream(_specLibFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new BinaryReader(stream))
        {
            _specLib.Read(reader);
        }
        Verbosity.Status($"Read {_specLib.Entries.Count} entries from speclib.");

        // --- 2) find the companion report -------------------------------------------------
        var diannReportFilepath = FindDiannReport(out var diannStatsFilepath);

        // --- 3) optionally parse stats.tsv for File.Name mapping --------------------------
        var diannFilenameByRun = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(diannStatsFilepath))
        {
            try
            {
                Verbosity.Debug($"Reading filenames from stats.tsv: {diannStatsFilepath}");
                using var sr = new StreamReader(diannStatsFilepath);
                var header = sr.ReadLine();
                if (header is null)
                    throw new BlibException(false, $"stats file '{diannStatsFilepath}' is empty");

                var hdrCols = header.Split('\t');
                int fileNameCol = Array.IndexOf(hdrCols, "File.Name");
                if (fileNameCol < 0)
                    throw new BlibException(false, "stats file is missing 'File.Name' column");

                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Length == 0) break;
                    var cols = line.Split('\t');
                    if (fileNameCol >= cols.Length) continue;
                    var fileName = cols[fileNameCol];
                    string runName = Path.GetFileNameWithoutExtension(fileName);
                    string fnOnly = Path.GetFileName(fileName);
                    diannFilenameByRun[runName] = fnOnly;
                    Verbosity.Debug($"{runName} -> {fnOnly}");
                }
            }
            catch (Exception e) when (e is not BlibException)
            {
                Verbosity.Warn($"error reading filepaths from stats report \"{diannStatsFilepath}\": {e.Message}");
            }
        }

        // --- 4) parse the DIA-NN report TSV -----------------------------------------------
        // cpp parity: DiaNNSpecLibReader.cpp:1160 — pre-create PSMs from the speclib entries
        // with score=2 (sentinel "not yet added"); we'll flip them to real q-values as report
        // rows come in.
        var psmByPrecursorId = new Dictionary<string, NonRedundantPSM>(StringComparer.Ordinal);
        Verbosity.Debug($"Creating PSMs for {_specLib.Entries.Count} speclib entries.");
        foreach (var entry in _specLib.Entries)
        {
            var psm = new NonRedundantPSM
            {
                Charge = entry.Target.Charge,
                SpecIndex = entry.Target.Index,
                Score = 2.0, // sentinel: not yet added
            };
            psm.UnmodSeq = ParseSequenceAndMods(entry.Name, psm.Mods);
            psmByPrecursorId[entry.Name] = psm;
        }
        Verbosity.Debug("Finished creating PSMs.");

        // cpp parity: DiaNNSpecLibReader.cpp:1158 uses std::map<string, list<RtPSM>>, which
        // iterates in key-sorted order. The .check golden's RetentionTimes rows depend on this
        // sort order, so we use SortedDictionary in C# to match.
        var retentionTimesByPrecursorId = new SortedDictionary<string, List<RtPsm>>(StringComparer.Ordinal);
        long redundantPsmCount = 0;
        string firstRun = string.Empty;
        double scoreThreshold = GetScoreThreshold(BuildInput.GenericQValueInput);

        using (var sr = new StreamReader(diannReportFilepath))
        {
            var header = sr.ReadLine()
                ?? throw new BlibException(false, $"DIA-NN report '{diannReportFilepath}' is empty");
            var hdrCols = header.Split('\t');

            // cpp parity: DiaNNSpecLibReader.cpp:1143 — DIANN v2 parquet doesn't provide
            // File.Name; falls back to Run. The TSV always has File.Name; we mirror by
            // probing for File.Name first.
            int colRun = MustFindColumn(hdrCols, "Run", diannReportFilepath);
            int colFileName = Array.IndexOf(hdrCols, "File.Name");
            if (colFileName < 0) colFileName = colRun;
            int colProteinGroup = MustFindColumn(hdrCols, "Protein.Group", diannReportFilepath);
            int colPrecursorId = MustFindColumn(hdrCols, "Precursor.Id", diannReportFilepath);
            int colGlobalQValue = MustFindColumn(hdrCols, "Global.Q.Value", diannReportFilepath);
            int colQValue = MustFindColumn(hdrCols, "Q.Value", diannReportFilepath);
            int colRt = MustFindColumn(hdrCols, "RT", diannReportFilepath);
            int colRtStart = MustFindColumn(hdrCols, "RT.Start", diannReportFilepath);
            int colRtStop = MustFindColumn(hdrCols, "RT.Stop", diannReportFilepath);
            int colIm = MustFindColumn(hdrCols, "IM", diannReportFilepath);

            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                var cols = line.Split('\t');
                if (cols.Length <= Math.Max(colIm, Math.Max(colRtStop, colGlobalQValue)))
                    continue;

                string fileName = cols[colFileName];
                string proteinGrp = cols[colProteinGroup];
                string precursorIdStr = cols[colPrecursorId];
                float globalQValue = ParseFloat(cols[colGlobalQValue]);
                float qValue = ParseFloat(cols[colQValue]);
                float rt = ParseFloat(cols[colRt]);
                float rtStart = ParseFloat(cols[colRtStart]);
                float rtEnd = ParseFloat(cols[colRtStop]);
                float im = ParseFloat(cols[colIm]);

                if (!psmByPrecursorId.TryGetValue(precursorIdStr, out var psm))
                {
                    // cpp parity: DiaNNSpecLibReader.cpp:1181 — silently skip contaminants.
                    if (proteinGrp.StartsWith("contaminant_", StringComparison.Ordinal))
                        continue;

                    throw new BlibException(false,
                        $"could not find precursorId '{precursorIdStr}' in speclib; is " +
                        $"'{Path.GetFileName(diannReportFilepath)}' the correct report TSV file?");
                }

                // cpp parity: DiaNNSpecLibReader.cpp:1187 — normalise slashes then strip .dia suffix.
                string currentRunFilepath = fileName.Replace('\\', '/');
                string currentRunFilename = Path.GetFileName(currentRunFilepath);
                if (currentRunFilename.EndsWith(".dia", StringComparison.OrdinalIgnoreCase))
                    currentRunFilename = currentRunFilename.Substring(0, currentRunFilename.Length - 4);
                if (firstRun.Length == 0)
                    firstRun = currentRunFilename;

                bool rowPassesFilter = globalQValue <= scoreThreshold;

                if (!retentionTimesByPrecursorId.TryGetValue(precursorIdStr, out var rtList))
                {
                    rtList = new List<RtPsm>();
                    retentionTimesByPrecursorId[precursorIdStr] = rtList;
                }

                if (rowPassesFilter)
                {
                    string spectrumFilepath = currentRunFilename;
                    if (diannFilenameByRun.TryGetValue(currentRunFilename, out var mapped))
                        spectrumFilepath = mapped;

                    var psmRecord = new RtPsm
                    {
                        Rt = rt,
                        RtStart = rtStart,
                        RtEnd = rtEnd,
                        Score = qValue,
                        IonMobility = im,
                        FileId = (int)InsertSpectrumFilename(spectrumFilepath, insertAsIs: true, WorkflowType.Dia),
                    };
                    rtList.Add(psmRecord);
                    ++redundantPsmCount;
                }

                // cpp parity: DiaNNSpecLibReader.cpp:1212 — first row added to PSM list when
                // score is still the sentinel value of 2.
                if (rowPassesFilter && psm.Score == 2.0)
                {
                    Psms.Add(psm);
                    if (im > 0)
                        _ionMobilityType = IonMobilityType.InverseReducedVsecPerCm2;
                }

                if (rowPassesFilter && globalQValue < psm.Score)
                {
                    if (!_specLib.EntryByName.TryGetValue(precursorIdStr, out var entry))
                    {
                        throw new BlibException(false,
                            $"could not find precursorId '{precursorIdStr}' in speclib; is " +
                            $"'{Path.GetFileName(diannReportFilepath)}' the correct report TSV file?");
                    }

                    psm.Score = globalQValue;
                    psm.FileId = rtList[rtList.Count - 1].FileId;

                    entry.BestQValue = globalQValue;
                    entry.BestPsm = rtList[rtList.Count - 1];
                }
            }
        }

        FilteredOutPsmCount = _specLib.Entries.Count - Psms.Count;

        // cpp parity: DiaNNSpecLibReader.cpp:1242 — populate the RetentionTimes table.
        Verbosity.Status($"Building retention time table with {redundantPsmCount.ToString(CultureInfo.InvariantCulture)} entries.");
        BlibMaker.BeginTransaction();
        foreach (var kvp in retentionTimesByPrecursorId)
        {
            var precursorIdStr = kvp.Key;
            var redundantPsms = kvp.Value;
            if (!_specLib.EntryByName.TryGetValue(precursorIdStr, out var entry)) continue;

            foreach (var psm in redundantPsms)
            {
                int bestSpectrum = entry.BestPsm != null && entry.BestPsm.FileId == psm.FileId ? 1 : 0;
                BindAndExecRtInsert(entry.Target.Index, psm, bestSpectrum);
            }

            if (psmByPrecursorId.TryGetValue(precursorIdStr, out var nrpsm))
                nrpsm.Copies = redundantPsms.Count;
        }
        BlibMaker.EndTransaction();

        Verbosity.Status($"Reading {Psms.Count.ToString(CultureInfo.InvariantCulture)} spectra from speclib.");
        SetSpecFileName(firstRun, checkFile: false);
        BuildTables(PsmScoreType.GenericQValue);

        // cpp parity: DiaNNSpecLibReader.cpp:1289 — remap RetentionTimes.RefSpectraID after
        // duplicate filtering reorders RefSpectra rows.
        BlibMaker.SqlStmt(
            "UPDATE RetentionTimes SET RefSpectraID = newId " +
            "FROM (SELECT DISTINCT t.[RefSpectraID] as oldId, s.[id] as newId " +
            "FROM RefSpectra s, RetentionTimes t WHERE s.[SpecIdInFile] = t.[RefSpectraID]) AS IdMapping " +
            "WHERE RefSpectraID == IdMapping.oldId");

        return true;
    }

    // --- sibling-report discovery -------------------------------------------------------

    /// <summary>
    /// Locate the DIA-NN report TSV that accompanies the speclib. cpp parity:
    /// DiaNNSpecLibReader.cpp:1014.
    /// </summary>
    private string FindDiannReport(out string statsFilepath)
    {
        statsFilepath = string.Empty;
        string specLibFile = _specLibFile;
        string parentDir = Path.GetDirectoryName(specLibFile) ?? string.Empty;
        if (parentDir.Length == 0) parentDir = ".";

        // cpp parity: DiaNNSpecLibReader.cpp:1014-1018 — recognise the canonical name patterns.
        string diannReportFilepath = ReplaceLast(specLibFile, "-lib.tsv.speclib", "-report.tsv");
        if (diannReportFilepath == specLibFile)
            diannReportFilepath = ReplaceLast(specLibFile, "-lib.parquet.skyline.speclib", ".parquet");
        if (diannReportFilepath == specLibFile)
            diannReportFilepath = ReplaceLast(specLibFile, "-lib.skyline.speclib", ".parquet");

        // cpp parity: DiaNNSpecLibReader.cpp:1021 — FragPipe special-case.
        string specLibFilename = Path.GetFileName(specLibFile);
        if (specLibFilename == "library.tsv.speclib"
            || specLibFilename == "library.tsv.skyline.speclib"
            || specLibFilename == "lib.predicted.speclib")
        {
            foreach (var filename in new[] { "diann-output.parquet", "report.parquet",
                                             "diann-output.tsv", "report.tsv" })
            {
                var fragpipeDiannReport = Path.Combine(parentDir, "diann-output", filename);
                if (File.Exists(fragpipeDiannReport))
                {
                    Verbosity.Debug("Found DIA-NN tsv/speclib from FragPipe.");
                    diannReportFilepath = fragpipeDiannReport;
                    break;
                }
            }
        }

        if (diannReportFilepath == specLibFile || !File.Exists(diannReportFilepath))
        {
            // cpp parity: DiaNNSpecLibReader.cpp:1045 — scan all *.tsv siblings, ordered by
            // shared filename prefix length (greater = better match), and pick the first
            // file with the required report headers.
            var siblingFiles = new List<string>();
            try
            {
                siblingFiles.AddRange(Directory.GetFiles(parentDir, "*.parquet"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            try
            {
                siblingFiles.AddRange(Directory.GetFiles(parentDir, "*.tsv"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

            var bySharedPrefix = new SortedDictionary<int, List<string>>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
            foreach (var tsvFilepath in siblingFiles)
            {
                if (!File.Exists(tsvFilepath)) continue;
                string tsvFilename = Path.GetFileName(tsvFilepath);

                // cpp parity: DiaNNSpecLibReader.cpp:1059 — skip first-pass files unless the
                // speclib is itself first-pass.
                if (tsvFilename.Contains("first-pass", StringComparison.Ordinal)
                    && !specLibFilename.Contains("first-pass", StringComparison.Ordinal))
                    continue;
                if (tsvFilepath.EndsWith(".stats.tsv", StringComparison.Ordinal))
                {
                    statsFilepath = tsvFilepath;
                    continue;
                }

                int sharedPrefixLength = 0;
                for (int i = 0; i < tsvFilename.Length && i < specLibFilename.Length; i++)
                {
                    if (tsvFilename[i] != specLibFilename[i]) break;
                    sharedPrefixLength++;
                }
                if (!bySharedPrefix.TryGetValue(sharedPrefixLength, out var bucket))
                {
                    bucket = new List<string>();
                    bySharedPrefix[sharedPrefixLength] = bucket;
                }
                bucket.Add(tsvFilepath);
            }

            diannReportFilepath = string.Empty;
            foreach (var bucket in bySharedPrefix.Values)
            {
                foreach (var candidate in bucket)
                {
                    if (HasRequiredHeaders(candidate))
                    {
                        diannReportFilepath = candidate;
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(diannReportFilepath)) break;
            }

            if (string.IsNullOrEmpty(diannReportFilepath))
            {
                throw new BlibException(true,
                    $"unable to determine DIA-NN report filename for '{specLibFile}': the Parquet or TSV report " +
                    "is required to read speclib files and must be in the same directory as the speclib and " +
                    "share some leading characters (e.g. somedata-tsv.speclib and somedata-report.parquet)");
            }
        }
        else
        {
            // cpp parity: DiaNNSpecLibReader.cpp:1099 — still try to locate a *.stats.tsv.
            try
            {
                foreach (var candidate in Directory.GetFiles(parentDir, "*.stats.tsv"))
                {
                    string fn = Path.GetFileName(candidate);
                    if (fn.Contains("first-pass", StringComparison.Ordinal)
                        && !diannReportFilepath.Contains("first-pass", StringComparison.Ordinal))
                        continue;
                    statsFilepath = candidate;
                    break;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        // cpp parity: parquet reports require Apache Arrow; we don't have that in the C# port.
        if (diannReportFilepath.EndsWith(".parquet", StringComparison.OrdinalIgnoreCase))
        {
            throw new BlibException(true,
                $"DIA-NN parquet reports are not supported in the C# port (file '{diannReportFilepath}'); " +
                "convert the report to TSV before running BlibBuild.");
        }

        Verbosity.Debug($"Opened report file {diannReportFilepath}.");
        return diannReportFilepath;
    }

    private static bool HasRequiredHeaders(string tsvFilepath)
    {
        try
        {
            using var sr = new StreamReader(tsvFilepath);
            var header = sr.ReadLine();
            if (header is null) return false;
            var cols = header.Split('\t');
            return Array.IndexOf(cols, "Precursor.Id") >= 0
                && Array.IndexOf(cols, "Global.Q.Value") >= 0
                && Array.IndexOf(cols, "RT") >= 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static int MustFindColumn(string[] cols, string name, string filepath)
    {
        int idx = Array.IndexOf(cols, name);
        if (idx < 0)
            throw new BlibException(false,
                $"DIA-NN report '{Path.GetFileName(filepath)}' is missing required column '{name}'");
        return idx;
    }

    private static string ReplaceLast(string source, string oldValue, string newValue)
    {
        int idx = source.LastIndexOf(oldValue, StringComparison.Ordinal);
        if (idx < 0) return source;
        return string.Concat(source.AsSpan(0, idx), newValue, source.AsSpan(idx + oldValue.Length));
    }

    /// <summary>
    /// Float parser that mirrors the cpp DIA-NN reader's CSV library (csv.h's
    /// <c>parse_float&lt;float&gt;</c>) digit-by-digit accumulator. .NET's
    /// <see cref="float.Parse(string, NumberStyles, IFormatProvider)"/> uses double-precision
    /// internally and rounds to the nearest float, which produces values one ULP off from cpp
    /// at the 7th decimal place — observable in the RT / RT.Start / RT.Stop golden columns.
    /// We replicate the cpp accumulator exactly so the .blib row-text comparisons match.
    /// </summary>
    internal static float ParseFloat(string v)
    {
        if (string.IsNullOrEmpty(v)) return 0f;

        int i = 0;
        bool isNeg = false;
        if (v[i] == '-') { isNeg = true; i++; }
        else if (v[i] == '+') { i++; }

        float x = 0f;
        while (i < v.Length && v[i] >= '0' && v[i] <= '9')
        {
            x = x * 10f + (v[i] - '0');
            i++;
        }
        if (i < v.Length && (v[i] == '.' || v[i] == ','))
        {
            i++;
            float pos = 1f;
            while (i < v.Length && v[i] >= '0' && v[i] <= '9')
            {
                pos /= 10f;
                x += (v[i] - '0') * pos;
                i++;
            }
        }
        if (i < v.Length && (v[i] == 'e' || v[i] == 'E'))
        {
            i++;
            int eSign = 1;
            if (i < v.Length && v[i] == '-') { eSign = -1; i++; }
            else if (i < v.Length && v[i] == '+') { i++; }
            int e = 0;
            while (i < v.Length && v[i] >= '0' && v[i] <= '9')
            {
                e = e * 10 + (v[i] - '0');
                i++;
            }
            if (eSign < 0) e = -e;

            // cpp parity: csv.h uses exponentiation-by-squaring on base 0.1f / 10f. The
            // float-precision path through (base*base) reductions gives a different rounding
            // than a naive (x /= 10) loop — that difference is observable in the .blib's
            // RetentionTimes.score column (e.g. 1.63792037e-06 vs 1.63792014e-06).
            if (e != 0)
            {
                float fbase;
                if (e < 0)
                {
                    fbase = 0.1f;
                    e = -e;
                }
                else
                {
                    fbase = 10f;
                }
                while (e != 1)
                {
                    if ((e & 1) == 0)
                    {
                        fbase = fbase * fbase;
                        e >>= 1;
                    }
                    else
                    {
                        x *= fbase;
                        --e;
                    }
                }
                x *= fbase;
            }
        }
        return isNeg ? -x : x;
    }

    // --- modification parsing -----------------------------------------------------------

    /// <summary>
    /// Strip modifications from a DIA-NN modified-sequence name, populating
    /// <paramref name="mods"/> with the resolved (position, deltaMass) pairs. cpp parity:
    /// DiaNNSpecLibReader.cpp:193 <c>get_aas</c>.
    /// </summary>
    private static string ParseSequenceAndMods(string name, List<SeqMod> mods)
    {
        const string modPrefix = "(UniMod:";
        const int modPrefixLength = 8;

        var result = new StringBuilder();
        int j = 0;
        int i = 0;
        while (i < name.Length)
        {
            char symbol = name[i];
            if (symbol < 'A' || symbol > 'Z')
            {
                if (symbol != '(' && symbol != '[')
                {
                    i++;
                    continue;
                }

                int end = ClosingBracket(name, symbol, i);
                int position = Math.Max(1, j);

                // cpp parity: DiaNNSpecLibReader.cpp:206 — check "(UniMod:" prefix at i.
                bool isUniMod = symbol == '(' && i + modPrefixLength <= name.Length
                    && string.CompareOrdinal(name, i, modPrefix, 0, modPrefixLength) == 0;

                if (!isUniMod)
                {
                    // cpp parity: DiaNNSpecLibReader.cpp:208 — treat as a delta-mass literal.
                    string potentialMass = name.Substring(i + 1, end - i - 1);
                    if (potentialMass.IndexOfAny(_nonNumericMod) >= 0)
                        throw new BlibException(false,
                            $"unable to handle mod in library entry as either a UniMod id or " +
                            $"delta mass: {potentialMass} in {name}");
                    double modMass = double.Parse(potentialMass, NumberStyles.Float, CultureInfo.InvariantCulture);
                    AddOrMergeMod(mods, position, modMass);
                }
                else
                {
                    int idStart = i + modPrefixLength;
                    string modIdStr = name.Substring(idStart, end - idStart);
                    int unimodId = int.Parse(modIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    AddOrMergeMod(mods, position, GetUnimodDeltaMass(unimodId));
                }

                i = end + 1;
                continue;
            }

            ++j;
            result.Append(symbol);
            i++;
        }
        return result.ToString();
    }

    private static readonly char[] _nonNumericMod = { 'a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',
                                                       'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
                                                       ' ','\t',';',',','/','\\','+','-','(',')','[',']','{','}','<','>','=','!','@','#','$','%','^','&','*' };

    // Cache of UniMod id → monoisotopic delta mass computed from the composition string.
    // cpp parity: pwiz <c>unimod::modification(...).deltaMonoisotopicMass()</c> recomputes from
    // the composition (e.g. "H(2) C(2) O") via Composition::monoisotopicMass(); the C# Unimod
    // port stores the obo's text <c>delta_mono_mass</c> verbatim, which is rounded to 6 decimal
    // places and therefore loses precision in sums (Acetyl + Carbamidomethyl differs at the
    // 7th decimal place). We restore byte-for-byte parity by parsing the composition through
    // the C# Formula type which uses the same element-mass table as cpp pwiz.
    private static readonly Dictionary<int, double> _unimodMassCache = new();

    private static double GetUnimodDeltaMass(int unimodId)
    {
        if (_unimodMassCache.TryGetValue(unimodId, out var cached))
            return cached;

        var mod = Unimod.Modification((CVID)((int)CVID.UNIMOD_unimod_root_node + unimodId))
            ?? throw new BlibException(false,
                $"UniMod:{unimodId.ToString(CultureInfo.InvariantCulture)} not found in Unimod database");

        // cpp delta_composition format is "H(2) C(2) O", which our Formula parser handles after
        // stripping parens around counts.
        var compStr = mod.DeltaComposition;
        double mass;
        if (string.IsNullOrWhiteSpace(compStr))
        {
            mass = mod.DeltaMonoisotopicMass;
        }
        else
        {
            try
            {
                mass = ComputeCompositionMass(compStr);
            }
            catch
            {
                // Fall back to the obo's text mass if the composition can't be parsed (e.g.
                // labelled isotopes or unsupported brick syntax).
                mass = mod.DeltaMonoisotopicMass;
            }
        }
        _unimodMassCache[unimodId] = mass;
        return mass;
    }

    /// <summary>
    /// Compute the monoisotopic mass of a Unimod composition string, handling both elemental
    /// terms (e.g. <c>"H(2) C(2) O"</c>) and brick aliases (e.g. <c>"Hex(9) HexNAc(2)"</c>).
    /// cpp parity: <c>Unimod.cpp</c> defines a brick table that maps short names to elemental
    /// formulas; we replicate the same table so summed masses match cpp byte-for-byte.
    /// </summary>
    private static double ComputeCompositionMass(string composition)
    {
        var formula = new Formula();
        int i = 0;
        while (i < composition.Length)
        {
            // skip whitespace
            while (i < composition.Length && char.IsWhiteSpace(composition[i])) i++;
            if (i >= composition.Length) break;

            // parse term name: continue until '(' or whitespace
            int nameStart = i;
            while (i < composition.Length && composition[i] != '(' && !char.IsWhiteSpace(composition[i]))
                i++;
            string name = composition[nameStart..i];

            // optional "(count)"
            int count = 1;
            while (i < composition.Length && char.IsWhiteSpace(composition[i])) i++;
            if (i < composition.Length && composition[i] == '(')
            {
                int countStart = i + 1;
                int closeIdx = composition.IndexOf(')', countStart);
                if (closeIdx < 0)
                    throw new FormatException($"Unbalanced '(' in composition: {composition}");
                count = int.Parse(composition[countStart..closeIdx], NumberStyles.Integer, CultureInfo.InvariantCulture);
                i = closeIdx + 1;
            }

            if (_brickFormulae.TryGetValue(name, out var brickStr))
            {
                var brick = new Formula(brickStr);
                formula += brick * count;
            }
            else
            {
                // treat as a regular element symbol (single-letter or two-letter)
                formula += new Formula(name + count.ToString(CultureInfo.InvariantCulture));
            }
        }
        return formula.MonoisotopicMass;
    }

    // cpp parity: Unimod.cpp brick table.
    private static readonly Dictionary<string, string> _brickFormulae = new(StringComparer.Ordinal)
    {
        ["Hex"] = "H10 C6 O5",
        ["HexN"] = "H11 C6 O4 N1",
        ["HexNAc"] = "H13 C8 N1 O5",
        ["dHex"] = "C6 H10 O4",
        ["HexA"] = "C6 H8 O6",
        ["Kdn"] = "C9 H14 O8",
        ["Kdo"] = "C8 H12 O7",
        ["NeuAc"] = "C11 H17 N1 O8",
        ["NeuGc"] = "C11 H17 N1 O9",
        ["Pent"] = "C5 H8 O4",
        ["Water"] = "H2 O1",
        ["Phos"] = "H1 P1 O3",
        ["Sulf"] = "S1 O3",
        ["Hep"] = "C7 H12 O6",
        ["Me"] = "C1 H2",
        ["13C"] = "_13C1",
        ["2H"] = "_2H1",
        ["18O"] = "_18O1",
        ["15N"] = "_15N1",
    };

    private static void AddOrMergeMod(List<SeqMod> mods, int position, double deltaMass)
    {
        if (mods.Count > 0 && mods[mods.Count - 1].Position == position)
            mods[mods.Count - 1] = new SeqMod(position, mods[mods.Count - 1].DeltaMass + deltaMass);
        else
            mods.Add(new SeqMod(position, deltaMass));
    }

    private static int ClosingBracket(string name, char symbol, int pos)
    {
        char close = symbol == '(' ? ')' : ']';
        int par = 1;
        int end;
        for (end = pos + 1; end < name.Length; end++)
        {
            char s = name[end];
            if (s == close)
            {
                par--;
                if (par == 0) break;
            }
            else if (s == symbol)
            {
                par++;
            }
        }
        return end;
    }

    // --- RetentionTimes insert ----------------------------------------------------------

    private void BindAndExecRtInsert(int refSpectraId, RtPsm psm, int bestSpectrum)
    {
        if (_insertRtCmd is null)
            throw new InvalidOperationException("RetentionTimes INSERT statement not prepared.");
        var p = _insertRtCmd.Parameters;
        // Column order: RefSpectraID, RedundantRefSpectraID, SpectrumSourceID, ionMobility,
        // collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType,
        // retentionTime, startTime, endTime, score, bestSpectrum.
        p[0].Value = refSpectraId;
        p[1].Value = 0;
        p[2].Value = psm.FileId;
        p[3].Value = (double)psm.IonMobility;
        p[4].Value = 0.0;
        p[5].Value = 0.0;
        p[6].Value = (int)(psm.IonMobility > 0 ? _ionMobilityType : IonMobilityType.None);
        p[7].Value = (double)psm.Rt;
        p[8].Value = (double)psm.RtStart;
        p[9].Value = (double)psm.RtEnd;
        p[10].Value = (double)psm.Score;
        p[11].Value = bestSpectrum;
        _insertRtCmd.ExecuteNonQuery();
    }

    // --- internal types -----------------------------------------------------------------

    /// <summary>cpp parity: anonymous <c>RtPSM</c> struct at DiaNNSpecLibReader.cpp:40.</summary>
    internal sealed class RtPsm
    {
        public float Rt;
        public float RtStart;
        public float RtEnd;
        public int FileId;
        public float Score;
        public float IonMobility;
    }

    /// <summary>cpp parity: <c>Product</c> class at DiaNNSpecLibReader.cpp:278.</summary>
    internal sealed class Product
    {
        public float Mz;
        public float Height;
        // charge, type, index, loss are 1-byte fields in the cpp Product; we only need mz+height.

        public const int CppSizeofBytes = 4 /*mz*/ + 4 /*height*/ + 4 /*4 chars padded to 4*/;
    }

    /// <summary>cpp parity: <c>Peptide</c> class at DiaNNSpecLibReader.cpp:308.</summary>
    internal sealed class Peptide
    {
        public int Index;
        public int Charge;
        public int Length;
        public float Mz;
        public float IRT;
        public float SRT;
        public float LibQValue;
        public float IIM;
        public float SIM;
        public List<Product> Fragments { get; } = new();

        public void Read(BinaryReader r, int version)
        {
            Index = r.ReadInt32();
            Charge = r.ReadInt32();
            Length = r.ReadInt32();
            Mz = r.ReadSingle();
            IRT = r.ReadSingle();
            SRT = r.ReadSingle();
            if (version <= -2)
            {
                LibQValue = r.ReadSingle();
                IIM = r.ReadSingle();
                SIM = r.ReadSingle();
            }

            // cpp parity: read_vector<Product> — int32 count + raw bytes. We deserialise each
            // Product as 4 (mz) + 4 (height) + 4 (char charge,type,index,loss) bytes.
            int count = r.ReadInt32();
            Fragments.Capacity = count;
            for (int i = 0; i < count; i++)
            {
                var p = new Product
                {
                    Mz = r.ReadSingle(),
                    Height = r.ReadSingle(),
                };
                // skip the four 1-byte trailers
                r.ReadByte(); r.ReadByte(); r.ReadByte(); r.ReadByte();
                Fragments.Add(p);
            }
        }
    }

    /// <summary>cpp parity: <c>Isoform</c> class at DiaNNSpecLibReader.cpp:347.</summary>
    internal sealed class Isoform
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Gene = string.Empty;

        public void Read(BinaryReader r, int version)
        {
            _ = version;
            int sp = r.ReadInt32();
            _ = sp;
            int size = r.ReadInt32();

            Id = ReadString(r);
            Name = ReadString(r);
            Gene = ReadString(r);
            r.ReadInt32(); // name_index
            r.ReadInt32(); // gene_index
            for (int i = 0; i < size; i++)
            {
                r.ReadInt32(); // precursor index
            }
        }
    }

    /// <summary>cpp parity: <c>PG</c> class at DiaNNSpecLibReader.cpp:387.</summary>
    internal sealed class PG
    {
        public string Ids = string.Empty;

        public void Read(BinaryReader r, int version)
        {
            _ = version;
            int sizeP = r.ReadInt32();

            Ids = ReadString(r);
            ReadString(r); // names
            ReadString(r); // genes
            ReadIntVector(r); // name_indices
            ReadIntVector(r); // gene_indices
            ReadIntVector(r); // precursors

            for (int i = 0; i < sizeP; i++)
                r.ReadInt32();
        }
    }

    /// <summary>cpp parity: <c>Library::Entry</c> class at DiaNNSpecLibReader.cpp:443.</summary>
    internal sealed class Entry
    {
        public readonly Peptide Target = new();
        public readonly Peptide Decoy = new();
        public bool HasDecoy;
        public EntryFlags EntryFlags;
        public int Proteotypic;
        public int PidIndex;
        public string Name = string.Empty;
        public float PgQValue;
        public float PtmQValue;
        public float SiteConf;

        // temporary state shared with parseFile
        public float BestQValue;
        public RtPsm? BestPsm;

        public void Read(BinaryReader r, int version)
        {
            Target.Read(r, version);
            int dc = r.ReadInt32();
            HasDecoy = dc != 0;
            if (HasDecoy) Decoy.Read(r, version);

            int ff = r.ReadInt32();
            int prt = r.ReadInt32();
            EntryFlags = (EntryFlags)ff;
            Proteotypic = prt;
            PidIndex = r.ReadInt32();
            Name = ReadString(r);
            if (version <= -3)
            {
                PgQValue = r.ReadSingle();
                PtmQValue = r.ReadSingle();
                SiteConf = r.ReadSingle();
            }
        }
    }

    /// <summary>cpp parity: <c>Library</c> class at DiaNNSpecLibReader.cpp:422.</summary>
    internal sealed class Library
    {
        public string Name = string.Empty;
        public string FastaNames = string.Empty;
        public double IRtMin;
        public double IRtMax;
        public bool GenDecoys;
        public bool GenCharges;
        public bool InferProteotypicity;
        public List<Isoform> Proteins { get; } = new();
        public List<PG> ProteinIds { get; } = new();
        public List<string> Precursors { get; } = new();
        public List<string> Names { get; } = new();
        public List<string> Genes { get; } = new();
        public List<Entry> Entries { get; } = new();
        public Dictionary<string, Entry> EntryByName { get; } = new(StringComparer.Ordinal);

        public void Read(BinaryReader r)
        {
            int gd = 0;
            int version = r.ReadInt32();
            if (version >= 0)
            {
                gd = version;
                version = 0;
            }
            else
            {
                gd = r.ReadInt32();
            }

            if (version < LatestSupportedVersion)
                Verbosity.Error($"speclib file has version {(-version).ToString(CultureInfo.InvariantCulture)}, " +
                    $"but BiblioSpec only supports up to version {(-LatestSupportedVersion).ToString(CultureInfo.InvariantCulture)}");
            else
                Verbosity.Debug($"speclib file has version {(-version).ToString(CultureInfo.InvariantCulture)}");

            int gc = r.ReadInt32();
            int ip = r.ReadInt32();
            GenDecoys = gd != 0;
            GenCharges = gc != 0;
            InferProteotypicity = ip != 0;

            Name = ReadString(r);
            FastaNames = ReadString(r);

            ReadArray(r, Proteins, version, (rd, v) => { var x = new Isoform(); x.Read(rd, v); return x; });
            ReadArray(r, ProteinIds, version, (rd, v) => { var x = new PG(); x.Read(rd, v); return x; });

            ReadStrings(r, Precursors);
            ReadStrings(r, Names);
            ReadStrings(r, Genes);

            IRtMin = r.ReadDouble();
            IRtMax = r.ReadDouble();

            ReadArray(r, Entries, version, (rd, v) => { var x = new Entry(); x.Read(rd, v); return x; });

            // cpp parity: DiaNNSpecLibReader.cpp:512 — precursor names should line up with entries.
            for (int i = 0; i < Entries.Count; i++)
            {
                if (i < Precursors.Count && Entries[i].Name != Precursors[i])
                {
                    Verbosity.Error($"Precursor mismatch between {Entries[i].Name} and {Precursors[i]} in speclib file");
                }
                EntryByName[Entries[i].Name] = Entries[i];
            }

            // cpp parity: DiaNNSpecLibReader.cpp:523 — optional elution_groups vector tail.
            if (version <= -1 && r.BaseStream.Position < r.BaseStream.Length)
            {
                int egCount = r.ReadInt32();
                r.BaseStream.Seek((long)egCount * sizeof(int), SeekOrigin.Current);
            }
        }
    }

    // cpp parity: DiaNNSpecLibReader.cpp:56 read_string.
    private static string ReadString(BinaryReader r)
    {
        int size = r.ReadInt32();
        if (size <= 0) return string.Empty;
        var bytes = r.ReadBytes(size);
        // cpp uses std::string which is byte-based; DIA-NN stores ASCII/UTF-8 fields.
        return Encoding.UTF8.GetString(bytes);
    }

    // cpp parity: DiaNNSpecLibReader.cpp:64 read_array<T>.
    private static void ReadArray<T>(BinaryReader r, List<T> dst, int version, Func<BinaryReader, int, T> factory)
    {
        int size = r.ReadInt32();
        if (size <= 0) return;
        dst.Capacity = size;
        for (int i = 0; i < size; i++)
            dst.Add(factory(r, version));
    }

    // cpp parity: DiaNNSpecLibReader.cpp:73 read_strings.
    private static void ReadStrings(BinaryReader r, List<string> dst)
    {
        int size = r.ReadInt32();
        if (size <= 0) return;
        dst.Capacity = size;
        for (int i = 0; i < size; i++)
            dst.Add(ReadString(r));
    }

    // cpp parity: DiaNNSpecLibReader.cpp:48 read_vector specialised for int32.
    private static void ReadIntVector(BinaryReader r)
    {
        int size = r.ReadInt32();
        if (size <= 0) return;
        r.BaseStream.Seek((long)size * sizeof(int), SeekOrigin.Current);
    }

    // --- inner spec reader --------------------------------------------------------------

    /// <summary>
    /// Spec-file reader that returns peaks straight off the parsed <see cref="Entry"/>. cpp
    /// parity: DiaNNSpecLibReader.cpp:1314 <c>getSpectrum(int identifier, ...)</c> override.
    /// </summary>
    private sealed class DiaNNSpecLibSpecReader : SpecFileReaderBase
    {
        private readonly Library _lib;

        public DiaNNSpecLibSpecReader(Library lib)
        {
            _lib = lib;
        }

        public override void OpenFile(string path, bool mzSort = false) { /* no-op */ }
        public override SpecIdType IdType { set { /* no-op */ } }

        public override bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true)
        {
            ArgumentNullException.ThrowIfNull(returnData);
            if (identifier < 0 || identifier >= _lib.Entries.Count) return false;
            var entry = _lib.Entries[identifier];

            returnData.Charge = entry.Target.Charge;
            returnData.Id = entry.Target.Index;
            returnData.NumPeaks = entry.Target.Fragments.Count;
            returnData.Mz = entry.Target.Mz;
            if (entry.BestPsm != null)
            {
                returnData.RetentionTime = entry.BestPsm.Rt;
                returnData.StartTime = entry.BestPsm.RtStart;
                returnData.EndTime = entry.BestPsm.RtEnd;
                returnData.IonMobility = entry.BestPsm.IonMobility;
                if (returnData.IonMobility > 0)
                    returnData.IonMobilityType = IonMobilityType.InverseReducedVsecPerCm2;
            }

            if (!getPeaks) return true;

            returnData.Mzs = new double[returnData.NumPeaks];
            returnData.Intensities = new float[returnData.NumPeaks];
            for (int i = 0; i < returnData.NumPeaks; i++)
            {
                var product = entry.Target.Fragments[i];
                returnData.Mzs[i] = product.Mz;
                returnData.Intensities[i] = product.Height;
            }
            return true;
        }

        public override bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true) => false;
        public override bool GetNextSpectrum(SpecData returnData, bool getPeaks = true) => false;
    }
}
