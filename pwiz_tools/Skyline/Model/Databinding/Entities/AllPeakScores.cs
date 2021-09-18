using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class AllPeakScores
    {
        public AllPeakScores(DefaultPeakScores defaultPeakScores, IDictionary<string, float> featureScores)
        {
            DefaultPeakScores = defaultPeakScores;
            Features = featureScores;
        }

        public DefaultPeakScores DefaultPeakScores { get; private set; }
        public IDictionary<string, float> Features { get; private set; }
        public static AllPeakScores MakeAllPeakScores(CandidatePeakScoreCalculator calculator)
        {
            var features = new Dictionary<string, float>();
            foreach (var scoreCalculator in PeakFeatureCalculator.Calculators)
            {
                {
                    var value = calculator.Calculate(scoreCalculator);
                    if (!float.IsNaN(value))
                    {
                        features[scoreCalculator.Name] = value;
                    }
                }
            }

            return new AllPeakScores(DefaultPeakScores.CalculateScores(calculator), features);
        }
    }
}
