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
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Dapper;
using JetBrains.Annotations;
using pwiz.BiblioSpec;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Database;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
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
            get { return TextUtil.FileDialogFilter(LibResources.BiblioSpecLiteSpec_FILTER_BLIB_BiblioSpec_Library, EXT); }
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

        public BiblioSpecLiteSpec(string name, string path, bool useExplicitPeakBounds = true)
            : base(name, path, useExplicitPeakBounds)
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

        public override string GetLibraryTypeName()
        {
            return LibResources.BiblioSpecLiteSpec_FILTER_BLIB_BiblioSpec_Library;
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
        public const string DEFAULT_AUTHORITY = "proteome.gs.washington.edu";
        private PooledSqliteConnection _sqliteConnection;
        private PooledSqliteConnection _sqliteConnectionRedundant;

        private BiblioLiteSourceInfo[] _librarySourceFiles;
        private LibraryFiles _libraryFiles = LibraryFiles.EMPTY;
        private bool _anyExplicitPeakBounds;

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
            _librarySourceFiles = Array.Empty<BiblioLiteSourceInfo>();
            _libraryFiles = LibraryFiles.EMPTY;
        }

        protected override LibrarySpec CreateSpec()
        {
            return new BiblioSpecLiteSpec(Name, FilePath);
        }

        public override string SpecFilter
        {
            get { return TextUtil.FileDialogFilterAll(LibResources.BiblioSpecLiteLibrary_SpecFilter_BiblioSpec_Library, BiblioSpecLiteSpec.EXT); }
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
                return _libraryFiles;
            }
        }

        public override LibraryDetails LibraryDetails
        {
            get
            {
                var dataFiles = GetDataFileDetails();
                var uniquePeptideCount = Keys.Select(entry => entry.Target.IsProteomic ? entry.Target.Sequence : entry.Target.Molecule.ToSerializableString()).Distinct().Count();

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

        private class ScoreTypeRow
        {
            public string ScoreType { get; set; }
            public string ProbabilityType { get; set; }
        }

        public IEnumerable<SpectrumSourceFileDetails> GetDataFileDetails()
        {
            var detailsByFileId = new Dictionary<int, SpectrumSourceFileDetails>();
            var scoreTypesByFileId = new Dictionary<int, HashSet<string>>();
            Dictionary<string, ScoreType> scoreTypesByName = new Dictionary<string, ScoreType>();
            try
            {
                lock (_sqliteConnection)
                {
                    foreach (var scoreTypeRow in _sqliteConnection.Connection.Query<ScoreTypeRow>(
                                 @"SELECT * FROM ScoreTypes"))
                    {
                        var scoreType = new ScoreType(scoreTypeRow.ScoreType, scoreTypeRow.ProbabilityType);
                        scoreTypesByName[scoreType.NameInvariant] = scoreType;
                    }
                }
            }
            catch
            {
                // Ignore
            }
            foreach (var spectrumSourceFile in _librarySourceFiles)
            {
                detailsByFileId.Add(spectrumSourceFile.Id, new SpectrumSourceFileDetails(spectrumSourceFile.FilePath, spectrumSourceFile.IdFilePath, spectrumSourceFile.WorkflowType));
                scoreTypesByFileId.Add(spectrumSourceFile.Id, new HashSet<string>());
            }

            if (_libraryEntries != null)
            {
                foreach (var entry in _libraryEntries)
                {
                    if (entry.SpectrumSourceId.HasValue)
                    {
                        if (detailsByFileId.TryGetValue(entry.SpectrumSourceId.Value, out var bestDetails))
                        {
                            bestDetails.BestSpectrum++;
                            scoreTypesByFileId[entry.SpectrumSourceId.Value].Add(entry.ScoreType);
                        }
                    }

                    foreach (var idTimes in entry.RetentionTimesByFileId.GetTimesById())
                    {
                        if (detailsByFileId.TryGetValue(idTimes.Key, out var details))
                        {
                            details.MatchedSpectrum += idTimes.Value.Count;
                        }
                    }
                }
            }

            foreach (var file in _librarySourceFiles)
            {
                if (scoreTypesByFileId.TryGetValue(file.Id, out var scoreTypes))
                {
                    foreach (var scoreTypeName in scoreTypes)
                    {
                        if (scoreTypeName != null && scoreTypesByName.TryGetValue(scoreTypeName, out var scoreType))
                        {
                            detailsByFileId[file.Id].ScoreThresholds.Add(scoreType, file.CutoffScore);
                        }
                    }
                }
            }

            return _librarySourceFiles.Select(file => detailsByFileId[file.Id]);
        }

        /// <summary>
        /// Returns True iff the library is redundant (it has no RetentionTimes table)
        /// </summary>
        public static bool IsRedundantLibrary(string filepath)
        {
            using var conn = SqliteOperations.OpenConnection(filepath);
            using var cmd = new SQLiteCommand(@"SELECT name FROM sqlite_master WHERE name = 'RetentionTimes'", conn);
            return cmd.ExecuteScalar() == null;
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

        private class RefSpectraRow
        {
            public int id { get; set; }
            public string peptideSeq { get; set; }
            public double precursorMZ { get; set; }
            public int precursorCharge { get; set; } 
            public string moleculeName { get; set; }
            public string chemicalFormula { get; set; }
            public string precursorAdduct { get; set; }
            public string inchiKey { get; set; }
            public string otherKeys { get; set; }
            public double ionMobilityValue { get; set; }
            public IonMobilityType ionMobilityType { get; set; }
            public string peptideModSeq { get; set; }
            public int copies { get; set; }
            public int numPeaks { get; set; }
            public double? score { get; set; }
            public int? scoreType { get; set; }
            public string SpecIDinFile { get; set; }
            public int? fileId { get; set; } 
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
            scoreType,
            SpecIDinFile,
            retentionTime,
            fileId
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
            idFileName,
            cutoffScore,
            workflowType
        }

        private enum ScoreTypes
        {
            scoreType,
            probabilityType
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

        private bool ReadFromDatabase(ILoadMonitor loader, IProgressStatus status)
        {
            var sm = loader.StreamManager;
            EnsureConnections(sm);
            int rows;
            string lsid;
            int dataRev, schemaVer;
            var scoreTypesById = new Dictionary<int, string>();
            var proteinsBySpectraID = ProteinsBySpectraID();
            var librarySourceFiles = new List<BiblioLiteSourceInfo>();
            bool hasRetentionTimesTable = SqliteOperations.TableExists(_sqliteConnection.Connection, @"RetentionTimes");
            int segmentCount = hasRetentionTimesTable ? 2 : 1;
            var blibFilePath = Path.GetFileName(FilePath);
            status = status.ChangeSegments(0, segmentCount).ChangeMessage(string.Format(LibResources.BiblioSpecLiteLibrary_ReadFromDatabase_Reading_entries_from__0__library, blibFilePath));
            using (SQLiteCommand select = new SQLiteCommand(_sqliteConnection.Connection))
            {
                // First get header information
                select.CommandText = @"SELECT * FROM [LibInfo]";
                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new IOException(string.Format(
                            LibResources.BiblioSpecLiteLibrary_CreateCache_Failed_reading_library_header_for__0__,
                            FilePath));

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
                            throw new InvalidDataException(string.Format(
                                LibResources
                                    .BiblioSpecLiteLibrary_CreateCache_Unable_to_get_a_valid_count_of_spectra_in_the_library__0__,
                                FilePath));
                        rows = reader.GetInt32(0);
                    }
                }


                if (schemaVer >= 1)
                {
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
                            }
                        }
                    }
                }

                if (schemaVer > 0)
                {
                    select.CommandText = @"SELECT * FROM [SpectrumSourceFiles]";
                    using (SQLiteDataReader reader = select.ExecuteReader())
                    {
                        int iId = reader.GetOrdinal(SpectrumSourceFiles.id);
                        int iFilename = reader.GetOrdinal(SpectrumSourceFiles.fileName);
                        int iIdFilename =
                            reader.GetOrdinal(SpectrumSourceFiles
                                .idFileName); // Save the search result file, too (may be distinct from the spectra source file)
                        int iIdCutoffScore = reader.GetOrdinal(SpectrumSourceFiles.cutoffScore);
                        int iWorkflowType = reader.GetOrdinal(SpectrumSourceFiles.workflowType);
                        while (reader.Read())
                        {
                            string filename = reader.GetString(iFilename);
                            string idFilename = iIdFilename < 0 || reader.IsDBNull(iIdFilename)
                                ? null
                                : reader.GetString(iIdFilename);
                            int id = reader.GetInt32(iId);
                            double? cutoffScore = iIdCutoffScore < 0 || reader.IsDBNull(iIdCutoffScore) ? (double?) null : reader.GetDouble(iIdCutoffScore);
                            WorkflowType workflowType = iWorkflowType < 0 || reader.IsDBNull(iWorkflowType)
                                ? WorkflowType.DDA
                                : (WorkflowType) reader.GetInt32(iWorkflowType);
                            librarySourceFiles.Add(new BiblioLiteSourceInfo(id, filename, idFilename ?? string.Empty, cutoffScore, workflowType));
                        }
                    }

                }


            }

            var setLibKeys = new HashSet<LibKey>(rows);
            var libraryEntries = new List<BiblioLiteSpectrumInfo>(rows);

            int threadCount = ParallelEx.GetThreadCount(4);
            int rowsRead = 0;
            object lockObject = new object();
            ParallelEx.For(0, threadCount, threadIndex =>
            {
                using var connection = SqliteOperations.OpenConnection(FilePath);
                string sql = @"SELECT * FROM RefSpectra WHERE id % " + threadCount + @" = " + threadIndex;
                foreach (var row in connection.Query<RefSpectraRow>(sql, buffered:false))
                {
                    lock (lockObject)
                    {
                        rowsRead++;
                        if (loader.IsCanceled)
                        {
                            return;
                        }

                        int percentComplete = Math.Min(99, rowsRead * 100 / rows);
                        if (percentComplete != status.PercentComplete)
                        {
                            loader.UpdateProgress(status = status.ChangePercentComplete(percentComplete));
                        }
                    }

                    proteinsBySpectraID.TryGetValue(row.id, out var protein);
                    int? fileId = row.fileId;

                    string sequence = row.peptideModSeq;
                    int charge = row.precursorCharge;
                    string adduct = row.precursorAdduct;
                    int copies = row.copies;
                    int numPeaks = row.numPeaks;
                    double? score = row.score;
                    int? scoreType = row.scoreType;
                    var chemicalFormula = row.chemicalFormula;
                    bool isProteomic =
                        (string.IsNullOrEmpty(adduct) || Adduct.FromStringAssumeProtonated(adduct).IsProtonated) &&
                        !string.IsNullOrEmpty(sequence); // We may write an adduct like [M+H] for peptides
                    SmallMoleculeLibraryAttributes smallMoleculeLibraryAttributes;
                    if (isProteomic)
                    {
                        smallMoleculeLibraryAttributes = SmallMoleculeLibraryAttributes.EMPTY;
                    }
                    else
                    {
                        var moleculeName = row.moleculeName;
                        var inChiKey = row.inchiKey;
                        var otherKeys = row.otherKeys;
                        if (string.IsNullOrEmpty(chemicalFormula))
                        {
                            var precursorMz = row.precursorMZ;
                            Adduct precursorAdduct;
                            if (string.IsNullOrEmpty(adduct))
                            {
                                precursorAdduct = Adduct.FromChargeNoMass(charge);
                            }
                            else
                            {
                                precursorAdduct = Adduct.FromString(adduct, Adduct.ADDUCT_TYPE.non_proteomic,
                                    charge);
                            }

                            TypedMass monoMass = precursorAdduct.MassFromMz(precursorMz, MassType.Monoisotopic);
                            TypedMass avgMass = precursorAdduct.MassFromMz(precursorMz, MassType.Average);
                            smallMoleculeLibraryAttributes = SmallMoleculeLibraryAttributes.Create(moleculeName,
                                ParsedMolecule.Create(monoMass, avgMass), inChiKey, otherKeys);
                        }
                        else
                        {
                            smallMoleculeLibraryAttributes =
                                SmallMoleculeLibraryAttributes.Create(moleculeName, chemicalFormula, inChiKey,
                                    otherKeys);
                        }

                        // Construct a custom molecule so we can be sure we're using the same keys
                        var mol = CustomMolecule.FromSmallMoleculeLibraryAttributes(smallMoleculeLibraryAttributes);
                        sequence = mol.PrimaryEquivalenceKey;
                    }

                    // Avoid creating a cache which will just report it is corrupted.
                    // Older versions of BlibBuild used to create matches with charge 0.
                    // Newer versions that handle small molecules may reasonably use negative charges.
                    if (charge == 0 || Math.Abs(charge) > TransitionGroup.MAX_PRECURSOR_CHARGE)
                        continue;
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

                    lock (lockObject)
                    {
                        // These libraries should not have duplicates, but just in case.
                        // CONSIDER: Emit error about redundancy?
                        if (setLibKeys.Add(key))
                        {
                            string scoreName = null;
                            if (scoreType.HasValue)
                            {
                                scoreTypesById.TryGetValue(scoreType.Value, out scoreName);
                            }

                            libraryEntries.Add(new BiblioLiteSpectrumInfo(key, copies, numPeaks, row.id, fileId, protein,
                                scoreName == null ? null : score, scoreName));
                        }
                    }
                }
            }, maxThreads:threadCount);
            if (loader.IsCanceled)
            {
                loader.UpdateProgress(status.Cancel());
                return false;
            }


            var valueCache = new ValueCache();
            if (hasRetentionTimesTable) // Only a filtered library will have this table
            {
                status = status.ChangeSegments(1, segmentCount).ChangeMessage(string.Format(LibResources.BiblioSpecLiteLibrary_ReadFromDatabase_Reading_retention_times_from__0_, blibFilePath));
                var retentionTimeReader = new RetentionTimeReader(FilePath, schemaVer);
                retentionTimeReader.ReadAllRows(loader, ref status, rows);
                if (loader.IsCanceled)
                {
                    loader.UpdateProgress(status.Cancel());
                    return false;
                }
                var retentionTimesBySpectraId = retentionTimeReader.GetRetentionTimes();
                var driftTimesBySpectraId = retentionTimeReader.GetIonMobilities();
                var peakBoundsBySpectraId = retentionTimeReader.GetExplicitPeakBounds();
                for (int i = 0; i < libraryEntries.Count; i++)
                {
                    var libraryEntry = libraryEntries[i];
                    if (retentionTimesBySpectraId.TryGetValue(libraryEntry.Id, out var retentionTimes))
                    {
                        libraryEntry = libraryEntry.ChangeRetentionTimes(retentionTimes);
                    }

                    if (driftTimesBySpectraId.TryGetValue(libraryEntry.Id, out var driftTimes))
                    {
                        libraryEntry = libraryEntry.ChangeIonMobilities(driftTimes);
                    }

                    if (peakBoundsBySpectraId.TryGetValue(libraryEntry.Id, out var peakBounds) && peakBounds.Count > 0)
                    {
                        _anyExplicitPeakBounds = true;
                        peakBounds = peakBounds.ValueFromCache(valueCache);
                        libraryEntry = libraryEntry.ChangePeakBoundaries(peakBounds);
                    }
                    libraryEntries[i] = libraryEntry;
                }
            }

            _librarySourceFiles = librarySourceFiles.ToArray();
            _libraryFiles = new LibraryFiles(_librarySourceFiles.Select(file => file.FilePath));
            
            // Remove and report nonsense entries (e.g. adduct removes more H2O than present in molecule)
            libraryEntries = FilterInvalidLibraryEntries(ref status, libraryEntries.OrderBy(spec => spec.Id), blibFilePath);
            SetLibraryEntries(libraryEntries);
            
            EnsureConnections(sm);
            loader.UpdateProgress(status.ChangeSegments(segmentCount - 1, segmentCount).Complete());
            return true;
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
            try
            {
                if (ReadFromDatabase(loader, status))
                {
                    return true;
                }
            }
            catch (Exception x)
            {
                // SQLiteExceptions are not considered programming defects and should be shown to the user
                // as an ordinary error message
                if (x is SQLiteException || x is TargetInvocationException && x.InnerException is SQLiteException || !ExceptionUtil.IsProgrammingDefect(x))
                {
                    var message = string.Format(Resources.BiblioSpecLiteLibrary_Load_Failed_loading_library__0__, FilePath);
                    // This will show the user the error message after which the operation can be treated as canceled.
                    loader.UpdateProgress(status.ChangeErrorException(new Exception(message, x)));
                }
                else
                {
                    // Other sorts of exceptions should be posted to the Exception Web
                    throw new Exception(FormatErrorMessage(x), x);
                }
            }
            // Close any streams that got opened
            foreach (var pooledStream in ReadStreams)
                pooledStream.CloseStream();

            return false;
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

        public void WriteDebugMgf(string filepath, int? precision = null)
        {
            using var mgf = new StreamWriter(filepath);
            var emc = new SequenceMassCalc(MassType.Monoisotopic);

            string formatMgfDouble(double value)
            {
                if (precision.HasValue)
                    return value.ToString(@"F" + precision.Value, CultureInfo.InvariantCulture);
                return value.ToString(CultureInfo.InvariantCulture);
            }

            foreach (PeptideLibraryKey key in Keys)
            {
                var spectra = GetSpectra(key, IsotopeLabelType.light, LibraryRedundancy.best);
                var spectrum = spectra.First();
                double mass = emc.GetPrecursorMass(key.UnmodifiedSequence) +
                              key.GetModifications().Sum(mod => MassModification.Parse(mod.Value).Mass);
                mgf.WriteLine(@"BEGIN IONS");
                mgf.WriteLine(@"TITLE=" + key.ModifiedSequence);
                mgf.WriteLine(@"RTINSECONDS=" + formatMgfDouble(spectrum.RetentionTime.GetValueOrDefault(0) * 60));
                mgf.WriteLine(@"CHARGE=" + key.Charge + @"+");
                mgf.WriteLine(@"PEPMASS=" + formatMgfDouble(key.Adduct.MzFromNeutralMass(new TypedMass(mass, MassType.Monoisotopic))));
                foreach (var peak in spectrum.SpectrumPeaksInfo.Peaks.OrderBy(peak => peak.Mz))
                    if (peak.Intensity > 0)
                        mgf.WriteLine(@"{0}	{1}", formatMgfDouble(peak.Mz), formatMgfDouble(peak.Intensity));
                mgf.WriteLine(@"END IONS");
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
                {
                    var redundantSpectrum = ReadRedundantSpectrum(spectrumLiteKey.RedundantId);
                    return redundantSpectrum == null ? SpectrumPeaksInfo.EMPTY : new SpectrumPeaksInfo(redundantSpectrum);
                }

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
            if (info.NumPeaks == 0)
            {
                return Array.Empty<SpectrumPeaksInfo.MI>();
            }
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
                throw new IOException(string.Format(LibResources.BiblioSpecLiteLibrary_ReadRedundantSpectrum_The_redundant_library__0__does_not_exist, FilePathRedundant));

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
            if (numPeaks == 0)
            {
                return Array.Empty<SpectrumPeaksInfo.MI>();
            }
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

        public override bool HasExplicitBounds
        {
            get
            {
                return _anyExplicitPeakBounds;
            }
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
                var dictionary = new Dictionary<Target, Tuple<TimeSource, double[]>>();
                foreach (var grouping in _libraryEntries.GroupBy(entry => entry.Key.Target))
                {
                    var times = grouping.SelectMany(entry => entry.RetentionTimesByFileId.GetTimes(source.Id))
                        .OrderBy(time => time).ToArray();
                    if (times.Length > 0)
                    {
                        dictionary.Add(grouping.Key, Tuple.Create(TimeSource.scan, times));
                    }
                }

                retentionTimes = new LibraryRetentionTimes(filePath.ToString(), dictionary);
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
                JobTitle = LibResources.BiblioSpecLiteLibrary_CalculateFileRetentionTimeAlignments_Aligning_library_retention_times
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



        private double? GetMinRetentionTime(IEnumerable<BiblioLiteSpectrumInfo> spectrumInfos, BiblioLiteSourceInfo sourceInfo)
        {
            var minTime = spectrumInfos.SelectMany(spectrum => spectrum.RetentionTimesByFileId.GetTimes(sourceInfo.Id))
                .Append(double.MaxValue).Min();
            if (minTime == double.MaxValue)
            {
                return null;
            }

            return minTime;
        }

        private Dictionary<int, double> GetMinRetentionTimes(IEnumerable<BiblioLiteSpectrumInfo> spectrumInfos, IList<int> fileIds)
        {
            var result = new Dictionary<int, double>();
            foreach (var spectrumInfo in spectrumInfos)
            {
                foreach (var entry in spectrumInfo.RetentionTimesByFileId.GetMinRetentionTimes(fileIds))
                {
                    if (result.TryGetValue(entry.Key, out var oldMin))
                    {
                        if (entry.Value < oldMin)
                        {
                            result[entry.Key] = entry.Value;
                        }
                    }
                    else
                    {
                        result.Add(entry.Key, entry.Value);
                    }
                }
            }

            return result;
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

        private int FindSource(MsDataFileUri filePath)
        {
            return _libraryFiles.FindIndexOf(filePath);
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
                        var retentionTime = UtilDB.GetNullableDouble(reader, iRetentionTime);
                        bool isBest = !hasRetentionTimesTable || reader.GetInt16(iBestSpectrum) != 0;

                        IonMobilityAndCCS ionMobilityInfo = IonMobilityAndCCS.EMPTY;
                        if (hasGeneralIonMobility)
                        {
                            var ionMobilityType = eIonMobilityUnits.none;
                            if (iIonMobilityType != -1)
                                ionMobilityType = (eIonMobilityUnits)NullSafeToInteger(reader.GetValue(iIonMobilityType));
                            if (!ionMobilityType.Equals(eIonMobilityUnits.none))
                            {
                                var ionMobility = UtilDB.GetNullableDouble(reader, iIonMobility);
                                var collisionalCrossSectionSqA = UtilDB.GetNullableDouble(reader, iCCS);
                                var ionMobilityHighEnergyOffset = UtilDB.GetNullableDouble(reader, iIonMobilityHighEnergyOffset);
                                if (!(ionMobility == 0 && collisionalCrossSectionSqA == 0 && ionMobilityHighEnergyOffset == 0))
                                    ionMobilityInfo = IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(ionMobility, ionMobilityType), collisionalCrossSectionSqA, ionMobilityHighEnergyOffset);
                            }
                            else
                            {
                                var collisionalCrossSectionSqA = UtilDB.GetNullableDouble(reader, iCCS);
                                if (collisionalCrossSectionSqA != 0) 
                                {
                                    ionMobilityInfo = IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(0, ionMobilityType), collisionalCrossSectionSqA, 0);
                                }
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
                        throw new InvalidDataException(string.Format(LibResources.BiblioSpecLiteLibrary_RetentionTimesPsmCount_Unable_to_get_a_valid_count_of_all_spectra_in_the_library__0__, FilePath));
                    int rows = reader.GetInt32(0);
                    return rows;
                }
            }
        }

        private bool HasRedundantModificationsTable()
        {
            return SqliteOperations.TableExists(_sqliteConnectionRedundant.Connection, @"Modifications");
        }

        private bool HasScoreTypesTable(SQLiteConnection connection)
        {
            if (!SqliteOperations.TableExists(connection, @"ScoreTypes"))
            {
                return false;
            }

            using (SQLiteCommand select = new SQLiteCommand(_sqliteConnection.Connection))
            {
                select.CommandText = @"SELECT count(*) FROM [ScoreTypes]";

                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int rows = reader.GetInt32(0);
                        return rows > 0;
                    }

                    return false;
                }
            }
        }

        private bool HasSourceFilesTable(SQLiteConnection connection)
        {
            return SqliteOperations.TableExists(connection, @"SpectrumSourceFiles");
        }

        private bool HasFileIdColumn(SQLiteConnection connection)
        {
            return SqliteOperations.ColumnExists(connection, @"RefSpectra", @"fileID");
        }

        public class BiblioSpecSheetInfo
        {
            [CanBeNull] public string SpecIdInFile { get; set; }
            [CanBeNull] public string IDFileName { get; set; }

            [CanBeNull] public string FileName { get; set; }
            public WorkflowType WorkflowType { get; set; }

            public int Count { get; set; }

            [CanBeNull] public double? Score { get; set; }

            [CanBeNull] public string ScoreType { get; set; }
        }

        public BiblioSpecSheetInfo GetRedundantSheetInfo(int redundantId)
        {
            return GetSheetInfo(redundantId, false);
        }

        public BiblioSpecSheetInfo GetBestSheetInfo(LibKey key)
        {
            int i = FindEntry(key);
            if (i == -1)
                return null;
            var info = _libraryEntries[i];
            return GetSheetInfo(info.Id, true);
        }
        
        /// <summary>
        /// Gets data on a spectrum to populate the Property Sheet in ViewLibraryDlg
        /// </summary>
        /// <param name="id">The RefSpectraID of the spectrum</param>
        /// <param name="isBest">Whether to search the redundant or non-redundant library</param>
        /// <returns></returns>
        public BiblioSpecSheetInfo GetSheetInfo(long id, bool isBest)
        {
            var connection = isBest ? _sqliteConnection.Connection : _sqliteConnectionRedundant.Connection;
            var hasScores = HasScoreTypesTable(connection);
            var hasFiles = HasSourceFilesTable(connection) && HasFileIdColumn(connection);
            using (SQLiteCommand select = new SQLiteCommand(connection))
            {
                if (hasScores)
                {
                    // Resolves issue with RefSpectra and ScoreTypes table both having a column named scoreType
                    select.CommandText = @"SELECT u.SpecIDinFile, u.retentionTime, u.score, u.copies, q.*";
                    select.CommandText += hasFiles ? @", s.* " : @" ";
                }
                else
                {
                    select.CommandText = @"SELECT * ";
                }

                select.CommandText += @"FROM [RefSpectra] as u ";

                if (hasFiles)
                    select.CommandText += @"INNER JOIN [SpectrumSourceFiles] as s ON u.[FileID] = s.[id] ";

                if (hasScores)
                    select.CommandText += @"INNER JOIN [ScoreTypes] as q ON u.[scoreType] = q.[id] ";

                select.CommandText += @"WHERE u.[id] = ?";

                select.Parameters.Add(new SQLiteParameter(DbType.UInt64, id));
                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                    var iSpecIdInFile = reader.GetOrdinal(RefSpectra.SpecIDinFile);
                    var iIdFileName = reader.GetOrdinal(SpectrumSourceFiles.idFileName);
                    var iFileName = reader.GetOrdinal(SpectrumSourceFiles.fileName);
                    var iWorkflowType = reader.GetOrdinal(SpectrumSourceFiles.workflowType);
                    var iCopies = reader.GetOrdinal(RefSpectra.copies);
                    var iScore = reader.GetOrdinal(RefSpectra.score);
                    var iScoreType = reader.GetOrdinal(ScoreTypes.scoreType);
                    var iProbabilityType = reader.GetOrdinal(ScoreTypes.probabilityType);
                    var sheetInfo = new BiblioSpecSheetInfo();
                    if (reader.Read())
                    {
                        sheetInfo.SpecIdInFile = reader.IsDBNull(iSpecIdInFile) ? null : reader.GetString(iSpecIdInFile);
                        sheetInfo.Count = reader.GetInt32(iCopies);
                        if (hasFiles)
                        {
                            sheetInfo.IDFileName = reader.IsDBNull(iIdFileName) ? null : reader.GetString(iIdFileName);
                            sheetInfo.FileName = reader.IsDBNull(iFileName) ? null : reader.GetString(iFileName);
                        }

                        sheetInfo.WorkflowType = iWorkflowType < 0 || reader.IsDBNull(iWorkflowType)
                            ? WorkflowType.DDA
                            : (WorkflowType) reader.GetInt32(iWorkflowType);

                        if (hasScores)
                        {
                            sheetInfo.Score = reader.IsDBNull(iScore) ? (double?)null : reader.GetDouble(iScore);
                            var scoreType = reader.IsDBNull(iScoreType) ? null : reader.GetString(iScoreType);
                            var probabilityType = reader.IsDBNull(iProbabilityType) ? null : reader.GetString(iProbabilityType);
                            if (ScoreType.INVARIANT_NAMES.Contains(scoreType))
                            {
                                sheetInfo.ScoreType = new ScoreType(scoreType, probabilityType).ToString();
                            }
                            else
                            {
                                sheetInfo.ScoreType = scoreType;
                            }
                        }
                        return sheetInfo;
                    }
                    // Should never reach here, as there should always be an sql entry matching the query
                    return null;
                }
            }
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
                IProgressStatus status = new ProgressStatus(LibResources.BiblioSpecLiteLibrary_DeleteDataFiles_Removing_library_runs_from_document_library_);
                if (!blibFilter.Filter(FilePathRedundant, saver.SafeName, monitor, ref status))
                    throw new IOException(string.Format(LibResources.BiblioSpecLiteLibrary_DeleteDataFiles_Failed_attempting_to_filter_redundant_library__0__to__1_, FilePathRedundant, FilePath));

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
            public BiblioLiteSourceInfo(int id, string filePath, string idFilePath, double? cutoffScore, WorkflowType workflowType) : this()
            {
                Id = id;
                FilePath = filePath;
                IdFilePath = idFilePath;
                CutoffScore = cutoffScore;
                WorkflowType = workflowType;
            }

            public int Id { get; private set; }
            public string FilePath { get; private set; } // File from which the spectra were taken (may be same as idFilePath if spectra are taken from search file)
            public string IdFilePath { get; private set; } // File from which the IDs were taken (e.g. search results file from Mascot etc)
            public double? CutoffScore { get; }
            public WorkflowType WorkflowType { get; private set; } // DDA or DIA

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

        class RetentionTimeRow
        {
            public int? RefSpectraID { get; set; }
            public int? SpectrumSourceID { get; set; }
            public double? retentionTime { get; set; }
            public double? driftTimeMsec { get; set; }
            public double? collisionalCrossSectionSqA { get; set; }
            public double? driftTimeHighEnergyOffsetMsec { get; set; }
            public byte? ionMobilityType { get; set; }
            public double? ionMobilityValue { get; set; }
            public double? ionMobilityHighEnergyDriftTimeOffsetMsec { get; set; }
            public double? startTime { get; set; }
            public double? endTime { get; set; }
            public double? ionMobility { get; set; }
            public double? ionMobilityHighEnergyOffset { get; set; }
            public double? score { get; set; }
        }
        class RetentionTimeReader
        {
            private readonly int _schemaVer;
            private readonly string _dbPath;

            private Dictionary<int, IndexedRetentionTimes> _retentionTimes =
                new Dictionary<int, IndexedRetentionTimes>();

            private Dictionary<int, IndexedIonMobilities> _ionMobilities =
                new Dictionary<int, IndexedIonMobilities>();

            private Dictionary<int, ExplicitPeakBoundsDict<int>> _explicitPeakBounds =
                new Dictionary<int, ExplicitPeakBoundsDict<int>>();

            private IProgressMonitor _progressMonitor;
            private IProgressStatus _progressStatus;
            private int _progressValue;
            private int _completedSpectraCount;
            private int _refSpectraCount;

            public RetentionTimeReader(string dbPath, int schemaVer)
            {
                _dbPath = dbPath;
                _schemaVer = schemaVer;
            }

            // ReSharper disable InconsistentlySynchronizedField
            public Dictionary<int, IndexedRetentionTimes> GetRetentionTimes()
            {
                return _retentionTimes;
            }

            public Dictionary<int, IndexedIonMobilities> GetIonMobilities()
            {
                return _ionMobilities;
            }

            public Dictionary<int, ExplicitPeakBoundsDict<int>> GetExplicitPeakBounds()
            {
                return _explicitPeakBounds;
            }
            // ReSharper restore InconsistentlySynchronizedField

            public void ReadAllRows(IProgressMonitor progressMonitor, ref IProgressStatus status, int refSpectraCount)
            {
                _progressMonitor = progressMonitor;
                _progressStatus = status;
                _refSpectraCount = refSpectraCount;
                int threadCount = ParallelEx.GetThreadCount();
                ParallelEx.For(0, threadCount, threadIndex =>
                {
                    using var conn = SqliteOperations.OpenConnection(_dbPath);
                    ReadSubset(conn, threadCount, threadIndex);
                }, maxThreads: threadCount);
                status = _progressStatus;
            }

            private void ReadSubset(IDbConnection connection, int threadCount, int threadIndex)
            {
                var sql = @"SELECT * From RetentionTimes WHERE RefSpectraId % "
                          + threadCount + @" = " + threadIndex;
                List<RetentionTimeRow> rows = new List<RetentionTimeRow>();
                int? lastRefSpectraId = null;
                foreach (var row in connection.Query<RetentionTimeRow>(sql, buffered: false))
                {
                    int? refSpectraId = row.RefSpectraID;
                    if (refSpectraId == null)
                    {
                        continue;
                    }
                    if (refSpectraId != lastRefSpectraId)
                    {
                        if (rows.Count != 0)
                        {
                            if (!ConsumeRows(rows))
                            {
                                return;
                            }
                            rows = new List<RetentionTimeRow>();
                        }

                        lastRefSpectraId = refSpectraId;
                    }
                    rows.Add(row);
                }

                if (rows.Count != 0)
                {
                    ConsumeRows(rows);
                }
            }

            private bool ConsumeRows(List<RetentionTimeRow> rows)
            {
                var refSpectraId = rows[0].RefSpectraID;
                var retentionTimes = new List<KeyValuePair<int, double>>();
                var ionMobilities = new List<KeyValuePair<int, IonMobilityAndCCS>>();
                var explicitPeakBounds = new List<KeyValuePair<int, ExplicitPeakBounds>>();
                foreach (var row in rows)
                {
                    int? fileId = row.SpectrumSourceID;
                    if (!fileId.HasValue)
                    {
                        continue;
                    }

                    if (row.retentionTime.HasValue)
                    {
                        retentionTimes.Add(new KeyValuePair<int, double>(fileId.Value, row.retentionTime.Value));
                    }

                    var ionMobility = ReadIonMobilityInfo(row);
                    if (ionMobility != null)
                    {
                        ionMobilities.Add(new KeyValuePair<int, IonMobilityAndCCS>(fileId.Value, ionMobility));
                    }

                    var peakBounds = ReadPeakBounds(row);
                    if (peakBounds != null)
                    {
                        explicitPeakBounds.Add(new KeyValuePair<int, ExplicitPeakBounds>(fileId.Value, peakBounds));
                    }
                }

                if (retentionTimes.Count > 0)
                {
                    var indexedRetentionTimes = new IndexedRetentionTimes(retentionTimes);
                    lock (_retentionTimes)
                    {
                        if (_retentionTimes.TryGetValue(refSpectraId.Value, out var existing))
                        {
                            _retentionTimes[refSpectraId.Value] = existing.MergeWith(indexedRetentionTimes);
                        }
                        else
                        {
                            _retentionTimes.Add(refSpectraId.Value, indexedRetentionTimes);
                        }
                    }
                }

                if (ionMobilities.Count > 0)
                {
                    var indexedIonMobilities = new IndexedIonMobilities(ionMobilities);
                    lock (_ionMobilities)
                    {
                        if (_ionMobilities.TryGetValue(refSpectraId.Value, out var existing))
                        {
                            _ionMobilities[refSpectraId.Value] = existing.MergeWith(indexedIonMobilities);
                        }
                        else
                        {
                            _ionMobilities.Add(refSpectraId.Value, indexedIonMobilities);
                        }
                    }
                }

                if (explicitPeakBounds.Count > 0)
                {
                    var explicitPeakBoundsDict = new ExplicitPeakBoundsDict<int>(explicitPeakBounds.Distinct());
                    lock (_explicitPeakBounds)
                    {
                        if (_explicitPeakBounds.TryGetValue(refSpectraId.Value, out var existing))
                        {
                            _explicitPeakBounds[refSpectraId.Value] =
                                new ExplicitPeakBoundsDict<int>(existing.Concat(explicitPeakBounds));
                        }
                        else
                        {
                            _explicitPeakBounds.Add(refSpectraId.Value, explicitPeakBoundsDict);
                        }
                    }
                }

                return SpectrumCompleted();
            }

            private bool SpectrumCompleted()
            {
                lock (this)
                {
                    if (_progressMonitor.IsCanceled)
                    {
                        return false;
                    }
                    _completedSpectraCount++;
                    int newProgressValue = Math.Min(99, _completedSpectraCount * 100 / _refSpectraCount);
                    if (newProgressValue != _progressValue)
                    {
                        _progressStatus = _progressStatus.ChangePercentComplete(newProgressValue);
                        _progressValue = newProgressValue;
                        _progressMonitor.UpdateProgress(_progressStatus);
                    }

                    return true;
                }
            }

            public IonMobilityAndCCS ReadIonMobilityInfo(RetentionTimeRow row)
            {
                if (_schemaVer < 2)
                {
                    return null;
                }
                switch (_schemaVer)
                {
                    default:
                    {
                        double mobility = row.ionMobility ?? 0;
                        double collisionalCrossSection = row.collisionalCrossSectionSqA ?? 0;
                        double highEnergyOffset = row.ionMobilityHighEnergyOffset ?? 0;
                        var units = (eIonMobilityUnits)row.ionMobilityType.GetValueOrDefault();
                        if (mobility == 0 && collisionalCrossSection == 0 &&
                            highEnergyOffset == 0)
                        {
                            return null;
                        }
                        return IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(mobility, units),
                            collisionalCrossSection, highEnergyOffset);
                    }
                    case 5:
                    case 4:
                    {
                        double driftTimeMsec = row.driftTimeMsec ?? 0;
                        double collisionalCrossSection =
                            row.collisionalCrossSectionSqA ?? 0;
                        double highEnergyOffset = row.driftTimeHighEnergyOffsetMsec ?? 0;
                        if (driftTimeMsec == 0 && collisionalCrossSection == 0 &&
                            highEnergyOffset == 0)
                        {
                            return null;
                        }
                        return IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(driftTimeMsec, eIonMobilityUnits.drift_time_msec),
                            collisionalCrossSection, highEnergyOffset);
                    }
                    case 3:
                    case 2:
                    {
                        int ionMobilityType = row.ionMobilityType ?? 0;
                        double ionMobilityValue = row.ionMobilityValue ?? 0;
                        double highEnergyOffset = row.ionMobilityHighEnergyDriftTimeOffsetMsec ?? 0;
                        if (ionMobilityValue == 0 && highEnergyOffset == 0)
                        {
                            return null;
                        }
                        bool isCcs = ionMobilityType == (int) IonMobilityType.collisionalCrossSection;
                        return IonMobilityAndCCS.GetIonMobilityAndCCS(isCcs ? IonMobilityValue.EMPTY : IonMobilityValue.GetIonMobilityValue(ionMobilityValue, eIonMobilityUnits.drift_time_msec), isCcs ? ionMobilityValue : (double?)null, highEnergyOffset);
                    }
                }
            }

            public ExplicitPeakBounds ReadPeakBounds(RetentionTimeRow row)
            {
                if (row.startTime.HasValue && row.endTime.HasValue)
                {
                    return new ExplicitPeakBounds(row.startTime.Value, row.endTime.Value, row.score ?? ExplicitPeakBounds.UNKNOWN_SCORE);
                }
                return null;
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

        public override Dictionary<Target, double>[] GetAllRetentionTimes(IEnumerable<string> spectrumSourceFiles)
        {
            var files = GetSourceInfos(spectrumSourceFiles, out var fileIds);
            var result = files.Select(file => new Dictionary<Target, double>()).ToArray();
            foreach (var grouping in _libraryEntries.GroupBy(entry => entry.Key.Target))
            {
                var minTimes = GetMinRetentionTimes(grouping, fileIds);
                if (minTimes.Count == 0)
                {
                    continue;
                }
                for (int iFile = 0; iFile < files.Count; iFile++)
                {
                    var file = files[iFile];
                    if (file.HasValue && minTimes.TryGetValue(file.Value.Id, out var minTime))
                    {
                        result[iFile].Add(grouping.Key, minTime);
                    }
                }
            }

            return result;
        }

        public override IList<double>[] GetRetentionTimesWithSequences(IEnumerable<string> spectrumSourceFiles,
            ICollection<Target> targets)
        {
            var files = GetSourceInfos(spectrumSourceFiles, out var fileIds);
            var result = files.OfType<BiblioLiteSourceInfo>().ToDictionary(file => file.Id, file => new List<double>());
            foreach (var entry in targets.SelectMany(target =>
                         _libraryEntries.ItemsMatching(new LibKey(target, Adduct.EMPTY), false)))
            {
                foreach (var fileIdRetentionTimes in entry.RetentionTimesByFileId.GetRetentionTimes(fileIds))
                {
                    result[fileIdRetentionTimes.Key].AddRange(fileIdRetentionTimes.Value.Select(t=>(double) t));
                }
            }

            return files.Select(file => file == null ? (IList<double>)Array.Empty<double>() : result[file.Value.Id])
                .ToArray();
        }

        private IList<BiblioLiteSourceInfo?> GetSourceInfos(IEnumerable<string> spectrumSourceFiles,
            out IList<int> orderedFileIds)
        {
            var files = _librarySourceFiles.Cast<BiblioLiteSourceInfo?>().ToList();
            if (spectrumSourceFiles != null)
            {
                files = spectrumSourceFiles.Select(file => files.ElementAtOrDefault(LibraryFiles.IndexOfFilePath(file))).ToList();
                orderedFileIds = files.Select(file => file?.Id).OfType<int>().OrderBy(i => i).ToList();
            }
            else
            {
                orderedFileIds = null;
            }

            return files;
        }
        public BiblioSpecLiteLibrary ChangeLibrarySpec(BiblioSpecLiteSpec newSpec, ConnectionPool connectionPool)
        {
            return ChangeProp(ImClone((BiblioSpecLiteLibrary)ChangeName(newSpec.Name)), im =>
            {
                im.FilePath = newSpec.FilePath;
                _sqliteConnection = new PooledSqliteConnection(connectionPool, newSpec.FilePath);
            });
        }
    }

    public sealed class SpectrumLiteKey : IEquatable<SpectrumLiteKey>
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

        public bool Equals(SpectrumLiteKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return NonRedundantId == other.NonRedundantId && RedundantId == other.RedundantId && IsBest == other.IsBest;
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is SpectrumLiteKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = NonRedundantId;
                hashCode = (hashCode * 397) ^ RedundantId;
                hashCode = (hashCode * 397) ^ IsBest.GetHashCode();
                return hashCode;
            }
        }
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

        public IEnumerable<KeyValuePair<int, double>> GetMinRetentionTimes(IList<int> fileIds)
        {
            foreach (var entry in GetRetentionTimes(fileIds))
            {
                if (entry.Value.Length > 0)
                {
                    yield return new KeyValuePair<int, double>(entry.Key, entry.Value.Min());
                }
            }
        }

        public IEnumerable<KeyValuePair<int, float[]>> GetRetentionTimes(IList<int> fileIds)
        {
            if (_timesById == null)
            {
                yield break;
            }

            using var enFileIds = fileIds?.GetEnumerator();
            if (false == enFileIds?.MoveNext())
            {
                yield break;
            }
            foreach (var entry in _timesById)
            {
                if (enFileIds != null)
                {
                    while (entry.Key > enFileIds.Current)
                    {
                        if (!enFileIds.MoveNext())
                        {
                            yield break;
                        }
                    }
                }

                if (enFileIds == null || entry.Key == enFileIds.Current)
                {
                    yield return new KeyValuePair<int, float[]>(entry.Key, entry.Value);
                }
            }
        }

        public IEnumerable<KeyValuePair<int, IList<float>>> GetTimesById()
        {
            if (_timesById == null)
            {
                return Array.Empty<KeyValuePair<int, IList<float>>>();
            }
            return _timesById.Select(entry =>
                new KeyValuePair<int, IList<float>>(entry.Key, new ReadOnlyCollection<float>(entry.Value)));
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

        public IndexedRetentionTimes MergeWith(params IndexedRetentionTimes[] other)
        {
            return new IndexedRetentionTimes(other.Prepend(this).SelectMany(item =>
                item._timesById.SelectMany(
                    kvp => kvp.Value.Select(time => new KeyValuePair<int, double>(kvp.Key, time)))));
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

        public IndexedIonMobilities MergeWith(params IndexedIonMobilities[] all)
        {
            return new IndexedIonMobilities(all.Prepend(this).SelectMany(item => item._ionMobilityById.SelectMany(kvp =>
                kvp.Value.Select(ionMobility => new KeyValuePair<int, IonMobilityAndCCS>(kvp.Key, ionMobility)))));
        }
    }

    public class BiblioLiteSpectrumInfo : Immutable, ICachedSpectrumInfo
    {
        public BiblioLiteSpectrumInfo(LibKey key, int copies, int numPeaks, int id, int? spectrumSourceId, string protein, 
            double? score = null, string scoreType = null)
        {
            Key = key;
            Copies = copies;
            NumPeaks = numPeaks;
            Id = id;
            SpectrumSourceId = spectrumSourceId;
            Protein = protein;
            PeakBoundariesByFileId = ExplicitPeakBoundsDict<int>.EMPTY;
            Score = score;
            ScoreType = scoreType;
        }

        public LibKey Key { get; }
        public int Copies { get; }
        public int NumPeaks { get; }
        public int Id { get; }
        public int? SpectrumSourceId { get; }
        public string Protein { get; } // From the RefSpectraProteins table, either a protein accession or an arbitrary molecule list name
        public IndexedRetentionTimes RetentionTimesByFileId { get; private set; }
        public IndexedIonMobilities IonMobilitiesByFileId { get; private set; }
        public ExplicitPeakBoundsDict<int> PeakBoundariesByFileId { get; private set; }
        public double? Score { get; }
        public string ScoreType { get; }

        public BiblioLiteSpectrumInfo ChangeRetentionTimes(IndexedRetentionTimes retentionTimes)
        {
            return ChangeProp(ImClone(this), im => im.RetentionTimesByFileId = retentionTimes);
        }

        public BiblioLiteSpectrumInfo ChangeIonMobilities(IndexedIonMobilities ionMobilities)
        {
            return ChangeProp(ImClone(this), im => im.IonMobilitiesByFileId = ionMobilities);
        }

        public BiblioLiteSpectrumInfo ChangePeakBoundaries(ExplicitPeakBoundsDict<int> peakBoundaries)
        {
            return ChangeProp(ImClone(this), im => im.PeakBoundariesByFileId = peakBoundaries);
        }
    }
}
