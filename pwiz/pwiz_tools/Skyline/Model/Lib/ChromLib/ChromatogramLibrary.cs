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
using NHibernate.Exceptions;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib.ChromLib.Data;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.ChromLib
{
    [XmlRoot("chromatogram_library")]
    public class ChromatogramLibrary : CachedLibrary<ChromLibSpectrumInfo>
    {
        private IStreamManager _streamManager;
        private PooledSessionFactory _pooledSessionFactory;
        public const string EXT_CACHE = ".clc"; // Not L10N

        private ChromatogramLibrarySourceInfo[] _librarySourceFiles;
        private ChromatogramLibraryIrt[] _libraryIrts;

        public static string FILTER_CLIB
        {
            get { return TextUtil.FileDialogFilter(Resources.ChromatogramLibrary_FILTER_CLIB_Chromatogram_Libraries, ChromatogramLibrarySpec.EXT); }
        }

        public ChromatogramLibrary(ChromatogramLibrarySpec chromatogramLibrarySpec) : base(chromatogramLibrarySpec)
        {
            LibrarySpec = chromatogramLibrarySpec;
            FilePath = LibrarySpec.FilePath;
            CachePath = Path.Combine(Path.GetDirectoryName(FilePath) ?? string.Empty,
                                     Path.GetFileNameWithoutExtension(FilePath) + EXT_CACHE);
            _libraryIrts = new ChromatogramLibraryIrt[0];
            _librarySourceFiles = new ChromatogramLibrarySourceInfo[0];
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
                               .Add(Restrictions.Eq("SampleFile", precursor.SampleFile)) // Not L10N
                               .Add(Restrictions.Eq("Precursor", precursor)) // Not L10N
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
            get { return TextUtil.FileDialogFiltersAll(FILTER_CLIB); }
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
            get { return _librarySourceFiles.Count(); }
        }

        private bool Load(ILoadMonitor loader)
        {
            ProgressStatus status = new ProgressStatus(string.Format(Resources.ChromatogramLibrary_Load_Loading__0_, Name));
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
                var status = new ProgressStatus(string.Format(Resources.ChromatogramLibrary_LoadLibraryFromDatabase_Reading_precursors_from__0_, Name));
                loader.UpdateProgress(status);
                //                _pooledSessionFactory = new PooledSessionFactory(loader.StreamManager.ConnectionPool,
//                                                                 typeof (ChromLibEntity), FilePath);
                using (var session = _pooledSessionFactory.Connection.OpenSession())
                {
                    var libInfo =
                        session.CreateSQLQuery("SELECT PanoramaServer, LibraryRevision, SchemaVersion FROM LibInfo") // Not L10N
                               .UniqueResult<object[]>();
                    PanoramaServer = Convert.ToString(libInfo[0]);
                    LibraryRevision = Convert.ToInt32(libInfo[1]);
                    SchemaVersion = Convert.ToString(libInfo[2]);

                    try
                    {
                        var irtQuery = session.CreateQuery("SELECT PeptideModSeq, Irt, TimeSource FROM IrtLibrary"); // Not L10N
                        _libraryIrts = irtQuery.List<object[]>().Select(
                            irt => new ChromatogramLibraryIrt((string) irt[0], (TimeSource) irt[2], Convert.ToDouble(irt[1]))
                            ).ToArray();
                    }
                    catch (GenericADOException)
                    {
                        // IrtLibrary table probably doesn't exist
                    }

                    var rtQuery = session.CreateQuery("SELECT Precursor.Id, SampleFile.Id, RetentionTime FROM PrecursorRetentionTime"); // Not L10N
                    var rtDictionary = new Dictionary<int, List<KeyValuePair<int, double>>>(); // PrecursorId -> [SampleFileId -> RetentionTime]
                    foreach (object[] row in rtQuery.List<object[]>())
                    {
                        var precursorId = (int) row[0];
                        var sampleFileId = (int) row[1];
                        var rt = Convert.ToDouble(row[2]);
                        if (!rtDictionary.ContainsKey(precursorId))
                        {
                            rtDictionary.Add(precursorId, new List<KeyValuePair<int, double>>());
                        }
                        rtDictionary[precursorId].Add(new KeyValuePair<int, double>(sampleFileId, rt));
                    }

                    var precursorQuery =
                        session.CreateQuery("SELECT P.Id, P.ModifiedSequence, P.Charge, P.TotalArea FROM " + typeof (Precursor) + // Not L10N
                                            " P"); // Not L10N
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
                        List<KeyValuePair<int, double>> retentionTimes;
                        var indexedRetentionTimes = new IndexedRetentionTimes();
                        if (rtDictionary.TryGetValue(id, out retentionTimes))
                        {
                            indexedRetentionTimes = new IndexedRetentionTimes(retentionTimes);
                        }
                        string modSeqNormal;
                        try
                        {
                            modSeqNormal = SequenceMassCalc.NormalizeModifiedSequence(modifiedSequence);
                        }
                        catch (ArgumentException)
                        {
                            continue;
                        }

                        var libKey = new LibKey(modSeqNormal, charge);
                        IList<SpectrumPeaksInfo.MI> transitionAreas;
                        allTransitionAreas.TryGetValue(id, out transitionAreas);
                        spectrumInfos.Add(new ChromLibSpectrumInfo(libKey, id, totalArea, indexedRetentionTimes, transitionAreas));
                    }
                    SetLibraryEntries(spectrumInfos);

                    var sampleFileQuery =
                        session.CreateQuery("SELECT Id, FilePath, SampleName, AcquiredTime, ModifiedTime, InstrumentIonizationType, " + // Not L10N
                                            "InstrumentAnalyzer, InstrumentDetector FROM SampleFile"); // Not L10N
                    var sampleFiles = new List<ChromatogramLibrarySourceInfo>();
                    foreach (object[] row in sampleFileQuery.List<object[]>())
                    {
                        var id = (int) row[0];
                        if (row[1] == null || row[2] == null)
                        {
                            continue; // Throw an error?
                        }
                        var filePath = row[1].ToString();
                        var sampleName = row[2].ToString();
                        var acquiredTime = row[3] != null ? row[3].ToString() : string.Empty;
                        var modifiedTime = row[4] != null ? row[4].ToString() : string.Empty;
                        var instrumentIonizationType = row[5] != null ? row[5].ToString() : string.Empty;
                        var instrumentAnalyzer = row[6] != null ? row[6].ToString() : string.Empty;
                        var instrumentDetector = row[7] != null ? row[7].ToString() : string.Empty;
                        sampleFiles.Add(new ChromatogramLibrarySourceInfo(id, filePath, sampleName, acquiredTime, modifiedTime, instrumentIonizationType,
                                                                          instrumentAnalyzer, instrumentDetector));
                    }
                    _librarySourceFiles = sampleFiles.ToArray();

                    loader.UpdateProgress(status.Complete());
                    return true;
                }
            }
            catch (Exception e)
            {
                Trace.TraceWarning(Resources.ChromatogramLibrary_LoadLibraryFromDatabase_Error_loading_chromatogram_library__0_, e);
                return false;
            }
        }

        public override bool TryGetRetentionTimes(LibKey key, MsDataFileUri filePath, out double[] retentionTimes)
        {
            int i = FindEntry(key);
            int j = _librarySourceFiles.IndexOf(info => Equals(filePath.ToString(), info.FilePath));
            if (i != -1 && j != -1)
            {
                retentionTimes = _libraryEntries[i].RetentionTimesByFileId.GetTimes(_librarySourceFiles[j].Id);
                return true;
            }

            return base.TryGetRetentionTimes(key, filePath, out retentionTimes);
        }

        public override bool TryGetRetentionTimes(MsDataFileUri filePath, out LibraryRetentionTimes retentionTimes)
        {
            int j = _librarySourceFiles.IndexOf(info => Equals(filePath.ToString(), info.FilePath));
            if (j != -1)
            {
                var source = _librarySourceFiles[j];
                ILookup<string, double[]> timesLookup = _libraryEntries.ToLookup(
                    entry => entry.Key.Sequence,
                    entry => entry.RetentionTimesByFileId.GetTimes(source.Id));
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
                    .ToDictionary(kvp => kvp.Key, kvp => new Tuple<TimeSource, double[]>(TimeSource.peak, kvp.Value));

                retentionTimes = new LibraryRetentionTimes(filePath.ToString(), nonEmptyTimesDict);
                return true;
            }

            return base.TryGetRetentionTimes(filePath, out retentionTimes);
        }

        public override bool TryGetRetentionTimes(int fileIndex, out LibraryRetentionTimes retentionTimes)
        {
            return TryGetRetentionTimes(MsDataFileUri.Parse(_librarySourceFiles[fileIndex].FilePath), out retentionTimes);
        }

        public override bool TryGetIrts(out LibraryRetentionTimes retentionTimes)
        {
            if (_libraryIrts == null || !_libraryIrts.Any())
            {
                retentionTimes = null;
                return false;
            }

            var irtDictionary = new Dictionary<string, Tuple<TimeSource, double[]>>();
            foreach (var irt in _libraryIrts)
            {
                if (!irtDictionary.ContainsKey(irt.Sequence))
                {
                    irtDictionary[irt.Sequence] = new Tuple<TimeSource, double[]>(irt.TimeSource, new []{irt.Irt});
                }
            }
            retentionTimes = new LibraryRetentionTimes(null, irtDictionary);
            return true;
        }

        /// <summary>
        /// Returns a mapping from PrecursorId to list of mz/intensities.
        /// </summary>
        private IDictionary<int, IList<SpectrumPeaksInfo.MI>> ReadAllTransitionAreas(ISession session)
        {
            var allPeakAreas = new Dictionary<int, IList<SpectrumPeaksInfo.MI>>();
            var query =
                session.CreateQuery("SELECT T.Precursor.Id as First, T.Mz, T.Area FROM " + typeof(Data.Transition) + " T"); // Not L10N
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
                Trace.TraceWarning(Resources.ChromatogramLibrary_LoadFromCache_Exception_reading_cache__0_, exception);
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

        private struct ChromatogramLibrarySourceInfo
        {
            public ChromatogramLibrarySourceInfo(int id, string filePath, string sampleName, string acquiredTime, string modifiedTime,
                string instrumentIonizationType, string instrumentAnalyzer, string instrumentDetector)
                : this()
            {
                Id = id;
                FilePath = filePath;
                SampleName = sampleName;
                AcquiredTime = acquiredTime;
                ModifiedTime = modifiedTime;
                InstrumentIonizationType = instrumentIonizationType;
                InstrumentAnalyzer = instrumentAnalyzer;
                InstrumentDetector = instrumentDetector;
            }

            public int Id { get; private set; }
            public string FilePath { get; private set; }
            public string SampleName { get; private set; }
            public string AcquiredTime { get; private set; }
            public string ModifiedTime { get; private set; }
            public string InstrumentIonizationType { get; private set; }
            public string InstrumentAnalyzer { get; private set; }
            public string InstrumentDetector { get; private set; }

            public void Write(Stream stream)
            {
                PrimitiveArrays.WriteOneValue(stream, Id);
                WriteString(stream, FilePath);
                WriteString(stream, SampleName);
                WriteString(stream, AcquiredTime);
                WriteString(stream, ModifiedTime);
                WriteString(stream, InstrumentIonizationType);
                WriteString(stream, InstrumentAnalyzer);
                WriteString(stream, InstrumentDetector);
            }

            public static ChromatogramLibrarySourceInfo Read(Stream stream)
            {
                int id = PrimitiveArrays.ReadOneValue<int>(stream);
                string filePath = ReadString(stream);
                string sampleName = ReadString(stream);
                string acquiredTime = ReadString(stream);
                string modifiedTime = ReadString(stream);
                string instrumentIonizationType = ReadString(stream);
                string instrumentAnalyzer = ReadString(stream);
                string instrumentDetector = ReadString(stream);
                return new ChromatogramLibrarySourceInfo(id, filePath, sampleName, acquiredTime, modifiedTime, instrumentIonizationType,
                                                         instrumentAnalyzer, instrumentDetector);
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

        private struct ChromatogramLibraryIrt
        {
            public ChromatogramLibraryIrt(string seq, TimeSource timeSource, double irt)
                : this()
            {
                Sequence = seq;
                TimeSource = timeSource;
                Irt = irt;
            }

            public string Sequence { get; private set; }
            public TimeSource TimeSource { get; private set; }
            public double Irt { get; private set; }

            public void Write(Stream stream)
            {
                WriteString(stream, Sequence);
                PrimitiveArrays.WriteOneValue(stream, (int)TimeSource);
                PrimitiveArrays.WriteOneValue(stream, Irt);
            }

            public static ChromatogramLibraryIrt Read(Stream stream)
            {
                var seq = ReadString(stream);
                var timeSource = (TimeSource)PrimitiveArrays.ReadOneValue<int>(stream);
                var irt = PrimitiveArrays.ReadOneValue<double>(stream);
                return new ChromatogramLibraryIrt(seq, timeSource, irt);
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

        private class Serializer
        {
            private const int CURRENT_VERSION = 4;
            private const int MIN_READABLE_VERSION = 4;

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
                PrimitiveArrays.WriteOneValue(_stream, _library._libraryIrts.Length);
                foreach (var entry in _library._libraryIrts)
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
                int irtCount = PrimitiveArrays.ReadOneValue<int>(_stream);
                _library._libraryIrts = new ChromatogramLibraryIrt[irtCount];
                for (int i = 0; i < irtCount; i++)
                {
                    _library._libraryIrts[i] = ChromatogramLibraryIrt.Read(_stream);
                }
            }
            private void WriteHeader()
            {
                _locationHeader = _stream.Position;
                PrimitiveArrays.WriteOneValue(_stream, CURRENT_VERSION);
                PrimitiveArrays.WriteOneValue(_stream, _locationEntries);
                WriteString(_stream, _library.PanoramaServer);
                PrimitiveArrays.WriteOneValue(_stream, _library.LibraryRevision);
                WriteString(_stream, _library.SchemaVersion);
                PrimitiveArrays.WriteOneValue(_stream, _library._librarySourceFiles.Count());
                foreach (var sampleFile in _library._librarySourceFiles)
                {
                    sampleFile.Write(_stream);
                }
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
                    throw new InvalidDataException(string.Format(Resources.Serializer_ReadHeader_Unsupported_file_version__0_, version));
                }
                _locationEntries = PrimitiveArrays.ReadOneValue<long>(_stream);
                _library.PanoramaServer = ReadString(_stream);
                _library.LibraryRevision = PrimitiveArrays.ReadOneValue<int>(_stream);
                _library.SchemaVersion = ReadString(_stream);
                var sampleFileCount = PrimitiveArrays.ReadOneValue<int>(_stream);
                var sampleFiles = new List<ChromatogramLibrarySourceInfo>();
                for (int i = 0; i < sampleFileCount; i++)
                {
                    sampleFiles.Add(ChromatogramLibrarySourceInfo.Read(_stream));
                }
                _library._librarySourceFiles = sampleFiles.ToArray();
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
