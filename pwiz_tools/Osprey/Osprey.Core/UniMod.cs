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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// The UniMod modifications Osprey knows by id or recognizes by mass: one table for every
    /// library reader and writer. Masses are the monoisotopic mass deltas Skyline computes from
    /// the formulas in its <c>UniModData.cs</c>, until Osprey shares that table with Skyline.
    ///
    /// <para>A mass printed with limited precision (BiblioSpec's <c>C[+57.0]</c>) is matched as
    /// Skyline's <c>MassModification.Matches</c> does: equal when both round to the same value
    /// at the printed precision, capped at <see cref="MAX_PRECISION_TO_MATCH"/> decimals, or
    /// within <see cref="MATCH_WITHIN"/> of each other. Only a modification allowed on the
    /// residue or terminus matches, and the table's order is the preference when two do (at one
    /// decimal, Acetyl and Trimethyl are both <c>K[+42.0]</c>): Skyline's common modifications
    /// first, then the hidden ones libraries also carry.</para>
    /// </summary>
    public static class UniMod
    {
        /// <summary>The prefix of a modification named by its UniMod id: <c>UniMod:4</c>, in any case.</summary>
        public const string PREFIX = @"UniMod:";

        /// <summary>The most decimals a printed mass is compared at, as in Skyline.</summary>
        public const int MAX_PRECISION_TO_MATCH = 4;

        /// <summary>Masses this close match whatever their printed precision, as in Skyline.</summary>
        public const double MATCH_WITHIN = 1e-4;

        private static readonly UniModEntry[] ENTRIES =
        {
            // Skyline's common (not hidden) modifications
            new UniModEntry(4, @"Carbamidomethyl", 57.021464, @"C"),
            new UniModEntry(4, @"Carbamidomethyl", 57.021464, null, ModSite.NTerm),
            new UniModEntry(35, @"Oxidation", 15.994915, @"MHW"),
            new UniModEntry(1, @"Acetyl", 42.010565, @"K"),
            new UniModEntry(1, @"Acetyl", 42.010565, null, ModSite.NTerm),
            new UniModEntry(21, @"Phospho", 79.966331, @"STY"),
            new UniModEntry(40, @"Sulfo", 79.956815, @"STY"),
            new UniModEntry(7, @"Deamidated", 0.984016, @"NQR"),
            new UniModEntry(5, @"Carbamyl", 43.005814, @"K"),
            new UniModEntry(5, @"Carbamyl", 43.005814, null, ModSite.NTerm),
            new UniModEntry(26, @"Pyro-carbamidomethyl", 39.994915, @"C", ModSite.NTerm),
            new UniModEntry(27, @"Glu->pyro-Glu", -18.010565, @"E", ModSite.NTerm),
            new UniModEntry(28, @"Gln->pyro-Glu", -17.026549, @"Q", ModSite.NTerm),
            new UniModEntry(385, @"Ammonia-loss", -17.026549, @"C", ModSite.NTerm),
            new UniModEntry(34, @"Methyl", 14.015650, @"DE"),
            new UniModEntry(34, @"Methyl", 14.015650, null, ModSite.CTerm),
            new UniModEntry(122, @"Formyl", 27.994915, null, ModSite.NTerm),
            new UniModEntry(214, @"iTRAQ4plex", 144.102062, @"KY"),
            new UniModEntry(214, @"iTRAQ4plex", 144.102062, null, ModSite.NTerm),
            new UniModEntry(737, @"TMT6plex", 229.162932, @"K"),
            new UniModEntry(737, @"TMT6plex", 229.162932, null, ModSite.NTerm),
            new UniModEntry(2016, @"TMTpro", 304.207146, @"K"),
            new UniModEntry(2016, @"TMTpro", 304.207146, null, ModSite.NTerm),
            // Hidden in Skyline, but carried by libraries: SILAC labels, ubiquitination and
            // other lysine and arginine chemistry
            new UniModEntry(188, @"Label:13C(6)", 6.020129, @"KR"),
            new UniModEntry(259, @"Label:13C(6)15N(2)", 8.014199, @"K"),
            new UniModEntry(267, @"Label:13C(6)15N(4)", 10.008269, @"R"),
            new UniModEntry(121, @"GG", 114.042927, @"K"),
            new UniModEntry(34, @"Methyl", 14.015650, @"KR"),
            new UniModEntry(36, @"Dimethyl", 28.031300, @"KR"),
            new UniModEntry(36, @"Dimethyl", 28.031300, null, ModSite.NTerm),
            new UniModEntry(37, @"Trimethyl", 42.046950, @"KR"),
            new UniModEntry(312, @"Cysteinyl", 119.004099, @"C"),
            new UniModEntry(354, @"Nitro", 44.985078, @"FWY"),
            new UniModEntry(747, @"Malonyl", 86.000394, @"CKS"),
        };

        private static readonly Dictionary<int, UniModEntry> BY_ID = IndexById();

        /// <summary>The modification with UniMod id <paramref name="id"/>, or null when Osprey does not know it.</summary>
        public static UniModEntry Find(int id)
        {
            return BY_ID.TryGetValue(id, out var entry) ? entry : null;
        }

        /// <summary>
        /// The preferred known modification allowed on <paramref name="residue"/> - the first
        /// residue when <paramref name="isNTerm"/>, the last when <paramref name="isCTerm"/> -
        /// whose mass matches <paramref name="mass"/> printed with <paramref name="precision"/>
        /// decimals, or null.
        /// </summary>
        public static UniModEntry Match(double mass, int precision, char residue, bool isNTerm, bool isCTerm)
        {
            foreach (var entry in ENTRIES)
            {
                if (entry.AllowedOn(residue, isNTerm, isCTerm) && MassMatches(entry.Mass, mass, precision))
                    return entry;
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

        private static Dictionary<int, UniModEntry> IndexById()
        {
            var byId = new Dictionary<int, UniModEntry>();
            foreach (var entry in ENTRIES)
            {
                if (!byId.ContainsKey(entry.Id))
                    byId.Add(entry.Id, entry);
            }
            return byId;
        }
    }

    /// <summary>Where on a peptide a <see cref="UniModEntry"/> is allowed besides its residues.</summary>
    public enum ModSite
    {
        /// <summary>On its residues, anywhere in the peptide.</summary>
        Residue,
        /// <summary>On the peptide N-terminus: on the first residue when it has residues.</summary>
        NTerm,
        /// <summary>On the peptide C-terminus: on the last residue when it has residues.</summary>
        CTerm
    }

    /// <summary>One UniMod modification with one specificity, as Skyline lists them.</summary>
    public sealed class UniModEntry
    {
        public UniModEntry(int id, string name, double mass, string residues, ModSite site = ModSite.Residue)
        {
            Id = id;
            Name = name;
            Mass = mass;
            Residues = residues;
            Site = site;
        }

        public int Id { get; }
        public string Name { get; }

        /// <summary>The monoisotopic mass delta.</summary>
        public double Mass { get; }

        /// <summary>The residues it modifies, or null for any residue at its terminus.</summary>
        public string Residues { get; }

        public ModSite Site { get; }

        /// <summary>Whether it is allowed on <paramref name="residue"/> at that place in a peptide.</summary>
        public bool AllowedOn(char residue, bool isNTerm, bool isCTerm)
        {
            if (Site == ModSite.NTerm && !isNTerm || Site == ModSite.CTerm && !isCTerm)
                return false;
            return Residues == null || Residues.IndexOf(residue) >= 0;
        }
    }
}
