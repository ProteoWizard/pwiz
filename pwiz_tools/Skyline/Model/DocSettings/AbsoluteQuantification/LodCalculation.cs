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
            () => QuantificationStrings.LodCalculation_NONE_None, args => null);
        public static readonly LodCalculation TURNING_POINT = new LodCalculation(@"turning_point",
            () => QuantificationStrings.LodCalculation_TURNING_POINT_Bilinear_turning_point_legacy, CalculateLodFromTurningPoint);
        public static readonly LodCalculation BLANK_PLUS_2SD = new LodCalculation(@"blank_plus_2_sd",
            () => QuantificationStrings.LodCalculation_BLANK_PLUS_2SD_Blank_plus_2___SD,
            args=>BlankPlusSdMultiple(args, 2.0));
        public static readonly LodCalculation BLANK_PLUS_3SD = new LodCalculation(@"blank_plus_3_sd",
            () => QuantificationStrings.LodCalculation_BLANK_PLUS_3SD_Blank_plus_3___SD,
            args=>BlankPlusSdMultiple(args, 3.0));

        public static readonly LodCalculation TURNING_POINT_STDERR = new LodCalculation(@"turning_point_stderr",
            () => QuantificationStrings.LodCalculation_TURNING_POINT_STDERR_Bilinear_turning_point, CalculateLodFromTurningPointWithStdErr);

        private static readonly ImmutableList<LodCalculation> ALL =
            ImmutableList.ValueOf(new[]
            {
                NONE, BLANK_PLUS_2SD, BLANK_PLUS_3SD, TURNING_POINT, TURNING_POINT_STDERR
            });

        public static IEnumerable<LodCalculation> ForRegressionFit(RegressionFit regressionFit)
        {
            if (regressionFit == RegressionFit.BILINEAR)
            {
                return new[]
                {
                    NONE, TURNING_POINT_STDERR, BLANK_PLUS_2SD, BLANK_PLUS_3SD, TURNING_POINT
                };
            }

            return new[] { NONE, BLANK_PLUS_2SD, BLANK_PLUS_3SD };
        }

        private readonly Func<LodCalculationArgs, double?> _calculateLodFunc;

        private LodCalculation(string name, Func<string> getLabelFunc, Func<LodCalculationArgs, double?> calculateLodFunc) :
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


        public double? CalculateLod(LodCalculationArgs args)
        {
            return _calculateLodFunc(args);
        }

        public static double? CalculateLodFromTurningPoint(LodCalculationArgs args)
        {
            if (args.CalibrationCurve is CalibrationCurve.Bilinear bilinear)
            {
                return bilinear.TurningPoint;
            }

            if (args.CalibrationCurve is CalibrationCurve.Linear)
            {
                return args.Standards.Min(pt => pt.X);
            }

            return null;
        }

        public static double? CalculateLodFromTurningPointWithStdErr(LodCalculationArgs args)
        {
            return BootstrapFiguresOfMeritCalculator.ComputeLod(args.Standards);
        }

        public static double? BlankPlusSdMultiple(LodCalculationArgs args, double sdMultiple)
        {
            var blankPeakAreas = args.Blanks;
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
            return args.CalibrationCurve.GetXValueForLimitOfDetection(meanPlusSd);
        }
        public class LodCalculationArgs
        {
            public LodCalculationArgs(CalibrationCurve calibrationCurve, IEnumerable<WeightedPoint> standards,
                IEnumerable<double> blanks)
            {
                CalibrationCurve = calibrationCurve;
                Standards = ImmutableList.ValueOf(standards);
                Blanks = ImmutableList.ValueOf(blanks);
            }
            public CalibrationCurve CalibrationCurve { get; }
            public ImmutableList<WeightedPoint> Standards { get; }
            public ImmutableList<double> Blanks { get; }
        }
    }
}
