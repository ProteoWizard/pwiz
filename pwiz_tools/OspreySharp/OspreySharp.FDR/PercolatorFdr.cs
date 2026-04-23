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
using System.IO;
using pwiz.OspreySharp.ML;

namespace pwiz.OspreySharp.FDR
{
    /// <summary>
    /// Configuration for the Percolator runner.
    /// </summary>
    public class PercolatorConfig
    {
        /// <summary>FDR threshold for selecting positive training set (default: 0.01).</summary>
        public double TrainFdr { get; set; }

        /// <summary>FDR threshold for final results (default: 0.01).</summary>
        public double TestFdr { get; set; }

        /// <summary>Maximum SVM training iterations per fold (default: 10).</summary>
        public int MaxIterations { get; set; }

        /// <summary>Number of cross-validation folds (default: 3).</summary>
        public int NFolds { get; set; }

        /// <summary>Random seed for reproducibility (default: 42).</summary>
        public ulong Seed { get; set; }

        /// <summary>Grid search C values for SVM cost parameter.</summary>
        public double[] CValues { get; set; }

        /// <summary>Maximum paired entries for SVM cross-validation (default: 300000).</summary>
        public int MaxTrainSize { get; set; }

        /// <summary>Optional feature names for logging (must match feature count).</summary>
        public string[] FeatureNames { get; set; }

        public PercolatorConfig()
        {
            TrainFdr = 0.01;
            TestFdr = 0.01;
            MaxIterations = 10;
            NFolds = 3;
            Seed = 42;
            CValues = new[] { 0.001, 0.01, 0.1, 1.0, 10.0, 100.0 };
            MaxTrainSize = 300000;
        }
    }

    /// <summary>
    /// Input entry for Percolator scoring.
    /// </summary>
    public class PercolatorEntry
    {
        /// <summary>Unique precursor ID (e.g., "filename_libidx").</summary>
        public string Id { get; set; }

        /// <summary>Source file name (for per-run FDR).</summary>
        public string FileName { get; set; }

        /// <summary>Modified peptide sequence (for fold grouping and peptide-level FDR).</summary>
        public string Peptide { get; set; }

        /// <summary>Precursor charge state.</summary>
        public byte Charge { get; set; }

        /// <summary>Whether this is a decoy.</summary>
        public bool IsDecoy { get; set; }

        /// <summary>Entry ID for target-decoy pairing (high bit = decoy).</summary>
        public uint EntryId { get; set; }

        /// <summary>Raw feature values.</summary>
        public double[] Features { get; set; }
    }

    /// <summary>
    /// Result for a single entry after Percolator scoring.
    /// </summary>
    public class PercolatorResult
    {
        /// <summary>Unique precursor ID (matches PercolatorEntry.Id).</summary>
        public string Id { get; set; }

        /// <summary>SVM decision function score.</summary>
        public double Score { get; set; }

        /// <summary>Per-run precursor-level q-value.</summary>
        public double RunPrecursorQvalue { get; set; }

        /// <summary>Per-run peptide-level q-value.</summary>
        public double RunPeptideQvalue { get; set; }

        /// <summary>Experiment-wide precursor-level q-value.</summary>
        public double ExperimentPrecursorQvalue { get; set; }

        /// <summary>Experiment-wide peptide-level q-value.</summary>
        public double ExperimentPeptideQvalue { get; set; }

        /// <summary>Posterior error probability.</summary>
        public double Pep { get; set; }

        public PercolatorResult()
        {
            RunPrecursorQvalue = 1.0;
            RunPeptideQvalue = 1.0;
            ExperimentPrecursorQvalue = 1.0;
            ExperimentPeptideQvalue = 1.0;
            Pep = 1.0;
        }
    }

    /// <summary>
    /// Full results from Percolator analysis.
    /// </summary>
    public class PercolatorResults
    {
        /// <summary>Per-entry results.</summary>
        public List<PercolatorResult> Entries { get; set; }

        /// <summary>Feature weights from best model per fold.</summary>
        public List<double[]> FoldWeights { get; set; }

        /// <summary>Bias terms from best model per fold.</summary>
        public List<double> FoldBiases { get; set; }

        /// <summary>Feature standardizer used during training.</summary>
        public FeatureStandardizer Standardizer { get; set; }

        /// <summary>Number of iterations used per fold.</summary>
        public List<int> IterationsPerFold { get; set; }

        public PercolatorResults()
        {
            Entries = new List<PercolatorResult>();
            FoldWeights = new List<double[]>();
            FoldBiases = new List<double>();
            IterationsPerFold = new List<int>();
        }
    }

