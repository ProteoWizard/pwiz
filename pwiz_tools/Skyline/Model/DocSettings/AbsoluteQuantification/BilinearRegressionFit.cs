using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class BilinearRegressionFit : RegressionFit
    {
        public BilinearRegressionFit() : base("bootstrap_bilinear", () => "Bilinear")
        {

        }

        protected override CalibrationCurve FitPoints(IList<WeightedPoint> points)
        {
            var uniqueConcentrations = points.Select(pt => pt.X).Distinct().OrderBy(x => x).ToList();
            CalibrationCurve bestCurve = null;
            double bestError = double.MaxValue;
            foreach (var xOffset in uniqueConcentrations)
            {
                CalibrationCurve candidateCurve = FitPointsWithOffset(xOffset, points);
                if (candidateCurve == null)
                {
                    continue;
                }
                double error = CalculateError(candidateCurve, points);
                if (bestCurve == null || error < bestError)
                {
                    bestCurve = candidateCurve;
                    bestError = error;
                }
            }

            return bestCurve;
        }

        public static CalibrationCurve FitPointsWithOffset(double xOffset, IList<WeightedPoint> points)
        {
            var linearPoints = points.Where(pt => pt.X > xOffset).ToList();
            var baselinePoints = points.Where(pt => pt.X <= xOffset).ToList();
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

        public static double CalculateError(CalibrationCurve calibrationCurve, IList<WeightedPoint> points)
        {
            double totalError = 0;
            foreach (var point in points)
            {
                double expected = calibrationCurve.GetY(point.X);
                double difference = expected - point.Y;
                totalError += difference * difference * point.Weight;
            }

            return totalError;
        }
    }
}