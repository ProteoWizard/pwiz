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
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR.Reconciliation;
using pwiz.OspreySharp.IO;
using pwiz.OspreySharp.Tasks;

namespace pwiz.OspreySharp.FirstJoin
{
    /// <summary>
    /// First-join phase of the OspreySharp pipeline (Stage 5 in the
    /// HPC-boundary view from <c>Osprey-workflow.html</c>): runs the
    /// first-pass Percolator SVM + protein FDR over the joined per-file
    /// scores, persists the per-file 1st-pass FDR sidecar, compacts
    /// each file's stub list to the post-first-pass passing base_ids,
    /// and (when reconciliation is enabled and we have at least one
    /// file's worth of evidence) plans the per-(file, entry)
    /// reconciliation actions that PerFileRescoreTask will execute.
    /// All work that requires all-file representation lives here —
    /// this is the natural fan-out / join boundary on an HPC node.
    ///
    /// Phase A scope: this task is a thin orchestration wrapper that
    /// delegates to the existing private (now <c>internal</c>)
    /// AnalysisPipeline methods (RunFdr, RunFirstPassProteinFdr,
    /// WriteFdrScoresSidecars, WriteReconciliationFiles) plus the
    /// FDR.Reconciliation static helpers (MultiChargeConsensus,
    /// ConsensusRts, CalibrationRefit, ReconciliationPlanner). The
    /// inline planning block from <c>AnalysisPipeline.Run</c> moved
    /// here verbatim; the only changes are LogInfo / LogWarning /
    /// LogError → ctx.LogInfo etc., and a return-false / set
    /// ctx.ExitCode flow for the early-exit paths the original block
    /// had as <c>return 0</c> / <c>return 1</c>.
    ///
    /// Outputs (PerFileConsensusTargets, ReconciliationActions,
    /// RefinedCalibrations, PerFileGapFillForRescore) are exposed as
    /// instance properties for the next task (PerFileRescoreTask) to
    /// consume after this one completes successfully. <c>DidPlan</c>
    /// is the gate for that next task — it flips to <c>true</c> only
    /// when the Stage 6 planning block actually ran.
    /// </summary>
    internal sealed class FirstJoinTask : OspreyTask
    {
        private readonly AnalysisPipeline _pipeline;
        private readonly List<KeyValuePair<string, List<FdrEntry>>> _perFileEntries;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RTCalibration> _perFileCalibrations;
        private readonly Dictionary<string, string> _perFileParquetPaths;
        private readonly List<LibraryEntry> _fullLibrary;

        public FirstJoinTask(
            AnalysisPipeline pipeline,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            System.Collections.Concurrent.ConcurrentDictionary<string, RTCalibration> perFileCalibrations,
            Dictionary<string, string> perFileParquetPaths,
            List<LibraryEntry> fullLibrary)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _perFileEntries = perFileEntries ?? throw new ArgumentNullException(nameof(perFileEntries));
            _perFileCalibrations = perFileCalibrations ?? throw new ArgumentNullException(nameof(perFileCalibrations));
            _perFileParquetPaths = perFileParquetPaths ?? throw new ArgumentNullException(nameof(perFileParquetPaths));
            _fullLibrary = fullLibrary ?? throw new ArgumentNullException(nameof(fullLibrary));
        }

        public override string Name => @"FirstJoin";

        // Outputs read by AnalysisPipeline.Run after Run completes
        // successfully. Default values match the "skipped planning"
        // case (single-file, --join-at-pass=2, or
        // !Reconciliation.Enabled), so the caller can pass them
        // straight through to PerFileRescoreTask without further
        // null guards.
        public bool DidPlan { get; private set; }
        public IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> PerFileConsensusTargets { get; private set; }
            = new Dictionary<string, IReadOnlyList<(int, double, double, double)>>();
        public IReadOnlyDictionary<(string FileName, int Index), ReconcileAction> ReconciliationActions { get; private set; }
            = new Dictionary<(string, int), ReconcileAction>();
        public IReadOnlyDictionary<string, RTCalibration> RefinedCalibrations { get; private set; }
            = new Dictionary<string, RTCalibration>();
        public IReadOnlyDictionary<string, List<GapFillTarget>> PerFileGapFillForRescore { get; private set; }
            = new Dictionary<string, List<GapFillTarget>>();

