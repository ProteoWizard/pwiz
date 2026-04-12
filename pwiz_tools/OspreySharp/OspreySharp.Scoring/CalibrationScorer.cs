using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.OspreySharp.ML;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Result of calibration scoring for a single library-spectrum match.
    /// Port of CalibrationMatch from osprey-scoring/src/batch.rs.
    /// </summary>
    public class CalibrationMatch
    {
        public uint EntryId { get; set; }
        public bool IsDecoy { get; set; }
        public string Sequence { get; set; }
        public uint ScanNumber { get; set; }
        public double CorrelationScore { get; set; }
        public double LibcosineApex { get; set; }
        public byte Top6MatchedApex { get; set; }
        public double XcorrScore { get; set; }
        public double IsotopeCosine { get; set; }
        public double DiscriminantScore { get; set; }
        public double QValue { get; set; }
        /// <summary>MS2 fragment mass errors at apex spectrum (in config units).</summary>
        public double[] Ms2MassErrors { get; set; }
    }

    /// <summary>
    /// LDA-based calibration scoring with target-decoy competition and iterative
    /// non-negative CV refinement. Direct port of
    /// osprey-scoring/src/calibration_ml.rs `train_and_score_calibration`.
    ///
    /// The refinement loop implements the Percolator-style approach:
    ///   1. Baseline: best single feature at 1% FDR
    ///   2. For up to MAX_ITERATIONS:
    ///      - 3-fold stratified CV grouped by peptide sequence
    ///      - Per fold: select high-confidence targets from train set using
    ///        current scores, combine with all train decoys, fit LDA
    ///      - Average fold weights, clip negatives, renormalize
    ///      - Score all data with consensus weights
    ///      - Track best iteration; early-stop after 2 non-improvements
    ///   3. Return scores from the best iteration (may be the baseline)
    /// </summary>
    public static class CalibrationScorer
    {
        private const int N_FOLDS = 3;
        private const int MAX_ITERATIONS = 3;
        private const int MIN_POSITIVE_EXAMPLES = 50;

        /// <summary>
        /// Train LDA on calibration matches and score them using 3-fold cross-validation.
        /// Returns the number of matches passing 1% FDR threshold.
        /// </summary>
        public static int TrainAndScoreCalibration(CalibrationMatch[] matches, bool useIsotopeFeature)
        {
            if (matches == null || matches.Length == 0)
                return 0;

            // 1. Extract feature matrix
            Matrix features = ExtractFeatureMatrix(matches, useIsotopeFeature);

            // 2. Build target/decoy labels (true = decoy)
            bool[] decoyLabels = new bool[matches.Length];
            for (int i = 0; i < matches.Length; i++)
                decoyLabels[i] = matches[i].IsDecoy;

            // 3. Entry IDs for paired competition
            uint[] entryIds = new uint[matches.Length];
            for (int i = 0; i < matches.Length; i++)
                entryIds[i] = matches[i].EntryId;

            // 4. Sequences for fold grouping (keeps charge states together)
            string[] sequences = new string[matches.Length];
            for (int i = 0; i < matches.Length; i++)
                sequences[i] = matches[i].Sequence;

            // 5. Train iterative non-negative CV LDA
            double[] discriminants = TrainLdaWithNonNegativeCv(
                features, decoyLabels, entryIds, sequences);

            // 6. Assign discriminant scores
            for (int i = 0; i < matches.Length; i++)
                matches[i].DiscriminantScore = discriminants[i];

            // 7. Paired target-decoy competition on final scores
            int[] allIndices = new int[matches.Length];
            for (int i = 0; i < allIndices.Length; i++)
                allIndices[i] = i;
            int[] winnerIndices = CompeteCalibrationPairs(
                discriminants, entryIds, decoyLabels, allIndices);

            // 8. Compute q-values on winners (already sorted by score descending)
            bool[] winnerIsDecoy = new bool[winnerIndices.Length];
            for (int i = 0; i < winnerIndices.Length; i++)
                winnerIsDecoy[i] = decoyLabels[winnerIndices[i]];
            double[] winnerQ = new double[winnerIndices.Length];
            int nPassing = QValueCalculator.ComputeQValues(winnerIsDecoy, winnerQ);

            // 9. Map q-values back: winners get their q-value, losers get 1.0
            double[] qValues = new double[matches.Length];
            for (int i = 0; i < qValues.Length; i++)
                qValues[i] = 1.0;
            for (int rank = 0; rank < winnerIndices.Length; rank++)
                qValues[winnerIndices[rank]] = winnerQ[rank];

            for (int i = 0; i < matches.Length; i++)
                matches[i].QValue = qValues[i];

            return nPassing;
        }

        /// <summary>
        /// Paired target-decoy competition for calibration matches.
        /// Groups matches by (entry_id &amp; 0x7FFFFFFF), competes each pair on the
        /// given score, and returns only winner indices (sorted by score
        /// descending, then base_id ascending for deterministic tie-breaking).
        /// Singletons auto-win. Ties between paired target+decoy go to decoy
        /// (conservative for FDR estimation, matches Rust strict `&gt;` semantics).
        /// </summary>
        public static int[] CompeteCalibrationPairs(
            double[] scores, uint[] entryIds, bool[] isDecoy, int[] subsetIndices)
        {
            // Group indices by base_id = entry_id & 0x7FFFFFFF
            // Value: (bestTarget, bestDecoy) as indices, -1 if none.
            var groups = new Dictionary<uint, KeyValuePair<int, int>>();

            foreach (int idx in subsetIndices)
            {
                uint baseId = entryIds[idx] & 0x7FFFFFFF;
                KeyValuePair<int, int> existing;
                int bestTarget = -1;
                int bestDecoy = -1;
                if (groups.TryGetValue(baseId, out existing))
                {
                    bestTarget = existing.Key;
                    bestDecoy = existing.Value;
                }

                if (isDecoy[idx])
                {
                    if (bestDecoy < 0 || scores[idx] > scores[bestDecoy])
                        bestDecoy = idx;
                }
                else
                {
                    if (bestTarget < 0 || scores[idx] > scores[bestTarget])
                        bestTarget = idx;
                }

                groups[baseId] = new KeyValuePair<int, int>(bestTarget, bestDecoy);
            }

            // Compete each pair
            var winners = new List<int>(groups.Count);
            foreach (var pair in groups.Values)
            {
                int t = pair.Key;
                int d = pair.Value;

                if (t >= 0 && d < 0)
                    winners.Add(t);
                else if (t < 0 && d >= 0)
                    winners.Add(d);
                else if (t >= 0 && d >= 0)
                {
                    // Strict >: ties go to decoy
                    if (scores[t] > scores[d])
                        winners.Add(t);
                    else
                        winners.Add(d);
                }
            }

            // Sort by score descending, then base_id ascending for deterministic tie-breaking.
            // Using base_id (not array index) as secondary key matches Rust's
            // compete_calibration_pairs. Array-index tiebreaks correlate with
            // target/decoy bias when input is sorted by entry_id.
            winners.Sort((a, b) =>
            {
                int cmp = scores[b].CompareTo(scores[a]);
                if (cmp != 0) return cmp;
                uint baseA = entryIds[a] & 0x7FFFFFFF;
                uint baseB = entryIds[b] & 0x7FFFFFFF;
                return baseA.CompareTo(baseB);
            });
            return winners.ToArray();
        }

        /// <summary>
        /// Iterative non-negative LDA with cross-validation. Direct port of
        /// `train_lda_with_nonnegative_cv` in osprey-scoring/src/calibration_ml.rs.
        /// Returns the discriminant scores from the best iteration.
        /// </summary>
        private static double[] TrainLdaWithNonNegativeCv(
            Matrix features, bool[] decoyLabels, uint[] entryIds, string[] sequences)
        {
            int nSamples = features.Rows;
            int nFeatures = features.Cols;
            double trainFdr = 0.01;

            // Create fold assignments (stable across iterations)
            int[] foldAssignments = CreateStratifiedFoldsByPeptide(decoyLabels, sequences, N_FOLDS);

            // Find best single feature as baseline
            int bestFeatIdx = 0;
            int bestFeatPassing = 0;
            for (int featIdx = 0; featIdx < nFeatures; featIdx++)
            {
                double[] featScores = new double[nSamples];
                for (int i = 0; i < nSamples; i++)
                    featScores[i] = features[i, featIdx];
                int nPass = CountPassingTargets(featScores, decoyLabels, entryIds, trainFdr);
                if (nPass > bestFeatPassing)
                {
                    bestFeatPassing = nPass;
                    bestFeatIdx = featIdx;
                }
            }

            // If no targets pass at configured FDR, loosen to 5% so training can proceed
            if (bestFeatPassing == 0)
            {
                const double relaxedFdr = 0.05;
                int relaxedBestIdx = 0;
                int relaxedBestPassing = 0;
                for (int featIdx = 0; featIdx < nFeatures; featIdx++)
                {
                    double[] featScores = new double[nSamples];
                    for (int i = 0; i < nSamples; i++)
                        featScores[i] = features[i, featIdx];
                    int nPass = CountPassingTargets(featScores, decoyLabels, entryIds, relaxedFdr);
                    if (nPass > relaxedBestPassing)
                    {
                        relaxedBestPassing = nPass;
                        relaxedBestIdx = featIdx;
                    }
                }
                if (relaxedBestPassing > 0)
                {
                    trainFdr = relaxedFdr;
                    bestFeatIdx = relaxedBestIdx;
                    bestFeatPassing = relaxedBestPassing;
                }
            }

            // Initialize with best single feature as both current and best-so-far
            double[] baselineScores = new double[nSamples];
            for (int i = 0; i < nSamples; i++)
                baselineScores[i] = features[i, bestFeatIdx];

            double[] bestScores = (double[])baselineScores.Clone();
            int bestPassing = bestFeatPassing;
            // best_iteration: 0 = baseline, 1..MAX_ITERATIONS = refinement iterations
            // (unused beyond the iteration tracking  -  kept for parity with Rust)
            double[] currentScores = baselineScores;
            int consecutiveNoImprove = 0;

            for (int iteration = 0; iteration < MAX_ITERATIONS; iteration++)
            {
                var foldWeights = new List<double[]>();

                // Phase 1: Use CV to estimate stable weights
                for (int foldIdx = 0; foldIdx < N_FOLDS; foldIdx++)
                {
                    var trainIndicesList = new List<int>();
                    for (int i = 0; i < nSamples; i++)
                    {
                        if (foldAssignments[i] != foldIdx)
                            trainIndicesList.Add(i);
                    }
                    int[] trainIndices = trainIndicesList.ToArray();

                    // Select high-confidence targets from TRAIN set using current scores
                    int[] selectedTargetIndices = SelectPositiveTrainingSet(
                        currentScores, decoyLabels, entryIds,
                        trainIndices, trainFdr, MIN_POSITIVE_EXAMPLES);

                    // Collect ALL decoy indices from train set
                    var decoyIndicesList = new List<int>();
                    foreach (int i in trainIndices)
                    {
                        if (decoyLabels[i])
                            decoyIndicesList.Add(i);
                    }

                    // Build training set: selected targets + all decoys
                    int[] trainingIndices = new int[selectedTargetIndices.Length + decoyIndicesList.Count];
                    Array.Copy(selectedTargetIndices, 0, trainingIndices, 0, selectedTargetIndices.Length);
                    for (int k = 0; k < decoyIndicesList.Count; k++)
                        trainingIndices[selectedTargetIndices.Length + k] = decoyIndicesList[k];

                    Matrix trainFeatures = features.ExtractRows(trainingIndices);
                    bool[] trainLabels = new bool[trainingIndices.Length];
                    for (int k = 0; k < trainingIndices.Length; k++)
                        trainLabels[k] = decoyLabels[trainingIndices[k]];

                    // Train LDA on clean training set. If LDA fails (singular
                    // scatter matrix), skip this fold.
                    LinearDiscriminant lda = LinearDiscriminant.Fit(trainFeatures, trainLabels);
                    if (lda != null)
                    {
                        foldWeights.Add((double[])lda.Eigenvector.Clone());
                    }
                }

                // Phase 2: Average weights across folds -> consensus weights
                if (foldWeights.Count == 0)
                {
                    // All folds failed; keep baseline and stop iterating
                    break;
                }

                double[] consensusWeights = AverageWeights(foldWeights);

                // Clip negative weights to zero (non-negative constraint)
                bool anyNegative = false;
                for (int i = 0; i < consensusWeights.Length; i++)
                {
                    if (consensusWeights[i] < 0.0)
                    {
                        consensusWeights[i] = 0.0;
                        anyNegative = true;
                    }
                }
                if (anyNegative)
                {
                    // Renormalize
                    double normSq = 0.0;
                    for (int i = 0; i < consensusWeights.Length; i++)
                        normSq += consensusWeights[i] * consensusWeights[i];
                    double norm = Math.Sqrt(normSq);
                    if (norm > 1e-10)
                    {
                        for (int i = 0; i < consensusWeights.Length; i++)
                            consensusWeights[i] /= norm;
                    }
                }

                // Phase 3: Score ALL data with consensus weights
                LinearDiscriminant ldaConsensus = LinearDiscriminant.FromWeights(consensusWeights);
                double[] newScores = ldaConsensus.Predict(features);

                int nPassingIter = CountPassingTargets(newScores, decoyLabels, entryIds, trainFdr);

                // Track best iteration: keep scores from whichever iteration gave the most passing
                if (nPassingIter > bestPassing)
                {
                    bestScores = (double[])newScores.Clone();
                    bestPassing = nPassingIter;
                    consecutiveNoImprove = 0;
                }
                else
                {
                    consecutiveNoImprove++;
                }

                // Update current scores for next iteration's target selection
                currentScores = newScores;

                // Stop early if 2 consecutive iterations didn't improve
                if (consecutiveNoImprove >= 2)
                    break;
            }

            return bestScores;
        }

        /// <summary>
        /// Create stratified fold assignments for cross-validation by peptide sequence.
        /// Direct port of `create_stratified_folds_by_peptide` in calibration_ml.rs:
        /// groups target PSMs by target sequence, decoy PSMs by decoy sequence, sorts
        /// each group list by ordinal sequence, and assigns peptide groups to folds
        /// round-robin. Target and decoy sequences are handled separately (so a target
        /// and its paired decoy may land in different folds  -  this matches Rust).
        /// </summary>
        private static int[] CreateStratifiedFoldsByPeptide(
            bool[] labels, string[] sequences, int nFolds)
        {
            int[] foldAssignments = new int[labels.Length];

            var targetPeptides = new Dictionary<string, List<int>>();
            var decoyPeptides = new Dictionary<string, List<int>>();

            for (int i = 0; i < labels.Length; i++)
            {
                Dictionary<string, List<int>> map = labels[i] ? decoyPeptides : targetPeptides;
                List<int> list;
                if (!map.TryGetValue(sequences[i], out list))
                {
                    list = new List<int>();
                    map[sequences[i]] = list;
                }
                list.Add(i);
            }

            // Sort groups by sequence (ordinal) for deterministic fold assignment.
            // Rust uses default String Ord which is lexicographic byte comparison;
            // C# matches via StringComparer.Ordinal.
            var targetGroups = new List<KeyValuePair<string, List<int>>>(targetPeptides);
            targetGroups.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var decoyGroups = new List<KeyValuePair<string, List<int>>>(decoyPeptides);
            decoyGroups.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

            // Round-robin assign target peptide groups to folds
            for (int i = 0; i < targetGroups.Count; i++)
            {
                int fold = i % nFolds;
                foreach (int idx in targetGroups[i].Value)
                    foldAssignments[idx] = fold;
            }
            // Round-robin assign decoy peptide groups to folds (independently)
            for (int i = 0; i < decoyGroups.Count; i++)
            {
                int fold = i % nFolds;
                foreach (int idx in decoyGroups[i].Value)
                    foldAssignments[idx] = fold;
            }

            return foldAssignments;
        }

        /// <summary>
        /// Count targets passing a given FDR threshold using paired competition.
        /// Direct port of `count_passing_targets` in calibration_ml.rs.
        /// </summary>
        private static int CountPassingTargets(
            double[] scores, bool[] decoyLabels, uint[] entryIds, double fdrThreshold)
        {
            int[] allIndices = new int[scores.Length];
            for (int i = 0; i < scores.Length; i++)
                allIndices[i] = i;

            int[] winnerIndices = CompeteCalibrationPairs(scores, entryIds, decoyLabels, allIndices);

            // Compute q-values on winners (already sorted by score descending)
            bool[] winnerIsDecoy = new bool[winnerIndices.Length];
            for (int i = 0; i < winnerIndices.Length; i++)
                winnerIsDecoy[i] = decoyLabels[winnerIndices[i]];
            double[] winnerQ = new double[winnerIndices.Length];
            if (winnerIndices.Length > 0)
                QValueCalculator.ComputeQValues(winnerIsDecoy, winnerQ);

            int nPassing = 0;
            for (int rank = 0; rank < winnerIndices.Length; rank++)
            {
                if (!decoyLabels[winnerIndices[rank]] && winnerQ[rank] <= fdrThreshold)
                    nPassing++;
            }
            return nPassing;
        }

        /// <summary>
        /// Select the positive training set (high-confidence targets) from a subset of
        /// indices using paired competition. Direct port of `select_positive_training_set`
        /// in calibration_ml.rs. If the initial threshold yields fewer than minTargets,
        /// progressively relaxes to {5%, 10%, 25%, 50%} FDR until at least minTargets
        /// are found.
        /// </summary>
        private static int[] SelectPositiveTrainingSet(
            double[] scores, bool[] decoyLabels, uint[] entryIds,
            int[] subsetIndices, double fdrThreshold, int minTargets)
        {
            int[] winnerIndices = CompeteCalibrationPairs(scores, entryIds, decoyLabels, subsetIndices);

            bool[] winnerIsDecoy = new bool[winnerIndices.Length];
            for (int i = 0; i < winnerIndices.Length; i++)
                winnerIsDecoy[i] = decoyLabels[winnerIndices[i]];
            double[] qValues = new double[winnerIndices.Length];
            for (int i = 0; i < qValues.Length; i++)
                qValues[i] = 1.0;
            if (winnerIndices.Length > 0)
                QValueCalculator.ComputeQValues(winnerIsDecoy, qValues);

            int[] selected = SelectTargetsAtThreshold(winnerIndices, decoyLabels, qValues, fdrThreshold);

            if (selected.Length < minTargets)
            {
                double[] relaxedThresholds = { 0.05, 0.10, 0.25, 0.50 };
                foreach (double threshold in relaxedThresholds)
                {
                    selected = SelectTargetsAtThreshold(winnerIndices, decoyLabels, qValues, threshold);
                    if (selected.Length >= minTargets)
                        break;
                }
            }

            return selected;
        }

        private static int[] SelectTargetsAtThreshold(
            int[] winnerIndices, bool[] decoyLabels, double[] qValues, double threshold)
        {
            var result = new List<int>();
            for (int rank = 0; rank < winnerIndices.Length; rank++)
            {
                int idx = winnerIndices[rank];
                if (!decoyLabels[idx] && qValues[rank] <= threshold)
                    result.Add(idx);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Average feature weights across multiple models, then renormalize to unit
        /// length. Direct port of `average_weights` in calibration_ml.rs.
        /// </summary>
        private static double[] AverageWeights(List<double[]> weightsList)
        {
            if (weightsList.Count == 0)
                return new double[0];

            int nFeatures = weightsList[0].Length;
            int nModels = weightsList.Count;

            double[] avg = new double[nFeatures];
            foreach (double[] weights in weightsList)
            {
                for (int i = 0; i < nFeatures; i++)
                    avg[i] += weights[i];
            }
            for (int i = 0; i < nFeatures; i++)
                avg[i] /= nModels;

            // Renormalize to unit length
            double normSq = 0.0;
            for (int i = 0; i < nFeatures; i++)
                normSq += avg[i] * avg[i];
            double norm = Math.Sqrt(normSq);
            if (norm > 1e-10)
            {
                for (int i = 0; i < nFeatures; i++)
                    avg[i] /= norm;
            }

            return avg;
        }

        /// <summary>
        /// Extract feature matrix from calibration matches. Features are normalized
        /// to similar ranges for fair weighting:
        ///   correlation: 0-1 (typical range 0-6, divided by 6)
        ///   libcosine:   0-1 (already normalized)
        ///   top6:        0-1 (count 0-6, divided by 6)
        ///   xcorr:       ~0-1 (typical range 0-3, divided by 3)
        /// </summary>
        private static Matrix ExtractFeatureMatrix(CalibrationMatch[] matches, bool useIsotopeFeature)
        {
            const int nFeatures = 4;
            double[] data = new double[matches.Length * nFeatures];

            for (int i = 0; i < matches.Length; i++)
            {
                var m = matches[i];
                int offset = i * nFeatures;
                data[offset] = Math.Max(0.0, Math.Min(1.0, m.CorrelationScore / 6.0));
                data[offset + 1] = Math.Max(0.0, Math.Min(1.0, m.LibcosineApex));
                data[offset + 2] = Math.Max(0.0, Math.Min(1.0, m.Top6MatchedApex / 6.0));
                data[offset + 3] = Math.Max(0.0, Math.Min(1.0, m.XcorrScore / 3.0));
            }

            return new Matrix(data, matches.Length, nFeatures);
        }
    }
}
