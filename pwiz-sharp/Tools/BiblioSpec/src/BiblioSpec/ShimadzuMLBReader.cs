// Port of pwiz_tools/BiblioSpec/src/ShimadzuMLBReader.{h,cpp}
//
// Parses Shimadzu LabSolutions small-molecule libraries (.mlb), which are SQLite databases
// with a single MSMSSP table holding both PSM metadata and the spectrum peak blobs. The
// reader doubles as the spec-file reader (cpp parity: `specReader_ = this`) — peaks are
// looked up out of the same SQLite handle by the spectrum's index id.

using System.Data.SQLite;
using System.Globalization;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Parses Shimadzu LabSolutions <c>.mlb</c> small-molecule libraries. cpp parity:
/// <c>BiblioSpec::ShimadzuMLBReader</c> at
/// <c>pwiz_tools/BiblioSpec/src/ShimadzuMLBReader.{h,cpp}</c>.
/// </summary>
/// <remarks>
/// <para>The .mlb file is a SQLite database, NOT a vendor-SDK format. The reader opens the
/// db once, iterates MSMSSP rows where MSStage==2, and builds one <see cref="PSM"/> per
/// distinct SP_ID. The spectrum peaks ship as a binary blob in the same row, so the reader
/// also serves as the <see cref="ISpecFileReader"/>: cpp sets <c>specReader_ = this</c> in
/// its ctor; the C# port does the same by installing an inner reader that reads back from
/// an in-memory <see cref="Dictionary{TKey,TValue}"/> populated during
/// <see cref="ParseFile"/>.</para>
/// <para>Each row's DataFilePath column groups PSMs by source data-file; cpp emits one
/// SpectrumSourceFile row per group via <see cref="BuildParser.SetSpecFileName(string,bool)"/>
/// followed by <see cref="BuildParser.BuildTables(PsmScoreType,string,bool,WorkflowType)"/>.</para>
/// </remarks>
public sealed class ShimadzuMLBReader : BuildParser
{
    private readonly string _mlbName;
    private SQLiteConnection? _mlbFile;
    private int _schemaVersion = -1;

    // cpp parity: ShimadzuMLBReader.h:47 — DataFilePath -> PSMs that came out of that file.
    private readonly Dictionary<string, List<PSM>> _fileMap = new(StringComparer.Ordinal);

    // cpp parity: ShimadzuMLBReader.h:49 — SP_ID -> in-memory SpecData (the peak list comes
    // out of the same SQLite row that produced the PSM, so we keep it cached here for the
    // later spectrum lookups from BuildParser.BuildTables).
    private readonly Dictionary<int, SpecData> _spectra = new();

    /// <summary>Returns true if <paramref name="path"/> ends with <c>.mlb</c>.</summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith(".mlb", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Construct a ShimadzuMLBReader bound to <paramref name="builder"/> and the file at
    /// <paramref name="mlbFile"/>. cpp parity: ShimadzuMLBReader.cpp:27.
    /// </summary>
    public ShimadzuMLBReader(BlibBuilder builder, string mlbFile, ProgressIndicator? parentProgress)
        : base(builder, mlbFile, parentProgress)
    {
        _mlbName = mlbFile;

        // cpp parity: ShimadzuMLBReader.cpp:33 — record the mlb path as the spec-file, no
        // existence check (we already know we opened it).
        SetSpecFileName(mlbFile, checkFile: false);

        // cpp parity: ShimadzuMLBReader.cpp:34 — spectra are addressed by their SP_ID, which
        // we treat as an index id.
        LookUpBy = SpecIdType.IndexId;

        // cpp parity: ShimadzuMLBReader.cpp:37 — point the spec reader at ourselves via an
        // inner adapter that reads back from _spectra.
        SpecReader = new MlbSpecFileReader(this);
    }

    /// <summary>Score types this reader produces. cpp parity: ShimadzuMLBReader.cpp:100.</summary>
    public override IList<PsmScoreType> GetScoreTypes() =>
        new[] { PsmScoreType.UnknownScoreType };

