/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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

// Native Percolator implementation for semi-supervised FDR control
//
// Implements the Percolator algorithm (Kall et al. 2007) as refined by
// mokapot (Fondrie & Noble, 2021):
// - 3-fold cross-validation with peptide-grouped fold assignment
// - Iterative linear SVM training on high-confidence targets vs all decoys
// - Grid search for SVM cost parameter C
// - Per-run and experiment-level FDR with conservative (n_decoy+1)/n_target formula
// - Posterior error probability via KDE + isotonic regression
//
// Port of osprey-fdr/src/percolator.rs.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Performs false discovery rate estimation using the Percolator algorithm.
    /// Port of osprey-fdr/src/percolator.rs.
    /// </summary>
    public static class PercolatorFdr
    {
        // internal so the extracted PercolatorDiagnosticsDump can mask base IDs
        // the same way the pipeline does.
        internal static readonly uint BASE_ID_MASK = 0x7FFFFFFF;
        private static readonly int MIN_POSITIVE = 50;

        /// <summary>
        /// Run the Percolator algorithm on a collection of entries.
        /// </summary>
        public static PercolatorResults RunPercolator(
            IList<PercolatorEntry> entries,
            PercolatorConfig config)
        {
            if (entries.Count == 0)
                return new PercolatorResults();

            int n = entries.Count;
            int nFeatures = entries[0].Features.Length;

            var swSetup = Stopwatch.StartNew();

            // 1. Build feature matrix
            var featureData = new double[n * nFeatures];
            var labels = new bool[n];
            var entryIds = new uint[n];
            var peptides = new string[n];
            for (int i = 0; i < n; i++)
            {
                Array.Copy(entries[i].Features, 0, featureData, i * nFeatures, nFeatures);
                labels[i] = entries[i].IsDecoy;
                entryIds[i] = entries[i].EntryId;
                peptides[i] = entries[i].Peptide;
            }
            var features = Matrix.WrapNoClone(featureData, n, nFeatures);

            // 2. Standardize features
            Matrix stdFeatures;
            var standardizer = FeatureStandardizer.FitTransform(features, out stdFeatures);
            swSetup.Stop();
            OspreyOutput.Out.WriteLine(
                $"[TIMING]   Percolator setup + standardize: {swSetup.Elapsed.TotalSeconds:F1}s ({n} entries x {nFeatures} features)");

            // Stage 5 standardizer dump. Gated by the injected diagnostics config
            // (OSPREY_DUMP_STANDARDIZER); a *Only request returns the abort
            // sentinel so the Tasks-layer caller -- not this engine -- decides the
            // early-exit. Mirrors Rust dump_stage5_standardizer in
            // osprey-fdr/src/percolator.rs.
            if (config.Diagnostics != null && config.Diagnostics.DumpStandardizer)
            {
                PercolatorDiagnosticsDump.WriteStandardizerDump(standardizer, config.FeatureInfos);
                if (config.Diagnostics.StandardizerOnly)
                    return new PercolatorResults { DiagnosticAbort = true };
            }

            // One-shot diagnostic for 2nd-pass divergence localization.
            // Gated by OSPREY_DUMP_PERC_INPUT; a *Only request returns the abort
            // sentinel. Dumps the raw per-entry feature vectors fed into the
            // standardizer so cross-impl compare can pinpoint which rows differ.
            if (config.Diagnostics != null && config.Diagnostics.DumpPercInput)
            {
                PercolatorDiagnosticsDump.WritePercInputDump(entries, config.FeatureInfos);
                if (config.Diagnostics.PercInputOnly)
                    return new PercolatorResults { DiagnosticAbort = true };
            }

            // 3a. Best-per-precursor: pick the single best-scoring observation per
            //     (base_id, isDecoy) tuple across all files. With N files per peptide,
            //     this avoids the SVM seeing the same precursor's target/decoy pair
            //     N times, which would inflate apparent target/decoy separation and
            //     cause the SVM to learn file-specific noise rather than peptide
            //     discriminating features. Mirrors the streaming Percolator path's
            //     dedup step (RunPercolatorStreaming); the Rust direct path
            //     historically omitted this step, but on multi-file inputs sized
            //     below the streaming threshold (Stellar 3-file at 393k entries)
            //     the omission produced a statistically incorrect training set
            //     that treated multi-file repeats of the same precursor as
            //     independent samples. Rust was patched to match this dedup
            //     (osprey-fdr/src/percolator.rs::run_percolator direct path).
            // 3b. ...then, if still > MaxTrainSize, subsample by peptide groups
            //     (so target/decoy pairs and same-peptide multi-charge stay
            //     together). Both selection steps live in BuildTrainingSubset so
            //     this direct path and the streaming path cannot drift.
            int[] bestPerPrecursor;
            int[] trainSubset = PercolatorSampling.BuildTrainingSubset(
                labels, entryIds, peptides, entries, config.MaxTrainSize, config.Seed,
                out bestPerPrecursor);

            int dedupTargets = 0, dedupDecoys = 0;
            for (int i = 0; i < bestPerPrecursor.Length; i++)
            {
                if (labels[bestPerPrecursor[i]])
                    dedupDecoys++;
                else dedupTargets++;
            }
            OspreyOutput.Out.WriteLine("[COUNT]   Percolator best-per-precursor: {0} entries ({1} targets, {2} decoys) from {3} total",
                bestPerPrecursor.Length, dedupTargets, dedupDecoys, n);

            int subN = trainSubset.Length;
            int subTargets = 0, subDecoys = 0;
            for (int i = 0; i < trainSubset.Length; i++)
            {
                if (labels[trainSubset[i]])
                    subDecoys++;
                else subTargets++;
            }
            OspreyOutput.Out.WriteLine("[COUNT]   Percolator subsample: {0} entries ({1} targets, {2} decoys) from {3} dedup",
                subN, subTargets, subDecoys, bestPerPrecursor.Length);

            // Build subset-local arrays
            bool[] subLabels;
            uint[] subEntryIds;
            string[] subPeptides;
            Matrix subFeatures;
            if (trainSubset != null)
            {
                subLabels = new bool[subN];
                subEntryIds = new uint[subN];
                subPeptides = new string[subN];
                for (int i = 0; i < subN; i++)
                {
                    subLabels[i] = labels[trainSubset[i]];
                    subEntryIds[i] = entryIds[trainSubset[i]];
                    subPeptides[i] = peptides[trainSubset[i]];
                }
                subFeatures = MatrixRows.ExtractRows(stdFeatures, trainSubset);
            }
            else
            {
                subLabels = (bool[])labels.Clone();
                subEntryIds = (uint[])entryIds.Clone();
                subPeptides = (string[])peptides.Clone();
                subFeatures = stdFeatures;
            }

            // 4. Assign folds on the (possibly subsampled) set
            int[] foldAssignments = PercolatorSampling.CreateStratifiedFoldsByPeptide(
                subLabels, subPeptides, subEntryIds, config.NFolds);

            // Stage 5 sub-stage diagnostic dump. Gated by OSPREY_DUMP_SUBSAMPLE;
            // a *Only request returns the abort sentinel. Captures subsample
            // membership and fold assignment per entry, mirroring the Rust dump in
            // osprey-fdr/src/percolator.rs. The dump writer is inlined here (not
            // routed through OspreyDiagnostics) because Osprey.FDR does not
            // reference the main Osprey assembly; only the gate flag + the
            // early-exit decision are lifted out to the Tasks-layer caller.
            if (config.Diagnostics != null && config.Diagnostics.DumpSubsample)
            {
                PercolatorDiagnosticsDump.WriteSubsampleDump(entries, trainSubset, foldAssignments);
                if (config.Diagnostics.SubsampleOnly)
                    return new PercolatorResults { DiagnosticAbort = true };
            }

            // 5. Find best initial feature
            double trainFdr = config.TrainFdr;
            int bestFeatIdx;
            int bestFeatPassing;
            FindBestInitialFeature(subFeatures, subLabels, subEntryIds, trainFdr,
                out bestFeatIdx, out bestFeatPassing);

            if (bestFeatPassing == 0)
            {
                double relaxedFdr = 0.05;
                FindBestInitialFeature(subFeatures, subLabels, subEntryIds, relaxedFdr,
                    out bestFeatIdx, out bestFeatPassing);
                if (bestFeatPassing > 0)
                    trainFdr = relaxedFdr;
            }

            string bestFeatName = (config.FeatureInfos != null &&
                                   bestFeatIdx >= 0 &&
                                   bestFeatIdx < config.FeatureInfos.Length)
                ? config.FeatureInfos[bestFeatIdx].Name
                : string.Format("feature_{0}", bestFeatIdx);
            OspreyOutput.Out.WriteLine(
                "[COUNT] Best initial feature: {0} ({1} targets at {2:F0}% FDR)",
                bestFeatName, bestFeatPassing, trainFdr * 100.0);

            var initialScores = new double[subN];
            for (int i = 0; i < subN; i++)
                initialScores[i] = subFeatures[i, bestFeatIdx];

            // 6. Train per-fold models via cross-validation
            var finalScores = new double[n];
            var foldWeights = new List<double[]>();
            var foldBiases = new List<double>();
            var iterationsPerFold = new List<int>();

            var foldModels = new LinearSvmClassifier[config.NFolds];
            // Populated instead of foldModels when config.UseGradientBoostedTrees
            // (--fdr-method gbdt). Exactly one of the two is non-null.
            var foldGbtModels = config.UseGradientBoostedTrees
                ? new GradientBoostedTrees[config.NFolds]
                : null;
            var foldIterations = new int[config.NFolds];
            var foldElapsed = new double[config.NFolds];
            // Selected SVM cost C per fold (chosen by inner CV over config.CValues,
            // the log-scale sweep grid). Reported on the default console after
            // training so the coefficients above have context (issue #4364).
            var foldBestC = new double[config.NFolds];

            // Pre-compute training indices for each fold (cheap, single-threaded).
            var foldTrainIndices = new int[config.NFolds][];
            for (int fold = 0; fold < config.NFolds; fold++)
            {
                var list = new List<int>(subN - subN / config.NFolds);
                for (int i = 0; i < subN; i++)
                {
                    if (foldAssignments[i] != fold)
                        list.Add(i);
                }
                foldTrainIndices[fold] = list.ToArray();
            }

            // One scratch pool for the whole outer-fold Parallel.For and
            // every nested GridSearchC.Parallel.For below. Initial size
            // = the subsampled training set; grid_search inner SVMs may
            // need a different (typically smaller) capacity, handled by
            // SvmTrainScratch.EnsureCapacity on rent. Pool grows
            // organically to the parallel-worker high-water mark and
            // arrays stay in gen-2 LOH for the rest of the run.
            var svmScratchPool = new SvmTrainScratchPool(subN, nFeatures);

            // Train all folds in parallel. Each fold reads from the shared
            // subFeatures matrix (read-only) and produces an independent model.
            // Mirrors Rust's into_par_iter(). Use OspreyParallel.For (explicit
            // dedicated threads) rather than TPL Parallel.For: the TPL
            // TaskReplicator was throttling effective parallelism to ~2.5x
            // on HRAM Astral (vs Rust rayon's ~9x) even with the same
            // per-call cost. Explicit threads remove the ThreadPool
            // scheduling variable.
            // Section sub-header (default human log): the actual (possibly subsampled)
            // training-set size the per-iteration percent lines below are computed against.
            // subN / subTargets are the post-subsample counts computed above.
            OspreyOutput.Out.WriteLine("  {0}-fold cross-validation on {1} training entries ({2} targets)",
                config.NFolds, subN, subTargets);

            var swTrain = Stopwatch.StartNew();
            var trainProgress = new TrainProgressReporter(config.NFolds, config.MaxIterations, trainFdr);
            OspreyParallel.For(0, config.NFolds, config.NFolds, fold =>
            {
                var swFold = Stopwatch.StartNew();
                int iters;
                // Non-null exactly when config.UseGradientBoostedTrees; testing the array
                // rather than the flag is the same condition by construction and keeps the
                // element write provably non-null.
                if (foldGbtModels != null)
                {
                    foldGbtModels[fold] = TrainFoldGbt(
                        subFeatures, subLabels, subEntryIds, subPeptides,
                        foldTrainIndices[fold], initialScores, config, trainFdr,
                        fold, trainProgress, out iters);
                    // No C to sweep: the trees are regularized by depth / gamma /
                    // lambda / min-child-weight, all fixed by GbtParams.
                    foldBestC[fold] = double.NaN;
                }
                else
                {
                    double foldC;
                    foldModels[fold] = TrainFold(
                        subFeatures, subLabels, subEntryIds, subPeptides,
                        foldTrainIndices[fold], initialScores, config, trainFdr,
                        svmScratchPool, fold, trainProgress, out iters, out foldC);
                    foldBestC[fold] = foldC;
                }
                foldIterations[fold] = iters;
                swFold.Stop();
                foldElapsed[fold] = swFold.Elapsed.TotalSeconds;
            });
            swTrain.Stop();

            for (int fold = 0; fold < config.NFolds; fold++)
            {
                OspreyOutput.Out.WriteLine("[TIMING]   Percolator fold {0}/{1}: {2:F1}s ({3} iterations)",
                    fold + 1, config.NFolds, foldElapsed[fold], foldIterations[fold]);
            }
            OspreyOutput.Out.WriteLine("[TIMING]   Percolator train all folds (parallel): {0:F1}s",
                swTrain.Elapsed.TotalSeconds);

            if (config.UseGradientBoostedTrees)
            {
                // The tree counterpart of the C report below: the knobs that actually
                // bound this model's capacity, so the fold scores above have context.
                var gp = config.GbtParams;
                OspreyOutput.Out.WriteLine(
                    "  Gradient-boosted trees: {0} trees, max depth {1}, learning rate {2}, " +
                    "subsample {3}, colsample {4}, lambda {5}, alpha {6}, gamma {7}, min child weight {8}",
                    gp.NTrees, gp.MaxDepth, gp.LearningRate, gp.Subsample, gp.ColSample,
                    gp.RegLambda, gp.RegAlpha, gp.Gamma, gp.MinChildWeight);
            }
            else
            {
                // Selected SVM regularization C per fold, on the default console (issue
                // #4364): C controls the SVM margin, so the trained coefficients above are
                // only interpretable with it. C is chosen per fold by inner cross-validation
                // from a log-scale sweep grid; report the grid and each fold's pick.
                OspreyOutput.Out.WriteLine("  SVM regularization C (swept over {0}, chosen by cross-validation per fold):",
                    FormatCGrid(config.CValues));
                for (int fold = 0; fold < config.NFolds; fold++)
                {
                    OspreyOutput.Out.WriteLine("    fold {0}/{1}: C = {2}",
                        fold + 1, config.NFolds, FormatC(foldBestC[fold]));
                }
            }

            // Stage 5 SVM-internals dump. Gated by OSPREY_DUMP_SVM_WEIGHTS;
            // a *Only request returns the abort sentinel. Captures per-fold
            // weights, bias, and iteration count right after SVM training
            // converges and before Granholm calibration. Mirrors rust side in
            // osprey-fdr/src/percolator.rs::dump_stage5_svm_weights.
            // Skipped on the gradient-boosted-trees path: the dump's whole content is
            // per-fold linear weights + bias, which a tree ensemble does not have. It is
            // a cross-impl parity dump for the SVM (which gbdt has no Rust
            // counterpart for), so there is nothing to emit rather than something to port.
            if (config.Diagnostics != null && config.Diagnostics.DumpSvmWeights &&
                !config.UseGradientBoostedTrees)
            {
                PercolatorDiagnosticsDump.WriteSvmWeightsDump(foldModels, foldIterations, config.FeatureInfos);
                if (config.Diagnostics.SvmWeightsOnly)
                    return new PercolatorResults { DiagnosticAbort = true };
            }

            // Train-only mode: return fold models + standardizer; skip the
            // CV/averaged scoring of the input entries, PEP, and q-values.
            // Used by the streaming path where the caller (AnalysisPipeline)
            // will apply the averaged model to ALL FdrEntry values and
            // compute q-values on the full, scored population. Mirrors
            // Rust's `config.train_only` short-circuit in
            // osprey-fdr/src/percolator.rs.
            if (config.TrainOnly)
            {
                var trainFoldWeights = new List<double[]>(config.NFolds);
                var trainFoldBiases = new List<double>(config.NFolds);
                var trainIterations = new List<int>(config.NFolds);
                for (int fold = 0; fold < config.NFolds; fold++)
                {
                    if (foldGbtModels == null)
                    {
                        trainFoldWeights.Add(foldModels[fold].Weights);
                        trainFoldBiases.Add(foldModels[fold].Bias);
                    }
                    trainIterations.Add(foldIterations[fold]);
                }
                return new PercolatorResults
                {
                    Entries = new List<PercolatorResult>(),
                    FoldWeights = trainFoldWeights,
                    FoldBiases = trainFoldBiases,
                    FoldGbtModels = foldGbtModels != null
                        ? new List<GradientBoostedTrees>(foldGbtModels)
                        : null,
                    Standardizer = standardizer,
                    IterationsPerFold = trainIterations
                };
            }

            // Score ALL entries with trained models
            if (trainSubset != null)
            {
                var inSubset = new HashSet<int>();
                foreach (int idx in trainSubset)
                    inSubset.Add(idx);

                // For subset entries: score with held-out fold model
                for (int fold = 0; fold < config.NFolds; fold++)
                {
                    var testSubIndices = new List<int>();
                    for (int i = 0; i < subN; i++)
                    {
                        if (foldAssignments[i] == fold)
                            testSubIndices.Add(i);
                    }
                    var testGlobalIndices = new int[testSubIndices.Count];
                    for (int i = 0; i < testSubIndices.Count; i++)
                        testGlobalIndices[i] = trainSubset[testSubIndices[i]];
                    var testFeatures = MatrixRows.ExtractRows(stdFeatures, testGlobalIndices);
                    var testScores = PercolatorScorer.ScoreWithFoldModel(foldModels, foldGbtModels, fold, testFeatures);
                    for (int i = 0; i < testGlobalIndices.Length; i++)
                        finalScores[testGlobalIndices[i]] = testScores[i];
                }

                // For non-subset entries: average scores from all fold models
                var nonSubsetIndices = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    if (!inSubset.Contains(i))
                        nonSubsetIndices.Add(i);
                }
                if (nonSubsetIndices.Count > 0)
                {
                    var nonSubFeatures = MatrixRows.ExtractRows(stdFeatures, nonSubsetIndices.ToArray());
                    double nModels = config.NFolds;
                    var avgScores = new double[nonSubsetIndices.Count];
                    for (int fold = 0; fold < config.NFolds; fold++)
                    {
                        var modelScores = PercolatorScorer.ScoreWithFoldModel(foldModels, foldGbtModels, fold, nonSubFeatures);
                        for (int i = 0; i < avgScores.Length; i++)
                            avgScores[i] += modelScores[i];
                    }
                    for (int i = 0; i < nonSubsetIndices.Count; i++)
                        finalScores[nonSubsetIndices[i]] = avgScores[i] / nModels;
                }
            }
            else
            {
                // No subsampling: score test fold directly
                for (int fold = 0; fold < config.NFolds; fold++)
                {
                    var testIndices = new List<int>();
                    for (int i = 0; i < n; i++)
                    {
                        if (foldAssignments[i] == fold)
                            testIndices.Add(i);
                    }
                    var testFeatures = MatrixRows.ExtractRows(stdFeatures, testIndices.ToArray());
                    var testScores = PercolatorScorer.ScoreWithFoldModel(foldModels, foldGbtModels, fold, testFeatures);
                    for (int i = 0; i < testIndices.Count; i++)
                        finalScores[testIndices[i]] = testScores[i];
                }
            }

            for (int fold = 0; fold < config.NFolds; fold++)
            {
                if (foldGbtModels == null)
                {
                    foldWeights.Add(foldModels[fold].Weights);
                    foldBiases.Add(foldModels[fold].Bias);
                }
                iterationsPerFold.Add(foldIterations[fold]);
            }

            // 6b. Calibrate scores between folds
            if (trainSubset != null)
            {
                var globalFoldAssignments = new int[n];
                for (int i = 0; i < n; i++)
                    globalFoldAssignments[i] = int.MaxValue;
                for (int si = 0; si < trainSubset.Length; si++)
                    globalFoldAssignments[trainSubset[si]] = foldAssignments[si];
                CalibrateScoresBetweenFolds(finalScores, globalFoldAssignments,
                    labels, entryIds, config.NFolds, trainFdr);
            }
            else
            {
                CalibrateScoresBetweenFolds(finalScores, foldAssignments,
                    labels, entryIds, config.NFolds, trainFdr);
            }

            // 7. Compute PEP on competition winners
            int[] winnerIndices;
            double[] winnerScores;
            bool[] winnerIsDecoy;
            CompeteAll(finalScores, labels, entryIds,
                out winnerIndices, out winnerScores, out winnerIsDecoy);

            var pepEstimator = PepEstimator.FitDefault(winnerScores, winnerIsDecoy);
            var peps = new double[n];
            for (int i = 0; i < n; i++)
                peps[i] = 1.0;
            foreach (int idx in winnerIndices)
                peps[idx] = pepEstimator.PosteriorError(finalScores[idx]);

            // 8. Compute q-values at precursor and peptide levels
            var fileNames = new string[n];
            for (int i = 0; i < n; i++)
                fileNames[i] = entries[i].FileName;

            var uniqueFiles = new HashSet<string>(fileNames);
            bool isSingleFile = uniqueFiles.Count <= 1;

            var runPrecursorQvalues = ComputePerRunPrecursorQvalues(
                finalScores, labels, entryIds, fileNames);
            var runPeptideQvalues = ComputePerRunPeptideQvalues(
                finalScores, labels, entryIds, fileNames, peptides);

            double[] expPrecursorQvalues;
            double[] expPeptideQvalues;
            if (isSingleFile)
            {
                expPrecursorQvalues = (double[])runPrecursorQvalues.Clone();
                expPeptideQvalues = (double[])runPeptideQvalues.Clone();
            }
            else
            {
                expPrecursorQvalues = ComputeExperimentPrecursorQvalues(
                    finalScores, labels, entryIds);
                expPeptideQvalues = ComputeExperimentPeptideQvalues(
                    finalScores, labels, entryIds, peptides);
            }

            // Best-of-runs monotonicity (issue #4390 clamp, memory-bounded flat form): floor
            // each experiment q up to the entry's best (min-over-runs) combined run q, so an
            // experiment-level q is never more confident than the entry's best single run.
            // Identical floors to PercolatorEngine.ClampExperimentQToBestRun, over the flat
            // score-pass arrays (no resident FdrEntry buffer). Covers the direct dispatch.
            ClampExperimentQToBestRunFlat(
                entryIds, labels, peptides, runPrecursorQvalues, runPeptideQvalues,
                expPrecursorQvalues, expPeptideQvalues);

            // 8b. Feature weight + percent-contribution report (reporting only).
            // The Accumulator sums per-feature target/decoy means over the FULL
            // standardized matrix and averages the per-fold weights into the
            // model it decomposes -- a pure read of stdFeatures + foldWeights that
            // never perturbs finalScores / q-values (serial in row/index order).
            // The table characterizes the AVERAGED model -- the same object the
            // production streaming path (ScorePopulationAndComputeFdr) actually
            // scores with -- not the per-entry CV ensemble that produced
            // finalScores on this test / small-input standalone path.
            // Null on the gradient-boosted-trees path: the report decomposes a score
            // into per-feature weight x mean-difference terms, which only exists for a
            // linear model. A tree ensemble's analogue is split-gain importance -- a
            // different quantity that would need its own report rather than a
            // reinterpretation of this one. Callers already tolerate null (the Simple
            // and transfer 2nd-pass paths return it), so the --model-diagnostics Model
            // panel renders "n/a" exactly as it does for transfer-compete.
            FeatureContributions contributions = null;
            if (!config.UseGradientBoostedTrees)
            {
                var contribAcc = new FeatureContributions.Accumulator(nFeatures, config.CollectFeatureHistograms);
                for (int i = 0; i < n; i++)
                    contribAcc.Add(stdFeatures, i, labels[i]);   // labels[i] == IsDecoy
                contributions = contribAcc.Build(foldWeights, config.FeatureInfos);
                PercolatorDiagnosticsDump.EmitFeatureContributions(contributions);
            }

            // 9. Build results
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
                    Pep = peps[i]
                });
            }

            return new PercolatorResults
            {
                Entries = results,
                FoldWeights = foldWeights,
                FoldBiases = foldBiases,
                FoldGbtModels = foldGbtModels != null
                    ? new List<GradientBoostedTrees>(foldGbtModels)
                    : null,
                Standardizer = standardizer,
                IterationsPerFold = iterationsPerFold,
                FeatureContributions = contributions
            };
        }

        /// <summary>
        /// The streaming competition + PEP + per-run / experiment q-value math, over
        /// the flat per-observation arrays that both the <see cref="PercolatorEntry"/>
        /// score pass (<see cref="PercolatorScorer.ScorePopulationAndComputeFdr"/>) and the
        /// projection-native score pass
        /// (<see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>) produce. Extracted as a
        /// single source of truth (issue #4355 step (b) increment iii) so the two
        /// buffer shapes cannot drift on the byte-parity-locked ordering. UNCHANGED
        /// math relative to the pre-extraction inline block:
        /// <list type="bullet">
        /// <item>PEP is fed to <see cref="PepEstimator.FitDefault"/> in
        /// <c>base_id</c>-ascending order (risk #6): the KDE sum is non-associative,
        /// so the winner arrays are reordered by <c>entryIds &amp; BASE_ID_MASK</c>
        /// before the fit; the score-sorted arrays stay intact for the q-value
        /// calls.</item>
        /// <item>Per-run q-values group by <paramref name="fileNames"/>; experiment
        /// q-values take the single-file shortcut (clone the per-run arrays) exactly
        /// as the direct path does.</item>
        /// </list>
        /// The five outputs are returned as parallel arrays (index-aligned to the
        /// inputs); the caller either packs them into <see cref="PercolatorResult"/>s
        /// or writes them straight onto the projection rows.
        /// </summary>
        internal static void ComputeStreamingCompetitionQvalues(
            double[] finalScores, bool[] labels, uint[] entryIds,
            string[] peptides, string[] fileNames,
            out double[] peps, out double[] runPrecursorQvalues,
            out double[] runPeptideQvalues, out double[] expPrecursorQvalues,
            out double[] expPeptideQvalues)
        {
            int n = finalScores.Length;

            // PEP via global target-decoy competition. The bounded winner->PEP map
            // (base_id-ascending KDE order -- see ComputePepWinnerMap) is expanded to the
            // full per-row peps array here; the projection score pass reads the map directly
            // so the O(n) array is never materialized (issue #4355 Part B).
            var pepByWinnerIdx = ComputePepWinnerMap(finalScores, labels, entryIds);
            peps = new double[n];
            for (int i = 0; i < n; i++)
                peps[i] = 1.0;
            foreach (var kv in pepByWinnerIdx)
                peps[kv.Key] = kv.Value;

            // Per-run precursor + peptide q-values (each file independently).
            runPrecursorQvalues = ComputePerRunPrecursorQvalues(
                finalScores, labels, entryIds, fileNames);
            runPeptideQvalues = ComputePerRunPeptideQvalues(
                finalScores, labels, entryIds, fileNames, peptides);

            // Experiment-level q-values: single-file shortcut matches
            // direct-path semantics.
            var uniqueFiles = new HashSet<string>(fileNames);
            bool isSingleFile = uniqueFiles.Count <= 1;
            if (isSingleFile)
            {
                expPrecursorQvalues = (double[])runPrecursorQvalues.Clone();
                expPeptideQvalues = (double[])runPeptideQvalues.Clone();
            }
            else
            {
                expPrecursorQvalues = ComputeExperimentPrecursorQvalues(
                    finalScores, labels, entryIds);
                expPeptideQvalues = ComputeExperimentPeptideQvalues(
                    finalScores, labels, entryIds, peptides);
            }

            // Best-of-runs monotonicity (issue #4390 clamp, memory-bounded flat form): floor
            // each experiment q up to the entry's best (min-over-runs) combined run q. Shared by
            // the FdrEntry streaming path and the projection score pass, so both clamp
            // identically without a resident FdrEntry buffer.
            ClampExperimentQToBestRunFlat(
                entryIds, labels, peptides, runPrecursorQvalues, runPeptideQvalues,
                expPrecursorQvalues, expPeptideQvalues);
        }

        /// <summary>
        /// Bounded (O(base_ids)) posterior-error-probability (PEP) map: the global
        /// target-decoy competition winner index -&gt; its PEP. This is the intrinsic working
        /// set of the PEP step -- one PEP per competition winner (every other row's PEP is the
        /// default 1.0) -- so the projection score pass
        /// (<see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>) reads the map directly to set
        /// the winning rows' PEP without materializing the O(n) per-row array (issue #4355
        /// Part B). <see cref="ComputeStreamingCompetitionQvalues"/> expands the same map, so
        /// both share the one PEP fit and cannot drift.
        ///
        /// The KDE is fed in base_id-ascending order (risk #6): CompeteAll returns winners
        /// score-descending, but PepEstimator.FitDefault's KDE sum is NOT associative, so for
        /// cross-impl byte parity the winners must be reordered to the same base_id-sorted
        /// order Rust's compute_fdr_from_stubs uses before the fit.
        /// </summary>
        internal static Dictionary<int, double> ComputePepWinnerMap(
            double[] finalScores, bool[] labels, uint[] entryIds)
        {
            int n = finalScores.Length;
            int[] winnerIndices;
            double[] winnerScores;
            bool[] winnerIsDecoy;
            // Throttled progress over the ~344M-row population competition (the big walk that
            // ran silent at 82 files); null (silent) on small runs. Console-only, byte-neutral.
            using (var pepProgress = QProgress(@"Population target/decoy competition", n, n))
                CompeteAll(finalScores, labels, entryIds,
                    out winnerIndices, out winnerScores, out winnerIsDecoy, pepProgress);

            int nWinners = winnerIndices.Length;
            var pepOrder = new int[nWinners];
            for (int k = 0; k < nWinners; k++)
                pepOrder[k] = k;
            Array.Sort(pepOrder, (a, b) => // Array.Sort OK: TDC's CompeteAll already produced one winner per base_id, so each base_id appears at most once in pepOrder -- no ties.
            {
                uint ba = entryIds[winnerIndices[a]] & BASE_ID_MASK;
                uint bb = entryIds[winnerIndices[b]] & BASE_ID_MASK;
                return ba.CompareTo(bb);
            });
            var pepScores = new double[nWinners];
            var pepIsDecoy = new bool[nWinners];
            for (int k = 0; k < nWinners; k++)
            {
                pepScores[k] = winnerScores[pepOrder[k]];
                pepIsDecoy[k] = winnerIsDecoy[pepOrder[k]];
            }

            var pepEstimator = PepEstimator.FitDefault(pepScores, pepIsDecoy);
            var pepByWinnerIdx = new Dictionary<int, double>(nWinners);
            foreach (int idx in winnerIndices)
                pepByWinnerIdx[idx] = pepEstimator.PosteriorError(finalScores[idx]);
            return pepByWinnerIdx;
        }

        /// <summary>
        /// Memory-bounded flat form of <see cref="PercolatorEngine.ClampExperimentQToBestRun"/>
        /// (issue #4378): floor each experiment q up to the entry's best (min-over-runs)
        /// combined run q (<c>runBoth = max(runPrecursorQ, runPeptideQ)</c>), keyed by EntryId
        /// for the precursor floor and by <c>(peptide, isDecoy)</c> for the peptide floor (an
        /// empty peptide is skipped). Operates on the flat score-pass scalar arrays the FDR math
        /// already holds -- no resident FdrEntry buffer -- so the streaming path clamps without
        /// materializing every entry. <c>min</c>/<c>max</c> are order-independent, so the result
        /// is byte-identical to the resident overload on the same values.
        /// </summary>
        internal static void ClampExperimentQToBestRunFlat(
            uint[] entryIds, bool[] labels, string[] peptides,
            double[] runPrecursorQvalues, double[] runPeptideQvalues,
            double[] expPrecursorQvalues, double[] expPeptideQvalues)
        {
            BuildExperimentQClampFloors(
                entryIds, labels, peptides, runPrecursorQvalues, runPeptideQvalues,
                out var minRunBothByEntryId, out var minRunBothByPeptide);

            int n = entryIds.Length;
            for (int i = 0; i < n; i++)
            {
                double floorPrec;
                if (minRunBothByEntryId.TryGetValue(entryIds[i], out floorPrec) &&
                    floorPrec > expPrecursorQvalues[i])
                    expPrecursorQvalues[i] = floorPrec;

                if (!string.IsNullOrEmpty(peptides[i]))
                {
                    double floorPept;
                    if (minRunBothByPeptide.TryGetValue((peptides[i], labels[i]), out floorPept) &&
                        floorPept > expPeptideQvalues[i])
                        expPeptideQvalues[i] = floorPept;
                }
            }
        }

        /// <summary>
        /// Builds the min-over-runs combined-run-q floors that
        /// <see cref="ClampExperimentQToBestRunFlat"/> applies:
        /// <c>minRunBothByEntryId[entryId]</c> and <c>minRunBothByPeptide[(peptide, isDecoy)]</c>
        /// = the minimum over that entry's / peptide's rows of
        /// <c>max(runPrecursorQ, runPeptideQ)</c>. Bounded (O(distinct entryIds) +
        /// O(distinct peptides)), shared with the projection score pass
        /// (<see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>) so both clamp identically
        /// without a resident per-row experiment-q array (issue #4355 Part B).
        /// </summary>
        private static void BuildExperimentQClampFloors(
            uint[] entryIds, bool[] labels, string[] peptides,
            double[] runPrecursorQvalues, double[] runPeptideQvalues,
            out Dictionary<uint, double> minRunBothByEntryId,
            out Dictionary<(string, bool), double> minRunBothByPeptide)
        {
            int n = entryIds.Length;
            minRunBothByEntryId = new Dictionary<uint, double>();
            minRunBothByPeptide = new Dictionary<(string, bool), double>();
            for (int i = 0; i < n; i++)
            {
                double runBoth = Math.Max(runPrecursorQvalues[i], runPeptideQvalues[i]);
                UpdateExperimentQClampFloor(
                    minRunBothByEntryId, minRunBothByPeptide, entryIds[i], peptides[i], labels[i], runBoth);
            }
        }

        /// <summary>
        /// Folds one row into the best-of-runs clamp floors (issue #4390): tracks the minimum over
        /// an entry's / peptide's rows of <paramref name="runBoth"/> = <c>max(runPrecursorQ,
        /// runPeptideQ)</c>, keyed by <paramref name="entryId"/> and by
        /// <c>(<paramref name="peptide"/>, <paramref name="isDecoy"/>)</c>. Shared by
        /// <see cref="BuildExperimentQClampFloors"/> (flat path) and the projection score pass's
        /// floor reduction so the two cannot drift on a byte-identity-locked path (issue #4355
        /// Part B). An empty ModifiedSequence has no peptide identity and is not bucketed; peptide
        /// identity is (sequence, isDecoy) so a decoy's good run never lowers its target's floor.
        /// </summary>
        internal static void UpdateExperimentQClampFloor(
            Dictionary<uint, double> minRunBothByEntryId,
            Dictionary<(string, bool), double> minRunBothByPeptide,
            uint entryId, string peptide, bool isDecoy, double runBoth)
        {
            double curPrec;
            if (!minRunBothByEntryId.TryGetValue(entryId, out curPrec) || runBoth < curPrec)
                minRunBothByEntryId[entryId] = runBoth;

            if (string.IsNullOrEmpty(peptide))
                return;
            var pkey = (peptide, isDecoy);
            double curPept;
            if (!minRunBothByPeptide.TryGetValue(pkey, out curPept) || runBoth < curPept)
                minRunBothByPeptide[pkey] = runBoth;
        }

        // ============================================================
        // SVM fold training
        // ============================================================

        /// <summary>
        /// Serializes and collapses the per-cycle training progress emitted from inside the
        /// parallel fold training (OspreyParallel.For below). The NFolds cross-validation folds
        /// run on dedicated threads, each reporting one update per iteration in nondeterministic
        /// completion order. This reporter buffers a given iteration's per-fold reports under a
        /// lock and flushes them only once all folds have reported, so output is always ordered:
        /// <list type="bullet">
        /// <item>Default: one line reporting the percent of training targets passing at the train
        /// FDR -- passing/total summed over folds, a ratio that cancels both the subsample scale
        /// and the CV fold-overlap double-count, giving a scale-free convergence signal.</item>
        /// <item>--verbose: each fold's line in fold order, with its own count, denominator, and
        /// percent (so the ~2/3 per-fold training split is explicit, not assumed).</item>
        /// </list>
        /// Early-converging folds that stop before others simply leave that iteration's partial
        /// buffer unflushed (the always-emitted result line carries the final count).
        /// </summary>
        private sealed class TrainProgressReporter
        {
            private sealed class FoldReport
            {
                public int Fold;
                public int Passing;
                public int Targets;
            }

            private readonly int _nFolds;
            private readonly int _maxIterations;
            private readonly double _trainFdr;
            private readonly object _lock = new object();
            private readonly Dictionary<int, List<FoldReport>> _rounds =
                new Dictionary<int, List<FoldReport>>();

            public TrainProgressReporter(int nFolds, int maxIterations, double trainFdr)
            {
                _nFolds = nFolds;
                _maxIterations = maxIterations;
                _trainFdr = trainFdr;
            }

            public void ReportIteration(int foldIndex, int iteration, int nPassing, int nTargets)
            {
                lock (_lock)
                {
                    List<FoldReport> reports;
                    if (!_rounds.TryGetValue(iteration, out reports))
                    {
                        reports = new List<FoldReport>(_nFolds);
                        _rounds[iteration] = reports;
                    }
                    reports.Add(new FoldReport { Fold = foldIndex, Passing = nPassing, Targets = nTargets });
                    if (reports.Count < _nFolds)
                        return;
                    _rounds.Remove(iteration);

                    if (OspreyOutput.Verbose)
                    {
                        reports.Sort((a, b) => a.Fold.CompareTo(b.Fold)); // Array.Sort OK: Verbose diagnostic print only (not parity-sensitive); one report per fold so Fold is unique anyway
                        foreach (var r in reports)
                        {
                            double foldPct = r.Targets > 0 ? 100.0 * r.Passing / r.Targets : 0.0;
                            OspreyOutput.Out.WriteLine(
                                "  Percolator fold {0}/{1}: iteration {2} of {3} ({4} of {5} targets, {6:F1}% at {7:P0} FDR)",
                                r.Fold + 1, _nFolds, iteration + 1, _maxIterations,
                                r.Passing, r.Targets, foldPct, _trainFdr);
                        }
                        return;
                    }

                    int sumPassing = 0, sumTargets = 0;
                    foreach (var r in reports)
                    {
                        sumPassing += r.Passing;
                        sumTargets += r.Targets;
                    }
                    double pct = sumTargets > 0 ? 100.0 * sumPassing / sumTargets : 0.0;
                    OspreyOutput.Out.WriteLine(
                        "  Percolator iteration {0} of {1} ({2:F1}% of training targets at {3:P0} FDR)",
                        iteration + 1, _maxIterations, pct, _trainFdr);
                }
            }
        }

        private static LinearSvmClassifier TrainFold(
            Matrix stdFeatures,
            bool[] labels,
            uint[] entryIds,
            string[] peptides,
            int[] trainIndices,
            double[] initialScores,
            PercolatorConfig config,
            double trainFdr,
            SvmTrainScratchPool svmScratchPool,
            int foldIndex,
            TrainProgressReporter progress,
            out int bestIteration,
            out double bestC)
        {
            int nFeatures = stdFeatures.Cols;
            var currentScores = (double[])initialScores.Clone();
            // The C (SVM cost / margin) of the winning iteration's model. Tracked
            // alongside bestModel so Stage 5 can report the selected regularization
            // on the default console (issue #4364): C is swept in log steps per
            // GridSearchC and chosen by inner CV each iteration, so the fold's C is
            // whichever iteration's model became bestModel below.
            bestC = 1.0;

            // Rent one scratch for this outer fold's sequential Train calls
            // (the final per-iteration Train at the bottom of the loop). The
            // inner parallel grid search rents its own scratches from the
            // same pool. Return at the end of the fold.
            var foldScratch = svmScratchPool != null ? svmScratchPool.Rent() : null;
            try {

            var bestModel = LinearSvmClassifier.Train(
                Matrix.Zeros(0, nFeatures), new bool[0], 1.0, config.Seed);
            bestIteration = 0;
            int bestPassing = 0;
            int consecutiveNoImprove = 0;

            var trainLabels = new bool[trainIndices.Length];
            var trainEntryIds = new uint[trainIndices.Length];
            int nTrainTargets = 0;
            for (int i = 0; i < trainIndices.Length; i++)
            {
                trainLabels[i] = labels[trainIndices[i]];
                trainEntryIds[i] = entryIds[trainIndices[i]];
                if (!trainLabels[i])
                    nTrainTargets++;
            }

            for (int iteration = 0; iteration < config.MaxIterations; iteration++)
            {
                // i. Select positive training set
                var trainCurrentScores = new double[trainIndices.Length];
                for (int i = 0; i < trainIndices.Length; i++)
                    trainCurrentScores[i] = currentScores[trainIndices[i]];

                var selectedTargetIndices = SelectPositiveTrainingSet(
                    trainCurrentScores, trainLabels, trainEntryIds, trainFdr, MIN_POSITIVE);

                if (selectedTargetIndices.Length == 0)
                    break;

                // Build SVM training set: selected targets + all decoys
                var decoyIndices = new List<int>();
                for (int i = 0; i < trainIndices.Length; i++)
                {
                    if (trainLabels[i])
                        decoyIndices.Add(i);
                }
                var svmIndices = new List<int>(selectedTargetIndices);
                svmIndices.AddRange(decoyIndices);

                var svmGlobalIndices = new int[svmIndices.Count];
                for (int i = 0; i < svmIndices.Count; i++)
                    svmGlobalIndices[i] = trainIndices[svmIndices[i]];

                // svmFeatures is live from here through the Train call below
                // (used by Train + DecisionFunction). Use foldScratch.TrainData
                // to avoid an 8+ MB LOH allocation per TrainFold iteration.
                Matrix svmFeatures;
                if (foldScratch != null)
                {
                    foldScratch.EnsureExtractCapacity(svmGlobalIndices.Length, nFeatures);
                    svmFeatures = MatrixRows.ExtractRowsInto(stdFeatures, svmGlobalIndices, foldScratch.TrainData);
                }
                else
                {
                    svmFeatures = MatrixRows.ExtractRows(stdFeatures, svmGlobalIndices);
                }
                var svmLabels = new bool[svmIndices.Count];
                var svmEntryIds = new uint[svmIndices.Count];
                for (int i = 0; i < svmIndices.Count; i++)
                {
                    svmLabels[i] = trainLabels[svmIndices[i]];
                    svmEntryIds[i] = trainEntryIds[svmIndices[i]];
                }

                // ii. Grid search for best C
                var svmPeptides = new string[svmIndices.Count];
                for (int i = 0; i < svmIndices.Count; i++)
                    svmPeptides[i] = peptides[trainIndices[svmIndices[i]]];
                var svmFoldAssignments = PercolatorSampling.CreateStratifiedFoldsByPeptide(
                    svmLabels, svmPeptides, svmEntryIds, config.NFolds);

                double bestC1 = GridSearchC(
                    svmFeatures, svmLabels, svmEntryIds,
                    config.CValues, svmFoldAssignments, config.NFolds,
                    config.Seed, trainFdr, svmScratchPool);

                // iii. Train SVM with best C
                var model = LinearSvmClassifier.Train(
                    svmFeatures, svmLabels, bestC1, config.Seed, foldScratch);

                // iv. Score ALL training set entries with new model
                // trainFeatures is live just for the DecisionFunction call;
                // foldScratch.TestData is not used elsewhere in this iteration
                // (svmFeatures is in TrainData), so reuse it here.
                Matrix trainFeatures;
                if (foldScratch != null)
                {
                    foldScratch.EnsureExtractCapacity(trainIndices.Length, nFeatures);
                    trainFeatures = MatrixRows.ExtractRowsInto(stdFeatures, trainIndices, foldScratch.TestData);
                }
                else
                {
                    trainFeatures = MatrixRows.ExtractRows(stdFeatures, trainIndices);
                }
                var newTrainScores = model.DecisionFunction(trainFeatures);

                for (int i = 0; i < trainIndices.Length; i++)
                    currentScores[trainIndices[i]] = newTrainScores[i];

                // v. Count passing targets
                int nPassing = CountPassing(newTrainScores, trainLabels, trainEntryIds, trainFdr, foldScratch);

                // Per-cycle progress so the otherwise-silent SVM training (tens of
                // seconds on Stellar/Astral-scale inputs) shows liveness, the way
                // Skyline reports mProphet LDA refinement cycles. The reporter collapses
                // the parallel folds' updates to one summed line per iteration by default
                // (--verbose shows each fold). A determinate ProgressStatus does not fit:
                // the loop stops on convergence (consecutiveNoImprove) before MaxIterations.
                progress.ReportIteration(foldIndex, iteration, nPassing, nTrainTargets);

                if (nPassing > bestPassing)
                {
                    bestModel = model;
                    bestPassing = nPassing;
                    bestIteration = iteration + 1;
                    bestC = bestC1;
                    consecutiveNoImprove = 0;
                }
                else
                {
                    consecutiveNoImprove++;
                }

                if (consecutiveNoImprove >= 2)
                    break;
            }

            bestIteration = Math.Max(bestIteration, 1);
            return bestModel;

            } finally {
                if (foldScratch != null && svmScratchPool != null)
                    svmScratchPool.Return(foldScratch);
            }
        }

        /// <summary>
        /// Gradient-boosted-trees counterpart of <see cref="TrainFold"/>
        /// (<c>--fdr-method gbdt</c>): the SAME semi-supervised loop -- select the
        /// targets that reach <paramref name="trainFdr"/> under the current score, train
        /// on those positives against all decoys, re-score, keep the iteration that
        /// passes the most targets, stop after two without improvement -- with the linear
        /// SVM swapped for a tree ensemble.
        ///
        /// Two things drop out relative to the SVM, both because trees have no cost
        /// parameter: the inner <see cref="GridSearchC"/> sweep (capacity is fixed by
        /// <see cref="PercolatorConfig.GbtParams"/> instead) and the scratch pool (which
        /// exists to recycle the SVM's per-iteration <see cref="Matrix"/> buffers).
        /// Everything the fold selection depends on -- <see cref="SelectPositiveTrainingSet"/>,
        /// <see cref="CountPassing(double[],bool[],uint[],double)"/>, and the caller's
        /// peptide-grouped fold assignment --
        /// is the identical shared code, so the two methods differ only in the classifier.
        /// </summary>
        // Inner validation fraction for tree iteration selection: 1 of these folds is held
        // out of tree fitting and used ONLY to pick the best iteration. 5 -> 20% held out.
        private static GradientBoostedTrees TrainFoldGbt(
            Matrix stdFeatures,
            bool[] labels,
            uint[] entryIds,
            string[] peptides,
            int[] trainIndices,
            double[] initialScores,
            PercolatorConfig config,
            double trainFdr,
            int foldIndex,
            TrainProgressReporter progress,
            out int bestIteration)
        {
            var currentScores = (double[])initialScores.Clone();
            GradientBoostedTrees bestModel = null;
            bestIteration = 0;
            // -1, where TrainFold uses 0: the SVM seeds bestModel with a degenerate
            // zero-weight model that is a valid scorer if no iteration ever passes a
            // target, but there is no equivalent empty tree ensemble (Train rejects an
            // empty training set). Starting below zero guarantees the first trained
            // model is installed, so a fold that passes nothing still returns a real
            // model rather than null.
            int bestPassing = -1;
            int consecutiveNoImprove = 0;

            var trainLabels = new bool[trainIndices.Length];
            var trainEntryIds = new uint[trainIndices.Length];
            var trainPeptides = new string[trainIndices.Length];
            int nTrainTargets = 0;
            for (int i = 0; i < trainIndices.Length; i++)
            {
                trainLabels[i] = labels[trainIndices[i]];
                trainEntryIds[i] = entryIds[trainIndices[i]];
                trainPeptides[i] = peptides[trainIndices[i]];
                if (!trainLabels[i])
                    nTrainTargets++;
            }

            // Inner held-out split for HONEST iteration selection. The tree ensemble grows
            // in capacity every iteration, so counting passing targets on the rows it was
            // fit on is an in-sample metric that rises monotonically -- argmax then always
            // picks the last (most overfit) iteration and never early-stops. Instead, hold
            // out one inner fold, fit the trees on the rest, and pick the iteration that
            // passes the most targets on the HELD-OUT rows. The linear SVM's TrainFold does
            // not need this (a high-bias model has a negligible in/out-of-sample gap), so
            // its parity-locked loop is untouched. The split reuses the same peptide-grouped
            // fold assignment as the outer CV, so target-decoy pairs and a peptide's charge
            // states stay on one side -- no leakage between fit and validation.
            // Inner-fold count is env-tunable (OSPREY_GBT_INNER_FOLDS, default 5 -> 20% val).
            // A value <= 1 skips the split and selects the iteration IN-SAMPLE (fit = val =
            // all training rows) -- the pre-held-out behavior -- so held-out selection can be
            // turned off for a regularization sweep or an A/B without a code revert.
            int innerFolds = OspreyEnvironment.GbtInnerFolds;
            int[] innerFold = innerFolds > 1
                ? PercolatorSampling.CreateStratifiedFoldsByPeptide(
                    trainLabels, trainPeptides, trainEntryIds, innerFolds)
                : null;
            var fitLocal = new List<int>(trainIndices.Length);
            var valLocal = new List<int>(innerFolds > 1
                ? trainIndices.Length / innerFolds + 1
                : trainIndices.Length);
            if (innerFold != null)
            {
                for (int i = 0; i < trainIndices.Length; i++)
                {
                    if (innerFold[i] == 0) valLocal.Add(i); else fitLocal.Add(i);
                }
            }
            // No split (innerFolds <= 1), or degenerate tiny folds (no validation or no fit
            // rows), fall back to using all training rows for both -- in-sample selection --
            // rather than failing. Also reached on pathologically small inputs the tests use.
            bool haveVal = innerFold != null && valLocal.Count > 0 && fitLocal.Count > 0;
            if (!haveVal)
            {
                fitLocal.Clear();
                for (int i = 0; i < trainIndices.Length; i++) fitLocal.Add(i);
                valLocal = fitLocal;
            }

            var fitLabels = new bool[fitLocal.Count];
            var fitEntryIds = new uint[fitLocal.Count];
            for (int i = 0; i < fitLocal.Count; i++)
            {
                fitLabels[i] = trainLabels[fitLocal[i]];
                fitEntryIds[i] = trainEntryIds[fitLocal[i]];
            }
            var valLabels = new bool[valLocal.Count];
            var valEntryIds = new uint[valLocal.Count];
            int nValTargets = 0;
            for (int i = 0; i < valLocal.Count; i++)
            {
                valLabels[i] = trainLabels[valLocal[i]];
                valEntryIds[i] = trainEntryIds[valLocal[i]];
                if (!valLabels[i]) nValTargets++;
            }

            var rowBuf = new double[stdFeatures.Cols];
            for (int iteration = 0; iteration < config.MaxIterations; iteration++)
            {
                // i. Select positive training set under the CURRENT score -- from the FIT
                //    rows only, so the validation rows never influence what the trees fit.
                var fitCurrentScores = new double[fitLocal.Count];
                for (int i = 0; i < fitLocal.Count; i++)
                    fitCurrentScores[i] = currentScores[trainIndices[fitLocal[i]]];

                var selectedFitTargets = SelectPositiveTrainingSet(
                    fitCurrentScores, fitLabels, fitEntryIds, trainFdr, MIN_POSITIVE);

                if (selectedFitTargets.Length == 0)
                    break;

                // Training set: selected confident FIT targets + all FIT decoys. Train
                // reads the same isDecoy convention the SVM does (decoy = negative).
                var gbtLocal = new List<int>(selectedFitTargets);  // local indices into fitLocal
                for (int i = 0; i < fitLocal.Count; i++)
                {
                    if (fitLabels[i])
                        gbtLocal.Add(i);
                }

                var gbtRows = new double[gbtLocal.Count][];
                var gbtLabels = new bool[gbtLocal.Count];
                for (int i = 0; i < gbtLocal.Count; i++)
                {
                    gbtRows[i] = MatrixRows.ExtractRow(stdFeatures, trainIndices[fitLocal[gbtLocal[i]]]);
                    gbtLabels[i] = fitLabels[gbtLocal[i]];
                }

                // ii. Train. No grid search: capacity is bounded by depth / gamma /
                //     lambda / min-child-weight, all fixed up front.
                var model = GradientBoostedTrees.Train(gbtRows, gbtLabels, config.GbtParams);

                // iii. Re-score the FIT rows (drives the next iteration's positive
                //      selection) and the VALIDATION rows (drives model selection).
                for (int i = 0; i < fitLocal.Count; i++)
                {
                    MatrixRows.CopyRow(stdFeatures, trainIndices[fitLocal[i]], rowBuf);
                    currentScores[trainIndices[fitLocal[i]]] = model.ScoreSingle(rowBuf);
                }
                var valScores = new double[valLocal.Count];
                for (int i = 0; i < valLocal.Count; i++)
                {
                    MatrixRows.CopyRow(stdFeatures, trainIndices[valLocal[i]], rowBuf);
                    valScores[i] = model.ScoreSingle(rowBuf);
                }

                // iv. Keep the iteration that passes the most targets on the HELD-OUT rows.
                int nPassing = CountPassing(valScores, valLabels, valEntryIds, trainFdr);
                progress.ReportIteration(foldIndex, iteration, nPassing, nValTargets);

                if (nPassing > bestPassing)
                {
                    bestModel = model;
                    bestPassing = nPassing;
                    bestIteration = iteration + 1;
                    consecutiveNoImprove = 0;
                }
                else
                {
                    consecutiveNoImprove++;
                }

                if (consecutiveNoImprove >= 2)
                    break;
            }

            if (bestModel == null)
            {
                // Only reachable when the very first SelectPositiveTrainingSet found no
                // targets even after relaxing to 50% FDR -- i.e. the fold has essentially
                // no target population. Fail loudly: a constant-scoring fallback would
                // silently produce meaningless q-values that still look like a result.
                throw new InvalidOperationException(string.Format(
                    @"TrainFoldGbt: fold {0} selected no positive training targets at any " +
                    @"FDR threshold ({1} training entries, {2} targets); cannot train a model.",
                    foldIndex, trainIndices.Length, nTrainTargets));
            }

            bestIteration = Math.Max(bestIteration, 1);
            return bestModel;
        }

        // ============================================================
        // Target-decoy competition and q-value computation
        // ============================================================

        /// <summary>
        /// Core competition logic: group by base_id, compete, return winners sorted by score desc.
        ///
        /// This is deliberately a SEPARATE implementation from
        /// <see cref="FdrController.CompeteAndFilter{T}"/>, not a duplicate to be
        /// merged: the two serve different regimes. This array/index form is the
        /// hot Percolator path -- it works on pre-flattened primitive arrays and a
        /// caller-supplied index subset, returns winner arrays for downstream
        /// scratch-pooled q-value passes (see <c>CountPassing</c>), and
        /// allocates nothing on the scratch overload. <c>CompeteAndFilter</c> is
        /// the ergonomic generic form for simple-FDR callers
        /// (<see cref="PercolatorEngine.RunSimpleFdr"/>): it competes an
        /// <c>IEnumerable&lt;T&gt;</c> via score/decoy/id selectors and returns a
        /// typed result. Same competition rule (strict &gt;, ties to decoy), two
        /// shapes tuned to performance vs. ergonomics.
        /// </summary>
        public static void CompeteFromIndices(
            double[] scores,
            bool[] labels,
            uint[] entryIds,
            int[] indices,
            out int[] winnerIndices,
            out double[] winnerScores,
            out bool[] winnerIsDecoy,
            ProgressReporter progress = null)
        {
            var targets = new Dictionary<uint, KeyValuePair<int, double>>();
            var decoys = new Dictionary<uint, KeyValuePair<int, double>>();

            // Throttled per-row progress for the large experiment / PEP competitions -- the
            // ~344M-row base_id reduction below ran ~90 s silent at 82 files. Console-only via
            // the caller's reporter (null on the small per-file per-run calls, which report at
            // their own per-file granularity); never affects the winners, so q-values are
            // byte-identical.
            long processed = 0;
            foreach (int idx in indices)
            {
                if (progress != null && (++processed & 0x3FFFFF) == 0)
                    progress.Report(processed);
                uint baseId = entryIds[idx] & BASE_ID_MASK;
                if (labels[idx])
                {
                    KeyValuePair<int, double> existing;
                    if (decoys.TryGetValue(baseId, out existing))
                    {
                        if (scores[idx] > existing.Value)
                            decoys[baseId] = new KeyValuePair<int, double>(idx, scores[idx]);
                    }
                    else
                    {
                        decoys[baseId] = new KeyValuePair<int, double>(idx, scores[idx]);
                    }
                }
                else
                {
                    KeyValuePair<int, double> existing;
                    if (targets.TryGetValue(baseId, out existing))
                    {
                        if (scores[idx] > existing.Value)
                            targets[baseId] = new KeyValuePair<int, double>(idx, scores[idx]);
                    }
                    else
                    {
                        targets[baseId] = new KeyValuePair<int, double>(idx, scores[idx]);
                    }
                }
            }

            CompeteFromDicts(targets, decoys,
                out winnerIndices, out winnerScores, out winnerIsDecoy, out _);
        }

        /// <summary>
        /// Shared finish for target/decoy competition: given per-base_id best-target and
        /// best-decoy maps (winning row index + score), compete each pair (higher score wins,
        /// ties to decoy), add unpaired decoys, and sort winners by score desc / base_id asc.
        /// Extracted from <see cref="CompeteFromIndices"/> so the flat-array path (which builds
        /// the maps by walking an index subset) and the streaming path (issue #4355 struct-shrink
        /// S3, which builds the identical maps by pushing rows in flat (file,row) order) share the
        /// EXACT compete + sort, and so cannot drift. The stored index is the winning row's flat
        /// index / streaming ordinal; both label the same row because the streaming pass visits
        /// rows in the same order the flat arrays were built. <paramref name="winnerBaseIds"/>
        /// carries each winner's base_id (the map key) so the streaming path can key the
        /// experiment-precursor / PEP maps WITHOUT a resident <c>entryIds[]</c> array (the flat
        /// path recovers the same base_id via <c>entryIds[wi[rank]] &amp; BASE_ID_MASK</c>).
        /// </summary>
        internal static void CompeteFromDicts(
            Dictionary<uint, KeyValuePair<int, double>> targets,
            Dictionary<uint, KeyValuePair<int, double>> decoys,
            out int[] winnerIndices,
            out double[] winnerScores,
            out bool[] winnerIsDecoy,
            out uint[] winnerBaseIds)
        {
            // Compete pairs: higher score wins, ties go to decoy
            var winners = new List<Tuple<int, double, bool, uint>>(targets.Count);
            foreach (var kvp in targets)
            {
                uint baseId = kvp.Key;
                int tIdx = kvp.Value.Key;
                double tScore = kvp.Value.Value;

                KeyValuePair<int, double> decoyEntry;
                if (decoys.TryGetValue(baseId, out decoyEntry))
                {
                    if (tScore > decoyEntry.Value)
                        winners.Add(Tuple.Create(tIdx, tScore, false, baseId));
                    else
                        winners.Add(Tuple.Create(decoyEntry.Key, decoyEntry.Value, true, baseId));
                }
                else
                {
                    winners.Add(Tuple.Create(tIdx, tScore, false, baseId));
                }
            }
            // Unpaired decoys
            foreach (var kvp in decoys)
            {
                if (!targets.ContainsKey(kvp.Key))
                    winners.Add(Tuple.Create(kvp.Value.Key, kvp.Value.Value, true, kvp.Key));
            }

            // Sort by score desc, then base_id asc for deterministic tiebreaking.
            // Array.Sort OK: the secondary key Item4 is the unique base_id, so the
            // comparator never returns 0 and the unstable-sort tie path is unreachable.
            winners.Sort((a, b) => // Array.Sort OK: (see above) secondary key Item4 is unique base_id, comparator never ties
            {
                int cmp = b.Item2.CompareTo(a.Item2);
                if (cmp != 0)
                    return cmp;
                return a.Item4.CompareTo(b.Item4);
            });

            winnerIndices = new int[winners.Count];
            winnerScores = new double[winners.Count];
            winnerIsDecoy = new bool[winners.Count];
            winnerBaseIds = new uint[winners.Count];
            for (int i = 0; i < winners.Count; i++)
            {
                winnerIndices[i] = winners[i].Item1;
                winnerScores[i] = winners[i].Item2;
                winnerIsDecoy[i] = winners[i].Item3;
                winnerBaseIds[i] = winners[i].Item4;
            }
        }

        /// <summary>
        /// OSPREY_PASS2_QVALUE=transfer-compete (full-population form): given the FULL
        /// 1st-pass population as flat SCALAR arrays -- scores, is_decoy labels, entry_ids,
        /// file names, all index-aligned, with the reconciled minority's scores already
        /// overwritten by the caller -- run the global target-decoy competition and compute
        /// per-run + experiment PRECURSOR q-values and PEP over that full population. Same
        /// competition/PEP/q math as <see cref="PercolatorScorer.ScorePopulationAndComputeFdr"/>, but takes
        /// pre-computed scores (no features, no model application), so the 2nd pass can
        /// recompete over the persisted full-population scalars from
        /// <c>.1st-pass.fdr_scores.bin</c> -- with only the ~0.4% reconciled scores swapped in
        /// -- without ever holding features resident. Outputs are index-aligned to the inputs.
        /// Precursor-level only (the entrapment-FDR path); peptide-level q is not computed here.
        /// </summary>
        public static void ComputeFullPopulationPrecursorFdr(
            double[] scores, bool[] labels, uint[] entryIds, string[] fileNames,
            out double[] runPrecursorQ, out double[] experimentPrecursorQ, out double[] pep)
        {
            int n = scores.Length;

            // Global target-decoy competition (group by base_id, winner per pair) + PEP on winners.
            CompeteAll(scores, labels, entryIds,
                out int[] winnerIndices, out double[] winnerScores, out bool[] winnerIsDecoy);
            var pepEstimator = PepEstimator.FitDefault(winnerScores, winnerIsDecoy);
            pep = new double[n];
            for (int i = 0; i < n; i++) pep[i] = 1.0;
            foreach (int idx in winnerIndices)
                pep[idx] = pepEstimator.PosteriorError(scores[idx]);

            // Per-run and experiment-wide precursor q over the full population.
            runPrecursorQ = ComputePerRunPrecursorQvalues(scores, labels, entryIds, fileNames);
            var uniqueFiles = new HashSet<string>(fileNames);
            experimentPrecursorQ = uniqueFiles.Count <= 1
                ? (double[])runPrecursorQ.Clone()
                : ComputeExperimentPrecursorQvalues(scores, labels, entryIds);

            // Best-of-runs monotonicity (issue #4390): floor each entry's experiment q up to
            // its own best (min-over-runs) run q -- an experiment q is never more confident
            // than the precursor's best single run. Keyed by full entry_id (target and decoy
            // are distinct entries), matching ClampExperimentQToBestRunFlat's precursor clamp.
            var bestRunQ = new Dictionary<uint, double>();
            for (int i = 0; i < n; i++)
                if (!bestRunQ.TryGetValue(entryIds[i], out double q) || runPrecursorQ[i] < q)
                    bestRunQ[entryIds[i]] = runPrecursorQ[i];
            for (int i = 0; i < n; i++)
                if (experimentPrecursorQ[i] < bestRunQ[entryIds[i]])
                    experimentPrecursorQ[i] = bestRunQ[entryIds[i]];
        }

        /// <summary>
        /// Bounded-memory streaming form of <see cref="ComputeFullPopulationPrecursorFdr"/> for
        /// OSPREY_PASS2_QVALUE=transfer-compete. Streams one file's 1st-pass population at a time
        /// (run-level competition + conservative q per file) while accumulating only the
        /// per-base_id best target/decoy observation for the experiment-level competition.
        /// Resident footprint is therefore O(distinct precursors + largest single file +
        /// survivors) -- flat in file count -- where the resident overload is O(total
        /// observations). Emits run/experiment precursor q and PEP identical to the resident
        /// method for the reported survivors (verified byte-for-byte on the 3-file Stellar
        /// entrapment set). File reading is injected so this assembly needs no IO dependency.
        /// </summary>
        /// <param name="fileKeys">Stable per-file keys to stream, in any order.</param>
        /// <param name="readFileScalars">Reads one file's full population as (entryIds, scores);
        ///   invoked once per file, arrays released before the next file is read.</param>
        /// <param name="survivorScoreOverride">Frozen-model score to substitute for a reconciled
        ///   survivor observation, keyed (fileKey, entryId). Observations absent here keep their
        ///   stored 1st-pass score.</param>
        /// <param name="survivors">Every reported survivor (fileKey, entryId) to emit q/PEP for.</param>
        /// <param name="survivorRunQ">Out: run-level precursor q per reported (fileKey, entryId).</param>
        /// <param name="survivorExpQ">Out: experiment-level precursor q per reported (fileKey, entryId).</param>
        /// <param name="survivorPep">Out: PEP per reported (fileKey, entryId).</param>
        /// <param name="stratumBaseIds">Null for the full-population competition; non-null restricts
        ///   the competition to these base_ids (protein-compact).</param>
        public static void ComputeFullPopulationPrecursorFdrStreaming(
            IReadOnlyList<string> fileKeys,
            Func<string, (uint[] entryIds, double[] scores)> readFileScalars,
            IReadOnlyDictionary<(string, uint), double> survivorScoreOverride,
            IReadOnlyCollection<(string, uint)> survivors,
            out Dictionary<(string, uint), double> survivorRunQ,
            out Dictionary<(string, uint), double> survivorExpQ,
            out Dictionary<(string, uint), double> survivorPep,
            HashSet<uint> stratumBaseIds = null)
        {
            // stratumBaseIds == null -> full-population competition (transfer-compete).
            // non-null -> STRATIFIED competition (protein-compact): only observations whose
            // base_id is in the stratum participate in the run/experiment competitions, so
            // off-stratum decoys leave the null (reduced multiple testing). The per-base_id
            // maps hold only stratum members, so peak memory stays flat in file count -- it
            // only shrinks relative to the full-population path.
            var survivorSet = new HashSet<(string, uint)>(survivors);
            var survivorEntryIds = new HashSet<uint>();
            foreach (var s in survivorSet) survivorEntryIds.Add(s.Item2);

            survivorRunQ = new Dictionary<(string, uint), double>(survivorSet.Count);
            survivorExpQ = new Dictionary<(string, uint), double>(survivorSet.Count);
            survivorPep = new Dictionary<(string, uint), double>(survivorSet.Count);

            // Experiment-level per-base_id best target/decoy observation (score + locator),
            // accumulated across every file. Bounded by the number of distinct precursors.
            var bestTarget = new Dictionary<uint, (double score, int fileIdx, uint entryId)>();
            var bestDecoy = new Dictionary<uint, (double score, int fileIdx, uint entryId)>();

            // Best (min) run q per SURVIVOR entry_id across the files it won in -- the
            // best-of-runs monotonicity floor for the experiment q (only survivors are emitted).
            var minRunQ = new Dictionary<uint, double>(survivorEntryIds.Count);

            for (int fileIdx = 0; fileIdx < fileKeys.Count; fileIdx++)
            {
                string fileKey = fileKeys[fileIdx];
                var (entryIds, scores) = readFileScalars(fileKey);
                int m = entryIds.Length;
                var labels = new bool[m];
                for (int i = 0; i < m; i++)
                {
                    uint eid = entryIds[i];
                    labels[i] = (eid & ~BASE_ID_MASK) != 0u; // decoy high bit set
                    if (survivorScoreOverride.TryGetValue((fileKey, eid), out double ov))
                        scores[i] = ov; // swap in the reconciled survivor's frozen-model score
                }

                // Run-level: compete within this file (only stratum members when
                // stratified), conservative q on the winners.
                int[] allIdx;
                if (stratumBaseIds == null)
                {
                    allIdx = new int[m];
                    for (int i = 0; i < m; i++) allIdx[i] = i;
                }
                else
                {
                    var idxList = new List<int>(m);
                    for (int i = 0; i < m; i++)
                        if (stratumBaseIds.Contains(entryIds[i] & BASE_ID_MASK)) idxList.Add(i);
                    allIdx = idxList.ToArray();
                }
                CompeteFromIndices(scores, labels, entryIds, allIdx,
                    out int[] wi, out double[] ws, out bool[] wd);
                var q = new double[wi.Length];
                ComputeConservativeQvalues(ws, wd, q);
                for (int rank = 0; rank < wi.Length; rank++)
                {
                    uint eid = entryIds[wi[rank]];
                    if (!survivorEntryIds.Contains(eid)) continue;
                    double qv = q[rank];
                    var key = (fileKey, eid);
                    if (survivorSet.Contains(key)) survivorRunQ[key] = qv;
                    if (!minRunQ.TryGetValue(eid, out double cur) || qv < cur) minRunQ[eid] = qv;
                }

                // Experiment-level: fold every observation into the per-base_id bests
                // (stratum members only when stratified -> the experiment competition
                // below runs over exactly the stratum's base_ids).
                for (int i = 0; i < m; i++)
                {
                    uint eid = entryIds[i];
                    uint bid = eid & BASE_ID_MASK;
                    if (stratumBaseIds != null && !stratumBaseIds.Contains(bid)) continue;
                    double s = scores[i];
                    if (labels[i])
                    {
                        if (!bestDecoy.TryGetValue(bid, out var cur) || s > cur.score)
                            bestDecoy[bid] = (s, fileIdx, eid);
                    }
                    else
                    {
                        if (!bestTarget.TryGetValue(bid, out var cur) || s > cur.score)
                            bestTarget[bid] = (s, fileIdx, eid);
                    }
                }
                // entryIds/scores/labels/allIdx released here before the next file is read.
            }

            // Experiment competition: one winner per base_id, conservative q, PEP fit over
            // exactly the winner set the resident method fits.
            var baseIds = new HashSet<uint>(bestTarget.Keys);
            baseIds.UnionWith(bestDecoy.Keys);
            int w = baseIds.Count;
            var expScore = new double[w];
            var expIsDecoy = new bool[w];
            var expBaseId = new uint[w];
            var winnerLoc = new Dictionary<uint, (int fileIdx, uint entryId, double score)>(w);
            int wi2 = 0;
            foreach (uint bid in baseIds)
            {
                bool hasT = bestTarget.TryGetValue(bid, out var t);
                bool hasD = bestDecoy.TryGetValue(bid, out var d);
                // CompeteFromIndices: target wins strictly (tScore > dScore); ties go to the decoy.
                bool decoyWins = hasT && hasD ? !(t.score > d.score) : !hasT;
                var win = decoyWins ? d : t;
                expScore[wi2] = win.score; expIsDecoy[wi2] = decoyWins; expBaseId[wi2] = bid;
                winnerLoc[bid] = (win.fileIdx, win.entryId, win.score);
                wi2++;
            }

            // Sort winners by score desc, base_id asc (unique base_id => total order).
            var perm = new int[w];
            for (int i = 0; i < w; i++) perm[i] = i;
            Array.Sort(perm, (a, b) => // Array.Sort OK: unique baseId tie-break makes comparator total
            {
                int cmp = expScore[b].CompareTo(expScore[a]);
                return cmp != 0 ? cmp : expBaseId[a].CompareTo(expBaseId[b]);
            });
            var sortedScore = new double[w];
            var sortedDecoy = new bool[w];
            var sortedBaseId = new uint[w];
            for (int i = 0; i < w; i++)
            {
                sortedScore[i] = expScore[perm[i]];
                sortedDecoy[i] = expIsDecoy[perm[i]];
                sortedBaseId[i] = expBaseId[perm[i]];
            }
            var qExp = new double[w];
            ComputeConservativeQvalues(sortedScore, sortedDecoy, qExp);
            var baseIdExpQ = new Dictionary<uint, double>(w);
            for (int i = 0; i < w; i++) baseIdExpQ[sortedBaseId[i]] = qExp[i];

            var pepEstimator = PepEstimator.FitDefault(expScore, expIsDecoy);

            bool multiFile = fileKeys.Count > 1;
            foreach (var key in survivorSet)
            {
                string fileKey = key.Item1;
                uint eid = key.Item2;
                uint bid = eid & BASE_ID_MASK;

                if (!survivorRunQ.ContainsKey(key)) survivorRunQ[key] = 1.0;

                if (multiFile)
                {
                    // Experiment q = base_id winner q, floored up to this precursor's best run q.
                    // An entry_id that won no within-file competition has best run q = 1.0 (every
                    // observation stayed at the q=1.0 default), matching the resident bestRunQ.
                    double eq = baseIdExpQ.TryGetValue(bid, out double bq) ? bq : 1.0;
                    double floorQ = minRunQ.TryGetValue(eid, out double mrq) ? mrq : 1.0;
                    if (eq < floorQ) eq = floorQ;
                    survivorExpQ[key] = eq;
                }
                else
                {
                    // Single file: experiment q == run q (resident short-circuit).
                    survivorExpQ[key] = survivorRunQ[key];
                }

                // PEP is real only on the single experiment-winner observation of each base_id.
                double pep = 1.0;
                if (winnerLoc.TryGetValue(bid, out var loc) &&
                    loc.entryId == eid && fileKeys[loc.fileIdx] == fileKey)
                    pep = pepEstimator.PosteriorError(loc.score);
                survivorPep[key] = pep;
            }
        }

        private static void CompeteAll(
            double[] scores,
            bool[] labels,
            uint[] entryIds,
            out int[] winnerIndices,
            out double[] winnerScores,
            out bool[] winnerIsDecoy,
            ProgressReporter progress = null)
        {
            var allIndices = new int[scores.Length];
            for (int i = 0; i < scores.Length; i++)
                allIndices[i] = i;
            CompeteFromIndices(scores, labels, entryIds, allIndices,
                out winnerIndices, out winnerScores, out winnerIsDecoy, progress);
        }

        /// <summary>
        /// Compute conservative q-values: FDR = (n_decoy + 1) / n_target.
        /// Input must be sorted by score descending (winners from competition).
        /// </summary>
        public static void ComputeConservativeQvalues(
            double[] scores, bool[] isDecoy, double[] qValues)
        {
            ComputeQvaluesCore(isDecoy, qValues, isDecoy.Length, decoyOffset: 1);
        }

        /// <summary>
        /// Compute non-conservative q-values: FDR = n_decoy / n_target.
        /// Used internally for iteration tracking and positive training set selection.
        /// </summary>
        private static void ComputeQvalues(
            double[] scores, bool[] isDecoy, double[] qValues)
        {
            ComputeQvaluesCore(isDecoy, qValues, isDecoy.Length, decoyOffset: 0);
        }

        /// <summary>
        /// Count targets passing FDR threshold using non-conservative formula.
        /// </summary>
        public static int CountPassing(
            double[] scores, bool[] labels, uint[] entryIds, double fdrThreshold)
        {
            return CountPassing(scores, labels, entryIds, fdrThreshold, null);
        }

        /// <summary>
        /// Overload that reuses pre-allocated buffers from a
        /// <see cref="SvmTrainScratch"/>. Pass null
        /// to allocate per-call (the legacy path). For the hot Percolator
        /// path (CountPassing is called ~570x per grid-search session),
        /// passing scratch eliminates ~400 KB of per-call LOH allocation
        /// (int[scores.Length] + double[winners]) plus the
        /// CompeteFromIndices internal allocations via the scratch-aware
        /// helper below.
        /// </summary>
        public static int CountPassing(
            double[] scores, bool[] labels, uint[] entryIds, double fdrThreshold,
            SvmTrainScratch scratch)
        {
            if (scratch == null)
            {
                // Allocating path -- preserved verbatim for callers
                // that don't have a scratch (tests, non-hot sites).
                var allIndices = new int[scores.Length];
                for (int i = 0; i < scores.Length; i++)
                    allIndices[i] = i;

                int[] wi;
                double[] ws;
                bool[] wd;
                CompeteFromIndices(scores, labels, entryIds, allIndices, out wi, out ws, out wd);

                var qValues = new double[wi.Length];
                ComputeQvalues(ws, wd, qValues);

                int count = 0;
                for (int rank = 0; rank < wi.Length; rank++)
                {
                    if (!labels[wi[rank]] && qValues[rank] <= fdrThreshold)
                        count++;
                }
                return count;
            }

            scratch.EnsureCountPassingCapacity(scores.Length);
            int[] allIdx = scratch.CountPassingIndices;
            for (int i = 0; i < scores.Length; i++)
                allIdx[i] = i;

            int winnerCount = CompeteFromIndicesInto(
                scores, labels, entryIds, allIdx, scores.Length, scratch);

            double[] qVals = scratch.CountPassingQvalues;
            // ComputeQvalues operates on a winner-sized slice; pass the
            // prefix of the pooled arrays (Compute reads scores[i] for
            // i in [0, n), assuming n = winnerCount).
            ComputeQvaluesInto(
                scratch.CompetitionWinnerScores, scratch.CompetitionWinnerIsDecoy,
                qVals, winnerCount);

            int[] winIdx = scratch.CompetitionWinnerIndices;
            int passCount = 0;
            for (int rank = 0; rank < winnerCount; rank++)
            {
                if (!labels[winIdx[rank]] && qVals[rank] <= fdrThreshold)
                    passCount++;
            }
            return passCount;
        }

        /// <summary>
        /// Scratch-pooled internal variant of <see cref="CompeteFromIndices"/>.
        /// Writes winners into <paramref name="scratch"/>'s three
        /// CompetitionWinner* arrays (prefix [0..returned count) is
        /// active). Same algorithm as the allocating version; only the
        /// output destination differs. Returns the active winner count.
        /// </summary>
        private static int CompeteFromIndicesInto(
            double[] scores, bool[] labels, uint[] entryIds,
            int[] indices, int indicesCount,
            SvmTrainScratch scratch)
        {
            // Allocate the small per-call dictionaries / list at full
            // expected capacity to avoid rehash growth. Could be pooled
            // on scratch in a follow-up; the n*p allocations above are
            // the bigger LOH issue.
            var targets = new Dictionary<uint, KeyValuePair<int, double>>(indicesCount / 2);
            var decoys = new Dictionary<uint, KeyValuePair<int, double>>(indicesCount / 2);

            for (int ii = 0; ii < indicesCount; ii++)
            {
                int idx = indices[ii];
                uint baseId = entryIds[idx] & BASE_ID_MASK;
                double s = scores[idx];
                if (labels[idx])
                {
                    KeyValuePair<int, double> existing;
                    if (decoys.TryGetValue(baseId, out existing))
                    {
                        if (s > existing.Value)
                            decoys[baseId] = new KeyValuePair<int, double>(idx, s);
                    }
                    else
                    {
                        decoys[baseId] = new KeyValuePair<int, double>(idx, s);
                    }
                }
                else
                {
                    KeyValuePair<int, double> existing;
                    if (targets.TryGetValue(baseId, out existing))
                    {
                        if (s > existing.Value)
                            targets[baseId] = new KeyValuePair<int, double>(idx, s);
                    }
                    else
                    {
                        targets[baseId] = new KeyValuePair<int, double>(idx, s);
                    }
                }
            }

            // Walk pairs into local struct array (parallel-array layout
            // avoids the per-element Tuple class allocation that the
            // public CompeteFromIndices pays).
            int maxWinners = targets.Count + decoys.Count;
            scratch.EnsureCountPassingCapacity(maxWinners);
            int[] winIdx = scratch.CompetitionWinnerIndices;
            double[] winScores = scratch.CompetitionWinnerScores;
            bool[] winDecoy = scratch.CompetitionWinnerIsDecoy;
            // baseIds for tie-break ordering; reuse CountPassingIndices
            // as a uint[] surrogate (interpret bits). Cleaner: small
            // separate buffer; for now allocate per-call (small).
            var winBaseIds = new uint[maxWinners];

            int n = 0;
            foreach (var kvp in targets)
            {
                uint baseId = kvp.Key;
                int tIdx = kvp.Value.Key;
                double tScore = kvp.Value.Value;
                KeyValuePair<int, double> de;
                if (decoys.TryGetValue(baseId, out de))
                {
                    if (tScore > de.Value)
                    { winIdx[n] = tIdx; winScores[n] = tScore; winDecoy[n] = false; winBaseIds[n] = baseId; n++; }
                    else
                    { winIdx[n] = de.Key; winScores[n] = de.Value; winDecoy[n] = true; winBaseIds[n] = baseId; n++; }
                }
                else
                {
                    winIdx[n] = tIdx; winScores[n] = tScore; winDecoy[n] = false; winBaseIds[n] = baseId; n++;
                }
            }
            foreach (var kvp in decoys)
            {
                if (!targets.ContainsKey(kvp.Key))
                {
                    winIdx[n] = kvp.Value.Key; winScores[n] = kvp.Value.Value;
                    winDecoy[n] = true; winBaseIds[n] = kvp.Key; n++;
                }
            }

            // Sort: score desc, then baseId asc. Build index permutation
            // then permute the parallel arrays. Sorting an int[] of
            // length n with a comparison delegate beats the previous
            // List<Tuple<...>>.Sort because no per-element boxing was
            // required to populate the list.
            var perm = new int[n];
            for (int i = 0; i < n; i++) perm[i] = i;
            // The tie-break key (winBaseIds) is unique per row -- post-deduplication
            // best-per-precursor selection above guarantees one row per (base_id, isDecoy)
            // tuple -- so the comparator never returns 0 for distinct rows and introsort's
            // instability is moot. Exemption comment must be on the Array.Sort line itself
            // for the regex in CodeInspectionTest.TestNoUnstableArraySort to recognize it.
            Array.Sort(perm, (a, b) => // Array.Sort OK: unique baseId tie-break makes comparator total
            {
                int cmp = winScores[b].CompareTo(winScores[a]);
                if (cmp != 0) return cmp;
                return winBaseIds[a].CompareTo(winBaseIds[b]);
            });

            // Apply permutation in-place via scratch swap arrays. Reuse
            // the still-spare prefix of CountPassingQvalues as a double
            // swap buffer; for int and bool we need small temp arrays.
            var tmpIdx = new int[n];
            var tmpScores = new double[n];
            var tmpDecoy = new bool[n];
            for (int i = 0; i < n; i++)
            {
                tmpIdx[i] = winIdx[perm[i]];
                tmpScores[i] = winScores[perm[i]];
                tmpDecoy[i] = winDecoy[perm[i]];
            }
            Array.Copy(tmpIdx, winIdx, n);
            Array.Copy(tmpScores, winScores, n);
            Array.Copy(tmpDecoy, winDecoy, n);
            return n;
        }

        /// <summary>
        /// Variant of <see cref="ComputeQvalues"/> that operates on the
        /// active prefix [0..n) of pre-allocated arrays.
        /// </summary>
        private static void ComputeQvaluesInto(
            double[] scores, bool[] isDecoy, double[] qValuesOut, int n)
        {
            ComputeQvaluesCore(isDecoy, qValuesOut, n, decoyOffset: 0);
        }

        /// <summary>
        /// Shared core behind <see cref="ComputeConservativeQvalues"/>,
        /// <see cref="ComputeQvalues"/>, and <see cref="ComputeQvaluesInto"/>.
        /// Walks the score-descending prefix [0..<paramref name="n"/>)
        /// accumulating target / decoy counts, writes
        /// FDR = (nDecoy + <paramref name="decoyOffset"/>) / nTarget at each rank,
        /// then enforces a monotone-non-increasing q-value with a backward pass.
        /// <paramref name="decoyOffset"/> is 1 for the conservative (Savitski +1)
        /// estimate and 0 for the plain ratio. Scores are not read -- the input is
        /// assumed already sorted by score descending.
        /// </summary>
        private static void ComputeQvaluesCore(
            bool[] isDecoy, double[] qValues, int n, int decoyOffset)
        {
            int nTarget = 0;
            int nDecoy = 0;
            for (int i = 0; i < n; i++)
            {
                if (isDecoy[i])
                    nDecoy++;
                else
                    nTarget++;
                qValues[i] = nTarget > 0 ? (double)(nDecoy + decoyOffset) / nTarget : 1.0;
            }

            double qMin = 1.0;
            for (int i = n - 1; i >= 0; i--)
            {
                qMin = Math.Min(qMin, qValues[i]);
                qValues[i] = qMin;
            }
        }

        /// <summary>
        /// Count targets passing FDR threshold using conservative formula.
        /// </summary>
        public static int CountPassingConservative(
            double[] scores, bool[] labels, uint[] entryIds, double fdrThreshold)
        {
            var allIndices = new int[scores.Length];
            for (int i = 0; i < scores.Length; i++)
                allIndices[i] = i;

            int[] wi;
            double[] ws;
            bool[] wd;
            CompeteFromIndices(scores, labels, entryIds, allIndices, out wi, out ws, out wd);

            var qValues = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, qValues);

            int count = 0;
            for (int rank = 0; rank < wi.Length; rank++)
            {
                if (!labels[wi[rank]] && qValues[rank] <= fdrThreshold)
                    count++;
            }
            return count;
        }

        // ============================================================
        // Positive training set selection
        // ============================================================

        private static int[] SelectPositiveTrainingSet(
            double[] scores, bool[] labels, uint[] entryIds,
            double fdrThreshold, int minTargets)
        {
            int[] wi;
            double[] ws;
            bool[] wd;
            var allIndices = new int[scores.Length];
            for (int i = 0; i < scores.Length; i++)
                allIndices[i] = i;
            CompeteFromIndices(scores, labels, entryIds, allIndices, out wi, out ws, out wd);

            var qValues = new double[wi.Length];
            if (wi.Length > 0)
                ComputeQvalues(ws, wd, qValues);

            Func<double, int[]> selectAtThreshold = threshold =>
            {
                var sel = new List<int>();
                for (int rank = 0; rank < wi.Length; rank++)
                {
                    if (!labels[wi[rank]] && qValues[rank] <= threshold)
                        sel.Add(wi[rank]);
                }
                return sel.ToArray();
            };

            var selected = selectAtThreshold(fdrThreshold);

            if (selected.Length < minTargets)
            {
                double[] thresholds = { 0.05, 0.10, 0.25, 0.50 };
                foreach (double threshold in thresholds)
                {
                    selected = selectAtThreshold(threshold);
                    if (selected.Length >= minTargets)
                        break;
                }
            }

            return selected;
        }

        // ============================================================
        // Best initial feature
        // ============================================================

        private static void FindBestInitialFeature(
            Matrix features, bool[] labels, uint[] entryIds, double fdrThreshold,
            out int bestIdx, out int bestPassing)
        {
            int n = features.Rows;
            int p = features.Cols;
            bestIdx = 0;
            bestPassing = 0;

            for (int feat = 0; feat < p; feat++)
            {
                var scores = new double[n];
                for (int i = 0; i < n; i++)
                    scores[i] = features[i, feat];
                int nPass = CountPassing(scores, labels, entryIds, fdrThreshold);
                if (nPass > bestPassing)
                {
                    bestPassing = nPass;
                    bestIdx = feat;
                }
            }
        }

        // ============================================================
        // Grid search for SVM C parameter
        // ============================================================

        private static double GridSearchC(
            Matrix features, bool[] labels, uint[] entryIds,
            double[] cValues, int[] foldAssignments, int nFolds,
            ulong seed, double fdrThreshold,
            SvmTrainScratchPool svmScratchPool)
        {
            // Evaluate each candidate C in parallel. Mirrors Rust's
            // c_values.par_iter() in osprey-ml/src/svm.rs::grid_search_c.
            // Each C is independent (no shared mutable state during
            // training); the per-C totalPassing is stored by index so
            // the tie-break below is deterministic. OspreyParallel.For
            // (explicit threads) replaces TPL Parallel.For for the same
            // reason as the outer loop above.
            var totalPassingByC = new int[cValues.Length];
            OspreyParallel.For(0, cValues.Length, cValues.Length, ci =>
            {
                // Rent one scratch per parallel c-value; reused across
                // the inner sequential nFolds Train calls. Returned at
                // end of this parallel body.
                var localScratch = svmScratchPool != null ? svmScratchPool.Rent() : null;
                // Ensure ExtractRowsInto buffers can hold the larger of
                // train/test sizes (= labels.Length, the parent set,
                // which is the upper bound on either subset).
                if (localScratch != null)
                    localScratch.EnsureExtractCapacity(labels.Length, features.Cols);
                try {
                double c = cValues[ci];
                int totalPassing = 0;
                for (int fold = 0; fold < nFolds; fold++)
                {
                    var trainIdx = new List<int>();
                    var testIdx = new List<int>();
                    for (int i = 0; i < labels.Length; i++)
                    {
                        if (foldAssignments[i] == fold)
                            testIdx.Add(i);
                        else
                            trainIdx.Add(i);
                    }

                    if (trainIdx.Count == 0 || testIdx.Count == 0)
                        continue;

                    Matrix trainFeatures, testFeatures;
                    if (localScratch != null)
                    {
                        trainFeatures = MatrixRows.ExtractRowsInto(features, trainIdx.ToArray(), localScratch.TrainData);
                        testFeatures = MatrixRows.ExtractRowsInto(features, testIdx.ToArray(), localScratch.TestData);
                    }
                    else
                    {
                        trainFeatures = MatrixRows.ExtractRows(features, trainIdx.ToArray());
                        testFeatures = MatrixRows.ExtractRows(features, testIdx.ToArray());
                    }
                    var trainLabels = new bool[trainIdx.Count];
                    for (int i = 0; i < trainIdx.Count; i++)
                        trainLabels[i] = labels[trainIdx[i]];

                    var model = LinearSvmClassifier.Train(trainFeatures, trainLabels, c, seed, localScratch);
                    var testScores = model.DecisionFunction(testFeatures);
                    var testLabels = new bool[testIdx.Count];
                    var testEntryIds = new uint[testIdx.Count];
                    for (int i = 0; i < testIdx.Count; i++)
                    {
                        testLabels[i] = labels[testIdx[i]];
                        testEntryIds[i] = entryIds[testIdx[i]];
                    }

                    totalPassing += CountPassing(testScores, testLabels, testEntryIds, fdrThreshold, localScratch);
                }
                totalPassingByC[ci] = totalPassing;
                } finally {
                    if (localScratch != null && svmScratchPool != null)
                        svmScratchPool.Return(localScratch);
                }
            });

            // Tie-break: first index with the maximum totalPassing wins,
            // matching the strict `>` semantics of the prior serial loop
            // and the corresponding Rust path.
            double bestC = cValues[0];
            int bestTotal = totalPassingByC[0];
            for (int ci = 1; ci < cValues.Length; ci++)
            {
                if (totalPassingByC[ci] > bestTotal)
                {
                    bestTotal = totalPassingByC[ci];
                    bestC = cValues[ci];
                }
            }
            return bestC;
        }

        // ============================================================
        // Score calibration between CV folds (Granholm et al. 2012)
        // ============================================================

        private static void CalibrateScoresBetweenFolds(
            double[] finalScores, int[] foldAssignments,
            bool[] labels, uint[] entryIds,
            int nFolds, double fdrThreshold)
        {
            for (int fold = 0; fold < nFolds; fold++)
            {
                var testIndices = new List<int>();
                for (int i = 0; i < finalScores.Length; i++)
                {
                    if (foldAssignments[i] == fold)
                        testIndices.Add(i);
                }

                if (testIndices.Count == 0)
                    continue;

                var testScores = new double[testIndices.Count];
                var testLabels = new bool[testIndices.Count];
                var testEids = new uint[testIndices.Count];
                for (int i = 0; i < testIndices.Count; i++)
                {
                    testScores[i] = finalScores[testIndices[i]];
                    testLabels[i] = labels[testIndices[i]];
                    testEids[i] = entryIds[testIndices[i]];
                }

                double thresholdScore;
                if (!FindScoreAtFdr(testScores, testLabels, testEids, fdrThreshold, out thresholdScore))
                    continue;

                // Find median decoy score
                var decoyScores = new List<double>();
                foreach (int idx in testIndices)
                {
                    if (labels[idx])
                        decoyScores.Add(finalScores[idx]);
                }
                decoyScores.Sort(); // Array.Sort OK: median of single primitive list, no parallel data; tie order irrelevant

                double medianDecoy = decoyScores.Count > 0
                    ? decoyScores[decoyScores.Count / 2]
                    : thresholdScore - 1.0;

                double denom = thresholdScore - medianDecoy;
                if (denom <= 0.0)
                    continue;

                foreach (int idx in testIndices)
                    finalScores[idx] = (finalScores[idx] - thresholdScore) / denom;
            }
        }

        private static bool FindScoreAtFdr(
            double[] scores, bool[] labels, uint[] entryIds,
            double fdrThreshold, out double thresholdScore)
        {
            thresholdScore = 0.0;
            var allIndices = new int[scores.Length];
            for (int i = 0; i < scores.Length; i++)
                allIndices[i] = i;

            int[] wi;
            double[] ws;
            bool[] wd;
            CompeteFromIndices(scores, labels, entryIds, allIndices, out wi, out ws, out wd);

            if (wi.Length == 0)
                return false;

            var qValues = new double[wi.Length];
            ComputeQvalues(ws, wd, qValues);

            bool found = false;
            double minPassingScore = double.MaxValue;
            for (int rank = 0; rank < wi.Length; rank++)
            {
                if (!labels[wi[rank]] && qValues[rank] <= fdrThreshold)
                {
                    if (scores[wi[rank]] < minPassingScore)
                    {
                        minPassingScore = scores[wi[rank]];
                        found = true;
                    }
                }
            }

            if (found)
                thresholdScore = minPassingScore;
            return found;
        }

        // ============================================================
        // Per-run and experiment-level q-value computation
        // ============================================================

        /// <summary>
        /// One file's per-run PRECURSOR q-values. Competes the file's rows -- the contiguous
        /// global index range in <paramref name="indices"/> (<c>[off, off+count)</c>) -- directly
        /// over the global score-pass arrays (no per-file slice copy; issue #4355 Part B), then
        /// maps each winning global index back to its local offset. Byte-identical to the per-file
        /// group body of <see cref="ComputePerRunPrecursorQvalues"/>: winners get their q, every
        /// other row stays 1.0. Returns a local array indexed 0..count-1 (the caller scatters it back).
        /// </summary>
        private static double[] ComputePerRunPrecursorQvaluesForFile(
            double[] scores, bool[] labels, uint[] entryIds, int[] indices, int off)
        {
            int count = indices.Length;
            var qvalues = new double[count];
            for (int i = 0; i < count; i++)
                qvalues[i] = 1.0;

            int[] wi;
            double[] ws;
            bool[] wd;
            CompeteFromIndices(scores, labels, entryIds, indices, out wi, out ws, out wd);

            var q = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, q);
            for (int rank = 0; rank < wi.Length; rank++)
                qvalues[wi[rank] - off] = q[rank];   // wi[rank] is a global index in [off, off+count)
            return qvalues;
        }

        /// <summary>
        /// One file's per-run PEPTIDE q-values, competing directly over the global arrays via the
        /// file's contiguous global index range <paramref name="indices"/> (<c>[off, off+count)</c>)
        /// -- no per-file slice copy (issue #4355 Part B). Byte-identical to the per-file group body
        /// of <see cref="ComputePerRunPeptideQvalues"/>: best-per-peptide over the file, competition,
        /// then the peptide's q propagated to every row of that peptide (others stay 1.0).
        /// <see cref="PercolatorSampling.BestPrecursorPerPeptide"/> returns global indices, which
        /// <see cref="CompeteFromIndices"/> then competes directly (both take an index subset).
        /// Returns a local array indexed 0..count-1.
        /// </summary>
        private static double[] ComputePerRunPeptideQvaluesForFile(
            double[] scores, bool[] labels, uint[] entryIds, string[] peptides, int[] indices, int off)
        {
            int count = indices.Length;
            var qvalues = new double[count];
            for (int i = 0; i < count; i++)
                qvalues[i] = 1.0;

            var bestPerPeptide = PercolatorSampling.BestPrecursorPerPeptide(indices, scores, labels, peptides);

            int[] wi;
            double[] ws;
            bool[] wd;
            CompeteFromIndices(scores, labels, entryIds, bestPerPeptide, out wi, out ws, out wd);

            var q = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, q);

            var peptideQvalue = new Dictionary<string, double>();
            for (int rank = 0; rank < wi.Length; rank++)
                peptideQvalue[peptides[wi[rank]]] = q[rank];   // wi[rank] is a global index

            for (int r = 0; r < count; r++)
            {
                double qv;
                if (peptideQvalue.TryGetValue(peptides[off + r], out qv))
                    qvalues[r] = qv;
            }
            return qvalues;
        }

        /// <summary>
        /// Computes one file's per-run precursor + peptide q-values from its contiguous slice
        /// <c>[off, off+count)</c> of the flat score-pass arrays (nested (file, row) order, so a
        /// file's rows are contiguous). Used by the projection score pass in place of the full
        /// double[n] per-run arrays (issue #4355 Part B); bounded to one file.
        /// </summary>
        internal static void ComputePerFileRunQvalues(
            double[] scores, bool[] labels, uint[] entryIds, string[] peptides,
            int off, int count,
            out double[] runPrecursorQvalues, out double[] runPeptideQvalues)
        {
            // A file's rows are the contiguous global range [off, off+count); compete directly over
            // the global arrays through this index buffer instead of copying four per-file slices
            // (issue #4355 Part B, Copilot review). One int[count] shared by both per-run passes.
            var indices = new int[count];
            for (int r = 0; r < count; r++)
                indices[r] = off + r;
            runPrecursorQvalues = ComputePerRunPrecursorQvaluesForFile(scores, labels, entryIds, indices, off);
            runPeptideQvalues = ComputePerRunPeptideQvaluesForFile(scores, labels, entryIds, peptides, indices, off);
        }

        private static double[] ComputePerRunPrecursorQvalues(
            double[] scores, bool[] labels, uint[] entryIds, string[] fileNames)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            for (int i = 0; i < n; i++)
                qvalues[i] = 1.0;

            var fileGroups = new Dictionary<string, List<int>>();
            for (int i = 0; i < n; i++)
            {
                List<int> list;
                if (!fileGroups.TryGetValue(fileNames[i], out list))
                {
                    list = new List<int>();
                    fileGroups[fileNames[i]] = list;
                }
                list.Add(i);
            }

            var progress = QProgress(@"Per-run precursor q-values", fileGroups.Count, n);
            int fileDone = 0;
            foreach (var group in fileGroups.Values)
            {
                progress?.Report(++fileDone);
                var fileScores = new double[group.Count];
                var fileLabels = new bool[group.Count];
                var fileEntryIds = new uint[group.Count];
                var allIndices = new int[group.Count];
                for (int i = 0; i < group.Count; i++)
                {
                    fileScores[i] = scores[group[i]];
                    fileLabels[i] = labels[group[i]];
                    fileEntryIds[i] = entryIds[group[i]];
                    allIndices[i] = i;
                }

                int[] wi;
                double[] ws;
                bool[] wd;
                CompeteFromIndices(fileScores, fileLabels, fileEntryIds, allIndices,
                    out wi, out ws, out wd);

                var q = new double[wi.Length];
                ComputeConservativeQvalues(ws, wd, q);

                for (int rank = 0; rank < wi.Length; rank++)
                {
                    int globalIdx = group[wi[rank]];
                    qvalues[globalIdx] = q[rank];
                }
            }
            progress?.Dispose();

            return qvalues;
        }

        /// <summary>
        /// STRATIFIED target-decoy competition q-values (OSPREY_PASS2_QVALUE=protein-compact):
        /// compete + compute q over ONLY the observations whose <c>base_id</c>
        /// (<c>entry_id &amp; 0x7FFFFFFF</c>) is in <paramref name="stratumBaseIds"/> --
        /// the peptides of proteins detected in the 1st pass, admitted as target+decoy
        /// PAIRS. Off-stratum observations get q = 1.0 (not reported).
        ///
        /// The sensitivity comes from reduced multiple testing: removing off-stratum
        /// (mostly-false) peptides removes their decoys from the null, so the decoy count
        /// above a given score drops and q falls for the stratum's marginal targets
        /// (independent filtering; Bourgon 2010). It stays honest because (a) the stratum
        /// is defined by protein membership, ~independent of a peptide's own decoy score
        /// under the null since a protein is detected via its OTHER peptides, and (b) the
        /// stratum keeps its paired decoys, including the ones that win -- so the null is a
        /// fair sample, not a target-winner-selected one (the failure mode of the old
        /// two-pass compaction). Uses the same conservative competition + q the
        /// full-population path uses; only the participating index set is constrained.
        /// </summary>
        internal static double[] ComputeStratifiedCompetitionQvalues(
            double[] scores, bool[] labels, uint[] entryIds, HashSet<uint> stratumBaseIds)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            for (int i = 0; i < n; i++)
                qvalues[i] = 1.0;
            if (stratumBaseIds == null || stratumBaseIds.Count == 0)
                return qvalues;

            // Indices whose base_id is in the stratum -- target and decoy alike, so the
            // pair-symmetric null is preserved.
            var stratIdx = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (stratumBaseIds.Contains(entryIds[i] & BASE_ID_MASK))
                    stratIdx.Add(i);
            }
            if (stratIdx.Count == 0)
                return qvalues;

            var sScores = new double[stratIdx.Count];
            var sLabels = new bool[stratIdx.Count];
            var sEntryIds = new uint[stratIdx.Count];
            var allIndices = new int[stratIdx.Count];
            for (int i = 0; i < stratIdx.Count; i++)
            {
                sScores[i] = scores[stratIdx[i]];
                sLabels[i] = labels[stratIdx[i]];
                sEntryIds[i] = entryIds[stratIdx[i]];
                allIndices[i] = i;
            }

            int[] wi;
            double[] ws;
            bool[] wd;
            CompeteFromIndices(sScores, sLabels, sEntryIds, allIndices, out wi, out ws, out wd);

            var q = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, q);

            for (int rank = 0; rank < wi.Length; rank++)
                qvalues[stratIdx[wi[rank]]] = q[rank];

            return qvalues;
        }

        private static double[] ComputePerRunPeptideQvalues(
            double[] scores, bool[] labels, uint[] entryIds,
            string[] fileNames, string[] peptides)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            for (int i = 0; i < n; i++)
                qvalues[i] = 1.0;

            var fileGroups = new Dictionary<string, List<int>>();
            for (int i = 0; i < n; i++)
            {
                List<int> list;
                if (!fileGroups.TryGetValue(fileNames[i], out list))
                {
                    list = new List<int>();
                    fileGroups[fileNames[i]] = list;
                }
                list.Add(i);
            }

            var progress = QProgress(@"Per-run peptide q-values", fileGroups.Count, n);
            int fileDone = 0;
            foreach (var group in fileGroups.Values)
            {
                progress?.Report(++fileDone);
                var bestPerPeptide = PercolatorSampling.BestPrecursorPerPeptide(
                    group.ToArray(), scores, labels, peptides);

                var peptScores = new double[bestPerPeptide.Length];
                var peptLabels = new bool[bestPerPeptide.Length];
                var peptEntryIds = new uint[bestPerPeptide.Length];
                var allIndices = new int[bestPerPeptide.Length];
                for (int i = 0; i < bestPerPeptide.Length; i++)
                {
                    peptScores[i] = scores[bestPerPeptide[i]];
                    peptLabels[i] = labels[bestPerPeptide[i]];
                    peptEntryIds[i] = entryIds[bestPerPeptide[i]];
                    allIndices[i] = i;
                }

                int[] wi;
                double[] ws;
                bool[] wd;
                CompeteFromIndices(peptScores, peptLabels, peptEntryIds, allIndices,
                    out wi, out ws, out wd);

                var q = new double[wi.Length];
                ComputeConservativeQvalues(ws, wd, q);

                var peptideQvalue = new Dictionary<string, double>();
                for (int rank = 0; rank < wi.Length; rank++)
                {
                    int globalIdx = bestPerPeptide[wi[rank]];
                    peptideQvalue[peptides[globalIdx]] = q[rank];
                }

                foreach (int idx in group)
                {
                    double qv;
                    if (peptideQvalue.TryGetValue(peptides[idx], out qv))
                        qvalues[idx] = qv;
                }
            }
            progress?.Dispose();

            return qvalues;
        }

        /// <summary>
        /// Bounded (O(base_ids)) experiment-precursor q map: <c>base_id -&gt; q</c>. This is
        /// the intrinsic working set of the experiment-precursor competition -- one q per
        /// distinct base_id -- so it is what the projection score pass
        /// (<see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>) reads to assign each row's
        /// experiment-precursor q WITHOUT ever materializing the O(n) per-row array
        /// (issue #4355 Part B, bounded q-value reconstruction). The full-length
        /// <see cref="ComputeExperimentPrecursorQvalues"/> wrapper simply expands this map,
        /// so the two share the SAME competition + conservative-q math and cannot drift.
        /// </summary>
        internal static Dictionary<uint, double> ComputeExperimentPrecursorQMap(
            double[] scores, bool[] labels, uint[] entryIds)
        {
            int n = scores.Length;
            int[] wi;
            double[] ws;
            bool[] wd;
            using (var progress = QProgress(@"Experiment precursor q-values", n, n))
                CompeteAll(scores, labels, entryIds, out wi, out ws, out wd, progress);

            var q = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, q);

            // Winner's q-value keyed by base_id -- assigned to all observations sharing the
            // same base_id (both target and decoy sides) at expand/assign time. Matches
            // Rust's base_id_exp_prec_q HashMap at osprey-fdr/src/percolator.rs:2168 --
            // without this, non-winning per-file observations of a multi-file precursor stay
            // at q=1.0 and downstream stages that gate on experiment_precursor_qvalue (Stage
            // 6 calibration refit and reconciliation) miss the bulk of the consensus pool.
            var baseIdExpQ = new Dictionary<uint, double>();
            for (int rank = 0; rank < wi.Length; rank++)
            {
                uint baseId = entryIds[wi[rank]] & BASE_ID_MASK;
                baseIdExpQ[baseId] = q[rank];
            }
            return baseIdExpQ;
        }

        private static double[] ComputeExperimentPrecursorQvalues(
            double[] scores, bool[] labels, uint[] entryIds)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            var baseIdExpQ = ComputeExperimentPrecursorQMap(scores, labels, entryIds);
            for (int i = 0; i < n; i++)
            {
                double qv;
                qvalues[i] = baseIdExpQ.TryGetValue(entryIds[i] & BASE_ID_MASK, out qv) ? qv : 1.0;
            }
            return qvalues;
        }

        /// <summary>
        /// Bounded (O(peptides)) experiment-peptide q map: <c>peptide -&gt; q</c>. The
        /// intrinsic working set of the experiment-peptide competition -- one q per distinct
        /// peptide string -- which the projection score pass
        /// (<see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>) reads to assign each row's
        /// experiment-peptide q without materializing the O(n) per-row array (issue #4355
        /// Part B). The full-length <see cref="ComputeExperimentPeptideQvalues"/> wrapper
        /// expands this map, so both share the SAME best-per-peptide + competition +
        /// conservative-q math and cannot drift.
        /// </summary>
        internal static Dictionary<string, double> ComputeExperimentPeptideQMap(
            double[] scores, bool[] labels, uint[] entryIds, string[] peptides)
        {
            int n = scores.Length;
            var allIndices = new int[n];
            for (int i = 0; i < n; i++)
                allIndices[i] = i;

            var bestPerPeptide = PercolatorSampling.BestPrecursorPerPeptide(allIndices, scores, labels, peptides);

            var peptScores = new double[bestPerPeptide.Length];
            var peptLabels = new bool[bestPerPeptide.Length];
            var peptEntryIds = new uint[bestPerPeptide.Length];
            var allPeptIndices = new int[bestPerPeptide.Length];
            for (int i = 0; i < bestPerPeptide.Length; i++)
            {
                peptScores[i] = scores[bestPerPeptide[i]];
                peptLabels[i] = labels[bestPerPeptide[i]];
                peptEntryIds[i] = entryIds[bestPerPeptide[i]];
                allPeptIndices[i] = i;
            }

            int[] wi;
            double[] ws;
            bool[] wd;
            using (var progress = QProgress(@"Experiment peptide q-values", bestPerPeptide.Length, bestPerPeptide.Length))
                CompeteFromIndices(peptScores, peptLabels, peptEntryIds, allPeptIndices,
                    out wi, out ws, out wd, progress);

            var q = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, q);

            var peptideQvalue = new Dictionary<string, double>();
            for (int rank = 0; rank < wi.Length; rank++)
            {
                int globalIdx = bestPerPeptide[wi[rank]];
                peptideQvalue[peptides[globalIdx]] = q[rank];
            }
            return peptideQvalue;
        }

        private static double[] ComputeExperimentPeptideQvalues(
            double[] scores, bool[] labels, uint[] entryIds, string[] peptides)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            var peptideQvalue = ComputeExperimentPeptideQMap(scores, labels, entryIds, peptides);
            for (int i = 0; i < n; i++)
            {
                double qv;
                qvalues[i] = peptideQvalue.TryGetValue(peptides[i], out qv) ? qv : 1.0;
            }
            return qvalues;
        }

        /// <summary>
        /// Streaming builder for the three GLOBAL bounded first-pass q maps (issue #4355
        /// struct-shrink S3, Stage B): the experiment-precursor <c>base_id -&gt; q</c> map, the
        /// experiment-peptide <c>peptide -&gt; q</c> map, and the PEP <c>winner-ordinal -&gt; pep</c>
        /// map -- built by pushing each scored row via <see cref="Add"/> in flat (file,row) order
        /// instead of reading the resident <c>finalScores/labels/entryIds/peptides[n]</c> arrays.
        /// Bounded: it retains only per-base_id and per-peptide bests (O(distinct)), never an O(n)
        /// buffer. Each Build* reuses the SAME <see cref="CompeteFromDicts"/> +
        /// <see cref="ComputeConservativeQvalues"/> (+ <c>PepEstimator</c>) finish the flat
        /// <see cref="ComputeExperimentPrecursorQMap"/> / <see cref="ComputeExperimentPeptideQMap"/>
        /// / <see cref="ComputePepWinnerMap"/> run, so a population fed in the same order yields
        /// byte-identical maps (verified by <c>FdrTest.TestStreamingFirstPassQMatchesFlat</c>). The
        /// PEP map is keyed by the streaming ordinal <c>g</c>, which equals the flat winner index
        /// because both visit rows in the same nested (file,row) order.
        /// </summary>
        internal sealed class StreamingFirstPassQ
        {
            // Global experiment-precursor / PEP competition: base_id -> best (g, score), strict
            // '>' first-seen, split target/decoy -- the identical maps CompeteAll builds.
            private readonly Dictionary<uint, KeyValuePair<int, double>> _precTargets =
                new Dictionary<uint, KeyValuePair<int, double>>();
            private readonly Dictionary<uint, KeyValuePair<int, double>> _precDecoys =
                new Dictionary<uint, KeyValuePair<int, double>>();
            // Experiment-peptide: peptide -> best row, mirroring BestPrecursorPerPeptide.
            private readonly Dictionary<string, PeptideBest> _peptBest =
                new Dictionary<string, PeptideBest>();

            /// <summary>Fold one scored row (in flat (file,row) order) into the bounded bests.</summary>
            public void Add(int g, double score, uint entryId, bool isDecoy, string peptide)
            {
                uint baseId = entryId & BASE_ID_MASK;
                var dict = isDecoy ? _precDecoys : _precTargets;
                KeyValuePair<int, double> existing;
                if (dict.TryGetValue(baseId, out existing))
                {
                    if (score > existing.Value)
                        dict[baseId] = new KeyValuePair<int, double>(g, score);
                }
                else
                {
                    dict[baseId] = new KeyValuePair<int, double>(g, score);
                }

                PeptideBest pb;
                if (_peptBest.TryGetValue(peptide, out pb))
                {
                    if (score > pb.Score)
                        _peptBest[peptide] = new PeptideBest(g, score, isDecoy, entryId, peptide);
                }
                else
                {
                    _peptBest[peptide] = new PeptideBest(g, score, isDecoy, entryId, peptide);
                }
            }

            /// <summary>
            /// Experiment-precursor <c>base_id -&gt; q</c>: compete the global base_id bests,
            /// conservative-q, keyed by each winner's base_id -- byte-identical to
            /// <see cref="ComputeExperimentPrecursorQMap"/>.
            /// </summary>
            public Dictionary<uint, double> BuildExperimentPrecursorQMap()
            {
                CompeteFromDicts(_precTargets, _precDecoys,
                    out _, out double[] ws, out bool[] wd, out uint[] wb);
                var q = new double[ws.Length];
                ComputeConservativeQvalues(ws, wd, q);
                var map = new Dictionary<uint, double>(wb.Length);
                for (int rank = 0; rank < wb.Length; rank++)
                    map[wb[rank]] = q[rank];
                return map;
            }

            /// <summary>
            /// Experiment-peptide <c>peptide -&gt; q</c>: materialize the best-per-peptide set
            /// sorted by ordinal (matching <see cref="PercolatorSampling.BestPrecursorPerPeptide"/>'s sort), compete
            /// by base_id, conservative-q, keyed by the winner's peptide -- byte-identical to
            /// <see cref="ComputeExperimentPeptideQMap"/>.
            /// </summary>
            public Dictionary<string, double> BuildExperimentPeptideQMap()
            {
                var best = new List<PeptideBest>(_peptBest.Values);
                best.Sort((a, b) => a.G.CompareTo(b.G)); // Array.Sort OK: G is the unique streaming ordinal of each peptide's best row, so the comparator never ties -- reproduces BestPrecursorPerPeptide's result.Sort() on ascending global index
                var targets = new Dictionary<uint, KeyValuePair<int, double>>();
                var decoys = new Dictionary<uint, KeyValuePair<int, double>>();
                for (int i = 0; i < best.Count; i++)
                {
                    uint baseId = best[i].EntryId & BASE_ID_MASK;
                    var dict = best[i].IsDecoy ? decoys : targets;
                    KeyValuePair<int, double> existing;
                    if (dict.TryGetValue(baseId, out existing))
                    {
                        if (best[i].Score > existing.Value)
                            dict[baseId] = new KeyValuePair<int, double>(i, best[i].Score);
                    }
                    else
                    {
                        dict[baseId] = new KeyValuePair<int, double>(i, best[i].Score);
                    }
                }
                CompeteFromDicts(targets, decoys,
                    out int[] wi, out double[] ws, out bool[] wd, out _);
                var q = new double[ws.Length];
                ComputeConservativeQvalues(ws, wd, q);
                var map = new Dictionary<string, double>(wi.Length);
                for (int rank = 0; rank < wi.Length; rank++)
                    map[best[wi[rank]].Peptide] = q[rank];
                return map;
            }

            /// <summary>
            /// PEP <c>winner-ordinal -&gt; pep</c>: compete the global base_id bests, fit the PEP
            /// estimator on winners sorted base_id-ascending (the non-associative KDE sum is
            /// order-sensitive), then posterior-error each winner -- byte-identical to
            /// <see cref="ComputePepWinnerMap"/>.
            /// </summary>
            public Dictionary<int, double> BuildPepWinnerMap()
            {
                CompeteFromDicts(_precTargets, _precDecoys,
                    out int[] wi, out double[] ws, out bool[] wd, out uint[] wb);
                int nWinners = wi.Length;
                var pepOrder = new int[nWinners];
                for (int k = 0; k < nWinners; k++)
                    pepOrder[k] = k;
                Array.Sort(pepOrder, (a, b) => wb[a].CompareTo(wb[b])); // Array.Sort OK: one winner per base_id, so wb has no ties -- matches ComputePepWinnerMap
                var pepScores = new double[nWinners];
                var pepIsDecoy = new bool[nWinners];
                for (int k = 0; k < nWinners; k++)
                {
                    pepScores[k] = ws[pepOrder[k]];
                    pepIsDecoy[k] = wd[pepOrder[k]];
                }
                var pepEstimator = PepEstimator.FitDefault(pepScores, pepIsDecoy);
                var map = new Dictionary<int, double>(nWinners);
                for (int k = 0; k < nWinners; k++)
                    map[wi[k]] = pepEstimator.PosteriorError(ws[k]);
                return map;
            }

            private readonly struct PeptideBest
            {
                public readonly int G;
                public readonly double Score;
                public readonly bool IsDecoy;
                public readonly uint EntryId;
                public readonly string Peptide;

                public PeptideBest(int g, double score, bool isDecoy, uint entryId, string peptide)
                {
                    G = g;
                    Score = score;
                    IsDecoy = isDecoy;
                    EntryId = entryId;
                    Peptide = peptide;
                }
            }
        }

        // A console progress reporter for the large first-pass q-value / competition passes,
        // or null (no output) when the population is small enough that the pass is sub-second
        // -- keeps unit tests / Stellar clutter-free. Console-only; never affects the q-values.
        internal static ProgressReporter QProgress(string activity, long reportTotal, long workSize)
        {
            return workSize > 2_000_000 ? new ProgressReporter(activity, reportTotal) : null;
        }

        /// <summary>
        /// Format a single SVM cost C for the console using the invariant culture
        /// (a numeric value, not localizable text), with the general "R" round-trip
        /// so grid values like 0.001 / 100 print exactly rather than as 1E-03.
        /// </summary>
        private static string FormatC(double c)
        {
            return c.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format the log-scale C sweep grid as "{a, b, c}" for the console header.
        /// </summary>
        private static string FormatCGrid(double[] cValues)
        {
            if (cValues == null || cValues.Length == 0)
                return "{}";
            var parts = new string[cValues.Length];
            for (int i = 0; i < cValues.Length; i++)
                parts[i] = FormatC(cValues[i]);
            return "{" + string.Join(", ", parts) + "}";
        }

    }
}
