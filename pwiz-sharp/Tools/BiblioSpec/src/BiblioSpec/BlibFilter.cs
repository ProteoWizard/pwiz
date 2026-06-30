// Port of pwiz_tools/BiblioSpec/src/BlibFilter.cpp
//
// Faithful C# port of BiblioSpec::BlibFilter. Reads a redundant .blib library and produces
// a non-redundant .blib: for each peptide+charge group, picks the "best" representative
// spectrum and writes only the survivors to the output library.
//
// "Best" is either:
//   * the spectrum with the highest average normalised-peak dot-product against the other
//     members of its peptide+charge group (default), or
//   * (under -b / useBestScoring) the spectrum with the best search-engine score for its
//     ScoreType, with TIC as a tiebreaker.
//
// Numerical behaviour (peak processing, dot product, scoring) is preserved byte-for-byte
// with the cpp original — comments tag the key parity points with cpp file:line refs.

using System.Data.SQLite;
using System.Globalization;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Filters a redundant <c>.blib</c> library down to one representative spectrum per
/// peptide+charge group, producing a non-redundant <c>.blib</c>.
/// Port of <c>BiblioSpec::BlibFilter</c> at <c>pwiz_tools/BiblioSpec/src/BlibFilter.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// Extends <see cref="BlibMaker"/> directly — BlibFilter does NOT go through the
/// reader-based ingestion pipeline of <see cref="BlibBuilder"/>. The redundant library is
/// ATTACH'd as an auxiliary schema; <see cref="BlibMaker.CreateUpdatedRefSpectraView"/> normalises
/// any older schema columns; then we iterate sorted by peptideModSeq+charge and choose one
/// representative per group.
/// </para>
/// <para>
/// CLI parity: cpp BlibFilter uses boost::program_options. The C# port hooks into
/// <see cref="BlibMaker.ParseCommandArgs"/> / <see cref="BlibMaker.ParseNextSwitch"/> so that
/// it can support boost-style positional args: <c>BlibFilter [options] redundant.blib
/// filtered.blib</c>.
/// </para>
/// </remarks>
public class BlibFilter : BlibMaker
{
    // cpp BlibFilter.cpp:124-134 — defaults from the cpp constructor.
    private const string DefaultRedundantDbName = "redundant";

    // cpp parity: BlibFilter.cpp:126 — minPeaks_ default is 20 (the constructor sets 20, but
    // the boost::program_options default at cpp:163 is 1; the program_options default wins).
    private const int DefaultMinPeaks = 1;

    // cpp parity: BlibFilter.cpp:126 — minAverageScore_ default 0.
    private const double DefaultMinAverageScore = 0.0;

    // cpp BlibFilter.cpp:727-729 — peak processor defaults used inside compAndInsert.
    private const int PeakProcessorNumTopPeaks = 100;

    private readonly string _redundantDbName = DefaultRedundantDbName;
    private string _redundantFileName = string.Empty;
    private int _minPeaks = DefaultMinPeaks;
    private double _minAverageScore = DefaultMinAverageScore;
    private bool _useBestScoring;

    private int _tableVersion;
    private readonly Dictionary<int, bool> _higherIsBetter = new();
    private SQLiteCommand? _insertStmt;

    /// <summary>
    /// Constructs a BlibFilter with cpp-default options. cpp BlibFilter.cpp:124-134:
    /// <c>setOverwrite(true)</c> (never append to a non-redundant library) and
    /// <c>setRedundant(false)</c> (the output is non-redundant by definition).
    /// </summary>
    public BlibFilter()
    {
        // cpp parity: BlibFilter.cpp:132-133 — never append to a non-redundant library,
        // and the output library is non-redundant.
        Overwrite = true;
        Redundant = false;
    }

    /// <summary>Path to the redundant input library. Set by <see cref="ParseCommandArgs"/>.</summary>
    public string RedundantFileName
    {
        get => _redundantFileName;
        set => _redundantFileName = value ?? string.Empty;
    }

    /// <summary>
    /// Minimum number of peaks a spectrum must have to be considered. cpp <c>-n</c> /
    /// <c>--min-peaks</c>. Default 1 (matches the boost::program_options default at
    /// BlibFilter.cpp:163).
    /// </summary>
    public int MinPeaks
    {
        get => _minPeaks;
        set => _minPeaks = value;
    }

    /// <summary>
    /// Minimum average dot-product score required to keep a peptide+charge group's best
    /// representative. cpp <c>-s</c> / <c>--min-score</c>. Default 0.
    /// </summary>
    public double MinAverageScore
    {
        get => _minAverageScore;
        set => _minAverageScore = value;
    }

    /// <summary>
    /// When true, pick the spectrum with the best search-engine score (TIC as tiebreaker)
    /// instead of the spectrum with the highest average dot product. cpp <c>-b</c> /
    /// <c>--best-scoring</c>. Default false.
    /// </summary>
    public bool UseBestScoring
    {
        get => _useBestScoring;
        set => _useBestScoring = value;
    }

    // --- BlibMaker overrides --------------------------------------------------------------

    /// <summary>
    /// cpp <c>BlibFilter::attachAll</c> at BlibFilter.cpp:214-223. ATTACH'es the redundant
    /// library as <c>redundant</c> and creates the <c>RefSpectraTransfer</c> temp view.
    /// </summary>
    protected override void AttachAll()
    {
        if (string.IsNullOrEmpty(_redundantFileName))
            throw new BlibException(false, "Redundant library name has not been set; call ParseCommandArgs or RedundantFileName first.");

        Verbosity.Status($"Filtering redundant library '{_redundantFileName}'.");
        var sql = string.Format(
            CultureInfo.InvariantCulture,
            "ATTACH DATABASE '{0}' as {1}",
            SqliteRoutine.EscapeApostrophes(_redundantFileName),
            _redundantDbName);
        SqlStmt(sql);

        CreateUpdatedRefSpectraView(_redundantDbName);
    }

    /// <summary>
    /// cpp <c>BlibFilter::commit</c> at BlibFilter.cpp:225-232. Calls the base implementation
    /// (which writes indexes) and then DETACH'es the redundant schema.
    /// </summary>
    public override void Commit()
    {
        base.Commit();
        SqlStmt("DETACH DATABASE " + _redundantDbName);
    }

