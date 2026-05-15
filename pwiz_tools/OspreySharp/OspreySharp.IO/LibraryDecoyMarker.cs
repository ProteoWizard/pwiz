/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.IO
{
    /// <summary>
    /// Counts of entries flagged as decoys by
    /// <see cref="LibraryDecoyMarker.ApplyLibraryDecoyMarking"/>.
    /// Initially carries only the prefix-match count; commit 4 of the
    /// library-decoy catch-up extends this with a column-flagged count
    /// for DIA-NN TSV loads that carry a <c>Decoy</c> column.
    /// </summary>
    public class MarkingStats
    {
        /// <summary>
        /// Entries flagged as decoys this pass via prefix match on a
        /// protein accession. Entries already flagged on entry to
        /// <c>ApplyLibraryDecoyMarking</c> are NOT counted here.
        /// </summary>
        public int NViaPrefix { get; set; }

        /// <summary>Total entries newly flagged in this marking pass.</summary>
        public int NMarked { get { return NViaPrefix; } }
    }

    /// <summary>
    /// Marks library entries as decoys based on protein-accession prefixes.
    /// Maps to Rust <c>osprey_core::types::apply_library_decoy_marking</c>.
    ///
    /// This is the post-load companion to <c>DecoyGenerator</c>: when the
    /// user supplies a library that already contains decoys (e.g., DIA-NN
    /// or EncyclopeDIA output with <c>rev_</c> / <c>DECOY_</c> prefixes on
    /// protein accessions), this marking step sets the same metadata that
    /// <c>DecoyGenerator</c> would, so downstream FDR sees decoys as decoys.
    /// </summary>
    public static class LibraryDecoyMarker
    {
        /// <summary>
        /// Scan <paramref name="library"/> and flag entries whose protein
        /// accession matches one of <paramref name="prefixes"/> (case-
        /// insensitive prefix match). For each matching entry, sets
        /// <see cref="LibraryEntry.IsDecoy"/> = true and ORs
        /// <see cref="LibraryEntry.DECOY_ID_BIT"/> into the Id.
        ///
        /// Idempotent: entries already flagged as decoys are left
        /// unchanged (no double-OR of the high bit, no double-count in
        /// <see cref="MarkingStats.NMarked"/>).
        ///
        /// Library-supplied decoys are NOT paired with specific targets
        /// via matching base_ids; that wiring is the job of the
        /// composition pairer (commit 2) and manifest reader (commit 3).
        /// </summary>
        public static void ApplyLibraryDecoyMarking(
            IList<LibraryEntry> library,
            IList<string> prefixes,
            out MarkingStats stats)
        {
            stats = new MarkingStats();
            if (library == null || library.Count == 0)
                return;
            for (int i = 0; i < library.Count; i++)
            {
                var entry = library[i];
                if (entry == null || entry.IsDecoy)
                    continue;
                if (entry.LooksLikeLibraryDecoy(prefixes))
                {
                    entry.IsDecoy = true;
                    entry.Id |= LibraryEntry.DECOY_ID_BIT;
                    stats.NViaPrefix++;
                }
            }
        }
    }
}
