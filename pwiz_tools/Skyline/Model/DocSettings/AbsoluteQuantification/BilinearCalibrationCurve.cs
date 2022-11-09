using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class BilinearCalibrationCurve : CalibrationCurve
    {
        private readonly LinearCalibrationCurve _linearCalibrationCurve;
        public BilinearCalibrationCurve(LinearCalibrationCurve linearCalibrationCurve, double turningPoint)
        {
            _linearCalibrationCurve = linearCalibrationCurve;
            TurningPoint = turningPoint;
        }

        public double Slope
        {
            get { return _linearCalibrationCurve.Slope; }
        }

        public double Intercept
        {
            get
            {
                return _linearCalibrationCurve.Intercept.GetValueOrDefault();
            }
        }

        public double TurningPoint { get; }

        public override double? GetY(double? x)
        {
            if (x < TurningPoint)
            {
                return _linearCalibrationCurve.GetY(TurningPoint);
            }

            return _linearCalibrationCurve.GetY(x);
        }

        public override double? GetX(double? y)
        {
            double? x = _linearCalibrationCurve.GetX(y);
            if (x < TurningPoint)
            {
                return null;
            }

            return x;
        }

        public override double? GetXValueForLimitOfDetection(double? y)
        {
            return _linearCalibrationCurve.GetX(y);
        }

        protected override CalibrationCurveMetrics ToCalibrationCurveRow()
        {
            return new CalibrationCurveMetrics().ChangeSlope(Slope).ChangeIntercept(Intercept)
                .ChangeTurningPoint(TurningPoint);
        }
    }
}
