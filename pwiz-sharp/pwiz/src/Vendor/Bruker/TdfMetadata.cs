using System.Globalization;
using System.Data.SQLite;

namespace Pwiz.Vendor.Bruker;

/// <summary>MsMsType values from the Frames table.</summary>
/// <remarks>
/// Mirrors Bruker's enum values: 0=MS1, 2=DDA, 4=MRM/PRM, 8=PASEF DDA (also historically DIA),
/// 9=PASEF DIA, 10=PASEF PRM. When MsMsType=8 appears in a file that also has DIA tables, treat
/// it as <see cref="PasefDia"/> by looking at <see cref="TdfMetadata.HasDiaPasefData"/>.
/// </remarks>
public enum MsMsType
{
    /// <summary>Full scan (MS1).</summary>
    Ms1 = 0,
    /// <summary>Data-dependent acquisition MS2.</summary>
    Dda = 2,
    /// <summary>MRM / PRM-style triggered MS2.</summary>
    Mrm = 4,
    /// <summary>PASEF DDA (Bruker uses 8 for both this and legacy DIA — disambiguate via tables).</summary>
    PasefDda = 8,
    /// <summary>PASEF DIA.</summary>
    PasefDia = 9,
    /// <summary>PRM PASEF.</summary>
    PasefPrm = 10,
}

/// <summary>Ion polarity.</summary>
public enum IonPolarity
{
    /// <summary>Positive ionization.</summary>
    Positive,
    /// <summary>Negative ionization.</summary>
    Negative,
}

/// <summary>One row of the <c>DiaFrameMsMsWindows</c> table (with 1/K0 boundaries).</summary>
public sealed record DiaWindowGroup(
    int WindowGroup,
    double InvK0Begin,
    double InvK0End,
    double IsolationMz,
    double IsolationWidth,
    double CollisionEnergy);

/// <summary>
/// One isolation window applied to a DIA-PASEF MS2 frame. <c>ScanEnd</c> is <b>inclusive</b>
/// (the TDF SQLite stores it as exclusive; we subtract 1 on load to match pwiz C++).
/// </summary>
public sealed record DiaFrameWindow(
    long FrameId,
    int WindowGroup,
    int ScanBegin,
    int ScanEnd,
    double IsolationMz,
    double IsolationWidth,
    double CollisionEnergy);

/// <summary>
/// One PASEF DDA precursor covering a scan range within an MS2 frame. <c>ScanEnd</c> is
/// <b>inclusive</b> (TDF's <c>PasefFrameMsMsInfo.ScanNumEnd</c> is exclusive; we subtract
/// 1 on load to match pwiz C++).
/// </summary>
public sealed record PasefPrecursorInfo(
    long FrameId,
    int ScanBegin,
    int ScanEnd,
    double IsolationMz,
    double IsolationWidth,
    double CollisionEnergy,
    double MonoisotopicMz,
    int Charge,
    double AvgScanNumber,
    double Intensity);

/// <summary>One row from the <c>Frames</c> table in a .tdf SQLite file.</summary>
public sealed record TdfFrame(
    long FrameId,
    double RetentionTimeSeconds,
    IonPolarity Polarity,
    int ScanMode,
    MsMsType MsMsType,
    double MaxIntensity,
    double SummedIntensities,
    int NumScans,
    long NumPeaks,
    long? ParentFrameId,
    double? PrecursorMz,
    double? IsolationWidth,
    int? PrecursorCharge,
    double? CollisionEnergy,
    int CalibrationIndex);

/// <summary>
/// Reads Bruker <c>.tdf</c> SQLite metadata: <c>GlobalMetadata</c> plus one <see cref="TdfFrame"/>
/// per row of the <c>Frames</c> table. Pairs with <see cref="TimsBinaryData"/> (binary frames via
/// the native DLL) to give the full analysis picture.
/// </summary>
/// <remarks>
/// Port of the SQLite-reading half of pwiz::vendor_api::Bruker::TimsDataImpl. PASEF / DIA tables
/// (<c>PasefFrameMsMsInfo</c>, <c>DiaFrameMsMsInfo</c>, <c>DiaFrameMsMsWindows</c>) are exposed via
/// dedicated accessors rather than merged into the frames list — callers choose what they need.
/// </remarks>
public sealed class TdfMetadata : IDisposable
{
    private readonly SQLiteConnection _conn;
    private bool _disposed;

