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

using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public abstract class CalibrationCurve
    {
        // The CalibrationCurve that you get if you have no external standards.
        public static readonly CalibrationCurve NO_EXTERNAL_STANDARDS
            = new LinearCalibrationCurve(1, null);

        public abstract double? GetY(double? x);

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

        public CalibrationCurveMetrics MakeCalibrationCurveRow(IList<WeightedPoint> points)
        {
            var calibrationCurveRow = ToCalibrationCurveRow();
            return AddRSquared(calibrationCurveRow, points);
        }

        protected abstract CalibrationCurveMetrics ToCalibrationCurveRow();

        protected CalibrationCurveMetrics AddRSquared(CalibrationCurveMetrics curve, IList<WeightedPoint> points)
        {
            List<double> yValues = new List<double>();
            List<double> residuals = new List<double>();
            foreach (var point in points)
            {
                double? yFitted = GetY(point.X);
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
            double totalSumOfSquares = yValues.Sum(y => (y - yMean) * (y - yMean));
            double sumOfSquaresOfResiduals = residuals.Sum(r => r * r);
            double rSquared = 1 - sumOfSquaresOfResiduals / totalSumOfSquares;
            return curve.ChangeRSquared(rSquared);
        }
    }
}
