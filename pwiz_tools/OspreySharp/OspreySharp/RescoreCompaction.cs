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

using System;
using System.Collections.Generic;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR.Reconciliation;

namespace pwiz.OspreySharp
{
    /// <summary>
    /// Worker-side compaction for the Stage 6 per-file rescore worker.
    /// Mirrors the inline compaction block in
    /// <c>osprey/crates/osprey/src/rescore.rs::run_rescore</c> (the
    /// section guarded by the comment <i>"Save (file, entry_id) → action
    /// before per_file_entries shrinks"</i>).
    ///
    /// The hydration layer loads PRE-COMPACTION stubs from the parquet
    /// (every entry in the Stage 4 cache, including non-passing
    /// targets and decoys). The in-process pipeline drops non-passing
    /// entries between first-pass FDR and Stage 6 — see
    /// <c>AnalysisPipeline</c>'s <i>"First-pass compaction"</i> block.
    /// The worker has to reproduce that drop EXACTLY, otherwise it
    /// would re-score entries the in-process flow had already
    /// discarded, overwriting their Stage-4 features with
    /// Stage-6-recomputed values that diverge on the multi-scan
    /// columns. (This is the original ~324-row Stellar divergence
    /// described in the umbrella TODO Session 5 entry.)
    ///
    /// Compaction is keyed by <c>base_id</c> (the entry_id with the
    /// high decoy bit cleared), so a passing target retains its paired
    /// decoy automatically. The reconciliation action map is
    /// pre-compaction (vec_idx values point into the loaded stub list);
    /// after compaction those indices are stale, so the actions are
    /// re-keyed via a <c>(file_name, entry_id)</c> intermediate to land
    /// on the new vec_idx values.
    /// </summary>
    public static class RescoreCompaction
    {
        // EntryId encodes target/decoy in the high bit; base_id is the
        // lower 31 bits, shared by a target and its paired decoy.
        private const uint BASE_ID_MASK = 0x7FFFFFFFu;

        /// <summary>
        /// Result of <see cref="Apply"/>: compaction statistics for the
        /// caller's log line. The <see cref="RescoreInputs"/> passed in is
        /// mutated in place.
        /// </summary>
        public class Stats
        {
            /// <summary>Total stubs across all files BEFORE compaction.</summary>
            public int EntriesBefore { get; set; }

            /// <summary>Total stubs across all files AFTER compaction.</summary>
            public int EntriesAfter { get; set; }

            /// <summary>Distinct base_ids that passed the predicate.</summary>
            public int FirstPassBaseIds { get; set; }

            /// <summary>
            /// Reconciliation actions whose pre-compaction entry got
            /// dropped by compaction. Should be 0 in healthy boundary
            /// files (the planner only emits actions for passing entries);
            /// a non-zero count means the boundary file was written with
            /// different config than the worker's, or by an older binary.
            /// </summary>
            public int DroppedActions { get; set; }
        }

        /// <summary>
        /// Apply first-pass FDR compaction in place to
        /// <paramref name="inputs"/>. Mutates
        /// <see cref="RescoreInputs.PerFileEntries"/> (drops non-passing
        /// entries) and rebuilds
        /// <see cref="RescoreInputs.ReconciliationActions"/> with
        /// post-compaction <c>vec_idx</c> values.
        ///
        /// Predicate (mirrors Rust <c>rescore::run_rescore</c>'s
        /// compaction block): an entry's <c>base_id</c> is retained if
        /// EITHER <c>RunPeptideQvalue ≤ peptideGate</c> OR
        /// (<c>--protein-fdr</c> set AND <c>RunProteinQvalue ≤ proteinGate</c>).
        /// <paramref name="config"/>'s <see cref="OspreyConfig.RunFdr"/>
        /// supplies the peptide gate (matches the in-process flow's
        /// <c>peptideGate = config.RunFdr</c> at the same step), and
        /// <see cref="OspreyConfig.ProteinFdr"/> the protein-rescue
        /// gate (skipped entirely when null).
        /// </summary>
        public static Stats Apply(RescoreInputs inputs, OspreyConfig config)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (config == null) throw new ArgumentNullException(nameof(config));

            double peptideGate = config.RunFdr;
            double? proteinGate = config.ProteinFdr;

            // 1. Build the passing base_id set across all files.
            var firstPassBaseIds = new HashSet<uint>();
            int entriesBefore = 0;
            foreach (var kvp in inputs.PerFileEntries)
            {
                entriesBefore += kvp.Value.Count;
                foreach (var e in kvp.Value)
                {
                    if (e.RunPeptideQvalue <= peptideGate ||
                        (proteinGate.HasValue && e.RunProteinQvalue <= proteinGate.Value))
                    {
                        firstPassBaseIds.Add(e.EntryId & BASE_ID_MASK);
                    }
                }
            }

            // 2. Snapshot pre-compaction (file, entry_id) -> action so
            //    the action map can be rebuilt with post-compaction
            //    vec_idx values. We have to do this BEFORE per_file_entries
            //    shrinks, because the saved (file, vec_idx) keys are about
            //    to become stale.
            var actionsById = new Dictionary<(string FileName, uint EntryId), ReconcileAction>(
                inputs.ReconciliationActions.Count);
            // Build a lookup map so the per-action entry_id resolution is
            // O(1) instead of O(num_files) per action.
            var entriesByName = new Dictionary<string, List<FdrEntry>>(
                inputs.PerFileEntries.Count);
            foreach (var kvp in inputs.PerFileEntries)
                entriesByName[kvp.Key] = kvp.Value;

            foreach (var kvp in inputs.ReconciliationActions)
            {
                var (fileName, vecIdx) = kvp.Key;
                if (!entriesByName.TryGetValue(fileName, out var entries))
                    continue;
                if (vecIdx < 0 || vecIdx >= entries.Count)
                    continue;
                actionsById[(fileName, entries[vecIdx].EntryId)] = kvp.Value;
            }

            // 3. Compact each per-file entry list in place.
            foreach (var kvp in inputs.PerFileEntries)
            {
                kvp.Value.RemoveAll(e => !firstPassBaseIds.Contains(e.EntryId & BASE_ID_MASK));
                kvp.Value.TrimExcess();
            }
            int entriesAfter = 0;
            foreach (var kvp in inputs.PerFileEntries)
                entriesAfter += kvp.Value.Count;

            // 4. Rebuild reconciliation_actions with post-compaction
            //    vec_idx. Walk the now-compact list, look up each entry's
            //    (file, entry_id) in actionsById, and drop the matched
            //    action onto its new index. Anything left in actionsById
            //    after the walk references entries that compaction
            //    discarded — log the count.
            var newActions = new Dictionary<(string, int), ReconcileAction>(actionsById.Count);
            int dropped;
            foreach (var kvp in inputs.PerFileEntries)
            {
                string fileName = kvp.Key;
                var entries = kvp.Value;
                for (int newIdx = 0; newIdx < entries.Count; newIdx++)
                {
                    var key = (fileName, entries[newIdx].EntryId);
                    if (actionsById.TryGetValue(key, out var action))
                    {
                        newActions[(fileName, newIdx)] = action;
                        actionsById.Remove(key);
                    }
                }
            }
            dropped = actionsById.Count;
            inputs.ReconciliationActions = newActions;

            return new Stats
            {
                EntriesBefore = entriesBefore,
                EntriesAfter = entriesAfter,
                FirstPassBaseIds = firstPassBaseIds.Count,
                DroppedActions = dropped,
            };
        }
    }
}
