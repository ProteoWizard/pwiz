/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on compomics-utilities 5.0.39P2 (https://github.com/compomics/compomics-utilities)
 *   com.compomics.util.experiment.biology.enzymes.Enzyme, Apache-2.0, as Carafe
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

using System.Collections.Generic;
using System.Text;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// A cleavage rule with compomics semantics. A site between two residues cleaves when a
    /// residue the left one stands for is an "amino acid before" and no residue the right one
    /// stands for is a "restriction after", or symmetrically for "amino acid after" and
    /// "restriction before". Ambiguity codes expand through <see cref="CompomicsAminoAcids"/>,
    /// so for trypsin K-X is not cleaved (X could be P) while X-K is (X could be K or R).
    /// </summary>
    public sealed class Enzyme
    {
        private readonly HashSet<char> _aminoAcidBefore;
        private readonly HashSet<char> _aminoAcidAfter;
        private readonly HashSet<char> _restrictionBefore;
        private readonly HashSet<char> _restrictionAfter;

        public Enzyme(string name, string aminoAcidBefore, string aminoAcidAfter = @"",
            string restrictionBefore = @"", string restrictionAfter = @"")
        {
            Name = name;
            _aminoAcidBefore = new HashSet<char>(aminoAcidBefore);
            _aminoAcidAfter = new HashSet<char>(aminoAcidAfter);
            _restrictionBefore = new HashSet<char>(restrictionBefore);
            _restrictionAfter = new HashSet<char>(restrictionAfter);
        }

        public string Name { get; }

        /// <summary>
        /// Same as compomics <c>Enzyme.isCleavageSite(char, char)</c>. Throws for a character
        /// that is not a letter, as compomics does.
        /// </summary>
        public bool IsCleavageSite(char aaBefore, char aaAfter)
        {
            string subBefore = CompomicsAminoAcids.GetSubResidues(aaBefore);
            string subAfter = CompomicsAminoAcids.GetSubResidues(aaAfter);
            foreach (char before in subBefore)
            {
                if (_aminoAcidBefore.Contains(before) && !ContainsAny(_restrictionAfter, subAfter))
                    return true;
            }
            foreach (char after in subAfter)
            {
                if (_aminoAcidAfter.Contains(after) && !ContainsAny(_restrictionBefore, subBefore))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Same as compomics <c>Enzyme.digest(String, int, Integer, Integer)</c>: every fully
        /// cleaved peptide plus, for each k in 1..<paramref name="maxMissedCleavages"/>, every
        /// run of up to k + 1 consecutive peptides, kept when its length is within the inclusive
        /// bounds (a null bound is not applied). Note compomics's window for k also emits the
        /// shorter runs at the start of the protein, which the smaller windows produce anyway.
        /// </summary>
        public HashSet<string> Digest(string sequence, int maxMissedCleavages, int? minLength, int? maxLength)
        {
            char aaAfter = sequence[0];
            var currentPeptide = new StringBuilder();
            currentPeptide.Append(aaAfter);
            var results = new HashSet<string>();
            // Keyed 1..maxMissedCleavages; iterated in key order, which is also the order of
            // Java's HashMap for these small integer keys.
            var windows = new List<List<string>>();
            for (int i = 1; i <= maxMissedCleavages; i++)
                windows.Add(new List<string>(maxMissedCleavages));

            for (int i = 1; i < sequence.Length; i++)
            {
                char aa = sequence[i];
                char aaBefore = aaAfter;
                aaAfter = aa;
                if (IsCleavageSite(aaBefore, aaAfter) && currentPeptide.Length != 0)
                {
                    AddPeptideAndWindows(currentPeptide.ToString(), windows, minLength, maxLength, results);
                    currentPeptide.Clear();
                }
                currentPeptide.Append(aa);
            }
            AddPeptideAndWindows(currentPeptide.ToString(), windows, minLength, maxLength, results);
            return results;
        }

        public override string ToString()
        {
            return Name;
        }

        private static void AddPeptideAndWindows(string peptide, List<List<string>> windows,
            int? minLength, int? maxLength, HashSet<string> results)
        {
            if (IsWithin(peptide.Length, minLength, maxLength))
                results.Add(peptide);
            for (int w = 0; w < windows.Count; w++)
            {
                var window = windows[w];
                int missedCleavages = w + 1;
                window.Add(peptide);
                while (window.Count > missedCleavages + 1)
                    window.RemoveAt(0);
                int length = 0;
                foreach (string part in window)
                    length += part.Length;
                if (IsWithin(length, minLength, maxLength))
                    results.Add(string.Concat(window));
            }
        }

        private static bool IsWithin(int length, int? minLength, int? maxLength)
        {
            return (!minLength.HasValue || length >= minLength.Value) &&
                   (!maxLength.HasValue || length <= maxLength.Value);
        }

        private static bool ContainsAny(HashSet<char> set, string residues)
        {
            foreach (char c in residues)
            {
                if (set.Contains(c))
                    return true;
            }
            return false;
        }
    }
}
