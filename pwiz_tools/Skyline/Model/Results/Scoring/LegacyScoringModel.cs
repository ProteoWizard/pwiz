/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{
    [XmlRoot(@"legacy_peak_scoring_model")]
    public class LegacyScoringModel : PeakScoringModelSpec
    {
        public static string DEFAULT_NAME { get { return Resources.LegacyScoringModel_DEFAULT_NAME_Default; } } 

        public static readonly double[] DEFAULT_WEIGHTS = {W0, W1, W2, W3, W4, W5, W6};

        public static LinearModelParams DEFAULT_PARAMS { get { return new LinearModelParams(DEFAULT_WEIGHTS); } }

        public static readonly LegacyScoringModel DEFAULT_MODEL = new LegacyScoringModel(DEFAULT_NAME, DEFAULT_PARAMS);

        // Special placeholder model to use as the default.  It's "untrained", so it will never be persisted, but when
        // it needs to be used, "DEFAULT_MODEL" gets used instead.
        public static readonly LegacyScoringModel DEFAULT_UNTRAINED_MODEL 
            = new LegacyScoringModel(DEFAULT_NAME);

        // Weighting coefficients.
        private const double W0 = 1.0;  // Log intensity
        private const double W1 = 1.0;  // Unforced count score
        private const double W2 = 20.0; // Identified count
        private const double W3 = 3.0; // Library intensity correlation
        private const double W4 = 4.0; // Shape score
        private const double W5 = -0.05; // Co-elution (weighted)
        private const double W6 = -0.7; // Retention time score


        public static double Score(double logUnforcedArea,
                                   double unforcedCountScore,
                                   double unforcedCountScoreStandard,
                                   double identifiedCount)
        {
            return 
                W0*logUnforcedArea + 
                W1*unforcedCountScore + 
                W1*LegacyLogUnforcedAreaCalc.STANDARD_MULTIPLIER *unforcedCountScoreStandard + 
                W2*identifiedCount;
        }

        private FeatureCalculators _calculators;

        public LegacyScoringModel(string name, LinearModelParams parameters = null, bool usesDecoys = true, bool usesSecondBest = false) : base(name)
        {
            SetPeakFeatureCalculators();

            Parameters = parameters;
            UsesDecoys = usesDecoys;
            UsesSecondBest = usesSecondBest;
        }

        public override FeatureCalculators PeakFeatureCalculators
        {
            get { return _calculators; }
        }

        private void SetPeakFeatureCalculators()
        {
            _calculators = new FeatureCalculators(new IPeakFeatureCalculator[]
            {
                new MQuestDefaultIntensityCalc(),
                new LegacyUnforcedCountScoreDefaultCalc(),
                new LegacyIdentifiedCountCalc(),
                new MQuestDefaultIntensityCorrelationCalc(),
                new MQuestDefaultWeightedShapeCalc(),
                new MQuestDefaultWeightedCoElutionCalc(),
                new MQuestRetentionTimePredictionCalc(),
            });
        }

        public static FeatureCalculators AnalyteFeatureCalculators = new FeatureCalculators(new IPeakFeatureCalculator[]
        {
            new MQuestIntensityCalc(),
            new LegacyUnforcedCountScoreCalc(),
            new LegacyIdentifiedCountCalc(),
            new MQuestIntensityCorrelationCalc(),
            new MQuestWeightedShapeCalc(),
            new MQuestWeightedCoElutionCalc(),
            new MQuestRetentionTimePredictionCalc(),
        });

        public static FeatureCalculators StandardFeatureCalculators = new FeatureCalculators(new IPeakFeatureCalculator[]
        {
            new MQuestStandardIntensityCalc(),
            new LegacyUnforcedCountScoreStandardCalc(),
            new LegacyIdentifiedCountCalc(),
            new MQuestStandardIntensityCorrelationCalc(),
            new MQuestStandardWeightedShapeCalc(),
            new MQuestStandardWeightedCoElutionCalc(),
            new MQuestRetentionTimePredictionCalc(),
        });

        public override IPeakScoringModel Train(IList<IList<FeatureScores>> targets, IList<IList<FeatureScores>> decoys, TargetDecoyGenerator targetDecoyGenerator, LinearModelParams initParameters,
            IList<double> cutoffs, int? iterations = null, bool includeSecondBest = false, bool preTrain = true, IProgressMonitor progressMonitor = null, string documentPath = null)
        {
            return ChangeProp(ImClone(this), im =>
            {
                    int nWeights = initParameters.Weights.Count;
                    var weights = new double [nWeights];
                    for (int i = 0; i < initParameters.Weights.Count; ++i)
                    {
                        weights[i] = double.IsNaN(initParameters.Weights[i]) ? double.NaN : DEFAULT_WEIGHTS[i];
                    }
                    var parameters = new LinearModelParams(weights);
                    ScoredGroupPeaksSet decoyTransitionGroups = new ScoredGroupPeaksSet(decoys, decoys.Count);
                    ScoredGroupPeaksSet targetTransitionGroups = new ScoredGroupPeaksSet(targets, targets.Count);
                    targetTransitionGroups.ScorePeaks(parameters.Weights, ReplaceUnknownFeatureScores);

                    if (includeSecondBest)
                    {
                        ScoredGroupPeaksSet secondBestTransitionGroups;
                        targetTransitionGroups.SelectTargetsAndDecoys(out targetTransitionGroups, out secondBestTransitionGroups);
                        foreach (var secondBestGroup in secondBestTransitionGroups.ScoredGroupPeaksList)
                        {
                            decoyTransitionGroups.Add(secondBestGroup);
                        }
                    }
                    decoyTransitionGroups.ScorePeaks(parameters.Weights, ReplaceUnknownFeatureScores);
                    im.UsesDecoys = decoys.Count > 0;
                    im.UsesSecondBest = includeSecondBest;
                    im.Parameters = parameters.RescaleParameters(decoyTransitionGroups.Mean, decoyTransitionGroups.Stdev);
                    im.Parameters = im.Parameters.CalculatePercentContributions(im, targetDecoyGenerator);
            });
        }

        public override bool ReplaceUnknownFeatureScores => true;

        #region object overrides

        // Because LegacyScoringModel has a fixed set of calculators, no equality override is necessary

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        public LegacyScoringModel()
        {
            SetPeakFeatureCalculators();
        }

        private enum ATTR
        {
            // Model
            uses_decoys,
            uses_false_targets,
            bias
        }

        public static LegacyScoringModel Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new LegacyScoringModel());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            // Earlier versions always used decoys only
            UsesDecoys = reader.GetBoolAttribute(ATTR.uses_decoys, true);
            UsesSecondBest = reader.GetBoolAttribute(ATTR.uses_false_targets, false);
            double bias = reader.GetDoubleAttribute(ATTR.bias);

            bool isEmpty = reader.IsEmptyElement;

            // Consume tag
            reader.Read();

            if (!isEmpty)
            {
                // Read calculators
                var calculators = new List<FeatureCalculator>();
                reader.ReadElements(calculators);
                var weights = new double[calculators.Count];
                for (int i = 0; i < calculators.Count; i++)
                {
                    if (calculators[i].Type != PeakFeatureCalculators[i].GetType())
                        throw new InvalidDataException(Resources.LegacyScoringModel_ReadXml_Invalid_legacy_model_);
                    weights[i] = calculators[i].Weight;
                }
                Parameters = new LinearModelParams(weights, bias);

                reader.ReadEndElement();
            }

            DoValidate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);

            if (IsTrained)
            {
                writer.WriteAttribute(ATTR.uses_decoys, UsesDecoys, true);
                writer.WriteAttribute(ATTR.uses_false_targets, UsesSecondBest);
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
            if (!string.Equals(Name, DEFAULT_NAME) && Parameters == null)
                throw new InvalidDataException(Resources.LegacyScoringModel_DoValidate_Legacy_scoring_model_is_not_trained_);
            if (Parameters != null && Parameters.Weights != null && Parameters.Weights.Count < 1)
                throw new InvalidDataException(Resources.MProphetPeakScoringModel_DoValidate_MProphetPeakScoringModel_requires_at_least_one_peak_feature_calculator_with_a_weight_value);
        }

        #endregion
    }
}
