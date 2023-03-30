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
            var mass = CalculateMassFromFormula(desc, out _);
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
        /// If the formula contains and adduct, e.g. "[M+2H]" in "C12H5[M+2H]", that is factored in.
        /// 
        /// </summary>
        /// <param name="desc">Input description</param>
        /// <param name="molReturn">Molecule object for returning the atoms and counts</param>
        /// <returns>Total mass of formula parsed</returns>
        public double ParseFormulaWithAdductMass(string desc, out MoleculeMassOffset molReturn)
        {
            if (!IonInfo.IsFormulaWithAdduct(desc, out molReturn, out _, out _))
            {
                molReturn = MoleculeMassOffset.Create(desc);
            }
            return molReturn.GetTotalMass(this.MassType);
        }
    }
}
