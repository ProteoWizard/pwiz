namespace pwiz.Skyline.Model.Results
{
    public class IntensityAccumulator
    {
        public double TotalIntensity { get; set; }
        public double MeanMassError { get; set; }
        bool _highAcc;
        ChromExtractor _extractor;
        private double _targetMz;


        public IntensityAccumulator(bool highAcc, ChromExtractor extractor, double targetMz)
        {
            _highAcc = highAcc;
            _extractor = extractor;
            _targetMz = targetMz;
        }

        public void AddPoint(double mz, double intensity)
        {
            if (_extractor == ChromExtractor.summed)
                TotalIntensity += intensity;
            else if (intensity > TotalIntensity)
            {
                TotalIntensity = intensity;
                MeanMassError = 0;
            }

            // Accumulate weighted mean mass error for summed, or take a single
            // mass error of the most intense peak for base peak.
            if (_highAcc && (_extractor == ChromExtractor.summed || MeanMassError == 0))
            {
                if (TotalIntensity > 0.0)
                {
                    double deltaPeak = mz - _targetMz;
                    MeanMassError += (deltaPeak - MeanMassError) * intensity / TotalIntensity;
                }
            }
        }
    }
}