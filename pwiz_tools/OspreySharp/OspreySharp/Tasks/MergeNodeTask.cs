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

        // Phase B resume surface. Reads each file's reconciled
        // .scores.parquet, writes the .2nd-pass.fdr_scores.bin
        // sidecars (only when protein-FDR is enabled) and the
        // .blib output. ValidityKey adds the reconciliation hash
        // because the reconciled parquet is read.
        public override IEnumerable<string> Inputs(PipelineContext ctx)
        {
            if (ctx.Config.InputFiles == null) yield break;
            foreach (var input in ctx.Config.InputFiles)
                yield return ParquetScoreCache.GetScoresPath(input);
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
                + @";reconciliation=" + ctx.Config.ReconciliationParameterHash();
        }

        public override bool Run(PipelineContext ctx)
        {
            _ctx = ctx;
            // Mid-Run crash safety: see FirstJoinTask.Run for rationale.
            foreach (var output in Outputs(ctx))
                TaskValiditySidecar.Delete(output, Name);
            var config = ctx.Config;
            // perFileEntries comes from PerFileRescoreTask -- it owns
            // the post-rescore version (mutated in place; or the
            // unchanged upstream reference when planning was skipped).
            var perFileEntries = ctx.GetTask<PerFileRescoreTask>().GetPerFileEntries(ctx);
            var perFileScoring = ctx.GetTask<PerFileScoringTask>();
            var fullLibrary = perFileScoring.GetFullLibrary(ctx);
            var libraryById = perFileScoring.GetLibraryById(ctx);
            var perFileParquetPaths = perFileScoring.GetPerFileParquetPaths(ctx);

            // Stage 8: Protein FDR (optional)
            if (config.ProteinFdr.HasValue)
            {
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
                // sidecar on disk for downstream --join-at-pass=2
                // rehydration. Skipped on --join-at-pass=2 itself
                // (sidecar already loaded; no need to round-trip).
                if (!config.ExpectReconciledInput
                    && perFileParquetPaths.Count > 0)
                {
                    var inputByFileName = new Dictionary<string, string>();
                    foreach (var inputFile in config.InputFiles)
                        inputByFileName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;

                    int pass2Failures = 0;
                    foreach (var kvp in perFileEntries)
                    {
                        string fileName = kvp.Key;
                        if (!inputByFileName.TryGetValue(fileName, out string inputFile3))
                            continue;
                        try
                        {
                            FdrScoresSidecar.Write(
                                FdrScoresSidecar.Pass2Path(inputFile3),
                                kvp.Value, FdrScoresSidecar.Pass.SecondPass);
                        }
                        catch (Exception ex)
                        {
                            ctx.LogWarning(string.Format(
                                @"Failed to write 2nd-pass FDR sidecar for {0}: {1}",
                                fileName, ex.Message));
                            pass2Failures++;
                        }
                    }
                    if (pass2Failures == 0)
                    {
                        ctx.LogInfo(string.Format(
                            @"Wrote 2nd-pass FDR sidecars for {0} file(s)",
                            perFileEntries.Count));
                    }
                }

                ctx.LogInfo(string.Empty);
                ctx.LogInfo(string.Format(@"Running protein-level FDR at {0:P1}...",
                    config.ProteinFdr.Value));
                var swProtein = Stopwatch.StartNew();
                RunProteinFdr(perFileEntries, fullLibrary, config);
                swProtein.Stop();
                ctx.LogInfo(string.Format(@"[TIMING] Protein FDR: {0:F1}s",
                    swProtein.Elapsed.TotalSeconds));
            }

            // Stage 9: Write output blib
            ctx.LogInfo(string.Empty);
            ctx.LogInfo(string.Format(@"Writing output to {0}...", config.OutputBlib));
            var swBlib = Stopwatch.StartNew();
            WriteBlibOutput(perFileEntries, fullLibrary, libraryById, config);
            swBlib.Stop();
            ctx.LogInfo(string.Format(@"[TIMING] Blib output: {0:F1}s",
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
            Dictionary<uint, LibraryEntry> libraryById,
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
            double fdrThreshold = config.RunFdr; // run-level threshold for ID-line semantics
            double expThreshold = config.ExperimentFdr;

            // Stage 1: passing peptides
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

            // Stage 2: passing precursors, with fallback to best charge per peptide.
            // Tuple keys (modseq, charge) avoid the separator-collision risk of
            // string concatenation and skip a string allocation per lookup —
            // same shape as Rust's HashMap<(Arc<str>, u8), ...> at
            // pipeline.rs:4630.
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
            // The OR check (`best.Value <= expThreshold`) is the substantive one — if
            // best has q <= threshold, the loop above already added it to passingPrecursors;
            // the Contains check is redundant defensive belt-and-suspenders.
            int nFallback = 0;
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
            if (nFallback > 0)
            {
                _ctx.LogInfo(string.Format(
                    "{0} peptides had no charge state passing precursor-level FDR; best charge state kept as fallback",
                    nFallback));
            }

            // Collect passing entries for downstream best-per-precursor selection.
            // A precursor is admitted iff (modseq, charge) is in passingPrecursors.
            //
            // No protein-FDR gate here: Rust only filters the .blib by protein
            // FDR when `--fdr-level=protein` (the FdrLevel::Protein variant
            // routes through the peptide-gate's effective_experiment_qvalue).
            // C#'s FdrLevel enum doesn't include Protein, and `--protein-fdr`
            // is interpreted by Rust as a computation-enable flag, not a
            // hard blib filter. Mirror that: keep the (modseq, charge)
            // membership check from Stages 1+2 and don't apply
            // ExperimentProteinQvalue here.
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

            // Deduplicate by (modseq, charge) — keep best by
            // EffectiveRunQvalue(FdrLevel.Both). Matches Rust
            // pipeline.rs:6133-6138 which picks `best = min_by(run_qvalue)`
            // from the precursor group. The blib's downstream
            // OspreyRunScores / OspreyPeakBoundaries / RefSpectra
            // (peak boundaries + retention time) all source from this
            // best run, so the cross-impl best-file choice has to match
            // exactly or the per-file rows split into disjoint sets.
            // (Earlier this dedup keyed on ExperimentPrecursorQvalue,
            // producing a 26002+26002 only-rust/only-cs key split on
            // OspreyRunScores/PeakBoundaries.)
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

            _ctx.LogInfo(string.Format(
                "[COUNT] Best-per-precursor for blib: {0}", bestByPrecursor.Count));

            // Compute best (min) experiment_precursor_qvalue per (modseq, charge)
            // across all files. This is the value Rust writes into the .blib's
            // RefSpectra.score and OspreyExperimentScores.ExperimentQValue
            // columns (pipeline.rs:4670-4683 + 4795). NOT max(precursor,
            // peptide) — the experiment-level peptide q-value isn't used at
            // the .blib write site at all.
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

            // Build shared peak boundaries per (peptide, file): when the same
            // peptide is detected at multiple charge states in the same run,
            // all charges share the boundaries from the charge with lowest
            // run_qvalue. Mirrors Rust pipeline.rs:6020-6063
            // (build_shared_boundaries_from_plan). Without this, charge-N's
            // RefSpectra row gets charge-N's own boundaries, but Rust gives
            // charge-N the boundaries of whatever charge happened to score
            // best in that file — Skyline wants the consistent peptide-level
            // boundary so quantification across charges integrates the same
            // RT region. Key: (modseq, fileName); value: (apexRt, startRt,
            // endRt) from the min-run-qvalue entry across charges.
            // Tuple key matches Rust HashMap<(Arc<str>, u16), ...> at
            // pipeline.rs:6027 directly — no string concat or separator needed.
            var sharedBounds = new Dictionary<(string, string), double[]>();
            // For each (modseq, file), track the (apex, start, end, run_q) of best entry
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

            // Pre-index all per-file target entries by (ModifiedSequence, Charge) for O(1)
            // lookup of cross-file observations. Without this, the inner loop below is
            // O(N_passing * N_total) which is ~70 billion ops for typical experiments.
            var entriesByPrecursor =
                new Dictionary<(string, byte), List<KeyValuePair<string, FdrEntry>>>();
            int nCrossFileObservations = 0;
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
                    nCrossFileObservations++;
                }
            }

            _ctx.LogInfo(string.Format(
                "[COUNT] Cross-file observations to write: {0}", nCrossFileObservations));

            // Diagnostic: dump per-best-precursor q-values for cross-impl
            // bisection of the RefSpectra.score / OspreyExperimentScores
            // gap. Rust and C# agree on run-q-values (RetentionTimes.score
            // and OspreyRunScores PASS) but disagree on experiment-peptide-q
            // for ~42k of 45k entries. Schema:
            //   modseq <tab> charge <tab> file <tab> entry_id <tab>
            //   run_prec_q <tab> run_pept_q <tab> exp_prec_q <tab> exp_pept_q
            // Sort key = (modseq, charge). Compared externally against a
            // Rust-side dump produced by mirroring this code on the Rust
            // side (pipeline.rs blib write loop) under the same env-var
            // gate. Gated by OSPREY_DUMP_BLIB_QVALUES=1; zero overhead
            // when unset. Temporary — remove once peptide-q drift is
            // bisected and fixed.
            if (Environment.GetEnvironmentVariable(@"OSPREY_DUMP_BLIB_QVALUES") == @"1")
            {
                var rows = new List<string>();
                foreach (var kvp in bestByPrecursor.Values)
                {
                    var e = kvp.Value;
                    rows.Add(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "{0}\t{1}\t{2}\t{3}\t{4:R}\t{5:R}\t{6:R}\t{7:R}",
                        e.ModifiedSequence, e.Charge, kvp.Key, e.EntryId,
                        e.RunPrecursorQvalue, e.RunPeptideQvalue,
                        e.ExperimentPrecursorQvalue, e.ExperimentPeptideQvalue));
                }
                rows.Sort(StringComparer.Ordinal);
                // \n newlines (not Environment.NewLine) so the dump
                // byte-diffs against the corresponding Rust-side TSVs.
                // Same convention as OspreyDiagnostics — see its `LF`
                // field doc comment.
                using (var w = new StreamWriter(@"cs_blib_qvalues.tsv"))
                {
                    w.NewLine = "\n";
                    w.WriteLine("modseq\tcharge\tfile\tentry_id\trun_prec_q\trun_pept_q\texp_prec_q\texp_pept_q");
                    foreach (var row in rows)
                        w.WriteLine(row);
                }
                _ctx.LogInfo(string.Format(
                    @"Wrote cs_blib_qvalues.tsv ({0} best-per-precursor q-value rows)", rows.Count));
            }

            using (var writer = new BlibWriter(config.OutputBlib))
            {
                writer.BeginBatch();

                // Pre-create source file IDs once (instead of lazily inside the loop).
                // SpectrumSourceFiles.idFileName carries the library filename
                // (Skyline expects this — see Rust pipeline.rs:6110 + blib.rs:435).
                // The library file is the "ID source" because that's what the IDs
                // came from; the mzML file is the spectrum source.
                string libraryIdName = Path.GetFileName(config.LibrarySource.Path);
                var sourceFileIds = new Dictionary<string, long>();
                foreach (var kvp in perFileEntries)
                {
                    sourceFileIds[kvp.Key] = writer.AddSourceFile(
                        kvp.Key + ".mzML", libraryIdName, fdrThreshold);
                }

                // Parallel pre-compress pass. Per-spectrum zlib (Ionic.Zlib
                // level 6) dominates the blib write wall (~12s of the
                // observed 26s C# Stellar 3-file blib run); the SQLite
                // INSERT itself is fast and must stay sequential because
                // BlibWriter holds a single connection. Pre-compute
                // (mzBlob, intBlob, numPeaks) for every entry in parallel,
                // then drive AddSpectrumPrecompressed in iteration order
                // so RefSpectra row IDs stay deterministic. Mirrors the
                // Skyline BlibDb pattern in Model/Lib/BlibData/BlibDb.cs.
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

                    // RefSpectra.score is the EXPERIMENT-PRECURSOR q-value
                    // (min across all observations of this (modseq, charge)
                    // precursor in the experiment). Mirrors Rust
                    // pipeline.rs:4670-4683 which builds best_exp_q from
                    // e.experiment_precursor_qvalue (NOT max(precursor,
                    // peptide), despite the misleading LightFdr.experiment_qvalue
                    // = effective_experiment_qvalue(Both) at pipeline.rs:4705 —
                    // BlibPlanEntry.experiment_qvalue at pipeline.rs:4795
                    // overrides with best_exp_q.get(...) which is precursor-only).
                    // The same value feeds OspreyExperimentScores.ExperimentQValue
                    // below.
                    var lookupKey = (entry.ModifiedSequence, entry.Charge);
                    double scoreQvalue;
                    if (!bestExpPrecursorQ.TryGetValue(lookupKey, out scoreQvalue))
                        scoreQvalue = entry.ExperimentPrecursorQvalue;

                    // Compute nRunsDetected up-front so AddSpectrum can pass it
                    // through to RefSpectra.copies (matches Rust pipeline.rs:6179
                    // which passes n_runs_detected = group.len()). Was hardcoded
                    // to 1 before this fix; the same count is reused by
                    // OspreyExperimentScores below.
                    List<KeyValuePair<string, FdrEntry>> observations;
                    int nRunsDetected = 1;
                    if (entriesByPrecursor.TryGetValue(lookupKey, out observations) &&
                        observations.Count > 0)
                    {
                        nRunsDetected = observations.Count;
                    }

                    // Use shared peak boundaries when the same peptide
                    // is detected at multiple charges in this file (Rust
                    // pipeline.rs:6160-6164 + 6219-6222).
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

                    // Per-file RetentionTimes — one row for EVERY run where this
                    // precursor was detected, including the best-run/RefSpectra-source
                    // run itself. retentionTime (which drives Skyline ID-line
                    // display) is populated iff the run passes run-level FDR, OR
                    // (fallback) no run passes run-level FDR and this is the best
                    // run by lowest run_qvalue. Mirrors Rust pipeline.rs:6191-6243
                    // exactly. Uses FdrLevel.Both for the run-level q-value, matching
                    // the LightFdr.run_qvalue assignment at pipeline.rs:4704.
                    if (observations != null)
                    {
                        // Compute the fallback ID-line file: if NO run passes
                        // run-level FDR (post-second-pass q-values can shift
                        // slightly above threshold even when the precursor passes
                        // experiment-level), the run with the lowest run_qvalue
                        // gets the ID line so every blib RefSpectra has at least
                        // one ID line.
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
                            // Show an ID line if this run passes run-level FDR,
                            // OR if no run passes and this is the fallback best.
                            bool showIdLine = passesFdr ||
                                (!anyPassesRunFdr && obs.Key == bestRunFile);
                            bool isBest = obs.Key == fileName;

                            // Apply shared peak boundaries for this peptide
                            // in this run's file (cross-charge sharing —
                            // Rust pipeline.rs:6219-6222).
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

                    // Osprey extension tables — one row per RefSpectra each, mirroring
                    // Rust pipeline.rs:6255-6272 byte-for-byte. Best-run-only semantics
                    // for OspreyPeakBoundaries + OspreyRunScores; experiment-level for
                    // OspreyExperimentScores. Note the four 0.0 fields below are the
                    // same "not yet plumbed through Stage 7 plan entries" placeholders
                    // Rust currently writes:
                    //   PeakBoundaries.ApexIntensity (Rust: apex_coefficient = 0.0)
                    //   RunScores.DiscriminantScore (Rust: dot_product not avail = 0.0)
                    //   RunScores.PosteriorErrorProb (Rust: PEP not avail = 0.0)
                    // When Rust starts plumbing real values through, this block updates
                    // in lockstep to keep cross-impl parity.
                    // OspreyPeakBoundaries uses the shared boundaries for
                    // this (peptide, file) — same source as RefSpectra above.
                    writer.AddPeakBoundaries(refId, fileName,
                        sharedStart, sharedEnd, sharedApex,
                        0.0, // ApexIntensity — matches Rust's apex_coefficient placeholder
                        entry.BoundsArea);
                    writer.AddRunScores(refId, fileName,
                        entry.EffectiveRunQvalue(FdrLevel.Both),
                        0.0, // DiscriminantScore — matches Rust's dot_product placeholder
                        0.0); // PosteriorErrorProb — matches Rust's PEP placeholder
                    writer.AddExperimentScores(refId,
                        scoreQvalue, // Same value as RefSpectra.score: min(experiment_precursor_qvalue) across observations
                        nRunsDetected,
                        perFileEntries.Count);
                }

                writer.Commit();

                // Add metadata
                // OspreyMetadata key set must match Rust's
                // write_blib_from_plan (pipeline.rs:6078-6081) byte-for-byte.
                // The previous C#-only keys (search_parameter_hash,
                // n_passing_precursors) are dropped: search_parameter_hash
                // is already on every reconciled .scores.parquet (where it's
                // used for cache validation, the actual purpose), and
                // n_passing_precursors is recoverable as
                // SELECT COUNT(*) FROM RefSpectra.
                writer.AddMetadata(@"osprey_version", Program.VERSION_STRING);
                writer.AddMetadata(@"search_mode", @"coelution");
                writer.AddMetadata(@"run_fdr",
                    config.RunFdr.ToString(System.Globalization.CultureInfo.InvariantCulture));
                writer.AddMetadata(@"experiment_fdr",
                    config.ExperimentFdr.ToString(System.Globalization.CultureInfo.InvariantCulture));

                writer.FinalizeDatabase();
            }

            _ctx.LogInfo(string.Format("Wrote {0} spectra to {1}",
                bestByPrecursor.Count, config.OutputBlib));
        }
    }
}
