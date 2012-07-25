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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    [XmlRoot("measured_results")]
    public sealed class MeasuredResults : Immutable, IXmlSerializable
    {
        private ReadOnlyCollection<ChromatogramSet> _chromatograms;

        private ChromatogramCache _cacheFinal;
        private ReadOnlyCollection<ChromatogramCache> _listPartialCaches;
        private ReadOnlyCollection<string> _listSharedCachePaths;
        private ProgressStatus _statusLoading;
        
        public MeasuredResults(IList<ChromatogramSet> chromatograms)
        {
            Chromatograms = chromatograms;
            // The only way to get peaks with areas not normalized by
            // time is to load an older document that was created this way.
            IsTimeNormalArea = true;
        }

        public IList<ChromatogramSet> Chromatograms
        {
            get { return _chromatograms; }
            private set { _chromatograms = MakeReadOnly(value.ToArray()); }
        }

        public bool IsTimeNormalArea { get; private set; }

        public bool IsLoaded
        {
            get
            {
                // All the chromatogram sets are loaded, and the cache has not been modified
                return !Chromatograms.Contains(c => !c.IsLoaded) &&
                       _cacheFinal != null && !_cacheFinal.ReadStream.IsModified;
            }
        }

        public bool IsChromatogramSetLoaded(int index)
        {
            return Chromatograms[index].IsLoaded;
        }

        public ProgressStatus StatusLoading { get { return _statusLoading; } }

        public IEnumerable<IPooledStream> ReadStreams
        {
            get { return Caches.Select(cache => cache.ReadStream); }
        }

        public IEnumerable<string> CachePaths
        {
            get { return Caches.Select(cache => cache.CachePath); }
        }

        public IEnumerable<string> CachedFilePaths
        {
            get { return Caches.SelectMany(cache => cache.CachedFilePaths); }
        }

        public IEnumerable<ChromCachedFile> CachedFileInfos
        {
            get { return Caches.SelectMany(cache => cache.CachedFiles); }
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
        public IEnumerable<string> MSDataFilePaths
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

        public ChromSetFileMatch FindMatchingMSDataFile(string filePathFind)
        {
            // First look for an exact match
            int fileOrder = 0;
            foreach (ChromatogramSet chromSet in Chromatograms)
            {
                foreach (string filePath in chromSet.MSDataFilePaths)
                {
                    if (Equals(filePath, filePathFind))
                        return new ChromSetFileMatch(chromSet, filePath, fileOrder);
                    fileOrder++;
                }
            }
            // Then look for a basename match
            fileOrder = 0;
            foreach (ChromatogramSet chromSet in Chromatograms)
            {
                string fileBasename = Path.GetFileNameWithoutExtension(filePathFind);
                foreach (string filePath in chromSet.MSDataFilePaths)
                {
                    if (IsBaseNameMatch(Path.GetFileNameWithoutExtension(filePath), fileBasename))
                        return new ChromSetFileMatch(chromSet, filePath, fileOrder);
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
            // Check for Waters MSe
            string suffix = name.Substring(prefix.Length);
            if (suffix[0] == '_' && IsUnderscoreSuffix(suffix))
                return true;
            return false;
        }

        public static bool IsUnderscoreSuffix(string name)
        {
            return name.EndsWith("_final_fragment");
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
                throw new InvalidOperationException("The chromatogram cache must be loaded before it can be changed.");

            _cacheFinal.CommitCache(fs);
            // Now the cach needs to be reloaded.
            return ChangeProp(ImClone(this), im => im._cacheFinal = null);
        }

        public MeasuredResults OptimizeCache(string documentPath, IStreamManager streamManager)
        {
            if (!IsLoaded)
                throw new InvalidOperationException("The chromatogram cache must be loaded before it is optimized.");

            var cacheOptimized = _cacheFinal.Optimize(documentPath, MSDataFilePaths, streamManager);
            if (ReferenceEquals(cacheOptimized, _cacheFinal))
                return this;
            return ChangeProp(ImClone(this), im => im._cacheFinal = cacheOptimized);
        }

        public MeasuredResults UpdateCaches(string documentPath, MeasuredResults resultsCache)
        {
            // Clone the current node, and update its cache properties.
            var results = ImClone(this);
            results._statusLoading = resultsCache._statusLoading;
            results._listPartialCaches = resultsCache._listPartialCaches;
            results._cacheFinal = resultsCache._cacheFinal;

            // If both sets have partial caches, merge them
            if (_listPartialCaches != null && results._listPartialCaches != null)
            {
                // CONSIDER: It is hypothetically possible that the two sets
                //           may contain overlapping caches with multiple
                //           cached files that are not the same, but very
                //           unlikely.  So, ignored for the moment.
                var listUnionCaches = new List<ChromatogramCache>(
                    results._listPartialCaches.Union(_listPartialCaches, ChromatogramCache.PathComparer));
                if (listUnionCaches.Count != results._listPartialCaches.Count)
                {
                    // Use the more advanced status
                    if (_statusLoading != null && results._statusLoading.PercentComplete <
                            _statusLoading.PercentComplete)
                    {
                        results._statusLoading = _statusLoading;
                    }
                    results._listPartialCaches = MakeReadOnly(listUnionCaches);
                }
            }

            string cachePath = ChromatogramCache.FinalPathForName(documentPath, null);
            var cachedFiles = results.CachedFileInfos.Distinct(new PathComparer<ChromCachedFile>()).ToArray();
            var dictCachedFiles = cachedFiles.ToDictionary(cachedFile => cachedFile.FilePath);
            var enumCachedNames = cachedFiles.Select(cachedFile => SampleHelp.GetFileName(cachedFile.FilePath));
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

        public bool TryGetChromatogramSet(string name, out ChromatogramSet chromatogramSet, out int index)
        {
            index = _chromatograms.IndexOf(chrom => Equals(name, chrom.Name));
            return ChromatogramSetForIndex(index, out chromatogramSet);
        }

        public bool TryGetChromatogramSet(int setId, out ChromatogramSet chromatogramSet, out int index)
        {
            index = _chromatograms.IndexOf(chrom => Equals(setId, chrom.Id.GlobalIndex));
            return ChromatogramSetForIndex(index, out chromatogramSet);
        }

        private bool ChromatogramSetForIndex(int index, out ChromatogramSet chromatogramSet)
        {
            chromatogramSet = (index != -1 ? _chromatograms[index] : null);
            return index != -1;            
        }
        
        public bool TryLoadChromatogram(int index,
                                        TransitionGroupDocNode nodeGroup,
                                        float tolerance,
                                        bool loadPoints,
                                        out ChromatogramGroupInfo[] infoSet)
        {
            return TryLoadChromatogram(_chromatograms[index],
                                       nodeGroup, tolerance, loadPoints, out infoSet);
        }

        public bool TryLoadChromatogram(ChromatogramSet chromatogram,
                                        TransitionGroupDocNode nodeGroup,
                                        float tolerance,
                                        bool loadPoints,
                                        out ChromatogramGroupInfo[] infoSet)
        {
            // Add precursor matches to a list, if they match at least 1 transition
            // in this group, and are potentially the maximal transition match.

            // Using only the maximum works well for the case where there are 2
            // precursors in the same document that match a single entry.
            // TODO: But it messes up when there are 2 sets of transitions for
            //       the same precursor covering different numbers of transitions.
            //       Skyline never creates this case, but it has been reported
            int maxTranMatch = 1;

            var listChrom = new List<ChromatogramGroupInfo>();
            foreach (var cache in Caches)
            {
                ChromatogramGroupInfo[] info;
                if (!cache.TryLoadChromatogramInfo(nodeGroup, tolerance, out info))
                    continue;

                foreach (var chromInfo in info)
                {
                    if (!ContainsInfo(chromatogram, chromInfo))
                        continue;

                    // If the chromatogram set has an optimazation function than the number
                    // of matching chromatograms per transition is a reflection of better
                    // matching.  Otherwise, we only expect one match per transition.
                    bool multiMatch = chromatogram.OptimizationFunction != null;
                    int tranMatch = chromInfo.MatchTransitions(nodeGroup, tolerance, multiMatch);
                    if (tranMatch >= maxTranMatch)
                    {
                        // If new maximum, clear anything collected at the previous maximum
                        if (tranMatch > maxTranMatch)
                            listChrom.Clear();

                        maxTranMatch = tranMatch;
                        // Read the points now, if requested.
                        if (loadPoints)
                            chromInfo.ReadChromatogram(cache);
                        listChrom.Add(chromInfo);
                    }
                }
            }

            // If more than one value was found, make a final pass to ensure that there
            // is only one precursor match per file.
            if (listChrom.Count > 1)
            {
                double precursorMz = nodeGroup.PrecursorMz;
                var listChromFinal = new List<ChromatogramGroupInfo>();
                foreach (var chromInfo in listChrom)
                {
                    string filePath = chromInfo.FilePath;
                    int fileIndex = listChromFinal.IndexOf(info => ReferenceEquals(filePath, info.FilePath));
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
            infoSet = listChrom.ToArray();
            return infoSet.Length > 0;
        }

        private static bool ContainsInfo(ChromatogramSet chromatogram, ChromatogramGroupInfo chromInfo)
        {
            return chromatogram.MSDataFilePaths.Contains(chromInfo.FilePath);
        }

        public bool ContainsChromatogram(string name)
        {
            return _chromatograms.Contains(set => Equals(name, set.Name));
        }

        public void Load(SrmDocument document, string documentPath, ILoadMonitor loadMonitor,
            Action<string, MeasuredResults> completed)
        {
            var loader = new Loader(ImClone(this), document, documentPath, loadMonitor, completed);
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
                        Helpers.TryTwice(() => File.Delete(cachePath));
                        listPartialCaches.Remove(cache);
                    }
                }
                // Always take the existing final cache forward.  It will be optimized
                // on save.
                if (results._cacheFinal != null)
                    listPartialCaches.Insert(0, results._cacheFinal);
                results._cacheFinal = null;
                results._listPartialCaches = (listPartialCaches.Count == 0 ? null : listPartialCaches.AsReadOnly());
            }
            return results;
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

        public MeasuredResults ChangeSharedCachePaths(params string[] prop)
        {
            return ChangeProp(ImClone(this), im => im.SharedCachePaths = prop);
        }

        public enum MergeAction { remove, merge_names, merge_indices, add }

        public static MeasuredResults MergeResults(MeasuredResults resultsOrig, MeasuredResults resultsImport,
            string documentPath, MergeAction mergeAction, out MeasuredResults resultsBase)
        {
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
            results._cacheFinal = null;
            results._listPartialCaches = MakeReadOnly(listPartialCaches);
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
            time_normal_area
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
            reader.Read();

            // Read chromatogram sets
            var list = new List<ChromatogramSet>();
            reader.ReadElements(list);
            Chromatograms = list.ToArray();

            // Read end tag
            reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.time_normal_area, IsTimeNormalArea);
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
            private readonly Action<string, MeasuredResults> _completed;
            private readonly ILoadMonitor _loader;
            private ProgressStatus _status;

            public Loader(MeasuredResults resultsClone, SrmDocument document, string documentPath,
                ILoadMonitor loader, Action<string, MeasuredResults> completed)
            {
                _resultsClone = resultsClone;
                _document = document;
                _documentPath = documentPath;
                _completed = completed;
                _loader = loader;
            }

            public void Load()
            {
                if (_resultsClone._statusLoading == null)
                {
                    _status = new ProgressStatus(string.Format("Loading results for {0}", Path.GetFileName(_documentPath)));
                    _loader.UpdateProgress(_status);
                }
                else
                {
                    _status = _resultsClone._statusLoading.NextSegment();
                }

                // It is not possible to be here with a valid final cache.
                _resultsClone._cacheFinal = null;

                // Try loading the final cache from disk, if progressive loading has not started
                string cachePath = ChromatogramCache.FinalPathForName(_documentPath, null);
                // If the final cache exists and it is not in the partial caches or partial caches
                // contain the final cache, but it is not open (Undo-Redo case), then make sure it
                // is reloaded from scratch, as it may have changed since it was last open.
                bool cacheExists = File.Exists(cachePath);
                if (_resultsClone._listPartialCaches != null && cacheExists)
                {
                    int finalIndex = _resultsClone._listPartialCaches.IndexOf(cache =>
                        Equals(cache.CachePath, cachePath));
                    if (finalIndex == -1 || _resultsClone._listPartialCaches[finalIndex].ReadStream.IsModified)
                    {
                        foreach (var cache in _resultsClone._listPartialCaches)
                            cache.ReadStream.CloseStream();
                        _resultsClone._listPartialCaches = null;
                    }
                }
                if (_resultsClone._listPartialCaches == null)
                {
                    if (cacheExists)
                    {
                        try
                        {
                            var cache = ChromatogramCache.Load(cachePath, _status, _loader);
                            // If it is valid, add it to the list of partial caches.  If it
                            // turns out that this cache covers all necessary paths, it will
                            // be moved to _cacheFinal later in this function.
                            if (_resultsClone.IsValidCache(cache, false))
                                _resultsClone._listPartialCaches = new ReadOnlyCollection<ChromatogramCache>(new[] {cache});
                            else
                            {
                                // Otherwise, get rid of this cache, since it will need to be
                                // replaced.
                                cache.ReadStream.CloseStream();
                                File.Delete(cache.CachePath);
                            }
                        }
                        catch (Exception x)
                        {
                            string message = string.Format("Failure reading the data file {0}.\n{1}",
                                                           cachePath, x.Message);
                            Fail(new IOException(message, x));
                            return;
                        }
                    }
                    // Otherwise, try loading replicate caches from before implementation
                    // of single cache per replicate.
                    else
                    {
                        var listPartialCaches = new List<ChromatogramCache>();
                        foreach (var chromSet in _resultsClone.Chromatograms)
                        {
                            string replicatePath = ChromatogramCache.FinalPathForName(_documentPath, chromSet.Name);
                            if (File.Exists(replicatePath))
                            {
                                try
                                {
                                    var cache = ChromatogramCache.Load(replicatePath, _status, _loader);
                                    if (cache.IsSupportedVersion)
                                        listPartialCaches.Add(cache);
                                }
                                catch (Exception x)
                                {
                                    Fail(x);
                                    return;
                                }
                            }
                        }
                        if (listPartialCaches.Count > 0)
                            _resultsClone._listPartialCaches = listPartialCaches.AsReadOnly();
                    }
                }

                // Attempt to load any shared caches that have not been loaded yet
                if (_resultsClone._listSharedCachePaths != null)
                {
                    var listAddCaches = new List<ChromatogramCache>();
                    foreach (var sharedCachePath in _resultsClone._listSharedCachePaths)
                    {
                        if (_resultsClone.IsCachePath(sharedCachePath))
                            continue;

                        if (File.Exists(sharedCachePath))
                        {
                            try
                            {
                                var cache = ChromatogramCache.Load(sharedCachePath, _status, _loader);
                                if (cache.IsSupportedVersion)
                                    listAddCaches.Add(cache);
                            }
                            catch (IOException)
                            {
                            }
                            catch (InvalidDataException)
                            {                                
                            }
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

                // Create a set of the paths for which existing caches contain results
                var cachedPaths = new HashSet<string>();
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
                        _resultsClone._listPartialCaches = (countValid > 0 ? listValidCaches.AsReadOnly() : null);
                }

                // Keep a record of files which have been found in a new location
                // on the local system, and need to be updated in these results.
                Dictionary<string, string> dictReplace = null;
                // Find the next file not represented in the list of partial caches
                var uncachedPaths = new List<KeyValuePair<string, string>>();
                var msDataFilePaths = _resultsClone.MSDataFilePaths.ToArray();
                foreach (string path in msDataFilePaths)
                {
                    if (!cachedPaths.Contains(path))
                    {
                        // First make sure the file wasn't found and loaded locally
                        if (cachedPaths.Count > 0)
                        {
                            string dataFilePathPart;
                            string dataFilePath = ChromatogramSet.GetExistingDataFilePath(cachePath, path, out dataFilePathPart);
                            if (cachedPaths.Contains(dataFilePath))
                            {
                                if (dictReplace == null)
                                    dictReplace = new Dictionary<string, string>();
                                dictReplace.Add(path, dataFilePath);
                                continue;
                            }
                        }

                        string partPath;
                        // If there is only one result path, then just create the cache directly to its
                        // final destination.
                        if (msDataFilePaths.Length == 1)
                            partPath = cachePath;
                        else
                        {
                            partPath = ChromatogramCache.PartPathForName(_documentPath, path, null);
                            // If the partial cache exists, try to load it.
                            if (File.Exists(partPath))
                            {
                                try
                                {
                                    var cache = ChromatogramCache.Load(partPath, _status, _loader);
                                    if (cache.IsSupportedVersion)
                                    {
                                        var listPartialCaches = new List<ChromatogramCache>();
                                        if (_resultsClone._listPartialCaches != null)
                                            listPartialCaches.AddRange(_resultsClone._listPartialCaches);
                                        listPartialCaches.Add(cache);
                                        _resultsClone._listPartialCaches = listPartialCaches.AsReadOnly();
                                        continue;
                                    }
                                }
                                catch (Exception x)
                                {
                                    Fail(new IOException(string.Format("Failure attempting to load the data cache file {0}", partPath), x));
                                    return;
                                }
                            }
                        }
                        uncachedPaths.Add(new KeyValuePair<string, string>(path, partPath));
                    }
                }

                // If there are uncached paths, then initialize state for loading them,
                // and start the load for the first one.
                int uncachedCount = uncachedPaths.Count;
                if (uncachedCount > 0)
                {
                    // If more than just a single file will be cached, then create a segmented
                    // status object for marking progress.
                    if (msDataFilePaths.Length > 1 && _status.SegmentCount < uncachedCount)
                        _status = _status.ChangeSegments(0, uncachedCount + 1); // +1 for join

                    // CONSIDER: In theory concurrent builds should be possible, and could
                    //           improve resource utilization to speed up cache creation.  Initial
                    //           tests, however, expose issues with coordinating progress status
                    //           and even successful completion.
                    var uncached = uncachedPaths[0];
                    ChromatogramCache.Build(_document, uncached.Value, new[] { uncached.Key }, _status, _loader,
                        FinishCacheBuild);
                    return;
                }

                // This would mean that there were not data files
                Debug.Assert(_resultsClone._listPartialCaches != null);
                // Keep ReSharper happy
                if (_resultsClone._listPartialCaches == null)
                    return;

                // Once everything is represented, if there is only one cache, then it is final
                // As long as it is not a shared cache
                if (_resultsClone._listPartialCaches.Count == 1 &&
                        !_resultsClone.IsSharedCache(_resultsClone._listPartialCaches[0]))
                {
                    _resultsClone._cacheFinal = _resultsClone._listPartialCaches[0];
                    _resultsClone._listPartialCaches = null;

                    _loader.UpdateProgress(_status.ChangeSegments(0, 0).Complete());

                    Complete(true);
                }
                else
                {
                    _status = _status.NextSegment();
                    var listPaths = new List<string>();
                    IPooledStream streamDestination = null;
                    foreach (var cache in _resultsClone._listPartialCaches)
                    {
                        listPaths.Add(cache.CachePath);
                        if (Equals(cachePath, cache.CachePath))
                            streamDestination = cache.ReadStream;
                    }
                    ChromatogramCache.Join(cachePath, streamDestination,
                        listPaths, _status, _loader, FinishCacheJoin);
                }
            }

            private void Complete(bool final)
            {
                _resultsClone._statusLoading = (final ? null : _status);
                _completed(_documentPath, _resultsClone);
            }

            private void Fail(Exception x)
            {
                if (x is IOException || x is InvalidDataException)
                    _loader.UpdateProgress(_status.ChangeErrorException(x));
                else
                {
                    x = new Exception(string.Format("Failed to build a cache for '{0}'.\n{1}", _documentPath, x.Message), x);
                    _loader.UpdateProgress(_status.ChangeErrorException(x));
                }

                _completed(_documentPath, null);
            }

            private void FinishCacheBuild(ChromatogramCache cache, Exception x)
            {
                if (x != null)
                {
                    Fail(x);
                    return;
                }

                if (cache != null)
                {
                    List<ChromatogramCache> listPartialCaches = new List<ChromatogramCache>();
                    if (_resultsClone._listPartialCaches != null)
                        listPartialCaches.AddRange(_resultsClone._listPartialCaches);
                    listPartialCaches.Add(cache);
                    _resultsClone._listPartialCaches =
                        new ReadOnlyCollection<ChromatogramCache>(listPartialCaches);
                }

                Complete(false);
            }

            private void FinishCacheJoin(ChromatogramCache cache, Exception x)
            {
                if (x != null)
                {
                    Fail(x);
                    return;
                }

                if (cache != null)
                {
                    foreach (var cachePartial in _resultsClone._listPartialCaches)
                    {
                        // Close partial cache file
                        try { cachePartial.Dispose(); }
                        catch (IOException) { }

                        // Remove from disk if not shared and not the final cache
                        if (!_resultsClone.IsSharedCache(cachePartial) && !Equals(cache.CachePath, cachePartial.CachePath))
                            File.Delete(cachePartial.CachePath);
                    }

                    _resultsClone._listPartialCaches = null;
                    _resultsClone._cacheFinal = cache;

                    _loader.UpdateProgress(_status.ChangeSegments(0, 0).Complete());
                }

                Complete(true);
            }
        }

        public ChromFileInfo GetChromFileInfo<TChromInfo>(Results<TChromInfo> results, int replicateIndex) where TChromInfo:ChromInfo
        {
            OneOrManyList<TChromInfo> replicateChromInfos = null;
            if (results != null && replicateIndex >= 0 && replicateIndex < results.Count)
            {
                replicateChromInfos = results[replicateIndex];
            }
            if (replicateChromInfos != null)
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
    }

    public sealed class ChromSetFileMatch
    {
        public ChromSetFileMatch(ChromatogramSet chromatograms, string filePath, int fileOrder)
        {
            Chromatograms = chromatograms;
            FilePath = filePath;
            FileOrder = fileOrder;
        }

        public ChromatogramSet Chromatograms { get; private set; }
        public string FilePath { get; private set; }
        public int FileOrder { get; private set; }
    }
}
