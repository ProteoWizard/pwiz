using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class DefaultPeakScores
    {
        public float Intensity { get; private set; }
        public float CoelutionCount { get; private set; }
        public bool Identified { get; private set; }
        public float Dotp { get; private set; }
        public float Shape { get; private set; }
        public float WeightedCoelution { get; private set; }
        public float RetentionTimeDifference { get; private set; }

        private static readonly MQuestDefaultIntensityCalc intensityCalc = new MQuestDefaultIntensityCalc();

        private static readonly LegacyUnforcedCountScoreDefaultCalc coelutionCalc =
            new LegacyUnforcedCountScoreDefaultCalc();

        private static readonly LegacyIdentifiedCountCalc identifiedCalc = new LegacyIdentifiedCountCalc();

        private static readonly MQuestDefaultIntensityCorrelationCalc dotpCalc =
            new MQuestDefaultIntensityCorrelationCalc();

        private static readonly MQuestDefaultWeightedShapeCalc shapeCalc = new MQuestDefaultWeightedShapeCalc();

        private static readonly MQuestDefaultWeightedCoElutionCalc weightedCoelutionCalc =
            new MQuestDefaultWeightedCoElutionCalc();

        private static readonly MQuestRetentionTimePredictionCalc rtDifferenceCalc =
            new MQuestRetentionTimePredictionCalc();


        public static DefaultPeakScores CalculateScores(PeakScoreCalculator scoreCalculator)
        {
            var peakScores = new DefaultPeakScores()
            {
                Intensity = scoreCalculator.Calculate(intensityCalc),
                CoelutionCount = scoreCalculator.Calculate(coelutionCalc),
                Identified = scoreCalculator.Calculate(identifiedCalc) != 0,
                Dotp = scoreCalculator.Calculate(dotpCalc),
                Shape = scoreCalculator.Calculate(shapeCalc),
                WeightedCoelution = scoreCalculator.Calculate(weightedCoelutionCalc),
                RetentionTimeDifference = scoreCalculator.Calculate(rtDifferenceCalc)
            };
            return peakScores;
        }
    }
}
