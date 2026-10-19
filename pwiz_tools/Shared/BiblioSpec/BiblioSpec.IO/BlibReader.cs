/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace pwiz.BiblioSpec.IO
{
    /// <summary>
    /// Reads a BiblioSpec <c>.blib</c>: the format counterpart of <see cref="BlibWriter"/>,
    /// returning the records the writer takes, so a column is added in one place for both
    /// directions. It knows the file format and nothing of any tool's domain: Osprey makes its
    /// library entries from the records, Skyline its library keys, and each converts the rows
    /// itself.
    ///
    /// <para>The library's header (<see cref="Info"/>) and its small tables (source files,
    /// score types, proteins, metadata, decoy pairs) are read whole. The spectra and the
    /// retention times stream, each as a scan that can be split into partitions by id - the
    /// span of RefSpectra ids cut into equal ranges, <c>WHERE id BETWEEN low AND high</c>, which
    /// the rowid index serves, where a modulo partition is a full scan of the table per
    /// partition - so a caller can scan on several threads, one reader on its own connection
    /// per thread. A spectrum's peaks, modifications and annotations are also read singly by
    /// id, for a caller that keeps an index of the library and loads a spectrum when it is
    /// asked for.</para>
    ///
    /// <para>A library of an older schema reads through the same calls: a column or table the
    /// library lacks reads as the record's default, and a reader tests for a table
    /// (<see cref="HasTable"/>), never for a schema version. Ordinal reads throughout, with no
    /// object mapper: the dependencies stay System.Data.SQLite and Ionic.Zlib, since Osprey and
    /// CarafeSharp run on Linux.</para>
    /// </summary>
    public sealed class BlibReader : IDisposable
    {
        public const string TABLE_LIB_INFO = @"LibInfo";
        public const string TABLE_REF_SPECTRA = @"RefSpectra";
        public const string TABLE_REF_SPECTRA_PEAKS = @"RefSpectraPeaks";
        public const string TABLE_REF_SPECTRA_PEAK_ANNOTATIONS = @"RefSpectraPeakAnnotations";
        public const string TABLE_MODIFICATIONS = @"Modifications";
        public const string TABLE_PROTEINS = @"Proteins";
        public const string TABLE_REF_SPECTRA_PROTEINS = @"RefSpectraProteins";
        public const string TABLE_SPECTRUM_SOURCE_FILES = @"SpectrumSourceFiles";
        public const string TABLE_SCORE_TYPES = @"ScoreTypes";
        public const string TABLE_RETENTION_TIMES = @"RetentionTimes";

        private readonly SQLiteConnection _connection;
        private readonly bool _ownsConnection;
        // Which tables the library has, asked once each: a read-only library's tables do not
        // change, and the by-id reads ask per spectrum.
        private readonly Dictionary<string, bool> _tables = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private BlibLibraryInfo _info;
        private SpectrumColumns _spectrumColumns;
        private RetentionTimeColumns _retentionTimeColumns;
        // The lowest and highest RefSpectra id, read once for the partitioned scans; (1, 0) for
        // a library with no spectra, an empty range.
        private (long Low, long High)? _idSpan;

        /// <summary>Opens the library at <paramref name="path"/> read-only, on a connection of its own.</summary>
        public BlibReader(string path)
            : this(BlibSqlite.OpenReadOnly(path), true)
        {
        }

        /// <summary>Reads the library on <paramref name="connection"/>, which the caller opened and keeps; disposing the reader leaves it open.</summary>
        public BlibReader(SQLiteConnection connection)
            : this(connection, false)
        {
        }

        private BlibReader(SQLiteConnection connection, bool ownsConnection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _ownsConnection = ownsConnection;
        }

        /// <summary>The connection the library is read on, for a query of a tool's own tables.</summary>
        public SQLiteConnection Connection
        {
            get { return _connection; }
        }

        /// <summary>The <c>LibInfo</c> row, read on first use.</summary>
        public BlibLibraryInfo Info
        {
            get { return _info ?? (_info = ReadInfo()); }
        }

        public bool HasTable(string table)
        {
            if (!_tables.TryGetValue(table, out bool exists))
                _tables.Add(table, exists = BlibSqlite.TableExists(_connection, table));
            return exists;
        }

        /// <summary>The <c>RefSpectra</c> rows counted, for a library whose header does not say (<see cref="BlibLibraryInfo.SpectrumCount"/>).</summary>
        public int CountSpectra()
        {
            using (var command = new SQLiteCommand(@"SELECT COUNT(*) FROM RefSpectra", _connection))
                return Convert.ToInt32(command.ExecuteScalar());
        }

        /// <summary>
        /// The <c>RefSpectra</c> ids in ascending order: the order <see cref="ReadSpectra(bool,int,int)"/> reads
        /// the rows in, so a row's position in this list is its index in that order.
        /// </summary>
        public IReadOnlyList<long> ReadSpectrumIds()
        {
            var ids = new List<long>();
            using (var command = new SQLiteCommand(@"SELECT id FROM RefSpectra ORDER BY id", _connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    ids.Add(reader.GetInt64(0));
            }
            return ids;
        }

        /// <summary>The <c>SpectrumSourceFiles</c> rows in id order; empty in a library without the table.</summary>
        public IReadOnlyList<BlibSourceFile> ReadSourceFiles()
        {
            var files = new List<BlibSourceFile>();
            var columns = BlibSqlite.ColumnNames(_connection, TABLE_SPECTRUM_SOURCE_FILES);
            if (columns.Count == 0)
                return files;
            // The columns after fileName arrived with later schemas; one the library lacks reads as null or DDA.
            bool hasIdFile = columns.Contains(@"idFileName");
            bool hasCutoff = columns.Contains(@"cutoffScore");
            bool hasWorkflow = columns.Contains(@"workflowType");
            string sql = @"SELECT id, fileName" +
                         (hasIdFile ? @", idFileName" : string.Empty) +
                         (hasCutoff ? @", cutoffScore" : string.Empty) +
                         (hasWorkflow ? @", workflowType" : string.Empty) +
                         @" FROM SpectrumSourceFiles ORDER BY id";
            using (var command = new SQLiteCommand(sql, _connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    int next = 2;
                    string idFile = hasIdFile ? GetString(reader, next++) : null;
                    double? cutoff = hasCutoff ? GetDouble(reader, next++) : null;
                    int workflow = hasWorkflow ? GetInt32(reader, next) ?? BlibWriter.WORKFLOW_TYPE_DDA : BlibWriter.WORKFLOW_TYPE_DDA;
                    files.Add(new BlibSourceFile(reader.GetInt64(0), GetString(reader, 1), idFile, cutoff, workflow));
                }
            }
            return files;
        }

        /// <summary>
        /// The distinct <c>RefSpectra.scoreType</c> values, in ascending order: the score types
        /// the library's spectra actually carry, as against the <see cref="ReadScoreTypes"/> rows
        /// every library seeds. Empty in a library without the column.
        /// </summary>
        public IReadOnlyList<int> ReadScoreTypesInUse()
        {
            var scoreTypes = new List<int>();
            string column = SpectrumColumnsOfLibrary().ScoreTypeColumn;
            if (column == null)
                return scoreTypes;
            using (var command = new SQLiteCommand(@"SELECT DISTINCT " + column + @" FROM RefSpectra WHERE " + column +
                                                   @" IS NOT NULL ORDER BY 1", _connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    scoreTypes.Add(GetInt32(reader, 0) ?? BlibWriter.SCORE_TYPE_UNKNOWN);
            }
            return scoreTypes;
        }

        /// <summary>The <c>ScoreTypes</c> rows, name by id; empty in a library without the table.</summary>
        public IReadOnlyDictionary<int, string> ReadScoreTypes()
        {
            var scoreTypes = new Dictionary<int, string>();
            if (!HasTable(TABLE_SCORE_TYPES))
                return scoreTypes;
            using (var command = new SQLiteCommand(@"SELECT id, scoreType FROM ScoreTypes", _connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    scoreTypes[reader.GetInt32(0)] = GetString(reader, 1);
            }
            return scoreTypes;
        }

        /// <summary>
        /// The <c>LibMetadata</c> rows (<see cref="BlibMetadata"/>), value by key, or null in a
        /// library without the table; the legacy table is read where the current one is absent.
        /// </summary>
        public IReadOnlyDictionary<string, string> ReadMetadata()
        {
            string table = HasTable(BlibMetadata.TABLE) ? BlibMetadata.TABLE
                : HasTable(BlibMetadata.LEGACY_TABLE) ? BlibMetadata.LEGACY_TABLE : null;
            if (table == null)
                return null;
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var command = new SQLiteCommand(@"SELECT key, value FROM " + table, _connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    metadata[reader.GetString(0)] = GetString(reader, 1);
            }
            return metadata;
        }

        /// <summary>
        /// The <c>DecoyPairs</c> rows by RefSpectra id, or null in a library without the table:
        /// one that pairs no decoys of its own. A table with other columns than
        /// <see cref="BlibSchema.CreateDecoyPairs"/> fails to read as the SQLite error it is.
        /// </summary>
        public IReadOnlyDictionary<long, BlibDecoyPair> ReadDecoyPairs()
        {
            if (!HasTable(BlibDecoyPair.TABLE))
                return null;
            var pairs = new Dictionary<long, BlibDecoyPair>();
            using (var command = new SQLiteCommand(@"SELECT RefSpectraID, IsDecoy, IsEntrapment, PairingID, TargetRefSpectraID, Method FROM DecoyPairs", _connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    pairs[reader.GetInt64(0)] = new BlibDecoyPair(reader.GetInt64(1) != 0, reader.GetInt64(2) != 0,
                        (int)reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetInt64(4), GetString(reader, 5));
                }
            }
            return pairs;
        }

        /// <summary>
        /// The protein accessions of every spectrum, by RefSpectra id and in the order the
        /// library maps them; empty in a library without the protein tables. A spectrum without
        /// a protein is absent. The accessions only; <see cref="ReadProteinTable"/> and
        /// <see cref="ReadProteinIds"/> read the rows with their optional values and the mapping
        /// by id, for a copy of the library that keeps them.
        /// </summary>
        public ILookup<long, string> ReadProteins()
        {
            var mappings = new List<KeyValuePair<long, string>>();
            if (HasTable(TABLE_REF_SPECTRA_PROTEINS) && HasTable(TABLE_PROTEINS))
            {
                using (var command = new SQLiteCommand(@"SELECT rsp.RefSpectraId, p.accession FROM RefSpectraProteins rsp " +
                                                       @"JOIN Proteins p ON rsp.ProteinId = p.id ORDER BY rsp.RefSpectraId", _connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(1))
                            mappings.Add(new KeyValuePair<long, string>(reader.GetInt64(0), reader.GetString(1)));
                    }
                }
            }
            return mappings.ToLookup(mapping => mapping.Key, mapping => mapping.Value);
        }

        /// <summary>
        /// The <c>Proteins</c> rows in id order, with the global q-value and PEP where the library
        /// has those columns; empty in a library without the table.
        /// </summary>
        public IReadOnlyList<BlibProtein> ReadProteinTable()
        {
            var proteins = new List<BlibProtein>();
            if (!HasTable(TABLE_PROTEINS))
                return proteins;
            string qValue = HasColumn(TABLE_PROTEINS, BlibSchema.ProteinQValue.Name) ? BlibSchema.ProteinQValue.Name : @"NULL";
            string pep = HasColumn(TABLE_PROTEINS, BlibSchema.ProteinPep.Name) ? BlibSchema.ProteinPep.Name : @"NULL";
            using (var command = new SQLiteCommand(@"SELECT id, accession, " + qValue + @", " + pep + @" FROM Proteins ORDER BY id", _connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    proteins.Add(new BlibProtein(reader.GetInt64(0), GetString(reader, 1), GetDouble(reader, 2), GetDouble(reader, 3)));
            }
            return proteins;
        }

        /// <summary>
        /// The <c>Proteins</c> ids of every spectrum, by RefSpectra id and in the order the
        /// library maps them (<c>RefSpectraProteins</c>); empty in a library without the table.
        /// </summary>
        public ILookup<long, long> ReadProteinIds()
        {
            var mappings = new List<KeyValuePair<long, long>>();
            if (HasTable(TABLE_REF_SPECTRA_PROTEINS))
            {
                using (var command = new SQLiteCommand(@"SELECT RefSpectraId, ProteinId FROM RefSpectraProteins ORDER BY RefSpectraId", _connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(1))
                            mappings.Add(new KeyValuePair<long, long>(reader.GetInt64(0), reader.GetInt64(1)));
                    }
                }
            }
            return mappings.ToLookup(mapping => mapping.Key, mapping => mapping.Value);
        }

        /// <summary>Whether the library's <paramref name="table"/> has <paramref name="column"/>; false for a table it lacks.</summary>
        public bool HasColumn(string table, string column)
        {
            return HasTable(table) && BlibSqlite.ColumnNames(_connection, table).Contains(column);
        }

        /// <summary>
        /// Whether the library records the <c>RefSpectra</c> column <paramref name="column"/>,
        /// under its current name or an earlier one the reader reads as it
        /// (<see cref="ReadSpectra(bool,int,int)"/>); a value the library does not record reads
        /// as the record's default.
        /// </summary>
        public bool HasSpectrumColumn(string column)
        {
            return SpectrumColumnsOfLibrary().HasNamed(column);
        }

        /// <summary>
        /// The <c>RefSpectra</c> rows in id order, each with <see cref="BlibSpectrumRecord.Id"/>
        /// set and, with <paramref name="withPeaks"/>, its peak blobs from
        /// <c>RefSpectraPeaks</c> (null blobs for a spectrum without a peaks row, or in a library
        /// without the table); without, the blobs are null and the scan touches no blob. The
        /// modifications, proteins and annotations are not filled in: <see cref="ReadProteins"/>
        /// reads them for every spectrum at once, <see cref="ReadModifications"/> and
        /// <see cref="ReadAnnotations"/> for one. With <paramref name="partitionCount"/> above
        /// one, only the rows whose id falls in partition <paramref name="partitionIndex"/> of
        /// the id span cut into that many equal ranges, so the partitions together are the whole
        /// table, each in id order and every id of one partition below every id of the next, and
        /// a caller can scan them on several threads, each with a reader of its own.
        /// </summary>
        public IEnumerable<BlibSpectrumRecord> ReadSpectra(bool withPeaks, int partitionCount = 1, int partitionIndex = 0)
        {
            return ReadSpectra(withPeaks, null, partitionCount, partitionIndex);
        }

        /// <summary>
        /// <see cref="ReadSpectra(bool,int,int)"/> narrowed to the rows <paramref name="filter"/>
        /// admits, in the SQL, so a search tool reads the candidates of one precursor m/z window
        /// without scanning the library. A null filter admits every row.
        /// </summary>
        public IEnumerable<BlibSpectrumRecord> ReadSpectra(bool withPeaks, BlibSpectrumFilter filter, int partitionCount = 1, int partitionIndex = 0)
        {
            ValidatePartition(partitionCount, partitionIndex);
            bool hasPeaksTable = HasTable(TABLE_REF_SPECTRA_PEAKS);
            bool requirePeaksRow = filter != null && filter.RequirePeaksRow;
            if (requirePeaksRow && !hasPeaksTable)
                return Array.Empty<BlibSpectrumRecord>();
            var where = new WhereClause();
            if (partitionCount > 1)
                where.AddPartition(@"r.id", IdSpan(), partitionCount, partitionIndex);
            AddFilter(where, filter);
            return EnumerateSpectra(SpectrumColumnsOfLibrary(), withPeaks && hasPeaksTable, requirePeaksRow, where);
        }

        /// <summary>
        /// The <c>RefSpectra</c> rows with the spectra of one precursor together, read as
        /// <see cref="ReadSpectra(bool,int,int)"/> reads them: ordered by modified sequence,
        /// small-molecule identity, charge and adduct, and within a precursor by the spectrum's
        /// own columns, so a consumer picking one spectrum per precursor (BlibFilter) walks the
        /// groups in one pass. Only spectra of at least <paramref name="minPeaks"/> peaks, except
        /// a precursor-only entry (an empty <c>SpecIDinFile</c>), which has no peaks to count.
        /// </summary>
        public IEnumerable<BlibSpectrumRecord> ReadSpectraByPrecursor(bool withPeaks, int minPeaks)
        {
            var columns = SpectrumColumnsOfLibrary();
            var where = new WhereClause();
            if (columns.NumPeaksColumn != null)
            {
                string condition = @"r." + columns.NumPeaksColumn + @" >= @minPeaks";
                if (columns.SpecIdInFileColumn != null)
                    condition = @"(" + condition + @" OR r." + columns.SpecIdInFileColumn + @" = '')";
                where.Add(condition, @"@minPeaks", minPeaks);
            }
            return EnumerateSpectra(columns, withPeaks && HasTable(TABLE_REF_SPECTRA_PEAKS), false, where, columns.PrecursorOrder(@"r."));
        }

        /// <summary>
        /// The <c>RefSpectra</c> row of id <paramref name="refId"/>, read as
        /// <see cref="ReadSpectra(bool,int,int)"/> reads a row, or null when the library has no
        /// such row.
        /// </summary>
        public BlibSpectrumRecord ReadSpectrum(long refId, bool withPeaks)
        {
            var where = new WhereClause();
            where.Add(@"r.id = @id", @"@id", refId);
            return EnumerateSpectra(SpectrumColumnsOfLibrary(), withPeaks && HasTable(TABLE_REF_SPECTRA_PEAKS), false, where).FirstOrDefault();
        }

        private SpectrumColumns SpectrumColumnsOfLibrary()
        {
            return _spectrumColumns ?? (_spectrumColumns = new SpectrumColumns(BlibSqlite.ColumnNames(_connection, TABLE_REF_SPECTRA)));
        }

        /// <summary>The conditions of <paramref name="filter"/>, each bound inclusive, on the <c>r</c> alias of <c>RefSpectra</c>.</summary>
        private static void AddFilter(WhereClause where, BlibSpectrumFilter filter)
        {
            if (filter == null)
                return;
            if (filter.MinPrecursorMz.HasValue)
                where.Add(@"r.precursorMZ >= @minMz", @"@minMz", filter.MinPrecursorMz.Value);
            if (filter.MaxPrecursorMz.HasValue)
                where.Add(@"r.precursorMZ <= @maxMz", @"@maxMz", filter.MaxPrecursorMz.Value);
            if (filter.MinPrecursorCharge.HasValue)
                where.Add(@"r.precursorCharge >= @minCharge", @"@minCharge", filter.MinPrecursorCharge.Value);
            if (filter.MaxPrecursorCharge.HasValue)
                where.Add(@"r.precursorCharge <= @maxCharge", @"@maxCharge", filter.MaxPrecursorCharge.Value);
            if (filter.MinNumPeaks.HasValue)
                where.Add(@"r.numPeaks >= @minPeaks", @"@minPeaks", filter.MinNumPeaks.Value);
        }

        /// <summary>
        /// The scan of <see cref="ReadSpectra(bool,int,int)"/>, deferred until enumerated; the
        /// arguments were checked by the caller. With <paramref name="requirePeaksRow"/> the
        /// join to the peaks table is inner, so a spectrum without a peaks row is not returned.
        /// </summary>
        private IEnumerable<BlibSpectrumRecord> EnumerateSpectra(SpectrumColumns columns, bool withPeaks, bool requirePeaksRow, WhereClause where,
            string orderBy = @"r.id")
        {
            var sql = new StringBuilder(@"SELECT ");
            sql.Append(columns.SelectList(@"r."));
            if (withPeaks)
                sql.Append(@", p.peakMZ, p.peakIntensity");
            sql.Append(@" FROM RefSpectra r");
            if (withPeaks || requirePeaksRow)
                sql.Append(requirePeaksRow ? @" JOIN" : @" LEFT JOIN").Append(@" RefSpectraPeaks p ON r.id = p.RefSpectraID");
            where.AppendTo(sql);
            sql.Append(@" ORDER BY ").Append(orderBy);
            using (var command = new SQLiteCommand(sql.ToString(), _connection))
            {
                where.Bind(command);
                using (var reader = command.ExecuteReader())
                {
                    int peakMz = columns.Count;
                    int peakIntensity = columns.Count + 1;
                    while (reader.Read())
                    {
                        var spectrum = columns.Read(reader);
                        if (withPeaks)
                        {
                            spectrum.MzBlob = GetBlob(reader, peakMz);
                            spectrum.IntensityBlob = GetBlob(reader, peakIntensity);
                        }
                        yield return spectrum;
                    }
                }
            }
        }

        /// <summary>
        /// The <c>RetentionTimes</c> rows, in RefSpectra id order so one spectrum's observations
        /// come together; empty in a library without the table (a redundant library). Partitioned
        /// by RefSpectra id as <see cref="ReadSpectra(bool,int,int)"/> is.
        /// </summary>
        public IEnumerable<BlibRetentionTimeRecord> ReadRetentionTimes(int partitionCount = 1, int partitionIndex = 0)
        {
            ValidatePartition(partitionCount, partitionIndex);
            var columns = _retentionTimeColumns ??
                          (_retentionTimeColumns = new RetentionTimeColumns(BlibSqlite.ColumnNames(_connection, TABLE_RETENTION_TIMES)));
            if (columns.Count == 0)
                return Array.Empty<BlibRetentionTimeRecord>();
            return EnumerateRetentionTimes(columns, partitionCount, partitionIndex);
        }

        /// <summary>The scan of <see cref="ReadRetentionTimes"/>, deferred until enumerated; the arguments were checked by the caller.</summary>
        private IEnumerable<BlibRetentionTimeRecord> EnumerateRetentionTimes(RetentionTimeColumns columns, int partitionCount, int partitionIndex)
        {
            var sql = new StringBuilder(@"SELECT ");
            sql.Append(columns.SelectList(string.Empty));
            sql.Append(@" FROM RetentionTimes");
            var where = new WhereClause();
            if (partitionCount > 1)
                where.AddPartition(@"RefSpectraID", IdSpan(), partitionCount, partitionIndex);
            where.AppendTo(sql);
            sql.Append(@" ORDER BY RefSpectraID");
            using (var command = new SQLiteCommand(sql.ToString(), _connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    yield return columns.Read(reader);
            }
        }

        /// <summary>
        /// The peaks of spectrum <paramref name="refId"/>, decoded from its
        /// <c>RefSpectraPeaks</c> row with <paramref name="numPeaks"/> (the spectrum's
        /// <c>RefSpectra.numPeaks</c>) peaks, or null for a spectrum without a peaks row, or in
        /// a library without the table.
        /// </summary>
        public BlibPeaks ReadPeaks(long refId, int numPeaks)
        {
            if (!HasTable(TABLE_REF_SPECTRA_PEAKS))
                return null;
            using (var command = new SQLiteCommand(@"SELECT peakMZ, peakIntensity FROM RefSpectraPeaks WHERE RefSpectraID = @id", _connection))
            {
                command.Parameters.AddWithValue(@"@id", refId);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;
                    return DecodePeaks(GetBlob(reader, 0), GetBlob(reader, 1), numPeaks);
                }
            }
        }

        /// <summary>The <c>Modifications</c> rows of spectrum <paramref name="refId"/>, in the order written; empty without the table.</summary>
        public IReadOnlyList<BlibModification> ReadModifications(long refId)
        {
            var modifications = new List<BlibModification>();
            if (!HasTable(TABLE_MODIFICATIONS))
                return modifications;
            using (var command = new SQLiteCommand(@"SELECT position, mass FROM Modifications WHERE RefSpectraID = @id ORDER BY id", _connection))
            {
                command.Parameters.AddWithValue(@"@id", refId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        modifications.Add(new BlibModification(reader.GetInt32(0), reader.GetDouble(1)));
                }
            }
            return modifications;
        }

        /// <summary>The <c>RefSpectraPeakAnnotations</c> rows of spectrum <paramref name="refId"/>, in the order written; empty without the table.</summary>
        public IReadOnlyList<BlibPeakAnnotation> ReadAnnotations(long refId)
        {
            var annotations = new List<BlibPeakAnnotation>();
            if (!HasTable(TABLE_REF_SPECTRA_PEAK_ANNOTATIONS))
                return annotations;
            using (var command = new SQLiteCommand(@"SELECT peakIndex, name, charge, mzTheoretical, mzObserved, formula, inchiKey, otherKeys, " +
                                                   @"adduct, comment FROM RefSpectraPeakAnnotations WHERE RefSpectraID = @id ORDER BY id", _connection))
            {
                command.Parameters.AddWithValue(@"@id", refId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        annotations.Add(new BlibPeakAnnotation(reader.GetInt32(0), GetString(reader, 1), GetInt32(reader, 2) ?? 0,
                            reader.GetDouble(3), reader.GetDouble(4), GetString(reader, 5), GetString(reader, 6), GetString(reader, 7),
                            GetString(reader, 8), GetString(reader, 9)));
                    }
                }
            }
            return annotations;
        }

        /// <summary>
        /// The peaks of a spectrum <see cref="ReadSpectra(bool,int,int)"/> read with its blobs, or null for a
        /// spectrum without a peaks row (a null <see cref="BlibSpectrumRecord.MzBlob"/>).
        /// </summary>
        public static BlibPeaks DecodePeaks(BlibSpectrumRecord spectrum)
        {
            if (spectrum.MzBlob == null || spectrum.IntensityBlob == null)
                return null;
            return DecodePeaks(spectrum.MzBlob, spectrum.IntensityBlob, spectrum.NumPeaks);
        }

        /// <summary>The peaks of a <c>RefSpectraPeaks</c> row of <paramref name="numPeaks"/> peaks, through the decoders on <see cref="BlibWriter"/>.</summary>
        public static BlibPeaks DecodePeaks(byte[] mzBlob, byte[] intensityBlob, int numPeaks)
        {
            if (mzBlob == null || intensityBlob == null)
                return null;
            return new BlibPeaks(BlibWriter.DecodeMzs(mzBlob, numPeaks), BlibWriter.DecodeIntensities(intensityBlob, numPeaks));
        }

        public void Dispose()
        {
            if (_ownsConnection)
                _connection.Dispose();
        }

        private BlibLibraryInfo ReadInfo()
        {
            using (var command = new SQLiteCommand(@"SELECT libLSID, createTime, numSpecs, majorVersion, minorVersion FROM LibInfo", _connection))
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read())
                    throw new InvalidDataException(@"The library has no LibInfo row");
                return new BlibLibraryInfo(GetString(reader, 0), GetString(reader, 1), GetInt32(reader, 2) ?? 0,
                    GetInt32(reader, 3) ?? 0, GetInt32(reader, 4) ?? 0);
            }
        }

        private static void ValidatePartition(int partitionCount, int partitionIndex)
        {
            if (partitionCount < 1)
                throw new ArgumentOutOfRangeException(nameof(partitionCount));
            if (partitionIndex < 0 || partitionIndex >= partitionCount)
                throw new ArgumentOutOfRangeException(nameof(partitionIndex));
        }

        /// <summary>The lowest and highest RefSpectra id, read once; (1, 0) when the table is empty.</summary>
        private (long Low, long High) IdSpan()
        {
            if (_idSpan.HasValue)
                return _idSpan.Value;
            using (var command = new SQLiteCommand(@"SELECT MIN(id), MAX(id) FROM RefSpectra", _connection))
            using (var reader = command.ExecuteReader())
            {
                _idSpan = reader.Read() && !reader.IsDBNull(0)
                    ? (reader.GetInt64(0), reader.GetInt64(1))
                    : (1L, 0L);
            }
            return _idSpan.Value;
        }

        private static string GetString(SQLiteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        private static double? GetDouble(SQLiteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
        }

        private static int? GetInt32(SQLiteDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
                return null;
            object value = reader.GetValue(ordinal);
            // System.Data.SQLite surfaces a TINYINT as an unsigned byte, so the -1 BiblioSpec's
            // tools have written for an unknown workflow or score type would read as 255.
            if (value is byte b)
                return (sbyte)b;
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static long? GetInt64(SQLiteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
        }

        private static byte[] GetBlob(SQLiteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : (byte[])reader.GetValue(ordinal);
        }

        /// <summary>
        /// The conditions of a scan, joined by AND, with the values they compare to bound as
        /// parameters of the command (new parameter objects per command, since a deferred scan
        /// may be enumerated more than once); no conditions, no WHERE.
        /// </summary>
        private sealed class WhereClause
        {
            private readonly List<string> _conditions = new List<string>();
            private readonly List<KeyValuePair<string, object>> _parameters = new List<KeyValuePair<string, object>>();

            public void Add(string condition)
            {
                _conditions.Add(condition);
            }

            public void Add(string condition, string parameterName, object value)
            {
                _conditions.Add(condition);
                _parameters.Add(new KeyValuePair<string, object>(parameterName, value));
            }

            /// <summary>
            /// The rows whose <paramref name="idColumn"/> falls in partition <paramref name="partitionIndex"/>
            /// of the id <paramref name="span"/> cut into <paramref name="partitionCount"/> equal ranges: a
            /// range the rowid index serves, so a thread reads its own rows and no other's.
            /// </summary>
            public void AddPartition(string idColumn, (long Low, long High) span, int partitionCount, int partitionIndex)
            {
                long count = span.High - span.Low + 1;
                long from = span.Low + count * partitionIndex / partitionCount;
                long to = span.Low + count * (partitionIndex + 1) / partitionCount - 1;
                Add(string.Format(CultureInfo.InvariantCulture, @"{0} BETWEEN {1} AND {2}", idColumn, from, to));
            }

            public void AppendTo(StringBuilder sql)
            {
                if (_conditions.Count > 0)
                    sql.Append(@" WHERE ").Append(string.Join(@" AND ", _conditions));
            }

            public void Bind(SQLiteCommand command)
            {
                foreach (var parameter in _parameters)
                    command.Parameters.AddWithValue(parameter.Key, parameter.Value);
            }
        }

        /// <summary>
        /// The columns of a table the reader knows, with the ordinal each has in a SELECT of
        /// those the library has, or -1 for a column the library lacks, which reads as the
        /// record's default. A column a library of an older schema holds under an earlier name
        /// is read from that name (<see cref="Source"/>), as the current column.
        /// </summary>
        private abstract class KnownColumns
        {
            private readonly string[] _names;
            private readonly string[] _sources;
            private readonly int[] _ordinals;

            protected KnownColumns(string[] names, HashSet<string> present, IReadOnlyDictionary<string, string[]> legacyNames = null)
            {
                _names = names;
                _sources = new string[names.Length];
                _ordinals = new int[names.Length];
                Count = 0;
                for (int i = 0; i < names.Length; i++)
                {
                    _sources[i] = SourceOf(names[i], present, legacyNames);
                    _ordinals[i] = _sources[i] != null ? Count++ : -1;
                }
            }

            /// <summary>The columns the library has, which the SELECT lists.</summary>
            public int Count { get; }

            /// <summary>
            /// The comma-separated columns the library has, each under <paramref name="prefix"/>
            /// (a table alias with its dot); one read from an earlier name is aliased to its current one.
            /// </summary>
            public string SelectList(string prefix)
            {
                var list = new StringBuilder();
                for (int i = 0; i < _names.Length; i++)
                {
                    if (_ordinals[i] < 0)
                        continue;
                    if (list.Length > 0)
                        list.Append(@", ");
                    list.Append(prefix).Append(_sources[i]);
                    if (_sources[i] != _names[i])
                        list.Append(@" AS ").Append(_names[i]);
                }
                return list.ToString();
            }

            protected int Ordinal(int column)
            {
                return _ordinals[column];
            }

            protected bool Has(int column)
            {
                return _ordinals[column] >= 0;
            }

            /// <summary>Whether the library has the column of current <paramref name="name"/>, under that name or an earlier one.</summary>
            public bool HasNamed(string name)
            {
                int column = Array.IndexOf(_names, name);
                return column >= 0 && Has(column);
            }

            /// <summary>The library's column read for <paramref name="column"/>: its current name, an earlier one, or null when the library lacks it.</summary>
            protected string Source(int column)
            {
                return _sources[column];
            }

            /// <summary>Whether <paramref name="column"/> is read from an earlier name.</summary>
            protected bool FromLegacyName(int column)
            {
                return _sources[column] != null && _sources[column] != _names[column];
            }

            private static string SourceOf(string name, HashSet<string> present, IReadOnlyDictionary<string, string[]> legacyNames)
            {
                if (present.Contains(name))
                    return name;
                if (legacyNames != null && legacyNames.TryGetValue(name, out var earlier))
                {
                    foreach (string legacy in earlier)
                    {
                        if (present.Contains(legacy))
                            return legacy;
                    }
                }
                return null;
            }

            protected string Text(SQLiteDataReader reader, int column)
            {
                return GetString(reader, _ordinals[column]);
            }

            protected double? Real(SQLiteDataReader reader, int column)
            {
                return GetDouble(reader, _ordinals[column]);
            }

            protected int? Integer(SQLiteDataReader reader, int column)
            {
                return GetInt32(reader, _ordinals[column]);
            }

            protected long? Integer64(SQLiteDataReader reader, int column)
            {
                return GetInt64(reader, _ordinals[column]);
            }
        }

        /// <summary>
        /// The <c>RefSpectra</c> columns, in the order <see cref="BlibSchema.CreateRefSpectra"/>
        /// declares them. A NULL in a column the library has reads as null, or as 0 in a column
        /// the record holds as a plain number. The ion mobility columns of the schemas before
        /// <see cref="BlibSchema.MinVersionImsUnits"/> (<c>driftTimeMsec</c> and
        /// <c>ionMobilityValue</c>, <c>driftTimeHighEnergyOffsetMsec</c> and
        /// <c>ionMobilityHighEnergyDriftTimeOffsetMsec</c>) read as the current ones, in drift
        /// time units where the library has no <c>ionMobilityType</c> to say.
        /// </summary>
        private sealed class SpectrumColumns : KnownColumns
        {
            private static readonly IReadOnlyDictionary<string, string[]> LEGACY_NAMES = new Dictionary<string, string[]>
            {
                { @"ionMobility", new[] { @"driftTimeMsec", @"ionMobilityValue" } },
                { @"ionMobilityHighEnergyOffset", new[] { @"driftTimeHighEnergyOffsetMsec", @"ionMobilityHighEnergyDriftTimeOffsetMsec" } },
            };

            private const int ID = 0, PEPTIDE_SEQ = 1, PRECURSOR_MZ = 2, PRECURSOR_CHARGE = 3, PEPTIDE_MOD_SEQ = 4, PREV_AA = 5, NEXT_AA = 6,
                COPIES = 7, NUM_PEAKS = 8, ION_MOBILITY = 9, CCS = 10, ION_MOBILITY_HIGH_ENERGY_OFFSET = 11, ION_MOBILITY_TYPE = 12,
                RETENTION_TIME = 13, START_TIME = 14, END_TIME = 15, TOTAL_ION_CURRENT = 16, MOLECULE_NAME = 17, CHEMICAL_FORMULA = 18,
                PRECURSOR_ADDUCT = 19, INCHI_KEY = 20, OTHER_KEYS = 21, FILE_ID = 22, SPEC_ID_IN_FILE = 23, SCORE = 24, SCORE_TYPE = 25,
                PRECURSOR_Q_VALUE = 26, PRECURSOR_PEP = 27, PEPTIDE_Q_VALUE = 28, PEPTIDE_PEP = 29;

            // The standard columns, then the optional ones (BlibSchema.OptionalRefSpectraColumns).
            private static readonly string[] NAMES =
            {
                @"id", @"peptideSeq", @"precursorMZ", @"precursorCharge", @"peptideModSeq", @"prevAA", @"nextAA", @"copies", @"numPeaks",
                @"ionMobility", @"collisionalCrossSectionSqA", @"ionMobilityHighEnergyOffset", @"ionMobilityType", @"retentionTime",
                @"startTime", @"endTime", @"totalIonCurrent", @"moleculeName", @"chemicalFormula", @"precursorAdduct", @"inchiKey",
                @"otherKeys", @"fileID", @"SpecIDinFile", @"score", @"scoreType",
                BlibSchema.PrecursorQValue.Name, BlibSchema.PrecursorPep.Name, BlibSchema.PeptideQValue.Name, BlibSchema.PeptidePep.Name,
            };

            public SpectrumColumns(HashSet<string> present)
                : base(NAMES, present, LEGACY_NAMES)
            {
                if (!Has(ID))
                    throw new InvalidDataException(@"The library has no RefSpectra table");
            }

            /// <summary>
            /// The ORDER BY that brings the spectra of one precursor together, for a scan that
            /// groups them: the modified sequence, then the small-molecule identity where the
            /// library has those columns, then the charge and adduct; within a precursor, the
            /// spectrum's own columns in a fixed order, so the order of a group's members is a
            /// property of their values and not of the file. BlibFilter has always sorted this way.
            /// </summary>
            public string PrecursorOrder(string prefix)
            {
                var columns = new List<int>();
                columns.Add(PEPTIDE_MOD_SEQ);
                bool smallMolecules = Has(INCHI_KEY);
                if (smallMolecules)
                    columns.AddRange(new[] { MOLECULE_NAME, CHEMICAL_FORMULA, INCHI_KEY, OTHER_KEYS });
                columns.Add(PRECURSOR_CHARGE);
                if (smallMolecules)
                    columns.Add(PRECURSOR_ADDUCT);
                columns.AddRange(new[]
                {
                    SPEC_ID_IN_FILE, RETENTION_TIME, ION_MOBILITY, CCS, ION_MOBILITY_HIGH_ENERGY_OFFSET, START_TIME, END_TIME,
                    TOTAL_ION_CURRENT,
                });
                var order = new StringBuilder();
                foreach (int column in columns)
                {
                    if (!Has(column))
                        continue;
                    if (order.Length > 0)
                        order.Append(@", ");
                    order.Append(prefix).Append(Source(column));
                }
                return order.ToString();
            }

            /// <summary>The library's name for <c>SpecIDinFile</c>, or null without the column.</summary>
            public string SpecIdInFileColumn
            {
                get { return Source(SPEC_ID_IN_FILE); }
            }

            /// <summary>The library's name for <c>numPeaks</c>, or null without the column.</summary>
            public string NumPeaksColumn
            {
                get { return Source(NUM_PEAKS); }
            }

            /// <summary>The library's name for <c>scoreType</c>, or null without the column.</summary>
            public string ScoreTypeColumn
            {
                get { return Source(SCORE_TYPE); }
            }

            public BlibSpectrumRecord Read(SQLiteDataReader reader)
            {
                var spectrum = new BlibSpectrumRecord { Id = reader.GetInt64(Ordinal(ID)) };
                if (Has(PEPTIDE_SEQ))
                    spectrum.PeptideSeq = Text(reader, PEPTIDE_SEQ);
                if (Has(PRECURSOR_MZ))
                    spectrum.PrecursorMz = Real(reader, PRECURSOR_MZ) ?? 0;
                if (Has(PRECURSOR_CHARGE))
                    spectrum.PrecursorCharge = Integer(reader, PRECURSOR_CHARGE) ?? 0;
                if (Has(PEPTIDE_MOD_SEQ))
                    spectrum.PeptideModSeq = Text(reader, PEPTIDE_MOD_SEQ);
                if (Has(PREV_AA))
                    spectrum.PrevAA = Text(reader, PREV_AA);
                if (Has(NEXT_AA))
                    spectrum.NextAA = Text(reader, NEXT_AA);
                if (Has(COPIES))
                    spectrum.Copies = Integer(reader, COPIES) ?? 0;
                if (Has(NUM_PEAKS))
                    spectrum.NumPeaks = Integer(reader, NUM_PEAKS) ?? 0;
                if (Has(ION_MOBILITY))
                    spectrum.IonMobility = Real(reader, ION_MOBILITY);
                if (Has(CCS))
                    spectrum.CollisionalCrossSectionSqA = Real(reader, CCS);
                if (Has(ION_MOBILITY_HIGH_ENERGY_OFFSET))
                    spectrum.IonMobilityHighEnergyOffset = Real(reader, ION_MOBILITY_HIGH_ENERGY_OFFSET);
                if (Has(ION_MOBILITY_TYPE))
                    spectrum.IonMobilityType = Integer(reader, ION_MOBILITY_TYPE) ?? BlibWriter.ION_MOBILITY_TYPE_NONE;
                else if (FromLegacyName(ION_MOBILITY) && spectrum.IonMobility.HasValue)
                    spectrum.IonMobilityType = BlibWriter.ION_MOBILITY_TYPE_DRIFT_TIME;
                // A library of schema 3 held a drift time or a CCS in ionMobilityValue, told apart by
                // its ionMobilityType; only the drift time (1) is an ion mobility, as BlibFilter has
                // always read it, and the type codes of that schema are not the current ones.
                if (Source(ION_MOBILITY) == @"ionMobilityValue" && spectrum.IonMobilityType != BlibWriter.ION_MOBILITY_TYPE_DRIFT_TIME)
                {
                    spectrum.IonMobility = null;
                    spectrum.IonMobilityType = BlibWriter.ION_MOBILITY_TYPE_NONE;
                }
                if (Has(RETENTION_TIME))
                    spectrum.RetentionTime = Real(reader, RETENTION_TIME);
                if (Has(START_TIME))
                    spectrum.StartTime = Real(reader, START_TIME);
                if (Has(END_TIME))
                    spectrum.EndTime = Real(reader, END_TIME);
                if (Has(TOTAL_ION_CURRENT))
                    spectrum.TotalIonCurrent = Real(reader, TOTAL_ION_CURRENT);
                if (Has(MOLECULE_NAME))
                    spectrum.MoleculeName = Text(reader, MOLECULE_NAME);
                if (Has(CHEMICAL_FORMULA))
                    spectrum.ChemicalFormula = Text(reader, CHEMICAL_FORMULA);
                if (Has(PRECURSOR_ADDUCT))
                    spectrum.PrecursorAdduct = Text(reader, PRECURSOR_ADDUCT);
                if (Has(INCHI_KEY))
                    spectrum.InchiKey = Text(reader, INCHI_KEY);
                if (Has(OTHER_KEYS))
                    spectrum.OtherKeys = Text(reader, OTHER_KEYS);
                if (Has(FILE_ID))
                    spectrum.FileId = Integer64(reader, FILE_ID) ?? 0;
                if (Has(SPEC_ID_IN_FILE))
                    spectrum.SpecIdInFile = Text(reader, SPEC_ID_IN_FILE);
                if (Has(SCORE))
                    spectrum.Score = Real(reader, SCORE) ?? 0;
                if (Has(SCORE_TYPE))
                    spectrum.ScoreType = Integer(reader, SCORE_TYPE) ?? BlibWriter.SCORE_TYPE_UNKNOWN;
                if (Has(PRECURSOR_Q_VALUE))
                    spectrum.PrecursorQValue = Real(reader, PRECURSOR_Q_VALUE);
                if (Has(PRECURSOR_PEP))
                    spectrum.PrecursorPep = Real(reader, PRECURSOR_PEP);
                if (Has(PEPTIDE_Q_VALUE))
                    spectrum.PeptideQValue = Real(reader, PEPTIDE_Q_VALUE);
                if (Has(PEPTIDE_PEP))
                    spectrum.PeptidePep = Real(reader, PEPTIDE_PEP);
                return spectrum;
            }
        }

        /// <summary>The <c>RetentionTimes</c> columns of <see cref="BlibSchema.CreateRetentionTimes"/>.</summary>
        private sealed class RetentionTimeColumns : KnownColumns
        {
            private const int REF_SPECTRA_ID = 0, REDUNDANT_REF_SPECTRA_ID = 1, SPECTRUM_SOURCE_ID = 2, ION_MOBILITY = 3, CCS = 4,
                ION_MOBILITY_HIGH_ENERGY_OFFSET = 5, ION_MOBILITY_TYPE = 6, RETENTION_TIME = 7, START_TIME = 8, END_TIME = 9, SCORE = 10,
                BEST_SPECTRUM = 11;

            private static readonly string[] NAMES =
            {
                @"RefSpectraID", @"RedundantRefSpectraID", @"SpectrumSourceID", @"ionMobility", @"collisionalCrossSectionSqA",
                @"ionMobilityHighEnergyOffset", @"ionMobilityType", @"retentionTime", @"startTime", @"endTime", @"score", @"bestSpectrum",
            };

            public RetentionTimeColumns(HashSet<string> present)
                : base(NAMES, present)
            {
            }

            public BlibRetentionTimeRecord Read(SQLiteDataReader reader)
            {
                var row = new BlibRetentionTimeRecord { RefSpectraId = Integer64(reader, REF_SPECTRA_ID) ?? 0 };
                if (Has(REDUNDANT_REF_SPECTRA_ID))
                    row.RedundantRefSpectraId = Integer64(reader, REDUNDANT_REF_SPECTRA_ID) ?? 0;
                if (Has(SPECTRUM_SOURCE_ID))
                    row.SpectrumSourceId = Integer64(reader, SPECTRUM_SOURCE_ID);
                if (Has(ION_MOBILITY))
                    row.IonMobility = Real(reader, ION_MOBILITY);
                if (Has(CCS))
                    row.CollisionalCrossSectionSqA = Real(reader, CCS);
                if (Has(ION_MOBILITY_HIGH_ENERGY_OFFSET))
                    row.IonMobilityHighEnergyOffset = Real(reader, ION_MOBILITY_HIGH_ENERGY_OFFSET);
                if (Has(ION_MOBILITY_TYPE))
                    row.IonMobilityType = Integer(reader, ION_MOBILITY_TYPE) ?? BlibWriter.ION_MOBILITY_TYPE_NONE;
                if (Has(RETENTION_TIME))
                    row.RetentionTime = Real(reader, RETENTION_TIME);
                if (Has(START_TIME))
                    row.StartTime = Real(reader, START_TIME);
                if (Has(END_TIME))
                    row.EndTime = Real(reader, END_TIME);
                if (Has(SCORE))
                    row.Score = Real(reader, SCORE);
                if (Has(BEST_SPECTRUM))
                    row.BestSpectrum = (Integer(reader, BEST_SPECTRUM) ?? 0) != 0;
                return row;
            }
        }
    }

    /// <summary>
    /// The <c>RefSpectra</c> rows a scan returns
    /// (<see cref="BlibReader.ReadSpectra(bool,BlibSpectrumFilter,int,int)"/>): each bound
    /// that is set narrows the scan in its SQL, inclusively, and one left null does not apply.
    /// </summary>
    public sealed class BlibSpectrumFilter
    {
        public double? MinPrecursorMz { get; set; }
        public double? MaxPrecursorMz { get; set; }
        public int? MinPrecursorCharge { get; set; }
        public int? MaxPrecursorCharge { get; set; }
        /// <summary>The fewest peaks (<c>RefSpectra.numPeaks</c>) a row may have.</summary>
        public int? MinNumPeaks { get; set; }
        /// <summary>
        /// Only rows with a <c>RefSpectraPeaks</c> row, the join becoming inner, as BiblioSpec's
        /// own tools select a library's spectra; none in a library without the table.
        /// </summary>
        public bool RequirePeaksRow { get; set; }
    }
}
