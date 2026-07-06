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
using pwiz.Osprey.IO;

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
        internal static void ComputeAndPersist(
            PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            string taskName,
            string taskValidityKey)
        {
            var config = ctx.Config;

            // When the projection 2nd-pass compute ran (flag on), this holds the scored
            // FdrProjectionSet so the sidecar-write block below persists it (in the
            // projection's sorted per-file order) instead of the resident survivor
            // buffer -- the survivor buffer is NOT scored on the projection path; the
            // entry_id overlay carries the 2nd-pass q-values onto it afterward. Null on
            // the resident path (flag off) and on the skip / resume path. (#4374)
            FdrProjectionSet pass2Projections = null;

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
                    if (OspreyEnvironment.UseFdrProjection && config.FdrMethod == FdrMethod.Percolator)
                    {
                        // Projection 2nd pass (issue #4374): stream the reconciled PIN
                        // features through the SAME projection engine the 1st pass uses,
                        // rather than loading every survivor's 21-feature vector resident.
                        // Build the reconciled-row-indexed projection (each survivor's
                        // ParquetIndex baked to its reconciled-parquet row via the
                        // identity->row resolver), then run the projection Percolator,
                        // which ALWAYS streams features per file. The sidecar-write block
                        // below persists the projection rows and the existing entry_id
                        // overlay carries the 2nd-pass q-values onto the resident survivor
                        // buffer -- which therefore never holds features and never builds
                        // a full-population PercolatorEntry/PercolatorResult stack.
                        pass2Projections = ComputePass2Projection(
                            ctx, perFileEntries, perFileParquetPaths, config);
                    }
                    else
                    {
                        // Resident 2nd pass (flag off): the byte-identity oracle. Reload
                        // every survivor's PIN features resident, then run the resident
                        // Percolator over the full FdrEntry survivor buffer.
                        ComputePass2Resident(ctx, perFileEntries, perFileParquetPaths, config);
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

                // When the projection path (flag on) computed the 2nd pass, index its
                // scored per-file rows by file name so each sidecar is written from the
                // projection -- the survivor buffer kvp.Value is NOT scored on that path.
                // Empty on the resident / resume path, so the fallback below writes the
                // resident buffer exactly as before. (#4374)
                var projRowsByFile = new Dictionary<string, List<FdrProjection>>(StringComparer.Ordinal);
                if (pass2Projections != null)
                {
                    foreach (var kvp in pass2Projections.PerFile)
                        projRowsByFile[kvp.Key] = kvp.Value;
                }

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
                        // Projection rows carry the 2nd-pass Score + q-values + the
                        // carried-through RunProteinQvalue in the projection's sorted
                        // per-file order -- byte-identical to the resident path's
                        // post-sort survivor order (both scan-monotonic). Fall back to the
                        // resident survivor buffer when no projection was built.
                        if (projRowsByFile.TryGetValue(fileName, out var projRows))
                        {
                            FdrScoresSidecar.Write(
                                pass2Path, projRows, FdrScoresSidecar.Pass.SecondPass);
                        }
                        else
                        {
                            FdrScoresSidecar.Write(
                                pass2Path, kvp.Value, FdrScoresSidecar.Pass.SecondPass);
                        }
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
        /// Resident 2nd-pass compute (flag off): the byte-identity oracle. Reload every
        /// survivor's 21-PIN feature vector RESIDENT from each file's reconciled parquet
        /// (keyed by identity via <see cref="LoadReconciledFeaturesByIdentity"/> +
        /// <see cref="MapFeaturesByIdentity"/>), then run the resident FdrEntry
        /// <c>FirstJoinTask.RunPercolatorFdr</c> over the full survivor buffer, which
        /// scores it in place. Pure code motion out of <see cref="ComputeAndPersist"/>
        /// -- behavior (and therefore the 2nd-pass sidecars) is unchanged.
        /// </summary>
        private static void ComputePass2Resident(
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
                    FirstJoinTask.RunPercolatorFdr(
                        perFileEntries, config, ctx, "Second-pass");
                    break;
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
                    break;
            }
        }

        /// <summary>
        /// Projection 2nd-pass compute (flag on, issue #4374): build the thin
        /// <see cref="FdrProjectionSet"/> from the survivor buffer with each row's
        /// <see cref="FdrProjection.ParquetIndex"/> baked to that survivor's RECONCILED
        /// parquet row (via <see cref="BuildReconciledIdentityToRow"/>), then run the
        /// projection <c>FirstJoinTask.RunPercolatorFdr</c>, which ALWAYS streams the
        /// reconciled features per file. Returns the scored projection so the caller
        /// writes the 2nd-pass sidecars from it; the survivor buffer is intentionally
        /// left unscored (the entry_id overlay carries the q-values back). The reconciled
        /// features are never held resident and no full-population
        /// PercolatorEntry/PercolatorResult stack is built -- the memory win over the
        /// resident path.
        /// </summary>
        private static FdrProjectionSet ComputePass2Projection(
            PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config)
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
                kvp.Value.Sort((a, b) =>
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
            // ever Percolator; the projection engine always streams via load2.
            FirstJoinTask.RunPercolatorFdr(
                projections, config, ctx, "Second-pass", load2);
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
    }
}
