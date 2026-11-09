/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.input.PeptideUtils
 *   (calcPeptideIsoforms, addFixedModification)
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
    /// Carafe's peptidoforms of a peptide, in Carafe's order: every combination of 1 to maxVar
    /// variable-modification sites (combinations in lexicographic order of the candidate
    /// list, sizes ascending) with at most maxModsPerAA modifications on one site, then the
    /// form with fixed modifications only. Fixed modifications fill every site of theirs that
    /// no variable modification took.
    /// </summary>
    public sealed class PeptideIsoformGenerator
    {
        private readonly IReadOnlyList<CarafeModification> _fixedModifications;
        private readonly IReadOnlyList<CarafeModification> _variableModifications;
        private readonly int _maxVariableModifications;
        private readonly int _maxModificationsPerSite;
        private readonly ISet<string> _proteinNTermPeptides;

        /// <param name="settings">The modifications to enumerate.</param>
        /// <param name="proteinNTermPeptides">
        /// Peptides seen at a protein N terminus, which alone may carry a protein N-term
        /// variable modification (<see cref="Digester.ProteinNTermPeptides"/>).
        /// </param>
        public PeptideIsoformGenerator(ModificationSettings settings, ISet<string> proteinNTermPeptides)
        {
            _fixedModifications = settings.GetFixedModifications();
            _variableModifications = settings.GetVariableModifications();
            _maxVariableModifications = settings.MaxVariableModifications;
            _maxModificationsPerSite = settings.MaxModificationsPerSite;
            _proteinNTermPeptides = proteinNTermPeptides;
        }

        /// <summary>The peptidoforms of a standard-residue peptide, generated lazily.</summary>
        public IEnumerable<PeptideIsoform> Enumerate(string sequence)
        {
            var sites = VariableSites(sequence);
            int maxCount = Math.Min(sites.Count, _maxVariableModifications);
            for (int k = 1; k <= maxCount; k++)
            {
                foreach (var combination in Combinations(sites.Count, k))
                {
                    var chosen = combination.Select(i => sites[i]).ToList();
                    if (MaxModificationsOnOneSite(chosen) <= _maxModificationsPerSite)
                        yield return CreateIsoform(sequence, chosen);
                }
            }
            yield return CreateIsoform(sequence, new List<ModificationSite>());
        }

        /// <summary>
        /// Every (site, variable modification) pair, modifications in option order and sites
        /// ascending within each, as Carafe lists them.
        /// </summary>
        private List<ModificationSite> VariableSites(string sequence)
        {
            var sites = new List<ModificationSite>();
            foreach (var modification in _variableModifications)
            {
                if (modification.Type == CarafeModificationType.protein_n_term)
                {
                    if (_proteinNTermPeptides.Contains(sequence))
                        sites.Add(new ModificationSite(0, modification));
                    continue;
                }
                foreach (int site in modification.GetPossibleSites(sequence))
                    sites.Add(new ModificationSite(site, modification));
            }
            return sites;
        }

        /// <summary>
        /// The variable modifications in combination order, then each fixed modification on
        /// each of its sites a variable one does not occupy (PeptideUtils.addFixedModification).
        /// </summary>
        private PeptideIsoform CreateIsoform(string sequence, List<ModificationSite> variable)
        {
            var variableSites = new HashSet<int>(variable.Select(site => site.Position));
            var modifications = variable;
            foreach (var modification in _fixedModifications)
            {
                foreach (int site in modification.GetPossibleSites(sequence))
                {
                    if (!variableSites.Contains(site))
                        modifications.Add(new ModificationSite(site, modification));
                }
            }
            return new PeptideIsoform(sequence, modifications);
        }

        private static int MaxModificationsOnOneSite(IEnumerable<ModificationSite> sites)
        {
            return sites.GroupBy(site => site.Position).Max(group => group.Count());
        }

        /// <summary>The k-subsets of 0..n-1 as ascending index arrays, in lexicographic order.</summary>
        private static IEnumerable<int[]> Combinations(int n, int k)
        {
            var indices = new int[k];
            for (int i = 0; i < k; i++)
                indices[i] = i;
            while (true)
            {
                yield return indices;
                int position = k - 1;
                while (position >= 0 && indices[position] == n - k + position)
                    position--;
                if (position < 0)
                    yield break;
                indices[position]++;
                for (int i = position + 1; i < k; i++)
                    indices[i] = indices[i - 1] + 1;
            }
        }
    }
}
