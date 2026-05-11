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

using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp
{
    /// <summary>
    /// Top-level entry point for the <c>--join-at-pass=1 --no-join</c>
    /// per-file rescore worker. Mirrors <c>run_rescore</c> in
    /// <c>osprey/crates/osprey/src/rescore.rs</c>.
    ///
    /// End-to-end behavior (cross-impl byte-parity validated on
    /// Stellar + Astral as of 2026-05-06):
    /// <list type="bullet">
    ///   <item>
    ///     Validates the CLI shape (<c>--input-scores</c>,
    ///     <c>--library</c>, <c>--output</c> required;
    ///     <c>--input</c> forbidden) — handled by
    ///     <c>Program.ValidateArgs</c>.
    ///   </item>
    ///   <item>
    ///     Loads each <c>&lt;stem&gt;.scores.parquet</c> into
    ///     <see cref="Core.FdrEntry"/> stubs and overlays the
    ///     <c>&lt;stem&gt;.1st-pass.fdr_scores.bin</c> sidecar (v3),
    ///     parses the sibling <c>&lt;stem&gt;.reconciliation.json</c>
    ///     into per-file actions + refined RT calibration + gap-fill
    ///     targets.
    ///   </item>
    ///   <item>
    ///     Reproduces the in-process compaction predicate (peptide-FDR
    ///     OR protein-rescue) so the post-hydrate in-memory state
    ///     matches what the in-process pipeline holds at the same seam.
    ///   </item>
    ///   <item>
    ///     Runs the per-file rescore engine: consensus + reconciliation
    ///     overlay (Phase 1), gap-fill two-pass with prefilter-off CWT
    ///     and forced-integration fallback (Phase 2), and reconciled
    ///     parquet write-back with `osprey.reconciled` /
    ///     `osprey.reconciliation_hash` footer metadata (Phase 3).
    ///   </item>
    /// </list>
    ///
    /// Returns 0 on full success, non-zero with an explanatory log
    /// line on any failure (library load, hydration, compaction, or
    /// rescore loop). Six per-row blob columns (<c>fragment_mzs</c>,
    /// <c>fragment_intensities</c>, <c>reference_xic_rts</c>,
    /// <c>reference_xic_intensities</c>, <c>bounds_area</c>,
    /// <c>bounds_snr</c>) are written as null/zero today — tracked as
    /// follow-up against
    /// <c>ai/todos/backlog/brendanx67/TODO-ospreysharp_missing_scoring_columns.md</c>.
    /// </summary>
    public static class RescoreWorker
    {
        /// <summary>
        /// Run the per-file rescore worker on the boundary files
        /// referenced by <see cref="OspreyConfig.InputScores"/>.
        /// Returns 0 on success, non-zero on failure.
        /// </summary>
        public static int Run(OspreyConfig config)
        {
            // Thin facade -- worker logic lives on PerFileRescoreTask
            // so it can share the in-process Stage 6 code path through
            // the same AbstractScoringTask base. Keeps Program.Main's
            // dispatch unchanged while letting the heavy lifting
            // (library load, hydration, compaction, rescore loop,
            // gap-fill, parquet write-back) live alongside the
            // in-process rescore task.
            var task = new PerFileRescore.PerFileRescoreTask();
            return task.RunWorker(config);
        }
    }
}
