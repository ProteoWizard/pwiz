/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Text.RegularExpressions;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Strips the per-peptide <c>_pepNNNNN</c> pseudo-protein suffix Carafe writes into a
    /// spectral library's ProteinID column, so protein-level statistics see real proteins.
    ///
    /// <para>Carafe emits one synthetic accession PER PEPTIDE -
    /// <c>sp|O95139_pep00019|NDUB6_HUMAN</c> - so every peptide becomes its own protein.
    /// Measured on the 3-file Stellar entrapment library: 359,656 distinct target accessions
    /// that collapse to 23,874 real ones, and on the searched subset 26,710 reported protein
    /// groups against 5,462 real proteins.</para>
    ///
    /// <para><b>The damage is not confined to the protein report.</b>
    /// <c>OSPREY_PASS2_QVALUE=protein-compact</c> builds its stratum from proteins with &gt;=2
    /// DETECTED peptides, and a protein that owns exactly one peptide can never qualify. On that
    /// same dataset the stratum collapsed to 7 proteins / 23 base_ids out of 28,813 detected
    /// peptides, which silently degrades the default second-pass mode into <c>transfer</c> - no
    /// entry recompetes, every reported q-value is the carried-over pass-1 value, and the failure
    /// is invisible in the output. After stripping: 4,022 proteins / 167,660 base_ids.</para>
    ///
    /// <para><b>Why this lives here and not in the pairing manifest.</b> A manifest already
    /// carries clean accessions in its <c>proteins</c> column and
    /// <see cref="DecoyPairingManifest.ApplyToLibrary"/> overwrites the library with them - but
    /// its only caller is the library-supplied-decoy path, which also hard-fails when the library
    /// holds no decoys. So a Carafe library searched with GENERATED decoys had no route back to
    /// real accessions, whether or not a manifest was supplied. Normalizing at load closes that
    /// gap for every mode; on a manifest run this simply finds nothing left to do, because the
    /// manifest's accessions carry no suffix.</para>
    ///
    /// <para>Entrapment markers are preserved: only the <c>_pep</c> + digits token is removed, so
    /// <c>sp|Q9ULK4_p_target_pep00052|MED23_HUMAN_p_target</c> becomes
    /// <c>sp|Q9ULK4_p_target|MED23_HUMAN_p_target</c> - exactly the form the pairing manifest
    /// uses - and a <c>decoy_</c> prefix is likewise untouched.</para>
    /// </summary>
    public static class CarafeProteinIdNormalizer
    {
        /// <summary>
        /// Cheap pre-filter so the regex only runs on accessions that could possibly match. The
        /// scan is O(entries x accessions) over a multi-million-entry library, and an ordinal
        /// IndexOf is far cheaper than a Regex.Replace that finds nothing.
        /// </summary>
        private const string PEP_TOKEN = @"_pep";

        /// <summary>
        /// Digits are required, so a real accession containing a bare "_pep" is left alone. Not
        /// anchored: the token sits mid-accession, between the source protein and the closing
        /// pipe field.
        /// </summary>
        private static readonly Regex PEP_SUFFIX =
            new Regex(@"_pep\d+", RegexOptions.CultureInvariant);

        /// <summary>
        /// Rewrite every <see cref="LibraryEntry.ProteinIds"/> carrying the suffix, warning once
        /// with the collapse it produced. Returns the number of entries rewritten (0 when the
        /// library is already clean, which is the no-op every non-Carafe library takes).
        ///
        /// <para><b>Call before <see cref="LibraryDeduplicator"/>.</b> Dedup groups on
        /// (modified sequence, charge) and unions the group's accessions through a
        /// <c>SortedSet</c>; stripping afterwards would leave two entries that differed only by
        /// <c>_pepNNNNN</c> as duplicate identical accessions in that union. Stripping first lets
        /// the existing union collapse them, which is also what pre-stripping the source TSV
        /// produces - the two must agree, because the regression goldens were captured that
        /// way.</para>
        /// </summary>
        /// <param name="library">Entries to normalize in place.</param>
        /// <param name="logWarning">Warning sink; the message names the cause and the action.</param>
        public static int Normalize(IList<LibraryEntry> library, Action<string> logWarning)
        {
            if (library == null || library.Count == 0)
                return 0;

            // One regex evaluation per DISTINCT accession rather than per entry. The loaders
            // intern accessions, so a 6.3M-entry library holds only thousands of distinct
            // strings and this map stays small.
            var cleaned = new Dictionary<string, string>(StringComparer.Ordinal);
            var realAccessions = new HashSet<string>(StringComparer.Ordinal);
            string example = null, exampleCleaned = null;
            foreach (var entry in library)
            {
                var ids = entry.ProteinIds;
                if (ids == null)
                    continue;
                for (int i = 0; i < ids.Count; i++)
                {
                    string id = ids[i];
                    if (id == null || id.IndexOf(PEP_TOKEN, StringComparison.Ordinal) < 0 ||
                        cleaned.ContainsKey(id))
                        continue;
                    string clean = PEP_SUFFIX.Replace(id, string.Empty);
                    if (string.Equals(clean, id, StringComparison.Ordinal))
                        continue;   // "_pep" with no digits after it - a real accession
                    cleaned[id] = clean;
                    realAccessions.Add(clean);
                    if (example == null)
                    {
                        example = id;
                        exampleCleaned = clean;
                    }
                }
            }
            if (cleaned.Count == 0)
                return 0;

            // Rewrite through a shared interner into a distinct, ordered array. Distinct matters:
            // once the suffix is gone, several of an entry's accessions can collapse onto one.
            var interner = new LibraryStringInterner();
            int nEntries = 0;
            foreach (var entry in library)
            {
                var ids = entry.ProteinIds;
                if (ids == null || ids.Count == 0)
                    continue;
                bool touched = false;
                for (int i = 0; i < ids.Count; i++)
                {
                    if (ids[i] != null && cleaned.ContainsKey(ids[i]))
                    {
                        touched = true;
                        break;
                    }
                }
                if (!touched)
                    continue;
                var mapped = new SortedSet<string>(StringComparer.Ordinal);
                bool hasNull = false;
                for (int i = 0; i < ids.Count; i++)
                {
                    string id = ids[i];
                    // Nulls are carried, not dropped. Dropping changed ProteinIds.Count on
                    // TOUCHED entries only, so two entries in the same library came out of one
                    // load with different shapes - a hazard for any consumer that reasons about
                    // the count or indexes it positionally against another per-entry array.
                    // Held aside rather than added to the set because SortedSet.Add is
                    // annotated non-null; re-inserted first below, where Ordinal would sort it.
                    if (id == null)
                    {
                        hasNull = true;
                        continue;
                    }
                    mapped.Add(cleaned.TryGetValue(id, out string clean) ? clean : id);
                }
                string[] rewritten = interner.InternToArray(mapped);
                if (hasNull)
                {
                    var withNull = new string[rewritten.Length + 1];
                    Array.Copy(rewritten, 0, withNull, 1, rewritten.Length);
                    rewritten = withNull;
                }
                entry.ProteinIds = rewritten;
                nEntries++;
            }

            logWarning?.Invoke(string.Format(
                @"Spectral library protein accessions carry Carafe's per-peptide '_pepNNNNN' " +
                @"suffix: {0:N0} distinct accessions on {1:N0} entries collapse to {2:N0} real " +
                @"proteins (e.g. '{3}' -> '{4}'). Stripping it - left in place every peptide is " +
                @"its own protein, which breaks protein parsimony and picked-protein FDR, and " +
                @"leaves OSPREY_PASS2_QVALUE=protein-compact with an empty stratum because no " +
                @"protein can reach 2 detected peptides.",
                cleaned.Count, nEntries, realAccessions.Count, example, exampleCleaned));
            return nEntries;
        }
    }
}
