/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on datum (https://github.com/maccoss/datum)
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

namespace pwiz.Quasar.Core.Elution
{
    /// <summary>
    /// Gaussian elimination with partial pivoting for the small dense systems the peak fits
    /// produce (a handful of unknowns). Taken out of datum's Levenberg-Marquardt fitter unchanged
    /// so the non-negative least squares solver can share it.
    /// </summary>
    internal static class DenseLinearSolver
    {
        /// <summary>
        /// Solves a * z = b. Returns false when a pivot is below 1e-15, that is when the system is
        /// singular or nearly so. Neither input is modified.
        /// </summary>
        public static bool TrySolve(double[,] a, double[] b, out double[] z)
        {
            int n = b.Length;
            var m = (double[,])a.Clone();
            var rhs = (double[])b.Clone();
            z = new double[n];

            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                double best = Math.Abs(m[col, col]);
                for (int r = col + 1; r < n; r++)
                {
                    double v = Math.Abs(m[r, col]);
                    if (v > best)
                    {
                        best = v;
                        pivot = r;
                    }
                }

                if (best < 1e-15)
                    return false;

                if (pivot != col)
                {
                    for (int c = 0; c < n; c++)
                        (m[col, c], m[pivot, c]) = (m[pivot, c], m[col, c]);
                    (rhs[col], rhs[pivot]) = (rhs[pivot], rhs[col]);
                }

                for (int r = col + 1; r < n; r++)
                {
                    double factor = m[r, col] / m[col, col];
                    for (int c = col; c < n; c++)
                        m[r, c] -= factor * m[col, c];
                    rhs[r] -= factor * rhs[col];
                }
            }

            for (int r = n - 1; r >= 0; r--)
            {
                double sum = rhs[r];
                for (int c = r + 1; c < n; c++)
                    sum -= m[r, c] * z[c];
                z[r] = sum / m[r, r];
            }

            return true;
        }
    }
}
