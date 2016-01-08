/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Linq;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Lib
{
    /// <summary>
    /// Contains the results of aligning a set of MS2 Id's from one file to
    /// another.
    /// </summary>
    public class AlignedRetentionTimes
    {
        public IDictionary<string, double> TargetTimes { get; private set; }
        /// <summary>
        /// The original times that were read out of the spectral library.
        /// </summary>
        public IDictionary<string, double> OriginalTimes { get; private set; }

        public RetentionTimeRegression Regression { get; private set; }
        public RetentionTimeStatistics RegressionStatistics { get; private set; }
        public RetentionTimeRegression RegressionRefined { get; private set; }
        public RetentionTimeStatistics RegressionRefinedStatistics { get; private set; }
        public HashSet<int> OutlierIndexes { get; private set; }

        /// <summary>
        /// The number of points that were used to do the alignment.  (i.e. the number of peptide sequences which were in
        /// common between the two data files)
        /// </summary>
        public int RegressionPointCount { get { return Regression.PeptideTimes.Count; } }

        public IList<double> YValues { get { return Array.AsReadOnly(Regression.PeptideTimes.Select(measuredRetentionTime => measuredRetentionTime.RetentionTime).ToArray()); } }
        public IList<double> XValues { get
        {
            return
                Array.AsReadOnly(
                    Regression.PeptideTimes.Select(
                        measuredRetentionTime =>
                        Regression.Calculator.ScoreSequence(measuredRetentionTime.PeptideSequence)).Cast<double>().ToArray());
        } }

        /// <summary>
        /// Align retention times with a target.
        /// For the MS2 Id's that are found in both the target and the timesToAlign, the MS2 id's 
        /// are plotted against each other, and a linear regression is performed.
        /// In cases where there is more than one MS2 id in either file, only the earliest MS2 id from
        /// each file is used.
        /// </summary>
        public static AlignedRetentionTimes AlignLibraryRetentionTimes(IDictionary<string, double> target, IDictionary<string, double> originalTimes, double refinementThreshhold, Func<bool> isCanceled)
        {
            var calculator = new DictionaryRetentionScoreCalculator("alignment", originalTimes); // Not L10N
            var targetTimesList = new List<MeasuredRetentionTime>();
            foreach (var entry in calculator.RetentionTimes)
            {
                double targetTime;
                if (!target.TryGetValue(entry.Key, out targetTime))
                {
                    continue;
                }
                MeasuredRetentionTime measuredRetentionTime;
                try
                {
                    measuredRetentionTime = new MeasuredRetentionTime(entry.Key, targetTime);
                }
                catch
                {
                    continue;
                }
                targetTimesList.Add(measuredRetentionTime);
            }
            RetentionTimeStatistics regressionStatistics;
            var regression = RetentionTimeRegression.CalcRegression(XmlNamedElement.NAME_INTERNAL, new[] {calculator}, targetTimesList, out regressionStatistics);
            if (regression == null)
            {
                return null;
            }
            RetentionTimeRegression regressionRefined;
            RetentionTimeStatistics regressionRefinedStatistics = regressionStatistics;
            HashSet<int> outIndexes = new HashSet<int>();
            if (regressionStatistics.R >= refinementThreshhold)
            {
                regressionRefined = regression;
            }
            else
            {
                var cache = new RetentionTimeScoreCache(new[] {calculator}, new MeasuredRetentionTime[0], null);
                regressionRefined = regression.FindThreshold(refinementThreshhold, null, 0,
                                                                targetTimesList.Count, new MeasuredRetentionTime[0], targetTimesList, regressionStatistics,
                                                                calculator, cache, isCanceled, ref regressionRefinedStatistics,
                                                                ref outIndexes);
            }
                
            return new AlignedRetentionTimes
                       {
                           TargetTimes = target,
                           OriginalTimes = originalTimes,
                           Regression = regression,
                           RegressionStatistics = regressionStatistics,
                           RegressionRefined = regressionRefined,
                           RegressionRefinedStatistics = regressionRefinedStatistics,
                           OutlierIndexes = outIndexes,
                       };
        }

    }

    internal class DictionaryRetentionScoreCalculator : RetentionScoreCalculatorSpec
    {
        public DictionaryRetentionScoreCalculator(string name, IDictionary<string, double> retentionTimes)
            : base(name)
        {
            RetentionTimes = retentionTimes;
        }

        public IDictionary<string, double> RetentionTimes { get; private set; }
        public override double? ScoreSequence(string modifiedSequence)
        {
            double result;
            if (RetentionTimes.TryGetValue(modifiedSequence, out result))
            {
                return result;
            }
            return null;
        }

        public override double UnknownScore
        {
            get { return double.NaN; }
        }

        public override IEnumerable<string> ChooseRegressionPeptides(IEnumerable<string> peptides, out int minCount)
        {
            minCount = 0;
            return peptides.Where(peptide => null != ScoreSequence(peptide));
        }

        public override IEnumerable<string> GetStandardPeptides(IEnumerable<string> peptides)
        {
            int minCount;
            return ChooseRegressionPeptides(peptides, out minCount);
        }
    }
}


