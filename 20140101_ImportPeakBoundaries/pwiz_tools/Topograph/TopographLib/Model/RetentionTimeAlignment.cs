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
namespace pwiz.Topograph.Model
{
    public class RetentionTimeAlignment
    {
        public static readonly RetentionTimeAlignment Invalid = new RetentionTimeAlignment(double.NaN, double.NaN);
        public RetentionTimeAlignment(double slope, double intercept)
        {
            Slope = slope;
            Intercept = intercept;
        }

        public double Slope { get; private set; }
        public double Intercept { get; private set; }

        public double GetTargetTime(double time)
        {
            return Slope*time + Intercept;
        }

        protected bool Equals(RetentionTimeAlignment other)
        {
            return Slope.Equals(other.Slope) && Intercept.Equals(other.Intercept);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RetentionTimeAlignment) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Slope.GetHashCode()*397) ^ Intercept.GetHashCode();
            }
        }
        public bool IsInvalid
        {
            get { return Equals(Slope, double.NaN); }
        }
    }
}
