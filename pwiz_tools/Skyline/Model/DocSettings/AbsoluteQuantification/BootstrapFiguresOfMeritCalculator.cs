using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using ZedGraph;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class BootstrapFiguresOfMeritCalculator : Immutable, IFiguresOfMeritCalculator
    {
        private const double SAME_LOQ_REL_THRESHOLD = .001;
        public BootstrapFiguresOfMeritCalculator(double cvThreshold)
        {
            CvThreshold = cvThreshold;
            RandomSeed = 0;
            GridSize = 100;
            MaxBootstrapIterations = 100;
            MinBootstrapIterations = 10;
            MinSameLoqCountForAccept = 25;
        }

        public double CvThreshold { get; }
        public int RandomSeed { get; }
        public int GridSize { get; }
        public int MaxBootstrapIterations { get; set; }

        public BootstrapFiguresOfMeritCalculator ChangeMaxBootstrapIterations(int value)
        {
            return ChangeProp(ImClone(this), im => im.MaxBootstrapIterations = value);
        }
        public int MinBootstrapIterations { get; private set; }

        public BootstrapFiguresOfMeritCalculator ChangeMinBootstrapIterations(int value)
        {
            return ChangeProp(ImClone(this), im => im.MinBootstrapIterations = value);
        }
        public int MinSameLoqCountForAccept { get; }

        public FiguresOfMerit GetFiguresOfMerit(CalibrationCurve calibrationCurve, IList<WeightedPoint> standards, IList<double> blanks,
            List<ImmutableList<PointPair>> bootstrapCurves)
        {
            var limitOfDetection = ComputeLod(standards);
            var limitOfQuantification = ComputeBootstrappedLoq(standards, bootstrapCurves);
            limitOfQuantification = Math.Max(limitOfDetection, limitOfQuantification);
            var figuresOfMerit = FiguresOfMerit.EMPTY;
            if (limitOfDetection < double.MaxValue)
            {
                figuresOfMerit = figuresOfMerit.ChangeLimitOfDetection(limitOfDetection);
            }

            if (limitOfQuantification < double.MaxValue)
            {
                figuresOfMerit = figuresOfMerit.ChangeLimitOfQuantification(limitOfQuantification);
            }
            return figuresOfMerit;
        }

        private static double ComputeLod(IList<WeightedPoint> points, out ScoredBilinearCurve fit)
        {
            fit = null;
            if (points.Count == 0)
            {
                return double.MaxValue;
            }
            fit = ScoredBilinearCurve.FromPoints(points);
            if (fit == null || double.IsNaN(fit.StdDevBaseline))
            {
                return double.MaxValue;
            }
            var largestConc = points.Max(pt => pt.X);
            var lodArea = fit.BaselineHeight + fit.StdDevBaseline;
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

        public static double ComputeLod(IList<WeightedPoint> points)
        {
            return ComputeLod(points, out _);
        }

        public double ComputeBootstrappedLoq(IList<WeightedPoint> points, List<ImmutableList<PointPair>> bootstrapCurves)
        {
            var random = new Random(RandomSeed);
            var lod = ComputeLod(points, out var bilinearCurve);
            var linearPoints = points.Where(pt => pt.X >= lod).ToList();
            if (linearPoints.Count < 2)
            {
                return double.MaxValue;
            }

            var intercept = bilinearCurve.Intercept;
            var compensationFactor = Math.Sqrt((linearPoints.Count - 1.0) / 2);
            var adjustedCvThreshold = CvThreshold / compensationFactor;
            var maxConcentration = linearPoints.Max(pt => pt.X);
            var concentrationValues = Enumerable.Range(0, GridSize)
                .Select(i => lod + (maxConcentration - lod) * i / (GridSize - 1)).ToList();
            var areaGrid = Enumerable.Range(0, GridSize).Select(i => new RunningStatistics()).ToList();
            int numItersWithSameLoq = 0;
            double lastLoq = maxConcentration;
            for (int i = 0; i < MaxBootstrapIterations; i++)
            {
                var p = ComputeBootstrapParams(random, points);
                List<double> areaValues = null;
                if (bootstrapCurves != null)
                {
                    areaValues = new List<double>();
                }
                for (int iConcentration = 0; iConcentration < concentrationValues.Count; iConcentration++)
                {
                    var area = p.CalibrationCurve.GetY(concentrationValues[iConcentration]);
                    areaGrid[iConcentration].Push(area - intercept);
                    areaValues?.Add(area);
                }

                bootstrapCurves?.Add(ImmutableList.ValueOf(concentrationValues.Zip(areaValues, (x, y) => new PointPair(x, y))));

                if (i > MinBootstrapIterations)
                {
                    var loqCheck = maxConcentration;
                    for (int iConcentration = concentrationValues.Count - 1; iConcentration >= 0; iConcentration--)
                    {
                        if (GetCv(areaGrid[iConcentration]) > adjustedCvThreshold)
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

            double loq = double.MaxValue;
            for (int iConcentration = concentrationValues.Count - 1; iConcentration >= 0; iConcentration--)
            {
                var cv = GetCv(areaGrid[iConcentration]);
                if (cv > adjustedCvThreshold)
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
        public ScoredBilinearCurve ComputeBootstrapParams(Random random, IList<WeightedPoint> points)
        {
            var randomPoints = Enumerable.Range(0, points.Count)
                .Select(i => points[random.Next(points.Count)]).ToList();
            return ScoredBilinearCurve.FromPoints(randomPoints);
        }

    }
}
