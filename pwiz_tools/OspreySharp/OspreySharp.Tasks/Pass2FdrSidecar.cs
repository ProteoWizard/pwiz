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
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp.Tasks
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
    /// The parity-locked 2nd-pass scoring core (<see
    /// cref="FirstJoinTask.RunPercolatorFdr"/>) is invoked whole through the
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
        internal static void ComputeAndPersist(
            PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            string taskName,
            string taskValidityKey)
        {
            var config = ctx.Config;

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
                        List<double[]> featRows;
                        try
                        {
                            featRows = ParquetScoreCache.LoadPinFeaturesFromParquet(effectiveParquetPath);
                        }
                        catch (Exception ex)
                        {
                            ctx.LogWarning(string.Format(
                                "Second-pass FDR: failed to reload PIN features from {0}: {1}",
                                effectiveParquetPath, ex.Message));
                            continue;
                        }
                        int nMapped = MapFeaturesByParquetIndex(kvp.Value, featRows);
                        // An entry whose ParquetIndex lies past the loaded row count
                        // is a stub/parquet mismatch (e.g., the first-join parquet
                        // was regenerated with fewer rows than the in-memory FDR
                        // stubs reference). Such entries silently keep their stale
                        // Features and corrupt 2nd-pass FDR; warn so the mismatch
                        // is visible.
                        if (nMapped < kvp.Value.Count)
                        {
                            ctx.LogWarning(string.Format(
                                "Second-pass FDR: file '{0}' parquet has {1} feature rows " +
                                "but {2} FDR entries reference it; {3} entries will run with " +
                                "stale/null features. Stub/parquet mismatch -- check first-join " +
                                "output integrity.",
                                kvp.Key, featRows.Count, kvp.Value.Count, kvp.Value.Count - nMapped));
                        }
                        nReloaded += nMapped;
                    }
                    swReloadFeats.Stop();
                    ctx.LogInfo(string.Format(
                        "[TIMING] Reloaded PIN features for {0} entries: {1:F1}s",
                        nReloaded, swReloadFeats.Elapsed.TotalSeconds));

                    var swPass2 = Stopwatch.StartNew();
                    switch (config.FdrMethod)
                    {
                        case FdrMethod.Percolator:
                            FirstJoinTask.RunPercolatorFdr(
                                perFileEntries, config, ctx, "Second-pass");
                            break;
                        // Simple / Mokapot 2nd-pass paths intentionally
                        // not implemented yet -- the in-process pipeline's
                        // FirstJoinTask.RunFdr already covers Simple, and
                        // Mokapot is not used in OspreySharp's current
                        // scope. If those become relevant for an HPC chain,
                        // mirror the Rust dispatch in pipeline.rs:4424-4448.
                        default:
                            ctx.LogWarning(string.Format(
                                "Second-pass FDR: {0} is not supported in MergeNodeTask; " +
                                "skipping (protein FDR will run on first-pass scores)",
                                config.FdrMethod));
                            break;
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

                int pass2Failures = 0;
                int pass2Written = 0;
                int pass2AlreadyOnDisk = 0;
                foreach (var kvp in perFileEntries)
                {
                    string fileName = kvp.Key;
                    if (!inputByFileName.TryGetValue(fileName, out string inputFile3))
                        continue;
                    string pass2Path = FdrScoresSidecar.Pass2Path(inputFile3);
                    if (File.Exists(pass2Path))
                    {
                        pass2AlreadyOnDisk++;
                        continue;
                    }
                    try
                    {
                        FdrScoresSidecar.Write(
                            pass2Path,
                            kvp.Value, FdrScoresSidecar.Pass.SecondPass);
                        pass2Written++;
                    }
                    catch (Exception ex)
                    {
                        ctx.LogWarning(string.Format(
                            @"Failed to write 2nd-pass FDR sidecar for {0}: {1}",
                            fileName, ex.Message));
                        pass2Failures++;
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
                if (pass2Failures == 0 && pass2Written > 0)
                {
                    ctx.LogVerbose(string.Format(
                        @"Wrote 2nd-pass FDR scores for {0} file(s){1}",
                        pass2Written,
                        pass2AlreadyOnDisk > 0
                            ? string.Format(@" ({0} already on disk; skipped)", pass2AlreadyOnDisk)
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
        }

        /// <summary>
        /// Overlay re-scored PIN features onto <paramref name="entries"/> by
        /// each entry's <see cref="FdrEntry.ParquetIndex"/>, skipping any entry
        /// whose index is out of range for <paramref name="featRows"/> (a
        /// stub/parquet mismatch -- e.g. the first-join parquet was regenerated
        /// with fewer rows than the in-memory FDR stubs reference). Returns the
        /// number of entries whose <see cref="FdrEntry.Features"/> were assigned;
        /// the caller compares it against the entry count to detect and report a
        /// mismatch. Pure: no I/O, no logging.
        /// </summary>
        internal static int MapFeaturesByParquetIndex(
            IReadOnlyList<FdrEntry> entries, IReadOnlyList<double[]> featRows)
        {
            int nMapped = 0;
            foreach (var entry in entries)
            {
                int idx = (int)entry.ParquetIndex;
                if (idx >= 0 && idx < featRows.Count)
                {
                    entry.Features = featRows[idx];
                    nMapped++;
                }
            }
            return nMapped;
        }
    }
}
