using System;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class LinearInLogSpaceCalibrationCurve : CalibrationCurve
    {
        private readonly LinearCalibrationCurve _linearCalibrationCurve;
        public LinearInLogSpaceCalibrationCurve(LinearCalibrationCurve linearCalibrationCurve)
        {
            _linearCalibrationCurve = linearCalibrationCurve;
        }

        public double Slope
        {
            get { return _linearCalibrationCurve.Slope; }
        }

        public double Intercept
        {
            get { return _linearCalibrationCurve.Intercept.GetValueOrDefault(); }
        }

        public override double? GetY(double? x)
        {
            if (x.HasValue)
            {
                var y = _linearCalibrationCurve.GetY(Math.Log(x.Value));
                if (y.HasValue)
                {
                    return Math.Exp(y.Value);
                }
            }
            return null;
        }

        public override double? GetX(double? y)
        {
            if (y.HasValue)
            {
                var x = _linearCalibrationCurve.GetX(Math.Log(y.Value));
                if (x.HasValue)
                {
                    return Math.Exp(x.Value);
                }
            }
            return null;
        }

        protected override CalibrationCurveMetrics ToCalibrationCurveRow()
        {
            return new CalibrationCurveMetrics().ChangeSlope(Slope).ChangeIntercept(Intercept);
        }
    }
}
