// Port of pwiz_tools/BiblioSpec/src/BlibBuilder.{h,cpp}
//
// Faithful C# port of BiblioSpec::BlibBuilder. CLI plumbing (parseCommandArgs / usage /
// parseNextSwitch) and the stdin-driven sequence-list reader are intentionally omitted —
// those move to the BlibBuild executable later in Phase 4.
//
// The per-file reader dispatch inside BuildLibrary() throws NotImplementedException for
// every input type until the concrete reader subclasses are ported (Phase 2 stage 3).

using System.Globalization;
using System.Text;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Supported input file formats for <see cref="BlibBuilder"/>. Numeric values match the cpp
/// <c>BUILD_INPUT</c> enum at <c>BlibBuilder.h:62</c> so they can be used as dictionary keys
/// against cpp parity tables.
/// </summary>
/// <remarks>
/// cpp parity: lives in BlibBuilder.h (not BlibUtils.h), so we co-locate it with the C#
/// <see cref="BlibBuilder"/> class for the same reason.
/// </remarks>
public enum BuildInput
{
    /// <summary>SEQUEST <c>.sqt</c>.</summary>
    Sqt = 0,
    /// <summary><c>.pep.xml</c> / <c>.pepXML</c>.</summary>
    PepXml,
    /// <summary>IDPicker <c>.idpXML</c>.</summary>
    IdpXml,
    /// <summary>Mascot <c>.dat</c>.</summary>
    Mascot,
    /// <summary>X! Tandem <c>.xtan.xml</c>.</summary>
    Tandem,
    /// <summary>Protein Pilot <c>.group.xml</c>.</summary>
    ProtPilot,
    /// <summary>Scaffold <c>.mzid</c> (Scaffold flavour).</summary>
    Scaffold,
    /// <summary>Waters MSE <c>final_fragment.csv</c>.</summary>
    Mse,
    /// <summary>OMSSA pep.xml.</summary>
    Omssa,
    /// <summary>Protein Prospector pep.xml.</summary>
    ProtProspect,
    /// <summary>MaxQuant <c>msms.txt</c>.</summary>
    MaxQuant,
    /// <summary>Morpheus pep.xml.</summary>
    Morpheus,
    /// <summary>MS-GF+ pep.xml.</summary>
    Msgf,
    /// <summary>PEAKS pep.xml.</summary>
    Peaks,
    /// <summary>Byonic <c>.mzid</c>.</summary>
    Byonic,
    /// <summary>PeptideShaker <c>.mzid</c>.</summary>
    PeptideShaker,
    /// <summary>Generic q-value input (e.g. <c>.tsv</c>, <c>.osw</c>, <c>.speclib</c>, etc.).</summary>
    GenericQValueInput,

    // Keep this last; cpp BlibBuilder.h:83.
    /// <summary>Marker / count, do not use as a real input type. cpp <c>NUM_BUILD_INPUTS</c>.</summary>
    NumBuildInputs,
}

/// <summary>
/// Builds a BiblioSpec <c>.blib</c> library from a mixture of search-result and library files.
/// Port of <c>BiblioSpec::BlibBuilder</c> at <c>pwiz_tools/BiblioSpec/src/BlibBuilder.{h,cpp}</c>.
/// </summary>
/// <remarks>
/// <para>
/// CLI plumbing — <c>parseCommandArgs</c>, <c>parseNextSwitch</c>, <c>usage()</c>, and the
/// stdin-driven sequence-list reader — is NOT ported here; that responsibility moves to the
/// BlibBuild executable (Phase 4). Everything else from the cpp class is reproduced.
/// </para>
/// <para>
/// The per-input-type reader dispatch in <see cref="BuildLibrary"/> currently throws
/// <see cref="NotImplementedException"/> for every input type because the concrete reader
/// subclasses (SslReader, PepXmlReader, SqtReader, MzIdentMLReader, MascotResultsReader,
/// MaxQuantReader, ...) have not landed yet. The surrounding driver — file iteration,
/// per-file score threshold setup, transaction wrapping, error capture — is in place and
/// ready for the readers.
/// </para>
/// </remarks>
public class BlibBuilder : BlibMaker
{
    // cpp BlibBuilder.cpp:41 — defaults from the cpp constructor initializer list.
    private const int DefaultLevelCompress = 3;
    private const long DefaultFileSizeThresholdForCaching = 800_000_000L;

    // cpp parity: BlibBuilder.cpp:67 — list of all extensions BlibBuild recognises.
    private static readonly string[] _supportedTypes =
    {
        ".blib",
        ".pep.xml",
        ".pep.XML",
        ".pepXML",
        ".sqt",
        ".perc.xml",
        ".dat",
        ".xtan.xml",
        ".idpXML",
        ".group.xml",
        ".group", // score type only
        ".pride.xml",
        ".msf",
        ".pdResult",
        ".mzid",
        ".mzid.gz",
        "msms.txt",
        "final_fragment.csv",
        ".proxl.xml",
        ".ssl",
        ".hk.bs.kro", // Hardklor result file postprocessed by BullseyeSharp
        ".mlb",
        ".speclib",
        ".tsv",
        ".osw",
        ".mzTab",
        "mztab.txt",
    };

    // cpp parity: BlibBuilder.cpp:43 — explicitCutoff = -1 means "no explicit -c switch".
    private double _explicitCutoff = -1;

    // cpp parity: per-input-file score thresholds parsed from `score_threshold=` suffixes in
    // the stdin file list. CLI plumbing is out of scope; the dictionary is exposed so the
    // future BlibBuild CLI layer (or unit tests) can populate it directly.
    private readonly Dictionary<string, double> _inputThresholds = new(StringComparer.Ordinal);

    private readonly List<string> _inputFiles = new();
    private HashSet<string>? _targetSequences;
    private HashSet<string>? _targetSequencesModified;

    /// <summary>Constructs a BlibBuilder with cpp defaults.</summary>
    public BlibBuilder()
    {
        LevelCompress = DefaultLevelCompress;
        FileSizeThresholdForCaching = DefaultFileSizeThresholdForCaching;
        ForcedPusherInterval = -1; // cpp parity: BlibBuilder.cpp:43
    }

    /// <summary>
    /// zlib compression level applied to inserted peak blobs. cpp <c>level_compress</c>;
    /// default 3.
    /// </summary>
    public int LevelCompress { get; set; }

    /// <summary>
    /// Minimum <c>.dat</c> file size before caching kicks in. cpp <c>fileSizeThresholdForCaching</c>;
    /// default 800 MB.
    /// </summary>
    public long FileSizeThresholdForCaching { get; set; }

    /// <summary>
    /// cpp <c>getCacheThreshold</c>. Returns the same value as <see cref="FileSizeThresholdForCaching"/>;
    /// kept as a method for cpp parity with call sites.
    /// </summary>
    public int GetCacheThreshold()
    {
        // cpp returns `int`; clamp to int.MaxValue for safety.
        return FileSizeThresholdForCaching > int.MaxValue
            ? int.MaxValue
            : (int)FileSizeThresholdForCaching;
    }

