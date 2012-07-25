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
using System.Linq;
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public sealed class ChromatogramCache : Immutable, IDisposable
    {
        public const int FORMAT_VERSION_CACHE = 4;
        public const int FORMAT_VERSION_CACHE_3 = 3;
        public const int FORMAT_VERSION_CACHE_2 = 2;

        public const string EXT = ".skyd";

        /// <summary>
        /// Construct path to a final data cache from the document path.
        /// </summary>
        /// <param name="documentPath">Path to saved document</param>
        /// <param name="name">Name of data cache</param>
        /// <returns>A path to the data cache</returns>
        public static string FinalPathForName(string documentPath, string name)
        {
            string documentDir = Path.GetDirectoryName(documentPath) ?? "";
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
            string dirData = Path.GetDirectoryName(filePath);
            string dirDocument = Path.GetDirectoryName(documentPath) ?? "";

            // Start with the file basename
            StringBuilder sbName = new StringBuilder(Path.GetFileNameWithoutExtension(filePath));
            // If the data file is not in the same directory as the document, add a checksum
            // of the data directory.
            if (!Equals(dirData, dirDocument))
                sbName.Append('_').Append(AdlerChecksum.MakeForString(dirData));
            // If it has a sample name, append the index to differentiate this name from
            // the other samples in the multi-sample file
            if (SampleHelp.HasSamplePart(dataFilePath))
                sbName.Append('_').Append(SampleHelp.GetPathSampleIndexPart(dataFilePath));
            if (name != null)
                sbName.Append('_').Append(name);
            // Append the extension to differentiate between different file types (.mzML, .mzXML)
            sbName.Append(Path.GetExtension(filePath));
            sbName.Append(EXT);

            return Path.Combine(dirDocument, sbName.ToString());
        }

        private readonly ReadOnlyCollection<ChromCachedFile> _cachedFiles;
        // ReadOnlyCollection is not fast enough for use with these arrays
        private readonly ChromGroupHeaderInfo[] _chromatogramEntries;
        private readonly ChromTransition[] _chromTransitions;
        private readonly ChromPeak[] _chromatogramPeaks;

        public ChromatogramCache(string cachePath,
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

        /// <summary>
        /// True if cache version is acceptable for current use.
        /// </summary>
        public bool IsSupportedVersion
        {
            get
            {
                return (Version >= FORMAT_VERSION_CACHE_2);
            }
        }

        public bool IsCurrentVersion
        {
            get
            {
                return IsVersionCurrent(Version);
            }
        }

        public static bool IsVersionCurrent(int version)
        {
            return (version == FORMAT_VERSION_CACHE ||
                    version == FORMAT_VERSION_CACHE_3);
        }

        public bool IsCurrentDisk
        {
            get { return CachedFiles.IndexOf(cachedFile => !cachedFile.IsCurrent) == -1; }
        }

        public ChromTransition GetTransition(int index)
        {
            return _chromTransitions[index];
        }

        public ChromPeak GetPeak(int index)
        {
            return _chromatogramPeaks[index];
        }

        /// <summary>
        /// Returns true if the cached file paths in this cache are completely covered
        /// by an existing set of caches.
        /// </summary>
        /// <param name="caches">Existing caches to check for paths in this cache that are missing</param>
        /// <returns>True if all paths in this cache are covered</returns>
        public bool IsCovered(IEnumerable<ChromatogramCache> caches)
        {
            // True if there are not any paths that are not covered
            return CachedFilePaths.All(path => IsCovered(path, caches));
        }

        /// <summary>
        /// Returns true, if a single path can be found in a set of caches.
        /// </summary>
        private static bool IsCovered(string path, IEnumerable<ChromatogramCache> caches)
        {
            return caches.Any(cache => cache.CachedFilePaths.Contains(path));
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
        public ChromatogramGroupInfo LoadChromatogramInfo(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            return new ChromatogramGroupInfo(chromGroupHeaderInfo, _cachedFiles, _chromTransitions, _chromatogramPeaks);
        }

        public int Count
        {
            get { return _chromatogramEntries.Length; }
        }

        public IEnumerable<ChromGroupHeaderInfo> ChromGroupHeaderInfos
        {
            get { return Array.AsReadOnly(_chromatogramEntries); }
        }

        private ChromatogramCache ChangeCachePath(string prop)
        {
            return ChangeProp(ImClone(this), im => im.CachePath = prop);
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
            if (compare > 0)
                return FindEntry(precursorMz, tolerance, mid + 1, right);
            
            // Scan backward until the first matching element is found.
            while (mid > 0 && MatchMz(precursorMz, tolerance, _chromatogramEntries[mid - 1].Precursor))
                mid--;

            return mid;
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
            // Version 3 file header addition
            runstart_lo,
            runstart_hi,
            // Version 4 file header addition
            len_instrument_info,

            count,
            count2 = runstart_lo,
			count3 = len_instrument_info,
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
            try
            {
                var joiner = new ChromCacheJoiner(cachePath, streamDest, listCachePaths, loader, status, complete);
                joiner.JoinParts();
            }
            catch (Exception x)
            {
                complete(null, x);
            }
        }

        public static void Build(SrmDocument document, string cachePath, IList<string> listResultPaths,
            ProgressStatus status, ILoadMonitor loader, Action<ChromatogramCache, Exception> complete)
        {
            try
            {
                var builder = new ChromCacheBuilder(document, cachePath, listResultPaths, loader, status, complete);
                builder.BuildCache();
            }
            catch (Exception x)
            {
                complete(null, x);
            }
        }

        public static long LoadStructs(Stream stream,
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
            if (formatVersion < FORMAT_VERSION_CACHE_2)
            {
                return EmptyCache(out chromCacheFiles, out chromatogramEntries, out chromTransitions, out chromatogramPeaks);
            }

            int numPeaks = GetInt32(cacheHeader, (int)Header.num_peaks);
            long locationPeaks = BitConverter.ToInt64(cacheHeader, ((int)Header.location_peaks_lo) * 4);
            int numChrom = GetInt32(cacheHeader, (int)Header.num_chromatograms);
            long locationHeaders = BitConverter.ToInt64(cacheHeader, ((int)Header.location_headers_lo) * 4);
            int numTrans = GetInt32(cacheHeader, (int)Header.num_transitions);
            long locationTrans = BitConverter.ToInt64(cacheHeader, ((int)Header.location_trans_lo) * 4);
            int numFiles = GetInt32(cacheHeader, (int)Header.num_files);
            long locationFiles = BitConverter.ToInt64(cacheHeader, ((int)Header.location_files_lo) * 4);

            // Unexpected empty cache.  Return values that will force it to be completely rebuild.
            if (numFiles == 0)
            {
                return EmptyCache(out chromCacheFiles, out chromatogramEntries, out chromTransitions, out chromatogramPeaks);
            }

            // Read list of files cached
            stream.Seek(locationFiles, SeekOrigin.Begin);
            chromCacheFiles = new ChromCachedFile[numFiles];
            var countFileHeader = GetFileHeaderCount(formatVersion);

            byte[] fileHeader = new byte[countFileHeader];
            byte[] filePathBuffer = new byte[1024];
            for (int i = 0; i < numFiles; i++)
            {
                ReadComplete(stream, fileHeader, countFileHeader);
                long modifiedBinary = BitConverter.ToInt64(fileHeader, ((int)FileHeader.modified_lo) * 4);
                int lenPath = GetInt32(fileHeader, (int)FileHeader.len_path);
                ReadComplete(stream, filePathBuffer, lenPath);
				long runstartBinary = (IsVersionCurrent(formatVersion)
                                           ? BitConverter.ToInt64(fileHeader, ((int) FileHeader.runstart_lo)*4)
                                           : 0);
                string filePath = Encoding.Default.GetString(filePathBuffer, 0, lenPath);

                string instrumentInfoStr = null;
                if (formatVersion > FORMAT_VERSION_CACHE_3)
                {
                    int lenInstrumentInfo = GetInt32(fileHeader, (int) FileHeader.len_instrument_info);
                    byte[] instrumentInfoBuffer = new byte[lenInstrumentInfo];
                    ReadComplete(stream, instrumentInfoBuffer, lenInstrumentInfo);
                    instrumentInfoStr = Encoding.UTF8.GetString(instrumentInfoBuffer, 0, lenInstrumentInfo);
                }

                DateTime modifiedTime = DateTime.FromBinary(modifiedBinary);
                DateTime? runstartTime = runstartBinary != 0 ? DateTime.FromBinary(runstartBinary) : (DateTime?) null;
                var instrumentInfoList = InstrumentInfoUtil.GetInstrumentInfo(instrumentInfoStr);
                chromCacheFiles[i] = new ChromCachedFile(filePath, modifiedTime, runstartTime, instrumentInfoList);
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

        private static int GetFileHeaderCount(int formatVersion)
        {
            switch (formatVersion)
            {
                case (FORMAT_VERSION_CACHE):
                    return (int) (FileHeader.count)*4;
                case (FORMAT_VERSION_CACHE_3):
                    return (int) (FileHeader.count3)*4;
                default:
                    return (int) (FileHeader.count2)*4;
            }
        }

        private static long EmptyCache(out ChromCachedFile[] chromCacheFiles,
                                       out ChromGroupHeaderInfo[] chromatogramEntries,
                                       out ChromTransition[] chromTransitions,
                                       out ChromPeak[] chromatogramPeaks)
        {
            chromCacheFiles = new ChromCachedFile[0];
            chromatogramEntries = new ChromGroupHeaderInfo[0];
            chromTransitions = new ChromTransition[0];
            chromatogramPeaks = new ChromPeak[0];
            return 0;
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

        public static void WriteStructs(Stream outStream,
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
                long lastPeak = info.StartPeakIndex + info.NumPeaks*info.NumTransitions;
                if (lastPeak > chromatogramPeaks.Count)
                    throw new InvalidDataException(string.Format("Failure writing cache.  Specified {0} peaks exceed total peak count {1}", lastPeak, chromatogramPeaks.Count));
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
                // Version 3 write modified time
                var runStartTime = cachedFile.RunStartTime;
                time = (runStartTime.HasValue ? runStartTime.Value.ToBinary() : 0);
                outStream.Write(BitConverter.GetBytes(time), 0, sizeof(long));

                // Version 4 write instrument information
                string instrumentInfo = InstrumentInfoUtil.GetInstrumentInfoString(cachedFile.InstrumentInfoList);
                int instrumentInfoLen = Encoding.UTF8.GetByteCount(instrumentInfo);
                byte[] instrumentInfoBuffer = new byte[instrumentInfoLen];
                Encoding.UTF8.GetBytes(instrumentInfo, 0, instrumentInfo.Length, instrumentInfoBuffer, 0);
                outStream.Write(BitConverter.GetBytes(instrumentInfoLen), 0, sizeof(int));

                outStream.Write(pathBuffer, 0, len);
                outStream.Write(instrumentInfoBuffer, 0, instrumentInfoLen);
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

            int sizeArray = sizeof(float)*numPoints;
            Debug.Assert(sizeArray == Buffer.ByteLength(times));
            Buffer.BlockCopy(peaks, 0, times, 0, sizeArray);
            for (int i = 0, offsetTran = sizeArray; i < numTrans; i++, offsetTran += sizeArray)
            {
                intensities[i] = new float[numPoints];
                Debug.Assert(sizeArray == Buffer.ByteLength(intensities[i]));
                Buffer.BlockCopy(peaks, offsetTran, intensities[i], 0, sizeArray);
            }
        }

        public static byte[] TimeIntensitiesToBytes(float[] times, float[][] intensities)
        {
            int len = times.Length;
            int countChroms = intensities.Length;
            int sizeArray = len * sizeof(float);
            byte[] points = new byte[sizeArray * (countChroms + 1)];

            Debug.Assert(sizeArray == Buffer.ByteLength(times));
            Buffer.BlockCopy(times, 0, points, 0, sizeArray);
            for (int i = 0, offsetTran = sizeArray; i < countChroms; i++, offsetTran += sizeArray)
            {
                Debug.Assert(sizeArray == Buffer.ByteLength(intensities[i]));
                Buffer.BlockCopy(intensities[i], 0, points, offsetTran, sizeArray);
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
                // Copy the cache, if moving to a new location
                using (FileSaver fs = new FileSaver(cachePathOpt))
                {
                    File.Copy(CachePath, fs.SafeName, true);
                    fs.Commit(ReadStream);                    
                }
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

                CommitCache(fs);
            }

            return new ChromatogramCache(cachePathOpt,
                                         FORMAT_VERSION_CACHE,
                                         listKeepCachedFiles.ToArray(),
                                         listKeepEntries.ToArray(),
                                         listKeepTransitions.ToArray(),
                                         listKeepPeaks.ToArray(),
                                         // Create a new read stream, for the newly created file
                                         streamManager.CreatePooledStream(cachePathOpt, false));
        }

        public void CommitCache(FileSaver fs)
        {
            // Close the read stream, in case the destination is the source, and
            // overwrite is necessary.
            ReadStream.CloseStream();
            fs.Commit(ReadStream);
        }

        public class PathEqualityComparer : IEqualityComparer<ChromatogramCache>
        {
            public bool Equals(ChromatogramCache x, ChromatogramCache y)
            {
                return Equals(x.CachePath, y.CachePath);
            }

            public int GetHashCode(ChromatogramCache obj)
            {
                return obj.CachePath.GetHashCode();
            }
        }

        public static PathEqualityComparer PathComparer { get; private set; }

        static ChromatogramCache()
        {
            PathComparer = new PathEqualityComparer();
        }
    }
}
