/*
 * Original author: Brian Pratt <bspratt .at. protein.ms>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.Chemistry;

namespace pwiz.Skyline.Util
{

    /// <summary>
    ///
    /// A molecule with optional mass offsets that can express the chemical formula it came from in its original form.
    /// 
    /// </summary>
    public class ParsedMoleculeMassOffset : MoleculeMassOffset
    {

        private string _orderHintString;
        private int _originalMoleculeHashCode;
        public new static ParsedMoleculeMassOffset EMPTY = new ParsedMoleculeMassOffset(Molecule.Empty, TypedMass.ZERO_MONO_MASSNEUTRAL, TypedMass.ZERO_AVERAGE_MASSNEUTRAL, string.Empty, 0);

        public new bool IsEmpty => ReferenceEquals(this, ParsedMoleculeMassOffset.EMPTY) || base.IsEmpty;

        public static bool IsNullOrEmpty(ParsedMoleculeMassOffset parsedMoleculeMassOffset) => parsedMoleculeMassOffset == null || parsedMoleculeMassOffset.IsEmpty;

        public static ParsedMoleculeMassOffset Create(double monoMassOffset, double averageMassOffset)
        {
            return Create(null,
                monoMassOffset == 0 ? TypedMass.ZERO_MONO_MASSNEUTRAL : TypedMass.Create(monoMassOffset, MassType.Monoisotopic),
                averageMassOffset == 0 ? TypedMass.ZERO_AVERAGE_MASSNEUTRAL : TypedMass.Create(averageMassOffset, MassType.Average));
        }

        public static ParsedMoleculeMassOffset Create(string formula, double monoMassOffset = 0, double averageMassOffset = 0)
        {
            return Create(formula, 
                monoMassOffset == 0 ? TypedMass.ZERO_MONO_MASSNEUTRAL : TypedMass.Create(monoMassOffset, MassType.Monoisotopic),
                averageMassOffset == 0 ? TypedMass.ZERO_AVERAGE_MASSNEUTRAL : TypedMass.Create(averageMassOffset, MassType.Average));
        }

        public static ParsedMoleculeMassOffset Create(MoleculeMassOffset formula)
        {
            return IsNullOrEmpty(formula) ?
                EMPTY :
                new ParsedMoleculeMassOffset(formula.Molecule, formula.MonoMassOffset, formula.AverageMassOffset, String.Empty, 0) ;
        }

        public static ParsedMoleculeMassOffset Create(string formulaAndMasses, TypedMass monoMassOffset, TypedMass averageMassOffset)
        {
            // Watch out for mass offsets appearing in the string representation
            SplitFormulaAndMasses(formulaAndMasses, out var formula, out var monoDeclared, out var averageDeclared);
            if (TypedMass.IsNullOrEmpty(monoMassOffset))
            {
                monoMassOffset = monoDeclared;
                averageMassOffset = averageDeclared;
            }

            if (string.IsNullOrEmpty(formula) && TypedMass.IsNullOrEmpty(monoMassOffset) && TypedMass.IsNullOrEmpty(averageMassOffset))
            {
                return EMPTY;
            }

            try
            {
                var molecule = Molecule.Parse(formula, out var regularizedFormula);
                monoMassOffset = TypedMass.IsNullOrEmpty(monoMassOffset) ? TypedMass.ZERO_MONO_MASSNEUTRAL : monoMassOffset;
                averageMassOffset = TypedMass.IsNullOrEmpty(averageMassOffset) ? TypedMass.ZERO_AVERAGE_MASSNEUTRAL : averageMassOffset;
                var result = new ParsedMoleculeMassOffset(molecule, monoMassOffset, averageMassOffset, regularizedFormula, molecule.GetHashCode());
                return result;
            }
            catch (ArgumentException)
            {
                throw new ArgumentException(BioMassCalc.FormatArgumentExceptionMessage(formula));
            }
            
        }

        private ParsedMoleculeMassOffset(Molecule molecule, TypedMass monoMassOffset, TypedMass averageMassOffset, string formula, int originalMoleculeHashCode) :
            base(molecule, monoMassOffset, averageMassOffset)
        {
            _orderHintString = formula;
            _originalMoleculeHashCode = originalMoleculeHashCode;
            var mass = BioMassCalc.MONOISOTOPIC.CalculateMass(molecule); // Syntax check, as well as noting any isotopes in formula
            if (mass.IsHeavy())
            {
                MonoMassOffset = MonoMassOffset.ChangeIsHeavy(true);
                AverageMassOffset = AverageMassOffset.ChangeIsHeavy(true);
            }
        }

        public bool HasIsotopes() => MonoMassOffset.IsHeavy() || BioMassCalc.ContainsIsotopicElement(Molecule);

        public override MoleculeMassOffset Change(Molecule newMolecule, TypedMass monoMassOffset, TypedMass averageMassOffset)
        {
            if (Molecule.IsNullOrEmpty(newMolecule) && monoMassOffset == 0 && averageMassOffset == 0)
            {
                return EMPTY;
            }

            if (Molecule.Equals(newMolecule) && MonoMassOffset.Equals(monoMassOffset) && AverageMassOffset.Equals(averageMassOffset))
            {
                return this;
            }

            return new ParsedMoleculeMassOffset(newMolecule, monoMassOffset, averageMassOffset, _orderHintString, _originalMoleculeHashCode);
        }

        public ParsedMoleculeMassOffset ChangeMolecule(Molecule newMolecule)
        {
            if (Molecule.Equals(newMolecule))
            {
                return this;
            }
            return new ParsedMoleculeMassOffset(newMolecule, MonoMassOffset, AverageMassOffset, _orderHintString, _originalMoleculeHashCode);
        }
        
        public static bool TryParse(string formulaAndMasses, out ParsedMoleculeMassOffset result, out string errorMessage)
        {
            try
            {
                errorMessage = string.Empty;
                result = Create(formulaAndMasses);
                return true;
            }
            catch (ArgumentException e)
            {
                errorMessage = e.Message;
                result = EMPTY;
                return false;
            }
        }

        public static bool StringContainsMassOffsetCue(string formula) =>
            formula.Contains(MASS_MOD_CUE_PLUS) || formula.Contains(MASS_MOD_CUE_MINUS);

        /// <summary>
        ///  Separate the formula and mass offset declarations in a string
        /// </summary>
        /// <param name="formulaAndMasses">input string  e.g. "C12H5", "C12H5[-1.2/1.21]", "[+1.3/1.31]", "1.3/1.31", "C12H5[-1.2/1.21]-C2H[-1.1]"</param>
        /// <param name="formula">string with any mass modifiers stripped out e.g.  "C12H5[-1.2/1.21]-C2H[-1.1]" ->  "C12H5-C2H" </param>
        /// <param name="mono">effect of any mono mass modifiers e.g. "C12H5[-1.2/1.21]-C2H[-1.1]" => 0.1 </param>
        /// <param name="average">effect of any avg mass modifiers e.g. "C12H5[-1.2/1.21]-C2H[-1.1]" => 0.11</param>
        private static void SplitFormulaAndMasses(string formulaAndMasses, out string formula, out TypedMass mono, out TypedMass average)
        {
            mono = TypedMass.ZERO_MONO_MASSNEUTRAL;
            average = TypedMass.ZERO_AVERAGE_MASSNEUTRAL;
            formula = formulaAndMasses?.Trim();
            if (string.IsNullOrEmpty(formula))
            {
                return;
            }
            // A few different possibilities here, e.g. "C12H5", "C12H5[-1.2/1.21]", "[+1.3/1.31]", "1.3/1.31"
            // Also possibly  "C12H5[-1.2/1.21]-C2H[-1.1]"
            double modMassMono = 0;
            double modMassAvg = 0;
            var position = 0;
            while (StringContainsMassOffsetCue(formula))
            {
                var cuePlus = formula.IndexOf(MASS_MOD_CUE_PLUS, position, StringComparison.InvariantCulture);
                var cueMinus = formula.IndexOf(MASS_MOD_CUE_MINUS, position, StringComparison.InvariantCulture);
                if (cuePlus < 0)
                {
                    cuePlus = int.MaxValue;
                }
                if (cueMinus < 0)
                {
                    cueMinus = int.MaxValue;
                }
                // e.g. "C12H5[-1.2/1.21]", "{+1.3/1.31]",  "{-1.3]"
                position = Math.Min(cuePlus, cueMinus);
                var close = formula.IndexOf(']', position);
                var parts = formula.Substring(position, close-position).Split('/');
                double negate = Equals(cueMinus, position) ? -1 : 1;
                if (formula.Substring(0, position).Contains('-'))
                {
                    negate *= -1;
                }
                var monoMass = negate * double.Parse(parts[0].Substring(2).Trim(), CultureInfo.InvariantCulture);
                modMassMono += monoMass;
                modMassAvg += (parts.Length > 1 ? negate * double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture) : monoMass);
                formula = formula.Substring(0, position) + formula.Substring(close+1);
            }
            if (formula.Contains(@"/"))
            {
                // e.g.  "1.3/1.31"
                var parts = formula.Split(new[] { '/' });
                modMassMono = double.Parse(parts[0], CultureInfo.InvariantCulture);
                modMassAvg = double.Parse(parts[1], CultureInfo.InvariantCulture);
                formula = string.Empty;
            }

            mono = TypedMass.Create(modMassMono, MassType.Monoisotopic); 
            average = TypedMass.Create(modMassAvg, MassType.Average);
        }

        /// <summary>
        /// Return a string representation as close as possible to the one that gave rise to this object
        /// </summary>
        public override string ChemicalFormulaString()
        {
            // Nicely format the part of the object that's described as a chemical formula.
            if (IsMassOnly)
            {
                return string.Empty;
            }

            // It's likely that _orderHintString is the very string that this was built from, if so just return that
            if (!string.IsNullOrEmpty(_orderHintString) && Molecule.GetHashCode() == _originalMoleculeHashCode)
            {
                return _orderHintString;
            }

            // Otherwise, we need to rebuild the string from the molecule, using the order hint if available, ir Hill System order if not
            return HillSystemOrdering.ToOrderedString(Molecule, _orderHintString);
        }

        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
        {
            return Molecule.GetEnumerator();
        }

        public override string ToString()
        {
            return ToString(BioMassCalc.MassPrecision);
        }
        
    }
}
