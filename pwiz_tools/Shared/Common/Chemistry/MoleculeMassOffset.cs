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
    /// </summary>
    public class MoleculeMassOffset : Immutable, IFormattable
    {
        public static readonly MoleculeMassOffset EMPTY = new MoleculeMassOffset(Molecule.Empty, 0, 0);
        public MoleculeMassOffset(Molecule molecule, double monoMassOffset, double averageMassOffset)
        {
            Molecule = molecule;
            MonoMassOffset = monoMassOffset;
            AverageMassOffset = averageMassOffset;
        }

        public MoleculeMassOffset(Molecule molecule) : this(molecule, 0, 0)
        {

        }

        public Molecule Molecule { get; private set; }
        public double MonoMassOffset { get; private set; }
        public double AverageMassOffset { get; private set; }

        public MoleculeMassOffset Plus(MoleculeMassOffset moleculeMassOffset)
        {
            var newMolecule = MoleculeFromEntries(Molecule.Concat(moleculeMassOffset.Molecule));
            return new MoleculeMassOffset(newMolecule, MonoMassOffset + moleculeMassOffset.MonoMassOffset, AverageMassOffset + moleculeMassOffset.AverageMassOffset);
        }

        public MoleculeMassOffset Minus(MoleculeMassOffset moleculeMassOffset)
        {
            var newMolecule = MoleculeFromEntries(Molecule.Concat(
                moleculeMassOffset.Molecule.Select(entry => new KeyValuePair<string, int>(entry.Key, -entry.Value))));
            return new MoleculeMassOffset(newMolecule, MonoMassOffset - moleculeMassOffset.MonoMassOffset, AverageMassOffset - moleculeMassOffset.AverageMassOffset);
        }

        public override string ToString()
        {
            return ToString(null, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(Molecule);
            
            if (MonoMassOffset != 0 || AverageMassOffset != 0)
            {
                if (Equals(MonoMassOffset, AverageMassOffset))
                {
                    stringBuilder.Append(ToSignedString(format, formatProvider, MonoMassOffset));
                }
                else
                {
                    stringBuilder.Append(@"(" + ToSignedString(format, formatProvider, MonoMassOffset) + "," +
                                         ToSignedString(format, formatProvider, AverageMassOffset) + ")");
                }
            }

            return stringBuilder.ToString();
        }

        private static string ToSignedString(string format, IFormatProvider formatProvider, double value)
        {
            return (value >= 0 ? @"+" : string.Empty) + value.ToString(format, formatProvider);
        }

        protected bool Equals(MoleculeMassOffset other)
        {
            return Molecule.Equals(other.Molecule) && MonoMassOffset.Equals(other.MonoMassOffset) && AverageMassOffset.Equals(other.AverageMassOffset);
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
