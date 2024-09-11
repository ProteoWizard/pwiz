using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class ScoredBilinearCurve
    {
        public CalibrationCurve CalibrationCurve { get; private set; }
        public double StdDevBaseline { get; private set; }

        public double Slope
        {
            get
            {
                if (CalibrationCurve is CalibrationCurve.Bilinear bilinear)
                {
                    return bilinear.Slope;
                }

                if (CalibrationCurve is CalibrationCurve.Linear linear)
                {
                    return linear.Slope;
                }
                return 0;
            }
        }

        public double Intercept
        {
            get
            {
                if (CalibrationCurve is CalibrationCurve.Bilinear bilinear)
                {
                    return bilinear.Intercept;
                }

                if (CalibrationCurve is CalibrationCurve.Linear linear)
                {
                    return linear.Intercept ?? 0;
                }
                return 0;
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

        public static ScoredBilinearCurve FromPoints(IList<WeightedPoint> points)
        {
            return FromCalibrationCurve(RegressionFit.BILINEAR.Fit(points), points);
        }

        public static ScoredBilinearCurve FromCalibrationCurve(CalibrationCurve calibrationCurve, IList<WeightedPoint> points)
        {
            if (calibrationCurve == null)
            {
                return null;
            }
            var fit = new ScoredBilinearCurve {CalibrationCurve = calibrationCurve};
            IList<WeightedPoint> reweightedPoints = points;
            if (calibrationCurve is CalibrationCurve.Bilinear bilinear)
            {
                var baselinePoints = points.Where(pt => pt.X <= bilinear.TurningPoint).ToList();
                var baselineStats = new Statistics(baselinePoints.Select(pt => pt.Y));
                if (baselineStats.Length <= 1)
                {
                    fit.StdDevBaseline = 0;
                }
                else
                {
                    fit.StdDevBaseline = baselineStats.StdDev();
                    var minBaselineWeight = baselinePoints.Min(pt => pt.Weight);
                    reweightedPoints = points.Where(pt => pt.X > bilinear.TurningPoint)
                        .Concat(baselinePoints.Select(pt => new WeightedPoint(pt.X, pt.Y, minBaselineWeight))).ToList();
                }
            }
            else
            {
                fit.StdDevBaseline = 0;
            }

            fit.Error = CalculateError(calibrationCurve, reweightedPoints);

            return fit;
        }

        public static ScoredBilinearCurve WithOffset(double xOffset, IList<WeightedPoint> points)
        {
            if (points.Count == 0)
            {
                return FlatBilinearCurve(points);
            }
            var linearPoints = points.Where(pt => pt.X > xOffset).ToList();
            if (linearPoints.Select(pt=>pt.X).Distinct().Count() < 2)
            {
                return FlatBilinearCurve(points);
            }
            var linearCurve = RegressionFit.LINEAR.Fit(linearPoints) as CalibrationCurve.Linear;
            if (linearCurve == null || linearCurve.Slope <= 0)
            {
                return FlatBilinearCurve(points);
            }

            if (linearPoints.Count == points.Count)
            {
                return FromCalibrationCurve(linearCurve, points);
            }
            
            var baselinePoints = points.Where(pt => pt.X <= xOffset).ToList();
            var baselineHeight = baselinePoints.Average(pt => pt.Y);
            var turningPoint = linearCurve.GetX(baselineHeight).GetValueOrDefault();
            if (turningPoint < 0)
            {
                return FlatBilinearCurve(points);
            }

            var curve = new CalibrationCurve.Bilinear(linearCurve, turningPoint);
            return FromCalibrationCurve(curve, points);
        }

        private static ScoredBilinearCurve FlatBilinearCurve(IList<WeightedPoint> points)
        {
            if (points.Count == 0)
            {
                return FromCalibrationCurve(new CalibrationCurve.Linear(0, 0), points);
            }

            var baselineHeight = points.Average(pt => pt.Y);
            var linearCurve = new CalibrationCurve.Linear(0, baselineHeight);
            var bilinearCurve = new CalibrationCurve.Bilinear(linearCurve, points.Max(pt => pt.X));
            return FromCalibrationCurve(bilinearCurve, points.Select(pt => new WeightedPoint(pt.X, pt.Y, 1)).ToList());
        }

        private static double CalculateError(CalibrationCurve calibrationCurve, IList<WeightedPoint> points)
        {
            double totalError = 0;
            double totalWeight = 0;
            foreach (var point in points)
            {
                double expected = calibrationCurve.GetY(point.X);
                double difference = expected - point.Y;
                totalError += difference * difference * point.Weight;
                totalWeight += point.Weight;
            }

            if (totalWeight == 0)
            {
                return double.MaxValue;
            }
            return totalError / totalWeight;
        }

    }
}
