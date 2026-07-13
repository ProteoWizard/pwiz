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

using System;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Shared reference-XIC selection for the peak-shape family (peak_apex,
    /// peak_area, peak_sharpness). Computed once per candidate by the first
    /// peak-shape calculator and published to the
    /// <see cref="OspreyScoringContext"/> byproduct cache for its siblings -- the
    /// same arithmetic the inline <c>ComputePeakShapeFeatures</c> performed once.
    ///
    /// NOTE: this is the PEAK-SHAPE selection -- highest total intensity, LAST on
    /// tie (<c>&gt;=</c>), seed <c>-1.0</c>. It is deliberately distinct from the
    /// MS1 reference-XIC selection (seed <c>0.0</c>) and the harness fallback
    /// (<c>&gt;</c>, seed <c>0.0</c>); do not share it across those paths.
    /// <see cref="Valid"/> is false for the degenerate cases (no XICs, empty XIC,
    /// start &gt; end) in which all three features are 0.0.
    /// </summary>
    internal sealed class PeakShapeReference
    {
        public bool Valid;
        public double[] RefIntensities;
        public double[] RefRetentionTimes;
        public int Start;
        public int End;
        public int ApexIndex;
        public double ApexValue;

        public static PeakShapeReference GetOrCompute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out PeakShapeReference reference))
                return reference;
            reference = Compute(peakData);
            context.AddInfo(reference);
            return reference;
        }

        private static PeakShapeReference Compute(IOspreyDetailedPeakData peakData)
        {
            var reference = new PeakShapeReference();

            var xics = peakData.Xics;
            if (xics.Count == 0)
                return reference;

            int len = xics[0].Intensities.Length;
            if (len == 0)
                return reference;

            var peak = peakData.PeakBounds;
            int start = Math.Max(0, peak.StartIndex);
            int end = Math.Min(len - 1, peak.EndIndex);
            int apex = Math.Max(start, Math.Min(end, peak.ApexIndex));

            if (start > end)
                return reference;

            // Reference XIC = highest total intensity, LAST on tie. Rust's
            // xics.iter().max_by(...) returns the last equal element, so use
            // >= with bestTotal seeded to -1.0 (NOT >). Matches Rust
            // pipeline.rs:7140-7148.
            int refIdx = 0;
            double bestTotal = -1.0;
            for (int f = 0; f < xics.Count; f++)
            {
                double total = 0.0;
                double[] inten = xics[f].Intensities;
                for (int i = 0; i < inten.Length; i++)
                    total += inten[i];
                if (total >= bestTotal)
                {
                    bestTotal = total;
                    refIdx = f;
                }
            }

            reference.RefIntensities = xics[refIdx].Intensities;
            reference.RefRetentionTimes = xics[refIdx].RetentionTimes;
            reference.Start = start;
            reference.End = end;
            reference.ApexIndex = apex;
            reference.ApexValue = reference.RefIntensities[apex];
            reference.Valid = true;
            return reference;
        }

        /// <summary>
        /// Conditions a heavy-tailed intensity-magnitude PIN feature:
        /// <c>log10(max(x, 0) + 1)</c>. Shared by the three peak-shape features so the
        /// transform lives in exactly one place on this side, mirroring Rust's
        /// <c>condition_intensity_feature</c> (osprey pipeline.rs) for cross-impl parity.
        ///
        /// Raw peak intensity is heavy-tailed across four orders of magnitude, so the
        /// single experiment-wide Percolator standardizer maps a lone high-intensity DIA
        /// interference to a z-score of 100-300 that dominates the linear discriminant and
        /// lets intensity outliers hijack the top of the ranking. log10 compresses the tail
        /// and is monotonic, so ranking within the feature is preserved. A linear SVM cannot
        /// learn a saturating transform on its own, so it must be applied in the feature.
        /// Matches Skyline mProphet's <c>MQuestIntensityCalc</c>.
        ///
        /// The floor applies to the INPUT, not the result: <c>Math.Log10</c> of a negative
        /// argument is NaN and <c>Math.Max(0, NaN)</c> is NaN, so flooring afterwards would
        /// not save it. Flooring first keeps the argument &gt;= 1, so the value is always
        /// finite and &gt;= 0. Only <see cref="PeakSharpnessCalc"/> can actually go negative
        /// (its apex is the override/CWT lookup, not the recomputed reference-XIC max);
        /// apex and area are &gt;= 0 by construction because XIC intensities are raw --
        /// never smoothed, and never background-subtracted (the background subtraction in
        /// <c>PeakDetector.ComputeSnr</c> works on a scalar and never rewrites the array).
        /// For them the floor is the identity, so the value stays bit-identical to the
        /// un-floored <c>log10(x + 1)</c>; it enforces an invariant that was previously
        /// only assumed, and a NaN in a PIN feature would silently poison the SVM rather
        /// than fail loudly.
        ///
        /// NaN CROSS-IMPL CAVEAT: on a NaN input the two implementations DISAGREE.
        /// <c>Math.Max</c> propagates NaN, so this returns NaN; Rust's <c>f64::max</c>
        /// ignores NaN, so <c>condition_intensity_feature</c> returns 0.0 there. No NaN can
        /// reach either side today (XIC intensities are finite and the slope divisors are
        /// guarded at <c>dt &gt; 1e-10</c>), but if one ever does, fix the source of the
        /// NaN -- do not "harmonize" by dropping the floor.
        /// </summary>
        public static double ConditionIntensityFeature(double value)
        {
            return Math.Log10(Math.Max(value, 0.0) + 1.0);
        }
    }

    /// <summary>
    /// peak_apex: the reference-XIC intensity at the (clamped) peak apex index, log-
    /// conditioned by <see cref="PeakShapeReference.ConditionIntensityFeature"/> (see
    /// there for why the PIN feature is logged). The apex intensity is a direct lookup
    /// of the override/CWT-supplied apex -- NOT a recomputed local max over
    /// [start, end] (Rust pipeline.rs uses peak.apex_intensity).
    /// </summary>
    internal sealed class PeakApexCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "peak_apex"; } }

        // The value is log10-conditioned, so a model weight on it reads per DECADE of
        // intensity, not per intensity unit. The label says so, because the feature-
        // contribution report shows these weights side by side with linear features.
        public override string DisplayName { get { return "Peak apex intensity (log10)"; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            var reference = PeakShapeReference.GetOrCompute(context, peakData);
            return reference.Valid
                ? PeakShapeReference.ConditionIntensityFeature(reference.ApexValue)
                : 0.0;
        }
    }

    /// <summary>
    /// peak_area: trapezoidal integration of the reference XIC over [start, end),
    /// log-conditioned by <see cref="PeakShapeReference.ConditionIntensityFeature"/>.
    /// The raw area is <c>trapezoidal_area(ref_xic[si..=ei])</c>, accumulated strictly
    /// left-to-right (f64 addition is non-associative). This is a PIN feature for the
    /// experiment-wide Percolator model only; it does NOT feed quantification, which
    /// uses <c>bounds_area</c> and stays raw.
    /// </summary>
    internal sealed class PeakAreaCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "peak_area"; } }

        // Log10-conditioned; see PeakApexCalc.DisplayName. Deliberately NOT the same
        // quantity as the raw "Peak area" used for quantification (bounds_area).
        public override string DisplayName { get { return "Peak area (log10)"; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            var reference = PeakShapeReference.GetOrCompute(context, peakData);
            if (!reference.Valid)
                return 0.0;

            double[] refInten = reference.RefIntensities;
            double[] refRts = reference.RefRetentionTimes;
            double area = 0.0;
            for (int i = reference.Start; i < reference.End; i++)
            {
                double dt = refRts[i + 1] - refRts[i];
                double avgHeight = (refInten[i] + refInten[i + 1]) * 0.5;
                area += avgHeight * dt;
            }
            return PeakShapeReference.ConditionIntensityFeature(area);
        }
    }

    /// <summary>
    /// peak_sharpness: the mean of the left and right slopes on the reference XIC,
    /// using the same apex position as peak_apex, log-conditioned by
    /// <see cref="PeakShapeReference.ConditionIntensityFeature"/>. The raw mean slope
    /// follows Rust pipeline.rs (edge guards strict, dt threshold strict &gt; 1e-10).
    ///
    /// Why an intensity conditioning is right for what sounds like a SHAPE feature:
    /// the slopes are computed at linear scale from the raw XIC intensities --
    /// <c>(apexIntensity - edgeIntensity) / dt</c> -- but the RESULT is an ABSOLUTE
    /// slope (intensity per minute), which scales ~linearly with peak intensity: a 2x
    /// more intense peak of the same shape has ~2x steeper slopes. So as defined this
    /// is an intensity-MAGNITUDE feature, not a scale-free shape descriptor. It carries
    /// the same heavy tail as apex/area and demonstrably participated in the same
    /// intensity hijack (a lone high-intensity DIA interference standardized to
    /// z ~= 190 on this feature). The log is monotonic, so a sharper peak still ranks
    /// above a broader one at equal intensity.
    ///
    /// A truly scale-free sharpness would normalize the slope by the apex
    /// (<c>slope / apex</c>) and stay linear. That is a feature redesign, not a
    /// conditioning change: see ai/todos/backlog/TODO-osprey_scale_free_sharpness.md.
    ///
    /// This is the ONE peak-shape feature whose raw value can genuinely be negative:
    /// the apex is the override/CWT-supplied apex (see <see cref="PeakApexCalc"/>), NOT
    /// a recomputed local max over the reference XIC, so a supplied apex sitting below
    /// a reference-XIC edge yields a negative edge slope. Such a peak (apex below its
    /// own edges -- a degenerate shape) collapses to 0 rather than producing a
    /// non-finite feature.
    /// </summary>
    internal sealed class PeakSharpnessCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "peak_sharpness"; } }

        // Log10-conditioned; see PeakApexCalc.DisplayName.
        public override string DisplayName { get { return "Peak sharpness (log10)"; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            var reference = PeakShapeReference.GetOrCompute(context, peakData);
            if (!reference.Valid)
                return 0.0;

            double[] refInten = reference.RefIntensities;
            double[] refRts = reference.RefRetentionTimes;
            int apexIdx = reference.ApexIndex;
            double apexVal = reference.ApexValue;

            double leftSlope = 0.0;
            if (apexIdx > reference.Start)
            {
                double dt = refRts[apexIdx] - refRts[reference.Start];
                if (dt > 1e-10)
                    leftSlope = (apexVal - refInten[reference.Start]) / dt;
            }
            double rightSlope = 0.0;
            if (reference.End > apexIdx)
            {
                double dt = refRts[reference.End] - refRts[apexIdx];
                if (dt > 1e-10)
                    rightSlope = (apexVal - refInten[reference.End]) / dt;
            }
            double sharpness = (leftSlope + rightSlope) * 0.5;
            // This is the one peak-shape feature whose raw value can be negative, so the
            // helper's input floor is load-bearing here rather than a no-op. See
            // PeakShapeReference.ConditionIntensityFeature.
            return PeakShapeReference.ConditionIntensityFeature(sharpness);
        }
    }
}
