// Port of pwiz_tools/BiblioSpec/src/BlibMaker.{h,cpp}
//
// Faithful C# port of BiblioSpec::BlibMaker. CLI plumbing (parseCommandArgs / usage /
// parseNextSwitch) is intentionally omitted — those move to BlibBuild.exe later.
// Compression is the .NET 8 ZLibStream, which produces zlib-format (RFC 1950) output
// matching the cpp `compress()` zlib wrapper; the "if compressed >= uncompressed,
// store uncompressed" rule is preserved verbatim.

using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO.Compression;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Builds (or appends to) a BiblioSpec <c>.blib</c> SQLite library. Port of
/// <c>BiblioSpec::BlibMaker</c> at <c>pwiz_tools/BiblioSpec/src/BlibMaker.{h,cpp}</c>.
/// </summary>
/// <remarks>
/// <para>
/// BlibBuilder and BlibFilter (cpp) inherit from BlibMaker; the corresponding C# subclasses
/// will override <see cref="AttachAll"/> and <see cref="GetLsid"/> the same way.
/// </para>
/// <para>
/// CLI parsing (cpp <c>parseCommandArgs</c> / <c>parseNextSwitch</c> / <c>usage</c>) is NOT
/// ported here — those move to the BlibBuild executable. Everything else from BlibMaker.h
/// is reproduced.
/// </para>
/// </remarks>
public class BlibMaker : IDisposable
{
    // SQLite uses 1.5K pages; PRAGMA cache_size is expressed in pages. cpp BlibMaker.cpp:39.
    private const int PagesPerMeg = (int)(1024.0 / 1.5);

    // cpp: static const char ERROR_GENERIC[] = "Unexpected failure.";
    private const string ErrorGeneric = "Unexpected failure.";

    private SQLiteConnection? _db;
    private string? _libName;
    private string? _libId;
    private string _authority = "proteome.gs.washington.edu";
    private int _cacheSize = 250 * PagesPerMeg;
    private bool _redundant = true;
    private bool _overwrite;
    private string _message = ErrorGeneric;

    // cpp parity: file -> (id, cutoffScore). Preserved as a managed map; the cpp uses
    // std::map<string, pair<int, double>> — semantics: re-insert on cutoff mismatch
    // sets the stored cutoff to -1.
    private readonly Dictionary<string, (int Id, double Cutoff)> _fileIdCache = new(StringComparer.Ordinal);

    // cpp parity: old fileID (in temp/attached schema) -> new fileID (in main).
    private readonly Dictionary<int, int> _oldToNewFileId = new();

    // cpp parity: sentinel for "no UNKNOWN row inserted yet". Set to -1 by default.
    private int _unknownFileId = -1;

    /// <summary>Sentinel returned from <see cref="GetUnknownFileId"/> before an UNKNOWN row exists.</summary>
    internal const int UnknownFileIdSentinel = -1;

    /// <summary>Constructs a BlibMaker. Call <see cref="OpenDb"/> + <see cref="Init"/> to begin.</summary>
    public BlibMaker()
    {
    }

