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
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    [XmlRoot("bibliospec_lite_spec")]
    public sealed class BiblioSpecLiteSpec : LibrarySpec
    {
        public const string EXT = ".blib";
        public const string EXT_REDUNDANT = ".redundant.blib";

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
    public sealed class BiblioSpecLiteLibrary : Library
    {
        private const int FORMAT_VERSION_CACHE = 2;

        public const string DEFAULT_AUTHORITY = "proteome.gs.washington.edu";

        public const string EXT_CACHE = ".slc";

        private BiblioLiteSpectrumInfo[] _libraryEntries;
        
        private Dictionary<LibSeqKey, bool> _setSequences;

        private PooledSqliteConnection _sqliteConnection;

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
        public BiblioSpecLiteLibrary(LibrarySpec spec, string lsid, int minorVer, int majorVer,
            BiblioLiteSpectrumInfo[] libraryEntries,IStreamManager streamManager)
            :this(spec)
        {
            Lsid = lsid;
            SetRevision(minorVer, majorVer);

            _libraryEntries = libraryEntries;
            Array.Sort(_libraryEntries, CompareSpectrumInfo);

            // Create the SQLite connection without actually connecting
            _sqliteConnection = new PooledSqliteConnection(streamManager.ConnectionPool, FilePath);
        }

        public SpectrumHeaderInfo CreateSpectrumHeaderInfo(string libraryName,
            int spectrumCount)
        {
            return new BiblioSpecSpectrumHeaderInfo(libraryName, spectrumCount);
        }

        public override LibrarySpec CreateSpec(string path)
        {
            return new BiblioSpecLiteSpec(Name, path);
        }

        public string Lsid { get; private set; }

        /// <summary>
        /// A monotonically increasing revision number associated with this library.
        /// </summary>
        public float Revision { get; private set; }

        /// <summary>
        /// Sets the revision float value, given integer minor and major versions.
        /// </summary>
        /// <param name="majorVer">Major version from database</param>
        /// <param name="minorVer">Minor version from database</param>
        private void SetRevision(int majorVer, int minorVer)
        {
            Revision = float.Parse(string.Format("{0}.{1}", majorVer, minorVer),
                CultureInfo.InvariantCulture);            
        }

        /// <summary>
        /// Path to the file on disk from which this library was loaded.  This value
        /// may be null, if the library was deserialized from XML and has not yet
        /// been loaded.
        /// </summary>
        public string FilePath { get; private set; }

        public string CachePath { get; set; }

        public override bool IsLoaded
        {
            get { return _libraryEntries != null; }
        }

        public override IPooledStream ReadStream
        {
            get { return _sqliteConnection; }
        }

        private SQLiteConnection CreateConnection(IStreamManager streamManager)
        {
            if (_sqliteConnection == null)
                _sqliteConnection = new PooledSqliteConnection(streamManager.ConnectionPool, FilePath);
            return _sqliteConnection.Connection;
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

        // Cache struct layouts
        private enum LibHeaders
        {
            lsid_byte_count,
            major_ver,
            minor_ver,
            format_version,
            num_spectra,
            location_headers_lo,
            location_headers_hi,

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

            using (SQLiteCommand select = new SQLiteCommand(CreateConnection(loader.StreamManager)))
            {
                int rows;
                string lsid;
                int majorVer, minorVer;

                // First get header information
                select.CommandText = "SELECT * FROM [LibInfo]";
                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new IOException(string.Format("Failed reading library header for {0}.", FilePath));

                    rows = reader.GetInt32(LibInfo.numSpecs);

                    lsid = reader.GetString(LibInfo.libLSID);

                    majorVer = reader.GetInt32(LibInfo.majorVersion);
                    minorVer = reader.GetInt32(LibInfo.minorVersion);
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

                var setSequences = new Dictionary<LibSeqKey, bool>(rows);
                var libraryEntries = new BiblioLiteSpectrumInfo[rows];
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
                        short numPeaks = reader.GetInt16(iPeaks);
                        int id = reader.GetInt32(iId);

                        // These libraries should not have duplicates, but just in case.
                        // CONSIDER: Emit error about redundancy?
                        LibKey key = new LibKey(sequence, charge);
                        libraryEntries[rowsRead - 1] = new BiblioLiteSpectrumInfo(key, copies, numPeaks, id);
                    }
                }

                Array.Sort(libraryEntries, CompareSpectrumInfo);

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
                        outStream.Write(BitConverter.GetBytes(info.Copies), 0, sizeof (int));
                        outStream.Write(BitConverter.GetBytes(info.NumPeaks), 0, sizeof (int));
                        outStream.Write(BitConverter.GetBytes(info.Id), 0, sizeof (int));
                        info.Key.WriteSequence(outStream);
                    }
                    byte[] lsidBytes = Encoding.UTF8.GetBytes(lsid);
                    outStream.Write(lsidBytes, 0, lsidBytes.Length);
                    outStream.Write(BitConverter.GetBytes(lsidBytes.Length), 0, sizeof(int));
                    outStream.Write(BitConverter.GetBytes(majorVer), 0, sizeof(int));
                    outStream.Write(BitConverter.GetBytes(minorVer), 0, sizeof(int));
                    outStream.Write(BitConverter.GetBytes(FORMAT_VERSION_CACHE), 0, sizeof(int));
                    outStream.Write(BitConverter.GetBytes(libraryEntries.Length), 0, sizeof (int));
                    outStream.Write(BitConverter.GetBytes((long) 0), 0, sizeof (long));

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
                    byte[] lsidBytes = new byte[countLsidBytes];
                    ReadComplete(stream, lsidBytes, countLsidBytes);
                    Lsid = Encoding.UTF8.GetString(lsidBytes);
                    int majorVer = GetInt32(libHeader, (int)LibHeaders.major_ver);
                    int minorVer = GetInt32(libHeader, (int) LibHeaders.minor_ver);
                    SetRevision(majorVer, minorVer);

                    int numSpectra = GetInt32(libHeader, (int) LibHeaders.num_spectra);

                    var setSequences = new Dictionary<LibSeqKey, bool>(numSpectra);
                    var libraryEntries = new BiblioLiteSpectrumInfo[numSpectra];

                    // Seek to beginning of spectrum headers
                    long locationHeaders = BitConverter.ToInt64(libHeader,
                                                                ((int) LibHeaders.location_headers_lo)*sizeof (int));
                    stream.Seek(locationHeaders, SeekOrigin.Begin);

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
                        if (charge == 0 || charge > 10)
                            throw new InvalidDataException("Invalid precursor charge found. File may be corrupted.");
                        int copies = GetInt32(specHeader, ((int) SpectrumCacheHeader.copies));
                        int numPeaks = GetInt32(specHeader, ((int) SpectrumCacheHeader.num_peaks));
                        int id = GetInt32(specHeader, ((int) SpectrumCacheHeader.id));
                        int seqLength = GetInt32(specHeader, (int) SpectrumCacheHeader.seq_len);
                    
                        // Read sequence information
                        ReadComplete(stream, specSequence, seqLength);

                        // These libraries should not have duplicates, but just in case.
                        // CONSIDER: Emit error about redundancy?
                        LibKey key = new LibKey(specSequence, 0, seqLength, charge);
                        libraryEntries[i] = new BiblioLiteSpectrumInfo(key, (short)copies, (short)numPeaks, id);
                        if (seqKeyLength > 0)
                        {
                            LibSeqKey seqKey = new LibSeqKey(key, seqKeyHash, seqKeyLength);
                            if (!setSequences.ContainsKey(seqKey))
                                setSequences.Add(seqKey, true);
                        }
                    }

                    // Checksum = checksum.ChecksumValue;
                    _libraryEntries = libraryEntries;
                    _setSequences = setSequences;

                    loader.UpdateProgress(status.Complete());

                    // Create a connection to the database from which the spectra will be read
                    CreateConnection(sm);
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

        private static int GetInt32(byte[] bytes, int index)
        {
            int ibyte = index * 4;
            return bytes[ibyte] | bytes[ibyte + 1] << 8 | bytes[ibyte + 2] << 16 | bytes[ibyte + 3] << 24;
        }

        public override bool Contains(LibKey key)
        {
            return FindEntry(key) != -1;
        }

        private int FindEntry(LibKey key)
        {
            if (_libraryEntries == null)
                return -1;
            return FindEntry(key, 0, _libraryEntries.Length - 1);
        }

        private int FindEntry(LibKey key, int left, int right)
        {
            // Binary search for the right key
            if (left > right)
                return -1;
            int mid = (left + right)/2;
            int compare = key.Compare(_libraryEntries[mid].Key);
            if (compare < 0)
                return FindEntry(key, left, mid - 1);
            else if (compare > 0)
                return FindEntry(key, mid + 1, right);
            else
            {
                return mid;
            }
        }

        public override bool ContainsAny(LibSeqKey key)
        {
            return (_setSequences != null && _setSequences.ContainsKey(key));
        }

        private static int CompareSpectrumInfo(BiblioLiteSpectrumInfo info1, BiblioLiteSpectrumInfo info2)
        {
            return info1.Key.Compare(info2.Key);
        }


        private static void ReadComplete(Stream stream, byte[] buffer, int size)
        {
            if (stream.Read(buffer, 0, size) != size)
                throw new InvalidDataException("Data truncation in library header. File may be corrupted.");
        }

        public override bool TryGetLibInfo(LibKey key, out SpectrumHeaderInfo libInfo)
        {
            int i = FindEntry(key);
            if (i != -1)
            {
                var entry = _libraryEntries[i];
                libInfo = CreateSpectrumHeaderInfo(Name, entry.Copies);
                return true;

            }
            libInfo = null;
            return false;
        }

        public override bool TryLoadSpectrum(LibKey key, out SpectrumPeaksInfo spectrum)
        {
            int i = FindEntry(key);
            if (i != -1)
            {
                spectrum = new SpectrumPeaksInfo(ReadSpectrum(_libraryEntries[i]));
                return true;
            }
            spectrum = null;
            return false;
        }

        public override int Count
        {
            get { return _libraryEntries == null ? 0 : _libraryEntries.Length; }
        }

        public override IEnumerable<LibKey> Keys
        {
            get
            {
                if (IsLoaded)
                    foreach (var entry in _libraryEntries)
                        yield return entry.Key;
            }
        }

        private SpectrumPeaksInfo.MI[] ReadSpectrum(BiblioLiteSpectrumInfo info)
        {
            using (SQLiteCommand select = new SQLiteCommand(_sqliteConnection.Connection))
            {
                select.CommandText = "SELECT * FROM [RefSpectraPeaks] WHERE [RefSpectraID] = ?";
                select.Parameters.Add(new SQLiteParameter(DbType.UInt64, info.Id));

                using (SQLiteDataReader reader = select.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int numPeaks = info.NumPeaks;
                        const int sizeMz = sizeof (double);
                        const int sizeInten = sizeof (float);

                        byte[] peakMzCompressed = reader.GetBytes(RefSpectraPeaks.peakMZ);
                        byte[] peakMz = peakMzCompressed.Uncompress(numPeaks*sizeMz);

                        byte[] peakIntensCompressed = reader.GetBytes(RefSpectraPeaks.peakIntensity);
                        byte[] peakIntens = peakIntensCompressed.Uncompress(numPeaks*sizeInten);

                        // Build the list
                        var arrayMI = new SpectrumPeaksInfo.MI[numPeaks];

                        for (int i = 0; i < numPeaks; i++)
                        {
                            arrayMI[i].Intensity = BitConverter.ToSingle(peakIntens, i*sizeInten);
                            arrayMI[i].Mz = BitConverter.ToDouble(peakMz, i*sizeMz);
                        }

                        return arrayMI;
                    }
                }
            }

            return null;
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
            Revision = reader.GetFloatAttribute(ATTR.revision, 0);
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
    }

    public struct BiblioLiteSpectrumInfo
    {
        private readonly LibKey _key;
        private readonly short _copies;
        private readonly short _numPeaks;
        private readonly int _id;

        public BiblioLiteSpectrumInfo(LibKey key, short copies, short numPeaks, int id)
        {
            _key = key;
            _copies = copies;
            _numPeaks = numPeaks;
            _id = id;
        }

        public LibKey Key { get { return _key;  } }
        public int Copies { get { return _copies; } }
        public int NumPeaks { get { return _numPeaks; } }
        public long Id { get { return _id; } }
    }
}