    /// <summary>
    /// Performs false discovery rate estimation using the Percolator algorithm.
    /// Port of osprey-fdr/src/percolator.rs.
    /// </summary>
    public static class PercolatorFdr
    {
        private static readonly uint BASE_ID_MASK = 0x7FFFFFFF;
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
            Console.Error.WriteLine(
                $"[TIMING]   Percolator setup + standardize: {swSetup.Elapsed.TotalSeconds:F1}s ({n} entries x {nFeatures} features)");

            // Stage 5 standardizer dump. Gated by OSPREY_DUMP_STANDARDIZER=1;
            // exits via OSPREY_STANDARDIZER_ONLY=1. Mirrors Rust
            // dump_stage5_standardizer in osprey-fdr/src/percolator.rs.
            if (string.Equals(Environment.GetEnvironmentVariable(@"OSPREY_DUMP_STANDARDIZER"), @"1"))
            {
                WriteStage5StandardizerDump(standardizer, config.FeatureNames);
                if (string.Equals(Environment.GetEnvironmentVariable(@"OSPREY_STANDARDIZER_ONLY"), @"1"))
                {
                    Console.Error.WriteLine(@"[BISECT] OSPREY_STANDARDIZER_ONLY set - aborting after dump");
                    Environment.Exit(0);
                }
            }

            // 3a. Best-per-precursor: pick the single best-scoring observation per
            //     (base_id, isDecoy) tuple across all files. With N files per peptide,
            //     this avoids the SVM seeing the same precursor's target/decoy pair
            //     N times, which would inflate apparent target/decoy separation and
            //     cause the SVM to learn file-specific noise rather than peptide
            //     discriminating features. Matches the Rust streaming Percolator path.
            int[] bestPerPrecursor = SelectBestPerPrecursor(labels, entryIds, entries);

            int dedupTargets = 0, dedupDecoys = 0;
            for (int i = 0; i < bestPerPrecursor.Length; i++)
            {
                if (labels[bestPerPrecursor[i]])
                    dedupDecoys++;
                else dedupTargets++;
            }
            Console.Error.WriteLine("[COUNT]   Percolator best-per-precursor: {0} entries ({1} targets, {2} decoys) from {3} total",
                bestPerPrecursor.Length, dedupTargets, dedupDecoys, n);

            // 3b. If still > MaxTrainSize, subsample by peptide groups
            //     (so target/decoy pairs and same-peptide multi-charge stay together).
            int[] trainSubset = bestPerPrecursor;
            if (config.MaxTrainSize > 0 && bestPerPrecursor.Length > config.MaxTrainSize)
            {
                var dedupLabels = new bool[bestPerPrecursor.Length];
                var dedupEntryIds = new uint[bestPerPrecursor.Length];
                var dedupPeptides = new string[bestPerPrecursor.Length];
                for (int i = 0; i < bestPerPrecursor.Length; i++)
                {
                    int gi = bestPerPrecursor[i];
                    dedupLabels[i] = labels[gi];
                    dedupEntryIds[i] = entryIds[gi];
                    dedupPeptides[i] = peptides[gi];
                }

                int[] localSelected = SubsampleByPeptideGroup(
                    dedupLabels, dedupEntryIds, dedupPeptides,
                    config.MaxTrainSize, config.Seed);

                trainSubset = new int[localSelected.Length];
                for (int i = 0; i < localSelected.Length; i++)
                    trainSubset[i] = bestPerPrecursor[localSelected[i]];
            }

            int subN = trainSubset.Length;
            int subTargets = 0, subDecoys = 0;
            for (int i = 0; i < trainSubset.Length; i++)
            {
                if (labels[trainSubset[i]])
                    subDecoys++;
                else subTargets++;
            }
            Console.Error.WriteLine("[COUNT]   Percolator subsample: {0} entries ({1} targets, {2} decoys) from {3} dedup",
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
                subFeatures = ExtractRows(stdFeatures, trainSubset);
            }
            else
            {
                subLabels = (bool[])labels.Clone();
                subEntryIds = (uint[])entryIds.Clone();
                subPeptides = (string[])peptides.Clone();
                subFeatures = stdFeatures;
            }

            // 4. Assign folds on the (possibly subsampled) set
            int[] foldAssignments = CreateStratifiedFoldsByPeptide(
                subLabels, subPeptides, subEntryIds, config.NFolds);

