/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Aggregate counts returned by <see cref="PerFileRescoreTask.ExecuteRescore"/>.
    /// Mirrors <c>RescoreStats</c> in
    /// <c>osprey/crates/osprey/src/pipeline.rs</c>.
    /// </summary>
    public class RescoreStats
    {
        /// <summary>
        /// Total entries re-scored across all files: existing
        /// (consensus + reconciliation) plus gap-fill (CWT + forced).
        /// </summary>
        public int TotalRescored { get; set; }

        /// <summary>
        /// Number of non-Keep reconciliation actions executed across all files.
        /// </summary>
        public int TotalReconciliation { get; set; }

        /// <summary>
        /// Gap-fill targets that landed via the CWT-detected pass.
        /// Phase 2 of the port; zero today.
        /// </summary>
        public int TotalGapCwt { get; set; }

        /// <summary>
        /// Gap-fill targets that landed via the forced-integration pass.
        /// Phase 2 of the port; zero today.
        /// </summary>
        public int TotalGapForced { get; set; }
    }

    /// <summary>
    /// Stage 6 per-file rescore phase: re-scores each input file's
    /// previously-scored entries against the consensus + reconciliation
    /// boundaries produced by the FirstPassFDR phase, runs the gap-fill
    /// two-pass for missing precursors, and writes the reconciled
    /// results back into the per-file <c>.scores.parquet</c>. The HPC
    /// "second per-file fan-out" boundary in the
    /// <c>Osprey-workflow.html</c> view -- each input file's rescore is
    /// independent of the others.
    ///
    /// Single entry point: <see cref="Run"/> is invoked by
    /// <c>AnalysisPipeline</c>'s task driver during both
    /// straight-through pipeline runs and the stage6 worker mode
    /// (<c>--task PerFileRescoring</c>). The worker
    /// mode previously had a separate <c>RunWorker</c> entry that
    /// hand-assembled the upstream hydration; Phase C collapsed that
    /// path so the canonical pipeline's IsIncluded membership + the
    /// upstream tasks' lazy-rehydrate handle it. Run reads upstream state
    /// as typed byproducts through <c>ctx.Get&lt;CompactedEntries&gt;()</c>,
    /// <c>ctx.Get&lt;ReconciliationActions&gt;()</c>, etc. (a cache miss
    /// materializes the producing task), dispatches into
    /// <see cref="ExecuteRescore"/>, then runs the per-process
    /// diagnostic-writer close + cross-impl bisection dump. Reaches the
    /// scoring engine (RunCoelutionScoring via <see cref="ScoringPipeline"/>,
    /// ExtractIsolationWindows via <see cref="ScoringTaskShared"/>) directly
    /// rather than through a base class.
    /// </summary>
    internal sealed class PerFileRescoreTask : OspreyTask
    {
        // Equal-weight progress segments one file's Stage 6 rescore is divided into
        // for the --parallel-files "[i] p%" aggregate line: reload spectra, re-score
        // the subset, write the reconciled parquet. RescoreOneFile advances them via
        // MultiProgressReporter.Current?.BeginSegment(); off the parallel path those
        // are no-ops.
        private const int RESCORE_FILE_SEGMENTS = 3;

        // Captured during Run so SecondPassFdrTask (downstream) can reach
        // the post-rescore version. Per the ownership-transfer semantics
        // of the pipeline: this task is the producer of the post-rescore
        // perFileEntries; consumers query us rather than
        // PerFileScoringTask. When Run is a no-op (no planning state)
        // the list reference falls through unchanged from
        // PerFileScoringTask.
        private List<KeyValuePair<string, List<FdrEntry>>> _perFileEntries;

        // Admits one survivor refill at a time under the parallel file loop, so the
        // pre-compaction transient each load holds is not multiplied by the file parallelism.
        // See RescoreOneFileStreamed for why that trade is free.
        private readonly object _survivorLoadLock = new object();

        public override string Name => @"PerFileRescoring";

        /// <summary>
        /// Computes the Stage 6 rescore in straight-through, the rescore worker
        /// (--task PerFileRescoring), and the --input-scores
        /// full-pipeline. Excluded in --task PerFileScoring, --task FirstPassFDR (stops at Stage 5),
        /// and the --task SecondPassFDR run (where it rehydrates rather than
        /// re-scoring, SecondPassFDR having no mzMLs).
        /// </summary>
        public override bool IsIncluded(PipelineContext ctx)
        {
            var c = ctx.Config;
            bool inputs = c.InputScores != null && c.InputScores.Count > 0;
            return (!inputs && !c.NoJoin)
                || (inputs && c.NoJoin)
                || (inputs && !c.NoJoin && !c.StopAfterStage5 && !c.ExpectReconciledInput);
        }

        // The final milestone of the shared mutable entry buffer: this task
        // overlays the Stage 6 rescore (or, in the --task SecondPassFDR reconciled-input path,
        // applies its own compaction) onto the same backing list. SecondPassFDR
        // pulls RescoredEntries, so a cache miss lazily materializes this task --
        // which is exactly what triggers the rescore/compaction in reconciled-input mode
        // where the driver does not run this task.
        public override IEnumerable<Type> Publishes => new[] { typeof(RescoredEntries) };

        // _perFileEntries is the shared buffer this task overlays in place; it is
        // published as the RescoredEntries milestone (in Run and the SecondPassFDR-mode
        // Rehydrate) for SecondPassFDR to pull via ctx.Get<RescoredEntries>().

        // Phase B resume surface. The reconciled parquet is written to a
        // SEPARATE <stem>.scores-reconciled.parquet sibling, leaving the
        // upstream PerFileScoringTask <stem>.scores.parquet intact (so a
        // partial Stage 6 crash can no longer half-rewrite the Stage 4
        // output, and downstream readers can fall back to the original
        // for files that had no reconciliation work). ValidityKey adds the
        // reconciliation parameter hash because the rescored content
        // depends on it.
        public override IEnumerable<string> Inputs(PipelineContext ctx)
        {
            if (ctx.Config.InputFiles == null) yield break;
            foreach (var input in ctx.Config.InputFiles)
            {
                yield return FdrScoresSidecar.Pass1Path(input);
                if (ctx.Config.Reconciliation != null && ctx.Config.Reconciliation.Enabled)
                    yield return ReconciliationFile.PathForInput(input);
            }
        }

        public override IEnumerable<string> Outputs(PipelineContext ctx)
        {
            if (ctx.Config.InputFiles == null) yield break;
            // Declares a reconciled path per input, but ExecuteRescore skips
            // files with no consensus/reconciliation/gap-fill work, so those get
            // no reconciled output. When any no-work file is present the driver's
            // task-level IsTaskAlreadyDone (which requires EVERY declared output
            // to exist) therefore can't short-circuit the whole task on resume --
            // it re-enters Run, which fast per-file-skips already-rescored files
            // via their reconciled sidecars. Correctness is unaffected; this is a
            // deliberate, inert coarse-skip. (We don't filter to work-files here
            // because that set isn't known until the Stage 6 planner has run.)
            foreach (var input in ctx.Config.InputFiles)
                yield return ParquetScoreCache.GetReconciledScoresPath(input);
        }

        public override string ValidityKey(PipelineContext ctx)
        {
            // Stage 6 rescore + reconciliation gate on the experiment precursor q-value, which
            // the experiment-wide aggregation changes, so the reconciled parquets this task
            // writes differ across arms and must be invalidated alongside FirstPassFdrTask's Stage-5
            // outputs. Same shared suffix, so the two cannot disagree.
            // Same shared suffix for the 2nd-pass mode, for the same reason: this task writes the
            // per-file 2nd-pass sidecar, whose q-values ARE the mode's output.
            // The Stage 6 handoff arm joins them: the streamed and resident arms are supposed
            // to write byte-identical reconciled parquets, and an in-place A/B that silently
            // adopted the other arm's outputs would report that identity without testing it.
            // The sidecar format version belongs here too, not only in FirstPassFdrTask: this
            // task WRITES the 2nd-pass sidecar, so a record-layout change (v3 -> v4) invalidates
            // its output exactly as it invalidates the 1st-pass one. Without it, FirstPassFDR
            // re-ran and rewrote v4 while this task and SecondPassFDR considered themselves
            // valid against v3 files.
            return base.ValidityKey(ctx)
                + @";fdrsidecar=" + FdrScoresSidecar.FormatVersion
                + @";reconciliation=" + ctx.Config.Identity.ReconciliationParameterHash()
                + OspreyEnvironment.ExperimentAggValidityKeySuffix()
                + OspreyEnvironment.Pass2QValueValidityKeySuffix()
                + OspreyEnvironment.Stage6StreamSurvivorsValidityKeySuffix()
                + LibraryFragmentRelease.ValidityKeySuffix(ctx);
        }

        public override bool Run(PipelineContext ctx)
        {
            // Compute path (Stage 6 rescore): re-score each file's entries
            // against the consensus + reconciliation boundaries and write the
            // reconciled parquets. Used by the straight-through pipeline and
            // the stage6 rescore worker. The --task SecondPassFDR node,
            // which has only reconciled parquets + sidecars (no mzMLs to
            // rescore from), takes Rehydrate instead: the driver reaches this
            // task here only in the rescore-capable modes, and a SecondPassFDR
            // consumer materializes it via ctx.Demand, which routes to Rehydrate.
            // CompactedEntries: the post-FirstPassFDR buffer. Demanding it
            // materializes FirstPassFDR (running its compaction + Stage 6 planning
            // when the driver skipped it in worker-rescore mode), which is also
            // what makes the planning byproducts read by ExecuteRescore below
            // available -- one Get expresses the whole dependency.
            _perFileEntries = ctx.Get<CompactedEntries>().Value;

            // Publish the RescoredEntries milestone over the shared backing list
            // now, while we hold its reference. ExecuteRescore (below) overlays
            // it in place, and the self-gate may leave it unchanged; either way a
            // consumer reading RescoredEntries.Value later sees the final buffer
            // (milestone token over a shared store -- see PipelineByproducts.cs).
            ctx.Publish(new RescoredEntries(_perFileEntries));

            // Self-gate: rescore + reconciliation only run when there is
            // planning state to act on AND the rescore hasn't already been
            // done upstream. State comes from either FirstPassFdrTask's
            // planning block (in-process pipeline, DidPlan=true) or
            // PerFileScoringTask's probe-the-disk bundle (collapsed worker
            // path, DidPlan=false but bundle != null). A 2nd-pass FDR
            // sidecar already on disk for any file is the signal that the
            // rescore engine has already produced the reconciled output;
            // re-running it would re-apply reconciliation actions on top
            // of already-reconciled values, so this branch falls back to
            // the no-op alongside the no-state case. Probe-the-disk on
            // 2nd-pass sidecar presence replaces the prior
            // ExpectReconciledInput gate (Phase C: mechanism-driven, not
            // flag-driven) for the worker self-gate cases below;
            // ExpectReconciledInput keeps the hard short-circuit above for
            // the strict --task SecondPassFDR reconciled-input path. Downstream SecondPassFdrTask
            // reads the RescoredEntries milestone of this same backing list.
            // Read the planning gate from the typed byproduct registry rather
            // than reaching for the concrete FirstPassFdrTask. ctx.Get lazily
            // materializes the slot's producer (FirstPassFdrTask) if it has not run
            // yet, so the value is always populated; FirstPassFDR publishes
            // PlanningPerformed alongside CompactedEntries (already read above)
            // from every materialization path.
            bool didPlan = ctx.Get<PlanningPerformed>().Value;
            var rescoreBundle = ctx.Get<RescoreBundle>().Value;
            bool anyPass2Present = false;
            if (ctx.Config.InputFiles != null)
            {
                foreach (var inputFile in ctx.Config.InputFiles)
                {
                    // Presence is not readability. A bare File.Exists cannot see a version, so a
                    // sidecar left by a build before the v3 -> v4 record change satisfied this
                    // gate and made the WHOLE Stage 6 rescore a no-op - the run then finished
                    // green carrying 1st-pass q-values into the picked-protein FDR and the .blib.
                    if (FdrScoresSidecar.IsCurrentFormat(FdrScoresSidecar.Pass2Path(inputFile),
                                                         FdrScoresSidecar.Pass.SecondPass))
                    {
                        anyPass2Present = true;
                        break;
                    }
                }
            }
            // Non-null only when FirstPassFDR released the survivor contents after planning
            // (issue #4526). Every exit from here on has to leave the RescoredEntries
            // milestone holding a full buffer, because that is what SecondPassFDR reads -
            // so each return below refills it first.
            var survivorLoader = StreamedSurvivorLoader(ctx);

            if (!didPlan && (rescoreBundle == null || anyPass2Present))
            {
                // No rescore to run. The RESIDENT arm does nothing at all here - it leaves the
                // buffer exactly as Stage 5 compacted it, and SecondPassFDR reloads the rescored
                // features from the valid reconciled parquets on disk. So the streamed arm has
                // to do exactly one thing: put back the contents FirstPassFDR released. Overlaying
                // the reconciled parquets as well (as this did) applied Stage-6 boundaries the
                // resident arm never applies, which made OSPREY_STAGE6_STREAM_SURVIVORS=0
                // something other than a byte-identity oracle on this path - the one property
                // the whole design rests on.
                if (survivorLoader != null && !MaterializeAllSurvivors(survivorLoader, ctx))
                    return false;
                return true;
            }

            // Per-file sidecar lifecycle (delete-before / write-after) is
            // handled inside ExecuteRescore's loop so a per-file skip can
            // preserve the valid sidecars for already-rescored files and
            // only invalidate the file(s) about to be re-rescored.

            // Join file stems for the reconciled parquet metadata hash.
            // In the in-process pipeline _perFileEntries has every file in
            // the run; in worker mode (--task PerFileRescoring) it has
            // a single file and the planner's full set comes from
            // RescoreInputs.JoinFileStems (read from reconciliation.json
            // v2+). Pass _perFileEntries keys when there's more than one;
            // else fall through to the bundle's JoinFileStems. Null /
            // empty means "let ExecuteRescore fall back to the
            // InputFiles-derived hash" (preserves v1 behavior).
            IReadOnlyList<string> joinFileStems = null;
            if (_perFileEntries != null && _perFileEntries.Count > 1)
            {
                var stems = new List<string>(_perFileEntries.Count);
                foreach (var kv in _perFileEntries)
                    stems.Add(kv.Key);
                joinFileStems = stems;
            }
            else if (rescoreBundle != null
                     && rescoreBundle.JoinFileStems != null
                     && rescoreBundle.JoinFileStems.Count > 0)
            {
                joinFileStems = rescoreBundle.JoinFileStems;
            }

            var rescoreStats = ExecuteRescore(
                _perFileEntries,
                ctx.Get<PerFileConsensusTargets>().Value,
                ctx.Get<ReconciliationActions>().Value,
                ctx.Get<RefinedCalibrations>().Value,
                ctx.Get<PerFileCalibrations>().Value,
                ctx.Get<PerFileGapFillForRescore>().Value,
                ctx.Get<PerFileParquetPaths>().Value,
                ctx.Get<FullLibrary>().Value,
                ctx.Config,
                ctx,
                out var rescoredFiles,
                joinFileStems,
                survivorLoader);
            ctx.LogInfo(string.Format(
                @"Reconciliation rescore: {0} entries re-scored ({1} reconciliation actions executed)",
                rescoreStats.TotalRescored, rescoreStats.TotalReconciliation));

            // The streamed loop emptied every file's list again as it went, so rebuild the
            // buffer SecondPassFDR reads. Reading it back from the reconciled parquets - rather
            // than keeping it live through the loop - is the whole point: it is resident
            // from here to the end of Stage 7 instead of for the entire rescore. Identical
            // to what the resume path reconstructs, which regression.ps1 mode 2 gates.
            //
            // Skipped when SecondPassFDR will not run in THIS process, because it is the only
            // reader of RescoredEntries and the rebuild exists solely to serve it. That is the
            // ordinary case for a --task PerFileRescoring worker (NoJoin, so
            // SecondPassFdrTask.IsIncluded is false), where the block would otherwise re-read
            // every .scores.parquet and 1st-pass sidecar and then re-read the
            // .scores-reconciled.parquet this task has just written - a full extra pass per
            // worker, per file, for a buffer the process exits without touching.
            if (survivorLoader != null && SecondPassFdrWillRun(ctx))
            {
                if (!MaterializeAllSurvivors(survivorLoader, ctx))
                    return false;
                // BEFORE the overlay, which appends gap-fill rows and re-sorts: the
                // planner's indices address the survivor list as loaded, and the sort
                // moves the appended rows into EntryId order, shifting every position
                // after them. The overlay preserves Score / q-values, so the reset
                // survives it.
                ResetRescoredTargets(_perFileEntries, rescoredFiles, ctx);
                OverlayReconciledIntoAllFiles(_perFileEntries, ctx, canonicalize: false);
            }

            // Cross-impl bisection seam: dump per-precursor state
            // immediately after the rescore loop. Mirrors Rust's
            // dump_stage6_rescored call from pipeline.rs.
            if (ctx.Diagnostics?.DumpRescored ?? false)
            {
                ctx.Diagnostics?.WriteStage6RescoredDump(_perFileEntries);
                if (ctx.Diagnostics?.RescoredOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_RESCORED_ONLY");
            }

            // Flush + close the persistent per-process diagnostic
            // dump writers (no-ops when their env vars are unset).
            // Mirrors the worker-mode close calls in RunWorker; without
            // these, the in-process pipeline path can leave the writers
            // unflushed and produce truncated bisection dumps.
            ctx.Diagnostics?.CloseMpInputsDump();
            // ClosePredictRtDump disabled with the rest of the predict-rt
            // diagnostic (perf hotspot); restore alongside WritePredictRtCall.
            // ctx.Diagnostics?.ClosePredictRtDump();
            ctx.Diagnostics?.CloseCwtPathDump();
            return true;
        }

        public override bool Rehydrate(PipelineContext ctx)
        {
            // Disk-load path for --task SecondPassFDR: every input parquet already
            // has osprey.reconciled = "true" (asserted by
            // ParquetScoreCache.CheckParquetMetadata when ExpectReconciledInput
            // is set), so Stage 5 first-pass Percolator AND Stage 6 planning /
            // rescore have ALREADY been performed upstream by the worker nodes
            // that wrote those parquets. We must NOT touch FirstPassFdrTask here --
            // demanding it would re-run Stage 5 first-pass Percolator from
            // scratch on the reconciled parquets (producing wildly different
            // action counts than the planner saw on the raw Stage 4 inputs) and
            // then attempt a Stage 6 rescore that needs mzML files the SecondPassFDR
            // node does not have (in production HPC SecondPassFDR ships only
            // sidecars + reconciled parquets, no mzMLs). SecondPassFdrTask is
            // responsible for 2nd-pass Percolator (Bug C) and protein FDR + blib
            // output starting from this hydrated, reconciled state. Mirrors Rust
            // pipeline.rs:3313-3344 which gates the entire Stage 5+6 block on
            // `!config.expect_reconciled_input`.
            //
            // Compaction still needs to run though: PerFileScoringTask's
            // bundle-hydration path loads ALL entries from the parquet,
            // including ones that failed first-pass FDR. FirstPassFdrTask's normal
            // flow would run this compaction inline after first-pass Percolator
            // (and we skip FirstPassFdrTask entirely here). Without it,
            // SecondPassFdrTask's 2nd-pass Percolator would train on ~3x too many
            // entries -- specifically the non-passing first-pass entries whose
            // 1st-pass q-values are 1.0 -- and the SVM would learn a much worse
            // decision boundary than the in-memory pipeline's, producing
            // different per-precursor scores and different protein-FDR results.
            // The compaction reads first-pass q-values already overlaid onto
            // each entry from the .1st-pass.fdr_scores.bin sidecar by
            // PerFileScoringTask's bundle hydration; no fresh FDR computation.
            //
            // Straight-through resume: the driver skipped this task's Run
            // because its reconciled parquets are already valid on disk
            // (CanRehydrate) and a downstream task (SecondPassFDR) is the first to
            // touch its state. A resumed Run self-gates to a no-op here --
            // FirstPassFDR rehydrates (so DidPlan is false) and there is no rescore
            // bundle, so ExecuteRescore never runs and the shared buffer is left
            // at its post-compaction (CompactedEntries) state; SecondPassFDR reloads
            // the rescored features from the valid reconciled parquets on disk.
            // Reproduce exactly that end state by loading the CompactedEntries
            // milestone (which materializes FirstPassFDR's own pure rehydrate) and
            // publishing it as RescoredEntries -- never calling Run, so Rehydrate
            // stays pure. The --task SecondPassFDR path (ExpectReconciledInput)
            // below is a different rehydrate that must NOT materialize FirstPassFDR.
            if (!ctx.Config.ExpectReconciledInput)
            {
                _perFileEntries = ctx.Get<CompactedEntries>().Value;

                // PR-E: a fresh ExecuteRescore would overlay each file's reconciled
                // boundaries/area/features onto its CompactedEntries rows + append
                // gap-fill. On resume the driver skipped Run because the reconciled
                // parquets are already valid, so do the equivalent in-place overlay
                // from each file's OWN .scores-reconciled.parquet -- otherwise the
                // buffer stays at 1st-pass RTs and SecondPassFDR (which reads ApexRt/
                // StartRt/EndRt/BoundsArea straight off these entries) writes 1st-pass
                // RTs into the final blib instead of the Stage 6 reconciled values.
                // Files with no reconciled sibling on disk are no-work files; a fresh
                // run leaves their entries at 1st-pass too, so they are left unchanged.
                //
                // Refill first when Stage 5 released the survivors (issue #4526). A resume
                // that re-runs Stage 5 lands here with an EMPTY buffer, and overlaying onto
                // empty lists produces an almost-empty blib instead of failing.
                var resumeLoader = StreamedSurvivorLoader(ctx);
                if (resumeLoader != null && !MaterializeAllSurvivors(resumeLoader, ctx))
                    return false;
                OverlayReconciledIntoAllFiles(_perFileEntries, ctx);

                ctx.Publish(new RescoredEntries(_perFileEntries));
                return true;
            }

            // ScoredEntries, NOT CompactedEntries: the reconciled-input path must NOT
            // materialize FirstPassFDR (that would re-run Stage 5 Percolator on the
            // reconciled parquets); it applies its own compaction below. Reading
            // the pre-compaction milestone keeps the dependency on PerFileScoring
            // alone, mirroring the old explicit Demand<PerFileScoringTask>.
            _perFileEntries = ctx.Get<ScoredEntries>().Value;

            // Publish the RescoredEntries milestone over the shared backing list
            // (the reconciled-input path applies its own compaction below, in place).
            ctx.Publish(new RescoredEntries(_perFileEntries));

            var bundle = ctx.Get<RescoreBundle>().Value;
            if (bundle != null)
            {
                // First-pass protein FDR BEFORE compaction. The 1st-pass FDR
                // sidecar v3 already carries RunProteinQvalue from the original
                // straight-through pipeline, but Rust pipeline.rs:4292 (gated by
                // `!can_skip_fdr || config.expect_reconciled_input`) recomputes
                // it inline in the --task SecondPassFDR path. The recompute uses the
                // post-rehydration detected_peptides set + best_peptide_scores
                // (which differ from the original write-time inputs whenever any
                // upstream rebuild has nudged peptide q-values or score values
                // even at the ULP level). RescoreCompaction below now retains the
                // persisted global first-pass base_id set and does NOT consult
                // RunProteinQvalue, so this recompute no longer affects the
                // compacted set; it is kept to hold RunProteinQvalue byte-consistent
                // with the straight-through pipeline for the downstream 2nd-pass
                // protein FDR and cross-impl parity (before recon-v3 read the
                // persisted set, omitting it diverged the post-compaction set from
                // Rust by ~19 peptides / 1 protein at Stage 7 on Stellar Single).
                // Do not remove without re-checking the 2nd-pass protein-FDR path.
                // Runs unconditionally (not gated on --protein-fdr), matching Rust where
                // first-pass protein FDR is gated only on !can_skip_fdr || expect_reconciled_input
                // (pipeline.rs:4529). Mirrors Rust pipeline.rs:4292-4358.
                //
                // SKIPPED when the bundle arrives ALREADY COMPACTED (#4486). The streaming
                // hydrate compacts each file as it loads - RescoreHydration's
                // stubs.RemoveAll(...) runs before perFileEntries.Add(...) - so on that path
                // "BEFORE compaction" above is no longer true and this call would recompute
                // over survivors only.
                //
                // The reason to skip is the SHAPE of that subset, not a measured defect. The
                // retained set is driven by which TARGETS passed, and a target and its paired
                // decoy share a base_id, so retaining a base_id retains both. That drops the
                // high-scoring decoys whose own targets did not pass - precisely the ones that
                // would compete near the threshold - which biases any FDR recomputed on the
                // survivors OPTIMISTIC. Subsetting without that bias needs a composite-score
                // cutoff admitting targets AND decoys above it plus their pairs, which this
                // pool is not. So do not run an FDR over it.
                //
                // Two things this comment previously asserted are MEASURED FALSE (#4486), and
                // must not be restored:
                //   * That recomputing here over the compacted pool drives protein q
                //     anti-conservatively low. A/B on StellarGenDecoyEntrap: recompute over the
                //     compacted pool vs over the uncompacted pool is BYTE-IDENTICAL across all
                //     260,419 records. This statistic is insensitive to the bias above (its
                //     decoy side comes from q-gated detected peptides either way), so the skip
                //     is a conservative choice, not a bug fix. It moves 740 records (0.28%)
                //     upward and changes no output.
                //   * That the 1st-pass sidecar's RunProteinQvalue is "what the straight-through
                //     pipeline computed". It is not: the join node's value differs from
                //     straight-through for 12.46% of records (1.57% at 82 files), always lower.
                //     That divergence is PRE-EXISTING - master's routing differs by 12.74% - and
                //     is tracked separately in #4553, which also covers the regression.ps1 gap
                //     that lets it pass green (mode 3 compares the blib, never these sidecars).
                //
                // PreCompactionTallies is the same "was this pre-compacted" signal
                // RescoreCompaction.Apply keys its own invariant on, so the two cannot
                // disagree about which path they are on.
                bool preCompacted = bundle.PreCompactionTallies != null;
                if (bundle.PerFileEntries.Count > 0 && !preCompacted)
                {
                    var fullLibrary = ctx.Get<FullLibrary>().Value;
                    // Silent (logInfo: null) -- the rehydration recompute runs
                    // before compaction with no log output, as it did when it
                    // called ProteinFdr.RunFirstPassProteinFdr directly.
                    ProteinFdrEngine.RunFirstPass(
                        bundle.PerFileEntries, fullLibrary, ctx.Config, null);
                }
                var stats = RescoreCompaction.Apply(bundle);
                // On the streaming hydrate stats.EntriesBefore EQUALS EntriesAfter by
                // construction - RescoreCompaction sums an already-compacted pool and makes
                // "removes nothing" a hard invariant - so reporting it raw prints
                // "N -> N entries" where ~350 M stubs were actually reduced, which is
                // indistinguishable from a broken retain set. TotalPreCompactionStubs is the
                // real figure on that path, and FirstPassFdrTask reads it for the same
                // reason (#4486).
                long entriesBefore = preCompacted
                    ? bundle.TotalPreCompactionStubs
                    : stats.EntriesBefore;
                ctx.LogInfo(string.Format(
                    @"--task SecondPassFDR compaction: {0} -> {1} entries ({2} passing base_ids; {3} action(s) dropped)",
                    entriesBefore, stats.EntriesAfter,
                    stats.FirstPassBaseIds, stats.DroppedActions));
            }
            return true;
        }

        // RunWorker + its helpers (AddIfNotNull, LoadOriginalRtCalibration)
        // were removed in Phase C. The stage6 worker mode
        // (--task PerFileRescoring) now routes through
        // AnalysisPipeline.Run with StartAt = StopAfter =
        // PerFileRescoreTask. Upstream state previously assembled in
        // RunWorker (library load, hydration, compaction, consensus,
        // calibration) is produced by PerFileScoringTask's joinOnly
        // probe-the-disk path and consumed through the lazy-rehydrate
        // accessors. Run() above is the only entry point.

        /// <summary>
        /// Execute the per-file Stage 6 rescore loop. Mirrors
        /// <c>rescore_per_file_loop</c> in
        /// <c>osprey/crates/osprey/src/pipeline.rs</c>.
        ///
        /// For each file with at least one re-scoring target:
        /// <list type="number">
        ///   <item>Build boundary_overrides keyed by entry_id.</item>
        ///   <item>Subset the library to the entries that need re-scoring.</item>
        ///   <item>Stream MS2 by isolation window from the .spectra.bin cache (required).</item>
        ///   <item>Reload MS2/MS1 mass calibration from the sibling .calibration.json.</item>
        ///   <item>Pick the refined RT calibration when present, else fall back to
        ///       the original first-pass calibration.</item>
        ///   <item>Call <see cref="ScoringPipeline"/>.RunCoelutionScoring with the override-aware
        ///       <see cref="ScoringContext"/>.</item>
        ///   <item>Overlay the re-scored entries back onto the per-file
        ///       FdrEntry stubs by entry_id, preserving ParquetIndex.</item>
        /// </list>
        ///
        /// The mutable <paramref name="perFileEntries"/> is updated in place
        /// (Score, Pep, q-values, Features, ApexRt/StartRt/EndRt, etc.).
        /// Returns <see cref="RescoreStats"/> with the per-stage counts.
        /// </summary>
        internal RescoreStats ExecuteRescore(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> perFileConsensusTargets,
            IReadOnlyDictionary<(string FileName, int Index), ReconcileAction> reconciliationActions,
            IReadOnlyDictionary<string, RTCalibration> refinedCalibrations,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, List<GapFillTarget>> perFileGapFill,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            PipelineContext ctx,
            out HashSet<string> rescoredFiles,
            IReadOnlyList<string> joinFileStems = null,
            FirstPassSurvivorLoader survivorLoader = null)
        {
            // Pre-group reconciliation actions by file so the per-file loop
            // below just looks up its slice.
            var perFileReconTargets =
                GroupReconciliationActionsByFile(reconciliationActions, out int totalReconciliation);

            // file_name -> input_files index, used to pick the right mzML
            // path for spectra cache load + sibling .calibration.json.
            var fileNameToIdx = BuildFileNameToIndex(config.InputFiles);

            // Cross-file inputs every per-file rescore reads, bundled so
            // RescoreOneFile takes one collaborator object instead of a dozen
            // positional parameters.
            var inputs = new RescorePassInputs
            {
                ConsensusTargets = perFileConsensusTargets,
                ReconTargets = perFileReconTargets,
                RefinedCalibrations = refinedCalibrations,
                PerFileCalibrations = perFileCalibrations,
                GapFill = perFileGapFill,
                ParquetPaths = perFileParquetPaths,
                FullLibrary = fullLibrary,
                Config = config,
                FileNameToIdx = fileNameToIdx,
                TaskValidityKey = ValidityKey(ctx),
                JoinFileStems = joinFileStems,
            };

            // Clean PERSISTENT floor entering reconciliation (post-GC, before the
            // rescore loop repopulates the heavy per-entry arrays) -- fires early,
            // so it lands even if a long run is later killed. #4376.
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"reconciliation start (pre-GC)");
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"reconciliation-floor",
                string.Format(@"(post-GC, entering rescore, files={0})", perFileEntries.Count));

            int nTotalFiles = perFileEntries.Count;
            // The Stage 6 rescore is the "second per-file fan-out": each file's rescore
            // is independent (its own entry list + its own .scores-reconciled.parquet),
            // and it reuses the same RunCoelutionScoring the Stage 1-4 fan-out already
            // runs concurrently. So run files in parallel under the SAME
            // EffectiveFileParallelism the scoring phase resolved (set on RunPlan by
            // PerFileScoringTask). Output is byte-identical to the sequential loop --
            // gated by regression.ps1 -- because the per-file work shares no mutable
            // state. Per-file results land by index so the accumulation is order-free.
            int parallelism = Math.Max(1, ctx.RunPlan.EffectiveFileParallelism);
            var counts = new (int Rescored, int GapCwt, int GapForced, bool Scored)[nTotalFiles];
            // Per-file survivor-refill failures, collected rather than thrown from inside the
            // parallel body so the actionable message survives (see RescoreOneFileStreamed).
            var loadErrors = new string[nTotalFiles];

            if (nTotalFiles == 1 || parallelism == 1)
            {
                for (int fileNum = 0; fileNum < nTotalFiles; fileNum++)
                    counts[fileNum] = RescoreOneFileStreamed(
                        fileNum, nTotalFiles, perFileEntries[fileNum],
                        inputs, survivorLoader, ctx, out loadErrors[fileNum]);
            }
            else
            {
                // Legend mapping each aggregate-line slot to its file, then the
                // concurrent rescore collapsed onto the throttled "[i] p%" line +
                // per-file buffered blocks (same MultiProgressReporter as scoring).
                ctx.LogInfo(string.Format(@"Re-scoring {0} files in parallel:", nTotalFiles));
                for (int i = 0; i < nTotalFiles; i++)
                {
                    string key = perFileEntries[i].Key;
                    string label =
                        inputs.FileNameToIdx.TryGetValue(key, out int inputIdx)
                        && inputIdx < inputs.Config.InputFiles.Count
                            ? inputs.Config.InputFiles[inputIdx]
                            : key;
                    ctx.LogInfo(string.Format(@"  {0}. {1}", i + 1, label));
                }
                var multi = new MultiProgressReporter();
                var parallelOpts = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
                Parallel.For(0, nTotalFiles, parallelOpts, fileNum =>
                {
                    using (multi.BeginFile(fileNum, RESCORE_FILE_SEGMENTS))
                    {
                        counts[fileNum] = RescoreOneFileStreamed(
                            fileNum, nTotalFiles, perFileEntries[fileNum],
                            inputs, survivorLoader, ctx, out loadErrors[fileNum]);
                    }
                });
            }

            // Fail the rescore on any per-file refill failure, HERE rather than inside the
            // loop above: the message names the file and the missing artifact, which is the
            // whole value of the diagnostic, and net472 loses it through AggregateException.
            foreach (string loadError in loadErrors)
            {
                if (loadError != null)
                    throw new InvalidDataException(loadError);
            }

            // Reconciliation memory probe (#4376). The pre-GC snapshot's peak
            // working set reflects the transient in-loop peak (parallelism x the
            // per-file ~1.5 GB spectra + parquet reload); the forced-GC line is the
            // clean PERSISTENT managed heap (all files' rescored FdrEntry buffer +
            // library). Zero-cost when OSPREY_LOG_MEMORY is unset.
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"reconciliation end (pre-GC)");
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"reconciliation-resident",
                string.Format(@"(files={0}, file_parallelism={1})", nTotalFiles, parallelism));

            int totalRescored = 0;
            int totalGapCwt = 0;
            int totalGapForced = 0;
            // The files that actually reached the scoring engine, so the streamed rebuild can
            // reapply the score / q-value reset to exactly those - see ResetRescoredTargets.
            rescoredFiles = new HashSet<string>();
            for (int fileNum = 0; fileNum < nTotalFiles; fileNum++)
            {
                var c = counts[fileNum];
                totalRescored += c.Rescored;
                totalGapCwt += c.GapCwt;
                totalGapForced += c.GapForced;
                if (c.Scored)
                    rescoredFiles.Add(perFileEntries[fileNum].Key);
            }

            return new RescoreStats
            {
                TotalRescored = totalRescored,
                TotalReconciliation = totalReconciliation,
                TotalGapCwt = totalGapCwt,
                TotalGapForced = totalGapForced,
            };
        }

        /// <summary>
        /// Cross-file inputs shared by every <see cref="RescoreOneFile"/> call:
        /// the planner's per-file byproducts, the library/config, and the
        /// resume/identity keys. Bundled so the per-file worker takes one
        /// collaborator object rather than a dozen positional parameters.
        /// </summary>
        private sealed class RescorePassInputs
        {
            public IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> ConsensusTargets;
            public IReadOnlyDictionary<string, List<(int Index, double Apex, double Start, double End)>> ReconTargets;
            public IReadOnlyDictionary<string, RTCalibration> RefinedCalibrations;
            public IReadOnlyDictionary<string, RTCalibration> PerFileCalibrations;
            public IReadOnlyDictionary<string, List<GapFillTarget>> GapFill;
            public IReadOnlyDictionary<string, string> ParquetPaths;
            public List<LibraryEntry> FullLibrary;
            public OspreyConfig Config;
            public Dictionary<string, int> FileNameToIdx;
            public string TaskValidityKey;
            public IReadOnlyList<string> JoinFileStems;
        }

        /// <summary>
        /// Bracket one file's <see cref="RescoreOneFile"/> with the survivor refill and
        /// release when Stage 6 is streaming (issue #4526), or call straight through when
        /// the resident buffer is in use.
        ///
        /// <para>The refill targets the file's EXISTING list object rather than replacing
        /// it, because that list is the shared backing store every
        /// <see cref="PerFileEntries"/> milestone wraps - swapping the reference would
        /// leave the published milestones pointing at the old one. Contents are transient;
        /// identity is not.</para>
        ///
        /// <para>Safe under the parallel file loop: each call touches only its own file's
        /// list, and the loader holds no per-file state.</para>
        ///
        /// <para><c>loadError</c> is set when this file's survivors could not be refilled, so
        /// the caller can fail the whole rescore with an actionable message AFTER the parallel
        /// loop. Throwing from inside <c>Parallel.For</c> wraps the exception in an
        /// AggregateException whose net472 message does not include the inner text, and nothing
        /// in Osprey unwraps it, so the reason was lost.</para>
        /// </summary>
        private (int Rescored, int GapCwt, int GapForced, bool Scored) RescoreOneFileStreamed(
            int fileNum, int nTotalFiles, KeyValuePair<string, List<FdrEntry>> file,
            RescorePassInputs inputs, FirstPassSurvivorLoader survivorLoader, PipelineContext ctx,
            out string loadError)
        {
            loadError = null;
            if (survivorLoader == null)
                return RescoreOneFile(fileNum, nTotalFiles, file.Key, file.Value, inputs, ctx);

            List<FdrEntry> stubs;
            // Serialized: the refill transiently holds the file's FULL pre-compaction stub set
            // plus the sidecar byte array before filtering down to survivors, which at 163
            // files is ~700 MB for one file. Under the parallel rescore that transient would
            // otherwise be multiplied by the file parallelism - several GB of new peak in a
            // change whose entire purpose is lowering peak. The rescore itself takes 2-2.5
            // minutes per file against a few seconds for this load, so admitting one loader at
            // a time costs a rounding error of wall clock and keeps the transient at 1x.
            lock (_survivorLoadLock)
            {
                stubs = survivorLoader.Load(file.Key, out string error);
                if (stubs == null)
                {
                    // Stage 5 wrote both artifacts, so this is a fault. Reporting it (rather
                    // than returning zero counts) keeps a file that could not be rescored from
                    // silently contributing nothing to the totals.
                    loadError = error;
                    return (0, 0, 0, false);
                }
                file.Value.Clear();
                file.Value.AddRange(stubs);
            }
            try
            {
                return RescoreOneFile(fileNum, nTotalFiles, file.Key, file.Value, inputs, ctx);
            }
            finally
            {
                // Drop this file's entries before the next one loads its own - but ONLY when
                // its reconciled parquet is actually on disk, because that file is what the
                // end-of-loop rebuild restores them from. RescoreOneFile makes the same call
                // about the heavy payload for the same reason (see the wroteReconciled gate
                // it applies to ReleaseRescoredPayload): when the write no-opped or failed,
                // these entries are the ONLY copy of the rescore, and dropping them would
                // discard it on a warning. Keeping one file's entries resident is the right
                // trade against silently losing its precursors.
                if (ReconciledParquetOnDisk(file.Key, inputs))
                {
                    file.Value.Clear();
                    file.Value.TrimExcess();
                }
            }
        }

        /// <summary>
        /// Run the Stage 6 rescore for a single file: resume-skip check, target
        /// assembly, subset scoring + overlay, gap-fill, and the reconciled
        /// parquet write-back. The per-file <paramref name="fdrEntries"/> buffer
        /// is updated in place. Returns the per-file (rescored, gap-CWT,
        /// gap-forced) counts the caller accumulates. The scoring orchestration
        /// is kept whole here (parity-locked); only the cross-file plumbing was
        /// lifted up into <see cref="ExecuteRescore"/>.
        /// </summary>
        private (int Rescored, int GapCwt, int GapForced, bool Scored) RescoreOneFile(
            int fileNum, int nTotalFiles, string fileName, List<FdrEntry> fdrEntries,
            RescorePassInputs inputs, PipelineContext ctx)
        {
            int totalRescored = 0;
            int totalGapCwt = 0;
            int totalGapForced = 0;

            // Per-file resume: a file whose reconciled parquet is already on
            // disk with a matching sidecar is overlaid in place and skipped.
            if (TryResumeRescoredFile(fileNum, nTotalFiles, fileName, fdrEntries, inputs, ctx))
                return (totalRescored, totalGapCwt, totalGapForced, false);

            // Assemble this file's rescore targets (multi-charge consensus +
            // reconciliation dedup + gap-fill) and resolve its input mzML.
            // Bails when there is no work or the file has no input_files entry.
            if (!TryAssembleRescoreTargets(fileNum, nTotalFiles, fileName, inputs, ctx,
                    out var combinedTargets, out var gapFillTargets, out string inputFile))
                return (totalRescored, totalGapCwt, totalGapForced, false);

            var config = inputs.Config;
            var fullLibrary = inputs.FullLibrary;

            // Clone the outer config for this file's ScoringContexts.
            // RunCoelutionScoring reassigns config.FragmentTolerance to
            // the MS2-calibrated tolerance (AnalysisPipeline.cs ~line 3552);
            // without a per-file clone the mutation persists on the outer
            // config, leaks into subsequent files, AND poisons the
            // WriteReconciledParquet hash stamp (config.Identity.SearchParameterHash()
            // would then reflect the calibrated tolerance, not the value
            // a fresh --task SecondPassFDR invocation recomputes from CLI
            // defaults -- causing search_hash mismatch errors). Mirrors
            // the per-file clone pattern in ProcessFile.
            var fileConfig = config.ShallowClone();

            // Divide the inner main-search thread budget across concurrently rescored
            // files so total demand stays near core count (mirrors ProcessFile under
            // --parallel-files). The subset rescore is light, but this still avoids
            // thread oversubscription when several files re-score at once.
            if (ctx.RunPlan.EffectiveFileParallelism > 1)
                fileConfig.NThreads = Math.Max(1, config.NThreads / ctx.RunPlan.EffectiveFileParallelism);

            // Build the per-file scoring subset: boundary_overrides keyed
            // by entry_id + the subset library RunCoelutionScoring scores.
            var (boundaryOverrides, subsetLibrary) =
                BuildScoringSubset(combinedTargets, fdrEntries, fullLibrary);

            // Segment 1/3 (read): index this file's spectra cache -- the file's first
            // progress slice on the --parallel-files "[i] p%" aggregate line.
            MultiProgressReporter.Current?.BeginSegment();
            // Stream this file's MS2 by isolation window from the .spectra.bin cache the
            // original Stage 1-4 run wrote, instead of materializing the whole ~6 GB
            // resident List<Spectrum>: build the seekable SpectraWindowIndex (MS1 + the
            // first-cycle isolation windows come from it too) and load each window on
            // demand during the re-score. Stage 6 REQUIRES that cache; there is no mzML
            // fallback -- a rescore without the cache is a deployment error, not a reason
            // to re-read the 6 GB mzML (LoadSpectraForRescore throws).
            SpectraWindowIndex spectraIndex = LoadSpectraForRescore(inputFile, fileName, ctx);
            var ms1Spectra = spectraIndex.Ms1Spectra.ToList();

            // Load-boundary memory probe. With streaming this measures the small index +
            // MS1 on the reconciliation floor (library + this file's parquet stubs) -- the
            // ~6 GB resident MS2 the resident load used to root here is gone (that reduction
            // is the point of this change). Kept as permanent instrumentation to catch a
            // future regression that re-materializes the MS2 at this boundary; the forced-GC
            // line is the live number and, under Profile-Osprey.ps1 -MemoryProfile, captures
            // a "perfile-rescore-loaded" retention snapshot. Zero cost -- collection
            // included -- when OSPREY_LOG_MEMORY is unset.
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"perfile-rescore-loaded (pre-GC)");
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"perfile-rescore-loaded",
                @"(post-GC, streaming index resident)");

            // Load the sibling .calibration.json so the search uses the
            // same MS2/MS1 mass calibrations the original Stage 1-4 run
            // used. The file is written by the original ProcessFile call
            // and read here -- same disk-roundtrip path the worker uses.
            LoadMassCalibrations(inputFile,
                out MzCalibrationResult ms2Cal,
                out MzCalibrationResult ms1Cal,
                out double? rtMadFromCalJson);

            // Pick the RT calibration: refined (from Stage 6 planning's
            // calibration refit) wins; original first-pass falls back.
            if (!inputs.RefinedCalibrations.TryGetValue(fileName, out RTCalibration rtCal))
                inputs.PerFileCalibrations.TryGetValue(fileName, out rtCal);

            // Bisection seam DISABLED (paired with the per-candidate
            // WritePredictRtCall, which was removed from the scoring
            // hotspot). Dumped the cal's library_rts + fitted_values once
            // per file. Mirrors Rust's dump_predict_rt_arrays at
            // pipeline.rs ~2886. To restore, re-enable this and the
            // WritePredictRtCall in CoelutionScorer. See
            // ai/todos/active/TODO-20260606_ospreysharp_diagnostics_di.md.
            // if (rtCal != null)
            // {
            //     ctx.Diagnostics.WritePredictRtArrays(
            //         fileName, rtCal.LibraryRts, rtCal.FittedValues);
            // }

            // Build the scoring context with the boundary overrides.
            // RunCoelutionScoring inspects context.BoundaryOverrides
            // inside ScoreCandidate and routes through the override
            // peak-construction path.
            var context = new ScoringContext(fileConfig, fileName);
            context.BoundaryOverrides = boundaryOverrides;
            context.OriginalRtMad = rtMadFromCalJson;

            // Isolation windows come straight from the streaming index -- reconstructed
            // with the same first-cycle dedup + Center sort ScoringTaskShared.ExtractIsolationWindows
            // applied to the resident list, so the window fan-out is unchanged.
            var isolationWindows = spectraIndex.IsolationWindows.ToList();

            // Stream each isolation window's calibrated MS2 from the index on demand. ONE
            // provider is shared across the subset re-score + both gap-fill passes: it holds
            // no per-window state (each GetCalibratedWindow is a fresh decode + in-place
            // calibration), so re-scoring the file three times just re-reads windows -- no
            // resident ~6 GB list, and none of the per-pass whole-list calibrated COPIES the
            // resident provider built. (That repeated-scoring is exactly why the resident path
            // had to pass consumeInputMzs:false; streaming has no such constraint.)
            IWindowSpectraProvider spectraProvider =
                new StreamingWindowSpectraProvider(spectraIndex, ms2Cal);

            // Segment 2/3 (score): the subset re-score; its "Re-scoring isolation
            // windows" reporter feeds this slice (the bulk of the file's motion).
            MultiProgressReporter.Current?.BeginSegment();
            // Re-score the subset.
            var swRescore = Stopwatch.StartNew();
            List<FdrEntry> rescored;
            if (subsetLibrary.Count > 0)
            {
                rescored = ScoringTaskShared.Pipeline(ctx).RunCoelutionScoring(
                    subsetLibrary, spectraProvider, ms1Spectra,
                    isolationWindows, rtCal,
                    ms2Cal, ms1Cal,
                    context, passLabel: "Re-scoring");
            }
            else
            {
                rescored = new List<FdrEntry>();
            }
            swRescore.Stop();

            // Overlay the re-scored subset back onto the per-file stubs,
            // resetting discriminant fields to Rust to_fdr_entry defaults.
            var (nOverlay, nNoPeak) =
                OverlayRescoredEntries(fdrEntries, combinedTargets, rescored);
            totalRescored += nOverlay;
            if (nNoPeak > 0)
            {
                ctx.LogInfo(string.Format(
                    "  {0} targets had no peak at override boundary (reset to defaults)",
                    nNoPeak));
            }

            ctx.LogInfo(string.Format(
                "  {0} of {1} existing entries re-scored ({2:F1}s)",
                nOverlay, combinedTargets.Count, swRescore.Elapsed.TotalSeconds));

            // PHASE 2 -- gap-fill two-pass.
            if (gapFillTargets.Count > 0)
            {
                var (nGapCwt, nGapForced) = RunGapFillTwoPass(
                    gapFillTargets, fullLibrary, spectraProvider, ms1Spectra,
                    isolationWindows, rtCal, ms2Cal, ms1Cal,
                    fileConfig, fileName, rtMadFromCalJson, fdrEntries, ctx);
                totalGapCwt += nGapCwt;
                totalGapForced += nGapForced;
                totalRescored += nGapCwt + nGapForced;
            }

            // Segment 3/3 (write): the reconciled parquet write-back.
            MultiProgressReporter.Current?.BeginSegment();
            // PHASE 3 -- reconciled parquet write-back + sidecar stamp.
            bool wroteReconciled = WriteReconciledAndStamp(fileName, inputFile, fdrEntries, inputs, ctx);

            // Per-file rescore high-water mark: the raw (pre-GC) working_set peak and
            // managed_heap since the last collection -- the during-scoring transient (the
            // per-window streamed+calibrated spectra + the scored/gap-fill entries). With
            // streaming there is no resident MS2 nor the per-pass whole-list calibrated
            // copies here, so this is the reduced "after" peak vs the resident baseline.
            // A forced-GC [MEM] is deliberately NOT taken (it would just show the
            // post-release floor). Zero cost when OSPREY_LOG_MEMORY is unset.
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"perfile-rescore-peak (pre-GC)");

            // Apex retention snapshot (dotMemory only). Once the resident MS2 is streamed,
            // the remaining per-file accumulation is the scored + reconciled entries still
            // rooted by fdrEntries here (each carries the heavy Features / CwtCandidates /
            // Fragment* / ReferenceXic* arrays). dotMemory forces its own GC before the
            // snapshot, so the write-back's parquet-reload transient collapses and the
            // dominators are exactly that retained entry set. Taken BEFORE the release
            // below so the snapshot still shows the pre-release apex it was written to
            // show. No-op unless a Profile-Osprey.ps1 -MemoryProfile session is attached.
            ProfilerHooks.CaptureRetentionSnapshot(@"perfile-rescore-apex");

            // Drop this file's heavy per-entry payload now that its reconciled parquet is
            // on disk. Without this the fat arrays stay rooted through every remaining
            // file, which is the O(files) Stage-6 growth term of #4472: the survivor stubs
            // themselves are lean (10 scalar columns off the parquet), but rescoring
            // replaces them with entries carrying Features / CwtCandidates / Fragment* /
            // ReferenceXic* at roughly 1-3 KB each, and gap fill appends more.
            //
            // MUST run AFTER WriteReconciledAndStamp above: ReconciledParquetWriter uses
            // "Features != null" as the "this row was rescored" sentinel when it builds the
            // overlay, so releasing before the write would silently emit a parquet with no
            // overlaid rows.
            //
            // And ONLY when that write actually persisted. WriteReconciledAndStamp no-ops
            // when the file has no ParquetPaths entry or its original parquet is missing, and
            // DELETES the reconciled parquet after a failed write - in all three cases these
            // arrays are still the only copy of the rescore, so dropping them would discard
            // it silently. Mirrors the Stage-4 release, which is likewise gated on its write
            // having happened (PerFileScoringTask's parquetFooterMetadata != null branch).
            if (wroteReconciled)
                ReleaseRescoredPayload(fdrEntries);

            // Deterministically drop this file's transients before the next file loads its
            // own: the streaming index + MS1 and the write-back's full-parquet reload. At
            // 100s of files .NET's Server GC otherwise defers collection until it nears the
            // RAM ceiling -- so the reconciliation working set rides up with file count
            // instead of staying flat. Forcing the collection here mirrors Rust's
            // per-iteration spectra drop (pipeline.rs:3338, in Rust's strictly sequential
            // reconciliation file loop) and keeps the working set at ~the persistent floor +
            // one file's transient. Output is unchanged (GC timing only). Skipped under
            // file-parallelism > 1, where concurrent files legitimately share residency and
            // a blocking GC would stall the other in-flight rescores.
            spectraProvider = null;
            spectraIndex = null;
            ms1Spectra = null;
            isolationWindows = null;
            rescored = null;
            if (ctx.RunPlan.EffectiveFileParallelism <= 1)
                GC.Collect();

            // Post-release floor: the persistent set that survives after this file's
            // transients (streaming index, MS1, scored entries) are dropped. Forced-GC so
            // it is the true floor, and it captures a "perfile-rescore-live" retention
            // snapshot to pair with the loaded snapshot. Zero cost -- collection included --
            // when OSPREY_LOG_MEMORY is unset.
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"perfile-rescore-live",
                @"(post-GC, after release)");

            return (totalRescored, totalGapCwt, totalGapForced, true);
        }

        /// <summary>
        /// Release the heavy per-entry payload a rescore attaches, once the file's
        /// reconciled parquet has been written. These six arrays have no reader after
        /// Stage 6: <see cref="ReconciledParquetWriter"/> consumes them per file during
        /// the write above, the Stage-6 planner loads CWT candidates through its own
        /// per-file loader rather than off these entries, and the resident 2nd pass
        /// reloads features from the reconciled parquet
        /// (<c>Pass2FdrSidecar.LoadReconciledFeaturesByIdentity</c>) rather than reading
        /// them here. Keeping them rooted is what made Stage-6 memory O(files) - the
        /// entries themselves are lean stubs until a rescore fattens them.
        ///
        /// Only the payload is dropped; the entry objects stay in the caller's list
        /// because <see cref="RescoredEntries"/> is published over that shared backing
        /// list and SecondPassFDR reads it for the run-wide reductions.
        ///
        /// Called on all three paths that end with a file's reconciled parquet on disk: the
        /// fresh rescore (gated on the write having persisted), the per-file resume skip
        /// (<see cref="TryResumeRescoredFile"/>), and the whole-task
        /// <see cref="Rehydrate"/> - the last two re-fatten the same six arrays out of that
        /// parquet via <see cref="OverlayReconciledIntoBuffer"/>, so without this they hold
        /// the full payload for every file at once. All three therefore leave the identical
        /// buffer shape.
        /// </summary>
        private static void ReleaseRescoredPayload(List<FdrEntry> fdrEntries)
        {
            if (fdrEntries == null)
                return;
            foreach (var entry in fdrEntries)
            {
                entry.Features = null;
                entry.CwtCandidates = null;
                entry.FragmentMzs = null;
                entry.FragmentIntensities = null;
                entry.ReferenceXicRts = null;
                entry.ReferenceXicIntensities = null;
            }
        }

        /// <summary>
        /// Per-file resume probe. When the file's reconciled parquet is already
        /// on disk with a matching
        /// <c>&lt;output&gt;.PerFileRescoring.osprey.task</c> sidecar, overlays
        /// the reconciled values back onto the in-memory entries (a partial
        /// resume must not leave 1st-pass RTs in the buffer a downstream
        /// SecondPassFDR reads) and returns true so the caller skips re-scoring.
        /// Pairs with the worker (stage6) crash-resume contract: re-invoking
        /// the same CLI on the same inputs is a no-op for files whose rescore
        /// completed. Otherwise clears any stale sidecar so a mid-Run crash
        /// leaves no false-positive, and returns false.
        /// </summary>
        private bool TryResumeRescoredFile(
            int fileNum, int nTotalFiles, string fileName,
            List<FdrEntry> fdrEntries, RescorePassInputs inputs, PipelineContext ctx)
        {
            // The rescore READS the original Stage 4 parquet and WRITES a
            // separate <stem>.scores-reconciled.parquet. Resume validity is
            // keyed on the reconciled output (the task's declared Output),
            // not on the original read source.
            bool hasParquetPath = inputs.ParquetPaths.TryGetValue(fileName, out string perFileParquetPath);
            string reconciledPath = hasParquetPath
                ? ParquetScoreCache.ReconciledPathFromScoresPath(perFileParquetPath)
                : null;
            if (hasParquetPath
                && PerFileResumeDriver.IsCurrent(reconciledPath, Name, inputs.TaskValidityKey))
            {
                ctx.LogInfo(string.Format(
                    @"[file] {0}/{1} {2}: skipping (outputs valid)",
                    fileNum + 1, nTotalFiles, fileName));

                // PR-E: a partial resume skips this already-rescored file, but
                // a downstream consumer (SecondPassFDR, in the full pipeline) reads
                // ApexRt/StartRt/EndRt/BoundsArea straight off these in-memory
                // entries. Without overlaying the reconciled values they stay at
                // the 1st-pass state and the final blib carries 1st-pass RTs.
                // Reproduce the fresh end state in place from the valid reconciled
                // parquet we just confirmed on disk + this file's gap-fill targets.
                IReadOnlyList<GapFillTarget> gapFillForFile = null;
                if (inputs.GapFill != null &&
                    inputs.GapFill.TryGetValue(fileName, out var gfList))
                    gapFillForFile = gfList;
                OverlayReconciledIntoBuffer(fdrEntries, reconciledPath, gapFillForFile);
                SortFileEntriesCanonical(fdrEntries);
                // The overlay above re-fattened every entry from the reconciled parquet
                // (Features / CwtCandidates / Fragment* / ReferenceXic*), so this skip arm
                // rooted the same ~1-3 KB per entry the rescore arm does - across every
                // resumed file, which is the O(files) Stage-6 growth term #4472 removed
                // there. Drop it the same way: the reconciled parquet those arrays came
                // from is on disk (that is the premise of this arm), and the only reader
                // of the "Features != null means rescored" sentinel is
                // ReconciledParquetWriter, which this arm returns before ever reaching.
                // Leaves exactly the buffer shape a fresh rescore leaves.
                ReleaseRescoredPayload(fdrEntries);
                return true;
            }
            // About to (re-)rescore this file: clear any stale sidecar
            // so a mid-Run crash leaves no false-positive pointing at
            // the partially-written reconciled parquet.
            if (hasParquetPath)
                PerFileResumeDriver.ClearStale(reconciledPath, Name);
            return false;
        }

        /// <summary>
        /// Assemble the per-file rescore target set: merge multi-charge
        /// consensus with reconciliation actions (reconciliation wins on
        /// conflict -- the inter-replicate peak boundary is more authoritative
        /// than the multi-charge consensus boundary), collect this file's
        /// gap-fill targets, and resolve its input mzML path. Logs the
        /// re-scoring banner + entry breakdown when there is work. Returns
        /// false -- caller skips the file -- when there is no work to do or the
        /// file has no input_files entry.
        /// </summary>
        private bool TryAssembleRescoreTargets(
            int fileNum, int nTotalFiles, string fileName,
            RescorePassInputs inputs, PipelineContext ctx,
            out Dictionary<int, (double Apex, double Start, double End)> combinedTargets,
            out List<GapFillTarget> gapFillTargets,
            out string inputFile)
        {
            combinedTargets = new Dictionary<int, (double Apex, double Start, double End)>();
            inputFile = null;

            IReadOnlyList<(int Index, double Apex, double Start, double End)> consensusTargets;
            if (!inputs.ConsensusTargets.TryGetValue(fileName, out consensusTargets))
                consensusTargets = new List<(int, double, double, double)>();

            List<(int Index, double Apex, double Start, double End)> reconTargets;
            if (!inputs.ReconTargets.TryGetValue(fileName, out reconTargets))
                reconTargets = new List<(int, double, double, double)>();

            // PHASE 2 (gap-fill): per-file gap-fill targets land here.
            if (inputs.GapFill == null ||
                !inputs.GapFill.TryGetValue(fileName, out gapFillTargets))
            {
                gapFillTargets = new List<GapFillTarget>();
            }

            // Merge consensus + reconciliation into a per-(idx, override) map.
            foreach (var t in consensusTargets)
                combinedTargets[t.Index] = (t.Apex, t.Start, t.End);
            foreach (var t in reconTargets)
                combinedTargets[t.Index] = (t.Apex, t.Start, t.End);

            // Skip files with no work to do.
            if (combinedTargets.Count == 0 && gapFillTargets.Count == 0)
                return false;

            if (!inputs.FileNameToIdx.TryGetValue(fileName, out int inputIdx))
            {
                ctx.LogWarning(string.Format(
                    "Reconciliation rescore: no input_files entry for {0} (skipping)", fileName));
                return false;
            }
            inputFile = inputs.Config.InputFiles[inputIdx];

            ctx.LogInfo(string.Format(
                "Re-scoring file {0}/{1}: {2}", fileNum + 1, nTotalFiles, fileName));
            ctx.LogInfo(string.Format(
                "  {0} entries ({1} consensus, {2} reconciliation, {3} gap-fill, {4} unique after dedup)",
                combinedTargets.Count + gapFillTargets.Count * 2,
                consensusTargets.Count,
                reconTargets.Count,
                gapFillTargets.Count,
                combinedTargets.Count));
            return true;
        }

        /// <summary>
        /// PHASE 3 -- reconciled parquet write-back. Reads the original Stage 4
        /// parquet and writes a separate <c>.scores-reconciled.parquet</c>
        /// sibling (leaving the original intact), then stamps the per-file
        /// resume sidecar -- but ONLY on a successful write, so a failed write
        /// can never mark stale reconciled content valid (which would let
        /// Stage 7 / a future resume consume old rescored content). On failure
        /// clears the sidecar and removes the partially-written parquet so the
        /// next run re-rescores this file from scratch.
        ///
        /// Returns whether the reconciled parquet was actually PERSISTED. False when
        /// this file has no <c>ParquetPaths</c> entry or its original parquet is gone
        /// (both no-ops) and when the write failed (the output was just deleted). The
        /// caller must not drop the heavy per-entry payload on a false return: those
        /// arrays are then still the only copy of the rescore, and
        /// <see cref="ReconciledParquetWriter"/> reads them off the entries on the retry.
        /// </summary>
        private bool WriteReconciledAndStamp(
            string fileName, string inputFile, List<FdrEntry> fdrEntries,
            RescorePassInputs inputs, PipelineContext ctx)
        {
            var config = inputs.Config;
            // ParquetPaths is non-null here (dereferenced at the resume probe).
            if (!inputs.ParquetPaths.TryGetValue(fileName, out string parquetPath) ||
                !File.Exists(parquetPath))
            {
                return false;
            }

            string reconciledOutPath = ParquetScoreCache.ReconciledPathFromScoresPath(parquetPath);
            bool wrote = ReconciledParquetWriter.Write(parquetPath, reconciledOutPath, fdrEntries, fileName,
                inputs.FullLibrary, config, inputs.JoinFileStems, ctx.LogInfo, ctx.LogWarning);

            if (wrote)
            {
                var perFileInputs = new List<string>
                {
                    FdrScoresSidecar.Pass1Path(inputFile),
                };
                if (config.Reconciliation != null && config.Reconciliation.Enabled)
                    perFileInputs.Add(ReconciliationFile.PathForInput(inputFile));
                PerFileResumeDriver.Stamp(reconciledOutPath, Name, OspreyVersion.Current,
                    inputs.TaskValidityKey, perFileInputs, ctx.LogWarning);
                return true;
            }

            // Clear the stale sidecar AND remove the partially-written
            // reconciled parquet (output mechanics, the task's own
            // concern) so the next run re-rescores from scratch.
            PerFileResumeDriver.ClearStale(reconciledOutPath, Name);
            try
            {
                if (File.Exists(reconciledOutPath))
                    File.Delete(reconciledOutPath);
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(
                    @"  Failed to remove stale reconciled parquet {0} after a failed write: {1}",
                    reconciledOutPath, ex.Message));
            }
            return false;
        }

        /// <summary>
        /// Pre-group reconciliation actions by file. Mirrors the Rust
        /// pre-grouping at pipeline.rs:2719-2744 -- a single pass over
        /// the action map produces (file -> [(idx, apex, start, end)])
        /// so the per-file loop just looks up its slice. Returns the
        /// per-file map; <paramref name="totalReconciliation"/> receives
        /// the count of non-Keep actions grouped.
        /// </summary>
        private static Dictionary<string, List<(int Index, double Apex, double Start, double End)>>
            GroupReconciliationActionsByFile(
                IReadOnlyDictionary<(string FileName, int Index), ReconcileAction> reconciliationActions,
                out int totalReconciliation)
        {
            var perFileReconTargets =
                new Dictionary<string, List<(int Index, double Apex, double Start, double End)>>();
            totalReconciliation = 0;
            foreach (var kvp in reconciliationActions)
            {
                var fileName = kvp.Key.FileName;
                var idx = kvp.Key.Index;
                double apex, start, end;
                if (kvp.Value is ReconcileAction.UseCwtPeak useCwt)
                {
                    apex = useCwt.ApexRt;
                    start = useCwt.StartRt;
                    end = useCwt.EndRt;
                }
                else if (kvp.Value is ReconcileAction.ForcedIntegration forced)
                {
                    apex = forced.ExpectedRt;
                    start = forced.ExpectedRt - forced.HalfWidth;
                    end = forced.ExpectedRt + forced.HalfWidth;
                }
                else
                {
                    // Keep: planner omits these from the map by design,
                    // but stay defensive -- skip rather than crash.
                    continue;
                }
                if (!perFileReconTargets.TryGetValue(fileName, out var list))
                {
                    list = new List<(int, double, double, double)>();
                    perFileReconTargets[fileName] = list;
                }
                list.Add((idx, apex, start, end));
                totalReconciliation++;
            }
            return perFileReconTargets;
        }

        /// <summary>
        /// Build the file_name -> input_files index map used to pick the
        /// right mzML path for the spectra-cache load + sibling
        /// .calibration.json. For the worker, config.InputFiles was
        /// synthesized from --input-scores parquet stems by Program.Main;
        /// for in-process it's the user's -i mzML list. Either way the
        /// stem matches the file_name keys in perFileEntries.
        /// </summary>
        private static Dictionary<string, int> BuildFileNameToIndex(IReadOnlyList<string> inputFiles)
        {
            var fileNameToIdx = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < inputFiles.Count; i++)
            {
                string stem = Path.GetFileNameWithoutExtension(inputFiles[i]) ?? string.Empty;
                fileNameToIdx[stem] = i;
            }
            return fileNameToIdx;
        }

        /// <summary>
        /// Build the per-file scoring subset: the boundary_overrides map
        /// keyed by entry_id, and the subset library handed to
        /// <see cref="ScoringPipeline"/>.RunCoelutionScoring so it
        /// doesn't waste work on entries we're not re-scoring. The subset
        /// is the same library entries the original Stage 1-4 scoring used,
        /// just a smaller list.
        /// </summary>
        private static (Dictionary<uint, (double Apex, double Start, double End)> BoundaryOverrides,
            List<LibraryEntry> SubsetLibrary) BuildScoringSubset(
                Dictionary<int, (double Apex, double Start, double End)> combinedTargets,
                List<FdrEntry> fdrEntries,
                List<LibraryEntry> fullLibrary)
        {
            // Build boundary_overrides keyed by entry_id. Also collect the
            // subset of library ids the search engine needs to score.
            var boundaryOverrides = new Dictionary<uint, (double Apex, double Start, double End)>();
            var subsetIds = new HashSet<uint>();
            foreach (var kvp in combinedTargets)
            {
                int idx = kvp.Key;
                uint entryId = fdrEntries[idx].EntryId;
                boundaryOverrides[entryId] = kvp.Value;
                subsetIds.Add(entryId);
            }

            List<LibraryEntry> subsetLibrary;
            if (subsetIds.Count == 0)
            {
                subsetLibrary = new List<LibraryEntry>();
            }
            else
            {
                subsetLibrary = new List<LibraryEntry>(subsetIds.Count);
                foreach (var libEntry in fullLibrary)
                {
                    if (subsetIds.Contains(libEntry.Id))
                        subsetLibrary.Add(libEntry);
                }
            }
            return (boundaryOverrides, subsetLibrary);
        }

        /// <summary>
        /// Overlay re-scored entries back onto the per-file FdrEntry stubs
        /// by entry_id, preserving the original ParquetIndex so the
        /// write-back step can target the right Parquet row (post-compaction
        /// Vec position != Parquet row index).
        ///
        /// Mirror Rust's to_fdr_entry semantics: post-rescore stubs carry
        /// default Score (0.0), q-values (1.0), and Pep (1.0). Percolator
        /// (Stage 7, second-pass FDR) recomputes these from the new
        /// Features. Without this reset the Osprey ScoreCandidate's
        /// <c>Score = coelutionSum</c> initializer bleeds through, producing
        /// 173k rows of post-rescore divergence vs the Rust worker's
        /// rust_stage6_rescored.tsv. Targets where RunCoelutionScoring
        /// returned no entry (no peak at the override boundary) STILL get
        /// their existing stub reset in place -- Rust's worker emits zeroed
        /// stubs for every override regardless of peak success. Returns
        /// (entries overlaid, no-peak resets).
        /// </summary>
        private static (int NOverlay, int NNoPeak) OverlayRescoredEntries(
            List<FdrEntry> fdrEntries,
            Dictionary<int, (double Apex, double Start, double End)> combinedTargets,
            List<FdrEntry> rescored)
        {
            // Pass 1: index the rescored results by entry_id so we
            // can look up successful re-scores in the second pass.
            var rescoredByEntryId = new Dictionary<uint, FdrEntry>();
            foreach (var entry in rescored)
            {
                rescoredByEntryId[entry.EntryId] = entry;
            }

            // Pass 2: iterate every combined target.
            int nOverlay = 0;
            int nNoPeak = 0;
            foreach (var kvp in combinedTargets)
            {
                int idx = kvp.Key;
                uint entryId = fdrEntries[idx].EntryId;
                if (rescoredByEntryId.TryGetValue(entryId, out FdrEntry rescoredEntry))
                {
                    rescoredEntry.ResetScores();
                    rescoredEntry.ParquetIndex = fdrEntries[idx].ParquetIndex;
                    fdrEntries[idx] = rescoredEntry;
                    nOverlay++;
                }
                else
                {
                    // No peak at the override boundary -- reset to
                    // defaults in place to match Rust's behavior.
                    fdrEntries[idx].ResetScores();
                    nNoPeak++;
                }
            }
            return (nOverlay, nNoPeak);
        }

        /// <summary>
        /// Resume overlay: reproduce a fresh Stage 6 ExecuteRescore's in-memory
        /// end state for a single file by loading that file's OWN
        /// <c>.scores-reconciled.parquet</c> and overlaying its reconciled
        /// boundary / area / feature / blob columns onto the post-compaction
        /// buffer entries (matched by <see cref="FdrEntry.EntryId"/>), then
        /// appending the file's gap-fill rows.
        ///
        /// This is the parity-safe counterpart to <see cref="OverlayRescoredEntries"/>
        /// + <see cref="RunGapFillTwoPass"/> for the resume paths, where the
        /// reconciled parquet is already valid on disk and re-running the scoring
        /// engine would be both wasteful and (on a SecondPassFDR node) impossible. Both
        /// resume paths -- the straight-through <see cref="Rehydrate"/> no-op and
        /// the per-file skip inside <see cref="ExecuteRescore"/> -- previously
        /// left the buffer at its 1st-pass <see cref="CompactedEntries"/> state,
        /// so SecondPassFDR (which reads ApexRt/StartRt/EndRt/BoundsArea DIRECTLY off
        /// these entries) wrote 1st-pass RTs into the final blib instead of the
        /// Stage 6 reconciled RTs.
        ///
        /// Mirrors the fresh end state exactly: the reconciled parquet row's
        /// reconciled boundary fields are copied in place, the original
        /// ParquetIndex + 1st-pass Score / q-values are PRESERVED (matching what
        /// CompactedEntries + the PR-D worker-strict gate established), and
        /// gap-fill rows are appended with <c>ParquetIndex = uint.MaxValue</c>.
        /// Non-passing reconciled rows (compacted out of the buffer) are skipped,
        /// matching the buffer a fresh run produces.
        /// </summary>
        /// <param name="fileEntries">One file's entry list, updated in place.</param>
        /// <param name="reconciledPath">That file's <c>.scores-reconciled.parquet</c>.</param>
        /// <param name="gapFillForFile">The file's gap-fill targets, or null when it had none.</param>
        private void OverlayReconciledIntoBuffer(List<FdrEntry> fileEntries,
            string reconciledPath, IReadOnlyList<GapFillTarget> gapFillForFile)
        {
            List<FdrEntry> loaded;
            try
            {
                // Scalars only: every caller releases the heavy payload immediately after this
                // returns (see ReleaseRescoredPayload), so decoding the 21 PIN feature columns
                // and the four blob columns per row would be work done purely to discard it.
                loaded = ParquetScoreCache.LoadFullFdrEntries(reconciledPath, scalarsOnly: true);
            }
            catch (Exception ex)
            {
                // CanRehydrate already certified this reconciled parquet valid on
                // disk, so a load failure here is a genuine fault -- and neither
                // resume path has a compute fallback (straight-through resume does
                // not re-score; the per-file skip explicitly trusts on-disk
                // outputs). Leaving the buffer at its 1st-pass state would silently
                // write wrong RTs to the blib, so fail loudly: the throw propagates
                // to Program's top-level handler (exit code 1).
                throw new InvalidDataException(string.Format(
                    @"Stage 6 resume overlay: failed to reload valid-on-disk reconciled parquet {0}: {1}",
                    reconciledPath, ex.Message), ex);
            }

            // Index reconciled rows by EntryId, keeping ALL rows for an id in the order the
            // parquet holds them (canonical entry_id, charge, scan). A given EntryId can
            // legitimately appear more than once - one row per isolation window a precursor
            // was scored in - and the buffer can hold the same duplication, because
            // compaction filters by base_id rather than collapsing to one row per EntryId
            // (nothing enforces the "at most one" the previous comment here asserted). Taking
            // the FIRST row for every buffer entry aliased both buffer rows onto one
            // reconciled row, which gave them the same ScanNumber and so the same pass-2
            // (EntryId, Charge, ScanNumber) identity - two survivors competing as one.
            var byId = new Dictionary<uint, List<FdrEntry>>(loaded.Count);
            foreach (var r in loaded)
            {
                if (!byId.TryGetValue(r.EntryId, out var rows))
                {
                    rows = new List<FdrEntry>(1);
                    byId[r.EntryId] = rows;
                }
                rows.Add(r);
            }

            // Overlay reconciled boundary / area / feature / blob columns onto the
            // existing buffer rows IN PLACE, preserving each row's ParquetIndex and
            // 1st-pass Score / q-values (FdrEntry is a reference type, so mutating
            // fields updates the shared list element directly).
            //
            // Rows are paired POSITIONALLY within an EntryId: both sides are in canonical
            // order, so the n-th buffer row for an id takes the n-th reconciled row for it.
            // There is no stable key finer than EntryId available - Charge does not separate
            // two isolation windows, and ScanNumber is the very field a rescore moves - so
            // positional pairing is the most specific correspondence the data supports. It
            // reduces to the obvious answer in the single-row case, which is every row today.
            var nextRowById = new Dictionary<uint, int>();
            var existingIds = new HashSet<uint>();
            foreach (var entry in fileEntries)
            {
                existingIds.Add(entry.EntryId);
                if (!byId.TryGetValue(entry.EntryId, out var rows))
                    continue;
                nextRowById.TryGetValue(entry.EntryId, out int next);
                if (next >= rows.Count)
                    continue;
                nextRowById[entry.EntryId] = next + 1;
                var r = rows[next];
                // A MOVED peak's ScanNumber changes, because the rescore replaces the buffer
                // entry with the newly scored one. The pass-2 frozen-model override is looked
                // up by (EntryId, Charge, ScanNumber) (Pass2FdrSidecar), so leaving the
                // 1st-pass ScanNumber here makes every moved peak miss that lookup: it gets no
                // override, reads as UNCHANGED, never earns a fresh run q, and is reported on
                // its stale pass-1 q. On Stellar that was 110,541 of 994,509 survivors missing
                // an override and 31,583 spectra reported against the golden 29,364.
                entry.ScanNumber = r.ScanNumber;
                // Same class of omission as ScanNumber, and the same consequence. A cold
                // rescore swaps the whole entry, so CoelutionSum becomes the new peak's
                // feature 0; it IS persisted (fragment_coelution_sum) and IS loaded into
                // r, but not copying it leaves every reconciled peak on its 1st-pass
                // value - and ReleaseRescoredPayload then nulls Features, destroying the
                // only fresh copy. Stage 7 reads it straight off the buffer
                // (PercolatorEntryBuilder's basic-feature fallback, and best-per-precursor
                // selection), so a path that skipped this would train on a different subset
                // than the resident oracle.
                entry.CoelutionSum = r.CoelutionSum;
                entry.ApexRt = r.ApexRt;
                entry.StartRt = r.StartRt;
                entry.EndRt = r.EndRt;
                entry.BoundsArea = r.BoundsArea;
                entry.BoundsSnr = r.BoundsSnr;
                // The heavy payload is deliberately NOT copied. The load above is scalar-only,
                // so those fields are null on r, and assigning them would only overwrite the
                // entry's own payload with nulls - inert while every caller releases the
                // payload immediately afterwards, but a trap the moment one does not.
            }

            // Append gap-fill rows. A fresh run appends one stub per gap-fill
            // target (decoys already excluded by the planner) with ParquetIndex =
            // uint.MaxValue. Pull the reconciled row for each target EntryId that
            // is not already in the buffer; append in ascending TargetEntryId order
            // for determinism. Targets whose reconciled row is missing (no peak)
            // are skipped -- a fresh run would not have appended a stub either.
            // Gap fill is appended from the target list in reconciliation.json, for BOTH the
            // resume overlay and the streamed rebuild, in ascending TargetEntryId - the same
            // order the fresh rescore now appends its own gap-fill block in
            // (RunGapFillTwoPass sorts before appending, for exactly this reason). Two earlier
            // mechanisms were tried and are recorded here so they are not retried:
            //
            //  * Rows carried out of the rescore in memory. Cannot work: --task
            //    PerFileRescoring rescores each file in its own process, so nothing held
            //    there reaches SecondPassFDR - and it is an O(files) resident term
            //    (~270 MB at 163 files), the shape this change exists to remove.
            //  * Replaying the reconciled parquet's appended "tail" by row index. There is no
            //    tail to replay: StreamReconciledScoresParquet MERGES the gap-fill rows into
            //    their canonical (entry_id, charge, scan) position rather than appending them,
            //    because Pass 2 recovers scan order from the reconciled row index. (An earlier
            //    comment here blamed LoadFullFdrEntries for not preserving parquet row order.
            //    That was wrong - it reads the row groups in order and preserves it exactly.)
            if (gapFillForFile != null && gapFillForFile.Count > 0)
            {
                var gapFillIds = new SortedSet<uint>();
                foreach (var t in gapFillForFile)
                    gapFillIds.Add(t.TargetEntryId);
                foreach (var gid in gapFillIds)
                {
                    if (existingIds.Contains(gid))
                        continue;
                    if (!byId.TryGetValue(gid, out var gapRows) || gapRows.Count == 0)
                        continue;
                    // EVERY row for this target, not just the first. Gap fill is the one place
                    // a duplicate EntryId can actually arise: the survivor rows come from
                    // .scores.parquet, which Stage 4 wrote AFTER DeduplicatePairs, but
                    // RunGapFillTwoPass calls RunCoelutionScoring with neither
                    // DeduplicatePairs nor DeduplicateDoubleCounting, and ScoreWindow admits a
                    // candidate per isolation window. With overlapping windows one target
                    // yields two stubs, and a cold rescore appends both. Taking only the first
                    // silently dropped a survivor from the buffer Stage 7 competes over.
                    //
                    // Parquet order within an EntryId is canonical (entry_id, charge, scan),
                    // which is the order the cold path's sorted block puts them in too, so the
                    // two agree row for row.
                    foreach (var g in gapRows)
                    {
                        g.ParquetIndex = uint.MaxValue;
                        fileEntries.Add(g);
                    }
                    existingIds.Add(gid);
                }
            }

            // The canonical (EntryId, Charge, ScanNumber, ParquetIndex) re-sort is
            // applied by the CALLER via SortFileEntriesCanonical -- it runs for every
            // file in the resume path, not only files with a reconciled parquet.
        }


        /// <summary>
        /// True when this file's <c>.scores-reconciled.parquet</c> is on disk AND current for
        /// this run's validity key, i.e. the end-of-loop rebuild really can restore its
        /// rescored state from it. Asks the same question <see cref="TryResumeRescoredFile"/>
        /// asks, so the answer cannot disagree with it: a parquet the rebuild would refuse to
        /// overlay must not be treated as a safe place to drop this file's entries.
        /// </summary>
        private bool ReconciledParquetOnDisk(string fileName, RescorePassInputs inputs)
        {
            if (inputs.ParquetPaths == null ||
                !inputs.ParquetPaths.TryGetValue(fileName, out string scoresPath))
            {
                return false;
            }
            return PerFileResumeDriver.IsCurrent(
                ParquetScoreCache.ReconciledPathFromScoresPath(scoresPath), Name,
                inputs.TaskValidityKey);
        }

        /// <summary>
        /// The loader Stage 6 refills from, or null when this run keeps the resident
        /// buffer. Non-null requires BOTH that FirstPassFDR published a source (only the
        /// projection path computes the passing base_id set it needs) and that streaming
        /// is enabled, which is what <c>OSPREY_STAGE6_STREAM_SURVIVORS=0</c> turns off to
        /// get the resident A/B oracle back.
        /// </summary>
        private static FirstPassSurvivorLoader StreamedSurvivorLoader(PipelineContext ctx)
        {
            if (!OspreyEnvironment.Stage6StreamSurvivors)
                return null;
            return ctx.TryGet<FirstPassSurvivorSource>(out var source) ? source?.Value : null;
        }

        /// <summary>
        /// True when <see cref="SecondPassFdrTask"/> is part of THIS process's pipeline, i.e.
        /// when the <c>RescoredEntries</c> milestone will actually be read. Asked of the task
        /// itself rather than re-derived from the config, so the answer cannot drift from
        /// <see cref="OspreyTask.IsIncluded"/> - a second copy of that truth table is how a
        /// worker ends up doing whole-run work nothing in the process consumes.
        /// </summary>
        private static bool SecondPassFdrWillRun(PipelineContext ctx)
        {
            foreach (var task in ctx.Tasks)
            {
                if (task is SecondPassFdrTask)
                    return task.IsIncluded(ctx);
            }
            return false;
        }

        /// <summary>
        /// Refill every file whose survivor list was released, leaving files that already
        /// hold entries untouched so a second call is a no-op. Returns false (ExitCode
        /// set) if any file's parquet or 1st-pass sidecar cannot be read - Stage 5 wrote
        /// both, so a failure here is a fault rather than an absence.
        /// </summary>
        private bool MaterializeAllSurvivors(FirstPassSurvivorLoader loader, PipelineContext ctx)
        {
            // Reported, not silent: this is a per-file parquet + sidecar read across every file
            // in the run, landing immediately after the parallel rescore where nothing else is
            // printing. An unreported sequential loop of exactly this shape has twice read as a
            // hung run in this codebase (#4513, Pass2FdrSidecar). Console-only.
            using (var progress = new ProgressReporter(string.Format(
                       @"Rebuilding first-pass survivors from {0} file(s)", _perFileEntries.Count),
                       _perFileEntries.Count))
            {
                int done = 0;
                foreach (var kv in _perFileEntries)
                {
                    progress.Report(++done);
                    if (kv.Value.Count > 0)
                        continue;
                    var stubs = loader.Load(kv.Key, out string error);
                    if (stubs == null)
                    {
                        ctx.LogError(error);
                        ctx.ExitCode = 1;
                        return false;
                    }
                    kv.Value.AddRange(stubs);
                }
            }
            return true;
        }

        /// <summary>
        /// Reapply the score / q-value reset that <see cref="OverlayRescoredEntries"/>
        /// performs on every rescore target, which the rebuild-from-disk cannot recover.
        ///
        /// <para>A fresh rescore sets each target's Score to 0 and all q-values and Pep to
        /// 1.0, because the 2nd pass is what assigns their real values. That reset lives
        /// ONLY in memory: <c>ReconciledParquetWriter</c> persists boundaries, area and
        /// features, not scores, and <see cref="OverlayReconciledIntoBuffer"/> documents
        /// that it deliberately preserves each row's 1st-pass Score / q-values. So a
        /// buffer rebuilt from the reconciled parquet carries 1st-pass q-values where a
        /// fresh run carries 1.0.</para>
        ///
        /// <para>That used to be invisible, because the retired pass-2 Percolator recomputed
        /// q for every entry. Under the frozen-model modes that replaced it (#4528,
        /// protein-compact now the default) an OFF-stratum survivor KEEPS its 1st-pass q,
        /// so the difference reaches the report: without this reset the Stellar straight-
        /// through run reported 31,583 precursors against the golden 29,364.</para>
        ///
        /// <para>The target set is rebuilt from the same bounded planner byproducts
        /// <see cref="TryAssembleRescoreTargets"/> reads, and the rebuilt list is in the
        /// same canonical order the planner indexed, so the positional indices select the
        /// same entries they did during the rescore.</para>
        ///
        /// <para>Applied ONLY to the files in <paramref name="rescoredFiles"/> - the ones that
        /// actually reached <see cref="OverlayRescoredEntries"/> in this process. Having
        /// planner targets is not the same as having been rescored: a file that took the
        /// per-file resume skip (<see cref="TryResumeRescoredFile"/>), or whose
        /// <see cref="TryAssembleRescoreTargets"/> returned false because it has no
        /// <c>input_files</c> entry, returns before the reset and keeps its real 1st-pass
        /// q-values. Resetting those too would zero q-values the resident path leaves alone,
        /// and under protein-compact an off-stratum survivor KEEPS its 1st-pass q - so they
        /// would drop out of the report. That is the mirror image of the over-reporting this
        /// reset exists to fix.</para>
        /// </summary>
        private static void ResetRescoredTargets(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            HashSet<string> rescoredFiles, PipelineContext ctx)
        {
            var consensus = ctx.Get<PerFileConsensusTargets>().Value;
            var reconTargets = GroupReconciliationActionsByFile(
                ctx.Get<ReconciliationActions>().Value, out _);
            foreach (var kv in perFileEntries)
            {
                if (!rescoredFiles.Contains(kv.Key))
                    continue;
                var indices = new HashSet<int>();
                if (consensus != null && consensus.TryGetValue(kv.Key, out var consensusTargets))
                {
                    foreach (var t in consensusTargets)
                        indices.Add(t.Index);
                }
                if (reconTargets.TryGetValue(kv.Key, out var recon))
                {
                    foreach (var t in recon)
                        indices.Add(t.Index);
                }
                var entries = kv.Value;
                foreach (int idx in indices)
                {
                    // A planner index outside the rebuilt list means the rebuild did not
                    // reproduce the list the planner indexed - which would silently reset the
                    // WRONG rows for every index that IS in range. Skipping it (as this did)
                    // swallowed the one cheap symptom of a misaligned rebuild; the fresh
                    // rescore would have thrown IndexOutOfRange on the same index.
                    if (idx < 0 || idx >= entries.Count)
                    {
                        throw new InvalidDataException(string.Format(
                            @"Stage 6 rebuild: planner index {0} for {1} is outside the rebuilt " +
                            @"survivor list ({2} entries). The rebuilt buffer does not match the " +
                            @"one the planner indexed.", idx, kv.Key, entries.Count));
                    }
                    entries[idx].ResetScores();
                }
            }
        }

        /// <summary>
        /// Bring EVERY file's list to its post-rescore state by overlaying that file's
        /// <c>.scores-reconciled.parquet</c>, canonicalizing the order, and releasing the
        /// re-fattened payload. This is the state a fresh <see cref="ExecuteRescore"/>
        /// leaves behind, rebuilt from disk.
        ///
        /// <para>Two callers, one body. The resume <see cref="Rehydrate"/> uses it because
        /// the driver skipped <see cref="Run"/> when the reconciled parquets were already
        /// valid. The streamed rescore uses it because it deliberately dropped each file's
        /// entries after writing that file's parquet, so the
        /// <see cref="RescoredEntries"/> milestone SecondPassFDR reads has to be rebuilt at
        /// the end (issue #4526). Sharing the body is what makes the streamed buffer
        /// identical to the resumed one.</para>
        /// </summary>
        /// <param name="perFileEntries">The shared per-file buffer to bring to its
        /// post-rescore state, updated in place.</param>
        /// <param name="ctx">Pipeline context, for the planner byproducts and parquet paths.</param>
        /// <param name="canonicalize">Re-sort each file by the canonical
        /// (EntryId, Charge, ScanNumber, ParquetIndex) key. TRUE on resume, where the
        /// buffer has to be brought to the order a cold run ends in. FALSE on the streamed
        /// rebuild, which is REPRODUCING a cold run: a cold rescore appends gap-fill at the
        /// END of the list and never re-sorts, so sorting here would move those rows into
        /// EntryId order and change the buffer order Stage 7 writes its 2nd-pass sidecars
        /// in - which changes the protein-compact competition and the reported set.</param>
        private void OverlayReconciledIntoAllFiles(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries, PipelineContext ctx,
            bool canonicalize = true)
        {
            var gapFill = ctx.Get<PerFileGapFillForRescore>().Value;
            var parquetPaths = ctx.Get<PerFileParquetPaths>().Value;
            string validityKey = ValidityKey(ctx);
            // Reported for the same reason as the survivor rebuild above: a per-file parquet
            // read across the whole run, in the silent window after the parallel rescore.
            using (var progress = new ProgressReporter(string.Format(
                       @"Overlaying reconciled results from {0} file(s)", perFileEntries.Count),
                       perFileEntries.Count))
            {
                int done = 0;
                foreach (var kv in perFileEntries)
                {
                    progress.Report(++done);
                    // Overlay each file's reconciled boundaries when its
                    // .scores-reconciled.parquet is present AND CURRENT; no-work files (none on
                    // disk) keep their 1st-pass boundaries, matching a fresh run.
                    //
                    // Validity, not mere existence. The rescore's own per-file gate
                    // (TryResumeRescoredFile) asks PerFileResumeDriver.IsCurrent, so testing
                    // File.Exists here accepted a reconciled parquet this run would have
                    // REJECTED and re-scored - one left by a run with different reconciliation
                    // parameters, say. That overlays stale boundaries onto a cold run's buffer,
                    // which is worse than the no-work fallback of leaving 1st-pass values.
                    if (parquetPaths != null &&
                        parquetPaths.TryGetValue(kv.Key, out string scoresPath))
                    {
                        string reconciledPath = ParquetScoreCache.ReconciledPathFromScoresPath(scoresPath);
                        if (PerFileResumeDriver.IsCurrent(reconciledPath, Name, validityKey))
                        {
                            IReadOnlyList<GapFillTarget> gapFillForFile = null;
                            if (gapFill != null && gapFill.TryGetValue(kv.Key, out var gfList))
                                gapFillForFile = gfList;
                            OverlayReconciledIntoBuffer(kv.Value, reconciledPath, gapFillForFile);
                        }
                    }
                    // Canonical sort for EVERY file (incl. no-work files) so the WARM
                    // buffer order matches the order COLD establishes in
                    // RunPercolatorFdr, independent of whether the file was rescored.
                    if (canonicalize)
                        SortFileEntriesCanonical(kv.Value);
                    // Same release the rescore path does once a file's reconciled parquet is
                    // on disk: the overlay above re-fattened this file's entries straight
                    // from that parquet, and holding those arrays for every file is the
                    // O(files) Stage-6 growth term. A no-work file was never fattened, so
                    // this is a no-op there. Nothing downstream reads them off the buffer -
                    // SecondPassFDR's 2nd pass reloads PIN features from the reconciled parquet
                    // by identity - and this leaves the same buffer shape COLD leaves.
                    ReleaseRescoredPayload(kv.Value);
                }
            }
        }

        /// <summary>
        /// Sort one file's entry list by (EntryId, Charge, ScanNumber, ParquetIndex) --
        /// the exact order a COLD run establishes via
        /// <c>FirstPassFdrTask.RunPercolatorFdr</c> (run by SecondPassFDR's 2nd-pass,
        /// which a WARM straight-through resume skips when the <c>.2nd-pass</c> sidecars
        /// are already valid on disk). Both resume paths apply this to EVERY file's
        /// list, including no-work files with no reconciled parquet, so the WARM buffer
        /// order matches COLD regardless of whether the file was rescored -- otherwise
        /// SecondPassFDR's <c>BuildSharedBoundaries</c> could iterate a different order and,
        /// on a q-value tie between charge states of a peptide, pick a different shared
        /// (modseq, file) boundary. A no-work file already lands in this order today via
        /// the single-key compaction sort (compacted EntryIds are unique per file), but
        /// sorting unconditionally future-proofs the tie-break against any later change
        /// that retains multiple rows per EntryId.
        /// </summary>
        private static void SortFileEntriesCanonical(List<FdrEntry> fileEntries)
        {
            fileEntries.Sort(FdrEntry.CANONICAL_ORDER); // Array.Sort OK: CANONICAL_ORDER's terminal key ParquetIndex is unique per row, so the comparison never ties
        }

        /// <summary>
        /// PHASE 2 gap-fill two-pass for a single file: a CWT pass (prefilter
        /// disabled, peaks picked freely) followed by a forced-integration
        /// pass for the targets CWT missed. CWT + forced results are appended
        /// to <paramref name="fdrEntries"/> as new gap-fill stubs (ParquetIndex
        /// sentinel + score-reset, mirroring Rust to_fdr_entry semantics).
        /// Decoys are intentionally excluded from gap-fill: forcing a random
        /// decoy sequence to be scored at the target's consensus RT has no
        /// biological basis (decoys are not expected to co-elute with their
        /// paired target), and the 1st-pass parquet already has a score for
        /// every decoy at its own natural-but-best peak. Gap-filling decoys
        /// also re-scored them at consensus RT and APPENDED a second parquet
        /// row alongside the existing 1st-pass row, producing exact-duplicate
        /// rows in the reconciled parquet. Those duplicates cascaded into
        /// different max-per-modseq aggregations cross-impl and a 1.1e-4
        /// group_qvalue drift on Astral 3-file. Targets are still gap-filled
        /// because they were missing from this file by definition. Returns
        /// (CWT hits, forced integrations).
        /// </summary>
        private (int NGapCwt, int NGapForced) RunGapFillTwoPass(
            List<GapFillTarget> gapFillTargets,
            List<LibraryEntry> fullLibrary,
            IWindowSpectraProvider spectraProvider,
            List<MS1Spectrum> ms1Spectra,
            List<IsolationWindow> isolationWindows,
            RTCalibration rtCal,
            MzCalibrationResult ms2Cal,
            MzCalibrationResult ms1Cal,
            OspreyConfig fileConfig,
            string fileName,
            double? rtMadFromCalJson,
            List<FdrEntry> fdrEntries, PipelineContext ctx)
        {
            int nGapCwt = 0;
            int nGapForced = 0;

            // Both passes collect here instead of appending straight onto fdrEntries, so the
            // whole gap-fill block can be appended ONCE in ascending EntryId order (below).
            // The two passes emit in scoring order - all CWT hits, then all forced - which is
            // an order no other path can reconstruct: it depends on which targets CWT happened
            // to hit, and that is recorded nowhere on disk. The rebuild-from-disk therefore had
            // to append by ascending TargetEntryId and could not match a file that produced
            // both kinds. Every 163-file file does (e.g. 10,965 CWT + 2,361 forced), so the two
            // buffers would differ at scale while 3-file Stellar stayed green. Sorting here
            // makes the order a property of the data rather than of the scoring emit sequence,
            // which both paths can then reproduce.
            var gapFillAppended = new List<FdrEntry>();

            // Build gap-fill library subset (targets only).
            var gapFillIds = new HashSet<uint>();
            foreach (var gf in gapFillTargets)
            {
                gapFillIds.Add(gf.TargetEntryId);
            }
            var gapFillLibrary = new List<LibraryEntry>(gapFillIds.Count);
            foreach (var libEntry in fullLibrary)
            {
                if (gapFillIds.Contains(libEntry.Id))
                    gapFillLibrary.Add(libEntry);
            }

            HashSet<uint> cwtHitIds;
            if (gapFillLibrary.Count > 0)
            {
                // Pass 1: CWT pass with prefilter disabled. Clone
                // fileConfig (already a per-file clone) so the
                // disable is scoped to this CWT pass and doesn't
                // affect the forced-integration pass below.
                var cwtConfig = fileConfig.ShallowClone();
                cwtConfig.PrefilterEnabled = false;
                var cwtContext = new ScoringContext(cwtConfig, fileName);
                cwtContext.OriginalRtMad = rtMadFromCalJson;
                // No BoundaryOverrides -- CWT picks peaks freely.

                var swCwt = Stopwatch.StartNew();
                var cwtResults = ScoringTaskShared.Pipeline(ctx).RunCoelutionScoring(
                    gapFillLibrary, spectraProvider, ms1Spectra,
                    isolationWindows, rtCal,
                    ms2Cal, ms1Cal,
                    cwtContext, passLabel: "Gap-fill scoring");
                swCwt.Stop();

                cwtHitIds = new HashSet<uint>();
                foreach (var entry in cwtResults)
                    cwtHitIds.Add(entry.EntryId);
                nGapCwt = cwtResults.Count;

                // Collect the CWT results as new FdrEntry stubs with the
                // gap-fill sentinel + score-reset (mirroring Rust
                // to_fdr_entry semantics for new stubs).
                foreach (var entry in cwtResults)
                {
                    entry.ParquetIndex = uint.MaxValue;
                    entry.ResetScores();
                    gapFillAppended.Add(entry);
                }

                ctx.LogInfo(string.Format(
                    "  Gap-fill CWT: {0} hits ({1:F1}s)",
                    nGapCwt, swCwt.Elapsed.TotalSeconds));
            }
            else
            {
                cwtHitIds = new HashSet<uint>();
            }

            // Pass 2: Forced integration for targets CWT missed.
            // Decoys are intentionally excluded from gap-fill (see
            // gapFillIds build above).
            var forcedOverrides = new Dictionary<uint, (double Apex, double Start, double End)>();
            var forcedIds = new HashSet<uint>();
            foreach (var gf in gapFillTargets)
            {
                double start = gf.ExpectedRt - gf.HalfWidth;
                double end = gf.ExpectedRt + gf.HalfWidth;
                if (!cwtHitIds.Contains(gf.TargetEntryId))
                {
                    forcedOverrides[gf.TargetEntryId] = (gf.ExpectedRt, start, end);
                    forcedIds.Add(gf.TargetEntryId);
                }
            }

            if (forcedOverrides.Count > 0)
            {
                var forcedLibrary = new List<LibraryEntry>(forcedIds.Count);
                foreach (var libEntry in gapFillLibrary)
                {
                    if (forcedIds.Contains(libEntry.Id))
                        forcedLibrary.Add(libEntry);
                }

                var forcedContext = new ScoringContext(fileConfig, fileName);
                forcedContext.BoundaryOverrides = forcedOverrides;
                forcedContext.OriginalRtMad = rtMadFromCalJson;

                var swForced = Stopwatch.StartNew();
                var forcedResults = ScoringTaskShared.Pipeline(ctx).RunCoelutionScoring(
                    forcedLibrary, spectraProvider, ms1Spectra,
                    isolationWindows, rtCal,
                    ms2Cal, ms1Cal,
                    forcedContext, passLabel: "Gap-fill forced integration");
                swForced.Stop();
                nGapForced = forcedResults.Count;

                foreach (var entry in forcedResults)
                {
                    entry.ParquetIndex = uint.MaxValue;
                    entry.ResetScores();
                    gapFillAppended.Add(entry);
                }

                ctx.LogInfo(string.Format(
                    "  Gap-fill forced: {0} integrated ({1:F1}s)",
                    nGapForced, swForced.Elapsed.TotalSeconds));
            }

            // Append the whole block at the END of the buffer, in ascending EntryId - the one
            // order the rebuild-from-disk can also produce (it appends by ascending
            // TargetEntryId from the persisted target list). Appending at the end rather than
            // merging into canonical position is itself required: the Stage 6 planner's
            // positional indices address the survivors as loaded, so anything inserted before
            // them shifts every index after it.
            //
            // The CWT and forced blocks are disjoint (forced runs only for the targets CWT
            // missed), but EntryId is NOT unique within this list: neither pass runs
            // DeduplicatePairs or DeduplicateDoubleCounting, and ScoreWindow admits a candidate
            // per isolation window, so one target scored in two overlapping windows emits two
            // rows. Charge and ScanNumber separate them, and every row carries the same
            // uint.MaxValue ParquetIndex sentinel - so the comparison can only tie on rows that
            // agree on all four keys, which are indistinguishable anyway. The rebuild-from-disk
            // reproduces this by appending EVERY reconciled row for a target, in the parquet's
            // canonical (entry_id, charge, scan) order, which is the order this sort produces.
            gapFillAppended.Sort(FdrEntry.CANONICAL_ORDER); // Array.Sort OK: a tie needs equal (EntryId, Charge, ScanNumber), and such rows are indistinguishable
            fdrEntries.AddRange(gapFillAppended);

            return (nGapCwt, nGapForced);
        }

        /// <summary>
        /// Build a streaming <see cref="SpectraWindowIndex"/> over the <c>.spectra.bin</c>
        /// cache the original Stage 1-4 run wrote, so Stage-6 rescore loads each isolation
        /// window's MS2 on demand instead of materializing the whole ~6 GB resident
        /// <c>List&lt;Spectrum&gt;</c>. MS1 + the first-cycle isolation windows come from the
        /// same index. There is NO mzML fallback: Stage 6 always runs against a file the
        /// upstream stages already cached, so an absent/invalid cache is a deployment error
        /// (the mzML may not even be shipped to the rescore worker), and re-reading the 6 GB
        /// mzML would defeat the streaming this method exists to enable. Throws
        /// <see cref="InvalidDataException"/> when the cache cannot be indexed.
        /// </summary>
        private SpectraWindowIndex LoadSpectraForRescore(string inputFile, string fileName,
            PipelineContext ctx)
        {
            string cachePath = SpectraCache.GetCachePath(inputFile);
            SpectraWindowIndex index;
            try
            {
                // null = absent / stale (source changed) / bad magic-version -- the same
                // rejection rules LoadSpectraCache applies. Any of them is fatal here.
                index = SpectraWindowIndex.BuildFromCache(cachePath, inputFile);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(string.Format(
                    "Stage-6 rescore requires the '{0}' spectra cache written by the per-file " +
                    "scoring stage, but indexing it failed: {1}", cachePath, ex.Message), ex);
            }
            if (index == null)
                throw new InvalidDataException(string.Format(
                    "Stage-6 rescore requires the '{0}' spectra cache written by the per-file " +
                    "scoring stage (absent, stale, or wrong version). Re-run PerFileScoring for " +
                    "'{1}' so the cache is present beside its outputs.", cachePath, fileName));

            ctx.LogInfo(string.Format(
                "  Streaming {1} MS1 and {0} MS/MS spectra from cache for {2}",
                index.Ms2Count, index.Ms1Spectra.Count, fileName));
            return index;
        }

        /// <summary>
        /// Load MS2 + MS1 mass calibrations and the original Stage-4 RT
        /// calibration MAD from the sibling .calibration.json that
        /// Stage 2 wrote. Throws <see cref="InvalidDataException"/> if the
        /// calibration sidecar is missing or unreadable -- Stage 6
        /// requires the Stage 1-4 calibration to rescore, and silently
        /// falling back to uncalibrated would mask a real configuration
        /// error (the worker's output would diverge from the
        /// straight-through pipeline's output). Mirrors the hard-error
        /// behavior in Rust <c>run_rescore</c> at
        /// <c>osprey/crates/osprey/src/rescore.rs</c>. Individual calibration
        /// sections (Ms1Calibration / Ms2Calibration / RtMad) may still
        /// be absent within the file; those leave the corresponding
        /// out-param at its uncalibrated / null default.
        /// </summary>
        private void LoadMassCalibrations(string inputFile,
            out MzCalibrationResult ms2Cal, out MzCalibrationResult ms1Cal,
            out double? rtMadFromCalJson)
        {
            ms2Cal = MzCalibrationResult.Uncalibrated();
            ms1Cal = MzCalibrationResult.Uncalibrated();
            rtMadFromCalJson = null;

            // Stage 1-4 wrote the calibration sidecar to the configured output
            // directory (ArtifactPaths), which for a straight-through --output-dir
            // run is NOT the (possibly read-only) input mzML's directory. Resolve
            // it the same way the writer did; fall back to the input's own dir
            // (via GetFullPath so a bare-filename input still yields an absolute
            // dir) when no output dir is configured.
            string parent = !string.IsNullOrEmpty(ArtifactPaths.OutputDir)
                ? ArtifactPaths.OutputDir
                : Path.GetDirectoryName(Path.GetFullPath(inputFile));
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidDataException(string.Format(
                    "LoadMassCalibrations: cannot derive sidecar directory from input path `{0}`. " +
                    "Stage 6 needs to read the Stage 1-4 calibration sidecar; without it the " +
                    "worker would silently produce uncalibrated rescore output.", inputFile));
            }
            string calPath = CalibrationIO.CalibrationPathForInput(inputFile, parent);
            if (!File.Exists(calPath))
            {
                throw new InvalidDataException(string.Format(
                    "LoadMassCalibrations: required calibration JSON not found at `{0}` " +
                    "(input file: `{1}`). Stage 6 needs the Stage 1-4 calibration sidecar to " +
                    "rescore. Run Stages 1-4 first or fix the path.", calPath, inputFile));
            }

            CalibrationParams calParams;
            try
            {
                calParams = CalibrationIO.LoadCalibration(calPath);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(string.Format(
                    "LoadMassCalibrations: failed to read calibration JSON `{0}`: {1}. The file " +
                    "exists but could not be parsed -- check that it was written by a matching " +
                    "Osprey version.", calPath, ex.Message), ex);
            }

            if (calParams.Ms2Calibration != null && calParams.Ms2Calibration.Calibrated)
            {
                ms2Cal = new MzCalibrationResult
                {
                    Mean = calParams.Ms2Calibration.Mean,
                    Median = calParams.Ms2Calibration.Median,
                    SD = calParams.Ms2Calibration.SD,
                    Count = calParams.Ms2Calibration.Count,
                    Unit = calParams.Ms2Calibration.Unit,
                    AdjustedTolerance = calParams.Ms2Calibration.AdjustedTolerance,
                    Calibrated = true
                };
            }
            if (calParams.Ms1Calibration != null && calParams.Ms1Calibration.Calibrated)
            {
                ms1Cal = new MzCalibrationResult
                {
                    Mean = calParams.Ms1Calibration.Mean,
                    Median = calParams.Ms1Calibration.Median,
                    SD = calParams.Ms1Calibration.SD,
                    Count = calParams.Ms1Calibration.Count,
                    Unit = calParams.Ms1Calibration.Unit,
                    AdjustedTolerance = calParams.Ms1Calibration.AdjustedTolerance,
                    Calibrated = true
                };
            }
            // The MAD is what Rust's run_search uses for rt_tolerance
            // derivation; emit it from here (not from the refined cal's
            // abs_residuals) so the C# rescore matches Rust's window
            // size byte-for-byte.
            if (calParams.RtCalibration != null && calParams.RtCalibration.MAD.HasValue)
            {
                rtMadFromCalJson = calParams.RtCalibration.MAD.Value;
            }
        }
    }
}
