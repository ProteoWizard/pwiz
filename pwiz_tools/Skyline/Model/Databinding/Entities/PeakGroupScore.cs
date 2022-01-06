using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        public PeakGroupScore(FeatureValues scores, double? modelScore, IDictionary<string, WeightedFeature> weightedFeatures)
        {
            Features = new Features(scores);
            WeightedFeatures = weightedFeatures;
            ModelScore = modelScore;
        }

        public Features Features
        {
            get;
        }
        [Format(Formats.PEAK_SCORE, NullValue = TextUtil.EXCEL_NA)]
        public double? ModelScore { get; } 
        [OneToMany(ItemDisplayName = "WeightedFeature")]
        public IDictionary<string, WeightedFeature> WeightedFeatures { get; }

        public static PrecursorCandidatePeakScores MakePeakScores(FeatureValues featureValues, PeakScoringModelSpec model)
        {
            var weightedFeatures = new Dictionary<string, WeightedFeature>();
            double? modelScore = 0;
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
                    float? score = featureValues.GetValue(featureCalc);
                    if (!score.HasValue && model is LegacyScoringModel)
                    {
                        score = 0;
                    }

                    var weightedFeature = new WeightedFeature(score, weight);
                    modelScore += weightedFeature.WeightedScore;
                    weightedFeatures[featureCalc.HeaderName] = weightedFeature;
                }
            }

            return new PrecursorCandidatePeakScores(featureValues, modelScore, weightedFeatures);
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
        private FeatureValues _scores;
        public Features(FeatureValues scores)
        {
            _scores = scores;
        }

        public float? GetFeature(IPeakFeatureCalculator calculator)
        {
            return _scores.GetValue(calculator.GetType());
        }

        public override string ToString()
        {
            return ToString(null, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            var parts = new List<string>();
            foreach (var calc in _scores.Calculators.OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var value = GetFeature(calc);
                if (value.HasValue)
                {
                    parts.Add(calc.Name + @":" + value.Value.ToString(format, formatProvider));
                }
            }

            return new FormattableList<string>(parts).ToString(format, formatProvider);
        }
    }

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
}
