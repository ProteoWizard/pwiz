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
using System;
using pwiz.Common.DataAnalysis;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.GroupComparison
{
    public struct FoldChangeResult : IComparable
    {
        public FoldChangeResult(double confidenceLevel, double adjustedPValue, LinearFitResult linearFitResult) : this()
        {
            ConfidenceLevel = confidenceLevel;
            LinearFit = linearFitResult;
            AdjustedPValue = adjustedPValue;
            
        }

        [Format(Formats.CV)]
        public double ConfidenceLevel { get; private set; }
        [Format(Formats.FoldChange)]
        public double FoldChange 
        { get { return Math.Pow(2.0, LinearFit.EstimatedValue); }}
        [Format(Formats.FoldChange)]
        public double Log2FoldChange { get { return LinearFit.EstimatedValue; }}
        [Format(Formats.PValue)]
        public double AdjustedPValue { get; private set; }

        [Format(Formats.FoldChange)]
        public double MinFoldChange
        {
            get
            {
                double criticalValue = GetCriticalValue(ConfidenceLevel, LinearFit.DegreesOfFreedom);
                return Math.Pow(2.0, LinearFit.EstimatedValue - LinearFit.StandardError * criticalValue);
            }
        }

        [Format(Formats.FoldChange)]
        public double MaxFoldChange
        {
            get
            {
                double criticalValue = GetCriticalValue(ConfidenceLevel, LinearFit.DegreesOfFreedom);
                return Math.Pow(2.0, LinearFit.EstimatedValue + LinearFit.StandardError*criticalValue);
            }
        }

        public LinearFitResult LinearFit { get; private set; }

        public override string ToString()
        {
            string formatFoldChange = Formats.PEAK_FOUND_RATIO;
            string formatConfidenceLevel = Formats.CV;
            return string.Format("{0} ({1} CI:{2} to {3})", // Not L10N
                FoldChange.ToString(formatFoldChange), 
                ConfidenceLevel.ToString(formatConfidenceLevel), 
                MinFoldChange.ToString(formatFoldChange), 
                MaxFoldChange.ToString(formatFoldChange));
        }

        /// <summary>
        /// Returns the value from the Student's T-Distribution table for the particular confidence level and
        /// degrees of freedom.
        /// </summary>
        private static double GetCriticalValue(double twoTailedConfidence, int degreesOfFreedom)
        {
            if (degreesOfFreedom <= 0)
            {
                return double.NaN;
            }
            return Statistics.QT((1 + twoTailedConfidence) / 2, degreesOfFreedom);
        }

        public int CompareTo(object obj)
        {
            if (null == obj)
            {
                return 1;
            }
            FoldChangeResult that = (FoldChangeResult) obj;
            var thisTuple = new Tuple<double, double>(FoldChange, MinFoldChange);
            var thatTuple = new Tuple<double, double>(that.FoldChange, that.MinFoldChange);
            return ((IComparable) thisTuple).CompareTo(thatTuple);
        }
    }
}
