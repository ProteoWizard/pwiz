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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
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
        private ReadOnlyCollection<IPeakFeatureCalculator> _peakFeatureCalculators;
        private ScoredGroupPeaksSet _allTransitionGroups;

        // Number of iterations to run.  Most weight values will converge within this number of iterations.
        private const int MAX_ITERATIONS = 7;

        public MProphetPeakScoringModel(
            string name, 
            IList<double> weights, 
            IList<IPeakFeatureCalculator> peakFeatureCalculators = null,
            double decoyMean = 0, 
            double decoyStdev = 0,
            bool colinearWarning = false)
            : base(name)
        {
            SetPeakFeatureCalculators(peakFeatureCalculators ?? DEFAULT_CALCULATORS);
            Weights = weights;
            DecoyMean = decoyMean;
            DecoyStdev = decoyStdev;
            ColinearWarning = colinearWarning;
            Lambda = 0.4;   // Default from R
            DoValidate();
        }

        public MProphetPeakScoringModel(string name)
            : base(name)
        {
            SetPeakFeatureCalculators(DEFAULT_CALCULATORS);
            Weights = new double[_peakFeatureCalculators.Count];
            DecoyMean = double.NaN;
            DecoyStdev = double.NaN;
            Lambda = 0.4;   // Default from R
            DoValidate();
        }

        private static readonly IPeakFeatureCalculator[] DEFAULT_CALCULATORS = new IPeakFeatureCalculator[]
        {
            new MQuestIntensityCalc(),
            new MQuestRetentionTimePredictionCalc(), 
            new MQuestIntensityCorrelationCalc(), 
            new MQuestReferenceCorrelationCalc(), 

            // Detail feature calculators
            new MQuestWeightedShapeCalc(), 
            new MQuestWeightedCoElutionCalc(), 
            new MQuestWeightedReferenceShapeCalc(), 
            new MQuestWeightedReferenceCoElutionCalc(),

            // Legacy calculators
            new LegacyUnforcedCountScoreCalc(),
            new LegacyUnforcedCountScoreStandardCalc(),
            new LegacyIdentifiedCountCalc()
        };

        protected MProphetPeakScoringModel()
        {
            Lambda = 0.4;   // Default from R
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
        /// <returns>Immutable model with new weights.</returns>
        public override IPeakScoringModel Train(IList<IList<double[]>> targets, IList<IList<double[]>> decoys)
        {
            return ChangeProp(ImClone(this), im =>
                {
                    var targetTransitionGroups = new ScoredGroupPeaksSet(targets);
                    var decoyTransitionGroups = new ScoredGroupPeaksSet(decoys);
                    var allTransitionGroups = decoyTransitionGroups.Count == 0 ? targetTransitionGroups : null;

                    // Iteratively refine the weights through multiple iterations.
                    var weights = im.Weights.ToArray();
                    double decoyMean = 0;
                    double decoyStdev = 0;
                    bool colinearWarning = false;
                    for (int iteration = 0; iteration < MAX_ITERATIONS; iteration++)
                    {
                        im.CalculateWeights(iteration, targetTransitionGroups, decoyTransitionGroups,
                                            allTransitionGroups, weights, out decoyMean, out decoyStdev, ref colinearWarning);
                    }

                    im.Weights = weights;
                    im.DecoyMean = decoyMean;
                    im.DecoyStdev = decoyStdev;
                    im.ColinearWarning = colinearWarning;
                });
        }

        /// <summary>
        /// Compute a score given a new set of features.
        /// </summary>
        /// <param name="features">Array of feature values.</param>
        /// <returns>A score computed from the feature values.</returns>
        public override double Score(double[] features)
        {
            if (features.Length != PeakFeatureCalculators.Count)
                throw new InvalidDataException(
                    string.Format(Resources.MProphetPeakScoringModel_CreateTransitionGroups_MProphetScoringModel_was_given_a_peak_with__0__features__but_it_has__1__peak_feature_calculators,
                    features.Length, PeakFeatureCalculators.Count));
            return features.Select((t, i) => t * Weights[i]).Sum();
        }

        /// <summary>
        /// Calculate new weight factors for one iteration of the refinement process.  This is the heart
        /// of the MProphet algorithm.
        /// </summary>
        /// <param name="iteration">Iteration number (special processing happens for iteration 0).</param>
        /// <param name="targetTransitionGroups">Target transition groups.</param>
        /// <param name="decoyTransitionGroups">Decoy transition groups.</param>
        /// <param name="allTransitionGroups">All transition groups if no decoys are present, otherwise null.</param>
        /// <param name="weights">Array of weights per calculator.</param>
        /// <param name="decoyMean">Output mean of decoy transition groups.</param>
        /// <param name="decoyStdev">Output standard deviation of decoy transition groups.</param>
        /// <param name="colinearWarning">Set to true if colinearity was detected.</param>
        private void CalculateWeights(
            int iteration,
            ScoredGroupPeaksSet targetTransitionGroups,
            ScoredGroupPeaksSet decoyTransitionGroups,
            ScoredGroupPeaksSet allTransitionGroups,
            double[] weights,
            out double decoyMean,
            out double decoyStdev,
            ref bool colinearWarning)
        {
            if (allTransitionGroups != null)
                allTransitionGroups.SelectTargetsAndDecoys(out targetTransitionGroups, out decoyTransitionGroups);

            // Select true target peaks using a q-value cutoff filter.
            var qValueCutoff = (iteration == 0 ? 0.15 : 0.02);
            var truePeaks = targetTransitionGroups.SelectTruePeaks(qValueCutoff, Lambda, decoyTransitionGroups);
            var decoyPeaks = decoyTransitionGroups.SelectMaxPeaks();

            // Omit first feature during first iteration, since it is used as the initial score value.
            weights[0] = (iteration == 0) ? double.NaN : 0;
            var featureCount = weights.Count(w => !double.IsNaN(w));

            // Copy target and decoy peaks to training data array.
            var trainData =
                new double[truePeaks.Count + decoyTransitionGroups.Count, featureCount + 1];
            for (int i = 0; i < truePeaks.Count; i++)
                CopyToTrainData(truePeaks[i].Features, trainData, weights, i, 1);
            for (int i = 0; i < decoyPeaks.Count; i++)
                CopyToTrainData(decoyPeaks[i].Features, trainData, weights, i + truePeaks.Count, 0);

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
        public void CopyToTrainData(double[] features, double[,] trainData, double[] weights, int row, int category)
        {
            int j = 0;
            for (int i = 0; i < features.Length; i++)
            {
                if (!double.IsNaN(weights[i]))
                    trainData[row, j++] = features[i];
            }
            trainData[row, j] = category;
        }

        protected bool Equals(MProphetPeakScoringModel other)
        {
            if (base.Equals(other) &&
                Weights.SequenceEqual(other.Weights) &&
                DecoyMean.Equals(other.DecoyMean) &&
                DecoyStdev.Equals(other.DecoyStdev) &&
                ColinearWarning.Equals(other.ColinearWarning) &&
                Lambda.Equals(other.Lambda) &&
                PeakFeatureCalculators.Count == other.PeakFeatureCalculators.Count)
            {
                for (int i = 0; i < PeakFeatureCalculators.Count; i++)
                {
                    if (PeakFeatureCalculators[i].GetType() != other.PeakFeatureCalculators[i].GetType())
                        return false;
                }
                return true;
            }
            return false;
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
                hashCode = (hashCode * 397) ^ (Weights != null ? Weights.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ DecoyMean.GetHashCode();
                hashCode = (hashCode * 397) ^ DecoyStdev.GetHashCode();
                hashCode = (hashCode * 397) ^ ColinearWarning.GetHashCode();
                hashCode = (hashCode * 397) ^ Lambda.GetHashCode();
                hashCode = (hashCode * 397) ^ (PeakFeatureCalculators != null ? PeakFeatureCalculators.GetHashCode() : 0);
                return hashCode;
            }
        }

        private enum ATTR
        {
            // Model
            decoy_mean,
            decoy_stdev,
            colinear_warning
        }

        public static MProphetPeakScoringModel Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new MProphetPeakScoringModel());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            DecoyMean = reader.GetDoubleAttribute(ATTR.decoy_mean);
            DecoyStdev = reader.GetDoubleAttribute(ATTR.decoy_stdev);
            ColinearWarning = reader.GetBoolAttribute(ATTR.colinear_warning);

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
            Weights = weights;

            reader.ReadEndElement();

            DoValidate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.decoy_mean, DecoyMean);
            writer.WriteAttribute(ATTR.decoy_stdev, DecoyStdev);
            writer.WriteAttribute(ATTR.colinear_warning, ColinearWarning);

            // Write calculators
            var calculators = new List<FeatureCalculator>(PeakFeatureCalculators.Count);
            for (int i = 0; i < PeakFeatureCalculators.Count; i++)
                calculators.Add(new FeatureCalculator(PeakFeatureCalculators[i].GetType(), Weights[i]));
            writer.WriteElements(calculators);
        }

        public override void Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if (Weights.Count < 1 || PeakFeatureCalculators.Count < 1)
                throw new InvalidDataException(Resources.MProphetPeakScoringModel_DoValidate_MProphetPeakScoringModel_requires_at_least_one_peak_feature_calculator_with_a_weight_value);
        }
    }
}
