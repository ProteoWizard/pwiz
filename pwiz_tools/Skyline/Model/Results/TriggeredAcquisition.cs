using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class TriggeredAcquisition : Immutable
    {
        public TriggeredAcquisition()
        {
            ScanTimeTolerance = 10;
        }
        public double ScanTimeTolerance { get; private set; }

        public TriggeredAcquisition ChangeScanTimeTolerance(double value)
        {
            return ChangeProp(ImClone(this), im => im.ScanTimeTolerance = value);
        }

        public TimeIntervals InferTimeIntervals(IEnumerable<IList<float>> timesList)
        {
            TimeIntervals result = null;
            var timesSet = new HashSet<object>(new IdentityEqualityComparer<object>());
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
            float medianDuration = GetMedianScanDuration(times);
            return TimeIntervals.FromScanTimes(times, (float) (medianDuration * ScanTimeTolerance));
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

        public static float GetMedianScanDuration(IEnumerable<float> times)
        {
            var statistics = new Statistics(GetMinimumScanDurations(times).Select(t => (double) t));
            return (float) statistics.Median();
        }
    }
}