using System.Data.SQLite;
using System.Globalization;
using System.Text;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// Reader for legacy Bruker BAF (<c>analysis.baf</c>) files via the <c>baf2sql_c.dll</c>
/// SDK. Port of pwiz C++ <c>Baf2SqlImpl</c> in <c>Baf2Sql.cpp</c>.
/// </summary>
/// <remarks>
/// The BAF format is used on micrOTOF / impact / maXis / solariX instruments. baf2sql adds a
/// SQLite cache (<c>analysis.sqlite</c>) next to the .baf — first read materializes it, later
/// reads are fast metadata queries against that cache plus binary array fetches via
/// <c>baf2sql_array_*</c> for actual peaks.
/// </remarks>
internal sealed class Baf2SqlData : IBrukerData
{
    private readonly string _analysisDir;
    private readonly string _bafPath;
    private readonly string _sqlitePath;
    private SQLiteConnection? _db;
    private ulong _arrayHandle;          // baf2sql binary-array storage handle
    private bool _disposed;
    private readonly List<BafRow> _rows = new();
    private (double Low, double High) _mzAcqRange;

    public Baf2SqlData(string analysisDir, bool useRecalibratedState = true)
    {
        _analysisDir = analysisDir;
        _bafPath = Path.Combine(analysisDir, "analysis.baf");
        if (!File.Exists(_bafPath))
            _bafPath = Path.Combine(analysisDir, "Analysis.baf");
        if (!File.Exists(_bafPath))
            throw new FileNotFoundException("analysis.baf not found", _bafPath);

        _arrayHandle = NativeMethods.baf2sql_array_open_storage(useRecalibratedState ? 0 : 1, _bafPath);
        if (_arrayHandle == 0)
            throw new InvalidOperationException(
                "baf2sql_array_open_storage failed: " + GetLastBaf2SqlError());

        _sqlitePath = ResolveSqliteCachePath(_bafPath);
        var connStr = $"Data Source={_sqlitePath};Read Only=True;Pooling=False";
        _db = new SQLiteConnection(connStr);
        _db.Open();

        GlobalMetadata = LoadGlobalMetadata();
        LoadRowsAndAcqRange();
    }

    /// <summary>
    /// Asks baf2sql for the SQLite cache path. Two-call pattern: first call returns the
    /// required buffer length, second call fills the buffer.
    /// </summary>
    private static string ResolveSqliteCachePath(string bafPath)
    {
        uint needed = NativeMethods.baf2sql_get_sqlite_cache_filename(Array.Empty<byte>(), 0, bafPath);
        if (needed == 0)
            throw new InvalidOperationException(
                "baf2sql_get_sqlite_cache_filename failed: " + GetLastBaf2SqlError());
        var buf = new byte[needed];
        uint copied = NativeMethods.baf2sql_get_sqlite_cache_filename(buf, needed, bafPath);
        if (copied == 0)
            throw new InvalidOperationException(
                "baf2sql_get_sqlite_cache_filename(fill) failed: " + GetLastBaf2SqlError());
        // The buffer is null-terminated UTF-8.
        int len = Array.IndexOf(buf, (byte)0);
        if (len < 0) len = buf.Length;
        return Encoding.UTF8.GetString(buf, 0, len);
    }

    private static string GetLastBaf2SqlError()
    {
        var buf = new byte[2048];
        uint copied = NativeMethods.baf2sql_get_last_error_string(buf, (uint)buf.Length);
        if (copied == 0) return "(no error message)";
        int len = Math.Min((int)copied, buf.Length) - 1; // exclude trailing null
        if (len < 0) len = 0;
        return Encoding.UTF8.GetString(buf, 0, len);
    }

