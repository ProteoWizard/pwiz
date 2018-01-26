using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class LodCalculation
    {
        public static readonly LodCalculation NONE = new LodCalculation("none", ()=>"None", (curve, fitter)=>null);
        public static readonly LodCalculation TURNING_POINT = new LodCalculation(
            "turning_point", ()=>"Turning Point", CalculateLodFromTurningPoint);
        public static readonly LodCalculation BLANK_PLUS_2SD = new LodCalculation(
            "blank_plus_2_sd", ()=>"Blank plus 2 * SD",
            (curve, fitter)=>BlankPlusSdMultiple(curve, fitter, 2.0));
        public static readonly LodCalculation BLANK_PLUS_3SD = new LodCalculation(
            "blank_plus_3_sd", ()=>"Blank plus 3 * SD",
            (curve, fitter)=>BlankPlusSdMultiple(curve, fitter, 3.0));

        public static readonly ImmutableList<LodCalculation> ALL = ImmutableList.ValueOf(new[]
        {
            NONE, BLANK_PLUS_2SD, BLANK_PLUS_3SD, TURNING_POINT
        });

        private readonly Func<CalibrationCurve, CalibrationCurveFitter, double?> _calculateLodFunc;
        private readonly Func<string> _getLabelFunc;

        private LodCalculation(string name, Func<string> getLabelFunc, Func<CalibrationCurve, CalibrationCurveFitter, double?> calculateLodFunc)
        {
            Name = name;
            _getLabelFunc = getLabelFunc;
            _calculateLodFunc = calculateLodFunc;
        }

        public string Name { get; private set; }

        public string Label { get { return _getLabelFunc(); } }

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
                double? peakArea = fitter.GetNormalizedPeakArea(iReplicate);
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
                meanPlusSd = blankPeakAreas.StandardDeviation() * sdMultiple;
            }
            if (double.IsNaN(meanPlusSd) || double.IsInfinity(meanPlusSd))
            {
                return null;
            }
            return calibrationCurve.GetFittedX(meanPlusSd);
        }
    }
}