    /// <summary>cpp <c>maxQuantModsPath_</c> — path to XML mods file for MaxQuant parsing.</summary>
    public string MaxQuantModsPath { get; set; } = string.Empty;

    /// <summary>cpp <c>maxQuantParamsPath_</c> — path to XML params file for MaxQuant parsing.</summary>
    public string MaxQuantParamsPath { get; set; } = string.Empty;

    /// <summary>
    /// cpp <c>forcedPusherInterval</c>. Pusher interval for Waters final_fragment.csv files.
    /// Default -1 ("not set").
    /// </summary>
    public double ForcedPusherInterval { get; set; }

    /// <summary>
    /// cpp <c>targetSequences_</c>. When non-null, only PSMs whose unmodified peptide
    /// sequence is in this set are included.
    /// </summary>
    public IReadOnlySet<string>? TargetSequences => _targetSequences;

    /// <summary>
    /// cpp <c>targetSequencesModified_</c>. When non-null, only PSMs whose
    /// (low-precision) modified peptide sequence is in this set are included.
    /// </summary>
    public IReadOnlySet<string>? TargetSequencesModified => _targetSequencesModified;

    /// <summary>
    /// Replace the target unmodified sequence filter set. Pass <c>null</c> to disable the
    /// filter (cpp parity: pointer-null = "no filter").
    /// </summary>
    public void SetTargetSequences(IEnumerable<string>? sequences) =>
        _targetSequences = sequences == null ? null : new HashSet<string>(sequences, StringComparer.Ordinal);

    /// <summary>
    /// Replace the target modified-sequence filter set. Pass <c>null</c> to disable the
    /// filter (cpp parity: pointer-null = "no filter").
    /// </summary>
    public void SetTargetSequencesModified(IEnumerable<string>? sequences) =>
        _targetSequencesModified = sequences == null ? null : new HashSet<string>(sequences, StringComparer.Ordinal);

    /// <summary>
    /// cpp <c>input_files</c>. List of input result/library files. Exposed read-only via
    /// <see cref="InputFiles"/>; populate via <see cref="AddInputFile(string)"/> /
    /// <see cref="AddInputFile(string, double)"/>.
    /// </summary>
    public IReadOnlyList<string> InputFiles => _inputFiles;

    /// <summary>Append an input file with no per-file score threshold override.</summary>
    public void AddInputFile(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        if (!IsSupportedType(file))
        {
            // cpp parity: BlibBuilder.cpp:294 — error out with the supported list.
            Verbosity.Error($"Unsupported file type '{file}'.  Must be one of {string.Join(", ", _supportedTypes)}.");
        }
        _inputFiles.Add(file);
    }

    /// <summary>Append an input file with an explicit per-file score threshold (cpp parity: <c>score_threshold=</c>).</summary>
    public void AddInputFile(string file, double scoreThreshold)
    {
        AddInputFile(file);
        _inputThresholds[file] = scoreThreshold;
    }

    /// <summary>
    /// cpp <c>setCurFile</c>. Index into <see cref="InputFiles"/> currently being processed by
    /// <see cref="BuildLibrary"/>.
    /// </summary>
    public int CurFile { get; set; }

    /// <summary>cpp <c>explicitCutoff</c>. -1 means "no explicit -c switch".</summary>
    public double ExplicitCutoff
    {
        get => _explicitCutoff;
        set => _explicitCutoff = value;
    }

    /// <summary>
    /// cpp <c>getScoreThreshold</c>. Returns the per-file override if present, otherwise the
    /// format-specific default (with the explicit -c cutoff folded in). cpp BlibBuilder.cpp:130.
    /// </summary>
    public virtual double GetScoreThreshold(BuildInput fileType)
    {
        // cpp parity: per-file override wins.
        if (CurFile >= 0 && CurFile < _inputFiles.Count &&
            _inputThresholds.TryGetValue(_inputFiles[CurFile], out var perFile))
        {
            return perFile;
        }

        // cpp parity: format-specific defaults; -c is folded into "incorrect" types as 1-cutoff.
        // BlibBuilder.cpp:135-170.
        return fileType switch
        {
            BuildInput.Sqt => _explicitCutoff >= 0 ? 1 - _explicitCutoff : 0.01,                    // FDR
            BuildInput.PepXml => _explicitCutoff >= 0 ? _explicitCutoff : 0.95,                     // peptide prophet probability
            BuildInput.IdpXml => 0,                                                                 // use all results
            BuildInput.Mascot => _explicitCutoff >= 0 ? 1 - _explicitCutoff : 0.05,                 // expectation value
            BuildInput.Tandem => _explicitCutoff >= 0 ? 1 - _explicitCutoff : 0.1,                  // expect score
            BuildInput.ProtPilot => _explicitCutoff >= 0 ? _explicitCutoff : 0.95,                  // confidence
            BuildInput.Scaffold => _explicitCutoff >= 0 ? _explicitCutoff : 0.95,                   // peptide probability
            BuildInput.Mse => 6,                                                                    // Waters MSe peptide score
            BuildInput.Omssa => _explicitCutoff >= 0 ? 1 - _explicitCutoff : 0.00001,               // max OMSSA expect score
            BuildInput.ProtProspect => _explicitCutoff >= 0 ? 1 - _explicitCutoff : 0.001,          // expect score
            BuildInput.MaxQuant => _explicitCutoff >= 0 ? 1 - _explicitCutoff : 0.05,               // PEP
            BuildInput.Morpheus => _explicitCutoff >= 0 ? 1 - _explicitCutoff : 0.01,               // PSM q-value
            BuildInput.Msgf => _explicitCutoff >= 0 ? 1 - _explicitCutoff : 0.01,                   // PSM q-value
            BuildInput.Peaks => _explicitCutoff >= 0 ? 1 - _explicitCutoff : 0.05,                  // p-value
            BuildInput.Byonic => _explicitCutoff >= 0 ? 1 - _explicitCutoff : 0.05,                 // PEP
            BuildInput.PeptideShaker => _explicitCutoff >= 0 ? _explicitCutoff : 0.95,              // PSM confidence
            BuildInput.GenericQValueInput => _explicitCutoff >= 0 ? 1 - _explicitCutoff : 0.01,
            _ => -1, // cpp parity: falls through to `return -1;` at BlibBuilder.cpp:171.
        };
    }

    /// <summary>
    /// cpp <c>getCutoffScore</c>. Returns the explicit / per-file cutoff used when no
    /// format-specific default applies. Overrides <see cref="BlibMaker.GetCutoffScore"/>.
    /// </summary>
    /// <remarks>cpp BlibBuilder.cpp:570 — per-file override wins, else explicitCutoff.</remarks>
    protected internal override double GetCutoffScore()
    {
        if (CurFile >= 0 && CurFile < _inputFiles.Count &&
            _inputThresholds.TryGetValue(_inputFiles[CurFile], out var perFile))
        {
            return perFile;
        }
        return _explicitCutoff;
    }

