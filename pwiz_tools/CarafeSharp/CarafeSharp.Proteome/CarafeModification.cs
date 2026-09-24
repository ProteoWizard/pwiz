/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/input/CModification.java
 *   and src/main/resources/top_modifications.tsv, with masses and types from the compomics
 *   ModificationFactory of the Carafe-vendored compomics-utilities 5.0.39P2
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
using System.Globalization;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>Where a modification can sit, as compomics <c>ModificationType</c> names it.</summary>
    public enum CarafeModificationType
    {
        /// <summary>On a residue (<c>modaa</c>).</summary>
        residue,

        /// <summary>On the peptide N terminus (<c>modn_peptide</c>).</summary>
        peptide_n_term,

        /// <summary>On the protein N terminus (<c>modn_protein</c>).</summary>
        protein_n_term,

        /// <summary>On the peptide N terminus when it is a given residue (<c>modnaa_peptide</c>).</summary>
        peptide_n_term_residue,
    }

    /// <summary>
    /// One of Carafe's numbered modifications (its <c>-fixMod</c> / <c>-varMod</c> ids), with
    /// the mass compomics adds for it at run time.
    /// </summary>
    public sealed class CarafeModification
    {
        /// <summary>
        /// Carafe's top modifications, ids 1 to 27, which are also every modification its GUI
        /// offers. Higher ids come from the full compomics and Unimod lists and are not ported.
        /// </summary>
        private static readonly CarafeModification[] TOP_MODIFICATIONS =
        {
            new CarafeModification(1, @"Carbamidomethyl of C", CarafeModificationType.residue, 'C', 57.02146372057,
                @"57.02146372057", 4, @"Carbamidomethyl@C"),
            new CarafeModification(2, @"Oxidation of M", CarafeModificationType.residue, 'M', 15.99491461956,
                @"15.99491461956", 35, @"Oxidation@M"),
            new CarafeModification(3, @"Deamidated of N", CarafeModificationType.residue, 'N', 0.984016,
                @"0.9840155826899988", 7, @"Deamidated@N"),
            new CarafeModification(4, @"Deamidated of Q", CarafeModificationType.residue, 'Q', 0.984016,
                @"0.9840155826899988", 7, @"Deamidated@Q"),
            new CarafeModification(5, @"Acetyl of protein N-term", CarafeModificationType.protein_n_term, null, 42.010565,
                @"42.0105646837", 1, @"Acetyl@Protein_N-term"),
            new CarafeModification(6, @"Acetyl of K", CarafeModificationType.residue, 'K', 42.010565,
                @"42.0105646837", 1, @"Acetyl@K"),
            new CarafeModification(7, @"Phospho of S", CarafeModificationType.residue, 'S', 79.96633052074999,
                @"79.96633052074999", 21, @"Phospho@S"),
            new CarafeModification(8, @"Phospho of T", CarafeModificationType.residue, 'T', 79.96633052074999,
                @"79.96633052074999", 21, @"Phospho@T"),
            new CarafeModification(9, @"Phospho of Y", CarafeModificationType.residue, 'Y', 79.96633052074999,
                @"79.96633052074999", 21, @"Phospho@Y"),
            new CarafeModification(10, @"GG of K", CarafeModificationType.residue, 'K', 114.042927,
                @"114.04292744114", 121, @"GG@K"),
            new CarafeModification(11, @"TMT 10-plex of K", CarafeModificationType.residue, 'K', 229.16293213472,
                @"229.16293213472", 737, null),
            new CarafeModification(12, @"TMT 10-plex of peptide N-term", CarafeModificationType.peptide_n_term, null, 229.16293213472,
                @"229.16293213472", 737, null),
            new CarafeModification(13, @"TMT 11-plex of K", CarafeModificationType.residue, 'K', 229.16293213472,
                @"229.16293213472", 737, null),
            new CarafeModification(14, @"TMT 11-plex of peptide N-term", CarafeModificationType.peptide_n_term, null, 229.16293213472,
                @"229.16293213472", 737, null),
            new CarafeModification(15, @"TMT 6-plex of K", CarafeModificationType.residue, 'K', 229.16293213472,
                @"229.16293213472", 737, null),
            new CarafeModification(16, @"TMT 6-plex of peptide N-term", CarafeModificationType.peptide_n_term, null, 229.16293213472,
                @"229.16293213472", 737, null),
            new CarafeModification(17, @"TMT 2-plex of K", CarafeModificationType.residue, 'K', 225.15583272792,
                @"225.15583272792", 738, null),
            new CarafeModification(18, @"TMT 2-plex of peptide N-term", CarafeModificationType.peptide_n_term, null, 225.15583272792,
                @"225.15583272792", 738, null),
            new CarafeModification(19, @"TMTpro of K", CarafeModificationType.residue, 'K', 304.20714532623,
                @"304.20714532623", 2016, null),
            new CarafeModification(20, @"TMTpro of peptide N-term", CarafeModificationType.peptide_n_term, null, 304.20714532623,
                @"304.20714532623", 2016, null),
            new CarafeModification(21, @"iTRAQ 4-plex of K", CarafeModificationType.residue, 'K', 144.1020624208,
                @"144.1020624208", 214, null),
            new CarafeModification(22, @"iTRAQ 4-plex of peptide N-term", CarafeModificationType.peptide_n_term, null, 144.1020624208,
                @"144.1020624208", 214, null),
            new CarafeModification(23, @"iTRAQ 4-plex of Y", CarafeModificationType.residue, 'Y', 144.1020624208,
                @"144.1020624208", 214, null),
            new CarafeModification(24, @"iTRAQ 8-plex of K", CarafeModificationType.residue, 'K', 304.19903946116,
                @"304.19903946116", 730, null),
            new CarafeModification(25, @"iTRAQ 8-plex of peptide N-term", CarafeModificationType.peptide_n_term, null, 304.19903946116,
                @"304.19903946116", 730, null),
            new CarafeModification(26, @"iTRAQ 8-plex of Y", CarafeModificationType.residue, 'Y', 304.19903946116,
                @"304.19903946116", 730, null),
            new CarafeModification(27, @"Glu->pyro-Glu of E", CarafeModificationType.peptide_n_term_residue, 'E', -18.010565,
                @"-18.0105646837", 27, null),
        };

        /// <summary>The modifications Carafe numbers 1 to 27.</summary>
        public static IReadOnlyList<CarafeModification> TopModifications
        {
            get { return TOP_MODIFICATIONS; }
        }

        /// <summary>The modification with a Carafe id; ids above 27 are not supported.</summary>
        public static CarafeModification GetById(int id)
        {
            if (id < 1 || id > TOP_MODIFICATIONS.Length)
            {
                throw new NotSupportedException(string.Format(
                    @"Carafe modification {0} is not supported; CarafeSharp ports ids 1 to {1}", id, TOP_MODIFICATIONS.Length));
            }
            return TOP_MODIFICATIONS[id - 1];
        }

        private CarafeModification(int id, string name, CarafeModificationType type, char? target, double mass,
            string preferredMassText, int unimodAccession, string alphabaseName)
        {
            Id = id;
            Name = name;
            Type = type;
            Target = target;
            Mass = mass;
            PreferredMassText = preferredMassText;
            PreferredMass = decimal.Parse(preferredMassText, NumberStyles.Float, CultureInfo.InvariantCulture);
            UnimodAccession = unimodAccession;
            AlphabaseName = alphabaseName;
        }

        public int Id { get; }
        public string Name { get; }
        public CarafeModificationType Type { get; }

        /// <summary>The residue a residue or N-terminal-residue modification needs, else null.</summary>
        public char? Target { get; }

        /// <summary>The monoisotopic mass shift compomics adds.</summary>
        public double Mass { get; }

        /// <summary>
        /// The mass <c>top_modifications.tsv</c> lists, exactly as written: Carafe's Skyline
        /// .blib uses it, as a BigDecimal, for the peptideModSeq mass shifts and the
        /// Modifications table.
        /// </summary>
        public string PreferredMassText { get; }

        /// <summary><see cref="PreferredMassText"/> as an exact decimal.</summary>
        public decimal PreferredMass { get; }

        /// <summary>The Unimod record id (<c>UniMod:4</c> is 4).</summary>
        public int UnimodAccession { get; }

        /// <summary>
        /// The alphabase name Carafe's library prediction passes to the models
        /// (<c>AIGear.mod_map</c>: the Unimod title, <c>@</c>, and the residue or
        /// <c>Protein_N-term</c>), or null when Carafe has no usable name for it: the TMT and
        /// iTRAQ names are compomics's rather than Unimod's, so Carafe stops with
        /// "Unrecognized modification", and <c>Glu-&gt;pyro-Glu@E</c> is not an alphabase name.
        /// </summary>
        public string AlphabaseName { get; }

        /// <summary>
        /// The Unimod title, the <c>psi_ms_name</c> Carafe's EncyclopeDIA and generic notations
        /// print; null where <see cref="AlphabaseName"/> is.
        /// </summary>
        public string UnimodTitle
        {
            get { return AlphabaseName?.Substring(0, AlphabaseName.IndexOf('@')); }
        }

        /// <summary>
        /// compomics <c>ModificationUtils.getPossibleModificationSites</c> on a peptide with no
        /// protein context: 1-based residue positions, or 0 for the N terminus. Matching treats
        /// I and L as indistinguishable, as compomics's default matching does. A protein N-term
        /// modification has no site here; Carafe places a variable one itself.
        /// </summary>
        public IEnumerable<int> GetPossibleSites(string sequence)
        {
            switch (Type)
            {
                case CarafeModificationType.residue:
                    for (int i = 0; i < sequence.Length; i++)
                    {
                        if (MatchesTarget(sequence[i]))
                            yield return i + 1;
                    }
                    break;
                case CarafeModificationType.peptide_n_term:
                    yield return 0;
                    break;
                case CarafeModificationType.peptide_n_term_residue:
                    if (MatchesTarget(sequence[0]))
                        yield return 0;
                    break;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        private bool MatchesTarget(char aa)
        {
            return Target.HasValue && (aa == Target.Value || (IsIsobaricLeucine(aa) && IsIsobaricLeucine(Target.Value)));
        }

        private static bool IsIsobaricLeucine(char aa)
        {
            return aa == 'I' || aa == 'L';
        }
    }
}
