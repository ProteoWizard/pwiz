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

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Immutable metadata for one trained PIN feature: the machine name, the
    /// human-friendly display label, and the expected score direction. This is the
    /// projection of an Osprey feature calculator that the FDR layer is allowed to
    /// see -- the FDR project does not reference the Scoring assembly that owns the
    /// calculator SPI, so the Tasks-layer caller populates a vector of these from
    /// the calculators and passes it down. It replaces what used to be three
    /// parallel name / label / reversed-score arrays threaded through the config.
    /// </summary>
    public readonly struct OspreyFeatureInfo
    {
        /// <summary>
        /// Machine feature name -- the parity-critical PIN / parquet column name,
        /// written verbatim into the Stage 5 diagnostic TSV dumps.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Human-friendly (Skyline-style) display label for the feature-contribution
        /// report, or <c>null</c> to fall back to <see cref="Name"/>. Display only --
        /// never written to a parity-gated column.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// True when a LOWER raw value is target-like (Skyline's
        /// <c>IsReversedScore</c>). Defines the EXPECTED sign of the trained
        /// coefficient; the contribution table flags a feature as an unexpected
        /// direction when <c>IsReversedScore XOR (weight &lt; 0)</c> is true.
        /// </summary>
        public bool IsReversedScore { get; }

        /// <param name="name">Machine PIN feature name.</param>
        /// <param name="label">Display label, or null to fall back to <paramref name="name"/>.</param>
        /// <param name="isReversedScore">Whether a lower raw value is target-like.</param>
        public OspreyFeatureInfo(string name, string label, bool isReversedScore)
        {
            Name = name;
            Label = label;
            IsReversedScore = isReversedScore;
        }
    }
}
