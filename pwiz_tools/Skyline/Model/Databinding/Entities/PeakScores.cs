using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class AbstractPeakScores
    {
        protected IDictionary<Type, float> _scores;

        protected AbstractPeakScores(IDictionary<Type, float> scores)
        {
            _scores = scores;
        }

        protected float? GetScore<T>() where T : IPeakFeatureCalculator
        {
            if (_scores.TryGetValue(typeof(T), out float value))
            {
                return value;
            }

            return null;

        }
    }

    public class DefaultFeatures : AbstractPeakScores
    {
        public DefaultFeatures(IDictionary<Type, float> scores) : base(scores)
        {
        }
        [Format(Formats.PEAK_SCORE)]
        public float? Intensity => GetScore<MQuestDefaultIntensityCalc>();

        [Format(Formats.PEAK_SCORE)]
        public float? CoelutionCount => GetScore<LegacyUnforcedCountScoreCalc>();
        [Format(Formats.PEAK_SCORE)]
        public bool Identified {
            get
            {
                return GetScore<LegacyIdentifiedCountCalc>() != 0;
            }
        } 
        [Format(Formats.PEAK_SCORE)]
        public float? Dotp =>GetScore<MQuestDefaultIntensityCorrelationCalc>();
        [Format(Formats.PEAK_SCORE)]
        public float? Shape => GetScore<MQuestDefaultWeightedShapeCalc>();

        [Format(Formats.PEAK_SCORE)] public float? WeightedCoelution => GetScore<MQuestDefaultWeightedCoElutionCalc>();
        [Format(Formats.PEAK_SCORE)]
        public float? RetentionTimeDifference=> GetScore<MQuestRetentionTimePredictionCalc>();
        [Format(Formats.PEAK_SCORE)]
        public double DefaultPeakScore { get; private set; }

        public override string ToString()
        {
            return DefaultPeakScore.ToString(Formats.PEAK_SCORE, CultureInfo.CurrentCulture);
        }
    }

    public class MProphetFeatures : AbstractPeakScores
    {
        public MProphetFeatures(IDictionary<Type, float> scores) : base(scores)
        {

        }
    }

    public class PeakScores : AbstractPeakScores
    {
        public PeakScores(IDictionary<Type, float> scores, IDictionary<string, Feature> features) : base(scores)
        {
            MProphetScores = new MProphetFeatures(scores);
            DefaultScores = new DefaultFeatures(scores);
            Features = features;
        }

        public MProphetFeatures MProphetScores
        {
            get;
        }
        public DefaultFeatures DefaultScores { get; }
        public IDictionary<string, Feature> Features { get; }

        public static PeakScores MakePeakScores(CandidatePeakScoreCalculator calculator, PeakScoringModelSpec model)
        {
            var features = new Dictionary<string, Feature>();
            var scores = new Dictionary<Type, float>();
            var weights = new Dictionary<Type, double>();
            if (model?.Parameters != null)
            {
                Assume.AreEqual(model.PeakFeatureCalculators.Count, model.Parameters.Weights.Count);
                for (int i = 0; i < model.Parameters.Weights.Count; i++)
                {
                    var weight = model.Parameters.Weights[i];
                    var featureCalc = model.PeakFeatureCalculators[i];
                    weights[featureCalc.GetType()] = weight;
                }
            }
            foreach (var featureCalc in PeakFeatureCalculator.Calculators)
            {
                float? score = calculator.Calculate(featureCalc);
                var featureType = featureCalc.GetType();
                if (float.IsNaN(score.Value))
                {
                    score = null;
                }
                else
                {
                    scores[featureType] = score.Value;
                }
                double weight;
                weights.TryGetValue(featureType, out weight);
                if (score == null && weight == 0)
                {
                    continue;
                }
                var feature = new Feature(score, weight);
                features[featureCalc.HeaderName] = feature;
            }

            return new PeakScores(scores, features);
        }
    }

    public class Feature
    {
        public Feature(float? score, double weight)
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
