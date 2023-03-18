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
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Calculates molecular masses based on atomic masses.
    /// Atomic masses come from http://www.unimod.org/unimod_help.html.
    /// Some heavy isotopes come from pwiz/utility/chemistry/isotopes.text, which
    /// comes from http://physics.nist.gov/PhysRefData/Compositions/index.html
    /// The average mass of Carbon comes from Michael MacCoss, which he claims
    /// was derived by Dwight Matthews and John Hayes in the 70s.  It takes into
    /// account carbon 12 enrichment in living organisms:
    /// 
    /// http://www.madsci.org/posts/archives/2003-06/1055532737.Bc.r.html
    /// 
    /// But at 12.01085 is slightly higher than the current Unimod standard
    /// of 12.0107.
    ///  </summary>
    public class BioMassCalc : BioMassCalcBase
    {
        public static readonly BioMassCalc MONOISOTOPIC = new BioMassCalc(MassType.Monoisotopic);
        public static readonly BioMassCalc AVERAGE = new BioMassCalc(MassType.Average);

        public new static double MassProton => BioMassCalcBase.MassProton;
        public new static double MassElectron => BioMassCalcBase.MassElectron;


        public static readonly IsotopeAbundances DEFAULT_ABUNDANCES = IsotopeAbundances.Default;

        /// <summary>
        /// Find the first atomic symbol in a given expression.
        /// </summary>
        /// <param name="expression">The expression to search</param>
        /// <returns>The first atomic symbol</returns>
        private static string NextSymbol(string expression)
        {

            // Skip the first character, since it is always the start of
            // the symbol, and then look for the end of the symbol.
            var i = 1;
            foreach (var c in expression.Skip(1))
            {
                if (!char.IsLower(c) && c != '\'' && c != '"')
                {
                    return expression.Substring(0, i);
                }
                i++;
            }
            return expression;
        }

        /// <summary>
        /// Create a simple mass calculator for use in calculating
        /// protein, peptide and fragment masses.
        /// </summary>
        /// <param name="type">Monoisotopic or average mass calculations</param>
        public BioMassCalc(MassType type) : base(type,
            Resources.BioMassCalc_CalculateMass_The_expression__0__is_not_a_valid_chemical_formula,
            Resources.BioMassCalc_FormatArgumentException__Supported_chemical_symbols_include__)
        {
        }

        /// <summary>
        /// For test purposes
        /// </summary>
        public double CalculateIonMz(string desc, Adduct adduct)
        {
            var mass = CalculateMassFromFormula(desc);
            return adduct.MzFromNeutralMass(mass);
        }

        /// <summary>
        /// For test purposes
        /// </summary>
        public static double CalculateIonMz(TypedMass mass, Adduct adduct)
        {
            return adduct.MzFromNeutralMass(mass);
        }

        /// <summary>
        /// For test purposes
        /// </summary>
        public static double CalculateIonMass(TypedMass mass, Adduct adduct)
        {
            return adduct.ApplyToMass(mass);
        }

        /// <summary>
        /// Parses a chemical formula expressed as "[{atom}[count][spaces]]*",
        /// e.g. "C6H11ON", where supported atoms are H, O, N, C, S or P, etc.
        /// returning the total mass for the formula.
        /// 
        /// The parser removes atoms and counts until it encounters a character
        /// it does not understand as being part of the chemical formula.
        /// The remainder is returned in the desc parameter.
        /// 
        /// This parser will stop at the first minus sign. If you need to parse
        /// an expression that might contain a minus sign, use <see cref="BioMassCalcBase.ParseMassExpression"/>.
        /// </summary>
        /// <param name="desc">Input description, and remaining string after parsing</param>
        /// <param name="molReturn">Optional dictionary for returning the atoms and counts</param>
        /// <returns>Total mass of formula parsed</returns>
        public double ParseMass(ref string desc, Dictionary<string, int> molReturn = null)
        {
            double totalMass = 0.0;
            desc = desc.Trim();
            Adduct adduct;
            string neutralFormula;
            Dictionary<string, int> dict = null;
            if (IonInfo.IsFormulaWithAdduct(desc, out var mol, out adduct, out neutralFormula))
            {
                totalMass = mol.Sum(p => p.Value*GetMass(p.Key));
                desc = string.Empty; // Signal that we parsed the whole thing
                if (molReturn != null)
                {
                    dict = mol.Dictionary.ToDictionary(kvp=>kvp.Key, kvp=>kvp.Value);
                }
            }
            else
            {
                if (molReturn != null)
                {
                    dict = new Dictionary<string, int>();
                }
                totalMass = ParseFormulaMass(ref desc, dict);
            }

            if (molReturn != null)
            {
                foreach (var kvp in dict)
                {
                    var sym = kvp.Key;
                    var count = kvp.Value;
                    if (molReturn.TryGetValue(sym, out var oldCount))
                    {
                        molReturn[sym] = count + oldCount;
                    }
                    else
                    {
                        molReturn.Add(sym, count);
                    }
                }
            }
            return totalMass;            
        }
    }
}
