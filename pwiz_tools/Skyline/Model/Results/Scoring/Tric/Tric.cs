/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring.Tric
{
    /// <summary>
    /// Performs the TRansfer of Identification Confidence (TRIC) algorithm
    /// http://dx.doi.org/10.1038/nmeth.3954
    /// This involves doing a retention time alignment between the detected peaks in all of the replicates.
    /// </summary>
    public class Tric
    {
        // Progress percentages
        private const int PERCENT_STATS = 20;
        public const int PERCENT_EDGE_WEIGHTS = 40;
        public const int PERCENT_LEARN_TREE = 5;
        public const int PERCENT_TRAIN_ALINGERS = 35;
        // 5% for peak picking


        public enum SeedScores
        {
            all,
            single,
            none
        }

        public enum TreeSearchMethod
        {
            mst,
            star
        }

        #region hyperparameters

        /// <summary>
        /// Q value cutoff used for best the best scoring peak to act as a seed for the others.
        /// Peptides with no peak in any run below this threshold are not included in the mProphet training.
        /// This needs to be high enough to allow enough decoys into the next round to support training.
        /// </summary>
        private const double SEED_CUTOFF = 0.01;

        /// <summary>
        /// Q value cutoff used for choosing times to train alignment functions
        /// </summary>
        private const double ALIGNMENT_CUTOFF = 0.0001;

        /// <summary>
        /// Q value cutoff used for allowing a peak to be considered for alignment to the best peak in all runs
        /// </summary>
        private const double EXTENSION_SCORE_CUTOFF = 0.1;

        /// <summary>
        /// Multiplier applied to RMSD in retention time alignment as a possible maximum retention time delta
        /// allowed from predicted aligned RT to be considered aligned to the best peak, if the resulting value
        /// is larger than MIN_RT_DIFF. (See original TRIC paper)
        /// </summary>
        private const double RMSD_MULTIPLIER = 3.0;

        /// <summary>
        /// Absolute maximum retention time delta allowed from predicted aligned RT to be considered aligned
        /// to the best peak. (See original TRIC paper)
        /// </summary>
        private const double MIN_RT_DIFF = 1;

        /// <summary>
        /// Train mProphet with all scores from the best scoring peak across all runs for each
        /// peptide if all, just the single round 1 trained score if single and none else. Consider risk
        /// of overtraining with using the single pre-trained score.
        /// </summary>
        public static SeedScores SEED_SCORES_USED
        {
            get { return SeedScores.none; }
        }

        /// <summary>
        /// Adds a score for the intensity dot-product between the best scoring peak across all runs
        /// with each other peak. Consider that the best scoring peak for any peptide will always
        /// score zero.
        /// </summary>
        public static bool USE_SEED_DOT_PRODUCT_CORRELATION_SCORE
        {
            get { return false; }
        }

        /// <summary>
        /// If true then we calculate QValues using only PValues from the best peakgroup of each peptide, using 
        /// both targets and decoys. This creates Peptide level QValues
        /// </summary>
        public static bool CALCULATE_BEST_OVERALL_QVALUES_FOR_SEED_SELECTION
        {
            get { return true; }
        }

        /// <summary>
        /// If false don't do rescoring and LDA to calculate QValues
        /// </summary>
        public static bool RESCORE_PICKED_PEAKS
        {
            get { return false; }
        }

        /// <summary>
        /// If true then instead of interpolating from pVAlues to get qValues,
        ///we instead calculate a QValue at the peptide level and use that as
        ///the qValue for all peakgroups from the same peptide.
        ///Only should be set true if CALCULATE_BEST_OVERALL_QVALUES_FOR_SEED_SELECTION is true
        /// as well
        /// </summary>
        public static bool USE_GLOBAL_Q_VALUES
        {
            get { return false; }
        }

        /// <summary>
        /// What method will we use to build the Alignment Tree for TRIC
        /// </summary>
        public static TreeSearchMethod TREE_SEARCH_METHOD
        {
            get { return TreeSearchMethod.mst; }
        }

        #endregion

        private static float MaximumRTDiff(double rmsd)
        {
            return (float) Math.Max(MIN_RT_DIFF, RMSD_MULTIPLIER * rmsd);
        }

        private readonly double _alignmentCutoff;
        private readonly double _seedCutoff;
        private readonly double _extensionCutoff;
        private readonly IEnumerable<PeptideFileFeatureSet> _peptides;
        private readonly int _fileCount;
        private readonly IList<int> _fileIndexes;
        private readonly int _featureStatCount;
        private readonly RegressionMethodRT _regressionMethod;
        public TricTree _tree;
        private readonly IDictionary<int, string> _fileNames;

        public Tric(IEnumerable<PeptideFileFeatureSet> peptides,
            IList<string> fileNames,
            IList<int> fileIndexes,
            int featureStatCount,
            double alignmentCutoff,
            double seedCutoff,
            double extensionCutoff,
            RegressionMethodRT regressionMethod)
        {
            _fileCount = fileIndexes.Count;
            Assume.IsTrue(fileIndexes.Count == fileNames.Count);
            _fileNames = new Dictionary<int, string>();
            _fileIndexes = fileIndexes;
            for (int i = 0; i < _fileIndexes.Count; i++)
            {
                _fileNames[_fileIndexes[i]] = fileNames[i];
            }
            _featureStatCount = featureStatCount;
            _alignmentCutoff = alignmentCutoff;
            _seedCutoff = seedCutoff;
            _extensionCutoff = extensionCutoff;
            _peptides = peptides;
            _regressionMethod = regressionMethod;
        }

        public ConcurrentBag<TricPeptide> Predictions { get; private set; }

        public static void Rescore(PeakScoringModelSpec originalScoringModel,
            Dictionary<PeakTransitionGroupIdKey, PeakFeatureStatistics> statDict,
            PeakTransitionGroupFeatureSet featureSet,
            IList<string> fileNames,
            IList<int> fileIndexes,
            IList<double> origScores,
            IList<double> origQValues,
            IProgressMonitor progressMonitor,
            string documentPath,
            TextWriter output = null)
        {
            // First initialize transfer of confidence alignment
            var tric = InitializeAndRunTric(statDict, featureSet, fileNames, fileIndexes, origScores, origQValues,
                progressMonitor);

            // Train the model and append the scores for retention time distance etc.
            var featureScoreCalculator = new TricFeatureScoreCalculator(
                SEED_SCORES_USED,
                USE_SEED_DOT_PRODUCT_CORRELATION_SCORE,
                originalScoringModel);


            if (RESCORE_PICKED_PEAKS)
            {
                // Collect up the feature scores for the best run for each peptide.
                var targets = new List<IList<float[]>>(featureSet.TargetCount);
                var decoys = new List<IList<float[]>>(featureSet.DecoyCount);

                var targetIds = new List<PeakTransitionGroupIdKey>(featureSet.TargetCount);

                //Iterate over each peptide
                foreach (var prediction in tric.Predictions)
                {
                    //Set no best peak if no seed was found.
                    if (prediction.Seed == null)
                    {
                        foreach (var tricStat in prediction.Peptide.FileValues)
                        {
                            tricStat.FeatureStats.SetNoBestPeak();
                        }
                        continue;
                    }

                    var isDecoy = prediction.Seed.Features.IsDecoy;

                    //Iterate over each file and if peak_group was found 
                    //store its feature scores for LDA
                    foreach (var tricStat in prediction.Peptide.FileValues)
                    {
                        var alignedIndex = tricStat.AlignedPeakIndex;
                        //No peak found, then make sure FeatureStats agrees
                        if (alignedIndex == -1)
                        {
                            tricStat.FeatureStats.SetNoBestPeak();
                            continue;
                        }
                        if (!isDecoy)
                        {
                            targetIds.Add(tricStat.Features.Key);
                        }
                        tricStat.FeatureStats.ResetBestPeak(tricStat.AlignedPeakIndex, tricStat.Features);

                        var featureArray = featureScoreCalculator.GetFeatureArray(tricStat, prediction);

                        if (isDecoy)
                            decoys.Add(new SingletonList<float[]>(featureArray));
                        else
                            targets.Add(new SingletonList<float[]>(featureArray));
                    }
                }

                //Cannot do any FDR Estimation with zero decoys
                //TODO is this threshold too low?
                if (decoys.Count == 0)
                    throw new Exception(
                        Resources.Tric_Rescore_Not_enough_high_scoring_Decoys_found_in_run_to_run_alignment_);
                var initWeights = featureScoreCalculator.GetInitialFeatureWeights();

                //train final model
                var initParams = new LinearModelParams(initWeights);
                var finalModel = new MProphetPeakScoringModel("FinalModel", initParams, null, true);
                finalModel = (MProphetPeakScoringModel) finalModel.Train(targets, decoys, initParams,
                    null, false, false, progressMonitor, documentPath);

                //Write final model parameters to console
                if (output != null)
                {
                    var names = featureScoreCalculator.GetFeatureNames();
                    var weights = finalModel.Parameters.Weights;
                    Assume.IsTrue(names.Length == weights.Count);
                    for (int i = 0; i < names.Length; i++)
                    {
                        output.WriteLine("{0}: {1:F04}", names[i], weights[i]); // Not L10N
                    }
                }
                List<double> pValues, scores;
                CalcScores(targets, finalModel, out scores, out pValues);
                //TODO what should lambda and piZeroMinBe ?
                var qValues = new Statistics(pValues).Qvalues(0.95, 0.005);
                // make sure to reset the scores
                for (int i = 0; i < qValues.Length; i++)
                {
                    var stat = statDict[targetIds[i]];
                    stat.ResetBestScores((float) scores[i], qValues[i]);
                }
            }
            // If not rescoring, then either use interpolated qValues or
            // peptide level qValues depending on our hyperparameter settings
            else
            {
                var interpolator = new ValueInterpolation(origQValues, origScores);
                //Iterate peptides
                foreach (var prediction in tric.Predictions)
                {
                    //Set no best peak if no seed was found.
                    if (prediction.Seed == null)
                    {
                        foreach (var tricStat in prediction.Peptide.FileValues)
                        {
                            tricStat.FeatureStats.SetNoBestPeak();
                        }
                        continue;
                    }

                    //Iterate files
                    foreach (var tricStat in prediction.Peptide.FileValues)
                    {
                        var alignedIndex = tricStat.AlignedPeakIndex;
                        if (alignedIndex == -1)
                        {
                            tricStat.FeatureStats.SetNoBestPeak();
                            continue;
                        }
                        tricStat.FeatureStats.ResetBestPeak(tricStat.AlignedPeakIndex, tricStat.Features);
                        //If we are doing peptide level scoring
                        //then every peak group picked by TRIC gets the peptide level
                        //score and qValue
                        if (USE_GLOBAL_Q_VALUES)
                        {
                            tricStat.FeatureStats.ResetBestScores(
                                (float) prediction.Peptide.GlobalBestScore,
                                prediction.Peptide.GlobalQValue
                            );
                        }
                        //Otherwise retain original mProphet score
                        //and interpolated qValue 
                        else
                        {
                            tricStat.FeatureStats.ResetBestScores(
                                tricStat.BestScore,
                                interpolator.GetValueAForValueB(
                                    tricStat.FeatureStats.MprophetScores[tricStat.AlignedPeakIndex]));
                        }
                    }
                }
            }
        }

        private static Tric InitializeAndRunTric(Dictionary<PeakTransitionGroupIdKey, PeakFeatureStatistics> statDict,
            PeakTransitionGroupFeatureSet featureSet,
            IList<string> fileNames,
            IList<int> fileIndexes,
            IList<double> origScores,
            IList<double> origQValues,
            IProgressMonitor progressMonitor)
        {
            IProgressStatus status =
                new ProgressStatus(Resources.Tric_InitializeTric_Rescoring_peptides_for_retention_time_alignment);
            if (progressMonitor != null)
                progressMonitor.UpdateProgress(status);

            // These two operations account for 20% of progress
            var peptides = GetStatsForAllPeptidesInAllRuns(featureSet, statDict, progressMonitor, ref status,
                PERCENT_STATS);

            var interpolation = new ValueInterpolation(origScores, origQValues);

            var alignmentScoreCutoff = interpolation.GetValueAForValueB(ALIGNMENT_CUTOFF);
            var seedScoreCutoff = CALCULATE_BEST_OVERALL_QVALUES_FOR_SEED_SELECTION
                ? GetSeedScoreCutoffUsingBestOverallPeaks(peptides, SEED_CUTOFF)
                : interpolation.GetValueAForValueB(SEED_CUTOFF);
            interpolation.GetValueAForValueB(SEED_CUTOFF);
            var extensionScoreCutoff = interpolation.GetValueAForValueB(EXTENSION_SCORE_CUTOFF);

            // Do a retention time alignment between all runs
            var tric = new Tric(peptides, fileNames, fileIndexes, fileIndexes.Count * peptides.Length,
                alignmentScoreCutoff, seedScoreCutoff, extensionScoreCutoff, RegressionMethodRT.loess);

            // And running the alignment is the other 80%
            tric.RunTric(progressMonitor, ref status, true);

            if (progressMonitor != null)
                progressMonitor.UpdateProgress(status.Complete());
            return tric;
        }

        private static double GetSeedScoreCutoffUsingBestOverallPeaks(PeptideFileFeatureSet[] peptides,
            double qValueCutoff)
        {
            var targets = peptides.Where(p => !p.IsDecoy).ToArray();
            var decoys = peptides.Where(p => p.IsDecoy).ToArray();
            var statTargetBestScores = new Statistics(targets.Select(p => p.GlobalBestScore));
            var statDecoyBestScores = new Statistics(decoys.Select(p => p.GlobalBestScore).ToList());
            var pValues = statDecoyBestScores.PvaluesNull(statTargetBestScores);
            var qValues = new Statistics(pValues).Qvalues(MProphetPeakScoringModel.DEFAULT_R_LAMBDA,
                MProphetPeakScoringModel.PI_ZERO_MIN);
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i].GlobalQValue = qValues[i];
            }
            var interpolation = new ValueInterpolation(statTargetBestScores.CopyList(), qValues);
            return interpolation.GetValueAForValueB(qValueCutoff);
        }


        private static void CalcScores(List<IList<float[]>> targets, MProphetPeakScoringModel finalModel,
            out List<double> scores, out List<double> pValues)
        {
            pValues = new List<double>();
            scores = new List<double>();
            //Iterate through all points, order matters
            foreach (var featureArray in targets.SelectMany(p => p))
            {
                var score = finalModel.Score(featureArray);
                var pValue = 1 - Statistics.PNorm(score);
                pValues.Add(pValue);
                scores.Add(score);
            }
        }


        /// <summary>
        /// For each peptide and each run, get the PeakFeatureStatistics
        /// </summary>
        /// <returns></returns>
        private static PeptideFileFeatureSet[] GetStatsForAllPeptidesInAllRuns(
            PeakTransitionGroupFeatureSet featuresSet,
            Dictionary<PeakTransitionGroupIdKey, PeakFeatureStatistics> featureStats,
            IProgressMonitor progressMonitor,
            ref IProgressStatus status,
            int percentRange)
        {
            // Bag of unordered features stats
            var bag = new ConcurrentBag<TricFileFeatureStatistics>(); // Just a list?
            int currentStat = 0;
            int totalStats = featureStats.Count*100/percentRange;
            foreach (var features in featuresSet.Features)
            {
                var featureStat = featureStats[features.Key];
                bag.Add(new TricFileFeatureStatistics(featureStat, features));

                if (progressMonitor != null)
                {
                    int? percentComplete = ProgressStatus.ThreadsafeIncementPercent(ref currentStat, totalStats);
                    if (percentComplete.HasValue)
                        progressMonitor.UpdateProgress(status = status.ChangePercentComplete(percentComplete.Value));
                }
            }

            return bag.GroupBy(tricStat => tricStat.Features.Key.PepIndex)
                .Select(g => new PeptideFileFeatureSet(g)).ToArray();
        }
        /// <summary>
        /// Create alignment tree and then do alignment on all peptides to pick peaks. 
        /// </summary>
        public void RunTric(IProgressMonitor progressMonitor, ref IProgressStatus status, bool verbose = false)
        {
            switch (TREE_SEARCH_METHOD)
            {
                case TreeSearchMethod.mst:
                    _tree = new TricMst(_peptides, _fileNames, _fileIndexes, _alignmentCutoff, _regressionMethod,
                        progressMonitor, ref status, verbose);
                    break;
                case TreeSearchMethod.star:
                    _tree = new TricStarTree(_peptides, _fileNames, _fileIndexes, _alignmentCutoff, _regressionMethod,
                        progressMonitor, ref status, verbose);
                    break;
            }

            if (progressMonitor != null)
                progressMonitor.UpdateProgress(status = status.ChangeMessage(Resources.Tric_RunTric_Picking_peaks));

            int featureStatsAligned = 0;
            int startPercent = status.PercentComplete;
            int percentRange = 99 - startPercent;

            Predictions = new ConcurrentBag<TricPeptide>();

            // Loop through the peak statistics for each peptide, find the best scoring replicate
            // Traverse the alignment tree starting from the best scoring replicate and 
            // store picked peak information in Predictions. 

            var statusParallel = status;
            ParallelEx.ForEach(_peptides, peptide =>
            {
                //If there are no files for this peptide, then add an empty prediction element to Predictions
                if (!peptide.FileFeatures.Any())
                {
                    ProgressStatus.ThreadsafeIncrementPercent(ref featureStatsAligned, peptide.Count, _featureStatCount);
                    Predictions.Add(TricPeptide.GetEmptyPredictions(peptide));
                    return;
                }

                //Find best scoring peakGroup across files.
                //Null if none make score cutoff
                var seed = peptide.GetSeed(_seedCutoff);

                // Maybe not fair to give this value
                if (seed != null)
                {
                    seed.DistanceFromAlignedRetentionTime = 0;
                }
                // If we can't find a seed under the cutoff simply don't choose a peak for all runs
                else
                {
                    foreach (var stat in peptide.FileValues)
                    {
                        if (stat == null)
                            continue;

                        stat.AlignedPeakIndex = -1;
                    }
                    Predictions.Add(TricPeptide.GetEmptyPredictions(peptide));
                    ProgressStatus.ThreadsafeIncrementPercent(ref featureStatsAligned, peptide.Count, _featureStatCount);
                    return;
                }
                seed.AlignedPeakIndex = seed.FeatureStats.BestScoreIndex;

                var seedIndex = seed.FileIndex;
                var seedPredElement = new PredictionElement
                {
                    Score = seed.FeatureStats.BestScore,
                    RetentionTime =
                        seed.Features.PeakGroupFeatures[seed.FeatureStats.BestScoreIndex].MedianRetentionTime
                };
                var predictions = new Dictionary<int, PredictionElement>(_fileCount);
                predictions[seedIndex] = seedPredElement;

                float squaredRtErrorSum = 0;
                var extensions = 0;

                //Traverse alignment tree starting from seed and try to find peaks in other files
                foreach (var directionalEdge in _tree.Traverse(seed.Features.Key.FileIndex, _fileNames))
                {
                    var sourceFileIndex = directionalEdge.SourceFileIndex;
                    var targetFileIndex = directionalEdge.TargetFileIndex;
                    PredictionElement sourcePrediction;
                    Assume.IsTrue(predictions.TryGetValue(sourceFileIndex, out sourcePrediction),
                        "Source Run not found in TRIC tree traversal. This should not happen"); // Not L10N
                    var sourceScore = sourcePrediction.Score;
                    var sourceRetentionTime = sourcePrediction.RetentionTime;
                    Assume.IsTrue(sourceRetentionTime.HasValue,
                        "Source retention was not calculated. This should not happen."); // Not L10N
                    var expectedRetentionTime = directionalEdge.Transform(sourceRetentionTime.Value);
                    Assume.IsFalse(predictions.ContainsKey(targetFileIndex));

                    TricFileFeatureStatistics targetStat;
                    //If null will be handled by extend. Just gives back predicted retention time
                    //as prediction
                    peptide.FileFeatures.TryGetValue(targetFileIndex, out targetStat);

                    //Extend and potentially pick a peak in target File
                    predictions[targetFileIndex] = Extend(targetStat,
                        _extensionCutoff,
                        expectedRetentionTime,
                        directionalEdge.Rmsd,
                        sourceScore,
                        ref squaredRtErrorSum,
                        ref extensions);
                }

                Predictions.Add(new TricPeptide(seed, peptide, (float) Math.Sqrt(squaredRtErrorSum / extensions),
                    (float) extensions / peptide.Count));

                if (progressMonitor != null)
                {
                    var percentComplete = ProgressStatus.ThreadsafeIncrementPercent(ref featureStatsAligned,
                        peptide.Count, _featureStatCount);
                    if (percentComplete.HasValue)
                        progressMonitor.UpdateProgress(
                            statusParallel =
                                statusParallel.ChangePercentComplete(startPercent +
                                                                     percentComplete.Value * percentRange / 100));
                }
            });
            status = statusParallel;
        }


        /// <summary>
        /// Loops through the peaks for a peptide in a particular replicate and finds the one with the highest score within
        /// the retention time window within Math.Max(RT_DIFF, RMSD_MULTIPLIER*rmsd) from expectedRt.
        /// </summary>
        private PredictionElement Extend(TricFileFeatureStatistics stat, double scoreCutoff, double expectedRt,
            double rmsd,
            double? lastScore, ref float squaredRtErrorSum, ref int extensions)
        {
            //If we don't contain chromatogram data for this file
            //Just give back empty prediction with expected rt
            if (stat == null)
            {
                return new PredictionElement
                {
                    Score = lastScore,
                    RetentionTime = expectedRt,
                };
            }
            var bestIndex = -1;
            double bestScore = Double.NegativeInfinity;
            var secondBestIndex = -1;
            double secondBestScore = Double.NegativeInfinity;
            var localRTDiff = MaximumRTDiff(rmsd);
            float bestOffset = 0;
            for (var i = 0; i < stat.FeatureStats.MprophetScores.Count; i++)
            {
                var offset = (float) Math.Abs(stat.Features.PeakGroupFeatures[i].MedianRetentionTime - expectedRt);
                if (offset < localRTDiff && stat.FeatureStats.MprophetScores[i] > scoreCutoff)
                {
                    if (stat.FeatureStats.MprophetScores[i] > bestScore)
                    {
                        secondBestIndex = bestIndex;
                        secondBestScore = bestScore;
                        bestIndex = i;
                        bestScore = stat.FeatureStats.MprophetScores[i];
                        bestOffset = offset;
                    }
                    // Somewhat problematic trying to imagine a second best peak falling within
                    // the cutoffs applied above
                    else if (stat.FeatureStats.MprophetScores[i] > secondBestScore)
                    {
                        secondBestIndex = i;
                        secondBestScore = stat.FeatureStats.MprophetScores[i];
                    }
                }
            }
            stat.AlignedPeakIndex = bestIndex;
            //If no peak group found just return
            //back expected RT
            if (bestIndex == -1)
            {
                return new PredictionElement
                {
                    Score = lastScore,
                    RetentionTime = expectedRt,
                };
            }
            stat.DistanceFromAlignedRetentionTime = bestOffset;

            squaredRtErrorSum += (float) Math.Pow(bestOffset / localRTDiff, 2);
            extensions++;
            var prediction = new PredictionElement
            {
                Score = stat.FeatureStats.MprophetScores[bestIndex],
                RetentionTime = stat.Features.PeakGroupFeatures[bestIndex].MedianRetentionTime,
                BestIndex = bestIndex
            };
            if (secondBestIndex != -1)
                prediction.SecondBestIndex = secondBestIndex;
            return prediction;
        }

        public class TricPeptide 
        {
            public TricPeptide(TricFileFeatureStatistics seed,
                PeptideFileFeatureSet peptide,
                float rmsdRtError,
                float extensionPercentage)
            {
                Seed = seed;
                Peptide = peptide;
                RmsdRtError = rmsdRtError;
                ExtensionPercentage = extensionPercentage;
            }

            public TricFileFeatureStatistics Seed { get; private set; }
            public PeptideFileFeatureSet Peptide { get; private set; }
            public float RmsdRtError { get; private set; }
            public float ExtensionPercentage { get; private set; }

            //0 predictions made 
            public static TricPeptide GetEmptyPredictions(PeptideFileFeatureSet peptide)
            {
                return new TricPeptide(null, peptide, 0, 0);
            }
        }

        // ReSharper disable UnusedAutoPropertyAccessor.Local
        private struct PredictionElement
        {
            public double? RetentionTime { get; set; }
            public double? Score { get; set; }
            public int? BestIndex { get; set; }
            public int? SecondBestIndex { get; set; }
        }
        // ReSharper restore UnusedAutoPropertyAccessor.Local
    }

    public class PeptideFileFeatureSet
    {
        public PeptideFileFeatureSet(IGrouping<int, TricFileFeatureStatistics> grouping)
        {
            PeptideIndex = grouping.Key;
            FileFeatures = new Dictionary<int, TricFileFeatureStatistics>();
            GlobalBestScore = float.MinValue;
            foreach (var fileFeatures in grouping)
            {
                FileFeatures.Add(fileFeatures.FileIndex, fileFeatures);
                if (fileFeatures.IsDecoy)
                    IsDecoy = true;
                if (GlobalBestScore < fileFeatures.BestScore)
                {
                    GlobalBestScore = fileFeatures.BestScore;
                    BestFileIndex = fileFeatures.FileIndex;
                }
            }
        }

        public int PeptideIndex { get; private set; }

        public bool IsDecoy { get; private set; }

        public int BestFileIndex { get; private set; }

        public double GlobalBestScore { get; private set; }

        public double GlobalQValue { get; internal set; }

        public IDictionary<int, TricFileFeatureStatistics> FileFeatures { get; private set; }

        public int Count
        {
            get { return FileFeatures.Count; }
        }

        public IEnumerable<TricFileFeatureStatistics> FileValues
        {
            get { return FileFeatures.Values; }
        }

        /// <summary>
        /// Finds the replicate with the best scoring peak under the given cut-off
        /// </summary>
        public TricFileFeatureStatistics GetSeed(double seedCutoff)
        {
            // Search interpolated q values, since they may have changed the ordering
            var best = FileFeatures[BestFileIndex];
            if (best.FeatureStats.BestScore > seedCutoff)
                return best;
            return null;
        }
    }

    public class TricFileFeatureStatistics
    {
        public TricFileFeatureStatistics(PeakFeatureStatistics featureStats, PeakTransitionGroupFeatures features)
        {
            FeatureStats = featureStats;
            Features = features;
        }

        public bool IsDecoy
        {
            get { return Features.IsDecoy; }
        }

        public int FileIndex
        {
            get { return Features.Key.FileIndex; }
        }

        public float BestScore
        {
            get { return FeatureStats.BestScore; }
        }

        public PeakFeatureStatistics FeatureStats { get; private set; }

        public PeakTransitionGroupFeatures Features { get; private set; }

        public float DistanceFromAlignedRetentionTime { get; internal set; }

        // Index of the peak group that was aligned using tric, if none then -1
        public int AlignedPeakIndex { get; internal set; }
    }

    /// <summary>
    /// Used for rescoring peak-groups after TRIC picks peaks if 
    /// we are doing rescoring
    /// </summary>
    public class TricFeatureScoreCalculator
    {
        private readonly Tric.SeedScores _seedScoresUsed;
        private readonly bool _useDotProductCorrelation;
        private readonly int _originalMprophetFeaturesCount;
        private readonly IPeakScoringModel _originalModel;

        private List<TricScoreCalculator> _scoreCalculators;


        public TricFeatureScoreCalculator(Tric.SeedScores seedScoresUsed, bool useDotProductCorrelation,
            IPeakScoringModel originalModel)
        {
            _seedScoresUsed = seedScoresUsed;
            _useDotProductCorrelation = useDotProductCorrelation;
            _originalModel = originalModel;
            _originalMprophetFeaturesCount = originalModel.PeakFeatureCalculators.Count;
            SetScoreCalculators();
        }

        private void SetScoreCalculators()
        {
            _scoreCalculators = new List<TricScoreCalculator>();
            if (_seedScoresUsed == Tric.SeedScores.all)
            {
                for (int i = 0; i < _originalMprophetFeaturesCount; i++)
                {
                    var calc = _originalModel.PeakFeatureCalculators[i];
                    _scoreCalculators.Add(new SeedMProphetCopyScore(i, calc));
                }
            }
            else if (_seedScoresUsed == Tric.SeedScores.single)
            {
                _scoreCalculators.Add(new SeedSingleScoreCalculator());
            }
            for (int i = 0; i < _originalMprophetFeaturesCount; i++)
            {
                var calc = _originalModel.PeakFeatureCalculators[i];
                _scoreCalculators.Add(new MProphetCopyScore(i, calc));
            }
            if (_useDotProductCorrelation)
                _scoreCalculators.Add(new DotProductCorrelationCalculator());
        }


        public float[] GetFeatureArray(TricFileFeatureStatistics alignedStat, Tric.TricPeptide peptide)
        {
            return _scoreCalculators.Select(calc => calc.GetScore(alignedStat, peptide)).ToArray();
        }

        public double[] GetInitialFeatureWeights()
        {
            return _scoreCalculators.Select(calc => calc.GetInitialWeight(_originalModel)).ToArray();
        }

        public string[] GetFeatureNames()
        {
            return _scoreCalculators.Select(calc => calc.Name).ToArray();
        }

        public abstract class TricScoreCalculator
        {
            public abstract float GetScore(TricFileFeatureStatistics alignedStat, Tric.TricPeptide peptide);

            public String Name { get; protected set; }

            public abstract double GetInitialWeight(IPeakScoringModel orignalModel);
        }

        /// <summary>
        /// This calculator simply copies scores calculated by mProphet calculators
        /// </summary>
        public class MProphetCopyScore : TricScoreCalculator
        {
            protected int _originalScoreIndex;

            public MProphetCopyScore(int originalScoreIndex, IPeakFeatureCalculator originalCalculator)
            {
                _originalScoreIndex = originalScoreIndex;
                Name = "(Target)" + originalCalculator.Name; // Not L10N
            }

            public override float GetScore(TricFileFeatureStatistics alignedStat, Tric.TricPeptide peptide)
            {
                var peakIndex = alignedStat.AlignedPeakIndex;
                return alignedStat.Features.PeakGroupFeatures[peakIndex].Features[_originalScoreIndex];
            }

            public override double GetInitialWeight(IPeakScoringModel orignalModel)
            {
                return orignalModel.Parameters.Weights[_originalScoreIndex];
            }
        }

        /// <summary>
        /// Copies the mProphet feature scores of the seed peak-group for the peptide 
        /// of this peak-group
        /// </summary>
        public class SeedMProphetCopyScore : MProphetCopyScore
        {
            public SeedMProphetCopyScore(int originalScoreIndex, IPeakFeatureCalculator originalCalculator)
                : base(originalScoreIndex, originalCalculator)
            {
                Name = "(Seed) " + originalCalculator.Name; // Not L10N 
            }

            public override float GetScore(TricFileFeatureStatistics alignedStat, Tric.TricPeptide peptide)
            {
                var seed = peptide.Seed;
                var peakIndex = seed.AlignedPeakIndex;
                return seed.Features.PeakGroupFeatures[peakIndex].Features[_originalScoreIndex];
            }
        }

        /// <summary>
        /// Gets average intensity dot-product correlation to the seed across
        /// all peak-groups selected by TRIC 
        /// </summary>
        public class DotProductCorrelationCalculator : TricScoreCalculator
        {
            public DotProductCorrelationCalculator()
            {
                Name = "TRIC Aligned intensity dot-product"; // Not L10N
            }

            public override float GetScore(TricFileFeatureStatistics alignedStat, Tric.TricPeptide peptide)
            {
                var sum = 0f;
                var count = 0;
                foreach (var stat in peptide.Peptide.FileValues)
                {
                    // skip seed
                    if (stat == peptide.Seed)
                        continue;
                    // Skip if no peak found
                    if (stat.AlignedPeakIndex == -1)
                        continue;

                    var seed = peptide.Seed;
                    var seedPeakIndex = seed.AlignedPeakIndex;
                    var alignedStatPeakIndex = stat.AlignedPeakIndex;
                    var seedPeakAreasList = seed.Features.PeakGroupFeatures[seedPeakIndex].PeakAreasList;
                    var statPeakAreasList = stat.Features.PeakGroupFeatures[alignedStatPeakIndex].PeakAreasList;
                    var bestDotProductScore = 0f;
                    // Find the best correlation between any two transitionGroups
                    foreach (var seedPeakAreas in seedPeakAreasList)
                    {
                        foreach (var alignedPeakAreas in statPeakAreasList)
                        {
                            var statPeakAreas = new Statistics(Array.ConvertAll(alignedPeakAreas, f => (double) f));
                            var statSeedAreas = new Statistics(Array.ConvertAll(seedPeakAreas, f => (double) f));

                            var dotProduct = (float) statPeakAreas.NormalizedContrastAngleSqrt(statSeedAreas);
                            if (double.IsNaN(dotProduct))
                                dotProduct = 0;
                            bestDotProductScore = Math.Max(dotProduct, bestDotProductScore);
                        }
                    }
                    sum += bestDotProductScore;
                    count++;
                }
                if (sum == 0)
                    return 0;
                return sum / count;
            }

            public override double GetInitialWeight(IPeakScoringModel orignalModel)
            {
                return 0;
            }
        }

        /// <summary>
        /// Copies the original mProphet score of the seed.
        /// </summary>
        public class SeedSingleScoreCalculator : TricScoreCalculator
        {
            public SeedSingleScoreCalculator()
            {
                Name = "(Seed) mProphet score"; // Not L10N
            }

            public override float GetScore(TricFileFeatureStatistics alignedStat, Tric.TricPeptide peptide)
            {
                return peptide.Seed.BestScore;
            }

            public override double GetInitialWeight(IPeakScoringModel orignalModel)
            {
                return 0;
            }
        }
    }
}

