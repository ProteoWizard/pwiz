/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
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

using pwiz.Common.SystemUtil;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Diagnostics.Contracts;


namespace pwiz.Skyline.Util
{
    /// <summary>
    /// There are many places where we carry a mass or massH and also need to track how it was derived
    /// </summary>
    public struct TypedMass :  IComparable<TypedMass>, IEquatable<TypedMass>, IFormattable
    {
        public static TypedMass ZERO_AVERAGE_MASSNEUTRAL = new TypedMass(0.0, MassType.Average);
        public static TypedMass ZERO_MONO_MASSNEUTRAL = new TypedMass(0.0, MassType.Monoisotopic);

        public static TypedMass ZERO_AVERAGE_MASSH = new TypedMass(0.0, MassType.AverageMassH);
        public static TypedMass ZERO_MONO_MASSH = new TypedMass(0.0, MassType.MonoisotopicMassH);

        public static TypedMass ZERO_AVERAGE_MASSNEUTRAL_HEAVY = new TypedMass(0.0, MassType.AverageHeavy);
        public static TypedMass ZERO_MONO_MASSNEUTRAL_HEAVY = new TypedMass(0.0, MassType.MonoisotopicHeavy);

        public static TypedMass ZERO_AVERAGE_MASSH_HEAVY = new TypedMass(0.0, MassType.AverageMassH | MassType.bHeavy);
        public static TypedMass ZERO_MONO_MASSH_HEAVY = new TypedMass(0.0, MassType.MonoisotopicMassH | MassType.bHeavy);


        private readonly double _value;
        private readonly MassType _massType;

        public double Value { get { return _value; } }
        public MassType MassType { get { return _massType; } }
        [Pure]
        public bool IsMassH() { return _massType.IsMassH();  }
        [Pure]
        public bool IsMonoIsotopic() { return _massType.IsMonoisotopic(); }
        [Pure]
        public bool IsAverage() { return _massType.IsAverage(); }
        [Pure]
        public bool IsHeavy() { return _massType.IsHeavy(); }
        [Pure]
        public static bool IsNullOrEmpty(TypedMass t) => t._value == 0;

        private TypedMass(double value, MassType t)
        {
            _value = value;
            _massType = t;
        }

        // Use this instead of ctor to avoid lots of copies of ZERO_*_*
        public static TypedMass Create(double value, MassType t)
        {
            if (value == 0)
            {
                if (t.IsAverage())
                {
                    return t.IsHeavy() ?
                        (t.IsMassH() ? ZERO_AVERAGE_MASSH_HEAVY : ZERO_AVERAGE_MASSNEUTRAL_HEAVY) :
                        (t.IsMassH() ? ZERO_AVERAGE_MASSH : ZERO_AVERAGE_MASSNEUTRAL);
                }

                return t.IsHeavy() ?
                    (t.IsMassH() ? ZERO_MONO_MASSH_HEAVY : ZERO_MONO_MASSNEUTRAL_HEAVY) : 
                    (t.IsMassH() ? ZERO_MONO_MASSH : ZERO_MONO_MASSNEUTRAL);
            }

            return new TypedMass(value, t);
        }

        [Pure]
        public bool Equivalent(TypedMass other)
        {
            if (IsMassH() != other.IsMassH())
            {
                var adjust = IsMassH() ? - BioMassCalc.MassProton : BioMassCalc.MassProton;
                return Math.Abs(_value + adjust - other.Value) < BioMassCalc.MassElectron;
            }
            return Equals(other); // Can't lead with this, as it will throw if IsMassH doesn't agree
        }

        public TypedMass ChangeIsMassH(bool newIsMassH)
        {
            if (Equals(newIsMassH, IsMassH()))
            {
                return this;
            }
            return TypedMass.Create(_value, newIsMassH ? _massType | MassType.bMassH : _massType & ~MassType.bMassH);
        }

        public TypedMass ChangeMass(double mass)
        {
            return (_value != mass) ? TypedMass.Create(mass, _massType) : this;
        }

        public TypedMass ChangeIsHeavy(bool newIsHeavy)
        {
            if (Equals(newIsHeavy, IsHeavy()))
            {
                return this;
            }
            return TypedMass.Create(_value, newIsHeavy ? _massType | MassType.bHeavy : _massType & ~MassType.bHeavy);
        }

        public TypedMass ChangeIsMonoIsotopic(bool bIsMono)
        {
            return (bIsMono == IsMonoIsotopic()) ?
                this :
                TypedMass.Create(_value, (_massType & ~(MassType.Monoisotopic | MassType.Average)) | (bIsMono ? MassType.Monoisotopic : MassType.Average));
        }

        public TypedMass ChangeMassType(MassType massType)
        {
            return (massType == _massType) ?
                this :
                TypedMass.Create(_value, massType);
        }

