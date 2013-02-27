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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
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
        // Weighting values applied to each feature in each peak to determine the peak score.
        public double[] Weights { get; private set; }
        public double DecoyMean { get; private set; }
        public double DecoyStdev { get; private set; }
        public double? Lambda { get; private set; }

        // Number of iterations to run.  Most weight values will converge within this number of iterations.
        private const int MAX_ITERATIONS = 7;

        public MProphetPeakScoringModel(string name, IList<Type> peakFeatureCalculators) : base(name)
        {
            SetPeakFeatureCalculators(peakFeatureCalculators);
            Lambda = 0.4;   // Default from R
            DoValidate();
        }

        public MProphetPeakScoringModel(string name, IList<Type> peakFeatureCalculators,
            double[] weights, double decoyMean, double decoyStdev) : this(name, peakFeatureCalculators)
        {
            Weights = weights;
            DecoyMean = decoyMean;
            DecoyStdev = decoyStdev;
        }

        public MProphetPeakScoringModel ChangeLambda(double? prop)
        {
            return ChangeProp(ImClone(this), im => im.Lambda = prop);            
        }

        private ReadOnlyCollection<Type> _peakFeatureCalculators; 

        public override IList<Type> PeakFeatureCalculators
        {
            get { return _peakFeatureCalculators; }
        }

        private void SetPeakFeatureCalculators(IList<Type> peakFeatureCalculators)
        {
            _peakFeatureCalculators = MakeReadOnly(peakFeatureCalculators);
            Weights = new double[peakFeatureCalculators.Count];
        }

        /// <summary>
        /// A peak with features and a score.  The score is calculated as
        /// the dot product of the features with weight values that are
        /// produced in successive iterations of the algorithm.
        /// </summary>
        public class Peak
        {
            public double[] Features { get; private set; }
            public double Score { get; set; }

            /// <summary>
            /// Construct a peak with the given feature values.  By default, the
            /// initial score is the simply the value of the first feature in
            /// the array.
            /// </summary>
            /// <param name="features">Array of feature values.</param>
            public Peak(double[] features)
            {
                Features = features;
                Score = Features[0];    // Use the first feature as the initial score.
            }

            /// <summary>
            /// Copy peak features and category to training data array in preparation
            /// for analysis using LDA.
            /// </summary>
            /// <param name="trainData">Training data array.</param>
            /// <param name="row">Which row in trainData to copy to.</param>
            /// <param name="sourceOffset">Which feature to start copying from.</param>
            /// <param name="category">Peak category.</param>
            public void CopyToTrainData(double[,] trainData, int row, int sourceOffset, int category)
            {
                for (int i = 0; i < Features.Length - sourceOffset; i++)
                    trainData[row, i] = Features[i + sourceOffset];
                trainData[row, Features.Length - sourceOffset] = category;
            }

            // for debugging...
            public override string ToString()
            {
                return string.Format("{0:0.00}", Score);
            }
        }

        /// <summary>
        /// A transition group of peaks.
        /// </summary>
        public class TransitionGroup
        {
            public string Id { get; set; }    // used for debugging
            public List<Peak> Peaks { get; private set; }

            public TransitionGroup()
            {
                Peaks = new List<Peak>();
            }

            /// <summary>
            /// Add a peak.
            /// </summary>
            /// <param name="peak">Peak to add to this group.</param>
            public void Add(Peak peak)
            {
                Peaks.Add(peak);
            }

            /// <summary>
            /// Find the peak with the maximum score.
            /// </summary>
            public Peak MaxPeak
            {
                get
                {
                    var maxPeak = Peaks[0];
                    var maxScore = maxPeak.Score;
                    for (int i = 1; i < Peaks.Count; i++)
                    {
                        var peak = Peaks[i];
                        if (maxScore < peak.Score)
                        {
                            maxScore = peak.Score;
                            maxPeak = peak;
                        }
                    }
                    return maxPeak;
                }
            }

            #region Functional Test Support
            /// <summary>
            /// Return a list of peak feature values.
            /// </summary>
            /// <returns></returns>
            public List<double[]> ToList()
            {
                return Peaks.Select(peak => peak.Features).ToList();
            }

            public override string ToString()
            {
                return string.Format("{0}: {1:0.00}", Id, MaxPeak.Score);
            }
            #endregion
        }

        /// <summary>
        /// A collection of transition groups.
        /// </summary>
        public class TransitionGroups
        {
            public double Mean { get; private set; }
            public double Stdev { get; private set; }

            private List<TransitionGroup> _transitionGroups = new List<TransitionGroup>();

            public int Count
            {
                get { return _transitionGroups.Count; }
            }

            public void Add(TransitionGroup transitionGroup)
            {
                _transitionGroups.Add(transitionGroup);
            }

            /// <summary>
            /// Discard half the transition groups (usually to save them for testing).  If a random
            /// number generator is specified, the discarded groups are chosen randomly.  Otherwise,
            /// the groups are sorted alphabetically by id, and the last half are discarded.
            /// </summary>
            /// <param name="random">Optional random number generator to discard random groups.</param>
            public void DiscardHalf(Random random = null)
            {
                var newCount = (int)Math.Round(Count / 2.0, MidpointRounding.ToEven);    // Emulate R rounding.

                if (random == null)
                {
                    // Sort by transition group name, then discard upper half of list.
                    _transitionGroups = new List<TransitionGroup>(_transitionGroups.OrderBy(key => key.Id));
                    _transitionGroups.RemoveRange(newCount, Count - newCount);
                }
                else
                {
                    // Randomly select half the transition groups to keep.
                    var oldTransitionGroups = _transitionGroups;
                    _transitionGroups = new List<TransitionGroup>(newCount);
                    for (int i = 0; i < newCount; i++)
                    {
                        var randomIndex = random.Next(oldTransitionGroups.Count);
                        _transitionGroups.Add(oldTransitionGroups[randomIndex]);
                        oldTransitionGroups.RemoveAt(randomIndex);
                    }
                }
            }

            /// <summary>
            /// Return a list of peaks, where each peak has the maximum score in its transition group,
            /// and its q-value is less than the cutoff value.
            /// </summary>
            /// <param name="qValueCutoff">Cutoff q-value.</param>
            /// <param name="lambda">Optional p-value cutoff for calculating Pi-zero.</param>
            /// <param name="decoyTransitionGroups">Decoy transition groups.</param>
            /// <returns>List of peaks the meet the criteria.</returns>
            public List<Peak> SelectTruePeaks(double qValueCutoff, double? lambda, TransitionGroups decoyTransitionGroups)
            {
                // Get max peak score for each transition group.
                var targetScores = GetMaxScores();
                var decoyScores = decoyTransitionGroups.GetMaxScores();

                // Calculate statistics for each set of scores.
                var statDecoys = new Statistics(decoyScores);
                var statTarget = new Statistics(targetScores);

                // Calculate q values from decoy set.
                var pvalues = statDecoys.PvaluesNorm(statTarget);
                var qvalues = new Statistics(pvalues).Qvalues(lambda);

                // Select max peak with q value less than the cutoff from each target group.
                var truePeaks = new List<Peak>(_transitionGroups.Count);
                for (int i = 0; i < _transitionGroups.Count; i++)
                {
                    if (qvalues[i] <= qValueCutoff)
                        truePeaks.Add(_transitionGroups[i].MaxPeak);
                }
                return truePeaks;
            }

            /// <summary>
            /// Return a list of peaks that have the highest score in each transition group.
            /// </summary>
            /// <returns>List of highest scoring peaks.</returns>
            public List<Peak> SelectMaxPeaks()
            {
                return _transitionGroups.Select(t => t.MaxPeak).ToList();
            }

            public Peak FirstPeak
            {
                get { return _transitionGroups[0].Peaks[0]; }
            }

            private double[] GetMaxScores()
            {
                var maxScores = new double[_transitionGroups.Count];
                for (int i = 0; i < maxScores.Length; i++)
                    maxScores[i] = _transitionGroups[i].MaxPeak.Score;
                return maxScores;
            }

            /// <summary>
            /// Recalculate the scores of each peak by applying the given feature weighting factors.
            /// </summary>
            /// <param name="weights">Array of weight factors applied to each feature.</param>
            /// <param name="omitFeature">Optional feature index to omit from scoring.</param>
            /// <returns>Mean peak score.</returns>
            public void ScorePeaks(double[] weights, int omitFeature)
            {
                var count = 0;
                var total = 0.0;
                foreach (var peak in _transitionGroups.SelectMany(transitionGroup => transitionGroup.Peaks))
                {
                    peak.Score = 0;
                    for (int i = 0; i < omitFeature; i++)
                    {
                        peak.Score += weights[i] * peak.Features[i];
                    }
                    for (int i = omitFeature; i < weights.Length; i++)
                    {
                        peak.Score += weights[i] * peak.Features[i + 1];
                    }

                    // To calculate mean peak score...
                    count++;
                    total += peak.Score;
                }

                // Calculate mean peak score.
                Mean = total / count;

                // Calculate standard deviation.
                total = 0.0;
                foreach (var peak in _transitionGroups.SelectMany(transitionGroup => transitionGroup.Peaks))
                {
                    var deviation = peak.Score - Mean;
                    total += deviation*deviation;
                }
                Stdev = Math.Sqrt(total/(count - 1));
            }

            #region Functional Test Support
            /// <summary>
            /// Return a list of transition groups, each containing peak feature values.
            /// </summary>
            /// <returns></returns>
            public List<IList<double[]>> ToList()
            {
                var list = new List<IList<double[]>>(_transitionGroups.Count);
                foreach (var transitionGroup in _transitionGroups)
                    list.Add(transitionGroup.ToList());
                return list;
            }
            #endregion
        }

        /// <summary>
        /// Train the model by iterative calculating weights to separate target and decoy transition groups.
        /// </summary>
        /// <param name="targets">Target transition groups.</param>
        /// <param name="decoys">Decoy transition groups.</param>
        /// <returns>Immutable model with new weights.</returns>
        public override IPeakScoringModel Train(IList<IList<double[]>> targets, IList<IList<double[]>> decoys)
        {
            var targetTransitionGroups = CreateTransitionGroups(targets);
            var decoyTransitionGroups = CreateTransitionGroups(decoys);

            // Iteratively refine the weights through multiple iterations.
            double[] weights = null;
            for (int iteration = 0; iteration < MAX_ITERATIONS; iteration++)
            {
                weights = CalculateWeights(iteration, targetTransitionGroups, decoyTransitionGroups);
            }

            return new MProphetPeakScoringModel(Name, PeakFeatureCalculators)
            {
                Weights = weights,
                DecoyMean = decoyTransitionGroups.Mean,
                DecoyStdev = decoyTransitionGroups.Stdev
            };
        }

        private TransitionGroups CreateTransitionGroups(IEnumerable<IList<double[]>> groupList)
        {
            var transitionGroups = new TransitionGroups();
            foreach (var group in groupList)
            {
                var transitionGroup = new TransitionGroup();
                transitionGroups.Add(transitionGroup);
                foreach (var features in group)
                {
                    if (features.Length != PeakFeatureCalculators.Count)
                        throw new InvalidDataException(
                            string.Format(Resources.MProphetPeakScoringModel_CreateTransitionGroups_MProphetScoringModel_was_given_a_peak_with__0__features__but_it_has__1__peak_feature_calculators,
                            features.Length, PeakFeatureCalculators.Count));
                    transitionGroup.Add(new Peak(features));
                }
            }
            return transitionGroups;
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
        /// <returns>A new set of feature weights.</returns>
        private double[] CalculateWeights(
            int iteration,
            TransitionGroups targetTransitionGroups,
            TransitionGroups decoyTransitionGroups)
        {
            double[] weights;

            // Select true target peaks using a q-value cutoff filter.
            var qValueCutoff = (iteration == 0 ? 0.15 : 0.02);
            var truePeaks = targetTransitionGroups.SelectTruePeaks(qValueCutoff, Lambda, decoyTransitionGroups);
            var decoyPeaks = decoyTransitionGroups.SelectMaxPeaks();
            var featureCount = targetTransitionGroups.FirstPeak.Features.Length;

            // Omit first feature during first iteration, since it is used as the initial score value.
            var offset = (iteration == 0) ? 1 : 0;

            // Copy target and decoy peaks to training data array.
            var trainData =
                new double[truePeaks.Count + decoyTransitionGroups.Count, featureCount + 1 - offset];
            for (int i = 0; i < truePeaks.Count; i++)
                truePeaks[i].CopyToTrainData(trainData, i, offset, 1);
            for (int i = 0; i < decoyPeaks.Count; i++)
                decoyPeaks[i].CopyToTrainData(trainData, i + truePeaks.Count, offset, 0);

            // Use Linear Discriminant Analysis to find weights that separate true and decoy peak scores.
            int info;
            alglib.fisherlda(
                trainData,
                trainData.GetLength(0),
                trainData.GetLength(1) - 1,
                2,
                out info,
                out weights);

            // Check for collinearity.
            if (info == 2)
            {
                // Ignore collinearity warnings for now.
// ReSharper disable RedundantAssignment
                info = 1;   // Just a handy place to put a breakpoint...
// ReSharper restore RedundantAssignment
            }

            // Recalculate all peak scores.
            var omitFeature = (iteration == 0) ? 0 : featureCount;
            targetTransitionGroups.ScorePeaks(weights, omitFeature);
            decoyTransitionGroups.ScorePeaks(weights, omitFeature);
            
            // If the mean target score is less than the mean decoy score, then the
            // weights came out negative, and all the weights and scores must be negated to
            // restore the proper ordering.
            if (targetTransitionGroups.Mean < decoyTransitionGroups.Mean)
            {
                for (int i = 0; i < weights.Length; i++)
                    weights[i] *= -1;
                targetTransitionGroups.ScorePeaks(weights, omitFeature);
                decoyTransitionGroups.ScorePeaks(weights, omitFeature);
            }

            return weights;
        }

        protected bool Equals(MProphetPeakScoringModel other)
        {
            return 
                base.Equals(other) &&
                Weights.SequenceEqual(other.Weights) &&
                DecoyMean.Equals(other.DecoyMean) &&
                DecoyStdev.Equals(other.DecoyStdev) &&
                Lambda.Equals(other.Lambda) &&
                PeakFeatureCalculators.SequenceEqual(other.PeakFeatureCalculators);
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
                hashCode = (hashCode * 397) ^ Lambda.GetHashCode();
                hashCode = (hashCode * 397) ^ (PeakFeatureCalculators != null ? PeakFeatureCalculators.GetHashCode() : 0);
                return hashCode;
            }
        }

        #region XmlNamedElement

        [XmlRoot("peak_feature_calculator")] // Not L10N
        public class FeatureCalculator : IXmlSerializable
        {
            public FeatureCalculator(Type type, double weight)
            {
                Type = type;
                Weight = weight;

                Validate();
            }

            public Type Type { get; private set; }
            public double Weight { get; private set; }

            #region Implementation of IXmlSerializable

            /// <summary>
            /// For serialization
            /// </summary>
            protected FeatureCalculator()
            {
            }

            private enum ATTR2
            {
                type,
                weight
            }

            private void Validate()
            {
                if (Type == null)
                    throw new InvalidDataException();
            }

// ReSharper disable MemberHidesStaticFromOuterClass
            public static FeatureCalculator Deserialize(XmlReader reader)
// ReSharper restore MemberHidesStaticFromOuterClass
            {
                return reader.Deserialize(new FeatureCalculator());
            }

            public XmlSchema GetSchema()
            {
                return null;
            }

            public void ReadXml(XmlReader reader)
            {
                // Read tag attributes
                Type = reader.GetTypeAttribute(ATTR2.type);
                Weight = reader.GetDoubleAttribute(ATTR2.weight);

                // Consume tag
                reader.Read();

                Validate();
            }

            public void WriteXml(XmlWriter writer)
            {
                // Write tag attributes
                writer.WriteAttribute(ATTR2.type, Type);
                writer.WriteAttribute(ATTR2.weight, Weight);
            }

            #endregion

            #region object overrides

            public bool Equals(FeatureCalculator obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj.Type == Type && Equals(obj.Weight, Weight);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != typeof(FeatureCalculator)) return false;
                return Equals((FeatureCalculator)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Type.GetHashCode() * 397) ^ Weight.GetHashCode();
                }
            }

            #endregion
        }

        /// <summary>
        /// For serialization
        /// </summary>
        protected MProphetPeakScoringModel()
        {
        }

        private enum ATTR
        {
            // Model
            decoy_mean,
            decoy_stdev
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

            // Consume tag
            reader.Read();

            // Read calculators
            var calculators = new List<FeatureCalculator>();
            reader.ReadElements(calculators);
            var peakFeatureCalculators = new List<Type>(calculators.Count);
            var weights = new double[calculators.Count];
            for (int i = 0; i < calculators.Count; i++)
            {
                weights[i] = calculators[i].Weight;
                peakFeatureCalculators.Add(calculators[i].Type);
            }
            SetPeakFeatureCalculators(peakFeatureCalculators);
            Weights = weights;

            DoValidate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.decoy_mean, DecoyMean);
            writer.WriteAttribute(ATTR.decoy_stdev, DecoyStdev);

            // Write calculators
            var calculators = new List<FeatureCalculator>(PeakFeatureCalculators.Count);
            for (int i = 0; i < PeakFeatureCalculators.Count; i++)
                calculators.Add(new FeatureCalculator(PeakFeatureCalculators[i], Weights[i]));
            writer.WriteElements(calculators);
        }

        #endregion

        public override void Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if (Weights.Length < 1 || PeakFeatureCalculators.Count < 1)
                throw new InvalidDataException(Resources.MProphetPeakScoringModel_DoValidate_MProphetPeakScoringModel_requires_at_least_one_peak_feature_calculator_with_a_weight_value);

            foreach (var type in PeakFeatureCalculators)
            {
                if (type.GetInterface("pwiz.Skyline.Model.Results.Scoring.IPeakFeatureCalculator") == null) // not L10N
                    throw new InvalidDataException(string.Format(Resources.MProphetPeakScoringModel_DoValidate_MProphetScoringModel_has_a_peak_feature_calculator_of_type__0__which_does_not_implement_IPeakFeatureCalculator, type));
            }
        }
    }
}
