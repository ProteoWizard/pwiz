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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR
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
            OspreyFeatureInfo[] featureInfos,
            Action<string> logInfo,
            PercolatorDiagnosticsConfig diagnostics = null,
            string passLabel = @"First-pass",
            Func<string, IReadOnlyList<double[]>> loadFileFeatures = null)
        {
            int numFeatures = featureInfos.Length;

            // Sort each file's entries by EntryId so the SVM working-set
            // selection sees a canonical order regardless of upstream operation
            // history. The 1st-pass input is already entry_id-sorted via
            // DeduplicatePairs (AbstractScoringTask.cs), but the post-rescore
            // pool that feeds 2nd-pass Percolator can have gap-fill entries
            // appended after the sorted pre-existing rows. Re-sorting here
            // guarantees identical iteration order across Rust and Osprey;
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
            // When a per-file feature loader is supplied (issue #4355 Phase 4) the
            // stubs are built without resident feature vectors -- the score pass
            // reloads them per file from parquet. Without a loader the entries must
            // already carry their features (the 2nd-pass reload / resident path).
            var percEntries = PercolatorEntryBuilder.Build(
                perFileEntries, numFeatures, streamFeatures: loadFileFeatures != null,
                out int nWithFeatures, out int nWithoutFeatures,
                out int nInputTargets, out int nInputDecoys);

            logInfo(string.Format(
                "[COUNT] {0} Percolator input: {1} entries ({2} targets, {3} decoys, {4} features)",
                passLabel, percEntries.Count, nInputTargets, nInputDecoys, numFeatures));
            logInfo(string.Format(
                "[COUNT] {0} Percolator features computed: {1} entries with PIN features, {2} fallback",
                passLabel, nWithFeatures, nWithoutFeatures));

            PercolatorResults results = DispatchSvm(
                percEntries, config, featureInfos, logInfo, diagnostics, passLabel, loadFileFeatures);

            // A diagnostic-only (*Only) dump fired inside the engine; it left the
            // run as a pure no-op and signalled here. Stop without scoring the
            // stubs and let the Tasks-layer caller perform the process exit.
            if (results.DiagnosticAbort)
                return true;

            // Zip the SVM results back onto the FdrEntry stubs by position
            // (replaces the former psm_id-keyed resultMap re-join).
            ApplyPercolatorResults(perFileEntries, results);

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
        /// Projection-buffer counterpart of <c>RunPercolatorFdr</c> (issue
        /// #4355 step (b) increment ii): run first-pass Percolator FDR over the thin
        /// <see cref="FdrProjectionSet"/> peak buffer instead of the full
        /// <see cref="FdrEntry"/> stub buffer. The SVM path is UNCHANGED -- the
        /// projection is expanded into the identical <see cref="PercolatorEntry"/>
        /// input (strings materialized from the interned peptide table), the same
        /// streaming/direct dispatch runs, and the results are zipped back onto the
        /// projection rows by position. Only the buffer that stays resident across
        /// the peak differs, so the trained model + every q-value are byte-identical
        /// to the legacy path (the flag-off oracle). Returns <c>true</c> on a
        /// diagnostic-only abort (same contract as the legacy overload).
        /// </summary>
        public static bool RunPercolatorFdr(
            FdrProjectionSet projections,
            OspreyConfig config,
            OspreyFeatureInfo[] featureInfos,
            Action<string> logInfo,
            PercolatorDiagnosticsConfig diagnostics = null,
            string passLabel = @"First-pass",
            Func<string, IReadOnlyList<double[]>> loadFileFeatures = null)
        {
            int numFeatures = featureInfos.Length;
            var peptideById = projections.PeptideById;

            // Same canonical sort as the legacy path, minus ScanNumber: on the
            // first pass each file's parquet is already written in
            // (entry_id, charge, scan_number) order, so ParquetIndex increases with
            // scan_number within a (entry_id, charge) group. Sorting by
            // (EntryId, Charge, ParquetIndex) is therefore a total order that yields
            // the identical sequence the legacy (EntryId, Charge, ScanNumber,
            // ParquetIndex) sort produces (risk #3) -- and a stable no-op on the
            // already-sorted first-pass buffer.
            foreach (var kvp in projections.PerFile)
            {
                kvp.Value.Sort((a, b) =>
                {
                    int c = a.EntryId.CompareTo(b.EntryId);
                    if (c != 0) return c;
                    c = a.Charge.CompareTo(b.Charge);
                    if (c != 0) return c;
                    return a.ParquetIndex.CompareTo(b.ParquetIndex);
                });
            }

            var percEntries = PercolatorEntryBuilder.BuildFromProjection(
                projections.PerFile, peptideById, numFeatures,
                streamFeatures: loadFileFeatures != null,
                out int nWithFeatures, out int nWithoutFeatures,
                out int nInputTargets, out int nInputDecoys);

            logInfo(string.Format(
                "[COUNT] {0} Percolator input: {1} entries ({2} targets, {3} decoys, {4} features)",
                passLabel, percEntries.Count, nInputTargets, nInputDecoys, numFeatures));
            logInfo(string.Format(
                "[COUNT] {0} Percolator features computed: {1} entries with PIN features, {2} fallback",
                passLabel, nWithFeatures, nWithoutFeatures));

            PercolatorResults results = DispatchSvm(
                percEntries, config, featureInfos, logInfo, diagnostics, passLabel, loadFileFeatures);

            if (results.DiagnosticAbort)
                return true;

            ApplyPercolatorResultsToProjection(projections.PerFile, results);

            // FDR result logging, reading the projection rows. Identical values to
            // the legacy loops (same EntryId/IsDecoy/q-values), so identical [COUNT]
            // lines.
            int nTargetPassing = 0;
            int nDecoyPassing = 0;
            foreach (var kvp in projections.PerFile)
            {
                int fileTargets = 0;
                int fileDecoys = 0;
                foreach (var proj in kvp.Value)
                {
                    if (proj.EffectiveRunQvalue(config.FdrLevel) <= config.RunFdr)
                    {
                        if (proj.IsDecoy)
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

            var bestQByPrecursor = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var kvp in projections.PerFile)
            {
                foreach (var proj in kvp.Value)
                {
                    if (proj.IsDecoy)
                        continue;
                    if (proj.EffectiveRunQvalue(config.FdrLevel) > config.RunFdr)
                        continue;
                    string pkey = peptideById[proj.PeptideId] + "|" + proj.Charge;
                    double q = proj.EffectiveRunQvalue(config.FdrLevel);
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
        /// Shared SVM dispatch for the legacy and projection first-pass paths: build
        /// the <see cref="PercolatorConfig"/>, log the "Running..." header, and pick
        /// the streaming vs direct path off the same <c>MaxTrainSize * 2</c>
        /// threshold. Pure code motion out of <c>RunPercolatorFdr</c> so both
        /// buffer shapes drive the identical, parity-locked SVM core -- the projection
        /// path cannot silently diverge in dispatch, standardizer, or subsample.
        /// </summary>
        private static PercolatorResults DispatchSvm(
            List<PercolatorEntry> percEntries,
            OspreyConfig config,
            OspreyFeatureInfo[] featureInfos,
            Action<string> logInfo,
            PercolatorDiagnosticsConfig diagnostics,
            string passLabel,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures)
        {
            var percConfig = new PercolatorConfig
            {
                TrainFdr = config.RunFdr,
                TestFdr = config.RunFdr,
                MaxIterations = 10,
                NFolds = 3,
                FeatureInfos = featureInfos,
                Diagnostics = diagnostics
            };

            // Section header (full input population). The cross-validation fold count and the
            // actual training-subset size are reported by RunPercolator once the subsample is
            // built, just above the per-iteration percent lines.
            logInfo(string.Format("Running {0} Percolator on {1} entries...",
                passLabel, percEntries.Count));

            // Streaming vs direct dispatch, matching Rust
            // osprey/src/pipeline.rs::run_percolator_fdr. Above the
            // MaxTrainSize * 2 threshold the training set is dominated by
            // multi-observation-per-precursor redundancy; best-per-precursor
            // dedup + peptide-grouped subsample give the SVM a diverse
            // per-peptide training pool (same approach mokapot takes) and
            // keep the Stage 5 standardizer fit on the subset -- essential
            // for cross-impl byte parity with Rust once Astral-scale inputs
            // push past the threshold.
            if (percConfig.MaxTrainSize > 0 &&
                percEntries.Count > percConfig.MaxTrainSize * 2)
            {
                return RunPercolatorStreaming(
                    percEntries, percConfig, logInfo, passLabel, loadFileFeatures);
            }

            // Direct path (<= MaxTrainSize * 2 entries). On the streaming build
            // the stubs have no features yet; load every entry's vector up
            // front (bounded by this branch's size) so RunPercolator sees real
            // features. Without a loader the entries already carry them.
            if (loadFileFeatures != null)
                PopulateFeaturesFromFiles(percEntries, loadFileFeatures, featureInfos.Length);
            return PercolatorFdr.RunPercolator(percEntries, percConfig);
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
        /// Zip Percolator results back onto the <see cref="FdrEntry"/> stubs by
        /// position. <see cref="PercolatorEntryBuilder.Build"/> emits exactly one
        /// <see cref="PercolatorEntry"/> per stub in nested (file, entry) order,
        /// and both SVM paths return <see cref="PercolatorResults.Entries"/>
        /// index-aligned to that input (the direct and streaming result assembly
        /// in <see cref="PercolatorFdr"/>). Walking <paramref name="perFileEntries"/>
        /// in that same nested order therefore pairs each stub with its own result,
        /// which is why the former psm_id string + resultMap re-join was pure
        /// redundancy (issue #4355 step (b)): it re-joined by a key that position
        /// already determines. Row order is a single source of truth -- the buffer
        /// is built once, sorted once (in <c>RunPercolatorFdr</c>), and not
        /// mutated between the build and this write-back. Mirrors the Rust direct
        /// path, which likewise zips by index.
        /// </summary>
        internal static void ApplyPercolatorResults(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            PercolatorResults results)
        {
            var resultEntries = results.Entries;
            int resultIndex = 0;
            foreach (var kvp in perFileEntries)
            {
                foreach (var fdrEntry in kvp.Value)
                {
                    var result = resultEntries[resultIndex++];
                    fdrEntry.Score = result.Score;
                    fdrEntry.RunPrecursorQvalue = result.RunPrecursorQvalue;
                    fdrEntry.RunPeptideQvalue = result.RunPeptideQvalue;
                    fdrEntry.ExperimentPrecursorQvalue = result.ExperimentPrecursorQvalue;
                    fdrEntry.ExperimentPeptideQvalue = result.ExperimentPeptideQvalue;
                    fdrEntry.Pep = result.Pep;
                }
            }

            // Guard the index-alignment invariant the zip depends on: the builder
            // emitted exactly one result per stub. A count mismatch would silently
            // misbind scores to the wrong entries, so fail loudly -- this is
            // byte-identity-critical first-pass FDR output.
            if (resultIndex != resultEntries.Count)
            {
                throw new InvalidOperationException(string.Format(
                    "Percolator result count ({0}) does not match FdrEntry stub count ({1}); " +
                    "the index-zip write-back requires them to be equal.",
                    resultEntries.Count, resultIndex));
            }
        }

        /// <summary>
        /// Projection-buffer counterpart of <see cref="ApplyPercolatorResults"/>
        /// (issue #4355 step (b) increment ii): zip the SVM results back onto the
        /// <see cref="FdrProjection"/> rows by position. <c>FdrProjection</c> is a
        /// readonly struct, so each row is replaced in place on its backing list via
        /// <see cref="FdrProjection.WithPercolatorResults"/> (Score + the four
        /// run/experiment precursor+peptide q-values + PEP; RunProteinQvalue is set
        /// later by first-pass protein FDR). Same nested (file, entry) walk and same
        /// count guard as the FdrEntry overload.
        /// </summary>
        internal static void ApplyPercolatorResultsToProjection(
            List<KeyValuePair<string, List<FdrProjection>>> perFileProjections,
            PercolatorResults results)
        {
            var resultEntries = results.Entries;
            int resultIndex = 0;
            foreach (var kvp in perFileProjections)
            {
                var rows = kvp.Value;
                for (int i = 0; i < rows.Count; i++)
                {
                    var result = resultEntries[resultIndex++];
                    rows[i] = rows[i].WithPercolatorResults(
                        result.Score, result.RunPrecursorQvalue, result.RunPeptideQvalue,
                        result.ExperimentPrecursorQvalue, result.ExperimentPeptideQvalue,
                        result.Pep);
                }
            }

            if (resultIndex != resultEntries.Count)
            {
                throw new InvalidOperationException(string.Format(
                    "Percolator result count ({0}) does not match FdrProjection row count ({1}); " +
                    "the index-zip write-back requires them to be equal.",
                    resultEntries.Count, resultIndex));
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
            string passLabel,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures = null)
        {
            int n = percEntries.Count;
            int maxTrain = percConfig.MaxTrainSize;

            // Pull labels / entry IDs / peptides into flat arrays for the
            // subset helpers. On the streaming build the stubs carry no feature
            // vector, so best-per-precursor cannot read Features[0]; feed it the
            // resident CoelutionSum scalar instead (byte-identical to Features[0]
            // on the first pass -- CoelutionScorer assigns CoelutionSum from
            // features[0]). Without a loader the entries carry features, so leave
            // bestScores null and let the selection read Features[0] as before.
            double[] bestScores = loadFileFeatures != null ? new double[n] : null;
            var labels = new bool[n];
            var entryIds = new uint[n];
            var peptides = new string[n];
            for (int i = 0; i < n; i++)
            {
                labels[i] = percEntries[i].IsDecoy;
                entryIds[i] = percEntries[i].EntryId;
                peptides[i] = percEntries[i].Peptide;
                if (bestScores != null)
                    bestScores[i] = percEntries[i].CoelutionSum;
            }

            // 1. Best-per-precursor dedup, then 2. peptide-grouped subsample when
            //    the dedup count still exceeds MaxTrainSize. Both steps are owned
            //    by PercolatorFdr.BuildTrainingSubset so this streaming path and
            //    the direct path select identical subsets for identical input.
            int[] bestIdx;
            int[] trainSubsetGlobalIdx = PercolatorFdr.BuildTrainingSubset(
                labels, entryIds, peptides, percEntries, maxTrain, percConfig.Seed,
                out bestIdx, bestScores);
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

            // On the streaming build, load ONLY the subset's feature vectors
            // (issue #4355 Phase 4) -- one file at a time, cloning each row so the
            // subset entry owns it and the file's row list can be released -- so
            // RunPercolator trains on real features without the full population's
            // O(N) vectors ever being resident. subsetEntries are references into
            // percEntries, so this also makes the vectors visible to the subset's
            // held-out CV scoring inside RunPercolator.
            if (loadFileFeatures != null)
            {
                var subsetByFile = PercolatorFdr.GroupIndicesByFileName(subsetEntries);
                foreach (var kvp in subsetByFile)
                {
                    IReadOnlyList<double[]> rows = loadFileFeatures(kvp.Key);
                    foreach (int k in kvp.Value)
                    {
                        var entry = subsetEntries[k];
                        entry.Features = (double[])PercolatorFdr.ResolveFeatureRow(
                            rows, entry.ParquetIndex, entry.CoelutionSum,
                            percConfig.FeatureInfos.Length).Clone();
                    }
                }
            }

            var trainConfig = new PercolatorConfig
            {
                TrainFdr = percConfig.TrainFdr,
                TestFdr = percConfig.TestFdr,
                MaxIterations = percConfig.MaxIterations,
                NFolds = percConfig.NFolds,
                Seed = percConfig.Seed,
                CValues = percConfig.CValues,
                MaxTrainSize = percConfig.MaxTrainSize,
                FeatureInfos = percConfig.FeatureInfos,
                TrainOnly = true,
                Diagnostics = percConfig.Diagnostics
            };
            PercolatorResults trainResults = PercolatorFdr.RunPercolator(subsetEntries, trainConfig);

            // A diagnostic-only (*Only) dump can fire during the train-only pass
            // (standardizer / subsample / SVM-weights dumps all run there);
            // forward the abort sentinel so RunPercolatorFdr stops before scoring.
            if (trainResults.DiagnosticAbort)
                return trainResults;

            // 4. Apply averaged model to ALL entries and compute q-values. The
            //    score pass reloads features one file at a time via loadFileFeatures
            //    (issue #4355 Phase 4), keeping only the scalar scores resident.
            return PercolatorFdr.ScorePopulationAndComputeFdr(
                percEntries, trainResults, percConfig, loadFileFeatures);
        }

        /// <summary>
        /// Load every entry's 21-feature vector from its source file's parquet by
        /// <see cref="PercolatorEntry.ParquetIndex"/>, one file at a time, and
        /// assign it onto the stub. Used by the direct path (issue #4355 Phase 4)
        /// where the population is bounded (&lt;= MaxTrainSize * 2) so holding all
        /// vectors is acceptable, but they still must be reloaded because the
        /// streaming build left them null. Mirrors the per-file reload the Tasks
        /// layer previously performed before Percolator.
        /// </summary>
        private static void PopulateFeaturesFromFiles(
            List<PercolatorEntry> percEntries,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures,
            int numFeatures)
        {
            var indicesByFile = PercolatorFdr.GroupIndicesByFileName(percEntries);
            foreach (var kvp in indicesByFile)
            {
                IReadOnlyList<double[]> rows = loadFileFeatures(kvp.Key);
                foreach (int i in kvp.Value)
                {
                    var entry = percEntries[i];
                    entry.Features = PercolatorFdr.ResolveFeatureRow(
                        rows, entry.ParquetIndex, entry.CoelutionSum, numFeatures);
                }
            }
        }
    }
}
