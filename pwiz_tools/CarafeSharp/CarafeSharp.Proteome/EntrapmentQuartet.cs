/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/EntrapmentFastaGear.java
 *   (Quartet)
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// One target peptide, the entrapment (p_target), decoy and p_decoy sequences generated
    /// for it, and the proteins it came from. All but the target may be null.
    /// </summary>
    public sealed class EntrapmentQuartet
    {
        public EntrapmentQuartet(string target)
        {
            Target = target;
        }

        public string Target { get; }
        public string PTarget { get; set; }
        public string Decoy { get; set; }
        public string PDecoy { get; set; }

        /// <summary>
        /// True when this target was chosen to carry entrapment (all of them unless the ratio is
        /// below 1). Separates "deliberately has none" from "none could be generated", which
        /// must be dropped.
        /// </summary>
        public bool EntrapmentSelected { get; set; }

        /// <summary>Source proteins, in FASTA order until the builder sorts them.</summary>
        public List<ProteinRecord> Sources { get; } = new List<ProteinRecord>();

        public override string ToString()
        {
            return Target;
        }
    }
}
