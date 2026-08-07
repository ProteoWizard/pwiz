/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.GroupComparison
{
    /// <summary>
    /// Holds the RT LOESS curves used by RT LOESS normalization.
    /// One curve is fit per data file plus a global median curve across all files.
    /// </summary>
    public class RtLoessCurves
    {
        public static readonly RtLoessCurves EMPTY =
            new RtLoessCurves(new Dictionary<FileDataKey, LoessCurve>(), default);

        private const int RT_GRID_POINTS = 100;
        private const double LOESS_BANDWIDTH = 0.3;

        private readonly IDictionary<FileDataKey, LoessCurve> _curves;
        private readonly LoessCurve _globalMedianCurve;

        private RtLoessCurves(IDictionary<FileDataKey, LoessCurve> curves, LoessCurve globalMedianCurve)
        {
            _curves = curves;
            _globalMedianCurve = globalMedianCurve;
        }

        public bool HasCurves => _curves.Count > 0;

        public IEnumerable<RtLoessCurveInfo> GetCurves()
        {
            foreach (var kvp in _curves)
            {
                yield return new RtLoessCurveInfo(kvp.Key.ReplicateIndex, kvp.Key.ChromFileInfoId,
                    kvp.Value.RtGrid, kvp.Value.FittedValues);
            }
        }

        public double[] GetGlobalMedianRtGrid()
        {
            return _globalMedianCurve.RtGrid;
        }

        public double[] GetGlobalMedianFittedValues()
        {
            return _globalMedianCurve.FittedValues;
        }

        /// <summary>
        /// Returns the log2 adjustment for RT LOESS normalization at a given retention time.
        /// The adjustment is: sample_curve(RT) - global_median_curve(RT).
        /// To normalize, divide the area by 2^adjustment.
        /// </summary>
        public double? GetAdjustment(int replicateIndex, ChromFileInfoId chromFileInfoId, double retentionTime)
        {
            if (_globalMedianCurve.RtGrid == null)
            {
                return null;
            }

            var dataKey = new FileDataKey(replicateIndex, chromFileInfoId);
            if (!_curves.TryGetValue(dataKey, out var sampleCurve))
            {
                return null;
            }

            double sampleValue = LoessInterpolator.Interpolate(retentionTime, sampleCurve.RtGrid, sampleCurve.FittedValues);
            double globalValue = LoessInterpolator.Interpolate(retentionTime, _globalMedianCurve.RtGrid, _globalMedianCurve.FittedValues);
            return sampleValue - globalValue;
        }

        public static readonly Producer<ReferenceValue<SrmDocument>, RtLoessCurves> PRODUCER =
            Producer.FromFunction<ReferenceValue<SrmDocument>, RtLoessCurves>((productionMonitor, doc) =>
                ProduceRtLoessCurves(productionMonitor, doc.Value));

        public static RtLoessCurves ProduceRtLoessCurves(ProductionMonitor productionMonitor, SrmDocument document)
        {
            if (!document.Settings.HasResults)
            {
                return EMPTY;
            }

            productionMonitor.SetProgress(0);
            var rtAreaData = CollectRtAreaData(productionMonitor, document);
            return ComputeCurves(productionMonitor, rtAreaData);
        }

        public static RtLoessCurves GetRtLoessCurves(CancellationToken cancellationToken, SrmDocument document)
        {
            return ProduceRtLoessCurves(new ProductionMonitor(cancellationToken, _ => { }), document);
        }


        private static Dictionary<FileDataKey, List<RtAreaPoint>> CollectRtAreaData(
            ProductionMonitor productionMonitor, SrmDocument document)
        {
            var cancellationToken = productionMonitor.CancellationToken;
            var rtAreaData = new Dictionary<FileDataKey, List<RtAreaPoint>>();
            // Roll transitions up to one log2 abundance per peptide per file using
            // PeptideQuantifier, so the RT LOESS curves honor the document's quantification
            // settings - in particular QuantificationSettings.MsLevel and each transition's
            // IsQuantitative flag. (Previously this read TransitionGroupChromInfo.Area, the
            // raw precursor total, which includes MS1 precursor and non-quantitative
            // transitions regardless of MsLevel.) This mirrors skyline-prism, which sums only
            // the quantitative MS-level transitions into a single per-peptide abundance before
            // fitting the curves.
            var noneNormalization = new NormalizedValueCalculator(cancellationToken, document);
            // AVERAGING ignores this set (it summarizes every replicate); it is required by the
            // shared GetPeptideLog2Abundances signature and matches PolishedPeptideAbundances.
            var replicateIndexes = PeptideQuantifier.GetMedianPolishReplicates(document.Settings);

            var moleculeGroupMolecules = document.MoleculeGroups
                .SelectMany(mg => mg.Molecules
                    .Where(mol => !mol.IsDecoy &&
                                  PeptideDocNode.STANDARD_TYPE_IRT != mol.GlobalStandardType)
                    .Select(mol => (mg, mol)))
                .ToList();
            int molCount = Math.Max(1, moleculeGroupMolecules.Count);
            int processed = 0;

            ParallelEx.For(0, moleculeGroupMolecules.Count, i =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                try
                {
                    var (peptideGroup, peptide) = moleculeGroupMolecules[i];
                    var quantifier = PeptideQuantifier.GetPeptideQuantifier(noneNormalization,
                        document.Settings, peptideGroup.PeptideGroup, peptide);
                    // Impute missing/zero transitions so the per-file abundances match the
                    // transition-by-sample matrix skyline-prism sums before fitting its curves.
                    quantifier.ImputeMissingValues = true;
                    // AVERAGING = sum of the quantitative transition areas, matching
                    // skyline-prism's default "sum" transition->peptide rollup.
                    var log2Abundances = quantifier.GetPeptideLog2Abundances(document.Settings,
                        replicateIndexes, SummarizationMethod.AVERAGING, NormalizationMethod.NONE);
                    if (log2Abundances == null)
                        return;

                    // One retention time per peptide (mean over all measured precursor peaks),
                    // used as the x value for every replicate's point.
                    double meanRt = quantifier.GetPeptideMeanRetentionTime(document.Settings);
                    if (double.IsNaN(meanRt))
                        return;

                    for (int iReplicate = 0; iReplicate < log2Abundances.Length; iReplicate++)
                    {
                        var value = log2Abundances[iReplicate];
                        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                            continue;

                        foreach (var fileId in GetReplicateFileIds(document.Settings, iReplicate))
                        {
                            var dataKey = new FileDataKey(iReplicate, fileId);
                            var point = new RtAreaPoint(meanRt, value.Value);
                            lock (rtAreaData)
                            {
                                if (!rtAreaData.TryGetValue(dataKey, out var list))
                                {
                                    list = new List<RtAreaPoint>();
                                    rtAreaData.Add(dataKey, list);
                                }
                                list.Add(point);
                            }
                        }
                    }
                }
                finally
                {
                    lock (rtAreaData)
                    {
                        processed++;
                        productionMonitor.SetProgress(processed * 50 / molCount);
                    }
                }
            });
            return rtAreaData;
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

        private static RtLoessCurves ComputeCurves(ProductionMonitor productionMonitor,
            Dictionary<FileDataKey, List<RtAreaPoint>> rtAreaData)
        {
            var cancellationToken = productionMonitor.CancellationToken;
            if (rtAreaData.Count == 0)
                return EMPTY;

            // Find global RT range
            double rtMin = double.MaxValue;
            double rtMax = double.MinValue;
            foreach (var points in rtAreaData.Values)
            {
                foreach (var point in points)
                {
                    if (point.RetentionTime < rtMin)
                        rtMin = point.RetentionTime;
                    if (point.RetentionTime > rtMax)
                        rtMax = point.RetentionTime;
                }
            }

            if (rtMax <= rtMin)
                return EMPTY;

            // Create common RT grid
            var rtGrid = new double[RT_GRID_POINTS];
            for (int i = 0; i < RT_GRID_POINTS; i++)
            {
                rtGrid[i] = rtMin + (rtMax - rtMin) * i / (RT_GRID_POINTS - 1);
            }

            // Fit LOWESS per file and evaluate on grid
            int binCount = AlignmentTarget.DEFAULT_LOESS_BIN_COUNT;
            var allCurves = new Dictionary<FileDataKey, LoessCurve>();
            int fileCount = Math.Max(1, rtAreaData.Count);
            int filesProcessed = 0;

            ParallelEx.ForEach(rtAreaData.Keys, key =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                try
                {
                    var points = rtAreaData[key];
                    if (points.Count < 20)
                        return;

                    // Downsample and sort by RT
                    var weightedPoints = points.Select(p => new WeightedPoint(p.RetentionTime, p.Log2Area)).ToList();
                    if (binCount > 0)
                    {
                        weightedPoints = AlignmentTarget.DownsamplePoints(weightedPoints, binCount).ToList();
                    }
                    weightedPoints = weightedPoints.OrderBy(pt => pt.X).ToList();
                    if (weightedPoints.Count < 3)
                        return;

                    try
                    {
                        var loess = new LoessInterpolator(
                            Math.Max(LOESS_BANDWIDTH, 2.0 / weightedPoints.Count),
                            LoessInterpolator.DEFAULT_ROBUSTNESS_ITERS);
                        var xArray = weightedPoints.Select(pt => pt.X).ToArray();
                        var smoothed = loess.Smooth(xArray,
                            weightedPoints.Select(pt => pt.Y).ToArray(),
                            weightedPoints.Select(pt => pt.Weight).ToArray(),
                            cancellationToken);
                        // Interpolate onto grid
                        var gridValues = new double[RT_GRID_POINTS];
                        for (int i = 0; i < RT_GRID_POINTS; i++)
                        {
                            gridValues[i] = LoessInterpolator.Interpolate(rtGrid[i], xArray, smoothed);
                        }

                        var fitted = new LoessCurve(rtGrid, gridValues);
                        lock (allCurves)
                        {
                            allCurves[key] = fitted;
                        }
                    }
                    catch (Exception)
                    {
                        // Skip files where LOWESS fitting fails
                    }
                }
                finally
                {
                    lock (allCurves)
                    {
                        filesProcessed++;
                        productionMonitor.SetProgress(50 + filesProcessed * 50 / fileCount);
                    }
                }
            });
            cancellationToken.ThrowIfCancellationRequested();

            if (allCurves.Count == 0)
                return EMPTY;

            // Compute global median curve
            var medianValues = new double[RT_GRID_POINTS];
            var valuesAtPoint = new double[allCurves.Count];
            for (int i = 0; i < RT_GRID_POINTS; i++)
            {
                int j = 0;
                foreach (var curve in allCurves.Values)
                {
                    valuesAtPoint[j++] = curve.FittedValues[i];
                }
                medianValues[i] = valuesAtPoint.Median();
            }

            productionMonitor.SetProgress(100);
            return new RtLoessCurves(allCurves, new LoessCurve(rtGrid, medianValues));
        }

        public readonly struct RtLoessCurveInfo
        {
            public RtLoessCurveInfo(int replicateIndex, ChromFileInfoId chromFileInfoId,
                double[] rtGrid, double[] fittedValues)
            {
                ReplicateIndex = replicateIndex;
                ChromFileInfoId = chromFileInfoId;
                RtGrid = rtGrid;
                FittedValues = fittedValues;
            }

            public int ReplicateIndex { get; }
            public ChromFileInfoId ChromFileInfoId { get; }
            public double[] RtGrid { get; }
            public double[] FittedValues { get; }
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
                return ReplicateIndex == other.ReplicateIndex && ReferenceEquals(ChromFileInfoId, other.ChromFileInfoId);
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

        private readonly struct LoessCurve
        {
            public LoessCurve(double[] rtGrid, double[] fittedValues)
            {
                RtGrid = rtGrid;
                FittedValues = fittedValues;
            }

            public double[] RtGrid { get; }
            public double[] FittedValues { get; }
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
    }
}