        public static implicit operator double(TypedMass d)
        {
            return d.Value;
        }

        public static TypedMass operator +(TypedMass tm, double step)
        {
            return step == 0 ? tm : TypedMass.Create(tm.Value + step, tm._massType);
        }

        public static TypedMass operator -(TypedMass tm, double step)
        {
            return step == 0 ? tm : TypedMass.Create(tm.Value - step, tm._massType);
        }

        public static TypedMass operator+(TypedMass tm, TypedMass step)
        {
            return step.Value == 0 && tm._massType == step._massType ? // If step is zero, and the mass types are the same, return the original
                tm : 
                tm.Value == 0 && tm._massType == step._massType ? // If tm is zero, and the mass types are the same, return the step
                step :
                Create(tm.Value + step.Value, tm._massType | step._massType); // Make sure that the mass type includes bHeavy if either is heavy
        }

        public static TypedMass operator -(TypedMass tm, TypedMass step)
        {
            return step.Value == 0 && tm._massType == step._massType ? // If step is zero, and the mass types are the same, return the original
                tm : 
                Create(tm.Value - step.Value, tm._massType | step._massType); // Make sure that the mass type includes bHeavy if either is heavy
        }

        public static TypedMass operator *(TypedMass tm, double step)
        {
            return step == 1 ? tm : TypedMass.Create(tm.Value * step, tm._massType);
        }

        public int CompareTo(TypedMass other)
        {
            Debug.Assert(_massType.IsMonoisotopic() == other._massType.IsMonoisotopic());  // It's a mistake to compare mono and average
            var d = Value.CompareTo(other.Value);
            return d;
        }

        public int CompareTolerant(TypedMass other, double tolerance)
        {
            var d = CompareTo(other);
            if (d != 0)
            {
                if (Math.Abs(Value - other.Value) <= tolerance)
                {
                    return 0;
                }
            }
            return d;
        }

        public static bool operator <(TypedMass massA, TypedMass massB)
        {
            return massA.CompareTo(massB) < 0;
        }

        public static bool operator <=(TypedMass massA, TypedMass massB)
        {
            return massA.CompareTo(massB) <= 0;
        }

        public static bool operator >=(TypedMass massA, TypedMass massB)
        {
            return massA.CompareTo(massB) >= 0;
        }

        public static bool operator >(TypedMass massA, TypedMass massB)
        {
            return massA.CompareTo(massB) > 0;
        }

        public static bool operator ==(TypedMass massA, TypedMass massB)
        {
            return massA.CompareTo(massB) == 0;
        }

        public static bool operator !=(TypedMass massA, TypedMass massB)
        {
            return !(massA == massB);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TypedMass)obj);
        }

        public bool Equals(TypedMass other)
        {
            return CompareTo(other) == 0;
        }

        public bool Equals(TypedMass other, double tolerance)
        {
            return CompareTo(other) == 0 || Math.Abs(Value - other.Value) <= tolerance;
        }

        public override int GetHashCode()
        {
            var result = Value.GetHashCode();
            result = (result * 397) ^ _massType.GetHashCode();
            return result;
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.CurrentCulture);
        }

        public string ToString(CultureInfo ci)
        {
            return Value.ToString(ci);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return Value.ToString(format, formatProvider);
        }

    }

    /// <summary>
    /// Enum used to specify the use of monoisotopic or average
    /// masses when calculating molecular masses.
    /// </summary>
    [Flags]
    [IgnoreEnumValues(new object[] {
        bMassH,
        bHeavy,
        MonoisotopicMassH,
        AverageMassH,
        MonoisotopicHeavy,
        AverageHeavy})]
    public enum MassType
    {
        // ReSharper disable InconsistentNaming
        Monoisotopic = 0,
        Average = 1,
        bMassH = 2, // As with peptides, where masses are traditionally given as massH
        bHeavy = 4, // As with small molecules described by mass only, which have already been processed by isotope-declaring adducts
        MonoisotopicMassH = Monoisotopic | bMassH,
        AverageMassH = Average | bMassH,
        MonoisotopicHeavy = Monoisotopic | bHeavy,
        AverageHeavy = Average | bHeavy
        // ReSharper restore InconsistentNaming
    }

    public static class MassTypeExtension
    {
        [Pure]
        public static bool IsMonoisotopic(this MassType val)
        {
            return !val.IsAverage();
        }

        [Pure]
        public static bool IsAverage(this MassType val)
        {
            return (val & MassType.Average) != 0;
        }

        [Pure]
        public static bool IsMassH(this MassType val)
        {
            return (val & MassType.bMassH) != 0;
        }

        // For small molecule use: distinguishes a mass calculated from an isotope-specifying adduct
        [Pure]
        public static bool IsHeavy(this MassType val)
        {
            return (val & MassType.bHeavy) != 0;
        }
    }


}
