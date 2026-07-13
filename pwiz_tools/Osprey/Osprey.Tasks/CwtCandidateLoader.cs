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
using System.IO;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Streams the per-file CWT candidate lists the Stage 6 reconciliation planner
    /// indexes by <see cref="FdrEntry.ParquetIndex"/> (mirrors Rust
    /// reconciliation.rs:672). <see cref="ValidateAllInRange"/> confirms up front --
    /// via a footer-only metadata probe, nothing held resident -- that every
    /// post-compaction stub's ParquetIndex is in range of its file's parquet row
    /// count (the reconciliation all-or-nothing gate); the planner then pulls each
    /// file's candidates on demand through <see cref="LoadOneFile"/> and releases
    /// them before the next file. This replaces the former eager <c>Load</c> that
    /// decoded and held EVERY file's candidate lists at once -- the all-runs buffer
    /// that OOM'd the 82-file Stage-6 planning phase on a 64 GB box. A file whose
    /// parquet is missing, fails to load, or has an out-of-range max index fails the
    /// gate (with a warning), which the caller treats as "no reconciliation
    /// planning" -- the same decision the eager loader produced, byte-identical.
    /// </summary>
    internal static class CwtCandidateLoader
    {
        /// <summary>
        /// Validate -- via a footer-only metadata probe, decoding no CWT blobs and
        /// holding nothing resident -- that EVERY file's post-compaction stubs have
        /// a <see cref="FdrEntry.ParquetIndex"/> in range of its scores parquet's
        /// row count. This is the reconciliation all-or-nothing gate the caller
        /// reads (reconciliation runs only when every file passes); returns true
        /// only then, and reports the passing count via <paramref name="validFileCount"/>
        /// for the "loaded for X/Y files" log on a partial failure. The valid/invalid
        /// decision and the two warning messages mirror the former eager loader
        /// exactly, so the gate is byte-identical -- only the residency changes.
        /// </summary>
        internal static bool ValidateAllInRange(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            Action<string> logWarning,
            out int validFileCount)
        {
            validFileCount = 0;
            foreach (var kvp in perFileEntries)
            {
                // Missing path / file: the former eager loader silently omitted the
                // file (no warning), which failed the all-files gate. Keep that.
                if (!perFileParquetPaths.TryGetValue(kvp.Key, out string parquetPath) ||
                    !File.Exists(parquetPath))
                    continue;

                long effectiveRowCount;
                try
                {
                    var probe = ParquetScoreCache.ProbeCwtRowMetadata(parquetPath);
                    // LoadCwtCandidatesFromParquet yields an empty list (count 0)
                    // when the cwt_candidates column is absent, so a file lacking it
                    // reads as zero rows here -- matching the former bounds check.
                    effectiveRowCount = probe.HasCwtCandidatesField ? probe.RowCount : 0L;
                }
                catch (Exception ex)
                {
                    logWarning(string.Format(
                        @"Failed to load CWT candidates for {0}: {1}",
                        kvp.Key, ex.Message));
                    continue;
                }

                // The planner indexes CWT lists by entry.ParquetIndex (mirrors Rust
                // at reconciliation.rs:672). effectiveRowCount is the parquet's raw
                // Stage-4 row count; kvp.Value.Count is the post-first-pass-compaction
                // stub count. They are not equal by design -- what we validate is that
                // every stub's ParquetIndex falls within the parquet's rows.
                uint maxIdx = MaxParquetIndex(kvp.Value);
                if (kvp.Value.Count > 0 && maxIdx >= effectiveRowCount)
                {
                    logWarning(string.Format(
                        @"CWT candidate row count out of range for {0}: " +
                        @"max stub ParquetIndex={1}, parquet has {2} rows -- " +
                        @"skipping reconciliation planning for this file",
                        kvp.Key, maxIdx, effectiveRowCount));
                    continue;
                }
                validFileCount++;
            }
            return validFileCount == perFileEntries.Count;
        }

        /// <summary>
        /// Load and convert ONE file's CWT candidate lists (indexed by
        /// <see cref="FdrEntry.ParquetIndex"/>) for the planner to consume and
        /// release before moving to the next file -- the streaming replacement for
        /// the former eager all-files load. Returns an empty list when the parquet
        /// path is unknown, missing, or fails to decode; the planner then falls back
        /// to no candidates for that file (its entries keep their current peak), the
        /// same fallback its per-entry bounds guard already applies. The all-files
        /// in-range gate is enforced up front by <see cref="ValidateAllInRange"/>,
        /// so on the planning path this is the happy-path loader.
        /// </summary>
        internal static IReadOnlyList<IReadOnlyList<CwtCandidate>> LoadOneFile(
            string fileName,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            Action<string> logWarning)
        {
            if (!perFileParquetPaths.TryGetValue(fileName, out string parquetPath) ||
                !File.Exists(parquetPath))
                return Array.Empty<IReadOnlyList<CwtCandidate>>();

            try
            {
                var cwtRows = ParquetScoreCache.LoadCwtCandidatesFromParquet(parquetPath);
                var converted = new List<IReadOnlyList<CwtCandidate>>(cwtRows.Count);
                foreach (var row in cwtRows)
                    converted.Add(row);
                return converted;
            }
            catch (Exception ex)
            {
                logWarning(string.Format(
                    @"Failed to load CWT candidates for {0}: {1}",
                    fileName, ex.Message));
                return Array.Empty<IReadOnlyList<CwtCandidate>>();
            }
        }

        /// <summary>
        /// Largest <see cref="FdrEntry.ParquetIndex"/> across the stubs (0 when the
        /// list is empty). The caller compares this against the loaded CWT row
        /// count to decide whether the stubs' indices are in range. Pure: no I/O.
        /// </summary>
        internal static uint MaxParquetIndex(IReadOnlyList<FdrEntry> entries)
        {
            uint maxIdx = 0;
            foreach (var entry in entries)
            {
                if (entry.ParquetIndex > maxIdx)
                    maxIdx = entry.ParquetIndex;
            }
            return maxIdx;
        }
    }
}
