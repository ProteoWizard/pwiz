using System;

namespace pwiz.Skyline.Model.Results
{
    public readonly struct PeakShapeValues : IEquatable<PeakShapeValues>
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

        public bool Equals(PeakShapeValues other)
        {
            return StdDev.Equals(other.StdDev) && Skewness.Equals(other.Skewness) && Kurtosis.Equals(other.Kurtosis) && ShapeCorrelation.Equals(other.ShapeCorrelation);
        }

        public override bool Equals(object obj)
        {
            return obj is PeakShapeValues other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StdDev.GetHashCode();
                hashCode = (hashCode * 397) ^ Skewness.GetHashCode();
                hashCode = (hashCode * 397) ^ Kurtosis.GetHashCode();
                hashCode = (hashCode * 397) ^ ShapeCorrelation.GetHashCode();
                return hashCode;
            }
        }
    }
}
