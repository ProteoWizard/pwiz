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
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Application of a trained Percolator model to a population, extracted from
    /// the original <c>PercolatorFdr</c> god class (issue #4468): full-population scoring, the projection-native
    /// in-place path, and the per-row primitives it shares with the streaming first pass
    /// (<see cref="PercolatorEngine.RunStreamingFirstPass"/>). That train-then-score driver lives
    /// in <see cref="PercolatorEngine"/> beside its siblings, so this class never calls the
    /// trainer and the two do not depend on each other in a cycle (issue #4494).
    ///
    /// Where training decides what the model IS, this decides what the model SAYS
    /// about every entry. The two meet at <see cref="ScoreWithFoldModel"/>, which
    /// training also calls to score its held-out folds - hence internal rather
    /// than private.
    ///
    /// These entry points still compute q-values as well as scores, as their names
    /// say (<see cref="ScoreProjectionAndComputeFdrInPlace"/>). That conflation is
    /// pre-existing; separating it is what the FDR extraction that follows does.
    /// </summary>
    public static class PercolatorScorer
    {
        /// <summary>
        /// Projection-native population scoring (issue #4355 step (b) increment iii): apply
        /// the trained averaged model to every projection row by streaming its file's parquet
        /// features, then run the competition + q-value math and write the Score + five
        /// q-values STRAIGHT BACK onto the <see cref="FdrProjection"/> rows, in the flat
        /// working arrays the parity-locked math already needs rather than a full-population
        /// <see cref="PercolatorEntry"/> list and <see cref="PercolatorResult"/> list. The
        /// per-entry scoring loop (bias first, then the averaged-weight dot product in feature
        /// order) and the q-value math
        /// (<see cref="StreamingFdr.ComputeStreamingCompetitionQvalues"/>) are the ones the
        /// removed <see cref="PercolatorEntry"/>-list scorer used.
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
            bool applyExperimentAgg = true,
            Dictionary<int, int> heldOutFoldByRow = null)
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

            // Average fold weights + biases;
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
            // The feature report: each file's share (sums in row order, histograms) added in file
            // order - the same reduction the streaming first pass makes, so the two report the
            // same contributions for the same rows (FeatureSums). Only when it has a reader: this
            // path persists no share, and under a tree model the report is a serial
            // standardization of every row, which the second pass would otherwise pay for nothing.
            bool reportContributions = captureContributions != null || OspreyOutput.Verbose;
            var contribAcc = new FeatureContributions.Accumulator(nFeatures, true);

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
                    var share = reportContributions ? new FeatureContributions.Accumulator(nFeatures, true) : null;
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
                            gbtModels, standardizer, nFeatures, finalScores, gi, config.NThreads,
                            heldOutFoldByRow);
                        // The per-feature report IS a cross-row accumulation, so it stays serial and
                        // in row order, after the parallel pass.
                        if (share != null)
                        {
                            foreach (var proj in projRows)
                            {
                                share.Add(StandardizeFeatureRow(standardizer, featureBuf, rows,
                                    proj.ParquetIndex, proj.CoelutionSum, nFeatures), proj.IsDecoy);
                            }
                        }
                        gi += projRows.Count;
                    }
                    else
                    {
                        // Linear path: serial. share.Add is a running float sum in row order.
                        for (int r = 0; r < projRows.Count; r++)
                        {
                            var proj = projRows[r];
                            double[] featRow = ResolveFeatureRow(
                                rows, proj.ParquetIndex, proj.CoelutionSum, nFeatures);
                            Array.Copy(featRow, 0, featureBuf, 0, nFeatures);
                            standardizer.TransformSlice(featureBuf);
                            finalScores[gi] = ScoreStandardizedRow(null, avgWeights, avgBias, featureBuf);

                            share?.Add(featureBuf, proj.IsDecoy);
                            gi++;
                        }
                    }
                    if (share != null)
                        contribAcc.Add(share.ToFeatureSums());
                    scoreProgress.Report(gi);
                }
                scoreProgress.Complete();
            }

            var contributions = reportContributions
                ? BuildContributions(contribAcc, gbtModels, trainResults, config)
                : null;
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
                floorProgress?.Complete();
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
        /// Resolve one row's raw features by its parquet row index and standardize them into
        /// <paramref name="featureBuf"/>, which is returned. The first half of
        /// <c>PercolatorEngine.ComputeStreamedScore</c>, and on its own what the tree paths'
        /// feature-distribution report bins: the same copy and transform
        /// <see cref="ScoreRowsGbt"/> applies before it scores, so the report sees the values the
        /// trees did.
        /// </summary>
        internal static double[] StandardizeFeatureRow(FeatureStandardizer standardizer, double[] featureBuf,
            IReadOnlyList<double[]> rows, uint parquetIndex, double coelutionSum, int nFeatures)
        {
            double[] featRow = ResolveFeatureRow(rows, parquetIndex, coelutionSum, nFeatures);
            Array.Copy(featRow, 0, featureBuf, 0, nFeatures);
            standardizer.TransformSlice(featureBuf);
            return featureBuf;
        }

        /// <summary>
        /// The feature-contribution report a score pass hands on. For the linear model it is the
        /// full decomposition, printed as it always was. A tree ensemble has no weights to
        /// decompose, so its report carries only the per-feature target/decoy distributions
        /// (<see cref="FeatureContributions.Accumulator.BuildForTreeEnsemble"/>).
        /// </summary>
        internal static FeatureContributions BuildContributions(FeatureContributions.Accumulator contribAcc,
            IReadOnlyList<GradientBoostedTrees> gbtModels, PercolatorResults model,
            PercolatorConfig config)
        {
            if (gbtModels != null)
                return contribAcc.BuildForTreeEnsemble(config.FeatureInfos, model.TrainingAnalysis);
            var contributions = contribAcc.Build(model.FoldWeights, config.FeatureInfos, model.TrainingAnalysis);
            PercolatorDiagnosticsDump.EmitFeatureContributions(contributions);
            return contributions;
        }

        /// <summary>
        /// The first-pass feature report <see cref="PercolatorEngine.RunStreamingFirstPass"/> builds, from the model
        /// it scored with and every run's <see cref="FeatureSums"/> share in run order - for a
        /// report written by a later process. Null when there is no run or a run has no share.
        /// </summary>
        internal static FeatureContributions ContributionsFromShares(PercolatorResults model,
            IEnumerable<FeatureSums> shares, OspreyFeatureInfo[] featureInfos)
        {
            FeatureContributions.Accumulator contribAcc = null;
            foreach (var share in shares)
            {
                if (share == null)
                    return null;
                contribAcc = contribAcc ?? new FeatureContributions.Accumulator(share.FeatureCount, true);
                contribAcc.Add(share);
            }
            if (contribAcc == null)
                return null;
            return BuildContributions(contribAcc, ResolveGbtModels(model), model,
                new PercolatorConfig { FeatureInfos = featureInfos });
        }

        /// <summary>
        /// Resolve one entry's 21-feature vector from a file's freshly loaded
        /// parquet rows by <see cref="PercolatorEntry.ParquetIndex"/>. Falls back
        /// to the basic feature vector (built from the resident coelution_sum) when
        /// the index is out of range - the fallback for entries without a loadable row
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
            return BuildBasicFeatures(coelutionSum, numFeatures);
        }

        /// <summary>
        /// Build a minimal PIN feature vector from an entry's coelution_sum.
        /// Used as a fallback ONLY when the full 21-feature vector is unavailable
        /// (e.g. stubs loaded from a Parquet cache without features, or a streaming
        /// entry whose parquet row cannot be resolved). In normal operation the
        /// 21-feature vector is computed during coelution scoring in
        /// <c>CoelutionScorer</c> and stored on the entry; its only caller is
        /// <see cref="ResolveFeatureRow"/>.
        /// </summary>
        private static double[] BuildBasicFeatures(double coelutionSum, int numFeatures)
        {
            double[] features = new double[numFeatures];

            // 0: coelution_sum
            features[0] = coelutionSum;
            // 1: coelution_max (approximate as coelution_sum for basic version)
            features[1] = coelutionSum * 0.5;
            // 2: n_coeluting_fragments
            features[2] = 3.0;
            // 3: peak_apex
            features[3] = 0.0;
            // 4: peak_area
            features[4] = 0.0;
            // 5: peak_sharpness
            features[5] = 0.0;
            // 6: xcorr
            features[6] = 0.0;
            // 7: consecutive_ions
            features[7] = 0.0;
            // 8: explained_intensity
            features[8] = 0.0;
            // 9: mass_accuracy_mean
            features[9] = 0.0;
            // 10: abs_mass_accuracy_mean
            features[10] = 0.0;
            // 11: rt_deviation
            features[11] = 0.0;
            // 12: abs_rt_deviation
            features[12] = 0.0;
            // 13: ms1_precursor_coelution
            features[13] = 0.0;
            // 14: ms1_isotope_cosine
            features[14] = 0.0;
            // 15: median_polish_cosine
            features[15] = 0.0;
            // 16: median_polish_residual_ratio
            features[16] = 0.0;
            // 17: sg_weighted_xcorr
            features[17] = 0.0;
            // 18: sg_weighted_cosine
            features[18] = 0.0;
            // 19: median_polish_min_fragment_r2
            features[19] = 0.0;
            // 20: median_polish_residual_correlation
            features[20] = 0.0;

            return features;
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
        /// product. Keeping both here is what lets the projection
        /// (<see cref="ScoreProjectionAndComputeFdrInPlace"/>) and streamed first-pass score
        /// passes stay a single shared implementation. Internal rather than
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
        internal static double AverageGbtScore(
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
        internal static List<GradientBoostedTrees> ResolveGbtModels(PercolatorResults trainResults)
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
            int nThreads,
            IReadOnlyDictionary<int, int> heldOutFoldByRow = null)
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
                    // A training row is scored by the one fold model that held it out
                    // (OSPREY_GBT_OOF); the others trained on it.
                    int fold;
                    scores[baseIndex + r] = heldOutFoldByRow != null && heldOutFoldByRow.TryGetValue(baseIndex + r, out fold)
                        ? gbtModels[fold].ScoreSingle(featureBuf)
                        : AverageGbtScore(gbtModels, featureBuf);
                }
            });
        }
    }
}
