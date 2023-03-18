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
            return TypedMass.IsEmpty(monoMassOffset) ?
                EMPTY :
                new MoleculeMassOffset(string.Empty, monoMassOffset, averageMassOffset);
        }

        public static MoleculeMassOffset Create(double? monoMassOffset, double? averageMassOffset)
        {
            return Create(TypedMass.Create(monoMassOffset ?? averageMassOffset ?? 0, MassType.Monoisotopic),
                TypedMass.Create(averageMassOffset ?? monoMassOffset ?? 0, MassType.Average));
        }

        public static MoleculeMassOffset Create(string formula, TypedMass monoMassOffset, TypedMass averageMassOffset)
        {
            return (string.IsNullOrEmpty(formula) && TypedMass.IsEmpty(monoMassOffset))
                ? EMPTY
                : new MoleculeMassOffset(formula, monoMassOffset, averageMassOffset);
        }

        public static MoleculeMassOffset Create(string formula, double monoMassOffset = 0, double averageMassOffset = 0)
        {
            return Create(formula, 
                monoMassOffset == 0 ? TypedMass.ZERO_MONO_MASSNEUTRAL : TypedMass.Create(monoMassOffset, MassType.Monoisotopic),
                averageMassOffset == 0 ? TypedMass.ZERO_AVERAGE_MASSNEUTRAL : TypedMass.Create(averageMassOffset, MassType.Average));
        }

        public static MoleculeMassOffset Create(IEnumerable<KeyValuePair<string, int>> molecule, double monoMassOffset = 0, double averageMassOffset = 0)
        {
            return molecule == null ? EMPTY : new MoleculeMassOffset(molecule, monoMassOffset, averageMassOffset);
        }

        private MoleculeMassOffset(IEnumerable<KeyValuePair<string, int>> molecule, double monoMassOffset, double averageMassOffset, List<string> elementOrder = null) :
            this(molecule,
                monoMassOffset == 0 ? TypedMass.ZERO_MONO_MASSNEUTRAL : TypedMass.Create(monoMassOffset, MassType.Monoisotopic),
                averageMassOffset == 0 ? TypedMass.ZERO_AVERAGE_MASSNEUTRAL : TypedMass.Create(averageMassOffset, MassType.Average),
                elementOrder)
        {
        }

        private MoleculeMassOffset(string formula, TypedMass monoMassOffset, TypedMass averageMassOffset)
        {
            _elementOrder = new List<string>();
            Dictionary = new ImmutableDictionary<string, int>(ParseExpressionToDictionary(formula, _elementOrder));
            MonoMassOffset = monoMassOffset;
            AverageMassOffset = averageMassOffset;
        }

        private MoleculeMassOffset(IEnumerable<KeyValuePair<string, int>> molecule, TypedMass monoMassOffset, TypedMass averageMassOffset, List<string> elementOrder = null)
        {
            _elementOrder = elementOrder;
            Dictionary = molecule == null ?
                new ImmutableDictionary<string, int>(new Dictionary<string, int>()) :
                new ImmutableDictionary<string, int>(molecule.Where(kvp => kvp.Value != 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
            MonoMassOffset = monoMassOffset;
            AverageMassOffset = averageMassOffset;
        }

        public MoleculeMassOffset AdjustElementCount(string element, int delta)
        {
            if (delta == 0)
            {
                return this;
            }
            TryGetValue(element, out var count);
            count += delta;
            var dict = (count == 0) ?
                Dictionary.RemoveKey(element) :
                Dictionary.Replace(element, count);
            return ChangeFormula(dict);
        }

        public new MoleculeMassOffset ChangeFormula(IEnumerable<KeyValuePair<string, int>> formula)
        {
            return new MoleculeMassOffset(formula, MonoMassOffset, AverageMassOffset, _elementOrder);
        }

        public MoleculeMassOffset ChangeMassOffset(TypedMass mono, TypedMass avg)
        {
            return (!Equals(MonoMassOffset, mono) || !Equals(AverageMassOffset, avg)) ?
                new MoleculeMassOffset(Dictionary, mono, avg, _elementOrder) :
                this;
        }

        public MoleculeMassOffset StripLabelsFromFormula()
        {
            var stripped = BioMassCalcBase.StripLabelsFromFormula(Dictionary);

            return CollectionUtil.EqualsDeep(stripped, Dictionary)
                ? this
                : ChangeFormula(stripped);
        }



        public static string MASS_MOD_CUE_PLUS = @"[+";
        public static string MASS_MOD_CUE_MINUS = @"[-";

        public new static MoleculeMassOffset Parse(string formulaAndMasses)
        {
            if (string.IsNullOrEmpty(formulaAndMasses))
            {
                return EMPTY;
            }
            // A few different possibilities here, e.g. "C12H5", "C12H5[-1.2/1.21]", "{+1.3/1.31]", "1.3/1.31"
            var formula = formulaAndMasses.Trim();
            double modMassMono = 0;
            double modMassAvg = 0;
            var cue = formulaAndMasses.Contains(MASS_MOD_CUE_PLUS) ? MASS_MOD_CUE_PLUS :
                formulaAndMasses.Contains(MASS_MOD_CUE_MINUS) ? MASS_MOD_CUE_MINUS : null;
            if (cue != null)
            {
                // e.g. "C12H5[-1.2/1.21]", "{+1.3/1.31]",  "{-1.3]"
                var pos = formulaAndMasses.LastIndexOf(cue, StringComparison.InvariantCulture);
                formula = formulaAndMasses.Substring(0, pos);
                var parts = formulaAndMasses.Substring(pos).Split(new[]{'/',']'});
                double negate = Equals(cue, MASS_MOD_CUE_MINUS) ? -1 : 1;
                modMassMono = negate * double.Parse(parts[0].Substring(2), CultureInfo.InvariantCulture);
                modMassAvg = negate * (parts.Length > 2 ? double.Parse(parts[1], CultureInfo.InvariantCulture) : modMassMono);
            }
            else if (formulaAndMasses.Contains(@"/"))
            {
                // e.g.  "1.3/1.31"
                var parts = formula.Split(new[] { '/' });
                modMassMono = double.Parse(parts[0], CultureInfo.InvariantCulture);
                modMassAvg = double.Parse(parts[1], CultureInfo.InvariantCulture);
                formula = string.Empty;
            }

            return MoleculeMassOffset.Create(formula, 
                TypedMass.Create(modMassMono, MassType.Monoisotopic),
                TypedMass.Create(modMassAvg, MassType.Average));
        }

        public TypedMass GetMass(MassType massType)
        {
            return massType.IsMonoisotopic()
                ? TypedMass.Create(BioMassCalcBase.MONO.ParseMass(this.Dictionary) + MonoMassOffset, MassType.Monoisotopic)
                : TypedMass.Create(BioMassCalcBase.AVG.ParseMass(this.Dictionary) + AverageMassOffset, MassType.Average);
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

            var newMolecule = new Dictionary<string, int>(this)
            {
                [element] = count
            };
            return new MoleculeMassOffset(newMolecule, MonoMassOffset, AverageMassOffset);
        }

        public TypedMass MonoMassOffset { get; private set; }
        public TypedMass AverageMassOffset { get; private set; }

        public bool IsEmpty =>
            ReferenceEquals(this, MoleculeMassOffset.EMPTY) ||
            (IsMassOnly && AverageMassOffset == 0 && MonoMassOffset == 0);

        public static bool IsNullOrEmpty(MoleculeMassOffset moleculeMassOffset) =>
            moleculeMassOffset == null || moleculeMassOffset.IsEmpty;

        public bool IsMassOnly => Count == 0;

        public bool HasMassModifications => MonoMassOffset != 0;

        public TypedMass GetMassOffset(MassType t) => t.IsMonoisotopic() ? MonoMassOffset : AverageMassOffset;

        public MoleculeMassOffset Plus(MoleculeMassOffset moleculeMassOffset)
        {
            var newMolecule = MoleculeFromEntries(Dictionary.Concat(moleculeMassOffset.Dictionary));
            return ChangeFormula(newMolecule).ChangeMassOffset(MonoMassOffset + moleculeMassOffset.MonoMassOffset, AverageMassOffset + moleculeMassOffset.AverageMassOffset);
        }

        public MoleculeMassOffset Plus(Molecule moleculeMassOffset)
        {
            var newMolecule = MoleculeFromEntries(Dictionary.Concat(moleculeMassOffset.Dictionary));
            return ChangeFormula(newMolecule);
        }

        public MoleculeMassOffset Minus(MoleculeMassOffset moleculeMassOffset)
        {
            var newMolecule = MoleculeFromEntries(Dictionary.Concat(
                moleculeMassOffset.Dictionary.Select(entry => new KeyValuePair<string, int>(entry.Key, -entry.Value))));
            return ChangeFormula(newMolecule).ChangeMassOffset(MonoMassOffset - moleculeMassOffset.MonoMassOffset, AverageMassOffset - moleculeMassOffset.AverageMassOffset);
        }

        public static string FormatMassModification(double massMod, int desiredDecimals = 6)
        {
            var sign = massMod > 0 ? @"+" : string.Empty;
            return string.Format($@"[{sign}{massMod.ToString($"F{desiredDecimals}", CultureInfo.InvariantCulture).TrimEnd('0')}]");
        }

        public static string FormatMassModification(double massModMono, double massModAverage, int desiredDecimals = 6)
        {
            if (Equals(massModMono, massModAverage))
            {
                return FormatMassModification(massModMono, desiredDecimals);
            }
            var sign = massModMono > 0 ? @"+" : string.Empty;
            return string.Format($@"[{sign}{massModMono.ToString($"F{desiredDecimals}", CultureInfo.InvariantCulture).TrimEnd('0')}/{Math.Abs(massModAverage).ToString($"F{desiredDecimals}", CultureInfo.InvariantCulture).TrimEnd('0')}]");
        }

        public string ChemicalFormulaPart()
        {
            // Nicely format the part of the object that's described as a chemical formula.
            if (IsMassOnly)
            {
                return string.Empty;
            }

            return base.ToString();
        }


        public override string ToString()
        {
            return ToString();
        }

        public string ToStringInvariant()
        {
            return ToString();
        }

        public string ToString(int desiredDigits = 6)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(ChemicalFormulaPart());

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
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
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
            var d = base.CompareTo(other);
            if (d != 0)
            {
                return d;
            }
            d = MonoMassOffset.CompareTo(other.MonoMassOffset);
            if (d != 0)
            {
                return d;
            }
            return AverageMassOffset.CompareTo(other.AverageMassOffset);
        }
    }
}
