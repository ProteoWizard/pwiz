using System;
using System.Collections.Generic;
using System.Linq;
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

        public static BilinearCurveFit FitBilinearCurve(IEnumerable<WeightedPoint> points)
        {
            var pointList = points.ToList();
            var uniqueConcentrations = pointList.Select(pt => pt.X).Distinct().OrderBy(x => x).ToList();
            var fits = new List<BilinearCurveFit>();
            foreach (var conc in uniqueConcentrations)
            {
                var fit = FitBilinearCurveWithOffset(conc, pointList);
                if (fit != null)
                {
                    fits.Add(fit);
                }
            }

            return fits.OrderBy(fit => fit.Error).FirstOrDefault();
        }

        public static BilinearCurveFit FitBilinearCurveWithOffset(double xOffset, ICollection<WeightedPoint> points)
        {
            var linearPoints = points.Where(pt => pt.X > xOffset).ToList();
            var baselinePoints = points.Where(pt => pt.X <= xOffset).ToList();

            var linearFit = RegressionFit.LinearFit(linearPoints);
            if (linearFit == null || !string.IsNullOrEmpty(linearFit.ErrorMessage))
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
                StdDevBaseline = baselineStats.Length == 0 ? double.NaN : baselineStats.StdDevP(),
                Error = totalError,
            };
        }

        public static BilinearCurveFit ComputeBootstrapParams(Random random, IList<WeightedPoint> points)
        {
            var randomPoints = Enumerable.Range(0, points.Count)
                .Select(i => points[random.Next(points.Count)]).ToList();
            return FitBilinearCurve(randomPoints);
        }

        public static double ComputeBootstrappedLoq(Random random, int maxIterations, IList<WeightedPoint> points)
        {
            var lod = ComputeLod(points);
            const int gridSize = 100;
            var maxConcentration = points.Max(pt => pt.X);
            var concentrationValues = Enumerable.Range(0, gridSize)
                .Select(i => lod + (maxConcentration - lod) * i / (gridSize - 1)).ToList();
            var areaGrid = Enumerable.Range(0, gridSize).Select(i => new MathNet.Numerics.Statistics.RunningStatistics()).ToList();
            for (int i = 0; i < maxIterations; i++)
            {
                var p = ComputeBootstrapParams(random, points);
                for (int iConcentration = 0; iConcentration < concentrationValues.Count; iConcentration++)
                {
                    areaGrid[iConcentration].Push(p.GetY(concentrationValues[iConcentration]));
                }
            }

            const double cvThreshold = .2;
            var cvs = new List<double>(areaGrid.Count);
            foreach (var runningStatistics in areaGrid)
            {
                if (runningStatistics.Mean <= 0)
                {
                    cvs.Add(cvThreshold * 2);
                }
                else
                {
                    cvs.Add(runningStatistics.StandardDeviation / runningStatistics.Mean);
                }
            }

            double loq = maxConcentration;
            for (int iConcentration = concentrationValues.Count - 1; iConcentration >= 0; iConcentration--)
            {
                var cv = cvs[iConcentration];
                if (cv > cvThreshold)
                {
                    break;
                }

                loq = concentrationValues[iConcentration];
            }

            return loq;
        }

        public static double ComputeLod(IList<WeightedPoint> points)
        {
            BilinearCurveFit fit = FitBilinearCurve(points);
            var lodArea = fit.BaselineHeight + fit.StdDevBaseline;
            var largestConc = points.Max(pt => pt.X);
            var smallestNonzeroConc = points.Where(pt => pt.X > 0).Select(pt => pt.X).Append(largestConc).Min();
            double lodConc;
            if (fit.Slope == 0)
            {
                lodConc = largestConc;
            }
            else
            {
                lodConc = (lodArea - fit.Intercept) / fit.Slope;
            }

            lodConc = Math.Max(smallestNonzeroConc, Math.Min(lodConc, largestConc));
            return lodConc;
        }
    }
}
