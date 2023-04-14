using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using ZedGraph;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public interface IFiguresOfMeritCalculator
    {
        public FiguresOfMerit GetFiguresOfMerit(CalibrationCurve calibrationCurve, IList<WeightedPoint> standards,
            IList<double> blanks, List<ImmutableList<PointPair>> bootstrapCurves);
    }

    public class SimpleFiguresOfMeritCalculator : IFiguresOfMeritCalculator
    {
        public SimpleFiguresOfMeritCalculator(LodCalculation lodCalculation, double? maxLoqCv, double? maxLoqBias)
        {
            LodCalculation = lodCalculation;
            MaxLoqCv = maxLoqCv;
            MaxLoqBias = maxLoqBias;
        }

        public LodCalculation LodCalculation { get; }
        public double? MaxLoqCv { get; }
        public double? MaxLoqBias { get; }

        public FiguresOfMerit GetFiguresOfMerit(CalibrationCurve calibrationCurve, IList<WeightedPoint> standards, IList<double> blanks,
            List<ImmutableList<PointPair>> bootstrapCurves)
        {
            FiguresOfMerit figuresOfMerit = FiguresOfMerit.EMPTY;
            if (LodCalculation != null)
            {
                figuresOfMerit =
                    figuresOfMerit.ChangeLimitOfDetection(
                        LodCalculation.CalculateLod(new LodCalculation.LodCalculationArgs(calibrationCurve, standards, blanks)));
            }

            figuresOfMerit = figuresOfMerit.ChangeLimitOfQuantification(CalculateLoq(calibrationCurve, standards));
            return figuresOfMerit;
        }

        public double? CalculateLoq(CalibrationCurve calibrationCurve, IList<WeightedPoint> standards)
        {
            if (!MaxLoqCv.HasValue && !MaxLoqBias.HasValue)
            {
                return null;
            }
            double? bestLoq = null;
            foreach (var concentrationGroup in standards.GroupBy(pt => pt.X).OrderByDescending(grouping => grouping.Key))
            {
                var peakAreas = concentrationGroup.Select(pt => pt.Y).ToList();
                if (MaxLoqCv.HasValue)
                {
                    if (peakAreas.Count > 1)
                    {
                        double cv = peakAreas.StandardDeviation() / peakAreas.Mean();
                        if (double.IsNaN(cv) || double.IsInfinity(cv))
                        {
                            break;
                        }
                        if (cv > MaxLoqCv)
                        {
                            break;
                        }
                    }
                }
                if (MaxLoqBias.HasValue)
                {
                    if (calibrationCurve == null)
                    {
                        continue;
                    }
                    double meanPeakArea = peakAreas.Mean();
                    double? backCalculatedConcentration = calibrationCurve.GetXValueForLimitOfDetection(meanPeakArea);
                    if (!backCalculatedConcentration.HasValue)
                    {
                        break;
                    }
                    double bias = (concentrationGroup.Key - backCalculatedConcentration.Value) /
                                  concentrationGroup.Key;
                    if (double.IsNaN(bias) || double.IsInfinity(bias))
                    {
                        break;
                    }
                    if (Math.Abs(bias) > MaxLoqBias.Value)
                    {
                        break;
                    }
                }
                bestLoq = concentrationGroup.Key;
            }
            return bestLoq;
        }
    }
}