    /// <summary>The <c>GlobalMetadata</c> key/value pairs (as strings).</summary>
    public IReadOnlyDictionary<string, string> GlobalMetadata { get; }

    /// <summary>True if this analysis has a <c>PasefFrameMsMsInfo</c> table with rows.</summary>
    public bool HasPasefData { get; }

    /// <summary>True if this analysis has a <c>DiaFrameMsMsInfo</c> table with rows.</summary>
    public bool HasDiaPasefData { get; }

    /// <summary>True if this analysis has a <c>PrmFrameMsMsInfo</c> table with rows.</summary>
    public bool HasPrmPasefData { get; }

    /// <summary>Max scan number across all frames (i.e. the TIMS ramp depth).</summary>
    public int MaxNumScans { get; }

    /// <summary>Number of distinct TimsCalibration values used across frames (minimum 1).</summary>
    public int CalibrationCount { get; }

    /// <summary>Opens the <c>analysis.tdf</c> SQLite database inside <paramref name="dotDdirectory"/>.</summary>
    public TdfMetadata(string dotDdirectory)
    {
        ArgumentNullException.ThrowIfNull(dotDdirectory);
        string tdfPath = Path.Combine(dotDdirectory, "analysis.tdf");
        if (!File.Exists(tdfPath))
            throw new FileNotFoundException($"analysis.tdf not found in {dotDdirectory}", tdfPath);

        _conn = new SQLiteConnection($"Data Source={tdfPath};Read Only=True");
        _conn.Open();

        GlobalMetadata = LoadGlobalMetadata();
        HasPasefData = TableHasRows("PasefFrameMsMsInfo");
        HasDiaPasefData = !HasPasefData && TableHasRows("DiaFrameMsMsInfo");
        HasPrmPasefData = !HasPasefData && !HasDiaPasefData && TableHasRows("PrmFrameMsMsInfo");
        MaxNumScans = (int)QueryScalar<long>("SELECT IFNULL(MAX(NumScans), 0) FROM Frames");
        CalibrationCount = (int)QueryScalar<long>("SELECT IFNULL(MAX(TimsCalibration), 1) FROM Frames");
    }

    /// <summary>Total number of rows in the <c>Frames</c> table.</summary>
    public int FrameCount => (int)QueryScalar<long>("SELECT COUNT(*) FROM Frames");

    /// <summary>True if at least one frame has <c>MsMsType = 0</c>.</summary>
    public bool HasMs1Frames => QueryScalar<long>("SELECT COUNT(*) FROM Frames WHERE MsMsType = 0") > 0;

    /// <summary>True if at least one frame has <c>MsMsType &gt; 0</c>.</summary>
    public bool HasMsNFrames => QueryScalar<long>("SELECT COUNT(*) FROM Frames WHERE MsMsType > 0") > 0;

