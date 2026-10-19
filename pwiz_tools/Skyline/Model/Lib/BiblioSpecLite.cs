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
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using JetBrains.Annotations;
using pwiz.BiblioSpec;
using pwiz.BiblioSpec.IO;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
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
        private bool _anyRedundantSpectra;
        private ImmutableList<GlobalQValueLevel> _globalQValueLevels = ImmutableList<GlobalQValueLevel>.EMPTY;

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

        public IEnumerable<SpectrumSourceFileDetails> GetDataFileDetails()
        {
            var detailsByFileIndex = new SpectrumSourceFileDetails[_librarySourceFiles.Length];
            var scoreTypesByFileIndex = new HashSet<string>[_librarySourceFiles.Length];
            Dictionary<string, ScoreType> scoreTypesByName = new Dictionary<string, ScoreType>();
            try
            {
                foreach (var scoreTypeRow in _sqliteConnection.ExecuteWithReader(reader => reader.ReadScoreTypeTable()))
                {
                    var scoreType = new ScoreType(scoreTypeRow.Name, scoreTypeRow.ProbabilityType);
                    scoreTypesByName[scoreType.NameInvariant] = scoreType;
                }
            }
            catch
            {
                // Ignore
            }
            for (int fileIndex = 0; fileIndex < _librarySourceFiles.Length; fileIndex++)
            {
                var spectrumSourceFile = _librarySourceFiles[fileIndex];
                detailsByFileIndex[fileIndex] = new SpectrumSourceFileDetails(spectrumSourceFile.FilePath, spectrumSourceFile.IdFilePath, spectrumSourceFile.WorkflowType);
                scoreTypesByFileIndex[fileIndex] = new HashSet<string>();
            }

            if (_libraryEntries != null)
            {
                foreach (var entry in _libraryEntries)
                {
                    int? bestFileIndex = entry.SpectrumSourceIndex;
                    if (bestFileIndex.HasValue)
                    {
                        detailsByFileIndex[bestFileIndex.Value].BestSpectrum++;
                        scoreTypesByFileIndex[bestFileIndex.Value].Add(entry.ScoreType);
                    }

                    var retentionTimes = entry.RetentionTimesByFileIndex;
                    for (int fileIndex = 0; fileIndex < retentionTimes.Count; fileIndex++)
                    {
                        detailsByFileIndex[fileIndex].MatchedSpectrum += retentionTimes.GetCount(fileIndex);
                    }
                }
            }

            for (int fileIndex = 0; fileIndex < _librarySourceFiles.Length; fileIndex++)
            {
                foreach (var scoreTypeName in scoreTypesByFileIndex[fileIndex])
                {
                    if (scoreTypeName != null && scoreTypesByName.TryGetValue(scoreTypeName, out var scoreType))
                    {
                        detailsByFileIndex[fileIndex].ScoreThresholds.Add(scoreType, _librarySourceFiles[fileIndex].CutoffScore);
                    }
                }
            }

            return detailsByFileIndex;
        }

        /// <summary>
        /// Returns True iff the library is redundant (it has no RetentionTimes table)
        /// </summary>
        public static bool IsRedundantLibrary(string filepath)
        {
            using var reader = new BlibReader(filepath);
            return !reader.HasTable(BlibReader.TABLE_RETENTION_TIMES);
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

        private bool ReadFromDatabase(ILoadMonitor loader, IProgressStatus status)
        {
            var sm = loader.StreamManager;
            EnsureConnections(sm);
            int rows = 0;
            bool hasRetentionTimesTable = false;
            IReadOnlyDictionary<int, string> scoreTypesById = null;
            Dictionary<long, ImmutableList<LibraryProteinGroup>> proteinGroupsBySpectraId = null;
            var librarySourceFiles = new List<BiblioLiteSourceInfo>();
            // The header and the small tables, on the pooled connection
            _sqliteConnection.ExecuteWithReader(reader =>
            {
                BlibLibraryInfo info;
                try
                {
                    info = reader.Info;
                }
                catch (InvalidDataException x)
                {
                    throw new IOException(string.Format(
                        LibResources.BiblioSpecLiteLibrary_CreateCache_Failed_reading_library_header_for__0__, FilePath), x);
                }

                // Set these now, in case we encounter an error further in
                Lsid = info.Lsid;
                SetRevision(info.Revision, info.SchemaVersion);

                // A library of schema 1 or later lists its source files, and one without the
                // table is damaged. The reader reads none for a schema-0 library, which never
                // had the table, so the requirement is checked here, as this loader always has.
                if (info.SchemaVersion > 0 && !reader.HasTable(BlibReader.TABLE_SPECTRUM_SOURCE_FILES))
                {
                    throw new InvalidDataException(string.Format(
                        LibResources.BiblioSpecLiteLibrary_ReadFromDatabase_The_library__0__has_no__1__table,
                        FilePath, BlibReader.TABLE_SPECTRUM_SOURCE_FILES));
                }

                // Corrupted library without a valid row count, but try to compensate
                // by using count(*)
                rows = info.SpectrumCount > 0 ? info.SpectrumCount : reader.CountSpectra();
                hasRetentionTimesTable = reader.HasTable(BlibReader.TABLE_RETENTION_TIMES);
                proteinGroupsBySpectraId = ProteinGroupsBySpectraId(reader);
                _globalQValueLevels = ImmutableList.ValueOf(ReadGlobalQValueLevels(reader));
                scoreTypesById = reader.ReadScoreTypes();
                foreach (var file in reader.ReadSourceFiles())
                {
                    // Save the search result file, too (may be distinct from the spectra source file)
                    librarySourceFiles.Add(new BiblioLiteSourceInfo((int)file.Id, file.FileName ?? string.Empty,
                        file.IdFileName ?? string.Empty, file.CutoffScore, (WorkflowType)file.WorkflowType));
                }
                return true;
            });

            int segmentCount = hasRetentionTimesTable ? 2 : 1;
            var blibFilePath = Path.GetFileName(FilePath);
            status = status.ChangeSegments(0, segmentCount).ChangeMessage(string.Format(LibResources.BiblioSpecLiteLibrary_ReadFromDatabase_Reading_entries_from__0__library, blibFilePath));

            var setLibKeys = new HashSet<LibKey>(rows);
            var libraryEntries = new List<BiblioLiteSpectrumInfo>(rows);
            var fileIdToIndex = Enumerable.Range(0, librarySourceFiles.Count)
                .ToDictionary(i => (long)librarySourceFiles[i].Id);

            int threadCount = ParallelEx.GetThreadCount(4);
            int rowsRead = 0;
            object lockObject = new object();
            ParallelEx.For(0, threadCount, threadIndex =>
            {
                // Each thread scans its own range of ids on a reader of its own
                using var reader = new BlibReader(FilePath);
                foreach (var row in reader.ReadSpectra(false, threadCount, threadIndex))
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

                    proteinGroupsBySpectraId.TryGetValue(row.Id, out var proteinGroups);
                    int? fileIndex = null;
                    if (fileIdToIndex.TryGetValue(row.FileId, out int iFile))
                    {
                        fileIndex = iFile;
                    }

                    string sequence = row.PeptideModSeq;
                    int charge = row.PrecursorCharge;
                    string adduct = row.PrecursorAdduct;
                    var chemicalFormula = row.ChemicalFormula;
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
                        var moleculeName = row.MoleculeName;
                        var inChiKey = row.InchiKey;
                        var otherKeys = row.OtherKeys;
                        if (string.IsNullOrEmpty(chemicalFormula))
                        {
                            var precursorMz = row.PrecursorMz;
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
                            scoreTypesById.TryGetValue(row.ScoreType, out string scoreName);
                            libraryEntries.Add(new BiblioLiteSpectrumInfo(key, row.Copies, row.NumPeaks, (int)row.Id, fileIndex,
                                GetGlobalScores(row, proteinGroups), scoreName == null ? null : row.Score, scoreName));
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
                var retentionTimeReader = new RetentionTimeReader(FilePath, fileIdToIndex);
                retentionTimeReader.ReadAllRows(loader, ref status, rows);
                if (loader.IsCanceled)
                {
                    loader.UpdateProgress(status.Cancel());
                    return false;
                }
                _anyRedundantSpectra = retentionTimeReader.AnyRedundantSpectra;
                var retentionTimesBySpectraId = retentionTimeReader.GetRetentionTimes();
                var driftTimesBySpectraId = retentionTimeReader.GetIonMobilities();
                var peakBoundsBySpectraId = retentionTimeReader.GetExplicitPeakBounds();
                for (int i = 0; i < libraryEntries.Count; i++)
                {
                    var libraryEntry = libraryEntries[i];
                    // Share the ReplicatePositions between the entries which have values in the
                    // same files, which on a library where every spectrum was found in every file
                    // leaves one of them instead of one per spectrum.
                    if (retentionTimesBySpectraId.TryGetValue(libraryEntry.Id, out var retentionTimes))
                    {
                        libraryEntry = libraryEntry.ChangeRetentionTimes(retentionTimes.ValueFromCache(valueCache));
                    }

                    if (driftTimesBySpectraId.TryGetValue(libraryEntry.Id, out var driftTimes))
                    {
                        libraryEntry = libraryEntry.ChangeIonMobilities(driftTimes.ValueFromCache(valueCache));
                    }

                    if (peakBoundsBySpectraId.TryGetValue(libraryEntry.Id, out var peakBounds) && !peakBounds.IsEmpty)
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

        /// <summary>
        /// Reads every protein group each spectrum is mapped to (RefSpectraProteins is many-to-many),
        /// with the group's global statistics when the Proteins table has those columns. A group
        /// is one object shared by the entries mapped to it.
        /// </summary>
        private static Dictionary<long, ImmutableList<LibraryProteinGroup>> ProteinGroupsBySpectraId(BlibReader reader)
        {
            var proteinGroupsById = new Dictionary<long, LibraryProteinGroup>();
            foreach (var protein in reader.ReadProteinTable())
            {
                if (!string.IsNullOrEmpty(protein.Accession))
                    proteinGroupsById.Add(protein.Id, new LibraryProteinGroup(protein.Accession, protein.QValue, protein.Pep));
            }
            var proteinGroupsBySpectraId = new Dictionary<long, ImmutableList<LibraryProteinGroup>>();
            if (proteinGroupsById.Count == 0)
                return proteinGroupsBySpectraId;
            foreach (var mapping in reader.ReadProteinIds())
            {
                var proteinGroups = new List<LibraryProteinGroup>();
                foreach (long proteinId in mapping)
                {
                    if (proteinGroupsById.TryGetValue(proteinId, out var proteinGroup))
                        proteinGroups.Add(proteinGroup);
                }
                if (proteinGroups.Count > 0)
                    proteinGroupsBySpectraId.Add(mapping.Key, ImmutableList.ValueOf(proteinGroups));
            }
            return proteinGroupsBySpectraId;
        }

        /// <summary>
        /// The levels with global q-values: those whose optional column the library has with a
        /// value in at least one row. A column that is NULL throughout would offer a cutoff every
        /// entry fails.
        /// </summary>
        private static IList<GlobalQValueLevel> ReadGlobalQValueLevels(BlibReader reader)
        {
            var levels = new List<GlobalQValueLevel>();
            if (reader.HasTable(BlibReader.TABLE_REF_SPECTRA_PROTEINS) && reader.HasValues(BlibReader.TABLE_PROTEINS, BlibSchema.ProteinQValue.Name))
                levels.Add(GlobalQValueLevel.protein);
            if (reader.HasValues(BlibReader.TABLE_REF_SPECTRA, BlibSchema.PeptideQValue.Name))
                levels.Add(GlobalQValueLevel.peptide);
            if (reader.HasValues(BlibReader.TABLE_REF_SPECTRA, BlibSchema.PrecursorQValue.Name))
                levels.Add(GlobalQValueLevel.precursor);
            return levels;
        }

        private static LibraryGlobalScores GetGlobalScores(BlibSpectrumRecord row, ImmutableList<LibraryProteinGroup> proteinGroups)
        {
            if (proteinGroups == null && !row.PrecursorQValue.HasValue && !row.PrecursorPep.HasValue &&
                !row.PeptideQValue.HasValue && !row.PeptidePep.HasValue)
            {
                return LibraryGlobalScores.EMPTY;
            }
            return new LibraryGlobalScores(row.PrecursorQValue, row.PrecursorPep, row.PeptideQValue,
                row.PeptidePep, proteinGroups ?? ImmutableList<LibraryProteinGroup>.EMPTY);
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
                if (x is SQLiteException || !ExceptionUtil.IsProgrammingDefect(x))
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
            return _sqliteConnection.ExecuteWithReader(reader =>
            {
                var peaks = reader.ReadPeaks(info.Id, info.NumPeaks);
                return peaks == null ? null : ToPeaks(peaks, reader.ReadAnnotations(info.Id));
            });
        }

        private SpectrumPeaksInfo.MI[] ReadRedundantSpectrum(int spectrumId)
        {
            if (_sqliteConnectionRedundant == null)
                throw new IOException(string.Format(LibResources.BiblioSpecLiteLibrary_ReadRedundantSpectrum_The_redundant_library__0__does_not_exist, FilePathRedundant));

            return _sqliteConnectionRedundant.ExecuteWithReader(reader =>
            {
                var spectrum = reader.ReadSpectrum(spectrumId, true);
                if (spectrum == null)
                    return null;
                if (spectrum.NumPeaks == 0)
                    return Array.Empty<SpectrumPeaksInfo.MI>();
                var peaks = BlibReader.DecodePeaks(spectrum);
                return peaks == null ? null : ToPeaks(peaks, reader.ReadAnnotations(spectrumId));
            });
        }

        /// <summary>
        /// The peaks of a spectrum with the annotations the library keeps for them. A peak may
        /// carry more than one annotation and not every peak has one; small molecules need them,
        /// where for peptides Skyline generates its own fragment information. Each names the
        /// peak's index and the m/z stored for it, which must agree with the peak list.
        /// </summary>
        private static SpectrumPeaksInfo.MI[] ToPeaks(BlibPeaks peaks, IReadOnlyList<BlibPeakAnnotation> annotations)
        {
            var arrayMI = new SpectrumPeaksInfo.MI[peaks.Count];
            for (int i = 0; i < arrayMI.Length; i++)
            {
                arrayMI[i].Mz = peaks.Mzs[i];
                arrayMI[i].Intensity = peaks.Intensities[i];
            }

            foreach (var annotation in annotations)
            {
                int peakIndex = annotation.PeakIndex;
                if (Math.Abs(annotation.MzObserved - arrayMI[peakIndex].Mz) > 0.0000001)
                {
                    Assume.Fail(string.Format(@"trouble reading peak annotation: mzObserved {0} disagrees with indexed peak mz {1}",
                        annotation.MzObserved, arrayMI[peakIndex].Mz));
                }
                var molecule = SmallMoleculeLibraryAttributes.Create(annotation.Name, annotation.Formula, annotation.InchiKey, annotation.OtherKeys);
                var adduct = string.IsNullOrEmpty(annotation.Adduct)
                    ? Adduct.FromChargeNoMass(annotation.Charge)
                    : Adduct.FromStringAssumeChargeOnly(annotation.Adduct);
                arrayMI[peakIndex].Annotations ??= new List<SpectrumPeakAnnotation>();
                arrayMI[peakIndex].Annotations.Add(SpectrumPeakAnnotation.Create(molecule, adduct, annotation.Comment, annotation.MzTheoretical));
            }

            return arrayMI;
        }

        public override bool TryGetRetentionTimes(LibKey key, MsDataFileUri filePath, out double[] retentionTimes)
        {
            int i = FindEntry(key);
            int j = FindSource(filePath);
            if (i != -1 && j != -1)
            {
                retentionTimes = ToDoubleArray(_libraryEntries[i].RetentionTimesByFileIndex[j]);
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
            bool anySequenceMatch = false;
            foreach (var item in LibraryEntriesWithSequences(peptideSequences))
            {
                var peakBoundaries = item.PeakBoundariesByFileIndex[iFile];
                if (peakBoundaries != null)
                {
                    return peakBoundaries;
                }

                if (!item.PeakBoundariesByFileIndex.IsEmpty)
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

        public override ImmutableList<GlobalQValueLevel> GlobalQValueLevels
        {
            get { return _globalQValueLevels; }
        }

        public override LibraryGlobalScores GetGlobalScores(LibKey key)
        {
            int i = FindEntry(key);
            return i == -1 ? LibraryGlobalScores.EMPTY : _libraryEntries[i].GlobalScores;
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
                var dictionary = new Dictionary<Target, Tuple<TimeSource, double[]>>();
                foreach (var grouping in _libraryEntries.GroupBy(entry => entry.Key.Target))
                {
                    var times = grouping.SelectMany(entry => ToDoubleArray(entry.RetentionTimesByFileIndex[j]))
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
                times.Add(ToDoubleArray(item.RetentionTimesByFileIndex[iFile.Value]));
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



        public override bool TryGetIonMobilityInfos(LibKey key, MsDataFileUri filePath, out IonMobilityAndCCS[] ionMobilities)
        {
            int i = FindEntry(key);
            int j = FindSource(filePath);
            if (i != -1 && j != -1)
            {
                var entryIonMobilities = _libraryEntries[i].IonMobilitiesByFileIndex[j];
                ionMobilities = entryIonMobilities.Count == 0 ? null : entryIonMobilities.ToArray();
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
                ILookup<LibKey, IList<IonMobilityAndCCS>> ionMobilitiesLookup;
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
                        entry => entry.IonMobilitiesByFileIndex[fileIndex]);
                }
                else
                {
                    ionMobilitiesLookup = _libraryEntries.ToLookup(
                        entry => entry.Key,
                        entry => entry.IonMobilitiesByFileIndex[fileIndex]);
                }
                var ionMobilitiesDict = ionMobilitiesLookup.ToDictionary(
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
                        if (matchedItem.IonMobilitiesByFileIndex.IsEmpty)
                            continue;
                        var match = matchedItem.IonMobilitiesByFileIndex.FlatValues.ToArray();
                        Array.Sort(match);
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
            // In case of a failure to read the SQLite file
            catch (IOException)
            {
                return base.GetSpectra(key, labelType, redundancy);
            }
        }

        private IEnumerable<SpectrumInfoLibrary> GetRedundantSpectra(LibKey key, IsotopeLabelType labelType, LibraryRedundancy redundancy)
        {
            // No redundant spectra before schema version 1
            if (SchemaVersion == 0)
                return Array.Empty<SpectrumInfoLibrary>();
            int i = FindEntry(key);
            if (i == -1)
                return Array.Empty<SpectrumInfoLibrary>();

            var info = _libraryEntries[i];
            var listSpectra = _sqliteConnection.ExecuteWithReader(reader =>
            {
                var spectra = new List<SpectrumInfoLibrary>();
                // Only bother with the RetentionTimes table if it can lead to a redundant spectrum.
                // For libraries which never went through BlibFilter it holds nothing the spectrum's
                // own row does not.
                if (_anyRedundantSpectra)
                {
                    foreach (var time in reader.ReadSpectrumRetentionTimes(info.Id))
                    {
                        bool isBest = time.BestSpectrum;
                        if (redundancy == LibraryRedundancy.best && !isBest)
                            continue;
                        // An observation whose source file the library does not list is left out,
                        // as the join to the files table always left it.
                        if (!time.SpectrumSourceId.HasValue || !TryGetSourceFile(time.SpectrumSourceId.Value, out var file))
                            continue;
                        var ionMobility = GetIonMobility(time.IonMobility, time.CollisionalCrossSectionSqA,
                            time.IonMobilityHighEnergyOffset, time.IonMobilityType);
                        spectra.Add(CreateSpectrumInfo(i, labelType, file.FilePath, time.RetentionTime, ionMobility,
                            info.Protein, isBest, (int)time.RedundantRefSpectraId, redundancy));
                    }
                }
                else
                {
                    var spectrum = reader.ReadSpectrum(info.Id, false);
                    if (spectrum != null && TryGetSourceFile(spectrum.FileId, out var file))
                    {
                        var ionMobility = GetIonMobility(spectrum.IonMobility, spectrum.CollisionalCrossSectionSqA,
                            spectrum.IonMobilityHighEnergyOffset, spectrum.IonMobilityType);
                        spectra.Add(CreateSpectrumInfo(i, labelType, file.FilePath, spectrum.RetentionTime, ionMobility,
                            info.Protein, true, -1, redundancy));
                    }
                }
                return spectra;
            });
            // A spectrum the library records no source file for (one minimized from a library
            // without files) is still the best spectrum of its entry.
            if (listSpectra.Count == 0 && !_anyRedundantSpectra)
                return base.GetSpectra(key, labelType, redundancy);
            return listSpectra;
        }

        private SpectrumInfoLibrary CreateSpectrumInfo(int entryIndex, IsotopeLabelType labelType, string filePath, double? retentionTime,
            IonMobilityAndCCS ionMobility, string protein, bool isBest, int redundantId, LibraryRedundancy redundancy)
        {
            // If this is not a reference(best) spectrum, the spectrumKey should be of the
            // type SpectrumLiteKey so that peaks for this spectrum are loaded from the
            // redundant library. Otherwise, for best spectra, the key should simply be
            // the id of this spectrum in the cache.
            // Look at the LoadSpectrum method to see how spectrumKey is used.
            object spectrumKey = entryIndex;
            if (!isBest || redundancy == LibraryRedundancy.all_redundant)
                spectrumKey = new SpectrumLiteKey(entryIndex, redundantId, isBest);
            return new SpectrumInfoLibrary(this, labelType, filePath, retentionTime, ionMobility ?? IonMobilityAndCCS.EMPTY,
                protein, isBest, spectrumKey)
            {
                SpectrumHeaderInfo = CreateSpectrumHeaderInfo(_libraryEntries[entryIndex])
            };
        }

        private bool TryGetSourceFile(long id, out BiblioLiteSourceInfo file)
        {
            foreach (var sourceFile in _librarySourceFiles)
            {
                if (sourceFile.Id == id)
                {
                    file = sourceFile;
                    return true;
                }
            }
            file = default;
            return false;
        }

        /// <summary>
        /// The ion mobility of a library row, or null when the row has none: every value zero or
        /// missing, as libraries have always written a spectrum without one. A null value is
        /// passed through as unknown; a caller that wants the zero the row holds passes it.
        /// </summary>
        private static IonMobilityAndCCS GetIonMobility(double? mobility, double? collisionalCrossSection, double? highEnergyOffset, int ionMobilityType)
        {
            if ((mobility ?? 0) == 0 && (collisionalCrossSection ?? 0) == 0 && (highEnergyOffset ?? 0) == 0)
                return null;
            var units = (eIonMobilityUnits)ionMobilityType;
            return IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(mobility, units),
                collisionalCrossSection, highEnergyOffset);
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

            return _sqliteConnection.ExecuteWithReader(reader => reader.CountRetentionTimes());
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
            var connection = isBest ? _sqliteConnection : _sqliteConnectionRedundant;
            return connection.ExecuteWithReader(reader =>
            {
                var spectrum = reader.ReadSpectrum(id, false);
                // Should never reach here, as there should always be a row matching the id
                if (spectrum == null)
                    return null;
                var sheetInfo = new BiblioSpecSheetInfo
                {
                    SpecIdInFile = spectrum.SpecIdInFile,
                    Count = spectrum.Copies,
                };
                var sourceFile = reader.ReadSourceFiles().FirstOrDefault(file => file.Id == spectrum.FileId);
                if (sourceFile != null)
                {
                    sheetInfo.IDFileName = sourceFile.IdFileName;
                    sheetInfo.FileName = sourceFile.FileName;
                    sheetInfo.WorkflowType = (WorkflowType)sourceFile.WorkflowType;
                }
                var scoreType = reader.ReadScoreTypeTable().FirstOrDefault(type => type.Id == spectrum.ScoreType);
                if (scoreType != null)
                {
                    sheetInfo.Score = spectrum.Score;
                    sheetInfo.ScoreType = ScoreType.INVARIANT_NAMES.Contains(scoreType.Name)
                        ? new ScoreType(scoreType.Name, scoreType.ProbabilityType).ToString()
                        : scoreType.Name;
                }
                return sheetInfo;
            });
        }

        public void DeleteDataFiles(string[] filenames, IProgressMonitor monitor)
        {
            // Release our handle to the redundant library before writing to it and before shelling
            // out to BlibFilter.exe, which opens it with File.OpenRead (FileShare.Read). On net8 the
            // still-open SQLite connection keeps a write handle, making that read a sharing
            // violation ("... cannot be opened"); net472 released it in time. BlibFilter reads the
            // file from disk, so the in-process connection is not needed for the filter step.
            _sqliteConnectionRedundant.CloseStream();

            // Make the changes to the redundant library and then use BlibFilter
            using (var writer = BlibWriter.Open(FilePathRedundant, true))
            {
                writer.DeleteSourceFiles(filenames);
            }

            // Write the non-redundant library to a temporary file first
            using (var saver = new FileSaver(FilePath))
            {
                var blibFilter = new BlibFilter();
                IProgressStatus status = new ProgressStatus(LibResources.BiblioSpecLiteLibrary_DeleteDataFiles_Removing_library_runs_from_document_library_);
                if (!blibFilter.Filter(FilePathRedundant, saver.SafeName, monitor, ref status))
                    throw new IOException(string.Format(LibResources.BiblioSpecLiteLibrary_DeleteDataFiles_Failed_attempting_to_filter_redundant_library__0__to__1_, FilePathRedundant, FilePath));

                _sqliteConnection.CloseStream();
                saver.Commit();
            }
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

        class RetentionTimeReader
        {
            private readonly string _dbPath;

            private Dictionary<int, IndexedMultiArray<float>> _retentionTimes =
                new Dictionary<int, IndexedMultiArray<float>>();

            private Dictionary<int, IndexedMultiArray<IonMobilityAndCCS>> _ionMobilities =
                new Dictionary<int, IndexedMultiArray<IonMobilityAndCCS>>();

            private Dictionary<int, ExplicitPeakBoundsList> _explicitPeakBounds =
                new Dictionary<int, ExplicitPeakBoundsList>();

            private IProgressMonitor _progressMonitor;
            private IProgressStatus _progressStatus;
            private int _progressValue;
            private int _completedSpectraCount;
            private int _refSpectraCount;

            public RetentionTimeReader(string dbPath, Dictionary<long, int> fileIdToIndex)
            {
                _dbPath = dbPath;
                FileIdToIndex = fileIdToIndex;
            }

            /// <summary>
            /// The index in <see cref="LibraryFiles"/> of each spectrum source file, keyed by the
            /// file's id in the library database. Rows for files which are not in here are skipped.
            /// </summary>
            public IDictionary<long, int> FileIdToIndex { get; private set; }

            /// <summary>
            /// True if any row pointed at a spectrum in the redundant library. Libraries which were
            /// built without going through BlibFilter (e.g. DIA-NN) always store zero here, which
            /// means there is nothing a redundant spectrum could ever be loaded from.
            /// This is only ever set to true, so the reader threads need no synchronization.
            /// </summary>
            public bool AnyRedundantSpectra { get; private set; }

            // ReSharper disable InconsistentlySynchronizedField
            public Dictionary<int, IndexedMultiArray<float>> GetRetentionTimes()
            {
                return _retentionTimes;
            }

            public Dictionary<int, IndexedMultiArray<IonMobilityAndCCS>> GetIonMobilities()
            {
                return _ionMobilities;
            }

            public Dictionary<int, ExplicitPeakBoundsList> GetExplicitPeakBounds()
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
                    // Each thread scans its own range of spectrum ids on a reader of its own
                    using var reader = new BlibReader(_dbPath);
                    ReadSubset(reader, threadCount, threadIndex);
                }, maxThreads: threadCount);
                status = _progressStatus;
            }

            private void ReadSubset(BlibReader reader, int threadCount, int threadIndex)
            {
                var rows = new List<BlibRetentionTimeRecord>();
                long? lastRefSpectraId = null;
                foreach (var row in reader.ReadRetentionTimes(threadCount, threadIndex))
                {
                    // A row without a spectrum id reads as 0, which no spectrum has
                    if (row.RefSpectraId == 0)
                    {
                        continue;
                    }
                    if (row.RefSpectraId != lastRefSpectraId)
                    {
                        if (rows.Count != 0)
                        {
                            if (!ConsumeRows(rows))
                            {
                                return;
                            }
                            rows = new List<BlibRetentionTimeRecord>();
                        }

                        lastRefSpectraId = row.RefSpectraId;
                    }
                    rows.Add(row);
                }

                if (rows.Count != 0)
                {
                    ConsumeRows(rows);
                }
            }

            private bool ConsumeRows(List<BlibRetentionTimeRecord> rows)
            {
                int refSpectraId = (int)rows[0].RefSpectraId;
                var retentionTimes = new List<KeyValuePair<int, float>>();
                var ionMobilities = new List<KeyValuePair<int, IonMobilityAndCCS>>();
                // One entry per file index, holding null for the files which have no boundaries
                ExplicitPeakBounds[] explicitPeakBounds = null;
                foreach (var row in rows)
                {
                    if (row.RedundantRefSpectraId != 0)
                    {
                        AnyRedundantSpectra = true;
                    }

                    long? fileId = row.SpectrumSourceId;
                    if (!fileId.HasValue)
                    {
                        continue;
                    }

                    // Everything here is keyed by the file's index in LibraryFiles rather than by
                    // its id in the library database.
                    if (!FileIdToIndex.TryGetValue(fileId.Value, out int fileIndex))
                    {
                        continue;
                    }

                    if (row.RetentionTime.HasValue)
                    {
                        retentionTimes.Add(new KeyValuePair<int, float>(fileIndex, (float) row.RetentionTime.Value));
                    }

                    var ionMobility = ReadIonMobilityInfo(row);
                    if (ionMobility != null)
                    {
                        ionMobilities.Add(new KeyValuePair<int, IonMobilityAndCCS>(fileIndex, ionMobility));
                    }

                    var peakBounds = ReadPeakBounds(row);
                    if (peakBounds != null)
                    {
                        explicitPeakBounds ??= new ExplicitPeakBounds[FileIdToIndex.Count];
                        explicitPeakBounds[fileIndex] ??= peakBounds;
                    }
                }

                if (retentionTimes.Count > 0)
                {
                    var indexedRetentionTimes = retentionTimes.ToIndexedMultiArray();
                    lock (_retentionTimes)
                    {
                        if (_retentionTimes.TryGetValue(refSpectraId, out var existing))
                        {
                            _retentionTimes[refSpectraId] = existing.Merge(indexedRetentionTimes);
                        }
                        else
                        {
                            _retentionTimes.Add(refSpectraId, indexedRetentionTimes);
                        }
                    }
                }

                if (ionMobilities.Count > 0)
                {
                    var indexedIonMobilities = ionMobilities.ToIndexedMultiArray();
                    lock (_ionMobilities)
                    {
                        if (_ionMobilities.TryGetValue(refSpectraId, out var existing))
                        {
                            _ionMobilities[refSpectraId] = existing.Merge(indexedIonMobilities);
                        }
                        else
                        {
                            _ionMobilities.Add(refSpectraId, indexedIonMobilities);
                        }
                    }
                }

                if (explicitPeakBounds != null)
                {
                    lock (_explicitPeakBounds)
                    {
                        if (_explicitPeakBounds.TryGetValue(refSpectraId, out var existing))
                        {
                            _explicitPeakBounds[refSpectraId] = existing.Merge(explicitPeakBounds);
                        }
                        else
                        {
                            _explicitPeakBounds.Add(refSpectraId, new ExplicitPeakBoundsList(explicitPeakBounds));
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

            /// <summary>
            /// The ion mobility of an observation, with the zero the row holds for a value it lacks
            /// (a library has always written every ion mobility column of a spectrum without one),
            /// or null when there is none at all; the reader has already read the columns of the
            /// older schemas as the current ones.
            /// </summary>
            public static IonMobilityAndCCS ReadIonMobilityInfo(BlibRetentionTimeRecord row)
            {
                return GetIonMobility(row.IonMobility ?? 0, row.CollisionalCrossSectionSqA ?? 0,
                    row.IonMobilityHighEnergyOffset ?? 0, row.IonMobilityType);
            }

            public static ExplicitPeakBounds ReadPeakBounds(BlibRetentionTimeRecord row)
            {
                if (row.StartTime.HasValue && row.EndTime.HasValue)
                {
                    return new ExplicitPeakBounds(row.StartTime.Value, row.EndTime.Value, row.Score ?? ExplicitPeakBounds.UNKNOWN_SCORE);
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

        public override Dictionary<Target, double>[] GetAllRetentionTimes()
        {
            var result = Enumerable.Range(0, LibraryFiles.Count)
                .Select(i => new Dictionary<Target, double>()).ToArray();
            foreach (var grouping in _libraryEntries.GroupBy(entry => entry.Key.Target))
            {
                for (int iFile = 0; iFile < LibraryFiles.Count; iFile ++)
                {
                    var minTime = grouping.SelectMany(entry => entry.RetentionTimesByFileIndex[iFile])
                        .Append(float.MaxValue).Min();
                    if (minTime != float.MaxValue)
                    {
                        result[iFile].Add(grouping.Key, minTime);
                    }
                }
            }

            return result;
        }

        public override IList<double>[] GetRetentionTimesWithSequences(ICollection<Target> targets)
        {
            var result = new List<double>[LibraryFiles.Count];
            for (int fileIndex = 0; fileIndex < result.Length; fileIndex++)
            {
                result[fileIndex] = new List<double>();
            }

            foreach (var entry in EntriesMatching(targets))
            {
                AppendTimesTo(entry.RetentionTimesByFileIndex, result);
            }

            return result;
        }

        public override IList<double> GetRetentionTimesWithSequences(int fileIndex, ICollection<Target> targets)
        {
            var result = new List<double>();
            foreach (var entry in EntriesMatching(targets))
            {
                result.AddRange(ToDoubleArray(entry.RetentionTimesByFileIndex[fileIndex]));
            }

            return result;
        }

        private IEnumerable<BiblioLiteSpectrumInfo> EntriesMatching(IEnumerable<Target> targets)
        {
            return targets.SelectMany(target =>
                _libraryEntries.ItemsMatching(new LibKey(target, Adduct.EMPTY), false));
        }

        private static double[] ToDoubleArray(IList<float> values)
        {
            var result = new double[values.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = values[i];
            }

            return result;
        }

        /// <summary>
        /// Appends the times of each file to the list at the same index of
        /// <paramref name="timesByFileIndex"/>, which must have an entry for every file.
        /// </summary>
        private static void AppendTimesTo(IndexedMultiArray<float> times, IList<List<double>> timesByFileIndex)
        {
            for (int fileIndex = 0; fileIndex < times.Count; fileIndex++)
            {
                int start = times.ReplicatePositions.GetStart(fileIndex);
                int end = start + times.GetCount(fileIndex);
                if (start == end)
                {
                    continue;
                }

                var fileTimes = timesByFileIndex[fileIndex];
                for (int i = start; i < end; i++)
                {
                    fileTimes.Add(times.FlatValues[i]);
                }
            }
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

    public class BiblioLiteSpectrumInfo : Immutable, ICachedSpectrumInfo
    {
        public BiblioLiteSpectrumInfo(LibKey key, int copies, int numPeaks, int id, int? spectrumSourceIndex,
            LibraryGlobalScores globalScores, double? score = null, string scoreType = null)
        {
            Key = key;
            Copies = copies;
            NumPeaks = numPeaks;
            Id = id;
            SpectrumSourceIndex = spectrumSourceIndex;
            RetentionTimesByFileIndex = IndexedMultiArray<float>.EMPTY;
            IonMobilitiesByFileIndex = IndexedMultiArray<IonMobilityAndCCS>.EMPTY;
            PeakBoundariesByFileIndex = ExplicitPeakBoundsList.EMPTY;
            Score = score;
            ScoreType = scoreType;
            GlobalScores = globalScores ?? LibraryGlobalScores.EMPTY;
        }

        /// <summary>
        /// An entry whose only protein information is a name (a protein accession, or a molecule
        /// list name for a small molecule), which becomes its one protein group without values.
        /// </summary>
        public BiblioLiteSpectrumInfo(LibKey key, int copies, int numPeaks, int id, int? spectrumSourceIndex, string protein,
            double? score = null, string scoreType = null)
            : this(key, copies, numPeaks, id, spectrumSourceIndex, LibraryGlobalScores.ForProtein(protein), score, scoreType)
        {
        }

        public LibKey Key { get; }
        public int Copies { get; }
        public int NumPeaks { get; }
        public int Id { get; }
        /// <summary>
        /// The index in <see cref="LibraryFiles"/> of the file which the best spectrum came from,
        /// or null when it is not known. Note that this is not the file's id in the library
        /// database.
        /// </summary>
        public int? SpectrumSourceIndex { get; }
        /// <summary>
        /// The entry's first protein group's name from the RefSpectraProteins table: a protein
        /// accession, or an arbitrary molecule list name. Read from <see cref="GlobalScores"/>
        /// rather than held again on every entry; the group is shared by the entries mapped to it.
        /// </summary>
        public string Protein
        {
            get { return GlobalScores.ProteinGroups.Count > 0 ? GlobalScores.ProteinGroups[0].Name : null; }
        }
        public IndexedMultiArray<float> RetentionTimesByFileIndex { get; private set; }
        public IndexedMultiArray<IonMobilityAndCCS> IonMobilitiesByFileIndex { get; private set; }
        public ExplicitPeakBoundsList PeakBoundariesByFileIndex { get; private set; }
        public double? Score { get; }
        public string ScoreType { get; }
        public LibraryGlobalScores GlobalScores { get; }

        public BiblioLiteSpectrumInfo ChangeRetentionTimes(IndexedMultiArray<float> retentionTimes)
        {
            return ChangeProp(ImClone(this), im => im.RetentionTimesByFileIndex = retentionTimes);
        }

        public BiblioLiteSpectrumInfo ChangeIonMobilities(IndexedMultiArray<IonMobilityAndCCS> ionMobilities)
        {
            return ChangeProp(ImClone(this), im => im.IonMobilitiesByFileIndex = ionMobilities);
        }

        public BiblioLiteSpectrumInfo ChangePeakBoundaries(ExplicitPeakBoundsList peakBoundaries)
        {
            return ChangeProp(ImClone(this), im => im.PeakBoundariesByFileIndex = peakBoundaries);
        }
    }
}
