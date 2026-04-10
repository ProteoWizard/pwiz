using System;
using System.Collections.Generic;

namespace pwiz.OspreySharp.FDR
{
    /// <summary>
    /// Result of target-decoy competition.
    /// </summary>
    public class CompetitionResult<T>
    {
        /// <summary>Winning items that pass FDR threshold (targets only).</summary>
        public List<T> PassingTargets { get; set; }

        /// <summary>Number of target winners.</summary>
        public int NTargetWins { get; set; }

        /// <summary>Number of decoy winners.</summary>
        public int NDecoyWins { get; set; }

        /// <summary>FDR at the threshold.</summary>
        public double FdrAtThreshold { get; set; }

        public CompetitionResult()
        {
            PassingTargets = new List<T>();
        }
    }

    /// <summary>
    /// Detection counts at various FDR thresholds.
    /// </summary>
    public class FdrCounts
    {
        /// <summary>Count at 0.1% FDR.</summary>
        public int At001 { get; set; }

        /// <summary>Count at 1% FDR.</summary>
        public int At01 { get; set; }

        /// <summary>Count at 5% FDR.</summary>
        public int At05 { get; set; }

        /// <summary>Count at 10% FDR.</summary>
        public int At10 { get; set; }

        /// <summary>Total count.</summary>
        public int Total { get; set; }
    }

    /// <summary>
    /// FDR controller using target-decoy competition.
    /// Port of FdrController from osprey-fdr/src/lib.rs.
    /// </summary>
    public class FdrController
    {
        private readonly double _fdrThreshold;

        /// <summary>
        /// Create a new FDR controller.
        /// </summary>
        public FdrController(double fdrThreshold)
        {
            _fdrThreshold = fdrThreshold;
        }

        /// <summary>
        /// Get the FDR threshold.
        /// </summary>
        public double Threshold { get { return _fdrThreshold; } }

