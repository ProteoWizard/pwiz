/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;

namespace pwiz.Skyline.Model.GroupComparison
{
    /// <summary>
    /// Holds per-peptide median-polished log2 abundances and the per-file normalization
    /// factors derived from those abundances. Used when SummarizationMethod=MEDIANPOLISH
    /// is paired with a peptide-level normalization method (EQUALIZE_MEDIANS or RT_LOESS),
    /// so that the normalization factor is computed from the post-rollup peptide values
    /// rather than from raw transition values.
    ///
    /// This mirrors the skyline-prism pipeline, where median normalization and RT-lowess
    /// normalization are applied AFTER the transition->peptide rollup, on log2 peptide
    /// abundances.
    /// </summary>
    public class PolishedPeptideAbundances
    {
        public static readonly PolishedPeptideAbundances EMPTY = new PolishedPeptideAbundances(
            new Dictionary<FileDataKey, FileSummary>(),
            new Dictionary<ReferenceValue<PeptideDocNode>, double?[]>(),
            0, double.NaN);

        private const int RT_GRID_POINTS = 100;
        private const double LOESS_BANDWIDTH = 0.3;

        private readonly IDictionary<FileDataKey, FileSummary> _fileSummaries;
        private readonly IDictionary<ReferenceValue<PeptideDocNode>, double?[]> _polishedByPeptide;
        private readonly double _medianOfMedians;

        private PolishedPeptideAbundances(
            IDictionary<FileDataKey, FileSummary> fileSummaries,
            IDictionary<ReferenceValue<PeptideDocNode>, double?[]> polishedByPeptide,
            int peptideCount, double medianOfMedians)
        {
            _fileSummaries = fileSummaries;
            _polishedByPeptide = polishedByPeptide;
            PeptideCount = peptideCount;
            _medianOfMedians = medianOfMedians;
        }

        /// <summary>
        /// Returns the median-polished log2 abundance for the given peptide in the given
        /// replicate, or null if the peptide was not polished or the replicate index is
        /// out of range. The value is the polish row effect (plus the standard scale
        /// factor) on the un-normalized log2 transition input - i.e. the value you'd
        /// take 2^x of to get a linear "MedianPolishedArea".
        /// </summary>
        public double? GetPolishedLog2Abundance(PeptideDocNode peptide, int replicateIndex)
        {
            if (peptide == null)
            {
                return null;
            }
            if (!_polishedByPeptide.TryGetValue(new ReferenceValue<PeptideDocNode>(peptide), out var arr))
            {
                return null;
            }
            if (replicateIndex < 0 || replicateIndex >= arr.Length)
            {
                return null;
            }
            return arr[replicateIndex];
        }

        public int PeptideCount { get; }

        public bool HasData => _fileSummaries.Count > 0;

        /// <summary>
        /// Returns the log2 adjustment for EQUALIZE_MEDIANS normalization at the peptide level.
        /// Adjustment = file_log2_median - global_log2_median (where the medians are taken
        /// over polished log2 peptide abundances).
        /// </summary>
        public double? GetMedianAdjustment(int replicateIndex, ChromFileInfoId chromFileInfoId)
        {
            if (!_fileSummaries.TryGetValue(new FileDataKey(replicateIndex, chromFileInfoId), out var summary))
            {
                return null;
            }
            if (double.IsNaN(_medianOfMedians))
            {
                return null;
            }
            return summary.Log2Median - _medianOfMedians;
        }

        /// <summary>
        /// Returns the log2 adjustment for RT_LOESS normalization at the peptide level.
        /// Adjustment = sample_loess_curve(peptideRt) - global_median_loess_curve(peptideRt),
        /// where the curves are fit on (peptide mean RT, polished log2 abundance) per file.
        /// </summary>
        public double? GetRtLoessAdjustment(int replicateIndex, ChromFileInfoId chromFileInfoId,
            double peptideRt)
        {
            if (!_fileSummaries.TryGetValue(new FileDataKey(replicateIndex, chromFileInfoId), out var summary))
            {
                return null;
            }
            if (summary.LoessFitted == null || _globalLoessFitted == null)
            {
                return null;
            }
            double sampleValue = LoessInterpolator.Interpolate(peptideRt, _loessRtGrid, summary.LoessFitted);
            double globalValue = LoessInterpolator.Interpolate(peptideRt, _loessRtGrid, _globalLoessFitted);
            return sampleValue - globalValue;
        }

