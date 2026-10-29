// Port of pwiz_tools/BiblioSpec/src/BlibMaker.{h,cpp}
//
// Faithful C# port of BiblioSpec::BlibMaker, with the library itself written through
// pwiz.BiblioSpec.IO.BlibWriter, the one .blib writer Osprey and CarafeSharp share. The writer
// owns the schema, the per-row inserts (spectra with their peaks, modifications and proteins,
// source files, retention times, metadata), the indexes and the temp-then-move of the finished
// file. This class keeps what the cpp does around that: overwrite versus append, bringing an
// older library up to the current schema, the LSID and revision, the CLI switches the tools
// share, and the SQL transfers between an attached input library and the one being written.
// CLI plumbing beyond the shared switches lives in the BlibBuild and BlibFilter executables.

using System.Data.SQLite;
using System.Globalization;
using pwiz.BiblioSpec.IO;
using pwiz.Common.CommandLine;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Builds (or appends to) a BiblioSpec <c>.blib</c> SQLite library. Port of
/// <c>BiblioSpec::BlibMaker</c> at <c>pwiz_tools/BiblioSpec/src/BlibMaker.{h,cpp}</c>, writing
/// through <see cref="BlibWriter"/>.
/// </summary>
/// <remarks>
/// <para>
/// BlibBuilder and BlibFilter (cpp) inherit from BlibMaker; the corresponding C# subclasses
/// override <see cref="OpenSources"/>, <see cref="CloseSources"/> and <see cref="GetLsid"/> the same way.
/// </para>
/// <para>
/// A new library is written at a temp beside its final name and moved over it by
/// <see cref="Commit"/> (the writer's doing), so until then the file at <see cref="LibName"/>
/// is whatever it was, overwrite or not. Appending writes the existing library in place, as cpp
/// did.
/// </para>
/// </remarks>
public class BlibMaker : IDisposable
{
    // cpp: static const char ERROR_GENERIC[] = "Unexpected failure.";
    private const string ErrorGeneric = "Unexpected failure.";

    // cpp BlibMaker.cpp:39 sized the page cache at 250 MB for a bulk write.
    private const int DefaultCacheSizeMb = 250;

    private BlibWriter? _writer;
    private string? _libName;
    private string? _libId;
    private string _authority = "proteome.gs.washington.edu";
    private int _cacheSizeMb = DefaultCacheSizeMb;
    private bool _redundant = true;
    private bool _overwrite;
    private string _message = ErrorGeneric;

    // cpp parity: file -> (id, cutoffScore). Preserved as a managed map; the cpp uses
    // std::map<string, pair<int, double>> - semantics: re-insert on cutoff mismatch
    // sets the stored cutoff to -1.
    private readonly Dictionary<string, (int Id, double Cutoff)> _fileIdCache = new(StringComparer.Ordinal);

    /// <summary>Constructs a BlibMaker. Call <see cref="SetLibName"/> + <see cref="Init"/> to begin.</summary>
    public BlibMaker()
    {
    }

