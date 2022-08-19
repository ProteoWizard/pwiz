/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Text;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class PeakGroupScore
    {
        public PeakGroupScore(FeatureScores scores, double? modelScore, double? qValue, IDictionary<FeatureKey, WeightedFeature> weightedFeatures)
        {
            Features = new Features(scores);
            WeightedFeatures = weightedFeatures;
            ModelScore = modelScore;
            PeakQValue = qValue;
        }

        public Features Features
        {
            get;
        }
        [Format(Formats.PEAK_SCORE, NullValue = TextUtil.EXCEL_NA)]
        public double? ModelScore { get; }
        [Format(Formats.PValue, NullValue = TextUtil.EXCEL_NA)]
        public double? PeakQValue { get; }

        [OneToMany(ItemDisplayName = "WeightedFeature")]
        public IDictionary<FeatureKey, WeightedFeature> WeightedFeatures { get; }

        public static PeakGroupScore MakePeakScores(FeatureScores featureScores, PeakScoringModelSpec model, ScoreQValueMap scoreQValueMap)
        {
            var weightedFeatures = new Dictionary<FeatureKey, WeightedFeature>();
            double? modelScore = 0;
            double? qValue = null;
            if (model?.Parameters != null)
            {
                Assume.AreEqual(model.PeakFeatureCalculators.Count, model.Parameters.Weights.Count);
                for (int i = 0; i < model.Parameters.Weights.Count; i++)
                {
                    var weight = model.Parameters.Weights[i];
                    if (double.IsNaN(weight) || weight == 0)
                    {
                        continue;
                    }
                    var featureCalc = model.PeakFeatureCalculators[i];
                    float? score = featureScores.GetFeature(featureCalc);
                    if (!score.HasValue && model is LegacyScoringModel)
                    {
                        score = 0;
                    }

                    var weightedFeature = new WeightedFeature(score, weight);
                    modelScore += weightedFeature.WeightedScore;
                    weightedFeatures[FeatureKey.FromCalculator(featureCalc)] = weightedFeature;
                }
                qValue = scoreQValueMap.GetQValue(modelScore + model.Parameters.Bias);
            }
            return new PeakGroupScore(featureScores, modelScore, qValue, weightedFeatures);
        }

        public override string ToString()
        {
            if (ModelScore.HasValue)
            {
                return ModelScore.Value.ToString(Formats.PEAK_SCORE);
            }

            return string.Empty;
        }
    }

    public class Features : IFeatureScores, IFormattable
    {
        private FeatureScores _scores;
        public Features(FeatureScores scores)
        {
            _scores = scores;
        }

        public float? GetFeature(IPeakFeatureCalculator calculator)
        {
            return _scores.GetFeature(calculator.GetType());
        }

        public override string ToString()
        {
            return _scores.ToString(null, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return _scores.ToString(format, formatProvider);
        }
    }

    [InvariantDisplayName(nameof(WeightedFeature))]
    public class WeightedFeature
    {
        public WeightedFeature(float? score, double weight)
        {
            Score = score;
            Weight = weight;
        }
        [Format(Formats.PEAK_SCORE)]
        public float? Score { get;  }
        [Format(Formats.PEAK_SCORE)]
        public double Weight { get; }
        [Format(Formats.PEAK_SCORE)]
        public double? WeightedScore
        {
            get { return Score * Weight; }
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (Score.HasValue)
            {
                stringBuilder.Append(Score.Value.ToString(Formats.PEAK_SCORE, CultureInfo.CurrentCulture));
            }

            if (Weight != 0)
            {
                stringBuilder.Append(@"x");
                stringBuilder.Append(Weight.ToString(Formats.PEAK_SCORE, CultureInfo.CurrentCulture));
                if (WeightedScore.HasValue)
                {
                    stringBuilder.Append(@"=");
                    stringBuilder.Append(WeightedScore.Value.ToString(Formats.PEAK_SCORE, CultureInfo.CurrentCulture));
                }
            }

            return stringBuilder.ToString();
        }
    }

    public struct FeatureKey : IComparable<FeatureKey>
    {
        private string _calculatorName;
        public static FeatureKey FromCalculator(IPeakFeatureCalculator calculator)
        {
            return new FeatureKey {_calculatorName = calculator.FullyQualifiedName};
        }

        public static FeatureKey FromCalculatorType(Type type)
        {
            return new FeatureKey {_calculatorName = type.FullName};
        }

        public override string ToString()
        {
            var calculator = FeatureNames.CalculatorFromTypeName(_calculatorName);
            return calculator.Name ?? _calculatorName;
        }

        public int CompareTo(FeatureKey other)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(ToString(), other.ToString());
        }
    }
}
