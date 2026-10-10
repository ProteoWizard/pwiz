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
using System.Diagnostics;
using System.IO;
using System.Threading;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The SecondPassFDR 2nd-pass FDR sidecar step (Stage 8 input prep, mirrors
    /// Rust pipeline.rs:4394-4494): reload PIN features from the reconciled
    /// parquets, run 2nd-pass Percolator on the post-reconciliation entries,
    /// write the per-file <c>.2nd-pass.fdr_scores.bin</c> sidecars, then reload
    /// those sidecars onto the post-compaction stubs so run-wide protein FDR
    /// sees the 2nd-pass q-values rather than the stale 1st-pass values.
    ///
    /// Extracted verbatim from <see cref="SecondPassFdrTask.Run"/> as pure code
    /// motion so that method reads as a sequencer; behavior (and therefore the
    /// 2nd-pass sidecars and downstream protein-FDR / blib output) is unchanged.
    /// The parity-locked 2nd-pass scoring core (<c>FirstPassFdrTask.RunPercolatorFdr</c>)
    /// is invoked whole through the
    /// live <see cref="PipelineContext"/>; it is not decomposed here.
    /// </summary>
    internal static class Pass2FdrSidecar
    {
        /// <summary>
        /// The low 31 bits of an entry_id: its base_id, with the high bit marking a decoy.
        ///
        /// <para>Declared here because <c>BASE_ID_MASK</c> is internal to
        /// Osprey.FDR and therefore invisible from this assembly. Three other files in
        /// Osprey.Tasks already carry a private copy of the same literal
        /// (<c>FdrBenchInputWriter</c>, <c>ModelDiagnosticsReport</c>,
        /// <c>PeakCoAssignmentSource</c>); this one is internal rather than private so the
        /// second-pass worker shares it instead of adding a fifth. Consolidating all five - most
        /// cleanly by making the FDR constant public, since it encodes a cross-assembly wire
        /// convention rather than an implementation detail - is worth doing separately.</para>
        /// </summary>
        internal const uint BASE_ID_MASK = 0x7FFFFFFF;

        /// <summary>
        /// Run SecondPassFDR's second pass: fold every run's per-run 2nd-pass answer, written by
        /// PerFileRescoring, into the analysis-wide experiment scope, and put the answers back on
        /// the survivors the later folds read. <paramref name="taskName"/> and
        /// <paramref name="taskValidityKey"/> are the owning task's identity.
        ///
        /// <para><b>No per-run file is written here, in any mode (#4665).</b> Every pass-2 mode
        /// computes its per-file half in the rescore worker (<see cref="Pass2PerFileWorker"/>),
        /// so every run's <c>.2nd-pass.fdr_scores.bin</c> is PerFileRescoring's output and this
        /// stage's input. That removed three things that lived here: a whole-pool second pass for
        /// <c>OSPREY_PASS2_QVALUE=transfer</c>, which held every run's survivors for the whole
        /// stage; a fallback that recomputed any run the worker had not answered; and a rewrite of
        /// every per-run file from the pool when Stage 6 had done no work anywhere - a path that
        /// published no experiment scope and so failed when the experiment sidecar was written.</para>
        ///
        /// <para>A run without a current answer is a stop rather than a run to recompute: there
        /// is nothing here to recompute it with, and the remedy is PerFileRescoring's.</para>
        /// </summary>
        internal static void ComputeAndPersist(
            PipelineContext ctx,
            RescoredEntries rescored,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            string taskName,
            string taskValidityKey)
        {
            var config = ctx.Config;

            // OSPREY_PASS2_QVALUE selects how this 2nd pass assigns reported q-values.
            // Log the active mode once so a run's provenance is in the log. An unrecognized
            // token never reaches here: Program aborts at startup.
            if (OspreyEnvironment.Pass2TransferQ)
            {
                // The pass-1 model scores the moved peaks and each file maps score to run q with
                // its own first-pass table; experiment q stays anchored on the best peak.
                ctx.LogInfo(string.Format(
                    @"OSPREY_PASS2_QVALUE={0}: first-pass q-values are kept; only peaks moved by " +
                    @"cross-run reconciliation get a new run-level q-value.",
                    OspreyEnvironment.PASS2_QVALUE_TRANSFER));
            }
            ctx.LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_PASS2_QVALUE, OspreyEnvironment.Pass2QValue));

            EnsureFrozenFirstPassPublished(ctx, perFileParquetPaths);

            if (perFileParquetPaths.Count == 0 || config.InputFiles == null)
                return;

            var pass2Writer = new Pass2SidecarWriter(config, taskName, taskValidityKey);
            RequireWorkerAnswers(rescored.FileNames, pass2Writer);

            // LogInfo, not LogVerbose: this is the heading for the longest stretch of work left
            // in Stage 7, and a --verbose-only heading is invisible on the runs that actually take
            // the time (#4571).
            ctx.LogInfo(CountText.Format(rescored.FileCount, OspreyTasksResources.Pass2FdrSidecar_ComputeAndPersist_Computing_second_pass_FDR_scores_for_1_file_,
                OspreyTasksResources.Pass2FdrSidecar_ComputeAndPersist_Computing_second_pass_FDR_scores_for__0__files_));
            var swPass2 = Stopwatch.StartNew();
            // Two modes, two folds, and no third: OSPREY_PASS2_QVALUE normalizes to
            // protein-compact or transfer and nothing else. Both read the same per-run answers;
            // they differ only in how a record becomes an experiment-scope value - re-competed
            // over the stratum, or carried from the first pass.
            if (OspreyEnvironment.Pass2ProteinCompact)
                ComputePass2FrozenCompetition(ctx, rescored, perFileParquetPaths, config, pass2Writer);
            else
                ComputePass2TransferFold(ctx, rescored, config, pass2Writer);
            swPass2.Stop();
            ctx.LogInfo(LogTag.STAGE_WALL, @"second-pass-fdr: {0:F1}s",
                swPass2.Elapsed.TotalSeconds);

            // Put the answers back on a RESIDENT pool, which every later fold of this stage reads.
            // RunProteinFdr's detected_peptides gate filters on ExperimentPrecursorQvalue, which
            // has to be the 2nd-pass value to match Rust pipeline.rs:4480-4494's
            // reload-then-second-pass-FDR sequence; without it single-file --task SecondPassFDR
            // runs include ~19 borderline peptides whose 1st-pass q passes 1% and whose 2nd-pass q
            // does not, a 1-protein delta in the picked-protein output cross-impl.
            //
            // A STREAMED pool has no entries to overlay: they are dropped as each run is folded.
            // The same overlay is installed as a per-run hook instead (InstallStreamedPass2Overlay,
            // called by the stage right after this method), so every later fold rebuilds a run and
            // immediately gets its second-pass values.
            if (!rescored.Streams)
            {
                ReloadPass2Sidecars(ctx, pass2Writer, rescored.Value, @"post-fold",
                    LazyPass2ExperimentRecords(ctx));
            }
        }

        /// <summary>
        /// Stop unless every run has PerFileRescoring's current answer on disk: a
        /// <c>.2nd-pass.fdr_scores.bin</c> (and under protein-compact the competition decoys)
        /// carrying the stamp of the reconciled parquet beside it
        /// (<see cref="HasWorkerStamp"/>).
        ///
        /// <para>Every run, named, before any is folded. Stage 7 used to recompute a run the
        /// worker had not answered; there is no recompute any more, so a missing answer can only
        /// be reported - and reporting it here, with the whole list, beats discovering one run at
        /// a time partway through a fold that has already spent minutes per run. A key with no
        /// matching input file is in the list too: its answer has nowhere to be.</para>
        /// </summary>
        private static void RequireWorkerAnswers(IReadOnlyList<string> fileNames, Pass2SidecarWriter writer)
        {
            var missing = new List<string>();
            foreach (string fileName in fileNames)
            {
                string inputFile = writer.InputFor(fileName);
                if (inputFile == null || !HasWorkerStamp(inputFile))
                    missing.Add(fileName);
            }
            if (missing.Count == 0)
                return;
            throw new InvalidOperationException(string.Format(
                OspreyTasksResources.Pass2FdrSidecar_RequireWorkerAnswers__0__of__1__runs_have_no_current_second_pass_results_file___3____which__4__writes_for_every_run_,
                missing.Count, fileNames.Count, string.Join(@", ", missing),
                @"." + FdrScoresSidecar.LABEL_SECOND_PASS + FdrScoresSidecar.EXT,
                PerFileRescoreTask.TASK_NAME, OspreyArgNames.TaskText(PerFileRescoreTask.TASK_NAME)));
        }

        /// <summary>
        /// Publish the trained 1st-pass model - and, under protein-compact, the stratum that
        /// rides in the same sidecar - from disk when this process did not train it.
        ///
        /// <para>Needed by every entry point that reads frozen 1st-pass state without having
        /// run the first pass: a distributed <c>--task SecondPassFDR</c> node, any resume that
        /// skipped training, and the pass-2 diagnostics-only fold, which runs no second pass at
        /// all and still has to know the stratum in order to split the acceptance boundary the
        /// same way the join did. Shared rather than repeated because a fold that resolved the
        /// stratum differently from the join would describe a different pool while looking like
        /// the same report.</para>
        ///
        /// <para>A no-op when the model is already published or the sidecar is absent; in the
        /// latter case the caller's own fail-fast applies. Only a stratum the sidecar actually
        /// carried is published - an empty one would silently constrain the competition to
        /// nothing, which is worse than the honest absence.</para>
        /// </summary>
        internal static void EnsureFrozenFirstPassPublished(
            PipelineContext ctx, IReadOnlyDictionary<string, string> perFileParquetPaths)
        {
            if (ctx.TryGet<FirstPassPercolatorModel>(out _))
                return;
            var reloaded = FirstPassModelIO.LoadFromAny(perFileParquetPaths);
            if (reloaded == null)
                return;
            // ExperimentAgg is what the TRAINING process ran under (null on a sidecar
            // written before the field existed). This node's own OSPREY_EXPERIMENT_AGG
            // says nothing about it, so carry the recorded value rather than re-reading.
            ctx.Publish(new FirstPassPercolatorModel
                { Results = reloaded.Model, ExperimentAgg = reloaded.ExperimentAgg });
            ctx.LogInfo(OspreyTasksResources.Pass2FdrSidecar_EnsureFrozenFirstPassPublished_Reusing_the_saved_first_pass_model_);
            ctx.LogVerbose(string.Format(
                OspreyTasksResources.Pass2FdrSidecar_EnsureFrozenFirstPassPublished_Reloaded_the_saved_first_pass_model_for_second_pass_FDR__first_pass_experiment_,
                reloaded.ExperimentAgg ?? @"not recorded"));

            if (OspreyEnvironment.Pass2ProteinCompact && reloaded.StratumBaseIds != null &&
                !ctx.TryGet<ProteinCompactStratum>(out _))
            {
                ctx.Publish(new ProteinCompactStratum(reloaded.StratumBaseIds));
                ctx.LogVerbose(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_EnsureFrozenFirstPassPublished_Reloaded_the_precursor_candidates_from_proteins_with_2_or_more_detections___0__target_,
                    reloaded.StratumBaseIds.Count));
            }
        }

        /// <summary>
        /// Overlay every file's <c>.2nd-pass.fdr_scores.bin</c> back onto its survivors.
        ///
        /// <para>Called for two mutually exclusive reasons. BEFORE the write when this run did
        /// not recompute, so the unconditional write puts a resumed run's own second-pass
        /// values back rather than downgrading the file to the standing first-pass ones; and
        /// AFTER it when it did, because the paths that write their own sidecars during the
        /// score pass (the projection sink) leave the survivors untouched, and
        /// RunProteinFdr's detected-peptide gate filters on
        /// <c>ExperimentPrecursorQvalue</c>, which has to be the second-pass
        /// value to match Rust pipeline.rs:4480-4494's reload-then-second-pass-FDR sequence.
        /// Without the second one, single-file <c>--task SecondPassFDR</c> runs include ~19
        /// borderline peptides whose 1st-pass q passes 1% and whose 2nd-pass q does not,
        /// producing a 1-protein delta in the Stage 7 picked-protein output cross-impl.</para>
        ///
        /// <para>A file with no readable sidecar is reported, not skipped silently: every input
        /// file has one declared as an output of this task, so on the post-write pass an absent
        /// file means the write failed. On the pre-write pass a first run that has never
        /// written one is the one legitimate absence, and it is not a fault - the entries
        /// already hold the values that run is about to write.</para>
        /// </summary>
        /// <summary>
        /// The second pass's EXPERIMENT-scope records for this run, from whichever source holds
        /// them. Handed to the per-file overlay so each entry gets them only where that file's
        /// own 2nd-pass sidecar carries a record for it.
        ///
        /// <para>TWO sources, because there are two ways to arrive here. When a pass-2 path
        /// actually computed, it published a <see cref="Pass2ExperimentScope"/> and the values
        /// are in memory. When nothing was recomputed - a resume that adopted standing 2nd-pass
        /// sidecars - there is no accumulator, and they come off the 2nd-pass experiment sidecar
        /// the earlier run left on disk. Before the v5 split both cases were served by the same
        /// mechanism, because the values rode in the per-file sidecar this overlays; they no
        /// longer do, so the resume case needs its own read or the entries silently keep
        /// pre-competition values.</para>
        /// </summary>
        private static IReadOnlyDictionary<uint, FdrExperimentRecord> ResolvePass2ExperimentRecords(
            PipelineContext ctx)
        {
            return ctx.TryGet<Pass2ExperimentScope>(out var scope)
                ? scope.Accumulator.Records
                : LoadExperimentRecords(ctx.Config, FdrScoresSidecar.Pass.SecondPass);
        }

        /// <summary>
        /// A DEFERRED resolution of <see cref="ResolvePass2ExperimentRecords"/>, to be handed to
        /// every consumer that should share ONE answer.
        ///
        /// <para>Deferred because WHEN it resolves is a correctness question: Stage 7 crosses the
        /// no-scope / scope boundary partway through - protein FDR publishes and writes - so a
        /// consumer installed before that point must not capture the earlier answer. Shared
        /// because resolving it twice on a path that has no scope deserializes the whole
        /// analysis-wide sidecar twice and holds two copies of it; on the 446-run CHS cohort
        /// that is 1,239,178 records, about 100 MB, per copy.</para>
        ///
        /// <para>One instance per group of consumers that must agree, created by the caller, so
        /// how many times an analysis deserializes this file is a visible property of the call
        /// site rather than a hidden one. The pre-write and post-write reloads inside
        /// <see cref="ComputeAndPersist"/> each take their own, deliberately: they sit on
        /// opposite sides of that boundary and must NOT share.</para>
        /// </summary>
        internal static Lazy<IReadOnlyDictionary<uint, FdrExperimentRecord>>
            LazyPass2ExperimentRecords(PipelineContext ctx)
        {
            return new Lazy<IReadOnlyDictionary<uint, FdrExperimentRecord>>(
                () => ResolvePass2ExperimentRecords(ctx));
        }

        /// <summary>
        /// The RESIDENT sibling of <see cref="InstallStreamedPass2Overlay"/>: overlay every
        /// file's second-pass sidecar onto a resident survivor pool. A no-op on the streamed
        /// arm, where the installed per-run overlay already does it.
        ///
        /// <para>Needed by the pass-2 diagnostics fold and by nothing else. Every other caller
        /// arrives here having just run <see cref="ComputeAndPersist"/>, which stamps the
        /// second-pass values onto the resident entries as it computes them; the fold skips
        /// that compute by definition, so on the resident arm its pool would otherwise still
        /// carry the FIRST pass's q-values - and the report would describe pass 1 while
        /// labelling it pass 2. That failure is invisible to every other check: the page is
        /// complete, every card is populated, and the numbers are real, just from the wrong
        /// pass. Only a byte-comparison against the flag-up-front report catches it, which is
        /// why P16 makes that comparison half of the requirement.</para>
        /// </summary>
        internal static void OverlayPass2OntoResidentPool(
            PipelineContext ctx, RescoredEntries rescored, string taskName, string taskValidityKey,
            Lazy<IReadOnlyDictionary<uint, FdrExperimentRecord>> experimentRecords)
        {
            if (rescored.Streams)
                return;
            var writer = new Pass2SidecarWriter(ctx.Config, taskName, taskValidityKey);
            ReloadPass2Sidecars(ctx, writer, rescored.Value, @"diagnostics-fold", experimentRecords);
        }

        private static void ReloadPass2Sidecars(
            PipelineContext ctx,
            Pass2SidecarWriter writer,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            string phase,
            Lazy<IReadOnlyDictionary<uint, FdrExperimentRecord>> lazyExperimentRecords)
        {
            int filesReloaded = 0;
            int filesMissing = 0;
            // Resolved once for the whole reload; the overlay applies them per file, to the
            // records that file's own sidecar carries (format v5, issue #4486).
            var experimentRecords = lazyExperimentRecords.Value;
            // Per-file progress: reads back every file's sidecar and rebuilds an entry_id map
            // over that file's survivors. Silent, and the second half of the 38s gap between
            // the competition's [STAGE-WALL] line and the next probe (#4486); the write loop is
            // the first half.
            using (var reloadProgress = new ProgressReporter(
                CountText.Format(perFileEntries.Count, OspreyTasksResources.Pass2FdrSidecar_ReloadPass2Sidecars_Checking_second_pass_intermediate_files_for_1_file,
                    OspreyTasksResources.Pass2FdrSidecar_ReloadPass2Sidecars_Checking_second_pass_intermediate_files_for__0__files),
                perFileEntries.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                long nReloadReported = 0;
                var byEntryId = new Dictionary<uint, FdrEntry>();
                foreach (var kvp in perFileEntries)
                {
                    reloadProgress.Report(++nReloadReported);
                    if (OverlayPass2SidecarOntoFile(
                            writer, kvp.Key, kvp.Value, experimentRecords, ctx.LogWarning, byEntryId))
                    {
                        filesReloaded++;
                    }
                    else
                    {
                        filesMissing++;
                    }
                }
            }
            if (filesReloaded > 0)
            {
                ctx.LogVerbose(CountText.Format(filesReloaded + filesMissing,
                    OspreyTasksResources.Pass2FdrSidecar_ReloadPass2Sidecars_Reloaded_second_pass_FDR_scores___1___for_1_file,
                    OspreyTasksResources.Pass2FdrSidecar_ReloadPass2Sidecars_Reloaded_second_pass_FDR_scores___1___for__2__of__0__files,
                    phase, filesReloaded));
            }
        }

        /// <summary>
        /// Overlay ONE run's <c>.2nd-pass.fdr_scores.bin</c> - plus the analysis-wide 2nd-pass
        /// experiment records - onto that run's entries. The body of
        /// <see cref="ReloadPass2Sidecars"/>' loop, extracted because a STREAMED Stage 7 has to
        /// apply it once per run per pass rather than once per run in total: the entries are
        /// rebuilt from disk carrying their 1st-pass values, so without this every fold after
        /// the second pass would read pass-1 q-values off pass-2 rows.
        ///
        /// <para>Returns false when the run has no readable current sidecar - the caller decides
        /// whether that is the legitimate absence (a first run with no rescore work) or the
        /// failed write it is on the post-write pass, which is why the disposition is not taken
        /// here.</para>
        ///
        /// <para>A missing sidecar leaves the entries EXACTLY as they arrived rather than
        /// resetting them. On the resident path that is the standing first-pass state, which is
        /// what the loop's own contract says such a run keeps; on the streamed path it is the
        /// same state, freshly rebuilt. The two agree because neither invents a value.</para>
        /// </summary>
        /// <summary>
        /// Overlay one file's 2nd-pass FDR sidecar onto its entries, returning false when the file
        /// has no current sidecar (the caller reports it and the run keeps 1st-pass q-values).
        ///
        /// <para>The join index is the CALLER's, reused across files and cleared here. It used to be allocated
        /// per call and sized to the file (<c>new Dictionary&lt;uint, FdrEntry&gt;(entries.Count)</c>),
        /// which at cohort scale is a ~4.2 M-entry bucket and entry array on the large-object heap
        /// per file - and this overlay is a post-materialize hook, so it runs once per file per
        /// STREAMED PASS: three times over a 446-run cohort in SecondPassFDR alone. Clearing keeps
        /// the capacity of the largest file seen and allocates nothing after it, which is the
        /// answer <see cref="Pass1ScalarSeeder"/> already gives for its own per-file buffers.</para>
        /// </summary>
        private static bool OverlayPass2SidecarOntoFile(
            Pass2SidecarWriter writer, string fileName, List<FdrEntry> entries,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            Action<string> logWarning, Dictionary<uint, FdrEntry> byEntryId)
        {
            string inputFile = writer.InputFor(fileName);
            if (inputFile == null)
                return false;
            string pass2Path = FdrScoresSidecar.Pass2Path(inputFile);
            if (!FdrScoresSidecar.IsCurrentFormat(pass2Path, FdrScoresSidecar.Pass.SecondPass))
                return false;
            byEntryId.Clear();
            foreach (var e in entries)
                byEntryId[e.EntryId] = e;
            if (FdrScoresSidecar.TryReadOverlay(
                    pass2Path, byEntryId, FdrScoresSidecar.Pass.SecondPass, experimentRecords))
            {
                return true;
            }
            logWarning(string.Format(
                OspreyTasksResources.Pass2FdrSidecar_OverlayPass2SidecarOntoFile_Failed_to_reload_the_second_pass_intermediate_file_for___0_____1____protein_FDR_will_use_, fileName, pass2Path));
            return false;
        }

        /// <summary>
        /// Make every fold that runs AFTER the second pass see the second pass's answer, on a
        /// Stage 7 whose runs are rebuilt from disk one at a time (#4486).
        ///
        /// <para>On the resident pool <see cref="ReloadPass2Sidecars"/> stamps the entries once
        /// and every later pass reads the stamps. A streamed pool has no entries to stamp
        /// between passes, so the same overlay is installed as a per-run hook and re-applied to
        /// each run as it is rebuilt. Same operation, same rows, same result - once per run per
        /// pass instead of once per run, which is the price of not holding the pool.</para>
        ///
        /// <para>The experiment records arrive as a DEFERRED resolution the caller owns - see
        /// <see cref="LazyPass2ExperimentRecords"/> for why resolving it once, late, and shared
        /// with the fold arm's other consumers is what this needs. Resolving it at install time
        /// on the join path would capture the records before the second-pass competition
        /// publishes its scope, freezing pre-competition values into every later fold.</para>
        /// </summary>
        internal static void InstallStreamedPass2Overlay(
            PipelineContext ctx, RescoredEntries rescored, string taskName, string taskValidityKey,
            Lazy<IReadOnlyDictionary<uint, FdrExperimentRecord>> experimentRecords)
        {
            if (!rescored.Streams)
                return;
            var writer = new Pass2SidecarWriter(ctx.Config, taskName, taskValidityKey);
            // ONE join index per thread rather than per file: the cohort does not pay a
            // large-object dictionary per file per pass. Per thread, not one shared: StreamFiles
            // prepares several files at once on its lanes, each overlay on its own lane thread.
            var byEntryId = new ThreadLocal<Dictionary<uint, FdrEntry>>(() => new Dictionary<uint, FdrEntry>());
            rescored.AddPostMaterialize((fileName, entries) =>
                OverlayPass2SidecarOntoFile(
                    writer, fileName, entries, experimentRecords.Value, ctx.LogWarning, byEntryId.Value));
        }

        /// <summary>
        /// Re-seeds ONE file's survivors from that file's <c>.1st-pass.fdr_scores.bin</c>, and
        /// carries the run-wide counts the summary reports.
        ///
        /// <para>Per file because the frozen streamed second pass seeds each file inside its own
        /// materialization - it never holds the whole pool to walk (#4486) - while the whole-pool
        /// caller loops over this. One object either way, so the two routes cannot drift on what
        /// they seed or on what a missing sidecar means.</para>
        ///
        /// <para>ONE index and ONE staging buffer for the whole run, cleared per file rather than
        /// reallocated. At cohort scale both back onto arrays far past the 85 KB Large Object Heap
        /// threshold - a 257-file CHS run stages ~533 K records per file - and the LOH is swept
        /// only on a gen2 collection, so a fresh pair per file left roughly 125 MB of dead buffers
        /// standing each time. Over 257 files that accumulated +24 GB and WAS the global memory
        /// peak of the run (65.2 GB managed), dwarfing the pass-2 work it feeds. Clear() keeps the
        /// capacity, so the steady state is one file's worth of buffer instead of the whole
        /// cohort's.</para>
        /// </summary>
        /// <summary>
        /// Read one pass's analysis-wide EXPERIMENT-scope records, keyed by entry_id (format v5,
        /// issue #4486). Returns an EMPTY map when the analysis names no such sidecar or never
        /// wrote one, so callers look up unconditionally and an absent entry takes the same
        /// defaults an entry that competed in nothing would carry.
        ///
        /// <para>A sidecar that EXISTS but cannot be read is a STOP, not an empty map. The two
        /// used to be the same answer, and that tolerance is how an HPC chain once ran to
        /// completion on wrong inputs: every consumer applies these values through a
        /// <c>TryGetValue</c>, so an empty map is indistinguishable from one that simply holds
        /// no matching entry, and every entry then keeps its <c>ResetScores</c> defaults - an
        /// <c>ExperimentAggregateScore</c> of 0.0 that <c>BuildCoAssignment</c> takes a MINIMUM
        /// over, collapsing a run-wide acceptance boundary. No return value makes a truncated,
        /// wrong-version or wrong-pass file safe, so the run fails instead.</para>
        /// </summary>
        private static IReadOnlyDictionary<uint, FdrExperimentRecord> LoadExperimentRecords(
            OspreyConfig config, FdrScoresSidecar.Pass pass)
        {
            string path = FdrExperimentSidecar.PathFor(config?.OutputBlib,
                ScoringTaskShared.ArtifactSiblingPath(config), pass);
            return LoadExperimentRecordsCached(path, pass);
        }

        /// <summary>
        /// One deserialization per GENERATION of the experiment sidecar, rather than one per
        /// call, keyed on the file's identity (path, length, last write) so a rewrite invalidates
        /// the cache on its own.
        ///
        /// <para>The caller that needs this is the streamed pass-2 overlay: it resolves the
        /// records inside a per-run hook, deliberately (see
        /// <see cref="InstallStreamedPass2Overlay"/> - Stage 7 crosses the
        /// no-scope-to-scope boundary partway through, so capturing once would freeze
        /// pre-competition values into every later fold). On a resume where the competition is
        /// skipped and no <c>Pass2ExperimentScope</c> is ever published, that per-call resolution
        /// falls through to disk for EVERY run of EVERY <c>StreamFiles</c> pass - and Stage 7
        /// makes many. At 446 runs that is O(runs x sidecar) deserialization of a file with 1.24 M
        /// records, which is the shape this whole area exists to eliminate.</para>
        ///
        /// <para>Caching on identity rather than hoisting the call is what keeps the documented
        /// semantics intact: protein FDR REWRITES this sidecar mid-Stage-7, and a rewrite changes
        /// length or write time, so the next resolve re-reads. A plain hoist would have frozen the
        /// pre-competition answer, which is the bug the per-call resolution was written to avoid.
        /// One <c>FileInfo</c> stat per call replaces one full deserialization per call.</para>
        /// </summary>
        private static readonly object EXPERIMENT_CACHE_LOCK = new object();
        private static string _experimentCacheKey;
        private static IReadOnlyDictionary<uint, FdrExperimentRecord> _experimentCacheValue;

        private static IReadOnlyDictionary<uint, FdrExperimentRecord> LoadExperimentRecordsCached(
            string path, FdrScoresSidecar.Pass pass)
        {
            string key = null;
            if (!string.IsNullOrEmpty(path))
            {
                var info = new FileInfo(path);
                // A missing file gets no cache entry: LoadExperimentRecordsFrom owns that
                // degrade, and caching "absent" would outlive the write that fixes it.
                if (info.Exists)
                {
                    key = string.Format(@"{0}|{1}|{2}|{3}", path, info.Length,
                        info.LastWriteTimeUtc.Ticks, (int)pass);
                }
            }
            if (key == null)
                return LoadExperimentRecordsFrom(path, pass);
            lock (EXPERIMENT_CACHE_LOCK)
            {
                if (string.Equals(_experimentCacheKey, key, StringComparison.Ordinal))
                    return _experimentCacheValue;
            }
            var loaded = LoadExperimentRecordsFrom(path, pass);
            lock (EXPERIMENT_CACHE_LOCK)
            {
                _experimentCacheKey = key;
                _experimentCacheValue = loaded;
            }
            return loaded;
        }

        /// <summary>
        /// Whether <paramref name="inputFile"/>'s 2nd-pass scores - and under protein-compact its
        /// competition decoys - are the worker answer for the reconciled parquet beside them.
        /// Kept in one place so every reader of "is this run answered" asks the same question:
        /// PerFileRescoring's resume, SecondPassFDR's check before it folds, and the training
        /// export's choice of q-values.
        ///
        /// <para>Decided from DISK, not from a published byproduct (#4486). Stage 6 and Stage 7
        /// are separate PROCESSES in an HPC chain, so anything published in one is simply absent
        /// in the other.</para>
        ///
        /// <para>Every artifact carries the stamp of the <c>PerFileRescoring</c> run that wrote
        /// it, and they must carry the SAME one. Existence alone is not enough: an earlier run
        /// leaves the same files behind. The reconciled parquet is rewritten by every
        /// PerFileRescoring run whose key changed, so an answer left by an earlier run under
        /// another key no longer matches it. Comparing the stamps to each other, rather than to
        /// PerFileRescoring's key recomputed in this process, is what keeps the test valid on a
        /// separate HPC leg - that key folds in a per-leg flag
        /// (<c>LibraryFragmentRelease.ValidityKeySuffix</c>), so a SecondPassFDR process cannot
        /// reconstruct it.</para>
        ///
        /// <para>The decoys exist only where a competition produced them. The transfer competes
        /// nothing and writes none, so requiring them there would leave every transfer run
        /// permanently unanswered.</para>
        /// </summary>
        internal static bool HasWorkerStamp(string inputFile)
        {
            var reconciled = ParquetScoreCache.ReadStamp(ParquetScoreCache.GetReconciledScoresPath(inputFile));
            if (reconciled == null ||
                !string.Equals(reconciled.Task, PerFileRescoreTask.TASK_NAME, StringComparison.Ordinal) ||
                !string.Equals(reconciled.Version, OspreyVersion.Current, StringComparison.Ordinal))
            {
                return false;
            }
            if (!IsSameStamp(FdrScoresSidecar.ReadStamp(FdrScoresSidecar.Pass2Path(inputFile), FdrScoresSidecar.Pass.SecondPass), reconciled))
                return false;
            return !OspreyEnvironment.Pass2ProteinCompact ||
                   IsSameStamp(Pass2CompetitionDecoys.ReadStamp(Pass2CompetitionDecoys.PathFor(inputFile)), reconciled);
        }

        private static bool IsSameStamp(ArtifactStamp stamp, ArtifactStamp other)
        {
            return stamp != null &&
                   string.Equals(stamp.Task, other.Task, StringComparison.Ordinal) &&
                   string.Equals(stamp.Version, other.Version, StringComparison.Ordinal) &&
                   string.Equals(stamp.Key, other.Key, StringComparison.Ordinal);
        }

        /// <summary>
        /// On a per-file competition disagreement, write THIS pass's answer beside the worker's
        /// so the two can be diffed directly (#4486).
        ///
        /// <para>The worker's sidecar is immutable and is the evidence; this goes to a NEW path
        /// rather than over it. Without this the failure leaves one side on disk and the other
        /// only in memory, and the first thing anyone would do is spend a run reproducing what
        /// the failing process was already holding.</para>
        ///
        /// <para>Best-effort by design. This runs while an exception is in flight and its only
        /// job is to improve the diagnosis; a failure to write the dump must not replace the
        /// disagreement with an error about the dump. The path is logged either way, because a
        /// dump nobody can find is not a diagnostic.</para>
        /// </summary>
        private static void DumpRecomputedForDiff(
            PipelineContext ctx, Pass2SidecarWriter writer, string fileKey,
            IReadOnlyList<FdrEntry> entries)
        {
            if (fileKey == null || entries == null)
                return;
            string inputFile = writer.InputFor(fileKey);
            if (inputFile == null)
                return;
            string dumpPath = FdrScoresSidecar.Pass2Path(inputFile) + @".recomputed";
            try
            {
                FdrScoresSidecar.Write(dumpPath, entries, FdrScoresSidecar.Pass.SecondPass, writer.Stamp);
                ctx.LogWarning(string.Format(
                    @"Second-pass competition disagreement on '{0}': wrote this pass's " +
                    @"recomputed answer to {1} for diffing against the worker's sidecar at {2}.",
                    fileKey, dumpPath, FdrScoresSidecar.Pass2Path(inputFile)));
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                ctx.LogWarning(string.Format(
                    @"Second-pass competition disagreement on '{0}': could not write the " +
                    @"recomputed answer to {1} for diffing: {2}",
                    fileKey, dumpPath, ex.Message));
            }
        }

        /// <summary>
        /// The AUTHORITATIVE per-file competition: the one the rescore worker computed and wrote
        /// into this file's per-run 2nd-pass sidecar (#4486). Stage 7 folds this; its own
        /// recomputation exists only to check it, and goes away with this transition.
        ///
        /// <para>Every run has one (#4665), checked current against the reconciled parquet
        /// beside it before the fold began, so a resumed run cannot fold an EARLIER run's
        /// answer as this one's. Every failure below is a throw.</para>
        /// </summary>
        /// <summary>
        /// Put the worker's answer onto this file's entries: the composite score each pool entry
        /// competed on, plus the two experiment-scope scalars the 1st-pass seed used to supply.
        ///
        /// <para>THIS IS WHAT LETS STAGE 7 STOP READING PER-FILE 1st-PASS INPUTS. Before, the
        /// score arrived by a two-step re-derivation: seed the 1st-pass value from that file's
        /// <c>.1st-pass.fdr_scores.bin</c>, then overwrite it for every survivor by reloading PIN
        /// features from the reconciled parquet and re-running the frozen model. The worker had
        /// already done exactly that computation and written the result down; the join was
        /// repeating it because nothing handed the records over.</para>
        ///
        /// <para><c>Pep</c> and <c>ExperimentAggregateScore</c> come from the ANALYSIS-WIDE
        /// 1st-pass experiment sidecar, which is where the Phase 1a scope split put them. The
        /// per-file 1st-pass record was never their source - it was only the loop key - so
        /// iterating the ENTRIES instead removes the last reason to open that file.</para>
        /// </summary>
        private static void ApplyWorkerScores(
            IReadOnlyList<FdrEntry> entries, IReadOnlyList<FdrScoreRecord> records,
            Pass1ScalarSeeder seeder)
        {
            if (entries == null || records == null)
                return;
            var byEntryId = new Dictionary<uint, FdrEntry>(entries.Count);
            foreach (var e in entries)
                byEntryId[e.EntryId] = e;
            foreach (var rec in records)
            {
                // A record with no entry is not an error here: the sidecar is the file's POOL
                // image, and a --task run may hold a subset. The reverse - an entry with no
                // record - is caught by AssertSidecarDescribesPool on the writing side.
                if (byEntryId.TryGetValue(rec.EntryId, out FdrEntry entry))
                    entry.Score = rec.Score;
            }
            seeder.SeedExperimentScalars(entries);
        }

        // `records` hands the worker's per-run records back so the caller can APPLY them to its
        // entries rather than re-deriving the same values. They carry the composite score each
        // pool entry competed on, which is what Stage 7 needs downstream; re-deriving it cost a
        // reconciled-parquet feature reload and a frozen rescore per survivor, on top of the
        // 1st-pass sidecar read that supplied the fallback seed.
        private static StreamingFdr.FileCompetition ReadWorkerContribution(
            Pass2SidecarWriter writer, string fileKey,
            HashSet<uint> stratumBaseIds, out List<FdrScoreRecord> records)
        {
            // Every run's answer was checked present and current before the fold began
            // (RequireWorkerAnswers), so each failure below is a throw rather than a fallback.
            string inputFile = writer.InputFor(fileKey);
            if (inputFile == null)
            {
                throw new InvalidOperationException(string.Format(
                    @"Second-pass worker verification for '{0}': the worker reported writing a " +
                    @"sidecar for this file, but it matches no configured input file. See issue #4486.",
                    fileKey));
            }
            string pass2Path = FdrScoresSidecar.Pass2Path(inputFile);
            var recordsRead = new List<FdrScoreRecord>();
            if (!FdrScoresSidecar.ReadRecords(
                    pass2Path, FdrScoresSidecar.Pass.SecondPass, rec => recordsRead.Add(rec)))
            {
                throw new InvalidOperationException(string.Format(
                    @"Second-pass worker verification for '{0}': the worker's sidecar could not " +
                    @"be read back from {1}. Absence is not a pass - it is the one outcome that " +
                    @"would let the move ship unverified. See issue #4486.",
                    fileKey, pass2Path));
            }
            // The DECOY side comes from the artifact the worker wrote it to, never from the
            // records. A non-survivor decoy is not a pool member, so the pool image cannot carry
            // the observation that won its base_id - the competition runs over the file's
            // pre-compaction population (issue #4436), which is exactly the point.
            //
            // Absence is a stop. Deriving the decoy bests from the pool anyway is what the
            // previous shape did, and it produced a null missing real decoy observations: every
            // experiment q computed against it moved (113,552 entries measured on Stellar), with
            // every correctness leg green because none of the movement crossed the 1% cutoff.
            // There is no gate that can see this, so a missing file has to fail the run.
            string decoysPath = Pass2CompetitionDecoys.PathFor(inputFile);
            var bestDecoy = Pass2CompetitionDecoys.ReadMap(decoysPath);
            if (bestDecoy == null)
            {
                throw new InvalidOperationException(string.Format(
                    @"Second-pass worker verification for '{0}': the worker's per-run sidecar is " +
                    @"present, but the decoy side of its competition could not be read from {1}. " +
                    @"The pool image cannot supply it - a non-survivor decoy holds no pool row - " +
                    @"so folding without it would compute every experiment q against a " +
                    @"decoy-depleted null. See issue #4486.",
                    fileKey, decoysPath));
            }
            records = recordsRead;
            return FileCompetitionFromRecords(
                recordsRead, stratumBaseIds, bestDecoy, LoadGapFillEntryIds(inputFile));
        }

        /// <summary>
        /// This file's GAP-FILL entry ids, from the <c>gap_fill_targets</c> the join node already
        /// persists in its <c>.reconciliation.json</c>. Empty when the envelope is absent or
        /// unreadable, which is the correct degrade: a file with no envelope had no gap-fill
        /// planned for it.
        ///
        /// <para><b>Why the sidecar cannot answer this itself.</b> A gap-filled peak is a POOL
        /// member that never COMPETED - the per-file competition takes its population from the
        /// file's 1st-pass sidecar, where a gap-fill has no record by definition. The per-run
        /// 2nd-pass sidecar is the pool image, so it necessarily contains rows the competition
        /// never saw, and nothing in a record distinguishes them. That distinction used to live
        /// only in memory, on the machine that recomputed the competition; a separate
        /// experiment-wide node has to read it from somewhere (issue #4486).</para>
        ///
        /// <para>It is read from the envelope rather than from the reconciled parquet's
        /// <c>score_index</c> + <c>osprey.scores_row_count</c> footer - the other discriminator
        /// Phase 1 left for exactly this question - because this list is ~200 entries against a
        /// 311K-row column read, and because the envelope is already the artifact whose join-wide
        /// hash this stage validates against.</para>
        /// </summary>
        private static HashSet<uint> LoadGapFillEntryIds(string inputFile)
        {
            var ids = new HashSet<uint>();
            try
            {
                string reconPath = ReconciliationFile.PathForInput(inputFile);
                if (!File.Exists(reconPath))
                    return ids;
                var envelope = ReconciliationFile.Load(reconPath);
                if (envelope?.GapFillTargets == null)
                    return ids;
                foreach (var g in envelope.GapFillTargets)
                    ids.Add(g.TargetEntryId);
            }
            catch (Exception)
            {
                // An unreadable envelope leaves the set empty, which reproduces the pre-#4486
                // behaviour of folding every record. The competition assert downstream is what
                // turns that into a visible failure rather than a silent one.
                return ids;
            }
            return ids;
        }

        /// <summary>
        /// The analysis-wide 1st-pass experiment-scope records, for a caller outside this class
        /// that needs the same map on the same terms - today the rescore worker's
        /// <see cref="Pass1ScalarSeeder"/> (issue #4486). Deliberately the SAME entry point Stage
        /// 7 uses rather than a second reader, so the two stages cannot come to different
        /// conclusions about a missing or unreadable sidecar.
        /// </summary>
        internal static IReadOnlyDictionary<uint, FdrExperimentRecord> LoadPass1ExperimentRecords(
            OspreyConfig config)
        {
            return LoadExperimentRecords(config, FdrScoresSidecar.Pass.FirstPass);
        }

        /// <summary>
        /// The path-taking half of <see cref="LoadExperimentRecords"/>, split out so the
        /// absent-versus-unreadable distinction is testable without standing up an
        /// <see cref="OspreyConfig"/> and an artifact tree around it.
        /// </summary>
        internal static IReadOnlyDictionary<uint, FdrExperimentRecord> LoadExperimentRecordsFrom(
            string path, FdrScoresSidecar.Pass pass)
        {
            // No path names no artifact, and no file on disk means this analysis never wrote
            // one. Both are "the analysis has none", which the callers' defaults cover. Testing
            // existence SEPARATELY is what makes them distinguishable from a file that is there
            // and unreadable: ReadMap answers null to all three, by design, because it cannot
            // know which of them its caller can tolerate.
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new Dictionary<uint, FdrExperimentRecord>();
            var map = FdrExperimentSidecar.ReadMap(path, pass);
            if (map == null)
            {
                // Not read as empty: that would leave every entry on its reset defaults (an
                // experiment aggregate score of 0, an experiment q of 1) and report those as
                // computed values (issue #4486).
                throw new InvalidOperationException(string.Format(
                    pass == FdrScoresSidecar.Pass.FirstPass
                        ? OspreyTasksResources.Pass2FdrSidecar_LoadExperimentRecordsFrom_The_whole_experiment_first_pass_intermediate_file_exists_but_could_not_be_read___0_
                        : OspreyTasksResources.Pass2FdrSidecar_LoadExperimentRecordsFrom_The_whole_experiment_second_pass_intermediate_file_exists_but_could_not_be_read___0_,
                    path));
            }
            return map;
        }

        internal sealed class Pass1ScalarSeeder
        {
            /// <summary>
            /// Report what one or more seeders restored. The rescore worker aggregates its
            /// per-thread seeders and calls this once; a single seeder calls it through
            /// <see cref="LogSummary"/>.
            ///
            /// <para>There is no unreadable-file case to report any more. The one caller that
            /// read a 1st-pass sidecar here and could find it unreadable was the whole-pool seed
            /// Stage 7 ran outside protein-compact, which went with #4665; every remaining caller
            /// hands over records it has already read cleanly, or reads none.</para>
            /// </summary>
            public static void LogSeedSummary(PipelineContext ctx, int restored, int filesRead)
            {
                ctx.LogVerbose(CountText.Format(filesRead,
                    OspreyTasksResources.Pass1ScalarSeeder_Restored_the_first_pass_scores_of__1__kept_precursor_candidate_peaks_in_1_file_,
                    OspreyTasksResources.Pass1ScalarSeeder_Restored_the_first_pass_scores_of__1__kept_precursor_candidate_peaks_across__0__files_,
                    restored));
            }

            /// <summary>
            /// The analysis-wide 1st-pass EXPERIMENT-scope records (format v5, issue #4486).
            /// <c>ExperimentAggregateScore</c> is one of the three scalars this seeder restores,
            /// and it no longer travels in the per-file record, so the seeder has to hold the
            /// one file that does. Null when the analysis has no experiment sidecar, which
            /// leaves the aggregate at its reset default.
            /// </summary>
            private readonly IReadOnlyDictionary<uint, FdrExperimentRecord> _experimentRecords;

            private Dictionary<uint, FdrEntry> _byEntryId;
            private int _capacity;
            private int _nRestored;
            private int _filesRead;

            /// <param name="capacity">Entries in the largest file, when the caller knows it. A
            /// streamed caller does not and passes 0; the buffers then grow to each new
            /// high-water file, which is a handful of reallocations over a run rather than one
            /// per file. (<c>Dictionary.EnsureCapacity</c> is net8.0-only and this builds net472
            /// too, so growth means a fresh pair rather than a resize in place.)</param>
            /// <param name="experimentRecords">The analysis-wide 1st-pass experiment-scope
            /// records, keyed by entry_id; null when the analysis has none.</param>
            public Pass1ScalarSeeder(int capacity,
                IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords)
            {
                _experimentRecords = experimentRecords;
                Resize(capacity);
            }

            /// <summary>
            /// Seed from records the caller has ALREADY read, rather than reading the sidecar
            /// again. The frozen competition reads each file's 1st-pass sidecar for its own
            /// reasons and hands the survivor records here, so one traversal serves both
            /// (#4486).
            ///
            /// <para>No unreadable case: a caller holding decoded records has already had a clean
            /// read, so there is no partial-callback state to discard.</para>
            /// </summary>
            public void Apply(IReadOnlyList<FdrEntry> entries, IReadOnlyList<FdrScoreRecord> records)
            {
                if (entries.Count > _capacity)
                    Resize(entries.Count);
                _byEntryId.Clear();
                foreach (var e in entries)
                    _byEntryId[e.EntryId] = e;
                int restored = 0;
                foreach (var rec in records)
                {
                    if (!_byEntryId.TryGetValue(rec.EntryId, out FdrEntry entry))
                        continue;
                    ApplyRecord(entry, rec);
                    restored++;
                }
                _filesRead++;
                _nRestored += restored;
            }

            /// <summary>
            /// Survivors this instance restored, for a caller that owns SEVERAL seeders and has
            /// to report them as one run-wide total - the rescore worker keeps one per THREAD.
            /// </summary>            /// <summary>Survivors this instance restored, for the same aggregation.</summary>
            public int Restored
            {
                get { return _nRestored; }
            }

            /// <summary>Files this instance seeded, for the same aggregation.</summary>
            public int FilesRead
            {
                get { return _filesRead; }
            }

            public void LogSummary(PipelineContext ctx)
            {
                LogSeedSummary(ctx, _nRestored, _filesRead);
            }

            /// <summary>
            /// Copy the three scalars <c>ResetScores</c> clears that no frozen 2nd-pass mode
            /// writes back, from one 1st-pass record onto its entry.
            ///
            /// <para>ExperimentProteinQvalue is deliberately NOT seeded: its pass-2 producer is
            /// <see cref="WritePass2ExperimentSidecar"/>. The second-pass protein FDR writes the
            /// second-pass value onto the entry, and the second-pass experiment sidecar records
            /// it (#4559); seeding a pass-1 value made that the value on every route. The other
            /// three land in the 2nd-pass artifacts at their reset defaults for every peak
            /// Stage 6 touched, which is the population this repairs.</para>
            ///
            /// <para>Two sources, because the three scalars no longer share one record: Score
            /// and Pep are RUN-scope and come from the file's own sidecar record, while the
            /// experiment aggregate is a property of the entry for the whole analysis and comes
            /// from the experiment sidecar (format v5, issue #4486).</para>
            /// </summary>
            private void ApplyRecord(FdrEntry entry, FdrScoreRecord rec)
            {
                entry.Score = rec.Score;
                // PEP comes from the EXPERIMENT record beside the aggregate, not from the
                // per-run record: it is one value per entry_id for the whole analysis, and the
                // per-run sidecars stopped carrying it with issue #4486.
                if (_experimentRecords != null &&
                    _experimentRecords.TryGetValue(rec.EntryId, out var exp))
                {
                    entry.Pep = exp.Pep;
                    entry.ExperimentAggregateScore = exp.ExperimentAggregateScore;
                }
            }

            /// <summary>
            /// Seed the two EXPERIMENT-scope scalars straight onto the entries, without opening
            /// any per-file 1st-pass sidecar.
            ///
            /// <para>The scope split (issue #4486) already moved <c>Pep</c> and
            /// <c>ExperimentAggregateScore</c> into the analysis-wide experiment sidecar, so the
            /// per-file record that <see cref="Apply"/> iterates has not been their source for a
            /// while - it is only the loop key. Keying off the ENTRIES instead is therefore not a
            /// weaker seed, it is the same values reached without the file. That is what lets the
            /// shipped Stage 7 path stop depending on per-run 1st-pass sidecars entirely.</para>
            ///
            /// <para><c>Score</c> is deliberately NOT set here: on this path it comes from the
            /// worker's 2nd-pass record, which is the score the entry actually competed on.
            /// <see cref="Apply"/>'s 1st-pass seed existed only as the fallback under a rescore
            /// that no longer runs.</para>
            /// </summary>
            public void SeedExperimentScalars(IReadOnlyList<FdrEntry> entries)
            {
                if (_experimentRecords == null || entries == null)
                    return;
                int restored = 0;
                foreach (var e in entries)
                {
                    if (!_experimentRecords.TryGetValue(e.EntryId, out var exp))
                        continue;
                    e.Pep = exp.Pep;
                    e.ExperimentAggregateScore = exp.ExperimentAggregateScore;
                    restored++;
                }
                _filesRead++;
                _nRestored += restored;
            }

            private void Resize(int capacity)
            {
                _capacity = capacity;
                _byEntryId = new Dictionary<uint, FdrEntry>(capacity);
            }
        }

        /// <summary>
        /// Patch every file's <c>.2nd-pass.fdr_scores.bin</c> with the SECOND-pass
        /// <see cref="FdrEntry.ExperimentProteinQvalue"/>, so that column is a pass-2 value like
        /// every other column in that file (issue #4559).
        ///
        /// <para>Must be called AFTER the second-pass protein FDR has propagated onto the
        /// entries (<c>SecondPassFdrTask.RunProteinFdr</c>), which is necessarily after the
        /// sidecar itself is written - the sidecar write feeds that protein FDR. Hence a patch
        /// rather than a reordering: the same two-phase shape the first-pass path already uses,
        /// where the score pass writes a placeholder and
        /// <c>FdrScoresSidecar.PatchProteinQvalues</c> fills the column in once the protein FDR
        /// is known.</para>
        ///
        /// <para>Why the column was a pass-1 value before: no pass-2 q-value mode writes a
        /// protein q at all - the first- and second-pass protein FDRs are its only producers -
        /// so the value present at sidecar-write time was whatever pass 1 left on the stub. Both
        /// routes copied it identically, which is why no two-route comparison could see it; the
        /// guard that can is <c>Test-Pass2ProteinQvalue</c> in
        /// <c>Regression/FdrSidecars.ps1</c>.</para>
        ///
        /// <para>One file's map is resident at a time, released before the next. A file with no
        /// 2nd-pass sidecar is skipped silently: the sidecar exists only where Stage 6 produced
        /// a reconciled parquet, and the caller runs on the same condition.</para>
        /// </summary>
        internal static void WritePass2ExperimentSidecar(
            PipelineContext ctx,
            IReadOnlyList<string> fileNames,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            IReadOnlyDictionary<string, double> peptideQvalues,
            ArtifactStamp stamp)
        {
            var inputByName = new Dictionary<string, string>();
            foreach (var inputFile in ctx.Config.InputFiles)
                inputByName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;

            // The three EXPERIMENT-scope columns the second pass already computed, handed over
            // by the pass that computed them. Absent only when no pass-2 path ran, in which case
            // there is nothing to write.
            if (!ctx.TryGet<Pass2ExperimentScope>(out var scope))
            {
                // ABSENCE IS A STOP, on every mode that computes a second pass. This used to
                // log-and-return, which is how OSPREY_PASS2_QVALUE=transfer silently produced no
                // experiment sidecar for as long as nothing ran that arm - a whole artifact
                // missing, reported at verbose level. Every second-pass path now publishes a
                // scope, so reaching here means one stopped doing so.
                //
                // This is the gate for that class of defect, and it costs nothing: it runs on
                // every leg of every dataset already in the suite, rather than needing an arm of
                // its own. --task ModelDiagnostics is excluded by its caller, whose contract is
                // that it touches no artifact but the report.
                throw new InvalidOperationException(
                    @"No second-pass experiment-scope records were published, so the analysis-wide " +
                    @"2nd-pass FDR sidecar cannot be written. Every mode that computes a second " +
                    @"pass must publish this - the competition modes from the fold, and " +
                    @"OSPREY_PASS2_QVALUE=transfer from the carried pass-1 values. See issue #4486.");
            }
            var experiment = scope.Accumulator;

            int filesPatched = 0;
            long nPatched = 0;
            var failed = new List<string>();
            // A heading alone was not enough here: with only the caller's line in place this loop
            // was still a 54 s gap on the --task SecondPassFDR measurement of 2026-08-15. It
            // rewrites one 8-byte field per record over every file's whole 2nd-pass sidecar, so
            // it is per-file work and reports as such.
            int patchIdx = 0;
            using (var progress = new ProgressReporter(
                       CountText.Format(fileNames.Count, OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Writing_protein_q_values_for_1_file,
                           OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Writing_protein_q_values_for__0__files),
                       fileNames.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                foreach (string fileName in fileNames)
                {
                    progress.Report(++patchIdx);
                    if (!inputByName.TryGetValue(fileName, out string inputFile))
                        continue;
                    // ONE gate now. There is no longer a file that legitimately has no 2nd-pass
                    // sidecar - every input file gets one written - so absent and unusable are
                    // the same outcome here and both are reported. The two-gate form this
                    // replaces treated absence as a silent skip, which is precisely the
                    // ambiguity always-writing removes: an absent file could not be told apart
                    // from a write that failed and never committed.
                    if (!FdrScoresSidecar.IsCurrentFormat(
                            FdrScoresSidecar.Pass2Path(inputFile), FdrScoresSidecar.Pass.SecondPass))
                    {
                        failed.Add(fileName);
                        continue;
                    }

                    // entry_id -> protein q, built from the RECONCILED parquet's own
                    // modified_sequence column rather than from the survivor entries. That
                    // column is where an entry's ModifiedSequence came from in the first place,
                    // so the peptide -> q lookup matches by construction - and it is what lets
                    // the write-back happen without the pool (#4486). The map is a SUPERSET of
                    // the sidecar's records, which the patch tolerates: it rewrites only the
                    // records the file holds. A peptide absent from the parsimony result takes
                    // 1.0, exactly as the resident PropagateProteinQvalues did.
                    var byEntryId = new Dictionary<uint, double>();
                    try
                    {
                        // The RECONCILED parquet, derived rather than probed. This runs both
                        // in-process - where the published map holds Stage 4 paths, because
                        // Stage 1-4 ran here - and on a --task SecondPassFDR node, where it
                        // already holds reconciled ones; the derivation is idempotent, so one
                        // expression states the same intent on both routes.
                        string parquetPath = ParquetScoreCache.ReconciledPathFromScoresPath(
                            perFileParquetPaths[fileName]);
                        ParquetScoreCache.ReadFdrStubScalars(parquetPath,
                            (entryId, charge, isDecoy, coelutionSum, modseq, apexRt) =>
                            {
                                double q;
                                if (!peptideQvalues.TryGetValue(modseq ?? string.Empty, out q))
                                    q = 1.0;
                                byEntryId[entryId] = q;
                            },
                            StubColumns.Core);
                    }
                    catch (Exception ex)
                    {
                        ctx.LogWarning(string.Format(
                            OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Could_not_read_the_re_scored_results_for__0____1_,
                            fileName, ex.Message));
                        failed.Add(fileName);
                        continue;
                    }

                    foreach (var kvp in byEntryId)
                        experiment.SetProteinQvalue(kvp.Key, kvp.Value);
                    filesPatched++;
                    nPatched += byEntryId.Count;
                }
            }

            // Reported, not thrown on: nothing in this process reads the 2nd-pass sidecar's
            // protein column, so a failed patch cannot corrupt this run's output. It DOES leave
            // a file whose protein column is not a pass-2 value while its header says pass 2,
            // which is precisely the state #4559 existed to remove - so the warning names that
            // rather than implying the file is merely missing an optional extra.
            if (failed.Count > 0)
            {
                // Each record keeps what it held when the sidecar was written: the reset default
                // for every entry Stage 6 rescored or gap-filled, a pass-1 value only for entries
                // Stage 6 left alone. Any consumer joining on that column reads the wrong pass.
                ctx.LogWarning(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Protein_q_values_could_not_be_written_for__0__files____1____The_second_pass_intermediate_,
                    failed.Count, string.Join(@", ", failed)));
            }
            ctx.LogVerbose(CountText.Format(filesPatched,
                OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Resolved_the_second_pass_protein_q_values_of__1__precursor_candidate_peaks_in_1_file_,
                OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Resolved_the_second_pass_protein_q_values_of__1__precursor_candidate_peaks_across__0__,
                nPatched));

            // The experiment-scope record set is complete now that protein FDR has filled the
            // one column it owns, so write it once beside the blib.
            string experimentPath = FdrExperimentSidecar.PathFor(ctx.Config?.OutputBlib,
                ScoringTaskShared.ArtifactSiblingPath(ctx.Config), FdrScoresSidecar.Pass.SecondPass);
            if (string.IsNullOrEmpty(experimentPath))
            {
                ctx.LogWarning(
                    string.Format(OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_No_output__blib_was_given__so_the_second_pass_experiment_level_q_values_are_not_saved_to_,
                        LibrarySource.EXT_BLIB));
                return;
            }
            try
            {
                FdrExperimentSidecar.Write(experimentPath, experiment.Records,
                    FdrScoresSidecar.Pass.SecondPass, stamp);
                ctx.LogVerbose(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Wrote_experiment_level_FDR_results_for__1__precursor_candidates_to__0_,
                    experimentPath, experiment.Count));
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Failed_to_write__0____1_, experimentPath, ex.Message));
            }
        }

        /// <summary>
        /// The second pass's EXPERIMENT-scope records, published by whichever pass-2 path
        /// computed them and consumed by <see cref="WritePass2ExperimentSidecar"/> after the
        /// second-pass protein FDR has filled in the one column it owns.
        ///
        /// <para>A context byproduct rather than a return value because the two halves run in
        /// different places: the q-values come out of the second pass, the protein q out of the
        /// protein FDR the owning task runs afterwards, and the file cannot be written until
        /// both are in.</para>
        /// </summary>
        internal sealed class Pass2ExperimentScope
        {
            public Pass2ExperimentScope(FdrExperimentAccumulator accumulator)
            {
                Accumulator = accumulator;
            }

            public FdrExperimentAccumulator Accumulator { get; }
        }

        /// <summary>
        /// Run the frozen-model COMPETITION second pass (protein-compact): resolve the frozen
        /// 1st-pass model and its stratum, then hand them to
        /// <see cref="ComputePass2TransferCompeteFull"/>.
        ///
        /// <para>Fail-fast, because an explicitly requested frozen mode must NEVER silently
        /// degrade to the anti-conservative retrain. Absent inputs - the frozen 1st-pass model
        /// or protein stratum are not in this process (a warm rerun that loaded cached scores
        /// and skipped 1st-pass training, or a distributed SecondPassFDR node that never trained
        /// pass 1), or a missing / corrupt 1st-pass sidecar - mean the mode cannot be honored,
        /// so this aborts with actionable guidance rather than reporting looser FDR than a cold
        /// straight-through run under the same mode. There is no longer a retrain to degrade
        /// TO - second-pass retraining was removed with issue #4484 - so an absent input is a
        /// hard stop rather than a quieter answer.</para>
        /// </summary>
        private static void ComputePass2FrozenCompetition(
            PipelineContext ctx,
            RescoredEntries rescored,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            Pass2SidecarWriter writer)
        {
            HashSet<uint> stratum = null;
            bool haveInputs =
                ctx.TryGet<FirstPassPercolatorModel>(out var frozen) && frozen?.Results != null;
            if (haveInputs && OspreyEnvironment.Pass2ProteinCompact)
            {
                haveInputs = ctx.TryGet<ProteinCompactStratum>(out var pcStratum) &&
                             pcStratum?.BaseIds != null && pcStratum.BaseIds.Count > 0;
                if (haveInputs)
                    stratum = pcStratum.BaseIds;
            }
            if (haveInputs && ComputePass2TransferCompeteFull(
                    ctx, rescored, perFileParquetPaths, config, frozen.Results,
                    frozen.ExperimentAgg, writer, stratum))
            {
                return;
            }
            // Absent inputs: the frozen 1st-pass model, 1st-pass scalar sidecars or protein
            // stratum, or a file whose input path could not be resolved - e.g. a warm rerun or a
            // distributed SecondPassFDR node that did not train pass 1 in-process.
            throw new InvalidOperationException(string.Format(
                OspreyTasksResources.Pass2FdrSidecar_ComputePass2FrozenCompetition_Second_pass_FDR_cannot_run__the_saved_first_pass_model__a_first_pass_intermediate_file__,
                OspreyArgNames.Text(OspreyArgNames.TASK)));
        }

        /// <summary>
        /// OSPREY_PASS2_QVALUE=protein-compact. Recompute the reported
        /// precursor q-values + PEP by re-running the target-decoy competition over the ENTIRE
        /// 1st-pass population -- read as SCALARS from each file's persisted
        /// <c>.1st-pass.fdr_scores.bin</c> -- with ONLY the reconciled survivors' scores swapped
        /// in (the FROZEN 1st-pass model applied to their reconciled features). Because &gt;99% of
        /// scores are unchanged, the recomputed q lands on the calibrated 1st-pass value; the
        /// reconciled minority get honest full-population q. No 2nd-pass retrain and no
        /// reduced-pool null (the null is the full 1st-pass decoy set).
        ///
        /// <para>ONE FILE at a time, end to end (#4486). Each file is materialized inside the
        /// competition's own read, seeded with its 1st-pass scalars, scored with the frozen
        /// model, competed, given its run q, written to its <c>.2nd-pass.fdr_scores.bin</c> and
        /// then DROPPED - so this pass never needs the whole-run survivor pool, and nothing it
        /// allocates outlives the file that produced it except flat scalar arrays and O(distinct)
        /// maps. The experiment-scope columns cannot be written there, because the competition
        /// that produces them is only complete once every file has been folded in; they are
        /// patched into the sidecars afterwards, per file, by step 4.</para>
        ///
        /// <para>The sidecar is therefore the CARRIER of this pass's results, not a copy of
        /// them. While the pool is still resident for other consumers the entries are the pool's
        /// own objects and the writes land on them too, and <c>ComputeAndPersist</c>'s reload
        /// loop puts the sidecar back on the pool either way - so the two are the same values
        /// by construction rather than by coincidence.</para>
        ///
        /// <para>Returns false when the frozen model or any 1st-pass scalar sidecar is missing;
        /// the caller then THROWS with actionable guidance - an explicitly requested frozen mode
        /// must never silently degrade to the anti-conservative retrain. Every `return false` is
        /// placed BEFORE any file is scored or written, so a refusal leaves every artifact and
        /// every entry untouched.</para>
        /// </summary>
        private static bool ComputePass2TransferCompeteFull(
            PipelineContext ctx,
            RescoredEntries rescored,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            PercolatorResults frozenModel,
            string pass1ExperimentAgg,
            Pass2SidecarWriter writer,
            HashSet<uint> stratumBaseIds)
        {
            // The competition is CONSTRAINED to the stratum (peptides of >=2-peptide 1st-pass
            // proteins), and the map-back below leaves OFF-stratum survivors on their 1st-pass q
            // (report = pass1 U stratum passers, so re-scoping only adds, never drops an
            // already-passing peptide). The full-population form this method also served was
            // transfer-compete, removed because its competition ran over a target-conditioned
            // subset - see the OspreyEnvironment.Pass2QValue remarks and issue #4581.
            if (stratumBaseIds == null)
            {
                throw new ArgumentNullException(nameof(stratumBaseIds),
                    @"protein-compact is the only competition mode; its stratum is required.");
            }
            // Works for whichever classifier the 1st pass trained (linear SVM or
            // gradient-boosted trees) -- the scorer hides that choice, so this stays the
            // honest-FDR path under OSPREY_FDR_MODEL=gbdt too.
            var scorer = FrozenModelScorer.TryCreate(frozenModel);
            if (scorer == null)
            {
                // The frozen 1st-pass model has no usable model or standardizer.
                ctx.LogWarning(OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Second_pass_FDR__the_saved_first_pass_model_cannot_be_used_);
                return false;
            }
            var sw = Stopwatch.StartNew();
            int nFeatures = scorer.NumFeatures;

            // 1. The one genuinely global set: every survivor entry_id. The best-of-runs floor is
            //    a per-entry_id minimum across files, so that set has to span the run - but it is
            //    O(distinct entry_ids), not O(files x entry_ids). Folded over the files one at a
            //    time and each dropped, so this walk does not build the pool (#4486); while
            //    anything else still does, Files() yields from the resident buffer and it costs
            //    nothing. Fusing this fold with the other converted consumers' walks is a
            //    separate, run-wide question - it is free until nothing builds the buffer.
            //
            //    What used to sit here was a separate whole-run pass that loaded EVERY file's
            //    reconciled PIN features and stashed all of their frozen-model scores in one
            //    Dictionary<(file, entry_id), double> (~3.8 GB at 82 files, #4486). The scoring
            //    is per file by nature, so it now happens one file at a time inside ReadFile
            //    below: same loader and identity key (LoadReconciledFeaturesByScoreIndex keyed by
            //    (EntryId,Charge,ScanNumber)), same scores, one file's worth resident, and one
            //    fewer pass over the reconciled parquets.
            //
            //    Two --input-scores paths in different directories CAN share a stem
            //    (RescoreHydration.PreCompactionTallies is index-keyed for exactly that reason).
            //    A same-stem pair used to be MERGED into one list here, which a streamed reader
            //    cannot do - it is handed one file's rows at a time and there is no whole-run
            //    map to merge into. Such a stem now resolves last-wins, exactly as this method's
            //    sibling lookups already did (sidecarByKey, perFileParquetPaths) and as the
            //    projection second pass's survivorsByFile does. It does NOT make duplicate stems
            //    correct (#4555): either disposition applies ONE file's scalars to a name that
            //    denotes two files. The real fix is path-hashed identity across artifact naming
            //    and every per-file map at once, tracked there.
            //
            //    A hard throw was tried here and removed: it fired at Stage 7, after hours of
            //    Stages 1-6, for a condition knowable at argument-parse time, while the sibling
            //    maps stayed last-wins - so it converted one silent inconsistency into a late
            //    abort without making the class of input any safer.
            var fileNames = rescored.FileNames;
            var survivorEntryIds = new HashSet<uint>();
            // The best-of-runs floors. Declared HERE because the two halves are gathered in two
            // different walks: the peptide IDENTITIES come from the survivor walk just below -
            // the only walk that sees entries - and the FLOORS themselves from the per-file
            // second-pass records read much further down, which carry run q but no sequence.
            var floors = new ExperimentQFloors();
            // The resident survivor lists by file. It holds one REFERENCE per file, not a
            // copy, so it costs nothing beyond the whole-run buffer Stage 7 has already
            // built by the time this pass runs. Only where the pool is going to stay: on a
            // streamed source those lists are emptied the moment the fold moves on, so a map of
            // references to them would hand the competition empty runs - the failure that looks
            // like a cohort with no survivors rather than like a bug. There LoadOneFile rebuilds
            // the run it is asked for instead.
            var residentByFile =
                new Dictionary<string, List<FdrEntry>>(fileNames.Count, StringComparer.Ordinal);
            long survivorObservations = WalkSurvivors(rescored, floors,
                rescored.Streams ? null : residentByFile, survivorEntryIds);

            // 2. Per-file scalar sidecar paths. Validate every sidecar up front so we fail fast
            //    (and fall back to the retrain) before streaming any file.
            var fileKeys = new List<string>(fileNames.Count);
            // The analysis-wide pass-1 EXPERIMENT-scope records (format v5, issue #4486). This
            // replaces a per-(file, entry_id) stash of the off-stratum peaks Stage 6 changed:
            // that stash existed because the post-rescore overlay zeroes an in-memory experiment
            // q, so the pass-1 value had to be recovered from somewhere, and the only place it
            // lived was the file's own record. An off-stratum peak keeps its pass-1 experiment q
            // whether Stage 6 changed it or not, so with one analysis-wide record per entry_id
            // there is nothing left to stash or to condition on.
            var pass1Experiment = LoadExperimentRecords(config, FdrScoresSidecar.Pass.FirstPass);

            // The per-file 1st-pass sidecars are located and header-checked HERE, before any
            // survivor is mutated, because ReadScalars throws on a bad file and doing that inside
            // the streaming loop would abort a multi-hour run with the pool half-written.
            //
            // ONLY WHEN THIS PASS WILL ACTUALLY READ THEM, which is only to verify. The fold
            // applies the worker's own 2nd-pass records and seeds the experiment scalars from the
            // analysis-wide sidecar, so requiring per-file 1st-pass files would demand inputs
            // this stage never opens - and would make a SecondPassFDR node fail on files an HPC
            // orchestrator has no reason to send it (issue #4486).

            var sidecarByKey = new Dictionary<string, string>(fileNames.Count, StringComparer.Ordinal);
            foreach (string fileName in fileNames)
            {
                if (!perFileParquetPaths.TryGetValue(fileName, out string parquetPath))
                {
                    ctx.LogWarning(string.Format(
                        OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Second_pass_FDR__no__scores_parquet_file_is_known_for___0____so_its_first_pass_, fileName,
                        ParquetScoreCache.EXT_SCORES));
                    return false;
                }
                // ONLY WHEN THIS PASS WILL READ THEM - see above.
                //
                // Conditioned on the CHECK, not on the loop: this loop also builds fileKeys - the
                // list Stage 7 streams - and validates the parquet mapping. Skipping the loop
                // wholesale left fileKeys empty, so the competition streamed zero files and wrote
                // a 32-byte experiment sidecar and a wrong blib, all without an error.
                if (!OspreyEnvironment.Pass2VerifyWorker)
                {
                    fileKeys.Add(fileName);
                    continue;
                }
                string sidecarPath = Path.Combine(
                    Path.GetDirectoryName(parquetPath) ?? string.Empty,
                    fileName + @"." + FdrScoresSidecar.LABEL_FIRST_PASS + FdrScoresSidecar.EXT);
                if (!File.Exists(sidecarPath))
                {
                    ctx.LogWarning(string.Format(
                        OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Second_pass_FDR__first_pass_intermediate_file_not_found___0_, sidecarPath));
                    return false;
                }
                // Existence was never enough. ReadScalars THROWS on bad magic, a stale version, a
                // wrong pass byte or a partial record, and its only call site is inside the
                // streaming closure below - which has already written e.Score = frozenScore for
                // files 1..N by the time file N+1 is rejected. That aborts a multi-hour run on a
                // raw IOException with the survivor pool half-mutated, contradicting this method's
                // own contract that "every return false is placed BEFORE any survivor is mutated".
                // Checking the header here keeps the refusal where the contract says it is.
                if (!FdrScoresSidecar.IsCurrentFormat(sidecarPath, FdrScoresSidecar.Pass.FirstPass))
                {
                    ctx.LogWarning(string.Format(
                        OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Second_pass_FDR___0__is_not_a_first_pass_intermediate_file_this_version_of_Osprey_can_, sidecarPath, FdrScoresSidecar.FormatVersion));
                    return false;
                }
                // No input file means no .2nd-pass.fdr_scores.bin path, and the sidecar is where
                // this pass now puts its results - so such a file would take a fresh run q with
                // nowhere to record its experiment q, and the protein FDR that reads that column
                // would gate it on a pass-1 value while every other file used a pass-2 one. The
                // resident form could leave the answer on the entry and merely skip the write;
                // this one cannot, so it refuses instead of reporting a mixed column. The
                // condition is a Stage-5-to-Stage-7 name drift, which ComputeAndPersist has
                // already warned about twice by the time this runs.
                if (writer.InputFor(fileName) == null)
                {
                    ctx.LogWarning(string.Format(
                        OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Second_pass_FDR__no_input_file_matches___0____so_its_second_pass_intermediate_file_cannot_, fileName));
                    return false;
                }
                fileKeys.Add(fileName);
                sidecarByKey[fileName] = sidecarPath;
            }

            // What happens to the q-values, not how: the mode is on the [PATH] pass2-qvalue line,
            // and the frozen model, streaming and survivor-observation count are mechanism.
            ctx.LogInfo(CountText.Format(fileKeys.Count,
                OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Second_pass_FDR__recomputing_q_values_for__1__precursor_candidates_from_proteins_with_2_,
                OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Second_pass_FDR_over__0__files__recomputing_q_values_for__1__precursor_candidates_from_,
                stratumBaseIds.Count));
            ctx.LogVerbose(string.Format(
                OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Scoring_up_to__0__re_scored_peaks_with_the_first_pass_model__one_file_at_a_time_,
                survivorObservations));

            // This competition reduces per base_id by MAX, and BOTH modes that reach it then
            // overwrite the reported experiment q from that reduction. Neither is compatible with
            // a mean(best-N) 1st pass, in two different ways:
            //
            //   protein-compact assembles the reported column from TWO sources - on-stratum
            //   survivors get the max-aggregated value computed here, off-stratum survivors keep
            //   their 1st-pass mean(best-N) q (the `continue` in the map-back below). One column,
            //   two aggregations, and no way for a consumer to tell which row used which.
            //
            //   transfer-compete rewrites EVERY survivor, so its column is at least internally
            //   consistent - but it is consistently MAX, silently discarding the mean(best-N)
            //   statistic the operator asked for and reporting a reproducibility-weighted run as
            //   an ordinary one. Uniformly wrong is not better than mixed here, because the run
            //   is indistinguishable from a max run in its own output.
            //
            // Refuse both rather than emit either: a number a user would reasonably trust and
            // cannot audit is worse than an error. Making the streamed competition itself
            // aggregate-aware is the real fix and is deliberately NOT folded in - it depends on
            // the gap-fill run-count exclusion, which is its own design (issue #4511).
            //
            // Gated on the arm the FIRST PASS recorded, not on this process's environment: a
            // --task SecondPassFDR node reloads the frozen model from disk and never
            // trained pass 1, so its own OSPREY_EXPERIMENT_AGG is unrelated to the q-values it is
            // about to rewrite. Reading the live process was wrong in both directions - unset on
            // SecondPassFDR emitted a mixed column with no refusal, and a stale exported
            // variable aborted a consistent run.
            // A sidecar written before the arm was recorded reports null. Null means UNKNOWN, not
            // "max", so fall back to this process's variable and SAY SO - an inferred answer the
            // operator can see beats a silent one, and it is exactly the pre-provenance behavior
            // for exactly the artifacts that predate provenance.
            bool armRecorded = pass1ExperimentAgg != null;
            string pass1Arm = armRecorded ? pass1ExperimentAgg : OspreyEnvironment.ExperimentAgg;
            if (OspreyEnvironment.IsMeanBestArm(pass1Arm))
            {
                throw new InvalidOperationException(string.Format(
                    @"OSPREY_PASS2_QVALUE={0} cannot be combined with a 1st pass run under " +
                    @"OSPREY_EXPERIMENT_AGG={1}{2}. This mode recomputes the reported experiment q " +
                    @"from a MAX-aggregated competition, which {3}. Use OSPREY_PASS2_QVALUE={4}, " +
                    @"which carries the 1st-pass mean(best-N) q through unchanged, for a " +
                    @"mean(best-N) arm.",
                    OspreyEnvironment.PASS2_QVALUE_PROTEIN_COMPACT,
                    pass1Arm,
                    armRecorded
                        ? @" (recorded in the 1st-pass model sidecar)"
                        : @" (INFERRED from this process's environment - the 1st-pass model sidecar " +
                          @"predates arm recording and does not say which arm trained it)",
                    @"would leave on-stratum precursors max-aggregated and off-stratum " +
                    @"precursors on their 1st-pass mean(best-N) q - one column, two statistics",
                    OspreyEnvironment.PASS2_QVALUE_TRANSFER));
            }

            // 3. Streamed full-population competition + run/experiment precursor q + PEP. One
            //    file's ENTRIES, features, scalars and run q are resident at a time; the
            //    cross-file state is bounded by the number of distinct precursors and distinct
            //    survivor entry_ids, so peak memory is flat in file count (the 32/64 GB many-file
            //    target). Run q is written onto each file's entries and that file's 2nd-pass
            //    sidecar as it finishes; experiment q and PEP are patched into those sidecars
            //    afterwards from the bounded state this returns, so no (file, entry_id)-keyed
            //    result map is ever built and no file is held for a later pass over the pool.
            StreamingFdr.StreamedCompetitionState competition;
            long nScored = 0;
            // The file the competition is working on. StreamingFdr reads a file and hands back
            // its run q within one iteration, so exactly one file is live here at a time; the
            // pair is set by ReadFile and released by ApplyFileRunQ once the sidecar is written.
            string currentKey = null;
            List<FdrEntry> currentEntries = null;
            // The worker's own records for the file in flight. They carry the composite score
            // each pool entry COMPETED on, which is the score Stage 7 needs downstream - so on
            // the shipped path they replace both the 1st-pass seed and the frozen rescore that
            // used to re-derive it. Released with the file.
            List<FdrScoreRecord> currentWorkerRecords = null;
            // Files this pass folded, in the order it folded them, which is the order step 4
            // reads their records back in.
            var sidecarsFolded = new List<string>(fileKeys.Count);
            // Seeds each file's 1st-pass Score/Pep/ExperimentAggregateScore as it is
            // materialized, in place of the whole-pool pass ComputeAndPersist skips for this
            // mode. Capacity grows to the largest file seen rather than being scanned for,
            // because there is no pool to scan.
            var seeder = new Pass1ScalarSeeder(0,
                LoadExperimentRecords(config, FdrScoresSidecar.Pass.FirstPass));
            // The streamed phase is otherwise silent; at 163 files that was a 9.6 min gap
            // immediately after the line above announced it. ReadFile is invoked exactly once per
            // file, so counting calls here is an honest per-file progress signal without threading
            // a callback through the FDR layer. It now covers the frozen-model feature reload too
            // (folded in below), which is the expensive half and used to have its own reporter.
            using (var progress = new ProgressReporter(
                CountText.Format(fileKeys.Count, OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Recomputing_second_pass_q_values_for_1_file,
                    OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Recomputing_second_pass_q_values_across__0__files),
                fileKeys.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                long nRead = 0;
                // One file's 1st-pass records and survivor ids, reused across files rather than
                // reallocated per file. Both are O(one file's survivors) - about 533 K of the
                // ~2.99 M records a CHS file's sidecar holds - because the selector below keeps
                // only the survivor subset.
                var pass1Records = new List<FdrScoreRecord>();
                var survivorIds = new HashSet<uint>();

                // ONE file's post-rescore survivors, off the resident buffer. The list may be
                // stamped and then dropped, and those stamps are also visible on the pool -
                // which is why the sidecar, not the entry, is what carries this pass's
                // results forward.
                List<FdrEntry> LoadOneFile(string fileKey)
                {
                    // Rebuilt from this run's own artifacts on a streamed source, taken off the
                    // resident buffer when there is one. Either way it is ONE run, which is what
                    // this pass was already written to hold - the streamed source only makes
                    // that true of the stage around it as well.
                    if (rescored.Streams)
                        return rescored.MaterializeFile(fileKey);
                    return residentByFile.TryGetValue(fileKey, out var resident)
                        ? resident
                        : new List<FdrEntry>();
                }

                // ownSurvivorIds is this file's own survivor entry_ids, which is what
                // CompeteOneFile now filters its run q on - the set a per-file worker will have
                // once that half moves (#4486). ReadOneFilePass2Inputs refills the scratch set
                // from the entries it is handed, so it is exactly this file's survivors, and
                // the caller enforces its equivalence with the global union.
                //
                // Returned BY REFERENCE, not copied: it is the reused per-file scratch, so it
                // stays valid only until the next ReadFile call clears it. The streaming loop
                // consumes it within the same iteration, which is the contract that makes the
                // scratch safe to share; copying 1.16 M entry_ids per file to avoid the
                // aliasing would reintroduce the allocation the scratch exists to remove. The
                // aliasing disappears with the move, where the worker owns the only set.
                // Stage the file. ALWAYS runs: these entries are what ApplyFileRunQ stamps and
                // what the sidecar write serializes, so they are needed whether or not this pass
                // reads the file's 1st-pass population.
                void BeginFile(string fileKey)
                {
                    // Reported HERE, not in ReadFile. ReadFile runs only where this pass has to
                    // RECOMPUTE a file's competition, so on the path the move exists to produce -
                    // every file answered by the worker - it never ran, the counter never moved,
                    // and the phase was a single silent block: 654 s at 446 runs, immediately
                    // after the line announcing that the fold was reading the worker's answers.
                    // BeginFile is the call that always happens, once per file, which is what a
                    // per-file progress signal has to be attached to.
                    progress.Report(++nRead);
                    currentKey = fileKey;
                    currentEntries = LoadOneFile(fileKey);
                    currentWorkerRecords = null;
                }

                // Read this file's whole 1st-pass population and re-run the frozen rescore.
                // ONLY for the recomputation - see StreamingFdr's beginFile/readFile split.
                (uint[] entryIds, double[] scores, IReadOnlyDictionary<uint, double> survivorScores,
                    HashSet<uint> ownSurvivorIds)
                    ReadFile(string fileKey)
                {
                    // The parquet lookup is established by the validation loop above (every file
                    // has a parquet path or this method already returned false), and resolved
                    // HERE so a key miss cannot be reported as a parquet failure by the reader.
                    string effectiveParquetPath = ParquetScoreCache.ReconciledPathFromScoresPath(
                        perFileParquetPaths[fileKey]);
                    // currentKey/currentEntries are staged by BeginFile now, on every path.
                    // Read from the path the validation loop above checked with IsCurrentFormat,
                    // not from writer.InputFor's. Both resolve through
                    // ArtifactPaths.ResolveOutputDir, so they name the same file in every
                    // configuration this method accepts; this is the one whose header was
                    // verified BEFORE any survivor was mutated, which is where the contract
                    // above puts the refusal.
                    ReadOneFilePass2Inputs(
                        sidecarByKey[fileKey], effectiveParquetPath, currentEntries,
                        scorer, nFeatures, seeder, ctx.LogWarning,
                        survivorIds, pass1Records,
                        out uint[] eids, out double[] scs, out var fileScores);
                    nScored += fileScores.Count;
                    return (eids, scs, fileScores, survivorIds);
                }

                // Finish this file while its run q map is still in hand: stamp the run q onto its
                // entries, write its 2nd-pass sidecar, and let both go. Holding every file's run
                // q to the end of the run cost ~3.8 GB at 82 files; holding every file's ENTRIES
                // to a later write pass is the 40 GB pool #4486 is removing. An entry absent from
                // the map won no competition in this file and takes the 1.0 default the streamed
                // form used to fill in centrally.
                //
                // The sidecar's four experiment-scope columns are NOT final here - the
                // competition that produces them is not finished until every file has been read -
                // so they go in as whatever the entry carries now and step 4 patches them.
                void ApplyFileRunQ(string fileKey, StreamingFdr.FileCompetition contribution)
                {
                    IReadOnlyDictionary<uint, double> fileRunQ = contribution.RunQ;
                    // The whole per-file cycle depends on StreamingFdr finishing each file
                    // before it reads the next. Asserted rather than assumed: if that order ever
                    // changed, the entries stamped here would silently belong to another file.
                    if (currentEntries == null || !Equals(fileKey, currentKey))
                    {
                        throw new InvalidOperationException(string.Format(
                            @"Second-pass competition applied run q for '{0}' while '{1}' was the file in hand.",
                            fileKey, currentKey ?? @"(none)"));
                    }
                    foreach (var e in currentEntries)
                    {
                        double rq = fileRunQ.TryGetValue(e.EntryId, out double v) ? v : 1.0;
                        e.RunPrecursorQvalue = rq;
                        // Precursor-level path: keep peptide q in step with precursor q for the
                        // reported set (peptide-level FDR is not the target here).
                        e.RunPeptideQvalue = rq;
                    }
                    // The worker wrote this file's sidecar (#4486), and it is NOT rewritten
                    // here: pipeline artifacts are immutable once written, and this stage owns
                    // none of the per-run ones (#4665).
                    sidecarsFolded.Add(fileKey);
                    // DROP the run here, on a streamed source: its answer is on disk and step 4
                    // patches the sidecar rather than the entries, so this is the last line that
                    // reads them. Without it the pass would refill run after run and never let
                    // one go - the whole-run pool rebuilt one run at a time, which is the shape
                    // that looks like a fix in the code and like no fix at all in the profile.
                    if (rescored.Streams)
                        rescored.DropFile(fileKey);
                    currentKey = null;
                    currentEntries = null;
                }

                // Say whether this run verified, ALWAYS - the verifier re-reads every 1st-pass
                // sidecar, and a silent flag is indistinguishable from a flag that stopped
                // reaching the child process. This line is what regression.ps1 and
                // SubsetPipelineTest assert on to prove the straight leg verified and the HPC
                // chain did not. It used to count the files the worker had answered as well; every
                // file is answered now (RequireWorkerAnswers), so that count is gone (#4665).
                if (OspreyEnvironment.Pass2VerifyWorker)
                {
                    ctx.LogInfo(string.Format(
                        @"Second-pass worker verification ACTIVE (OSPREY_PASS2_VERIFY_WORKER): " +
                        @"recomputing the per-file competition for {0} file(s) to assert the " +
                        @"worker's answer. This re-reads each 1st-pass sidecar; it is a test " +
                        @"instrument and is off by default.",
                        fileKeys.Count));
                }
                ctx.LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_SECOND_PASS_FOLD, @"verify={0} runs={1}",
                    OspreyEnvironment.Pass2VerifyWorker ? @"on" : @"off", fileKeys.Count));

                try
                {
                    competition = StreamingFdr.ComputeFullPopulationPrecursorFdrStreaming(
                        fileKeys, ReadFile, survivorEntryIds, ApplyFileRunQ, stratumBaseIds,
                        // The rescore worker's answer is what gets folded; the streaming pass
                        // recomputes only to assert against it (#4486).
                        fileKey =>
                        {
                            var answer = ReadWorkerContribution(
                                writer, fileKey, stratumBaseIds, out currentWorkerRecords);
                            // APPLY what the worker wrote, rather than recomputing it. Each pool
                            // entry's Score is the composite score that file's competition ranked
                            // it on; protein FDR and the blib read it downstream. Re-deriving it
                            // here meant reloading PIN features from the reconciled parquet and
                            // re-running the frozen model per survivor - work the worker already
                            // did and wrote down.
                            ApplyWorkerScores(currentEntries, currentWorkerRecords, seeder);
                            return answer;
                        },
                        // Off by default: the recompute is a TEST INSTRUMENT and costs exactly
                        // the re-reads this phase exists to remove. regression.ps1 turns it on.
                        OspreyEnvironment.Pass2VerifyWorker,
                        BeginFile);
                }
                catch (InvalidOperationException)
                {
                    // A per-file competition disagreement leaves the worker's sidecar on disk and
                    // this pass's answer only in memory - i.e. exactly one side of the diff you
                    // need. Persist the other side BESIDE it before the throw propagates, so the
                    // investigation starts from two files rather than from a reproduction run.
                    // Deliberately a NEW path: the worker's file is immutable and is the evidence.
                    DumpRecomputedForDiff(ctx, writer, currentKey, currentEntries);
                    throw;
                }
            }
            // Once, after the stream, rather than per file: the counts are run-wide and a
            // missing 1st-pass sidecar is a run-wide conclusion.
            seeder.LogSummary(ctx);

            // 4. Finish each reported survivor from the bounded competition state, one file at a
            //    time, over the per-run SIDECARS rather than the entries - each file's records
            //    carry the entry_id and run q this needs, and the file's entries have been dropped
            //    by now. That is the point: a pass over the pool here would put every file back in
            //    memory at once and undo the whole per-file cycle above (#4486).
            int nMapped = FoldAndPublishExperimentScope(ctx, writer, sidecarsFolded, floors, FinishRecord);
            ctx.LogVerbose(string.Format(
                OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Applied_the_recomputed_q_values_to__0__precursor_candidate_peaks___1__scored_with_the_first_pass_model_,
                nMapped, nScored, sw.Elapsed.TotalSeconds));
            return true;

            // The experiment-scope record for one observation plus its PEP, from the bounded
            // competition state. Split out of the loop above only so the two dispositions -
            // on-stratum recompute and off-stratum carry-through - read side by side.
            FdrExperimentRecord FinishRecord(FdrScoreRecord rec)
            {
                if (!stratumBaseIds.Contains(rec.EntryId & 0x7FFFFFFFu))
                {
                    // Off-stratum survivors keep their 1st-pass EXPERIMENT q (report = pass1 U
                    // stratum passers). That q is a pass-1 property anchored on the
                    // best-scoring peak, and reconciliation corrects peaks TOWARD that anchor
                    // rather than moving it, so a changed peak was not the one that set the
                    // maximum and cannot become it. Carrying the pass-1 value is therefore
                    // exact, and it is what keeps the re-scoping additive.
                    //
                    // Their RUN q was refreshed with everyone else's: a peak Stage 6 changed
                    // competed above on its recalculated score, and one that did not compete
                    // takes the 1.0 that says so, rather than a stale q describing a peak that
                    // no longer exists (the post-rescore overlay zeroes it for that reason).
                    pass1Experiment.TryGetValue(rec.EntryId, out var q1);
                    // PEP carried with the rest of the pass-1 experiment scope: an off-stratum
                    // entry did not enter this pass's competition, so it has no 2nd-pass winner
                    // and its pass-1 winner fact is the one that still describes it.
                    return new FdrExperimentRecord(rec.EntryId,
                        q1.ExperimentPrecursorQvalue, q1.ExperimentPeptideQvalue,
                        1.0, q1.ExperimentAggregateScore, q1.Pep);
                }
                double eq = competition.ExperimentQ(rec.EntryId, rec.RunPrecursorQvalue);
                // The aggregate MUST move with the q. This mode recomputes experiment q from a
                // fresh full-population competition, so the pass-1 aggregate the seed carried is
                // no longer the score that q was ranked on - and this is the DEFAULT mode, so
                // leaving it stale is not an edge case. Measured cost of the omission: the
                // co-assignment panel's experiment boundary is a minimum over accepted
                // precursors' aggregates, so entries still holding the ResetScores 0.0 default
                // dragged it to 0.0 and admitted the entire decoy pool - 542,368 decoys against
                // 117,783 targets on astral, 183x the pass-1 count, from a rule meant to admit
                // about 1%.
                // null means the entry never entered the experiment fold (off-stratum under
                // protein-compact); those keep the pass-1 value, which is correct because they
                // keep the pass-1 experiment q too - the branch above.
                double? agg = competition.ExperimentAggregateScore(rec.EntryId);
                // The protein q goes in at 1.0: the second-pass protein FDR has not run yet, and
                // it is the one column of this record that the step after it owns.
                // Precursor-level path: peptide q stays in step with precursor q for the
                // reported set (peptide-level FDR is not the target here).
                pass1Experiment.TryGetValue(rec.EntryId, out var prior);
                // The PEP WINNER FACT, stored once per entry rather than joined onto every
                // observation. This is what retired PatchPep: the value used to be knowable only
                // after the fold and only for one run, so it was written back into each per-run
                // sidecar afterwards - which is what made those files mutable (issue #4486).
                return new FdrExperimentRecord(rec.EntryId, eq, eq, 1.0,
                    agg ?? prior.ExperimentAggregateScore, competition.PepWinner(rec.EntryId));
            }
        }

        /// <summary>
        /// OSPREY_PASS2_QVALUE=transfer's JOIN: fold the per-run answers the rescore worker
        /// wrote into the analysis-wide experiment scope (#4665).
        ///
        /// <para>The per-file half - re-mapping each run's run q through its own 1st-pass
        /// score-to-q table - ran in <see cref="Pass2PerFileWorker"/>. What is left is genuinely
        /// experiment-wide and is the same fold the competition ends with
        /// (<see cref="FoldAndPublishExperimentScope"/>): each record takes its precursor's
        /// pass-1 experiment values, carried rather than recomputed because the transfer never
        /// re-competes, and the best-of-runs floors come out of the run q-values the records
        /// hold. Both used to run over the resident pool after the per-file half, which is why
        /// this mode alone held every run's survivors for the whole of Stage 7 - ~4.4 GB plus
        /// ~0.197 GB per run, 92.3 GB predicted against 91.1 GB measured at 446 runs.</para>
        ///
        /// <para>The analysis-wide 1st-pass experiment sidecar is required, not defaulted: an
        /// absent one would carry every precursor forward at q = 1.0 and drop it from the
        /// output.</para>
        /// </summary>
        private static void ComputePass2TransferFold(PipelineContext ctx, RescoredEntries rescored,
            OspreyConfig config, Pass2SidecarWriter writer)
        {
            string pass1ExperimentPath = FdrExperimentSidecar.PathFor(config.OutputBlib,
                ScoringTaskShared.ArtifactSiblingPath(config), FdrScoresSidecar.Pass.FirstPass);
            if (string.IsNullOrEmpty(pass1ExperimentPath) || !File.Exists(pass1ExperimentPath))
            {
                throw new InvalidOperationException(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_ComputePass2FrozenCompetition_Second_pass_FDR_cannot_run__the_saved_first_pass_model__a_first_pass_intermediate_file__,
                    OspreyArgNames.Text(OspreyArgNames.TASK)));
            }
            var pass1Experiment = LoadExperimentRecords(config, FdrScoresSidecar.Pass.FirstPass);
            var floors = new ExperimentQFloors();
            WalkSurvivors(rescored, floors, null, null);
            FoldAndPublishExperimentScope(ctx, writer, rescored.FileNames, floors,
                rec => CarryPass1Experiment(rec.EntryId, pass1Experiment));
        }

        /// <summary>
        /// One entry_id's experiment-scope record under the transfer: its pass-1 experiment
        /// values, carried verbatim, with the protein q left at 1.0 for the second-pass protein
        /// FDR to fill in. A precursor with no pass-1 record never competed: q = 1.0, aggregate
        /// 0.0, PEP 1.0 - the values <see cref="AssignPerRunQ"/> gives its entries.
        /// </summary>
        private static FdrExperimentRecord CarryPass1Experiment(uint entryId,
            IReadOnlyDictionary<uint, FdrExperimentRecord> pass1Experiment)
        {
            if (!pass1Experiment.TryGetValue(entryId, out var q1))
                return new FdrExperimentRecord(entryId, 1.0, 1.0, 1.0, 0.0, 1.0);
            return new FdrExperimentRecord(entryId, q1.ExperimentPrecursorQvalue,
                q1.ExperimentPeptideQvalue, 1.0, q1.ExperimentAggregateScore, q1.Pep);
        }

        /// <summary>
        /// Walk every run's survivors once, recording each entry_id's peptide identity for the
        /// best-of-runs PEPTIDE floor - and, for the competition, the survivor entry_id set and
        /// the resident lists by file. Returns the number of survivor observations walked.
        ///
        /// <para>The identities are recorded HERE because this is the one walk that sees
        /// entries - the floors themselves come from the per-file 2nd-pass records, which carry
        /// run q but no sequence - and because the entries are the only route-independent source
        /// of them. LibraryById is NOT: a --task SecondPassFDR node loads a library with no
        /// generated decoys, so resolving a decoy entry_id there answers on the straight route
        /// and returns nothing on the distributed one (measured on Stellar: 166,680 of 333,404
        /// records differing, every one a decoy).</para>
        ///
        /// <para>Folded over the files one at a time and each dropped, so this walk does not
        /// build the pool (#4486). Reported because it walks EVERY survivor observation -
        /// 89,068,375 of them on the 82-file SEA-AD run - before anything downstream logs a
        /// word; it sat inside a 195 s silence that read as a hung run at the very end of a
        /// multi-hour search.</para>
        /// </summary>
        /// <param name="rescored">The survivor source.</param>
        /// <param name="floors">Receives the peptide identities.</param>
        /// <param name="residentByFile">Receives a reference to each run's list, or null. Only
        /// for a pool that stays resident - see the caller.</param>
        /// <param name="survivorEntryIds">Receives every survivor entry_id, or null.</param>
        private static long WalkSurvivors(RescoredEntries rescored, ExperimentQFloors floors,
            Dictionary<string, List<FdrEntry>> residentByFile, HashSet<uint> survivorEntryIds)
        {
            long survivorObservations = 0;
            int fileCount = rescored.FileCount;
            using (var mergeProgress = new ProgressReporter(
                CountText.Format(fileCount, OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Collecting_second_pass_precursor_candidates_from_1_file,
                    OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Collecting_second_pass_precursor_candidates_from__0__files),
                fileCount, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                int mergeIdx = 0;
                foreach (var kvp in rescored.StreamFiles())
                {
                    mergeProgress.Report(++mergeIdx);
                    if (residentByFile != null)
                        residentByFile[kvp.Key] = kvp.Value;
                    survivorObservations += kvp.Value.Count;
                    if (survivorEntryIds != null)
                    {
                        foreach (var e in kvp.Value)
                            survivorEntryIds.Add(e.EntryId);
                    }
                    floors.ObserveIdentities(kvp.Value);
                }
            }
            return survivorObservations;
        }

        /// <summary>
        /// Fold every run's per-run 2nd-pass records into the analysis-wide experiment scope,
        /// raise it to the best-of-runs floors, and publish it for the protein-FDR step - the
        /// last step of both second passes. Returns the number of records folded.
        ///
        /// <para>Over the SIDECARS, not the entries: each file's records carry the entry_id and
        /// run q this needs. READ-ONLY over them. This loop used to finish by rewriting each
        /// one's pep column (PatchPep), which is what made a per-file sidecar mutable; PEP is now
        /// stored once, as a winner fact on the experiment record.</para>
        ///
        /// <para><paramref name="finishRecord"/> is the mode: what experiment-scope values a
        /// record takes. It is the only thing the two second passes do differently here.</para>
        ///
        /// <para>The peptide floors are derived from the entry floors just folded - no per-run
        /// data and no second pass over anything - and both halves are stamped onto the records
        /// before anyone sees them, which is what makes this file's q-values FINAL rather than a
        /// value the pipeline overrides afterwards (issue #4522). It is also the point the two
        /// strata of the competition stop differing: an on-stratum entry takes a fresh
        /// competition q that was never clamped, and an off-stratum entry carries its pass-1 q -
        /// which WAS clamped, against pass-1 run q, while pass 2 refreshed run q underneath it.
        /// One rule closes both.</para>
        /// </summary>
        private static int FoldAndPublishExperimentScope(
            PipelineContext ctx, Pass2SidecarWriter writer, IReadOnlyList<string> fileKeys,
            ExperimentQFloors floors, Func<FdrScoreRecord, FdrExperimentRecord> finishRecord)
        {
            int nMapped = 0;
            var unpatched = new List<string>();
            var experiment = new FdrExperimentAccumulator();
            using (var patchProgress = new ProgressReporter(
                CountText.Format(fileKeys.Count, OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Writing_experiment_level_q_values_for_1_file,
                    OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Writing_experiment_level_q_values_for__0__files),
                fileKeys.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                int patchIdx = 0;
                foreach (string fileKey in fileKeys)
                {
                    patchProgress.Report(++patchIdx);
                    string inputFile = writer.InputFor(fileKey);
                    if (inputFile == null)
                        continue;
                    string pass2Path = FdrScoresSidecar.Pass2Path(inputFile);
                    // Staged, then applied: ReadRecords can return false AFTER invoking the
                    // callback, so accumulating experiment values as they arrive would leave the
                    // analysis-wide record half-built from a file that then failed.
                    var staged = new List<FdrExperimentRecord>();
                    if (!FdrScoresSidecar.ReadRecords(pass2Path, FdrScoresSidecar.Pass.SecondPass,
                            rec =>
                            {
                                floors.Observe(rec.EntryId,
                                    rec.RunPrecursorQvalue, rec.RunPeptideQvalue);
                                staged.Add(finishRecord(rec));
                            }))
                    {
                        unpatched.Add(fileKey);
                        continue;
                    }
                    foreach (var exp in staged)
                    {
                        experiment.Add(exp.EntryId, exp.ExperimentPrecursorQvalue,
                            exp.ExperimentPeptideQvalue, exp.ExperimentProteinQvalue,
                            exp.ExperimentAggregateScore, exp.Pep);
                    }
                    nMapped += staged.Count;
                }
            }
            floors.DerivePeptideFloors();
            int raised = experiment.ApplyRunQFloors(entryId => floors.FloorsFor(entryId));
            ctx.LogInfo(string.Format(
                OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Raised__0__of__1__experiment_level_precursor_candidate_q_values_to_their_best_run_level_,
                raised, experiment.Count));

            // Handed to the protein-FDR step, which fills the one column it owns and writes the
            // 2nd-pass experiment sidecar. Published rather than returned because the protein
            // FDR runs in the owning task after this method returns.
            ctx.Publish(new Pass2ExperimentScope(experiment));
            if (unpatched.Count > 0)
            {
                // Hard, not a warning. A run whose records could not be read contributed nothing
                // to the experiment q, PEP and aggregate - a q-value a consumer would reasonably
                // trust and could not audit. The protein FDR that runs next gates on
                // ExperimentPrecursorQvalue, so continuing means reporting a protein set computed
                // from unfinished numbers.
                throw new IOException(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Second_pass_experiment_level_q_values_could_not_be_written_for__0__files____1____Stopping_,
                    unpatched.Count, string.Join(@", ", unpatched)));
            }
            return nMapped;
        }

        /// <summary>
        /// Load the reconciled parquet's 21-PIN feature rows keyed by each row's
        /// stable identity (entry_id, charge, scan_number). The Stage 6 reconciled
        /// parquet is re-sorted and re-indexed by <c>ParquetScoreCache.WriteScoresParquet</c>
        /// -- the appended gap-fill rows interleave into the (entry_id, charge,
        /// scan_number) sort order -- so a post-compaction stub's
        /// <see cref="FdrEntry.ParquetIndex"/> (assigned against the ORIGINAL Stage
        /// 4 parquet, or carried on the in-memory buffer through rescore) no longer
        /// addresses that stub's own row in the reconciled parquet. Identity is
        /// invariant across the reindex, so <see cref="MapFeaturesByScoreIndex"/>
        /// keys on it. Reads the lean stub columns + the PIN feature columns (no
        /// heavy fragment/XIC/CWT blobs), one file at a time, so the reload stays
        /// within the issue #4355 memory bound. (issue #4355)
        /// </summary>
        /// <summary>
        /// Everything one file's pass-2 competition needs, taken from that file's OWN artifacts:
        /// the whole-population <c>(entry_id, score)</c> arrays it competes over, the survivor
        /// records it seeds from, and the frozen-model score for each survivor whose reconciled
        /// features resolve.
        ///
        /// <para>Explicitly parameterized rather than reading enclosing state, because this is
        /// per-FILE work that belongs to the rescore worker: the run-level competition is
        /// computable from one file alone, and every input here is either that file's own
        /// sidecar / parquet or a whole-run constant that already rides the per-file
        /// <c>.1st-pass.model.json</c> relay (the frozen model and the protein stratum). Stage 7
        /// calls it today; moving the call to <c>PerFileRescoreTask</c> is then a call-site
        /// change rather than a rewrite, which is what stops Stage 7 having to open a 1st-pass
        /// sidecar at all (issue #4486).</para>
        ///
        /// <para>ONE traversal of the 1st-pass sidecar yields all three outputs. They were three
        /// separate passes over the same ~204 MB file, i.e. two of every three reads of the
        /// largest artifact class in the run - 52.3 GB of 1st-pass sidecars at 257 files.</para>
        ///
        /// <para>Only the feature LOAD is guarded. Widening the try over the scoring loop would
        /// let a mid-loop throw leave a PARTIALLY swapped-in map: the competition would then run
        /// on a mixed population, and under protein-compact the unscored remainder would also be
        /// missing from the changed set, so those peaks would never be admitted and would be
        /// stamped run q 1.0 - all under a warning blaming a load that succeeded. Failure here is
        /// all-or-nothing per file: the file contributes no swapped-in scores and competes on its
        /// stored 1st-pass ones.</para>
        ///
        /// <para><c>effectiveParquetPath</c> is the file's reconciled parquet, or its Stage 4
        /// parquet when no reconciled sibling exists. <c>survivors</c> is seeded and scored IN
        /// PLACE. <c>survivorIds</c> and <c>pass1Records</c> are caller-owned scratch, cleared and
        /// refilled here so a per-file loop does not reallocate them; the caller reads
        /// <c>pass1Records</c> again for the off-stratum experiment-q carry-forward.</para>
        /// </summary>
        /// <summary>
        /// Assert that a per-run 2nd-pass sidecar about to be written describes the POOL its
        /// file's Stage 6 parquet defines - one record per row - and throw naming both counts
        /// if it does not.
        ///
        /// <para><b>The invariant this protects.</b> The per-run 2nd-pass sidecar is the only
        /// thing a separate experiment-wide node receives about a run. Its population is not the
        /// writer's to choose: the join node fixes it, distributing each file's
        /// <c>.reconciliation.json</c> from the traversal of the whole population, and
        /// <c>ReconciledParquetWriter</c> stamps the parquet Stage 6 derives from it with the
        /// join-wide reconciliation hash. Every node emitting the same rows is what makes an
        /// HPC split equal a single-machine run. Nothing checked it.</para>
        ///
        /// <para><b>Why it is worth a per-file parquet footer read.</b> The omission it was
        /// written for (594 gap-fill observations across 3 Stellar files) passed every other
        /// gate the project has - golden blib, 2nd-pass protein q, resume, HPC-chain route
        /// independence, warm re-run and library-fragment release - and surfaced only as two
        /// moved numbers in a diagnostics panel. A silently short artifact is exactly the shape
        /// that reads as correct downstream: the entries it omits keep <c>ResetScores</c>'
        /// defaults, so they report as never having competed rather than as missing. The probe
        /// decodes no column data, so the cost is a footer per file.</para>
        ///
        /// <para>Skipped when the effective parquet is not a reconciled SURVIVORS parquet: with
        /// no reconciled sibling the caller falls back to the Stage 4 file, whose rows are the
        /// whole pre-compaction population and are not this artifact's population.</para>
        /// </summary>
        internal static void AssertSidecarDescribesPool(
            string fileName, string effectiveParquetPath, IReadOnlyList<FdrScoreRecord> records)
        {
            var pool = ParquetScoreCache.ProbePoolPopulation(effectiveParquetPath);
            if (!pool.IsReconciledSurvivors)
                return;
            if (pool.RowCount != records.Count)
            {
                throw new InvalidOperationException(string.Format(
                    @"Second-pass per-file competition wrote {0} record(s) for '{1}', but its Stage 6 " +
                    @"pool holds {2} (from {3}). The per-run 2nd-pass sidecar must describe that pool " +
                    @"one record per row - it is all a separate experiment-wide node receives about " +
                    @"this run, and its population is fixed by the join, not chosen by the writer. " +
                    @"See issue #4486.",
                    records.Count, fileName, pool.RowCount, effectiveParquetPath));
            }
            AssertRecordsMatchPoolSequence(fileName, effectiveParquetPath, records,
                ParquetScoreCache.StreamEntryIds(effectiveParquetPath));
        }

        /// <summary>
        /// The per-run 2nd-pass sidecar's record SEQUENCE must equal the reconciled parquet's row
        /// sequence, position for position - not merely its population or its length.
        ///
        /// <para><b>Why a count is not enough.</b> A count cannot see a PERMUTATION, and a
        /// permutation is exactly what this file acquired: the rescore task APPENDS gap-fill
        /// entries to its in-memory pool list, while
        /// <see cref="ParquetScoreCache.StreamReconciledScoresParquet"/> merges each one into its
        /// canonical <c>(entry_id, charge, scan_number)</c> position, so writing in list order
        /// emitted the same rows with the gap-fills in a trailing block. Every count-based check
        /// passed. The reconciled parquet is the authority here rather than the writer's
        /// convenience: its population and order are fixed by the JOIN, stamped with a join-wide
        /// reconciliation hash, and the parquet writer itself hard-fails a row out of canonical
        /// order - so a sidecar that disagrees is the thing that is wrong.</para>
        ///
        /// <para><b>Why it matters beyond byte-identity with a baseline.</b> The fold
        /// (<see cref="FileCompetitionFromRecords"/>) resolves a per-base_id maximum with strict
        /// greater-than and takes the FIRST record at the maximum, matching
        /// <c>StreamingFdr.CompeteOneFile</c>'s reduction over the population. That agreement
        /// holds only while both walk the same order. A baseline comparison catches this today
        /// and will not exist for the next dataset.</para>
        ///
        /// <para>Pure, and takes the pool sequence as an <see cref="IEnumerable{T}"/>, so the
        /// comparison is unit-testable without writing a parquet and stays O(1) in memory over a
        /// streamed column.</para>
        /// </summary>
        internal static void AssertRecordsMatchPoolSequence(
            string fileName, string effectiveParquetPath,
            IReadOnlyList<FdrScoreRecord> records, IEnumerable<uint> poolEntryIds)
        {
            int i = 0;
            foreach (uint poolEntryId in poolEntryIds)
            {
                if (i >= records.Count)
                {
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass per-file competition for '{0}': its Stage 6 pool has more " +
                        @"rows than the {1} record(s) written, starting at row {2} (from {3}). " +
                        @"See issue #4486.",
                        fileName, records.Count, i, effectiveParquetPath));
                }
                if (records[i].EntryId != poolEntryId)
                {
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass per-file competition for '{0}': record {1} is entry_id {2}, " +
                        @"but its Stage 6 pool holds entry_id {3} at that row (from {4}). The " +
                        @"per-run 2nd-pass sidecar must describe the pool in the pool's own order " +
                        @"- the experiment fold takes the FIRST observation at a per-base_id " +
                        @"maximum, so a reordering silently changes which observation represents " +
                        @"a precursor. See issue #4486.",
                        fileName, i, records[i].EntryId, poolEntryId, effectiveParquetPath));
                }
                i++;
            }
            if (i == records.Count)
                return;
            throw new InvalidOperationException(string.Format(
                @"Second-pass per-file competition for '{0}': wrote {1} record(s) but its Stage 6 " +
                @"pool yielded only {2} row(s) (from {3}). See issue #4486.",
                fileName, records.Count, i, effectiveParquetPath));
        }

        internal static void ReadOneFilePass2Inputs(
            string pass1SidecarPath, string effectiveParquetPath, List<FdrEntry> survivors,
            FrozenModelScorer scorer, int nFeatures, Pass1ScalarSeeder seeder,
            Action<string> logWarning,
            HashSet<uint> survivorIds, List<FdrScoreRecord> pass1Records,
            out uint[] entryIds, out double[] scores, out Dictionary<uint, double> survivorScores)
        {
            survivorIds.Clear();
            foreach (var e in survivors)
                survivorIds.Add(e.EntryId);
            FdrScoresSidecar.ReadScalars(pass1SidecarPath, FdrScoresSidecar.Pass.FirstPass,
                out entryIds, out scores, survivorIds.Contains, pass1Records);
            // The whole-pool seed is skipped for this mode, so each file is seeded as it
            // arrives - before the scoring below, which overwrites Score for every survivor
            // whose features resolve and leaves the seeded 1st-pass value on the rest.
            seeder.Apply(survivors, pass1Records);

            Dictionary<uint, double[]> featByScoreIndex;
            try
            {
                featByScoreIndex = LoadReconciledFeaturesByScoreIndex(effectiveParquetPath);
            }
            catch (Exception ex)
            {
                logWarning(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_ReadOneFilePass2Inputs_Failed_to_reload_peak_features_from__0____1_,
                    effectiveParquetPath, ex.Message));
                featByScoreIndex = null;
            }

            survivorScores = new Dictionary<uint, double>();
            if (featByScoreIndex == null)
                return;
            foreach (var e in survivors)
            {
                if (e.ParquetIndex.HasValue &&
                    featByScoreIndex.TryGetValue(e.ParquetIndex.Value, out double[] feats) &&
                    feats != null && feats.Length == nFeatures)
                {
                    double frozenScore = scorer.Score(feats);
                    survivorScores[e.EntryId] = frozenScore;
                    // This is the score the entry COMPETES on, so it is the one the 2nd-pass
                    // sidecar must carry. The seed above supplied the 1st-pass value, which is
                    // what a survivor whose features did not resolve keeps - and competes on.
                    e.Score = frozenScore;
                }
            }
            // featByScoreIndex released here (one file resident at a time).
        }

        /// <summary>
        /// Rebuild one file's <see cref="StreamingFdr.FileCompetition"/> from the records its
        /// per-run <c>.2nd-pass.fdr_scores.bin</c> already holds, instead of recomputing it from
        /// that file's 1st-pass sidecar and reconciled parquet.
        ///
        /// <para>This is the JOIN side of the relocation (#4486). Once the per-file half runs in
        /// <c>PerFileRescoreTask</c>, the worker has already competed the file and written its
        /// answer down; Stage 7's remaining job is to FOLD, and folding needs nothing the
        /// per-run sidecar does not carry. That is what stops Stage 7 opening a 1st-pass sidecar
        /// at all - 52.3 GB of them at 257 files.</para>
        ///
        /// <para>It lives HERE and not beside <see cref="StreamingFdr.CompeteOneFile"/> because
        /// <c>Osprey.FDR</c> does not reference <c>Osprey.IO</c>, so
        /// <see cref="FdrScoreRecord"/> is not visible there. Adding that reference to put the
        /// two halves in one file would invert the DLL layering for a cosmetic adjacency;
        /// <c>Osprey.Tasks</c> already references both, and <c>FileCompetition</c> is public
        /// precisely so a join stage can construct one.</para>
        ///
        /// <para><b>Why each output is recoverable from the records alone:</b></para>
        /// <list type="bullet">
        /// <item><b>BestTarget.</b> The winning target observation of every stratum base_id is
        /// one of this file's survivors, so it HAS a record here. That is not assumed - it is the
        /// experiment-fold scope invariant enforced in
        /// <c>ComputeFullPopulationPrecursorFdrStreaming</c>, measured at 82 files. The
        /// survivor-restricted scan is a subsequence of the population scan and both take the
        /// FIRST observation at the maximum, so recovering the same winner needs only that the
        /// winner be present. The SCORE matches too, which is the easy half to get wrong:
        /// <see cref="ReadOneFilePass2Inputs"/> writes <c>e.Score = frozenScore</c> onto each
        /// survivor whose reconciled features resolve, and leaves the seeded 1st-pass value on
        /// the rest - which is exactly the score <c>CompeteOneFile</c> competes on in both
        /// cases, since a survivor absent from its override map keeps its stored score.</item>
        /// <item><b>BestDecoy.</b> NOT recoverable from the records, and not attempted here - it
        /// is supplied by <paramref name="bestDecoy"/>, read from the artifact the worker
        /// serialized its own competition to (<see cref="Pass2CompetitionDecoys"/>). The winning
        /// decoy of a base_id is routinely a non-survivor, which by definition holds no row in
        /// the pool image. An earlier form smuggled those observations INTO the sidecar to make
        /// this half work; that made one file answer two questions and cost 594 gap-fill
        /// observations their records.</item>
        /// <item><b>RunQ.</b> <c>CompeteOneFile</c> emits an entry only where it WON a
        /// competition, and the stamp defaults every other survivor to 1.0 - so a record reading
        /// 1.0 cannot be told from one that won nothing. It does not have to be: the only
        /// consumer is the per-entry_id MINIMUM across files, and 1.0 is the largest value a q
        /// can take, so a non-winner contributes nothing to a minimum it could only raise.</item>
        /// </list>
        /// </summary>
        /// <param name="records">One file's 2nd-pass records, as written by the worker.</param>
        /// <param name="stratumBaseIds">The protein stratum, or null for the full-population
        /// competition. Mirrors <see cref="StreamingFdr.CompeteOneFile"/>: the per-base_id bests
        /// are STRATUM ONLY when stratified, deliberately not the wider run-level admitted
        /// set.</param>
        /// <param name="gapFillEntryIds">
        /// This file's gap-fill entry ids (see <see cref="LoadGapFillEntryIds"/>). Excluded from
        /// the per-base_id bests because a gap-filled peak is a POOL member that never entered
        /// the per-file competition - <see cref="StreamingFdr.CompeteOneFile"/> draws its
        /// population from the file's 1st-pass sidecar, where a gap-fill has no record. Folding
        /// one in here would let a row the competition never ranked become a base_id's best, and
        /// the fold would then disagree with the worker that produced it. Their run q is still
        /// recorded: that map is per-observation and its only consumer is a cross-file MINIMUM,
        /// which a gap-fill's q participates in exactly as the resident pool's did.
        /// </param>
        /// <param name="bestDecoy">The decoy side of this file's competition, read back from
        /// <see cref="Pass2CompetitionDecoys"/> - the map the worker's <c>CompeteOneFile</c>
        /// returned, not a reconstruction. Used as given: it is already reduced to exactly the
        /// population that competed under whatever mode was active, and re-filtering it here
        /// (by stratum, by gap-fill, by anything) would re-introduce the coupling the artifact
        /// exists to remove.</param>
        internal static StreamingFdr.FileCompetition FileCompetitionFromRecords(
            IReadOnlyList<FdrScoreRecord> records, HashSet<uint> stratumBaseIds,
            Dictionary<uint, (double score, uint entryId)> bestDecoy,
            HashSet<uint> gapFillEntryIds = null)
        {
            var runQ = new Dictionary<uint, double>(records.Count);
            var bestTarget = new Dictionary<uint, (double score, uint entryId)>();
            foreach (var rec in records)
            {
                if (gapFillEntryIds != null && gapFillEntryIds.Contains(rec.EntryId))
                {
                    // Run q only - see the parameter remarks. Recorded BEFORE the stratum test
                    // below for the same reason that one does: the map is not stratum-scoped.
                    runQ[rec.EntryId] = rec.RunPrecursorQvalue;
                    continue;
                }
                uint eid = rec.EntryId;
                runQ[eid] = rec.RunPrecursorQvalue;
                uint bid = eid & BASE_ID_MASK;
                if (stratumBaseIds != null && !stratumBaseIds.Contains(bid))
                    continue;
                // DECOY records contribute their run q and nothing else. Their per-base_id best
                // comes from the worker's own artifact, and a pool decoy that outscored the
                // competition's winner would be an observation the competition never ranked -
                // the same error the gap-fill exclusion above prevents on the target side.
                if ((eid & ~BASE_ID_MASK) != 0u)
                    continue;
                // Strictly-greater, so the FIRST record at the maximum wins - the same rule
                // CompeteOneFile and the cross-file fold use. Records are written in the
                // reconciled parquet's canonical order, which is the order CompeteOneFile's own
                // population scan runs in, so "first" means the same observation on both sides.
                double s = rec.Score;
                if (!bestTarget.TryGetValue(bid, out var curT) || s > curT.score)
                    bestTarget[bid] = (s, eid);
            }
            return new StreamingFdr.FileCompetition(runQ, bestTarget, bestDecoy);
        }

        internal static Dictionary<uint, double[]> LoadReconciledFeaturesByScoreIndex(
            string reconciledPath)
        {
            var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(reconciledPath);
            var featRows = ParquetScoreCache.LoadPinFeaturesFromParquet(reconciledPath);
            int n = Math.Min(stubs.Count, featRows.Count);
            var map = new Dictionary<uint, double[]>(n);
            for (int i = 0; i < n; i++)
            {
                if (stubs[i].ParquetIndex.HasValue)
                    map[stubs[i].ParquetIndex.Value] = featRows[i];
            }
            return map;
        }

        /// <summary>
        /// Overlay re-scored PIN features onto <paramref name="entries"/> by each
        /// entry's stable identity (entry_id, charge, scan_number), skipping any
        /// entry whose identity is absent from <paramref name="featByScoreIndex"/> (a
        /// stub/parquet mismatch). Returns the number of entries whose
        /// <see cref="FdrEntry.Features"/> were assigned; the caller compares it
        /// against the entry count to detect and report a mismatch. Identity (not
        /// <see cref="FdrEntry.ParquetIndex"/>) is used because the reconciled
        /// parquet is re-indexed relative to the compacted stubs -- see
        /// <see cref="LoadReconciledFeaturesByScoreIndex"/>. Pure: no I/O, no logging.
        /// </summary>
        internal static int MapFeaturesByScoreIndex(
            IReadOnlyList<FdrEntry> entries,
            IReadOnlyDictionary<uint, double[]> featByScoreIndex)
        {
            int nMapped = 0;
            foreach (var entry in entries)
            {
                if (entry.ParquetIndex.HasValue &&
                    featByScoreIndex.TryGetValue(entry.ParquetIndex.Value, out double[] features))
                {
                    entry.Features = features;
                    nMapped++;
                }
            }
            return nMapped;
        }

        /// <summary>Counts one <see cref="TransferOneFile"/> call adds to. A struct rather
        /// than six ref parameters, so the per-file body could be lifted out of the whole-run
        /// loop without its signature becoming the reason not to.</summary>
        internal struct TransferTally
        {
            public int Unchanged, Moved, GapFill, Skipped, MissingSidecar, FilesDone;
        }

        /// <summary>
        /// Transfer ONE run's per-run q-values: build that run's own score->q tables from its own
        /// <c>.1st-pass.fdr_scores.bin</c>, then classify and re-map its survivors.
        ///
        /// <para>Nothing in it reads another run's state - the tables come from this run's
        /// sidecar and the experiment records are the analysis-wide map every run shares - which
        /// is what makes the mode a fan-out computation. It ran in the join, over the whole pool,
        /// until #4665 moved its caller to <see cref="Pass2PerFileWorker"/>; #4438 established
        /// the per-run form.</para>
        ///
        /// <para>Classifies every survivor by its reconciled feature score against its 1st-pass
        /// sidecar record - UNCHANGED (bit-exact score match: carry the 1st-pass record), MOVED
        /// (re-map run q through this file's tables) or GAP-FILL (no record: run q from the
        /// tables) - and gives each the precursor's pass-1 experiment values; see
        /// <see cref="AssignPerRunQ"/>. The sidecar Score is the averaged-model score, the SAME
        /// scale <see cref="FrozenModelScorer.Score"/> produces, so each table is
        /// scale-consistent by construction.</para>
        ///
        /// <para>Scores through <see cref="FrozenModelScorer"/>, so it applies whichever
        /// classifier the first pass trained. The transfer used to average the fold weights
        /// itself and inline the dot product, which threw on a gradient-boosted-tree model (no
        /// weights to average). The scorer reuses one buffer, so a caller running files in parallel needs
        /// one scorer per thread.</para>
        /// </summary>
        internal static void TransferOneFile(
            string fileName, string pass1Path, List<FdrEntry> survivors,
            FrozenModelScorer scorer,
            IReadOnlyDictionary<uint, FdrExperimentRecord> globalExperiment,
            Action<string> logWarning, ref TransferTally tally)
        {
            int nFeatures = scorer.NumFeatures;

            // Build this file's per-run tables + record map from its own 1st-pass sidecar.
            var firstPassByEntryId = new Dictionary<uint, FdrScoreRecord>();
            var precScores = new List<double>();
            var precQs = new List<double>();
            var pepScores = new List<double>();
            var pepQs = new List<double>();
            bool ok = FdrScoresSidecar.ReadRecords(
                pass1Path, FdrScoresSidecar.Pass.FirstPass, rec =>
            {
                firstPassByEntryId[rec.EntryId] = rec; // entry_id is unique per file (DeduplicatePairs)
                precScores.Add(rec.Score);
                precQs.Add(rec.RunPrecursorQvalue);
                pepScores.Add(rec.Score);
                pepQs.Add(rec.RunPeptideQvalue);
            });
            if (!ok || precScores.Count == 0)
            {
                tally.MissingSidecar++;
                logWarning(string.Format(
                    @"OSPREY_PASS2_QVALUE=transfer: could not read the first-pass intermediate file " +
                    @"for '{0}' ({1}); this file's run-level q-values are left unadjusted.", fileName, pass1Path));
                return;
            }
            BuildScoreToQTable(precScores, precQs, out double[] precScoresDesc, out double[] precQDesc);
            BuildScoreToQTable(pepScores, pepQs, out double[] pepScoresDesc, out double[] pepQDesc);

            foreach (var entry in survivors)
            {
                if (entry.Features == null || entry.Features.Length != nFeatures)
                {
                    // No reconciled features resolved (a stub/parquet mismatch the reload
                    // already warned about). Leave this ENTRY's q as-is rather than guess, and
                    // go on to the next one - a `return` here would abandon the rest of the
                    // file's survivors at their Stage-6 q AND skip the FilesDone count below,
                    // which is a whole run silently dropped for one entry's missing features.
                    tally.Skipped++;
                    continue;
                }
                double newScore = scorer.Score(entry.Features);

                FdrScoreRecord? rec1 = null;
                if (firstPassByEntryId.TryGetValue(entry.EntryId, out FdrScoreRecord recFound))
                    rec1 = recFound;
                // The precursor's analysis-wide pass-1 experiment record, which supplies
                // every disposition: an UNCHANGED or MOVED peak carries these values through,
                // and a gap-fill peak (no 1st-pass run-scope record) takes them so
                // ClampExperimentQToBestRun - a floor that only raises - lands it at the
                // precursor's best-run q. A precursor with no record anywhere gets the
                // default 1.0 q-values and a 0.0 aggregate, which pair correctly: never
                // competed, never accepted, so nothing reads it.
                FdrExperimentRecord? exp1 = null;
                if (globalExperiment.TryGetValue(entry.EntryId, out var expFound))
                    exp1 = expFound;
                switch (AssignPerRunQ(entry, newScore, rec1, exp1,
                    precScoresDesc, precQDesc, pepScoresDesc, pepQDesc))
                {
                    case PerRunClass.Unchanged: tally.Unchanged++; break;
                    case PerRunClass.Moved: tally.Moved++; break;
                    default: tally.GapFill++; break;
                }
            }
            // Counted only HERE, where the whole-run loop counted it: the unreadable-sidecar
            // path above returns first, so a run whose sidecar could not be read was never one
            // this pass finished.
            tally.FilesDone++;
        }

        /// <summary>How a survivor was classified against its 1st-pass sidecar record.</summary>
        internal enum PerRunClass
        {
            /// <summary>Reconciliation did not move the peak (recomputed score == the sidecar's).</summary>
            Unchanged,
            /// <summary>Reconciliation moved the peak to a different position (score differs).</summary>
            Moved,
            /// <summary>A new detection with no 1st-pass record (gap-fill).</summary>
            GapFill,
        }

        /// <summary>
        /// Assign one survivor's pass-2 q-values per the per-run-only invariant and return its
        /// classification. Pure (no I/O): the caller supplies the recomputed frozen-model score
        /// (<paramref name="newScore"/>), the entry's 1st-pass sidecar record
        /// (<paramref name="firstPass"/>, null for a gap-fill), that file's per-run lookup tables,
        /// and the precursor's analysis-wide pass-1 experiment record
        /// (<paramref name="firstPassExperiment"/>). The experiment values are NEVER derived from
        /// a table -- they are the pass-1 carry, frozen by the best-peak anchor, and every
        /// disposition takes them from the same place:
        /// <list type="bullet">
        /// <item>UNCHANGED (<paramref name="newScore"/> == the record's Score, bit-exact): carry the
        /// 1st-pass run-scope record verbatim.</item>
        /// <item>MOVED: run q re-mapped from the tables; PEP carried from the record.</item>
        /// <item>GAP-FILL (no run-scope record): run q from the tables.</item>
        /// </list>
        /// </summary>
        internal static PerRunClass AssignPerRunQ(
            FdrEntry entry,
            double newScore,
            FdrScoreRecord? firstPass,
            FdrExperimentRecord? firstPassExperiment,
            double[] precScoresDesc,
            double[] precQDesc,
            double[] pepScoresDesc,
            double[] pepQDesc)
        {
            // The EXPERIMENT-scope half is one record per entry_id for the whole analysis
            // (format v5, issue #4486), so every disposition below reads the same three values -
            // there is no longer a per-run copy for an unchanged peak to prefer over the
            // cross-file one a gap-fill peak fell back to. An entry with no record never
            // competed: q = 1.0, aggregate = 0.0.
            double expPrecQ = firstPassExperiment?.ExperimentPrecursorQvalue ?? 1.0;
            double expPepQ = firstPassExperiment?.ExperimentPeptideQvalue ?? 1.0;
            double expAgg = firstPassExperiment?.ExperimentAggregateScore ?? 0.0;
            // PEP rides with the other experiment-scope values: one per entry_id, from the
            // analysis-wide record rather than from any run's own file (issue #4486).
            double expPep = firstPassExperiment?.Pep ?? 1.0;
            if (firstPass.HasValue)
            {
                FdrScoreRecord rec1 = firstPass.Value;
                // Bit-exact equality is the reliable MOVED discriminator: an UNCHANGED survivor's
                // reconciled features ARE its original Stage-4 features (ReconciledParquetWriter
                // streams unchanged rows through untouched), and the sidecar Score was computed from
                // those same parquet features with this same averaged model -- so the recomputation
                // is bit-identical. A MOVED peak carries rescored features, so its score differs.
                if (newScore == rec1.Score)
                {
                    entry.Score = rec1.Score;
                    entry.RunPrecursorQvalue = rec1.RunPrecursorQvalue;
                    entry.RunPeptideQvalue = rec1.RunPeptideQvalue;
                    entry.ExperimentPrecursorQvalue = expPrecQ;
                    entry.ExperimentPeptideQvalue = expPepQ;
                    entry.Pep = expPep;
                    entry.ExperimentAggregateScore = expAgg;
                    return PerRunClass.Unchanged;
                }
                entry.Score = newScore;
                entry.RunPrecursorQvalue = LookupQForScore(newScore, precScoresDesc, precQDesc);
                entry.RunPeptideQvalue = LookupQForScore(newScore, pepScoresDesc, pepQDesc);
                // Experiment q is a pass-1 property (best-peak anchor) -- carry it, never re-map.
                entry.ExperimentPrecursorQvalue = expPrecQ;
                entry.ExperimentPeptideQvalue = expPepQ;
                entry.Pep = expPep;
                // Carried with the experiment q for the same reason, and NOT re-derived from
                // newScore: it is the score that pass-1 experiment q was computed from, so
                // re-mapping it to the rescored value would break the pairing that is the
                // whole point of persisting it.
                entry.ExperimentAggregateScore = expAgg;
                return PerRunClass.Moved;
            }
            entry.Score = newScore;
            entry.RunPrecursorQvalue = LookupQForScore(newScore, precScoresDesc, precQDesc);
            entry.RunPeptideQvalue = LookupQForScore(newScore, pepScoresDesc, pepQDesc);
            entry.ExperimentPrecursorQvalue = expPrecQ;
            entry.ExperimentPeptideQvalue = expPepQ;
            // Carried for the same reason as the experiment q beside it, and from the same
            // record: the aggregate is a per-entry roll-up, so a gap-fill is entitled to it even
            // with no run-scope record of its own. Leaving it at ResetScores' 0.0 would persist
            // a real experiment q next to a score that q was not computed from, and a
            // score-space acceptance boundary built from the 2nd-pass artifacts would then be
            // drawn from the wrong ranking.
            entry.ExperimentAggregateScore = expAgg;
            return PerRunClass.GapFill;
        }

        /// <summary>Number of equal-count score-quantile bins
        /// <see cref="BuildScoreToQTable"/> smooths the per-entry q into. Large enough to
        /// trace the FDR curve finely, small enough that each bin averages out the
        /// per-entry q noise from the raw-vs-calibrated score scale mismatch.</summary>
        private const int SCORE_Q_TABLE_BINS = 1000;

        /// <summary>
        /// Build the score-&gt;q lookup table from parallel (score, q) lists (the raw
        /// averaged-model score paired with the unbiased 1st-pass effective q). A calibrated
        /// q is monotone NON-INCREASING in score, but the per-entry pairs are not
        /// individually monotone (the stored 1st-pass q was computed on the per-fold
        /// calibrated CV score, a different scale from this raw averaged-model score), so a
        /// running-min/max envelope would collapse to the global extreme on one outlier.
        /// Instead: (1) sort by score ascending; (2) partition into
        /// <see cref="SCORE_Q_TABLE_BINS"/> equal-count quantile bins and take each bin's
        /// MEAN q; (3) run pool-adjacent-violators (isotonic regression) so q is
        /// non-decreasing as score decreases. Emits parallel arrays:
        /// <paramref name="scoresDesc"/> (bin score, descending) and <paramref name="qDesc"/>
        /// (isotonic bin-mean q, non-decreasing as score decreases).
        /// </summary>
        internal static void BuildScoreToQTable(
            IReadOnlyList<double> scores,
            IReadOnlyList<double> qs,
            out double[] scoresDesc,
            out double[] qDesc)
        {
            int nPts = scores.Count;
            var order = new int[nPts];
            for (int i = 0; i < nPts; i++)
                order[i] = i;
            // Sort indices by score ASCENDING (ties by q ascending, deterministic).
            Array.Sort(order, (a, b) => // Array.Sort OK: quantile-bin means are tie-order-insensitive, and this table feeds only the OSPREY_PASS2_QVALUE=transfer path (never cross-impl parity output)
            {
                int c = scores[a].CompareTo(scores[b]);
                if (c != 0)
                    return c;
                return qs[a].CompareTo(qs[b]);
            });

            int nBins = Math.Min(SCORE_Q_TABLE_BINS, nPts);
            var binScoreAsc = new double[nBins];   // representative (max) score in bin
            var binQAsc = new double[nBins];        // mean q in bin
            for (int b = 0; b < nBins; b++)
            {
                // Equal-count partition of the ascending-sorted points.
                int start = (int)((long)b * nPts / nBins);
                int end = (int)((long)(b + 1) * nPts / nBins);
                if (end <= start)
                    end = start + 1;
                double qSum = 0.0;
                double maxScore = double.NegativeInfinity;
                for (int k = start; k < end; k++)
                {
                    int idx = order[k];
                    qSum += qs[idx];
                    if (scores[idx] > maxScore)
                        maxScore = scores[idx];
                }
                binScoreAsc[b] = maxScore;
                binQAsc[b] = qSum / (end - start);
            }

            // Pool-adjacent-violators (isotonic regression) over the ascending-score bins to
            // force q NON-INCREASING as score increases. Blocks are stored low-score-first;
            // blockW[j] counts bins from the low-score end.
            var blockQ = new double[nBins];
            var blockW = new int[nBins];
            int nBlocks = 0;
            for (int b = 0; b < nBins; b++)
            {
                double q = binQAsc[b];
                int w = 1;
                while (nBlocks > 0 && blockQ[nBlocks - 1] < q)
                {
                    double pooledSum = blockQ[nBlocks - 1] * blockW[nBlocks - 1] + q * w;
                    w += blockW[nBlocks - 1];
                    q = pooledSum / w;
                    nBlocks--;
                }
                blockQ[nBlocks] = q;
                blockW[nBlocks] = w;
                nBlocks++;
            }
            // Expand blocks back to per-bin isotonic q (low-score-first).
            var binQIso = new double[nBins];
            int fillLo = 0;
            for (int j = 0; j < nBlocks; j++)
            {
                for (int c = 0; c < blockW[j]; c++)
                {
                    binQIso[fillLo] = blockQ[j];
                    fillLo++;
                }
            }

            // Emit descending-by-score (highest score first) for LookupQForScore.
            scoresDesc = new double[nBins];
            qDesc = new double[nBins];
            for (int b = 0; b < nBins; b++)
            {
                scoresDesc[b] = binScoreAsc[nBins - 1 - b];
                qDesc[b] = binQIso[nBins - 1 - b];
            }
        }

        /// <summary>
        /// Map a score to a q via the score-&gt;q table built by
        /// <see cref="BuildScoreToQTable"/>. Binary search for the deepest table entry whose
        /// score is still &gt;= the query score and return its q; clamp at both ends (a score
        /// above the table max gets the table's minimum q; a score below the table min gets
        /// the maximum q).
        /// </summary>
        internal static double LookupQForScore(
            double score, double[] scoresDesc, double[] qDesc)
        {
            int n = scoresDesc.Length;
            if (n == 0)
                return 1.0;
            // scoresDesc is descending; qDesc is non-decreasing along it. A score above the
            // best table score is the most confident -> the minimum q at qDesc[0]; a score
            // below the worst table score is the least confident -> the maximum q at qDesc[n-1].
            if (score > scoresDesc[0])
                return qDesc[0];
            if (score <= scoresDesc[n - 1])
                return qDesc[n - 1];
            // Largest index i such that scoresDesc[i] >= score (deepest table position still
            // at least as good as the query); qDesc non-decreasing -> most conservative q.
            int lo = 0, hi = n - 1, best = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (scoresDesc[mid] >= score)
                {
                    best = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return qDesc[best];
        }

        /// <summary>
        /// Resolves each per-file key to its input file, and carries the stamp the verifier's
        /// diagnostic dump embeds.
        ///
        /// <para>It wrote every per-run <c>.2nd-pass.fdr_scores.bin</c> once - from three paths,
        /// which is why it existed. SecondPassFDR writes none now (#4665): PerFileRescoring
        /// writes every one.</para>
        /// </summary>
        private sealed class Pass2SidecarWriter
        {
            private readonly ArtifactStamp _stamp;
            private readonly Dictionary<string, string> _inputByFileName =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public Pass2SidecarWriter(OspreyConfig config, string taskName, string taskValidityKey)
            {
                _stamp = ArtifactStamp.ForCurrentBuild(taskName, taskValidityKey);
                if (config.InputFiles == null)
                    return;
                foreach (string inputFile in config.InputFiles)
                    _inputByFileName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;
            }

            /// <summary>The validity stamp the verifier's diagnostic dump embeds.</summary>
            public ArtifactStamp Stamp => _stamp;

            /// <summary>
            /// The input file a per-file key names, or null when no <c>config.InputFiles</c>
            /// entry matches it - a name drift between Stage 5 and Stage 7, which
            /// <see cref="RequireWorkerAnswers"/> refuses before anything is folded.
            /// </summary>
            public string InputFor(string fileName)
            {
                return _inputByFileName.TryGetValue(fileName, out string inputFile) ? inputFile : null;
            }
        }
    }
}
