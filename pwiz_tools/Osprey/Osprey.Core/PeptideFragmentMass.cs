/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using pwiz.Common.Chemistry;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// b and y fragment m/z from a stripped sequence plus per-residue modification masses.
    /// Moved out of <c>DecoyGenerator</c> with its arithmetic unchanged, so decoy fragments and
    /// anything else that needs a peptide's fragment m/z share one set of residue masses
    /// and one order of floating-point additions. The one addition is that an ordinal the
    /// sequence cannot hold returns null instead of indexing past either end.
    ///
    /// <para>The residue masses and the water are those of <see cref="pwiz.Common.Chemistry"/>:
    /// each residue's formula (<see cref="AminoAcidFormulas"/>) summed over the isotope table
    /// Skyline calculates with (<see cref="IsotopeAbundances.Default"/>). Until 2026-10-16 this
    /// was a table of the same values rounded to six decimals, ported from osprey-core; the
    /// tools that compute peptide and fragment masses must agree to the last digit, so now it
    /// is the calculation. The proton is the one Skyline's own m/z use today
    /// (<c>BioMassCalc.MassProton</c>, 1.007276), not yet the higher-precision
    /// <see cref="AminoAcidFormulas.ProtonMass"/> its isotope distributions use; moving both
    /// tools to that constant is a sprint of its own.</para>
    /// </summary>
    public static class PeptideFragmentMass
    {
        public const double PROTON_MASS = 1.007276;
        public static readonly double H2O_MASS = NeutralLoss.H2OMass;

        /// <summary>
        /// The 20 standard amino acids and selenocysteine (U, C3H5NOSe: Carafe predicts spectra
        /// for it, so a library carries it, and without a mass its fragments could not be typed
        /// and its decoys had no fragments).
        /// </summary>
        private const string RESIDUES = @"ACDEFGHIKLMNPQRSTVWYU";

        private static readonly Dictionary<char, double> STANDARD_AA_MASSES = ResidueMasses();

        private static Dictionary<char, double> ResidueMasses()
        {
            var formulas = AminoAcidFormulas.Default;
            // A peptide's mass is its residues' plus water, so one residue's is that of the
            // one-residue peptide less the water of the empty one.
            double water = formulas.GetMonoisotopicMass(string.Empty);
            var masses = new Dictionary<char, double>(RESIDUES.Length);
            foreach (char aa in RESIDUES)
                masses.Add(aa, formulas.GetMonoisotopicMass(aa.ToString()) - water);
            return masses;
        }

        /// <summary>
        /// Monoisotopic residue mass for one of the 20 standard amino acids or selenocysteine.
        /// False for the ambiguity codes (B, J, O, X, Z).
        /// </summary>
        public static bool TryGetResidueMass(char aa, out double mass)
        {
            return STANDARD_AA_MASSES.TryGetValue(aa, out mass);
        }

        /// <summary>
        /// Modification mass by 0-based residue position, the map
        /// <see cref="CalculateFragmentMz"/> takes. Modifications at the same position add:
        /// both library loaders put an N-terminal modification on residue 0, where a
        /// modification of the first residue also sits (<c>(UniMod:1)M(UniMod:35)</c>,
        /// <c>[+42.0]M[+16.0]</c>), and every ion spanning that residue carries both.
        /// </summary>
        public static Dictionary<int, double> ModMassesByPosition(IEnumerable<Modification> modifications)
        {
            var modMasses = new Dictionary<int, double>();
            if (modifications == null)
                return modMasses;
            foreach (var m in modifications)
            {
                modMasses.TryGetValue(m.Position, out double mass);
                modMasses[m.Position] = mass + m.MassDelta;
            }
            return modMasses;
        }

        /// <summary>
        /// m/z of the b or y ion of <paramref name="ordinal"/> residues at
        /// <paramref name="charge"/>, or null for another ion type, an ordinal below 1 or past
        /// the end of the sequence, or an ion spanning a residue with no standard mass. A charge
        /// of 0 throws: the library readers refuse one, so reaching here with it is a defect,
        /// and any m/z returned for it would be bogus.
        /// </summary>
        public static double? CalculateFragmentMz(
            IonType ionType, int ordinal, byte charge,
            string sequence, IReadOnlyDictionary<int, double> modMasses,
            double? neutralLoss)
        {
            if (charge == 0)
                throw new ArgumentOutOfRangeException(nameof(charge), $@"Fragment charge 0 for {ionType}{ordinal} of {sequence}");
            if (ordinal < 1)
                return null;
            int seqLen = sequence.Length;
            int start, end;

            switch (ionType)
            {
                case IonType.B:
                    start = 0;
                    end = ordinal;
                    break;
                case IonType.Y:
                    start = seqLen - ordinal;
                    end = seqLen;
                    break;
                default:
                    return null;
            }

            // A b ion past the end runs off the C-terminus; a y ion past it off the N-terminus.
            if (end > seqLen || start < 0)
                return null;

            double mass = 0.0;
            for (int i = start; i < end; i++)
            {
                double aaMass;
                if (!STANDARD_AA_MASSES.TryGetValue(sequence[i], out aaMass))
                    return null;
                mass += aaMass;

                double modMass;
                if (modMasses.TryGetValue(i, out modMass))
                    mass += modMass;
            }

            switch (ionType)
            {
                case IonType.B:
                    mass += PROTON_MASS;
                    break;
                case IonType.Y:
                    mass += H2O_MASS + PROTON_MASS;
                    break;
            }

            if (neutralLoss.HasValue)
                mass -= neutralLoss.Value;

            double mz = (mass + (charge - 1.0) * PROTON_MASS) / charge;
            return mz;
        }
    }
}
