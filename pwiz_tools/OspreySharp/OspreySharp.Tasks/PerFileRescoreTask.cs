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
using System.Threading.Tasks;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR;
using pwiz.OspreySharp.FDR.Reconciliation;
using pwiz.OspreySharp.IO;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp.Tasks
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
    /// boundaries produced by the first-join phase, runs the gap-fill
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

        // Captured during Run so MergeNodeTask (downstream) can reach
        // the post-rescore version. Per the ownership-transfer semantics
        // of the pipeline: this task is the producer of the post-rescore
        // perFileEntries; consumers query us rather than
        // PerFileScoringTask. When Run is a no-op (no planning state)
        // the list reference falls through unchanged from
        // PerFileScoringTask.
        private List<KeyValuePair<string, List<FdrEntry>>> _perFileEntries;

        public override string Name => @"PerFileRescoring";

        /// <summary>
        /// Computes the Stage 6 rescore in straight-through, the rescore worker
        /// (--task PerFileRescoring), and the --input-scores
        /// full-pipeline. Excluded in --task PerFileScoring, --task FirstPassFDR (stops at Stage 5),
        /// and the --task SecondPassFDR merge (where it rehydrates rather than
        /// re-scoring, the merge node having no mzMLs).
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
        // overlays the Stage 6 rescore (or, in the --task SecondPassFDR merge path,
        // applies its own compaction) onto the same backing list. MergeNode
        // pulls RescoredEntries, so a cache miss lazily materializes this task --
        // which is exactly what triggers the rescore/compaction in merge mode
        // where the driver does not run this task.
        public override IEnumerable<Type> Publishes => new[] { typeof(RescoredEntries) };

        // _perFileEntries is the shared buffer this task overlays in place; it is
        // published as the RescoredEntries milestone (in Run and the merge-mode
        // Rehydrate) for MergeNode to pull via ctx.Get<RescoredEntries>().

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
            return base.ValidityKey(ctx)
                + @";reconciliation=" + ctx.Config.Identity.ReconciliationParameterHash();
        }

        public override bool Run(PipelineContext ctx)
        {
            // Compute path (Stage 6 rescore): re-score each file's entries
            // against the consensus + reconciliation boundaries and write the
            // reconciled parquets. Used by the straight-through pipeline and
            // the stage6 rescore worker. The --task SecondPassFDR merge node,
            // which has only reconciled parquets + sidecars (no mzMLs to
            // rescore from), takes Rehydrate instead: the driver reaches this
            // task here only in the rescore-capable modes, and a merge-node
            // consumer materializes it via ctx.Demand, which routes to Rehydrate.
            // CompactedEntries: the post-first-join buffer. Demanding it
            // materializes FirstJoin (running its compaction + Stage 6 planning
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
            // done upstream. State comes from either FirstJoinTask's
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
            // the strict --task SecondPassFDR merge path. Downstream MergeNodeTask
            // reads the RescoredEntries milestone of this same backing list.
            // Read the planning gate from the typed byproduct registry rather
            // than reaching for the concrete FirstJoinTask. ctx.Get lazily
            // materializes the slot's producer (FirstJoinTask) if it has not run
            // yet, so the value is always populated; FirstJoin publishes
            // PlanningPerformed alongside CompactedEntries (already read above)
            // from every materialization path.
            bool didPlan = ctx.Get<PlanningPerformed>().Value;
            var rescoreBundle = ctx.Get<RescoreBundle>().Value;
            bool anyPass2Present = false;
            if (ctx.Config.InputFiles != null)
            {
                foreach (var inputFile in ctx.Config.InputFiles)
                {
                    if (File.Exists(FdrScoresSidecar.Pass2Path(inputFile)))
                    {
                        anyPass2Present = true;
                        break;
                    }
                }
            }
            if (!didPlan && (rescoreBundle == null || anyPass2Present))
                return true;

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
                joinFileStems);
            ctx.LogInfo(string.Format(
                @"Reconciliation rescore: {0} entries re-scored ({1} reconciliation actions executed)",
                rescoreStats.TotalRescored, rescoreStats.TotalReconciliation));

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
            // that wrote those parquets. We must NOT touch FirstJoinTask here --
            // demanding it would re-run Stage 5 first-pass Percolator from
            // scratch on the reconciled parquets (producing wildly different
            // action counts than the planner saw on the raw Stage 4 inputs) and
            // then attempt a Stage 6 rescore that needs mzML files the merge
            // node does not have (in production HPC the merge node ships only
            // sidecars + reconciled parquets, no mzMLs). MergeNodeTask is
            // responsible for 2nd-pass Percolator (Bug C) and protein FDR + blib
            // output starting from this hydrated, reconciled state. Mirrors Rust
            // pipeline.rs:3313-3344 which gates the entire Stage 5+6 block on
            // `!config.expect_reconciled_input`.
            //
            // Compaction still needs to run though: PerFileScoringTask's
            // bundle-hydration path loads ALL entries from the parquet,
            // including ones that failed first-pass FDR. FirstJoinTask's normal
            // flow would run this compaction inline after first-pass Percolator
            // (and we skip FirstJoinTask entirely here). Without it,
            // MergeNodeTask's 2nd-pass Percolator would train on ~3x too many
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
            // (CanRehydrate) and a downstream task (MergeNode) is the first to
            // touch its state. A resumed Run self-gates to a no-op here --
            // FirstJoin rehydrates (so DidPlan is false) and there is no rescore
            // bundle, so ExecuteRescore never runs and the shared buffer is left
            // at its post-compaction (CompactedEntries) state; MergeNode reloads
            // the rescored features from the valid reconciled parquets on disk.
            // Reproduce exactly that end state by loading the CompactedEntries
            // milestone (which materializes FirstJoin's own pure rehydrate) and
            // publishing it as RescoredEntries -- never calling Run, so Rehydrate
            // stays pure. The --task SecondPassFDR merge path (ExpectReconciledInput)
            // below is a different rehydrate that must NOT materialize FirstJoin.
            if (!ctx.Config.ExpectReconciledInput)
            {
                _perFileEntries = ctx.Get<CompactedEntries>().Value;

                // PR-E: a fresh ExecuteRescore would overlay each file's reconciled
                // boundaries/area/features onto its CompactedEntries rows + append
                // gap-fill. On resume the driver skipped Run because the reconciled
                // parquets are already valid, so do the equivalent in-place overlay
                // from each file's OWN .scores-reconciled.parquet -- otherwise the
                // buffer stays at 1st-pass RTs and MergeNode (which reads ApexRt/
                // StartRt/EndRt/BoundsArea straight off these entries) writes 1st-pass
                // RTs into the final blib instead of the Stage 6 reconciled values.
                // Files with no reconciled sibling on disk are no-work files; a fresh
                // run leaves their entries at 1st-pass too, so they are left unchanged.
                var gapFill = ctx.Get<PerFileGapFillForRescore>().Value;
                var parquetPaths = ctx.Get<PerFileParquetPaths>().Value;
                foreach (var kv in _perFileEntries)
                {
                    // Overlay each file's reconciled boundaries when its
                    // .scores-reconciled.parquet is present; no-work files (none on
                    // disk) keep their 1st-pass boundaries, matching a fresh run.
                    if (parquetPaths != null &&
                        parquetPaths.TryGetValue(kv.Key, out string scoresPath))
                    {
                        string reconciledPath = ParquetScoreCache.ReconciledPathFromScoresPath(scoresPath);
                        if (File.Exists(reconciledPath))
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
                    SortFileEntriesCanonical(kv.Value);
                }

                ctx.Publish(new RescoredEntries(_perFileEntries));
                return true;
            }

            // ScoredEntries, NOT CompactedEntries: the merge path must NOT
            // materialize FirstJoin (that would re-run Stage 5 Percolator on the
            // reconciled parquets); it applies its own compaction below. Reading
            // the pre-compaction milestone keeps the dependency on PerFileScoring
            // alone, mirroring the old explicit Demand<PerFileScoringTask>.
            _perFileEntries = ctx.Get<ScoredEntries>().Value;

            // Publish the RescoredEntries milestone over the shared backing list
            // (the merge path applies its own compaction below, in place).
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
                // even at the ULP level). Without this matching recompute on the
                // C# side, the protein-rescue branch of compaction below sees
                // slightly stale RunProteinQvalue values and the post-compaction
                // detected_peptides set diverges from Rust by ~19 peptides on
                // Stellar Single (1 protein delta at Stage 7). Only runs when
                // protein FDR is enabled. Mirrors Rust pipeline.rs:4292-4358.
                if (ctx.Config.ProteinFdr.HasValue && bundle.PerFileEntries.Count > 0)
                {
                    var fullLibrary = ctx.Get<FullLibrary>().Value;
                    // Silent (logInfo: null) -- the rehydration recompute runs
                    // before compaction with no log output, as it did when it
                    // called ProteinFdr.RunFirstPassProteinFdr directly.
                    ProteinFdrEngine.RunFirstPass(
                        bundle.PerFileEntries, fullLibrary, ctx.Config, null);
                }
                var stats = RescoreCompaction.Apply(bundle, ctx.Config);
                ctx.LogInfo(string.Format(
                    @"--task SecondPassFDR compaction: {0} -> {1} entries ({2} passing base_ids; {3} action(s) dropped)",
                    stats.EntriesBefore, stats.EntriesAfter,
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
        ///   <item>Reload spectra from the .spectra.bin cache or the mzML.</item>
        ///   <item>Reload MS2/MS1 mass calibration from the sibling .calibration.json.</item>
        ///   <item>Pick the refined RT calibration when present, else fall back to
        ///       the original first-pass calibration.</item>
        ///   <item>Call <see cref="ScoringPipeline.RunCoelutionScoring"/> with the override-aware
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
            IReadOnlyList<string> joinFileStems = null)
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
            var counts = new (int Rescored, int GapCwt, int GapForced)[nTotalFiles];

            if (nTotalFiles == 1 || parallelism == 1)
            {
                for (int fileNum = 0; fileNum < nTotalFiles; fileNum++)
                    counts[fileNum] = RescoreOneFile(
                        fileNum, nTotalFiles,
                        perFileEntries[fileNum].Key, perFileEntries[fileNum].Value,
                        inputs, ctx);
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
                    using (multi.BeginFile(fileNum, perFileEntries[fileNum].Key, RESCORE_FILE_SEGMENTS))
                        counts[fileNum] = RescoreOneFile(
                            fileNum, nTotalFiles,
                            perFileEntries[fileNum].Key, perFileEntries[fileNum].Value,
                            inputs, ctx);
                });
            }

            int totalRescored = 0;
            int totalGapCwt = 0;
            int totalGapForced = 0;
            foreach (var c in counts)
            {
                totalRescored += c.Rescored;
                totalGapCwt += c.GapCwt;
                totalGapForced += c.GapForced;
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
        /// Run the Stage 6 rescore for a single file: resume-skip check, target
        /// assembly, subset scoring + overlay, gap-fill, and the reconciled
        /// parquet write-back. The per-file <paramref name="fdrEntries"/> buffer
        /// is updated in place. Returns the per-file (rescored, gap-CWT,
        /// gap-forced) counts the caller accumulates. The scoring orchestration
        /// is kept whole here (parity-locked); only the cross-file plumbing was
        /// lifted up into <see cref="ExecuteRescore"/>.
        /// </summary>
        private (int Rescored, int GapCwt, int GapForced) RescoreOneFile(
            int fileNum, int nTotalFiles, string fileName, List<FdrEntry> fdrEntries,
            RescorePassInputs inputs, PipelineContext ctx)
        {
            int totalRescored = 0;
            int totalGapCwt = 0;
            int totalGapForced = 0;

            // Per-file resume: a file whose reconciled parquet is already on
            // disk with a matching sidecar is overlaid in place and skipped.
            if (TryResumeRescoredFile(fileNum, nTotalFiles, fileName, fdrEntries, inputs, ctx))
                return (totalRescored, totalGapCwt, totalGapForced);

            // Assemble this file's rescore targets (multi-charge consensus +
            // reconciliation dedup + gap-fill) and resolve its input mzML.
            // Bails when there is no work or the file has no input_files entry.
            if (!TryAssembleRescoreTargets(fileNum, nTotalFiles, fileName, inputs, ctx,
                    out var combinedTargets, out var gapFillTargets, out string inputFile))
                return (totalRescored, totalGapCwt, totalGapForced);

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

            // Segment 1/3 (read): reload this file's spectra -- the file's first
            // progress slice on the --parallel-files "[i] p%" aggregate line.
            MultiProgressReporter.Current?.BeginSegment();
            // Load spectra: prefer the .spectra.bin cache the original
            // Stage 1 wrote; fall back to mzML if the cache is missing
            // or unreadable.
            List<Spectrum> spectra;
            List<MS1Spectrum> ms1Spectra;
            LoadSpectraForRescore(inputFile, fileName, out spectra, out ms1Spectra, ctx);

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

            // Build isolation windows from the loaded spectra (same as
            // the first-pass ProcessFile path).
            var isolationWindows = ScoringTaskShared.ExtractIsolationWindows(spectra);

            // Segment 2/3 (score): the subset re-score; its "Re-scoring isolation
            // windows" reporter feeds this slice (the bulk of the file's motion).
            MultiProgressReporter.Current?.BeginSegment();
            // Re-score the subset.
            var swRescore = Stopwatch.StartNew();
            List<FdrEntry> rescored;
            if (subsetLibrary.Count > 0)
            {
                rescored = ScoringTaskShared.Pipeline(ctx).RunCoelutionScoring(
                    subsetLibrary, spectra, ms1Spectra,
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
                    gapFillTargets, fullLibrary, spectra, ms1Spectra,
                    isolationWindows, rtCal, ms2Cal, ms1Cal,
                    fileConfig, fileName, rtMadFromCalJson, fdrEntries, ctx);
                totalGapCwt += nGapCwt;
                totalGapForced += nGapForced;
                totalRescored += nGapCwt + nGapForced;
            }

            // Segment 3/3 (write): the reconciled parquet write-back.
            MultiProgressReporter.Current?.BeginSegment();
            // PHASE 3 -- reconciled parquet write-back + sidecar stamp.
            WriteReconciledAndStamp(fileName, inputFile, fdrEntries, inputs, ctx);

            return (totalRescored, totalGapCwt, totalGapForced);
        }

        /// <summary>
        /// Per-file resume probe. When the file's reconciled parquet is already
        /// on disk with a matching
        /// <c>&lt;output&gt;.PerFileRescoring.osprey.task</c> sidecar, overlays
        /// the reconciled values back onto the in-memory entries (a partial
        /// resume must not leave 1st-pass RTs in the buffer a downstream
        /// MergeNode reads) and returns true so the caller skips re-scoring.
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
                // a downstream consumer (MergeNode, in the full pipeline) reads
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
        /// </summary>
        private void WriteReconciledAndStamp(
            string fileName, string inputFile, List<FdrEntry> fdrEntries,
            RescorePassInputs inputs, PipelineContext ctx)
        {
            var config = inputs.Config;
            // ParquetPaths is non-null here (dereferenced at the resume probe).
            if (inputs.ParquetPaths.TryGetValue(fileName, out string parquetPath) &&
                File.Exists(parquetPath))
            {
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
                }
                else
                {
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
                }
            }
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
        /// <see cref="ScoringPipeline.RunCoelutionScoring"/> so it
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
        /// Features. Without this reset the OspreySharp ScoreCandidate's
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
                    rescoredEntry.Score = 0.0;
                    rescoredEntry.RunPrecursorQvalue = 1.0;
                    rescoredEntry.RunPeptideQvalue = 1.0;
                    rescoredEntry.RunProteinQvalue = 1.0;
                    rescoredEntry.ExperimentPrecursorQvalue = 1.0;
                    rescoredEntry.ExperimentPeptideQvalue = 1.0;
                    rescoredEntry.ExperimentProteinQvalue = 1.0;
                    rescoredEntry.Pep = 1.0;
                    rescoredEntry.ParquetIndex = fdrEntries[idx].ParquetIndex;
                    fdrEntries[idx] = rescoredEntry;
                    nOverlay++;
                }
                else
                {
                    // No peak at the override boundary -- reset to
                    // defaults in place to match Rust's behavior.
                    var existing = fdrEntries[idx];
                    existing.Score = 0.0;
                    existing.RunPrecursorQvalue = 1.0;
                    existing.RunPeptideQvalue = 1.0;
                    existing.RunProteinQvalue = 1.0;
                    existing.ExperimentPrecursorQvalue = 1.0;
                    existing.ExperimentPeptideQvalue = 1.0;
                    existing.ExperimentProteinQvalue = 1.0;
                    existing.Pep = 1.0;
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
        /// engine would be both wasteful and (on a merge node) impossible. Both
        /// resume paths -- the straight-through <see cref="Rehydrate"/> no-op and
        /// the per-file skip inside <see cref="ExecuteRescore"/> -- previously
        /// left the buffer at its 1st-pass <see cref="CompactedEntries"/> state,
        /// so MergeNode (which reads ApexRt/StartRt/EndRt/BoundsArea DIRECTLY off
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
        private void OverlayReconciledIntoBuffer(List<FdrEntry> fileEntries,
            string reconciledPath, IReadOnlyList<GapFillTarget> gapFillForFile)
        {
            List<FdrEntry> loaded;
            try
            {
                loaded = ParquetScoreCache.LoadFullFdrEntries(reconciledPath);
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

            // Index reconciled rows by EntryId. The reconciled parquet carries the
            // full original entry set (incl. non-passing rows) re-sorted by
            // (entry_id, charge, scan) plus appended gap-fill; a given EntryId can
            // appear more than once (multiple charges / scans). Keep the FIRST row
            // for a given EntryId -- the existing OverlayRescoredEntries path also
            // matches a single rescored entry per EntryId, and the buffer carries
            // at most one row per EntryId after compaction.
            var byId = new Dictionary<uint, FdrEntry>(loaded.Count);
            foreach (var r in loaded)
            {
                if (!byId.ContainsKey(r.EntryId))
                    byId[r.EntryId] = r;
            }

            // Overlay reconciled boundary / area / feature / blob columns onto the
            // existing buffer rows IN PLACE, preserving each row's ParquetIndex and
            // 1st-pass Score / q-values (FdrEntry is a reference type, so mutating
            // fields updates the shared list element directly).
            var existingIds = new HashSet<uint>();
            foreach (var entry in fileEntries)
            {
                existingIds.Add(entry.EntryId);
                if (!byId.TryGetValue(entry.EntryId, out FdrEntry r))
                    continue;
                entry.ApexRt = r.ApexRt;
                entry.StartRt = r.StartRt;
                entry.EndRt = r.EndRt;
                entry.BoundsArea = r.BoundsArea;
                entry.BoundsSnr = r.BoundsSnr;
                entry.Features = r.Features;
                entry.CwtCandidates = r.CwtCandidates;
                entry.FragmentMzs = r.FragmentMzs;
                entry.FragmentIntensities = r.FragmentIntensities;
                entry.ReferenceXicRts = r.ReferenceXicRts;
                entry.ReferenceXicIntensities = r.ReferenceXicIntensities;
            }

            // Append gap-fill rows. A fresh run appends one stub per gap-fill
            // target (decoys already excluded by the planner) with ParquetIndex =
            // uint.MaxValue. Pull the reconciled row for each target EntryId that
            // is not already in the buffer; append in ascending TargetEntryId order
            // for determinism. Targets whose reconciled row is missing (no peak)
            // are skipped -- a fresh run would not have appended a stub either.
            if (gapFillForFile != null && gapFillForFile.Count > 0)
            {
                var gapFillIds = new SortedSet<uint>();
                foreach (var t in gapFillForFile)
                    gapFillIds.Add(t.TargetEntryId);
                foreach (var gid in gapFillIds)
                {
                    if (existingIds.Contains(gid))
                        continue;
                    if (!byId.TryGetValue(gid, out FdrEntry g))
                        continue;
                    g.ParquetIndex = uint.MaxValue;
                    fileEntries.Add(g);
                    existingIds.Add(gid);
                }
            }

            // The canonical (EntryId, Charge, ScanNumber, ParquetIndex) re-sort is
            // applied by the CALLER via SortFileEntriesCanonical -- it runs for every
            // file in the resume path, not only files with a reconciled parquet.
        }

        /// <summary>
        /// Sort one file's entry list by (EntryId, Charge, ScanNumber, ParquetIndex) --
        /// the exact order a COLD run establishes via
        /// <see cref="FirstJoinTask.RunPercolatorFdr"/> (run by MergeNode's 2nd-pass,
        /// which a WARM straight-through resume skips when the <c>.2nd-pass</c> sidecars
        /// are already valid on disk). Both resume paths apply this to EVERY file's
        /// list, including no-work files with no reconciled parquet, so the WARM buffer
        /// order matches COLD regardless of whether the file was rescored -- otherwise
        /// MergeNode's <c>BuildSharedBoundaries</c> could iterate a different order and,
        /// on a q-value tie between charge states of a peptide, pick a different shared
        /// (modseq, file) boundary. A no-work file already lands in this order today via
        /// the single-key compaction sort (compacted EntryIds are unique per file), but
        /// sorting unconditionally future-proofs the tie-break against any later change
        /// that retains multiple rows per EntryId.
        /// </summary>
        private static void SortFileEntriesCanonical(List<FdrEntry> fileEntries)
        {
            fileEntries.Sort((a, b) =>
            {
                int c = a.EntryId.CompareTo(b.EntryId);
                if (c != 0) return c;
                c = a.Charge.CompareTo(b.Charge);
                if (c != 0) return c;
                c = a.ScanNumber.CompareTo(b.ScanNumber);
                if (c != 0) return c;
                return a.ParquetIndex.CompareTo(b.ParquetIndex);
            });
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
            List<Spectrum> spectra,
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
                    gapFillLibrary, spectra, ms1Spectra,
                    isolationWindows, rtCal,
                    ms2Cal, ms1Cal,
                    cwtContext, passLabel: "Gap-fill scoring");
                swCwt.Stop();

                cwtHitIds = new HashSet<uint>();
                foreach (var entry in cwtResults)
                    cwtHitIds.Add(entry.EntryId);
                nGapCwt = cwtResults.Count;

                // Append CWT results as new FdrEntry stubs with the
                // gap-fill sentinel + score-reset (mirroring Rust
                // to_fdr_entry semantics for new stubs).
                foreach (var entry in cwtResults)
                {
                    entry.ParquetIndex = uint.MaxValue;
                    entry.Score = 0.0;
                    entry.RunPrecursorQvalue = 1.0;
                    entry.RunPeptideQvalue = 1.0;
                    entry.RunProteinQvalue = 1.0;
                    entry.ExperimentPrecursorQvalue = 1.0;
                    entry.ExperimentPeptideQvalue = 1.0;
                    entry.ExperimentProteinQvalue = 1.0;
                    entry.Pep = 1.0;
                    fdrEntries.Add(entry);
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
                    forcedLibrary, spectra, ms1Spectra,
                    isolationWindows, rtCal,
                    ms2Cal, ms1Cal,
                    forcedContext, passLabel: "Gap-fill forced integration");
                swForced.Stop();
                nGapForced = forcedResults.Count;

                foreach (var entry in forcedResults)
                {
                    entry.ParquetIndex = uint.MaxValue;
                    entry.Score = 0.0;
                    entry.RunPrecursorQvalue = 1.0;
                    entry.RunPeptideQvalue = 1.0;
                    entry.RunProteinQvalue = 1.0;
                    entry.ExperimentPrecursorQvalue = 1.0;
                    entry.ExperimentPeptideQvalue = 1.0;
                    entry.ExperimentProteinQvalue = 1.0;
                    entry.Pep = 1.0;
                    fdrEntries.Add(entry);
                }

                ctx.LogInfo(string.Format(
                    "  Gap-fill forced: {0} integrated ({1:F1}s)",
                    nGapForced, swForced.Elapsed.TotalSeconds));
            }

            return (nGapCwt, nGapForced);
        }

        /// <summary>
        /// Load MS2 spectra + MS1 spectra for the rescore loop. Prefers the
        /// .spectra.bin cache the original Stage 1 wrote; falls back to
        /// re-parsing the mzML on cache miss / read error. Mirrors the
        /// Rust spectra-load block at pipeline.rs:2851-2872.
        /// </summary>
        private void LoadSpectraForRescore(string inputFile, string fileName,
            out List<Spectrum> spectra, out List<MS1Spectrum> ms1Spectra, PipelineContext ctx)
        {
            string cachePath = SpectraCache.GetCachePath(inputFile);
            if (File.Exists(cachePath))
            {
                try
                {
                    var result = SpectraCache.LoadSpectraCache(cachePath, inputFile);
                    spectra = result.Ms2Spectra;
                    ms1Spectra = result.Ms1Spectra;
                    ctx.LogInfo(string.Format(
                        "  Loaded {1} MS1 and {0} MS/MS spectra from cache for {2}",
                        spectra.Count, ms1Spectra.Count, fileName));
                    return;
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        "Failed to load spectra cache {0}: {1}; falling back to mzML",
                        cachePath, ex.Message));
                }
            }
            var fresh = MzmlReader.LoadAllSpectra(inputFile);
            spectra = fresh.Ms2Spectra;
            ms1Spectra = fresh.Ms1Spectra;
            ctx.LogInfo(string.Format(
                "  Loaded {1} MS1 and {0} MS/MS spectra from mzML for {2}",
                spectra.Count, ms1Spectra.Count, fileName));
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
                    "OspreySharp version.", calPath, ex.Message), ex);
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
