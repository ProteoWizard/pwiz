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
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR;
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// Final merge-node phase of the OspreySharp pipeline (Stage 7 in the
    /// HPC-boundary view from <c>Osprey-workflow.html</c>): persists the
    /// per-file 2nd-pass FDR-score sidecars, runs run-wide protein FDR
    /// (parsimony + picked-protein TDC), and writes the BiblioSpecLite
    /// <c>.blib</c> output. Invoked once per pipeline run on the merge
    /// node — no per-file fan-out beyond the sidecar write loop.
    ///
    /// All three substeps (the 2nd-pass FDR sidecar block, RunProteinFdr,
    /// and WriteBlibOutput) live in this file; nothing on AnalysisPipeline
    /// is needed for the merge-node phase.
    /// </summary>
    internal sealed class MergeNodeTask : OspreyTask
    {
        // PipelineContext is set on Run entry so the moved
        // RunProteinFdr / WriteBlibOutput methods can log
        // through the same callbacks the pipeline driver uses.
        private PipelineContext _ctx;

        public override string Name => @"MergeNode";

        /// <summary>
        /// Computes Stage 7-8 (2nd-pass FDR + protein FDR + blib) in
        /// straight-through, the --task MergeNode stage, and the --input-scores
        /// full-pipeline. Excluded in --task PerFileScoring, --task FirstJoin,
        /// and --task PerFileRescore (all of which stop before the merge node).
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
        // sidecars (only when protein-FDR is enabled) and the
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
            if (!string.IsNullOrEmpty(ctx.Config.OutputBlib))
                yield return ctx.Config.OutputBlib;
            if (ctx.Config.ProteinFdr.HasValue && ctx.Config.InputFiles != null)
            {
                foreach (var input in ctx.Config.InputFiles)
                    yield return FdrScoresSidecar.Pass2Path(input);
            }
        }

        public override string ValidityKey(PipelineContext ctx)
        {
            return base.ValidityKey(ctx)
                + @";reconciliation=" + ctx.Config.Identity.ReconciliationParameterHash();
        }

        /// <summary>
        /// No-op disk-load: MergeNode is the terminal aggregator. Its output
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
            _ctx = ctx;
            // Mid-Run crash safety: see FirstJoinTask.Run for rationale.
            foreach (var output in Outputs(ctx))
                TaskValiditySidecar.Delete(output, Name);
            var config = ctx.Config;
            // RescoredEntries is the final milestone of the shared buffer:
            // demanding it materializes PerFileRescore (running its rescore /
            // merge-mode compaction when the driver skipped it), which is what
            // produces the post-rescore version this stage reads.
            var perFileEntries = ctx.Get<RescoredEntries>().Value;
            var fullLibrary = ctx.Get<FullLibrary>().Value;
            var libraryById = ctx.Get<LibraryById>().Value;
            var perFileParquetPaths = ctx.Get<PerFileParquetPaths>().Value;

            // Stage 8: Protein FDR (optional)
            if (config.ProteinFdr.HasValue)
            {
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
                            "--task MergeNode: {0} perFileEntries key(s) have no matching " +
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
                        ctx.LogInfo(string.Format(
                            "--task MergeNode: {0}/{1} file(s) lack a 2nd-pass sidecar -- running " +
                            "second-pass FDR to compute scores from reconciled features " +
                            "(HPC distribution path; mirrors Rust pipeline.rs:4394-4468).",
                            missingPass2, totalFiles));
                        ctx.LogInfo(string.Empty);
                        ctx.LogInfo("Second-pass FDR");
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
                            int nMapped = 0;
                            foreach (var entry in kvp.Value)
                            {
                                int idx = (int)entry.ParquetIndex;
                                if (idx >= 0 && idx < featRows.Count)
                                {
                                    entry.Features = featRows[idx];
                                    nMapped++;
                                }
                            }
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
                                    perFileEntries, fullLibrary, config, ctx, "Second-pass");
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
                    // .MergeNode.osprey.task sidecar carries an identical
                    // key. AnalysisPipeline.WriteTaskSidecars also writes
                    // these at end-of-Run, but that step is bypassed when
                    // OspreyDiagnostics.ExitAfterDump calls Environment.Exit
                    // (the test-snapshot stage7 / OSPREY_STAGE7_PROTEIN_FDR_ONLY
                    // path). Writing inline next to each 2nd-pass binary
                    // makes the per-file resume contract survive that
                    // early exit, so a downstream run sees a fully
                    // resume-able boundary file pair (binary + validity
                    // sidecar) for every file that completed.
                    string taskValidityKey = ValidityKey(ctx);

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
                            TaskValiditySidecar.Write(pass2Path, Name, Program.VERSION,
                                taskValidityKey,
                                new[] { ParquetScoreCache.EffectiveScoresPathFromScoresPath(
                                    ParquetScoreCache.GetScoresPath(inputFile3)) });
                        }
                        catch (Exception ex)
                        {
                            ctx.LogWarning(string.Format(
                                @"Failed to write {0} sidecar for {1}: {2}",
                                Name, pass2Path, ex.Message));
                        }
                    }
                    if (pass2Failures == 0 && pass2Written > 0)
                    {
                        ctx.LogInfo(string.Format(
                            @"Wrote 2nd-pass FDR sidecars for {0} file(s){1}",
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
                // sequence. Without this reload, single-file --task MergeNode runs
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
                        ctx.LogInfo(string.Format(
                            "--task MergeNode: reloaded 2nd-pass FDR sidecar for {0}/{1} file(s) post-compaction",
                            filesReloaded, filesReloaded + filesMissing));
                    }
                }

                ctx.LogInfo(string.Empty);
                ctx.LogInfo(string.Format(@"Running protein-level FDR at {0:P1}...",
                    config.ProteinFdr.Value));
                var swProtein = Stopwatch.StartNew();
                RunProteinFdr(perFileEntries, fullLibrary, config);
                swProtein.Stop();
                ctx.LogInfo(string.Format(@"[STAGE-WALL] stage7: {0:F1}s",
                    swProtein.Elapsed.TotalSeconds));
            }

            // Stage 9: Write output blib
            ctx.LogInfo(string.Empty);
            ctx.LogInfo(string.Format(@"Writing output to {0}...", config.OutputBlib));
            var swBlib = Stopwatch.StartNew();
            WriteBlibOutput(perFileEntries, fullLibrary, libraryById, config);
            swBlib.Stop();
            ctx.LogInfo(string.Format(@"[STAGE-WALL] blib: {0:F1}s",
                swBlib.Elapsed.TotalSeconds));
            return true;
        }

        /// <summary>
        /// Run protein-level FDR using parsimony and picked-protein
        /// competition. Moved here from AnalysisPipeline as part of
        /// the Phase A monolith breakup.
        /// </summary>
        private void RunProteinFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config)
        {
            // Collect best peptide scores
            var bestScores = ProteinFdr.CollectBestPeptideScores(perFileEntries);
            _ctx.LogInfo(string.Format("Collected scores for {0} unique peptides", bestScores.Count));

            // Get detected peptide set: targets passing experiment-level
            // q-value at the configured fdr_level (matches Rust pipeline.rs
            // second-pass parsimony input which filters on
            // `effective_experiment_qvalue(peptide_gate_level) <= experiment_fdr`
            // where peptide_gate_level = config.fdr_level (Peptide if config
            // is Protein, otherwise the config value). The Rust default
            // `FdrLevel::Precursor` means a default run filters on precursor-
            // level experiment q-values, NOT peptide-level. Matching that
            // here prevents losing ~1500 peptides to an unintentionally
            // stricter Peptide-level gate.
            // Rust pipeline.rs:4510 maps `FdrLevel::Protein -> Peptide` and
            // passes other variants through. C#'s FdrLevel enum doesn't
            // include `Protein` (just Precursor/Peptide/Both), so the remap
            // is a no-op here -- pass config.FdrLevel through directly. The
            // important property is that the gate level matches Rust's
            // default `FdrLevel::Precursor`, NOT a hardcoded Peptide.
            var peptideGateLevel = config.FdrLevel;
            var detectedPeptides = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (!entry.IsDecoy &&
                        entry.EffectiveExperimentQvalue(peptideGateLevel) <= config.ExperimentFdr)
                    {
                        detectedPeptides.Add(entry.ModifiedSequence);
                    }
                }
            }

            _ctx.LogInfo(string.Format("Detected {0} unique peptides at {1:P1} experiment FDR ({2})",
                detectedPeptides.Count, config.ExperimentFdr, peptideGateLevel));
            _ctx.LogInfo(string.Format(
                "[COUNT] Detected peptides for protein FDR: {0} unique",
                detectedPeptides.Count));

            // Cross-impl bisection dump (env-var-gated, no-op in production).
            if (OspreyDiagnostics.DumpDetectedPeptides)
                OspreyDiagnostics.WriteStage7DetectedPeptidesDump(detectedPeptides);

            // Build protein parsimony
            var parsimony = ProteinFdr.BuildProteinParsimony(
                fullLibrary, config.SharedPeptides, detectedPeptides);

            _ctx.LogInfo(string.Format("Protein parsimony: {0} groups", parsimony.Groups.Count));
            _ctx.LogInfo(string.Format(
                "[COUNT] Protein parsimony groups: {0}", parsimony.Groups.Count));

            // Compute protein FDR. Gate is config.RunFdr (1x) per Savitski's
            // convention, matching Rust pipeline.rs:4389
            // (compute_protein_fdr at config.run_fdr). The previous 2x gate
            // was a divergence from Rust that has since been corrected.
            var proteinFdr = ProteinFdr.ComputeProteinFdr(parsimony, bestScores, config.RunFdr);

            // Count passing proteins
            int passingProteins = 0;
            foreach (var kvp in proteinFdr.GroupQvalues)
            {
                if (kvp.Value <= config.ProteinFdr.Value)
                    passingProteins++;
            }

            _ctx.LogInfo(string.Format("{0} protein groups pass {1:P1} protein FDR",
                passingProteins, config.ProteinFdr.Value));
            _ctx.LogInfo(string.Format(
                "[COUNT] Protein groups passing FDR: {0} at {1:P0}",
                passingProteins, config.ProteinFdr.Value));

            // Stage 7 cross-impl bisection dump (no-op unless
            // OSPREY_DUMP_STAGE7_PROTEIN_FDR=1). Fires before propagation so
            // the dumped state captures the picked-protein computation in
            // isolation, matching Rust diagnostics.dump_stage7_protein_fdr.
            if (OspreyDiagnostics.DumpStage7ProteinFdr)
            {
                OspreyDiagnostics.WriteStage7ProteinFdrDump(parsimony, proteinFdr);
                if (OspreyDiagnostics.Stage7ProteinFdrOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_STAGE7_PROTEIN_FDR_ONLY");
            }

            // Propagate protein q-values to FdrEntry stubs
            ProteinFdr.PropagateProteinQvalues(perFileEntries, proteinFdr, true, true);
        }

        /// <summary>
        /// Write passing entries to a BiblioSpec blib file.
        /// </summary>
        private void WriteBlibOutput(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            List<LibraryEntry> fullLibrary,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            OspreyConfig config)
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
            var passingPeptides = ComputePassingPeptides(perFileEntries, config);

            var passingPrecursors = ComputePassingPrecursors(
                perFileEntries, config, passingPeptides, out int nFallback);
            if (nFallback > 0)
            {
                _ctx.LogInfo(string.Format(
                    "{0} peptides had no charge state passing precursor-level FDR; best charge state kept as fallback",
                    nFallback));
            }

            var passingEntries = CollectPassingEntries(perFileEntries, passingPrecursors);

            _ctx.LogInfo(string.Format(
                "[COUNT] Stage 1 passing peptides: {0}", passingPeptides.Count));
            _ctx.LogInfo(string.Format(
                "[COUNT] Stage 2 passing precursors: {0}", passingPrecursors.Count));
            _ctx.LogInfo(string.Format("Writing {0} passing entries to blib", passingEntries.Count));

            if (passingEntries.Count == 0)
            {
                _ctx.LogWarning("No entries pass FDR threshold. Creating empty blib.");
            }

            // Ensure output directory exists
            string outputDir = Path.GetDirectoryName(config.OutputBlib);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var bestByPrecursor = BuildBestByPrecursor(passingEntries);

            _ctx.LogInfo(string.Format(
                "[COUNT] Best-per-precursor for blib: {0}", bestByPrecursor.Count));

            var bestExpPrecursorQ = BuildBestExpPrecursorQ(perFileEntries, passingPrecursors);

            var sharedBounds = BuildSharedBoundaries(perFileEntries, passingPrecursors);

            var entriesByPrecursor = BuildCrossFileObservations(
                perFileEntries, out int nCrossFileObservations);

            _ctx.LogInfo(string.Format(
                "[COUNT] Cross-file observations to write: {0}", nCrossFileObservations));

            WriteBlibFile(config, perFileEntries, libraryById, bestByPrecursor,
                bestExpPrecursorQ, sharedBounds, entriesByPrecursor);

            _ctx.LogInfo(string.Format("Wrote {0} spectra to {1}",
                bestByPrecursor.Count, config.OutputBlib));
        }

        // Stage 1 (peptide gate): the configured FdrLevel determines which
        // peptide identities are eligible for output. EXPERIMENT-level q-value.
        private static HashSet<string> ComputePassingPeptides(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries, OspreyConfig config)
        {
            double expThreshold = config.ExperimentFdr;
            var passingPeptides = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in perFileEntries)
            {
                foreach (var e in kvp.Value)
                {
                    if (e.IsDecoy)
                        continue;
                    if (e.EffectiveExperimentQvalue(config.FdrLevel) <= expThreshold)
                        passingPeptides.Add(e.ModifiedSequence);
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
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries, OspreyConfig config,
            HashSet<string> passingPeptides, out int nFallback)
        {
            double expThreshold = config.ExperimentFdr;
            var passingPrecursors = new HashSet<(string, byte)>();
            var bestChargePerPeptide = new Dictionary<string, KeyValuePair<byte, double>>(
                StringComparer.Ordinal);
            foreach (var kvp in perFileEntries)
            {
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

        // Collect passing entries for downstream best-per-precursor selection.
        // A precursor is admitted iff (modseq, charge) is in passingPrecursors.
        // No protein-FDR gate here (mirrors Rust: --protein-fdr is a compute
        // flag, not a hard blib filter; FdrLevel has no Protein variant).
        private static List<KeyValuePair<string, FdrEntry>> CollectPassingEntries(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            HashSet<(string, byte)> passingPrecursors)
        {
            var passingEntries = new List<KeyValuePair<string, FdrEntry>>();
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (entry.IsDecoy)
                        continue;
                    if (!passingPrecursors.Contains((entry.ModifiedSequence, entry.Charge)))
                        continue;
                    passingEntries.Add(
                        new KeyValuePair<string, FdrEntry>(kvp.Key, entry));
                }
            }
            return passingEntries;
        }

        // Deduplicate by (modseq, charge) — keep best by EffectiveRunQvalue(Both).
        // Matches Rust pipeline.rs:6133-6138. The blib's RefSpectra /
        // OspreyRunScores / OspreyPeakBoundaries all source from this best run,
        // so the cross-impl best-file choice must match exactly.
        private static Dictionary<(string, byte), KeyValuePair<string, FdrEntry>> BuildBestByPrecursor(
            List<KeyValuePair<string, FdrEntry>> passingEntries)
        {
            var bestByPrecursor = new Dictionary<(string, byte), KeyValuePair<string, FdrEntry>>();
            foreach (var kvp in passingEntries)
            {
                var key = (kvp.Value.ModifiedSequence, kvp.Value.Charge);
                KeyValuePair<string, FdrEntry> existing;
                if (!bestByPrecursor.TryGetValue(key, out existing) ||
                    kvp.Value.EffectiveRunQvalue(FdrLevel.Both) <
                    existing.Value.EffectiveRunQvalue(FdrLevel.Both))
                {
                    bestByPrecursor[key] = kvp;
                }
            }
            return bestByPrecursor;
        }

        // Best (min) experiment_precursor_qvalue per (modseq, charge) across all
        // files — the value Rust writes into RefSpectra.score and
        // OspreyExperimentScores.ExperimentQValue (pipeline.rs:4670-4683 + 4795).
        private static Dictionary<(string, byte), double> BuildBestExpPrecursorQ(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            HashSet<(string, byte)> passingPrecursors)
        {
            var bestExpPrecursorQ = new Dictionary<(string, byte), double>();
            foreach (var fileKvpExp in perFileEntries)
            {
                foreach (var e in fileKvpExp.Value)
                {
                    if (e.IsDecoy) continue;
                    var keyExp = (e.ModifiedSequence, e.Charge);
                    if (!passingPrecursors.Contains(keyExp)) continue;
                    double existingExp;
                    if (!bestExpPrecursorQ.TryGetValue(keyExp, out existingExp)
                        || e.ExperimentPrecursorQvalue < existingExp)
                    {
                        bestExpPrecursorQ[keyExp] = e.ExperimentPrecursorQvalue;
                    }
                }
            }
            return bestExpPrecursorQ;
        }

        // Shared peak boundaries per (peptide, file): all charge states of the
        // same peptide in a run share the boundaries from the charge with lowest
        // run_qvalue. Mirrors Rust pipeline.rs:6020-6063. Key: (modseq, fileName);
        // value: { apexRt, startRt, endRt, run_q } from the min-run-qvalue entry.
        private static Dictionary<(string, string), double[]> BuildSharedBoundaries(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            HashSet<(string, byte)> passingPrecursors)
        {
            var sharedBounds = new Dictionary<(string, string), double[]>();
            foreach (var fileKvpBounds in perFileEntries)
            {
                string boundsFile = fileKvpBounds.Key;
                foreach (var e in fileKvpBounds.Value)
                {
                    if (e.IsDecoy) continue;
                    if (!passingPrecursors.Contains((e.ModifiedSequence, e.Charge))) continue;
                    var sk = (e.ModifiedSequence, boundsFile);
                    double rq = e.EffectiveRunQvalue(FdrLevel.Both);
                    double[] existingB;
                    if (!sharedBounds.TryGetValue(sk, out existingB) || rq < existingB[3])
                    {
                        sharedBounds[sk] = new[] { e.ApexRt, e.StartRt, e.EndRt, rq };
                    }
                }
            }
            return sharedBounds;
        }

        // Pre-index all per-file target entries by (ModifiedSequence, Charge) for
        // O(1) lookup of cross-file observations (otherwise the write loop is
        // O(N_passing * N_total)). nObservations = total non-decoy rows indexed.
        private static Dictionary<(string, byte), List<KeyValuePair<string, FdrEntry>>> BuildCrossFileObservations(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries, out int nObservations)
        {
            var entriesByPrecursor =
                new Dictionary<(string, byte), List<KeyValuePair<string, FdrEntry>>>();
            nObservations = 0;
            foreach (var fileKvp in perFileEntries)
            {
                string fn = fileKvp.Key;
                foreach (var fileEntry in fileKvp.Value)
                {
                    if (fileEntry.IsDecoy)
                        continue;
                    var key = (fileEntry.ModifiedSequence, fileEntry.Charge);
                    List<KeyValuePair<string, FdrEntry>> list;
                    if (!entriesByPrecursor.TryGetValue(key, out list))
                    {
                        list = new List<KeyValuePair<string, FdrEntry>>(perFileEntries.Count);
                        entriesByPrecursor[key] = list;
                    }
                    list.Add(new KeyValuePair<string, FdrEntry>(fn, fileEntry));
                    nObservations++;
                }
            }
            return entriesByPrecursor;
        }

        // Per-observation RetentionTimes rows — one row for EVERY run where this
        // precursor was detected. retentionTime (drives Skyline ID-line display)
        // is populated iff the run passes run-level FDR, OR (fallback) no run
        // passes and this is the best run by lowest run_qvalue. Cross-charge
        // shared boundaries applied. Mirrors Rust pipeline.rs:6191-6243.
        private static void WriteRetentionTimes(
            BlibWriter writer, long refId, string fileName,
            List<KeyValuePair<string, FdrEntry>> observations,
            Dictionary<string, long> sourceFileIds,
            Dictionary<(string, string), double[]> sharedBounds,
            double fdrThreshold)
        {
            if (observations == null)
                return;
            // Compute the fallback ID-line file: if NO run passes run-level FDR,
            // the run with the lowest run_qvalue gets the ID line so every blib
            // RefSpectra has at least one ID line.
            bool anyPassesRunFdr = false;
            string bestRunFile = null;
            double bestRunQ = double.MaxValue;
            foreach (var obs in observations)
            {
                double rq = obs.Value.EffectiveRunQvalue(FdrLevel.Both);
                if (rq <= fdrThreshold)
                    anyPassesRunFdr = true;
                if (rq < bestRunQ)
                {
                    bestRunQ = rq;
                    bestRunFile = obs.Key;
                }
            }

            foreach (var obs in observations)
            {
                long srcId = sourceFileIds[obs.Key];
                var fileEntry = obs.Value;
                double runQ = fileEntry.EffectiveRunQvalue(FdrLevel.Both);
                bool passesFdr = runQ <= fdrThreshold;
                bool showIdLine = passesFdr ||
                    (!anyPassesRunFdr && obs.Key == bestRunFile);
                bool isBest = obs.Key == fileName;

                var runSharedKey = (fileEntry.ModifiedSequence, obs.Key);
                double runApex = fileEntry.ApexRt;
                double runStart = fileEntry.StartRt;
                double runEnd = fileEntry.EndRt;
                double[] runShared;
                if (sharedBounds.TryGetValue(runSharedKey, out runShared))
                {
                    runApex = runShared[0];
                    runStart = runShared[1];
                    runEnd = runShared[2];
                }

                double? rtForIdLine = null;
                if (showIdLine)
                    rtForIdLine = runApex;
                writer.AddRetentionTime(
                    refId, srcId,
                    rtForIdLine,
                    runStart,
                    runEnd,
                    runQ,
                    isBest);
            }
        }

        // Write the .blib: source file IDs, parallel zlib pre-compress, then the
        // sequential per-best-precursor RefSpectra + modifications + protein +
        // RetentionTimes + Osprey extension-table emission, metadata, finalize.
        private static void WriteBlibFile(
            OspreyConfig config,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            Dictionary<(string, byte), KeyValuePair<string, FdrEntry>> bestByPrecursor,
            Dictionary<(string, byte), double> bestExpPrecursorQ,
            Dictionary<(string, string), double[]> sharedBounds,
            Dictionary<(string, byte), List<KeyValuePair<string, FdrEntry>>> entriesByPrecursor)
        {
            double fdrThreshold = config.RunFdr; // run-level threshold for ID-line semantics
            using (var writer = new BlibWriter(config.OutputBlib))
            {
                writer.BeginBatch();

                // Pre-create source file IDs once. SpectrumSourceFiles.idFileName
                // carries the library filename (Skyline expects this — Rust
                // pipeline.rs:6110 + blib.rs:435). The library file is the "ID
                // source"; the mzML file is the spectrum source.
                string libraryIdName = Path.GetFileName(config.LibrarySource.Path);
                var sourceFileIds = new Dictionary<string, long>();
                foreach (var kvp in perFileEntries)
                {
                    sourceFileIds[kvp.Key] = writer.AddSourceFile(
                        kvp.Key + ".mzML", libraryIdName, fdrThreshold);
                }

                // Parallel pre-compress pass. Per-spectrum zlib dominates the blib
                // write wall; pre-compute (mzBlob, intBlob, numPeaks) for every
                // entry in parallel, then drive AddSpectrumPrecompressed in
                // iteration order so RefSpectra row IDs stay deterministic.
                var blibEntries = bestByPrecursor.Values.ToList();
                int blibN = blibEntries.Count;
                var blibMzBlobs = new byte[blibN][];
                var blibIntBlobs = new byte[blibN][];
                var blibNumPeaks = new int[blibN];
                Parallel.For(0, blibN,
                    new ParallelOptions { MaxDegreeOfParallelism = config.NThreads },
                    i =>
                    {
                        var entry = blibEntries[i].Value;
                        LibraryEntry libEntryP;
                        if (!libraryById.TryGetValue(entry.EntryId, out libEntryP))
                            return;
                        int nFrags = libEntryP.Fragments.Count;
                        var mzsP = new double[nFrags];
                        var intsP = new float[nFrags];
                        for (int j = 0; j < nFrags; j++)
                        {
                            mzsP[j] = libEntryP.Fragments[j].Mz;
                            intsP[j] = libEntryP.Fragments[j].RelativeIntensity;
                        }
                        blibMzBlobs[i] = BlibWriter.CompressMzs(mzsP);
                        blibIntBlobs[i] = BlibWriter.CompressIntensities(intsP);
                        blibNumPeaks[i] = nFrags;
                    });

                for (int blibIdx = 0; blibIdx < blibN; blibIdx++)
                {
                    var kvp = blibEntries[blibIdx];
                    string fileName = kvp.Key;
                    var entry = kvp.Value;

                    LibraryEntry libEntry;
                    if (!libraryById.TryGetValue(entry.EntryId, out libEntry))
                        continue;

                    long fileId = sourceFileIds[fileName];

                    byte[] mzBlobPre = blibMzBlobs[blibIdx];
                    byte[] intBlobPre = blibIntBlobs[blibIdx];
                    int numPeaksPre = blibNumPeaks[blibIdx];

                    // RefSpectra.score is the EXPERIMENT-PRECURSOR q-value (min
                    // across all observations of this (modseq, charge)). Mirrors
                    // Rust pipeline.rs:4670-4683 / 4795. Same value feeds
                    // OspreyExperimentScores.ExperimentQValue below.
                    var lookupKey = (entry.ModifiedSequence, entry.Charge);
                    double scoreQvalue;
                    if (!bestExpPrecursorQ.TryGetValue(lookupKey, out scoreQvalue))
                        scoreQvalue = entry.ExperimentPrecursorQvalue;

                    // nRunsDetected -> RefSpectra.copies (Rust pipeline.rs:6179
                    // passes n_runs_detected = group.len()). Reused by
                    // OspreyExperimentScores below.
                    List<KeyValuePair<string, FdrEntry>> observations;
                    int nRunsDetected = 1;
                    if (entriesByPrecursor.TryGetValue(lookupKey, out observations) &&
                        observations.Count > 0)
                    {
                        nRunsDetected = observations.Count;
                    }

                    // Shared peak boundaries when the peptide is detected at
                    // multiple charges in this file (Rust pipeline.rs:6160-6164).
                    var sharedKey = (entry.ModifiedSequence, fileName);
                    double sharedApex = entry.ApexRt;
                    double sharedStart = entry.StartRt;
                    double sharedEnd = entry.EndRt;
                    double[] sharedVals;
                    if (sharedBounds.TryGetValue(sharedKey, out sharedVals))
                    {
                        sharedApex = sharedVals[0];
                        sharedStart = sharedVals[1];
                        sharedEnd = sharedVals[2];
                    }

                    long refId = writer.AddSpectrumPrecompressed(
                        libEntry.Sequence,
                        libEntry.ModifiedSequence,
                        libEntry.PrecursorMz,
                        libEntry.Charge,
                        sharedApex,
                        sharedStart,
                        sharedEnd,
                        mzBlobPre, intBlobPre, numPeaksPre,
                        scoreQvalue, fileId, nRunsDetected, 0.0);

                    // Add modifications
                    if (libEntry.Modifications != null && libEntry.Modifications.Count > 0)
                        writer.AddModifications(refId, libEntry.Modifications);

                    // Add protein mappings
                    if (libEntry.ProteinIds != null && libEntry.ProteinIds.Count > 0)
                        writer.AddProteinMapping(refId, libEntry.ProteinIds);

                    WriteRetentionTimes(writer, refId, fileName, observations,
                        sourceFileIds, sharedBounds, fdrThreshold);

                    // Osprey extension tables — one row per RefSpectra each,
                    // mirroring Rust pipeline.rs:6255-6272. Best-run-only for
                    // OspreyPeakBoundaries + OspreyRunScores; experiment-level for
                    // OspreyExperimentScores. The 0.0 fields are the same "not yet
                    // plumbed through Stage 7 plan entries" placeholders Rust writes.
                    writer.AddPeakBoundaries(refId, fileName,
                        sharedStart, sharedEnd, sharedApex,
                        0.0, // ApexIntensity — matches Rust's apex_coefficient placeholder
                        entry.BoundsArea);
                    writer.AddRunScores(refId, fileName,
                        entry.EffectiveRunQvalue(FdrLevel.Both),
                        0.0, // DiscriminantScore — matches Rust's dot_product placeholder
                        0.0); // PosteriorErrorProb — matches Rust's PEP placeholder
                    writer.AddExperimentScores(refId,
                        scoreQvalue, // Same value as RefSpectra.score
                        nRunsDetected,
                        perFileEntries.Count);
                }

                writer.Commit();

                // Add metadata. OspreyMetadata key set must match Rust's
                // write_blib_from_plan (pipeline.rs:6078-6081) byte-for-byte.
                writer.AddMetadata(@"osprey_version", Program.VERSION_STRING);
                writer.AddMetadata(@"search_mode", @"coelution");
                writer.AddMetadata(@"run_fdr",
                    config.RunFdr.ToString(System.Globalization.CultureInfo.InvariantCulture));
                writer.AddMetadata(@"experiment_fdr",
                    config.ExperimentFdr.ToString(System.Globalization.CultureInfo.InvariantCulture));

                writer.FinalizeDatabase();
            }
        }
    }
}
