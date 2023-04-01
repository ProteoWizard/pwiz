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
    /// Extends BioMassCalc to add Skyline-specific functionality.
    ///  </summary>
    public class SkylineBioMassCalc : BioMassCalc
    {

        public new static SkylineBioMassCalc MONOISOTOPIC = new SkylineBioMassCalc(MassType.Monoisotopic);
        public new static SkylineBioMassCalc AVERAGE = new SkylineBioMassCalc(MassType.Average);


        /// <summary>
        /// Create a simple mass calculator for use in calculating
        /// protein, peptide and fragment masses.
        /// </summary>
        /// <param name="type">Monoisotopic or average mass calculations</param>
        public SkylineBioMassCalc(MassType type) : base(type,
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
