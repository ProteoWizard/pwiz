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
using System.Collections.Generic;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Top-N fragment-overlap counting between two library fragment lists.
    ///
    /// Relocated verbatim out of <c>AbstractScoringTask</c>. Lives in
    /// Scoring rather than Core (alongside its sibling fragment helpers in
    /// <c>OspreySharp.Core.FragmentMath</c>) because it uses
    /// <see cref="ScoringMath.LowerBoundDouble"/> for the m/z lower-bound
    /// search, and the Core leaf cannot reference Scoring.
    /// </summary>
    public static class FragmentOverlap
    {
        /// <summary>
        /// Count how many of the top-N (by intensity) m/z values from
        /// <paramref name="fragsA"/> have a match within tolerance in
        /// the top-N of <paramref name="fragsB"/>. Mirrors
        /// osprey/crates/osprey/src/pipeline.rs::count_topn_fragment_overlap.
        /// </summary>
        public static int CountTopNFragmentOverlap(
            IList<LibraryFragment> fragsA, IList<LibraryFragment> fragsB,
            int n, double tolerance, ToleranceUnit unit)
        {
            double[] topA = TopNFragmentMzs(fragsA, n);
            double[] topB = TopNFragmentMzs(fragsB, n);
            Array.Sort(topB); // Array.Sort OK: single primitive array used only for binary-search of m/z; tie-ordering doesn't affect match-or-not
            int matches = 0;
            for (int i = 0; i < topA.Length; i++)
            {
                double mz = topA[i];
                double tolDa = unit == ToleranceUnit.Ppm ? mz * tolerance / 1e6 : tolerance;
                double lo = mz - tolDa;
                double hi = mz + tolDa;
                int idx = ScoringMath.LowerBoundDouble(topB, lo);
                if (idx < topB.Length && topB[idx] <= hi) matches++;
            }
            return matches;
        }

        /// <summary>Get m/z values of top-N fragments by intensity (stable on ties).</summary>
        private static double[] TopNFragmentMzs(IList<LibraryFragment> fragments, int n)
        {
            if (fragments.Count <= n)
            {
                var all = new double[fragments.Count];
                for (int i = 0; i < fragments.Count; i++) all[i] = fragments[i].Mz;
                return all;
            }
            // Stable sort by descending intensity (matches Rust slice::sort_by).
            var idx = new int[fragments.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            Array.Sort(idx, (a, b) => // Array.Sort OK: comparator's secondary key is the unique fragment index, so no ties
            {
                int c = fragments[b].RelativeIntensity.CompareTo(fragments[a].RelativeIntensity);
                return c != 0 ? c : a.CompareTo(b);
            });
            var top = new double[n];
            for (int i = 0; i < n; i++) top[i] = fragments[idx[i]].Mz;
            return top;
        }
    }
}
