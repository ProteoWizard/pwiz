/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.GroupComparison
{
    /// <summary>
    /// Stores the intensities of all of the transitions in a document and
    /// is able to answer the question "what is the median intensity for a particular
    /// data file."
    /// </summary>
    public class NormalizationData
    {
        public static readonly NormalizationData EMPTY = new NormalizationData(new Dictionary<FileDataKey, FileDataValue>());
        private readonly IDictionary<FileDataKey, FileDataValue> _data;
        private readonly double _medianMedians;
        private readonly IDictionary<FileDataKey, LoessCurve> _rtLoessCurves;
        private readonly LoessCurve _globalMedianCurve;

        private NormalizationData(IDictionary<FileDataKey, FileDataValue> data)
            : this(data, null, default)
        {
        }

        private NormalizationData(IDictionary<FileDataKey, FileDataValue> data,
            IDictionary<FileDataKey, LoessCurve> rtLoessCurves,
            LoessCurve globalMedianCurve)
        {
            _data = data;
            _rtLoessCurves = rtLoessCurves;
            _globalMedianCurve = globalMedianCurve;
            if (data.Count > 0)
            {
                _medianMedians = data.Values.Select(value => value.Median).Median();
            }
        }

        public static NormalizationData GetNormalizationData(SrmDocument document, bool treatMissingValuesAsZero,
            double? qValueCutoff)
        {
            return GetNormalizationData(CancellationToken.None, new Parameters(document, treatMissingValuesAsZero, qValueCutoff));
        }

        public static NormalizationData GetNormalizationData(CancellationToken cancellationToken, Parameters parameters)
        {
            var document = parameters.Document;
            if (!document.Settings.HasResults)
            {
                return EMPTY;
            }

            var internalStandardLabelTypes =
                document.Settings.PeptideSettings.Modifications.InternalStandardTypes.ToHashSet();
            var endogenousAreas = new Dictionary<FileDataKey, List<double>>();
            var internalStandardAreas = new Dictionary<FileDataKey, List<double>>();

            ParallelEx.ForEach(document.Molecules, peptide =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                if (peptide.IsDecoy)
                {
                    return;
                }
                if (PeptideDocNode.STANDARD_TYPE_IRT == peptide.GlobalStandardType)
                {
                    // Skip all iRT standards because they are excluded in MSstatsGC.R
                    return;
                }

                foreach (var transitionGroup in peptide.TransitionGroups)
                {
                    bool isInternalStandard = internalStandardLabelTypes.Contains(transitionGroup.LabelType);
                    foreach (var dataFileAreas in GetAreasFromTransitionGroup(parameters, transitionGroup)
                                 .GroupBy(tuple => tuple.Item1, tuple => tuple.Item2))
                    {
                        var dictionary = isInternalStandard ? internalStandardAreas : endogenousAreas;
                        lock (dictionary)
                        {
                            if (!dictionary.TryGetValue(dataFileAreas.Key, out var list))
                            {
                                list = new List<double>();
                                dictionary.Add(dataFileAreas.Key, list);
                            }
                            list.AddRange(dataFileAreas);
                        }
                    }
                }
            });
            var allFileData = new Dictionary<FileDataKey, FileDataValue>();
            ParallelEx.ForEach(endogenousAreas.Keys.Concat(internalStandardAreas.Keys).Distinct(), dataKey =>
            {
                internalStandardAreas.TryGetValue(dataKey, out var fileAreas);
                if (!(fileAreas?.Count > 0))
                {
                    endogenousAreas.TryGetValue(dataKey, out fileAreas);
                }

                if (fileAreas?.Count > 0)
                {
                    var log2Areas = fileAreas.Select(area => Math.Log(Math.Max(area, 1), 2.0));
                    var fileData = new FileDataValue(log2Areas);
                    lock (allFileData)
                    {
                        allFileData.Add(dataKey, fileData);
                    }
                }
            });

            // Compute RT LOESS normalization curves
            var rtAreaData = CollectRtAreaData(cancellationToken, parameters);
            ComputeRtLoessCurves(cancellationToken, rtAreaData, out var rtLoessCurves, out var globalMedianCurve);

            return new NormalizationData(allFileData, rtLoessCurves, globalMedianCurve);
        }

        public double? NormalizeQuantile(int replicateIndex, ChromFileInfoId chromFileInfoId, double value)
        {
            FileDataValue dataValue;
            if (!_data.TryGetValue(new FileDataKey(replicateIndex, chromFileInfoId), out dataValue))
            {
                return null;
            }
            double percentile = dataValue.FindPercentileOfValue(value);
            return GetMeanPercentile(percentile).Value;
        }

        public double? Percentile(int replicateIndex, ChromFileInfoId chromFileInfoId, double percentile)
        {
            var dataKey = new FileDataKey(replicateIndex, chromFileInfoId);
            FileDataValue dataValue;
            if (!_data.TryGetValue(dataKey, out dataValue))
            {
                return null;
            }
            return dataValue.GetValueAtPercentile(percentile);
        }

        public double? GetLog2Median(int replicateIndex, ChromFileInfoId chromFileInfoId)
        {
            var dataKey = new FileDataKey(replicateIndex, chromFileInfoId);
            FileDataValue dataValue;
            if (!_data.TryGetValue(dataKey, out dataValue))
            {
                return null;
            }
            return dataValue.Median;
        }

        /// <summary>
        /// Returns the median of the Log base 2 of peak areas of all of the replicates.
        /// </summary>
        public double GetMedianLog2Median()
        {
            return _medianMedians;
        }

        public double? GetMeanPercentile(double percentile)
        {
            var values = new List<double>();
            foreach (var entry in _data)
            {
                values.Add(entry.Value.GetValueAtPercentile(percentile));
            }
            if (values.Count == 0)
            {
                return null;
            }
            return values.Mean();
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

        private readonly struct FileDataValue
        {
            internal readonly double[] _sortedIntensities;

            public FileDataValue(IEnumerable<double> intensities) : this()
            {
                _sortedIntensities = intensities.ToArray();
                Array.Sort(_sortedIntensities);
                Median = GetValueAtPercentile(0.5);
            }

            public double Median
            {
                get;
            }

            public double GetValueAtPercentile(double percentile)
            {
                return ValueAtPercentileInSortedList(_sortedIntensities, percentile);
            }

            public double FindPercentileOfValue(double value)
            {
                return PercentileOfValueInSortedList(_sortedIntensities, value);
            }
        }

        public static double ValueAtPercentileInSortedList(IList<double> sortedValues, double percentile)
        {
            double index = percentile * (sortedValues.Count - 1);
            if (index <= 0)
            {
                return sortedValues[0];
            }
            if (index >= sortedValues.Count - 1)
            {
                return sortedValues[sortedValues.Count - 1];
            }
            int lbound = (int)index;
            return sortedValues[lbound] * (1 - index + lbound) + sortedValues[lbound + 1] * (index - lbound);
        }

        public static double PercentileOfValueInSortedList(IList<double> sortedValues, double value)
        {
            int index = CollectionUtil.BinarySearch(sortedValues, value);
            if (index >= 0)
            {
                return ((double)index) / (sortedValues.Count - 1);
            }
            index = ~index;
            if (index <= 0)
            {
                return 0;
            }
            if (index >= sortedValues.Count)
            {
                return 1;
            }
            double fraction = (value - sortedValues[index - 1]) /
                              (sortedValues[index] - sortedValues[index - 1]);
            return (index - 1 + fraction) / (sortedValues.Count - 1);
        }

        /// <summary>
        /// Returns the log2 adjustment for RT LOESS normalization at a given retention time.
        /// The adjustment is: sample_curve(RT) - global_median_curve(RT).
        /// To normalize, divide the area by 2^adjustment.
        /// </summary>
        public double? GetRtLoessAdjustment(int replicateIndex, ChromFileInfoId chromFileInfoId, double retentionTime)
        {
            if (_rtLoessCurves == null || _globalMedianCurve.RtGrid == null)
            {
                return null;
            }

            var dataKey = new FileDataKey(replicateIndex, chromFileInfoId);
            if (!_rtLoessCurves.TryGetValue(dataKey, out var sampleCurve))
            {
                return null;
            }

            double sampleValue = LoessInterpolator.Interpolate(retentionTime, sampleCurve.RtGrid, sampleCurve.FittedValues);
            double globalValue = LoessInterpolator.Interpolate(retentionTime, _globalMedianCurve.RtGrid, _globalMedianCurve.FittedValues);
            return sampleValue - globalValue;
        }

        public bool HasRtLoessCurves => _rtLoessCurves != null && _rtLoessCurves.Count > 0;

        public IEnumerable<RtLoessCurveInfo> GetRtLoessCurves()
        {
            if (_rtLoessCurves == null)
                yield break;
            foreach (var kvp in _rtLoessCurves)
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

        public static Lazy<NormalizationData> LazyNormalizationData(SrmDocument document)
        {
            return new Lazy<NormalizationData>(()=> GetNormalizationData(document, false, null));
        }

        public static readonly Producer<Parameters, NormalizationData> PRODUCER =
            Producer.FromFunction<Parameters, NormalizationData>((progressCallback, parameters) =>
                GetNormalizationData(progressCallback.CancellationToken, parameters));
        /// <summary>
        /// For the MS2 transitions, returns all the Area values for each of the Transitions for each of the replicates.
        /// For the MS1 transitions, returns the sum of the MS1 Area values for each of the replicates.
        /// </summary>
        private static IEnumerable<Tuple<FileDataKey, double>> GetAreasFromTransitionGroup(Parameters parameters, TransitionGroupDocNode transitionGroup)
        {
            var transitionsByMsLevel = transitionGroup.Transitions.Where(transition => null != transition.Results)
                .GroupBy(transition => transition.IsMs1);
            return transitionsByMsLevel.SelectMany(msLevelGroup =>
            {
                bool areMs1 = msLevelGroup.Key;
                var areaValues = msLevelGroup.SelectMany(transition => GetAreasFromTransition(parameters, transitionGroup, transition));
                if (areMs1)
                {
                    // The MS1 transition peak areas should be summed together before returning.
                    return areaValues.GroupBy(areaValue => areaValue.Item1, areaValue => areaValue.Item2)
                        .Select(group => Tuple.Create(group.Key, group.Sum()));
                }
                else
                {
                    return areaValues;
                }
            });
        }

        private static IEnumerable<Tuple<FileDataKey, double>> GetAreasFromTransition(Parameters parameters, TransitionGroupDocNode transitionGroup, TransitionDocNode transition)
        {
            for (int iResult = 0; iResult < transition.Results.Count; iResult++)
            {
                foreach (var chromInfo in transition.Results[iResult])
                {
                    if (chromInfo.OptimizationStep != 0)
                    {
                        continue;
                    }
                    double? area = GetTransitionArea(parameters, transitionGroup, transition, iResult, chromInfo);
                    if (area.HasValue)
                    {
                        yield return Tuple.Create(new FileDataKey(iResult, chromInfo.FileId), area.Value);
                    }
                }
            }
        }

        private static double? GetTransitionArea(Parameters parameters, TransitionGroupDocNode transitionGroup,
            TransitionDocNode transition, int replicateIndex, TransitionChromInfo chromInfo)
        {
            return PeptideQuantifier.GetArea(parameters.TreatMissingValuesAsZero, parameters.QValueCutoff,
                false, transitionGroup, transition, replicateIndex, chromInfo);
        }

        private static Dictionary<FileDataKey, List<RtAreaPoint>> CollectRtAreaData(
            CancellationToken cancellationToken, Parameters parameters)
        {
            var document = parameters.Document;
            var rtAreaData = new Dictionary<FileDataKey, List<RtAreaPoint>>();
            if (!document.Settings.HasResults)
            {
                return rtAreaData;
            }

            ParallelEx.ForEach(document.Molecules, peptide =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                if (peptide.IsDecoy)
                    return;
                if (PeptideDocNode.STANDARD_TYPE_IRT == peptide.GlobalStandardType)
                    return;

                foreach (var transitionGroup in peptide.TransitionGroups)
                {
                    if (transitionGroup.Results == null)
                        continue;
                    for (int iResult = 0; iResult < transitionGroup.Results.Count; iResult++)
                    {
                        if (transitionGroup.Results[iResult].IsEmpty)
                            continue;
                        foreach (var groupChromInfo in transitionGroup.Results[iResult])
                        {
                            if (groupChromInfo.OptimizationStep != 0)
                                continue;
                            if (!groupChromInfo.RetentionTime.HasValue || !groupChromInfo.Area.HasValue)
                                continue;
                            if (groupChromInfo.Area.Value <= 0)
                                continue;

                            var dataKey = new FileDataKey(iResult, groupChromInfo.FileId);
                            var point = new RtAreaPoint(groupChromInfo.RetentionTime.Value,
                                Math.Log(groupChromInfo.Area.Value, 2.0));
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
            });
            return rtAreaData;
        }

        private const int RT_GRID_POINTS = 100;
        private const double LOESS_BANDWIDTH = 0.3;

        private static void ComputeRtLoessCurves(CancellationToken cancellationToken,
            Dictionary<FileDataKey, List<RtAreaPoint>> rtAreaData,
            out Dictionary<FileDataKey, LoessCurve> rtLoessCurves,
            out LoessCurve globalMedianCurve)
        {
            rtLoessCurves = new Dictionary<FileDataKey, LoessCurve>();
            globalMedianCurve = default;

            if (rtAreaData.Count == 0)
                return;

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
                return;

            // Create common RT grid
            var rtGrid = new double[RT_GRID_POINTS];
            for (int i = 0; i < RT_GRID_POINTS; i++)
            {
                rtGrid[i] = rtMin + (rtMax - rtMin) * i / (RT_GRID_POINTS - 1);
            }

            // Fit LOWESS per file and evaluate on grid
            int binCount = Settings.Default.RtRegressionBinCount;
            var allCurves = new Dictionary<FileDataKey, LoessCurve>();

            foreach (var kvp in rtAreaData)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var points = kvp.Value;
                if (points.Count < 20)
                    continue;

                // Downsample and sort by RT
                var weightedPoints = points.Select(p => new WeightedPoint(p.RetentionTime, p.Log2Area)).ToList();
                if (binCount > 0)
                {
                    weightedPoints = AlignmentTarget.DownsamplePoints(weightedPoints, binCount).ToList();
                }
                weightedPoints = weightedPoints.OrderBy(pt => pt.X).ToList();
                if (weightedPoints.Count < 3)
                    continue;

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

                    allCurves[kvp.Key] = new LoessCurve(rtGrid, gridValues);
                }
                catch (Exception)
                {
                    // Skip files where LOWESS fitting fails
                }
            }

            if (allCurves.Count == 0)
                return;

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

            rtLoessCurves = allCurves;
            globalMedianCurve = new LoessCurve(rtGrid, medianValues);
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

        public class Parameters
        {
            public Parameters(SrmDocument document) : this(document, false, null)
            {
            }
            public Parameters(SrmDocument document, bool treatMissingAsZero, double? qValueCutoff)
            {
                Document = document;
                TreatMissingValuesAsZero = treatMissingAsZero;
                QValueCutoff = qValueCutoff;
            }

            public bool TreatMissingValuesAsZero { get; }
            public double? QValueCutoff { get; }
            public SrmDocument Document { get; }

            protected bool Equals(Parameters other)
            {
                return TreatMissingValuesAsZero == other.TreatMissingValuesAsZero &&
                       Nullable.Equals(QValueCutoff, other.QValueCutoff) && ReferenceEquals(Document, other.Document);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Parameters)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = TreatMissingValuesAsZero.GetHashCode();
                    hashCode = (hashCode * 397) ^ QValueCutoff.GetHashCode();
                    hashCode = (hashCode * 397) ^ RuntimeHelpers.GetHashCode(Document);
                    return hashCode;
                }
            }
        }
    }
}
