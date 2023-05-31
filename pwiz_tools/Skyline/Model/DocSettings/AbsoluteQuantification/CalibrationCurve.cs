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
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public abstract class CalibrationCurve
    {
        // The CalibrationCurve that you get if you have no external standards.
        public static readonly CalibrationCurve NO_EXTERNAL_STANDARDS
            = new Simple(1);

        public double? GetY(double? x)
        {
            return x == null ? (double?) null : GetY(x.Value);
        }
        public abstract double GetY(double x);

        /// <summary>
        /// Returns the inverse of GetY
        /// </summary>
        public abstract double? GetX(double? y);

        /// <summary>
        /// Returns the same thing as GetX, except when this is a BilinearCalibrationCurve.
        /// This method is only used when calculating Limit of Detection by doing &lt;blank plus something>
        /// </summary>
        public virtual double? GetXValueForLimitOfDetection(double? y)
        {
            return GetX(y);
        }

        public CalibrationCurveMetrics GetMetrics(IList<WeightedPoint> points)
        {
            var calibrationCurveRow = CreateCalibrationCurveMetrics();
            if (points.Count != 0)
            {
                calibrationCurveRow = calibrationCurveRow.ChangePointCount(points.Count);
                double? rSquared = CalculateRSquared(points);
                if (rSquared.HasValue)
                {
                    calibrationCurveRow = calibrationCurveRow.ChangeRSquared(rSquared);
                }
            }
            return calibrationCurveRow;
        }

        protected abstract CalibrationCurveMetrics CreateCalibrationCurveMetrics();
        public abstract override string ToString();

        public virtual double? CalculateRSquared(IEnumerable<WeightedPoint> points)
        {
            List<double> yValues = new List<double>();
            List<double> residuals = new List<double>();
            foreach (var point in points)
            {
                double? yFitted = GetY(point.X);
                yValues.Add(point.Y);
                residuals.Add(point.Y - yFitted.Value);
            }
            if (!residuals.Any())
            {
                return null;
            }
            double yMean = yValues.Average();
            double totalSumOfSquares = yValues.Sum(y => (y - yMean) * (y - yMean));
            double sumOfSquaresOfResiduals = residuals.Sum(r => r * r);
            double rSquared = 1 - sumOfSquaresOfResiduals / totalSumOfSquares;
            return rSquared;
        }
        public class Linear : CalibrationCurve
        {
            public Linear(double slope, double? intercept)
            {
                Slope = slope;
                Intercept = intercept;
            }

            public double Slope { get; }
            public double? Intercept { get; }

            public override double GetY(double x)
            {
                return x * Slope + Intercept.GetValueOrDefault();
            }

            public override double? GetX(double? y)
            {
                return (y - Intercept.GetValueOrDefault()) / Slope;
            }

            protected override CalibrationCurveMetrics CreateCalibrationCurveMetrics()
            {
                return new CalibrationCurveMetrics().ChangeSlope(Slope).ChangeIntercept(Intercept);
            }

            public override string ToString()
            {
                if (Intercept.HasValue)
                {
                    return string.Format(@"y = {0}x + {1}", Slope, Intercept);
                }

                return string.Format(@"y = {0}x", Slope);
            }
        }
        public class Quadratic : CalibrationCurve
        {
            public Quadratic(double intercept, double slope, double quadraticCoefficient)
            {
                Intercept = intercept;
                Slope = slope;
                QuadraticCoefficient = quadraticCoefficient;
            }

            public double Intercept { get; }
            public double Slope { get; }
            public double QuadraticCoefficient { get; }

            public override double? GetX(double? y)
            {
                // Quadratic formula: x = (-b +/- sqrt(b^2-4ac))/2a
                double? a = QuadraticCoefficient;
                double? b = Slope;
                double? c = Intercept - y;

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

            public override double GetY(double x)
            {
                return x * x * QuadraticCoefficient + x * Slope + Intercept;
            }

            protected override CalibrationCurveMetrics CreateCalibrationCurveMetrics()
            {
                return new CalibrationCurveMetrics().ChangeIntercept(Intercept).ChangeSlope(Slope)
                    .ChangeQuadraticCoefficient(QuadraticCoefficient);
            }

            public override string ToString()
            {
                return string.Format(@"y = {0}x^2 + {1}x + {2}", QuadraticCoefficient, Slope, Intercept);
            }
        }

        public class LinearInLogSpace : CalibrationCurve
        {
            private readonly Linear _linearCalibrationCurve;
            public LinearInLogSpace(Linear linearCalibrationCurve)
            {
                _linearCalibrationCurve = linearCalibrationCurve;
            }

            public double Slope
            {
                get { return _linearCalibrationCurve.Slope; }
            }

            public double Intercept
            {
                get { return _linearCalibrationCurve.Intercept.GetValueOrDefault(); }
            }

            public override double GetY(double x)
            {
                var y = _linearCalibrationCurve.GetY(Math.Log(x));
                return Math.Exp(y);
            }

            public override double? GetX(double? y)
            {
                if (y.HasValue)
                {
                    var x = _linearCalibrationCurve.GetX(Math.Log(y.Value));
                    if (x.HasValue)
                    {
                        return Math.Exp(x.Value);
                    }
                }
                return null;
            }

            protected override CalibrationCurveMetrics CreateCalibrationCurveMetrics()
            {
                return new CalibrationCurveMetrics().ChangeSlope(Slope).ChangeIntercept(Intercept);
            }

            /// <summary>
            /// Linear in log space calibration curves, the R-squared is calculated using the logarithm
            /// of the points and 
            /// </summary>
            public override double? CalculateRSquared(IEnumerable<WeightedPoint> points)
            {
                return _linearCalibrationCurve.CalculateRSquared(points.Select(LogOfPoint).ToList());
            }

            public static WeightedPoint LogOfPoint(WeightedPoint weightedPoint)
            {
                return new WeightedPoint(Math.Log(weightedPoint.X), Math.Log(weightedPoint.Y), weightedPoint.Weight);
            }

            public override string ToString()
            {
                return string.Format(@"log(y) = {0} log(x) + {1}", _linearCalibrationCurve.Slope,
                    _linearCalibrationCurve.Intercept ?? 0);
            }
        }
        public class Bilinear : CalibrationCurve
        {
            private readonly Linear _linearCalibrationCurve;
            public Bilinear(Linear linearCalibrationCurve, double turningPoint)
            {
                _linearCalibrationCurve = linearCalibrationCurve;
                TurningPoint = turningPoint;
            }

            public double Slope
            {
                get { return _linearCalibrationCurve.Slope; }
            }

            public double Intercept
            {
                get
                {
                    return _linearCalibrationCurve.Intercept.GetValueOrDefault();
                }
            }

            public double TurningPoint { get; }

            public override double GetY(double x)
            {
                if (x < TurningPoint)
                {
                    return _linearCalibrationCurve.GetY(TurningPoint);
                }

                return _linearCalibrationCurve.GetY(x);
            }

            public override double? GetX(double? y)
            {
                double? x = _linearCalibrationCurve.GetX(y);
                if (x < TurningPoint)
                {
                    return null;
                }

                return x;
            }

            public override double? GetXValueForLimitOfDetection(double? y)
            {
                return _linearCalibrationCurve.GetX(y);
            }

            protected override CalibrationCurveMetrics CreateCalibrationCurveMetrics()
            {
                return new CalibrationCurveMetrics().ChangeSlope(Slope).ChangeIntercept(Intercept)
                    .ChangeTurningPoint(TurningPoint);
            }

            public override string ToString()
            {
                return _linearCalibrationCurve + string.Format(@"; x > {0}", TurningPoint);
            }

            public Linear GetLinearCalibrationCurve()
            {
                return _linearCalibrationCurve;
            }
        }

        public class Simple : CalibrationCurve
        {
            public Simple(double slope)
            {
                Slope = slope;
            }

            public double Slope { get; }
            public override double GetY(double x)
            {
                return x * Slope;
            }

            public override double? GetX(double? y)
            {
                return y / Slope;
            }

            protected override CalibrationCurveMetrics CreateCalibrationCurveMetrics()
            {
                return new CalibrationCurveMetrics().ChangeSlope(Slope).ChangePointCount(0);
            }

            public override string ToString()
            {
                return string.Format(@"y = {0}x", Slope);
            }
        }

        public class Error : CalibrationCurve
        {
            public Error(string message)
            {
                ErrorMessage = message;
            }

            public string ErrorMessage { get; }
            public override double GetY(double x)
            {
                return 0;
            }

            public override double? GetX(double? y)
            {
                return null;
            }

            protected override CalibrationCurveMetrics CreateCalibrationCurveMetrics()
            {
                return new CalibrationCurveMetrics().ChangeErrorMessage(ErrorMessage);
            }

            public override string ToString()
            {
                return @"Error: " + ErrorMessage;
            }
        }
    }
}