    /// <summary>
    /// cpp <c>keepCharge</c>. True if the precursor charge list is empty (no filter) or
    /// contains <paramref name="z"/>.
    /// </summary>
    public bool KeepCharge(int z) =>
        PrecursorCharges.Count == 0 || PrecursorCharges.Contains(z);

    /// <summary>
    /// cpp <c>attachAll</c>. No-op — the cpp implementation is empty (see BlibBuilder.cpp:305-316
    /// for the commented-out original ATTACH loop).
    /// </summary>
    protected override void AttachAll()
    {
        // cpp parity: BlibBuilder no longer attaches all libraries up front; each
        // .blib input is attached / detached individually by transferLibrary / commit.
    }

    /// <summary>
    /// cpp <c>commit</c>. After the BlibMaker commit, DETACH every <c>.blib</c> source library
    /// that was ATTACHed during transfer. cpp BlibBuilder.cpp:491.
    /// </summary>
    public override void Commit()
    {
        base.Commit();

        for (var i = 0; i < _inputFiles.Count; i++)
        {
            if (HasExtensionCi(_inputFiles[i], ".blib"))
            {
                SqlStmt(string.Format(CultureInfo.InvariantCulture, "DETACH DATABASE tmp{0}", i));
            }
        }
    }

    /// <summary>
    /// cpp <c>insertPeaks</c>. Forwards to <see cref="BlibMaker.InsertPeaks"/> with the
    /// configured <see cref="LevelCompress"/>. cpp BlibBuilder.cpp:741.
    /// </summary>
    public void InsertPeaks(int spectraId, int peaksCount, ReadOnlySpan<double> mz, ReadOnlySpan<float> intensity)
    {
        InsertPeaks(spectraId, LevelCompress, peaksCount, mz, intensity);
    }

    /// <summary>
    /// cpp <c>generateModifiedSeq</c>. Combine an unmodified sequence and a list of mods into
    /// a single modified-sequence string, honouring <see cref="BlibMaker.HighPrecisionModifications"/>.
    /// </summary>
    public string GenerateModifiedSeq(string unmodSeq, IReadOnlyList<SeqMod> mods) =>
        GetModifiedSequenceWithPrecision(unmodSeq, mods, HighPrecisionModifications);

    /// <summary>
    /// cpp <c>getModifiedSequenceWithPrecision</c>. Insert mods into the unmod sequence using
    /// either fixed 1-decimal or precision-sensitive formatting. Static for parity with cpp.
    /// </summary>
    /// <remarks>
    /// cpp parity: BlibBuilder.cpp:679. Mods are applied from the end so positions don't
    /// shift; mods with deltaMass == 0 are skipped; positions past the end raise an error.
    /// </remarks>
    public static string GetModifiedSequenceWithPrecision(string unmodSeq, IReadOnlyList<SeqMod> mods, bool highPrecision)
    {
        ArgumentNullException.ThrowIfNull(unmodSeq);
        ArgumentNullException.ThrowIfNull(mods);

        var modifiedSeq = new StringBuilder(unmodSeq);

        // cpp parity: iterate in reverse so the position remains valid as we insert.
        for (var i = mods.Count - 1; i >= 0; i--)
        {
            var mod = mods[i];
            if (mod.DeltaMass == 0)
                continue;
            if (mod.Position > modifiedSeq.Length)
            {
                Verbosity.Error(
                    $"Cannot modify sequence {modifiedSeq}, length {modifiedSeq.Length}, at position {mods[^1].Position}. ");
            }

            var format = GetModMassFormat(mod.DeltaMass, highPrecision);
            // cpp parity: sprintf with "[%+.Nf]" — leading sign for positive values, fixed precision.
            var formatted = string.Format(CultureInfo.InvariantCulture, format, mod.DeltaMass);
            modifiedSeq.Insert(mod.Position, formatted);
        }

        return modifiedSeq.ToString();
    }

    /// <summary>
    /// cpp <c>getLowPrecisionModSeq</c>. Convenience for the highPrecision=false form.
    /// </summary>
    public static string GetLowPrecisionModSeq(string unmodSeq, IReadOnlyList<SeqMod> mods) =>
        GetModifiedSequenceWithPrecision(unmodSeq, mods, highPrecision: false);

    /// <summary>
    /// cpp <c>getModMassFormat</c> at BlibBuilder.cpp:658. Decide which decimal precision to use
    /// for a given mass under high-precision mode.
    /// </summary>
    /// <returns>A composite format string such as <c>"[{0:+0.0;-0.0}]"</c>. Caller passes the
    /// mass into <see cref="string.Format(System.IFormatProvider, string, object?[])"/>.</returns>
    private static string GetModMassFormat(double mass, bool highPrecision)
    {
        // cpp parity: low precision is always 1 decimal.
        // cpp parity uses sprintf "[%+.1f]" which forces a leading sign. The C# equivalent
        // is the section-format "[{0:+0.0;-0.0}]" — positives use "+0.0", negatives "-0.0".
        if (!highPrecision)
            return "[{0:+0.0;-0.0}]";

        // cpp parity: choose the smallest precision that doesn't lose information. Tested by
        // rounding to 5 decimals and inspecting trailing zero groups.
        var decimalPart = mass - (int)mass;
        var decimalInt = (int)Math.Round(decimalPart * 100000);
        if (decimalInt == (decimalInt / 10000) * 10000) return "[{0:+0.0;-0.0}]";
        if (decimalInt == (decimalInt / 1000) * 1000) return "[{0:+0.00;-0.00}]";
        if (decimalInt == (decimalInt / 100) * 100) return "[{0:+0.000;-0.000}]";
        if (decimalInt == (decimalInt / 10) * 10) return "[{0:+0.0000;-0.0000}]";
        // cpp parity: matches Skyline's MassModification.MAX_PRECISION_FOR_LIB (Dec 2019).
        return "[{0:+0.00000;-0.00000}]";
    }

