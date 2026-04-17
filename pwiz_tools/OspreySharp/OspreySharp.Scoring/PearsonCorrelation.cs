/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Pearson correlation and co-elution helpers.
    /// Port of correlation functions from osprey-scoring/src/lib.rs.
    /// </summary>
    public static class PearsonCorrelation
    {
        /// <summary>
        /// Compute Pearson correlation coefficient between two arrays.
        /// Returns 0 if either array has no variance or arrays are too short.
        /// </summary>
        public static double Pearson(double[] x, double[] y)
        {
            int n = Math.Min(x.Length, y.Length);
            if (n < 2)
                return 0.0;

            double dn = n;
            double sx = 0.0, sy = 0.0;
            double sx2 = 0.0, sy2 = 0.0;
            double sxy = 0.0;

            for (int i = 0; i < n; i++)
            {
                double xi = x[i];
                double yi = y[i];
                sx += xi;
                sy += yi;
                sx2 += xi * xi;
                sy2 += yi * yi;
                sxy += xi * yi;
            }

            double denom = (dn * sx2 - sx * sx) * (dn * sy2 - sy * sy);
            if (denom < 1e-30)
                return 0.0;

            return (dn * sxy - sx * sy) / Math.Sqrt(denom);
        }

        /// <summary>
        /// Compute mean pairwise Pearson correlation between fragment XICs.
        /// Each XIC is a double[] of intensities across scans.
        /// Returns the average correlation across all fragment pairs.
        /// </summary>
        public static double MeanPairwiseCorrelation(double[][] fragmentXics)
        {
            if (fragmentXics == null || fragmentXics.Length < 2)
                return 0.0;

            int nFrags = fragmentXics.Length;
            double corrSum = 0.0;
            int nPairs = 0;

            for (int i = 0; i < nFrags; i++)
            {
                for (int j = i + 1; j < nFrags; j++)
                {
                    double r = Pearson(fragmentXics[i], fragmentXics[j]);
                    if (!double.IsNaN(r))
                    {
                        corrSum += r;
                        nPairs++;
                    }
                }
            }

            if (nPairs == 0)
                return 0.0;

            return corrSum / nPairs;
        }

        /// <summary>
        /// Compute sum of pairwise Pearson correlations between fragment XICs.
        /// Only positive correlations are summed (co-eluting fragments).
        /// </summary>
        public static double CoelutionSum(double[][] fragmentXics)
        {
            if (fragmentXics == null || fragmentXics.Length < 2)
                return 0.0;

            int nFrags = fragmentXics.Length;
            double corrSum = 0.0;

            for (int i = 0; i < nFrags; i++)
            {
                for (int j = i + 1; j < nFrags; j++)
                {
                    double r = Pearson(fragmentXics[i], fragmentXics[j]);
                    if (!double.IsNaN(r) && r > 0.0)
                        corrSum += r;
                }
            }

            return corrSum;
        }
    }
}
