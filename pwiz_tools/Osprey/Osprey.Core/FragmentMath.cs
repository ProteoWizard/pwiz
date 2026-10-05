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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Stateless library-fragment m/z utilities: top-6 fragment selection
    /// (memoized per library entry) and the top-N spectrum-match prefilter.
    ///
    /// Relocated verbatim out of <c>AbstractScoringTask</c> (which had
    /// accreted these alongside its I/O and orchestration). The arithmetic
    /// is unchanged from the original so cross-impl parity is unaffected.
    /// The fragment-overlap counter that paired with these lives in
    /// <c>Osprey.Scoring.FragmentOverlap</c> instead, because it
    /// depends on <c>ScoringMath</c> (the Core leaf cannot reference Scoring).
    /// </summary>
    public static class FragmentMath
    {
        /// <summary>
        /// Values <see cref="GetTopNFragmentWindows"/> writes at most: a lower and an upper
        /// bound for each of the top 6 fragments.
        /// </summary>
        public const int TOP_N_WINDOW_VALUES = 12;

        /// <summary>
        /// Cached top-6 fragment m/z values for an entry. Computed once,
        /// reused across all prefilter calls for the same entry. Thread-safe
        /// via ConcurrentDictionary.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, double[]> _top6MzCache =
            new ConcurrentDictionary<uint, double[]>();

        /// <summary>
        /// Drop the memoized top-6 table. Keyed by full entry Id and never evicted, it grows to
        /// the size of the WHOLE library during Stages 3-4 - library-derived spectral data that
        /// outlives the spectra themselves, on the order of a gigabyte at SEA-AD scale.
        ///
        /// <para>Safe to call at any time: this is a pure memo of
        /// <see cref="LibraryEntry.Fragments"/>, so an entry that still holds its spectrum
        /// recomputes the identical value on the next call. Called from the library-fragment
        /// release, whose retained entries are the only ones that can be asked again.</para>
        /// </summary>
        public static void ClearTop6MzCache()
        {
            _top6MzCache.Clear();
        }

        private static double[] GetTop6FragmentMzs(LibraryEntry entry)
        {
            // Entry passed as the factory argument, not captured: a cache hit allocates nothing.
            return _top6MzCache.GetOrAdd(entry.Id, static (_, e) =>
            {
                var frags = e.Fragments;
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
            }, entry);
        }

        /// <summary>
        /// Check if at least 2 of the top 6 library fragments have matching peaks
        /// in the spectrum. Uses cached top-6 m/z values (no allocation per call).
        /// Port of has_topn_fragment_match in osprey-scoring/src/lib.rs:112.
        /// </summary>
        public static bool HasTopNFragmentMatch(
            LibraryEntry entry, double[] spectrumMzs, FragmentToleranceConfig fragTol)
        {
            return HasTopNFragmentMatch(entry, new Spectrum { Mzs = spectrumMzs }, fragTol);
        }

        /// <summary>
        /// <see cref="HasTopNFragmentMatch(LibraryEntry, double[], FragmentToleranceConfig)"/>
        /// for a spectrum, through its m/z bucket index (<see cref="Spectrum.MzLowerBound"/>):
        /// the binary search's lower bound, found in O(1). Computes the entry's windows on every
        /// call; the scan-major passes, which match one entry against many spectra, compute them
        /// once with <see cref="GetTopNFragmentWindows"/> and call the
        /// <see cref="HasTopNFragmentMatch(ReadOnlySpan{double}, Spectrum)"/> overload, the hot
        /// path and the one implementation.
        /// </summary>
        public static bool HasTopNFragmentMatch(
            LibraryEntry entry, Spectrum spectrum, FragmentToleranceConfig fragTol)
        {
            Span<double> windows = stackalloc double[TOP_N_WINDOW_VALUES];
            int nValues = GetTopNFragmentWindows(entry, fragTol, windows);
            return HasTopNFragmentMatch(windows.Slice(0, nValues), spectrum);
        }

        /// <summary>
        /// Write the m/z windows <see cref="HasTopNFragmentMatch(LibraryEntry, Spectrum, FragmentToleranceConfig)"/>
        /// tests for the entry's top-6 fragments into <paramref name="windows"/>, packed
        /// lower0, upper0, lower1, upper1, ..., and return how many values were written (none
        /// when the entry has no fragments, which matches every spectrum). Lets a caller that
        /// matches one entry against many spectra compute them once.
        /// </summary>
        public static int GetTopNFragmentWindows(
            LibraryEntry entry, FragmentToleranceConfig fragTol, Span<double> windows)
        {
            var frags = entry.Fragments;
            if (frags == null || frags.Count == 0)
                return 0;

            // Per-fragment tolerance: in ppm mode, each fragment's Da window
            // depends on its own m/z. Matches Rust has_topn_fragment_match.
            double[] top6Mzs = GetTop6FragmentMzs(entry);
            for (int t = 0; t < top6Mzs.Length; t++)
            {
                double mz = top6Mzs[t];
                double tolDa = fragTol.ToleranceDa(mz);
                windows[2 * t] = mz - tolDa;
                windows[2 * t + 1] = mz + tolDa;
            }
            return 2 * top6Mzs.Length;
        }

        /// <summary>
        /// <see cref="HasTopNFragmentMatch(LibraryEntry, Spectrum, FragmentToleranceConfig)"/>
        /// over windows from <see cref="GetTopNFragmentWindows"/>: at least 2 of them (1 when
        /// there is only one) hold a spectrum peak. True for no windows or an empty spectrum.
        /// </summary>
        public static bool HasTopNFragmentMatch(ReadOnlySpan<double> fragmentWindows, Spectrum spectrum)
        {
            var spectrumMzs = spectrum.Mzs;
            if (fragmentWindows.Length == 0 || spectrumMzs == null || spectrumMzs.Length == 0)
                return true;

            int nTop = fragmentWindows.Length / 2;
            int requiredMatches = nTop <= 1 ? 1 : 2;
            int matchCount = 0;
            for (int t = 0; t < nTop; t++)
            {
                int lo = spectrum.MzLowerBound(fragmentWindows[2 * t]);
                if (lo < spectrumMzs.Length && spectrumMzs[lo] <= fragmentWindows[2 * t + 1])
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
