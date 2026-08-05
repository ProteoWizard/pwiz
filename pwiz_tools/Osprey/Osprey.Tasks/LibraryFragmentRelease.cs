/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Collections;
using System.Collections.Generic;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The value a released <see cref="LibraryEntry.Fragments"/> is set to: a single shared
    /// instance that THROWS on every access.
    ///
    /// <para>Neither <c>null</c> nor <c>Array.Empty</c> works here. Every scorer already guards
    /// with <c>if (entry.Fragments == null || entry.Fragments.Count == 0)</c> - so both a null
    /// and an empty list are silently absorbed as "this entry has no spectrum", and a released
    /// entry that something still wanted would score as a degenerate zero instead of failing.
    /// That is precisely the silently-invalid output a caller would trust. Throwing turns the
    /// SAME guard expression into a tripwire: the null check short-circuits false, then
    /// <see cref="Count"/> throws and names the defect.</para>
    ///
    /// <para>We free these arrays because we believe nothing reads them. This makes a wrong
    /// belief loud instead of quiet, and costs one object for the whole process.</para>
    /// </summary>
    internal sealed class ReleasedFragments : IReadOnlyList<LibraryFragment>
    {
        public static readonly ReleasedFragments INSTANCE = new ReleasedFragments();

        private ReleasedFragments()
        {
        }

        public int Count => throw Fail();

        public LibraryFragment this[int index] => throw Fail();

        public IEnumerator<LibraryFragment> GetEnumerator()
        {
            throw Fail();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw Fail();
        }

        private static InvalidOperationException Fail()
        {
            return new InvalidOperationException(
                @"Library fragments for this entry were released at the Stage 5 -> 6 boundary " +
                @"because it is neither a compaction survivor nor a gap-fill candidate, so " +
                @"nothing should score or write it. Reaching them means the retained set is " +
                @"wrong. Set OSPREY_RELEASE_LIBRARY_FRAGMENTS=0 to keep the whole library " +
                @"resident and confirm.");
        }
    }

    /// <summary>
    /// The Stage 5 -&gt; 6 library-fragment release (issue #4532), as a pure function of the
    /// surviving base_ids and the gap-fill plan so it can be tested without a pipeline.
    /// <see cref="OspreyEnvironment.ReleaseLibraryFragments"/> carries the rationale and the
    /// safety argument; this type carries only the set arithmetic.
    /// </summary>
    internal static class LibraryFragmentRelease
    {
        /// <summary>
        /// The base_ids whose spectra are still needed after Stage 5: the post-compaction
        /// survivors plus every gap-fill candidate.
        ///
        /// <para>Gap-fill MUST be unioned in. <c>GapFillTargetIdentifier</c> resolves the
        /// MISSING charge states of passing peptides through the library, so by construction it
        /// names entries that did not survive compaction - and Stage 6 then scores them. A
        /// retained set of survivors alone would strip exactly the spectra gap-fill is about to
        /// ask for.</para>
        ///
        /// <paramref name="firstPassBaseIds"/> is already pair-symmetric (a target and its
        /// paired decoy share a base_id), so decoys ride along without being named.
        /// </summary>
        public static HashSet<uint> BuildRetainedBaseIds(
            HashSet<uint> firstPassBaseIds,
            IReadOnlyDictionary<string, List<GapFillTarget>> gapFillByFile)
        {
            var retained = firstPassBaseIds == null
                ? new HashSet<uint>()
                : new HashSet<uint>(firstPassBaseIds);
            if (gapFillByFile == null)
                return retained;

            foreach (var kvp in gapFillByFile)
            {
                if (kvp.Value == null)
                    continue;
                foreach (var g in kvp.Value)
                    retained.Add(g.TargetEntryId & ScoringTaskShared.BASE_ID_MASK);
            }
            return retained;
        }

        /// <summary>
        /// Point <see cref="LibraryEntry.Fragments"/> at <see cref="ReleasedFragments"/> on
        /// every entry whose base_id is outside <paramref name="retainedBaseIds"/>, freeing the
        /// backing array, and return how many were released. Identity fields
        /// are untouched on ALL entries, because protein parsimony and the protein-compact
        /// stratum walk the whole library after this point and read them.
        /// </summary>
        public static int ReleaseFragments(
            IList<LibraryEntry> fullLibrary, HashSet<uint> retainedBaseIds)
        {
            if (fullLibrary == null || retainedBaseIds == null)
                return 0;

            int released = 0;
            foreach (var e in fullLibrary)
            {
                // ReferenceEquals, not .Count: the sentinel THROWS on Count, so an
                // already-released entry has to be recognized by identity. That also makes the
                // pass idempotent without ever touching a released entry's contents.
                if (ReferenceEquals(e.Fragments, ReleasedFragments.INSTANCE) ||
                    retainedBaseIds.Contains(e.Id & ScoringTaskShared.BASE_ID_MASK))
                {
                    continue;
                }
                e.Fragments = ReleasedFragments.INSTANCE;
                released++;
            }
            return released;
        }
    }
}
