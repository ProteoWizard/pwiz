/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using pwiz.Common.SystemUtil.Caching;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class ChromatogramTimeRanges
    {
        public static readonly Producer<Parameter, ChromatogramTimeRanges> PRODUCER = new Producer();
        private Dictionary<Key, TimeRangeDict> _timeRanges;


        private ChromatogramTimeRanges(Dictionary<Key, TimeRangeDict> timeRanges)
        {
            _timeRanges = timeRanges;
        }

        public static ChromatogramTimeRanges ReadChromatogramTimeRanges(CancellationToken cancellationToken,
            IEnumerable<ChromatogramCache> caches)
        {
            var timeRangeDicts = new Dictionary<Key, Dictionary<MsDataFileUri, TimeIntervals>>();
            foreach (var cache in caches)
            {
                for (int iHeader = 0; iHeader < cache.ChromGroupHeaderInfos.Count; iHeader++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var header = cache.ChromGroupHeaderInfos[iHeader];
                    TimeIntervals timeIntervals = null;
                    if (header.StartTime.HasValue && header.EndTime.HasValue)
                    {
                        timeIntervals = TimeIntervals.FromIntervals(new []{new KeyValuePair<float, float>(header.StartTime.Value, header.EndTime.Value)});
                    }

                    if (timeIntervals != null)
                    {
                        var key = new Key(cache.GetChromatogramGroupId(header), header.Precursor);
                        if (!timeRangeDicts.TryGetValue(key, out var dict))
                        {
                            dict = new Dictionary<MsDataFileUri, TimeIntervals>();
                            timeRangeDicts[key] = dict;
                        }

                        var msDataFileUri = cache.CachedFiles[header.FileIndex].FilePath;
                        dict[msDataFileUri] = timeIntervals;
                    }
                }
            }

            return new ChromatogramTimeRanges(timeRangeDicts.ToDictionary(kvp=>kvp.Key, kvp=>new TimeRangeDict(kvp.Value)));
        }

        public class TimeRangeDict
        {
            private Dictionary<MsDataFileUri, TimeIntervals> _timeIntervalsByFile;
            public TimeRangeDict(Dictionary<MsDataFileUri, TimeIntervals> timeIntervalsByFile)
            {
                _timeIntervalsByFile = timeIntervalsByFile;
            }

            public TimeIntervals GetTimeIntervals(MsDataFileUri msDataFileUri)
            {
                _timeIntervalsByFile.TryGetValue(msDataFileUri, out var timeIntervals);
                return timeIntervals;
            }

            public TimeRangeDict Intersect(TimeRangeDict timeRangeDict)
            {
                var newDict = new Dictionary<MsDataFileUri, TimeIntervals>();
                foreach (var grouping in _timeIntervalsByFile.Concat(timeRangeDict._timeIntervalsByFile)
                             .GroupBy(kvp => kvp.Key, kvp=>kvp.Value))
                {
                    if (grouping.Count() == 1)
                    {
                        newDict.Add(grouping.Key, grouping.First());
                    }
                    else
                    {
                        TimeIntervals mergedTimeIntervals = null;
                        foreach (var timeIntervals in grouping)
                        {
                            mergedTimeIntervals = mergedTimeIntervals?.Intersect(timeIntervals) ?? timeIntervals;
                        }

                        if (mergedTimeIntervals != null)
                        {
                            newDict.Add(grouping.Key, mergedTimeIntervals);
                        }
                    }
                }

                return new TimeRangeDict(newDict);
            }
        }

        private class Key
        {
            public Key(ChromatogramGroupId chromatogramGroupId, double precursorMz)
            {
                ChromatogramGroupId = chromatogramGroupId;
                PrecursorMz = precursorMz;
            }
            public ChromatogramGroupId ChromatogramGroupId { get; }
            public double PrecursorMz { get; }

            protected bool Equals(Key other)
            {
                return Equals(ChromatogramGroupId, other.ChromatogramGroupId) && PrecursorMz.Equals(other.PrecursorMz);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Key)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((ChromatogramGroupId != null ? ChromatogramGroupId.GetHashCode() : 0) * 397) ^ PrecursorMz.GetHashCode();
                }
            }
        }

        public TimeRangeDict GetTimeRanges(PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode)
        {
            var key = new Key(ChromatogramGroupId.ForPeptide(peptideDocNode, transitionGroupDocNode),
                transitionGroupDocNode.PrecursorMz);
            _timeRanges.TryGetValue(key, out var timeRanges);
            return timeRanges;
        }

        public TimeRangeDict GetTimeRanges(PeptideDocNode peptideDocNode)
        {
            TimeRangeDict result = null;
            foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
            {
                var timeRangeDict = GetTimeRanges(peptideDocNode, transitionGroupDocNode);
                if (timeRangeDict != null)
                {
                    result = result?.Intersect(timeRangeDict) ?? timeRangeDict;
                }
            }

            return result;
        }

        public class Parameter
        {
            public Parameter(MeasuredResults measuredResults, bool inferFromPoints)
            {
                MeasuredResults = measuredResults;
                InferFromPoints = inferFromPoints;
            }

            public MeasuredResults MeasuredResults { get; }
            public bool InferFromPoints { get; }

            protected bool Equals(Parameter other)
            {
                return ReferenceEquals(MeasuredResults, other.MeasuredResults) && InferFromPoints == other.InferFromPoints;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Parameter)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var result = MeasuredResults == null ? 0 : RuntimeHelpers.GetHashCode(MeasuredResults);
                    result = (result * 397) ^ InferFromPoints.GetHashCode();
                    return result;
                }
            }
        }

        private class Producer : Producer<Parameter, ChromatogramTimeRanges>
        {
            public override ChromatogramTimeRanges ProduceResult(ProductionMonitor productionMonitor, Parameter parameter, IDictionary<WorkOrder, object> inputs)
            {
                return parameter.MeasuredResults?.GetChromatogramTimeRanges(productionMonitor.CancellationToken);
            }

            public override string GetDescription(object workParameter)
            {
                return "Reading chromatogram time ranges";
            }
        }
    }
}