    private Dictionary<string, string> LoadGlobalMetadata()
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (_db is null) return dict;
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM Properties";
        try
        {
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string key = rdr.GetString(0);
                string val = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
                dict[key] = val;
            }
        }
        catch { /* missing table — leave empty */ }
        return dict;
    }

    /// <summary>
    /// Mirrors <c>Baf2SqlImpl</c>'s SQL: one row per Spectra entry with AcquisitionKey,
    /// scan range, BPI/TIC, polarity, profile/line array ids, and optional precursor info.
    /// All metadata is loaded up front; binary arrays are fetched on demand by id.
    /// </summary>
    private void LoadRowsAndAcqRange()
    {
        if (_db is null) return;
        using var cmd = _db.CreateCommand();
        // Mirrors Baf2Sql.cpp:121 — Parent is on Spectra, Mass/IsolationType/ReactionType on Steps.
        cmd.CommandText =
            "SELECT s.Id, ak.MsLevel+1, s.Rt, s.Segment, s.AcquisitionKey, " +
            "s.MzAcqRangeLower, s.MzAcqRangeUpper, s.SumIntensity, s.MaxIntensity, ak.Polarity, " +
            "s.ProfileMzId, s.ProfileIntensityId, s.LineMzId, s.LineIntensityId, " +
            "s.Parent, step.Mass, step.IsolationType, step.ReactionType, ak.ScanMode, " +
            "IFNULL(iw.Value, 0) AS IsolationWidth, " +
            "IFNULL(cs.Value, 0) AS ChargeState, " +
            "IFNULL(ce.Value, 0) AS CollisionEnergy " +
            "FROM Spectra s " +
            "JOIN AcquisitionKeys ak ON ak.Id = s.AcquisitionKey " +
            "LEFT JOIN PerSpectrumVariables iw ON iw.Spectrum = s.Id AND iw.Variable = 8 " +
            "LEFT JOIN PerSpectrumVariables cs ON cs.Spectrum = s.Id AND cs.Variable = 6 " +
            "LEFT JOIN PerSpectrumVariables ce ON ce.Spectrum = s.Id AND ce.Variable = 5 " +
            "LEFT JOIN Steps step ON step.TargetSpectrum = s.Id " +
            "ORDER BY s.Rt";

        var ic = CultureInfo.InvariantCulture;
        double globalLow = double.PositiveInfinity, globalHigh = 0;
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            var row = new BafRow
            {
                ScanId = rdr.GetInt64(0),
                MsLevel = rdr.GetInt32(1),
                RetentionTimeSeconds = rdr.GetDouble(2),
                Segment = rdr.GetInt32(3),
                AcquisitionKey = rdr.GetInt32(4),
                MzAcqLow = rdr.GetDouble(5),
                MzAcqHigh = rdr.GetDouble(6),
                Tic = rdr.GetDouble(7),
                Bpi = rdr.GetDouble(8),
                Polarity = rdr.GetInt32(9),
                ProfileMzId = rdr.IsDBNull(10) ? null : (ulong)rdr.GetInt64(10),
                ProfileIntensityId = rdr.IsDBNull(11) ? null : (ulong)rdr.GetInt64(11),
                LineMzId = rdr.IsDBNull(12) ? null : (ulong)rdr.GetInt64(12),
                LineIntensityId = rdr.IsDBNull(13) ? null : (ulong)rdr.GetInt64(13),
                ParentId = rdr.IsDBNull(14) ? null : rdr.GetInt64(14),
                PrecursorMz = rdr.IsDBNull(15) ? null : rdr.GetDouble(15),
                IsolationMode = rdr.IsDBNull(16) ? null : rdr.GetInt32(16),
                ReactionMode = rdr.IsDBNull(17) ? null : rdr.GetInt32(17),
                ScanMode = rdr.GetInt32(18),
                IsolationWidth = rdr.GetDouble(19),
                ChargeState = rdr.GetInt32(20),
                CollisionEnergy = rdr.GetDouble(21),
            };

            // BAF "all-ions" trick (Baf2Sql.cpp:354) — translate broadband-CID MS1 to MS2 with
            // a precursor that spans the entire scan range, so downstream tools see fragments
            // tied to a (very wide) isolation window.
            if (row.MsLevel == 1 && (row.ScanMode == 4 || row.ScanMode == 5))
            {
                row.MsLevel = 2;
                row.IsolationWidth = row.MzAcqHigh - row.MzAcqLow;
                row.PrecursorMz = row.MzAcqLow + 0.5 * row.IsolationWidth;
            }

            _rows.Add(row);

            if (row.MzAcqLow > 0 && row.MzAcqLow < globalLow) globalLow = row.MzAcqLow;
            if (row.MzAcqHigh > globalHigh) globalHigh = row.MzAcqHigh;
        }
        _mzAcqRange = double.IsPositiveInfinity(globalLow)
            ? (0.0, 0.0)
            : (globalLow, globalHigh);
    }

    private sealed class BafRow
    {
        public long ScanId;
        public int MsLevel;
        public double RetentionTimeSeconds;
        public int Segment;
        public int AcquisitionKey;
        public double MzAcqLow;
        public double MzAcqHigh;
        public double Tic;
        public double Bpi;
        public int Polarity;          // 0 = positive, 1 = negative
        public ulong? ProfileMzId;
        public ulong? ProfileIntensityId;
        public ulong? LineMzId;
        public ulong? LineIntensityId;
        public long? ParentId;
        public double? PrecursorMz;
        public int? IsolationMode;
        public int? ReactionMode;
        public int ScanMode;
        public double IsolationWidth;
        public int ChargeState;
        public double CollisionEnergy;
    }

    /// <inheritdoc/>
    public BrukerFormat Format => BrukerFormat.Baf;

    /// <inheritdoc/>
    public string AnalysisDirectory => _analysisDir;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> GlobalMetadata { get; } = new Dictionary<string, string>();

    /// <inheritdoc/>
    public bool HasPasefData => false;

    /// <inheritdoc/>
    public bool HasDiaPasefData => false;

    /// <inheritdoc/>
    public bool HasMs1Frames => _rows.Any(r => r.MsLevel == 1);

    /// <inheritdoc/>
    public bool HasMsNFrames => _rows.Any(r => r.MsLevel > 1);

    /// <inheritdoc/>
    public (double Low, double High) MzAcquisitionRange => _mzAcqRange;

    /// <inheritdoc/>
    public bool IsMaldiSource => false;

    /// <inheritdoc/>
    public IEnumerable<DiaFrameWindow> EnumerateDiaFrameWindows() =>
        Array.Empty<DiaFrameWindow>();

    /// <inheritdoc/>
    public IReadOnlyList<BrukerIndexEntry> BuildSpectrumIndex(
        bool combineIonMobilitySpectra, int preferOnlyMsLevel)
    {
        _ = combineIonMobilitySpectra; // BAF has no mobility dimension.
        var index = new List<BrukerIndexEntry>(_rows.Count);
        foreach (var row in _rows)
        {
            if (preferOnlyMsLevel == 1 && row.MsLevel != 1) continue;
            if (preferOnlyMsLevel == 2 && row.MsLevel < 2) continue;
            // 1-based scan index — BAF nativeID format is "scan=N" (SpectrumList_Bruker.cpp:821).
            int scan = index.Count + 1;
            index.Add(new BrukerIndexEntry
            {
                Index = index.Count,
                Id = "scan=" + scan.ToString(CultureInfo.InvariantCulture),
                Tag = row,
                MsLevel = row.MsLevel,
            });
        }
        return index;
    }

    /// <inheritdoc/>
    public void FillSpectrum(Spectrum spec, BrukerIndexEntry entry, bool getBinaryData,
        bool preferCentroid, bool sortAndJitter)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(entry);
        var row = (BafRow)entry.Tag;

        spec.Params.Set(CVID.MS_ms_level, row.MsLevel);
        spec.Params.Set(row.MsLevel == 1 ? CVID.MS_MS1_spectrum : CVID.MS_MSn_spectrum);

        // Polarity: BAF AcquisitionKeys.Polarity is 0 = positive, 1 = negative.
        if (row.Polarity == 0) spec.Params.Set(CVID.MS_positive_scan);
        else if (row.Polarity == 1) spec.Params.Set(CVID.MS_negative_scan);

        if (row.Tic > 0)
        {
            spec.Params.Set(CVID.MS_base_peak_intensity, row.Bpi);
            spec.Params.Set(CVID.MS_total_ion_current, row.Tic);
        }

        var scan = new Scan();
        if (row.RetentionTimeSeconds > 0)
            scan.Set(CVID.MS_scan_start_time, row.RetentionTimeSeconds, CVID.UO_second);
        if (row.MzAcqLow > 0 && row.MzAcqHigh > 0)
            scan.ScanWindows.Add(new ScanWindow(row.MzAcqLow, row.MzAcqHigh, CVID.MS_m_z));

        spec.ScanList.Set(CVID.MS_no_combination);
        spec.ScanList.Scans.Add(scan);

        if (row.MsLevel > 1 && row.PrecursorMz.HasValue)
            AddPrecursor(spec, row);

        // Decide profile vs centroid. cpp: prefer profile, fall back to line if profile empty
        // OR if caller asked for line data via msLevelsToCentroid (we pass that as preferCentroid).
        double[] mz = Array.Empty<double>();
        double[] intensity = Array.Empty<double>();
        bool isCentroid = preferCentroid;

        if (!isCentroid && row.ProfileIntensityId.HasValue)
        {
            (mz, intensity) = ReadPair(row.ProfileMzId, row.ProfileIntensityId);
            if (mz.Length == 0) isCentroid = true;
        }
        if (mz.Length == 0 && row.LineIntensityId.HasValue)
        {
            (mz, intensity) = ReadPair(row.LineMzId, row.LineIntensityId);
            isCentroid = true;
        }

        spec.Params.Set(isCentroid ? CVID.MS_centroid_spectrum : CVID.MS_profile_spectrum);
        spec.DefaultArrayLength = mz.Length;

        if (mz.Length > 0 && getBinaryData)
            spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
    }

    private static void AddPrecursor(Spectrum spec, BafRow row)
    {
        var precursor = new Precursor();
        double isolationMz = row.PrecursorMz!.Value;
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, isolationMz, CVID.MS_m_z);
        if (row.IsolationWidth > 0)
        {
            double half = row.IsolationWidth / 2.0;
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, half, CVID.MS_m_z);
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, half, CVID.MS_m_z);
        }

        var selected = new SelectedIon();
        selected.Set(CVID.MS_selected_ion_m_z, isolationMz, CVID.MS_m_z);
        if (row.ChargeState > 0)
            selected.Set(CVID.MS_charge_state, row.ChargeState);
        precursor.SelectedIons.Add(selected);

        precursor.Activation.Set(TranslateActivation(row.ScanMode));
        if (row.CollisionEnergy != 0)
            precursor.Activation.Set(CVID.MS_collision_energy, Math.Abs(row.CollisionEnergy));

        spec.Precursors.Add(precursor);
    }

    private static CVID TranslateActivation(int scanMode) => scanMode switch
    {
        4 or 5 => CVID.MS_in_source_collision_induced_dissociation,
        _ => CVID.MS_collision_induced_dissociation,
    };

    private (double[] mz, double[] intensity) ReadPair(ulong? mzId, ulong? intensityId)
    {
        if (!mzId.HasValue || !intensityId.HasValue) return (Array.Empty<double>(), Array.Empty<double>());
        // cpp reads intensity first (smaller — float-backed) then mz (double-backed) using the
        // intensity count to size the mz read. baf2sql_array_read_double returns one double per
        // element either way, so we just match its size.
        var intensity = ReadDoubleArray(intensityId.Value);
        if (intensity.Length == 0) return (Array.Empty<double>(), Array.Empty<double>());
        var mz = ReadDoubleArray(mzId.Value);
        return (mz, intensity);
    }

    /// <inheritdoc/>
    public IEnumerable<BrukerChromatogramPoint> EnumerateChromatogramPoints(int preferOnlyMsLevel)
    {
        foreach (var row in _rows)
        {
            if (preferOnlyMsLevel == 1 && row.MsLevel != 1) continue;
            if (preferOnlyMsLevel == 2 && row.MsLevel < 2) continue;
            yield return new BrukerChromatogramPoint(
                row.RetentionTimeSeconds, row.Tic, row.Bpi, row.MsLevel == 1 ? 1 : 2);
        }
    }

    /// <inheritdoc/>
    public List<LcTrace> ReadLcTraces() => ChromatographyDataSqlite.ReadAll(_analysisDir);

    /// <summary>Reads <paramref name="id"/>'s array as a managed double[]. Returns empty on error.</summary>
    internal double[] ReadDoubleArray(ulong id)
    {
        if (_arrayHandle == 0) return Array.Empty<double>();
        if (NativeMethods.baf2sql_array_get_num_elements(_arrayHandle, id, out ulong n) == 0)
            return Array.Empty<double>();
        if (n == 0) return Array.Empty<double>();
        var buf = new double[(int)n];
        if (NativeMethods.baf2sql_array_read_double(_arrayHandle, id, buf) == 0)
            throw new InvalidOperationException(
                $"baf2sql_array_read_double(id={id}) failed: " + GetLastBaf2SqlError());
        return buf;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _db?.Dispose();
        _db = null;
        if (_arrayHandle != 0)
        {
            NativeMethods.baf2sql_array_close_storage(_arrayHandle);
            _arrayHandle = 0;
        }
    }
}
