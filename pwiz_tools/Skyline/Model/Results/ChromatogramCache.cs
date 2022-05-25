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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    public sealed class ChromatogramCache : Immutable, IDisposable
    {
        public const CacheFormatVersion FORMAT_VERSION_CACHE_11 = CacheFormatVersion.Eleven; // Adds chromatogram start, stop times, and uncompressed size info, and new flag bit for SignedMz
        public const CacheFormatVersion FORMAT_VERSION_CACHE_10 = CacheFormatVersion.Ten; // Introduces waters lockmass correction in MSDataFileUri syntax
        public const CacheFormatVersion FORMAT_VERSION_CACHE_9 = CacheFormatVersion.Nine; // Introduces abbreviated scan ids
        public const CacheFormatVersion FORMAT_VERSION_CACHE_8 = CacheFormatVersion.Eight; // Introduces ion mobility data
        public const CacheFormatVersion FORMAT_VERSION_CACHE_7 = CacheFormatVersion.Seven; // Introduces UTF8 character support
        public const CacheFormatVersion FORMAT_VERSION_CACHE_6 = CacheFormatVersion.Six;
        public const CacheFormatVersion FORMAT_VERSION_CACHE_5 = CacheFormatVersion.Five;
        public const CacheFormatVersion FORMAT_VERSION_CACHE_4 = CacheFormatVersion.Four;
        public const CacheFormatVersion FORMAT_VERSION_CACHE_3 = CacheFormatVersion.Three;
        public const CacheFormatVersion FORMAT_VERSION_CACHE_2 = CacheFormatVersion.Two;

        public const string EXT = ".skyd";
        public const string PEAKS_EXT = ".peaks";
        public const string SCANS_EXT = ".scans";
        public const string SCORES_EXT = ".scores";
        public const int SCORE_VALUE_SIZE = sizeof(float);

        public static CacheFormatVersion FORMAT_VERSION_CACHE
        {
            get { return CacheFormatVersion.CURRENT; }
        }

        // Set default block size for scores BlockedArray<float>
        public const int DEFAULT_SCORES_BLOCK_SIZE = 100*1024*1024;  // 100 megabytes

        /// <summary>
        /// Construct path to a final data cache from the document path.
        /// </summary>
        /// <param name="documentPath">Path to saved document</param>
        /// <param name="name">Name of data cache</param>
        /// <returns>A path to the data cache</returns>
        public static string FinalPathForName(string documentPath, string name)
        {
            string documentDir = Path.GetDirectoryName(documentPath) ?? string.Empty;
            string modifier = (name != null ? '_' + name : string.Empty);
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
        public static string PartPathForName(string documentPath, MsDataFileUri dataFilePath, string name = null)
        {
            string dirDocument = Path.GetDirectoryName(documentPath) ?? string.Empty;

            // Start with the file basename
            StringBuilder sbName = new StringBuilder(dataFilePath.GetFileNameWithoutExtension());
            // If the data file is not in the same directory as the document, add a checksum
            // of the data directory.
            var msDataFilePath = dataFilePath as MsDataFilePath;
            if (msDataFilePath != null)
            {
                string dirData = Path.GetDirectoryName(msDataFilePath.FilePath);
                // Perhaps one of these hasn't a path at all - are both in the current working directory?
                string fullDocDirPath = String.IsNullOrEmpty(dirDocument) ? Directory.GetCurrentDirectory() : Path.GetFullPath(dirDocument);
                string fullFileDirPath = String.IsNullOrEmpty(dirData) ? Directory.GetCurrentDirectory() : Path.GetFullPath(dirData);
                if (!Equals(fullDocDirPath, fullFileDirPath))
                    sbName.Append('_').Append(AdlerChecksum.MakeForString(fullFileDirPath));
            }
            // If it has a sample name, append the index to differentiate this name from
            // the other samples in the multi-sample file
            if (null != dataFilePath.GetSampleName())
                sbName.Append('_').Append(dataFilePath.GetSampleIndex());
            if (name != null)
                sbName.Append('_').Append(name);
            // Append the extension to differentiate between different file types (.mzML, .mzXML)
            sbName.Append(dataFilePath.GetExtension());
            sbName.Append(EXT);

            return Path.Combine(dirDocument, sbName.ToString());
        }

        private RawData _rawData;
        // ReadOnlyCollection is not fast enough for use with these arrays
        private readonly LibKeyMap<int[]> _chromEntryIndex;
        private readonly FeatureNames _scoreTypeIndices;

        public ChromatogramCache(string cachePath, RawData raw, IPooledStream readStream)
        {
            CachePath = cachePath;
            _rawData = raw;
            _scoreTypeIndices = raw.ScoreTypes;
            ReadStream = readStream;
            _chromEntryIndex = MakeChromEntryIndex();
        }

        public string CachePath { get; private set; }
        public CacheFormatVersion Version
        {
            get { return _rawData.FormatVersion; }
        }
        public IList<ChromCachedFile> CachedFiles { get { return _rawData.ChromCacheFiles; } }
        public IPooledStream ReadStream { get; private set; }

        public IEnumerable<MsDataFileUri> CachedFilePaths
        {
            get { return CachedFiles.Select(cachedFile => cachedFile.FilePath.GetLocation()); } // Strip any "?combine_ims=true" etc decoration
        }

        /// <summary>
        /// In order enumeration of score types
        /// </summary>
        public FeatureNames ScoreTypes
        {
            get { return _scoreTypeIndices; }
        }

        public int ScoreTypesCount
        {
            get { return _scoreTypeIndices.Count; }
        }

        /// <summary>
        /// True if cache version is acceptable for current use.
        /// </summary>
        public bool IsSupportedVersion
        {
            get { return (Version >= FORMAT_VERSION_CACHE_2); }
        }

        public bool IsCurrentVersion
        {
            get { return IsVersionCurrent(Version); }
        }

        public static bool IsVersionCurrent(CacheFormatVersion version)
        {
            return (version >= FORMAT_VERSION_CACHE_3 && FORMAT_VERSION_CACHE >= version);
        }

        public bool IsCurrentDisk
        {
            get { return CachedFiles.IndexOf(cachedFile => !cachedFile.IsCurrent) == -1; }
        }

        public ChromTransition GetTransition(int index)
        {
            return _rawData.ChromTransitions[index];
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
        private static bool IsCovered(MsDataFileUri path, IEnumerable<ChromatogramCache> caches)
        {
            return caches.Any(cache => cache.CachedFilePaths.Contains(path.GetLocation())); // Strip any "?combine_ims=true" etc decoration
        }

        public MsDataFileScanIds LoadMSDataFileScanIds(int fileIndex)
        {
            return MsDataFileScanIds.FromBytes(LoadMSDataFileScanIdBytes(fileIndex));
        }

        public byte[] LoadMSDataFileScanIdBytes(int fileIndex)
        {
            var cachedFile = CachedFiles[fileIndex];
            if (cachedFile.SizeScanIds == 0)
            {
                return Array.Empty<byte>();
            }
            return CallWithStream(stream =>
            {
                byte[] scanIdBytes = new byte[cachedFile.SizeScanIds];
                stream.Seek(_rawData.LocationScanIds + cachedFile.LocationScanIds, SeekOrigin.Begin);

                // Single read to get all the points
                if (stream.Read(scanIdBytes, 0, scanIdBytes.Length) < scanIdBytes.Length)
                    throw new IOException(Resources
                        .ChromatogramCache_LoadScanIdBytes_Failure_trying_to_read_scan_IDs);
                return scanIdBytes;
            });
        }

        public IEnumerable<ChromatogramGroupInfo> LoadChromatogramInfos(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup,
            float tolerance, ChromatogramSet chromatograms)
        {
            var precursorMz = nodeGroup != null ? nodeGroup.PrecursorMz : SignedMz.ZERO;
            double? explicitRT = null;
            if (nodePep != null && nodePep.ExplicitRetentionTime != null)
            {
                explicitRT = nodePep.ExplicitRetentionTime.RetentionTime;
            }

            return GetHeaderInfos(nodePep, precursorMz, explicitRT, tolerance, chromatograms);
        }

        public bool HasAllIonsChromatograms
        {
            get
            {
                return LoadChromatogramInfos(null, null, 0, null).Any();
            }
        }

        public IEnumerable<ChromatogramGroupInfo> LoadAllIonsChromatogramInfo(ChromExtractor extractor, ChromatogramSet chromatograms)
        {
            return LoadChromatogramInfos(null, null, 0, chromatograms)
                .Where(groupInfo => groupInfo.Header.Extractor == extractor);
        }

        public ChromatogramGroupInfo LoadChromatogramInfo(int index)
        {
            return LoadChromatogramInfo(ChromGroupHeaderInfos[index]);
        }

        public ChromatogramGroupInfo LoadChromatogramInfo(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            return new ChromatogramGroupInfo(chromGroupHeaderInfo,
                                             _scoreTypeIndices,
                                             _rawData.TextIdBytes,
                                             _rawData.ChromCacheFiles,
                                             _rawData.ChromTransitions,
                                             this);
        }

        public IReadOnlyList<ChromGroupHeaderInfo> ChromGroupHeaderInfos
        {
            get { return _rawData.ChromatogramEntries; }
        }

        private ChromatogramCache ChangeCachePath(string prop, IStreamManager manager)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.CachePath = prop;
                im.ReadStream.CloseStream();
                im.ReadStream = manager.CreatePooledStream(prop,false);
            });
        }        

        public void Dispose()
        {
            ReadStream.CloseStream();
        }

        private IEnumerable<ChromatogramGroupInfo> GetHeaderInfos(PeptideDocNode nodePep, SignedMz precursorMz, double? explicitRT, float tolerance,
            ChromatogramSet chromatograms)
        {
            foreach (int i in ChromatogramIndexesMatching(nodePep, precursorMz, tolerance, chromatograms))
            {
                var entry = ChromGroupHeaderInfos[i];
                // If explicit retention time info is available, use that to discard obvious mismatches
                if (!explicitRT.HasValue || // No explicit RT
                    !entry.StartTime.HasValue || // No time data loaded yet
                    (entry.StartTime <= explicitRT && explicitRT <= entry.EndTime))
                    // No overlap
                {
                    yield return LoadChromatogramInfo(entry);
                }
            }
        }

        public IEnumerable<int> ChromatogramIndexesMatching(PeptideDocNode nodePep, SignedMz precursorMz,
            float tolerance, ChromatogramSet chromatograms)
        {
            if (nodePep != null && nodePep.IsProteomic && _chromEntryIndex != null)
            {
                bool anyFound = false;
                var key = new LibKey(nodePep.ModifiedTarget, Adduct.EMPTY).LibraryKey;
                foreach (var chromatogramIndex in _chromEntryIndex.ItemsMatching(key, false).SelectMany(list=>list))
                {
                    var entry = ChromGroupHeaderInfos[chromatogramIndex];
                    if (!MatchMz(precursorMz, entry.Precursor, tolerance))
                    {
                        continue;
                    }
                    if (chromatograms != null &&
                        !chromatograms.ContainsFile(_rawData.ChromCacheFiles[entry.FileIndex]
                            .FilePath))
                    {
                        continue;
                    }
                    anyFound = true;
                    yield return chromatogramIndex;
                }
                if (anyFound)
                {
                    yield break;
                }
            }
            int i = FindEntry(precursorMz, tolerance);
            if (i < 0)
            {
                yield break;
            }
            for (; i < ChromGroupHeaderInfos.Count; i++)
            {
                var entry = ChromGroupHeaderInfos[i];
                if (!MatchMz(precursorMz, entry.Precursor, tolerance))
                    break;
                if (chromatograms != null &&
                    !chromatograms.ContainsFile(_rawData.ChromCacheFiles[entry.FileIndex]
                        .FilePath))
                {
                    continue;
                }

                if (nodePep != null && !TextIdEqual(entry, nodePep))
                    continue;
                yield return i;
            }
        }

        private bool TextIdEqual(ChromGroupHeaderInfo entry, PeptideDocNode nodePep)
        {
            // Older format cache files will not have stored textId bytes
            if (Version < FORMAT_VERSION_CACHE_5 && _rawData.TextIdBytes == null)
                return true;
            int textIdIndex = entry.TextIdIndex;
            if (textIdIndex == -1)
                return true;
            int textIdLen = entry.TextIdLen;
            if (nodePep.Peptide.IsCustomMolecule)
            {
                if (EqualTextIdBytes(nodePep.CustomMolecule.ToSerializableString(), textIdIndex, textIdLen))
                {
                    return true;
                }
                // Older .skyd files used just the name of the molecule as the TextId.
                // We can't rely on the FormatVersion in the .skyd, because of the way that .skyd files can get merged.
                if (EqualTextIdBytes(nodePep.CustomMolecule.InvariantName, textIdIndex, textIdLen))
                {
                    return true;
                }
                // Test support - in "AsSmallMolecules" versions of tests where we transform a proteomic document to small molecules,
                // cached chromatogram textID may be that of the original peptide. In that case, look for "PEPTIDER" instead of "pep_PEPTIDER"
                if (SrmDocument.IsConvertedFromProteomicTestDocNode(nodePep) &&
                    nodePep.CustomMolecule.Name.StartsWith(RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator))
                {
                    if (EqualTextIdBytes(nodePep.CustomMolecule.Name.Substring(RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator.Length), textIdIndex, textIdLen))
                    {
                        return true;
                    }
                }
                return false;
            }
            else
            {
                var key1 = new PeptideLibraryKey(nodePep.ModifiedSequence, 0);
                var key2 = new PeptideLibraryKey(Encoding.ASCII.GetString(_rawData.TextIdBytes, textIdIndex, textIdLen), 0);
                return LibKeyIndex.KeysMatch(key1, key2);
            }
        }

        private bool EqualTextIdBytes(string compareString, int textIdIndex, int textIdLen)
        {
            var compareBytes = Encoding.UTF8.GetBytes(compareString);
            if (compareBytes.Length != textIdLen)
            {
                return false;
            }
            for (int i = 0; i < textIdLen; i++)
            {
                if (_rawData.TextIdBytes[textIdIndex + i] != compareBytes[i])
                    return false;
            }
            return true;
        }

        private int FindEntry(SignedMz precursorMz, float tolerance)
        {
            return FindEntry(precursorMz, tolerance, 0, ChromGroupHeaderInfos.Count - 1);
        }

        private int FindEntry(SignedMz precursorMz, float tolerance, int left, int right)
        {
            // Binary search for the right precursorMz
            if (left > right)
                return -1;
            int mid = (left + right) / 2;
            int compare = CompareMz(precursorMz, ChromGroupHeaderInfos[mid].Precursor, tolerance);
            if (compare < 0)
                return FindEntry(precursorMz, tolerance, left, mid - 1);
            if (compare > 0)
                return FindEntry(precursorMz, tolerance, mid + 1, right);
            
            // Scan backward until the first matching element is found.
            while (mid > 0 && MatchMz(precursorMz, ChromGroupHeaderInfos[mid - 1].Precursor, tolerance))
                mid--;

            return mid;
        }

        private static int CompareMz(SignedMz precursorMz1, SignedMz precursorMz2, float tolerance)
        {
            return precursorMz1.CompareTolerant(precursorMz2, tolerance);
        }

        private static bool MatchMz(SignedMz mz1, SignedMz mz2, float tolerance)
        {
            return CompareMz(mz1, mz2, tolerance) == 0;
        }

        // ReSharper disable UnusedMember.Local

        public static int HeaderSize
        {
            get
            {
                return CacheHeaderStruct.GetStructSize(FORMAT_VERSION_CACHE);
            }
        }

        // ReSharper restore UnusedMember.Local

        public ChromatogramCache ReleaseMemory()
        {
            return ChangeProp(ImClone(this), im =>
            {
                im._rawData = im._rawData.ChangeChromatogramEntries(BlockedArray<ChromGroupHeaderInfo>.EMPTY)
                    .ChangeChromTransitions(BlockedArray<ChromTransition>.EMPTY)
                    .ChangeTextIdBytes(Array.Empty<byte>());
            });
        }

        public class RawData : Immutable
        {
            public RawData(CacheHeaderStruct header, IEnumerable<ChromCachedFile> chromCacheFiles,
                BlockedArray<ChromGroupHeaderInfo> chromatogramEntries, BlockedArray<ChromTransition> transitions, 
                FeatureNames scoreTypes, long locationScoreValues, byte[] textIdBytes) : this(header)
            {
                ChromCacheFiles = ImmutableList.ValueOf(chromCacheFiles);
                ChromatogramEntries = chromatogramEntries;
                ChromTransitions = transitions;
                ScoreTypes = scoreTypes;
                LocationScoreValues = locationScoreValues;
                TextIdBytes = textIdBytes;
            }

            public RawData(CacheHeaderStruct header) : this(CacheFormat.FromCacheHeader(header))
            {
                LocationPeaks = header.locationPeaks;
                NumPeaks = header.numPeaks;
                NumScores = header.numScores;
                LocationScanIds = header.locationScanIds;
                if (FormatVersion > CacheFormatVersion.Eight)
                {
                    LocationScanIds = header.locationScanIds;
                }
                else
                {
                    // scan ids were not part of a .skyd file until version 9.
                    LocationScanIds = header.locationPeaks;
                }
            }
            public RawData(CacheFormat cacheFormat)
            {
                CacheFormat = cacheFormat;
                ChromCacheFiles = ImmutableList<ChromCachedFile>.EMPTY;
                ChromatogramEntries = BlockedArray<ChromGroupHeaderInfo>.EMPTY;
                ChromTransitions = BlockedArray<ChromTransition>.EMPTY;
                ScoreTypes = FeatureNames.EMPTY;
                TextIdBytes = Array.Empty<byte>();
            }

            public CacheFormat CacheFormat { get; private set; }
            public CacheFormatVersion FormatVersion { get { return CacheFormat.FormatVersion; } }
            public ImmutableList<ChromCachedFile> ChromCacheFiles { get; private set; }

            public RawData ChangeChromCacheFiles(IEnumerable<ChromCachedFile> files)
            {
                return ChangeProp(ImClone(this), im => im.ChromCacheFiles = ImmutableList.ValueOf(files));
            }
            public BlockedArray<ChromGroupHeaderInfo> ChromatogramEntries { get; private set; }

            public RawData ChangeChromatogramEntries(BlockedArray<ChromGroupHeaderInfo> entries)
            {
                return ChangeProp(ImClone(this), im => im.ChromatogramEntries = entries);
            }
            public BlockedArray<ChromTransition> ChromTransitions { get; private set; }

            public RawData ChangeChromTransitions(BlockedArray<ChromTransition> chromTransitions)
            {
                return ChangeProp(ImClone(this), im => im.ChromTransitions = chromTransitions);
            }
            public long LocationPeaks { get; private set; }
            public int NumPeaks { get; private set; }
            public FeatureNames ScoreTypes { get; private set; }

            public RawData ChangeScoreTypes(FeatureNames types, long locationScoreValues)
            {
                return ChangeProp(ImClone(this), im =>
                {
                    im.ScoreTypes = types;
                    im.LocationScoreValues = locationScoreValues;
                });
            }
            public long LocationScoreValues { get; private set; }
            public int NumScores { get; private set; }
            public byte[] TextIdBytes { get; private set; }

            public RawData ChangeTextIdBytes(byte[] bytes)
            {
                return ChangeProp(ImClone(this), im => im.TextIdBytes = bytes);
            }
            public long LocationScanIds { get; private set; }
            public long CountBytesScanIds
            {
                get { return LocationPeaks - LocationScanIds; }
            }

            public ChromGroupHeaderInfo RecalcEntry(int entryIndex,
                int offsetFiles,
                int offsetTransitions,
                int offsetPeaks,
                int offsetScores,
                long offsetPoints,
                Dictionary<Target, int> dictTextIdToByteIndex,
                List<byte> listTextIdBytes)
            {
                var entry = ChromatogramEntries[entryIndex];
                entry.Offset(offsetFiles,
                    offsetTransitions,
                    offsetPeaks,
                    offsetScores,
                    offsetPoints);
                entry.CalcTextIdIndex(GetTextId(entryIndex),
                    dictTextIdToByteIndex,
                    listTextIdBytes);
                return entry;
            }
            
            private Target GetTextId(int entryIndex)
            {
                int textIdIndex = ChromatogramEntries[entryIndex].TextIdIndex;
                if (textIdIndex == -1)
                    return null;
                int textIdLen = ChromatogramEntries[entryIndex].TextIdLen;
                return Target.FromSerializableString(Encoding.UTF8.GetString(TextIdBytes, textIdIndex, textIdLen));
            }

            public void TransferPeaks(Stream readStream, CacheFormat targetFormat, int firstPeakIndex, int peakCount, Stream writeStream)
            {
                readStream.Seek(LocationPeaks + firstPeakIndex * CacheFormat.ChromPeakSize, SeekOrigin.Begin);
                if (CacheFormat.ChromPeakSize == targetFormat.ChromPeakSize)
                {
                    readStream.TransferBytes(writeStream, (long) peakCount * CacheFormat.ChromPeakSize);
                    return;
                }

                var peaks = CacheFormat.ChromPeakSerializer().ReadArray(readStream, peakCount);
                targetFormat.ChromPeakSerializer().WriteItems(writeStream, peaks);
            }
        }

        public static ChromatogramCache Load(string cachePath, IProgressStatus status, ILoadMonitor loader, bool assumeNegativeChargeInPreV11Caches)
        {
            status = status.ChangeMessage(string.Format(Resources.ChromatogramCache_Load_Loading__0__cache, Path.GetFileName(cachePath)));
            loader.UpdateProgress(status);

            IPooledStream readStream = null;
            try
            {
                readStream = loader.StreamManager.CreatePooledStream(cachePath, false);
                // DebugLog.Info("{0}. {1} - loaded", readStream.GlobalIndex, cachePath);

                RawData raw;
                LoadStructs(readStream.Stream, status, loader, out raw, assumeNegativeChargeInPreV11Caches);

                var result = new ChromatogramCache(cachePath, raw, readStream);
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
            IList<string> listCachePaths, ILoadMonitor loader,
            Action<ChromatogramCache, IProgressStatus> complete,
            bool assumeNegativeChargeInPreV11Caches)
        {
            var status = new ProgressStatus(string.Empty);
            try
            {
                var joiner = new ChromCacheJoiner(cachePath, streamDest, listCachePaths, loader, status, complete, assumeNegativeChargeInPreV11Caches);
                joiner.JoinParts();
            }
            catch (Exception x)
            {
                complete(null, status.ChangeErrorException(x));
            }
        }

        public static void Build(SrmDocument document, string documentFilePath, ChromatogramCache cacheRecalc,
            string cachePath, MsDataFileUri msDataFileUri, IProgressStatus status, ILoadMonitor loader,
            Action<ChromatogramCache, IProgressStatus> complete)
        {
            try
            {
                if (Program.MultiProcImport && Program.ImportProgressPipe == null)
                {
                    // Import using a child process.
                    Run(msDataFileUri, documentFilePath, cachePath, status, loader);

                    var cacheNew = Load(cachePath, status, loader, false);
                    complete(cacheNew, status);
                }
                else
                {
                    // Import using threads in this process.
                    status = ((ChromatogramLoadingStatus) status).ChangeFilePath(msDataFileUri);
                    var builder = new ChromCacheBuilder(document, cacheRecalc, cachePath, msDataFileUri, loader, status, complete);
                    builder.BuildCache();
                }
            }
            catch (Exception x)
            {
                complete(null, status.ChangeErrorException(x));
            }
        }

        private static void Run(MsDataFileUri msDataFileUri, string documentFilePath, string cachePath, IProgressStatus status, ILoadMonitor loader)
        {
            // Arguments for child Skyline process.
            string importProgressPipe = @"SkylineImportProgress-" + Guid.NewGuid();
            var argsText =
                // ReSharper disable LocalizableElement
                "--in=\"" + documentFilePath + "\" " +
                "--import-file=\"" + msDataFileUri.GetFilePath() + "\" " +
                "--import-file-cache=\"" + cachePath + "\" " +
                "--import-progress-pipe=\"" + importProgressPipe + "\" " +
                "--import-no-join";
                // ReSharper restore LocalizableElement
            var psi = new ProcessStartInfo
            {
                // ReSharper disable once PossibleNullReferenceException
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Arguments = argsText,
                UseShellExecute = false,
            };
            RunProcess(psi, importProgressPipe, status, loader);
        }

        private static void RunProcess(ProcessStartInfo psi, string importProgressPipe, IProgressStatus status, ILoadMonitor loader)
        {
            // Make sure required streams are redirected.
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            var proc = Process.Start(psi);
            if (proc == null)
                throw new IOException(string.Format(@"Failure starting {0} command.", psi.FileName));

            var reader = new ProcessStreamReader(proc);
            string errorPrefix = Resources.CommandLine_GeneralException_Error___0_.Split('{')[0];
            var errorMessage = new StringBuilder();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var index = line.IndexOf(@"%%", StringComparison.Ordinal);
                if (index >= 0)
                {
                    int percentComplete;
                    if (int.TryParse(line.Substring(index + 2), out percentComplete))
                    {
                        status = status.ChangePercentComplete(percentComplete);
                        loader.UpdateProgress(status);
                    }
                }
                else if (line.StartsWith(errorPrefix) || errorMessage.Length > 0)
                {
                    errorMessage.AppendLine(line);
                }
            }

            proc.WaitForExit();

            if (errorMessage.Length > 0)
                throw new IOException(errorMessage.ToString());
        }

        public static long LoadStructs(Stream stream, out RawData raw, bool assumeNegativeChargesInPreV11Caches)
        {
            return LoadStructs(stream, null, null, out raw, assumeNegativeChargesInPreV11Caches);
        }

        public static long LoadStructs(Stream stream, IProgressStatus status, IProgressMonitor progressMonitor, out RawData raw, bool assumeNegativeChargeInPreV11Caches)
        {
            CacheHeaderStruct cacheHeader = CacheHeaderStruct.Read(stream);
            if (cacheHeader.formatVersion < CacheFormatVersion.Two || cacheHeader.numFiles == 0)
            {
                // Unexpected empty cache.  Return values that will force it to be completely rebuild.
                raw = new RawData(CacheFormat.EMPTY);
                return 0;
            }

            if (cacheHeader.IsCorrupted(stream.Length))
            {
                throw new InvalidDataException(Resources.ChromatogramCache_LoadStructs_FileCorrupted);
            }

            var formatVersion = cacheHeader.formatVersion;
            raw = new RawData(cacheHeader);
            CacheFormat cacheFormat = raw.CacheFormat;
            // Read list of files cached
            stream.Seek(cacheHeader.locationFiles, SeekOrigin.Begin);
            var chromCachedFiles = new List<ChromCachedFile>();

            var cachedFileHeaderSerializer = cacheFormat.CachedFileSerializer();

            for (int i = 0; i < cacheHeader.numFiles; i++)
            {
                var cachedFileStruct = cachedFileHeaderSerializer.ReadArray(stream, 1)[0];
                int lenPath = cachedFileStruct.lenPath;
                var filePathBuffer = new byte[lenPath];
                ReadComplete(stream, filePathBuffer, lenPath);
                string filePathString = formatVersion > CacheFormatVersion.Six
                                      ? Encoding.UTF8.GetString(filePathBuffer, 0, lenPath)
                                      : Encoding.Default.GetString(filePathBuffer, 0, lenPath); // Backward compatibility
                var filePath = MsDataFileUri.Parse(filePathString);

                string sampleId = null;
                int lenSampleId = cachedFileStruct.lenSampleId;
                if (formatVersion > CacheFormatVersion.Thirteen && lenSampleId > 0)
                {
                    byte[] sampleIdBuffer = new byte[lenSampleId];
                    ReadComplete(stream, sampleIdBuffer, lenSampleId);
                    sampleId = Encoding.UTF8.GetString(sampleIdBuffer, 0, lenSampleId);
                }

                string serialNumber = null;
                int lenSerialNumber = cachedFileStruct.lenSerialNumber;
                if (formatVersion > CacheFormatVersion.Thirteen && lenSerialNumber > 0)
                {
                    byte[] serialNumberBuffer = new byte[lenSerialNumber];
                    ReadComplete(stream, serialNumberBuffer, lenSerialNumber);
                    serialNumber = Encoding.UTF8.GetString(serialNumberBuffer, 0, lenSerialNumber);
                }

                string instrumentInfoStr = null;
                if (formatVersion > CacheFormatVersion.Three)
                {
                    int lenInstrumentInfo = cachedFileStruct.lenInstrumentInfo;
                    byte[] instrumentInfoBuffer = new byte[lenInstrumentInfo];
                    ReadComplete(stream, instrumentInfoBuffer, lenInstrumentInfo);
                    instrumentInfoStr = Encoding.UTF8.GetString(instrumentInfoBuffer, 0, lenInstrumentInfo);
                }

                DateTime modifiedTime = DateTime.FromBinary(cachedFileStruct.modified);
                DateTime? runstartTime = cachedFileStruct.runstart != 0 ? DateTime.FromBinary(cachedFileStruct.runstart) : (DateTime?)null;
                DateTime? importTime = cachedFileStruct.importTime != 0
                    ? DateTime.FromBinary(cachedFileStruct.importTime)
                    : (DateTime?) null;
                var instrumentInfoList = InstrumentInfoUtil.GetInstrumentInfo(instrumentInfoStr);
                chromCachedFiles.Add(new ChromCachedFile(filePath,
                                                             cachedFileStruct.flags,
                                                             modifiedTime,
                                                             runstartTime,
                                                             importTime,
                                                             cachedFileStruct.maxRetentionTime,
                                                             cachedFileStruct.maxIntensity,
                                                             cachedFileStruct.sizeScanIds,
                                                             cachedFileStruct.locationScanIds,
                                                             cachedFileStruct.ticArea == 0 ? (float?) null : cachedFileStruct.ticArea,
                                                             ChromCachedFile.IonMobilityUnitsFromFlags(cachedFileStruct.flags),
                                                             sampleId,
                                                             serialNumber,
                                                             instrumentInfoList));
            }
            Assume.AreEqual(cacheHeader.numFiles, chromCachedFiles.Count);
            raw = raw.ChangeChromCacheFiles(chromCachedFiles);

            if (progressMonitor != null)
                progressMonitor.UpdateProgress(status = status.ChangePercentComplete(10));

            // Read list of chromatogram group headers
            stream.Seek(cacheHeader.locationHeaders, SeekOrigin.Begin);
            raw = raw.ChangeChromatogramEntries(new BlockedArray<ChromGroupHeaderInfo>(
                count => cacheFormat.ChromGroupHeaderInfoSerializer().ReadArray(stream, count),
                cacheHeader.numChromatograms,
                ChromGroupHeaderInfo.SizeOf,
                ChromGroupHeaderInfo.DEFAULT_BLOCK_SIZE,
                progressMonitor,
                status));
            if (formatVersion < CacheFormatVersion.Eleven && assumeNegativeChargeInPreV11Caches)
            {
                raw = raw.ChangeChromatogramEntries(raw.ChromatogramEntries.ChangeAll(
                    chromGroupHeader => chromGroupHeader.ChangeChargeToNegative()));
            }

            if (formatVersion > CacheFormatVersion.Four)
            {
                // Read textId bytes (sequence, or custom ion id)
                raw = raw.ChangeTextIdBytes(new byte[cacheHeader.numTextIdBytes]);
                stream.Seek(cacheHeader.locationTextIdBytes, SeekOrigin.Begin);
                ReadComplete(stream, raw.TextIdBytes, raw.TextIdBytes.Length);
            }
            if (formatVersion > CacheFormatVersion.Four && cacheHeader.numScoreTypes > 0)
            {
                // Read scores
                var scoreTypes = new string[cacheHeader.numScoreTypes];
                stream.Seek(cacheHeader.locationScores, SeekOrigin.Begin);
                byte[] scoreTypeLengths = new byte[cacheHeader.numScoreTypes * 4];
                byte[] typeNameBuffer = new byte[1024];
                ReadComplete(stream, scoreTypeLengths, scoreTypeLengths.Length);
                for (int i = 0; i < cacheHeader.numScoreTypes; i++)
                {
                    int lenTypeName = GetInt32(scoreTypeLengths, i);
                    ReadComplete(stream, typeNameBuffer, lenTypeName);
                    scoreTypes[i] = Encoding.UTF8.GetString(typeNameBuffer, 0, lenTypeName);
                }

                raw = raw.ChangeScoreTypes(new FeatureNames(scoreTypes), stream.Position);
                Assume.AreEqual(raw.LocationScoreValues, stream.Position);
            }
            else
            {
                Assume.AreEqual(0, raw.ScoreTypes.Count);
            }

            if (progressMonitor != null)
                progressMonitor.UpdateProgress(status = status.ChangePercentComplete(30));

            // Read list of transitions
            stream.Seek(cacheHeader.locationTransitions, SeekOrigin.Begin);
            raw = raw.ChangeChromTransitions(new BlockedArray<ChromTransition>(
                count => cacheFormat.ChromTransitionSerializer().ReadArray(stream, count),
                cacheHeader.numTransitions,
                ChromTransition.SizeOf,
                ChromTransition.DEFAULT_BLOCK_SIZE,
                progressMonitor,
                status));

            if (progressMonitor != null)
                progressMonitor.UpdateProgress(status = status.ChangePercentComplete(50));

            return raw.LocationScanIds;  // Bytes of chromatogram data
        }

        private static int GetInt32(byte[] bytes, int index)
        {
            int ibyte = index * 4;
            return BitConverter.ToInt32(bytes, ibyte);
        }
        
        private static void ReadComplete(Stream stream, byte[] buffer, int size)
        {
            if (stream.Read(buffer, 0, size) != size)
                throw new InvalidDataException(Resources.ChromatogramCache_ReadComplete_Data_truncation_in_cache_header_File_may_be_corrupted);
        }

        public static CacheHeaderStruct WriteStructs(CacheFormat cacheFormat,
                                        Stream outStream,
                                        Stream outStreamScans,
                                        Stream outStreamPeaks,
                                        Stream outStreamScores,
                                        ICollection<ChromCachedFile> chromCachedFiles,
                                        ICollection<ChromGroupHeaderInfo> chromatogramEntries,
                                        ICollection<ChromTransition> chromTransitions,
                                        ICollection<byte> textIdBytes,
                                        FeatureNames scoreTypes,
                                        int scoreCount,
                                        int peakCount,
                                        out long scoreValueLocation)
        {
            scoreValueLocation = 0;
            var formatVersion = cacheFormat.FormatVersion;
            long locationScans = outStream.Position;
            if (formatVersion > FORMAT_VERSION_CACHE_8)
            {
                // Write any scan ids
                outStreamScans.Seek(0, SeekOrigin.Begin);
                outStreamScans.CopyTo(outStream);
            }
            // Write the picked peaks
            long locationPeaks = outStream.Position;
            outStreamPeaks.Seek(0, SeekOrigin.Begin);
            outStreamPeaks.CopyTo(outStream);

            // Write the transitions
            long locationTrans = outStream.Position;
            
            cacheFormat.ChromTransitionSerializer().WriteItems(outStream, chromTransitions);
            long locationScores = outStream.Position;
            long locationTextIdBytes = outStream.Position;
            int countScores = (int) (outStreamScores.Position/sizeof (float));
            if (formatVersion > FORMAT_VERSION_CACHE_4)
            {
                // Write the scores
                StringBuilder sbTypes = new StringBuilder();
                foreach (string scoreTypeName in scoreTypes)
                {
                    outStream.Write(BitConverter.GetBytes(scoreTypeName.Length), 0, sizeof(int));
                    sbTypes.Append(scoreTypeName);
                }
                int len = sbTypes.Length;
                if (len > 0)
                {
                    byte[] typesBuffer = new byte[len];
                    Encoding.UTF8.GetBytes(sbTypes.ToString(), 0, sbTypes.Length, typesBuffer, 0);
                    outStream.Write(typesBuffer, 0, len);
                    scoreValueLocation = outStream.Position;
                    outStreamScores.Seek(0, SeekOrigin.Begin);
                    outStreamScores.CopyTo(outStream);
                }

                // Write sequence or custom ion id bytes
                locationTextIdBytes = outStream.Position;
                if (textIdBytes != null && textIdBytes.Count > 0)
                {
                    byte[] textIdBytesBuffer = textIdBytes.ToArray();
                    outStream.Write(textIdBytesBuffer, 0, textIdBytesBuffer.Length);
                }
            }

            // ASSUME the entries reach here already sorted by whatever process created them
            // This allows ChromCacheJoiner to use a merge sort and the sort below is limited to
            // the 2 GB .NET maximum memory allocation block size.
            // Old code....
            // Sort the chromatogramEntries ("OrderBy" is a stable sort)
            // var sortedEntries = chromatogramEntries.OrderBy(entry => entry).ToArray();
            // chromatogramEntries.Clear();
            // chromatogramEntries.AddRange(sortedEntries);

            long locationHeaders = outStream.Position;

            cacheFormat.ChromGroupHeaderInfoSerializer().WriteItems(outStream, chromatogramEntries);
            // Write the list of cached files and their modification time stamps
            long locationFiles = outStream.Position;
            var cachedFileSerializer = cacheFormat.CachedFileSerializer();
            foreach (var cachedFile in chromCachedFiles)
            {
                var filePath = cachedFile.FilePath;
                if (formatVersion < CacheFormatVersion.Fourteen)
                    filePath = filePath.RestoreLegacyParameters(cachedFile.UsedMs1Centroids, cachedFile.UsedMs2Centroids);
                var filePathBytes = Encoding.UTF8.GetBytes(filePath.ToString());
                var instrumentInfoBytes =
                    Encoding.UTF8.GetBytes(InstrumentInfoUtil.GetInstrumentInfoString(cachedFile.InstrumentInfoList));
                var sampleIdBytes = Encoding.UTF8.GetBytes(cachedFile.SampleId ?? string.Empty);
                var serialNumberBytes = Encoding.UTF8.GetBytes(cachedFile.InstrumentSerialNumber ?? string.Empty);
                var cachedFileStruct = new CachedFileHeaderStruct
                {
                    modified = cachedFile.FileWriteTime.ToBinary(),
                    lenPath = filePathBytes.Length,
                    runstart = cachedFile.RunStartTime?.ToBinary() ?? 0,
                    lenInstrumentInfo = instrumentInfoBytes.Length,
                    flags = cachedFile.Flags,
                    maxRetentionTime = cachedFile.MaxRetentionTime,
                    maxIntensity = cachedFile.MaxIntensity,
                    sizeScanIds = cachedFile.SizeScanIds,
                    locationScanIds = cachedFile.LocationScanIds,
                    ticArea = cachedFile.TicArea.GetValueOrDefault(),
                    lenSampleId = sampleIdBytes.Length,
                    lenSerialNumber = serialNumberBytes.Length,
                    importTime = cachedFile.ImportTime?.ToBinary() ?? 0
                };
                cachedFileSerializer.WriteItems(outStream, new []{cachedFileStruct});
                // Write variable length buffers
                outStream.Write(filePathBytes, 0, filePathBytes.Length);
                if (formatVersion >= CacheFormatVersion.Fourteen)
                {
                    outStream.Write(sampleIdBytes, 0, sampleIdBytes.Length);
                    outStream.Write(serialNumberBytes, 0, serialNumberBytes.Length);
                }
                outStream.Write(instrumentInfoBytes, 0, instrumentInfoBytes.Length);
            }

            CacheHeaderStruct cacheHeader = new CacheHeaderStruct(cacheFormat)
            {
                locationScanIds = locationScans,
                numScoreTypes = scoreTypes.Count,
                numScores = countScores,
                locationScores = locationScores,
                numTextIdBytes = textIdBytes != null ? textIdBytes.Count : 0,
                locationTextIdBytes = locationTextIdBytes,
                numPeaks = peakCount,
                locationPeaks = locationPeaks,
                numTransitions = chromTransitions.Count,
                locationTransitions = locationTrans,
                numChromatograms = chromatogramEntries.Count,
                locationHeaders = locationHeaders,
                numFiles = chromCachedFiles.Count,
                locationFiles = locationFiles
            };
            cacheHeader.Write(outStream);
            return cacheHeader;
        }

        public static void BytesToTimeIntensities(byte[] bytes, int numPoints, int numTrans, bool withErrors,
            bool withMs1ScanIds, bool withFragmentScanIds, bool withSimScanIds,
            out float[] times, out float[][] intensities, out short[][] massErrors, out int[][] scanIds)
        {
            times = new float[numPoints];
            intensities = new float[numTrans][];
            massErrors = withErrors ? new short[numTrans][] : null;
            scanIds = null; 

            int sizeArray = sizeof(float)*numPoints;
            Buffer.BlockCopy(bytes, 0, times, 0, sizeArray);
            int offset = sizeArray;
            for (int i = 0; i < numTrans; i++, offset += sizeArray)
            {
                intensities[i] = new float[numPoints];
                Buffer.BlockCopy(bytes, offset, intensities[i], 0, sizeArray);
            }
            if (withErrors)
            {
                int sizeArrayErrors = sizeof(short)*numPoints;
                for (int i = 0; i < numTrans; i++, offset += sizeArrayErrors)
                {
                    massErrors[i] = new short[numPoints];
                    Buffer.BlockCopy(bytes, offset, massErrors[i], 0, sizeArrayErrors);
                }
            }
            if (withMs1ScanIds || withFragmentScanIds || withSimScanIds)
            {
                scanIds = new int[Helpers.CountEnumValues<ChromSource>() - 1][];
                int sizeArrayScanIds = sizeof(int) * numPoints;
                if (withMs1ScanIds)
                    scanIds[(int)ChromSource.ms1] = new int[numPoints];
                if (withFragmentScanIds)
                    scanIds[(int)ChromSource.fragment] = new int[numPoints];
                if (withSimScanIds)
                    scanIds[(int)ChromSource.sim] = new int[numPoints];
                for (int source = 0; source < scanIds.Length; source++)
                {
                    if (scanIds[source] != null)
                    {
                        Buffer.BlockCopy(bytes, offset, scanIds[source], 0, sizeArrayScanIds);
                        offset += sizeArrayScanIds;
                    }
                }
            }
        }

        public static byte[] TimeIntensitiesToBytes(float[] times, float[][] intensities, short[][] massErrors, int[][] scanIds)
        {
            int numPoints = times.Length;
            int sizeArray = numPoints*sizeof(float);
            int numTrans = intensities.Length;
            bool hasErrors = massErrors != null;
            bool hasMs1ScanIds = scanIds != null && scanIds[(int) ChromSource.ms1] != null;
            bool hasFragmentScanIds = scanIds != null && scanIds[(int) ChromSource.fragment] != null;
            bool hasSimScanIds = scanIds != null && scanIds[(int) ChromSource.sim] != null;
            byte[] points = new byte[GetChromatogramsByteCount(numTrans, numPoints, hasErrors, hasMs1ScanIds, hasFragmentScanIds, hasSimScanIds)];

            // Write times
            Buffer.BlockCopy(times, 0, points, 0, sizeArray);
            int offset = sizeArray;

            // Write intensities
            for (int i = 0; i < numTrans; i++, offset += sizeArray)
            {
                Buffer.BlockCopy(intensities[i], 0, points, offset, sizeArray);
            }

            // Write mass errors, if provided
            if (hasErrors)
            {
                int sizeArrayErrors = numPoints*sizeof(short);
                for (int i = 0; i < numTrans; i++, offset += sizeArrayErrors)
                {
                    if (massErrors[i] != null)
                    {
                        Buffer.BlockCopy(massErrors[i], 0, points, offset, sizeArrayErrors);
                    }
                }
            }

            // Write scan ids, if provided
            if (scanIds != null)
            {
                int sizeArrayScanIds = numPoints*sizeof(int);
                for (int source = 0; source < scanIds.Length; source++)
                {
                    if (scanIds[source] != null)
                    {
                        Buffer.BlockCopy(scanIds[source], 0, points, offset, sizeArrayScanIds);
                        offset += sizeArrayScanIds;
                    }
                }
            }

            return points;
        }

        public static int GetChromatogramsByteCount(int numTrans, int numPoints, bool hasErrors, 
            bool hasMs1ScanIds, bool hasFragmentScanIds, bool hasSimScanIds)
        {
            int sizeArray = sizeof(float)*numPoints;
            int sizeArrayErrors = sizeof(short)*numPoints;
            int sizeTotal = sizeArray*(numTrans + 1);
            if (hasErrors)
                sizeTotal += sizeArrayErrors*numTrans;
            if (hasMs1ScanIds)
                sizeTotal += sizeof (int)*numPoints;
            if (hasFragmentScanIds)
                sizeTotal += sizeof (int)*numPoints;
            if (hasSimScanIds)
                sizeTotal += sizeof (int)*numPoints;
            return sizeTotal;
        }

        public void GetStatusDimensions(MsDataFileUri msDataFilePath, out float? maxRetentionTime, out float? maxIntensity)
        {
            int fileIndex = CachedFiles.IndexOf(f => Equals(f.FilePath, msDataFilePath));
            if (fileIndex == -1)
            {
                maxRetentionTime = maxIntensity = null;
            }
            else
            {
                var cacheFile = CachedFiles[fileIndex];
                maxRetentionTime = cacheFile.MaxRetentionTime;
                maxIntensity = cacheFile.MaxIntensity;
            }
        }

        public IEnumerable<ChromKeyIndices> GetChromKeys(MsDataFileUri msDataFilePath)
        {
            int fileIndex = CachedFiles.IndexOf(f => Equals(f.FilePath, msDataFilePath));
            if (fileIndex == -1)
                yield break;

            for (int i = 0; i < ChromGroupHeaderInfos.Count; i++)
            {
                var groupInfo = ChromGroupHeaderInfos[i];
                if (groupInfo.FileIndex != fileIndex)
                    continue;

                IonMobilityValue ionMobilityValue = null;
                for (int j = 0; j < groupInfo.NumTransitions; j++)
                {
                    int tranIndex = groupInfo.StartTransitionIndex + j;
                    var tranInfo = _rawData.ChromTransitions[tranIndex];
                    var product = new SignedMz(tranInfo.Product, groupInfo.NegativeCharge);
                    float extractionWidth = tranInfo.ExtractionWidth;
                    var units = groupInfo.IonMobilityUnits;
                    if (units == eIonMobilityUnits.none && extractionWidth != 0)
                        units = eIonMobilityUnits.drift_time_msec; // Backward compatibility - drift time is all we had before
                    ChromSource source = tranInfo.Source;
                    ionMobilityValue = ionMobilityValue == null ? 
                        IonMobilityValue.GetIonMobilityValue(tranInfo.IonMobilityValue, units) :
                        ionMobilityValue.ChangeIonMobility(tranInfo.IonMobilityValue); // This likely doesn't change from transition to transition, so reuse it
                    ChromKey key = new ChromKey(_rawData.TextIdBytes, groupInfo.TextIdIndex, groupInfo.TextIdLen,
                        groupInfo.Precursor, product, extractionWidth, 
                        IonMobilityFilter.GetIonMobilityFilter(ionMobilityValue, tranInfo.IonMobilityExtractionWidth, groupInfo.CollisionalCrossSection),
                        source, groupInfo.Extractor, true, true);

                    int id = groupInfo.HasStatusId ? groupInfo.StatusId : i;
                    int rank = groupInfo.HasStatusRank ? groupInfo.StatusRank : -1;
                    yield return new ChromKeyIndices(key, groupInfo.LocationPoints, i, id, rank, j);
                }
            }
        }

        public ChromatogramCache Optimize(string documentPath, IEnumerable<MsDataFileUri> msDataFilePaths, IStreamManager streamManager,
            ILongWaitBroker progress)
        {
            string cachePathOpt = FinalPathForName(documentPath, null);
            return OptimizeToPath(null, cachePathOpt, msDataFilePaths, streamManager, progress);
        }

        public ChromatogramCache OptimizeToPath(CacheFormatVersion? formatVersion, string cachePathOpt,
            IEnumerable<MsDataFileUri> msDataFilePaths, IStreamManager streamManager, ILongWaitBroker progress)
        {
            var keepFilePaths = new HashSet<MsDataFileUri>(msDataFilePaths);
            var keepFileIndices = new HashSet<int>();
            for (int i = 0; i < _rawData.ChromCacheFiles.Count; i++)
            {
                var filePath = _rawData.ChromCacheFiles[i].FilePath;
                if (keepFilePaths.Contains(filePath))
                    keepFileIndices.Add(i);
            }

            // If the cache contains only the files in the document, then no
            // further optimization is necessary.
            if (keepFilePaths.Count == CachedFiles.Count && !formatVersion.HasValue || formatVersion == Version)
            {
                if (Equals(cachePathOpt, CachePath))
                    return this;
                // Copy the cache, if moving to a new location
                using (FileSaver fs = new FileSaver(cachePathOpt))
                {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    File.Copy(CachePath, fs.SafeName, true);
                    fs.Commit(ReadStream);
                }
                return ChangeCachePath(cachePathOpt, streamManager);
            }

            CacheFormat cacheFormat = CacheFormat.FromVersion(formatVersion ?? CacheFormatVersion.CURRENT);
            Assume.IsTrue(keepFilePaths.Count > 0);

            // Sort by file, points location into new array
            // CONSIDER: This is limited to 2 GB allocation size, but 4 bytes per Tuple instead of 72 bytes per header
            var listEntries = ChromGroupHeaderInfos
                .Select((e, i) => new Tuple<int, long, int>(e.FileIndex, e.LocationPoints, i))
                .Where(t => keepFileIndices.Contains(t.Item1)).ToArray();
            Array.Sort(listEntries);

            var listKeepEntries = new BlockedArrayList<ChromGroupHeaderInfo>(
                ChromGroupHeaderInfo.SizeOf, ChromGroupHeaderInfo.DEFAULT_BLOCK_SIZE);
            var listKeepCachedFiles = new List<ChromCachedFile>();
            var listKeepTransitions = new BlockedArrayList<ChromTransition>(
                ChromTransition.SizeOf, ChromTransition.DEFAULT_BLOCK_SIZE);
            var listKeepTextIdBytes = new List<byte>();
            var dictKeepTextIdIndices = new Dictionary<int, int>();

            // TODO: Make these first 3 temporary files that delete on close
            using (var fsPeaks = new FileSaver(cachePathOpt + PEAKS_EXT, true))
            using (var fsScans = new FileSaver(cachePathOpt + SCANS_EXT, true))
            using (var fsScores = new FileSaver(cachePathOpt + SCORES_EXT, true))
            using (var fs = new FileSaver(cachePathOpt))
            {
                var inStream = ReadStream.Stream;
                fs.Stream = streamManager.CreateStream(fs.SafeName, FileMode.Create, true);
                int peakCount = 0, scoreCount = 0;

                byte[] buffer = new byte[0x40000]; // 256K

                int i = 0;
                do
                {
                    var firstEntry = ChromGroupHeaderInfos[listEntries[i].Item3];
                    var lastEntry = firstEntry;
                    int fileIndex = firstEntry.FileIndex;
                    long offsetPoints = fs.Stream.Position - firstEntry.LocationPoints;

                    int iNext = i;
                    int firstPeakToTransfer = 0;
                    int numPeaksToTransfer = 0;
                    // Enumerate until end of current file encountered
                    while (iNext < listEntries.Length && fileIndex == ChromGroupHeaderInfos[listEntries[iNext].Item3].FileIndex)
                    {
                        lastEntry = ChromGroupHeaderInfos[listEntries[iNext++].Item3];
                        // Otherwise add entries to the keep lists
                        int textIdIndex = -1;
                        if (lastEntry.TextIdIndex != -1 &&
                            !dictKeepTextIdIndices.TryGetValue(lastEntry.TextIdIndex, out textIdIndex))
                        {
                            textIdIndex = listKeepTextIdBytes.Count;
                            dictKeepTextIdIndices.Add(lastEntry.TextIdIndex, textIdIndex);
                        }
                        listKeepEntries.Add(new ChromGroupHeaderInfo(lastEntry.Precursor,
                            textIdIndex,
                            lastEntry.TextIdLen,
                            listKeepCachedFiles.Count,
                            lastEntry.NumTransitions,
                            listKeepTransitions.Count,
                            lastEntry.NumPeaks,
                            peakCount,
                            scoreCount,
                            lastEntry.MaxPeakIndex,
                            lastEntry.NumPoints,
                            lastEntry.CompressedSize,
                            lastEntry.UncompressedSize,
                            lastEntry.LocationPoints + offsetPoints,
                            lastEntry.Flags,
                            lastEntry.StatusId,
                            lastEntry.StatusRank,
                            lastEntry.StartTime,
                            lastEntry.EndTime,
                            lastEntry.CollisionalCrossSection, 
                            lastEntry.IonMobilityUnits));
                        int start = lastEntry.StartTransitionIndex;
                        int end = start + lastEntry.NumTransitions;
                        for (int j = start; j < end; j++)
                            listKeepTransitions.Add(_rawData.ChromTransitions[j]);
                        int numEntryPeaks = lastEntry.NumPeaks * lastEntry.NumTransitions;
                        if (lastEntry.StartPeakIndex == firstPeakToTransfer + numPeaksToTransfer)
                        {
                            numPeaksToTransfer += numEntryPeaks;
                        }
                        else
                        {
                            if (numPeaksToTransfer > 0)
                            {
                                TransferPeaks(cacheFormat, firstPeakToTransfer, numPeaksToTransfer, fsPeaks.FileStream);
                            }

                            firstPeakToTransfer = lastEntry.StartPeakIndex;
                            numPeaksToTransfer = numEntryPeaks;
                        }
                        peakCount += numEntryPeaks;
                        
                        start = lastEntry.TextIdIndex;
                        end = start + lastEntry.TextIdLen;
                        for (int j = start; j < end; j++)
                            listKeepTextIdBytes.Add(_rawData.TextIdBytes[j]);

                        start = lastEntry.StartScoreIndex;
                        if (start != -1)
                        {
                            end = start + lastEntry.NumPeaks * ScoreTypes.Count;
                            scoreCount += end - start;
                            if (scoreCount > 0)
                            {
                                inStream.Seek(_rawData.LocationScoreValues + start * SCORE_VALUE_SIZE, SeekOrigin.Begin);
                                inStream.TransferBytes(fsScores.FileStream, (end - start) * SCORE_VALUE_SIZE);
                            }
                        }
                    }

                    if (numPeaksToTransfer > 0)
                    {
                        TransferPeaks(cacheFormat, firstPeakToTransfer, numPeaksToTransfer, fsPeaks.FileStream);
                    }

                    if (_rawData.ChromCacheFiles[fileIndex].SizeScanIds == 0)
                        listKeepCachedFiles.Add(_rawData.ChromCacheFiles[fileIndex]);
                    else
                    {
                        // Write all scan ids for the last file to the scan ids output stream
                        inStream.Seek(_rawData.LocationScanIds + _rawData.ChromCacheFiles[fileIndex].LocationScanIds, SeekOrigin.Begin);
                        int lenReadIds = _rawData.ChromCacheFiles[fileIndex].SizeScanIds;
                        long locationScanIds = fsScans.Stream.Position;
                        inStream.TransferBytes(fsScans.Stream, lenReadIds, buffer);
                        listKeepCachedFiles.Add(_rawData.ChromCacheFiles[fileIndex].RelocateScanIds(locationScanIds));
                    }

                    // Write all points for the last file to the output stream
                    inStream.Seek(firstEntry.LocationPoints, SeekOrigin.Begin);
                    long lenRead = lastEntry.LocationPoints + lastEntry.CompressedSize - firstEntry.LocationPoints;
                    inStream.TransferBytes(fs.Stream, lenRead, buffer);

                    // Advance to next file
                    i = iNext;

                    if (progress != null)
                        progress.SetProgressCheckCancel(i, listEntries.Length);
                } while (i < listEntries.Length);

                // CONSIDER: We should be able to figure out the order from the original order
                //           without needing this second sort
                listKeepEntries.Sort();

                var newCacheHeader = WriteStructs(
                    cacheFormat,
                    fs.Stream,
                    fsScans.Stream,
                    fsPeaks.Stream,
                    fsScores.Stream,
                    listKeepCachedFiles,
                    listKeepEntries,
                    listKeepTransitions,
                    listKeepTextIdBytes,
                    ScoreTypes,
                    scoreCount,
                    peakCount, out long scoreValueLocation);

                CommitCache(fs);

                fsPeaks.Stream.Seek(0, SeekOrigin.Begin);
                fsScores.Stream.Seek(0, SeekOrigin.Begin);
                var rawData =
                    new RawData(newCacheHeader, listKeepCachedFiles, listKeepEntries.ToBlockedArray(),
                        listKeepTransitions.ToBlockedArray(), ScoreTypes, scoreValueLocation, listKeepTextIdBytes.ToArray());
                return new ChromatogramCache(cachePathOpt, rawData,
                    // Create a new read stream, for the newly created file
                    streamManager.CreatePooledStream(cachePathOpt, false));
            }
        }

        public void TransferPeaks(CacheFormat targetFormat, int firstPeakIndex, int peakCount, Stream writeStream)
        {
            _rawData.TransferPeaks(ReadStream.Stream, targetFormat, firstPeakIndex, peakCount, writeStream);
        }

        public void WriteScanIds(FileStream outputStreamScans)
        {
            if (_rawData.CountBytesScanIds == 0)
                return;

            var stream = ReadStream.Stream;
            stream.Seek(_rawData.LocationScanIds, SeekOrigin.Begin);
            ReadStream.Stream.TransferBytes(outputStreamScans, _rawData.CountBytesScanIds);
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
                if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
                {
                    return ReferenceEquals(x, null) && ReferenceEquals(y, null);
                }
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

        /// <summary>
        /// Create a map of LibraryKey to the indexes into _chromatogramEntries that have that particular
        /// TextId.
        /// </summary>
        private LibKeyMap<int[]> MakeChromEntryIndex()
        {
            if (_rawData.TextIdBytes == null)
            {
                return null;
            }

            var libraryKeyIndexes= new Dictionary<KeyValuePair<int, int>, int>();
            List<LibraryKey> libraryKeys = new List<LibraryKey>();
            List<List<int>> chromGroupIndexes = new List<List<int>>();

            for (int i = 0; i < ChromGroupHeaderInfos.Count; i++)
            {
                var entry = ChromGroupHeaderInfos[i];
                int textIdIndex = entry.TextIdIndex;
                int textIdLength = entry.TextIdLen;
                if (textIdLength == 0)
                {
                    continue;
                }
                var kvp = new KeyValuePair<int, int>(textIdIndex, textIdLength);
                int libraryKeyIndex;
                List<int> chromGroupIndexList;
                if (libraryKeyIndexes.TryGetValue(kvp, out libraryKeyIndex))
                {
                    chromGroupIndexList = chromGroupIndexes[libraryKeyIndex];
                }
                else
                {
                    libraryKeyIndexes.Add(kvp, libraryKeys.Count);
                    LibraryKey libraryKey;
                    if (_rawData.TextIdBytes[textIdIndex] == '#')
                    {
                        var customMolecule =
                            CustomMolecule.FromSerializableString(Encoding.UTF8.GetString(_rawData.TextIdBytes, textIdIndex,
                                textIdLength));
                        libraryKey = new MoleculeLibraryKey(customMolecule.GetSmallMoleculeLibraryAttributes(), Adduct.EMPTY);
                    }
                    else
                    {
                        libraryKey =
                            new PeptideLibraryKey(Encoding.ASCII.GetString(_rawData.TextIdBytes, textIdIndex, textIdLength), 0);
                    }
                    libraryKeys.Add(libraryKey);
                    chromGroupIndexList = new List<int>();
                    chromGroupIndexes.Add(chromGroupIndexList);
                }
                chromGroupIndexList.Add(i);
            }
            return new LibKeyMap<int[]>(
                ImmutableList.ValueOf(chromGroupIndexes.Select(indexes=>indexes.ToArray())), 
                libraryKeys);
        }

        public byte[] GetTextIdBytes(int textIdOffset, int textIdLength)
        {
            if (textIdOffset == -1)
            {
                return null;
            }
            byte[] result = new byte[textIdLength];
            Array.Copy(_rawData.TextIdBytes, textIdOffset, result, 0, textIdLength);
            return result;
        }

        public byte[] ReadTimeIntensitiesBytes(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            return CallWithStream(stream =>
            {
                byte[] pointsCompressed = new byte[chromGroupHeaderInfo.CompressedSize];
                // Seek to stored location
                stream.Seek(chromGroupHeaderInfo.LocationPoints, SeekOrigin.Begin);

                // Single read to get all the points
                if (stream.Read(pointsCompressed, 0, pointsCompressed.Length) < pointsCompressed.Length)
                    throw new IOException(Resources
                        .ChromatogramGroupInfo_ReadChromatogram_Failure_trying_to_read_points);
                return pointsCompressed;

            });
        }

        private T CallWithStream<T>(Func<Stream, T> func)
        {
            var stream = ReadStream.Stream;
            lock (stream)
            {
                try
                {
                    return func(stream);
                }
                catch (Exception)
                {
                    // If an exception is thrown, close the stream in case the failure is something
                    // like a network failure that can be remedied by re-opening the stream.
                    ReadStream.CloseStream();
                    throw;
                }
            }
        }

        public TimeIntensitiesGroup ReadTimeIntensities(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            var compressedBytes = ReadTimeIntensitiesBytes(chromGroupHeaderInfo);
            int uncompressedSize = chromGroupHeaderInfo.UncompressedSize;
            if (uncompressedSize < 0) // Before version 11
            {
                int numPoints = chromGroupHeaderInfo.NumPoints;
                int numTrans = chromGroupHeaderInfo.NumTransitions;
                bool hasErrors = chromGroupHeaderInfo.HasMassErrors;
                bool hasMs1ScanIds = chromGroupHeaderInfo.HasMs1ScanIds;
                bool hasFragmentScanIds = chromGroupHeaderInfo.HasFragmentScanIds;
                bool hasSimScanIds = chromGroupHeaderInfo.HasSimScanIds;

                uncompressedSize = GetChromatogramsByteCount(numTrans, numPoints, hasErrors, hasMs1ScanIds,
                    hasFragmentScanIds, hasSimScanIds);
            }
            var uncompressedBytes = compressedBytes.Uncompress(uncompressedSize);
            if (chromGroupHeaderInfo.HasRawChromatograms)
            {
                return RawTimeIntensities.ReadFromStream(new MemoryStream(uncompressedBytes));
            }
            else
            {
                var chromTransitions = Enumerable.Range(chromGroupHeaderInfo.StartTransitionIndex, chromGroupHeaderInfo.NumTransitions)
                    .Select(i => _rawData.ChromTransitions[i]).ToArray();
                return InterpolatedTimeIntensities.ReadFromStream(new MemoryStream(uncompressedBytes),
                    chromGroupHeaderInfo, chromTransitions);
            }
        }

        public IList<ChromPeak> ReadPeaks(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            return ReadPeaksStartingAt(chromGroupHeaderInfo.StartPeakIndex,
                chromGroupHeaderInfo.NumPeaks * chromGroupHeaderInfo.NumTransitions);
        }

        public IList<ChromPeak> ReadPeaksStartingAt(long startPeakIndex, int count)
        {
            if (count == 0)
            {
                return Array.Empty<ChromPeak>();
            }
            return CallWithStream(stream => ReadPeaksStartingAt(stream, startPeakIndex, count));
        }

        private IList<ChromPeak> ReadPeaks(Stream stream, ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            return ReadPeaksStartingAt(stream, chromGroupHeaderInfo.StartPeakIndex,
                chromGroupHeaderInfo.NumPeaks * chromGroupHeaderInfo.NumTransitions);
        }

        private IList<ChromPeak> ReadPeaksStartingAt(Stream stream, long startPeakIndex, int count)
        {
            stream.Seek(_rawData.LocationPeaks + _rawData.CacheFormat.ChromPeakSize * startPeakIndex,
                SeekOrigin.Begin);
            return _rawData.CacheFormat.ChromPeakSerializer().ReadArray(stream, count);
        }


        public IList<float> ReadScores(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            var scoreValueCount = chromGroupHeaderInfo.NumPeaks * _scoreTypeIndices.Count;
            if (scoreValueCount == 0)
            {
                return Array.Empty<float>();
            }
            return CallWithStream(stream => ReadScoresStartingAt(stream, chromGroupHeaderInfo.StartScoreIndex, scoreValueCount));
        }

        private IList<float> ReadScoresStartingAt(Stream stream, long startScoreIndex, int scoreValueCount)
        {
            if (scoreValueCount == 0)
            {
                return Array.Empty<float>();
            }
            stream.Seek(_rawData.LocationScoreValues + startScoreIndex * SCORE_VALUE_SIZE, SeekOrigin.Begin);
            return PrimitiveArrays.Read<float>(stream, scoreValueCount);
        }

        /// <summary>
        /// Reads the peaks and/or the scores for a list of ChromGroupHeaderInfo's.
        /// The passed in arrays must either be null, or must have a length equal to the number of ChromGroupHeaderInfo's.
        /// </summary>
        public void ReadDataForAll(IList<ChromGroupHeaderInfo> chromGroupHeaderInfos, IList<ChromPeak>[] peaks, IList<float>[] scores)
        {
            if (peaks != null)
            {
                Assume.AreEqual(chromGroupHeaderInfos.Count, peaks.Length);
            }

            if (scores != null)
            {
                Assume.AreEqual(chromGroupHeaderInfos.Count, scores.Length);
            }
            using (var stream = new FileStream(CachePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (peaks != null)
                {
                    // Read the peaks for all of the ChromGroupHeaderInfos
                    // We process these in order of StartPeakIndex, because, in theory, it might make seeking in the file stream faster,
                    // but it does not seem to make a difference in practice
                    foreach (var index in Enumerable.Range(0, chromGroupHeaderInfos.Count)
                        .OrderBy(i => chromGroupHeaderInfos[i].StartPeakIndex))
                    {
                        peaks[index] = ReadPeaks(stream, chromGroupHeaderInfos[index]);
                    }
                }

                if (scores != null)
                {
                    // Read the scores. Some of the ChromGroupHeaderInfos may have the same StartScoreIndex and number of peaks,
                    // so process those at the same time
                    foreach (var indexGroup in Enumerable.Range(0, chromGroupHeaderInfos.Count)
                        .GroupBy(i => Tuple.Create(chromGroupHeaderInfos[i].StartScoreIndex,
                            chromGroupHeaderInfos[i].NumPeaks))
                        .OrderBy(group => group.Key.Item1))
                    {
                        var groupScores = ReadScoresStartingAt(stream, indexGroup.Key.Item1,
                            indexGroup.Key.Item2 * ScoreTypesCount);
                        foreach (var index in indexGroup)
                        {
                            scores[index] = groupScores;
                        }
                    }
                }
            }
        }
    }

    public struct ChromKeyIndices
    {
        public ChromKeyIndices(ChromKey key, long locationPoints, int groupIndex, int statusId, int statusRank, int tranIndex)
            : this()
        {
            Key = key;
            LocationPoints = locationPoints;
            GroupIndex = groupIndex;
            StatusId = statusId;
            StatusRank = statusRank;
            TranIndex = tranIndex;
        }

        public ChromKey Key { get; private set; }
        public long LocationPoints { get; private set; }
        public int GroupIndex { get; private set; }
        public int StatusId { get; private set; }
        public int StatusRank { get; private set; }
        public int TranIndex { get; private set; }
    }
}
