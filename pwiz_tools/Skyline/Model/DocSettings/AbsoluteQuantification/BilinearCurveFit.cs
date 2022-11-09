using System;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class BilinearCurveFit
    {
        public double Slope { get; private set; }
        public double Intercept { get; private set; }
        public double BaselineHeight { get; private set; }
        public double Error { get; private set; }
        public double StdDevBaseline { get; private set; }

        public double GetY(double x)
        {
            return Math.Max(BaselineHeight, x * Slope + Intercept);
        }

        public static BilinearCurveFit FromLinearFit(CalibrationCurve.Linear calibrationCurve, Statistics baselineStats,
            double totalError)
        {
            return new BilinearCurveFit
            {
                StdDevBaseline = baselineStats.Length == 0 ? double.NaN : baselineStats.StdDevP(),
                BaselineHeight = baselineStats.Length == 0 ? 0 : baselineStats.Mean(),
                Slope = calibrationCurve.Slope,
                Intercept = calibrationCurve.Intercept.Value,
                Error = totalError
            };
        }
    }
}
