/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Lib
{
    public class ExplicitPeakBounds : Immutable
    {
        public const double UNKNOWN_SCORE = double.NaN;
        public static readonly ExplicitPeakBounds EMPTY = new ExplicitPeakBounds(0, 0, UNKNOWN_SCORE);

        public ExplicitPeakBounds(double startTime, double endTime, double score)
        {
            StartTime = startTime;
            EndTime = endTime;
            Score = score;
        }
        public double StartTime { get; private set; }
        public double EndTime { get; private set; }
        public double Score { get; private set; }

        protected bool Equals(ExplicitPeakBounds other)
        {
            return StartTime.Equals(other.StartTime) && EndTime.Equals(other.EndTime) && Score.Equals(other.Score);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ExplicitPeakBounds) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StartTime.GetHashCode();
                hashCode = (hashCode * 397) ^ EndTime.GetHashCode();
                hashCode = (hashCode * 397) ^ Score.GetHashCode();
                return hashCode;
            }
        }

        public bool IsEmpty
        {
            get { return StartTime == 0 && EndTime == 0; }
        }
    }
}
