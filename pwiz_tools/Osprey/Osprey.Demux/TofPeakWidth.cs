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
    /// The width of the TOF peak, measured from a file's own profile spectra: the core sigma of its strong
    /// isolated peaks, in grid samples, by m/z.
    /// </summary>
    /// <remarks>
    /// The core, not the second moment: the ZenoTOF peak has heavier tails than a Gaussian, so a peak's second
    /// moment (1.0 to 1.4 samples on MS2) overstates the width its top follows (0.74 to 1.08). The sigma comes
    /// from the parabola through the logs of the top sample and its two neighbours, sigma^2 = -1 / (2a).
    /// </remarks>
    public static class TofPeakWidth
    {
        /// <summary>Samples each side of a peak's top that must be nearly empty for the peak to count as isolated.</summary>
        public const int ISOLATION = 6;

        /// <summary>The share of the top that the samples at the isolation edges may reach.</summary>
        public const double EDGE_FRACTION = 0.05;

        /// <summary>
        /// Core sigma of every strong isolated peak in a profile spectrum on <paramref name="grid"/>: a local
        /// maximum of at least <paramref name="minTop"/> (in the intensities' units) with both neighbours
        /// nonzero and the samples <see cref="ISOLATION"/> away on each side below
        /// <see cref="EDGE_FRACTION"/> of it. Each peak as (m/z of its top, sigma in samples).
        /// </summary>
        public static List<(double Mz, double Sigma)> Measure(IReadOnlyList<double> mz, IReadOnlyList<double> intensity,
            TofGrid grid, double minTop)
        {
            var peaks = new List<(double, double)>();
            if (mz.Count == 0)
                return peaks;
            // The spectrum on its grid, zeros filled in.
            long first = grid.Index(mz[0]), last = grid.Index(mz[mz.Count - 1]);
            var full = new double[last - first + 1 + 2 * ISOLATION];
            for (int i = 0; i < mz.Count; i++)
                full[grid.Index(mz[i]) - first + ISOLATION] += intensity[i];
            for (int k = ISOLATION; k < full.Length - ISOLATION; k++)
            {
                double top = full[k];
                if (top < minTop || top < full[k - 1] || top <= full[k + 1] || full[k - 1] <= 0 || full[k + 1] <= 0)
                    continue;
                bool isolated = true;
                for (int d = ISOLATION - 1; d <= ISOLATION && isolated; d++)
                    isolated = full[k - d] <= EDGE_FRACTION * top && full[k + d] <= EDGE_FRACTION * top;
                if (!isolated)
                    continue;
                double a = (Math.Log(full[k - 1]) - 2 * Math.Log(top) + Math.Log(full[k + 1])) / 2;
                if (a >= 0)
                    continue;
                peaks.Add((grid.Mz(k - ISOLATION + first), Math.Sqrt(-1 / (2 * a))));
            }
            return peaks;
        }

        /// <summary>
        /// The median sigma in each m/z bin between consecutive <paramref name="edges"/> that holds at least
        /// <paramref name="minPeaks"/> peaks, at the bin's median m/z: the table
        /// <see cref="JointDemuxParams.PeakSigmaMz"/> and <see cref="JointDemuxParams.PeakSigmaSamples"/> take.
        /// Null when no bin has enough.
        /// </summary>
        public static (double[] Mz, double[] Sigma)? Calibrate(IEnumerable<(double Mz, double Sigma)> peaks,
            double[] edges, int minPeaks)
        {
            var all = peaks.ToList();
            var mzs = new List<double>();
            var sigmas = new List<double>();
            for (int b = 0; b + 1 < edges.Length; b++)
            {
                var inBin = all.Where(p => p.Mz >= edges[b] && p.Mz < edges[b + 1]).ToList();
                if (inBin.Count < minPeaks)
                    continue;
                mzs.Add(Median(inBin.Select(p => p.Mz)));
                sigmas.Add(Median(inBin.Select(p => p.Sigma)));
            }
            return mzs.Count == 0 ? null : (mzs.ToArray(), sigmas.ToArray());
        }

        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            return n % 2 == 1 ? sorted[n / 2] : 0.5 * (sorted[n / 2 - 1] + sorted[n / 2]);
        }
    }
}
