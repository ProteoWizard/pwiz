/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.FDR.ModelDiagnostics
{
    /// <summary>
    /// Where the searched library's entrapment peptides sit: on entrapment proteins of their
    /// own (the standard design, the <c>_p_target</c> accessions), or on real target proteins
    /// (the protein-aware audit, where an entrapment peptide can collect the protein evidence a
    /// false target would). The FDP views split their entrapment counts by this, so a
    /// protein-aware step that boosts targets on detected proteins shows as the two halves
    /// diverging; each half is scaled by its own entrapment-to-target ratio.
    /// </summary>
    public sealed class EntrapmentPlacement
    {
        /// <summary>Library base-ids of the entrapment peptides placed on real target proteins.</summary>
        public HashSet<uint> OnTargetProteinBaseIds { get; } = new HashSet<uint>();

        /// <summary>Entrapment peptides on real target proteins, over the library's targets.</summary>
        public double RatioOnTargetProteins { get; set; }

        /// <summary>Entrapment peptides on entrapment proteins of their own, over the library's targets.</summary>
        public double RatioOnEntrapmentProteins { get; set; }

        /// <summary>True when the library carries entrapment on target proteins, so a split is worth showing.</summary>
        public bool HasEntrapmentOnTargetProteins
        {
            get { return OnTargetProteinBaseIds.Count > 0; }
        }

        /// <summary>True when <paramref name="baseId"/> is an entrapment peptide placed on a real target protein.</summary>
        public bool IsOnTargetProtein(uint baseId)
        {
            return OnTargetProteinBaseIds.Contains(baseId);
        }
    }
}
