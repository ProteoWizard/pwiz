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
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results.Scoring;
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
            var writer = outStream == null ? null : new Writer(ChromatogramCache, settings.CacheFormat, outStream, outStreamScans, outStreamPeaks, outStreamScores);
            var statisticsCollector = new MinStatisticsCollector(this);

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
                    foreach (var chromGroupInfo in ChromatogramCache.LoadChromatogramInfos(nodePep, nodeGroup, _tolerance, null))
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
            minimizer.RunAsync(MINIMIZING_THREADS, @"Minimizing/Writing", MAX_GROUP_READ_AHEAD);

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
            var fileIndexes = new List<int>();
            for (int fileIndex = 0; fileIndex < Document.Settings.MeasuredResults.Chromatograms.Count; fileIndex++)
            {
                var chromatogramSet = Document.Settings.MeasuredResults.Chromatograms[fileIndex];
                if (chromatogramSet.MSDataFilePaths.Any(path=>Equals(path, chromatogramGroupInfo.FilePath)))
                {
                    fileIndexes.Add(fileIndex);
                }
            }
            var chromatograms = Enumerable.Range(0, chromatogramGroupInfo.NumTransitions)
                .Select(chromatogramGroupInfo.GetRawTransitionInfo).ToArray();
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
                    if (0!=chromatogram.ProductMz.CompareTolerant(transitionDocNode.Mz, _tolerance))
                    {
                        continue;
                    }
                    matchingTransitions.Add(transitionDocNode);
                    foreach (var fileIndex in fileIndexes)
                    {
                        var chromatogramSet = transitionDocNode.Results[fileIndex];
                        if (chromatogramSet.IsEmpty)
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
                    || matchingTransitions.Count > 0
                    || (chromatogram.PrecursorMz.Equals(SignedMz.ZERO) && !settings.DiscardAllIonsChromatograms);
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
                result.SetStartEndTime((float)(minRetentionTime - settings.NoiseTimeRange), (float)(maxRetentionTime + settings.NoiseTimeRange));
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
            }

            public ChromGroupHeaderInfo ChromGroupHeaderInfo { get { return _chromatogramGroupInfo.Header; } }

            public IList<int> RetainedTransitionIndexes { get; set; }
            public float? OptimizedStartTime { get; private set; }
            public float? OptimizedEndTime { get; private set; }

            public int NumPeaks { get; private set; }
            public int TotalPeakCount { get; private set; }
            public int MaxPeakIndex { get; private set; }
            public TimeIntensitiesGroup MinimizedTimeIntensitiesGroup { get; private set; }
            public ChromPeak[] MinimizedPeaks { get; private set; }
            public IList<ChromTransition> Transitions { get; private set; }
            public float[] PeakScores { get; private set; }

            public void SetStartEndTime(float minRetentionTime, float maxRetentionTime)
            {
                OptimizedStartTime = minRetentionTime;
                OptimizedEndTime = maxRetentionTime;
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
                var groupPeaks = cache.ReadPeaks(header);
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

                            var peak = groupPeaks[transitionIndex * header.NumPeaks + iPeak];
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
                var groupScores = cache.ReadScores(header);
                for (int iPeak = 0; iPeak < numPeaks; iPeak++)
                {
                    if (!retainedPeakIndexes.Contains(iPeak))
                        continue;

                    int iScores = iPeak * cache.ScoreTypesCount;
                    peakScores.AddRange(Enumerable.Range(iScores, cache.ScoreTypesCount).Select(i => groupScores[i]));
                }
                PeakScores = peakScores.ToArray();
                var peaks = new List<ChromPeak>();
                Transitions = new List<ChromTransition>();
                foreach (var originalIndex in RetainedTransitionIndexes)
                {
                    Transitions.Add(cache.GetTransition(originalIndex + header.StartTransitionIndex));
                    for (int iPeak = 0; iPeak < numPeaks; iPeak++)
                    {
                        if (!retainedPeakIndexes.Contains(iPeak))
                            continue;

                        var originalPeak = groupPeaks[originalIndex*numPeaks + iPeak];
                        peaks.Add(originalPeak);
                        TotalPeakCount++;
                    }
                }
                MinimizedPeaks = peaks.ToArray();
            }

            public void CalcChromatogramBytes()
            {
                TimeIntensitiesGroup timeIntensitiesGroup;
                if (OptimizedStartTime.HasValue && OptimizedEndTime.HasValue)
                {
                    timeIntensitiesGroup = _chromatogramGroupInfo.TimeIntensitiesGroup
                        .Truncate(OptimizedStartTime.Value, OptimizedEndTime.Value);
                }
                else
                {
                    timeIntensitiesGroup = _chromatogramGroupInfo.TimeIntensitiesGroup;
                }
                MinimizedTimeIntensitiesGroup = timeIntensitiesGroup.RetainTransitionIndexes(new HashSet<int>(RetainedTransitionIndexes));
            }
        }

        public class Settings : Immutable
        {
            public Settings()
            {
                CacheFormat = CacheFormat.CURRENT;
            }

            public double? NoiseTimeRange { get; private set; }
            public Settings ChangeNoiseTimeRange(double? value)
            {
                return ChangeProp(ImClone(this), im => im.NoiseTimeRange = value);
            }
            public bool DiscardAllIonsChromatograms { get; private set; }
            public Settings ChangeDiscardAllIonsChromatograms(bool value)
            {
                return ChangeProp(ImClone(this), im => im.DiscardAllIonsChromatograms = value);
            }
            public bool DiscardUnmatchedChromatograms { get; private set; }
            public Settings ChangeDiscardUnmatchedChromatograms(bool value)
            {
                return ChangeProp(ImClone(this), im => im.DiscardUnmatchedChromatograms = value);
            }
            public CacheFormat CacheFormat { get; private set; }

            public Settings ChangeCacheFormat(CacheFormat cacheFormat)
            {
                return ChangeProp(ImClone(this), im => im.CacheFormat = cacheFormat);
            }
        }

        public class MinStatistics
        {
            public MinStatistics(IEnumerable<Replicate> replicates)
            {
                Replicates = replicates.ToArray();
                OriginalFileSize = Math.Max(1, Replicates.Select(r => r.OriginalFileSize).Sum());
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
            private DateTime _lastOutput = DateTime.UtcNow; // Said to be 117x faster than Now and this is for a delta

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
                    _replicates[results.Chromatograms.Count].Name = @"<Unmatched Files>"; // CONSIDER: localize? Function invoke uses?
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
                    var header = minimizedChromGroup.ChromGroupHeaderInfo;
                    if (header.StartTime.HasValue && header.EndTime.HasValue && minimizedChromGroup.OptimizedEndTime.HasValue && minimizedChromGroup.OptimizedStartTime.HasValue)
                    {
                        double oldLength = header.EndTime.Value - header.StartTime.Value;
                        double newLength = minimizedChromGroup.OptimizedEndTime.Value -
                                           minimizedChromGroup.OptimizedStartTime.Value;
                        if (oldLength > 0 && newLength > 0)
                        {
                            minimizedFileSize *= newLength/oldLength;
                        }
                    }
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

                var currentTime = DateTime.UtcNow;
                // Show progress at least every second
                if (!isFinal && (currentTime - _lastOutput).TotalSeconds < 1)
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
            private readonly CacheFormat _cacheFormat;
            private readonly Stream _outputStream;
            private readonly FileStream _outputStreamPeaks;
            private readonly FileStream _outputStreamScans;
            private readonly FileStream _outputStreamScores;
            private int _peakCount;
            private int _scoreCount;
            private readonly BlockedArrayList<ChromGroupHeaderInfo> _chromGroupHeaderInfos =
                new BlockedArrayList<ChromGroupHeaderInfo>(ChromGroupHeaderInfo.SizeOf, ChromGroupHeaderInfo.DEFAULT_BLOCK_SIZE);
            private readonly BlockedArrayList<ChromTransition> _transitions =
                new BlockedArrayList<ChromTransition>(ChromTransition.SizeOf, ChromTransition.DEFAULT_BLOCK_SIZE);
            private readonly FeatureNames _scoreTypes;
            private readonly List<byte> _textIdBytes = new List<byte>();
            private readonly IDictionary<ImmutableList<byte>, int> _textIdIndexes 
                = new Dictionary<ImmutableList<byte>, int>();

            public Writer(ChromatogramCache chromatogramCache, CacheFormat cacheFormat, Stream outputStream, FileStream outputStreamScans, FileStream outputStreamPeaks, FileStream outputStreamScores)
            {
                _originalCache = chromatogramCache;
                _cacheFormat = cacheFormat;
                _outputStream = outputStream;
                _outputStreamScans = outputStreamScans;
                _outputStreamPeaks = outputStreamPeaks;
                _outputStreamScores = outputStreamScores;
                _scoreTypes = chromatogramCache.ScoreTypes;
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

                var flags = originalHeader.Flags;
                MemoryStream pointsStream = new MemoryStream();
                var transitionChromSources = minimizedChromGroup.Transitions.Select(tran => tran.Source).ToArray();
                var timeIntensitiesGroup = minimizedChromGroup.MinimizedTimeIntensitiesGroup;
                if (timeIntensitiesGroup is RawTimeIntensities && _cacheFormat.FormatVersion < CacheFormatVersion.Twelve)
                {
                    timeIntensitiesGroup = ((RawTimeIntensities) minimizedChromGroup.MinimizedTimeIntensitiesGroup)
                            .Interpolate(transitionChromSources);
                }
                timeIntensitiesGroup.WriteToStream(pointsStream);
                if (timeIntensitiesGroup is RawTimeIntensities)
                {
                    flags |= ChromGroupHeaderInfo.FlagValues.raw_chromatograms;
                }
                else
                {
                    flags &= ~ChromGroupHeaderInfo.FlagValues.raw_chromatograms;
                    var interpolatedTimeIntensities = timeIntensitiesGroup as InterpolatedTimeIntensities;
                    if (interpolatedTimeIntensities != null)
                    {
                        flags &=
                            ~(ChromGroupHeaderInfo.FlagValues.has_frag_scan_ids |
                              ChromGroupHeaderInfo.FlagValues.has_sim_scan_ids |
                              ChromGroupHeaderInfo.FlagValues.has_ms1_scan_ids);
                        var scanIdsByChromSource = interpolatedTimeIntensities.ScanIdsByChromSource();
                        if (scanIdsByChromSource.ContainsKey(ChromSource.fragment))
                        {
                            flags |= ChromGroupHeaderInfo.FlagValues.has_frag_scan_ids;
                        }
                        if (scanIdsByChromSource.ContainsKey(ChromSource.ms1))
                        {
                            flags |= ChromGroupHeaderInfo.FlagValues.has_ms1_scan_ids;
                        }
                        if (scanIdsByChromSource.ContainsKey(ChromSource.sim))
                        {
                            flags |= ChromGroupHeaderInfo.FlagValues.has_sim_scan_ids;
                        }
                    }
                }

                int numPeaks = minimizedChromGroup.NumPeaks;
                int maxPeakIndex = minimizedChromGroup.MaxPeakIndex;

                _cacheFormat.ChromPeakSerializer().WriteItems(_outputStreamPeaks, minimizedChromGroup.MinimizedPeaks);

                long location = _outputStream.Position;
                int lenUncompressed = (int) pointsStream.Length;
                byte[] pointsCompressed = pointsStream.ToArray().Compress(3);
                int lenCompressed = pointsCompressed.Length;
                _outputStream.Write(pointsCompressed, 0, lenCompressed);
                int textIdIndex;
                int textIdLen;
                var newTextId = GetNewTextId(originalHeader);
                if (newTextId == null)
                {
                    textIdIndex = -1;
                    textIdLen = 0;
                }
                else
                {
                    textIdIndex = CalcTextIdOffset(newTextId);
                    textIdLen = newTextId.Count;
                }
                
                var header = new ChromGroupHeaderInfo(originalHeader.Precursor,
                                                      textIdIndex,
                                                      textIdLen,
                                                      fileIndex,
                                                      _transitions.Count - startTransitionIndex,
                                                      startTransitionIndex,
                                                      numPeaks,
                                                      startPeakIndex,
                                                      startScoreIndex,
                                                      maxPeakIndex,
                                                      timeIntensitiesGroup.NumInterpolatedPoints,
                                                      lenCompressed,
                                                      lenUncompressed,
                                                      location,
                                                      flags,
                                                      originalHeader.StatusId,
                                                      originalHeader.StatusRank,
                                                      originalHeader.StartTime, originalHeader.EndTime,
                                                      originalHeader.CollisionalCrossSection, 
                                                      originalHeader.IonMobilityUnits);
                _chromGroupHeaderInfos.Add(header);
            }

            public void WriteEndOfFile()
            {
                _originalCache.WriteScanIds(_outputStreamScans);

                _chromGroupHeaderInfos.Sort();
                ChromatogramCache.WriteStructs(_cacheFormat,
                                               _outputStream,
                                               _outputStreamScans,
                                               _outputStreamPeaks,
                                               _outputStreamScores,
                                               _originalCache.CachedFiles,
                                               _chromGroupHeaderInfos,
                                               _transitions,
                                               _textIdBytes,
                                               _scoreTypes,
                                               _scoreCount,
                                               _peakCount,
                                               out _);
            }

            public ImmutableList<byte> GetNewTextId(ChromGroupHeaderInfo chromGroupHeaderInfo)
            {
                byte[] textIdBytes = _originalCache.GetTextIdBytes(chromGroupHeaderInfo.TextIdIndex, chromGroupHeaderInfo.TextIdLen);
                if (textIdBytes == null)
                {
                    return null;
                }
                const CacheFormatVersion versionNewTextId = CacheFormatVersion.Thirteen;
                ImmutableList<byte> newTextId;
                if (_originalCache.Version < versionNewTextId ||
                    _cacheFormat.FormatVersion >= versionNewTextId)
                {
                    newTextId = ImmutableList.ValueOf(textIdBytes);
                }
                else
                {
                    if (textIdBytes[0] == '#')
                    {
                        newTextId = ImmutableList.ValueOf(textIdBytes);
                    }
                    else
                    {
                        var oldKey = new PeptideLibraryKey(Encoding.UTF8.GetString(textIdBytes), 0);
                        var newKey = oldKey.FormatToOneDecimal();
                        newTextId = ImmutableList.ValueOf(Encoding.UTF8.GetBytes(newKey.ModifiedSequence));
                    }
                }
                return newTextId;
            }

            public int CalcTextIdOffset(ImmutableList<byte> textId)
            {
                int offset;
                if (!_textIdIndexes.TryGetValue(textId, out offset))
                {
                    offset = _textIdBytes.Count;
                    _textIdBytes.AddRange(textId);
                    _textIdIndexes.Add(textId, offset);
                }
                return offset;
            }
        }
    }
}
