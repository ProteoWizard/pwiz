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

using System.Collections.Generic;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;

namespace pwiz.Osprey.Tasks
{
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
        /// Call <see cref="LibraryEntry.ReleaseSpectrum"/> on every entry whose base_id is
        /// outside <paramref name="retainedBaseIds"/> and return how many were released.
        /// Identity fields
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
                if (retainedBaseIds.Contains(e.Id & ScoringTaskShared.BASE_ID_MASK))
                    continue;
                if (e.ReleaseSpectrum())
                    released++;
            }
            return released;
        }
    }
}
