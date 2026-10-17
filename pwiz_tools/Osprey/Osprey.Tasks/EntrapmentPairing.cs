/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Reconciles the searched library against the external entrapment pairing
    /// manifest, producing a clean target &lt;-&gt; entrapment pairing and identifying
    /// entrapment peptides that must be dropped because they have no target twin.
    ///
    /// The library legitimately contains peptide forms the manifest never had --
    /// notably N-terminal-Met-clipped variants generated at library-prediction time
    /// (Carafe <c>-clip_n_m</c>). Because the entrapment shuffle does not fix the
    /// N-terminus, that clip can fire on one side of a target/entrapment pair but
    /// not the other, leaving an entrapment with no target: a nonsensical artifact.
    /// FDRBench's paired estimator assumes every entrapment has a target and crashes
    /// on such a row, so we drop these here (with a warning) rather than emit them.
    ///
    /// Pairing for peptides the manifest already covers comes from the manifest.
    /// For peptides it does not (the extras), the pairing is reconstructed from the
    /// library protein accession's <c>_pep&lt;NNNNN&gt;</c> token, which a target and
    /// its entrapment/decoy quartet share (this token survives on the extras because
    /// the manifest's clean accessions -- which drop it -- are only substituted for
    /// covered peptides).
    /// </summary>
    public sealed class EntrapmentPairing
    {
        /// <summary>Peptide sequence -&gt; pair index (target and its entrapment share it).</summary>
        public readonly Dictionary<string, uint> PairIndexBySeq;

        /// <summary>
        /// Entrapment sequences excluded everywhere -- the emitted manifest, the
        /// FDRBench input, and the diagnostics classification -- because they have no
        /// target twin. Keeping all three consistent is the whole point: the HTML and
        /// FDRBench must see the same peptides.
        /// </summary>
        public readonly HashSet<string> ExcludedEntrapment;

        private EntrapmentPairing(Dictionary<string, uint> pairIndexBySeq, HashSet<string> excluded)
        {
            PairIndexBySeq = pairIndexBySeq;
            ExcludedEntrapment = excluded;
        }

        /// <summary>
        /// The pairing of the searched library, read from its entries: every non-decoy peptide
        /// with a <see cref="LibraryEntry.PairingId"/> is paired on it (a target and its
        /// entrapment share it), and an entrapment peptide with none is excluded - the orphan
        /// the load found no target twin for. The ids were assigned at load
        /// (<c>PairingIds.Assign</c>, from the manifest for a .tsv or from <c>DecoyPairs</c> for a
        /// .blib), so no manifest is read here.
        /// </summary>
        public static EntrapmentPairing Build(IReadOnlyDictionary<uint, LibraryEntry> libraryById)
        {
            var pairIndexBySeq = new Dictionary<string, uint>(StringComparer.Ordinal);
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            foreach (var lib in libraryById.Values)
            {
                if (lib == null || lib.Sequence == null)
                    continue;
                if (EntrapmentLibraryClassifier.IsDecoySide(lib.ProteinIds))
                    continue;
                if (pairIndexBySeq.ContainsKey(lib.Sequence) || excluded.Contains(lib.Sequence))
                    continue;
                if (lib.PairingId.HasValue)
                    pairIndexBySeq[lib.Sequence] = lib.PairingId.Value;
                else if (EntrapmentLibraryClassifier.IsEntrapment(lib.ProteinIds))
                    excluded.Add(lib.Sequence);
            }
            return new EntrapmentPairing(pairIndexBySeq, excluded);
        }
    }
}
