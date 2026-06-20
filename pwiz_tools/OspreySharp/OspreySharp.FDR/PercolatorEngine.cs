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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.FDR
{
    /// <summary>
    /// Owns Percolator-based run-level FDR orchestration: build the flat
    /// <see cref="PercolatorEntry"/> input from <see cref="FdrEntry"/> stubs,
    /// dispatch to the direct or streaming SVM path, and write the resulting
    /// scores / q-values back onto the stubs. Moved out of the Tasks layer
    /// (the former <c>FirstJoinTask.RunPercolatorFdr</c>) so FDR orchestration
    /// physically lives in the FDR project; the Tasks layer calls this through
    /// a thin facade, passing <c>ctx.LogInfo</c> as the log sink and the PIN
    /// feature names. Pure: takes data + a log delegate, never the pipeline
    /// context -- no diagnostics dump, no process-exit, no byproduct registry
    /// (those stay in the Tasks facade).
    /// </summary>
    public static class PercolatorEngine
    {
        /// <summary>
        /// Run Percolator-based FDR control. Builds PercolatorEntry objects from
        /// FdrEntry stubs and runs Percolator, then maps results back onto the
        /// stubs. Static so the second-pass run after Stage 6 reconciliation
        /// (driven from the Tasks layer for the HPC distribution case where
        /// workers wrote reconciled .scores.parquet but no
        /// .2nd-pass.fdr_scores.bin sidecars; mirrors Rust
        /// pipeline.rs:4394-4468) can call it as well as the first-pass run. The
        /// <paramref name="diagnostics"/> Stage 5 dump gates are <c>null</c>
        /// (diagnostics off) on every production run.
        /// </summary>
        /// <returns><c>true</c> when a diagnostic-only (<c>*Only</c>) dump fired and
        /// the run should stop early -- the Tasks-layer caller owns the process exit;
        /// <c>false</c> on a normal completion (the FdrEntry stubs are scored and
        /// q-valued).</returns>
        public static bool RunPercolatorFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            OspreyConfig config,
            string[] featureNames,
            Action<string> logInfo,
            PercolatorDiagnosticsConfig diagnostics = null,
            string passLabel = @"First-pass")
        {
            int numFeatures = featureNames.Length;

            // Sort each file's entries by EntryId so the SVM working-set
            // selection sees a canonical order regardless of upstream operation
            // history. The 1st-pass input is already entry_id-sorted via
            // DeduplicatePairs (AbstractScoringTask.cs), but the post-rescore
            // pool that feeds 2nd-pass Percolator can have gap-fill entries
            // appended after the sorted pre-existing rows. Re-sorting here
            // guarantees identical iteration order across Rust and OspreySharp;
            // without it, gap-fill ordering diverges and the cross-impl 2nd-pass
            // scores drift on multi-file datasets even when feature columns are
            // bit-equal. Mirrors Rust pipeline.rs::run_percolator_fdr.
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

            // Build the flat PercolatorEntry list (one per observation), preferring
            // each entry's stored 21-feature vector and falling back to a basic
            // vector for stubs. Extracted to PercolatorEntryBuilder.
            var percEntries = PercolatorEntryBuilder.Build(
                perFileEntries, numFeatures,
                out int nWithFeatures, out int nWithoutFeatures,
                out int nInputTargets, out int nInputDecoys);

            logInfo(string.Format(
                "[COUNT] {0} Percolator input: {1} entries ({2} targets, {3} decoys, {4} features)",
                passLabel, percEntries.Count, nInputTargets, nInputDecoys, numFeatures));
            logInfo(string.Format(
                "[COUNT] {0} Percolator features computed: {1} entries with PIN features, {2} fallback",
                passLabel, nWithFeatures, nWithoutFeatures));

            logInfo(string.Format("Running {0} Percolator on {1} entries...",
                passLabel, percEntries.Count));

            var percConfig = new PercolatorConfig
            {
                TrainFdr = config.RunFdr,
                TestFdr = config.RunFdr,
                MaxIterations = 10,
                NFolds = 3,
                FeatureNames = featureNames,
                Diagnostics = diagnostics
            };

            // Streaming vs direct dispatch, matching Rust
            // osprey/src/pipeline.rs::run_percolator_fdr. Above the
            // MaxTrainSize * 2 threshold the training set is dominated by
            // multi-observation-per-precursor redundancy; best-per-precursor
            // dedup + peptide-grouped subsample give the SVM a diverse
            // per-peptide training pool (same approach mokapot takes) and
            // keep the Stage 5 standardizer fit on the subset -- essential
            // for cross-impl byte parity with Rust once Astral-scale inputs
            // push past the threshold.
            PercolatorResults results;
            if (percConfig.MaxTrainSize > 0 &&
                percEntries.Count > percConfig.MaxTrainSize * 2)
            {
                results = RunPercolatorStreaming(percEntries, percConfig, logInfo, passLabel);
            }
            else
            {
                results = PercolatorFdr.RunPercolator(percEntries, percConfig);
            }

            // A diagnostic-only (*Only) dump fired inside the engine; it left the
            // run as a pure no-op and signalled here. Stop without scoring the
            // stubs and let the Tasks-layer caller perform the process exit.
            if (results.DiagnosticAbort)
                return true;

            // Map results back to FdrEntry stubs
            var resultMap = new Dictionary<string, PercolatorResult>();
            foreach (var result in results.Entries)
                resultMap[result.Id] = result;

            foreach (var kvp in perFileEntries)
            {
                string fileName = kvp.Key;
                foreach (var fdrEntry in kvp.Value)
                {
                    // 4-component psm_id matches the construction in
                    // the loop above so each FdrEntry pulls back its
                    // own PercolatorResult. Mirrors Rust direct path.
                    string id = string.Format("{0}_{1}_{2}_{3}",
                        fileName, fdrEntry.ModifiedSequence,
                        fdrEntry.Charge, fdrEntry.ScanNumber);
                    PercolatorResult result;
                    if (resultMap.TryGetValue(id, out result))
                    {
                        fdrEntry.Score = result.Score;
                        fdrEntry.RunPrecursorQvalue = result.RunPrecursorQvalue;
                        fdrEntry.RunPeptideQvalue = result.RunPeptideQvalue;
                        fdrEntry.ExperimentPrecursorQvalue = result.ExperimentPrecursorQvalue;
                        fdrEntry.ExperimentPeptideQvalue = result.ExperimentPeptideQvalue;
                        fdrEntry.Pep = result.Pep;
                    }
                }
            }
            // Log FDR results
            int nTargetPassing = 0;
            int nDecoyPassing = 0;
            foreach (var kvp in perFileEntries)
            {
                int fileTargets = 0;
                int fileDecoys = 0;
                foreach (var entry in kvp.Value)
                {
                    if (entry.EffectiveRunQvalue(config.FdrLevel) <= config.RunFdr)
                    {
                        if (entry.IsDecoy)
                            fileDecoys++;
                        else
                            fileTargets++;
                    }
                }
                logInfo(string.Format(
                    "[COUNT] {0} Percolator pass [{1}]: {2} targets, {3} decoys at {4:P0} FDR",
                    passLabel, kvp.Key, fileTargets, fileDecoys, config.RunFdr));
                nTargetPassing += fileTargets;
                nDecoyPassing += fileDecoys;
            }

            logInfo(string.Format(
                "{0} Percolator results: {1} targets, {2} decoys pass {3:P1} FDR",
                passLabel, nTargetPassing, nDecoyPassing, config.RunFdr));
            logInfo(string.Format(
                "[COUNT] {0} total across files: {1}",
                passLabel, nTargetPassing));

            // Compute unique precursors across files (best q-value per modseq+charge)
            var bestQByPrecursor = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (entry.IsDecoy)
                        continue;
                    if (entry.EffectiveRunQvalue(config.FdrLevel) > config.RunFdr)
                        continue;
                    string pkey = entry.ModifiedSequence + "|" + entry.Charge;
                    double q = entry.EffectiveRunQvalue(config.FdrLevel);
                    double existing;
                    if (!bestQByPrecursor.TryGetValue(pkey, out existing) || q < existing)
                        bestQByPrecursor[pkey] = q;
                }
            }
            logInfo(string.Format(
                "[COUNT] {0} unique precursors (best q across files): {1}",
                passLabel, bestQByPrecursor.Count));
            return false;
        }

        /// <summary>
        /// Run simple target-decoy competition FDR (no machine learning).
        /// Uses coelution_sum as the scoring function.
        /// </summary>
        public static void RunSimpleFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            OspreyConfig config,
            Action<string> logInfo)
        {
            var fdrController = new FdrController(config.RunFdr);

            foreach (var kvp in perFileEntries)
            {
                var result = fdrController.CompeteAndFilter(
                    kvp.Value,
                    e => e.CoelutionSum,
                    e => e.IsDecoy,
                    e => e.EntryId);

                logInfo(string.Format(
                    "  {0}: {1} targets pass (FDR={2:F4}, {3} target wins, {4} decoy wins)",
                    kvp.Key, result.PassingTargets.Count, result.FdrAtThreshold,
                    result.NTargetWins, result.NDecoyWins));

                // Assign q-values based on simple competition
                // Passing targets get fdr_at_threshold, non-passing get 1.0
                var passingIds = new HashSet<uint>();
                foreach (var target in result.PassingTargets)
                    passingIds.Add(target.EntryId);

                foreach (var entry in kvp.Value)
                {
                    if (!entry.IsDecoy && passingIds.Contains(entry.EntryId))
                    {
                        entry.RunPrecursorQvalue = result.FdrAtThreshold;
                        entry.RunPeptideQvalue = result.FdrAtThreshold;
                        entry.ExperimentPrecursorQvalue = result.FdrAtThreshold;
                        entry.ExperimentPeptideQvalue = result.FdrAtThreshold;
                    }
                }
            }
        }

        /// <summary>
        /// Streaming Percolator dispatch for multi-observation-per-precursor
        /// inputs (total entries above <c>MaxTrainSize * 2</c>). Mirrors
        /// Rust's <c>run_percolator_fdr</c> streaming branch
        /// (osprey/src/pipeline.rs:4232-4580):
        /// <list type="number">
        /// <item>Best-per-precursor dedup across all per-file entries (one
        /// target + one decoy per base_id, by Features[0] = coelution_sum).
        /// </item>
        /// <item>Peptide-grouped subsample to <c>MaxTrainSize</c> using the
        /// same XOR-shift RNG and peptide-key sort order as Rust.</item>
        /// <item>Train fold models + standardizer on that subset
        /// (<c>TrainOnly = true</c>) -- the standardizer is fit on the
        /// subset, matching Rust's run_percolator-on-subset behaviour
        /// instead of fitting on the full 1M+ observation pool.</item>
        /// <item>Apply the averaged model to ALL entries for scoring, then
        /// compute PEP and per-run / experiment q-values on that flat
        /// score array.</item>
        /// </list>
        /// The selection uses <see cref="PercolatorFdr.SelectBestPerPrecursor"/>
        /// and <see cref="PercolatorFdr.SubsampleByPeptideGroup"/> -- the
        /// same helpers the direct path calls internally, so both paths
        /// select identical 300K subsets when given identical input.
        /// </summary>
        private static PercolatorResults RunPercolatorStreaming(
            List<PercolatorEntry> percEntries,
            PercolatorConfig percConfig,
            Action<string> logInfo,
            string passLabel)
        {
            int n = percEntries.Count;
            int maxTrain = percConfig.MaxTrainSize;

            // Pull labels / entry IDs / peptides into flat arrays for the
            // subset helpers.
            var labels = new bool[n];
            var entryIds = new uint[n];
            var peptides = new string[n];
            for (int i = 0; i < n; i++)
            {
                labels[i] = percEntries[i].IsDecoy;
                entryIds[i] = percEntries[i].EntryId;
                peptides[i] = percEntries[i].Peptide;
            }

            // 1. Best-per-precursor dedup, then 2. peptide-grouped subsample when
            //    the dedup count still exceeds MaxTrainSize. Both steps are owned
            //    by PercolatorFdr.BuildTrainingSubset so this streaming path and
            //    the direct path select identical subsets for identical input.
            int[] bestIdx;
            int[] trainSubsetGlobalIdx = PercolatorFdr.BuildTrainingSubset(
                labels, entryIds, peptides, percEntries, maxTrain, percConfig.Seed,
                out bestIdx);
            int dedupTargets = 0, dedupDecoys = 0;
            for (int i = 0; i < bestIdx.Length; i++)
            {
                if (labels[bestIdx[i]]) dedupDecoys++;
                else dedupTargets++;
            }
            logInfo(string.Format(
                "[COUNT] {0} Percolator streaming best-per-precursor: {1} entries ({2} targets, {3} decoys) from {4} total",
                passLabel, bestIdx.Length, dedupTargets, dedupDecoys, n));

            int subTargets = 0, subDecoys = 0;
            for (int i = 0; i < trainSubsetGlobalIdx.Length; i++)
            {
                if (labels[trainSubsetGlobalIdx[i]]) subDecoys++;
                else subTargets++;
            }
            logInfo(string.Format(
                "[COUNT] {0} Percolator streaming subsample: {1} entries ({2} targets, {3} decoys)",
                passLabel, trainSubsetGlobalIdx.Length, subTargets, subDecoys));

            // 3. Build subset entry list + train.
            var subsetEntries = new List<PercolatorEntry>(trainSubsetGlobalIdx.Length);
            foreach (int i in trainSubsetGlobalIdx)
                subsetEntries.Add(percEntries[i]);
            var trainConfig = new PercolatorConfig
            {
                TrainFdr = percConfig.TrainFdr,
                TestFdr = percConfig.TestFdr,
                MaxIterations = percConfig.MaxIterations,
                NFolds = percConfig.NFolds,
                Seed = percConfig.Seed,
                CValues = percConfig.CValues,
                MaxTrainSize = percConfig.MaxTrainSize,
                FeatureNames = percConfig.FeatureNames,
                TrainOnly = true,
                Diagnostics = percConfig.Diagnostics
            };
            PercolatorResults trainResults = PercolatorFdr.RunPercolator(subsetEntries, trainConfig);

            // A diagnostic-only (*Only) dump can fire during the train-only pass
            // (standardizer / subsample / SVM-weights dumps all run there);
            // forward the abort sentinel so RunPercolatorFdr stops before scoring.
            if (trainResults.DiagnosticAbort)
                return trainResults;

            // 4. Apply averaged model to ALL entries and compute q-values.
            return PercolatorFdr.ScorePopulationAndComputeFdr(percEntries, trainResults, percConfig);
        }
    }
}