            // Stage 5 sub-stage diagnostic dump. Gated by OSPREY_DUMP_SUBSAMPLE=1;
            // exits via OSPREY_SUBSAMPLE_ONLY=1. Captures subsample membership and
            // fold assignment per entry, mirroring the Rust dump in
            // osprey-fdr/src/percolator.rs. The dump is inlined here (not routed
            // through OspreyDiagnostics) because OspreySharp.FDR does not
            // reference the main OspreySharp assembly.
            if (string.Equals(Environment.GetEnvironmentVariable(@"OSPREY_DUMP_SUBSAMPLE"), @"1"))
            {
                WriteStage5SubsampleDump(entries, trainSubset, foldAssignments);
                if (string.Equals(Environment.GetEnvironmentVariable(@"OSPREY_SUBSAMPLE_ONLY"), @"1"))
                {
                    Console.Error.WriteLine(@"[BISECT] OSPREY_SUBSAMPLE_ONLY set - aborting after dump");
                    Environment.Exit(0);
                }
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

            string bestFeatName = (config.FeatureNames != null &&
                                   bestFeatIdx >= 0 &&
                                   bestFeatIdx < config.FeatureNames.Length)
                ? config.FeatureNames[bestFeatIdx]
                : string.Format("feature_{0}", bestFeatIdx);
            Console.Error.WriteLine(
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
            var foldIterations = new int[config.NFolds];
            var foldElapsed = new double[config.NFolds];

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

            // Train all folds in parallel. Each fold reads from the shared
            // subFeatures matrix (read-only) and produces an independent model.
            // Matches the Rust implementation's into_par_iter() over folds.
            var swTrain = Stopwatch.StartNew();
            System.Threading.Tasks.Parallel.For(0, config.NFolds, fold =>
            {
                var swFold = Stopwatch.StartNew();
                int iters;
                foldModels[fold] = TrainFold(
                    subFeatures, subLabels, subEntryIds, subPeptides,
                    foldTrainIndices[fold], initialScores, config, trainFdr, out iters);
                foldIterations[fold] = iters;
                swFold.Stop();
                foldElapsed[fold] = swFold.Elapsed.TotalSeconds;
            });
            swTrain.Stop();

            for (int fold = 0; fold < config.NFolds; fold++)
            {
                Console.Error.WriteLine("[TIMING]   Percolator fold {0}/{1}: {2:F1}s ({3} iterations)",
                    fold + 1, config.NFolds, foldElapsed[fold], foldIterations[fold]);
            }
            Console.Error.WriteLine("[TIMING]   Percolator train all folds (parallel): {0:F1}s",
                swTrain.Elapsed.TotalSeconds);

            // Stage 5 SVM-internals dump. Gated by OSPREY_DUMP_SVM_WEIGHTS=1;
            // exits via OSPREY_SVM_WEIGHTS_ONLY=1. Captures per-fold weights,
            // bias, and iteration count right after SVM training converges
            // and before Granholm calibration. Mirrors rust side in
            // osprey-fdr/src/percolator.rs::dump_stage5_svm_weights.
            if (string.Equals(Environment.GetEnvironmentVariable(@"OSPREY_DUMP_SVM_WEIGHTS"), @"1"))
            {
                WriteStage5SvmWeightsDump(foldModels, foldIterations, config.FeatureNames);
                if (string.Equals(Environment.GetEnvironmentVariable(@"OSPREY_SVM_WEIGHTS_ONLY"), @"1"))
                {
                    Console.Error.WriteLine(@"[BISECT] OSPREY_SVM_WEIGHTS_ONLY set - aborting after dump");
                    Environment.Exit(0);
                }
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
                    var testFeatures = ExtractRows(stdFeatures, testGlobalIndices);
                    var testScores = foldModels[fold].DecisionFunction(testFeatures);
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
                    var nonSubFeatures = ExtractRows(stdFeatures, nonSubsetIndices.ToArray());
                    double nModels = config.NFolds;
                    var avgScores = new double[nonSubsetIndices.Count];
                    for (int fold = 0; fold < config.NFolds; fold++)
                    {
                        var modelScores = foldModels[fold].DecisionFunction(nonSubFeatures);
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
                    var testFeatures = ExtractRows(stdFeatures, testIndices.ToArray());
                    var testScores = foldModels[fold].DecisionFunction(testFeatures);
                    for (int i = 0; i < testIndices.Count; i++)
                        finalScores[testIndices[i]] = testScores[i];
                }
            }

            for (int fold = 0; fold < config.NFolds; fold++)
            {
                foldWeights.Add(foldModels[fold].Weights);
                foldBiases.Add(foldModels[fold].Bias);
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

            // 9. Build results
            var results = new List<PercolatorResult>(n);
            for (int i = 0; i < n; i++)
            {
                results.Add(new PercolatorResult
                {
                    Id = entries[i].Id,
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
                Standardizer = standardizer,
                IterationsPerFold = iterationsPerFold
            };
        }

        // ============================================================
        // SVM fold training
        // ============================================================

        private static LinearSvmClassifier TrainFold(
            Matrix stdFeatures,
            bool[] labels,
            uint[] entryIds,
            string[] peptides,
            int[] trainIndices,
            double[] initialScores,
            PercolatorConfig config,
            double trainFdr,
            out int bestIteration)
        {
            int nFeatures = stdFeatures.Cols;
            var currentScores = (double[])initialScores.Clone();

            var bestModel = LinearSvmClassifier.Train(
                Matrix.Zeros(0, nFeatures), new bool[0], 1.0, config.Seed);
            bestIteration = 0;
            int bestPassing = 0;
            int consecutiveNoImprove = 0;

            var trainLabels = new bool[trainIndices.Length];
            var trainEntryIds = new uint[trainIndices.Length];
            for (int i = 0; i < trainIndices.Length; i++)
            {
                trainLabels[i] = labels[trainIndices[i]];
                trainEntryIds[i] = entryIds[trainIndices[i]];
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

                var svmFeatures = ExtractRows(stdFeatures, svmGlobalIndices);
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
                var svmFoldAssignments = CreateStratifiedFoldsByPeptide(
                    svmLabels, svmPeptides, svmEntryIds, config.NFolds);

                double bestC = GridSearchC(
                    svmFeatures, svmLabels, svmEntryIds,
                    config.CValues, svmFoldAssignments, config.NFolds,
                    config.Seed, trainFdr);

                // iii. Train SVM with best C
                var model = LinearSvmClassifier.Train(
                    svmFeatures, svmLabels, bestC, config.Seed);

                // iv. Score ALL training set entries with new model
                var trainFeatures = ExtractRows(stdFeatures, trainIndices);
                var newTrainScores = model.DecisionFunction(trainFeatures);

                for (int i = 0; i < trainIndices.Length; i++)
                    currentScores[trainIndices[i]] = newTrainScores[i];

                // v. Count passing targets
                int nPassing = CountPassing(newTrainScores, trainLabels, trainEntryIds, trainFdr);

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

            bestIteration = Math.Max(bestIteration, 1);
            return bestModel;
        }

        // ============================================================
        // Target-decoy competition and q-value computation
        // ============================================================

        /// <summary>
        /// Core competition logic: group by base_id, compete, return winners sorted by score desc.
        /// </summary>
        public static void CompeteFromIndices(
            double[] scores,
            bool[] labels,
            uint[] entryIds,
            int[] indices,
            out int[] winnerIndices,
            out double[] winnerScores,
            out bool[] winnerIsDecoy)
        {
            var targets = new Dictionary<uint, KeyValuePair<int, double>>();
            var decoys = new Dictionary<uint, KeyValuePair<int, double>>();

            foreach (int idx in indices)
            {
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

            // Sort by score desc, then base_id asc for deterministic tiebreaking
            winners.Sort((a, b) =>
            {
                int cmp = b.Item2.CompareTo(a.Item2);
                if (cmp != 0)
                    return cmp;
                return a.Item4.CompareTo(b.Item4);
            });

            winnerIndices = new int[winners.Count];
            winnerScores = new double[winners.Count];
            winnerIsDecoy = new bool[winners.Count];
            for (int i = 0; i < winners.Count; i++)
            {
                winnerIndices[i] = winners[i].Item1;
                winnerScores[i] = winners[i].Item2;
                winnerIsDecoy[i] = winners[i].Item3;
            }
        }

        private static void CompeteAll(
            double[] scores,
            bool[] labels,
            uint[] entryIds,
            out int[] winnerIndices,
            out double[] winnerScores,
            out bool[] winnerIsDecoy)
        {
            var allIndices = new int[scores.Length];
            for (int i = 0; i < scores.Length; i++)
                allIndices[i] = i;
            CompeteFromIndices(scores, labels, entryIds, allIndices,
                out winnerIndices, out winnerScores, out winnerIsDecoy);
        }

        /// <summary>
        /// Compute conservative q-values: FDR = (n_decoy + 1) / n_target.
        /// Input must be sorted by score descending (winners from competition).
        /// </summary>
        public static void ComputeConservativeQvalues(
            double[] scores, bool[] isDecoy, double[] qValues)
        {
            int n = isDecoy.Length;
            int nTarget = 0;
            int nDecoy = 0;

            for (int i = 0; i < n; i++)
            {
                if (isDecoy[i])
                    nDecoy++;
                else
                    nTarget++;
                qValues[i] = nTarget > 0 ? (double)(nDecoy + 1) / nTarget : 1.0;
            }

            // Backward pass: make monotonically non-increasing
            double qMin = 1.0;
            for (int i = n - 1; i >= 0; i--)
            {
                qMin = Math.Min(qMin, qValues[i]);
                qValues[i] = qMin;
            }
        }

        /// <summary>
        /// Compute non-conservative q-values: FDR = n_decoy / n_target.
        /// Used internally for iteration tracking and positive training set selection.
        /// </summary>
        private static void ComputeQvalues(
            double[] scores, bool[] isDecoy, double[] qValues)
        {
            int n = isDecoy.Length;
            int nTarget = 0;
            int nDecoy = 0;

            for (int i = 0; i < n; i++)
            {
                if (isDecoy[i])
                    nDecoy++;
                else
                    nTarget++;
                qValues[i] = nTarget > 0 ? (double)nDecoy / nTarget : 1.0;
            }

            double qMin = 1.0;
            for (int i = n - 1; i >= 0; i--)
            {
                qMin = Math.Min(qMin, qValues[i]);
                qValues[i] = qMin;
            }
        }

        /// <summary>
        /// Count targets passing FDR threshold using non-conservative formula.
        /// </summary>
        public static int CountPassing(
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
            ComputeQvalues(ws, wd, qValues);

            int count = 0;
            for (int rank = 0; rank < wi.Length; rank++)
            {
                if (!labels[wi[rank]] && qValues[rank] <= fdrThreshold)
                    count++;
            }
            return count;
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
            ulong seed, double fdrThreshold)
        {
            double bestC = cValues[0];
            int bestTotal = 0;

            foreach (double c in cValues)
            {
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

                    var trainFeatures = ExtractRows(features, trainIdx.ToArray());
                    var trainLabels = new bool[trainIdx.Count];
                    for (int i = 0; i < trainIdx.Count; i++)
                        trainLabels[i] = labels[trainIdx[i]];

                    var model = LinearSvmClassifier.Train(trainFeatures, trainLabels, c, seed);
                    var testFeatures = ExtractRows(features, testIdx.ToArray());
                    var testScores = model.DecisionFunction(testFeatures);
                    var testLabels = new bool[testIdx.Count];
                    var testEntryIds = new uint[testIdx.Count];
                    for (int i = 0; i < testIdx.Count; i++)
                    {
                        testLabels[i] = labels[testIdx[i]];
                        testEntryIds[i] = entryIds[testIdx[i]];
                    }

                    totalPassing += CountPassing(testScores, testLabels, testEntryIds, fdrThreshold);
                }

                if (totalPassing > bestTotal)
                {
                    bestTotal = totalPassing;
                    bestC = c;
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
                decoyScores.Sort();

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

            foreach (var group in fileGroups.Values)
            {
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

            foreach (var group in fileGroups.Values)
            {
                var bestPerPeptide = BestPrecursorPerPeptide(
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

            return qvalues;
        }

        private static double[] ComputeExperimentPrecursorQvalues(
            double[] scores, bool[] labels, uint[] entryIds)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            for (int i = 0; i < n; i++)
                qvalues[i] = 1.0;

            int[] wi;
            double[] ws;
            bool[] wd;
            CompeteAll(scores, labels, entryIds, out wi, out ws, out wd);

            var q = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, q);

            for (int rank = 0; rank < wi.Length; rank++)
                qvalues[wi[rank]] = q[rank];

            return qvalues;
        }

        private static double[] ComputeExperimentPeptideQvalues(
            double[] scores, bool[] labels, uint[] entryIds, string[] peptides)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            for (int i = 0; i < n; i++)
                qvalues[i] = 1.0;

            var allIndices = new int[n];
            for (int i = 0; i < n; i++)
                allIndices[i] = i;

            var bestPerPeptide = BestPrecursorPerPeptide(allIndices, scores, labels, peptides);

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
            CompeteFromIndices(peptScores, peptLabels, peptEntryIds, allPeptIndices,
                out wi, out ws, out wd);

            var q = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, q);

            var peptideQvalue = new Dictionary<string, double>();
            for (int rank = 0; rank < wi.Length; rank++)
            {
                int globalIdx = bestPerPeptide[wi[rank]];
                peptideQvalue[peptides[globalIdx]] = q[rank];
            }

            for (int i = 0; i < n; i++)
            {
                double qv;
                if (peptideQvalue.TryGetValue(peptides[i], out qv))
                    qvalues[i] = qv;
            }

            return qvalues;
        }

        // ============================================================
        // Best precursor per peptide
        // ============================================================

        /// <summary>
        /// Find the best-scoring precursor per peptide from a set of indices.
        /// Returns global indices sorted for deterministic order.
        /// </summary>
        public static int[] BestPrecursorPerPeptide(
            int[] indices, double[] scores, bool[] labels, string[] peptides)
        {
            var best = new Dictionary<string, KeyValuePair<int, double>>();

            foreach (int idx in indices)
            {
                string pept = peptides[idx];
                KeyValuePair<int, double> existing;
                if (best.TryGetValue(pept, out existing))
                {
                    if (scores[idx] > existing.Value)
                        best[pept] = new KeyValuePair<int, double>(idx, scores[idx]);
                }
                else
                {
                    best[pept] = new KeyValuePair<int, double>(idx, scores[idx]);
                }
            }

            var result = new List<int>(best.Count);
            foreach (var kvp in best)
                result.Add(kvp.Value.Key);
            result.Sort();
            return result.ToArray();
        }

        // ============================================================
        // Fold assignment
        // ============================================================

        /// <summary>
        /// Create stratified fold assignments grouped by target peptide, keeping pairs together.
        /// </summary>
        public static int[] CreateStratifiedFoldsByPeptide(
            bool[] labels, string[] peptides, uint[] entryIds, int nFolds)
        {
            // 1. Build base_id -> target peptide mapping
            var baseIdToTargetPeptide = new Dictionary<uint, string>();
            for (int i = 0; i < labels.Length; i++)
            {
                uint baseId = entryIds[i] & BASE_ID_MASK;
                if (!labels[i])
                {
                    if (!baseIdToTargetPeptide.ContainsKey(baseId))
                        baseIdToTargetPeptide[baseId] = peptides[i];
                }
            }

            // 2. Map each entry to its group key
            var groupKeys = new string[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                uint baseId = entryIds[i] & BASE_ID_MASK;
                string key;
                if (!baseIdToTargetPeptide.TryGetValue(baseId, out key))
                    key = peptides[i];
                groupKeys[i] = key;
            }

            // 3. Group all entries by target peptide
            var peptideGroups = new Dictionary<string, List<int>>();
            for (int i = 0; i < groupKeys.Length; i++)
            {
                List<int> list;
                if (!peptideGroups.TryGetValue(groupKeys[i], out list))
                {
                    list = new List<int>();
                    peptideGroups[groupKeys[i]] = list;
                }
                list.Add(i);
            }

            // 4. Sort for deterministic assignment, round-robin assign folds
            var sortedKeys = new List<string>(peptideGroups.Keys);
            sortedKeys.Sort(StringComparer.Ordinal);

            var foldAssignments = new int[labels.Length];
            for (int i = 0; i < sortedKeys.Count; i++)
            {
                int fold = i % nFolds;
                foreach (int idx in peptideGroups[sortedKeys[i]])
                    foldAssignments[idx] = fold;
            }

            return foldAssignments;
        }

        // ============================================================
        // Subsampling
        // ============================================================

        /// <summary>
        /// Pick the best-scoring observation per (base_id, isDecoy) tuple across all
        /// entries. Used to deduplicate multi-file observations of the same precursor
        /// before SVM training, so the SVM doesn't see the same peptide N times.
        ///
        /// Score for ranking is taken from PercolatorEntry.Features[0], which is
        /// coelution_sum (matches Rust's selection criterion in pipeline.rs).
        /// </summary>
        public static int[] SelectBestPerPrecursor(
            bool[] labels, uint[] entryIds, IList<PercolatorEntry> entries)
        {
            int n = labels.Length;
            // Map base_id to best target index, separately for targets and decoys
            var bestTarget = new Dictionary<uint, int>();
            var bestDecoy = new Dictionary<uint, int>();

            for (int i = 0; i < n; i++)
            {
                uint baseId = entryIds[i] & BASE_ID_MASK;
                double score = entries[i].Features[0];

                Dictionary<uint, int> map = labels[i] ? bestDecoy : bestTarget;
                int existing;
                if (map.TryGetValue(baseId, out existing))
                {
                    if (score > entries[existing].Features[0])
                        map[baseId] = i;
                }
                else
                {
                    map[baseId] = i;
                }
            }

            var result = new int[bestTarget.Count + bestDecoy.Count];
            int idx = 0;
            foreach (int i in bestTarget.Values)
                result[idx++] = i;
            foreach (int i in bestDecoy.Values)
                result[idx++] = i;
            Array.Sort(result); // deterministic order for downstream subsampling
            return result;
        }

        /// <summary>
        /// Subsample entries by peptide group, keeping target-decoy pairs and charge states together.
        /// </summary>
        public static int[] SubsampleByPeptideGroup(
            bool[] labels, uint[] entryIds, string[] peptides,
            int maxEntries, ulong seed)
        {
            int n = labels.Length;
            if (n <= maxEntries)
            {
                var all = new int[n];
                for (int i = 0; i < n; i++)
                    all[i] = i;
                return all;
            }

            // Build peptide groups (group by target peptide via base_id)
            var baseIdToTargetPeptide = new Dictionary<uint, string>();
            for (int i = 0; i < n; i++)
            {
                uint baseId = entryIds[i] & BASE_ID_MASK;
                if (!labels[i])
                {
                    if (!baseIdToTargetPeptide.ContainsKey(baseId))
                        baseIdToTargetPeptide[baseId] = peptides[i];
                }
            }

            var peptideGroups = new Dictionary<string, List<int>>();
            for (int i = 0; i < n; i++)
            {
                uint baseId = entryIds[i] & BASE_ID_MASK;
                string key;
                if (!baseIdToTargetPeptide.TryGetValue(baseId, out key))
                    key = peptides[i];
                List<int> list;
                if (!peptideGroups.TryGetValue(key, out list))
                {
                    list = new List<int>();
                    peptideGroups[key] = list;
                }
                list.Add(i);
            }

            // Sort deterministically and shuffle with Fisher-Yates
            var groups = new List<KeyValuePair<string, List<int>>>(peptideGroups);
            groups.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));

            ulong rngState = seed;
            for (int i = groups.Count - 1; i >= 1; i--)
            {
                rngState ^= rngState << 13;
                rngState ^= rngState >> 7;
                rngState ^= rngState << 17;
                int j = (int)(rngState % (ulong)(i + 1));
                var tmp = groups[i];
                groups[i] = groups[j];
                groups[j] = tmp;
            }

            // Select groups until we reach maxEntries
            var selected = new List<int>(maxEntries);
            foreach (var group in groups)
            {
                if (selected.Count + group.Value.Count > maxEntries && selected.Count > 0)
                    break;
                selected.AddRange(group.Value);
            }

            selected.Sort();
            return selected.ToArray();
        }

        /// <summary>
        /// Cross-impl bisection dump of Stage 5 subsample + fold-assignment
        /// state, written to cs_stage5_subsample.tsv. Mirrors the Rust dump
        /// in osprey-fdr/src/percolator.rs so Compare-Subsample.ps1 can
        /// hash-join on entry_id.
        ///
        /// Columns: entry_id, native_position, charge, modified_sequence,
        /// is_decoy, base_id, in_subsample, fold_id. native_position is
        /// the entry's index in the input list -- divergence here means
        /// the two tools populate their arrays in different order. Rows
        /// sorted by entry_id for stable human inspection; compare is
        /// sort-order-agnostic.
        /// </summary>
        private static void WriteStage5SubsampleDump(
            IList<PercolatorEntry> entries,
            int[] trainSubset,
            int[] foldAssignments)
        {
            const string path = @"cs_stage5_subsample.tsv";
            var inv = CultureInfo.InvariantCulture;
            int n = entries.Count;

            var inSub = new bool[n];
            var foldFor = new int[n];
            for (int i = 0; i < n; i++) foldFor[i] = -1;

            for (int subPos = 0; subPos < trainSubset.Length; subPos++)
            {
                int nativePos = trainSubset[subPos];
                inSub[nativePos] = true;
                foldFor[nativePos] = foldAssignments[subPos];
            }

            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Array.Sort(order, (a, b) => entries[a].EntryId.CompareTo(entries[b].EntryId));

            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"entry_id	native_position	charge	modified_sequence	is_decoy	base_id	in_subsample	fold_id");
                foreach (int i in order)
                {
                    var e = entries[i];
                    uint baseId = e.EntryId & BASE_ID_MASK;
                    sw.Write(e.EntryId.ToString(inv));
                    sw.Write('\t'); sw.Write(i.ToString(inv));
                    sw.Write('\t'); sw.Write(e.Charge.ToString(inv));
                    sw.Write('\t'); sw.Write(e.Peptide ?? string.Empty);
                    sw.Write('\t'); sw.Write(e.IsDecoy ? @"true" : @"false");
                    sw.Write('\t'); sw.Write(baseId.ToString(inv));
                    sw.Write('\t'); sw.Write(inSub[i] ? @"true" : @"false");
                    sw.Write('\t'); sw.WriteLine(foldFor[i].ToString(inv));
                }
            }
            Console.Error.WriteLine(@"Wrote Stage 5 subsample dump: {0} ({1} rows)", path, n);
        }

        /// <summary>
        /// Cross-impl bisection dump of per-fold SVM weights, taken right
        /// after training converges and before Granholm cross-fold
        /// calibration. Mirrors dump_stage5_svm_weights in Rust. Writes
        /// cs_stage5_svm_weights.tsv with one row per (fold, weight) pair:
        /// 21 feature weights + 1 bias per fold.
        ///
        /// Columns: fold, weight_idx, feature_name, value, fold_iterations.
        /// Sorted by (fold, weight_idx) for stable inspection; compare is
        /// hash-joined.
        /// </summary>
        private static void WriteStage5SvmWeightsDump(
            LinearSvmClassifier[] foldModels,
            int[] foldIterations,
            string[] featureNames)
        {
            const string path = @"cs_stage5_svm_weights.tsv";
            var inv = CultureInfo.InvariantCulture;

            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"fold	weight_idx	feature_name	value	fold_iterations");
                for (int fold = 0; fold < foldModels.Length; fold++)
                {
                    var model = foldModels[fold];
                    var weights = model.Weights;
                    int iters = fold < foldIterations.Length ? foldIterations[fold] : 0;
                    for (int wi = 0; wi < weights.Length; wi++)
                    {
                        string name = (featureNames != null && wi < featureNames.Length)
                            ? featureNames[wi]
                            : @"unknown";
                        sw.Write(fold.ToString(inv));
                        sw.Write('\t'); sw.Write(wi.ToString(inv));
                        sw.Write('\t'); sw.Write(name);
                        sw.Write('\t'); sw.Write(weights[wi].ToString(@"G17", inv));
                        sw.Write('\t'); sw.WriteLine(iters.ToString(inv));
                    }
                    sw.Write(fold.ToString(inv));
                    sw.Write('\t'); sw.Write(weights.Length.ToString(inv));
                    sw.Write('\t'); sw.Write(@"bias");
                    sw.Write('\t'); sw.Write(model.Bias.ToString(@"G17", inv));
                    sw.Write('\t'); sw.WriteLine(iters.ToString(inv));
                }
            }
            Console.Error.WriteLine(@"Wrote Stage 5 SVM weights dump: {0} ({1} folds)", path, foldModels.Length);
        }

