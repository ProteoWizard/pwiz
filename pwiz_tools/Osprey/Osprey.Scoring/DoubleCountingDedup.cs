/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Threading;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// The double-counting dedup (see <see cref="ScoringPipeline.DeduplicateDoubleCounting"/>):
    /// within one isolation window, of two same-class entries eluting within the RT neighborhood
    /// whose top-6 fragments overlap, the one with the lower coelution_sum is dropped.
    ///
    /// <para>The dedup groups the scored rows by the isolation window their precursor m/z falls
    /// in. When no scorable library entry falls in two windows and every library id is unique,
    /// that group is exactly the rows <see cref="CoelutionScorer.ScoreWindow"/> returned for the window, and the order
    /// the pairs are tested in is a total order on (apex RT, base id, entry id). The dedup of a
    /// window then depends on nothing outside it, so <see cref="DeduplicateWindow"/> can run it
    /// as each window finishes scoring (<see cref="IsWindowLocal"/>), with the same result as
    /// the pass over the whole file.</para>
    /// </summary>
    public class DoubleCountingDedup
    {
        private readonly List<LibraryEntry> _library;
        private readonly Dictionary<uint, int> _libIdMap;
        private readonly double _rtNeighborhood;
        private double _fragTolValue;
        private ToleranceUnit _fragTolUnit;
        private int _removedCount;
        private int _removedTargets;

        public DoubleCountingDedup(List<LibraryEntry> library, IReadOnlyList<double> ms2Rts,
            List<IsolationWindow> isolationWindows)
        {
            _library = library;
            // Library lookup by EntryId (Id may not be array-index aligned).
            _libIdMap = new Dictionary<uint, int>(library.Count);
            for (int i = 0; i < library.Count; i++)
                _libIdMap[library[i].Id] = i;
            _rtNeighborhood = RtNeighborhood(ms2Rts);
            LibraryIdsUnique = _libIdMap.Count == library.Count;
            MultiWindowEntries = CountMultiWindowEntries(library, isolationWindows);
        }

        /// <summary>
        /// True when the library ids are unique and no scorable library entry falls in two
        /// isolation windows, so <see cref="DeduplicateWindow"/> on each window's scored rows
        /// gives the same result as the pass over the whole file.
        /// </summary>
        public bool IsWindowLocal => LibraryIdsUnique && MultiWindowEntries == 0;

        /// <summary>
        /// True when no two library entries share an id.
        /// </summary>
        public bool LibraryIdsUnique { get; }

        /// <summary>
        /// The library entries with fragments whose precursor m/z falls in more than one
        /// isolation window. <see cref="CoelutionScorer.ScoreWindow"/> scores such an entry in
        /// each of them, and the pass over the whole file groups every one of its rows with
        /// both windows, so the dedup is not window-local.
        /// </summary>
        public int MultiWindowEntries { get; }

        /// <summary>
        /// Rows <see cref="DeduplicateWindow"/> has removed so far, and the targets among them.
        /// </summary>
        public int RemovedCount => _removedCount;
        public int RemovedTargets => _removedTargets;

        /// <summary>
        /// Sets the fragment tolerance pairs are matched with, from the search configuration as
        /// it is when the dedup runs (<see cref="ScoringPipeline.DoubleCountingTolerance"/>).
        /// Coelution scoring replaces <c>config.FragmentTolerance</c> with the calibrated search
        /// tolerance before it scores, so a per-window dedup sets this after that replacement,
        /// which is the value the pass over the whole file has always read.
        /// </summary>
        public void SetFragmentTolerance(MzCalibrationResult ms2Cal, OspreyConfig config)
        {
            ScoringPipeline.DoubleCountingTolerance(ms2Cal, config, out _fragTolValue, out _fragTolUnit);
        }

        /// <summary>
        /// The precursor m/z of the library entry with an entry id, or NaN when the library has
        /// no such entry.
        /// </summary>
        public double PrecursorMz(uint entryId)
        {
            return _libIdMap.TryGetValue(entryId, out int libIdx) ? _library[libIdx].PrecursorMz : double.NaN;
        }

        /// <summary>
        /// The dedup of one isolation window's scored rows: the kept rows, in their original order.
        /// Valid only when <see cref="IsWindowLocal"/>. Thread-safe across windows.
        /// </summary>
        public List<FdrEntry> DeduplicateWindow(List<FdrEntry> windowEntries)
        {
            int n = windowEntries.Count;
            var indices = new int[n];
            for (int i = 0; i < n; i++)
                indices[i] = i;
            var removed = new bool[n];
            MarkRemoved(windowEntries, indices, removed);

            var kept = new List<FdrEntry>(n);
            int removedCount = 0, removedTargets = 0;
            for (int i = 0; i < n; i++)
            {
                if (!removed[i])
                {
                    kept.Add(windowEntries[i]);
                    continue;
                }
                removedCount++;
                if (!windowEntries[i].IsDecoy)
                    removedTargets++;
            }
            Interlocked.Add(ref _removedCount, removedCount);
            Interlocked.Add(ref _removedTargets, removedTargets);
            return kept;
        }

        /// <summary>
        /// Marks in <paramref name="removed"/> the entries of one window group
        /// (<paramref name="indices"/> into <paramref name="entries"/>) that are double-counted.
        /// The pairs are tested in (apex RT, base id, entry id) order, a total order when the
        /// entry ids in the group are unique, so the result does not depend on the order of
        /// <paramref name="indices"/>.
        /// </summary>
        public void MarkRemoved(List<FdrEntry> entries, int[] indices, bool[] removed)
        {
            if (indices.Length < 2)
                return;

            int[] rtSorted = (int[])indices.Clone();
            // Stable sort: apex_rt then base_id then entry_id (matches
            // Rust's deterministic tiebreaker for the dedup pass).
            Array.Sort(rtSorted, (a, b) => // Array.Sort OK: comparator's terminal key is the unique EntryId, so no ties
                CompareOrder(entries[a].ApexRt, entries[a].EntryId,
                    entries[b].ApexRt, entries[b].EntryId));

            for (int iPos = 0; iPos < rtSorted.Length; iPos++)
            {
                int idxA = rtSorted[iPos];
                if (removed[idxA]) continue;
                double apexA = entries[idxA].ApexRt;

                for (int jPos = iPos + 1; jPos < rtSorted.Length; jPos++)
                {
                    int idxB = rtSorted[jPos];
                    double apexB = entries[idxB].ApexRt;
                    if (apexB - apexA > _rtNeighborhood) break;
                    if (removed[idxB]) continue;
                    if (entries[idxA].IsDecoy != entries[idxB].IsDecoy) continue;

                    int libIdxA, libIdxB;
                    if (!_libIdMap.TryGetValue(entries[idxA].EntryId, out libIdxA)) continue;
                    if (!_libIdMap.TryGetValue(entries[idxB].EntryId, out libIdxB)) continue;

                    if (!SharesFragments(_library[libIdxA].Fragments,
                            _library[libIdxB].Fragments, _fragTolValue, _fragTolUnit))
                        continue;

                    if (entries[idxA].CoelutionSum >= entries[idxB].CoelutionSum)
                    {
                        removed[idxB] = true;
                    }
                    else
                    {
                        removed[idxA] = true;
                        break; // idxA is gone; further pairs irrelevant
                    }
                }
            }
        }

        /// <summary>
        /// The library entries with fragments (the ones <see cref="CoelutionScorer.ScoreWindow"/>
        /// scores) whose precursor m/z falls in two or more of <paramref name="windows"/>. Each
        /// window holds [LowerBound, UpperBound). With the windows sorted by lower bound, an m/z
        /// is in two windows exactly when it lies in [LowerBound, UpperBound) of some window and
        /// below the highest upper bound of the windows before it, so those ranges are counted.
        /// </summary>
        private static int CountMultiWindowEntries(List<LibraryEntry> library, List<IsolationWindow> windows)
        {
            if (windows == null || windows.Count < 2)
                return 0;
            var byLower = new List<IsolationWindow>(windows);
            byLower.Sort((a, b) => a.LowerBound.CompareTo(b.LowerBound)); // Array.Sort OK: the ranges built below do not depend on the order of windows with equal lower bounds
            var sharedLower = new List<double>();
            var sharedUpper = new List<double>();
            double maxUpper = byLower[0].UpperBound;
            for (int i = 1; i < byLower.Count; i++)
            {
                var window = byLower[i];
                double upper = Math.Min(maxUpper, window.UpperBound);
                if (upper > window.LowerBound)
                {
                    sharedLower.Add(window.LowerBound);
                    sharedUpper.Add(upper);
                }
                maxUpper = Math.Max(maxUpper, window.UpperBound);
            }
            if (sharedLower.Count == 0)
                return 0;

            int count = 0;
            foreach (var entry in library)
            {
                if (entry.Fragments == null || entry.Fragments.Count == 0)
                    continue;
                double mz = entry.PrecursorMz;
                for (int i = 0; i < sharedLower.Count; i++)
                {
                    if (mz >= sharedLower[i] && mz < sharedUpper[i])
                    {
                        count++;
                        break;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// The apex-RT neighborhood within which the double-counting dedup compares two
        /// entries: 5 x the median spacing between distinct MS2 retention times, or 0.25 min
        /// when there are fewer than two.
        /// </summary>
        private static double RtNeighborhood(IReadOnlyList<double> ms2Rts)
        {
            var sortedRts = new List<double>(ms2Rts.Count);
            foreach (var rt in ms2Rts) sortedRts.Add(rt);
            sortedRts.Sort(); // Array.Sort OK: single primitive (double) list, sorted only to dedup and take the median spacing; tie order is irrelevant
            // Dedup adjacent identicals
            int writeIdx = 0;
            for (int i = 0; i < sortedRts.Count; i++)
            {
                if (i == 0 || sortedRts[i] != sortedRts[i - 1])
                {
                    sortedRts[writeIdx++] = sortedRts[i];
                }
            }
            if (writeIdx < sortedRts.Count)
                sortedRts.RemoveRange(writeIdx, sortedRts.Count - writeIdx);
            if (sortedRts.Count < 2)
                return 0.25; // 5 * 0.05 fallback
            var intervals = new List<double>(sortedRts.Count - 1);
            for (int i = 1; i < sortedRts.Count; i++)
                intervals.Add(sortedRts[i] - sortedRts[i - 1]);
            intervals.Sort(); // Array.Sort OK: single primitive (double) list, sorted only to take the median interval; tie order is irrelevant
            return 5.0 * intervals[intervals.Count / 2];
        }

        /// <summary>
        /// The double-counting dedup's order: apex RT, then base_id, then entry_id. The dedup
        /// tests each pair with the entry that comes first in it as the first argument of
        /// <see cref="SharesFragments"/>, which is not symmetric.
        /// </summary>
        private static int CompareOrder(double apexRtA, uint entryIdA, double apexRtB, uint entryIdB)
        {
            int c = apexRtA.CompareTo(apexRtB);
            if (c != 0)
                return c;
            uint baseA = entryIdA & 0x7FFFFFFFu;
            uint baseB = entryIdB & 0x7FFFFFFFu;
            c = baseA.CompareTo(baseB);
            if (c != 0)
                return c;
            return entryIdA.CompareTo(entryIdB);
        }

        /// <summary>
        /// The double-counting collision test on two library spectra: at least half (rounded
        /// up) of the FIRST list's top 6 fragments match the second's top 6 within the given
        /// tolerance - a count of the first list's peaks, so the test is not symmetric - where
        /// half is of the smaller top-6 list, rounded up.
        /// </summary>
        private static bool SharesFragments(IReadOnlyList<LibraryFragment> fragsA,
            IReadOnlyList<LibraryFragment> fragsB, double tolerance, ToleranceUnit unit)
        {
            int overlap = FragmentOverlap.CountTopNFragmentOverlap(fragsA, fragsB, 6,
                tolerance, unit);
            int minA = Math.Min(fragsA.Count, 6);
            int minB = Math.Min(fragsB.Count, 6);
            int threshold = (int)Math.Ceiling(Math.Min(minA, minB) * 0.5);
            return overlap >= threshold;
        }
    }
}
