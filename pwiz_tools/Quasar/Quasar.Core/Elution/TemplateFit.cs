/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

namespace pwiz.Quasar.Core.Elution
{
    /// <summary>
    /// The window fit for one trace (spec Stage 3a): over a segment of the scan axis, the trace is
    /// modeled as y(t) = sum_k a_k T_k(t) + baseline(t), with fixed unit-area templates T_k, every
    /// a_k &gt;= 0, and a baseline that is a straight line with non-negative values at both ends of
    /// the segment. The shapes are fixed, so this is weighted non-negative least squares, with
    /// weights from the noise model. This is the fixed-shape integrator from the datum design note,
    /// applied across all the templates that overlap a trace.
    /// </summary>
    /// <remarks>
    /// The variances come from the fit itself, not from the observed values, which would give low
    /// points too much weight: a first pass weights by the observed values, a second by the
    /// first pass's fitted values.
    /// </remarks>
    public static class TemplateFit
    {
        /// <summary>
        /// Default membership threshold on <see cref="TemplateFitResult.DeltaChiSquare"/>. With the
        /// amplitude constrained non-negative, the statistic under "no contribution" is a 50:50
        /// mixture of 0 and chi-square with one degree of freedom, so 9.55 is p = 0.001.
        /// </summary>
        public const double DEFAULT_MEMBERSHIP_THRESHOLD = 9.55;

        /// <summary>A member needs at least this many points where its template is significant.</summary>
        public const int MIN_SUPPORT_POINTS = 4;

        /// <summary>A template is significant where it is at least this fraction of its peak.</summary>
        public const double SUPPORT_FRACTION = 0.05;

        /// <summary>
        /// Fits points <paramref name="start"/> to <paramref name="end"/> (inclusive) of the trace
        /// <paramref name="y"/>, sampled at <paramref name="rt"/>, to <paramref name="templates"/>.
        /// When <paramref name="y"/> is a residual, <paramref name="explained"/> is the signal
        /// already taken out of it (on the same axis): it is not fitted, but it is part of every
        /// point's variance, so what is left is judged against the noise of the whole signal and not
        /// against the near-zero noise a residual would imply on its own.
        /// </summary>
        public static TemplateFitResult Fit(double[] rt, double[] y, int start, int end,
            IReadOnlyList<EmgShape> templates, NoiseModel noise, double[] explained = null)
        {
            return Fit(rt, y, start, end, templates, noise, explained, templates.Count);
        }

        /// <summary>
        /// As <see cref="Fit(double[],double[],int,int,IReadOnlyList{EmgShape},NoiseModel,double[])"/>,
        /// but computes <see cref="TemplateFitResult.DeltaChiSquare"/> only for the first
        /// <paramref name="testedCount"/> templates (each one is an extra fit); the rest are left at
        /// zero. A caller that tests one template against its neighbors passes 1.
        /// </summary>
        public static TemplateFitResult Fit(double[] rt, double[] y, int start, int end,
            IReadOnlyList<EmgShape> templates, NoiseModel noise, double[] explained, int testedCount)
        {
            start = Math.Max(0, start);
            end = Math.Min(Math.Min(rt.Length, y.Length) - 1, end);
            int n = end - start + 1;
            if (n <= 0)
                throw new ArgumentException($@"Empty segment {start}..{end}");

            var x = new double[n];
            var obs = new double[n];
            var offset = new double[n];
            for (int i = 0; i < n; i++)
            {
                x[i] = rt[start + i];
                obs[i] = y[start + i];
                offset[i] = explained?[start + i] ?? 0.0;
            }

            var columns = BuildColumns(x, templates);
            var weights = obs.Select((v, i) => 1.0 / noise.Variance(v + offset[i])).ToArray();
            double[] coefficients = NonNegativeLeastSquares.Solve(columns, obs, weights);
            var fitted = Evaluate(columns, coefficients);
            weights = fitted.Select((v, i) => 1.0 / noise.Variance(v + offset[i])).ToArray();
            coefficients = NonNegativeLeastSquares.Solve(columns, obs, weights);
            fitted = Evaluate(columns, coefficients);
            double chiSquare = NonNegativeLeastSquares.WeightedSumOfSquares(columns, obs, weights, coefficients);

            int templateCount = templates.Count;
            var deltaChiSquare = new double[templateCount];
            for (int k = 0; k < Math.Min(testedCount, templateCount); k++)
            {
                if (coefficients[k] <= 0.0)
                    continue;
                var without = columns.Where((_, j) => j != k).ToArray();
                double[] reduced = NonNegativeLeastSquares.Solve(without, obs, weights);
                double reducedChiSquare = NonNegativeLeastSquares.WeightedSumOfSquares(without, obs, weights, reduced);
                deltaChiSquare[k] = Math.Max(0.0, reducedChiSquare - chiSquare);
            }

            var amplitudes = coefficients.Take(templateCount).ToArray();
            var supportPoints = templates.Select(t => SupportPointCount(t, x)).ToArray();
            return new TemplateFitResult(start, amplitudes, coefficients[templateCount],
                coefficients[templateCount + 1], fitted, chiSquare, deltaChiSquare, supportPoints);
        }

        /// <summary>
        /// The number of times in <paramref name="x"/> where <paramref name="template"/> is at
        /// least <see cref="SUPPORT_FRACTION"/> of its peak height.
        /// </summary>
        public static int SupportPointCount(EmgShape template, double[] x)
        {
            var (left, right) = template.HeightCrossings(SUPPORT_FRACTION);
            return x.Count(t => t >= left && t <= right);
        }

        /// <summary>
        /// The design matrix by columns: one unit-area template per column, then the two baseline
        /// columns, which ramp from 1 to 0 and from 0 to 1 across the segment.
        /// </summary>
        private static double[][] BuildColumns(double[] x, IReadOnlyList<EmgShape> templates)
        {
            int n = x.Length;
            var columns = new double[templates.Count + 2][];
            for (int k = 0; k < templates.Count; k++)
            {
                var template = templates[k];
                columns[k] = x.Select(template.Density).ToArray();
            }

            double span = x[n - 1] - x[0];
            var rampDown = new double[n];
            var rampUp = new double[n];
            for (int i = 0; i < n; i++)
            {
                double u = span > 0.0 ? (x[i] - x[0]) / span : 0.0;
                rampDown[i] = 1.0 - u;
                rampUp[i] = u;
            }

            columns[templates.Count] = rampDown;
            columns[templates.Count + 1] = rampUp;
            return columns;
        }

        private static double[] Evaluate(double[][] columns, double[] coefficients)
        {
            int n = columns[0].Length;
            var values = new double[n];
            for (int j = 0; j < columns.Length; j++)
            {
                if (coefficients[j] == 0.0)
                    continue;
                for (int i = 0; i < n; i++)
                    values[i] += columns[j][i] * coefficients[j];
            }

            return values;
        }
    }
}