    /// <summary>Disposes the underlying SQLite connection.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes the underlying SQLite connection.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && _db != null)
        {
            _db.Dispose();
            _db = null;
        }
    }

    /// <summary>Open <see cref="SQLiteConnection"/> for the library being built. cpp <c>getDb</c>.</summary>
    public SQLiteConnection Db => _db ?? throw new InvalidOperationException("Db not open. Call OpenDb first.");

    /// <summary>True if the underlying SQLite connection has been opened.</summary>
    /// <remarks>cpp parity: cpp's <c>sqlite3_prepare</c> on a NULL db returns an error code
    /// and silently no-ops; the cpp BuildParser ctor relies on that to construct a temporary
    /// builder (e.g. for SQT-mod-table parsing in PercolatorXmlReader). The C# port throws on
    /// access, so subclasses use this property to short-circuit before touching the DB.</remarks>
    public bool IsDbOpen => _db != null;

    /// <summary>The library file name / path. cpp <c>getLibName</c>.</summary>
    public string? LibName => _libName;

    /// <summary>True when running in score-lookup mode (cpp <c>-t</c> switch sets this).</summary>
    public bool IsScoreLookupMode { get; set; }

    /// <summary>cpp <c>setMessage</c> / <c>message</c> — appended into SQL failure diagnostics.</summary>
    public string Message
    {
        get => _message;
        set => _message = value ?? ErrorGeneric;
    }

    /// <summary>cpp <c>isOverwrite</c> / <c>setOverwrite</c>.</summary>
    public bool Overwrite
    {
        get => _overwrite;
        set => _overwrite = value;
    }

    /// <summary>cpp <c>isRedundant</c> / <c>setRedundant</c>.</summary>
    public bool Redundant
    {
        get => _redundant;
        set => _redundant = value;
    }

    /// <summary>cpp <c>ambiguityMessages_</c> protected field.</summary>
    public bool AmbiguityMessages { get; set; }

    /// <summary>cpp <c>keepAmbiguous_</c> protected field.</summary>
    public bool KeepAmbiguous { get; set; }

    /// <summary>cpp <c>highPrecisionModifications_</c> protected field.</summary>
    public bool HighPrecisionModifications { get; set; }

    /// <summary>cpp <c>preferEmbeddedSpectra_</c> — <c>boost::optional&lt;bool&gt;</c> → C# nullable bool.</summary>
    public bool? PreferEmbeddedSpectra { get; set; }

    /// <summary>cpp <c>verbose</c>.</summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// cpp <c>precursorCharges_</c> — when non-empty, only emit entries whose precursor
    /// charge is listed here (cpp <c>-z</c> option).
    /// </summary>
    public HashSet<int> PrecursorCharges { get; } = new();

    /// <summary>
    /// LSID authority string (cpp default <c>"proteome.gs.washington.edu"</c>, overridable via
    /// the cpp <c>-a</c> switch).
    /// </summary>
    public string Authority
    {
        get => _authority;
        set => _authority = string.IsNullOrEmpty(value) ? "proteome.gs.washington.edu" : value;
    }

    /// <summary>
    /// Library identifier appended to the LSID (cpp <c>-i</c> switch overrides; otherwise
    /// derived from the library file basename).
    /// </summary>
    public string? LibId
    {
        get => _libId;
        set => _libId = value;
    }

    /// <summary>SQLite cache size in pages (cpp <c>-m</c> switch sets megabytes).</summary>
    public int CacheSizeMb
    {
        get => _cacheSize / PagesPerMeg;
        set
        {
            if (value <= 0) throw new BlibException(false, "Invalid cache size specified.");
            _cacheSize = value * PagesPerMeg;
        }
    }

    /// <summary>
    /// cpp <c>verifyFileExists</c>. Throws <see cref="BlibException"/> with hasFilename=true
    /// on missing / unreadable file.
    /// </summary>
    public static void VerifyFileExists(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        if (!File.Exists(file))
        {
            var absolute = Path.GetFullPath(file);
            throw new BlibException(true, $"Library file '{absolute}' cannot be opened: file does not exist");
        }

        try
        {
            // cpp parity: BlibMaker.cpp:159 uses std::ifstream, which shares read+write, so the
            // check succeeds even when another process holds the file open (e.g. Skyline has this
            // .blib loaded as a document library while the Build Library dialog queries its score
            // types). File.OpenRead uses FileShare.Read and would spuriously throw a sharing
            // violation against that writer, flagging a score-type error that hangs the dialog.
            using var test = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
        catch (IOException)
        {
            throw new BlibException(true, $"Library file '{file}' cannot be opened");
        }
        catch (UnauthorizedAccessException)
        {
            throw new BlibException(true, $"Library file '{file}' cannot be opened");
        }
    }

    /// <summary>
    /// Set the library file path. Derives <see cref="LibId"/> from the basename like cpp's
    /// <c>libIdFromName</c>.
    /// </summary>
    public void SetLibName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _libName = name;
        _libId = LibIdFromName(name);
    }

    /// <summary>
    /// cpp <c>openDb</c>. Opens or creates the SQLite file. Throws via <see cref="Verbosity.Error"/>
    /// on failure.
    /// </summary>
    public void OpenDb(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        try
        {
            _db = SqliteRoutine.Open(file, readOnly: false);
        }
        catch (Exception ex) when (ex is SQLiteException or InvalidOperationException)
        {
            Verbosity.Error($"Failed to create '{file}'. Make sure the directory exists with write permissions.");
        }

        if (_libName is null)
        {
            SetLibName(file);
        }
    }

    /// <summary>
    /// cpp <c>init</c>. Decides overwrite vs append, opens the db, sets PRAGMAs, then calls
    /// <see cref="AttachAll"/> followed by either <see cref="CreateTables"/> or
    /// <see cref="UpdateTables"/>.
    /// </summary>
    public virtual void Init()
    {
        if (IsScoreLookupMode)
            return;

        if (_libName is null)
            throw new BlibException(false, "Library name has not been set; call SetLibName first.");

        // cpp parity: empty / non-existent file => overwrite. Existing non-empty file =>
        // remove if overwrite was already requested, else append.
        if (!File.Exists(_libName))
        {
            _overwrite = true;
        }
        else if (_overwrite)
        {
            File.Delete(_libName);
            if (File.Exists(_libName))
            {
                Verbosity.Error($"Failed to remove existing redundant library '{_libName}'.");
            }
        }
        else
        {
            // cpp parity: zero-length file is treated as overwrite candidate.
            var info = new FileInfo(_libName);
            if (info.Length == 0)
                _overwrite = true;
        }

        OpenDb(_libName);

        _message = "Failed to initialize " + _libName;

        SqlStmt("PRAGMA synchronous=OFF");
        SqlStmt($"PRAGMA cache_size={_cacheSize.ToString(CultureInfo.InvariantCulture)}");
        SqlStmt("PRAGMA temp_store=MEMORY");

        AttachAll();

        var commands = new List<string>();
        if (_overwrite)
        {
            CreateTables(commands, execute: true);
        }
        else
        {
            // cpp parity: drop indexes for bulk inserts. Ignore failures (the index may not exist).
            foreach (var idxName in BlibSchema.AllIndexNames)
                SqlStmt($"DROP INDEX {idxName}", ignoreFailure: true);

            UpdateTables();
        }

        _message = ErrorGeneric;
    }

    /// <summary>
    /// cpp <c>is_empty</c>. True when the library has no RefSpectra rows.
    /// </summary>
    public virtual bool IsEmpty() => GetSpectrumCount() == 0;

    /// <summary>
    /// cpp <c>abort_current_library</c>. Closes the connection and deletes the file.
    /// </summary>
    public virtual void AbortCurrentLibrary()
    {
        Verbosity.Debug("Deleting current library.");

        if (_db != null)
        {
            _db.Dispose();
            _db = null;
        }

        if (!string.IsNullOrEmpty(_libName) && File.Exists(_libName))
        {
            File.Delete(_libName);
        }
    }

    /// <summary>
    /// cpp <c>beginTransaction</c>. Commits any open transaction, then BEGIN. Uses raw SQL
    /// rather than <c>SQLiteConnection.BeginTransaction</c> so behaviour matches cpp's
    /// autocommit-mode toggling exactly.
    /// </summary>
    public void BeginTransaction()
    {
        // cpp parity: use sqlite3_get_autocommit to detect an existing explicit transaction.
        // We approximate by querying via PRAGMA; simpler is to attempt COMMIT with
        // ignoreFailure=true, then BEGIN.
        SqlStmt("COMMIT", ignoreFailure: true);
        SqlStmt("BEGIN");
    }

    /// <summary>
    /// cpp <c>endTransaction</c>. COMMIT, ignoring failure if no transaction is active.
    /// </summary>
    public void EndTransaction()
    {
        // cpp logs "No open transaction to end" then returns; we let SQLite report and ignore.
        SqlStmt("COMMIT", ignoreFailure: true);
    }

    /// <summary>
    /// cpp <c>undoActiveTransaction</c>. ROLLBACK, ignoring failure if no transaction is active.
    /// </summary>
    public void UndoActiveTransaction()
    {
        SqlStmt("ROLLBACK", ignoreFailure: true);
    }

    /// <summary>
    /// cpp <c>commit</c>. Updates LibInfo, then creates the indexes inside a transaction.
    /// </summary>
    public virtual void Commit()
    {
        UpdateLibInfo();

        SqlStmt("BEGIN");
        foreach (var idx in BlibSchema.AllIndexStatements)
            SqlStmt(idx);
        SqlStmt("COMMIT");
    }

    /// <summary>cpp <c>getLSID</c>. <c>urn:lsid:&lt;authority&gt;:spectral_library:bibliospec:...</c>.</summary>
    protected virtual string GetLsid()
    {
        var libType = _redundant ? "redundant" : "nr";
        return $"urn:lsid:{_authority}:spectral_library:bibliospec:{libType}:{_libId ?? string.Empty}";
    }

    /// <summary>
    /// Virtual no-op hook called from <see cref="Init"/>. BlibBuilder / BlibFilter overrides
    /// attach the temp / source databases here.
    /// </summary>
    protected virtual void AttachAll()
    {
    }

    /// <summary>
    /// cpp <c>createTables</c>. Generates the canonical schema; optionally executes each
    /// statement against the open db.
    /// </summary>
    public virtual void CreateTables(List<string> commands, bool execute)
    {
        ArgumentNullException.ThrowIfNull(commands);

        // LibInfo + bootstrap INSERT (matches cpp order: CREATE LibInfo, INSERT LibInfo,
        // CREATE RefSpectra, CREATE Modifications, CREATE RefSpectraPeaks, then the
        // named tables via createTable).
        commands.Add(BlibSchema.CreateLibInfo);

        // cpp uses %.24s of ctime() to drop the trailing newline. The C# equivalent: format
        // DateTime.Now in the same "Ddd MMM dd HH:mm:ss yyyy" shape using invariant culture.
        // cpp parity: ctime() emits "Ddd Mmm DD HH:MM:SS YYYY" with the DAY space-padded
        // (e.g. "Thu Nov  3 17:02:18 2017"). C# "dd" zero-pads; build the day manually.
        var now = DateTime.Now;
        var createTime = string.Format(
            CultureInfo.InvariantCulture,
            "{0:ddd} {0:MMM} {1,2} {0:HH:mm:ss yyyy}",
            now, now.Day);
        var blibLsid = GetLsid();
        // cpp parity: BlibMaker.cpp:353 uses sprintf with %s; we escape apostrophes defensively
        // because the LSID derives from the (user-supplied) library file basename.
        commands.Add(string.Format(
            CultureInfo.InvariantCulture,
            "INSERT INTO LibInfo values('{0}','{1}',{2},{3},{4})",
            SqliteRoutine.EscapeApostrophes(blibLsid),
            SqliteRoutine.EscapeApostrophes(createTime),
            -1, // cpp parity: -1 means 'not counted yet'
            BlibSchema.CurrentMajorVersion,
            BlibSchema.CurrentMinorVersion));

        commands.Add(BlibSchema.CreateRefSpectra);
        commands.Add(BlibSchema.CreateModifications);
        commands.Add(BlibSchema.CreateRefSpectraPeaks);

        CreateTable("Proteins", commands, execute: false);
        CreateTable("RefSpectraProteins", commands, execute: false);
        CreateTable("RefSpectraPeakAnnotations", commands, execute: false);
        CreateTable("SpectrumSourceFiles", commands, execute: false);
        CreateTable("ScoreTypes", commands, execute: false);
        CreateTable("IonMobilityTypes", commands, execute: false);

        if (execute)
        {
            foreach (var stmt in commands)
                SqlStmt(stmt);
        }
    }

    /// <summary>
    /// cpp <c>createTable</c>. Emits the CREATE for one named table plus, for ScoreTypes and
    /// IonMobilityTypes, the seed INSERTs. Optionally executes them.
    /// </summary>
    public virtual void CreateTable(string tableName, List<string> commands, bool execute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(commands);

        var initialCount = commands.Count;
        switch (tableName)
        {
            case "SpectrumSourceFiles":
                commands.Add(BlibSchema.CreateSpectrumSourceFiles);
                break;

            case "ScoreTypes":
                commands.Add(BlibSchema.CreateScoreTypes);
                // Seed: one row per PsmScoreType ordinal. cpp BlibMaker.cpp:441-446.
                var scoreTypeCount = Enum.GetValues<PsmScoreType>().Length;
                for (var i = 0; i < scoreTypeCount; i++)
                {
                    var scoreType = (PsmScoreType)i;
                    commands.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "INSERT INTO ScoreTypes(id, scoreType, probabilityType) VALUES({0}, '{1}', '{2}')",
                        i,
                        BlibUtils.ScoreTypeToString(scoreType),
                        BlibUtils.ScoreTypeToProbabilityTypeString(scoreType)));
                }
                break;

            case "IonMobilityTypes":
                commands.Add(BlibSchema.CreateIonMobilityTypes);
                // Seed: one row per IonMobilityType ordinal. cpp BlibMaker.cpp:456-460.
                var imTypeCount = Enum.GetValues<IonMobilityType>().Length;
                for (var i = 0; i < imTypeCount; i++)
                {
                    var imType = (IonMobilityType)i;
                    commands.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "INSERT INTO IonMobilityTypes(id, ionMobilityType) VALUES({0}, '{1}')",
                        i,
                        BlibUtils.IonMobilityTypeToString(imType)));
                }
                break;

            case "RefSpectraPeakAnnotations":
                commands.Add(BlibSchema.CreateRefSpectraPeakAnnotations);
                break;

            case "Proteins":
                commands.Add(BlibSchema.CreateProteins);
                break;

            case "RefSpectraProteins":
                commands.Add(BlibSchema.CreateRefSpectraProteins);
                break;

            default:
                Verbosity.Error($"Cannot create '{tableName}' table. Unknown name.");
                return; // unreachable; Verbosity.Error throws
        }

        if (execute)
        {
            for (var i = initialCount; i < commands.Count; i++)
                SqlStmt(commands[i]);
        }
    }

    /// <summary>
    /// cpp <c>updateTables</c>. Adds missing tables / columns when appending to an existing
    /// library. Mirrors the full column-by-column ALTER list at BlibMaker.cpp:543-590.
    /// </summary>
    public virtual void UpdateTables()
    {
        var commands = new List<string>();

        if (!TableExists("main", "SpectrumSourceFiles"))
        {
            CreateTable("SpectrumSourceFiles", commands, execute: true);
            // cpp parity: seed the UNKNOWN source file id at insert time so we can map
            // legacy rows lacking a fileID.
            SqlStmt("INSERT INTO SpectrumSourceFiles (fileName, cutoffScore) VALUES ('UNKNOWN', -1)");
            _unknownFileId = (int)Db.LastInsertRowId;
        }
        else
        {
            _unknownFileId = GetUnknownFileId();
        }

        if (!TableExists("main", "ScoreTypes"))
            CreateTable("ScoreTypes", commands, execute: true);

        if (!TableExists("main", "RefSpectraPeakAnnotations"))
            CreateTable("RefSpectraPeakAnnotations", commands, execute: true);

        if (!TableExists("main", "Proteins"))
            CreateTable("Proteins", commands, execute: true);

        if (!TableExists("main", "RefSpectraProteins"))
            CreateTable("RefSpectraProteins", commands, execute: true);

        // cpp parity: full list of columns to ALTER-ADD if missing.
        var newColumns = new List<(string Name, string Type)>
        {
            ("retentionTime", "REAL"),
            ("startTime", "REAL"),
            ("endTime", "REAL"),
            ("fileID", "INTEGER"),
            ("SpecIDinFile", "VARCHAR(256)"),
            ("score", "REAL"),
            ("scoreType", "TINYINT"),
            ("collisionalCrossSectionSqA", "REAL"),
            ("ionMobility", "REAL"),
            ("ionMobilityHighEnergyOffset", "REAL"),
            ("ionMobilityType", "TINYINT"),
            ("totalIonCurrent", "REAL"),
        };

        foreach (var col in SmallMolMetadata.SqlColumns)
            newColumns.Add((col.Name, "VARCHAR(128)"));

        foreach (var (name, type) in newColumns)
        {
            if (!TableColumnExists("main", "RefSpectra", name))
                SqlStmt($"ALTER TABLE RefSpectra ADD {name} {type}");
        }

        if (!TableColumnExists("main", "SpectrumSourceFiles", "idFileName"))
            SqlStmt("ALTER TABLE SpectrumSourceFiles ADD COLUMN idFileName TEXT");

        if (!TableColumnExists("main", "SpectrumSourceFiles", "workflowType"))
        {
            SqlStmt("ALTER TABLE SpectrumSourceFiles ADD COLUMN workflowType TINYINT");
            SqlStmt("UPDATE SpectrumSourceFiles SET workflowType = 0"); // cpp parity: default DDA
        }

        // cpp parity: backfill nulls for fileID and scoreType.
        SqlStmt(string.Format(
            CultureInfo.InvariantCulture,
            "UPDATE RefSpectra SET fileID = '{0}' WHERE fileID IS NULL",
            _unknownFileId));
        SqlStmt(string.Format(
            CultureInfo.InvariantCulture,
            "UPDATE RefSpectra SET scoreType = '{0}' WHERE scoreType IS NULL",
            (int)PsmScoreType.UnknownScoreType));
    }

    /// <summary>
    /// cpp <c>getUnknownFileId</c>. Returns the id of the SpectrumSourceFiles row named
    /// <c>'UNKNOWN'</c>, or -1 if no such row (or no table) exists.
    /// </summary>
    public int GetUnknownFileId()
    {
        if (!TableExists("main", "SpectrumSourceFiles"))
            return UnknownFileIdSentinel;

        using var cmd = Db.CreateCommand();
        cmd.CommandText = "SELECT id FROM SpectrumSourceFiles WHERE fileName = 'UNKNOWN'";
        var result = cmd.ExecuteScalar();
        if (result is null || result is DBNull) return UnknownFileIdSentinel;
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// cpp <c>updateLibInfo</c>. Bumps majorVersion and refreshes numSpecs.
    /// </summary>
    protected virtual void UpdateLibInfo()
    {
        GetNextRevision(out var dataRev);
        var spectrumCount = CountSpectra();
        SqlStmt(string.Format(
            CultureInfo.InvariantCulture,
            "UPDATE LibInfo SET numSpecs={0}, majorVersion={1}",
            spectrumCount, dataRev));
    }

    /// <summary>cpp <c>getNextRevision</c>. Reads <c>majorVersion</c> and returns +1.</summary>
    protected virtual void GetNextRevision(out int dataRev)
    {
        GetRevisionInfo(null, out dataRev, out _);
        dataRev++;
    }

    /// <summary>
    /// cpp <c>getRevisionInfo</c>. Reads <c>majorVersion</c> / <c>minorVersion</c> from
    /// the named schema's LibInfo (null = main).
    /// </summary>
    public void GetRevisionInfo(string? schemaName, out int dataRev, out int schemaVer)
    {
        var sql = string.IsNullOrEmpty(schemaName)
            ? "SELECT majorVersion, minorVersion FROM LibInfo"
            : $"SELECT majorVersion, minorVersion FROM {schemaName}.LibInfo";

        using var cmd = Db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            FailSql(0, sql, null, _message);

        dataRev = reader.GetInt32(0);
        schemaVer = reader.GetInt32(1);
    }

    /// <summary>
    /// cpp <c>getSpectrumCount</c>. Reads <c>LibInfo.numSpecs</c>; falls back to
    /// <see cref="CountSpectra"/> when the cached count is -1.
    /// </summary>
    public int GetSpectrumCount(string? databaseName = null)
    {
        var sql = string.IsNullOrEmpty(databaseName)
            ? "SELECT numSpecs FROM LibInfo"
            : $"SELECT numSpecs FROM {databaseName}.LibInfo";

        var numSpec = -1;
        try
        {
            using var cmd = Db.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
                numSpec = reader.IsDBNull(0) ? -1 : reader.GetInt32(0);
        }
        catch (SQLiteException)
        {
            // cpp logs "Failed to get spectrum count, so count them."
            Verbosity.Debug("Failed to get spectrum count, so count them.");
        }

        if (numSpec == -1)
            numSpec = CountSpectra(databaseName);

        return numSpec;
    }

    /// <summary>
    /// cpp <c>countSpectra</c>. <c>SELECT count(*)</c> directly from RefSpectra in the named
    /// (or main) schema.
    /// </summary>
    public int CountSpectra(string? databaseName = null)
    {
        var sql = string.IsNullOrEmpty(databaseName)
            ? "SELECT count(*) FROM RefSpectra"
            : $"SELECT count(*) FROM {databaseName}.RefSpectra";

        Verbosity.Debug("About to submit count statement.");
        using var cmd = Db.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        Verbosity.Debug("Done counting.");
        if (result is null || result is DBNull)
            FailSql(0, sql, null, "Failed getting spectrum count.");
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    /// <summary>Default cutoff score: -1. Override in subclasses to provide a real filter.</summary>
    /// <remarks>cpp parity: protected in BlibMaker but called from BuildParser (a sibling
    /// type, not a subclass) via the BlibBuilder reference. We mark this <c>protected
    /// internal</c> so BuildParser (same assembly) can read it while still letting
    /// external subclasses override.</remarks>
    protected internal virtual double GetCutoffScore() => -1;

    /// <summary>
    /// cpp <c>tableExists</c>. True when the named schema contains the named table.
    /// </summary>
    public bool TableExists(string schemaTmp, string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaTmp);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        // cpp interpolates schema and uses "= ..." (no = 'main' bind). We follow suit
        // because schema and table names cannot be bound via parameters; both should be
        // trusted callers (BlibMaker internals).
        var sql = $"SELECT name FROM {schemaTmp}.sqlite_master WHERE name = '{SqliteRoutine.EscapeApostrophes(tableName)}'";
        using var cmd = Db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        return reader.Read();
    }

    /// <summary>
    /// cpp <c>tableColumnExists</c>. True when the given table in the given schema contains
    /// the named column. Uses <c>PRAGMA table_info</c> like cpp does.
    /// </summary>
    public bool TableColumnExists(string schemaTmp, string tableName, string columnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaTmp);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        using var cmd = Db.CreateCommand();
        cmd.CommandText = $"PRAGMA {schemaTmp}.table_info({tableName})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            // column 1 (0-based) is the column name in PRAGMA table_info output.
            if (string.Equals(reader.GetString(1), columnName, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// cpp <c>getFileId</c>. Looks up an existing SpectrumSourceFiles row by fileName,
    /// caching the (id, cutoff) pair. If the cached cutoff disagrees, the row's cutoff is
    /// reset to -1 (cpp parity at BlibMaker.cpp:1037-1041).
    /// </summary>
    public int GetFileId(string file, double cutoffScore)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (_fileIdCache.TryGetValue(file, out var cached))
        {
            // cpp parity: if cached cutoff disagrees with the requested cutoff, reset
            // SpectrumSourceFiles.cutoffScore to -1 ("unknown / heterogeneous").
            if (cutoffScore != cached.Cutoff && TableColumnExists("main", "SpectrumSourceFiles", "cutoffScore"))
            {
                SqlStmt("UPDATE SpectrumSourceFiles SET cutoffScore = -1 WHERE id = " +
                        cached.Id.ToString(CultureInfo.InvariantCulture));
            }
            return cached.Id;
        }

        var cutoffSelect = TableColumnExists("main", "SpectrumSourceFiles", "cutoffScore") ? "cutoffScore" : "-1";
        var sql = $"SELECT id, {cutoffSelect} FROM SpectrumSourceFiles WHERE filename = '" +
                  SqliteRoutine.EscapeApostrophes(file) + "'";

        using var cmd = Db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var fileId = reader.GetInt32(0);
            var cutoff = reader.IsDBNull(1) ? -1.0 : reader.GetDouble(1);
            _fileIdCache[file] = (fileId, cutoff);
            return fileId;
        }
        return -1;
    }

    /// <summary>
    /// cpp <c>addFile</c>. Inserts a SpectrumSourceFiles row and caches its new id. Adapts
    /// the column list based on whether <c>workflowType</c> / <c>cutoffScore</c> exist
    /// (cpp BlibMaker.cpp:1066-1082).
    /// </summary>
    public int AddFile(string specFile, double cutoffScore, string idFile, WorkflowType workflowType)
    {
        ArgumentNullException.ThrowIfNull(specFile);
        ArgumentNullException.ThrowIfNull(idFile);

        string sql;
        var specEsc = SqliteRoutine.EscapeApostrophes(specFile);
        var idEsc = SqliteRoutine.EscapeApostrophes(idFile);
        var cutoffStr = cutoffScore.ToString("R", CultureInfo.InvariantCulture);

        if (TableColumnExists("main", "SpectrumSourceFiles", "workflowType"))
        {
            sql = $"INSERT INTO SpectrumSourceFiles(fileName, idFileName, cutoffScore, workflowType) VALUES('{specEsc}', '{idEsc}', {cutoffStr}, {(int)workflowType})";
        }
        else if (TableColumnExists("main", "SpectrumSourceFiles", "cutoffScore"))
        {
            sql = $"INSERT INTO SpectrumSourceFiles(fileName, idFileName, cutoffScore) VALUES('{specEsc}', '{idEsc}', {cutoffStr})";
        }
        else
        {
            sql = $"INSERT INTO SpectrumSourceFiles(filename, idFileName) VALUES('{specEsc}', '{idEsc}')";
        }

        SqlStmt(sql);
        var newFileId = (int)Db.LastInsertRowId;
        _fileIdCache[specFile] = (newFileId, cutoffScore);
        return newFileId;
    }

    /// <summary>
    /// cpp <c>insertPeaks</c>. Encodes mz (doubles) and intensity (floats) blobs to little-endian
    /// bytes, optionally zlib-compresses each, and inserts a RefSpectraPeaks row. cpp parity:
    /// if zlib compression doesn't actually shrink the blob, the uncompressed form is stored.
    /// </summary>
    /// <param name="spectraId">RefSpectra.id this peaks row belongs to.</param>
    /// <param name="levelCompress">cpp <c>levelCompress</c>: 0 disables compression. Any other
    /// value (cpp passed it through to <c>compress</c> which used the default level) enables it.</param>
    /// <param name="peaksCount">Number of peaks. Zero is a no-op (matches cpp).</param>
    /// <param name="mz">Peak m/z values. Must contain at least <paramref name="peaksCount"/> entries.</param>
    /// <param name="intensity">Peak intensities. Must contain at least <paramref name="peaksCount"/> entries.</param>
    public void InsertPeaks(int spectraId, int levelCompress, int peaksCount,
        ReadOnlySpan<double> mz, ReadOnlySpan<float> intensity)
    {
        if (peaksCount == 0)
            return;
        if (mz.Length < peaksCount)
            throw new ArgumentException("mz array shorter than peaksCount", nameof(mz));
        if (intensity.Length < peaksCount)
            throw new ArgumentException("intensity array shorter than peaksCount", nameof(intensity));

        // Encode to little-endian byte arrays (cpp blob format is little-endian doubles / floats).
        var mzBytes = new byte[peaksCount * sizeof(double)];
        var intBytes = new byte[peaksCount * sizeof(float)];

        if (BitConverter.IsLittleEndian)
        {
            // Fast path: copy verbatim like cpp's compress((Bytef*)pM, ...). MemoryMarshal
            // reinterprets the span without copying, then we slice + CopyTo into the byte buf.
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(mz[..peaksCount]).CopyTo(mzBytes);
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(intensity[..peaksCount]).CopyTo(intBytes);
        }
        else
        {
            // cpp parity: blobs are always little-endian. If we ever run on a big-endian
            // host, swap byte order explicitly. (Not expected for .NET hosts we ship to.)
            for (var i = 0; i < peaksCount; i++)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(
                    mzBytes.AsSpan(i * sizeof(double)), BitConverter.DoubleToInt64Bits(mz[i]));
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                    intBytes.AsSpan(i * sizeof(float)), BitConverter.SingleToInt32Bits(intensity[i]));
            }
        }

        var mzBlob = mzBytes;
        var intBlob = intBytes;

        if (levelCompress != 0)
        {
            // cpp parity: try zlib compression. If the compressed length is >= original,
            // store the raw blob instead. ZLibStream emits zlib-wrapped output (RFC 1950)
            // matching cpp's compress() default.
            mzBlob = TryCompress(mzBytes);
            intBlob = TryCompress(intBytes);
        }

        using var cmd = Db.CreateCommand();
        cmd.CommandText = "INSERT INTO RefSpectraPeaks VALUES(@id, @mz, @i)";
        cmd.Parameters.AddWithValue("@id", spectraId);
        cmd.Parameters.AddWithValue("@mz", mzBlob);
        cmd.Parameters.AddWithValue("@i", intBlob);
        if (cmd.ExecuteNonQuery() != 1)
            FailSql(0, cmd.CommandText, null, "Failed importing peaks.");
    }

    /// <summary>Zlib-compress <paramref name="bytes"/>; if the result is no smaller, return <paramref name="bytes"/> verbatim.</summary>
    /// <remarks>
    /// cpp parity: BlibMaker.cpp:1101-1130 uses <c>compress()</c> from zlib (RFC 1950 wrapper).
    /// .NET 8's <see cref="ZLibStream"/> emits the same zlib-wrapped output. Decompressors
    /// on the cpp side use <c>uncompress()</c> which accepts this format directly.
    /// </remarks>
    private static byte[] TryCompress(byte[] bytes)
    {
        if (bytes.Length == 0) return bytes;

        using var output = new MemoryStream(bytes.Length);
        using (var z = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(bytes, 0, bytes.Length);
        }

        if (output.Length >= bytes.Length)
            return bytes; // cpp parity: no shrink => store uncompressed.

        return output.ToArray();
    }

    // --- transfer helpers (cpp BlibMaker.cpp:716-1031) --------------------------------------

    /// <summary>
    /// cpp <c>createUpdatedRefSpectraView</c>. Creates a TEMP VIEW with up-to-date column
    /// names, substituting previous names or default values for columns absent from the
    /// attached source schema.
    /// </summary>
    public void CreateUpdatedRefSpectraView(string sourceDbName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDbName);
        var sb = new System.Text.StringBuilder();
        sb.Append("CREATE TEMP VIEW RefSpectraTransfer AS SELECT id, peptideSeq, precursorMZ, precursorCharge, peptideModSeq, " +
                  "prevAA, nextAA, copies, numPeaks, fileID, score, scoreType");

        // cpp parity: anonymous struct array at BlibMaker.cpp:614-639. Preserve order;
        // each entry is "current column name" + "list of legacy names to forward" +
        // "default value when neither exists".
        foreach (var info in _schemaColumnInfo)
        {
            if (TableColumnExists(sourceDbName, "RefSpectra", info.CurrentName))
            {
                sb.Append(", ").Append(info.CurrentName);
            }
            else
            {
                string? substitute = null;
                foreach (var prev in info.PreviousNames)
                {
                    if (TableColumnExists(sourceDbName, "RefSpectra", prev))
                    {
                        substitute = prev;
                        break;
                    }
                }
                substitute ??= info.DefaultValue;
                sb.Append(", ").Append(substitute).Append(" AS ").Append(info.CurrentName);
            }
        }

        sb.Append(" FROM ").Append(sourceDbName).Append(".RefSpectra");
        SqlStmt(sb.ToString());
    }

    private readonly record struct SchemaColumnInfo(string CurrentName, string[] PreviousNames, string DefaultValue);

    // cpp parity: BlibMaker.cpp:622-638. Ion mobility columns have legacy aliases preserved.
    private static readonly SchemaColumnInfo[] _schemaColumnInfo =
    {
        new("SpecIDinFile", Array.Empty<string>(), "NULL"),
        new("retentionTime", Array.Empty<string>(), "0"),
        new("ionMobility", new[] { "driftTimeMsec", "ionMobilityValue" }, "0"),
        new("ionMobilityType", Array.Empty<string>(), "1"), // drift time default
        new("collisionalCrossSectionSqA", Array.Empty<string>(), "0"),
        new("ionMobilityHighEnergyOffset", new[] { "driftTimeHighEnergyOffsetMsec", "ionMobilityHighEnergyDriftTimeOffsetMsec" }, "0"),
        new("startTime", Array.Empty<string>(), "NULL"),
        new("endTime", Array.Empty<string>(), "NULL"),
        new("totalIonCurrent", Array.Empty<string>(), "NULL"),
        new("moleculeName", Array.Empty<string>(), "NULL"),
        new("chemicalFormula", Array.Empty<string>(), "NULL"),
        new("inchiKey", Array.Empty<string>(), "NULL"),
        new("otherKeys", Array.Empty<string>(), "NULL"),
        new("precursorAdduct", Array.Empty<string>(), "NULL"),
    };

    /// <summary>
    /// cpp <c>transferSpectrumFiles</c>. Copies SpectrumSourceFiles rows from the attached
    /// source library, populating <see cref="_oldToNewFileId"/> for later spectrum inserts.
    /// </summary>
    public void TransferSpectrumFiles(string schemaTmp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaTmp);

        if (!TableExists(schemaTmp, "SpectrumSourceFiles"))
        {
            if (_unknownFileId == UnknownFileIdSentinel)
            {
                Verbosity.Warn("Orignal library does not contain filenames for the  library spectra"); // cpp typo preserved
                SqlStmt("INSERT INTO SpectrumSourceFiles (fileName, cutoffScore) VALUES ('UNKNOWN', -1)");
                _unknownFileId = (int)Db.LastInsertRowId;
            }
            return;
        }

        var cutoffSelect = TableColumnExists(schemaTmp, "SpectrumSourceFiles", "cutoffScore")
            ? "IFNULL(cutoffScore, -1)"
            : "-1";
        var idFileSelect = TableColumnExists(schemaTmp, "SpectrumSourceFiles", "idFileName")
            ? "IFNULL(idFileName, fileName)"
            : "fileName";
        var workflowSelect = TableColumnExists(schemaTmp, "SpectrumSourceFiles", "workflowType")
            ? "workflowType"
            : "0";

        // cpp parity (faithful, including the bug): BlibMaker.cpp:740 SELECTs columns in the
        // order `id, fileName, idFileName, cutoffScore, workflowType` — but cpp:749-750
        // reads them BACK as `workflowType = column 3, cutoffScore = column 4`, i.e. with the
        // last two SWAPPED. This means the output's cutoffScore column gets the
        // workflowType-select value and vice versa. The .check golden files encode that
        // behavior. To match cpp byte-for-byte we replicate the same column-read swap.
        var sql = $"SELECT id, fileName, {idFileSelect}, {cutoffSelect}, {workflowSelect} FROM {schemaTmp}.SpectrumSourceFiles";
        using var cmd = Db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var oldId = reader.GetInt32(0);
            var fileName = reader.GetString(1);
            var idFileName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            // cpp:749 — workflowType from column 3 (which the SQL fills with cutoffScore).
            // cpp uses sqlite3_column_int which does loose coercion (REAL→truncated int, TINYINT→signed).
            // .NET's GetInt32 throws on REAL; replicate cpp's behavior manually.
            var workflowType = (WorkflowType)SqliteLooseInt(reader, 3);
            // cpp:750 — cutoff from column 4 (which the SQL fills with workflowType).
            // sqlite3_column_double also coerces — TINYINT → double, etc.
            var cutoff = SqliteLooseDouble(reader, 4);

            var existingFileId = GetFileId(fileName, cutoff);
            if (existingFileId >= 0)
            {
                _oldToNewFileId[oldId] = existingFileId;
                continue;
            }

            var newFileId = AddFile(fileName, cutoff, idFileName, workflowType);
            _oldToNewFileId[oldId] = newFileId;
        }
    }

    /// <summary>
    /// cpp <c>transferProteins</c>. Copies the entire Proteins table from the attached source
    /// library, preserving ids.
    /// </summary>
    public void TransferProteins(string schemaTmp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaTmp);
        if (!TableExists(schemaTmp, "Proteins"))
            return;

        SqlStmt($"INSERT INTO main.Proteins (id, accession) SELECT id, accession FROM {schemaTmp}.Proteins");
    }

    /// <summary>
    /// cpp <c>getNewFileId</c>. Look up (or copy) the new SpectrumSourceFiles id for a
    /// spectrum being transferred from the source library.
    /// </summary>
    internal int GetNewFileId(string libName, int specId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libName);

        var sql = $"SELECT fileID FROM {libName}.RefSpectra WHERE id = {specId.ToString(CultureInfo.InvariantCulture)}";
        int oldFileId;
        try
        {
            using var cmd = Db.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return _unknownFileId;
            oldFileId = reader.IsDBNull(0) ? _unknownFileId : reader.GetInt32(0);
        }
        catch (SQLiteException)
        {
            return _unknownFileId;
        }

        if (_oldToNewFileId.TryGetValue(oldFileId, out var cached))
            return cached;

        var cutoffSelect = TableColumnExists(libName, "SpectrumSourceFiles", "cutoffScore") ? "cutoffScore" : "-1";
        var idFileSelect = TableColumnExists(libName, "SpectrumSourceFiles", "idFileName") ? "idFileName" : "fileName";
        var workflowSelect = TableColumnExists(libName, "SpectrumSourceFiles", "workflowType") ? "workflowType" : "0";

        var insertSql = $"INSERT INTO main.SpectrumSourceFiles(fileName, idFileName, cutoffScore, workflowType) " +
                        $"SELECT fileName, {idFileSelect}, {cutoffSelect}, {workflowSelect} FROM {libName}.SpectrumSourceFiles " +
                        $"WHERE {libName}.SpectrumSourceFiles.id = {oldFileId.ToString(CultureInfo.InvariantCulture)}";
        SqlStmt(insertSql);
        var newId = (int)Db.LastInsertRowId;
        _oldToNewFileId[oldFileId] = newId;
        return newId;
    }

    /// <summary>
    /// cpp <c>transferSpectrum</c>. Pulls a single spectrum (plus its peaks / mods / annotations /
    /// proteins) from the attached source schema into main.
    /// </summary>
    /// <returns>The new RefSpectra.id in main.</returns>
    public int TransferSpectrum(string schemaTmp, int spectraTmpId, int copies, int tableVersion = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaTmp);

        var newFileId = GetNewFileId(schemaTmp, spectraTmpId);

        var sql = string.Format(
            CultureInfo.InvariantCulture,
            "INSERT INTO RefSpectra(peptideSeq, precursorMZ, precursorCharge, peptideModSeq, prevAA, nextAA, copies, numPeaks, fileID, " +
            "ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, " +
            "moleculeName, chemicalFormula, inchiKey, otherKeys, precursorAdduct, " +
            "startTime, endTime, totalIonCurrent, retentionTime, specIDinFile, score, scoreType) " +

            "SELECT peptideSeq, precursorMZ, precursorCharge, peptideModSeq, prevAA, nextAA, {0}, numPeaks, {1}, " +
            "ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, " +
            "moleculeName, chemicalFormula, inchiKey, otherKeys, precursorAdduct, " +
            "startTime, endTime, totalIonCurrent, retentionTime, specIDinFile, score, scoreType " +
            "FROM RefSpectraTransfer WHERE id = {2}",
            copies, newFileId, spectraTmpId);
        SqlStmt(sql);

        var spectraId = (int)Db.LastInsertRowId;

        TransferPeaks(schemaTmp, spectraId, spectraTmpId);
        TransferModifications(schemaTmp, spectraId, spectraTmpId);
        if (tableVersion >= BlibSchema.MinVersionPeakAnnot)
            TransferPeakAnnotations(schemaTmp, spectraId, spectraTmpId);
        if (tableVersion >= BlibSchema.MinVersionProteins)
            TransferRefSpectraProteins(schemaTmp, spectraId, spectraTmpId);

        return spectraId;
    }

    /// <summary>
    /// cpp <c>transferSpectra</c>. Bulk-transfers a set of (id, copies) pairs from the attached
    /// source schema into main via a TEMP TABLE join.
    /// </summary>
    public int TransferSpectra(string schemaTmp, IReadOnlyList<(int Id, int Copies)> bestSpectraIdAndCount, int tableVersion = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaTmp);
        ArgumentNullException.ThrowIfNull(bestSpectraIdAndCount);

        SqlStmt("CREATE TEMP TABLE TempSpectrumCopies (Id INTEGER PRIMARY KEY, copies INT)");

        using (var insertCmd = Db.CreateCommand())
        {
            insertCmd.CommandText = "INSERT INTO TempSpectrumCopies VALUES (@id, @copies)";
            var idParam = insertCmd.CreateParameter();
            idParam.ParameterName = "@id";
            insertCmd.Parameters.Add(idParam);
            var copiesParam = insertCmd.CreateParameter();
            copiesParam.ParameterName = "@copies";
            insertCmd.Parameters.Add(copiesParam);

            foreach (var (id, copies) in bestSpectraIdAndCount)
            {
                idParam.Value = id;
                copiesParam.Value = copies;
                try
                {
                    if (insertCmd.ExecuteNonQuery() != 1)
                        throw new BlibException(false, "Error inserting row into TempSpectrumCopies");
                }
                catch (SQLiteException ex)
                {
                    throw new BlibException(false, "Error inserting row into TempSpectrumCopies: " + ex.Message);
                }
            }
        }

        SqlStmt(
            "INSERT INTO RefSpectra(id, peptideSeq, precursorMZ, precursorCharge, " +
            "peptideModSeq, prevAA, nextAA, copies, numPeaks, fileID, " +
            "ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, " +
            "moleculeName, chemicalFormula, inchiKey, otherKeys, precursorAdduct, " +
            "startTime, endTime, totalIonCurrent, retentionTime, specIDinFile, score, scoreType) " +

            "SELECT ref.id, peptideSeq, precursorMZ, precursorCharge, " +
            "peptideModSeq, prevAA, nextAA, tmp.copies, numPeaks, fileID, " +
            "ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, " +
            "moleculeName, chemicalFormula, inchiKey, otherKeys, precursorAdduct, " +
            "startTime, endTime, totalIonCurrent, retentionTime, specIDinFile, score, scoreType " +
            "FROM RefSpectraTransfer ref " +
            "JOIN TempSpectrumCopies tmp ON ref.id=tmp.id " +
            "GROUP BY ref.id");

        SqlStmt(
            $"INSERT INTO RefSpectraPeaks(RefSpectraID,peakMZ,peakIntensity) " +
            $"SELECT RefSpectraID, peakMZ, peakIntensity " +
            $"FROM {schemaTmp}.RefSpectraPeaks peaks " +
            $"WHERE peaks.RefSpectraID IN (SELECT id FROM RefSpectra)");

        SqlStmt(
            $"INSERT INTO Modifications (RefSpectraID, position, mass) " +
            $"SELECT RefSpectraID, position, mass " +
            $"FROM {schemaTmp}.Modifications mods " +
            $"WHERE mods.RefSpectraID IN (SELECT id FROM RefSpectra)");

        if (tableVersion >= BlibSchema.MinVersionPeakAnnot)
        {
            SqlStmt(
                $"INSERT INTO RefSpectraPeakAnnotations (RefSpectraID, peakIndex, name, formula, inchiKey, otherKeys, charge, adduct, comment, mzTheoretical, mzObserved) " +
                $"SELECT RefSpectraID, peakIndex, name, formula, ann.inchiKey, ann.otherKeys, charge, adduct, comment, mzTheoretical, mzObserved " +
                $"FROM {schemaTmp}.RefSpectraPeakAnnotations ann " +
                $"WHERE ann.RefSpectraID IN (SELECT id FROM RefSpectra)");
        }

        if (tableVersion >= BlibSchema.MinVersionProteins)
        {
            SqlStmt(
                $"INSERT INTO RefSpectraProteins (RefSpectraId, ProteinId) " +
                $"SELECT RefSpectraID, ProteinId " +
                $"FROM {schemaTmp}.RefSpectraProteins pro " +
                $"WHERE pro.RefSpectraID IN (SELECT id FROM RefSpectra)");
        }

        return 0;
    }

    /// <summary>
    /// cpp <c>transferModifications</c>. Reads modifications from the attached source schema
    /// and inserts them under the new spectraID in main.
    /// </summary>
    public void TransferModifications(string schemaTmp, int spectraId, int spectraTmpId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaTmp);

        var sql = $"SELECT RefSpectraID, position, mass FROM {schemaTmp}.Modifications WHERE RefSpectraID={spectraTmpId.ToString(CultureInfo.InvariantCulture)} ORDER BY id";
        using var cmd = Db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var position = reader.GetInt32(1);
            var mass = reader.GetDouble(2);
            SqlStmt(string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO Modifications(RefSpectraID, position,mass) VALUES({0}, {1}, {2:F6})",
                spectraId, position, mass));
        }
    }

    /// <summary>
    /// cpp <c>transferPeaks</c>. Copies the peaks blob row(s) verbatim from the attached source.
    /// </summary>
    public void TransferPeaks(string schemaTmp, int spectraId, int spectraTmpId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaTmp);
        SqlStmt(string.Format(
            CultureInfo.InvariantCulture,
            "INSERT INTO RefSpectraPeaks(RefSpectraID,peakMZ,peakIntensity) " +
            "SELECT {0}, peakMZ, peakIntensity FROM {1}.RefSpectraPeaks WHERE RefSpectraID={2}",
            spectraId, schemaTmp, spectraTmpId));
    }

    /// <summary>
    /// cpp <c>transferPeakAnnotations</c>. Copies peak annotations from the attached source.
    /// </summary>
    public void TransferPeakAnnotations(string schemaTmp, int spectraId, int spectraTmpId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaTmp);

        const string cols = "RefSpectraID, peakIndex, name, formula, inchiKey, otherKeys, charge, adduct, comment, mzTheoretical, mzObserved";
        var sql = $"SELECT {cols} FROM {schemaTmp}.RefSpectraPeakAnnotations WHERE RefSpectraID={spectraTmpId.ToString(CultureInfo.InvariantCulture)} ORDER BY id";

        using var cmd = Db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            // cpp parity: BlibMaker.cpp:1005-1020 interpolates strings directly via %s. Preserve
            // shape; values that may contain apostrophes are escaped here even though cpp didn't —
            // cpp relies on the source library being well-formed, but escaping is strictly safer.
            var peakIndex = reader.GetInt32(1);
            var name = ReadText(reader, 2);
            var formula = ReadText(reader, 3);
            var inchi = ReadText(reader, 4);
            var otherKeys = ReadText(reader, 5);
            var charge = reader.GetInt32(6);
            var adduct = ReadText(reader, 7);
            var comment = ReadText(reader, 8);
            var mzTheor = reader.GetDouble(9);
            var mzObs = reader.GetDouble(10);

            var insertSql = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO RefSpectraPeakAnnotations({0}) VALUES({1}, {2},  '{3}',  '{4}', '{5}', '{6}', {7}, '{8}',  '{9}',  {10},  {11})",
                cols,
                spectraId,
                peakIndex,
                SqliteRoutine.EscapeApostrophes(name),
                SqliteRoutine.EscapeApostrophes(formula),
                SqliteRoutine.EscapeApostrophes(inchi),
                SqliteRoutine.EscapeApostrophes(otherKeys),
                charge,
                SqliteRoutine.EscapeApostrophes(adduct),
                SqliteRoutine.EscapeApostrophes(comment),
                mzTheor,
                mzObs);
            SqlStmt(insertSql);
        }

        static string ReadText(SQLiteDataReader r, int ordinal) =>
            r.IsDBNull(ordinal) ? string.Empty : r.GetString(ordinal);
    }

    /// <summary>
    /// cpp <c>transferRefSpectraProteins</c>. Copies the (refSpectraId, proteinId) mappings.
    /// </summary>
    public void TransferRefSpectraProteins(string schemaTmp, int spectraId, int spectraTmpId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaTmp);
        SqlStmt(string.Format(
            CultureInfo.InvariantCulture,
            "INSERT INTO RefSpectraProteins (RefSpectraId, ProteinId) " +
            "SELECT {0}, ProteinId FROM {1}.RefSpectraProteins WHERE RefSpectraId = {2}",
            spectraId, schemaTmp, spectraTmpId));
    }

    /// <summary>
    /// Generic <c>INSERT INTO main.&lt;table&gt; SELECT * FROM &lt;schemaTmp&gt;.&lt;table&gt;</c>.
    /// Used in cpp to bulk-copy non-spectrum tables.
    /// </summary>
    public void TransferTable(string schemaTmp, string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaTmp);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        SqlStmt($"INSERT INTO main.{tableName} SELECT * FROM {schemaTmp}.{tableName}");
    }

    // --- SQL execution / diagnostics --------------------------------------------------------

    /// <summary>
    /// cpp <c>sql_stmt</c>. Executes a non-query statement. On failure (unless
    /// <paramref name="ignoreFailure"/> is true) throws <see cref="BlibException"/> via
    /// <see cref="FailSql"/>.
    /// </summary>
    public void SqlStmt(string stmt, bool ignoreFailure = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stmt);
        try
        {
            using var cmd = Db.CreateCommand();
            cmd.CommandText = stmt;
            cmd.ExecuteNonQuery();
        }
        catch (SQLiteException ex) when (ignoreFailure)
        {
            // cpp parity: drop indexes / rollback when no transaction etc. should be silent.
            _ = ex;
        }
        catch (SQLiteException ex)
        {
            FailSql(ex.ErrorCode, stmt, ex.Message);
        }
    }

    /// <summary>
    /// cpp <c>check_rc</c>. Returns false on failure, throws when <paramref name="dieOnFailure"/>
    /// is true.
    /// </summary>
    public static bool CheckRc(int rc, string stmt, string? msg = null, bool dieOnFailure = true)
    {
        if (rc == 0 /* SQLITE_OK */)
            return true;
        if (dieOnFailure)
            FailSql(rc, stmt, null, msg);
        return false;
    }

    /// <summary>
    /// cpp <c>check_step</c>. Executes a SELECT-style statement and asserts that a row is
    /// returned. Throws when no row is available.
    /// </summary>
    public static void CheckStep(SQLiteDataReader reader, string stmt, string? msg = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (!reader.Read())
            FailSql(0, stmt, null, msg);
    }

    /// <summary>
    /// cpp <c>just_check_step</c>. Returns whether <see cref="SQLiteDataReader.Read"/> succeeded
    /// without throwing.
    /// </summary>
    public static bool JustCheckStep(SQLiteDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.Read();
    }

    /// <summary>
    /// cpp <c>fail_sql</c>. Throws via <see cref="Verbosity.Error"/> with diagnostic text
    /// including the SQL, the SQLite error, and the additional message.
    /// </summary>
    public static void FailSql(int rc, string stmt, string? err, string? msg = null)
    {
        var firstMsg = msg ?? "SQL failure. ";
        var sqlMsg = err ?? " ";
        Verbosity.Error($"{firstMsg} {sqlMsg} [SQL statement '{stmt}', return code {rc}]");
    }

    /// <summary>cpp <c>libIdFromName</c>: basename after the last <c>/</c> or <c>\</c>.</summary>
    private static string LibIdFromName(string name)
    {
        var lastSlash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        return lastSlash < 0 ? name : name.Substring(lastSlash + 1);
    }

    // --- CLI parsing ------------------------------------------------------------------------
    //
    // cpp parity: BlibMaker.cpp:68 parseCommandArgs / BlibMaker.cpp:93 parseNextSwitch /
    // BlibMaker.cpp:40 usage. The cpp argv layout is `argv[0]=programName; argv[1..]=actualArgs`,
    // and the cpp loop starts at i=1. In C#, Main(string[] args) gives us the actual args with
    // NO program name, so the loop starts at i=0.

    /// <summary>
    /// cpp <c>BlibMaker::usage</c> at BlibMaker.cpp:40 — empty body. Subclasses
    /// (BlibBuilder etc.) override this to print their help text and exit.
    /// </summary>
    public virtual void Usage()
    {
        // cpp parity: BlibMaker.cpp:40 — `void BlibMaker::usage() {}`.
    }

    /// <summary>
    /// cpp <c>BlibMaker::parseCommandArgs</c> at BlibMaker.cpp:68. Loop over argv consuming
    /// option switches (any 2-char arg starting with <c>-</c>), then if not in score-lookup
    /// mode pop the last arg as the output library name.
    /// </summary>
    /// <param name="argv">Argv from Main, NOT including any program name.</param>
    /// <returns>The next argv index after all consumed switches (i.e. the index of the
    /// first positional argument).</returns>
    public virtual int ParseCommandArgs(string[] argv)
    {
        ArgumentNullException.ThrowIfNull(argv);

        // cpp parity: BlibMaker.cpp:71 starts at i=1 (skipping argv[0]=program name). In
        // C# Main(args) we already get a name-less argv, so start at i=0.
        var i = 0;
        while (i < argv.Length)
        {
            var arg = argv[i];
            // cpp parity: BlibMaker.cpp:74 — break out as soon as we see anything that's not
            // a single short option ("-" + 1 char). Multi-char switches don't exist in cpp; a
            // bare "-" or something longer than 2 chars terminates the option loop.
            if (arg.Length != 2 || arg[0] != '-')
                break;
            i = ParseNextSwitch(i, argv);
        }

        if (!IsScoreLookupMode)
        {
            // cpp parity: BlibMaker.cpp:82 — at least one arg (the library name) must remain.
            if (argv.Length - i < 1)
            {
                Usage();
                // cpp Usage typically calls exit(1); subclasses may not, so guard here.
                throw new BlibException(false, "Missing output library name.");
            }

            // cpp parity: BlibMaker.cpp:85 — last arg is the library name.
            var libNameArg = argv[argv.Length - 1];

            // cpp parity: BlibMaker.cpp:85 sets lib_name = argv[argc-1]; we additionally
            // validate the extension here. cpp doesn't check the extension at this point —
            // the docs say `.blib` or no extension. We mirror by allowing both.
            ValidateLibraryName(libNameArg);

            // SetLibName derives lib_id from the basename like cpp BlibMaker.cpp:1267.
            SetLibName(libNameArg);
        }

        return i;
    }

    /// <summary>
    /// cpp <c>BlibMaker::parseNextSwitch</c> at BlibMaker.cpp:93. Handles the short flags
    /// <c>-v</c>, <c>-m</c>, <c>-a</c>, <c>-i</c>, <c>-d</c>, <c>-t</c>, <c>-z</c>.
    /// Subclasses override to add their own switches and fall back to this base for the
    /// shared ones. Returns the next argv index to consume.
    /// </summary>
    protected virtual int ParseNextSwitch(int i, string[] argv)
    {
        ArgumentNullException.ThrowIfNull(argv);
        if (i < 0 || i >= argv.Length) throw new ArgumentOutOfRangeException(nameof(i));

        var arg = argv[i];
        // Caller already verified arg.Length == 2 and arg[0] == '-'.
        var switchName = arg[1];

        switch (switchName)
        {
            case 'v':
                // cpp parity: BlibMaker.cpp:98 sets verbose=true; BlibBuilder overrides to
                // consume a level argument. The base maker just flips the bool.
                Verbose = true;
                break;

            case 'm':
                // cpp parity: BlibMaker.cpp:100 — `-m <megabytes>` sets PRAGMA cache_size.
                if (++i < argv.Length)
                {
                    if (!int.TryParse(argv[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mb) || mb <= 0)
                        Verbosity.Error("Invalid cache size specified.");
                    CacheSizeMb = mb;
                }
                break;

            case 'a':
                // cpp parity: BlibMaker.cpp:105 — `-a <authority>` overrides LSID authority.
                if (++i < argv.Length)
                    Authority = argv[i];
                break;

            case 'i':
                // cpp parity: BlibMaker.cpp:107 — `-i <lib_id>` overrides LSID library ID.
                if (++i < argv.Length)
                    LibId = argv[i];
                break;

            case 'd':
            {
                // cpp parity: BlibMaker.cpp:109 — `-d [filename]` dumps CREATE TABLE script
                // to <filename> (or stdout if absent) then exit(0). We mirror with
                // Environment.Exit(0) so behaviour matches cpp exactly.
                var commands = new List<string>();
                LibId = "example"; // cpp parity: BlibMaker.cpp:112.
                CreateTables(commands, execute: false);

                string? fname = (++i < argv.Length) ? argv[i] : null;

                // cpp prints to file when given, else stdout (NOT stderr).
                TextWriter writer;
                if (fname != null)
                    writer = new StreamWriter(fname);
                else
                    writer = Console.Out;

                try
                {
                    writer.WriteLine("-- BiblioSpec format documentation");
                    writer.WriteLine("-- These commands will create an empty BiblioSpec library when used with SQLite3. You can use them as a starting point for your own files.");
                    writer.WriteLine("-- This file was generated by the \"blibbuild -d\" command during the ProteoWizard build process. Do not edit, it may be overwritten and your changes will be lost.");
                    writer.WriteLine();
                    foreach (var stmt in commands)
                    {
                        writer.WriteLine();
                        writer.WriteLine(stmt);
                    }
                }
                finally
                {
                    if (fname != null)
                        writer.Dispose();
                }

                Environment.Exit(0);
                break; // unreachable
            }

            case 't':
                // cpp parity: BlibMaker.cpp:131 — `-t` enables score-lookup-only mode.
                IsScoreLookupMode = true;
                break;

            case 'z':
                // cpp parity: BlibMaker.cpp:134 — `-z <charges>` is a comma-separated list of
                // precursor charges to keep. cpp uses boost::split + boost::lexical_cast; we
                // use string.Split + int.TryParse.
                if (++i < argv.Length)
                {
                    var charges = argv[i];
                    var parts = charges.Split(',');
                    foreach (var part in parts)
                    {
                        if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
                        {
                            throw new BlibException(true,
                                $"the -z argument '{charges}' was not understood, expected a list of charges like \"2,3\" or \"1,2,4\" etc");
                        }
                        PrecursorCharges.Add(z);
                    }
                }
                break;

            default:
                // cpp parity: BlibMaker.cpp:149 — unknown switch -> usage() (which exits).
                Usage();
                throw new BlibException(false, $"Unknown switch '{arg}'.");
        }

        // cpp parity: BlibMaker.cpp:152 returns min(argc, i+1). In C# we just clamp.
        return Math.Min(argv.Length, i + 1);
    }

    /// <summary>
    /// Validate that the library file name ends in <c>.blib</c> or has no extension.
    /// cpp parity: the BlibBuild docs say the library file name must end in <c>.blib</c>
    /// (or have no extension), even though the cpp parseCommandArgs doesn't enforce this.
    /// We add the check defensively so common typos surface as a friendly error.
    /// </summary>
    /// <summary>cpp parity for <c>sqlite3_column_int</c>: loose coercion across REAL / signed-int /
    /// unsigned-byte (TINYINT). .NET's <c>GetInt32</c> throws on REAL — sqlite3 truncates.</summary>
    private static int SqliteLooseInt(SQLiteDataReader reader, int idx)
    {
        if (reader.IsDBNull(idx)) return 0;
        var raw = reader.GetValue(idx);
        return raw switch
        {
            double d => (int)d,
            float f => (int)f,
            long l => (int)l,
            int i => i,
            short s => s,
            byte b => (sbyte)b, // System.Data.SQLite maps TINYINT to unsigned byte; sign-recover
            sbyte sb => sb,
            // cpp's sqlite3_column_int returns 0 silently on non-numeric text — must use
            // TryParse with 0 fallback (not int.Parse which throws on 'N/A' etc.).
            string str => int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0,
            _ => Convert.ToInt32(raw, CultureInfo.InvariantCulture),
        };
    }

    /// <summary>cpp parity for <c>sqlite3_column_double</c>: coerces TINYINT → double, etc.</summary>
    private static double SqliteLooseDouble(SQLiteDataReader reader, int idx)
    {
        if (reader.IsDBNull(idx)) return 0.0;
        var raw = reader.GetValue(idx);
        return raw switch
        {
            double d => d,
            float f => f,
            long l => l,
            int i => i,
            short s => s,
            byte b => (sbyte)b,
            sbyte sb => sb,
            // cpp's sqlite3_column_double returns 0.0 silently on non-numeric text — same TryParse pattern as the int variant.
            string str => double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0.0,
            _ => Convert.ToDouble(raw, CultureInfo.InvariantCulture),
        };
    }

    private static void ValidateLibraryName(string libName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libName);
        var ext = Path.GetExtension(libName);
        if (!string.IsNullOrEmpty(ext) &&
            !string.Equals(ext, ".blib", StringComparison.OrdinalIgnoreCase))
        {
            throw new BlibException(true,
                $"Output library name '{libName}' must end in .blib (or have no extension).");
        }
    }
}