    /// <summary>
    /// cpp <c>BlibFilter::getLSID</c> at BlibFilter.cpp:234-260. Reads the redundant
    /// library's LSID and replaces <c>:redundant:</c> with <c>:nr:</c>.
    /// </summary>
    protected override string GetLsid()
    {
        var sql = $"SELECT libLSID FROM {_redundantDbName}.LibInfo";
        using var cmd = Db.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        if (result is null || result is DBNull)
            throw new BlibException(false, $"Failed to read libLSID from redundant library '{_redundantFileName}'.");

        var libLsid = Convert.ToString(result, CultureInfo.InvariantCulture) ?? string.Empty;
        const string redundantMarker = ":redundant:";
        var idx = libLsid.IndexOf(redundantMarker, StringComparison.Ordinal);
        if (idx < 0)
        {
            throw new BlibException(false, string.Format(
                CultureInfo.InvariantCulture,
                "The library {0} does not appear to be a redundant library.\n'{1}' was not found in LSID '{2}'.",
                _redundantFileName, redundantMarker, libLsid));
        }

        return string.Concat(
            libLsid.AsSpan(0, idx),
            ":nr:".AsSpan(),
            libLsid.AsSpan(idx + redundantMarker.Length));
    }

    /// <summary>
    /// cpp <c>BlibFilter::getNextRevision</c> at BlibFilter.cpp:262-278. Reads majorVersion
    /// from the redundant library directly (no +1 bump, unlike <see cref="BlibMaker.GetNextRevision"/>).
    /// </summary>
    protected override void GetNextRevision(out int dataRev)
    {
        var sql = $"SELECT majorVersion, minorVersion FROM {_redundantDbName}.LibInfo";
        using var cmd = Db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            throw new BlibException(false, $"Failed to read version info from redundant library '{_redundantFileName}'.");
        dataRev = reader.GetInt32(0);
    }

