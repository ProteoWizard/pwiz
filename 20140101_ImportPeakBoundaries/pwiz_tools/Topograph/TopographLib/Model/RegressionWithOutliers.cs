/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Model
{
    public class RegressionWithOutliers
    {
        public const double RequiredCorrelationCoefficient = .99;
        public const int MinRefinedPointCount = 20;
        public RegressionWithOutliers(IEnumerable<double> originalTimes, IEnumerable<double> targetTimes) : this(ImmutableList.ValueOf(originalTimes), ImmutableList.ValueOf(targetTimes), new HashSet<int>())
        {
        }
        private RegressionWithOutliers(IList<double> originalTimes, IList<double> targetTimes, ISet<int> outlierIndexes)
        {
            if (originalTimes.Count != targetTimes.Count)
            {
                throw new ArgumentException("Value lists must have same length");
            }
            OriginalTimes = originalTimes;
            TargetTimes = targetTimes;
            OutlierIndexes = outlierIndexes;
            var statsTarget = new Statistics(TargetTimes.Where((value, index) => !outlierIndexes.Contains(index)).ToArray());
            var statsOriginal =
                new Statistics(OriginalTimes.Where((value, index) => !outlierIndexes.Contains(index)).ToArray());
            Debug.Assert(statsTarget.Length == statsOriginal.Length);
            Debug.Assert(statsTarget.Length == TotalCount - outlierIndexes.Count);
            Slope = statsTarget.Slope(statsOriginal);
            Intercept = statsTarget.Intercept(statsOriginal);
            R = statsTarget.R(statsOriginal);
            if (double.IsNaN(R))
            {
                R = 0;
            }
        }

        public IList<double> OriginalTimes { get; private set; }
        public IList<double> TargetTimes { get; private set; }

        public int TotalCount { get { return OriginalTimes.Count; } }
        public int RefinedCount { get { return TotalCount - OutlierCount; } }
        public int OutlierCount { get { return OutlierIndexes.Count; } }
        public double Slope { get; private set; }
        public double Intercept { get; private set; }
        public double R { get; private set; }
        public ISet<int> OutlierIndexes { get; private set; }
        public RegressionWithOutliers WithNewOutlierCount(int newOutlierCount)
        {
            var residuals =
                TargetTimes.Select(
                    (targetTime, index) =>
                    new KeyValuePair<double, int>(OriginalTimes[index]*Slope + Intercept - targetTime, index)).ToArray();
            Array.Sort(residuals, CompareResidualIndex);
            var newOutliers =
                new HashSet<int>(residuals.Skip(residuals.Length - newOutlierCount).Select(kvp => kvp.Value));
            return new RegressionWithOutliers(OriginalTimes, TargetTimes, newOutliers);
        }
        public RegressionWithOutliers WithNewRefinedCount(int newRefinedCount)
        {
            return WithNewOutlierCount(TotalCount - newRefinedCount);
        }
        private static int CompareResidualIndex(KeyValuePair<double, int> value1, KeyValuePair<double, int> value2)
        {
            int result = Math.Abs(value1.Key).CompareTo(Math.Abs(value2.Key));
            if (result != 0)
            {
                return result;
            }
            return value1.Value.CompareTo(value2.Value);
        }
        public RegressionWithOutliers Refine()
        {
            return Refine(RequiredCorrelationCoefficient, MinRefinedPointCount);
        }
        public RegressionWithOutliers Refine(double targetCorrelationCoefficient, int minRefinedPointCount)
        {
            var mostOutliers = this;
            while (mostOutliers.R < targetCorrelationCoefficient)
            {
                if (mostOutliers.RefinedCount <= minRefinedPointCount)
                {
                    return null;
                }
                int newRefinedCount = (mostOutliers.RefinedCount + minRefinedPointCount)/2;
                mostOutliers = mostOutliers.WithNewRefinedCount(newRefinedCount);
            }
            var leastOutliers = this;
            while (leastOutliers.RefinedCount > mostOutliers.RefinedCount + 1)
            {
                var newRefinedCount = (leastOutliers.RefinedCount + mostOutliers.RefinedCount)/2;
                var mid = mostOutliers.WithNewRefinedCount(newRefinedCount);
                if (mid.R >= targetCorrelationCoefficient)
                {
                    mostOutliers = mid;
                }
                else
                {
                    leastOutliers = mid;
                }
            }
            return mostOutliers;
        }
    }
}
