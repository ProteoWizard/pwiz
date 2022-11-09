using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;

namespace pwiz.Skyline.Model.DocSettings
{
    public class ErrorCalibrationCurve : CalibrationCurve
    {
        public ErrorCalibrationCurve(string message)
        {
            ErrorMessage = message;
        }

        public string ErrorMessage { get; }
        public override double? GetY(double? x)
        {
            return null;
        }

        public override double? GetX(double? y)
        {
            return null;
        }

        protected override CalibrationCurveMetrics ToCalibrationCurveRow()
        {
            return new CalibrationCurveMetrics().ChangeErrorMessage(ErrorMessage);
        }
    }
}
