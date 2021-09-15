using System.Collections.Generic;
using System.Globalization;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class DefaultPeakScores
    {
        [Format(Formats.PEAK_SCORE)]
        public float? Intensity { get; private set; }
        [Format(Formats.PEAK_SCORE)]
        public float? CoelutionCount { get; private set; }
        [Format(Formats.PEAK_SCORE)]
        public bool Identified { get; private set; }
        [Format(Formats.PEAK_SCORE)]
        public float? Dotp { get; private set; }
        [Format(Formats.PEAK_SCORE)]
        public float? Shape { get; private set; }
        [Format(Formats.PEAK_SCORE)]
        public float? WeightedCoelution { get; private set; }
        [Format(Formats.PEAK_SCORE)]
        public float? RetentionTimeDifference { get; private set; }
        [Format(Formats.PEAK_SCORE)]
        public double CombinedScore { get; private set; }

        public override string ToString()
        {
            return CombinedScore.ToString(Formats.PEAK_SCORE, CultureInfo.CurrentCulture);
        }

        private static readonly int? INDEX_INTENSITY = IndexOfScore<MQuestDefaultIntensityCalc>();
        private static readonly int? INDEX_COELUTION = IndexOfScore<LegacyUnforcedCountScoreDefaultCalc>();
        private static readonly int? INDEX_IDENTIFIED = IndexOfScore<LegacyIdentifiedCountCalc>();
        private static readonly int? INDEX_DOTP = IndexOfScore<MQuestDefaultIntensityCorrelationCalc>();
        private static readonly int? INDEX_SHAPE = IndexOfScore<MQuestDefaultWeightedShapeCalc>();
        private static readonly int? INDEX_WEIGHTED_COELUTION = IndexOfScore<MQuestDefaultWeightedCoElutionCalc>();

        private static readonly int? INDEX_RETENTION_TIME_DIFFERENCE =
            IndexOfScore<MQuestRetentionTimePredictionCalc>();

        public static DefaultPeakScores CalculateScores(IPeakScoreCalculator scoreCalculator)
        {
            var scores = new List<float>();
            var model = LegacyScoringModel.DEFAULT_MODEL;
            foreach (var feature in model.PeakFeatureCalculators)
            {
                var score = scoreCalculator.Calculate(feature);
                scores.Add(score);
            }

            var peakScores = new DefaultPeakScores()
            {
                Intensity = GetScore(scores, INDEX_INTENSITY),
                CoelutionCount = GetScore(scores, INDEX_COELUTION),
                Identified = GetScore(scores, INDEX_IDENTIFIED) != 0,
                Dotp = GetScore(scores, INDEX_DOTP),
                Shape = GetScore(scores, INDEX_SHAPE),
                WeightedCoelution = GetScore(scores, INDEX_WEIGHTED_COELUTION),
                RetentionTimeDifference = GetScore(scores, INDEX_RETENTION_TIME_DIFFERENCE)
            };
            peakScores.CombinedScore = model.Score(scores);
            return peakScores;
        }

        private static int? IndexOfScore<T>()
        {
            var featureCalculators = LegacyScoringModel.DEFAULT_MODEL.PeakFeatureCalculators;
            for (int i = 0; i < featureCalculators.Count; i++)
            {
                if (featureCalculators[i] is T)
                {
                    return i;
                }
            }

            return null;
        }

        private static float? GetScore(IList<float> scores, int? index)
        {
            if (!index.HasValue)
            {
                return null;
            }

            if (index < 0 || index >= scores.Count)
            {
                return null;
            }

            return scores[index.Value];
        }
    }
}
