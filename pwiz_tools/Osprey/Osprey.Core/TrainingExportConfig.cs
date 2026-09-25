/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Settings of the optional training export (<c>--training-export</c>): a per-run parquet
    /// of every confidently identified target precursor's observed b/y ladder plus the
    /// interference evidence behind it, for a consumer that trains a fragment-intensity or
    /// retention-time model on Osprey's identifications (docs/22-training-export.md).
    ///
    /// <para>None of these settings is in any search identity hash. With
    /// <see cref="Enabled"/> false the export task is not part of the run at all, so every
    /// other artifact is byte-identical to a run that never heard of it; the task's own
    /// validity key carries the values below.</para>
    /// </summary>
    public class TrainingExportConfig
    {
        /// <summary>Default <see cref="ClaimantQ"/>.</summary>
        public const double DEFAULT_CLAIMANT_Q = 0.01;

        /// <summary>Write <c>&lt;stem&gt;.training.parquet</c> for every run.</summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Export targets whose second-pass run precursor q-value is at most this, or
        /// <c>--run-fdr</c> when unset (<see cref="EffectiveMaxQ"/>).
        /// </summary>
        public double? MaxQ { get; set; }

        /// <summary>
        /// The run q-value at or below which another target counts as a CLAIMANT of a shared
        /// fragment peak - the precursors whose evidence competes with an exported one - or
        /// <see cref="DEFAULT_CLAIMANT_Q"/> when unset (<see cref="EffectiveClaimantQ"/>).
        /// </summary>
        public double? ClaimantQ { get; set; }

        /// <summary>Also write each precursor's per-ion XIC matrix over its final boundaries.</summary>
        public bool WriteXics { get; set; }

        /// <summary>The claimant q actually applied.</summary>
        public double EffectiveClaimantQ => ClaimantQ ?? DEFAULT_CLAIMANT_Q;

        /// <summary>
        /// True when an export setting was given without the export itself - settings that
        /// would otherwise be silently inert.
        /// </summary>
        public bool HasSettingsWithoutExport => !Enabled && (MaxQ.HasValue || ClaimantQ.HasValue || WriteXics);

        /// <summary>The max-q actually applied.</summary>
        public double EffectiveMaxQ(double runFdr)
        {
            return MaxQ ?? runFdr;
        }
    }
}
