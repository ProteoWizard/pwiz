/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.IO;
using System.Text;
using Google.Protobuf;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    public abstract class LibraryKey
    {
        public abstract Target Target { get; }
        public abstract Adduct Adduct { get; }

        protected internal abstract LibraryKeyProto ToLibraryKeyProto();

        public void Write(Stream stream)
        {
            var memoryStream = new MemoryStream();
            ToLibraryKeyProto().WriteTo(memoryStream);
            PrimitiveArrays.WriteOneValue(stream, (int) memoryStream.Length);
            memoryStream.Seek(0, SeekOrigin.Begin);
            memoryStream.CopyTo(stream);
        }

        public static LibraryKey Read(ValueCache valueCache, Stream stream)
        {
            int length = PrimitiveArrays.ReadOneValue<int>(stream);
            byte[] buffer = new byte[length];
            stream.Read(buffer, 0, length);
            var proto = new LibraryKeyProto();
            proto.MergeFrom(new CodedInputStream(buffer));
            switch (proto.KeyType)
            {
                case LibraryKeyProto.Types.KeyType.Peptide:
                    return new PeptideLibraryKey(proto.ModifiedSequence, proto.Charge).ValueFromCache(valueCache);
                case LibraryKeyProto.Types.KeyType.PrecursorMz:
                    return new PrecursorLibraryKey(proto);
                case LibraryKeyProto.Types.KeyType.SmallMolecule:
                    return new MoleculeLibraryKey(valueCache,proto);
            }
            return null;
        }

        public virtual bool IsEquivalentTo(LibraryKey that)
        {
            return Equals(that);
        }

        public virtual int GetEquivalencyHashCode()
        {
            return GetHashCode();
        }

        public virtual LibraryKey StripModifications()
        {
            return this;
        }

        public virtual LibraryKey ValueFromCache(ValueCache valueCache)
        {
            return valueCache.CacheValue(this);
        }

        public static implicit operator LibKey(LibraryKey libraryKey)
        {
            return new LibKey(libraryKey);
        }
    }

    public class PeptideLibraryKey : LibraryKey
    {
        public PeptideLibraryKey(string modifiedSequence, int charge)
        {
            ModifiedSequence = modifiedSequence;
            UnmodifiedSequence = GetUnmodifiedSequence(modifiedSequence, null);
            Charge = charge;
        }

        private PeptideLibraryKey()
        {
        }

        public string ModifiedSequence { get; private set; }
        public string UnmodifiedSequence { get; private set; }
        public int Charge { get; private set; }
        public bool HasModifications { get { return ModifiedSequence != UnmodifiedSequence; } }

        public override LibraryKey ValueFromCache(ValueCache valueCache)
        {
            var libraryKey = this;
            if (valueCache.TryGetCachedValue(ref libraryKey))
            {
                return libraryKey;
            }
            libraryKey = new PeptideLibraryKey
            {
                ModifiedSequence = valueCache.CacheValue(ModifiedSequence),
                UnmodifiedSequence = valueCache.CacheValue(UnmodifiedSequence),
                Charge = Charge,
            };
            return valueCache.CacheValue(libraryKey);
        }

        public int ModificationCount
        {
            get
            {
                var modifications = new List<KeyValuePair<int, string>>();
                GetUnmodifiedSequence(ModifiedSequence, modifications);
                return modifications.Count;
            }
        }

        public override LibraryKey StripModifications()
        {
            if (ModifiedSequence == UnmodifiedSequence)
            {
                return this;
            }
            return new PeptideLibraryKey
            {
                ModifiedSequence = UnmodifiedSequence,
                UnmodifiedSequence = UnmodifiedSequence,
                Charge = Charge
            };
        }

        public override Adduct Adduct
        {
            get { return Adduct.FromChargeProtonated(Charge); }
        }

        public IList<KeyValuePair<int, string>> GetModifications()
        {
            if (ReferenceEquals(ModifiedSequence, UnmodifiedSequence))
            {
                return ImmutableList<KeyValuePair<int, string>>.EMPTY;
            }
            var modifications = new List<KeyValuePair<int, string>>();
            GetUnmodifiedSequence(ModifiedSequence, modifications);
            return modifications;
        }

        public static string GetUnmodifiedSequence(string modifiedSequence, IList<KeyValuePair<int, string>> modifications)
        {
            StringBuilder unmodifiedSequence = null;
            int? modificationStart = null;
            for (int i = 0; i < modifiedSequence.Length; i++)
            {
                char ch = modifiedSequence[i];
                if (modificationStart.HasValue)
                {
                    if (ch == ']')
                    {
                        if (modifications != null)
                        {
                            string strModification =
                                modifiedSequence.Substring(modificationStart.Value, i - modificationStart.Value);
                            modifications.Add(new KeyValuePair<int, string>(
                                unmodifiedSequence.Length - 1, strModification));
                        }
                        modificationStart = null;
                    }
                }
                else
                {
                    if (ch == '[')
                    {
                        if (unmodifiedSequence == null)
                        {
                            unmodifiedSequence = new StringBuilder(modifiedSequence.Substring(0, i), modifiedSequence.Length);
                        }
                        modificationStart = i + 1;
                    }
                    else
                    {
                        if (unmodifiedSequence != null)
                        {
                            unmodifiedSequence.Append(ch);
                        }
                    }
                }
            }
            if (unmodifiedSequence != null)
            {
                return unmodifiedSequence.ToString();
            }
            return modifiedSequence;
        }

        public override Target Target
        {
            get { return new Target(ModifiedSequence); }
        }

        protected internal override LibraryKeyProto ToLibraryKeyProto()
        {
            return new LibraryKeyProto
            {
                ModifiedSequence = ModifiedSequence,
                Charge = Charge
            };
        }

        protected bool Equals(PeptideLibraryKey other)
        {
            return string.Equals(ModifiedSequence, other.ModifiedSequence) && 
                   Charge == other.Charge;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PeptideLibraryKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ModifiedSequence.GetHashCode();
                hashCode = (hashCode * 397) ^ Charge;
                return hashCode;
            }
        }

        public override string ToString()
        {
            return ModifiedSequence + Transition.GetChargeIndicator(Adduct);
        }

        /// <summary>
        /// Change the format of all modifications so that they are in the Skyline 3.7
        /// format of having one digit after the decimal.
        /// </summary>
        public PeptideLibraryKey FormatToOneDecimal()
        {
            if (!HasModifications)
            {
                return this;
            }
            StringBuilder newSequence = new StringBuilder();
            int aaCount = 0;
            foreach (var mod in GetModifications())
            {
                newSequence.Append(UnmodifiedSequence.Substring(aaCount, mod.Key + 1 - aaCount));
                aaCount = mod.Key + 1;
                var massModification = MassModification.Parse(mod.Value);
                string newMod;
                if (massModification == null)
                {
                    newMod = mod.Value;
                }
                else
                {
                    newMod = new MassModification(massModification.Mass, 1).ToString();
                }
                newSequence.Append(Model.ModifiedSequence.Bracket(newMod));
            }
            newSequence.Append(UnmodifiedSequence.Substring(aaCount));
            return new PeptideLibraryKey(newSequence.ToString(), Charge);
        }
    }

    public class MoleculeLibraryKey : LibraryKey
    {
        private Adduct _adduct;

        internal MoleculeLibraryKey(ValueCache valueCache, LibraryKeyProto libraryKeyProto) : this (
            valueCache,
            SmallMoleculeLibraryAttributes.Create(
            valueCache.CacheValue(libraryKeyProto.MoleculeName),
            valueCache.CacheValue(libraryKeyProto.ChemicalFormula), 
            libraryKeyProto.InChiKey, libraryKeyProto.OtherKeys), 
            Adduct.FromString(libraryKeyProto.Adduct,Adduct.ADDUCT_TYPE.non_proteomic, null))
        {
        }

        public MoleculeLibraryKey(ValueCache valueCache, SmallMoleculeLibraryAttributes smallMoleculeLibraryAttributes,
            Adduct adduct)
        {
            SmallMoleculeLibraryAttributes = Normalize(valueCache, smallMoleculeLibraryAttributes);
            PreferredKey = SmallMoleculeLibraryAttributes.GetPreferredKey() ??
                           SmallMoleculeLibraryAttributes.MoleculeName ?? String.Empty;
            _adduct = adduct;
        }

        public MoleculeLibraryKey(SmallMoleculeLibraryAttributes smallMoleculeLibraryAttributes, Adduct adduct) : this(null, smallMoleculeLibraryAttributes, adduct)
        {
        }

        public SmallMoleculeLibraryAttributes SmallMoleculeLibraryAttributes { get; private set; }

        public string PreferredKey { get; private set; }

        public override Adduct Adduct
        {
            get { return _adduct; }
        }

        public override Target Target
        {
            get { return new Target(CustomMolecule.FromSmallMoleculeLibraryAttributes(SmallMoleculeLibraryAttributes)); }
        }

        protected internal override LibraryKeyProto ToLibraryKeyProto()
        {
            return new LibraryKeyProto()
            {
                KeyType = LibraryKeyProto.Types.KeyType.SmallMolecule,
                Adduct = Adduct.AdductFormula ?? string.Empty,
                MoleculeName = SmallMoleculeLibraryAttributes.MoleculeName ?? string.Empty,
                ChemicalFormula = SmallMoleculeLibraryAttributes.ChemicalFormulaOrMassesString ?? string.Empty,
                InChiKey = SmallMoleculeLibraryAttributes.InChiKey ?? string.Empty,
                OtherKeys = SmallMoleculeLibraryAttributes.OtherKeys ?? string.Empty
            };
        }

        protected bool Equals(MoleculeLibraryKey other)
        {
            return Equals(_adduct, other._adduct) && Equals(SmallMoleculeLibraryAttributes, other.SmallMoleculeLibraryAttributes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MoleculeLibraryKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_adduct != null ? _adduct.GetHashCode() : 0) * 397) ^ (SmallMoleculeLibraryAttributes != null ? SmallMoleculeLibraryAttributes.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return SmallMoleculeLibraryAttributes.ToString() + Adduct;
        }

        private static SmallMoleculeLibraryAttributes Normalize(
            ValueCache valueCache,
            SmallMoleculeLibraryAttributes smallMoleculeLibraryAttributes)
        {
            if (smallMoleculeLibraryAttributes == null)
            {
                return SmallMoleculeLibraryAttributes.EMPTY;
            }

            if (null == smallMoleculeLibraryAttributes.MoleculeName ||
                null == smallMoleculeLibraryAttributes.ChemicalFormulaOrMassesString ||
                null == smallMoleculeLibraryAttributes.InChiKey || 
                null == smallMoleculeLibraryAttributes.OtherKeys)
            {
                smallMoleculeLibraryAttributes = SmallMoleculeLibraryAttributes.Create(
                    smallMoleculeLibraryAttributes.MoleculeName ?? string.Empty,
                    smallMoleculeLibraryAttributes.ChemicalFormulaOrMassesString ?? string.Empty,
                    smallMoleculeLibraryAttributes.InChiKey ?? string.Empty,
                    smallMoleculeLibraryAttributes.OtherKeys ?? string.Empty);
            }
            if (valueCache != null)
            {
                if (!valueCache.TryGetCachedValue(ref smallMoleculeLibraryAttributes))
                {
                    smallMoleculeLibraryAttributes = valueCache.CacheValue(SmallMoleculeLibraryAttributes.Create(
                        valueCache.CacheValue(smallMoleculeLibraryAttributes.MoleculeName),
                        valueCache.CacheValue(smallMoleculeLibraryAttributes.ChemicalFormulaOrMassesString),
                        valueCache.CacheValue(smallMoleculeLibraryAttributes.InChiKey),
                        valueCache.CacheValue(smallMoleculeLibraryAttributes.OtherKeys)
                    ));
                }
            }
            return smallMoleculeLibraryAttributes;
        }

        public override int GetEquivalencyHashCode()
        {
            return (397 * PreferredKey.GetHashCode()) ^ Adduct.GetHashCode();
        }

        public override bool IsEquivalentTo(LibraryKey other)
        {
            var that = other as MoleculeLibraryKey;
            if (that == null)
            {
                return false;
            }
            return Equals(PreferredKey, that.PreferredKey) && Equals(Adduct, that.Adduct);
        }
    }

    public class PrecursorLibraryKey : LibraryKey
    {
        public PrecursorLibraryKey(LibraryKeyProto libraryKeyProto)
        {
            Mz = libraryKeyProto.PrecursorMz;
            if (0 != libraryKeyProto.RetentionTime)
            {
                RetentionTime = libraryKeyProto.RetentionTime;
            }
        }
        public PrecursorLibraryKey(double mz, double? retentionTime)
        {
            Mz = mz;
            RetentionTime = retentionTime;
        }

        public double Mz { get; private set; }
        public double? RetentionTime { get; private set; }

        public override Adduct Adduct
        {
            get { return Adduct.EMPTY; }
        }

        public override Target Target
        {
            get { return null; }
        }

        protected internal override LibraryKeyProto ToLibraryKeyProto()
        {
            return new LibraryKeyProto()
            {
                KeyType = LibraryKeyProto.Types.KeyType.PrecursorMz,
                PrecursorMz = Mz,
                RetentionTime = RetentionTime.GetValueOrDefault(),
            };
        }

        protected bool Equals(PrecursorLibraryKey other)
        {
            return Mz.Equals(other.Mz) && RetentionTime.Equals(other.RetentionTime);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PrecursorLibraryKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Mz.GetHashCode() * 397) ^ RetentionTime.GetHashCode();
            }
        }

        public override string ToString()
        {
            var precursor = Mz.ToString(@"0.000", CultureInfo.CurrentCulture);
            if (!RetentionTime.HasValue)
                return precursor;
            var rt = RetentionTime.GetValueOrDefault().ToString(@"0.00", CultureInfo.CurrentCulture);
            return string.Format(@"{0} ({1})", precursor, rt);
        }
    }
}
