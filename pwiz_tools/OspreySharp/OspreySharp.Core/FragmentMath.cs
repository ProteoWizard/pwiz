/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
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
using System.Collections.Concurrent;
using System.Linq;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Stateless library-fragment m/z utilities: top-6 fragment selection
    /// (memoized per library entry) and the top-N spectrum-match prefilter.
    ///
    /// Relocated verbatim out of <c>AbstractScoringTask</c> (which had
    /// accreted these alongside its I/O and orchestration). The arithmetic
    /// is unchanged from the original so cross-impl parity is unaffected.
    /// The fragment-overlap counter that paired with these lives in
    /// <c>OspreySharp.Scoring.FragmentOverlap</c> instead, because it
    /// depends on <c>ScoringMath</c> (the Core leaf cannot reference Scoring).
    /// </summary>
    public static class FragmentMath
    {
        /// <summary>
        /// Cached top-6 fragment m/z values for an entry. Computed once,
        /// reused across all prefilter calls for the same entry. Thread-safe
        /// via ConcurrentDictionary.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, double[]> _top6MzCache =
            new ConcurrentDictionary<uint, double[]>();

        private static double[] GetTop6FragmentMzs(LibraryEntry entry)
        {
            return _top6MzCache.GetOrAdd(entry.Id, _ =>
            {
                var frags = entry.Fragments;
                if (frags == null || frags.Count == 0)
                    return new double[0];

                int nTop = Math.Min(frags.Count, 6);
                if (frags.Count <= 6)
                {
                    var mzs = new double[frags.Count];
                    for (int i = 0; i < frags.Count; i++)
                        mzs[i] = frags[i].Mz;
                    return mzs;
                }

                // Find top 6 by intensity, stable on ties to match
                // Rust slice::sort_by. Array.Sort with Comparison<T>
                // is introsort and unstable.
                var result = Enumerable.Range(0, frags.Count)
                    .OrderByDescending(i => frags[i].RelativeIntensity)
                    .Take(nTop)
                    .Select(i => frags[i].Mz)
                    .ToArray();
                return result;
            });
        }

        /// <summary>
        /// Check if at least 2 of the top 6 library fragments have matching peaks
        /// in the spectrum. Uses cached top-6 m/z values (no allocation per call).
        /// Port of has_topn_fragment_match in osprey-scoring/src/lib.rs:112.
        /// </summary>
        public static bool HasTopNFragmentMatch(
            LibraryEntry entry, double[] spectrumMzs, FragmentToleranceConfig fragTol)
        {
            var frags = entry.Fragments;
            if (frags == null || frags.Count == 0 || spectrumMzs == null || spectrumMzs.Length == 0)
                return true;

            double[] top6Mzs = GetTop6FragmentMzs(entry);
            int nTop = top6Mzs.Length;
            int requiredMatches = nTop <= 1 ? 1 : 2;
            int matchCount = 0;

            // Per-fragment tolerance: in ppm mode, each fragment's Da window
            // depends on its own m/z. Matches Rust has_topn_fragment_match.
            for (int t = 0; t < nTop; t++)
            {
                double mz = top6Mzs[t];
                double tolDa = fragTol.ToleranceDa(mz);
                double lower = mz - tolDa;
                double upper = mz + tolDa;
                int lo = 0, hi = spectrumMzs.Length;
                while (lo < hi) { int mid = (lo + hi) / 2; if (spectrumMzs[mid] < lower) lo = mid + 1; else hi = mid; }
                if (lo < spectrumMzs.Length && spectrumMzs[lo] <= upper)
                {
                    matchCount++;
                    if (matchCount >= requiredMatches)
                        return true;
                }
            }
            return false;
        }
    }
}
