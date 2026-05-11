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
    /// One row of the cross-impl bisection trace for
    /// <see cref="ConsensusRts.Compute"/>: the (apex_rt, library_rt) pair
    /// for each detection that contributes to the consensus median, plus
    /// the sigmoid-of-SVM-score weight. Diffing this trace across the Rust
    /// reference and the OspreySharp port localizes
    /// <c>consensus_library_rt</c> divergence to either the loaded apex_rt
    /// (Parquet decode) or the LOESS inverse-interpolation step.
    /// </summary>
    public class InvPredictRecord
    {
        public string FileName { get; set; }
        public string ModifiedSequence { get; set; }
        public bool IsDecoy { get; set; }
        public double ApexRt { get; set; }
        public double LibraryRt { get; set; }
        public double Weight { get; set; }
    }
}