        public override bool Run(PipelineContext ctx)
        {
            var config = ctx.Config;

            // Stage 5: First-pass FDR
            // Skipped under --join-at-pass=2: the 1st-pass sidecar
            // loaded above already carries the SVM scores + q-values
            // from the straight-through pipeline run that produced
            // the reconciled parquets. Re-running Percolator here
            // would re-train SVMs on the same data and drift vs the
            // sidecar. Mirrors Rust's compute_fdr_from_stubs skip
            // (pipeline.rs:3916 "if !can_skip_fdr || expect_reconciled_input").
            if (!config.ExpectReconciledInput)
            {
                ctx.LogInfo(string.Empty);
                ctx.LogInfo(string.Format(@"Running {0} FDR control on coelution results...",
                    config.FdrMethod));

                var swFdr = Stopwatch.StartNew();
                _pipeline.RunFdr(_perFileEntries, _fullLibrary, config);
                swFdr.Stop();
                ctx.LogInfo(string.Format(@"[TIMING] Percolator/Simple FDR: {0:F1}s",
                    swFdr.Elapsed.TotalSeconds));
            }
            else
            {
                ctx.LogInfo(@"--join-at-pass=2: skipping first-pass Percolator (sidecar provides q-values).");
            }

            // Log first-pass results
            int passingTargets = 0;
            foreach (var kvp in _perFileEntries)
            {
                int fileTargets = 0;
                foreach (var entry in kvp.Value)
                {
                    if (!entry.IsDecoy &&
                        entry.EffectiveRunQvalue(config.FdrLevel) <= config.RunFdr)
                    {
                        fileTargets++;
                    }
                }
                ctx.LogInfo(string.Format(@"  {0}: {1} precursors at {2:P1} run-level FDR",
                    kvp.Key, fileTargets, config.RunFdr));
                passingTargets += fileTargets;
            }
            ctx.LogInfo(string.Format(@"Total: {0} precursors pass run-level FDR across all files",
                passingTargets));

            // Stage 5 diagnostic dump. Gated by OSPREY_DUMP_PERCOLATOR=1;
            // exits when OSPREY_PERCOLATOR_ONLY=1 is also set. Writes all
            // four q-values plus SVM score and PEP for every FdrEntry,
            // before compaction drops any rows, so the cross-impl diff
            // sees both targets and decoys.
            if (OspreyDiagnostics.DumpPercolator)
            {
                OspreyDiagnostics.WriteStage5PercolatorDump(_perFileEntries);
                if (OspreyDiagnostics.PercolatorOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_PERCOLATOR_ONLY");
            }

            // First-pass protein FDR: runs on the full pre-compaction
            // peptide pool so target and decoy proteins compete on a
            // symmetric set. Sets RunProteinQvalue on every FdrEntry,
            // which Stage 6 reconciliation reads via the protein-rescue
            // gate in ConsensusRts.Compute. Mirrors Rust pipeline.rs:3029
            // ("First-pass protein FDR"). Skipped on
            // --join-at-pass=2: the 1st-pass FDR sidecar loaded above
            // already carries RunProteinQvalue from the original
            // straight-through pipeline. Re-running the deterministic
            // protein-FDR computation on identical inputs would just
            // overwrite the loaded values with the same numbers
            // (~17s on Astral 1-file; saves duplicate work on every
            // post-Stage-6 rehydration entry).
            if (config.ProteinFdr.HasValue && _perFileEntries.Count > 0
                && !config.ExpectReconciledInput)
            {
                ctx.LogInfo(string.Empty);
                ctx.LogInfo(@"First-pass protein FDR");
                var swFirstPassProtein = Stopwatch.StartNew();
                _pipeline.RunFirstPassProteinFdr(_perFileEntries, _fullLibrary, config);
                swFirstPassProtein.Stop();
                ctx.LogInfo(string.Format(@"[TIMING] First-pass protein FDR: {0:F1}s",
                    swFirstPassProtein.Elapsed.TotalSeconds));
            }

            // Compaction: drop entries whose base_id (entry_id with the
            // decoy bit masked off) does not pass either the peptide-q
            // or protein-q gate. Target and paired decoy share base_id
            // and are kept or dropped together. Mirrors Rust
            // pipeline.rs:3094-3132. Without this, Stage 6 multi-charge
            // consensus selection groups by modified_sequence and
            // includes non-passing charge states that Rust has already
            // dropped, producing different rescore-target sets and
            // different per-file Vec positions.
            // Persist the per-file `.1st-pass.fdr_scores.bin` sidecars
            // BEFORE compaction so every stub (passing or not) carries
            // its q-values into the file. Mirrors osprey/src/pipeline.rs
            // around persist_fdr_scores at line ~3180. Stage 6 workers
            // re-derive the post-compaction set by applying the q-value
            // threshold themselves, so they need every entry's q-values
            // — not just the survivors. Skipped on --join-at-pass=2:
            // the 1st-pass sidecar is what we just LOADED from to seed
            // entries, so re-writing produces the same bytes (any
            // divergence would be a sidecar-load bug, not a write
            // requirement). Saves ~6s I/O per --join-at-pass=2 invocation.
            int fdrSidecarFailures = 0;
            if (!config.ExpectReconciledInput)
            {
                fdrSidecarFailures = _pipeline.WriteFdrScoresSidecars(
                    _perFileEntries, _perFileParquetPaths, config);
                if (fdrSidecarFailures > 0 && config.StopAfterStage5)
                {
                    ctx.LogError(string.Format(
                        @"--join-at-pass=1 --join-only: {0}/{1} 1st-pass fdr_scores.bin sidecar " +
                        @"writes failed; boundary file pair is incomplete. See warnings above.",
                        fdrSidecarFailures, _perFileEntries.Count));
                    ctx.ExitCode = 1;
                    return false;
                }
            }

            if (_perFileEntries.Count > 0)
            {
                var firstPassBaseIds = new HashSet<uint>();
                double peptideGate = config.RunFdr;
                double proteinGate = config.ProteinFdr ?? 0.0;
                foreach (var kvp in _perFileEntries)
                {
                    foreach (var entry in kvp.Value)
                    {
                        if (entry.IsDecoy)
                            continue;
                        if (entry.RunPeptideQvalue <= peptideGate ||
                            (proteinGate > 0.0 && entry.RunProteinQvalue <= proteinGate))
                        {
                            firstPassBaseIds.Add(entry.EntryId & AnalysisPipeline.BASE_ID_MASK);
                        }
                    }
                }
                int beforeCount = 0, afterCount = 0;
                foreach (var kvp in _perFileEntries)
                {
                    beforeCount += kvp.Value.Count;
                    kvp.Value.RemoveAll(e => !firstPassBaseIds.Contains(e.EntryId & AnalysisPipeline.BASE_ID_MASK));
                    kvp.Value.TrimExcess();
                    afterCount += kvp.Value.Count;
                }
                ctx.LogInfo(string.Format(
                    @"First-pass compaction: {0} -> {1} entries ({2} passing base_ids)",
                    beforeCount, afterCount, firstPassBaseIds.Count));
            }

            // --join-at-pass=2: re-load the 2nd-pass FDR sidecar onto
            // the post-compaction stub list. The 2nd-pass scores +
            // q-values are what Stage 7 (protein FDR) and the blib
            // writer consume. Loading happens AFTER compaction
            // because the 2nd-pass sidecar was written from the
            // post-compaction state — overlaying it before compaction
            // would either silently miss records (if the pre-compaction
            // entries dict happens to also contain compaction-dropped
            // entry_ids that the 2nd-pass never saw) or scramble
            // q-values on entries the 2nd-pass had already discarded.
            // TryReadOverlay tolerates count mismatch (records whose
            // entry_id is unknown to the caller's dict are skipped),
            // so the constraint here is correctness-of-state, not
            // size-equality. Mirrors Rust's load-order inversion at
            // pipeline.rs:4030-4076.
            if (config.ExpectReconciledInput)
            {
                // Same input-by-fileName map shape as the 1st-pass
                // load above (sidecar paths derive from input file
                // stem, not parquet stem).
                var inputByFileName2 = new Dictionary<string, string>();
                foreach (var inputFile in config.InputFiles)
                    inputByFileName2[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;

                foreach (var kvp in _perFileEntries)
                {
                    string fileName = kvp.Key;
                    var entries = kvp.Value;
                    // Sidecar overlay onto the compacted entry list.
                    // The sidecar carries entry_ids per record, so we
                    // skip the parquet re-read that earlier versions
                    // used purely to size-check the load.
                    if (!inputByFileName2.TryGetValue(fileName, out string inputFile2))
                    {
                        ctx.LogError(string.Format(
                            @"--join-at-pass=2: no synthetic input path for {0}", fileName));
                        ctx.ExitCode = 1;
                        return false;
                    }
                    // Prefer 2nd-pass sidecar; fall back to 1st-pass.
                    // Rust skips 2nd-pass sidecar write on single-file
                    // runs (pipeline.rs:4489 gates on input_files > 1)
                    // and reuses 1st-pass SVM scores in that case. The
                    // 2nd-pass sidecar's distinct contents arise only
                    // when reconciliation actually rescores entries
                    // (multi-file Stellar+Astral). Without this
                    // fallback, single-file --join-at-pass=2 errors
                    // even though the 1st-pass scores are equivalent.
                    string sidecarPath = FdrScoresSidecar.Pass2Path(inputFile2);
                    FdrScoresSidecar.Pass expectedPass = FdrScoresSidecar.Pass.SecondPass;
                    if (!File.Exists(sidecarPath))
                    {
                        string pass1Path = FdrScoresSidecar.Pass1Path(inputFile2);
                        if (!File.Exists(pass1Path))
                        {
                            ctx.LogError(string.Format(
                                @"--join-at-pass=2: neither 2nd-pass nor 1st-pass FDR " +
                                @"sidecar found for {0} (looked at {1} and {2}). Re-run a " +
                                @"straight-through pipeline to produce them.",
                                fileName, sidecarPath, pass1Path));
                            ctx.ExitCode = 1;
                            return false;
                        }
                        sidecarPath = pass1Path;
                        expectedPass = FdrScoresSidecar.Pass.FirstPass;
                        ctx.LogInfo(string.Format(
                            @"--join-at-pass=2: 2nd-pass sidecar missing for {0}; " +
                            @"falling back to 1st-pass (matches Rust single-file behavior)",
                            fileName));
                    }
                    // Overlay sidecar scores directly onto the
                    // compacted list by entry_id. The sidecar's binary
                    // format carries entry_ids per record, so we don't
                    // need to re-read the parquet to size-match — that
                    // re-read used to dominate Stage 7 cs walls (~7s
                    // on Stellar 3-file). TryReadOverlay tolerates
                    // sidecar entries that aren't in the compacted
                    // dict (failing precursors dropped by compaction).
                    var entriesByEntryId = new Dictionary<uint, FdrEntry>(entries.Count);
                    for (int i = 0; i < entries.Count; i++)
                        entriesByEntryId[entries[i].EntryId] = entries[i];
                    if (!FdrScoresSidecar.TryReadOverlay(sidecarPath, entriesByEntryId, expectedPass))
                    {
                        ctx.LogError(string.Format(
                            @"--join-at-pass=2: sidecar at {0} failed to load (expected {1}).",
                            sidecarPath, expectedPass));
                        ctx.ExitCode = 1;
                        return false;
                    }
                }
                ctx.LogInfo(string.Format(
                    @"--join-at-pass=2: loaded 2nd-pass FDR sidecars for {0} file(s)",
                    _perFileEntries.Count));
            }

            // Stage 6: planning checkpoint — multi-charge consensus +
            // cross-run consensus RTs + per-file calibration refit. The
            // execution side (per-file rescore at locked boundaries +
            // gap-fill + second-pass FDR) lives in PerFileRescoreTask;
            // this pass produces the inputs that task consumes.
            // Mirrors pipeline.rs Stage 6 entry block at lines 3208-3273.
            // Stage 6 runs even with a single file: multi-charge
            // consensus needs multiple charge states of the same
            // peptide (which can exist within a single run), and the
            // rescore loop applies the consensus targets it produces.
            // Cross-run reconciliation degenerates to zero actions on
            // a single file, but the planning checkpoint and the
            // multi-charge rescore must still execute to match Rust's
            // single-file behavior.
            //
            // --join-at-pass=2 SKIPS Stage 6 entirely. The reconciled
            // .scores.parquet + 2nd-pass sidecar combination already
            // carries the post-Stage-6 state (including any rescored
            // entries' final SVM scores). Re-running Stage 6 here
            // would re-apply multi-charge consensus + reconciliation
            // on top of already-reconciled data and drift vs the
            // straight-through pipeline. Mirrors Rust's
            // pipeline.rs:3823 expect_reconciled_input gate.
            if (!config.ExpectReconciledInput
                && _perFileEntries.Count >= 1 && config.Reconciliation.Enabled)
            {
                ctx.LogInfo(string.Empty);
                ctx.LogInfo(@"Stage 6: planning");

                // 1. Multi-charge consensus per file (independent — runs
                //    first per Rust pipeline.rs:3217, before consensus
                //    RT computation).
                var perFileConsensusTargets = new Dictionary<string,
                    IReadOnlyList<(int Index, double Apex, double Start, double End)>>();
                foreach (var kvp in _perFileEntries)
                {
                    perFileConsensusTargets[kvp.Key] =
                        MultiChargeConsensus.SelectRescoreTargets(kvp.Value, config.RunFdr);
                }
                int totalMulticharge = 0;
                foreach (var kvp in perFileConsensusTargets)
                    totalMulticharge += kvp.Value.Count;
                ctx.LogInfo(string.Format(
                    @"Stage 6 multi-charge consensus: {0} entries need re-scoring across {1} files",
                    totalMulticharge, _perFileEntries.Count));

                if (OspreyDiagnostics.DumpMulticharge)
                {
                    var perFileForDump = new List<KeyValuePair<string,
                        IReadOnlyList<FdrEntry>>>(_perFileEntries.Count);
                    foreach (var kvp in _perFileEntries)
                    {
                        perFileForDump.Add(new KeyValuePair<string,
                            IReadOnlyList<FdrEntry>>(kvp.Key, kvp.Value));
                    }
                    OspreyDiagnostics.WriteStage6MultichargeDump(
                        perFileForDump, perFileConsensusTargets);
                    if (OspreyDiagnostics.MultichargeOnly)
                        OspreyDiagnostics.ExitAfterDump(@"OSPREY_MULTICHARGE_ONLY");
                }

                // 2. Cross-run consensus RTs (target peptides + paired
                //    decoys, sigmoid(score)-weighted median, hard
                //    run_precursor_qvalue gate).
                var perFileForRecon = new List<KeyValuePair<string,
                    IReadOnlyList<FdrEntry>>>(_perFileEntries.Count);
                foreach (var kvp in _perFileEntries)
                {
                    perFileForRecon.Add(new KeyValuePair<string,
                        IReadOnlyList<FdrEntry>>(kvp.Key, kvp.Value));
                }
                // Cross-impl bisection trace for InversePredict: if the
                // OSPREY_DUMP_INV_PREDICT env var is set, ConsensusRts
                // populates this list with one row per detection. The
                // caller drives the dump via OspreyDiagnostics so the
                // FDR project doesn't have to know about the diagnostic
                // file format.
                List<InvPredictRecord> invPredictTrace = null;
                if (OspreyDiagnostics.DumpInvPredict)
                    invPredictTrace = new List<InvPredictRecord>();

                // Cross-file consensus is only meaningful with > 1 file.
                // Mirrors Rust pipeline.rs:4146 where reconciliation_enabled
                // requires per_file_entries.len() > 1 — single-file runs
                // skip consensus computation, refit, and reconciliation
                // entirely, leaving multi-charge consensus rescore as the
                // only Stage 6 work performed.
                IReadOnlyList<PeptideConsensusRT> consensus =
                    _perFileEntries.Count > 1
                        ? ConsensusRts.Compute(
                            perFileForRecon, _perFileCalibrations,
                            config.Reconciliation.ConsensusFdr,
                            config.ProteinFdr ?? 0.0,
                            invPredictTrace)
                        : Array.Empty<PeptideConsensusRT>();

                if (invPredictTrace != null)
                {
                    OspreyDiagnostics.WriteStage6InvPredictDump(invPredictTrace);
                    if (OspreyDiagnostics.InvPredictOnly)
                        OspreyDiagnostics.ExitAfterDump(@"OSPREY_INV_PREDICT_ONLY");
                }
                int nTargets = 0, nDecoys = 0;
                foreach (var c in consensus)
                {
                    if (c.IsDecoy) nDecoys++;
                    else nTargets++;
                }
                ctx.LogInfo(string.Format(
                    @"Stage 6 consensus: {0} target peptides, {1} decoy peptides",
                    nTargets, nDecoys));

                // Skip the dump on empty consensus to match Rust's
                // dump_stage6_consensus, which silently elides the
                // file when there is nothing to write (the dump is
                // gated on Some(file) in Rust, derived from
                // !consensus.is_empty()). Without this gate, C#
                // emits a header-only cs_stage6_consensus.tsv and
                // Test-Regression sees an asymmetric-absence FAIL
                // even though both sides agree on the empty result.
                if (OspreyDiagnostics.DumpConsensus && consensus.Count > 0)
                {
                    OspreyDiagnostics.WriteStage6ConsensusDump(consensus);
                    if (OspreyDiagnostics.ConsensusOnly)
                        OspreyDiagnostics.ExitAfterDump(@"OSPREY_CONSENSUS_ONLY");
                }

                // 3. Per-file calibration refit on consensus peptides.
                var refinedCalibrations = new Dictionary<string, RTCalibration>();
                foreach (var kvp in _perFileEntries)
                {
                    var refined = CalibrationRefit.Refit(consensus, kvp.Value,
                        config.Reconciliation.ConsensusFdr);
                    if (refined != null)
                        refinedCalibrations[kvp.Key] = refined;
                }
                ctx.LogInfo(string.Format(
                    @"Stage 6 refit: {0}/{1} files produced refined calibrations",
                    refinedCalibrations.Count, _perFileEntries.Count));

                if (OspreyDiagnostics.DumpLoessFit)
                {
                    OspreyDiagnostics.WriteStage6LoessFitDump(refinedCalibrations);
                    if (OspreyDiagnostics.LoessFitOnly)
                        OspreyDiagnostics.ExitAfterDump(@"OSPREY_LOESS_FIT_ONLY");
                }

                if (OspreyDiagnostics.DumpRefit)
                {
                    OspreyDiagnostics.WriteStage6RefitDump(refinedCalibrations);
                    if (OspreyDiagnostics.RefitOnly)
                        OspreyDiagnostics.ExitAfterDump(@"OSPREY_REFIT_ONLY");
                }

                // 4. Reconciliation planning. Reads each file's CWT
                //    candidates from the parquet cache and asks the
                //    planner to choose, per (file, entry), whether to
                //    keep the existing peak, switch to a stored CWT
                //    candidate at the consensus RT, or force an
                //    integration window. Mirrors Rust pipeline.rs
                //    reconciliation block at ~3260-3380.
                IReadOnlyDictionary<(string File, int Index), ReconcileAction> reconciliationActions = null;
                var perFileCwtCandidates = new Dictionary<string,
                    IReadOnlyList<IReadOnlyList<CwtCandidate>>>();
                foreach (var kvp in _perFileEntries)
                {
                    if (_perFileParquetPaths.TryGetValue(kvp.Key, out string parquetPath) &&
                        File.Exists(parquetPath))
                    {
                        try
                        {
                            var cwtRows = ParquetScoreCache
                                .LoadCwtCandidatesFromParquet(parquetPath);
                            // The planner indexes CWT lists by
                            // entry.ParquetIndex (mirrors Rust at
                            // reconciliation.rs:672). cwtRows.Count is
                            // the parquet's raw Stage-4 row count;
                            // kvp.Value.Count is the post-first-pass-
                            // compaction stub count. They are not
                            // equal by design — what we actually need
                            // to validate is that every stub's
                            // ParquetIndex falls within cwtRows.
                            uint maxIdx = 0;
                            foreach (var entry in kvp.Value)
                            {
                                if (entry.ParquetIndex > maxIdx)
                                    maxIdx = entry.ParquetIndex;
                            }
                            if (kvp.Value.Count > 0 && maxIdx >= cwtRows.Count)
                            {
                                ctx.LogWarning(string.Format(
                                    @"CWT candidate row count out of range for {0}: " +
                                    @"max stub ParquetIndex={1}, parquet has {2} rows -- " +
                                    @"skipping reconciliation planning for this file",
                                    kvp.Key, maxIdx, cwtRows.Count));
                                continue;
                            }
                            var converted = new List<IReadOnlyList<CwtCandidate>>(cwtRows.Count);
                            foreach (var row in cwtRows)
                                converted.Add(row);
                            perFileCwtCandidates[kvp.Key] = converted;
                        }
                        catch (Exception ex)
                        {
                            ctx.LogWarning(string.Format(
                                @"Failed to load CWT candidates for {0}: {1}",
                                kvp.Key, ex.Message));
                        }
                    }
                }
                var perFileForPlan = new List<KeyValuePair<string,
                    IReadOnlyList<FdrEntry>>>(_perFileEntries.Count);
                foreach (var kvp in _perFileEntries)
                {
                    perFileForPlan.Add(new KeyValuePair<string,
                        IReadOnlyList<FdrEntry>>(kvp.Key, kvp.Value));
                }
                // Match Rust pipeline.rs:4223: only run the reconciliation
                // planner when the cross-file consensus is non-empty.
                // Single-file runs (or any case where no peptide had
                // enough cross-replicate evidence to form a consensus
                // RT) degenerate to zero reconciliation actions in
                // Rust; C# previously planned regardless and produced
                // ~22k spurious use_cwt actions on Stellar single-file.
                if (perFileCwtCandidates.Count == _perFileEntries.Count
                    && consensus.Count > 0)
                {
                    reconciliationActions = ReconciliationPlanner.Plan(
                        consensus,
                        perFileForPlan,
                        perFileCwtCandidates,
                        refinedCalibrations,
                        _perFileCalibrations,
                        config.Reconciliation.ConsensusFdr);
                    ctx.LogInfo(string.Format(
                        @"Stage 6 reconciliation: {0} per-(file, entry) actions planned",
                        reconciliationActions.Count));
                }
                else if (consensus.Count == 0)
                {
                    ctx.LogInfo(@"Stage 6 reconciliation: skipped (empty consensus; single-file or no cross-file evidence)");
                }
                else
                {
                    ctx.LogInfo(string.Format(
                        @"Stage 6 reconciliation: skipped (CWT candidates loaded for {0}/{1} files)",
                        perFileCwtCandidates.Count, _perFileEntries.Count));
                }

                // Stage 6 cross-impl bisection dump for the planner output.
                // Fires unconditionally when OSPREY_DUMP_RECONCILIATION=1
                // is set so the skipped / empty paths still produce a
                // header-only TSV and still honor OSPREY_RECONCILIATION_ONLY
                // for early exit. Mirrors the Rust side at
                // crates/osprey/src/pipeline.rs after the reconciliation
                // block closes.
                if (OspreyDiagnostics.DumpReconciliation)
                {
                    var dumpActions = reconciliationActions
                        ?? new Dictionary<(string File, int Index), ReconcileAction>();
                    OspreyDiagnostics.WriteStage6ReconciliationDump(
                        dumpActions, perFileForPlan);
                    if (OspreyDiagnostics.ReconciliationOnly)
                        OspreyDiagnostics.ExitAfterDump(@"OSPREY_RECONCILIATION_ONLY");
                }

                // Stage 5 → Stage 6 boundary: write the per-file
                // .reconciliation.json envelope (the .fdr_scores.bin
                // companion was already written above pre-compaction).
                // Pairs with the --join-at-pass=1 --no-join Stage 6
                // worker mode (next sprint).
                //
                // Surfaces gap-fill targets via out param so the in-
                // process Stage 6 rescore call below can execute them.
                int reconWriteFailures = _pipeline.WriteReconciliationFiles(
                    _perFileEntries,
                    reconciliationActions,
                    consensus,
                    refinedCalibrations,
                    _perFileCalibrations,
                    _fullLibrary,
                    _perFileParquetPaths,
                    config,
                    out var perFileGapFillForRescore);

                if (config.StopAfterStage5)
                {
                    if (reconWriteFailures > 0)
                    {
                        ctx.LogError(string.Format(
                            @"--join-at-pass=1 --join-only: {0}/{1} reconciliation.json " +
                            @"writes failed; boundary file pair is incomplete. See warnings above.",
                            reconWriteFailures, _perFileEntries.Count));
                        ctx.ExitCode = 1;
                        return false;
                    }
                    ctx.LogInfo(string.Format(
                        @"--join-at-pass=1 --join-only: Stage 5 + reconciliation planning " +
                        @"complete; wrote {0} reconciliation.json + matching fdr_scores.bin " +
                        @"sidecar pair(s). Exiting before Stage 6 rescore.",
                        _perFileEntries.Count));
                    ctx.ExitCode = 0;
                    return false;
                }

                // Surface outputs for the next task.
                DidPlan = true;
                PerFileConsensusTargets = perFileConsensusTargets;
                ReconciliationActions = reconciliationActions
                    ?? new Dictionary<(string, int), ReconcileAction>();
                RefinedCalibrations = refinedCalibrations;
                PerFileGapFillForRescore = perFileGapFillForRescore
                    ?? new Dictionary<string, List<GapFillTarget>>();
            }

            return true;
        }
    }
}
