/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on AlphaPeptDeep (https://github.com/MannLabs/alphapeptdeep) peptdeep/model/rt.py
 *   (add_irt_column_to_precursor_df), Apache-2.0
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// The Biognosys iRT kit peptides and their iRT values. A retention-time model predicts them to map
    /// its own scale (a fraction of the gradient, a hydrophobic index) onto iRT, as peptdeep does.
    /// </summary>
    internal static class IrtKit
    {
        private static readonly (string Sequence, double Irt)[] PEPTIDES =
        {
            (@"LGGNEQVTR", -24.92),
            (@"GAGSSEPVTGLDAK", 0.00),
            (@"VEATFGVDESNAK", 12.39),
            (@"YILAGVENSK", 19.79),
            (@"TPVISGGPYEYR", 28.71),
            (@"TPVITGAPYEYR", 33.38),
            (@"DGLDAASYYAPVR", 42.26),
            (@"ADVTPADFSEWSK", 54.62),
            (@"GTFIIDPGGVIR", 70.52),
            (@"GTFIIDPAAVIR", 87.23),
            (@"LFLQFGAQGSPFLK", 100.00),
        };

        /// <summary>The least-squares line from a model's predictions of the eleven peptides to their iRT values.</summary>
        public static (double Slope, double Intercept) FitCalibration(Func<IReadOnlyList<PeptideForm>, double[]> predict)
        {
            var peptides = PEPTIDES.Select(p => new PeptideForm(p.Sequence)).ToArray();
            double[] predicted = predict(peptides);
            double predictedMean = predicted.Average();
            double irtMean = PEPTIDES.Average(p => p.Irt);
            double sxy = 0, sxx = 0;
            for (int i = 0; i < predicted.Length; i++)
            {
                double x = predicted[i] - predictedMean;
                double y = PEPTIDES[i].Irt - irtMean;
                sxy += x * y;
                sxx += x * x;
            }
            double slope = sxy / sxx;
            return (slope, irtMean - slope * predictedMean);
        }
    }
}
