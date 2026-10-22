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

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// Centroids time-of-flight profile spectra without dropping single ion events. Each run of
    /// adjacent sampling points with signal is one centroid at its intensity-weighted m/z, carrying
    /// the run's summed intensity; a run holding two peaks is split at a valley under half the smaller
    /// of them. Adjacency is judged in sqrt(m/z), in which a TOF digitizer samples uniformly.
    /// </summary>
    /// <remarks>
    /// SCIEX's vendor centroiding of ZenoTOF 8600 ZT Scan spectra reports no peak of a single event:
    /// every one-point 100-count event is dropped, about a fifth of a spectrum's profile intensity.
    /// On ZT Scan, where a fragment is often a few ions per spectrum, those events carry part of the
    /// weak fragments' signal. Here one event is one ion at 100 counts, so the demultiplexer's
    /// counts-per-ion default of 100 holds.
    /// </remarks>
    public static class EventCentroider
    {
        /// <summary>A valley below this fraction of the smaller neighboring maximum splits a run.</summary>
        public const double VALLEY_FRACTION = 0.5;

        /// <summary>Points farther apart than this many sampling steps, in sqrt(m/z), are not adjacent.</summary>
        public const double GAP_STEPS = 1.5;

        /// <summary>
        /// Centroids one spectrum. <paramref name="mz"/> must be ascending; points of zero intensity
        /// may be present or absent. The centroids replace the contents of the two output lists.
        /// </summary>
        public static void Centroid(IReadOnlyList<double> mz, IReadOnlyList<double> intensity, List<double> centroidMz,
            List<double> centroidIntensity)
        {
            centroidMz.Clear();
            centroidIntensity.Clear();
            int n = Math.Min(mz.Count, intensity.Count);
            if (n == 0)
                return;
            double maxGap = GAP_STEPS * SamplingStep(mz, n);
            int i = 0;
            while (i < n)
            {
                if (intensity[i] <= 0)
                {
                    i++;
                    continue;
                }
                int start = i++;
                while (i < n && intensity[i] > 0 && Math.Sqrt(mz[i]) - Math.Sqrt(mz[i - 1]) <= maxGap)
                    i++;
                AddRun(mz, intensity, start, i, VALLEY_FRACTION, centroidMz, centroidIntensity);
            }
        }

        /// <summary>
        /// Centroids a profile sampled uniformly in m/z (an ion trap's): every local maximum is one centroid, a run
        /// split at each valley between maxima, and centroids closer than <paramref name="minSeparation"/> (Th) merged
        /// into one at their intensity-weighted m/z. Unit-resolution peaks one Th apart leave a valley at about 0.9 of
        /// their height, which <see cref="Centroid"/>'s half-height rule would merge.
        /// </summary>
        public static void CentroidLinear(IReadOnlyList<double> mz, IReadOnlyList<double> intensity, double minSeparation,
            List<double> centroidMz, List<double> centroidIntensity)
        {
            centroidMz.Clear();
            centroidIntensity.Clear();
            int n = Math.Min(mz.Count, intensity.Count);
            if (n == 0)
                return;
            double maxGap = GAP_STEPS * LinearStep(mz, n);
            var runMz = new List<double>();
            var runIntensity = new List<double>();
            int i = 0;
            while (i < n)
            {
                if (intensity[i] <= 0)
                {
                    i++;
                    continue;
                }
                int start = i++;
                while (i < n && intensity[i] > 0 && mz[i] - mz[i - 1] <= maxGap)
                    i++;
                AddRun(mz, intensity, start, i, 1.0, runMz, runIntensity);
            }
            // Merge neighbors closer than the separation, in m/z order.
            for (int c = 0; c < runMz.Count; c++)
            {
                int last = centroidMz.Count - 1;
                if (last >= 0 && runMz[c] - centroidMz[last] < minSeparation)
                {
                    double sum = centroidIntensity[last] + runIntensity[c];
                    centroidMz[last] = (centroidMz[last] * centroidIntensity[last] + runMz[c] * runIntensity[c]) / sum;
                    centroidIntensity[last] = sum;
                    continue;
                }
                centroidMz.Add(runMz[c]);
                centroidIntensity.Add(runIntensity[c]);
            }
        }

        /// <summary>
        /// The digitizer's step in sqrt(m/z): the median gap between consecutive points, which within
        /// peaks, and between the zeros a reader may keep, is one step.
        /// </summary>
        private static double SamplingStep(IReadOnlyList<double> mz, int n)
        {
            var gaps = new List<double>(n);
            for (int i = 1; i < n; i++)
            {
                double gap = Math.Sqrt(mz[i]) - Math.Sqrt(mz[i - 1]);
                if (gap > 0)
                    gaps.Add(gap);
            }
            if (gaps.Count == 0)
                return double.MaxValue;
            gaps.Sort(); // Array.Sort OK: primitive values sorted only for their median
            return gaps[gaps.Count / 2];
        }

        /// <summary>The sampling step of a profile uniform in m/z: the median gap between consecutive points.</summary>
        private static double LinearStep(IReadOnlyList<double> mz, int n)
        {
            var gaps = new List<double>(n);
            for (int i = 1; i < n; i++)
            {
                double gap = mz[i] - mz[i - 1];
                if (gap > 0)
                    gaps.Add(gap);
            }
            if (gaps.Count == 0)
                return double.MaxValue;
            gaps.Sort(); // Array.Sort OK: primitive values sorted only for their median
            return gaps[gaps.Count / 2];
        }

        /// <summary>
        /// Adds the centroids of points [start, end): one per peak, the run split at each valley
        /// deeper than <paramref name="valleyFraction"/> of the smaller of the maxima it separates.
        /// </summary>
        private static void AddRun(IReadOnlyList<double> mz, IReadOnlyList<double> intensity, int start, int end,
            double valleyFraction, List<double> centroidMz, List<double> centroidIntensity)
        {
            int segmentStart = start;
            int top = -1;  // the current segment's highest maximum
            for (int k = start; k < end; k++)
            {
                double left = k > start ? intensity[k - 1] : 0;
                double right = k + 1 < end ? intensity[k + 1] : 0;
                if (intensity[k] < left || intensity[k] <= right)
                    continue;
                // k is a maximum.
                if (top >= 0)
                {
                    int valley = top + 1;
                    for (int v = top + 2; v < k; v++)
                    {
                        if (intensity[v] < intensity[valley])
                            valley = v;
                    }
                    if (intensity[valley] < valleyFraction * Math.Min(intensity[top], intensity[k]))
                    {
                        Emit(mz, intensity, segmentStart, valley + 1, centroidMz, centroidIntensity);
                        segmentStart = valley + 1;
                        top = k;
                        continue;
                    }
                }
                if (top < 0 || intensity[k] > intensity[top])
                    top = k;
            }
            Emit(mz, intensity, segmentStart, end, centroidMz, centroidIntensity);
        }

        private static void Emit(IReadOnlyList<double> mz, IReadOnlyList<double> intensity, int start, int end,
            List<double> centroidMz, List<double> centroidIntensity)
        {
            double sum = 0, weighted = 0;
            for (int k = start; k < end; k++)
            {
                sum += intensity[k];
                weighted += intensity[k] * mz[k];
            }
            if (sum <= 0)
                return;
            centroidMz.Add(weighted / sum);
            centroidIntensity.Add(sum);
        }
    }
}
