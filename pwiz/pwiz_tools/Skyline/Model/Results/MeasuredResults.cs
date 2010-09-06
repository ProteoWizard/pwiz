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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    [XmlRoot("measured_results")]
    public sealed class MeasuredResults : Immutable, IXmlSerializable
    {
        private ReadOnlyCollection<ChromatogramSet> _chromatograms;

        private ChromatogramCache _cacheFinal;
        private ReadOnlyCollection<ChromatogramCache> _listPartialCaches;
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
            get
            {
                foreach (var cache in Caches)
                    yield return cache.ReadStream;
            }
        }

        public IEnumerable<string> CachePaths
        {
            get
            {
                foreach (var cache in Caches)
                    yield return cache.CachePath;
            }
        }

        public IEnumerable<string> CachedFilePaths
        {
            get
            {
                foreach (var cache in Caches)
                {
                    foreach (var path in cache.CachedFilePaths)
                        yield return path;
                }
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
        /// The unique set of file paths represented in all replicates,
        /// in the order they appear.
        /// </summary>
        public IEnumerable<string> MSDataFilePaths
        {
            get
            {
                var seenPaths = new HashSet<string>();
                foreach (ChromatogramSet chromSet in Chromatograms)
                {
                    foreach (string filePath in chromSet.MSDataFilePaths)
                    {
                        if (!seenPaths.Contains(filePath))
                        {
                            seenPaths.Add(filePath);
                            yield return filePath;
                        }
                    }
                }
            }
        }

        private bool IsValidCache(ChromatogramCache cache, bool current)
        {
            if (!cache.IsCurrentVersion)
                return false;
            // Not if missing any files from the replicates
//            if (MSDataFilePaths.Contains(path => !cache.CachedFiles.Contains(cachedFile =>
//                    Equals(path, cachedFile.FilePath))))
//                return false;
            return !current || cache.IsCurrentDisk;
        }

        public MeasuredResults OptimizeCache(string documentPath, IStreamManager streamManager)
        {
            if (!IsLoaded)
                throw new InvalidOperationException("The ChromatogramCache must be loaded before it is optimized.");

            var cacheOptimized = _cacheFinal.Optimize(documentPath, MSDataFilePaths, streamManager);
            if (ReferenceEquals(cacheOptimized, _cacheFinal))
                return this;
            return ChangeProp(ImClone(this), (im, v) => im._cacheFinal = v, cacheOptimized);
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
                    results._listPartialCaches.Union(_listPartialCaches));
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
            var setCachedFiles = new HashSet<string>(results.CachedFilePaths);
            var listCachedNames = new List<string>(setCachedFiles).ConvertAll(
                path => SampleHelp.GetFileName(path));
            var setCachedFileNames = new HashSet<string>(listCachedNames);
            var chromatogramSets = new List<ChromatogramSet>();
            foreach (var chromSet in results.Chromatograms)
            {
                chromatogramSets.Add(chromSet.ChangeFileCacheFlags(
                    setCachedFiles, setCachedFileNames, cachePath));
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

                    int tranMatch = chromInfo.MatchTransitions(nodeGroup, tolerance);
                    if (tranMatch >= maxTranMatch)
                    {
                        maxTranMatch = tranMatch;
                        // Read the points now, if requested.
                        if (loadPoints)
                            chromInfo.ReadChromatogram(cache);
                        listChrom.Add(chromInfo);
                    }
                }
            }
            // If more than one value was found, make a final pass to remove
            // any entries that match fewer than the maximum number of matched
            // transitions.
            if (listChrom.Count > 1)
            {
                for (int i = listChrom.Count - 1; i >= 0; i--)
                {
                    var chromInfo = listChrom[i];
                    if (chromInfo.MatchTransitions(nodeGroup, tolerance) < maxTranMatch)
                        listChrom.RemoveAt(i);
                }
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

        #region Property change methods

        public MeasuredResults ChangeChromatograms(IList<ChromatogramSet> prop)
        {
            var results = ChangeProp(ImClone(this), (im, v) => im.Chromatograms = v, prop);
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
                        File.Delete(cache.CachePath);
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
                if (!ArrayUtil.EqualsDeep(chromSet.MSDataFilePaths, chromSetExisting.MSDataFilePaths))
                    return true;
            }
            return false;
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
            private readonly MeasuredResults _setClone;
            private readonly SrmDocument _document;
            private readonly string _documentPath;
            private readonly Action<string, MeasuredResults> _completed;
            private readonly ILoadMonitor _loader;
            private ProgressStatus _status;

            public Loader(MeasuredResults setClone, SrmDocument document, string documentPath,
                ILoadMonitor loader, Action<string, MeasuredResults> completed)
            {
                _setClone = setClone;
                _document = document;
                _documentPath = documentPath;
                _completed = completed;
                _loader = loader;
            }

            public void Load()
            {
                if (_setClone._statusLoading == null)
                {
                    _status = new ProgressStatus(string.Format("Loading results for {0}", Path.GetFileName(_documentPath)));
                    _loader.UpdateProgress(_status);
                }
                else
                {
                    _status = _setClone._statusLoading.NextSegment();
                }

                // It is not possible to be here with a valid final cache.
                _setClone._cacheFinal = null;

                // Try loading the final cache from disk, if progressive loading has not started
                string cachePath = ChromatogramCache.FinalPathForName(_documentPath, null);
                // Always try to load, if the cache exists, since the list of partial caches
                // may be populated because the user chose Undo.  In this case, failing to
                // attempt a cache load will force a complete reload of all files in the cache.
                bool cacheExists = File.Exists(cachePath);
                if (_setClone._listPartialCaches == null || cacheExists)
                {
                    if (cacheExists)
                    {
                        try
                        {
                            var cache = ChromatogramCache.Load(cachePath, _status, _loader);
                            // If it is valid, add it to the list of partial caches.  If it
                            // turns out that this cache covers all necessary paths, it will
                            // be moved to _cacheFinal later in this function.
                            if (_setClone.IsValidCache(cache, false))
                                _setClone._listPartialCaches = new ReadOnlyCollection<ChromatogramCache>(new[] {cache});
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
                        foreach (var chromSet in _setClone.Chromatograms)
                        {
                            string replicatePath = ChromatogramCache.FinalPathForName(_documentPath, chromSet.Name);
                            if (File.Exists(replicatePath))
                            {
                                try
                                {
                                    var cache = ChromatogramCache.Load(replicatePath, _status, _loader);
                                    if (cache.IsCurrentVersion)
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
                            _setClone._listPartialCaches = listPartialCaches.AsReadOnly();
                    }
                }

                // Create a set of the paths for which existing caches contain results
                var cachedPaths = new HashSet<string>();
                if (_setClone._listPartialCaches != null)
                {
                    // Check that all partial caches are valid
                    var listValidCaches = new List<ChromatogramCache>();
                    foreach (var cache in _setClone._listPartialCaches.ToArray())
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
                    if (countValid < _setClone._listPartialCaches.Count)
                        _setClone._listPartialCaches = (countValid > 0 ? listValidCaches.AsReadOnly() : null);
                }

                // Keep a record of files which have been found in a new location
                // on the local system, and need to be updated in these results.
                Dictionary<string, string> dictReplace = null;
                // Find the next file not represented in the list of partial caches
                var uncachedPaths = new List<KeyValuePair<string, string>>();
                var msDataFilePaths = _setClone.MSDataFilePaths.ToArray();
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
                                    if (cache.IsCurrentVersion)
                                    {
                                        var listPartialCaches = new List<ChromatogramCache>();
                                        if (_setClone._listPartialCaches != null)
                                            listPartialCaches.AddRange(_setClone._listPartialCaches);
                                        listPartialCaches.Add(cache);
                                        _setClone._listPartialCaches = listPartialCaches.AsReadOnly();
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
                Debug.Assert(_setClone._listPartialCaches != null);

                // Once everything is represented, if there is only one cache, then it is final
                if (_setClone._listPartialCaches.Count == 1)
                {
                    _setClone._cacheFinal = _setClone._listPartialCaches[0];
                    _setClone._listPartialCaches = null;

                    _loader.UpdateProgress(_status.ChangeSegments(0, 0).Complete());

                    Complete(true);
                }
                else
                {
                    _status = _status.NextSegment();
                    var listPaths = new List<string>();
                    IPooledStream streamDestination = null;
                    foreach (var cache in _setClone._listPartialCaches)
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
                _setClone._statusLoading = (final ? null : _status);
                _completed(_documentPath, _setClone);
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
                    if (_setClone._listPartialCaches != null)
                        listPartialCaches.AddRange(_setClone._listPartialCaches);
                    listPartialCaches.Add(cache);
                    _setClone._listPartialCaches =
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
                    foreach (var cachePartial in _setClone._listPartialCaches)
                    {
                        // Close partial cache file
                        try { cachePartial.Dispose(); }
                        catch (IOException) { }

                        // Remove from disk
                        if (!Equals(cache.CachePath, cachePartial.CachePath))
                            File.Delete(cachePartial.CachePath);
                    }

                    _setClone._listPartialCaches = null;
                    _setClone._cacheFinal = cache;

                    _loader.UpdateProgress(_status.ChangeSegments(0, 0).Complete());
                }

                Complete(true);
            }
        }
    }
}
