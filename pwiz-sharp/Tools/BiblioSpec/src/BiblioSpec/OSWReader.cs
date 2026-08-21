// Port of pwiz_tools/BiblioSpec/src/OSWReader.{h,cpp}
//
// Parses OpenSWATH `.osw` SQLite files. Each row of the FEATURE join is one product-ion
// peak; consecutive rows with the same FEATURE.ID are aggregated into a single PSM with
// a peak list. The reader doubles as the spectrum-file reader — spectra come from an
// in-memory map keyed by FEATURE.ID rather than an external spectrum file.
//
// Modifications in PEPTIDE.MODIFIED_SEQUENCE use the `(UNIMOD:N)` syntax; we resolve N
// against the pwiz Unimod table (DeltaMonoisotopicMass) instead of porting BiblioSpec's
// own UnimodParser. The cpp UnimodParser reads the same Unimod source — the per-mod delta
// masses agree to byte-for-byte parity on the seven significant digits the .check goldens
// preserve.

using System.Data.SQLite;
using System.Globalization;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Parses OpenSWATH <c>.osw</c> SQLite result files into BlibBuilder PSMs. cpp parity:
/// <c>BiblioSpec::OSWReader</c> at <c>pwiz_tools/BiblioSpec/src/OSWReader.{h,cpp}</c>.
/// </summary>
/// <remarks>
/// <para>The .osw is a SQLite database; the reader opens it, runs the cpp's joined SELECT,
/// and groups rows by <c>FEATURE.ID</c> into PSMs. Each FEATURE carries N product-ion peaks
/// inline (one row per peak), so the reader also serves as the <see cref="ISpecFileReader"/>
/// for the run: the embedded spec-reader hands back cached <see cref="SpecData"/> keyed by
/// the FEATURE.ID string.</para>
/// <para>PSMs are grouped by <c>RUN.FILENAME</c> and flushed one file at a time via
/// <see cref="BuildParser.BuildTables(PsmScoreType,string,bool,WorkflowType)"/>, matching
/// the cpp's per-RUN.FILENAME flush loop.</para>
/// </remarks>
public sealed class OSWReader : BuildParser
{
    private readonly string _oswName;
    private readonly double _scoreThreshold;
    private SQLiteConnection? _osw;

    // cpp parity: OSWReader.h:47 — std::map<string, vector<PSM*>>. SortedDictionary mirrors
    // cpp std::map's ascending-key iteration order so the per-file flush order is stable.
    private readonly SortedDictionary<string, List<PSM>> _fileMap = new(StringComparer.Ordinal);

    // cpp parity: OSWReader.h:48 — std::map<string, SpecData> keyed by FEATURE.ID string.
    private readonly Dictionary<string, SpecData> _spectra = new(StringComparer.Ordinal);

