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

namespace pwiz.Osprey.Centroid
{
    /// <summary>
    /// A spectrum's baseline and noise sigma. First rough, from order statistics of all its intensities; then refined
    /// along m/z from the residuals of the first pass's fits and the points outside every region, so the second
    /// pass's penalty follows the noise where it changes. The rough estimate counts every point given, so a profile
    /// with its zero points dropped (as the joint demultiplexer's shares are) reads a higher noise, and a higher
    /// penalty, than the same profile with them; both are as Centrix computes it.
    /// </summary>
    public sealed class NoiseProfile
    {
        private readonly double[] _refinedMz;
        private readonly double[] _refinedSigma;

        internal NoiseProfile(double baseline, double noiseSigma, double[] refinedMz, double[] refinedSigma)
        {
            Baseline = baseline;
            NoiseSigma = noiseSigma;
            _refinedMz = refinedMz;
            _refinedSigma = refinedSigma;
        }

        /// <summary>The rough baseline: the 10th percentile intensity, at least 0.</summary>
        public double Baseline { get; }

        /// <summary>
        /// The rough noise sigma: the interquartile range / 1.349, at least a tenth of the baseline and at least 1, so
        /// it assumes intensities on the scale of a vendor's (an ion trap's rates), not of ions.
        /// </summary>
        public double NoiseSigma { get; }

        public bool IsRefined => _refinedMz != null;

        /// <summary>The rough estimate from a spectrum's intensities.</summary>
        public static NoiseProfile Rough(double[] intensity)
        {
            int n = intensity.Length;
            if (n == 0)
                return new NoiseProfile(0, 1, null, null);
            var sorted = (double[])intensity.Clone();
            Array.Sort(sorted); // Array.Sort OK: equal doubles are indistinguishable
            double p10 = Math.Max(sorted[n / 10], 0);
            double p25 = sorted[n / 4];
            double p75 = sorted[n * 3 / 4];
            double sigma = Math.Max(Math.Max((p75 - p25) / 1.349, p10 * 0.1), 1.0);
            return new NoiseProfile(p10, sigma, null, null);
        }

        /// <summary>
        /// The noise sigma at an m/z: interpolated linearly between the refined windows' centers and held at the
        /// end values beyond them; the rough sigma when not refined.
        /// </summary>
        public double NoiseAt(double mz)
        {
            if (_refinedMz == null)
                return NoiseSigma;
            int last = _refinedMz.Length - 1;
            if (mz <= _refinedMz[0])
                return _refinedSigma[0];
            if (mz >= _refinedMz[last])
                return _refinedSigma[last];
            // The first window center at or above mz.
            int hi = 1;
            while (_refinedMz[hi] < mz)
                hi++;
            double t = (mz - _refinedMz[hi - 1]) / (_refinedMz[hi] - _refinedMz[hi - 1]);
            return _refinedSigma[hi - 1] + t * (_refinedSigma[hi] - _refinedSigma[hi - 1]);
        }

        /// <summary>
        /// The noise refined in windows of windowTh Th stepped by stepTh along the spectrum: in each, the root mean
        /// square of the intensities of the points outside every region and of the residuals of the regions centered
        /// in it, when at least 5, floored at a tenth of the rough sigma; then each window's value replaced by the
        /// median of it and its neighbors. Unrefined when no region was fitted or no window had enough values.
        /// The m/z must ascend.
        /// </summary>
        public NoiseProfile Refine(double[] mz, double[] intensity, IReadOnlyList<SignalRegion> regions,
            IReadOnlyList<(double CenterMz, double[] Residuals)> residuals, double windowTh, double stepTh)
        {
            int n = mz.Length;
            if (n == 0 || residuals.Count == 0)
                return this;

            var inRegion = new bool[n];
            foreach (var region in regions)
            {
                for (int i = region.Start; i <= Math.Min(region.End, n - 1); i++)
                    inRegion[i] = true;
            }

            var centers = new List<double>();
            var sigmas = new List<double>();
            double half = windowTh / 2;
            double mzMax = mz[n - 1];
            int first = 0;
            // The center steps by addition, as Centrix's does, so the windows fall where its windows fall.
            for (double center = mz[0] + half; center <= mzMax - half; center += stepTh)
            {
                double lo = center - half;
                double hi = center + half;
                double sumSquares = 0;
                int count = 0;
                while (first < n && mz[first] < lo)
                    first++;
                for (int i = first; i < n && mz[i] <= hi; i++)
                {
                    if (!inRegion[i])
                    {
                        sumSquares += intensity[i] * intensity[i];
                        count++;
                    }
                }
                foreach (var (centerMz, values) in residuals)
                {
                    if (centerMz >= lo && centerMz <= hi)
                    {
                        foreach (double r in values)
                            sumSquares += r * r;
                        count += values.Length;
                    }
                }
                if (count >= 5)
                {
                    double rms = Math.Sqrt(sumSquares / count);
                    if (rms > 0)
                    {
                        centers.Add(center);
                        sigmas.Add(Math.Max(rms, NoiseSigma * 0.1));
                    }
                }
            }
            if (centers.Count == 0)
                return this;

            return new NoiseProfile(Baseline, NoiseSigma, centers.ToArray(), MedianOfThree(sigmas));
        }

        /// <summary>Each value replaced by the median of it and its neighbors; an end's median of two is the larger.</summary>
        private static double[] MedianOfThree(List<double> values)
        {
            int n = values.Count;
            var smoothed = values.ToArray();
            if (n < 3)
                return smoothed;
            for (int i = 0; i < n; i++)
            {
                int lo = Math.Max(i - 1, 0);
                int hi = Math.Min(i + 1, n - 1);
                var window = new double[hi - lo + 1];
                for (int k = lo; k <= hi; k++)
                    window[k - lo] = values[k];
                Array.Sort(window); // Array.Sort OK: equal doubles are indistinguishable
                smoothed[i] = window[window.Length / 2];
            }
            return smoothed;
        }
    }
}
