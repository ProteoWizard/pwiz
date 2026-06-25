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

using System;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// One scoring feature, mirroring Skyline's
    /// <c>IPeakFeatureCalculator</c>. Each calculator is a stateless singleton
    /// that computes a single PIN feature value from a candidate's peak data and
    /// the shared scoring context. The parity-critical order and identity of the
    /// 21 features lives in <see cref="OspreyFeatureCalculators"/>, not on the
    /// calculator.
    ///
    /// The SPI is typed to the least-specific peak-data tier
    /// (<see cref="IOspreySummaryPeakData"/>); each abstract base below narrows it to
    /// the tier its family needs, so a calculator declares its data dependency in the
    /// type system and the harness presents no more data than the score reads. See the
    /// tier hierarchy in <c>IOspreyPeakData.cs</c>.
    /// </summary>
    public interface IOspreyFeatureCalculator
    {
        /// <summary>
        /// The feature name -- the column emitted in the <c>.cs_features.tsv</c>
        /// dump. Parity-critical (matches the Rust PIN column name).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The human-friendly (Skyline-style) display label for this feature, used
        /// by the post-training feature-contribution report. Owned by the
        /// implementing calculator -- the single source of truth, in lockstep with
        /// <see cref="Name"/> and <see cref="IsReversedScore"/> -- so it cannot drift
        /// out of PIN-index order. Display text only; never written to a
        /// parity-gated column (those use <see cref="Name"/>).
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// True if a LOWER raw value is target-like ("better"), false if a higher
        /// value is better. Mirrors Skyline's <c>IPeakFeatureCalculator.IsReversedScore</c>.
        /// Defines the EXPECTED sign of the trained coefficient; the feature
        /// contribution table flags a feature as an unexpected direction when
        /// <c>IsReversedScore XOR (weight &lt; 0)</c> is true.
        /// </summary>
        bool IsReversedScore { get; }

        /// <summary>
        /// Compute this feature's value for one candidate peak.
        /// </summary>
        double Calculate(OspreyScoringContext context, IOspreySummaryPeakData peakData);
    }

    /// <summary>
    /// Base for scores that read only summary peak data (identity + chosen peak
    /// location), mirroring Skyline's <c>SummaryPeakFeatureCalculator</c>. The
    /// rt-deviation family is the only Osprey family at this tier.
    /// </summary>
    public abstract class SummaryOspreyFeatureCalculator : IOspreyFeatureCalculator
    {
        public abstract string Name { get; }

        public abstract string DisplayName { get; }

        public abstract bool IsReversedScore { get; }

        public abstract double Calculate(OspreyScoringContext context, IOspreySummaryPeakData peakData);
    }

    /// <summary>
    /// Base for scores that read per-fragment XICs, mirroring Skyline's
    /// <c>DetailedPeakFeatureCalculator</c>. Narrows the SPI's summary peak data to
    /// <see cref="IOspreyDetailedPeakData"/>; throws a clear error rather than a raw
    /// <see cref="InvalidCastException"/> if a narrower view is supplied. The
    /// coelution, peak-shape, and median-polish families ride this tier.
    /// </summary>
    public abstract class DetailedOspreyFeatureCalculator : IOspreyFeatureCalculator
    {
        public abstract string Name { get; }

        public abstract string DisplayName { get; }

        public abstract bool IsReversedScore { get; }

        public double Calculate(OspreyScoringContext context, IOspreySummaryPeakData peakData)
        {
            var detailed = peakData as IOspreyDetailedPeakData;
            if (detailed == null)
                throw new InvalidOperationException(
                    @"This OspreySharp feature calculator requires IOspreyDetailedPeakData; a narrower peak-data tier was provided.");
            return Calculate(context, detailed);
        }

        protected abstract double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData);
    }

    /// <summary>
    /// Base for scores that read the single apex MS2 spectrum -- the first tier above
    /// what Skyline's results layer can supply. Narrows the SPI's summary peak data to
    /// <see cref="IOspreyApexSpectrumPeakData"/>. The xcorr feature and the apex-match
    /// family ride this tier.
    /// </summary>
    public abstract class ApexSpectrumOspreyFeatureCalculator : IOspreyFeatureCalculator
    {
        public abstract string Name { get; }

        public abstract string DisplayName { get; }

        public abstract bool IsReversedScore { get; }

        public double Calculate(OspreyScoringContext context, IOspreySummaryPeakData peakData)
        {
            var apex = peakData as IOspreyApexSpectrumPeakData;
            if (apex == null)
                throw new InvalidOperationException(
                    @"This OspreySharp feature calculator requires IOspreyApexSpectrumPeakData; a narrower peak-data tier was provided.");
            return Calculate(context, apex);
        }

        protected abstract double Calculate(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData);
    }

    /// <summary>
    /// Base for scores that read the apex +/- 2 MS2 spectra -- the widest tier, two
    /// levels above Skyline. Narrows the SPI's summary peak data to
    /// <see cref="IOspreyApexSpectraPeakData"/>. The Savitzky-Golay sweep is the only
    /// family that rides this tier; the MS1 family does NOT (its precursor XIC +
    /// isotope envelope are produced upstream and exposed on
    /// <see cref="IOspreyDetailedPeakData"/>).
    /// </summary>
    public abstract class ApexSpectraOspreyFeatureCalculator : IOspreyFeatureCalculator
    {
        public abstract string Name { get; }

        public abstract string DisplayName { get; }

        public abstract bool IsReversedScore { get; }

        public double Calculate(OspreyScoringContext context, IOspreySummaryPeakData peakData)
        {
            var spectra = peakData as IOspreyApexSpectraPeakData;
            if (spectra == null)
                throw new InvalidOperationException(
                    @"This OspreySharp feature calculator requires IOspreyApexSpectraPeakData; a narrower peak-data tier was provided.");
            return Calculate(context, spectra);
        }

        protected abstract double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData);
    }
}
