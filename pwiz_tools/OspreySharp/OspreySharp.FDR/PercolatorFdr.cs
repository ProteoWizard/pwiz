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
using pwiz.OspreySharp.Core;
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

        /// <summary>
        /// Optional human-friendly feature labels for the post-training
        /// feature-contribution report (falls back to <see cref="FeatureNames"/>
        /// when null). Display only -- never used for scoring.
        /// </summary>
        public string[] FeatureLabels { get; set; }

        /// <summary>
        /// Optional per-feature expected-direction flags (Skyline's
        /// <c>IsReversedScore</c>), passed in from the Tasks layer because the FDR
        /// project does not reference the Scoring assembly that owns the SPI. Used
        /// only to flag unexpected-direction coefficients in the contribution table;
        /// never used for scoring. Null suppresses the flag.
        /// </summary>
        public bool[] ReversedScore { get; set; }

        /// <summary>
        /// If true, <see cref="PercolatorFdr.RunPercolator"/> trains the fold
        /// models + standardizer and returns early -- skips CV/averaged
        /// scoring of the input entries, PEP estimation, q-value
        /// computation, and per-file FDR logging. Used by the streaming
        /// path in <c>AnalysisPipeline.RunPercolatorStreaming</c>, where
        /// the caller pre-dedups + subsamples the entries for training,
        /// then invokes <see cref="PercolatorFdr.ScorePopulationAndComputeFdr"/>
        /// on the full per-file FdrEntry population. Mirrors Rust's
        /// <c>PercolatorConfig::train_only</c>.
        /// </summary>
        public bool TrainOnly { get; set; }

        /// <summary>
        /// Stage 5 diagnostic-dump gates, or <c>null</c> (the common case) when
        /// diagnostics are off. Carried in by the Tasks-layer caller so
        /// <see cref="PercolatorFdr.RunPercolator"/> stays a pure function of its
        /// inputs -- it reads no env vars and never exits the process. See
        /// <see cref="PercolatorDiagnosticsConfig"/>.
        /// </summary>
        public PercolatorDiagnosticsConfig Diagnostics { get; set; }

        public PercolatorConfig()
        {
            TrainFdr = 0.01;
            TestFdr = 0.01;
            MaxIterations = 10;
            NFolds = 3;
            Seed = 42;
            CValues = new[] { 0.001, 0.01, 0.1, 1.0, 10.0, 100.0 };
            MaxTrainSize = 300000;
            TrainOnly = false;
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

        /// <summary>
        /// The averaged (across-fold) standardized feature weight vector formed at
        /// scoring time, or <c>null</c> when scoring did not run (e.g. the
        /// <c>TrainOnly</c> early-return). Surfaced for the feature-contribution
        /// report and tests; not used by scoring.
        /// </summary>
        public double[] AvgWeights { get; set; }

        /// <summary>The averaged (across-fold) bias paired with <see cref="AvgWeights"/>.</summary>
        public double AvgBias { get; set; }

        /// <summary>Feature standardizer used during training.</summary>
        public FeatureStandardizer Standardizer { get; set; }

        /// <summary>Number of iterations used per fold.</summary>
        public List<int> IterationsPerFold { get; set; }

        /// <summary>
        /// Set when <see cref="PercolatorFdr.RunPercolator"/> wrote a
        /// diagnostic-only (<c>*Only</c>) dump and stopped early instead of
        /// completing FDR. The Tasks-layer caller inspects this and performs the
        /// process early-exit; the engine itself never exits. The other fields are
        /// unpopulated when this is <c>true</c>.
        /// </summary>
        public bool DiagnosticAbort { get; set; }

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
            OspreyOutput.Out.WriteLine(
                $"[TIMING]   Percolator setup + standardize: {swSetup.Elapsed.TotalSeconds:F1}s ({n} entries x {nFeatures} features)");

            // Stage 5 standardizer dump. Gated by the injected diagnostics config
            // (OSPREY_DUMP_STANDARDIZER); a *Only request returns the abort
            // sentinel so the Tasks-layer caller -- not this engine -- decides the
            // early-exit. Mirrors Rust dump_stage5_standardizer in
            // osprey-fdr/src/percolator.rs.
            if (config.Diagnostics != null && config.Diagnostics.DumpStandardizer)
            {
                WriteStage5StandardizerDump(standardizer, config.FeatureNames);
                if (config.Diagnostics.StandardizerOnly)
                    return new PercolatorResults { DiagnosticAbort = true };
            }

            // One-shot diagnostic for 2nd-pass divergence localization.
            // Gated by OSPREY_DUMP_PERC_INPUT; a *Only request returns the abort
            // sentinel. Dumps the raw per-entry feature vectors fed into the
            // standardizer so cross-impl compare can pinpoint which rows differ.
            if (config.Diagnostics != null && config.Diagnostics.DumpPercInput)
            {
                WriteStage5PercInputDump(entries, config.FeatureNames);
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
            int[] trainSubset = BuildTrainingSubset(
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

            // Stage 5 sub-stage diagnostic dump. Gated by OSPREY_DUMP_SUBSAMPLE;
            // a *Only request returns the abort sentinel. Captures subsample
            // membership and fold assignment per entry, mirroring the Rust dump in
            // osprey-fdr/src/percolator.rs. The dump writer is inlined here (not
            // routed through OspreyDiagnostics) because OspreySharp.FDR does not
            // reference the main OspreySharp assembly; only the gate flag + the
            // early-exit decision are lifted out to the Tasks-layer caller.
            if (config.Diagnostics != null && config.Diagnostics.DumpSubsample)
            {
                WriteStage5SubsampleDump(entries, trainSubset, foldAssignments);
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

            string bestFeatName = (config.FeatureNames != null &&
                                   bestFeatIdx >= 0 &&
                                   bestFeatIdx < config.FeatureNames.Length)
                ? config.FeatureNames[bestFeatIdx]
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
                foldModels[fold] = TrainFold(
                    subFeatures, subLabels, subEntryIds, subPeptides,
                    foldTrainIndices[fold], initialScores, config, trainFdr,
                    svmScratchPool, fold, trainProgress, out iters);
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

            // Stage 5 SVM-internals dump. Gated by OSPREY_DUMP_SVM_WEIGHTS;
            // a *Only request returns the abort sentinel. Captures per-fold
            // weights, bias, and iteration count right after SVM training
            // converges and before Granholm calibration. Mirrors rust side in
            // osprey-fdr/src/percolator.rs::dump_stage5_svm_weights.
            if (config.Diagnostics != null && config.Diagnostics.DumpSvmWeights)
            {
                WriteStage5SvmWeightsDump(foldModels, foldIterations, config.FeatureNames);
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
                    trainFoldWeights.Add(foldModels[fold].Weights);
                    trainFoldBiases.Add(foldModels[fold].Bias);
                    trainIterations.Add(foldIterations[fold]);
                }
                return new PercolatorResults
                {
                    Entries = new List<PercolatorResult>(),
                    FoldWeights = trainFoldWeights,
                    FoldBiases = trainFoldBiases,
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

            // 8b. Feature weight + percent-contribution report (reporting only).
            // Form the averaged model by averaging the per-fold weights (same
            // arithmetic as the streaming path) and accumulate per-feature
            // target/decoy means over the FULL standardized matrix -- the same
            // population the model scores. Pure read of foldWeights + stdFeatures;
            // never perturbs finalScores / q-values. Serial in row/index order.
            // NOTE: this direct path scores each entry with its held-out fold
            // model (the cross-validation ensemble), so the table characterizes
            // the AVERAGED model -- the same object the production streaming path
            // (ScorePopulationAndComputeFdr) actually scores with -- not the
            // per-entry CV ensemble that produced finalScores here. The averaged
            // model is the right thing to report (it is what ships); this direct
            // path is test / small-input standalone only.
            var avgWeights = new double[nFeatures];
            double avgBias = 0.0;
            for (int f = 0; f < foldWeights.Count; f++)
            {
                double[] foldW = foldWeights[f];
                for (int j = 0; j < nFeatures; j++)
                    avgWeights[j] += foldW[j];
                avgBias += foldBiases[f];
            }
            double nFoldsD = foldWeights.Count;
            for (int j = 0; j < nFeatures; j++)
                avgWeights[j] /= nFoldsD;
            avgBias /= nFoldsD;

            var contribSumTarget = new double[nFeatures];
            var contribSumDecoy = new double[nFeatures];
            long contribNTgt = 0, contribNDcy = 0;
            for (int i = 0; i < n; i++)
            {
                if (labels[i])   // labels[i] == IsDecoy
                {
                    contribNDcy++;
                    for (int j = 0; j < nFeatures; j++)
                        contribSumDecoy[j] += stdFeatures[i, j];
                }
                else
                {
                    contribNTgt++;
                    for (int j = 0; j < nFeatures; j++)
                        contribSumTarget[j] += stdFeatures[i, j];
                }
            }
            ComputeAndEmitFeatureContributions(avgWeights, contribSumTarget, contribSumDecoy,
                contribNTgt, contribNDcy, config.FeatureNames, config.FeatureLabels,
                config.ReversedScore,
                config.Diagnostics != null && config.Diagnostics.DumpFeatureContrib);

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
                IterationsPerFold = iterationsPerFold,
                AvgWeights = avgWeights,
                AvgBias = avgBias
            };
        }

        /// <summary>
        /// Streaming-path continuation: given the <paramref name="trainResults"/>
        /// returned by <see cref="RunPercolator"/> with <c>TrainOnly = true</c>
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
            PercolatorConfig config)
        {
            int n = entries.Count;
            if (n == 0)
            {
                return new PercolatorResults
                {
                    Entries = new List<PercolatorResult>(),
                    FoldWeights = trainResults.FoldWeights,
                    FoldBiases = trainResults.FoldBiases,
                    Standardizer = trainResults.Standardizer,
                    IterationsPerFold = trainResults.IterationsPerFold
                };
            }

            int nFeatures = entries[0].Features.Length;
            int nModels = trainResults.FoldWeights.Count;
            if (nModels == 0)
                throw new InvalidOperationException(
                    @"ScorePopulationAndComputeFdr: trainResults contains no fold models");

            // Average fold weights + biases. Matches Rust streaming:
            //   avg_weights[j] = mean_f(fold_weights[f][j])
            //   avg_bias       = mean_f(fold_biases[f])
            var avgWeights = new double[nFeatures];
            double avgBias = 0.0;
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
            var sumTarget = new double[nFeatures];
            var sumDecoy = new double[nFeatures];
            long nTgt = 0, nDcy = 0;
            for (int i = 0; i < n; i++)
            {
                var entry = entries[i];
                labels[i] = entry.IsDecoy;
                entryIds[i] = entry.EntryId;
                peptides[i] = entry.Peptide;
                fileNames[i] = entry.FileName;

                Array.Copy(entry.Features, 0, featureBuf, 0, nFeatures);
                standardizer.TransformSlice(featureBuf);
                double score = avgBias;
                for (int j = 0; j < nFeatures; j++)
                    score += avgWeights[j] * featureBuf[j];
                finalScores[i] = score;

                if (entry.IsDecoy)
                {
                    nDcy++;
                    for (int j = 0; j < nFeatures; j++)
                        sumDecoy[j] += featureBuf[j];
                }
                else
                {
                    nTgt++;
                    for (int j = 0; j < nFeatures; j++)
                        sumTarget[j] += featureBuf[j];
                }
            }

            ComputeAndEmitFeatureContributions(avgWeights, sumTarget, sumDecoy, nTgt, nDcy,
                config.FeatureNames, config.FeatureLabels, config.ReversedScore,
                config.Diagnostics != null && config.Diagnostics.DumpFeatureContrib);

            // PEP via global target-decoy competition. CompeteAll returns
            // winners sorted by score-descending (matches the direct-path
            // Percolator flow and is what ComputeConservativeQvalues needs
            // downstream). Rust's streaming path uses compute_fdr_from_stubs
            // instead, which iterates a base_id-ascending-sorted union of
            // targets + decoys -- because PepEstimator.FitDefault's KDE sum
            // is NOT associative and HashMap iteration order would leak 1-
            // ULP-level noise into every PEP value. For cross-impl byte
            // parity we must feed PepEstimator in the same base_id-sorted
            // order Rust uses, not CompeteAll's score-sorted order. Reorder
            // a parallel copy here; the score-sorted arrays stay intact for
            // the per-run / experiment q-value calls below.
            int[] winnerIndices;
            double[] winnerScores;
            bool[] winnerIsDecoy;
            CompeteAll(finalScores, labels, entryIds,
                out winnerIndices, out winnerScores, out winnerIsDecoy);

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
            var peps = new double[n];
            for (int i = 0; i < n; i++)
                peps[i] = 1.0;
            foreach (int idx in winnerIndices)
                peps[idx] = pepEstimator.PosteriorError(finalScores[idx]);

            // Per-run precursor + peptide q-values (each file independently).
            var runPrecursorQvalues = ComputePerRunPrecursorQvalues(
                finalScores, labels, entryIds, fileNames);
            var runPeptideQvalues = ComputePerRunPeptideQvalues(
                finalScores, labels, entryIds, fileNames, peptides);

            // Experiment-level q-values: single-file shortcut matches
            // direct-path semantics.
            var uniqueFiles = new HashSet<string>(fileNames);
            bool isSingleFile = uniqueFiles.Count <= 1;
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
                FoldWeights = trainResults.FoldWeights,
                FoldBiases = trainResults.FoldBiases,
                Standardizer = standardizer,
                IterationsPerFold = trainResults.IterationsPerFold,
                AvgWeights = avgWeights,
                AvgBias = avgBias
            };
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
                        reports.Sort((a, b) => a.Fold.CompareTo(b.Fold));
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
            out int bestIteration)
        {
            int nFeatures = stdFeatures.Cols;
            var currentScores = (double[])initialScores.Clone();

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
                    svmFeatures = ExtractRowsInto(stdFeatures, svmGlobalIndices, foldScratch.TrainData);
                }
                else
                {
                    svmFeatures = ExtractRows(stdFeatures, svmGlobalIndices);
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
                var svmFoldAssignments = CreateStratifiedFoldsByPeptide(
                    svmLabels, svmPeptides, svmEntryIds, config.NFolds);

                double bestC = GridSearchC(
                    svmFeatures, svmLabels, svmEntryIds,
                    config.CValues, svmFoldAssignments, config.NFolds,
                    config.Seed, trainFdr, svmScratchPool);

                // iii. Train SVM with best C
                var model = LinearSvmClassifier.Train(
                    svmFeatures, svmLabels, bestC, config.Seed, foldScratch);

                // iv. Score ALL training set entries with new model
                // trainFeatures is live just for the DecisionFunction call;
                // foldScratch.TestData is not used elsewhere in this iteration
                // (svmFeatures is in TrainData), so reuse it here.
                Matrix trainFeatures;
                if (foldScratch != null)
                {
                    foldScratch.EnsureExtractCapacity(trainIndices.Length, nFeatures);
                    trainFeatures = ExtractRowsInto(stdFeatures, trainIndices, foldScratch.TestData);
                }
                else
                {
                    trainFeatures = ExtractRows(stdFeatures, trainIndices);
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
                        trainFeatures = ExtractRowsInto(features, trainIdx.ToArray(), localScratch.TrainData);
                        testFeatures = ExtractRowsInto(features, testIdx.ToArray(), localScratch.TestData);
                    }
                    else
                    {
                        trainFeatures = ExtractRows(features, trainIdx.ToArray());
                        testFeatures = ExtractRows(features, testIdx.ToArray());
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

            // Propagate the winner's q-value to all observations sharing the
            // same base_id (both target and decoy sides). Matches Rust's
            // base_id_exp_prec_q HashMap at osprey-fdr/src/percolator.rs:2168
            // -- without this, non-winning per-file observations of a
            // multi-file precursor stay at q=1.0 and downstream stages that
            // gate on experiment_precursor_qvalue (Stage 6 calibration refit
            // and reconciliation) miss the bulk of the consensus pool.
            var baseIdExpQ = new Dictionary<uint, double>();
            for (int rank = 0; rank < wi.Length; rank++)
            {
                uint baseId = entryIds[wi[rank]] & BASE_ID_MASK;
                baseIdExpQ[baseId] = q[rank];
            }
            for (int i = 0; i < n; i++)
            {
                uint baseId = entryIds[i] & BASE_ID_MASK;
                double qv;
                if (baseIdExpQ.TryGetValue(baseId, out qv))
                    qvalues[i] = qv;
            }

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
        /// Build the SVM training subset for one Percolator pass: best-per-precursor
        /// dedup, then -- only when the deduped count still exceeds
        /// <paramref name="maxTrainSize"/> -- a peptide-grouped subsample. Returns
        /// indices into the original <paramref name="entries"/> list;
        /// <paramref name="bestPerPrecursor"/> returns the post-dedup indices so the
        /// caller can emit its own path-specific [COUNT] dedup line. Owned here so
        /// the direct (<see cref="RunPercolator"/>) and streaming
        /// (PercolatorEngine.RunPercolatorStreaming) paths select identical subsets
        /// for identical input instead of hand-mirroring the dedup + index map-back.
        /// </summary>
        internal static int[] BuildTrainingSubset(
            bool[] labels, uint[] entryIds, string[] peptides,
            IList<PercolatorEntry> entries, int maxTrainSize, ulong seed,
            out int[] bestPerPrecursor)
        {
            bestPerPrecursor = SelectBestPerPrecursor(labels, entryIds, entries);
            if (maxTrainSize <= 0 || bestPerPrecursor.Length <= maxTrainSize)
                return bestPerPrecursor;

            // Project the deduped rows into local arrays, subsample by peptide
            // group, then map the local selection back to entries indices.
            int m = bestPerPrecursor.Length;
            var dedupLabels = new bool[m];
            var dedupEntryIds = new uint[m];
            var dedupPeptides = new string[m];
            for (int i = 0; i < m; i++)
            {
                int gi = bestPerPrecursor[i];
                dedupLabels[i] = labels[gi];
                dedupEntryIds[i] = entryIds[gi];
                dedupPeptides[i] = peptides[gi];
            }

            int[] localSelected = SubsampleByPeptideGroup(
                dedupLabels, dedupEntryIds, dedupPeptides, maxTrainSize, seed);
            var trainSubset = new int[localSelected.Length];
            for (int i = 0; i < localSelected.Length; i++)
                trainSubset[i] = bestPerPrecursor[localSelected[i]];
            return trainSubset;
        }

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
            Array.Sort(result); // Array.Sort OK: result holds unique entry indices (one per base_id from bestTarget/bestDecoy), so no ties
            return result;
        }

        /// <summary>
        /// Subsample entries by peptide group, keeping target-decoy pairs and charge states together.
        /// </summary>
        internal static int[] SubsampleByPeptideGroup(
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
            // EntryId is NOT unique in the 2nd-pass entries[] vector --
            // a single (base_id, charge) precursor observed across N
            // files contributes N entries with the same EntryId, and
            // post-reconciliation gap-fill can add yet more duplicates
            // at the same (EntryId, Charge, ScanNumber). Tie-break on
            // the input index a (native_position) so the dump order
            // is deterministic AND matches Rust's stable
            // sort_by_key(|&i| entries[i].entry_id), which preserves
            // native_position order at duplicate EntryIds.
            Array.Sort(order, (a, b) => // Array.Sort OK: tie-break on native_position (the input index a/b) makes the comparator total
            {
                int c = entries[a].EntryId.CompareTo(entries[b].EntryId);
                return c != 0 ? c : a.CompareTo(b);
            });

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
            OspreyOutput.Out.WriteLine(@"Wrote Stage 5 subsample dump: {0} ({1} rows)", path, n);
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
                        sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(weights[wi]));
                        sw.Write('\t'); sw.WriteLine(iters.ToString(inv));
                    }
                    sw.Write(fold.ToString(inv));
                    sw.Write('\t'); sw.Write(weights.Length.ToString(inv));
                    sw.Write('\t'); sw.Write(@"bias");
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(model.Bias));
                    sw.Write('\t'); sw.WriteLine(iters.ToString(inv));
                }
            }
            OspreyOutput.Out.WriteLine(@"Wrote Stage 5 SVM weights dump: {0} ({1} folds)", path, foldModels.Length);
        }

        /// <summary>
        /// Skyline-style per-feature percent-contribution decomposition of the
        /// trained linear model, emitted to <c>OspreyOutput.Out</c> after Stage 5
        /// training. Ports TargetDecoyGenerator.GetPercentContribution: for each
        /// feature
        ///   contribution_j = w_j*(meanTarget_j - meanDecoy_j) / sum_k w_k*(meanTarget_k - meanDecoy_k)
        /// so the percentages sum to 100% by linearity (the denominator is the sum of
        /// the numerators). All means are over the STANDARDIZED feature space the SVM
        /// scores in (the same space <paramref name="avgWeights"/> live in), so the
        /// product w_j*deltaMu_j is space-invariant.
        ///
        /// Pure reporting: a read of <paramref name="avgWeights"/> and the
        /// accumulated target/decoy sums. It never mutates the model or the features
        /// and must never move q-values. The signed percent is emitted as-is; the
        /// unexpected-direction flag is Skyline's weight-sign test
        /// <c>IsReversedScore XOR (weight &lt; 0)</c> (independent of the data).
        /// </summary>
        private static void ComputeAndEmitFeatureContributions(
            double[] avgWeights,
            double[] sumTarget, double[] sumDecoy, long nTarget, long nDecoy,
            string[] featureNames, string[] featureLabels, bool[] reversedScore,
            bool dumpTsv)
        {
            int p = avgWeights.Length;
            var deltaMu = new double[p];
            var weighted = new double[p];
            double composite = 0.0;
            for (int j = 0; j < p; j++)
            {
                double mt = nTarget > 0 ? sumTarget[j] / nTarget : 0.0;
                double md = nDecoy > 0 ? sumDecoy[j] / nDecoy : 0.0;
                deltaMu[j] = mt - md;
                weighted[j] = avgWeights[j] * deltaMu[j];
                composite += weighted[j];   // == sum_k w_k*deltaMu_k == deltaMu_composite
            }
            // Targets should score above decoys, so composite > 0. Guard /0 for a
            // degenerate model (percentages become NaN, which the report shows plainly).
            bool ok = Math.Abs(composite) > 1e-12;

            OspreyOutput.Out.WriteLine(
                "  Feature weight contributions (trained linear model, coefficients standardized):");
            OspreyOutput.Out.WriteLine("    {0,-36} {1,12} {2,9}", "feature", "coefficient", "percent");
            for (int j = 0; j < p; j++)
            {
                double pct = ok ? 100.0 * weighted[j] / composite : double.NaN;
                bool wrongSign = reversedScore != null && j < reversedScore.Length &&
                                 (reversedScore[j] ^ (avgWeights[j] < 0.0));
                string label = (featureLabels != null && j < featureLabels.Length)
                    ? featureLabels[j]
                    : (featureNames != null && j < featureNames.Length ? featureNames[j] : string.Format("feature_{0}", j));
                OspreyOutput.Out.WriteLine("    {0,-36} {1,12:F4} {2,8:F1}%{3}",
                    label, avgWeights[j], pct, wrongSign ? "  (unexpected direction)" : string.Empty);
            }

            if (dumpTsv)
                WriteFeatureContributionsDump(avgWeights, deltaMu, weighted, composite,
                    ok, featureNames, reversedScore);
        }

        /// <summary>
        /// Optional machine-precision TSV form of the feature-contribution table
        /// (gated by OSPREY_DUMP_FEATURE_CONTRIB), for cross-impl / cross-run diffing.
        /// Mirrors the f64-roundtrip formatting of the Stage 5 dumps. Columns:
        /// feature_idx, feature_name, coefficient, delta_mu, contribution_pct,
        /// reversed, wrong_sign.
        /// </summary>
        private static void WriteFeatureContributionsDump(
            double[] avgWeights, double[] deltaMu, double[] weighted, double composite,
            bool ok, string[] featureNames, bool[] reversedScore)
        {
            const string path = @"cs_feature_contributions.tsv";
            var inv = CultureInfo.InvariantCulture;
            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"feature_idx	feature_name	coefficient	delta_mu	contribution_pct	reversed	wrong_sign");
                for (int j = 0; j < avgWeights.Length; j++)
                {
                    double pct = ok ? 100.0 * weighted[j] / composite : double.NaN;
                    bool reversed = reversedScore != null && j < reversedScore.Length && reversedScore[j];
                    bool wrongSign = reversedScore != null && j < reversedScore.Length &&
                                     (reversedScore[j] ^ (avgWeights[j] < 0.0));
                    string name = (featureNames != null && j < featureNames.Length)
                        ? featureNames[j]
                        : @"unknown";
                    sw.Write(j.ToString(inv));
                    sw.Write('\t'); sw.Write(name);
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(avgWeights[j]));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(deltaMu[j]));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(pct));
                    sw.Write('\t'); sw.Write(reversed ? @"true" : @"false");
                    sw.Write('\t'); sw.WriteLine(wrongSign ? @"true" : @"false");
                }
            }
            OspreyOutput.Out.WriteLine(@"Wrote feature contributions dump: {0} ({1} features)",
                path, avgWeights.Length);
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
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(means[i]));
                    sw.Write('\t'); sw.WriteLine(Diagnostics.FormatF64Roundtrip(stds[i]));
                }
            }
            OspreyOutput.Out.WriteLine(@"Wrote Stage 5 standardizer dump: {0} ({1} features)", path, means.Length);
        }

        /// <summary>
        /// One-shot diagnostic dump of the raw per-entry feature vectors
        /// fed into FeatureStandardizer.FitTransform. Mirrors Rust
        /// dump_stage5_perc_input. Writes cs_stage5_perc_input.tsv with
        /// columns native_position, entry_id, is_decoy, &lt;features...&gt;
        /// sorted by (entry_id, native_position).
        /// </summary>
        private static void WriteStage5PercInputDump(
            IList<PercolatorEntry> entries,
            string[] featureNames)
        {
            const string path = @"cs_stage5_perc_input.tsv";
            var inv = CultureInfo.InvariantCulture;
            int nFeatures = entries.Count > 0 ? entries[0].Features.Length : 0;
            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.Write(@"native_position	entry_id	is_decoy");
                for (int i = 0; i < nFeatures; i++)
                {
                    string name = (featureNames != null && i < featureNames.Length)
                        ? featureNames[i]
                        : @"unknown";
                    sw.Write('\t'); sw.Write(name);
                }
                sw.WriteLine();

                int n = entries.Count;
                int[] order = new int[n];
                for (int i = 0; i < n; i++) order[i] = i;
                Array.Sort(order, (a, b) => // Array.Sort OK: tie-break on native_position (the input index a/b) makes the comparator total
                {
                    int c = entries[a].EntryId.CompareTo(entries[b].EntryId);
                    return c != 0 ? c : a.CompareTo(b);
                });

                foreach (int idx in order)
                {
                    var e = entries[idx];
                    sw.Write(idx.ToString(inv));
                    sw.Write('\t'); sw.Write(e.EntryId.ToString(inv));
                    sw.Write('\t'); sw.Write(e.IsDecoy ? @"true" : @"false");
                    for (int i = 0; i < e.Features.Length; i++)
                    {
                        sw.Write('\t');
                        sw.Write(Diagnostics.FormatF64Roundtrip(e.Features[i]));
                    }
                    sw.WriteLine();
                }
            }
            OspreyOutput.Out.WriteLine(@"Wrote Stage 5 Percolator input dump: {0} ({1} rows)", path, entries.Count);
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

        /// <summary>
        /// Variant of <see cref="ExtractRows"/> that writes into a
        /// caller-supplied <paramref name="destData"/> buffer (must be
        /// at least <c>rowIndices.Length * matrix.Cols</c> long) and
        /// wraps the prefix as a Matrix. Avoids the ~8 MB LOH allocation
        /// per call on HRAM Astral. The trailing unused suffix of
        /// <paramref name="destData"/> is left untouched (Matrix.Rows
        /// hides it).
        /// </summary>
        private static Matrix ExtractRowsInto(Matrix matrix, int[] rowIndices, double[] destData)
        {
            int nCols = matrix.Cols;
            int nRows = rowIndices.Length;
            int need = nRows * nCols;
            if (destData.Length < need)
                throw new ArgumentException(
                    string.Format("destData length {0} < required {1}", destData.Length, need));
            double[] src = matrix.Data;
            for (int i = 0; i < nRows; i++)
            {
                int srcOffset = rowIndices[i] * nCols;
                int dstOffset = i * nCols;
                Array.Copy(src, srcOffset, destData, dstOffset, nCols);
            }
            // Pool-friendly wrap: Matrix.WrapPrefixNoClone accepts a
            // backing array >= rows*cols. The trailing suffix of
            // destData (from prior larger calls) is left untouched and
            // never read.
            return Matrix.WrapPrefixNoClone(destData, nRows, nCols);
        }
    }
}
