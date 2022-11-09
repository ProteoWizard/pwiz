using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class LinearCalibrationCurve : CalibrationCurve
    {
        public LinearCalibrationCurve(double slope, double? intercept)
        {
            Slope = slope;
            Intercept = intercept;
        }

        public double Slope { get; }
        public double? Intercept { get; }

        public override double? GetY(double? x)
        {
            return x * Slope + Intercept.GetValueOrDefault();
        }

        public override double? GetX(double? y)
        {
            return (y - Intercept.GetValueOrDefault()) / Slope;
        }

        protected override CalibrationCurveMetrics ToCalibrationCurveRow()
        {
            return new CalibrationCurveMetrics().ChangeSlope(Slope).ChangeIntercept(Intercept);
        }
    }
}
