/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class RatioValue : IComparable
    {
        private RatioValue()
        {
        }

        public RatioValue(double ratio)
        {
            Ratio = (float) ratio;
            StdDev = float.NaN;
            DotProduct = 1;
        }

        public float Ratio { get; private set; }
        public float StdDev { get; private set; }
        public float DotProduct { get; private set; }

        public static RatioValue Calculate(IList<double> numerators, IList<double> denominators)
        {
            if (numerators.Count != denominators.Count)
            {
                throw new ArgumentException();
            }
            if (numerators.Count == 0)
            {
                return null;
            }
            if (numerators.Count == 1)
            {
                return new RatioValue(numerators.First()/denominators.First());
            }
            var statsNumerators = new Statistics(numerators);
            var statsDenominators = new Statistics(denominators);
            var ratios = new Statistics(numerators.Select((value, index) => value/denominators[index]));
            
            // The mean ratio is the average of "ratios" weighted by "statsDenominators".
            // It's also equal to the sum of the numerators divided by the sum of the denominators.
            var meanRatio = statsNumerators.Sum()/statsDenominators.Sum();

            // Helpers.Assume(Math.Abs(mean - stats.Mean(statsW)) < 0.0001);
            // Make sure the value does not exceed the bounds of a float.
            float meanRatioFloat = (float)Math.Min(float.MaxValue, Math.Max(float.MinValue, meanRatio));

            return new RatioValue
            {
                Ratio = meanRatioFloat,
                StdDev = (float) ratios.StdDev(statsDenominators),
                DotProduct = (float) statsNumerators.Angle(statsDenominators),
            };
        }

        public static RatioValue ValueOf(double? ratio)
        {
            return ratio.HasValue ? new RatioValue(ratio.Value) : null;
        }

        public override string ToString()
        {
            return Ratio + (double.IsNaN(StdDev) ? "" : (" rdotp " + DotProduct));  // Not L10N
        }

        public int CompareTo(object obj)
        {
            if (null == obj)
            {
                return 1;
            }
            return Ratio.CompareTo(((RatioValue) obj).Ratio);
        }

        public static float? GetRatio(RatioValue ratioValue)
        {
            return ratioValue == null ? (float?) null : ratioValue.Ratio;
        }

        public static float? GetDotProduct(RatioValue ratioValue)
        {
            return ratioValue == null ? (float?) null : ratioValue.DotProduct;
        }

        #region Equality Members
        protected bool Equals(RatioValue other)
        {
            return Ratio.Equals(other.Ratio) && StdDev.Equals(other.StdDev) && DotProduct.Equals(other.DotProduct);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((RatioValue)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Ratio.GetHashCode();
                hashCode = (hashCode * 397) ^ StdDev.GetHashCode();
                hashCode = (hashCode * 397) ^ DotProduct.GetHashCode();
                return hashCode;
            }
        }
        #endregion
    }
}
