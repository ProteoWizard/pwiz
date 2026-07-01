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

using System;
using System.Collections.Generic;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Represents a single entry in a spectral library.
    /// Maps to osprey-core/src/types.rs in the Rust implementation.
    /// </summary>
    public class LibraryEntry
    {
        /// <summary>
        /// High bit of <see cref="Id"/> marking a decoy entry. The
        /// <c>base_id = Id &amp; 0x7FFFFFFF</c> convention pairs a target
        /// with its decoy. Generated decoys (<c>DecoyGenerator</c>) inherit
        /// this from the target; library-supplied decoys get the bit set
        /// during post-load marking when they match a configured prefix.
        /// Maps to Rust <c>osprey_core::types::DECOY_ID_BIT</c>.
        /// </summary>
        public const uint DECOY_ID_BIT = 0x80000000u;

        public uint Id { get; set; }
        public string Sequence { get; set; }
        public string ModifiedSequence { get; set; }
        public List<Modification> Modifications { get; set; }
        public byte Charge { get; set; }
        public double PrecursorMz { get; set; }
        public double RetentionTime { get; set; }
        public bool RtCalibrated { get; set; }
        public List<LibraryFragment> Fragments { get; set; }
        public List<string> ProteinIds { get; set; }
        public List<string> GeneNames { get; set; }
        public bool IsDecoy { get; set; }

        public LibraryEntry(uint id, string sequence, string modifiedSequence,
            byte charge, double precursorMz, double retentionTime)
        {
            Id = id;
            Sequence = sequence;
            ModifiedSequence = modifiedSequence;
            Charge = charge;
            PrecursorMz = precursorMz;
            RetentionTime = retentionTime;
            Modifications = new List<Modification>();
            Fragments = new List<LibraryFragment>();
            ProteinIds = new List<string>();
            GeneNames = new List<string>();
        }

        /// <summary>
        /// Tests whether this entry should be treated as a decoy based on a
        /// configured prefix list. Returns true if ANY protein accession
        /// starts (case-insensitively) with any of the prefixes.
        ///
        /// Used only when the user has set <c>DecoysInLibrary = true</c>
        /// (or <c>DecoyMethod = FromLibrary</c>). For Osprey-generated
        /// decoys, <see cref="IsDecoy"/> is set directly by
        /// <c>DecoyGenerator</c> and this function is not called.
        ///
        /// Returns false if the prefix list or the entry's protein list is
        /// empty. Empty prefix strings are ignored.
        /// Maps to Rust <c>LibraryEntry::looks_like_library_decoy</c>.
        /// </summary>
        public bool LooksLikeLibraryDecoy(IList<string> prefixes)
        {
            if (prefixes == null || prefixes.Count == 0 ||
                ProteinIds == null || ProteinIds.Count == 0)
                return false;
            for (int i = 0; i < ProteinIds.Count; i++)
            {
                string acc = ProteinIds[i];
                if (acc == null)
                    continue;
                for (int j = 0; j < prefixes.Count; j++)
                {
                    string p = prefixes[j];
                    if (string.IsNullOrEmpty(p))
                        continue;
                    if (acc.Length >= p.Length &&
                        acc.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }
    }
}
