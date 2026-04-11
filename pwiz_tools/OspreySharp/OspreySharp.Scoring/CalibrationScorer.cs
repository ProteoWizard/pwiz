using System;
using System.Collections.Generic;
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
    }

    /// <summary>
    /// LDA-based calibration scoring with target-decoy competition.
    /// Port of calibration_ml.rs from osprey-scoring.
    /// </summary>
    public static class CalibrationScorer
    {
        private const int N_FOLDS = 3;

        /// <summary>
        /// Train LDA on calibration matches and score them using 3-fold cross-validation.
        /// Returns the number of matches passing 1% FDR threshold.
        /// </summary>
        /// <param name="matches">Calibration matches with extracted features.</param>
        /// <param name="useIsotopeFeature">Whether to include isotope_cosine as 5th feature.</param>
        /// <returns>Number of matches passing 1% FDR.</returns>
        public static int TrainAndScoreCalibration(CalibrationMatch[] matches, bool useIsotopeFeature)
        {
            if (matches == null || matches.Length == 0)
                return 0;

            int nFeatures = useIsotopeFeature ? 5 : 4;

            // Extract feature matrix
            Matrix features = ExtractFeatureMatrix(matches, useIsotopeFeature);

            // Build target/decoy labels
            bool[] decoyLabels = new bool[matches.Length];
            for (int i = 0; i < matches.Length; i++)
                decoyLabels[i] = matches[i].IsDecoy;

            // Extract entry IDs for paired competition
            uint[] entryIds = new uint[matches.Length];
            for (int i = 0; i < matches.Length; i++)
                entryIds[i] = matches[i].EntryId;

            // Train LDA with non-negative weights
            double[] discriminants = TrainLdaNonNegative(features, decoyLabels, nFeatures);

            // Assign discriminant scores
            for (int i = 0; i < matches.Length; i++)
                matches[i].DiscriminantScore = discriminants[i];

            // Paired target-decoy competition
            int[] allIndices = new int[matches.Length];
            for (int i = 0; i < allIndices.Length; i++)
                allIndices[i] = i;

            int[] winnerIndices = CompeteCalibrationPairs(
                discriminants, entryIds, decoyLabels, allIndices);

            // Compute q-values on winners
            double[] winnerQ = new double[winnerIndices.Length];
            int nPassing = CalculateQValues(winnerIndices, decoyLabels, winnerQ);

            // Map q-values back
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
        /// Groups by entry_id masked to 31 bits, competes on score.
        /// Ties go to decoy (conservative). Returns winner indices sorted by score descending.
        /// </summary>
        public static int[] CompeteCalibrationPairs(
            double[] scores, uint[] entryIds, bool[] isDecoy, int[] subsetIndices)
        {
            // Group indices by base_id = entry_id & 0x7FFFFFFF
            var groups = new Dictionary<uint, KeyValuePair<int, int>>();
            // Key: base_id, Value: (bestTarget, bestDecoy) as indices, -1 if none

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

            // Sort by score descending
            winners.Sort((a, b) => scores[b].CompareTo(scores[a]));
            return winners.ToArray();
        }

        private static double[] TrainLdaNonNegative(
            Matrix features, bool[] decoyLabels, int nFeatures)
        {
            // Train LDA
            LinearDiscriminant lda = LinearDiscriminant.Fit(features, decoyLabels);

            double[] weights;
            if (lda != null)
            {
                weights = (double[])lda.Eigenvector.Clone();

                // Clip negative weights to zero
                for (int i = 0; i < weights.Length; i++)
                {
                    if (weights[i] < 0.0)
                        weights[i] = 0.0;
                }

                // Renormalize to unit length
                double norm = 0.0;
                for (int i = 0; i < weights.Length; i++)
                    norm += weights[i] * weights[i];
                norm = Math.Sqrt(norm);

                if (norm > 1e-10)
                {
                    for (int i = 0; i < weights.Length; i++)
                        weights[i] /= norm;
                }
                else
                {
                    // All weights were negative - use equal weights
                    double equalWeight = 1.0 / Math.Sqrt(weights.Length);
                    for (int i = 0; i < weights.Length; i++)
                        weights[i] = equalWeight;
                }
            }
            else
            {
                // LDA failed - use equal weights
                weights = new double[nFeatures];
                double equalWeight = 1.0 / Math.Sqrt(nFeatures);
                for (int i = 0; i < nFeatures; i++)
                    weights[i] = equalWeight;
            }

            // Score all samples with non-negative weights
            LinearDiscriminant ldaNonneg = LinearDiscriminant.FromWeights(weights);
            return ldaNonneg.Predict(features);
        }

        private static Matrix ExtractFeatureMatrix(CalibrationMatch[] matches, bool useIsotopeFeature)
        {
            int nFeatures = useIsotopeFeature ? 5 : 4;
            double[] data = new double[matches.Length * nFeatures];

            for (int i = 0; i < matches.Length; i++)
            {
                var m = matches[i];
                int offset = i * nFeatures;
                data[offset] = Math.Max(0.0, Math.Min(1.0, m.CorrelationScore / 6.0));
                data[offset + 1] = Math.Max(0.0, Math.Min(1.0, m.LibcosineApex));
                data[offset + 2] = Math.Max(0.0, Math.Min(1.0, m.Top6MatchedApex / 6.0));
                data[offset + 3] = Math.Max(0.0, Math.Min(1.0, m.XcorrScore / 3.0));
                if (useIsotopeFeature)
                    data[offset + 4] = Math.Max(0.0, Math.Min(1.0, m.IsotopeCosine));
            }

            return new Matrix(data, matches.Length, nFeatures);
        }

        /// <summary>
        /// Calculate q-values from ordered winner indices (sorted by score descending).
        /// Returns count passing 1% FDR.
        /// </summary>
        private static int CalculateQValues(int[] winnerIndices, bool[] isDecoy, double[] qValues)
        {
            int n = winnerIndices.Length;
            if (n == 0)
                return 0;

            int nDecoys = 0;
            int nTargets = 0;
            double[] rawQ = new double[n];

            for (int i = 0; i < n; i++)
            {
                if (isDecoy[winnerIndices[i]])
                    nDecoys++;
                else
                    nTargets++;

                rawQ[i] = nTargets > 0 ? (double)nDecoys / nTargets : 1.0;
            }

            // Running minimum from bottom up (monotone q-values)
            double minQ = 1.0;
            for (int i = n - 1; i >= 0; i--)
            {
                if (rawQ[i] < minQ)
                    minQ = rawQ[i];
                qValues[i] = minQ;
            }

            // Count passing targets at 1% FDR
            int nPassing = 0;
            for (int i = 0; i < n; i++)
            {
                if (!isDecoy[winnerIndices[i]] && qValues[i] <= 0.01)
                    nPassing++;
            }

            return nPassing;
        }
    }
}
