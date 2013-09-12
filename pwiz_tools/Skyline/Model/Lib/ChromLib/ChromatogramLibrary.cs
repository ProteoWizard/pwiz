/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib.ChromLib.Data;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.ChromLib
{
    [XmlRoot("chromatogram_library")]
    public class ChromatogramLibrary : CachedLibrary<ChromLibSpectrumInfo>
    {
        private IStreamManager _streamManager;
        private PooledSessionFactory _pooledSessionFactory;
        public const string EXT_CACHE = ".clc";

        public ChromatogramLibrary(ChromatogramLibrarySpec chromatogramLibrarySpec) : base(chromatogramLibrarySpec)
        {
            LibrarySpec = chromatogramLibrarySpec;
            FilePath = LibrarySpec.FilePath;
            CachePath = Path.Combine(Path.GetDirectoryName(FilePath) ?? string.Empty,
                                     Path.GetFileNameWithoutExtension(FilePath) + EXT_CACHE);
        }

        public ChromatogramLibrary(ChromatogramLibrarySpec chromatogramLibrarySpec, IStreamManager streamManager)
            : this(chromatogramLibrarySpec)
        {
            _streamManager = streamManager;
            _pooledSessionFactory = new PooledSessionFactory(streamManager.ConnectionPool, typeof (ChromLibEntity),
                                                             chromatogramLibrarySpec.FilePath);
        }

        public ChromatogramLibrarySpec LibrarySpec { get; private set; }
        public string FilePath { get; private set; }

        public string PanoramaServer { get; private set; }
        public string SchemaVersion { get; private set; }
        public int LibraryRevision { get; private set; }

        protected override SpectrumHeaderInfo CreateSpectrumHeaderInfo(ChromLibSpectrumInfo info)
        {
            return new ChromLibSpectrumHeaderInfo(Name, info.PeakArea);
        }

        protected override SpectrumPeaksInfo.MI[] ReadSpectrum(ChromLibSpectrumInfo info)
        {
            return info.TransitionAreas.ToArray();
        }

        protected override LibraryChromGroup ReadChromatogram(ChromLibSpectrumInfo info)
        {
            using (var session = _pooledSessionFactory.Connection.OpenSession())
            {
                var precursor = session.Get<Precursor>(info.Id);
                if (null != precursor)
                {
                    var timeIntensities = precursor.ChromatogramData;
                    double height = 0;
                    var chromDatas = new List<LibraryChromGroup.ChromData>();
                    foreach (var transition in precursor.Transitions)
                    {
                        chromDatas.Add(new LibraryChromGroup.ChromData
                            {
                                Mz = transition.Mz,
                                Height = transition.Height,
                                Intensities = timeIntensities.Intensities[transition.ChromatogramIndex],
                                Charge = transition.Charge == 0 ? precursor.Charge : transition.Charge,
                                IonType = Helpers.ParseEnum(transition.FragmentType, IonType.y),
                                Ordinal = transition.FragmentOrdinal,
                                MassIndex = transition.MassIndex
                            });
                        height = Math.Max(height, transition.Height);
                    }
                    var precursorRetentionTime =
                        session.CreateCriteria<PrecursorRetentionTime>()
                               .Add(Restrictions.Eq("SampleFile", precursor.SampleFile))
                               .Add(Restrictions.Eq("Precursor", precursor))
                               .List<PrecursorRetentionTime>()
                               .FirstOrDefault();
                    double startTime = 0;
                    double endTime = 0;
                    double retentionTime = 0;
                    if (precursorRetentionTime != null)
                    {
                        startTime = precursorRetentionTime.StartTime;
                        endTime = precursorRetentionTime.EndTime;
                        retentionTime = precursorRetentionTime.RetentionTime;
                    }
                    return new LibraryChromGroup
                        {
                            RetentionTime = retentionTime,
                            StartTime = startTime,
                            EndTime = endTime,
                            Times = timeIntensities.Times,
                            ChromDatas = chromDatas,
                        };
                }
            }
            return null;
        }

        public override LibrarySpec CreateSpec(string path)
        {
            return new ChromatogramLibrarySpec(Name, path);
        }

        public override string SpecFilter
        {
            get { return TextUtil.FileDialogFilterAll("Chromatogram Libraries", ChromatogramLibrarySpec.EXT); }
        }

        public override IPooledStream ReadStream
        {
            get { return _pooledSessionFactory; }
        }

        public override bool IsSameLibrary(Library library)
        {
            var chromatogramLibrary = library as ChromatogramLibrary;
            return null != chromatogramLibrary && Equals(PanoramaServer, chromatogramLibrary.PanoramaServer);
        }

        public override int CompareRevisions(Library library)
        {
            return LibraryRevision.CompareTo(((ChromatogramLibrary) library).LibraryRevision);
        }

        public override LibraryDetails LibraryDetails
        {
            get { return new LibraryDetails(); }
        }

        public override int? FileCount
        {
            get { return 0; }
        }

        private bool Load(ILoadMonitor loader)
        {
            ProgressStatus status = new ProgressStatus(string.Format("Loading {0}", Name));
            loader.UpdateProgress(status);
            if (LoadFromCache(loader, status))
            {
                loader.UpdateProgress(status.Complete());
                return true;
            }
            if (LoadLibraryFromDatabase(loader))
            {
                using (var fileSaver = new FileSaver(CachePath, loader.StreamManager))
                {
                    using (var stream = loader.StreamManager.CreateStream(fileSaver.SafeName, FileMode.Create, true))
                    {
                        var serializer = new Serializer(this, stream);
                        serializer.Write();
                        loader.StreamManager.Finish(stream);
                        fileSaver.Commit();
                        loader.StreamManager.SetCache(FilePath, CachePath);
                    }
                }
                loader.UpdateProgress(status.Complete());
                return true;
            }
            return false;
        }

        private bool LoadLibraryFromDatabase(ILoadMonitor loader)
        {
            try
            {
                var status = new ProgressStatus(string.Format("Reading precursors from {0}", Name));
                loader.UpdateProgress(status);
                //                _pooledSessionFactory = new PooledSessionFactory(loader.StreamManager.ConnectionPool,
//                                                                 typeof (ChromLibEntity), FilePath);
                using (var session = _pooledSessionFactory.Connection.OpenSession())
                {
                    var libInfo =
                        session.CreateSQLQuery("SELECT PanoramaServer, LibraryRevision, SchemaVersion FROM LibInfo")
                               .UniqueResult<object[]>();
                    PanoramaServer = Convert.ToString(libInfo[0]);
                    LibraryRevision = Convert.ToInt32(libInfo[1]);
                    SchemaVersion = Convert.ToString(libInfo[2]);

                    var precursorQuery =
                        session.CreateQuery("SELECT P.Id, P.ModifiedSequence, P.Charge, P.TotalArea FROM " + typeof (Precursor) +
                                            " P");
                    var allTransitionAreas = ReadAllTransitionAreas(session);
                    var spectrumInfos = new List<ChromLibSpectrumInfo>();
                    foreach (object[] row in precursorQuery.List<object[]>())
                    {
                        var id = (int) row[0];
                        if (row[1] == null || row[2] == null)
                        {
                            continue; // Throw an error?
                        }
                        var modifiedSequence = (string) row[1];
                        int charge = (int) row[2];
                        double totalArea = Convert.ToDouble(row[3]);
                        var libKey = new LibKey(SequenceMassCalc.NormalizeModifiedSequence(modifiedSequence), charge);
                        IList<SpectrumPeaksInfo.MI> transitionAreas;
                        allTransitionAreas.TryGetValue(id, out transitionAreas);
                        spectrumInfos.Add(new ChromLibSpectrumInfo(libKey, id, totalArea, transitionAreas));
                    }
                    SetLibraryEntries(spectrumInfos);
                    loader.UpdateProgress(status.Complete());
                    return true;
                }
            }
            catch (Exception e)
            {
                Trace.TraceWarning("Error loading chromatogram library:{0}", e);
                return false;
            }
        }

        /// <summary>
        /// Returns a mapping from PrecursorId to list of mz/intensities.
        /// </summary>
        private IDictionary<int, IList<SpectrumPeaksInfo.MI>> ReadAllTransitionAreas(ISession session)
        {
            var allPeakAreas = new Dictionary<int, IList<SpectrumPeaksInfo.MI>>();
            var query =
                session.CreateQuery("SELECT T.Precursor.Id as First, T.Mz, T.Area FROM " + typeof(Data.Transition) + " T");
            var rows = query.List<object[]>();
            var rowsLookup = rows.ToLookup(row => (int) (row[0]));
            foreach (var grouping in rowsLookup)
            {
                var mis = ImmutableList.ValueOf(grouping.Select(row 
                    => new SpectrumPeaksInfo.MI{Mz = Convert.ToDouble(row[1]), Intensity = Convert.ToSingle(row[2])}));
                allPeakAreas.Add(grouping.Key, mis);
            }
            return allPeakAreas;
        }

        private void SetLibraryEntries(IEnumerable<ChromLibSpectrumInfo> spectrumInfos)
        {
            var libraryEntries = spectrumInfos.ToArray();
            Array.Sort(libraryEntries);
            _libraryEntries = libraryEntries;
            _setSequences = _libraryEntries
                        .Select(info => new LibSeqKey(info.Key))
                        .Distinct()
                        .ToDictionary(key => key, key => true);
        }

        private bool LoadFromCache(ILoadMonitor loadMonitor, ProgressStatus status)
        {
            if (!loadMonitor.StreamManager.IsCached(FilePath, CachePath))
            {
                return false;
            }
            try
            {
                using (var stream = loadMonitor.StreamManager.CreateStream(CachePath, FileMode.Open, true))
                {
                    var serializer = new Serializer(this, stream);
                    serializer.Read();
                    return true;
                }
            }
            catch (Exception exception)
            {
                Trace.TraceWarning("Exception reading cache:{0}", exception);
                return false;
            }
        }

        public static ChromatogramLibrary LoadFromDatabase(ChromatogramLibrarySpec chromatogramLibrarySpec, ILoadMonitor loadMonitor)
        {
            var library = new ChromatogramLibrary(chromatogramLibrarySpec, loadMonitor.StreamManager);
            if (library.Load(loadMonitor))
                return library;
            return null;
        }
        private ChromatogramLibrary()
        {
        }
        private enum ATTR
        {
            panorama_server,
            revision
        }
        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            PanoramaServer = reader.GetAttribute(ATTR.panorama_server);
            LibraryRevision = reader.GetIntAttribute(ATTR.revision);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.panorama_server, PanoramaServer);
            writer.WriteAttribute(ATTR.revision, LibraryRevision);
        }

        public static ChromatogramLibrary Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ChromatogramLibrary());
        }

        protected bool Equals(ChromatogramLibrary other)
        {
            return base.Equals(other) && string.Equals(FilePath, other.FilePath) && string.Equals(PanoramaServer, other.PanoramaServer) && LibraryRevision == other.LibraryRevision;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ChromatogramLibrary) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode*397) ^ (FilePath != null ? FilePath.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (PanoramaServer != null ? PanoramaServer.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ LibraryRevision;
                return hashCode;
            }
        }

        private class Serializer
        {
            private const int CURRENT_VERSION = 2;
            private const int MIN_READABLE_VERSION = 2;

            private readonly ChromatogramLibrary _library;
            private readonly Stream _stream;
            private long _locationEntries;
            private long _locationHeader;
            
            public Serializer(ChromatogramLibrary library, Stream stream)
            {
                _library = library;
                _stream = stream;
            }

            public void Read()
            {
                ReadHeader();
                ReadEntries();
            }

            public void Write()
            {
                WriteEntries();
                WriteHeader();
            }

            private void WriteEntries()
            {
                _locationEntries = _stream.Position;
                PrimitiveArrays.WriteOneValue(_stream, _library._libraryEntries.Length);
                foreach (var entry in _library._libraryEntries)
                {
                    entry.Write(_stream);
                }
            }
            private void ReadEntries()
            {
                _stream.Seek(_locationEntries, SeekOrigin.Begin);
                int entryCount = PrimitiveArrays.ReadOneValue<int>(_stream);
                var entries = new ChromLibSpectrumInfo[entryCount];
                for (int i = 0; i < entryCount; i++)
                {
                    entries[i] = ChromLibSpectrumInfo.Read(_stream);
                }
                _library.SetLibraryEntries(entries);
            }
            private void WriteHeader()
            {
                _locationHeader = _stream.Position;
                PrimitiveArrays.WriteOneValue(_stream, CURRENT_VERSION);
                PrimitiveArrays.WriteOneValue(_stream, _locationEntries);
                WriteString(_stream, _library.PanoramaServer);
                PrimitiveArrays.WriteOneValue(_stream, _library.LibraryRevision);
                WriteString(_stream, _library.SchemaVersion);
                PrimitiveArrays.WriteOneValue(_stream, _locationEntries);
                PrimitiveArrays.WriteOneValue(_stream, _locationHeader);
            }

            private void ReadHeader()
            {
                _stream.Seek(-sizeof (long), SeekOrigin.End);
                _locationHeader = PrimitiveArrays.ReadOneValue<long>(_stream);
                _stream.Seek(_locationHeader, SeekOrigin.Begin);
                int version = PrimitiveArrays.ReadOneValue<int>(_stream);
                if (version > CURRENT_VERSION || version < MIN_READABLE_VERSION)
                {
                    throw new InvalidDataException(string.Format("Unsupported file version {0}", version));
                }
                _locationEntries = PrimitiveArrays.ReadOneValue<long>(_stream);
                _library.PanoramaServer = ReadString(_stream);
                _library.LibraryRevision = PrimitiveArrays.ReadOneValue<int>(_stream);
                _library.SchemaVersion = ReadString(_stream);
                _locationEntries = PrimitiveArrays.ReadOneValue<long>(_stream);
            }

            private static void WriteString(Stream stream, string str)
            {
                var bytes = Encoding.UTF8.GetBytes(str);
                PrimitiveArrays.WriteOneValue(stream, bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
            }

            private static string ReadString(Stream stream)
            {
                int byteLength = PrimitiveArrays.ReadOneValue<int>(stream);
                var bytes = new byte[byteLength];
                stream.Read(bytes, 0, bytes.Length);
                return Encoding.UTF8.GetString(bytes);
            }
        }
    }
}
