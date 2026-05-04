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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp
{
    /// <summary>
    /// Top-level entry point for the <c>--join-at-pass=1 --no-join</c>
    /// per-file rescore worker. Mirrors <c>run_rescore</c> in
    /// <c>osprey/crates/osprey/src/rescore.rs</c>.
    ///
    /// Today this entry point handles the foundation pieces only:
    /// hydration of the Stage 5 → Stage 6 boundary files
    /// (<see cref="RescoreHydration.HydrateForRescore"/>) followed by
    /// worker compaction (<see cref="RescoreCompaction.Apply"/>). The
    /// per-file rescore engine itself (boundary-overrides search +
    /// gap-fill two-pass + reconciled parquet write-back) is not yet
    /// ported; the in-process pipeline at
    /// <c>AnalysisPipeline.Run</c> also stubs it out with the same
    /// "Stage 6 per-file rescore: not yet implemented" log line. Both
    /// sides will lift together once the C# rescore engine port
    /// lands.
    ///
    /// What this entry point does deliver today:
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
    ///     Reproduces the in-process compaction predicate
    ///     (peptide-FDR OR protein-rescue) so the post-hydrate
    ///     in-memory state matches what the in-process pipeline holds
    ///     at the same seam.
    ///   </item>
    ///   <item>
    ///     Logs the hydrated / compacted counts so a future cross-impl
    ///     bit-parity diagnostic dump can verify both sides reach
    ///     identical state at the Stage 5 → Stage 6 seam.
    ///   </item>
    /// </list>
    ///
    /// Returns a non-zero exit code with a clear message until the
    /// rescore engine lands; do NOT swallow this as success.
    /// </summary>
    public static class RescoreWorker
    {
        /// <summary>
        /// Run the per-file rescore worker on the boundary files
        /// referenced by <see cref="OspreyConfig.InputScores"/>.
        ///
        /// Returns 0 on full success (foundation runs cleanly + rescore
        /// engine completes) or non-zero with an explanatory log line
        /// on any failure. Today's stub returns a non-zero exit code
        /// after hydration + compaction succeed because the rescore
        /// engine isn't ported yet.
        /// </summary>
        public static int Run(OspreyConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.InputScores == null || config.InputScores.Count == 0)
            {
                Program.LogError(
                    "--join-at-pass=1 --no-join requires --input-scores <path...>.");
                return 1;
            }

            Program.LogInfo(string.Format(
                "--join-at-pass=1 --no-join: per-file rescore worker starting on {0} parquet(s)",
                config.InputScores.Count));

            RescoreInputs inputs;
            try
            {
                inputs = RescoreHydration.HydrateForRescore(config.InputScores);
            }
            catch (Exception ex)
            {
                Program.LogError(string.Format(
                    "--join-at-pass=1 --no-join: hydration failed: {0}", ex.Message));
                return 1;
            }
            Program.LogInfo(string.Format(
                "Hydrated {0} file(s); {1} pre-compaction stubs, {2} reconciliation actions, " +
                "{3} gap-fill candidates, {4} refined RT calibration(s)",
                inputs.PerFileEntries.Count,
                inputs.TotalStubs,
                inputs.TotalActions,
                inputs.TotalGapFillTargets,
                inputs.RefinedCalibrations.Count));

            // Cross-impl bisection seam: dump the per-precursor q-values
            // that were just hydrated from .1st-pass.fdr_scores.bin so
            // the result can be diffed against Rust's
            // rust_stage5_percolator.tsv via Compare-Percolator.ps1.
            // If the diff is non-empty, the divergence is in hydration
            // (sidecar parsing); if empty, divergence (if any) is
            // downstream of this seam (compaction or rescore).
            if (OspreyDiagnostics.DumpPercolator)
                OspreyDiagnostics.WriteStage5PercolatorDump(inputs.PerFileEntries);

            RescoreCompaction.Stats stats;
            try
            {
                stats = RescoreCompaction.Apply(inputs, config);
            }
            catch (Exception ex)
            {
                Program.LogError(string.Format(
                    "--join-at-pass=1 --no-join: compaction failed: {0}", ex.Message));
                return 1;
            }
            Program.LogInfo(string.Format(
                "Worker compaction: {0} -> {1} entries ({2} surviving base_ids), " +
                "{3} reconciliation actions retained ({4} dropped)",
                stats.EntriesBefore,
                stats.EntriesAfter,
                stats.FirstPassBaseIds,
                inputs.ReconciliationActions.Count,
                stats.DroppedActions));

            // Foundation is in place; the rescore engine itself
            // (boundary-overrides search + gap-fill two-pass + reconciled
            // parquet write-back) is the next porting effort. Until it
            // lands, the worker can't produce reconciled .scores.parquet
            // output, so return non-zero with a clear pointer rather
            // than letting a downstream --join-at-pass=2 invocation
            // silently consume stale Stage 4 parquets.
            Program.LogError(
                "--join-at-pass=1 --no-join: hydration + compaction completed cleanly, but " +
                "the per-file rescore engine (boundary-overrides search + gap-fill + reconciled " +
                "parquet write-back) is not yet ported to OspreySharp. The in-process pipeline " +
                "stubs this out with the same message. Both sides will lift together once the " +
                "C# rescore engine port lands.");
            return 2;
        }
    }
}
