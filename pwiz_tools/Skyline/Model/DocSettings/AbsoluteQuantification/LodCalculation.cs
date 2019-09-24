/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class LodCalculation : LabeledValues<string>
    {
        public static readonly LodCalculation NONE = new LodCalculation(@"none",
            () => QuantificationStrings.LodCalculation_NONE_None, (curve, fitter) => null);
        public static readonly LodCalculation TURNING_POINT = new LodCalculation(@"turning_point",
            () => QuantificationStrings.LodCalculation_TURNING_POINT_Bilinear_turning_point, CalculateLodFromTurningPoint);
        public static readonly LodCalculation BLANK_PLUS_2SD = new LodCalculation(@"blank_plus_2_sd",
            () => QuantificationStrings.LodCalculation_BLANK_PLUS_2SD_Blank_plus_2___SD,
            (curve, fitter)=>BlankPlusSdMultiple(curve, fitter, 2.0));
        public static readonly LodCalculation BLANK_PLUS_3SD = new LodCalculation(@"blank_plus_3_sd",
            () => QuantificationStrings.LodCalculation_BLANK_PLUS_3SD_Blank_plus_3___SD,
            (curve, fitter)=>BlankPlusSdMultiple(curve, fitter, 3.0));

        public static readonly ImmutableList<LodCalculation> ALL = ImmutableList.ValueOf(new[]
        {
            NONE, BLANK_PLUS_2SD, BLANK_PLUS_3SD, TURNING_POINT
        });

        private readonly Func<CalibrationCurve, CalibrationCurveFitter, double?> _calculateLodFunc;

        private LodCalculation(string name, Func<string> getLabelFunc, Func<CalibrationCurve, CalibrationCurveFitter, double?> calculateLodFunc) :
            base(name, getLabelFunc)
        {
            _calculateLodFunc = calculateLodFunc;
        }

        public override string ToString()
        {
            return Label;
        }

        public static LodCalculation Parse(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return NONE;
            }
            return ALL.FirstOrDefault(calc => calc.Name == name) ?? NONE;
        }


        public double? CalculateLod(CalibrationCurve calibrationCurve, CalibrationCurveFitter calibrationCurveFitter)
        {
            return _calculateLodFunc(calibrationCurve, calibrationCurveFitter);
        }

        public static double? CalculateLodFromTurningPoint(CalibrationCurve calibrationCurve,
            CalibrationCurveFitter fitter)
        {
            return calibrationCurve.TurningPoint;
        }

        public static double? BlankPlusSdMultiple(CalibrationCurve calibrationCurve, CalibrationCurveFitter fitter, double sdMultiple)
        {
            List<double> blankPeakAreas = new List<double>();
            var measuredResults = fitter.SrmSettings.MeasuredResults;
            if (measuredResults == null)
            {
                return null;
            }
            for (int iReplicate = 0; iReplicate < measuredResults.Chromatograms.Count; iReplicate++)
            {
                var chromatogramSet = measuredResults.Chromatograms[iReplicate];
                if (!SampleType.BLANK.Equals(chromatogramSet.SampleType))
                {
                    continue;
                }
                double? peakArea = fitter.GetNormalizedPeakArea(new CalibrationPoint(iReplicate, null));
                if (!peakArea.HasValue)
                {
                    continue;
                }
                blankPeakAreas.Add(peakArea.Value);
            }
            if (!blankPeakAreas.Any())
            {
                return null;
            }
            double meanPlusSd = blankPeakAreas.Mean();
            if (sdMultiple != 0)
            {
                meanPlusSd += blankPeakAreas.StandardDeviation() * sdMultiple;
            }
            if (double.IsNaN(meanPlusSd) || double.IsInfinity(meanPlusSd))
            {
                return null;
            }
            return calibrationCurve.GetFittedX(meanPlusSd);
        }
    }
}
