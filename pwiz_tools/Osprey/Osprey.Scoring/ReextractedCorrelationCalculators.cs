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
    /// Correlations of extra XICs, re-extracted from the window spectra over the
    /// <see cref="TimeCorrelations"/> window, against its smoothed reference elution, after two
    /// DIA-NN 1.7.12 scores (src/diann.cpp, CC BY 4.0, Demichev et al.):
    /// <list type="bullet">
    /// <item>pHeavy (~7363-7369, ~7646): each top-6 library fragment's M+1 isotope, at fragment
    /// m/z + 1.003355 / z with the fragment tolerance. A real fragment carries its 13C isotope
    /// along with it, an interfering peak that happens to sit at the fragment m/z usually does
    /// not. DIA-NN reads the isotope in the scan that contains the precursor M+1; Osprey reads it
    /// in the same MS2 scans as the fragment XICs, which with Astral's 3 Th windows usually
    /// contain the precursor M+1 too.</item>
    /// <item>pTightCorrOne / pTightCorrTwo (~7630-7638): each top-6 fragment re-extracted at 0.45 x
    /// and 0.2 x the fragment tolerance, so an interferer near the edge of the tolerance window
    /// drops out, and DIA-NN's pTimeCorr rule of keeping, per fragment, the best of the full and
    /// the two tight correlations.</item>
    /// </list>
    /// Peaks are matched as the fragment XICs match them: the closest peak by m/z within the
    /// tolerance (<see cref="TopFragmentExtractor.FindClosestPeakInWindow(Spectrum, double, double, double)"/>).
    /// The cost is 18 lookups per window scan (6 fragments x 3 re-extractions) and 18 Pearson
    /// correlations over the window, computed once per candidate and cached on the
    /// <see cref="OspreyScoringContext"/>.
    /// </summary>
    internal sealed class ReextractedCorrelations
    {
        /// <summary>Mass difference between 13C and 12C, the M+1 isotope spacing at charge 1.</summary>
        public const double C13_DELTA = 1.003355;

        /// <summary>Fraction of the fragment tolerance for the wider tight re-extraction (pTightCorrOne).</summary>
        public const double TIGHT_FRACTION_WIDE = 0.45;

        /// <summary>Fraction of the fragment tolerance for the narrower tight re-extraction (pTightCorrTwo).</summary>
        public const double TIGHT_FRACTION_NARROW = 0.2;

        /// <summary>Correlation at or above which an M+1 isotope trace counts as co-eluting.</summary>
        public const double GOOD_CORRELATION = 0.5;

        /// <summary>Summed correlation of the top-6 fragments' M+1 isotope traces with the reference.</summary>
        public double HeavySum { get; private set; }

        /// <summary>How many of the top-6 fragments' M+1 isotope traces correlate at or above <see cref="GOOD_CORRELATION"/>.</summary>
        public int HeavyGoodCount { get; private set; }

        /// <summary>Summed correlation of the top-6 fragments re-extracted at <see cref="TIGHT_FRACTION_WIDE"/> x tolerance.</summary>
        public double TightWideSum { get; private set; }

        /// <summary>Summed correlation of the top-6 fragments re-extracted at <see cref="TIGHT_FRACTION_NARROW"/> x tolerance.</summary>
        public double TightNarrowSum { get; private set; }

        /// <summary>Sum over the top-6 fragments of the best of the full and the two tight correlations (DIA-NN pTimeCorr).</summary>
        public double BestSum { get; private set; }

        public static ReextractedCorrelations GetOrCompute(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            if (context.TryGetInfo(out ReextractedCorrelations reextracted))
                return reextracted;
            reextracted = Compute(peakData, TimeCorrelations.GetOrCompute(context, peakData),
                context.Config?.FragmentTolerance);
            context.AddInfo(reextracted);
            return reextracted;
        }

        internal static ReextractedCorrelations Compute(IOspreyApexSpectraPeakData peakData,
            TimeCorrelations timeCorr, FragmentToleranceConfig tolerance)
        {
            var result = new ReextractedCorrelations();
            var elution = timeCorr.Elution;
            if (elution == null || tolerance == null)
                return result;

            int n = elution.Length;
            var spectra = WindowSpectra(peakData, timeCorr);
            var fragments = peakData.Candidate.Fragments;
            var ranked = timeCorr.RankedFragments;
            var trace = new double[n];
            for (int r = 0; r < ranked.Length; r++)
            {
                var fragment = fragments[ranked[r]];
                double tolDa = tolerance.ToleranceDa(fragment.Mz);
                double tightWide = Correlate(elution, spectra, fragment.Mz, tolDa * TIGHT_FRACTION_WIDE, trace);
                double tightNarrow = Correlate(elution, spectra, fragment.Mz, tolDa * TIGHT_FRACTION_NARROW, trace);
                int charge = Math.Max(1, (int) fragment.Annotation.Charge);
                double heavyMz = fragment.Mz + C13_DELTA / charge;
                double heavy = Correlate(elution, spectra, heavyMz, tolerance.ToleranceDa(heavyMz), trace);

                result.TightWideSum += tightWide;
                result.TightNarrowSum += tightNarrow;
                result.BestSum += Math.Max(timeCorr.RankCorrelations[r], Math.Max(tightWide, tightNarrow));
                result.HeavySum += heavy;
                if (heavy >= GOOD_CORRELATION)
                    result.HeavyGoodCount++;
            }
            return result;
        }

        /// <summary>
        /// The window spectra of <paramref name="timeCorr"/>, one per window scan, null where a scan is
        /// outside the scored range; null when it has no window.
        /// </summary>
        internal static Spectrum[] WindowSpectra(IOspreyApexSpectraPeakData peakData, TimeCorrelations timeCorr)
        {
            if (timeCorr.Elution == null)
                return null;
            // XIC index i is apex offset i - ApexIndex. The offset is taken from the unclamped ApexIndex because
            // TryGetApexOffsetSpectrum counts from that same index; WindowStart is an absolute XIC index,
            // whichever apex placed it.
            int n = timeCorr.Elution.Length;
            var spectra = new Spectrum[n];
            int firstOffset = timeCorr.WindowStart - peakData.PeakBounds.ApexIndex;
            for (int i = 0; i < n; i++)
            {
                if (peakData.TryGetApexOffsetSpectrum(firstOffset + i, out var spectrum, out _))
                    spectra[i] = spectrum;
            }
            return spectra;
        }

        /// <summary>
        /// Fills <paramref name="trace"/> with the closest peak to <paramref name="mz"/> within +/- <paramref name="tolDa"/>
        /// in each window spectrum, 0 where there is none or the scan is outside the scored range.
        /// </summary>
        internal static void FillTrace(Spectrum[] spectra, double mz, double tolDa, double[] trace)
        {
            for (int i = 0; i < spectra.Length; i++)
            {
                trace[i] = 0.0;
                var spectrum = spectra[i];
                if (spectrum == null)
                    continue;
                int best = TopFragmentExtractor.FindClosestPeakInWindow(spectrum, mz, mz - tolDa, mz + tolDa);
                if (best >= 0)
                    trace[i] = spectrum.Intensities[best];
            }
        }

        // The trace re-extracted at mz within +/- tolDa, correlated with the elution; 0 when undefined.
        private static double Correlate(double[] elution, Spectrum[] spectra, double mz, double tolDa, double[] trace)
        {
            FillTrace(spectra, mz, tolDa, trace);
            double corr = ScoringMath.PearsonCorrelationInRange(elution, trace, 0, trace.Length - 1);
            return double.IsNaN(corr) ? 0.0 : corr;
        }
    }

    /// <summary>heavy_sum6: summed correlation of the top six fragments' M+1 isotope traces with the time-correlation reference.</summary>
    internal sealed class HeavyIsotopeSumCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return @"heavy_sum6"; } }

        public override string DisplayName { get { return OspreyScoringResources.HeavyIsotopeSumCalc_DisplayName_Fragment_M_1_isotope_correlation__top_6_sum_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return ReextractedCorrelations.GetOrCompute(context, peakData).HeavySum;
        }
    }

    /// <summary>heavy_n_good: how many of the top six fragments' M+1 isotope traces correlate at 0.5 or better.</summary>
    internal sealed class HeavyIsotopeGoodCountCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return @"heavy_n_good"; } }

        public override string DisplayName { get { return OspreyScoringResources.HeavyIsotopeGoodCountCalc_DisplayName_Fragment_M_1_isotopes_co_eluting; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return ReextractedCorrelations.GetOrCompute(context, peakData).HeavyGoodCount;
        }
    }

    /// <summary>tight45_sum6 / tight20_sum6: summed top-6 correlation re-extracted at 0.45 x or 0.2 x the fragment tolerance.</summary>
    internal sealed class TightToleranceSumCalc : ApexSpectraOspreyFeatureCalculator
    {
        private readonly bool _narrow;

        public TightToleranceSumCalc(bool narrow)
        {
            _narrow = narrow;
        }

        public override string Name { get { return _narrow ? @"tight20_sum6" : @"tight45_sum6"; } }

        public override string DisplayName
        {
            get
            {
                return _narrow
                    ? OspreyScoringResources.TightToleranceSumCalc_DisplayName_Time_correlation_at_0_2x_tolerance__top_6_sum_
                    : OspreyScoringResources.TightToleranceSumCalc_DisplayName_Time_correlation_at_0_45x_tolerance__top_6_sum_;
            }
        }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            var reextracted = ReextractedCorrelations.GetOrCompute(context, peakData);
            return _narrow ? reextracted.TightNarrowSum : reextracted.TightWideSum;
        }
    }

    /// <summary>tc_best_sum6: sum over the top six fragments of the best of the full, 0.45 x and 0.2 x tolerance correlations.</summary>
    internal sealed class TimeCorrelationBestSumCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return @"tc_best_sum6"; } }

        public override string DisplayName { get { return OspreyScoringResources.TimeCorrelationBestSumCalc_DisplayName_Time_correlation_best_tolerance__top_6_sum_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return ReextractedCorrelations.GetOrCompute(context, peakData).BestSum;
        }
    }
}
