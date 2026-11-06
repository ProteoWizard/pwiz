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
    /// The library agreement family, over the fragments that co-elute: each extracted fragment's Pearson correlation
    /// over the peak bounds with the sum of the other fragments, the fragments above
    /// <see cref="MIN_CORRELATION"/> taken as co-eluting, and the observed intensity summed over apex +/- 1 scan.
    /// <list type="bullet">
    /// <item>CoelutingCosine: cosine of those apex intensities against the library over the co-eluting fragments,
    /// 0 with fewer than <see cref="MIN_FRAGMENTS"/> (alphaDIA lib_cosine_over_50)</item>
    /// <item>EntropySimilarity: spectral entropy similarity (Li et al. 2021) over all extracted fragments
    /// (alphaDIA, Scribe)</item>
    /// <item>CoelutingLibraryShare: the share of the extracted fragments' library intensity that co-elutes
    /// (alphaDIA ion coverage)</item>
    /// </list>
    /// Computed once per candidate and cached on the <see cref="OspreyScoringContext"/>. HRAM only: at unit
    /// resolution nearly every fragment window holds signal, so the family does not run
    /// (<see cref="GetOrCompute"/> reports 0, and <c>PinFeatureMask</c> keeps it out of scoring and FDR).
    /// </summary>
    internal sealed class LibraryAgreement
    {
        public const double MIN_CORRELATION = 0.5;
        public const int MIN_FRAGMENTS = 3;

        public double CoelutingCosine { get; private set; }
        public double EntropySimilarity { get; private set; }
        public double CoelutingLibraryShare { get; private set; }

        public static LibraryAgreement GetOrCompute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out LibraryAgreement agreement))
                return agreement;
            agreement = context.Config.ResolutionMode == ResolutionMode.HRAM
                ? Compute(peakData)
                : new LibraryAgreement();
            context.AddInfo(agreement);
            return agreement;
        }

        internal static LibraryAgreement Compute(IOspreyDetailedPeakData peakData)
        {
            var agreement = new LibraryAgreement();
            var xics = peakData.Xics;
            var fragments = peakData.Candidate.Fragments;
            int n = xics.Count;
            if (n < 2 || fragments == null)
                return agreement;
            var bounds = peakData.PeakBounds;
            int len = xics[0].Intensities.Length;
            int start = Math.Max(0, bounds.StartIndex);
            int end = Math.Min(len - 1, bounds.EndIndex);
            int apex = Math.Max(start, Math.Min(end, bounds.ApexIndex));
            if (end - start + 1 < 3)
                return agreement;

            var total = new double[len];
            foreach (var xic in xics)
            {
                for (int s = start; s <= end; s++)
                    total[s] += xic.Intensities[s];
            }
            var observed = new double[n];
            var library = new double[n];
            var coeluting = new bool[n];
            var rest = new double[len];
            for (int f = 0; f < n; f++)
            {
                var intensities = xics[f].Intensities;
                for (int s = Math.Max(0, apex - 1); s <= Math.Min(len - 1, apex + 1); s++)
                    observed[f] += intensities[s];
                library[f] = Math.Max(0.0, fragments[xics[f].FragmentIndex].RelativeIntensity);
                for (int s = start; s <= end; s++)
                    rest[s] = total[s] - intensities[s];
                double corr = ScoringMath.PearsonCorrelationInRange(intensities, rest, start, end);
                coeluting[f] = !double.IsNaN(corr) && corr > MIN_CORRELATION;
            }

            double dot = 0, obsNorm = 0, libNorm = 0, libAll = 0, libCoeluting = 0;
            int nCoeluting = 0;
            for (int f = 0; f < n; f++)
            {
                libAll += library[f];
                if (!coeluting[f])
                    continue;
                nCoeluting++;
                libCoeluting += library[f];
                dot += observed[f] * library[f];
                obsNorm += observed[f] * observed[f];
                libNorm += library[f] * library[f];
            }
            if (nCoeluting >= MIN_FRAGMENTS && obsNorm > 0 && libNorm > 0)
                agreement.CoelutingCosine = dot / Math.Sqrt(obsNorm * libNorm);
            agreement.CoelutingLibraryShare = libAll > 0 ? libCoeluting / libAll : 0.0;
            agreement.EntropySimilarity = ComputeEntropySimilarity(observed, library);
            return agreement;
        }

        // 1 - (2 H((p + q) / 2) - H(p) - H(q)) / ln 4 over the normalized vectors, 0 when either is empty.
        private static double ComputeEntropySimilarity(double[] p, double[] q)
        {
            double sp = 0, sq = 0;
            for (int i = 0; i < p.Length; i++)
            {
                sp += p[i];
                sq += q[i];
            }
            if (sp <= 0 || sq <= 0)
                return 0.0;
            double hp = 0, hq = 0, hm = 0;
            for (int i = 0; i < p.Length; i++)
            {
                double a = p[i] / sp, b = q[i] / sq, m = (a + b) / 2;
                hp += Entropy(a);
                hq += Entropy(b);
                hm += Entropy(m);
            }
            return 1.0 - (2 * hm - hp - hq) / Math.Log(4.0);
        }

        private static double Entropy(double x)
        {
            return x > 0 ? -x * Math.Log(x) : 0.0;
        }
    }

    /// <summary>lib_cosine_coel: apex +/- 1 cosine against the library over the co-eluting fragments.</summary>
    internal sealed class CoelutingLibraryCosineCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"lib_cosine_coel"; } }

        public override string DisplayName { get { return OspreyScoringResources.CoelutingLibraryCosineCalc_DisplayName_Library_cosine__co_eluting_fragments_; } }

        public override bool IsReversedScore { get { return false; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return LibraryAgreement.GetOrCompute(context, peakData).CoelutingCosine;
        }
    }

    /// <summary>lib_entropy_sim: apex +/- 1 spectral entropy similarity against the library.</summary>
    internal sealed class LibraryEntropySimilarityCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"lib_entropy_sim"; } }

        public override string DisplayName { get { return OspreyScoringResources.LibraryEntropySimilarityCalc_DisplayName_Library_spectral_entropy_similarity; } }

        public override bool IsReversedScore { get { return false; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return LibraryAgreement.GetOrCompute(context, peakData).EntropySimilarity;
        }
    }

    /// <summary>lib_coel_share: the share of the extracted fragments' library intensity that co-elutes.</summary>
    internal sealed class CoelutingLibraryShareCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return @"lib_coel_share"; } }

        public override string DisplayName { get { return OspreyScoringResources.CoelutingLibraryShareCalc_DisplayName_Co_eluting_library_intensity_share; } }

        public override bool IsReversedScore { get { return false; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return LibraryAgreement.GetOrCompute(context, peakData).CoelutingLibraryShare;
        }
    }
}
