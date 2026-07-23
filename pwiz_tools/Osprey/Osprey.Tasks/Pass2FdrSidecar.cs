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
                    "OSPREY_PASS2_QVALUE={0}: pass-2 carries the pass-1 q through and re-maps ONLY the " +
                    "per-run q of reconciliation-moved peaks (frozen 1st-pass model + each file's own " +
                    "score->run-q table); experiment q is frozen by the best-peak anchor, no retrain.",
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
        /// OSPREY_PASS2_QVALUE=transfer-compete (full-population form). Recompute the reported
        /// precursor q-values + PEP by re-running the target-decoy competition over the ENTIRE
        /// 1st-pass population -- read as SCALARS from each file's persisted
        /// <c>.1st-pass.fdr_scores.bin</c> -- with ONLY the reconciled survivors' scores swapped
        /// in (the FROZEN 1st-pass model applied to their reconciled features). Because &gt;99% of
        /// scores are unchanged, the recomputed q lands on the calibrated 1st-pass value; the
        /// reconciled minority get honest full-population q. No 2nd-pass retrain and no
        /// reduced-pool null (the null is the full 1st-pass decoy set). No features are held
        /// resident -- only flat scalar arrays. Writes q/PEP onto the reported survivor entries in
        /// place. Returns false (caller falls back to the retrain) when the frozen model or any
        /// 1st-pass scalar sidecar is missing.
        /// </summary>
        private static bool ComputePass2TransferCompeteFull(
            PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            PercolatorResults frozenModel,
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
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int nFeatures = scorer.NumFeatures;

            // 1. Frozen-model score for each reconciled survivor, STREAMED one file at a
            //    time: load that file's reconciled PIN features, score with the frozen 1st-pass
            //    weights, keep only the scalar score, and release the features before the next
            //    file. The ~300k paired targets+decoys are never all resident -- peak memory is
            //    one file's features (flat in file count, <= the retrain's streaming ingest).
            //    Uses the SAME loader + identity key the resident reload used
            //    (LoadReconciledFeaturesByIdentity keyed by (EntryId,Charge,ScanNumber), the
            //    MapFeaturesByIdentity key), so each survivor's score is byte-identical to the
            //    old resident path. Keyed by (file, entry_id); entry_id is unique per file.
            var survivorScore = new Dictionary<(string, uint), double>();
            foreach (var kvp in perFileEntries)
            {
                if (!perFileParquetPaths.TryGetValue(kvp.Key, out string scoreParquetPath))
                    continue;
                string effectiveParquetPath =
                    ParquetScoreCache.EffectiveScoresPathFromScoresPath(scoreParquetPath);
                Dictionary<(uint, byte, uint), double[]> featByIdentity;
                try
                {
                    featByIdentity = LoadReconciledFeaturesByIdentity(effectiveParquetPath);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        "{0}: failed to reload PIN features from {1}: {2}",
                        mode, effectiveParquetPath, ex.Message));
                    continue;
                }
                foreach (var e in kvp.Value)
                {
                    if (featByIdentity.TryGetValue(
                            (e.EntryId, e.Charge, e.ScanNumber), out double[] feats) &&
                        feats != null && feats.Length == nFeatures)
                    {
                        survivorScore[(kvp.Key, e.EntryId)] = scorer.Score(feats);
                    }
                }
                // featByIdentity released here (one file resident at a time).
            }

            // 2. Reported survivors to emit (every post-reconciliation entry) + per-file scalar
            //    sidecar paths. Validate every sidecar up front so we fail fast (and fall back to
            //    the retrain) before streaming any file.
            var survivors = new List<(string, uint)>();
            foreach (var kvp in perFileEntries)
                foreach (var e in kvp.Value)
                    survivors.Add((kvp.Key, e.EntryId));

            var fileKeys = new List<string>(perFileEntries.Count);
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
                fileKeys.Add(kvp.Key);
                sidecarByKey[kvp.Key] = sidecarPath;
            }

            ctx.LogInfo(string.Format(
                "OSPREY_PASS2_QVALUE={0}: recomputing q/PEP by streaming {1} file(s), frozen-model " +
                "scores swapped in for {2} reconciled survivors -- no retrain, one file resident at a " +
                "time{3}.",
                mode, fileKeys.Count, survivorScore.Count,
                proteinCompact ? ", competition CONSTRAINED to the " + stratumBaseIds.Count + "-base_id protein stratum"
                               : ", full-population null"));

            // 3. Streamed full-population competition + run/experiment precursor q + PEP. Only one
            //    file's scalars are resident at a time; the cross-file state is bounded by the
            //    number of distinct precursors, not the total observation count -- so peak memory
            //    is flat in file count (the 32/64 GB many-file target).
            (uint[] entryIds, double[] scores) ReadFile(string fileKey)
            {
                FdrScoresSidecar.ReadScalars(sidecarByKey[fileKey], out uint[] eids, out double[] scs);
                return (eids, scs);
            }

            PercolatorFdr.ComputeFullPopulationPrecursorFdrStreaming(
                fileKeys, ReadFile, survivorScore, survivors,
                out var runQ, out var expQ, out var pep, stratumBaseIds);

            // 4. Map the recomputed q/PEP back onto the reported survivor entries. Under
            //    protein-compact, an OFF-stratum survivor got q=1.0 from the (constrained)
            //    competition -- skip it so it KEEPS its already-passing 1st-pass q rather
            //    than being dropped (report = pass1 U stratum passers).
            int nMapped = 0;
            foreach (var kvp in perFileEntries)
                foreach (var e in kvp.Value)
                {
                    if (proteinCompact && !stratumBaseIds.Contains(e.EntryId & 0x7FFFFFFFu))
                        continue;
                    var key = (kvp.Key, e.EntryId);
                    if (!runQ.TryGetValue(key, out double rq))
                        continue;
                    e.RunPrecursorQvalue = rq;
                    e.ExperimentPrecursorQvalue = expQ[key];
                    e.Pep = pep[key];
                    // Precursor-level path: keep peptide q in step with precursor q for the
                    // reported set (peptide-level FDR is not the target here).
                    e.RunPeptideQvalue = rq;
                    e.ExperimentPeptideQvalue = expQ[key];
                    nMapped++;
                }
            ctx.LogInfo(string.Format(
                "{0}: mapped recomputed q onto {1} reported survivors in {2:F1}s.",
                mode, nMapped, sw.Elapsed.TotalSeconds));
            return true;
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
                        ctx, perFileEntries, perFileParquetPaths, config, frozen.Results, stratum))
                {
                    // Frozen recompute streamed the score pass + wrote q/PEP onto the
                    // survivors; the resident full-feature reload below is skipped.
                    return null;
                }
                // Fail-fast: an explicitly requested frozen mode must NEVER silently degrade to the
                // anti-conservative retrain. Absent inputs (the frozen 1st-pass model / protein
                // stratum are not in this process -- a warm rerun that loaded cached scores and
                // skipped 1st-pass training, or a distributed SecondPassFDR merge node that never
                // trained pass 1) or a missing/corrupt 1st-pass sidecar mean the flag cannot be
                // honored; abort with actionable guidance rather than reporting looser FDR than a
                // cold straight-through run under the same flag. (protein-compact +
                // OSPREY_PROTEIN_COMPACT_RETRAIN=1 retrains by design and never reaches here.)
                throw new InvalidOperationException(string.Format(
                    "OSPREY_PASS2_QVALUE={0} could not run the frozen recompute (the frozen 1st-pass " +
                    "model, 1st-pass scalar sidecars, or protein stratum are absent -- e.g. a warm " +
                    "rerun or a distributed merge node that did not train pass 1 in-process). Run the " +
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
                    switch (AssignPerRunQ(entry, newScore, rec1,
                        precScoresDesc, precQDesc, pepScoresDesc, pepQDesc, gapExpPrecQ, gapExpPepQ))
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
        /// <paramref name="gapFillExpPrecQ"/> / <paramref name="gapFillExpPepQ"/>.</item>
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
            double gapFillExpPepQ)
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
                    return PerRunClass.Unchanged;
                }
                entry.Score = newScore;
                entry.RunPrecursorQvalue = LookupQForScore(newScore, precScoresDesc, precQDesc);
                entry.RunPeptideQvalue = LookupQForScore(newScore, pepScoresDesc, pepQDesc);
                // Experiment q is a pass-1 property (best-peak anchor) -- carry it, never re-map.
                entry.ExperimentPrecursorQvalue = rec1.ExperimentPrecursorQvalue;
                entry.ExperimentPeptideQvalue = rec1.ExperimentPeptideQvalue;
                entry.Pep = rec1.Pep;
                return PerRunClass.Moved;
            }
            entry.Score = newScore;
            entry.RunPrecursorQvalue = LookupQForScore(newScore, precScoresDesc, precQDesc);
            entry.RunPeptideQvalue = LookupQForScore(newScore, pepScoresDesc, pepQDesc);
            entry.ExperimentPrecursorQvalue = gapFillExpPrecQ;
            entry.ExperimentPeptideQvalue = gapFillExpPepQ;
            return PerRunClass.GapFill;
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
