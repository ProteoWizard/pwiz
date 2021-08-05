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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class CalibrationCurve : Immutable, IComparable
    {
        // The CalibrationCurve that you get if you have no external standards.
        public static readonly CalibrationCurve NO_EXTERNAL_STANDARDS
            = new CalibrationCurve(RegressionFit.NONE).ChangePointCount(0).ChangeSlope(1.0);

        public CalibrationCurve(RegressionFit regressionFit)
        {
            RegressionFit = regressionFit;
        }

        public CalibrationCurve() : this(RegressionFit.NONE)
        {
        }

        [Format(Formats.CalibrationCurve, NullValue = TextUtil.EXCEL_NA)]
        public double? Slope { get; private set; }

        public CalibrationCurve ChangeSlope(double? slope)
        {
            return ChangeProp(ImClone(this), im => im.Slope = slope);
        }
        [Format(Formats.CalibrationCurve, NullValue = TextUtil.EXCEL_NA)]
        public double? Intercept { get; private set; }

        public CalibrationCurve ChangeIntercept(double? intercept)
        {
            return ChangeProp(ImClone(this), im => im.Intercept = intercept);
        }

        [Format(Formats.CalibrationCurve, NullValue = TextUtil.EXCEL_NA)]
        public double? TurningPoint { get; private set; }

        public CalibrationCurve ChangeTurningPoint(double? turningPoint)
        {
            return ChangeProp(ImClone(this), im => im.TurningPoint = turningPoint);
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? PointCount { get; private set; }

        public CalibrationCurve ChangePointCount(int? pointCount)
        {
            return ChangeProp(ImClone(this), im => im.PointCount = pointCount);
        }
        
        [Format(Formats.CalibrationCurve, NullValue=TextUtil.EXCEL_NA)]
        public double? QuadraticCoefficient { get; private set; }

        public CalibrationCurve ChangeQuadraticCoefficient(double? quadraticCoefficient)
        {
            return ChangeProp(ImClone(this), im => im.QuadraticCoefficient = quadraticCoefficient);
        }

        [Browsable(false)]
        public RegressionFit RegressionFit { get; private set; }

        public CalibrationCurve ChangeRegressionFit(RegressionFit regressionFit)
        {
            return ChangeProp(ImClone(this), im => im.RegressionFit = regressionFit);
        }

        [Format("0.####", NullValue = TextUtil.EXCEL_NA)]
        public double? RSquared { get; private set; }

        public static string RSquaredDisplayText(double rSquared)
        {
            return QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_ +
                   rSquared.ToString(@"0.####");
        }

        public CalibrationCurve ChangeRSquared(double? rSquared)
        {
            return ChangeProp(ImClone(this), im => im.RSquared = rSquared);
        }

        public string ErrorMessage { get; private set; }

        public CalibrationCurve ChangeErrorMessage(string message)
        {
            return ChangeProp(ImClone(this), im => im.ErrorMessage = message);
        }

        public double? GetY(double? x)
        {
            return RegressionFit.GetY(this, x);
        }

        public double? GetFittedX(double? y)
        {
            return RegressionFit.GetFittedX(this, y);
        }

        public double? GetX(double? y)
        {
            return RegressionFit.GetX(this, y);
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                return TextUtil.SpaceSeparate(QuantificationStrings.CalibrationCurve_ToString_Error__, ErrorMessage);
            }
            return Format(Slope, Intercept, QuadraticCoefficient, TurningPoint);
        }

        public static string Format(double? slope, double? intercept, double? quadraticCoefficient = null, double? turningPoint = null)
        {
            List<string> parts = new List<string>();
            if (quadraticCoefficient.HasValue)
            {
                parts.Add(string.Format(QuantificationStrings.CalibrationCurve_ToString_y_x_2_x_c,
                    quadraticCoefficient.Value.ToString(Formats.CalibrationCurve),
                    slope.Value.ToString(Formats.CalibrationCurve),
                    intercept.Value.ToString(Formats.CalibrationCurve)));
            }
            else if (slope.HasValue)
            {
                parts.Add(String.Format(QuantificationStrings.CalibrationCurve_ToString_Slope___0_,
                    slope.Value.ToString(Formats.CalibrationCurve)));
                if (intercept.HasValue)
                {
                    parts.Add(string.Format(QuantificationStrings.CalibrationCurve_ToString_Intercept___0_,
                        intercept.Value.ToString(Formats.CalibrationCurve)));
                }
            }
            if (turningPoint.HasValue)
            {
                parts.Add(string.Format(QuantificationStrings.CalibrationCurve_ToString_Turning_Point___0_,
                    turningPoint.Value.ToString(Formats.CalibrationCurve)));
            }
            return TextUtil.SpaceSeparate(parts);
        }

        public int CompareTo(object obj)
        {
            if (null == obj)
            {
                return 1;
            }
            return Comparer.Default.Compare(GetCompareKey(), ((CalibrationCurve) obj).GetCompareKey());
        }

        private Tuple<double?, double?> GetCompareKey()
        {
            return new Tuple<double?, double?>(Slope, Intercept);
        }
    }
}
