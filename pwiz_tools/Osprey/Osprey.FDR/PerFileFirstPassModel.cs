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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// The per-file first-pass model of the <c>OSPREY_PER_FILE_FDR</c> study: one model trained on
    /// one file's own rows, the way DIA-NN trains one model per run, and that file's rows scored
    /// with it. Experiment code, reached only through the environment switch.
    ///
    /// <para>The training rows are the file's best candidate peak per precursor, subsampled by
    /// peptide group to <see cref="PercolatorConfig.MaxTrainSize"/> as the global training does.
    /// With <c>OSPREY_PER_FILE_FDR_ROUNDS=2</c> the file trains as the global model
    /// does, through <see cref="PercolatorTrainer.TrainWithScorePrefilter"/>: round 1 on that subsample,
    /// round 2 on the peptide groups round 1 scores best, started from round 1's scores.
    /// At SEA-AD scale that subset covers most of the file's precursors, so the score pass is OUT
    /// OF FOLD: every row is scored by the one fold model that held its peptide group out, and only
    /// a row whose peptide group no fold trained on gets the fold mean.</para>
    ///
    /// <para>The scores are then calibrated per file (Granholm et al. 2012, the map
    /// <c>CalibrateScoresBetweenFolds</c> applies between folds): the lowest target score passing
    /// the run's precursor q at <see cref="PercolatorConfig.TestFdr"/> maps to 0 and the median of the
    /// run's best decoy per entry to -1. Without it the experiment competition, which ranks each
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
        // Round 2's row count when each file trains in two rounds (OSPREY_PER_FILE_FDR_ROUNDS=2),
        // as the global model does; 0 for one round.
        private readonly int _round2Size;
        // OSPREY_PER_FILE_FDR=file: the protein-best column each file computes from its OWN round-1
        // model and appends for round 2 and the score pass; null otherwise.
        private readonly ProteinBestRequest _ownProteinBest;
        private long _files;
        private long _trainTicks;
        private long _scoreTicks;
        private long _calibrateTicks;
        private long _trainRows;
        private long _uncalibrated;

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
            // The file's own column reaches its model only through round 2, so that arm always
            // trains two rounds.
            _ownProteinBest = string.Equals(OspreyEnvironment.PerFileFdr, OspreyEnvironment.PER_FILE_FDR_FILE, StringComparison.Ordinal)
                ? percConfig.ProteinBest
                : null;
            _threads = Math.Max(1, percConfig.NThreads / Math.Max(1, lanes));
            _trainConfig = percConfig.CloneForTrainOnly();
            // OSPREY_PER_FILE_FDR_TRAIN_SIZE: round 1's rows, and two thirds of them for round 2
            // unless OSPREY_PER_FILE_FDR_ROUND2_SIZE names round 2's own size.
            int? trainSize = OspreyEnvironment.PerFileFdrTrainSize;
            if (trainSize.HasValue)
                _trainConfig.MaxTrainSize = trainSize.Value;
            int round2Size = OspreyEnvironment.PrefilterSizeFor(true);
            if (trainSize.HasValue)
                round2Size = Math.Min(round2Size, trainSize.Value * 2 / 3);
            // The override never turns on a round 2 that OSPREY_GBT_PREFILTER_SIZE=0 turned off.
            if (OspreyEnvironment.PerFileFdrRound2Size.HasValue && round2Size > 0)
                round2Size = OspreyEnvironment.PerFileFdrRound2Size.Value;
            _round2Size = OspreyEnvironment.PerFileFdrTwoRound || _ownProteinBest != null ? round2Size : 0;
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
            var scores = new double[count];
            if (count == 0)
                return scores;
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
                model = PercolatorTrainer.RunPercolator(entries, _trainConfig);
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
            PercolatorScorer.ScoreRowsWithModel(count, rowAt, model, scoreFeatures, scores, 0, _threads, heldOutFoldByRow);
            t = Accumulate(ref _scoreTicks, t);

            double scoreSeconds = Seconds(t - trainEnd);
            if (!Calibrate(scores, labels, ids, seqs, _trainConfig.TestFdr, out double threshold, out double denominator))
            {
                threshold = 0.0;
                denominator = 1.0;
                // Its scores stay on the model's own scale, so its precursors meet the other runs'
                // calibrated scores in the experiment competition unadjusted.
                Interlocked.Increment(ref _uncalibrated);
                _log.LogInfo(LogTag.COUNT, string.Format(
                    @"{0}: the run's first-pass model passed no target at q {1}, so its scores could not be calibrated against the other runs.",
                    fileName, _trainConfig.TestFdr));
            }
            Accumulate(ref _calibrateTicks, t);
            _log.LogInfo(LogTag.TIMING,
                @"{0} per-file model [{1}]: {2:F1}s wall at {3} thread(s); {4} rows, {5} best-per-precursor, {6} final training rows; train {7:F1}s (protein-best column {8:F1}s), out-of-fold scoring {9:F1}s",
                _passLabel, fileName, Seconds(Stopwatch.GetTimestamp() - fileStart), _threads, count,
                bestPerPrecursor.Length, entries.Count, trainSeconds, Seconds(columnTicks), scoreSeconds);
            Interlocked.Increment(ref _files);
            trained = new RunFirstPassModel(model, threshold, denominator, column, columnFloor);
            return scores;
        }

        /// <summary>The [TIMING] line of the per-file models: their summed lane time by step.</summary>
        public void LogTiming(IOspreyLog log, string passLabel, string where)
        {
            log.LogInfo(LogTag.TIMING,
                @"{0} per-file models ({1}): {2} file(s), {3} training rows; lanes (summed) train {4:F1}s, score {5:F1}s, calibrate {6:F1}s; {7} thread(s) per file; {8} file(s) left uncalibrated",
                passLabel, where, Interlocked.Read(ref _files), Interlocked.Read(ref _trainRows),
                Seconds(Interlocked.Read(ref _trainTicks)), Seconds(Interlocked.Read(ref _scoreTicks)),
                Seconds(Interlocked.Read(ref _calibrateTicks)), _threads, Interlocked.Read(ref _uncalibrated));
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
        /// Maps the file's scores in place so the lowest target passing the run's precursor q at
        /// <paramref name="testFdr"/> sits at 0 and the median best decoy per entry at -1, the map
        /// <c>(score - threshold) / denominator</c>. Returns false, leaving the scores as they are,
        /// when no target passes or the two coincide.
        /// </summary>
        private static bool Calibrate(double[] scores, bool[] labels, uint[] ids, string[] seqs, double testFdr,
            out double threshold, out double denom)
        {
            int count = scores.Length;
            PercolatorQValues.ComputePerFileRunQvalues(scores, labels, ids, seqs, 0, count,
                out double[] runPrecursorQ, out _);
            threshold = double.MaxValue;
            denom = 0;
            var bestDecoy = new Dictionary<uint, double>();
            for (int r = 0; r < count; r++)
            {
                if (labels[r])
                {
                    if (!bestDecoy.TryGetValue(ids[r], out double best) || scores[r] > best)
                        bestDecoy[ids[r]] = scores[r];
                }
                else if (runPrecursorQ[r] <= testFdr && scores[r] < threshold)
                {
                    threshold = scores[r];
                }
            }
            if (threshold == double.MaxValue || bestDecoy.Count == 0)
                return false;
            var decoys = new List<double>(bestDecoy.Values);
            decoys.Sort(); // Array.Sort OK: median of single primitive list, no parallel data; tie order irrelevant
            denom = threshold - decoys[decoys.Count / 2];
            if (denom <= 0.0)
                return false;
            for (int r = 0; r < count; r++)
                scores[r] = (scores[r] - threshold) / denom;
            return true;
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
