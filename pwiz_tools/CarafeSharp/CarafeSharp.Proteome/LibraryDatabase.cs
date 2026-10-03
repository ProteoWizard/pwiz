/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.db.DBGear
 *   (protein_digest, digest_protein)
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// The two passes Carafe's library generation makes over its <c>-db</c> FASTA: the unique
    /// peptides to predict, and the peptide-to-protein map the library's ProteinID column comes
    /// from.
    /// </summary>
    public static class LibraryDatabase
    {
        /// <summary>The initial capacity of the protein map in <c>DBGear.digest_protein(String)</c>.</summary>
        private const int PROTEIN_MAP_CAPACITY = 10000;

        /// <summary>
        /// <c>DBGear.protein_digest</c>: every unique peptide of every record, those containing X
        /// dropped. <paramref name="digester"/> records the protein N-terminal peptides the
        /// protein N-term variable modifications need.
        /// </summary>
        public static HashSet<string> DigestPeptides(string fastaPath, Digester digester, Action<string> log = null)
        {
            var proteins = new HashSet<string>(StringComparer.Ordinal);
            int records = 0;
            foreach (var record in FastaReader.ReadFile(fastaPath, log))
            {
                records++;
                proteins.Add(JavaText.StripTerminalAsterisks(record.Sequence));
            }
            var peptides = new HashSet<string>(StringComparer.Ordinal);
            foreach (string protein in proteins)
            {
                foreach (string peptide in digester.Digest(protein))
                {
                    if (peptide.IndexOf('X') < 0)
                        peptides.Add(peptide);
                }
            }
            log?.Invoke(string.Format(@"Protein sequences:{0}, total unique peptide sequences:{1}", records, peptides.Count));
            return peptides;
        }

        /// <summary>
        /// <c>DBGear.digest_protein(String)</c>: each peptide's proteins, <c>;</c>-joined. A
        /// protein is the first whitespace-delimited token of its header, a repeated one keeps
        /// its last sequence, and the proteins are joined in the order Java's HashMap of them
        /// iterates (<see cref="JavaHashOrder"/>), which is what Carafe's parallel collector
        /// preserves. Carafe digests here with a fresh <c>DBGear</c>, so -I2L does not apply.
        /// </summary>
        public static Dictionary<string, string> MapPeptidesToProteins(string fastaPath, DigestSettings digest)
        {
            var sequences = new Dictionary<string, string>(StringComparer.Ordinal);
            var insertionOrder = new List<string>();
            foreach (var record in FastaReader.ReadFile(fastaPath))
            {
                string protein = JavaText.FirstToken(record.Header);
                if (!sequences.ContainsKey(protein))
                    insertionOrder.Add(protein);
                sequences[protein] = JavaText.StripTerminalAsterisks(record.Sequence);
            }
            var settings = new DigestSettings
            {
                EnzymeIndex = digest.EnzymeIndex,
                MaxMissedCleavages = digest.MaxMissedCleavages,
                MinLength = digest.MinLength,
                MaxLength = digest.MaxLength,
                ClipNTermMethionine = digest.ClipNTermMethionine,
            };
            var digester = new Digester(settings);
            var peptideToProteins = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string protein in JavaHashOrder.OrderStringKeys(insertionOrder, PROTEIN_MAP_CAPACITY))
            {
                foreach (string peptide in digester.Digest(sequences[protein]))
                {
                    peptideToProteins[peptide] = peptideToProteins.TryGetValue(peptide, out string proteins)
                        ? proteins + @";" + protein
                        : protein;
                }
            }
            return peptideToProteins;
        }
    }
}
