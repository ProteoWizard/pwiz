// Port of pwiz_tools/BiblioSpec/src/LibReader.{h,cpp}

using System.Buffers.Binary;
using System.Data.SQLite;
using System.Globalization;
using System.IO.Compression;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Reads a BiblioSpec <c>.blib</c> SQLite library: enumerate <see cref="RefSpectrum"/>
/// rows, look up spectra by id or m/z range, decompress peak blobs.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::LibReader</c> (cpp LibReader.h / LibReader.cpp).</para>
/// <para>cpp behavior preserved 1:1 where it affects observable output:</para>
/// <list type="bullet">
///   <item>Read-only SQLite handle — matches cpp LibReader's "never write" contract
///         (cpp uses bare <c>sqlite3_open</c> but never issues an INSERT/UPDATE; we pin
///         <c>ReadOnly=true</c> so we can't accidentally take a write lock).</item>
///   <item>Peak-blob compression detection is heuristic-by-length: if the blob size matches
///         the expected uncompressed size (<c>numPeaks * sizeof(double)</c> for m/z,
///         <c>numPeaks * sizeof(float)</c> for intensity), the blob is treated as raw;
///         otherwise it's zlib-decompressed. Mirror of cpp <c>getUncompressedPeaks</c>.</item>
///   <item>m/z arrays are little-endian 64-bit doubles, intensity arrays are little-endian
///         32-bit floats. Matches what BlibMaker writes.</item>
///   <item><see cref="GetRefSpec(int)"/>'s <c>prevAA</c> / <c>nextAA</c> behavior is dual:
///         the <c>RefSpectrum getRefSpec(libID)</c> overload populates them from the
///         peptideSeq columns (LibReader.cpp:397-398), while every other entry point hard-codes
///         them to <c>"-"</c> (cpp LibReader.cpp:449-450, 669-670, 722-723). Preserved.</item>
///   <item>When <c>modPrecision_ &gt;= 0</c>, <see cref="GetRefSpec(int)"/> rebuilds the modified
///         sequence from the Modifications table — inserting <c>[+15.99]</c>-style brackets at
///         each position with the configured number of decimals. cpp LibReader.cpp:472-502.</item>
/// </list>
/// </remarks>
public sealed class LibReader : IDisposable
{
    private readonly string _libraryName;
    private readonly int _modPrecision;
    private SQLiteConnection? _db;
    private SQLiteCommand? _enumSpectraStatement;
    private SQLiteDataReader? _enumSpectraReader;
    private int _maxSpecId;
    private int _curSpecId = 1;

    // SQL fragments. cpp uses sprintf-into-buffer; we keep the same column order so the
    // ordinal indices below match the cpp parse code exactly.

    // 12 columns: id, peptideSeq, precursorMZ, precursorCharge, peptideModSeq,
    //             prevAA, nextAA, copies, numPeaks, peakMZ, peakIntensity, retentionTime
    private const string EnumSpectraSql =
        "SELECT id, peptideSeq, precursorMZ, precursorCharge, peptideModSeq, " +
        "prevAA, nextAA, copies, numPeaks, peakMZ, peakIntensity, retentionTime " +
        "FROM RefSpectra, RefSpectraPeaks " +
        "WHERE id = RefSpectraID " +
        "ORDER BY id";

    /// <summary>Construct a closed reader. Mostly here for parity with cpp's default ctor.</summary>
    /// <remarks>cpp LibReader.cpp:30-42. Caller must subsequently set the library path and call
    /// <see cref="Initialize"/> explicitly — but since the path is set at construction in C#,
    /// this overload exists mainly to ease testing.</remarks>
    public LibReader()
    {
        _libraryName = string.Empty;
        _modPrecision = -1;
    }

