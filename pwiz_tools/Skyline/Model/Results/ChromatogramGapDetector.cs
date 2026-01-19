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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class ChromatogramGapDetector : Immutable
    {
        /// <summary>
        /// Gap detector to use when "Triggered Chromatogram Extraction" is chosen.
        /// </summary>
        public static readonly ChromatogramGapDetector SENSITIVE = new ChromatogramGapDetector
        {
            ToleranceFactor = 10,
            PercentileReference = 0.9
        };

        /// <summary>
        /// Gap detector to use for regular data which is not expected to have any gaps.
        /// </summary>
        public static readonly ChromatogramGapDetector SELECTIVE = new ChromatogramGapDetector()
        {
            ToleranceFactor = 10,
            PercentileReference = .5
        };

        /// <summary>
        /// Gaps in the retention times are found by examining the times between scans.
        /// </summary>
        public double PercentileReference { get; private set; }

        public ChromatogramGapDetector ChangePercentileReference(double value)
        {
            return ChangeProp(ImClone(this), im => im.PercentileReference = value);
        }
        public double ToleranceFactor { get; private set; }

        public ChromatogramGapDetector ChangeToleranceFactor(double value)
        {
            return ChangeProp(ImClone(this), im => im.ToleranceFactor = value);
        }

        public TimeIntervals InferTimeIntervals(IEnumerable<IList<float>> timesList)
        {
            TimeIntervals result = null;
            var timesSet = new HashSet<object>(ReferenceValue.EQUALITY_COMPARER);
            foreach (var times in timesList)
            {
                if (!timesSet.Add(times))
                {
                    continue;
                }
                TimeIntervals timeIntervals = InferTimeIntervalsFromScanTimes(times);
                if (result == null)
                {
                    result = timeIntervals;
                }
                else
                {
                    var newResult = result.Intersect(timeIntervals);
                    result = newResult;
                }
            }

            return result;
        }

        public TimeIntervals InferTimeIntervalsFromScanTimes(IList<float> times)
        {
            float referenceDuration = GetReferenceScanDuration(times, PercentileReference / 100);
            return TimeIntervals.FromScanTimes(times, (float) (referenceDuration * ToleranceFactor));
        }

        public static IEnumerable<float> GetScanDurations(IEnumerable<float> times)
        {
            float? lastTime = null;
            foreach (var time in times)
            {
                if (lastTime.HasValue)
                {
                    yield return time - lastTime.Value;
                }

                lastTime = time;
            }
        }

        public static IEnumerable<float> GetMinimumScanDurations(IEnumerable<float> times)
        {
            float? lastTimeInterval = null;
            foreach (var duration in GetScanDurations(times))
            {
                if (lastTimeInterval.HasValue)
                {
                    yield return Math.Min(lastTimeInterval.Value, duration);
                }

                lastTimeInterval = duration;
            }
        }

        public static float GetReferenceScanDuration(IEnumerable<float> times, double percentile)
        {
            var statistics = new Statistics(GetMinimumScanDurations(times).Select(t => (double) t));
            return (float)statistics.QPercentile(percentile);
        }
    }
}
