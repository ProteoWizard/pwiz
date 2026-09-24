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

using System;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// Carafe's modification-aware precursor m/z window, the same "does this peptide put any
    /// precursor in the library" test its library prediction applies: a peptide passes when
    /// some peptidoform at some charge has an m/z in [min, max]. Peptidoforms are the fixed-only
    /// form plus every combination of 1 to maxVar variable-modification sites with at most
    /// maxModsPerAA modifications on one site, fixed modifications filling every site no
    /// variable one took. Masses are compomics's, to the bit.
    /// </summary>
    public sealed class ModifiedPrecursorMzFilter
    {
        private readonly IReadOnlyList<CarafeModification> _fixedModifications;
        private readonly IReadOnlyList<CarafeModification> _variableModifications;
        private readonly int _maxVariableModifications;
        private readonly int _maxModificationsPerSite;
        private readonly int[] _charges;
        private readonly double _minMz;
        private readonly double _maxMz;
        private readonly ISet<string> _proteinNTermPeptides;

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
            _fixedModifications = settings.GetFixedModifications();
            _variableModifications = settings.GetVariableModifications();
            _maxVariableModifications = settings.MaxVariableModifications;
            _maxModificationsPerSite = settings.MaxModificationsPerSite;
            _charges = charges;
            _minMz = minMz;
            _maxMz = maxMz;
            _proteinNTermPeptides = proteinNTermPeptides;
        }

        /// <summary>True when some peptidoform of a standard-residue peptide has an m/z in range.</summary>
        public bool Fits(string sequence)
        {
            var sites = VariableSites(sequence);
            int maxCount = Math.Min(sites.Count, _maxVariableModifications);
            for (int k = 1; k <= maxCount; k++)
            {
                foreach (var combination in Combinations(sites.Count, k))
                {
                    var chosen = combination.Select(i => sites[i]).ToList();
                    if (MaxModificationsOnOneSite(chosen) <= _maxModificationsPerSite && FormFits(sequence, chosen))
                        return true;
                }
            }
            return FormFits(sequence, Array.Empty<ModificationSite>());
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
        /// Whether one peptidoform fits: its variable modifications in combination order, then
        /// each fixed modification on each of its sites a variable one does not occupy.
        /// </summary>
        private bool FormFits(string sequence, IReadOnlyCollection<ModificationSite> variable)
        {
            var modificationMasses = variable.Select(site => site.Modification.Mass).ToList();
            var variableSites = new HashSet<int>(variable.Select(site => site.Position));
            foreach (var modification in _fixedModifications)
            {
                foreach (int site in modification.GetPossibleSites(sequence))
                {
                    if (!variableSites.Contains(site))
                        modificationMasses.Add(modification.Mass);
                }
            }
            double mass = CompomicsMasses.PeptideMass(sequence, modificationMasses);
            foreach (int z in _charges)
            {
                double mz = (mass + z * CompomicsMasses.PROTON) / z;
                if (_minMz <= mz && mz <= _maxMz)
                    return true;
            }
            return false;
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

        /// <summary>A candidate modification site: 0 for the N terminus, else the 1-based residue.</summary>
        private sealed class ModificationSite
        {
            public ModificationSite(int position, CarafeModification modification)
            {
                Position = position;
                Modification = modification;
            }

            public int Position { get; }
            public CarafeModification Modification { get; }
        }
    }
}
