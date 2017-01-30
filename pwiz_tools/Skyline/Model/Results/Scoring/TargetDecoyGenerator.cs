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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{
    /// <summary>
    /// Class to separate target and decoy peaks, and keep track of disabled calculators.
    /// </summary>
    internal class TargetDecoyGenerator
    {
        public bool[] EligibleScores { get; private set; }

        public IList<IPeakFeatureCalculator> FeatureCalculators { get; private set; }

        private readonly PeakTransitionGroupFeatures[] _peakTransitionGroupFeaturesList;

        public Dictionary<KeyValuePair<int, int>, List<PeakTransitionGroupFeatures>> PeakTransitionGroupDictionary { get; private set; }
        public TargetDecoyGenerator(SrmDocument document, IPeakScoringModel scoringModel, IProgressMonitor progressMonitor = null)
        {
            // Determine which calculators will be used to score peaks in this document.
            FeatureCalculators = scoringModel.PeakFeatureCalculators.ToArray();
            _peakTransitionGroupFeaturesList = document.GetPeakFeatures(FeatureCalculators, progressMonitor);
            PopulateDictionary();

            EligibleScores = new bool[FeatureCalculators.Count];
            // Disable calculators that have only a single score value or any unknown scores.
            for (int i = 0; i < FeatureCalculators.Count; i++)
                EligibleScores[i] = IsValidCalculator(i);
        }

        public IList<PeakTransitionGroupFeatures> PeakGroupFeatures { get { return _peakTransitionGroupFeaturesList; } }

        private void PopulateDictionary()
        {
            PeakTransitionGroupDictionary = new Dictionary<KeyValuePair<int, int>, List<PeakTransitionGroupFeatures>>();
            foreach (var transitionGroupFeatures in _peakTransitionGroupFeaturesList)
            {
                var pepId = transitionGroupFeatures.Id.NodePep.Id.GlobalIndex;
                var fileId = transitionGroupFeatures.Id.ChromatogramSet.FindFile(transitionGroupFeatures.Id.FilePath).GlobalIndex;
                var key = new KeyValuePair<int, int>(pepId, fileId);
                if (PeakTransitionGroupDictionary.ContainsKey(key))
                {
                    PeakTransitionGroupDictionary[key].Add(transitionGroupFeatures);
                }
                else
                {
                    PeakTransitionGroupDictionary.Add(key, new List<PeakTransitionGroupFeatures> { transitionGroupFeatures });
                } 
            }
        }

        public void GetTransitionGroups(out List<IList<float[]>> targetGroups,
            out List<IList<float[]>> decoyGroups)
        {
            targetGroups = new List<IList<float[]>>();
            decoyGroups = new List<IList<float[]>>();

            foreach (var peakTransitionGroupFeatures in _peakTransitionGroupFeaturesList)
            {
                var transitionGroup = new List<float[]>();
                foreach (var peakGroupFeatures in peakTransitionGroupFeatures.PeakGroupFeatures)
                    transitionGroup.Add(peakGroupFeatures.Features);

                if (!transitionGroup.Any())
                    continue;
                if (peakTransitionGroupFeatures.Id.NodePep.IsDecoy)
                    decoyGroups.Add(transitionGroup);
                else
                    targetGroups.Add(transitionGroup);
            }
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
        /// <param name="invert">If true, select minimum rather than maximum scores</param>
        public void GetScores(LinearModelParams scoringParams, LinearModelParams calculatorParams, out List<double> targetScores, out List<double> decoyScores,
            out List<double> secondBestScores, bool invert = false)
        {
            targetScores = new List<double>();
            decoyScores = new List<double>();
            secondBestScores = new List<double>();
            int invertSign = invert ? -1 : 1;

            foreach (var peakTransitionGroupFeatures in _peakTransitionGroupFeaturesList)
            {
                PeakGroupFeatures maxFeatures = null;
                PeakGroupFeatures nextFeatures = null;
                double maxScore = Double.MinValue;
                double nextScore = Double.MinValue;

                // No peaks in this transition group record
                if (peakTransitionGroupFeatures.PeakGroupFeatures.Count == 0)
                    continue;

                // Find the highest and second highest scores among the transitions in this group.
                foreach (var peakGroupFeatures in peakTransitionGroupFeatures.PeakGroupFeatures)
                {
                    double score = invertSign * GetScore(scoringParams, peakGroupFeatures);
                    if (nextScore < score)
                    {
                        if (maxScore < score)
                        {
                            nextScore = maxScore;
                            maxScore = score;
                            nextFeatures = maxFeatures;
                            maxFeatures = peakGroupFeatures;
                        }
                        else
                        {
                            nextScore = score;
                            nextFeatures = peakGroupFeatures;
                        }
                    }
                }

                double currentScore = maxFeatures == null ? Double.NaN : GetScore(calculatorParams, maxFeatures);
                if (peakTransitionGroupFeatures.Id.NodePep.IsDecoy)
                    decoyScores.Add(currentScore);
                else
                {
                    targetScores.Add(currentScore);
                    // Skip if only one peak
                    if (peakTransitionGroupFeatures.PeakGroupFeatures.Count == 1)
                        continue;
                    double secondBestScore = nextFeatures == null ? Double.NaN : GetScore(calculatorParams, nextFeatures);
                    secondBestScores.Add(secondBestScore);
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

                var name = peakScoringModel.PeakFeatureCalculators[i].Name;
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
                peakCalculatorWeights[i] = new PeakCalculatorWeight(name, weight, normalWeight, enabled);
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
            List<double> decoyScores;
            List<double> secondBestScores;
            activeDecoyScores = new List<double>();
            GetScores(scoringParams,
                                calculatorParams,
                                out targetScores,
                                out decoyScores,
                                out secondBestScores);
            if (peakScoringModel.UsesDecoys)
                activeDecoyScores.AddRange(decoyScores);
            if (peakScoringModel.UsesSecondBest)
                activeDecoyScores.AddRange(secondBestScores);
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
            double maxValue = Double.MinValue;
            double minValue = Double.MaxValue;

            foreach (var peakTransitionGroupFeatures in _peakTransitionGroupFeaturesList)
            {
                // Find the highest and second highest scores among the transitions in this group.
                foreach (var peakGroupFeatures in peakTransitionGroupFeatures.PeakGroupFeatures)
                {
                    double value = peakGroupFeatures.Features[calculatorIndex];
                    if (EditPeakScoringModelDlg.IsUnknown(value))
                        return false;
                    maxValue = Math.Max(value, maxValue);
                    minValue = Math.Min(value, minValue);
                }
            }
            return maxValue > minValue;
        }

        /// <summary>
        /// Calculate the score of a set of features given an array of weighting coefficients.
        /// </summary>
        private static double GetScore(IList<double> weights, PeakGroupFeatures peakGroupFeatures, double bias)
        {
            return LinearModelParams.Score(peakGroupFeatures.Features, weights, bias);
        }

        private static double GetScore(LinearModelParams parameters, PeakGroupFeatures peakGroupFeatures)
        {
            return GetScore(parameters.Weights, peakGroupFeatures, parameters.Bias);
        }
    }

    /// <summary>
    /// Associate a weight value with a calculator for display in the grid.
    /// </summary>
    public class PeakCalculatorWeight
    {
        public string Name { get; private set; }
        public double? Weight { get; set; }
        public double? PercentContribution { get; set; }
        public bool IsEnabled { get; set; }

        public PeakCalculatorWeight(string name, double? weight, double? percentContribution, bool enabled)
        {
            Name = name;
            Weight = weight;
            PercentContribution = percentContribution;
            IsEnabled = enabled;
        }
    }
}