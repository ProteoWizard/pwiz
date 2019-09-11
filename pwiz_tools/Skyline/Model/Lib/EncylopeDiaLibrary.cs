/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.Database;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib.ChromLib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
// ReSharper disable InvokeAsExtensionMethod

namespace pwiz.Skyline.Model.Lib
{
    [XmlRoot("elib_spec")]
    public sealed class EncyclopeDiaSpec : LibrarySpec
    {
        public const string EXT = ".elib";
        public static string FILTER_ELIB
        {
            get { return TextUtil.FileDialogFilter(Resources.EncyclopediaSpec_FILTER_ELIB_EncyclopeDIA_Library, EXT); }
        }

        public EncyclopeDiaSpec(string name, string path)
            : base(name, path)
        {
        }

        public override Library LoadLibrary(ILoadMonitor loader)
        {
            return EncyclopeDiaLibrary.Load(this, loader);
        }

        public override IEnumerable<PeptideRankId> PeptideRankIds
        {
            get { return new[] { PEP_RANK_PICKED_INTENSITY }; }
        }

        public override string Filter
        {
            get { return FILTER_ELIB; }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private EncyclopeDiaSpec()
        {
        }

        public static EncyclopeDiaSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new EncyclopeDiaSpec());
        }

        #endregion
    }

    [XmlRoot("elib_library")]
    public sealed class EncyclopeDiaLibrary : CachedLibrary<EncyclopeDiaLibrary.ElibSpectrumInfo>
    {
        private const int FORMAT_VERSION_CACHE = 5;
        private const double MIN_QUANTITATIVE_INTENSITY = 1.0;
        private ImmutableList<string> _sourceFiles;
        private readonly PooledSqliteConnection _pooledSqliteConnection;

        private EncyclopeDiaLibrary()
        {
            
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            reader.Read();
        }

        public static string FILTER_ELIB
        {
            get { return TextUtil.FileDialogFilter(Resources.EncyclopediaLibrary_FILTER_ELIB_EncyclopeDIA_Libraries, EncyclopeDiaSpec.EXT); }
        }

        public EncyclopeDiaLibrary(EncyclopeDiaSpec spec) : base(spec)
        {
            LibrarySpec = spec;
            FilePath = spec.FilePath;
            CachePath = GetLibraryCachePath(FilePath);
        }

        private EncyclopeDiaLibrary(EncyclopeDiaSpec spec, IStreamManager streamManager) : this(spec)
        {
            _pooledSqliteConnection = new PooledSqliteConnection(streamManager.ConnectionPool, FilePath);
        }

        public EncyclopeDiaSpec LibrarySpec { get; private set; }
        public string FilePath { get; private set; }
        public override int CompareRevisions(Library library)
        {
            return 0;
        }

        protected override LibrarySpec CreateSpec()
        {
            return new EncyclopeDiaSpec(Name, FilePath);
        }

        public override bool IsSameLibrary(Library library)
        {
            var encyclopediaLibrary = library as EncyclopeDiaLibrary;
            if (encyclopediaLibrary == null)
            {
                return false;
            }
            return Equals(Name, encyclopediaLibrary.Name);
        }

        public override LibraryDetails LibraryDetails
        {
            get
            {
                return new LibraryDetails
                {
                    DataFiles = _sourceFiles.Select(file => new SpectrumSourceFileDetails(file))
                };
            }
        }

        public override IPooledStream ReadStream
        {
            get { return _pooledSqliteConnection; }
        }

        public override string SpecFilter
        {
            get { return TextUtil.FileDialogFiltersAll(FILTER_ELIB); }
        }

        public static EncyclopeDiaLibrary Load(EncyclopeDiaSpec spec, ILoadMonitor loader)
        {
            if (File.Exists(spec.FilePath) && new FileInfo(spec.FilePath).Length > 0)
            {
                var library = new EncyclopeDiaLibrary(spec, loader.StreamManager);
                if (library.Load(loader))
                    return library;
            }
            return null;
        }
        private bool Load(ILoadMonitor loader)
        {
            try
            {
                if (LoadFromCache(loader))
                {
                    return true;
                }

                if (LoadLibraryFromDatabase(loader))
                {
                    WriteCache(loader);
                    return true;
                }
            }
            catch (Exception e)
            {
                if (!loader.IsCanceled)
                {
                    var msgException = new ApplicationException(string.Format(Resources.BiblioSpecLiteLibrary_Load_Failed_loading_library__0__, FilePath), e);
                    loader.UpdateProgress(new ProgressStatus().ChangeErrorException(msgException));
                }
            }
            return false;
        }

        // ReSharper disable LocalizableElement
        private bool LoadLibraryFromDatabase(ILoadMonitor loader)
        {
            var status = new ProgressStatus(
                string.Format(Resources.ChromatogramLibrary_LoadLibraryFromDatabase_Reading_precursors_from__0_,
                    Name));
            try
            {
                loader.UpdateProgress(status);
                var libKeySourceFileDatas = new Dictionary<Tuple<string, int>, Dictionary<string, Tuple<double?, FileData>>>();

                var scores = new EncyclopeDiaScores();
                scores.ReadScores(_pooledSqliteConnection.Connection);
                HashSet<Tuple<string, int>> quantPeptides = new HashSet<Tuple<string, int>>();

                using (var cmd = new SQLiteCommand(_pooledSqliteConnection.Connection))
                {
                    // From the "entries" table, read all of the peptides that were actually found
                    cmd.CommandText =
                        "SELECT PeptideModSeq, PrecursorCharge, SourceFile, Score, RTInSeconds, RTInSecondsStart, RTInSecondsStop FROM entries";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (loader.IsCanceled)
                            {
                                throw new OperationCanceledException();
                            }
                            var libKey = Tuple.Create(reader.GetString(0), Convert.ToInt32(reader.GetValue(1)));
                            // Tuple of filename, score, FileData
                            Dictionary<string, Tuple<double?, FileData>> dataByFilename;
                            
                            if (!libKeySourceFileDatas.TryGetValue(libKey, out dataByFilename))
                            {
                                dataByFilename = new Dictionary<string, Tuple<double?, FileData>>();
                                libKeySourceFileDatas.Add(libKey, dataByFilename);
                            }
                            string fileName = reader.GetString(2);
                            if (dataByFilename.ContainsKey(fileName))
                            {
                                continue;
                            }
                            double score = reader.GetDouble(3);
                            var qValue = scores.GetQValue(libKey.Item1, libKey.Item2, fileName) ??
                                         ExplicitPeakBounds.UNKNOWN_SCORE;
                            dataByFilename.Add(fileName, Tuple.Create((double?) score,
                                new FileData(reader.GetDouble(4)/60, 
                                new ExplicitPeakBounds(reader.GetDouble(5)/60, reader.GetDouble(6)/60, qValue))));
                        }
                    }

                    // Also, read the PeptideQuants table in order to get peak boundaries for any peptide&sourcefiles that were
                    // not found in the Entries table.
                    cmd.CommandText =
                        "SELECT PeptideModSeq, PrecursorCharge, SourceFile, RTInSecondsStart, RTInSecondsStop FROM PeptideQuants"; 
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var libKey = Tuple.Create(reader.GetString(0), Convert.ToInt32(reader.GetValue(1)));
                            quantPeptides.Add(libKey);
                            // Tuple of filename, score, FileData
                            Dictionary<string, Tuple<double?, FileData>> dataByFilename;
                            if (!libKeySourceFileDatas.TryGetValue(libKey, out dataByFilename))
                            {
                                dataByFilename = new Dictionary<string, Tuple<double?, FileData>>();
                                libKeySourceFileDatas.Add(libKey, dataByFilename);
                            }
                            string fileName = reader.GetString(2);
                            if (dataByFilename.ContainsKey(fileName))
                            {
                                continue;
                            }
                            double qValue = scores.GetQValue(libKey.Item1, libKey.Item2, fileName) ??
                                            ExplicitPeakBounds.UNKNOWN_SCORE;
                            dataByFilename.Add(fileName,
                                Tuple.Create((double?) null,
                                    new FileData(null, new ExplicitPeakBounds(reader.GetDouble(3)/60, reader.GetDouble(4)/60, qValue))));
                        }
                    }
                }
                // ReSharper disable PossibleMultipleEnumeration
                var sourceFiles = libKeySourceFileDatas
                    .SelectMany(entry => entry.Value.Keys)
                    .Distinct()
                    .ToArray();
                Array.Sort(sourceFiles);
                var sourceFileIds = sourceFiles.Select((file, index) => Tuple.Create(file, index))
                    .ToDictionary(tuple => tuple.Item1, tuple => tuple.Item2);
                var spectrumInfos = libKeySourceFileDatas
                    .Where(entry => quantPeptides.Contains(entry.Key))
                    .Select(entry => MakeSpectrumInfo(entry.Key.Item1, entry.Key.Item2, entry.Value, sourceFileIds));
                SetLibraryEntries(spectrumInfos);
                _sourceFiles = ImmutableList.ValueOf(sourceFiles);
                // ReSharper restore PossibleMultipleEnumeration
                loader.UpdateProgress(status.Complete());
                return true;
            }
            catch (Exception e)
            {
                e = new InvalidDataException(string.Format(Resources.BiblioSpecLiteLibrary_Load_Failed_loading_library__0__, FilePath), e);
                loader.UpdateProgress(status.ChangeErrorException(e));
                return false;
            }
        }
        // ReSharper restore LocalizableElement

        private void WriteCache(ILoadMonitor loader)
        {
            using (FileSaver fs = new FileSaver(CachePath, loader.StreamManager))
            {
                using (var stream = loader.StreamManager.CreateStream(fs.SafeName, FileMode.Create, true))
                {
                    PrimitiveArrays.WriteOneValue(stream, FORMAT_VERSION_CACHE);
                    PrimitiveArrays.WriteOneValue(stream, _sourceFiles.Count);
                    foreach (var file in _sourceFiles)
                    {
                        byte[] fileNameBytes = Encoding.UTF8.GetBytes(file);
                        PrimitiveArrays.WriteOneValue(stream, fileNameBytes.Length);
                        PrimitiveArrays.Write(stream, fileNameBytes);
                    }
                    PrimitiveArrays.WriteOneValue(stream, _libraryEntries.Length);
                    foreach (var elibSpectrumInfo in _libraryEntries)
                    {
                        elibSpectrumInfo.Write(stream);
                    }
                    loader.StreamManager.Finish(stream);
                    fs.Commit();
                    loader.StreamManager.SetCache(FilePath, CachePath);
                }
            }
        }

        private static string GetLibraryCachePath(string filepath)
        {
            return Path.ChangeExtension(filepath, @".elibc");
        }

        private bool LoadFromCache(ILoadMonitor loader)
        {
            if (!loader.StreamManager.IsCached(FilePath, CachePath))
            {
                return false;
            }
            try
            {
                ValueCache valueCache = new ValueCache();
                using (var stream = loader.StreamManager.CreateStream(CachePath, FileMode.Open, true))
                {
                    int version = PrimitiveArrays.ReadOneValue<int>(stream);
                    if (version != FORMAT_VERSION_CACHE)
                    {
                        return false;
                    }
                    int fileCount = PrimitiveArrays.ReadOneValue<int>(stream);
                    List<String> sourceFiles = new List<string>(fileCount);
                    while (sourceFiles.Count < fileCount)
                    {
                        int byteCount = PrimitiveArrays.ReadOneValue<int>(stream);
                        byte[] bytes = new byte[byteCount];
                        stream.Read(bytes, 0, bytes.Length);
                        sourceFiles.Add(Encoding.UTF8.GetString(bytes));
                    }
                    int spectrumInfoCount = PrimitiveArrays.ReadOneValue<int>(stream);
                    _sourceFiles = ImmutableList.ValueOf(sourceFiles);
                    List<ElibSpectrumInfo> spectrumInfos = new List<ElibSpectrumInfo>();
                    while (spectrumInfos.Count < spectrumInfoCount)
                    {
                        spectrumInfos.Add(ElibSpectrumInfo.Read(valueCache, stream));
                    }
                    SetLibraryEntries(spectrumInfos);
                    return true;
                }
            }
            catch (Exception exception)
            {
                Trace.TraceWarning(@"Exception loading cache: {0}", exception);
                return false;
            }
        }

        protected override SpectrumPeaksInfo.MI[] ReadSpectrum(ElibSpectrumInfo info)
        {
            return ReadSpectrum(info, info.BestFileId);
        }

        private SpectrumPeaksInfo.MI[] ReadSpectrum(ElibSpectrumInfo info, int sourceFileId)
        {
            if (sourceFileId < 0)
            {
                return null;
            }
            return _pooledSqliteConnection.ExecuteWithConnection(connection =>
            {
                HashSet<double> mzs = new HashSet<double>();
                List<SpectrumPeaksInfo.MI> spectrum = new List<SpectrumPeaksInfo.MI>();
                // First read all of the quantifiable transitions from the PeptideQuants table.
                var peptideQuantSpectrum = ReadSpectrumFromPeptideQuants(connection, info);
                if (peptideQuantSpectrum != null)
                {
                    foreach (var mi in peptideQuantSpectrum)
                    {
                        if (mzs.Add(mi.Mz))
                        {
                            spectrum.Add(mi);
                        }
                    }
                }
                // Then read the spectrum for the specific file
                var entriesSpectrum = ReadSpectrumFromEntriesTable(connection, info, sourceFileId);
                foreach (var mi in entriesSpectrum)
                {
                    if (mzs.Add(mi.Mz))
                    {
                        var miToAdd = mi;
                        if (peptideQuantSpectrum != null)
                        {
                            // If we successfully read from the PeptideQuants table, then the
                            // rest of the mzs we find in the entries table are non-quantitative.
                            miToAdd.Quantitative = false;
                        }
                        else
                        {
                            // If we were unable to read from the PeptideQuants table, then 
                            // the non-quantitative transitions are the ones with really low intensity.
                            miToAdd.Quantitative = miToAdd.Intensity >= MIN_QUANTITATIVE_INTENSITY;
                        }
                        spectrum.Add(miToAdd);
                    }
                }
                return spectrum.ToArray();
            });
        }

        private IEnumerable<SpectrumPeaksInfo.MI> ReadSpectrumFromPeptideQuants(SQLiteConnection connection, ElibSpectrumInfo info)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"SELECT QuantIonMassLength, QuantIonMassArray, QuantIonIntensityLength, QuantIonIntensityArray FROM peptidequants WHERE PrecursorCharge = ? AND PeptideModSeq = ?";
                cmd.Parameters.Add(new SQLiteParameter(DbType.Int32) { Value = info.Key.Charge });
                cmd.Parameters.Add(new SQLiteParameter(DbType.String) { Value = info.PeptideModSeq });
                SQLiteDataReader reader;
                try
                {
                    reader = cmd.ExecuteReader();
                }
                catch (DbException)
                {
                    // Older .elib files do not have these columns, so just return null
                    return null;
                }
                using (reader)
                {
                    if (!reader.Read())
                    {
                        // None of the transitions are considered Quantifiable.
                        return new SpectrumPeaksInfo.MI[0];
                    }
                    double[] mzs = PrimitiveArrays.FromBytes<double>(
                        PrimitiveArrays.ReverseBytesInBlocks(
                            UncompressEncyclopeDiaData((byte[])reader.GetValue(1), reader.GetInt32(0)),
                            sizeof(double)));
                    float[] intensities =
                        PrimitiveArrays.FromBytes<float>(PrimitiveArrays.ReverseBytesInBlocks(
                            UncompressEncyclopeDiaData((byte[])reader.GetValue(3), reader.GetInt32(2)), sizeof(float)));
                    return mzs.Select(
                        (mz, index) => new SpectrumPeaksInfo.MI { Mz = mz, Intensity = intensities[index]});
                }
            }
        }

        private IEnumerable<SpectrumPeaksInfo.MI> ReadSpectrumFromEntriesTable(SQLiteConnection connection, ElibSpectrumInfo info,
            int sourceFileId)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText =
                    @"SELECT MassEncodedLength, MassArray, IntensityEncodedLength, IntensityArray FROM entries WHERE PrecursorCharge = ? AND PeptideModSeq = ? AND SourceFile = ?";
                cmd.Parameters.Add(new SQLiteParameter(DbType.Int32) {Value = info.Key.Charge});
                cmd.Parameters.Add(new SQLiteParameter(DbType.String) {Value = info.PeptideModSeq});
                cmd.Parameters.Add(new SQLiteParameter(DbType.String) {Value = _sourceFiles[sourceFileId]});
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        double[] mzs = PrimitiveArrays.FromBytes<double>(
                            PrimitiveArrays.ReverseBytesInBlocks(
                                UncompressEncyclopeDiaData((byte[]) reader.GetValue(1), reader.GetInt32(0)),
                                sizeof(double)));
                        float[] intensities =
                            PrimitiveArrays.FromBytes<float>(PrimitiveArrays.ReverseBytesInBlocks(
                                UncompressEncyclopeDiaData((byte[]) reader.GetValue(3), reader.GetInt32(2)),
                                sizeof(float)));
                        return mzs.Select((mz, index) => new SpectrumPeaksInfo.MI
                                {
                                    Mz = mz,
                                    Intensity = intensities[index],
                                }) // CONSIDER(bspratt): annotation?
                            .ToArray();
                    }
                    return null;
                }
            }
        }

        /// <summary>
        /// Uncompress a block of data found in an EncyclopeDIA library.
        /// </summary>
        private byte[] UncompressEncyclopeDiaData(byte[] compressedBytes, int uncompressedSize)
        {
            // Pass -1 in for uncompressed length since EnclyclopeDIA always compresses
            // the bytes even if the uncompressed size is equal to the compresssed size.
            byte[] uncompressedBytes = UtilDB.Uncompress(compressedBytes, -1, false);
            if (uncompressedBytes.Length != uncompressedSize)
            {
                throw new IOException(Resources.UtilDB_Uncompress_Failure_uncompressing_data);
            }
            return uncompressedBytes;
        }

        protected override SpectrumHeaderInfo CreateSpectrumHeaderInfo(ElibSpectrumInfo info)
        {
            return new ChromLibSpectrumHeaderInfo(Name, 0);
        }

        public override LibraryFiles LibraryFiles
        {
            get
            {
                return new LibraryFiles{FilePaths = _sourceFiles};
            }
        }

        public override ExplicitPeakBounds GetExplicitPeakBounds(MsDataFileUri filePath, IEnumerable<Target> peptideSequences)
        {
            int fileId = FindFileInList(filePath, _sourceFiles);
            if (fileId < 0)
            {
                return null;
            }

            bool anyMatch = false;
            foreach (var entry in LibraryEntriesWithSequences(peptideSequences))
            {
                FileData fileData;
                if (entry.FileDatas.TryGetValue(fileId, out fileData))
                {
                    return fileData.PeakBounds;
                }

                if (entry.FileDatas.Any())
                {
                    anyMatch = true;
                }
            }

            if (anyMatch)
            {
                return ExplicitPeakBounds.EMPTY;
            }
            return null;
        }

        public override IEnumerable<SpectrumInfoLibrary> GetSpectra(LibKey key, IsotopeLabelType labelType, LibraryRedundancy redundancy)
        {
            int iEntry = FindEntry(key);
            if (iEntry < 0)
            {
                return new SpectrumInfoLibrary[0];
            }
            var entry = _libraryEntries[iEntry];
            return entry.FileDatas.Where(kvp =>
                {
                    if (!kvp.Value.ApexTime.HasValue)
                    {
                        return false;
                    }
                    if (redundancy == LibraryRedundancy.best && kvp.Key != entry.BestFileId)
                    {
                        return false;
                    }
                    return true;
                })
                .Select(kvp =>
                    new SpectrumInfoLibrary(this, labelType, _sourceFiles[kvp.Key], kvp.Value.ApexTime, null,
                        kvp.Key == entry.BestFileId, new ElibSpectrumKey(iEntry, kvp.Key))
                    {
                        SpectrumHeaderInfo = CreateSpectrumHeaderInfo(entry)
                    });
        }

        public override SpectrumPeaksInfo LoadSpectrum(object spectrumKey)
        {
            var elibSpectrumKey = spectrumKey as ElibSpectrumKey;
            if (null != elibSpectrumKey)
            {
                return new SpectrumPeaksInfo(ReadSpectrum(_libraryEntries[elibSpectrumKey.EntryIndex], elibSpectrumKey.FileId));
            }
            return base.LoadSpectrum(spectrumKey);
        }

        public override LibraryChromGroup LoadChromatogramData(object spectrumKey)
        {
            return null;
        }

        public override bool TryGetRetentionTimes(int fileId, out LibraryRetentionTimes retentionTimes)
        {
            return TryGetRetentionTimes(fileId, _sourceFiles[fileId], out retentionTimes);
        }

        public override bool TryGetRetentionTimes(MsDataFileUri filePath, out LibraryRetentionTimes retentionTimes)
        {
            return TryGetRetentionTimes(FindFileInList(filePath, _sourceFiles), filePath.ToString(), out retentionTimes);
        }

        private bool TryGetRetentionTimes(int fileId, string filePath, out LibraryRetentionTimes retentionTimes)
        {
            if (fileId < 0)
            {
                retentionTimes = null;
                return false;
            }
            ILookup<Target, double?> timesLookup = _libraryEntries.ToLookup(
                entry => entry.Key.Target,
                entry =>
                {
                    FileData fileData;
                    if (!entry.FileDatas.TryGetValue(fileId, out fileData))
                    {
                        return null;
                    }
                    return fileData.ApexTime;
                });
            var nonEmptyTimesDict = timesLookup
                .Where(grouping=>grouping.Any(value=>value.HasValue))
                .ToDictionary(grouping=>grouping.Key, grouping=>Tuple.Create(TimeSource.peak, grouping.OfType<double>().ToArray()));
            retentionTimes = new LibraryRetentionTimes(filePath, nonEmptyTimesDict);
            return true;
        }

        public override bool TryGetRetentionTimes(LibKey key, MsDataFileUri filePath, out double[] retentionTimes)
        {
            retentionTimes = null;
            int i = FindEntry(key);
            if (i < 0)
            {
                return false;
            }
            int fileId = FindFileInList(filePath, _sourceFiles);
            if (fileId < 0)
            {
                return false;
            }
            var entry = _libraryEntries[i];
            FileData fileData;
            if (!entry.FileDatas.TryGetValue(fileId, out fileData))
            {
                return false;
            }
            if (!fileData.ApexTime.HasValue)
            {
                return false;
            }
            retentionTimes = new[] {fileData.ApexTime.Value};
            return true;
        }

        public override IEnumerable<double> GetRetentionTimesWithSequences(string filePath, IEnumerable<Target> peptideSequences, ref int? iFile)
        {
            if (!iFile.HasValue)
                iFile = FindFileInList(MsDataFileUri.Parse(filePath), _sourceFiles);
            if (iFile.Value < 0)
            {
                return new double[0];
            }
            var times = new List<double>();
            
            foreach (var entry in LibraryEntriesWithSequences(peptideSequences))
            {
                FileData fileData;
                if (entry.FileDatas.TryGetValue(iFile.Value, out fileData))
                {
                    if (fileData.ApexTime.HasValue)
                    {
                        times.Add(fileData.ApexTime.Value);
                    }
                }
            }
            return times;
        }

        public override IList<RetentionTimeSource> ListRetentionTimeSources()
        {
            List<RetentionTimeSource> sources = new List<RetentionTimeSource>();
            foreach (var sourceFile in _sourceFiles)
            {
                try
                {
                    sources.Add(new RetentionTimeSource(Path.GetFileNameWithoutExtension(sourceFile), Name));
                }
                catch (Exception)
                {
                    // ignore
                }
            }
            return sources;
        }

        public static EncyclopeDiaLibrary Deserialize(XmlReader reader)
        {
            EncyclopeDiaLibrary encyclopeDiaLibrary = new EncyclopeDiaLibrary();
            encyclopeDiaLibrary.ReadXml(reader);
            return encyclopeDiaLibrary;
        }

        private static ElibSpectrumInfo MakeSpectrumInfo(string peptideModSeq, int charge,
            IDictionary<string, Tuple<double?, FileData>> fileDatas, IDictionary<string, int> sourceFileIds)
        {
            double bestScore = double.MaxValue;
            string bestFileName = null;

            foreach (var entry in fileDatas)
            {
                if (!entry.Value.Item1.HasValue)
                {
                    continue;
                }
                if (bestFileName == null || entry.Value.Item1 < bestScore)
                {
                    bestFileName = entry.Key;
                    bestScore = entry.Value.Item1.Value;
                }
            }
            return new ElibSpectrumInfo(peptideModSeq, charge, bestFileName == null ? -1 : sourceFileIds[bestFileName],
                fileDatas.Select(
                    entry => new KeyValuePair<int, FileData>(sourceFileIds[entry.Key], entry.Value.Item2)));

        }

        public class ElibSpectrumInfo : ICachedSpectrumInfo
        {
            public ElibSpectrumInfo(String peptideModSeq, int charge, int bestFileId, IEnumerable<KeyValuePair<int, FileData>> fileDatas)
            {
                PeptideModSeq = peptideModSeq;
                Key = new LibKey(SequenceMassCalc.NormalizeModifiedSequence(peptideModSeq), charge);
                BestFileId = bestFileId;
                FileDatas = ImmutableSortedList.FromValues(fileDatas);
            }

            public string PeptideModSeq { get; private set; }
            public LibKey Key { get; private set; }
            public int BestFileId { get; private set;}
            public ImmutableSortedList<int, FileData> FileDatas { get; private set; }

            public void Write(Stream stream)
            {
                PrimitiveArrays.WriteOneValue(stream, PeptideModSeq.Length);
                PrimitiveArrays.Write(stream, Encoding.UTF8.GetBytes(PeptideModSeq));
                PrimitiveArrays.WriteOneValue(stream, Key.Charge);
                PrimitiveArrays.WriteOneValue(stream, BestFileId);
                PrimitiveArrays.WriteOneValue(stream, FileDatas.Count);
                foreach (var peakBoundEntry in FileDatas)
                {
                    PrimitiveArrays.WriteOneValue(stream, peakBoundEntry.Key);
                    PrimitiveArrays.WriteOneValue(stream, peakBoundEntry.Value.PeakBounds.StartTime);
                    PrimitiveArrays.WriteOneValue(stream, peakBoundEntry.Value.PeakBounds.EndTime);
                    PrimitiveArrays.WriteOneValue(stream, peakBoundEntry.Value.PeakBounds.Score);
                    if (peakBoundEntry.Value.ApexTime.HasValue)
                    {
                        PrimitiveArrays.WriteOneValue<byte>(stream, 1);
                        PrimitiveArrays.WriteOneValue(stream, peakBoundEntry.Value.ApexTime.Value);
                    }
                    else
                    {
                        PrimitiveArrays.WriteOneValue<byte>(stream, 0);
                    }
                }
            }

            public static ElibSpectrumInfo Read(ValueCache valueCache, Stream stream)
            {
                byte[] peptideModSeqBytes = new byte[PrimitiveArrays.ReadOneValue<int>(stream)];
                stream.Read(peptideModSeqBytes, 0, peptideModSeqBytes.Length);
                var peptideModSeq = valueCache.CacheValue(Encoding.UTF8.GetString(peptideModSeqBytes));
                int charge = PrimitiveArrays.ReadOneValue<int>(stream);
                int bestFileId = PrimitiveArrays.ReadOneValue<int>(stream);
                int peakBoundCount = PrimitiveArrays.ReadOneValue<int>(stream);
                var peakBounds = new List<KeyValuePair<int, FileData>>();
                while (peakBounds.Count < peakBoundCount)
                {
                    var fileId = PrimitiveArrays.ReadOneValue<int>(stream);
                    var startTime = PrimitiveArrays.ReadOneValue<double>(stream);
                    var endTime = PrimitiveArrays.ReadOneValue<double>(stream);
                    var score = PrimitiveArrays.ReadOneValue<double>(stream);
                    byte bHasApexTime = PrimitiveArrays.ReadOneValue<byte>(stream);
                    double? apexTime;
                    if (bHasApexTime == 0)
                    {
                        apexTime = null;
                    }
                    else
                    {
                        apexTime = PrimitiveArrays.ReadOneValue<double>(stream);
                    }
                    peakBounds.Add(new KeyValuePair<int, FileData>(fileId, new FileData(apexTime, new ExplicitPeakBounds(startTime, endTime, score))));
                }
                return new ElibSpectrumInfo(peptideModSeq, charge, bestFileId, peakBounds);
            }
        }
        public class FileData
        {
            public FileData(double? apexTime, ExplicitPeakBounds peakBounds)
            {
                ApexTime = apexTime;
                PeakBounds = peakBounds;
            }
            public double? ApexTime { get; private set; }
            public ExplicitPeakBounds PeakBounds { get; private set; }
        }

        private class ElibSpectrumKey
        {
            public ElibSpectrumKey(int entryIndex, int fileId)
            {
                EntryIndex = entryIndex;
                FileId = fileId;
            }

            public int EntryIndex { get; private set; }
            public int FileId { get; private set; }
        }

        private class EncyclopeDiaScores
        {
            /// <summary>
            /// Mapping from (peptideModSeq,precursorCharge) to Map of filename to (qValue, posteriorErrorProbability).
            /// Only one file should have a qValue, but just in case there are multiple files, we store it in a dictionary.
            /// </summary>
            private Dictionary<Tuple<string, int>, IDictionary<string, Tuple<double, double>>> _dictionary
                = new Dictionary<Tuple<string, int>, IDictionary<string, Tuple<double, double>>>();

            public double? GetQValue(string peptideModSeq, int precursorCharge, string file)
            {
                IDictionary<string, Tuple<double, double>> values;
                if (!_dictionary.TryGetValue(Tuple.Create(peptideModSeq, precursorCharge), out values))
                {
                    return null;
                }
                Tuple<double, double> result;
                values.TryGetValue(file, out result);
                if (result == null)
                {
                    // If there is not an exact match of the file, just use the first qValue for that peptide&charge
                    result = values.Values.FirstOrDefault();
                }
                if (result == null)
                {
                    return null;
                }
                return result.Item1;
            }

            public void ReadScores(SQLiteConnection connection)
            {
                if (!SqliteOperations.TableExists(connection, @"peptidescores"))
                {
                    return;
                }
                using (var cmd = new SQLiteCommand(connection))
                {
                    cmd.CommandText =
                        @"select PeptideModSeq, PrecursorCharge, SourceFile, QValue, PosteriorErrorProbability from peptidescores";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var key = Tuple.Create(reader.GetString(0), Convert.ToInt32(reader.GetValue(1)));
                            var value = Tuple.Create(reader.GetDouble(3), reader.GetDouble(3));
                            string filename = reader.GetString(2);
                            IDictionary<string, Tuple<double, double>> values;
                            if (!_dictionary.TryGetValue(key, out values))
                            {
                                values = new Dictionary<string, Tuple<double, double>>();
                                _dictionary.Add(key, values);
                            }
                            values[filename] = value;
                        }
                    }
                }
            }
        }
    }
}