    /// <summary>
    /// Open the SQLite db, read its schema version, parse all MSMSSP MS2 rows into
    /// <see cref="_fileMap"/> + <see cref="_spectra"/>, then emit one library entry per
    /// DataFilePath group. cpp parity: ShimadzuMLBReader.cpp:57.
    /// </summary>
    public override bool ParseFile()
    {
        // cpp parity: ShimadzuMLBReader.cpp:59 — open the database; on failure cpp throws.
        // System.Data.SQLite throws on Open(); we wrap into a BlibException for parity.
        var connStr = new SQLiteConnectionStringBuilder
        {
            DataSource = _mlbName,
            ReadOnly = true,
        }.ToString();

        try
        {
            _mlbFile = new SQLiteConnection(connStr);
            _mlbFile.Open();
        }
        catch (Exception ex)
        {
            throw new BlibException(true, $"Couldn't open '{_mlbName}': {ex.Message}");
        }

        // cpp parity: ShimadzuMLBReader.cpp:64 — read the schema version from PROPERTY.
        using (var cmd = _mlbFile.CreateCommand())
        {
            cmd.CommandText = "SELECT DBVer FROM PROPERTY";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var version = reader.IsDBNull(0) ? string.Empty : reader.GetValue(0)?.ToString() ?? string.Empty;
                var pieces = version.Split('.');
                if (pieces.Length > 0 &&
                    int.TryParse(pieces[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var major))
                {
                    _schemaVersion = major;
                    Verbosity.Debug($"Schema version is {_schemaVersion} ({version})");
                }
                else
                {
                    Verbosity.Error($"Unknown schema version format: '{version}'");
                }
            }
        }

        if (_schemaVersion < 1)
        {
            Verbosity.Error("Could not determine schema version.");
        }

        ReadMsmsSp();

        // cpp parity: ShimadzuMLBReader.cpp:84 — flush each DataFilePath group to the library.
        foreach (var kvp in _fileMap)
        {
            if (kvp.Value.Count == 0) continue;

            Psms.Clear();
            foreach (var psm in kvp.Value)
                Psms.Add(psm);

            SetSpecFileName(kvp.Key, checkFile: false);
            BuildTables(PsmScoreType.UnknownScoreType, kvp.Key);
        }

        return true;
    }

