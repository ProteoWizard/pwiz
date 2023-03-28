using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class BilinearRegressionFit : RegressionFit
    {
        public BilinearRegressionFit() : base(@"bootstrap_bilinear", () => "Bilinear")
        {

        }

        protected override CalibrationCurve FitPoints(IList<WeightedPoint> points)
        {
            var concentrations = points.Select(pt => pt.X).Distinct().OrderBy(x=>x).ToList();
            BilinearCurveFit bestCurve = null;
            foreach (var xOffset in concentrations)
            {
                var candidateCurveFit = BilinearCurveFit.WithOffset(xOffset, points);
                if (candidateCurveFit == null)
                {
                    continue;
                }
                if (bestCurve == null || candidateCurveFit.Error < bestCurve.Error)
                {
                    bestCurve = candidateCurveFit;
                }
            }

            return bestCurve?.CalibrationCurve;
        }

        public static CalibrationCurve FitPointsWithOffset(double xOffset, double baselineWeight, IList<WeightedPoint> points)
        {
            var linearPoints = points.Where(pt => pt.X > xOffset).ToList();
            var baselinePoints = points.Where(pt => pt.X <= xOffset).Select(pt=>new WeightedPoint(pt.X, pt.Y, baselineWeight)).ToList();
            if (linearPoints.Select(pt => pt.X).Distinct().Count() >= 2)
            {
                var linearCurve = RegressionFit.LINEAR.Fit(linearPoints) as CalibrationCurve.Linear;
                if (linearCurve == null || linearCurve.Slope <= 0)
                {
                    return null;
                }

                if (baselinePoints.Count == 0)
                {
                    return linearCurve;
                }
                else
                {
                    var baselineHeight = baselinePoints.Select(pt => pt.Y).Mean();
                    var turningPoint = linearCurve.GetX(baselineHeight).GetValueOrDefault();
                    if (turningPoint < 0)
                    {
                        return null;
                    }
                    return new CalibrationCurve.Bilinear(linearCurve, turningPoint);
                }
            }
            else
            {
                if (baselinePoints.Count == 0)
                {
                    return null;
                }

                return new CalibrationCurve.Linear(0, baselinePoints.Select(pt => pt.Y).Mean());
            }
        }

    }
}