        /// <summary>
        /// Cross-impl bisection dump of the feature standardizer state,
        /// taken right after FitTransform returns and before subsampling
        /// / fold assignment. Mirrors dump_stage5_standardizer in Rust.
        /// Writes cs_stage5_standardizer.tsv with one row per feature.
        /// Columns: feature_idx, feature_name, mean, std.
        /// </summary>
        private static void WriteStage5StandardizerDump(
            FeatureStandardizer standardizer,
            string[] featureNames)
        {
            const string path = @"cs_stage5_standardizer.tsv";
            var inv = CultureInfo.InvariantCulture;
            var means = standardizer.Means;
            var stds = standardizer.Stds;

            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"feature_idx	feature_name	mean	std");
                for (int i = 0; i < means.Length; i++)
                {
                    string name = (featureNames != null && i < featureNames.Length)
                        ? featureNames[i]
                        : @"unknown";
                    sw.Write(i.ToString(inv));
                    sw.Write('\t'); sw.Write(name);
                    sw.Write('\t'); sw.Write(means[i].ToString(@"G17", inv));
                    sw.Write('\t'); sw.WriteLine(stds[i].ToString(@"G17", inv));
                }
            }
            Console.Error.WriteLine(@"Wrote Stage 5 standardizer dump: {0} ({1} features)", path, means.Length);
        }

        // ============================================================
        // Utility
        // ============================================================

        private static Matrix ExtractRows(Matrix matrix, int[] rowIndices)
        {
            int nCols = matrix.Cols;
            int nRows = rowIndices.Length;
            var data = new double[nRows * nCols];
            // Direct array access avoids property accessor overhead and the
            // bounds-check on every cell. This loop is the hottest path in
            // Percolator: called ~540 times per file with 200K x 21 matrices.
            double[] src = matrix.Data;
            for (int i = 0; i < nRows; i++)
            {
                int srcOffset = rowIndices[i] * nCols;
                int dstOffset = i * nCols;
                Array.Copy(src, srcOffset, data, dstOffset, nCols);
            }
            return Matrix.WrapNoClone(data, nRows, nCols);
        }
    }
}
