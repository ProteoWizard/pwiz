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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// Infers the demultiplexing scheme of a run from the isolation windows its MS2
    /// spectra report.
    /// </summary>
    /// <remarks>
    /// The narrow bins are the intervals between adjacent window boundaries (the sorted
    /// union of every lower and upper bound), so no uniform ladder is assumed and
    /// variable-width schedules work unchanged. Boundaries closer than a minimum bin width
    /// are merged first, because instruments report the same nominal edge with small
    /// floating-point differences. Unlike the pwiz codec, which infers the scheme from the
    /// first few cycles, every spectrum is examined, so a window that first appears late
    /// in the run is still a window.
    /// </remarks>
    public static class DemuxSchemeDetector
    {
        /// <summary>Boundaries closer than this (Th) are one boundary; msconvert's minWindowSize.</summary>
        public const double DEFAULT_MINIMUM_BIN_WIDTH = 0.2;

        // Boundaries are compared as integers at this resolution (0.1 mTh), which keeps the
        // clustering exact and independent of floating-point summation order.
        private const double BOUNDARY_SCALE = 1e4;

        /// <summary>
        /// Detects the scheme from each MS2 spectrum's isolation window, given in
        /// acquisition order.
        /// </summary>
        public static DemuxScheme Detect(IReadOnlyList<IsolationWindow> spectrumWindows,
            double minimumBinWidth = DEFAULT_MINIMUM_BIN_WIDTH)
        {
            if (spectrumWindows == null)
                throw new ArgumentNullException(nameof(spectrumWindows));

            int n = spectrumWindows.Count;
            var boundaryKeys = new long[2 * n];
            for (int i = 0; i < n; i++)
            {
                boundaryKeys[2 * i] = ToKey(spectrumWindows[i].LowerBound);
                boundaryKeys[2 * i + 1] = ToKey(spectrumWindows[i].UpperBound);
            }

            var boundaries = MergeBoundaries(boundaryKeys, minimumBinWidth,
                out long[] uniqueKeys, out int[] clusterOfUniqueKey);

            // Snap every spectrum's window onto the merged boundaries. Two windows that snap to
            // the same pair of boundaries are the same window, whatever their raw digits.
            var windowOfPair = new Dictionary<(int, int), int>();
            var pairs = new List<(int Lower, int Upper)>();
            var rawWindowOfSpectrum = new int[n];
            for (int i = 0; i < n; i++)
            {
                int lower = clusterOfUniqueKey[Array.BinarySearch(uniqueKeys, boundaryKeys[2 * i])];
                int upper = clusterOfUniqueKey[Array.BinarySearch(uniqueKeys, boundaryKeys[2 * i + 1])];
                if (upper <= lower)
                {
                    throw new InvalidOperationException(string.Format(
                        @"Isolation window [{0}, {1}] is narrower than the minimum bin width {2}.",
                        spectrumWindows[i].LowerBound, spectrumWindows[i].UpperBound, minimumBinWidth));
                }
                if (!windowOfPair.TryGetValue((lower, upper), out int w))
                {
                    w = pairs.Count;
                    windowOfPair.Add((lower, upper), w);
                    pairs.Add((lower, upper));
                }
                rawWindowOfSpectrum[i] = w;
            }

            // Order windows by m/z so window indices mean the same thing in every run.
            var order = new int[pairs.Count];
            for (int w = 0; w < order.Length; w++)
                order[w] = w;
            Array.Sort(order, (a, b) => // Array.Sort OK: the pair key is unique per window, so no ties
            {
                int c = pairs[a].Lower.CompareTo(pairs[b].Lower);
                return c != 0 ? c : pairs[a].Upper.CompareTo(pairs[b].Upper);
            });
            var rank = new int[order.Length];
            for (int r = 0; r < order.Length; r++)
                rank[order[r]] = r;

            // A boundary interval is a bin only if some window covers it; the gaps between
            // non-contiguous windows are not bins.
            var coverage = new int[boundaries.Length];
            foreach (var pair in pairs)
            {
                for (int b = pair.Lower; b < pair.Upper; b++)
                    coverage[b]++;
            }
            var bins = new List<DemuxBin>();
            var binOfInterval = new int[boundaries.Length];
            int maxCoverage = 0;
            for (int b = 0; b + 1 < boundaries.Length; b++)
            {
                binOfInterval[b] = -1;
                if (coverage[b] == 0)
                    continue;
                binOfInterval[b] = bins.Count;
                bins.Add(new DemuxBin(boundaries[b], boundaries[b + 1]));
                maxCoverage = Math.Max(maxCoverage, coverage[b]);
            }

            // The overlap factor is the coverage that spans the most m/z, not the largest
            // coverage anywhere. Ordinary DIA whose adjacent windows overlap by a margin of
            // 0.5 to 1 Th has thin slivers covered twice but is covered once almost everywhere,
            // and it is not a staggered design to demultiplex. Ties go to the lower coverage.
            var widthAtCoverage = new double[maxCoverage + 1];
            for (int b = 0; b + 1 < boundaries.Length; b++)
                widthAtCoverage[coverage[b]] += boundaries[b + 1] - boundaries[b];
            int overlapFactor = 1;
            for (int k = 2; k <= maxCoverage; k++)
            {
                if (widthAtCoverage[k] > widthAtCoverage[overlapFactor])
                    overlapFactor = k;
            }

            var windows = new AcquisitionWindow[pairs.Count];
            for (int r = 0; r < order.Length; r++)
            {
                var pair = pairs[order[r]];
                windows[r] = new AcquisitionWindow(r, boundaries[pair.Lower], boundaries[pair.Upper],
                    binOfInterval[pair.Lower], binOfInterval[pair.Upper - 1]);
            }

            var windowOfSpectrum = new int[n];
            for (int i = 0; i < n; i++)
                windowOfSpectrum[i] = rank[rawWindowOfSpectrum[i]];

            var kind = overlapFactor > 1 ? DemuxSchemeKind.overlapping : DemuxSchemeKind.non_overlapping;
            return new DemuxScheme(kind, bins, windows, overlapFactor, maxCoverage, windowOfSpectrum);
        }

        private static long ToKey(double mz)
        {
            return (long)Math.Round(mz * BOUNDARY_SCALE);
        }

        /// <summary>
        /// Clusters the distinct boundary keys: a boundary within the minimum bin width of the
        /// first member of the current cluster joins it. Returns each cluster's m/z (the mean of
        /// its distinct members), and maps every distinct key to its cluster.
        /// </summary>
        private static double[] MergeBoundaries(long[] keys, double minimumBinWidth,
            out long[] uniqueKeys, out int[] clusterOfUniqueKey)
        {
            var sorted = (long[])keys.Clone();
            Array.Sort(sorted); // Array.Sort OK: primitive longs, so tied values are indistinguishable
            var unique = new List<long>();
            foreach (long key in sorted)
            {
                if (unique.Count == 0 || unique[unique.Count - 1] != key)
                    unique.Add(key);
            }
            uniqueKeys = unique.ToArray();
            clusterOfUniqueKey = new int[uniqueKeys.Length];

            long mergeDistance = (long)Math.Round(minimumBinWidth * BOUNDARY_SCALE);
            var clusters = new List<double>();
            int start = 0;
            while (start < uniqueKeys.Length)
            {
                int end = start + 1;
                while (end < uniqueKeys.Length && uniqueKeys[end] - uniqueKeys[start] <= mergeDistance)
                    end++;
                long sum = 0;
                for (int i = start; i < end; i++)
                {
                    sum += uniqueKeys[i];
                    clusterOfUniqueKey[i] = clusters.Count;
                }
                clusters.Add(sum / (double)(end - start) / BOUNDARY_SCALE);
                start = end;
            }
            return clusters.ToArray();
        }
    }
}
