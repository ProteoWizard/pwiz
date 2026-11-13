/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on compomics-utilities 5.0.39P2 (https://github.com/compomics/compomics-utilities)
 *   com.compomics.util.experiment.biology.aminoacids.AminoAcid, Apache-2.0, as Carafe
 *   (https://github.com/maccoss/carafe) vendors it in lib/utilities-5.0.39P2.jar
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// The residue letters compomics expands each one-letter code to when it tests a cleavage
    /// site (<c>AminoAcid.getSubAminoAcids()</c>): a standard residue is only itself, the
    /// ambiguity codes B, J and Z are their two residues, and X is every letter except X,
    /// including B, J and Z. Lookups are case-insensitive, as in compomics, and anything outside
    /// A-Z throws, which is how a stray character in a protein sequence fails a Carafe digest.
    /// </summary>
    public static class CompomicsAminoAcids
    {
        // Values dumped from the Carafe-vendored jar, in its order (the order only decides
        // which match is found first, never whether one is).
        private const string X_SUB_RESIDUES = @"ABCDEFGHIJKLMNPQRSTYUOVWZ";

        private static readonly string[] SUB_RESIDUES = BuildSubResidues();

        /// <summary>
        /// The residues <paramref name="aa"/> stands for, as <c>AminoAcid.getAminoAcid(aa)
        /// .getSubAminoAcids()</c> returns them.
        /// </summary>
        public static string GetSubResidues(char aa)
        {
            char upper = aa >= 'a' && aa <= 'z' ? (char)(aa - 'a' + 'A') : aa;
            if (upper < 'A' || upper > 'Z')
                throw new ArgumentException(string.Format(@"No amino acid found for letter {0}.", aa));
            return SUB_RESIDUES[upper - 'A'];
        }

        private static string[] BuildSubResidues()
        {
            var table = new string[26];
            for (char c = 'A'; c <= 'Z'; c++)
                table[c - 'A'] = c.ToString();
            table['B' - 'A'] = @"ND";
            table['J' - 'A'] = @"IL";
            table['Z' - 'A'] = @"QE";
            table['X' - 'A'] = X_SUB_RESIDUES;
            return table;
        }
    }
}
