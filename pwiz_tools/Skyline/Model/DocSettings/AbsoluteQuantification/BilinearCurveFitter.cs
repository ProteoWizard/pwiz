using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MathNet.Numerics.Statistics;
using NHibernate.Linq.GroupJoin;
using Statistics = pwiz.Skyline.Util.Statistics;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class BilinearCurveFitter
    {
        private const double SAME_LOQ_REL_THRESHOLD = .001;
        public BilinearCurveFitter()
        {
            MaxBootstrapIterations = 100;
            MinBootstrapIterations = 10;
            MinSameLoqCountForAccept = 25;
            GridSize = 100;
            CvThreshold = .2;
        }
        public int MaxBootstrapIterations { get; set; }
        public int MinBootstrapIterations { get; set; }
        public int MinSameLoqCountForAccept { get; set; }
        public int GridSize { get; set; }
        public double CvThreshold { get; set; }

        public BilinearCurveFit FitBilinearCurve(IEnumerable<WeightedPoint> points)
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

        public BilinearCurveFit FitBilinearCurveWithOffset(double xOffset, ICollection<WeightedPoint> points)
        {
            var linearPoints = points.Where(pt => pt.X > xOffset).ToList();
            var baselinePoints = points.Where(pt => pt.X <= xOffset).ToList();

            CalibrationCurve linearFit;
            if (linearPoints.Count >= 2)
            {
                linearFit = RegressionFit.LinearFit(linearPoints);
                if (linearFit == null || !string.IsNullOrEmpty(linearFit.ErrorMessage))
                {
                    return null;
                }
            }
            else
            {
                baselinePoints.AddRange(linearPoints);
                linearFit = new CalibrationCurve(RegressionFit.LINEAR).ChangeSlope(0).ChangeIntercept(0);
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
            return BilinearCurveFit.FromLinearFit(linearFit, baselineStats, totalError);
        }

        public BilinearCurveFit ComputeBootstrapParams(Random random, IList<WeightedPoint> points)
        {
            var randomPoints = Enumerable.Range(0, points.Count)
                .Select(i => points[random.Next(points.Count)]).ToList();
            return FitBilinearCurve(randomPoints);
        }

        public double ComputeBootstrappedLoq(Random random, IList<WeightedPoint> points)
        {
            var lod = ComputeLod(points);
            var maxConcentration = points.Max(pt => pt.X);
            var concentrationValues = Enumerable.Range(0, GridSize)
                .Select(i => lod + (maxConcentration - lod) * i / (GridSize - 1)).ToList();
            var areaGrid = Enumerable.Range(0, GridSize).Select(i => new RunningStatistics()).ToList();
            int numItersWithSameLoq = 0;
            double lastLoq = maxConcentration;
            for (int i = 0; i < MaxBootstrapIterations; i++)
            {
                var p = ComputeBootstrapParams(random, points);
                for (int iConcentration = 0; iConcentration < concentrationValues.Count; iConcentration++)
                {
                    var area = p.GetY(concentrationValues[iConcentration]);
                    if (double.IsNaN(area) || double.IsInfinity(area))
                    {
                        Trace.TraceWarning("Invalid area {0}", area);
                    }
                    areaGrid[iConcentration].Push(area);
                }

                if (i > MinBootstrapIterations)
                {
                    var loqCheck = maxConcentration;
                    for (int iConcentration = concentrationValues.Count - 1; iConcentration >= 0; iConcentration--)
                    {
                        if (GetCv(areaGrid[iConcentration]) > CvThreshold)
                        {
                            break;
                        }
                        else
                        {
                            loqCheck = concentrationValues[iConcentration];
                        }
                    }

                    if (loqCheck >= 0 && Math.Abs(loqCheck - lastLoq) / loqCheck < SAME_LOQ_REL_THRESHOLD)
                    {
                        numItersWithSameLoq++;
                    }
                    else
                    {
                        numItersWithSameLoq = 0;
                    }

                    lastLoq = loqCheck;
                    if (numItersWithSameLoq > MinSameLoqCountForAccept)
                    {
                        break;
                    }
                }
            }

            double loq = maxConcentration;
            for (int iConcentration = concentrationValues.Count - 1; iConcentration >= 0; iConcentration--)
            {
                var cv = GetCv(areaGrid[iConcentration]);
                if (cv > CvThreshold)
                {
                    break;
                }

                loq = concentrationValues[iConcentration];
            }

            return loq;
        }

        private double GetCv(RunningStatistics runningStatistics)
        {
            if (runningStatistics.Mean <= 0)
            {
                return CvThreshold * 2;
            }

            return runningStatistics.StandardDeviation / runningStatistics.Mean;
        }

        public double ComputeLod(IList<WeightedPoint> points)
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
