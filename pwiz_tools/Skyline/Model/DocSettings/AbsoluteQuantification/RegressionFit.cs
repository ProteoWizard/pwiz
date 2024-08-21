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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public abstract class RegressionFit : LabeledValues<string>
    {
        public static readonly RegressionFit NONE = new SimpleRegressionFit(@"none",
            ()=>QuantificationStrings.RegressionFit_NONE_None, points=>CalibrationCurve.NO_EXTERNAL_STANDARDS);

        public static readonly RegressionFit LINEAR = new SimpleRegressionFit(@"linear",
            () => QuantificationStrings.RegressionFit_LINEAR_Linear, LinearFit);

        public static readonly RegressionFit LINEAR_THROUGH_ZERO = new SimpleRegressionFit(@"linear_through_zero",
            () => QuantificationStrings.RegressionFit_LINEAR_THROUGH_ZERO_Linear_through_zero, LinearFitThroughZero);

        public static readonly RegressionFit QUADRATIC = new QuadraticFit();

        public static readonly RegressionFit BILINEAR = new BilinearRegressionFit();

        public static readonly RegressionFit LINEAR_IN_LOG_SPACE = new LinearInLogSpace();
        public static readonly ImmutableList<RegressionFit> All = ImmutableList<RegressionFit>.ValueOf(new[]
        {
            NONE, LINEAR_THROUGH_ZERO, LINEAR, BILINEAR, QUADRATIC, LINEAR_IN_LOG_SPACE
        });

        protected RegressionFit(string name, Func<string> getLabelFunc) : base(name, getLabelFunc)
        {
        }

        public CalibrationCurve Fit(IList<WeightedPoint> points)
        {
            try
            {
                CalibrationCurve curve = FitPoints(points);
                return curve;
            }
            catch (Exception e)
            {
                return new CalibrationCurve.Error(e.Message);
            }
        }

        public CalibrationCurveMetrics GetCalibrationCurveMetrics(IList<WeightedPoint> points)
        {
            return Fit(points).GetMetrics(points);
        }

        public override string ToString()
        {
            return Label;
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

        protected static CalibrationCurve LinearFit(IList<WeightedPoint> points)
        {
            double[] values = WeightedRegression.Weighted(points.Select(p => new Tuple<double[], double>(new[] {p.X}, p.Y)),
                points.Select(p => p.Weight).ToArray(), true);
            return new CalibrationCurve.Linear(values[1], values[0]);
        }

        protected static CalibrationCurve LinearFitThroughZero(IList<WeightedPoint> points)
        {
            // ReSharper disable RedundantArgumentDefaultValue
            var values = WeightedRegression.Weighted(points.Select(p => new Tuple<double[], double>(new[] { p.X }, p.Y)),
                points.Select(p => p.Weight).ToArray(), false);
            // ReSharper restore RedundantArgumentDefaultValue
            return new CalibrationCurve.Linear(values[0], null);
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
                return new CalibrationCurve.Quadratic(result[0], result[1], result[2]);
            }
        }

        private class LinearInLogSpace : RegressionFit
        {
            public LinearInLogSpace() : base(@"linear_in_log_space", () => AbsoluteQuantificationResources.LinearInLogSpace_Label_Linear_in_Log_Space)
            {
                
            }

            protected override CalibrationCurve FitPoints(IList<WeightedPoint> points)
            {
                if (points.Any(pt => pt.Y <= 0 || pt.X <= 0))
                {
                    return new CalibrationCurve.Error(AbsoluteQuantificationResources.LinearInLogSpace_FitPoints_Unable_to_do_a_regression_in_log_space_because_one_or_more_points_are_non_positive_);
                }
                var logPoints = points.Select(LogOfPoint).ToList();
                var calibrationCurve = (CalibrationCurve.Linear) LinearFit(logPoints);
                return new CalibrationCurve.LinearInLogSpace(calibrationCurve);
            }

            protected WeightedPoint LogOfPoint(WeightedPoint pt)
            {
                return CalibrationCurve.LinearInLogSpace.LogOfPoint(pt);
            }
        }

    }
}
