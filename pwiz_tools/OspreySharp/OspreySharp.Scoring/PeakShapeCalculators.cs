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

namespace pwiz.OspreySharp.Scoring
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
    }

    /// <summary>
    /// peak_apex: the reference-XIC intensity at the (clamped) peak apex index.
    /// A direct lookup of the override/CWT-supplied apex -- NOT a recomputed local
    /// max over [start, end] (Rust pipeline.rs:6547 uses peak.apex_intensity).
    /// </summary>
    internal sealed class PeakApexCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "peak_apex"; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            var reference = PeakShapeReference.GetOrCompute(context, peakData);
            return reference.Valid ? reference.ApexValue : 0.0;
        }
    }

    /// <summary>
    /// peak_area: trapezoidal integration of the reference XIC over [start, end).
    /// Matches Rust trapezoidal_area(ref_xic[si..=ei]). Accumulated strictly
    /// left-to-right (f64 addition is non-associative).
    /// </summary>
    internal sealed class PeakAreaCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "peak_area"; } }

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
            return area;
        }
    }

    /// <summary>
    /// peak_sharpness: mean of the left and right slopes on the reference XIC,
    /// using the same apex position as peak_apex. Matches Rust
    /// pipeline.rs:5212-5234 (edge guards strict, dt threshold strict &gt; 1e-10).
    /// </summary>
    internal sealed class PeakSharpnessCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "peak_sharpness"; } }

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
            return (leftSlope + rightSlope) * 0.5;
        }
    }
}
