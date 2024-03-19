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
using System.Text.RegularExpressions;
using pwiz.Common.Chemistry;

namespace pwiz.Skyline.Util
{

    /// <summary>
    ///
    /// A molecule with optional mass offsets that can express the chemical formula it came from in its original form.
    ///
    /// Similar to MoleculeMassOffset, but has the extra logic to preserve parsed chemical formula, and tracks MassType of the mass offsets.
    /// 
    /// </summary>
    public class ParsedMolecule : IEquatable<ParsedMolecule>, IComparable<ParsedMolecule>
    {

        private string _orderHintString; // The original parsed string, if any, that was used to order this molecule
        private int _originalMoleculeHashCode; // Useful for deciding whether or not _orderHintString is still valid for direct use

        public static ParsedMolecule Create(double monoMassOffset, double averageMassOffset)
        {
            return Create(Molecule.Empty,
                monoMassOffset == 0 ? TypedMass.ZERO_MONO_MASSNEUTRAL : new TypedMass(monoMassOffset, MassType.Monoisotopic),
                averageMassOffset == 0 ? TypedMass.ZERO_AVERAGE_MASSNEUTRAL : new TypedMass(averageMassOffset, MassType.Average));
        }

        public static ParsedMolecule Create(string formula, double monoMassOffset = 0, double averageMassOffset = 0)
        {
            return Create(formula,
                monoMassOffset == 0 ? TypedMass.ZERO_MONO_MASSNEUTRAL : new TypedMass(monoMassOffset, MassType.Monoisotopic),
                averageMassOffset == 0 ? TypedMass.ZERO_AVERAGE_MASSNEUTRAL : new TypedMass(averageMassOffset, MassType.Average));
        }

        public static ParsedMolecule Create(MoleculeMassOffset formula)
        {
            return MoleculeMassOffset.IsNullOrEmpty(formula) ?
                EMPTY :
                new ParsedMolecule(formula.Molecule,
                    new TypedMass(formula.MonoMassOffset, MassType.Monoisotopic),
                    new TypedMass(formula.AverageMassOffset, MassType.Average),
                    string.Empty, 0);
        }

        public static ParsedMolecule Create(Molecule formula)
        {
            return Molecule.IsNullOrEmpty(formula) ?
                EMPTY :
                new ParsedMolecule(formula, TypedMass.ZERO_MONO_MASSNEUTRAL, TypedMass.ZERO_AVERAGE_MASSNEUTRAL,
                    string.Empty, 0);
        }

        public static ParsedMolecule Create(Molecule formula, TypedMass monoMassOffset, TypedMass averageMassOffset)
        {
            return Molecule.IsNullOrEmpty(formula) && monoMassOffset==0 ?
                EMPTY :
                new ParsedMolecule(formula, monoMassOffset, averageMassOffset, string.Empty, 0);
        }

        public static ParsedMolecule Create(string formulaAndMasses, TypedMass monoMassOffset, TypedMass averageMassOffset)
        {
            // Watch out for mass offsets appearing in the string representation
            ParseFormulaAndMasses(formulaAndMasses, out var formula, out var monoDeclaredD, out var averageDeclaredD);
            if (monoDeclaredD.HasValue)
            {
                monoMassOffset = new TypedMass(monoDeclaredD.Value, MassType.Monoisotopic);
                averageMassOffset = new TypedMass(averageDeclaredD.Value, MassType.Average);
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
                var result = new ParsedMolecule(molecule, monoMassOffset, averageMassOffset, regularizedFormula, molecule.GetHashCode());
                return result;
            }
            catch (ArgumentException)
            {
                throw new ArgumentException(BioMassCalc.FormatArgumentExceptionMessage(formula));
            }

        }

