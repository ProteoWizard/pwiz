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
        public const string H = "H";
        public const string H2 = "H'";
        public const string C = "C";
        public const string C13 = "C'";
        public const string N = "N";
        public const string N15 = "N'";
        public const string O = "O";
        public const string O18 = "O'";
        public const string P = "P";
        public const string S = "S";
// ReSharper disable InconsistentNaming
        public const string Se = "Se";
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
        /// e.g. "C6H11ON", where supported atoms are H, O, N, C, S or P,
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