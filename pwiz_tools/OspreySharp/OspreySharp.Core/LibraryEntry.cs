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
    /// Represents a single entry in a spectral library.
    /// Maps to osprey-core/src/types.rs in the Rust implementation.
    /// </summary>
    public class LibraryEntry
    {
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
    }
}
