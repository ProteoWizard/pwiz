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
    /// Top-3-fragment versions of the features that need the deep fragments to agree:
    /// ref_n_corr_good, median_polish_min_fragment_r2 / mp_fragment_r2_median and the pairwise
    /// coelution, each restricted to the <see cref="TOP_COUNT"/> most intense LIBRARY fragments
    /// (<see cref="FragmentMath.TopIndicesByIntensity"/>). On a weak true peak fragments 4-12 sit in
    /// the noise, so a count or minimum over all of them goes flat while the top three still
    /// follow one elution profile. A top-3 fragment without an XIC (or a median-polish row) counts
    /// as not following: correlation 0, R^2 0. Computed once per candidate and cached on the
    /// <see cref="OspreyScoringContext"/>.
    /// </summary>
    internal sealed class TopFragments
    {
        /// <summary>The number of most intense library fragments the family looks at.</summary>
        public const int TOP_COUNT = 3;

        /// <summary>
        /// Top-3 fragments, other than the reference fragment of <see cref="ReferenceCorrelations"/>, whose
        /// correlation with it over the peak bounds is at least <see cref="ReferenceCorrelations.GOOD_CORRELATION"/>.
        /// </summary>
        public int RefGoodCount { get; private set; }

        /// <summary>The worst top-3 fragment R^2 against the median-polish profile, 0 without a fit.</summary>
        public double MpMinR2 { get; private set; }

        /// <summary>The median top-3 fragment R^2 against the median-polish profile, 0 without a fit.</summary>
        public double MpMedianR2 { get; private set; }

        /// <summary>The lowest pairwise correlation among the top-3 XICs over the peak bounds, 0 with under two top fragments.</summary>
        public double CoelutionMin { get; private set; }

        public static TopFragments GetOrCompute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out TopFragments top))
                return top;
            context.TryGetInfo(out MedianPolishByproduct mp);
            var pairs = peakData.PeakBounds != null ? PeakPairCorrelations.GetOrCompute(context, peakData) : null;
            top = Compute(peakData, ReferenceCorrelations.GetOrCompute(context, peakData), mp?.Polish, pairs);
            context.AddInfo(top);
            return top;
        }

        internal static TopFragments Compute(IOspreyDetailedPeakData peakData, ReferenceCorrelations refCorr,
            TukeyMedianPolishResult polish)
        {
            return Compute(peakData, refCorr, polish, null);
        }

        // Compute, reading the pairwise correlations of the XICs over the peak bounds from pairs, or
        // computing them when it is null.
        private static TopFragments Compute(IOspreyDetailedPeakData peakData, ReferenceCorrelations refCorr,
            TukeyMedianPolishResult polish, PeakPairCorrelations pairs)
        {
            var result = new TopFragments();
            var xics = peakData.Xics;
            var peak = peakData.PeakBounds;
            var fragments = peakData.Candidate?.Fragments;
            if (peak == null || fragments == null || fragments.Count == 0)
                return result;
            int[] top = FragmentMath.TopIndicesByIntensity(fragments, Math.Min(TOP_COUNT, fragments.Count));
            int[] topXics = XicIndices(xics, top);
            pairs = pairs ?? PeakPairCorrelations.Compute(xics, peak.StartIndex, peak.EndIndex);

            if (xics.Count >= 2)
            {
                foreach (int xi in topXics)
                {
                    if (xi < 0 || xi == refCorr.ReferenceIndex)
                        continue;
                    double corr = pairs.Get(refCorr.ReferenceIndex, xi);
                    if (corr >= ReferenceCorrelations.GOOD_CORRELATION)
                        result.RefGoodCount++;
                }
            }

            if (polish != null && polish.RowEffects.Length >= 1 && polish.ColEffects.Length >= 3)
            {
                var r2s = new double[top.Length];
                for (int i = 0; i < top.Length; i++)
                {
                    int row = Array.IndexOf(polish.FragmentIndices, top[i]);
                    if (row >= 0)
                    {
                        r2s[i] = Math.Max(0.0, TukeyMedianPolish.FragmentR2(polish.Overall, polish.RowEffects[row],
                            polish.ColEffects, polish.Residuals[row]));
                    }
                }
                Array.Sort(r2s); // Array.Sort OK: primitive values only, ties are equal values
                result.MpMinR2 = r2s[0];
                int n = r2s.Length;
                result.MpMedianR2 = n % 2 == 1 ? r2s[n / 2] : (r2s[n / 2 - 1] + r2s[n / 2]) / 2;
            }

            result.CoelutionMin = MinPairCorrelation(pairs, topXics);
            return result;
        }

        /// <summary>
        /// The XIC index of each library fragment in <paramref name="fragmentIndices"/>, -1 for one without an XIC.
        /// </summary>
        internal static int[] XicIndices(IReadOnlyList<XicData> xics, int[] fragmentIndices)
        {
            var result = new int[fragmentIndices.Length];
            for (int i = 0; i < fragmentIndices.Length; i++)
            {
                result[i] = -1;
                for (int xi = 0; xi < xics.Count; xi++)
                {
                    if (xics[xi].FragmentIndex == fragmentIndices[i])
                    {
                        result[i] = xi;
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// The lowest Pearson correlation over [start, end] among every pair of <paramref name="xicIndices"/>, a pair
        /// with a missing XIC (index -1) or too few scans scoring 0; 0 with fewer than two indices.
        /// </summary>
        internal static double MinPairCorrelation(IReadOnlyList<XicData> xics, int[] xicIndices, int start, int end)
        {
            // Only the few pairs asked for: the range can be wider than any peak.
            return MinPairCorrelation(xicIndices,
                (i, j) => ScoringMath.PearsonCorrelationInRange(xics[i].Intensities, xics[j].Intensities, start, end));
        }

        /// <summary>
        /// <see cref="MinPairCorrelation(IReadOnlyList{XicData}, int[], int, int)"/> from the XICs' pairwise
        /// correlations over the range, already computed.
        /// </summary>
        internal static double MinPairCorrelation(PeakPairCorrelations pairs, int[] xicIndices)
        {
            return MinPairCorrelation(xicIndices, pairs.Get);
        }

        private static double MinPairCorrelation(int[] xicIndices, Func<int, int, double> correlation)
        {
            if (xicIndices.Length < 2)
                return 0.0;
            double min = double.MaxValue;
            for (int i = 0; i < xicIndices.Length; i++)
            {
                for (int j = i + 1; j < xicIndices.Length; j++)
                {
                    double corr = 0.0;
                    if (xicIndices[i] >= 0 && xicIndices[j] >= 0)
                    {
                        corr = correlation(xicIndices[i], xicIndices[j]);
                        if (double.IsNaN(corr))
                            corr = 0.0;
                    }
                    min = Math.Min(min, corr);
                }
            }
            return min;
        }
    }

    /// <summary>top3_ref_n_corr_good: top-3 library fragments correlating with the reference fragment at 0.9 or above.</summary>
    internal sealed class TopFragmentRefGoodCountCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"top3_ref_n_corr_good"; } }

        public override string DisplayName { get { return OspreyScoringResources.TopFragmentRefGoodCountCalc_DisplayName_Top_3_fragments_correlating_with_reference; } }

        public override bool IsReversedScore { get { return false; } }   // more is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return TopFragments.GetOrCompute(context, peakData).RefGoodCount;
        }
    }

    /// <summary>top3_mp_min_r2: the worst top-3 library fragment R^2 against the median-polish profile.</summary>
    internal sealed class TopFragmentMpMinR2Calc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"top3_mp_min_r2"; } }

        public override string DisplayName { get { return OspreyScoringResources.TopFragmentMpMinR2Calc_DisplayName_Median_polish_min_R2__top_3_fragments_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return TopFragments.GetOrCompute(context, peakData).MpMinR2;
        }
    }

    /// <summary>top3_mp_r2_median: the median top-3 library fragment R^2 against the median-polish profile.</summary>
    internal sealed class TopFragmentMpMedianR2Calc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"top3_mp_r2_median"; } }

        public override string DisplayName { get { return OspreyScoringResources.TopFragmentMpMedianR2Calc_DisplayName_Median_polish_median_R2__top_3_fragments_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return TopFragments.GetOrCompute(context, peakData).MpMedianR2;
        }
    }

    /// <summary>top3_coelution_min: the lowest pairwise correlation among the top-3 library fragment XICs over the bounds.</summary>
    internal sealed class TopFragmentCoelutionMinCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"top3_coelution_min"; } }

        public override string DisplayName { get { return OspreyScoringResources.TopFragmentCoelutionMinCalc_DisplayName_Fragment_co_elution_min__top_3_fragments_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return TopFragments.GetOrCompute(context, peakData).CoelutionMin;
        }
    }
}
