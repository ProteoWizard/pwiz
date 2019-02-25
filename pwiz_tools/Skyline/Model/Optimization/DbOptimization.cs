/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Globalization;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

// ReSharper disable VirtualMemberCallInConstructor

namespace pwiz.Skyline.Model.Optimization
{
    public enum OptimizationType
    {
        unknown = 0,
        collision_energy = 1,
        declustering_potential = 2,
        compensation_voltage_rough = 3,
        compensation_voltage_medium = 4,
        compensation_voltage_fine = 5,
    }

    public class OptimizationKey : IComparable
    {
        public OptimizationType OptType { get; set; }
        public Target PeptideModSeq { get; set; }
        public Adduct PrecursorAdduct { get; set; }
        public string FragmentIon { get; set; }
        public Adduct ProductAdduct { get; set; }

        public OptimizationKey()
        {
            OptType = OptimizationType.unknown;
        }

        public OptimizationKey(OptimizationType optType, Target peptideModSeq, Adduct precursorAdduct, string fragmentIon, Adduct productAdduct)
        {
            OptType = optType;
            PeptideModSeq = peptideModSeq;
            PrecursorAdduct = precursorAdduct;
            FragmentIon = fragmentIon;
            ProductAdduct = productAdduct;
        }

        public OptimizationKey(OptimizationKey other)
            : this(other.OptType, other.PeptideModSeq, other.PrecursorAdduct, other.FragmentIon, other.ProductAdduct)
        {
        }

        public override string ToString()  // For debugging
        {
            if (PeptideModSeq.IsProteomic)
              return !string.IsNullOrEmpty(FragmentIon)
                    ? string.Format(@"{0} (charge {1}); {2} (charge {3})", PeptideModSeq, PrecursorAdduct, FragmentIon, ProductAdduct)
                    : string.Format(@"{0} (charge {1})", PeptideModSeq, PrecursorAdduct);
            return !string.IsNullOrEmpty(FragmentIon)
                ? string.Format(@"{0}{1}; {2}{3}", PeptideModSeq, PrecursorAdduct, FragmentIon, ProductAdduct)
                : string.Format(@"{0}{1}", PeptideModSeq, PrecursorAdduct);
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }
            var other = obj as OptimizationKey;
            if (other == null)
            {
                throw new ArgumentException(Resources.OptimizationKey_CompareTo_Cannot_compare_OptimizationKey_to_an_object_of_a_different_type);
            }
            else if (!Equals(OptType, other.OptType))
            {
                return OptType.CompareTo(other.OptType);
            }
            else if (!Equals(PeptideModSeq, other.PeptideModSeq))
            {
                return PeptideModSeq.CompareTo(other.PeptideModSeq);
            }
            else if (!PrecursorAdduct.Equals(other.PrecursorAdduct))
            {
                return PrecursorAdduct.CompareTo(other.PrecursorAdduct);
            }
            else if (FragmentIon != other.FragmentIon)
            {
                return String.Compare(FragmentIon, other.FragmentIon, StringComparison.InvariantCulture);
            }
            else
            {
                return ProductAdduct.CompareTo(other.ProductAdduct);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }
            var other = obj as OptimizationKey;
            if (other == null)
            {
                return false;
            }
            return OptType.Equals(other.OptType) &&
                Equals(PeptideModSeq, other.PeptideModSeq) &&
                Equals(PrecursorAdduct, other.PrecursorAdduct) &&
                FragmentIon == other.FragmentIon &&
                Equals(ProductAdduct, other.ProductAdduct);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (PeptideModSeq != null ? PeptideModSeq.GetHashCode() : 0);
                result = (result*397) ^ OptType.GetHashCode();
                result = (result*397) ^ PrecursorAdduct.GetHashCode();
                if (FragmentIon != null)
                    result = (result*397) ^ FragmentIon.GetHashCode();
                result = (result*397) ^ ProductAdduct.GetHashCode();
                return result;
            }
        }
    }

    public class DbOptimization : DbEntity, IPeptideData
    {
        protected DbOptimization()
        {
            Key = new OptimizationKey();
        }
        public override Type EntityClass
        {
            get { return typeof(DbOptimization); }
        }

        public virtual OptimizationKey Key { get; set; }
        public virtual double Value { get; set; }

        public virtual string PeptideModSeq
        {
            get { return Key.PeptideModSeq.ToSerializableString(); }
            set { Key.PeptideModSeq = Target.FromSerializableString(value); }
        }
        public virtual Adduct Adduct { get { return Key.PrecursorAdduct; } set { Key.PrecursorAdduct = value; } }
        public virtual string Charge
        {
            get
            {
                return Key.PrecursorAdduct.ToString(CultureInfo.InvariantCulture);
            }
            set
            {
                Key.PrecursorAdduct = int.TryParse(value, out var z) ? Adduct.FromStringAssumeProtonated(value) : Adduct.FromStringAssumeProtonatedNonProteomic(value);
            }
        }
        public virtual string FragmentIon { get { return Key.FragmentIon; } set { Key.FragmentIon = value; } }
        public virtual Adduct ProductAdduct { get { return Key.ProductAdduct; } set { Key.ProductAdduct = value; } }
        public virtual string ProductCharge
        {
            get
            {
                return Key.ProductAdduct.ToString(CultureInfo.InvariantCulture);
            }
            set
            {
                Key.ProductAdduct = int.TryParse(value, out var z) ? Adduct.FromStringAssumeProtonated(value) : Adduct.FromStringAssumeProtonatedNonProteomic(value);
            }
        }
        public virtual int Type
        {
            get
            {
                return (int)Key.OptType;
            }
            set
            {
                CheckEnumValue(value);
                Key.OptType = (OptimizationType)value;
            }
        }

        public virtual Target Target { get { return Key.PeptideModSeq; } }

        public DbOptimization(OptimizationKey key, double value)
            : this(key.OptType, key.PeptideModSeq, key.PrecursorAdduct, key.FragmentIon, key.ProductAdduct, value)
        {
        }

        public DbOptimization(DbOptimization other)
            : this(other.Key, other.Value)
        {
            Id = other.Id;
        }

        public DbOptimization(OptimizationType type, Target seq, Adduct charge, string fragmentIon, Adduct productCharge, double value)
        {
            Key = new OptimizationKey(type, seq, charge, fragmentIon, productCharge);
            Value = value;
        }

        protected void CheckEnumValue(int type)
        {
            if (!Enum.IsDefined(typeof (OptimizationType), type))
            {
                throw new ArgumentException(Resources.DbOptimization_DbOptimization_Optimization_type_out_of_range);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }
            var other = obj as DbOptimization;
            if (other == null)
            {
                return false;
            }
            return Equals(Key, other.Key) && Equals(Value, other.Value);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Key.GetHashCode() * 397) ^ Value.GetHashCode();
            }
        }
    }
}
