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
using System.Xml;
using System.Xml.Serialization;
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
        public const string DEFAULT_AUTHORITY = "proteome.gs.washington.edu";

        private IDictionary<LibKey, BiblioLiteSpectrumInfo> _dictLibrary;
        private HashSet<LibSeqKey> _setSequences;

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
        }

        /// <summary>
        /// Constructs library from its component parts.  For use with <see cref="BlibDb"/>.
        /// </summary>
        public BiblioSpecLiteLibrary(LibrarySpec spec, string lsid, int minorVer, int majorVer,
            IDictionary<LibKey, BiblioLiteSpectrumInfo> dictLibrary, IStreamManager streamManager)
            :this(spec)
        {
            Lsid = lsid;
            SetRevision(minorVer, majorVer);

            _dictLibrary = dictLibrary;
            // Create the SQLite connection without actually connecting
            _sqliteConnection = new PooledSqliteConnection(streamManager.ConnectionPool, FilePath);
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

        public override bool IsLoaded
        {
            get { return _dictLibrary != null; }
        }

        public override IPooledStream ReadStream
        {
            get { return _sqliteConnection; }
        }

        private SQLiteConnection CreateConnection(IStreamManager streamManager)
        {
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
        // ReSharper restore InconsistentNaming
        // ReSharper restore UnusedMember.Local

        private bool Load(ILoadMonitor loader)
        {
            ProgressStatus status = new ProgressStatus(string.Format("Loading {0} library", Path.GetFileName(FilePath)));
            loader.UpdateProgress(status);
            try
            {
                using (SQLiteCommand select = new SQLiteCommand(CreateConnection(loader.StreamManager)))
                {
                    int rows;

                    // First get header information
                    select.CommandText = "SELECT * FROM [LibInfo]";
                    using (SQLiteDataReader reader = select.ExecuteReader())
                    {
                        if (!reader.Read())
                            throw new IOException(string.Format("Failed reading library header for {0}.", FilePath));

                        rows = reader.GetInt32(LibInfo.numSpecs);

                        Lsid = reader.GetString(LibInfo.libLSID);

                        SetRevision(reader.GetInt32(LibInfo.majorVersion), reader.GetInt32(LibInfo.minorVersion));
                    }

                    // Corrupted library without a valid row count, but try to compensate
                    // by using count(*)
                    if (rows == 0)
                    {
                        select.CommandText = "SELECT count(*) FROM [RefSpectra]";
                        using (SQLiteDataReader reader = select.ExecuteReader())
                        {
                            if (!reader.Read())
                                throw new InvalidDataException(string.Format("Unable to get a valid count of spectra in the library {0}", FilePath));
                            rows = reader.GetInt32(0);
                            if (rows == 0)
                                throw new InvalidDataException(string.Format("No spectra were found in the library {0}", FilePath));
                        }
                    }

                    // Then read in spectrum headers
                    _dictLibrary = new Dictionary<LibKey, BiblioLiteSpectrumInfo>(rows);
                    _setSequences = new HashSet<LibSeqKey>();

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
                            int percent = rowsRead++*100/rows;
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

                            string sequence = reader.GetString(iSeq);
                            int charge = reader.GetInt16(iCharge);
                            short copies = reader.GetInt16(iCopies);
                            short numPeaks = reader.GetInt16(iPeaks);
                            int id = reader.GetInt32(iId);

                            // These libraries should not have duplicates, but just in case.
                            // CONSIDER: Emit error about redundancy?
                            var key = new LibKey(sequence, charge);
                            if (!_dictLibrary.ContainsKey(key))
                                _dictLibrary.Add(key, new BiblioLiteSpectrumInfo(copies, numPeaks, id));
                            _setSequences.Add(new LibSeqKey(key));
                        }
                    }
                }
            }
            catch (InvalidDataException x)
            {
                loader.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            catch (IOException x)
            {
                loader.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            catch (Exception x)
            {
                x = new Exception(string.Format("Failed loading library '{0}'.", FilePath), x);
                loader.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }


            loader.UpdateProgress(status.Complete());
            return true;
        }

        public override bool Contains(LibKey key)
        {
            return (_dictLibrary != null && _dictLibrary.ContainsKey(key));
        }

        public override bool ContainsAny(LibSeqKey key)
        {
            return (_setSequences != null && _setSequences.Contains(key));
        }

        public override bool TryGetLibInfo(LibKey key, out SpectrumHeaderInfo libInfo)
        {
            BiblioLiteSpectrumInfo info;
            if (_dictLibrary != null && _dictLibrary.TryGetValue(key, out info))
            {
                libInfo = new BiblioSpecSpectrumHeaderInfo(Name, info.Copies);
                return true;
            }
            libInfo = null;
            return false;
        }

        public override bool TryLoadSpectrum(LibKey key, out SpectrumPeaksInfo spectrum)
        {
            BiblioLiteSpectrumInfo info;
            if (_dictLibrary != null && _dictLibrary.TryGetValue(key, out info))
            {
                spectrum = new SpectrumPeaksInfo(ReadSpectrum(info));
                return true;
            }

            spectrum = null;
            return false;
        }

        public override int Count
        {
            get { return _dictLibrary == null ? 0 : _dictLibrary.Count; }
        }

        public override IEnumerable<LibKey> Keys
        {
            get
            {
                if (_dictLibrary != null)
                    foreach (var key in _dictLibrary.Keys)
                        yield return key;
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
                conn.ConnectionString = string.Format("Data Source={0};Version=3", FilePath);
                conn.Open();
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
        private readonly short _copies;
        private readonly short _numPeaks;
        private readonly int _id;

        public BiblioLiteSpectrumInfo(short copies, short numPeaks, int id)
        {
            _copies = copies;
            _numPeaks = numPeaks;
            _id = id;
        }

        public int Copies { get { return _copies; } }
        public int NumPeaks { get { return _numPeaks; } }
        public long Id { get { return _id; } }
    }
}
