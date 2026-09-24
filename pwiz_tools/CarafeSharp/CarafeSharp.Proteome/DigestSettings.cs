/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/input/CParameter.java
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
    /// <summary>
    /// The protein digestion options Carafe keeps in <c>CParameter</c>. The defaults are
    /// CParameter's own, which are not all what Carafe's help text says: missed cleavages
    /// default to 2 (help: 1), and N-terminal methionine clipping is on here but off on the
    /// command line unless <c>-clip_n_m</c> is given (see <see cref="CarafeCommandLine"/>).
    /// </summary>
    public sealed class DigestSettings
    {
        public const int DEFAULT_MAX_MISSED_CLEAVAGES = 2;
        public const int DEFAULT_MIN_LENGTH = 7;
        public const int DEFAULT_MAX_LENGTH = 35;

        /// <summary>Index into <see cref="EnzymeTable"/> (Carafe's <c>-enzyme</c>).</summary>
        public int EnzymeIndex { get; set; } = EnzymeTable.DEFAULT_ENZYME_INDEX;

        public int MaxMissedCleavages { get; set; } = DEFAULT_MAX_MISSED_CLEAVAGES;

        /// <summary>Shortest peptide kept, inclusive.</summary>
        public int MinLength { get; set; } = DEFAULT_MIN_LENGTH;

        /// <summary>Longest peptide kept, inclusive.</summary>
        public int MaxLength { get; set; } = DEFAULT_MAX_LENGTH;

        /// <summary>
        /// Also emit a protein's N-terminal peptides without their initiator methionine
        /// (Carafe's <c>clip_nTerm_M</c>). Never applied under NoCut.
        /// </summary>
        public bool ClipNTermMethionine { get; set; } = true;

        public Enzyme Enzyme
        {
            get { return EnzymeTable.GetByIndex(EnzymeIndex); }
        }
    }
}
