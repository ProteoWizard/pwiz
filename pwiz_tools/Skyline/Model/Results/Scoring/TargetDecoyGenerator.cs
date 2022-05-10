/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results.Scoring
{
    /// <summary>
    /// Class to separate target and decoy peaks, and keep track of disabled calculators.
    /// </summary>
    public class TargetDecoyGenerator
    {
        public bool[] EligibleScores { get; private set; }

        public FeatureCalculators FeatureCalculators { get; private set; }

        private readonly PeakTransitionGroupFeatureSet _peakTransitionGroupFeaturesList;

        public Dictionary<PeakTransitionGroupIdKey, List<PeakTransitionGroupFeatures>> PeakTransitionGroupDictionary { get; private set; }
        public bool ReplaceUnknownFeatureScores { get; }

        public TargetDecoyGenerator(IPeakScoringModel scoringModel, PeakTransitionGroupFeatureSet featureScores)
        {
            ReplaceUnknownFeatureScores = scoringModel.ReplaceUnknownFeatureScores;
            // Determine which calculators will be used to score peaks in this document.
            FeatureCalculators = scoringModel.PeakFeatureCalculators;
            _peakTransitionGroupFeaturesList = featureScores;
            PopulateDictionary();

            EligibleScores = new bool[FeatureCalculators.Count];
            // Disable calculators that have only a single score value or any unknown scores.
            ParallelEx.For(0, FeatureCalculators.Count, i => EligibleScores[i] = IsValidCalculator(i));
        }

        public TargetDecoyGenerator(SrmDocument document, IPeakScoringModel scoringModel, IFeatureScoreProvider scoreProvider, IProgressMonitor progressMonitor)
            : this(scoringModel,
                   scoreProvider != null
                       ? scoreProvider.GetFeatureScores(document, scoringModel, progressMonitor)
                       : document.GetPeakFeatures(scoringModel.PeakFeatureCalculators, progressMonitor))
        {
        }

        public int TargetCount { get { return _peakTransitionGroupFeaturesList.TargetCount; } }
        public int DecoyCount { get { return _peakTransitionGroupFeaturesList.DecoyCount; } }
        public PeakTransitionGroupFeatureSet PeakGroupFeatures { get { return _peakTransitionGroupFeaturesList; } }

        private void PopulateDictionary()
        {
            PeakTransitionGroupDictionary = new Dictionary<PeakTransitionGroupIdKey, List<PeakTransitionGroupFeatures>>();
            foreach (var transitionGroupFeatures in _peakTransitionGroupFeaturesList.Features)
            {
                var key = transitionGroupFeatures.Key;
                List<PeakTransitionGroupFeatures> listFeatures;
                if (!PeakTransitionGroupDictionary.TryGetValue(key, out listFeatures))
                {
                    listFeatures = new List<PeakTransitionGroupFeatures>();
                    PeakTransitionGroupDictionary.Add(key, listFeatures);                    
                }
                listFeatures.Add(transitionGroupFeatures);
            }
        }

        public void GetTransitionGroups(out List<IList<FeatureScores>> targetGroups,
            out List<IList<FeatureScores>> decoyGroups)
        {
            targetGroups = new List<IList<FeatureScores>>(TargetCount);
            decoyGroups = new List<IList<FeatureScores>>(DecoyCount);

            foreach (var peakTransitionGroupFeatures in _peakTransitionGroupFeaturesList.Features)
            {
                int featuresCount = peakTransitionGroupFeatures.PeakGroupFeatures.Count;
                if (featuresCount == 0)
                    continue;
                var transitionGroup = new FeatureScores[featuresCount];
                for (int i = 0; i < featuresCount; i++)
                {
                    transitionGroup[i] = peakTransitionGroupFeatures.PeakGroupFeatures[i].FeatureScores;
                }

                if (peakTransitionGroupFeatures.IsDecoy)
                    decoyGroups.Add(transitionGroup);
                else
                    targetGroups.Add(transitionGroup);
            }
        }

        public void GetScoresForCalculator(int calculatorIndex, List<double> targetScores, List<double> decoyScores,
            List<double> secondBestScores)
        {
            var weights = ImmutableList.ValueOf(Enumerable.Range(0, FeatureCalculators.Count)
                .Select(i => i == calculatorIndex ? 1 : double.NaN));
            var linearModelParams = new LinearModelParams(weights);
            int invertSign = FeatureCalculators[calculatorIndex].IsReversedScore ? -1 : 1;
            GetScores(linearModelParams, linearModelParams, targetScores, decoyScores, secondBestScores, false, invertSign);
        }

        /// <summary>
        /// Calculate scores for targets and decoys.  A transition is selected from each transition group using the
        /// scoring weights, and then its score is calculated using the calculator weights applied to each feature.
        /// </summary>
        /// <param name="calculatorParams">Parameters to calculate the score of the best peak.</param>
        /// <param name="targetScores">Output list of target scores.</param>
        /// <param name="decoyScores">Output list of decoy scores.</param>
        /// <param name="secondBestScores">Output list of false target scores.</param>
        public void GetScores(LinearModelParams calculatorParams, List<double> targetScores, List<double> decoyScores,
            List<double> secondBestScores)
        {
            GetScores(calculatorParams, calculatorParams, targetScores, decoyScores, secondBestScores, ReplaceUnknownFeatureScores, 1);
        }


        /// <summary>
        /// Calculate scores for targets and decoys.  A transition is selected from each transition group using the
        /// scoring weights, and then its score is calculated using the calculator weights applied to each feature.
        /// </summary>
        /// <param name="scoringParams">Parameters to choose the best peak</param>
        /// <param name="calculatorParams">Parameters to calculate the score of the best peak.</param>
        /// <param name="targetScores">Output list of target scores.</param>
        /// <param name="decoyScores">Output list of decoy scores.</param>
        /// <param name="secondBestScores">Output list of false target scores.</param>
        /// <param name="replaceUnknownFeatureScores">If true, replace NaN and Infinite feature scores with zero</param>
        /// <param name="invertSign">Either 1 or -1 to multiply the final scores by</param>
        public void GetScores(LinearModelParams scoringParams, LinearModelParams calculatorParams,
            List<double> targetScores, List<double> decoyScores, List<double> secondBestScores,
            bool replaceUnknownFeatureScores,
            int invertSign)
        {
            foreach (var peakTransitionGroupFeatures in _peakTransitionGroupFeaturesList.Features)
            {
                PeakGroupFeatures? maxFeatures = null;
                PeakGroupFeatures? nextFeatures = null;
                double maxScore = Double.MinValue;
                double nextScore = Double.MinValue;

                // No peaks in this transition group record
                if (peakTransitionGroupFeatures.PeakGroupFeatures.Count == 0)
                    continue;

                // Find the highest and second highest scores among the transitions in this group.
                foreach (var peakGroupFeatures in peakTransitionGroupFeatures.PeakGroupFeatures)
                {
                    double score = invertSign * GetScore(scoringParams, peakGroupFeatures, replaceUnknownFeatureScores);
                    if (maxScore < score)
                    {
                        nextScore = maxScore;
                        maxScore = score;
                        nextFeatures = maxFeatures;
                        maxFeatures = peakGroupFeatures;
                    }
                    else if(nextScore < score)
                    {
                        nextScore = score;
                        nextFeatures = peakGroupFeatures;
                    }
                }

                double currentScore = maxFeatures.HasValue
                    ? GetScore(calculatorParams, maxFeatures.Value, replaceUnknownFeatureScores) : Double.NaN;
                if (peakTransitionGroupFeatures.IsDecoy)
                {
                    if (decoyScores != null)
                        decoyScores.Add(currentScore);
                }
                else
                {
                    targetScores.Add(currentScore);
                    // Skip if only one peak
                    if (peakTransitionGroupFeatures.PeakGroupFeatures.Count == 1)
                        continue;
                    if (secondBestScores != null)
                    {
                        double secondBestScore = nextFeatures.HasValue
                            ? GetScore(calculatorParams, nextFeatures.Value, replaceUnknownFeatureScores) : Double.NaN;
                        secondBestScores.Add(secondBestScore);
                    }
                }
            }
        }

        public PeakCalculatorWeight[] GetPeakCalculatorWeights(IPeakScoringModel peakScoringModel, IProgressMonitor progressMonitor)
        {
            IProgressStatus status = new ProgressStatus(Resources.EditPeakScoringModelDlg_TrainModel_Calculating_score_contributions);
            var peakCalculatorWeights = new PeakCalculatorWeight[peakScoringModel.PeakFeatureCalculators.Count];
            int seenContributingScores = 0;
            int totalContributingScores = peakScoringModel.IsTrained
                ? peakScoringModel.Parameters.Weights.Count(w => !double.IsNaN(w))
                : 1;    // Should never get used, but just in case, avoid divide by zero
            ParallelEx.For(0, peakCalculatorWeights.Length, i =>
            {
                bool isNanWeight = !peakScoringModel.IsTrained ||
                                   double.IsNaN(peakScoringModel.Parameters.Weights[i]);

                double? weight = null, normalWeight = null;
                if (!isNanWeight)
                {
                    progressMonitor.UpdateProgress(status = status.ChangePercentComplete(seenContributingScores*100/totalContributingScores));
                    weight = peakScoringModel.Parameters.Weights[i];
                    normalWeight = GetPercentContribution(peakScoringModel, i);
                    Interlocked.Increment(ref seenContributingScores);
                    progressMonitor.UpdateProgress(status = status.ChangePercentComplete(seenContributingScores * 100 / totalContributingScores));
                }
                // If the score is not eligible (e.g. has unknown values), definitely don't enable it
                // If it is eligible, enable if untrained or if trained and not nan
                bool enabled = EligibleScores[i] &&
                               (!peakScoringModel.IsTrained || !double.IsNaN(peakScoringModel.Parameters.Weights[i]));
                peakCalculatorWeights[i] = new PeakCalculatorWeight(peakScoringModel.PeakFeatureCalculators[i], weight, normalWeight, enabled);
            });
            return peakCalculatorWeights;
        }

        public double? GetPercentContribution(IPeakScoringModel peakScoringModel, int index)
        {
            if (double.IsNaN(peakScoringModel.Parameters.Weights[index]))
                return null;
            List<double> targetScores;
            List<double> activeDecoyScores;
            List<double> targetScoresAll;
            List<double> activeDecoyScoresAll;
            var scoringParameters = peakScoringModel.Parameters;
            var calculatorParameters = CreateParametersSelect(peakScoringModel, index);
            GetActiveScoredValues(peakScoringModel, scoringParameters, calculatorParameters, out targetScores, out activeDecoyScores);
            GetActiveScoredValues(peakScoringModel, scoringParameters, scoringParameters, out targetScoresAll, out activeDecoyScoresAll);
            if (targetScores.Count == 0 ||
                activeDecoyScores.Count == 0 ||
                targetScoresAll.Count == 0 ||
                activeDecoyScoresAll.Count == 0)
            {
                return null;
            }
            double meanDiffAll = targetScoresAll.Average() - activeDecoyScoresAll.Average();
            double meanDiff = targetScores.Average() - activeDecoyScores.Average();
            double meanWeightedDiff = meanDiff * peakScoringModel.Parameters.Weights[index];
            if (meanDiffAll == 0 || double.IsNaN(meanDiffAll) || double.IsNaN(meanDiff))
                return null;
            return meanWeightedDiff / meanDiffAll;
        }

        private void GetActiveScoredValues(IPeakScoringModel peakScoringModel,
            LinearModelParams scoringParams,
            LinearModelParams calculatorParams,
            out List<double> targetScores,
            out List<double> activeDecoyScores)
        {
            targetScores = new List<double>(TargetCount);
            List<double> decoyScores = peakScoringModel.UsesDecoys ? new List<double>(DecoyCount) : null;
            List<double> secondBestScores = peakScoringModel.UsesSecondBest ? new List<double>(TargetCount) : null;

            GetScores(scoringParams, calculatorParams, targetScores, decoyScores, secondBestScores, ReplaceUnknownFeatureScores, 1);

            if (peakScoringModel.UsesDecoys && !peakScoringModel.UsesSecondBest)
                activeDecoyScores = decoyScores;
            else if (peakScoringModel.UsesSecondBest && !peakScoringModel.UsesDecoys)
                activeDecoyScores = secondBestScores;
            else
            {
                activeDecoyScores = new List<double>();
                if (decoyScores != null)
                    activeDecoyScores.AddRange(decoyScores);
                if (secondBestScores != null)
                    activeDecoyScores.AddRange(secondBestScores);
            }
        }

        /// <summary>
        /// Create parameter object in which the given index will have a value of 1, all the others
        /// will have a value of NaN, and the bias is zero.
        /// </summary>
        public static LinearModelParams CreateParametersSelect(IPeakScoringModel _peakScoringModel, int index)
        {
            var weights = new double[_peakScoringModel.PeakFeatureCalculators.Count];
            for (int i = 0; i < weights.Length; i++)
                weights[i] = double.NaN;
            weights[index] = 1;
            return new LinearModelParams(weights);
        }

        /// <summary>
        ///  Is the specified calculator valid for this dataset (has no unknown values and not all the same value)?
        /// </summary>
        private bool IsValidCalculator(int calculatorIndex)
        {
            if (ReplaceUnknownFeatureScores)
            {
                return true;
            }
            double maxValue = Double.MinValue;
            double minValue = Double.MaxValue;

            foreach (var peakTransitionGroupFeatures in _peakTransitionGroupFeaturesList.Features)
            {
                // Find the highest and second highest scores among the transitions in this group.
                foreach (var peakGroupFeatures in peakTransitionGroupFeatures.PeakGroupFeatures)
                {
                    double value = peakGroupFeatures.Features[calculatorIndex];
                    if (IsUnknown(value))
                    {
                        return false;
                    }
                    maxValue = Math.Max(value, maxValue);
                    minValue = Math.Min(value, minValue);
                }
            }
            return maxValue > minValue;
        }

        private double GetScore(LinearModelParams parameters, PeakGroupFeatures peakGroupFeatures, bool replaceUnknownFeatureScores)
        {
            IList<float> features = peakGroupFeatures.Features;
            if (replaceUnknownFeatureScores)
            {
                features = LinearModelParams.ReplaceUnknownFeatureScores(features);
            }
            return parameters.Score(features);
        }

        public static bool IsUnknown(double d)
        {
            return (double.IsNaN(d) || double.IsInfinity(d));
        }
    }

    /// <summary>
    /// Associate a weight value with a calculator for display in the grid.
    /// </summary>
    public class PeakCalculatorWeight
    {
        public string Name
        {
            get { return Calculator.Name; }
        }
        public IPeakFeatureCalculator Calculator { get; private set; }
        public double? Weight { get; set; }
        public double? PercentContribution { get; set; }
        public bool IsEnabled { get; set; }

        public PeakCalculatorWeight(IPeakFeatureCalculator calculator, double? weight, double? percentContribution, bool enabled)
        {
            Calculator = calculator;
            Weight = weight;
            PercentContribution = percentContribution;
            IsEnabled = enabled;
        }
    }
}