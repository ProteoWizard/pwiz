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
using System.Linq;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;
using pwiz.Osprey.ML;

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
        /// Run the 2nd-pass FDR / sidecar persistence step for SecondPassFDR.
        /// Only invoked when protein FDR is enabled (the sole consumer of the
        /// 2nd-pass q-values). <paramref name="taskName"/> and
        /// <paramref name="taskValidityKey"/> are the owning task's identity,
        /// stamped into each inline per-file validity sidecar.
        /// </summary>
        /// <returns>
        /// The second-pass Percolator model's <see cref="FeatureContributions"/> when
        /// this call actually retrained (feature histograms included if
        /// <c>--model-diagnostics</c>), for the model-diagnostics pass-2 model view;
        /// null when the 2nd-pass scores were rehydrated from sidecars (no retrain) or
        /// the method is not Percolator.
        /// </returns>
        internal static FeatureContributions ComputeAndPersist(
            PipelineContext ctx,
            bool anyRescoreWork,
            RescoredEntries rescored,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            string taskName,
            string taskValidityKey)
        {
            var config = ctx.Config;
            // The one buffer behind the milestone. Taken here rather than as a second
            // parameter so the frozen-competition path (which reads the milestone) and
            // every other path (which reads the buffer) cannot be handed different pools.
            var perFileEntries = rescored.Value;
            FeatureContributions pass2Contributions = null;

            // OSPREY_PASS2_QVALUE selects how this 2nd pass assigns reported q-values.
            // Log the active mode once so a run's provenance is in the log. An unrecognized
            // token never reaches here: Program aborts at startup.
            if (OspreyEnvironment.Pass2TransferQ)
            {
                ctx.LogInfo(string.Format(
                    "OSPREY_PASS2_QVALUE={0}: pass-2 carries the pass-1 q through and re-maps ONLY the " +
                    "per-run q of reconciliation-moved peaks (frozen 1st-pass model + each file's own " +
                    "score->run-q table); experiment q is frozen by the best-peak anchor, no retrain.",
                    OspreyEnvironment.PASS2_QVALUE_TRANSFER));
            }

            // Frozen 2nd-pass modes need the trained 1st-pass model. On a distributed
            // --task SecondPassFDR node (or any resume that skipped 1st-pass training)
            // it was never published in-process; reload it from the per-file sidecar and
            // publish so the frozen dispatch below finds it instead of fail-fasting. No-op
            // when the model is already present, the mode is the default retrain, or the
            // sidecar is absent (the existing fail-fast then applies).
            // protein-compact needs the ProteinCompactStratum too; it rides in the same
            // sidecar, so one reload serves all three frozen modes.
            bool wantsFrozenModel = OspreyEnvironment.Pass2TransferQ ||
                                    OspreyEnvironment.Pass2TransferCompete ||
                                    OspreyEnvironment.Pass2ProteinCompact;
            if (wantsFrozenModel && !ctx.TryGet<FirstPassPercolatorModel>(out _))
            {
                var reloaded = FirstPassModelIO.LoadFromAny(perFileParquetPaths);
                if (reloaded != null)
                {
                    // ExperimentAgg is what the TRAINING process ran under (null on a sidecar
                    // written before the field existed). This node's own OSPREY_EXPERIMENT_AGG
                    // says nothing about it, so carry the recorded value rather than re-reading.
                    ctx.Publish(new FirstPassPercolatorModel
                        { Results = reloaded.Model, ExperimentAgg = reloaded.ExperimentAgg });
                    ctx.LogInfo(string.Format(
                        @"Reloaded persisted 1st-pass model sidecar for frozen 2nd-pass (pass-1 " +
                        @"experiment aggregation: {0}).",
                        reloaded.ExperimentAgg ?? @"not recorded"));

                    // Only publish a stratum the sidecar actually carried. Leaving it absent
                    // keeps the existing fail-fast, which is the honest outcome: an empty
                    // stratum would silently constrain the competition to nothing.
                    if (OspreyEnvironment.Pass2ProteinCompact && reloaded.StratumBaseIds != null &&
                        !ctx.TryGet<ProteinCompactStratum>(out _))
                    {
                        ctx.Publish(new ProteinCompactStratum(reloaded.StratumBaseIds));
                        ctx.LogInfo(string.Format(
                            @"Reloaded the persisted protein-compact stratum ({0} base ids).",
                            reloaded.StratumBaseIds.Count));
                    }
                }
            }

            // When the projection 2nd-pass compute ran (flag on), this holds the scored
            // FdrProjectionSet -- non-null is the flag that the StreamingSink already
            // wrote each file's .2nd-pass.fdr_scores.bin + validity sidecar DURING the
            // score pass (issue #4355 struct-shrink S0 / C1: the q-values are never
            // stored on the projection). The resident write block below is then only
            // for the flag-off / resume path. Null on the resident path (flag off) and
            // on the skip / resume path. (#4374)
            FdrProjectionSet pass2Projections = null;

            // The frozen-model COMPETITION modes - transfer-compete, and protein-compact unless
            // it was told to retrain. These own their whole per-file cycle (materialize, score,
            // compete, write the sidecar, drop the file), so they skip both the whole-pool
            // pass-1 scalar seed above and the resident sidecar write below.
            bool frozenCompetition =
                OspreyEnvironment.Pass2TransferCompete ||
                (OspreyEnvironment.Pass2ProteinCompact && !OspreyEnvironment.Pass2ProteinCompactRetrain);

            // True once a path has written every file's .2nd-pass.fdr_scores.bin itself, which
            // is what the resident write block below tests before repeating the work.
            bool pass2SidecarsWritten = false;

            // True when THIS run computed the second-pass values, rather than carrying the
            // standing ones. It decides whether the entries or the on-disk sidecars are
            // authoritative going into the write below - not whether a file gets written.
            bool recomputed = false;

            // The files whose per-run 2nd-pass sidecar the RESCORE WORKER owns (#4486). Empty
            // when no worker produced one, which is the pre-move behaviour throughout.
            //
            // Determined FROM DISK, not from a published byproduct. The byproduct answered
            // correctly in-process and wrongly in an HPC chain, where PerFileRescoring and
            // SecondPassFDR are separate PROCESSES: nothing published in Stage 6 reaches Stage
            // 7, so Stage 7 concluded no worker had run and rewrote every sidecar with survivors
            // only - 332,269 records against the straight route's 407,624, on artifacts that are
            // supposed to be route-independent. An in-memory signal cannot answer a question
            // about a file that outlives the process.
            //
            // The artifact answers it itself: the sidecar exists and carries a PerFileRescoring
            // validity stamp with the current key. That is exactly "presence is the indicator,
            // and you never have to open a file to learn who wrote it".
            var workerWroteFiles = WorkerOwnedPass2Sidecars(ctx);

            // The one per-file .2nd-pass.fdr_scores.bin writer, shared by every path that
            // emits one - the projection score pass's flush callback, the frozen streamed
            // competition, and the resident write block below - so the resume skip, the
            // validity sidecar and the summary counts are decided in one place rather than
            // reimplemented per path.
            var pass2Writer = new Pass2SidecarWriter(ctx, config, taskName, taskValidityKey);
            var pass2Tally = pass2Writer.Tallies;

            // Run 2nd-pass Percolator on the post-reconciliation
            // entries when any 2nd-pass FDR sidecar is missing.
            // Mirrors Rust pipeline.rs:4394-4468. After Stage 6
            // reconciliation, the entries' Features have been
            // overwritten with rescored values, but their Scores
            // are still the 1st-pass Percolator output (from
            // FirstPassFdrTask). Without this 2nd-pass run, protein
            // FDR (Stage 8) and the blib output would use stale
            // 1st-pass scores; in the HPC distribution case the
            // straight-through pipeline would silently lose ~25%
            // of the precursors it produces -- the missing
            // 2nd-pass step was the root cause behind the C#
            // Stage 7 algorithmic divergence (issue: "Bug C").
            if (perFileParquetPaths.Count > 0 && config.InputFiles != null)
            {
                // Surface any perFileEntries key that has no matching
                // entry in config.InputFiles -- a silent skip here would
                // hide a name-drift bug that the standard cross-impl gate
                // (where keys always match) cannot catch.
                var unmatchedKeys = pass2Writer.UnmatchedKeys(
                    perFileEntries.Select(kvp => kvp.Key));
                if (unmatchedKeys.Count > 0)
                {
                    ctx.LogWarning(string.Format(
                        "--task SecondPassFDR: {0} perFileEntries key(s) have no matching " +
                        "config.InputFiles entry and will be skipped: [{1}]. This usually " +
                        "indicates an input-file rename or path drift between Stage 5 and " +
                        "Stage 7; the skipped files will not get a 2nd-pass sidecar.",
                        unmatchedKeys.Count, string.Join(", ", unmatchedKeys)));
                }

                int missingPass2 = 0;
                int totalFiles = 0;
                foreach (var kvp in perFileEntries)
                {
                    totalFiles++;
                    // A key with no input file is not "missing" - it is unmatched, reported
                    // above, and gets no sidecar either way.
                    if (pass2Writer.InputFor(kvp.Key) == null)
                        continue;
                    if (!pass2Writer.IsCurrent(kvp.Key))
                        missingPass2++;
                }
                // The RECOMPUTE gate, and only that. Every file gets a sidecar written below
                // whichever way this goes: what is conditional is whether the values in it were
                // computed by this run or carried from the standing ones.
                //
                // anyRescoreWork is Rust's `total_rescored > 0` (pipeline.rs:5209): with no
                // reconciliation, multi-charge consensus or gap-fill anywhere in the cohort
                // there is nothing for a second Percolator pass to re-score, and the standing
                // first-pass values ARE the second-pass answer.
                // A sidecar the RESCORE WORKER wrote this run does NOT mean the second pass is
                // done (#4486). It means the PER-FILE HALF is done. The join - the experiment
                // competition, PEP, the protein FDR and the analysis-wide experiment sidecar -
                // has not run, and skipping it leaves every entry on its pre-competition values
                // and writes no .2nd-pass.fdr_experiment.bin at all.
                //
                // Measured, not theorised: without this clause Stage 7 finished in 2.7s, mode1c
                // reported the experiment sidecar absent, and mode1's discovery set moved by 32
                // RefSpectra keys - one cause, four failing modes. It also silently disabled the
                // worker-vs-recompute assertion, so the run looked agreeable precisely because
                // nothing was compared.
                bool workerDidPerFileHalf = workerWroteFiles != null && workerWroteFiles.Count > 0;
                recomputed = anyRescoreWork && (missingPass2 > 0 || workerDidPerFileHalf);
                if (recomputed)
                {
                    // LogInfo, not LogVerbose: this is the heading for the longest stretch of
                    // work left in Stage 7, and a --verbose-only heading is invisible on the runs
                    // that actually take the time (#4571).
                    ctx.LogInfo(string.Format(
                        "{0}/{1} file(s) have no precomputed second-pass FDR scores - computing " +
                        "them here from the reconciled features (reused distributed-run code path).",
                        missingPass2, totalFiles));
                    // Stage 6's post-rescore overlay calls FdrEntry.ResetScores(), which clears
                    // eight fields - one for every scalar the v4 record carries. Three of them
                    // can reach the sidecar at their reset defaults (issue #4553):
                    //   Score                    - neither COMPETITION mode wrote one back
                    //                              (transfer-compete, protein-compact). The
                    //                              transfer mode's AssignPerRunQ does set it,
                    //                              on all three of its branches.
                    //   Pep                      - written only on the on-stratum path
                    //   ExperimentAggregateScore - the third field of the same gap (sidecar v4,
                    //                              issue #4522); no frozen 2nd-pass mode writes
                    //                              it back, so it lands at 0.0 for every peak
                    //                              Stage 6 touched.
                    // ExperimentProteinQvalue is the fourth field ResetScores clears that no
                    // 2nd-pass mode writes back, and it is deliberately NOT seeded here: its
                    // pass-2 producer is WritePass2ExperimentSidecar, which writes the second-pass
                    // value into the 2nd-pass sidecar after the second-pass protein FDR (#4559).
                    // Seeding those three from the 1st-pass sidecar reproduces exactly what the
                    // distributed route has in hand at this point, which is why that route never
                    // showed the loss: it must rehydrate from that same sidecar, and the sidecar
                    // carries all seven scalars. Whatever pass 2 genuinely recomputes then
                    // overwrites the seed. Done ahead of the mode dispatch because the loss is
                    // not specific to one mode.
                    //
                    // Timed separately from swPass2: this streams every file's ENTIRE 1st-pass
                    // sidecar (the pre-compaction pool), so billing it to the pass-2 stage wall
                    // would show a jump in [STAGE-WALL] second-pass-fdr with no pass-2 change
                    // and no way to attribute it.
                    // Skipped on the frozen COMPETITION path, which seeds each file inside its
                    // own per-file materialization: running both would read every file's
                    // 1st-pass sidecar twice, and - the point of #4486 - this loop can only walk
                    // a pool that is resident, which that path exists not to build.
                    if (!frozenCompetition)
                    {
                        var swRestore = Stopwatch.StartNew();
                        RestorePass1Scalars(ctx, perFileEntries, pass2Writer);
                        swRestore.Stop();
                        ctx.LogVerbose(string.Format(
                            "[STAGE-WALL] pass-1 scalar restore: {0:F1}s", swRestore.Elapsed.TotalSeconds));
                    }

                    var swPass2 = Stopwatch.StartNew();

                    // The frozen COMPETITION modes (transfer-compete, protein-compact) re-score
                    // with the frozen 1st-pass model over the full pre-compaction population /
                    // protein stratum - a competition the projection engine does not do (it
                    // trains + competes over the survivor set only). They own the whole per-file
                    // cycle: materialize, score, compete, write the sidecar, drop, so they need
                    // neither the projection engine nor a resident pool.
                    // protein-compact + OSPREY_PROTEIN_COMPACT_RETRAIN=1 is the exception: it
                    // retrains, so it stays on the projection (streaming-retrain) path.
                    //
                    // --model-diagnostics needs the resident 2nd-pass model: its feature
                    // contributions feed the pass-2 model view, and the projection 2nd pass
                    // streams through a sink and produces none. Route --model-diagnostics to
                    // the resident path so ComputePass2Resident can return the model. Off the
                    // default output path, so byte-identity is unaffected (#4377).
                    // transfer takes the resident path too: it needs each survivor's RECONCILED
                    // features on entry.Features, which ComputePass2Resident does.
                    if (frozenCompetition)
                    {
                        pass2SidecarsWritten = ComputePass2FrozenCompetition(
                            ctx, rescored, perFileParquetPaths, config, pass2Writer);
                    }
                    else if (OspreyEnvironment.UseFdrProjection && config.FdrMethod.UsesPercolatorFramework() &&
                        !config.ModelDiagnostics && !OspreyEnvironment.Pass2TransferQ)
                    {
                        // Projection 2nd pass (issue #4374 + #4355 struct-shrink S0 / C1):
                        // stream the reconciled PIN features through the SAME projection
                        // engine the 1st pass uses, rather than loading every survivor's
                        // 21-feature vector resident. The lean projection no longer stores
                        // the q-values (2nd-pass peak 80 -> 32 B); a StreamingSink assembles
                        // each .2nd-pass.fdr_scores.bin record DURING the score pass (from
                        // the streamed q-values + the survivor's ExperimentProteinQvalue looked up
                        // by entry_id) and flushes the per-file sidecar + validity sidecar
                        // directly, so the resident write block below is skipped for this
                        // path. The existing entry_id overlay still carries the 2nd-pass
                        // q-values onto the resident survivor buffer afterward (unchanged).

                        // Survivor ExperimentProteinQvalue by entry_id, per file: the value
                        // BuildFromEntries used to carry onto the struct. All survivors
                        // sharing an entry_id share a precursor (hence a ModifiedSequence,
                        // hence a experiment_protein_qvalue), so the last-write map is exact.
                        var survivorsByFile =
                            new Dictionary<string, List<FdrEntry>>(StringComparer.Ordinal);
                        foreach (var kvp in perFileEntries)
                            survivorsByFile[kvp.Key] = kvp.Value;

                        IReadOnlyDictionary<uint, double> ResolveProteinQ(string fileName)
                        {
                            var map = new Dictionary<uint, double>();
                            if (survivorsByFile.TryGetValue(fileName, out var survivors))
                            {
                                foreach (var e in survivors)
                                    map[e.EntryId] = e.ExperimentProteinQvalue;
                            }
                            return map;
                        }

                        // Per-file flush: write the .2nd-pass.fdr_scores.bin from the
                        // assembled records, sourced from records instead of the resident
                        // buffer. The write body itself - resume skip, validity sidecar,
                        // tallies - is the writer's, shared with every other path that emits
                        // one of these files.
                        void FlushPass2File(string fileName, IReadOnlyList<FdrScoreRecord> records)
                        {
                            pass2Writer.Write(fileName, records);
                        }

                        pass2Projections = ComputePass2Projection(
                            ctx, perFileEntries, perFileParquetPaths, config,
                            ResolveProteinQ, FlushPass2File);
                    }
                    else
                    {
                        // Resident 2nd pass (flag off): the byte-identity oracle. Reload
                        // every survivor's PIN features resident, then run the resident
                        // Percolator over the full FdrEntry survivor buffer.
                        pass2Contributions = ComputePass2Resident(ctx, perFileEntries, perFileParquetPaths, config);
                    }
                    swPass2.Stop();
                    ctx.LogInfo(string.Format(
                        "[STAGE-WALL] second-pass-fdr: {0:F1}s",
                        swPass2.Elapsed.TotalSeconds));
                }
            }

            // Not recomputed, so the entries still carry the standing first-pass values while
            // any sidecar already on disk carries a previous run's SECOND-pass ones. Load those
            // back onto the entries before the write below, so the write puts the same bytes
            // back instead of quietly downgrading a resumed run's file to pass-1 values. A file
            // with no sidecar yet - a first run with no rescore work - simply has nothing to
            // load, and the write gives it the standing values, which are its answer.
            if (!recomputed)
                ReloadPass2Sidecars(ctx, pass2Writer, perFileEntries, @"pre-write");

            // Persist post-Stage-6 per-file 2nd-pass FDR scores
            // BEFORE RunProteinFdr. The sidecar holds Score +
            // run/experiment precursor/peptide q-values + Pep +
            // ExperimentAggregateScore + ExperimentProteinQvalue
            // (the last set by RunFirstPassProteinFdr earlier).
            // Exactly one of them is not final yet --
            // ExperimentProteinQvalue, which the second-pass protein FDR
            // has not run to produce - which is why that column is
            // patched back into this file from RunProteinFdr rather than
            // written here (#4559). Writing here lets the
            // OSPREY_STAGE7_PROTEIN_FDR_ONLY early exit (used
            // by stage6 isolation in Test-Regression) leave the
            // sidecar on disk for downstream rehydration.
            // Every file's sidecar is (re)written unconditionally - WriteCore
            // documents why the skip-when-already-present probe was removed (a
            // conditionally-written file makes its own absence ambiguous, and
            // the second pass is deterministic, so a rewrite is the same bytes).
            // The planned scope split - immutable per-run sidecars plus one
            // experiment-scope sidecar beside the blib - retires this
            // write-then-patch shape entirely.
            if (perFileParquetPaths.Count > 0 && config.InputFiles != null)
            {
                // Surface any perFileEntries key not in config.InputFiles
                // -- a silent skip below would mean that file gets no
                // .2nd-pass sidecar written and the next resume re-runs
                // its second-pass FDR unnecessarily.
                var unmatchedSidecarKeys = pass2Writer.UnmatchedKeys(
                    perFileEntries.Select(kvp => kvp.Key));
                if (unmatchedSidecarKeys.Count > 0)
                {
                    ctx.LogWarning(string.Format(
                        "2nd-pass sidecar write: {0} perFileEntries key(s) have no matching " +
                        "config.InputFiles entry and will be skipped: [{1}].",
                        unmatchedSidecarKeys.Count, string.Join(", ", unmatchedSidecarKeys)));
                }

                // Compute the task validity key once so each per-file
                // .SecondPassFDR.osprey.task sidecar carries an identical
                // key. AnalysisPipeline.WriteTaskSidecars also writes
                // these at end-of-Run, but that step is bypassed when
                // OspreyDiagnosticsLog.ExitAfterDump calls Environment.Exit
                // (the test-snapshot stage7 / OSPREY_STAGE7_PROTEIN_FDR_ONLY
                // path). Writing inline next to each 2nd-pass binary
                // makes the per-file resume contract survive that
                // early exit, so a downstream run sees a fully
                // resume-able boundary file pair (binary + validity
                // sidecar) for every file that completed.

                // Resident / resume path only: write each file's .2nd-pass sidecar from
                // the resident survivor buffer. The projection path (pass2Projections
                // != null, issue #4355 struct-shrink S0 / C1) and the frozen competition
                // (#4486) both wrote the .bin + validity sidecar per file as they went, so
                // this loop is skipped for them - only the shared tallies they updated
                // drive the summary log below.
                if (pass2Projections == null && !pass2SidecarsWritten)
                {
                    // Per-file progress: this writes one .2nd-pass.fdr_scores.bin per file
                    // (~4.8 GB across 82) and was silent, which with the reload loop below is
                    // the 38s gap perfviz reports between the competition's [STAGE-WALL] line
                    // and the next probe (#4486). IO-paced, like the other disk loops here.
                    using (var writeProgress = new ProgressReporter(
                        string.Format(@"Writing 2nd-pass FDR scores for {0} file(s)", perFileEntries.Count),
                        perFileEntries.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
                    {
                        long nWrittenReported = 0;
                        foreach (var kvp in perFileEntries)
                        {
                            writeProgress.Report(++nWrittenReported);
                            pass2Writer.Write(kvp.Key, kvp.Value);
                        }
                    }
                }
                if (pass2Tally.Failures == 0 && pass2Tally.Written > 0)
                {
                    ctx.LogVerbose(string.Format(
                        @"Wrote 2nd-pass FDR scores for {0} file(s)", pass2Tally.Written));
                }
                // Said out loud rather than inferred from a smaller count: this is the one
                // path that leaves a file untouched, and an unexplained gap between the file
                // count and the write count is exactly the ambiguity always-writing removes.
                if (pass2Tally.Skipped > 0)
                {
                    ctx.LogVerbose(string.Format(
                        @"Left {0} 2nd-pass FDR sidecar(s) untouched (--task ModelDiagnostics writes no artifact but the report)",
                        pass2Tally.Skipped));
                }
            }

            // Re-load 2nd-pass FDR sidecar onto the post-compaction stub list.
            // After the post-Stage-6 rehydration path, every stub still carries
            // the 1st-pass q-values from RescoreHydration's 1st-pass sidecar
            // overlay (PerFileScoringTask). The 2nd-pass q-values produced by
            // Stage 6's reconciliation-aware rescore live in the
            // .2nd-pass.fdr_scores.bin sidecar (or were just computed above and
            // written to it). RunProteinFdr's detected_peptides gate filters on
            // ExperimentPrecursorQvalue, which has to be the 2nd-pass value to
            // match Rust pipeline.rs:4480-4494's reload-then-second-pass-FDR
            // sequence. Without this reload, single-file --task SecondPassFDR runs
            // include ~19 borderline peptides whose 1st-pass q-value passes
            // <=1% but 2nd-pass q-value does not, producing a 1-protein delta
            // in the Stage 7 picked-protein output cross-impl.
            // Only after a RECOMPUTE: on the not-recomputed path the pre-write reload
            // above already overlaid every sidecar onto these same entries and the
            // write put identical bytes back, so a second read of the whole sidecar
            // set (~4.8 GB at 82 files) applied values the entries already hold.
            if (recomputed && perFileParquetPaths.Count > 0 && config.InputFiles != null)
                ReloadPass2Sidecars(ctx, pass2Writer, perFileEntries, @"post-write");


            return pass2Contributions;
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

        private static void ReloadPass2Sidecars(
            PipelineContext ctx,
            Pass2SidecarWriter writer,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            string phase)
        {
            int filesReloaded = 0;
            int filesMissing = 0;
            // Resolved once for the whole reload; the overlay applies them per file, to the
            // records that file's own sidecar carries (format v5, issue #4486).
            var experimentRecords = ResolvePass2ExperimentRecords(ctx);
            // Per-file progress: reads back every file's sidecar and rebuilds an entry_id map
            // over that file's survivors. Silent, and the second half of the 38s gap between
            // the competition's [STAGE-WALL] line and the next probe (#4486); the write loop is
            // the first half.
            using (var reloadProgress = new ProgressReporter(
                string.Format(@"Reloading 2nd-pass FDR scores ({0}) for {1} file(s)",
                              phase, perFileEntries.Count),
                perFileEntries.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                long nReloadReported = 0;
                foreach (var kvp in perFileEntries)
                {
                    reloadProgress.Report(++nReloadReported);
                    string inputFile = writer.InputFor(kvp.Key);
                    if (inputFile == null)
                        continue;
                    string pass2Path = FdrScoresSidecar.Pass2Path(inputFile);
                    if (!FdrScoresSidecar.IsCurrentFormat(pass2Path, FdrScoresSidecar.Pass.SecondPass))
                    {
                        filesMissing++;
                        continue;
                    }
                    var byEntryId = new Dictionary<uint, FdrEntry>(kvp.Value.Count);
                    foreach (var e in kvp.Value)
                        byEntryId[e.EntryId] = e;
                    if (FdrScoresSidecar.TryReadOverlay(
                            pass2Path, byEntryId, FdrScoresSidecar.Pass.SecondPass,
                            experimentRecords))
                    {
                        filesReloaded++;
                    }
                    else
                    {
                        filesMissing++;
                        ctx.LogWarning(string.Format(
                            "Failed to reload 2nd-pass FDR sidecar for {0} ({1}); " +
                            "protein FDR will use stale 1st-pass q-values",
                            kvp.Key, pass2Path));
                    }
                }
            }
            if (filesReloaded > 0)
            {
                ctx.LogVerbose(string.Format(
                    "Reloaded 2nd-pass FDR scores ({0}) for {1}/{2} file(s) post-compaction",
                    phase, filesReloaded, filesReloaded + filesMissing));
            }
        }

        /// <summary>
        /// Re-seed each survivor's <see cref="FdrEntry.Score"/> and <see cref="FdrEntry.Pep"/>
        /// from that file's <c>.1st-pass.fdr_scores.bin</c>.
        ///
        /// <para>These are the sidecar fields <see cref="FdrEntry.ResetScores"/> clears that
        /// pass 2 does not reliably recompute: neither COMPETITION mode wrote <c>Score</c>
        /// back (the <c>transfer</c> mode's <c>AssignPerRunQ</c> does, on all three branches),
        /// and <c>Pep</c> is written only for on-stratum survivors. Left unseeded they reach
        /// the 2nd-pass sidecar at their reset defaults, where a q-value of 1.0 reads as a
        /// confident rejection and a <c>Score</c> of 0 sits exactly ON the discriminant's
        /// accept/reject boundary (issue #4553).</para>
        ///
        /// <para>Seeding, not overriding: whatever pass 2 genuinely recomputes is written after
        /// this and wins. What is left is the pass-1 value, which is precisely what the
        /// distributed route holds at the same point - it rehydrates from this same sidecar -
        /// so the two routes agree by construction rather than by coincidence.</para>
        ///
        /// <para><see cref="FdrEntry.ExperimentProteinQvalue"/> was seeded here too until
        /// issue #4559. It should not be: the 2nd-pass sidecar's protein column is a
        /// SECOND-pass value, written by <see cref="WritePass2ExperimentSidecar"/> after the
        /// second-pass protein FDR has run. Seeding it here made a pass-1 value the one it
        /// carried on every route - and because both routes copied the same wrong value, no
        /// two-route comparison could see it. One producer, not two.</para>
        ///
        /// <para>An entry Stage 6 did not touch already holds these values, so the write is a
        /// no-op for it; a gap-fill entry is absent from the sidecar and keeps the reset
        /// defaults until pass 2 scores it. One file's records stream at a time.</para>
        /// </summary>
        private static void RestorePass1Scalars(
            PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            Pass2SidecarWriter writer)
        {
            // Reported because this is the longest silent step left in Stage 7 (#4571): it streams
            // every file's ENTIRE 1st-pass sidecar - the PRE-compaction pool, 345,024,871 records
            // at 82 files, not the 89 M survivors - and logged nothing while doing it. On the
            // 82-file SEA-AD runs of 2026-08-12/14 that was the 130-141 s gap, and on the
            // --task SecondPassFDR leg the same step is a 127 s gap. It ran unbracketed: the
            // "N/M file(s) have no precomputed second-pass FDR scores" heading above was
            // LogVerbose (this change promotes it to LogInfo, so it is now visible), and the
            // swRestore duration goes out as a [STAGE-WALL] line, which OspreyOutput.IsStatLine
            // filters unless --perf-stats. A heading alone would not cover this anyway - the
            // step is O(records) and the silence is INSIDE it.
            int restoreIdx = 0;
            // ONE index and ONE staging buffer for the whole loop, cleared per file rather than
            // reallocated. At cohort scale both back onto arrays far past the 85 KB Large Object
            // Heap threshold - a 257-file CHS run stages ~533 K records per file - and the LOH is
            // swept only on a gen2 collection, so a fresh pair per file left roughly 125 MB of
            // dead buffers standing each time. Over 257 files that accumulated +24 GB and WAS the
            // global memory peak of the run (65.2 GB managed), dwarfing the pass-2 work it feeds.
            // Clear() keeps the capacity, so the steady state is one file's worth of buffer
            // instead of the whole cohort's, and the loop stops scaling with file count.
            // Sized ONCE from the cohort's largest file, not left to grow. The per-file
            // versions this replaced passed an exact capacity, and hoisting without one would
            // have traded the per-file churn for a resize walk on the first file and on every
            // new high-water file after it - 16 rehashes and ~17 MB of abandoned arrays for a
            // ~533 K-entry file, most of it over the 85 KB LOH line. Dictionary.EnsureCapacity
            // is net8.0-only and this builds net472 too, so the capacity goes in the
            // constructor. The scan is O(files), not O(entries).
            int maxEntries = 0;
            foreach (var kvp in perFileEntries)
            {
                if (kvp.Value.Count > maxEntries)
                    maxEntries = kvp.Value.Count;
            }
            var seeder = new Pass1ScalarSeeder(maxEntries,
                LoadExperimentRecords(ctx.Config, FdrScoresSidecar.Pass.FirstPass));
            using (var progress = new ProgressReporter(
                       string.Format(@"Seeding pass-1 scalars from {0} file(s)", perFileEntries.Count),
                       perFileEntries.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                foreach (var kvp in perFileEntries)
                {
                    progress.Report(++restoreIdx);
                    seeder.Seed(kvp.Key, kvp.Value, writer.InputFor(kvp.Key));
                }
            }
            seeder.LogSummary(ctx);
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
            return LoadExperimentRecordsFrom(
                FdrExperimentSidecar.PathFor(config?.OutputBlib,
                    ScoringTaskShared.ArtifactSiblingPath(config), pass), pass);
        }

        /// <summary>
        /// Which per-run 2nd-pass sidecars the RESCORE WORKER owns, decided from what is on disk
        /// (#4486).
        ///
        /// <para>A file qualifies when its <c>.2nd-pass.fdr_scores.bin</c> carries a VALID
        /// <c>PerFileRescoring</c> validity stamp - the producer's own task name and current key.
        /// Existence alone is not enough: an earlier run leaves the same file behind, and a
        /// stale one must not be folded as though this run had computed it.</para>
        ///
        /// <para>Deliberately not a published byproduct. Stage 6 and Stage 7 are separate
        /// PROCESSES in an HPC chain, so anything published in one is simply absent in the other
        /// - and the failure is silent and asymmetric: the in-process route folds the worker's
        /// answer while the distributed route quietly recomputes and rewrites it. Route
        /// dependence in an artifact that is supposed to be route-independent is the exact class
        /// of defect mode 3 exists to catch, and it caught this one.</para>
        /// </summary>
        private static HashSet<string> WorkerOwnedPass2Sidecars(PipelineContext ctx)
        {
            var owned = new HashSet<string>(StringComparer.Ordinal);
            if (ctx.Config?.InputFiles == null)
                return owned;
            // PRESENCE of the producer's stamp, not a recomputation of the producer's KEY.
            //
            // The stamp is named <output>.<taskName>.osprey.task, so the filename already says
            // who wrote the binary - which is the whole point of stamping it. Recomputing
            // PerFileRescoring's key from THIS process was wrong and failed exactly where it
            // mattered: PerFileRescoreTask.ValidityKey folds in
            // LibraryFragmentRelease.ValidityKeySuffix, which asks RunsOnThisLeg(ctx) ->
            // ctx.Config.ExpectReconciledInput - a PER-LEG flag. A --task SecondPassFDR process
            // and a --task PerFileRescoring process therefore compute different keys for the
            // same task, so in an HPC chain IsValid said "not valid", Stage 7 concluded no
            // worker had run, and it recomputed and rewrote every sidecar. One task cannot
            // reconstruct another task's key from a different leg, and should not try.
            //
            // Staleness is still covered, by the task that owns it rather than by this check: if
            // the worker's inputs changed, PerFileRescoring's OWN validity fails, the driver
            // re-runs it, and it rewrites both the binary and this stamp. A stamp that survives
            // is one whose producer was legitimately skipped as already-done.
            foreach (string inputFile in ctx.Config.InputFiles)
            {
                string pass2Path = FdrScoresSidecar.Pass2Path(inputFile);
                if (File.Exists(pass2Path) &&
                    File.Exists(TaskValiditySidecar.PathFor(pass2Path, PerFileRescoreTask.TASK_NAME)))
                {
                    owned.Add(Path.GetFileNameWithoutExtension(inputFile));
                }
            }
            return owned;
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
                FdrScoresSidecar.Write(dumpPath, entries, FdrScoresSidecar.Pass.SecondPass);
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
        /// <para>Returns null when this file has no worker answer, which leaves Stage 7 folding
        /// its own recomputation exactly as it always has. That is the ONLY tolerated absence,
        /// and it is decided by an explicit published set rather than by probing for a file: a
        /// resumed run finds standing 2nd-pass sidecars from an EARLIER run, and folding one of
        /// those - or comparing against it - would be reading a previous run's answer as this
        /// one's. Once the worker says it wrote a file, every failure below is a throw.</para>
        /// </summary>
        private static StreamingFdr.FileCompetition TryReadWorkerContribution(
            Pass2SidecarWriter writer, string fileKey,
            HashSet<uint> stratumBaseIds, HashSet<string> workerOwned)
        {
            // The owned set is computed ONCE per run and passed in: deciding it here would
            // re-stat every input file's validity sidecar for every file streamed, which is
            // O(files^2) on the artifact class this move exists to stop re-reading.
            if (workerOwned == null || !workerOwned.Contains(fileKey))
                return null;
            string inputFile = writer.InputFor(fileKey);
            if (inputFile == null)
            {
                throw new InvalidOperationException(string.Format(
                    @"Second-pass worker verification for '{0}': the worker reported writing a " +
                    @"sidecar for this file, but it matches no configured input file. See issue #4486.",
                    fileKey));
            }
            string pass2Path = FdrScoresSidecar.Pass2Path(inputFile);
            var records = new List<FdrScoreRecord>();
            if (!FdrScoresSidecar.ReadRecords(
                    pass2Path, FdrScoresSidecar.Pass.SecondPass, rec => records.Add(rec)))
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
            return FileCompetitionFromRecords(
                records, stratumBaseIds, bestDecoy, LoadGapFillEntryIds(inputFile));
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
                throw new InvalidOperationException(string.Format(
                    @"The {0} experiment-scope FDR sidecar exists but could not be read: {1}. " +
                    @"Treating it as empty would leave every entry on its reset defaults - an " +
                    @"experiment aggregate score of 0 and an experiment q of 1 - and the run " +
                    @"would then report those as computed values. See issue #4486.",
                    pass, path));
            }
            return map;
        }

        internal sealed class Pass1ScalarSeeder
        {
            private readonly List<string> _unreadable = new List<string>();

            /// <summary>
            /// The analysis-wide 1st-pass EXPERIMENT-scope records (format v5, issue #4486).
            /// <c>ExperimentAggregateScore</c> is one of the three scalars this seeder restores,
            /// and it no longer travels in the per-file record, so the seeder has to hold the
            /// one file that does. Null when the analysis has no experiment sidecar, which
            /// leaves the aggregate at its reset default.
            /// </summary>
            private readonly IReadOnlyDictionary<uint, FdrExperimentRecord> _experimentRecords;

            private Dictionary<uint, FdrEntry> _byEntryId;
            private List<KeyValuePair<FdrEntry, FdrScoreRecord>> _staged;
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
            /// Seed one file's entries. A file with no input path is skipped silently - the
            /// caller has already reported it as unmatched - and one with no readable 1st-pass
            /// sidecar is recorded for the summary and left at its reset defaults.
            /// </summary>
            public void Seed(string fileName, IReadOnlyList<FdrEntry> entries, string inputFile)
            {
                if (inputFile == null)
                    return;
                string pass1Path = FdrScoresSidecar.Pass1Path(inputFile);
                if (!File.Exists(pass1Path))
                {
                    _unreadable.Add(fileName);
                    return;
                }
                if (entries.Count > _capacity)
                    Resize(entries.Count);
                _byEntryId.Clear();
                foreach (var e in entries)
                    _byEntryId[e.EntryId] = e;

                // Stage into a buffer and apply only on a clean read. ReadRecords documents
                // that a false return can arrive AFTER it has invoked the callback ("with the
                // partial callback effects the caller must discard"), and records stream in
                // file order, so mutating in the callback would leave the entries before the
                // fault carrying pass-1 values and the rest at reset defaults - a half-seeded
                // pool that no warning could describe and nothing downstream could detect.
                // Cleared, not reallocated: the discard contract only requires that nothing
                // staged before a fault is APPLIED, which Clear() ahead of each file gives.
                // Allocated on first use, not in Resize: the streamed frozen path constructs
                // this seeder purely to call Apply, which never stages, so sizing the buffer
                // alongside the entry map handed it a dead ~40 MB LOH list per high-water file
                // - a self-inflicted allocation in a change whose purpose is the Stage 7 peak.
                if (_staged == null)
                    _staged = new List<KeyValuePair<FdrEntry, FdrScoreRecord>>(_capacity);
                _staged.Clear();
                bool ok = FdrScoresSidecar.ReadRecords(
                    pass1Path, FdrScoresSidecar.Pass.FirstPass,
                    rec =>
                    {
                        if (_byEntryId.TryGetValue(rec.EntryId, out FdrEntry entry))
                            _staged.Add(new KeyValuePair<FdrEntry, FdrScoreRecord>(entry, rec));
                    });
                if (!ok)
                {
                    _unreadable.Add(fileName);
                    return;
                }
                foreach (var pair in _staged)
                    ApplyRecord(pair.Key, pair.Value);
                _filesRead++;
                _nRestored += _staged.Count;
            }

            /// <summary>
            /// Seed from records the caller has ALREADY read, rather than reading the sidecar
            /// again. The frozen competition reads each file's 1st-pass sidecar for its own
            /// reasons and hands the survivor records here, so one traversal serves both
            /// (#4486); <see cref="Seed"/> stays the form for callers that only want the seed.
            ///
            /// <para>No <c>_unreadable</c> case: a caller holding decoded records has already
            /// had a clean read, which is also why this needs none of <see cref="Seed"/>'s
            /// staging - there is no partial-callback state to discard.</para>
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
            /// Report what was seeded and what could not be.
            ///
            /// <para>The warning is reported, not thrown on - but NOT because the consequence is
            /// cosmetic. Score feeds the Stage 8 picked-protein FDR that runs a few statements
            /// after the second pass returns (SecondPassFdrTask RunProteinFdr -&gt;
            /// ProteinFdrEngine.RunSecondPass -&gt; ProteinFdr.CollectBestPeptideScores takes
            /// max(entry.Score)), and that decoy side is not q-gated, so an unseeded 0.0 competes
            /// in the null. That is the very mechanism this seed exists to remove.</para>
            ///
            /// <para>It stays a warning because the modes divide cleanly: a frozen mode genuinely
            /// needs the sidecar and already fail-fasts on it, while the retrain path rescores
            /// every entry and overwrites the seed, so a missing sidecar there is harmless.
            /// Escalating here would break the harmless case to guard one that is already
            /// guarded. The warning therefore has to state the real consequence rather than imply
            /// there is none.</para>
            /// </summary>
            /// <summary>
            /// Files whose 1st-pass sidecar could not be read, for a caller that owns SEVERAL
            /// seeders and has to report them as one run-wide set. Exposed rather than logged
            /// per instance because the split across instances is a scheduling artifact: the
            /// rescore worker keeps one seeder per THREAD, so which seeder holds which file
            /// name depends on which thread happened to take that file. Reporting per instance
            /// would make the warning text vary run to run for identical inputs.
            /// </summary>
            public IReadOnlyList<string> Unreadable
            {
                get { return _unreadable; }
            }

            /// <summary>Survivors this instance restored, for the same aggregation.</summary>
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
                if (_unreadable.Count > 0)
                {
                    ctx.LogWarning(string.Format(
                        "1st-pass Score/Pep/ExperimentAggregateScore could not be " +
                        "restored for {0} file(s) (no readable 1st-pass sidecar): [{1}]. Peaks Stage 6 " +
                        "changed in those files keep reset defaults, so their 2nd-pass sidecars are " +
                        "wrong AND a Score of 0 enters the second-pass protein FDR null unfiltered. " +
                        "Treat this run's protein-level numbers as unreliable.",
                        _unreadable.Count, string.Join(", ", _unreadable)));
                }
                ctx.LogVerbose(string.Format(
                    "Restored 1st-pass Score/Pep/ExperimentAggregateScore onto {0} survivor(s) across {1} file(s).",
                    _nRestored, _filesRead));
            }

            /// <summary>
            /// Copy the three scalars <c>ResetScores</c> clears that no frozen 2nd-pass mode
            /// writes back, from one 1st-pass record onto its entry.
            ///
            /// <para>ExperimentProteinQvalue is deliberately NOT seeded - see the remarks on
            /// <see cref="Seed"/>. The second-pass protein FDR writes the second-pass value onto
            /// the entry, and the second-pass experiment sidecar records it (#4559). The other
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

            private void Resize(int capacity)
            {
                _capacity = capacity;
                _byEntryId = new Dictionary<uint, FdrEntry>(capacity);
                // _staged is deliberately NOT sized here - see Seed.
                _staged = null;
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
            IReadOnlyDictionary<string, double> peptideQvalues)
        {
            var inputByName = new Dictionary<string, string>();
            foreach (var inputFile in ctx.Config.InputFiles)
                inputByName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;

            // The three EXPERIMENT-scope columns the second pass already computed, handed over
            // by the pass that computed them. Absent only when no pass-2 path ran, in which case
            // there is nothing to write.
            if (!ctx.TryGet<Pass2ExperimentScope>(out var scope))
            {
                ctx.LogVerbose(
                    "No second-pass experiment-scope records were published, so no 2nd-pass " +
                    "experiment FDR sidecar is written.");
                return;
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
                       string.Format(@"Patching pass-2 protein q into {0} sidecar(s)", fileNames.Count),
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
                        string parquetPath =
                            ParquetScoreCache.EffectiveScoresPathFromScoresPath(
                                perFileParquetPaths[fileName]);
                        ParquetScoreCache.ReadFdrStubScalars(parquetPath,
                            (entryId, charge, isDecoy, coelutionSum, modseq) =>
                            {
                                double q;
                                if (!peptideQvalues.TryGetValue(modseq ?? string.Empty, out q))
                                    q = 1.0;
                                byEntryId[entryId] = q;
                            });
                    }
                    catch (Exception ex)
                    {
                        ctx.LogWarning(string.Format(
                            @"Could not read the reconciled parquet scalars for {0}: {1}",
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
                ctx.LogWarning(string.Format(
                    "Could not patch the second-pass protein q-value into the 2nd-pass FDR " +
                    "sidecar for {0} file(s): [{1}]. Those files carry no pass-2 protein " +
                    "q-value: each record keeps what it held when the sidecar was written, " +
                    "which is the reset default for every entry Stage 6 rescored or gap-filled " +
                    "and a pass-1 value only for entries Stage 6 left alone. Any consumer " +
                    "joining on that column is reading the wrong pass.",
                    failed.Count, string.Join(", ", failed)));
            }
            ctx.LogVerbose(string.Format(
                "Resolved the second-pass protein q-value for {0} record(s) across {1} file(s).",
                nPatched, filesPatched));

            // The experiment-scope record set is complete now that protein FDR has filled the
            // one column it owns, so write it once beside the blib.
            string experimentPath = FdrExperimentSidecar.PathFor(ctx.Config?.OutputBlib,
                ScoringTaskShared.ArtifactSiblingPath(ctx.Config), FdrScoresSidecar.Pass.SecondPass);
            if (string.IsNullOrEmpty(experimentPath))
            {
                ctx.LogWarning(
                    "No output blib to name the 2nd-pass experiment-scope FDR sidecar after, so " +
                    "this run's experiment q-values are not persisted.");
                return;
            }
            try
            {
                FdrExperimentSidecar.Write(experimentPath, experiment.Records,
                    FdrScoresSidecar.Pass.SecondPass);
                ctx.LogInfo(string.Format(
                    @"Wrote experiment-scope FDR sidecar: {0} ({1} distinct entry ids)",
                    experimentPath, experiment.Count));
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(
                    "Failed to write {0}: {1}", experimentPath, ex.Message));
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
        /// Run the frozen-model COMPETITION second pass (transfer-compete / protein-compact):
        /// resolve the frozen 1st-pass model and, for protein-compact, its stratum, then hand
        /// them to <see cref="ComputePass2TransferCompeteFull"/>. Returns true when it ran and
        /// wrote every file's 2nd-pass sidecar.
        ///
        /// <para>Fail-fast, because an explicitly requested frozen mode must NEVER silently
        /// degrade to the anti-conservative retrain. Absent inputs - the frozen 1st-pass model
        /// or protein stratum are not in this process (a warm rerun that loaded cached scores
        /// and skipped 1st-pass training, or a distributed SecondPassFDR node that never trained
        /// pass 1), or a missing / corrupt 1st-pass sidecar - mean the mode cannot be honored,
        /// so this aborts with actionable guidance rather than reporting looser FDR than a cold
        /// straight-through run under the same mode. (protein-compact +
        /// OSPREY_PROTEIN_COMPACT_RETRAIN=1 retrains by design and never reaches here.)</para>
        /// </summary>
        private static bool ComputePass2FrozenCompetition(
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
                return true;
            }
            throw new InvalidOperationException(string.Format(
                "OSPREY_PASS2_QVALUE={0} could not run the frozen recompute (the frozen 1st-pass " +
                "model, 1st-pass scalar sidecars or protein stratum are absent, or a file's input " +
                "path could not be resolved - e.g. a warm " +
                "rerun or a distributed SecondPassFDR node that did not train pass 1 in-process). " +
                "The warning above names which. Run the " +
                "frozen modes on the straight-through path, rerun without the score cache, or unset " +
                "OSPREY_PASS2_QVALUE for the default retrain{1}.",
                OspreyEnvironment.Pass2QValue,
                OspreyEnvironment.Pass2ProteinCompact
                    ? ", or set OSPREY_PROTEIN_COMPACT_RETRAIN=1 to retrain over the stratum"
                    : string.Empty));
        }

        /// <summary>
        /// OSPREY_PASS2_QVALUE=transfer-compete (full-population form). Recompute the reported
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
            HashSet<uint> stratumBaseIds = null)
        {
            // stratumBaseIds == null -> transfer-compete (full-population competition).
            // non-null -> protein-compact: the competition is CONSTRAINED to the stratum
            // (peptides of >=2-peptide 1st-pass proteins), and the map-back below leaves
            // OFF-stratum survivors on their 1st-pass q (report = pass1 U stratum passers,
            // so re-scoping only adds, never drops an already-passing peptide).
            bool proteinCompact = stratumBaseIds != null;
            string mode = proteinCompact ? "protein-compact" : "transfer-compete";
            // Works for whichever classifier the 1st pass trained (linear SVM or
            // gradient-boosted trees) -- the scorer hides that choice, so transfer-compete
            // stays the honest-FDR path under --fdr-method gbdt too.
            var scorer = FrozenModelScorer.TryCreate(frozenModel);
            if (scorer == null)
            {
                ctx.LogWarning("transfer-compete: frozen 1st-pass model has no usable model/standardizer.");
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
            long survivorObservations = 0;
            // The resident survivor lists by file. It holds one REFERENCE per file, not a
            // copy, so it costs nothing beyond the whole-run buffer Stage 7 has already
            // built by the time this pass runs; the lean-row work is what retires that
            // buffer and hands this pass a per-file source instead.
            var residentByFile =
                new Dictionary<string, List<FdrEntry>>(fileNames.Count, StringComparer.Ordinal);
            // Reported because this walks EVERY survivor observation - 89,068,375 of them on the
            // 82-file SEA-AD run - into a HashSet before anything downstream logs a word. It sat
            // inside a 195 s silence between "Released library fragments" and the
            // OSPREY_PASS2_QVALUE banner, which reads as a hung run at the very end of a
            // multi-hour search. The two steps after it (sidecar path validation and the protein
            // stratum build) are in the same silence and are NOT yet reported - see the TODO.
            using (var mergeProgress = new ProgressReporter(
                string.Format(@"Collecting pass-2 survivors from {0} file(s)", fileNames.Count),
                fileNames.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                int mergeIdx = 0;
                foreach (var kvp in rescored.Files())
                {
                    mergeProgress.Report(++mergeIdx);
                    residentByFile[kvp.Key] = kvp.Value;
                    survivorObservations += kvp.Value.Count;
                    foreach (var e in kvp.Value)
                        survivorEntryIds.Add(e.EntryId);
                }
            }

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

            var sidecarByKey = new Dictionary<string, string>(fileNames.Count, StringComparer.Ordinal);
            foreach (string fileName in fileNames)
            {
                if (!perFileParquetPaths.TryGetValue(fileName, out string parquetPath))
                {
                    ctx.LogWarning("transfer-compete: no parquet path for '" + fileName +
                                   "'; cannot locate its 1st-pass scalar sidecar.");
                    return false;
                }
                string sidecarPath = Path.Combine(
                    Path.GetDirectoryName(parquetPath) ?? string.Empty,
                    fileName + ".1st-pass.fdr_scores.bin");
                if (!File.Exists(sidecarPath))
                {
                    ctx.LogWarning("transfer-compete: 1st-pass scalar sidecar not found: " + sidecarPath);
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
                    ctx.LogWarning(
                        "transfer-compete: 1st-pass scalar sidecar is not a readable v" +
                        FdrScoresSidecar.FormatVersion + " first-pass file: " + sidecarPath);
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
                    ctx.LogWarning(mode + ": no input file matches '" + fileName +
                                   "'; its 2nd-pass FDR sidecar cannot be written.");
                    return false;
                }
                fileKeys.Add(fileName);
                sidecarByKey[fileName] = sidecarPath;
            }

            ctx.LogInfo(string.Format(
                "OSPREY_PASS2_QVALUE={0}: recomputing q/PEP by streaming {1} file(s), frozen-model " +
                "scores swapped in for up to {2} reconciled survivor observations - no retrain, one " +
                "file resident at a time{3}.",
                mode, fileKeys.Count, survivorObservations,
                proteinCompact ? ", competition CONSTRAINED to the " + stratumBaseIds.Count + "-base_id protein stratum"
                               : ", full-population null"));

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
                    "OSPREY_PASS2_QVALUE={0} cannot be combined with a 1st pass run under " +
                    "OSPREY_EXPERIMENT_AGG={1}{2}. This mode recomputes the reported experiment q " +
                    "from a MAX-aggregated competition, which {3}. Use OSPREY_PASS2_QVALUE={4}, " +
                    "which carries the 1st-pass mean(best-N) q through unchanged, for a " +
                    "mean(best-N) arm.",
                    proteinCompact
                        ? OspreyEnvironment.PASS2_QVALUE_PROTEIN_COMPACT
                        : OspreyEnvironment.PASS2_QVALUE_TRANSFER_COMPETE,
                    pass1Arm,
                    armRecorded
                        ? " (recorded in the 1st-pass model sidecar)"
                        : " (INFERRED from this process's environment - the 1st-pass model sidecar " +
                          "predates arm recording and does not say which arm trained it)",
                    proteinCompact
                        ? "would leave on-stratum precursors max-aggregated and off-stratum " +
                          "precursors on their 1st-pass mean(best-N) q - one column, two statistics"
                        : "would replace every precursor's mean(best-N) q with a max q, making the " +
                          "run indistinguishable from a default run in its own output",
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
            // Files whose sidecar this pass wrote, i.e. every file it was given. Kept as a
            // list rather than re-deriving it, because step 4 must patch exactly what step 3
            // wrote: a file that failed its write has no finished sidecar to patch, and that is
            // a failure to report, not a file to skip.
            var sidecarsWritten = new List<string>(fileKeys.Count);
            var writeFailures = new List<string>();
            // The files the rescore worker already wrote a per-run 2nd-pass sidecar for, so this
            // pass does not rewrite them (#4486). Null when no worker ran, which is the pre-move
            // behaviour.
            var workerWroteFiles = WorkerOwnedPass2Sidecars(ctx);
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
                string.Format("{0}: streaming the competition over {1} file(s)", mode, fileKeys.Count),
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
                (uint[] entryIds, double[] scores, IReadOnlyDictionary<uint, double> survivorScores,
                    HashSet<uint> ownSurvivorIds)
                    ReadFile(string fileKey)
                {
                    // The parquet lookup is established by the validation loop above (every file
                    // has a parquet path or this method already returned false), and resolved
                    // HERE so a key miss cannot be reported as a parquet failure by the reader.
                    string effectiveParquetPath = ParquetScoreCache.EffectiveScoresPathFromScoresPath(
                        perFileParquetPaths[fileKey]);
                    currentKey = fileKey;
                    currentEntries = LoadOneFile(fileKey);
                    // Read from the path the validation loop above checked with IsCurrentFormat,
                    // not from writer.InputFor's. Both resolve through
                    // ArtifactPaths.ResolveOutputDir, so they name the same file in every
                    // configuration this method accepts; this is the one whose header was
                    // verified BEFORE any survivor was mutated, which is where the contract
                    // above puts the refusal.
                    ReadOneFilePass2Inputs(
                        sidecarByKey[fileKey], effectiveParquetPath, currentEntries,
                        scorer, nFeatures, seeder, ctx.LogWarning, mode,
                        survivorIds, pass1Records,
                        out uint[] eids, out double[] scs, out var fileScores);
                    nScored += fileScores.Count;

                    progress.Report(++nRead);
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
                    // A --task ModelDiagnostics run declines every sidecar write by
                    // contract (WriteCore's DiagnosticsOnly skip): that is not a
                    // failure, and counting it as one made the unpatched check below
                    // throw after the whole competition had already produced the
                    // in-memory values the report needs.
                    // A file the rescore worker already wrote is NOT rewritten here (#4486).
                    // Pipeline artifacts are immutable once written: presence is the indicator,
                    // so nobody has to open a file to learn who produced it. Rewriting would
                    // also be destructive rather than merely redundant - this write serializes
                    // the resident survivors, while the worker's file additionally carries the
                    // carried-forward decoy observations the experiment fold needs, so the
                    // rewrite would silently delete exactly the rows bestDecoy is recovered
                    // from. Counted as written because it IS written; step 4 must patch it.
                    if (workerWroteFiles != null && workerWroteFiles.Contains(fileKey))
                        sidecarsWritten.Add(fileKey);
                    else if (writer.Write(fileKey, currentEntries))
                        sidecarsWritten.Add(fileKey);
                    else if (!ctx.Config.DiagnosticsOnly)
                        writeFailures.Add(fileKey);
                    currentKey = null;
                    currentEntries = null;
                }

                // Say which way this run folded, ALWAYS - the two paths read different artifacts
                // and cost different amounts, and a silent flag is indistinguishable from a flag
                // that stopped reaching the child process. This line is what regression.ps1
                // asserts on to prove its straight leg and its HPC chain really did run the
                // verified and shipped paths respectively, rather than both running whichever
                // one the environment happened to supply.
                if (OspreyEnvironment.Pass2VerifyWorker)
                {
                    ctx.LogInfo(string.Format(
                        @"Second-pass worker verification ACTIVE (OSPREY_PASS2_VERIFY_WORKER): " +
                        @"recomputing the per-file competition for {0} file(s) to assert the " +
                        @"worker's answer. This re-reads each 1st-pass sidecar; it is a test " +
                        @"instrument and is off by default.", fileKeys.Count));
                }
                else
                {
                    ctx.LogInfo(string.Format(
                        @"Second-pass fold reading the worker's written answer for {0} file(s); " +
                        @"no 1st-pass sidecar is opened (OSPREY_PASS2_VERIFY_WORKER off).",
                        fileKeys.Count));
                }

                try
                {
                    competition = StreamingFdr.ComputeFullPopulationPrecursorFdrStreaming(
                        fileKeys, ReadFile, survivorEntryIds, ApplyFileRunQ, stratumBaseIds,
                        // The rescore worker's answer, when it produced one, is what gets folded;
                        // the streaming pass recomputes only to assert against it (#4486).
                        fileKey => TryReadWorkerContribution(
                            writer, fileKey, stratumBaseIds, workerWroteFiles),
                        // Off by default: the recompute is a TEST INSTRUMENT and costs exactly
                        // the re-reads this phase exists to remove. regression.ps1 turns it on.
                        OspreyEnvironment.Pass2VerifyWorker);
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
            //    time. Run q and Score were written as the stream advanced, so what is left is
            //    experiment q, PEP and the experiment aggregate - all derived per entry from
            //    O(distinct) maps rather than read out of a whole-run (file, entry_id)-keyed
            //    result dictionary.
            //
            //    Over the SIDECARS, not the entries: each file's records carry the entry_id and
            //    run q this needs, and the file's entries have been dropped by now. That is the
            //    point - a pass over the pool here would put every file back in memory at once
            //    and undo the whole per-file cycle above (#4486).
            //
            //    Three of the four columns are EXPERIMENT-scope and now collapse into the one
            //    analysis-wide record per entry_id that ctx carries to the protein-FDR step,
            //    which writes the 2nd-pass experiment sidecar once. Only PEP is written back
            //    into the per-file file, because it is real on a single observation per base_id
            //    and an entry_id-keyed record cannot say which - see FdrScoresSidecar.PatchPep.
            int nMapped = 0;
            var unpatched = new List<string>(writeFailures);
            var experiment = new FdrExperimentAccumulator();
            using (var patchProgress = new ProgressReporter(
                string.Format("{0}: writing experiment q to {1} file(s)", mode, sidecarsWritten.Count),
                sidecarsWritten.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                int patchIdx = 0;
                foreach (string fileKey in sidecarsWritten)
                {
                    patchProgress.Report(++patchIdx);
                    string inputFile = writer.InputFor(fileKey);
                    if (inputFile == null)
                        continue;
                    string pass2Path = FdrScoresSidecar.Pass2Path(inputFile);
                    // READ-ONLY over the per-run sidecars. This loop used to finish by rewriting
                    // each one's pep column (PatchPep), which is what made a per-file sidecar
                    // mutable and required this stage to hold write access to every run's output.
                    // PEP is now stored once, as a winner fact on the experiment record below.
                    //
                    // Staged, then applied: ReadRecords can return false AFTER invoking the
                    // callback, so accumulating experiment values as they arrive would leave the
                    // analysis-wide record half-built from a file that then failed.
                    var staged = new List<FdrExperimentRecord>();
                    if (!FdrScoresSidecar.ReadRecords(pass2Path, FdrScoresSidecar.Pass.SecondPass,
                            rec => staged.Add(FinishRecord(rec))))
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
            // Handed to the protein-FDR step, which fills the one column it owns and writes the
            // 2nd-pass experiment sidecar. Published rather than returned because the protein
            // FDR runs in the owning task after this method returns.
            ctx.Publish(new Pass2ExperimentScope(experiment));
            if (unpatched.Count > 0)
            {
                // Hard, not a warning. Every column but these four is already final in those
                // files, so the header says second-pass while the experiment q, PEP and
                // aggregate are whatever the per-file write happened to carry - a q-value a
                // consumer would reasonably trust and could not audit. The protein FDR that runs
                // next gates on ExperimentPrecursorQvalue, so continuing means reporting a
                // protein set computed from unfinished numbers.
                throw new IOException(string.Format(
                    "{0}: could not write the recomputed experiment q-values into the 2nd-pass FDR " +
                    "sidecar for {1} file(s): [{2}]. Those files' experiment q, PEP and experiment " +
                    "aggregate are not second-pass values, and the protein FDR that reads them runs next.",
                    mode, unpatched.Count, string.Join(", ", unpatched)));
            }
            ctx.LogInfo(string.Format(
                "{0}: mapped recomputed q onto {1} reported survivors ({2} frozen-model scores " +
                "swapped in) in {3:F1}s.",
                mode, nMapped, nScored, sw.Elapsed.TotalSeconds));
            return true;

            // The experiment-scope record for one observation plus its PEP, from the bounded
            // competition state. Split out of the loop above only so the two dispositions -
            // on-stratum recompute and off-stratum carry-through - read side by side.
            FdrExperimentRecord FinishRecord(FdrScoreRecord rec)
            {
                // stratumBaseIds != null IS proteinCompact - written as the null test the branch
                // actually depends on, so the guard is local to the dereference it protects.
                if (stratumBaseIds != null && !stratumBaseIds.Contains(rec.EntryId & 0x7FFFFFFFu))
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
        /// Resident 2nd-pass compute (flag off): the byte-identity oracle. Reload every
        /// survivor's 21-PIN feature vector RESIDENT from each file's reconciled parquet
        /// (keyed by identity via <see cref="LoadReconciledFeaturesByScoreIndex"/> +
        /// <see cref="MapFeaturesByScoreIndex"/>), then run the resident FdrEntry
        /// <c>FirstPassFdrTask.RunPercolatorFdr</c> over the full survivor buffer, which
        /// scores it in place.
        ///
        /// <para>Reached by the RETRAIN modes and by <c>transfer</c>, which needs each survivor's
        /// reconciled features on <c>entry.Features</c>. The frozen COMPETITION modes used to
        /// enter here and return early; they now have their own entry point
        /// (<see cref="ComputePass2FrozenCompetition"/>), because nothing about them is resident
        /// any more - they never see this buffer (#4486).</para>
        /// </summary>
        private static FeatureContributions ComputePass2Resident(
            PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config)
        {
            // Reload PIN features from the reconciled parquets.
            // PerFileScoringTask's bundle-hydration path
            // explicitly nulls Features after stub load (see
            // PerFileScoringTask.cs ~line 710) to keep
            // PerFileRescoreTask.WriteReconciledParquet's
            // "Features != null means this entry was rescored"
            // criterion. That assumption was safe when Stage 7
            // didn't run Percolator -- with the Bug C 2nd-pass
            // wired in below, we now need the 21-PIN features
            // for SVM training, so pull them back from the
            // post-Stage-6 reconciled parquet. The features
            // there are the rescored values that Stage 6 wrote
            // back, so they are the correct input for 2nd-pass
            // Percolator. Mirrors Rust pipeline.rs:4209-4218
            // (run_search loads PIN features from parquet
            // before second-pass FDR via the cache path).
            var swReloadFeats = Stopwatch.StartNew();
            int nReloaded = 0;
            // Per-file progress: reloading each file's reconciled PIN features from parquet
            // ran ~10 min silent before 2nd-pass Percolator. Console-only.
            var reloadProgress = new ProgressReporter(
                string.Format(@"Reloading reconciled features from {0} file(s)", perFileEntries.Count),
                perFileEntries.Count);
            int reloadIdx = 0;
            foreach (var kvp in perFileEntries)
            {
                reloadProgress.Report(++reloadIdx);
                if (!perFileParquetPaths.TryGetValue(kvp.Key, out string parquetPath))
                {
                    // No scores parquet was produced (or mapped) for this
                    // file. The {0} entries below will go into the second-pass
                    // Percolator with stale / null Features, which silently
                    // regresses 2nd-pass FDR -- log so the operator can detect
                    // an incomplete hand-off.
                    ctx.LogWarning(string.Format(
                        "Second-pass FDR: no parquet path mapped for file '{0}' " +
                        "({1} entries will run with stale/null features). " +
                        "Check that each file's scores parquet was produced and mapped.",
                        kvp.Key, kvp.Value.Count));
                    continue;
                }
                // Read the RECONCILED parquet (Stage 6's rescored
                // features) when it exists; fall back to the original
                // Stage 4 parquet for files that had no reconciliation
                // work (no reconciled sibling was written). The
                // perFileParquetPaths map holds original paths.
                string effectiveParquetPath =
                    ParquetScoreCache.EffectiveScoresPathFromScoresPath(parquetPath);
                Dictionary<uint, double[]> featByScoreIndex;
                try
                {
                    featByScoreIndex = LoadReconciledFeaturesByScoreIndex(effectiveParquetPath);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        "Second-pass FDR: failed to reload PIN features from {0}: {1}",
                        effectiveParquetPath, ex.Message));
                    continue;
                }
                int nMapped = MapFeaturesByScoreIndex(kvp.Value, featByScoreIndex);
                // An entry whose identity is absent from the reconciled
                // parquet is a stub/parquet mismatch (e.g., the FirstPassFDR
                // parquet was regenerated with fewer rows than the in-memory
                // FDR stubs reference). Such entries silently keep their stale
                // Features and corrupt 2nd-pass FDR; warn so the mismatch
                // is visible.
                if (nMapped < kvp.Value.Count)
                {
                    ctx.LogWarning(string.Format(
                        "Second-pass FDR: file '{0}' reconciled parquet has {1} feature rows " +
                        "but {2} FDR entries reference it; {3} entries will run with " +
                        "stale/null features. Stub/parquet mismatch - check reconciled-parquet " +
                        "output integrity.",
                        kvp.Key, featByScoreIndex.Count, kvp.Value.Count, kvp.Value.Count - nMapped));
                }
                nReloaded += nMapped;
            }
            reloadProgress.Dispose();
            swReloadFeats.Stop();
            ctx.LogInfo(string.Format(
                "[TIMING] Reloaded PIN features for {0} entries: {1:F1}s",
                nReloaded, swReloadFeats.Elapsed.TotalSeconds));

            switch (config.FdrMethod)
            {
                // Gbdt shares this path with Percolator: the 2nd pass is the same
                // sequence (transfer-compete's frozen-model recompute, or a retrain)
                // regardless of which classifier the 1st pass trained. The frozen model
                // carried in ctx is whichever one that was, and the score passes select
                // on it, so transfer-compete works unchanged for trees.
                case FdrMethod.Percolator:
                case FdrMethod.Gbdt:
                    // OSPREY_PASS2_QVALUE=transfer-compete / protein-compact (frozen) are handled
                    // at the TOP of ComputePass2Resident (before the resident feature reload) so
                    // their frozen score pass streams one file at a time -- see
                    // ComputePass2TransferCompeteFull. Only the retrain A/B toggle and
                    // OSPREY_PASS2_QVALUE=transfer reach here.
                    if (OspreyEnvironment.Pass2ProteinCompact && OspreyEnvironment.Pass2ProteinCompactRetrain)
                    {
                        ctx.LogInfo(
                            "OSPREY_PROTEIN_COMPACT_RETRAIN=1: skipping the frozen-model + stratum " +
                            "competition; RETRAINING the 2nd-pass over the stratum-expanded compacted pool " +
                            "(frozen-vs-retrain FDR A/B).");
                    }
                    // OSPREY_PASS2_QVALUE=transfer: instead of retraining a 2nd-pass SVM on
                    // the decoy-depleted reconciled+compacted set (which re-derives an
                    // anti-conservative experiment-scope q), carry the pass-1 q through and
                    // recompute ONLY the per-run q of the peaks reconciliation actually moved.
                    // Each moved/gap-filled peak is re-scored with the FROZEN 1st-pass model
                    // (its RECONCILED features are on entry.Features above) and mapped through
                    // THAT file's own (1st-pass score -> run q) table; experiment q is left as
                    // the pass-1 carry. Falls through to the retrain if the flag is off or the
                    // frozen model was not captured. See TODO-osprey_pass2_per_run_only_qvalue.
                    if (OspreyEnvironment.Pass2TransferQ &&
                        ctx.TryGet<FirstPassPercolatorModel>(out var frozenModel) &&
                        frozenModel?.Results != null &&
                        TransferPerRunQ(perFileEntries, config, ctx, frozenModel.Results))
                    {
                        // Transferred: no retrained 2nd-pass model in transfer mode -> no
                        // pass-2 SVM model view for --model-diagnostics (the pass-2 FDR
                        // calibration curve still renders from the transferred q-values;
                        // the pass-1 model view still renders too).
                        return null;
                    }
                    if (OspreyEnvironment.Pass2TransferQ)
                    {
                        ctx.LogWarning(
                            "OSPREY_PASS2_QVALUE=transfer could not transfer (frozen 1st-pass " +
                            "model byproduct absent); falling back to the 2nd-pass Percolator retrain.");
                    }
                    // Capture the 2nd-pass model for the --model-diagnostics pass-2 model
                    // view (retrained on the post-reconciliation pool, #4377). Capturing
                    // the return value does not change what RunPercolatorFdr does, so the
                    // resident 2nd-pass scores stay byte-identical.
                    return FirstPassFdrTask.RunPercolatorFdr(
                        perFileEntries, config, ctx, "Second-pass");
                // Simple / Mokapot 2nd-pass paths intentionally
                // not implemented yet -- the in-process pipeline's
                // FirstPassFdrTask.RunFdr already covers Simple, and
                // Mokapot is not used in Osprey's current
                // scope. If those become relevant for an HPC chain,
                // mirror the Rust dispatch in pipeline.rs:4424-4448.
                default:
                    ctx.LogWarning(string.Format(
                        "Second-pass FDR: {0} is not supported in SecondPassFdrTask; " +
                        "skipping (protein FDR will run on first-pass scores)",
                        config.FdrMethod));
                    return null;
            }
        }

        /// <summary>
        /// Projection 2nd-pass compute (flag on, issue #4374 + #4355 struct-shrink S0):
        /// build the thin <see cref="FdrProjectionSet"/> from the survivor buffer with
        /// each row's <see cref="FdrProjection.ParquetIndex"/> baked to that survivor's
        /// RECONCILED parquet row (via <see cref="BuildReconciledScoreIndexToRow"/>), then
        /// run the projection <c>FirstPassFdrTask.RunPercolatorFdr</c> through an
        /// <see cref="FdrStreamingSink"/>, which ALWAYS streams the reconciled features
        /// per file and streams the q-value outputs straight to the per-file
        /// <c>.2nd-pass.fdr_scores.bin</c> via <paramref name="flushFile"/> (the lean
        /// projection never stores them -> 32 B). <paramref name="resolveProteinQ"/>
        /// supplies each row's <c>ExperimentProteinQvalue</c> (looked up from the resident
        /// survivor by entry_id, no longer carried on the struct). Returns the scored
        /// projection as the flag that the sink wrote the sidecars; the survivor buffer
        /// is intentionally left unscored (the entry_id overlay carries the q-values
        /// back). No full-population PercolatorEntry/PercolatorResult stack is built.
        /// </summary>
        private static FdrProjectionSet ComputePass2Projection(
            PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            Func<string, IReadOnlyDictionary<uint, double>> resolveProteinQ,
            Action<string, IReadOnlyList<FdrScoreRecord>> flushFile)
        {
            // Canonicalize the survivor buffer order EXACTLY as the resident path does.
            // The FdrEntry RunPercolatorFdr overload (the flag-off oracle) sorts
            // perFileEntries in place by (EntryId, Charge, ScanNumber, ParquetIndex) as
            // its first step (PercolatorEngine.cs) -- the post-rescore pool can carry
            // gap-fill entries appended after the sorted pre-existing rows, and that
            // re-sort moves them into place. Downstream Stage 7/8 (protein FDR, blib
            // retention-time reporting) reads this buffer IN ORDER, so its ordering is
            // byte-identity-critical even though the projection carries its own sorted
            // copy. The projection path routes the buffer to the SVM as a thin copy and
            // never sorts the buffer itself, so replicate the oracle's sort here or the
            // gap-fill order diverges and file-level RT sums drift (issue #4374).
            foreach (var kvp in perFileEntries)
            {
                kvp.Value.Sort(FdrEntry.CANONICAL_ORDER); // Array.Sort OK: CANONICAL_ORDER's terminal key ParquetIndex is unique per survivor here (reconciled-write numbering), so the comparison never ties
            }

            // Per-file reconciled scores path (Stage 6's rescored features when a
            // reconciled sibling exists, else the original Stage 4 parquet), or null when
            // no parquet was mapped -- mirrors the resident reload's effective-path pick.
            string Recon(string fileName) =>
                perFileParquetPaths.TryGetValue(fileName, out string parquetPath)
                    ? ParquetScoreCache.EffectiveScoresPathFromScoresPath(parquetPath)
                    : null;

            // identity -> reconciled row, resolved one file at a time so no more than one
            // file's map is resident. On a missing parquet or a read fault, return an
            // empty map -> every entry resolves to uint.MaxValue -> basic-feature
            // fallback, byte-identical to the resident path (null Features ->
            // BuildBasicFeatures).
            IReadOnlyDictionary<uint, uint> RowMap(string fileName)
            {
                string recon = Recon(fileName);
                if (recon == null)
                {
                    ctx.LogWarning(string.Format(
                        "Second-pass FDR: no parquet path mapped for file '{0}' " +
                        "(entries will run with basic-feature fallback). " +
                        "Check that each file's reconciled parquet is present.", fileName));
                    return new Dictionary<uint, uint>();
                }
                try
                {
                    return BuildReconciledScoreIndexToRow(recon);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        "Second-pass FDR: failed to read identity columns from {0}: {1}",
                        recon, ex.Message));
                    return new Dictionary<uint, uint>();
                }
            }

            var swReloadRows = Stopwatch.StartNew();
            var projections = FdrProjectionSet.BuildFromEntries(perFileEntries, RowMap);
            swReloadRows.Stop();

            // Preserve the resident path's nMapped < count visibility (risk #6): a
            // survivor whose identity is absent from the reconciled parquet resolves to
            // uint.MaxValue and runs on the basic-feature fallback. On the standard
            // datasets every survivor maps (nMapped == count), so this warns only on a
            // genuine stub/parquet mismatch.
            int totalMapped = 0;
            foreach (var kvp in projections.PerFile)
            {
                int total = kvp.Value.Count;
                int nMapped = 0;
                foreach (var proj in kvp.Value)
                {
                    if (proj.ParquetIndex != uint.MaxValue)
                        nMapped++;
                }
                if (nMapped < total)
                {
                    ctx.LogWarning(string.Format(
                        "Second-pass FDR: file '{0}' reconciled parquet is missing {1} of " +
                        "{2} survivor identities; those entries run with basic-feature " +
                        "fallback. Stub/parquet mismatch - check reconciled-parquet output integrity.",
                        kvp.Key, total - nMapped, total));
                }
                totalMapped += nMapped;
            }
            ctx.LogInfo(string.Format(
                "[TIMING] Baked reconciled rows for {0} survivor entries: {1:F1}s",
                totalMapped, swReloadRows.Elapsed.TotalSeconds));

            // Features streamed from the reconciled parquet by the baked (reconciled)
            // ParquetIndex; NaN/Inf are clamped to 0 by LoadPinFeaturesFromParquet -- the
            // same normalization the resident reload applied. A parquet-less file yields
            // an empty row list, so ResolveFeatureRow falls back to basic features.
            Func<string, IReadOnlyList<double[]>> load2 = fileName =>
            {
                string recon = Recon(fileName);
                if (recon == null)
                    return Array.Empty<double[]>();
                return ParquetScoreCache.LoadPinFeaturesFromParquet(recon);
            };

            // The caller gates on FdrMethod.Percolator, so the projection path is only
            // ever Percolator; the projection engine always streams via load2. The
            // StreamingSink assembles + writes each file's .2nd-pass.fdr_scores.bin from
            // the streamed q-values + the survivor's ExperimentProteinQvalue during the score
            // pass, so the q-values are never stored on the projection (issue #4355 / C1).
            // The EXPERIMENT-scope columns collapse into one record per entry_id, published for
            // WritePass2ExperimentSidecar to finish and write after the protein FDR runs
            // (format v5, issue #4486).
            var experiment = new FdrExperimentAccumulator();
            var sink = new FdrStreamingSink(
                projections, config, "Second-pass", resolveProteinQ, flushFile, experiment);
            FirstPassFdrTask.RunPercolatorFdr(
                projections, config, ctx, "Second-pass", load2, sink);
            ctx.Publish(new Pass2ExperimentScope(experiment));
            return projections;
        }

        /// <summary>
        /// Build the reconciled parquet's <c>(entry_id, charge, scan_number) -&gt; row</c>
        /// map from its lean stub identity columns
        /// (<see cref="ParquetScoreCache.LoadFdrStubsFromParquet(string)"/>, which assigns
        /// <see cref="FdrEntry.ParquetIndex"/> = row). The mirror of
        /// <see cref="LoadReconciledFeaturesByScoreIndex"/> that yields the ROW INDEX
        /// instead of the feature vector: that loader keys <c>featRows[i]</c> by identity
        /// and the streaming score pass reads <c>rows[row]</c> by the baked
        /// <see cref="FdrProjection.ParquetIndex"/>, so
        /// <c>rows[map[identity]] == featByScoreIndex[identity]</c> - the streamed feature
        /// lookup is byte-identical to the resident identity binding (issue #4374 risk
        /// #2). Because the reconciled parquet is written
        /// <c>(entry_id, charge, scan_number)</c>-sorted, the row is scan-monotonic within
        /// a <c>(entry_id, charge)</c> group, which is what keeps the scan-omitted
        /// projection sort valid. Duplicate identities keep the last row (map overwrite),
        /// matching the loader. Reads only the identity columns (no PIN feature / heavy
        /// blob load), one file at a time.
        /// </summary>
        internal static Dictionary<uint, uint> BuildReconciledScoreIndexToRow(
            string reconciledPath)
        {
            var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(reconciledPath);
            var map = new Dictionary<uint, uint>(stubs.Count);
            for (int i = 0; i < stubs.Count; i++)
            {
                // KEY is the row's score_index - its identity. VALUE is its position in THIS
                // file, because that is what addresses LoadPinFeaturesFromParquet's positional
                // feature array. The two were the same number before the reconciled parquet
                // became a subset, which is why this used to be able to conflate them.
                if (stubs[i].ParquetIndex.HasValue)
                    map[stubs[i].ParquetIndex.Value] = (uint)i;
            }
            // No collision to reason about any more. This keyed on
            // (entry_id, charge, scan_number) and needed a paragraph explaining why a
            // duplicate identity was harmless; score_index is unique per row by construction,
            // including for gap-fill rows, which are numbered past the source row count.
            return map;
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
            Action<string> logWarning, string mode,
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
                    "{0}: failed to reload PIN features from {1}: {2}",
                    mode, effectiveParquetPath, ex.Message));
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

        /// <summary>
        /// OSPREY_PASS2_QVALUE=transfer (per-run-only redesign). Carry the pass-1 q through
        /// verbatim and recompute ONLY the per-run q of the peaks reconciliation MOVED -- never
        /// the experiment q, which the best-peak anchor freezes (the best run is untouched, so
        /// re-taking the best-of-runs min returns the pass-1 value; see
        /// TODO-osprey_pass2_per_run_only_qvalue). For each file, read its OWN
        /// <c>.1st-pass.fdr_scores.bin</c> sidecar and build two per-file lookup tables from its
        /// <c>(Score, RunPrecursorQvalue)</c> / <c>(Score, RunPeptideQvalue)</c> pairs -- the
        /// sidecar Score is the averaged-model score, the SAME scale
        /// <see cref="ScoreWithFrozenModel"/> produces, so the table is scale-consistent by
        /// construction. Then classify every survivor by its reconciled feature score against
        /// its 1st-pass sidecar record:
        /// <list type="bullet">
        /// <item>UNCHANGED (recomputed score == the sidecar's, bit-exact): carry the full
        /// 1st-pass record verbatim.</item>
        /// <item>MOVED (has a sidecar record but the reconciled score differs): recompute run q
        /// from that file's tables; keep the 1st-pass experiment q + PEP.</item>
        /// <item>GAP-FILL (no sidecar record -- a new detection): run q from the tables;
        /// experiment q = the precursor's pass-1 experiment q (from <paramref name="firstPassModel"/>'s
        /// companion cross-file map) so the downstream best-of-runs clamp resolves it correctly.</item>
        /// </list>
        /// No global full-population table and no resident first-pass pool: the frozen model is
        /// captured on the lean projection first pass and each file's table is built from data
        /// already on disk, one file at a time. Returns false (caller falls back to the retrain)
        /// when the frozen model is unusable or the input-file list is absent.
        /// </summary>
        internal static bool TransferPerRunQ(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            OspreyConfig config,
            PipelineContext ctx,
            PercolatorResults firstPassModel)
        {
            var scorer = FrozenModelScorer.TryCreate(firstPassModel);
            if (scorer == null)
            {
                ctx.LogWarning(
                    "OSPREY_PASS2_QVALUE=transfer: frozen 1st-pass model has no usable model " +
                    "or standardizer; cannot transfer.");
                return false;
            }
            if (config.InputFiles == null)
            {
                ctx.LogWarning(
                    "OSPREY_PASS2_QVALUE=transfer: no input-file list to locate the per-file " +
                    "1st-pass sidecars; cannot transfer.");
                return false;
            }

            AverageFoldModel(firstPassModel, out double[] avgWeights, out double avgBias);
            int nFeatures = avgWeights.Length;
            var standardizer = firstPassModel.Standardizer;

            var inputByFileName = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var inputFile in config.InputFiles)
                inputByFileName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;

            // Cross-file pass-1 experiment q per entry id (the MIN across files -- experiment q is
            // an experiment-scope property, so every file's record for a precursor carries the same
            // value; min is a safe reducer). ONLY gap-fill peaks (no per-file record) consult it.
            // These light uint->double maps stay resident while the heavier per-file record maps +
            // tables are built and released one file at a time.
            //
            // This first pass ALSO gates the whole transfer on every mapped file's 1st-pass sidecar
            // being readable: a missing/corrupt sidecar would silently leave that file's moved peaks
            // at Stage-6's q=1.0 (dropped from the output). Rather than degrade one file, fail the
            // transfer here (BEFORE any entry is mutated) so the caller falls back to the 2nd-pass
            // retrain -- hard-fail over warn-and-proceed on silently-invalid output.
            // One read of the analysis-wide 1st-pass experiment sidecar, in place of the
            // per-file scan that used to reduce these three maps by MIN / MAX across every
            // file's sidecar (format v5, issue #4486). The reduction was collapsing copies:
            // every file carried the same experiment value for a given entry_id, so a MIN over
            // them returned that value. Now there is one record to read.
            var globalExperiment =
                FdrExperimentSidecar.ReadMap(
                    FdrExperimentSidecar.PathFor(ctx.Config?.OutputBlib,
                    ScoringTaskShared.ArtifactSiblingPath(ctx.Config), FdrScoresSidecar.Pass.FirstPass),
                    FdrScoresSidecar.Pass.FirstPass);
            if (globalExperiment == null)
            {
                // Same disposition the unreadable per-file sidecar had: fall back to the
                // 2nd-pass retrain rather than silently leaving moved peaks at q = 1.0, which
                // drops them from the output. Hard-fail over warn-and-proceed.
                ctx.LogWarning(
                    "OSPREY_PASS2_QVALUE=transfer: the 1st-pass experiment-scope FDR sidecar is " +
                    "missing or unreadable; falling back to the 2nd-pass Percolator retrain " +
                    "rather than silently dropping reconciliation-moved peaks.");
                return false;
            }

            var scratch = new double[nFeatures]; // reused per entry to avoid a per-row allocation
            int nUnchanged = 0, nMoved = 0, nGapFill = 0, nSkipped = 0, nMissingSidecar = 0, nFilesDone = 0;
            // Per-file progress: building each file's per-run tables + classifying its survivors ran
            // silently for minutes on an 82-file join (the gap between Stage 6 and the summary below).
            var transferProgress = new ProgressReporter(
                string.Format(@"Transferring per-run q-values across {0} file(s)", perFileEntries.Count),
                perFileEntries.Count);
            int transferIdx = 0;
            foreach (var kvp in perFileEntries)
            {
                transferProgress.Report(++transferIdx);
                if (!inputByFileName.TryGetValue(kvp.Key, out string inputFile))
                {
                    nSkipped += kvp.Value.Count;
                    continue;
                }
                string pass1Path = FdrScoresSidecar.Pass1Path(inputFile);

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
                    nMissingSidecar++;
                    ctx.LogWarning(string.Format(
                        "OSPREY_PASS2_QVALUE=transfer: could not read the 1st-pass sidecar for '{0}' " +
                        "({1}); this file's per-run q is left unadjusted.", kvp.Key, pass1Path));
                    continue;
                }
                BuildScoreToQTable(precScores, precQs, out double[] precScoresDesc, out double[] precQDesc);
                BuildScoreToQTable(pepScores, pepQs, out double[] pepScoresDesc, out double[] pepQDesc);

                foreach (var entry in kvp.Value)
                {
                    if (entry.Features == null || entry.Features.Length != nFeatures)
                    {
                        // No reconciled features resolved (a stub/parquet mismatch the reload
                        // already warned about). Leave this entry's q as-is rather than guess.
                        nSkipped++;
                        continue;
                    }
                    double newScore = ScoreWithFrozenModel(
                        entry.Features, standardizer, avgWeights, avgBias, scratch);

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
                        case PerRunClass.Unchanged: nUnchanged++; break;
                        case PerRunClass.Moved: nMoved++; break;
                        default: nGapFill++; break;
                    }
                }
                nFilesDone++;
            }
            transferProgress.Dispose();

            ctx.LogInfo(string.Format(
                "OSPREY_PASS2_QVALUE=transfer: per-run q transfer over {0} file(s) -- {1} unchanged " +
                "(pass-1 q carried), {2} moved (run q re-mapped, experiment q carried), {3} gap-fill " +
                "(new run q + carried experiment q){4}{5}.",
                nFilesDone, nUnchanged, nMoved, nGapFill,
                nMissingSidecar > 0
                    ? string.Format("; {0} file(s) had no readable 1st-pass sidecar", nMissingSidecar)
                    : string.Empty,
                nSkipped > 0
                    ? string.Format("; {0} entr(y/ies) skipped for missing features", nSkipped)
                    : string.Empty));
            return true;
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

        /// <summary>
        /// Apply the averaged frozen model to a single raw feature vector: standardize a
        /// copy into the caller-supplied <paramref name="scratch"/> buffer, then
        /// score = avgBias + sum(avgWeights[j] * std(feat)[j]). Mirrors the per-entry math
        /// in <c>PercolatorScorer.ScorePopulationAndComputeFdr</c>, which likewise reuses a
        /// single feature buffer to avoid a per-entry allocation in the scoring loop. Does
        /// not mutate <paramref name="rawFeatures"/>; overwrites <paramref name="scratch"/>
        /// (length must be &gt;= rawFeatures.Length).
        /// </summary>
        internal static double ScoreWithFrozenModel(
            double[] rawFeatures,
            FeatureStandardizer standardizer,
            double[] avgWeights,
            double avgBias,
            double[] scratch)
        {
            Array.Copy(rawFeatures, 0, scratch, 0, rawFeatures.Length);
            standardizer.TransformSlice(scratch);
            double score = avgBias;
            for (int j = 0; j < avgWeights.Length; j++)
                score += avgWeights[j] * scratch[j];
            return score;
        }

        /// <summary>Number of equal-count score-quantile bins
        /// <see cref="BuildScoreToQTable"/> smooths the per-entry q into. Large enough to
        /// trace the FDR curve finely, small enough that each bin averages out the
        /// per-entry q noise from the raw-vs-calibrated score scale mismatch.</summary>
        private const int SCORE_Q_TABLE_BINS = 1000;

        /// <summary>
        /// Average the frozen Percolator fold weights + biases into a single (weights, bias)
        /// pair -- the same averaged-model math <c>PercolatorScorer.ScorePopulationAndComputeFdr</c>
        /// applies before scoring a population. Caller has already verified the model carries
        /// at least one fold.
        /// </summary>
        private static void AverageFoldModel(
            PercolatorResults model, out double[] avgWeights, out double avgBias)
        {
            int nModels = model.FoldWeights.Count;
            int nFeatures = model.FoldWeights[0].Length;
            avgWeights = new double[nFeatures];
            avgBias = 0.0;
            for (int f = 0; f < nModels; f++)
            {
                double[] foldW = model.FoldWeights[f];
                for (int j = 0; j < nFeatures; j++)
                    avgWeights[j] += foldW[j];
                avgBias += model.FoldBiases[f];
            }
            for (int j = 0; j < nFeatures; j++)
                avgWeights[j] /= nModels;
            avgBias /= nModels;
        }

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
        /// Mutable holder for the per-file 2nd-pass sidecar write counts. Passing this
        /// object (rather than captured <c>int</c> locals) into the StreamingSink flush
        /// closure keeps the counts shared with the resident write block without the
        /// closure capturing variables the outer scope also mutates.
        /// </summary>
        private sealed class Pass2WriteTallies
        {
            public int Written;

            /// <summary>Files the ONE remaining skip declined to write - <c>--task
            /// ModelDiagnostics</c>, whose contract is that it touches no artifact but the
            /// report. It creates no absence: that mode runs over a completed run whose files
            /// are already on disk.</summary>
            public int Skipped;

            public int Failures;
        }

        /// <summary>
        /// Writes one file's <c>.2nd-pass.fdr_scores.bin</c> plus its inline validity sidecar,
        /// with the resume skip and the shared counts.
        ///
        /// <para>Three paths emit these files - the projection score pass's flush callback, the
        /// frozen streamed competition, and the resident write block - and the per-file body was
        /// written out twice before this existed, which is how the projection path acquired the
        /// <c>--task ModelDiagnostics</c> skip and the resident one did not. One body, so a path
        /// cannot quietly differ from another in what it writes or what it counts.</para>
        ///
        /// <para>The inline validity sidecar is not optional bookkeeping: it is written next to
        /// each binary as that binary lands, so an early <c>Environment.Exit</c> (the
        /// OSPREY_STAGE7_PROTEIN_FDR_ONLY / diagnostics-dump path, which never reaches
        /// <c>AnalysisPipeline.WriteTaskSidecars</c>) still leaves every completed file as a
        /// resume-able binary + sidecar pair.</para>
        /// </summary>
        private sealed class Pass2SidecarWriter
        {
            private readonly PipelineContext _ctx;
            private readonly OspreyConfig _config;
            private readonly string _taskName;
            private readonly string _taskValidityKey;
            private readonly Dictionary<string, string> _inputByFileName =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public Pass2SidecarWriter(PipelineContext ctx, OspreyConfig config,
                string taskName, string taskValidityKey)
            {
                _ctx = ctx;
                _config = config;
                _taskName = taskName;
                _taskValidityKey = taskValidityKey;
                if (config.InputFiles == null)
                    return;
                foreach (string inputFile in config.InputFiles)
                    _inputByFileName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;
            }

            /// <summary>The per-file write counts this run's summary line reports.</summary>
            public Pass2WriteTallies Tallies { get; } = new Pass2WriteTallies();

            /// <summary>
            /// The input file a per-file key names, or null when no <c>config.InputFiles</c>
            /// entry matches it - a name drift between Stage 5 and Stage 7, which every caller
            /// reports before skipping the file.
            /// </summary>
            public string InputFor(string fileName)
            {
                return _inputByFileName.TryGetValue(fileName, out string inputFile) ? inputFile : null;
            }

            /// <summary>Every per-file key that has no <c>config.InputFiles</c> entry.</summary>
            public List<string> UnmatchedKeys(IEnumerable<string> fileNames)
            {
                var unmatched = new List<string>();
                foreach (string fileName in fileNames)
                    if (!_inputByFileName.ContainsKey(fileName))
                        unmatched.Add(fileName);
                return unmatched;
            }

            /// <summary>True when this file's sidecar is already a readable current-format
            /// 2nd-pass file, i.e. this run has nothing to compute for it.</summary>
            public bool IsCurrent(string fileName)
            {
                string inputFile = InputFor(fileName);
                return inputFile != null && FdrScoresSidecar.IsCurrentFormat(
                    FdrScoresSidecar.Pass2Path(inputFile), FdrScoresSidecar.Pass.SecondPass);
            }

            /// <summary>Write one file's sidecar from the resident survivor entries.</summary>
            public bool Write(string fileName, IReadOnlyList<FdrEntry> entries)
            {
                return WriteCore(fileName, path => FdrScoresSidecar.Write(
                    path, entries, FdrScoresSidecar.Pass.SecondPass));
            }

            /// <summary>Write one file's sidecar from assembled records (the projection path,
            /// which never materializes an <see cref="FdrEntry"/>). No return value: that path
            /// writes final records and has no second pass to decide about.</summary>
            public void Write(string fileName, IReadOnlyList<FdrScoreRecord> records)
            {
                WriteCore(fileName, path => FdrScoresSidecar.Write(
                    path, records, FdrScoresSidecar.Pass.SecondPass));
            }

            /// <summary>
            /// The shared body: resolve the path, honor the two skips, write, then write the
            /// validity sidecar. Returns true only when this call actually wrote the binary -
            /// a caller that finishes the file in a later pass (the frozen competition's
            /// experiment-scope patch) must not touch a file it did not write.
            /// </summary>
            private bool WriteCore(string fileName, Action<string> write)
            {
                string inputFile = InputFor(fileName);
                if (inputFile == null)
                    return false;
                string pass2Path = FdrScoresSidecar.Pass2Path(inputFile);
                // --task ModelDiagnostics touches no artifact but the report. The sidecar it
                // would write here holds the same q-values it is reading back, so skipping the
                // write changes nothing except leaving the completed run's files untouched.
                if (_config.DiagnosticsOnly)
                {
                    Tallies.Skipped++;
                    return false;
                }
                // No "already on disk, skip" here, deliberately. A conditionally-written file
                // makes its own absence ambiguous - unnecessary, or a write that failed and
                // never committed - and the second pass is deterministic, so rewriting is
                // writing the same bytes again. The caller reloads before this when it did not
                // recompute, so "the same bytes" is what a resumed run actually puts back.
                // OutOfMemoryException propagates: swallowing it here would report a
                // memory-dead process as one file's write failure and let the run
                // exit 0 with a declared sidecar absent - the same filter every
                // read-side catch of this artifact carries (#4615).
                try
                {
                    write(pass2Path);
                    Tallies.Written++;
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    _ctx.LogWarning(string.Format(
                        @"Failed to write 2nd-pass FDR sidecar for {0}: {1}", fileName, ex.Message));
                    Tallies.Failures++;
                    return false;
                }
                try
                {
                    TaskValiditySidecar.Write(pass2Path, _taskName, OspreyVersion.Current,
                        _taskValidityKey,
                        new[] { ParquetScoreCache.EffectiveScoresPathFromScoresPath(
                            ParquetScoreCache.GetScoresPath(inputFile)) });
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    _ctx.LogWarning(string.Format(
                        @"Failed to write {0} sidecar for {1}: {2}", _taskName, pass2Path, ex.Message));
                }
                return true;
            }
        }
    }
}