    /// <summary>
    /// cpp <c>collapseSources</c>. Detect Q1/Q2/Q3 (or ForLibQ1/2/3) split mzXML source
    /// groups and merge them back into a single source. cpp BlibBuilder.cpp:416.
    /// </summary>
    public virtual void CollapseSources()
    {
        const string reqExt = ".mzXML";

        // Gather all .mzXML SpectrumSourceFiles into id -> filename map (insertion order
        // matters for deterministic iteration like cpp).
        var fileIds = new SortedDictionary<string, int>(StringComparer.Ordinal);
        using (var cmd = Db.CreateCommand())
        {
            cmd.CommandText = "SELECT id, filename FROM SpectrumSourceFiles";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                if (name.Length >= reqExt.Length &&
                    name.AsSpan(name.Length - reqExt.Length).Equals(reqExt.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    fileIds[name] = id;
                }
            }
        }

        // cpp parity: BlibBuilder.cpp:436-439 — two pattern groups.
        var patternGroups = new[]
        {
            new[] { "_Q1.", "_Q2.", "_Q3." },
            new[] { ".ForLibQ1.", ".ForLibQ2.", ".ForLibQ3." },
        };

        foreach (var (sourceFile, sourceId) in fileIds)
        {
            foreach (var group in patternGroups)
            {
                var firstPattern = group[0];
                var pos = sourceFile.IndexOf(firstPattern, StringComparison.Ordinal);
                if (pos < 0)
                    continue;

                var groupIds = new List<int>();
                var found = true;
                for (var p = 1; p < group.Length; p++)
                {
                    var expected = string.Concat(
                        sourceFile.AsSpan(0, pos),
                        group[p].AsSpan(),
                        sourceFile.AsSpan(pos + firstPattern.Length));
                    if (!fileIds.TryGetValue(expected, out var expectedId))
                    {
                        found = false;
                        break;
                    }
                    groupIds.Add(expectedId);
                }
                if (!found || groupIds.Count != group.Length - 1)
                    continue;

                // Found a complete pattern group; reassign RefSpectra.fileID and delete the
                // redundant SpectrumSourceFiles rows, then rename the survivor.
                var update = new StringBuilder();
                var delete = new StringBuilder();
                update.Append(CultureInfo.InvariantCulture, $"UPDATE RefSpectra SET fileID = {sourceId} WHERE");
                delete.Append("DELETE FROM SpectrumSourceFiles WHERE");
                for (var k = 0; k < groupIds.Count; k++)
                {
                    if (k > 0)
                    {
                        update.Append(" OR");
                        delete.Append(" OR");
                    }
                    update.Append(CultureInfo.InvariantCulture, $" fileID = {groupIds[k]}");
                    delete.Append(CultureInfo.InvariantCulture, $" id = {groupIds[k]}");
                }
                SqlStmt(update.ToString());
                SqlStmt(delete.ToString());

                var renamed = string.Concat(
                    sourceFile.AsSpan(0, pos),
                    ".".AsSpan(),
                    sourceFile.AsSpan(pos + firstPattern.Length));
                SqlStmt(string.Format(
                    CultureInfo.InvariantCulture,
                    "UPDATE SpectrumSourceFiles SET fileName = '{0}' WHERE id = {1}",
                    SqliteRoutine.EscapeApostrophes(renamed), sourceId));
                break;
            }
        }
    }

    /// <summary>
    /// cpp <c>transferLibrary</c>. ATTACH the iLib-th input .blib, copy its source files,
    /// then walk RefSpectra (optionally filtering by target sequences) calling
    /// <see cref="BlibMaker.TransferSpectrum"/> for each row.
    /// </summary>
    /// <returns>Number of spectra processed.</returns>
    public virtual int TransferLibrary(int iLib)
    {
        if (iLib < 0 || iLib >= _inputFiles.Count)
            throw new ArgumentOutOfRangeException(nameof(iLib));

        var path = _inputFiles[iLib];
        VerifyFileExists(path);

        var schemaTmp = "tmp" + iLib.ToString(CultureInfo.InvariantCulture);
        SqlStmt(string.Format(
            CultureInfo.InvariantCulture,
            "ATTACH DATABASE '{0}' as {1}",
            SqliteRoutine.EscapeApostrophes(path), schemaTmp));

        CreateUpdatedRefSpectraView(schemaTmp);

        // cpp parity: BlibBuilder.cpp:334-356 — inspect columns to pick a tableVersion.
        var tableVersion = 0;
        if (TableColumnExists(schemaTmp, "RefSpectra", "retentionTime"))
        {
            if (TableColumnExists(schemaTmp, "RefSpectra", "startTime"))
                tableVersion = BlibSchema.MinVersionTic;
            else if (TableExists(schemaTmp, "RefSpectraPeakAnnotations"))
                tableVersion = BlibSchema.MinVersionPeakAnnot;
            else if (TableColumnExists(schemaTmp, "RefSpectra", "ionMobilityHighEnergyOffset"))
                tableVersion = BlibSchema.MinVersionImsUnits;
            else if (TableColumnExists(schemaTmp, "RefSpectra", "moleculeName"))
                tableVersion = BlibSchema.MinVersionSmallMol;
            else if (TableColumnExists(schemaTmp, "RefSpectra", "collisionalCrossSectionSqA"))
                tableVersion = BlibSchema.MinVersionCcs;
            else if (TableColumnExists(schemaTmp, "RefSpectra", "ionMobilityHighEnergyDriftTimeOffsetMsec"))
                tableVersion = BlibSchema.MinVersionImsHeoff;
            else if (TableColumnExists(schemaTmp, "RefSpectra", "ionMobilityValue"))
                tableVersion = BlibSchema.MinVersionIms;
            else
                tableVersion = 1;
        }

        BeginTransaction();

        Message = "ERROR: Failed transferring spectra from " + path;
        Verbosity.Status($"Transferring spectra from {BaseName(path)}.");

        TransferSpectrumFiles(schemaTmp);

        var processed = 0;
        using (var cmd = Db.CreateCommand())
        {
            cmd.CommandText = string.Format(
                CultureInfo.InvariantCulture,
                "SELECT id, peptideSeq, peptideModSeq FROM {0}.RefSpectra ORDER BY id",
                schemaTmp);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                processed++;

                var skip = false;
                if (_targetSequences != null || _targetSequencesModified != null)
                {
                    var seqUnmodified = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    var rawModSeq = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    // cpp parity: BlibBuilder.cpp:389 normalises the modified sequence via
                    // parseSequence(..., true) before comparing. parseSequence isn't ported yet
                    // (it lives in the CLI / stdin path); we compare against the raw string.
                    // The future stdin path will need to mirror the parse before populating
                    // _targetSequencesModified.
                    var inUnmod = _targetSequences != null && _targetSequences.Contains(seqUnmodified);
                    var inMod = _targetSequencesModified != null && _targetSequencesModified.Contains(rawModSeq);
                    skip = !(inUnmod || inMod);
                }

                if (!skip)
                {
                    var spectraId = reader.GetInt32(0);
                    // cpp parity: BlibBuilder.cpp:402 — always passes copies=1, regardless of
                    // whether the source library was redundant.
                    TransferSpectrum(schemaTmp, spectraId, 1, tableVersion);
                }
            }
        }