    /// <summary>Disposes the writer, deleting an uncommitted library's temp file.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes the writer, deleting an uncommitted library's temp file.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && _writer != null)
        {
            _writer.Dispose();
            _writer = null;
        }
    }

    /// <summary>
    /// The writer the library is written through. Every row the tools write, and every
    /// question they ask of the library being written, goes through it; the tools issue no SQL
    /// of their own against a <c>.blib</c>.
    /// </summary>
    protected internal BlibWriter Writer => _writer ?? throw new InvalidOperationException("Db not open. Call Init first.");

    /// <summary>True while the library is open for writing: after <see cref="Init"/>, until <see cref="Commit"/> closes it.</summary>
    /// <remarks>cpp parity: cpp's <c>sqlite3_prepare</c> on a NULL db returns an error code
    /// and silently no-ops; the cpp BuildParser ctor relies on that to construct a temporary
    /// builder (e.g. for SQT-mod-table parsing in PercolatorXmlReader). The C# port throws on
    /// access, so subclasses use this property to short-circuit before touching the DB.</remarks>
    public bool IsDbOpen => _writer?.Connection != null;

    /// <summary>The library file name / path. cpp <c>getLibName</c>.</summary>
    public string? LibName => _libName;

    /// <summary>True when running in score-lookup mode (cpp <c>-t</c> switch sets this).</summary>
    public bool IsScoreLookupMode { get; set; }

    /// <summary>cpp <c>setMessage</c> / <c>message</c> - appended into SQL failure diagnostics.</summary>
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

    /// <summary>cpp <c>preferEmbeddedSpectra_</c> - <c>boost::optional&lt;bool&gt;</c>, a C# nullable bool.</summary>
    public bool? PreferEmbeddedSpectra { get; set; }

    /// <summary>cpp <c>verbose</c>.</summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// cpp <c>precursorCharges_</c> - when non-empty, only emit entries whose precursor
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

    /// <summary>SQLite page cache size in megabytes (cpp <c>-m</c> switch).</summary>
    public int CacheSizeMb
    {
        get => _cacheSizeMb;
        set
        {
            if (value <= 0) throw new BlibException(false, "Invalid cache size specified.");
            _cacheSizeMb = value;
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
    /// cpp <c>init</c>. Decides overwrite vs append, opens the writer, sizes its cache, then calls
    /// <see cref="OpenSources"/> followed by either the writer's schema creation or its upgrade
    /// of the appended library (<see cref="BlibWriter.UpgradeSchema"/>).
    /// </summary>
    public virtual void Init()
    {
        if (IsScoreLookupMode)
            return;

        if (_libName is null)
            throw new BlibException(false, "Library name has not been set; call SetLibName first.");

        // cpp parity: empty / non-existent file => overwrite. Existing non-empty file =>
        // overwrite if requested, else append. cpp removed the file up front when overwriting;
        // the writer builds the new library beside it and replaces it at Commit, so a build that
        // stops short leaves the old library where it was.
        if (!File.Exists(_libName) || new FileInfo(_libName).Length == 0)
            _overwrite = true;

        OpenWriter(_libName);

        _message = "Failed to initialize " + _libName;

        Writer.SetCacheSize(_cacheSizeMb);

        OpenSources();

        if (_overwrite)
        {
            Writer.CreateSchema(GetLsid());
        }
        else
        {
            // cpp parity: drop indexes for bulk inserts; Commit creates them again. An older
            // library is brought up to the current schema before anything is added to it.
            Writer.DropIndexes();
            Writer.UpgradeSchema();
        }

        _message = ErrorGeneric;
    }

    /// <summary>
    /// Opens the writer on <paramref name="file"/>: an empty library when overwriting, else a
    /// copy of the existing one to append to. Throws via <see cref="Verbosity.Error"/> on failure.
    /// </summary>
    private void OpenWriter(string file)
    {
        try
        {
            // Callers hand BlibBuild paths already run through Skyline's PathEx.ToLongPath; the
            // writer's temp beside such a path would carry the prefix into SQLite, which Wine
            // cannot open. The prefix only buys anything past MAX_PATH.
            _writer = BlibWriter.Open(SqliteRoutine.WithoutRedundantExtendedPrefix(file), append: !_overwrite);
        }
        catch (Exception ex) when (ex is SQLiteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Verbosity.Error($"Failed to create '{file}'. Make sure the directory exists with write permissions. {ex.Message}");
        }
    }

    /// <summary>
    /// cpp <c>is_empty</c>. True when the library has no RefSpectra rows.
    /// </summary>
    public virtual bool IsEmpty() => SpectrumCount == 0;

    /// <summary>cpp <c>countSpectra</c>. The RefSpectra rows of the library being written, an appended library's included.</summary>
    public int SpectrumCount => Writer.SpectrumCount;

    /// <summary>
    /// cpp <c>abort_current_library</c>. Closes the writer, which deletes the library in
    /// progress, and deletes the file at <see cref="LibName"/>.
    /// </summary>
    public virtual void AbortCurrentLibrary()
    {
        Verbosity.Debug("Deleting current library.");

        // Disposing the writer without Complete deletes its temp - the library being written -
        // and releases the file before the delete below, which matters for an empty-result
        // build (e.g. when DIA-NN produced 0 precursors and the redundant library is aborted).
        if (_writer != null)
        {
            _writer.Dispose();
            _writer = null;
        }

        // cpp parity: cpp wrote in place and removed the file it was writing - when appending,
        // the library it was adding to.
        if (!string.IsNullOrEmpty(_libName) && File.Exists(_libName))
        {
            // Warn rather than throw out of abort if the file can't be removed (parity with
            // cpp's error_code bfs::remove).
            try
            {
                File.Delete(_libName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Verbosity.Warn($"Could not remove '{_libName}' during abort: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// cpp <c>beginTransaction</c>. Commits any open transaction, then begins one.
    /// </summary>
    public void BeginTransaction()
    {
        Writer.CommitBatch();
        Writer.BeginBatch();
    }

    /// <summary>
    /// cpp <c>endTransaction</c>. Commits the open transaction; without one, does nothing.
    /// </summary>
    public void EndTransaction()
    {
        Writer.CommitBatch();
    }

    /// <summary>
    /// cpp <c>undoActiveTransaction</c>. Rolls back the open transaction; without one, does nothing.
    /// </summary>
    public void UndoActiveTransaction()
    {
        Writer.RollbackBatch();
    }

    /// <summary>
    /// cpp <c>commit</c>. Updates LibInfo, closes the input libraries, then has the writer
    /// create the indexes, count the spectra and move the library over its final name.
    /// </summary>
    public virtual void Commit()
    {
        Writer.CommitBatch();
        UpdateLibInfo();
        CloseSources();
        Writer.Complete();
    }

    /// <summary>
    /// Records the command line a tool was run with in the library's <c>LibMetadata</c>, under
    /// <paramref name="key"/> (<c>bibliospec.blibbuild.command</c>), as every tool of ours that
    /// makes or transforms a library does. Nothing in score-lookup mode, which writes no library.
    /// </summary>
    public void RecordCommandLine(string tool, string key, IEnumerable<string> args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(args);
        if (!IsDbOpen)
            return;
        Writer.AddMetadata(key, CommandLineText.Format(tool, args));
    }

    /// <summary>cpp <c>getLSID</c>. <c>urn:lsid:&lt;authority&gt;:spectral_library:bibliospec:...</c>.</summary>
    protected virtual string GetLsid()
    {
        var libType = _redundant ? "redundant" : "nr";
        return $"urn:lsid:{_authority}:spectral_library:bibliospec:{libType}:{_libId ?? string.Empty}";
    }

    /// <summary>
    /// Virtual no-op hook called from <see cref="Init"/>, before the new library's schema is
    /// created or the appended library's brought current. BlibFilter opens the redundant
    /// library it reads here (cpp attached it to the connection).
    /// </summary>
    protected virtual void OpenSources()
    {
    }

    /// <summary>
    /// Virtual no-op hook called from <see cref="Commit"/> before the writer completes the
    /// library. BlibFilter closes the redundant library here.
    /// </summary>
    protected virtual void CloseSources()
    {
    }
    /// <summary>
    /// cpp <c>createTables</c>. The statements that create and seed an empty library, for the
    /// <c>-d</c> switch's documentation dump: <see cref="BlibSchema.CreateScript"/>, the tables
    /// and seeds <see cref="BlibWriter.CreateSchema"/> gives the library itself.
    /// </summary>
    public virtual void CreateTables(List<string> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        // cpp parity: BlibMaker.cpp:353 - the LSID derives from the (user-supplied) library file
        // basename; the script escapes it.
        commands.AddRange(BlibSchema.CreateScript(GetLsid(), BlibSchema.FormatCreateTime(DateTime.Now)));
    }

    /// <summary>
    /// cpp <c>updateLibInfo</c>. Bumps majorVersion; the writer counts the spectra into
    /// numSpecs when it completes the library.
    /// </summary>
    protected virtual void UpdateLibInfo()
    {
        GetNextRevision(out var dataRev);
        Writer.SetRevision(dataRev);
    }

    /// <summary>cpp <c>getNextRevision</c>. The appended library's <c>majorVersion</c> plus one; 1 for a new library.</summary>
    protected virtual void GetNextRevision(out int dataRev)
    {
        dataRev = (Writer.ExistingRevision ?? 0) + 1;
    }

    /// <summary>Default cutoff score: -1. Override in subclasses to provide a real filter.</summary>
    /// <remarks>cpp parity: protected in BlibMaker but called from BuildParser (a sibling
    /// type, not a subclass) via the BlibBuilder reference. We mark this <c>protected
    /// internal</c> so BuildParser (same assembly) can read it while still letting
    /// external subclasses override.</remarks>
    protected internal virtual double GetCutoffScore() => -1;

    /// <summary>
    /// Adds a <see cref="BlibOptionalColumn"/> to the library being built, if it is not there
    /// already, through the writer (<see cref="BlibWriter.AddOptionalColumn"/>), which then
    /// writes the value each spectrum and protein record holds for it.
    /// </summary>
    public void AddOptionalColumn(BlibOptionalColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        Writer.AddOptionalColumn(column);
    }

    /// <summary>
    /// Drops every <see cref="BlibOptionalColumn"/> the library being built has
    /// (<see cref="BlibWriter.RemoveOptionalColumns"/>).
    /// </summary>
    public void RemoveOptionalColumns()
    {
        Writer.RemoveOptionalColumns();
    }

    /// <summary>
    /// Writes a protein, or a protein group under its joined accessions, with its experiment-wide
    /// q-value and PEP, for the optional <c>Proteins</c> columns the library has; the spectra
    /// mapping to it name it by accession (<see cref="BlibWriter.AddProtein"/>).
    /// </summary>
    public long AddProtein(string accession, double? qValue, double? pep)
    {
        return Writer.AddProtein(accession, qValue, pep);
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
            if (cutoffScore != cached.Cutoff)
                Writer.SetCutoffScore(cached.Id, -1);
            return cached.Id;
        }

        if (Writer.TryFindSourceFile(file, out var fileId, out var cutoff))
        {
            _fileIdCache[file] = ((int)fileId, cutoff ?? -1.0);
            return (int)fileId;
        }
        return -1;
    }

    /// <summary>
    /// cpp <c>addFile</c>. Inserts a SpectrumSourceFiles row through the writer and caches
    /// its new id. cpp BlibMaker.cpp:1066-1082 adapted its INSERT to the columns an older
    /// library had; <see cref="BlibWriter.UpgradeSchema"/> adds them all first here.
    /// </summary>
    public int AddFile(string specFile, double cutoffScore, string idFile, WorkflowType workflowType)
    {
        ArgumentNullException.ThrowIfNull(specFile);
        ArgumentNullException.ThrowIfNull(idFile);

        int newFileId;
        try
        {
            newFileId = (int)Writer.AddSourceFile(specFile, idFile, cutoffScore, (int)workflowType);
        }
        catch (SQLiteException ex)
        {
            // cpp parity: a failed statement is a BlibException with the SQL and the message.
            FailSql(ex.ErrorCode, "INSERT INTO SpectrumSourceFiles", ex.Message, _message);
            throw; // unreachable; FailSql throws
        }
        _fileIdCache[specFile] = (newFileId, cutoffScore);
        return newFileId;
    }

    /// <summary>
    /// Writes a spectrum - its RefSpectra row, peaks, modifications and proteins - through
    /// the writer and returns its RefSpectra id. cpp's BuildParser did this with its own
    /// prepared INSERT, then <c>insertPeaks</c> and one statement per modification and protein.
    /// </summary>
    public int InsertSpectrum(BlibSpectrumRecord spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        try
        {
            return (int)Writer.AddSpectrum(spectrum);
        }
        catch (SQLiteException ex)
        {
            // cpp parity: BuildParser.cpp:638 - a failed insert is a BlibException via
            // Verbosity::error, with the SQL and the message.
            FailSql(ex.ErrorCode, "INSERT INTO RefSpectra", ex.Message, "Error inserting spectrum row.");
            throw; // unreachable; FailSql throws
        }
    }

    /// <summary>
    /// cpp <c>fail_sql</c>. Throws via <see cref="Verbosity.Error"/> with diagnostic text
    /// including the SQL, the SQLite error, and the additional message.
    /// </summary>
    private static void FailSql(int rc, string stmt, string? err, string? msg = null)
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
    /// cpp <c>BlibMaker::usage</c> at BlibMaker.cpp:40 - empty body. Subclasses
    /// (BlibBuilder etc.) override this to print their help text and exit.
    /// </summary>
    public virtual void Usage()
    {
        // cpp parity: BlibMaker.cpp:40 - `void BlibMaker::usage() {}`.
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
            // cpp parity: BlibMaker.cpp:74 - break out as soon as we see anything that's not
            // a single short option ("-" + 1 char). Multi-char switches don't exist in cpp; a
            // bare "-" or something longer than 2 chars terminates the option loop.
            if (arg.Length != 2 || arg[0] != '-')
                break;
            i = ParseNextSwitch(i, argv);
        }

        if (!IsScoreLookupMode)
        {
            // cpp parity: BlibMaker.cpp:82 - at least one arg (the library name) must remain.
            if (argv.Length - i < 1)
            {
                Usage();
                // cpp Usage typically calls exit(1); subclasses may not, so guard here.
                throw new BlibException(false, "Missing output library name.");
            }

            // cpp parity: BlibMaker.cpp:85 - last arg is the library name.
            var libNameArg = argv[argv.Length - 1];

            // cpp parity: BlibMaker.cpp:85 sets lib_name = argv[argc-1]; we additionally
            // validate the extension here. cpp doesn't check the extension at this point -
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
                // cpp parity: BlibMaker.cpp:100 - `-m <megabytes>` sets PRAGMA cache_size.
                if (++i < argv.Length)
                {
                    if (!int.TryParse(argv[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mb) || mb <= 0)
                        Verbosity.Error("Invalid cache size specified.");
                    CacheSizeMb = mb;
                }
                break;

            case 'a':
                // cpp parity: BlibMaker.cpp:105 - `-a <authority>` overrides LSID authority.
                if (++i < argv.Length)
                    Authority = argv[i];
                break;

            case 'i':
                // cpp parity: BlibMaker.cpp:107 - `-i <lib_id>` overrides LSID library ID.
                if (++i < argv.Length)
                    LibId = argv[i];
                break;

            case 'd':
            {
                // cpp parity: BlibMaker.cpp:109 - `-d [filename]` dumps CREATE TABLE script
                // to <filename> (or stdout if absent) then exit(0). We mirror with
                // Environment.Exit(0) so behaviour matches cpp exactly.
                var commands = new List<string>();
                LibId = "example"; // cpp parity: BlibMaker.cpp:112.
                CreateTables(commands);

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
                // cpp parity: BlibMaker.cpp:131 - `-t` enables score-lookup-only mode.
                IsScoreLookupMode = true;
                break;

            case 'z':
                // cpp parity: BlibMaker.cpp:134 - `-z <charges>` is a comma-separated list of
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
                // cpp parity: BlibMaker.cpp:149 - unknown switch -> usage() (which exits).
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
