using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class BilinearRegressionFit : RegressionFit
    {
        public BilinearRegressionFit() : base(@"bilinear", () => QuantificationStrings.RegressionFit_BILINEAR_Bilinear)
        {

        }

        protected override CalibrationCurve FitPoints(IList<WeightedPoint> points)
        {
            var concentrations = points.Select(pt => pt.X).Distinct().OrderBy(x=>x).ToList();
            ScoredBilinearCurve bestCurve = null;
            var linearCurve = LINEAR.Fit(points) as CalibrationCurve.Linear;
            if (linearCurve != null)
            {
                bestCurve = ScoredBilinearCurve.FromCalibrationCurve(linearCurve, points);
            }
            foreach (var xOffset in concentrations)
            {
                var candidateCurveFit = ScoredBilinearCurve.WithOffset(xOffset, points);
                if (candidateCurveFit == null)
                {
                    continue;
                }
                if (bestCurve == null || candidateCurveFit.Error < bestCurve.Error)
                {
                    bestCurve = candidateCurveFit;
                }
            }

            return bestCurve?.CalibrationCurve;
        }
    }
}