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
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    [XmlRoot("measured_results")]
    public sealed class MeasuredResults : Immutable, IXmlSerializable
    {
        public static readonly MeasuredResults EMPTY = new MeasuredResults(new ChromatogramSet[0]);

        private static readonly HashSet<MsDataFileUri> EMPTY_FILES = new HashSet<MsDataFileUri>();

        private ImmutableList<ChromatogramSet> _chromatograms;
        private ImmutableList<bool> _newChromatogramData;
        private ImmutableDictionary<string, int> _dictNameToIndex;
        private ImmutableDictionary<int, int> _dictIdToIndex;
        private HashSet<MsDataFileUri> _setFiles;

        private int _countUnloaded;

        private ChromatogramCache _cacheFinal;
        private ChromatogramCache _cacheRecalc;
        private ImmutableList<ChromatogramCache> _listPartialCaches;
        private ImmutableList<string> _listSharedCachePaths;
        private HashSet<MsDataFileUri> _setCachedFiles = EMPTY_FILES;

        public MeasuredResults(IList<ChromatogramSet> chromatograms, bool disableJoining = false)
        {
            Chromatograms = chromatograms;
            IsJoiningDisabled = disableJoining;

            // The only way to get peaks with areas not normalized by
            // time is to load an older document that was created this way.
            IsTimeNormalArea = true;
        }

        public bool IsEmpty
        {
            get { return _chromatograms == null || _chromatograms.Count == 0; }
        }

        [TrackChildren]
        public IList<ChromatogramSet> Chromatograms
        {
            get { return _chromatograms; }
            private set
            {
                _chromatograms = MakeReadOnly(value.ToArray());
                var dictNameToIndex = new Dictionary<string, int>();
                var dictIdToIndex = new Dictionary<int, int>();
                _setFiles = new HashSet<MsDataFileUri>();
                int count = _chromatograms.Count;
                for (int i = 0; i < count; i++)
                {
                    var set = _chromatograms[i];
                    try
                    {
                        dictNameToIndex.Add(set.Name, i);
                    }
                    catch (ArgumentException argumentException)
                    {
                        throw new ArgumentException(
                            set.Name + @" appears multiple times in the list ('" +
                            string.Join(@"','", value.Select(c => c.Name)) + @"')", argumentException);
                    }

                    dictIdToIndex.Add(set.Id.GlobalIndex, i);
                    foreach (var path in set.MSDataFilePaths)
                        _setFiles.Add(path.GetLocation());
                }
                _dictNameToIndex = new ImmutableDictionary<string, int>(dictNameToIndex);
                _dictIdToIndex = new ImmutableDictionary<int, int>(dictIdToIndex);
                _countUnloaded = _chromatograms.Count(c => !c.IsLoaded);
                HasGlobalStandardArea = MSDataFileInfos.Any(chromFileInfo =>
                    chromFileInfo.ExplicitGlobalStandardArea.HasValue);

                // Pre-allocate empty arrays in case they are needed
                EmptyPeptideResults = new Results<PeptideChromInfo>(new ChromInfoList<PeptideChromInfo>[count]);
                EmptyTransitionGroupResults = new Results<TransitionGroupChromInfo>(new ChromInfoList<TransitionGroupChromInfo>[count]);
                EmptyTransitionResults = new Results<TransitionChromInfo>(new ChromInfoList<TransitionChromInfo>[count]);
                CheckForNewChromatogramData();
            }
        }

        public IDictionary<int, int> IdToIndexDictionary { get { return _dictIdToIndex; } }

        public bool IsTimeNormalArea { get; private set; }

        public Results<PeptideChromInfo> EmptyPeptideResults { get; private set; }
        public Results<TransitionGroupChromInfo> EmptyTransitionGroupResults { get; private set; }
        public Results<TransitionChromInfo> EmptyTransitionResults { get; private set; }

        public CacheFormatVersion? CacheVersion
        {
            get { return _cacheFinal != null ? _cacheFinal.Version : (CacheFormatVersion?) null; }
        }

        public bool IsLoaded
        {
            get
            {
                if (_countUnloaded > 0)
                    return false;
                return (IsJoiningDisabled || (_cacheFinal != null && !_cacheFinal.ReadStream.IsModified));
            }
        }

        public string IsNotLoadedExplained
        {
            get
            {
                // All the chromatogram sets are loaded, and the cache has not been modified
                if (Chromatograms.Contains(c => !c.IsLoaded))
                {
                    return @"Not all chromatogram sets are loaded - " + string.Join(@";", Chromatograms.Where(c => !c.IsLoaded).Select(i => i.IsLoadedExplained()));
                }
                if (!IsJoiningDisabled)
                {
                    if (_cacheFinal == null)
                        return @"No final cache";
                    if (_cacheFinal.ReadStream.IsModified)
                        return string.Format(@"Cache has been modified ({0})", _cacheFinal.ReadStream.ModifiedExplanation);
                }
                return null;
            }
        }

        public bool IsJoiningDisabled { get; private set; }
        public bool IsResultsUpdateRequired { get; private set; }
        public bool IsDeserialized { get; private set; }
        public bool HasGlobalStandardArea { get; private set; }

        public bool IsChromatogramSetLoaded(int index)
        {
            return Chromatograms[index].IsLoaded;
        }

        public IEnumerable<IPooledStream> ReadStreams
        {
            get { return Caches.Select(cache => cache.ReadStream); }
        }

        public IEnumerable<string> CachePaths
        {
            get { return Caches.Select(cache => cache.CachePath); }
        }

        public IEnumerable<MsDataFileUri> CachedFilePaths
        {
            get { return Caches.SelectMany(cache => cache.CachedFilePaths); }
        }

        public IEnumerable<ChromCachedFile> CachedFileInfos
        {
            get { return Caches.SelectMany(cache => cache.CachedFiles); }
        }

        public bool IsCachedFile(MsDataFileUri filePath)
        {
            return _setCachedFiles.Contains(filePath.GetLocation()); // Search filename only, ignoring centroiding, combineIM etc
        }

        public IEnumerable<Type> CachedScoreTypes
        {
            get
            {
                return Caches.SelectMany(cache => cache.ScoreTypes.AsCalculatorTypes()).Distinct();
            }
        }

        private IEnumerable<ChromatogramCache> Caches
        {
            get
            {
                if (_cacheFinal != null)
                    yield return _cacheFinal;
                else if (_listPartialCaches != null)
                {
                    foreach (var cache in _listPartialCaches)
                        yield return cache;
                }
            }
        }

        /// <summary>
        /// List of caches with _cacheRecalc as backstop during reloading
        /// </summary>
        private IEnumerable<ChromatogramCache> CachesEx
        {
            get
            {
                foreach (var cache in Caches)
                    yield return cache;
                if (_cacheRecalc != null)
                    yield return _cacheRecalc;
            }
        }

        private IList<string> SharedCachePaths
        {
            get { return _listSharedCachePaths; }
            set { _listSharedCachePaths = MakeReadOnly(value); }
        }

        private bool IsSharedCache(ChromatogramCache cache)
        {
            return SharedCachePaths != null && SharedCachePaths.Contains(cache.CachePath);
        }

        private bool IsCachePath(string cachePath)
        {
            return (_cacheFinal != null && Equals(cachePath, _cacheFinal.CachePath)) ||
                (_listPartialCaches != null && _listPartialCaches.Contains(cache => Equals(cachePath, cache.CachePath)));
        }

        /// <summary>
        /// The unique set of file paths represented in all replicates,
        /// in the order they appear.
        /// </summary>
        public IEnumerable<MsDataFileUri> MSDataFilePaths
        {
            get { return Chromatograms.SelectMany(chromSet => chromSet.MSDataFilePaths).Distinct(); }
        }

        public IEnumerable<ChromFileInfo> MSDataFileInfos
        {
            get
            {
                return Chromatograms.SelectMany(chromSet =>
                    chromSet.MSDataFileInfos).Distinct(new RefCompareChromFileInfo());
            }
        }

        public bool IsDataFilePath(MsDataFileUri path)
        {
            return _setFiles.Contains(path.GetLocation());
        }

        public ChromFileInfo GetChromFileInfo<TChromInfo>(Results<TChromInfo> results, int replicateIndex)
            where TChromInfo : ChromInfo
        {
            ChromInfoList<TChromInfo> replicateChromInfos = default(ChromInfoList<TChromInfo>);
            if (results != null && replicateIndex >= 0 && replicateIndex < results.Count)
            {
                replicateChromInfos = results[replicateIndex];
            }
            if (!replicateChromInfos.IsEmpty)
            {
                var chromatograms = Chromatograms[replicateIndex];
                foreach (var replicateChromInfo in replicateChromInfos)
                {
                    ChromFileInfo chromFileInfo = chromatograms.GetFileInfo(replicateChromInfo.FileId);
                    if (chromFileInfo != null)
                    {
                        return chromFileInfo;
                    }
                }
            }
            return null;
        }

        public ChromFileInfo GetChromFileInfo(MsDataFileUri filePath)
        {
            return Chromatograms.Select(chromatogramSet => chromatogramSet.GetFileInfo(filePath))
                                .FirstOrDefault(fileInfo => fileInfo != null);
        }

        private class RefCompareChromFileInfo : IEqualityComparer<ChromFileInfo>
        {
            public bool Equals(ChromFileInfo x, ChromFileInfo y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(ChromFileInfo obj)
            {
                return obj.Id.GlobalIndex.GetHashCode();
            }
        }

        public ChromSetFileMatch FindMatchingMSDataFile(MsDataFileUri filePathFind)
        {
            // First look for an exact match
            var exactMatch = FindExactNameMatchingMSDataFile(filePathFind);
            if (exactMatch != null)
                return exactMatch;
            // Then look for a basename match
            string sampleName = filePathFind.GetSampleName();
            int fileOrder = 0;
            foreach (ChromatogramSet chromSet in Chromatograms)
            {
                string fileBasename = filePathFind.GetFileNameWithoutExtension();
                foreach (var filePath in chromSet.MSDataFilePaths)
                {
                    if (sampleName == null || sampleName == filePath.GetSampleName())
                    {
                        if (IsBaseNameMatch(filePath.GetFileNameWithoutExtension(), fileBasename))
                            return new ChromSetFileMatch(chromSet, filePath, fileOrder);
                    }
                    fileOrder++;
                }
            }
            return null;
        }

        public ChromSetFileMatch FindMatchingOrExistingMSDataFile(MsDataFileUri filePathFind)
        {
            // First look for an exact match, ignoring any details like centroid or combineIMS settins
            var exactMatch = FindExactNameMatchingMSDataFile(filePathFind);
            if (exactMatch != null)
                return exactMatch;
            // Then look for an existing file
            int fileOrder = 0;
            foreach (ChromatogramSet chromSet in Chromatograms)
            {
                foreach (var filePath in chromSet.MSDataFilePaths)
                {
                    var filePathMatching = ChromatogramSet.GetExistingDataFilePath(filePathFind.ToString(), filePath);
                    if (filePathMatching != null)
                        return new ChromSetFileMatch(chromSet, filePathMatching, fileOrder);
                    fileOrder++;
                }
            }
            return null;
        }

        /// <summary>
        /// Look for this file in the list, ignoring details like centroiding, combineIonMobilitySpectra etc
        /// </summary>
        private ChromSetFileMatch FindExactNameMatchingMSDataFile(MsDataFileUri fileUri)
        {
            var filePathFind = fileUri.GetFilePath();
            string sampleName = fileUri.GetSampleName();
            int fileOrder = 0;
            foreach (ChromatogramSet chromSet in Chromatograms)
            {
                foreach (var filePath in chromSet.MSDataFilePaths)
                {
                    if (Equals(filePath.GetFilePath(), filePathFind))
                    {
                        if (sampleName == null || sampleName.Equals(filePath.GetSampleName()))
                        {
                            return new ChromSetFileMatch(chromSet, filePath, fileOrder);
                        }
                    }
                    fileOrder++;
                }
            }
            return null;
        }

        public static bool IsBaseNameMatch(string baseName1, string baseName2)
        {
            return IsPrefixToExtension(baseName1, baseName2) ||
                   IsPrefixToExtension(baseName2, baseName1);
        }

        private static bool IsPrefixToExtension(string name, string prefix)
        {
            // Do this complex check, because ETH has a pipeline that produces
            // data files with the extension <basename>.c.mzXML.  So, this needs
            // to be able to match <basename> with <basename>.c, and Vanderbilt
            // has a pipeline that generates mzML files all uppercase
            if (!name.ToLower().StartsWith(prefix.ToLower()))
                return false;
            if (name.Length == prefix.Length || name[prefix.Length] == '.')
                return true;
            // Check for special suffixes we know get added to the basename by other tools
            string suffix = name.Substring(prefix.Length);
            if (suffix[0] == '_' && IsUnderscoreSuffix(suffix))
                return true;
            return false;
        }

        public static bool IsUnderscoreSuffix(string suffix)
        {
            string suffixLower = suffix.ToLowerInvariant();
            return
                // Waters MSe
                suffixLower.EndsWith(@"_ia_final_fragment") ||
                suffixLower.EndsWith(@"_final_fragment") ||
                // MSFragger
                suffixLower.EndsWith(@"_calibrated") ||
                suffixLower.EndsWith(@"_uncalibrated");
        }

// ReSharper disable MemberCanBeMadeStatic.Local
        private bool IsValidCache(ChromatogramCache cache, bool current)
        {
            if (!cache.IsSupportedVersion)
                return false;
            // Not if missing any files from the replicates
//            if (MSDataFilePaths.Contains(path => !cache.CachedFiles.Contains(cachedFile =>
//                    Equals(path, cachedFile.FilePath))))
//                return false;
            // No longer ever check modified times of files, since removing .skyd files
            // and rebuilding is no longer trivial.
//            return !current || cache.IsCurrentDisk;
            return true;
        }
// ReSharper restore MemberCanBeMadeStatic.Local

        public MeasuredResults CommitCacheFile(FileSaver fs)
        {
            if (!IsLoaded)
                throw new InvalidOperationException(Resources.MeasuredResults_CommitCacheFile_The_chromatogram_cache_must_be_loaded_before_it_can_be_changed);

            _cacheFinal.CommitCache(fs);
            // Now the cach needs to be reloaded.
            return ChangeProp(ImClone(this), im => im.SetClonedCacheState(null));
        }

        public MeasuredResults OptimizeCache(string documentPath, IStreamManager streamManager, ILongWaitBroker progress = null)
        {
            // No optimizing until we have a joined final cache
            if (IsJoiningDisabled)
                return this;

            if (!IsLoaded)
                throw new InvalidOperationException(Resources.MeasuredResults_OptimizeCache_The_chromatogram_cache_must_be_loaded_before_it_is_optimized);

            var cacheOptimized = _cacheFinal.Optimize(documentPath, MSDataFilePaths, streamManager, progress);
            if (ReferenceEquals(cacheOptimized, _cacheFinal))
                return this;
            return ChangeProp(ImClone(this), im => im.SetClonedCacheState(cacheOptimized));
        }

        /// <summary>
        /// Used on a clone during a change operation on a cloned object to set the state of its
        /// <see cref="ChromatogramCache"/> files
        /// </summary>
        private void SetClonedCacheState(ChromatogramCache cacheFinal, IList<ChromatogramCache> partialCaches = null)
        {
            _cacheFinal = cacheFinal;
            if (_cacheRecalc != null)
            {
                if (partialCaches != null &&
                    partialCaches.Any(partialCache => partialCache.CachePath == _cacheRecalc.CachePath))
                {
                    // If any of the partial caches has the same cache file path as _cacheRecalc,
                    // it means that the file has been replaced on disk and _cacheRecalc can only throw
                    // FileModifiedException
                    _cacheRecalc = null;
                }
            }
            if (_cacheFinal != null)
            {
                _cacheRecalc = null;
            }
            _listPartialCaches = MakeReadOnly(partialCaches);
            _setCachedFiles = new HashSet<MsDataFileUri>(CachedFilePaths.Select(p => p.GetLocation()));
            CheckForNewChromatogramData();
        }

        private IDictionary<MsDataFileUri, DateTime> GetImportTimesFromCacheFiles()
        {
            return CollectionUtil.SafeToDictionary(Caches.SelectMany(cache => cache.CachedFiles)
                .Where(file => file.ImportTime.HasValue).Select(file =>
                    new KeyValuePair<MsDataFileUri, DateTime>(file.FilePath, file.ImportTime.Value)));
        }

        /// <summary>
        /// Sets _newChromatogramData to indicate which replicates have files whose ImportTime
        /// is different from what is in the cache file.
        /// </summary>
        private void CheckForNewChromatogramData()
        {
            var changedReplicateIndexes = new HashSet<int>();
            var importTimes = GetImportTimesFromCacheFiles();
            for (int replicateIndex = 0; replicateIndex < Chromatograms.Count; replicateIndex++)
            {
                var chromatogramSet = Chromatograms[replicateIndex];
                foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                {
                    if (importTimes.TryGetValue(chromFileInfo.FilePath, out DateTime cachedImportTime))
                    {
                        if (!Equals(cachedImportTime, chromFileInfo.ImportTime))
                        {
                            changedReplicateIndexes.Add(replicateIndex);
                            break;
                        }
                    }
                }
            }

            if (changedReplicateIndexes.Any())
            {
                _newChromatogramData = ImmutableList.ValueOf(Enumerable.Range(0, Chromatograms.Count)
                    .Select(changedReplicateIndexes.Contains));
            }
            else
            {
                _newChromatogramData = null;
            }
        }

        public MeasuredResults UpdateCaches(string documentPath, MeasuredResults resultsCache)
        {
            // Clone the current node, and update its cache properties.
            var results = ImClone(this);

            // Make sure peaks are adjusted as chromatograms are rescored
            if (resultsCache._cacheRecalc != null &&
                resultsCache._listPartialCaches != null)
            {
                results.Chromatograms = results.GetRescoredChromatograms(resultsCache);
            }

            results.UpdateClonedCaches(resultsCache);

            results.IsResultsUpdateRequired = resultsCache.IsResultsUpdateRequired;
            results.IsDeserialized = false;

            string cachePath = ChromatogramCache.FinalPathForName(documentPath, null);
            var cachedFiles = results.CachedFileInfos.Distinct(new PathComparer<ChromCachedFile>()).ToArray();
            var dictCachedFiles = cachedFiles.ToDictionary(cachedFile => cachedFile.FilePath.GetLocation()); // Ignore centroiding, combineIMS etc for key purposes
            var enumCachedNames = cachedFiles.Select(cachedFile => cachedFile.FilePath.GetFileName());
            var setCachedFileNames = new HashSet<string>(enumCachedNames);
            var chromatogramSets = new List<ChromatogramSet>();
            foreach (var chromSet in results.Chromatograms)
            {
                chromatogramSets.Add(chromSet.ChangeFileCacheFlags(
                    dictCachedFiles, setCachedFileNames, cachePath));
            }

            if (!ArrayUtil.ReferencesEqual(chromatogramSets, results.Chromatograms))
                results.Chromatograms = chromatogramSets;

            return results;
        }

        private void UpdateClonedCaches(MeasuredResults resultsCache)
        {
            var cacheFinal = resultsCache._cacheFinal;
            IList<ChromatogramCache> partialCaches = resultsCache._listPartialCaches;

            // Preserve partial caches in this, if none to add and not final
            if (partialCaches == null)
            {
                if (cacheFinal == null)
                    partialCaches = _listPartialCaches;
            }
            // If both sets have partial caches, merge them
            else if (_listPartialCaches != null)
            {
                // CONSIDER: It is hypothetically possible that the two sets
                //           may contain overlapping caches with multiple
                //           cached files that are not the same, but very
                //           unlikely.  So, ignored for the moment.
                var listUnionCaches = new List<ChromatogramCache>(
                    _listPartialCaches.Union(partialCaches, ChromatogramCache.PathComparer));
                if (listUnionCaches.Count != partialCaches.Count)
                {
                    partialCaches = MakeReadOnly(listUnionCaches);
                }
            }
            SetClonedCacheState(cacheFinal, partialCaches);
        }

        public MeasuredResults ApplyChromatogramSetRemovals(MeasuredResults resultsLoad, MeasuredResults resultsPrevious)
        {
            // If nothing changed, then leave the current results alone
            if (ReferenceEquals(resultsLoad.Chromatograms, resultsPrevious.Chromatograms))
                return this;

            var setRemove = new HashSet<int>(resultsPrevious.Chromatograms.Select(c => c.Id.GlobalIndex));
            foreach (var chromatogramSet in resultsLoad.Chromatograms)
                setRemove.Remove(chromatogramSet.Id.GlobalIndex);
            var chromatogramSetsNew = Chromatograms.Where(c => !setRemove.Contains(c.Id.GlobalIndex)).ToArray();
            if (chromatogramSetsNew.Length == 0)
                return null;
            return ChangeChromatograms(chromatogramSetsNew);
        }

        /// <summary>
        /// Increments rescore count on all <see cref="ChromatogramSet"/> objects for
        /// which there is newly cached data.
        /// </summary>
        private IList<ChromatogramSet> GetRescoredChromatograms(MeasuredResults resultsNew)
        {
            var listNewCaches = GetNewCaches(resultsNew);
            return resultsNew.Chromatograms.Select(c => IsCachedChromatogramSet(c, listNewCaches) ? c.ChangeRescoreCount() : c).ToArray();
        }

        private IList<ChromatogramCache> GetNewCaches(MeasuredResults resultsNew)
        {
            if (_listPartialCaches == null)
                return resultsNew._listPartialCaches;
            var setCaches = new HashSet<ChromatogramCache>(_listPartialCaches);
            return resultsNew._listPartialCaches.Where(c => !setCaches.Contains(c)).ToArray();
        }

        private bool IsCachedChromatogramSet(ChromatogramSet chromatogramSet, IList<ChromatogramCache> listNewCaches)
        {
            return listNewCaches.Any(
                cache => chromatogramSet.MSDataFilePaths.Any(
                    msDataFilePath => cache.CachedFilePaths.Contains(msDataFilePath)));
        }

        public bool TryGetChromatogramSet(string name, out ChromatogramSet chromatogramSet, out int index)
        {
            if (!_dictNameToIndex.TryGetValue(name, out index))
                index = -1;
            return ChromatogramSetForIndex(index, out chromatogramSet);
        }

        public bool TryGetChromatogramSet(int setId, out ChromatogramSet chromatogramSet, out int index)
        {
            if (!_dictIdToIndex.TryGetValue(setId, out index))
                index = -1;
            return ChromatogramSetForIndex(index, out chromatogramSet);
        }

        private bool ChromatogramSetForIndex(int index, out ChromatogramSet chromatogramSet)
        {
            chromatogramSet = (index != -1 ? _chromatograms[index] : null);
            return index != -1;            
        }

        public MsDataFileScanIds LoadMSDataFileScanIds(MsDataFileUri dataFilePath, out ChromCachedFile cachedFile)
        {
            foreach (var cache in Caches)
            {
                int fileIndex = cache.CachedFiles.IndexOf(f => Equals(f.FilePath, dataFilePath));
                if (fileIndex != -1)
                {
                    cachedFile = cache.CachedFiles[fileIndex];
                    return cache.LoadMSDataFileScanIds(fileIndex);
                }
            }

            cachedFile = null;
            return null;
        }

        public bool HasAllIonsChromatograms
        {
            get { return Caches.Any(cache => cache.HasAllIonsChromatograms); }
        }

        /// <summary>
        /// Returns true if the data in the chromatogram cache is newer than what the SrmDocument
        /// knows about.
        /// </summary>
        public bool HasNewChromatogramData(int resultsIndex)
        {
            if (_newChromatogramData == null || resultsIndex < 0 || resultsIndex >= _newChromatogramData.Count)
            {
                return false;
            }
            return _newChromatogramData[resultsIndex];
        }

        /// <summary>
        /// Set the <see cref="ChromFileInfo.ImportTime"/> values to the times from
        /// the ChromatogramCache so that <see cref="HasNewChromatogramData"/> will
        /// return false.
        /// </summary>
        public MeasuredResults UpdateImportTimes()
        {
            if (_newChromatogramData == null)
            {
                return this;
            }

            var cacheFileImportTimes = GetImportTimesFromCacheFiles();
            var newChromatograms = new List<ChromatogramSet>();
            for (int resultsIndex = 0; resultsIndex < Chromatograms.Count; resultsIndex++)
            {
                var chromatograms = Chromatograms[resultsIndex];
                if (!HasNewChromatogramData(resultsIndex))
                {
                    newChromatograms.Add(chromatograms);
                    continue;
                }

                var newFileInfos = new List<ChromFileInfo>();
                foreach (var chromFileInfo in chromatograms.MSDataFileInfos)
                {
                    if (cacheFileImportTimes.TryGetValue(chromFileInfo.FilePath, out DateTime importTime))
                    {
                        newFileInfos.Add(chromFileInfo.ChangeImportTime(importTime));
                    }
                    else
                    {
                        newFileInfos.Add(chromFileInfo.ChangeImportTime(null));
                    }
                }
                newChromatograms.Add(chromatograms.ChangeMSDataFileInfos(newFileInfos));
            }

            return ChangeChromatograms(newChromatograms);
        }

        /// <summary>
        /// Sets the ImportTimes on all of the ChromFileInfo's to null so that they will not
        /// be written out when saving a .sky in an earlier format.
        /// </summary>
        public MeasuredResults ClearImportTimes()
        {
            return ChangeChromatograms(Chromatograms.Select(chrom => chrom.ChangeMSDataFileInfos(
                chrom.MSDataFileInfos.Select(info => info.ChangeImportTime(null)).ToList())).ToList());
        }

        public IEnumerable<string> QcTraceNames
        {
            get
            {
                var qcTraceInfos = Caches.SelectMany(cache=>cache.ChromGroupHeaderInfos
                                                                 .Where(header => header.Flags.HasFlag(ChromGroupHeaderInfo.FlagValues.extracted_qc_trace))
                                                                 .Select(header => cache.LoadChromatogramInfo(header)));
                var qcTraceNames = qcTraceInfos.Select(info => info.TextId).Distinct().ToList();
                qcTraceNames.Sort();
                return qcTraceNames;
            }
        }

        public bool TryLoadAllIonsChromatogram(int index,
                                               ChromExtractor extractor,
                                               bool loadPoints,
                                               out ChromatogramGroupInfo[] infoSet)
        {
            return TryLoadAllIonsChromatogram(_chromatograms[index], extractor, loadPoints, out infoSet);
        }

        public bool TryLoadAllIonsChromatogram(ChromatogramSet chromatogram,
                                               ChromExtractor extractor,
                                               bool loadPoints,
                                               out ChromatogramGroupInfo[] infoSet)
        {
            var listChrom = new List<ChromatogramGroupInfo>();
            foreach (var cache in CachesEx)
            {
                foreach (var chromInfo in cache.LoadAllIonsChromatogramInfo(extractor, chromatogram))
                {
                    listChrom.Add(chromInfo);
                }
            }
            infoSet = listChrom.ToArray();
            return infoSet.Length > 0;
        }
        
        public bool TryLoadChromatogram(int index,
                                        PeptideDocNode nodePep,
                                        TransitionGroupDocNode nodeGroup,
                                        float tolerance,
                                        out ChromatogramGroupInfo[] infoSet)
        {
            return TryLoadChromatogram(_chromatograms[index], nodePep, nodeGroup, tolerance, out infoSet);
        }

        private static readonly ChromatogramGroupInfo[] EMPTY_GROUP_INFOS = Array.Empty<ChromatogramGroupInfo>();

        public bool TryLoadChromatogram(ChromatogramSet chromatogram,
                                        PeptideDocNode nodePep,
                                        TransitionGroupDocNode nodeGroup,
                                        float tolerance,
                                        out ChromatogramGroupInfo[] infoSet)
        {
            IEnumerable<ChromatogramGroupInfo> infoEnum = Enumerable.Empty<ChromatogramGroupInfo>();
            foreach (var cache in CachesEx)
            {
                if (_cacheFinal == null && cache.CachedFiles.Count == 1)
                {
                    // If the cache has only one file in it, it's likely to be a temporary one.
                    // Skip the cache if it does not contain any files of interest.
                    if (!chromatogram.ContainsFile(cache.CachedFiles[0].FilePath))
                    {
                        continue;
                    }
                }

                infoEnum = infoEnum.Concat(cache.LoadChromatogramInfos(nodePep, nodeGroup, tolerance, chromatogram));
            }

            infoSet = infoEnum.ToArray();
            // Short-circuit further processing for common case in label free data
            if (infoSet.Length == 1)
            {
                return true;
            }

            var listChrom = GetMatchingChromatograms(chromatogram, nodePep, nodeGroup, tolerance, infoSet);
            if (listChrom.Count == 0)
            {
                infoSet = EMPTY_GROUP_INFOS;
                return false;
            }
            infoSet = listChrom.ToArray();
            return true;
        }

        private IList<ChromatogramGroupInfo> GetMatchingChromatograms(ChromatogramSet chromatogram,
            PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup,
            float tolerance, 
            IEnumerable<ChromatogramGroupInfo> infoSet)
        {
            // Add precursor matches to a list, if they match at least 1 transition
            // in this group, and are potentially the maximal transition match.

            // Using only the maximum works well for the case where there are 2
            // precursors in the same document that match a single entry.
            // TODO: But it messes up when there are 2 sets of transitions for
            //       the same precursor covering different numbers of transitions.
            //       Skyline never creates this case, but it has been reported
            // In small molecule SRM, it's not at all unusual to have the same Q1>Q3
            // pair repeatedly, at different retention times, so we use explicit RT to disambiguate if available
            int maxTranMatch = 1;
            // If the chromatogram set has an optimization function, then the number
            // of matching chromatograms per transition is a reflection of better
            // matching.  Otherwise, we only expect one match per transition.
            bool multiMatch = chromatogram.OptimizationFunction != null;
            List<ChromatogramGroupInfo> listChrom = new List<ChromatogramGroupInfo>();
            foreach (var chromInfo in infoSet)
            {
                int tranMatch = chromInfo.MatchTransitions(nodePep, nodeGroup, tolerance, multiMatch);
                // CONSIDER: This is pretty tricky code, and we are currently favoring
                //           peak proximity to explicit retention time over number of matching
                //           transitions.
                if (tranMatch >= maxTranMatch)
                {
                    // If new maximum, clear anything collected at the previous maximum
                    if (tranMatch > maxTranMatch)
                        listChrom.Clear();

                    maxTranMatch = tranMatch;
                    listChrom.Add(chromInfo);
                }
            }
            // If more than one value was found, make a final pass to ensure that there
            // is only one precursor match per file.
            if (listChrom.Count > 1)
            {
                var precursorMz = nodeGroup.PrecursorMz;
                var listChromFinal = new List<ChromatogramGroupInfo>();
                foreach (var chromInfo in listChrom)
                {
                    // Polarity matching should have happened in the TryLoad
                    Assume.IsTrue(chromInfo.PrecursorMz.IsNegative == precursorMz.IsNegative);
                    var filePath = chromInfo.FilePath;
                    int fileIndex = listChromFinal.IndexOf(info => Equals(filePath, info.FilePath));
                    if (fileIndex == -1)
                        listChromFinal.Add(chromInfo);
                    // Use the entry with the m/z closest to the target
                    else if (Math.Abs(precursorMz - chromInfo.PrecursorMz) <
                             Math.Abs(precursorMz - listChromFinal[fileIndex].PrecursorMz))
                    {
                        listChromFinal[fileIndex] = chromInfo;
                    }
                }
                listChrom = listChromFinal;
            }

            return listChrom;
        }

        public List<IList<ChromatogramGroupInfo>> LoadChromatogramsForAllReplicates(PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup,
            float tolerance)
        {
            var chromatogramGroupInfosByFile = CachesEx.SelectMany(cache => cache.LoadChromatogramInfos(nodePep, nodeGroup, tolerance, null))
                .ToLookup(chromGroupInfo=>chromGroupInfo.FilePath.GetLocation());
            var result = new List<IList<ChromatogramGroupInfo>>();
            foreach (var chromatogramSet in Chromatograms)
            {
                var listChrom = chromatogramSet.MSDataFileInfos
                    .SelectMany(fileInfo => chromatogramGroupInfosByFile[fileInfo.FilePath.GetLocation()]).ToList();
                result.Add(GetMatchingChromatograms(chromatogramSet, nodePep, nodeGroup, tolerance, listChrom));
            }
            Assume.AreEqual(Chromatograms.Count, result.Count);
            return result;
        }

        public bool ContainsChromatogram(string name)
        {
            return _dictNameToIndex.ContainsKey(name);
        }

        public void Load(SrmDocument document, string documentPath, MultiFileLoadMonitor loadMonitor, MultiFileLoader multiFileLoader,
            Action<string, MeasuredResults, MeasuredResults> completed)
        {
            var loader = new Loader(ImClone(this), document, documentPath, loadMonitor, multiFileLoader, completed);
            loader.Load();
        }

        public ChromCacheMinimizer GetChromCacheMinimizer(SrmDocument document)
        {
            if (!IsLoaded)
            {
                return null;
            }
            return new ChromCacheMinimizer(document, _cacheFinal);
        }

        #region Property change methods

        public MeasuredResults ClearDeserialized()
        {
            return ChangeProp(ImClone(this), im => im.IsDeserialized = false);
        }

        public MeasuredResults ChangeIsJoiningDisabled(bool prop)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.IsJoiningDisabled = prop;
                if (!prop)
                    im.IsResultsUpdateRequired = true;
            });
        }

        public MeasuredResults ChangeChromatograms(IList<ChromatogramSet> prop)
        {
            var results = ChangeProp(ImClone(this), im => im.Chromatograms = prop);
            if (RequiresCacheUpdate(results))
            {
                // Cache is no longer final
                var listPartialCaches = new List<ChromatogramCache>();
                if (_listPartialCaches != null)
                    listPartialCaches.AddRange(results._listPartialCaches);
                // Check for any caches that contain files no longer in the set, or that
                // have changed, and get rid of them
                foreach (var cache in listPartialCaches.ToArray())
                {
                    if (!IsValidCache(cache, true))
                    {
                        cache.ReadStream.CloseStream();
                        var cachePath = cache.CachePath;
                        FileEx.SafeDelete(cachePath, true);
                        listPartialCaches.Remove(cache);
                    }
                }
                // Always take the existing final cache forward.  It will be optimized
                // on save.  Unless it has already been modified, in which case it needs
                // to be reloaded to know what is now in it.
                if (results._cacheFinal != null && !results._cacheFinal.ReadStream.IsModified)
                    listPartialCaches.Insert(0, results._cacheFinal);
                results.SetClonedCacheState(null, listPartialCaches.Count != 0 ? listPartialCaches : null);
            }
            return results;
        }

        public MeasuredResults AddDataFile(MsDataFileUri dataFile, IEnumerable<string> replicateNames)
        {
            var newChromatograms = new List<ChromatogramSet>(Chromatograms);
            foreach (var replicateName in replicateNames)
            {
                var name = replicateName;
                int indexSet = newChromatograms.IndexOf(set => set.Name == name);
                if (indexSet == -1)
                    newChromatograms.Add(new ChromatogramSet(name, new[] { dataFile }));
                else
                {
                    var chromSet = newChromatograms[indexSet];
                    var listPaths = chromSet.MSDataFilePaths.ToList();
                    listPaths.Add(dataFile);
                    newChromatograms[indexSet] = chromSet.ChangeMSDataFilePaths(listPaths);
                }
            }
            return ChangeChromatograms(newChromatograms);
        }

        private bool RequiresCacheUpdate(MeasuredResults results)
        {
            var dicExistingIdToSet = new Dictionary<int, ChromatogramSet>();
            foreach (var chromSet in Chromatograms)
                dicExistingIdToSet.Add(chromSet.Id.GlobalIndex, chromSet);
            foreach (var chromSet in results.Chromatograms)
            {
                ChromatogramSet chromSetExisting;
                // If there is a new chromatogram set, then update the cache
                if (!dicExistingIdToSet.TryGetValue(chromSet.Id.GlobalIndex, out chromSetExisting))
                    return true;
                // If a previously existing set has changed files, then updat the cache
                if (!ArrayUtil.EqualsDeep(chromSet.MSDataFilePaths.ToArray(),
                                          chromSetExisting.MSDataFilePaths.ToArray()))
                    return true;
            }
            return false;
        }

        public MeasuredResults FilterFiles(Func<ChromFileInfo, bool> selectFilesToKeepFunc)
        {
            var keepChromatograms = new List<ChromatogramSet>();
            foreach (var chromSet in Chromatograms)
            {
                var keepFiles = chromSet.MSDataFileInfos.Where(selectFilesToKeepFunc).ToList();
                if (keepFiles.Count != 0)
                {
                    if (keepFiles.Count == chromSet.FileCount)
                        keepChromatograms.Add(chromSet);
                    else
                        keepChromatograms.Add(chromSet.ChangeMSDataFileInfos(keepFiles));
                }
            }

            // If nothing changed, don't create a new document instance
            if (ArrayUtil.ReferencesEqual(keepChromatograms, Chromatograms))
                return this;

            return keepChromatograms.Count > 0
                ? ChangeChromatograms(keepChromatograms)
                : null;
        }

        public MeasuredResults ChangeSharedCachePaths(params string[] prop)
        {
            return ChangeProp(ImClone(this), im => im.SharedCachePaths = prop);
        }

        public enum MergeAction { remove, merge_names, merge_indices, add }

        public static MeasuredResults MergeResults(MeasuredResults resultsOrig, MeasuredResults resultsImport,
            string documentPath, MergeAction mergeAction, out MeasuredResults resultsBase)
        {
            // Code below expects 'remove' when there are no measured results to import
            if (resultsImport == null)
                mergeAction = MergeAction.remove;

            MeasuredResults resultsNew;
            if (resultsOrig != null)
            {
                resultsNew = resultsOrig.MergeResults(resultsImport, documentPath, mergeAction, out resultsBase);
            }
            else
            {
                resultsBase = resultsImport;

                if (mergeAction == MergeAction.remove)
                    resultsNew = null;
                // ReSharper disable once PossibleNullReferenceException
                else if (resultsBase._cacheFinal != null || resultsBase._listPartialCaches != null)
                    resultsNew = resultsBase;
                else
                {
                    resultsNew = resultsBase.ChangeSharedCachePaths(
                        ChromatogramCache.FinalPathForName(documentPath, null));
                }
            }
            return resultsNew;
        }

        public MeasuredResults MergeResults(MeasuredResults resultsImport, string documentPath,
            MergeAction mergeAction, out MeasuredResults resultsBase)
        {
            // To remove results from an import document, use the existing results and
            // a settings diff with no previous document.
            if (mergeAction == MergeAction.remove)
            {
                resultsBase = resultsImport;
                return this;
            }

            // Calculate list of caches that cover the merged results
            var listPartialCaches = new List<ChromatogramCache>();
            if (_listPartialCaches != null)
                listPartialCaches.AddRange(_listPartialCaches);
            else if (_cacheFinal != null)
                listPartialCaches.Add(_cacheFinal);
            var listSharedCachePaths = new List<string>();
            if (_listSharedCachePaths != null)
                listSharedCachePaths.AddRange(_listSharedCachePaths);
            if (resultsImport._listPartialCaches != null)
            {
                listPartialCaches.AddRange(resultsImport._listPartialCaches);
                listSharedCachePaths.AddRange(resultsImport._listPartialCaches.Select(cache => cache.CachePath));
            }
            else if (resultsImport._cacheFinal != null)
            {
                listPartialCaches.Add(resultsImport._cacheFinal);
                listSharedCachePaths.Add(resultsImport._cacheFinal.CachePath);
            }
            else
            {
                listSharedCachePaths.Add(ChromatogramCache.FinalPathForName(documentPath, null));
            }

            var results = ImClone(this);
            results.SetClonedCacheState(null, listPartialCaches);
            results._listSharedCachePaths = MakeReadOnly(listSharedCachePaths);

            // Calculate the replicates to cover the merge
            // TODO: Deal with the case of adding files to replicate that have chromatograms for already covered nodes
            var dictNameToChromatograms = results.Chromatograms.ToDictionary(chrom => chrom.Name);
            List<ChromatogramSet> chromatogramsBase, chromatogramsNew;
            if (mergeAction == MergeAction.merge_indices)
            {
                chromatogramsBase = new List<ChromatogramSet>();
                chromatogramsNew = new List<ChromatogramSet>();
                // Enumerate all possible indexes
                int len = Math.Max(results.Chromatograms.Count, resultsImport.Chromatograms.Count);
                for (int i = 0; i < len; i++)
                {
                    // Add new replicates with no matching import
                    if (i >= resultsImport.Chromatograms.Count)
                    {
                        chromatogramsNew.Add(results.Chromatograms[i]);
                    }
                    // Add imported replicates with no matching new replicate
                    else if (i >= results.Chromatograms.Count)
                    {
                        var chromatogramSetImport = EnsureUniqueSetName(resultsImport.Chromatograms[i],
                            dictNameToChromatograms.Keys);
                        chromatogramsBase.Add(chromatogramSetImport);
                        chromatogramsNew.Add(chromatogramSetImport);
                    }
                    // Merge replicates found in both new and import
                    else
                    {
                        MergeChromatogramSets(results.Chromatograms[i], resultsImport.Chromatograms[i],
                            chromatogramsNew, chromatogramsBase);
                    }
                }
            }
            else if (mergeAction == MergeAction.merge_names)
            {
                chromatogramsBase = new List<ChromatogramSet>();
                chromatogramsNew = new List<ChromatogramSet>();
                var dictNameToChromatogramsImport = resultsImport.Chromatograms.ToDictionary(chrom => chrom.Name);
                // Add all of the original chromatogram sets merging imported sets
                // with matching names
                foreach (var chromatogramSetNew in results.Chromatograms)
                {
                    ChromatogramSet chromatogramSetImport;
                    if (dictNameToChromatogramsImport.TryGetValue(chromatogramSetNew.Name, out chromatogramSetImport))
                    {
                        MergeChromatogramSets(chromatogramSetNew, chromatogramSetImport,
                            chromatogramsNew, chromatogramsBase);
                    }
                    else
                    {
                        chromatogramsNew.Add(chromatogramSetNew);
                    }
                }
                // Add all imported chromatogram sets not merged.  The need
                // to be added in their original order, with preference given to sets
                // that have been merged, over the original sets.
                var dictNameToChromatogramsBase = chromatogramsBase.ToDictionary(chrom => chrom.Name);
                chromatogramsBase.Clear();
                foreach (var chromatogramSetImport in resultsImport.Chromatograms)
                {
                    ChromatogramSet chromatogramSetBase;
                    if (!dictNameToChromatogramsBase.TryGetValue(chromatogramSetImport.Name, out chromatogramSetBase))
                    {
                        chromatogramsNew.Add(chromatogramSetImport);
                        chromatogramSetBase = chromatogramSetImport;
                    }

                    chromatogramsBase.Add(chromatogramSetBase);
                }
            }
            else
            {
                chromatogramsBase = new List<ChromatogramSet>();
                foreach (var chromatogramSet in resultsImport.Chromatograms)
                {
                    var chromatogramSetBase = EnsureUniqueSetName(chromatogramSet, dictNameToChromatograms.Keys);
                    chromatogramsBase.Add(chromatogramSetBase);
                    dictNameToChromatograms.Add(chromatogramSetBase.Name, chromatogramSetBase);
                }
                chromatogramsNew = Chromatograms.ToList();
                chromatogramsNew.AddRange(chromatogramsBase);
            }
            resultsBase = ArrayUtil.ReferencesEqual(chromatogramsBase, resultsImport.Chromatograms)
                ? resultsImport
                : resultsImport.ChangeChromatograms(chromatogramsBase);
            // Special case: avoid handling in ChangeChromatograms()
            results.Chromatograms = chromatogramsNew;
            return results;
        }

        private static void MergeChromatogramSets(ChromatogramSet chromatogramSetNew,
                                                  ChromatogramSet chromatogramSetImport,
                                                  ICollection<ChromatogramSet> chromatogramsNew,
                                                  ICollection<ChromatogramSet> chromatogramsBase)
        {
            var listMSDataFilePaths = chromatogramSetNew.MSDataFileInfos.ToList();
            var baseFilePaths = chromatogramSetImport.MSDataFileInfos.ToArray();
            chromatogramsBase.Add(chromatogramSetNew.ChangeMSDataFileInfos(baseFilePaths));
            listMSDataFilePaths.AddRange(baseFilePaths);
            chromatogramsNew.Add(chromatogramSetNew.ChangeMSDataFileInfos(listMSDataFilePaths));
        }

        private static ChromatogramSet EnsureUniqueSetName(ChromatogramSet chromatogramSet, ICollection<string> set)
        {
            string replicateName = Helpers.GetUniqueName(chromatogramSet.Name, set);
            if (!Equals(replicateName, chromatogramSet.Name))
                chromatogramSet = (ChromatogramSet) chromatogramSet.ChangeName(replicateName);
            return chromatogramSet;
        }


        public MeasuredResults ChangeRecalcStatus()
        {
            if (_cacheFinal == null)
                throw new InvalidOperationException(Resources.MeasuredResults_ChangeRecalcStatus_Attempting_to_recalculate_peak_integration_without_first_completing_raw_data_import_);

            return ChangeProp(ImClone(this), im => im.SetClonedCacheRecalc());
        }

        /// <summary>
        /// Sets cache recalc to current catch on a cloned object
        /// </summary>
        private void SetClonedCacheRecalc()
        {
            _cacheRecalc = _cacheFinal;
            SetClonedCacheState(null);
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private MeasuredResults()
        {
        }

        private enum ATTR
        {
            time_normal_area,
            joining_disabled
        }

        public static MeasuredResults Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new MeasuredResults());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Consume tag
            IsTimeNormalArea = reader.GetBoolAttribute(ATTR.time_normal_area);
            bool unjoinedResults = reader.GetBoolAttribute(ATTR.joining_disabled);
            reader.Read();

            // Read chromatogram sets
            var list = new List<ChromatogramSet>();
            reader.ReadElements(list);
            Chromatograms = list.ToArray();

            // Read end tag
            reader.ReadEndElement();

            IsDeserialized = !unjoinedResults && Chromatograms.Count > 0;
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.time_normal_area, IsTimeNormalArea);
            writer.WriteAttribute(ATTR.joining_disabled, IsJoiningDisabled);
            writer.WriteElements(Chromatograms);
        }

        #endregion

        #region object overrides

        public bool Equals(MeasuredResults obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return ArrayUtil.EqualsDeep(obj._chromatograms, _chromatograms);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (MeasuredResults)) return false;
            return Equals((MeasuredResults) obj);
        }

        public override int GetHashCode()
        {
            return _chromatograms.GetHashCodeDeep();
        }

        #endregion

        private class Loader
        {
            private readonly MeasuredResults _resultsClone;
            private readonly SrmDocument _document;
            private readonly string _documentPath;
            private readonly Action<string, MeasuredResults, MeasuredResults> _completed;
            private readonly MultiFileLoadMonitor _loadMonitor;
            private readonly MultiFileLoader _multiFileLoader;

            public Loader(MeasuredResults resultsClone, SrmDocument document, string documentPath,
                MultiFileLoadMonitor loadMonitor, MultiFileLoader multiFileLoader, Action<string, MeasuredResults, MeasuredResults> completed)
            {
                _resultsClone = resultsClone;
                _document = document;
                _documentPath = documentPath;
                _completed = completed;
                _loadMonitor = loadMonitor;
                _multiFileLoader = multiFileLoader;
            }
            
            public void Load()
            {
                // Turn of deserialized flag
                _resultsClone.IsDeserialized = false;

                // If there is a final cache, move it to partial and let it prove itself usable.
                if (_resultsClone._cacheFinal != null)
                {
                    _resultsClone._listPartialCaches = MakeReadOnly(new[] { _resultsClone._cacheFinal });
                    _resultsClone._cacheFinal = null;
                }
                    
                // Try loading the final cache from disk, if progressive loading has not started
                string cachePath = FinalCachePath;
                if (!CheckFinalCache(cachePath))
                    return; // Error reported

                // Attempt to load any shared caches that have not been loaded yet
                LoadSharedCaches();

                bool allowLoadingPartialCaches = !_multiFileLoader.IsLoading(_document);

                // Create a set of the paths for which existing caches contain results
                var cachedPaths = GetCachedPaths(allowLoadingPartialCaches);

                // Create a set of paths for nonexistent caches.
                var dataFiles = GetDataFiles();
                var uncachedPaths = GetUncachedPaths(dataFiles, cachedPaths, cachePath, allowLoadingPartialCaches);
                if (uncachedPaths == null)
                    return; // Error reported

                // If there are uncached paths, then initialize state for loading them,
                // and start the load for the first one.
                int uncachedCount = uncachedPaths.Count;
                if (uncachedCount > 0)
                {
                    // Checkpoint any partial cache changes, in case subsequent builds fail
                    var resultsCurrent = _document.Settings.MeasuredResults;
                    if (!ArrayUtil.ReferencesEqual(_resultsClone._listPartialCaches, resultsCurrent._listPartialCaches))
                    {
                        // Only if this will change the MeasuredResults, causing us to return here
                        var resultsMerged = resultsCurrent.UpdateCaches(_documentPath, _resultsClone);
                        if (!ReferenceEquals(resultsMerged._listPartialCaches, resultsCurrent._listPartialCaches))
                        {
                            Complete(_resultsClone, false);
                            return;
                        }
                    }

                    // Start loading uncached paths in parallel.
                    _multiFileLoader.Load(uncachedPaths, _document, _documentPath, _resultsClone._cacheRecalc, _loadMonitor, FinishCacheBuild);
                    return;
                }

                // This would mean that there were not data files
                Assume.IsTrue(_resultsClone._listPartialCaches != null);
                // Keep ReSharper happy
                if (_resultsClone._listPartialCaches == null)
                    return;

                // Only finish if the multi-file loader is done with the document
                if (!_multiFileLoader.CompleteDocument(_document, _loadMonitor))
                    return;

                // If joining is not allowed just finish
                var firstCache = _resultsClone._listPartialCaches[0];
                if (_resultsClone.IsJoiningDisabled)
                {
                    Complete(_resultsClone, true);
                }
                // Once everything is represented, if there is only one cache, then it is final
                // As long as it is not a shared cache
                else if (_resultsClone._listPartialCaches.Count == 1 &&
                            !_resultsClone.IsSharedCache(firstCache) &&
                            firstCache.CachePath == cachePath)
                {
                    _resultsClone.SetClonedCacheState(firstCache);
                    _resultsClone.IsResultsUpdateRequired = false;

                    ReleaseCacheRecalc();
                    Complete(_resultsClone, true);
                }
                // Otherwise perform join
                else
                {
                    var listPaths = new List<string>();
                    IPooledStream streamDestination = null;
                    foreach (var cache in _resultsClone._listPartialCaches)
                    {
                        listPaths.Add(cache.CachePath);
                        if (Equals(cachePath, cache.CachePath))
                            streamDestination = cache.ReadStream;
                    }

                    var streamRecalc = ReleaseCacheRecalc();
                    if (streamRecalc != null)
                    {
                        // It should not be possible to be both adding to an existing final cache
                        // and re-scoring the existing final cache at the same time.
                        Assume.IsNull(streamDestination);
                        streamDestination = streamRecalc;
                    }
                    var assumeNegativeChargeInPreV11Caches = _document.MoleculeTransitionGroups.All(tg => tg.PrecursorMz.IsNegative);
                    ChromatogramCache.Join(cachePath, streamDestination,
                        listPaths, _loadMonitor, FinishCacheJoin, assumeNegativeChargeInPreV11Caches);
                }
            }

            private IPooledStream ReleaseCacheRecalc()
            {
                if (_resultsClone._cacheRecalc == null)
                    return null;
                var recalcStream = _resultsClone._cacheRecalc.ReadStream;
                _resultsClone._cacheRecalc = null;
                recalcStream.CloseStream();
                return recalcStream;
            }

            private List<DataFileReplicates> GetDataFiles()
            {
                List<DataFileReplicates> resultList = new List<DataFileReplicates>();
                var replicatesByDataFile = new Dictionary<MsDataFileUri, DataFileReplicates>();
                foreach (var chromatogramSet in _resultsClone.Chromatograms)
                {
                    foreach (var msDataFileUri in chromatogramSet.MSDataFilePaths)
                    {
                        DataFileReplicates dataFileReplicates;
                        if (!replicatesByDataFile.TryGetValue(msDataFileUri, out dataFileReplicates))
                        {
                            dataFileReplicates = new DataFileReplicates
                            {
                                DataFile = msDataFileUri,
                                ReplicateList = new List<string>()
                            };
                            replicatesByDataFile.Add(msDataFileUri, dataFileReplicates);
                            resultList.Add(dataFileReplicates);
                        }
                        dataFileReplicates.ReplicateList.Add(chromatogramSet.Name);
                    }
                }

                return resultList;
            }

            private string FinalCachePath
            {
                get
                {
                    return Program.ReplicateCachePath ?? ChromatogramCache.FinalPathForName(_documentPath, null);
                }
            }

            private bool CheckFinalCache(string cachePath)
            {
                // If the final cache exists and it is not in the partial caches or partial caches
                // contain the final cache, but it is not open (Undo-Redo case), then make sure it
                // is reloaded from scratch, as it may have changed since it was last open.
                if (_resultsClone._cacheRecalc == null && File.Exists(cachePath))
                {
                    if (_resultsClone._listPartialCaches != null)
                    {
                        int finalIndex = _resultsClone._listPartialCaches.IndexOf(cache =>
                            Equals(cache.CachePath, cachePath));
                        if (finalIndex == -1 || _resultsClone._listPartialCaches[finalIndex].ReadStream.IsModified)
                        {
                            foreach (var cache in _resultsClone._listPartialCaches)
                                cache.ReadStream.CloseStream();
                            _resultsClone._listPartialCaches = null;    // Drop through and reload
                        }
                    }
                    if (_resultsClone._listPartialCaches == null)
                    {
                        var status = new ProgressStatus();
                        try
                        {
                            // Watch out for older caches that didn't record chromatogram polarity.  We can only reliably handle this for completely negative docs.
                            bool assumeNegativeChargesInPreV11Caches = _document.MoleculeTransitionGroups.All(p => p.PrecursorMz.IsNegative);
                            var cache = ChromatogramCache.Load(cachePath, status, _loadMonitor, assumeNegativeChargesInPreV11Caches);
                            if (_resultsClone.IsValidCache(cache, false))
                                _resultsClone._listPartialCaches = ImmutableList.Singleton(cache);
                            else
                            {
                                // Otherwise, get rid of this cache, since it will need to be
                                // replaced.
                                cache.Dispose();
                                FileEx.SafeDelete(cache.CachePath);
                            }
                        }
                        catch (Exception x)
                        {
                            string message = TextUtil.LineSeparate(string.Format(Resources.Loader_Load_Failure_reading_the_data_file__0__, cachePath),
                                                                   x.Message);
                            Fail(status.ChangeErrorException(new IOException(message, x)));
                            return false;
                        }
                    }
                }
                return true;
            }

            private HashSet<MsDataFileUri> GetCachedPaths(bool allowLoading)
            {
                var cachedPaths = new HashSet<MsDataFileUri>();
                if (_resultsClone._listPartialCaches != null)
                {
                    // Check that all partial caches are valid
                    var listValidCaches = new List<ChromatogramCache>();
                    foreach (var cache in _resultsClone._listPartialCaches.ToArray())
                    {
                        // Skip modified caches
                        if (cache.ReadStream.IsModified)
                            continue;

                        listValidCaches.Add(cache);

                        foreach (var cachedFile in cache.CachedFiles)
                            cachedPaths.Add(cachedFile.FilePath);
                    }

                    // Update the list if necessary
                    int countValid = listValidCaches.Count;
                    if (countValid < _resultsClone._listPartialCaches.Count)
                        _resultsClone._listPartialCaches = (countValid > 0 ? ImmutableList.ValueOf(listValidCaches) : null);
                }
                // Otherwise, try loading replicate caches from before implementation
                // of single cache per replicate.
                else if (allowLoading)
                {
                    var listPartialCaches = new List<ChromatogramCache>();
                    foreach (var chromSet in _resultsClone.Chromatograms)
                    {
                        string replicatePath = ChromatogramCache.FinalPathForName(_documentPath, chromSet.Name);
                        if (File.Exists(replicatePath))
                        {
                            if (!LoadAndAdd(replicatePath, listPartialCaches))
                                return null;
                        }
                        else
                        {
                            foreach (var filePath in chromSet.MSDataFilePaths)
                            {
                                string cacheFilePath = ChromatogramCache.PartPathForName(_documentPath, filePath);
                                if (File.Exists(cacheFilePath))
                                {
                                    if (!LoadAndAdd(cacheFilePath, listPartialCaches))
                                        return null;
                                }
                            }
                        }
                    }
                    if (listPartialCaches.Count > 0)
                    {
                        _resultsClone._listPartialCaches = ImmutableList.ValueOf(listPartialCaches);
                        // Use recursion to get the cached paths from the new partial caches
                        return GetCachedPaths(true);
                    }
                }
                return cachedPaths;
            }

            private void LoadSharedCaches()
            {
                if (_resultsClone._listSharedCachePaths != null)
                {
                    var status = new ProgressStatus();
                    var listAddCaches = new List<ChromatogramCache>();
                    foreach (var sharedCachePath in _resultsClone._listSharedCachePaths)
                    {
                        if (_resultsClone.IsCachePath(sharedCachePath))
                            continue;

                        if (File.Exists(sharedCachePath))
                        {
                            try
                            {
                                var assumeNegativeChargeInPreV11Caches = _document.MoleculeTransitionGroups.All(t => t.PrecursorMz.IsNegative);
                                var cache = ChromatogramCache.Load(sharedCachePath, status, _loadMonitor, assumeNegativeChargeInPreV11Caches);
                                if (cache.IsSupportedVersion)
                                    listAddCaches.Add(cache);
                                else
                                {
                                    cache.Dispose();
                                }
                            }
                            catch (UnauthorizedAccessException) {}
                            catch (IOException) {}
                            catch (InvalidDataException) {}
                        }
                    }
                    if (listAddCaches.Count > 0)
                    {
                        var listPartialCaches = new List<ChromatogramCache>();
                        if (_resultsClone._listPartialCaches != null)
                            listPartialCaches.AddRange(_resultsClone._listPartialCaches);
                        // Add the caches that are not already covered.
                        listPartialCaches.AddRange(listAddCaches.Where(cache => !cache.IsCovered(listPartialCaches)));
                        _resultsClone._listPartialCaches = MakeReadOnly(listPartialCaches);
                    }
                    // Make sure none of the failed cache paths get tried again
                    var sharedCachePaths = _resultsClone._listPartialCaches != null
                           ? _resultsClone._listSharedCachePaths.Intersect(
                               _resultsClone._listPartialCaches.Select(cache => cache.CachePath)).ToArray()
                           : _resultsClone._listSharedCachePaths.ToArray();
                    _resultsClone._listSharedCachePaths = MakeReadOnly(sharedCachePaths);
                }
            }

            private List<DataFileReplicates> GetUncachedPaths(ICollection<DataFileReplicates> dataFileReplicatesList,
                ICollection<MsDataFileUri> cachedPaths, string cachePath, bool allowLoading)
            {
                // Keep a record of files which have been found in a new location
                // on the local system, and need to be updated in these results.
                Dictionary<MsDataFileUri, MsDataFileUri> dictReplace = null;
                // Find the next file not represented in the list of partial caches
                var uncachedPaths = new List<DataFileReplicates>();
                foreach (var dataFileReplicates in dataFileReplicatesList)
                {
                    if (!cachedPaths.Contains(dataFileReplicates.DataFile))
                    {
                        // First make sure the file wasn't found and loaded locally
                        var path = dataFileReplicates.DataFile;
                        if (cachedPaths.Count > 0 &&  path is MsDataFilePath)
                        {
                            var dataFilePath = ChromatogramSet.GetExistingDataFilePath(cachePath, path);
                            if (cachedPaths.Contains(dataFilePath))
                            {
                                if (dictReplace == null)
                                    dictReplace = new Dictionary<MsDataFileUri, MsDataFileUri>();
                                dictReplace.Add(path, dataFilePath);
                                continue;
                            }
                        }

                        // If there is only one result path and joining is not disabled and no prior caches exist,
                        // then just create the cache directly to its final destination.
                        if (dataFileReplicatesList.Count == 1 && !_resultsClone.IsJoiningDisabled && _resultsClone._listPartialCaches == null)
                            dataFileReplicates.PartPath = cachePath;
                        else
                        {
                            dataFileReplicates.PartPath = ChromatogramCache.PartPathForName(_documentPath, path);
                            // If the partial cache exists, try to load it.
                            if (allowLoading && File.Exists(dataFileReplicates.PartPath) && !_resultsClone.IsCachePath(dataFileReplicates.PartPath))
                            {
                                var status = new ProgressStatus();
                                try
                                {
                                    // Deal with older cache formats where we did not record chromatogram polarity
                                    var assumeNegativeChargeInPreV11Caches = _document.MoleculeTransitionGroups.All(tg => tg.PrecursorMz.IsNegative);
                                    var cache = ChromatogramCache.Load(dataFileReplicates.PartPath, status, _loadMonitor, assumeNegativeChargeInPreV11Caches);
                                    if (cache.IsSupportedVersion && EnsurePathsMatch(cache))
                                    {
                                        var listPartialCaches = new List<ChromatogramCache>();
                                        if (_resultsClone._listPartialCaches != null)
                                            listPartialCaches.AddRange(_resultsClone._listPartialCaches);
                                        listPartialCaches.Add(EnsureOptimalMemoryUse(cache));
                                        _resultsClone._listPartialCaches = ImmutableList.ValueOf(listPartialCaches);
                                        continue;
                                    }
                                    else
                                    {
                                        // If not getting added to the partial caches close the file and delete
                                        // it, so it won't get tried again.
                                        cache.Dispose();
                                        FileEx.SafeDelete(dataFileReplicates.PartPath);
                                    }
                                }
                                catch (Exception x)
                                {
                                    Fail(status.ChangeErrorException(new IOException(
                                        string.Format(Resources.Loader_Load_Failure_attempting_to_load_the_data_cache_file__0_,
                                        dataFileReplicates.PartPath), x)));
                                    return null;
                                }
                            }
                        }
                        uncachedPaths.Add(dataFileReplicates);
                    }
                }
                return uncachedPaths;
            }

            private bool LoadAndAdd(string replicatePath, List<ChromatogramCache> listPartialCaches)
            {
                var status = new ProgressStatus();
                try
                {
                    // Deal with older cache formats where we did not record chromatogram polarity
                    var assumeNegativeChargeInPreV11Caches = _document.MoleculeTransitionGroups.All(tg => tg.PrecursorMz.IsNegative);
                    var cache = ChromatogramCache.Load(replicatePath, status, _loadMonitor, assumeNegativeChargeInPreV11Caches);
                    if (cache.IsSupportedVersion)
                        listPartialCaches.Add(EnsureOptimalMemoryUse(cache));
                    else
                    {
                        cache.Dispose();
                        FileEx.SafeDelete(replicatePath);
                    }
                }
                catch (Exception x)
                {
                    Fail(status.ChangeErrorException(x));
                    return false;
                }
                return true;
            }

            private void Complete(MeasuredResults results, bool final)
            {
                _completed(_documentPath, results, _document.Settings.MeasuredResults);
            }

            private void Fail(IProgressStatus status)
            {
                var x = status.ErrorException;
                
                var xBuild = x as DataFileException;
                if (xBuild != null)
                {
                    var newMeasuredResults = _document.Settings.MeasuredResults.FilterFiles(info => !Equals(info.FilePath, xBuild.ImportPath));
                    _completed(_documentPath, newMeasuredResults, _document.Settings.MeasuredResults);
                }

                if (x is ChromCacheBuildException || x is IOException || x is InvalidDataException)
                    _loadMonitor.UpdateProgress(status);
                else
                {
//                    var sb = new StringBuilder(x.Message);
//                    sb.AppendLine(x.StackTrace);
//                    for (var xInner = x.InnerException; xInner != null; xInner = xInner.InnerException)
//                        sb.AppendLine("------Inner------").AppendLine(xInner.StackTrace);
//                    string xMessage = sb.ToString();
                    string xMessage = x.Message;

                    var message = TextUtil.LineSeparate(string.Format(Resources.Loader_Fail_Failed_importing_results_into___0___, _documentPath),
                                                        xMessage);
                    x = new Exception(message, x);
                    _loadMonitor.UpdateProgress(status.ChangeErrorException(x));
                }
            }

            private void FinishCacheBuild(IList<FileLoadCompletionAccumulator.Completion> buildCompletions)
            {
                foreach (var completion in buildCompletions.Where(c => c.Status.IsError))
                    Fail(completion.Status);

                if (buildCompletions.All(c => c.Status.IsError))
                    return;

                var results = _resultsClone;
                var cachesToAdd = buildCompletions
                    .Where(c => c.Cache != null && c.Status.IsComplete && EnsurePathsMatch(c.Cache))
                    .Select(c => c.Cache).ToArray();
                if (cachesToAdd.Length > 0)
                {
                    // Add this to the list of partial caches
                    results = ImClone(results); // Clone because many files may come through here
                    var listPartialCaches = new List<ChromatogramCache>();
                    if (results._listPartialCaches != null)
                        listPartialCaches.AddRange(results._listPartialCaches);
                    listPartialCaches.AddRange(cachesToAdd.Select(EnsureOptimalMemoryUse));
                    results.SetClonedCacheState(null, listPartialCaches);
                }

                Complete(results, false);
            }

            private ChromatogramCache EnsureOptimalMemoryUse(ChromatogramCache cache)
            {
                if (!_resultsClone.IsJoiningDisabled || cache.CachePath == FinalCachePath)
                    return cache;
                // Free cache memory required for maintaining UI for partial caches, if this is the command-line
                // The final join will pull everything from disk
                return cache.ReleaseMemory();
            }

            private bool EnsurePathsMatch(ChromatogramCache cache)
            {
                // Make sure the path in the result matches the one in the cache. Otherwise,
                // loading will go into an infinite loop.
                foreach (var cachedFilePath in cache.CachedFilePaths)
                {
                    var match = _resultsClone.FindMatchingOrExistingMSDataFile(cachedFilePath);
                    if (match == null)
                        return false;
                }
                return true;
            }

            private void FinishCacheJoin(ChromatogramCache cache, IProgressStatus status)
            {
                if (status.IsError)
                {
                    Fail(status);
                    return;
                }

                if (cache != null)
                {
                    foreach (var cachePartial in _resultsClone._listPartialCaches)
                    {
                        string cachePath = cachePartial.CachePath;
                        bool isSharedCache = _resultsClone.IsSharedCache(cachePartial);

                        // Close partial cache file
                        try { cachePartial.Dispose(); }
                        catch (IOException) { }

                        // Remove from disk if not shared and not the final cache
                        if (!isSharedCache && !Equals(cache.CachePath, cachePath))
                            _loadMonitor.StreamManager.Delete(cachePartial.CachePath);
                    }

                    _resultsClone.SetClonedCacheState(cache);
                }

                Complete(_resultsClone, true);
            }
        }

        public static bool HasResults(IEnumerable<string> filePaths)
        {
            foreach (string filePath in filePaths)
            {
                try
                {
                    using (var reader = new StreamReader(filePath))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            // If there is a measured results tag before the settings_summary end
                            // tag, then this document contains results.  Otherwise not.
                            if (line.Contains(@"<measured_results"))
                                return true;
                            if (line.Contains(@"</settings_summary>"))
                                return false;
                        }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
            return false;
        }

        public double? GetMedianTicArea()
        {
            var ticAreas = new Statistics(Chromatograms.SelectMany(c => c.MSDataFileInfos)
                .Where(fileInfo => fileInfo.TicArea.HasValue).Select(fileInfo => fileInfo.TicArea.Value));
            if (ticAreas.Length == 0)
            {
                return null;
            }

            return ticAreas.Median();
        }
    }

    public sealed class DataFileReplicates
    {
        public MsDataFileUri DataFile;
        public List<string> ReplicateList;
        public string PartPath;
    }

    public sealed class ChromSetFileMatch
    {
        public ChromSetFileMatch(ChromatogramSet chromatograms, MsDataFileUri filePath, int fileOrder)
        {
            Chromatograms = chromatograms;
            FilePath = filePath;
            FileOrder = fileOrder;
        }

        public ChromatogramSet Chromatograms { get; private set; }
        public MsDataFileUri FilePath { get; private set; }
        public int FileOrder { get; private set; }
    }
}
