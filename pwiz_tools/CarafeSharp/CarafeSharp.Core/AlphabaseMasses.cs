/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on alphabase (https://github.com/MannLabs/alphabase), Apache-2.0
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

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// The mass constants alphabase 1.2.1 uses to compute precursor and fragment m/z for the
    /// AlphaPeptDeep predictions Carafe writes into its libraries. Element masses are the most
    /// abundant isotope from alphabase's <c>nist_element.yaml</c>; residue masses are computed
    /// from the formulas in its <c>amino_acid.yaml</c>, so they carry the same float64
    /// rounding alphabase does.
    /// </summary>
    public static class AlphabaseMasses
    {
        public const double PROTON = 1.007276467;

        /// <summary>
        /// Most-abundant-isotope masses for every element that appears in alphabase's
        /// modification table and residue formulas.
        /// </summary>
        private static readonly Dictionary<string, double> ELEMENT_MASSES = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            { @"13C", 13.00335483507 },
            { @"15N", 15.00010889888 },
            { @"18O", 17.99915961286 },
            { @"2H", 2.01410177812 },
            { @"Ag", 106.9050916 },
            { @"Al", 26.98153853 },
            { @"As", 74.92159457 },
            { @"B", 11.00930536 },
            { @"Br", 78.9183376 },
            { @"C", 12.0 },
            { @"Ca", 39.962590863 },
            { @"Cl", 34.968852682 },
            { @"Cu", 62.92959772 },
            { @"F", 18.99840316273 },
            { @"Fe", 55.93493633 },
            { @"H", 1.00782503223 },
            { @"Hg", 201.9706434 },
            { @"I", 126.9044719 },
            { @"K", 38.9637064864 },
            { @"Li", 7.0160034366 },
            { @"Mg", 23.985041697 },
            { @"Mo", 97.90540482 },
            { @"N", 14.00307400443 },
            { @"Na", 22.989769282 },
            { @"Ni", 57.93534241 },
            { @"O", 15.99491461957 },
            { @"P", 30.97376199842 },
            { @"S", 31.9720711744 },
            { @"Se", 79.9165218 },
            { @"Zn", 63.92914201 },
        };

        /// <summary>Residue formulas from alphabase <c>amino_acid.yaml</c>, upper case only.</summary>
        private static readonly Dictionary<char, string> RESIDUE_FORMULAS = new Dictionary<char, string>
        {
            { 'A', @"C(3)H(5)N(1)O(1)S(0)" },
            { 'B', @"C(1000000)" },
            { 'C', @"C(3)H(5)N(1)O(1)S(1)" },
            { 'D', @"C(4)H(5)N(1)O(3)S(0)" },
            { 'E', @"C(5)H(7)N(1)O(3)S(0)" },
            { 'F', @"C(9)H(9)N(1)O(1)S(0)" },
            { 'G', @"C(2)H(3)N(1)O(1)S(0)" },
            { 'H', @"C(6)H(7)N(3)O(1)S(0)" },
            { 'I', @"C(6)H(11)N(1)O(1)S(0)" },
            { 'J', @"C(6)H(11)N(1)O(1)S(0)" },
            { 'K', @"C(6)H(12)N(2)O(1)S(0)" },
            { 'L', @"C(6)H(11)N(1)O(1)S(0)" },
            { 'M', @"C(5)H(9)N(1)O(1)S(1)" },
            { 'N', @"C(4)H(6)N(2)O(2)S(0)" },
            { 'O', @"C(12)H(19)N(3)O(2)" },
            { 'P', @"C(5)H(7)N(1)O(1)S(0)" },
            { 'Q', @"C(5)H(8)N(2)O(2)S(0)" },
            { 'R', @"C(6)H(12)N(4)O(1)S(0)" },
            { 'S', @"C(3)H(5)N(1)O(2)S(0)" },
            { 'T', @"C(4)H(7)N(1)O(2)S(0)" },
            { 'U', @"C(3)H(5)N(1)O(1)Se(1)" },
            { 'V', @"C(5)H(9)N(1)O(1)S(0)" },
            { 'W', @"C(11)H(10)N(2)O(1)S(0)" },
            { 'X', @"C(1000000)" },
            { 'Y', @"C(9)H(9)N(1)O(2)S(0)" },
            { 'Z', @"C(1000000)" },
        };

        private static readonly double[] RESIDUE_MASSES = BuildResidueMasses();

        /// <summary>H2O as alphabase computes it: 2 H + O.</summary>
        public static readonly double H2O = ELEMENT_MASSES[@"H"] * 2 + ELEMENT_MASSES[@"O"];

        public static double GetElementMass(string element)
        {
            if (!ELEMENT_MASSES.TryGetValue(element, out double mass))
                throw new ArgumentException(string.Format(@"No mass for element '{0}'.", element), nameof(element));
            return mass;
        }

        /// <summary>Residue mass for an upper-case amino acid letter.</summary>
        public static double GetResidueMass(char aa)
        {
            if (aa < 'A' || aa > 'Z')
                throw new ArgumentException(string.Format(@"Unsupported amino acid '{0}'.", aa), nameof(aa));
            return RESIDUE_MASSES[aa - 'A'];
        }

        private static double[] BuildResidueMasses()
        {
            var masses = new double[26];
            foreach (var pair in RESIDUE_FORMULAS)
                masses[pair.Key - 'A'] = ChemicalFormula.Parse(pair.Value).MonoisotopicMass;
            return masses;
        }
    }
}
