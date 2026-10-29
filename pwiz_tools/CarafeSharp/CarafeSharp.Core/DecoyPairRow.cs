/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.db.DecoyPairPlanner.DecoyPair
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

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// One row of the <c>DecoyPairs</c> table Carafe adds to a .blib: a target or decoy
    /// precursor, the pairing it belongs to, and, for a decoy, its target.
    /// </summary>
    public sealed class DecoyPairRow
    {
        public DecoyPairRow(int refSpectraId, bool isDecoy, bool isEntrapment, int pairingId, int? targetRefSpectraId, string method)
        {
            RefSpectraId = refSpectraId;
            IsDecoy = isDecoy;
            IsEntrapment = isEntrapment;
            PairingId = pairingId;
            TargetRefSpectraId = targetRefSpectraId;
            Method = method;
        }

        public int RefSpectraId { get; }

        public bool IsDecoy { get; }

        /// <summary>True for an FDRBench entrapment pair (<c>p_target</c> / <c>p_decoy</c>).</summary>
        public bool IsEntrapment { get; }

        /// <summary>The manifest's <c>peptide_pair_index</c>: shared by a target, its entrapment target and both their decoys.</summary>
        public int PairingId { get; }

        /// <summary>A decoy's target precursor, at the same charge; null on a target.</summary>
        public int? TargetRefSpectraId { get; }

        /// <summary><c>reverse</c> or <c>cycle</c> on decoy rows; null on targets.</summary>
        public string Method { get; }
    }
}
