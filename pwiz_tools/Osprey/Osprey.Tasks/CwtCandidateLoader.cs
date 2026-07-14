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
    /// parquet is missing, fails to decode, or has an out-of-range max index ABORTS the
    /// run (fail-fast) with a clear error naming the file to delete + regenerate: a
    /// corrupt Stage-4 output must stop the pipeline, never silently produce partial or
    /// feature-degraded reconciliation. Byte-identical on valid inputs.
    /// </summary>
    internal static class CwtCandidateLoader
    {
        /// <summary>
        /// Fail-fast validation -- via a footer-only metadata probe, decoding no CWT
        /// blobs and holding nothing resident -- that EVERY file's post-compaction
        /// stubs have a <see cref="FdrEntry.ParquetIndex"/> in range of its scores
        /// parquet's row count, and that the parquet is present and readable. THROWS
        /// <see cref="InvalidDataException"/> (naming every offending file) if any file
        /// is missing, unreadable, or out of range -- a corrupt Stage-4 output stops the
        /// run before Stage 6 rather than silently reconciling only the good files. A
        /// footer-clean file whose CWT blob column still fails to decode is caught during
        /// planning by <see cref="LoadOneFile"/>, which throws the same way.
        /// </summary>
        internal static void ValidateAllInRange(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths)
        {
            var invalid = new List<string>();
            foreach (var kvp in perFileEntries)
            {
                if (!perFileParquetPaths.TryGetValue(kvp.Key, out string parquetPath) ||
                    !File.Exists(parquetPath))
                {
                    invalid.Add(string.Format(@"{0} (scores parquet missing)", kvp.Key));
                    continue;
                }

                long effectiveRowCount;
                try
                {
                    var probe = ParquetScoreCache.ProbeCwtRowMetadata(parquetPath);
                    // LoadCwtCandidatesFromParquet yields an empty list (count 0) when
                    // the cwt_candidates column is absent, so a file lacking it reads as
                    // zero rows here -- and thus fails the in-range check below.
                    effectiveRowCount = probe.HasCwtCandidatesField ? probe.RowCount : 0L;
                }
                catch (Exception ex)
                {
                    invalid.Add(string.Format(@"{0} (unreadable: {1})", kvp.Key, ex.Message));
                    continue;
                }

                // The planner indexes CWT lists by entry.ParquetIndex (mirrors Rust at
                // reconciliation.rs:672). effectiveRowCount is the parquet's raw Stage-4
                // row count; kvp.Value.Count is the post-compaction stub count -- unequal
                // by design. What must hold is that every stub's ParquetIndex is in range.
                uint maxIdx = MaxParquetIndex(kvp.Value);
                if (kvp.Value.Count > 0 && maxIdx >= effectiveRowCount)
                    invalid.Add(string.Format(
                        @"{0} (max stub ParquetIndex {1} >= {2} parquet rows)",
                        kvp.Key, maxIdx, effectiveRowCount));
            }

            if (invalid.Count > 0)
                throw new InvalidDataException(string.Format(
                    @"Reconciliation planning aborted: {0} of {1} file(s) have missing or corrupt CWT " +
                    @"candidates and cannot be reconciled: [{2}]. Delete the affected .scores.parquet " +
                    @"file(s) and re-run so they are regenerated.",
                    invalid.Count, perFileEntries.Count, string.Join(@"; ", invalid)));
        }

        /// <summary>
        /// Load and convert ONE file's CWT candidate lists (indexed by
        /// <see cref="FdrEntry.ParquetIndex"/>) for the planner to consume and release
        /// before moving to the next file -- the streaming replacement for the former
        /// eager all-files load. THROWS <see cref="InvalidDataException"/> if the parquet
        /// is missing or its CWT blob column fails to decode (a corrupt Stage-4 output),
        /// so the run fails fast rather than silently reconciling with this file's peaks
        /// kept. The cheap missing / out-of-range cases are already caught up front by
        /// <see cref="ValidateAllInRange"/>; this catches a footer-clean-but-corrupt blob.
        /// </summary>
        internal static IReadOnlyList<IReadOnlyList<CwtCandidate>> LoadOneFile(
            string fileName,
            IReadOnlyDictionary<string, string> perFileParquetPaths)
        {
            if (!perFileParquetPaths.TryGetValue(fileName, out string parquetPath) ||
                !File.Exists(parquetPath))
                throw new InvalidDataException(string.Format(
                    @"Reconciliation planning aborted: scores parquet for {0} is missing. Delete any " +
                    @"partial outputs and re-run so it is regenerated.", fileName));

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
                throw new InvalidDataException(string.Format(
                    @"Reconciliation planning aborted: failed to decode CWT candidates from {0}: {1}. " +
                    @"The scores parquet is corrupt -- delete it and re-run to regenerate.",
                    parquetPath, ex.Message));
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
