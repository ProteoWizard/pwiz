// Port of pwiz_tools/BiblioSpec/src/BuildParser.{h,cpp}
//
// Abstract base for every concrete BiblioSpec results-file reader (pepXML, mzIdentML, SSL,
// MaxQuant msms.txt, etc.). Subclasses populate `Psms` and call `BuildTables` to flush them
// into the .blib SQLite library.
//
// cpp inherits from SAXHandler so its subclasses can use Expat. The C# port drops that —
// readers that need XML parsing will use System.Xml.XmlReader directly.

using System.Buffers;
using System.Data.SQLite;
using System.Globalization;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Abstract base for every BiblioSpec results-file reader. Subclasses parse a particular
/// file format (pepXML, mzIdentML, SSL, MaxQuant msms.txt, etc.), populate
/// <see cref="Psms"/>, and call <see cref="BuildTables"/> to flush them into the .blib.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::BuildParser</c> (BuildParser.h:69). The cpp class inherits
/// from <c>SAXHandler</c> so subclasses get an Expat hook for free; in C# we drop that
/// inheritance and let XML-reading subclasses use <see cref="System.Xml.XmlReader"/>
/// directly.</para>
/// <para>cpp <c>SpecFileReader*</c> is owned by BuildParser and defaults to
/// <c>new PwizReader()</c>. In C# the owner injects an <see cref="ISpecFileReader"/>
/// (typically the pwiz-CLI-backed implementation) via <see cref="SpecReader"/>;
/// BuildParser will dispose it on cleanup.</para>
/// </remarks>
public abstract class BuildParser : IDisposable
{
    /// <summary>Monoisotopic mass of H2O. Used by <see cref="CalculatePeptideMass"/>.</summary>
    /// <remarks>cpp parity: BuildParser.h:43 <c>H2O_MASS</c>.</remarks>
    public const double H2OMass = 18.01056469252;

    // cpp: prepared INSERT INTO RefSpectra statement, lazily initialised in the ctor.
    private SQLiteCommand? _insertSpectrumStmt;

    // cpp parity: full path to the result file (as supplied to the ctor).
    private readonly string _fullFilename;
    // cpp parity: path stripped from full name (with trailing separator).
    private readonly string _filepath;
    // cpp parity: filename stripped of path and extension.
    private readonly string _fileroot;
    // cpp parity: name of the next spectrum file to parse.
    private string _curSpecFileName = string.Empty;

    private ProgressIndicator? _fileProgress;
    private ProgressIndicator? _specProgress;
    private ProgressIndicator? _readAddProgress;
    private int _fileProgressIncrement; // cpp parity: when progress is byte-based not file-based

    // cpp parity: map of input file index -> spectrum file count for that input file
    private readonly Dictionary<int, int> _inputToSpec = new();

    // cpp parity: 128-entry amino-acid mass table, initialised by AminoAcidMasses for avg masses.
    // BuildParser.cpp:39 — fill with zero then initializeMass(table, /*monoisotopic*/1).
    // The cpp ctor passes `1` (true) for monoisotopic; preserved here.
    private readonly double[] _aaMasses;

    // cpp parity: shared path-separator search set used by the file-root-peeling loop.
    private static readonly SearchValues<char> _pathSeparators = SearchValues.Create("/\\");

    // cpp parity: filteredOutPsmCount_ — number of PSMs that did not pass threshold.
    /// <summary>Number of PSMs that did not pass the score threshold during the current parse.</summary>
    /// <remarks>cpp parity: BuildParser.h:101 <c>filteredOutPsmCount_</c>. Subclasses
    /// increment this whenever they discard a PSM for score reasons.</remarks>
    protected internal int FilteredOutPsmCount { get; set; }

    /// <summary>Reference to the library being built.</summary>
    /// <remarks>cpp parity: BuildParser.h:96 <c>blibMaker_</c> (reference, not pointer).</remarks>
    protected internal BlibBuilder BlibMaker { get; }

    /// <summary>Progress of our caller (the BlibBuild driver).</summary>
    /// <remarks>cpp parity: BuildParser.h:97 <c>parentProgress_</c>.</remarks>
    protected internal ProgressIndicator? ParentProgress { get; }

    /// <summary>Temp holding space for the PSM currently being parsed.</summary>
    /// <remarks>cpp parity: BuildParser.h:99 <c>curPSM_</c>.</remarks>
    protected internal PSM? CurPsm { get; set; }

    /// <summary>Collected list of PSMs parsed from this file.</summary>
    /// <remarks>cpp parity: BuildParser.h:100 <c>psms_</c>. Subclasses populate this from
    /// their parse loop; <see cref="BuildTables"/> drains and clears it.</remarks>
    protected internal List<PSM?> Psms { get; } = new();

    /// <summary>Reader for getting peak lists from the spectrum file. Lazily constructed
    /// as <see cref="PwizSharpSpecFileReader"/> on first read; setting a non-null value
    /// disposes the previously-held reader so injecting a mock or alternative impl can't
    /// leak the default.</summary>
    /// <remarks>cpp parity: BuildParser.h:102 <c>specReader_</c>. cpp defaults to
    /// <c>new PwizReader()</c>; the C# port matches but defers construction so tests can
    /// inject before the default is allocated.</remarks>
    protected internal ISpecFileReader? SpecReader
    {
        get => _specReader ??= new PwizSharpSpecFileReader();
        set
        {
            if (ReferenceEquals(_specReader, value)) return;
            _specReader?.Dispose();
            _specReader = value;
        }
    }

    private ISpecFileReader? _specReader;

    /// <summary>How spectra are identified in the result file (scan, index, or name).</summary>
    /// <remarks>cpp parity: BuildParser.h:103 <c>lookUpBy_</c>, default <see cref="SpecIdType.ScanNumberId"/>.</remarks>
    protected internal SpecIdType LookUpBy { get; set; } = SpecIdType.ScanNumberId;

    /// <summary>
    /// True to prefer peaks embedded in the search results file (e.g. Mascot DAT,
    /// MaxQuant msms.txt) over external spectrum files. Defaults to the BlibBuilder's
    /// setting; if unset, true.
    /// </summary>
    /// <remarks>cpp parity: BuildParser.h:104 <c>preferEmbeddedSpectra_</c>; cpp ctor at
    /// BuildParser.cpp:46 reads <c>maker.preferEmbeddedSpectra().get_value_or(true)</c>.</remarks>
    protected internal bool PreferEmbeddedSpectra { get; set; }

    /// <summary>
    /// Construct a BuildParser bound to the given library and result file.
    /// </summary>
    /// <param name="maker">The library being built. Must outlive this parser.</param>
    /// <param name="filename">Path to the result file. Used both for error messages and
    /// as the base path for resolving spectrum-file references.</param>
    /// <param name="parentProgress">Optional caller-supplied progress indicator; nested
    /// indicators are derived from it via <see cref="ParentProgress"/>.</param>
    /// <remarks>cpp parity: BuildParser.cpp:29.</remarks>
    protected BuildParser(BlibBuilder maker, string filename, ProgressIndicator? parentProgress)
    {
        ArgumentNullException.ThrowIfNull(maker);
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        BlibMaker = maker;
        _fullFilename = filename;
        FilteredOutPsmCount = 0;

        // cpp parity: BuildParser.cpp:39 — initialise the average-mass table for charge calc.
        _aaMasses = AminoAcidMasses.BuildMassTable(monoisotopic: true);

        // cpp parity: BuildParser.cpp:43 — split path / fileroot.
        _filepath = BlibUtils.GetPath(_fullFilename);
        _fileroot = BlibUtils.GetFileRoot(_fullFilename);

        // cpp parity: BuildParser.cpp:46 — boost::optional get_value_or(true)
        PreferEmbeddedSpectra = maker.PreferEmbeddedSpectra ?? true;

        ParentProgress = parentProgress;
        _readAddProgress = null;
        _fileProgress = null;
        _specProgress = null;
        CurPsm = null;

        // cpp parity: BuildParser.cpp:53 — `this->specReader_ = new PwizReader()`. Lazy in
        // the C# port (see the SpecReader property): the wrapper is allocated on first read,
        // so tests / subclasses can inject a different reader before any code path triggers
        // the default — no resource leak if the default would have been replaced.

        PrepareInsertSpectrumStatement();
    }

