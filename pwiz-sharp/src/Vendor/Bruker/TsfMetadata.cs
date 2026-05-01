using System.Globalization;
using System.Data.SQLite;

namespace Pwiz.Vendor.Bruker;

/// <summary>ScanMode column values in the TSF <c>Frames</c> table.</summary>
public enum TsfScanMode
{
    /// <summary>Full scan (MS1).</summary>
    Ms1 = 0,
    /// <summary>Data-dependent MS2.</summary>
    AutoMsMs = 1,
    /// <summary>MRM / PRM-style triggered MS2.</summary>
    Mrm = 2,
    /// <summary>In-source CID.</summary>
    IsCid = 3,
    /// <summary>Broadband CID.</summary>
    BbCid = 4,
    /// <summary>PASEF DDA.</summary>
    PasefDda = 8,
    /// <summary>PASEF DIA.</summary>
    PasefDia = 9,
    /// <summary>PRM PASEF.</summary>
    PasefPrm = 10,
    /// <summary>MALDI acquisition.</summary>
    Maldi = 20,
}

/// <summary>One row from the <c>Frames</c> table in a .tsf SQLite file, with optional MS/MS/MALDI info.</summary>
public sealed record TsfFrame(
    long FrameId,
    double RetentionTimeSeconds,
    IonPolarity Polarity,
    TsfScanMode ScanMode,
    MsMsType MsMsType,
    double MaxIntensity,
    double SummedIntensities,
    long NumPeaks,
    long? ParentFrameId,
    double? PrecursorMz,
    double? IsolationWidth,
    int? PrecursorCharge,
    double? CollisionEnergy,
    int? MaldiChip,
    string? MaldiSpotName);

/// <summary>
/// Reads Bruker <c>.tsf</c> SQLite metadata: <c>GlobalMetadata</c> plus one <see cref="TsfFrame"/>
/// per row of the <c>Frames</c> table. Pairs with <see cref="TsfBinaryData"/> (binary frames via
/// the native DLL). TSF is the non-mobility timsTOF format — one spectrum per frame, no TIMS scans.
/// </summary>
/// <remarks>Port of the SQLite-reading half of pwiz::vendor_api::Bruker::TsfDataImpl.</remarks>
public sealed class TsfMetadata : IDisposable
{
    private readonly SQLiteConnection _conn;
    private readonly bool _hasMaldiFrameInfo;
    private bool _disposed;

    /// <summary>The <c>GlobalMetadata</c> key/value pairs (as strings).</summary>
    public IReadOnlyDictionary<string, string> GlobalMetadata { get; }

    /// <summary>True if the <c>MaldiFrameInfo</c> table exists and has rows.</summary>
    public bool HasMaldiData => _hasMaldiFrameInfo;

    /// <summary>Opens the <c>analysis.tsf</c> SQLite database inside <paramref name="dotDdirectory"/>.</summary>
    public TsfMetadata(string dotDdirectory)
    {
        ArgumentNullException.ThrowIfNull(dotDdirectory);
        string tsfPath = Path.Combine(dotDdirectory, "analysis.tsf");
        if (!File.Exists(tsfPath))
            throw new FileNotFoundException($"analysis.tsf not found in {dotDdirectory}", tsfPath);

        _conn = new SQLiteConnection($"Data Source={tsfPath};Read Only=True");
        _conn.Open();

        GlobalMetadata = LoadGlobalMetadata();
        _hasMaldiFrameInfo = TableHasRows("MaldiFrameInfo");
    }

    /// <summary>Total number of rows in the <c>Frames</c> table.</summary>
    public int FrameCount => (int)QueryScalar<long>("SELECT COUNT(*) FROM Frames");

    /// <summary>True if at least one frame has <c>MsMsType = 0</c>.</summary>
    public bool HasMs1Frames => QueryScalar<long>("SELECT COUNT(*) FROM Frames WHERE MsMsType = 0") > 0;

