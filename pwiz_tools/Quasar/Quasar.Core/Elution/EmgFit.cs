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
    /// Levenberg-Marquardt fit of an area-normalized EMG, y(t) = A * g(t; mu, sigma, tau), to
    /// sampled points. Ported from datum's <c>EmgFit</c> with the arithmetic unchanged.
    /// </summary>
    public static class EmgFit
    {
        /// <summary>A four-parameter EMG needs at least this many points to be well conditioned.</summary>
        public const int MIN_POINTS = 5;

        /// <summary>Iteration limit for the Levenberg-Marquardt fit.</summary>
        private const int MAX_ITERATIONS = 80;

        /// <summary>
        /// Fits an EMG to <paramref name="x"/>, <paramref name="y"/>. Returns null when there are too
        /// few points, the data has no positive area, or the fit does not give a usable shape: a
        /// non-finite or non-positive area or width, or an area more than ten times the trapezoid area.
        /// </summary>
        public static EmgFitResult Fit(double[] x, double[] y)
        {
            if (x.Length < MIN_POINTS)
                return null;

            // Initial guess from the data's moments and its trapezoid area.
            double total = 0.0, weightedMean = 0.0, max = 0.0;
            int apexIdx = 0;
            for (int i = 0; i < x.Length; i++)
            {
                total += y[i];
                weightedMean += x[i] * y[i];
                if (y[i] > max)
                {
                    max = y[i];
                    apexIdx = i;
                }
            }

            if (total <= 0.0)
                return null;

            weightedMean /= total;
            double variance = 0.0;
            for (int i = 0; i < x.Length; i++)
            {
                double d = x[i] - weightedMean;
                variance += y[i] * d * d;
            }

            variance /= total;
            double spread = Math.Sqrt(Math.Max(variance, 1e-6));
            double reference = TrapezoidArea(x, y);
            double area0 = Math.Max(1e-6, reference);
            var initial = new[] { area0, x[apexIdx], spread * 0.7, spread * 0.5 };

            double[] fit = LevenbergMarquardt.Fit(x, y, initial, EmgShape.Model, MAX_ITERATIONS);
            double area = fit[0];
            double sigma = Math.Abs(fit[2]);
            double tau = Math.Abs(fit[3]);

            if (!double.IsFinite(area) || area <= 0.0 || sigma <= 0.0 || tau <= 0.0 ||
                (reference > 0.0 && area > 10.0 * reference))
            {
                return null;
            }

            return new EmgFitResult(area, new EmgShape(fit[1], sigma, tau));
        }

        /// <summary>Trapezoid-rule area of <paramref name="y"/> sampled at <paramref name="x"/>.</summary>
        public static double TrapezoidArea(double[] x, double[] y)
        {
            double area = 0.0;
            for (int i = 1; i < x.Length; i++)
                area += 0.5 * (y[i] + y[i - 1]) * (x[i] - x[i - 1]);
            return area;
        }
    }
}
