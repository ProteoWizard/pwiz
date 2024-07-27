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
using Google.Protobuf;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results.Legacy;
using pwiz.Skyline.Model.Results.ProtoBuf;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.Results.Spectra;
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

        public const CacheFormatVersion FORMAT_VERSION_CHROM_TRANSITION_OPTSTEP = CacheFormatVersion.Seventeen;
        public const CacheFormatVersion FORMAT_VERSION_RESULT_FILE_DATA = CacheFormatVersion.Eighteen;

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
            return GetOrLoadResultFileMetadata(fileIndex)?.ToMsDataFileScanIds();
        }

        public ResultFileMetaData GetResultFileMetadata(int fileIndex)
        {
            return _rawData.ResultFileDatas?[fileIndex];
        }

        public IResultFileMetadata GetOrLoadResultFileMetadata(int fileIndex)
        {
            var resultFileData = _rawData.ResultFileDatas?[fileIndex];

            if (resultFileData != null)
            {
                return resultFileData;
            }
            return MsDataFileScanIds.FromBytes(LoadMSDataFileScanIdBytes(fileIndex));
        }

        private byte[] LoadMSDataFileScanIdBytes(int fileIndex)
        {
            var cachedFile = CachedFiles[fileIndex];
            if (cachedFile.SizeScanIds == 0)
            {
                return null;
            }
            return CallWithStream(stream =>
            {
                byte[] scanIdBytes = new byte[cachedFile.SizeScanIds];
                stream.Seek(_rawData.LocationScanIds + cachedFile.LocationScanIds, SeekOrigin.Begin);

                // Single read to get all the points
                if (stream.Read(scanIdBytes, 0, scanIdBytes.Length) < scanIdBytes.Length)
                    throw new IOException(ResultsResources
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

            return GetHeaderInfos(nodePep, nodeGroup?.SpectrumClassFilter ?? default, precursorMz, explicitRT, tolerance, chromatograms);
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
                                             GetChromatogramGroupId(chromGroupHeaderInfo),
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

        private ChromatogramCache ChangeRawData(RawData rawData)
        {
            return ChangeProp(ImClone(this), im => im._rawData = rawData);
        }

        public void Dispose()
        {
            ReadStream.CloseStream();
        }

        private IEnumerable<ChromatogramGroupInfo> GetHeaderInfos(PeptideDocNode nodePep, SpectrumClassFilter spectrumClassFilter, SignedMz precursorMz, double? explicitRT, float tolerance,
            ChromatogramSet chromatograms)
        {
            foreach (int i in ChromatogramIndexesMatching(nodePep, spectrumClassFilter, precursorMz, tolerance, chromatograms))
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

        public IEnumerable<int> ChromatogramIndexesMatching(PeptideDocNode nodePep, SpectrumClassFilter spectrumClassFilter, SignedMz precursorMz,
            float tolerance, ChromatogramSet chromatograms)
        {
            var fileIndexesFound = new HashSet<int>();
            // For peptides, see if "_chromEntryIndex" has any matches.
            // We only do this for peptides because:
            // 1. Small molecules do not require the complicated matching logic in "MassModification.Matches".
            // 2. The LibKey for a peptide is always based on "nodePep.ModifiedTarget". Small molecule LibKey's have
            // evolved over time and is implemented in "TextIdEqual".
            if (true == nodePep?.IsProteomic && _chromEntryIndex != null)
            {
                var key = new LibKey(nodePep.ModifiedTarget, Adduct.EMPTY).LibraryKey;
                foreach (var chromatogramIndex in _chromEntryIndex.ItemsMatching(key, false).SelectMany(list=>list))
                {
                    var entry = ChromGroupHeaderInfos[chromatogramIndex];
                    if (!Equals(spectrumClassFilter, GetChromatogramGroupId(entry)?.SpectrumClassFilter))
                    {
                        continue;
                    }
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

                    yield return chromatogramIndex;
                    fileIndexesFound.Add(entry.FileIndex);
                }

                if (chromatograms == null)
                {
                    if (fileIndexesFound.Count == _rawData.ChromCacheFiles.Count)
                    {
                        // If matching chromatograms were found in every file, then we are finished
                        yield break;
                    }
                    // Otherwise, there might be some matching chromatograms in other replicates which have no TextId
                }
                else
                {
                    if (fileIndexesFound.Any())
                    {
                        // If searching for chromatograms in a particular replicate, then we are done if we find any matches
                        yield break;
                    }
                }
            }

            // Look for matching chromatograms which do not have a text id.
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
                if (fileIndexesFound.Contains(entry.FileIndex))
                {
                    continue;
                }
                if (chromatograms != null &&
                    !chromatograms.ContainsFile(_rawData.ChromCacheFiles[entry.FileIndex]
                        .FilePath))
                {
                    continue;
                }

                if (nodePep != null && !TextIdEqual(entry, nodePep, spectrumClassFilter))
                    continue;
                yield return i;
            }
        }

        private bool TextIdEqual(ChromGroupHeaderInfo entry, PeptideDocNode nodePep, SpectrumClassFilter spectrumClassFilter)
        {
            var chromatogramGroupId = GetChromatogramGroupId(entry);
            if (!Equals(spectrumClassFilter, chromatogramGroupId?.SpectrumClassFilter ?? default(SpectrumClassFilter)))
            {
                return false;
            }
            if (chromatogramGroupId == null)
            {
                return true;
            }
            if (Equals(nodePep.ChromatogramTarget, chromatogramGroupId.Target))
            {
                return true;
            }
            if (nodePep.Peptide.IsCustomMolecule)
            {
                // Older .skyd files used just the name of the molecule as the TextId.
                // We can't rely on the FormatVersion in the .skyd, because of the way that .skyd files can get merged.
                if (Equals(nodePep.CustomMolecule.InvariantName, chromatogramGroupId.Target.Sequence))
                {
                    return true;
                }
                // Test support - in "AsSmallMolecules" versions of tests where we transform a proteomic document to small molecules,
                // cached chromatogram textID may be that of the original peptide. In that case, look for "PEPTIDER" instead of "pep_PEPTIDER"
                if (SrmDocument.IsConvertedFromProteomicTestDocNode(nodePep) &&
                    nodePep.CustomMolecule.Name.StartsWith(RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator))
                {
                    if (chromatogramGroupId.Target.Sequence ==
                        nodePep.CustomMolecule.Name.Substring(RefinementSettings
                            .TestingConvertedFromProteomicPeptideNameDecorator.Length))
                    {
                        return true;
                    }
                }
                return false;
            }
            else
            {
                if (chromatogramGroupId.Target?.Sequence == null)
                {
                    return false;
                }
                var key1 = new PeptideLibraryKey(nodePep.ModifiedSequence, 0);
                var key2 = new PeptideLibraryKey(chromatogramGroupId.Target.Sequence, 0);
                return LibKeyIndex.KeysMatch(key1, key2);
            }
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
                    .ChangeChromatogramGroupIds(ImmutableList.Empty<ChromatogramGroupId>());
            });
        }

        public class RawData : Immutable
        {
            public RawData(CacheHeaderStruct header, IEnumerable<ChromCachedFile> chromCacheFiles, IList<ResultFileMetaData> resultFileDatas,
                BlockedArray<ChromGroupHeaderInfo> chromatogramEntries, BlockedArray<ChromTransition> transitions, 
                FeatureNames scoreTypes, long locationScoreValues, IEnumerable<ChromatogramGroupId> chromatogramGroupIds) : this(header)
            {
                ChromCacheFiles = ImmutableList.ValueOf(chromCacheFiles);
                ChromatogramEntries = chromatogramEntries;
                ChromTransitions = transitions;
                ScoreTypes = scoreTypes;
                LocationScoreValues = locationScoreValues;
                ChromatogramGroupIds = ImmutableList.ValueOf(chromatogramGroupIds);
                ResultFileDatas = ImmutableList.ValueOf(resultFileDatas);
                if (ResultFileDatas != null)
                {
                    Assume.AreEqual(ResultFileDatas.Count, ChromCacheFiles.Count);
                }
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
                ChromatogramGroupIds = ImmutableList.Empty<ChromatogramGroupId>();
            }

            public CacheFormat CacheFormat { get; private set; }
            public CacheFormatVersion FormatVersion { get { return CacheFormat.FormatVersion; } }
            public ImmutableList<ChromCachedFile> ChromCacheFiles { get; private set; }

            public RawData ChangeChromCacheFiles(IEnumerable<ChromCachedFile> files)
            {
                return ChangeProp(ImClone(this), im =>
                {
                    im.ChromCacheFiles = ImmutableList.ValueOf(files);
                    if (im.ResultFileDatas != null)
                    {
                        Assume.AreEqual(im.ChromCacheFiles.Count, im.ResultFileDatas.Count);
                    }
                });
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
            public ImmutableList<ChromatogramGroupId> ChromatogramGroupIds { get; private set; }

            public RawData ChangeChromatogramGroupIds(IEnumerable<ChromatogramGroupId> groupIds)
            {
                return ChangeProp(ImClone(this), im => im.ChromatogramGroupIds = ImmutableList.ValueOf(groupIds));
            }
            public long LocationScanIds { get; private set; }
            public long CountBytesScanIds
            {
                get { return LocationPeaks - LocationScanIds; }
            }

            public ImmutableList<ResultFileMetaData> ResultFileDatas { get; private set; }

            public RawData ChangeResultFileDatas(IEnumerable<ResultFileMetaData> resultFileDatas)
            {
                var list = ImmutableList.ValueOf(resultFileDatas);
                if (list != null)
                {
                    Assume.AreEqual(ChromCacheFiles.Count, list.Count);
                }
                return ChangeProp(ImClone(this), im => im.ResultFileDatas = list);
            }

            public ChromGroupHeaderInfo RecalcEntry(int entryIndex,
                int offsetFiles,
                int offsetTransitions,
                int offsetPeaks,
                int offsetScores,
                long offsetPoints,
                ChromatogramGroupIds chromatogramGroupIds)
            {
                var entry = ChromatogramEntries[entryIndex];
                entry.Offset(offsetFiles,
                    offsetTransitions,
                    offsetPeaks,
                    offsetScores,
                    offsetPoints);
                if (entry.TextIdIndex >= 0)
                {
                    entry = chromatogramGroupIds.SetId(entry, ChromatogramGroupIds[entry.TextIdIndex]);
                }
                return entry;
            }
            
            public void TransferPeaks(Stream readStream, CacheFormat targetFormat, int firstPeakIndex, int peakCount, Stream writeStream)
            {
                readStream.Seek(LocationPeaks + firstPeakIndex * CacheFormat.ChromPeakSize, SeekOrigin.Begin);
                if (CacheFormat.ChromPeakSize == targetFormat.ChromPeakSize)
                {
                    readStream.TransferBytes(writeStream, (long) peakCount * CacheFormat.ChromPeakSize);
                    return;
                }

                int chunkSize = 1024;
                int peaksRemaining = peakCount;
                while (peaksRemaining > 0)
                {
                    int peaksThisChunk = Math.Min(peaksRemaining, chunkSize);
                    var peaks = CacheFormat.ChromPeakSerializer().ReadArray(readStream, peaksThisChunk);
                    targetFormat.ChromPeakSerializer().WriteItems(writeStream, peaks);
                    peaksRemaining -= peaksThisChunk;
                }
            }
        }

        public static ChromatogramCache Load(string cachePath, IProgressStatus status, ILoadMonitor loader, SrmDocument doc)
        {
            status = status.ChangeMessage(string.Format(ResultsResources.ChromatogramCache_Load_Loading__0__cache, Path.GetFileName(cachePath)));
            loader.UpdateProgress(status);

            IPooledStream readStream = null;
            try
            {
                readStream = loader.StreamManager.CreatePooledStream(cachePath, false);
                // DebugLog.Info("{0}. {1} - loaded", readStream.GlobalIndex, cachePath);

                LoadStructs(readStream.Stream, status, loader, out var raw);

                var result = new ChromatogramCache(cachePath, raw, readStream);
                result = result.UpdateChargeSigns(doc);
                result = result.UpdateOptimizationSteps(doc);
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

        public static void Join(string cachePath, IPooledStream streamDest, IList<string> listCachePaths,
            ILoadMonitor loader, Action<ChromatogramCache, IProgressStatus> complete)
        {
            var status = new ProgressStatus(string.Empty);
            try
            {
                var joiner = new ChromCacheJoiner(cachePath, streamDest, listCachePaths, loader, status, complete);
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

                    var cacheNew = Load(cachePath, status, loader, document);
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
            string errorPrefix = Resources.Error___0_.Split('{')[0];
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

        public static long LoadStructs(Stream stream, IProgressStatus status, IProgressMonitor progressMonitor, out RawData raw)
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
                throw new InvalidDataException(ResultsResources.ChromatogramCache_LoadStructs_FileCorrupted);
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

            if (formatVersion >= FORMAT_VERSION_RESULT_FILE_DATA)
            {
                stream.Seek(cacheHeader.locationTextIdBytes, SeekOrigin.Begin);
                var proto = new ChromatogramGroupIdsProto();
                proto.MergeFrom(stream.ReadBytes(cacheHeader.numTextIdBytes));
                raw = raw.ChangeChromatogramGroupIds(ChromatogramGroupId.FromProto(proto));
                stream.Seek(cacheHeader.locationHeaders, SeekOrigin.Begin);
                var chromatogramEntries = new BlockedArray<ChromGroupHeaderInfo>(
                    count => cacheFormat.ChromGroupHeaderInfoSerializer().ReadArray(stream, count),
                    cacheHeader.numChromatograms,
                    ChromGroupHeaderInfo.SizeOf,
                    ChromGroupHeaderInfo.DEFAULT_BLOCK_SIZE,
                    progressMonitor,
                    status);
                raw = raw.ChangeChromatogramEntries(chromatogramEntries);
            }
            else
            {
                byte[] textIdBytes = null;
                if (formatVersion > CacheFormatVersion.Four)
                {
                    stream.Seek(cacheHeader.locationTextIdBytes, SeekOrigin.Begin);
                    textIdBytes = stream.ReadBytes(cacheHeader.numTextIdBytes);
                }

                stream.Seek(cacheHeader.locationHeaders, SeekOrigin.Begin);
                var oldChromatogramEntries = new BlockedArray<ChromGroupHeaderInfo16>(
                    count => cacheFormat.OldChromGroupHeaderInfoSerializer().ReadArray(stream, count),
                    cacheHeader.numChromatograms,
                    ChromGroupHeaderInfo16.SizeOf,
                    ChromGroupHeaderInfo.DEFAULT_BLOCK_SIZE,
                    progressMonitor,
                    status);

                var chromatogramGroupIds = new ChromatogramGroupIds();
                var chromatogramEntries = BlockedArray<ChromGroupHeaderInfo>.FromEnumerable(
                    chromatogramGroupIds.ConvertFromTextIdBytes(textIdBytes, oldChromatogramEntries),
                    oldChromatogramEntries.Count, ChromGroupHeaderInfo.SizeOf, ChromGroupHeaderInfo.DEFAULT_BLOCK_SIZE,
                    progressMonitor, status);
                raw = raw.ChangeChromatogramEntries(chromatogramEntries).ChangeChromatogramGroupIds(chromatogramGroupIds);
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
            if (raw.ChromCacheFiles.Any(file => file.HasResultFileData))
            {
                var resultFileDatas = new List<ResultFileMetaData>();
                foreach (var chromCachedFile in raw.ChromCacheFiles)
                {
                    if (!chromCachedFile.HasResultFileData)
                    {
                        resultFileDatas.Add(null);
                        continue;
                    }

                    stream.Seek(chromCachedFile.LocationScanIds + cacheHeader.locationScanIds, SeekOrigin.Begin);
                    var byteArray = new byte[chromCachedFile.SizeScanIds];
                    ReadComplete(stream, byteArray, byteArray.Length);
                    resultFileDatas.Add(ResultFileMetaData.FromByteArray(byteArray));
                }

                raw = raw.ChangeResultFileDatas(resultFileDatas);
            }

            if (progressMonitor != null)
                progressMonitor.UpdateProgress(status = status.ChangePercentComplete(50));

            return raw.LocationScanIds;  // Bytes of chromatogram data
        }

        private IList<ResultFileMetaData> ReadResultFileDatas(Stream stream,
            CachedFileHeaderStruct cacheHeader,
            IList<ChromCachedFile> chromCachedFiles)
        {
            if (chromCachedFiles.All(file=>!file.HasResultFileData))
            {
                return null;
            }
            var resultFileDatas = new List<ResultFileMetaData>();
            foreach (var chromCachedFile in chromCachedFiles)
            {
                if (!chromCachedFile.HasResultFileData)
                {
                    resultFileDatas.Add(null);
                    continue;
                }

                stream.Seek(chromCachedFile.LocationScanIds + cacheHeader.locationScanIds, SeekOrigin.Begin);
                var byteArray = new byte[chromCachedFile.SizeScanIds];
                ReadComplete(stream, byteArray, byteArray.Length);
                resultFileDatas.Add(ResultFileMetaData.FromByteArray(byteArray));
            }

            return resultFileDatas;
        }

        private static int GetInt32(byte[] bytes, int index)
        {
            int ibyte = index * 4;
            return BitConverter.ToInt32(bytes, ibyte);
        }
        
        private static void ReadComplete(Stream stream, byte[] buffer, int size)
        {
            if (stream.Read(buffer, 0, size) != size)
                throw new InvalidDataException(ResultsResources.ChromatogramCache_ReadComplete_Data_truncation_in_cache_header_File_may_be_corrupted);
        }

        public static CacheHeaderStruct WriteStructs(CacheFormat cacheFormat,
                                        Stream outStream,
                                        Stream outStreamScans,
                                        Stream outStreamPeaks,
                                        Stream outStreamScores,
                                        ICollection<ChromCachedFile> chromCachedFiles,
                                        ICollection<ChromGroupHeaderInfo> chromatogramEntries,
                                        ICollection<ChromTransition> chromTransitions,
                                        ChromatogramGroupIds chromatogramGroupIds,
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
            IEnumerable<ChromTransition> transitionsToWrite = chromTransitions;
            if (formatVersion < FORMAT_VERSION_CHROM_TRANSITION_OPTSTEP &&
                chromTransitions.Any(chromTransition => 0 != chromTransition.OptimizationStep))
            {
                transitionsToWrite = chromTransitions.Select(chromTransition =>
                    chromTransition.ChangeOptimizationStep(0,
                        chromTransition.Product +
                        ChromatogramInfo.OPTIMIZE_SHIFT_SIZE * chromTransition.OptimizationStep));
            }
            cacheFormat.ChromTransitionSerializer().WriteItems(outStream, transitionsToWrite);
            long locationScores = outStream.Position;
            long locationTextIdBytes = outStream.Position;
            int countScores = (int) (outStreamScores.Position/sizeof (float));
            long locationHeaders;
            int numTextIdBytes;
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
                if (formatVersion >= CacheFormatVersion.Seventeen)
                {
                    chromatogramGroupIds.ToProtoMessage().WriteTo(outStream);
                    numTextIdBytes = (int) (outStream.Position - locationTextIdBytes);
                    locationHeaders = outStream.Position;
                    cacheFormat.ChromGroupHeaderInfoSerializer().WriteItems(outStream, chromatogramEntries);
                }
                else
                {
                    var textIdBytes = new List<byte>();
                    var map = new Dictionary<Target, TextIdLocation>();
                    foreach (var chromGroupHeaderInfo in chromatogramEntries)
                    {
                        chromatogramGroupIds.ConvertToTextId(textIdBytes, map, chromGroupHeaderInfo);
                    }
                    outStream.Write(textIdBytes.ToArray(), 0, textIdBytes.Count);
                    numTextIdBytes = textIdBytes.Count;
                    locationHeaders = outStream.Position;
                    cacheFormat.OldChromGroupHeaderInfoSerializer().WriteItems(outStream, chromatogramEntries
                        .Select(chromGroupHeaderInfo=>chromatogramGroupIds.ConvertToTextId(textIdBytes, map, chromGroupHeaderInfo)));
                    // Nothing should have been added to textIdBytes after the first pass
                    Assume.AreEqual(numTextIdBytes, textIdBytes.Count);
                }
            }
            else
            {
                numTextIdBytes = 0;
                locationHeaders = outStream.Position;
                cacheFormat.OldChromGroupHeaderInfoSerializer().WriteItems(outStream, 
                    chromatogramEntries.Select(chromGroupHeaderInfo=>new ChromGroupHeaderInfo16(chromGroupHeaderInfo, -1, 0)));
            }
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
                numTextIdBytes = numTextIdBytes,
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
                    ChromKey key = new ChromKey(GetChromatogramGroupId(groupInfo),
                        groupInfo.Precursor, IonMobilityFilter.GetIonMobilityFilter(ionMobilityValue, tranInfo.IonMobilityExtractionWidth, groupInfo.CollisionalCrossSection), product, 
                        tranInfo.OptimizationStep, 0, extractionWidth, source, groupInfo.Extractor);

                    int id = i;
                    int rank = -1;
                    yield return new ChromKeyIndices(key, groupInfo.LocationPoints, i, id, rank, j);
                }
            }
        }
        /// <summary>
        /// If the cache format is less than 11, and all of the precursors in the document
        /// are negative, then assume that all of the chromatograms in the .skyd file
        /// should be negative.
        /// </summary>
        private ChromatogramCache UpdateChargeSigns(SrmDocument doc)
        {
            if (_rawData.FormatVersion >= CacheFormatVersion.Eleven)
            {
                return this;
            }

            if (doc.MoleculeTransitionGroups.Any(p => !p.PrecursorMz.IsNegative))
            {
                return this;
            }

            var raw = _rawData;
            raw = raw.ChangeChromatogramEntries(raw.ChromatogramEntries.ChangeAll(
                chromGroupHeader => chromGroupHeader.ChangeChargeToNegative()));
            return ChangeRawData(raw);

        }

        /// <summary>
        /// When reading older format .skyd files, find the groups of ChromTransition's whose product
        /// m/z's differ by <see cref="ChromatogramInfo.OPTIMIZE_SHIFT_SIZE"/>, and set the
        /// <see cref="ChromTransition.OptimizationStep"/> appropriately.
        /// </summary>
        private ChromatogramCache UpdateOptimizationSteps(SrmDocument doc)
        {
            if (!doc.Settings.HasResults || Version >= FORMAT_VERSION_CHROM_TRANSITION_OPTSTEP)
                return this;

            // Determine which files belong to ChromatogramSets with optimization functions.
            var optimizationFunctions = CachedFiles.Select(file =>
                doc.MeasuredResults.Chromatograms
                    .FirstOrDefault(c => c.OptimizationFunction != null && c.ContainsFile(file.FilePath))
                    ?.OptimizationFunction).ToList();
            if (optimizationFunctions.All(optFunc => null == optFunc))
                return this;

            var tolerance = (float)doc.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var chromTransitions = new List<ChromTransition>(_rawData.ChromTransitions);
            var anyChanges = false;

            foreach (var nodePep in doc.Molecules)
            {
                foreach (var nodeTranGroup in nodePep.TransitionGroups)
                {
                    var transitions = nodeTranGroup.Transitions.OrderBy(nodeTran => nodeTran.Mz).ToArray();
                    foreach (var chromIdx in ChromatogramIndexesMatching(nodePep, default, nodeTranGroup.PrecursorMz, tolerance, null))
                    {
                        var info = ChromGroupHeaderInfos[chromIdx];
                        var optimizableRegression = optimizationFunctions[info.FileIndex];
                        if (info.NumTransitions <= 1 || null == optimizableRegression)
                            continue;

                        var curTranIdx = 0;
                        var curTran = transitions[curTranIdx];
                        var nextTran = transitions.Length > 1 ? transitions[curTranIdx + 1] : null;

                        var groupStartIdx = info.StartTransitionIndex;

                        for (var i = info.StartTransitionIndex; i < info.StartTransitionIndex + info.NumTransitions; i++)
                        {
                            var chromTran = _rawData.ChromTransitions[i];
                            while (nextTran != null && Math.Abs(curTran.Mz - chromTran.Product) > Math.Abs(nextTran.Mz - chromTran.Product))
                            {
                                // Matching a new transition.
                                anyChanges |= ProcessOptimizationGroup(curTran, chromTransitions, groupStartIdx, i, optimizableRegression);

                                curTranIdx++;
                                curTran = nextTran;
                                nextTran = curTranIdx < transitions.Length - 1 ? transitions[curTranIdx + 1] : null;

                                groupStartIdx = i;
                            }
                        }

                        anyChanges |= ProcessOptimizationGroup(curTran, chromTransitions, groupStartIdx, info.StartTransitionIndex + info.NumTransitions, optimizableRegression);
                    }
                }
            }

            if (!anyChanges)
            {
                return this;
            }

            return ChangeRawData(_rawData.ChangeChromTransitions(new BlockedArray<ChromTransition>(
                chromTransitions, ChromTransition.SizeOf, ChromTransition.DEFAULT_BLOCK_SIZE)));
        }

        private static bool ProcessOptimizationGroup(TransitionDocNode transitionDocNode, IList<ChromTransition> transitions, int startIdx, int endIdx, OptimizableRegression optimizableRegression)
        {
            if (endIdx - startIdx <= 1)
                return false;

            // Make sure all of the transitions have optimization spacing.
            var prev = transitions[startIdx];
            for (var i = startIdx + 1; i < endIdx; i++)
            {
                var cur = transitions[i];
                if (!ChromatogramInfo.IsOptimizationSpacing(prev.Product, cur.Product))
                {
                    return false;
                }
                prev = cur;
            }

            var productMzs = Enumerable.Range(startIdx, endIdx - startIdx).Select(i =>
                new SignedMz(transitions[i].Product, transitionDocNode.Mz.IsNegative));
            int centerIdx = startIdx + OptStepChromatograms.IndexOfCenterMz(
                transitionDocNode.Mz, productMzs, optimizableRegression.StepCount);
            // Update optimization steps.
            for (var i = startIdx; i < endIdx; i++)
                transitions[i] = transitions[i].ChangeOptimizationStep((short)(i - centerIdx), transitions[centerIdx].Product);

            return true;
        }

        public ChromatogramCache Optimize(string documentPath, IEnumerable<MsDataFileUri> msDataFilePaths, IStreamManager streamManager,
            ILongWaitBroker progress)
        {
            string cachePathOpt = FinalPathForName(documentPath, null);
            return OptimizeToPath(cachePathOpt, msDataFilePaths, streamManager, progress);
        }

        public ChromatogramCache OptimizeToPath(string cachePathOpt,
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
            if (keepFilePaths.Count == CachedFiles.Count)
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

            CacheFormat cacheFormat = CacheFormat.FromVersion(CacheFormatVersion.CURRENT);
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
            List<ResultFileMetaData> listKeepResultFileDatas = null;
            if (_rawData.ResultFileDatas != null && cacheFormat.FormatVersion >= FORMAT_VERSION_RESULT_FILE_DATA)
            {
                listKeepResultFileDatas = new List<ResultFileMetaData>();
            }
            var listKeepTransitions = new BlockedArrayList<ChromTransition>(
                ChromTransition.SizeOf, ChromTransition.DEFAULT_BLOCK_SIZE);
            ChromatogramGroupIds keepGroupIds = new ChromatogramGroupIds();
            var dictKeepTextIdIndices = new Dictionary<ChromatogramGroupId, int>();

            // TODO: Make these first 3 temporary files that delete on close
            using (var fsPeaks = new FileSaver(cachePathOpt + PEAKS_EXT, true))
            using (var fsScans = new FileSaver(cachePathOpt + SCANS_EXT, true))
            using (var fsScores = new FileSaver(cachePathOpt + SCORES_EXT, true))
            using (var fs = new FileSaver(cachePathOpt))
            {
                lock (ReadStream)
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
                        listKeepEntries.Add(new ChromGroupHeaderInfo(lastEntry.Precursor,
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
                            lastEntry.StartTime,
                            lastEntry.EndTime,
                            lastEntry.CollisionalCrossSection, 
                            lastEntry.IonMobilityUnits).ChangeTextIdIndex(keepGroupIds.AddId(GetChromatogramGroupId(lastEntry))));

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

                    if (listKeepResultFileDatas != null)
                    {
                        listKeepResultFileDatas.Add(_rawData.ResultFileDatas[fileIndex]);
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
                    keepGroupIds,
                    ScoreTypes,
                    scoreCount,
                    peakCount, out long scoreValueLocation);

                CommitCache(fs);

                fsPeaks.Stream.Seek(0, SeekOrigin.Begin);
                fsScores.Stream.Seek(0, SeekOrigin.Begin);
                var rawData =
                    new RawData(newCacheHeader, listKeepCachedFiles, listKeepResultFileDatas, listKeepEntries.ToBlockedArray(),
                        listKeepTransitions.ToBlockedArray(), ScoreTypes, scoreValueLocation, keepGroupIds);
                return new ChromatogramCache(cachePathOpt, rawData,
                    // Create a new read stream, for the newly created file
                    streamManager.CreatePooledStream(cachePathOpt, false));
            }
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
            if (_rawData.ChromatogramGroupIds.Count == 0)
            {
                return null;
            }

            var libraryKeyIndexes= new Dictionary<Target, int>();
            List<LibraryKey> libraryKeys = new List<LibraryKey>();
            List<List<int>> chromGroupIndexes = new List<List<int>>();

            for (int i = 0; i < ChromGroupHeaderInfos.Count; i++)
            {
                var entry = ChromGroupHeaderInfos[i];
                var target = GetChromatogramGroupId(entry)?.Target;
                if (target == null)
                {
                    continue;
                }
                int libraryKeyIndex;
                List<int> chromGroupIndexList;
                if (libraryKeyIndexes.TryGetValue(target, out libraryKeyIndex))
                {
                    chromGroupIndexList = chromGroupIndexes[libraryKeyIndex];
                }
                else
                {
                    libraryKeyIndexes.Add(target, libraryKeys.Count);
                    LibraryKey libraryKey;
                    if (!target.IsProteomic)
                    {
                        libraryKey = new MoleculeLibraryKey(target.Molecule.GetSmallMoleculeLibraryAttributes(), Adduct.EMPTY);
                    }
                    else
                    {
                        libraryKey =
                            new PeptideLibraryKey(target.Sequence, 0);
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

        public ChromatogramGroupId GetChromatogramGroupId(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            if (chromGroupHeaderInfo.TextIdIndex == -1)
            {
                return null;
            }

            return _rawData.ChromatogramGroupIds[chromGroupHeaderInfo.TextIdIndex];
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
                    throw new IOException(ResultsResources
                        .ChromatogramGroupInfo_ReadChromatogram_Failure_trying_to_read_points);
                return pointsCompressed;

            });
        }

        private T CallWithStream<T>(Func<Stream, T> func)
        {
            lock (ReadStream)
            {
            var stream = ReadStream.Stream;
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

            using (ReadStream.ReaderWriterLock.GetReadLock())
            {
                var cancellationToken = ReadStream.ReaderWriterLock.CancellationToken;
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
                            cancellationToken.ThrowIfCancellationRequested();
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
                            cancellationToken.ThrowIfCancellationRequested();
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
