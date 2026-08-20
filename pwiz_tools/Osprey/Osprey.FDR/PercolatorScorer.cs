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

                    if (gbtModels == null)
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

                        if (gbtModels == null)
                            contribAcc.Add(featureBuf, entry.IsDecoy);
                    }
                }
            }

            // Null on the tree path: the report is a decomposition of linear weights.
            // See the matching comment in PercolatorTrainer.RunPercolator.
            FeatureContributions contributions = null;
            if (gbtModels == null)
            {
                contributions = contribAcc.Build(trainResults.FoldWeights, config.FeatureInfos);
                PercolatorDiagnosticsDump.EmitFeatureContributions(contributions);
            }

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
            IFdrOutputSink sink,
            Action<FeatureContributions> captureContributions = null,
            bool applyExperimentAgg = true)
        {
            if (loadFileFeatures == null)
                throw new InvalidOperationException(
                    @"ScoreProjectionAndComputeFdrInPlace requires a per-file feature loader: " +
                    @"the projection carries no resident feature vectors.");

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

            // Streaming score pass over the projection, one file at a time. The
            // per-entry math and the per-file iteration order match the
            // PercolatorEntry streaming loop exactly, so finalScores + the
            // contribution sums are byte-for-byte identical.
            // Per-file progress so this full-population score pass (~15 min silent at
            // 344M rows on the 82-file first-pass join) shows movement; the heartbeat
            // covers a slow single file. Console-only -- never touches finalScores /
            // the sink, so byte-identity is unaffected.
            int gi = 0;
            using (var scoreProgress = new ProgressReporter(string.Format(@"Scoring {0} entries", n), n))
            {
                foreach (var kvp in perFile)
                {
                    IReadOnlyList<double[]> rows = loadFileFeatures(kvp.Key);
                    var projRows = kvp.Value;
                    if (gbtModels != null)
                    {
                        // Tree path: parallel over contiguous row chunks. A tree score is a
                        // pure function of its own row written to its own slot, and this path
                        // accumulates nothing across rows (contributions are linear-only), so
                        // this is bit-identical to scoring the file serially -- there is no
                        // float accumulation whose order could drift. Worth doing: a tree row
                        // costs ~NFolds x NTrees x depth node traversals against the linear
                        // path's NFeatures multiply-adds, so serial scoring dominates the run.
                        ScoreProjectionRowsGbt(rows, projRows, gbtModels, standardizer,
                            nFeatures, finalScores, gi, config.NThreads);
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

            // Null on the tree path (no linear weights to decompose); see PercolatorTrainer.RunPercolator.
            FeatureContributions contributions = null;
            if (gbtModels == null)
            {
                contributions = contribAcc.Build(trainResults.FoldWeights, config.FeatureInfos);
                PercolatorDiagnosticsDump.EmitFeatureContributions(contributions);
            }
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
            var pepByWinnerIdx = PercolatorQValues.ComputePepWinnerMap(finalScores, labels, entryIds);

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
            using (var floorProgress = PercolatorQValues.QProgress(@"Per-run q-value floors", perFile.Count, n))
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

                    double pep = pepByWinnerIdx.TryGetValue(g, out double pv) ? pv : 1.0;

                    double ea = expAggByEntryId.TryGetValue(entryIds[g], out double eav)
                        ? eav : finalScores[g];

                    projRows[r] = projRows[r].WithScore(finalScores[g]);
                    sink.Accept(fileIdx, r, projRows[r].EntryId, projRows[r].IsDecoy,
                        projRows[r].Charge, pept, finalScores[g], ea,
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
        /// 1st-pass-ONLY streaming Percolator that holds NO resident row buffer at all (issue
        /// #4355 struct-shrink S3, Stage B -- the FLAT-memory win): the memory-collapsing fork of
        /// <see cref="PercolatorEngine.RunStreamingIntoProjection"/> +
        /// <see cref="ScoreProjectionAndComputeFdrInPlace"/>. Where those hold the resident
        /// <see cref="FdrProjection"/>[] + the flat <c>labels/entryIds/peptides/finalScores[n]</c>
        /// arrays (O(pre-compaction rows), the 82->500 file blocker), this streams every row's
        /// identity + features straight from parquet THREE times -- once to select the training
        /// subset, once to score + build the bounded q-value maps + clamp floors, once to score +
        /// emit -- recomputing the SVM score per row instead of parking an O(n) score array. Only
        /// the SUBSET (&lt;= MaxTrainSize) and the intrinsically-bounded lookups (O(base_ids) /
        /// O(peptides), via <see cref="StreamingFdr.StreamingFirstPassQ"/> + per-file run-q) are ever resident,
        /// so the peak is FLAT in file count.
        ///
        /// Byte-identical to the resident projection path on the same rows in the same (file,row)
        /// order (verified by <c>FdrTest.TestStreamingFirstPassMatchesProjection</c>): the
        /// training-subset selection reproduces <see cref="PercolatorSampling.SelectBestPerPrecursor"/> +
        /// <see cref="PercolatorSampling.BuildTrainingSubset"/> (strict-<c>&gt;</c> first-seen dedup ranked on
        /// CoelutionSum, ascending-global-ordinal order, identical
        /// <see cref="PercolatorSampling.SubsampleByPeptideGroup"/>), the SVM training runs the SAME
        /// <see cref="PercolatorTrainer.RunPercolator"/> on the SAME subset, and the score + q-value math reuses the
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
            Action<string, Action<uint, byte, bool, double, string>> streamFileRows,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures,
            PercolatorConfig percConfig,
            Action<string> logInfo,
            string passLabel,
            IFdrOutputSink sink,
            Action<FeatureContributions> captureContributions = null,
            Action<PercolatorResults> captureModel = null)
        {
            if (streamFileRows == null)
                throw new ArgumentNullException(nameof(streamFileRows));
            if (loadFileFeatures == null)
                throw new ArgumentNullException(nameof(loadFileFeatures));
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));

            int nFiles = fileNames.Count;
            int nFeatures = percConfig.FeatureInfos.Length;
            int maxTrain = percConfig.MaxTrainSize;
            // One file's raw rows buffered at a time (bounded -- the same one-file resident set the
            // per-file run-q already needs). The stream callback only appends to the buffer's lists
            // (reference types, never reassigned), so the running row/global ordinals are advanced
            // in the plain indexed loops below, not captured-and-mutated inside the closure.
            var buffer = new RowBuffer();

            // ---- Pass 0: stream identity, build the training subset, train the model ----
            // Best-per-precursor dedup captured WITH identity, in flat (file,row) order. Strict
            // '>' on CoelutionSum with first-seen (lowest g) winning ties -- exactly
            // SelectBestPerPrecursor iterating ascending global index. bestScores == CoelutionSum
            // (byte-identical to Features[0] on the 1st pass), so no feature load is needed here.
            var bestTarget = new Dictionary<uint, FirstPassDedupRow>();
            var bestDecoy = new Dictionary<uint, FirstPassDedupRow>();
            int g = 0;
            int nInputTargets = 0, nInputDecoys = 0;
            // This pass streams every file's parquet rows before the [PATH] line below, so it is a
            // determinate O(files) I/O step (43s at 82 files, minutes at 500). Report per-file
            // progress through the standard throttled reporter so a large join never goes silent.
            var ingestProgress = new ProgressReporter(
                string.Format(@"Streaming first-pass ingest from {0} file(s)", nFiles), nFiles,
                intervalSeconds: ProgressReporter.IO_INTERVAL_SECONDS);
            for (int f = 0; f < nFiles; f++)
            {
                string file = fileNames[f];
                buffer.Clear();
                streamFileRows(file, buffer.Add);
                int count = buffer.Count;
                for (int r = 0; r < count; r++)
                {
                    uint entryId = buffer.EntryIds[r];
                    bool isDecoy = buffer.IsDecoys[r];
                    double coelutionSum = buffer.CoelutionSums[r];
                    uint baseId = entryId & PercolatorEntry.BASE_ID_MASK;
                    var map = isDecoy ? bestDecoy : bestTarget;
                    if (map.TryGetValue(baseId, out FirstPassDedupRow existing))
                    {
                        if (coelutionSum > existing.CoelutionSum)
                            map[baseId] = new FirstPassDedupRow(
                                g, file, entryId, buffer.Charges[r], isDecoy, (uint)r, coelutionSum, buffer.Peptides[r]);
                    }
                    else
                    {
                        map[baseId] = new FirstPassDedupRow(
                            g, file, entryId, buffer.Charges[r], isDecoy, (uint)r, coelutionSum, buffer.Peptides[r]);
                    }
                    if (isDecoy) nInputDecoys++; else nInputTargets++;
                    g++;
                }
                ingestProgress.Report(f + 1);
            }
            ingestProgress.Dispose();
            int n = g;
            logInfo(string.Format(
                @"[PATH] {0} streaming ingest (RunStreamingFirstPass): {1} rows", passLabel, n));
            logInfo(string.Format(
                "[COUNT] {0} Percolator input: {1} entries ({2} targets, {3} decoys, {4} features)",
                passLabel, n, nInputTargets, nInputDecoys, nFeatures));

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
            logInfo(string.Format(
                "[COUNT] {0} Percolator streaming best-per-precursor: {1} entries ({2} targets, {3} decoys) from {4} total",
                passLabel, m, dedupTargets, m - dedupTargets, n));

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
            logInfo(string.Format(
                "[COUNT] {0} Percolator streaming subsample: {1} entries ({2} targets, {3} decoys)",
                passLabel, subsetEntries.Count, subTargets, subsetEntries.Count - subTargets));

            // Load ONLY the subset's feature vectors, one file at a time (bounded by MaxTrainSize),
            // cloning each row so the subset entry owns it -- mirrors RunStreamingIntoProjection.
            var subsetByFile = GroupIndicesByFileName(subsetEntries);
            int subsetFilesLoaded = 0;
            using (var loadProgress = new ProgressReporter(string.Format(
                       @"Loading training-subset feature vectors from {0} file(s)", subsetByFile.Count), subsetByFile.Count))
            foreach (var kvp in subsetByFile)
            {
                IReadOnlyList<double[]> rows = loadFileFeatures(kvp.Key);
                foreach (int k in kvp.Value)
                {
                    var entry = subsetEntries[k];
                    entry.Features = (double[])ResolveFeatureRow(
                        rows, entry.ParquetIndex, entry.CoelutionSum, nFeatures).Clone();
                }
                loadProgress.Report(++subsetFilesLoaded);
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
            PercolatorResults trainResults = PercolatorTrainer.RunPercolator(subsetEntries, trainConfig);
            if (trainResults.DiagnosticAbort)
                return true;

            // Publish the frozen first-pass model (fold weights + biases + standardizer) for the
            // OSPREY_PASS2_QVALUE=transfer pass-2 step. No-op (null) on the default path, so this
            // streaming first pass stays byte-identical. See TODO-osprey_pass2_per_run_only_qvalue.
            captureModel?.Invoke(trainResults);

            // Release the pass-0 working sets before the score passes so only the bounded lookups
            // remain resident across the peak.
            bestTarget = null;
            bestDecoy = null;
            dedup = null;
            localSelected = null;
            subsetEntries = null;
            subsetByFile = null;

            // Average fold weights + biases (identical to ScoreProjectionAndComputeFdrInPlace).
            int nModels = trainResults.FoldWeights.Count;
            if (nModels == 0)
                throw new InvalidOperationException(
                    @"RunStreamingFirstPass: trainResults contains no fold models");
            var avgWeights = new double[nFeatures];
            double avgBias = 0.0;
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
            var standardizer = trainResults.Standardizer;
            var featureBuf = new double[nFeatures];

            // ---- Pass 1: score + build the 3 bounded q maps + reduce the clamp floors ----
            // Reuses the verified StreamingFdr.StreamingFirstPassQ kernel; per-file run-q from a bounded one-file
            // buffer, reduced into the best-of-runs clamp floors (issue #4390). The score is
            // recomputed per row (bias first, then the averaged-weight dot product in feature order)
            // -- byte-for-byte the resident score loop, only without the O(n) finalScores array.
            // Gate the aggregation on the pass label, exactly as the resident and projection score
            // passes do. This method has one caller and it passes FIRST_PASS_LABEL, so today the
            // gate is a no-op - but an ungated read of MeanBestN here is the identical shape of the
            // defect that let the 2nd pass re-aggregate on the other two paths, and a future
            // second-pass caller would reintroduce it silently.
            var streamingQ = new StreamingFdr.StreamingFirstPassQ(
                passLabel == PercolatorEngine.FIRST_PASS_LABEL ? OspreyEnvironment.MeanBestN : 0);
            var minRunBothByEntryId = new Dictionary<uint, double>();
            var minRunBothByPeptide = new Dictionary<(string, bool), double>();
            var contribAcc = new FeatureContributions.Accumulator(nFeatures, percConfig.CollectFeatureHistograms);
            int nonEmptyFiles = 0;
            int g1 = 0;
            logInfo(string.Format(@"Running {0} Percolator on {1} entries...", passLabel, n));
            // Fill the previously-silent multi-minute streaming score pass with throttled percent,
            // mirroring the resident ScoreProjectionAndComputeFdrInPlace "Scoring N entries" line.
            // Progress is log-only (OspreyOutput.Out), so the FDR output stays byte-identical.
            using (var scoreProgress = new ProgressReporter(string.Format(@"Scoring {0} entries", n), n))
            for (int f = 0; f < nFiles; f++)
            {
                IReadOnlyList<double[]> rows = loadFileFeatures(fileNames[f]);
                buffer.Clear();
                streamFileRows(fileNames[f], buffer.Add);
                int count = buffer.Count;
                if (count > 0)
                    nonEmptyFiles++;
                var fScores = new double[count];
                var fLabels = new bool[count];
                var fEntryIds = new uint[count];
                var fPeptides = new string[count];
                for (int r = 0; r < count; r++)
                {
                    // ComputeStreamedScore leaves featureBuf standardized, which contribAcc bins.
                    double score = ComputeStreamedScore(
                        avgWeights, avgBias, standardizer, featureBuf, rows, r, buffer.CoelutionSums[r], nFeatures);
                    bool isDecoy = buffer.IsDecoys[r];
                    fScores[r] = score;
                    fLabels[r] = isDecoy;
                    fEntryIds[r] = buffer.EntryIds[r];
                    fPeptides[r] = buffer.Peptides[r];
                    streamingQ.Add(g1, score, buffer.EntryIds[r], isDecoy, buffer.Peptides[r]);
                    contribAcc.Add(featureBuf, isDecoy);
                    g1++;
                    scoreProgress.Report(g1);
                }
                PercolatorQValues.ComputePerFileRunQvalues(
                    fScores, fLabels, fEntryIds, fPeptides, 0, count,
                    out double[] runPrecFile, out double[] runPeptFile);
                for (int r = 0; r < count; r++)
                {
                    double runBoth = Math.Max(runPrecFile[r], runPeptFile[r]);
                    PercolatorQValues.UpdateExperimentQClampFloor(
                        minRunBothByEntryId, minRunBothByPeptide, fEntryIds[r], fPeptides[r], fLabels[r], runBoth);
                }
            }

            var contributions = contribAcc.Build(trainResults.FoldWeights, percConfig.FeatureInfos);
            PercolatorDiagnosticsDump.EmitFeatureContributions(contributions);
            captureContributions?.Invoke(contributions);

            // Finalize the bounded lookups. PEP is global (built always); the experiment maps use
            // the single-file shortcut (exp == per-run) so they are built only when multi-file --
            // matching ScoreProjectionAndComputeFdrInPlace exactly.
            var pepByWinnerIdx = streamingQ.BuildPepWinnerMap();
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
            int gEmit = 0;
            using (var emitProgress = new ProgressReporter(string.Format(@"Assigning q-values to {0} entries", n), n))
            for (int f = 0; f < nFiles; f++)
            {
                IReadOnlyList<double[]> rows = loadFileFeatures(fileNames[f]);
                buffer.Clear();
                streamFileRows(fileNames[f], buffer.Add);
                int count = buffer.Count;
                var fScores = new double[count];
                var fLabels = new bool[count];
                var fEntryIds = new uint[count];
                var fPeptides = new string[count];
                var fCharges = new byte[count];
                for (int r = 0; r < count; r++)
                {
                    fScores[r] = ComputeStreamedScore(
                        avgWeights, avgBias, standardizer, featureBuf, rows, r, buffer.CoelutionSums[r], nFeatures);
                    fLabels[r] = buffer.IsDecoys[r];
                    fEntryIds[r] = buffer.EntryIds[r];
                    fPeptides[r] = buffer.Peptides[r];
                    fCharges[r] = buffer.Charges[r];
                }
                PercolatorQValues.ComputePerFileRunQvalues(
                    fScores, fLabels, fEntryIds, fPeptides, 0, count,
                    out double[] runPrecFile, out double[] runPeptFile);
                for (int r = 0; r < count; r++)
                {
                    double rp = runPrecFile[r];
                    double rpe = runPeptFile[r];

                    double ep = isSingleFile
                        ? rp
                        : (expPrecByWinnerId.TryGetValue(fEntryIds[r], out double epv) ? epv : 1.0);
                    if (minRunBothByEntryId.TryGetValue(fEntryIds[r], out double floorPrec) && floorPrec > ep)
                        ep = floorPrec;

                    string pept = fPeptides[r];
                    double epe = isSingleFile
                        ? rpe
                        : (expPeptByPeptide.TryGetValue(pept, out double epev) ? epev : 1.0);
                    if (!string.IsNullOrEmpty(pept) &&
                        minRunBothByPeptide.TryGetValue((pept, fLabels[r]), out double floorPept) && floorPept > epe)
                        epe = floorPept;

                    double pep = pepByWinnerIdx.TryGetValue(gEmit + r, out double pv) ? pv : 1.0;

                    double ea = expAggByEntryId.TryGetValue(fEntryIds[r], out double eav)
                        ? eav : fScores[r];

                    sink.Accept(f, r, fEntryIds[r], fLabels[r], fCharges[r], pept, fScores[r], ea,
                        new FdrQValues(rp, rpe, ep, epe, pep));
                    emitProgress.Report(gEmit + r + 1);
                }
                gEmit += count;
            }
            sink.Finish(logInfo);
            return false;
        }

        /// <summary>
        /// Recompute one row's SVM discriminant from its streamed features (issue #4355
        /// struct-shrink S3, Stage B): resolve the parquet feature row by its running ordinal,
        /// standardize it in <paramref name="featureBuf"/>, then the averaged-model score = bias
        /// first + the weight dot product in feature order -- byte-for-byte the resident score
        /// loop's per-entry math (<see cref="ScoreProjectionAndComputeFdrInPlace"/>). Leaves the
        /// standardized values in <paramref name="featureBuf"/> so the caller can bin them into
        /// the feature-contribution accumulator without recomputing.
        /// </summary>
        private static double ComputeStreamedScore(
            double[] avgWeights, double avgBias, FeatureStandardizer standardizer, double[] featureBuf,
            IReadOnlyList<double[]> rows, int parquetIndex, double coelutionSum, int nFeatures)
        {
            double[] featRow = ResolveFeatureRow(rows, (uint)parquetIndex, coelutionSum, nFeatures);
            Array.Copy(featRow, 0, featureBuf, 0, nFeatures);
            standardizer.TransformSlice(featureBuf);
            double score = avgBias;
            for (int j = 0; j < nFeatures; j++)
                score += avgWeights[j] * featureBuf[j];
            return score;
        }

        /// <summary>
        /// One file's raw parquet stub rows, buffered by the 1st-pass streaming score path
        /// (<see cref="RunStreamingFirstPass"/>) so the stream callback only appends to
        /// reference-type lists (never a captured-and-mutated counter) and the row/global ordinals
        /// advance in plain indexed loops. Bounded to one file at a time -- the same one-file
        /// resident set the per-file run-q competition already requires. The five parallel lists
        /// mirror the <see cref="FdrProjection"/> scalar slice the resident path holds.
        /// </summary>
        private sealed class RowBuffer
        {
            public readonly List<uint> EntryIds = new List<uint>();
            public readonly List<byte> Charges = new List<byte>();
            public readonly List<bool> IsDecoys = new List<bool>();
            public readonly List<double> CoelutionSums = new List<double>();
            public readonly List<string> Peptides = new List<string>();

            public int Count => EntryIds.Count;

            public void Clear()
            {
                EntryIds.Clear();
                Charges.Clear();
                IsDecoys.Clear();
                CoelutionSums.Clear();
                Peptides.Clear();
            }

            public void Add(uint entryId, byte charge, bool isDecoy, double coelutionSum, string peptide)
            {
                EntryIds.Add(entryId);
                Charges.Add(charge);
                IsDecoys.Add(isDecoy);
                CoelutionSums.Add(coelutionSum);
                // Normalize a null modseq to string.Empty exactly as the resident FdrProjectionSet
                // .Builder.AddRow does (a present-but-null modified_sequence element survives
                // ReadFdrStubScalars' column-level guard): a null peptide would otherwise throw as a
                // Dictionary<string,...> key in StreamingFdr.StreamingFirstPassQ / SubsampleByPeptideGroup and
                // would group differently from the resident path's ""-normalized peptides.
                Peptides.Add(peptide ?? string.Empty);
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
        /// Score one file's projection rows with the tree ensembles, in parallel over
        /// contiguous chunks (one standardization buffer per chunk, disjoint writes into
        /// <paramref name="finalScores"/>). Bit-identical to the serial loop: each row's
        /// score depends only on that row, and the tree path has no cross-row
        /// accumulation to order. Chunked rather than per-row so the work per
        /// <see cref="OspreyParallel"/> interlocked hand-out is a whole slice, not one row.
        /// </summary>
        private static void ScoreProjectionRowsGbt(
            IReadOnlyList<double[]> rows,
            List<FdrProjection> projRows,
            IReadOnlyList<GradientBoostedTrees> gbtModels,
            FeatureStandardizer standardizer,
            int nFeatures,
            double[] finalScores,
            int baseIndex,
            int nThreads)
        {
            int count = projRows.Count;
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
                    var proj = projRows[r];
                    double[] featRow = ResolveFeatureRow(
                        rows, proj.ParquetIndex, proj.CoelutionSum, nFeatures);
                    Array.Copy(featRow, 0, featureBuf, 0, nFeatures);
                    standardizer.TransformSlice(featureBuf);
                    finalScores[baseIndex + r] = AverageGbtScore(gbtModels, featureBuf);
                }
            });
        }
    }
}
