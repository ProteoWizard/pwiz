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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{

    /// <summary>
    /// This is an implementation of the MProphet peak scoring algorithm
    /// described in http://www.nature.com/nmeth/journal/v8/n5/full/nmeth.1584.html.
    /// </summary>
    [XmlRoot(@"mprophet_peak_scoring_model")]
    public class MProphetPeakScoringModel : PeakScoringModelSpec
    {
        public const string NAME = "mProphet";  // Proper name not localized

        private FeatureCalculators _peakFeatureCalculators;

        // Number of iterations to run.  Most weight values will converge within this number of iterations.
        private const int MAX_ITERATIONS = 10;
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
            FeatureCalculators peakFeatureCalculators = null,
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
            FeatureCalculators peakFeatureCalculators = null,
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

        public MProphetPeakScoringModel(string name, SrmDocument document = null)
            : base(name)
        {
            SetPeakFeatureCalculators(GetDefaultCalculators(document));
            Lambda = DEFAULT_R_LAMBDA;   // Default from R
            DoValidate();
        }

        public static FeatureCalculators GetDefaultCalculators(SrmDocument document)
        {
            IEnumerable<IPeakFeatureCalculator> calcs = DEFAULT_CALCULATORS;
            if (document != null)
            {
                if (!document.Settings.PeptideSettings.Modifications.HasHeavyImplicitModifications)
                {
                    calcs = calcs.Where(calc => !calc.IsReferenceScore);
                }
                if (!document.Settings.TransitionSettings.FullScan.IsEnabledMs)
                {
                    calcs = calcs.Where(calc => !calc.IsMs1Score);
                }
                if (document.Settings.DocumentRetentionTimes.IsEmpty)
                {
                    calcs = calcs.Where(calc => !(calc is LegacyIdentifiedCountCalc));
                }
            }
            return FeatureCalculators.FromCalculators(calcs);
        }

        private static readonly FeatureCalculators DEFAULT_CALCULATORS = new FeatureCalculators(new IPeakFeatureCalculator[]
        {
            // Intensity, retention time, library dotp
            new MQuestIntensityCalc(),
            new MQuestRetentionTimePredictionCalc(), 
            // new MQuestRetentionTimeSquaredPredictionCalc(), // somewhat redundant with RT prediction and can lead to strange effects with a positive coefficient and large deltas
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
        });

        protected MProphetPeakScoringModel()
        {
            Lambda = DEFAULT_R_LAMBDA;   // Default from R
        }

        public double? Lambda { get; private set; }
        public bool ColinearWarning { get; private set; }

        public override FeatureCalculators PeakFeatureCalculators
        {
            get { return _peakFeatureCalculators; }
        }

        private void SetPeakFeatureCalculators(FeatureCalculators peakFeatureCalculators)
        {
            _peakFeatureCalculators = peakFeatureCalculators;
        }

        public static readonly IList<double> DEFAULT_CUTOFFS = new[] {0.15, 0.02, 0.01};

        /// <summary>
        /// Train the model by iterative calculating weights to separate target and decoy transition groups.
        /// </summary>
        /// <param name="targetsIn">Target transition groups.</param>
        /// <param name="decoysIn">Decoy transition groups.</param>
        /// <param name="targetDecoyGenerator">Target decoy generator used to calculate contribution percentages</param>
        /// <param name="initParameters">Initial model parameters (weights and bias)</param>
        /// <param name="cutoffs">A list of q value cutoffs used in the training</param>
        /// <param name="iterations">Optional specific number of iterations to use in training</param>
        /// <param name="includeSecondBest">Include the second best peaks in the targets as decoys?</param>
        /// <param name="preTrain">Use a pre-trained model to bootstrap the learning.</param>
        /// <param name="progressMonitor">Used to report progress to the calling context</param>
        /// <param name="documentPath">The path to the current document for writing score distributions</param>
        /// <returns>Immutable model with new weights.</returns>
        public override IPeakScoringModel Train(IList<IList<FeatureScores>> targetsIn,
                                                IList<IList<FeatureScores>> decoysIn,
                                                TargetDecoyGenerator targetDecoyGenerator,
                                                LinearModelParams initParameters,
                                                IList<double> cutoffs,
                                                int? iterations = null,
                                                bool includeSecondBest = false,
                                                bool preTrain = true,
                                                IProgressMonitor progressMonitor = null,
                                                string documentPath = null)
        {
            if (cutoffs == null)
                cutoffs = DEFAULT_CUTOFFS;
            if (initParameters == null)
                initParameters = new LinearModelParams(_peakFeatureCalculators.Count);
            return ChangeProp(ImClone(this), im =>
            {
                // This may take a long time between progress updates, but just measure progress by cycles through the training
                IProgressStatus status = new ProgressStatus(Resources.MProphetPeakScoringModel_Train_Training_peak_scoring_model);
                progressMonitor?.UpdateProgress(status);

                try
                {
                    TrainInternal(im, targetsIn, decoysIn, targetDecoyGenerator, initParameters, cutoffs, iterations,
                        includeSecondBest, preTrain, progressMonitor, status, documentPath);
                }
                finally
                {
                    progressMonitor?.UpdateProgress(status.Complete());
                }
            });
        }

        private void TrainInternal(MProphetPeakScoringModel im, IList<IList<FeatureScores>> targetsIn, IList<IList<FeatureScores>> decoysIn,
            TargetDecoyGenerator targetDecoyGenerator, LinearModelParams initParameters, IList<double> cutoffs,
            int? iterations, bool includeSecondBest, bool preTrain, IProgressMonitor progressMonitor, IProgressStatus status, string documentPath)
        {
            var targets = targetsIn.Where(list => list.Count > 0);
            var decoys = decoysIn.Where(list => list.Count > 0);
            var targetTransitionGroups = new ScoredGroupPeaksSet(targets, targetsIn.Count);
            var decoyTransitionGroups = new ScoredGroupPeaksSet(decoys, decoysIn.Count);
            // Iteratively refine the weights through multiple iterations.
            var calcWeights = new double[initParameters.Weights.Count];
            Array.Copy(initParameters.Weights.ToArray(), calcWeights, initParameters.Weights.Count);
            int lastCutoffIndex = cutoffs.Count - 1;
            int firstCutoffIndex = Math.Min(1, lastCutoffIndex);
            double qValueCutoff;
            // Start with scores calculated from the initial weights
            if (!preTrain)
            {
                qValueCutoff = 0.01; // First iteration cut-off - if not pretraining, just start at 0.01
                targetTransitionGroups.ScorePeaks(calcWeights, ReplaceUnknownFeatureScores);
                decoyTransitionGroups.ScorePeaks(calcWeights, ReplaceUnknownFeatureScores);
            }
            // Bootstrap from the pre-trained legacy model
            else
            {
                qValueCutoff = cutoffs[0];
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
                for (int i = 0; i < calculators.Count; ++i)
                {
                    if (calculators[i].GetType() == typeof(MQuestRetentionTimePredictionCalc))
                        continue;
                    SetCalculatorValue(calculators[i].GetType(), LegacyScoringModel.DEFAULT_WEIGHTS[i], preTrainedWeights);
                }
                targetTransitionGroups.ScorePeaks(preTrainedWeights, ReplaceUnknownFeatureScores);
                decoyTransitionGroups.ScorePeaks(preTrainedWeights, ReplaceUnknownFeatureScores);
            }

            double decoyMean = 0;
            double decoyStdev = 0;
            bool colinearWarning = false;
            int cutoffIndex = firstCutoffIndex;
            int iterationCount = iterations ?? MAX_ITERATIONS;
            int truePeaksCount = 0;
            var lastWeights = new double[calcWeights.Length];
            for (int i = 0; i < iterationCount; i++)
            {
                int percentComplete = 0;
                double decoyMeanNew, decoyStdevNew;
                bool colinearWarningNew = colinearWarning;
                int truePeaksCountNew = im.CalculateWeights(documentPath,
                    targetTransitionGroups,
                    decoyTransitionGroups,
                    includeSecondBest,
                    i == 0, // Use non-parametric q values for first round, when normality assumption may not hold
                    qValueCutoff,
                    calcWeights,
                    out decoyMeanNew,
                    out decoyStdevNew,
                    ref colinearWarningNew);

                if (progressMonitor != null)
                {
                    if (progressMonitor.IsCanceled)
                        throw new OperationCanceledException();

                    // Calculate progress, but wait to make sure convergence has not occurred before setting it
                    string formatText = qValueCutoff > 0.02
                        ? Resources.MProphetPeakScoringModel_Train_Training_scoring_model__iteration__0__of__1__
                        : Resources.MProphetPeakScoringModel_Train_Training_scoring_model__iteration__0__of__1_____2______peaks_at__3_0_____FDR_;
                    percentComplete = (i + 1) * 100 / (iterationCount + 1);
                    status = status.ChangeMessage(string.Format(formatText, i + 1, iterationCount, truePeaksCountNew, qValueCutoff))
                        .ChangePercentComplete(percentComplete);
                }

                if (qValueCutoff > cutoffs[firstCutoffIndex])
                {
                    // Tighten the q value cut-off for "truth" to 2% FDR
                    qValueCutoff = cutoffs[firstCutoffIndex];
                    // And allow the true peaks count to go down in the next iteration
                    // Though it rarely will
                    truePeaksCountNew = 0;
                }
                // Decided in 2018 that equal should be counted as converging, since otherwise training can just get stuck,
                // and go to full iteration count without progressing
                else if (truePeaksCountNew <= truePeaksCount)
                {
                    // Advance looking for a smaller cutoff
                    while (cutoffIndex < cutoffs.Count && cutoffs[cutoffIndex] >= qValueCutoff)
                        cutoffIndex++;
                    // The model has leveled off enough to begin losing discriminant value
                    if (cutoffIndex < cutoffs.Count)
                    {
                        // Tighten the q value cut-off for "truth" to 1% FDR
                        qValueCutoff = cutoffs[cutoffIndex];
                        // And allow the true peaks count to go down in the next iteration
                        truePeaksCountNew = 0;
                    }
                    else
                    {
                        progressMonitor?.UpdateProgress(status =
                            status.ChangeMessage(string.Format(Resources.MProphetPeakScoringModel_Train_Scoring_model_converged__iteration__0_____1______peaks_at__2_0_____FDR_, i + 1, truePeaksCount, qValueCutoff))
                                .ChangePercentComplete(Math.Max(95, percentComplete)));
                        calcWeights = lastWeights;
                        break;
                    }
                }
                truePeaksCount = truePeaksCountNew;
                Array.Copy(calcWeights, lastWeights, calcWeights.Length);
                decoyMean = decoyMeanNew;
                decoyStdev = decoyStdevNew;
                colinearWarning = colinearWarningNew;

                progressMonitor?.UpdateProgress(status);
            }

            var parameters = new LinearModelParams(calcWeights);
            parameters = parameters.RescaleParameters(decoyMean, decoyStdev);
            im.Parameters = parameters;
            im.ColinearWarning = colinearWarning;
            im.UsesSecondBest = includeSecondBest;
            im.UsesDecoys = decoysIn.Count > 0;
            im.Parameters = parameters.CalculatePercentContributions(im, targetDecoyGenerator);
        }

        private int GetEnabledCount(FeatureCalculators featureCalculators, IList<double> weights)
        {
            int enabledCount = 0;
            foreach (var calculator in featureCalculators)
            {
                var calculatorType = calculator.GetType();
                int indexWeight = PeakFeatureCalculators.IndexOf(calculatorType);
                if (indexWeight != -1 && !double.IsNaN(weights[indexWeight]))
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
            int indexStandardIntensity = PeakFeatureCalculators.IndexOf(calculatorType);
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
        /// <param name="documentPath">The path to the current document for writing score distributions</param>
        /// <param name="targetTransitionGroups">Target transition groups.</param>
        /// <param name="decoyTransitionGroups">Decoy transition groups.</param>
        /// <param name="includeSecondBest">Include the second best peaks in the targets as additional decoys?</param>
        /// <param name="nonParametricPValues">Non-parametric p values used in selecting true peaks if true</param>
        /// <param name="qValueCutoff">The q value cut-off for true peaks in the training</param>
        /// <param name="weights">Array of weights per calculator.</param>
        /// <param name="decoyMean">Output mean of decoy transition groups.</param>
        /// <param name="decoyStdev">Output standard deviation of decoy transition groups.</param>
        /// <param name="colinearWarning">Set to true if colinearity was detected.</param>
        private int CalculateWeights(string documentPath,
            ScoredGroupPeaksSet targetTransitionGroups,
            ScoredGroupPeaksSet decoyTransitionGroups,
            bool includeSecondBest,
            bool nonParametricPValues,
            double qValueCutoff,
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
            var truePeaks = targetTransitionGroups.SelectTruePeaks(decoyTransitionGroups, qValueCutoff, Lambda, nonParametricPValues);
            var decoyPeaks = decoyTransitionGroups.SelectMaxPeaks();

            WriteDistributionInfo(documentPath, targetTransitionGroups, decoyTransitionGroups); // Only if asked to do so in command-line arguments

            // Better to let a really poor model through for the user to see than to give an error message here
            if (((double)truePeaks.Count)*10*1000 < decoyPeaks.Count) // Targets must be at least 0.01% of decoys (still rejects zero)
                throw new InvalidDataException(string.Format(Resources.MProphetPeakScoringModel_CalculateWeights_Insufficient_target_peaks___0__with__1__decoys__detected_at__2___FDR_to_continue_training_, truePeaks.Count, decoyPeaks.Count, qValueCutoff*100));
            if (((double)decoyPeaks.Count)*1000 < truePeaks.Count) // Decoys must be at least 0.1% of targets
                throw new InvalidDataException(string.Format(Resources.MProphetPeakScoringModel_CalculateWeights_Insufficient_decoy_peaks___0__with__1__targets__to_continue_training_, decoyPeaks.Count, truePeaks.Count));

            var featureCount = weights.Count(w => !double.IsNaN(w));

            // Copy target and decoy peaks to training data array.
            int totalTrainingPeaks = truePeaks.Count + decoyTransitionGroups.Count;
            // Calculate the maximum number of training peaks (8 bytes per score - double, featurCount + 1 scores per peak)
            int maxTrainingPeaks = MAX_TRAINING_MEMORY/8/(featureCount + 1);

            var trainData = new double[Math.Min(totalTrainingPeaks, maxTrainingPeaks), featureCount + 1];
            if (totalTrainingPeaks < maxTrainingPeaks)
            {
                for (int i = 0; i < truePeaks.Count; i++)
                    CopyToTrainData(truePeaks[i].FeatureScores, trainData, weights, i, 1);
                for (int i = 0; i < decoyPeaks.Count; i++)
                    CopyToTrainData(decoyPeaks[i].FeatureScores, trainData, weights, i + truePeaks.Count, 0);
            }
            else
            {
                double proportionTrue = truePeaks.Count*1.0/totalTrainingPeaks;
                int truePeakCount = (int) Math.Round(maxTrainingPeaks*proportionTrue);
                int i = 0;
                foreach (var peak in truePeaks.RandomOrder(ArrayUtil.RANDOM_SEED))
                {
                    if (i < truePeakCount)
                        CopyToTrainData(peak.FeatureScores, trainData, weights, i, 1);
                    else
                        break;
                    i++;
                }
                int decoyPeakCount = maxTrainingPeaks - truePeakCount;
                i = 0;
                foreach (var peak in decoyPeaks.RandomOrder(ArrayUtil.RANDOM_SEED))
                {
                    if (i < decoyPeakCount)
                        CopyToTrainData(peak.FeatureScores, trainData, weights, i + truePeakCount, 0);
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
            targetTransitionGroups.ScorePeaks(weights, ReplaceUnknownFeatureScores);
            decoyTransitionGroups.ScorePeaks(weights, ReplaceUnknownFeatureScores);

            // If the mean target score is less than the mean decoy score, then the
            // weights came out negative, and all the weights and scores must be negated to
            // restore the proper ordering.
            if (targetTransitionGroups.Mean < decoyTransitionGroups.Mean)
            {
                for (int i = 0; i < weights.Length; i++)
                    weights[i] *= -1;
                targetTransitionGroups.ScorePeaks(weights, ReplaceUnknownFeatureScores);
                decoyTransitionGroups.ScorePeaks(weights, ReplaceUnknownFeatureScores);
            }

            decoyMean = decoyTransitionGroups.Mean;
            decoyStdev = decoyTransitionGroups.Stdev;
            return truePeaks.Count;
        }

        private void WriteDistributionInfo(string documentPath, ScoredGroupPeaksSet targetTransitionGroups, ScoredGroupPeaksSet decoyTransitionGroups)
        {
            string documentDir = Path.GetDirectoryName(documentPath);
            if (documentDir != null)
            {
                string distBase = Helpers.GetUniqueName(Path.Combine(documentDir, @"dist1"),
                    value => !File.Exists(value + @"Targets.txt"));
                targetTransitionGroups.WriteBest(distBase + @"Targets.txt");
                decoyTransitionGroups.WriteBest(distBase + @"Decoys.txt");
            }
        }

        /// <summary>
        /// Copy peak features and category to training data array in preparation
        /// for analysis using LDA.
        /// </summary>
        public void CopyToTrainData(FeatureScores features, double[,] trainData, double[] weights, int row, int category)
        {
            int j = 0;
            for (int i = 0; i < features.Count; i++)
            {
                if (!double.IsNaN(weights[i]))
                    trainData[row, j++] = features.Values[i];
            }
            trainData[row, j] = category;
        }
        public override bool ReplaceUnknownFeatureScores => false;

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
            SetPeakFeatureCalculators(new FeatureCalculators(peakFeatureCalculators));
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
