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

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// Fragment m/z channels for one block of scanning-quadrupole spectra: which peaks, across
    /// the block's encoded bins and sweeps, are the same product ion.
    /// </summary>
    /// <remarks>
    /// <para>A real fragment repeats at nearly the same m/z in every spectrum that transmits its
    /// precursor (about 16 encoded bins) and in every sweep of its elution, so it stands out as
    /// a sharp maximum of the block's intensity-weighted m/z histogram, even where unrelated peaks
    /// fall about every ppm. Chaining neighbors by m/z gap does not work at that density: the
    /// whole spectrum chains into one channel.</para>
    /// <para>The histogram has 1 ppm bins on a log m/z axis and is smoothed over +/- 4 ppm. Its
    /// maxima are taken greedily, largest first, each claiming the tolerance around it, and each
    /// peak is assigned to the nearest claimed center within the tolerance. Ties resolve to the
    /// lower m/z, so the result does not depend on peak order.</para>
    /// </remarks>
    public static class FragmentChannelFinder
    {
        /// <summary>Half-width of the histogram smoothing, in 1 ppm bins.</summary>
        public const int SMOOTHING_HALF_WIDTH = 4;

        /// <summary>
        /// Assigns each peak to a channel, or to -1 when no channel center lies within
        /// <paramref name="tolerancePpm"/> of it.
        /// </summary>
        /// <param name="mz">Peak m/z values, in any order.</param>
        /// <param name="ions">Peak intensities, in ions.</param>
        /// <param name="tolerancePpm">How far a peak may lie from its channel center.</param>
        /// <param name="minIons">
        /// A center needs at least this many ions within the smoothing width around it.
        /// </param>
        /// <param name="channel">Receives each peak's channel index, or -1.</param>
        /// <returns>The number of channels, indexed in increasing m/z.</returns>
        public static int Find(double[] mz, double[] ions, double tolerancePpm, double minIons, int[] channel)
        {
            int count = mz.Length;
            if (count == 0)
                return 0;

            // 1 ppm bins on a log m/z axis, and the peaks sorted by bin with the peak index as the
            // tie-breaker, packed into one key so a plain sort is stable.
            var key = new long[count];
            var order = new long[count];
            for (int i = 0; i < count; i++)
            {
                key[i] = (long)Math.Floor(Math.Log(mz[i]) * 1e6);
                order[i] = key[i] * count + i;
            }
            Array.Sort(order); // Array.Sort OK: keys are unique (bin * count + index)

            // Distinct bins and the ions in each.
            var binKey = new long[count];
            var binIons = new double[count];
            int bins = 0;
            for (int k = 0; k < count; k++)
            {
                int i = (int)(order[k] % count);
                if (bins == 0 || key[i] != binKey[bins - 1])
                {
                    binKey[bins] = key[i];
                    bins++;
                }
                binIons[bins - 1] += ions[i];
            }

            // Smoothed ions: the sum over bins within the smoothing width, by prefix sums.
            var prefix = new double[bins + 1];
            for (int b = 0; b < bins; b++)
                prefix[b + 1] = prefix[b] + binIons[b];
            var smooth = new double[bins];
            for (int b = 0, lo = 0, hi = 0; b < bins; b++)
            {
                while (binKey[lo] < binKey[b] - SMOOTHING_HALF_WIDTH)
                    lo++;
                while (hi < bins && binKey[hi] <= binKey[b] + SMOOTHING_HALF_WIDTH)
                    hi++;
                smooth[b] = prefix[hi] - prefix[lo];
            }

            // Candidate centers, largest smoothed value first, the lower bin on a tie.
            int candidates = 0;
            var candidate = new int[bins];
            for (int b = 0; b < bins; b++)
            {
                if (smooth[b] >= minIons)
                    candidate[candidates++] = b;
            }
            var rank = new double[candidates];
            var rankedBins = new int[candidates];
            for (int c = 0; c < candidates; c++)
            {
                rank[c] = -smooth[candidate[c]];
                rankedBins[c] = candidate[c];
            }
            SortByValueThenIndex(rank, rankedBins);

            // Greedy maxima, each claiming the bins within the tolerance of it.
            int radius = (int)Math.Ceiling(tolerancePpm);
            var taken = new bool[bins];
            var centers = new long[candidates];
            int channels = 0;
            for (int c = 0; c < candidates; c++)
            {
                int b = rankedBins[c];
                if (taken[b])
                    continue;
                centers[channels++] = binKey[b];
                int lo = LowerBound(binKey, bins, binKey[b] - radius);
                int hi = LowerBound(binKey, bins, binKey[b] + radius + 1);
                for (int j = lo; j < hi; j++)
                    taken[j] = true;
            }
            Array.Sort(centers, 0, channels); // Array.Sort OK: centers are distinct bins

            // Each peak to the nearest center within the tolerance, the lower center on a tie.
            for (int i = 0; i < count; i++)
            {
                channel[i] = -1;
                if (channels == 0)
                    continue;
                int hi = LowerBound(centers, channels, key[i]);
                int best = -1;
                long bestDistance = long.MaxValue;
                if (hi > 0)
                {
                    best = hi - 1;
                    bestDistance = key[i] - centers[hi - 1];
                }
                if (hi < channels && centers[hi] - key[i] < bestDistance)
                {
                    best = hi;
                    bestDistance = centers[hi] - key[i];
                }
                if (best >= 0 && bestDistance <= tolerancePpm)
                    channel[i] = best;
            }
            return channels;
        }

        /// <summary>First index in the sorted prefix of <paramref name="values"/> not below <paramref name="target"/>.</summary>
        private static int LowerBound(long[] values, int length, long target)
        {
            int lo = 0, hi = length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (values[mid] < target)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        /// <summary>Sorts both arrays by value, then by index, so equal values keep a fixed order.</summary>
        private static void SortByValueThenIndex(double[] values, int[] indices)
        {
            var order = new int[values.Length];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            Array.Sort(order, (a, b) =>  // Array.Sort OK: ties broken by index
            {
                int byValue = values[a].CompareTo(values[b]);
                return byValue != 0 ? byValue : indices[a].CompareTo(indices[b]);
            });
            var sortedValues = new double[values.Length];
            var sortedIndices = new int[indices.Length];
            for (int i = 0; i < order.Length; i++)
            {
                sortedValues[i] = values[order[i]];
                sortedIndices[i] = indices[order[i]];
            }
            Array.Copy(sortedValues, values, values.Length);
            Array.Copy(sortedIndices, indices, indices.Length);
        }
    }
}