        // RT grid + global median curve are shared across all per-file curves.
        private double[] _loessRtGrid;
        private double[] _globalLoessFitted;

        public static readonly Producer<ReferenceValue<SrmDocument>, PolishedPeptideAbundances> PRODUCER =
            Producer.FromFunction<ReferenceValue<SrmDocument>, PolishedPeptideAbundances>(
                (productionMonitor, doc) => Produce(productionMonitor, doc.Value));

        public static PolishedPeptideAbundances Get(CancellationToken cancellationToken, SrmDocument document)
        {
            return Produce(new ProductionMonitor(cancellationToken, _ => { }), document);
        }

        public static PolishedPeptideAbundances Produce(ProductionMonitor productionMonitor, SrmDocument document)
        {
            if (!document.Settings.HasResults)
            {
                return EMPTY;
            }
            var cancellationToken = productionMonitor.CancellationToken;
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            int replicateCount = document.Settings.MeasuredResults.Chromatograms.Count;
            var medianPolishReplicateIndexes = PeptideQuantifier.GetMedianPolishReplicates(document.Settings);
            var noneNormalization = new NormalizedValueCalculator(cancellationToken, document);

            // Per (replicateIndex, chromFileInfoId): list of log2 polished peptide abundances.
            var polishedPerFile = new Dictionary<FileDataKey, List<RtAreaPoint>>();
            // Per peptide: full polished log2 array indexed by replicate. Used by the
            // PeptideResult.MedianPolishedArea report column.
            var polishedByPeptide = new Dictionary<ReferenceValue<PeptideDocNode>, double?[]>();
            int peptideCount = 0;

            var moleculeGroupMolecules =
                document.MoleculeGroups.SelectMany(mg => mg.Molecules.Where(mol=>!mol.IsDecoy).Select(mol => (mg, mol))).ToList();
            int molCount = moleculeGroupMolecules.Count;
            int processed = 0;
            ParallelEx.For(0, moleculeGroupMolecules.Count, i=>
            {
                bool contributedAny = false;
                try
                {
                    var (peptideGroup, peptide) = moleculeGroupMolecules[i];
                    cancellationToken.ThrowIfCancellationRequested();
                    if (PeptideDocNode.STANDARD_TYPE_IRT == peptide.GlobalStandardType)
                    {
                        return;
                    }

                    var quantifier = PeptideQuantifier.GetPeptideQuantifier(noneNormalization, document.Settings,
                        peptideGroup.PeptideGroup, peptide);
                    var polished = quantifier.PolishUnnormalizedTransitions(document.Settings,
                        medianPolishReplicateIndexes);
                    if (polished == null)
                    {
                        return;
                    }
                    lock (polishedByPeptide)
                    {
                        polishedByPeptide[new ReferenceValue<PeptideDocNode>(peptide)] = polished;
                    }

                    for (int iReplicate = 0; iReplicate < replicateCount && iReplicate < polished.Length; iReplicate++)
                    {
                        var value = polished[iReplicate];
                        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                        {
                            continue;
                        }

                        double meanRt = GetPeptideMeanRt(peptide, iReplicate);
                        var fileIds = GetReplicateFileIds(document.Settings, iReplicate);
                        foreach (var fileId in fileIds)
                        {
                            var key = new FileDataKey(iReplicate, fileId);
                            lock (polishedPerFile)
                            {
                                if (!polishedPerFile.TryGetValue(key, out var list))
                                {
                                    list = new List<RtAreaPoint>();
                                    polishedPerFile.Add(key, list);
                                }

                                list.Add(new RtAreaPoint(meanRt, value.Value));
                            }

                            contributedAny = true;
                        }
                    }
                }
                finally
                {
                    lock (polishedPerFile)
                    {
                        if (contributedAny)
                        {
                            peptideCount++;
                        }
                        processed++;
                        productionMonitor.SetProgress(processed * 100 / molCount);
                    }
                }
            });

            if (polishedPerFile.Count == 0)
            {
                return EMPTY;
            }

            return BuildSummaries(polishedPerFile, polishedByPeptide, peptideCount, cancellationToken);
        }

