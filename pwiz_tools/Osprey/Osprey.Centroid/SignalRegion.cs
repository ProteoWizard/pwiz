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
    /// <summary>A run of profile points [Start, End] above the noise, each one fitted by its own lasso.</summary>
    public readonly struct SignalRegion
    {
        public SignalRegion(int start, int end, double mzStart, double mzEnd)
        {
            Start = start;
            End = end;
            MzStart = mzStart;
            MzEnd = mzEnd;
        }

        /// <summary>The first point, inclusive.</summary>
        public int Start { get; }

        /// <summary>The last point, inclusive.</summary>
        public int End { get; }

        public double MzStart { get; }
        public double MzEnd { get; }

        public int Count => End - Start + 1;
        public double CenterMz => (MzStart + MzEnd) / 2;

        /// <summary>
        /// The runs of points above <c>baseline + thresholdSigma * noiseSigma</c>, runs separated by no more than
        /// mergeGap points joined, each extended by extension points on both sides (within the spectrum) and kept
        /// when at least minWidth points wide. Ascending, but not always disjoint: runs more than mergeGap and fewer
        /// than 2 * extension points apart give regions that share points, as Rust Centrix's do (pass 2 replaces a
        /// refit region's centroids by its m/z range, as Rust does).
        /// </summary>
        public static List<SignalRegion> Detect(double[] mz, double[] intensity, double baseline, double noiseSigma,
            double thresholdSigma, int mergeGap, int extension, int minWidth)
        {
            var regions = new List<SignalRegion>();
            int n = mz.Length;
            if (n < 3)
                return regions;

            double threshold = baseline + thresholdSigma * noiseSigma;
            // The runs above the threshold, each joined to the one before when the gap allows.
            var runs = new List<(int Start, int End)>();
            int runStart = -1;
            for (int i = 0; i <= n; i++)
            {
                bool above = i < n && intensity[i] > threshold;
                if (above && runStart < 0)
                {
                    runStart = i;
                }
                else if (!above && runStart >= 0)
                {
                    int runEnd = i - 1;
                    if (runs.Count > 0 && runStart <= runs[runs.Count - 1].End + mergeGap + 1)
                        runs[runs.Count - 1] = (runs[runs.Count - 1].Start, runEnd);
                    else
                        runs.Add((runStart, runEnd));
                    runStart = -1;
                }
            }

            foreach (var (s, e) in runs)
            {
                int start = Math.Max(s - extension, 0);
                int end = Math.Min(e + extension, n - 1);
                if (end - start + 1 < minWidth)
                    continue;
                regions.Add(new SignalRegion(start, end, mz[start], mz[end]));
            }
            return regions;
        }
    }
}
