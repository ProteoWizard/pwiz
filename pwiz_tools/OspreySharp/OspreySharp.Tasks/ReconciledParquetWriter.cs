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
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// Writes the reconciled per-file <c>.scores-reconciled.parquet</c> for
    /// Stage 6. Reloads the original Stage 4 parquet's full per-row data
    /// (identity, boundaries, 21 PIN features, CWT candidate lists), overlays the
    /// re-scored rows in place by <see cref="FdrEntry.ParquetIndex"/>, appends
    /// gap-fill rows at the end, then writes a SEPARATE reconciled sibling with
    /// reconciliation metadata -- the original parquet is never overwritten.
    ///
    /// Extracted verbatim from <c>PerFileRescoreTask.WriteReconciledParquet</c> so
    /// the row-overlay and metadata-hash logic can be unit-tested without a live
    /// <see cref="PipelineContext"/>: the only context dependency was logging, now
    /// taken as <see cref="Action{T}"/> callbacks. Behavior (and therefore the
    /// reconciled parquet bytes) is unchanged. Mirrors Rust pipeline.rs:3050-3110.
    /// </summary>
    internal static class ReconciledParquetWriter
    {
        /// <summary>
        /// Reload <paramref name="originalPath"/>, overlay re-scored + gap-fill
        /// rows from <paramref name="fdrEntries"/>, and write the result to
        /// <paramref name="reconciledPath"/>. Returns true when the reconciled
        /// parquet was written; false on a reload/write failure (so the caller
        /// does not stamp a validity sidecar over a stale or absent output).
        /// </summary>
        internal static bool Write(string originalPath, string reconciledPath,
            List<FdrEntry> fdrEntries,
            string fileName, List<LibraryEntry> fullLibrary, OspreyConfig config,
            IReadOnlyList<string> joinFileStems,
            Action<string> logInfo, Action<string> logWarning)
        {
            // 1. Reload the original parquet's per-row state (read-only).
            List<FdrEntry> fullEntries;
            try
            {
                fullEntries = ParquetScoreCache.LoadFullFdrEntries(originalPath);
            }
            catch (Exception ex)
            {
                logWarning(string.Format(
                    "Stage 6 write-back: failed to reload {0}: {1} (skipping)",
                    originalPath, ex.Message));
                return false;
            }
            int origRowCount = fullEntries.Count;

            // 2-3. Overlay re-scored rows by ParquetIndex and append gap-fill rows.
            int nReplaced = ApplyRescoredRows(fullEntries, fdrEntries, fileName,
                logWarning, out int nAppended);

            // 4. Build libraryById for the WriteScoresParquet sequence /
            //    precursor_mz / protein_ids columns.
            var libraryById = new Dictionary<uint, LibraryEntry>(fullLibrary.Count);
            foreach (var libEntry in fullLibrary)
                libraryById[libEntry.Id] = libEntry;

            // 5. Reconciliation metadata (mirrors Rust build_reconciled_metadata).
            var metadata = BuildReconciliationMetadata(config, joinFileStems);

            try
            {
                ParquetScoreCache.WriteScoresParquet(reconciledPath, fullEntries,
                    metadata, libraryById, fileName);
            }
            catch (Exception ex)
            {
                logWarning(string.Format(
                    "Stage 6 write-back: failed to write reconciled scores for {0}: {1}",
                    fileName, ex.Message));
                return false;
            }

            logInfo(string.Format(
                "  Wrote reconciled parquet for {0}: {1} rows ({2} replaced + {3} appended; original {4} rows)",
                fileName, fullEntries.Count, nReplaced, nAppended, origRowCount));
            return true;
        }

        /// <summary>
        /// Overlay the re-scored rows in <paramref name="fdrEntries"/> onto
        /// <paramref name="fullEntries"/> (loaded from the original parquet) in
        /// place by <see cref="FdrEntry.ParquetIndex"/>, then append the gap-fill
        /// rows at the end, reassigning each gap-fill stub's
        /// <see cref="FdrEntry.ParquetIndex"/> to the row it now occupies so a
        /// downstream worker can locate its features. Returns the replaced-row
        /// count; <paramref name="nAppended"/> receives the appended-row count.
        ///
        /// Replacement is keyed on <see cref="FdrEntry.ParquetIndex"/> (NOT
        /// post-compaction Vec position; the two diverge after first-pass FDR
        /// drops non-passing entries). Re-scored rows are detected by
        /// <see cref="FdrEntry.Features"/> != null: hydration's
        /// LoadFdrStubsFromParquet does NOT populate Features, so unchanged
        /// post-compaction stubs (Features == null) leave their corresponding
        /// <paramref name="fullEntries"/> row alone, preserving Features +
        /// CwtCandidates + the binary blob columns from the original parquet.
        /// Gap-fill stubs carry ParquetIndex == uint.MaxValue and are appended.
        /// </summary>
        internal static int ApplyRescoredRows(List<FdrEntry> fullEntries,
            List<FdrEntry> fdrEntries, string fileName,
            Action<string> logWarning, out int nAppended)
        {
            // Replace re-scored rows (Phase 1 + Phase 2 existing-entry overlay).
            int nReplaced = 0;
            foreach (var entry in fdrEntries)
            {
                if (entry.ParquetIndex == uint.MaxValue)
                    continue;
                if (entry.Features == null)
                    continue;  // hydrated stub, never re-scored
                int pqIdx = (int)entry.ParquetIndex;
                if (pqIdx < 0 || pqIdx >= fullEntries.Count)
                {
                    logWarning(string.Format(
                        "Stage 6 write-back: ParquetIndex {0} out of range for {1} ({2} rows)",
                        pqIdx, fileName, fullEntries.Count));
                    continue;
                }
                fullEntries[pqIdx] = entry;
                nReplaced++;
            }

            // Append gap-fill rows at the end, reassigning ParquetIndex.
            //
            // GUARD (single-invocation invariant): this loop CONSUMES the gap-fill
            // sentinel by overwriting entry.ParquetIndex (uint.MaxValue) with the
            // appended row position in place. Callers MUST therefore pass a FRESH
            // per-file fdrEntries list per invocation -- re-running this method on
            // the same list would find no sentinels to append (so nothing re-appends)
            // while the already-reassigned gap-fill rows would present to the replace
            // loop above as in-range / out-of-range re-scores and corrupt the overlay.
            // Not reachable today: WriteReconciledParquet builds the fdrEntries list
            // fresh from the per-file stubs on every call. No Debug.Assert here -- the
            // double-Write footprint (Features != null, ParquetIndex past the original
            // rows) is indistinguishable from a legitimate out-of-range stub, which
            // the replace loop already handles with a warning.
            nAppended = 0;
            foreach (var entry in fdrEntries)
            {
                if (entry.ParquetIndex != uint.MaxValue)
                    continue;
                entry.ParquetIndex = (uint)fullEntries.Count;
                fullEntries.Add(entry);
                nAppended++;
            }

            return nReplaced;
        }

        /// <summary>
        /// Build the reconciliation parquet metadata (mirrors Rust
        /// build_reconciled_metadata). <c>osprey.version</c> is what the next
        /// reload's CacheValidity check compares against.
        ///
        /// The reconciliation hash must be the JOIN-wide hash (over every file in
        /// the planner step), not the worker's single-file InputFiles hash;
        /// without that, a worker rescoring a single parquet stamps a single-file
        /// hash that the downstream --task SecondPassFDR merge node rejects on
        /// mismatch. The join file stems come from the planner's
        /// reconciliation.json (v2+) via <see cref="RescoreInputs.JoinFileStems"/>;
        /// fall back to the config-derived hash when the caller passed none
        /// (in-process pipeline where config.InputFiles already has all files, or
        /// v1 backward compat).
        /// </summary>
        internal static Dictionary<string, string> BuildReconciliationMetadata(
            OspreyConfig config, IReadOnlyList<string> joinFileStems)
        {
            string reconciliationHash = (joinFileStems != null && joinFileStems.Count > 0)
                ? config.Identity.ReconciliationParameterHashForStems(joinFileStems)
                : config.Identity.ReconciliationParameterHash();
            return new Dictionary<string, string>
            {
                { @"osprey.version", OspreyVersion.Current },
                { @"osprey.search_hash", config.Identity.SearchParameterHash() },
                { @"osprey.library_hash", config.Identity.LibraryIdentityHash() },
                { @"osprey.reconciled", @"true" },
                { @"osprey.reconciliation_hash", reconciliationHash },
            };
        }
    }
}
