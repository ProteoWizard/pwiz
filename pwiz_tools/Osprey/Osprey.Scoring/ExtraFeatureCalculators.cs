/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

// EXTRA (non-PIN) scoring features -- --extra-features only, appended at PIN index 21+.
//
// These are scores the Rust engine computes into CoelutionFeatureSet but which the
// 21-feature PIN deliberately excludes, because each one misbehaves with a LINEAR model:
//
//   * coelution_min    -- "weakest link" across fragments. Interacts with the sum/max
//                         already in the PIN rather than adding an independent direction.
//   * peak_symmetry    -- U-SHAPED about 1.0: a symmetric peak (~1.0) is good, and BOTH
//                         tails (fronting << 1, tailing >> 1) are bad. A single weight
//                         cannot express "middle is best"; a tree splits it twice.
//   * mass_accuracy_std-- spread of fragment mass errors. Its discriminating power is in
//                         interaction with the mean the PIN already carries.
//   * hyperscore       -- X!Tandem statistic. Strongly collinear with xcorr /
//                         explained_intensity (which broke the linear model), but
//                         ORTHOGONAL in the tails, which is exactly what trees exploit.
//
// So they are meaningful ONLY with --fdr-method fasttree. See OspreyConfig.ExtraFeatures.
//
// Every value here mirrors the Rust computation cited on each calculator, so a future
// cross-impl comparison has a fixed reference even though these columns are not in the
// PIN parity gate.

