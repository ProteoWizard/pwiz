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
    /// Scale-free descriptions of the chosen peak, after the scoring of DIA-NN 1.7.12
    /// (src/diann.cpp, CC BY 4.0, Demichev et al.): its width, its elution shape as five
    /// intensity fractions across the peak (pShape), the fragments' correlation with the
    /// reference near the apex only (pLocCorr), the library's relative intensities of the top
    /// fragments (pRef, the same for a target and its decoy, so a nonlinear model can read the
    /// observed shares against them), and those fragments' shares of the intensity summed
    /// across the peak (pSig) rather than at the apex. Computed once per candidate and cached
    /// on the <see cref="OspreyScoringContext"/>.
    /// </summary>
    internal sealed class PeakProfile
    {
        /// <summary>Number of elution-shape bins.</summary>
        public const int SHAPE_BINS = 5;

        /// <summary>Library fragment ranks covered by the relative-intensity and share features.</summary>
        public const int RANK_COUNT = 6;

        /// <summary>Scans on each side of the apex in the apex-local correlation.</summary>
        public const int LOCAL_HALF_WIDTH = 2;

        /// <summary>Peak end minus start retention time, in minutes.</summary>
        public double Width { get; private set; }

        /// <summary>Fraction of the summed fragment intensity in each fifth of the peak, by scan.</summary>
        public double[] Shape { get; private set; }

        /// <summary>Mean of the best three correlations with the reference over apex +/- 2 scans.</summary>
        public double LocalCorrelationTop3Mean { get; private set; }

        /// <summary>Library intensity of each rank relative to rank 1 (index 0 is rank 1, always 1).</summary>
        public double[] LibraryRelative { get; private set; }

        /// <summary>Each rank's share of the top ranks' intensity summed across the peak, 0 without an XIC.</summary>
        public double[] WindowShares { get; private set; }

        public static PeakProfile GetOrCompute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out PeakProfile profile))
                return profile;
            profile = Compute(context, peakData);
            context.AddInfo(profile);
            return profile;
        }

        private static PeakProfile Compute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            var profile = new PeakProfile
            {
                Shape = new double[SHAPE_BINS],
                LibraryRelative = new double[RANK_COUNT],
                WindowShares = new double[RANK_COUNT],
            };
            var peak = peakData.PeakBounds;
            if (peak != null)
                profile.Width = Math.Max(0.0, peak.EndRt - peak.StartRt);
            var fragments = peakData.Candidate?.Fragments;
            if (fragments != null && fragments.Count > 0)
            {
                int[] ranked = FragmentMath.TopIndicesByIntensity(fragments, Math.Min(RANK_COUNT, fragments.Count));
                double top = fragments[ranked[0]].RelativeIntensity;
                for (int r = 0; r < ranked.Length; r++)
                    profile.LibraryRelative[r] = top > 0 ? fragments[ranked[r]].RelativeIntensity / top : 0.0;
                if (peak != null)
                    FillWindowShares(profile.WindowShares, ranked, peakData.Xics, peak);
            }
            var xics = peakData.Xics;
            if (peak == null || xics.Count == 0)
                return profile;
            FillShape(profile.Shape, xics, peak);
            if (xics.Count >= 2)
            {
                int reference = ReferenceCorrelations.GetOrCompute(context, peakData).ReferenceIndex;
                profile.LocalCorrelationTop3Mean = LocalTop3Mean(xics, reference, peak);
            }
            return profile;
        }

        private static void FillShape(double[] shape, IReadOnlyList<XicData> xics, XICPeakBounds peak)
        {
            int start = Math.Max(0, peak.StartIndex);
            int end = Math.Min(xics[0].Intensities.Length - 1, peak.EndIndex);
            int n = end - start + 1;
            if (n <= 0)
                return;
            double total = 0;
            for (int i = start; i <= end; i++)
            {
                double sum = 0;
                foreach (var xic in xics)
                    sum += xic.Intensities[i];
                int bin = Math.Min(SHAPE_BINS - 1, (i - start) * SHAPE_BINS / n);
                shape[bin] += sum;
                total += sum;
            }
            if (total <= 0)
                return;
            for (int b = 0; b < SHAPE_BINS; b++)
                shape[b] /= total;
        }

        private static double LocalTop3Mean(IReadOnlyList<XicData> xics, int reference, XICPeakBounds peak)
        {
            int last = xics[reference].Intensities.Length - 1;
            int start = Math.Max(Math.Max(0, peak.StartIndex), peak.ApexIndex - LOCAL_HALF_WIDTH);
            int end = Math.Min(Math.Min(last, peak.EndIndex), peak.ApexIndex + LOCAL_HALF_WIDTH);
            if (end - start < 2)
                return 0.0;
            var corrs = new List<double>();
            for (int i = 0; i < xics.Count; i++)
            {
                if (i == reference)
                    continue;
                double corr = ScoringMath.PearsonCorrelationInRange(
                    xics[reference].Intensities, xics[i].Intensities, start, end);
                corrs.Add(double.IsNaN(corr) ? 0.0 : corr);
            }
            var sorted = corrs.ToArray();
            Array.Sort(sorted); // Array.Sort OK: primitive values only, ties are equal values
            Array.Reverse(sorted);
            double sum = 0;
            for (int i = 0; i < 3 && i < sorted.Length; i++)
                sum += sorted[i];
            return sum / 3;
        }

        private static void FillWindowShares(double[] shares, int[] ranked, IReadOnlyList<XicData> xics, XICPeakBounds peak)
        {
            double total = 0;
            for (int r = 0; r < ranked.Length; r++)
            {
                foreach (var xic in xics)
                {
                    if (xic.FragmentIndex != ranked[r])
                        continue;
                    int start = Math.Max(0, peak.StartIndex);
                    int end = Math.Min(xic.Intensities.Length - 1, peak.EndIndex);
                    for (int i = start; i <= end; i++)
                        shares[r] += xic.Intensities[i];
                    break;
                }
                total += shares[r];
            }
            if (total <= 0)
                return;
            for (int r = 0; r < ranked.Length; r++)
                shares[r] /= total;
        }
    }

    /// <summary>peak_width: peak end minus start retention time, in minutes.</summary>
    internal sealed class PeakWidthCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"peak_width"; } }

        public override string DisplayName { get { return OspreyScoringResources.PeakWidthCalc_DisplayName_Peak_width; } }

        public override bool IsReversedScore { get { return false; } }   // wider is more target-like here

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return PeakProfile.GetOrCompute(context, peakData).Width;
        }
    }

    /// <summary>shape_binK: fraction of the summed fragment intensity in the K-th fifth of the peak.</summary>
    internal sealed class ElutionShapeBinCalc : DetailedOspreyFeatureCalculator
    {
        private readonly int _bin;

        public ElutionShapeBinCalc(int bin)
        {
            _bin = bin;
        }

        public override string Name { get { return string.Format(@"shape_bin{0}", _bin); } }

        public override string DisplayName
        {
            get { return string.Format(OspreyScoringResources.ElutionShapeBinCalc_DisplayName_Elution_shape__bin__0__of_5_, _bin); }
        }

        public override bool IsReversedScore { get { return false; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return PeakProfile.GetOrCompute(context, peakData).Shape[_bin - 1];
        }
    }

    /// <summary>local_corr_top3_mean: mean of the best three correlations with the reference over apex +/- 2 scans.</summary>
    internal sealed class LocalCorrelationTop3Calc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"local_corr_top3_mean"; } }

        public override string DisplayName { get { return OspreyScoringResources.LocalCorrelationTop3Calc_DisplayName_Apex_local_reference_correlation__top_3_mean_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return PeakProfile.GetOrCompute(context, peakData).LocalCorrelationTop3Mean;
        }
    }

    /// <summary>lib_rel_rankK: the library intensity of fragment rank K relative to rank 1.</summary>
    internal sealed class LibraryRelativeIntensityCalc : DetailedOspreyFeatureCalculator
    {
        private readonly int _rank;

        public LibraryRelativeIntensityCalc(int rank)
        {
            _rank = rank;
        }

        public override string Name { get { return string.Format(@"lib_rel_rank{0}", _rank); } }

        public override string DisplayName
        {
            get { return string.Format(OspreyScoringResources.LibraryRelativeIntensityCalc_DisplayName_Library_intensity_of_fragment__0__relative_to_fragment_1, _rank); }
        }

        public override bool IsReversedScore { get { return false; } }   // context, not evidence

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return PeakProfile.GetOrCompute(context, peakData).LibraryRelative[_rank - 1];
        }
    }

    /// <summary>window_share_rankK: fragment rank K's share of the top ranks' intensity summed across the peak.</summary>
    internal sealed class WindowShareCalc : DetailedOspreyFeatureCalculator
    {
        private readonly int _rank;

        public WindowShareCalc(int rank)
        {
            _rank = rank;
        }

        public override string Name { get { return string.Format(@"window_share_rank{0}", _rank); } }

        public override string DisplayName
        {
            get { return string.Format(OspreyScoringResources.WindowShareCalc_DisplayName_Fragment__0__share_across_peak, _rank); }
        }

        public override bool IsReversedScore { get { return false; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return PeakProfile.GetOrCompute(context, peakData).WindowShares[_rank - 1];
        }
    }
}
