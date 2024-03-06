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
            Molecule = molecule ?? Molecule.Empty;
            MonoMassOffset = monoMassOffset;
            AverageMassOffset = averageMassOffset;
        }

        public Molecule Molecule { get; private set; }
        public double MonoMassOffset { get; private set; }
        public double AverageMassOffset { get; private set; }
        public double GetMassOffset(bool bMono) => bMono ? MonoMassOffset : AverageMassOffset;
        public bool IsEmpty => Equals(EMPTY);
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

        public MoleculeMassOffset Plus(MoleculeMassOffset moleculeMassOffset)
        {
            return Sum(new[] { this, moleculeMassOffset });
        }

        public MoleculeMassOffset TimesMinusOne()
        {
            return Create(Molecule.TimesMinusOne(), -MonoMassOffset, -AverageMassOffset);
        }

        public MoleculeMassOffset Minus(MoleculeMassOffset moleculeMassOffset)
        {
            return Plus(moleculeMassOffset.TimesMinusOne());
        }

        public static MoleculeMassOffset Sum(IEnumerable<MoleculeMassOffset> parts)
        {
            return Sum(parts.ToList());
        }

        private static MoleculeMassOffset Sum(IList<MoleculeMassOffset> parts)
        {
            var monoMassOffset = parts.Sum(part => part.MonoMassOffset);
            var averageMassOffset = parts.Sum(part => part.AverageMassOffset);
            return Create(Formula<Molecule>.Sum(parts.Select(mol => mol.Molecule)), monoMassOffset, averageMassOffset);
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
        
    }
}
