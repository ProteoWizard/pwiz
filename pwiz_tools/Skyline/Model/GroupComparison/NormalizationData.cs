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
        private readonly double _medianMedians;

        private NormalizationData(IDictionary<DataKey, DataValue> data)
        {
            _data = data;
            if (data.Count > 0)
            {
                _medianMedians = data.Values.Select(value => value.Median).Median();

            }
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
            var internalStandardLabelTypes =
                document.Settings.PeptideSettings.Modifications.InternalStandardTypes.ToHashSet();

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
                        bool isInternalStandard = internalStandardLabelTypes.Contains(transitionGroup.LabelType);
                        foreach (var transition in transitionGroup.Transitions)
                        {
                            if (!transition.HasResults)
                            {
                                continue;
                            }

                            int msLevel = transition.IsMs1 ? 1 : 2;
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

                                    areaAccumulators[iResult].AddArea(chromInfo.FileId, msLevel, isInternalStandard, area.Value);
                                }
                            }
                        }

                        foreach (var areaAccumulator in areaAccumulators)
                        {
                            areaAccumulator.FinishTransitionGroup(isInternalStandard);
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

        public double? NormalizeQuantile(int replicateIndex, ChromFileInfoId chromFileInfoId, double value)
        {
            DataValue dataValue;
            if (!_data.TryGetValue(new DataKey(replicateIndex, chromFileInfoId), out dataValue))
            {
                return null;
            }
            double percentile = dataValue.FindPercentileOfValue(value);
            return GetMeanPercentile(percentile).Value;
        }

        public double? Percentile(int replicateIndex, ChromFileInfoId chromFileInfoId, double percentile)
        {
            var dataKey = new DataKey(replicateIndex, chromFileInfoId);
            DataValue dataValue;
            if (!_data.TryGetValue(dataKey, out dataValue))
            {
                return null;
            }
            return dataValue.GetValueAtPercentile(percentile);
        }

        public double? GetLog2Median(int replicateIndex, ChromFileInfoId chromFileInfoId)
        {
            var dataKey = new DataKey(replicateIndex, chromFileInfoId);
            DataValue dataValue;
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

        private struct DataKey
        {
            public DataKey(int replicateIndex, ChromFileInfoId chromFileInfoId)
                : this()
            {
                ReplicateIndex = replicateIndex;
                ChromFileInfoId = chromFileInfoId;
            }
            public int ReplicateIndex { get; private set; }
            public ChromFileInfoId ChromFileInfoId { get; private set; }
            public bool Equals(DataKey other)
            {
                return ReplicateIndex == other.ReplicateIndex && ReferenceEquals(ChromFileInfoId, other.ChromFileInfoId);
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
            private IDictionary<DataKey, FileData> _fileDataDictionary = new Dictionary<DataKey, FileData>();

            private ChromFileInfoId _firstFileId;
            private FileData _firstFileData = new FileData();
            public AreaAccumulator(int replicateIndex, ChromatogramSet chromatogramSet)
            {
                ReplicateIndex = replicateIndex;
                ChromatogramSet = chromatogramSet;
                _firstFileId = chromatogramSet.MSDataFileInfos.First().FileId;
            }
            public int ReplicateIndex { get; private set; }

            public ChromatogramSet ChromatogramSet { get; private set; }

            public void AddArea(ChromFileInfoId fileId, int msLevel, bool isInternalStandard, double area)
            {
                GetFileData(fileId).AddArea(msLevel, isInternalStandard, area);
            }

            private FileData GetFileData(ChromFileInfoId fileId)
            {
                if (ReferenceEquals(fileId, _firstFileId))
                {
                    // Avoid a dictionary lookup for the most common case where the fileId is the first one in the replicate
                    return _firstFileData;
                }

                var key = MakeDataKey(fileId);
                if (!_fileDataDictionary.TryGetValue(key, out var fileData))
                {
                    fileData = new FileData();
                    _fileDataDictionary.Add(key, fileData);
                }

                return fileData;
            }

            private DataKey MakeDataKey(ChromFileInfoId fileId)
            {
                return new DataKey(ReplicateIndex, fileId);
            }

            /// <summary>
            /// After processing all of the transitions in a transition group, the observed MS1 areas should be summed,
            /// and then added to the list of areas.
            /// </summary>
            public void FinishTransitionGroup(bool isInternalStandard)
            {
                foreach (var fileData in _fileDataDictionary.Values.Prepend(_firstFileData))
                {
                    fileData.FinishTransitionGroup(isInternalStandard);
                }
            }

            public IEnumerable<KeyValuePair<DataKey, List<double>>> GetAreaLists()
            {
                var result = _fileDataDictionary.Select(entry=>new KeyValuePair<DataKey, List<double>>(entry.Key, entry.Value.GetAreasList()));
                var firstFileAreas = _firstFileData.GetAreasList();
                if (firstFileAreas.Any())
                {
                    result = result.Prepend(new KeyValuePair<DataKey, List<double>>(MakeDataKey(_firstFileId), firstFileAreas));
                }
                return result;
            }

            private class FileData
            {
                private double? _ms1Area;
                private List<double> _areasList = new List<double>();
                private List<double> _internalStandardAreaList = new List<double>();

                public void AddArea(int msLevel, bool internalStandard, double area)
                {
                    if (msLevel == 1)
                    {
                        _ms1Area = (_ms1Area ?? 0) + area;
                    }
                    else
                    {
                        if (internalStandard)
                        {
                            _internalStandardAreaList.Add(area);
                        }
                        else
                        {
                            _areasList.Add(area);
                        }
                    }
                }

                public void FinishTransitionGroup(bool internalStandard)
                {
                    if (_ms1Area.HasValue)
                    {
                        if (internalStandard)
                        {
                            _internalStandardAreaList.Add(_ms1Area.Value);
                        }
                        else
                        {
                            _areasList.Add(_ms1Area.Value);
                        }

                        _ms1Area = null;
                    }
                }

                /// <summary>
                /// If there are any internal standard peak areas, then return them, otherwise return all of the areas.
                /// </summary>
                public List<double> GetAreasList()
                {
                    if (_internalStandardAreaList.Count != 0)
                    {
                        return _internalStandardAreaList;
                    }

                    return _areasList;
                }
            }
        }
    }

}
