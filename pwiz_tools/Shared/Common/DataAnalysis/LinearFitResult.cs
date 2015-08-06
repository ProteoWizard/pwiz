/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

namespace pwiz.Common.DataAnalysis
{
    public sealed class LinearFitResult
    {
        public LinearFitResult(double estimatedValue)
        {
            EstimatedValue = estimatedValue;
        }

        private LinearFitResult(LinearFitResult linearFitResult)
        {
            EstimatedValue = linearFitResult.EstimatedValue;
            StandardError = linearFitResult.StandardError;
            TValue = linearFitResult.TValue;
            DegreesOfFreedom = linearFitResult.DegreesOfFreedom;
            PValue = linearFitResult.PValue;
        }
        public double EstimatedValue { get; private set; }

        public LinearFitResult SetEstimatedValue(double value)
        {
            return new LinearFitResult(this){EstimatedValue = value};
        }
        public double StandardError { get; private set; }

        public LinearFitResult SetStandardError(double value)
        {
            return new LinearFitResult(this){StandardError = value};
        }
       
        public double TValue { get; private set; }

        public LinearFitResult SetTValue(double value)
        {
            return new LinearFitResult(this){TValue = value};
        }
        public int DegreesOfFreedom { get; private set; }

        public LinearFitResult SetDegreesOfFreedom(int value)
        {
            return new LinearFitResult(this){DegreesOfFreedom = value};
        }
        public double PValue { get; private set; }

        public LinearFitResult SetPValue(double value)
        {
            return new LinearFitResult(this){PValue = value};
        }

        private bool Equals(LinearFitResult other)
        {
            return EstimatedValue.Equals(other.EstimatedValue) && StandardError.Equals(other.StandardError) && TValue.Equals(other.TValue) && DegreesOfFreedom == other.DegreesOfFreedom && PValue.Equals(other.PValue);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is LinearFitResult && Equals((LinearFitResult) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = EstimatedValue.GetHashCode();
                hashCode = (hashCode*397) ^ StandardError.GetHashCode();
                hashCode = (hashCode*397) ^ TValue.GetHashCode();
                hashCode = (hashCode*397) ^ DegreesOfFreedom;
                hashCode = (hashCode*397) ^ PValue.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            // ReSharper disable NonLocalizedString
            return string.Format("{0} SE {1}", EstimatedValue.ToString("0.####"), StandardError.ToString("0.####"));
            // ReSharper restore NonLocalizedString
        }
    }
}
