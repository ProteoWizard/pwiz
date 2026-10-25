/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/EntrapmentFastaGear.java
 *   (Result)
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>Summary counts of an entrapment FASTA build, as Carafe logs them.</summary>
    public sealed class EntrapmentFastaResult
    {
        public int Proteins { get; set; }
        public int UniqueTargets { get; set; }
        public int DroppedUnknownAa { get; set; }
        public int DroppedOutOfMz { get; set; }
        public int QuartetsBuilt { get; set; }
        public int QuartetsDropped { get; set; }
        public int KeptQuartets { get; set; }
        public int TargetEntries { get; set; }
        public int PTargetEntries { get; set; }
        public int DecoyEntries { get; set; }
        public int PDecoyEntries { get; set; }

        /// <summary>Kept peptides with more than one source protein.</summary>
        public int SharedEntries { get; set; }

        /// <summary>Selected targets with no acceptable entrapment (quartet dropped).</summary>
        public int DroppedNoEntrapment { get; set; }

        /// <summary>Dropped quartets whose target had no acceptable decoy.</summary>
        public int DroppedNoDecoy { get; set; }

        /// <summary>Entrapment peptides drawn from a foreign proteome.</summary>
        public int EntrapmentFromForeign { get; set; }

        /// <summary>Targets deliberately left without entrapment because the ratio is below 1.</summary>
        public int EntrapmentNotSelected { get; set; }

        /// <summary>Median |target - entrapment| neutral mass (Da) of a foreign assignment.</summary>
        public double EntrapmentMedianAbsMassDelta { get; set; }

        /// <summary>Fraction of foreign entrapment peptides within the co-location window.</summary>
        public double EntrapmentInIsolationWindowFraction { get; set; }

        /// <summary>Foreign candidates excluded for being I/L-isobaric to a real target.</summary>
        public int EntrapmentIlCollisionsDropped { get; set; }
    }
}
