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
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
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
        public static readonly NormalizationData EMPTY = new NormalizationData(new Dictionary<DataKey, DataValue>());
        private readonly IDictionary<DataKey, DataValue> _data;
        private readonly IDictionary<IsotopeLabelType, double> _medianMedians;

        private NormalizationData(IDictionary<DataKey, DataValue> data)
        {
            _data = data;
            _medianMedians = data.GroupBy(keyValuePair => keyValuePair.Key.IsotopeLabelType, kvp => kvp.Value)
                .ToDictionary(grouping => grouping.Key,
                    grouping => grouping.Select(dataValue => dataValue.Median).Median());
        }

        public static NormalizationData GetNormalizationData(SrmDocument document, bool treatMissingValuesAsZero, double? qValueCutoff)
        {
            if (!document.Settings.HasResults)
            {
                return EMPTY;
            }
            var chromatogramSets = document.Settings.MeasuredResults.Chromatograms;
            var areaAccumulators = Enumerable.Range(0, chromatogramSets.Count).Select(replicateIndex =>
                new AreaAccumulator(replicateIndex, chromatogramSets[replicateIndex])).ToList();

            foreach (var peptideGroup in document.MoleculeGroups)
            {
                foreach (var peptide in peptideGroup.Molecules)
                {
                    if (peptide.IsDecoy)
                    {
                        continue;
                    }
                    if (PeptideDocNode.STANDARD_TYPE_IRT == peptide.GlobalStandardType)
                    {
                        // Skip all iRT standards because they are excluded in MSstatsGC.R
                        continue;
                    }
                    foreach (var transitionGroup in peptide.TransitionGroups)
                    {
                        foreach (var transition in transitionGroup.Transitions)
                        {
                            if (!transition.HasResults)
                            {
                                continue;
                            }

                            bool isMs1 = transition.IsMs1;
                            for (int iResult = 0; iResult < transition.Results.Count && iResult < chromatogramSets.Count; iResult++)
                            {
                                var results = transition.Results[iResult];
                                foreach (var chromInfo in results)
                                {
                                    if (chromInfo.OptimizationStep != 0)
                                    {
                                        continue;
                                    }
                                    double? area = PeptideQuantifier.GetArea(treatMissingValuesAsZero, qValueCutoff,
                                        false, transitionGroup, transition, iResult, chromInfo);
                                    if (!area.HasValue)
                                    {
                                        continue;
                                    }

                                    areaAccumulators[iResult].AddArea(chromInfo.FileId, transitionGroup.LabelType,
                                        isMs1, area.Value);
                                }
                            }
                        }

                        foreach (var areaAccumulator in areaAccumulators)
                        {
                            areaAccumulator.FinishTransitionGroup();
                        }
                    }
                }
            }
            var data = new Dictionary<DataKey, DataValue>();
            foreach (var areaAccumulator in areaAccumulators)
            {
                foreach (var entry in areaAccumulator.GetAreaLists())
                {
                    var log2Areas = entry.Value.Select(area => Math.Log(Math.Max(area, 1), 2.0));
                    data.Add(entry.Key, new DataValue(log2Areas));
                }
            }
            return new NormalizationData(data);
        }

        public double? NormalizeQuantile(int replicateIndex, ChromFileInfoId chromFileInfoId, IsotopeLabelType isotopeLabelType,
            double value)
        {
            DataValue dataValue;
            if (!_data.TryGetValue(new DataKey(replicateIndex, chromFileInfoId, null, isotopeLabelType), out dataValue))
            {
                return null;
            }
            double percentile = dataValue.FindPercentileOfValue(value);
            return GetMeanPercentile(isotopeLabelType, percentile).Value;
        }

        public double? Percentile(int replicateIndex, ChromFileInfoId chromFileInfoId, IsotopeLabelType isotopeLabelType, double percentile)
        {
            var dataKey = new DataKey(replicateIndex, chromFileInfoId, null, isotopeLabelType);
            DataValue dataValue;
            if (!_data.TryGetValue(dataKey, out dataValue))
            {
                return null;
            }
            return dataValue.GetValueAtPercentile(percentile);
        }

        public double? GetMedian(int replicateIndex, ChromFileInfoId chromFileInfoId, IsotopeLabelType isotopeLabelType)
        {
            var dataKey = new DataKey(replicateIndex, chromFileInfoId, null, isotopeLabelType);
            DataValue dataValue;
            if (!_data.TryGetValue(dataKey, out dataValue))
            {
                return null;
            }
            return dataValue.Median;
        }

        public double? GetMedianMedian(IsotopeLabelType isotopeLabelType)
        {
            double medianMedian;
            if (_medianMedians.TryGetValue(isotopeLabelType, out medianMedian))
            {
                return medianMedian;
            }
            return null;
        }

        public double? GetMeanPercentile(IsotopeLabelType isotopeLabelType, double percentile)
        {
            var values = new List<double>();
            foreach (var entry in _data)
            {
                if (!Equals(entry.Key.IsotopeLabelType, isotopeLabelType))
                {
                    continue;
                }
                values.Add(entry.Value.GetValueAtPercentile(percentile));
            }
            if (values.Count == 0)
            {
                return null;
            }
            return values.Mean();
        }

        private struct DataKey
        {
            public DataKey(int replicateIndex, ChromFileInfoId chromFileInfoId, SampleType sampleType, IsotopeLabelType isotopeLabelType)
                : this()
            {
                ReplicateIndex = replicateIndex;
                ChromFileInfoId = chromFileInfoId;
                SampleType = sampleType;
                IsotopeLabelType = isotopeLabelType;
            }
            public int ReplicateIndex { get; private set; }
            public ChromFileInfoId ChromFileInfoId { get; private set; }
            public SampleType SampleType { get; private set; }
            public IsotopeLabelType IsotopeLabelType { get; private set; }

            public bool Equals(DataKey other)
            {
                return ReplicateIndex == other.ReplicateIndex && ReferenceEquals(ChromFileInfoId, other.ChromFileInfoId) && Equals(IsotopeLabelType, other.IsotopeLabelType);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is DataKey && Equals((DataKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int result = ReplicateIndex;
                    result = (result * 397) ^ (ChromFileInfoId != null ? RuntimeHelpers.GetHashCode(ChromFileInfoId) : 0);
                    result = (result * 397) ^ IsotopeLabelType.GetHashCode();
                    return result;
                }
            }
        }

        private struct DataValue
        {
            internal readonly double[] _sortedIntensities;

            public DataValue(IEnumerable<double> intensities) : this()
            {
                _sortedIntensities = intensities.ToArray();
                Array.Sort(_sortedIntensities);
                Median = GetValueAtPercentile(0.5);
            }

            public double Median
            {
                get; private set;
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
        /// Keeps track of a list of transition peak areas for a single replicate.
        /// </summary>
        private class AreaAccumulator
        {
            private IDictionary<DataKey, List<double>> _areasDictionary = new Dictionary<DataKey, List<double>>();
            private Dictionary<DataKey, double> _ms1AreasDictionary = new Dictionary<DataKey, double>();

            private ChromFileInfoId _firstFileId;
            private List<double> _firstFileLightAreasList = new List<double>();
            private double? _firstFileLightMs1Area;
            public AreaAccumulator(int replicateIndex, ChromatogramSet chromatogramSet)
            {
                ReplicateIndex = replicateIndex;
                ChromatogramSet = chromatogramSet;
                _firstFileId = chromatogramSet.MSDataFileInfos.First().FileId;
            }
            public int ReplicateIndex { get; private set; }

            public ChromatogramSet ChromatogramSet { get; private set; }

            public void AddArea(ChromFileInfoId fileId, IsotopeLabelType labelType, bool isMs1, double area)
            {
                // Avoid a dictionary lookup for the most common case where label type is light, and the fileId is the first one in the replicate
                if (labelType.IsLight && ReferenceEquals(fileId, _firstFileId))
                {
                    if (isMs1)
                    {
                        _firstFileLightMs1Area = (_firstFileLightMs1Area ?? 0) + area;
                    }
                    else
                    {
                        _firstFileLightAreasList.Add(area);
                    }

                    return;
                }

                var key = MakeDataKey(fileId, labelType);
                if (isMs1)
                {
                    _ms1AreasDictionary.TryGetValue(key, out double ms1Area);
                    ms1Area += area;
                    _ms1AreasDictionary[key] = ms1Area;
                }
                else
                {
                    AddArea(key, area);
                }
            }

            private DataKey MakeDataKey(ChromFileInfoId fileId, IsotopeLabelType labelType)
            {
                return new DataKey(ReplicateIndex, fileId, ChromatogramSet.SampleType, labelType);
            }

            private void AddArea(DataKey key, double area)
            {
                if (!_areasDictionary.TryGetValue(key, out var areasList))
                {
                    areasList = new List<double>();
                    _areasDictionary.Add(key, areasList);
                }
                areasList.Add(area);

            }

            /// <summary>
            /// After processing all of the transitions in a transition group, the observed MS1 areas should be summed,
            /// and then added to the list of areas.
            /// </summary>
            public void FinishTransitionGroup()
            {
                if (_firstFileLightMs1Area.HasValue)
                {
                    _firstFileLightAreasList.Add(_firstFileLightMs1Area.Value);
                    _firstFileLightMs1Area = null;
                }

                foreach (var entry in _ms1AreasDictionary)
                {
                    AddArea(entry.Key, entry.Value);
                }
                _ms1AreasDictionary.Clear();
            }

            public IEnumerable<KeyValuePair<DataKey, List<double>>> GetAreaLists()
            {
                var result = _areasDictionary.AsEnumerable();
                if (_firstFileLightAreasList.Any())
                {
                    result = result.Prepend(new KeyValuePair<DataKey, List<double>>(MakeDataKey(_firstFileId, IsotopeLabelType.light), _firstFileLightAreasList));
                }

                return result;
            }
        }
    }

}
