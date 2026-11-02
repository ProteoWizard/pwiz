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
using pwiz.Osprey.Chromatography;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// How far the chosen peak dominates the other apexes in the candidate's XIC range, after
    /// DIA-NN 1.7.12's pBestCorrDelta and pTotCorrSum (src/diann.cpp, peaks() 7423-7536 and
    /// score() 7715-7717, CC BY 4.0, Demichev et al.). DIA-NN's peak search walks every scan k of
    /// the chromatogram with a window of k +/- S over its top-6 library fragments; at each k it
    /// scores each fragment by its summed correlation with the other fragments over the window
    /// (just the one correlation when only two fragments have signal at k), takes the best-scoring
    /// fragment whose score is at least 0.5 and whose 3-point-smoothed elution peaks at k (no higher
    /// value within S/3 scans, at least 0.99 x the maximum within S - 1), and accepts k as a candidate
    /// apex with that score. best_corr_sum is the highest accepted score and total_corr_sum their sum;
    /// for the scored peak pBestCorrDelta = pTimeCorr - best_corr_sum and
    /// pTotCorrSum = log(max(1, pTimeCorr) / (total_corr_sum + 1)).
    /// <para>
    /// The search here is that walk over Osprey's XICs, with S and pTimeCorr (tc_sum6) taken from
    /// <see cref="TimeCorrelations"/>. Two departures, both because Osprey lacks the data at scoring
    /// time: DIA-NN also adds each fragment's correlation with the MS1 precursor trace to its score
    /// and rejects an apex whose MS1 correlation is too low (MS1PeakSelection, on by default), which
    /// is left out because Osprey samples MS1 only along the chosen peak, so the sums here are
    /// fragment-only; and the walk covers Osprey's XIC extraction range rather than DIA-NN's
    /// whole-run chromatogram. The chosen peak is Osprey's pick, which need not be the best-scoring
    /// apex, so <see cref="CorrMargin"/> adds a direct comparison: the same score at the chosen apex
    /// against the best accepted apex more than max(S / 2, 1) scans away (DIA-NN's same-peak distance).
    /// Computed once per candidate and cached on the <see cref="OspreyScoringContext"/>.
    /// </para>
    /// </summary>
    internal sealed class PeakDominance
    {
        /// <summary>DIA-NN MinPeakHeight: the least intensity counted as signal.</summary>
        public const double MIN_PEAK_HEIGHT = 0.01;

        /// <summary>DIA-NN MinCorrScore: the least fragment score accepted at an apex.</summary>
        public const double MIN_CORR_SCORE = 0.5;

        /// <summary>DIA-NN PeakApexEvidence: an apex is at least this fraction of the maximum within S - 1 scans.</summary>
        public const double PEAK_APEX_EVIDENCE = 0.99;

        // DIA-NN's E: scores at or below it do not count.
        private const double EPSILON = 1e-9;

        /// <summary>The top-6 time-correlation sum at the chosen peak (DIA-NN pTimeCorr, tc_sum6).</summary>
        public double TimeCorrSum { get; private set; }

        /// <summary>Highest score among the accepted apexes (DIA-NN best_corr_sum), 0 when none.</summary>
        public double BestCorrSum { get; private set; }

        /// <summary>Sum of the accepted apexes' scores (DIA-NN total_corr_sum).</summary>
        public double TotalCorrSum { get; private set; }

        /// <summary>Number of accepted apexes in the XIC range.</summary>
        public int ApexCount { get; private set; }

        /// <summary>The best fragment score at the chosen apex, without the apex gates; 0 without a window.</summary>
        public double ChosenCorrSum { get; private set; }

        /// <summary>Highest score among the accepted apexes more than max(S / 2, 1) scans from the chosen one, 0 when none.</summary>
        public double BestOtherCorrSum { get; private set; }

        /// <summary>DIA-NN pBestCorrDelta: <see cref="TimeCorrSum"/> - <see cref="BestCorrSum"/>.</summary>
        public double BestCorrDelta { get { return TimeCorrSum - BestCorrSum; } }

        /// <summary>DIA-NN pTotCorrSum: log(max(1, <see cref="TimeCorrSum"/>) / (<see cref="TotalCorrSum"/> + 1)).</summary>
        public double TotCorrSum { get { return Math.Log(Math.Max(1.0, TimeCorrSum) / (TotalCorrSum + 1.0)); } }

        /// <summary><see cref="ChosenCorrSum"/> - <see cref="BestOtherCorrSum"/>.</summary>
        public double CorrMargin { get { return ChosenCorrSum - BestOtherCorrSum; } }

        public static PeakDominance GetOrCompute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out PeakDominance dominance))
                return dominance;
            dominance = Compute(peakData, TimeCorrelations.GetOrCompute(context, peakData));
            context.AddInfo(dominance);
            return dominance;
        }

        internal static PeakDominance Compute(IOspreyDetailedPeakData peakData, TimeCorrelations timeCorr)
        {
            var result = new PeakDominance { TimeCorrSum = timeCorr.TopSum };
            if (timeCorr.Elution == null)
                return result;
            var traces = TopTraces(peakData.Xics, timeCorr.RankedFragments);
            if (traces.Length < 2)
                return result;
            int s = timeCorr.HalfScans;
            int n = traces[0].Length;
            var smoothed = new double[traces.Length][];
            for (int fr = 0; fr < traces.Length; fr++)
                smoothed[fr] = TimeCorrelations.Smooth(traces[fr], 0, n - 1);

            int apex = Math.Max(0, Math.Min(n - 1, peakData.PeakBounds.ApexIndex));
            int samePeak = Math.Max(s / 2, 1);
            var scorer = new ApexScorer(traces, smoothed, s);
            for (int k = s + 1; k < n - s - 1; k++)
            {
                if (!scorer.AnyApexAt(k))
                    continue;
                double score = scorer.AcceptedScore(k);
                if (double.IsNaN(score))
                    continue;
                result.TotalCorrSum += score;
                result.ApexCount++;
                result.BestCorrSum = Math.Max(result.BestCorrSum, score);
                if (Math.Abs(k - apex) > samePeak)
                    result.BestOtherCorrSum = Math.Max(result.BestOtherCorrSum, score);
            }
            result.ChosenCorrSum = scorer.BestScore(Math.Max(0, apex - s), Math.Min(n - 1, apex + s));
            return result;
        }

        // The XIC intensities of the top-ranked library fragments, in rank order, skipping ranks without an XIC
        // (DIA-NN would see an all-zero trace there, which never has signal and correlates 0 with the others).
        private static double[][] TopTraces(IReadOnlyList<XicData> xics, int[] rankedFragments)
        {
            var traces = new List<double[]>();
            foreach (int xicIndex in TopFragments.XicIndices(xics, rankedFragments))
            {
                if (xicIndex >= 0)
                    traces.Add(xics[xicIndex].Intensities);
            }
            return traces.ToArray();
        }

        /// <summary>The per-scan step of DIA-NN's peak search over a fixed set of fragment traces.</summary>
        private sealed class ApexScorer
        {
            private readonly double[][] _traces;
            private readonly double[][] _smoothed;
            private readonly int _halfScans;
            private readonly double[] _scores;
            private readonly int[] _order;

            public ApexScorer(double[][] traces, double[][] smoothed, int halfScans)
            {
                _traces = traces;
                _smoothed = smoothed;
                _halfScans = halfScans;
                _scores = new double[traces.Length];
                _order = new int[traces.Length];
            }

            /// <summary>Whether any fragment's smoothed elution passes the apex gates at k, a cheap pre-check.</summary>
            public bool AnyApexAt(int k)
            {
                for (int fr = 0; fr < _smoothed.Length; fr++)
                {
                    if (IsApex(fr, k))
                        return true;
                }
                return false;
            }

            /// <summary>The score DIA-NN accepts at apex k (window k +/- S, fully inside the traces), NaN when none.</summary>
            public double AcceptedScore(int k)
            {
                int p = _traces.Length;
                int present = 0, flagged = 0, first = -1, second = -1;
                for (int fr = 0; fr < p; fr++)
                {
                    var trace = _traces[fr];
                    bool flag = false;
                    if (trace[k] >= MIN_PEAK_HEIGHT)
                    {
                        present++;
                        flag = true;
                    }
                    else if (trace[k - 1] >= MIN_PEAK_HEIGHT || trace[k + 1] >= MIN_PEAK_HEIGHT)
                        flag = true;
                    if (flag)
                    {
                        if (flagged == 0)
                            first = fr;
                        else if (flagged == 1)
                            second = fr;
                        flagged++;
                    }
                }
                if (present == 0 || flagged < 2)
                    return double.NaN;

                int start = k - _halfScans, end = k + _halfScans;
                int count;
                if (flagged == 2)
                {
                    // Only two fragments with signal: both score their one correlation, the larger trace first.
                    double corr = Correlation(first, second, start, end);
                    if (corr <= EPSILON)
                        return double.NaN;
                    bool secondLarger = Sum(second, start, end) > Sum(first, start, end);
                    _order[0] = secondLarger ? second : first;
                    _order[1] = secondLarger ? first : second;
                    _scores[first] = _scores[second] = corr;
                    count = 2;
                }
                else
                {
                    // All top-6 traces, flagged or not: DIA-NN's get_corr_matrix covers every fragment here, and
                    // the flags only decide between this branch and the two-fragment one.
                    count = RankByCorrelationSum(start, end);
                }

                for (int i = 0; i < count; i++)
                {
                    int fr = _order[i];
                    if (_scores[fr] < MIN_CORR_SCORE)
                        continue;
                    if (IsApex(fr, k))
                        return _scores[fr];
                }
                return double.NaN;
            }

            /// <summary>The highest summed correlation of a fragment with the others over [start, end], 0 when none is positive.</summary>
            public double BestScore(int start, int end)
            {
                if (end - start < 2)
                    return 0.0;
                int count = RankByCorrelationSum(start, end);
                return count > 0 ? _scores[_order[0]] : 0.0;
            }

            // Scores every fragment by its summed correlation with the others over [start, end] and fills
            // _order with those above EPSILON, highest first and ties in fragment order (DIA-NN's insertion).
            private int RankByCorrelationSum(int start, int end)
            {
                int p = _traces.Length;
                Array.Clear(_scores, 0, p);
                for (int i = 0; i < p; i++)
                {
                    for (int j = i + 1; j < p; j++)
                    {
                        double corr = Correlation(i, j, start, end);
                        _scores[i] += corr;
                        _scores[j] += corr;
                    }
                }
                int count = 0;
                for (int fr = 0; fr < p; fr++)
                {
                    if (_scores[fr] <= EPSILON)
                        continue;
                    int pos = count++;
                    while (pos > 0 && _scores[fr] > _scores[_order[pos - 1]])
                    {
                        _order[pos] = _order[pos - 1];
                        pos--;
                    }
                    _order[pos] = fr;
                }
                return count;
            }

            // DIA-NN's apex gates on the 3-point-smoothed elution of fragment fr: signal at k, no higher
            // value within max(S / 3, 1) scans, and at least PEAK_APEX_EVIDENCE of the maximum within S - 1.
            private bool IsApex(int fr, int k)
            {
                var elution = _smoothed[fr];
                double current = elution[k];
                if (current < MIN_PEAK_HEIGHT)
                    return false;
                int near = Math.Max(_halfScans / 3, 1);
                for (int pos = k - near; pos <= k + near; pos++)
                {
                    if (elution[pos] > current)
                        return false;
                }
                double max = current;
                int far = _halfScans - 1;
                for (int pos = k - far; pos <= k + far; pos++)
                    max = Math.Max(max, elution[pos]);
                return current >= max * PEAK_APEX_EVIDENCE;
            }

            private double Correlation(int i, int j, int start, int end)
            {
                double corr = ScoringMath.PearsonOverRange(_traces[i], _traces[j], start, end);
                return double.IsNaN(corr) ? 0.0 : corr;
            }

            private double Sum(int fr, int start, int end)
            {
                double sum = 0.0;
                for (int i = start; i <= end; i++)
                    sum += _traces[fr][i];
                return sum;
            }
        }
    }

    /// <summary>dom_best_corr_delta: tc_sum6 minus the best apex correlation sum in the XIC range (DIA-NN pBestCorrDelta).</summary>
    internal sealed class DominanceBestCorrDeltaCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"dom_best_corr_delta"; } }

        public override string DisplayName { get { return OspreyScoringResources.DominanceBestCorrDeltaCalc_DisplayName_Time_correlation_minus_best_apex_correlation_sum; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return PeakDominance.GetOrCompute(context, peakData).BestCorrDelta;
        }
    }

    /// <summary>dom_tot_corr_sum: log(max(1, tc_sum6) / (total apex correlation sum + 1)) (DIA-NN pTotCorrSum).</summary>
    internal sealed class DominanceTotalCorrSumCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"dom_tot_corr_sum"; } }

        public override string DisplayName { get { return OspreyScoringResources.DominanceTotalCorrSumCalc_DisplayName_Time_correlation_over_total_apex_correlation_sum__log_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return PeakDominance.GetOrCompute(context, peakData).TotCorrSum;
        }
    }

    /// <summary>dom_apex_count: apexes accepted by the DIA-NN peak search over the XIC range.</summary>
    internal sealed class DominanceApexCountCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"dom_apex_count"; } }

        public override string DisplayName { get { return OspreyScoringResources.DominanceApexCountCalc_DisplayName_Correlated_apexes_in_XIC_range; } }

        public override bool IsReversedScore { get { return true; } }   // fewer is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return PeakDominance.GetOrCompute(context, peakData).ApexCount;
        }
    }

    /// <summary>dom_corr_margin: the chosen apex's correlation sum minus the best accepted apex elsewhere in the XIC range.</summary>
    internal sealed class DominanceCorrMarginCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"dom_corr_margin"; } }

        public override string DisplayName { get { return OspreyScoringResources.DominanceCorrMarginCalc_DisplayName_Apex_correlation_margin_over_best_other_apex; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return PeakDominance.GetOrCompute(context, peakData).CorrMargin;
        }
    }
}
