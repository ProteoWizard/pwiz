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
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Optimization
{
    public enum OptimizationType { unknown = 0, collision_energy = 1, declustering_potential = 2 }

    public class OptimizationKey : IComparable
    {
        public OptimizationType OptType { get; set; }
        public string PeptideModSeq { get; set; }
        public int Charge { get; set; }
        public double Mz { get; set; }

        public OptimizationKey()
        {
            OptType = OptimizationType.unknown;
        }

        public OptimizationKey(OptimizationType optType, string peptideModSeq, int charge, double mz)
        {
            OptType = optType;
            PeptideModSeq = peptideModSeq;
            Charge = charge;
            Mz = mz;
        }

        public OptimizationKey(OptimizationKey other)
            : this(other.OptType, other.PeptideModSeq, other.Charge, other.Mz)
        {
        }

        public override string ToString()
        {
            return string.Format("{0} (charge {1}, m/z {2:f2})", PeptideModSeq, Charge, Mz);    // Not L10N: for debugging
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
            else if (!Equals(PeptideModSeq, other.PeptideModSeq))
            {
                return String.Compare(PeptideModSeq, other.PeptideModSeq, StringComparison.InvariantCulture);
            }
            else if (Charge != other.Charge)
            {
                return Charge.CompareTo(other.Charge);
            }
            else
            {
                return Mz.CompareTo(other.Mz);
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
            return Equals(PeptideModSeq, other.PeptideModSeq) &&
                Charge == other.Charge &&
                Mz == other.Mz;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (PeptideModSeq != null ? PeptideModSeq.GetHashCode() : 0);
                result = (result*397) ^ Charge.GetHashCode();
                result = (result*397) ^ Mz.GetHashCode();
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

        public virtual string PeptideModSeq { get { return Key.PeptideModSeq; } set { Key.PeptideModSeq = value; } }
        public virtual int Charge { get { return Key.Charge; } set { Key.Charge = value; } }
        public virtual double Mz { get { return Key.Mz; } set { Key.Mz = value; } }
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

        public virtual string Sequence { get { return PeptideModSeq; } }

        public DbOptimization(OptimizationKey key, double value)
            : this(key.OptType, key.PeptideModSeq, key.Charge, key.Mz, value)
        {
        }

        public DbOptimization(DbOptimization other)
            : this(other.Key, other.Value)
        {
            Id = other.Id;
        }

        public DbOptimization(OptimizationType type, string seq, int charge, double mz, double value)
        {
            Key = new OptimizationKey(type, seq, charge, mz);
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
