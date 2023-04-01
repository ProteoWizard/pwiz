/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Common.Chemistry
{
    /// <summary>
    /// Holds a chemical formula as well as an extra offset for the monoisotopic mass and the average mass.
    /// This is used when a molecule is made up of some things that we know the chemical formula for,
    /// and other things where the user has only told us the mono and average masses.
    /// Examples of these formulas with mass offsets include crosslinking and Hardklor output.
    /// </summary>
    public class MoleculeMassOffset : Molecule, IEquatable<MoleculeMassOffset>, IComparable<MoleculeMassOffset>
    {
        public new static readonly MoleculeMassOffset EMPTY = new MoleculeMassOffset(null, 0, 0);

        /// <summary>
        /// We provide these Create methods to avoid producing copies of an empty object
        /// </summary>
        public static MoleculeMassOffset Create(TypedMass monoMassOffset, TypedMass averageMassOffset)
        {
            return TypedMass.IsNullOrEmpty(monoMassOffset) ?
                EMPTY :
                Create(string.Empty, monoMassOffset, averageMassOffset);
        }

        public static MoleculeMassOffset Create(double? monoMassOffset, double? averageMassOffset)
        {
            return Create(string.Empty, TypedMass.Create(monoMassOffset ?? averageMassOffset ?? 0, MassType.Monoisotopic),
                TypedMass.Create(averageMassOffset ?? monoMassOffset ?? 0, MassType.Average));
        }

        public static MoleculeMassOffset Create(string formula, double monoMassOffset = 0, double averageMassOffset = 0)
        {
            return Create(formula, 
                monoMassOffset == 0 ? TypedMass.ZERO_MONO_MASSNEUTRAL : TypedMass.Create(monoMassOffset, MassType.Monoisotopic),
                averageMassOffset == 0 ? TypedMass.ZERO_AVERAGE_MASSNEUTRAL : TypedMass.Create(averageMassOffset, MassType.Average));
        }

        public static MoleculeMassOffset Create(IEnumerable<KeyValuePair<string, int>> molecule, double monoMassOffset = 0, double averageMassOffset = 0, string elementOrderHint = null)
        {
            var dict = molecule?.Where(kvp => kvp.Value != 0)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            if ((dict == null || dict.Count == 0) && monoMassOffset == 0 && averageMassOffset == 0)
            {
                return EMPTY;
            }
            return new MoleculeMassOffset(dict, monoMassOffset, averageMassOffset, elementOrderHint);
        }

        public static MoleculeMassOffset Create(string formulaAndMasses, TypedMass monoMassOffset, TypedMass averageMassOffset)
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

            var dictionary = new ReadOnlyDictionary<string, int>(ParseToDictionary(formula, out var orderHintString));
            monoMassOffset = TypedMass.IsNullOrEmpty(monoMassOffset) ? TypedMass.ZERO_MONO_MASSNEUTRAL : monoMassOffset;
            averageMassOffset = TypedMass.IsNullOrEmpty(averageMassOffset) ? TypedMass.ZERO_AVERAGE_MASSNEUTRAL : averageMassOffset;
            return new MoleculeMassOffset(dictionary, monoMassOffset, averageMassOffset, orderHintString);
        }

        public static MoleculeMassOffset Create(Molecule molecule)
        {
            return Molecule.IsNullOrEmpty(molecule) ? EMPTY : new MoleculeMassOffset(molecule.Dictionary, null, null, molecule._orderHintString, molecule._originalHashCode);
        }

        private MoleculeMassOffset(IEnumerable<KeyValuePair<string, int>> molecule, double monoMassOffset, double averageMassOffset, string elementOrderHint = null) :
            this(molecule,
                monoMassOffset == 0 ? TypedMass.ZERO_MONO_MASSNEUTRAL : TypedMass.Create(monoMassOffset, MassType.Monoisotopic),
                averageMassOffset == 0 ? TypedMass.ZERO_AVERAGE_MASSNEUTRAL : TypedMass.Create(averageMassOffset, MassType.Average),
                elementOrderHint)
        {
        }

        private MoleculeMassOffset(IEnumerable<KeyValuePair<string, int>> molecule, TypedMass monoMassOffset,
            TypedMass averageMassOffset, string elementOrderHint = null, int? previousHashCode = null) :
            this(molecule == null
                    ? new ReadOnlyDictionary<string, int>(new Dictionary<string, int>())
                    : molecule is ReadOnlyDictionary<string, int> readOnlyDictionary ?
                        readOnlyDictionary :
                        new ReadOnlyDictionary<string, int>(molecule.Where(kvp => kvp.Value != 0)
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)),
                monoMassOffset, averageMassOffset, elementOrderHint, previousHashCode)
        {
        }

        private MoleculeMassOffset(ReadOnlyDictionary<string, int> dictionary, double monoMassOffset, double averageMassOffset)
            : this(dictionary, 
                monoMassOffset == 0
                    ? TypedMass.ZERO_MONO_MASSNEUTRAL
                    : TypedMass.Create(monoMassOffset, MassType.Monoisotopic),
                averageMassOffset == 0
                    ? TypedMass.ZERO_AVERAGE_MASSNEUTRAL
                    : TypedMass.Create(averageMassOffset, MassType.Average),
                null, null)
        {
        }


        private MoleculeMassOffset(ReadOnlyDictionary<string, int> molecule, TypedMass monoMassOffset, TypedMass averageMassOffset, 
            string elementOrderHint, int? previousHashCode)
        {
            _originalHashCode = previousHashCode;  // Important to set this before setting Dictionary
            Dictionary = molecule;
            _orderHintString = elementOrderHint;

            if (monoMassOffset == null)
            {
                monoMassOffset = TypedMass.ZERO_MONO_MASSNEUTRAL;
            }
            if (averageMassOffset == null)
            {
                averageMassOffset = TypedMass.ZERO_AVERAGE_MASSNEUTRAL;
            }

            if ((monoMassOffset == 0) != (averageMassOffset == 0))
            {
                // One or the other was unspecified, just copy the specified one
                if (monoMassOffset == 0)
                {
                    monoMassOffset = averageMassOffset.ChangeIsMonoIsotopic(true);
                }
                else
                {
                    averageMassOffset = monoMassOffset.ChangeIsMonoIsotopic(false);
                }
            }
            MonoMassOffset = monoMassOffset;
            AverageMassOffset = averageMassOffset;
        }

        public bool IsHeavy() => Keys.Any(BioMassCalc.ContainsIsotopicElement) || MonoMassOffset.IsHeavy();

        public MoleculeMassOffset ChangeFormulaAndMassOffset(IDictionary<string, int> dict, TypedMass monoMassOffset, TypedMass averageMassOffset)
        {
            if (dict == null)
            {
                dict = new Dictionary<string, int>();
            }

            if (CollectionUtil.EqualsDeep(dict, Dictionary) && MonoMassOffset.Equals(monoMassOffset) && AverageMassOffset.Equals(averageMassOffset))
            {
                return this;
            }

            if (dict.Count == 0 && monoMassOffset == 0 && averageMassOffset == 0)
            {
                return EMPTY;
            }

            return new MoleculeMassOffset(dict, monoMassOffset, averageMassOffset, _orderHintString, _originalHashCode);
        }

        public MoleculeMassOffset ChangeIsMassH(bool isMassH)
        {
            if (GetTotalMass(MassType.Monoisotopic).IsMassH() == isMassH)
            {
                return this;
            }

            return new MoleculeMassOffset(Dictionary, MonoMassOffset.ChangeIsMassH(isMassH),
                AverageMassOffset.ChangeIsMassH(isMassH), _orderHintString, _originalHashCode);
        }

        public MoleculeMassOffset AdjustElementCountNoMassOffsetChange(string element, int delta)
        {
            return AdjustElementCount(element, delta) as MoleculeMassOffset;
        }


        public override Molecule AdjustElementCount(string element, int delta)
        {
            var newDict = base.AdjustElementCount(element, delta).Dictionary;
            return ChangeFormulaAndMassOffset(newDict, MonoMassOffset, AverageMassOffset);
        }

        public MoleculeMassOffset ChangeFormulaNoOffsetMassChange(IDictionary<string, int> formula)
        {
            return ChangeFormula(formula) as MoleculeMassOffset;
        }


        public override Molecule ChangeFormula(IDictionary<string, int> formula)
        {
            return ChangeFormulaAndMassOffset(formula, MonoMassOffset, AverageMassOffset);
        }

        public MoleculeMassOffset ChangeMassOffset(TypedMass mono, TypedMass avg)
        {
            return (!Equals(MonoMassOffset, mono) || !Equals(AverageMassOffset, avg)) ?
                new MoleculeMassOffset(Dictionary, mono, avg, _orderHintString, _originalHashCode) :
                this;
        }

        public MoleculeMassOffset StripIsotopicLabelsFromFormulaAndMassOffset()
        {
            return StripIsotopicLabels() as MoleculeMassOffset;
        }

        public override Molecule StripIsotopicLabels()
        {
            var stripped = base.StripIsotopicLabels();

            return ChangeFormulaAndMassOffset(stripped.Dictionary, MonoMassOffset.ChangeIsHeavy(false), AverageMassOffset.ChangeIsHeavy(false));
        }


        public static string MASS_MOD_CUE_PLUS = @"[+";
        public static string MASS_MOD_CUE_MINUS = @"[-";

        public static bool TryParse(string formulaAndMasses, out MoleculeMassOffset result, out string errorMessage)
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

        public new static MoleculeMassOffset Parse(string formulaAndMasses)
        {
            return Create(formulaAndMasses);
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

        public override TypedMass GetTotalMass(MassType massType)
        {
            return massType.IsMonoisotopic()
                ? base.GetTotalMass(MassType.Monoisotopic) + MonoMassOffset
                : base.GetTotalMass(MassType.Average) + AverageMassOffset;
        }

        public new MoleculeMassOffset SetElementCount(string element, int count)
        {
            if (TryGetValue(element, out var existing))
            {
                if (existing == count)
                {
                    return this;
                }
            }
            else if (count == 0)
            {
                return this; // There weren't any, and caller doesn't want any, we're all set
            }

            var newMolecule = new Dictionary<string, int>(Dictionary)
            {
                [element] = count
            };
            return new MoleculeMassOffset(newMolecule, MonoMassOffset, AverageMassOffset, _orderHintString, _originalHashCode);
        }

        public TypedMass MonoMassOffset { get; private set; }
        public TypedMass AverageMassOffset { get; private set; }

        public Molecule Molecule => this; // Convenience for code written before inheritance model changed

        public bool IsEmpty =>
            ReferenceEquals(this, MoleculeMassOffset.EMPTY) ||
            (IsMassOnly && AverageMassOffset == 0 && MonoMassOffset == 0);

        public static bool IsNullOrEmpty(MoleculeMassOffset moleculeMassOffset) =>
            moleculeMassOffset == null || moleculeMassOffset.IsEmpty;

        public bool HasChemicalFormula => Count != 0;

        public bool IsMassOnly => Count == 0;

        public bool HasMassModifications => MonoMassOffset != 0;

        public TypedMass GetMassOffset(MassType t) => t.IsMonoisotopic() ? MonoMassOffset : AverageMassOffset;

        #region math

        public MoleculeMassOffset Add(MoleculeMassOffset moleculeMassOffset)
        {
            var newMolecule = base.Plus(moleculeMassOffset).Dictionary; // Deal with the formula
            return ChangeFormulaAndMassOffset(newMolecule, MonoMassOffset + moleculeMassOffset.MonoMassOffset, AverageMassOffset + moleculeMassOffset.AverageMassOffset);
        }

        public override Molecule Plus(Molecule molecule)
        {
            if (molecule is MoleculeMassOffset moleculeAsMoleculeMassOffset)
            {
                return Add(moleculeAsMoleculeMassOffset); // Deal with the formula and mass offset
            }
            var newMolecule = base.Plus(molecule); // Deal with the formula
            return ChangeFormulaAndMassOffset(newMolecule.Dictionary, MonoMassOffset, AverageMassOffset); // Don't change the mass offset
        }

        public MoleculeMassOffset Minus(MoleculeMassOffset moleculeMassOffset)
        {
            var newMolecule = base.Difference(moleculeMassOffset);
            return ChangeFormulaAndMassOffset(newMolecule.Dictionary, MonoMassOffset - moleculeMassOffset.MonoMassOffset, AverageMassOffset - moleculeMassOffset.AverageMassOffset);
        }

        public override Molecule Difference(Molecule molecule)
        {
            if (molecule is MoleculeMassOffset moleculeAsMoleculeMassOffset)
            {
                return Minus(moleculeAsMoleculeMassOffset); // Deal with the formula and mass offset
            }
            var newMolecule = base.Difference(molecule); // Deal with the formula
            return ChangeFormulaAndMassOffset(newMolecule.Dictionary, MonoMassOffset, AverageMassOffset); // Don't change the mass offset
        }

        public static MoleculeMassOffset Sum(IEnumerable<MoleculeMassOffset> molecules)
        {
            var items = molecules.ToArray();
            var mol = Molecule.Sum(items);
            double monoMassOffset = 0;
            double averageMassOffset = 0;
            foreach (var item in items)
            {
                monoMassOffset += item.MonoMassOffset;
                averageMassOffset += item.AverageMassOffset;
            }
            return new MoleculeMassOffset(mol.Dictionary, monoMassOffset, averageMassOffset);
        }

        #endregion

        // N.B. The mass portion is always written with InvariantCulture.

        public static string FormatMassModification(double massMod, int desiredDecimals = BioMassCalc.MassPrecision)
        {
            var sign = massMod > 0 ? @"+" : string.Empty;
            return string.Format($@"[{sign}{massMod.ToString($"F{desiredDecimals}", CultureInfo.InvariantCulture).TrimEnd('0')}]");
        }

        public static string FormatMassModification(double massModMono, double massModAverage, int desiredDecimals = BioMassCalc.MassPrecision)
        {
            if (Equals(massModMono, massModAverage))
            {
                return FormatMassModification(massModMono, desiredDecimals);
            }
            var sign = massModMono > 0 ? @"+" : string.Empty;
            return string.Format($@"[{sign}{massModMono.ToString($"F{desiredDecimals}", CultureInfo.InvariantCulture).TrimEnd('0')}/{Math.Abs(massModAverage).ToString($"F{desiredDecimals}", CultureInfo.InvariantCulture).TrimEnd('0')}]");
        }

        public string ChemicalFormulaWithoutOffsets()
        {
            // Nicely format the part of the object that's described as a chemical formula.
            return IsMassOnly ? string.Empty : base.ToString();
        }

        public override string ToDisplayString()
        {
            return ToString(BioMassCalc.MassPrecision);
        }

        public override string ToString()
        {
            return ToString(BioMassCalc.MassPrecision);
        }

        public string ToString(int desiredDigits)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(ChemicalFormulaWithoutOffsets());

            if (MonoMassOffset != 0 || AverageMassOffset != 0)
            {
                stringBuilder.Append(FormatMassModification(MonoMassOffset, AverageMassOffset, desiredDigits)); // Use our standard notation e.g. [+1.23/1.24], [-1.2] etc
            }

            return stringBuilder.ToString();
        }

        public bool Equals(MoleculeMassOffset other)
        {
            if (other == null)
            {
                return false;
            }
            return CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) 
                return false;
            if (ReferenceEquals(this, obj)) 
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((MoleculeMassOffset) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ MonoMassOffset.GetHashCode();
                hashCode = (hashCode * 397) ^ AverageMassOffset.GetHashCode();
                return hashCode;
            }
        }

        private static Dictionary<string, int> MoleculeFromEntries(IEnumerable<KeyValuePair<string, int>> entries)
        {
            var dictionary = new Dictionary<string, int>();
            foreach (var entry in entries)
            {
                int count;
                if (dictionary.TryGetValue(entry.Key, out count))
                {
                    count += entry.Value;
                    if (count == 0)
                    {
                        dictionary.Remove(entry.Key);
                    }
                    else
                    {
                        dictionary[entry.Key] = count;
                    }
                }
                else
                {
                    if (entry.Value != 0)
                    {
                        dictionary.Add(entry.Key, entry.Value);
                    }
                }
            }

            return dictionary;
        }

        public int CompareTo(MoleculeMassOffset other)
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
            var d = base.CompareTo(other);
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


        public int CompareTolerant(MoleculeMassOffset other, double massTolerance)
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
            var d = base.CompareTo(other);
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
    }
}
