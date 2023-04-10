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
        private MoleculeMassOffset(Molecule molecule, double monoMassOffset, double averageMassOffset)
        {
            Molecule = molecule;
            MonoMassOffset = monoMassOffset;
            AverageMassOffset = averageMassOffset;
        }

        public Molecule Molecule { get; private set; }
        public double MonoMassOffset { get; private set; }
        public double AverageMassOffset { get; private set; }

        public double GetMassOffset(bool bMono) => bMono ? MonoMassOffset : AverageMassOffset;
        public bool IsEmpty =>
            ReferenceEquals(this, MoleculeMassOffset.EMPTY) ||
            (Molecule.IsNullOrEmpty(Molecule) && AverageMassOffset == 0 && MonoMassOffset == 0);

        public static bool IsNullOrEmpty(MoleculeMassOffset moleculeMassOffset) =>
            moleculeMassOffset == null || moleculeMassOffset.IsEmpty;

        public static MoleculeMassOffset Create(Molecule molecule) => Create(molecule, null, null);

        public static MoleculeMassOffset Create(Molecule molecule, double? monoMassOffset, double? averageMassOffset)
        {
            return Molecule.IsNullOrEmpty(molecule) && (monoMassOffset??0) == 0 && (averageMassOffset??0) == 0 ?
                EMPTY :
                new MoleculeMassOffset(molecule, monoMassOffset ?? averageMassOffset ?? 0, averageMassOffset ?? monoMassOffset ?? 0);
        }

        public MoleculeMassOffset ChangeMolecule(Molecule molecule)
        {
            if (Molecule.Equals(molecule))
            {
                return this;
            }
            return Create(molecule, MonoMassOffset, AverageMassOffset);
        }

        public static MoleculeMassOffset Sum(IEnumerable<MoleculeMassOffset> parts)
        {
            var array = parts.ToArray();
            var monoMassOffset = array.Sum(part => part.MonoMassOffset);
            var averageMassOffset = array.Sum(part => part.AverageMassOffset);
            return MoleculeMassOffset.Create(MoleculeFromEntries(array.SelectMany(part => part.Molecule??Molecule.Empty)), monoMassOffset, averageMassOffset);
        }
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

        /// <summary>
        /// Separate the formula and mass offset declarations in a string presumed to describe a chemical formula and/or mass offsets.
        /// Is aware of simple math, e.g. "C12H5[-1.2/1.21]-C2H[-1.1]" => "C12H5-C2H", 0.1, 0.11
        /// </summary>
        /// <param name="formulaAndMasses">input string  e.g. "C12H5", "C12H5[-1.2/1.21]", "[+1.3/1.31]", "1.3/1.31", "C12H5[-1.2/1.21]-C2H[-1.1]"</param>
        /// <param name="formula">string with any mass modifiers stripped out e.g.  "C12H5[-1.2/1.21]-C2H[-1.1]" ->  "C12H5-C2H" </param>
        /// <param name="modMassMono">effect of any mono mass modifiers e.g. "C12H5[-1.2/1.21]-C2H[-1.1]" => 0.1 </param>
        /// <param name="modMassAverage">effect of any avg mass modifiers e.g. "C12H5[-1.2/1.21]-C2H[-1.1]" => 0.11</param>
        public static void SplitFormulaAndMasses(string formulaAndMasses, out string formula, out double? modMassMono, out double? modMassAverage)
        {
            modMassMono = null;
            modMassAverage = null;
            formula = formulaAndMasses?.Trim();
            if (string.IsNullOrEmpty(formula))
            {
                return;
            }
            // A few different possibilities here, e.g. "C12H5", "C12H5[-1.2/1.21]", "[+1.3/1.31]", "1.3/1.31"
            // Also possibly  "C12H5[-1.2/1.21]-C2H[-1.1]"
            var position = 0;
            while (MoleculeMassOffset.StringContainsMassOffsetCue(formula))
            {
                var cuePlus = formula.IndexOf(MoleculeMassOffset.MASS_MOD_CUE_PLUS, position, StringComparison.InvariantCulture);
                var cueMinus = formula.IndexOf(MoleculeMassOffset.MASS_MOD_CUE_MINUS, position, StringComparison.InvariantCulture);
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
                var parts = formula.Substring(position, close - position).Split('/');
                double negate = Equals(cueMinus, position) ? -1 : 1;
                if (formula.Substring(0, position).Contains('-'))
                {
                    negate *= -1;
                }
                var monoMass = negate * double.Parse(parts[0].Substring(2).Trim(), CultureInfo.InvariantCulture);
                modMassMono = (modMassMono??0) + monoMass;
                modMassAverage = (modMassAverage??0) + (parts.Length > 1 ? negate * double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture) : monoMass);
                formula = formula.Substring(0, position) + formula.Substring(close + 1);
            }
            if (formula.Contains(@"/"))
            {
                // e.g.  "1.3/1.31"
                var parts = formula.Split(new[] { '/' });
                modMassMono = double.Parse(parts[0], CultureInfo.InvariantCulture);
                modMassAverage = double.Parse(parts[1], CultureInfo.InvariantCulture);
                formula = string.Empty;
            }
        }


        public override string ToString()
        {
            return ToString(6);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return ToString(); // Always written with invariant culture
        }

        public string ToString(int desiredDigits)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(Molecule);
            stringBuilder.Append(FormatMassModification(MonoMassOffset, AverageMassOffset, desiredDigits));
            return stringBuilder.ToString();
        }


        public static string MASS_MOD_CUE_PLUS = @"[+";   // e.g. C12H5[+15.994915/16.1]
        public static string MASS_MOD_CUE_MINUS = @"[-";  // e.g. C12H5[-15.994915/16.1]

        public static bool StringContainsMassOffsetCue(string str) => str.Contains(MASS_MOD_CUE_PLUS) || str.Contains(MASS_MOD_CUE_MINUS);

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

        public static string FormatMassModification(double massMod, int desiredDecimals)
        {
            if (massMod == 0)
            {
                return string.Empty;
            }
            var sign = massMod > 0 ? @"+" : string.Empty;
            return string.Format($@"[{sign}{massMod.ToString($"F{desiredDecimals}", CultureInfo.InvariantCulture).TrimEnd('0')}]");
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