using System;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// coelution_min: the WEAKEST valid pairwise fragment correlation -- the
    /// weakest-link counterpart to the sum/max already in the PIN. A precursor whose
    /// fragments all track each other has a high floor; one good pair can carry the max
    /// while a bad fragment drags this down.
    ///
    /// Mirrors Rust pipeline.rs:7539 (<c>corr_min</c>, 0.0 when non-finite). Free: the
    /// pairwise pass that produces the PIN's sum/max already visits every pair.
    /// </summary>
    internal sealed class CoelutionMinCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "fragment_coelution_min"; } }

        public override string DisplayName { get { return "Fragment co-elution (min)"; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            return CoelutionStats.GetOrCompute(context, peakData).Min;
        }
    }

    /// <summary>
    /// peak_symmetry: trapezoidal area LEFT of the apex divided by the area RIGHT of it,
    /// capped at 10. ~1.0 is a symmetric (good) peak; &lt; 1 fronting, &gt; 1 tailing --
    /// both bad. That U shape about 1.0 is precisely why it is not a PIN feature: a
    /// linear weight must choose one direction, while a tree brackets the middle.
    ///
    /// Mirrors Rust pipeline.rs:7442: <c>(left_area / right_area).min(10.0)</c>, or 1.0
    /// when the right area underflows (1e-10) -- i.e. a degenerate peak reports
    /// "symmetric" rather than exploding.
    /// </summary>
    internal sealed class PeakSymmetryCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "peak_symmetry"; } }

        public override string DisplayName { get { return "Peak symmetry (L/R area)"; } }

        // Non-monotone (1.0 is best, both tails are bad), so "higher is better" is
        // ill-defined; false by convention. The contribution table's unexpected-direction
        // flag is not meaningful for this feature -- and it is linear-model-meaningless
        // by construction, which is why it is gated to the tree classifier.
        public override bool IsReversedScore { get { return false; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            var reference = PeakShapeReference.GetOrCompute(context, peakData);
            if (!reference.Valid)
                return 1.0;

            double leftArea = TrapezoidalArea(
                reference.RefRetentionTimes, reference.RefIntensities, reference.Start, reference.ApexIndex);
            double rightArea = TrapezoidalArea(
                reference.RefRetentionTimes, reference.RefIntensities, reference.ApexIndex, reference.End);

            if (rightArea <= 1e-10)
                return 1.0;
            return Math.Min(leftArea / rightArea, 10.0);
        }

        /// <summary>
        /// Trapezoidal area over <c>[from, to]</c> INCLUSIVE. Mirrors Rust
        /// osprey-chromatography/src/lib.rs:53 <c>trapezoidal_area</c>, including its
        /// fewer-than-two-points case: return that single point's intensity (not 0).
        /// </summary>
        private static double TrapezoidalArea(double[] rts, double[] intensities, int from, int to)
        {
            if (to <= from)
                return from >= 0 && from < intensities.Length ? intensities[from] : 0.0;

            double area = 0.0;
            for (int i = from; i < to; i++)
            {
                double dt = rts[i + 1] - rts[i];
                double avgHeight = (intensities[i] + intensities[i + 1]) / 2.0;
                area += avgHeight * dt;
            }
            return area;
        }
    }

    /// <summary>
    /// mass_accuracy_std: population standard deviation of the SIGNED per-fragment mass
    /// errors at the apex spectrum. The PIN already carries their mean and absolute mean;
    /// the spread is a different question -- a systematically shifted-but-tight set of
    /// errors (real peptide, miscalibrated) looks nothing like a scattered set (random
    /// matches) even when the means agree. That is an interaction the linear model cannot
    /// see and the trees can.
    ///
    /// Mirrors Rust <c>compute_mass_accuracy</c> (osprey-scoring/src/lib.rs:465): the
    /// TWO-PASS population variance (divide by n, not n-1) over the signed errors, and
    /// the empty-match fallback of the configured tolerance.
    /// </summary>
    internal sealed class MassAccuracyStdCalc : ApexSpectrumOspreyFeatureCalculator
    {
        public override string Name { get { return "mass_accuracy_std"; } }

        public override string DisplayName { get { return "Mass error (std dev)"; } }

        public override bool IsReversedScore { get { return true; } }   // tighter is better

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData)
        {
            var matchSet = ApexFragmentMatchSet.GetOrCompute(context, peakData);
            var errors = matchSet.MassErrors;
            if (errors == null || errors.Count == 0)
            {
                // Rust returns (0.0, tolerance, tolerance) for no matches -- the std slot
                // gets the tolerance, same as the abs-mean slot.
                return context.Config.FragmentTolerance.Tolerance;
            }

            double n = errors.Count;
            double meanSigned = matchSet.MassErrSum / n;
            double variance = 0.0;
            for (int i = 0; i < errors.Count; i++)
            {
                double d = errors[i] - meanSigned;
                variance += d * d;
            }
            variance /= n;
            return Math.Sqrt(variance);
        }
    }

    /// <summary>
    /// hyperscore: the X!Tandem score, <c>ln(n_b!) + ln(n_y!) + SUM ln(I+1)</c> over the
    /// matched fragments at the apex spectrum. It rewards matching MANY b/y ions (the
    /// factorial terms grow fast) AND matching intense ones -- a combination that is
    /// orthogonal to xcorr / cosine in the tails even though it is strongly collinear
    /// with them in the bulk. That collinearity is what made it useless to the linear
    /// SVM; the trees can use the tail behavior.
    ///
    /// Mirrors Rust <c>compute_hyperscore</c> (osprey-scoring/src/lib.rs:1463), including
    /// 0.0 when nothing matched, and counting only b/y ions toward the factorial terms
    /// (other ion types still contribute intensity).
    /// </summary>
    internal sealed class HyperscoreCalc : ApexSpectrumOspreyFeatureCalculator
    {
        public override string Name { get { return "hyperscore"; } }

        public override string DisplayName { get { return "Hyperscore (X!Tandem)"; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData)
        {
            var matchSet = ApexFragmentMatchSet.GetOrCompute(context, peakData);
            if (matchSet.NMatched <= 0)
                return 0.0;

            // ln(k!) == LogGamma(k + 1); use the gamma form (as Rust does) so large ion
            // counts cannot overflow a factorial.
            return ScoringMath.LogGamma(matchSet.NB + 1.0) +
                   ScoringMath.LogGamma(matchSet.NY + 1.0) +
                   matchSet.SumLogMatchedIntensity;
        }
    }
}
