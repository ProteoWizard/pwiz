/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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

namespace pwiz.Topograph.Model
{
    public class RetentionTimeAlignment
    {
        private double[] _sourceTimes;
        private double[] _targetTimes;
        private RetentionTimeAlignment(double[] sourceTimes, double[] targetTimes)
        {
            _sourceTimes = sourceTimes;
            _targetTimes = targetTimes;
        }

        public double GetTargetTime(double time)
        {
            var index = Array.BinarySearch(_sourceTimes, time);
            if (index >= 0)
            {
                return _targetTimes[index];
            }
            index = ~index;
            if (index <= 0)
            {
                return _sourceTimes[0];
            }
            if (index >= _sourceTimes.Length)
            {
                return _targetTimes[_targetTimes.Length - 1];
            }
            var result = (((time - _sourceTimes[index - 1]) * _targetTimes[index]) + (_sourceTimes[index] - time) * _targetTimes[index - 1])
                   / (_sourceTimes[index] - _sourceTimes[index - 1]);
            return result;
        }

        public static RetentionTimeAlignment GetRetentionTimeAlignment(IList<double> sourceTimes, IList<double> targetTimes)
        {
            if (sourceTimes.Count() != targetTimes.Count())
            {
                throw new ArgumentException("Arrays must be same length");
            }
            if (sourceTimes.Count() <= 2)
            {
                return new RetentionTimeAlignment(sourceTimes.ToArray(), targetTimes.ToArray());
            }
            // Since we do linear interpolation, reduce size of arrays we store by not including points
            // that lie on a straight line within a tolerance of epsilon.
            double epsilon = .0001;
            var filteredSourceTimes = new List<double> { sourceTimes[0] };
            var filteredTargetTimes = new List<double> {targetTimes[0]};
            for (int i = 1; i < sourceTimes.Count - 1; i++)
            {
                double delta = (sourceTimes[i] - sourceTimes[i - 1])*(targetTimes[i + 1] - targetTimes[i])
                               - (sourceTimes[i + 1] - sourceTimes[i])*(targetTimes[i] - targetTimes[i - 1]);

                if (Math.Abs(delta) <= epsilon)
                {
                    continue;
                }
                filteredSourceTimes.Add(sourceTimes[i]);
                filteredTargetTimes.Add(targetTimes[i]);
            }
            filteredSourceTimes.Add(sourceTimes[sourceTimes.Count - 1]);
            filteredTargetTimes.Add(targetTimes[targetTimes.Count - 1]);
            return new RetentionTimeAlignment(filteredSourceTimes.ToArray(), filteredTargetTimes.ToArray());
        }
    }
}
