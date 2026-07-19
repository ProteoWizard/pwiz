/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Shared pairwise-fragment coelution statistics for the coelution family
    /// (fragment_coelution_sum / fragment_coelution_max / n_coeluting_fragments).
    /// The three features come from one intertwined single (i&lt;j) pairwise pass, so
    /// -- unlike peak-shape -- the producer computes all three final values and the
    /// calculators just return their field. This is the exact arithmetic the inline
    /// ComputeCoelutionStats performed once. Computed once per candidate, published
    /// to the <see cref="OspreyScoringContext"/> byproduct cache.
    /// </summary>
    internal sealed class CoelutionStats
    {
        public double Sum;
        public double Max;
        /// <summary>Weakest pairwise fragment correlation ("weakest link"), or 0 when no
        /// pair was valid. Non-PIN: read only by <see cref="CoelutionMinCalc"/> under
        /// <c>--extra-features</c>. Computed in the same pass as Sum/Max (free), and its
        /// presence does not perturb them.</summary>
        public double Min;
        public int NCoeluting;

        public static CoelutionStats GetOrCompute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out CoelutionStats stats))
                return stats;
            stats = Compute(peakData);
            context.AddInfo(stats);
            return stats;
        }

        private static CoelutionStats Compute(IOspreyDetailedPeakData peakData)
        {
            var stats = new CoelutionStats();

            var xics = peakData.Xics;
            if (xics.Count < 2)
                return stats;   // Sum = 0, Max = 0, NCoeluting = 0

            // Per-fragment mean pairwise correlation; a fragment is "coeluting" if
            // its mean pairwise correlation is > 0. Matches Rust
            // pipeline.rs:5049-5058. NaN pairs are skipped (not zeroed); max is
            // seeded -Infinity and only adopted when at least one pair was valid.
            var peak = peakData.PeakBounds;
            double[] fragCorrSum = new double[xics.Count];
            int[] fragCorrCount = new int[xics.Count];
            bool haveAny = false;
            double maxCorr = double.NegativeInfinity;
            double minCorr = double.PositiveInfinity;   // non-PIN: CoelutionMinCalc (--extra-features)
            double sum = 0.0;

            for (int i = 0; i < xics.Count; i++)
            {
                for (int j = i + 1; j < xics.Count; j++)
                {
                    double corr = ScoringMath.PearsonCorrelationInRange(
                        xics[i].Intensities, xics[j].Intensities,
                        peak.StartIndex, peak.EndIndex);
                    if (double.IsNaN(corr))
                        continue;

                    sum += corr;
                    if (corr > maxCorr)
                        maxCorr = corr;
                    if (corr < minCorr)
                        minCorr = corr;
                    haveAny = true;

                    fragCorrSum[i] += corr;
                    fragCorrCount[i]++;
                    fragCorrSum[j] += corr;
                    fragCorrCount[j]++;
                }
            }

            stats.Sum = sum;
            if (haveAny)
            {
                stats.Max = maxCorr;
                // Rust pipeline.rs:7539 -- non-finite (no valid pair) reports 0.0.
                stats.Min = minCorr;
            }

            int nCoeluting = 0;
            for (int i = 0; i < xics.Count; i++)
            {
                if (fragCorrCount[i] > 0 && fragCorrSum[i] / fragCorrCount[i] > 0.0)
                    nCoeluting++;
            }
            stats.NCoeluting = nCoeluting;
            return stats;
        }
    }

    /// <summary>fragment_coelution_sum: sum of all valid pairwise fragment correlations.</summary>
    internal sealed class FragmentCoelutionSumCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "fragment_coelution_sum"; } }

        public override string DisplayName { get { return "Fragment co-elution (sum)"; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return CoelutionStats.GetOrCompute(context, peakData).Sum;
        }
    }

    /// <summary>fragment_coelution_max: maximum pairwise fragment correlation (0 if none valid).</summary>
    internal sealed class FragmentCoelutionMaxCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "fragment_coelution_max"; } }

        public override string DisplayName { get { return "Fragment co-elution (max)"; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return CoelutionStats.GetOrCompute(context, peakData).Max;
        }
    }

    /// <summary>n_coeluting_fragments: count of fragments whose mean pairwise correlation is &gt; 0.</summary>
    internal sealed class NCoelutingFragmentsCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "n_coeluting_fragments"; } }

        public override string DisplayName { get { return "Co-eluting fragment count"; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return CoelutionStats.GetOrCompute(context, peakData).NCoeluting;
        }
    }
}