        /// <summary>
        /// Run target-decoy competition and return targets passing FDR threshold.
        ///
        /// This is the proper competition approach (matching pyXcorrDIA):
        /// 1. Each target competes with its paired decoy - higher score wins
        /// 2. Winners are sorted by score descending
        /// 3. Walk down list computing FDR = decoy_wins / target_wins at each position
        /// 4. Find the MAXIMUM cumulative_targets at any position where FDR &lt;= threshold
        /// 5. Return all targets up to that count
        /// </summary>
        /// <typeparam name="T">Type of items being competed.</typeparam>
        /// <param name="items">Collection of items to compete.</param>
        /// <param name="getScore">Function to extract score from an item.</param>
        /// <param name="isDecoy">Function to determine if an item is a decoy.</param>
        /// <param name="getEntryId">Function to extract entry ID (high bit = decoy).</param>
        /// <returns>CompetitionResult with passing targets and statistics.</returns>
        public CompetitionResult<T> CompeteAndFilter<T>(
            IEnumerable<T> items,
            Func<T, double> getScore,
            Func<T, bool> isDecoy,
            Func<T, uint> getEntryId)
        {
            // Group by base_id (mask off high bit to get base ID)
            var targetScores = new Dictionary<uint, KeyValuePair<T, double>>();
            var decoyScores = new Dictionary<uint, double>();

            foreach (var item in items)
            {
                double score = getScore(item);
                bool isDecoyItem = isDecoy(item);
                uint entryId = getEntryId(item);
                uint baseId = entryId & 0x7FFFFFFF;

                if (isDecoyItem)
                {
                    double existing;
                    if (decoyScores.TryGetValue(baseId, out existing))
                    {
                        if (score > existing)
                            decoyScores[baseId] = score;
                    }
                    else
                    {
                        decoyScores[baseId] = score;
                    }
                }
                else
                {
                    KeyValuePair<T, double> existing;
                    if (targetScores.TryGetValue(baseId, out existing))
                    {
                        if (score > existing.Value)
                            targetScores[baseId] = new KeyValuePair<T, double>(item, score);
                    }
                    else
                    {
                        targetScores[baseId] = new KeyValuePair<T, double>(item, score);
                    }
                }
            }

            // Competition: for each target, compare with its decoy
            // Winner advances. Ties go to decoy (conservative for FDR estimation).
            var winners = new List<WinnerEntry<T>>(targetScores.Count);

            foreach (var kvp in targetScores)
            {
                uint baseId = kvp.Key;
                T targetItem = kvp.Value.Key;
                double targetScore = kvp.Value.Value;

                double decoyScore;
                if (!decoyScores.TryGetValue(baseId, out decoyScore))
                    decoyScore = double.NegativeInfinity;

                if (targetScore > decoyScore)
                {
                    // Target wins (strict greater than)
                    winners.Add(new WinnerEntry<T>(targetScore, true, targetItem));
                }
                else
                {
                    // Decoy wins (including ties - conservative)
                    winners.Add(new WinnerEntry<T>(decoyScore, false, default(T)));
                }
            }

            // Sort winners by score descending (highest scores first)
            winners.Sort((a, b) => b.Score.CompareTo(a.Score));

            // First pass: walk down and find MAX cumulative_targets at any position where FDR <= threshold
            int nTargetWins = 0;
            int nDecoyWins = 0;
            int maxTargetsAtValidFdr = 0;
            double fdrAtThreshold = 0.0;

            for (int i = 0; i < winners.Count; i++)
            {
                if (winners[i].IsTargetWinner)
                    nTargetWins++;
                else
                    nDecoyWins++;

                double fdr = nTargetWins > 0
                    ? (double)nDecoyWins / nTargetWins
                    : 1.0;

                if (fdr <= _fdrThreshold)
                {
                    maxTargetsAtValidFdr = nTargetWins;
                    fdrAtThreshold = fdr;
                }
            }

            // Second pass: collect the first maxTargetsAtValidFdr target winners
            var passingTargets = new List<T>(maxTargetsAtValidFdr);
            int targetsCollected = 0;

            for (int i = 0; i < winners.Count; i++)
            {
                if (winners[i].IsTargetWinner)
                {
                    targetsCollected++;
                    if (targetsCollected <= maxTargetsAtValidFdr)
                    {
                        passingTargets.Add(winners[i].Item);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return new CompetitionResult<T>
            {
                PassingTargets = passingTargets,
                NTargetWins = nTargetWins,
                NDecoyWins = nDecoyWins,
                FdrAtThreshold = fdrAtThreshold
            };
        }

        /// <summary>
        /// Filter targets by q-value threshold.
        /// </summary>
        public List<T> FilterByQvalue<T>(IList<T> items, IList<double> qvalues)
        {
            var result = new List<T>();
            int count = Math.Min(items.Count, qvalues.Count);
            for (int i = 0; i < count; i++)
            {
                if (qvalues[i] <= _fdrThreshold)
                    result.Add(items[i]);
            }
            return result;
        }

        /// <summary>
        /// Count detections at various FDR thresholds.
        /// </summary>
        public FdrCounts CountAtThresholds(IList<double> qvalues)
        {
            var counts = new FdrCounts { Total = qvalues.Count };
            for (int i = 0; i < qvalues.Count; i++)
            {
                double q = qvalues[i];
                if (q <= 0.001) counts.At001++;
                if (q <= 0.01) counts.At01++;
                if (q <= 0.05) counts.At05++;
                if (q <= 0.10) counts.At10++;
            }
            return counts;
        }

        /// <summary>
        /// Internal helper for tracking winners during competition.
        /// </summary>
        private class WinnerEntry<TItem>
        {
            public readonly double Score;
            public readonly bool IsTargetWinner;
            public readonly TItem Item;

            public WinnerEntry(double score, bool isTargetWinner, TItem item)
            {
                Score = score;
                IsTargetWinner = isTargetWinner;
                Item = item;
            }
        }
    }
}
