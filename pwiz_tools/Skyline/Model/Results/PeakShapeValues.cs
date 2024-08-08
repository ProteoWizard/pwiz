namespace pwiz.Skyline.Model.Results
{
    public struct PeakShapeValues
    {
        public PeakShapeValues(float stdDev, float skewness, float kurtosis, float shapeCorrelation)
        {
            StdDev = stdDev;
            Skewness = skewness;
            Kurtosis = kurtosis;
            ShapeCorrelation = shapeCorrelation;
        }

        public float StdDev { get; }
        public float Skewness { get; }
        public float Kurtosis { get; }
        public float ShapeCorrelation { get; }
    }
}
