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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Evidence that a top-6 fragment channel carries another ion's M+1 isotope: the trace one
    /// 13C spacing below each fragment, at fragment m/z - 1.003355 / z with the fragment tolerance,
    /// re-extracted from the window spectra over the <see cref="TimeCorrelations"/> window. A real
    /// fragment has nothing there; an ion one isotope spacing lower puts its monoisotope there and
    /// its M+1 into the fragment channel, so the two traces co-elute. The weak precursors Osprey
    /// misses often share their isolation window with a co-eluting identification several times
    /// more intense, whose fragments and isotopes land in theirs. Computed once per candidate and
    /// cached on the <see cref="OspreyScoringContext"/>.
    /// </summary>
    internal sealed class ShadowIsotopeCorrelations
    {
        /// <summary>Summed correlation of each top-6 fragment's XIC with the trace one 13C spacing below it.</summary>
        public double CorrSum { get; private set; }

        /// <summary>
        /// Mean over the top-6 fragments with signal of the shadow trace's summed intensity over the
        /// window as a fraction of the fragment's, capped at 1.
        /// </summary>
        public double Fraction { get; private set; }

        public static ShadowIsotopeCorrelations GetOrCompute(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            if (context.TryGetInfo(out ShadowIsotopeCorrelations shadow))
                return shadow;
            shadow = Compute(peakData, TimeCorrelations.GetOrCompute(context, peakData), context.Config?.FragmentTolerance);
            context.AddInfo(shadow);
            return shadow;
        }

        internal static ShadowIsotopeCorrelations Compute(IOspreyApexSpectraPeakData peakData,
            TimeCorrelations timeCorr, FragmentToleranceConfig tolerance)
        {
            var result = new ShadowIsotopeCorrelations();
            if (timeCorr.Elution == null || tolerance == null)
                return result;
            var spectra = ReextractedCorrelations.WindowSpectra(peakData, timeCorr);
            int n = spectra.Length;
            var xics = peakData.Xics;
            var fragments = peakData.Candidate.Fragments;
            var ranked = timeCorr.RankedFragments;
            int[] xicIndices = TopFragments.XicIndices(xics, ranked);
            var shadowTrace = new double[n];
            var fragmentTrace = new double[n];
            double fractionSum = 0;
            int fractionCount = 0;
            for (int r = 0; r < ranked.Length; r++)
            {
                if (xicIndices[r] < 0)
                    continue;
                var fragment = fragments[ranked[r]];
                int charge = Math.Max(1, (int) fragment.Annotation.Charge);
                double shadowMz = fragment.Mz - ReextractedCorrelations.C13_DELTA / charge;
                ReextractedCorrelations.FillTrace(spectra, shadowMz, tolerance.ToleranceDa(shadowMz), shadowTrace);
                Array.Copy(xics[xicIndices[r]].Intensities, timeCorr.WindowStart, fragmentTrace, 0, n);
                double corr = ScoringMath.PearsonCorrelationInRange(shadowTrace, fragmentTrace, 0, n - 1);
                if (!double.IsNaN(corr))
                    result.CorrSum += corr;
                double fragmentTotal = 0, shadowTotal = 0;
                for (int i = 0; i < n; i++)
                {
                    fragmentTotal += fragmentTrace[i];
                    shadowTotal += shadowTrace[i];
                }
                if (fragmentTotal > 0)
                {
                    fractionSum += Math.Min(1.0, shadowTotal / fragmentTotal);
                    fractionCount++;
                }
            }
            result.Fraction = fractionCount > 0 ? fractionSum / fractionCount : 0.0;
            return result;
        }
    }

    /// <summary>
    /// The top-6 <see cref="TimeCorrelations"/> sum with the scans each fragment exceeds its
    /// library-predicted intensity in set aside, as interference: the prediction at a scan is the
    /// fragment's library intensity relative to the reference fragment's, times the smoothed
    /// reference elution, and a fragment more than <see cref="BOUND"/> x that is taken to hold another
    /// ion's signal there (after DIA-NN's removal of interfered fragment points). Only scans where the
    /// elution is at least <see cref="PEAK_FRACTION"/> of its maximum are tested, so the baseline the
    /// peak has to rise out of stays in. Two ways of setting a scan aside: leaving it out of that
    /// fragment's correlation, or capping the fragment at the bound there. Computed once per candidate
    /// and cached on the <see cref="OspreyScoringContext"/>.
    /// </summary>
    internal sealed class CleanedTimeCorrelations
    {
        /// <summary>How many times its predicted intensity a fragment may be before a scan counts as interfered.</summary>
        public const double BOUND = 2.0;

        /// <summary>Fraction of the elution maximum at or above which a scan is tested for interference.</summary>
        public const double PEAK_FRACTION = 0.1;

        /// <summary>Summed top-6 correlation with the interfered scans left out of each fragment's correlation.</summary>
        public double ExcludedSum { get; private set; }

        /// <summary>Summed top-6 correlation with each fragment capped at <see cref="BOUND"/> x its prediction.</summary>
        public double ClippedSum { get; private set; }

        public static CleanedTimeCorrelations GetOrCompute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out CleanedTimeCorrelations cleaned))
                return cleaned;
            cleaned = Compute(peakData, TimeCorrelations.GetOrCompute(context, peakData));
            context.AddInfo(cleaned);
            return cleaned;
        }

        internal static CleanedTimeCorrelations Compute(IOspreyDetailedPeakData peakData, TimeCorrelations timeCorr)
        {
            var result = new CleanedTimeCorrelations();
            var elution = timeCorr.Elution;
            if (elution == null)
                return result;
            var xics = peakData.Xics;
            var fragments = peakData.Candidate.Fragments;
            double referenceIntensity = fragments[xics[timeCorr.ReferenceXicIndex].FragmentIndex].RelativeIntensity;
            if (referenceIntensity <= 0)
                return result;
            int n = elution.Length;
            double gate = 0;
            foreach (double v in elution)
                gate = Math.Max(gate, v);
            gate *= PEAK_FRACTION;

            var ranked = timeCorr.RankedFragments;
            int[] xicIndices = TopFragments.XicIndices(xics, ranked);
            var keptElution = new double[n];
            var keptTrace = new double[n];
            var clipped = new double[n];
            for (int r = 0; r < ranked.Length; r++)
            {
                if (xicIndices[r] < 0)
                    continue;
                var intensities = xics[xicIndices[r]].Intensities;
                double ratio = fragments[ranked[r]].RelativeIntensity / referenceIntensity;
                int kept = 0;
                for (int i = 0; i < n; i++)
                {
                    double value = intensities[timeCorr.WindowStart + i];
                    double limit = BOUND * ratio * elution[i];
                    bool interfered = elution[i] >= gate && value > limit;
                    clipped[i] = interfered ? limit : value;
                    if (!interfered)
                    {
                        keptElution[kept] = elution[i];
                        keptTrace[kept] = value;
                        kept++;
                    }
                }
                result.ExcludedSum += CorrelationOrZero(keptElution, keptTrace, kept);
                result.ClippedSum += CorrelationOrZero(elution, clipped, n);
            }
            return result;
        }

        // Pearson correlation of the first count values, 0 when undefined or under 3 values.
        private static double CorrelationOrZero(double[] x, double[] y, int count)
        {
            if (count < 3)
                return 0.0;
            double corr = ScoringMath.PearsonCorrelationInRange(x, y, 0, count - 1);
            return double.IsNaN(corr) ? 0.0 : corr;
        }
    }

    /// <summary>shadow_corr_sum6: summed correlation of the top six fragments' XICs with the traces one 13C spacing below them.</summary>
    internal sealed class ShadowIsotopeCorrSumCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return @"shadow_corr_sum6"; } }

        public override string DisplayName { get { return OspreyScoringResources.ShadowIsotopeCorrSumCalc_DisplayName_Fragment_M_1_shadow_correlation__top_6_sum_; } }

        public override bool IsReversedScore { get { return true; } }   // lower is better

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return ShadowIsotopeCorrelations.GetOrCompute(context, peakData).CorrSum;
        }
    }

    /// <summary>shadow_frac6: mean share of the top six fragments' window intensity found one 13C spacing below them.</summary>
    internal sealed class ShadowIsotopeFractionCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return @"shadow_frac6"; } }

        public override string DisplayName { get { return OspreyScoringResources.ShadowIsotopeFractionCalc_DisplayName_Fragment_M_1_shadow_intensity_fraction__top_6_mean_; } }

        public override bool IsReversedScore { get { return true; } }   // lower is better

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return ShadowIsotopeCorrelations.GetOrCompute(context, peakData).Fraction;
        }
    }

    /// <summary>tc_clean_sum6 / tc_clip_sum6: the top-6 time-correlation sum with interfered scans left out or capped.</summary>
    internal sealed class CleanedTimeCorrelationSumCalc : DetailedOspreyFeatureCalculator
    {
        private readonly bool _clip;

        public CleanedTimeCorrelationSumCalc(bool clip)
        {
            _clip = clip;
        }

        public override string Name { get { return _clip ? @"tc_clip_sum6" : @"tc_clean_sum6"; } }

        public override string DisplayName
        {
            get
            {
                return _clip
                    ? OspreyScoringResources.CleanedTimeCorrelationSumCalc_DisplayName_Time_correlation_interference_capped__top_6_sum_
                    : OspreyScoringResources.CleanedTimeCorrelationSumCalc_DisplayName_Time_correlation_interference_removed__top_6_sum_;
            }
        }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            var cleaned = CleanedTimeCorrelations.GetOrCompute(context, peakData);
            return _clip ? cleaned.ClippedSum : cleaned.ExcludedSum;
        }
    }
}
