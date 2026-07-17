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
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Writes the reconciled per-file <c>.scores-reconciled.parquet</c> for
    /// Stage 6 by STREAMING the original Stage 4 parquet group-by-group: each
    /// original row group is read, the re-scored rows whose original
    /// <see cref="FdrEntry.ParquetIndex"/> falls in that group are overlaid, and the
    /// group is written straight through to a SEPARATE reconciled sibling (the
    /// original is never overwritten); gap-fill rows are merged into their canonical
    /// (entry_id, charge, scan_number) sorted position. Peak residency is one original
    /// row group rather than the whole file's <see cref="FdrEntry"/> list -- see
    /// <see cref="ParquetScoreCache.StreamReconciledScoresParquet"/>.
    ///
    /// The overlay/gap-fill split (<see cref="BuildOverlay"/>) and metadata-hash
    /// selection (<see cref="BuildReconciliationMetadata"/>) are pure and unit-tested
    /// here; the streaming transfer itself is covered by the IO round-trip test.
    /// Logging is taken as <see cref="Action{T}"/> callbacks so no live
    /// <see cref="PipelineContext"/> is needed. The streaming merge reproduces the exact
    /// physical row order of the former load-all + re-sort write (gap-fill interleaved by
    /// scan, not appended at the end), which Pass 2's projection sort relies on. Mirrors
    /// Rust pipeline.rs:3050-3110.
    /// </summary>
    internal static class ReconciledParquetWriter
    {
        /// <summary>
        /// Stream <paramref name="originalPath"/> group-by-group, overlaying the
        /// re-scored + gap-fill rows from <paramref name="fdrEntries"/>, and write the
        /// result to <paramref name="reconciledPath"/>. Returns true when the reconciled
        /// parquet was written; false on a read/write failure (so the caller does not
        /// stamp a validity sidecar over a stale or absent output).
        /// </summary>
        internal static bool Write(string originalPath, string reconciledPath,
            List<FdrEntry> fdrEntries,
            string fileName, List<LibraryEntry> fullLibrary, OspreyConfig config,
            IReadOnlyList<string> joinFileStems,
            Action<string> logInfo, Action<string> logWarning)
        {
            // 1. Split the re-scored entries into the small resident overlay map
            //    (keyed by original ParquetIndex) + the gap-fill list. No whole-file
            //    materialization -- these hold references into the per-file fdrEntries.
            var overlayByIndex = new Dictionary<uint, FdrEntry>();
            var gapFill = new List<FdrEntry>();
            BuildOverlay(fdrEntries, overlayByIndex, gapFill);

            // 2. libraryById for the sequence / precursor_mz / protein_ids columns.
            var libraryById = new Dictionary<uint, LibraryEntry>(fullLibrary.Count);
            foreach (var libEntry in fullLibrary)
                libraryById[libEntry.Id] = libEntry;

            // 3. Reconciliation metadata (mirrors Rust build_reconciled_metadata).
            var metadata = BuildReconciliationMetadata(config, joinFileStems);

            // 4. Stream the reconciled transfer group-by-group: read the original,
            //    overlay re-scored rows, merge gap-fill into canonical position, write
            //    the sibling. Peak residency is one original row group, not the whole file.
            int nReplaced, nAppended, origRowCount;
            try
            {
                var result = ParquetScoreCache.StreamReconciledScoresParquet(
                    originalPath, reconciledPath, overlayByIndex, gapFill,
                    metadata, libraryById, fileName, logWarning);
                nReplaced = result.NReplaced;
                nAppended = result.NAppended;
                origRowCount = result.OrigRowCount;
            }
            catch (Exception ex)
            {
                logWarning(string.Format(
                    "Stage 6 write-back: failed to transfer {0} -> {1}: {2}",
                    originalPath, reconciledPath, ex.Message));
                return false;
            }

            logInfo(string.Format(
                "  Wrote reconciled parquet for {0}: {1} rows ({2} replaced + {3} appended; original {4} rows)",
                fileName, origRowCount + nAppended, nReplaced, nAppended, origRowCount));
            return true;
        }

        /// <summary>
        /// Split the re-scored entries in <paramref name="fdrEntries"/> into the resident
        /// overlay map <paramref name="overlayByIndex"/> (keyed by original
        /// <see cref="FdrEntry.ParquetIndex"/>) and the <paramref name="gapFill"/> list
        /// that <see cref="ParquetScoreCache.StreamReconciledScoresParquet"/> consumes --
        /// the streaming replacement for the former load-all + in-place overlay (no
        /// whole-file <see cref="FdrEntry"/> list is ever built).
        ///
        /// Replacement is keyed on <see cref="FdrEntry.ParquetIndex"/> (NOT post-compaction
        /// Vec position; the two diverge after first-pass FDR drops non-passing entries).
        /// Re-scored rows are detected by <see cref="FdrEntry.Features"/> != null:
        /// hydration's LoadFdrStubsFromParquet does NOT populate Features, so an unchanged
        /// post-compaction stub (Features == null) is skipped, leaving its original parquet
        /// row (Features + CwtCandidates + the binary blob columns) to stream through
        /// untouched. A row with <see cref="FdrEntry.ParquetIndex"/> == uint.MaxValue is a
        /// gap-fill stub (absent from the original parquet) and is appended; every other
        /// re-scored row overlays the original row at its ParquetIndex (last write wins,
        /// matching the former fullEntries[pqIdx] = entry). Out-of-range indices are
        /// reported by the streaming write, not here.
        /// </summary>
        internal static void BuildOverlay(List<FdrEntry> fdrEntries,
            Dictionary<uint, FdrEntry> overlayByIndex, List<FdrEntry> gapFill)
        {
            foreach (var entry in fdrEntries)
            {
                if (entry.ParquetIndex == uint.MaxValue)
                {
                    gapFill.Add(entry);
                    continue;
                }
                if (entry.Features == null)
                    continue;  // hydrated stub, never re-scored
                overlayByIndex[entry.ParquetIndex] = entry;
            }
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