    /// <summary>
    /// cpp parity: ShimadzuMLBReader.cpp:171 — iterate every MS2 row in MSMSSP, building
    /// one PSM + cached SpecData per SP_ID.
    /// </summary>
    private void ReadMsmsSp()
    {
        if (_mlbFile is null)
            throw new InvalidOperationException("MLB file not opened.");

        int specCount = GetRowCount(
            "(SELECT DISTINCT SP_ID FROM MSMSSP WHERE MSStage IS 2)");
        Verbosity.Status($"Parsing {specCount.ToString(CultureInfo.InvariantCulture)} spectra.");
        var progress = new ProgressIndicator(specCount);

        using var cmd = _mlbFile.CreateCommand();
        cmd.CommandText =
            "SELECT SP_ID, RT, PrecursorMZ, SpecPeak, AdductType, CompForm, CompName, IUPACNo, CASNo, DataFilePath " +
            "FROM MSMSSP " +
            "WHERE SP_ID IN (SELECT DISTINCT SP_ID FROM MSMSSP WHERE MSStage IS 2)";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var specData = new SpecData();
            int specId = ReadIntColumn(reader, 0);
            specData.Id = specId;

            // cpp parity: ShimadzuMLBReader.cpp:189 — RT is msec; library wants minutes.
            specData.RetentionTime = ReadDoubleColumn(reader, 1) / 60000.0;

            // cpp parity: ShimadzuMLBReader.cpp:190 — cpp uses sqlite3_column_int on the
            // PrecursorMZ column; preserve the truncating-to-int semantics.
            specData.Mz = ReadIntColumn(reader, 2);

            // cpp parity: ShimadzuMLBReader.cpp:191-206 — SpecPeak is a binary blob laid out
            // as a sequence of doubles: [peakCount*4, then for each peak (mz, intensity,
            // 2-double pad)]. peakCount = blob_bytes / (4 * sizeof(double)).
            var blob = (byte[])reader.GetValue(3);
            int numPeaks = blob.Length / (4 * sizeof(double));
            specData.NumPeaks = numPeaks;
            specData.Mzs = new double[numPeaks];
            specData.Intensities = new float[numPeaks];

            int offset = 0;
            // First double in the blob is the entry count (peaks*4); used as a sanity check.
            double headerCount = ReadDoubleFromBuffer(blob, ref offset);
            int confirmNPeaks = (int)(headerCount / 4);
            if (confirmNPeaks != numPeaks)
            {
                throw new BlibException(false, "Inconsistent peak count");
            }
            for (int n = 0; n < numPeaks; n++)
            {
                specData.Mzs[n] = ReadDoubleFromBuffer(blob, ref offset);
                // cpp parity: ShimadzuMLBReader.cpp:204 — intensity is the second double of
                // the per-peak quad, then 2 doubles of padding (skipped).
                specData.Intensities[n] = (float)ReadDoubleFromBuffer(blob, ref offset);
                offset += 2 * sizeof(double);
            }

            int adductType = ReadIntColumn(reader, 4);
            string precursorAdduct = GetAdduct(adductType, out int charge);

            var psm = new PSM
            {
                SpecKey = specId,
                SpecIndex = specId,
                Charge = charge,
            };
            psm.SmallMolMetadata.PrecursorAdduct = precursorAdduct;
            psm.SmallMolMetadata.ChemicalFormula = ReadStringColumn(reader, 5);
            psm.SmallMolMetadata.MoleculeName = ReadStringColumn(reader, 6);
            psm.SmallMolMetadata.InchiKey = ReadStringColumn(reader, 7);

            // cpp parity: ShimadzuMLBReader.cpp:218 — strip stray spaces in the CAS column.
            var cas = ReadStringColumn(reader, 8).Replace(" ", string.Empty);
            if (cas.Length > 0)
            {
                psm.SmallMolMetadata.OtherKeys = "cas:" + cas;
            }

            string dataFilePath = ReadStringColumn(reader, 9);
            if (string.IsNullOrEmpty(dataFilePath))
                dataFilePath = "unknown";

            if (!_fileMap.TryGetValue(dataFilePath, out var list))
            {
                list = new List<PSM>();
                _fileMap[dataFilePath] = list;
            }
            list.Add(psm);

            _spectra[specId] = specData;

            progress.Increment();
        }

