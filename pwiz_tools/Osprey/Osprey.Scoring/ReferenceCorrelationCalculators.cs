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
    /// Every fragment's elution correlation with one reference fragment, after the scoring of
    /// DIA-NN 1.7.12 (src/diann.cpp, CC BY 4.0, Demichev et al.): the reference is the fragment
    /// whose XIC correlates best with all the others over the peak, so an interfered fragment,
    /// which correlates poorly with the rest, is never chosen - unlike the peak-shape reference,
    /// which is the most intense fragment and so the one interference inflates. The correlations
    /// of the other fragments with it are kept sorted best first, raw and after a 3-scan running
    /// minimum that removes single-scan spikes (DIA-NN's pMinCorr). Computed once per candidate
    /// and cached on the <see cref="OspreyScoringContext"/>.
    /// </summary>
    internal sealed class ReferenceCorrelations
    {
        /// <summary>Correlation at or above which a fragment counts as following the reference.</summary>
        public const double GOOD_CORRELATION = 0.9;

        /// <summary>Other fragments' correlation with the reference, highest first; empty with under two XICs.</summary>
        public double[] SortedCorrelations { get; private set; }

        /// <summary>The same after a 3-scan running minimum of every XIC, highest first.</summary>
        public double[] SortedMinFilteredCorrelations { get; private set; }

        /// <summary>Index into the XICs of the reference fragment; 0 with under two XICs.</summary>
        public int ReferenceIndex { get; private set; }

        /// <summary>log10(1 + summed apex intensity of every fragment but the most intense one).</summary>
        public double ApexLogIntensityExcludingTop { get; private set; }

        public static ReferenceCorrelations GetOrCompute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out ReferenceCorrelations refCorr))
                return refCorr;
            refCorr = Compute(peakData);
            context.AddInfo(refCorr);
            return refCorr;
        }

        /// <summary>The k-th best correlation with the reference (1-based), or 0 when there are fewer.</summary>
        public double KthBest(int k)
        {
            return k <= SortedCorrelations.Length ? SortedCorrelations[k - 1] : 0.0;
        }

        /// <summary>Number of fragments correlating with the reference at <see cref="GOOD_CORRELATION"/> or above.</summary>
        public int CountGood()
        {
            int count = 0;
            foreach (double corr in SortedCorrelations)
            {
                if (corr >= GOOD_CORRELATION)
                    count++;
            }
            return count;
        }

        /// <summary>Mean of the best k min-filtered correlations, counting missing fragments as 0.</summary>
        public double MinFilteredTopMean(int k)
        {
            double sum = 0;
            for (int i = 0; i < k && i < SortedMinFilteredCorrelations.Length; i++)
                sum += SortedMinFilteredCorrelations[i];
            return sum / k;
        }

        private static ReferenceCorrelations Compute(IOspreyDetailedPeakData peakData)
        {
            var result = new ReferenceCorrelations
            {
                SortedCorrelations = Array.Empty<double>(),
                SortedMinFilteredCorrelations = Array.Empty<double>(),
            };
            var xics = peakData.Xics;
            var peak = peakData.PeakBounds;
            if (xics.Count == 0 || peak == null)
                return result;
            result.ApexLogIntensityExcludingTop = ApexLogIntensityWithoutTop(xics, peak.ApexIndex);
            if (xics.Count < 2)
                return result;

            int reference = ChooseReference(xics, peak);
            result.ReferenceIndex = reference;
            result.SortedCorrelations = SortedCorrelationsWith(xics, reference, peak, x => x);
            result.SortedMinFilteredCorrelations = SortedCorrelationsWith(xics, reference, peak, RunningMinimum);
            return result;
        }

        private static double ApexLogIntensityWithoutTop(IReadOnlyList<XicData> xics, int apex)
        {
            double sum = 0, top = 0;
            foreach (var xic in xics)
            {
                if (apex < 0 || apex >= xic.Intensities.Length)
                    continue;
                double v = xic.Intensities[apex];
                sum += v;
                top = Math.Max(top, v);
            }
            return Math.Log10(1.0 + Math.Max(0.0, sum - top));
        }

        // The fragment with the highest summed correlation to the others over the peak; ties go to the first.
        private static int ChooseReference(IReadOnlyList<XicData> xics, XICPeakBounds peak)
        {
            var sums = new double[xics.Count];
            for (int i = 0; i < xics.Count; i++)
            {
                for (int j = i + 1; j < xics.Count; j++)
                {
                    double corr = ScoringMath.PearsonCorrelationInRange(
                        xics[i].Intensities, xics[j].Intensities, peak.StartIndex, peak.EndIndex);
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

        private static double[] SortedCorrelationsWith(IReadOnlyList<XicData> xics, int reference, XICPeakBounds peak,
            Func<double[], double[]> transform)
        {
            var refIntensities = transform(xics[reference].Intensities);
            var corrs = new double[xics.Count - 1];
            int n = 0;
            for (int i = 0; i < xics.Count; i++)
            {
                if (i == reference)
                    continue;
                double corr = ScoringMath.PearsonCorrelationInRange(
                    refIntensities, transform(xics[i].Intensities), peak.StartIndex, peak.EndIndex);
                corrs[n++] = double.IsNaN(corr) ? 0.0 : corr;
            }
            Array.Sort(corrs); // Array.Sort OK: primitive values only, ties are equal values
            Array.Reverse(corrs);
            return corrs;
        }

        // Each point replaced by the minimum of itself and its two neighbors, so a one-scan spike
        // (noise or a brief interferer) cannot drive the correlation.
        private static double[] RunningMinimum(double[] values)
        {
            var filtered = new double[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                double v = values[i];
                if (i > 0)
                    v = Math.Min(v, values[i - 1]);
                if (i < values.Length - 1)
                    v = Math.Min(v, values[i + 1]);
                filtered[i] = v;
            }
            return filtered;
        }
    }

    /// <summary>ref_corr_rankK: the K-th best correlation of another fragment with the reference fragment.</summary>
    internal sealed class ReferenceKthCorrelationCalc : DetailedOspreyFeatureCalculator
    {
        private readonly int _k;

        public ReferenceKthCorrelationCalc(int k)
        {
            _k = k;
        }

        public override string Name { get { return string.Format(@"ref_corr_rank{0}", _k); } }

        public override string DisplayName
        {
            get { return string.Format(OspreyScoringResources.ReferenceKthCorrelationCalc_DisplayName_Reference_fragment_correlation__rank__0__, _k); }
        }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return ReferenceCorrelations.GetOrCompute(context, peakData).KthBest(_k);
        }
    }

    /// <summary>ref_n_corr_good: fragments correlating with the reference at 0.9 or above.</summary>
    internal sealed class ReferenceGoodCountCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"ref_n_corr_good"; } }

        public override string DisplayName { get { return OspreyScoringResources.ReferenceGoodCountCalc_DisplayName_Fragments_correlating_with_reference; } }

        public override bool IsReversedScore { get { return false; } }   // more is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return ReferenceCorrelations.GetOrCompute(context, peakData).CountGood();
        }
    }

    /// <summary>ref_minfilt_corr_top3_mean: mean of the best three spike-filtered correlations with the reference.</summary>
    internal sealed class ReferenceMinFilteredTop3Calc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"ref_minfilt_corr_top3_mean"; } }

        public override string DisplayName { get { return OspreyScoringResources.ReferenceMinFilteredTop3Calc_DisplayName_Spike_filtered_reference_correlation__top_3_mean_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return ReferenceCorrelations.GetOrCompute(context, peakData).MinFilteredTopMean(3);
        }
    }

    /// <summary>apex_log_intensity_excl_top: log10(1 + apex intensity of all fragments but the most intense).</summary>
    internal sealed class ApexIntensityExcludingTopCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"apex_log_intensity_excl_top"; } }

        public override string DisplayName { get { return OspreyScoringResources.ApexIntensityExcludingTopCalc_DisplayName_Apex_intensity_without_top_fragment; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return ReferenceCorrelations.GetOrCompute(context, peakData).ApexLogIntensityExcludingTop;
        }
    }
}
