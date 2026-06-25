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

using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// ms1_precursor_coelution (feature 13): Pearson correlation between the MS1
    /// precursor-intensity trace and the reference fragment XIC, sampled at the
    /// nearest MS1 scan along the peak. Both chromatograms are produced upstream by
    /// <see cref="PeakDataExtractor"/> (<see cref="IOspreyDetailedPeakData.Ms1PrecursorXic"/>
    /// / <see cref="IOspreyDetailedPeakData.Ms1ReferenceXic"/>), so this calculator
    /// is a pure consumer -- the MS1-specific reference selection, reverse
    /// calibration, and skip-not-zerofill sampling live in the producer. HRAM-only:
    /// for unit-resolution runs (or no MS1) the producer emits no chromatogram and
    /// this is exactly 0.0. The &lt; 3-sample gate and the &lt; 1e-10 Pearson
    /// denominator guard (with feature-side NaN-&gt;0.0) are preserved here.
    /// </summary>
    internal sealed class Ms1PrecursorCoelutionCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "ms1_precursor_coelution"; } }

        public override string DisplayName { get { return "MS1 precursor co-elution"; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            var precursor = peakData.Ms1PrecursorXic;
            var reference = peakData.Ms1ReferenceXic;
            if (precursor == null || reference == null || precursor.Intensities.Length < 3)
                return 0.0;

            double coelution = ScoringMath.PearsonCorrelationInRange(
                precursor.Intensities, reference.Intensities, 0, precursor.Intensities.Length - 1);
            return double.IsNaN(coelution) ? 0.0 : coelution;
        }
    }

    /// <summary>
    /// ms1_isotope_cosine (feature 14): cosine similarity between the observed
    /// isotope envelope at the apex MS1 scan and the theoretical averagine envelope
    /// for the candidate sequence. The observed envelope is produced upstream
    /// (<see cref="IOspreyDetailedPeakData.ApexIsotopeEnvelope"/>); this calculator
    /// applies the M0 gate (Intensities[1] &gt; 0.0) and the PeptideIsotopeCosine
    /// score &gt;= 0.0 acceptance, returning 0.0 otherwise. HRAM-only -- exactly 0.0
    /// for unit-resolution runs or when no apex MS1 scan was found. Uses the
    /// UNMODIFIED Candidate.Sequence.
    /// </summary>
    internal sealed class Ms1IsotopeCosineCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "ms1_isotope_cosine"; } }

        public override string DisplayName { get { return "MS1 isotope dot-product"; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            double[] envelope = peakData.ApexIsotopeEnvelope;
            // Gate: skip if M0 peak is missing (matches Rust envelope.has_m0()).
            // index 1 = M0 (M-1 at 0).
            if (envelope == null || envelope.Length <= 1 || envelope[1] <= 0.0)
                return 0.0;

            // Sequence-based isotope distribution, matching Rust pipeline.rs:5400
            // peptide_isotope_cosine. A composition failure or zero-norm returns a
            // negative score, which stays 0.0.
            double score = IsotopeDistribution.PeptideIsotopeCosine(
                peakData.Candidate.Sequence, envelope);
            return score >= 0.0 ? score : 0.0;
        }
    }
}
