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
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            string taskName,
            string taskValidityKey)
        {
            var config = ctx.Config;
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

            // Per-file 2nd-pass sidecar write tallies, shared by the projection path
            // (updated in the StreamingSink flush callback during the score pass) and
            // the resident write block below, so the summary log reads one set of
            // counts. A holder object (not captured ints) keeps the flush closure clean.
            var pass2Tally = new Pass2WriteTallies();

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
                var inputByFileName = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var inputFile in config.InputFiles)
                    inputByFileName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;

                // Surface any perFileEntries key that has no matching
                // entry in config.InputFiles -- a silent skip here would
                // hide a name-drift bug that the standard cross-impl gate
                // (where keys always match) cannot catch.
                var unmatchedKeys = perFileEntries
                    .Where(kvp => !inputByFileName.ContainsKey(kvp.Key))
                    .Select(kvp => kvp.Key)
                    .ToList();
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
                    if (!inputByFileName.TryGetValue(kvp.Key, out string probeInput))
                        continue;
                    if (!FdrScoresSidecar.IsCurrentFormat(FdrScoresSidecar.Pass2Path(probeInput),
                                                          FdrScoresSidecar.Pass.SecondPass))
                        missingPass2++;
                }
                if (missingPass2 > 0)
                {
                    ctx.LogVerbose(string.Format(
                        "{0}/{1} file(s) have no precomputed second-pass FDR scores -- computing " +
                        "them here from the reconciled features (reused distributed-run code path).",
                        missingPass2, totalFiles));
                    // Stage 6's post-rescore overlay calls FdrEntry.ResetScores(), which clears
                    // eight fields. Of the seven the sidecar carries, three can reach it at
                    // their reset defaults (issue #4553):
                    //   Score              - neither COMPETITION mode wrote one back
                    //                        (transfer-compete, protein-compact). The transfer
                    //                        mode's AssignPerRunQ does set it, on all three of
                    //                        its branches.
                    //   Pep                - written only on the on-stratum path
                    //   RunProteinQvalue   - written by NO mode; first-pass protein FDR is its
                    //                        only producer, and the second-pass one writes
                    //                        ExperimentProteinQvalue instead
                    // (ResetScores' eighth field, ExperimentProteinQvalue, has no slot in the
                    // v3 record and is written after this by RunProteinFdr, so it is out of
                    // scope here rather than recomputed.)
                    // Seeding all three from the 1st-pass sidecar reproduces exactly what the
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
                    var swRestore = Stopwatch.StartNew();
                    RestorePass1Scalars(ctx, perFileEntries, inputByFileName);
                    swRestore.Stop();
                    ctx.LogVerbose(string.Format(
                        "[STAGE-WALL] pass-1 scalar restore: {0:F1}s", swRestore.Elapsed.TotalSeconds));

                    var swPass2 = Stopwatch.StartNew();

                    // --model-diagnostics needs the resident 2nd-pass model: its feature
                    // contributions feed the pass-2 model view, and the projection 2nd pass
                    // streams through a sink and produces none. Route --model-diagnostics to
                    // the resident path so ComputePass2Resident can return the model. Off the
                    // default output path, so byte-identity is unaffected (#4377).
                    // The frozen-model modes (transfer, transfer-compete, protein-compact) also
                    // take the resident path: transfer needs each survivor's RECONCILED features
                    // on entry.Features (ComputePass2Resident does that), and transfer-compete /
                    // protein-compact re-score with the frozen 1st-pass model over the full
                    // pre-compaction population / protein stratum -- a competition the projection
                    // engine does not do (it trains + competes over the survivor set only). Their
                    // frozen score pass itself STREAMS one file at a time inside
                    // ComputePass2TransferCompeteFull, so routing them resident does NOT hold all
                    // features resident. protein-compact + OSPREY_PROTEIN_COMPACT_RETRAIN=1 is the
                    // exception: it retrains, so it stays on the projection (streaming-retrain) path.
                    if (OspreyEnvironment.UseFdrProjection && config.FdrMethod.UsesPercolatorFramework() &&
                        !config.ModelDiagnostics && !OspreyEnvironment.Pass2TransferQ &&
                        !OspreyEnvironment.Pass2TransferCompete &&
                        !(OspreyEnvironment.Pass2ProteinCompact && !OspreyEnvironment.Pass2ProteinCompactRetrain))
                    {
                        // Projection 2nd pass (issue #4374 + #4355 struct-shrink S0 / C1):
                        // stream the reconciled PIN features through the SAME projection
                        // engine the 1st pass uses, rather than loading every survivor's
                        // 21-feature vector resident. The lean projection no longer stores
                        // the q-values (2nd-pass peak 80 -> 32 B); a StreamingSink assembles
                        // each .2nd-pass.fdr_scores.bin record DURING the score pass (from
                        // the streamed q-values + the survivor's RunProteinQvalue looked up
                        // by entry_id) and flushes the per-file sidecar + validity sidecar
                        // directly, so the resident write block below is skipped for this
                        // path. The existing entry_id overlay still carries the 2nd-pass
                        // q-values onto the resident survivor buffer afterward (unchanged).

                        // Survivor RunProteinQvalue by entry_id, per file: the value
                        // BuildFromEntries used to carry onto the struct. All survivors
                        // sharing an entry_id share a precursor (hence a ModifiedSequence,
                        // hence a run_protein_qvalue), so the last-write map is exact.
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
                                    map[e.EntryId] = e.RunProteinQvalue;
                            }
                            return map;
                        }

                        // Per-file flush: write the .2nd-pass.fdr_scores.bin from the
                        // assembled records (skip-if-already-on-disk, preserving the resume
                        // optimization), then the inline validity sidecar, updating the
                        // shared tallies. This is the per-file body the resident write block
                        // ran, sourced from records instead of the resident buffer.
                        void FlushPass2File(string fileName, IReadOnlyList<FdrScoreRecord> records)
                        {
                            if (!inputByFileName.TryGetValue(fileName, out string inputFileFlush))
                                return;
                            string pass2PathFlush = FdrScoresSidecar.Pass2Path(inputFileFlush);
                            if (FdrScoresSidecar.IsCurrentFormat(pass2PathFlush, FdrScoresSidecar.Pass.SecondPass))
                            {
                                pass2Tally.AlreadyOnDisk++;
                                return;
                            }
                            try
                            {
                                FdrScoresSidecar.Write(
                                    pass2PathFlush, records, FdrScoresSidecar.Pass.SecondPass);
                                pass2Tally.Written++;
                            }
                            catch (Exception ex)
                            {
                                ctx.LogWarning(string.Format(
                                    @"Failed to write 2nd-pass FDR sidecar for {0}: {1}",
                                    fileName, ex.Message));
                                pass2Tally.Failures++;
                                return;
                            }
                            try
                            {
                                TaskValiditySidecar.Write(pass2PathFlush, taskName,
                                    OspreyVersion.Current, taskValidityKey,
                                    new[] { ParquetScoreCache.EffectiveScoresPathFromScoresPath(
                                        ParquetScoreCache.GetScoresPath(inputFileFlush)) });
                            }
                            catch (Exception ex)
                            {
                                ctx.LogWarning(string.Format(
                                    @"Failed to write {0} sidecar for {1}: {2}",
                                    taskName, pass2PathFlush, ex.Message));
                            }
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

            // Persist post-Stage-6 per-file 2nd-pass FDR scores
            // BEFORE RunProteinFdr. The sidecar holds Score +
            // run/experiment precursor/peptide q-values + Pep +
            // RunProteinQvalue (the latter set by
            // RunFirstPassProteinFdr earlier); none of those
            // fields are mutated by RunProteinFdr, which only
            // sets ExperimentProteinQvalue via
            // PropagateProteinQvalues. Writing here lets the
            // OSPREY_STAGE7_PROTEIN_FDR_ONLY early exit (used
            // by stage6 isolation in Test-Regression) leave the
            // sidecar on disk for downstream rehydration.
            // Probe-the-disk per file: only write sidecars that are
            // not already on disk. The earlier "any sidecar present
            // -> skip all writes" gate broke partial-resume -- if a
            // prior run crashed mid-write and left some files with
            // sidecars and others without, the missing ones would
            // never get written. Per-file probe preserves the
            // skip-when-already-present optimization for the
            // stage7-style "everything loaded from disk" case while
            // also healing partial state.
            if (perFileParquetPaths.Count > 0 && config.InputFiles != null)
            {
                var inputByFileName = new Dictionary<string, string>();
                foreach (var inputFile in config.InputFiles)
                    inputByFileName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;

                // Surface any perFileEntries key not in config.InputFiles
                // -- a silent skip below would mean that file gets no
                // .2nd-pass sidecar written and the next resume re-runs
                // its second-pass FDR unnecessarily.
                var unmatchedSidecarKeys = perFileEntries
                    .Where(kvp => !inputByFileName.ContainsKey(kvp.Key))
                    .Select(kvp => kvp.Key)
                    .ToList();
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
                // the resident survivor buffer. On the projection path (pass2Projections
                // != null, issue #4355 struct-shrink S0 / C1) the StreamingSink already
                // wrote the .bin + validity sidecar per file during the score pass, so
                // this loop is skipped -- only the shared tallies it updated drive the
                // summary log below.
                if (pass2Projections == null)
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
                            string fileName = kvp.Key;
                            if (!inputByFileName.TryGetValue(fileName, out string inputFile3))
                                continue;
                            string pass2Path = FdrScoresSidecar.Pass2Path(inputFile3);
                            if (FdrScoresSidecar.IsCurrentFormat(pass2Path, FdrScoresSidecar.Pass.SecondPass))
                            {
                                pass2Tally.AlreadyOnDisk++;
                                continue;
                            }
                            try
                            {
                                FdrScoresSidecar.Write(
                                    pass2Path, kvp.Value, FdrScoresSidecar.Pass.SecondPass);
                                pass2Tally.Written++;
                            }
                            catch (Exception ex)
                            {
                                ctx.LogWarning(string.Format(
                                    @"Failed to write 2nd-pass FDR sidecar for {0}: {1}",
                                    fileName, ex.Message));
                                pass2Tally.Failures++;
                                continue;
                            }
                            // Inline per-file validity sidecar: same content
                            // the end-of-Run WriteTaskSidecars would produce,
                            // written immediately so an early Environment.Exit
                            // does not strand the binary without its metadata.
                            try
                            {
                                TaskValiditySidecar.Write(pass2Path, taskName, OspreyVersion.Current,
                                    taskValidityKey,
                                    new[] { ParquetScoreCache.EffectiveScoresPathFromScoresPath(
                                        ParquetScoreCache.GetScoresPath(inputFile3)) });
                            }
                            catch (Exception ex)
                            {
                                ctx.LogWarning(string.Format(
                                    @"Failed to write {0} sidecar for {1}: {2}",
                                    taskName, pass2Path, ex.Message));
                            }
                        }
                    }
                }
                if (pass2Tally.Failures == 0 && pass2Tally.Written > 0)
                {
                    ctx.LogVerbose(string.Format(
                        @"Wrote 2nd-pass FDR scores for {0} file(s){1}",
                        pass2Tally.Written,
                        pass2Tally.AlreadyOnDisk > 0
                            ? string.Format(@" ({0} already on disk; skipped)", pass2Tally.AlreadyOnDisk)
                            : string.Empty));
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
            if (perFileParquetPaths.Count > 0 && config.InputFiles != null)
            {
                var inputByName = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var inputFile in config.InputFiles)
                    inputByName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;
                int filesReloaded = 0;
                int filesMissing = 0;
                // Per-file progress: reads back every file's just-written sidecar and rebuilds
                // an entry_id map over that file's survivors. Silent, and the second half of
                // the 38s gap between the competition's [STAGE-WALL] line and the next probe
                // (#4486); the write loop above is the first half.
                using (var reloadProgress = new ProgressReporter(
                    string.Format(@"Reloading 2nd-pass FDR scores for {0} file(s)", perFileEntries.Count),
                    perFileEntries.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
                {
                    long nReloadReported = 0;
                    foreach (var kvp in perFileEntries)
                    {
                        reloadProgress.Report(++nReloadReported);
                        if (!inputByName.TryGetValue(kvp.Key, out string inputFile4))
                            continue;
                        string pass2Path = FdrScoresSidecar.Pass2Path(inputFile4);
                        if (!FdrScoresSidecar.IsCurrentFormat(pass2Path, FdrScoresSidecar.Pass.SecondPass))
                        {
                            filesMissing++;
                            continue;
                        }
                        var byEntryId = new Dictionary<uint, FdrEntry>(kvp.Value.Count);
                        foreach (var e in kvp.Value)
                            byEntryId[e.EntryId] = e;
                        if (FdrScoresSidecar.TryReadOverlay(
                                pass2Path, byEntryId, FdrScoresSidecar.Pass.SecondPass))
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
                        "Reloaded 2nd-pass FDR scores for {0}/{1} file(s) post-compaction",
                        filesReloaded, filesReloaded + filesMissing));
                }
            }

            return pass2Contributions;
        }

        /// <summary>
        /// Re-seed each survivor's <see cref="FdrEntry.Score"/>, <see cref="FdrEntry.Pep"/> and
        /// <see cref="FdrEntry.RunProteinQvalue"/> from that file's
        /// <c>.1st-pass.fdr_scores.bin</c>.
        ///
        /// <para>These are the three sidecar fields <see cref="FdrEntry.ResetScores"/> clears
        /// that pass 2 does not reliably recompute: neither COMPETITION mode wrote
        /// <c>Score</c> back (the <c>transfer</c> mode's <c>AssignPerRunQ</c> does, on all
        /// three branches), <c>Pep</c> is written only for on-stratum survivors, and
        /// <c>RunProteinQvalue</c> is written by no mode at all. Left unseeded they reach the
        /// 2nd-pass sidecar at their reset defaults, where a q-value of 1.0 reads as a
        /// confident rejection and a <c>Score</c> of 0 sits exactly ON the discriminant's
        /// accept/reject boundary (issue #4553).</para>
        ///
        /// <para>Seeding, not overriding: whatever pass 2 genuinely recomputes is written after
        /// this and wins. What is left is the pass-1 value, which is precisely what the
        /// distributed route holds at the same point - it rehydrates from this same sidecar -
        /// so the two routes agree by construction rather than by coincidence.</para>
        ///
        /// <para>An entry Stage 6 did not touch already holds these values, so the write is a
        /// no-op for it; a gap-fill entry is absent from the sidecar and correctly keeps the
        /// defaults, which is where the distributed route leaves it too (its own overlay runs
        /// before gap-fill appends). One file's records stream at a time.</para>
        /// </summary>
        private static void RestorePass1Scalars(
            PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> inputByFileName)
        {
            int nRestored = 0;
            int filesRead = 0;
            var unreadable = new List<string>();
            foreach (var kvp in perFileEntries)
            {
                if (!inputByFileName.TryGetValue(kvp.Key, out string inputFile))
                    continue;
                string pass1Path = FdrScoresSidecar.Pass1Path(inputFile);
                if (!File.Exists(pass1Path))
                {
                    unreadable.Add(kvp.Key);
                    continue;
                }
                var byEntryId = new Dictionary<uint, FdrEntry>(kvp.Value.Count);
                foreach (var e in kvp.Value)
                    byEntryId[e.EntryId] = e;

                // Stage into a buffer and apply only on a clean read. ReadRecords documents
                // that a false return can arrive AFTER it has invoked the callback ("with the
                // partial callback effects the caller must discard"), and records stream in
                // file order, so mutating in the callback would leave the entries before the
                // fault carrying pass-1 values and the rest at reset defaults - a half-seeded
                // pool that no warning could describe and nothing downstream could detect.
                var staged = new List<KeyValuePair<FdrEntry, FdrScoreRecord>>();
                bool ok = FdrScoresSidecar.ReadRecords(
                    pass1Path, FdrScoresSidecar.Pass.FirstPass,
                    rec =>
                    {
                        if (byEntryId.TryGetValue(rec.EntryId, out FdrEntry entry))
                            staged.Add(new KeyValuePair<FdrEntry, FdrScoreRecord>(entry, rec));
                    });
                if (ok)
                {
                    foreach (var pair in staged)
                    {
                        pair.Key.Score = pair.Value.Score;
                        pair.Key.Pep = pair.Value.Pep;
                        pair.Key.RunProteinQvalue = pair.Value.RunProteinQvalue;
                        // The FOURTH field of the same five-of-eight gap (sidecar v4, issue
                        // #4522). ResetScores clears it with Score, and no frozen 2nd-pass mode
                        // writes it back, so it lands in the 2nd-pass sidecar at 0.0 for every
                        // peak Stage 6 touched. That is the whole population this method exists
                        // to repair, and it is why the seed should follow the record rather than
                        // an enumerated list: the list has now grown twice.
                        pair.Key.ExperimentAggregateScore = pair.Value.ExperimentAggregateScore;
                    }
                    filesRead++;
                    nRestored += staged.Count;
                }
                else
                {
                    unreadable.Add(kvp.Key);
                }
            }

            // Reported, not thrown on - but NOT because the consequence is cosmetic. Score
            // feeds the Stage 8 picked-protein FDR that runs a few statements after this whole
            // method returns (SecondPassFdrTask RunProteinFdr -> ProteinFdrEngine.RunSecondPass
            // -> ProteinFdr.CollectBestPeptideScores takes max(entry.Score)), and that decoy
            // side is not q-gated, so an unseeded 0.0 competes in the null. That is the very
            // mechanism this fix exists to remove.
            //
            // It stays a warning because the modes divide cleanly: a frozen mode genuinely
            // needs the sidecar and already fail-fasts on it further down, while the retrain
            // path rescores every entry and overwrites the seed, so a missing sidecar there is
            // harmless. Escalating here would break the harmless case to guard one that is
            // already guarded. The warning therefore has to state the real consequence rather
            // than imply there is none.
            if (unreadable.Count > 0)
            {
                ctx.LogWarning(string.Format(
                    "1st-pass Score/Pep/RunProteinQvalue/ExperimentAggregateScore could not be " +
                    "restored for {0} file(s) (no readable 1st-pass sidecar): [{1}]. Peaks Stage 6 " +
                    "changed in those files keep reset defaults, so their 2nd-pass sidecars are " +
                    "wrong AND a Score of 0 enters the second-pass protein FDR null unfiltered. " +
                    "Treat this run's protein-level numbers as unreliable.",
                    unreadable.Count, string.Join(", ", unreadable)));
            }
            ctx.LogVerbose(string.Format(
                "Restored 1st-pass Score/Pep/RunProteinQvalue/ExperimentAggregateScore onto {0} survivor(s) across {1} file(s).",
                nRestored, filesRead));
        }

        /// <summary>
        /// OSPREY_PASS2_QVALUE=transfer-compete (full-population form). Recompute the reported
        /// precursor q-values + PEP by re-running the target-decoy competition over the ENTIRE
        /// 1st-pass population -- read as SCALARS from each file's persisted
        /// <c>.1st-pass.fdr_scores.bin</c> -- with ONLY the reconciled survivors' scores swapped
        /// in (the FROZEN 1st-pass model applied to their reconciled features). Because &gt;99% of
        /// scores are unchanged, the recomputed q lands on the calibrated 1st-pass value; the
        /// reconciled minority get honest full-population q. No 2nd-pass retrain and no
        /// reduced-pool null (the null is the full 1st-pass decoy set). ONE FILE's PIN feature
        /// map is resident at a time - the frozen-model scoring moved inside this method's
        /// per-file read (#4486), so it is the largest allocation on the path, released before
        /// the next file; the cross-file state is flat scalar arrays and O(distinct) maps.
        /// Writes q/PEP onto the reported survivor entries in
        /// place. Returns false when the frozen model or any 1st-pass scalar sidecar is missing;
        /// the caller then THROWS with actionable guidance - an explicitly requested frozen mode
        /// must never silently degrade to the anti-conservative retrain. (This said "caller falls
        /// back to the retrain", which is the opposite of the fail-fast the caller implements.)
        /// Every `return false` is placed BEFORE any survivor is mutated, so a refusal leaves the
        /// pool untouched.
        /// </summary>
        private static bool ComputePass2TransferCompeteFull(
            PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            PercolatorResults frozenModel,
            string pass1ExperimentAgg,
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

            // 1. Reported survivors indexed by file for the per-file emit below, plus the one
            //    genuinely global set: every survivor entry_id. The best-of-runs floor is a
            //    per-entry_id minimum across files, so that set has to span the run - but it is
            //    O(distinct entry_ids), not O(files x entry_ids).
            //
            //    What used to sit here was a separate whole-run pass that loaded EVERY file's
            //    reconciled PIN features and stashed all of their frozen-model scores in one
            //    Dictionary<(file, entry_id), double> (~3.8 GB at 82 files, #4486). The scoring
            //    is per file by nature, so it now happens one file at a time inside ReadFile
            //    below: same loader and identity key (LoadReconciledFeaturesByIdentity keyed by
            //    (EntryId,Charge,ScanNumber)), same scores, one file's worth resident, and one
            //    fewer pass over the reconciled parquets.
            //
            //    Two --input-scores paths in different directories CAN share a stem
            //    (RescoreHydration.PreCompactionTallies is index-keyed for exactly that reason),
            //    so a same-stem pair is MERGED here rather than last-wins.
            //
            //    Merging REPRODUCES the old disposition; it does NOT make duplicate stems
            //    correct, and must not be read as fixing them (#4555). The lookups this method
            //    performs are still stem-keyed and last-wins - sidecarByKey and
            //    perFileParquetPaths below, and the projection second pass's own
            //    survivorsByFile - so a duplicate stem still reads ONE file's scalars and
            //    applies the result to both files' entries. That is exactly what the whole-run
            //    map this replaced did (keyed (file, entry_id), map-back over perFileEntries),
            //    so this is the pre-existing hazard carried forward, not a new one; keeping
            //    only the last list would have been WORSE, leaving the other's entries with a
            //    mixture of refreshed and stale q-values. The real fix is path-hashed identity
            //    across artifact naming and every per-file map at once, tracked in #4555.
            //
            //    A hard throw was tried here and removed: it fired at Stage 7, after hours of
            //    Stages 1-6, for a condition knowable at argument-parse time, while the sibling
            //    maps stayed last-wins - so it converted one silent inconsistency into a late
            //    abort without making the class of input any safer.
            var entriesByFile = new Dictionary<string, List<FdrEntry>>(
                perFileEntries.Count, StringComparer.Ordinal);
            var survivorEntryIds = new HashSet<uint>();
            long survivorObservations = 0;
            // Reported because this walks EVERY survivor observation - 89,068,375 of them on the
            // 82-file SEA-AD run - into a HashSet before anything downstream logs a word. It sat
            // inside a 195 s silence between "Released library fragments" and the
            // OSPREY_PASS2_QVALUE banner, which reads as a hung run at the very end of a
            // multi-hour search. The two steps after it (sidecar path validation and the protein
            // stratum build) are in the same silence and are NOT yet reported - see the TODO.
            using (var mergeProgress = new ProgressReporter(
                string.Format(@"Collecting pass-2 survivors from {0} file(s)", perFileEntries.Count),
                perFileEntries.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                int mergeIdx = 0;
                foreach (var kvp in perFileEntries)
                {
                    mergeProgress.Report(++mergeIdx);
                    if (entriesByFile.TryGetValue(kvp.Key, out var merged))
                    {
                        // New list, never AddRange onto the caller's: perFileEntries is the live
                        // Stage 7 survivor buffer and must not gain entries as a side effect.
                        var combined = new List<FdrEntry>(merged.Count + kvp.Value.Count);
                        combined.AddRange(merged);
                        combined.AddRange(kvp.Value);
                        entriesByFile[kvp.Key] = combined;
                    }
                    else
                    {
                        entriesByFile[kvp.Key] = kvp.Value;
                    }
                    survivorObservations += kvp.Value.Count;
                    foreach (var e in kvp.Value)
                        survivorEntryIds.Add(e.EntryId);
                }
            }

            // 2. Per-file scalar sidecar paths. Validate every sidecar up front so we fail fast
            //    (and fall back to the retrain) before streaming any file.
            var fileKeys = new List<string>(perFileEntries.Count);
            // Pass-1 experiment q for the OFF-STRATUM peaks Stage 6 changed, read from the
            // sidecar because the post-rescore overlay already zeroed the in-memory value. Only
            // that set is stashed, so this stays small however many files there are.
            var pass1ExpQByKey = new Dictionary<(string, uint), (double prec, double pep)>();
            var sidecarByKey = new Dictionary<string, string>(perFileEntries.Count, StringComparer.Ordinal);
            foreach (var kvp in perFileEntries)
            {
                if (!perFileParquetPaths.TryGetValue(kvp.Key, out string parquetPath))
                {
                    ctx.LogWarning("transfer-compete: no parquet path for '" + kvp.Key +
                                   "'; cannot locate its 1st-pass scalar sidecar.");
                    return false;
                }
                string sidecarPath = Path.Combine(
                    Path.GetDirectoryName(parquetPath) ?? string.Empty,
                    kvp.Key + ".1st-pass.fdr_scores.bin");
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
                fileKeys.Add(kvp.Key);
                sidecarByKey[kvp.Key] = sidecarPath;
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
            //    file's features, scalars and run q are resident at a time; the cross-file state
            //    is bounded by the number of distinct precursors and distinct survivor entry_ids,
            //    so peak memory is flat in file count (the 32/64 GB many-file target). Run q is
            //    written onto each file's entries as that file finishes; experiment q and PEP are
            //    derived per entry afterwards from the bounded state this returns, so no
            //    (file, entry_id)-keyed result map is ever built.
            StreamingFdr.StreamedCompetitionState competition;
            long nScored = 0;
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

                (uint[] entryIds, double[] scores, IReadOnlyDictionary<uint, double> survivorScores)
                    ReadFile(string fileKey)
                {
                    // Frozen-model score for THIS file's reconciled survivors: load its PIN
                    // features, score with the frozen 1st-pass weights, keep only the scalar
                    // score, and release the features on the way out. Same loader and identity
                    // key the resident reload used, so each survivor's score is byte-identical.
                    // Both lookups are established by the validation loop above (every file has
                    // a parquet path or this method already returned false; entriesByFile is
                    // built from the same perFileEntries fileKeys comes from). Resolved OUTSIDE
                    // the try so a key miss cannot be reported as a parquet failure.
                    string effectiveParquetPath = ParquetScoreCache.EffectiveScoresPathFromScoresPath(
                        perFileParquetPaths[fileKey]);
                    var fileEntries = entriesByFile[fileKey];

                    // ONLY the load is guarded, as it was before this method took over the
                    // scoring. Widening the try over the scoring loop would let a mid-loop
                    // throw leave a PARTIALLY swapped-in map: the competition would then run
                    // on a mixed population, and under protein-compact the unscored remainder
                    // would also be missing from changedBaseIds, so those peaks would never be
                    // admitted and would be stamped run q 1.0 - all under a warning blaming a
                    // load that succeeded. Failure here is all-or-nothing per file.
                    Dictionary<(uint, byte, uint), double[]> featByIdentity;
                    try
                    {
                        featByIdentity = LoadReconciledFeaturesByIdentity(effectiveParquetPath);
                    }
                    catch (Exception ex)
                    {
                        // Same disposition as the old whole-run pass: this file contributes no
                        // swapped-in scores and competes on its stored 1st-pass ones.
                        ctx.LogWarning(string.Format(
                            "{0}: failed to reload PIN features from {1}: {2}",
                            mode, effectiveParquetPath, ex.Message));
                        featByIdentity = null;
                    }

                    var fileScores = new Dictionary<uint, double>();
                    if (featByIdentity != null)
                    {
                        foreach (var e in fileEntries)
                        {
                            if (featByIdentity.TryGetValue(
                                    (e.EntryId, e.Charge, e.ScanNumber), out double[] feats) &&
                                feats != null && feats.Length == nFeatures)
                            {
                                double frozenScore = scorer.Score(feats);
                                fileScores[e.EntryId] = frozenScore;
                                // This is the score the entry COMPETES on below, so it is the one
                                // the 2nd-pass sidecar must carry. RestorePass1Scalars seeded the
                                // 1st-pass value, which is what a survivor whose features did not
                                // resolve keeps - and which is what it competes on too.
                                e.Score = frozenScore;
                            }
                        }
                        // featByIdentity released here (one file resident at a time).
                    }
                    nScored += fileScores.Count;

                    FdrScoresSidecar.ReadScalars(sidecarByKey[fileKey], FdrScoresSidecar.Pass.FirstPass,
                        out uint[] eids, out double[] scs);
                    if (stratumBaseIds != null)
                        StashOffStratumPass1ExperimentQ(fileKey, sidecarByKey[fileKey], eids, scs, fileScores);
                    progress.Report(++nRead);
                    return (eids, scs, fileScores);
                }

                // Write this file's run q onto its entries while the map is still in hand, then
                // let it go. Those entries are already resident (they are Stage 7's input), so
                // this costs nothing, where holding every file's run q to the end of the run cost
                // ~3.8 GB at 82 files. An entry absent from the map won no competition in this
                // file and takes the 1.0 default the streamed form used to fill in centrally.
                void ApplyFileRunQ(string fileKey, IReadOnlyDictionary<uint, double> fileRunQ)
                {
                    foreach (var e in entriesByFile[fileKey])
                    {
                        double rq = fileRunQ.TryGetValue(e.EntryId, out double v) ? v : 1.0;
                        e.RunPrecursorQvalue = rq;
                        // Precursor-level path: keep peptide q in step with precursor q for the
                        // reported set (peptide-level FDR is not the target here).
                        e.RunPeptideQvalue = rq;
                    }
                }

                // Capture the pass-1 experiment q of the off-stratum peaks Stage 6 changed, so
                // the map-back can carry it. "Changed" is the same bit-exact test the admission
                // uses: the recomputed frozen-model score differs from the sidecar score. The
                // set is small, and the second sidecar pass is skipped entirely when it is empty.
                void StashOffStratumPass1ExperimentQ(string fileKey, string sidecarPath,
                    uint[] eids, double[] scs, IReadOnlyDictionary<uint, double> fileScores)
                {
                    var wanted = new HashSet<uint>();
                    for (int i = 0; i < eids.Length; i++)
                    {
                        if (!stratumBaseIds.Contains(eids[i] & 0x7FFFFFFFu) &&
                            fileScores.TryGetValue(eids[i], out double ov) &&
                            ov != scs[i])
                            wanted.Add(eids[i]);
                    }
                    if (wanted.Count == 0)
                        return;
                    // The result matters here as much as at the other read sites: ReadRecords
                    // returns false AFTER invoking the callback, so a partial read leaves
                    // pass1ExpQByKey holding SOME of this file's off-stratum q-values. Those are
                    // carried forward verbatim by the off-stratum branch, so a silent partial
                    // fill gives a subset of survivors their pass-1 q and the rest a default -
                    // a per-entry mix no downstream check can see.
                    if (!FdrScoresSidecar.ReadRecords(sidecarPath, FdrScoresSidecar.Pass.FirstPass, rec =>
                    {
                        if (wanted.Contains(rec.EntryId))
                        {
                            pass1ExpQByKey[(fileKey, rec.EntryId)] =
                                (rec.ExperimentPrecursorQvalue, rec.ExperimentPeptideQvalue);
                        }
                    }))
                    {
                        throw new IOException(
                            @"1st-pass sidecar could not be read in full while stashing off-stratum experiment q-values: " +
                            sidecarPath);
                    }
                }

                competition = StreamingFdr.ComputeFullPopulationPrecursorFdrStreaming(
                    fileKeys, ReadFile, survivorEntryIds, ApplyFileRunQ, stratumBaseIds);
            }

            // 4. Finish each reported survivor from the bounded competition state, one file at a
            //    time. Run q was written per file as the stream advanced, so what is left here is
            //    experiment q and PEP - both derived per entry from O(distinct) maps instead of
            //    read out of a whole-run (file, entry_id)-keyed result dictionary.
            int nMapped = 0;
            foreach (var kvp in perFileEntries)
                foreach (var e in kvp.Value)
                {
                    if (proteinCompact && !stratumBaseIds.Contains(e.EntryId & 0x7FFFFFFFu))
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
                        if (pass1ExpQByKey.TryGetValue((kvp.Key, e.EntryId), out var q1))
                        {
                            e.ExperimentPrecursorQvalue = q1.prec;
                            e.ExperimentPeptideQvalue = q1.pep;
                        }
                        nMapped++;
                        continue;
                    }
                    double eq = competition.ExperimentQ(e.EntryId, e.RunPrecursorQvalue);
                    e.ExperimentPrecursorQvalue = eq;
                    // Precursor-level path: keep peptide q in step with precursor q for the
                    // reported set (peptide-level FDR is not the target here).
                    e.ExperimentPeptideQvalue = eq;
                    e.Pep = competition.Pep(kvp.Key, e.EntryId);
                    // The aggregate MUST move with the q above. This mode recomputes experiment q
                    // from a fresh full-population competition, so the pass-1 aggregate
                    // RestorePass1Scalars seeded is no longer the score that q was ranked on -
                    // and this is the DEFAULT mode, so leaving it stale is not an edge case.
                    // Measured cost of the omission: the co-assignment panel's experiment
                    // boundary is a minimum over accepted precursors' aggregates, so entries
                    // still holding the ResetScores 0.0 default dragged it to 0.0 and admitted
                    // the entire decoy pool - 542,368 decoys against 117,783 targets on astral,
                    // 183x the pass-1 count, from a rule meant to admit about 1%.
                    // null means the entry never entered the experiment fold (off-stratum under
                    // protein-compact); those keep the pass-1 value, which is correct because
                    // they keep the pass-1 experiment q too - the branch above.
                    double? agg = competition.ExperimentAggregateScore(e.EntryId);
                    if (agg.HasValue)
                        e.ExperimentAggregateScore = agg.Value;
                    nMapped++;
                }
            ctx.LogInfo(string.Format(
                "{0}: mapped recomputed q onto {1} reported survivors ({2} frozen-model scores " +
                "swapped in) in {3:F1}s.",
                mode, nMapped, nScored, sw.Elapsed.TotalSeconds));
            return true;
        }

        /// <summary>
        /// Resident 2nd-pass compute (flag off): the byte-identity oracle. Reload every
        /// survivor's 21-PIN feature vector RESIDENT from each file's reconciled parquet
        /// (keyed by identity via <see cref="LoadReconciledFeaturesByIdentity"/> +
        /// <see cref="MapFeaturesByIdentity"/>), then run the resident FdrEntry
        /// <c>FirstPassFdrTask.RunPercolatorFdr</c> over the full survivor buffer, which
        /// scores it in place. Pure code motion out of <see cref="ComputeAndPersist"/>
        /// -- behavior (and therefore the 2nd-pass sidecars) is unchanged.
        /// </summary>
        private static FeatureContributions ComputePass2Resident(
            PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config)
        {
            // Frozen 2nd-pass (transfer-compete / protein-compact): apply the FROZEN 1st-pass
            // model to the reconciled survivors and recompute q/PEP by a fresh target-decoy
            // competition over the full pre-compaction population (transfer-compete) or the
            // protein stratum (protein-compact) -- NO retrain. ComputePass2TransferCompeteFull
            // STREAMS each file's features to score, so run it FIRST and return on success:
            // pre-loading every survivor's features resident (below) would defeat the memory
            // win. Falls through to the resident retrain only if the frozen model / stratum is
            // absent. (protein-compact + OSPREY_PROTEIN_COMPACT_RETRAIN=1 deliberately skips
            // this and retrains -- the diagnostic A/B lever.)
            if (OspreyEnvironment.Pass2TransferCompete ||
                (OspreyEnvironment.Pass2ProteinCompact && !OspreyEnvironment.Pass2ProteinCompactRetrain))
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
                        ctx, perFileEntries, perFileParquetPaths, config, frozen.Results,
                        frozen.ExperimentAgg, stratum))
                {
                    // Frozen recompute streamed the score pass + wrote q/PEP onto the
                    // survivors; the resident full-feature reload below is skipped.
                    return null;
                }
                // Fail-fast: an explicitly requested frozen mode must NEVER silently degrade to the
                // anti-conservative retrain. Absent inputs (the frozen 1st-pass model / protein
                // stratum are not in this process -- a warm rerun that loaded cached scores and
                // skipped 1st-pass training, or a distributed SecondPassFDR node that never
                // trained pass 1) or a missing/corrupt 1st-pass sidecar mean the flag cannot be
                // honored; abort with actionable guidance rather than reporting looser FDR than a
                // cold straight-through run under the same flag. (protein-compact +
                // OSPREY_PROTEIN_COMPACT_RETRAIN=1 retrains by design and never reaches here.)
                throw new InvalidOperationException(string.Format(
                    "OSPREY_PASS2_QVALUE={0} could not run the frozen recompute (the frozen 1st-pass " +
                    "model, 1st-pass scalar sidecars, or protein stratum are absent -- e.g. a warm " +
                    "rerun or a distributed SecondPassFDR node that did not train pass 1 in-process). Run the " +
                    "frozen modes on the straight-through path, rerun without the score cache, or unset " +
                    "OSPREY_PASS2_QVALUE for the default retrain{1}.",
                    OspreyEnvironment.Pass2QValue,
                    OspreyEnvironment.Pass2ProteinCompact
                        ? ", or set OSPREY_PROTEIN_COMPACT_RETRAIN=1 to retrain over the stratum"
                        : string.Empty));
            }

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
                Dictionary<(uint, byte, uint), double[]> featByIdentity;
                try
                {
                    featByIdentity = LoadReconciledFeaturesByIdentity(effectiveParquetPath);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        "Second-pass FDR: failed to reload PIN features from {0}: {1}",
                        effectiveParquetPath, ex.Message));
                    continue;
                }
                int nMapped = MapFeaturesByIdentity(kvp.Value, featByIdentity);
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
                        kvp.Key, featByIdentity.Count, kvp.Value.Count, kvp.Value.Count - nMapped));
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
        /// RECONCILED parquet row (via <see cref="BuildReconciledIdentityToRow"/>), then
        /// run the projection <c>FirstPassFdrTask.RunPercolatorFdr</c> through an
        /// <see cref="FdrStreamingSink"/>, which ALWAYS streams the reconciled features
        /// per file and streams the q-value outputs straight to the per-file
        /// <c>.2nd-pass.fdr_scores.bin</c> via <paramref name="flushFile"/> (the lean
        /// projection never stores them -> 32 B). <paramref name="resolveProteinQ"/>
        /// supplies each row's <c>RunProteinQvalue</c> (looked up from the resident
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
                kvp.Value.Sort((a, b) => // Array.Sort OK: terminal key ParquetIndex is unique per survivor, so the comparator never ties.
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
            IReadOnlyDictionary<(uint, byte, uint), uint> RowMap(string fileName)
            {
                string recon = Recon(fileName);
                if (recon == null)
                {
                    ctx.LogWarning(string.Format(
                        "Second-pass FDR: no parquet path mapped for file '{0}' " +
                        "(entries will run with basic-feature fallback). " +
                        "Check that each file's reconciled parquet is present.", fileName));
                    return new Dictionary<(uint, byte, uint), uint>();
                }
                try
                {
                    return BuildReconciledIdentityToRow(recon);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        "Second-pass FDR: failed to read identity columns from {0}: {1}",
                        recon, ex.Message));
                    return new Dictionary<(uint, byte, uint), uint>();
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
            // the streamed q-values + the survivor's RunProteinQvalue during the score
            // pass, so the q-values are never stored on the projection (issue #4355 / C1).
            var sink = new FdrStreamingSink(
                projections, config, "Second-pass", resolveProteinQ, flushFile);
            FirstPassFdrTask.RunPercolatorFdr(
                projections, config, ctx, "Second-pass", load2, sink);
            return projections;
        }

        /// <summary>
        /// Build the reconciled parquet's <c>(entry_id, charge, scan_number) -&gt; row</c>
        /// map from its lean stub identity columns
        /// (<see cref="ParquetScoreCache.LoadFdrStubsFromParquet"/>, which assigns
        /// <see cref="FdrEntry.ParquetIndex"/> = row). The mirror of
        /// <see cref="LoadReconciledFeaturesByIdentity"/> that yields the ROW INDEX
        /// instead of the feature vector: that loader keys <c>featRows[i]</c> by identity
        /// and the streaming score pass reads <c>rows[row]</c> by the baked
        /// <see cref="FdrProjection.ParquetIndex"/>, so
        /// <c>rows[map[identity]] == featByIdentity[identity]</c> -- the streamed feature
        /// lookup is byte-identical to the resident identity binding (issue #4374 risk
        /// #2). Because the reconciled parquet is written
        /// <c>(entry_id, charge, scan_number)</c>-sorted, the row is scan-monotonic within
        /// a <c>(entry_id, charge)</c> group, which is what keeps the scan-omitted
        /// projection sort valid. Duplicate identities keep the last row (map overwrite),
        /// matching the loader. Reads only the identity columns (no PIN feature / heavy
        /// blob load), one file at a time.
        /// </summary>
        internal static Dictionary<(uint, byte, uint), uint> BuildReconciledIdentityToRow(
            string reconciledPath)
        {
            var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(reconciledPath);
            var map = new Dictionary<(uint, byte, uint), uint>(stubs.Count);
            for (int i = 0; i < stubs.Count; i++)
            {
                // (uint)i == stubs[i].ParquetIndex (LoadFdrStubsFromParquet sets
                // ParquetIndex = row); use the row so it addresses LoadPinFeaturesFrom
                // Parquet's positional feature rows.
                map[(stubs[i].EntryId, stubs[i].Charge, stubs[i].ScanNumber)] = (uint)i;
            }
            // A duplicate (entry_id, charge, scan_number) identity would collapse two
            // reconciled stubs onto ONE map slot -- but such a collapsed pair is IDENTICAL
            // in the projection (same reconciled row => same features, Score, entry_id, and
            // sidecar record), so the scan-omitted 2nd-pass sort's tie on them is
            // order-irrelevant to the output (nothing downstream reads position, only value).
            // In practice DeduplicatePairs makes entry_id unique per file, so the collision
            // does not arise; either way byte-identity holds (see the "// Array.Sort OK" note
            // on the projection sort in PercolatorEngine.RunPercolatorFdr).
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
        /// invariant across the reindex, so <see cref="MapFeaturesByIdentity"/>
        /// keys on it. Reads the lean stub columns + the PIN feature columns (no
        /// heavy fragment/XIC/CWT blobs), one file at a time, so the reload stays
        /// within the issue #4355 memory bound. (issue #4355)
        /// </summary>
        internal static Dictionary<(uint, byte, uint), double[]> LoadReconciledFeaturesByIdentity(
            string reconciledPath)
        {
            var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(reconciledPath);
            var featRows = ParquetScoreCache.LoadPinFeaturesFromParquet(reconciledPath);
            int n = Math.Min(stubs.Count, featRows.Count);
            var map = new Dictionary<(uint, byte, uint), double[]>(n);
            for (int i = 0; i < n; i++)
                map[(stubs[i].EntryId, stubs[i].Charge, stubs[i].ScanNumber)] = featRows[i];
            return map;
        }

        /// <summary>
        /// Overlay re-scored PIN features onto <paramref name="entries"/> by each
        /// entry's stable identity (entry_id, charge, scan_number), skipping any
        /// entry whose identity is absent from <paramref name="featByIdentity"/> (a
        /// stub/parquet mismatch). Returns the number of entries whose
        /// <see cref="FdrEntry.Features"/> were assigned; the caller compares it
        /// against the entry count to detect and report a mismatch. Identity (not
        /// <see cref="FdrEntry.ParquetIndex"/>) is used because the reconciled
        /// parquet is re-indexed relative to the compacted stubs -- see
        /// <see cref="LoadReconciledFeaturesByIdentity"/>. Pure: no I/O, no logging.
        /// </summary>
        internal static int MapFeaturesByIdentity(
            IReadOnlyList<FdrEntry> entries,
            IReadOnlyDictionary<(uint, byte, uint), double[]> featByIdentity)
        {
            int nMapped = 0;
            foreach (var entry in entries)
            {
                if (featByIdentity.TryGetValue(
                        (entry.EntryId, entry.Charge, entry.ScanNumber), out double[] features))
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
            var globalExpPrecQ = new Dictionary<uint, double>();
            var globalExpPepQ = new Dictionary<uint, double>();
            // The experiment aggregate score belongs with the experiment q above: it is the score
            // that q's competition ranked on, and a gap-fill that takes one without the other
            // persists a q paired with a score it was never computed from - the exact pairing this
            // field exists to guarantee. Reduced by MAX rather than the q-values' MIN because
            // higher is better here, and because 0.0 is FdrEntry.ResetScores' default and sits mid
            // distribution for a signed discriminant, so max also keeps a reset stub from
            // displacing a real negative score.
            var globalExpAgg = new Dictionary<uint, double>();
            // Per-file progress: reading every file's 1st-pass sidecar ran silently for minutes on
            // an 82-file join. Console-only; disposed on every exit (including the fallback return).
            using (var scanProgress = new ProgressReporter(
                string.Format(@"Reading 1st-pass sidecars for cross-file experiment q from {0} file(s)",
                    perFileEntries.Count), perFileEntries.Count))
            {
                int scanIdx = 0;
                foreach (var kvp in perFileEntries)
                {
                    scanProgress.Report(++scanIdx);
                    if (!inputByFileName.TryGetValue(kvp.Key, out string inputFile))
                        continue;
                    string pass1Path = FdrScoresSidecar.Pass1Path(inputFile);
                    bool readOk = FdrScoresSidecar.ReadRecords(
                        pass1Path, FdrScoresSidecar.Pass.FirstPass, rec =>
                    {
                        if (!globalExpPrecQ.TryGetValue(rec.EntryId, out double curPrec) ||
                            rec.ExperimentPrecursorQvalue < curPrec)
                            globalExpPrecQ[rec.EntryId] = rec.ExperimentPrecursorQvalue;
                        if (!globalExpPepQ.TryGetValue(rec.EntryId, out double curPep) ||
                            rec.ExperimentPeptideQvalue < curPep)
                            globalExpPepQ[rec.EntryId] = rec.ExperimentPeptideQvalue;
                        // Prefer a REAL aggregate over the 0.0 ResetScores default, rather than
                        // taking the max - 0.0 sits above 93-99% of measured aggregates, so a max
                        // would let a single default row outrank every real (negative) one for
                        // this entry. Same rule, and the same reasoning, as
                        // CoAssignmentAccumulator.ObserveCutoff; see the comment there for the
                        // measurement. No 1st-pass sidecar record carries a 0.0 today (0 of 24.7M
                        // over six SEA-AD files), so this is prophylactic here and essential
                        // there, where the pass-2 pool does carry stubs.
                        if (!globalExpAgg.TryGetValue(rec.EntryId, out double curAgg) || curAgg == 0.0 ||
                            (rec.ExperimentAggregateScore != 0.0 && rec.ExperimentAggregateScore > curAgg))
                            globalExpAgg[rec.EntryId] = rec.ExperimentAggregateScore;
                    });
                    if (!readOk)
                    {
                        ctx.LogWarning(string.Format(
                            "OSPREY_PASS2_QVALUE=transfer: 1st-pass sidecar for '{0}' is missing or " +
                            "unreadable ({1}); falling back to the 2nd-pass Percolator retrain rather " +
                            "than silently dropping this file's reconciliation-moved peaks.",
                            kvp.Key, pass1Path));
                        return false;
                    }
                }
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
                    // Gap-fill peaks (no 1st-pass record) take the precursor's cross-file pass-1
                    // experiment q, so ClampExperimentQToBestRun (a floor that only raises) lands
                    // them at the precursor's best-run q; a precursor with no record anywhere -> 1.
                    double gapExpPrecQ = globalExpPrecQ.TryGetValue(entry.EntryId, out double gPrec) ? gPrec : 1.0;
                    double gapExpPepQ = globalExpPepQ.TryGetValue(entry.EntryId, out double gPep) ? gPep : 1.0;
                    // 0.0 when the precursor has no record anywhere, which pairs with the q = 1.0
                    // above: never competed, never accepted, so nothing reads it.
                    double gapExpAgg = globalExpAgg.TryGetValue(entry.EntryId, out double gAgg) ? gAgg : 0.0;
                    switch (AssignPerRunQ(entry, newScore, rec1,
                        precScoresDesc, precQDesc, pepScoresDesc, pepQDesc,
                        gapExpPrecQ, gapExpPepQ, gapExpAgg))
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
        /// and the precursor's cross-file pass-1 experiment q (used ONLY for a gap-fill). The
        /// experiment q is NEVER derived from a table -- it is the pass-1 carry, frozen by the
        /// best-peak anchor:
        /// <list type="bullet">
        /// <item>UNCHANGED (<paramref name="newScore"/> == the record's Score, bit-exact): carry the
        /// full 1st-pass record verbatim.</item>
        /// <item>MOVED: run q re-mapped from the tables; experiment q + PEP carried from the record.</item>
        /// <item>GAP-FILL (no record): run q from the tables; experiment q =
        /// <paramref name="gapFillExpPrecQ"/> / <paramref name="gapFillExpPepQ"/>, and the
        /// experiment aggregate score = <paramref name="gapFillExpAgg"/> from the same cross-file
        /// source, so the persisted score and the q it ranked for stay paired.</item>
        /// </list>
        /// </summary>
        internal static PerRunClass AssignPerRunQ(
            FdrEntry entry,
            double newScore,
            FdrScoreRecord? firstPass,
            double[] precScoresDesc,
            double[] precQDesc,
            double[] pepScoresDesc,
            double[] pepQDesc,
            double gapFillExpPrecQ,
            double gapFillExpPepQ,
            double gapFillExpAgg)
        {
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
                    entry.ExperimentPrecursorQvalue = rec1.ExperimentPrecursorQvalue;
                    entry.ExperimentPeptideQvalue = rec1.ExperimentPeptideQvalue;
                    entry.Pep = rec1.Pep;
                    entry.ExperimentAggregateScore = rec1.ExperimentAggregateScore;
                    return PerRunClass.Unchanged;
                }
                entry.Score = newScore;
                entry.RunPrecursorQvalue = LookupQForScore(newScore, precScoresDesc, precQDesc);
                entry.RunPeptideQvalue = LookupQForScore(newScore, pepScoresDesc, pepQDesc);
                // Experiment q is a pass-1 property (best-peak anchor) -- carry it, never re-map.
                entry.ExperimentPrecursorQvalue = rec1.ExperimentPrecursorQvalue;
                entry.ExperimentPeptideQvalue = rec1.ExperimentPeptideQvalue;
                entry.Pep = rec1.Pep;
                // Carried with the experiment q for the same reason, and NOT re-derived from
                // newScore: it is the score that pass-1 experiment q was computed from, so
                // re-mapping it to the rescored value would break the pairing that is the
                // whole point of persisting it.
                entry.ExperimentAggregateScore = rec1.ExperimentAggregateScore;
                return PerRunClass.Moved;
            }
            entry.Score = newScore;
            entry.RunPrecursorQvalue = LookupQForScore(newScore, precScoresDesc, precQDesc);
            entry.RunPeptideQvalue = LookupQForScore(newScore, pepScoresDesc, pepQDesc);
            entry.ExperimentPrecursorQvalue = gapFillExpPrecQ;
            entry.ExperimentPeptideQvalue = gapFillExpPepQ;
            // Carried for the same reason as the experiment q beside it, and from the same
            // cross-file source: the aggregate is a per-entry roll-up, identical in every file's
            // record for that entry, so a gap-fill is entitled to it even with no record of its
            // own. Leaving it at ResetScores' 0.0 would persist a real experiment q next to a
            // score that q was not computed from, and a score-space acceptance boundary built
            // from the 2nd-pass sidecar would then be drawn from the wrong ranking.
            entry.ExperimentAggregateScore = gapFillExpAgg;
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
            public int AlreadyOnDisk;
            public int Failures;
        }
    }
}
