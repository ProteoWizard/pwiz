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

using System.Collections.Generic;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// A fully scored DIA coelution search result, combining library identity,
    /// peak boundaries, feature scores, and fragment data.
    /// Maps to osprey-core/src/types.rs CoelutionScoredEntry.
    /// </summary>
    public class CoelutionScoredEntry
    {
        public uint EntryId { get; set; }
        public bool IsDecoy { get; set; }
        public string Sequence { get; set; }
        public string ModifiedSequence { get; set; }
        public byte Charge { get; set; }
        public double PrecursorMz { get; set; }
        public List<string> ProteinIds { get; set; }
        public uint ScanNumber { get; set; }
        public double ApexRt { get; set; }
        public XICPeakBounds PeakBounds { get; set; }
        public CoelutionFeatureSet Features { get; set; }
        public double[] FragmentMzs { get; set; }
        public float[] FragmentIntensities { get; set; }
        public List<CwtCandidate> CwtCandidates { get; set; }
        public string FileName { get; set; }

        public CoelutionScoredEntry()
        {
            ProteinIds = new List<string>();
            FragmentMzs = new double[0];
            FragmentIntensities = new float[0];
            CwtCandidates = new List<CwtCandidate>();
            PeakBounds = new XICPeakBounds();
            Features = new CoelutionFeatureSet();
        }
    }
}
