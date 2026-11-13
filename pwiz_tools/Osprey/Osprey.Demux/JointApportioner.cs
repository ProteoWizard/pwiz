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

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// A joint solve's measured profile (<see cref="JointDemultiplexer"/>) split among the columns it was fitted with,
    /// the bins of a staggered scheme or the source positions of ZT Scan, for a centroider of its own. A point of row r
    /// at grid sample k goes to column j in proportion to A[r, j] (B * beta_j)[k]: the transmission times column j's
    /// fitted peaks there. Where no column was fitted, in proportion to A[r, j] alone. The fit decides the split; the
    /// measurements are what is kept, with their noise, for the centroider's noise model.
    /// </summary>
    internal static class JointApportioner
    {
        /// <summary>
        /// Splits each point <paramref name="include"/> selects among its row's columns. <paramref name="share"/> is
        /// called with the point, a column and the fraction of the point that column receives, for each column with a
        /// positive fraction, in column order.
        /// </summary>
        /// <param name="unit">The unit solved.</param>
        /// <param name="coefficients">Its solve with <see cref="JointDemuxParams.KeepCoefficients"/>: every column's
        /// coefficients, at their grid points.</param>
        /// <param name="grid">The profile grid.</param>
        /// <param name="parameters">The solve's settings, for its peak kernel.</param>
        /// <param name="include">Which points to split.</param>
        /// <param name="share">Receives (point, column, fraction).</param>
        public static void Split(ScanningUnit unit, ScanningUnitResult coefficients, IProfileGrid grid,
            JointDemuxParams parameters, Func<int, bool> include, Action<int, int, double> share)
        {
            var fitted = FittedPeaks(unit, coefficients, grid, parameters);
            var none = new List<(int Column, double Value)>();
            for (int p = 0; p < unit.Mz.Length; p++)
            {
                if (!include(p))
                    continue;
                int r = unit.Row[p];
                long sample = grid.Index(unit.Mz[p]);
                if (!fitted.TryGetValue((unit.Cycles[unit.Cycle[p]], sample), out var peaks))
                    peaks = none;
                double total = 0;
                foreach (var (j, value) in peaks)
                {
                    double a = unit.Transmission[r, j];
                    if (a > 0)
                        total += a * value;
                }
                if (total > 0)
                {
                    foreach (var (j, value) in peaks)
                    {
                        double a = unit.Transmission[r, j];
                        if (a <= 0)
                            continue;
                        double fraction = a * value / total;
                        if (fraction > 0)
                            share(p, j, fraction);
                    }
                    continue;
                }
                double transmitted = 0;
                for (int j = 0; j < unit.ColumnBins.Length; j++)
                {
                    if (unit.Transmission[r, j] > 0)
                        transmitted += unit.Transmission[r, j];
                }
                for (int j = 0; j < unit.ColumnBins.Length; j++)
                {
                    double a = unit.Transmission[r, j];
                    if (a > 0)
                        share(p, j, a / transmitted);
                }
            }
        }

        /// <summary>
        /// Each (cycle, grid sample)'s fitted peaks, by column in column order: (B * beta_j)[k], summed over the kernel's
        /// offsets in ascending order, the kernel at the sample's m/z (taken once per Th). In the coefficients' units.
        /// </summary>
        public static Dictionary<(int Cycle, long Sample), List<(int Column, double Value)>> FittedPeaks(ScanningUnit unit,
            ScanningUnitResult coefficients, IProfileGrid grid, JointDemuxParams parameters)
        {
            var columnOf = new Dictionary<int, int>();
            for (int j = 0; j < unit.ColumnBins.Length; j++)
                columnOf[unit.ColumnBins[j]] = j;
            var beta = new Dictionary<(int Cycle, int Column), Dictionary<long, double>>();
            foreach (var peak in coefficients.Demultiplexed)
            {
                var key = (peak.Cycle, columnOf[peak.Bin]);
                if (!beta.TryGetValue(key, out var map))
                {
                    map = new Dictionary<long, double>();
                    beta[key] = map;
                }
                map[grid.Index(peak.Mz)] = peak.Ions;
            }

            int half = parameters.PeakHalfWidth;
            var kernels = new Dictionary<long, double[]>();
            var fitted = new Dictionary<(int Cycle, long Sample), List<(int Column, double Value)>>();
            var reached = new SortedSet<long>();
            foreach (var ((cycle, column), map) in beta.OrderBy(e => e.Key.Column).ThenBy(e => e.Key.Cycle))
            {
                reached.Clear();
                foreach (long q in map.Keys)
                {
                    for (int d = -half; d <= half; d++)
                        reached.Add(q + d);
                }
                foreach (long sample in reached)
                {
                    long th = (long)Math.Round(grid.Mz(sample));
                    if (!kernels.TryGetValue(th, out var kernel))
                    {
                        kernel = parameters.PeakAt(th);
                        kernels[th] = kernel;
                    }
                    double f = 0;
                    for (int d = -half; d <= half; d++)
                    {
                        if (map.TryGetValue(sample - d, out double b))
                            f += kernel[d + half] * b;
                    }
                    if (!(f > 0))
                        continue;
                    if (!fitted.TryGetValue((cycle, sample), out var list))
                    {
                        list = new List<(int Column, double Value)>();
                        fitted[(cycle, sample)] = list;
                    }
                    list.Add((column, f));
                }
            }
            return fitted;
        }
    }
}