        private ParsedMolecule(Molecule molecule, TypedMass monoMassOffset, TypedMass averageMassOffset, string formula, int originalMoleculeHashCode)
        {
            Molecule = molecule;
            MonoMassOffset = monoMassOffset;
            AverageMassOffset = averageMassOffset;
            _orderHintString = formula;
            _originalMoleculeHashCode = originalMoleculeHashCode;
            var mass = BioMassCalc.MONOISOTOPIC.CalculateMass(molecule); // Syntax check, as well as noting any isotopes in formula
            if (mass.IsHeavy())
            {
                MonoMassOffset = MonoMassOffset.ChangeIsHeavy(true);
                AverageMassOffset = AverageMassOffset.ChangeIsHeavy(true);
            }
        }



        public static ParsedMolecule EMPTY = new ParsedMolecule(Molecule.Empty, TypedMass.ZERO_MONO_MASSNEUTRAL, TypedMass.ZERO_AVERAGE_MASSNEUTRAL, string.Empty, 0);
        public bool IsEmpty => ReferenceEquals(this, EMPTY) || (Molecule.IsNullOrEmpty(Molecule) && MonoMassOffset==0 && AverageMassOffset==0);
        public bool HasMassOffsets => MonoMassOffset != 0;

        public static bool IsNullOrEmpty(ParsedMolecule parsedMolecule) => parsedMolecule == null || parsedMolecule.IsEmpty;
        public bool HasChemicalFormula => !Molecule.IsNullOrEmpty(Molecule);

        public Molecule Molecule { get; private set; }
        public TypedMass MonoMassOffset { get; private set; }
        public TypedMass AverageMassOffset { get; private set; }
        public TypedMass GetMassOffset(MassType massType) => massType.IsMonoisotopic() ? MonoMassOffset : AverageMassOffset;

        public bool IsMassOnly => Molecule.IsNullOrEmpty(Molecule);
        public bool HasIsotopes() => MonoMassOffset.IsHeavy() || BioMassCalc.ContainsIsotopicElement(Molecule);
        public MoleculeMassOffset GetMoleculeMassOffset() => MoleculeMassOffset.Create(Molecule, MonoMassOffset, AverageMassOffset);

        public ParsedMolecule Change(TypedMass monoMassOffset, TypedMass averageMassOffset)
        {
            if (MonoMassOffset.Equals(monoMassOffset) && AverageMassOffset.Equals(averageMassOffset))
            {
                return this;
            }

            if (Molecule.IsNullOrEmpty(Molecule) && monoMassOffset == 0 && averageMassOffset == 0)
            {
                return EMPTY;
            }

            return new ParsedMolecule(Molecule, monoMassOffset, averageMassOffset, _orderHintString, _originalMoleculeHashCode);
        }
        public ParsedMolecule Change(Molecule newMolecule, TypedMass monoMassOffset, TypedMass averageMassOffset)
        {
            if (Molecule.IsNullOrEmpty(newMolecule) && monoMassOffset == 0 && averageMassOffset == 0)
            {
                return EMPTY;
            }

            if (Molecule.Equals(newMolecule) && MonoMassOffset.Equals(monoMassOffset) && AverageMassOffset.Equals(averageMassOffset))
            {
                return this;
            }

            return new ParsedMolecule(newMolecule, monoMassOffset, averageMassOffset, _orderHintString, _originalMoleculeHashCode);
        }

        public ParsedMolecule ChangeIsMassH(bool isMassH)
        {
            return Change(Molecule, MonoMassOffset.ChangeIsMassH(isMassH), AverageMassOffset.ChangeIsMassH(isMassH));
        }

        public ParsedMolecule ChangeIsHeavy(bool isHeavy)
        {
            return Change(Molecule, MonoMassOffset.ChangeIsHeavy(isHeavy), AverageMassOffset.ChangeIsHeavy(isHeavy));
        }

        public ParsedMolecule ChangeMolecule(Molecule newMolecule)
        {
            if (Molecule.Equals(newMolecule))
            {
                return this;
            }
            return new ParsedMolecule(newMolecule, MonoMassOffset, AverageMassOffset, _orderHintString, _originalMoleculeHashCode);
        }


