/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Training
{
    /// <summary>
    /// Turns Osprey's modifications (0-based residue position, mass delta, UniMod id) into an
    /// alphabase <see cref="PeptideForm"/> (<c>Name@Site</c> at site 0 for the N-terminus and
    /// 1..n for residues). The UniMod id decides the name when the library gave one; otherwise
    /// the mass does, within <see cref="MASS_TOLERANCE"/>. Osprey puts an N-terminal
    /// modification at position 0 like a modification of the first residue, so the modified
    /// sequence's leading bracket tells them apart.
    /// </summary>
    public static class OspreyModificationMapper
    {
        public const double MASS_TOLERANCE = 0.001;

        private const string ANY_N_TERM = @"Any N-term";
        private const string PROTEIN_N_TERM = @"Protein N-term";

        private static readonly Lazy<Dictionary<string, List<ModificationDefinition>>> BY_SITE =
            new Lazy<Dictionary<string, List<ModificationDefinition>>>(() => ModificationTable.All
                .GroupBy(Site, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal));

        /// <summary>
        /// Maps one precursor's modifications, or returns false with the reason when one of them
        /// has no alphabase equivalent (the precursor cannot be featurized).
        /// </summary>
        public static bool TryMap(string sequence, string modifiedSequence, IReadOnlyList<int> positions,
            IReadOnlyList<double> masses, IReadOnlyList<int> unimodIds, out PeptideForm peptide, out string reason)
        {
            peptide = null;
            reason = null;
            var names = new List<string>(positions.Count);
            var sites = new List<int>(positions.Count);
            bool leadingNTerm = HasLeadingModification(modifiedSequence);
            for (int i = 0; i < positions.Count; i++)
            {
                int position = positions[i];
                if (position < 0 || position >= sequence.Length)
                {
                    reason = string.Format(@"modification position {0} is outside {1}", position, sequence);
                    return false;
                }
                int unimod = i < unimodIds.Count ? unimodIds[i] : -1;
                // Only the first modification at position 0 can be the bracketed N-terminal one.
                bool nTerm = position == 0 && leadingNTerm && !sites.Contains(0);
                var definition = nTerm
                    ? Find(ANY_N_TERM, masses[i], unimod) ?? Find(PROTEIN_N_TERM, masses[i], unimod)
                    : Find(sequence[position].ToString(), masses[i], unimod);
                if (definition == null)
                {
                    reason = string.Format(@"no alphabase modification of {0:F4} Da (UniMod {1}) at {2}{3}",
                        masses[i], unimod, nTerm ? @"the N-terminus of " : sequence[position] + @" of ", sequence);
                    return false;
                }
                names.Add(definition.Name);
                sites.Add(nTerm ? 0 : position + 1);
            }
            peptide = new PeptideForm(sequence, names, sites);
            return true;
        }

        private static ModificationDefinition Find(string site, double mass, int unimod)
        {
            if (!BY_SITE.Value.TryGetValue(site, out var candidates))
                return null;
            if (unimod > 0)
            {
                var byId = candidates.FirstOrDefault(d => d.UnimodId == unimod && Math.Abs(d.Mass - mass) <= MASS_TOLERANCE);
                if (byId != null)
                    return byId;
            }
            return candidates.Where(d => Math.Abs(d.Mass - mass) <= MASS_TOLERANCE)
                .OrderBy(d => Math.Abs(d.Mass - mass))
                .FirstOrDefault();
        }

        private static string Site(ModificationDefinition definition)
        {
            int at = definition.Name.LastIndexOf('@');
            return at < 0 ? string.Empty : definition.Name.Substring(at + 1);
        }

        /// <summary>
        /// True when the modified sequence opens with a modification before its first residue,
        /// as DIA-NN writes <c>(UniMod:1)PEPTIDE</c> or <c>[+42.0106]PEPTIDE</c>.
        /// </summary>
        private static bool HasLeadingModification(string modifiedSequence)
        {
            if (string.IsNullOrEmpty(modifiedSequence))
                return false;
            int start = modifiedSequence[0] == '_' ? 1 : 0;
            if (start < modifiedSequence.Length && modifiedSequence[start] == 'n')
                start++;
            return start < modifiedSequence.Length && (modifiedSequence[start] == '(' || modifiedSequence[start] == '[');
        }
    }
}
