/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/EntrapmentFastaGear.java
 *   (fitsMzRangeWithMods) and src/main/java/input/PeptideUtils.java (calcPeptideIsoforms,
 *   addFixedModification)
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
    /// Carafe's modification-aware precursor m/z window, the same "does this peptide put any
    /// precursor in the library" test its library prediction applies: a peptide passes when
    /// some peptidoform (<see cref="PeptideIsoformGenerator"/>) at some charge has an m/z in
    /// [min, max]. Masses are compomics's, to the bit.
    /// </summary>
    public sealed class ModifiedPrecursorMzFilter
    {
        private readonly PeptideIsoformGenerator _isoforms;
        private readonly int[] _charges;
        private readonly double _minMz;
        private readonly double _maxMz;

        /// <param name="settings">The modifications to enumerate.</param>
        /// <param name="charges">Precursor charges to try.</param>
        /// <param name="minMz">Lower m/z bound, inclusive.</param>
        /// <param name="maxMz">Upper m/z bound, inclusive.</param>
        /// <param name="proteinNTermPeptides">
        /// Peptides seen at a protein N terminus so far, which alone may carry a protein
        /// N-term variable modification (a live view of <see cref="Digester.ProteinNTermPeptides"/>).
        /// </param>
        public ModifiedPrecursorMzFilter(ModificationSettings settings, int[] charges, double minMz, double maxMz,
            ISet<string> proteinNTermPeptides)
        {
            _isoforms = new PeptideIsoformGenerator(settings, proteinNTermPeptides);
            _charges = charges;
            _minMz = minMz;
            _maxMz = maxMz;
        }

        /// <summary>True when some peptidoform of a standard-residue peptide has an m/z in range.</summary>
        public bool Fits(string sequence)
        {
            foreach (var isoform in _isoforms.Enumerate(sequence))
            {
                foreach (int z in _charges)
                {
                    double mz = isoform.GetMz(z);
                    if (_minMz <= mz && mz <= _maxMz)
                        return true;
                }
            }
            return false;
        }
    }
}