    /// <summary>Releases the prepared INSERT statement and any owned spec reader.</summary>
    /// <remarks>cpp parity: BuildParser.cpp:58 — frees the PSM list, the prepared stmt,
    /// and the spec reader. In C# the PSM list is GC-managed; we clean up the prepared
    /// stmt and (if we own it) the spec reader.</remarks>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the prepared INSERT statement and any owned spec reader.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        _insertSpectrumStmt?.Dispose();
        _insertSpectrumStmt = null;
        // Touch the backing field directly so a never-accessed lazy default isn't
        // materialised purely to be disposed.
        _specReader?.Dispose();
        _specReader = null;
    }

    /// <summary>
    /// Parse the file and append results into <see cref="Psms"/>. Subclasses must
    /// implement this; on success, return <c>true</c>. Throw <see cref="BlibException"/>
    /// on fatal errors.
    /// </summary>
    /// <remarks>cpp parity: BuildParser.h:146 <c>virtual bool parseFile() = 0;</c></remarks>
    public abstract bool ParseFile();

    /// <summary>
    /// Return the score types this reader can produce. cpp parity: BuildParser.h:147
    /// <c>virtual std::vector&lt;PSM_SCORE_TYPE&gt; getScoreTypes() = 0;</c>.
    /// </summary>
    public abstract IList<PsmScoreType> GetScoreTypes();

    /// <summary>
    /// Apply any values carried by subclassed PSM (e.g. SSL RT column values) that
    /// override those found by spectrum lookup. Default implementation does nothing.
    /// </summary>
    /// <remarks>cpp parity: BuildParser.cpp:542.</remarks>
    public virtual void ApplyPsmOverrideValues(PSM psm, SpecData specData)
    {
        // Default implementation does nothing.
    }

    /// <summary>Full path to the file being parsed.</summary>
    /// <remarks>cpp parity: BuildParser.cpp:906.</remarks>
    public string GetFileName() => _fullFilename;

    /// <summary>Name of the spectrum file most recently registered via <see cref="SetSpecFileName(string, bool)"/>.</summary>
    /// <remarks>cpp parity: BuildParser.cpp:910.</remarks>
    public string GetSpecFileName() => _curSpecFileName;

    /// <summary>Path containing the file being parsed (with trailing separator).</summary>
    /// <remarks>cpp parity: BuildParser.cpp:272.</remarks>
    protected internal string GetPsmFilePath() => _filepath;

    /// <summary>cpp parity: BuildParser.cpp:73 — prepare the cached <c>INSERT INTO RefSpectra</c> statement.</summary>
    protected internal void PrepareInsertSpectrumStatement()
    {
        // cpp parity: BuildParser.cpp:69-76. Column list matches the cpp string verbatim;
        // the trailing 25 placeholders cover the 20 explicit cols + 5 SmallMolMetadata cols.
        var stmt = "INSERT INTO RefSpectra(peptideSeq, precursorMZ, precursorCharge, "
                 + "peptideModSeq, prevAA, nextAA, copies, numPeaks, ionMobility, collisionalCrossSectionSqA, "
                 + "ionMobilityHighEnergyOffset, ionMobilityType, retentionTime, startTime, endTime, totalIonCurrent, fileID, "
                 + "specIDinFile, score, scoreType"
                 + SmallMolMetadata.SqlColumnNamesCsv()
                 + ") VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?) ";

        _insertSpectrumStmt?.Dispose();
        _insertSpectrumStmt = BlibMaker.Db.CreateCommand();
        _insertSpectrumStmt.CommandText = stmt;
        // 25 unnamed bind slots, matching the 25 question marks above.
        for (var i = 0; i < 25; i++)
        {
            var p = _insertSpectrumStmt.CreateParameter();
            _insertSpectrumStmt.Parameters.Add(p);
        }
        _insertSpectrumStmt.Prepare();
    }

    // --- progress wiring ------------------------------------------------------------

    /// <summary>
    /// Count the progress of reading the PSM file separately from the progress of adding
    /// spectra to the library. Optionally called by subclasses that want to track the
    /// rate of results being read from the input file.
    /// </summary>
    /// <remarks>
    /// cpp parity: BuildParser.cpp:873. Nesting is parent → readAdd → file → spec, with
    /// readAdd and file being optional.
    /// </remarks>
    protected internal void InitReadAddProgress()
    {
        if (ParentProgress is null) return;
        _readAddProgress = ParentProgress.NewNestedIndicator(2);
    }

    /// <summary>Register the number of spectrum files that will be processed.</summary>
    /// <remarks>cpp parity: BuildParser.cpp:886.</remarks>
    protected internal void InitSpecFileProgress(int numSpecFiles)
    {
        if (_readAddProgress != null)
        {
            _fileProgress = _readAddProgress.NewNestedIndicator(numSpecFiles);
        }
        else if (ParentProgress != null)
        {
            _fileProgress = ParentProgress.NewNestedIndicator(numSpecFiles);
        }
    }

    /// <summary>Register the number of spectra in the current file.</summary>
    /// <remarks>cpp parity: BuildParser.cpp:966.</remarks>
    protected internal void InitSpecProgress(int numSpec)
    {
        // cpp parity: just overwrites the field, no explicit delete needed in C#.
        if (_fileProgress != null)
        {
            _specProgress = _fileProgress.NewNestedIndicator(numSpec);
        }
        else if (ParentProgress != null)
        {
            _specProgress = ParentProgress.NewNestedIndicator(numSpec);
        }
    }

    /// <summary>
    /// Tell BuildParser the next progress increment is <paramref name="size"/> bytes —
    /// used for pep.xml-style readers where progress tracks bytes read rather than
    /// number of spectrum files.
    /// </summary>
    /// <remarks>cpp parity: BuildParser.cpp:1040.</remarks>
    protected internal void SetNextProgressSize(int size)
    {
        _fileProgressIncrement = size;
    }

    // --- spectrum-file resolution ---------------------------------------------------

    /// <summary>
    /// Register the name of the next spectrum file to be read by a full / explicit path.
    /// </summary>
    /// <param name="specfile">Path to the spectrum file.</param>
    /// <param name="checkFile">When true, verifies <paramref name="specfile"/> exists.</param>
    /// <remarks>cpp parity: BuildParser.cpp:196.</remarks>
    protected internal void SetSpecFileName(string specfile, bool checkFile = true)
    {
        ArgumentNullException.ThrowIfNull(specfile);
        _curSpecFileName = string.Empty;
        if (checkFile && !File.Exists(specfile))
        {
            throw new BlibException(true,
                $"Could not open spectrum file '{specfile}' for search results file '{_fullFilename}'.");
        }
        _curSpecFileName = specfile;
    }

    /// <summary>
    /// Register the next spectrum file by searching for one matching
    /// <c>{specfileroot}{extension}</c> across <paramref name="directories"/> (and
    /// the result file's own path).
    /// </summary>
    /// <param name="specfileroot">Basename of the file (may include parent path).</param>
    /// <param name="extensions">Extensions tried in priority order. Each should include the leading dot.</param>
    /// <param name="directories">Additional directories to search after the result file's
    /// own directory.</param>
    /// <remarks>cpp parity: BuildParser.cpp:92. Searches are case-insensitive on Windows
    /// (preserves cpp's <c>bal::iequals</c> behaviour everywhere). If nothing is found,
    /// throws <see cref="BlibException"/> with a multi-line "Could not find any of..." message.</remarks>
    protected internal void SetSpecFileName(
        string specfileroot,
        IList<string> extensions,
        IList<string>? directories = null)
    {
        ArgumentNullException.ThrowIfNull(specfileroot);
        ArgumentNullException.ThrowIfNull(extensions);

        _curSpecFileName = string.Empty;
        var localDirectories = directories is null
            ? new List<string>()
            : new List<string>(directories);

        // cpp parity: BuildParser.cpp:101 — replace backslashes with forward slashes so
        // Windows-style paths in the input parse on POSIX. C# Path.* handles both natively
        // on Windows, but we keep the rewrite for parity with the cpp test fixtures.
        specfileroot = specfileroot.Replace('\\', '/');

        // cpp parity: BuildParser.cpp:104 — if specfileroot has a parent path, prepend
        // that directory to the search list.
        var parentDir = Path.GetDirectoryName(specfileroot);
        if (!string.IsNullOrEmpty(parentDir))
        {
            try
            {
                // cpp parity: bfs::absolute(parent, filepath_) ; bfs::exists(...) check.
                var probe = Path.IsPathRooted(parentDir)
                    ? parentDir
                    : Path.Combine(_filepath.Length == 0 ? "." : _filepath, parentDir);
                if (Directory.Exists(probe))
                {
                    localDirectories.Insert(0, parentDir);
                }
            }
            catch
            {
                // cpp parity: BuildParser.cpp:113 — swallow any error from the probe.
            }
        }

        // cpp parity: peel down to just the file portion before searching by extension.
        var fileroot = Path.GetFileName(specfileroot);
        Verbosity.Debug($"checking for basename: {fileroot}");

        do
        {
            // cpp parity: BuildParser.cpp:124 — iterate over [-1, ..., size-1].
            // -1 means "the result file's own directory" (_filepath).
            for (var i = -1; i < localDirectories.Count; i++)
            {
                var path = _filepath;
                if (i >= 0)
                {
                    path = Path.IsPathRooted(localDirectories[i])
                        ? localDirectories[i]
                        : path + localDirectories[i];
                }
                if (string.IsNullOrEmpty(path))
                    path = ".";

                if (!Directory.Exists(path))
                    continue;

                foreach (var ext in extensions)
                {
                    string[] entries;
                    try
                    {
                        entries = Directory.GetFileSystemEntries(path);
                    }
                    catch (IOException)
                    {
                        continue;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }

                    foreach (var entry in entries)
                    {
                        var trialName = Path.GetFileName(entry);
                        // cpp parity: BuildParser.cpp:140 — case-insensitive comparison.
                        if (!string.Equals(fileroot + ext, trialName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (Directory.Exists(entry))
                        {
                            _curSpecFileName = entry;
                            break;
                        }

                        // cpp parity: BuildParser.cpp:148 — try opening to ensure we can read it.
                        try
                        {
                            using var probe = File.OpenRead(entry);
                            _curSpecFileName = entry;
                            break;
                        }
                        catch (IOException)
                        {
                            Verbosity.Comment(VerbosityLevel.Detail,
                                $"cannot open spectrum file {entry}");
                        }
                        catch (UnauthorizedAccessException)
                        {
                            Verbosity.Comment(VerbosityLevel.Detail,
                                $"cannot open spectrum file {entry}");
                        }
                    }

                    if (!string.IsNullOrEmpty(_curSpecFileName))
                        break;
                }

                if (!string.IsNullOrEmpty(_curSpecFileName))
                    break;
            }

            // cpp parity: BuildParser.cpp:168 — if nothing found, peel one extension off the
            // basename and retry. Proteome Discoverer leaves ".msf" on its pepXML names, etc.
            if (string.IsNullOrEmpty(_curSpecFileName))
            {
                var slash = fileroot.AsSpan().LastIndexOfAny(_pathSeparators);
                var dot = fileroot.LastIndexOf('.');
                if (dot < 0 || (slash >= 0 && dot < slash))
                    fileroot = string.Empty;
                else
                    fileroot = fileroot.Substring(0, dot);
            }
        } while (string.IsNullOrEmpty(_curSpecFileName) && !string.IsNullOrEmpty(fileroot));

        if (string.IsNullOrEmpty(_curSpecFileName))
        {
            var msg = FileNotFoundMessage(specfileroot, extensions, localDirectories);
            throw new BlibException(true, msg);
        }

        Verbosity.Comment(VerbosityLevel.Detail, $"spectrum filename set to {_curSpecFileName}");
    }

    /// <summary>Configure whether to prefer embedded spectra over external files.</summary>
    /// <remarks>cpp parity: BuildParser.cpp:263.</remarks>
    protected internal void SetPreferEmbeddedSpectra(bool preferEmbeddedSpectra)
    {
        PreferEmbeddedSpectra = preferEmbeddedSpectra;
    }

    // --- file-not-found message construction ----------------------------------------

    /// <summary>cpp parity: BuildParser.cpp:215.</summary>
    protected internal string FileNotFoundMessage(
        string specfileroot,
        IList<string> extensions,
        IList<string>? directories)
    {
        return FilesNotFoundMessage(new[] { specfileroot }, extensions, directories);
    }

    /// <summary>cpp parity: BuildParser.cpp:228.</summary>
    protected internal string FilesNotFoundMessage(
        IList<string> specfileroots,
        IList<string> extensions,
        IList<string>? directories)
    {
        ArgumentNullException.ThrowIfNull(specfileroots);
        ArgumentNullException.ThrowIfNull(extensions);

        if (extensions.Count == 0)
            throw new BlibException(false, "empty extensions list for filesNotFoundMessage");

        var extString = string.Join(", ", extensions);
        var filesPlural = "file";
        var namesPlural = "name";
        if (specfileroots.Count > 1)
        {
            filesPlural = "files";
            namesPlural = "names";
        }

        var messageString =
            $"While searching for spectrum {filesPlural} for the search results file '{_fullFilename}', " +
            $"could not find matches for the following base{namesPlural} with any of the supported file " +
            $"extensions ({extString}):";
        foreach (var root in specfileroots)
            messageString += "\n" + root;

        // cpp parity: BuildParser.cpp:249 — show the deepest absolute path we checked plus
        // each of the additional directories. cpp uses pwiz::util::canonical; we substitute
        // Path.GetFullPath, which on Windows gives an absolute, separator-preferred path.
        var deepestPath = string.IsNullOrEmpty(_filepath)
            ? Environment.CurrentDirectory
            : _filepath;
        string deepestCanonical;
        try { deepestCanonical = Path.GetFullPath(deepestPath); }
        catch { deepestCanonical = deepestPath; }
        messageString += "\n\nIn any of the following directories:\n" + deepestCanonical;

        if (directories != null)
        {
            // cpp parity: cpp inserts into a set<string> then iterates in reverse to dedupe and
            // print in reverse-sorted order. We replicate that ordering.
            var parentPaths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var dir in directories)
            {
                string canonical;
                try
                {
                    canonical = Path.IsPathRooted(dir)
                        ? Path.GetFullPath(dir)
                        : Path.GetFullPath(Path.Combine(deepestCanonical, dir));
                }
                catch
                {
                    canonical = dir;
                }
                parentPaths.Add(canonical);
            }
            foreach (var dir in parentPaths.Reverse())
                messageString += "\n" + dir;
        }

        return messageString;
    }

    // --- sequence verification ------------------------------------------------------

    /// <summary>
    /// Uppercase every <see cref="PSM.UnmodSeq"/>, sort each PSM's mods, and if no
    /// modified sequence was supplied, synthesize one.
    /// </summary>
    /// <remarks>cpp parity: BuildParser.cpp:697.</remarks>
    protected internal void VerifySequences()
    {
        foreach (var psm in Psms)
        {
            if (psm is null) continue;

            // cpp parity: boost::to_upper — ASCII uppercase. We use the BCL invariant
            // upper which folds the same set of ASCII letters.
            psm.UnmodSeq = psm.UnmodSeq.ToUpperInvariant();

            if (string.IsNullOrEmpty(psm.ModifiedSeq))
            {
                SortPsmMods(psm);
                psm.ModifiedSeq = BlibMaker.GenerateModifiedSeq(psm.UnmodSeq, psm.Mods);
            }
            else
            {
                psm.ModifiedSeq = psm.ModifiedSeq.ToUpperInvariant();
            }
        }
    }

    /// <summary>Score threshold for a given input file type.</summary>
    /// <remarks>cpp parity: BuildParser.cpp:902.</remarks>
    protected internal double GetScoreThreshold(BuildInput fileType) =>
        BlibMaker.GetScoreThreshold(fileType);

    // --- insertSpectrumFilename / insertProtein --------------------------------------

    /// <summary>
    /// Insert the full path of <paramref name="filename"/> into the SpectrumSourceFiles
    /// table (or return the existing row's id if already present).
    /// </summary>
    /// <param name="filename">Spectrum file path (or root, when <paramref name="insertAsIs"/> is false).</param>
    /// <param name="insertAsIs">When true, store the path verbatim; when false, expand to absolute first.</param>
    /// <param name="workflowType">Workflow type tag for this source file.</param>
    /// <returns>The new (or existing) SpectrumSourceFiles row id.</returns>
    /// <remarks>cpp parity: BuildParser.cpp:289.</remarks>
    protected internal long InsertSpectrumFilename(string filename, bool insertAsIs = false, WorkflowType workflowType = WorkflowType.Dda)
    {
        ArgumentNullException.ThrowIfNull(filename);

        var fullPath = insertAsIs ? filename : BlibUtils.GetAbsoluteFilePath(filename);

        // cpp parity: BuildParser.cpp:301 — see if the file is already registered.
        var existingFileId = BlibMaker.GetFileId(fullPath, BlibMaker.GetCutoffScore());
        if (existingFileId >= 0)
            return existingFileId;

        long fileId = BlibMaker.AddFile(fullPath, BlibMaker.GetCutoffScore(), _fullFilename, workflowType);

        const int maxSpectrumFiles = 2000;
        var curFile = BlibMaker.CurFile;
        if (!_inputToSpec.TryGetValue(curFile, out var existingCount))
        {
            _inputToSpec[curFile] = 1;
        }
        else
        {
            var newCount = existingCount + 1;
            if (newCount > maxSpectrumFiles)
            {
                throw new BlibException(false,
                    $"Maximum limit of {maxSpectrumFiles} spectrum source files was exceeded. " +
                    "There was most likely a problem reading the filenames.");
            }
            _inputToSpec[curFile] = newCount;
        }
        Verbosity.Comment(VerbosityLevel.Detail,
            $"Input file {curFile} has had {_inputToSpec[curFile]} spectrum source files inserted");

        return fileId;
    }

    /// <summary>Insert a protein into the Proteins table if not already present.</summary>
    /// <returns>The Proteins row id for this accession.</returns>
    /// <remarks>cpp parity: BuildParser.cpp:326.</remarks>
    protected internal long InsertProtein(Protein protein)
    {
        ArgumentNullException.ThrowIfNull(protein);

        // cpp parity: SELECT id WHERE accession = '...'
        var selectSql = "SELECT id FROM Proteins WHERE accession = '" +
            SqliteRoutine.EscapeApostrophes(protein.Accession) + "'";

        using (var selectCmd = BlibMaker.Db.CreateCommand())
        {
            selectCmd.CommandText = selectSql;
            using var reader = selectCmd.ExecuteReader();
            if (reader.Read())
            {
                return reader.GetInt64(0);
            }
        }

        // cpp parity: INSERT then return last_insert_rowid().
        var insertSql = "INSERT INTO Proteins (accession) VALUES('" +
            SqliteRoutine.EscapeApostrophes(protein.Accession) + "')";
        BlibMaker.SqlStmt(insertSql);
        return BlibMaker.Db.LastInsertRowId;
    }

    // --- sorting helpers -------------------------------------------------------------

    /// <summary>
    /// Sort the PSMs if the score type calls for it. Currently only
    /// <see cref="PsmScoreType.HardklorIdotp"/> triggers a sort.
    /// </summary>
    /// <remarks>cpp parity: BuildParser.cpp:350 <c>OptionalSort</c>.</remarks>
    protected internal void OptionalSort(PsmScoreType scoreType)
    {
        if (scoreType != PsmScoreType.HardklorIdotp)
            return;

        // cpp parity: BuildParser.cpp:354-372 — stable_sort by (mass asc, charge asc,
        // score desc); pull the mass from "mass<value>_RT<value>" in moleculeName.
        Psms.Sort((a, b) =>
        {
            if (a is null || b is null)
            {
                // cpp parity: nulls sort to the end, but sort consistency matters.
                if (a is null && b is null) return 0;
                return a is null ? 1 : -1;
            }

            var massA = ParseHardklorMass(a.SmallMolMetadata.MoleculeName);
            var massB = ParseHardklorMass(b.SmallMolMetadata.MoleculeName);
            if (massA == massB)
            {
                if (a.Charge == b.Charge)
                    return b.Score.CompareTo(a.Score); // high score first
                return a.Charge.CompareTo(b.Charge);
            }
            return massA.CompareTo(massB);
        });
    }

    // cpp parity: BuildParser.cpp:362 — pulls "123.45" out of "mass123.45_RT6.78".
    private static double ParseHardklorMass(string moleculeName)
    {
        if (string.IsNullOrEmpty(moleculeName) || moleculeName.Length < 5)
            return 0;
        // cpp: substr(4, find("_")) — i.e. start at index 4, length = index-of-"_".
        // That's actually a length, not an end-index; cpp's find("_") returns the
        // absolute position from index 0, so this is "everything from index 4 to that
        // position". For a string like "mass123.45_RT6.78", find("_")==10 so it parses
        // "123.45_RT" — but atof / lexical_cast stops at the first non-numeric. We replicate
        // by truncating at the underscore.
        var underscore = moleculeName.IndexOf('_', 4);
        var raw = underscore < 0
            ? moleculeName.Substring(4)
            : moleculeName.Substring(4, underscore - 4);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var mass)
            ? mass
            : 0;
    }

    /// <summary>Sort a PSM's mods by position ascending, merging mods at the same position.</summary>
    /// <remarks>cpp parity: BuildParser.cpp:979 <c>sortPsmMods</c>.</remarks>
    private static void SortPsmMods(PSM psm)
    {
        var mods = psm.Mods;
        if (mods.Count <= 1) return;

        mods.Sort((a, b) => a.Position.CompareTo(b.Position));

        // cpp parity: BuildParser.cpp:984 — fold mods at the same position into one.
        for (var i = 0; i < mods.Count - 1; i++)
        {
            while (i < mods.Count - 1 && mods[i].Position == mods[i + 1].Position)
            {
                // SeqMod is a record struct (immutable); replace the combined slot.
                mods[i] = new SeqMod(mods[i].Position, mods[i].DeltaMass + mods[i + 1].DeltaMass);
                mods.RemoveAt(i + 1);
            }
        }
    }

    /// <summary>
    /// Calculate the (theoretical) peptide mass for <paramref name="psm"/>: H2O plus
    /// residue masses plus mod deltas.
    /// </summary>
    /// <remarks>cpp parity: BuildParser.cpp:994.</remarks>
    private double CalculatePeptideMass(PSM psm)
    {
        var total = H2OMass;
        var seq = psm.UnmodSeq;

        for (var i = 0; i < seq.Length; i++)
        {
            var c = seq[i];
            if (c < _aaMasses.Length)
            {
                var curMass = _aaMasses[c];
                if (curMass > 0)
                    total += curMass;
                else
                    Verbosity.Warn(
                        $"Ignoring unrecognized amino acid '{c}' during calculation of peptide mass: {seq}");
            }
            else
            {
                Verbosity.Warn(
                    $"Ignoring unrecognized amino acid '{c}' during calculation of peptide mass: {seq}");
            }
        }

        foreach (var mod in psm.Mods)
            total += mod.DeltaMass;

        return total;
    }

    /// <summary>
    /// Calculate the charge state from a neutral mass and observed precursor m/z.
    /// Returns -1 if the result is implausible.
    /// </summary>
    /// <remarks>cpp parity: BuildParser.cpp:1022 — <c>floor(estCharge + 0.5)</c> rounds to nearest int.</remarks>
    private static int CalculateCharge(double neutralMass, double precursorMz)
    {
        var estCharge = neutralMass / (precursorMz - AminoAcidMasses.ProtonMass);
        if (estCharge < 0.5)
            return -1;

        return (int)Math.Floor(estCharge + 0.5);
    }

    // --- buildTables driver ---------------------------------------------------------

    /// <summary>
    /// Flush the accumulated PSMs into the library, fetching spectra from the current
    /// spectrum file as needed. Must be called once per spectrum file after
    /// <see cref="SetSpecFileName(string, bool)"/>.
    /// </summary>
    /// <param name="scoreType">Score type to record for these PSMs.</param>
    /// <param name="specFilename">Override spectrum filename to record (cpp uses this when
    /// the spectra logically came from a different file than the one being read).</param>
    /// <param name="showSpecProgress">When true, advance the per-spectrum progress indicator.</param>
    /// <param name="workflowType">Workflow type for the spectrum source file.</param>
    /// <remarks>cpp parity: BuildParser.cpp:385.</remarks>
    protected internal void BuildTables(
        PsmScoreType scoreType,
        string specFilename = "",
        bool showSpecProgress = true,
        WorkflowType workflowType = WorkflowType.Dda)
    {
        if (Psms.Count == 0)
        {
            Verbosity.Warn(
                $"No matches passed score filter in {_curSpecFileName}. {FilteredOutPsmCount} matches did not pass filter.");
            _curSpecFileName = string.Empty;
            AdvanceFileProgress();
            return;
        }

        Verbosity.Status(
            $"Read {Psms.Count} matches that passed the score filter ({FilteredOutPsmCount} matches did not pass).");

        // cpp parity: BuildParser.cpp:403
        VerifySequences();

        // cpp parity: BuildParser.cpp:406 — filter by target sequences.
        FilterBySequence(BlibMaker.TargetSequences, BlibMaker.TargetSequencesModified);

        var hasMatches = Psms.Count > 0;
        if (!hasMatches)
            Verbosity.Status($"No matches left after filtering for target sequences in {_curSpecFileName}.");

        OptionalSort(scoreType);

        // cpp parity: BuildParser.cpp:416 — prune duplicates unless we're keeping ambiguous.
        if (!KeepAmbiguous())
        {
            RemoveDuplicates();
            RemoveNulls();
            hasMatches = Psms.Count > 0;
            if (!hasMatches)
                Verbosity.Status($"No matches left after removing ambiguous spectra in {_curSpecFileName}.");
        }

        // cpp parity: BuildParser.cpp:424 — determine whether we need to open the spectrum file.
        var needsSpectra = false;
        foreach (var psm in Psms)
        {
            if (psm != null && !psm.IsPrecursorOnly())
            {
                needsSpectra = true;
                break;
            }
        }

        // cpp parity: BuildParser.cpp:434 — NonRedundantPSM check on the first element.
        var psmsAreNonRedundant = hasMatches && Psms.Count > 0 && Psms[0] is NonRedundantPSM;

        if (SpecReader != null)
        {
            if (!psmsAreNonRedundant && needsSpectra)
            {
                Verbosity.Status($"Loading {_curSpecFileName}.");
                SpecReader.OpenFile(_curSpecFileName);
                Verbosity.Status($"Reading spectra from {_curSpecFileName}.");
            }
        }
        else if (needsSpectra && !psmsAreNonRedundant)
        {
            throw new BlibException(true,
                $"Cannot read spectrum file '{_curSpecFileName}' with NULL reader.");
        }

        InitSpecProgress(Psms.Count);

        BlibMaker.BeginTransaction();

        // cpp parity: BuildParser.cpp:455 — register the spectrum file.
        var fileId = 0;
        if (!psmsAreNonRedundant)
        {
            if (string.IsNullOrEmpty(specFilename))
                fileId = (int)InsertSpectrumFilename(_curSpecFileName, insertAsIs: false, workflowType);
            else
                fileId = (int)InsertSpectrumFilename(specFilename, insertAsIs: true, workflowType);
        }

        Verbosity.Debug($"BuildParser lookup method is {BlibUtils.SpecIdTypeToString(LookUpBy)}");

        var proteinIds = new Dictionary<Protein, long>();

        // cpp parity: BuildParser.cpp:468 — iterate every PSM.
        for (var i = 0; i < Psms.Count; i++)
        {
            var psm = Psms[i];
            if (psm is null) continue;

            var curSpectrum = new SpecData();

            // cpp parity: BuildParser.cpp:474 — skip spectrum lookup for precursor-only.
            bool success;
            if (needsSpectra && SpecReader != null)
            {
                // SpecReader.GetSpectrum (PSM-overload) lives on SpecFileReaderBase; if the
                // implementation derives from that base it'll do the dispatch. Otherwise
                // we cannot proceed because the interface doesn't carry the PSM-overload.
                if (SpecReader is SpecFileReaderBase srBase)
                {
                    success = srBase.GetSpectrum(psm, LookUpBy, curSpectrum, getPeaks: !psm.IsPrecursorOnly());
                }
                else
                {
                    // cpp parity: dispatch by lookUpBy_, mirroring SpecFileReader::getSpectrum
                    // default implementation. We replicate it inline rather than requiring
                    // SpecFileReaderBase.
                    success = DispatchSpecLookup(psm, curSpectrum);
                }
            }
            else
            {
                success = true;
            }

            if (!success)
            {
                var idStr = psm.IdAsString();
                Verbosity.Warn($"Did not find spectrum '{idStr}' in '{_curSpecFileName}'.");
                continue;
            }

            ApplyPsmOverrideValues(psm, curSpectrum);

            // cpp parity: BuildParser.cpp:485 — sum intensities into totalIonCurrent for non-precursor-only.
            curSpectrum.TotalIonCurrent = 0;
            if (!psm.IsPrecursorOnly())
            {
                if (curSpectrum.Intensities != null)
                {
                    for (var j = 0; j < curSpectrum.NumPeaks; j++)
                        curSpectrum.TotalIonCurrent += curSpectrum.Intensities[j];
                }
            }
            else
            {
                curSpectrum.NumPeaks = 0;
            }

            Verbosity.Comment(VerbosityLevel.Detail,
                $"Adding spectrum key={psm.SpecKey} index={psm.SpecIndex} name={psm.SpecName} charge={psm.Charge}.");

            try
            {
                InsertSpectrum(psm, curSpectrum, fileId, scoreType, proteinIds);

                if (showSpecProgress)
                    _specProgress?.Increment();
            }
            catch (BlibException e)
            {
                e.AddMessage(
                    " Could not add spectrum to library: id " + psm.IdAsString() +
                    $", charge {psm.Charge.ToString(CultureInfo.InvariantCulture)}, " +
                    $"sequence (unmodified) {psm.UnmodSeq}, " +
                    $"score {psm.Score.ToString(CultureInfo.InvariantCulture)}, " +
                    $"from file {_fullFilename}.");
                if (!e.HasFilename) e.HasFilename = true;
                throw;
            }
        }

        BlibMaker.EndTransaction();

        // cpp parity: BuildParser.cpp:521 — clear the PSM list.
        Psms.Clear();

        _curSpecFileName = string.Empty;
        AdvanceFileProgress();
    }

    // cpp parity: BuildParser.cpp:529 — common tail of buildTables when a file finishes.
    private void AdvanceFileProgress()
    {
        if (_fileProgress is null) return;
        if (_fileProgressIncrement == 0)
            _fileProgress.Increment();
        else
            _fileProgress.Add(_fileProgressIncrement);
    }

    // Fallback dispatcher for the case where SpecReader is ISpecFileReader but not
    // SpecFileReaderBase. Mirrors cpp SpecFileReader.h:60 default impl.
    private bool DispatchSpecLookup(PSM psm, SpecData returnData)
    {
        var isMs1 = psm.IsPrecursorOnly();
        var getPeaks = !isMs1;
        var findBy = LookUpBy;
        if (isMs1 && psm.SpecKey == -1 && findBy == SpecIdType.ScanNumberId)
            findBy = SpecIdType.NameId;

        return findBy switch
        {
            SpecIdType.NameId => SpecReader!.GetSpectrum(psm.SpecName, returnData, getPeaks),
            SpecIdType.ScanNumberId => SpecReader!.GetSpectrum(psm.SpecKey, returnData, findBy, getPeaks),
            SpecIdType.IndexId => SpecReader!.GetSpectrum(psm.SpecIndex, returnData, findBy, getPeaks),
            _ => false,
        };
    }

    // --- insertSpectrum --------------------------------------------------------------

    /// <summary>
    /// Persist a single PSM + its spectrum into the library. This is the hot loop body of
    /// <see cref="BuildTables"/>. cpp parity: BuildParser.cpp:556.
    /// </summary>
    /// <remarks>
    /// cpp parity: this method binds the prepared INSERT INTO RefSpectra statement, runs
    /// it, then inserts the peaks blob + the modifications + the protein mappings. Order
    /// of binding MUST match the column list in <see cref="PrepareInsertSpectrumStatement"/>.
    /// </remarks>
    private void InsertSpectrum(
        PSM psm,
        SpecData curSpectrum,
        int fileId,
        PsmScoreType scoreType,
        Dictionary<Protein, long> proteins)
    {
        // cpp parity: BuildParser.cpp:564 — pretty-print the PSM id once for log messages.
        var specIdStr = psm.IdAsString();

        // cpp parity: BuildParser.cpp:567 — if charge is unknown, try to compute it.
        if (psm.Charge == 0)
        {
            Verbosity.Debug(
                $"Attempting to calculate charge state for spectrum {specIdStr} ({psm.ModifiedSeq})");
            var pepMass = CalculatePeptideMass(psm);
            var calcCharge = CalculateCharge(pepMass, curSpectrum.Mz);
            if (calcCharge > 0)
            {
                psm.Charge = calcCharge;
            }
            else
            {
                Verbosity.Warn(
                    $"Could not calculate charge state for spectrum {specIdStr} " +
                    $"({psm.ModifiedSeq}, mass {pepMass.ToString(CultureInfo.InvariantCulture)} " +
                    $"and precursor m/z {curSpectrum.Mz.ToString(CultureInfo.InvariantCulture)})");
            }
        }

        // cpp parity: BuildParser.cpp:582 — drop entries with unwanted charges.
        if (!BlibMaker.KeepCharge(psm.Charge))
            return;

        var nrpsm = psm as NonRedundantPSM;

        if (_insertSpectrumStmt is null)
            throw new InvalidOperationException("INSERT statement not prepared.");

        // cpp parity: the bind order MUST agree with PrepareInsertSpectrumStatement's
        // column list. See BuildParser.cpp:590-635 for the cpp version.
        var p = _insertSpectrumStmt.Parameters;

        p[0].Value = psm.UnmodSeq;
        p[1].Value = psm.SmallMolMetadata.PrecursorMzDeclared == 0
            ? curSpectrum.Mz
            : psm.SmallMolMetadata.PrecursorMzDeclared;
        p[2].Value = psm.Charge;
        p[3].Value = psm.ModifiedSeq;
        p[4].Value = "-";
        p[5].Value = "-";
        p[6].Value = nrpsm?.Copies ?? 1;
        p[7].Value = curSpectrum.NumPeaks;
        // cpp parity: BuildParser.cpp:599 — if the PSM didn't supply an IM, fall back to
        // the spectrum's.
        p[8].Value = psm.IonMobilityType == IonMobilityType.None
            ? (object)curSpectrum.IonMobility
            : psm.IonMobility;
        p[9].Value = curSpectrum.Ccs;
        p[10].Value = curSpectrum.GetIonMobilityHighEnergyOffset();
        p[11].Value = (int)(psm.IonMobilityType == IonMobilityType.None
            ? curSpectrum.IonMobilityType
            : psm.IonMobilityType);

        // cpp parity: BuildParser.cpp:603 — null retentionTime if 0.
        p[12].Value = curSpectrum.RetentionTime != 0 ? (object)curSpectrum.RetentionTime : DBNull.Value;

        // cpp parity: BuildParser.cpp:608 — startTime / endTime are linked.
        if (curSpectrum.StartTime != 0 && curSpectrum.EndTime != 0)
        {
            p[13].Value = curSpectrum.StartTime;
            p[14].Value = curSpectrum.EndTime;
        }
        else
        {
            p[13].Value = DBNull.Value;
            p[14].Value = DBNull.Value;
        }

        // cpp parity: BuildParser.cpp:615 — null TIC for precursor-only.
        if (psm.IsPrecursorOnly())
            p[15].Value = DBNull.Value;
        else
            p[15].Value = curSpectrum.TotalIonCurrent;

        // cpp parity: BuildParser.cpp:623 — NonRedundantPSM overrides fileId.
        p[16].Value = nrpsm?.FileId ?? fileId;

        // cpp parity: BuildParser.cpp:624 — empty SpecID for precursor-only.
        p[17].Value = psm.IsPrecursorOnly() ? string.Empty : specIdStr;

        p[18].Value = psm.Score;
        p[19].Value = (int)scoreType;

        // small-mol columns (5)
        p[20].Value = psm.SmallMolMetadata.MoleculeName;
        p[21].Value = psm.SmallMolMetadata.ChemicalFormula;
        p[22].Value = psm.SmallMolMetadata.PrecursorAdduct;
        p[23].Value = psm.SmallMolMetadata.InchiKey;
        p[24].Value = psm.SmallMolMetadata.OtherKeys;

        try
        {
            _insertSpectrumStmt.ExecuteNonQuery();
        }
        catch (SQLiteException ex)
        {
            // cpp parity: BuildParser.cpp:638 — Verbosity::error logs and throws.
            Verbosity.Error($"Error inserting spectrum row: {ex.Message}");
        }

        // cpp parity: BuildParser.cpp:646 — pull last_insert_rowid for the new spectrum.
        var libSpecId = (int)BlibMaker.Db.LastInsertRowId;

        // cpp parity: BuildParser.cpp:649 — write the peaks blob.
        BlibMaker.InsertPeaks(
            libSpecId,
            curSpectrum.NumPeaks,
            curSpectrum.Mzs is null ? ReadOnlySpan<double>.Empty : curSpectrum.Mzs.AsSpan(0, curSpectrum.NumPeaks),
            curSpectrum.Intensities is null ? ReadOnlySpan<float>.Empty : curSpectrum.Intensities.AsSpan(0, curSpectrum.NumPeaks));

        // cpp parity: BuildParser.cpp:656 — emit one row per non-zero modification.
        foreach (var mod in psm.Mods)
        {
            if (mod.DeltaMass == 0) continue;
            var sql = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO Modifications(RefSpectraID, position, mass) VALUES({0},{1},{2})",
                libSpecId, mod.Position, mod.DeltaMass);
            BlibMaker.SqlStmt(sql);
        }

        // cpp parity: BuildParser.cpp:672 — emit one row per protein mapping; first
        // insert the protein if we haven't seen it.
        foreach (var protein in psm.Proteins)
        {
            if (!proteins.TryGetValue(protein, out var proteinId))
            {
                proteinId = InsertProtein(protein);
                proteins[protein] = proteinId;
            }
            var sql = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO RefSpectraProteins (RefSpectraId, ProteinId) VALUES ({0}, {1})",
                libSpecId, proteinId);
            BlibMaker.SqlStmt(sql);
        }
    }

    // --- filter / dedupe -------------------------------------------------------------

    /// <summary>
    /// Erase all PSMs except those whose unmodified sequence is in
    /// <paramref name="targetSequences"/> OR whose modified sequence is in
    /// <paramref name="targetSequencesModified"/>.
    /// </summary>
    /// <remarks>cpp parity: BuildParser.cpp:720.</remarks>
    private void FilterBySequence(
        IReadOnlySet<string>? targetSequences,
        IReadOnlySet<string>? targetSequencesModified)
    {
        if (targetSequences is null && targetSequencesModified is null) return;

        for (var i = Psms.Count - 1; i >= 0; i--)
        {
            var psm = Psms[i];
            if (psm is null) continue;

            // cpp parity: keep if unmod matches.
            if (targetSequences != null && targetSequences.Contains(psm.UnmodSeq))
                continue;

            // cpp parity: keep if low-precision modSeq matches.
            if (targetSequencesModified != null)
            {
                var normalized = BlibBuilder.GetLowPrecisionModSeq(psm.UnmodSeq, psm.Mods);
                if (targetSequencesModified.Contains(normalized))
                    continue;
            }

            Psms.RemoveAt(i);
        }
    }

    /// <summary>
    /// Find spectra in the PSM list appearing twice; remove duplicates unless they
    /// differ only by an I/L swap (in which case both are kept).
    /// </summary>
    /// <remarks>cpp parity: BuildParser.cpp:762. Subclasses override to change semantics.</remarks>
    protected virtual void RemoveDuplicates()
    {
        // cpp parity: id -> index in psms_.
        var keyIndexPairs = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < Psms.Count; i++)
        {
            var psm = Psms[i];
            if (psm is null) continue;
            if (psm.IsPrecursorOnly()) continue; // no spectrum to disambiguate

            // cpp parity: pick the id matching lookUpBy_.
            var id = LookUpBy switch
            {
                SpecIdType.IndexId => psm.SpecIndex.ToString(CultureInfo.InvariantCulture),
                SpecIdType.NameId => psm.SpecName,
                _ => psm.SpecKey.ToString(CultureInfo.InvariantCulture),
            };

            if (!keyIndexPairs.TryGetValue(id, out var dupIndex))
            {
                keyIndexPairs[id] = i;
                continue;
            }

            var dupPsm = Psms[dupIndex];
            // case 1: duplicate already removed because of a third id collision.
            if (dupPsm is null)
            {
                Verbosity.Comment(VerbosityLevel.Debug,
                    $"Removing duplicate spectrum id '{id}' with sequence {psm.ModifiedSeq}.");
                if (BlibMaker.AmbiguityMessages)
                    Console.Out.WriteLine("AMBIGUOUS:" + psm.ModifiedSeq);
                Psms[i] = null;
            }
            // case 2: identical modSeq — remove one.
            else if (string.Equals(psm.ModifiedSeq, dupPsm.ModifiedSeq, StringComparison.Ordinal))
            {
                Psms[i] = null;
            }
            // case 3: differ only by I/L — keep both, but update the map to point at
            // the new index (cpp parity: BuildParser.cpp:813).
            else if (SeqsILEquivalent(psm.ModifiedSeq, dupPsm.ModifiedSeq))
            {
                keyIndexPairs[id] = i;
            }
            // case 4: truly different sequences — remove both as ambiguous.
            else
            {
                Verbosity.Comment(VerbosityLevel.Debug,
                    $"Removing duplicate spectra id '{id}', sequences {psm.ModifiedSeq} and {dupPsm.ModifiedSeq}.");
                if (BlibMaker.AmbiguityMessages)
                {
                    Console.Out.WriteLine("AMBIGUOUS:" + psm.ModifiedSeq);
                    Console.Out.WriteLine("AMBIGUOUS:" + dupPsm.ModifiedSeq);
                }
                Psms[i] = null;
                Psms[dupIndex] = null;
            }
        }
    }

    /// <summary>Whether to keep duplicate / ambiguous spectra. Forwards to the BlibMaker's flag.</summary>
    /// <remarks>cpp parity: BuildParser.cpp:547. Subclasses override to change semantics.</remarks>
    protected virtual bool KeepAmbiguous() => BlibMaker.KeepAmbiguous;

    /// <summary>Drop all null slots from <see cref="Psms"/>, preserving order.</summary>
    /// <remarks>cpp parity: BuildParser.cpp:838.</remarks>
    private void RemoveNulls()
    {
        Psms.RemoveAll(p => p is null);
    }

    /// <summary>
    /// Read an mzXML to map scan names (Spectrum Mill style) onto scan indexes. cpp
    /// parity: BuildParser.cpp:920.
    /// </summary>
    /// <remarks>
    /// The cpp implementation uses the <c>mzxmlFinder</c> helper. That helper hasn't been
    /// ported to C# yet because only the Spectrum-Mill reader uses it. When a reader that
    /// needs this method lands, port <c>mzxmlFinder</c> alongside it. Today this throws
    /// <see cref="NotImplementedException"/> so callers fail loudly rather than silently.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Instance method on cpp; future implementation will use Psms and SpecReader.")]
    protected internal void FindScanIndexFromName(IDictionary<PSM, double> precursorMap)
    {
        ArgumentNullException.ThrowIfNull(precursorMap);
        // touch an instance member so the analyzer is satisfied today; future port will
        // use Psms + _curSpecFileName + SpecReader to map names → indices.
        _ = Psms;
        throw new NotImplementedException(
            "FindScanIndexFromName requires the mzxmlFinder helper, which is only used by the " +
            "Spectrum Mill pepXML reader. Port it alongside that reader.");
    }

    // --- static helpers --------------------------------------------------------------

    /// <summary>
    /// Compare two sequences treating I, L, and J as equivalent. cpp parity:
    /// BuildParser.cpp:683 <c>seqsILEquivalent</c> — a free function in cpp, kept on
    /// BuildParser here since that's the only caller.
    /// </summary>
    public static bool SeqsILEquivalent(string seq1, string seq2)
    {
        ArgumentNullException.ThrowIfNull(seq1);
        ArgumentNullException.ThrowIfNull(seq2);
        if (seq1.Length != seq2.Length) return false;
        for (var i = 0; i < seq1.Length; i++)
        {
            var c1 = seq1[i];
            var c2 = seq2[i];
            if (c1 == c2) continue;
            // cpp parity: both characters must come from {I, L, J} for the swap to count.
            var c1IsIlj = c1 == 'I' || c1 == 'L' || c1 == 'J';
            var c2IsIlj = c2 == 'I' || c2 == 'L' || c2 == 'J';
            if (!c1IsIlj || !c2IsIlj) return false;
        }
        return true;
    }

    /// <summary>
    /// Returns true iff every token in <paramref name="tokens"/> parses as a non-zero
    /// integer that consumes its entire string.
    /// </summary>
    /// <remarks>
    /// cpp parity: BuildParser.cpp:1141. cpp's loop uses stringstream + reads-to-EOF +
    /// zero-check; we mirror exactly: a token like "0" or "1foo" or "" returns false.
    /// </remarks>
    public static bool ValidInts(IEnumerable<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        foreach (var token in tokens)
        {
            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return false;
            if (v == 0) return false;
        }
        return true;
    }

    /// <summary>
    /// Inspect a spectrum-id string for the originating filename. Looks for "File:..."
    /// markers, "&lt;basename&gt;-MSILE-..." prefixes, and TPP/SEQUEST
    /// "&lt;basename&gt;.&lt;start&gt;.&lt;end&gt;.&lt;charge&gt;[.dta]" / Proteome Discoverer
    /// "&lt;basename&gt;-&lt;id&gt;-&lt;start&gt;_&lt;end&gt;" patterns. Returns empty when no
    /// pattern matches.
    /// </summary>
    /// <remarks>cpp parity: BuildParser.cpp:1048.</remarks>
    protected internal static string GetFilenameFromId(string idStr)
    {
        ArgumentNullException.ThrowIfNull(idStr);
        var filename = string.Empty;

        // cpp parity: BuildParser.cpp:1052 — look for "File:".
        const string fileMarker = "File:";
        var start = idStr.IndexOf(fileMarker, StringComparison.Ordinal);
        if (start >= 0)
        {
            start += fileMarker.Length;
            // cpp parity: BuildParser.cpp:1056 — strip leading spaces.
            while (start < idStr.Length && idStr[start] == ' ')
                start++;

            if (start < idStr.Length)
            {
                int end;
                if (idStr[start] == '"')
                {
                    end = idStr.IndexOf('"', ++start);
                }
                else if (idStr[start] == '~')
                {
                    end = idStr.IndexOf('~', ++start);
                }
                else
                {
                    // cpp parity: BuildParser.cpp:1067 — end at next comma, else at the space
                    // before the next attribute colon, else at EOS.
                    end = idStr.IndexOf(',', start);
                    if (end < 0)
                    {
                        var nextAttr = idStr.IndexOf(':', start);
                        if (nextAttr >= 0)
                        {
                            end = idStr.LastIndexOf(' ', nextAttr);
                            if (end < start)
                                end = -1;
                            else if (end >= 0)
                            {
                                // cpp parity: BuildParser.cpp:1080 — strip trailing spaces.
                                // find_last_not_of(' ', end) + 1 — i.e. trim back through spaces.
                                while (end > start && idStr[end - 1] == ' ')
                                    end--;
                            }
                        }
                    }
                }

                if (end < 0)
                    end = idStr.Length;
                filename = idStr.Substring(start, end - start);
            }
        }

        // cpp parity: BuildParser.cpp:1093 — "<basename>-MSILE-..." takes precedence.
        var msileStart = idStr.IndexOf("-MSILE-", StringComparison.Ordinal);
        if (msileStart >= 0)
            return idStr.Substring(0, msileStart);

        // cpp parity: BuildParser.cpp:1098 — TPP/SEQUEST "<basename>.<start>.<end>.<charge>[.dta]".
        var raw = string.IsNullOrEmpty(filename) ? idStr : filename;
        var parts = raw.Split('.');

        if ((parts.Length == 4 || (parts.Length == 5 && string.Equals(parts[^1], "dta", StringComparison.Ordinal)))
            && ValidInts(parts.Skip(1).Take(3)))
        {
            filename = parts[0];

            // cpp parity: BuildParser.cpp:1105 — strip the "ScaffoldIDNumber_<n>_" prefix if present.
            const string scaffoldPrefix = "ScaffoldIDNumber_";
            if (filename.StartsWith(scaffoldPrefix, StringComparison.Ordinal))
            {
                var endPrefix = filename.IndexOf('_', scaffoldPrefix.Length);
                if (endPrefix > 0 && endPrefix < filename.Length - 1)
                {
                    var numPart = filename.Substring(scaffoldPrefix.Length, endPrefix - scaffoldPrefix.Length);
                    if (int.TryParse(numPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n != 0)
                    {
                        filename = filename.Substring(endPrefix + 1);
                    }
                }
            }
        }

        // cpp parity: BuildParser.cpp:1115 — Proteome Discoverer "<basename>-<spectrumId>-<start>_<end>".
        if (string.IsNullOrEmpty(filename))
        {
            var lastDash = idStr.LastIndexOf('-');
            if (lastDash > 0)
            {
                var lastDash2 = idStr.LastIndexOf('-', lastDash - 1);
                if (lastDash2 > 0)
                {
                    var suffixStart = lastDash + 1;
                    var spectrumStart = lastDash2 + 1;
                    var startAndEnd = idStr.Substring(suffixStart);
                    var pdParts = new List<string>(startAndEnd.Split('_'));

                    if (pdParts.Count == 2)
                    {
                        pdParts.Add(idStr.Substring(spectrumStart, lastDash - spectrumStart));
                        if (ValidInts(pdParts))
                        {
                            filename = idStr.Substring(0, lastDash2);
                        }
                    }
                }
            }
        }

        return filename;
    }
}