    /// <summary>
    /// Open <paramref name="libName"/> and prepare the enumeration statement.
    /// </summary>
    /// <param name="libName">Path to the <c>.blib</c> file.</param>
    /// <param name="modPrecision">When &gt;= 0, <see cref="GetRefSpec(int)"/> rebuilds the
    /// modified-peptide-sequence string from the Modifications table with this many decimals
    /// per mod-mass; when -1 (the cpp default), the raw <c>peptideModSeq</c> column is used as-is.</param>
    /// <remarks>cpp LibReader.cpp:44-58 — calls <c>strcpy(libraryName_, libName)</c> then
    /// <c>initialize()</c>. We do the equivalent.</remarks>
    public LibReader(string libName, int modPrecision = -1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libName);
        _libraryName = libName;
        _modPrecision = modPrecision;
        Initialize();
    }

    /// <summary>
    /// Open the SQLite database read-only, find the max RefSpectra id, prepare the
    /// enumeration statement. Idempotent — calling twice re-prepares the statement.
    /// </summary>
    /// <remarks>cpp LibReader.cpp:69-96. Finalises a stale prepared statement before
    /// re-preparing, same as cpp.</remarks>
    public void Initialize()
    {
        if (string.IsNullOrEmpty(_libraryName))
        {
            Verbosity.Error("Could not open database: no library name set.");
            return; // unreachable — Verbosity.Error throws
        }

        // Read-only matches the cpp contract: LibReader never writes.
        try
        {
            _db = SqliteRoutine.Open(_libraryName, readOnly: true);
        }
        catch (Exception ex)
        {
            Verbosity.Error($"Could not open database {_libraryName}. ({ex.Message})");
            return; // unreachable
        }

        SetMaxLibId();

        // cpp LibReader.cpp:79-80 — finalize before re-preparing.
        _enumSpectraReader?.Dispose();
        _enumSpectraReader = null;
        _enumSpectraStatement?.Dispose();

        try
        {
            _enumSpectraStatement = _db!.CreateCommand();
            _enumSpectraStatement.CommandText = EnumSpectraSql;
            _enumSpectraReader = _enumSpectraStatement.ExecuteReader();
        }
        catch (SQLiteException ex)
        {
            Verbosity.Debug($"SQLITE error message: {ex.Message}");
            Verbosity.Error(
                $"LibReader::initialize cannot prepare SQL statement for enumerating spectra " +
                $"from {_libraryName} ({ex.Message})");
        }
    }

    /// <summary>Compute and cache the largest <c>RefSpectra.id</c> in the database.</summary>
    /// <remarks>cpp LibReader.cpp:98-121. cpp's `sqlite3_step` returning
    /// SQLITE_ROW with a NULL column value (the empty-table case for
    /// <c>max(id)</c>) is treated as valid — only a failed-step is an error.
    /// SqliteRoutine.Scalar collapses both into <c>null</c>, so we treat
    /// <c>null</c> as "library is empty, maxSpecId = 0".</remarks>
    private void SetMaxLibId()
    {
        try
        {
            var result = SqliteRoutine.Scalar<long?>(_db!, "SELECT max(id) FROM RefSpectra");
            _maxSpecId = result is null ? 0 : checked((int)result.Value);
            Verbosity.Debug($"Highest lib spec ID is {_maxSpecId}.");
        }
        catch (SQLiteException ex)
        {
            Verbosity.Debug($"SQLITE error message: {ex.Message}");
            Verbosity.Error(
                $"LibReader::setMaxLibId cannot prepare SQL statement for finding maxLibId " +
                $"from {_libraryName} ({ex.Message})");
        }
    }

    /// <summary>The maximum <c>RefSpectra.id</c> in the open library.</summary>
    public int MaxSpecId => _maxSpecId;

    // ---- setters / getters (cpp LibReader.cpp:269-317) --------------------

    /// <summary>Experimental spectral low-end precursor m/z filter (cpp <c>expLowMZ_</c>).</summary>
    public double LowMz { get; set; }

    /// <summary>Experimental spectral high-end precursor m/z filter (cpp <c>expHighMZ_</c>).</summary>
    public double HighMz { get; set; }

    /// <summary>Experimental charge if determined (cpp <c>expPreChg_</c>, default -1).</summary>
    public int Charge { get; set; } = -1;

    /// <summary>Experimental charge range low end if not determined (cpp <c>expLowChg_</c>, default -1).</summary>
    public int LowCharge { get; set; } = -1;

    /// <summary>Experimental charge range high end if not determined (cpp <c>expHighChg_</c>, default -1).</summary>
    public int HighCharge { get; set; } = -1;

    /// <summary>Count of rows in the <c>RefSpectra</c> table.</summary>
    /// <remarks>cpp LibReader.cpp:319-335 — <c>countAllSpec()</c> uses <c>sqlite3_get_table</c>
    /// and <c>atoi(result[1])</c>. The managed equivalent is a scalar query.</remarks>
    public int CountAllSpec()
    {
        try
        {
            var count = SqliteRoutine.Scalar<long?>(_db!, "SELECT count(*) FROM RefSpectra");
            return count is null ? 0 : checked((int)count.Value);
        }
        catch (SQLiteException)
        {
            Verbosity.Error("Can't execute SQL statement to count all spectra");
            return 0; // unreachable
        }
    }

    // ---- spectrum fetch ---------------------------------------------------

    /// <summary>
    /// Select all RefSpectra with precursor m/z in <c>[minMz, maxMz]</c> and at least
    /// <c>minPeaks + 1</c> peaks (cpp uses <c>numPeaks &gt; minPeaks</c>, not <c>&gt;=</c>).
    /// </summary>
    /// <remarks>
    /// <para>cpp LibReader.cpp:129-188 (vector overload) and 197-260 (deque overload). The two cpp
    /// overloads exist purely to choose container; their SQL is identical except for the
    /// <c>&gt;=</c> vs <c>&gt;</c> bound on <c>minMz</c>:</para>
    /// <list type="bullet">
    ///   <item>vector overload: <c>precursorMZ &gt;= minMz</c> (LibReader.cpp:138)</item>
    ///   <item>deque overload: <c>precursorMZ &gt; minMz</c> (LibReader.cpp:206)</item>
    /// </list>
    /// <para>The vector overload is the canonical entrypoint (called from BlibSearch). We use that
    /// behavior here (inclusive low bound).</para>
    /// </remarks>
    public IList<RefSpectrum> GetSpecInMzRange(double minMz, double maxMz, int minPeaks)
    {
        var spectra = new List<RefSpectrum>();
        var sql = string.Format(
            CultureInfo.InvariantCulture,
            "SELECT id, peptideSeq, precursorMZ, precursorCharge, " +
            "peptideModSeq, copies, numPeaks, peakMZ, " +
            "peakIntensity, retentionTime FROM RefSpectra, RefSpectraPeaks " +
            "WHERE precursorMZ >= {0} AND precursorMZ <= {1} " +
            "AND numPeaks > {2} " +
            "AND id = RefSpectraId",
            minMz, maxMz, minPeaks);

        SQLiteDataReader reader;
        try
        {
            using var cmd = _db!.CreateCommand();
            cmd.CommandText = sql;
            reader = cmd.ExecuteReader();
        }
        catch (SQLiteException ex)
        {
            Verbosity.Debug($"SQLITE error message: {ex.Message}");
            Verbosity.Error(
                $"LibReader::getSpecInMzRange cannot prepare SQL select statement for " +
                $"fetching spectra (m/z {minMz:F3}-{maxMz:F3}) from {_libraryName}");
            return spectra; // unreachable
        }

        // cpp LibReader.cpp:156-183 — 10-column form, no prev/nextAA, no setPrevAA/setNextAA.
        using (reader)
        {
            while (reader.Read())
            {
                var tmp = new RefSpectrum();
                tmp.LibSpecId = reader.GetInt32(0);
                tmp.Sequence = SafeGetString(reader, 1);
                tmp.Mz = reader.GetDouble(2);
                tmp.Charge = reader.GetInt32(3);
                tmp.ModifiedSequence = SafeGetString(reader, 4);
                tmp.Copies = reader.GetInt32(5);

                int numPeaks = reader.GetInt32(6);
                var comprM = GetBlob(reader, 7);
                var comprI = GetBlob(reader, 8);

                tmp.RetentionTime = reader.GetDouble(9);

                tmp.SetRawPeaks(GetUncompressedPeaks(numPeaks, comprM, comprI));

                spectra.Add(tmp);
            }
        }

        return spectra;
    }

    /// <summary>
    /// Look up a spectrum by id. Returns <c>null</c> if no row matches (cpp's
    /// <c>bool getRefSpec(int, RefSpectrum&amp;)</c> overload, LibReader.cpp:422-505).
    /// </summary>
    /// <remarks>
    /// <para>cpp LibReader.cpp:444-450 — populates <c>prevAA</c> / <c>nextAA</c> with
    /// <c>"-"</c> rather than the column values, even though the SELECT pulls them. Preserved.</para>
    /// <para>When <see cref="_modPrecision"/> &gt;= 0 (set via constructor), the modified
    /// sequence is rebuilt from the Modifications table by inserting <c>[+15.99]</c>-style
    /// brackets at each position with that many decimals. cpp LibReader.cpp:472-502.</para>
    /// <para>cpp quirk: when multiple modifications occur at the same position, their masses
    /// are summed (LibReader.cpp:489-492). Preserved.</para>
    /// <para>cpp quirk: if a mod's position is past the end of the sequence, it's clamped to
    /// the sequence length (LibReader.cpp:484-486). Preserved.</para>
    /// </remarks>
    public RefSpectrum? GetRefSpec(int libID)
    {
        var sql = string.Format(
            CultureInfo.InvariantCulture,
            "SELECT id, peptideSeq, precursorMZ, precursorCharge, " +
            "peptideModSeq, prevAA, nextAA, copies, numPeaks, peakMZ, " +
            "peakIntensity, retentionTime " +
            "FROM RefSpectra, RefSpectraPeaks WHERE id = {0} " +
            "AND id = RefSpectraID",
            libID);

        RefSpectrum spec;
        using (var cmd = _db!.CreateCommand())
        {
            cmd.CommandText = sql;
            SQLiteDataReader reader;
            try
            {
                reader = cmd.ExecuteReader();
            }
            catch (SQLiteException ex)
            {
                Verbosity.Debug($"SQLITE error message: {ex.Message}");
                Verbosity.Warn(
                    $"LibReader::getRefSpec cannot prepare SQL statement for selecting spectrum " +
                    $"{libID} from {_libraryName}.");
                return null;
            }

            using (reader)
            {
                if (!reader.Read())
                {
                    Verbosity.Debug(
                        $"LibReader::getRefSpec cannot find spectrum {libID} in {_libraryName}.");
                    return null;
                }

                spec = new RefSpectrum
                {
                    LibSpecId = reader.GetInt32(0),
                    Sequence = SafeGetString(reader, 1),
                    Mz = reader.GetDouble(2),
                    Charge = reader.GetInt32(3),
                    ModifiedSequence = SafeGetString(reader, 4),
                    // cpp LibReader.cpp:449-450 — hard-coded "-", not from columns 5/6.
                    PrevAa = "-",
                    NextAa = "-",
                    Copies = reader.GetInt32(7),
                };

                int numPeaks = reader.GetInt32(8);
                var comprM = GetBlob(reader, 9);
                var comprI = GetBlob(reader, 10);

                spec.RetentionTime = reader.GetDouble(11);
                spec.SetRawPeaks(GetUncompressedPeaks(numPeaks, comprM, comprI));
            }
        }

        if (_modPrecision >= 0)
        {
            ApplyModificationsTable(spec, libID);
        }

        return spec;
    }

    /// <summary>
    /// Rebuild <see cref="RefSpectrum.ModifiedSequence"/> from the <c>Modifications</c> table.
    /// </summary>
    /// <remarks>
    /// cpp LibReader.cpp:472-502. The cpp pipeline:
    /// <list type="number">
    ///   <item>Query <c>SELECT position, mass FROM Modifications WHERE RefSpectraId = libID</c>.</item>
    ///   <item>For each row, clamp position to <c>seq.length()</c> and sum masses per position
    ///         into a <c>map&lt;int, double&gt;</c>.</item>
    ///   <item>Iterate the map in <em>reverse</em> order (rbegin → rend) and insert
    ///         <c>"[+15.99]"</c>-style brackets at each position; reverse order keeps earlier
    ///         positions stable as later inserts shift the string.</item>
    /// </list>
    /// </remarks>
    private void ApplyModificationsTable(RefSpectrum spec, int libID)
    {
        // cpp LibReader.cpp:473 — sprintf with %d, no parameter binding.
        var sql = string.Format(
            CultureInfo.InvariantCulture,
            "SELECT position, mass FROM Modifications WHERE RefSpectraId = {0}",
            libID);

        // cpp uses std::map<int, double> for sort+sum; SortedDictionary mirrors the order.
        var mods = new SortedDictionary<int, double>();
        var seq = spec.Sequence;

        try
        {
            using var cmd = _db!.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var position = reader.GetInt32(0);
                var mass = reader.GetDouble(1);
                // cpp LibReader.cpp:484-486 — clamp to sequence length.
                if (position > seq.Length) position = seq.Length;

                if (mods.TryGetValue(position, out var existing))
                {
                    mods[position] = existing + mass;
                }
                else
                {
                    mods[position] = mass;
                }
            }
        }
        catch (SQLiteException ex)
        {
            Verbosity.Error($"Cannot prepare SQL statement: {ex.Message} ");
            return; // unreachable
        }

        // cpp LibReader.cpp:497-500 — iterate in reverse so earlier insertions don't shift
        // the indices of later ones. std::map<>::rbegin gives largest key first.
        var builder = new System.Text.StringBuilder(seq);
        var precisionFmt = "F" + _modPrecision.ToString(CultureInfo.InvariantCulture);
        foreach (var kv in mods.Reverse())
        {
            // cpp LibReader.cpp:498 — "[%s%.*f]" with sign='+' when mass >= 0.
            var sign = kv.Value >= 0 ? "+" : string.Empty;
            var modBuf = string.Concat(
                "[",
                sign,
                kv.Value.ToString(precisionFmt, CultureInfo.InvariantCulture),
                "]");
            builder.Insert(kv.Key, modBuf);
        }

        spec.ModifiedSequence = builder.ToString();
    }

    /// <summary>
    /// Return all spectra with <c>id</c> in <c>[lowLibID, highLibID]</c> (inclusive).
    /// </summary>
    /// <remarks>cpp LibReader.cpp:556-612 — <c>getRefSpecsInRange</c>. Populates
    /// <c>prevAA</c>/<c>nextAA</c> from the columns (unlike <see cref="GetRefSpec(int)"/>).</remarks>
    public IList<RefSpectrum> GetRefSpecsInRange(int lowLibID, int highLibID)
    {
        var specs = new List<RefSpectrum>();
        var sql = string.Format(
            CultureInfo.InvariantCulture,
            "SELECT id, peptideSeq, precursorMZ, precursorCharge, " +
            "peptideModSeq, prevAA, nextAA, copies, numPeaks, peakMZ, peakIntensity, retentionTime " +
            "FROM RefSpectra, RefSpectraPeaks WHERE " +
            "id >= {0} AND id <= {1} AND id = RefSpectraID",
            lowLibID, highLibID);

        try
        {
            using var cmd = _db!.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var tmp = new RefSpectrum();
                tmp.LibSpecId = reader.GetInt32(0);
                tmp.Sequence = SafeGetString(reader, 1);
                tmp.Mz = reader.GetDouble(2);
                tmp.Charge = reader.GetInt32(3);
                tmp.ModifiedSequence = SafeGetString(reader, 4);
                tmp.PrevAa = SafeGetString(reader, 5);
                tmp.NextAa = SafeGetString(reader, 6);
                tmp.Copies = reader.GetInt32(7);

                int numPeaks = reader.GetInt32(8);
                var comprM = GetBlob(reader, 9);
                var comprI = GetBlob(reader, 10);

                tmp.RetentionTime = reader.GetDouble(11);
                tmp.SetRawPeaks(GetUncompressedPeaks(numPeaks, comprM, comprI));
                specs.Add(tmp);
            }
        }
        catch (SQLiteException)
        {
            Verbosity.Error(
                $"Cannot prepare SQL statement for getting spectra of IDs {lowLibID}-{highLibID} " +
                $"from library {_libraryName}.");
            return specs; // unreachable
        }

        return specs;
    }

    /// <summary>
    /// Select all spectra whose precursor m/z and charge match the configured experimental
    /// filters (<see cref="LowMz"/>, <see cref="HighMz"/>, <see cref="Charge"/> /
    /// <see cref="LowCharge"/> / <see cref="HighCharge"/>).
    /// </summary>
    /// <remarks>
    /// <para>cpp LibReader.cpp:623-697. The SQL is built incrementally:</para>
    /// <list type="bullet">
    ///   <item>If <see cref="Charge"/> != -1 (cpp's <c>expPreChg_</c> sentinel), filter on
    ///         <c>precursorCharge = Charge</c>.</item>
    ///   <item>Otherwise filter on <c>precursorCharge BETWEEN LowCharge AND HighCharge</c>.</item>
    /// </list>
    /// <para>cpp also hard-codes <c>prevAA</c>/<c>nextAA</c> to <c>"-"</c> (LibReader.cpp:669-670),
    /// despite the SELECT pulling them. Preserved.</para>
    /// </remarks>
    public IList<RefSpectrum> GetAllRefSpec()
    {
        var specs = new List<RefSpectrum>();

        var sb = new System.Text.StringBuilder();
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "SELECT id, peptideSeq, precursorMZ, precursorCharge, " +
            "peptideModSeq, prevAA, nextAA, copies, numPeaks, peakMZ, " +
            "peakIntensity, retentionTime " +
            "FROM RefSpectra, RefSpectraPeaks " +
            "WHERE precursorMZ >= {0} AND precursorMZ <= {1}",
            LowMz, HighMz);

        if (Charge != -1)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, " AND precursorCharge = {0}", Charge);
        }
        else
        {
            sb.AppendFormat(CultureInfo.InvariantCulture,
                " AND precursorCharge >= {0} AND precursorCharge <= {1}", LowCharge, HighCharge);
        }
        sb.Append(" AND id=RefSpectraID");
        var sql = sb.ToString();

        try
        {
            using var cmd = _db!.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var tmp = new RefSpectrum();
                tmp.LibSpecId = reader.GetInt32(0);
                tmp.Sequence = SafeGetString(reader, 1);
                tmp.Mz = reader.GetDouble(2);
                tmp.Charge = reader.GetInt32(3);
                tmp.ModifiedSequence = SafeGetString(reader, 4);
                // cpp LibReader.cpp:669-670 — hard-coded "-" despite columns 5/6.
                tmp.PrevAa = "-";
                tmp.NextAa = "-";
                tmp.Copies = reader.GetInt32(7);

                int numPeaks = reader.GetInt32(8);
                var comprM = GetBlob(reader, 9);
                var comprI = GetBlob(reader, 10);

                tmp.RetentionTime = reader.GetDouble(11);
                tmp.SetRawPeaks(GetUncompressedPeaks(numPeaks, comprM, comprI));
                specs.Add(tmp);
            }
        }
        catch (SQLiteException)
        {
            Verbosity.Error(
                $"Cannot prepare SQT select statement for fetching library spectra from {_libraryName}.");
            return specs; // unreachable
        }

        return specs;
    }

    /// <summary>
    /// Stateful iterator over every spectrum in the library (ordered by id). Returns
    /// <c>true</c> if a spectrum was hydrated, <c>false</c> after the last spectrum.
    /// </summary>
    /// <remarks>
    /// <para>cpp LibReader.cpp:705-738. Uses the prepared <c>enumSpectraStatement_</c>
    /// that <see cref="Initialize"/> set up.</para>
    /// <para>cpp behavior quirk: even when <c>sqlite3_step</c> returns <c>SQLITE_DONE</c>, cpp
    /// still calls all the <c>sqlite3_column_*</c> accessors on the now-empty row (which return
    /// 0 / null) and assigns them into <paramref name="spec"/> (LibReader.cpp:717-734). The
    /// observable effect is that on the final call the <paramref name="spec"/> is reset to a
    /// zero-id, empty-sequence spectrum and the return value is <c>false</c>. Preserved.</para>
    /// </remarks>
    public bool GetNextSpectrum(out RefSpectrum spec)
    {
        spec = new RefSpectrum();
        if (_enumSpectraReader is null)
        {
            Verbosity.Error($"Fetching library spectra from {_libraryName}.");
            return false; // unreachable
        }

        var hasRow = _enumSpectraReader.Read();
        if (!hasRow)
        {
            Verbosity.Debug("Returned the last spec from the library.");
            // cpp clobbers spec with the zero-row state. Match that.
            _curSpecId++;
            return false;
        }

        spec.LibSpecId = _enumSpectraReader.GetInt32(0);
        spec.Sequence = SafeGetString(_enumSpectraReader, 1);
        spec.Mz = _enumSpectraReader.GetDouble(2);
        spec.Charge = _enumSpectraReader.GetInt32(3);
        spec.ModifiedSequence = SafeGetString(_enumSpectraReader, 4);
        // cpp LibReader.cpp:722-723 — hard-coded "-".
        spec.PrevAa = "-";
        spec.NextAa = "-";
        spec.Copies = _enumSpectraReader.GetInt32(7);

        int numPeaks = _enumSpectraReader.GetInt32(8);
        var comprM = GetBlob(_enumSpectraReader, 9);
        var comprI = GetBlob(_enumSpectraReader, 10);

        spec.RetentionTime = _enumSpectraReader.GetDouble(11);
        spec.SetRawPeaks(GetUncompressedPeaks(numPeaks, comprM, comprI));

        _curSpecId++;
        return true;
    }

    // ---- peak decompression -----------------------------------------------

    /// <summary>
    /// Decode a (m/z, intensity) blob pair into a <see cref="PeakT"/> array. Mirrors cpp
    /// <c>LibReader::getUncompressedPeaks</c> (LibReader.cpp:507-553).
    /// </summary>
    /// <remarks>
    /// <para>Compression detection is heuristic-by-length: if the blob size matches the
    /// expected uncompressed size (<c>numPeaks * 8</c> for m/z, <c>numPeaks * 4</c> for
    /// intensity), the blob is treated as raw little-endian binary; otherwise it's run
    /// through zlib. cpp uses the same heuristic with <c>sizeof(double)</c> / <c>sizeof(float)</c>.</para>
    /// <para>If <paramref name="numPeaks"/> is 0 (degenerate but possible), returns an empty array.</para>
    /// </remarks>
    public static PeakT[] GetUncompressedPeaks(int numPeaks, byte[] comprM, byte[] comprI)
    {
        ArgumentNullException.ThrowIfNull(comprM);
        ArgumentNullException.ThrowIfNull(comprI);

        if (numPeaks <= 0)
            return Array.Empty<PeakT>();

        var uncomprLenM = numPeaks * sizeof(double);
        var uncomprLenI = numPeaks * sizeof(float);

        var mzBytes = comprM.Length == uncomprLenM
            ? comprM
            : InflateZlib(comprM, uncomprLenM);

        var iBytes = comprI.Length == uncomprLenI
            ? comprI
            : InflateZlib(comprI, uncomprLenI);

        var peaks = new PeakT[numPeaks];
        for (var i = 0; i < numPeaks; i++)
        {
            // Little-endian per cpp on-disk format.
            var mz = BinaryPrimitives.ReadDoubleLittleEndian(
                mzBytes.AsSpan(i * sizeof(double), sizeof(double)));
            var intensity = BinaryPrimitives.ReadSingleLittleEndian(
                iBytes.AsSpan(i * sizeof(float), sizeof(float)));
            peaks[i] = new PeakT(mz, intensity);
        }
        return peaks;
    }

    /// <summary>
    /// Inflate a zlib-wrapped blob (as written by zlib's <c>compress()</c>). Allocates a buffer
    /// of <paramref name="expectedSize"/> and reads exactly that many bytes (which matches
    /// cpp's <c>uncompress()</c> contract: the caller passes the expected uncompressed length).
    /// </summary>
    private static byte[] InflateZlib(byte[] compressed, int expectedSize)
    {
        var output = new byte[expectedSize];
        using var input = new MemoryStream(compressed, writable: false);
        using var z = new ZLibStream(input, CompressionMode.Decompress);
        var read = 0;
        while (read < expectedSize)
        {
            var chunk = z.Read(output, read, expectedSize - read);
            if (chunk == 0) break;
            read += chunk;
        }
        if (read != expectedSize)
        {
            throw new BlibException(false,
                $"LibReader: zlib inflate produced {read} bytes, expected {expectedSize}.");
        }
        return output;
    }

    // ---- helpers ----------------------------------------------------------

    private static string SafeGetString(SQLiteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static byte[] GetBlob(SQLiteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return Array.Empty<byte>();
        var len = (int)reader.GetBytes(ordinal, 0, null, 0, 0);
        if (len <= 0) return Array.Empty<byte>();
        var buf = new byte[len];
        reader.GetBytes(ordinal, 0, buf, 0, len);
        return buf;
    }

    // ---- IDisposable ------------------------------------------------------

    /// <inheritdoc/>
    public void Dispose()
    {
        _enumSpectraReader?.Dispose();
        _enumSpectraReader = null;
        _enumSpectraStatement?.Dispose();
        _enumSpectraStatement = null;
        _db?.Dispose();
        _db = null;
    }
}