        private static PolishedPeptideAbundances BuildSummaries(
            IDictionary<FileDataKey, List<RtAreaPoint>> polishedPerFile,
            IDictionary<ReferenceValue<PeptideDocNode>, double?[]> polishedByPeptide,
            int peptideCount,
            CancellationToken cancellationToken)
        {
            // Per-file log2 medians.
            var summaries = new Dictionary<FileDataKey, FileSummary>();
            foreach (var kvp in polishedPerFile)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var values = kvp.Value;
                if (values.Count == 0)
                {
                    continue;
                }
                double median = values.Select(p => p.Log2Area).Median();
                summaries.Add(kvp.Key, new FileSummary(median, null));
            }

            if (summaries.Count == 0)
            {
                return EMPTY;
            }

            double medianOfMedians = summaries.Values.Select(s => s.Log2Median).Median();
            var result = new PolishedPeptideAbundances(summaries, polishedByPeptide, peptideCount, medianOfMedians);

            // Build LOWESS curves on a shared RT grid.
            double rtMin = double.MaxValue;
            double rtMax = double.MinValue;
            foreach (var points in polishedPerFile.Values)
            {
                foreach (var pt in points)
                {
                    if (double.IsNaN(pt.RetentionTime))
                    {
                        continue;
                    }
                    if (pt.RetentionTime < rtMin) rtMin = pt.RetentionTime;
                    if (pt.RetentionTime > rtMax) rtMax = pt.RetentionTime;
                }
            }
            if (rtMax <= rtMin)
            {
                return result;
            }

            var rtGrid = new double[RT_GRID_POINTS];
            for (int i = 0; i < RT_GRID_POINTS; i++)
            {
                rtGrid[i] = rtMin + (rtMax - rtMin) * i / (RT_GRID_POINTS - 1);
            }

            var loessByKey = new Dictionary<FileDataKey, double[]>();
            // ParallelEx.ForEach requires a reference-type element, so iterate the keys
            // (FileDataKey is a class) and look up the points inside.
            ParallelEx.ForEach(polishedPerFile.Keys, key =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fitted = FitLoess(polishedPerFile[key], rtGrid, cancellationToken);
                if (fitted != null)
                {
                    lock (loessByKey)
                    {
                        loessByKey[key] = fitted;
                    }
                }
            });
            cancellationToken.ThrowIfCancellationRequested();
            if (loessByKey.Count == 0)
            {
                return result;
            }

            var globalFitted = new double[RT_GRID_POINTS];
            var perPointValues = new double[loessByKey.Count];
            for (int i = 0; i < RT_GRID_POINTS; i++)
            {
                int j = 0;
                foreach (var fittedValues in loessByKey.Values)
                {
                    perPointValues[j++] = fittedValues[i];
                }
                globalFitted[i] = perPointValues.Median();
            }

