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
    /// The TOF peak's shape, measured from a file's own profile spectra: the average of its strong isolated
    /// peaks, by m/z, as the kernel <see cref="JointDemultiplexer"/> fits with.
    /// </summary>
    /// <remarks>
    /// <para>A Gaussian does not fit the ZenoTOF peak: its core is narrower than its second moment says (MS2
    /// Gaussian-fit sigma 0.74 to 1.08 samples against second moments of 1.0 to 1.4), with tails beyond. A
    /// kernel too wide at the core misfits every strong peak and couples neighbouring grid points more (0.83 to
    /// 0.90 against 0.69 to 0.80 measured), which is what slows the solve.</para>
    /// <para>Each isolated local maximum (both neighbours nonzero, the samples <see cref="ISOLATION"/> - 1 and
    /// <see cref="ISOLATION"/> away below <see cref="EDGE_FRACTION"/> of the top) is centred on the vertex of the
    /// parabola through the logs of its top three samples and scaled to that vertex's height; the peaks centred
    /// within a quarter sample of a grid point are averaged sample by sample: the shape of a peak centred on a
    /// grid point.</para>
    /// </remarks>
    public sealed class TofPeakShape
    {
        /// <summary>Samples each side of a peak's top that must be nearly empty for the peak to count as isolated.</summary>
        public const int ISOLATION = 6;

        /// <summary>The share of the top that the samples at the isolation edges may reach.</summary>
        public const double EDGE_FRACTION = 0.05;

        private const double WINDOW = 0.25;
        private readonly double[] _edges;
        private readonly List<double>[] _mz;
        private readonly double[,] _sum;     // per m/z bin, per offset -ISOLATION..ISOLATION
        private readonly int[] _count;       // per m/z bin

        /// <summary>Accumulates peaks into the m/z bins between consecutive <paramref name="edges"/>.</summary>
        public TofPeakShape(double[] edges)
        {
            _edges = edges;
            int bins = edges.Length - 1;
            _mz = Enumerable.Range(0, bins).Select(_ => new List<double>()).ToArray();
            _sum = new double[bins, 2 * ISOLATION + 1];
            _count = new int[bins];
        }

        /// <summary>The peaks added so far, all bins.</summary>
        public int Peaks => _mz.Sum(list => list.Count);

        /// <summary>
        /// Adds the strong isolated peaks of a profile spectrum on <paramref name="grid"/>: local maxima of at
        /// least <paramref name="minTop"/> (in the intensities' units).
        /// </summary>
        public void Add(IReadOnlyList<double> mz, IReadOnlyList<double> intensity, TofGrid grid, double minTop)
        {
            if (mz.Count == 0)
                return;
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
                double l0 = Math.Log(full[k - 1]), l1 = Math.Log(top), l2 = Math.Log(full[k + 1]);
                double a = (l0 - 2 * l1 + l2) / 2, b = (l2 - l0) / 2;
                if (a >= 0)
                    continue;
                // The peak's centre, off its top sample; only peaks centred within a quarter sample of a grid
                // point are kept, so their samples lie within a quarter sample of whole offsets.
                double center = -b / (2 * a);
                if (Math.Abs(center) > WINDOW)
                    continue;
                double height = Math.Exp(l1 - b * b / (4 * a));
                int bin = Bin(grid.Mz(k - ISOLATION + first + center));
                if (bin < 0)
                    continue;
                _mz[bin].Add(grid.Mz(k - ISOLATION + first + center));
                for (int j = -ISOLATION; j <= ISOLATION; j++)
                    _sum[bin, j + ISOLATION] += full[k + j] / height;
                _count[bin]++;
            }
        }

        /// <summary>
        /// The measured kernel of every bin with at least <paramref name="minPeaks"/> peaks, at the bin's median
        /// m/z: 2 <paramref name="half"/> + 1 values for the offsets -half..half, summing to 1, the table
        /// <see cref="JointDemuxParams.PeakShapeMz"/> and <see cref="JointDemuxParams.PeakShapes"/> take. Null when
        /// no bin has enough.
        /// </summary>
        public (double[] Mz, double[][] Kernels)? Kernels(int half, int minPeaks)
        {
            var mzs = new List<double>();
            var kernels = new List<double[]>();
            for (int bin = 0; bin < _mz.Length; bin++)
            {
                if (_mz[bin].Count < minPeaks)
                    continue;
                var kernel = new double[2 * half + 1];
                double total = 0;
                for (int d = -half; d <= half; d++)
                {
                    kernel[d + half] = _sum[bin, d + ISOLATION] / _count[bin];
                    total += kernel[d + half];
                }
                for (int d = 0; d < kernel.Length; d++)
                    kernel[d] /= total;
                var sorted = _mz[bin].OrderBy(m => m).ToList();
                mzs.Add(sorted[sorted.Count / 2]);
                kernels.Add(kernel);
            }
            return mzs.Count == 0 ? null : (mzs.ToArray(), kernels.ToArray());
        }

        private int Bin(double mz)
        {
            for (int b = 0; b + 1 < _edges.Length; b++)
            {
                if (mz >= _edges[b] && mz < _edges[b + 1])
                    return b;
            }
            return -1;
        }
    }
}
