using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class BilinearCurveFit
    {
        public CalibrationCurve CalibrationCurve { get; private set; }
        public double StdDevBaseline { get; private set; }

        public double Slope
        {
            get
            {
                return (CalibrationCurve as CalibrationCurve.Bilinear)?.Slope ?? 0;
            }
        }

        public double Intercept
        {
            get
            {
                return (CalibrationCurve as CalibrationCurve.Bilinear)?.Intercept ?? 0;
            }
        }

        public double BaselineHeight
        {
            get
            {
                if (CalibrationCurve is CalibrationCurve.Bilinear bilinear)
                {
                    return bilinear.GetY(bilinear.TurningPoint);
                }

                return 0;
            }
        }

        public double Error
        {
            get; private set;
        }

        public static BilinearCurveFit FromCalibrationCurve(CalibrationCurve calibrationCurve, IList<WeightedPoint> points)
        {
            var fit = new BilinearCurveFit {CalibrationCurve = calibrationCurve};
            if (calibrationCurve is CalibrationCurve.Bilinear bilinear)
            {
                var baselinePoints = points.Where(pt => pt.X <= bilinear.TurningPoint);
                var baselineStats = new Statistics(baselinePoints.Select(pt => pt.Y));

                fit.StdDevBaseline = baselineStats.Length == 0 ? 0 : baselineStats.StdDevP();
            }
            else
            {
                fit.StdDevBaseline = 0;
            }

            fit.Error = BilinearRegressionFit.CalculateError(calibrationCurve, points);

            return fit;
        }
    }
}
