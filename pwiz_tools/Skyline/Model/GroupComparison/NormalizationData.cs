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
        private readonly IDictionary<Tuple<SampleType, IsotopeLabelType>, double> _medianMedians;

        private NormalizationData(IDictionary<DataKey, DataValue> data)
        {
            _data = data;
            _medianMedians = data.ToLookup(keyValuePair => Tuple.Create(keyValuePair.Key.SampleType, keyValuePair.Key.IsotopeLabelType),
                    kvp => kvp.Value)
                .ToDictionary(grouping => grouping.Key,
                    grouping => grouping.Select(dataValue => dataValue.Median).Median());
        }

        public static NormalizationData GetNormalizationData(SrmDocument document, bool treatMissingValuesAsZero, double? qValueCutoff)
        {
            if (!document.Settings.HasResults)
            {
                return EMPTY;
            }
            var intensitiesByFileAndLabelType = new Dictionary<DataKey, List<double>>();
            var chromatogramSets = document.Settings.MeasuredResults.Chromatograms;
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
                        Dictionary<DataKey, double> ms1Areas = new Dictionary<DataKey, double>();

                        foreach (var transition in transitionGroup.Transitions)
                        {
                            if (!transition.HasResults)
                            {
                                continue;
                            }
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
                                    
                                    var key = new DataKey(chromInfo.FileId, chromatogramSets[iResult].SampleType, transitionGroup.TransitionGroup.LabelType);
                                    if (transition.IsMs1)
                                    {
                                        double totalArea;
                                        ms1Areas.TryGetValue(key, out totalArea);
                                        totalArea += area.Value;
                                        ms1Areas[key] = totalArea;
                                    }
                                    else
                                    {
                                        List<double> fileLabelTypeIntensities;
                                        if (!intensitiesByFileAndLabelType.TryGetValue(key, out fileLabelTypeIntensities))
                                        {
                                            fileLabelTypeIntensities = new List<double>();
                                            intensitiesByFileAndLabelType.Add(key, fileLabelTypeIntensities);
                                        }
                                        // The logarithm of the area is stored instead of the area itself.
                                        // This has a tiny impact on the way the median is calculated if there are 
                                        // an even number of values
                                        fileLabelTypeIntensities.Add(Math.Log(Math.Max(area.Value, 1), 2.0));
                                    }
                                }
                            }
                        }
                        foreach (var entry in ms1Areas)
                        {
                            List<double> fileLabelTypeIntensities;
                            if (!intensitiesByFileAndLabelType.TryGetValue(entry.Key, out fileLabelTypeIntensities))
                            {
                                fileLabelTypeIntensities = new List<double>();
                                intensitiesByFileAndLabelType.Add(entry.Key, fileLabelTypeIntensities);
                            }
                            fileLabelTypeIntensities.Add(Math.Log(Math.Max(entry.Value, 1), 2.0));
                        }
                    }
                }
            }
            var data = new Dictionary<DataKey, DataValue>(intensitiesByFileAndLabelType.Count);
            foreach (var entry in intensitiesByFileAndLabelType)
            {
                data.Add(entry.Key, new DataValue(entry.Value));
            }
            return new NormalizationData(data);
        }

        public static NormalizationData GetNormalizationData(
            IEnumerable<Tuple<ChromFileInfoId, IsotopeLabelType, double>> values)
        {
            return new NormalizationData(
                values.ToLookup(value => new DataKey(value.Item1, null, value.Item2), value => value.Item3)
                .ToDictionary(grouping => grouping.Key, grouping => new DataValue(grouping))
                );
        }

        public double? NormalizeQuantile(ChromFileInfoId chromFileInfoId, IsotopeLabelType isotopeLabelType,
            double value)
        {
            DataValue dataValue;
            if (!_data.TryGetValue(new DataKey(chromFileInfoId, null, isotopeLabelType), out dataValue))
            {
                return null;
            }
            double percentile = dataValue.FindPercentileOfValue(value);
            return GetMeanPercentile(isotopeLabelType, percentile).Value;
        }

        public double? Percentile(ChromFileInfoId chromFileInfoId, IsotopeLabelType isotopeLabelType, double percentile)
        {
            var dataKey = new DataKey(chromFileInfoId, null, isotopeLabelType);
            DataValue dataValue;
            if (!_data.TryGetValue(dataKey, out dataValue))
            {
                return null;
            }
            return dataValue.GetValueAtPercentile(percentile);
        }

        public double? GetMedian(ChromFileInfoId chromFileInfoId, IsotopeLabelType isotopeLabelType)
        {
            var dataKey = new DataKey(chromFileInfoId, null, isotopeLabelType);
            DataValue dataValue;
            if (!_data.TryGetValue(dataKey, out dataValue))
            {
                return null;
            }
            return dataValue.Median;
        }

        public double? GetMedianMedian(SampleType sampleType, IsotopeLabelType isotopeLabelType)
        {
            double medianMedian;
            if (_medianMedians.TryGetValue(Tuple.Create(sampleType, isotopeLabelType), out medianMedian))
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
            public DataKey(ChromFileInfoId chromFileInfoId, SampleType sampleType, IsotopeLabelType isotopeLabelType)
                : this()
            {
                ChromFileInfoId = chromFileInfoId;
                SampleType = sampleType;
                IsotopeLabelType = isotopeLabelType;
            }
            public ChromFileInfoId ChromFileInfoId { get; private set; }
            public SampleType SampleType { get; private set; }
            public IsotopeLabelType IsotopeLabelType { get; private set; }

            public bool Equals(DataKey other)
            {
                return ReferenceEquals(ChromFileInfoId, other.ChromFileInfoId) && Equals(IsotopeLabelType, other.IsotopeLabelType);
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
                    return ((ChromFileInfoId != null ? RuntimeHelpers.GetHashCode(ChromFileInfoId) : 0) * 397) ^ (IsotopeLabelType != null ? IsotopeLabelType.GetHashCode() : 0);
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
    }
}
