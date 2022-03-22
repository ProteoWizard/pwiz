/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    // TODO(bspratt) kill this now that we have a proper adduct class?

    /// <summary>
    /// Describes a molecule as its neutral formula and an adduct.  Adducts have the form [M+H], [M-3K], [2M+Isoprop+H] etc.
    ///  </summary>
    public class IonInfo : Immutable
    {

        private string _formula;  // Chemical formula, possibly followed by adduct description - something like "C12H3[M+H]" or "[2M+K]" or "M+H" or "[M+H]+" or "[M+Br]-" 
        private string _unlabledFormula;   // Chemical formula after adduct application and stripping of labels


        /// <summary>
        /// Constructs an IonInfo, which holds a neutral formula and adduct, or possibly just a chemical formula if no adduct is included in the description
        /// </summary>
        public IonInfo(string formulaWithOptionalAdduct, Adduct adduct)
        {
            Formula = formulaWithOptionalAdduct + adduct.AdductFormula;
        }

        public IonInfo(string formulaWithOptionalAdduct)
        {
            Formula = formulaWithOptionalAdduct;
        }

        /// <summary>
        /// For serialization
        /// </summary>
        protected IonInfo()
        {
        }

        /// <summary>
        /// Formula description as originally provided to constructor.
        /// </summary>
        public string Formula
        {
            get { return _formula; }
            protected set
            {
                _formula = value;
                _unlabledFormula = BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(FormulaWithAdductApplied);
                Helpers.AssignIfEquals(ref _unlabledFormula, _formula); // Save some string space if actually unlableled
            }
        }

        /// <summary>
        /// Internal formula description with adduct description stripped off, or null if there is no adduct description
        /// </summary>
        public string NeutralFormula
        {
            get
            {
                var adductStart = AdductStartIndex;
                if (adductStart == 0)
                {
                    return null; // There is no formula, just an adduct
                }
                return adductStart < 0 ? _formula : _formula.Substring(0, adductStart);
            }
        }

        /// <summary>
        /// Adduct part of internal formula description, or null if there is none
        /// </summary>
        public string AdductText
        {
            get
            {
                int adductStart = AdductStartIndex;
                return adductStart < 0 ? null : _formula.Substring(adductStart);
            }
        }

        private int AdductStartIndex
        {
            get
            {
                return _formula != null ? _formula.IndexOf('[') : -1;
            }
        }

        /// <summary>
        /// Returns chemical formula with adduct applied then labels stripped
        /// </summary>
        public string UnlabeledFormula
        {
            get { return _unlabledFormula; }
        }

        /// <summary>
        /// Chemical formula after adduct description, if any, is applied
        /// </summary>
        public string FormulaWithAdductApplied
        {
            get
            {
                if (string.IsNullOrEmpty(AdductText))
                {
                    return _formula;
                }
                int charge;
                var mol = ApplyAdductInFormula(_formula, out charge);
                return mol.ToString();
            }
        }

        public static bool EquivalentFormulas(string fL, string fR)
        {
            if (fL == null)
            {
                return fR == null;
            }
            if (fR == null)
            {
                return false;
            }
            if (Equals(fL, fR))
            {
                return true;
            }
            var moleculeL = Molecule.Parse(fL.Trim());
            var moleculeR = Molecule.Parse(fR.Trim());
            return moleculeL.Equals(moleculeR);
        }

        /// <summary>
        /// Check to see if an adduct is only a charge declaration, as in "[M+]".
        /// </summary>
        /// <param name="formula">A string like "C12H3[M+H]"</param>
        /// <returns>True if the adduct description contributes nothing to the ion formula other than charge information, such as "[M+]"</returns>
        public static bool AdductIsChargeOnly(string formula)
        {
            int charge;
            return Equals(ApplyAdductInFormula(formula, out charge), ApplyAdductInFormula(formula.Split('[')[0], out charge));
        }

        /// <summary>
        /// Take a molecular formula with adduct in it and return a Molecule.
        /// </summary>
        /// <param name="formula">A string like "C12H3[M+H]"</param>
        /// <param name="charge">Charge derived from adduct description by counting H, K etc as found in DICT_ADDUCT_ION_CHARGES</param>
        /// <returns></returns>
        public static Molecule ApplyAdductInFormula(string formula, out int charge)
        {
            var withoutAdduct = (formula ?? string.Empty).Split('[')[0];
            var adduct = Adduct.FromStringAssumeProtonated((formula ?? string.Empty).Substring(withoutAdduct.Length));
            charge = adduct.AdductCharge;
            return ApplyAdductToFormula(withoutAdduct, adduct);
        }

        /// <summary>
        /// Take a molecular formula and apply the described adduct to it.
        /// </summary>
        /// <param name="formula">A string like "C12H3"</param>
        /// <param name="adduct">An adduct derived from a string like "[M+H]" or "[2M+K]" or "M+H" or "[M+H]+" or "[M+Br]- or "M2C13+Na" </param>
        /// <returns>A Molecule whose formula is the combination of the input formula and adduct</returns>
        public static Molecule ApplyAdductToFormula(string formula, Adduct adduct)
        {
            var resultDict = ApplyAdductToMoleculeAsDictionary(formula, adduct);
            var resultMol = Molecule.FromDict(new ImmutableSortedList<string, int>(resultDict));
            if (!resultMol.Keys.All(k => BioMassCalc.MONOISOTOPIC.IsKnownSymbol(k)))
            {
                throw new InvalidOperationException(string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Unknown_symbol___0___in_adduct_description___1__, resultMol.Keys.First(k => !BioMassCalc.MONOISOTOPIC.IsKnownSymbol(k)), formula + adduct));
            }
            return resultMol;
        }

        /// <summary>
        /// Take a molecular formula and apply the described adduct to it.
        /// </summary>
        /// <param name="formula">A string like "C12H3"</param>
        /// <param name="adduct">An adduct derived from a string like "[M+H]" or "[2M+K]" or "M+H" or "[M+H]+" or "[M+Br]- or "M2C13+Na" </param>
        /// <returns>A dictionary of atomic elements and counts, resulting from the combination of the input formula and adduct</returns>
        public static Dictionary<string, int> ApplyAdductToMoleculeAsDictionary(string formula, Adduct adduct)
        {
            var molecule = Molecule.Parse(formula.Trim());
            var resultDict = new Dictionary<string, int>();
            adduct.ApplyToMolecule(molecule, resultDict);
            return resultDict;
        }

        public static bool IsFormulaWithAdduct(string formula, out Molecule molecule, out Adduct adduct, out string neutralFormula, bool strict = false)
        {
            molecule = null;
            adduct = Adduct.EMPTY;
            neutralFormula = null;
            if (string.IsNullOrEmpty(formula))
            {
                return false;
            }
            // Does formula contain an adduct description?  If so, pull charge from that.
            var parts = formula.Split('[');
            if (parts.Length == 2 && parts[1].Count(c => c==']') == 1)
            {
                neutralFormula = parts[0];
                var adductString = formula.Substring(neutralFormula.Length);
                if (Adduct.TryParse(adductString, out adduct, Adduct.ADDUCT_TYPE.non_proteomic, strict))
                {
                    molecule = neutralFormula.Length > 0 ? ApplyAdductToFormula(neutralFormula, adduct) : Molecule.Empty;
                    return true;
                }
            }
            return false;
        }

        public override string ToString()
        {
            return _formula ?? string.Empty;
        }
    }
}