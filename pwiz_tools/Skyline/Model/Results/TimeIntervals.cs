/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class TimeIntervals
    {
        public static TimeIntervals FromIntervals(IEnumerable<KeyValuePair<float, float>> intervals)
        {
            return FromIntervalsSorted(MergeIntervals(intervals));
        }

        private static TimeIntervals FromIntervalsSorted(IEnumerable<KeyValuePair<float, float>> intervals)
        {
            var list = intervals.ToList();
            return new TimeIntervals
            {
                Starts = ImmutableList.ValueOf(list.Select(kvp => kvp.Key)),
                Ends = ImmutableList.ValueOf(list.Select(kvp => kvp.Value))
            };
        }

        public ImmutableList<float> Starts { get; private set; }
        public ImmutableList<float> Ends { get; private set; }

        public IEnumerable<KeyValuePair<float, float>> Intervals
        {
            get { return Enumerable.Range(0, Count).Select(i => new KeyValuePair<float, float>(Starts[i], Ends[i])); }
        }

        public int Count
        {
            get { return Starts.Count; }
        }

        public TimeIntervals Intersect(TimeIntervals other)
        {
            using (var myIntervals = Intervals.GetEnumerator())
            using (var otherIntervals = other.Intervals.GetEnumerator())
            {
                return FromIntervalsSorted(Intersect(myIntervals, otherIntervals));
            }
        }

        public int? IndexOfIntervalContaining(float time)
        {
            int index = IndexOfIntervalEndingAfter(time);
            if (index < 0 || index >= Ends.Count)
            {
                return null;
            }

            Assume.IsTrue(Ends[index] >= time);
            if (Starts[index] <= time)
            {
                return index;
            }

            return null;
        }

        public int IndexOfIntervalEndingAfter(float time)
        {
            int index = CollectionUtil.BinarySearch(Ends, time);
            if (index < 0)
            {
                index = ~index;
            }

            return index;
        }

        public bool ContainsTime(float time)
        {
            return IndexOfIntervalContaining(time).HasValue;
        }

        public static IEnumerable<KeyValuePair<float, float>> MergeIntervals(
            IEnumerable<KeyValuePair<float, float>> intervals)
        {
            KeyValuePair<float, float>? lastInterval = null;
            foreach (var interval in intervals.OrderBy(kvp => kvp.Key))
            {
                if (interval.Value <= interval.Key)
                {
                    continue;
                }

                if (lastInterval.HasValue)
                {
                    if (lastInterval.Value.Value >= interval.Key)
                    {
                        lastInterval = new KeyValuePair<float, float>(lastInterval.Value.Key,
                            Math.Max(lastInterval.Value.Value, interval.Value));
                        continue;
                    }
                    else
                    {
                        yield return lastInterval.Value;
                    }
                }

                lastInterval = interval;
            }

            if (lastInterval.HasValue)
            {
                yield return lastInterval.Value;
            }
        }

        private static IEnumerable<KeyValuePair<float, float>> Intersect(IEnumerator<KeyValuePair<float, float>> en1,
            IEnumerator<KeyValuePair<float, float>> en2)
        {
            if (!en1.MoveNext() || !en2.MoveNext())
            {
                yield break;
            }

            KeyValuePair<float, float> interval1 = en1.Current;
            KeyValuePair<float, float> interval2 = en2.Current;
            while (true)
            {
                var intersection = new KeyValuePair<float, float>(
                    Math.Max(interval1.Key, interval2.Key),
                    Math.Min(interval1.Value, interval2.Value));
                if (intersection.Key < intersection.Value)
                {
                    yield return intersection;
                }
                if (interval1.Value <= interval2.Value)
                {
                    if (!en1.MoveNext())
                    {
                        yield break;
                    }

                    interval1 = en1.Current;
                }
                else
                {
                    if (!en2.MoveNext())
                    {
                        yield break;
                    }

                    interval2 = en2.Current;
                }
            }
        }

        public static TimeIntervals FromScanTimes(IEnumerable<float> times, float maxScanDuration)
        {
            return FromIntervalsSorted(InferTimeIntervals(times, maxScanDuration));
        }

        private static IEnumerable<KeyValuePair<float, float>> InferTimeIntervals(IEnumerable<float> times,
            float maxScanDuration)
        {
            KeyValuePair<float, float>? lastInterval = null;
            foreach (var time in times)
            {
                if (lastInterval.HasValue)
                {
                    if (time < lastInterval.Value.Value)
                    {
                        throw new InvalidOperationException();
                    }
                    if (time <= lastInterval.Value.Value + maxScanDuration)
                    {
                        lastInterval = new KeyValuePair<float, float>(lastInterval.Value.Key, time);
                        continue;
                    }
                    else
                    {
                        if (lastInterval.Value.Value > lastInterval.Value.Key)
                        {
                            yield return lastInterval.Value;
                        }
                    }
                }
                lastInterval = new KeyValuePair<float, float>(time, time);
            }

            if (lastInterval.HasValue && lastInterval.Value.Value > lastInterval.Value.Key)
            {
                yield return lastInterval.Value;
            }
        }

        /// <summary>
        /// Replace with NaN all of the intensities in the list that fall outside of these time intervals.
        /// Also, if two adjacent points belong to different intervals, put a NaN in between them.
        /// </summary>
        public IEnumerable<KeyValuePair<float, float>> ReplaceExternalPointsWithNaN(
            IEnumerable<KeyValuePair<float, float>> timeIntensities)
        {
            int? lastInterval = null;
            foreach (var keyValuePair in timeIntensities)
            {
                int? intervalIndex = IndexOfIntervalContaining(keyValuePair.Key);
                if (intervalIndex.HasValue)
                {
                    if (lastInterval.HasValue && lastInterval != intervalIndex)
                    {
                        float midTime = (Ends[lastInterval.Value] + Starts[intervalIndex.Value]) / 2;
                        yield return new KeyValuePair<float, float>(midTime, float.NaN);
                    }

                    yield return keyValuePair;
                }
                else
                {
                    yield return new KeyValuePair<float, float>(keyValuePair.Key, float.NaN);
                }

                lastInterval = intervalIndex;
            }
        }
    }
}