            // Stitch LOESS results back into the summaries.
            var summariesWithLoess = new Dictionary<FileDataKey, FileSummary>(summaries.Count);
            foreach (var kvp in summaries)
            {
                loessByKey.TryGetValue(kvp.Key, out var fittedForKey);
                summariesWithLoess.Add(kvp.Key, new FileSummary(kvp.Value.Log2Median, fittedForKey));
            }
            var resultWithLoess = new PolishedPeptideAbundances(summariesWithLoess, polishedByPeptide, peptideCount, medianOfMedians)
            {
                _loessRtGrid = rtGrid,
                _globalLoessFitted = globalFitted,
            };
            return resultWithLoess;
        }

        private static double[] FitLoess(IList<RtAreaPoint> points, double[] rtGrid, CancellationToken cancellationToken)
        {
            // Need at least a handful of points for a meaningful fit; mirror RtLoessCurves.
            var weighted = new List<WeightedPoint>(points.Count);
            foreach (var pt in points)
            {
                if (!double.IsNaN(pt.RetentionTime))
                {
                    weighted.Add(new WeightedPoint(pt.RetentionTime, pt.Log2Area));
                }
            }
            if (weighted.Count < 3)
            {
                return null;
            }
            // Downsample dense point clouds (e.g. hundreds of thousands of peptides)
            // before fitting; LoessInterpolator.Smooth is roughly quadratic in input
            // count, so passing the full set unbounded would dominate runtime.
            weighted = AlignmentTarget.DownsamplePoints(weighted, AlignmentTarget.DEFAULT_LOESS_BIN_COUNT).ToList();
            weighted.Sort((a, b) => a.X.CompareTo(b.X));
            if (weighted.Count < 3)
            {
                return null;
            }
            try
            {
                var loess = new LoessInterpolator(
                    Math.Max(LOESS_BANDWIDTH, 2.0 / weighted.Count),
                    LoessInterpolator.DEFAULT_ROBUSTNESS_ITERS);
                var xArray = weighted.Select(p => p.X).ToArray();
                var yArray = weighted.Select(p => p.Y).ToArray();
                var weights = weighted.Select(p => p.Weight).ToArray();
                var smoothed = loess.Smooth(xArray, yArray, weights, cancellationToken);
                var gridValues = new double[rtGrid.Length];
                for (int i = 0; i < rtGrid.Length; i++)
                {
                    gridValues[i] = LoessInterpolator.Interpolate(rtGrid[i], xArray, smoothed);
                }
                return gridValues;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static double GetPeptideMeanRt(PeptideDocNode peptide, int replicateIndex)
        {
            // Use the peptide's measured RT in this replicate, averaged across precursors.
            // Falls back to NaN if no RT is available; caller skips NaN points when fitting LOESS.
            double sum = 0;
            int count = 0;
            foreach (var transitionGroup in peptide.TransitionGroups)
            {
                if (transitionGroup.Results == null || replicateIndex >= transitionGroup.Results.Count)
                {
                    continue;
                }
                var chromInfos = transitionGroup.Results[replicateIndex];
                if (chromInfos.IsEmpty)
                {
                    continue;
                }
                foreach (var chromInfo in chromInfos)
                {
                    if (chromInfo.OptimizationStep != 0)
                    {
                        continue;
                    }
                    if (chromInfo.RetentionTime.HasValue)
                    {
                        sum += chromInfo.RetentionTime.Value;
                        count++;
                    }
                }
            }
            return count > 0 ? sum / count : double.NaN;
        }

        private static IEnumerable<ChromFileInfoId> GetReplicateFileIds(SrmSettings settings, int replicateIndex)
        {
            if (settings.MeasuredResults == null || replicateIndex < 0
                || replicateIndex >= settings.MeasuredResults.Chromatograms.Count)
            {
                yield break;
            }
            foreach (var fileInfo in settings.MeasuredResults.Chromatograms[replicateIndex].MSDataFileInfos)
            {
                yield return fileInfo.FileId;
            }
        }

        private readonly struct FileSummary
        {
            public FileSummary(double log2Median, double[] loessFitted)
            {
                Log2Median = log2Median;
                LoessFitted = loessFitted;
            }
            public double Log2Median { get; }
            public double[] LoessFitted { get; }
        }

        private readonly struct RtAreaPoint
        {
            public RtAreaPoint(double retentionTime, double log2Area)
            {
                RetentionTime = retentionTime;
                Log2Area = log2Area;
            }
            public double RetentionTime { get; }
            public double Log2Area { get; }
        }

        private class FileDataKey
        {
            public FileDataKey(int replicateIndex, ChromFileInfoId chromFileInfoId)
            {
                ReplicateIndex = replicateIndex;
                ChromFileInfoId = chromFileInfoId;
            }
            public int ReplicateIndex { get; }
            public ChromFileInfoId ChromFileInfoId { get; }

            protected bool Equals(FileDataKey other)
            {
                return ReplicateIndex == other.ReplicateIndex
                       && ReferenceEquals(ChromFileInfoId, other.ChromFileInfoId);
            }
            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((FileDataKey)obj);
            }
            public override int GetHashCode()
            {
                unchecked
                {
                    return (ReplicateIndex * 397) ^ RuntimeHelpers.GetHashCode(ChromFileInfoId);
                }
            }
        }
    }
}