        public static bool TryParseFormula(string formula, out ParsedMolecule resultMolecule, out string errorMessage)
        {
            try
            {
                // ParseFormulaMass checks for unknown symbols, so it's useful to us as a syntax checking parser even if we don't care about mass right now
                BioMassCalc.MONOISOTOPIC.ParseFormulaMass(formula, out resultMolecule);
                errorMessage = string.Empty;
                return true;
            }
            catch (ArgumentException e)
            {
                resultMolecule = ParsedMolecule.EMPTY;
                errorMessage = e.Message;
                return false;
            }
        }

        // A few different possibilities here, e.g. "C12H5", "C12H5[-1.2/1.21]", "[+1.3/1.31]", "1.3/1.31"
        // Also possibly  "C12H5[-1.2/1.21]-C2H[-1.1]" or  "C12H5-C2H[-1.1]"
        private static Regex FormulaAndMassesRegex = new Regex(@"(-?)([^\[\-]*)(\[([^\]]+)\])?", 
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Parse the formula and mass offset declarations in a string presumed to describe a chemical formula and/or mass offsets.
        /// Is aware of simple math, e.g. "C12H5[-1.2/1.21]-C2H[-1.1]" => "C12H5-C2H", 0.1, 0.11
        /// </summary>
        /// <param name="formulaAndMasses">input string  e.g. "C12H5", "C12H5[-1.2/1.21]", "[+1.3/1.31]", "1.3/1.31", "C12H5[-1.2/1.21]-C2H[-1.1]"</param>
        /// <param name="formulaOut">string with any mass modifiers stripped out e.g.  "C12H5[-1.2/1.21]-C2H[-1.1]" ->  "C12H5-C2H" </param>
        /// <param name="modMassMonoOut">effect of any mono mass modifiers e.g. "C12H5[-1.2/1.21]-C2H[-1.1]" => 0.1 </param>
        /// <param name="modMassAverageOut">effect of any avg mass modifiers e.g. "C12H5[-1.2/1.21]-C2H[-1.1]" => 0.11</param>
        private static void ParseFormulaAndMasses(string formulaAndMasses, out string formulaOut, out double? modMassMonoOut, out double? modMassAverageOut)
        {
            modMassMonoOut = null;
            modMassAverageOut = null;
            formulaOut = formulaAndMasses?.Trim();
            if (string.IsNullOrEmpty(formulaOut))
            {
                return;
            }

            if (!char.IsUpper(formulaOut[0]) && !MoleculeMassOffset.StringContainsMassOffsetCue(formulaOut))
            {
                // No formula or "[+1.2/1.21]" for regex to match - but could be bare "+1.2/1.21"
                if (TryParseMassOffsetValues(formulaOut, 1.0, out var monoMassParsed, out var averageMassParsed))
                {
                    modMassMonoOut = monoMassParsed;
                    modMassAverageOut = averageMassParsed;
                    formulaOut = string.Empty;
                }
                return;
            }

            var matches = FormulaAndMassesRegex.Matches(formulaOut);
            if (matches.Count > 0)
            {
                formulaOut = string.Empty;
                foreach (Match match in matches)
                {
                    double sign = match.Groups[1].Value == @"-" ? -1 : 1;
                    var thisFormula = match.Groups[2].Value;
                    if (sign < 0 && !string.IsNullOrEmpty(thisFormula))
                    {
                        formulaOut += @"-";
                    }
                    formulaOut += thisFormula;
                    var brackets = match.Groups[3].Value;
                    if (!string.IsNullOrEmpty(brackets))
                    {
                        if (!TryParseMassOffsetValues(brackets, sign, out var monoMassParsed, out var averageMassParsed))
                        {
                            throw new ArgumentException(string.Format(UtilResources.ParsedMolecule_SplitFormulaAndMasses_Cannot_parse___0___as_a_mass_offset_in_the_text___1___, brackets, formulaAndMasses));
                        }
                        modMassMonoOut = (modMassMonoOut ?? 0) + monoMassParsed;
                        modMassAverageOut = (modMassAverageOut ?? 0) + averageMassParsed;
                    }
                }
            }

            bool TryParseMassOffsetValues(string text, double sign, out double monoMassParsed, out double averageMassParsed)
            {
                var index = text.StartsWith(@"[") ? 1 : 0; // Deal with "[+1.2/1.22]" or  "+1.2/1.22" or "1.2/1.22"
                if (text.Length == index)
                {
                    averageMassParsed = monoMassParsed = 0;
                    return false;
                }
                if (text[index] == '-')
                {
                    sign *= -1;
                    index++;
                }
                else if (text[index] == '+')
                {
                    index++;
                }
                var parts = text.Split('/');
                if (!double.TryParse(parts[0].Substring(index).Trim(']'), NumberStyles.Any, CultureInfo.InvariantCulture, out monoMassParsed))
                {
                    averageMassParsed = 0;
                    return false;
                }
                monoMassParsed *= sign;
                averageMassParsed = monoMassParsed;
                if (parts.Length > 1)
                {
                    if (!double.TryParse(parts[1].Trim(']'), NumberStyles.Any, CultureInfo.InvariantCulture, out averageMassParsed))
                    {
                        return false;
                    }
                    averageMassParsed *= sign;
                }

                return true;
            }

        }

        #region math

        public ParsedMolecule Difference(ParsedMolecule other)
        {
            if (ParsedMolecule.IsNullOrEmpty(other))
            {
                return this;
            }
            var newMolecule = Molecule.Difference(other.Molecule);
            return Create(newMolecule, MonoMassOffset - other.MonoMassOffset, AverageMassOffset - other.AverageMassOffset);
        }
        
        public ParsedMolecule AdjustElementCount(string element, int delta)
        {
            var newMolecule = Molecule.AdjustElementCount(element, delta, true); // Deal with the formula
            return ChangeMolecule(newMolecule);
        }

        #endregion



        #region comparisons

        public bool Equals(ParsedMolecule other)
        {
            if (other == null)
            {
                return false;
            }
            return CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ParsedMolecule)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Molecule.GetHashCode();
                hashCode = (hashCode * 397) ^ MonoMassOffset.GetHashCode();
                hashCode = (hashCode * 397) ^ AverageMassOffset.GetHashCode();
                return hashCode;
            }
        }

