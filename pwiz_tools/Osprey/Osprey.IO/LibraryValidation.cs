/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

using System.IO;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Invariants every spectral library must satisfy, checked while parsing so that
    /// downstream code may rely on them instead of defending against their absence.
    /// Kept in one place because each format has its own loader
    /// (<see cref="DiannTsvLoader"/>, <see cref="BlibLoader"/>) and an invariant enforced
    /// by only one of them is not an invariant.
    /// </summary>
    public static class LibraryValidation
    {
        /// <summary>
        /// Shortest peptide a library may contain. Deliberately a constant rather than a
        /// setting: a tryptic peptide below this length is not specific enough to identify,
        /// and the field has converged on excluding them (MaxQuant uses 6; the Carafe
        /// libraries the regression runs against start at 7). If someone has a real need
        /// for shorter peptides, this is the one place to change, and they should say why
        /// first.
        /// </summary>
        public const int MIN_PEPTIDE_LENGTH = 6;

        /// <summary>
        /// Throw when a library peptide is shorter than <see cref="MIN_PEPTIDE_LENGTH"/>.
        ///
        /// A hard error rather than a skip: a library carrying peptides this short is
        /// almost certainly built wrong, and silently dropping them would change the
        /// searched set without saying so. Downstream code is allowed to rely on the bound.
        /// <c>DecoyGenerator.IsCandidateAcceptable</c> is the case that matters today: its
        /// fragment-overlap ratio carries a structural 1/(n-1) floor, which is 0.2 against
        /// a 0.4 threshold at the enforced minimum but 0.5 at length 3, where every
        /// candidate decoy would be rejected and the peptide would vanish from the search.
        /// </summary>
        /// <param name="sequence">Stripped peptide sequence; a null sequence is not this
        /// check's concern and passes.</param>
        public static void ValidatePeptideLength(string sequence)
        {
            if (sequence == null || sequence.Length >= MIN_PEPTIDE_LENGTH)
                return;

            // Name the offending peptide: on a multi-million-row library an error that
            // does not is unactionable.
            throw new InvalidDataException(string.Format(
                "Library peptide '{0}' has {1} residues; the minimum supported length is {2}. " +
                "Peptides this short are not specific enough to identify and are excluded by " +
                "convention from spectral libraries. Rebuild the library with a longer minimum.",
                sequence, sequence.Length, MIN_PEPTIDE_LENGTH));
        }
    }
}
