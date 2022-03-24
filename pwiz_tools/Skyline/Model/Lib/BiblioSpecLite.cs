/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.BiblioSpec;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Database;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    [XmlRoot("bibliospec_lite_spec")]
    public sealed class BiblioSpecLiteSpec : LibrarySpec
    {
        public const string EXT = ".blib";
        public static string DotConvertedToSmallMolecules = @".converted_to_small_molecules";

        public static string FILTER_BLIB
        {
            get { return TextUtil.FileDialogFilter(Resources.BiblioSpecLiteSpec_FILTER_BLIB_BiblioSpec_Library, EXT); }
        }

        public const string EXT_REDUNDANT = ".redundant.blib";
        public const string ASSAY_NAME = "-assay";

        public static string GetLibraryFileName(string documentPath)
        {
            return Path.ChangeExtension(documentPath, EXT);
        }

        public static BiblioSpecLiteSpec GetDocumentLibrarySpec(string documentPath)
        {
            var spec = new BiblioSpecLiteSpec(Path.GetFileNameWithoutExtension(documentPath), GetLibraryFileName(documentPath));
            return (BiblioSpecLiteSpec) spec.ChangeDocumentLibrary(true);
        }

        public static string GetRedundantName(string libraryPath)
        {
            return Path.ChangeExtension(libraryPath, EXT_REDUNDANT);
        }

        private static readonly PeptideRankId[] RANK_IDS = { PEP_RANK_COPIES, PEP_RANK_PICKED_INTENSITY };

        public BiblioSpecLiteSpec(string name, string path)
            : base(name, path)
        {
        }

        public override string Filter
        {
            get { return FILTER_BLIB; }
        }

        public override Library LoadLibrary(ILoadMonitor loader)
        {
            return BiblioSpecLiteLibrary.Load(this, loader);
        }

        public override IEnumerable<PeptideRankId> PeptideRankIds
        {
            get { return RANK_IDS; }
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        private BiblioSpecLiteSpec()
        {
        }

        public static BiblioSpecLiteSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new BiblioSpecLiteSpec());
        }

        #endregion
    }

    [XmlRoot("bibliospec_lite_library")]
    public sealed class BiblioSpecLiteLibrary : CachedLibrary<BiblioLiteSpectrumInfo>
    {
        private const int FORMAT_VERSION_CACHE = 19;
        // V19 add protein/MoleculeGroupName
        // V18 crosslinks
        // V17 add ID file (alongside already-cached spectrum source file)
        // V16 scores and score types
        // V15 add score to peak boundaries
        // V14 adds peak annotations
        // V13 adds variable precision modifications
        // v12 adds small molecule support
        // v11 startTime and endTime in RetentionTimes table
        // v10 changes ion mobility encoding

        public const string DEFAULT_AUTHORITY = "proteome.gs.washington.edu";

        public const string EXT_CACHE = ".slc";

        private PooledSqliteConnection _sqliteConnection;
        private PooledSqliteConnection _sqliteConnectionRedundant;

        private BiblioLiteSourceInfo[] _librarySourceFiles;
        private bool _anyExplicitPeakBounds;

        public static string GetLibraryCachePath(string libraryPath)
        {
            return Path.ChangeExtension(libraryPath, EXT_CACHE);
        }

        public static BiblioSpecLiteLibrary Load(BiblioSpecLiteSpec spec, ILoadMonitor loader)
        {
            if (File.Exists(spec.FilePath) && new FileInfo(spec.FilePath).Length > 0)
            {
                var library = new BiblioSpecLiteLibrary(spec);
                if (library.Load(loader))
                    return library;
            }
            return null;
        }

        public static BiblioSpecLiteLibrary GetUnloadedDocumentLibrary(BiblioSpecLiteSpec spec)
        {
            Assume.IsTrue(spec.IsDocumentLibrary);  // Otherwise, not using as intended
            return new BiblioSpecLiteLibrary(spec);
        }

        /// <summary>
        /// Controlled access to this <see cref="Immutable"/> class, which should be
        /// created through <see cref="Load(BiblioSpecLiteSpec,ILoadMonitor)"/>.
        /// </summary>
        private BiblioSpecLiteLibrary(LibrarySpec spec)
            : base(spec)
        {
            _librarySourceFiles = new BiblioLiteSourceInfo[0];
            FilePath = spec.FilePath;
            CachePath = GetLibraryCachePath(FilePath);
        }

        /// <summary>
        /// Constructs library from its component parts.  For use with <see cref="BlibDb"/>.
        /// </summary>
        public BiblioSpecLiteLibrary(LibrarySpec spec, string lsid, int dataRev, int schemaVer,
            BiblioLiteSpectrumInfo[] libraryEntries,IStreamManager streamManager)
            :this(spec)
        {
            Lsid = lsid;
            SetRevision(dataRev, schemaVer);

            SetLibraryEntries(libraryEntries);

            // Create the SQLite connection without actually connecting
            _sqliteConnection = new PooledSqliteConnection(streamManager.ConnectionPool, FilePath);

            // Create an empty list for _librarySource files, will be updated when library is loaded
            _librarySourceFiles = new BiblioLiteSourceInfo[0];
        }

        protected override LibrarySpec CreateSpec()
        {
            return new BiblioSpecLiteSpec(Name, FilePath);
        }

        public override string SpecFilter
        {
            get { return TextUtil.FileDialogFilterAll(Resources.BiblioSpecLiteLibrary_SpecFilter_BiblioSpec_Library, BiblioSpecLiteSpec.EXT); }
        }

        public override IList<RetentionTimeSource> ListRetentionTimeSources()
        {
            if (SchemaVersion < 1)
            {
                return base.ListRetentionTimeSources();
            }
            return _librarySourceFiles.Where(biblioListSourceInfo => !string.IsNullOrEmpty(biblioListSourceInfo.BaseName))
                                      .Select(biblioListSourceInfo => new RetentionTimeSource(biblioListSourceInfo.BaseName, Name))
                                      .ToArray();
        }

        public string Lsid { get; private set; }

        /// <summary>
        /// A monotonically increasing revision number associated with this library.
        /// </summary>
        public int Revision { get; private set; }

        /// <summary>
        /// A monotonically increasing schema version number for this library.
        /// </summary>
        public int SchemaVersion { get; private set; }

        /// <summary>
        /// Sets the revision float value, given integer minor and major versions.
        /// </summary>
        /// <param name="revision">Data revision from database</param>
        /// <param name="schemaVer">Schema version from database</param>
        private void SetRevision(int revision, int schemaVer)
        {
            Revision = revision;
            SchemaVersion = schemaVer;
        }

        public override int? FileCount
        {
            get { return _librarySourceFiles.Length; }
        }

        public const string FORMAT_NAME ="BiblioSpec";

        public override LibraryFiles LibraryFiles
        {
            get
            {
                return new LibraryFiles
                {
                    FilePaths = from sourceFile in _librarySourceFiles
                        let fileName = sourceFile.FilePath
                        where fileName != null
                        select fileName
                };
            }
        }

        public override LibraryDetails LibraryDetails
        {
            get
            {
                var dataFiles = GetDataFileDetails();
                var uniquePeptideCount = Keys.Select(entry => entry.Target).Distinct().Count();

                LibraryDetails details = new LibraryDetails
                                             {
                                                 Id = Lsid,
                                                 Format = FORMAT_NAME,
                                                 Revision = Revision.ToString(LocalizationHelper.CurrentCulture),
                                                 Version = SchemaVersion.ToString(LocalizationHelper.CurrentCulture),
                                                 SpectrumCount = SpectrumCount,
                                                 UniquePeptideCount = uniquePeptideCount,
                                                 DataFiles = dataFiles,
                                             };

                // In Schema Version 1, the RefSpectra table contains 
                // only the non-redundant, or the best spectrum for each peptide. 
                // The RetentionTimes table, however, stores all the spectra,
                // with the best spectra distinguished from the redundant ones by the 
                // value in the "bestSpectrum" column. 
                // If the total number of spectra in the library is more than the number
                // of  non-redundant spectra, we will provide that information to the user.
                int allSpecCount = RetentionTimesPsmCount();
                int numBestSpectra = _libraryEntries != null ? _libraryEntries.Length : 0; // number of spectra read from the RefSpectra table
                
                if (numBestSpectra < allSpecCount)
                {
                    details.TotalPsmCount = allSpecCount;
                }
                
                return details;
            }
        }

        public IEnumerable<SpectrumSourceFileDetails> GetDataFileDetails()
        {
            try
            {
                var detailsByFileName = new Dictionary<string, SpectrumSourceFileDetails>();

                lock (_sqliteConnection)
                {
                    using (SQLiteCommand select = new SQLiteCommand(_sqliteConnection.Connection))
                    {
                        // ReSharper disable LocalizableElement

                        // Query for the source files.  
                        // The number of matching entries in the RefSpectra is "BestSpectra".
                        // The number of entries in the RetentionTimes table is "MatchedSpectra".
                        // Each of these numbers is subdivided by score type.
                        // Also, select "ssf.*" because not all tables have a column "cutoffScore".
                        select.CommandText =
                            @"SELECT ssf.fileName, st.scoreType, rs.BestSpectra, rs.MatchedSpectra, ssf.*
                            FROM SpectrumSourceFiles ssf 
                            LEFT JOIN (SELECT rsInner.fileId, rsInner.scoreType AS scoreType, COUNT(DISTINCT rsInner.id) AS BestSpectra, (SELECT COUNT(*) AS MatchedSpectra FROM RetentionTimes RT WHERE RT.SpectrumSourceId = rsInner.fileId) AS MatchedSpectra 
                                FROM RefSpectra rsInner GROUP BY rsInner.fileId, rsInner.scoreType) RS ON RS.fileId = ssf.id
                            LEFT JOIN ScoreTypes st ON rs.scoreType = st.id";
                        // ReSharper restore LocalizableElement
                        using (SQLiteDataReader reader = select.ExecuteReader())
                        {
                            int icolCutoffScore = GetColumnIndex(reader, @"cutoffScore");
                            int icolIdFileName = GetColumnIndex(reader, @"idFileName");
                            while (reader.Read())
                            {
                                string filename = reader.GetString(0);
                                string idFilename = icolIdFileName > 0 && !reader.IsDBNull(icolIdFileName) ? reader.GetString(icolIdFileName) : null;
                                SpectrumSourceFileDetails sourceFileDetails;
                                if (!detailsByFileName.TryGetValue(filename, out sourceFileDetails))
                                {
                                    sourceFileDetails = new SpectrumSourceFileDetails(filename, idFilename);
                                    detailsByFileName.Add(filename, sourceFileDetails);
                                }
                                sourceFileDetails.BestSpectrum += Convert.ToInt32(reader.GetValue(2));
                                sourceFileDetails.MatchedSpectrum += Convert.ToInt32(reader.GetValue(3));

                                string scoreName = reader.IsDBNull(1) ? null : reader.GetString(1);
                                if (null != scoreName)
                                {
                                    double? cutoffScore = null;
                                    if (icolCutoffScore >= 0 && !reader.IsDBNull(icolCutoffScore))
                                    {
                                        cutoffScore = Convert.ToDouble(reader.GetValue(icolCutoffScore));
                                    }
                                    sourceFileDetails.CutoffScores[scoreName] = cutoffScore;
                                }
                            }
                        }
                    }
                }
                return detailsByFileName.Values.ToArray();
            }
            catch (Exception)
            {
                return _librarySourceFiles.Select(file => new SpectrumSourceFileDetails(file.FilePath, file.IdFilePath));
            }
        }

        private static int GetColumnIndex(SQLiteDataReader reader, string columnName)
        {
            try
            {
                return reader.GetOrdinal(columnName);
            }
            catch (IndexOutOfRangeException)
            {
                return -1; // SQLite returns -1 if column does not exist, but documentation says can throw IndexOutOfRangeException
            }
        }

        /// <summary>
        /// Path to the file on disk from which this library was loaded.  This value
        /// may be null, if the library was deserialized from XML and has not yet
        /// been loaded.
        /// </summary>
        public string FilePath { get; private set; }

        public string FilePathRedundant
        {
            get { return BiblioSpecLiteSpec.GetRedundantName(FilePath); }
        }

        public override IPooledStream ReadStream
        {
            get { return _sqliteConnection; }
        }

        public override IEnumerable<IPooledStream> ReadStreams
        {
            get
            {
                if (_sqliteConnection != null)
                    yield return _sqliteConnection;
                if (_sqliteConnectionRedundant != null)
                    yield return _sqliteConnectionRedundant;
            }
        }

        private void EnsureConnections(IStreamManager streamManager)
        {
            if (_sqliteConnection == null)
            {
                var pool = streamManager.ConnectionPool;
                _sqliteConnection = new PooledSqliteConnection(pool, FilePath);
                if (File.Exists(FilePathRedundant))
                {
                    if (_sqliteConnectionRedundant != null)
                        _sqliteConnectionRedundant.CloseStream();
                    _sqliteConnectionRedundant = new PooledSqliteConnection(pool, FilePathRedundant);
                }
            }
        }

        public override bool IsSameLibrary(Library library)
        {
            // Not really possible to tell with the old library format.
            BiblioSpecLiteLibrary biblioLib = library as BiblioSpecLiteLibrary;
            if (biblioLib == null)
                return false;
            return Equals(Lsid, biblioLib.Lsid);
        }

        public override int CompareRevisions(Library library)
        {
            // Not a valid request, if the two libraries are not the same.
            Debug.Assert(IsSameLibrary(library));
            float diff = Revision - ((BiblioSpecLiteLibrary)library).Revision;
            return (diff == 0 ? 0 : (diff < 0 ? -1 : 1));
        }

        // ReSharper disable UnusedMember.Local
        // ReSharper disable InconsistentNaming
        // Column indices for BiblioSpec SQLite indices
        private enum LibInfo
        {
            libLSID,
            createTime,
            numSpecs,
            majorVersion,
            minorVersion,
        }

        public enum IonMobilityType
        {
             none,
             driftTime,
             collisionalCrossSection
        }

        private enum RefSpectra
        {
            id,
            libSpecNumber,
            peptideSeq,
            precursorMZ,
            precursorCharge,
            moleculeName, chemicalFormula, precursorAdduct, inchiKey, otherKeys, // Small molecule columns
            ionMobilityValue,     // See ionMobilityType value for interpretation - obsolete as of v4
            ionMobilityHighEnergyDriftTimeOffsetMsec, // in Waters Mse IMS, product ions travel slightly faster after the drift tube due to added kinetic energy in the fragmentation cell
            ionMobilityType, // See enum IonMobilityType - obsolete as of v4
            peptideModSeq,
            copies,
            numPeaks,
            score,
            scoreType
        }

        private enum RefSpectraPeaks
        {
            RefSpectraID,
            peakMZ,
            peakIntensity
        }


        // From BlibMaker.cpp:
        // CREATE TABLE RefSpectraPeakAnnotations(RefSpectraID INTEGER, "
        // "peakIndex INTEGER, "
        // "name VARCHAR(256), "
        // "formula VARCHAR(256), "
        // "inchiKey VARCHAR(256), " // molecular identifier for structure retrieval
        // "otherKeys VARCHAR(256), " // alternative molecular identifiers for structure retrieval (CAS or hmdb etc)
        // "charge INTEGER, "
        // "adduct VARCHAR(256), "
        // "comment VARCHAR(256), "
        // "mzTheoretical REAL, "
        // "mzObserved REAL )",
        private enum RefSpectraPeakAnnotations
        {
            RefSpectraID,
            peakIndex,
            name,
            formula,
            inchiKey,  // molecular identifier for structure retrieval (inChiKey or hmdb etc)
            otherKeys, // alternative molecular identifiers for structure retrieval (inChiKey or hmdb etc)
            charge,
            adduct,
            comment,
            mzTheoretical,
            mzObserved
        }

        // Note on ion mobility fields:
        // Our thoughts on this have changed over time.
        // Before v4, the fields of interest were 
        //   ionMobilityValue,    which was a drift time or a CCS
        //   ionMobilityHighEnergyDriftTimeOffsetMsec, which was an incremental drift time
        //   ionMobilityType, which was one of none, drift time, or CCS
        //
        // Before v6, the  fields of interest were a bit more rational, but still drift time oriented
        //   driftTimeMsec,
        //   collisionalCrossSectionSqA,
        //   driftTimeHighEnergyOffsetMsec
        //
        // Now we have
        //   ionMobility
        //   collisionalCrossSectionSqA,
        //   ionMobilityHighEnergyOffset
        //   ionMobilityType, which is one of none, drift time, or inverse reduced IM (for Bruker)

        private enum RetentionTimes
        {
            RefSpectraID,
            RedundantRefSpectraID,
            SpectrumSourceID,
            ionMobilityValue,     // See ionMobilityType value for interpretation - obsolete as of v4
            ionMobilityHighEnergyDriftTimeOffsetMsec, // in Waters Mse IMS, product ions travel slightly faster after the drift tube due to added kinetic energy in the fragmentation cell   - obsolete as of v4
            ionMobilityType,
            retentionTime,
            bestSpectrum,
            driftTimeMsec, // Obsolete
            collisionalCrossSectionSqA,
            driftTimeHighEnergyOffsetMsec,  // Obsolete
            ionMobility,
            ionMobilityHighEnergyOffset // in Waters Mse IMS, product ions travel slightly faster after the drift tube due to added kinetic energy in the fragmentation cell
        }

        private enum SpectrumSourceFiles
        {
            id,
            fileName,
            idFileName
        }

        // Cache struct layouts
        private enum LibHeaders
        {
            lsid_byte_count,
            data_rev,
            schema_ver,
            format_version,
            num_spectra,
            location_sources_lo,
            location_sources_hi,
            location_score_types_lo,
            location_score_types_hi,

            count
        }

        private enum SourceHeader
        {
            id,
            filename_length,
            id_filename_length,

            count
        }

        private enum ScoreTypeHeader
        {
            id,
            name_length,

            count
        }
        // ReSharper restore InconsistentNaming
        // ReSharper restore UnusedMember.Local

        private byte[] CreateCache(ILoadMonitor loader, IProgressStatus status, int percent)
        {
            byte[] cacheBytes = null;
            var sm = loader.StreamManager;
            EnsureConnections(sm);
            using (SQLiteCommand select = new SQLiteCommand(_sqliteConnection.Connection))
            {
                int rows;
                string lsid;
                int dataRev, schemaVer;

                // First get header information
                select.CommandText = @"SELECT * FROM [LibInfo]";
                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new IOException(string.Format(Resources.BiblioSpecLiteLibrary_CreateCache_Failed_reading_library_header_for__0__, FilePath));

                    rows = reader.GetInt32(LibInfo.numSpecs);

                    lsid = reader.GetString(LibInfo.libLSID);

                    dataRev = reader.GetInt32(LibInfo.majorVersion);
                    schemaVer = reader.GetInt32(LibInfo.minorVersion);

                    // Set these now, in case we encounter an error further in
                    Lsid = lsid;
                    SetRevision(dataRev, schemaVer);
                }

                // Corrupted library without a valid row count, but try to compensate
                // by using count(*)
                if (rows == 0)
                {
                    select.CommandText = @"SELECT count(*) FROM [RefSpectra]";
                    using (SQLiteDataReader reader = select.ExecuteReader())
                    {
                        if (!reader.Read())
                            throw new InvalidDataException(string.Format(Resources.BiblioSpecLiteLibrary_CreateCache_Unable_to_get_a_valid_count_of_spectra_in_the_library__0__, FilePath));
                        rows = reader.GetInt32(0);
                    }
                }

                ILookup<int, KeyValuePair<int, double>> retentionTimesBySpectraIdAndFileId = null;
                ILookup<int, KeyValuePair<int, IonMobilityAndCCS>> driftTimesBySpectraIdAndFileId = null;
                ILookup<int, KeyValuePair<int, ExplicitPeakBounds>> peakBoundsBySpectraIdAndFileId = null;
                var scoreTypesById = new Dictionary<int, string>();
                var scoreTypesByName = new Dictionary<string, int>();
                var proteinsBySpectraID = ProteinsBySpectraID();

                if (schemaVer >= 1)
                {
                    if (SqliteOperations.TableExists(_sqliteConnection.Connection, @"RetentionTimes")) // Only a filtered library will have this table
                    {
                        using (var cmd = _sqliteConnection.Connection.CreateCommand())
                        {
                            cmd.CommandText = @"SELECT * FROM RetentionTimes";
                            using (var dataReader = cmd.ExecuteReader())
                            {
                                var retentionTimeReader = new RetentionTimeReader(dataReader, schemaVer);
                                retentionTimeReader.ReadAllRows();
                                retentionTimesBySpectraIdAndFileId =
                                    retentionTimeReader.SpectaIdFileIdTimes.ToLookup(kvp => kvp.Key, kvp => kvp.Value);
                                driftTimesBySpectraIdAndFileId =
                                    retentionTimeReader.SpectraIdFileIdIonMobilities.ToLookup(kvp => kvp.Key, kvp => kvp.Value);
                                peakBoundsBySpectraIdAndFileId = retentionTimeReader.PeakBoundaries.ToLookup(kvp => kvp.Key,
                                    kvp => kvp.Value);
                            }
                        }
                    }
                    if (SqliteOperations.TableExists(_sqliteConnection.Connection, @"ScoreTypes"))
                    {
                        select.CommandText = @"SELECT id, scoreType FROM ScoreTypes";
                        using (var reader = select.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var id = reader.GetInt32(0);
                                var name = reader.GetString(1);
                                scoreTypesById[id] = name;
                                scoreTypesByName[name] = id;
                            }
                        }
                    }
                }

                var setLibKeys = new HashSet<LibKey>(rows);
                var libraryEntries = new List<BiblioLiteSpectrumInfo>(rows);
                var librarySourceFiles = new List<BiblioLiteSourceInfo>();

                select.CommandText = @"SELECT * FROM [RefSpectra]";
                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                    int iId = reader.GetOrdinal(RefSpectra.id);
                    //int iSeq = reader.GetOrdinal(RefSpectra.peptideSeq);
                    int iModSeq = reader.GetOrdinal(RefSpectra.peptideModSeq);
                    int iCharge = reader.GetOrdinal(RefSpectra.precursorCharge);
                    int iCopies = reader.GetOrdinal(RefSpectra.copies);
                    int iPeaks = reader.GetOrdinal(RefSpectra.numPeaks);
                    int iAdduct = reader.GetOrdinal(RefSpectra.precursorAdduct);
                    int iMoleculeName = reader.GetOrdinal(RefSpectra.moleculeName);
                    int iChemicalFormula = reader.GetOrdinal(RefSpectra.chemicalFormula);
                    int iInChiKey = reader.GetOrdinal(RefSpectra.inchiKey);
                    int iOtherKeys = reader.GetOrdinal(RefSpectra.otherKeys);
                    int iScore = reader.GetOrdinal(RefSpectra.score);
                    int iScoreType = reader.GetOrdinal(RefSpectra.scoreType);

                    int rowsRead = 0;
                    while (reader.Read())
                    {
                        int percentComplete = rowsRead++*percent/rows;
                        if (status.PercentComplete != percentComplete)
                        {
                            // Check for cancellation after each integer change in percent loaded.
                            if (loader.IsCanceled)
                            {
                                loader.UpdateProgress(status.Cancel());
                                return null;
                            }

                            // If not cancelled, update progress.
                            loader.UpdateProgress(status = status.ChangePercentComplete(percentComplete));
                        }

                        int id = reader.GetInt32(iId);
                        proteinsBySpectraID.TryGetValue(id, out var protein);

                        string sequence = reader.GetString(iModSeq);
                        int charge = reader.GetInt16(iCharge);
                        string adduct = iAdduct >= 0 && !reader.IsDBNull(iAdduct) ? reader.GetString(iAdduct) : null;
                        int copies = reader.GetInt32(iCopies);
                        int numPeaks = reader.GetInt32(iPeaks);
                        double? score = !reader.IsDBNull(iScore) ? reader.GetDouble(iScore) : (double?) null;
                        int? scoreType = !reader.IsDBNull(iScoreType) ? reader.GetInt32(iScoreType) : (int?) null;
                        var chemicalFormula = iChemicalFormula >= 0 && !reader.IsDBNull(iChemicalFormula) ? reader.GetString(iChemicalFormula) : null;
                        bool isProteomic = (string.IsNullOrEmpty(adduct) || Adduct.FromStringAssumeProtonated(adduct).IsProtonated) && 
                            string.IsNullOrEmpty(chemicalFormula); // We may write an adduct like [M+H] for peptides
                        SmallMoleculeLibraryAttributes smallMoleculeLibraryAttributes;
                        if (isProteomic)
                        {
                            smallMoleculeLibraryAttributes = SmallMoleculeLibraryAttributes.EMPTY;
                        }
                        else
                        {
                            var moleculeName = iMoleculeName >= 0 && !reader.IsDBNull(iMoleculeName) ? reader.GetString(iMoleculeName) : null;
                            var inChiKey = iInChiKey >= 0 && !reader.IsDBNull(iInChiKey) ? reader.GetString(iInChiKey) : null;
                            var otherKeys = iOtherKeys >= 0 && !reader.IsDBNull(iOtherKeys) ? reader.GetString(iOtherKeys) : null;
                            smallMoleculeLibraryAttributes = SmallMoleculeLibraryAttributes.Create(moleculeName, chemicalFormula, inChiKey, otherKeys);
                            // Construct a custom molecule so we can be sure we're using the same keys
                            var mol = CustomMolecule.FromSmallMoleculeLibraryAttributes(smallMoleculeLibraryAttributes);
                            sequence = mol.PrimaryEquivalenceKey;
                        }

                        // Avoid creating a cache which will just report it is corrupted.
                        // Older versions of BlibBuild used to create matches with charge 0.
                        // Newer versions that handle small molecules may reasonably use negative charges.
                        if (charge == 0 || Math.Abs(charge) > TransitionGroup.MAX_PRECURSOR_CHARGE)
                            continue;
                        var retentionTimesByFileId = default(IndexedRetentionTimes);
                        if (retentionTimesBySpectraIdAndFileId != null)
                        {
                            retentionTimesByFileId = new IndexedRetentionTimes(retentionTimesBySpectraIdAndFileId[id]);
                        }
                        var driftTimesByFileId = default(IndexedIonMobilities);
                        if (driftTimesBySpectraIdAndFileId != null)
                        {
                            driftTimesByFileId = new IndexedIonMobilities(driftTimesBySpectraIdAndFileId[id]);
                        }
                        var peakBoundariesByFileId = ImmutableSortedList<int, ExplicitPeakBounds>.EMPTY;
                        if (peakBoundsBySpectraIdAndFileId != null)
                        {
                            peakBoundariesByFileId = ImmutableSortedList.FromValues(peakBoundsBySpectraIdAndFileId[id].Distinct());
                        }
                        LibKey key;
                        if (isProteomic)
                        {
                            LibraryKey libraryKey =
                                CrosslinkSequenceParser.TryParseCrosslinkLibraryKey(sequence, charge);
                            if (libraryKey == null)
                            {
                                libraryKey = new PeptideLibraryKey(sequence, charge);
                            }
                            key = new LibKey(libraryKey);
                        }
                        else
                        {
                            key = new LibKey(smallMoleculeLibraryAttributes, Adduct.FromStringAssumeChargeOnly(adduct));
                        }
                        // These libraries should not have duplicates, but just in case.
                        // CONSIDER: Emit error about redundancy?
                        if (setLibKeys.Add(key))
                        {
                            string scoreName = null;
                            if (scoreType.HasValue)
                            {
                                scoreTypesById.TryGetValue(scoreType.Value, out scoreName);
                            }

                            libraryEntries.Add(new BiblioLiteSpectrumInfo(key, copies, numPeaks, id, protein,
                                retentionTimesByFileId, driftTimesByFileId, peakBoundariesByFileId, score, scoreName));
                        }
                    }
                }

                libraryEntries = FilterInvalidLibraryEntries(ref status, libraryEntries);

                if (schemaVer > 0)
                {
                    select.CommandText = @"SELECT * FROM [SpectrumSourceFiles]";
                    using (SQLiteDataReader reader = select.ExecuteReader())
                    {
                        int iId = reader.GetOrdinal(SpectrumSourceFiles.id);
                        int iFilename = reader.GetOrdinal(SpectrumSourceFiles.fileName);
                        int iIdFilename = reader.GetOrdinal(SpectrumSourceFiles.idFileName); // Save the search result file, too (may be distinct from the spectra source file)

                        while (reader.Read())
                        {
                            string filename = reader.GetString(iFilename);
                            string idFilename = iIdFilename < 0 || reader.IsDBNull(iIdFilename) ? null : reader.GetString(iIdFilename);
                            int id = reader.GetInt32(iId);
                            librarySourceFiles.Add(new BiblioLiteSourceInfo(id, filename, idFilename ?? string.Empty));
                        }
                    }

                }

                var outStream = new MemoryStream();
                foreach (var info in libraryEntries)
                {
                    // Write the spectrum header - order must match enum SpectrumCacheHeader
                    info.Key.Write(outStream);
                    outStream.Write(BitConverter.GetBytes(info.Copies), 0, sizeof (int));
                    outStream.Write(BitConverter.GetBytes(info.NumPeaks), 0, sizeof (int));
                    outStream.Write(BitConverter.GetBytes(info.Id), 0, sizeof (int));

                    // Optional protein name or molecule list name
                    if (string.IsNullOrEmpty(info.Protein))
                    {
                        const int len = 0;
                        outStream.Write(BitConverter.GetBytes(len), 0, sizeof(int));
                    }
                    else
                    {
                        var proteinOrMoleculeListBytes = Encoding.UTF8.GetBytes(info.Protein);
                        outStream.Write(BitConverter.GetBytes(proteinOrMoleculeListBytes.Length), 0, sizeof(int));
                        outStream.Write(proteinOrMoleculeListBytes, 0, proteinOrMoleculeListBytes.Length);
                    }

                    if (info.ScoreType == null || !scoreTypesByName.TryGetValue(info.ScoreType, out var scoreType))
                    {
                        scoreType = -1;
                    }
                    outStream.Write(BitConverter.GetBytes(scoreType), 0, sizeof(int));
                    outStream.Write(BitConverter.GetBytes(info.Score ?? double.NaN), 0, sizeof(double));
                    info.RetentionTimesByFileId.Write(outStream);
                    info.IonMobilitiesByFileId.Write(outStream);
                    WritePeakBoundaries(outStream, info.PeakBoundariesByFileId);
                }

                long sourcePosition = 0;
                if (librarySourceFiles.Count > 0)
                {
                    // Write all source files
                    sourcePosition = outStream.Position;
                    foreach (var librarySourceFile in librarySourceFiles)
                    {
                        outStream.Write(BitConverter.GetBytes(librarySourceFile.Id), 0, sizeof(int));
                        // Spectra source filename
                        var librarySourceFileNameBytes = Encoding.UTF8.GetBytes(librarySourceFile.FilePath);
                        // ID source (e.g. Mascot search results) filename
                        var searchResultsFileNameBytes = Encoding.UTF8.GetBytes(librarySourceFile.IdFilePath ?? string.Empty);
                        outStream.Write(BitConverter.GetBytes(librarySourceFileNameBytes.Length), 0, sizeof(int));
                        outStream.Write(BitConverter.GetBytes(searchResultsFileNameBytes.Length), 0, sizeof(int));
                        outStream.Write(librarySourceFileNameBytes, 0, librarySourceFileNameBytes.Length);
                        outStream.Write(searchResultsFileNameBytes, 0, searchResultsFileNameBytes.Length);
                    }
                    // Terminate with zero ID and zero name length
                    var zeroBytes = BitConverter.GetBytes(0);
                    outStream.Write(zeroBytes, 0, sizeof(int));
                    outStream.Write(zeroBytes, 0, sizeof(int));
                    outStream.Write(zeroBytes, 0, sizeof(int));
                    outStream.Write(zeroBytes, 0, sizeof(int));
                }

                long scoreTypesPosition = 0;
                if (scoreTypesById.Count > 0)
                {
                    scoreTypesPosition = outStream.Position;
                    foreach (var score in scoreTypesById.OrderBy(kvp => kvp.Key))
                    {
                        outStream.Write(BitConverter.GetBytes(score.Key), 0, sizeof(int));
                        var scoreTypeNameBytes = Encoding.UTF8.GetBytes(score.Value);
                        outStream.Write(BitConverter.GetBytes(scoreTypeNameBytes.Length), 0, sizeof(int));
                        outStream.Write(scoreTypeNameBytes, 0, scoreTypeNameBytes.Length);
                    }
                    // Terminate with zero ID and zero name length
                    var zeroBytes = BitConverter.GetBytes(0);
                    outStream.Write(zeroBytes, 0, sizeof(int));
                    outStream.Write(zeroBytes, 0, sizeof(int));
                }

                byte[] lsidBytes = Encoding.UTF8.GetBytes(lsid);
                outStream.Write(lsidBytes, 0, lsidBytes.Length);
                outStream.Write(BitConverter.GetBytes(lsidBytes.Length), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(dataRev), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(schemaVer), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(FORMAT_VERSION_CACHE), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(libraryEntries.Count), 0, sizeof (int));
                outStream.Write(BitConverter.GetBytes(sourcePosition), 0, sizeof (long));
                outStream.Write(BitConverter.GetBytes(scoreTypesPosition), 0, sizeof(long));
                cacheBytes = outStream.ToArray();
                try
                {
                    using (FileSaver fs = new FileSaver(CachePath, sm))
                    using (Stream cacheFileStream = sm.CreateStream(fs.SafeName, FileMode.Create, true))
                    {
                        cacheFileStream.Write(cacheBytes, 0, cacheBytes.Length);
                        sm.Finish(cacheFileStream);
                        fs.Commit();
                        sm.SetCache(FilePath, CachePath);
                    }
                }
                catch
                {
                    // ignore
                }
            }

            loader.UpdateProgress(status.Complete());
            return cacheBytes;
        }

        private Dictionary<int, string> ProteinsBySpectraID()
        {
            var proteinsBySpectraID = new Dictionary<int, string>();
            if (SqliteOperations.TableExists(_sqliteConnection.Connection, @"RefSpectraProteins"))
            {
                using (var cmd = _sqliteConnection.Connection.CreateCommand())
                {
                    cmd.CommandText =
                        @"SELECT RefSpectraId, accession FROM [Proteins] as t INNER JOIN [RefSpectraProteins] as s ON t.[id] = s.[ProteinId]";
                    using (var dataReader = cmd.ExecuteReader())
                    {
                        while (dataReader.Read())
                        {
                            var refSpectraId = dataReader.GetInt32(0);
                            var accession = dataReader.GetString(1);
                            // RefSpectraProteins is a many-to-many table, just use first seen for group naming purposes
                            if (!string.IsNullOrEmpty(accession) && !proteinsBySpectraID.ContainsKey(refSpectraId))
                            {
                                proteinsBySpectraID.Add(refSpectraId, accession);
                            }
                        }
                    }
                }
            }

            return proteinsBySpectraID;
        }

        private bool Load(ILoadMonitor loader)
        {
            ProgressStatus status = new ProgressStatus(string.Empty);
            loader.UpdateProgress(status);

            bool cached = loader.StreamManager.IsCached(FilePath, CachePath);
            if (Load(loader, status, cached))
                return true;

            // If loading from the cache failed, rebuild it.
            if (cached)
            {
                // Reset readStream so we don't read corrupt file.
                if (_sqliteConnection != null)
                {
                    _sqliteConnection.CloseStream();
                    _sqliteConnection = null;
                }
                if (Load(loader, status, false))
                    return true;
            }

            // Close any streams that got opened
            foreach (var pooledStream in ReadStreams)
                pooledStream.CloseStream();

            return false;
        }

        private bool Load(ILoadMonitor loader, IProgressStatus status, bool cached)
        {
            try
            {
                var valueCache = new ValueCache();
                int loadPercent = 100;
                byte[] cacheBytes = null;
                if (!cached)
                {
                    // Building the cache will take 95% of the load time.
                    loadPercent = 5;
                    status = status.ChangeMessage(string.Format(Resources.BiblioSpecLiteLibrary_Load_Building_binary_cache_for__0__library,
                                                           Path.GetFileName(FilePath)));
                    status = status.ChangePercentComplete(0);
                    loader.UpdateProgress(status);

                    cacheBytes = CreateCache(loader, status, 100 - loadPercent);
                    if (cacheBytes == null)
                    {
                        return false;
                    }

                }

                status = status.ChangeMessage(string.Format(Resources.BiblioSpecLiteLibraryLoadLoading__0__library,
                                                            Path.GetFileName(FilePath)));
                loader.UpdateProgress(status);

                var sm = loader.StreamManager;
                Stream stream;
                if (cacheBytes != null)
                {
                    stream = new MemoryStream(cacheBytes, false);
                }
                else
                {
                    stream = sm.CreateStream(CachePath, FileMode.Open, true);
                }
                using (stream)
                {
                    // Read library header from the end of the cache
                    int countHeader = (int) LibHeaders.count*sizeof (int);
                    stream.Seek(-countHeader, SeekOrigin.End);

                    byte[] libHeader = new byte[countHeader];
                    ReadComplete(stream, libHeader, countHeader);

                    int version = GetInt32(libHeader, (int) LibHeaders.format_version);
                    if (version != FORMAT_VERSION_CACHE)
                        return false;

                    int countLsidBytes = GetInt32(libHeader, (int) LibHeaders.lsid_byte_count);
                    stream.Seek(-countHeader-countLsidBytes, SeekOrigin.End);
                    Lsid = ReadString(stream, countLsidBytes);
                    int dataRev = GetInt32(libHeader, (int)LibHeaders.data_rev);
                    int schemaVer = GetInt32(libHeader, (int) LibHeaders.schema_ver);
                    SetRevision(dataRev, schemaVer);

                    int numSpectra = GetInt32(libHeader, (int) LibHeaders.num_spectra);

                    var libraryEntries = new BiblioLiteSpectrumInfo[numSpectra];
                    var librarySourceFiles = new List<BiblioLiteSourceInfo>();

                    long locationSources = BitConverter.ToInt64(libHeader,
                                                                ((int) LibHeaders.location_sources_lo)*sizeof (int));
                    long locationScoreTypes = BitConverter.ToInt64(libHeader,
                                                                ((int) LibHeaders.location_score_types_lo)*sizeof (int));

                    if (locationSources != 0)
                    {
                        stream.Seek(locationSources, SeekOrigin.Begin);
                        const int countSourceBytes = (int)SourceHeader.count * sizeof(int);
                        byte[] sourceHeader = new byte[countSourceBytes];
                        for (;;)
                        {
                            ReadComplete(stream, sourceHeader, countSourceBytes);
                            int sourceId = GetInt32(sourceHeader, (int)SourceHeader.id);
                            int filenameLength = GetInt32(sourceHeader, (int)SourceHeader.filename_length);
                            if (filenameLength == 0)
                                break;
                            int idfilenameLength = GetInt32(sourceHeader, (int)SourceHeader.id_filename_length);
                            string filename = ReadString(stream, filenameLength);
                            string idfilename = ReadString(stream, idfilenameLength);
                            librarySourceFiles.Add(new BiblioLiteSourceInfo(sourceId, filename, idfilename)); 
                        }
                    }

                    _librarySourceFiles = librarySourceFiles.ToArray();

                    var scoreTypes = new Dictionary<int, string>();
                    if (locationScoreTypes != 0)
                    {
                        stream.Seek(locationScoreTypes, SeekOrigin.Begin);
                        const int countScoreTypeBytes = (int) ScoreTypeHeader.count * sizeof(int);
                        var scoreTypeHeader = new byte[countScoreTypeBytes];
                        for (;;)
                        {
                            ReadComplete(stream, scoreTypeHeader, countScoreTypeBytes);
                            var id = GetInt32(scoreTypeHeader, (int) ScoreTypeHeader.id);
                            var nameLen = GetInt32(scoreTypeHeader, (int) ScoreTypeHeader.name_length);
                            if (nameLen == 0)
                            {
                                break;
                            }
                            scoreTypes[id] = ReadString(stream, nameLen);
                        }
                    }

                    // Seek to beginning of spectrum headers, which is the beginning of the
                    // files, since spectra are not stored in the cache.
                    stream.Seek(0, SeekOrigin.Begin);
                    for (int i = 0; i < numSpectra; i++)
                    {
                        int percent = (100 - loadPercent) + (i*loadPercent/numSpectra);
                        if (status.PercentComplete != percent)
                        {
                            // Check for cancellation after each integer change in percent loaded.
                            if (loader.IsCanceled)
                            {
                                loader.UpdateProgress(status.Cancel());
                                return false;
                            }
                            // If not cancelled, update progress.
                            loader.UpdateProgress(status = status.ChangePercentComplete(percent));
                        }

                        // Read spectrum header
                        var key = LibKey.Read(valueCache, stream);
                        int copies = PrimitiveArrays.ReadOneValue<int>(stream);
                        int numPeaks = PrimitiveArrays.ReadOneValue<int>(stream);
                        int id = PrimitiveArrays.ReadOneValue<int>(stream);
                        int proteinLength = PrimitiveArrays.ReadOneValue<int>(stream);
                        var proteinOrMoleculeList = proteinLength == 0 ? null : ReadString(stream, proteinLength);

                        var scoreTypeId = PrimitiveArrays.ReadOneValue<int>(stream);
                        var score = (double?) PrimitiveArrays.ReadOneValue<double>(stream);
                        if (!scoreTypes.TryGetValue(scoreTypeId, out var scoreType))
                        {
                            score = null;
                        }
                        var retentionTimesByFileId = IndexedRetentionTimes.Read(stream);
                        var driftTimesByFileId = IndexedIonMobilities.Read(stream);
                        ImmutableSortedList<int, ExplicitPeakBounds> peakBoundaries =
                            ReadPeakBoundaries(stream);
                        _anyExplicitPeakBounds = _anyExplicitPeakBounds || peakBoundaries.Count > 0;
                        libraryEntries[i] = new BiblioLiteSpectrumInfo(key, copies, numPeaks, id, proteinOrMoleculeList,
                            retentionTimesByFileId, driftTimesByFileId, peakBoundaries, score, scoreType);
                    }

                    // Checksum = checksum.ChecksumValue;
                    SetLibraryEntries(libraryEntries);

                    loader.UpdateProgress(status.Complete());

                    // Create a connection to the database from which the spectra will be read
                    EnsureConnections(sm);
                }

                return true;
            }

            catch (Exception x)
            {
                // Skyline first tries to load the library passing in "true" for "cached", and, if that fails, it tries
                // again passing in "false" for "cached". So, any errors encountered during that first cached=true pass
                // should be suppressed because we know Skyline is going to try again.
                if (!cached)
                {
                    var failureException = new Exception(FormatErrorMessage(x), x);
                    if (ExceptionUtil.IsProgrammingDefect(x))
                    {
                        throw failureException; // We want this to show up in ExceptionWeb
                    }
                    // This will show the user the error message after which the operation can be treated as canceled.
                    loader.UpdateProgress(status.ChangeErrorException(failureException));
                }
                return false;
            }
        }

        string FormatErrorMessage(Exception x)
        {
            string details;
            try
            {
                details = LibraryDetails.ToString();
            }
            catch (Exception e)
            {
                details = e.Message; // File is too messed up to pull details, leave a hint as to why that might be
            }
            return TextUtil.LineSeparate(
                string.Format(Resources.BiblioSpecLiteLibrary_Load_Failed_loading_library__0__, FilePath),
                x.Message,
                details);
        }

        private ImmutableSortedList<int, ExplicitPeakBounds> ReadPeakBoundaries(Stream stream)
        {
            int peakBoundCount = PrimitiveArrays.ReadOneValue<int>(stream);
            if (peakBoundCount == 0)
            {
                return ImmutableSortedList<int, ExplicitPeakBounds>.EMPTY;
            }
            var peakBoundaryValues = new List<KeyValuePair<int, ExplicitPeakBounds>>();
            for (int i = 0; i < peakBoundCount; i++)
            {
                int fileId = PrimitiveArrays.ReadOneValue<int>(stream);
                double peakStart = PrimitiveArrays.ReadOneValue<double>(stream);
                double peakEnd = PrimitiveArrays.ReadOneValue<double>(stream);
                double score = PrimitiveArrays.ReadOneValue<double>(stream);
                peakBoundaryValues.Add(new KeyValuePair<int, ExplicitPeakBounds>(fileId, new ExplicitPeakBounds(peakStart, peakEnd, score)));
            }
            return ImmutableSortedList.FromValues(peakBoundaryValues);
        }

        private void WritePeakBoundaries(Stream stream, ImmutableSortedList<int, ExplicitPeakBounds> peakBoundaries)
        {
            PrimitiveArrays.WriteOneValue(stream, peakBoundaries.Count);
            foreach (var entry in peakBoundaries)
            {
                PrimitiveArrays.WriteOneValue(stream, entry.Key);
                PrimitiveArrays.WriteOneValue(stream, entry.Value.StartTime);
                PrimitiveArrays.WriteOneValue(stream, entry.Value.EndTime);
                PrimitiveArrays.WriteOneValue(stream, entry.Value.Score);
            }
        }

        public override LibraryChromGroup LoadChromatogramData(object spectrumKey)
        {
            return null;
        }

        public override SpectrumPeaksInfo LoadSpectrum(object spectrumKey)
        {
            var spectrumLiteKey = spectrumKey as SpectrumLiteKey;
            if (spectrumLiteKey != null)
            {
                if (!spectrumLiteKey.IsBest)
                    return new SpectrumPeaksInfo(ReadRedundantSpectrum(spectrumLiteKey.RedundantId));

                // Always get the best spectrum from the non-redundant library
                spectrumKey = spectrumLiteKey.NonRedundantId;
            }
                
            return base.LoadSpectrum(spectrumKey);
        }

        protected override SpectrumHeaderInfo CreateSpectrumHeaderInfo(BiblioLiteSpectrumInfo info)
        {
            return new BiblioSpecSpectrumHeaderInfo(Name, info.Copies, info.Score, info.ScoreType, info.Protein);
        }

        protected override SpectrumPeaksInfo.MI[] ReadSpectrum(BiblioLiteSpectrumInfo info)
        {
            return _sqliteConnection.ExecuteWithConnection(connection =>
            {
                using (SQLiteCommand select = new SQLiteCommand(_sqliteConnection.Connection))
                {
                    select.CommandText = @"SELECT * FROM [RefSpectraPeaks] WHERE [RefSpectraID] = ?";
                    select.Parameters.Add(new SQLiteParameter(DbType.UInt64, (long)info.Id));
                    using (SQLiteDataReader reader = select.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int numPeaks = info.NumPeaks;
                            return ReadPeaks(reader, numPeaks, info.Id, _sqliteConnection);
                        }
                    }
                    return null;
                }
            });
        }

        // Libraries may contain multiple annotations for a single observed mz
        private class AnnotationsForObservedMz
        {
            public List<SpectrumPeakAnnotation> Annotations { get; private set; }
            public double ObservedMz { get; private set; } // For sanity checking

            public AnnotationsForObservedMz(double mz, List<SpectrumPeakAnnotation> annotations)
            {
                ObservedMz = mz;
                Annotations = annotations;
            }
        }

        // Create a dictionary of <peak index, <observedMz, annotations>> for a RefSpectraId
        private Dictionary<int, AnnotationsForObservedMz> ReadPeakAnnotations(int refSpectraId, PooledSqliteConnection connection)
        {
            var resultDict = new Dictionary<int, AnnotationsForObservedMz>();
            if (connection == null)
            {
                return resultDict;
            }
            if (!SqliteOperations.TableExists(connection.Connection, @"RefSpectraPeakAnnotations"))
            {
                return resultDict;
            }
            // Read fragment annotations if any
            // For small molecules, see if there are any peak annotations (for peptides, we can generate our own fragment info)
            using (var select = connection.Connection.CreateCommand())
            {
                select.CommandText = @"SELECT * FROM [RefSpectraPeakAnnotations] WHERE [RefSpectraID] = ?";
                select.Parameters.Add(new SQLiteParameter(DbType.UInt64, (long)refSpectraId));
                using (SQLiteDataReader reader = @select.ExecuteReader())
                {
                    // N.B. this code needs to track any changes to RefSpectraPeakAnnotations, which is to say changes in BlibMaker.cpp 
                    while (reader.Read())
                    {
                        // var refSpectraID = reader.GetInt32(RefSpectraPeakAnnotations.RefSpectraID);
                        var peakIndex = reader.GetInt32(RefSpectraPeakAnnotations.peakIndex);
                        var fragname = reader.GetString(RefSpectraPeakAnnotations.name);
                        var formula = reader.GetString(RefSpectraPeakAnnotations.formula);
                        var inchkey = reader.GetString(RefSpectraPeakAnnotations.inchiKey);
                        var otherKeys = reader.GetString(RefSpectraPeakAnnotations.otherKeys);
                        var charge = reader.GetInt32(RefSpectraPeakAnnotations.charge);
                        var adductString = reader.GetString(RefSpectraPeakAnnotations.adduct);
                        var comment = reader.GetString(RefSpectraPeakAnnotations.comment);
                        var mzTheoretical = reader.GetDouble(RefSpectraPeakAnnotations.mzTheoretical);
                        var mzObserved = reader.GetDouble(RefSpectraPeakAnnotations.mzObserved);
                        var molecule = SmallMoleculeLibraryAttributes.Create(fragname, formula, inchkey, otherKeys);
                        var adduct = string.IsNullOrEmpty(adductString)
                            ? Adduct.FromChargeNoMass(charge)
                            : Adduct.FromStringAssumeChargeOnly(adductString);
                        AnnotationsForObservedMz annotations;
                        if (!resultDict.TryGetValue(peakIndex, out annotations))
                        {
                            resultDict.Add(peakIndex, annotations = new AnnotationsForObservedMz(mzObserved, new List<SpectrumPeakAnnotation>()));
                        }
                        var annotation = SpectrumPeakAnnotation.Create(molecule, adduct, comment, mzTheoretical);
                        annotations.Annotations.Add(annotation);
                    }
                }
            }
            return resultDict;
        }

        private SpectrumPeaksInfo.MI[] ReadRedundantSpectrum(int spectrumId)
        {
            if (_sqliteConnectionRedundant == null)
                throw new IOException(string.Format(Resources.BiblioSpecLiteLibrary_ReadRedundantSpectrum_The_redundant_library__0__does_not_exist, FilePathRedundant));

            try
            {
                using (SQLiteCommand select = new SQLiteCommand(_sqliteConnectionRedundant.Connection))
                {
                    select.CommandText =
                        @"SELECT * FROM " +
                        @"[RefSpectra] as s INNER JOIN [RefSpectraPeaks] as p ON s.[id] = p.[RefSpectraID] " +
                        @"WHERE s.[id] = ?";
                    select.Parameters.Add(new SQLiteParameter(DbType.UInt64, (long)spectrumId));

                    using (SQLiteDataReader reader = select.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int numPeaks = reader.GetInt32(RefSpectra.numPeaks);
                            return ReadPeaks(reader, numPeaks, spectrumId, _sqliteConnectionRedundant);
                        }
                    }
                }
            }
            catch (SQLiteException x)
            {
                throw new IOException(
                    string.Format(Resources.BiblioSpecLiteLibrary_ReadSpectrum_Unexpected_SQLite_failure_reading__0__,
                                  FilePathRedundant), x);
            }

            return null;
        }

        private SpectrumPeaksInfo.MI[] ReadPeaks(SQLiteDataReader reader, int numPeaks, int refSpectraId, PooledSqliteConnection connection)
        {
            const int sizeMz = sizeof(double);
            const int sizeInten = sizeof(float);

            byte[] peakMzCompressed = reader.GetBytes(RefSpectraPeaks.peakMZ);
            byte[] peakMz = peakMzCompressed.Uncompress(numPeaks * sizeMz);

            byte[] peakIntensCompressed = reader.GetBytes(RefSpectraPeaks.peakIntensity);
            byte[] peakIntens = peakIntensCompressed.Uncompress(numPeaks * sizeInten);

            // Build the list
            var arrayMI = new SpectrumPeaksInfo.MI[numPeaks];

            for (int i = 0; i < numPeaks; i++)
            {
                arrayMI[i].Intensity = BitConverter.ToSingle(peakIntens, i * sizeInten);
                arrayMI[i].Mz = BitConverter.ToDouble(peakMz, i * sizeMz);
            }

            // Add peak annotations, if any
            var dict = ReadPeakAnnotations(refSpectraId, connection);
            foreach (var kvp in dict)
            {
                var peakIndex = kvp.Key;
                var annotationWithObservedMz = kvp.Value;
                if (Math.Abs(annotationWithObservedMz.ObservedMz-arrayMI[peakIndex].Mz) > 0.0000001)
                {
                    Assume.Fail(string.Format(@"trouble reading peak annotation: mzObserved {0} disagrees with indexed peak mz {1}",
                            annotationWithObservedMz.ObservedMz, arrayMI[peakIndex].Mz));
                }
                arrayMI[peakIndex].Annotations = annotationWithObservedMz.Annotations;
            }

            return arrayMI;
        }

        public override bool TryGetRetentionTimes(LibKey key, MsDataFileUri filePath, out double[] retentionTimes)
        {
            int i = FindEntry(key);
            int j = FindSource(filePath);
            if (i != -1 && j != -1)
            {
                retentionTimes = _libraryEntries[i].RetentionTimesByFileId.GetTimes(_librarySourceFiles[j].Id);
                return true;
            }

            return base.TryGetRetentionTimes(key, filePath, out retentionTimes);
        }

        public override ExplicitPeakBounds GetExplicitPeakBounds(MsDataFileUri filePath, IEnumerable<Target> peptideSequences)
        {
            if (!_anyExplicitPeakBounds)
            {
                return null;
            }
            int iFile = FindSource(filePath);
            if (iFile < 0)
            {
                return null;
            }
            int fileId = _librarySourceFiles[iFile].Id;
            bool anySequenceMatch = false;
            foreach (var item in LibraryEntriesWithSequences(peptideSequences))
            {
                ExplicitPeakBounds peakBoundaries;
                if (item.PeakBoundariesByFileId.TryGetValue(fileId, out peakBoundaries))
                {
                    return peakBoundaries;
                }

                if (item.PeakBoundariesByFileId.Any())
                {
                    // If the library has peak boundaries for this sequence in some other file, assume
                    // that the peptide was just not found in this file.
                    anySequenceMatch = true;
                }
            }

            if (anySequenceMatch)
            {
                // If the library has 
                return ExplicitPeakBounds.EMPTY;
            }
            return null;
        }

        public override bool TryGetRetentionTimes(int fileIndex, out LibraryRetentionTimes retentionTimes)
        {
            return TryGetRetentionTimes(MsDataFileUri.Parse(_librarySourceFiles[fileIndex].FilePath), out retentionTimes);
        }

        public override bool TryGetRetentionTimes(MsDataFileUri filePath, out LibraryRetentionTimes retentionTimes)
        {
            int j = FindSource(filePath);
            if (j != -1)
            {
                var source = _librarySourceFiles[j];
                ILookup<Target, double[]> timesLookup = _libraryEntries.ToLookup(
                    entry => entry.Key.Target, 
                    entry=>entry.RetentionTimesByFileId.GetTimes(source.Id));
                var timesDict = timesLookup.ToDictionary(
                    grouping => grouping.Key,
                    grouping =>
                        {
                            var array = grouping.SelectMany(values => values).ToArray();
                            Array.Sort(array);
                            return array;
                        });
                var nonEmptyTimesDict = timesDict
                    .Where(kvp => kvp.Value.Length > 0)
                    .ToDictionary(kvp => kvp.Key, kvp => new Tuple<TimeSource, double[]>(TimeSource.scan, kvp.Value));
                retentionTimes = new LibraryRetentionTimes(filePath.ToString(), nonEmptyTimesDict);
                return true;
            }

            return base.TryGetRetentionTimes(filePath, out retentionTimes);
        }

        public override IEnumerable<double> GetRetentionTimesWithSequences(string filePath, IEnumerable<Target> peptideSequences, ref int? iFile)
        {
            if (!iFile.HasValue)
                iFile = FindSource(MsDataFileUri.Parse(filePath));
            if (iFile.Value < 0)
            {
                return new double[0];
            }
            var times = new List<double[]>();
            foreach (var item in LibraryEntriesWithSequences(peptideSequences))
            {
                times.Add(item.RetentionTimesByFileId.GetTimes(_librarySourceFiles[iFile.Value].Id));
            }
            return times.SelectMany(array => array);
        }

        private const int MIN_IRT_ALIGNMENT_POINT_COUNT = 10;

        public override bool TryGetIrts(out LibraryRetentionTimes retentionTimes)
        {
            // TODO: This is a bit of a hack that needs to be improved to work better for
            //       more cases and further optimized. But, it has been shown to work very
            //       well for the case where the library is composed of a number of highly
            //       similar runs, and acceptably even for the combined runs of the Navarro,
            //       Nature Biotech, 2016 HYE data set where 6 runs share only tens of peptides
            //       including the iRT standards with the first run.
            var newSources = ResultNameMap.FromNamedElements(ListRetentionTimeSources());
            var allLibraryRetentionTimes = ReadAllRetentionTimes(newSources);
            foreach (var retentionTimeSource in newSources.Values)
            {
                var fileAlignments = CalculateFileRetentionTimeAlignments(retentionTimeSource.Name, allLibraryRetentionTimes);
                if (fileAlignments == null)
                    break;
                retentionTimes = new LibraryRetentionTimes(FilePath, AlignAndAverageAllRetentionTimes(newSources, fileAlignments));
                return true;
            }
            retentionTimes = null;
            return false;
        }

        public ResultNameMap<IDictionary<Target, double>> ReadAllRetentionTimes(ResultNameMap<RetentionTimeSource> sources)
        {
            var allRetentionTimes = new Dictionary<string, IDictionary<Target, double>>();
            foreach (var source in sources)
            {
                LibraryRetentionTimes libraryRetentionTimes;
                if (TryGetRetentionTimes(MsDataFileUri.Parse(source.Value.Name), out libraryRetentionTimes))
                    allRetentionTimes.Add(source.Key, libraryRetentionTimes.GetFirstRetentionTimes());
            }
            return ResultNameMap.FromDictionary(allRetentionTimes);
        }

        public static IList<AlignedRetentionTimes> CalculateFileRetentionTimeAlignments(
            string dataFileName, ResultNameMap<IDictionary<Target, double>> libraryRetentionTimes)
        {
            var alignments = new List<AlignedRetentionTimes>();

            new LongOperationRunner
            {
                JobTitle = Resources.BiblioSpecLiteLibrary_CalculateFileRetentionTimeAlignments_Aligning_library_retention_times
            }.Run(longWaitBroker =>
            {
                var targetTimes = libraryRetentionTimes.Find(dataFileName);
                foreach (var entry in libraryRetentionTimes)
                {
                    AlignedRetentionTimes aligned = null;
                    if (dataFileName != entry.Key)
                    {
                        try
                        {
                            aligned = AlignedRetentionTimes.AlignLibraryRetentionTimes(targetTimes, entry.Value, 0,
                                RegressionMethodRT.linear,
                                longWaitBroker.CancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            alignments = null;
                            throw;
                        }

                        if (aligned != null && aligned.RegressionPointCount < MIN_IRT_ALIGNMENT_POINT_COUNT)
                        {
                            alignments = null;
                            return;
                        }
                    }
                    alignments.Add(aligned);
                }
            });

            return alignments;
        }

        private IDictionary<Target, Tuple<TimeSource, double[]>> AlignAndAverageAllRetentionTimes(
            ResultNameMap<RetentionTimeSource> sources, IList<AlignedRetentionTimes> fileAlignments)
        {
            var allRetentionTimes = new Dictionary<Target, List<double>>();
            int i = 0;
            foreach (var source in sources)
            {
                LibraryRetentionTimes libraryRetentionTimes;
                if (TryGetRetentionTimes(MsDataFileUri.Parse(source.Value.Name), out libraryRetentionTimes))
                    AddAlignedRetentionTimes(libraryRetentionTimes, allRetentionTimes, fileAlignments[i]);
                i++;
            }
            return allRetentionTimes.ToDictionary(t => t.Key,
                t => new Tuple<TimeSource, double[]>(TimeSource.scan,
                    new[] { new Statistics(t.Value).Percentile(IrtStandard.GetSpectrumTimePercentile(t.Key)) }));
        }

        private void AddAlignedRetentionTimes(LibraryRetentionTimes libraryTimes, IDictionary<Target, List<double>> allRetentionTimes, AlignedRetentionTimes fileAlignment)
        {
            foreach (var measuredTime in libraryTimes.PeptideRetentionTimes)
            {
                List<double> peptideTimes;
                if (!allRetentionTimes.TryGetValue(measuredTime.PeptideSequence, out peptideTimes))
                {
                    peptideTimes = new List<double>();
                    allRetentionTimes.Add(measuredTime.PeptideSequence, peptideTimes);
                }
                var alignedTime = measuredTime.RetentionTime;
                if (fileAlignment != null)
                {
                    if (fileAlignment.RegressionRefined != null)
                    {
                        alignedTime = fileAlignment.RegressionRefined.Conversion.GetY(alignedTime);
                    }
                }
                peptideTimes.Add(alignedTime);
            }
        }

        public override bool TryGetIonMobilityInfos(LibKey key, MsDataFileUri filePath, out IonMobilityAndCCS[] ionMobilities)
        {
            int i = FindEntry(key);
            int j = FindSource(filePath);
            if (i != -1 && j != -1)
            {
                ionMobilities = _libraryEntries[i].IonMobilitiesByFileId.GetIonMobilityInfo(_librarySourceFiles[j].Id);
                return ionMobilities != null;
            }

            return base.TryGetIonMobilityInfos(key, filePath, out ionMobilities);
        }

        public override bool TryGetIonMobilityInfos(LibKey[] targetIons, MsDataFileUri filePath, out LibraryIonMobilityInfo ionMobilities)
        {
            return TryGetIonMobilityInfos(targetIons, FindSource(filePath), out ionMobilities);
        }

        public override bool TryGetIonMobilityInfos(LibKey[] targetIons, int fileIndex, out LibraryIonMobilityInfo ionMobilities)
        {
            if (fileIndex >= 0 && fileIndex < _librarySourceFiles.Length)
            {
                ILookup<LibKey, IonMobilityAndCCS[]> ionMobilitiesLookup;
                var source = _librarySourceFiles[fileIndex];
                if (targetIons != null)
                {
                    if (!targetIons.Any())
                    {
                        ionMobilities = null;
                        return true; // return value false means "that's not a proper file index"'
                    }

                    ionMobilitiesLookup = targetIons.SelectMany(target => _libraryEntries.ItemsMatching(target, true)).ToLookup(
                        entry => entry.Key,
                        entry => entry.IonMobilitiesByFileId.GetIonMobilityInfo(source.Id));
                }
                else
                {
                    ionMobilitiesLookup = _libraryEntries.ToLookup(
                        entry => entry.Key,
                        entry => entry.IonMobilitiesByFileId.GetIonMobilityInfo(source.Id));
                }
                var ionMobilitiesDict = ionMobilitiesLookup.Where(tl => !tl.IsNullOrEmpty() && tl.Any(i => i != null)).ToDictionary(
                    grouping => grouping.Key,
                    grouping =>
                    {
                        var array = grouping.SelectMany(values => values).Where(v => v != null && !v.IsEmpty).ToArray();
                        Array.Sort(array);
                        return array;
                    });
                var nonEmptyIonMobilitiesDict = ionMobilitiesDict
                    .Where(kvp => kvp.Value.Length > 0)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                ionMobilities = nonEmptyIonMobilitiesDict.Any() ? new LibraryIonMobilityInfo(source.FilePath, false, nonEmptyIonMobilitiesDict) : null;
                return true;  // return value false means "that's not a proper file index"'
            }

            return base.TryGetIonMobilityInfos(targetIons, fileIndex, out ionMobilities);
        }

        public override bool TryGetIonMobilityInfos(LibKey[] targetIons, out LibraryIonMobilityInfo ionMobilities)
        {
            if (targetIons != null && targetIons.Length > 0)
            {
                var ionMobilitiesDict = new Dictionary<LibKey, IonMobilityAndCCS[]>();
                foreach (var target in targetIons)
                {
                    foreach (var matchedItem in _libraryEntries.ItemsMatching(target, true))
                    {
                        var matchedTarget = matchedItem.Key;
                        var match = matchedItem.IonMobilitiesByFileId.AllValuesSorted;
                        if (match == null)
                            continue;
                        if (ionMobilitiesDict.TryGetValue(matchedTarget, out var mobilities))
                        {
                            var newMobilities = match.Concat(mobilities).ToArray();
                            Array.Sort(newMobilities);
                            ionMobilitiesDict[matchedTarget] = newMobilities;
                        }
                        else
                        {
                            ionMobilitiesDict[matchedTarget] = match;
                        }
                    }
                }
                if (!ionMobilitiesDict.Values.Any(v => v.Any()))
                {
                    ionMobilities = null;
                    return false;
                }
                ionMobilities = new LibraryIonMobilityInfo(FilePath, false, ionMobilitiesDict);
                return true;
            }

            return base.TryGetIonMobilityInfos(targetIons, out ionMobilities);
        }



        /// <summary>
        /// Reads all retention times for a specified source file into a dictionary by
        /// modified peptide sequence, with times stored in an array in ascending order.
        /// </summary>
        private IDictionary<string, double[]> ReadRetentionTimes(SQLiteConnection connection, BiblioLiteSourceInfo sourceInfo)
        {
            using (SQLiteCommand select = new SQLiteCommand(connection))
            {
                select.CommandText = @"SELECT peptideModSeq, t.retentionTime " +
                    @"FROM [RefSpectra] as s INNER JOIN [RetentionTimes] as t ON s.[id] = t.[RefSpectraID] " +
                    @"WHERE t.[SpectrumSourceId] = ? " +
                    @"ORDER BY peptideModSeq, t.retentionTime";
                select.Parameters.Add(new SQLiteParameter(DbType.UInt64, (long)sourceInfo.Id));

                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                    var dictKeyTimes = new Dictionary<string, double[]>();
                    string sequence = null;
                    var listTimes = new List<double>();
                    while (reader.Read())
                    {
                        int i = 0;
                        string sequenceNext = reader.GetString(i++);
                        double time = reader.GetDouble(i);
                        if (!Equals(sequence, sequenceNext))
                        {
                            if (sequence != null && listTimes.Count > 0)
                                dictKeyTimes.Add(sequence, listTimes.ToArray());

                            sequence = sequenceNext;
                            listTimes.Clear();                            
                        }
                        listTimes.Add(time);
                    }
                    if (sequence != null && listTimes.Count > 0)
                        dictKeyTimes.Add(sequence, listTimes.ToArray());
                    return dictKeyTimes;
                }
            }
        }

        private int FindSource(MsDataFileUri filePath)
        {
            if (filePath == null)
            {
                return -1;
            }
            string filePathToString = filePath.ToString();
            // First look for an exact path match
            int i = _librarySourceFiles.IndexOf(info => Equals(filePathToString, info.FilePath));
            // filePath.ToString may include decorators e.g. "C:\\data\\mydata.raw?centroid_ms1=true", try unadorned name ("mydata.raw")
            if (i == -1)
                i = _librarySourceFiles.IndexOf(info => Equals(filePath.GetFileName(), info.FilePath));
            // Or a straight basename match, which we sometimes use internally
            if (i == -1)
                i = _librarySourceFiles.IndexOf(info => Equals(filePathToString, info.BaseName));
            // NOTE: We don't expect multi-part wiff files to appear in a library
            if (i == -1 && null == filePath.GetSampleName())
            {
                try
                {
                    // Failing an exact path match, look for a basename match
                    string baseName = filePath.GetFileNameWithoutExtension();
                    i = _librarySourceFiles.IndexOf(info => MeasuredResults.IsBaseNameMatch(baseName, info.BaseName));
                }
                catch (ArgumentException)
                {
                    // Handle: Illegal characters in path
                }
            }
            return i;
        }

        public override IEnumerable<SpectrumInfoLibrary> GetSpectra(LibKey key, IsotopeLabelType labelType, LibraryRedundancy redundancy)
        {
            if (redundancy == LibraryRedundancy.best && SchemaVersion < 1)
            {
                // Retention time information is not available for schema (minor version) < 1
                return base.GetSpectra(key, labelType, redundancy);
            }

            try
            {
                return GetRedundantSpectra(key, labelType, redundancy);
            }
            // In case there is no RetentionTimes table
            // CONSIDER: Could also be a failure to read the SQLite file
            catch (SQLiteException)
            {
                return base.GetSpectra(key, labelType, redundancy);
            }
        }

        private IEnumerable<SpectrumInfoLibrary> GetRedundantSpectra(LibKey key, IsotopeLabelType labelType, LibraryRedundancy redundancy)
        {
            // No redundant spectra before schema version 1
            if (SchemaVersion == 0)
                return new SpectrumInfoLibrary[0];
            int i = FindEntry(key);
            if (i == -1)
                return new SpectrumInfoLibrary[0];

            var hasRetentionTimesTable = RetentionTimesPsmCount() != 0;
            var info = _libraryEntries[i];
            var protein = info.Protein;
            using (SQLiteCommand select = new SQLiteCommand(_sqliteConnection.Connection))
            {
                select.CommandText = hasRetentionTimesTable
                    ? @"SELECT * " +
                      @"FROM [RetentionTimes] as t INNER JOIN [SpectrumSourceFiles] as s ON t.[SpectrumSourceID] = s.[id] " +
                      @"WHERE t.[RefSpectraID] = ?":
                    @"SELECT * " +
                    @"FROM [RefSpectra] as t INNER JOIN [SpectrumSourceFiles] as s ON t.[FileID] = s.[id] " +
                    @"WHERE t.[id] = ?";
                if (hasRetentionTimesTable && redundancy == LibraryRedundancy.best)
                    select.CommandText += @" AND t.[bestSpectrum] = 1";

                select.Parameters.Add(new SQLiteParameter(DbType.UInt64, (long)info.Id));

                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                    int iFilePath = reader.GetOrdinal(SpectrumSourceFiles.fileName);
                    int iRedundantId = reader.GetOrdinal(RetentionTimes.RedundantRefSpectraID);
                    int iRetentionTime = reader.GetOrdinal(RetentionTimes.retentionTime);
                    int iBestSpectrum = reader.GetOrdinal(RetentionTimes.bestSpectrum);
                    bool hasDTvsCCS = (SchemaVersion > 1) && (SchemaVersion < 4); // Initially we saw DT and CCS as mutually exclusive values
                    bool hasDriftTime = reader.GetOrdinal(RetentionTimes.driftTimeMsec) >= 0;  // Then we went to saving both DT and CCS
                    bool hasGeneralIonMobility = (SchemaVersion >= 6); // And now we have CCS and generalized ion mobility with a type declaration (DT, inverse reduced IM, others to come?)
                    int iDriftTimeVsCCS = hasDTvsCCS
                        ? reader.GetOrdinal(RetentionTimes.ionMobilityType)
                        : int.MinValue;
                    hasDTvsCCS &= iDriftTimeVsCCS >= 0;
                    int iDTorCCS= hasDTvsCCS
                        ? reader.GetOrdinal(RetentionTimes.ionMobilityValue)
                        : int.MinValue;
                    int iIonMobility = 0;
                    int iIonMobilityType = 0;
                    int iCCS = 0;
                    int iIonMobilityHighEnergyOffset = 0;
                    if (hasGeneralIonMobility)
                    {
                        iIonMobility = reader.GetOrdinal(RetentionTimes.ionMobility);
                        iIonMobilityHighEnergyOffset = reader.GetOrdinal(RetentionTimes.ionMobilityHighEnergyOffset);
                        iCCS = reader.GetOrdinal(RetentionTimes.collisionalCrossSectionSqA);
                        iIonMobilityType = reader.GetOrdinal(RetentionTimes.ionMobilityType);
                    }
                    else if (hasDriftTime)
                    {
                        iIonMobility = reader.GetOrdinal(RetentionTimes.driftTimeMsec);
                        iIonMobilityHighEnergyOffset = reader.GetOrdinal(RetentionTimes.driftTimeHighEnergyOffsetMsec);
                        iCCS = reader.GetOrdinal(RetentionTimes.collisionalCrossSectionSqA);
                    }
                    else if (hasDTvsCCS)
                    {
                        iDTorCCS = reader.GetOrdinal(RetentionTimes.ionMobilityValue);
                        iDriftTimeVsCCS = reader.GetOrdinal(RetentionTimes.ionMobilityType);
                    }
                    var listSpectra = new List<SpectrumInfoLibrary>();
                    while (reader.Read())
                    {
                        string filePath = reader.GetString(iFilePath);
                        int redundantId = iRedundantId < 0 ? -1 : reader.GetInt32(iRedundantId);
                        double retentionTime = reader.GetDouble(iRetentionTime);
                        bool isBest = !hasRetentionTimesTable || reader.GetInt16(iBestSpectrum) != 0;

                        IonMobilityAndCCS ionMobilityInfo = IonMobilityAndCCS.EMPTY;
                        if (hasGeneralIonMobility)
                        {
                            var ionMobilityType = (eIonMobilityUnits)NullSafeToInteger(reader.GetValue(iIonMobilityType));
                            if (!ionMobilityType.Equals(eIonMobilityUnits.none))
                            {
                                var ionMobility = UtilDB.GetNullableDouble(reader, iIonMobility);
                                var collisionalCrossSectionSqA = UtilDB.GetNullableDouble(reader, iCCS);
                                var ionMobilityHighEnergyOffset = UtilDB.GetNullableDouble(reader, iIonMobilityHighEnergyOffset);
                                if (!(ionMobility == 0 && collisionalCrossSectionSqA == 0 && ionMobilityHighEnergyOffset == 0))
                                    ionMobilityInfo = IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(ionMobility, ionMobilityType), collisionalCrossSectionSqA, ionMobilityHighEnergyOffset);
                            }
                        }
                        else if (hasDriftTime) 
                        {
                            var driftTimeMsec = reader.GetDouble(iIonMobility);
                            var collisionalCrossSectionSqA = reader.GetDouble(iCCS);
                            var highEnergyDriftTimeOffsetMsec = reader.GetDouble(iIonMobilityHighEnergyOffset);
                            if (!(driftTimeMsec == 0 && collisionalCrossSectionSqA == 0 && highEnergyDriftTimeOffsetMsec == 0))
                               ionMobilityInfo =  IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(driftTimeMsec, eIonMobilityUnits.drift_time_msec) , collisionalCrossSectionSqA, highEnergyDriftTimeOffsetMsec);
                        }
                        else if (hasDTvsCCS)
                        {
                            // Ancient formats had CCS and DT as either/or
                            var type = NullSafeToInteger(reader.GetValue(iDriftTimeVsCCS));
                            if (type > 0)
                            {
                                double? val = reader.GetDouble(iDTorCCS);
                                var ionMobility = type == 1 
                                    ? IonMobilityValue.GetIonMobilityValue(val, eIonMobilityUnits.drift_time_msec)
                                    : IonMobilityValue.EMPTY;
                                ionMobilityInfo =  IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobility, type != 1 ? val : null,
                                    (iIonMobilityHighEnergyOffset > 0) ? reader.GetDouble(iIonMobilityHighEnergyOffset) : 0);
                            }
                        }

                        // If this is not a reference(best) spectrum, the spectrumKey should be of the 
                        // type SpectrumLiteKey so that peaks for this spectrum are loaded from the
                        // redundant library. Otherwise, for best spectra, the key should simply be 
                        // the id of this spectrum in the cache.
                        // Look at the LoadSpectrum method to see how spectrumKey is used.
                        object spectrumKey = i;
                        if (!isBest || redundancy == LibraryRedundancy.all_redundant)
                            spectrumKey = new SpectrumLiteKey(i, redundantId, isBest);
                        listSpectra.Add(new SpectrumInfoLibrary(this, labelType, filePath, retentionTime, ionMobilityInfo, protein, isBest,
                                                         spectrumKey)
                                            {
                                                SpectrumHeaderInfo = CreateSpectrumHeaderInfo(_libraryEntries[i])
                                            });
                    }
                    return listSpectra;
                }
            }
        }

        /// <summary>
        /// Query the RetentionTimes table for total PSM count.
        /// </summary>
        /// <returns>
        /// 0 if schema version is 0
        /// 0 if schema version > 0 and RetentionTimes table does not exist (e.g. redundant libraries)
        /// number of entries in the RetentionTimes table, otherwise.
        /// </returns> 
        private int RetentionTimesPsmCount()
        {
            // No redundant spectra before schema version 1
            if (SchemaVersion == 0)
                return 0;

            if (!SqliteOperations.TableExists(_sqliteConnection.Connection, @"RetentionTimes"))
            {
                // SchemaVersion 1 does not have RetentionTimes table for redundant libraries. 
                return 0;
            }

            using (SQLiteCommand select = new SQLiteCommand(_sqliteConnection.Connection))
            {
                select.CommandText = @"SELECT count(*) FROM [RetentionTimes]";

                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                        if (!reader.Read())
                        throw new InvalidDataException(string.Format(Resources.BiblioSpecLiteLibrary_RetentionTimesPsmCount_Unable_to_get_a_valid_count_of_all_spectra_in_the_library__0__, FilePath));
                    int rows = reader.GetInt32(0);
                    return rows;
                }
            }
        }

        private bool HasRedundantModificationsTable()
        {
            return SqliteOperations.TableExists(_sqliteConnectionRedundant.Connection, @"Modifications");
        }

        public void DeleteDataFiles(string[] filenames, IProgressMonitor monitor)
        {
            string inList = GetInList(filenames);
            bool hasModsTable = HasRedundantModificationsTable();
 
            // Make the changes to the redundant library and then use BlibFilter
            using (var myTrans = _sqliteConnectionRedundant.Connection.BeginTransaction(IsolationLevel.ReadCommitted))
            // ReSharper disable LocalizableElement
            using (var sqCommand = _sqliteConnectionRedundant.Connection.CreateCommand())
            {
                if (hasModsTable)
                {
                    sqCommand.CommandText = "DELETE FROM [Modifications] WHERE id IN " + "" +
                                                "(SELECT m.id FROM [Modifications] as m " + 
                                                    "INNER JOIN [RefSpectra] as s ON m.RefSpectraId = s.id " +
                                                    "INNER JOIN [SpectrumSourceFiles] as f ON s.fileId = f.id " +
                                                    "WHERE f.fileName IN " + inList + ")";
                    sqCommand.ExecuteNonQuery();
                }

                sqCommand.CommandText = "DELETE FROM [RefSpectraPeaks] WHERE RefSpectraId IN " + 
                                            "(SELECT p.RefSpectraId FROM [RefSpectraPeaks] as p " +
                                                "INNER JOIN [RefSpectra] as s ON p.RefSpectraId = s.id " +
                                                "INNER JOIN [SpectrumSourceFiles] as f ON s.fileId = f.id " +
                                                "WHERE f.fileName IN " + inList + ")";
                sqCommand.ExecuteNonQuery();

                sqCommand.CommandText = "DELETE FROM [RefSpectra] WHERE id IN " + 
                                            "(SELECT s.id FROM [RefSpectra] as s " + 
                                                "INNER JOIN [SpectrumSourceFiles] as f ON s.fileId = f.id " +
                                                "WHERE f.fileName IN " + inList + ")";
                sqCommand.ExecuteNonQuery();

                sqCommand.CommandText = "DELETE FROM SpectrumSourceFiles WHERE fileName IN " + inList;
                sqCommand.ExecuteNonQuery();

                myTrans.Commit();
            }
            // ReSharper restore LocalizableElement

            // Write the non-redundant library to a temporary file first
            using (var saver = new FileSaver(FilePath))
            {
                var blibFilter = new BlibFilter();
                IProgressStatus status = new ProgressStatus(Resources.BiblioSpecLiteLibrary_DeleteDataFiles_Removing_library_runs_from_document_library_);
                if (!blibFilter.Filter(FilePathRedundant, saver.SafeName, monitor, ref status))
                    throw new IOException(string.Format(Resources.BiblioSpecLiteLibrary_DeleteDataFiles_Failed_attempting_to_filter_redundant_library__0__to__1_, FilePathRedundant, FilePath));

                _sqliteConnectionRedundant.CloseStream();
                _sqliteConnection.CloseStream();
                saver.Commit();
            }
        }

        private string GetInList(string[] filenames)
        {
            var sb = new StringBuilder();
            foreach (string filename in filenames)
            {
                if (sb.Length > 0)
                    sb.Append(',');
                sb.Append('\'').Append(filename).Append('\'');
            }
            return @"(" + sb + @")";
        }

        private static int NullSafeToInteger(object value)
        {
            if (value is DBNull)
            {
                return 0;
            }
            return Convert.ToInt32(value);
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        private BiblioSpecLiteLibrary()
        {
            _librarySourceFiles = new BiblioLiteSourceInfo[0];
        }

        private enum ATTR
        {
            lsid,
            revision
        }

        public static BiblioSpecLiteLibrary Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new BiblioSpecLiteLibrary());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            Lsid = reader.GetAttribute(ATTR.lsid);
            Revision = (int) reader.GetFloatAttribute(ATTR.revision, 0);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.lsid, Lsid);
            writer.WriteAttribute(ATTR.revision, Revision);
        }

        #endregion

        #region object overrides

        public bool Equals(BiblioSpecLiteLibrary obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) &&
                Equals(obj.Lsid, Lsid) &&
                obj.Revision == Revision &&
                Equals(obj.FilePath, FilePath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as BiblioSpecLiteLibrary);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (Lsid != null ? Lsid.GetHashCode() : 0);
                result = (result*397) ^ Revision.GetHashCode();
                result = (result*397) ^ (FilePath != null ? FilePath.GetHashCode() : 0);
                return result;
            }
        }

        #endregion

        private struct BiblioLiteSourceInfo
        {
            public BiblioLiteSourceInfo(int id, string filePath, string idFilePath) : this()
            {
                Id = id;
                FilePath = filePath;
                IdFilePath = idFilePath;
            }

            public int Id { get; private set; }
            public string FilePath { get; private set; } // File from which the spectra were taken (may be same as idFilePath if spectra are taken from search file)
            public string IdFilePath { get; private set; } // File from which the IDs were taken (e.g. search results file from Mascot etc)

            public string BaseName
            {
                get
                {
                    try
                    {
                        return Path.GetFileNameWithoutExtension(FilePath);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }
        }

        class RetentionTimeReader
        {
            enum Column
            {
                RefSpectraID,
                SpectrumSourceID,
                retentionTime,
                driftTimeMsec,
                collisionalCrossSectionSqA,
                driftTimeHighEnergyOffsetMsec,
                ionMobilityType,
                ionMobilityValue,
                ionMobilityHighEnergyDriftTimeOffsetMsec,
                startTime,
                endTime,
                ionMobility,
                ionMobilityHighEnergyOffset,
                score,
                MAX_COLUMN
            }

            private int?[] _columnIndexes;
            private int _schemaVer;
            private IDataReader _reader;

            public RetentionTimeReader(IDataReader dataReader, int schemaVer)
            {
                PeakBoundaries = new List<KeyValuePair<int, KeyValuePair<int, ExplicitPeakBounds>>>();
                SpectraIdFileIdIonMobilities = new List<KeyValuePair<int, KeyValuePair<int, IonMobilityAndCCS>>>();
                SpectaIdFileIdTimes = new List<KeyValuePair<int, KeyValuePair<int, double>>>();
                _schemaVer = schemaVer;
                _columnIndexes = new int?[(int) Column.MAX_COLUMN];
                _reader = dataReader;
                for (int colEnum = 0; colEnum < (int) Column.MAX_COLUMN; colEnum++)
                {
                    string columnName = ((Column)colEnum).ToString();
                    int ordinal = dataReader.GetOrdinal(columnName);
                    if (ordinal >= 0)
                    {
                        _columnIndexes[colEnum] = ordinal;
                    }
                }
            }

            public List<KeyValuePair<int, KeyValuePair<int, double>>> SpectaIdFileIdTimes { get; private set; }

            // List of <RefSpectra Id, <FileId, ionMobility>>
            public List<KeyValuePair<int, KeyValuePair<int, IonMobilityAndCCS>>> SpectraIdFileIdIonMobilities { get; private set;
            }

            public List<KeyValuePair<int, KeyValuePair<int, ExplicitPeakBounds>>> PeakBoundaries
            {
                get;
                private set;
            }


            public void ReadAllRows()
            {
                while (_reader.Read())
                {
                    int? refSpectraId = GetInt(Column.RefSpectraID);
                    int? spectrumSourceId = GetInt(Column.SpectrumSourceID);
                    if (!refSpectraId.HasValue || !spectrumSourceId.HasValue)
                    {
                        continue;
                    }
                    double? retentionTime = ReadRetentionTime();
                    if (retentionTime.HasValue)
                    {
                        SpectaIdFileIdTimes.Add(new KeyValuePair<int, KeyValuePair<int, double>>(refSpectraId.Value,
                            new KeyValuePair<int, double>(spectrumSourceId.Value, retentionTime.Value)));
                    }
                    IonMobilityAndCCS ionMobilityInfo = ReadIonMobilityInfo();
                    if (!IonMobilityAndCCS.IsNullOrEmpty(ionMobilityInfo))
                    {
                        SpectraIdFileIdIonMobilities.Add(
                            new KeyValuePair<int, KeyValuePair<int, IonMobilityAndCCS>>(refSpectraId.Value,
                                new KeyValuePair<int, IonMobilityAndCCS>(spectrumSourceId.Value, ionMobilityInfo)));
                    }
                    var peakBounds = ReadPeakBounds();
                    if (peakBounds != null)
                    {
                        PeakBoundaries.Add(
                            new KeyValuePair<int, KeyValuePair<int, ExplicitPeakBounds>>(refSpectraId.Value,
                                new KeyValuePair<int, ExplicitPeakBounds>(spectrumSourceId.Value, peakBounds)));
                    }
                }
            }

            public double? ReadRetentionTime()
            {
                return GetDouble(Column.retentionTime);
            }

            public IonMobilityAndCCS ReadIonMobilityInfo()
            {
                if (_schemaVer < 2)
                {
                    return null;
                }
                switch (_schemaVer)
                {
                    default:
                    {
                        double mobility = GetDouble(Column.ionMobility).GetValueOrDefault();
                        double collisionalCrossSection =
                            GetDouble(Column.collisionalCrossSectionSqA).GetValueOrDefault();
                        double highEnergyOffset =
                            GetDouble(Column.ionMobilityHighEnergyOffset).GetValueOrDefault();
                        var units = (eIonMobilityUnits)GetInt(Column.ionMobilityType).GetValueOrDefault();
                        if (mobility == 0 && collisionalCrossSection == 0 &&
                            highEnergyOffset == 0)
                        {
                            return IonMobilityAndCCS.EMPTY;
                        }
                        return IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(mobility, units),
                            collisionalCrossSection, highEnergyOffset);
                    }
                    case 5:
                    case 4:
                    {
                        double driftTimeMsec = GetDouble(Column.driftTimeMsec).GetValueOrDefault();
                        double collisionalCrossSection =
                            GetDouble(Column.collisionalCrossSectionSqA).GetValueOrDefault();
                        double highEnergyOffset =
                            GetDouble(Column.driftTimeHighEnergyOffsetMsec).GetValueOrDefault();
                        if (driftTimeMsec == 0 && collisionalCrossSection == 0 &&
                            highEnergyOffset == 0)
                        {
                            return IonMobilityAndCCS.EMPTY;
                        }
                        return IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(driftTimeMsec, eIonMobilityUnits.drift_time_msec),
                            collisionalCrossSection, highEnergyOffset);
                    }
                    case 3:
                    case 2:
                    {
                        int ionMobilityType = GetInt(Column.ionMobilityType).GetValueOrDefault();
                        double ionMobilityValue = GetDouble(Column.ionMobilityValue).GetValueOrDefault();
                        double highEnergyOffset = GetDouble(Column.ionMobilityHighEnergyDriftTimeOffsetMsec).GetValueOrDefault();
                        if (ionMobilityValue == 0 && highEnergyOffset == 0)
                        {
                            return IonMobilityAndCCS.EMPTY;
                        }
                        bool isCcs = ionMobilityType == (int) IonMobilityType.collisionalCrossSection;
                        return IonMobilityAndCCS.GetIonMobilityAndCCS(isCcs ? IonMobilityValue.EMPTY : IonMobilityValue.GetIonMobilityValue(ionMobilityValue, eIonMobilityUnits.drift_time_msec), isCcs ? ionMobilityValue : (double?)null, highEnergyOffset);
                    }
                }
            }

            public ExplicitPeakBounds ReadPeakBounds()
            {
                double? startTime = GetDouble(Column.startTime);
                double? endTime = GetDouble(Column.endTime);
                double score = GetDouble(Column.score) ?? ExplicitPeakBounds.UNKNOWN_SCORE;
                if (startTime.HasValue && endTime.HasValue)
                {
                    return new ExplicitPeakBounds(startTime.Value, endTime.Value, score);
                }
                return null;
            }

            private object GetValue(Column column)
            {
                int? columnIndex = _columnIndexes[(int) column];
                if (!columnIndex.HasValue)
                {
                    return null;
                }
                object value = _reader.GetValue(columnIndex.Value);
                if (value is DBNull)
                {
                    return null;
                }
                return value;
            }

            private double? GetDouble(Column column)
            {
                object value = GetValue(column);
                if (value == null)
                {
                    return null;
                }
                return Convert.ToDouble(value);
            }

            private int? GetInt(Column column)
            {
                object value = GetValue(column);
                if (value == null)
                {
                    return null;
                }
                return Convert.ToInt32(value);
            }
        }

        public static PeptideLibraryKey MakePrecisePeptideLibraryKey(PeptideLibraryKey impreciseLibraryKey,
            IList<Tuple<int, double>> modificationMasses)
        {
            var modificationIndexes = new HashSet<int>(modificationMasses.Select(tuple => tuple.Item1 - 1));
            var expectedModificationIndexes =
                new HashSet<int>(impreciseLibraryKey.GetModifications().Select(mod => mod.Key));
            if (!expectedModificationIndexes.SetEquals(modificationIndexes))
            {
                return impreciseLibraryKey;
            }
            IList<KeyValuePair<int, string>> newModifications = impreciseLibraryKey.GetModifications()
                .Where(mod => !modificationIndexes.Contains(mod.Key)).ToList();
            foreach (var modMass in modificationMasses)
            {
                var massModification = MassModification.FromMass(modMass.Item2);
                newModifications.Add(new KeyValuePair<int, string>(modMass.Item1 - 1, massModification.ToString()));
            }
            StringBuilder newSequence = new StringBuilder();
            int aaCount = 0;
            var unmodifiedSequence = impreciseLibraryKey.UnmodifiedSequence;
            foreach (var mod in newModifications.OrderBy(mod => mod.Key))
            {
                newSequence.Append(unmodifiedSequence.Substring(aaCount, mod.Key + 1 - aaCount));
                aaCount = mod.Key + 1;
                newSequence.Append(ModifiedSequence.Bracket(mod.Value));
            }
            newSequence.Append(unmodifiedSequence.Substring(aaCount));
            return new PeptideLibraryKey(newSequence.ToString(), impreciseLibraryKey.Charge);
        }
    }

    public sealed class SpectrumLiteKey
    {
        public SpectrumLiteKey(int nonRedundantId, int redundantId, bool isBest)
        {
            NonRedundantId = nonRedundantId;
            RedundantId = redundantId;
            IsBest = isBest;
        }

        public int NonRedundantId { get; private set; }
        public int RedundantId { get; private set; }
        public bool IsBest { get; private set; }
    }

    public struct IndexedRetentionTimes
    {
        private readonly ImmutableSortedList<int, float[]> _timesById;
        public IndexedRetentionTimes(IEnumerable<KeyValuePair<int, double>> times)
        {
            var timesLookup = times.ToLookup(kvp => kvp.Key, kvp => kvp.Value);
            var floatArrayPairs = timesLookup.Select(grouping => new KeyValuePair<int, float[]>(
                    grouping.Key, grouping.Select(time => (float) time).ToArray()));
            var timesById = ImmutableSortedList.FromValues(floatArrayPairs);
            if (timesById.Count > 0)
            {
                _timesById = timesById;
            }
            else
            {
                _timesById = null;
            }
        }

        private IndexedRetentionTimes(IEnumerable<KeyValuePair<int, float[]>> timesById)
        {
            _timesById = ImmutableSortedList.FromValues(timesById);
        }

        public double[] GetTimes(int id)
        {
            float[] times;
            if (null == _timesById || !_timesById.TryGetValue(id, out times))
            {
                return new double[0];
            }
            return times.Select(time => (double) time).ToArray();
        }
      
        public void Write(Stream stream)
        {
            if (_timesById == null)
            {
                PrimitiveArrays.WriteOneValue(stream, 0);
                return;
            }
            PrimitiveArrays.WriteOneValue(stream, _timesById.Count);
            foreach (KeyValuePair<int, float[]> idTimesPair in _timesById)
            {
                PrimitiveArrays.WriteOneValue(stream, idTimesPair.Key);
                PrimitiveArrays.WriteOneValue(stream, idTimesPair.Value.Length);
                PrimitiveArrays.Write(stream, idTimesPair.Value);
            }
        }

        public static IndexedRetentionTimes Read(Stream stream)
        {
            int entryCount = PrimitiveArrays.ReadOneValue<int>(stream);
            if (0 == entryCount)
            {
                return default(IndexedRetentionTimes);
            }
            var keyValuePairs = new KeyValuePair<int, float[]>[entryCount];
            for (int i = 0; i < keyValuePairs.Length; i++)
            {
                int id = PrimitiveArrays.ReadOneValue<int>(stream);
                int timeCount = PrimitiveArrays.ReadOneValue<int>(stream);
                var times = PrimitiveArrays.Read<float>(stream, timeCount);
                keyValuePairs[i] = new KeyValuePair<int, float[]>(id, times);
            }
            return new IndexedRetentionTimes(keyValuePairs);
        }
    }

    public struct IndexedIonMobilities
    {
        private readonly ImmutableSortedList<int, IonMobilityAndCCS[]> _ionMobilityById; 
        public IndexedIonMobilities(IEnumerable<KeyValuePair<int, IonMobilityAndCCS>> times)
        {
            var timesLookup = times.ToLookup(kvp => kvp.Key, kvp => kvp.Value);
            var infoArrayPairs = timesLookup.Select(grouping => new KeyValuePair<int, IonMobilityAndCCS[]>(
                    grouping.Key, grouping.ToArray()));
            var timesById = ImmutableSortedList.FromValues(infoArrayPairs);
            if (timesById.Count > 0)
            {
                _ionMobilityById = timesById;
            }
            else
            {
                _ionMobilityById = null;
            }
        }

        private IndexedIonMobilities(IEnumerable<KeyValuePair<int, IonMobilityAndCCS[]>> timesById)
        {
            _ionMobilityById = ImmutableSortedList.FromValues(timesById);
        }

        public bool IsEmpty => _ionMobilityById == null || _ionMobilityById.Count == 0;

        public IonMobilityAndCCS[] AllValuesSorted
        {
            get
            {
                if (null == _ionMobilityById)
                    return null;

                var val = _ionMobilityById.Values.SelectMany(i => i).ToArray();
                Array.Sort(val);
                return val;
            }
        }

        public IonMobilityAndCCS[] GetIonMobilityInfo(int id)
        {
            if (null == _ionMobilityById || !_ionMobilityById.TryGetValue(id, out var times))
            {
                return null;
            }
            return times;
        }

        public void Write(Stream stream)
        {
            if (_ionMobilityById == null)
            {
                PrimitiveArrays.WriteOneValue(stream, 0);
                return;
            }
            PrimitiveArrays.WriteOneValue(stream, _ionMobilityById.Count);
            foreach (KeyValuePair<int, IonMobilityAndCCS[]> idTimesPair in _ionMobilityById)
            {
                PrimitiveArrays.WriteOneValue(stream, idTimesPair.Key);
                PrimitiveArrays.WriteOneValue(stream, idTimesPair.Value.Length);
                foreach (var driftTimeInfo in idTimesPair.Value)
                {
                    driftTimeInfo.Write(stream);
                }
            }
        }

        public static IndexedIonMobilities Read(Stream stream)
        {
            int entryCount = PrimitiveArrays.ReadOneValue<int>(stream);
            if (0 == entryCount)
            {
                return default(IndexedIonMobilities);
            }
            var keyValuePairs = new KeyValuePair<int, IonMobilityAndCCS[]>[entryCount];
            for (int i = 0; i < keyValuePairs.Length; i++)
            {
                int id = PrimitiveArrays.ReadOneValue<int>(stream);
                int driftTimeCount = PrimitiveArrays.ReadOneValue<int>(stream);
                var driftTimes = new List<IonMobilityAndCCS>();
                for (int j = 0; j < driftTimeCount; j++)
                {
                    var ionMobilityInfo = IonMobilityAndCCS.Read(stream);
                    driftTimes.Add(ionMobilityInfo);
                }
                keyValuePairs[i] = new KeyValuePair<int, IonMobilityAndCCS[]>(id, driftTimes.ToArray());
            }
            return new IndexedIonMobilities(keyValuePairs);
        }
    }

    public struct BiblioLiteSpectrumInfo : ICachedSpectrumInfo
    {
        public BiblioLiteSpectrumInfo(LibKey key, int copies, int numPeaks, int id, string protein)
            : this(key, copies, numPeaks, id, protein, default(IndexedRetentionTimes), default(IndexedIonMobilities), 
            ImmutableSortedList<int, ExplicitPeakBounds>.EMPTY, null, null)
        {
        }

        public BiblioLiteSpectrumInfo(LibKey key, int copies, int numPeaks, int id, string protein,
            IndexedRetentionTimes retentionTimesByFileId, IndexedIonMobilities ionMobilitiesByFileId,
            ImmutableSortedList<int, ExplicitPeakBounds> peakBoundaries, double? score, string scoreType)
        {
            Key = key;
            Copies = copies;
            NumPeaks = numPeaks;
            Id = id;
            Protein = protein;
            RetentionTimesByFileId = retentionTimesByFileId;
            IonMobilitiesByFileId = ionMobilitiesByFileId;
            PeakBoundariesByFileId = peakBoundaries;
            Score = score;
            ScoreType = scoreType;
        }

        public LibKey Key { get; }
        public SmallMoleculeLibraryAttributes SmallMoleculeLibraryAttributes { get { return Key.SmallMoleculeLibraryAttributes; } }
        public int Copies { get; }
        public int NumPeaks { get; }
        public int Id { get; }
        public string Protein { get; } // From the RefSpectraProteins table, either a protein accession or an arbitrary molecule list name
        public IndexedRetentionTimes RetentionTimesByFileId { get; }
        public IndexedIonMobilities IonMobilitiesByFileId { get; }
        public ImmutableSortedList<int, ExplicitPeakBounds> PeakBoundariesByFileId { get; }
        public double? Score { get; }
        public string ScoreType { get; }
    }
}
