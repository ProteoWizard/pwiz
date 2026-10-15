// Port of pwiz_tools/BiblioSpec/src/MSFReader.{h,cpp} and MSFSpecReader.{h,cpp}
//
// Reads Thermo Proteome Discoverer .msf (unfiltered) and .pdResult (filtered) files.
// Both are SQLite databases; the `filtered_` flag is driven by file extension. For
// .pdResult >= 3.1 there is also a sibling .pdResultDetails database that gets ATTACH'd
// in to supply the MassSpectrumItems table.
//
// The reader doubles as the spec-file reader (cpp `specReader_ = this`): peaks come from
// a zip-archived spectrum XML blob in the database, decompressed and parsed in memory.

using System.Data.SQLite;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Reads Thermo Proteome Discoverer .msf (unfiltered) and .pdResult (filtered, persistent)
/// files. Both are SQLite-backed; the reader doubles as the spec-file reader since peaks
/// come from a zip-archived spectrum XML blob inside the database.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::MSFReader</c> at <c>pwiz_tools/BiblioSpec/src/MSFReader.{h,cpp}</c>.
/// Schema-version gating mirrors the cpp behaviour: older (&lt; 2.2) unfiltered .msf files use
/// the <c>Peptides</c> / <c>SpectrumHeaders</c> / <c>Spectra</c> / <c>MassPeaks</c> tables;
/// newer files and all filtered <c>.pdResult</c> files use <c>TargetPsms</c> /
/// <c>TargetPsmsMSnSpectrumInfo</c> / <c>MSnSpectrumInfo</c> / <c>MassSpectrumItems</c>.</para>
/// <para>For .pdResult >= 3.1 the <c>MassSpectrumItems</c> table lives in a separate
/// <c>.pdResultDetails</c> sibling file; we ATTACH it as the <c>details</c> schema and read
/// from <c>details.MassSpectrumItems</c>.</para>
/// </remarks>
public sealed class MSFReader : BuildParser
{
    private readonly string _msfName;
    private readonly bool _filtered;
    private SQLiteConnection? _db;
    private int _schemaVersionMajor = -1;
    private int _schemaVersionMinor = -1;
    private bool _detailsAttached;

    // (filename -> scoreType -> psms). cpp uses std::map (sorted) for both levels; we
    // mirror with SortedDictionary so PSM emission order is byte-for-byte stable across
    // (filename, scoreType). The downstream RefSpectraID is assigned in iteration order, so
    // golden-file parity requires this.
    private readonly SortedDictionary<string, SortedDictionary<PsmScoreType, List<PSM>>> _fileMap =
        new(StringComparer.Ordinal);
    // FileID -> filename, populated only for older (<2.2) unfiltered .msf files.
    private readonly Dictionary<int, string> _fileNameMap = new();
    // uniqueSpecId -> SpecData.
    private readonly Dictionary<string, SpecData> _spectra = new(StringComparer.Ordinal);

    /// <summary>True if the file path ends with <c>.msf</c> or <c>.pdResult</c> (case-insensitive).</summary>
    public static bool AcceptsExtension(string path) =>
        BlibBuilder.HasExtensionCi(path, ".msf") ||
        BlibBuilder.HasExtensionCi(path, ".pdResult");

