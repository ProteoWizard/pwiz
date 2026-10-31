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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Fragment elution correlations after DIA-NN 1.7.12's pTimeCorr, pCorr and pResCorr
    /// (src/diann.cpp 7566-7682, CC BY 4.0, Demichev et al.), its strongest scores on SEA-AD
    /// Astral data. Unlike Osprey's other correlations, which run between the peak bounds, these
    /// run over a fixed window of apex +/- S scans, with S set from a time half-width
    /// (<see cref="HALF_WINDOW_MINUTES"/>, after DIA-NN's 2.2 x the run's peak
    /// width; 0.15 min here, about 2.2 x the FWHM of SEA-AD Astral peptides): a real peak has to rise out of its own baseline inside the window, which a
    /// fragment that is flat or noisy across a narrow pick does not. The reference is the
    /// fragment whose XIC correlates best with the others over the window, smoothed with
    /// DIA-NN's 3-point kernel. Computed once per candidate and cached on the
    /// <see cref="OspreyScoringContext"/>.
    /// </summary>
    internal sealed class TimeCorrelations
    {
        /// <summary>Half-width of the correlation window in minutes.</summary>
        public const double HALF_WINDOW_MINUTES = 0.15;

        /// <summary>Library fragment ranks with their own correlation feature, and summed into the top-6 score.</summary>
        public const int RANK_COUNT = 6;

        /// <summary>Correlation with the smoothed reference of library fragment rank r (index r - 1), 0 without an XIC.</summary>
        public double[] RankCorrelations { get; private set; }

        /// <summary>Sum of <see cref="RankCorrelations"/> (DIA-NN pTimeCorr).</summary>
        public double TopSum { get; private set; }

        /// <summary>Summed correlation of the XICs of fragments beyond the top ranks (DIA-NN pResCorr over Osprey's XICs).</summary>
        public double ResidualSum { get; private set; }

        /// <summary><see cref="ResidualSum"/> per fragment beyond the top ranks, 0 when there are none.</summary>
        public double ResidualMean { get; private set; }

        /// <summary>First XIC scan index of the window (apex - S, clipped to the XICs).</summary>
        public int WindowStart { get; private set; }

        /// <summary>
        /// The smoothed reference elution over the window, one value per scan from
        /// <see cref="WindowStart"/>; null when no window could be set up (all correlations 0).
        /// </summary>
        public double[] Elution { get; private set; }

        /// <summary>Library fragment indices of the top ranks (index r - 1 is rank r), null with <see cref="Elution"/>.</summary>
        public int[] RankedFragments { get; private set; }

        public static TimeCorrelations GetOrCompute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out TimeCorrelations timeCorr))
                return timeCorr;
            timeCorr = Compute(peakData, OspreyEnvironment.TcHalfWindowMinutes ?? HALF_WINDOW_MINUTES);
            context.AddInfo(timeCorr);
            return timeCorr;
        }

        internal static TimeCorrelations Compute(IOspreyDetailedPeakData peakData, double halfWindowMinutes)
        {
            var result = new TimeCorrelations { RankCorrelations = new double[RANK_COUNT] };
            var xics = peakData.Xics;
            var peak = peakData.PeakBounds;
            var fragments = peakData.Candidate?.Fragments;
            if (xics.Count < 2 || peak == null || fragments == null || fragments.Count == 0)
                return result;
            int n = xics[0].Intensities.Length;
            int apex = Math.Max(0, Math.Min(n - 1, peak.ApexIndex));
            int halfScans = HalfWindowScans(xics[0].RetentionTimes, apex, halfWindowMinutes);
            int start = Math.Max(0, apex - halfScans);
            int end = Math.Min(n - 1, apex + halfScans);
            if (end - start < 2)
                return result;

            var elution = Smooth(xics[ChooseReference(xics, start, end)].Intensities, start, end);
            int[] ranked = FragmentMath.TopIndicesByIntensity(fragments, Math.Min(RANK_COUNT, fragments.Count));
            result.WindowStart = start;
            result.Elution = elution;
            result.RankedFragments = ranked;
            int nResidual = 0;
            foreach (var xic in xics)
            {
                double corr = ScoringMath.PearsonCorrelationInRange(elution, Slice(xic.Intensities, start, end), 0, end - start);
                if (double.IsNaN(corr))
                    corr = 0.0;
                int rank = Array.IndexOf(ranked, xic.FragmentIndex);
                if (rank >= 0)
                    result.RankCorrelations[rank] = corr;
                else
                {
                    result.ResidualSum += corr;
                    nResidual++;
                }
            }
            foreach (double corr in result.RankCorrelations)
                result.TopSum += corr;
            result.ResidualMean = nResidual > 0 ? result.ResidualSum / nResidual : 0.0;
            return result;
        }

        // Scans spanning halfWindowMinutes on each side of the apex, at the scan spacing around the apex; at least 2.
        private static int HalfWindowScans(double[] rts, int apex, double halfWindowMinutes)
        {
            if (rts == null || rts.Length < 2)
                return 2;
            int lo = Math.Max(0, apex - 5);
            int hi = Math.Min(rts.Length - 1, apex + 5);
            double spacing = (rts[hi] - rts[lo]) / Math.Max(1, hi - lo);
            if (spacing <= 0)
                return 2;
            return Math.Max(2, (int) Math.Round(halfWindowMinutes / spacing));
        }

        // The XIC with the highest summed correlation to the others over the window; ties go to the first.
        private static int ChooseReference(IReadOnlyList<XicData> xics, int start, int end)
        {
            var sums = new double[xics.Count];
            for (int i = 0; i < xics.Count; i++)
            {
                for (int j = i + 1; j < xics.Count; j++)
                {
                    double corr = ScoringMath.PearsonCorrelationInRange(xics[i].Intensities, xics[j].Intensities, start, end);
                    if (double.IsNaN(corr))
                        continue;
                    sums[i] += corr;
                    sums[j] += corr;
                }
            }
            int best = 0;
            for (int i = 1; i < sums.Length; i++)
            {
                if (sums[i] > sums[best])
                    best = i;
            }
            return best;
        }

        // DIA-NN's smooth(): weights 1/4, 1/2, 1/4 inside, 2/3 and 1/3 at the ends.
        private static double[] Smooth(double[] values, int start, int end)
        {
            int n = end - start + 1;
            var smoothed = new double[n];
            smoothed[0] = 2.0 / 3.0 * values[start] + 1.0 / 3.0 * values[start + 1];
            smoothed[n - 1] = 2.0 / 3.0 * values[end] + 1.0 / 3.0 * values[end - 1];
            for (int i = 1; i < n - 1; i++)
                smoothed[i] = 0.5 * values[start + i] + 0.25 * (values[start + i - 1] + values[start + i + 1]);
            return smoothed;
        }

        private static double[] Slice(double[] values, int start, int end)
        {
            var slice = new double[end - start + 1];
            Array.Copy(values, start, slice, 0, slice.Length);
            return slice;
        }
    }

    /// <summary>tc_sum6: summed correlation of the top six library fragments with the smoothed reference over apex +/- S.</summary>
    internal sealed class TimeCorrelationSumCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"tc_sum6"; } }

        public override string DisplayName { get { return OspreyScoringResources.TimeCorrelationSumCalc_DisplayName_Time_correlation_with_reference__top_6_sum_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return TimeCorrelations.GetOrCompute(context, peakData).TopSum;
        }
    }

    /// <summary>tc_corr_rankK: library fragment rank K's correlation with the smoothed reference over apex +/- S.</summary>
    internal sealed class TimeCorrelationRankCalc : DetailedOspreyFeatureCalculator
    {
        private readonly int _rank;

        public TimeCorrelationRankCalc(int rank)
        {
            _rank = rank;
        }

        public override string Name { get { return string.Format(@"tc_corr_rank{0}", _rank); } }

        public override string DisplayName
        {
            get { return string.Format(OspreyScoringResources.TimeCorrelationRankCalc_DisplayName_Time_correlation_of_fragment__0_, _rank); }
        }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return TimeCorrelations.GetOrCompute(context, peakData).RankCorrelations[_rank - 1];
        }
    }

    /// <summary>tc_res_sum / tc_res_mean: correlation of the fragments beyond the top six, summed or per fragment.</summary>
    internal sealed class TimeCorrelationResidualCalc : DetailedOspreyFeatureCalculator
    {
        private readonly bool _mean;

        public TimeCorrelationResidualCalc(bool mean)
        {
            _mean = mean;
        }

        public override string Name { get { return _mean ? @"tc_res_mean" : @"tc_res_sum"; } }

        public override string DisplayName
        {
            get
            {
                return _mean
                    ? OspreyScoringResources.TimeCorrelationResidualCalc_DisplayName_Time_correlation_beyond_top_6__mean_
                    : OspreyScoringResources.TimeCorrelationResidualCalc_DisplayName_Time_correlation_beyond_top_6__sum_;
            }
        }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            var timeCorr = TimeCorrelations.GetOrCompute(context, peakData);
            return _mean ? timeCorr.ResidualMean : timeCorr.ResidualSum;
        }
    }
}
