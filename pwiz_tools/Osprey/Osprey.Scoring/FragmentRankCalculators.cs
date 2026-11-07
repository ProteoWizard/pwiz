/*
 * Original author: Michael J. MacCoss <maccoss .at. uw.edu>,
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
using System.Globalization;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// The candidate's most intense library fragments, by rank, matched in the apex
    /// spectrum: each one's absolute mass error and observed intensity (closest peak by
    /// m/z within tolerance, as <see cref="ApexFragmentMatchSet"/> matches). Computed once
    /// per candidate and shared by the per-rank features, which let the classifier weigh
    /// each fragment rank separately where the apex-match family averages over all of them.
    /// </summary>
    internal sealed class FragmentRankApexMatch
    {
        /// <summary>The library fragment ranks the per-rank features cover.</summary>
        public const int RANK_COUNT = 6;

        /// <summary>Absolute mass error of each rank's match, or NaN when it has none.</summary>
        public readonly double[] AbsMassErrors = new double[RANK_COUNT];

        /// <summary>Observed apex intensity of each rank's match, 0 when it has none.</summary>
        public readonly double[] Intensities = new double[RANK_COUNT];

        /// <summary>The sum of <see cref="Intensities"/>.</summary>
        public double TotalIntensity;

        public static FragmentRankApexMatch GetOrCompute(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData)
        {
            if (context.TryGetInfo(out FragmentRankApexMatch match))
                return match;
            match = Compute(context, peakData);
            context.AddInfo(match);
            return match;
        }

        private static FragmentRankApexMatch Compute(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData)
        {
            var match = new FragmentRankApexMatch();
            for (int r = 0; r < RANK_COUNT; r++)
                match.AbsMassErrors[r] = double.NaN;

            var spectrum = peakData.ApexSpectrum;
            var fragments = peakData.Candidate.Fragments;
            if (spectrum?.Mzs == null || spectrum.Intensities == null || fragments == null || fragments.Count == 0)
                return match;

            var tolerance = context.Config.FragmentTolerance;
            int[] ranked = FragmentMath.TopIndicesByIntensity(fragments, Math.Min(RANK_COUNT, fragments.Count));
            for (int r = 0; r < ranked.Length; r++)
            {
                double fragMz = fragments[ranked[r]].Mz;
                double tolDa = tolerance.ToleranceDa(fragMz);
                int lo = ScoringMath.BinarySearchLowerBound(spectrum.Mzs, fragMz - tolDa);
                double bestError = double.MaxValue;
                int best = -1;
                for (int k = lo; k < spectrum.Mzs.Length && spectrum.Mzs[k] <= fragMz + tolDa; k++)
                {
                    double errorDa = Math.Abs(spectrum.Mzs[k] - fragMz);
                    if (errorDa < bestError)
                    {
                        bestError = errorDa;
                        best = k;
                    }
                }
                if (best < 0)
                    continue;
                match.AbsMassErrors[r] = Math.Abs(tolerance.MassError(fragMz, spectrum.Mzs[best]));
                match.Intensities[r] = spectrum.Intensities[best];
                match.TotalIntensity += spectrum.Intensities[best];
            }
            return match;
        }
    }

    /// <summary>
    /// frag_rankN_abs_mass_error: the absolute mass error, at the apex, of the Nth most
    /// intense library fragment; the calibrated fragment tolerance when it is unmatched (or
    /// the library has fewer fragments), as abs_mass_accuracy_deviation_mean falls back.
    /// </summary>
    internal sealed class FragmentRankMassErrorCalc : ApexSpectrumOspreyFeatureCalculator
    {
        private readonly int _rank;

        public FragmentRankMassErrorCalc(int rank)
        {
            _rank = rank;
        }

        public override string Name
        {
            get { return @"frag_rank" + _rank.ToString(CultureInfo.InvariantCulture) + @"_abs_mass_error"; }
        }

        public override string DisplayName
        {
            get { return string.Format(OspreyScoringResources.FragmentRankMassErrorCalc_DisplayName_Fragment__0__mass_error__abs_, _rank); }
        }

        public override bool IsReversedScore { get { return true; } }   // lower is better

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData)
        {
            double error = FragmentRankApexMatch.GetOrCompute(context, peakData).AbsMassErrors[_rank - 1];
            return double.IsNaN(error) ? context.Config.FragmentTolerance.Tolerance : error;
        }
    }

    /// <summary>
    /// frag_rankN_intensity_share: the Nth most intense library fragment's share of the
    /// observed apex intensity of the top <see cref="FragmentRankApexMatch.RANK_COUNT"/>,
    /// 0 when none of them matched. With the ranks side by side the classifier sees whether
    /// the observed intensities fall in the library's order.
    /// </summary>
    internal sealed class FragmentRankIntensityShareCalc : ApexSpectrumOspreyFeatureCalculator
    {
        private readonly int _rank;

        public FragmentRankIntensityShareCalc(int rank)
        {
            _rank = rank;
        }

        public override string Name
        {
            get { return @"frag_rank" + _rank.ToString(CultureInfo.InvariantCulture) + @"_intensity_share"; }
        }

        public override string DisplayName
        {
            get { return string.Format(OspreyScoringResources.FragmentRankIntensityShareCalc_DisplayName_Fragment__0__intensity_share, _rank); }
        }

        public override bool IsReversedScore { get { return false; } }   // no single expected direction

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData)
        {
            var match = FragmentRankApexMatch.GetOrCompute(context, peakData);
            return match.TotalIntensity > 0.0 ? match.Intensities[_rank - 1] / match.TotalIntensity : 0.0;
        }
    }
}
