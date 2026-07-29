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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Counts of entries flagged as decoys by
    /// <see cref="LibraryDecoyMarker.ApplyLibraryDecoyMarking"/>, broken
    /// down by detection signal (DIA-NN <c>Decoy</c> column vs. protein-
    /// accession prefix match). Maps to Rust
    /// <c>osprey_core::types::MarkingStats</c>.
    /// </summary>
    public class MarkingStats
    {
        /// <summary>
        /// Entries whose <see cref="LibraryEntry.IsDecoy"/> was already
        /// true on entry (set by the loader from a <c>Decoy</c> column)
        /// and whose <see cref="LibraryEntry.Id"/> got the high bit set
        /// during this marking pass. Increments on each entry the marker
        /// canonicalised; idempotent on re-entry.
        /// </summary>
        public int NViaColumn { get; set; }

        /// <summary>
        /// Entries newly flagged as decoys this pass via prefix match on
        /// a protein accession.
        /// </summary>
        public int NViaPrefix { get; set; }

        /// <summary>Total entries flagged across both detection paths.</summary>
        public int NMarked { get { return NViaColumn + NViaPrefix; } }
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
        /// Scan <paramref name="library"/> for decoys, using two signals:
        /// (a) entries whose <see cref="LibraryEntry.IsDecoy"/> was set by
        /// the loader (from a DIA-NN <c>Decoy</c> column) -- these get
        /// <see cref="LibraryEntry.DECOY_ID_BIT"/> ORed into their Id if
        /// not already set, contributing to <see cref="MarkingStats.NViaColumn"/>;
        /// (b) entries whose protein accession matches one of
        /// <paramref name="prefixes"/> (case-insensitive) -- these get
        /// <see cref="LibraryEntry.IsDecoy"/> = true and DECOY_ID_BIT,
        /// contributing to <see cref="MarkingStats.NViaPrefix"/>.
        ///
        /// Idempotent: a second call is a no-op (no double-OR of the
        /// high bit, no double-count in stats).
        ///
        /// Library-supplied decoys are NOT paired with specific targets
        /// here; that wiring is the job of
        /// <c>LibraryDecoyPairing.PairLibraryDecoysByComposition</c> and
        /// the FDRBench manifest reader.
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
                if (entry == null)
                    continue;
                if (entry.IsDecoy)
                {
                    // Loader (Decoy column) already flagged this one; make
                    // sure the high bit on Id is set so base_id pairing
                    // works.
                    if ((entry.Id & LibraryEntry.DECOY_ID_BIT) == 0u)
                    {
                        entry.Id |= LibraryEntry.DECOY_ID_BIT;
                        stats.NViaColumn++;
                    }
                    continue;
                }
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
