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
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// Loads the per-file CWT candidate lists the Stage 6 reconciliation planner
    /// indexes by <see cref="FdrEntry.ParquetIndex"/> (mirrors Rust
    /// reconciliation.rs:672). For each file it reads the stored Stage-4 CWT rows
    /// from the parquet cache and validates that every post-compaction stub's
    /// ParquetIndex falls within the loaded row count; a file whose parquet is
    /// missing, fails to load, or has an out-of-range max index is omitted (with a
    /// warning), which the caller treats as "no reconciliation planning for this
    /// file".
    ///
    /// Extracted verbatim from <c>FirstJoinTask.PlanStage6</c> as pure code motion
    /// so the planning block reads as a sequencer; behavior is unchanged.
    /// </summary>
    internal static class CwtCandidateLoader
    {
        /// <summary>
        /// Load and bounds-validate the per-file CWT candidate lists. Returns the
        /// map keyed by file name; only files that loaded cleanly and passed the
        /// ParquetIndex bounds check are present. Logging is via
        /// <paramref name="logWarning"/> so this can run without a live context.
        /// </summary>
        internal static Dictionary<string, IReadOnlyList<IReadOnlyList<CwtCandidate>>> Load(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            Action<string> logWarning)
        {
            var perFileCwtCandidates =
                new Dictionary<string, IReadOnlyList<IReadOnlyList<CwtCandidate>>>();
            foreach (var kvp in perFileEntries)
            {
                if (perFileParquetPaths.TryGetValue(kvp.Key, out string parquetPath) &&
                    File.Exists(parquetPath))
                {
                    try
                    {
                        var cwtRows = ParquetScoreCache
                            .LoadCwtCandidatesFromParquet(parquetPath);
                        // The planner indexes CWT lists by
                        // entry.ParquetIndex (mirrors Rust at
                        // reconciliation.rs:672). cwtRows.Count is
                        // the parquet's raw Stage-4 row count;
                        // kvp.Value.Count is the post-first-pass-
                        // compaction stub count. They are not
                        // equal by design — what we actually need
                        // to validate is that every stub's
                        // ParquetIndex falls within cwtRows.
                        uint maxIdx = MaxParquetIndex(kvp.Value);
                        if (kvp.Value.Count > 0 && maxIdx >= cwtRows.Count)
                        {
                            logWarning(string.Format(
                                @"CWT candidate row count out of range for {0}: " +
                                @"max stub ParquetIndex={1}, parquet has {2} rows -- " +
                                @"skipping reconciliation planning for this file",
                                kvp.Key, maxIdx, cwtRows.Count));
                            continue;
                        }
                        var converted = new List<IReadOnlyList<CwtCandidate>>(cwtRows.Count);
                        foreach (var row in cwtRows)
                            converted.Add(row);
                        perFileCwtCandidates[kvp.Key] = converted;
                    }
                    catch (Exception ex)
                    {
                        logWarning(string.Format(
                            @"Failed to load CWT candidates for {0}: {1}",
                            kvp.Key, ex.Message));
                    }
                }
            }
            return perFileCwtCandidates;
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
