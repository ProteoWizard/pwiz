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

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class RegressionFit : IAuditLogObject
    {
        public static readonly RegressionFit NONE = new RegressionFit("none", // Not L10N
            ()=>QuantificationStrings.RegressionFit_NONE_None, NoExternalStandards);

        public static readonly RegressionFit LINEAR = new RegressionFit("linear", // Not L10N
            () => QuantificationStrings.RegressionFit_LINEAR_Linear, LinearFit);

        public static readonly RegressionFit LINEAR_THROUGH_ZERO = new RegressionFit("linear_through_zero", // Not L10N
            () => QuantificationStrings.RegressionFit_LINEAR_THROUGH_ZERO_Linear_through_zero, LinearFitThroughZero);

        public static readonly RegressionFit QUADRATIC = new RegressionFit("quadratic", // Not L10N
            () => QuantificationStrings.RegressionFit_QUADRATIC_Quadratic, QuadraticFit);

        public static readonly RegressionFit BILINEAR = new RegressionFit("bilinear", // Not L10N
            () => QuantificationStrings.RegressionFit_BILINEAR_Bilinear, BilinearFit);
        public static readonly ImmutableList<RegressionFit> All = ImmutableList<RegressionFit>.ValueOf(new[]
        {
            NONE, LINEAR_THROUGH_ZERO, LINEAR, BILINEAR, QUADRATIC
        });

        private readonly Func<String> _getLabelFunc;
        private readonly Func<IList<WeightedPoint>, CalibrationCurve> _fitFunc;
        private RegressionFit(String name, Func<String> getLabelFunc, Func<IList<WeightedPoint>, CalibrationCurve> fitFunc)
        {
            Name = name;
            _getLabelFunc = getLabelFunc;
            _fitFunc = fitFunc;
        }

        public string Name { get; private set; }

        public string AuditLogText { get { return Name; } }
        public bool IsName { get { return true; } }

        public override string ToString()
        {
            return _getLabelFunc();
        }

        public CalibrationCurve Fit(IList<WeightedPoint> points)
        {
            try
            {
                CalibrationCurve curve = _fitFunc(points);
                if (curve != null)
                {
                    curve = AddRSquared(curve, points);
                }
                return curve;
            }
            catch (Exception e)
            {
                return new CalibrationCurve().ChangeErrorMessage(e.Message);
            }
        }

        public CalibrationCurve AddRSquared(CalibrationCurve curve, IList<WeightedPoint> points)
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

        public static CalibrationCurve NoExternalStandards(IList<WeightedPoint> points)
        {
            return CalibrationCurve.NO_EXTERNAL_STANDARDS;
        }

        public static CalibrationCurve LinearFit(IList<WeightedPoint> points)
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

        public static CalibrationCurve QuadraticFit(IList<WeightedPoint> points)
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
        #region BilinearFit
        public static CalibrationCurve BilinearFit(IList<WeightedPoint> weightedPoints)
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
            return linearCalibrationCurve.ChangeTurningPoint(lod);
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
        #endregion
    }
}
