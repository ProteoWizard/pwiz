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

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// One scoring feature, mirroring Skyline's
    /// <c>IPeakFeatureCalculator</c>. Each calculator is a stateless singleton
    /// that computes a single PIN feature value from a candidate's peak data and
    /// the shared scoring context. The parity-critical order and identity of the
    /// 21 features lives in <see cref="OspreyFeatureCalculators"/>, not on the
    /// calculator.
    /// </summary>
    public interface IOspreyFeatureCalculator
    {
        /// <summary>
        /// The feature name -- the column emitted in the <c>.cs_features.tsv</c>
        /// dump. Parity-critical (matches the Rust PIN column name).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Compute this feature's value for one candidate peak.
        /// </summary>
        double Calculate(OspreyScoringContext context, IOspreyPeakData peakData);
    }

    /// <summary>
    /// Base for OspreySharp scoring calculators, mirroring Skyline's
    /// <c>DetailedPeakFeatureCalculator</c> (it narrows the peak data to the
    /// detailed view). Every current Osprey feature reads detailed
    /// (chromatogram/spectrum) data, so -- unlike Skyline -- there is no
    /// summary-only calculator base. The data-interface split
    /// (<see cref="IOspreyPeakData"/> / <see cref="IOspreyDetailedPeakData"/>)
    /// still mirrors <c>ISummaryPeakData</c> / <c>IDetailedPeakData</c>.
    /// </summary>
    public abstract class DetailedOspreyFeatureCalculator : IOspreyFeatureCalculator
    {
        public abstract string Name { get; }

        public double Calculate(OspreyScoringContext context, IOspreyPeakData peakData)
        {
            return Calculate(context, (IOspreyDetailedPeakData) peakData);
        }

        protected abstract double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData);
    }
}
