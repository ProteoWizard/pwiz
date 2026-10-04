/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Threading;
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Application of a trained Percolator model to a population, extracted from
    /// the original <c>PercolatorFdr</c> god class (issue #4468): full-population scoring, the projection-native
    /// in-place path, and the streaming first pass, plus the per-row primitives
    /// they share.
    ///
    /// Where training decides what the model IS, this decides what the model SAYS
    /// about every entry. The two meet at <see cref="ScoreWithFoldModel"/>, which
    /// training also calls to score its held-out folds - hence internal rather
    /// than private.
    ///
    /// These entry points still compute q-values as well as scores, as their names
    /// say (<see cref="ScorePopulationAndComputeFdr"/>). That conflation is
    /// pre-existing; separating it is what the FDR extraction that follows does.
    /// </summary>
    public static class PercolatorScorer
    {
        // [PATH] cost buckets of RunStreamingFirstPass's two score passes. LANE buckets are timed
        // on the file lanes and summed over them; ORDER buckets on the in-order consumer.
        private const int LANE1_WALK = 0;
        private const int LANE1_SIDECAR = 1;
        private const int LANE1_FEATURES = 2;
        private const int LANE1_SCORE = 3;
        private const int LANE1_COMPETITION = 4;
        private const int LANE1_RUN_Q = 5;
        private const int LANE1_FLOORS = 6;
        private const int LANE1_FLUSH = 7;
        private const int PASS1_LANE_BUCKETS = 8;
        private const int ORDER1_COMPETITION = 0;
        private const int PASS1_ORDER_BUCKETS = 1;
        private const int LANE2_WALK = 0;
        private const int LANE2_SIDECAR = 1;
        private const int LANE2_RESCORE = 2;
        private const int LANE2_LOOKUPS = 3;
        private const int LANE2_PREPARE = 4;
        private const int PASS2_LANE_BUCKETS = 5;
        private const int ORDER2_SINK = 0;
        private const int ORDER2_PREPARED = 1;
        private const int PASS2_ORDER_BUCKETS = 2;

        /// <summary>
        /// Streaming-path continuation: given the <paramref name="trainResults"/>
        /// returned by <see cref="PercolatorTrainer.RunPercolator"/> with <c>TrainOnly = true</c>
        /// on a pre-dedup + subsampled training set, apply the averaged fold
        /// model + standardizer to score ALL entries in the population, fit
        /// PEP on the global target-decoy competition winners, and compute
        /// per-run / experiment precursor + peptide q-values on that flat
        /// score array. Mirrors phases 4-5 of Rust's streaming
        /// <c>run_percolator_fdr</c> (pipeline.rs:4460-4800).
        ///
        /// The returned <see cref="PercolatorResults"/> has one
        /// <see cref="PercolatorResult"/> per input entry (sorted in the
        /// same order) plus the training model carried through from
        /// <paramref name="trainResults"/>.
        /// </summary>
        public static PercolatorResults ScorePopulationAndComputeFdr(
            IList<PercolatorEntry> entries,
            PercolatorResults trainResults,
            PercolatorConfig config,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures = null,
            bool applyExperimentAgg = true)
        {
            int n = entries.Count;
            if (n == 0)
            {
                return new PercolatorResults
                {
                    Entries = new List<PercolatorResult>(),
                    FoldWeights = trainResults.FoldWeights,
                    FoldBiases = trainResults.FoldBiases,
                    FoldGbtModels = trainResults.FoldGbtModels,
                    Standardizer = trainResults.Standardizer,
                    IterationsPerFold = trainResults.IterationsPerFold
                };
            }

            // Trees or linear weights -- whichever this run trained. Null gbtModels
            // selects the linear path below, exactly as on the projection score pass.
            var gbtModels = ResolveGbtModels(trainResults);
            int nModels = gbtModels != null ? gbtModels.Count : trainResults.FoldWeights.Count;
            if (nModels == 0)
                throw new InvalidOperationException(
                    @"ScorePopulationAndComputeFdr: trainResults contains no fold models");
            // Feature width comes from the trained model, not from entries[0]:
            // on the streaming path (issue #4355 Phase 4) the stubs carry no
            // resident Features vector to measure. A tree ensemble exposes no weight
            // vector to measure either, so read the width off the standardizer that
            // was fit on the same training matrix.
            int nFeatures = gbtModels != null
                ? trainResults.Standardizer.NumFeatures
                : trainResults.FoldWeights[0].Length;

            // Average fold weights + biases. Matches Rust streaming:
            //   avg_weights[j] = mean_f(fold_weights[f][j])
            //   avg_bias       = mean_f(fold_biases[f])
            // Trees are averaged per-score at scoring time instead (see AverageGbtScore).
            double[] avgWeights = null;
            double avgBias = 0.0;
            if (gbtModels == null)
            {
                avgWeights = new double[nFeatures];
                for (int f = 0; f < nModels; f++)
                {
                    double[] foldW = trainResults.FoldWeights[f];
                    for (int j = 0; j < nFeatures; j++)
                        avgWeights[j] += foldW[j];
                    avgBias += trainResults.FoldBiases[f];
                }
                double nModelsD = nModels;
                for (int j = 0; j < nFeatures; j++)
                    avgWeights[j] /= nModelsD;
                avgBias /= nModelsD;
            }

            // Apply standardizer + averaged SVM model to every entry.
            // Serial (not parallel) so float accumulation order stays
            // deterministic for byte-for-byte cross-impl parity.
            var standardizer = trainResults.Standardizer;
            var finalScores = new double[n];
            var labels = new bool[n];
            var entryIds = new uint[n];
            var peptides = new string[n];
            var fileNames = new string[n];
            var featureBuf = new double[nFeatures];
            // Accumulate per-feature target/decoy sums over the full standardized
            // population for the feature-contribution report below. Reporting only;
            // serial in row/index order (no PLINQ) so the printed numbers are stable
            // and this never perturbs finalScores.
            var contribAcc = new FeatureContributions.Accumulator(nFeatures, config.CollectFeatureHistograms);
            // The row is standardized either way here, so a tree model's report costs only the
            // binning - still taken only when the report asked for it, as on the other paths.
            bool accumulate = gbtModels == null || AccumulatesTreeFeatures(gbtModels, config);
            if (loadFileFeatures == null)
            {
                // Resident-feature path: each stub already carries its vector
                // (the 2nd-pass reload, or any caller that pre-populates
                // Features). Read it in place. Unchanged from the original loop.
                for (int i = 0; i < n; i++)
                {
                    var entry = entries[i];
                    labels[i] = entry.IsDecoy;
                    entryIds[i] = entry.EntryId;
                    peptides[i] = entry.Peptide;
                    fileNames[i] = entry.FileName;

                    Array.Copy(entry.Features, 0, featureBuf, 0, nFeatures);
                    standardizer.TransformSlice(featureBuf);
                    finalScores[i] = ScoreStandardizedRow(gbtModels, avgWeights, avgBias, featureBuf);

                    if (accumulate)
                        contribAcc.Add(featureBuf, entry.IsDecoy);
                }
            }
            else
            {
                // Streaming score pass (issue #4355 Phase 4): the stubs carry no
                // feature vector. Fill the scalar arrays first, then reload
                // features one file at a time -- never holding more than a single
                // file's rows resident. The per-entry math (bias first, then the
                // averaged-weight dot product in feature order) is identical to
                // the resident path above; only the feature SOURCE moves off the
                // O(N) buffer, so finalScores are byte-for-byte the same.
                for (int i = 0; i < n; i++)
                {
                    var entry = entries[i];
                    labels[i] = entry.IsDecoy;
                    entryIds[i] = entry.EntryId;
                    peptides[i] = entry.Peptide;
                    fileNames[i] = entry.FileName;
                }
                var indicesByFile = GroupIndicesByFileName(entries);
                foreach (var kvp in indicesByFile)
                {
                    IReadOnlyList<double[]> rows = loadFileFeatures(kvp.Key);
                    foreach (int i in kvp.Value)
                    {
                        var entry = entries[i];
                        double[] featRow = ResolveFeatureRow(
                            rows, entry.ParquetIndex, entry.CoelutionSum, nFeatures);
                        Array.Copy(featRow, 0, featureBuf, 0, nFeatures);
                        standardizer.TransformSlice(featureBuf);
                        finalScores[i] = ScoreStandardizedRow(gbtModels, avgWeights, avgBias, featureBuf);

                        if (accumulate)
                            contribAcc.Add(featureBuf, entry.IsDecoy);
                    }
                }
            }

            var contributions = BuildContributions(contribAcc, gbtModels, trainResults.FoldWeights, config);

            // Competition + PEP + per-run / experiment q-values over the flat score
            // arrays. Extracted verbatim into StreamingFdr.ComputeStreamingCompetitionQvalues
            // (issue #4355 step (b) increment iii) so the projection-native score
            // pass (ScoreProjectionAndComputeFdrInPlace) drives the byte-identical
            // math from a single source of truth instead of a divergent copy -- the
            // parity-locked ordering (base_id-sorted PEP, per-file q-value grouping)
            // therefore cannot drift between the two buffer shapes.
            double[] peps, runPrecursorQvalues, runPeptideQvalues,
                     expPrecursorQvalues, expPeptideQvalues;
            StreamingFdr.ComputeStreamingCompetitionQvalues(
                finalScores, labels, entryIds, peptides, fileNames,
                out peps, out runPrecursorQvalues, out runPeptideQvalues,
                out expPrecursorQvalues, out expPeptideQvalues, applyExperimentAgg);

            // The score the experiment competitions above ranked each entry on (sidecar v4,
            // issue #4522), from the same effScores selection they used.
            var expAggByEntryId = PercolatorQValues.ComputeExperimentAggregateScoreMap(
                finalScores, labels, entryIds, applyExperimentAgg);

            var results = new List<PercolatorResult>(n);
            for (int i = 0; i < n; i++)
            {
                results.Add(new PercolatorResult
                {
                    Score = finalScores[i],
                    RunPrecursorQvalue = runPrecursorQvalues[i],
                    RunPeptideQvalue = runPeptideQvalues[i],
                    ExperimentPrecursorQvalue = expPrecursorQvalues[i],
                    ExperimentPeptideQvalue = expPeptideQvalues[i],
                    Pep = peps[i],
                    ExperimentAggregateScore = expAggByEntryId.TryGetValue(entryIds[i], out double eav)
                        ? eav : finalScores[i]
                });
            }

            return new PercolatorResults
            {
                Entries = results,
                FoldWeights = trainResults.FoldWeights,
                FoldBiases = trainResults.FoldBiases,
                FoldGbtModels = trainResults.FoldGbtModels,
                Standardizer = standardizer,
                IterationsPerFold = trainResults.IterationsPerFold,
                FeatureContributions = contributions
            };
        }

        /// <summary>
        /// Projection-native counterpart of <see cref="ScorePopulationAndComputeFdr"/>
        /// (issue #4355 step (b) increment iii): apply the trained averaged model to
        /// every projection row by streaming its file's parquet features, then run the
        /// competition + q-value math and write the Score + five q-values STRAIGHT
        /// BACK onto the <see cref="FdrProjection"/> rows -- collapsing the transient
        /// SVM stack that <see cref="ScorePopulationAndComputeFdr"/> holds resident
        /// (the full-population <see cref="PercolatorEntry"/> list AND the
        /// <see cref="PercolatorResult"/> list) into the flat working arrays the
        /// parity-locked math already needs. Only WHERE THE DATA LIVES changes: the
        /// per-entry scoring loop (bias first, then the averaged-weight dot product in
        /// feature order) and the q-value math
        /// (<see cref="StreamingFdr.ComputeStreamingCompetitionQvalues"/>) are byte-for-byte those
        /// of the <see cref="PercolatorEntry"/> path.
        ///
        /// The caller passes the flat <paramref name="labels"/> / <paramref name="entryIds"/>
        /// / <paramref name="peptides"/> arrays it already built (in nested file/row order)
        /// for training-subset selection, so they are not rebuilt here; this method walks
        /// <paramref name="perFile"/> in the SAME nested order (its own key is the file name,
        /// so no flat <c>fileNames</c> array is needed) and zips the results back, keeping every
        /// index aligned. The feature-contribution accumulation runs in per-file order identical
        /// to the <see cref="PercolatorEntry"/>
        /// streaming loop (<c>GroupIndicesByFileName</c> preserves first-seen file
        /// order == <paramref name="perFile"/> order), so the reported contributions
        /// are bit-identical too.
        /// </summary>
        internal static void ScoreProjectionAndComputeFdrInPlace(
            List<KeyValuePair<string, List<FdrProjection>>> perFile,
            bool[] labels, uint[] entryIds, string[] peptides,
            PercolatorResults trainResults, PercolatorConfig config,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures,
            Func<string, double[]> loadFileApexRts,
            IFdrOutputSink sink,
            Action<FeatureContributions> captureContributions = null,
            bool applyExperimentAgg = true)
        {
            if (loadFileFeatures == null)
                throw new InvalidOperationException(
                    @"ScoreProjectionAndComputeFdrInPlace requires a per-file feature loader: " +
                    @"the projection carries no resident feature vectors.");
            // Required for the same reason the feature loader is: the lean FdrProjection carries
            // no retention time, and the sidecar this sink writes has a column for one (format
            // v7). Defaulting it would put a fabricated RT in a persisted artifact that no
            // reader could tell from a measured one, so the absence is a bug here, not a case.
            if (loadFileApexRts == null)
                throw new InvalidOperationException(
                    @"ScoreProjectionAndComputeFdrInPlace requires a per-file apex-RT loader: " +
                    @"the projection carries no retention time and the per-file FDR sidecar " +
                    @"persists one.");

            int n = labels.Length;
            var gbtModels = ResolveGbtModels(trainResults);
            int nModels = gbtModels != null ? gbtModels.Count : trainResults.FoldWeights.Count;
            if (nModels == 0)
                throw new InvalidOperationException(
                    @"ScoreProjectionAndComputeFdrInPlace: trainResults contains no fold models");
            int nFeatures = gbtModels != null
                ? trainResults.Standardizer.NumFeatures
                : trainResults.FoldWeights[0].Length;

            // Average fold weights + biases (identical to ScorePopulationAndComputeFdr);
            // the tree path averages per-score at scoring time instead.
            double[] avgWeights = null;
            double avgBias = 0.0;
            if (gbtModels == null)
            {
                avgWeights = new double[nFeatures];
                for (int f = 0; f < nModels; f++)
                {
                    double[] foldW = trainResults.FoldWeights[f];
                    for (int j = 0; j < nFeatures; j++)
                        avgWeights[j] += foldW[j];
                    avgBias += trainResults.FoldBiases[f];
                }
                double nModelsD = nModels;
                for (int j = 0; j < nFeatures; j++)
                    avgWeights[j] /= nModelsD;
                avgBias /= nModelsD;
            }

            var standardizer = trainResults.Standardizer;
            var finalScores = new double[n];
            var featureBuf = new double[nFeatures];
            // Collect the per-feature target/decoy standardized-value histograms when
            // --model-diagnostics asked for them (config.CollectFeatureHistograms == ModelDiagnostics,
            // set in BuildProjectionPercolatorConfig). The full-population Add loop below feeds the
            // identical standardized featureBuf the resident path bins, so the Model tab's
            // per-feature distributions are byte-identical to the resident build's; off the
            // production path this stays a plain (no-histogram) accumulator.
            var contribAcc = new FeatureContributions.Accumulator(nFeatures, config.CollectFeatureHistograms);
            bool accumulateTreeFeatures = AccumulatesTreeFeatures(gbtModels, config);

            // Streaming score pass over the projection, one file at a time. The
            // per-entry math and the per-file iteration order match the
            // PercolatorEntry streaming loop exactly, so finalScores + the
            // contribution sums are byte-for-byte identical.
            // Per-file progress so this full-population score pass (~15 min silent at
            // 344M rows on the 82-file first-pass join) shows movement; the heartbeat
            // covers a slow single file. Console-only -- never touches finalScores /
            // the sink, so byte-identity is unaffected.
            int gi = 0;
            using (var scoreProgress = new ProgressReporter(string.Format(OspreyFDRResources.PercolatorScorer_ScoreProjectionAndComputeFdrInPlace_Scoring__0__precursor_candidate_peaks, n), n))
            {
                foreach (var kvp in perFile)
                {
                    IReadOnlyList<double[]> rows = loadFileFeatures(kvp.Key);
                    var projRows = kvp.Value;
                    if (gbtModels != null)
                    {
                        // Tree path: parallel over contiguous row chunks. A tree score is a
                        // pure function of its own row written to its own slot, and scoring
                        // accumulates nothing across rows, so this is bit-identical to scoring
                        // the file serially - there is no float accumulation whose order could
                        // drift. Worth doing: a tree row costs ~NFolds x NTrees x depth node
                        // traversals against the linear path's NFeatures multiply-adds, so serial
                        // scoring dominates the run.
                        ScoreRowsGbt(projRows.Count,
                            r => ResolveFeatureRow(rows, projRows[r].ParquetIndex, projRows[r].CoelutionSum, nFeatures),
                            gbtModels, standardizer, nFeatures, finalScores, gi, config.NThreads);
                        // The per-feature report IS a cross-row accumulation, so it stays serial and
                        // in row order, after the parallel pass, and only when it was asked for.
                        if (accumulateTreeFeatures)
                        {
                            foreach (var proj in projRows)
                            {
                                contribAcc.Add(StandardizeFeatureRow(standardizer, featureBuf, rows,
                                    proj.ParquetIndex, proj.CoelutionSum, nFeatures), proj.IsDecoy);
                            }
                        }
                        gi += projRows.Count;
                    }
                    else
                    {
                        // Linear path: UNCHANGED and serial. contribAcc.Add is a running
                        // float sum, so its row order is byte-parity-locked to Rust.
                        for (int r = 0; r < projRows.Count; r++)
                        {
                            var proj = projRows[r];
                            double[] featRow = ResolveFeatureRow(
                                rows, proj.ParquetIndex, proj.CoelutionSum, nFeatures);
                            Array.Copy(featRow, 0, featureBuf, 0, nFeatures);
                            standardizer.TransformSlice(featureBuf);
                            finalScores[gi] = ScoreStandardizedRow(null, avgWeights, avgBias, featureBuf);

                            contribAcc.Add(featureBuf, proj.IsDecoy);
                            gi++;
                        }
                    }
                    scoreProgress.Report(gi);
                }
            }

            var contributions = BuildContributions(contribAcc, gbtModels, trainResults.FoldWeights, config);
            // Surface the trained model's contributions to the caller (the projection-path
            // --model-diagnostics report reads them). No-op (null) on every path that does not
            // request them; a pure hand-off, so scoring stays byte-identical.
            captureContributions?.Invoke(contributions);

            // Bounded q-value reconstruction (issue #4355 Part B): rather than materialize
            // five full-length double[n] q-value arrays (~14 GB at an 82-file join), build only
            // the intrinsically-bounded lookups the write-back reads. PEP is one value per
            // competition winner and experiment q is one value per base_id / per peptide (both
            // O(distinct), built once); the PER-RUN q-values are per-file, so they are computed
            // one file at a time from that file's slice, never a full double[n] array. The
            // competition + q-value math is byte-for-byte the shared code the five-array path
            // used (PercolatorQValues.ComputePepWinnerMap / PercolatorQValues.ComputeExperimentPrecursorQMap /
            // PercolatorQValues.ComputeExperimentPeptideQMap / PercolatorQValues.ComputePerFileRunQvalues all mirror
            // StreamingFdr.ComputeStreamingCompetitionQvalues), so the streamed outputs are identical.
            var pepByEntryId = PercolatorQValues.ComputePepWinnerMap(finalScores, labels, entryIds);

            // The per-file q-value passes below slice each file as one contiguous block
            // [off, off+count). The full-length ComputePerRun* path instead grouped by file
            // name, which is robust to a file appearing in more than one PerFile entry; the
            // slice is not (it would split one file's competition in two -> different run q ->
            // different clamp floors -> different bytes). Every population the pipeline builds
            // has distinct PerFile keys, so this is an invariant, not a live case -- assert it
            // (the single-file test + per-file passes below depend on it) so a future
            // duplicate-key producer (e.g. a 2nd-pass reconciliation re-opening a file) fails
            // fast instead of silently diverging.
            var seenFileKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in perFile)
                if (!seenFileKeys.Add(kvp.Key))
                    throw new InvalidOperationException(string.Format(
                        @"ScoreProjectionAndComputeFdrInPlace: duplicate per-file key '{0}'; the " +
                        @"bounded per-file q-value pass requires one contiguous block per file.",
                        kvp.Key));

            // Experiment q-values: the single-file shortcut is exp == per-run (matching
            // StreamingFdr.ComputeStreamingCompetitionQvalues), built only when multi-file. With distinct
            // keys (asserted above) the file count is just the number of non-empty PerFile
            // entries, so no flat fileNames[n] array is needed -- the caller no longer builds
            // one (issue #4355 Part B: dropping a full O(n) string reference array). This
            // reproduces the retired `new HashSet<string>(fileNames).Count <= 1` exactly (both
            // count the distinct files that contribute rows).
            int nonEmptyFiles = 0;
            foreach (var kvp in perFile)
                if (kvp.Value.Count > 0)
                    nonEmptyFiles++;
            bool isSingleFile = nonEmptyFiles <= 1;
            Dictionary<uint, double> expPrecByWinnerId = isSingleFile
                ? null : PercolatorQValues.ComputeExperimentPrecursorQMap(
                    finalScores, labels, entryIds, applyExperimentAgg);
            Dictionary<string, double> expPeptByPeptide = isSingleFile
                ? null : PercolatorQValues.ComputeExperimentPeptideQMap(
                    finalScores, labels, entryIds, peptides, applyExperimentAgg);

            // The score those experiment competitions ranked each entry on (sidecar v4, issue
            // #4522), persisted beside the q-values they produced. Built even on the
            // single-file shortcut: there the experiment scope IS the run scope, so the
            // aggregate is the entry's max over its own rows -- which is still not the per-row
            // Score, because a precursor carries several pre-compaction rows per file.
            var expAggByEntryId = PercolatorQValues.ComputeExperimentAggregateScoreMap(
                finalScores, labels, entryIds, applyExperimentAgg);

            // Best-of-runs monotonicity floors (issue #4390): the min-over-runs combined run q
            // that ClampExperimentQToBestRunFlat floors experiment q up to, keyed by EntryId and
            // by (peptide, isDecoy). The global minimum must be known before any row is emitted,
            // so a first pass computes each file's per-run q-values (bounded, one file at a time)
            // and reduces them into the floor maps; the emit pass below recomputes them per file
            // to assign. min/max are order-independent -> byte-identical to the flat clamp.
            var minRunBothByEntryId = new Dictionary<uint, double>();
            var minRunBothByPeptide = new Dictionary<(string, bool), double>();
            using (var floorProgress = PercolatorQValues.QProgress(OspreyFDRResources.PercolatorScorer_ScoreProjectionAndComputeFdrInPlace_Per_run_q_value_floors, perFile.Count, n))
            {
                int off = 0;
                int floorFile = 0;
                foreach (var kvp in perFile)
                {
                    int count = kvp.Value.Count;
                    PercolatorQValues.ComputePerFileRunQvalues(
                        finalScores, labels, entryIds, peptides, off, count,
                        out double[] runPrecFile, out double[] runPeptFile);
                    for (int r = 0; r < count; r++)
                    {
                        int g = off + r;
                        double runBoth = Math.Max(runPrecFile[r], runPeptFile[r]);
                        PercolatorQValues.UpdateExperimentQClampFloor(
                            minRunBothByEntryId, minRunBothByPeptide, entryIds[g], peptides[g], labels[g], runBoth);
                    }
                    off += count;
                    floorProgress?.Report(++floorFile);
                }
            }

            // Emit pass: recompute each file's per-run q-values, assign every row's five q-values
            // from the bounded lookups, write the Score onto the projection row (FdrProjection is
            // a readonly struct, so via WithScore), and stream to the sink -- same nested (file,
            // row) order as the scoring loop, so the sink sees the order the five-array write-back
            // produced.
            int wgi = 0;
            int fileIdx = 0;
            foreach (var kvp in perFile)
            {
                var projRows = kvp.Value;
                int count = projRows.Count;
                // Indexed by FdrProjection.ParquetIndex, which IS this file's parquet row
                // ordinal - not by r, because the projection rows were sorted by
                // (EntryId, Charge, ParquetIndex) before scoring. Read through the same
                // ReadFdrStubScalars the ordinal is defined by, so the two cannot drift.
                double[] fileApexRts = count > 0 ? loadFileApexRts(kvp.Key) : null;
                PercolatorQValues.ComputePerFileRunQvalues(
                    finalScores, labels, entryIds, peptides, wgi, count,
                    out double[] runPrecFile, out double[] runPeptFile);
                for (int r = 0; r < count; r++)
                {
                    int g = wgi + r;
                    double rp = runPrecFile[r];
                    double rpe = runPeptFile[r];

                    // Experiment precursor: base_id map (or per-run on the single-file
                    // shortcut), floored up to the entry's min-over-runs combined run q.
                    double ep = isSingleFile
                        ? rp
                        : (expPrecByWinnerId.TryGetValue(entryIds[g], out double epv)
                            ? epv : 1.0);
                    if (minRunBothByEntryId.TryGetValue(entryIds[g], out double floorPrec) &&
                        floorPrec > ep)
                        ep = floorPrec;

                    // Experiment peptide: peptide map (or per-run on the shortcut), floored up
                    // to the (peptide, isDecoy) min-over-runs combined run q. An empty peptide
                    // has no peptide identity and is not floored (matches the flat clamp).
                    string pept = peptides[g];
                    double epe = isSingleFile
                        ? rpe
                        : (expPeptByPeptide.TryGetValue(pept, out double epev) ? epev : 1.0);
                    if (!string.IsNullOrEmpty(pept) &&
                        minRunBothByPeptide.TryGetValue((pept, labels[g]), out double floorPept) &&
                        floorPept > epe)
                        epe = floorPept;

                    double pep = pepByEntryId.TryGetValue(entryIds[g], out double pv) ? pv : 1.0;

                    double ea = expAggByEntryId.TryGetValue(entryIds[g], out double eav)
                        ? eav : finalScores[g];

                    projRows[r] = projRows[r].WithScore(finalScores[g]);
                    uint parquetIndex = projRows[r].ParquetIndex;
                    double apexRt = fileApexRts != null && parquetIndex < fileApexRts.Length
                        ? fileApexRts[parquetIndex]
                        : double.NaN;
                    sink.Accept(fileIdx, r, projRows[r].EntryId, projRows[r].IsDecoy,
                        projRows[r].Charge, pept, finalScores[g], ea, apexRt,
                        new FdrQValues(rp, rpe, ep, epe, pep));
                }
                wgi += count;
                fileIdx++;
            }
        }

        /// <summary>
        /// One best-per-precursor winner captured while streaming identity in flat (file,row)
        /// order (issue #4355 struct-shrink S3, Stage B): enough to reproduce
        /// <see cref="PercolatorSampling.SelectBestPerPrecursor"/>'s output AND build the training-subset
        /// <see cref="PercolatorEntry"/> without a resident projection. <see cref="G"/> is the
        /// row's global (file,row) ordinal -- sorting the captured winners by it ascending
        /// reproduces <c>SelectBestPerPrecursor</c>'s <c>Array.Sort(globalIndex)</c> exactly.
        /// </summary>
        private readonly struct FirstPassDedupRow
        {
            public readonly int G;
            public readonly string FileName;
            public readonly uint EntryId;
            public readonly byte Charge;
            public readonly bool IsDecoy;
            public readonly uint ParquetIndex;
            public readonly double CoelutionSum;
            public readonly string Peptide;
            public FirstPassDedupRow(int g, string fileName, uint entryId, byte charge, bool isDecoy,
                uint parquetIndex, double coelutionSum, string peptide)
            {
                G = g;
                FileName = fileName;
                EntryId = entryId;
                Charge = charge;
                IsDecoy = isDecoy;
                ParquetIndex = parquetIndex;
                CoelutionSum = coelutionSum;
                Peptide = peptide;
            }
        }

        /// <summary>
        /// A file's already-computed scores, in parquet row order, or null when the caller has
        /// nothing on disk for it. The two run q-values pass 1 stored with each score come back
        /// through <paramref name="runPrecursorQvalues"/> and <paramref name="runPeptideQvalues"/>
        /// in the same row order, and are null exactly when the return is.
        ///
        /// <para>Taking the q-values from disk rather than recomputing them is byte-identical,
        /// not a second opinion: whichever path wrote the sidecar produced all three together
        /// from these same scores (see <see cref="CompletedScoreStreamer"/>), and nothing revises
        /// them afterwards. The count and entry_id checks below are what bind them to this file's
        /// rows. The recompute is a sort per file, and the reason it
        /// reads as free is that it is normally weighed against loading the file's feature
        /// vectors and re-running the dot product. On this path neither happens - that is what
        /// having the scores on disk means - so the sort is measured against nothing and becomes
        /// the pass's dominant cost.</para>
        ///
        /// <para>A caller that must WRITE the q-values still has to compute them; the sort is
        /// what produces them. Only a pure reader can take them from here.</para>
        /// </summary>
        private static double[] TryLoadCompletedScores(
            CompletedScoreStreamer tryStream, string fileName, int expectedCount,
            IReadOnlyList<uint> expectedEntryIds, out double[] runPrecursorQvalues,
            out double[] runPeptideQvalues)
        {
            runPrecursorQvalues = null;
            runPeptideQvalues = null;
            if (tryStream == null)
                return null;
            // Sized exactly, because the row count is known before the first record arrives, and
            // allocated only when that record does. A streamer with nothing on disk for the file
            // refuses without calling back - on a cold run that is every file, in both passes - and
            // allocating and zeroing four row-sized arrays (28 bytes a row, ~120 MB for a cohort
            // file) only to drop them would put that cost back on exactly the run with no sidecars
            // to read.
            double[] scores = null;
            uint[] entryIds = null;
            double[] runPrec = null;
            double[] runPept = null;
            int nRead = 0;
            if (!tryStream(fileName, (entryId, score, runPrecQ, runPeptQ) =>
                {
                    if (scores == null)
                    {
                        scores = new double[expectedCount];
                        entryIds = new uint[expectedCount];
                        runPrec = new double[expectedCount];
                        runPept = new double[expectedCount];
                    }
                    // A sidecar holding MORE records than the parquet has rows would run off the
                    // end of these arrays. Keep counting past it and let the length check below
                    // refuse the file, without an exception on the way.
                    if (nRead < expectedCount)
                    {
                        entryIds[nRead] = entryId;
                        scores[nRead] = score;
                        runPrec[nRead] = runPrecQ;
                        runPept[nRead] = runPeptQ;
                    }
                    nRead++;
                }))
            {
                return null;
            }
            // A count mismatch means the sidecar and the parquet disagree about how many rows
            // this file has, which no validity key can catch - so refuse the shortcut and score
            // it rather than emit a silently misaligned file.
            if (nRead != expectedCount)
                return null;
            // A zero-row file the streamer accepted never called back. It is still a file whose
            // (empty) output is on disk, and returning null would have the caller score it and
            // write its attested sidecar a second time.
            if (scores == null)
            {
                scores = Array.Empty<double>();
                entryIds = Array.Empty<uint>();
                runPrec = Array.Empty<double>();
                runPept = Array.Empty<double>();
            }
            // And the ROWS must line up, not just the count. This binds the sidecar's records
            // to parquet rows by POSITION, while every other reader of the file matches by
            // entry_id - so a sidecar that is complete but ordered differently (the resident
            // write path sorts by FdrEntry, not by parquet row) would hand every row its
            // neighbour's score, and the run would finish clean with wrong identifications.
            // The identity is already in hand at both call sites, so checking is free.
            for (int r = 0; r < expectedCount; r++)
            {
                if (entryIds[r] != expectedEntryIds[r])
                    return null;
            }
            // Published only now that count and row identity have both checked out, so a
            // rejected sidecar hands back three nulls together rather than q-values a caller
            // could pair with scores it was told not to use.
            runPrecursorQvalues = runPrec;
            runPeptideQvalues = runPept;
            return scores;
        }

        /// <summary>
        /// 1st-pass-ONLY streaming Percolator that holds NO resident row buffer at all (issue
        /// #4355 struct-shrink S3, Stage B -- the FLAT-memory win): the memory-collapsing fork of
        /// <see cref="PercolatorEngine.RunStreamingIntoProjection"/> +
        /// <see cref="ScoreProjectionAndComputeFdrInPlace"/>. Where those hold the resident
        /// <see cref="FdrProjection"/>[] + the flat <c>labels/entryIds/peptides/finalScores[n]</c>
        /// arrays (O(pre-compaction rows), the 82->500 file blocker), this streams every row's
        /// identity + features straight from parquet THREE times -- once to select the training
        /// subset, once to score + build the bounded q-value maps + clamp floors, once to score +
        /// emit, recomputing the score per row instead of parking an O(n) score array. Only
        /// the SUBSET (&lt;= MaxTrainSize) and the intrinsically-bounded lookups (O(base_ids) /
        /// O(peptides), via <see cref="StreamingFdr.StreamingFirstPassQ"/> + per-file run-q) are ever resident,
        /// so the peak is FLAT in file count.
        ///
        /// Byte-identical to the resident projection path on the same rows in the same (file,row)
        /// order, for either classifier (verified by <c>FdrTest.TestStreamingFirstPassMatchesProjection</c>
        /// for the linear SVM and <c>FdrTest.TestStreamingFirstPassTrainsGbdt</c> for gradient-boosted
        /// trees): the training-subset selection reproduces <see cref="PercolatorSampling.SelectBestPerPrecursor"/> +
        /// <see cref="PercolatorSampling.BuildTrainingSubset"/> (strict-<c>&gt;</c> first-seen dedup ranked on
        /// CoelutionSum, ascending-global-ordinal order, identical
        /// <see cref="PercolatorSampling.SubsampleByPeptideGroup"/>), the training runs the SAME
        /// <see cref="PercolatorTrainer.RunPercolator"/> with the SAME train-only config
        /// (<see cref="PercolatorConfig.CloneForTrainOnly"/>) on the SAME subset, the trained model
        /// scores each row through the same per-classifier code, and the q-value math reuses the
        /// SAME primitives (<see cref="StreamingFdr.StreamingFirstPassQ"/>, <see cref="PercolatorQValues.ComputePerFileRunQvalues"/>,
        /// <see cref="PercolatorQValues.UpdateExperimentQClampFloor"/>). This is 1st-pass-only: the 2nd pass keeps its
        /// O(survivors) resident projection (Stage 7/8 needs it) via the unchanged
        /// <see cref="ScoreProjectionAndComputeFdrInPlace"/>.
        ///
        /// The caller supplies the row source as delegates (kept free of an Osprey.IO dependency):
        /// <paramref name="fileNames"/> in the join's file order; <paramref name="streamFileRows"/>
        /// invokes its callback once per parquet row of a file in row order (== the resident sort's
        /// order on the 1st pass, since the parquet is written (entry_id,charge,scan)-sorted);
        /// <paramref name="loadFileFeatures"/> loads one file's feature vectors, indexed by the
        /// running parquet row ordinal. Returns <c>true</c> on a diagnostic-only train abort.
        /// </summary>
        internal static bool RunStreamingFirstPass(
            IReadOnlyList<string> fileNames,
            Action<string, StubColumns, Action<uint, byte, bool, double, string, double>> streamFileRows,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures,
            PercolatorConfig percConfig,
            IOspreyLog log,
            string passLabel,
            IFdrOutputSink sink,
            Action<FeatureContributions> captureContributions = null,
            Action<PercolatorResults> captureModel = null,
            CompletedScoreStreamer tryStreamCompletedScores = null,
            PercolatorResults pretrainedModel = null,
            FileRunScopeSink flushFileRunScope = null,
            int fileLanes = 1)
        {
            if (streamFileRows == null)
                throw new ArgumentNullException(nameof(streamFileRows));
            if (loadFileFeatures == null)
                throw new ArgumentNullException(nameof(loadFileFeatures));
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));
            // A persisted model of the OTHER classifier is refused - neither adopted, which would
            // score this run with the wrong classifier, nor retrained around, which would leave
            // any file already scored off the persisted model on a different discriminant from
            // the rest. The validity key records the FDR method, so a current model can only
            // mismatch through a defect; this is the backstop. Checked before the Pass 0 ingest,
            // which streams every file of the join and cannot change the answer.
            if (pretrainedModel != null &&
                pretrainedModel.IsGradientBoostedTrees != percConfig.UseGradientBoostedTrees)
            {
                throw new InvalidOperationException(ClassifierMismatchMessage(
                    pretrainedModel.IsGradientBoostedTrees, percConfig.UseGradientBoostedTrees));
            }

            int nFiles = fileNames.Count;
            int nFeatures = percConfig.FeatureInfos.Length;
            int maxTrain = percConfig.MaxTrainSize;
            // Each pass below walks the files through OrderedFileLanes: the file-local work - the
            // parquet decode, the feature load, the dot products, the per-file sort - runs on up to
            // fileLanes threads, and everything that crosses files (the training dedup, the
            // competition, the contribution sums, the sink) is applied on this thread in file
            // order, as the plain loop applied it. A file's raw rows live in that file's own
            // RowBuffer, so a bounded number of files (twice the lane count) is resident at once
            // rather than one; with one lane it is the plain loop exactly.

            // ---- Pass 0: stream identity, build the training subset, train the model ----
            // Best-per-precursor dedup captured WITH identity, in flat (file,row) order. Strict
            // '>' on CoelutionSum with first-seen (lowest g) winning ties -- exactly
            // SelectBestPerPrecursor iterating ascending global index. bestScores == CoelutionSum
            // (byte-identical to Features[0] on the 1st pass), so no feature load is needed here.
            var bestTarget = new Dictionary<uint, FirstPassDedupRow>();
            var bestDecoy = new Dictionary<uint, FirstPassDedupRow>();
            // Run bookkeeping for the reservoir, allocated only when it is on so the default-off
            // arm keeps exactly the dictionaries, and the memory, it has always had.
            var runPick = OspreyEnvironment.TrainPickRun ? new Dictionary<uint, PercolatorSampling.RunPickState>() : null;
            // Which observation represents a precursor. Logged when it is NOT the default,
            // because nothing else in the output would say which population trained the model.
            // Plain prose in the default log, not a gated tag: the user set the variable, and it
            // changes the results.
            bool pickRun = OspreyEnvironment.TrainPickRun;
            if (!pickRun)
            {
                log.LogInfo(@"OSPREY_TRAIN_PICK_RUN=0 is set: each precursor is trained on its best " +
                            @"observation across runs, not a uniform sample of them (the behavior before 26.1).");
            }
            int g = 0;
            int nInputTargets = 0, nInputDecoys = 0;
            // This pass streams every file's parquet rows before the [PATH] line below, so it is a
            // determinate O(files) I/O step (43s at 82 files, minutes at 500). Report per-file
            // progress through the standard throttled reporter so a large join never goes silent.
            var ingestProgress = new ProgressReporter(
                CountText.Format(nFiles, OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Reading_precursor_candidate_peaks_for_Percolator_from_1_file,
                    OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Reading_precursor_candidate_peaks_for_Percolator_from__0__files), nFiles,
                intervalSeconds: ProgressReporter.IO_INTERVAL_SECONDS);
            // The decode is this pass's cost and is file-local, so the lanes read files ahead. The
            // dedup is order-dependent - a tie goes to the first-seen ordinal, and the reservoir
            // draws per run in arrival order - so it stays on this thread, in file order.
            // Each file's row count, recorded here so the later passes can size a file's buffers
            // exactly instead of growing them.
            var rowCounts = new int[nFiles];
            void DedupFile(int f, RowBuffer buffer)
            {
                string file = fileNames[f];
                int count = buffer.Count;
                rowCounts[f] = count;
                for (int r = 0; r < count; r++)
                {
                    uint entryId = buffer.EntryIds[r];
                    bool isDecoy = buffer.IsDecoys[r];
                    double coelutionSum = buffer.CoelutionSums[r];
                    uint baseId = entryId & PercolatorEntry.BASE_ID_MASK;
                    var map = isDecoy ? bestDecoy : bestTarget;
                    if (map.TryGetValue(baseId, out FirstPassDedupRow existing))
                    {
                        bool replace;
                        if (pickRun)
                        {
                            // Reservoir of size one over RUNS - not over rows. Pass 1 is
                            // PRE-COMPACTION, so a precursor carries several candidate-peak rows
                            // within one file; drawing per row would both weight a run by how many
                            // candidates it produced AND leave a RANDOM candidate as the training
                            // row instead of that run's best peak.
                            uint seenKey = baseId | (isDecoy ? 0x80000000u : 0u);
                            PercolatorSampling.RunPickState state = runPick[seenKey];
                            if (f == state.LastRun)
                            {
                                // Another candidate peak from the run already drawn for: resolve it
                                // by score, so the surviving row is this run's BEST peak.
                                replace = state.HolderIsCurrentRun && coelutionSum > existing.CoelutionSum;
                            }
                            else
                            {
                                uint runs = state.RunsSeen + 1u;
                                // The DRAW takes the decoy bit too. BASE_ID_MASK clears the high
                                // bit, so drawing on the masked base_id would make a target and its
                                // paired decoy decide identically at every k and land on the same
                                // run for essentially every precursor.
                                replace = ReservoirTakesSlot(seenKey, runs, percConfig.Seed);
                                state.RunsSeen = runs;
                                state.LastRun = f;
                                state.HolderIsCurrentRun = replace;
                                runPick[seenKey] = state;
                            }
                        }
                        else
                        {
                            replace = coelutionSum > existing.CoelutionSum;
                        }
                        if (replace)
                        {
                            map[baseId] = new FirstPassDedupRow(
                                g, file, entryId, buffer.Charges[r], isDecoy, (uint)r, coelutionSum,
                                buffer.Peptides[r]);
                        }
                    }
                    else
                    {
                        map[baseId] = new FirstPassDedupRow(
                            g, file, entryId, buffer.Charges[r], isDecoy, (uint)r, coelutionSum, buffer.Peptides[r]);
                        if (pickRun)
                        {
                            uint seenKey = baseId | (isDecoy ? 0x80000000u : 0u);
                            runPick[seenKey] = new PercolatorSampling.RunPickState
                            {
                                RunsSeen = 1u, LastRun = f, HolderIsCurrentRun = true
                            };
                        }
                    }
                    if (isDecoy) nInputDecoys++; else nInputTargets++;
                    g++;
                }
                ingestProgress.Report(f + 1);
            }
            OrderedFileLanes.Run(nFiles, fileLanes,
                // Core only: this pass reduces to a best-per-precursor training subset and never
                // looks at RowBuffer.ApexRts, so decoding apex_rt here is pure waste.
                f => ReadFileRows(streamFileRows, fileNames[f], StubColumns.Core),
                DedupFile);
            ingestProgress.Dispose();
            int n = g;
            log.LogInfo(LogTag.PATH, @"{0} streaming ingest (RunStreamingFirstPass): {1} rows", passLabel, n);
            LogBlockReads(log, passLabel, @"pass 0");
            log.LogInfo(LogTag.COUNT, @"{0} Percolator input: {1} peaks ({2} targets, {3} decoys, {4} features)",
                passLabel, n, nInputTargets, nInputDecoys, nFeatures);

            // Dedup rows in ascending global ordinal == SelectBestPerPrecursor's Array.Sort of the
            // winning global indices (each base_id's best is one unique row, so g never ties).
            var dedup = new List<FirstPassDedupRow>(bestTarget.Count + bestDecoy.Count);
            dedup.AddRange(bestTarget.Values);
            dedup.AddRange(bestDecoy.Values);
            dedup.Sort((a, b) => a.G.CompareTo(b.G)); // Array.Sort OK: G is the unique global row ordinal of each base_id's best row, so the comparator never ties -- reproduces SelectBestPerPrecursor's Array.Sort(result).
            int m = dedup.Count;
            int dedupTargets = 0;
            foreach (var d in dedup)
                if (!d.IsDecoy) dedupTargets++;
            log.LogInfo(LogTag.COUNT, @"{0} Percolator streaming best-per-precursor: {1} precursors ({2} targets, {3} decoys) from {4} peaks",
                passLabel, m, dedupTargets, m - dedupTargets, n);

            // Peptide-grouped subsample when the dedup count exceeds MaxTrainSize (mirrors
            // BuildTrainingSubset: SelectBestPerPrecursor already ran above via the streaming dedup,
            // so only the SubsampleByPeptideGroup step remains). localSelected indexes `dedup`.
            int[] localSelected;
            if (maxTrain <= 0 || m <= maxTrain)
            {
                localSelected = new int[m];
                for (int i = 0; i < m; i++)
                    localSelected[i] = i;
            }
            else
            {
                var dedupLabels = new bool[m];
                var dedupEntryIds = new uint[m];
                var dedupPeptides = new string[m];
                for (int i = 0; i < m; i++)
                {
                    dedupLabels[i] = dedup[i].IsDecoy;
                    dedupEntryIds[i] = dedup[i].EntryId;
                    dedupPeptides[i] = dedup[i].Peptide;
                }
                localSelected = PercolatorSampling.SubsampleByPeptideGroup(
                    dedupLabels, dedupEntryIds, dedupPeptides, maxTrain, percConfig.Seed);
            }

            int subTargets = 0;
            var subsetEntries = new List<PercolatorEntry>(localSelected.Length);
            foreach (int li in localSelected)
            {
                var d = dedup[li];
                if (!d.IsDecoy) subTargets++;
                subsetEntries.Add(new PercolatorEntry
                {
                    FileName = d.FileName,
                    Peptide = d.Peptide,
                    Charge = d.Charge,
                    IsDecoy = d.IsDecoy,
                    EntryId = d.EntryId,
                    ParquetIndex = d.ParquetIndex,
                    CoelutionSum = d.CoelutionSum,
                    Features = null
                });
            }
            log.LogInfo(LogTag.COUNT, @"{0} Percolator streaming subsample: {1} precursors ({2} targets, {3} decoys)",
                passLabel, subsetEntries.Count, subTargets, subsetEntries.Count - subTargets);

            // A persisted model is only usable if it was trained on THIS run's feature set, and
            // nothing upstream can establish that: the validity key the caller checks carries no
            // feature-set or build term, so a model from a build with a different feature list
            // matches it exactly. Adopting one would either index past the end of its weight
            // vector mid-score-pass or, when it is wider, silently truncate it and apply the
            // standardizer at the wrong width - scores that look plausible and are wrong.
            //
            // Checked HERE, above the subset load, because rejecting it after that branch would
            // leave the subset unloaded and then train on entries with no features. (The
            // classifier, which needs no ingest to judge, is checked on entry.)
            if (pretrainedModel != null)
            {
                var pretrainedTrees = ResolveGbtModels(pretrainedModel);
                int modelFeatures = pretrainedTrees != null
                    ? pretrainedTrees[0].FeatureCount
                    : pretrainedModel.FoldWeights != null && pretrainedModel.FoldWeights.Count > 0
                        ? pretrainedModel.FoldWeights[0].Length
                        : -1;
                if (modelFeatures != nFeatures ||
                    pretrainedModel.Standardizer == null ||
                    pretrainedModel.Standardizer.NumFeatures != nFeatures)
                {
                    log.LogInfo(string.Format(
                        OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Ignoring_the_saved_first_pass_model__it_has__0__features_and_this_run_scores__1___, modelFeatures, nFeatures));
                    pretrainedModel = null;
                }
            }

            // Load ONLY the subset's feature vectors, one file at a time (bounded by MaxTrainSize),
            // cloning each row so the subset entry owns it -- mirrors RunStreamingIntoProjection.
            var subsetByFile = GroupIndicesByFileName(subsetEntries);
            // Both skipped when the caller supplied the model this pass would have trained.
            // Training exists to score entries; when every entry's score is already on disk
            // there is nothing for a model to do, and loading the training subset's feature
            // vectors is the single most expensive thing left - 21 minutes at 446 files, spent
            // to reproduce a model that was already persisted per file as .1st-pass.model.json.
            if (pretrainedModel == null)
            {
                // Each file fills only its own subset entries, so the files need no ordering.
                var subsetFiles = new List<KeyValuePair<string, List<int>>>(subsetByFile);
                int subsetFilesLoaded = 0;
                using (var loadProgress = new ProgressReporter(CountText.Format(subsetFiles.Count,
                           OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Loading_Percolator_training_features_from_1_file,
                           OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Loading_Percolator_training_features_from__0__files), subsetFiles.Count))
                {
                    OrderedFileLanes.For(subsetFiles.Count, fileLanes, i =>
                    {
                        IReadOnlyList<double[]> rows = loadFileFeatures(subsetFiles[i].Key);
                        foreach (int k in subsetFiles[i].Value)
                        {
                            var entry = subsetEntries[k];
                            entry.Features = (double[])ResolveFeatureRow(
                                rows, entry.ParquetIndex, entry.CoelutionSum, nFeatures).Clone();
                        }
                        loadProgress.Report(Interlocked.Increment(ref subsetFilesLoaded));
                    });
                }
            }
            else
            {
                // No training subset is loaded and no model is trained.
                log.LogInfo(OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Reusing_the_saved_first_pass_model_);
            }

            // The same train-only copy the two resident streaming paths hand the trainer. This
            // path used to build its own field list, which left out the classifier choice: under
            // gbdt it trained the linear SVM, at the tree iteration cap, and the
            // default gbdt run never trained a tree.
            var trainConfig = percConfig.CloneForTrainOnly();
            PercolatorResults trainResults =
                pretrainedModel ?? PercolatorTrainer.RunPercolator(subsetEntries, trainConfig);
            if (trainResults.DiagnosticAbort)
                return true;

            // Publish the frozen first-pass model (fold weights + biases + standardizer) for the
            // OSPREY_PASS2_QVALUE=transfer pass-2 step. No-op (null) on the default path, so this
            // streaming first pass stays byte-identical. See TODO-osprey_pass2_per_run_only_qvalue.
            captureModel?.Invoke(trainResults);
            LogBlockReads(log, passLabel, @"training load");

            // Release the pass-0 working sets before the score passes so only the bounded lookups
            // remain resident across the peak.
            bestTarget = null;
            bestDecoy = null;
            dedup = null;
            localSelected = null;
            subsetEntries = null;
            subsetByFile = null;

            // Trees or linear weights - whichever this run trained, exactly as on the projection
            // score pass. Null gbtModels selects the linear path, which is unchanged.
            var gbtModels = ResolveGbtModels(trainResults);
            int nModels = gbtModels != null ? gbtModels.Count : trainResults.FoldWeights.Count;
            if (nModels == 0)
                throw new InvalidOperationException(
                    @"RunStreamingFirstPass: trainResults contains no fold models");
            // Average fold weights + biases (identical to ScoreProjectionAndComputeFdrInPlace).
            // Trees are averaged per score instead (see AverageGbtScore).
            double[] avgWeights = null;
            double avgBias = 0.0;
            if (gbtModels == null)
            {
                avgWeights = new double[nFeatures];
                for (int fm = 0; fm < nModels; fm++)
                {
                    double[] foldW = trainResults.FoldWeights[fm];
                    for (int j = 0; j < nFeatures; j++)
                        avgWeights[j] += foldW[j];
                    avgBias += trainResults.FoldBiases[fm];
                }
                double nModelsD = nModels;
                for (int j = 0; j < nFeatures; j++)
                    avgWeights[j] /= nModelsD;
                avgBias /= nModelsD;
            }
            var standardizer = trainResults.Standardizer;

            // ---- Pass 1: score + build the 3 bounded q maps + reduce the clamp floors ----
            // Reuses the verified StreamingFdr.StreamingFirstPassQ kernel; per-file run-q from a bounded one-file
            // buffer, reduced into the best-of-runs clamp floors (issue #4390). The score is
            // recomputed per row (bias first, then the averaged-weight dot product in feature order;
            // a tree ensemble scores the file up front instead), byte-for-byte the resident score
            // loop, only without the O(n) finalScores array.
            // Gate the aggregation on the pass label, exactly as the resident and projection score
            // passes do. This method has one caller and it passes FIRST_PASS_LABEL, so today the
            // gate is a no-op - but an ungated read of MeanBestN here is the identical shape of the
            // defect that let the 2nd pass re-aggregate on the other two paths, and a future
            // second-pass caller would reintroduce it silently.
            var streamingQ = new StreamingFdr.StreamingFirstPassQ(
                passLabel == PercolatorEngine.FIRST_PASS_LABEL ? OspreyEnvironment.MeanBestN : 0);
            var clampFloors = new PercolatorQValues.ExperimentQClampFloors();
            // Each file's first global row ordinal - the g its first row takes in the plain loop -
            // from pass 0's counts, so a lane can stamp a file's ordinals before the files ahead of
            // it have been consumed.
            var firstOrdinal = new int[nFiles];
            for (int f = 1; f < nFiles; f++)
                firstOrdinal[f] = firstOrdinal[f - 1] + rowCounts[f - 1];
            var contribAcc = new FeatureContributions.Accumulator(nFeatures, percConfig.CollectFeatureHistograms);
            bool accumulateTreeFeatures = AccumulatesTreeFeatures(gbtModels, percConfig);
            int nonEmptyFiles = 0;
            int g1 = 0;
            // Pass-1 cost attribution on the [PATH] channel. The lane buckets are SUMMED over the
            // lanes - thread time, not wall time - so they say where the work is, and they add up to
            // more than the pass when lanes overlap. The in-order buckets and the wall clock are
            // single-threaded and say what the pass actually waits on. The run-q bucket is the one
            // pass 2 no longer pays: this pass calls the SAME per-file sort on the SAME rows, so it
            // measures directly what reading the q-values off the sidecar saves. Every timer reads
            // the clock once per FILE, never per row.
            var lane1 = new LaneCost(PASS1_LANE_BUCKETS);
            var order1 = new LaneCost(PASS1_ORDER_BUCKETS);
            var wall1 = Stopwatch.StartNew();
            // No "Running Percolator on N" heading here: it would print AFTER the training lines
            // above, reading as a second Percolator pass. The score heading below marks the step.
            // Fill the previously-silent multi-minute streaming score pass with throttled percent,
            // mirroring the resident ScoreProjectionAndComputeFdrInPlace "Scoring N entries" line.
            // Progress is log-only (OspreyOutput.Out), so the FDR output stays byte-identical.
            using (var scoreProgress = new ProgressReporter(string.Format(OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Scoring__0__precursor_candidate_peaks, n), n))
            {
                // On a lane: everything about one file that no other file can see.
                Pass1File ScoreFile(int f)
                {
                    string fileName = fileNames[f];
                    long t = Stopwatch.GetTimestamp();
                    // The ONE walk that needs apex RT: this pass hands the file's finished run-scope
                    // output to flushFileRunScope, which writes the v7 sidecar.
                    RowBuffer rows = ReadFileRows(streamFileRows, fileName, StubColumns.ApexRt, rowCounts[f]);
                    t = lane1.Stop(LANE1_WALK, t);
                    int count = rows.Count;
                    // Discards the stored q-values deliberately. This pass is the WRITER, and for
                    // any file it actually scores the sort is the step that produces them. A resumed
                    // file could read them back, but here they feed only the clamp-floor reduction,
                    // so the saving would be confined to a resume while the blast radius would be
                    // global state rather than one row's output.
                    var file = new Pass1File
                    {
                        Rows = rows,
                        Scores = TryLoadCompletedScores(
                            tryStreamCompletedScores, fileName, count, rows.EntryIds, out _, out _)
                    };
                    t = lane1.Stop(LANE1_SIDECAR, t);
                    bool scoredHere = file.Scores == null;
                    if (scoredHere)
                    {
                        IReadOnlyList<double[]> featureRows = loadFileFeatures(fileName);
                        t = lane1.Stop(LANE1_FEATURES, t);
                        ScoreRows(file, featureRows, gbtModels, avgWeights, avgBias, standardizer, nFeatures,
                            percConfig.CollectFeatureHistograms, accumulateTreeFeatures, percConfig.NThreads);
                        t = lane1.Stop(LANE1_SCORE, t);
                    }
                    // The competition's share of this file, reduced here when it is a first-seen
                    // maximum (the default max mode) and the file holds the rows pass 0 counted, so
                    // its ordinals are right. Otherwise the consumer adds the rows one at a time.
                    if (streamingQ.ReducesByFile && count == rowCounts[f])
                    {
                        var competition = streamingQ.BeginFile();
                        int g0 = firstOrdinal[f];
                        for (int r = 0; r < count; r++)
                            competition.Add(g0 + r, file.Scores[r], rows.EntryIds[r], rows.IsDecoys[r], rows.Peptides[r]);
                        file.Competition = competition;
                    }
                    t = lane1.Stop(LANE1_COMPETITION, t);
                    var labels = rows.IsDecoys.ToArray();
                    var entryIds = rows.EntryIds.ToArray();
                    var peptides = rows.Peptides.ToArray();
                    PercolatorQValues.ComputePerFileRunQvalues(
                        file.Scores, labels, entryIds, peptides, 0, count,
                        out double[] runPrecFile, out double[] runPeptFile);
                    t = lane1.Stop(LANE1_RUN_Q, t);
                    // The clamp floors are minimums: this file's rows reduce to one value per key
                    // here, and fold into the shared floors from this lane, in whatever order the
                    // files finish - see ExperimentQClampFloors for why no order is needed.
                    var fileFloorByEntryId = new Dictionary<uint, double>();
                    var fileFloorByPeptide = new Dictionary<(string, bool), double>();
                    for (int r = 0; r < count; r++)
                    {
                        double runBoth = Math.Max(runPrecFile[r], runPeptFile[r]);
                        PercolatorQValues.UpdateExperimentQClampFloor(fileFloorByEntryId,
                            fileFloorByPeptide, entryIds[r], peptides[r], labels[r], runBoth);
                    }
                    clampFloors.Merge(fileFloorByEntryId, fileFloorByPeptide);
                    t = lane1.Stop(LANE1_FLOORS, t);

                    // This file's run-scope output is COMPLETE here - score, run precursor q and run
                    // peptide q are all final, and none of them depends on another file. Hand it to
                    // the caller so it lands on disk now rather than one phase later, which is what
                    // makes an interrupted run lose one file instead of every file (see
                    // FileRunScopeSink). Skipped when the scores came off an existing sidecar: that
                    // file is already written, and rewriting an artifact a validity marker attests
                    // would replace it with a copy the marker no longer describes.
                    if (scoredHere)
                    {
                        flushFileRunScope?.Invoke(fileName, f, count, entryIds, file.Scores,
                            runPrecFile, runPeptFile, rows.ApexRts.ToArray());
                    }
                    lane1.Stop(LANE1_FLUSH, t);
                    return file;
                }

                // On this thread, in file order: merging the competition (a tie goes to the first-seen
                // ordinal) and the contribution sums (floating-point, so row order IS the sum).
                void ApplyScoredFile(int f, Pass1File file)
                {
                    long t = Stopwatch.GetTimestamp();
                    RowBuffer rows = file.Rows;
                    int count = rows.Count;
                    if (count > 0)
                        nonEmptyFiles++;
                    // The lane's reduction is used only when its ordinals are the ones this walk has
                    // reached. A file earlier in the cohort whose row count disagreed with pass 0 would
                    // shift every later file's, and then those files are added row by row instead.
                    bool merge = file.Competition != null && g1 == firstOrdinal[f];
                    if (merge)
                        streamingQ.MergeFile(file.Competition);
                    for (int r = 0; r < count; r++)
                    {
                        bool isDecoy = rows.IsDecoys[r];
                        if (!merge)
                            streamingQ.Add(g1, file.Scores[r], rows.EntryIds[r], isDecoy, rows.Peptides[r]);
                        // A resumed file has no standardized vectors: nothing was scored, so there
                        // is nothing honest to contribute, and the report covers the files actually
                        // scored.
                        if (file.Standardized != null)
                            contribAcc.AddSums(file.Standardized, r * nFeatures, isDecoy);
                        g1++;
                    }
                    scoreProgress.Report(g1);
                    contribAcc.MergeHistograms(file.Histograms);
                    order1.Stop(ORDER1_COMPETITION, t);
                }

                OrderedFileLanes.Run(nFiles, fileLanes, ScoreFile, ApplyScoredFile);
            }
            wall1.Stop();
            // "run-q" here is the per-file sort pass 2 stopped doing. Read it against pass 2's
            // own "run-q recompute" line below: this one is what that one used to cost.
            log.LogInfo(LogTag.PATH,
                @"{0} pass 1 cost over {1} rows, {2} lane(s): wall {3:F1}s; lanes (summed) parquet walk {4:F1}s, sidecar load {5:F1}s, feature load {6:F1}s, score {7:F1}s, competition {8:F1}s, run-q sort {9:F1}s, clamp floors {10:F1}s, sidecar write {11:F1}s; in order competition merge + contribution sums {12:F1}s",
                passLabel, n, LanesUsed(fileLanes, nFiles), wall1.Elapsed.TotalSeconds,
                lane1.Seconds(LANE1_WALK), lane1.Seconds(LANE1_SIDECAR), lane1.Seconds(LANE1_FEATURES),
                lane1.Seconds(LANE1_SCORE), lane1.Seconds(LANE1_COMPETITION), lane1.Seconds(LANE1_RUN_Q), lane1.Seconds(LANE1_FLOORS),
                lane1.Seconds(LANE1_FLUSH), order1.Seconds(ORDER1_COMPETITION));
            LogBlockReads(log, passLabel, @"pass 1");

            var contributions = BuildContributions(contribAcc, gbtModels, trainResults.FoldWeights, percConfig);
            captureContributions?.Invoke(contributions);

            // Finalize the bounded lookups. PEP is global (built always); the experiment maps use
            // the single-file shortcut (exp == per-run) so they are built only when multi-file --
            // matching ScoreProjectionAndComputeFdrInPlace exactly.
            var pepByEntryId = streamingQ.BuildPepWinnerMap();
            bool isSingleFile = nonEmptyFiles <= 1;
            Dictionary<uint, double> expPrecByWinnerId = isSingleFile
                ? null : streamingQ.BuildExperimentPrecursorQMap();
            Dictionary<string, double> expPeptByPeptide = isSingleFile
                ? null : streamingQ.BuildExperimentPeptideQMap();

            // The score those competitions ranked each entry on (sidecar v4, issue #4522).
            // Built unconditionally -- see ScoreProjectionAndComputeFdrInPlace for why the
            // single-file shortcut does NOT apply to the aggregate.
            var expAggByEntryId = streamingQ.BuildExperimentAggregateScoreMap();

            // ---- Pass 2: re-score + assign the 5 q-values + stream to the sink ----
            // Progress-reported (log-only) like Pass 1 so the second streaming pass over all rows
            // is not silent; byte-identical q-values and sink output.
            // Every map the q-values are looked up in is final by now and only read, so a lane
            // computes a whole file's five q-values; only the sink needs file order. Same [PATH]
            // treatment as pass 1: lane buckets summed over lanes, per-file clock reads only.
            var lane2 = new LaneCost(PASS2_LANE_BUCKETS);
            // A sink that can do its order-free per-row work a file at a time does it on the lane
            // that produced the file, leaving only the order-dependent rest in sequence.
            var laneSink = sink as IFdrFileLaneSink;
            var order2 = new LaneCost(PASS2_ORDER_BUCKETS);
            var wall2 = Stopwatch.StartNew();
            int gEmit = 0;
            using (var emitProgress = new ProgressReporter(string.Format(OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Assigning_q_values_to__0__precursor_candidate_peaks, n), n))
            {
                Pass2File AssignFile(int f)
                {
                    string fileName = fileNames[f];
                    long t = Stopwatch.GetTimestamp();
                    // Still asks, though on this path nothing consumes it: the value goes to
                    // sink.Accept, and FdrStoringSink writes a record from it whenever it owns the
                    // write. It never does here - pass 1 marks every file it writes and the resume
                    // gate marks the rest - but that is an invariant maintained in another file, and
                    // the cost of being wrong is a fabricated retention time in a persisted artifact
                    // that no reader could distinguish from a measured one.
                    // Without coelution_sum: only the rescore below uses it, and only for a file
                    // with no sidecar, which re-reads its rows with the column. It is the one
                    // walk column among the features, so leaving it out saves a disk read per
                    // row group.
                    RowBuffer rows = ReadFileRows(streamFileRows, fileName,
                        StubColumns.ApexRt | StubColumns.SkipCoelutionSum, rowCounts[f]);
                    t = lane2.Stop(LANE2_WALK, t);
                    int count = rows.Count;
                    // Pass 1's run q-values, read back off the sidecar with the scores. Recomputing
                    // them would sort this file again to arrive at the same numbers: they are a
                    // function of these same scores, and pass 1 computed and wrote all three
                    // together.
                    double[] scores = TryLoadCompletedScores(
                        tryStreamCompletedScores, fileName, count, rows.EntryIds,
                        out double[] runPrecFile, out double[] runPeptFile);
                    t = lane2.Stop(LANE2_SIDECAR, t);
                    // Only a file with no sidecar is scored and sorted here, which is also the only
                    // file whose scores are computed in this pass. Keyed on the scores, which
                    // TryLoadCompletedScores returns null exactly when it returns no q-values.
                    if (scores == null)
                    {
                        rows = ReadFileRows(streamFileRows, fileName, StubColumns.ApexRt, rowCounts[f]);
                        IReadOnlyList<double[]> featureRows = loadFileFeatures(fileName);
                        scores = new double[count];
                        if (ScoresBeforeRowLoop(null, featureRows, rows.CoelutionSums, gbtModels,
                                standardizer, nFeatures, percConfig.NThreads, scores) == null)
                        {
                            var featureBuf = new double[nFeatures];
                            for (int r = 0; r < count; r++)
                            {
                                scores[r] = ComputeStreamedScore(avgWeights, avgBias, standardizer, featureBuf,
                                    featureRows, r, rows.CoelutionSums[r], nFeatures);
                            }
                        }
                        PercolatorQValues.ComputePerFileRunQvalues(
                            scores, rows.IsDecoys.ToArray(), rows.EntryIds.ToArray(), rows.Peptides.ToArray(),
                            0, count, out runPrecFile, out runPeptFile);
                        t = lane2.Stop(LANE2_RESCORE, t);
                    }
                    var file = new Pass2File(rows, scores, runPrecFile, runPeptFile);
                    for (int r = 0; r < count; r++)
                    {
                        uint entryId = rows.EntryIds[r];
                        double ep = isSingleFile
                            ? runPrecFile[r]
                            : (expPrecByWinnerId.TryGetValue(entryId, out double epv) ? epv : 1.0);
                        if (clampFloors.TryGetByEntryId(entryId, out double floorPrec) && floorPrec > ep)
                            ep = floorPrec;

                        string pept = rows.Peptides[r];
                        double epe = isSingleFile
                            ? runPeptFile[r]
                            : (expPeptByPeptide.TryGetValue(pept, out double epev) ? epev : 1.0);
                        if (!string.IsNullOrEmpty(pept) &&
                            clampFloors.TryGetByPeptide((pept, rows.IsDecoys[r]), out double floorPept) && floorPept > epe)
                            epe = floorPept;

                        file.ExperimentPrecursorQ[r] = ep;
                        file.ExperimentPeptideQ[r] = epe;
                        file.Pep[r] = pepByEntryId.TryGetValue(entryId, out double pv) ? pv : 1.0;
                        file.ExperimentAggregate[r] = expAggByEntryId.TryGetValue(entryId, out double eav)
                            ? eav : scores[r];
                    }
                    t = lane2.Stop(LANE2_LOOKUPS, t);
                    if (laneSink != null)
                        file.Prepared = laneSink.PrepareFile(f, file.ToFileRows());
                    lane2.Stop(LANE2_PREPARE, t);
                    return file;
                }

                // On this thread, in file order: the sink, which tallies, folds the diagnostics
                // accumulator and collapses the experiment records in the order rows arrive.
                void EmitFile(int f, Pass2File file)
                {
                    long t = Stopwatch.GetTimestamp();
                    if (laneSink != null)
                        laneSink.AcceptPrepared(f, file.Prepared);
                    t = order2.Stop(ORDER2_PREPARED, t);
                    RowBuffer rows = file.Rows;
                    int count = rows.Count;
                    for (int r = 0; r < count; r++)
                    {
                        sink.Accept(f, r, rows.EntryIds[r], rows.IsDecoys[r], rows.Charges[r], rows.Peptides[r],
                            file.Scores[r], file.ExperimentAggregate[r], rows.ApexRts[r],
                            new FdrQValues(file.RunPrecursorQ[r], file.RunPeptideQ[r],
                                file.ExperimentPrecursorQ[r], file.ExperimentPeptideQ[r], file.Pep[r]));
                    }
                    // Once per file: a report per row is a lock taken 4 million times a file on
                    // the one thread every other lane is waiting on.
                    gEmit += count;
                    emitProgress.Report(gEmit);
                    order2.Stop(ORDER2_SINK, t);
                }

                OrderedFileLanes.Run(nFiles, fileLanes, AssignFile, EmitFile);
            }
            wall2.Stop();
            log.LogInfo(LogTag.PATH,
                @"{0} pass 2 cost over {1} rows, {2} lane(s): wall {3:F1}s; lanes (summed) parquet walk {4:F1}s, sidecar load {5:F1}s, rescore + run-q recompute {6:F1}s, q-value lookups {7:F1}s, sink preparation {8:F1}s; in order prepared-file merge {9:F1}s, sink {10:F1}s",
                passLabel, n, LanesUsed(fileLanes, nFiles), wall2.Elapsed.TotalSeconds,
                lane2.Seconds(LANE2_WALK), lane2.Seconds(LANE2_SIDECAR), lane2.Seconds(LANE2_RESCORE),
                lane2.Seconds(LANE2_LOOKUPS), lane2.Seconds(LANE2_PREPARE), order2.Seconds(ORDER2_PREPARED),
                order2.Seconds(ORDER2_SINK));
            LogBlockReads(log, passLabel, @"pass 2");
            sink.Finish(log);
            return false;
        }

        /// <summary>
        /// Reservoir decision for the default training selection: does the
        /// <paramref name="seen"/>-th run this precursor appears in take its training slot? True
        /// with probability 1/seen, which leaves every run the precursor actually appears in
        /// equally likely to be the survivor, however few runs contain it.
        ///
        /// Deterministic in <paramref name="baseId"/>, <paramref name="seen"/> and the training
        /// seed rather than drawn from a shared RNG. That makes the decision independent of how
        /// the ingest is SCHEDULED - thread interleaving cannot move it, as a shared RNG's draw
        /// order would - and reproducible across re-runs. It does not make the winner independent
        /// of file ORDER: the surviving ordinal is fixed, so which run holds that ordinal follows
        /// the arrival sequence. Reproducibility therefore rests on the input file list being
        /// ordered, which it is.
        ///
        /// Mixing is the SplitMix64 finalizer: the low bits of a raw base_id are far from uniform
        /// and comparing them directly would skew the draw.
        /// </summary>
        internal static bool ReservoirTakesSlot(uint baseId, uint seen, ulong seed)
        {
            if (seen <= 1)
                return true;
            // Wrapping is the point here - this is a hash mixer, not a quantity - so the
            // multiplications are marked unchecked rather than left to look like an oversight.
            unchecked
            {
                ulong x = baseId + seed * 0x9E3779B97F4A7C15UL + seen * 0xD1B54A32D192ED03UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                x ^= x >> 31;
                return x % seen == 0;
            }
        }

        /// <summary>
        /// Recompute one row's SVM discriminant from its streamed features (issue #4355
        /// struct-shrink S3, Stage B): resolve the parquet feature row by its running ordinal,
        /// standardize it in <paramref name="featureBuf"/>, then the averaged-model score = bias
        /// first + the weight dot product in feature order -- byte-for-byte the resident score
        /// loop's per-entry math (<see cref="ScoreProjectionAndComputeFdrInPlace"/>). Leaves the
        /// standardized values in <paramref name="featureBuf"/> so the caller can bin them into
        /// the feature-contribution accumulator without recomputing. Linear only: a tree
        /// ensemble scores its file before the row loop, in <see cref="ScoresBeforeRowLoop"/>.
        /// </summary>
        private static double ComputeStreamedScore(
            double[] avgWeights, double avgBias, FeatureStandardizer standardizer, double[] featureBuf,
            IReadOnlyList<double[]> rows, int parquetIndex, double coelutionSum, int nFeatures)
        {
            StandardizeFeatureRow(standardizer, featureBuf, rows, (uint)parquetIndex, coelutionSum, nFeatures);
            double score = avgBias;
            for (int j = 0; j < nFeatures; j++)
                score += avgWeights[j] * featureBuf[j];
            return score;
        }

        /// <summary>
        /// Resolve one row's raw features by its parquet row index and standardize them into
        /// <paramref name="featureBuf"/>, which is returned. The first half of
        /// <see cref="ComputeStreamedScore"/>, and on its own what the tree paths'
        /// feature-distribution report bins: the same copy and transform
        /// <see cref="ScoreRowsGbt"/> applies before it scores, so the report sees the values the
        /// trees did.
        /// </summary>
        private static double[] StandardizeFeatureRow(FeatureStandardizer standardizer, double[] featureBuf,
            IReadOnlyList<double[]> rows, uint parquetIndex, double coelutionSum, int nFeatures)
        {
            double[] featRow = ResolveFeatureRow(rows, parquetIndex, coelutionSum, nFeatures);
            Array.Copy(featRow, 0, featureBuf, 0, nFeatures);
            standardizer.TransformSlice(featureBuf);
            return featureBuf;
        }

        /// <summary>
        /// One file's scores when they are known before its row loop runs in
        /// <see cref="RunStreamingFirstPass"/>, or <c>null</c> when that loop computes them row
        /// by row. Known when the caller read them back off the file's own sidecar
        /// (<paramref name="completedScores"/>, returned as is), or when the model is a tree
        /// ensemble: a tree score is a pure function of its row, so the whole file is scored here
        /// in parallel (<see cref="ScoreRowsGbt"/>), which is what the projection score pass does
        /// too, straight into <paramref name="destination"/> - the loop's own score array, so no
        /// second per-file array is allocated - which is returned. The linear model returns
        /// <c>null</c> and keeps the serial per-row dot product: the parity-locked form, and the
        /// one that leaves each row's standardized vector behind for the feature-contribution
        /// report.
        /// </summary>
        private static double[] ScoresBeforeRowLoop(
            double[] completedScores, IReadOnlyList<double[]> rows, IReadOnlyList<double> coelutionSums,
            IReadOnlyList<GradientBoostedTrees> gbtModels, FeatureStandardizer standardizer,
            int nFeatures, int nThreads, double[] destination)
        {
            if (completedScores != null || gbtModels == null)
                return completedScores;
            // The running row ordinal IS the parquet row index here, exactly as it is for
            // ComputeStreamedScore: the rows arrive in parquet order.
            ScoreRowsGbt(destination.Length, r => ResolveFeatureRow(rows, (uint)r, coelutionSums[r], nFeatures),
                gbtModels, standardizer, nFeatures, destination, 0, nThreads);
            return destination;
        }

        /// <summary>
        /// The feature-contribution report a score pass hands on. For the linear model it is the
        /// full decomposition, printed as it always was. A tree ensemble has no weights to
        /// decompose, so its report carries only the per-feature target/decoy distributions
        /// (<see cref="FeatureContributions.Accumulator.BuildForTreeEnsemble"/>), and only when
        /// <c>--model-diagnostics</c> asked for them (<see cref="AccumulatesTreeFeatures"/>);
        /// otherwise null, as it always was for trees.
        /// </summary>
        private static FeatureContributions BuildContributions(FeatureContributions.Accumulator contribAcc,
            IReadOnlyList<GradientBoostedTrees> gbtModels, IReadOnlyList<double[]> foldWeights,
            PercolatorConfig config)
        {
            if (gbtModels != null)
                return AccumulatesTreeFeatures(gbtModels, config) ? contribAcc.BuildForTreeEnsemble(config.FeatureInfos) : null;
            var contributions = contribAcc.Build(foldWeights, config.FeatureInfos);
            PercolatorDiagnosticsDump.EmitFeatureContributions(contributions);
            return contributions;
        }

        /// <summary>
        /// Whether a score pass under a tree model standardizes each row for the feature report.
        /// Only when the distributions were asked for (<see cref="PercolatorConfig.CollectFeatureHistograms"/>,
        /// set by <c>--model-diagnostics</c>): the parallel tree score pass keeps no standardized
        /// vector per row, so this is extra serial work the production gbdt run should not pay.
        /// The linear path always accumulates, as it always has.
        /// </summary>
        private static bool AccumulatesTreeFeatures(IReadOnlyList<GradientBoostedTrees> gbtModels,
            PercolatorConfig config)
        {
            return gbtModels != null && config.CollectFeatureHistograms;
        }

        /// <summary>
        /// The refusal <see cref="RunStreamingFirstPass"/> throws for a pretrained model of the
        /// other classifier. Names the two classifiers by what they are, and claims neither which
        /// flag trained the persisted model nor what it scored: the scorer knows neither.
        /// </summary>
        internal static string ClassifierMismatchMessage(bool modelIsTrees, bool runUsesTrees)
        {
            return string.Format(
                @"The persisted first-pass model offered for reuse is {0}, but this run uses {1}, " +
                @"and a model of one classifier cannot score a run of the other. The task validity " +
                @"key records the classifier, so a current model should always match; this points " +
                @"to an inconsistency in the intermediate files. Re-run with a clean output directory.",
                PercolatorResults.ClassifierName(modelIsTrees), PercolatorResults.ClassifierName(runUsesTrees));
        }

        /// <summary>
        /// One file's raw parquet stub rows, buffered by the 1st-pass streaming score path
        /// (<see cref="RunStreamingFirstPass"/>) so the stream callback only appends to
        /// reference-type lists (never a captured-and-mutated counter) and the row/global ordinals
        /// advance in plain indexed loops. Bounded to one file at a time -- the same one-file
        /// resident set the per-file run-q competition already requires. Five of the six
        /// parallel lists mirror the <see cref="FdrProjection"/> scalar slice the resident path
        /// holds; <see cref="ApexRts"/> is the sixth, and it is scoring INPUT to nothing - it is
        /// carried so this file's <c>.1st-pass.fdr_scores.bin</c> can persist it (format v7,
        /// issue #4522).
        /// </summary>
        private sealed class RowBuffer
        {
            public readonly List<uint> EntryIds;
            public readonly List<byte> Charges;
            public readonly List<bool> IsDecoys;
            public readonly List<double> CoelutionSums;
            public readonly List<string> Peptides;
            public readonly List<double> ApexRts;

            public RowBuffer() : this(0)
            {
            }

            /// <param name="capacity">The file's row count when already known, so the lists are
            /// sized once rather than grown by doubling through ~4M rows.</param>
            public RowBuffer(int capacity)
            {
                EntryIds = new List<uint>(capacity);
                Charges = new List<byte>(capacity);
                IsDecoys = new List<bool>(capacity);
                CoelutionSums = new List<double>(capacity);
                Peptides = new List<string>(capacity);
                ApexRts = new List<double>(capacity);
            }

            public int Count => EntryIds.Count;

            public void Clear()
            {
                EntryIds.Clear();
                Charges.Clear();
                IsDecoys.Clear();
                CoelutionSums.Clear();
                Peptides.Clear();
                ApexRts.Clear();
            }

            public void Add(uint entryId, byte charge, bool isDecoy, double coelutionSum,
                string peptide, double apexRt)
            {
                EntryIds.Add(entryId);
                Charges.Add(charge);
                IsDecoys.Add(isDecoy);
                CoelutionSums.Add(coelutionSum);
                ApexRts.Add(apexRt);
                // Normalize a null modseq to string.Empty exactly as the resident FdrProjectionSet
                // .Builder.AddRow does (a present-but-null modified_sequence element survives
                // ReadFdrStubScalars' column-level guard): a null peptide would otherwise throw as a
                // Dictionary<string,...> key in StreamingFdr.StreamingFirstPassQ / SubsampleByPeptideGroup and
                // would group differently from the resident path's ""-normalized peptides.
                Peptides.Add(peptide ?? string.Empty);
            }
        }

        /// <summary>
        /// One file's rows, read into a buffer of its own so several files can be read at once.
        /// </summary>
        private static RowBuffer ReadFileRows(
            Action<string, StubColumns, Action<uint, byte, bool, double, string, double>> streamFileRows,
            string fileName, StubColumns columns, int capacity = 0)
        {
            var rows = new RowBuffer(capacity);
            streamFileRows(fileName, columns, rows.Add);
            return rows;
        }

        /// <summary>
        /// Scores every row of a pass-1 file on its lane, keeping each row's standardized feature
        /// vector for the in-order contribution sums and binning the order-free histograms here.
        /// A tree ensemble scores the whole file first (<see cref="ScoresBeforeRowLoop"/>) and keeps
        /// the vectors only when the report asked for its per-feature distributions
        /// (<paramref name="accumulateTreeFeatures"/>); otherwise the file contributes nothing.
        /// </summary>
        private static void ScoreRows(Pass1File file, IReadOnlyList<double[]> featureRows,
            IReadOnlyList<GradientBoostedTrees> gbtModels, double[] avgWeights, double avgBias,
            FeatureStandardizer standardizer, int nFeatures, bool collectHistograms,
            bool accumulateTreeFeatures, int nThreads)
        {
            RowBuffer rows = file.Rows;
            int count = rows.Count;
            var scores = new double[count];
            bool linear = ScoresBeforeRowLoop(null, featureRows, rows.CoelutionSums, gbtModels,
                standardizer, nFeatures, nThreads, scores) == null;
            file.Scores = scores;
            if (!linear && !accumulateTreeFeatures)
                return;
            // checked: one flat array per file, so a file past ~100M rows must fail loudly here
            // rather than wrap the index and overwrite its own vectors.
            var standardized = new double[checked(count * nFeatures)];
            var histograms = collectHistograms ? new FeatureContributions.Accumulator(nFeatures, true) : null;
            var featureBuf = new double[nFeatures];
            for (int r = 0; r < count; r++)
            {
                // Both leave featureBuf standardized - the vector the contribution report sums and
                // bins. The trees already scored the row; the linear model scores it here.
                if (linear)
                {
                    scores[r] = ComputeStreamedScore(
                        avgWeights, avgBias, standardizer, featureBuf, featureRows, r, rows.CoelutionSums[r], nFeatures);
                }
                else
                {
                    StandardizeFeatureRow(standardizer, featureBuf, featureRows, (uint)r, rows.CoelutionSums[r], nFeatures);
                }
                Array.Copy(featureBuf, 0, standardized, r * nFeatures, nFeatures);
                histograms?.AddHistogram(featureBuf, 0, rows.IsDecoys[r]);
            }
            file.Standardized = standardized;
            file.Histograms = histograms;
        }

        /// <summary>The lanes a pass actually runs: never more than the files it has.</summary>
        private static int LanesUsed(int fileLanes, int nFiles)
        {
            return Math.Max(1, Math.Min(fileLanes, nFiles));
        }

        // Experimental block-read totals (OSPREY_BLOCK_READ_MB) at a phase boundary; silent when off.
        private static void LogBlockReads(IOspreyLog log, string passLabel, string phase)
        {
            string text = BlockReadStats.Text();
            if (text != null)
                log.LogInfo(LogTag.PATH, @"{0} after {1}: {2}", passLabel, phase, text);
        }

        /// <summary>
        /// What a pass-1 lane hands the in-order consumer for one file: the rows and their scores,
        /// with the standardized vectors whose sums must be added in row order and the
        /// histogram counts that need not be.
        /// </summary>
        private sealed class Pass1File
        {
            public RowBuffer Rows;
            public double[] Scores;
            // Row-major, nFeatures per row; null for a file whose scores came off its sidecar.
            public double[] Standardized;
            // Null unless histograms are collected and the file was scored here.
            public FeatureContributions.Accumulator Histograms;
            // This file's share of the competition, or null when the consumer adds its rows.
            public StreamingFdr.StreamingFirstPassQ.FileReduction Competition;
        }

        /// <summary>
        /// What a pass-2 lane hands the in-order sink for one file: every value
        /// <see cref="IFdrOutputSink.Accept"/> takes, computed on the lane.
        /// </summary>
        private sealed class Pass2File
        {
            public Pass2File(RowBuffer rows, double[] scores, double[] runPrecursorQ, double[] runPeptideQ)
            {
                Rows = rows;
                Scores = scores;
                RunPrecursorQ = runPrecursorQ;
                RunPeptideQ = runPeptideQ;
                int count = rows.Count;
                ExperimentPrecursorQ = new double[count];
                ExperimentPeptideQ = new double[count];
                Pep = new double[count];
                ExperimentAggregate = new double[count];
            }

            public RowBuffer Rows { get; }
            public double[] Scores { get; }
            public double[] RunPrecursorQ { get; }
            public double[] RunPeptideQ { get; }
            public double[] ExperimentPrecursorQ { get; }
            public double[] ExperimentPeptideQ { get; }
            public double[] Pep { get; }
            public double[] ExperimentAggregate { get; }

            /// <summary>What <see cref="IFdrFileLaneSink.PrepareFile"/> returned for this file, if the
            /// sink prepares files.</summary>
            public object Prepared { get; set; }

            public FdrFileRows ToFileRows()
            {
                return new FdrFileRows(Rows.EntryIds, Rows.IsDecoys, Rows.Charges, Rows.Peptides, Scores,
                    RunPrecursorQ, RunPeptideQ, ExperimentPrecursorQ, ExperimentPeptideQ, Pep);
            }
        }

        /// <summary>
        /// Elapsed time per named bucket, safe to add to from several lanes at once. Callers read
        /// the clock once per file and chain the timestamp, so a bucket costs two counter reads per
        /// file however many rows it covers.
        /// </summary>
        private sealed class LaneCost
        {
            private readonly long[] _ticks;

            public LaneCost(int buckets)
            {
                _ticks = new long[buckets];
            }

            /// <summary>Adds the time since <paramref name="start"/> to the bucket and returns
            /// now, to start the next one.</summary>
            public long Stop(int bucket, long start)
            {
                long now = Stopwatch.GetTimestamp();
                Interlocked.Add(ref _ticks[bucket], now - start);
                return now;
            }

            public double Seconds(int bucket)
            {
                return Interlocked.Read(ref _ticks[bucket]) / (double)Stopwatch.Frequency;
            }
        }

        /// <summary>
        /// Bucket entry indices by source file name, preserving first-seen file
        /// order. The streaming feature loads (issue #4355 Phase 4) iterate these
        /// buckets so <c>loadFileFeatures</c> is called exactly once per file and
        /// only one file's rows are held resident at a time.
        /// </summary>
        internal static Dictionary<string, List<int>> GroupIndicesByFileName(
            IList<PercolatorEntry> entries)
        {
            var byFile = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                string file = entries[i].FileName;
                List<int> list;
                if (!byFile.TryGetValue(file, out list))
                {
                    list = new List<int>();
                    byFile[file] = list;
                }
                list.Add(i);
            }
            return byFile;
        }

        /// <summary>
        /// Resolve one entry's 21-feature vector from a file's freshly loaded
        /// parquet rows by <see cref="PercolatorEntry.ParquetIndex"/>. Falls back
        /// to the basic feature vector (built from the resident coelution_sum) when
        /// the index is out of range -- the same fallback the pre-streaming
        /// <c>PercolatorEntryBuilder</c> applied to entries without a loadable row
        /// (e.g. a stub/parquet mismatch, or a <c>uint.MaxValue</c> appended
        /// entry). The returned array is the live parquet row (not a copy); callers
        /// that retain it beyond the current file's scope must clone.
        /// </summary>
        internal static double[] ResolveFeatureRow(
            IReadOnlyList<double[]> rows, uint parquetIndex, double coelutionSum,
            int numFeatures)
        {
            int idx = (int)parquetIndex;
            if (rows != null && idx >= 0 && idx < rows.Count)
                return rows[idx];
            return PercolatorEntryBuilder.BuildBasicFeatures(coelutionSum, numFeatures);
        }

        /// <summary>
        /// Score <paramref name="rows"/> with fold <paramref name="fold"/>'s model,
        /// whichever classifier this run trained. Exactly one of
        /// <paramref name="svmModels"/> / <paramref name="gbtModels"/> is non-null.
        /// </summary>
        internal static double[] ScoreWithFoldModel(
            LinearSvmClassifier[] svmModels, GradientBoostedTrees[] gbtModels,
            int fold, Matrix rows)
        {
            if (gbtModels == null)
                return svmModels[fold].DecisionFunction(rows);

            var model = gbtModels[fold];
            var scores = new double[rows.Rows];
            var rowBuf = new double[rows.Cols];
            for (int i = 0; i < rows.Rows; i++)
            {
                MatrixRows.CopyRow(rows, i, rowBuf);
                scores[i] = model.ScoreSingle(rowBuf);
            }
            return scores;
        }

        /// <summary>
        /// The single place a standardized feature row becomes a score on the
        /// full-population passes, for both classifiers: the averaged tree margin when
        /// <paramref name="gbtModels"/> is non-null, otherwise the averaged-weights dot
        /// product. Keeping both here is what lets the resident
        /// (<see cref="ScorePopulationAndComputeFdr"/>) and projection
        /// (<see cref="ScoreProjectionAndComputeFdrInPlace"/>) score passes stay a
        /// single shared implementation across the two methods. Internal rather than
        /// private so <see cref="FrozenModelScorer"/> -- the 2nd-pass transfer paths'
        /// view of a trained model -- applies it identically.
        /// </summary>
        internal static double ScoreStandardizedRow(
            IReadOnlyList<GradientBoostedTrees> gbtModels,
            double[] avgWeights, double avgBias, double[] stdRow)
        {
            if (gbtModels != null)
                return AverageGbtScore(gbtModels, stdRow);

            double score = avgBias;
            for (int j = 0; j < avgWeights.Length; j++)
                score += avgWeights[j] * stdRow[j];
            return score;
        }

        /// <summary>Average raw margin over the per-fold tree ensembles -- the tree
        /// analogue of the SVM path's averaged-weights dot product. See
        /// <see cref="PercolatorResults.FoldGbtModels"/> for why trees average scores
        /// rather than models.</summary>
        private static double AverageGbtScore(
            IReadOnlyList<GradientBoostedTrees> models, double[] stdRow)
        {
            double sum = 0.0;
            for (int f = 0; f < models.Count; f++)
                sum += models[f].ScoreSingle(stdRow);
            return sum / models.Count;
        }

        /// <summary>The trained tree ensembles, or <c>null</c> when this run trained the
        /// linear SVM. Normalizes the empty-list case to null so every score path can
        /// select the classifier on a single null check.</summary>
        private static List<GradientBoostedTrees> ResolveGbtModels(PercolatorResults trainResults)
        {
            var models = trainResults.FoldGbtModels;
            return models != null && models.Count > 0 ? models : null;
        }

        /// <summary>
        /// Score <paramref name="count"/> rows with the tree ensembles, in parallel over
        /// contiguous chunks (one standardization buffer per chunk, disjoint writes into
        /// <paramref name="scores"/> from <paramref name="baseIndex"/>). Bit-identical to the
        /// serial loop: each row's score depends only on that row, and the tree path has no
        /// cross-row accumulation to order. Chunked rather than per-row so the work per
        /// <see cref="OspreyParallel"/> interlocked hand-out is a whole slice, not one row.
        /// Shared by the projection score pass and the streaming first pass, which differ only
        /// in how row r's raw feature vector is found - <paramref name="rawRowAt"/>, which is
        /// called concurrently and whose result is only read.
        /// </summary>
        internal static void ScoreRowsGbt(
            int count,
            Func<int, double[]> rawRowAt,
            IReadOnlyList<GradientBoostedTrees> gbtModels,
            FeatureStandardizer standardizer,
            int nFeatures,
            double[] scores,
            int baseIndex,
            int nThreads)
        {
            if (count == 0)
                return;
            int threads = Math.Max(1, Math.Min(nThreads, count));
            int chunk = (count + threads - 1) / threads;
            OspreyParallel.For(0, threads, threads, c =>
            {
                var featureBuf = new double[nFeatures];   // per-chunk: never shared
                int lo = c * chunk;
                int hi = Math.Min(lo + chunk, count);
                for (int r = lo; r < hi; r++)
                {
                    Array.Copy(rawRowAt(r), 0, featureBuf, 0, nFeatures);
                    standardizer.TransformSlice(featureBuf);
                    scores[baseIndex + r] = AverageGbtScore(gbtModels, featureBuf);
                }
            });
        }
    }
}
