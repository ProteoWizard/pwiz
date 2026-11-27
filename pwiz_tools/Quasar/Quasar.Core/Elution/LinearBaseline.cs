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
    /// Per-trace linear background: a straight line between the trace's intensities at the two
    /// peak boundaries (Skyline's linear background), subtracted and clipped at zero. It uses only
    /// the trace's own boundary values, so interference in the flanks does not move it. It assumes
    /// the boundaries sit near the true baseline and over-subtracts when they sit well above it.
    /// Ported from datum's <c>LinearBaselineSubtractor</c> with the arithmetic unchanged.
    /// </summary>
    public static class LinearBaseline
    {
        /// <summary>
        /// Returns a copy of <paramref name="intensity"/> with the line between its values at
        /// <paramref name="startRt"/> and <paramref name="endRt"/> subtracted, clipped at zero.
        /// </summary>
        public static double[] Subtract(double[] rt, double[] intensity, double startRt, double endRt)
        {
            double startValue = Interpolate(rt, intensity, startRt);
            double endValue = Interpolate(rt, intensity, endRt);
            double span = endRt - startRt;

            var result = new double[intensity.Length];
            for (int i = 0; i < intensity.Length; i++)
            {
                double baseline = Math.Abs(span) < 1e-12
                    ? startValue
                    : startValue + (endValue - startValue) * (rt[i] - startRt) / span;
                result[i] = Math.Max(0.0, intensity[i] - baseline);
            }

            return result;
        }

        /// <summary>
        /// Linear interpolation of <paramref name="y"/> sampled at sorted <paramref name="x"/>,
        /// clamped to the end values outside the sampled range (as numpy.interp does).
        /// </summary>
        public static double Interpolate(double[] x, double[] y, double query)
        {
            if (query <= x[0])
                return y[0];
            if (query >= x[x.Length - 1])
                return y[y.Length - 1];

            int lo = Array.BinarySearch(x, query);
            if (lo >= 0)
                return y[lo];

            int hi = ~lo;
            int prev = hi - 1;
            double t = (query - x[prev]) / (x[hi] - x[prev]);
            return y[prev] + t * (y[hi] - y[prev]);
        }
    }
}
