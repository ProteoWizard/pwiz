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
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    [XmlRoot("bibliospec_lite_spec")]
    public sealed class BiblioSpecLiteSpec : LibrarySpec
    {
        public const string EXT = ".blib";
        public const string EXT_REDUNDANT = ".redundant.blib";

        public static string GetRedundantName(string libraryPath)
        {
            string libraryDir = Path.GetDirectoryName(libraryPath);
            string libraryBasename = Path.GetFileNameWithoutExtension(libraryPath);
            return Path.Combine(libraryDir ?? "", libraryBasename + EXT_REDUNDANT);            
        }

        private static readonly PeptideRankId[] RANK_IDS = new[] { PEP_RANK_COPIES, PEP_RANK_PICKED_INTENSITY };

        public BiblioSpecLiteSpec(string name, string path)
            : base(name, path)
        {
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
        private const int FORMAT_VERSION_CACHE = 3;

        public const string DEFAULT_AUTHORITY = "proteome.gs.washington.edu";

        public const string EXT_CACHE = ".slc";

        private PooledSqliteConnection _sqliteConnection;
        private PooledSqliteConnection _sqliteConnectionRedundant;
        private IStreamManager _streamManager;

        private BiblioLiteSourceInfo[] _librarySourceFiles;

        public static BiblioSpecLiteLibrary Load(BiblioSpecLiteSpec spec, ILoadMonitor loader)
        {
            var library = new BiblioSpecLiteLibrary(spec);
            if (library.Load(loader))
                return library;
            return null;
        }

        /// <summary>
        /// Controlled access to this <see cref="Immutable"/> class, which should be
        /// created through <see cref="Load(BiblioSpecLiteSpec,ILoadMonitor)"/>.
        /// </summary>
        private BiblioSpecLiteLibrary(LibrarySpec spec)
            : base(spec)
        {
            FilePath = spec.FilePath;

            string baseName = Path.GetFileNameWithoutExtension(FilePath);
            CachePath = Path.Combine(Path.GetDirectoryName(FilePath) ?? "", baseName + EXT_CACHE);
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

            _libraryEntries = libraryEntries;
            Array.Sort(_libraryEntries, CompareSpectrumInfo);

            // Create the SQLite connection without actually connecting
            _sqliteConnection = new PooledSqliteConnection(streamManager.ConnectionPool, FilePath);
            _streamManager = streamManager;
        }

        public override LibrarySpec CreateSpec(string path)
        {
            return new BiblioSpecLiteSpec(Name, path);
        }

        public override IList<RetentionTimeSource> ListRetentionTimeSources()
        {
            if (SchemaVersion < 1)
            {
                return base.ListRetentionTimeSources();
            }
            return _librarySourceFiles.Select(
                        biblioListSourceInfo => new RetentionTimeSource(biblioListSourceInfo.BaseName, Name)
                    ).ToArray();
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

        public override LibraryDetails LibraryDetails
        {
            get
            {
                var dataFiles = (from sourceFile in _librarySourceFiles
                                 let fileName = sourceFile.FileName
                                 where fileName != null
                                 select fileName).ToArray();

                LibraryDetails details = new LibraryDetails
                                             {
                                                 Format = "BiblioSpec",
                                                 Revision = Revision.ToString(CultureInfo.CurrentCulture),
                                                 Version = SchemaVersion.ToString(CultureInfo.CurrentCulture),
                                                 PeptideCount = SpectrumCount,
                                                 DataFiles = dataFiles
                                             };

                // In Schema Version 1, the RefSpectra table contains 
                // only the non-redundant, or the best spectrum for each peptide. 
                // The RetentionTimes table, however, stores all the spectra,
                // with the best spectra distinguished from the redundant ones by the 
                // value in the "bestSpectrum" column. 
                // If the total number of spectra in the library is more than the number
                // of  non-redundant spectra, we will provide that information to the user.
                int allSpecCount = RetentionTimesPsmCount();
                int numBestSpectra = _libraryEntries.Length; // number of spectra read from the RefSpectra table
                
                if (numBestSpectra < allSpecCount)
                {
                    details.TotalPsmCount = allSpecCount;
                }
                
                return details;
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
                    _sqliteConnectionRedundant = new PooledSqliteConnection(pool, FilePathRedundant);                
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
            minorVersion
        }

        private enum RefSpectra
        {
            id,
            libSpecNumber,
            peptideSeq,
            precursorMZ,
            precursorCharge,
            peptideModSeq,
            copies,
            numPeaks
        }

        private enum RefSpectraPeaks
        {
            RefSpectraID,
            peakMZ,
            peakIntensity
        }

        private enum RetentionTimes
        {
            RefSpectraID,
            RedundantRefSpectraID,
            SpectrumSourceID,
            retentionTime,
            bestSpectrum
        }

        private enum SpectrumSourceFiles
        {
            id,
            fileName,
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

            count
        }

        private enum SourceHeader
        {
            id,
            filename_length,

            count
        }

        private enum SpectrumCacheHeader
        {
            seq_key_hash,
            seq_key_length,
            charge,
            copies,
            num_peaks,
            id,
            seq_len,

            count
        }
        // ReSharper restore InconsistentNaming
        // ReSharper restore UnusedMember.Local

        private bool CreateCache(ILoadMonitor loader, ProgressStatus status, int percent)
        {
            var sm = loader.StreamManager;
            EnsureConnections(sm);
            using (SQLiteCommand select = new SQLiteCommand(_sqliteConnection.Connection))
            {
                int rows;
                string lsid;
                int dataRev, schemaVer;

                // First get header information
                select.CommandText = "SELECT * FROM [LibInfo]";
                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new IOException(string.Format("Failed reading library header for {0}.", FilePath));

                    rows = reader.GetInt32(LibInfo.numSpecs);

                    lsid = reader.GetString(LibInfo.libLSID);

                    dataRev = reader.GetInt32(LibInfo.majorVersion);
                    schemaVer = reader.GetInt32(LibInfo.minorVersion);
                }

                // Corrupted library without a valid row count, but try to compensate
                // by using count(*)
                if (rows == 0)
                {
                    select.CommandText = "SELECT count(*) FROM [RefSpectra]";
                    using (SQLiteDataReader reader = select.ExecuteReader())
                    {
                        if (!reader.Read())
                            throw new InvalidDataException(
                                string.Format("Unable to get a valid count of spectra in the library {0}", FilePath));
                        rows = reader.GetInt32(0);
                        if (rows == 0)
                            throw new InvalidDataException(string.Format("No spectra were found in the library {0}",
                                                                         FilePath));
                    }
                }

                ILookup<int, KeyValuePair<int, double>> retentionTimesBySpectraIdAndFileId = null;
                if (schemaVer >= 1)
                {
                    select.CommandText = "SELECT RefSpectraId, SpectrumSourceId, retentionTime FROM [RetentionTimes]";
                    var spectraIdFileIdTimes = new List<KeyValuePair<int, KeyValuePair<int, double>>>();
                    using (SQLiteDataReader reader = select.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            spectraIdFileIdTimes.Add(new KeyValuePair<int, KeyValuePair<int, double>>(
                                reader.GetInt32(0), 
                                new KeyValuePair<int, double>(reader.GetInt32(1), reader.GetDouble(2)))
                            );
                        }
                    }
                    retentionTimesBySpectraIdAndFileId = spectraIdFileIdTimes.ToLookup(kvp => kvp.Key, kvp => kvp.Value);
                }
                var setLibKeys = new Dictionary<LibKey, bool>(rows);
                var setSequences = new Dictionary<LibSeqKey, bool>(rows);
                var libraryEntries = new List<BiblioLiteSpectrumInfo>(rows);
                var librarySourceFiles = new List<BiblioLiteSourceInfo>();

                select.CommandText = "SELECT * FROM [RefSpectra]";
                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                    int iId = reader.GetOrdinal(RefSpectra.id);
                    int iSeq = reader.GetOrdinal(RefSpectra.peptideModSeq);
                    int iCharge = reader.GetOrdinal(RefSpectra.precursorCharge);
                    int iCopies = reader.GetOrdinal(RefSpectra.copies);
                    int iPeaks = reader.GetOrdinal(RefSpectra.numPeaks);

                    int rowsRead = 0;
                    while (reader.Read())
                    {
                        int percentComplete = rowsRead++*100/rows;
                        if (status.PercentComplete != percentComplete)
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

                        string sequence = reader.GetString(iSeq);
                        int charge = reader.GetInt16(iCharge);
                        short copies = reader.GetInt16(iCopies);
                        int numPeaks32 = reader.GetInt32(iPeaks);
                        if (numPeaks32 > ushort.MaxValue)
                        {
                            // CONSIDER: Throw an error instead?
                            continue;
                        }
                        ushort numPeaks = (ushort) numPeaks32;
                        int id = reader.GetInt32(iId);

                        // Avoid creating a cache which will just report it is corrupted.
                        // Older versions of BlibBuild used to create matches with charge 0.
                        if (charge == 0 || charge > TransitionGroup.MAX_PRECURSOR_CHARGE)
                            continue;
                        var retentionTimesByFileId = default(IndexedRetentionTimes);
                        if (retentionTimesBySpectraIdAndFileId != null)
                        {
                            retentionTimesByFileId = new IndexedRetentionTimes(retentionTimesBySpectraIdAndFileId[id]);
                        }
                        // These libraries should not have duplicates, but just in case.
                        // CONSIDER: Emit error about redundancy?
                        LibKey key = new LibKey(sequence, charge);
                        if (!setLibKeys.ContainsKey(key))
                        {
                            setLibKeys.Add(key, true);
                            libraryEntries.Add(new BiblioLiteSpectrumInfo(key, copies, numPeaks, id, retentionTimesByFileId));
                        }
                    }
                }

                libraryEntries.Sort(CompareSpectrumInfo);

                if (schemaVer > 0)
                {
                    select.CommandText = "SELECT * FROM [SpectrumSourceFiles]";
                    using (SQLiteDataReader reader = select.ExecuteReader())
                    {
                        int iId = reader.GetOrdinal(SpectrumSourceFiles.id);
                        int iFilename = reader.GetOrdinal(SpectrumSourceFiles.fileName);

                        while (reader.Read())
                        {
                            string filename = reader.GetString(iFilename);
                            int id = reader.GetInt32(iId);
                            librarySourceFiles.Add(new BiblioLiteSourceInfo(id, filename));
                        }
                    }

                }

                using (FileSaver fs = new FileSaver(CachePath, sm))
                using (Stream outStream = sm.CreateStream(fs.SafeName, FileMode.Create, true))
                {
                    foreach (var info in libraryEntries)
                    {
                        LibSeqKey seqKey = new LibSeqKey(info.Key);
                        if (setSequences.ContainsKey(seqKey))
                        {
                            outStream.Write(BitConverter.GetBytes(0), 0, sizeof(int));
                            outStream.Write(BitConverter.GetBytes(-1), 0, sizeof(int));
                        }
                        else
                        {
                            // If it is unique, it will need to be added at cache load time.
                            outStream.Write(BitConverter.GetBytes(seqKey.GetHashCode()), 0, sizeof(int));
                            outStream.Write(BitConverter.GetBytes(seqKey.Length), 0, sizeof(int));
                            setSequences.Add(seqKey, true);
                        }
                        outStream.Write(BitConverter.GetBytes(info.Key.Charge), 0, sizeof (int));
                        outStream.Write(BitConverter.GetBytes((int)info.Copies), 0, sizeof (int));
                        outStream.Write(BitConverter.GetBytes((int)info.NumPeaks), 0, sizeof (int));
                        outStream.Write(BitConverter.GetBytes(info.Id), 0, sizeof (int));
                        info.Key.WriteSequence(outStream);
                        info.RetentionTimesByFileId.Write(outStream);
                    }

                    long sourcePosition = 0;
                    if (librarySourceFiles.Count > 0)
                    {
                        // Write all source files
                        sourcePosition = outStream.Position;
                        foreach (var librarySourceFile in librarySourceFiles)
                        {
                            outStream.Write(BitConverter.GetBytes(librarySourceFile.Id), 0, sizeof(int));
                            outStream.Write(BitConverter.GetBytes(librarySourceFile.FilePath.Length), 0, sizeof(int));
                            var librarySourceFileNameBytes = Encoding.UTF8.GetBytes(librarySourceFile.FilePath);
                            outStream.Write(librarySourceFileNameBytes, 0, librarySourceFileNameBytes.Length);
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

                    sm.Finish(outStream);
                    fs.Commit();
                    sm.SetCache(FilePath, CachePath);
                }
            }

            loader.UpdateProgress(status.Complete());

            return true;
        }

        private bool Load(ILoadMonitor loader)
        {
            ProgressStatus status = new ProgressStatus("");
            loader.UpdateProgress(status);

            bool cached = loader.StreamManager.IsCached(FilePath, CachePath);
            if (Load(loader, status, cached))
                return true;

            // If loading from the cache failed, rebuild it.
            if (cached)
            {
                // Reset readStream so we don't read corrupt file.
                _sqliteConnection = null;
                if (Load(loader, status, false))
                    return true;
            }

            return false;
        }

        private bool Load(ILoadMonitor loader, ProgressStatus status, bool cached)
        {
            try
            {
                int loadPercent = 100;
                
                if (!cached)
                {

                    // Building the cache will take 95% of the load time.
                    loadPercent = 5;

                    status =
                        status.ChangeMessage(string.Format("Building binary cache for {0} library",
                                                           Path.GetFileName(FilePath)));
                    status = status.ChangePercentComplete(0);

                    loader.UpdateProgress(status);

                    if (!CreateCache(loader, status, 100 - loadPercent))
                        return false;
                }

                status = status.ChangeMessage(string.Format("Loading {0} library,", Path.GetFileName(FilePath)));
                loader.UpdateProgress(status);

                var sm = loader.StreamManager;

                using (Stream stream = sm.CreateStream(CachePath, FileMode.Open, true))
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

                    var setSequences = new Dictionary<LibSeqKey, bool>(numSpectra);
                    var libraryEntries = new BiblioLiteSpectrumInfo[numSpectra];
                    var librarySourceFiles = new List<BiblioLiteSourceInfo>();

                    long locationSources = BitConverter.ToInt64(libHeader,
                                                                ((int) LibHeaders.location_sources_lo)*sizeof (int));

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
                            byte[] filenameBytes = new byte[filenameLength];
                            ReadComplete(stream, filenameBytes, filenameLength);
                            librarySourceFiles.Add(new BiblioLiteSourceInfo(sourceId,
                                Encoding.UTF8.GetString(filenameBytes)));
                        }
                    }

                    _librarySourceFiles = librarySourceFiles.ToArray();

                    // Seek to beginning of spectrum headers, which is the beginning of the
                    // files, since spectra are not stored in the cache.
                    stream.Seek(0, SeekOrigin.Begin);

                    byte[] specSequence = new byte[1024];
                    byte[] specHeader = new byte[1024];

                    countHeader = (int) SpectrumCacheHeader.count*sizeof (int);

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
                        ReadComplete(stream, specHeader, countHeader);

                        int seqKeyHash = GetInt32(specHeader, ((int) SpectrumCacheHeader.seq_key_hash));
                        int seqKeyLength = GetInt32(specHeader, ((int) SpectrumCacheHeader.seq_key_length));
                        int charge = GetInt32(specHeader, ((int) SpectrumCacheHeader.charge));
                        if (charge == 0 || charge > TransitionGroup.MAX_PRECURSOR_CHARGE)
                            throw new InvalidDataException(string.Format("Invalid precursor charge {0} found. File may be corrupted.", charge));
                        int copies = GetInt32(specHeader, ((int) SpectrumCacheHeader.copies));
                        int numPeaks = GetInt32(specHeader, ((int) SpectrumCacheHeader.num_peaks));
                        int id = GetInt32(specHeader, ((int) SpectrumCacheHeader.id));
                        int seqLength = GetInt32(specHeader, (int) SpectrumCacheHeader.seq_len);
                    
                        // Read sequence information
                        ReadComplete(stream, specSequence, seqLength);

                        var retentionTimesByFileId = IndexedRetentionTimes.Read(stream);
                        LibKey key = new LibKey(specSequence, 0, seqLength, charge);
                        libraryEntries[i] = new BiblioLiteSpectrumInfo(key, (short)copies, (ushort)numPeaks, id, retentionTimesByFileId);
                        if (seqKeyLength > 0)
                        {
                            LibSeqKey seqKey = new LibSeqKey(key, seqKeyHash, seqKeyLength);
                            // These libraries should not have duplicates, but just in case.
                            // CONSIDER: Emit error about redundancy?
                            if (!setSequences.ContainsKey(seqKey))
                                setSequences.Add(seqKey, true);
                        }
                    }

                    // Checksum = checksum.ChecksumValue;
                    _libraryEntries = libraryEntries;
                    _setSequences = setSequences;

                    loader.UpdateProgress(status.Complete());

                    // Create a connection to the database from which the spectra will be read
                    EnsureConnections(sm);
                }

                return true;
            }
            catch (InvalidDataException x)
            {
                if (!cached)
                    loader.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            catch (IOException x)
            {
                if (!cached)
                    loader.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            catch (Exception x)
            {
                if (!cached)
                {
                    x = new Exception(string.Format("Failed loading library '{0}'.", FilePath), x);
                    loader.UpdateProgress(status.ChangeErrorException(x));
                }
                return false;
            }
        }

        public override SpectrumPeaksInfo LoadSpectrum(object spectrumKey)
        {
            var spectrumLiteKey = spectrumKey as SpectrumLiteKey;
            if (spectrumLiteKey != null)
                return new SpectrumPeaksInfo(ReadRedundantSpectrum(spectrumLiteKey.RedundantId));
                
            return base.LoadSpectrum(spectrumKey);
        }

        protected override SpectrumHeaderInfo CreateSpectrumHeaderInfo(BiblioLiteSpectrumInfo info)
        {
            return new BiblioSpecSpectrumHeaderInfo(Name, info.Copies);
        }

        protected override SpectrumPeaksInfo.MI[] ReadSpectrum(BiblioLiteSpectrumInfo info)
        {
            try
            {
                using (SQLiteCommand select = new SQLiteCommand(_sqliteConnection.Connection))
                {
                    select.CommandText = "SELECT * FROM [RefSpectraPeaks] WHERE [RefSpectraID] = ?";
                    select.Parameters.Add(new SQLiteParameter(DbType.UInt64, (long)info.Id));

                    using (SQLiteDataReader reader = select.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ushort numPeaks = info.NumPeaks;
                            return ReadPeaks(reader, numPeaks);
                        }
                    }
                }
            }
            catch (SQLiteException x)
            {                
                throw new IOException(string.Format("Unexpected SQLite failure reading {0}.", FilePath), x);
            }

            return null;
        }

        private SpectrumPeaksInfo.MI[] ReadRedundantSpectrum(int spectrumId)
        {
            if (_sqliteConnectionRedundant == null)
                throw new IOException(string.Format("The redundant library {0} does not exist.", FilePathRedundant));

            try
            {
                using (SQLiteCommand select = new SQLiteCommand(_sqliteConnectionRedundant.Connection))
                {
                    select.CommandText =
                        "SELECT * FROM " +
                        "[RefSpectra] as s INNER JOIN [RefSpectraPeaks] as p ON s.[id] = p.[RefSpectraID] " +
                        "WHERE s.[id] = ?";
                    select.Parameters.Add(new SQLiteParameter(DbType.UInt64, (long)spectrumId));

                    using (SQLiteDataReader reader = select.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int numPeaks = reader.GetInt32(RefSpectra.numPeaks);
                            if (numPeaks > ushort.MaxValue)
                                throw new IOException(string.Format("Spectrum peaks {0} excede the maximum allowed {1}.", numPeaks, ushort.MaxValue));
                            return ReadPeaks(reader, (ushort) numPeaks);
                        }
                    }
                }
            }
            catch (SQLiteException x)
            {
                throw new IOException(string.Format("Unexpected SQLite failure reading {0}.", FilePathRedundant), x);
            }

            return null;
        }

        private static SpectrumPeaksInfo.MI[] ReadPeaks(SQLiteDataReader reader, ushort numPeaks)
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

            return arrayMI;
        }

        public override bool TryGetRetentionTimes(LibKey key, string filePath, out double[] retentionTimes)
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

        private double[] ReadRetentionTimes(BiblioLiteSpectrumInfo info, BiblioLiteSourceInfo sourceInfo)
        {
            using (SQLiteCommand select = new SQLiteCommand(_sqliteConnection.Connection))
            {
                select.CommandText = "SELECT retentionTime FROM [RetentionTimes] " +
                    "WHERE [RefSpectraID] = ? AND [SpectrumSourceId] = ?";
                select.Parameters.Add(new SQLiteParameter(DbType.UInt64, (long)info.Id));
                select.Parameters.Add(new SQLiteParameter(DbType.UInt64, (long)sourceInfo.Id));

                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                    var listRetentionTimes = new List<double>();
                    while (reader.Read())
                        listRetentionTimes.Add(reader.GetDouble(0));
                    return listRetentionTimes.ToArray();
                }
            }
        }

        public override bool TryGetRetentionTimes(int fileIndex, out LibraryRetentionTimes retentionTimes)
        {
            return TryGetRetentionTimes(_librarySourceFiles[fileIndex].FilePath, out retentionTimes);
        }

        public override bool TryGetRetentionTimes(string filePath, out LibraryRetentionTimes retentionTimes)
        {
            int j = FindSource(filePath);
            if (j != -1)
            {
                var source = _librarySourceFiles[j];
                ILookup<string, double[]> timesLookup = _libraryEntries.ToLookup(
                    entry => entry.Key.Sequence, 
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
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                retentionTimes = new LibraryRetentionTimes(filePath, nonEmptyTimesDict);
                return true;
            }

            return base.TryGetRetentionTimes(filePath, out retentionTimes);
        }

        /// <summary>
        /// Reads all retention times for a specified source file into a dictionary by
        /// modified peptide sequence, with times stored in an array in ascending order.
        /// </summary>
        private IDictionary<string, double[]> ReadRetentionTimes(SQLiteConnection connection, BiblioLiteSourceInfo sourceInfo)
        {
            using (SQLiteCommand select = new SQLiteCommand(connection))
            {
                select.CommandText = "SELECT peptideModSeq, t.retentionTime " +
                    "FROM [RefSpectra] as s INNER JOIN [RetentionTimes] as t ON s.[id] = t.[RefSpectraID] " +
                    "WHERE t.[SpectrumSourceId] = ? " +
                    "ORDER BY peptideModSeq, t.retentionTime";
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

        private int FindSource(string filePath)
        {
            // First look for an exact path match
            int i = _librarySourceFiles.IndexOf(info => Equals(filePath, info.FilePath));
            if (i == -1)
            {
                try
                {
                    // Failing an exact path match, look for a basename match
                    string baseName = Path.GetFileNameWithoutExtension(filePath);
                    i = _librarySourceFiles.IndexOf(info => MeasuredResults.IsBaseNameMatch(baseName, info.BaseName));
                }
                catch (ArgumentException)
                {
                    // Handle: Illegal characters in path
                }
            }
            return i;
        }

        public override IEnumerable<SpectrumInfo> GetSpectra(LibKey key, IsotopeLabelType labelType, bool bestMatch)
        {
            if (bestMatch && SchemaVersion < 1)
            {
                // Retention time information is not available for schema (minor version) < 1
                return base.GetSpectra(key, labelType, true);
            }

            try
            {
                return GetRedundantSpectra(key, labelType, !bestMatch);
            }
            // In case there is no RetentionTimes table
            // CONSIDER: Could also be a failure to read the SQLite file
            catch (SQLiteException)
            {
                return base.GetSpectra(key, labelType, true);
            }
        }

        private IEnumerable<SpectrumInfo> GetRedundantSpectra(LibKey key, IsotopeLabelType labelType, bool getAll)
        {
            // No redundant spectra before schema version 1
            if (SchemaVersion == 0)
                return new SpectrumInfo[0];
            int i = FindEntry(key);
            if (i == -1)
                return new SpectrumInfo[0];

            var info = _libraryEntries[i];
            using (SQLiteCommand select = new SQLiteCommand(_sqliteConnection.Connection))
            {
                select.CommandText =
                    "SELECT * " +
                    "FROM [RetentionTimes] as t INNER JOIN [SpectrumSourceFiles] as s ON t.[SpectrumSourceID] = s.[id] " +
                    "WHERE t.[RefSpectraID] = ?";
                if (!getAll)
                    select.CommandText += " AND t.[bestSpectrum] = 1";

                select.Parameters.Add(new SQLiteParameter(DbType.UInt64, (long)info.Id));

                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                    int iFilePath = reader.GetOrdinal(SpectrumSourceFiles.fileName);
                    int iRedundantId = reader.GetOrdinal(RetentionTimes.RedundantRefSpectraID);
                    int iRetentionTime = reader.GetOrdinal(RetentionTimes.retentionTime);
                    int iBestSpectrum = reader.GetOrdinal(RetentionTimes.bestSpectrum);

                    var listSpectra = new List<SpectrumInfo>();
                    while (reader.Read())
                    {
                        string filePath = reader.GetString(iFilePath);
                        int redundantId = reader.GetInt32(iRedundantId);
                        double retentionTime = reader.GetDouble(iRetentionTime);
                        bool isBest = reader.GetInt16(iBestSpectrum) != 0;


                        // If this is not a reference(best) spectrum, the spectrumKey should be of the 
                        // type SpectrumLiteKey so that peaks for this spectrum are loaded from the
                        // redundant library. Otherwise, for best spectra, the key should simply be 
                        // the id of this spectrum in the cache.
                        // Look at the LoadSpectrum method to see how spectrumKey is used.
                        object spectrumKey = i;
                        if (!isBest)
                            spectrumKey = new SpectrumLiteKey(redundantId);
                        listSpectra.Add(new SpectrumInfo(this, labelType, filePath, retentionTime, isBest,
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

            using (SQLiteCommand select = new SQLiteCommand(_sqliteConnection.Connection))
            {
                select.CommandText = "SELECT count(*) FROM [RetentionTimes]";

                // SchemaVersion 1 does not have RetentionTimes table for redundant libraries. 
                // Querying a non-existent RetentionTimes table will throw an exception. 
                try
                {
                    using (SQLiteDataReader reader = select.ExecuteReader())
                    {
                        if (!reader.Read())
                            throw new InvalidDataException(
                                string.Format("Unable to get a valid count of all spectra in the library {0}", FilePath));
                        int rows = reader.GetInt32(0);
                        return rows;
                    }
                }
                catch (SQLiteException)
                {
                    return 0;
                }
            }
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        private BiblioSpecLiteLibrary()
        {
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

        private class PooledSqliteConnection : ConnectionId<SQLiteConnection>, IPooledStream
        {
            public PooledSqliteConnection(ConnectionPool connectionPool, string filePath) : base(connectionPool)
            {
                FilePath = filePath;
                FileTime = File.GetLastWriteTime(FilePath);
            }

            private string FilePath { get; set; }
            private DateTime FileTime { get; set; }

            protected override IDisposable Connect()
            {
                DbProviderFactory fact = new SQLiteFactory();
                SQLiteConnection conn = (SQLiteConnection) fact.CreateConnection();
                if (conn != null)
                {
                    conn.ConnectionString = string.Format("Data Source={0};Version=3", FilePath);
                    conn.Open();
                }
                return conn;
            }

            Stream IPooledStream.Stream
            {
                get { throw new InvalidOperationException(); }
            }

            public bool IsModified
            {
                get
                {
                    // If it is still in the pool, then it can't have been modified
                    return !IsOpen && !Equals(FileTime, File.GetLastWriteTime(FilePath));
                }
            }

            public bool IsOpen
            {
                get { return ConnectionPool.IsInPool(this); }
            }

            public void CloseStream()
            {
                Disconnect();
            }
        }

        private struct BiblioLiteSourceInfo
        {
            public BiblioLiteSourceInfo(int id, string filePath) : this()
            {
                Id = id;
                FilePath = filePath;
            }

            public int Id { get; private set; }
            public string FilePath { get; private set; }
            
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

            public string FileName
            {
                get
                {
                    try
                    {
                        return Path.GetFileName(FilePath);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }
        }
    }

    public sealed class SpectrumLiteKey
    {
        public SpectrumLiteKey(int redundantId)
        {
            RedundantId = redundantId;
        }

        public int RedundantId { get; private set; }
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

    public struct BiblioLiteSpectrumInfo : ICachedSpectrumInfo
    {
        private readonly LibKey _key;
        private readonly short _copies;
        private readonly ushort _numPeaks;
        private readonly int _id;
        private readonly IndexedRetentionTimes _retentionTimesByFileId;

        public BiblioLiteSpectrumInfo(LibKey key, short copies, ushort numPeaks, int id, IndexedRetentionTimes retentionTimesByFileId)
        {
            _key = key;
            _copies = copies;
            _numPeaks = numPeaks;
            _id = id;
            _retentionTimesByFileId = retentionTimesByFileId;
        }

        public LibKey Key { get { return _key;  } }
        public short Copies { get { return _copies; } }
        public ushort NumPeaks { get { return _numPeaks; } }
        public int Id { get { return _id; } }
        public IndexedRetentionTimes RetentionTimesByFileId { get { return _retentionTimesByFileId; } }
    }
}
