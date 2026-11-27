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
using pwiz.Osprey.Centroid;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// Centrix (<see cref="CentrixCentroider"/>) on a profile grid's own coordinate, its sample index. A grid uniform in
    /// some coordinate of m/z is uniform there, as Centrix's basis needs: a TOF's (<see cref="TofGrid"/>, uniform in
    /// sqrt(m/z)) as well as a linear ion trap's. The peak sigma is given in samples, by m/z, since a TOF peak's widens
    /// slowly with m/z (on ZT Scan 1.17 samples at 300 m/z, 1.52 at 1200); each signal region takes the sigma at its
    /// center. Centroids are mapped back to m/z through the grid.
    /// <para>The Th settings of <see cref="CentrixParams"/> (the noise windows, the centroid separation) are converted to
    /// samples at a reference m/z. Intensities are in the instrument's units, as Centrix's noise model expects.</para>
    /// </summary>
    public sealed class GridCentroider
    {
        private readonly IProfileGrid _grid;
        private readonly CentrixCentroider _centrix;
        private readonly int _pad;

        /// <param name="grid">The profile's grid.</param>
        /// <param name="parameters">Centrix's settings; the sigma and grid spacing are replaced.</param>
        /// <param name="sigmaSamplesAt">The peak sigma, in samples, at an m/z.</param>
        /// <param name="referenceMz">Where the Th settings are converted to samples.</param>
        public GridCentroider(IProfileGrid grid, CentrixParams parameters, Func<double, double> sigmaSamplesAt,
            double referenceMz)
        {
            _grid = grid;
            var p = parameters.Copy();
            double position = grid.Position(referenceMz);
            double samplesPerTh = 1 / (grid.Mz(position + 0.5) - grid.Mz(position - 0.5));
            p.SigmaTh = sigmaSamplesAt(referenceMz);
            p.GridSpacingTh = 1;
            p.GridOffsetTh = 0;
            p.NoiseWindowTh *= samplesPerTh;
            p.NoiseStepTh *= samplesPerTh;
            p.MinCentroidSeparationTh *= samplesPerTh;
            _centrix = new CentrixCentroider(p, x => sigmaSamplesAt(grid.Mz(x)));
            _pad = p.MergeGapPoints + p.ExtensionPoints + 1;
        }

        /// <summary>
        /// The centroids of one profile on the grid, ascending in m/z: its samples ascending (zeros dropped or not), and
        /// their intensities. Missing samples are zeros; a gap, and each end, is given enough of them that no signal
        /// region spans a gap and each peak has its edges, rather than the whole grid, which over a TOF spectrum would
        /// be hundreds of thousands of samples.
        /// </summary>
        public List<(double Mz, double Intensity)> Centroid(IReadOnlyList<long> samples, IReadOnlyList<double> intensity)
        {
            if (samples.Count == 0)
                return new List<(double Mz, double Intensity)>();
            var x = new List<double>(samples.Count + 2 * _pad);
            var y = new List<double>(samples.Count + 2 * _pad);
            for (long k = samples[0] - _pad; k < samples[0]; k++)
                Add(x, y, k);
            for (int i = 0; i < samples.Count; i++)
            {
                if (i > 0)
                {
                    long previous = samples[i - 1], missing = samples[i] - previous - 1;
                    if (missing <= 2 * _pad)
                    {
                        for (long k = previous + 1; k < samples[i]; k++)
                            Add(x, y, k);
                    }
                    else
                    {
                        for (long k = previous + 1; k <= previous + _pad; k++)
                            Add(x, y, k);
                        for (long k = samples[i] - _pad; k < samples[i]; k++)
                            Add(x, y, k);
                    }
                }
                x.Add(samples[i]);
                y.Add(intensity[i]);
            }
            for (long k = samples[samples.Count - 1] + 1; k <= samples[samples.Count - 1] + _pad; k++)
                Add(x, y, k);
            var centroids = _centrix.Centroid(x.ToArray(), y.ToArray());
            for (int i = 0; i < centroids.Count; i++)
                centroids[i] = (_grid.Mz(centroids[i].Mz), centroids[i].Intensity);
            return centroids;
        }

        private static void Add(List<double> x, List<double> y, long sample)
        {
            x.Add(sample);
            y.Add(0);
        }
    }
}
