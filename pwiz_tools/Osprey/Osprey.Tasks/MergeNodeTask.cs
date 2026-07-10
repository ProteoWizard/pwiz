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
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks.ModelDiagnostics;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Final merge-node phase of the Osprey pipeline (Stage 7 in the
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
        public override string Name => @"SecondPassFDR";

        /// <summary>
        /// Computes Stage 7-8 (2nd-pass FDR + protein FDR + blib) in
        /// straight-through, the --task SecondPassFDR stage, and the --input-scores
        /// full-pipeline. Excluded in --task PerFileScoring, --task FirstPassFDR,
        /// and --task PerFileRescoring (all of which stop before the merge node).
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
            if (!string.IsNullOrEmpty(ctx.Config.OutputBlib))
                yield return ctx.Config.OutputBlib;
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

            // The 2nd-pass Percolator model, captured for the model-diagnostics
            // pass-2 model view; null when no reconciliation rescore happened.
            FeatureContributions pass2Contributions = null;

            // Second-pass Percolator FDR. Runs whenever Stage 6 reconciliation /
            // multi-charge consensus / gap-fill rescored entries -- the C# analog of
            // Rust's `total_rescored > 0` gate (pipeline.rs:5209) -- INDEPENDENT of
            // protein FDR. A reconciled parquet exists for a file iff that file had
            // rescore work, so "any reconciled parquet on disk" == total_rescored > 0,
            // and the test holds in both the straight-through pipeline (Stage 6 just
            // wrote them) and the --task SecondPassFDR merge (the Stage 6 worker wrote
            // them). Previously this was wrongly nested inside the ProteinFdr.HasValue
            // block, so a run without --protein-fdr wrote the blib from stale
            // first-pass (pre-reconciliation) scores. ComputeAndPersist reloads the
            // reconciled features, reruns Percolator, writes the .2nd-pass sidecars,
            // and reloads them onto the stubs so downstream protein FDR + blib see the
            // 2nd-pass q-values.
            if (AnyReconciledParquet(config))
            {
                pass2Contributions = Pass2FdrSidecar.ComputeAndPersist(
                    ctx, perFileEntries, perFileParquetPaths,
                    Name, ValidityKey(ctx));
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
            RunProteinFdr(perFileEntries, fullLibrary, config, ctx);
            swProtein.Stop();
            ctx.LogInfo(string.Format(@"[STAGE-WALL] stage7: {0:F1}s",
                swProtein.Elapsed.TotalSeconds));

            // Re-clamp experiment q to each entry's best run q on the FINAL post-Stage-6
            // pool. The pass-1 (and any pass-2) Percolator already clamped, but Stage 6
            // reconciliation zeroes the run q of moved peaks AFTER that clamp, so a precursor
            // whose only run-passing observation was relocated can otherwise keep a stale low
            // experiment q with no surviving run support -- reported with no run-level ID (the
            // blib ID-line artifact). Re-clamping here, against the run q's actually written to
            // the blib, restores "reported => some run genuinely passed" for the final output.
            PercolatorEngine.ClampExperimentQToBestRun(perFileEntries);

            // Write output blib
            ctx.LogInfo(string.Empty);
            var swBlib = Stopwatch.StartNew();
            WriteBlibOutput(perFileEntries, fullLibrary, libraryById, config, ctx);
            swBlib.Stop();
            ctx.LogInfo(string.Format(@"[STAGE-WALL] blib: {0:F1}s",
                swBlib.Elapsed.TotalSeconds));

            // FDRBench input TSV (pass 2): the peptides we report - the final merged/rescored set
            // written to the output - each with its final second-pass q-value and raw SVM
            // discriminant, so FDRBench can evaluate the FDR/FDP of what Osprey actually outputs.
            // (The blib writer only persists a 0.0 placeholder discriminant, so this is the only
            // path to a usable FDRBench score.) Pass 1 (the full pre-compaction first-pass pool)
            // is emitted earlier, in FirstJoinTask before compaction; --fdrbench-pass selects one
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
            // calibration views to the page FirstJoinTask wrote for pass 1, from
            // this post-compaction, second-pass-q-valued pool -- the same
            // RescoredEntries the pass-2 FDRBench TSV is written from. Opt-in and
            // off the default output path; a failure is logged and swallowed.
            if (config.ModelDiagnostics)
                ModelDiagnosticsReport.WritePass2AndFinalize(
                    perFileEntries, pass2Contributions, libraryById, config, ctx.LogInfo);

            return true;
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
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            PipelineContext ctx)
        {
            var result = ProteinFdrEngine.RunSecondPass(
                perFileEntries, fullLibrary, config, ctx.LogInfo);

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
        }

        /// <summary>
        /// True iff any input file has a reconciled scores parquet on disk -- i.e.
        /// Stage 6 rescored at least one file (multi-charge consensus, inter-replicate
        /// reconciliation, or gap-fill). Disk-based so it reads identically in the
        /// in-process pipeline (Stage 6 just wrote them) and the --task SecondPassFDR
        /// merge node (the Stage 6 worker wrote them). The C# analog of Rust's
        /// <c>total_rescored &gt; 0</c> gate (pipeline.rs:5209) for the second
        /// Percolator pass.
        /// </summary>
        private static bool AnyReconciledParquet(OspreyConfig config)
        {
            if (config.InputFiles == null)
                return false;
            foreach (var input in config.InputFiles)
            {
                if (File.Exists(ParquetScoreCache.GetReconciledScoresPath(input)))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Write passing entries to a BiblioSpec blib file.
        /// </summary>
        private void WriteBlibOutput(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
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
            var passingPeptides = ComputePassingPeptides(perFileEntries, config);

            var passingPrecursors = ComputePassingPrecursors(
                perFileEntries, config, passingPeptides, out int nFallback);
            if (nFallback > 0)
            {
                ctx.LogInfo(string.Format(
                    "{0} peptides had no charge state passing precursor-level FDR; best charge state kept as fallback",
                    nFallback));
            }

            var passingEntries = CollectPassingEntries(perFileEntries, passingPrecursors);

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

            var bestByPrecursor = BuildBestByPrecursor(passingEntries);

            ctx.LogInfo(string.Format(
                "[COUNT] Best-per-precursor for blib: {0}", bestByPrecursor.Count));

            var bestExpPrecursorQ = BuildBestExpPrecursorQ(perFileEntries, passingPrecursors);

            var sharedBounds = BuildSharedBoundaries(perFileEntries, passingPrecursors);

            var entriesByPrecursor = BuildCrossFileObservations(
                perFileEntries, out int nCrossFileObservations);

            ctx.LogInfo(string.Format(
                "[COUNT] Cross-file observations to write: {0}", nCrossFileObservations));

            BlibOutputWriter.Write(config, perFileEntries, libraryById, bestByPrecursor,
                bestExpPrecursorQ, sharedBounds, entriesByPrecursor);

            ctx.LogInfo(string.Format("Wrote {0} library spectra to {1} (from {2} passing entries)",
                bestByPrecursor.Count, config.OutputBlib, passingEntries.Count));
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
    }
}