    /// <summary>Returns true if <paramref name="path"/> ends with <c>.osw</c> (case-insensitive).</summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith(".osw", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Construct an OSWReader bound to <paramref name="builder"/> and the file at
    /// <paramref name="filename"/>. cpp parity: OSWReader.cpp:27.
    /// </summary>
    public OSWReader(BlibBuilder builder, string filename, ProgressIndicator? parentProgress)
        : base(builder, filename, parentProgress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        _oswName = filename;
        _scoreThreshold = GetScoreThreshold(BuildInput.GenericQValueInput);

        // cpp parity: OSWReader.cpp:32 — set spec-file name to the osw itself; the embedded
        // spec reader returns peaks straight from _spectra.
        SetSpecFileName(filename, checkFile: false);
        SpecReader = new OswSpecFileReader(this);

        // cpp parity: OSWReader.cpp:35 — spectra are addressed by FEATURE.ID string.
        LookUpBy = SpecIdType.NameId;
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: OSWReader.cpp:164.</remarks>
    public override IList<PsmScoreType> GetScoreTypes() =>
        new[] { PsmScoreType.GenericQValue };

    /// <summary>
    /// Open the .osw, run the joined SELECT, build PSMs + cached spectra, and emit one
    /// library entry per <c>RUN.FILENAME</c>. cpp parity: OSWReader.cpp:47.
    /// </summary>
    public override bool ParseFile()
    {
        Verbosity.Debug($"Opening {_oswName}");

        var connStr = new SQLiteConnectionStringBuilder
        {
            DataSource = _oswName,
            ReadOnly = true,
        }.ToString();

        try
        {
            _osw = new SQLiteConnection(connStr);
            _osw.Open();
        }
        catch (Exception ex)
        {
            throw new BlibException(true, $"Error opening {_oswName}: {ex.Message}");
        }

        // cpp parity: OSWReader.cpp:60-86 — the joined SELECT verbatim. The score-threshold
        // is interpolated directly because sqlite_prepare_v2 in cpp does the same (no bind).
        var query =
            "SELECT FEATURE.ID, "
          + "       RUN.FILENAME, "
          + "       FEATURE.EXP_RT, "
          + "       PEPTIDE.MODIFIED_SEQUENCE, "
          + "       PRECURSOR.CHARGE, "
          + "       PRECURSOR.PRECURSOR_MZ, "
          + "       PROTEIN.PROTEIN_ACCESSION, "
          + "       FEATURE.LEFT_WIDTH, "
          + "       FEATURE.RIGHT_WIDTH, "
          + "       TRANSITION.PRODUCT_MZ, "
          + "       FEATURE_TRANSITION.AREA_INTENSITY, "
          + "       SCORE_MS2.QVALUE "
          + "FROM FEATURE "
          + "JOIN PRECURSOR ON FEATURE.PRECURSOR_ID = PRECURSOR.ID "
          + "JOIN PRECURSOR_PEPTIDE_MAPPING ON PRECURSOR.ID = PRECURSOR_PEPTIDE_MAPPING.PRECURSOR_ID "
          + "JOIN PEPTIDE ON PRECURSOR_PEPTIDE_MAPPING.PEPTIDE_ID = PEPTIDE.ID "
          + "JOIN RUN ON FEATURE.RUN_ID = RUN.ID "
          + "JOIN SCORE_MS2 ON FEATURE.ID = SCORE_MS2.FEATURE_ID "
          + "JOIN FEATURE_TRANSITION ON FEATURE.ID = FEATURE_TRANSITION.FEATURE_ID "
          + "JOIN TRANSITION ON FEATURE_TRANSITION.TRANSITION_ID = TRANSITION.ID "
          + "LEFT JOIN PEPTIDE_PROTEIN_MAPPING ON PRECURSOR_PEPTIDE_MAPPING.PEPTIDE_ID = PEPTIDE_PROTEIN_MAPPING.PEPTIDE_ID "
          + "LEFT JOIN PROTEIN ON PEPTIDE_PROTEIN_MAPPING.PROTEIN_ID = PROTEIN.ID "
          + "WHERE PRECURSOR.DECOY = 0 "
          + "  AND SCORE_MS2.RANK = 1 "
          + "  AND SCORE_MS2.QVALUE <= " + _scoreThreshold.ToString("R", CultureInfo.InvariantCulture) + " "
          + "ORDER BY FEATURE.ID ASC";

        using (var cmd = _osw.CreateCommand())
        {
            cmd.CommandText = query;
            using var reader = cmd.ExecuteReader();

            // cpp parity: OSWReader.cpp:93 — proteins map, running feature id, peak accumulators.
            var proteins = new Dictionary<string, Protein>(StringComparer.Ordinal);
            string lastFeatureId = string.Empty;
            SpecData? curSpectrum = null;
            var curPeakMzs = new List<double>();
            var curPeakIntensities = new List<float>();

            while (reader.Read())
            {
                // cpp parity: OSWReader.cpp:99 — `sqlite3_column_text` on FEATURE.ID returns the
                // textual int64 value. System.Data.SQLite's GetValue can hand back Int32 for some
                // declared types; explicitly read as Int64 to preserve the full hash (the OSW
                // FEATURE.ID column holds hash-derived int64 keys, e.g. -7143345622628842681).
                var featureId = reader.GetInt64(0).ToString(CultureInfo.InvariantCulture);
                if (!string.Equals(featureId, lastFeatureId, StringComparison.Ordinal))
                {
                    lastFeatureId = featureId;

                    var curPsm = new PSM
                    {
                        Charge = reader.GetInt32(4),
                    };

                    var modSeq = Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture)
                                 ?? string.Empty;
                    if (!ParseSequence(modSeq, out var unmod, out var mods))
                    {
                        throw new BlibException(false,
                            $"Failed to parse modified sequence '{modSeq}' in {_oswName}");
                    }
                    curPsm.UnmodSeq = unmod;
                    foreach (var m in mods) curPsm.Mods.Add(m);

                    curPsm.Score = reader.GetDouble(11);
                    curPsm.SpecName = featureId;

                    if (!reader.IsDBNull(6))
                    {
                        var proteinString = Convert.ToString(reader.GetValue(6), CultureInfo.InvariantCulture)
                                            ?? string.Empty;
                        // cpp parity: OSWReader.cpp:115 — split on ';'.
                        foreach (var name in proteinString.Split(';'))
                        {
                            if (!proteins.TryGetValue(name, out var prot))
                            {
                                prot = new Protein(name);
                                proteins[name] = prot;
                            }
                            curPsm.Proteins.Add(prot);
                        }
                    }

                    var filename = Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture)
                                   ?? string.Empty;
                    if (!_fileMap.TryGetValue(filename, out var list))
                    {
                        list = new List<PSM>();
                        _fileMap[filename] = list;
                    }
                    list.Add(curPsm);

                    // cpp parity: OSWReader.cpp:135 — transfer peaks for the previous spectrum.
                    TransferPeaks(curSpectrum, curPeakMzs, curPeakIntensities);

                    curSpectrum = new SpecData
                    {
                        // cpp parity: OSWReader.cpp:138 — RT / start / end are seconds in the OSW;
                        // the library wants minutes.
                        RetentionTime = reader.GetDouble(2) / 60.0,
                        StartTime = reader.GetDouble(7) / 60.0,
                        EndTime = reader.GetDouble(8) / 60.0,
                        Mz = reader.GetDouble(5),
                    };
                    _spectra[featureId] = curSpectrum;
                }

                curPeakMzs.Add(reader.GetDouble(9));
                curPeakIntensities.Add((float)reader.GetDouble(10));
            }

            // cpp parity: OSWReader.cpp:146 — transfer peaks for the last spectrum.
            TransferPeaks(curSpectrum, curPeakMzs, curPeakIntensities);
        }

