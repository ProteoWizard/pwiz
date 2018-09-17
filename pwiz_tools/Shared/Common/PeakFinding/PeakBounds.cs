/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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

using System.Globalization;

namespace pwiz.Common.PeakFinding
{
    public sealed class PeakBounds
    {
        public PeakBounds(double startTime, double endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }

        public double StartTime { get; private set; }
        public double EndTime { get; private set; }

        private bool Equals(PeakBounds other)
        {
            return StartTime.Equals(other.StartTime) && EndTime.Equals(other.EndTime);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is PeakBounds && Equals((PeakBounds) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StartTime.GetHashCode() * 397) ^ EndTime.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, @"[{0:R},{1:R}]", StartTime, EndTime);
        }
    }
}
