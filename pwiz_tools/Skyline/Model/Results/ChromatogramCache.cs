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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using pwiz.Crawdad;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    public sealed class ChromatogramCache : Immutable, IDisposable
    {
        private const int FORMAT_VERSION_CACHE = 2;

        public const string EXT = ".skyd";

        /// <summary>
        /// Construct path to a final data cache from the document path.
        /// </summary>
        /// <param name="documentPath">Path to saved document</param>
        /// <param name="name">Name of data cache</param>
        /// <returns>A path to the data cache</returns>
        public static string FinalPathForName(string documentPath, string name)
        {
            string documentDir = Path.GetDirectoryName(documentPath);
            string modifier = (name != null ? '_' + name : "");
            return Path.Combine(documentDir,
                Path.GetFileNameWithoutExtension(documentPath) + modifier + EXT);
        }

        /// <summary>
        /// Construct path to a part of a progressive data cache creation
        /// in the document directory, named after the result file.
        /// </summary>
        /// <param name="documentPath">Path to saved document</param>
        /// <param name="dataFilePath">Results file path</param>
        /// <param name="name">Name of data cache</param>
        /// <returns>A path to the data cache</returns>
        public static string PartPathForName(string documentPath, string dataFilePath, string name)
        {
            string filePath = SampleHelp.GetPathFilePart(dataFilePath);
            StringBuilder sbName = new StringBuilder(Path.GetFileNameWithoutExtension(filePath));
            // If it has a sample name, append the index to differentiate this name from
            // the other samples in the multi-sample file
            if (SampleHelp.HasSamplePart(dataFilePath))
                sbName.Append('_').Append(SampleHelp.GetPathSampleIndexPart(dataFilePath));
            if (name != null)
                sbName.Append('_').Append(name);
            // Append the extension to differentiate between different file types (.mzML, .mzXML)
            sbName.Append(Path.GetExtension(filePath));
            sbName.Append(EXT);

            return Path.Combine(Path.GetDirectoryName(documentPath), sbName.ToString());
        }

        private readonly ReadOnlyCollection<ChromCachedFile> _cachedFiles;
        // ReadOnlyCollection is not fast enough for use with these arrays
        private readonly ChromGroupHeaderInfo[] _chromatogramEntries;
        private readonly ChromTransition[] _chromTransitions;
        private readonly ChromPeak[] _chromatogramPeaks;

        private ChromatogramCache(string cachePath,
            int version,
            IList<ChromCachedFile> cachedFiles,
            ChromGroupHeaderInfo[] chromatogramEntries,
            ChromTransition[] chromTransitions,
            ChromPeak[] chromatogramPeaks,
            IPooledStream readStream)
        {
            CachePath = cachePath;
            Version = version;
            _cachedFiles = MakeReadOnly(cachedFiles);
            _chromatogramEntries = chromatogramEntries;
            _chromTransitions = chromTransitions;
            _chromatogramPeaks = chromatogramPeaks;
            ReadStream = readStream;
        }

        public string CachePath { get; private set; }
        public int Version { get; private set; }
        public IList<ChromCachedFile> CachedFiles { get { return _cachedFiles; } }
        public IPooledStream ReadStream { get; private set; }

        public IEnumerable<string> CachedFilePaths
        {
            get
            {
                foreach (var cachedFile in CachedFiles)
                    yield return cachedFile.FilePath;
            }
        }

        public bool IsCurrentVersion
        {
            get { return (Version == FORMAT_VERSION_CACHE); }
        }

        public bool IsCurrentDisk
        {
            get { return CachedFiles.IndexOf(cachedFile => !cachedFile.IsCurrent) == -1; }
        }

        public bool TryLoadChromatogramInfo(TransitionGroupDocNode nodeGroup, float tolerance,
            out ChromatogramGroupInfo[] infoSet)
        {
            ChromGroupHeaderInfo[] headers;
            if (TryLoadChromInfo(nodeGroup, tolerance, out headers))
            {
                var infoSetNew = new ChromatogramGroupInfo[headers.Length];
                for (int i = 0; i < headers.Length; i++)
                {
                    infoSetNew[i] = new ChromatogramGroupInfo(headers[i],
                        _cachedFiles, _chromTransitions, _chromatogramPeaks);
                }
                infoSet = infoSetNew;
                return true;
            }

            infoSet = new ChromatogramGroupInfo[0];
            return false;            
        }

        public int Count
        {
            get { return _chromatogramEntries.Length; }
        }

        private ChromatogramCache ChangeCachePath(string prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.CachePath = v, prop);
        }        

        public void Dispose()
        {
            ReadStream.CloseStream();
        }

        private bool TryLoadChromInfo(TransitionGroupDocNode nodeGroup, float tolerance,
            out ChromGroupHeaderInfo[] headerInfos)
        {
            float precursorMz = (float)nodeGroup.PrecursorMz;
            int i = FindEntry(precursorMz, tolerance);
            if (i == -1)
            {
                headerInfos = new ChromGroupHeaderInfo[0];
                return false;
            }

            // Add entries to a list until they no longer match
            var listChromatograms = new List<ChromGroupHeaderInfo>();
            while (i < _chromatogramEntries.Length &&
                    MatchMz(precursorMz, _chromatogramEntries[i].Precursor, tolerance))
            {
                listChromatograms.Add(_chromatogramEntries[i++]);
            }

            headerInfos = listChromatograms.ToArray();
            return headerInfos.Length > 0;
        }

        private int FindEntry(float precursorMz, float tolerance)
        {
            if (_chromatogramEntries == null)
                return -1;
            return FindEntry(precursorMz, tolerance, 0, _chromatogramEntries.Length - 1);
        }

        private int FindEntry(float precursorMz, float tolerance, int left, int right)
        {
            // Binary search for the right precursorMz
            if (left > right)
                return -1;
            int mid = (left + right) / 2;
            int compare = CompareMz(precursorMz, _chromatogramEntries[mid].Precursor, tolerance);
            if (compare < 0)
                return FindEntry(precursorMz, tolerance, left, mid - 1);
            else if (compare > 0)
                return FindEntry(precursorMz, tolerance, mid + 1, right);
            else
            {
                // Scan backward until the first matching element is found.
                while (mid > 0 && MatchMz(precursorMz, tolerance, _chromatogramEntries[mid - 1].Precursor))
                    mid--;

                return mid;
            }
        }

        private static int CompareMz(float precursorMz1, float precursorMz2, float tolerance)
        {
            return ChromKey.CompareTolerant(precursorMz1, precursorMz2,
                tolerance);
        }

        private static bool MatchMz(float mz1, float mz2, float tolerance)
        {
            return CompareMz(mz1, mz2, tolerance) == 0;
        }

        // ReSharper disable UnusedMember.Local
        private enum Header
        {
            format_version,
            num_peaks,
            location_peaks_lo,
            location_peaks_hi,
            num_transitions,
            location_trans_lo,
            location_trans_hi,
            num_chromatograms,
            location_headers_lo,
            location_headers_hi,
            num_files,
            location_files_lo,
            location_files_hi,

            count
        }

        private enum FileHeader
        {
            modified_lo,
            modified_hi,
            len_path,

            count
        }
        // ReSharper restore UnusedMember.Local

        public static ChromatogramCache Load(string cachePath, ProgressStatus status, ILoadMonitor loader)
        {
            status = status.ChangeMessage(string.Format("Loading {0} cache", Path.GetFileName(cachePath)));
            loader.UpdateProgress(status);

            IPooledStream readStream = null;
            try
            {
                readStream = loader.StreamManager.CreatePooledStream(cachePath, false);
                Stream stream = readStream.Stream;
                int formatVersion;
                ChromCachedFile[] chromCacheFiles;
                ChromGroupHeaderInfo[] chromatogramEntries;
                ChromTransition[] chromTransitions;
                ChromPeak[] chromatogramPeaks;

                LoadStructs(stream,
                            out formatVersion,
                            out chromCacheFiles,
                            out chromatogramEntries,
                            out chromTransitions,
                            out chromatogramPeaks);

                var result = new ChromatogramCache(cachePath,
                                                   formatVersion,
                                                   chromCacheFiles,
                                                   chromatogramEntries,
                                                   chromTransitions,
                                                   chromatogramPeaks,
                                                   readStream);
                loader.UpdateProgress(status.Complete());
                return result;
            }
            finally
            {
                if (readStream != null)
                {
                    // Close the read stream to ensure we never leak it.
                    // This only costs on extra open, the first time the
                    // active document tries to read.
                    try { readStream.CloseStream(); }
                    catch (IOException) { }
                }
            }
        }

        public static void Join(string cachePath, IPooledStream streamDest,
            IList<string> listCachePaths, ProgressStatus status, ILoadMonitor loader,
            Action<ChromatogramCache, Exception> complete)
        {
            var joiner = new Joiner(cachePath, streamDest, listCachePaths, loader, status, complete);
            joiner.JoinParts();
        }

        public static void Build(string cachePath, IList<string> listResultPaths, ProgressStatus status, ILoadMonitor loader,
            Action<ChromatogramCache, Exception> complete)
        {
            var builder = new Builder(cachePath, listResultPaths, loader, status, complete);
            builder.BuildCache();
        }

        private static long LoadStructs(Stream stream,
            out int formatVersion,
            out ChromCachedFile[] chromCacheFiles,
            out ChromGroupHeaderInfo[] chromatogramEntries,
            out ChromTransition[] chromTransitions,
            out ChromPeak[] chromatogramPeaks)
        {
            // Read library header from the end of the cache
            const int countHeader = (int)Header.count * 4;
            stream.Seek(-countHeader, SeekOrigin.End);

            byte[] cacheHeader = new byte[countHeader];
            ReadComplete(stream, cacheHeader, countHeader);

            formatVersion = GetInt32(cacheHeader, (int)Header.format_version);
            if (formatVersion != FORMAT_VERSION_CACHE)
            {
                chromCacheFiles = new ChromCachedFile[0];
                chromatogramEntries = new ChromGroupHeaderInfo[0];
                chromTransitions = new ChromTransition[0];
                chromatogramPeaks = new ChromPeak[0];
                return 0;
            }

            int numPeaks = GetInt32(cacheHeader, (int)Header.num_peaks);
            long locationPeaks = BitConverter.ToInt64(cacheHeader, ((int)Header.location_peaks_lo) * 4);
            int numChrom = GetInt32(cacheHeader, (int)Header.num_chromatograms);
            long locationHeaders = BitConverter.ToInt64(cacheHeader, ((int)Header.location_headers_lo) * 4);
            int numTrans = GetInt32(cacheHeader, (int)Header.num_transitions);
            long locationTrans = BitConverter.ToInt64(cacheHeader, ((int)Header.location_trans_lo) * 4);
            int numFiles = GetInt32(cacheHeader, (int)Header.num_files);
            long locationFiles = BitConverter.ToInt64(cacheHeader, ((int)Header.location_files_lo) * 4);

            // Read list of files cached
            stream.Seek(locationFiles, SeekOrigin.Begin);
            chromCacheFiles = new ChromCachedFile[numFiles];
            const int countFileHeader = (int)FileHeader.count * 4;
            byte[] fileHeader = new byte[countFileHeader];
            byte[] filePathBuffer = new byte[1024];
            for (int i = 0; i < numFiles; i++)
            {
                ReadComplete(stream, fileHeader, countFileHeader);
                long modifiedBinary = BitConverter.ToInt64(fileHeader, ((int)FileHeader.modified_lo) * 4);
                int lenPath = GetInt32(fileHeader, (int)FileHeader.len_path);
                ReadComplete(stream, filePathBuffer, lenPath);
                string filePath = Encoding.Default.GetString(filePathBuffer, 0, lenPath);
                chromCacheFiles[i] = new ChromCachedFile(filePath, DateTime.FromBinary(modifiedBinary));
            }

            // Read list of chromatogram group headers
            stream.Seek(locationHeaders, SeekOrigin.Begin);
            chromatogramEntries = ChromGroupHeaderInfo.ReadArray(stream, numChrom);
            // Read list of transitions
            stream.Seek(locationTrans, SeekOrigin.Begin);
            chromTransitions = ChromTransition.ReadArray(stream, numTrans);
            // Read list of peaks
            stream.Seek(locationPeaks, SeekOrigin.Begin);
            chromatogramPeaks = ChromPeak.ReadArray(stream, numPeaks);

            return locationPeaks;
        }

        private static int GetInt32(byte[] bytes, int index)
        {
            int ibyte = index * 4;
            return bytes[ibyte] | bytes[ibyte + 1] << 8 | bytes[ibyte + 2] << 16 | bytes[ibyte + 3] << 24;
        }

        private static void ReadComplete(Stream stream, byte[] buffer, int size)
        {
            if (stream.Read(buffer, 0, size) != size)
                throw new InvalidDataException("Data truncation in cache header. File may be corrupted.");
        }

        private static void WriteStructs(Stream outStream,
            ICollection<ChromCachedFile> chromCachedFiles,
            List<ChromGroupHeaderInfo> chromatogramEntries,
            ICollection<ChromTransition> chromTransitions,
            ICollection<ChromPeak> chromatogramPeaks)
        {
            // Write the picked peaks
            long locationPeaks = outStream.Position;
            foreach (var peak in chromatogramPeaks)
            {
                outStream.Write(BitConverter.GetBytes(peak.RetentionTime), 0, sizeof(float));
                outStream.Write(BitConverter.GetBytes(peak.StartTime), 0, sizeof(float));
                outStream.Write(BitConverter.GetBytes(peak.EndTime), 0, sizeof(float));
                outStream.Write(BitConverter.GetBytes(peak.Area), 0, sizeof(float));
                outStream.Write(BitConverter.GetBytes(peak.BackgroundArea), 0, sizeof(float));
                outStream.Write(BitConverter.GetBytes(peak.Height), 0, sizeof(float));
                outStream.Write(BitConverter.GetBytes(peak.Fwhm), 0, sizeof(float));
                outStream.Write(BitConverter.GetBytes((int) peak.Flags), 0, sizeof(int));
            }

            // Write the transitions
            long locationTrans = outStream.Position;
            foreach (var tran in chromTransitions)
            {
                outStream.Write(BitConverter.GetBytes(tran.Product), 0, sizeof(float));
            }

            // Write sorted list of chromatogram header info structs
            chromatogramEntries.Sort();

            long locationHeaders = outStream.Position;
            foreach (var info in chromatogramEntries)
            {
                outStream.Write(BitConverter.GetBytes(info.Precursor), 0, sizeof(float));
                outStream.Write(BitConverter.GetBytes(info.FileIndex), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(info.NumTransitions), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(info.StartTransitionIndex), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(info.NumPeaks), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(info.StartPeakIndex), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(info.MaxPeakIndex), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(info.NumPoints), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(info.CompressedSize), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(info.Align), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(info.LocationPoints), 0, sizeof(long));
            }

            // Write the list of cached files and their modification time stamps
            long locationFiles = outStream.Position;
            byte[] pathBuffer = new byte[0x1000];
            foreach (var cachedFile in chromCachedFiles)
            {
                long time = cachedFile.FileWriteTime.ToBinary();
                outStream.Write(BitConverter.GetBytes(time), 0, sizeof(long));
                int len = cachedFile.FilePath.Length;
                Encoding.Default.GetBytes(cachedFile.FilePath, 0, len, pathBuffer, 0);
                outStream.Write(BitConverter.GetBytes(len), 0, sizeof(int));
                outStream.Write(pathBuffer, 0, len);
            }

            outStream.Write(BitConverter.GetBytes(FORMAT_VERSION_CACHE), 0, sizeof(int));
            outStream.Write(BitConverter.GetBytes(chromatogramPeaks.Count), 0, sizeof(int));
            outStream.Write(BitConverter.GetBytes(locationPeaks), 0, sizeof(long));
            outStream.Write(BitConverter.GetBytes(chromTransitions.Count), 0, sizeof(int));
            outStream.Write(BitConverter.GetBytes(locationTrans), 0, sizeof(long));
            outStream.Write(BitConverter.GetBytes(chromatogramEntries.Count), 0, sizeof(int));
            outStream.Write(BitConverter.GetBytes(locationHeaders), 0, sizeof(long));
            outStream.Write(BitConverter.GetBytes(chromCachedFiles.Count), 0, sizeof(int));
            outStream.Write(BitConverter.GetBytes(locationFiles), 0, sizeof(long));
        }

        public static void BytesToTimeIntensities(byte[] peaks, int numPoints, int numTrans,
            out float[][] intensities, out float[] times)
        {
            times = new float[numPoints];
            intensities = new float[numTrans][];
            for (int i = 0; i < numTrans; i++)
                intensities[i] = new float[numPoints];

            for (int i = 0, offset = 0; i < numPoints; i++, offset += sizeof(float))
                times[i] = BitConverter.ToSingle(peaks, offset);
            int sizeArray = sizeof(float) * numPoints;
            for (int i = 0, offsetTran = sizeArray; i < numTrans; i++, offsetTran += sizeArray)
            {
                for (int j = 0, offset = 0; j < numPoints; j++, offset += sizeof(float))
                    intensities[i][j] = BitConverter.ToSingle(peaks, offset + offsetTran);
            }
        }

        public static byte[] TimeIntensitiesToBytes(float[] times, float[][] intensities)
        {
            int len = times.Length;
            int countChroms = intensities.Length;
            int timeBytes = len * sizeof(float);
            byte[] points = new byte[timeBytes * (countChroms + 1)];
            for (int i = 0, offset = 0; i < len; i++, offset += sizeof(float))
                Array.Copy(BitConverter.GetBytes(times[i]), 0, points, offset, sizeof(float));
            for (int i = 0, offsetTran = timeBytes; i < countChroms; i++, offsetTran += timeBytes)
            {
                for (int j = 0, offset = 0; j < len; j++, offset += sizeof(float))
                {
                    Array.Copy(BitConverter.GetBytes(intensities[i][j]), 0,
                               points, offsetTran + offset, sizeof(float));
                }
            }
            return points;
        }

        public ChromatogramCache Optimize(string documentPath, IEnumerable<string> msDataFilePaths, IStreamManager streamManager)
        {
            string cachePathOpt = FinalPathForName(documentPath, null);

            var cachedFilePaths = new HashSet<string>(CachedFilePaths);
            cachedFilePaths.IntersectWith(msDataFilePaths);
            // If the cache contains only the files in the document, then no
            // further optimization is necessary.
            if (cachedFilePaths.Count == CachedFiles.Count)
            {
                if (Equals(cachePathOpt, CachePath))
                    return this;
                // Copy the cache, if movint to a new location
                File.Copy(CachePath, cachePathOpt);
                return ChangeCachePath(cachePathOpt);
            }

            Debug.Assert(cachedFilePaths.Count > 0);

            // Create a copy of the headers
            var listEntries = new List<ChromGroupHeaderInfo>(_chromatogramEntries);
            // Sort by file, points location
            listEntries.Sort((e1, e2) =>
                                 {
                                     int result = Comparer.Default.Compare(e1.FileIndex, e2.FileIndex);
                                     if (result != 0)
                                         return result;
                                     return Comparer.Default.Compare(e1.LocationPoints, e2.LocationPoints);
                                 });

            var listKeepEntries = new List<ChromGroupHeaderInfo>();
            var listKeepCachedFiles = new List<ChromCachedFile>();
            var listKeepPeaks = new List<ChromPeak>();
            var listKeepTransitions = new List<ChromTransition>();

            using (FileSaver fs = new FileSaver(cachePathOpt))
            {
                var inStream = ReadStream.Stream;
                var outStream = streamManager.CreateStream(fs.SafeName, FileMode.Create, true);

                byte[] buffer = new byte[0x40000];  // 256K

                int i = 0;
                do
                {
                    var firstEntry = listEntries[i];
                    var lastEntry = firstEntry;
                    int fileIndex = firstEntry.FileIndex;
                    bool keepFile = cachedFilePaths.Contains(_cachedFiles[fileIndex].FilePath);
                    long offsetPoints = outStream.Position - firstEntry.LocationPoints;

                    int iNext = i;
                    // Enumerate until end of current file encountered
                    while (iNext < listEntries.Count && fileIndex == listEntries[iNext].FileIndex)
                    {
                        lastEntry = listEntries[iNext++];
                        // If discarding this file, just skip its entries
                        if (!keepFile)
                            continue;
                        // Otherwise add entries to the keep lists
                        listKeepEntries.Add(new ChromGroupHeaderInfo(lastEntry.Precursor,
                            listKeepCachedFiles.Count,
                            lastEntry.NumTransitions,
                            listKeepTransitions.Count,
                            lastEntry.NumPeaks,
                            listKeepPeaks.Count,
                            lastEntry.MaxPeakIndex,
                            lastEntry.NumPoints,
                            lastEntry.CompressedSize,
                            lastEntry.LocationPoints + offsetPoints));
                        int start = lastEntry.StartTransitionIndex;
                        int end = start + lastEntry.NumTransitions;
                        for (int j = start; j < end; j++)
                            listKeepTransitions.Add(_chromTransitions[j]);
                        start = lastEntry.StartPeakIndex;
                        end = start + lastEntry.NumPeaks*lastEntry.NumTransitions;
                        for (int j = start; j < end; j++)
                            listKeepPeaks.Add(_chromatogramPeaks[j]);
                    }

                    if (keepFile)
                    {
                        listKeepCachedFiles.Add(_cachedFiles[fileIndex]);

                        // Write all points for the last file to the output stream
                        inStream.Seek(firstEntry.LocationPoints, SeekOrigin.Begin);
                        long lenRead = lastEntry.LocationPoints + lastEntry.CompressedSize - firstEntry.LocationPoints;
                        int len;
                        while (lenRead > 0 && (len = inStream.Read(buffer, 0, (int)Math.Min(lenRead, buffer.Length))) != 0)
                        {
                            outStream.Write(buffer, 0, len);
                            lenRead -= len;
                        }                        
                    }

                    // Advance to next file
                    i = iNext;
                }
                while (i < listEntries.Count);

                WriteStructs(outStream, listKeepCachedFiles, listKeepEntries, listKeepTransitions, listKeepPeaks);

                outStream.Close();
                // Close the read stream, in case the destination is the source, and
                // overwrite is necessary.
                ReadStream.CloseStream();
                fs.Commit(ReadStream);
            }

            return new ChromatogramCache(CachePath,
                                         FORMAT_VERSION_CACHE,
                                         listKeepCachedFiles.ToArray(),
                                         listKeepEntries.ToArray(),
                                         listKeepTransitions.ToArray(),
                                         listKeepPeaks.ToArray(),
                                         // Create a new read stream, for the newly created file
                                         streamManager.CreatePooledStream(CachePath, false));
        }

        private class CacheWriter : IDisposable
        {
            private readonly Action<ChromatogramCache, Exception> _completed;

            protected readonly List<ChromCachedFile> _listCachedFiles = new List<ChromCachedFile>();
            protected readonly List<ChromPeak> _listPeaks = new List<ChromPeak>();
            protected readonly List<ChromTransition> _listTransitions = new List<ChromTransition>();
            protected readonly List<ChromGroupHeaderInfo> _listGroups = new List<ChromGroupHeaderInfo>();
            protected readonly FileSaver _fs;
            protected readonly ILoadMonitor _loader;
            protected ProgressStatus _status;
            protected Stream _outStream;
            protected IPooledStream _destinationStream;

            protected CacheWriter(string cachePath, ILoadMonitor loader, ProgressStatus status,
                Action<ChromatogramCache, Exception> completed)
            {
                CachePath = cachePath;
                _fs = new FileSaver(CachePath);
                _loader = loader;
                _status = status;
                _completed = completed;
            }

            protected string CachePath { get; private set; }

            protected void Complete(Exception x)
            {
                lock (this)
                {
                    ChromatogramCache result = null;
                    try
                    {
                        if (x == null && !_status.IsFinal)
                        {
                            if (_outStream != null)
                            {
                                WriteStructs(_outStream, _listCachedFiles, _listGroups, _listTransitions, _listPeaks);

                                _loader.StreamManager.Finish(_outStream);
                                _outStream = null;
                                _fs.Commit(_destinationStream);
                            }

                            // Create stream identifier, but do not open.  The stream will be opened
                            // the first time the document uses it.
                            var readStream = _loader.StreamManager.CreatePooledStream(CachePath, false);

                            result = new ChromatogramCache(CachePath,
                                                           FORMAT_VERSION_CACHE,
                                                           _listCachedFiles.ToArray(),
                                                           _listGroups.ToArray(),
                                                           _listTransitions.ToArray(),
                                                           _listPeaks.ToArray(),
                                                           readStream);
                            _loader.UpdateProgress(_status.Complete());
                        }
                    }
                    catch (Exception x2)
                    {
                        x = x2;
                    }
                    finally
                    {
                        Dispose();
                    }

                    _completed(result, x);
                }
            }

            public virtual void Dispose()
            {
                if (_outStream != null)
                {
                    try { _loader.StreamManager.Finish(_outStream); }
                    catch (IOException) { }
                }
                _fs.Dispose();
            }
        }

        private sealed class Joiner : CacheWriter
        {
            private int _currentPartIndex = -1;
            private long _copyBytes;
            private Stream _inStream;
            private readonly byte[] _buffer = new byte[0x40000];  // 256K

            public Joiner(string cachePath, IPooledStream streamDest,
                IList<string> cacheFilePaths, ILoadMonitor loader, ProgressStatus status,
                Action<ChromatogramCache, Exception> completed)
                : base(cachePath, loader, status, completed)
            {
                _destinationStream = streamDest;

                CacheFilePaths = cacheFilePaths;
            }

            private IList<string> CacheFilePaths { get; set; }

            public void JoinParts()
            {
                lock (this)
                {
                    if (_currentPartIndex != -1)
                        return;
                    _currentPartIndex = 0;
                    JoinNextPart();
                }
            }

            private void JoinNextPart()
            {
                lock (this)
                {
                    if (_currentPartIndex >= CacheFilePaths.Count)
                    {
                        Complete(null);
                        return;
                    }

                    // Check for cancellation on every part.
                    if (_loader.IsCanceled)
                    {
                        _loader.UpdateProgress(_status = _status.Cancel());
                        Complete(null);
                        return;
                    }

                    // If not cancelled, update progress.
                    string cacheFilePath = CacheFilePaths[_currentPartIndex];
                    string message = string.Format("Joining file {0}", cacheFilePath);
                    int percent = _currentPartIndex * 100 / CacheFilePaths.Count;
                    _status = _status.ChangeMessage(message).ChangePercentComplete(percent);
                    _loader.UpdateProgress(_status);

                    try
                    {
                        _inStream = _loader.StreamManager.CreateStream(cacheFilePath, FileMode.Open, false);

                        if (_outStream == null)
                            _outStream = _loader.StreamManager.CreateStream(_fs.SafeName, FileMode.Create, true);

                        int formatVersion;
                        ChromCachedFile[] chromCacheFiles;
                        ChromGroupHeaderInfo[] chromatogramEntries;
                        ChromTransition[] chromTransitions;
                        ChromPeak[] chromatogramPeaks;

                        long bytesData = LoadStructs(_inStream,
                                                     out formatVersion,
                                                     out chromCacheFiles,
                                                     out chromatogramEntries,
                                                     out chromTransitions,
                                                     out chromatogramPeaks);

                        // If joining, then format version should have already been checked.
                        Debug.Assert(formatVersion == FORMAT_VERSION_CACHE);

                        int offsetFiles = _listCachedFiles.Count;
                        int offsetTransitions = _listTransitions.Count;
                        int offsetPeaks = _listPeaks.Count;
                        long offsetPoints = _outStream.Position;

                        _copyBytes = bytesData;
                        _inStream.Seek(0, SeekOrigin.Begin);

                        CopyInToOut();

                        _listCachedFiles.AddRange(chromCacheFiles);
                        _listPeaks.AddRange(chromatogramPeaks);
                        _listTransitions.AddRange(chromTransitions);
                        for (int i = 0; i < chromatogramEntries.Length; i++)
                            chromatogramEntries[i].Offset(offsetFiles, offsetTransitions, offsetPeaks, offsetPoints);
                        _listGroups.AddRange(chromatogramEntries);
                    }
                    catch (InvalidDataException x)
                    {
                        Complete(x);
                    }
                    catch (IOException x)
                    {
                        Complete(x);
                    }
                    catch (Exception x)
                    {
                        Complete(new Exception(string.Format("Failed to create cache '{0}'.", CachePath), x));
                    }
                }
            }

            private void CopyInToOut()
            {
                if (_copyBytes > 0)
                {
                    _inStream.BeginRead(_buffer, 0, (int)Math.Min(_buffer.Length, _copyBytes),
                                        FinishRead, null);
                }
                else
                {
                    try { _inStream.Close(); }
                    catch (IOException) { }
                    _inStream = null;

                    _currentPartIndex++;
                    JoinNextPart();
                }
            }

            private void FinishRead(IAsyncResult ar)
            {
                try
                {
                    int read = _inStream.EndRead(ar);
                    if (read == 0)
                        throw new IOException(string.Format("Unexpected end of file in {0}.", CacheFilePaths[_currentPartIndex]));
                    _copyBytes -= read;
                    _outStream.BeginWrite(_buffer, 0, read, FinishWrite, null);
                }
                catch (Exception x)
                {
                    Complete(x);
                }
            }

            private void FinishWrite(IAsyncResult ar)
            {
                try
                {
                    _outStream.EndWrite(ar);
                    CopyInToOut();
                }
                catch (Exception x)
                {
                    Complete(x);
                }
            }

            public override void Dispose()
            {
                base.Dispose();
                if (_inStream != null)
                {
                    try { _inStream.Close(); }
                    catch (IOException) { }
                }
            }
        }

        private sealed class Builder : CacheWriter
        {
            private int _currentFileIndex = -1;
            private readonly List<ChromDataSet> _chromDataSets = new List<ChromDataSet>();
            private bool _writerStarted;
            private bool _readedCompleted;
            private Exception _writeException;

            public Builder(string cachePath, IList<string> msDataFilePaths,
                    ILoadMonitor loader, ProgressStatus status,
                    Action<ChromatogramCache, Exception> complete)
                : base(cachePath, loader, status, complete)
            {
                MSDataFilePaths = msDataFilePaths;
            }

            private IList<string> MSDataFilePaths { get; set; }

            public void BuildCache()
            {
                lock (this)
                {
                    if (_currentFileIndex != -1)
                        return;
                    _currentFileIndex = 0;
                    BuildNextFile();
                }
            }

            private void BuildNextFile()
            {
                lock (this)
                {
                    if (_currentFileIndex >= MSDataFilePaths.Count)
                    {
                        Complete(null);
                        return;
                    }

                    // Check for cancellation on every chromatogram, because there
                    // have been some files that load VERY slowly, and appear to hang
                    // on a single file.
                    if (_loader.IsCanceled)
                    {
                        _loader.UpdateProgress(_status = _status.Cancel());
                        Complete(null);
                        return;
                    }

                    // If not cancelled, update progress.
                    string dataFilePath = MSDataFilePaths[_currentFileIndex];
                    string message = string.Format("Caching file {0}", dataFilePath);
                    int percent = _currentFileIndex*100/MSDataFilePaths.Count;
                    _status = _status.ChangeMessage(message).ChangePercentComplete(percent);
                    _loader.UpdateProgress(_status);

                    try
                    {
                        string dataFilePathPart;
                        dataFilePath = ChromatogramSet.GetExistingDataFilePath(CachePath, dataFilePath, out dataFilePathPart);                        
                        if (dataFilePath == null)
                            throw new FileNotFoundException(string.Format("The file {0} does not exist.", dataFilePathPart), dataFilePathPart);
                        MSDataFilePaths[_currentFileIndex] = dataFilePath;

                        // HACK: Force the thread that the writer will use into existence
                        // This allows Reader_Waters to function normally the first time through.
                        //
                        // TODO: Use of Reader_Waters will, however, eventually kill the ThreadPool
                        // So, something better needs to be worked out, if we can't get a fix
                        // from Waters.
                        //
                        // This does not actually start the loop, but calling the function once,
                        // seems to reserve a thread somehow, so that the next call works.
                        Action<int, bool> writer = WriteLoop;
                        writer.BeginInvoke(_currentFileIndex, true, null, null);

                        // Read the instrument data indexes
                        int sampleIndex = SampleHelp.GetPathSampleIndexPart(dataFilePath);
                        if (sampleIndex == -1)
                            sampleIndex = 0;

                        using (var inFile = new MsDataFileImpl(dataFilePathPart, sampleIndex))
                        {

                            // Check for cancelation
                            if (_loader.IsCanceled)
                            {
                                _loader.UpdateProgress(_status = _status.Cancel());
                                Complete(null);
                                return;
                            }
                            if (_outStream == null)
                                _outStream = _loader.StreamManager.CreateStream(_fs.SafeName, FileMode.Create, true);

                            // Read and write the mass spec data
                            if (inFile.ChromatogramCount > 0)
                                ReadChromatograms(inFile);
                            else
                            {
                                if (inFile.SpectrumCount > 0)
                                    ReadSpectra(inFile);
                                else
                                {
                                    throw new InvalidDataException(string.Format("The sample {0} contains no usable data.",
                                        SampleHelp.GetFileSampleName(dataFilePath)));
                                }
                            }
                        }

                        if (_status.IsCanceled)
                            Complete(null);
                    }
                    catch (Exception x)
                    {
                        // Add a more generic message to an exception message that may
                        // be fairly unintelligible to the user, but keep the exception
                        // message, because ProteoWizard "Unsupported file format" comes
                        // in on this channel.
                        Complete(x);
                    }
                }
            }

            private void ReadSpectra(MsDataFileImpl dataFile)
            {
                // 10% done with this file
                int percent = (_currentFileIndex*10 + 1)*100/MSDataFilePaths.Count/10;
                _loader.UpdateProgress(_status = _status.ChangePercentComplete(percent));

                // First read all of the spectra, building chromatogram time, intensity lists
                var chromMap = new Dictionary<double, ChromDataCollector>();
                int lenSpectra = dataFile.SpectrumCount;
                int eighth = 0;
                for (int i = 0; i < lenSpectra; i++)
                {
                    // Update progress indicator
                    if (i*8/lenSpectra > eighth)
                    {
                        // Check for cancellation after each integer change in percent loaded.
                        if (_loader.IsCanceled)
                        {
                            _loader.UpdateProgress(_status = _status.Cancel());
                            return;
                        }
                        eighth++;
                        percent = (_currentFileIndex*10 + 1 + eighth)*100/MSDataFilePaths.Count/10;
                        _loader.UpdateProgress(_status = _status.ChangePercentComplete(percent));
                    }

                    double? time, precursorMz;
                    double[] mzArray, intensityArray;
                    if (!dataFile.GetSrmSpectrum(i, out time, out precursorMz, out mzArray, out intensityArray))
                        continue;
                    if (!time.HasValue)
                        throw new InvalidDataException(string.Format("Scan {0} found without scan time.", dataFile.GetSpectrumId(i)));
                    if (!precursorMz.HasValue)
                        throw new InvalidDataException(string.Format("Scan {0} found without precursor m/z.", dataFile.GetSpectrumId(i)));

                    ChromDataCollector collector;
                    if (!chromMap.TryGetValue(precursorMz.Value, out collector))
                    {
                        collector = new ChromDataCollector(precursorMz.Value);
                        chromMap.Add(precursorMz.Value, collector);
                    }

                    int ionCount = collector.ProductIntensityMap.Count;
                    int ionScanCount = mzArray.Length;
                    if (ionCount == 0)
                        ionCount = ionScanCount;

                    int lenTimesCurrent = collector.TimeCount;
                    for (int j = 0; j < ionScanCount; j++)
                    {
                        double productMz = mzArray[j];
                        double intensity = intensityArray[j];

                        ChromCollector tis;
                        if (!collector.ProductIntensityMap.TryGetValue(productMz, out tis))
                        {
                            tis = new ChromCollector();
                            // If more than a single ion scan, add any zeros necessary
                            // to make this new chromatogram have an entry for each time.
                            if (ionScanCount > 1)
                            {
                                for (int k = 0; k < lenTimesCurrent; k++)
                                    tis.Intensities.Add(0);
                            }
                            collector.ProductIntensityMap.Add(productMz, tis);
                        }
                        int lenTimes = tis.Times.Count;
                        if (lenTimes == 0 || time >= tis.Times[lenTimes - 1])
                        {
                            tis.Times.Add((float)time);
                            tis.Intensities.Add((float)intensity);
                        }
                        else
                        {
                            // Insert out of order time in the correct location
                            int iGreater = tis.Times.BinarySearch((float)time);
                            if (iGreater < 0)
                                iGreater = ~iGreater;
                            tis.Times.Insert(iGreater, (float)time);
                            tis.Intensities.Insert(iGreater, (float)intensity);
                        }
                    }

                    // If this was a multiple ion scan and not all ions had measurements,
                    // make sure missing ions have zero intensities in the chromatogram.
                    if (ionScanCount > 1 &&
                        (ionCount != ionScanCount || ionCount != collector.ProductIntensityMap.Count))
                    {
                        // Times should have gotten one longer
                        lenTimesCurrent++;
                        foreach (var tis in collector.ProductIntensityMap.Values)
                        {
                            if (tis.Intensities.Count < lenTimesCurrent)
                            {
                                tis.Intensities.Add(0);
                                tis.Times.Add((float) time);
                            }
                        }
                    }
                }

                if (chromMap.Count == 0)
                    throw new InvalidDataException(String.Format("No SRM/MRM data found in {0}.",
                        SampleHelp.GetFileSampleName(MSDataFilePaths[_currentFileIndex])));

                foreach (var collector in chromMap.Values)
                {
                    var chromDataSet = new ChromDataSet(true);
                    foreach (var pair in collector.ProductIntensityMap)
                    {
                        var key = new ChromKey(collector.PrecursorMz, pair.Key);
                        var tis = pair.Value;
                        chromDataSet.Add(new ChromData(key, tis.Times.ToArray(), tis.Intensities.ToArray()));
                    }
                    PostChromDataSet(chromDataSet, false);
                }
                PostChromDataSet(null, true);
            }

            private void ReadChromatograms(MsDataFileImpl dataFile)
            {
                int len = dataFile.ChromatogramCount;

                var arrayKeyIndex = new List<KeyValuePair<ChromKey, int>>();
                for (int i = 0; i < len; i++)
                {
                    int index;
                    string id = dataFile.GetChromatogramId(i, out index);

                    if (!ChromKey.IsKeyId(id))
                        continue;

                    var ki = new KeyValuePair<ChromKey, int>(ChromKey.FromId(id), index);
                    arrayKeyIndex.Add(ki);
                }

                if (arrayKeyIndex.Count == 0)
                    throw new InvalidDataException(String.Format("No chromatogram data found in {0}.",
                        SampleHelp.GetFileSampleName(MSDataFilePaths[_currentFileIndex])));

                arrayKeyIndex.Sort((p1, p2) => p1.Key.CompareTo(p2.Key));

                // Half-way done with this file
                int percentStart = (_currentFileIndex*2 + 1)*100/MSDataFilePaths.Count/2;
                int percentComplete = (_currentFileIndex + 1)*100/MSDataFilePaths.Count;
                _loader.UpdateProgress(_status = _status.ChangePercentComplete(percentStart));

                // TODO: Handle the case where multiple TransitionGroups share a single precursorMz
                //       And different time sets for scheduled methods

                string lastChromId = null;
                var chromDataSet = new ChromDataSet(false);
//                for (int i = 0; i < len; i++)
                int iKey = 0;
                foreach (var keyIndex in arrayKeyIndex)
                {
                    var key = keyIndex.Key;
                    // ProteoWizard data arrays have slow access, making it faster
                    // to allocate an array here, for use downstream.
                    string chromId;
                    float[] times;
                    float[] intensities;
                    dataFile.GetChromatogram(keyIndex.Value, out chromId, out times, out intensities);

                    // Update status
                    int percent = (iKey++ * (percentComplete - percentStart) / arrayKeyIndex.Count) + percentStart;
                    if (!_status.IsPercentComplete(percent))
                    {
                        // Check for cancellation after each integer change in percent loaded.
                        if (_loader.IsCanceled)
                        {
                            _loader.UpdateProgress(_status = _status.Cancel());
                            return;
                        }

                        // If not cancelled, update progress.
                        _loader.UpdateProgress(_status = _status.ChangePercentComplete(percent));
                    }

                    var chromData = new ChromData(key, times, intensities);
                    if (lastChromId == null ||
                            key.Precursor == ChromKey.FromId(lastChromId).Precursor)
                        chromDataSet.Add(chromData);
                    else
                    {
                        PostChromDataSet(chromDataSet, false);
                        chromDataSet = new ChromDataSet(chromData);
                    }
                    lastChromId = chromId;
                }

                PostChromDataSet(chromDataSet, true);
            }

            private void PostChromDataSet(ChromDataSet chromatogramSet, bool complete)
            {
                lock (_chromDataSets)
                {
                    // First check for any errors on the writer thread
                    if (_writeException != null)
                        throw _writeException;

                    // Add new chromatogram data set, if not empty
                    if (chromatogramSet != null && chromatogramSet.Count > 0)
                    {
                        _chromDataSets.Add(chromatogramSet);
                    }
                    // Update completion status
                    _readedCompleted = _readedCompleted || complete;
                    // Notify the writer thread, if necessary
                    if (_readedCompleted || _chromDataSets.Count > 0)
                    {
                        if (_writerStarted)
                            Monitor.Pulse(_chromDataSets);
                        else
                        {
                            // Start the writer thread
                            _writerStarted = true;
                            Action<int, bool> writer = WriteLoop;
                            writer.BeginInvoke(_currentFileIndex, false, null, null);
                        }

                        // If this is the last read, then wait for the
                        // writer to complete, in case of an exception.
                        if (_readedCompleted)
                        {
                            int countSets = _chromDataSets.Count;
                            if (countSets > 0)
                            {
                                // Wait while work is being accomplished by the writer, but not
                                // if it is hung.
                                bool completed;
                                do
                                {
                                    countSets = _chromDataSets.Count;
                                    // Wait 5 seconds for some work to complete.  In debug mode,
                                    // a shorter time may not be enough to load DLLs necessary
                                    // for the first iteration.
                                    completed = Monitor.Wait(_chromDataSets, 5000);
                                }
                                while (!completed && countSets != _chromDataSets.Count);

                                // Try calling the write loop directly on this thread.
                                if (!completed)
                                    WriteLoop(_currentFileIndex, false);                                
                            }

                            if (_writeException != null)
                                throw _writeException;
                        }
                    }
                }
            }

            private void WriteLoop(int currentFileIndex, bool primeThread)
            {
                // HACK: This is a huge hack, for a temporary work-around to the problem
                // of Reader_Waters (or DACServer.dll) killing the ThreadPool.  WriteLoop
                // is called once as a no-op to force the thread it will use during
                // processing into existence before the file is opened.
                if (primeThread)
                    return;

                try
                {
                    for (;;)
                    {
                        ChromDataSet chromDataSetNext;
                        lock (_chromDataSets)
                        {
                            while (!_readedCompleted && _chromDataSets.Count == 0)
                                Monitor.Wait(_chromDataSets);

                            // If reading is complete, and there are no more sets to process,
                            // begin next file.
                            if (_readedCompleted && _chromDataSets.Count == 0)
                            {
                                // Write loop completion may have already been executed
                                if (_currentFileIndex != currentFileIndex)
                                    return;

                                string dataFilePath = MSDataFilePaths[_currentFileIndex];
                                _listCachedFiles.Add(new ChromCachedFile(dataFilePath));
                                _currentFileIndex++;

                                // Allow the reader thread to exit
                                lock (_chromDataSets)
                                {
                                    Monitor.Pulse(_chromDataSets);
                                }

                                Action build = BuildNextFile;
                                build.BeginInvoke(null, null);
                                return;
                            }

                            chromDataSetNext = _chromDataSets[0];
                            _chromDataSets.RemoveAt(0);
                        }

                        Debug.Assert(chromDataSetNext.Count > 0);

                        chromDataSetNext.PickChromatogramPeaks();

                        long location = _outStream.Position;

                        float[] times = chromDataSetNext.Times;
                        float[][] intensities = chromDataSetNext.Intensities;
                        // Write the raw chromatogram points
                        byte[] points = TimeIntensitiesToBytes(times, intensities);
                        // Compress the data (only about 12% savings)
                        byte[] peaksCompressed = points.Compress(3);
                        int lenCompressed = peaksCompressed.Length;
                        _outStream.Write(peaksCompressed, 0, lenCompressed);

                        // Add to header list
//                        Debug.Assert(headData.MaxPeakIndex != -1);

                        var header = new ChromGroupHeaderInfo(chromDataSetNext.PrecursorMz,
                                                         currentFileIndex,
                                                         chromDataSetNext.Count,
                                                         _listTransitions.Count,
                                                         chromDataSetNext.CountPeaks,
                                                         _listPeaks.Count,
                                                         chromDataSetNext.MaxPeakIndex,
                                                         times.Length,
                                                         lenCompressed,
                                                         location);

                        foreach (var chromData in chromDataSetNext.Chromatograms)
                        {
                            _listTransitions.Add(new ChromTransition(chromData.Key.Product));

                            // Add to peaks list
                            foreach (var peak in chromData.Peaks)
                                _listPeaks.Add(peak);
                        }

                        _listGroups.Add(header);
                    }
                }
                catch (Exception x)
                {
                    lock (_chromDataSets)
                    {
                        _writeException = x;
                        // Make sure the reader thread can exit
                        Monitor.Pulse(_chromDataSets);
                    }
                }
            }

            private sealed class ChromDataCollector
            {
                public ChromDataCollector(double precursorMz)
                {
                    PrecursorMz = precursorMz;
                    ProductIntensityMap = new Dictionary<double, ChromCollector>();
                }

                public double PrecursorMz { get; private set; }
                public Dictionary<double, ChromCollector> ProductIntensityMap { get; private set; }

                public int TimeCount
                {
                    get
                    {
                        // Return the length of any existing time list
                        foreach (var tis in ProductIntensityMap.Values)
                            return tis.Times.Count;
                        return 0;
                    }
                }
            }

            private sealed class ChromCollector
            {
                public ChromCollector()
                {
                    Times = new List<float>();
                    Intensities = new List<float>();
                }

                public List<float> Times { get; private set; }
                public List<float> Intensities { get; private set; }
            }

            private sealed class ChromDataSet
            {
                private readonly List<ChromData> _listChromData = new List<ChromData>();
                private readonly bool _isProcessedScans;

                public ChromDataSet(bool isProcessedScans)
                {
                    _isProcessedScans = isProcessedScans;
                }

                public ChromDataSet(params ChromData[] arrayChromData)
                {
                    _listChromData.AddRange(arrayChromData);
                }

                public IEnumerable<ChromData> Chromatograms { get { return _listChromData; } }

                public int Count { get { return _listChromData.Count; } }

                public void Add(ChromData chromData)
                {
                    _listChromData.Add(chromData);
                }

                public float PrecursorMz
                {
                    get { return _listChromData.Count > 0 ? _listChromData[0].Key.Precursor : 0; }
                }

                public int CountPeaks
                {
                    get { return _listChromData.Count > 0 ? _listChromData[0].Peaks.Count : 0; }
                }

                public int MaxPeakIndex
                {
                    get { return _listChromData.Count > 0 ? _listChromData[0].MaxPeakIndex : 0; }                    
                }

                public float[] Times
                {
                    get { return _listChromData.Count > 0 ? _listChromData[0].Times : new float[0]; }
                }

                public float[][] Intensities
                {
                    get { return _listChromData.ConvertAll(data => data.Intensities).ToArray(); }
                }

                private float MinRawTime
                {
                    get
                    {
                        float min = float.MaxValue;
                        foreach (var chromData in _listChromData)
                        {
                            if (chromData.RawTimes.Length > 0)
                                min = Math.Min(min, chromData.RawTimes[0]);                            
                        }
                        return min;
                    }
                }

                private float MaxStartTime
                {
                    get
                    {
                        float max = float.MinValue;
                        foreach (var chromData in _listChromData)
                        {
                            if (chromData.RawTimes.Length > 0)
                                max = Math.Max(max, chromData.RawTimes[0]);                            
                        }
                        return max;
                    }                    
                }

                private float MaxRawTime
                {
                    get
                    {
                        float max = float.MinValue;
                        foreach (var chromData in _listChromData)
                        {
                            if (chromData.RawTimes.Length > 0)
                                max = Math.Max(max, chromData.Times[chromData.Times.Length - 1]);
                        }
                        return max;
                    }
                }

                private float MinEndTime
                {
                    get
                    {
                        float min = float.MaxValue;
                        foreach (var chromData in _listChromData)
                        {
                            if (chromData.RawTimes.Length > 0)
                                min = Math.Min(min, chromData.Times[chromData.Times.Length - 1]);                            
                        }
                        return min;
                    }                    
                }

                /// <summary>
                /// If the minimum time is greater than two cycles from the maximum start,
                /// then use the minimum, and interpolate other transitions from it.
                /// Otherwise, try to avoid zeros at the edges, since they can create
                /// change that look like a peak.
                /// </summary>
                /// <param name="interval">Interval that will be used for interpolation</param>
                /// <returns>Value to use as the start time for chromatograms that do not infer zeros</returns>
                private float GetNonZeroStart(double interval)
                {
                    float min = MinRawTime;
                    float max = MaxStartTime;
                    if (max - min > interval * 2)
                        return min;
                    return max;
                }
                
                /// <summary>
                /// If the maximum time is greater than two cycles from the minimum end,
                /// then use the maximum, and interpolate other transitions to it.
                /// Otherwise, try to avoid zeros at the edges, since they can create
                /// change that looks like a peak.
                /// </summary>
                /// <param name="interval">Interval that will be used for interpolation</param>
                /// <returns>Value to use as the end time for chromatograms that do not infer zeros</returns>
                private float GetNonZeroEnd(double interval)
                {
                    float min = MinEndTime;
                    float max = MaxRawTime;
                    if (max - min > interval * 2)
                        return max;
                    return min;
                }

                private const double NOISE_CORRELATION_THRESHOLD = 0.95;
                private const double TIME_DELTA_VARIATION_THRESHOLD = 0.001;
                private const double TIME_DELTA_MAX_RATIO_THRESHOLD = 25;
                private const int MINIMUM_PEAKS = 3;

                public void PickChromatogramPeaks()
                {
                    // Make sure chromatograms are in sorted order
                    _listChromData.Sort((c1, c2) => c1.Key.CompareTo(c2.Key));

                    // Make sure times are evenly spaced before doing any peak detection.
                    EvenlySpaceTimes();

                    // Mark all optimization chromatograms
                    MarkOptimizationData();

//                    if (Math.Round(_listChromData[0].Key.Precursor) == 1143)
//                        Console.WriteLine("Issue");

                    // First use Crawdad to find the peaks
                    _listChromData.ForEach(chromData => chromData.FindPeaks());

                    // Merge sort all peaks into a single list
                    List<ChromDataPeak> allPeaks = MergePeaks(_listChromData);

                    // Inspect 20 most intense peak regions
                    var listPeakSets = new List<ChromDataPeakList>();
                    var listRank = new List<double>();
                    for (int i = 0; i < 20; i++)
                    {
                        if (allPeaks.Count == 0)
                            break;

                        ChromDataPeak peak = allPeaks[0];
                        allPeaks.RemoveAt(0);
                        ChromDataPeakList peakSet = FindCoelutingPeaks(peak, allPeaks);
                        //                Console.WriteLine("peak {0}: {1:F01}", i + 1, peakSet.TotalArea / 1000);

                        listPeakSets.Add(peakSet);
                        listRank.Add(i);
                    }

                    if (listPeakSets.Count == 0)
                        return;

                    // Sort by product area descending
                    listPeakSets.Sort((p1, p2) => Comparer<double>.Default.Compare(p2.TotalArea, p1.TotalArea));

                    // The peak will be a signigificant spike above the norm for this
                    // data.  Find a cut-off by removing peaks until the remaining
                    // peaks correlate well in a linear regression.
                    var listAreas = listPeakSets.ConvertAll(set => set.TotalArea);
                    // Keep at least 3 peaks
                    listRank.RemoveRange(0, Math.Min(MINIMUM_PEAKS, listRank.Count));
                    listAreas.RemoveRange(0, Math.Min(MINIMUM_PEAKS, listAreas.Count));
                    int iRemove = 0;
                    // And there must be at least 5 peaks in the line to qualify for removal
                    for (int i = 0, len = listAreas.Count; i < len - 4; i++)
                    {
                        var statsRank = new Statistics(listRank.ToArray());
                        var statsArea = new Statistics(listAreas.ToArray());
                        double rvalue = statsArea.R(statsRank);
                        //                Console.WriteLine("i = {0}, r = {1}", i, rvalue);
                        if (Math.Abs(rvalue) > NOISE_CORRELATION_THRESHOLD)
                        {
                            iRemove = i + MINIMUM_PEAKS;
                            RemoveNonOverlappingPeaks(listPeakSets, iRemove);
                            break;
                        }
                        listRank.RemoveAt(0);
                        listAreas.RemoveAt(0);
                    }
                    if (iRemove == 0)
                        iRemove = listPeakSets.Count;
                    // Add small peaks under the chosen peaks, to make adding them easier
                    foreach (var peak in allPeaks)
                    {
                        if (IsOverlappingPeak(peak, listPeakSets, iRemove))
                            listPeakSets.Add(new ChromDataPeakList(peak, _listChromData));                            
                    }

                    // Sort by product score
                    listPeakSets.Sort((p1, p2) => Comparer<double>.Default.Compare(p2.ProductArea, p1.ProductArea));
                    // Pick the maximum peak by the product score
                    ChromDataPeakList peakSetMax = listPeakSets[0];

                    // Sort them back into retention time order
                    listPeakSets.Sort((l1, l2) => l1[0].Peak.StartIndex - l2[0].Peak.StartIndex);

                    // Set the processed peaks back to the chromatogram data
                    int maxPeakIndex = listPeakSets.IndexOf(peakSetMax);
                    for (int i = 0, len = listPeakSets.Count; i < len; i++)
                    {
                        foreach (var peak in listPeakSets[i])
                        {
                            // Set the max peak index on the data for each transition,
                            // but only the first time through.
                            if (i == 0)
                                peak.Data.MaxPeakIndex = maxPeakIndex;

                            if (peak.Peak == null)
                                peak.Data.Peaks.Add(ChromPeak.EMPTY);
                            else
                                peak.Data.Peaks.Add(new ChromPeak(peak.Peak, peak.Data.Times));
                        }
                    }
                }

                private List<ChromDataPeak> MergePeaks(List<ChromData> listChromData)
                {
                    List<ChromDataPeak> allPeaks = new List<ChromDataPeak>();
                    var listEnumerators = new List<IEnumerator<CrawdadPeak>>(
                        listChromData.ConvertAll(item => item.RawPeaks.GetEnumerator()));
                    // Merge with list of chrom data that will match the enumerators
                    // list, as completed enumerators are removed.
                    var listUnmerged = new List<ChromData>(_listChromData);
                    // Initialize an enumerator for each set of raw peaks, or remove
                    // the set, if the list is found to be empty
                    for (int i = listEnumerators.Count - 1; i >= 0; i--)
                    {
                        if (!listEnumerators[i].MoveNext())
                        {
                            listEnumerators.RemoveAt(i);
                            listUnmerged.RemoveAt(i);
                        }
                    }

                    while (listEnumerators.Count > 0)
                    {
                        float maxIntensity = 0;
                        int iMaxEnumerator = -1;

                        for (int i = 0; i < listEnumerators.Count; i++)
                        {
                            float intensity = listEnumerators[i].Current.Area;
                            if (intensity > maxIntensity)
                            {
                                maxIntensity = intensity;
                                iMaxEnumerator = i;
                            }
                        }

                        // If only zero area peaks left, stop looping.
                        if (iMaxEnumerator == -1)
                            break;

                        var maxData = listUnmerged[iMaxEnumerator];
                        var maxEnumerator = listEnumerators[iMaxEnumerator];
                        var maxPeak = maxEnumerator.Current;
                        Debug.Assert(maxPeak != null);
                        // Discard peaks that occur at the edge of their range.
                        // These are not useful in SRM.
                        // TODO: Fix Crawdad peak detection to make this unnecessary
                        if (maxPeak.StartIndex != maxPeak.TimeIndex && maxPeak.EndIndex != maxPeak.TimeIndex)
                            allPeaks.Add(new ChromDataPeak(maxData, maxPeak));
                        if (!maxEnumerator.MoveNext())
                        {
                            listEnumerators.RemoveAt(iMaxEnumerator);
                            listUnmerged.RemoveAt(iMaxEnumerator);
                        }
                    }
                    return allPeaks;
                }

                private static void RemoveNonOverlappingPeaks(IList<ChromDataPeakList> listPeakSets, int iRemove)
                {
                    for (int i = listPeakSets.Count - 1; i >= iRemove; i--)
                    {
                        if (!IsOverlappingPeak(listPeakSets[i][0], listPeakSets, iRemove))
                            listPeakSets.RemoveAt(i);
                    }
                }

                private static bool IsOverlappingPeak(ChromDataPeak peak,
                    IList<ChromDataPeakList> listPeakSets, int count)
                {
                    var peak1 = peak.Peak;
                    int overlapThreshold = (int)Math.Round((peak1.EndIndex - peak1.StartIndex)/2.0);
                    for (int i = 0; i < count; i++)
                    {
                        var peak2 = listPeakSets[i][0].Peak;
                        if (Math.Min(peak1.EndIndex, peak2.EndIndex) - Math.Max(peak1.StartIndex, peak2.StartIndex) >= overlapThreshold)
                            return true;
                    }
                    return false;
                }

                private void MarkOptimizationData()
                {
                    int iFirst = 0;
                    for (int i = 0; i < _listChromData.Count; i++)
                    {
                        if (i < _listChromData.Count - 1 &&
                            _listChromData[i+1].Key.Product - _listChromData[i].Key.Product < ChromatogramInfo.OPTIMIZE_SHIFT_THRESHOLD)
                        {
                            Debug.Assert(_listChromData[i + 1].Key.Product > _listChromData[i].Key.Product, "Incorrectly sorted chromatograms");
                            _listChromData[i].IsOptimizationData = true;
                        }
                        else
                        {
                            if (iFirst != i)
                            {
                                _listChromData[i].IsOptimizationData = true;
                                // The middle element in the run is the regression value.
                                // Mark it as not optimization data.
                                _listChromData[(i - iFirst)/2 + iFirst].IsOptimizationData = false;
                            }
                            // Start a new run with the next value
                            iFirst = i + 1;
                        }
                    }
                }

                private void EvenlySpaceTimes()
                {
                    // Accumulate time deltas looking for variation that violates our ability
                    // to do valid peak detection with Crawdad.
                    bool foundVariation = false;

                    List<double> listDeltas = new List<double>();
                    float[] firstTimes = null;
                    double expectedTimeDelta = 0;
                    foreach (var chromData in _listChromData)
                    {
                        if (firstTimes == null)
                        {
                            firstTimes = chromData.Times;
                            if (firstTimes.Length == 0)
                                continue;
                            expectedTimeDelta = (firstTimes[firstTimes.Length - 1] - firstTimes[0])/firstTimes.Length;
                        }
                        if (firstTimes.Length != chromData.Times.Length)
                            foundVariation = true;

                        double lastTime = 0;
                        var times = chromData.Times;
                        if (times.Length > 0)
                            lastTime = times[0];
                        for (int i = 1, len = chromData.Times.Length; i < len; i++)
                        {
                            double time = times[i];
                            double delta = time - lastTime;
                            lastTime = time;
                            listDeltas.Add(Math.Round(delta, 4));

                            if (!foundVariation && (time != firstTimes[i] ||
                                    Math.Abs(delta - expectedTimeDelta) > TIME_DELTA_VARIATION_THRESHOLD))
                            {
                                foundVariation = true;
                            }
                        }
                    }

                    // Handle a bug where the ProteoWizard Reader_Thermo returns chromatograms
                    // with alternating zero intensity scans with real data
                    if (ThermoZerosFix())
                    {
                        EvenlySpaceTimes();
                        return;
                    }
                    // Moved to ProteoWizard
//                    else if (WiffZerosFix())
//                    {
//                        EvenlySpaceTimes();
//                        return;
//                    }

                    // If time deltas are sufficiently evenly spaced, then no further processing
                    // is necessary.
                    if (!foundVariation)
                        return;

                    // Interpolate the existing points onto time intervals evently spaced
                    // by the minimum interval observed in the measuered data.
                    var statDeltas = new Statistics(listDeltas.ToArray());
                    double[] bestDeltas = statDeltas.Modes();
                    double intervalDelta;
                    if (bestDeltas.Length == 0 || bestDeltas.Length > listDeltas.Count/2)
                        intervalDelta = statDeltas.Min();
                    else if (bestDeltas.Length == 1)
                        intervalDelta = bestDeltas[0];
                    else
                    {
                        var statIntervals = new Statistics(bestDeltas);
                        intervalDelta = statIntervals.Min();
                    }

                    // Never go smaller than 1/2 a second.
//                    if (intervalDelta < 0.5/60)
//                        intervalDelta = 0.5/60;  // For breakpoint setting

                    bool inferZeros = false;
                    if (_isProcessedScans && statDeltas.Max() / intervalDelta > TIME_DELTA_MAX_RATIO_THRESHOLD)
                        inferZeros = true;  // Verbose expression for easy breakpoint placement

                    // Create the single set of time intervals that all points for
                    // this precursor will be mapped onto.
                    double start, end;
                    // If infering zeros, make sure values start and end with zero.
                    if (inferZeros)
                    {
                        start = MinRawTime - intervalDelta*2;
                        end = MaxRawTime + intervalDelta*2;                        
                    }
                    else
                    {
                        start = GetNonZeroStart(intervalDelta);
                        end = GetNonZeroEnd(intervalDelta);
                    }
                    var listTimesNew = new List<float>();
                    for (double t = start; t < end; t += intervalDelta)
                        listTimesNew.Add((float)t);
                    var timesNew = listTimesNew.ToArray();

                    // Perform interpolation onto the new times
                    foreach (var chromData in _listChromData)
                    {
                        var intensNew = new List<float>();
                        var timesMeasured = chromData.RawTimes;
                        var intensMeasured = chromData.RawIntensities;

                        int iTime = 0;
                        double timeLast = start;
                        double intenLast = (inferZeros || intensMeasured.Length == 0 ? 0 : intensMeasured[0]);
                        for (int i = 0; i < timesMeasured.Length && iTime < timesNew.Length; i++)
                        {
                            double intenNext;
                            float time = timesMeasured[i];
                            float inten = intensMeasured[i];

                            // Continue enumerating points until one is encountered
                            // that has a greater time value than the point being assigned.
                            while (i < timesMeasured.Length - 1 && time < timesNew[iTime])
                            {
                                i++;
                                time = timesMeasured[i];
                                inten = intensMeasured[i];
                            }

                            if (i >= timesMeasured.Length)
                                break;

                            // If the next measured intensity is more than the new delta
                            // away from the intensity being assigned, then interpolated
                            // the next point toward zero, and set the last intensity to
                            // zero.
                            if (inferZeros && intenLast > 0 && timesNew[iTime] + intervalDelta < time)
                            {
                                intenNext = intenLast + (timesNew[iTime] - timeLast) * (0 - intenLast) / (timesNew[iTime] + intervalDelta - timeLast);
                                intensNew.Add((float)intenNext);
                                timeLast = timesNew[iTime++];
                                intenLast = 0;
                            }

                            if (inferZeros)
                            {
                                // If the last intensity was zero, and the next measured time
                                // is more than a delta away, assign zeros until within a
                                // delta of the measured intensity.
                                while (intenLast == 0 && iTime < timesNew.Length && timesNew[iTime] + intervalDelta < time)
                                {
                                    intensNew.Add(0);
                                    timeLast = timesNew[iTime++];
                                }
                            }
                            else
                            {
                                // Up to just before the current point, project the line from the
                                // last point to the current point at each interval.
                                while (iTime < timesNew.Length && timesNew[iTime] + intervalDelta < time)
                                {
                                    intenNext = intenLast + (timesNew[iTime] - timeLast) * (inten - intenLast) / (time - timeLast);
                                    intensNew.Add((float)intenNext);
                                    iTime++;
                                }
                            }

                            if (iTime >= timesNew.Length)
                                break;

                            // Interpolate from the last intensity toward the measured
                            // intenisty now within a delta of the point being assigned.
                            if (time == timeLast)
                                intenNext = intenLast;
                            else
                                intenNext = intenLast + (timesNew[iTime] - timeLast) * (inten - intenLast) / (time - timeLast);
                            intensNew.Add((float)intenNext);
                            iTime++;
                            intenLast = inten;
                            timeLast = time;
                        }

                        // Fill any unassigned intensities with zeros.
                        while (intensNew.Count < timesNew.Length)
                            intensNew.Add(0);

                        // Reassign times and intensities.
                        chromData.Times = timesNew;
                        chromData.Intensities = intensNew.ToArray();
                    }
                }

                private bool WiffZerosFix()
                {
                    if (!HasFlankingZeros)
                        return false;

                    // Remove flagging zeros
                    foreach (var chromData in _listChromData)
                    {
                        var times = chromData.Times;
                        var intensities = chromData.Intensities;
                        int start = 0;
                        while (start < intensities.Length - 1 && intensities[start] == 0)
                            start++;
                        int end = intensities.Length;
                        while (end > 0 && intensities[end - 1] == 0)
                            end--;

                        // Leave at least one bounding zero
                        if (start > 0)
                            start--;
                        if (end < intensities.Length)
                            end++;

                        var timesNew = new float[end - start];
                        var intensitiesNew = new float[end - start];
                        Array.Copy(times, start, timesNew, 0, timesNew.Length);
                        Array.Copy(intensities, start, intensitiesNew, 0, intensitiesNew.Length);
                        chromData.FixChromatogram(timesNew, intensitiesNew);
                    }
                    return true;
                }

                private bool HasFlankingZeros
                {
                    get
                    {
                        // Check for case where all chromatograms have at least
                        // 10 zero intensity entries on either side of the real data.
                        foreach (var chromData in _listChromData)
                        {
                            var intensities = chromData.Intensities;
                            if (intensities.Length < 10)
                                return false;
                            for (int i = 0; i < 10; i++)
                            {
                                if (intensities[i] != 0)
                                    return false;
                            }
                            for (int i = intensities.Length - 1; i < 10; i++)
                            {
                                if (intensities[i] != 0)
                                    return false;                                
                            }
                        }
                        return true;
                    }
                }

                private bool ThermoZerosFix()
                {
                    // Check for interleaving zeros
                    if (!HasThermZerosBug)
                        return false;
                    // Remove interleaving zeros
                    foreach (var chromData in _listChromData)
                    {
                        var times = chromData.Times;
                        var intensities = chromData.Intensities;
                        var timesNew = new float[intensities.Length/2];
                        var intensitiesNew = new float[intensities.Length/2];
                        for (int i = (intensities.Length > 0 && intensities[0] == 0 ? 1 : 0), iNew = 0; iNew < timesNew.Length; i += 2, iNew++)
                        {
                            timesNew[iNew] = times[i];
                            intensitiesNew[iNew] = intensities[i];
                        }
                        chromData.FixChromatogram(timesNew, intensitiesNew);
                    }
                    return true;
                }

                private bool HasThermZerosBug
                {
                    get
                    {
                        // Check for interleaving zeros
                        foreach (var chromData in _listChromData)
                        {
                            var intensities = chromData.Intensities;
                            for (int i = (intensities.Length > 0 && intensities[0] == 0 ? 0 : 1); i < intensities.Length; i += 2)
                            {
                                if (intensities[i] != 0)
                                    return false;
                            }
                        }
                        return true;
                    }
                }

                private ChromDataPeakList FindCoelutingPeaks(ChromDataPeak dataPeakMax,
                    IList<ChromDataPeak> allPeaks)
                {
                    CrawdadPeak peakMax = dataPeakMax.Peak;
                    float areaMax = peakMax.Area;
                    int centerMax = peakMax.TimeIndex;
                    int startMax = peakMax.StartIndex;
                    int endMax = peakMax.EndIndex;
                    int widthMax = peakMax.Length;
                    int deltaMax = (int)Math.Round(widthMax / 4.0, 0);
                    var listPeaks = new ChromDataPeakList(dataPeakMax);
                    foreach (var chromData in _listChromData)
                    {
                        if (ReferenceEquals(chromData, dataPeakMax.Data))
                            continue;

                        int iPeakNearest = -1;
                        int deltaNearest = deltaMax;

                        // Find nearest peak in remaining set that is less than 1/4 length
                        // from the primary peak's center
                        for (int i = 0, len = allPeaks.Count; i < len; i++)
                        {
                            var peak = allPeaks[i];
                            if (!ReferenceEquals(peak.Data, chromData))
                                continue;

                            // Exclude peaks where the apex is not inside the max peak,
                            // or apex is at one end of the peak
                            int timeIndex = peak.Peak.TimeIndex;
                            int startPeak = peak.Peak.StartIndex;
                            int endPeak = peak.Peak.EndIndex;
                            if (startMax >= timeIndex || timeIndex >= endMax ||
                                    startPeak == timeIndex || timeIndex == endPeak)
                                continue;
                            // or peak area is less than 1% of max peak area
                            if (peak.Peak.Area * 100 < areaMax)
                                continue;
                            // or when FWHM is very narrow, usually a good indicator of noise
                            if (/* peak.Peak.Fwhm < 1.2 too agressive || */ peak.Peak.Fwhm * 12 < widthMax)
                                continue;
                            // or where the peak does not overlap at least 50% of the max peak
                            int intersect = Math.Min(endMax, peak.Peak.EndIndex) -
                                            Math.Max(startMax, peak.Peak.StartIndex) + 1;   // +1 for inclusive end
                            int lenPeak = peak.Peak.Length;
                            // Allow 25% coverage, if the peak is entirely inside the max, since
                            // sometimes Crawdad breaks smaller peaks up.
                            int factor = (intersect == lenPeak ? 4 : 2);
                            if (intersect * factor < widthMax)
                                continue;
                            // If less than 2/3 of the peak is inside the max peak.
                            if (intersect * 3/2 < lenPeak)

                            // or where either end is more than 25% of its peak width outside
                            // the max peak.
                            if (intersect != lenPeak)
                            {
                                factor = (intersect == widthMax ? 2 : 4);
                                if ((peak.Peak.StartIndex - startMax) * factor > lenPeak ||
                                        (endMax - peak.Peak.EndIndex) * factor > lenPeak)
                                    continue;
                            }

                            int delta = Math.Abs(timeIndex - centerMax);
                            if (delta <= deltaNearest)
                            {
                                deltaNearest = delta;
                                iPeakNearest = i;
                            }
                        }

                        if (iPeakNearest == -1)
                            listPeaks.Add(new ChromDataPeak(chromData, null));
                        else
                        {
                            listPeaks.Add(new ChromDataPeak(chromData, allPeaks[iPeakNearest].Peak));
                            allPeaks.RemoveAt(iPeakNearest);
                        }
                    }
                    return listPeaks;
                }
            }

            private sealed class ChromData
            {
                /// <summary>
                /// Maximum number of peaks to label on a graph
                /// </summary>
                private const int MAX_PEAKS = 20;

                public ChromData(ChromKey key, float[] times, float[] intensities)
                {
                    Key = key;
                    RawTimes = Times = times;
                    RawIntensities = Intensities = intensities;
                    Peaks = new List<ChromPeak>();
                    MaxPeakIndex = -1;
                }

                public void FindPeaks()
                {
                    CrawdadPeakFinder finder = new CrawdadPeakFinder();
                    finder.SetChromatogram(Times, Intensities);
                    RawPeaks = finder.CalcPeaks(MAX_PEAKS);                    
                }

                public ChromKey Key { get; private set; }
                public float[] RawTimes { get; private set; }
                public float[] RawIntensities { get; private set; }
                public IEnumerable<CrawdadPeak> RawPeaks { get; private set; }

                public float[] Times { get; set; }
                public float[] Intensities { get; set; }
                public IList<ChromPeak> Peaks { get; private set; }
                public int MaxPeakIndex { get; set; }
                public bool IsOptimizationData { get; set; }

                public void FixChromatogram(float[] timesNew, float[] intensitiesNew)
                {
                    RawTimes = Times = timesNew;
                    RawIntensities = Intensities = intensitiesNew;
                }
            }

            private sealed class ChromDataPeak
            {
                public ChromDataPeak(ChromData data, CrawdadPeak peak)
                {
                    Data = data;
                    Peak = peak;
                }

                public ChromData Data { get; private set; }
                public CrawdadPeak Peak { get; private set; }

                public override string ToString()
                {
                    return Peak == null ? Data.Key.ToString() :
                        string.Format("{0} - area = {1:F0}, start = {2}, end = {3}",
                            Data.Key, Peak.Area, Peak.StartIndex, Peak.EndIndex);
                }
            }

            private sealed class ChromDataPeakList : Collection<ChromDataPeak>
            {
                public ChromDataPeakList(ChromDataPeak peak)
                {
                    Add(peak);
                }

                public ChromDataPeakList(ChromDataPeak peak, IEnumerable<ChromData> listChromData)
                    : this(peak)
                {
                    foreach (var chromData in listChromData)
                    {
                        if (!ReferenceEquals(chromData, peak.Data))
                            Add(new ChromDataPeak(chromData, null));
                    }
                }

                private int PeakCount { get; set; }
                public double TotalArea { get; private set; }
                public double ProductArea { get; private set; }

                private void AddPeak(ChromDataPeak dataPeak)
                {
                    // Avoid using optimization data in scoring
                    if (dataPeak.Peak != null && !dataPeak.Data.IsOptimizationData)
                    {
                        double area = dataPeak.Peak.Area;
                        if (PeakCount == 0)
                            TotalArea = area;
                        else
                            TotalArea += area;
                        PeakCount++;

                        ProductArea = TotalArea*Math.Pow(10.0, PeakCount);
                    }
                }

                private void SubtractPeak(ChromDataPeak dataPeak)
                {
                    // Avoid using optimization data in scoring
                    if (dataPeak.Peak != null && !dataPeak.Data.IsOptimizationData)
                    {
                        double area = dataPeak.Peak.Area;
                        PeakCount--;
                        if (PeakCount == 0)
                            TotalArea = 0;
                        else
                            TotalArea -= area;

                        ProductArea = TotalArea * Math.Pow(10.0, PeakCount);
                    }
                }

                protected override void ClearItems()
                {
                    PeakCount = 0;
                    TotalArea = 0;
                    ProductArea = 0;

                    base.ClearItems();
                }

                protected override void InsertItem(int index, ChromDataPeak item)
                {
                    AddPeak(item);
                    base.InsertItem(index, item);
                }

                protected override void RemoveItem(int index)
                {
                    SubtractPeak(this[index]);
                    base.RemoveItem(index);
                }

                protected override void SetItem(int index, ChromDataPeak item)
                {
                    SubtractPeak(this[index]);
                    AddPeak(item);
                    base.SetItem(index, item);
                }
            }
        }
    }
}
