using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public static BilinearCurveFit FitBilinearCurve(IEnumerable<WeightedPoint> points)
        {
            var pointList = points.ToList();
            var uniqueConcentrations = pointList.Select(pt => pt.X).Distinct().OrderBy(x => x).ToList();
            var fits = new List<BilinearCurveFit>();
            foreach (var conc in uniqueConcentrations)
            {
                fits.Add(FitBilinearCurveWithOffset(conc, pointList));
            }

            return fits.OrderBy(fit => -fit.Error).FirstOrDefault();
        }

        public static BilinearCurveFit FitBilinearCurveWithOffset(double xOffset, ICollection<WeightedPoint> points)
        {
            var linearPoints = points.Where(pt => pt.X > xOffset).ToList();
            var baselinePoints = points.Where(pt => pt.X <= xOffset).ToList();

            var linearFit = RegressionFit.LinearFit(linearPoints);
            if (linearFit == null)
            {
                return null;
            }

            var baselineStats = new Statistics(baselinePoints.Select(pt => pt.Y));
            var baselineHeight = baselineStats.Length == 0 ? 0 : baselineStats.Mean();
            double totalError = 0;
            foreach (var point in points)
            {
                double expected = Math.Max(baselineHeight, linearFit.GetY(point.X).Value);
                double difference = expected - point.Y;
                totalError += difference * difference * point.Weight;
            }
            return new BilinearCurveFit()
            {
                BaselineHeight = baselineHeight,
                Slope = linearFit.Slope.Value,
                Intercept = linearFit.Intercept.Value,
                StdDevBaseline = baselineStats.Length == 0 ? double.NaN : baselineStats.StdDev(),
                Error = totalError,
            };
        }
    }
}