    /// <summary>
    /// cpp <c>BlibFilter::init</c> at BlibFilter.cpp:286-312. Calls base init (which opens
    /// the file and creates standard tables via <see cref="AttachAll"/>), then creates the
    /// RetentionTimes table and prepares the insert statement used by
    /// <see cref="CompAndInsert"/>.
    /// </summary>
    public override void Init()
    {
        base.Init();

        SqlStmt(
            "CREATE TABLE RetentionTimes (RefSpectraID INTEGER, " +
            "RedundantRefSpectraID INTEGER, " +
            "SpectrumSourceID INTEGER, " +
            "ionMobility REAL, " +
            "collisionalCrossSectionSqA REAL, " +
            "ionMobilityHighEnergyOffset REAL, " +
            "ionMobilityType TINYINT, " +
            "retentionTime REAL, " +
            "startTime REAL, " +
            "endTime REAL, " +
            "score REAL, " +
            "bestSpectrum INTEGER, " + // boolean
            "FOREIGN KEY(RefSpectraID) REFERENCES RefSpectra(id) )");

        _insertStmt = Db.CreateCommand();
        _insertStmt.CommandText =
            "INSERT INTO RetentionTimes (RefSpectraID, RedundantRefSpectraID, " +
            "SpectrumSourceID, ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, " +
            "retentionTime, startTime, endTime, score, bestSpectrum) " +
            "VALUES (@refId, @redundantRefId, @sourceId, @ionMobility, @ccs, @ionMobilityHEO, @ionMobilityType, " +
            "@retentionTime, @startTime, @endTime, @score, @bestSpectrum)";
        _insertStmt.Parameters.Add("@refId", System.Data.DbType.Int32);
        _insertStmt.Parameters.Add("@redundantRefId", System.Data.DbType.Int32);
        _insertStmt.Parameters.Add("@sourceId", System.Data.DbType.Int32);
        _insertStmt.Parameters.Add("@ionMobility", System.Data.DbType.Double);
        _insertStmt.Parameters.Add("@ccs", System.Data.DbType.Double);
        _insertStmt.Parameters.Add("@ionMobilityHEO", System.Data.DbType.Double);
        _insertStmt.Parameters.Add("@ionMobilityType", System.Data.DbType.Int32);
        _insertStmt.Parameters.Add("@retentionTime", System.Data.DbType.Double);
        _insertStmt.Parameters.Add("@startTime", System.Data.DbType.Double);
        _insertStmt.Parameters.Add("@endTime", System.Data.DbType.Double);
        _insertStmt.Parameters.Add("@score", System.Data.DbType.Double);
        _insertStmt.Parameters.Add("@bestSpectrum", System.Data.DbType.Int32);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _insertStmt?.Dispose();
            _insertStmt = null;
        }
        base.Dispose(disposing);
    }

    // --- main algorithm --------------------------------------------------------------------

    /// <summary>
    /// cpp <c>BlibFilter::buildNonRedundantLib</c> at BlibFilter.cpp:314-643. For each
    /// (peptideModSeq, charge) or (small-molecule, charge) group, select one representative
    /// spectrum and transfer it to the output library along with all members' retention
    /// times.
    /// </summary>
    public void BuildNonRedundantLib()
    {
        Verbosity.Debug("Starting buildNonRedundant.");

        // cpp parity: BlibFilter.cpp:317-370 — if useBestScoring, classify each ScoreType in
        // the redundant library as "higher-is-better" or "lower-is-better". If we encounter
        // an unknown ScoreType that actually appears in RefSpectra, revert to dot-product
        // mode.
        if (_useBestScoring)
        {
            ClassifyScoreTypes();
        }

        Message = "ERROR: Failed building library " + (LibName ?? string.Empty);

        // cpp parity: BlibFilter.cpp:377-379 — copy SpectrumSourceFiles and Proteins first.
        TransferSpectrumFiles(_redundantDbName);
        TransferProteins(_redundantDbName);

        // cpp parity: BlibFilter.cpp:382-426 — probe the redundant schema for which optional
        // columns exist. This drives both `optional_cols` (used to build the SELECT) and the
        // `tableVersion_` flag (used to switch on behaviour in TransferSpectra).
        _tableVersion = 0;
        var optionalCols = string.Empty;
        var orderBy = string.Empty;

        if (TableColumnExists(_redundantDbName, "RefSpectra", "retentionTime"))
        {
            ++_tableVersion;
            optionalCols = ", SpecIDinFile, retentionTime";
            if (TableColumnExists(_redundantDbName, "RefSpectra", "collisionalCrossSectionSqA"))
            {
                if (TableColumnExists(_redundantDbName, "RefSpectra", "ionMobilityHighEnergyOffset"))
                {
                    _tableVersion = BlibSchema.MinVersionImsUnits;
                    optionalCols += ", ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset";
                    if (TableColumnExists(_redundantDbName, "RefSpectra", "startTime"))
                    {
                        _tableVersion = BlibSchema.MinVersionRtBounds;
                        optionalCols += ", startTime, endTime";
                        if (TableExists(_redundantDbName, "Proteins"))
                        {
                            _tableVersion = BlibSchema.MinVersionProteins;
                            if (TableColumnExists(_redundantDbName, "RefSpectra", "totalIonCurrent"))
                            {
                                _tableVersion = BlibSchema.MinVersionTic;
                                optionalCols += ", totalIonCurrent";
                            }
                        }
                    }
                    else if (TableExists(_redundantDbName, "RefSpectraPeakAnnotations"))
                    {
                        _tableVersion = BlibSchema.MinVersionPeakAnnot;
                    }
                }
                else
                {
                    _tableVersion = BlibSchema.MinVersionCcs;
                    optionalCols += ", driftTimeMsec, collisionalCrossSectionSqA, driftTimeHighEnergyOffsetMsec";
                }
                if (TableColumnExists(_redundantDbName, "RefSpectra", "inchiKey"))
                {
                    // May contain small molecules.
                    orderBy = "peptideModSeq, moleculeName, chemicalFormula, inchiKey, otherKeys, precursorCharge, precursorAdduct" + optionalCols;
                    optionalCols += SmallMolMetadata.SqlColumnNamesCsv();
                    if (_tableVersion >= BlibSchema.MinVersionImsUnits)
                        optionalCols += ", ionMobilityType";
                    else
                        _tableVersion = BlibSchema.MinVersionSmallMol;
                }
            }
            else if (TableColumnExists(_redundantDbName, "RefSpectra", "ionMobilityValue"))
            {
                ++_tableVersion;
                optionalCols += ", ionMobilityValue, ionMobilityType";
                if (TableColumnExists(_redundantDbName, "RefSpectra", "ionMobilityHighEnergyDriftTimeOffsetMsec"))
                {
                    ++_tableVersion;
                    optionalCols += ", ionMobilityHighEnergyDriftTimeOffsetMsec";
                }
            }
        }

        Verbosity.Debug("Counting Spectra.");
        var progress = new ProgressIndicator(GetSpectrumCount(_redundantDbName));

        Verbosity.Debug("Sorting spectra by sequence and charge.");
        if (string.IsNullOrEmpty(orderBy))
        {
            // cpp parity: BlibFilter.cpp:441 — default order_by.
            orderBy = "peptideModSeq, precursorCharge " + optionalCols;
        }

        // cpp parity: BlibFilter.cpp:444-453 — SELECT from the RefSpectraTransfer view with
        // numPeaks filter. Precursor-only entries (empty SpecIDinFile) bypass the filter.
        var selectSql = string.Format(
            CultureInfo.InvariantCulture,
            "SELECT id,peptideSeq,precursorMZ,precursorCharge,peptideModSeq," +
            "prevAA, nextAA, numPeaks, score, scoreType, " +
            "ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, " +
            "moleculeName, chemicalFormula, inchiKey, otherKeys, precursorAdduct, " +
            "startTime, endTime, totalIonCurrent, retentionTime, specIDinFile " +
            "FROM RefSpectraTransfer " +
            "WHERE numPeaks >= {0} OR SpecIDinFile=\"\"" +
            "ORDER BY {1}",
            _minPeaks, orderBy);

        // cpp parity: BlibFilter.cpp:490-503 — open a second connection just for peak blobs.
        // We don't need a second connection in C# (the main connection can handle parallel
        // commands), but we keep the comment for parity reference.
        var bestSpectraIdAndCount = new List<(int Id, int Copies)>();

        var oneIon = new List<RefSpectrum>();
        var lastPepModSeq = string.Empty;
        var lastCharge = 0;
        var lastMoleculeAndAdduct = new SmallMolMetadata();

        using (var cmd = Db.CreateCommand())
        {
            cmd.CommandText = selectSql;
            using var reader = cmd.ExecuteReader();
            Verbosity.Debug("Successfully sorted.");

            // cpp parity: BlibFilter.cpp:464-487 — column-name → ordinal dictionary for
            // backward compatibility with older schemas.
            var columns = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
                columns[reader.GetName(i)] = i;

            var molNameIndex = columns.GetValueOrDefault("moleculeName", -1);
            var formulaIndex = columns.GetValueOrDefault("chemicalFormula", -1);
            var adductIndex = columns.GetValueOrDefault("precursorAdduct", -1);
            var inchiKeyIndex = columns.GetValueOrDefault("inchiKey", -1);
            var otherKeysIndex = columns.GetValueOrDefault("otherKeys", -1);
            var ccsIndex = columns.GetValueOrDefault("collisionalCrossSectionSqA", -1);
            var ionMobilityTypeIndex = columns.GetValueOrDefault("ionMobilityType", -1);
            var ionMobilityValueIndex = columns.GetValueOrDefault("ionMobilityValue", -1); // V3 and earlier
            var ionMobilityIndex = columns.GetValueOrDefault("ionMobility", -1);
            var highEnergyOffsetIndex = columns.GetValueOrDefault("ionMobilityHighEnergyOffset", -1);
            var scoreIndex = columns.GetValueOrDefault("score", -1);
            var scoreTypeIndex = columns.GetValueOrDefault("scoreType", -1);
            var scanNumberIndex = columns.GetValueOrDefault("SpecIDinFile", -1);
            var retentionTimeIndex = columns.GetValueOrDefault("retentionTime", -1);
            var startTimeIndex = columns.GetValueOrDefault("startTime", -1);
            var endTimeIndex = columns.GetValueOrDefault("endTime", -1);
            var ticIndex = columns.GetValueOrDefault("totalIonCurrent", -1);
            var numPeaksIndex = columns.GetValueOrDefault("numPeaks", -1);

            // for each spectrum entry in table
            while (reader.Read())
            {
                progress.Increment();

                var tmpRef = new RefSpectrum();

                var pepModSeq = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                var charge = reader.GetInt32(3);

                tmpRef.LibSpecId = reader.GetInt32(0);
                tmpRef.Sequence = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                tmpRef.Mz = reader.GetDouble(2);
                tmpRef.Charge = charge;

                // cpp parity: BlibFilter.cpp:526-547 — ion mobility / CCS hydration depends
                // on which columns the source library has.
                if (ionMobilityValueIndex >= 0)
                {
                    // Records drift time or ccs but not both.
                    var ionMobilityValue = ionMobilityValueIndex >= 0 && !reader.IsDBNull(ionMobilityValueIndex) ? reader.GetDouble(ionMobilityValueIndex) : 0;
                    var imType = ionMobilityTypeIndex >= 0 ? SafeGetIntCoerce(reader, ionMobilityTypeIndex) : 0;
                    tmpRef.SetIonMobility(
                        imType == 1 ? ionMobilityValue : 0,
                        imType == 1 ? IonMobilityType.DriftTimeMsec : IonMobilityType.None);
                    tmpRef.CollisionalCrossSection = ccsIndex >= 0 && !reader.IsDBNull(ccsIndex) ? reader.GetDouble(ccsIndex) : 0;
                }
                else if (ionMobilityIndex >= 0)
                {
                    var ionMobilityValue = reader.IsDBNull(ionMobilityIndex) ? 0 : reader.GetDouble(ionMobilityIndex);
                    var imType = ionMobilityTypeIndex >= 0
                        ? (IonMobilityType)SafeGetIntCoerce(reader, ionMobilityTypeIndex)
                        : IonMobilityType.DriftTimeMsec;
                    tmpRef.SetIonMobility(ionMobilityValue, imType);
                    tmpRef.CollisionalCrossSection = ccsIndex >= 0 && !reader.IsDBNull(ccsIndex) ? reader.GetDouble(ccsIndex) : 0;
                    if (molNameIndex >= 0)
                    {
                        // moleculeName, chemicalFormula, precursorAdduct, inchiKey, otherKeys
                        tmpRef.MoleculeName = SafeGetString(reader, molNameIndex);
                        tmpRef.ChemicalFormula = SafeGetString(reader, formulaIndex);
                        tmpRef.Adduct = SafeGetString(reader, adductIndex);
                        tmpRef.InchiKey = SafeGetString(reader, inchiKeyIndex);
                        tmpRef.OtherKeys = SafeGetString(reader, otherKeysIndex);
                    }
                }
                else
                {
                    tmpRef.SetIonMobility(0, IonMobilityType.None);
                    tmpRef.CollisionalCrossSection = ccsIndex >= 0 && !reader.IsDBNull(ccsIndex) ? reader.GetDouble(ccsIndex) : 0;
                }

                if (startTimeIndex >= 0 && endTimeIndex >= 0)
                {
                    tmpRef.StartTime = reader.IsDBNull(startTimeIndex) ? 0 : reader.GetDouble(startTimeIndex);
                    tmpRef.EndTime = reader.IsDBNull(endTimeIndex) ? 0 : reader.GetDouble(endTimeIndex);
                }

                if (ticIndex >= 0)
                    tmpRef.TotalIonCurrentRaw = reader.IsDBNull(ticIndex) ? 0 : reader.GetDouble(ticIndex);

                // Older blibs (and some source data paths) leave these as NULL — cpp's
                // sqlite3_column_double returns 0 on NULL; .NET's GetDouble throws.
                tmpRef.IonMobilityHighEnergyOffset = highEnergyOffsetIndex >= 0 && !reader.IsDBNull(highEnergyOffsetIndex)
                    ? reader.GetDouble(highEnergyOffsetIndex) : 0;
                tmpRef.RetentionTime = retentionTimeIndex >= 0 && !reader.IsDBNull(retentionTimeIndex)
                    ? reader.GetDouble(retentionTimeIndex) : 0;
                tmpRef.ModifiedSequence = pepModSeq;
                tmpRef.PrevAa = "-";
                tmpRef.NextAa = "-";
                tmpRef.Score = scoreIndex >= 0 && !reader.IsDBNull(scoreIndex) ? reader.GetDouble(scoreIndex) : 0;
                tmpRef.ScoreType = scoreTypeIndex >= 0 ? SafeGetIntCoerce(reader, scoreTypeIndex) : 0;
                // cpp parity: BlibFilter.cpp:564 — SpecIDinFile may be a non-integer string in
                // the source DB; cpp does sqlite3_column_int which coerces or returns 0.
                tmpRef.ScanNumber = scanNumberIndex >= 0 ? SafeGetIntCoerce(reader, scanNumberIndex) : 0;

                // cpp parity: BlibFilter.cpp:567-569 — small molecule IonID is appended to the
                // group key. One of pepModSeq / smallMoleculeIonID is always empty.
                var smallMoleculeIonId = tmpRef.GetSmallMoleculeIonId();
                pepModSeq += smallMoleculeIonId;

                var numPeaks = numPeaksIndex >= 0 ? reader.GetInt32(numPeaksIndex) : 0;

                // cpp parity: BlibFilter.cpp:573-599 — fetch peak blobs unless useBestScoring
                // (in which case peaks are not needed for selecting the winner).
                if (!_useBestScoring)
                {
                    var refSpectraId = reader.GetInt32(0);
                    var peaks = ReadPeaks(_redundantFileName, refSpectraId, numPeaks);
                    if (peaks.Count == 0 && numPeaks != 0)
                    {
                        Verbosity.Error(string.Format(
                            CultureInfo.InvariantCulture,
                            "Unable to read peaks for redundant library spectrum {0}, sequence {1}, charge {2}.",
                            tmpRef.LibSpecId, tmpRef.DisplayName, tmpRef.Charge));
                    }
                    tmpRef.SetRawPeaks(peaks);
                }

                // cpp parity: BlibFilter.cpp:603-624 — group test: same peptide+charge (or
                // same molecule+adduct for small molecules) means add to the current group;
                // otherwise flush the current group and start a new one.
                bool sameGroup;
                if (!string.IsNullOrEmpty(pepModSeq))
                    sameGroup = pepModSeq == lastPepModSeq && lastCharge == charge;
                else
                    sameGroup = tmpRef.SmallMolMetadata.Equals(lastMoleculeAndAdduct);

                if (sameGroup)
                {
                    oneIon.Add(tmpRef);
                }
                else
                {
                    if (oneIon.Count > 0)
                    {
                        Verbosity.Comment(VerbosityLevel.Detail, string.Format(
                            CultureInfo.InvariantCulture,
                            "Selecting spec for {0}, charge {1} from {2} spectra.",
                            string.IsNullOrEmpty(lastPepModSeq) ? lastMoleculeAndAdduct.MoleculeName : lastPepModSeq,
                            lastCharge, oneIon.Count));
                        CompAndInsert(oneIon, bestSpectraIdAndCount);
                        oneIon.Clear();
                    }

                    oneIon.Add(tmpRef);
                    lastPepModSeq = pepModSeq;
                    lastCharge = charge;
                    // cpp parity: BlibFilter.cpp:620 — copy SmallMolMetadata for the new group.
                    var src = tmpRef.SmallMolMetadata;
                    lastMoleculeAndAdduct = new SmallMolMetadata
                    {
                        MoleculeName = src.MoleculeName,
                        ChemicalFormula = src.ChemicalFormula,
                        PrecursorAdduct = src.PrecursorAdduct,
                        InchiKey = src.InchiKey,
                        OtherKeys = src.OtherKeys,
                        PrecursorMzDeclared = src.PrecursorMzDeclared,
                    };

                    Verbosity.Comment(VerbosityLevel.Detail, string.Format(
                        CultureInfo.InvariantCulture,
                        "Collecting spec for {0}, charge {1},",
                        tmpRef.DisplayName, charge));
                }
            }
        }

        // cpp parity: BlibFilter.cpp:630-636 — flush the trailing group.
        if (oneIon.Count > 0)
        {
            progress.Increment();
            Verbosity.Comment(VerbosityLevel.Detail, string.Format(
                CultureInfo.InvariantCulture,
                "Selecting spec for {0}, charge {1} from {2} spectra.",
                oneIon[0].DisplayName, lastCharge, oneIon.Count));
            CompAndInsert(oneIon, bestSpectraIdAndCount);
            oneIon.Clear();
        }

        TransferSpectra(_redundantDbName, bestSpectraIdAndCount, _tableVersion);

        // cpp parity: BlibFilter.cpp:642 — clean up the progress bar.
        progress.Finish();
        progress.Complete();
    }

    /// <summary>
    /// cpp <c>BlibFilter::buildNonRedundantLib</c> at BlibFilter.cpp:317-370 — classify the
    /// known ScoreType values into <c>higherIsBetter</c> / <c>lowerIsBetter</c> buckets. If
    /// we encounter an unknown ScoreType that is actually present in RefSpectra, fall back
    /// to dot-product mode by setting <see cref="_useBestScoring"/> back to false.
    /// </summary>
    private void ClassifyScoreTypes()
    {
        using var scoreCmd = Db.CreateCommand();
        scoreCmd.CommandText = "SELECT id, scoreType FROM ScoreTypes";
        using var scoreReader = scoreCmd.ExecuteReader();

        using var checkCmd = Db.CreateCommand();
        checkCmd.CommandText = $"SELECT EXISTS(SELECT 1 FROM {_redundantDbName}.RefSpectra WHERE scoreType = @scoreType)";
        var checkParam = checkCmd.Parameters.Add("@scoreType", System.Data.DbType.Int32);

        while (scoreReader.Read())
        {
            var scoreTypeId = scoreReader.GetInt32(0);
            var scoreTypeName = scoreReader.GetString(1);
            // cpp parity: BlibFilter.cpp:330-352 — explicit higher/lower-is-better lists.
            // cpp comment at line 336: SEQUEST XCORR is actually the associated q-value here.
            switch (scoreTypeName)
            {
                case "PERCOLATOR QVALUE":
                case "IDPICKER FDR":
                case "MASCOT IONS SCORE":
                case "TANDEM EXPECTATION VALUE":
                case "OMSSA EXPECTATION SCORE":
                case "PROTEIN PROSPECTOR EXPECTATION SCORE":
                case "SEQUEST XCORR":
                case "MAXQUANT SCORE":
                case "MORPHEUS SCORE":
                case "MSGF+ SCORE":
                case "PEAKS CONFIDENCE SCORE":
                case "BYONIC SCORE":
                case "GENERIC Q-VALUE":
                    _higherIsBetter[scoreTypeId] = false;
                    break;
                case "SPECTRUM MILL":
                case "WATERS MSE PEPTIDE SCORE":
                case "PEPTIDE PROPHET SOMETHING":
                case "PROTEIN PILOT CONFIDENCE":
                case "SCAFFOLD SOMETHING":
                case "HARDKLOR IDOTP":
                case "PEPTIDE SHAKER CONFIDENCE":
                    _higherIsBetter[scoreTypeId] = true;
                    break;
                default:
                    Verbosity.Warn($"Don't know if higher or lower is better: {scoreTypeName}");
                    checkParam.Value = scoreTypeId;
                    var present = Convert.ToInt32(checkCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
                    if (present == 1)
                    {
                        // cpp parity: BlibFilter.cpp:360-364 — bail out of best-scoring mode if
                        // a spectrum with an unknown score type would actually be selected.
                        Verbosity.Warn("Cannot filter by score, reverting to normal behavior");
                        _useBestScoring = false;
                        return;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// cpp <c>BlibFilter::compAndInsert</c> at BlibFilter.cpp:707-875. Given a list of
    /// RefSpectrum with the same peptide+charge, pick the best representative and append
    /// its id (+ group size) to <paramref name="bestSpectraIdAndCount"/>. Also inserts a
    /// row into <c>RetentionTimes</c> for every member of the group.
    /// </summary>
    /// <remarks>
    /// Selection rules (cpp parity):
    /// <list type="bullet">
    /// <item>1 spectrum — pick it.</item>
    /// <item>2 spectra (default mode) — pick the one with more peaks; ties go to index 0.</item>
    /// <item>3+ spectra (default mode) — process peaks, compute pairwise dot products,
    ///   pick the spectrum with the highest sum of dot products (== highest average since
    ///   the divisor is the same for all). If the average is &lt; <see cref="MinAverageScore"/>,
    ///   drop the entire group.</item>
    /// <item>useBestScoring mode — group by ScoreType, find best score in each group
    ///   (direction per <see cref="_higherIsBetter"/>); if multiple possible winners,
    ///   highest TIC wins.</item>
    /// </list>
    /// </remarks>
    private void CompAndInsert(List<RefSpectrum> oneIon, List<(int Id, int Copies)> bestSpectraIdAndCount)
    {
        var numSpec = oneIon.Count;
        var specId = 0;
        var bestIndex = 0;

        if (!_useBestScoring)
        {
            if (numSpec == 1)
            {
                bestSpectraIdAndCount.Add((oneIon[0].LibSpecId, 1));
            }
            else if (numSpec == 2)
            {
                // cpp parity: BlibFilter.cpp:719 — in the future, pick the one with the best
                // search score. For now, pick the one with more peaks.
                if (oneIon[0].NumRawPeaks < oneIon[1].NumRawPeaks)
                    bestIndex = 1;
                bestSpectraIdAndCount.Add((oneIon[bestIndex].LibSpecId, 2));
            }
            else
            {
                // cpp parity: BlibFilter.cpp:723-776 — process peaks then compute all-by-all
                // dot products.

                // preprocess all RefSpectrum in oneIon (cpp BlibFilter.cpp:727-736).
                // cpp defaults: clearPrecursor=true, numTopPeaks=100, binSize=1, binOffset=0.
                var proc = new PeakProcessor
                {
                    ClearPrecursor = true,
                    NumTopPeaks = PeakProcessorNumTopPeaks,
                };
                foreach (var rs in oneIon)
                    proc.ProcessPeaks(rs);

                // create an array where we'll sum scores for each spectrum, init to 0.
                var scores = new double[oneIon.Count];

                // cpp parity: BlibFilter.cpp:743-758 — upper-triangular pairwise dot product.
                // cpp builds a Match(tmpRef1, tmpRef2) and calls DotProduct::compare; the C#
                // Match takes (Spectrum, RefSpectrum), so cast the first arg accordingly.
                for (var i = 0; i < oneIon.Count; i++)
                {
                    var refI = oneIon[i];
                    for (var j = i + 1; j < oneIon.Count; j++)
                    {
                        var refJ = oneIon[j];
                        var thisMatch = new Match(refI, refJ);
                        DotProduct.Compare(thisMatch);
                        var dotProduct = thisMatch.GetScore(MatchScoreType.Dotp);
                        scores[i] += dotProduct;
                        scores[j] += dotProduct;
                    }
                }

                // cpp parity: BlibFilter.cpp:761 — find the index of the maximum element. cpp
                // getMaxElementIndex returns the FIRST max in tie cases. Ties show up routinely
                // when redundant spectra come from the same source replicate; relying on the
                // float-precision asymmetry inside DotProduct.Compare (see comment there) is
                // what makes cpp and C# pick the same id when several spectra are equally good.
                bestIndex = GetMaxElementIndex(scores);
                var bestScore = scores[bestIndex];
                var bestAverageScore = bestScore / oneIon.Count;

                // cpp parity: BlibFilter.cpp:766-775 — drop the entire group if the best
                // average score is too low.
                if (bestAverageScore >= _minAverageScore)
                {
                    bestSpectraIdAndCount.Add((oneIon[bestIndex].LibSpecId, oneIon.Count));
                }
                else
                {
                    Verbosity.Warn(string.Format(
                        CultureInfo.InvariantCulture,
                        "Best score is {0} for {1}, charge {2} after comparing {3} spectra.  This sequence will not be included in the filtered library.",
                        bestAverageScore, oneIon[0].Sequence, oneIon[0].Charge, oneIon.Count));
                    return;
                }
            }
        }
        else
        {
            // cpp parity: BlibFilter.cpp:777-835 — useBestScoring mode.
            if (numSpec == 1)
            {
                bestSpectraIdAndCount.Add((oneIon[0].LibSpecId, 1));
            }
            else
            {
                var indices = new Dictionary<RefSpectrum, int>(ReferenceEqualityComparer.Instance);
                var groups = GroupByScoreType(oneIon, indices);
                RefSpectrum? winner = null;

                var possibleWinners = new List<RefSpectrum>();
                foreach (var kv in groups)
                {
                    if (!_higherIsBetter.TryGetValue(kv.Key, out var higherIsBetter))
                    {
                        Verbosity.Error(string.Format(
                            CultureInfo.InvariantCulture,
                            "Don't know if higher or lower is better for score type {0}",
                            kv.Key));
                    }
                    var bestScores = GetBestScores(kv.Value, higherIsBetter);
                    possibleWinners.AddRange(bestScores);
                }

                if (possibleWinners.Count == 1)
                {
                    winner = possibleWinners[0];
                }
                else
                {
                    // cpp parity: BlibFilter.cpp:799-806 — find highest TIC to break the tie.
                    var winningValue = -1.0;
                    foreach (var rs in possibleWinners)
                    {
                        var specValue = rs.TotalIonCurrentRaw;
                        if (specValue > winningValue)
                        {
                            winner = rs;
                            winningValue = specValue;
                        }
                    }
                }

                if (winner is null)
                {
                    // Defensive — cpp asserts via the algorithm above that winner != NULL when
                    // numSpec > 1 and groups is non-empty; we throw rather than null-deref.
                    throw new BlibException(false, "BlibFilter: no winner could be selected from best-scoring group.");
                }

                bestSpectraIdAndCount.Add((winner.LibSpecId, oneIon.Count));
                bestIndex = indices[winner];
            }
        }

        if (bestSpectraIdAndCount.Count > 0)
            specId = bestSpectraIdAndCount[^1].Id;

        // cpp parity: BlibFilter.cpp:841-874 — insert one RetentionTimes row per group member.
        for (var i = 0; i < numSpec; i++)
        {
            var spectrum = oneIon[i];
            var specIdRedundant = spectrum.LibSpecId;
            var retentionTime = spectrum.RetentionTime;
            var startTime = spectrum.StartTime;
            var endTime = spectrum.EndTime;

            var stmt = _insertStmt ?? throw new InvalidOperationException("Insert statement not initialised; call Init first.");
            stmt.Parameters["@refId"].Value = specId;
            stmt.Parameters["@redundantRefId"].Value = specIdRedundant;
            stmt.Parameters["@sourceId"].Value = GetNewFileId(_redundantDbName, specIdRedundant);
            stmt.Parameters["@ionMobility"].Value = spectrum.IonMobility;
            stmt.Parameters["@ccs"].Value = spectrum.CollisionalCrossSection;
            stmt.Parameters["@ionMobilityHEO"].Value = spectrum.IonMobilityHighEnergyOffset;
            stmt.Parameters["@ionMobilityType"].Value = (int)spectrum.IonMobilityType;
            stmt.Parameters["@retentionTime"].Value = retentionTime != 0 ? retentionTime : DBNull.Value;
            if (startTime != 0 && endTime != 0)
            {
                stmt.Parameters["@startTime"].Value = startTime;
                stmt.Parameters["@endTime"].Value = endTime;
            }
            else
            {
                stmt.Parameters["@startTime"].Value = DBNull.Value;
                stmt.Parameters["@endTime"].Value = DBNull.Value;
            }
            stmt.Parameters["@score"].Value = spectrum.Score;
            stmt.Parameters["@bestSpectrum"].Value = i == bestIndex ? 1 : 0;

            try
            {
                if (stmt.ExecuteNonQuery() != 1)
                    throw new BlibException(false, "Error inserting row into RetentionTimes");
            }
            catch (SQLiteException ex)
            {
                throw new BlibException(false, "Error inserting row into RetentionTimes: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// cpp <c>BlibFilter::groupByScoreType</c> at BlibFilter.cpp:877-891. Builds a
    /// <c>scoreType → spectra</c> map; <paramref name="outIndices"/> records each
    /// spectrum's position within the input list (used to find <c>bestIndex</c>).
    /// </summary>
    private static Dictionary<int, List<RefSpectrum>> GroupByScoreType(
        List<RefSpectrum> oneIon, Dictionary<RefSpectrum, int> outIndices)
    {
        var groups = new Dictionary<int, List<RefSpectrum>>();
        var index = -1;
        foreach (var rs in oneIon)
        {
            var scoreType = rs.ScoreType;
            if (!groups.TryGetValue(scoreType, out var list))
            {
                list = new List<RefSpectrum>();
                groups[scoreType] = list;
            }
            list.Add(rs);
            outIndices[rs] = ++index;
        }
        return groups;
    }

    /// <summary>
    /// cpp <c>BlibFilter::getBestScores</c> at BlibFilter.cpp:893-916. Return the subset
    /// of <paramref name="group"/> tied for the best score; "best" means greatest when
    /// <paramref name="higherIsBetter"/> is true, otherwise least.
    /// </summary>
    private static List<RefSpectrum> GetBestScores(List<RefSpectrum> group, bool higherIsBetter)
    {
        var best = new List<RefSpectrum>();
        if (group.Count == 0)
            return best;

        best.Add(group[0]);
        if (group.Count == 1)
            return best;

        var bestScore = best[0].Score;

        for (var i = 1; i < group.Count; i++)
        {
            var score = group[i].Score;
            if ((!higherIsBetter && score < bestScore) ||
                (higherIsBetter && score > bestScore))
            {
                best.Clear();
                best.Add(group[i]);
                bestScore = score;
            }
            else if (score == bestScore)
            {
                best.Add(group[i]);
            }
        }
        return best;
    }

    /// <summary>
    /// cpp <c>getMaxElementIndex</c>. Returns the index of the first occurrence of the
    /// maximum value in <paramref name="values"/>; assumes non-empty input.
    /// </summary>
    private static int GetMaxElementIndex(double[] values)
    {
        var bestIdx = 0;
        var bestVal = values[0];
        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] > bestVal)
            {
                bestVal = values[i];
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    /// <summary>
    /// cpp <c>BlibFilter::getUncompressedPeaks</c> at BlibFilter.cpp:645-691. Opens the
    /// redundant SQLite directly to read the peak blobs for a given spectrum, then decodes
    /// them into a peak list (decompressing zlib-wrapped blobs when necessary).
    /// </summary>
    private static List<PeakT> ReadPeaks(string redundantFile, int refSpectraId, int numPeaks)
    {
        if (numPeaks == 0)
            return new List<PeakT>();

        using var conn = SqliteRoutine.Open(redundantFile, readOnly: true);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT peakMZ, peakIntensity FROM RefSpectraPeaks WHERE RefSpectraId = @id";
        cmd.Parameters.AddWithValue("@id", refSpectraId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            Verbosity.Error($"Did not find peaks for spectrum {refSpectraId.ToString(CultureInfo.InvariantCulture)}.");
            return new List<PeakT>();
        }

        // NULL blobs (corrupt source library or partial write) fall through to an empty
        // peak list, not InvalidCastException. cpp parity: sqlite3_column_blob returns NULL
        // → 0-byte buffer that fails the length check.
        var mzBlob = reader.IsDBNull(0) ? Array.Empty<byte>() : (byte[])reader[0];
        var intBlob = reader.IsDBNull(1) ? Array.Empty<byte>() : (byte[])reader[1];

        // cpp parity: BlibFilter.cpp:654-668 — if the blob length already matches the
        // uncompressed size, the data is stored raw; otherwise zlib-decompress it.
        // `checked` arithmetic makes integer overflow at ~268M peaks fail loudly (a synthetic
        // .blib could try this) rather than silently producing a negative expected-size that
        // routes every blob through Decompress and corrupts the output.
        int mzExpected, intExpected;
        try
        {
            mzExpected = checked(numPeaks * sizeof(double));
            intExpected = checked(numPeaks * sizeof(float));
        }
        catch (OverflowException)
        {
            Verbosity.Warn(
                $"Spectrum {refSpectraId} declares {numPeaks} peaks — exceeds int.MaxValue "
                + "in byte-buffer size; treating as no peaks.");
            return new List<PeakT>();
        }

        var mzBytes = mzBlob.Length == mzExpected ? mzBlob : Decompress(mzBlob, mzExpected);
        var intBytes = intBlob.Length == intExpected ? intBlob : Decompress(intBlob, intExpected);

        var peaks = new List<PeakT>(numPeaks);
        for (var i = 0; i < numPeaks; i++)
        {
            var mz = BitConverter.IsLittleEndian
                ? BitConverter.ToDouble(mzBytes, i * sizeof(double))
                : BitConverter.Int64BitsToDouble(System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(mzBytes.AsSpan(i * sizeof(double))));
            var intensity = BitConverter.IsLittleEndian
                ? BitConverter.ToSingle(intBytes, i * sizeof(float))
                : BitConverter.Int32BitsToSingle(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(intBytes.AsSpan(i * sizeof(float))));

            // cpp parity: BlibFilter.cpp:675-680 — corrupted peak detection. Return empty
            // list (which surfaces as an error upstream).
            if (double.IsNaN(mz) || mz < 0 || mz > 100000 ||
                float.IsNaN(intensity) || intensity < 0)
            {
                return new List<PeakT>();
            }

            peaks.Add(new PeakT(mz, intensity));
        }

        return peaks;
    }

    /// <summary>
    /// Decompress a zlib-wrapped byte array into a buffer of <paramref name="expectedSize"/>
    /// bytes. Matches cpp <c>uncompress()</c> which expects zlib (RFC 1950) format. The
    /// cpp side writes via <c>compress()</c> which emits zlib format; .NET 8's
    /// <see cref="System.IO.Compression.ZLibStream"/> reads that format directly.
    /// </summary>
    private static byte[] Decompress(byte[] compressed, int expectedSize)
    {
        using var ms = new MemoryStream(compressed);
        using var z = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionMode.Decompress);
        var output = new byte[expectedSize];
        var read = 0;
        while (read < expectedSize)
        {
            var n = z.Read(output, read, expectedSize - read);
            if (n == 0) break;
            read += n;
        }
        return output;
    }

    private static string SafeGetString(SQLiteDataReader reader, int ordinal)
    {
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return string.Empty;
        return reader.GetString(ordinal);
    }

    /// <summary>
    /// cpp parity: BlibFilter.cpp:564 — sqlite3_column_int silently coerces text → 0.
    /// The C# reader throws on text → int; mirror cpp by reading the value as text first
    /// and parsing leniently.
    /// </summary>
    private static int SafeGetIntCoerce(SQLiteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return 0;
        var raw = reader.GetValue(ordinal);
        switch (raw)
        {
            case long l: return checked((int)l);
            case int i: return i;
            case short s2: return s2;
            // System.Data.SQLite maps TINYINT to unsigned byte; recover signedness so
            // workflowType / scoreType of -1 doesn't read back as 255.
            case byte b: return (sbyte)b;
            case sbyte sb: return sb;
            case double d: return (int)d;
            case float f: return (int)f;
        }
        var s = raw?.ToString() ?? string.Empty;
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            return v;
        // cpp's sqlite3_column_int silently returns 0 for non-numeric text; behavior preserved
        // for cpp parity, but a debug log surfaces the silent coercion when troubleshooting an
        // unexpected zero in the filtered library (e.g. a non-integer SpecIDinFile token).
        Verbosity.Debug(
            $"SafeGetIntCoerce: non-numeric value '{s}' at ordinal {ordinal}; coercing to 0 (cpp parity).");
        return 0;
    }

    // --- CLI parsing -----------------------------------------------------------------------

    /// <summary>
    /// cpp <c>BlibFilter</c> uses boost::program_options for CLI parsing (BlibFilter.cpp:149).
    /// The C# port hooks into the BlibMaker framework instead: consume short switches via
    /// <see cref="ParseNextSwitch"/>, then take the second-to-last and last positional args
    /// as the redundant + filtered library paths (cpp argNames at BlibFilter.cpp:177-179).
    /// </summary>
    public override int ParseCommandArgs(string[] argv)
    {
        ArgumentNullException.ThrowIfNull(argv);

        // cpp parity: no positional args at all → print usage and exit 1.
        if (argv.Length == 0)
        {
            Usage();
            // Defensive throw — Usage() exits, but if a test override doesn't, we need to
            // bail before the lib-name logic below.
            throw new BlibException(false, "Missing required arguments.");
        }

        // cpp parity: support `-h` / `--help` even though boost::program_options provides it
        // automatically (BiblioSpec::CommandLine wires it in). We treat any `-h` switch as
        // "print usage + exit 1".
        foreach (var a in argv)
        {
            if (a == "-h" || a == "--help")
            {
                Usage();
                throw new BlibException(false, "Help requested.");
            }
        }

        // Consume short options. Unlike BlibBuilder, BlibFilter requires TWO positional args
        // at the end (redundant + filtered), so we override the base logic.
        var i = 0;
        while (i < argv.Length)
        {
            var arg = argv[i];
            if (arg.Length != 2 || arg[0] != '-')
                break;
            i = ParseNextSwitch(i, argv);
        }

        // cpp parity: BlibFilter.cpp:177-189 — two positional args required.
        var remaining = argv.Length - i;
        if (remaining < 2)
        {
            Usage();
            throw new BlibException(false, "Missing required arguments: redundant-library filtered-library.");
        }

        // Penultimate arg = redundant input; last arg = filtered output.
        var redundant = argv[argv.Length - 2];
        var filtered = argv[argv.Length - 1];
        VerifyFileExists(redundant);
        _redundantFileName = redundant;
        SetLibName(filtered);

        return i;
    }

    /// <summary>
    /// cpp BlibFilter switches (BlibFilter.cpp:157-174) translated into the BlibMaker
    /// short-switch loop.
    /// </summary>
    protected override int ParseNextSwitch(int i, string[] argv)
    {
        ArgumentNullException.ThrowIfNull(argv);
        if (i < 0 || i >= argv.Length) throw new ArgumentOutOfRangeException(nameof(i));

        var arg = argv[i];
        var switchName = arg[1];

        switch (switchName)
        {
            case 'm':
                // cpp parity: BlibFilter.cpp:158-160 — `-m <megabytes>` SQLite cache size.
                // Default 250M. This shadows BlibMaker's identically-named -m switch (which
                // does the same thing) and overrides for parity.
                if (++i < argv.Length)
                {
                    if (!int.TryParse(argv[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mb) || mb <= 0)
                        throw new BlibException(false, "Invalid -m cache size specified.");
                    CacheSizeMb = mb;
                }
                break;

            case 'n':
                // cpp parity: BlibFilter.cpp:162-164 — `-n <min-peaks>` filter. Default 1.
                if (++i < argv.Length)
                {
                    if (!int.TryParse(argv[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < 0)
                        throw new BlibException(false, "Invalid -n min-peaks value specified.");
                    _minPeaks = n;
                }
                break;

            case 's':
                // cpp parity: BlibFilter.cpp:166-168 — `-s <min-score>`. Default 0.
                if (++i < argv.Length)
                {
                    if (!double.TryParse(argv[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
                        throw new BlibException(false, "Invalid -s min-score value specified.");
                    _minAverageScore = s;
                }
                break;

            case 'b':
                // cpp parity: BlibFilter.cpp:170-172 — `-b` (with optional bool arg) toggles
                // best-scoring mode. boost::program_options treats `-b` as `-b true`; here
                // we accept an optional argument that parses as a bool, or default to true.
                if (i + 1 < argv.Length && bool.TryParse(argv[i + 1], out var b))
                {
                    _useBestScoring = b;
                    i++;
                }
                else
                {
                    _useBestScoring = true;
                }
                break;

            default:
                // Fall through to the BlibMaker base implementation for -v, -a, -i, -d, -t, -z.
                // -m is handled above (it overrides the base's -m, but they do the same thing).
                return base.ParseNextSwitch(i, argv);
        }

        return Math.Min(argv.Length, i + 1);
    }

    /// <summary>
    /// cpp <c>BlibFilter</c> has no explicit usage() override; the boost::program_options
    /// CommandLine class generates the help text from the registered options. The C# port
    /// emits the same text by hand for parity.
    /// </summary>
    public override void Usage()
    {
        var usage =
            "Usage: BlibFilter [options] <redundant-library> <filtered-library>\n" +
            "Options:\n" +
            "   -m <megabytes>    SQLite memory cache size in megabytes. Default 250M.\n" +
            "   -n <min-peaks>    Only include spectra with at least this many peaks. Default 1.\n" +
            "   -s <min-score>    Best spectrum must have at least this average score to be included. Default 0.\n" +
            "   -b [<bool>]       Select only the spectrum with the best score for each peptide\n" +
            "                     (spectrum TIC is used as tiebreaker). Default false.\n" +
            "   -v                Verbose output (cpp BlibMaker base option).\n" +
            "   -a <authority>    LSID authority. Default proteome.gs.washington.edu.\n" +
            "   -i <library_id>   LSID library ID. Default uses file name.\n";
        Console.Error.WriteLine(usage);
        Environment.Exit(1);
    }
}
