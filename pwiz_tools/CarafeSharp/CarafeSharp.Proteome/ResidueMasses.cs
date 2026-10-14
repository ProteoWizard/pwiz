/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/EntrapmentFastaGear.java
 *   (AA_MONO_MASS, peptideNeutralMass, fitsMzRange)
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// The five-decimal monoisotopic residue masses Carafe's entrapment generator uses for its
    /// unknown-residue check, its unmodified m/z window, foreign-peptide mass matching and the
    /// similarity gate's fragment ladders. Only the 20 standard residues have a mass: a peptide
    /// with B, J, O, U, X or Z has none.
    /// </summary>
    public static class ResidueMasses
    {
        public const double H2O_MONO = 18.01056;
        public const double PROTON_MONO = 1.007276;

        private static readonly double[] MASSES = BuildMasses();

        /// <summary>The residue mass of an upper-case standard residue, or NaN for anything else.</summary>
        public static double Get(char aa)
        {
            return aa >= 'A' && aa <= 'Z' ? MASSES[aa - 'A'] : double.NaN;
        }

        /// <summary>
        /// Monoisotopic neutral mass (residues plus water, summed in sequence order), or null
        /// when any residue has no mass.
        /// </summary>
        public static double? PeptideNeutralMass(string sequence)
        {
            double total = H2O_MONO;
            foreach (char aa in sequence)
            {
                double mass = Get(aa);
                if (double.IsNaN(mass))
                    return null;
                total += mass;
            }
            return total;
        }

        /// <summary>True when at least one charge puts the neutral mass inside [minMz, maxMz].</summary>
        public static bool FitsMzRange(double neutralMass, int[] charges, double minMz, double maxMz)
        {
            foreach (int z in charges)
            {
                double mz = (neutralMass + z * PROTON_MONO) / z;
                if (minMz <= mz && mz <= maxMz)
                    return true;
            }
            return false;
        }

        private static double[] BuildMasses()
        {
            var masses = new double[26];
            for (int i = 0; i < masses.Length; i++)
                masses[i] = double.NaN;
            Set(masses, 'G', 57.02146);
            Set(masses, 'A', 71.03711);
            Set(masses, 'S', 87.03203);
            Set(masses, 'P', 97.05276);
            Set(masses, 'V', 99.06841);
            Set(masses, 'T', 101.04768);
            Set(masses, 'C', 103.00919);
            Set(masses, 'L', 113.08406);
            Set(masses, 'I', 113.08406);
            Set(masses, 'N', 114.04293);
            Set(masses, 'D', 115.02694);
            Set(masses, 'Q', 128.05858);
            Set(masses, 'K', 128.09496);
            Set(masses, 'E', 129.04259);
            Set(masses, 'M', 131.04049);
            Set(masses, 'H', 137.05891);
            Set(masses, 'F', 147.06841);
            Set(masses, 'R', 156.10111);
            Set(masses, 'Y', 163.06333);
            Set(masses, 'W', 186.07931);
            return masses;
        }

        private static void Set(double[] masses, char aa, double mass)
        {
            masses[aa - 'A'] = mass;
        }
    }
}
