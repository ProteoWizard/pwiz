/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
    /// Stateless numeric primitives used by the scoring pipeline:
    /// range-windowed Pearson correlation and lower-bound binary search
    /// over a sorted <c>double[]</c>.
    ///
    /// Relocated verbatim out of <c>AbstractScoringTask</c> (which had
    /// accreted these pure helpers alongside its I/O and orchestration).
    /// The arithmetic is byte-for-byte unchanged from the originals so
    /// cross-impl parity is unaffected by the move.
    ///
    /// Known duplication, deliberately preserved here pending a
    /// parity-gated consolidation (see backlog
    /// <c>TODO-ospreysharp_task_layer_decomposition</c>, PR-C):
    /// <list type="bullet">
    ///   <item><see cref="PearsonOverRange"/> and
    ///   <see cref="PearsonCorrelationInRange"/> are two independent
    ///   range-Pearson ports with different no-variance guards
    ///   (product &lt; 1e-30 vs sqrt &lt; 1e-10); they are NOT merged.</item>
    ///   <item><see cref="PearsonCorrelation.Pearson"/> is a third,
    ///   full-array variant.</item>
    ///   <item><see cref="BinarySearchLowerBound"/> and
    ///   <see cref="LowerBoundDouble"/> are functionally identical
    ///   lower-bound searches with different midpoint arithmetic.</item>
    /// </list>
    /// </summary>
    public static class ScoringMath
    {
        /// <summary>
        /// Pearson correlation over an inclusive index range. Returns NaN if either
        /// subrange has no variance or the range is too short.
        /// </summary>
        public static double PearsonOverRange(double[] x, double[] y, int start, int end)
        {
            int n = end - start + 1;
            if (n < 3)
                return double.NaN;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
            for (int i = start; i <= end; i++)
            {
                double xi = x[i];
                double yi = y[i];
                sumX += xi;
                sumY += yi;
                sumXY += xi * yi;
                sumX2 += xi * xi;
                sumY2 += yi * yi;
            }

            double dn = n;
            double denom = (dn * sumX2 - sumX * sumX) * (dn * sumY2 - sumY * sumY);
            if (denom < 1e-30)
                return 0.0;

            return (dn * sumXY - sumX * sumY) / Math.Sqrt(denom);
        }

        /// <summary>
        /// Compute Pearson correlation between two intensity arrays over a range.
        /// </summary>
        public static double PearsonCorrelationInRange(double[] x, double[] y, int start, int end)
        {
            int n = end - start + 1;
            if (n < 3)
                return double.NaN;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
            for (int i = start; i <= end; i++)
            {
                sumX += x[i];
                sumY += y[i];
                sumXY += x[i] * y[i];
                sumX2 += x[i] * x[i];
                sumY2 += y[i] * y[i];
            }

            double denom = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
            if (denom < 1e-10)
                return 0.0;

            return (n * sumXY - sumX * sumY) / denom;
        }

        /// <summary>Smallest index i where arr[i] >= v (sorted ascending).</summary>
        public static int LowerBoundDouble(double[] arr, double v)
        {
            int lo = 0, hi = arr.Length;
            while (lo < hi)
            {
                int m = (lo + hi) >> 1;
                if (arr[m] < v) lo = m + 1; else hi = m;
            }
            return lo;
        }

        public static int BinarySearchLowerBound(double[] sortedArray, double value)
        {
            int lo = 0;
            int hi = sortedArray.Length;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (sortedArray[mid] < value)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }
    }
}
