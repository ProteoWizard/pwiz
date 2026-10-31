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
    /// The bounds-computed coelution features over a span no narrower than apex +/-
    /// <see cref="HALF_WINDOW_MINUTES"/>: max(bounds, apex +/- 0.08 min), clipped to the XICs, with the
    /// minutes turned into scans at the scan spacing around the apex (<see cref="TimeCorrelations.HalfWindowScans"/>).
    /// On weak peaks Osprey's bounds are about 30% narrower than DIA-NN's (7.2 s vs 10.2 s median on SEA-AD
    /// Astral), and a correlation over a handful of scans near the top of a peak sees little of its rise and fall;
    /// apex +/- 0.08 min is about DIA-NN's width. Wider bounds are kept as they are. The sum and the coeluting
    /// count mirror <see cref="CoelutionStats"/> (fragment_coelution_sum, n_coeluting_fragments), the margin mirrors
    /// peak_coelution_margin with every candidate peak widened the same way around its own apex, and the top-3
    /// minimum is <see cref="TopFragments.CoelutionMin"/> over the widened span. Computed once per candidate and
    /// cached on the <see cref="OspreyScoringContext"/>.
    /// </summary>
    internal sealed class WideSpanCoelution
    {
        /// <summary>The least half-width of the span around the apex, in minutes.</summary>
        public const double HALF_WINDOW_MINUTES = 0.08;

        /// <summary>First XIC scan index of the chosen peak's widened span.</summary>
        public int SpanStart { get; private set; }

        /// <summary>Last XIC scan index of the chosen peak's widened span.</summary>
        public int SpanEnd { get; private set; }

        /// <summary>Sum of every valid pairwise fragment correlation over the span (fragment_coelution_sum).</summary>
        public double Sum { get; private set; }

        /// <summary>Fragments whose mean pairwise correlation over the span is above 0 (n_coeluting_fragments).</summary>
        public int NCoeluting { get; private set; }

        /// <summary>
        /// The chosen peak's mean pairwise correlation over its span minus the best other candidate's over its own,
        /// the other floored at 0 (peak_coelution_margin); 0 without the candidate peaks.
        /// </summary>
        public double Margin { get; private set; }

        /// <summary>The lowest pairwise correlation among the top-3 library fragment XICs over the span.</summary>
        public double Top3Min { get; private set; }

        public static WideSpanCoelution GetOrCompute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out WideSpanCoelution wide))
                return wide;
            context.TryGetInfo(out PeakCandidatesByproduct candidates);
            wide = Compute(peakData, candidates, HALF_WINDOW_MINUTES);
            context.AddInfo(wide);
            return wide;
        }

        internal static WideSpanCoelution Compute(IOspreyDetailedPeakData peakData, PeakCandidatesByproduct candidates,
            double halfWindowMinutes)
        {
            var result = new WideSpanCoelution();
            var xics = peakData.Xics;
            var peak = peakData.PeakBounds;
            if (xics.Count < 2 || peak == null)
                return result;
            Span(xics, peak, halfWindowMinutes, out int start, out int end);
            result.SpanStart = start;
            result.SpanEnd = end;

            var fragCorrSum = new double[xics.Count];
            var fragCorrCount = new int[xics.Count];
            int pairCount = 0;
            for (int i = 0; i < xics.Count; i++)
            {
                for (int j = i + 1; j < xics.Count; j++)
                {
                    double corr = ScoringMath.PearsonCorrelationInRange(xics[i].Intensities, xics[j].Intensities, start, end);
                    if (double.IsNaN(corr))
                        continue;
                    result.Sum += corr;
                    pairCount++;
                    fragCorrSum[i] += corr;
                    fragCorrCount[i]++;
                    fragCorrSum[j] += corr;
                    fragCorrCount[j]++;
                }
            }
            for (int i = 0; i < xics.Count; i++)
            {
                if (fragCorrCount[i] > 0 && fragCorrSum[i] / fragCorrCount[i] > 0.0)
                    result.NCoeluting++;
            }

            if (candidates != null)
                result.Margin = MarginOverRunnerUp(xics, candidates, pairCount > 0 ? result.Sum / pairCount : 0.0, halfWindowMinutes);

            var fragments = peakData.Candidate?.Fragments;
            if (fragments != null && fragments.Count > 0)
            {
                int[] top = FragmentMath.TopIndicesByIntensity(fragments, Math.Min(TopFragments.TOP_COUNT, fragments.Count));
                result.Top3Min = TopFragments.MinPairCorrelation(xics, TopFragments.XicIndices(xics, top), start, end);
            }
            return result;
        }

        // max(bounds, apex +/- halfWindowMinutes) in XIC scans, clipped to the XICs.
        private static void Span(IReadOnlyList<XicData> xics, XICPeakBounds peak, double halfWindowMinutes,
            out int start, out int end)
        {
            int n = xics[0].Intensities.Length;
            int apex = Math.Max(0, Math.Min(n - 1, peak.ApexIndex));
            int halfScans = TimeCorrelations.HalfWindowScans(xics[0].RetentionTimes, apex, halfWindowMinutes);
            start = Math.Max(0, Math.Min(peak.StartIndex, apex - halfScans));
            end = Math.Min(n - 1, Math.Max(peak.EndIndex, apex + halfScans));
        }

        // The chosen mean pairwise correlation minus the best other candidate's over its own widened span, the
        // other floored at 0; 0 when the chosen peak is not among the candidates (as peak_coelution_margin).
        private static double MarginOverRunnerUp(IReadOnlyList<XicData> xics, PeakCandidatesByproduct candidates,
            double chosenMean, double halfWindowMinutes)
        {
            var chosen = candidates.ChosenPeak;
            bool found = false;
            double other = 0.0;
            foreach (var candidate in candidates.Candidates)
            {
                // By bounds, as PeakCandidatesByproduct.TryGetChosenAndRunnerUp.
                if (!found && candidate.peak.StartIndex == chosen.StartIndex && candidate.peak.EndIndex == chosen.EndIndex)
                {
                    found = true;
                    continue;
                }
                Span(xics, candidate.peak, halfWindowMinutes, out int start, out int end);
                other = Math.Max(other, MeanPairCorrelation(xics, start, end));
            }
            return found ? chosenMean - other : 0.0;
        }

        // Mean of the valid pairwise correlations over [start, end], 0 when none (the pick's coelution score).
        private static double MeanPairCorrelation(IReadOnlyList<XicData> xics, int start, int end)
        {
            double sum = 0.0;
            int count = 0;
            for (int i = 0; i < xics.Count; i++)
            {
                for (int j = i + 1; j < xics.Count; j++)
                {
                    double corr = ScoringMath.PearsonCorrelationInRange(xics[i].Intensities, xics[j].Intensities, start, end);
                    if (double.IsNaN(corr))
                        continue;
                    sum += corr;
                    count++;
                }
            }
            return count > 0 ? sum / count : 0.0;
        }
    }

    /// <summary>wide_coelution_sum: fragment_coelution_sum over max(bounds, apex +/- 0.08 min).</summary>
    internal sealed class WideCoelutionSumCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"wide_coelution_sum"; } }

        public override string DisplayName { get { return OspreyScoringResources.WideCoelutionSumCalc_DisplayName_Fragment_co_elution__sum__widened_span_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return WideSpanCoelution.GetOrCompute(context, peakData).Sum;
        }
    }

    /// <summary>wide_n_coeluting: n_coeluting_fragments over max(bounds, apex +/- 0.08 min).</summary>
    internal sealed class WideCoelutingCountCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"wide_n_coeluting"; } }

        public override string DisplayName { get { return OspreyScoringResources.WideCoelutingCountCalc_DisplayName_Co_eluting_fragment_count__widened_span_; } }

        public override bool IsReversedScore { get { return false; } }   // more is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return WideSpanCoelution.GetOrCompute(context, peakData).NCoeluting;
        }
    }

    /// <summary>wide_coelution_margin: peak_coelution_margin with every candidate peak widened around its apex.</summary>
    internal sealed class WideCoelutionMarginCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"wide_coelution_margin"; } }

        public override string DisplayName { get { return OspreyScoringResources.WideCoelutionMarginCalc_DisplayName_Coelution_margin_over_runner_up_peak__widened_span_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return WideSpanCoelution.GetOrCompute(context, peakData).Margin;
        }
    }

    /// <summary>wide_top3_coelution_min: top3_coelution_min over max(bounds, apex +/- 0.08 min).</summary>
    internal sealed class WideTop3CoelutionMinCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"wide_top3_coelution_min"; } }

        public override string DisplayName { get { return OspreyScoringResources.WideTop3CoelutionMinCalc_DisplayName_Fragment_co_elution_min__top_3_fragments__widened_span_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return WideSpanCoelution.GetOrCompute(context, peakData).Top3Min;
        }
    }
}