    /// <summary>
    /// Construct an MSFReader bound to <paramref name="builder"/> and the file at
    /// <paramref name="msfFile"/>. cpp parity: MSFReader.cpp:27.
    /// </summary>
    public MSFReader(BlibBuilder builder, string msfFile, ProgressIndicator? parentProgress)
        : base(builder, msfFile, parentProgress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(msfFile);

        _msfName = msfFile;
        // cpp parity: MSFReader.cpp:31 — has_extension(msfFile, ".pdResult").
        _filtered = BlibBuilder.HasExtensionCi(msfFile, ".pdResult");

        // cpp parity: MSFReader.cpp:33 — record the msf path as the spec file (no existence
        // check; the spectra live inside the same db).
        SetSpecFileName(msfFile, checkFile: false);
        // cpp parity: MSFReader.cpp:34 — NAME_ID lookup so PSM.SpecName drives spectrum lookup.
        LookUpBy = SpecIdType.NameId;
        // cpp parity: MSFReader.cpp:36 — `delete specReader_; specReader_ = this;`. The
        // C# port uses an inner MsfSpecFileReader that delegates back to the parent reader
        // (which owns the in-memory _spectra map).
        SpecReader = new MsfSpecFileReader(this);
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: MSFReader.cpp:40.</remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_detailsAttached && _db is { State: System.Data.ConnectionState.Open })
            {
                try
                {
                    using var detach = _db.CreateCommand();
                    detach.CommandText = "DETACH DATABASE details";
                    detach.ExecuteNonQuery();
                }
                catch
                {
                    // best-effort cleanup; the connection close below will release it anyway.
                }
            }
            _db?.Dispose();
            _db = null;
        }
        base.Dispose(disposing);
    }

    /// <summary>Score types this reader produces. cpp parity: MSFReader.cpp:139.</summary>
    public override IList<PsmScoreType> GetScoreTypes()
    {
        OpenFile();
        GetScoreInfo(out _, out _, out var scoreType, out _, out _);
        return new[] { scoreType };
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: MSFReader.cpp:94.</remarks>
    public override bool ParseFile()
    {
        OpenFile();

        if (!HasQValues() && GetScoreThreshold(BuildInput.Sqt) < 1)
        {
            throw new BlibException(false,
                "This file does not contain q-values. You can set a cut-off score of 0 in "
                + "order to build a library from it, but this may cause your library to "
                + "include a lot of false-positives.");
        }

        try
        {
            CollectSpectra();
        }
        // cpp parity: MSFReader.cpp:109 — surface a friendly message when the join target
        // table is missing (typically because the pdResult was exported without "Spectra to
        // store=All"). cpp catches BlibException; the C# SQLite layer throws SQLiteException
        // directly, so we widen the catch.
        catch (Exception e) when (e.Message.Contains(
            "no such table: details.MassSpectrumItems", StringComparison.OrdinalIgnoreCase))
        {
            throw new BlibException(false,
                "This file seems to have been exported without the appropriate "
                + "\"Spectra to store\" option. Export it again with the \"All\" setting.\r\n"
                + e.Message);
        }

        CollectPsms();

        // cpp parity: MSFReader.cpp:119 — add psms by filename.
        foreach (var (filename, scoreMap) in _fileMap)
        {
            foreach (var (scoreType, psms) in scoreMap)
            {
                if (psms.Count == 0) continue;
                Psms.Clear();
                foreach (var psm in psms)
                    Psms.Add(psm);
                SetSpecFileName(filename, checkFile: false);
                BuildTables(scoreType, filename);
            }
        }

        return true;
    }

    // --- file open / schema --------------------------------------------------------------

    /// <summary>cpp parity: MSFReader.cpp:67.</summary>
    private void OpenFile()
    {
        if (_db is { State: System.Data.ConnectionState.Open })
            return;

        // System.Data.SQLite requires `Data Source=`; we open read-write because the cpp
        // code uses sqlite3_open (read-write). The reader never writes, so read-only would
        // work, but read-write matches cpp's behaviour byte-for-byte.
        var connectionString = "Data Source=" + _msfName;
        _db = new SQLiteConnection(connectionString);
        try
        {
            _db.Open();
        }
        catch (Exception ex)
        {
            throw new BlibException(true, $"Couldn't open '{_msfName}'. {ex.Message}");
        }

        // Read schema version from SchemaInfo.
        using (var stmt = _db.CreateCommand())
        {
            stmt.CommandText = "SELECT SoftwareVersion FROM SchemaInfo";
            using var rd = stmt.ExecuteReader();
            if (rd.Read())
            {
                var version = rd.IsDBNull(0) ? string.Empty : rd.GetValue(0)?.ToString() ?? string.Empty;
                var pieces = version.Split('.');
                try
                {
                    _schemaVersionMajor = int.Parse(pieces[0], CultureInfo.InvariantCulture);
                    if (pieces.Length > 1)
                        _schemaVersionMinor = int.Parse(pieces[1], CultureInfo.InvariantCulture);
                    Verbosity.Debug($"Schema version is {_schemaVersionMajor} ({version})");
                }
                catch (FormatException)
                {
                    Verbosity.Error($"Unknown schema version format: '{version}'");
                }
            }
        }
        if (_schemaVersionMajor < 0)
        {
            Verbosity.Error("Could not determine schema version.");
        }
    }

    /// <summary>cpp parity: MSFReader.cpp:56 — schemaVersionMajor_.minor_ &lt; (major, minor).</summary>
    private bool VersionLess(int major, int minor) =>
        _schemaVersionMajor < major
        || (_schemaVersionMajor == major && _schemaVersionMinor < minor);

    /// <summary>cpp parity: MSFReader.cpp:60 — specId stringified with optional workflow prefix.</summary>
    private static string UniqueSpecId(int specId, int workflowId)
    {
        if (workflowId != 0)
            return (-workflowId).ToString(CultureInfo.InvariantCulture) + "."
                + specId.ToString(CultureInfo.InvariantCulture);
        return specId.ToString(CultureInfo.InvariantCulture);
    }

    // --- spectra collection -------------------------------------------------------------

    /// <summary>cpp parity: MSFReader.cpp:150.</summary>
    private void CollectSpectra()
    {
        int specCount;
        string sql;
        var hasCompensationVoltage = false;

        var detailsPath = Path.ChangeExtension(_msfName, ".pdResultDetails");

        if (!VersionLess(3, 1) && File.Exists(detailsPath))
        {
            // cpp parity: MSFReader.cpp:159 — ATTACH the .pdResultDetails sibling DB.
            using (var attach = _db!.CreateCommand())
            {
                // Use forward slashes to match cpp generic_string().
                var forwardPath = detailsPath.Replace('\\', '/');
                attach.CommandText = "ATTACH DATABASE '"
                    + SqliteRoutine.EscapeApostrophes(forwardPath) + "' AS details";
                attach.ExecuteNonQuery();
                _detailsAttached = true;
            }

            specCount = GetRowCount(
                "MSnSpectrumInfo WHERE SpectrumID IN (SELECT DISTINCT MSnSpectrumInfoSpectrumID FROM TargetPsmsMSnSpectrumInfo)");
            // hasCompensationVoltage stays false here, matching cpp MSFReader.cpp:161 (the
            // cpp branch ignores the column-existence check inside the v3.1+ branch).
            sql =
                "SELECT SpectrumID, MSnSpectrumInfo.RetentionTime, Mass, Charge, Spectrum, MSnSpectrumInfo.WorkflowID"
                + (hasCompensationVoltage ? ", CompVoltageV " : " ")
                + "FROM MSnSpectrumInfo "
                + "JOIN details.MassSpectrumItems ON MSnSpectrumInfo.SpectrumID = details.MassSpectrumItems.ID "
                + "AND MSnSpectrumInfo.WorkflowID = details.MassSpectrumItems.WorkflowID";
        }
        else if (_filtered || !VersionLess(2, 2))
        {
            specCount = GetRowCount(
                "MSnSpectrumInfo WHERE SpectrumID IN (SELECT DISTINCT MSnSpectrumInfoSpectrumID FROM TargetPsmsMSnSpectrumInfo)");
            hasCompensationVoltage = ColumnExists(_db!, "MSnSpectrumInfo", "CompVoltageV");
            sql =
                "SELECT SpectrumID, MSnSpectrumInfo.RetentionTime, Mass, Charge, Spectrum, MSnSpectrumInfo.WorkflowID"
                + (hasCompensationVoltage ? ", CompVoltageV " : " ")
                + "FROM MSnSpectrumInfo "
                + "JOIN MassSpectrumItems ON MSnSpectrumInfo.SpectrumID = MassSpectrumItems.ID "
                + "AND MSnSpectrumInfo.WorkflowID = MassSpectrumItems.WorkflowID";
        }
        else
        {
            specCount = GetRowCount(
                "SpectrumHeaders WHERE SpectrumID IN (SELECT DISTINCT SpectrumID FROM Peptides)");
            sql =
                "SELECT SpectrumID, RetentionTime, Mass, Charge, Spectrum, 0 "
                + "FROM SpectrumHeaders "
                + "JOIN Spectra ON SpectrumHeaders.UniqueSpectrumID = Spectra.UniqueSpectrumID "
                + "WHERE SpectrumID IN (SELECT DISTINCT SpectrumID FROM Peptides)";
        }

        Verbosity.Status($"Parsing {specCount.ToString(CultureInfo.InvariantCulture)} spectra.");

        using var cmd = _db!.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var rawId = reader.GetInt32(0);
            var workflowId = reader.GetInt32(5);
            var specIdStr = UniqueSpecId(rawId, workflowId);
            var mass = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);

            var sd = new SpecData
            {
                Id = rawId,
                RetentionTime = reader.IsDBNull(1) ? 0 : reader.GetDouble(1),
                Charge = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
            };
            if (sd.Charge > 0)
                sd.Mz = (mass + AminoAcidMasses.ProtonMass * sd.Charge) / sd.Charge;
            else
                sd.Mz = mass;

            if (hasCompensationVoltage)
            {
                sd.IonMobilityType = IonMobilityType.CompensationV;
                sd.IonMobility = (float)(reader.IsDBNull(6) ? 0 : reader.GetDouble(6));
            }

            // Spectrum blob — zip archive containing one entry with the spectrum XML.
            if (!reader.IsDBNull(4))
            {
                var blob = (byte[])reader.GetValue(4);
                var spectrumXml = UnzipSpectrum(specIdStr, blob);
                ReadSpectrum(specIdStr, spectrumXml, sd);
            }
            else
            {
                sd.NumPeaks = 0;
            }

            _spectra[specIdStr] = sd;
        }

        Verbosity.Debug($"Map has {_spectra.Count.ToString(CultureInfo.InvariantCulture)} spectra");
    }

    // cpp parity: MSFReader.cpp:226 — unzip a single-entry zip archive held in memory.
    private static string UnzipSpectrum(string specId, byte[] zipBytes)
    {
        try
        {
            using var ms = new MemoryStream(zipBytes, writable: false);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count != 1)
            {
                throw new BlibException(false,
                    $"Compressed spectrum {specId} has invalid format.");
            }
            var entry = archive.Entries[0];
            using var entryStream = entry.Open();
            using var sr = new StreamReader(entryStream, Encoding.UTF8);
            return sr.ReadToEnd();
        }
        catch (BlibException)
        {
            throw;
        }
        catch (InvalidDataException ex)
        {
            throw new BlibException(false,
                $"Could not open compressed spectrum {specId}: {ex.Message}");
        }
    }

    // cpp parity: MSFReader.cpp:293 — MSFSpecReader parses the unzipped XML and writes peaks
    // into the SpecData. This is a SAX-style walk that picks up <PeakCentroids><Peak X=".." Y=".." />.
    private static void ReadSpectrum(string specId, string spectrumXml, SpecData sd)
    {
        var mzs = new List<double>();
        var intens = new List<float>();

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreWhitespace = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                CloseInput = true,
            };
            using var sr = new StringReader(spectrumXml);
            using var xr = XmlReader.Create(sr, settings);
            var inPeakCentroids = false;
            while (xr.Read())
            {
                switch (xr.NodeType)
                {
                    case XmlNodeType.Element:
                    {
                        if (string.Equals(xr.LocalName, "PeakCentroids", StringComparison.Ordinal))
                        {
                            inPeakCentroids = true;
                        }
                        else if (inPeakCentroids
                                 && string.Equals(xr.LocalName, "Peak", StringComparison.Ordinal))
                        {
                            // cpp parity: MSFSpecReader.cpp:55 — required attributes X, Y.
                            var xs = xr.GetAttribute("X");
                            var ys = xr.GetAttribute("Y");
                            if (xs is null || ys is null)
                            {
                                throw new BlibException(false,
                                    $"Peak in spectrum {specId} missing required X/Y attribute.");
                            }
                            mzs.Add(double.Parse(xs, NumberStyles.Float, CultureInfo.InvariantCulture));
                            // cpp parity: MSFSpecReader.cpp:57 — getDoubleRequiredAttrValue parses Y
                            // as double then push_back into vector<float>; we mimic the narrowing.
                            intens.Add((float)double.Parse(ys, NumberStyles.Float, CultureInfo.InvariantCulture));
                        }
                        break;
                    }
                    case XmlNodeType.EndElement:
                    {
                        if (string.Equals(xr.LocalName, "PeakCentroids", StringComparison.Ordinal))
                            inPeakCentroids = false;
                        break;
                    }
                }
            }
        }
        catch (BlibException e)
        {
            throw new BlibException(false,
                $"Error parsing spectrum XML from spectrum {specId}: {e.Message}");
        }
        catch (Exception e)
        {
            throw new BlibException(false,
                $"Unknown error while parsing spectrum file {specId}: {e.Message}");
        }

        sd.NumPeaks = mzs.Count;
        sd.Mzs = mzs.ToArray();
        sd.Intensities = intens.ToArray();
        Verbosity.Comment(VerbosityLevel.Detail,
            $"Done parsing spectrum XML from spectrum {specId}, {sd.NumPeaks} peaks found");
    }

    // --- PSM collection -----------------------------------------------------------------

    /// <summary>cpp parity: MSFReader.cpp:321.</summary>
    private void CollectPsms()
    {
        GetScoreInfo(out var sql, out var resultCount, out var scoreType,
                     out var pepConfidence, out var protConfidence);

        Verbosity.Status($"Parsing {resultCount.ToString(CultureInfo.InvariantCulture)} PSMs.");

        InitFileNameMap();
        // cpp parity: MSFReader.cpp:331 — `!versionLess(2, 2) || filtered_` chooses the
        // filtered-style mod tables.
        var modSet = new ModSet(_db!, !VersionLess(2, 2) || _filtered);
        var fileIdMap = GetFileIds();

        using var cmd = _db!.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (protConfidence > 0)
            {
                // cpp parity: MSFReader.cpp:337 — protein-confidence blob is 5N bytes; the
                // last byte of each 5 is a "used" flag, the first 4 are little-endian int.
                if (reader.IsDBNull(6)) continue;
                var blob = (byte[])reader.GetValue(6);
                if (blob.Length == 0) continue;
                if (blob.Length % 5 != 0)
                {
                    Verbosity.Error(
                        $"expected protein confidence to be multiple of 5 bytes but was {blob.Length}");
                    continue;
                }
                int maxConf = -1;
                for (int i = 0; i < blob.Length; i += 5)
                {
                    if (blob[i + 4] <= 0) continue;
                    int v = BitConverter.ToInt32(blob, i);
                    if (v > maxConf) maxConf = v;
                }
                if (maxConf < protConfidence) continue;
            }

            int peptideId = reader.GetInt32(0);
            int specRawId = reader.GetInt32(1);
            int workflowId = reader.GetInt32(4);
            string specId = UniqueSpecId(specRawId, workflowId);
            string sequence = reader.IsDBNull(2) ? string.Empty
                : (reader.GetValue(2)?.ToString() ?? string.Empty);
            double qvalue = pepConfidence <= 0
                ? (reader.IsDBNull(3) ? 0 : Convert.ToDouble(reader.GetValue(3), CultureInfo.InvariantCulture))
                : 0;
            int psmCharge = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture);

            if (!_spectra.TryGetValue(specId, out var sd))
            {
                Verbosity.Warn(
                    $"Peptide {peptideId.ToString(CultureInfo.InvariantCulture)} ({sequence}) "
                    + $"with score {qvalue.ToString(CultureInfo.InvariantCulture)} has a spectrum id "
                    + $"({specId}) not present in the spectrum map.");
                continue;
            }

            var psm = new PSM();
            if (sd.Charge > 0)
            {
                psm.Charge = sd.Charge;
            }
            else
            {
                // cpp parity: MSFReader.cpp:378 — convert the SpecData.mz (currently holding
                // the neutral mass) to actual m/z using the PSM's charge.
                psm.Charge = psmCharge;
                if (psmCharge > 0)
                    sd.Mz = (sd.Mz + AminoAcidMasses.ProtonMass * psmCharge) / psmCharge;
            }
            psm.UnmodSeq = sequence;
            var mods = VersionLess(2, 2) && !_filtered
                ? modSet.GetMods(peptideId)
                : modSet.GetMods(workflowId, peptideId);
            foreach (var m in mods)
                psm.Mods.Add(m);
            psm.SpecIndex = sd.Id;
            psm.SpecName = specId;
            psm.Score = qvalue;

            string psmFileName;
            if (!_filtered && VersionLess(2, 2))
            {
                if (!fileIdMap.TryGetValue(peptideId, out var fid))
                {
                    throw new BlibException(false,
                        $"No FileID for PSM {peptideId.ToString(CultureInfo.InvariantCulture)}.");
                }
                psmFileName = FileIdToName(fid);
                fileIdMap.Remove(peptideId);
            }
            else
            {
                var raw = reader.IsDBNull(5) ? string.Empty
                    : (reader.GetValue(5)?.ToString() ?? string.Empty);
                raw = raw.Replace('\\', '/');
                psmFileName = Path.GetFileName(raw);
            }

            // filename -> scoreType bucket.
            if (!_fileMap.TryGetValue(psmFileName, out var scoreMap))
            {
                scoreMap = new SortedDictionary<PsmScoreType, List<PSM>>();
                _fileMap[psmFileName] = scoreMap;
            }
            if (!scoreMap.TryGetValue(scoreType, out var bucket))
            {
                bucket = new List<PSM>();
                scoreMap[scoreType] = bucket;
            }
            bucket.Add(psm);
        }
    }

    // --- score / query selection --------------------------------------------------------

    /// <summary>
    /// cpp parity: MSFReader.cpp:429. Picks the SELECT statement + result count based on
    /// schema version, file type (msf vs pdResult), and which q-value column is present.
    /// </summary>
    private void GetScoreInfo(out string outSql, out int outResultCount,
                              out PsmScoreType outScoreType, out int outPepConfidence,
                              out int outProtConfidence)
    {
        outScoreType = PsmScoreType.PercolatorQValue;
        outPepConfidence = -1;
        outProtConfidence = -1;

        // Branch A: older (<2.2), unfiltered .msf.
        if (!_filtered && VersionLess(2, 2))
        {
            string stmtStr;
            string countStr;
            if (!HasQValues())
            {
                stmtStr = "SELECT PeptideID, SpectrumID, Sequence, '0' FROM Peptides";
                countStr = "Peptides";
            }
            else
            {
                stmtStr =
                    "SELECT Peptides.PeptideID, SpectrumID, Sequence, FieldValue, 0 "
                    + "FROM Peptides JOIN CustomDataPeptides ON Peptides.PeptideID = CustomDataPeptides.PeptideID "
                    + "WHERE FieldID IN (SELECT FieldID FROM CustomDataFields WHERE DisplayName IN ('q-Value', 'Percolator q-Value')) "
                    + "AND FieldValue <= " + GetScoreThreshold(BuildInput.Sqt).ToString("R", CultureInfo.InvariantCulture);
                countStr =
                    "Peptides JOIN CustomDataPeptides ON Peptides.PeptideID = CustomDataPeptides.PeptideID "
                    + "WHERE FieldID IN (SELECT FieldID FROM CustomDataFields WHERE DisplayName IN ('q-Value', 'Percolator q-Value')) "
                    + "AND FieldValue <= " + GetScoreThreshold(BuildInput.Sqt).ToString("R", CultureInfo.InvariantCulture);
            }
            // For this branch the cpp also doesn't have a SpectrumFileName / charge in the
            // SELECT; pad to the column indices the caller expects: PeptideID,SpectrumID,
            // Sequence,qvalue,workflowId,filename,protConf,charge. We use '' / 0 placeholders
            // so column accessors at indices 4-7 don't crash.
            stmtStr = stmtStr.Replace(
                ", '0' FROM Peptides", ", 0, '', 0, 0 FROM Peptides", StringComparison.Ordinal);
            stmtStr = stmtStr.Replace(
                ", FieldValue, 0 ", ", FieldValue, 0, '', 0, 0 ", StringComparison.Ordinal);
            outSql = stmtStr;
            outResultCount = GetRowCount(countStr);
            return;
        }

        // Branch B: filtered (.pdResult) OR newer (>=2.2) unfiltered (.msf).
        string pepPsmTable = string.Empty;
        if (TableExists(_db!, "TargetPeptideGroupsTargetPsms"))
            pepPsmTable = "TargetPeptideGroupsTargetPsms";
        else if (TableExists(_db!, "TargetPsmsTargetPeptideGroups"))
            pepPsmTable = "TargetPsmsTargetPeptideGroups";

        bool peptideGroups = false;
        bool proteins = false;
        const bool useProtConfidence = false; // cpp parity: MSFReader.cpp:470 — hardcoded false.
        if (ColumnExists(_db!, "TargetPeptideGroups", "Confidence") && !string.IsNullOrEmpty(pepPsmTable))
        {
            // cpp parity: MSFReader.cpp:474 — map cutoff to a TargetPeptideGroups.Confidence
            // level (3 = High <= 0.01, 2 = Medium <= 0.05).
            double threshold = GetScoreThreshold(BuildInput.Sqt);
            if (Math.Abs(threshold - 0.01) < 0.001) outPepConfidence = 3;
            else if (Math.Abs(threshold - 0.05) < 0.001) outPepConfidence = 2;
            if (useProtConfidence
                && ColumnExists(_db!, "TargetProteins", "ProteinFDRConfidence")
                && TableExists(_db!, "TargetPeptideGroupsTargetProteins"))
            {
                outProtConfidence = outPepConfidence;
            }
        }

        string filenameCol, filenameJoin;
        if (ColumnExists(_db!, "TargetPsms", "SpectrumFileId"))
        {
            filenameCol = "wf.FileName";
            filenameJoin = " JOIN WorkflowInputfiles wf ON psms.SpectrumFileId = wf.FileId";
        }
        else
        {
            filenameCol = "psms.SpectrumFileName";
            filenameJoin = string.Empty;
        }

        string qValueCol;
        string qValueWhere;
        if (!HasQValues())
        {
            qValueCol = "'0'";
            qValueWhere = string.Empty;
        }
        else
        {
            if (outPepConfidence > 0)
            {
                peptideGroups = true;
                qValueCol = "peps.Confidence";
                if (outProtConfidence > 0)
                    proteins = true;
            }
            else if (ColumnExists(_db!, "TargetPeptideGroups", "Qvalityqvalue"))
            {
                peptideGroups = true;
                qValueCol = "peps.Qvalityqvalue";
            }
            else if (ColumnExists(_db!, "TargetPsms", "PercolatorqValue"))
            {
                qValueCol = "psms.PercolatorqValue";
            }
            else if (ColumnExists(_db!, "TargetPsms", "qValue"))
            {
                qValueCol = "psms.qValue";
            }
            else if (ColumnExists(_db!, "TargetPsms", "ExpectationValue"))
            {
                qValueCol = "psms.ExpectationValue";
                outScoreType = PsmScoreType.MascotIonsScore;
            }
            else
            {
                qValueCol = "'0'";
            }

            if (outPepConfidence <= 0)
            {
                qValueWhere = " WHERE " + qValueCol + " <= "
                    + GetScoreThreshold(BuildInput.Sqt).ToString("R", CultureInfo.InvariantCulture);
            }
            else
            {
                qValueWhere = " WHERE " + qValueCol + " >= "
                    + outPepConfidence.ToString(CultureInfo.InvariantCulture);
            }
        }

        string stmtSql =
            "SELECT psms.PeptideID, psm_spec.MSnSpectrumInfoSpectrumID, psms.Sequence, "
            + qValueCol + ", psms.WorkflowID, " + filenameCol
            + (outProtConfidence > 0 ? ", prots.ProteinFDRConfidence" : ", 0")
            + ", psms.Charge"
            + " FROM TargetPsms psms"
            + filenameJoin
            + " JOIN TargetPsmsMSnSpectrumInfo psm_spec ON psms.PeptideID = psm_spec.TargetPsmsPeptideID"
            + "   AND psm_spec.TargetPsmsWorkflowID = psms.WorkflowID";
        string countStrB =
            "TargetPsms psms"
            + " JOIN TargetPsmsMSnSpectrumInfo psm_spec ON psms.PeptideID = psm_spec.TargetPsmsPeptideID"
            + "   AND psm_spec.TargetPsmsWorkflowID = psms.WorkflowID";

        if (peptideGroups)
        {
            string joins =
                " JOIN " + pepPsmTable + " psm_pep ON psms.PeptideID = psm_pep.TargetPsmsPeptideID"
                + " JOIN TargetPeptideGroups peps ON psm_pep.TargetPeptideGroupsPeptideGroupID = peps.PeptideGroupID";
            if (proteins)
            {
                joins +=
                    " JOIN TargetPeptideGroupsTargetProteins pep_prot ON peps.PeptideGroupID = pep_prot.TargetPeptideGroupsPeptideGroupID"
                    + " JOIN TargetProteins prots ON pep_prot.TargetProteinsUniqueSequenceID = prots.UniqueSequenceID";
            }
            stmtSql += joins;
            countStrB += joins;
        }
        stmtSql += qValueWhere;
        countStrB += qValueWhere;
        outSql = stmtSql;
        outResultCount = GetRowCount(countStrB);
    }

    /// <summary>cpp parity: MSFReader.cpp:559.</summary>
    private void InitFileNameMap()
    {
        if (!VersionLess(2, 2))
            return; // info already in TargetPsms table

        string fileTable = _schemaVersionMajor < 2 ? "FileInfos" : "WorkflowInputFiles";
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = "SELECT FileID, FileName FROM " + fileTable;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int id = reader.GetInt32(0);
            string fn = reader.IsDBNull(1) ? string.Empty
                : (reader.GetValue(1)?.ToString() ?? string.Empty);
            _fileNameMap[id] = fn;
        }
    }

    /// <summary>cpp parity: MSFReader.cpp:604.</summary>
    private string FileIdToName(int fileId)
    {
        if (!_fileNameMap.TryGetValue(fileId, out var name))
            throw new BlibException(false, $"Invalid FileID: {fileId.ToString(CultureInfo.InvariantCulture)}.");
        return name;
    }

    /// <summary>cpp parity: MSFReader.cpp:615.</summary>
    private bool HasQValues()
    {
        if (_filtered || !VersionLess(2, 2))
        {
            return ColumnExists(_db!, "TargetPeptideGroups", "Qvalityqvalue")
                || ColumnExists(_db!, "TargetPsms", "PercolatorqValue")
                || ColumnExists(_db!, "TargetPsms", "qValue")
                || ColumnExists(_db!, "TargetPsms", "ExpectationValue");
        }
        if (!TableExists(_db!, "CustomDataFields"))
            return false;

        using var cmd = _db!.CreateCommand();
        cmd.CommandText =
            "SELECT FieldID FROM CustomDataFields "
            + "WHERE DisplayName IN ('q-Value', 'Percolator q-Value') LIMIT 1";
        using var reader = cmd.ExecuteReader();
        return reader.Read();
    }

    /// <summary>cpp parity: MSFReader.cpp:739.</summary>
    private Dictionary<int, int> GetFileIds()
    {
        var map = new Dictionary<int, int>();
        if (_filtered || !VersionLess(2, 2))
            return map;

        using var cmd = _db!.CreateCommand();
        cmd.CommandText =
            "SELECT PeptideID, FileID "
            + "FROM Peptides "
            + "JOIN SpectrumHeaders ON Peptides.SpectrumID = SpectrumHeaders.SpectrumID "
            + "JOIN MassPeaks ON SpectrumHeaders.MassPeakID = MassPeaks.MassPeakID";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int pid = reader.GetInt32(0);
            int fid = reader.GetInt32(1);
            map[pid] = fid;
        }
        return map;
    }

    // --- helpers ------------------------------------------------------------------------

    /// <summary>cpp parity: MSFReader.cpp:902.</summary>
    private int GetRowCount(string table)
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM " + table;
        var result = cmd.ExecuteScalar();
        return result is null ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    /// <summary>cpp parity: MSFReader.cpp:913.</summary>
    private static bool TableExists(SQLiteConnection db, string table)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '"
            + SqliteRoutine.EscapeApostrophes(table) + "'";
        var v = cmd.ExecuteScalar();
        return v != null && Convert.ToInt32(v, CultureInfo.InvariantCulture) == 1;
    }

    /// <summary>cpp parity: MSFReader.cpp:920.</summary>
    private static bool ColumnExists(SQLiteConnection db, string table, string columnName)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(" + table + ")";
        using var reader = cmd.ExecuteReader();
        // Find the 'name' column index (case-insensitive).
        int nameIdx = -1;
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), "name", StringComparison.OrdinalIgnoreCase))
            {
                nameIdx = i;
                break;
            }
        }
        if (nameIdx < 0) return false;

        while (reader.Read())
        {
            var v = reader.IsDBNull(nameIdx) ? string.Empty
                : (reader.GetValue(nameIdx)?.ToString() ?? string.Empty);
            if (string.Equals(v, columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // --- inner: ModSet ------------------------------------------------------------------

    /// <summary>
    /// In-memory cache of (workflowId, peptideId) -> mods. Populated once at parse time.
    /// cpp parity: MSFReader.h:83 inner class.
    /// </summary>
    private sealed class ModSet
    {
        // workflowId -> peptideId -> mods. Same shape as cpp mods_.
        private readonly Dictionary<int, Dictionary<int, List<SeqMod>>> _mods = new();
        private static readonly List<SeqMod> _empty = new();

        public ModSet(SQLiteConnection db, bool filtered)
        {
            string sql;
            if (!filtered)
            {
                sql =
                    "SELECT '0', PeptideID, Position, DeltaMass "
                    + "FROM PeptidesAminoAcidModifications "
                    + "JOIN AminoAcidModifications "
                    + "   ON PeptidesAminoAcidModifications.AminoAcidModificationID = AminoAcidModifications.AminoAcidModificationID";
            }
            else
            {
                bool alt = TableExists(db, "FoundModificationsTargetPsms");
                string tName = !alt ? "TargetPsmsFoundModifications" : "FoundModificationsTargetPsms";
                sql =
                    "SELECT TargetPsmsWorkflowID, TargetPsmsPeptideID, Position, DeltaMonoisotopicMass "
                    + "FROM " + tName + " "
                    + "JOIN FoundModifications "
                    + "   ON " + tName + ".FoundModificationsModificationID = FoundModifications.ModificationID";
            }

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = sql;
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int workflowId = reader.IsDBNull(0) ? 0
                        : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
                    int peptideId = reader.GetInt32(1);
                    int pos = reader.GetInt32(2);
                    // cpp parity: MSFReader.cpp:661 — mod indices are 0-based in unfiltered,
                    // 1-based in filtered. Bump by 1 for unfiltered to match library convention.
                    if (!filtered) pos++;
                    double mass = reader.IsDBNull(3) ? 0 : reader.GetDouble(3);
                    AddMod(workflowId, peptideId, pos, mass);
                }
            }

            // cpp parity: MSFReader.cpp:670 — PeptidesTerminalModifications (legacy table).
            if (TableExists(db, "PeptidesTerminalModifications"))
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText =
                    "SELECT PeptidesTerminalModifications.PeptideID, PositionType, DeltaMass, Sequence "
                    + "FROM PeptidesTerminalModifications "
                    + "JOIN Peptides ON PeptidesTerminalModifications.PeptideID = Peptides.PeptideID "
                    + "JOIN AminoAcidModifications ON TerminalModificationID = AminoAcidModificationID";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int peptideId = reader.GetInt32(0);
                    int positionType = reader.GetInt32(1);
                    int position;
                    switch (positionType)
                    {
                        case 1:
                        case 3:
                            position = 1;
                            break;
                        case 2:
                        case 4:
                            var seq = reader.IsDBNull(3) ? string.Empty
                                : (reader.GetValue(3)?.ToString() ?? string.Empty);
                            position = seq.Length;
                            break;
                        default:
                            throw new BlibException(false,
                                $"Unknown position type in PeptideAminoAcidModifications for PeptideID {peptideId.ToString(CultureInfo.InvariantCulture)}");
                    }
                    double mass = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                    AddMod(0, peptideId, position, mass);
                }
            }
        }

        public List<SeqMod> GetMods(int peptideId) => GetMods(0, peptideId);

        public List<SeqMod> GetMods(int workflowId, int peptideId)
        {
            if (_mods.TryGetValue(workflowId, out var inner)
                && inner.TryGetValue(peptideId, out var list))
            {
                return list;
            }
            return _empty;
        }

        private void AddMod(int workflowId, int peptideId, int position, double mass)
        {
            if (!_mods.TryGetValue(workflowId, out var inner))
            {
                inner = new Dictionary<int, List<SeqMod>>();
                _mods[workflowId] = inner;
            }
            if (!inner.TryGetValue(peptideId, out var list))
            {
                list = new List<SeqMod>();
                inner[peptideId] = list;
            }
            list.Add(new SeqMod(position, mass));
        }
    }

    // --- inner: spec file reader --------------------------------------------------------

    // cpp parity: MSFReader.cpp:957 — the reader doubles as the SpecFileReader. The C# port
    // uses an inner class that delegates to the parent's _spectra map; this keeps SpecReader's
    // disposal semantics straight (Dispose on the SpecReader handle doesn't free the parent).
    private sealed class MsfSpecFileReader : SpecFileReaderBase
    {
        private readonly MSFReader _parent;

        public MsfSpecFileReader(MSFReader parent)
        {
            _parent = parent;
        }

        public override void OpenFile(string path, bool mzSort = false)
        {
            // cpp parity: MSFReader.cpp:961 — empty body; spectra come from the same .msf db.
        }

        public override SpecIdType IdType
        {
            set { /* cpp parity: MSFReader.cpp:963 — no-op. */ }
        }

        public override bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true)
        {
            // cpp parity: MSFReader.cpp:969 — int-form is unsupported.
            Verbosity.Warn("MSFReader does not support spectrum access by integer identifier.");
            return false;
        }

        public override bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true)
        {
            ArgumentNullException.ThrowIfNull(returnData);
            if (!_parent._spectra.TryGetValue(identifier ?? string.Empty, out var found))
                return false;
            returnData.CopyFrom(found);
            return true;
        }

        public override bool GetNextSpectrum(SpecData returnData, bool getPeaks = true)
        {
            Verbosity.Warn("MSFReader does not support sequential file reading.");
            return false;
        }
    }
}