    /// <summary>True if at least one frame has <c>MsMsType &gt; 0</c>.</summary>
    public bool HasMsNFrames => QueryScalar<long>("SELECT COUNT(*) FROM Frames WHERE MsMsType > 0") > 0;

    /// <summary>
    /// Enumerates frames in id order. LEFT JOINs optional MS/MS and MALDI metadata.
    /// When <paramref name="preferOnlyMsLevel"/> is 1 or 2, only frames at that MS level are
    /// returned (1 → MsMsType=0, 2 → MsMsType&gt;0); 0 (default) returns all frames.
    /// </summary>
    public IEnumerable<TsfFrame> EnumerateFrames(int preferOnlyMsLevel = 0)
    {
        string maldiJoin = _hasMaldiFrameInfo ? "LEFT JOIN MaldiFrameInfo maldi ON f.Id=maldi.Frame " : "";
        string maldiColumns = _hasMaldiFrameInfo ? ", maldi.Chip, maldi.SpotName" : ", NULL, NULL";
        string msFilter = preferOnlyMsLevel switch
        {
            1 => "WHERE f.MsMsType = 0 ",
            2 => "WHERE f.MsMsType > 0 ",
            _ => "",
        };
        string sql =
            "SELECT f.Id, f.Time, f.Polarity, f.ScanMode, f.MsMsType, f.MaxIntensity, f.SummedIntensities, f.NumPeaks, " +
            "       info.Parent, info.TriggerMass, info.IsolationWidth, info.PrecursorCharge, info.CollisionEnergy" +
            maldiColumns + " " +
            "FROM Frames f " +
            "LEFT JOIN FrameMsMsInfo info ON f.Id = info.Frame " +
            maldiJoin +
            msFilter +
            "ORDER BY f.Id";
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return new TsfFrame(
                FrameId: reader.GetInt64(0),
                RetentionTimeSeconds: reader.GetDouble(1),
                Polarity: reader.GetString(2) == "+" ? IonPolarity.Positive : IonPolarity.Negative,
                ScanMode: (TsfScanMode)reader.GetInt32(3),
                MsMsType: (MsMsType)reader.GetInt32(4),
                MaxIntensity: reader.GetDouble(5),
                SummedIntensities: reader.GetDouble(6),
                NumPeaks: reader.GetInt64(7),
                ParentFrameId: reader.IsDBNull(8) ? null : reader.GetInt64(8),
                PrecursorMz: reader.IsDBNull(9) ? null : reader.GetDouble(9),
                IsolationWidth: reader.IsDBNull(10) ? null : reader.GetDouble(10),
                PrecursorCharge: reader.IsDBNull(11) ? null : reader.GetInt32(11),
                CollisionEnergy: reader.IsDBNull(12) ? null : reader.GetDouble(12),
                MaldiChip: reader.IsDBNull(13) ? null : reader.GetInt32(13),
                MaldiSpotName: reader.IsDBNull(14) ? null : reader.GetString(14));
        }
    }

    /// <summary>
    /// Returns <see cref="GlobalMetadata"/>[<paramref name="key"/>] as a double, or null if missing.
    /// </summary>
    public double? GetGlobalDouble(string key)
    {
        if (!GlobalMetadata.TryGetValue(key, out var v)) return null;
        return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : null;
    }

    /// <summary>Acquired mass range from <c>MzAcqRangeLower/Upper</c>.</summary>
    public (double Low, double High) MzAcquisitionRange =>
        (GetGlobalDouble("MzAcqRangeLower") ?? 0, GetGlobalDouble("MzAcqRangeUpper") ?? 0);

    /// <summary>True if <c>GlobalMetadata.HasLineSpectra</c> is 1.</summary>
    public bool HasLineSpectra =>
        GlobalMetadata.TryGetValue("HasLineSpectra", out var v) && v == "1";

    /// <summary>True if <c>GlobalMetadata.HasProfileSpectra</c> is 1.</summary>
    public bool HasProfileSpectra =>
        GlobalMetadata.TryGetValue("HasProfileSpectra", out var v) && v == "1";

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
