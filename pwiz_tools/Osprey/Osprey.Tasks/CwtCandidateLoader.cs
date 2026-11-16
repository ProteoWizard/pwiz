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
    /// reconciliation.rs:672). <see cref="ValidateFileInRange"/> confirms up front --
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
        /// Fail-fast validation via a footer-only metadata probe, decoding no CWT blobs and
        /// holding nothing resident: probe ONE file and append a
        /// description to <paramref name="invalid"/> if it is missing, unreadable, or has a
        /// stub whose <see cref="FdrEntry.ParquetIndex"/> is out of range. Nothing is thrown
        /// here - the caller collects every offending file first and then calls
        /// <see cref="ThrowIfAnyInvalid"/>, so one corrupt parquet does not hide the others.
        ///
        /// <para>Split out so a caller that already walks the files one at a time can validate
        /// as it goes, instead of needing every file's stubs resident to validate them
        /// together.</para>
        /// </summary>
        internal static void ValidateFileInRange(
            string fileName,
            IReadOnlyList<FdrEntry> entries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            List<string> invalid)
        {
            if (!perFileParquetPaths.TryGetValue(fileName, out string parquetPath) ||
                !File.Exists(parquetPath))
            {
                invalid.Add(string.Format(OspreyTasksResources.CwtCandidateLoader_ValidateFileInRange__0____scores_parquet_file_missing_, fileName,
                    ParquetScoreCache.EXT_SCORES));
                return;
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
                invalid.Add(string.Format(OspreyTasksResources.CwtCandidateLoader_ValidateFileInRange__0___unreadable___1__, fileName, ex.Message));
                return;
            }

            // The planner indexes CWT lists by entry.ParquetIndex (mirrors Rust at
            // reconciliation.rs:672). effectiveRowCount is the parquet's raw Stage-4
            // row count; the stub count is the post-compaction one -- unequal by design.
            // What must hold is that every stub's ParquetIndex is in range.
            uint maxIdx = MaxParquetIndex(entries);
            if (entries.Count > 0 && maxIdx >= effectiveRowCount)
            {
                invalid.Add(string.Format(
                    OspreyTasksResources.CwtCandidateLoader_ValidateFileInRange__0___a_CWT_candidate_refers_to_row__1___but_the__scores_parquet_file_has_only__2__rows_,
                    fileName, maxIdx, effectiveRowCount, ParquetScoreCache.EXT_SCORES));
            }
        }

        /// <summary>
        /// Throw naming every file <see cref="ValidateFileInRange"/> rejected, or return when
        /// none was. A corrupt Stage-4 output stops the run before Stage 6 rather than
        /// silently reconciling only the good files.
        /// </summary>
        internal static void ThrowIfAnyInvalid(List<string> invalid, int fileCount)
        {
            if (invalid.Count == 0)
                return;
            throw new InvalidDataException(string.Format(
                OspreyTasksResources.CwtCandidateLoader_ThrowIfAnyInvalid_Reconciliation_planning_stopped__CWT_candidates_are_missing_or_damaged_in__0__of__1__,
                invalid.Count, fileCount, string.Join(@"; ", invalid), ParquetScoreCache.EXT_SCORES));
        }

        /// <summary>
        /// Load and convert ONE file's CWT candidate lists (indexed by
        /// <see cref="FdrEntry.ParquetIndex"/>) for the planner to consume and release
        /// before moving to the next file -- the streaming replacement for the former
        /// eager all-files load. THROWS <see cref="InvalidDataException"/> if the parquet
        /// is missing or its CWT blob column fails to decode (a corrupt Stage-4 output),
        /// so the run fails fast rather than silently reconciling with this file's peaks
        /// kept. The cheap missing / out-of-range cases are already caught up front by
        /// <see cref="ValidateFileInRange"/>; this catches a footer-clean-but-corrupt blob.
        /// </summary>
        internal static IReadOnlyList<IReadOnlyList<CwtCandidate>> LoadOneFile(
            string fileName,
            IReadOnlyDictionary<string, string> perFileParquetPaths)
        {
            if (!perFileParquetPaths.TryGetValue(fileName, out string parquetPath) ||
                !File.Exists(parquetPath))
            {
                throw new InvalidDataException(string.Format(
                    OspreyTasksResources.CwtCandidateLoader_LoadOneFile_Reconciliation_planning_stopped__the__scores_parquet_file_for__0__is_missing__Delete_any_, fileName,
                    ParquetScoreCache.EXT_SCORES));
            }

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
                    OspreyTasksResources.CwtCandidateLoader_LoadOneFile_Reconciliation_planning_stopped__the_CWT_candidates_in__0__could_not_be_read___1___The__,
                    parquetPath, ex.Message, ParquetScoreCache.EXT_SCORES));
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
                if (entry.ParquetIndex.HasValue && entry.ParquetIndex.Value > maxIdx)
                    maxIdx = entry.ParquetIndex.Value;
            }
            return maxIdx;
        }
    }
}
