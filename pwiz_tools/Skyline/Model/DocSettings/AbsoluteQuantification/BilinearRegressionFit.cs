/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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