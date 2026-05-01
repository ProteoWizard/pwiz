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

namespace pwiz.OspreySharp.FDR.Reconciliation
{
    /// <summary>
    /// Result of inter-replicate peak reconciliation for a single entry.
    /// Maps to <c>ReconcileAction</c> in
    /// <c>osprey/crates/osprey/src/reconciliation.rs</c>.
    /// </summary>
    public abstract class ReconcileAction
    {
        // Private constructor: only the nested subclasses below can inherit,
        // which makes this an effective discriminated union.
        private ReconcileAction()
        {
        }

        /// <summary>
        /// Existing peak already matches consensus RT — no re-scoring needed.
        /// Planner omits these from its returned map, so in consumers this
        /// case is "not present in the map" rather than a returned value.
        /// The singleton instance is exposed for tests and pattern-matching.
        /// </summary>
        public static ReconcileAction Keep { get; } = new KeepAction();

        private sealed class KeepAction : ReconcileAction
        {
        }

        /// <summary>
        /// Use an alternate stored CWT candidate whose apex is closest to the
        /// expected RT (within tolerance).
        /// </summary>
        public sealed class UseCwtPeak : ReconcileAction
        {
            /// <summary>Index into the entry's stored CWT candidates.</summary>
            public int CandidateIndex { get; }

            /// <summary>Selected CWT peak start RT (measured space, minutes).</summary>
            public double StartRt { get; }

            /// <summary>Selected CWT peak apex RT (measured space, minutes).</summary>
            public double ApexRt { get; }

            /// <summary>Selected CWT peak end RT (measured space, minutes).</summary>
            public double EndRt { get; }

            public UseCwtPeak(int candidateIndex, double startRt, double apexRt, double endRt)
            {
                CandidateIndex = candidateIndex;
                StartRt = startRt;
                ApexRt = apexRt;
                EndRt = endRt;
            }
        }

        /// <summary>
        /// No stored CWT candidate lies within tolerance of the expected RT —
        /// integrate at the consensus RT with a fixed half-width.
        /// </summary>
        public sealed class ForcedIntegration : ReconcileAction
        {
            /// <summary>Expected measured RT (from refined calibration).</summary>
            public double ExpectedRt { get; }

            /// <summary>Integration half-width (half of median peak width).</summary>
            public double HalfWidth { get; }

            public ForcedIntegration(double expectedRt, double halfWidth)
            {
                ExpectedRt = expectedRt;
                HalfWidth = halfWidth;
            }
        }
    }
}
