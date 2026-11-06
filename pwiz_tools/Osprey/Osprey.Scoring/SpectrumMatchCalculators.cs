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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// The spectrum-match family: counting and probability scores of the library fragments against one MS2 spectrum,
    /// which hold up in chimeric scans where xcorr's normalization does not. Each library fragment's observed
    /// intensity is the sum of the centroids within the fragment tolerance, and it matches when that is above 0.
    /// <list type="bullet">
    /// <item>MatchProbability: -log10 P(K &gt;= k), the Poisson-binomial tail of k matched fragments, where fragment j
    /// matches by chance with p_j = 1 - exp(-rho_j * w_j): rho_j the spectrum's centroids per Th within
    /// +/- <see cref="DENSITY_HALF_WIDTH"/> Th of the fragment and w_j the full tolerance window in Th</item>
    /// <item>ProjectedCosineScore: EncyclopeDIA's log10(cosine) + log10(k!) + 2, at least 0, from the cosine of the
    /// square-root intensities over all library fragments</item>
    /// <item>Hyperscore: X!Tandem's ln(n_b!) + ln(n_y!) + ln(matched b/y intensity), with the ion types from the
    /// library fragment annotations</item>
    /// </list>
    /// Scored at the apex, and as the maximum over the peak's scans from its start to its end (the scans
    /// <see cref="PeakSpectrumSweep"/> visits; scans outside the scored window are skipped). Computed once per
    /// candidate and cached on the <see cref="OspreyScoringContext"/>.
    /// HRAM only: at unit resolution nearly every library fragment matches something, so the family does not run
    /// (<see cref="GetOrCompute"/> reports 0, and <c>PinFeatureMask</c> keeps it out of scoring and FDR).
    /// </summary>
    internal sealed class SpectrumMatchScores
    {
        /// <summary>The half-width in Th of the m/z range a fragment's local centroid density is counted over.</summary>
        public const double DENSITY_HALF_WIDTH = 50.0;

        /// <summary>The smallest tail probability scored, so the match probability is at most 300.</summary>
        public const double MIN_TAIL_PROBABILITY = 1e-300;

        public double ApexMatchProbability { get; private set; }
        public double ApexProjectedCosineScore { get; private set; }
        public double ApexHyperscore { get; private set; }
        public double PeakMaxMatchProbability { get; private set; }
        public double PeakMaxProjectedCosineScore { get; private set; }

        public static SpectrumMatchScores GetOrCompute(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            if (context.TryGetInfo(out SpectrumMatchScores scores))
                return scores;
            scores = context.Config.ResolutionMode == ResolutionMode.HRAM
                ? Compute(peakData, context.Config.FragmentTolerance)
                : new SpectrumMatchScores();
            context.AddInfo(scores);
            return scores;
        }

        internal static SpectrumMatchScores Compute(IOspreyApexSpectraPeakData peakData, FragmentToleranceConfig tolerance)
        {
            var scores = new SpectrumMatchScores();
            var fragments = peakData.Candidate.Fragments;
            if (fragments == null || fragments.Count == 0)
                return scores;
            var scorer = new ScanScorer(fragments, tolerance);
            if (peakData.ApexSpectrum != null)
            {
                scorer.Score(peakData.ApexSpectrum);
                scores.ApexMatchProbability = scores.PeakMaxMatchProbability = scorer.MatchProbability;
                scores.ApexProjectedCosineScore = scores.PeakMaxProjectedCosineScore = scorer.ProjectedCosineScore;
                scores.ApexHyperscore = scorer.Hyperscore;
            }
            var bounds = peakData.PeakBounds;
            for (int offset = bounds.StartIndex - bounds.ApexIndex; offset <= bounds.EndIndex - bounds.ApexIndex; offset++)
            {
                if (offset == 0 || !peakData.TryGetApexOffsetSpectrum(offset, out var spectrum, out _))
                    continue;
                scorer.Score(spectrum);
                scores.PeakMaxMatchProbability = Math.Max(scores.PeakMaxMatchProbability, scorer.MatchProbability);
                scores.PeakMaxProjectedCosineScore = Math.Max(scores.PeakMaxProjectedCosineScore, scorer.ProjectedCosineScore);
            }
            return scores;
        }

        /// <summary>
        /// The upper tail P(K &gt;= k) of the Poisson-binomial distribution of the number of successes K among
        /// independent trials with success probabilities <paramref name="probabilities"/>; 1 for k &lt;= 0.
        /// </summary>
        internal static double PoissonBinomialTail(IList<double> probabilities, int k)
        {
            if (k <= 0)
                return 1.0;
            int n = probabilities.Count;
            if (k > n)
                return 0.0;
            var distribution = new double[n + 1];
            distribution[0] = 1.0;
            for (int j = 0; j < n; j++)
            {
                double p = probabilities[j];
                for (int i = j + 1; i >= 1; i--)
                    distribution[i] = distribution[i] * (1 - p) + distribution[i - 1] * p;
                distribution[0] *= 1 - p;
            }
            double tail = 0.0;
            for (int i = k; i <= n; i++)
                tail += distribution[i];
            return tail;
        }

        /// <summary>ln(n!) by summation; n is a fragment count, so small.</summary>
        internal static double LogFactorial(int n)
        {
            double sum = 0.0;
            for (int i = 2; i <= n; i++)
                sum += Math.Log(i);
            return sum;
        }

        /// <summary>
        /// Scores one candidate's library fragments against one spectrum at a time, reusing its per-fragment arrays.
        /// </summary>
        private sealed class ScanScorer
        {
            private readonly IReadOnlyList<LibraryFragment> _fragments;
            private readonly FragmentToleranceConfig _tolerance;
            private readonly double[] _probabilities;
            private readonly double _libraryNorm;

            public ScanScorer(IReadOnlyList<LibraryFragment> fragments, FragmentToleranceConfig tolerance)
            {
                _fragments = fragments;
                _tolerance = tolerance;
                _probabilities = new double[fragments.Count];
                double sumSq = 0.0;
                foreach (var fragment in fragments)
                    sumSq += Math.Max(0.0, fragment.RelativeIntensity);
                _libraryNorm = Math.Sqrt(sumSq);
            }

            public double MatchProbability { get; private set; }
            public double ProjectedCosineScore { get; private set; }
            public double Hyperscore { get; private set; }

            public void Score(Spectrum spectrum)
            {
                MatchProbability = ProjectedCosineScore = Hyperscore = 0.0;
                var mzs = spectrum.Mzs;
                var intensities = spectrum.Intensities;
                if (mzs == null || intensities == null || mzs.Length == 0)
                    return;

                int matched = 0, matchedB = 0, matchedY = 0;
                double dot = 0.0, observedSumSq = 0.0, typedIntensity = 0.0;
                for (int j = 0; j < _fragments.Count; j++)
                {
                    var fragment = _fragments[j];
                    double tolDa = _tolerance.ToleranceDa(fragment.Mz);
                    double observed = 0.0;
                    for (int k = spectrum.MzLowerBound(fragment.Mz - tolDa); k < mzs.Length && mzs[k] <= fragment.Mz + tolDa; k++)
                        observed += intensities[k];
                    int localCount = spectrum.MzLowerBound(fragment.Mz + DENSITY_HALF_WIDTH) -
                                     spectrum.MzLowerBound(fragment.Mz - DENSITY_HALF_WIDTH);
                    double density = localCount / (2 * DENSITY_HALF_WIDTH);
                    _probabilities[j] = 1.0 - Math.Exp(-density * 2 * tolDa);
                    if (observed <= 0)
                        continue;

                    matched++;
                    dot += Math.Sqrt(observed) * Math.Sqrt(Math.Max(0.0, fragment.RelativeIntensity));
                    observedSumSq += observed;
                    var ionType = fragment.Annotation.IonType;
                    if (ionType == IonType.B)
                        matchedB++;
                    else if (ionType == IonType.Y)
                        matchedY++;
                    if (ionType == IonType.B || ionType == IonType.Y)
                        typedIntensity += observed;
                }
                if (matched == 0)
                    return;

                double tail = PoissonBinomialTail(_probabilities, matched);
                MatchProbability = -Math.Log10(Math.Max(tail, MIN_TAIL_PROBABILITY));
                if (_libraryNorm > 0 && dot > 0)
                {
                    double cosine = dot / (Math.Sqrt(observedSumSq) * _libraryNorm);
                    ProjectedCosineScore = Math.Max(0.0, Math.Log10(cosine) + LogFactorial(matched) / Math.Log(10) + 2.0);
                }
                if (typedIntensity > 0)
                    Hyperscore = LogFactorial(matchedB) + LogFactorial(matchedY) + Math.Log(typedIntensity);
            }
        }
    }

    /// <summary>spec_match_prob: -log10 of the density-calibrated chance of the apex fragment matches.</summary>
    internal sealed class ApexMatchProbabilityCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return @"spec_match_prob"; } }

        public override string DisplayName { get { return OspreyScoringResources.ApexMatchProbabilityCalc_DisplayName_Fragment_match_probability__apex_; } }

        public override bool IsReversedScore { get { return false; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return SpectrumMatchScores.GetOrCompute(context, peakData).ApexMatchProbability;
        }
    }

    /// <summary>spec_proj_cosine_xt: EncyclopeDIA's projected square-root cosine score at the apex.</summary>
    internal sealed class ApexProjectedCosineScoreCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return @"spec_proj_cosine_xt"; } }

        public override string DisplayName { get { return OspreyScoringResources.ApexProjectedCosineScoreCalc_DisplayName_Projected_cosine_score__apex_; } }

        public override bool IsReversedScore { get { return false; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return SpectrumMatchScores.GetOrCompute(context, peakData).ApexProjectedCosineScore;
        }
    }

    /// <summary>spec_hyperscore: X!Tandem's b/y hyperscore at the apex.</summary>
    internal sealed class ApexHyperscoreCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return @"spec_hyperscore"; } }

        public override string DisplayName { get { return OspreyScoringResources.ApexHyperscoreCalc_DisplayName_Hyperscore__apex_; } }

        public override bool IsReversedScore { get { return false; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return SpectrumMatchScores.GetOrCompute(context, peakData).ApexHyperscore;
        }
    }

    /// <summary>spec_match_prob_max: the fragment match probability at the best scan of the peak.</summary>
    internal sealed class PeakMaxMatchProbabilityCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return @"spec_match_prob_max"; } }

        public override string DisplayName { get { return OspreyScoringResources.PeakMaxMatchProbabilityCalc_DisplayName_Fragment_match_probability__peak_maximum_; } }

        public override bool IsReversedScore { get { return false; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return SpectrumMatchScores.GetOrCompute(context, peakData).PeakMaxMatchProbability;
        }
    }

    /// <summary>spec_proj_cosine_xt_max: the projected cosine score at the best scan of the peak.</summary>
    internal sealed class PeakMaxProjectedCosineScoreCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return @"spec_proj_cosine_xt_max"; } }

        public override string DisplayName { get { return OspreyScoringResources.PeakMaxProjectedCosineScoreCalc_DisplayName_Projected_cosine_score__peak_maximum_; } }

        public override bool IsReversedScore { get { return false; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return SpectrumMatchScores.GetOrCompute(context, peakData).PeakMaxProjectedCosineScore;
        }
    }
}
