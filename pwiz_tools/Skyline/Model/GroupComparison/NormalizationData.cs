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
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.Results;

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

        private NormalizationData(IDictionary<FileDataKey, FileDataValue> data)
        {
            _data = data;
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
            return new NormalizationData(allFileData);
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
