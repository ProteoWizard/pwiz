/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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

namespace pwiz.CarafeSharp.Contracts
{
    /// <summary>
    /// One of the four peptide kinds of an FDRBench pairing manifest row
    /// (<see cref="PairingManifest"/>), the classification Osprey's library loader, its
    /// training export and CarafeSharp's FASTA builder all share. Maps to Rust
    /// <c>osprey_io::pairing::PeptideKind</c>.
    /// </summary>
    public enum PeptideKind
    {
        /// <summary>Original target peptide from a real protein.</summary>
        Target,
        /// <summary>Decoy of a real-protein target (randomized sequence).</summary>
        Decoy,
        /// <summary>Entrapment target peptide (a synthetic target).</summary>
        PTarget,
        /// <summary>Decoy of an entrapment target.</summary>
        PDecoy,
    }

    /// <summary>
    /// The FDRBench spellings of <see cref="PeptideKind"/> (<c>target</c>, <c>decoy</c>,
    /// <c>p_target</c>, <c>p_decoy</c>) and the two-way splits of the four kinds.
    /// </summary>
    public static class PeptideKinds
    {
        /// <summary>The manifest's <c>peptide_type</c> spelling of a kind.</summary>
        public static string Name(PeptideKind kind)
        {
            switch (kind)
            {
                case PeptideKind.Decoy:
                    return PairingManifest.KIND_DECOY;
                case PeptideKind.PTarget:
                    return PairingManifest.KIND_P_TARGET;
                case PeptideKind.PDecoy:
                    return PairingManifest.KIND_P_DECOY;
                default:
                    return PairingManifest.KIND_TARGET;
            }
        }

        /// <summary>The kind a manifest's <c>peptide_type</c> text names, trimmed and in any case.</summary>
        public static bool TryParse(string text, out PeptideKind kind)
        {
            string trimmed = text?.Trim();
            if (string.Equals(trimmed, PairingManifest.KIND_TARGET, StringComparison.OrdinalIgnoreCase))
            {
                kind = PeptideKind.Target;
                return true;
            }
            if (string.Equals(trimmed, PairingManifest.KIND_DECOY, StringComparison.OrdinalIgnoreCase))
            {
                kind = PeptideKind.Decoy;
                return true;
            }
            if (string.Equals(trimmed, PairingManifest.KIND_P_TARGET, StringComparison.OrdinalIgnoreCase))
            {
                kind = PeptideKind.PTarget;
                return true;
            }
            if (string.Equals(trimmed, PairingManifest.KIND_P_DECOY, StringComparison.OrdinalIgnoreCase))
            {
                kind = PeptideKind.PDecoy;
                return true;
            }
            kind = PeptideKind.Target;
            return false;
        }

        /// <summary>True for the decoy side of a pair: <c>decoy</c> or <c>p_decoy</c>.</summary>
        public static bool IsDecoySide(PeptideKind kind)
        {
            return kind == PeptideKind.Decoy || kind == PeptideKind.PDecoy;
        }

        /// <summary>True for the entrapment partition: <c>p_target</c> or <c>p_decoy</c>.</summary>
        public static bool IsEntrapment(PeptideKind kind)
        {
            return kind == PeptideKind.PTarget || kind == PeptideKind.PDecoy;
        }
    }
}
