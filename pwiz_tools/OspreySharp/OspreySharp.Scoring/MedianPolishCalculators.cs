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

using System.Collections.Generic;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// The Tukey median-polish fit shared by the four median-polish features
    /// (median_polish_cosine / _residual_ratio / _min_fragment_r2 /
    /// _residual_correlation). Unlike the other byproducts this one is PUBLISHED BY
    /// THE HARNESS, not lazily computed by a calculator: the crop +
    /// WriteMpInputsRow + Compute must stay in <c>ScoreCandidate</c> because the
    /// bisection diagnostics (<c>OspreyDiagnostics</c>) live in the exe layer that
    /// <c>OspreySharp.Scoring</c> cannot reference. The harness publishes the fit
    /// (and the cropped inputs the optional WriteMpDump needs) here; the four
    /// calculators read <see cref="Polish"/> and apply their family-specific
    /// default when it is null. Public for that cross-layer publish.
    /// </summary>
    public sealed class MedianPolishByproduct
    {
        /// <summary>The median-polish fit, or null when Compute did not converge.</summary>
        public TukeyMedianPolishResult Polish { get; }

        /// <summary>The peak-cropped per-fragment XIC slices passed to Compute (for WriteMpDump).</summary>
        public IList<KeyValuePair<int, double[]>> PeakXics { get; }

        public MedianPolishByproduct(TukeyMedianPolishResult polish, IList<KeyValuePair<int, double[]>> peakXics)
        {
            Polish = polish;
            PeakXics = peakXics;
        }
    }

    /// <summary>median_polish_cosine: library cosine of the median-polish fit (0 if no fit).</summary>
    internal sealed class MedianPolishCosineCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "median_polish_cosine"; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out MedianPolishByproduct mp) && mp.Polish != null)
                return TukeyMedianPolish.LibCosine(mp.Polish, peakData.Candidate.Fragments);
            return 0.0;
        }
    }

    /// <summary>median_polish_residual_ratio: residual ratio of the fit (1.0 if no fit).</summary>
    internal sealed class MedianPolishResidualRatioCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "median_polish_residual_ratio"; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            // NOTE: the no-fit default is 1.0 (NOT 0.0); a shared "return 0.0"
            // would corrupt this feature.
            if (context.TryGetInfo(out MedianPolishByproduct mp) && mp.Polish != null)
                return TukeyMedianPolish.ResidualRatio(mp.Polish);
            return 1.0;
        }
    }

    /// <summary>median_polish_min_fragment_r2: minimum per-fragment R^2 of the fit (0 if no fit).</summary>
    internal sealed class MedianPolishMinFragmentR2Calc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "median_polish_min_fragment_r2"; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out MedianPolishByproduct mp) && mp.Polish != null)
                return TukeyMedianPolish.MinFragmentR2(mp.Polish);
            return 0.0;
        }
    }

    /// <summary>median_polish_residual_correlation: residual correlation of the fit (0 if no fit).</summary>
    internal sealed class MedianPolishResidualCorrelationCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "median_polish_residual_correlation"; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out MedianPolishByproduct mp) && mp.Polish != null)
                return TukeyMedianPolish.ResidualCorrelation(mp.Polish);
            return 0.0;
        }
    }
}
