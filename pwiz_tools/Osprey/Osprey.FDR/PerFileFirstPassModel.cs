/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// The per-run first-pass model: one model trained on one file's own rows, the way DIA-NN
    /// trains one model per run, and that file's rows scored with it.
    ///
    /// <para>The training rows are the file's best candidate peak per precursor, subsampled by
    /// peptide group to <see cref="PercolatorConfig.MaxTrainSize"/>. The file trains in two rounds
    /// through <see cref="PercolatorTrainer.TrainWithScorePrefilter"/>: round 1 on that subsample,
    /// round 2 on the peptide groups round 1 scores best, started from round 1's scores.
    /// At SEA-AD scale that subset covers most of the file's precursors, so the score pass is OUT
    /// OF FOLD: every row is scored by the one fold model that held its peptide group out, and only
    /// a row whose peptide group no fold trained on gets the fold mean.</para>
    ///
    /// <para>A safeguard then checks the model against the run's best single feature, as Percolator
    /// checks its learned direction against its best initial one: when that feature, used as the
    /// score, passes more of the run's targets at its precursor q than the model's out-of-fold
    /// scores do, the run is scored with the feature instead (<see cref="RunFirstPassModel.FallbackFeature"/>).
    /// On the regression Stellar runs the model passes three times what its best feature does; the
    /// check matters for a run of a few hundred precursors, whose conservative q passes nothing once one decoy ranks in its top hundred.</para>
    ///
    /// <para>The scores are then calibrated per file (Granholm et al. 2012, the map
    /// <c>CalibrateScoresBetweenFolds</c> applies between folds): the lowest target score passing
    /// the run's precursor q at <see cref="PercolatorConfig.TestFdr"/> maps to 0 and the median of the
    /// run's best decoy per entry to -1, or, when no target passes, the run's highest-scoring decoy
    /// to 0 (see <see cref="Calibrate"/>). Without it the experiment competition, which ranks each
    /// precursor by its best score over runs, would compare scores from unrelated models.</para>
    ///
    /// <para>One instance serves every file of a pass and is safe to call from several lanes at
    /// once; it accumulates the cost of each step for one [TIMING] line.</para>
    /// </summary>
    internal sealed class PerFileFirstPassModel
    {
        private readonly PercolatorConfig _trainConfig;
        private readonly int _threads;
        private readonly IOspreyLog _log;
        private readonly string _passLabel;
        // Round 2's row count; 0 for one round (OSPREY_GBT_PREFILTER_SIZE=0).
        private readonly int _round2Size;
        // --protein-boost best: the protein-best column each file computes from its OWN round-1
        // model and appends for round 2 and the score pass; null otherwise.
        private readonly ProteinBestRequest _ownProteinBest;
        private long _files;
        private long _trainTicks;
        private long _scoreTicks;
        private long _calibrateTicks;
        private long _trainRows;
        private long _bestDecoyAnchored;
        private long _excluded;
        private long _fallbacks;

        // How far below 0, in units of the run's own score range, an excluded run's scores are
        // moved: below any calibrated run's scores, so its rows never win an entry another run has.
        private const double EXCLUDED_OFFSET = 1e6;

        /// <param name="percConfig">The pass's config, whose classifier and settings each file's
        /// model is trained with.</param>
        /// <param name="lanes">How many files train at once, which divides the threads between
        /// them so the lanes do not oversubscribe the machine.</param>
        /// <param name="log">Where the two-round training reports its round-2 subset.</param>
        /// <param name="passLabel">The pass label for those lines.</param>
        public PerFileFirstPassModel(PercolatorConfig percConfig, int lanes, IOspreyLog log, string passLabel)
        {
            _log = log;
            _passLabel = passLabel;
            // The file's own column, when the search asks for one, reaches its model only through
            // round 2.
            _ownProteinBest = percConfig.ProteinBest;
            _threads = Math.Max(1, percConfig.NThreads / Math.Max(1, lanes));
            _trainConfig = percConfig.CloneForTrainOnly();
            // OSPREY_FIRST_PASS_TRAIN_SIZE: round 1's rows, and two thirds of them for round 2
            // unless OSPREY_FIRST_PASS_ROUND2_SIZE names round 2's own size.
            int? trainSize = OspreyEnvironment.FirstPassTrainSize;
            if (trainSize.HasValue)
                _trainConfig.MaxTrainSize = trainSize.Value;
            int round2Size = OspreyEnvironment.PrefilterSizeFor(true);
            if (trainSize.HasValue)
                round2Size = Math.Min(round2Size, trainSize.Value * 2 / 3);
            // The override never turns on a round 2 that OSPREY_GBT_PREFILTER_SIZE=0 turned off.
            if (OspreyEnvironment.FirstPassRound2Size.HasValue && round2Size > 0)
                round2Size = OspreyEnvironment.FirstPassRound2Size.Value;
            // Two rounds, as the first-pass model always trained.
            _round2Size = round2Size;
            _trainConfig.NThreads = _threads;
            _trainConfig.ReportTrainEntryFolds = true;
            // A file's training-set analysis would be a report nobody reads, at minutes per file.
            _trainConfig.CollectTrainingAnalysis = false;
            _trainConfig.Diagnostics = null;
            _trainConfig.PrefilterTrainSize = 0;
            _trainConfig.ProteinBest = null;
        }

        /// <summary>Whether each file computes its own protein-best column, and so wants its raw
        /// feature vectors rather than ones the global column already extends.</summary>
        public bool UsesOwnProteinBest => _ownProteinBest != null;

        /// <summary>
        /// Trains one file's model on its own rows and returns every row's calibrated, out-of-fold
        /// score, in row order.
        /// </summary>
        /// <param name="fileName">The file, carried on the training entries.</param>
        /// <param name="entryIds">Each row's entry_id.</param>
        /// <param name="charges">Each row's charge.</param>
        /// <param name="isDecoys">Each row's decoy flag.</param>
        /// <param name="coelutionSums">Each row's coelution sum, which ranks a precursor's peaks.</param>
        /// <param name="peptides">Each row's modified sequence.</param>
        /// <param name="featureRows">Each row's feature vector, by row index.</param>
        /// <param name="nFeatures">The width of a feature vector.</param>
        public double[] TrainAndScore(string fileName, IReadOnlyList<uint> entryIds, IReadOnlyList<byte> charges,
            IReadOnlyList<bool> isDecoys, IReadOnlyList<double> coelutionSums, IReadOnlyList<string> peptides,
            IReadOnlyList<double[]> featureRows, int nFeatures)
        {
            return TrainAndScore(fileName, entryIds, charges, isDecoys, coelutionSums, peptides, featureRows, nFeatures,
                out _);
        }

        /// <summary>
        /// <see cref="TrainAndScore(string,IReadOnlyList{uint},IReadOnlyList{byte},IReadOnlyList{bool},IReadOnlyList{double},IReadOnlyList{string},IReadOnlyList{double[]},int)"/>,
        /// also returning what the run's second pass scores its moved rows with: the final model,
        /// the calibration map and the protein-best column with its floor. Null for a run with no rows.
        /// </summary>
        public double[] TrainAndScore(string fileName, IReadOnlyList<uint> entryIds, IReadOnlyList<byte> charges,
            IReadOnlyList<bool> isDecoys, IReadOnlyList<double> coelutionSums, IReadOnlyList<string> peptides,
            IReadOnlyList<double[]> featureRows, int nFeatures, out RunFirstPassModel trained)
        {
            trained = null;
            int count = entryIds.Count;
            if (count == 0)
                return new double[0];
            // The training reports its percent from inside, so a run that trains for minutes shows
            // motion; under --parallel-files the percent feeds the run's "[i] p%" slot.
            RunFirstPassModel model = null;
            var scores = ProgressReporter.Run(
                string.Format(OspreyFDRResources.PerFileFirstPassModel_TrainAndScore_Training_the_run_s_first_pass_model_on__0__precursor_candidate_peaks, count),
                TrainingProgress.Total,
                progress => TrainAndScore(new TrainingProgress(progress), fileName, entryIds, charges, isDecoys,
                    coelutionSums, peptides, featureRows, nFeatures, out model));
            trained = model;
            return scores;
        }

        /// <summary>The training and scoring of one run, reporting each step to <paramref name="progress"/>.</summary>
        private double[] TrainAndScore(TrainingProgress progress, string fileName, IReadOnlyList<uint> entryIds,
            IReadOnlyList<byte> charges, IReadOnlyList<bool> isDecoys, IReadOnlyList<double> coelutionSums,
            IReadOnlyList<string> peptides, IReadOnlyList<double[]> featureRows, int nFeatures,
            out RunFirstPassModel trained)
        {
            int count = entryIds.Count;
            var scores = new double[count];
            progress.BeginStep(TrainingStep.round_1);
            long t = Stopwatch.GetTimestamp();
            long fileStart = t;
            // This file's own step times for its [TIMING] line: the column inside round 2's setup.
            long columnTicks = 0;
            var labels = new bool[count];
            var ids = new uint[count];
            var seqs = new string[count];
            var bestScores = new double[count];
            for (int r = 0; r < count; r++)
            {
                labels[r] = isDecoys[r];
                ids[r] = entryIds[r];
                seqs[r] = peptides[r];
                bestScores[r] = coelutionSums[r];
            }

            // The file's best peak per precursor - one run, so the reservoir over runs reduces to
            // the best-scoring row - then the peptide-grouped subsample.
            int[] subset = PercolatorSampling.BuildTrainingSubset(labels, ids, seqs, new List<PercolatorEntry>(),
                _trainConfig.MaxTrainSize, _trainConfig.Seed, out int[] bestPerPrecursor, bestScores, new[] { 0 });
            PercolatorResults model;
            List<PercolatorEntry> entries;
            // The file's own protein-best column, a value per row, once its round 1 has trained,
            // and the floor it gives an entry with no row.
            double[] column = null;
            double columnFloor = 0;
            if (_round2Size > 0)
            {
                // Two rounds, as the global training: every best-per-precursor row is a candidate,
                // round 1 trains on the subsample, and round 2 on the groups round 1 scores best.
                var candidates = new List<PercolatorEntry>(bestPerPrecursor.Length);
                var candidateByRow = new Dictionary<int, int>(bestPerPrecursor.Length);
                foreach (int r in bestPerPrecursor)
                {
                    candidateByRow[r] = candidates.Count;
                    candidates.Add(NewEntry(fileName, r, labels, ids, seqs, charges, bestScores, featureRows, nFeatures));
                }
                var round1Subset = new int[subset.Length];
                for (int k = 0; k < subset.Length; k++)
                    round1Subset[k] = candidateByRow[subset[k]];
                // The trainer expects round 1's rows in ascending candidate order.
                Array.Sort(round1Subset); // Array.Sort OK: distinct candidate indices, so no ties
                var config = _trainConfig.CloneForTrainOnly();
                config.PrefilterTrainSize = _round2Size;
                config.TrainingProgress = progress;
                Action<PercolatorResults, IReadOnlyDictionary<uint, int>, List<PercolatorEntry>> appendColumn = null;
                if (_ownProteinBest != null)
                {
                    appendColumn = (round1, heldOutFoldByBaseId, round2Rows) =>
                    {
                        long columnStart = Stopwatch.GetTimestamp();
                        column = OwnProteinBestColumn(round1, heldOutFoldByBaseId, ids, labels, bestScores,
                            featureRows, nFeatures, out columnFloor);
                        columnTicks = Stopwatch.GetTimestamp() - columnStart;
                        foreach (var entry in round2Rows)
                        {
                            var extended = new double[entry.Features.Length + 1];
                            Array.Copy(entry.Features, extended, entry.Features.Length);
                            extended[entry.Features.Length] = column[entry.ParquetIndex];
                            entry.Features = extended;
                        }
                    };
                }
                model = PercolatorTrainer.TrainWithScorePrefilter(candidates, round1Subset, config, _log, _passLabel,
                    out entries, appendColumn);
            }
            else
            {
                entries = new List<PercolatorEntry>(subset.Length);
                foreach (int r in subset)
                    entries.Add(NewEntry(fileName, r, labels, ids, seqs, charges, bestScores, featureRows, nFeatures));
                // A copy for this run, as the two-round branch has: one instance serves every run.
                var config = _trainConfig.CloneForTrainOnly();
                config.TrainingProgress = progress;
                model = PercolatorTrainer.RunPercolator(entries, config);
            }
            long trainEnd = Stopwatch.GetTimestamp();
            double trainSeconds = Seconds(trainEnd - t);
            t = Accumulate(ref _trainTicks, t);
            Interlocked.Add(ref _trainRows, entries.Count);

            var heldOutFoldByRow = HeldOutFoldByRow(entries, model.TrainEntryFolds, labels, ids, seqs);
            Func<int, double[]> rowAt = r => PercolatorScorer.ResolveFeatureRow(featureRows, (uint)r, bestScores[r], nFeatures);
            int scoreFeatures = nFeatures;
            if (column != null)
            {
                var rawRowAt = rowAt;
                rowAt = r =>
                {
                    var extended = new double[nFeatures + 1];
                    Array.Copy(rawRowAt(r), extended, nFeatures);
                    extended[nFeatures] = column[r];
                    return extended;
                };
                scoreFeatures = nFeatures + 1;
            }
            progress.BeginStep(TrainingStep.scoring);
            PercolatorScorer.ScoreRowsWithModel(count, rowAt, model, scoreFeatures, scores, 0, _threads, heldOutFoldByRow);
            progress.BeginStep(TrainingStep.safeguard);
            int fallbackFeature = ChooseFallbackFeature(fileName, scores, labels, ids, featureRows, bestScores, nFeatures);
            if (fallbackFeature != RunFirstPassModel.NO_FALLBACK)
            {
                for (int r = 0; r < count; r++)
                    scores[r] = PercolatorScorer.ResolveFeatureRow(featureRows, (uint)r, bestScores[r], nFeatures)[fallbackFeature];
            }
            t = Accumulate(ref _scoreTicks, t);

            double scoreSeconds = Seconds(t - trainEnd);
            progress.BeginStep(TrainingStep.calibration);
            var anchor = Calibrate(scores, labels, ids, seqs, _trainConfig.TestFdr, out double threshold, out double denominator);
            if (anchor == CalibrationAnchor.BEST_DECOY)
            {
                Interlocked.Increment(ref _bestDecoyAnchored);
            }
            else if (anchor == CalibrationAnchor.EXCLUDED)
            {
                Interlocked.Increment(ref _excluded);
                // Log text for developers, not displayed in the default output
                _log.LogInfo(string.Format(
                    @"Warning: {0}: the run's first-pass model passed no target at q {1} and has no decoy above its median best decoy, so its scores could not be calibrated against the other runs and its precursors are left out of the experiment competition.",
                    fileName, _trainConfig.TestFdr));
            }
            _log.LogInfo(LogTag.COUNT, @"{0} per-file model [{1}]: calibration anchor {2}", _passLabel, fileName,
                anchor.Name);
            Accumulate(ref _calibrateTicks, t);
            _log.LogInfo(LogTag.TIMING,
                @"{0} per-file model [{1}]: {2:F1}s wall at {3} thread(s); {4} rows, {5} best-per-precursor, {6} final training rows; train {7:F1}s (protein-best column {8:F1}s), out-of-fold scoring {9:F1}s",
                _passLabel, fileName, Seconds(Stopwatch.GetTimestamp() - fileStart), _threads, count,
                bestPerPrecursor.Length, entries.Count, trainSeconds, Seconds(columnTicks), scoreSeconds);
            Interlocked.Increment(ref _files);
            trained = new RunFirstPassModel(model, threshold, denominator, column, columnFloor, fallbackFeature);
            return scores;
        }

        /// <summary>The [TIMING] line of the per-file models: their summed lane time by step.</summary>
        public void LogTiming(IOspreyLog log, string passLabel, string where)
        {
            log.LogInfo(LogTag.TIMING,
                @"{0} per-file models ({1}): {2} file(s), {3} training rows; lanes (summed) train {4:F1}s, score {5:F1}s, calibrate {6:F1}s; {7} thread(s) per file; {8} file(s) anchored on the best decoy, {9} file(s) excluded from the experiment competition, {10} file(s) scored by their best single feature",
                passLabel, where, Interlocked.Read(ref _files), Interlocked.Read(ref _trainRows),
                Seconds(Interlocked.Read(ref _trainTicks)), Seconds(Interlocked.Read(ref _scoreTicks)),
                Seconds(Interlocked.Read(ref _calibrateTicks)), _threads, Interlocked.Read(ref _bestDecoyAnchored),
                Interlocked.Read(ref _excluded), Interlocked.Read(ref _fallbacks));
        }

        /// <summary>
        /// The safeguard on a run's model, as Percolator's on its learned direction: the targets
        /// passing the run's precursor q at <see cref="PercolatorConfig.TestFdr"/> under the model's
        /// out-of-fold <paramref name="modelScores"/>, against the most any single feature of the
        /// run passes by the same competition and q
        /// (<see cref="PercolatorTrainer.BestSingleFeature"/>, the rule that picks the folds'
        /// initial direction). Returns that feature's index when it passes strictly more, so the
        /// run is scored with it, otherwise <see cref="RunFirstPassModel.NO_FALLBACK"/>. A model
        /// trained on a run of a few hundred precursors can lose to its own starting feature - one
        /// decoy it ranks among the top hundred targets passes nothing at q 0.01 - and the run's
        /// precursors would then be lost to the experiment for a model a single feature beats.
        /// </summary>
        private int ChooseFallbackFeature(string fileName, double[] modelScores, bool[] labels, uint[] ids,
            IReadOnlyList<double[]> featureRows, double[] bestScores, int nFeatures)
        {
            double testFdr = _trainConfig.TestFdr;
            int count = modelScores.Length;
            int modelPassing = PercolatorQValues.CountPassingConservative(modelScores, labels, ids, testFdr);
            var passing = new int[nFeatures];
            OspreyParallel.For(0, nFeatures, _threads, feat =>
            {
                var featureScores = new double[count];
                for (int r = 0; r < count; r++)
                    featureScores[r] = PercolatorScorer.ResolveFeatureRow(featureRows, (uint)r, bestScores[r], nFeatures)[feat];
                passing[feat] = PercolatorQValues.CountPassingConservative(featureScores, labels, ids, testFdr);
            });
            int bestFeature = PercolatorTrainer.BestSingleFeature(passing, out int bestPassing);
            bool fallBack = bestPassing > modelPassing;
            if (fallBack)
                Interlocked.Increment(ref _fallbacks);
            string featureName = FeatureName(bestFeature);
            // Log text for developers, not displayed in the default output
            _log.LogInfo(LogTag.COUNT,
                @"{0} per-file model [{1}]: safeguard: model passes {2} target(s) at run q {3}, best single feature {4} passes {5}; {6}",
                _passLabel, fileName, modelPassing, testFdr, featureName, bestPassing,
                fallBack ? @"scored with " + featureName + @" in place of the model" : @"scored with the model");
            return fallBack ? bestFeature : RunFirstPassModel.NO_FALLBACK;
        }

        private string FeatureName(int feature)
        {
            var infos = _trainConfig.FeatureInfos;
            return infos != null && feature < infos.Length ? infos[feature].Name : string.Format(@"feature_{0}", feature);
        }

        /// <summary>
        /// The file's protein-best column from its own round-1 model: every row scored by round 1,
        /// out of fold where its base_id trained (as the global column is), then each row's best
        /// sibling-peptide score on its proteins within this run (<see cref="ProteinBestFeature.ComputeColumn(ProteinBestMembership,int,IReadOnlyList{uint},IReadOnlyList{bool},IReadOnlyList{double})"/>).
        /// Nothing outside the file is read.
        /// </summary>
        private double[] OwnProteinBestColumn(PercolatorResults round1, IReadOnlyDictionary<uint, int> heldOutFoldByBaseId,
            uint[] ids, bool[] labels, double[] bestScores, IReadOnlyList<double[]> featureRows, int nFeatures,
            out double floor)
        {
            int count = ids.Length;
            var baseIds = new uint[count];
            var heldOut = new Dictionary<int, int>();
            for (int r = 0; r < count; r++)
            {
                baseIds[r] = ids[r] & PercolatorEntry.BASE_ID_MASK;
                if (heldOutFoldByBaseId.TryGetValue(baseIds[r], out int fold))
                    heldOut[r] = fold;
            }
            var priorScores = new double[count];
            PercolatorScorer.ScoreRowsWithModel(count,
                r => PercolatorScorer.ResolveFeatureRow(featureRows, (uint)r, bestScores[r], nFeatures),
                round1, nFeatures, priorScores, 0, _threads, heldOut);
            return ProteinBestFeature.ComputeColumn(_ownProteinBest.Membership, _ownProteinBest.TopN,
                baseIds, labels, priorScores, out floor);
        }

        /// <summary>A training entry for row <paramref name="r"/> of the file, owning a copy of its features.</summary>
        private static PercolatorEntry NewEntry(string fileName, int r, bool[] labels, uint[] ids, string[] seqs,
            IReadOnlyList<byte> charges, double[] bestScores, IReadOnlyList<double[]> featureRows, int nFeatures)
        {
            return new PercolatorEntry
            {
                FileName = fileName,
                Peptide = seqs[r],
                Charge = charges[r],
                IsDecoy = labels[r],
                EntryId = ids[r],
                ParquetIndex = (uint)r,
                CoelutionSum = bestScores[r],
                Features = (double[])PercolatorScorer.ResolveFeatureRow(featureRows, (uint)r, bestScores[r], nFeatures).Clone()
            };
        }

        /// <summary>
        /// Each row's held-out fold, by row index: the fold of its base_id when a training entry
        /// carried it, otherwise the fold of its peptide group (the target peptide of its base_id),
        /// which the folds keep together. Rows of a group no fold trained on are left out and
        /// scored by the fold mean.
        /// </summary>
        private static Dictionary<int, int> HeldOutFoldByRow(List<PercolatorEntry> entries, int[] entryFolds,
            bool[] labels, uint[] ids, string[] seqs)
        {
            var foldByBaseId = new Dictionary<uint, int>();
            var foldByGroup = new Dictionary<string, int>(StringComparer.Ordinal);
            if (entryFolds != null)
            {
                for (int k = 0; k < entries.Count; k++)
                {
                    int fold = entryFolds[k];
                    if (fold < 0)
                        continue;
                    uint baseId = entries[k].EntryId & PercolatorEntry.BASE_ID_MASK;
                    foldByBaseId[baseId] = fold;
                    if (!entries[k].IsDecoy)
                        foldByGroup[entries[k].Peptide] = fold;
                }
            }
            // The target peptide of each base_id in the file, which names a decoy's group.
            var targetPeptideByBaseId = new Dictionary<uint, string>();
            for (int r = 0; r < labels.Length; r++)
            {
                if (!labels[r])
                    targetPeptideByBaseId.TryAdd(ids[r] & PercolatorEntry.BASE_ID_MASK, seqs[r]);
            }
            var heldOut = new Dictionary<int, int>(labels.Length);
            for (int r = 0; r < labels.Length; r++)
            {
                uint baseId = ids[r] & PercolatorEntry.BASE_ID_MASK;
                if (foldByBaseId.TryGetValue(baseId, out int fold))
                {
                    heldOut[r] = fold;
                    continue;
                }
                string group = labels[r]
                    ? targetPeptideByBaseId.TryGetValue(baseId, out string target) ? target : null
                    : seqs[r];
                if (group != null && foldByGroup.TryGetValue(group, out fold))
                    heldOut[r] = fold;
            }
            return heldOut;
        }

        /// <summary>
        /// Maps the file's scores in place, <c>(score - threshold) / denominator</c>, with the
        /// median best decoy per entry at -1 and 0 at the first anchor that is above it:
        /// <list type="number">
        /// <item>the lowest target passing the run's precursor q at <paramref name="testFdr"/>;</item>
        /// <item>when no target passes, the run's highest-scoring decoy, the boundary between
        /// q = 0 and q &gt; 0. A small run's q has a floor of 1 / (targets above its first decoy),
        /// so with fewer than 1 / <paramref name="testFdr"/> targets there no target can pass, and
        /// its scores left on the model's own scale would meet the other runs' calibrated scores
        /// in the experiment competition unadjusted.</item>
        /// </list>
        /// With neither (no decoy, or no decoy above the median), the run is excluded: its scores
        /// keep their order, so its run q-values are unchanged, but move far below any calibrated
        /// score, so its rows win no entry another run has.
        /// </summary>
        private static CalibrationAnchor Calibrate(double[] scores, bool[] labels, uint[] ids, string[] seqs,
            double testFdr, out double threshold, out double denom)
        {
            int count = scores.Length;
            PercolatorQValues.ComputePerFileRunQvalues(scores, labels, ids, seqs, 0, count,
                out double[] runPrecursorQ, out _);
            double passingTarget = double.MaxValue;
            double topDecoy = double.MinValue;
            double minScore = double.MaxValue;
            double maxScore = double.MinValue;
            var bestDecoy = new Dictionary<uint, double>();
            for (int r = 0; r < count; r++)
            {
                minScore = Math.Min(minScore, scores[r]);
                maxScore = Math.Max(maxScore, scores[r]);
                if (labels[r])
                {
                    if (!bestDecoy.TryGetValue(ids[r], out double best) || scores[r] > best)
                        bestDecoy[ids[r]] = scores[r];
                    topDecoy = Math.Max(topDecoy, scores[r]);
                }
                else if (runPrecursorQ[r] <= testFdr && scores[r] < passingTarget)
                {
                    passingTarget = scores[r];
                }
            }
            var anchor = CalibrationAnchor.EXCLUDED;
            threshold = 0;
            denom = 0;
            if (bestDecoy.Count > 0)
            {
                var decoys = new List<double>(bestDecoy.Values);
                decoys.Sort(); // Array.Sort OK: median of single primitive list, no parallel data; tie order irrelevant
                double medianDecoy = decoys[decoys.Count / 2];
                if (passingTarget != double.MaxValue && passingTarget > medianDecoy)
                {
                    anchor = CalibrationAnchor.PASSING_TARGET;
                    threshold = passingTarget;
                }
                else if (topDecoy > medianDecoy)
                {
                    anchor = CalibrationAnchor.BEST_DECOY;
                    threshold = topDecoy;
                }
                denom = threshold - medianDecoy;
            }
            if (anchor == CalibrationAnchor.EXCLUDED)
            {
                // The run's own score range is the unit, so the map keeps its order.
                denom = maxScore > minScore ? maxScore - minScore : 1.0;
                threshold = maxScore + EXCLUDED_OFFSET * denom;
            }
            for (int r = 0; r < count; r++)
                scores[r] = (scores[r] - threshold) / denom;
            return anchor;
        }

        /// <summary>Which score a run's calibration maps to 0 (<see cref="Calibrate"/>).</summary>
        private sealed class CalibrationAnchor
        {
            public static readonly CalibrationAnchor PASSING_TARGET = new CalibrationAnchor(@"passing-target");
            public static readonly CalibrationAnchor BEST_DECOY = new CalibrationAnchor(@"best-decoy");
            public static readonly CalibrationAnchor EXCLUDED = new CalibrationAnchor(@"excluded");

            private CalibrationAnchor(string name)
            {
                Name = name;
            }

            /// <summary>The spelling of the [COUNT] line.</summary>
            public string Name { get; }
        }

        private static long Accumulate(ref long ticks, long start)
        {
            long now = Stopwatch.GetTimestamp();
            Interlocked.Add(ref ticks, now - start);
            return now;
        }

        private static double Seconds(long ticks)
        {
            return (double)ticks / Stopwatch.Frequency;
        }
    }
}