        Verbosity.Debug($"Map has {_spectra.Count.ToString(CultureInfo.InvariantCulture)} spectra");
    }

    /// <summary>cpp parity: ShimadzuMLBReader.cpp:104 — read 8 bytes from <paramref name="buf"/>
    /// at <paramref name="offset"/> as a little-endian double. Advances <paramref name="offset"/>.</summary>
    private static double ReadDoubleFromBuffer(byte[] buf, ref int offset)
    {
        double v = BitConverter.ToDouble(buf, offset);
        offset += sizeof(double);
        return v;
    }

    /// <summary>cpp parity: ShimadzuMLBReader.cpp:114 — Shimadzu adduct-type bitmask
    /// translation, courtesy of Yutaro Yamamura via Brian Pratt.</summary>
    private static string GetAdduct(int adductType, out int charge)
    {
        switch (adductType)
        {
            case 0x1:   charge = 1; return "[M+H]";
            case 0x2:   charge = 1; return "[M+Na]";
            case 0x4:   charge = 1; return "[M+K]";
            case 0x8:   charge = 1; return "[M+NH4]";
            case 0x10:  charge = 1; return "[M+]";       // other(+)
            case 0x20:  charge = 1; return "[M-H]";
            case 0x40:  charge = 1; return "[M+HCOO]";
            case 0x80:  charge = 1; return "[M+CH3COO]";
            case 0x100: charge = 1; return "[M+Cl]";
            case 0x200: charge = 1; return "[M-]";       // other(-)
            default:
                charge = 0;
                throw new BlibException(false, "Unknown adduct type");
        }
    }

    /// <summary>Read a string column safely; returns empty string when DBNull.</summary>
    private static string ReadStringColumn(SQLiteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return string.Empty;
        var value = reader.GetValue(ordinal);
        return value?.ToString() ?? string.Empty;
    }

    /// <summary>Read an int column tolerantly — cpp's sqlite3_column_int coerces text and reals;
    /// System.Data.SQLite's <see cref="SQLiteDataReader.GetInt32"/> is type-strict and would throw
    /// on REAL columns like Shimadzu's PrecursorMZ.</summary>
    private static int ReadIntColumn(SQLiteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return 0;
        var value = reader.GetValue(ordinal);
        return value switch
        {
            long l => (int)l,
            int i => i,
            double d => (int)d,
            float f => (int)f,
            decimal m => (int)m,
            string s => int.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (int)double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture),
            _ => Convert.ToInt32(value, CultureInfo.InvariantCulture),
        };
    }

    /// <summary>Read a double column tolerantly — Shimadzu sometimes stores numeric columns
    /// as TEXT; <see cref="SQLiteDataReader.GetDouble"/> would throw on those.</summary>
    private static double ReadDoubleColumn(SQLiteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return 0;
        var value = reader.GetValue(ordinal);
        return value switch
        {
            double d => d,
            float f => f,
            long l => l,
            int i => i,
            decimal m => (double)m,
            string s => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture),
            _ => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        };
    }

    /// <summary>cpp parity: ShimadzuMLBReader.cpp:272 — SELECT COUNT(*) helper.</summary>
    private int GetRowCount(string table)
    {
        if (_mlbFile is null) return 0;
        using var cmd = _mlbFile.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM " + table;
        var result = cmd.ExecuteScalar();
        return result is null or DBNull ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _mlbFile?.Dispose();
            _mlbFile = null;
        }
        base.Dispose(disposing);
    }

    // --- Inner spec reader ---------------------------------------------------------------

    /// <summary>
    /// Inner ISpecFileReader that hands out cached SpecData from the parent reader. cpp
    /// parity: ShimadzuMLBReader.cpp:288-326 — getSpectrum(int, ...) hits the SP_ID -> SpecData
    /// map; the string-id and next-spectrum overloads warn-and-return-false.
    /// </summary>
    private sealed class MlbSpecFileReader : SpecFileReaderBase
    {
        private readonly ShimadzuMLBReader _owner;

        public MlbSpecFileReader(ShimadzuMLBReader owner)
        {
            _owner = owner;
        }

        public override void OpenFile(string path, bool mzSort = false)
        {
            // cpp parity: ShimadzuMLBReader.cpp:288 — no-op (spec and results files are the same).
        }

        public override SpecIdType IdType
        {
            set { /* no-op, cpp parity: ShimadzuMLBReader.cpp:290 */ }
        }

        public override bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true)
        {
            ArgumentNullException.ThrowIfNull(returnData);
            if (!_owner._spectra.TryGetValue(identifier, out var found))
                return false;
            // cpp parity: ShimadzuMLBReader.cpp:305 `returnData = *foundData` — deep-copy via
            // SpecData.CopyFrom (re-allocates the peak arrays).
            returnData.CopyFrom(found);
            return true;
        }

        public override bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true)
        {
            Verbosity.Warn(
                "ShimadzuMLBReader cannot fetch spectra by string identifier, only by spectrum index.");
            return false;
        }

        public override bool GetNextSpectrum(SpecData returnData, bool getPeaks = true)
        {
            Verbosity.Warn("ShimadzuMLBReader does not support sequential file reading.");
            return false;
        }
    }
}
