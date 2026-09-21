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
using System.Globalization;
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Semi-supervised training of the Percolator model, extracted from
    /// the original <c>PercolatorFdr</c> god class (issue #4468): the top-level <see cref="RunPercolator"/>
    /// orchestration, per-fold SVM and gradient-boosted-tree training, the positive
    /// training set selection each iteration re-derives, the cost grid search, and
    /// the Granholm cross-fold score calibration.
    ///
    /// This decides what the model IS. <see cref="PercolatorScorer"/> decides what it
    /// SAYS about the population, and training calls into it
    /// (<see cref="PercolatorScorer.ScoreWithFoldModel"/>) to score its own held-out
    /// folds. Fold membership comes from <see cref="PercolatorSampling"/>.
    /// </summary>
    public static class PercolatorTrainer
    {
        // Floor on the positive training set: below this many targets the
        // iteration keeps the previous set rather than collapsing.
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

            // Build subset-local arrays.
            //
            // trainSubset is never null: both of BuildTrainingSubset's return paths hand
            // back a materialized array. The former "no subsampling" branch here (clone the
            // full arrays, use stdFeatures directly) was therefore unreachable and has been
            // removed, along with the two other trainSubset null tests below (issue #4468).
            // Worth being explicit about the evidence: unreachable code is invisible to the
            // regression, so the golden proves nothing about this removal - it rests on
            // BuildTrainingSubset's return paths, not on a green gate.
            var subLabels = new bool[subN];
            var subEntryIds = new uint[subN];
            var subPeptides = new string[subN];
            for (int i = 0; i < subN; i++)
            {
                subLabels[i] = labels[trainSubset[i]];
                subEntryIds[i] = entryIds[trainSubset[i]];
                subPeptides[i] = peptides[trainSubset[i]];
            }
            var subFeatures = MatrixRows.ExtractRows(stdFeatures, trainSubset);

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

            // Fold models are trained into the caller-allocated arrays (exactly one
            // of foldModels / foldGbtModels is populated, per config).
            TrainFoldModels(config, subFeatures, subLabels, subEntryIds, subPeptides,
                foldAssignments, initialScores, subN, subTargets, nFeatures, trainFdr,
                foldModels, foldGbtModels, foldIterations);

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

            // Cross-validated scoring: every entry is scored by a model that did not
            // train on it. Writes finalScores in place.
            ScoreEntriesWithFoldModels(config, stdFeatures, trainSubset, foldAssignments,
                foldModels, foldGbtModels, n, subN, finalScores);

            for (int fold = 0; fold < config.NFolds; fold++)
            {
                if (foldGbtModels == null)
                {
                    foldWeights.Add(foldModels[fold].Weights);
                    foldBiases.Add(foldModels[fold].Bias);
                }
                iterationsPerFold.Add(foldIterations[fold]);
            }

            // 6b. Calibrate scores between folds. foldAssignments is indexed subset-locally,
            // so it always needs remapping to global indices (trainSubset is never null, see
            // above); the former unmapped branch was unreachable.
            var globalFoldAssignments = new int[n];
            for (int i = 0; i < n; i++)
                globalFoldAssignments[i] = int.MaxValue;
            for (int si = 0; si < trainSubset.Length; si++)
                globalFoldAssignments[trainSubset[si]] = foldAssignments[si];
            CalibrateScoresBetweenFolds(finalScores, globalFoldAssignments,
                labels, entryIds, config.NFolds, trainFdr);

            // 7. Compute PEP on competition winners
            int[] winnerIndices;
            double[] winnerScores;
            bool[] winnerIsDecoy;
            TargetDecoyCompetition.CompeteAll(finalScores, labels, entryIds,
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

            var runPrecursorQvalues = PercolatorQValues.ComputePerRunPrecursorQvalues(
                finalScores, labels, entryIds, fileNames);
            var runPeptideQvalues = PercolatorQValues.ComputePerRunPeptideQvalues(
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
                expPrecursorQvalues = PercolatorQValues.ComputeExperimentPrecursorQvalues(
                    finalScores, labels, entryIds);
                expPeptideQvalues = PercolatorQValues.ComputeExperimentPeptideQvalues(
                    finalScores, labels, entryIds, peptides);
            }

            // Best-of-runs monotonicity (issue #4390 clamp, memory-bounded flat form): floor
            // each experiment q up to the entry's best (min-over-runs) combined run q, so an
            // experiment-level q is never more confident than the entry's best single run.
            // Identical floors to PercolatorEngine.ClampExperimentQToBestRun, over the flat
            // score-pass arrays (no resident FdrEntry buffer). Covers the direct dispatch.
            PercolatorQValues.ClampExperimentQToBestRunFlat(
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
        /// Scores every entry with a fold model that did not train on it: entries in the
        /// training subset get their own held-out fold's model, and entries left out of
        /// the subset entirely get the average over all folds.
        ///
        /// Writes into <paramref name="finalScores"/> in place, as the inline phase did.
        /// Extracted from <see cref="RunPercolator"/> as pure code motion; the cross-fold
        /// calibration that follows stays at the call site so the order of the two remains
        /// visible there.
        /// </summary>
        private static void ScoreEntriesWithFoldModels(
            PercolatorConfig config,
            Matrix stdFeatures,
            int[] trainSubset,
            int[] foldAssignments,
            LinearSvmClassifier[] foldModels,
            GradientBoostedTrees[] foldGbtModels,
            int n,
            int subN,
            double[] finalScores)
        {
            // trainSubset is never null - BuildTrainingSubset returns a materialized array
            // on both paths (see its <returns>), which is why there is no null guard here.
            // Stated rather than "see above": the proof lives in another method now.
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


        /// <summary>
        /// Cross-validated per-fold model training: precompute each fold's training
        /// indices, train the folds in parallel, and report per-fold timing plus the
        /// chosen regularization. Exactly one of <paramref name="foldModels"/> and
        /// <paramref name="foldGbtModels"/> is non-null, per
        /// <see cref="PercolatorConfig.UseGradientBoostedTrees"/>; both are allocated by
        /// the caller and filled here, which is also how the parallel loop writes them.
        ///
        /// Extracted from <see cref="RunPercolator"/> as pure code motion - the scratch
        /// pool, the fold-index precompute and the console reporting were already one
        /// contiguous phase, and nothing they compute escapes except through the three
        /// output arrays.
        /// </summary>
        private static void TrainFoldModels(
            PercolatorConfig config,
            Matrix subFeatures,
            bool[] subLabels,
            uint[] subEntryIds,
            string[] subPeptides,
            int[] foldAssignments,
            double[] initialScores,
            int subN,
            int subTargets,
            int nFeatures,
            double trainFdr,
            LinearSvmClassifier[] foldModels,
            GradientBoostedTrees[] foldGbtModels,
            int[] foldIterations)
        {
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
                int nPassing = PercolatorQValues.CountPassing(newTrainScores, trainLabels, trainEntryIds, trainFdr, foldScratch);

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
        /// <see cref="PercolatorQValues.CountPassing(double[],bool[],uint[],double)"/>, and the caller's
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
                int nPassing = PercolatorQValues.CountPassing(valScores, valLabels, valEntryIds, trainFdr);
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
            TargetDecoyCompetition.CompeteFromIndices(scores, labels, entryIds, allIndices, out wi, out ws, out wd);

            var qValues = new double[wi.Length];
            if (wi.Length > 0)
                PercolatorQValues.ComputeQvalues(ws, wd, qValues);

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
                int nPass = PercolatorQValues.CountPassing(scores, labels, entryIds, fdrThreshold);
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

                    totalPassing += PercolatorQValues.CountPassing(testScores, testLabels, testEntryIds, fdrThreshold, localScratch);
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
            TargetDecoyCompetition.CompeteFromIndices(scores, labels, entryIds, allIndices, out wi, out ws, out wd);

            if (wi.Length == 0)
                return false;

            var qValues = new double[wi.Length];
            PercolatorQValues.ComputeQvalues(ws, wd, qValues);

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
