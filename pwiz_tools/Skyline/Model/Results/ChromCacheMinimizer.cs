/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Reduces the size of a Chromatogram cache file by discarding unused transitions and
    /// limiting the length along the time axis.
    /// </summary>
    public class ChromCacheMinimizer
    {
        private readonly float _tolerance;

        public ChromCacheMinimizer(SrmDocument document, ChromatogramCache chromatogramCache)
        {
            Document = document;
            ChromatogramCache = chromatogramCache;
            var chromGroupHeaderInfos = chromatogramCache.ChromGroupHeaderInfos.ToArray();
            Array.Sort(chromGroupHeaderInfos, CompareLocation);
            ChromGroupHeaderInfos = Array.AsReadOnly(chromGroupHeaderInfos);
            _tolerance = (float) Document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
        }

        /// <summary>
        /// Delegate for receiving notifications as the minimizing task is running.
        /// If the task needs to be cancelled, the callback should throw an exception
        /// (recommended <see cref="ObjectDisposedException"/>)
        /// </summary>
        public delegate void ProgressCallback(MinStatistics minStatistics);

        private int CompareLocation(ChromGroupHeaderInfo5 chromGroupHeaderInfo1, ChromGroupHeaderInfo5 chromGroupHeaderInfo2)
        {
            return chromGroupHeaderInfo1.LocationPoints.CompareTo(chromGroupHeaderInfo2.LocationPoints);
        }

        public SrmDocument Document { get; private set; }
        public ChromatogramCache ChromatogramCache { get; private set; }
        public IList<ChromGroupHeaderInfo5> ChromGroupHeaderInfos
        {
            get; private set;
        }

        /// <summary>
        /// Collects statistics on how much space savings minimizing will achieve, and (if outStream
        /// is not null) writes out the minimized cache file.
        /// </summary>
        public void Minimize(Settings settings, ProgressCallback progressCallback, Stream outStream,
            FileStream outStreamScans = null, FileStream outStreamPeaks = null, FileStream outStreamScores = null)
        {
            var writer = outStream == null ? null : new Writer(ChromatogramCache, outStream, outStreamScans, outStreamPeaks, outStreamScores);
            var statisticsCollector = new MinStatisticsCollector(this);
            bool readChromatograms = settings.NoiseTimeRange.HasValue || writer != null;

            var chromGroupHeaderToIndex =
                ChromGroupHeaderInfos
                    .Select((cghi, index) => new KeyValuePair<ChromGroupHeaderInfo5, int>(cghi, index))
                    .ToDictionary(kvp => kvp.Key, kvp=>kvp.Value);
            var chromGroups = new ChromatogramGroupInfo[ChromGroupHeaderInfos.Count];
            var transitionGroups = new List<TransitionGroupDocNode>[ChromGroupHeaderInfos.Count];
            foreach (var nodePep in Document.Molecules)
            {
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    ChromatogramGroupInfo[] groupInfos;
                    ChromatogramCache.TryLoadChromatogramInfo(nodePep, nodeGroup, _tolerance, out groupInfos);
                    foreach (var chromGroupInfo in groupInfos)
                    {
                        int headerIndex = chromGroupHeaderToIndex[chromGroupInfo.Header];
                        if (chromGroups[headerIndex] == null)
                        {
                            chromGroups[headerIndex] = chromGroupInfo;
                            transitionGroups[headerIndex] = new List<TransitionGroupDocNode>();
                        }
                        transitionGroups[headerIndex].Add(nodeGroup);
                    }
                }
            }

            for (int iHeader = 0; iHeader < ChromGroupHeaderInfos.Count; iHeader++)
            {
                var chromGroupInfo = chromGroups[iHeader];
                IList<TransitionGroupDocNode> transitionGroupDocNodes;
                if (chromGroupInfo == null)
                {
                    chromGroupInfo = ChromatogramCache.LoadChromatogramInfo(ChromGroupHeaderInfos[iHeader]);
                    transitionGroupDocNodes = new TransitionGroupDocNode[0];
                }
                else
                {
                    transitionGroupDocNodes = transitionGroups[iHeader];
                }
                if (readChromatograms)
                {
                    try
                    {
                        chromGroupInfo.ReadChromatogram(ChromatogramCache);
                    }
                    catch (Exception exception)
                    {
                        Trace.TraceWarning("Unable to read chromatogram {0}", exception); // Not L10N
                    }
                }
                MinimizedChromGroup minimizedChromGroup = MinimizeChromGroup(settings,
                    chromGroupInfo, transitionGroupDocNodes);
                statisticsCollector.ProcessChromGroup(minimizedChromGroup);
                if (progressCallback != null)
                {
                    progressCallback.Invoke(statisticsCollector.GetStatistics());
                }
                if (writer != null)
                {
                    writer.WriteChromGroup(chromGroupInfo, minimizedChromGroup);
                }
                // Null out the ChromGroup in our array so it can be garbage collected.
                chromGroups[iHeader] = null;
            }
            if (progressCallback != null)
            {
                progressCallback.Invoke(statisticsCollector.GetStatistics());
            }
            if (writer != null)
            {
                writer.WriteEndOfFile();
            }
        }

        /// <summary>
        /// Decides what information from a ChromatogramGroupInfo should be thrown away.
        /// ChromatogramGroupInfo's can have the the retention time range shortened, and certain
        /// transitions discarded.
        /// </summary>
        private MinimizedChromGroup MinimizeChromGroup(Settings settings, ChromatogramGroupInfo chromatogramGroupInfo, IList<TransitionGroupDocNode> transitionGroups)
        {
            var fileIndexes = new List<int>();
            for (int fileIndex = 0; fileIndex < Document.Settings.MeasuredResults.Chromatograms.Count; fileIndex++)
            {
                var chromatogramSet = Document.Settings.MeasuredResults.Chromatograms[fileIndex];
                if (chromatogramSet.MSDataFilePaths.Any(path=>Equals(path, chromatogramGroupInfo.FilePath)))
                {
                    fileIndexes.Add(fileIndex);
                }
            }
            var chromatograms = chromatogramGroupInfo.TransitionPointSets.ToArray();
            Assume.IsTrue(Equals(chromatogramGroupInfo.NumTransitions, chromatograms.Length));
            var keptTransitionIndexes = new List<int>();
            double minRetentionTime = Double.MaxValue;
            double maxRetentionTime = -Double.MaxValue;
            for (int i = 0; i < chromatograms.Length; i++)
            {
                var chromatogram = chromatograms[i];
                var matchingTransitions = new List<TransitionDocNode>();
                foreach (var transitionDocNode in transitionGroups.SelectMany(tg => tg.Transitions))
                {
                    if (0!=ChromKey.CompareTolerant((float) chromatogram.ProductMz, (float) transitionDocNode.Mz, _tolerance))
                    {
                        continue;
                    }
                    matchingTransitions.Add(transitionDocNode);
                    foreach (var fileIndex in fileIndexes)
                    {
                        var chromatogramSet = transitionDocNode.Results[fileIndex];
                        if (chromatogramSet == null)
                        {
                            continue;
                        }
                        foreach (var transitionChromInfo in chromatogramSet)
                        {
                            if (transitionChromInfo.IsEmpty)
                            {
                                continue;
                            }
                            minRetentionTime = Math.Min(minRetentionTime, transitionChromInfo.StartRetentionTime);
                            maxRetentionTime = Math.Max(maxRetentionTime, transitionChromInfo.EndRetentionTime);
                        }
                    }
                }
                bool kept = !settings.DiscardUnmatchedChromatograms
                    || matchingTransitions.Count > 0;
                if (kept)
                {
                    keptTransitionIndexes.Add(i);
                }
            }
            var result = new MinimizedChromGroup(chromatogramGroupInfo.Header)
                             {
                                 RetainedTransitionIndexes = keptTransitionIndexes,
                             };
            if (settings.NoiseTimeRange.HasValue && minRetentionTime < maxRetentionTime)
            {
                if (null == chromatogramGroupInfo.Times)
                {
                    // Chromatogram was unreadable.
                    result.RetainedTransitionIndexes.Clear();
                }
                else
                {
                    result.SetStartEndTime(chromatogramGroupInfo.Times, (float)(minRetentionTime - settings.NoiseTimeRange), (float)(maxRetentionTime + settings.NoiseTimeRange));
                }
            }
            return result;
        }

        internal class MinimizedChromGroup
        {
            public MinimizedChromGroup(ChromGroupHeaderInfo5 chromGroupHeaderInfo)
            {
                ChromGroupHeaderInfo = chromGroupHeaderInfo;
                OptimizedFirstScan = 0;
                OptimizedLastScan = chromGroupHeaderInfo.NumPoints - 1;
            }

            public ChromGroupHeaderInfo5 ChromGroupHeaderInfo { get; private set; }

            public ICollection<int> RetainedTransitionIndexes { get; set; }
            public float? OptimizedStartTime { get; private set; }
            public float? OptimizedEndTime { get; private set; }
            public int OptimizedFirstScan { get; private set; }
            public int OptimizedLastScan { get; private set; }
            public int OptimizedScanCount { get { return OptimizedLastScan + 1 - OptimizedFirstScan; } }
            public void SetStartEndTime(float[] times, float minRetentionTime, float maxRetentionTime)
            {
                int firstIndex = Array.BinarySearch(times, minRetentionTime);
                if (firstIndex < 0)
                {
                    firstIndex = ~firstIndex - 1;
                }
                firstIndex = Math.Max(firstIndex, 0);
                int lastIndex = Array.BinarySearch(times, maxRetentionTime);
                if (lastIndex < 0)
                {
                    lastIndex = ~lastIndex;
                }
                lastIndex = Math.Min(lastIndex, ChromGroupHeaderInfo.NumPoints - 1);
                OptimizedFirstScan = firstIndex;
                OptimizedLastScan = lastIndex;
                OptimizedStartTime = times[firstIndex];
                OptimizedEndTime = times[lastIndex];
            }

            public static MinimizedChromGroup Discard(ChromGroupHeaderInfo5 chromGroupHeaderInfo)
            {
                return new MinimizedChromGroup(chromGroupHeaderInfo)
                           {
                               RetainedTransitionIndexes = new int[0]
                           };
            }
        }
        public struct Settings
        {
            public Settings(Settings that)
                : this()
            {
                NoiseTimeRange = that.NoiseTimeRange;
                DiscardUnmatchedChromatograms = that.DiscardUnmatchedChromatograms;
            }

            public double? NoiseTimeRange { get; private set; }
            public Settings SetNoiseTimeRange(double? value)
            {
                return new Settings(this) { NoiseTimeRange = value };
            }
            public bool DiscardUnmatchedChromatograms { get; private set; }
            public Settings SetDiscardUnmatchedChromatograms(bool value)
            {
                return new Settings(this) { DiscardUnmatchedChromatograms = value };
            }
        }

        public class MinStatistics
        {
            public MinStatistics(IEnumerable<Replicate> replicates)
            {
                Replicates = replicates.ToArray();
                OriginalFileSize = Replicates.Select(r => r.OriginalFileSize).Sum();
                var processedFileSize = Replicates.Select(r => r.ProcessedFileSize).Sum();
                PercentComplete = (int) (100*processedFileSize/OriginalFileSize);
            }

            public long OriginalFileSize { get; private set; }
            public int PercentComplete { get; private set; }
            public Replicate[] Replicates { get; private set; }
            public double MinimizedRatio 
            {
                get
                {
                    return Replicates.Select(r => r.MinimizedFileSize).Sum()/Replicates.Select(r => r.ProcessedFileSize).Sum();
                }
            }
            public struct Replicate
            {
                public string Name { get; set; }
                public long OriginalFileSize { get; set; }
                public long ProcessedFileSize { get; set; }
                public double MinimizedFileSize { get; set; }
                public double? MinimizedRatio
                {
                    get
                    {
                        if (ProcessedFileSize == 0)
                        {
                            return null;
                        }
                        return MinimizedFileSize / ProcessedFileSize;
                    }
                }
            }

        }

        class MinStatisticsCollector
        {
            private static readonly int CHROM_GROUP_HEADER_INFO_SIZE;
            private static readonly int PEAK_SIZE;
            private static readonly int TRANSITION_SIZE;

            static unsafe MinStatisticsCollector()
            {
                CHROM_GROUP_HEADER_INFO_SIZE = sizeof(ChromGroupHeaderInfo5);
                PEAK_SIZE = sizeof(ChromPeak);
                TRANSITION_SIZE = sizeof(ChromTransition);
            }

            private readonly MinStatistics.Replicate[] _replicates;
            private readonly int[] _fileIndexToReplicateIndex;
            private int _processedGroupCount;

            private static long GetFileSize(ChromGroupHeaderInfo5 chromGroupHeaderInfo)
            {
                return CHROM_GROUP_HEADER_INFO_SIZE + chromGroupHeaderInfo.CompressedSize
                       + chromGroupHeaderInfo.NumPeaks * chromGroupHeaderInfo.NumTransitions * PEAK_SIZE
                       + chromGroupHeaderInfo.NumTransitions * TRANSITION_SIZE;
            }

            public MinStatisticsCollector(ChromCacheMinimizer chromCacheMinimizer)
            {
                ChromCacheMinimizer = chromCacheMinimizer;
                var filePathToReplicateIndex = new Dictionary<MsDataFileUri, int>();
                var results = Document.Settings.MeasuredResults;
                for (int i = 0; i < results.Chromatograms.Count; i++)
                {
                    var chromatogramSet = results.Chromatograms[i];
                    foreach (var msDataFilePath in chromatogramSet.MSDataFilePaths)
                    {
                        filePathToReplicateIndex[msDataFilePath] = i;
                    }
                }
                var cachedFiles = ChromCacheMinimizer.ChromatogramCache.CachedFiles;
                _fileIndexToReplicateIndex = new int[cachedFiles.Count];
                bool hasOrphanFiles = false;
                for (int fileIndex = 0; fileIndex < cachedFiles.Count; fileIndex++)
                {
                    int replicateIndex;
                    if (filePathToReplicateIndex.TryGetValue(cachedFiles[fileIndex].FilePath, out replicateIndex))
                    {
                        _fileIndexToReplicateIndex[fileIndex] = replicateIndex;
                    }
                    else
                    {
                        _fileIndexToReplicateIndex[fileIndex] = results.Chromatograms.Count;
                        hasOrphanFiles = true;
                    }
                }
                _replicates = new MinStatistics.Replicate[results.Chromatograms.Count + (hasOrphanFiles ? 1 : 0)];
                for (int i = 0; i < results.Chromatograms.Count; i++ )
                {
                    _replicates[i].Name = results.Chromatograms[i].Name;
                }
                if (hasOrphanFiles)
                {
                    _replicates[results.Chromatograms.Count].Name = "<Unmatched Files>"; // Not L10N? Function invoke uses?
                }
                foreach (var chromHeaderInfo in ChromCacheMinimizer.ChromGroupHeaderInfos)
                {
                    _replicates[_fileIndexToReplicateIndex[chromHeaderInfo.FileIndex]].OriginalFileSize +=
                        GetFileSize(chromHeaderInfo);
                }
            }

            private ChromCacheMinimizer ChromCacheMinimizer { get; set; }
            private SrmDocument Document { get { return ChromCacheMinimizer.Document; } }

            internal void ProcessChromGroup(MinimizedChromGroup minimizedChromGroup)
            {
                Assume.IsTrue(Equals(minimizedChromGroup.ChromGroupHeaderInfo, ChromCacheMinimizer.ChromGroupHeaderInfos[_processedGroupCount]));
                var headerInfo = minimizedChromGroup.ChromGroupHeaderInfo;
                int replicateIndex = _fileIndexToReplicateIndex[headerInfo.FileIndex];
                long originalFileSize = GetFileSize(headerInfo);
                double minimizedFileSize;
                if (replicateIndex < 0)
                {
                    minimizedFileSize = 0;
                }
                else
                {
                    minimizedFileSize = originalFileSize;
                    minimizedFileSize *= (minimizedChromGroup.OptimizedLastScan - minimizedChromGroup.OptimizedFirstScan + 1.0)
                                     / minimizedChromGroup.ChromGroupHeaderInfo.NumPoints;
                    minimizedFileSize = minimizedFileSize * minimizedChromGroup.RetainedTransitionIndexes.Count /
                                        minimizedChromGroup.ChromGroupHeaderInfo.NumTransitions;

                }
                _replicates[replicateIndex].MinimizedFileSize += minimizedFileSize;
                _replicates[replicateIndex].ProcessedFileSize += originalFileSize;
                _processedGroupCount++;
            }

            public MinStatistics GetStatistics()
            {
                return new MinStatistics(_replicates);
            }
        }

        /// <summary>
        /// Writes out a minimized cache file.
        /// </summary>
        /// <remarks>Maybe this should inherit from <see cref="ChromCacheWriter"/>.</remarks>
        class Writer
        {
            private readonly ChromatogramCache _originalCache;
            private readonly Stream _outputStream;
            private readonly FileStream _outputStreamPeaks;
            private readonly FileStream _outputStreamScans;
            private readonly FileStream _outputStreamScores;
            private int _peakCount;
            private int _scoreCount;
            private readonly List<ChromGroupHeaderInfo5> _chromGroupHeaderInfos = new List<ChromGroupHeaderInfo5>();
            private readonly List<ChromTransition> _transitions = new List<ChromTransition>();
            private readonly List<Type> _scoreTypes;

            public Writer(ChromatogramCache chromatogramCache, Stream outputStream, FileStream outputStreamScans, FileStream outputStreamPeaks, FileStream outputStreamScores)
            {
                _originalCache = chromatogramCache;
                _outputStream = outputStream;
                _outputStreamScans = outputStreamScans;
                _outputStreamPeaks = outputStreamPeaks;
                _outputStreamScores = outputStreamScores;
                _scoreTypes = chromatogramCache.ScoreTypes.ToList();
            }

            public void WriteChromGroup(ChromatogramGroupInfo originalChromGroup, MinimizedChromGroup minimizedChromGroup)
            {
                if (minimizedChromGroup.RetainedTransitionIndexes.Count == 0)
                {
                    return;
                }
           
                var originalHeader = originalChromGroup.Header;
                int fileIndex = originalHeader.FileIndex;
                int startTransitionIndex = _transitions.Count;
                int startPeakIndex = _peakCount;
                int startScoreIndex = _scoreCount;
                for (int iPeak = 0; iPeak < originalHeader.NumPeaks; iPeak++)
                {
                    int iScores = originalHeader.StartScoreIndex + iPeak*_scoreTypes.Count;
                    var scores = _originalCache.GetCachedScores(iScores).ToArray();
                    PrimitiveArrays.Write(_outputStreamScores, scores);
                    _scoreCount += scores.Length;
                }
                int numPoints = minimizedChromGroup.OptimizedLastScan - minimizedChromGroup.OptimizedFirstScan + 1;
                var retainedPeakIndexes = new HashSet<int>();
                if (minimizedChromGroup.OptimizedStartTime.HasValue && minimizedChromGroup.OptimizedEndTime.HasValue)
                {
                    for (int iPeak = 0; iPeak < originalHeader.NumPeaks; iPeak++)
                    {
                        bool outsideRange = false;
                        for (var transitionIndex = 0; transitionIndex < originalHeader.NumTransitions; transitionIndex++)
                        {
                            if (!minimizedChromGroup.RetainedTransitionIndexes.Contains(transitionIndex))
                                continue;

                            var peak = _originalCache.GetPeak(originalHeader.StartPeakIndex +
                                                       transitionIndex*originalHeader.NumPeaks + iPeak);
                            if (peak.StartTime < minimizedChromGroup.OptimizedStartTime.Value ||
                                peak.EndTime > minimizedChromGroup.OptimizedEndTime.Value)
                            {
                                outsideRange = true;
                                break;
                            }
                        }
                        if (!outsideRange)
                        {
                            retainedPeakIndexes.Add(iPeak);
                        }
                    }
                }
                else
                {
                    retainedPeakIndexes.UnionWith(Enumerable.Range(0, originalHeader.NumPeaks));
                }
                int numPeaks = retainedPeakIndexes.Count;
                int maxPeakIndex;
                if (retainedPeakIndexes.Contains(originalHeader.MaxPeakIndex))
                {
                    maxPeakIndex = retainedPeakIndexes.Count(index => index < originalHeader.MaxPeakIndex);
                }
                else
                {
                    maxPeakIndex = -1;
                }
                long location = _outputStream.Position;

                float[] times = originalChromGroup.Times.Skip(minimizedChromGroup.OptimizedFirstScan).Take(numPoints).ToArray();
                List<float[]> intensities = new List<float[]>();
                List<short[]> massError10Xs = originalChromGroup.MassError10XArray != null
                                                  ? new List<short[]>()
                                                  : null;
                int[][] scanIndexes = null;
                if (originalChromGroup.ScanIndexes != null)
                {
                    scanIndexes = new int[originalChromGroup.ScanIndexes.Length][];
                    for (int i = 0; i < scanIndexes.Length; i++)
                    {
                        if (originalChromGroup.ScanIndexes[i] != null)
                        {
                            scanIndexes[i] =
                                originalChromGroup.ScanIndexes[i].Skip(minimizedChromGroup.OptimizedFirstScan)
                                    .Take(numPoints)
                                    .ToArray();
                        }
                    }
                }

                foreach (var originalIndex in minimizedChromGroup.RetainedTransitionIndexes)
                {
                    _transitions.Add(_originalCache.GetTransition(originalIndex + originalHeader.StartTransitionIndex));
                    for (int originalPeakIndex = 0; originalPeakIndex < originalHeader.NumPeaks; originalPeakIndex++)
                    {
                        if (!retainedPeakIndexes.Contains(originalPeakIndex))
                            continue;

                        var originalPeak = _originalCache.GetPeak(originalHeader.StartPeakIndex +
                                                                  originalIndex*originalHeader.NumPeaks +
                                                                  originalPeakIndex);
                        _peakCount++;
                        ChromPeak.WriteArray(_outputStreamPeaks.SafeFileHandle, new[] {originalPeak});
                    }
                    intensities.Add(originalChromGroup.IntensityArray[originalIndex]
                        .Skip(minimizedChromGroup.OptimizedFirstScan)
                        .Take(numPoints).ToArray());
                    if (massError10Xs != null)
                    {
                        massError10Xs.Add(originalChromGroup.MassError10XArray[originalIndex]
                            .Skip(minimizedChromGroup.OptimizedFirstScan)
                            .Take(numPoints).ToArray());
                    }
                }
                var massError10XArray = massError10Xs != null ? massError10Xs.ToArray() : null;
                byte[] points = ChromatogramCache.TimeIntensitiesToBytes(times, intensities.ToArray(), massError10XArray, scanIndexes);
                // Compress the data (can be huge for AB data with lots of zeros)
                byte[] pointsCompressed = points.Compress(3);
                int lenCompressed = pointsCompressed.Length;
                _outputStream.Write(pointsCompressed, 0, lenCompressed);
                var header = new ChromGroupHeaderInfo5(originalHeader.Precursor,
                                                      originalHeader.TextIdIndex,
                                                      originalHeader.TextIdLen,
                                                      fileIndex,
                                                      _transitions.Count - startTransitionIndex,
                                                      startTransitionIndex,
                                                      numPeaks,
                                                      startPeakIndex,
                                                      startScoreIndex,
                                                      maxPeakIndex,
                                                      numPoints,
                                                      pointsCompressed.Length,
                                                      location,
                                                      originalHeader.Flags,
                                                      originalHeader.StatusId,
                                                      originalHeader.StatusRank);
                _chromGroupHeaderInfos.Add(header);
            }

            public void WriteEndOfFile()
            {
                _originalCache.WriteScanIds(_outputStreamScans);

                ChromatogramCache.WriteStructs(_outputStream,
                                               _outputStreamScans,
                                               _outputStreamPeaks,
                                               _outputStreamScores,
                                               _originalCache.CachedFiles,
                                               _chromGroupHeaderInfos,
                                               _transitions,
                                               _scoreTypes,
                                               _scoreCount,
                                               _peakCount,
                                               _originalCache);
            }
        }
    }
}
