using System;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class QuadraticCalibrationCurve : CalibrationCurve
    {
        public QuadraticCalibrationCurve(double intercept, double slope, double quadraticCoefficient)
        {
            Intercept = intercept;
            Slope = slope;
            QuadraticCoefficient = quadraticCoefficient;
        }

        public double Intercept { get; }
        public double Slope { get; }
        public double QuadraticCoefficient { get; }

        public override double? GetX(double? y)
        {
            // Quadratic formula: x = (-b +/- sqrt(b^2-4ac))/2a
            double? a = QuadraticCoefficient;
            double? b = Slope;
            double? c = Intercept - y;

            double? discriminant = b * b - 4 * a * c;
            if (!discriminant.HasValue)
            {
                return null;
            }
            if (discriminant < 0)
            {
                return double.NaN;
            }
            double sqrtDiscriminant = Math.Sqrt(discriminant.Value);
            return (-b + sqrtDiscriminant) / 2 / a;
        }

        public override double? GetY(double? x)
        {
            return x * x * QuadraticCoefficient + x * Slope + Intercept;
        }

        protected override CalibrationCurveMetrics ToCalibrationCurveRow()
        {
            return new CalibrationCurveMetrics().ChangeIntercept(Intercept).ChangeSlope(Slope)
                .ChangeQuadraticCoefficient(QuadraticCoefficient);
        }
    }
}
