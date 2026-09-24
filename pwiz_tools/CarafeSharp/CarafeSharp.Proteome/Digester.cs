/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/DBGear.java
 *   (digest_protein(Enzyme, String))
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
using System.Linq;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// Digests protein sequences the way Carafe's <c>DBGear.digest_protein</c> does, including
    /// the optional initiator-methionine clip and the record of which peptides started a
    /// protein, which gates protein N-terminal variable modifications.
    /// </summary>
    public sealed class Digester
    {
        private readonly int _maxMissedCleavages;
        private readonly int _minLength;
        private readonly int _maxLength;
        private readonly bool _clipNTermMethionine;
        private readonly bool _isNoCut;

        public Digester(DigestSettings settings)
        {
            Enzyme = settings.Enzyme;
            _maxMissedCleavages = settings.MaxMissedCleavages;
            _minLength = settings.MinLength;
            _maxLength = settings.MaxLength;
            _clipNTermMethionine = settings.ClipNTermMethionine;
            _isNoCut = EnzymeTable.IsNoCut(Enzyme);
        }

        public Enzyme Enzyme { get; }

        /// <summary>
        /// Every peptide seen at the start of a digested protein, with and without a clipped
        /// methionine (Carafe's static <c>PeptideUtils.protein_n_term_peptides</c>, here per
        /// digester). It grows across calls and is never recorded under NoCut.
        /// </summary>
        public HashSet<string> ProteinNTermPeptides { get; } = new HashSet<string>();

        /// <summary>
        /// The unique peptides of one protein. The sequence is upper-cased and a leading and a
        /// trailing asterisk removed first; an empty result, or a character that is not a letter
        /// anywhere but a one-residue sequence, throws as it does in Carafe.
        /// </summary>
        public HashSet<string> Digest(string proteinSequence)
        {
            proteinSequence = JavaText.StripTerminalAsterisks(JavaText.ToUpper(proteinSequence));
            var peptides = Enzyme.Digest(proteinSequence, _maxMissedCleavages, _minLength, _maxLength);
            if (_clipNTermMethionine && !_isNoCut && proteinSequence.StartsWith(@"M", StringComparison.Ordinal))
            {
                var clipped = peptides
                    .Where(pep => proteinSequence.StartsWith(pep, StringComparison.Ordinal) && pep.Length >= _minLength + 1)
                    .Select(pep => pep.Substring(1))
                    .ToList();
                if (clipped.Count > 0)
                {
                    peptides.UnionWith(clipped);
                    ProteinNTermPeptides.UnionWith(clipped);
                }
            }
            if (!_isNoCut)
                ProteinNTermPeptides.UnionWith(peptides.Where(pep => proteinSequence.StartsWith(pep, StringComparison.Ordinal)));
            return peptides;
        }
    }
}
