﻿/*
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
using pwiz.Common.SystemUtil;
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

        private int CompareLocation(ChromGroupHeaderInfo chromGroupHeaderInfo1, ChromGroupHeaderInfo chromGroupHeaderInfo2)
        {
            return chromGroupHeaderInfo1.LocationPoints.CompareTo(chromGroupHeaderInfo2.LocationPoints);
        }

        public SrmDocument Document { get; private set; }
        public ChromatogramCache ChromatogramCache { get; private set; }
        public IList<ChromGroupHeaderInfo> ChromGroupHeaderInfos
        {
            get; private set;
        }

        private const int MAX_GROUP_READ_AHEAD = 100;
        private readonly int MINIMIZING_THREADS = ParallelEx.SINGLE_THREADED ? 1 : 8;

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

            var chromGroupHeaderToIndex = new Dictionary<long, int>(ChromGroupHeaderInfos.Count);
            for (int i = 0; i < ChromGroupHeaderInfos.Count; i++)
            {
                var cghi = ChromGroupHeaderInfos[i];
                chromGroupHeaderToIndex.Add(cghi.LocationPoints, i);
            }
            var chromGroups = new ChromatogramGroupInfo[ChromGroupHeaderInfos.Count];
            var transitionGroups = new List<TransitionGroupDocNode>[ChromGroupHeaderInfos.Count];
            foreach (var nodePep in Document.Molecules)
            {
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    ChromatogramGroupInfo[] groupInfos;
                    ChromatogramCache.TryLoadChromatogramInfo(nodePep, nodeGroup, _tolerance, null, out groupInfos);
                    foreach (var chromGroupInfo in groupInfos)
                    {
                        int headerIndex = chromGroupHeaderToIndex[chromGroupInfo.Header.LocationPoints];
                        if (chromGroups[headerIndex] == null)
                        {
                            chromGroups[headerIndex] = chromGroupInfo;
                            transitionGroups[headerIndex] = new List<TransitionGroupDocNode>();
                        }
                        transitionGroups[headerIndex].Add(nodeGroup);
                    }
                }
            }

            var minimizer = new QueueWorker<MinimizeParams>(null, MinimizeAndWrite);
            minimizer.RunAsync(MINIMIZING_THREADS, "Minimizing/Writing", MAX_GROUP_READ_AHEAD); // Not L10N

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
                        chromGroupInfo.ReadChromatogram(ChromatogramCache, true);
                    }
                    catch (Exception exception)
                    {
                        Trace.TraceWarning("Unable to read chromatogram {0}", exception); // Not L10N
                    }
                }

                if (minimizer.Exception != null)
                    break;

                minimizer.Add(new MinimizeParams(writer, settings, chromGroupInfo, transitionGroupDocNodes, progressCallback, statisticsCollector));

                // Null out the ChromGroup in our array so it can be garbage collected.
                chromGroups[iHeader] = null;
            }

            minimizer.DoneAdding(true);
            if (minimizer.Exception != null)
                throw minimizer.Exception;

            statisticsCollector.ReportProgress(progressCallback, true);

            if (writer != null)
            {
                writer.WriteEndOfFile();
            }
        }

        private void MinimizeAndWrite(MinimizeParams p, int threadIndex)
        {
            // Handle all decompression -> convert to values -> minimize -> convert to bytes -> compression
            // in parallel processing, since this is where most of the time is spent
            var minimizedChromGroup = MinimizeChromGroup(p.Settings, p.ChromGroupInfo, p.TransitionGroupDocNodes);
            if (p.Writer != null)
                minimizedChromGroup.CalcWriteArrays(ChromatogramCache);

            // Synchronized
            p.Write(minimizedChromGroup);
        }

        /// <summary>
        /// Decides what information from a ChromatogramGroupInfo should be thrown away.
        /// ChromatogramGroupInfo's can have the the retention time range shortened, and certain
        /// transitions discarded.
        /// </summary>
        private MinimizedChromGroup MinimizeChromGroup(Settings settings, ChromatogramGroupInfo chromatogramGroupInfo, IList<TransitionGroupDocNode> transitionGroups)
        {
            chromatogramGroupInfo.EnsureDecompressed();

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
                    if (0!=ChromKey.CompareTolerant(chromatogram.ProductMz, transitionDocNode.Mz, _tolerance))
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
            var result = new MinimizedChromGroup(chromatogramGroupInfo)
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

        private class MinimizeParams
        {
            public MinimizeParams(Writer writer,
                Settings settings,
                ChromatogramGroupInfo chromGroupInfo,
                IList<TransitionGroupDocNode> transitionGroupDocNodes,
                ProgressCallback progressCallback,
                MinStatisticsCollector statisticsCollector)
            {
                Writer = writer;
                Settings = settings;
                ChromGroupInfo = chromGroupInfo;
                TransitionGroupDocNodes = transitionGroupDocNodes;
                ProgressCallback = progressCallback;
                StatisticsCollector = statisticsCollector;
            }

            public Writer Writer { get; private set; }
            public Settings Settings { get; private set; }
            public ChromatogramGroupInfo ChromGroupInfo { get; private set; }
            public IList<TransitionGroupDocNode> TransitionGroupDocNodes { get; private set; }
            private ProgressCallback ProgressCallback { get; set; }
            private MinStatisticsCollector StatisticsCollector { get; set; }

            private void UpdateStatistics(MinimizedChromGroup minimizedChromGroup)
            {
                StatisticsCollector.ProcessChromGroup(minimizedChromGroup);
                StatisticsCollector.ReportProgress(ProgressCallback, false);
            }

            public void Write(MinimizedChromGroup minimizedChromGroup)
            {
                // Then one thread at a time for updating statistics and writing
                lock (StatisticsCollector)
                {
                    UpdateStatistics(minimizedChromGroup);

                    if (Writer != null)
                        Writer.WriteChromGroup(ChromGroupInfo, minimizedChromGroup);
                }
            }
        }

        internal class MinimizedChromGroup
        {
            private readonly ChromatogramGroupInfo _chromatogramGroupInfo;

            public MinimizedChromGroup(ChromatogramGroupInfo chromGroupHeaderInfo)
            {
                _chromatogramGroupInfo = chromGroupHeaderInfo;
                OptimizedFirstScan = 0;
                OptimizedLastScan = chromGroupHeaderInfo.Header.NumPoints - 1;
            }

            public ChromGroupHeaderInfo ChromGroupHeaderInfo { get { return _chromatogramGroupInfo.Header; } }

            public IList<int> RetainedTransitionIndexes { get; set; }
            public float? OptimizedStartTime { get; private set; }
            public float? OptimizedEndTime { get; private set; }
            public int OptimizedFirstScan { get; private set; }
            public int OptimizedLastScan { get; private set; }
            public int OptimizedScanCount { get { return OptimizedLastScan + 1 - OptimizedFirstScan; } }

            // Output values
            public int UncompressedLength { get; private set; }
            public byte[] ChromatogramBytes { get; private set; }
            public int NumPeaks { get; private set; }
            public int TotalPeakCount { get; private set; }
            public int MaxPeakIndex { get; private set; }
            public byte[] PeakBytes { get; private set; }
            public IList<ChromTransition> Transitions { get; private set; }
            public float[] PeakScores { get; private set; }

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

            public void CalcWriteArrays(ChromatogramCache chromatogramCache)
            {
                if (RetainedTransitionIndexes.Count == 0)
                    return;

                CalcPeakInfo(chromatogramCache);
                CalcChromatogramBytes();
            }

            public void CalcPeakInfo(ChromatogramCache cache)
            {
                var header = _chromatogramGroupInfo.Header;
                int numPeaks = header.NumPeaks;
                var retainedPeakIndexes = new HashSet<int>();
                if (!OptimizedStartTime.HasValue || !OptimizedEndTime.HasValue)
                {
                    retainedPeakIndexes.UnionWith(Enumerable.Range(0, numPeaks));
                }
                else
                {
                    for (int iPeak = 0; iPeak < header.NumPeaks; iPeak++)
                    {
                        bool outsideRange = false;
                        for (var transitionIndex = 0; transitionIndex < header.NumTransitions; transitionIndex++)
                        {
                            if (!RetainedTransitionIndexes.Contains(transitionIndex))
                                continue;

                            var peak = cache.GetPeak(header.StartPeakIndex +
                                                       transitionIndex * header.NumPeaks +
                                                       iPeak);
                            if (peak.StartTime < OptimizedStartTime.Value || peak.EndTime > OptimizedEndTime.Value)
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

                NumPeaks = retainedPeakIndexes.Count;
                if (retainedPeakIndexes.Contains(header.MaxPeakIndex))
                    MaxPeakIndex = retainedPeakIndexes.Count(index => index < header.MaxPeakIndex);
                else
                    MaxPeakIndex = -1;

                var peakScores = new List<float>();
                for (int iPeak = 0; iPeak < numPeaks; iPeak++)
                {
                    if (!retainedPeakIndexes.Contains(iPeak))
                        continue;

                    int iScores = header.StartScoreIndex + iPeak * cache.ScoreTypesCount;
                    peakScores.AddRange(cache.GetCachedScores(iScores));
                }
                PeakScores = peakScores.ToArray();
                var peakBytes = new List<byte>();
                Transitions = new List<ChromTransition>();
                foreach (var originalIndex in RetainedTransitionIndexes)
                {
                    Transitions.Add(cache.GetTransition(originalIndex + header.StartTransitionIndex));
                    for (int iPeak = 0; iPeak < numPeaks; iPeak++)
                    {
                        if (!retainedPeakIndexes.Contains(iPeak))
                            continue;

                        var originalPeak = cache.GetPeak(header.StartPeakIndex + originalIndex*numPeaks + iPeak);
                        peakBytes.AddRange(ChromPeak.GetBytes(originalPeak));
                        TotalPeakCount++;
                    }
                }
                PeakBytes = peakBytes.ToArray();
            }

            public void CalcChromatogramBytes()
            {
                int numPoints = OptimizedScanCount;

                float[] times = CopyMinimized(_chromatogramGroupInfo.Times, new float[numPoints]);
                int[][] scanIndexes = null;
                if (_chromatogramGroupInfo.ScanIndexes != null)
                {
                    scanIndexes = new int[_chromatogramGroupInfo.ScanIndexes.Length][];
                    for (int i = 0; i < scanIndexes.Length; i++)
                    {
                        var sourceIndexes = _chromatogramGroupInfo.ScanIndexes[i];
                        if (sourceIndexes != null)
                            scanIndexes[i] = CopyMinimized(sourceIndexes, new int[numPoints]);
                    }
                }

                int countTrans = RetainedTransitionIndexes.Count;
                float[][] intensities = new float[countTrans][];
                short[][] massError10Xs = null;
                if (_chromatogramGroupInfo.MassError10XArray != null)
                    massError10Xs = new short[countTrans][];

                for (int i = 0; i < countTrans; i++)
                {
                    int originalIndex = RetainedTransitionIndexes[i];
                    intensities[i] = CopyMinimized(_chromatogramGroupInfo.IntensityArray[originalIndex], new float[numPoints]);
                    if (massError10Xs != null)
                        massError10Xs[i] = CopyMinimized(_chromatogramGroupInfo.MassError10XArray[originalIndex], new short[numPoints]);
                }
                byte[] points = ChromatogramCache.TimeIntensitiesToBytes(times, intensities, massError10Xs, scanIndexes);
                UncompressedLength = points.Length;
                // Compress the data (can be huge for AB data with lots of zeros)
                ChromatogramBytes = points.Compress(3);
            }

            private TVal[] CopyMinimized<TVal>(TVal[] sourceArray, TVal[] destArray)
            {
                Array.Copy(sourceArray, OptimizedFirstScan, destArray, 0, destArray.Length);
                return destArray;
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
                CHROM_GROUP_HEADER_INFO_SIZE = sizeof(ChromGroupHeaderInfo);
                PEAK_SIZE = sizeof(ChromPeak);
                TRANSITION_SIZE = sizeof(ChromTransition);
            }

            private readonly MinStatistics.Replicate[] _replicates;
            private readonly int[] _fileIndexToReplicateIndex;
            private DateTime _lastOutput = DateTime.Now;

            private static long GetFileSize(ChromGroupHeaderInfo chromGroupHeaderInfo)
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
            }

            public void ReportProgress(ProgressCallback progressCallback, bool isFinal)
            {
                if (progressCallback == null)
                    return;

                var currentTime = DateTime.Now;
                // Show progress at least every second
                if (!isFinal && (currentTime - _lastOutput).Seconds < 1)
                    return;

                progressCallback(new MinStatistics(_replicates));
                _lastOutput = currentTime;
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
            private readonly List<ChromGroupHeaderInfo> _chromGroupHeaderInfos = new List<ChromGroupHeaderInfo>();
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

                _scoreCount += minimizedChromGroup.PeakScores.Length;
                PrimitiveArrays.Write(_outputStreamScores, minimizedChromGroup.PeakScores);

                _peakCount += minimizedChromGroup.TotalPeakCount;
                _transitions.AddRange(minimizedChromGroup.Transitions);

                int numPoints = minimizedChromGroup.OptimizedScanCount;
                int numPeaks = minimizedChromGroup.NumPeaks;
                int maxPeakIndex = minimizedChromGroup.MaxPeakIndex;

                var peakBytes = minimizedChromGroup.PeakBytes;
                _outputStreamPeaks.Write(peakBytes, 0, peakBytes.Length);

                long location = _outputStream.Position;
                int lenUncompressed = minimizedChromGroup.UncompressedLength;
                byte[] pointsCompressed = minimizedChromGroup.ChromatogramBytes;
                int lenCompressed = pointsCompressed.Length;
                _outputStream.Write(pointsCompressed, 0, lenCompressed);

                var header = new ChromGroupHeaderInfo(originalHeader.Precursor,
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
                                                      lenCompressed,
                                                      lenUncompressed,
                                                      location,
                                                      originalHeader.Flags,
                                                      originalHeader.StatusId,
                                                      originalHeader.StatusRank,
                                                      originalHeader.StartTime, originalHeader.EndTime);
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
