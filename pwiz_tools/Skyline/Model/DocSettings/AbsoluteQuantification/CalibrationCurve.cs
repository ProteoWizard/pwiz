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
            = new CalibrationCurve().ChangePointCount(0).ChangeSlope(1.0);

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

        [Format("0.####", NullValue = TextUtil.EXCEL_NA)]
        public double? RSquared { get; private set; }

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
            if (QuadraticCoefficient.HasValue)
            {
                return x*x*QuadraticCoefficient.Value + x*Slope + Intercept.GetValueOrDefault();
            }
            return x*Slope + Intercept.GetValueOrDefault();
        }

        public double? GetX(double? y)
        {
            if (QuadraticCoefficient.HasValue)
            {
                var discriminant = Slope*Slope - 4*QuadraticCoefficient*(Intercept - y);
                if (!discriminant.HasValue)
                {
                    return null;
                }
                if (discriminant < 0)
                {
                    return double.NaN;
                }
                double sqrtDiscriminant = Math.Sqrt(discriminant.Value);
                return (-Slope.Value + sqrtDiscriminant)/2/QuadraticCoefficient.Value;
            }
            return (y - Intercept.GetValueOrDefault())/Slope;
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                return TextUtil.SpaceSeparate(QuantificationStrings.CalibrationCurve_ToString_Error__, ErrorMessage);
            }
            if (QuadraticCoefficient.HasValue)
            {
                return string.Format(QuantificationStrings.CalibrationCurve_ToString_y_x_2_x_c,
                    QuadraticCoefficient.Value.ToString(Formats.CalibrationCurve),
                    Slope.Value.ToString(Formats.CalibrationCurve),
                    Intercept.Value.ToString(Formats.CalibrationCurve));
            }
            if (Slope.HasValue)
            {
                if (Intercept.HasValue)
                {
                    return string.Format(QuantificationStrings.CalibrationCurve_ToString_Slope___0__Intercept___1_,
                        Slope.Value.ToString(Formats.CalibrationCurve), 
                        Intercept.Value.ToString(Formats.CalibrationCurve));
                }
                return String.Format(QuantificationStrings.CalibrationCurve_ToString_Slope___0_, 
                    Slope.Value.ToString(Formats.CalibrationCurve));
            }
            return string.Empty;
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