        public int CompareTo(ParsedMolecule other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }
            if (ReferenceEquals(null, other))
            {
                return 1;
            }

            // Compare formulas
            var d = Molecule.CompareTo(other.Molecule);
            if (d != 0)
            {
                return d;
            }

            // Compare mass offsets
            d = MonoMassOffset.CompareTo(other.MonoMassOffset);
            if (d != 0)
            {
                return d;
            }
            d = AverageMassOffset.CompareTo(other.AverageMassOffset);
            if (d != 0)
            {
                return d;
            }

            return 0;
        }


        public int CompareTolerant(ParsedMolecule other, double massTolerance)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            if (ReferenceEquals(null, other))
            {
                return 1;
            }

            // Compare formulas
            var d = Molecule.CompareTo(other.Molecule);
            if (d != 0)
            {
                return d;
            }

            // Compare mass offsets
            d = MonoMassOffset.CompareTolerant(other.MonoMassOffset, massTolerance);
            if (d != 0)
            {
                return d;
            }
            d = AverageMassOffset.CompareTolerant(other.AverageMassOffset, massTolerance);
            if (d != 0)
            {
                return d;
            }

            return 0;
        }
        
        #endregion

        /// <summary>
        /// Return a string representation as close as possible to the one that gave rise to this object
        /// </summary>
        public string ChemicalFormulaString()
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

        public string ToString(int precision)
        {
            // Format the same way MoleculeMassOffset does, but with more attention to reproducing original formula if possible
            // N.B. mass offsets are always presented in Invariant Culture
            return ChemicalFormulaString() + MoleculeMassOffset.FormatMassModification(MonoMassOffset, AverageMassOffset, precision);
        }
    }
}
