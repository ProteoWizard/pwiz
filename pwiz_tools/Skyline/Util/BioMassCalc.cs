/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Text.RegularExpressions;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Enum used to specify the use of monoisotopic or average
    /// masses when calculating molecular masses.
    /// </summary>
    public enum MassType
    {
// ReSharper disable InconsistentNaming
        Monoisotopic, Average
// ReSharper restore InconsistentNaming
    }

    /// <summary>
    /// Calculates molecular masses based on atomic masses.
    /// Atomic masses come from http://www.unimod.org/unimod_help.html.
    /// Only supports the atoms H, O, N, C, S and P, and at most
    /// 2-digit atomic counts.
    /// </summary>
    public class BioMassCalc
    {
        public static readonly BioMassCalc MONOISOTOPIC = new BioMassCalc(MassType.Monoisotopic);
        public static readonly BioMassCalc AVERAGE = new BioMassCalc(MassType.Average);

        public const string H = "H";    // Hydrogen
        public const string H2 = "H'";  // Deuterium
        public const string C = "C";    // Carbon
        public const string C13 = "C'"; // Carbon13
        public const string N = "N";    // Nitrogen
        public const string N15 = "N'"; // Nitrogen15
        public const string O = "O";    // Oxygen
        public const string O18 = "O'"; // Oxygen18
        public const string P = "P";    // Phosphorus
        public const string S = "S";    // Sulfur
// ReSharper disable InconsistentNaming
        public const string Se = "Se";  // Selenium
        public const string Li = "Li";  // Lithium
        public const string F = "F";    // Fluorine
        public const string Na = "Na";  // Sodium
        public const string Cl = "Cl";  // Chlorine
        public const string K = "K";    // Potassium
        public const string Ca = "Ca";  // Calcium
        public const string Fe = "Fe";  // Iron
        public const string Ni = "Ni";  // Nickle
        public const string Cu = "Cu";  // Copper
        public const string Zn = "Zn";  // Zinc
        public const string Br = "Br";  // Bromine
        public const string Mo = "Mo";  // Molybdenum
        public const string Ag = "Ag";  // Silver
        public const string I = "I";    // Iodine
        public const string Au = "Au";  // Gold
        public const string Hg = "Hg";  // Mercury
        public const string B = "B";    // Boron
        public const string As = "As";  // Arsenic
        public const string Cd = "Cd";  // Cadmium
        public const string Cr = "Cr";  // Chromium
        public const string Co = "Co";  // Cobalt
        public const string Mn = "Mn";  // Manganese
        public const string Mg = "Mg";  // Magnesium
// ReSharper restore InconsistentNaming

        public static double MassProton
        {
            get { return 1.007276; }
        }

        /// <summary>
        /// Regular expression for possible characters that end an atomic
        /// symbol: capital letters, numbers or a space.
        /// </summary>
        private static readonly Regex REGEX_END_SYM = new Regex(@"[A-Z0-9 ]");

        /// <summary>
        /// Find the first atomic symbol in a given expression.
        /// </summary>
        /// <param name="expression">The expression to search</param>
        /// <returns>The first atomic symbol</returns>
        private static string NextSymbol(string expression)
        {
            // Skip the first character, since it is always the start of
            // the symbol, and then look for the end of the symbol.
            Match match = REGEX_END_SYM.Match(expression, 1);
            if (!match.Success)
                return expression;
            return expression.Substring(0, match.Index);
        }

        private readonly Dictionary<string, double> _atomicMasses =
            new Dictionary<string, double>();

        /// <summary>
        /// Create a simple mass calculator for use in calculating
        /// protein, peptide and fragment masses.
        /// </summary>
        /// <param name="type">Monoisotopic or average mass calculations</param>
        public BioMassCalc(MassType type)
        {
            MassType = type;
            addMass(H, 1.007825035, 1.00794);
            addMass(H2, 2.01355321270, 2.01355321270);
            addMass(O, 15.99491463, 15.9994);
            addMass(O18, 17.9991604, 17.9991604);
            addMass(N, 14.003074, 14.0067);
            addMass(N15, 15.0001088984, 15.0001088984);
            addMass(C, 12.0, 12.01085);
            addMass(C13, 13.0033548378, 13.0033548378);
            addMass(S, 31.9720707, 32.065);
            addMass(P, 30.97376151, 30.973761);

            addMass(Se, 73.9224766, 78.96);
            addMass(Li, 7.016003, 6.941);
            addMass(F, 18.99840322, 18.9984032);
            addMass(Na, 22.9897677, 22.98977);
            addMass(P, 30.973762, 30.973761);
            addMass(S, 31.9720707, 32.065);
            addMass(Cl, 34.96885272, 35.453);
            addMass(K, 38.9637074, 39.0983);
            addMass(Ca, 39.9625906, 40.078);
            addMass(Fe, 55.9349393, 55.845);
            addMass(Ni, 57.9353462, 58.6934);
            addMass(Cu, 62.9295989, 63.546);
            addMass(Zn, 63.9291448, 65.409);
            addMass(Br, 78.9183361, 79.904);
            addMass(Mo, 97.9054073, 95.94);
            addMass(Ag, 106.905092, 107.8682);
            addMass(I, 126.904473, 126.90447);
            addMass(Au, 196.966543, 196.96655);
            addMass(Hg, 201.970617, 200.59);
            addMass(B, 11.0093055, 10.811);
            addMass(As, 74.9215942, 74.9215942);
            addMass(Cd, 113.903357, 112.411);
            addMass(Cr, 51.9405098, 51.9961);
            addMass(Co, 58.9331976, 58.933195);
            addMass(Mn, 54.9380471, 54.938045);
            addMass(Mg, 23.9850423, 24.305);
        }

        public MassType MassType { get; private set; }

        /// <summary>
        /// Calculate the mass of a molecule specified as a character
        /// string like "C6H11ON", or "[{atom}[count][spaces]]*", where the
        /// atoms are H, O, N, C, S or P.
        /// </summary>
        /// <param name="desc">The molecule description string</param>
        /// <returns>The mass of the specified molecule</returns>
        public double CalculateMass(string desc)
        {
            string parse = desc;
            double totalMass = ParseMass(ref parse);

            if (totalMass == 0.0 || parse.Length > 0)
                throw new ArgumentException("The expression '{0}' is not a valid chemical formula.");

            return totalMass;
        }

        /// <summary>
        /// Parses a chemical formula expressed as "[{atom}[count][spaces]]*",
        /// e.g. "C6H11ON", where supported atoms are H, O, N, C, S or P, etc.
        /// returning the total mass for the formula.
        /// 
        /// The parser removes atoms and counts until it encounters a character
        /// it does not understand as being part of the chemical formula.
        /// The remainder is returned in the desc parameter.
        /// </summary>
        /// <param name="desc">Input description, and remaining string after parsing</param>
        /// <returns>Total mass of formula parsed</returns>
        public double ParseMass(ref string desc)
        {
            double totalMass = 0.0;
            desc = desc.Trim();
            while (desc.Length > 0)
            {
                string sym = NextSymbol(desc);
                double massAtom = getMass(sym);

                // Stop if unrecognized atom found.
                if (massAtom == 0)
                {
                    // CONSIDER: Throw with a useful message?
                    break;
                }

                desc = desc.Substring(sym.Length);
                int endCount = 0;
                while (endCount < desc.Length && Char.IsDigit(desc[endCount]))
                    endCount++;

                int count = 1;
                if (endCount > 0)
                    count = int.Parse(desc.Substring(0, endCount), CultureInfo.InvariantCulture);
                totalMass += massAtom * count;

                desc = desc.Substring(endCount).TrimStart();
            }

            return totalMass;            
        }

        /// <summary>
        /// Get the mass of a single atom.
        /// </summary>
        /// <param name="sym">Character specifying the atom</param>
        /// <returns>The mass of the single atom</returns>
        private double getMass(string sym)
        {
            double mass;
            if (_atomicMasses.TryGetValue(sym, out mass))
                return mass;
            return 0;
        }

        /// <summary>
        /// Adds atomic masses for a symbol character to a look-up table.
        /// </summary>
        /// <param name="sym">Atomic symbol character</param>
        /// <param name="mono">Monoisotopic mass</param>
        /// <param name="ave">Average mass</param>
        private void addMass(string sym, double mono, double ave)
        {
            _atomicMasses[sym] = (MassType == MassType.Monoisotopic ? mono : ave);
        }
    }
}