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
            out FeatureContributions contributions,
            PercolatorDiagnosticsConfig diagnostics = null,
            string passLabel = @"First-pass",
            Func<string, IReadOnlyList<double[]>> loadFileFeatures = null,
            Action<PercolatorResults> captureModel = null)
        {
            contributions = null;
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
                // Array.Sort OK: the terminal key is ParquetIndex, which is unique per row,
                // so the comparator never returns 0 and the unstable-sort tie path is unreachable.
                kvp.Value.Sort((a, b) => // Array.Sort OK: (see above) terminal key ParquetIndex is unique per row, comparator never ties
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

            var percConfig = BuildProjectionPercolatorConfig(config, featureInfos, diagnostics);
            PercolatorResults results = DispatchSvm(
                percEntries, percConfig, logInfo, passLabel, loadFileFeatures);

            // Surface the trained model's feature contributions to the caller
            // (the --model-diagnostics report reads them). Computed already; this
            // is a pure hand-off, no behavior change on any production path.
            contributions = results.FeatureContributions;

            // Frozen-model capture hook (OSPREY_PASS2_QVALUE=transfer): the caller
            // can grab the trained model (FoldWeights / FoldBiases / Standardizer)
            // here so a later 2nd-pass step re-scores reconciled features with this
            // FROZEN 1st-pass model instead of retraining. No-op (null) on every
            // default percolator run, so scoring stays byte-identical.
            captureModel?.Invoke(results);

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
        /// projection is scored through the identical <see cref="PercolatorEntry"/>
        /// SVM core (strings materialized from the interned peptide table), the same
        /// streaming dispatch runs, and the results are written back onto the
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
            IFdrOutputSink sink,
            PercolatorDiagnosticsConfig diagnostics = null,
            string passLabel = @"First-pass",
            Func<string, IReadOnlyList<double[]>> loadFileFeatures = null,
            Action<FeatureContributions> captureContributions = null)
        {
            // The lean FdrProjection no longer stores the q-value outputs (issue #4355
            // struct-shrink S0): the score pass hands them to this per-pass sink (the
            // 1st pass parks them in a parallel array; the 2nd pass streams them to the
            // sidecar), and the sink owns the tail [COUNT] tally. A null sink is a bug.
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));
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
                kvp.Value.Sort((a, b) => // Array.Sort OK: terminal key ParquetIndex is unique per distinct (entry_id,charge,scan) observation (1st pass: original-parquet row; 2nd pass: reconciled row); the only possible tie is two rows collapsed to one reconciled row, which are IDENTICAL (same features, Score, entry_id, sidecar record), so their order is irrelevant to the output.
                {
                    int c = a.EntryId.CompareTo(b.EntryId);
                    if (c != 0) return c;
                    c = a.Charge.CompareTo(b.Charge);
                    if (c != 0) return c;
                    return a.ParquetIndex.CompareTo(b.ParquetIndex);
                });
            }

            // The projection path always STREAMS its features per file (for both the
            // 1st and 2nd pass), so a per-file feature loader is mandatory -- the
            // 1st-pass caller (FirstJoinTask) and the 2nd-pass caller (Pass2FdrSidecar)
            // both supply one. A null loader reaching here is a bug, not a cue to fall
            // back to a resident build (that is the flag-off FdrEntry oracle's job).
            if (loadFileFeatures == null)
                throw new InvalidOperationException(
                    @"The projection RunPercolatorFdr overload always streams features " +
                    @"per file; a per-file feature loader is required. A null loader here " +
                    @"is a bug -- the resident build is the flag-off FdrEntry path.");

            var percConfig = BuildProjectionPercolatorConfig(config, featureInfos, diagnostics);
            int n = projections.TotalRows;

            // Streaming-only (cross-impl parity with the Rust streaming-only change):
            // ALWAYS run the projection-native streaming score + compete pass, regardless
            // of population size. The former sub-threshold direct branch built a full
            // PercolatorEntry list, dispatched the SVM over ALL entries, and zipped a
            // PercolatorResult list back; unifying on streaming removes that transient
            // full-population stack and the divergent standardizer-fit population (the
            // direct path fit on all entries, streaming fits on the best-per-precursor
            // subsample). One path, lower memory, and matched to Rust.
            LogProjectionInputCounts(
                projections, numFeatures, loadFileFeatures, logInfo, passLabel);
            logInfo(string.Format("Running {0} Percolator on {1} entries...",
                passLabel, n));
            bool streamingAbort = RunStreamingIntoProjection(
                projections.PerFile, peptideById, percConfig, logInfo, passLabel,
                loadFileFeatures, sink, captureContributions);
            if (streamingAbort)
                return true;

            // Tail [COUNT] logging (per-file pass counts, total, unique precursors)
            // moves into sink.Finish (issue #4355 struct-shrink S0, correction §0a):
            // the q-values are no longer on the struct, so the tally must read the
            // LIVE values the sink accumulated during the score pass (the 1st-pass
            // parallel array or the 2nd-pass streamed values) -- reading them off the
            // projection here would read the now-absent fields. The sink also flushes
            // any deferred per-file output (2nd-pass sidecars). Same values, same
            // FdrLevel selection, so identical [COUNT] lines.
            sink.Finish(logInfo);
            return false;
        }

        /// <summary>
        /// Build the first-pass <see cref="PercolatorConfig"/> shared by the legacy
        /// <see cref="FdrEntry"/> path and the projection path. Centralized (issue
        /// #4355 step (b) increment iii) so every SVM knob is IDENTICAL whether the
        /// SVM runs off a <see cref="PercolatorEntry"/> list or the projection row
        /// count -- the two buffer shapes cannot select a different SVM path for the
        /// same population. <c>MaxTrainSize</c> (the streaming training-subsample size)
        /// is left at the <see cref="PercolatorConfig"/> default (300000).
        /// </summary>
        private static PercolatorConfig BuildProjectionPercolatorConfig(
            OspreyConfig config,
            OspreyFeatureInfo[] featureInfos,
            PercolatorDiagnosticsConfig diagnostics)
        {
            return new PercolatorConfig
            {
                TrainFdr = config.RunFdr,
                TestFdr = config.RunFdr,
                MaxIterations = 10,
                NFolds = 3,
                FeatureInfos = featureInfos,
                // Collect per-feature target/decoy score histograms only for the
                // model-diagnostics report (#4377); off the production path otherwise,
                // so byte-neutral when --model-diagnostics is not requested.
                CollectFeatureHistograms = config.ModelDiagnostics,
                Diagnostics = diagnostics
            };
        }

        /// <summary>
        /// Shared SVM entry point for the legacy and projection first-pass paths: log the
        /// "Running..." header and run the streaming SVM path. Pure code motion out of
        /// <c>RunPercolatorFdr</c> so both buffer shapes drive the identical,
        /// parity-locked SVM core -- the projection path cannot silently diverge in
        /// dispatch, standardizer, or subsample. The <see cref="PercolatorConfig"/> is
        /// built by <see cref="BuildProjectionPercolatorConfig"/> and passed in.
        /// </summary>
        private static PercolatorResults DispatchSvm(
            List<PercolatorEntry> percEntries,
            PercolatorConfig percConfig,
            Action<string> logInfo,
            string passLabel,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures)
        {
            // Section header (full input population). The cross-validation fold count and the
            // actual training-subset size are reported by RunPercolator once the subsample is
            // built, just above the per-iteration percent lines.
            logInfo(string.Format("Running {0} Percolator on {1} entries...",
                passLabel, percEntries.Count));

            // Streaming-only (cross-impl parity with the Rust streaming-only change):
            // ALWAYS take the streaming SVM path -- best-per-precursor dedup +
            // peptide-grouped subsample, with the Stage 5 standardizer fit on that
            // subset. The former sub-threshold direct path trained on ALL entries with a
            // different standardizer-fit population; removed so C# and Rust train
            // identically at every scale (mirrors Rust run_percolator_fdr, now stream-only).
            return RunPercolatorStreaming(
                percEntries, percConfig, logInfo, passLabel, loadFileFeatures);
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

            // Guard the index-alignment invariant the zip depends on BEFORE indexing:
            // the builder emitted exactly one result per stub. Checking first means an
            // undersized result list fails with this clear message instead of an
            // IndexOutOfRangeException thrown mid-zip; a count mismatch would otherwise
            // silently misbind scores to the wrong entries. Fail loudly -- this is
            // byte-identity-critical first-pass FDR output.
            int stubCount = 0;
            foreach (var kvp in perFileEntries)
                stubCount += kvp.Value.Count;
            if (stubCount != resultEntries.Count)
            {
                throw new InvalidOperationException(string.Format(
                    "Percolator result count ({0}) does not match FdrEntry stub count ({1}); " +
                    "the index-zip write-back requires them to be equal.",
                    resultEntries.Count, stubCount));
            }

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
        }

        /// <summary>
        /// Streaming Percolator path for all first-pass inputs (streaming-only:
        /// the former sub-threshold direct branch is removed). Mirrors
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
        /// and <see cref="PercolatorFdr.SubsampleByPeptideGroup"/> to build the
        /// best-per-precursor training subsample -- the same helpers (and the same
        /// 300K cap) Rust's streaming path uses, so the subsets match given
        /// identical input.
        ///
        /// Internal (not private) so <c>FdrTest</c> can drive it head-to-head against
        /// <see cref="RunStreamingIntoProjection"/> on a multi-observation fixture,
        /// proving the two buffer shapes yield identical
        /// Score + q-values (issue #4355 step (b) increment iii, gate 1).
        /// </summary>
        internal static PercolatorResults RunPercolatorStreaming(
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
            //    by PercolatorFdr.BuildTrainingSubset so every caller (this projection
            //    streaming path and the FdrEntry path) selects identical subsets for
            //    identical input.
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
        /// Emit the two "[COUNT] ... Percolator input / features computed" lines for
        /// the streaming projection path directly from the projection rows (issue
        /// #4355 step (b) increment iii), reproducing the same per-file input counts
        /// without building a full-population <see cref="PercolatorEntry"/> list -- so
        /// the streaming path logs identical counts while never materializing that
        /// list. <c>nWithFeatures</c> counts rows with a real parquet row
        /// (<c>ParquetIndex != uint.MaxValue</c>) on the streaming build, matching the
        /// feature-resolver's fallback bookkeeping.
        /// </summary>
        private static void LogProjectionInputCounts(
            FdrProjectionSet projections, int numFeatures,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures,
            Action<string> logInfo, string passLabel)
        {
            bool streamFeatures = loadFileFeatures != null;
            int n = 0, nInputTargets = 0, nInputDecoys = 0;
            int nWithFeatures = 0, nWithoutFeatures = 0;
            foreach (var kvp in projections.PerFile)
            {
                foreach (var proj in kvp.Value)
                {
                    n++;
                    if (proj.IsDecoy)
                        nInputDecoys++;
                    else nInputTargets++;

                    if (streamFeatures)
                    {
                        if (proj.ParquetIndex != uint.MaxValue)
                            nWithFeatures++;
                        else
                            nWithoutFeatures++;
                    }
                    else nWithoutFeatures++;
                }
            }

            logInfo(string.Format(
                "[COUNT] {0} Percolator input: {1} entries ({2} targets, {3} decoys, {4} features)",
                passLabel, n, nInputTargets, nInputDecoys, numFeatures));
            logInfo(string.Format(
                "[COUNT] {0} Percolator features computed: {1} entries with PIN features, {2} fallback",
                passLabel, nWithFeatures, nWithoutFeatures));
        }

        /// <summary>
        /// Projection-native streaming first-pass Percolator (issue #4355 step (b)
        /// increment iii): the memory-collapsing counterpart of
        /// <see cref="RunPercolatorStreaming"/>. It runs the SAME four-phase streaming
        /// flow (best-per-precursor dedup -> peptide-grouped subsample -> train fold
        /// models + standardizer on the subset -> apply the averaged model to the full
        /// population and compute q-values) but WITHOUT ever materializing a
        /// full-population <see cref="PercolatorEntry"/> list or a
        /// <see cref="PercolatorResult"/> list:
        /// <list type="bullet">
        /// <item>the training-subset selection reads flat <c>labels</c> /
        /// <c>entryIds</c> / <c>peptides</c> / <c>bestScores</c> (= CoelutionSum)
        /// arrays built once from the projection -- <see cref="PercolatorEntry"/>
        /// objects are built ONLY for the &lt;= MaxTrainSize subset;</item>
        /// <item>the score + competition pass runs over the projection rows and writes
        /// the Score + five q-values straight back onto them via
        /// <see cref="PercolatorFdr.ScoreProjectionAndComputeFdrInPlace"/>, reusing the
        /// same flat identity arrays.</item>
        /// </list>
        /// Every parity-locked primitive (<see cref="PercolatorFdr.BuildTrainingSubset"/>,
        /// <see cref="PercolatorFdr.RunPercolator"/> on the subset, and the shared
        /// competition/q-value math) is called UNCHANGED, so the trained model and the
        /// resulting q-values are byte-identical to the <see cref="PercolatorEntry"/>
        /// streaming path on the same input order. Returns <c>true</c> on a
        /// diagnostic-only train abort (same contract as the <c>RunPercolatorFdr</c>
        /// projection overload).
        /// </summary>
        internal static bool RunStreamingIntoProjection(
            List<KeyValuePair<string, List<FdrProjection>>> perFile,
            string[] peptideById,
            PercolatorConfig percConfig,
            Action<string> logInfo,
            string passLabel,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures,
            IFdrOutputSink sink,
            Action<FeatureContributions> captureContributions = null)
        {
            if (loadFileFeatures == null)
                throw new InvalidOperationException(
                    @"RunStreamingIntoProjection requires a per-file feature " +
                    @"loader: the projection carries no resident feature vectors.");

            // Per-file start offsets in nested (file, row) order, so a global index
            // maps back to one projection row (used to build the subset entries below).
            int nFiles = perFile.Count;
            var fileStart = new int[nFiles + 1];
            int n = 0;
            for (int f = 0; f < nFiles; f++)
            {
                fileStart[f] = n;
                n += perFile[f].Value.Count;
            }
            fileStart[nFiles] = n;

            // Distinctive path marker (issue #4374): unambiguous proof this population
            // was ingested through the projection-native streaming path -- neither the
            // removed direct fork nor the resident FdrEntry path emits it. Under the
            // projection flag BOTH the 1st and 2nd pass must show this line, at every
            // scale (the direct-vs-streaming dispatch is gone).
            logInfo(string.Format(
                @"[PATH] {0} projection streaming ingest (RunStreamingIntoProjection): {1} rows",
                passLabel, n));

            int maxTrain = percConfig.MaxTrainSize;

            // Flat identity + best-score arrays from the projection, built ONCE:
            // labels/entryIds/peptides/fileNames drive BOTH the subset selection here
            // and the score/compete pass below. bestScores (= CoelutionSum) ranks
            // best-per-precursor before any Score exists (risk #2), byte-identical to
            // Features[0] on the first pass.
            var labels = new bool[n];
            var entryIds = new uint[n];
            var peptides = new string[n];
            var fileNames = new string[n];
            var bestScores = new double[n];
            int gi = 0;
            for (int f = 0; f < nFiles; f++)
            {
                string fileName = perFile[f].Key;
                var rows = perFile[f].Value;
                for (int r = 0; r < rows.Count; r++)
                {
                    var proj = rows[r];
                    labels[gi] = proj.IsDecoy;
                    entryIds[gi] = proj.EntryId;
                    peptides[gi] = peptideById[proj.PeptideId];
                    fileNames[gi] = fileName;
                    bestScores[gi] = proj.CoelutionSum;
                    gi++;
                }
            }

            // 1. best-per-precursor dedup + 2. peptide-grouped subsample. bestScores is
            //    supplied, so SelectBestPerPrecursor never dereferences the entries arg
            //    -- passing an empty list is exactly what lets this path avoid the
            //    full-N PercolatorEntry buffer the FdrEntry streaming path allocates.
            // Phase marker: the dedup + subsample over all N rows is a multi-minute
            // silent span on an 82-file join; announce it so the console is not blank.
            logInfo(string.Format(@"Selecting training subset from {0} scored entries...", n));
            int[] bestIdx;
            int[] trainSubsetGlobalIdx = PercolatorFdr.BuildTrainingSubset(
                labels, entryIds, peptides, Array.Empty<PercolatorEntry>(), maxTrain,
                percConfig.Seed, out bestIdx, bestScores);

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

            // 3. Build the subset PercolatorEntry list from the projection rows at the
            //    (ascending) subset indices, then load ONLY the subset's feature
            //    vectors one file at a time -- bounded by MaxTrainSize, so the full
            //    population's vectors are never resident. Mirrors RunPercolatorStreaming.
            var subsetEntries = new List<PercolatorEntry>(trainSubsetGlobalIdx.Length);
            int fcur = 0;
            foreach (int si in trainSubsetGlobalIdx)
            {
                while (fcur + 1 < fileStart.Length && si >= fileStart[fcur + 1])
                    fcur++;
                var proj = perFile[fcur].Value[si - fileStart[fcur]];
                subsetEntries.Add(new PercolatorEntry
                {
                    FileName = perFile[fcur].Key,
                    Peptide = peptideById[proj.PeptideId],
                    Charge = proj.Charge,
                    IsDecoy = proj.IsDecoy,
                    EntryId = proj.EntryId,
                    ParquetIndex = proj.ParquetIndex,
                    CoelutionSum = proj.CoelutionSum,
                    Features = null
                });
            }

            var subsetByFile = PercolatorFdr.GroupIndicesByFileName(subsetEntries);
            // Per-file progress: loading the training-subset feature vectors from every file
            // ran ~5 min silent before cross-validation. Console-only, never touches the
            // loaded features, so training is byte-identical.
            using (var loadProgress = new ProgressReporter(
                string.Format(@"Loading training-subset feature vectors from {0} file(s)", subsetByFile.Count),
                subsetByFile.Count))
            {
                int loadDone = 0;
                foreach (var kvp in subsetByFile)
                {
                    loadProgress.Report(++loadDone);
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

            if (trainResults.DiagnosticAbort)
                return true;

            // Release the subset-only working sets before the score pass so only the
            // flat per-observation arrays + the projection remain resident across the
            // peak (bestIdx can be O(N/2) on a 1:1 target/decoy population).
            bestScores = null;
            bestIdx = null;
            trainSubsetGlobalIdx = null;
            subsetEntries = null;
            subsetByFile = null;

            // 4. Score ALL rows + run the competition/q-values, writing the Score
            //    straight onto the projection rows and streaming the q-value outputs
            //    to the sink (no PercolatorResult list). Reuses the flat identity
            //    arrays already built above.
            PercolatorFdr.ScoreProjectionAndComputeFdrInPlace(
                perFile, labels, entryIds, peptides, fileNames, trainResults, percConfig,
                loadFileFeatures, sink, captureContributions);
            return false;
        }

        /// <summary>
        /// Clamp each entry's experiment-level q-value up to its own best (min) run-level
        /// q-value, enforcing that a best-of-runs aggregate can never be more confident than
        /// its best single run. Experiment-level FDR competes each precursor's single best
        /// observation against a de-duplicated (thinner) decoy null, so the raw experiment q
        /// can fall BELOW every per-run q -- letting a precursor pass experiment-level FDR with
        /// no run passing run-level FDR. Downstream that produces reported peptides with no
        /// run-level ID (the blib ID-line artifact Mike observed) and an anti-conservative
        /// experiment-wide FDP calibration.
        ///
        /// The run floor is the entry's best (min) <em>combined</em> run q-value
        /// (<see cref="FdrLevel.Both"/> = max(precursor, peptide)), matching the blib ID line
        /// and <c>OspreyRunScores.RunQValue</c> (both <c>EffectiveRunQvalue(Both)</c>): so
        /// "reported =&gt; some run passes at BOTH the precursor and peptide granularity",
        /// which is the exact invariant a Skyline ID line represents. Both floors key on the
        /// target/decoy-specific identity (never the shared base_id / bare sequence -- a target
        /// must not inherit its paired decoy's good run):
        ///   ExperimentPrecursorQvalue &lt;- max(ExperimentPrecursorQvalue, min-over-runs runBoth)   [by EntryId]
        ///   ExperimentPeptideQvalue   &lt;- max(ExperimentPeptideQvalue,   min-over-runs runBoth)   [by (ModifiedSequence, IsDecoy)]
        /// Run-level q is winner-only per file, so a losing run contributes 1.0 and the min
        /// naturally picks the entry's best genuine run.
        ///
        /// The floor is run-Both at BOTH experiment levels even when <c>--fdr-level</c> is
        /// <see cref="FdrLevel.Precursor"/> (the default): the reported gate and the blib ID
        /// line are always Both, so flooring by run-Both is what makes "reported =&gt; a run
        /// has an ID line" hold. A precursor can therefore be raised out of the reported set
        /// because no run cleared run-<em>peptide</em> FDR, even under precursor-level control
        /// -- intended, matching blib fidelity rather than the run-precursor gate alone.
        ///
        /// NOTE (issue #4378): the IN-PASS clamp (first/second-pass Percolator) now runs in the
        /// memory-bounded FLAT form -- <see cref="PercolatorFdr.ClampExperimentQToBestRunFlat"/>
        /// over the score-pass scalar arrays -- so the full FdrEntry buffer need not be resident
        /// on the streaming path. This resident overload remains for the post-Stage-6 pre-blib
        /// re-clamp (<c>MergeNodeTask</c>), which runs on the already-compacted survivor buffer.
        /// Both produce identical floors (same min/max over the same values).
        /// </summary>
        public static void ClampExperimentQToBestRun(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries)
        {
            var minRunBothByEntryId = new Dictionary<uint, double>();
            var minRunBothByPeptide = new Dictionary<(string ModifiedSequence, bool IsDecoy), double>();
            foreach (var kvp in perFileEntries)
            {
                foreach (var e in kvp.Value)
                {
                    double runBoth = e.EffectiveRunQvalue(FdrLevel.Both);
                    double curPrec;
                    if (!minRunBothByEntryId.TryGetValue(e.EntryId, out curPrec) || runBoth < curPrec)
                        minRunBothByEntryId[e.EntryId] = runBoth;

                    // Treat a null/empty ModifiedSequence as missing (a Parquet stub loaded
                    // without the modified_sequence column can be string.Empty): it has no
                    // peptide identity, so it must not bucket unrelated entries under an empty key.
                    if (string.IsNullOrEmpty(e.ModifiedSequence))
                        continue;
                    // Peptide identity is (ModifiedSequence, IsDecoy): a decoy can share its
                    // paired target's ModifiedSequence, so keying on the sequence alone would
                    // let a decoy's good run lower the target's peptide floor (anti-conservative).
                    var pkey = (e.ModifiedSequence, e.IsDecoy);
                    double curPept;
                    if (!minRunBothByPeptide.TryGetValue(pkey, out curPept) || runBoth < curPept)
                        minRunBothByPeptide[pkey] = runBoth;
                }
            }

            foreach (var kvp in perFileEntries)
            {
                foreach (var e in kvp.Value)
                {
                    double floorPrec;
                    if (minRunBothByEntryId.TryGetValue(e.EntryId, out floorPrec) &&
                        floorPrec > e.ExperimentPrecursorQvalue)
                        e.ExperimentPrecursorQvalue = floorPrec;

                    if (!string.IsNullOrEmpty(e.ModifiedSequence))
                    {
                        double floorPept;
                        if (minRunBothByPeptide.TryGetValue((e.ModifiedSequence, e.IsDecoy), out floorPept) &&
                            floorPept > e.ExperimentPeptideQvalue)
                            e.ExperimentPeptideQvalue = floorPept;
                    }
                }
            }
        }
    }
}
