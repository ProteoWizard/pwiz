/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearRegression;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public abstract class RegressionFit : LabeledValues<string>
    {
        public static readonly RegressionFit NONE = new SimpleRegressionFit(@"none",
            ()=>QuantificationStrings.RegressionFit_NONE_None, NoExternalStandards);

        public static readonly RegressionFit LINEAR = new SimpleRegressionFit(@"linear",
            () => QuantificationStrings.RegressionFit_LINEAR_Linear, LinearFit);

        public static readonly RegressionFit LINEAR_THROUGH_ZERO = new SimpleRegressionFit(@"linear_through_zero",
            () => QuantificationStrings.RegressionFit_LINEAR_THROUGH_ZERO_Linear_through_zero, LinearFitThroughZero);

        public static readonly RegressionFit QUADRATIC = new QuadraticFit();

        public static readonly RegressionFit BILINEAR = new BilinearFit();

        public static readonly RegressionFit LINEAR_IN_LOG_SPACE = new LinearInLogSpace();
        public static readonly ImmutableList<RegressionFit> All = ImmutableList<RegressionFit>.ValueOf(new[]
        {
            NONE, LINEAR_THROUGH_ZERO, LINEAR, BILINEAR, QUADRATIC, LINEAR_IN_LOG_SPACE
        });

        protected RegressionFit(string name, Func<string> getLabelFunc) : base(name, getLabelFunc)
        {
        }

        public virtual CalibrationCurve Fit(IList<WeightedPoint> points)
        {
            try
            {
                CalibrationCurve curve = FitPoints(points);
                if (curve != null)
                {
                    curve = curve.ChangeRegressionFit(this);
                    curve = AddRSquared(curve, points);
                }
                return curve;
            }
            catch (Exception e)
            {
                return new CalibrationCurve(this).ChangeErrorMessage(e.Message);
            }
        }

        public override string ToString()
        {
            return Label;
        }

        public virtual double? GetY(CalibrationCurve calibrationCurve, double? x)
        {
            return x * calibrationCurve.Slope + calibrationCurve.Intercept.GetValueOrDefault();
        }

        public virtual double? GetFittedX(CalibrationCurve calibrationCurve, double? y)
        {
            return (y - calibrationCurve.Intercept.GetValueOrDefault()) / calibrationCurve.Slope;
        }

        public virtual double? GetX(CalibrationCurve calibrationCurve, double? y)
        {
            return GetFittedX(calibrationCurve, y);
        }

        protected abstract CalibrationCurve FitPoints(IList<WeightedPoint> points);

        private class SimpleRegressionFit : RegressionFit
        {
            private readonly Func<IList<WeightedPoint>, CalibrationCurve> _fitFunc;
            public SimpleRegressionFit(String name, Func<String> getLabelFunc, Func<IList<WeightedPoint>, CalibrationCurve> fitFunc) : base(name, getLabelFunc)
            {
                _fitFunc = fitFunc;
            }

            protected override CalibrationCurve FitPoints(IList<WeightedPoint> points)
            {
                return _fitFunc(points);
            }
        }

        protected CalibrationCurve AddRSquared(CalibrationCurve curve, IList<WeightedPoint> points)
        {
            List<double> yValues = new List<double>();
            List<double> residuals = new List<double>();
            foreach (var point in points)
            {
                double? yFitted = curve.GetY(point.X);
                if (!yFitted.HasValue)
                {
                    continue;
                }
                yValues.Add(point.Y);
                residuals.Add(point.Y - yFitted.Value);
            }
            if (!residuals.Any())
            {
                return curve;
            }
            double yMean = yValues.Average();
            double totalSumOfSquares = yValues.Sum(y => (y - yMean)*(y - yMean));
            double sumOfSquaresOfResiduals = residuals.Sum(r => r*r);
            double rSquared = 1 - sumOfSquaresOfResiduals/totalSumOfSquares;
            return curve.ChangeRSquared(rSquared);
        }

        protected static CalibrationCurve NoExternalStandards(IList<WeightedPoint> points)
        {
            return CalibrationCurve.NO_EXTERNAL_STANDARDS;
        }

        protected static CalibrationCurve LinearFit(IList<WeightedPoint> points)
        {
            CalibrationCurve calibrationCurve = new CalibrationCurve().ChangePointCount(points.Count);
            try
            {
                double[] values = WeightedRegression.Weighted(points.Select(p => new Tuple<double[], double>(new[] {p.X}, p.Y)),
                    points.Select(p => p.Weight).ToArray(), true);
                calibrationCurve = calibrationCurve.ChangeSlope(values[1]).ChangeIntercept(values[0]);
            }
            catch (Exception ex)
            {
                calibrationCurve = calibrationCurve.ChangeErrorMessage(ex.Message);
            }
            return calibrationCurve;
        }

        public static CalibrationCurve LinearFitThroughZero(IList<WeightedPoint> points)
        {
            // ReSharper disable RedundantArgumentDefaultValue
            var values = WeightedRegression.Weighted(points.Select(p => new Tuple<double[], double>(new[] { p.X }, p.Y)),
                points.Select(p => p.Weight).ToArray(), false);
            // ReSharper restore RedundantArgumentDefaultValue
            return new CalibrationCurve().ChangePointCount(points.Count).ChangeSlope(values[0]);
        }

        public static CalibrationCurve QuadraticFit2(IList<WeightedPoint> points)
        {
            double[] result = MathNet.Numerics.Fit.PolynomialWeighted(
                points.Select(p => p.X).ToArray(),
                points.Select(p => p.Y).ToArray(),
                points.Select(p => p.Weight).ToArray(),
                2
                );
            return new CalibrationCurve().ChangePointCount(points.Count)
                .ChangeQuadraticCoefficient(result[2])
                .ChangeSlope(result[1])
                .ChangeIntercept(result[0]);
        }

        public static RegressionFit Parse(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return NONE;
            }
            return All.FirstOrDefault(fit => fit.Name == name) ?? LINEAR;
        }

        private class QuadraticFit : RegressionFit
        {
            public QuadraticFit() : base(@"quadratic", () => QuantificationStrings.RegressionFit_QUADRATIC_Quadratic)
            {
                
            }

            protected override CalibrationCurve FitPoints(IList<WeightedPoint> points)
            {
                double[] result = MathNet.Numerics.Fit.PolynomialWeighted(
                    points.Select(p => p.X).ToArray(),
                    points.Select(p => p.Y).ToArray(),
                    points.Select(p => p.Weight).ToArray(),
                    2
                );
                return new CalibrationCurve().ChangePointCount(points.Count)
                    .ChangeQuadraticCoefficient(result[2])
                    .ChangeSlope(result[1])
                    .ChangeIntercept(result[0]);
            }

            public override double? GetFittedX(CalibrationCurve calibrationCurve, double? y)
            {
                // Quadratic formula: x = (-b +/- sqrt(b^2-4ac))/2a
                double? a = calibrationCurve.QuadraticCoefficient;
                double? b = calibrationCurve.Slope;
                double? c = calibrationCurve.Intercept - y;

                double? discriminant = b * b - 4 * a * c;
                if (!discriminant.HasValue)
                {
                    return null;
                }
                if (discriminant < 0)
                {
                    return double.NaN;
                }
                double sqrtDiscriminant = Math.Sqrt(discriminant.Value);
                return (-b + sqrtDiscriminant) / 2 / a;
            }

            public override double? GetY(CalibrationCurve calibrationCurve, double? x)
            {
                return x * x * calibrationCurve.QuadraticCoefficient + x * calibrationCurve.Slope + calibrationCurve.Intercept;
            }
        }

        private class BilinearFit : RegressionFit
        {
            public BilinearFit() : base(@"bilinear", () => QuantificationStrings.RegressionFit_BILINEAR_Bilinear)
            {
                
            }

            protected override CalibrationCurve FitPoints(IList<WeightedPoint> weightedPoints)
            {
                double? bestLod = null;
                double bestScore = double.MaxValue;
                var xValues = weightedPoints.Select(pt => pt.X).Distinct().OrderBy(x => x).ToArray();
                for (int i = 0; i < xValues.Length - 1; i++)
                {
                    var simplexConstant = new NelderMeadSimplex.SimplexConstant((xValues[i] + xValues[i + 1]) / 2,
                        (xValues[i + 1] - xValues[i]) / 4);
                    var regressionResult = NelderMeadSimplex.Regress(new[] { simplexConstant }, 0, 50,
                        constants => LodObjectiveFunction(constants[0], weightedPoints));
                    if (regressionResult.ErrorValue < bestScore)
                    {
                        bestLod = regressionResult.Constants[0];
                        bestScore = regressionResult.ErrorValue;
                    }
                }
                if (!bestLod.HasValue)
                {
                    return LinearFit(weightedPoints);
                }
                return GetCalibrationCurveWithLod(bestLod.Value, weightedPoints);
            }
            private static CalibrationCurve GetCalibrationCurveWithLod(double lod, IList<WeightedPoint> weightedPoints)
            {
                var linearPoints = weightedPoints.Select(pt => pt.X > lod ? pt : new WeightedPoint(lod, pt.Y, pt.Weight)).ToArray();
                if (linearPoints.Select(p => p.X).Distinct().Count() <= 1)
                {
                    return null;
                }
                var linearCalibrationCurve = LinearFit(linearPoints);
                if (!string.IsNullOrEmpty(linearCalibrationCurve.ErrorMessage))
                {
                    return null;
                }
                return linearCalibrationCurve.ChangeTurningPoint(lod).ChangeRegressionFit(BILINEAR);
            }

            /// <summary>
            /// Optimization function used when doing NelderMeadSimplex to find the best Limit of Detection.
            /// </summary>
            private static double LodObjectiveFunction(double lod, IList<WeightedPoint> weightedPoints)
            {
                CalibrationCurve calibrationCurve = GetCalibrationCurveWithLod(lod, weightedPoints);
                if (calibrationCurve == null || !calibrationCurve.TurningPoint.HasValue)
                {
                    return double.MaxValue;
                }
                double totalDelta = 0;
                double totalWeight = 0;
                foreach (var pt in weightedPoints)
                {
                    double delta = pt.Y - calibrationCurve.GetY(pt.X).Value;
                    totalWeight += pt.Weight;
                    totalDelta += pt.Weight * delta * delta;
                }
                return totalDelta / totalWeight;
            }

            public override double? GetX(CalibrationCurve calibrationCurve, double? y)
            {
                double? x = GetFittedX(calibrationCurve, y);
                if (x.HasValue && calibrationCurve.TurningPoint.HasValue && x < calibrationCurve.TurningPoint)
                {
                    return null;
                }
                return x;
            }

            public override double? GetY(CalibrationCurve calibrationCurve, double? x)
            {
                if (calibrationCurve.TurningPoint.HasValue && x < calibrationCurve.TurningPoint)
                {
                    x = calibrationCurve.TurningPoint;
                }
                return base.GetY(calibrationCurve, x);
            }
        }

        private class LinearInLogSpace : RegressionFit
        {
            public LinearInLogSpace() : base(@"linear_in_log_space", () => Resources.LinearInLogSpace_Label_Linear_in_Log_Space)
            {
                
            }

            protected override CalibrationCurve FitPoints(IList<WeightedPoint> points)
            {
                if (points.Any(pt => pt.Y <= 0 || pt.X <= 0))
                {
                    return new CalibrationCurve(this).ChangeErrorMessage(Resources.LinearInLogSpace_FitPoints_Unable_to_do_a_regression_in_log_space_because_one_or_more_points_are_non_positive_);
                }
                var logPoints = points.Select(pt => new WeightedPoint(Math.Log(pt.X), Math.Log(pt.Y), pt.Weight)).ToArray();
                var calibrationCurve = LinearFit(logPoints);
                calibrationCurve.ChangeRegressionFit(this);
                return calibrationCurve;
            }

            public override double? GetFittedX(CalibrationCurve calibrationCurve, double? y)
            {
                if (y.HasValue)
                {
                    var x = base.GetFittedX(calibrationCurve, Math.Log(y.Value));
                    if (x.HasValue)
                    {
                        return Math.Exp(x.Value);
                    }
                }
                return null;
            }

            public override double? GetX(CalibrationCurve calibrationCurve, double? y)
            {
                return GetFittedX(calibrationCurve, y);
            }

            public override double? GetY(CalibrationCurve calibrationCurve, double? x)
            {
                if (x.HasValue)
                {
                    var y = base.GetY(calibrationCurve, Math.Log(x.Value));
                    if (y.HasValue)
                    {
                        return Math.Exp(y.Value);
                    }
                }
                return null;
            }
        }

    }
}
