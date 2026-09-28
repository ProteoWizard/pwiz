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

using System.Collections.Generic;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// b and y fragment m/z from a stripped sequence plus per-residue modification masses.
    /// Moved out of <c>DecoyGenerator</c> unchanged, so decoy fragments and anything that
    /// checks a library's fragment annotations share one set of residue masses and one
    /// order of floating-point additions.
    /// </summary>
    public static class PeptideFragmentMass
    {
        public const double PROTON_MASS = 1.007276;
        public const double H2O_MASS = 18.010565;

        private static readonly Dictionary<char, double> STANDARD_AA_MASSES = new Dictionary<char, double>
        {
            { 'A', 71.037114 }, { 'R', 156.101111 }, { 'N', 114.042927 },
            { 'D', 115.026943 }, { 'C', 103.009185 }, { 'E', 129.042593 },
            { 'Q', 128.058578 }, { 'G', 57.021464 }, { 'H', 137.058912 },
            { 'I', 113.084064 }, { 'L', 113.084064 }, { 'K', 128.094963 },
            { 'M', 131.040485 }, { 'F', 147.068414 }, { 'P', 97.052764 },
            { 'S', 87.032028 }, { 'T', 101.047679 }, { 'W', 186.079313 },
            { 'Y', 163.063329 }, { 'V', 99.068414 }
        };

        /// <summary>
        /// Monoisotopic residue mass for one of the 20 standard amino acids. False for
        /// selenocysteine and the ambiguity codes (B, J, O, U, X, Z).
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
        /// <paramref name="charge"/>, or null for another ion type, an ordinal past the end of
        /// the sequence, or an ion spanning a residue with no standard mass.
        /// </summary>
        public static double? CalculateFragmentMz(
            IonType ionType, int ordinal, byte charge,
            string sequence, IReadOnlyDictionary<int, double> modMasses,
            double? neutralLoss)
        {
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
