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

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Each fragment's R^2 against the median-polish elution profile (the robust consensus
    /// trace, as in EncyclopeDIA's normalized median chromatogram), sorted best first.
    /// median_polish_min_fragment_r2 keeps only the worst of these, so one interfered fragment
    /// sets it; the top-k features keep the best k instead, which is what a DIA peak needs to
    /// be believed: a handful of fragments that follow one elution profile cleanly, whatever
    /// the rest are doing. Computed once per candidate from the harness-published
    /// <see cref="MedianPolishByproduct"/> and cached on the <see cref="OspreyScoringContext"/>.
    /// </summary>
    internal sealed class MedianPolishFragmentFits
    {
        /// <summary>R^2 at or above which a fragment counts as following the profile.</summary>
        public const double GOOD_FIT_R2 = 0.8;

        /// <summary>Per-fragment R^2 against the fit, highest first; empty when there is no fit.</summary>
        public double[] SortedR2 { get; private set; }

        public static MedianPolishFragmentFits GetOrCompute(OspreyScoringContext context)
        {
            if (context.TryGetInfo(out MedianPolishFragmentFits fits))
                return fits;
            fits = new MedianPolishFragmentFits { SortedR2 = ComputeSortedR2(context) };
            context.AddInfo(fits);
            return fits;
        }

        /// <summary>The k-th best R^2 (1-based), or 0 when fewer than k fragments were fit.</summary>
        public double KthBest(int k)
        {
            return k <= SortedR2.Length ? SortedR2[k - 1] : 0.0;
        }

        /// <summary>Mean of the best k R^2 values, counting missing fragments as 0.</summary>
        public double TopMean(int k)
        {
            double sum = 0;
            for (int i = 0; i < k && i < SortedR2.Length; i++)
                sum += SortedR2[i];
            return sum / k;
        }

        /// <summary>Number of fragments whose R^2 is at least <see cref="GOOD_FIT_R2"/>.</summary>
        public int CountGood()
        {
            int count = 0;
            foreach (double r2 in SortedR2)
            {
                if (r2 >= GOOD_FIT_R2)
                    count++;
            }
            return count;
        }

        /// <summary>Median R^2 over the fit fragments, or 0 when there is no fit.</summary>
        public double Median()
        {
            int n = SortedR2.Length;
            if (n == 0)
                return 0.0;
            return n % 2 == 1 ? SortedR2[n / 2] : (SortedR2[n / 2 - 1] + SortedR2[n / 2]) / 2;
        }

        private static double[] ComputeSortedR2(OspreyScoringContext context)
        {
            if (!context.TryGetInfo(out MedianPolishByproduct mp) || mp.Polish == null)
                return Array.Empty<double>();
            var polish = mp.Polish;
            int nFrags = polish.RowEffects.Length;
            if (nFrags < 1 || polish.ColEffects.Length < 3)
                return Array.Empty<double>();
            var r2s = new double[nFrags];
            for (int f = 0; f < nFrags; f++)
            {
                r2s[f] = Math.Max(0.0, TukeyMedianPolish.FragmentR2(polish.Overall, polish.RowEffects[f],
                    polish.ColEffects, polish.Residuals[f]));
            }
            Array.Sort(r2s); // Array.Sort OK: primitive values only, ties are equal values
            Array.Reverse(r2s);
            return r2s;
        }
    }

    /// <summary>mp_fragment_r2_kth: the k-th best fragment R^2 against the median-polish profile.</summary>
    internal sealed class MedianPolishKthFragmentR2Calc : DetailedOspreyFeatureCalculator
    {
        private readonly int _k;

        public MedianPolishKthFragmentR2Calc(int k)
        {
            _k = k;
        }

        public override string Name { get { return string.Format(@"mp_fragment_r2_rank{0}", _k); } }

        public override string DisplayName
        {
            get { return string.Format(OspreyScoringResources.MedianPolishKthFragmentR2Calc_DisplayName_Median_polish_fragment_R2__rank__0__, _k); }
        }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return MedianPolishFragmentFits.GetOrCompute(context).KthBest(_k);
        }
    }

    /// <summary>mp_fragment_r2_top3_mean: mean of the three best fragment R^2 values.</summary>
    internal sealed class MedianPolishTop3MeanR2Calc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"mp_fragment_r2_top3_mean"; } }

        public override string DisplayName { get { return OspreyScoringResources.MedianPolishTop3MeanR2Calc_DisplayName_Median_polish_fragment_R2__top_3_mean_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return MedianPolishFragmentFits.GetOrCompute(context).TopMean(3);
        }
    }

    /// <summary>mp_fragment_r2_median: median fragment R^2 against the median-polish profile.</summary>
    internal sealed class MedianPolishMedianR2Calc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"mp_fragment_r2_median"; } }

        public override string DisplayName { get { return OspreyScoringResources.MedianPolishMedianR2Calc_DisplayName_Median_polish_fragment_R2__median_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return MedianPolishFragmentFits.GetOrCompute(context).Median();
        }
    }

    /// <summary>mp_n_fragments_good_fit: fragments whose R^2 against the profile is at least 0.8.</summary>
    internal sealed class MedianPolishGoodFitCountCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"mp_n_fragments_good_fit"; } }

        public override string DisplayName { get { return OspreyScoringResources.MedianPolishGoodFitCountCalc_DisplayName_Median_polish_fragments_with_good_fit; } }

        public override bool IsReversedScore { get { return false; } }   // more is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return MedianPolishFragmentFits.GetOrCompute(context).CountGood();
        }
    }
}
