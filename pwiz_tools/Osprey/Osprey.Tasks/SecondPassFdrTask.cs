/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks.ModelDiagnostics;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Final SecondPassFDR phase of the Osprey pipeline (Stage 7 in the
    /// HPC-boundary view from <c>Osprey-workflow.html</c>): persists the
    /// per-file 2nd-pass FDR-score sidecars, runs run-wide protein FDR
    /// (parsimony + picked-protein TDC), and writes the BiblioSpecLite
    /// <c>.blib</c> output. Invoked once per pipeline run on the SecondPassFDR
    /// node — no per-file fan-out beyond the sidecar write loop.
    ///
    /// All three substeps (the 2nd-pass FDR sidecar block, RunProteinFdr,
    /// and WriteBlibOutput) live in this file; nothing on AnalysisPipeline
    /// is needed for the SecondPassFDR phase.
    /// </summary>
    internal sealed class SecondPassFdrTask : OspreyTask
    {
        public override string Name => @"SecondPassFDR";

        /// <summary>
        /// Computes Stage 7-8 (2nd-pass FDR + protein FDR + blib) in
        /// straight-through, the --task SecondPassFDR stage, and the --input-scores
        /// full-pipeline. Excluded in --task PerFileScoring, --task FirstPassFDR,
        /// and --task PerFileRescoring (all of which stop before SecondPassFDR).
        /// </summary>
        public override bool IsIncluded(PipelineContext ctx)
        {
            var c = ctx.Config;
            bool inputs = c.InputScores != null && c.InputScores.Count > 0;
            return (!inputs && !c.NoJoin)
                || (inputs && c.ExpectReconciledInput)
                || (inputs && !c.NoJoin && !c.StopAfterStage5 && !c.ExpectReconciledInput);
        }

        // Phase B resume surface. Reads each file's reconciled
        // .scores.parquet, writes the .2nd-pass.fdr_scores.bin
        // sidecars (whenever Stage 6 rescored -- see AnyReconciledParquet) and the
        // .blib output. ValidityKey adds the reconciliation hash
        // because the reconciled parquet is read.
        public override IEnumerable<string> Inputs(PipelineContext ctx)
        {
            if (ctx.Config.InputFiles == null) yield break;
            // Stage 7 reads the reconciled parquet when Stage 6 produced one,
            // else the original Stage 4 parquet (no-work files). Recorded for
            // provenance only -- the driver validates tasks by output sidecar
            // key, never by re-checking Inputs() existence (TaskValiditySidecar).
            foreach (var input in ctx.Config.InputFiles)
                yield return ParquetScoreCache.EffectiveScoresPathFromScoresPath(
                    ParquetScoreCache.GetScoresPath(input));
        }

        public override IEnumerable<string> Outputs(PipelineContext ctx)
        {
            // A diagnostics-only regeneration declares NOTHING. Any declared output would let
            // the driver skip this task the moment that file exists - and the report existing
            // is exactly the case the caller is asking to redo. CanRehydrate returns false on an
            // empty output list, so zero outputs is what makes "regenerate on demand" mean it.
            // It also keeps the task from requiring the .blib and 2nd-pass sidecars it
            // deliberately leaves untouched, which would otherwise fail on a directory that no
            // longer has them.
            if (ctx.Config.DiagnosticsOnly)
                yield break;
            if (!string.IsNullOrEmpty(ctx.Config.OutputBlib))
                yield return ctx.Config.OutputBlib;
            // The --model-diagnostics report is an output of THIS task (it is finalized in
            // WritePass2AndFinalize), so declare it and let ordinary task validity regenerate
            // it. Task validity requires every declared output to exist, so a deleted or
            // renamed report invalidates this task alone - Stages 1-5 stay cached and the
            // pass-1 panel is rebuilt by rehydrating the 1st-pass sidecars, the same path
            // regression mode 5 already covers.
            //
            // CONDITIONAL ON THE FLAG, deliberately. Declaring it unconditionally would make
            // every run that never asked for diagnostics permanently invalid, re-running
            // SecondPassFDR forever.
            //
            // Without this the flag was inert on a completed directory: --model-diagnostics is
            // in no validity key and the HTML was in no Outputs list, so adding it to a re-run
            // changed nothing, every task reported "outputs valid", and no report was produced.
            //
            // AND conditional on FirstPassFDR being in this graph. WritePass2AndFinalize needs
            // the pass-1 .data.json hand-off sidecar, which only FirstPassFdrTask writes - so
            // under `--task SecondPassFDR --model-diagnostics` no report can ever be produced.
            // Declaring it there recreated the very loop the paragraph above set out to avoid,
            // one step later: CanRehydrate requires every declared output to exist, so the task
            // was never skippable and every invocation re-ran pass-2 Percolator, protein FDR and
            // the whole .blib write, still producing no report.
            if (ctx.Config.ModelDiagnostics && FirstPassFdrTask.IsIncludedFor(ctx.Config))
                yield return ModelDiagnosticsReport.ReportPath(ctx.Config);

            // 2nd-pass FDR sidecars are written whenever Stage 6 rescored entries
            // (independent of protein FDR -- the second Percolator pass runs on the
            // reconciled features), so declare them on that same condition.
            if (ctx.Config.InputFiles != null && AnyReconciledParquet(ctx.Config))
            {
                foreach (var input in ctx.Config.InputFiles)
                    yield return FdrScoresSidecar.Pass2Path(input);
            }
        }

        public override string ValidityKey(PipelineContext ctx)
        {
            // The experiment-wide aggregation reaches this task's outputs (the .blib and the
            // 2nd-pass FDR sidecars) through the experiment q it changes upstream, so it has to
            // invalidate them too. Without it, flipping the arm re-ran Stage 5 - FirstPassFdrTask
            // did carry the suffix - while THIS task's .blib and sidecars were reused from the
            // other arm, leaving one output directory holding two arms' results.
            // The 2nd-pass mode decides the q this task writes into the .blib and the 2nd-pass
            // sidecars, so it invalidates them by exactly the argument the aggregation suffix
            // makes above - one arm's .blib must never be reused as another's.
            // And the sidecar format version, for the same reason FirstPassFdrTask carries it:
            // this task writes the 2nd-pass sidecars, so a record-layout change invalidates them.
            // And the MEANING of the 2nd-pass sidecar's protein column, which issue #4559
            // changed from a pass-1 to a pass-2 value without moving a byte. The format version
            // cannot carry that: no offset, width or type changed, so a v4 record written before
            // #4559 is structurally valid and silently holds the wrong pass. Without this token
            // a post-#4559 build resuming into a pre-#4559 output directory finds every declared
            // output present and the validity key unchanged, skips this task entirely, and keeps
            // the stale column - which then reds regression mode 1c against a build that is in
            // fact correct. A key token forces the regeneration a version bump would have forced,
            // without breaking any reader or moving a golden.
            return base.ValidityKey(ctx)
                + @";fdrsidecar=" + FdrScoresSidecar.FormatVersion
                + @";pass2proteinq=2"
                + @";reconciliation=" + ctx.Config.Identity.ReconciliationParameterHash()
                + OspreyEnvironment.ExperimentAggValidityKeySuffix()
                + OspreyEnvironment.Pass2QValueValidityKeySuffix()
                + OspreyEnvironment.TrainSampleValidityKeySuffix()
                + LibraryFragmentRelease.ValidityKeySuffix(ctx);
        }

        /// <summary>
        /// No-op disk-load: SecondPassFDR is the terminal aggregator. Its output
        /// (the .blib + 2nd-pass FDR sidecars) is an external artifact that no
        /// other task consumes in-memory, so there is no cross-task state to
        /// rehydrate and nothing ever <see cref="PipelineContext.Demand{T}"/>s
        /// this task. The driver runs <see cref="Run"/> directly when the
        /// output is absent and skips it (resume) when the output is already
        /// valid; this override exists only to keep the contract satisfied once
        /// the transitional base Rehydrate=Run shim is removed (Phase B6).
        /// </summary>
        public override bool Rehydrate(PipelineContext ctx) => true;

        public override bool Run(PipelineContext ctx)
        {
            // Mid-Run crash safety: see FirstPassFdrTask.Run for rationale.
            foreach (var output in Outputs(ctx))
                TaskValiditySidecar.Delete(output, Name);
            var config = ctx.Config;
            // RescoredEntries is the final milestone of the shared buffer:
            // demanding it materializes PerFileRescore (running its rescore /
            // reconciled-input compaction when the driver skipped it), which is what
            // produces the post-rescore version this stage reads.
            //
            // Reading .Value on that milestone is also what BUILDS the global survivor pool
            // after a streamed rescore (issue #4597): PerFileRescore leaves it deferred,
            // because re-reading every file's artifacts is whole-run join work and that task
            // is a per-file HPC exit point. Stage 7 is the stage that needs a global pool -
            // the protein-compact competition is over a global stratum - so Stage 7 is where
            // the work lands, and a worker that never reaches this line never pays it.
            // Taken as a TOKEN first, so the probes below can bracket that build.
            var rescored = ctx.Get<RescoredEntries>();

            // Stage 7's INHERITED baseline, post-GC, before this stage does any work
            // (#4486). Every figure that issue has ever quoted came from --memstamp, i.e.
            // GC.GetTotalMemory(false), which includes uncollected garbage and so cannot
            // tell a live survivor pool from Server-GC committed-but-free gray - which is
            // precisely the question.
            //
            // BEFORE the .Value read, deliberately. #4597 moved the pool build onto that
            // read, and this probe exists to separate what Stage 7 inherits from what it
            // allocates - so taking it after the build would fold the build's own transients
            // (each file's pre-filter stub superset, the overlay's loaded list and maps) into
            // the "inherited" number and break comparability with the whole #4486 series.
            // The stage7-pool probe below is the one that measures the build.
            int nFiles = ctx.Config.InputFiles?.Count ?? 0;
            string memDetail = string.Format(@"(files={0})", nFiles);
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"stage7 start (pre-GC)");
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"stage7-inherited",
                string.Format(@"(post-GC, entering Stage 7, files={0})", nFiles));

            var perFileEntries = rescored.Value;
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"stage7-pool",
                string.Format(@"(post-GC, survivor pool built, files={0})", perFileEntries.Count));
            var fullLibrary = ctx.Get<FullLibrary>().Value;
            var libraryById = ctx.Get<LibraryById>().Value;
            var perFileParquetPaths = ctx.Get<PerFileParquetPaths>().Value;

            // Before the fragment release, which mutates the library entries this write
            // reads its sequence / precursor m/z / protein-id columns from.
            if (UpgradeReconciledParquets(perFileEntries, perFileParquetPaths, libraryById, ctx))
                return StopAfterUpgrade(ctx);

            ReleaseUnscorableLibraryFragments(rescored, perFileEntries.Count, fullLibrary, ctx);

            // The 2nd-pass Percolator model, captured for the model-diagnostics
            // pass-2 model view; null when no reconciliation rescore happened.
            FeatureContributions pass2Contributions = null;

            // Second-pass Percolator FDR. Runs whenever Stage 6 reconciliation /
            // multi-charge consensus / gap-fill rescored entries -- the C# analog of
            // Rust's `total_rescored > 0` gate (pipeline.rs:5209) -- INDEPENDENT of
            // protein FDR. A reconciled parquet exists for a file iff that file had
            // rescore work, so "any reconciled parquet on disk" == total_rescored > 0,
            // and the test holds in both the straight-through pipeline (Stage 6 just
            // wrote them) and the --task SecondPassFDR run (the Stage 6 worker wrote
            // them). Previously this was wrongly nested inside the ProteinFdr.HasValue
            // block, so a run without --protein-fdr wrote the blib from stale
            // first-pass (pre-reconciliation) scores. ComputeAndPersist reloads the
            // reconciled features, reruns Percolator, writes the .2nd-pass sidecars,
            // and reloads them onto the stubs so downstream protein FDR + blib see the
            // 2nd-pass q-values.
            if (AnyReconciledParquet(config))
            {
                pass2Contributions = Pass2FdrSidecar.ComputeAndPersist(
                    ctx, rescored, perFileEntries, perFileParquetPaths,
                    Name, ValidityKey(ctx));
                // The substep the 2026-07-31 characterization on #4486 located the churn in:
                // it reloads every file's reconciled features, so the pre-GC line carries the
                // transient reload peak and the post-GC line what survives it.
                ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"stage7 pass-2 scored (pre-GC)");
                ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"stage7-pass2-scored",
                    memDetail);
            }

            // Protein-level FDR. Always runs (parsimony + picked-protein at the
            // config.RunFdr Savitski gate), matching Rust's unconditional second-pass
            // protein-FDR block (pipeline.rs:5293). --protein-fdr only sets the
            // threshold used for the passing-group count and --fdr-level protein output
            // filtering; the machinery is not optional (EffectiveProteinFdr defaults to
            // 0.01). It consumes the 2nd-pass q-values above when they were recomputed,
            // else the standing first-pass scores.
            ctx.LogInfo(string.Empty);
            ctx.LogInfo(string.Format(@"Running protein-level FDR at {0:P1}...",
                config.EffectiveProteinFdr));
            var swProtein = Stopwatch.StartNew();
            RunProteinFdr(rescored, perFileParquetPaths, fullLibrary, config, ctx);
            swProtein.Stop();
            ctx.LogInfo(string.Format(@"[STAGE-WALL] stage7: {0:F1}s",
                swProtein.Elapsed.TotalSeconds));
            // Parsimony + picked-protein TDC are genuinely whole-run, so this probe is what
            // decides whether they are a REASON Stage 7 must hold every file at once or
            // merely a consumer of a pool held for other reasons (#4486). The pre-GC line is
            // not optional here: parsimony builds whole-run scratch (best score per peptide,
            // protein groups, the target/decoy competition) and the forced collection below
            // destroys it, so without this the substep reports a ~0 delta and gets written
            // off as a consumer even if it transiently doubled the heap.
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"stage7 protein FDR (pre-GC)");
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"stage7-protein-fdr",
                memDetail);

            // Re-clamp experiment q to each entry's best run q on the FINAL post-Stage-6
            // pool. The pass-1 (and any pass-2) Percolator already clamped, but Stage 6
            // reconciliation zeroes the run q of moved peaks AFTER that clamp, so a precursor
            // whose only run-passing observation was relocated can otherwise keep a stale low
            // experiment q with no surviving run support -- reported with no run-level ID (the
            // blib ID-line artifact). Re-clamping here, against the run q's actually written to
            // the blib, restores "reported => some run genuinely passed" for the final output.
            PercolatorEngine.ClampExperimentQToBestRun(perFileEntries);

            // Write output blib - unless this is a diagnostics-only regeneration, whose whole
            // contract is that it touches no artifact but the report.
            ctx.LogInfo(string.Empty);
            var swBlib = Stopwatch.StartNew();
            if (config.DiagnosticsOnly)
            {
                ctx.LogInfo(@"--task ModelDiagnostics: skipping the .blib write (report only).");
            }
            else
            {
                WriteBlibOutput(rescored, nFiles, fullLibrary, libraryById, config, ctx);
            }
            swBlib.Stop();
            // Only when a blib was actually written. [STAGE-WALL] is machine-read by the perf
            // tooling, so emitting it for a skipped write reports a real 0.0s blib stage and
            // drags the recorded cost toward zero whenever a regeneration is scraped alongside
            // real runs. The "skipping the .blib write" line above is prose that tooling does
            // not parse.
            if (!config.DiagnosticsOnly)
            {
                ctx.LogInfo(string.Format(@"[STAGE-WALL] blib: {0:F1}s",
                    swBlib.Elapsed.TotalSeconds));
            }
            // The blib write builds several whole-run indexes over the pool (passing
            // precursors, best-per-precursor, shared boundaries, cross-file observations),
            // so it is the other candidate reason the pool cannot be consumed per file.
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"stage7 blib written (pre-GC)");
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"stage7-blib-written",
                memDetail);

            // FDRBench input TSV (pass 2): the peptides we report - the final merged/rescored set
            // written to the output - each with its final second-pass q-value and raw SVM
            // discriminant, so FDRBench can evaluate the FDR/FDP of what Osprey actually outputs.
            // (The blib writer only persists a 0.0 placeholder discriminant, so this is the only
            // path to a usable FDRBench score.) Pass 1 (the full pre-compaction first-pass pool)
            // is emitted earlier, in FirstPassFdrTask before compaction; --fdrbench-pass selects one
            // or both (both writes .pass1/.pass2-suffixed files).
            var benchPath = FdrBenchInputWriter.PathForPass(config, OspreyConfig.FDRBENCH_PASS_2);
            if (benchPath != null)
            {
                var swFdrBench = Stopwatch.StartNew();
                var pairing = EntrapmentPairing.Build(libraryById, config.DecoyPairingManifestPath);
                var benchResult = FdrBenchInputWriter.WritePeptideInput(
                    benchPath, perFileEntries, libraryById, config.FdrLevel,
                    config.FdrBenchPerRun, pairing.ExcludedEntrapment);
                // Emit the corrected pairing manifest from the same library so FDRBench
                // classifies every reported peptide and drops nothing (feed FDRBench -pep with this).
                string manifestPath = benchPath + @".pairing.tsv";
                int manifestRows = FdrBenchInputWriter.WritePairingManifest(manifestPath, libraryById, pairing);
                swFdrBench.Stop();
                ctx.LogInfo(string.Format(@"Wrote FDRBench input (pass 2, {0}) to {1}: {2} rows",
                    config.FdrBenchPerRun ? @"per-run" : @"per-precursor",
                    benchPath, benchResult.Rows));
                ctx.LogInfo(string.Format(@"Wrote FDRBench pairing manifest (from the searched library) to {0}: {1} peptides",
                    manifestPath, manifestRows));
                pairing.LogSummary(ctx.LogInfo);
                if (benchResult.MissingLibrary > 0)
                    ctx.LogInfo(string.Format(
                        @"{0} FDRBench rows had no library entry; peptide and protein columns left blank",
                        benchResult.MissingLibrary));
                if (benchResult.TruncatedProtein > 0)
                    ctx.LogInfo(string.Format(
                        @"{0} FDRBench rows had oversize protein-ID lists; truncated with ';...+N_more'",
                        benchResult.TruncatedProtein));
                ctx.LogInfo(string.Format(@"[STAGE-WALL] fdrbench: {0:F1}s",
                    swFdrBench.Elapsed.TotalSeconds));
            }

            // --model-diagnostics: append the pass-2 (final reported pool) FDR
            // calibration views to the page FirstPassFdrTask wrote for pass 1, from
            // this post-compaction, second-pass-q-valued pool -- the same
            // RescoredEntries the pass-2 FDRBench TSV is written from. Opt-in and
            // off the default output path; a failure is logged and swallowed.
            // The protein-compact stratum splits the pass-2 acceptance boundary in two (#4573):
            // in-stratum entries were re-competed, off-stratum ones carry pass-1 q and aggregate
            // forward. Absent outside protein-compact, which leaves the panel on one boundary.
            if (config.ModelDiagnostics)
            {
                HashSet<uint> stratumBaseIds = null;
                if (OspreyEnvironment.Pass2ProteinCompact &&
                    ctx.TryGet<ProteinCompactStratum>(out var pcStratum))
                    stratumBaseIds = pcStratum.BaseIds;
                ModelDiagnosticsReport.WritePass2AndFinalize(
                    perFileEntries, pass2Contributions, libraryById, config, ctx.LogInfo,
                    stratumBaseIds);
            }

            return true;
        }

        /// <summary>
        /// Drop <c>Fragments</c> from every library entry outside the final per-file pool,
        /// keeping the identity fields on all of them. See
        /// <see cref="OspreyEnvironment.ReleaseLibraryFragments"/> for the rationale and
        /// <see cref="LibraryFragmentRelease"/> for the set arithmetic.
        ///
        /// <para>This is SecondPassFDR's own release, and on the HPC chain it is the ONLY one.
        /// <c>FirstPassFdrTask</c> - where the Stage 5 -&gt; 6 release lives - is excluded from a
        /// <c>--task SecondPassFDR</c> pipeline altogether, and that leg loads the library
        /// fragment-laden (<c>OmitFragments</c> is gated on <c>StopAfterStage5</c>, false here),
        /// so without this the one process that holds the whole 6.3 M-entry fragment set through
        /// second-pass Percolator, protein FDR AND the blib write realized no saving at all -
        /// the distributed path being exactly where memory hurts most.</para>
        ///
        /// <para>Harmless and near-free on the straight-through pipeline, where FirstPassFDR
        /// already released in this same process: <see cref="LibraryEntry.ReleaseSpectrum"/> is
        /// idempotent, so the count reported here is only what Stage 6 dropped afterwards (a
        /// gap-fill candidate that did not survive rescoring).</para>
        /// </summary>
        private void ReleaseUnscorableLibraryFragments(
            RescoredEntries rescored, int nFiles,
            List<LibraryEntry> fullLibrary, PipelineContext ctx)
        {
            if (!LibraryFragmentRelease.RunsOnThisLeg(ctx))
                return;

            // Streamed: this folds to O(distinct base_id) and retains nothing, so it can walk
            // the files one at a time and drop each. While something else still reads the
            // whole-run buffer, Files() yields from it and this costs nothing; once nothing
            // does, it is one file resident at a time (#4486).
            var retained = LibraryFragmentRelease.BuildRetainedBaseIds(rescored.Files());
            int released = LibraryFragmentRelease.ReleaseFragments(fullLibrary, retained);
            ctx.LogInfo(string.Format(
                @"Released library fragments for {0} of {1} entries ({2} base_ids retained for the reported pool)",
                released, fullLibrary.Count, retained.Count));
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"after library-fragment release");
            // Post-GC counterpart, so the release's actual recovery is attributable rather
            // than inferred: the pre-GC line above cannot show it, because the dropped
            // fragment arrays are garbage that has not been collected yet (#4486).
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"stage7-fragments-released",
                string.Format(@"(files={0}, released={1})", nFiles, released));
        }

        /// <summary>
        /// Run protein-level FDR using parsimony and picked-protein
        /// competition. The orchestration (collect best scores, detected-peptide
        /// gate, parsimony, picked-protein FDR, summary logging, q-value
        /// propagation) lives in <see cref="ProteinFdrEngine.RunSecondPass"/>,
        /// shared with the first-pass / rehydration paths. It returns the
        /// parsimony / FDR artifacts so the Stage-7 diagnostic dumps and the
        /// <c>Stage7ProteinFdrOnly</c> early-exit can stay in this Tasks facade --
        /// Osprey.FDR cannot reference Osprey.Diagnostics (the
        /// Diagnostics project references FDR), so the dump / Environment.Exit
        /// cannot move into the engine.
        /// </summary>
        private void RunProteinFdr(
            RescoredEntries rescored,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            PipelineContext ctx)
        {
            var result = ProteinFdrEngine.RunSecondPass(
                rescored.Files(), fullLibrary, config, ctx.LogInfo);

            // The 2nd-pass sidecar was written BEFORE this protein FDR ran - it is one of its
            // inputs - so the protein column it carries is still the pass-1 value at this point.
            // Patch it now that the pass-2 value exists, so every column in that file is a
            // pass-2 value (issue #4559). Cheap: 8 bytes per record, one file at a time, and
            // only where a 2nd-pass sidecar was written.
            // This MUST precede the dumps below: Stage7ProteinFdrOnly ends the process there,
            // and an unpatched record keeps whatever it held when the sidecar was written -
            // the ResetScores default for every entry Stage 6 rescored or gap-filled, since
            // RestorePass1Scalars no longer seeds this field.
            // The patch and the report writer below shared a 125 s silence on the 82-file SEA-AD
            // run of 2026-08-14, between "N protein groups pass ..." and the blib write (#4571).
            // Each now carries its own ProgressReporter inside the callee rather than a heading
            // here: a reporter prints its heading and then ONLY as many percent lines as the
            // elapsed time needs, so a fast run costs one line and a slow one stays alive. An
            // unconditional heading at the call site costs its line on every run forever, and
            // duplicated the reporter's own heading to the same second.
            // Not under --task ModelDiagnostics, whose contract is that it rewrites the report
            // and touches no other artifact. The patch is idempotent, so a regeneration wrote
            // the same bytes back - invisible to a content comparison, but it reset every
            // 2nd-pass sidecar's mtime, which is exactly the signal used to tell when a run's
            // inputs were produced. Caught by the mode 7 regeneration leg.
            if (AnyReconciledParquet(config) && !config.DiagnosticsOnly)
            {
                Pass2FdrSidecar.PatchPass2ProteinQvalues(
                    ctx, rescored.FileNames, perFileParquetPaths, result.ProteinFdr.PeptideQvalues);
            }

            // Cross-impl bisection dump (env-var-gated, no-op in production).
            if (ctx.Diagnostics?.DumpDetectedPeptides ?? false)
                ctx.Diagnostics?.WriteStage7DetectedPeptidesDump(result.DetectedPeptides);

            // Stage 7 cross-impl bisection dump (no-op unless
            // OSPREY_DUMP_STAGE7_PROTEIN_FDR=1). Mirrors Rust
            // diagnostics.dump_stage7_protein_fdr. The engine has already
            // propagated q-values onto the stubs, but the dump reads only the
            // parsimony / FDR result (not the stubs), so it is unaffected.
            if (ctx.Diagnostics?.DumpStage7ProteinFdr ?? false)
            {
                ctx.Diagnostics?.WriteStage7ProteinFdrDump(result.Parsimony, result.ProteinFdr);
                if (ctx.Diagnostics?.Stage7ProteinFdrOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_STAGE7_PROTEIN_FDR_ONLY");
            }

            // Default user-facing reports (protein groups + per-replicate/experiment
            // summary), modeled on DIA-NN. Additive files next to the output blib, so the
            // byte-parity gate (blib + Stage-7 dump) is unaffected. The per-replicate
            // protein counts re-run protein FDR per run, so this is the one place with the
            // full per-file pool + library in hand.
            if ((config.WriteProteinReport || config.WriteSummaryReport) && !config.DiagnosticsOnly)
            {
                OspreyReportWriter.WriteReports(result, rescored, fullLibrary, config, ctx.LogInfo);
            }
        }

        /// <summary>
        /// True iff any input file has a reconciled scores parquet on disk -- i.e.
        /// Stage 6 rescored at least one file (multi-charge consensus, inter-replicate
        /// reconciliation, or gap-fill). Disk-based so it reads identically in the
        /// in-process pipeline (Stage 6 just wrote them) and the --task SecondPassFDR
        /// node (the Stage 6 worker wrote them). The C# analog of Rust's
        /// <c>total_rescored &gt; 0</c> gate (pipeline.rs:5209) for the second
        /// Percolator pass.
        /// </summary>
        private static bool AnyReconciledParquet(OspreyConfig config)
        {
            if (config.InputFiles == null)
                return false;
            foreach (var input in config.InputFiles)
            {
                string reconciledPath = ParquetScoreCache.GetReconciledScoresPath(input);
                if (!File.Exists(reconciledPath))
                    continue;
                // Existence alone is no longer the answer. Stage 6 now writes a reconciled
                // parquet for EVERY file, including one that had no rescore work at all
                // (a faithful copy), so File.Exists would report total_rescored > 0 on a
                // cohort Rust skips the second pass for entirely - the anti-conservative
                // direction, since the pass-2 recalibration is what measured 1.57% FDP
                // against 0.92%. The footer says which it is.
                //
                // A parquet written before the key existed is treated as WORK, because back
                // then it was only written when there was some: the two statements meant the
                // same thing, which is why existence was ever a sound test.
                var footer = ParquetScoreCache.LoadFooterMetadata(reconciledPath);
                if (!footer.TryGetValue(@"osprey.rescored", out string rescored))
                    return true;
                if (!string.Equals(rescored, @"0", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Rewrite any <c>.scores-reconciled.parquet</c> still in the pre-#4486 full-row
        /// shape into the survivor subset this build writes, in place. Returns true when the
        /// run should stop here.
        ///
        /// <para>Recovery rather than migration tooling: a cohort staged by an older build
        /// would otherwise have to re-run Stage 6 - hours at cohort scale - for a format that
        /// is one sequential read and a much smaller write away. Any workflow holding old
        /// artifacts therefore self-heals on its next run.</para>
        ///
        /// <para>The rows to keep are the identities of the post-compaction list this stage
        /// already holds, which is by definition what Stage 7 consumes, so nothing about the
        /// survivor decision is re-derived here. The footer is preserved key for key except
        /// <c>osprey.reconciled</c>, so the version stamp and the search / library /
        /// reconciliation hashes still describe the run that produced the rows and every
        /// validity check that passed before passes after.</para>
        ///
        /// <para>Runs BEFORE <see cref="ReleaseUnscorableLibraryFragments"/>, which mutates
        /// the library entries the write reads its sequence / precursor m/z / protein-id
        /// columns from - with a null or stripped library those columns write empty.</para>
        /// </summary>
        private static bool UpgradeReconciledParquets(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            PipelineContext ctx)
        {
            if (perFileParquetPaths == null)
                return false;
            int upgraded = 0;
            foreach (var kv in perFileEntries)
            {
                if (!perFileParquetPaths.TryGetValue(kv.Key, out string scoresPath))
                    continue;
                string reconciledPath = ParquetScoreCache.ReconciledPathFromScoresPath(scoresPath);
                if (!File.Exists(reconciledPath))
                    continue;
                var metadata = ParquetScoreCache.LoadFooterMetadata(reconciledPath);
                metadata.TryGetValue(@"osprey.reconciled", out string marker);
                if (string.Equals(marker, ParquetScoreCache.RECONCILED_SURVIVORS,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                // Same refusal the dedicated task makes, for the same reason: this rewrite
                // re-derives every row's sequence and protein_ids from the library BY ENTRY
                // ID, so a foreign library silently renames every peptide. The run-level
                // parquet-group check does not cover it - that validates the paths in
                // --input-scores, while this rewrites their reconciled SIBLINGS, and on a
                // full-pipeline run it never runs at all.
                if (!CompactPerFileRescoreTask.VerifyLibraryMatches(
                        reconciledPath, metadata, ctx.Config.Identity.LibraryIdentityHash(),
                        ctx.Config, ctx))
                {
                    return false;
                }
                var keepIdentities = new HashSet<(uint, byte, uint)>(kv.Value.Count);
                foreach (var entry in kv.Value)
                    keepIdentities.Add((entry.EntryId, entry.Charge, entry.ScanNumber));
                metadata[@"osprey.reconciled"] = ParquetScoreCache.RECONCILED_SURVIVORS;

                // Written beside the target and swapped in, because the stream reads the very
                // file it replaces. Move -> Move -> Delete, not Delete -> Move: a crash
                // between the steps then leaves BOTH copies rather than neither. This runs
                // inside an ordinary Stage 7, which is exactly where this cohort has been
                // OOM-killed before, and a Delete -> Move window destroys the file outright -
                // after which the re-run's File.Exists check above skips it in silence.
                string upgradedPath = reconciledPath + @".upgraded";
                var result = ParquetScoreCache.StreamReconciledScoresParquet(
                    reconciledPath, upgradedPath, null, null, metadata, libraryById,
                    kv.Key, keepIdentities, null, ctx.LogWarning);
                string retiredPath = reconciledPath + @".retired";
                File.Replace(upgradedPath, reconciledPath, retiredPath);
                File.Delete(retiredPath);
                upgraded++;
                ctx.LogInfo(string.Format(
                    @"  Upgraded reconciled parquet for {0}: {1} rows kept of {2}",
                    kv.Key, result.NWritten, result.OrigRowCount));
            }
            if (upgraded == 0)
                return false;
            ctx.LogInfo(string.Format(
                @"Upgraded {0} reconciled parquet(s) to the survivor subset format.", upgraded));
            if (!OspreyEnvironment.UpgradeReconciledOnly)
                return false;
            ctx.LogInfo(
                @"OSPREY_UPGRADE_RECONCILED_ONLY is set - stopping after the upgrade so a " +
                @"following run profiles the new format alone.");
            return true;
        }

        /// <summary>
        /// True when <see cref="UpgradeReconciledParquets"/> asked the run to stop, which is
        /// NOT a successful Stage 7.
        ///
        /// <para>Run must return FALSE there. <c>AnalysisPipeline.RunTask</c> stamps a fresh
        /// validity sidecar onto every declared output that merely EXISTS whenever the exit
        /// code is zero, and Run deleted those sidecars on entry without rewriting the
        /// artifacts - so reporting success would certify a previous run's <c>out.blib</c> and
        /// <c>.2nd-pass.fdr_scores.bin</c> under this run's validity key, and the next
        /// invocation would skip Stage 7 and ship them as its own answer. Every other
        /// success-but-stop boundary in the pipeline returns false for this reason
        /// (PerFileScoringTask under --task PerFileScoring, FirstPassFdrTask under
        /// StopAfterStage5).</para>
        /// </summary>
        private static bool StopAfterUpgrade(PipelineContext ctx)
        {
            ctx.LogInfo(
                @"--task SecondPassFDR: stopping after the reconciled-parquet upgrade; no " +
                @"blib or 2nd-pass sidecar was written by this run.");
            return false;
        }

        /// <summary>
        /// Write passing entries to a BiblioSpec blib file.
        ///
        /// <para>Takes the milestone rather than the pool. Its three gates fold to O(distinct)
        /// over a per-file walk, the passing set it carries forward is compact records, and the
        /// writer needs only the run's file NAMES from what used to be the buffer - so nothing
        /// in this phase holds a file after the gate has walked past it (#4486).</para>
        /// </summary>
        private void WriteBlibOutput(
            RescoredEntries rescored, int nFiles,
            List<LibraryEntry> fullLibrary,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            OspreyConfig config,
            PipelineContext ctx)
        {
            // Two-stage blib output gate, mirroring Rust pipeline.rs:4596-4668.
            //
            // Stage 1 (peptide gate): the configured FdrLevel determines which
            // peptide identities are eligible for output. EXPERIMENT-level
            // q-value, not run-level — letting in any precursor that merely
            // passed run-level FDR in some replicate would admit identifications
            // upstream Rust filters out, and was the source of a 483-row
            // RefSpectra over-count (Stellar 3-file) before this fix.
            //
            // Stage 2 (precursor gate): within each eligible peptide, include
            // only charge states that individually pass
            // experiment_precursor_qvalue <= experiment_fdr. If NO charge state
            // of a peptide passes precursor-level FDR (possible because
            // peptide-level FDR aggregates across charges), include the best
            // charge state (lowest experiment_precursor_qvalue) as a
            // representative.
            // Streamed: both gates fold to O(distinct) and retain nothing.
            var passingPeptides = ComputePassingPeptides(rescored.Files(), config, nFiles);

            var passingPrecursors = ComputePassingPrecursors(
                rescored.Files(), config, passingPeptides, nFiles, out int nFallback);
            if (nFallback > 0)
            {
                ctx.LogInfo(string.Format(
                    "{0} peptides had no charge state passing precursor-level FDR; best charge state kept as fallback",
                    nFallback));
            }

            // Streamed, and the LAST walk of the pool in this phase: what comes back is a
            // compact record per passing observation plus the best run per precursor, so
            // everything after this line works on ~14 M values instead of holding 137 M
            // entries alive to read eight fields off them (#4486).
            var passingEntries = CollectPassingEntries(
                rescored.Files(), passingPrecursors, nFiles, out var bestByPrecursor);

            ctx.LogInfo(string.Format(
                "[COUNT] Stage 1 passing peptides: {0}", passingPeptides.Count));
            ctx.LogInfo(string.Format(
                "[COUNT] Stage 2 passing precursors: {0}", passingPrecursors.Count));

            if (passingEntries.Count == 0)
            {
                ctx.LogWarning("No entries pass FDR threshold. Creating empty blib.");
            }

            // Ensure output directory exists
            string outputDir = Path.GetDirectoryName(config.OutputBlib);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            ctx.LogInfo(string.Format(
                "[COUNT] Best-per-precursor for blib: {0}", bestByPrecursor.Count));

            // All three take the ALREADY-FILTERED passing entries, not the pool. Each applied
            // exactly the filter CollectPassingEntries applied 20 lines earlier - non-decoy
            // AND in passingPrecursors - so re-walking the pool was three passes over 137 M
            // rows at 257 files to reach the same ~14 M. BuildCrossFileObservations had no
            // passing gate at all and indexed every non-decoy precursor, though its only
            // consumer looks up keys from bestByPrecursor, which are passing by construction.
            var bestExpPrecursorQ = BuildBestExpPrecursorQ(passingEntries);

            var sharedBounds = BuildSharedBoundaries(
                passingEntries, MultiChargePeptides(passingPrecursors));

            var precursorFacts = BuildPrecursorFacts(passingEntries, config.RunFdr);

            ctx.LogInfo(string.Format(
                "[COUNT] Cross-file observations to write: {0}", passingEntries.Count));

            BlibOutputWriter.Write(config, rescored.FileNames, libraryById, bestByPrecursor,
                bestExpPrecursorQ, sharedBounds, passingEntries, precursorFacts);

            ctx.LogInfo(string.Format("Wrote {0} library spectra to {1} (from {2} passing entries)",
                bestByPrecursor.Count, config.OutputBlib, passingEntries.Count));
        }

        // Stage 1 (peptide gate): the configured FdrLevel determines which
        // peptide identities are eligible for output. EXPERIMENT-level q-value.
        private static HashSet<string> ComputePassingPeptides(
            IEnumerable<KeyValuePair<string, List<FdrEntry>>> perFileEntries, OspreyConfig config,
            int nFiles)
        {
            double expThreshold = config.ExperimentFdr;
            var passingPeptides = new HashSet<string>(StringComparer.Ordinal);
            // Reported. This and the two gates after it are the last whole-pool walks in the
            // blib phase - unavoidable, because each needs the previous one's set complete
            // before it can start - and at 257 files they ran as ~70 s of silence between the
            // protein-FDR line and the first [COUNT] (#4615 review).
            using (var progress = new ProgressReporter(string.Format(
                       @"Selecting peptides passing experiment FDR over {0} file(s)", nFiles),
                       nFiles, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                int done = 0;
                foreach (var kvp in perFileEntries)
                {
                    progress.Report(++done);
                    foreach (var e in kvp.Value)
                    {
                        if (e.IsDecoy)
                            continue;
                        if (e.EffectiveExperimentQvalue(config.FdrLevel) <= expThreshold)
                            passingPeptides.Add(e.ModifiedSequence);
                    }
                }
            }
            return passingPeptides;
        }

        // Stage 2 (precursor gate): within each eligible peptide, include only
        // charge states that individually pass experiment_precursor_qvalue <=
        // experiment_fdr; if none does, keep the best charge as a representative
        // (nFallback counts those). Tuple keys (modseq, charge) mirror Rust's
        // HashMap<(Arc<str>, u8), ...> at pipeline.rs:4630.
        private static HashSet<(string, byte)> ComputePassingPrecursors(
            IEnumerable<KeyValuePair<string, List<FdrEntry>>> perFileEntries, OspreyConfig config,
            HashSet<string> passingPeptides, int nFiles, out int nFallback)
        {
            double expThreshold = config.ExperimentFdr;
            var passingPrecursors = new HashSet<(string, byte)>();
            var bestChargePerPeptide = new Dictionary<string, KeyValuePair<byte, double>>(
                StringComparer.Ordinal);
            using (var progress = new ProgressReporter(string.Format(
                       @"Selecting charge states passing precursor FDR over {0} file(s)", nFiles),
                       nFiles, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                int done = 0;
                foreach (var kvp in perFileEntries)
                {
                    progress.Report(++done);
                    foreach (var e in kvp.Value)
                    {
                        if (e.IsDecoy || !passingPeptides.Contains(e.ModifiedSequence))
                            continue;
                        if (e.ExperimentPrecursorQvalue <= expThreshold)
                            passingPrecursors.Add((e.ModifiedSequence, e.Charge));
                        KeyValuePair<byte, double> existing;
                        if (!bestChargePerPeptide.TryGetValue(e.ModifiedSequence, out existing)
                            || e.ExperimentPrecursorQvalue < existing.Value)
                        {
                            bestChargePerPeptide[e.ModifiedSequence] =
                                new KeyValuePair<byte, double>(e.Charge, e.ExperimentPrecursorQvalue);
                        }
                    }
                }
            }
            // Fallback: peptides with no precursor-passing charge state keep their best.
            nFallback = 0;
            foreach (var peptide in passingPeptides)
            {
                KeyValuePair<byte, double> best;
                if (!bestChargePerPeptide.TryGetValue(peptide, out best))
                    continue;
                if (best.Value <= expThreshold)
                    continue; // already in passingPrecursors
                passingPrecursors.Add((peptide, best.Key));
                nFallback++;
            }
            return passingPrecursors;
        }

        /// <summary>
        /// Walk the pool ONCE and take from it the two things the blib phase needs: a compact
        /// record per passing observation, and the best run per precursor.
        ///
        /// <para>A precursor is admitted iff (modseq, charge) is in
        /// <paramref name="passingPrecursors"/>. No protein-FDR gate here (mirrors Rust:
        /// <c>--protein-fdr</c> is a compute flag, not a hard blib filter; FdrLevel has no
        /// Protein variant).</para>
        ///
        /// <para>The best-per-precursor map used to be a SECOND pass, over the passing list.
        /// It cannot be, once that list holds values rather than references: the RefSpectra
        /// rows need the winner's <see cref="FdrEntry"/> itself, for the library lookup and
        /// the spectrum. It is built here instead, in the same walk, and it is
        /// O(distinct precursor) - 45,724 entries at 257 CHS files, ~12 MB - so the entries it
        /// keeps pin themselves and nothing else (#4486).</para>
        ///
        /// <para>Deduplicated by (modseq, charge) keeping the best
        /// <c>EffectiveRunQvalue(Both)</c>, matching Rust pipeline.rs:6133-6138. The blib's
        /// RefSpectra / OspreyRunScores / OspreyPeakBoundaries all source from this best run,
        /// so the cross-impl best-file choice must match exactly - which is why the walk order
        /// is the pool's own and the comparison is strictly less-than, exactly as the second
        /// pass over the passing list was.</para>
        /// </summary>
        private static List<PassingObservation> CollectPassingEntries(
            IEnumerable<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            HashSet<(string, byte)> passingPrecursors, int nFiles,
            out Dictionary<(string, byte), KeyValuePair<string, FdrEntry>> bestByPrecursor)
        {
            var passingEntries = new List<PassingObservation>();
            bestByPrecursor = new Dictionary<(string, byte), KeyValuePair<string, FdrEntry>>();
            // One canonical string per distinct passing peptide. The parquet reader hands out a
            // fresh instance per row, so without this the records would retain 11.7 M strings
            // where there are about 40,000 distinct sequences - more memory than the records.
            var canonicalSequences = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var progress = new ProgressReporter(string.Format(
                       @"Collecting passing entries over {0} file(s)", nFiles),
                       nFiles, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                int done = 0;
                foreach (var kvp in perFileEntries)
                {
                    progress.Report(++done);
                    foreach (var entry in kvp.Value)
                    {
                        if (entry.IsDecoy)
                            continue;
                        var key = (entry.ModifiedSequence, entry.Charge);
                        if (!passingPrecursors.Contains(key))
                            continue;
                        if (!canonicalSequences.TryGetValue(entry.ModifiedSequence, out string modSeq))
                        {
                            modSeq = entry.ModifiedSequence;
                            canonicalSequences[modSeq] = modSeq;
                        }
                        double runQ = entry.EffectiveRunQvalue(FdrLevel.Both);
                        passingEntries.Add(new PassingObservation(
                            kvp.Key, modSeq, entry.Charge, runQ, entry.ExperimentPrecursorQvalue,
                            entry.ApexRt, entry.StartRt, entry.EndRt));
                        if (!bestByPrecursor.TryGetValue(key, out var existing) ||
                            runQ < existing.Value.EffectiveRunQvalue(FdrLevel.Both))
                        {
                            bestByPrecursor[key] =
                                new KeyValuePair<string, FdrEntry>(kvp.Key, entry);
                        }
                    }
                }
            }
            return passingEntries;
        }

        // Best (min) experiment_precursor_qvalue per (modseq, charge) across all
        // files — the value Rust writes into RefSpectra.score and
        // OspreyExperimentScores.ExperimentQValue (pipeline.rs:4670-4683 + 4795).
        private static Dictionary<(string, byte), double> BuildBestExpPrecursorQ(
            List<PassingObservation> passingEntries)
        {
            var bestExpPrecursorQ = new Dictionary<(string, byte), double>();
            // Reported: this and the two builders below run back to back with nothing between
            // them but [COUNT] lines, which OspreyOutput.IsStatLine filters out of normal
            // output - so at cohort scale the three ran as one 70 s silence broken only by a
            // blank line. Now over passingEntries, an order of magnitude smaller than the pool
            // they used to walk, but the silence would still be theirs to break.
            using (var progress = new ProgressReporter(
                       string.Format(@"Collecting best experiment q per precursor over {0} entries",
                                     passingEntries.Count),
                       passingEntries.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                int expIdx = 0;
                foreach (var obs in passingEntries)
                {
                    progress.Report(expIdx++);
                    var keyExp = obs.PrecursorKey;
                    double existingExp;
                    if (!bestExpPrecursorQ.TryGetValue(keyExp, out existingExp)
                        || obs.ExperimentPrecursorQvalue < existingExp)
                    {
                        bestExpPrecursorQ[keyExp] = obs.ExperimentPrecursorQvalue;
                    }
                }
            }
            return bestExpPrecursorQ;
        }

        // Shared peak boundaries per (peptide, file): all charge states of the
        // same peptide in a run share the boundaries from the charge with lowest
        // run_qvalue. Mirrors Rust pipeline.rs build_shared_boundaries_from_plan.
        // Key: (modseq, fileName); value: { apexRt, startRt, endRt, run_q, charge }
        // from the min-run-qvalue entry (charge breaks run_qvalue ties).
        /// <summary>
        /// The peptides with more than one passing charge state, taken from
        /// <paramref name="passingPrecursors"/> - 45,724 keys at 257 CHS files, so this costs
        /// nothing to build and is the whole reason the boundary map can stay small.
        /// </summary>
        private static HashSet<string> MultiChargePeptides(
            HashSet<(string, byte)> passingPrecursors)
        {
            var seenCharge = new Dictionary<string, byte>(StringComparer.Ordinal);
            var multi = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in passingPrecursors)
            {
                if (!seenCharge.TryGetValue(key.Item1, out byte first))
                {
                    seenCharge[key.Item1] = key.Item2;
                    continue;
                }
                if (first != key.Item2)
                    multi.Add(key.Item1);
            }
            return multi;
        }

        internal static Dictionary<(string, string), double[]> BuildSharedBoundaries(
            List<PassingObservation> passingEntries,
            HashSet<string> multiChargePeptides)
        {
            var sharedBounds = new Dictionary<(string, string), double[]>();
            using (var progress = new ProgressReporter(
                       string.Format(@"Resolving shared peak boundaries over {0} entries",
                                     passingEntries.Count),
                       passingEntries.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                int boundsIdx = 0;
                foreach (var e in passingEntries)
                {
                    progress.Report(boundsIdx++);
                    // A peptide with ONE passing charge is its own winner in every run, so
                    // the entry it would store equals the entry's own boundaries - and both
                    // readers (EmitSpectrumRows and WriteRetentionTimes) already initialize
                    // from the entry and overwrite only on a hit. Storing those keys was
                    // filling the map with its own fallback. At 257 CHS files the passing set
                    // is 45,724 precursors over ~40,000 peptides, so this drops roughly six
                    // keys in seven, and it drops them BEFORE insertion rather than pruning
                    // afterwards, which is the difference between a smaller map and a smaller
                    // peak.
                    if (multiChargePeptides != null &&
                        !multiChargePeptides.Contains(e.ModifiedSequence))
                    {
                        continue;
                    }
                    var sk = (e.ModifiedSequence, e.FileName);
                    double rq = e.RunQvalue;
                    double[] existingB;
                    // On a run_qvalue TIE (e.g. two charge states both gap-filled at
                    // q=1.0), break deterministically by LOWEST CHARGE so the winner
                    // does not depend on the per-file entry iteration order. Rust
                    // build_shared_boundaries_from_plan applies the identical
                    // (lower run_qvalue, then lower charge) rule, so both impls keep
                    // the same charge's window and the blib RetentionTimes start/end
                    // stay byte-identical cross-impl.
                    if (!sharedBounds.TryGetValue(sk, out existingB)
                        || rq < existingB[3]
                        || (rq == existingB[3] && e.Charge < existingB[4]))
                    {
                        sharedBounds[sk] = new[] { e.ApexRt, e.StartRt, e.EndRt, rq, e.Charge };
                    }
                }
            }
            return sharedBounds;
        }

        /// <summary>
        /// The three facts <c>WriteRetentionTimes</c> needs about a precursor that are not
        /// properties of the row it is writing: whether ANY run passed run-level FDR, which
        /// run has the lowest run q, and how many runs detected it.
        ///
        /// <para>This replaces the map of per-precursor observation LISTS the writer used to
        /// index. That map existed only to let a precursor-major writer find a precursor's
        /// rows across every file - it was O(observations), it held a reference to every
        /// passing entry, and it was the reason the blib phase could not emit file-major.
        /// These three folds are O(distinct precursor) instead: 45,724 keys at 257 CHS files
        /// against 11,745,026 references.</para>
        ///
        /// <para><c>BestRunFile</c> is folded here rather than taken from
        /// <c>bestByPrecursor</c>. The two apply the same rule over the same rows in the same
        /// order and should always agree, but "should always agree" is the kind of assumption
        /// that quietly stops being true, and the fold costs nothing.</para>
        /// </summary>
        internal static Dictionary<(string, byte), (bool AnyPassesRunFdr, string BestRunFile, int NRuns)>
            BuildPrecursorFacts(List<PassingObservation> passingEntries, double fdrThreshold)
        {
            var facts =
                new Dictionary<(string, byte), (bool AnyPassesRunFdr, string BestRunFile, int NRuns)>();
            var bestRunQ = new Dictionary<(string, byte), double>();
            foreach (var e in passingEntries)
            {
                var key = e.PrecursorKey;
                double rq = e.RunQvalue;
                if (!facts.TryGetValue(key, out var cur))
                {
                    facts[key] = (rq <= fdrThreshold, e.FileName, 1);
                    bestRunQ[key] = rq;
                    continue;
                }
                bool anyPasses = cur.AnyPassesRunFdr || rq <= fdrThreshold;
                string bestFile = cur.BestRunFile;
                // Strictly less, so the FIRST row wins a tie - the rule the precursor-major
                // writer applied over this same file-major order.
                if (rq < bestRunQ[key])
                {
                    bestRunQ[key] = rq;
                    bestFile = e.FileName;
                }
                facts[key] = (anyPasses, bestFile, cur.NRuns + 1);
            }
            return facts;
        }
    }
}
