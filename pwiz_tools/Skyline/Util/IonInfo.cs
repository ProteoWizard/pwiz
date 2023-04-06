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

using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util
{
    // TODO(bspratt) kill this now that we have a proper adduct class?

    /// <summary>
    /// Describes a molecule as its neutral formula and an adduct.  Adducts have the form [M+H], [M-3K], [2M+Isoprop+H] etc.
    ///  </summary>
    public class IonInfo : Immutable
    {
        private MoleculeMassOffset _neutralFormula; // Chemical formula and/or unexplained masses, no adduct applied
        private Adduct _adduct;
        private MoleculeMassOffset _ionFormula;  // Chemical formula and/or unexplained masses after adduct application
        private MoleculeMassOffset _unlabledFormula;   // Chemical formula after adduct application and stripping of labels

        /// <summary>
        /// Constructs an IonInfo, which holds a neutral formula and adduct, or possibly just a chemical formula if no adduct is included in the description
        /// </summary>
        public IonInfo(string formulaWithOptionalAdduct, Adduct adduct)
        {
            var ionString = Adduct.SplitFormulaAndTrailingAdduct(formulaWithOptionalAdduct, Adduct.ADDUCT_TYPE.charge_only, out var parsedAdduct);
            _adduct = Adduct.IsNullOrEmpty(adduct) ? parsedAdduct : adduct;
            Formula = ParsedMoleculeMassOffset.Create(ionString);
        }

        public IonInfo(MoleculeMassOffset formula, Adduct adduct)
        {
            _adduct = Adduct.IsNullOrEmpty(adduct) ? Adduct.EMPTY : adduct;
            Formula = formula;
        }

        public IonInfo(string formulaWithOptionalAdduct) : this(formulaWithOptionalAdduct, Adduct.EMPTY)
        {
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
        public MoleculeMassOffset Formula
        {
            get { return _ionFormula; }
            protected set
            {
                _neutralFormula = value;
                _ionFormula = _adduct.IsEmpty ?  _neutralFormula : _adduct.ApplyToMolecule(_neutralFormula);
                _unlabledFormula = BioMassCalc.StripLabelsFromFormula(_ionFormula);
            }
        }

        /// <summary>
        /// Internal formula description with adduct description stripped off, or null if there is no adduct description
        /// </summary>
        public MoleculeMassOffset NeutralFormula
        {
            get
            {
                return _neutralFormula;
            }
        }

        /// <summary>
        /// Adduct part of internal formula description, or null if there is none
        /// </summary>
        public string AdductText
        {
            get
            {
                return _adduct.AdductFormula;
            }
        }

        /// <summary>
        /// Returns chemical formula with adduct applied then labels stripped
        /// </summary>
        public MoleculeMassOffset UnlabeledFormula
        {
            get { return _unlabledFormula; }
        }

        /// <summary>
        /// Chemical formula after adduct description, if any, is applied
        /// </summary>
        public MoleculeMassOffset FormulaWithAdductApplied
        {
            get
            {
                return _ionFormula;
            }
        }

        /// <summary>
        /// Take a chemical formula (possibly with mass modifier) and apply the described adduct to it.
        /// </summary>
        /// <param name="formula">A string like "C12H3" or C11N3H5[+2.34]</param>
        /// <param name="adduct">An adduct derived from a string like "[M+H]" or "[2M+K]" or "M+H" or "[M+H]+" or "[M+Br]- or "M2C13+Na" </param>
        /// <returns>A dictionary of atomic elements and counts, resulting from the combination of the input formula and adduct</returns>
        public static MoleculeMassOffset ApplyAdductToFormula(string formula, Adduct adduct)
        {
            var trimmed = formula.Trim();
            var molecule = ParsedMoleculeMassOffset.Create(trimmed);
            return adduct.ApplyToMolecule(molecule);
        }

        public static bool IsFormulaWithAdduct(string formula, out MoleculeMassOffset molecule, out Adduct adduct, out string neutralFormula, bool strict = false)
        {
            molecule = MoleculeMassOffset.EMPTY;
            adduct = Adduct.EMPTY;
            neutralFormula = null;
            if (string.IsNullOrEmpty(formula))
            {
                return false;
            }
            // Does formula contain an adduct description?  If so, pull charge from that.
            // Watch out for mass modifications, e.g. C12H5[+1.23][M+3H] 
            neutralFormula = Adduct.SplitFormulaAndTrailingAdduct(formula, Adduct.ADDUCT_TYPE.non_proteomic, out adduct);
            if (!adduct.IsEmpty)
            {
                molecule = ApplyAdductToFormula(neutralFormula, adduct);
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return _ionFormula?.ToString() ?? string.Empty;
        }
    }
}