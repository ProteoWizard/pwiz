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
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Chemistry
{
    /// <summary>
    /// Holds a chemical formula as well as an extra offset for the monoisotopic mass and the average mass.
    /// This is used when a molecule is made up of some things that we know the chemical formula for,
    /// and other things where the user has only told us the mono and average masses.
    ///
    /// Examples of these formulas with mass offsets include crosslinking and Hardklor output.
    ///
    /// </summary>
    public class MoleculeMassOffset : Immutable, IFormattable, IEquatable<MoleculeMassOffset>, IComparable<MoleculeMassOffset>
    {
        public static readonly MoleculeMassOffset EMPTY = new MoleculeMassOffset(Molecule.Empty, TypedMass.ZERO_MONO_MASSNEUTRAL, TypedMass.ZERO_AVERAGE_MASSNEUTRAL);

        protected MoleculeMassOffset(Molecule molecule, TypedMass monoMassOffset, TypedMass averageMassOffset) // Use Create instead of constructor to avoid creating empty objects
        {
            Molecule = molecule ?? Molecule.Empty;
            MonoMassOffset = monoMassOffset;
            AverageMassOffset = averageMassOffset;
        }

        public Molecule Molecule { get; private set; }
        public TypedMass MonoMassOffset { get; protected set; }
        public TypedMass AverageMassOffset { get; protected set; }

        public static string MASS_MOD_CUE_PLUS = @"[+";   // e.g. C12H5[+15.994915/16.1]
        public static string MASS_MOD_CUE_MINUS = @"[-";  // e.g. C12H5[-15.994915/16.1]

        public static bool ContainsMassOffsetCue(string str) => str.Contains(MASS_MOD_CUE_PLUS) || str.Contains(MASS_MOD_CUE_MINUS);

        public bool IsEmpty =>
            ReferenceEquals(this, MoleculeMassOffset.EMPTY) ||
            (IsMassOnly && AverageMassOffset == 0 && MonoMassOffset == 0);

        public static bool IsNullOrEmpty(MoleculeMassOffset moleculeMassOffset) =>
            moleculeMassOffset == null || moleculeMassOffset.IsEmpty;

        public TypedMass GetMassOffset(MassType massType) => massType.IsMonoisotopic() ? MonoMassOffset : AverageMassOffset;

        public bool HasChemicalFormula => !Molecule.IsNullOrEmpty(Molecule);

        public bool IsMassOnly => Molecule.IsNullOrEmpty(Molecule);

        public bool HasMassModifications => MonoMassOffset != 0;

        public MoleculeMassOffset SetElementCount(string element, int count)
        {
            var newMolecule = Molecule.SetElementCount(element, count);
            return Change(newMolecule, MonoMassOffset, AverageMassOffset);
        }

        public bool HasElement(string element) => Molecule.ContainsKey(element);

        public int GetElementCount(string element)
        {
            Molecule.TryGetValue(element, out var count);
            return count;
        }

        public MoleculeMassOffset AdjustElementCount(string element, int delta)
        {
            var newMolecule = Molecule.AdjustElementCount(element, delta, true); // Deal with the formula
            return Change(newMolecule, MonoMassOffset, AverageMassOffset);
        }

        public MoleculeMassOffset Plus(MoleculeMassOffset moleculeMassOffset)
        {
            var newMolecule = Molecule.Plus(moleculeMassOffset.Molecule);
            return Change(newMolecule, MonoMassOffset + moleculeMassOffset.MonoMassOffset, AverageMassOffset + moleculeMassOffset.AverageMassOffset);
        }

        public MoleculeMassOffset Difference(MoleculeMassOffset moleculeMassOffset)
        {
            var newMolecule = Molecule.Difference(moleculeMassOffset.Molecule);
            return Change(newMolecule, MonoMassOffset - moleculeMassOffset.MonoMassOffset, AverageMassOffset - moleculeMassOffset.AverageMassOffset);
        }

        public MoleculeMassOffset ChangeIsMassH(bool isMassH)
        {
            return ChangeMassOffset(MonoMassOffset.ChangeIsMassH(isMassH), AverageMassOffset.ChangeIsMassH(isMassH));
        }

        public MoleculeMassOffset ChangeIsHeavy(bool isHeavy)
        {
            return ChangeMassOffset(MonoMassOffset.ChangeIsHeavy(isHeavy), AverageMassOffset.ChangeIsHeavy(isHeavy));
        }

        public bool IsHeavy => MonoMassOffset.IsHeavy();

        public MoleculeMassOffset ChangeMassOffset(TypedMass mono, TypedMass avg)
        {
            return Change(Molecule, mono, avg);
        }

        public MoleculeMassOffset ChangeMassOffset(double mass)
        {
            return Change(Molecule, MonoMassOffset.ChangeMass(mass), AverageMassOffset.ChangeMass(mass));
        }

        public MoleculeMassOffset Change(Molecule molecule)
        {
            return Change(molecule, MonoMassOffset, AverageMassOffset);
        }
        
        public virtual MoleculeMassOffset Change(Molecule molecule, TypedMass monoMassOffset, TypedMass averageMassOffset)
        {
            return Equals(molecule, Molecule) && monoMassOffset == MonoMassOffset && averageMassOffset == AverageMassOffset
                ? this
                : MoleculeMassOffset.Create(molecule, monoMassOffset, averageMassOffset);
        }

        public static MoleculeMassOffset Sum(IEnumerable<MoleculeMassOffset> parts)
        {
            var array = parts.ToArray();
            var monoMassOffset = array.Sum(part => part.MonoMassOffset);
            var averageMassOffset = array.Sum(part => part.AverageMassOffset);
            return MoleculeMassOffset.Create(MoleculeFromEntries(array.SelectMany(part => part.Molecule)), 
                TypedMass.Create(monoMassOffset, MassType.Monoisotopic), TypedMass.Create(averageMassOffset, MassType.Average));
        }

        public static MoleculeMassOffset Create(Molecule molecule, double? monoMassOffset, double? averageMassOffset)
        {
            return Create(molecule,
                TypedMass.Create(monoMassOffset, MassType.Monoisotopic), TypedMass.Create(averageMassOffset, MassType.Average));
        }

        public static MoleculeMassOffset Create(Molecule molecule, TypedMass monoMassOffset, TypedMass averageMassOffset)
        {
            if (monoMassOffset == null)
            {
                monoMassOffset = TypedMass.ZERO_MONO_MASSNEUTRAL;
            }
            if (averageMassOffset == null)
            {
                averageMassOffset = TypedMass.ZERO_AVERAGE_MASSNEUTRAL;
            }

            return Molecule.IsNullOrEmpty(molecule) &&
                   monoMassOffset== 0 && averageMassOffset == 0 ?
                EMPTY :
                new MoleculeMassOffset(molecule, monoMassOffset, averageMassOffset);
        }

        // N.B. The mass portion is always written with InvariantCulture.

        public static string FormatMassModification(double massMod, int desiredDecimals)
        {
            if (massMod == 0)
            {
                return string.Empty;
            }
            var sign = massMod > 0 ? @"+" : string.Empty;
            return string.Format($@"[{sign}{massMod.ToString($"F{desiredDecimals}", CultureInfo.InvariantCulture).TrimEnd('0')}]");
        }

        public static string FormatMassModification(double massModMono, double massModAverage, int desiredDecimals)
        {
            if (massModMono == 0 && massModAverage == 0)
            {
                return string.Empty;
            }
            if (Equals(massModMono, massModAverage))
            {
                return FormatMassModification(massModMono, desiredDecimals);
            }
            var sign = massModMono > 0 ? @"+" : string.Empty;
            return string.Format($@"[{sign}{massModMono.ToString($"F{desiredDecimals}", CultureInfo.InvariantCulture).TrimEnd('0')}/{Math.Abs(massModAverage).ToString($"F{desiredDecimals}", CultureInfo.InvariantCulture).TrimEnd('0')}]");
        }

        public override string ToString()
        {
            return ToString(null, CultureInfo.CurrentCulture);
        }

        // Satisfy IFormattable interface
        public string ToString(string format, IFormatProvider formatProvider)
        {
            return ToString(6);
        }

        public virtual string ChemicalFormulaString() => Molecule.ToString();
        
        public string ToString(int desiredDigits)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(ChemicalFormulaString());
            stringBuilder.Append(FormatMassModification(MonoMassOffset, AverageMassOffset, desiredDigits)); // Use our standard notation e.g. [+1.23/1.24], [-1.2] etc
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
                var hashCode = Molecule.GetHashCode();
                hashCode = (hashCode * 397) ^ MonoMassOffset.GetHashCode();
                hashCode = (hashCode * 397) ^ AverageMassOffset.GetHashCode();
                return hashCode;
            }
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



        private static Molecule MoleculeFromEntries(IEnumerable<KeyValuePair<string, int>> entries)
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

            return Molecule.FromDict(dictionary);
        }
    }
}
