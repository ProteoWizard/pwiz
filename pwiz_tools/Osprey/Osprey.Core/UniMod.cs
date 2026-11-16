/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using Pwiz.Data.Common.Unimod;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// The UniMod modifications every Osprey library reader and writer resolves modification
    /// text against. Ids, names and monoisotopic masses all come from <c>unimod.obo</c>, read by
    /// ProteoWizard's <see cref="Unimod"/> (<c>pwiz/data/common</c>), the one copy of UniMod in
    /// the repository the other tools read too. Osprey keeps no masses of its own: a hand-typed
    /// table here once gave six ids another modification's mass.
    ///
    /// <para>A UniMod id (<c>UniMod:21</c>) or title (<c>Phospho</c>) may name any modification
    /// in the file. A mass printed with limited precision (BiblioSpec's <c>C[+57.0]</c>) is
    /// matched only against the modifications and specificities Skyline lists, as Skyline's
    /// <c>MassModification.Matches</c> does: equal when both round to the same value at the
    /// printed precision, capped at <see cref="MAX_PRECISION_TO_MATCH"/> decimals, or within
    /// <see cref="MATCH_WITHIN"/> of each other. Only a modification allowed on the residue or
    /// terminus matches, and the list's order is the preference when two do (at one decimal,
    /// Acetyl and Trimethyl are both <c>K[+42.0]</c>): Skyline's common modifications first, then
    /// the hidden ones libraries also carry. Matching against all of UniMod instead would make a
    /// one-decimal mass ambiguous among dozens of entries.</para>
    /// </summary>
    public static class UniMod
    {
        /// <summary>The prefix of a modification named by its UniMod id: <c>UniMod:4</c>, in any case.</summary>
        public const string PREFIX = @"UniMod:";

        /// <summary>The most decimals a printed mass is compared at, as in Skyline.</summary>
        public const int MAX_PRECISION_TO_MATCH = 4;

        /// <summary>Masses this close match whatever their printed precision, as in Skyline.</summary>
        public const double MATCH_WITHIN = 1e-4;

        // Every modification in unimod.obo, by id and by title. Before MATCHED, which reads them.
        private static readonly Dictionary<int, UniModEntry> BY_ID = new Dictionary<int, UniModEntry>();
        private static readonly Dictionary<string, UniModEntry> BY_NAME = IndexAll();

        // Names some libraries use for a modification that are not its UniMod title.
        private static readonly Dictionary<string, string> TITLE_BY_ALIAS =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { @"CAM", @"Carbamidomethyl" },
                { @"Deamidation", @"Deamidated" },
            };

        // The specificities a printed mass is matched against, in order of preference. Only ids
        // and sites: the names and masses are unimod.obo's.
        private static readonly Specificity[] MATCHED =
        {
            // Skyline's common (not hidden) modifications
            new Specificity(4, @"C"),
            new Specificity(4, null, ModSite.NTerm),
            new Specificity(35, @"MHW"),
            new Specificity(1, @"K"),
            new Specificity(1, null, ModSite.NTerm),
            new Specificity(21, @"STY"),
            new Specificity(40, @"STY"),
            new Specificity(7, @"NQR"),
            new Specificity(5, @"K"),
            new Specificity(5, null, ModSite.NTerm),
            new Specificity(26, @"C", ModSite.NTerm),
            new Specificity(27, @"E", ModSite.NTerm),
            new Specificity(28, @"Q", ModSite.NTerm),
            new Specificity(385, @"C", ModSite.NTerm),
            new Specificity(34, @"DE"),
            new Specificity(34, null, ModSite.CTerm),
            new Specificity(122, null, ModSite.NTerm),
            new Specificity(214, @"KY"),
            new Specificity(214, null, ModSite.NTerm),
            new Specificity(737, @"K"),
            new Specificity(737, null, ModSite.NTerm),
            new Specificity(2016, @"K"),
            new Specificity(2016, null, ModSite.NTerm),
            // Hidden in Skyline, but carried by libraries: SILAC labels, ubiquitination and
            // other lysine and arginine chemistry
            new Specificity(188, @"KR"),
            new Specificity(259, @"K"),
            new Specificity(267, @"R"),
            new Specificity(121, @"K"),
            new Specificity(34, @"KR"),
            new Specificity(36, @"KR"),
            new Specificity(36, null, ModSite.NTerm),
            new Specificity(37, @"KR"),
            new Specificity(312, @"C"),
            new Specificity(354, @"FWY"),
            new Specificity(747, @"CKS"),
        };

        /// <summary>The modification with UniMod id <paramref name="id"/>, or null when UniMod has none.</summary>
        public static UniModEntry Find(int id)
        {
            return BY_ID.TryGetValue(id, out var entry) ? entry : null;
        }

        /// <summary>
        /// The modification titled <paramref name="name"/> in UniMod (<c>Oxidation</c>), in any
        /// case, or under one of the few other names libraries use (<c>CAM</c>); null when none is.
        /// </summary>
        public static UniModEntry FindByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (TITLE_BY_ALIAS.TryGetValue(name, out var title))
                name = title;
            return BY_NAME.TryGetValue(name, out var entry) ? entry : null;
        }

        /// <summary>
        /// The preferred modification Skyline lists allowed on <paramref name="residue"/> - the
        /// first residue when <paramref name="isNTerm"/>, the last when <paramref name="isCTerm"/> -
        /// whose mass matches <paramref name="mass"/> printed with <paramref name="precision"/>
        /// decimals, or null.
        /// </summary>
        public static UniModEntry Match(double mass, int precision, char residue, bool isNTerm, bool isCTerm)
        {
            foreach (var specificity in MATCHED)
            {
                if (specificity.AllowedOn(residue, isNTerm, isCTerm) && MassMatches(specificity.Entry.Mass, mass, precision))
                    return specificity.Entry;
            }
            return null;
        }

        /// <summary>
        /// Whether a known <paramref name="knownMass"/> matches <paramref name="mass"/> printed
        /// with <paramref name="precision"/> decimals (Skyline's <c>MassModification.Matches</c>).
        /// </summary>
        public static bool MassMatches(double knownMass, double mass, int precision)
        {
            int digits = Math.Min(precision, MAX_PRECISION_TO_MATCH);
            if (Math.Round(knownMass, digits) == Math.Round(mass, digits))
                return true;
            return Math.Abs(knownMass - mass) < MATCH_WITHIN;
        }

        /// <summary>
        /// Fills <see cref="BY_ID"/> from the modifications in unimod.obo and returns the same
        /// entries by title. A title two entries share in different case is kept for the first.
        /// </summary>
        private static Dictionary<string, UniModEntry> IndexAll()
        {
            var byName = new Dictionary<string, UniModEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var modification in Unimod.Modifications)
            {
                // The ontology root (UNIMOD:0) is a term with no composition, not a modification.
                if (string.IsNullOrEmpty(modification.DeltaComposition))
                    continue;
                var entry = new UniModEntry(modification.Id, modification.Name, modification.DeltaMonoisotopicMass);
                BY_ID.Add(entry.Id, entry);
                byName.TryAdd(entry.Name, entry);
            }
            return byName;
        }

        /// <summary>Where on a peptide a <see cref="Specificity"/> is allowed besides its residues.</summary>
        private enum ModSite
        {
            /// <summary>On its residues, anywhere in the peptide.</summary>
            Residue,
            /// <summary>On the peptide N-terminus: on the first residue when it has residues.</summary>
            NTerm,
            /// <summary>On the peptide C-terminus: on the last residue when it has residues.</summary>
            CTerm
        }

        /// <summary>One UniMod modification with one specificity, as Skyline lists them.</summary>
        private sealed class Specificity
        {
            public Specificity(int id, string residues, ModSite site = ModSite.Residue)
            {
                Entry = Find(id) ?? throw new InvalidOperationException($@"UniMod:{id} is not in unimod.obo");
                Residues = residues;
                Site = site;
            }

            public UniModEntry Entry { get; }

            /// <summary>The residues it modifies, or null for any residue at its terminus.</summary>
            private string Residues { get; }

            private ModSite Site { get; }

            /// <summary>Whether it is allowed on <paramref name="residue"/> at that place in a peptide.</summary>
            public bool AllowedOn(char residue, bool isNTerm, bool isCTerm)
            {
                if (Site == ModSite.NTerm && !isNTerm || Site == ModSite.CTerm && !isCTerm)
                    return false;
                return Residues == null || Residues.IndexOf(residue) >= 0;
            }
        }
    }

    /// <summary>A UniMod modification: its id, title and monoisotopic mass delta, as unimod.obo gives them.</summary>
    public sealed class UniModEntry
    {
        public UniModEntry(int id, string name, double mass)
        {
            Id = id;
            Name = name;
            Mass = mass;
        }

        /// <summary>The UniMod accession number (4 for <c>UniMod:4</c>).</summary>
        public int Id { get; }

        /// <summary>The UniMod title (<c>Carbamidomethyl</c>).</summary>
        public string Name { get; }

        /// <summary>The monoisotopic mass delta.</summary>
        public double Mass { get; }
    }
}
