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
using System.Collections.Generic;

namespace pwiz.Quasar.Core.Elution
{
    /// <summary>
    /// Quantifies co-eluting traces with one shared EMG shape. The shape is fitted once to the
    /// per-point median across traces, which suppresses interference present in only a minority
    /// of them (the same consensus principle as Osprey's median CWT). Each trace's area is then the
    /// robust amplitude of that unit-area shape against the trace's own points: the median of
    /// y / g over the points where g is at least 5% of its peak. An interference bump on a few
    /// traces is rejected, and the area under it is recovered from the shared shape.
    /// Ported from datum's <c>ConsensusEmgIntegrator</c> with the arithmetic unchanged.
    /// </summary>
    public static class ConsensusEmgIntegrator
    {
        /// <summary>
        /// Points count toward a trace's amplitude only where the unit shape is at least this
        /// fraction of its peak, so near-baseline points with tiny denominators add no noise.
        /// </summary>
        public const double SHAPE_FLOOR_FRACTION = 0.05;

        /// <summary>
        /// Fits the consensus shape over scans <paramref name="startIndex"/> to
        /// <paramref name="endIndex"/> (inclusive) and returns each trace's area. The traces should
        /// already be background subtracted, and all share the <paramref name="rt"/> axis.
        /// </summary>
        public static ConsensusEmgResult Integrate(double[] rt, double[][] traces, int startIndex, int endIndex)
        {
            int traceCount = traces.Length;
            var indices = new List<int>();
            for (int i = Math.Max(0, startIndex); i <= endIndex && i < rt.Length; i++)
                indices.Add(i);

            if (traceCount == 0 || indices.Count < EmgFit.MIN_POINTS)
                return NotQuantified(traceCount);

            // Fit one shape to the per-point median across traces.
            var x = new double[indices.Count];
            var medianTrace = new double[indices.Count];
            var column = new double[traceCount];
            for (int j = 0; j < indices.Count; j++)
            {
                int i = indices[j];
                x[j] = rt[i];
                for (int f = 0; f < traceCount; f++)
                    column[f] = traces[f][i];
                medianTrace[j] = Median(column);
            }

            var fit = EmgFit.Fit(x, medianTrace);
            if (fit == null)
                return NotQuantified(traceCount);

            var shape = fit.Shape;
            var g = new double[indices.Count];
            double gMax = 0.0;
            for (int j = 0; j < indices.Count; j++)
            {
                g[j] = shape.Density(x[j]);
                if (g[j] > gMax)
                    gMax = g[j];
            }

            if (gMax <= 0.0)
                return NotQuantified(traceCount);

            double floor = SHAPE_FLOOR_FRACTION * gMax;

            // Each trace's area is the median amplitude of the shared unit-area shape.
            var areas = new double[traceCount];
            var ratios = new List<double>(indices.Count);
            for (int f = 0; f < traceCount; f++)
            {
                ratios.Clear();
                for (int j = 0; j < indices.Count; j++)
                {
                    if (g[j] >= floor)
                        ratios.Add(traces[f][indices[j]] / g[j]);
                }

                double amplitude = ratios.Count > 0 ? Median(ratios) : double.NaN;
                areas[f] = double.IsFinite(amplitude) && amplitude > 0.0 ? amplitude : double.NaN;
            }

            return new ConsensusEmgResult(shape, areas);
        }

        private static ConsensusEmgResult NotQuantified(int traceCount)
        {
            var areas = new double[traceCount];
            Array.Fill(areas, double.NaN);
            return new ConsensusEmgResult(null, areas);
        }

        /// <summary>Median of the values, from a sorted copy (the input is not reordered).</summary>
        private static double Median(IReadOnlyList<double> values)
        {
            if (values.Count == 0)
                return double.NaN;

            var copy = new double[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];

            Array.Sort(copy);
            int mid = copy.Length / 2;
            return copy.Length % 2 == 1 ? copy[mid] : 0.5 * (copy[mid - 1] + copy[mid]);
        }
    }
}
