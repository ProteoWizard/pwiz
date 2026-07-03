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
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;

namespace pwiz.Osprey.Tasks
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
        /// Retained set: the join-wide first-pass base_id set that FirstJoin
        /// computed with every file in memory and persisted in the
        /// <c>reconciliation.json</c> envelope (v3 <c>first_pass_base_ids</c>,
        /// <see cref="RescoreInputs.GlobalFirstPassBaseIds"/>). Consuming it here
        /// is what makes a per-file HPC worker compact to the SAME set as the
        /// in-memory straight-through pipeline. The set is REQUIRED: when it is
        /// absent this method hard-fails rather than recompute a per-file subset,
        /// which on a single-file worker would be only a per-file subset and
        /// silently diverge from the in-memory run (regression mode3).
        ///
        /// The survival predicate itself -- the peptide/precursor FDR gate plus
        /// the protein-FDR rescue -- is applied UPSTREAM by FirstJoin when it
        /// builds that set (<see cref="FirstJoinTask"/>'s compaction), matching
        /// Rust's <c>rescore::run_rescore</c>. This method only consumes the set.
        /// </summary>
        public static Stats Apply(RescoreInputs inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            // 1. The join-wide passing base_id set is authoritative: FirstJoin
            //    computed it with every file in memory and persisted it in the
            //    reconciliation.json envelope. A worker missing it would have to
            //    recompute a PER-FILE subset and silently diverge from the
            //    in-memory pipeline (regression mode3), so fail loudly rather than
            //    compact to a "close-enough" set.
            if (inputs.GlobalFirstPassBaseIds == null)
            {
                throw new InvalidOperationException(
                    "RescoreCompaction: RescoreInputs.GlobalFirstPassBaseIds is null. The " +
                    "reconciliation.json envelope must carry the join-wide first-pass base_id " +
                    "set (format v3); recomputing per file would diverge from the in-memory run.");
            }

            int entriesBefore = 0;
            foreach (var kvp in inputs.PerFileEntries)
                entriesBefore += kvp.Value.Count;

            // Compact to exactly that set. Decoys need no separate predicate: a
            // target and its paired decoy share a base_id, so a target in the set
            // keeps its decoy via the base_id retain step below.
            var firstPassBaseIds = new HashSet<uint>(inputs.GlobalFirstPassBaseIds);

            // 2. Snapshot pre-compaction (file, entry_id) -> action so
            //    the action map can be rebuilt with post-compaction
            //    vec_idx values. We have to do this BEFORE per_file_entries
            //    shrinks, because the saved (file, vec_idx) keys are about
            //    to become stale.
            //
            //    ALSO: union firstPassBaseIds with the base_ids of every
            //    entry the planner emits a reconciliation action for. The
            //    planner runs compute_consensus_rts (cross-file consensus
            //    rescue), so an entry whose own file fails local first-pass
            //    FDR can still be a reconciliation target when its peptide
            //    passes FDR in a sibling file. The local-FDR-only predicate
            //    above drops those entries, then this loop silently drops
            //    their planner actions (counted as "DroppedActions"), and
            //    the rescore engine never applies them -- the reconciled
            //    .scores.parquet ends up with stale Stage 4 apex_rt /
            //    bounds for ~200 rows per Stellar file (0.04%) and the
            //    blib output diverges from the in-memory straight-through
            //    pipeline. Mirrors the Rust Option B fix in
            //    rescore.rs::run_rescore: first_pass_base_ids = UNION of
            //    local-FDR predicate AND entry_ids in
            //    reconciliation_actions_pre. Bisected via the C# in-memory
            //    vs C# HPC-chain strict-rehydration test (Stage 6 boundary
            //    revealed apex_rt / bounds drift on cross-file-rescued
            //    entries that were dropped by worker compaction).
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
                uint entryId = entries[vecIdx].EntryId;
                actionsById[(fileName, entryId)] = kvp.Value;
                // Extend firstPassBaseIds so the entry survives the local-FDR
                // compaction predicate above. Adding the masked base_id
                // (decoy bit stripped) keeps both the target and its paired
                // decoy alive, preserving the target-decoy invariant.
                firstPassBaseIds.Add(entryId & BASE_ID_MASK);
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