        Verbosity.Debug("Building tables");
        InitSpecFileProgress(_fileMap.Count);
        foreach (var kvp in _fileMap)
        {
            Psms.Clear();
            foreach (var p in kvp.Value)
                Psms.Add(p);

            // cpp parity: OSWReader.cpp:157 — record the RUN.FILENAME as the spec source,
            // no existence check (spectra are embedded).
            SetSpecFileName(kvp.Key, checkFile: false);
            BuildTables(PsmScoreType.GenericQValue, kvp.Key, showSpecProgress: false, WorkflowType.Dia);
        }

        return true;
    }

    /// <summary>
    /// cpp parity: OSWReader.cpp:168 — move per-row peak accumulators into the SpecData and
    /// clear them for the next group.
    /// </summary>
    private static void TransferPeaks(SpecData? dst, List<double> mzs, List<float> intensities)
    {
        if (dst is null) return;
        if (mzs.Count != intensities.Count)
        {
            throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                "Number of m/zs {0} did not match number of intensities {1}",
                mzs.Count, intensities.Count));
        }

        dst.NumPeaks = mzs.Count;
        dst.Mzs = mzs.ToArray();
        dst.Intensities = intensities.ToArray();
        mzs.Clear();
        intensities.Clear();
    }

    /// <summary>
    /// Strip <c>(UNIMOD:N)</c> tokens from <paramref name="seq"/>, returning the bare amino
    /// acid sequence and one <see cref="SeqMod"/> per token (1-based residue position +
    /// monoisotopic delta mass).
    /// </summary>
    /// <remarks>
    /// cpp parity: TSVReader.cpp:1142 <c>parseSequence</c>. Shared between OSWReader and
    /// TSVReader in cpp (TSVReader is the canonical owner); we colocate a copy here to keep
    /// the OSWReader self-contained, and expose it from TSVReader for the OSW path which
    /// the cpp code paths share.
    /// </remarks>
    internal static bool ParseSequence(string seq, out string unmodSeq, out List<SeqMod> mods)
    {
        return TSVReader.ParseSequence(seq, out unmodSeq, out mods);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _osw?.Dispose();
            _osw = null;
        }
        base.Dispose(disposing);
    }

    // --- Inner spec reader ---------------------------------------------------------------

    /// <summary>
    /// Inner <see cref="ISpecFileReader"/> that returns cached SpecData by FEATURE.ID string.
    /// cpp parity: OSWReader.cpp:187 — <c>getSpectrum(string, ...)</c> only.
    /// </summary>
    private sealed class OswSpecFileReader : SpecFileReaderBase
    {
        private readonly OSWReader _owner;

        public OswSpecFileReader(OSWReader owner)
        {
            _owner = owner;
        }

        public override void OpenFile(string path, bool mzSort = false) { /* no-op */ }
        public override SpecIdType IdType { set { /* no-op */ } }

        public override bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true)
            => false;

        public override bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true)
        {
            ArgumentNullException.ThrowIfNull(returnData);
            if (!_owner._spectra.TryGetValue(identifier, out var found))
                return false;

            // cpp parity: OSWReader.cpp:194 — copy fields + (optionally) peaks. Note cpp
            // copies peaks unconditionally; we mirror with getPeaks for consistency with
            // the other readers in pwiz-sharp.
            returnData.RetentionTime = found.RetentionTime;
            returnData.StartTime = found.StartTime;
            returnData.EndTime = found.EndTime;
            returnData.Mz = found.Mz;
            returnData.NumPeaks = found.NumPeaks;

            if (getPeaks && found.Mzs is not null && found.Intensities is not null)
            {
                returnData.Mzs = new double[found.NumPeaks];
                returnData.Intensities = new float[found.NumPeaks];
                Array.Copy(found.Mzs, returnData.Mzs, found.NumPeaks);
                Array.Copy(found.Intensities, returnData.Intensities, found.NumPeaks);
            }
            else
            {
                returnData.Mzs = null;
                returnData.Intensities = null;
            }
            return true;
        }

        public override bool GetNextSpectrum(SpecData returnData, bool getPeaks = true) => false;
    }
}
