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
    /// The merge-node 2nd-pass FDR sidecar step (Stage 8 input prep, mirrors
    /// Rust pipeline.rs:4394-4494): reload PIN features from the reconciled
    /// parquets, run 2nd-pass Percolator on the post-reconciliation entries,
    /// write the per-file <c>.2nd-pass.fdr_scores.bin</c> sidecars, then reload
    /// those sidecars onto the post-compaction stubs so run-wide protein FDR
    /// sees the 2nd-pass q-values rather than the stale 1st-pass values.
    ///
    /// Extracted verbatim from <see cref="MergeNodeTask.Run"/> as pure code
    /// motion so that method reads as a sequencer; behavior (and therefore the
    /// 2nd-pass sidecars and downstream protein-FDR / blib output) is unchanged.
    /// The parity-locked 2nd-pass scoring core (<c>FirstJoinTask.RunPercolatorFdr</c>)
    /// is invoked whole through the
    /// live <see cref="PipelineContext"/>; it is not decomposed here.
    /// </summary>
    internal static class Pass2FdrSidecar
    {
        /// <summary>
        /// Run the 2nd-pass FDR / sidecar persistence step for the merge node.
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
            // Log the active mode once so a run's provenance is in the log; warn on an
            // unrecognized token (normalized to the parity-preserving percolator default).
            if (OspreyEnvironment.Pass2QValueUnrecognized)
            {
                ctx.LogWarning(string.Format(
                    "OSPREY_PASS2_QVALUE was set to an unrecognized value; using the default " +
                    "'{0}'. Recognized modes: '{0}', '{1}'.",
                    OspreyEnvironment.PASS2_QVALUE_PERCOLATOR, OspreyEnvironment.PASS2_QVALUE_TRANSFER));
            }
            if (OspreyEnvironment.Pass2TransferQ)
            {
                ctx.LogInfo(string.Format(
                    "OSPREY_PASS2_QVALUE={0}: 2nd-pass q comes from the frozen 1st-pass model + " +
                    "full pre-compaction score->q table (confidence transfer), not a reduced-pool retrain.",
                    OspreyEnvironment.PASS2_QVALUE_TRANSFER));
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
            // FirstJoinTask). Without this 2nd-pass run, protein
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
                    if (!File.Exists(FdrScoresSidecar.Pass2Path(probeInput)))
                        missingPass2++;
                }
                if (missingPass2 > 0)
                {
                    ctx.LogVerbose(string.Format(
                        "{0}/{1} file(s) have no precomputed second-pass FDR scores -- computing " +
                        "them here from the reconciled features (reused distributed-run code path).",
                        missingPass2, totalFiles));
                    var swPass2 = Stopwatch.StartNew();
                    // --model-diagnostics needs the resident 2nd-pass model: its feature
                    // contributions feed the pass-2 model view, and the projection 2nd pass
                    // streams through a sink and produces none. Route --model-diagnostics to
                    // the resident path so ComputePass2Resident can return the model. Off the
                    // default output path, so byte-identity is unaffected (#4377).
                    // OSPREY_PASS2_QVALUE=transfer also takes the resident path: the transfer
                    // needs each survivor's RECONCILED features loaded onto entry.Features (which
                    // ComputePass2Resident does) so the frozen 1st-pass model can re-score them.
                    // The projection path streams features to a sink and never lands them resident.
                    if (OspreyEnvironment.UseFdrProjection && config.FdrMethod == FdrMethod.Percolator &&
                        !config.ModelDiagnostics && !OspreyEnvironment.Pass2TransferQ)
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
                            if (File.Exists(pass2PathFlush))
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
                    foreach (var kvp in perFileEntries)
                    {
                        string fileName = kvp.Key;
                        if (!inputByFileName.TryGetValue(fileName, out string inputFile3))
                            continue;
                        string pass2Path = FdrScoresSidecar.Pass2Path(inputFile3);
                        if (File.Exists(pass2Path))
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
                foreach (var kvp in perFileEntries)
                {
                    if (!inputByName.TryGetValue(kvp.Key, out string inputFile4))
                        continue;
                    string pass2Path = FdrScoresSidecar.Pass2Path(inputFile4);
                    if (!File.Exists(pass2Path))
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
        /// Resident 2nd-pass compute (flag off): the byte-identity oracle. Reload every
        /// survivor's 21-PIN feature vector RESIDENT from each file's reconciled parquet
        /// (keyed by identity via <see cref="LoadReconciledFeaturesByIdentity"/> +
        /// <see cref="MapFeaturesByIdentity"/>), then run the resident FdrEntry
        /// <c>FirstJoinTask.RunPercolatorFdr</c> over the full survivor buffer, which
        /// scores it in place. Pure code motion out of <see cref="ComputeAndPersist"/>
        /// -- behavior (and therefore the 2nd-pass sidecars) is unchanged.
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
            foreach (var kvp in perFileEntries)
            {
                if (!perFileParquetPaths.TryGetValue(kvp.Key, out string parquetPath))
                {
                    // No first-join parquet was produced (or mapped) for this
                    // file. The {0} entries below will go into the second-pass
                    // Percolator with stale / null Features, which silently
                    // regresses 2nd-pass FDR -- log so the operator can detect
                    // an incomplete first-join hand-off.
                    ctx.LogWarning(string.Format(
                        "Second-pass FDR: no parquet path mapped for file '{0}' " +
                        "({1} entries will run with stale/null features). " +
                        "Check first-join output completeness.",
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
                // parquet is a stub/parquet mismatch (e.g., the first-join
                // parquet was regenerated with fewer rows than the in-memory
                // FDR stubs reference). Such entries silently keep their stale
                // Features and corrupt 2nd-pass FDR; warn so the mismatch
                // is visible.
                if (nMapped < kvp.Value.Count)
                {
                    ctx.LogWarning(string.Format(
                        "Second-pass FDR: file '{0}' reconciled parquet has {1} feature rows " +
                        "but {2} FDR entries reference it; {3} entries will run with " +
                        "stale/null features. Stub/parquet mismatch -- check first-join " +
                        "output integrity.",
                        kvp.Key, featByIdentity.Count, kvp.Value.Count, kvp.Value.Count - nMapped));
                }
                nReloaded += nMapped;
            }
            swReloadFeats.Stop();
            ctx.LogInfo(string.Format(
                "[TIMING] Reloaded PIN features for {0} entries: {1:F1}s",
                nReloaded, swReloadFeats.Elapsed.TotalSeconds));

            switch (config.FdrMethod)
            {
                case FdrMethod.Percolator:
                    // OSPREY_PASS2_QVALUE=transfer: instead of retraining a 2nd-pass SVM on
                    // the decoy-depleted reconciled+compacted set, apply the FROZEN 1st-pass
                    // model to each entry's RECONCILED features (loaded onto entry.Features
                    // above) and map the resulting score to a q via the full pre-compaction
                    // 1st-pass score->q table. Falls through to the retrain if the flag is
                    // off or the frozen-model / table byproducts were not captured.
                    if (OspreyEnvironment.Pass2TransferQ &&
                        ctx.TryGet<FirstPassPercolatorModel>(out var frozenModel) &&
                        frozenModel?.Results != null &&
                        TransferQFromFrozenModel(perFileEntries, ctx, frozenModel.Results))
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
                            "model or full-population score->q table byproduct absent); falling " +
                            "back to the 2nd-pass Percolator retrain.");
                    }
                    // Capture the 2nd-pass model for the --model-diagnostics pass-2 model
                    // view (retrained on the post-reconciliation pool, #4377). Capturing
                    // the return value does not change what RunPercolatorFdr does, so the
                    // resident 2nd-pass scores stay byte-identical.
                    return FirstJoinTask.RunPercolatorFdr(
                        perFileEntries, config, ctx, "Second-pass");
                // Simple / Mokapot 2nd-pass paths intentionally
                // not implemented yet -- the in-process pipeline's
                // FirstJoinTask.RunFdr already covers Simple, and
                // Mokapot is not used in Osprey's current
                // scope. If those become relevant for an HPC chain,
                // mirror the Rust dispatch in pipeline.rs:4424-4448.
                default:
                    ctx.LogWarning(string.Format(
                        "Second-pass FDR: {0} is not supported in MergeNodeTask; " +
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
        /// run the projection <c>FirstJoinTask.RunPercolatorFdr</c> through an
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
                        "Check first-join output completeness.", fileName));
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
                        "fallback. Stub/parquet mismatch -- check first-join output integrity.",
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
            FirstJoinTask.RunPercolatorFdr(
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
        /// OSPREY_PASS2_QVALUE=transfer. Instead of retraining a 2nd-pass Percolator SVM
        /// on the decoy-depleted reconciled+compacted set -- which underestimates q
        /// anti-conservatively -- apply the FROZEN 1st-pass model
        /// (<paramref name="firstPassModel"/>: averaged fold weights + biases +
        /// standardizer) to each entry's RECONCILED features (already on
        /// <see cref="FdrEntry.Features"/>) and map the resulting score to a q via the
        /// FULL pre-compaction 1st-pass score-&gt;q table (the
        /// <see cref="FirstPassScoreQTable"/> byproduct built by
        /// <see cref="BuildFullPopulationScoreQTable"/> at first-pass time). This keeps
        /// the reconciliation peak-move (a peak moved to a worse position maps through the
        /// unbiased table to an honestly higher q) while discarding the retrain's
        /// decoy-depleted null (co-monotonic confidence transfer; Rost 2016 TRIC).
        /// Returns false (caller falls back to the retrain) when the model is unusable or
        /// the full-population table byproduct is absent (e.g. a resume path that did not
        /// run the 1st pass in this process).
        /// </summary>
        internal static bool TransferQFromFrozenModel(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            PipelineContext ctx,
            PercolatorResults firstPassModel)
        {
            if (firstPassModel.FoldWeights == null || firstPassModel.FoldWeights.Count == 0 ||
                firstPassModel.Standardizer == null)
            {
                ctx.LogWarning(
                    "OSPREY_PASS2_QVALUE=transfer: frozen 1st-pass model has no fold weights " +
                    "or standardizer; cannot transfer.");
                return false;
            }
            if (!ctx.TryGet<FirstPassScoreQTable>(out var fullTable) ||
                fullTable?.ScoresDesc == null || fullTable.ScoresDesc.Length == 0)
            {
                ctx.LogWarning(
                    "OSPREY_PASS2_QVALUE=transfer: no FULL-population score->q table byproduct " +
                    "present (the 1st pass did not build one in this process); cannot transfer.");
                return false;
            }
            // ScoresDesc and QDesc are built and published together (always parallel), but
            // guard against a partially-populated byproduct so the logging + lookup below
            // never index a null or mismatched QDesc.
            if (fullTable.QDesc == null || fullTable.QDesc.Length != fullTable.ScoresDesc.Length)
            {
                ctx.LogWarning(
                    "OSPREY_PASS2_QVALUE=transfer: score->q table byproduct has a null or " +
                    "length-mismatched QDesc; cannot transfer.");
                return false;
            }

            AverageFoldModel(firstPassModel, out double[] avgWeights, out double avgBias);
            int nFeatures = avgWeights.Length;

            ctx.LogInfo(string.Format(
                "OSPREY_PASS2_QVALUE=transfer: using FULL-population score->q table ({0} points, " +
                "raw-score range [{1:F4}, {2:F4}], q range [{3:E3}, {4:E3}]) from the " +
                "pre-compaction 1st-pass null.",
                fullTable.ScoresDesc.Length,
                fullTable.ScoresDesc[fullTable.ScoresDesc.Length - 1], fullTable.ScoresDesc[0],
                fullTable.QDesc[0], fullTable.QDesc[fullTable.QDesc.Length - 1]));

            TransferQWithTable(
                perFileEntries, firstPassModel.Standardizer, avgWeights, avgBias, nFeatures,
                fullTable.ScoresDesc, fullTable.QDesc, ctx);
            return true;
        }

        /// <summary>
        /// Build the FULL 1st-pass-population score-&gt;q table at first-pass FDR time
        /// (called from <see cref="FirstJoinTask"/> BEFORE compaction, while
        /// <paramref name="perFileEntries"/> still holds every entry -- passing, failing,
        /// target, and decoy -- with its in-memory Stage-4 <see cref="FdrEntry.Features"/>
        /// and unbiased 1st-pass q-values). Sourcing the table from the full pre-compaction
        /// null preserves the high-q failing/decoy tail so a peak that reconciliation moved
        /// to a worse position maps to an honestly higher q -- the tail the decoy-depleted
        /// compacted pool lacks. Each entry's key is the SAME raw averaged-model score the
        /// transfer uses (<see cref="ScoreWithFrozenModel"/>), NOT the stored per-fold
        /// recalibrated <see cref="FdrEntry.Score"/>, so table and transfer stay on one
        /// scale by construction. Paired q is the entry's effective experiment q (max of
        /// precursor + peptide). Returns null (logged) when the frozen model is unusable so
        /// the caller publishes nothing and the transfer falls back to the retrain.
        /// </summary>
        internal static FirstPassScoreQTable BuildFullPopulationScoreQTable(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            PercolatorResults firstPassModel,
            PipelineContext ctx)
        {
            if (firstPassModel.FoldWeights == null || firstPassModel.FoldWeights.Count == 0 ||
                firstPassModel.Standardizer == null)
            {
                ctx.LogWarning(
                    "OSPREY_PASS2_QVALUE=transfer: cannot build full-population score->q table -- " +
                    "frozen 1st-pass model has no fold weights or standardizer.");
                return null;
            }
            AverageFoldModel(firstPassModel, out double[] avgWeights, out double avgBias);
            int nFeatures = avgWeights.Length;
            var standardizer = firstPassModel.Standardizer;

            // Score every entry that still carries its in-memory Stage-4 features (the full
            // pre-compaction population), paired with its unbiased 1st-pass effective q.
            var tableScores = new List<double>();
            var tableQs = new List<double>();
            int nSkipped = 0;
            var scratch = new double[nFeatures]; // reused per entry to avoid a per-row allocation
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (entry.Features == null || entry.Features.Length != nFeatures)
                    {
                        nSkipped++;
                        continue;
                    }
                    double rawScore = ScoreWithFrozenModel(
                        entry.Features, standardizer, avgWeights, avgBias, scratch);
                    double effQ = Math.Max(
                        entry.ExperimentPrecursorQvalue, entry.ExperimentPeptideQvalue);
                    tableScores.Add(rawScore);
                    tableQs.Add(effQ);
                }
            }
            if (tableScores.Count == 0)
            {
                ctx.LogWarning(
                    "OSPREY_PASS2_QVALUE=transfer: full-population score->q table build found no " +
                    "entries with in-memory features; table not published (transfer will fall back).");
                return null;
            }

            BuildScoreToQTable(tableScores, tableQs, out double[] scoresDesc, out double[] qDesc);
            ctx.LogInfo(string.Format(
                "OSPREY_PASS2_QVALUE=transfer: built FULL-population score->q table from {0} entries " +
                "({1} skipped for missing features; raw-score range [{2:F4}, {3:F4}]; q range [{4:E3}, {5:E3}]).",
                scoresDesc.Length, nSkipped,
                scoresDesc[scoresDesc.Length - 1], scoresDesc[0],
                qDesc[0], qDesc[qDesc.Length - 1]));
            return new FirstPassScoreQTable { ScoresDesc = scoresDesc, QDesc = qDesc };
        }

        /// <summary>
        /// Apply the averaged frozen model to a single raw feature vector: standardize a
        /// copy into the caller-supplied <paramref name="scratch"/> buffer, then
        /// score = avgBias + sum(avgWeights[j] * std(feat)[j]). Mirrors the per-entry math
        /// in <c>PercolatorFdr.ScorePopulationAndComputeFdr</c>, which likewise reuses a
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
        /// pair -- the same averaged-model math <c>PercolatorFdr.ScorePopulationAndComputeFdr</c>
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
        /// Apply the frozen 1st-pass model to each entry's RECONCILED features (already on
        /// <see cref="FdrEntry.Features"/>), overwrite <see cref="FdrEntry.Score"/> with the
        /// raw averaged-model score, and map that score to a q via the supplied score-&gt;q
        /// table (<paramref name="scoresDesc"/> / <paramref name="qDesc"/>). Sets both
        /// precursor + peptide levels (run + experiment) so the downstream effective-q
        /// filtering (detected-peptides gate) sees the transferred q.
        /// </summary>
        private static void TransferQWithTable(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            FeatureStandardizer standardizer,
            double[] avgWeights,
            double avgBias,
            int nFeatures,
            double[] scoresDesc,
            double[] qDesc,
            PipelineContext ctx)
        {
            int nScored = 0;
            int nSkipped = 0;
            var scratch = new double[nFeatures]; // reused per entry to avoid a per-row allocation
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (entry.Features == null || entry.Features.Length != nFeatures)
                    {
                        nSkipped++;
                        continue;
                    }
                    double newScore = ScoreWithFrozenModel(
                        entry.Features, standardizer, avgWeights, avgBias, scratch);
                    entry.Score = newScore;
                    double q = LookupQForScore(newScore, scoresDesc, qDesc);
                    // Assign one transferred q to all four q slots (precursor/peptide x
                    // run/experiment). This is the coarse whole-pool transfer: it collapses
                    // the precursor-vs-peptide and run-vs-experiment distinctions the
                    // percolator path maintains, so the reported "experiment q" here is a
                    // per-observation transferred value, not a genuine across-file estimate.
                    // The downstream best-of-runs clamp (MergeNodeTask) is then a no-op on
                    // these equal values. Acceptable for this experimental mode; Part B's
                    // surgical transfer (Design 1) preserves the survivors' calibrated q
                    // per level instead. See TODO-20260710_osprey_pass2_recalibration_fix.
                    entry.ExperimentPrecursorQvalue = q;
                    entry.ExperimentPeptideQvalue = q;
                    entry.RunPrecursorQvalue = q;
                    entry.RunPeptideQvalue = q;
                    nScored++;
                }
            }
            ctx.LogInfo(string.Format(
                "OSPREY_PASS2_QVALUE=transfer: re-scored {0} entries with the FROZEN 1st-pass model " +
                "on reconciled features ({1} skipped for missing features); q transferred via the " +
                "score->q table.", nScored, nSkipped));
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