        SqlStmt("DROP VIEW RefSpectraTransfer");
        EndTransaction();
        return processed;
    }

    /// <summary>
    /// Per-file error messages accumulated during <see cref="BuildLibrary"/>. Exposed so
    /// the CLI driver can scan them for the optional <c>-e &lt;expected-error&gt;</c> match
    /// pattern (cpp BlibBuild.cpp:248). One entry per logged line; multiple entries per
    /// failing file are normal (one for the exception text, one for the "reading file X"
    /// follow-up).
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;
    private readonly List<string> _errors = new();

    /// <summary>
    /// Top-level driver. Iterate <see cref="InputFiles"/>, dispatch each to its reader, and
    /// catch / log errors. cpp BlibBuild.cpp:140 is the canonical reference for this loop —
    /// we mirror its structure here so this class can be driven directly from tests without
    /// the BlibBuild executable.
    /// </summary>
    /// <returns>True when every input file was processed without errors.</returns>
    public virtual bool BuildLibrary()
    {
        var success = true;
        _errors.Clear();

        for (var i = 0; i < _inputFiles.Count; i++)
        {
            var resultFile = _inputFiles[i];
            CurFile = i;
            Verbosity.Status($"Reading results from {resultFile}.");

            try
            {
                DispatchReader(i, resultFile);
            }
            catch (BlibException ex)
            {
                success = false;
                _errors.Add(ex.Message);
                Verbosity.Warn(ex.Message);
                if (!ex.HasFilename)
                {
                    _errors.Add("reading file " + resultFile);
                    Verbosity.Warn("reading file " + resultFile);
                }
            }
            catch (Exception ex) when (ex is not NotImplementedException)
            {
                // cpp parity: BlibBuild.cpp:231 — catch std::exception, record + continue.
                success = false;
                _errors.Add(ex.Message);
                _errors.Add("reading file " + resultFile);
                Verbosity.Warn(ex.Message);
                Verbosity.Warn("reading file " + resultFile);
            }
        }

        return success;
    }

    /// <summary>
    /// Registry of (accepts-predicate, constructor) pairs — one per BuildParser subclass.
    /// <see cref="DispatchReader"/> walks this in order and picks the first reader whose
    /// <c>AcceptsExtension</c> static method claims the file. Adding a new reader is a
    /// one-line append here; each reader owns its own extension list.
    /// </summary>
    /// <remarks>
    /// Ordering matters only when two readers' AcceptsExtension predicates could both
    /// match the same path (none of today's four overlap). The <c>.blib</c> path is
    /// handled separately by <see cref="DispatchReader"/> before this list runs — it's a
    /// transfer operation, not a reader instantiation. The remaining cpp reader
    /// subclasses still to port (each will append a row here):
    /// <list type="bullet">
    /// <item>PercolatorXmlReader — for <c>.perc.xml</c></item>
    /// <item>MascotResultsReader — for <c>.dat</c></item>
    /// <item>TandemNativeParser — for <c>.xtan.xml</c></item>
    /// <item>ProteinPilotReader — for <c>.group.xml</c> (also handles <c>.group</c> in score-lookup mode)</item>
    /// <item>PrideXmlReader — for <c>.pride.xml</c></item>
    /// <item>MaxQuantReader — for <c>msms.txt</c></item>
    /// <item>MsfReader    — for <c>.msf</c> / <c>.pdResult</c></item>
    /// <item>MzIdentMLReader — for <c>.mzid</c> / <c>.mzid.gz</c></item>
    /// <item>WatersMseReader — for <c>final_fragment.csv</c></item>
    /// <item>ProxlXmlReader — for <c>.proxl.xml</c></item>
    /// <item>ShimadzuMlbReader — for <c>.mlb</c></item>
    /// <item>DiaNnSpecLibReader — for <c>.speclib</c></item>
    /// <item>TsvReader    — for <c>.tsv</c></item>
    /// <item>OswReader    — for <c>.osw</c></item>
    /// <item>MzTabReader  — for <c>.mzTab</c> / <c>mztab.txt</c></item>
    /// <item>HardklorReader — for <c>.hk.bs.kro</c></item>
    /// </list>
    /// Files with extensions not yet covered cause <see cref="DispatchReader"/> to throw
    /// <see cref="NotImplementedException"/> identifying the missing reader.
    /// </remarks>
    private static readonly (Func<string, bool> Accepts, Func<BlibBuilder, string, BuildParser> Create)[]
        _readerFactories =
    {
        (SslReader.AcceptsExtension,         (b, f) => new SslReader(b, f, parentProgress: null)),
        (PepXMLreader.AcceptsExtension,      (b, f) => new PepXMLreader(b, f, parentProgress: null)),
        (SQTreader.AcceptsExtension,         (b, f) => new SQTreader(b, f, parentProgress: null)),
        (MzIdentMLReader.AcceptsExtension,   (b, f) => new MzIdentMLReader(b, f, parentProgress: null)),
        (WatersMseReader.AcceptsExtension,   (b, f) => new WatersMseReader(b, f, parentProgress: null)),
        (ProteinPilotReader.AcceptsExtension, (b, f) => new ProteinPilotReader(b, f, parentProgress: null)),
        (PrideXmlReader.AcceptsExtension,    (b, f) => new PrideXmlReader(b, f, parentProgress: null)),
        (IdpXMLreader.AcceptsExtension,      (b, f) => new IdpXMLreader(b, f, parentProgress: null)),
        (TandemNativeParser.AcceptsExtension, (b, f) => new TandemNativeParser(b, f, parentProgress: null)),
        (MaxQuantReader.AcceptsExtension,    (b, f) => new MaxQuantReader(b, f, parentProgress: null)),
        (HardklorReader.AcceptsExtension,    (b, f) => new HardklorReader(b, f, parentProgress: null)),
        (ShimadzuMLBReader.AcceptsExtension, (b, f) => new ShimadzuMLBReader(b, f, parentProgress: null)),
        (ProxlXmlReader.AcceptsExtension,    (b, f) => new ProxlXmlReader(b, f, parentProgress: null)),
        (PercolatorXmlReader.AcceptsExtension, (b, f) => new PercolatorXmlReader(b, f, parentProgress: null)),
        (MSFReader.AcceptsExtension,         (b, f) => new MSFReader(b, f, parentProgress: null)),
        (MzTabReader.AcceptsExtension,       (b, f) => new MzTabReader(b, f, parentProgress: null)),
        (DiaNNSpecLibReader.AcceptsExtension, (b, f) => new DiaNNSpecLibReader(b, f, parentProgress: null)),
        (OSWReader.AcceptsExtension,         (b, f) => new OSWReader(b, f, parentProgress: null)),
        (TSVReader.AcceptsExtension,         (b, f) => new TSVReader(b, f, parentProgress: null)),
    };

    /// <summary>
    /// Look up the reader class for <paramref name="resultFile"/> and parse it. cpp parity:
    /// BlibBuild.cpp:157-213. <c>.blib</c> short-circuits to <see cref="TransferLibrary"/>;
    /// every other extension is matched against <see cref="_readerFactories"/>.
    /// </summary>
    protected virtual void DispatchReader(int fileIndex, string resultFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultFile);

        // cpp parity: BlibBuild.cpp:158 — .blib is special-cased to call transferLibrary.
        // (Not a reader instantiation; it copies rows from another library.)
        if (HasExtensionCi(resultFile, ".blib"))
        {
            TransferLibrary(fileIndex);
            return;
        }

        // Ask each reader if it accepts the extension; first hit wins.
        foreach (var (accepts, create) in _readerFactories)
        {
            if (!accepts(resultFile))
                continue;
            var reader = create(this, resultFile);
            try
            {
                reader.ParseFile();
            }
            finally
            {
                (reader as IDisposable)?.Dispose();
            }
            return;
        }

        // No registered reader claimed the extension — list the unported ones for context.
        var inputType = ClassifyInput(resultFile);
        throw new NotImplementedException(
            $"Reader for input type {inputType} (file '{resultFile}') not yet ported. " +
            "See DispatchReader documentation for the list of pending reader subclasses.");
    }

    /// <summary>
    /// Map a filename to its <see cref="BuildInput"/> classification. Useful for callers /
    /// tests that want to probe per-format defaults without owning a reader.
    /// </summary>
    /// <remarks>
    /// Extensions are matched case-insensitively, mirroring cpp <c>has_extension</c>
    /// (BlibBuilder.cpp:722, which uses <c>bal::iends_with</c>).
    /// </remarks>
    public static BuildInput ClassifyInput(string filename)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        if (HasExtensionCi(filename, ".sqt")) return BuildInput.Sqt;
        if (HasExtensionCi(filename, ".pep.xml") || HasExtensionCi(filename, ".pepXML")) return BuildInput.PepXml;
        if (HasExtensionCi(filename, ".idpXML")) return BuildInput.IdpXml;
        if (HasExtensionCi(filename, ".dat")) return BuildInput.Mascot;
        if (HasExtensionCi(filename, ".xtan.xml")) return BuildInput.Tandem;
        if (HasExtensionCi(filename, ".group.xml") || HasExtensionCi(filename, ".group")) return BuildInput.ProtPilot;
        if (HasExtensionCi(filename, "final_fragment.csv")) return BuildInput.Mse;
        if (HasExtensionCi(filename, "msms.txt")) return BuildInput.MaxQuant;
        if (HasExtensionCi(filename, ".mzid") || HasExtensionCi(filename, ".mzid.gz")) return BuildInput.Scaffold;
        // .tsv, .osw, .speclib, .mzTab, mztab.txt, .hk.bs.kro, .mlb, .pride.xml, .ssl, .proxl.xml,
        // .msf, .pdResult, .perc.xml — all flow through readers that ultimately surface a generic
        // q-value (cpp drives them via the generic threshold).
        return BuildInput.GenericQValueInput;
    }

    /// <summary>
    /// Returns true if <paramref name="filename"/> is recognised by <see cref="ClassifyInput"/>
    /// or is a <c>.blib</c>. Used by <see cref="AddInputFile(string)"/>.
    /// </summary>
    public static bool IsSupportedType(string filename)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        foreach (var ext in _supportedTypes)
        {
            if (HasExtensionCi(filename, ext))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Case-insensitive ends-with test. cpp parity: <c>has_extension</c> uses
    /// <c>bal::iends_with</c> (BlibBuilder.cpp:722). Exposed as <c>internal</c> so
    /// reader classes can share the same predicate when implementing
    /// <c>AcceptsExtension</c>.
    /// </summary>
    internal static bool HasExtensionCi(string filename, string ext) =>
        filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// cpp <c>base_name</c> at BlibBuilder.cpp:716. Returns the substring after the last
    /// <c>/</c> or <c>\</c>, or the whole string if neither is present.
    /// </summary>
    private static string BaseName(string name)
    {
        var lastSlash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        return lastSlash < 0 ? name : name.Substring(lastSlash + 1);
    }

    // --- CLI parsing ------------------------------------------------------------------------
    //
    // cpp parity: BlibBuilder.cpp:215 parseCommandArgs / BlibBuilder.cpp:503 parseNextSwitch /
    // BlibBuilder.cpp:97 usage. The C# port adapts the cpp argv layout (argv[0] = program
    // name) to the C# Main(args) convention (no program name).

    /// <summary>
    /// cpp <c>STDIN_LIST</c> at BlibBuilder.h:131. Which kind of list (filenames /
    /// unmodified sequences / modified sequences) we're expected to read from stdin
    /// once parseCommandArgs is done with the switch loop.
    /// </summary>
    private enum StdinList
    {
        Filenames,
        UnmodifiedSequences,
        ModifiedSequences,
    }

    // cpp parity: BlibBuilder.h:143 — queue<STDIN_LIST> driven by the `-s`, `-u`, `-U`
    // switches; consumed at the bottom of parseCommandArgs to actually read stdin.
    private readonly Queue<StdinList> _stdinput = new();

    // cpp parity: BlibBuilder.h:144 — stdin replacement when `-S <file>` is supplied.
    // Null => read from Console.In. Owned by the builder; closed at the success-path end of
    // ParseCommandArgs AND in Dispose so an exception mid-parse can't leak the FileStream.
    private TextReader? _stdinStream;

    /// <summary>Disposes the SQLite connection AND the stdin-replacement file (if any).</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stdinStream?.Dispose();
            _stdinStream = null;
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// cpp <c>BlibBuilder::usage</c> at BlibBuilder.cpp:97. Prints the help text to stderr
    /// and exits with status 1. cpp parity: matches the cpp output verbatim.
    /// </summary>
    public override void Usage()
    {
        var supported = string.Join("|*", _supportedTypes);
        var usage =
            $"Usage: BlibBuild [options] <*{supported}>+ <library_name>\n" +
            "   -o                Overwrite existing library. Default append.\n" +
            "   -S  <filename>    Read from file as though it were stdin.\n" +
            "   -s                Result file names from stdin. e.g. ls *sqt | BlibBuild -s new.blib.\n" +
            "   -u                Ignore peptides except those with the unmodified sequences from stdin.\n" +
            "   -U                Ignore peptides except those with the modified sequences from stdin.\n" +
            "   -H                Use more than one decimal place when describing mass modifications.\n" +
            "   -C  <file size>   Minimum file size required to use caching for .dat files.  Specifiy units as B,K,G or M.  Default 800M.\n" +
            "   -c <cutoff>       Score threshold (0-1) for PSMs to be included in library. Higher threshold is more exclusive.\n" +
            "   -v  <level>       Level of output to stderr (silent, error, status, warn).  Default warn.\n" +
            "   -T                Add prefixes to log output showing time elapsed.\n" +
            "   -L                Write status and warning messages to log file.\n" +
            "   -m <size>         SQLite memory cache size in Megs. Default 250M.\n" +
            "   -l <level>        ZLib compression level (0-?). Default 3.\n" +
            "   -i <library_id>   LSID library ID. Default uses file name.\n" +
            "   -a <authority>    LSID authority. Default proteome.gs.washington.edu.\n" +
            "   -x <filename>     Specify the path of XML modifications file for parsing MaxQuant files.\n" +
            "   -p <filename>     Specify the path of XML parameters file for parsing MaxQuant files.\n" +
            "   -P <float>        Specify pusher interval for Waters final_fragment.csv files.\n" +
            "   -d [<filename>]   Document the .blib format by writing SQLite commands to a file, or stdout if no filename is given.\n" +
            "   -E                Prefer reading peaks from embedded spectra (currently only affects MaxQuant msms.txt)\n" +
            "   -A                Output messages noting ambiguously matched spectra (spectra matched to multiple peptides)\n" +
            "   -K                Keep ambiguously matched spectra\n" +
            "   -t                Only output score types (no library build).\n" +
            "   -z <charges>      Only output PSMs with these charges, e.g. \"2,3\".\n";
        Console.Error.WriteLine(usage);
        Environment.Exit(1);
    }

    /// <summary>
    /// cpp <c>BlibBuilder::parseCommandArgs</c> at BlibBuilder.cpp:215. Calls the base
    /// (BlibMaker) parser to consume short option switches, then walks the trailing
    /// positional args as input files (validating each against <see cref="_supportedTypes"/>).
    /// If `-s` / `-u` / `-U` switches were seen, reads the corresponding lists from
    /// <see cref="_stdinStream"/> or <see cref="Console.In"/>.
    /// </summary>
    /// <param name="argv">Argv from Main, NOT including any program name.</param>
    /// <returns>The argv index that <see cref="BlibMaker.ParseCommandArgs"/> stopped at.</returns>
    public override int ParseCommandArgs(string[] argv)
    {
        ArgumentNullException.ThrowIfNull(argv);

        // cpp parity: BlibBuilder.cpp:217 — defer to BlibMaker for the switch loop +
        // library-name handling. BlibMaker pops the last arg as the library name unless
        // we're in score-lookup mode.
        var i = base.ParseCommandArgs(argv);

        // cpp parity: BlibBuilder.cpp:219 — if not score-lookup, argv length effectively
        // shrinks by 1 (the library name was consumed). We mirror by capping the input-file
        // walk at argv.Length - 1.
        var effectiveLength = IsScoreLookupMode ? argv.Length : argv.Length - 1;

        // cpp parity: BlibBuilder.cpp:222-265 — consume the stdinput queue (filled by the
        // -s, -u, -U switches). Each entry triggers a read of either filenames or sequences.
        var filesFromStdin = false;
        var stdin = _stdinStream ?? Console.In;
        while (_stdinput.Count > 0)
        {
            var kind = _stdinput.Dequeue();
            switch (kind)
            {
                case StdinList.Filenames:
                    // cpp parity: BlibBuilder.cpp:225-251 — read filenames until EOF or
                    // blank line; honour the optional `score_threshold=<x>` suffix.
                    filesFromStdin = true;
                    Verbosity.Debug("Reading input filenames");
                    while (true)
                    {
                        var line = stdin.ReadLine();
                        if (line is null) break;
                        var infileName = line.Trim();
                        if (infileName.Length == 0) break;

                        const string thresholdSearch = "score_threshold=";
                        var thresholdIdx = infileName.LastIndexOf(thresholdSearch, StringComparison.Ordinal);
                        if (thresholdIdx < 0)
                        {
                            Verbosity.Debug($"Input file: {infileName}");
                            AddInputFile(infileName);
                        }
                        else
                        {
                            var thresholdStr = infileName.Substring(thresholdIdx + thresholdSearch.Length);
                            infileName = infileName.Substring(0, thresholdIdx).Trim();
                            var threshold = double.Parse(thresholdStr, CultureInfo.InvariantCulture);
                            Verbosity.Debug($"Input file: {infileName} (cutoff = {threshold:0.00})");
                            AddInputFile(infileName, threshold);
                        }
                    }
                    break;

                case StdinList.UnmodifiedSequences:
                    // cpp parity: BlibBuilder.cpp:253 — read lines as unmodified peptides.
                    Verbosity.Debug("Reading target unmodified sequences");
                    ReadSequences(stdin, ref _targetSequences, modified: false);
                    break;

                case StdinList.ModifiedSequences:
                    // cpp parity: BlibBuilder.cpp:257 — read lines as modified peptides.
                    Verbosity.Debug("Reading target modified sequences");
                    ReadSequences(stdin, ref _targetSequencesModified, modified: true);
                    break;
            }
        }

        // cpp parity: BlibBuilder.cpp:267 — close the stdin-replacement file if any.
        if (_stdinStream != null)
        {
            _stdinStream.Dispose();
            _stdinStream = null;
        }

        if (!filesFromStdin)
        {
            // cpp parity: BlibBuilder.cpp:273 — at least one positional input file is required.
            var nInputs = effectiveLength - i;
            if (nInputs < 1)
            {
                Verbosity.Comment(VerbosityLevel.Error,
                    "Not enough arguments. Missing input files (" + string.Join(", ", _supportedTypes) +
                    ".), or no output file specified.");
                Usage();
                throw new BlibException(false, "Missing input files."); // unreachable; Usage exits.
            }

            for (var j = i; j < effectiveLength; j++)
            {
                var fileName = argv[j];
                if (!IsSupportedType(fileName))
                {
                    Verbosity.Error(
                        $"Unsupported file type '{fileName}'.  Must be one of {string.Join(", ", _supportedTypes)}.");
                }
                AddInputFile(fileName);
            }
        }

        return i;
    }

    /// <summary>
    /// cpp <c>BlibBuilder::parseNextSwitch</c> at BlibBuilder.cpp:503. Handles BlibBuilder's
    /// own short flags first (<c>-o</c>, <c>-s</c>, <c>-S</c>, <c>-c</c>, <c>-l</c>,
    /// <c>-C</c>, <c>-v</c>, <c>-T</c>, <c>-x</c>, <c>-p</c>, <c>-P</c>, <c>-u</c>,
    /// <c>-U</c>, <c>-L</c>, <c>-A</c>, <c>-K</c>, <c>-H</c>, <c>-E</c>) and falls
    /// back to <see cref="BlibMaker.ParseNextSwitch"/> for the shared ones.
    /// </summary>
    protected override int ParseNextSwitch(int i, string[] argv)
    {
        ArgumentNullException.ThrowIfNull(argv);
        if (i < 0 || i >= argv.Length) throw new ArgumentOutOfRangeException(nameof(i));

        var arg = argv[i];
        var switchName = arg[1];

        switch (switchName)
        {
            case 'o':
                // cpp parity: BlibBuilder.cpp:508 — overwrite existing library.
                Overwrite = true;
                break;

            case 's':
                // cpp parity: BlibBuilder.cpp:510 — push FILENAMES onto the stdin queue.
                _stdinput.Enqueue(StdinList.Filenames);
                break;

            case 'S':
                // cpp parity: BlibBuilder.cpp:512 — `-S <file>` replaces stdin with <file>.
                if (++i < argv.Length)
                {
                    try
                    {
                        _stdinStream = new StreamReader(argv[i]);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
                    {
                        Verbosity.Error($"Could not open file {argv[i]} as stdin.");
                    }
                }
                break;

            case 'c':
                // cpp parity: BlibBuilder.cpp:518 — `-c <cutoff>` sets explicitCutoff.
                if (++i < argv.Length)
                {
                    if (!double.TryParse(argv[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var cutoff))
                        throw new BlibException(false, $"Invalid -c cutoff value '{argv[i]}'.");
                    ExplicitCutoff = cutoff;
                }
                break;

            case 'l':
                // cpp parity: BlibBuilder.cpp:520 — `-l <level>` zlib compression level.
                if (++i < argv.Length)
                {
                    if (!int.TryParse(argv[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var lvl))
                        throw new BlibException(false, $"Invalid -l compression level '{argv[i]}'.");
                    LevelCompress = lvl;
                }
                break;

            case 'C':
                // cpp parity: BlibBuilder.cpp:522 — `-C <size>` with last-char unit (B/K/M/G).
                if (++i < argv.Length)
                {
                    var token = argv[i];
                    if (token.Length < 2)
                        Verbosity.Error($"File sizes must end in B, K, M or G. '{token}' is invalid.");
                    var numPart = token.Substring(0, token.Length - 1);
                    var unit = token[^1];
                    if (!int.TryParse(numPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                        Verbosity.Error($"File sizes must end in B, K, M or G. '{token}' is invalid.");
                    long bytes = unit switch
                    {
                        'B' or 'b' => value,
                        'K' or 'k' => (long)value * 1000L,
                        'M' or 'm' => (long)value * 1_000_000L,
                        'G' or 'g' => (long)value * 1_000_000_000L,
                        _ => -1L,
                    };
                    if (bytes < 0)
                        Verbosity.Error($"File sizes must end in B, K, M or G. '{token}' is invalid.");
                    FileSizeThresholdForCaching = bytes;
                }
                break;

            case 'v':
                // cpp parity: BlibBuilder.cpp:538 — BlibBuilder overrides BlibMaker's bare-flag
                // -v with `-v <level>` (silent/error/status/warn/debug/detail/all).
                if (++i < argv.Length)
                {
                    Verbosity.GlobalLevel = Verbosity.StringToLevel(argv[i]);
                }
                break;

            case 'T':
                // cpp parity: BlibBuilder.cpp:541 — prepend elapsed-time to log lines.
                Verbosity.TimestampEnabled = true;
                break;

            case 'x':
                // cpp parity: BlibBuilder.cpp:543 — MaxQuant mods XML.
                if (++i < argv.Length)
                    MaxQuantModsPath = argv[i];
                break;

            case 'p':
                // cpp parity: BlibBuilder.cpp:545 — MaxQuant params XML.
                if (++i < argv.Length)
                    MaxQuantParamsPath = argv[i];
                break;

            case 'P':
                // cpp parity: BlibBuilder.cpp:547 — Waters pusher interval.
                if (++i < argv.Length)
                {
                    if (!double.TryParse(argv[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var pusher))
                        throw new BlibException(false, $"Invalid -P pusher interval '{argv[i]}'.");
                    ForcedPusherInterval = pusher;
                }
                break;

            case 'u':
                // cpp parity: BlibBuilder.cpp:549 — push UNMODIFIED_SEQUENCES.
                _stdinput.Enqueue(StdinList.UnmodifiedSequences);
                break;

            case 'U':
                // cpp parity: BlibBuilder.cpp:551 — push MODIFIED_SEQUENCES.
                _stdinput.Enqueue(StdinList.ModifiedSequences);
                break;

            case 'L':
                // cpp parity: BlibBuilder.cpp:553 — open log file.
                Verbosity.OpenLogfile();
                break;

            case 'A':
                // cpp parity: BlibBuilder.cpp:555 — ambiguity messages.
                AmbiguityMessages = true;
                break;

            case 'K':
                // cpp parity: BlibBuilder.cpp:557 — keep ambiguous matches.
                KeepAmbiguous = true;
                break;

            case 'H':
                // cpp parity: BlibBuilder.cpp:559 — high-precision modification masses.
                HighPrecisionModifications = true;
                break;

            case 'E':
                // cpp parity: BlibBuilder.cpp:561 — prefer embedded spectra.
                PreferEmbeddedSpectra = true;
                break;

            default:
                // cpp parity: BlibBuilder.cpp:563 — fall back to base for everything else
                // (the shared -v/-m/-a/-i/-d/-t/-z flags).
                return base.ParseNextSwitch(i, argv);
        }

        // cpp parity: BlibBuilder.cpp:567 returns min(argc, i + 1).
        return Math.Min(argv.Length, i + 1);
    }

    /// <summary>
    /// cpp <c>BlibBuilder::readSequences</c> at BlibBuilder.cpp:575. Reads peptide
    /// sequences (one per line, blank line / EOF terminates) into the given set,
    /// optionally normalising via <see cref="ParseSequence"/> for modified sequences.
    /// </summary>
    private static int ReadSequences(TextReader stdin, ref HashSet<string>? seqSet, bool modified)
    {
        seqSet ??= new HashSet<string>(StringComparer.Ordinal);

        var sequencesRead = 0;
        while (true)
        {
            var sequence = stdin.ReadLine();
            if (sequence is null) break;
            if (sequence.Length == 0) break;

            var newSeq = ParseSequence(sequence, modified);
            Verbosity.Debug($"Adding target sequence {newSeq}");
            seqSet.Add(newSeq);
            sequencesRead++;
        }
        return sequencesRead;
    }

    /// <summary>
    /// cpp <c>BlibBuilder::parseSequence</c> at BlibBuilder.cpp:601. Strip non-letter
    /// characters, optionally extracting <c>[+mass]</c> modifications when
    /// <paramref name="modified"/> is true. For modified sequences, builds the
    /// low-precision form (matching BuildParser::filterBySequence).
    /// </summary>
    public static string ParseSequence(string sequence, bool modified)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        var newSeq = new StringBuilder();
        var unexpected = new StringBuilder();
        var mods = new List<SeqMod>();
        var aaPosition = 0;

        for (var i = 0; i < sequence.Length; i++)
        {
            var ch = sequence[i];
            if (char.IsLetter(ch))
            {
                newSeq.Append(char.ToUpperInvariant(ch));
                aaPosition++;
            }
            else if (modified && ch == '[')
            {
                var endIdx = sequence.IndexOf(']', i + 1);
                if (endIdx >= 0)
                {
                    i++;
                    var massStr = sequence.Substring(i, endIdx - i);
                    if (double.TryParse(massStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var deltaMass))
                    {
                        mods.Add(new SeqMod(aaPosition, deltaMass));
                    }
                    else
                    {
                        Verbosity.Warn($"Could not read '{massStr}' as a mass in target sequence {sequence}, skipping this modification");
                    }
                    i = endIdx;
                }
                else
                {
                    Verbosity.Warn($"Ignoring opening bracket without closing bracket in target sequence {sequence}");
                }
            }
            else
            {
                unexpected.Append(ch);
            }
        }

        if (unexpected.Length > 0)
        {
            Verbosity.Warn($"Ignoring unexpected characters {unexpected} in target sequence {sequence}");
        }

        var result = newSeq.ToString();
        if (modified)
        {
            // cpp parity: BlibBuilder.cpp:652 — low-precision form for the modified set.
            result = GetLowPrecisionModSeq(result, mods);
        }
        return result;
    }
}
