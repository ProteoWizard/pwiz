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
    /// Consensus RT for a peptide across all runs.
    /// Computed independently for targets and decoys to maintain fair
    /// target-decoy competition in the second FDR pass.
    /// Maps to <c>PeptideConsensusRT</c> in
    /// <c>osprey/crates/osprey/src/reconciliation.rs</c>.
    /// </summary>
    public class PeptideConsensusRT
    {
        /// <summary>Modified sequence (peptide-level grouping).</summary>
        public string ModifiedSequence { get; set; }

        /// <summary>Whether this is a decoy consensus.</summary>
        public bool IsDecoy { get; set; }

        /// <summary>Consensus library RT (sigmoid-weighted median from all runs).</summary>
        public double ConsensusLibraryRt { get; set; }

        /// <summary>
        /// Sigmoid-weighted median peak width across runs where detected
        /// (minutes, measured RT space).
        /// </summary>
        public double MedianPeakWidth { get; set; }

        /// <summary>Number of runs where this peptide was detected.</summary>
        public int NRunsDetected { get; set; }

        /// <summary>
        /// MAD of this peptide's apex RTs across runs, in library RT space
        /// (minutes). Null when fewer than 3 detections contribute
        /// (insufficient to estimate). Captures within-peptide RT
        /// reproducibility, typically 3-5x tighter than the cross-peptide
        /// calibration MAD. Consumed by the reconciliation planner as a
        /// peptide-specific RT tolerance.
        /// </summary>
        public double? ApexLibraryRtMad { get; set; }
    }
}
