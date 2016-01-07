/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{

    /// <summary>
    /// This is an implementation of the MProphet peak scoring algorithm
    /// described in http://www.nature.com/nmeth/journal/v8/n5/full/nmeth.1584.html.
    /// </summary>
    [XmlRoot("mprophet_peak_scoring_model")] // Not L10N
    public class MProphetPeakScoringModel : PeakScoringModelSpec
    {
        public const string NAME = "mProphet";  // Not L10N : Proper name not localized

        private ImmutableList<IPeakFeatureCalculator> _peakFeatureCalculators;

        // Number of iterations to run.  Most weight values will converge within this number of iterations.
        private const int MAX_ITERATIONS = 30;
        public const double DEFAULT_R_LAMBDA = 0.4;    // Lambda for pi-zero from original R mProphet

        /// <summary>
        ///  Minimum possible value of PI_ZERO.  If PI_ZERO is allowed to be arbitrarily low, it
        /// will sometimes be zero, causing all q values to be assigned to zero.  In practice, we should
        /// always assume a baseline rate of nulls.
        /// </summary>
        public const double PI_ZERO_MIN = 0.05;

        public MProphetPeakScoringModel(
            string name, 
            LinearModelParams parameters,
            IList<IPeakFeatureCalculator> peakFeatureCalculators = null,
            bool usesDecoys = false,
            bool usesSecondBest = false,
            bool colinearWarning = false)
            : base(name)
        {
            SetPeakFeatureCalculators(peakFeatureCalculators ?? DEFAULT_CALCULATORS);
            Parameters = parameters;
            UsesDecoys = usesDecoys;
            UsesSecondBest = usesSecondBest;
            ColinearWarning = colinearWarning;
            Lambda = DEFAULT_R_LAMBDA;   // Default from R
            DoValidate();
        }

        public MProphetPeakScoringModel(
            string name,
            IList<double> weights,
            IList<IPeakFeatureCalculator> peakFeatureCalculators = null,
            bool usesDecoys = false,
            bool usesSecondBest = false,
            bool colinearWarning = false,
            double bias = 0)
            : this(name,
                   new LinearModelParams(weights, bias),
                   peakFeatureCalculators,
                   usesDecoys,
                   usesSecondBest,
                   colinearWarning)
        {
        }

        public MProphetPeakScoringModel(string name)
            : base(name)
        {
            SetPeakFeatureCalculators(DEFAULT_CALCULATORS);
            Lambda = DEFAULT_R_LAMBDA;   // Default from R
            DoValidate();
        }

        private static readonly IPeakFeatureCalculator[] DEFAULT_CALCULATORS =
        {
            // Intensity, retention time, library dotp
            new MQuestIntensityCalc(),
            new MQuestRetentionTimePredictionCalc(), 
            new MQuestRetentionTimeSquaredPredictionCalc(),
            new MQuestIntensityCorrelationCalc(), 

            // Shape-based and related calculators
            new MQuestWeightedShapeCalc(), 
            new MQuestWeightedCoElutionCalc(), 
            new LegacyUnforcedCountScoreCalc(),
            new NextGenSignalNoiseCalc(),
            new NextGenProductMassErrorCalc(),

            // Reference standard cross-calculators
            new MQuestReferenceCorrelationCalc(),
            new MQuestWeightedReferenceShapeCalc(), 
            new MQuestWeightedReferenceCoElutionCalc(),
            new LegacyUnforcedCountScoreStandardCalc(),

            // Reference standard self-calculators
            new MQuestStandardIntensityCalc(), 
            new MQuestStandardIntensityCorrelationCalc(),
            new NextGenStandardSignalNoiseCalc(),
            new NextGenStandardProductMassErrorCalc(),
            new MQuestStandardWeightedShapeCalc(),
            new MQuestStandardWeightedCoElutionCalc(), 

            // Precursor calculators
            new NextGenCrossWeightedShapeCalc(),
            new NextGenPrecursorMassErrorCalc(),
            new NextGenIsotopeDotProductCalc(),
            new LegacyIdentifiedCountCalc(),
        };

        protected MProphetPeakScoringModel()
        {
            Lambda = DEFAULT_R_LAMBDA;   // Default from R
        }

        public double? Lambda { get; private set; }
        public bool ColinearWarning { get; private set; }

        public override IList<IPeakFeatureCalculator> PeakFeatureCalculators
        {
            get { return _peakFeatureCalculators; }
        }

        private void SetPeakFeatureCalculators(IList<IPeakFeatureCalculator> peakFeatureCalculators)
        {
            _peakFeatureCalculators = MakeReadOnly(peakFeatureCalculators);
        }

        /// <summary>
        /// Train the model by iterative calculating weights to separate target and decoy transition groups.
        /// </summary>
        /// <param name="targets">Target transition groups.</param>
        /// <param name="decoys">Decoy transition groups.</param>
        /// <param name="initParameters">Initial model parameters (weights and bias)</param>
        /// <param name="includeSecondBest"> Include the second best peaks in the targets as decoys?</param>
        /// <param name="preTrain">Use a pre-trained model to bootstrap the learning.</param>
        /// <param name="progressMonitor"></param>
        /// <returns>Immutable model with new weights.</returns>
        public override IPeakScoringModel Train(IList<IList<float[]>> targets, IList<IList<float[]>> decoys, LinearModelParams initParameters,
            bool includeSecondBest = false, bool preTrain = true, IProgressMonitor progressMonitor = null)
        {
            if(initParameters == null)
                initParameters = new LinearModelParams(_peakFeatureCalculators.Count);
            return ChangeProp(ImClone(this), im =>
                {
                    targets = targets.Where(list => list.Count > 0).ToList();
                    decoys = decoys.Where(list => list.Count > 0).ToList();
                    var targetTransitionGroups = new ScoredGroupPeaksSet(targets);
                    var decoyTransitionGroups = new ScoredGroupPeaksSet(decoys);
                    // Bootstrap from the pre-trained legacy model
                    if (preTrain)
                    {
                        var preTrainedWeights = new double[initParameters.Weights.Count];
                        for (int i = 0; i < preTrainedWeights.Length; ++i)
                        {
                            if (double.IsNaN(initParameters.Weights[i]))
                            {
                                preTrainedWeights[i] = double.NaN;
                            }
                        }
                        int standardEnabledCount = GetEnabledCount(LegacyScoringModel.StandardFeatureCalculators, initParameters.Weights);
                        int analyteEnabledCount = GetEnabledCount(LegacyScoringModel.AnalyteFeatureCalculators, initParameters.Weights);
                        bool hasStandards = standardEnabledCount >= analyteEnabledCount;
                        var calculators = hasStandards ? LegacyScoringModel.StandardFeatureCalculators : LegacyScoringModel.AnalyteFeatureCalculators;
                        for (int i = 0; i < calculators.Length; ++i)
                        {
                            if (calculators[i].GetType() == typeof (MQuestRetentionTimePredictionCalc))
                                continue;
                            SetCalculatorValue(calculators[i].GetType(), LegacyScoringModel.DEFAULT_WEIGHTS[i], preTrainedWeights);
                        }
                        targetTransitionGroups.ScorePeaks(preTrainedWeights);
                        decoyTransitionGroups.ScorePeaks(preTrainedWeights);
                    }

                    // Iteratively refine the weights through multiple iterations.
                    var calcWeights = new double[initParameters.Weights.Count];
                    Array.Copy(initParameters.Weights.ToArray(), calcWeights, initParameters.Weights.Count);
                    double decoyMean = 0;
                    double decoyStdev = 0;
                    bool colinearWarning = false;
                    // This may take a long time between progress updates, but just measure progress by cycles through the training
                    var status = new ProgressStatus(Resources.MProphetPeakScoringModel_Train_Training_peak_scoring_model);
                    if (progressMonitor != null)
                        progressMonitor.UpdateProgress(status);
                    for (int iteration = 0; iteration < MAX_ITERATIONS; iteration++)
                    {
                        if (progressMonitor != null)
                        {
                            if (progressMonitor.IsCanceled)
                                throw new OperationCanceledException();

                            progressMonitor.UpdateProgress(status =
                                status.ChangeMessage(string.Format(Resources.MProphetPeakScoringModel_Train_Training_peak_scoring_model__iteration__0__of__1__, iteration + 1, MAX_ITERATIONS))
                                      .ChangePercentComplete((iteration + 1) * 100 / (MAX_ITERATIONS + 1)));
                        }

                        im.CalculateWeights(iteration, targetTransitionGroups, decoyTransitionGroups,
                                            includeSecondBest, calcWeights, out decoyMean, out decoyStdev, ref colinearWarning);

                        GC.Collect();   // Each loop generates a number of large objects. GC helps to keep private bytes under control
                    }
                    if (progressMonitor != null)
                        progressMonitor.UpdateProgress(status.ChangePercentComplete(100));

                    var parameters = new LinearModelParams(calcWeights);
                    parameters = parameters.RescaleParameters(decoyMean, decoyStdev);
                    im.Parameters = parameters;
                    im.ColinearWarning = colinearWarning;
                    im.UsesSecondBest = includeSecondBest;
                    im.UsesDecoys = decoys.Count > 0;
                });
        }

        private int GetEnabledCount(IPeakFeatureCalculator[] featureCalculators, IList<double> weights)
        {
            int enabledCount = 0;
            foreach (var calculator in featureCalculators)
            {
                var calculatorType = calculator.GetType();
                int indexWeight = PeakFeatureCalculators.IndexOf(calc => calc.GetType() == calculatorType);
                if (!double.IsNaN(weights[indexWeight]))
                    enabledCount++;
            }
            return enabledCount;
        }

        /// <summary>
        /// Searches for a calculator type in the PeakFeatureCalculators and sets it if finds it and if
        /// that calculator is currently active
        /// </summary>
        /// <param name="calculatorType">Type of the calculator to be found</param>
        /// <param name="setValue">Value to which to set the calculator</param>
        /// <param name="weightsToSet">Weight array whose values are to be set</param>
        private void SetCalculatorValue(Type calculatorType, double setValue, double[] weightsToSet)
        {
            int indexStandardIntensity = PeakFeatureCalculators.IndexOf(calc => calc.GetType() == calculatorType);
            if (indexStandardIntensity != -1 && !double.IsNaN(weightsToSet[indexStandardIntensity]))
            {
                weightsToSet[indexStandardIntensity] = setValue;
            }
        }

        private const int MAX_TRAINING_MEMORY = 512*1024*1024; // 512 MB

        /// <summary>
        /// Calculate new weight factors for one iteration of the refinement process.  This is the heart
        /// of the MProphet algorithm.
        /// </summary>
        /// <param name="iteration">Iteration number (special processing happens for iteration 0).</param>
        /// <param name="targetTransitionGroups">Target transition groups.</param>
        /// <param name="decoyTransitionGroups">Decoy transition groups.</param>
        /// <param name="includeSecondBest">Include the second best peaks in the targets as additional decoys?</param>
        /// <param name="weights">Array of weights per calculator.</param>
        /// <param name="decoyMean">Output mean of decoy transition groups.</param>
        /// <param name="decoyStdev">Output standard deviation of decoy transition groups.</param>
        /// <param name="colinearWarning">Set to true if colinearity was detected.</param>
        private void CalculateWeights(
            int iteration,
            ScoredGroupPeaksSet targetTransitionGroups,
            ScoredGroupPeaksSet decoyTransitionGroups,
            bool includeSecondBest,
            double[] weights,
            out double decoyMean,
            out double decoyStdev,
            ref bool colinearWarning)
        {
            if (includeSecondBest)
            {
                ScoredGroupPeaksSet secondBestTransitionGroups;
                targetTransitionGroups.SelectTargetsAndDecoys(out targetTransitionGroups, out secondBestTransitionGroups);
                foreach (var secondBestGroup in secondBestTransitionGroups.ScoredGroupPeaksList)
                {
                    decoyTransitionGroups.Add(secondBestGroup);
                }
                
            }

            // Select true target peaks using a q-value cutoff filter.
            var qValueCutoff = (iteration == 0 ? 0.15 : 0.02);
            var truePeaks = targetTransitionGroups.SelectTruePeaks(qValueCutoff, Lambda, decoyTransitionGroups);
            var decoyPeaks = decoyTransitionGroups.SelectMaxPeaks();

            // Omit first feature during first iteration, since it is used as the initial score value.
            weights[0] = (iteration == 0) ? double.NaN : 0;
            var featureCount = weights.Count(w => !double.IsNaN(w));

            // Copy target and decoy peaks to training data array.
            int totalTrainingPeaks = truePeaks.Count + decoyTransitionGroups.Count;
            // Calculate the maximum number of training peaks (8 bytes per score - double, featurCount + 1 scores per peak)
            int maxTrainingPeaks = MAX_TRAINING_MEMORY/8/(featureCount + 1);

            var trainData = new double[Math.Min(totalTrainingPeaks, maxTrainingPeaks), featureCount + 1];
            if (totalTrainingPeaks < maxTrainingPeaks)
            {
                for (int i = 0; i < truePeaks.Count; i++)
                    CopyToTrainData(truePeaks[i].Features, trainData, weights, i, 1);
                for (int i = 0; i < decoyPeaks.Count; i++)
                    CopyToTrainData(decoyPeaks[i].Features, trainData, weights, i + truePeaks.Count, 0);
            }
            else
            {
                double proportionTrue = truePeaks.Count*1.0/totalTrainingPeaks;
                int truePeakCount = (int) Math.Round(maxTrainingPeaks*proportionTrue);
                int i = 0;
                foreach (var peak in truePeaks.RandomOrder())
                {
                    if (i < truePeakCount)
                        CopyToTrainData(peak.Features, trainData, weights, i, 1);
                    else
                        break;
                    i++;
                }
                int decoyPeakCount = maxTrainingPeaks - truePeakCount;
                i = 0;
                foreach (var peak in decoyPeaks.RandomOrder())
                {
                    if (i < decoyPeakCount)
                        CopyToTrainData(peak.Features, trainData, weights, i + truePeakCount, 0);
                    else
                        break;
                    i++;
                }
            }

            // Use Linear Discriminant Analysis to find weights that separate true and decoy peak scores.
            int info;
            double[] weightsFromLda;
            alglib.fisherlda(
                trainData,
                trainData.GetLength(0),
                trainData.GetLength(1) - 1,
                2,
                out info,
                out weightsFromLda);

            // Check for colinearity.
            if (info == 2)
            {
                colinearWarning = true;
            }

            // Unpack weights array.
            for (int i = 0, j = 0; i < weights.Length; i++)
            {
                if (!double.IsNaN(weights[i]))
                    weights[i] = weightsFromLda[j++];
            }

            // Recalculate all peak scores.
            targetTransitionGroups.ScorePeaks(weights);
            decoyTransitionGroups.ScorePeaks(weights);
            
            // If the mean target score is less than the mean decoy score, then the
            // weights came out negative, and all the weights and scores must be negated to
            // restore the proper ordering.
            if (targetTransitionGroups.Mean < decoyTransitionGroups.Mean)
            {
                for (int i = 0; i < weights.Length; i++)
                    weights[i] *= -1;
                targetTransitionGroups.ScorePeaks(weights);
                decoyTransitionGroups.ScorePeaks(weights);
            }

            decoyMean = decoyTransitionGroups.Mean;
            decoyStdev = decoyTransitionGroups.Stdev;
        }


        /// <summary>
        /// Copy peak features and category to training data array in preparation
        /// for analysis using LDA.
        /// </summary>
        public void CopyToTrainData(float[] features, double[,] trainData, double[] weights, int row, int category)
        {
            int j = 0;
            for (int i = 0; i < features.Length; i++)
            {
                if (!double.IsNaN(weights[i]))
                    trainData[row, j++] = features[i];
            }
            trainData[row, j] = category;
        }

        #region object overrides
        public bool Equals(MProphetPeakScoringModel other)
        {
            return (base.Equals(other) &&
                    ColinearWarning.Equals(other.ColinearWarning) &&
                    Lambda.Equals(other.Lambda));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MProphetPeakScoringModel)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ ColinearWarning.GetHashCode();
                hashCode = (hashCode * 397) ^ Lambda.GetHashCode();
                hashCode = (hashCode * 397) ^ (PeakFeatureCalculators != null ? PeakFeatureCalculators.GetHashCode() : 0);
                return hashCode;
            }
        }
        #endregion

        #region Implementation of IXmlSerializable
        private enum ATTR
        {
            // Model
            colinear_warning,
            uses_decoys,
            uses_false_targets,
            bias
        }

        public static MProphetPeakScoringModel Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new MProphetPeakScoringModel());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            ColinearWarning = reader.GetBoolAttribute(ATTR.colinear_warning);
            // Earlier versions always used decoys only
            UsesDecoys = reader.GetBoolAttribute(ATTR.uses_decoys, true);
            UsesSecondBest = reader.GetBoolAttribute(ATTR.uses_false_targets);
            double bias = reader.GetDoubleAttribute(ATTR.bias);

            // Consume tag
            reader.Read();

            // Read calculators
            var calculators = new List<FeatureCalculator>();
            reader.ReadElements(calculators);
            var peakFeatureCalculators = new List<IPeakFeatureCalculator>(calculators.Count);
            var weights = new double[calculators.Count];
            for (int i = 0; i < calculators.Count; i++)
            {
                weights[i] = calculators[i].Weight;
                peakFeatureCalculators.Add(PeakFeatureCalculator.GetCalculator(calculators[i].Type));
            }
            SetPeakFeatureCalculators(peakFeatureCalculators);
            Parameters = new LinearModelParams(weights, bias);

            reader.ReadEndElement();

            DoValidate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.colinear_warning, ColinearWarning);
            writer.WriteAttribute(ATTR.uses_decoys, UsesDecoys, true);
            writer.WriteAttribute(ATTR.uses_false_targets, UsesSecondBest);
            if (null != Parameters)
            {
                writer.WriteAttribute(ATTR.bias, Parameters.Bias);

                // Write calculators
                var calculators = new List<FeatureCalculator>(PeakFeatureCalculators.Count);
                for (int i = 0; i < PeakFeatureCalculators.Count; i++)
                    calculators.Add(new FeatureCalculator(PeakFeatureCalculators[i].GetType(), Parameters.Weights[i]));
                writer.WriteElements(calculators);
            }
        }

        public override void Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if (Parameters != null && Parameters.Weights != null && Parameters.Weights.Count < 1 || PeakFeatureCalculators.Count < 1)
                throw new InvalidDataException(Resources.MProphetPeakScoringModel_DoValidate_MProphetPeakScoringModel_requires_at_least_one_peak_feature_calculator_with_a_weight_value);
        }
        #endregion
    }
}