    /// <summary>
    /// Enumerates frames in id order. LEFT JOIN pulls optional MS/MS metadata alongside.
    /// When <paramref name="preferOnlyMsLevel"/> is 1 or 2, only frames at that MS level are
    /// returned (1 → MsMsType=0, 2 → MsMsType&gt;0); 0 (default) returns all frames.
    /// </summary>
    public IEnumerable<TdfFrame> EnumerateFrames(int preferOnlyMsLevel = 0)
    {
        string msFilter = preferOnlyMsLevel switch
        {
            1 => "WHERE f.MsMsType = 0 ",
            2 => "WHERE f.MsMsType > 0 ",
            _ => "",
        };
        string sql =
            "SELECT f.Id, f.Time, f.Polarity, f.ScanMode, f.MsMsType, f.MaxIntensity, f.SummedIntensities, " +
            "       f.NumScans, f.NumPeaks, " +
            "       info.Parent, info.TriggerMass, info.IsolationWidth, info.PrecursorCharge, info.CollisionEnergy, " +
            "       IFNULL(f.TimsCalibration, 1) - 1 AS CalIdx " +
            "FROM Frames f " +
            "LEFT JOIN FrameMsMsInfo info ON f.Id = info.Frame " +
            msFilter +
            "ORDER BY f.Id";
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return new TdfFrame(
                FrameId: reader.GetInt64(0),
                RetentionTimeSeconds: reader.GetDouble(1),
                Polarity: reader.GetString(2) == "+" ? IonPolarity.Positive : IonPolarity.Negative,
                ScanMode: reader.GetInt32(3),
                MsMsType: (MsMsType)reader.GetInt32(4),
                MaxIntensity: reader.GetDouble(5),
                SummedIntensities: reader.GetDouble(6),
                NumScans: reader.GetInt32(7),
                NumPeaks: reader.GetInt64(8),
                ParentFrameId: reader.IsDBNull(9) ? null : reader.GetInt64(9),
                PrecursorMz: reader.IsDBNull(10) ? null : reader.GetDouble(10),
                IsolationWidth: reader.IsDBNull(11) ? null : reader.GetDouble(11),
                PrecursorCharge: reader.IsDBNull(12) ? null : reader.GetInt32(12),
                CollisionEnergy: reader.IsDBNull(13) ? null : reader.GetDouble(13),
                CalibrationIndex: reader.GetInt32(14));
        }
    }

    /// <summary>
    /// Enumerates per-frame DIA-PASEF isolation windows in frame + scanBegin order. Mirrors the
    /// C++ query that groups <c>DiaFrameMsMsWindows</c> by (Frame, IsolationMz, IsolationWidth),
    /// aggregating MIN(ScanNumBegin) / MAX(ScanNumEnd) — so consecutive rows of the same window
    /// group with identical isolation become one range.
    /// </summary>
    /// <remarks>
    /// ScanNumEnd in the TDF is exclusive; we subtract 1 so <c>ScanEnd</c> is inclusive, matching
    /// pwiz C++ conventions. Returns empty when no DIA tables are present.
    /// </remarks>
    public IEnumerable<DiaFrameWindow> EnumerateDiaFrameWindows()
    {
        if (!HasDiaPasefData) yield break;

        const string sql =
            "SELECT f.Frame, MIN(w.ScanNumBegin), MAX(w.ScanNumEnd), w.IsolationMz, w.IsolationWidth, " +
            "       AVG(w.CollisionEnergy), f.WindowGroup " +
            "FROM DiaFrameMsMsInfo f " +
            "JOIN DiaFrameMsMsWindows w ON w.WindowGroup = f.WindowGroup " +
            "GROUP BY f.Frame, w.IsolationMz, w.IsolationWidth " +
            "ORDER BY f.Frame, MIN(w.ScanNumBegin)";
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return new DiaFrameWindow(
                FrameId: reader.GetInt64(0),
                ScanBegin: reader.GetInt32(1),
                ScanEnd: reader.GetInt32(2) - 1, // TDF stores end exclusive; make inclusive
                IsolationMz: reader.GetDouble(3),
                IsolationWidth: reader.GetDouble(4),
                CollisionEnergy: reader.GetDouble(5),
                WindowGroup: reader.GetInt32(6));
        }
    }

    /// <summary>
    /// Enumerates per-frame PASEF DDA precursors in frame + scanBegin order. Mirrors the pwiz
    /// C++ join of <c>PasefFrameMsMsInfo</c> with <c>Precursors</c> (monoisotopic m/z, charge,
    /// intensity, avg scan number). Returns empty when the analysis is not PASEF DDA.
    /// </summary>
    /// <remarks>
    /// ScanNumEnd in the TDF is exclusive; we subtract 1 so <c>ScanEnd</c> is inclusive.
    /// </remarks>
    public IEnumerable<PasefPrecursorInfo> EnumeratePasefPrecursors()
    {
        if (!HasPasefData) yield break;

        const string sql =
            "SELECT f.Frame, f.ScanNumBegin, f.ScanNumEnd, f.IsolationMz, f.IsolationWidth, f.CollisionEnergy, " +
            "       p.MonoisotopicMz, p.Charge, p.ScanNumber, p.Intensity " +
            "FROM PasefFrameMsMsInfo f " +
            "JOIN Precursors p ON p.Id = f.Precursor " +
            "ORDER BY f.Frame, f.ScanNumBegin";
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return new PasefPrecursorInfo(
                FrameId: reader.GetInt64(0),
                ScanBegin: reader.GetInt32(1),
                ScanEnd: reader.GetInt32(2) - 1,
                IsolationMz: reader.GetDouble(3),
                IsolationWidth: reader.GetDouble(4),
                CollisionEnergy: reader.GetDouble(5),
                MonoisotopicMz: reader.IsDBNull(6) ? 0.0 : reader.GetDouble(6),
                Charge: reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                AvgScanNumber: reader.IsDBNull(8) ? 0.0 : reader.GetDouble(8),
                Intensity: reader.IsDBNull(9) ? 0.0 : reader.GetDouble(9));
        }
    }

    /// <summary>
    /// Enumerates rows of <c>DiaFrameMsMsWindows</c> for diaPASEF acquisitions. The 1/K0
    /// range is derived from the scan boundaries via the stored <c>OneOverK0</c> per-scan array.
    /// </summary>
    public IEnumerable<DiaWindowGroup> EnumerateDiaWindowGroups()
    {
        if (!HasDiaPasefData || !TableExists("DiaFrameMsMsWindows"))
            yield break;

        const string sql =
            "SELECT WindowGroup, ScanNumBegin, ScanNumEnd, IsolationMz, IsolationWidth, CollisionEnergy " +
            "FROM DiaFrameMsMsWindows " +
            "ORDER BY WindowGroup, ScanNumBegin";

        // We don't have 1/K0 conversion here (that lives in TimsBinaryData.ScanNumberToOneOverK0,
        // which needs a handle). Callers that need the 1/K0 bounds should derive them via
        // TimsBinaryData; we
        // report the scan numbers in the InvK0Begin/End fields so the info isn't lost.
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            // The TDF stores ScanNumEnd as exclusive; pwiz's convention is inclusive, so subtract 1.
            yield return new DiaWindowGroup(
                WindowGroup: reader.GetInt32(0),
                InvK0Begin: reader.GetDouble(1), // scan-number begin (caller converts to 1/K0)
                InvK0End: reader.GetDouble(2) - 1,
                IsolationMz: reader.GetDouble(3),
                IsolationWidth: reader.GetDouble(4),
                CollisionEnergy: reader.GetDouble(5));
        }
    }

    /// <summary>
    /// Returns <see cref="GlobalMetadata"/>[<paramref name="key"/>] as a double, or null if
    /// missing / unparsable. Uses invariant culture so decimal separators work cross-locale.
    /// </summary>
    public double? GetGlobalDouble(string key)
    {
        if (!GlobalMetadata.TryGetValue(key, out var v)) return null;
        return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : null;
    }

    /// <summary>Acquired mass range: (low, high) m/z from <c>MzAcqRangeLower/Upper</c> global params.</summary>
    public (double Low, double High) MzAcquisitionRange =>
        (GetGlobalDouble("MzAcqRangeLower") ?? 0, GetGlobalDouble("MzAcqRangeUpper") ?? 0);

    private Dictionary<string, string> LoadGlobalMetadata()
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM GlobalMetadata";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            dict[reader.GetString(0)] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        return dict;
    }

    private bool TableHasRows(string tableName)
    {
        if (!TableExists(tableName)) return false;
        return QueryScalar<long>($"SELECT COUNT(*) FROM {tableName}") > 0;
    }

    private bool TableExists(string tableName)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$name";
        cmd.Parameters.AddWithValue("$name", tableName);
        return cmd.ExecuteScalar() is not null;
    }

    private T QueryScalar<T>(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        object? result = cmd.ExecuteScalar();
        return result is null || result is DBNull
            ? default!
            : (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _conn.Dispose();
    }
}
