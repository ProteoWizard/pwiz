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
using System.Linq;
using System.Text;
using Google.Protobuf;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Crosslinking;
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
                case LibraryKeyProto.Types.KeyType.Crosslink:
                    return new CrosslinkLibraryKey(proto);
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

        /// <summary>
        /// Creates a Peptide object corresponding to this LibraryKey. Will throw
        /// an exception if this LibraryKey is malformed.
        /// </summary>
        /// <returns></returns>
        public abstract Peptide CreatePeptideIdentityObj();
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

        public override Peptide CreatePeptideIdentityObj()
        {
            return new Peptide(UnmodifiedSequence);
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

        public override Peptide CreatePeptideIdentityObj()
        {
            return new Peptide(Target);
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

        public override Peptide CreatePeptideIdentityObj()
        {
            return null;
        }
    }

    public class CrosslinkLibraryKey : LibraryKey
    {
        public CrosslinkLibraryKey(IEnumerable<PeptideLibraryKey> peptideLibraryKeys, IEnumerable<Crosslink> crosslinks, int charge)
        {
            PeptideLibraryKeys = ImmutableList.ValueOf(peptideLibraryKeys);
            Crosslinks = ImmutableList.ValueOf(crosslinks);
            Charge = charge;
        }

        public CrosslinkLibraryKey(LibraryKeyProto libraryKeyProto)
        {
            PeptideLibraryKeys = ImmutableList.ValueOf(libraryKeyProto.CrosslinkedSequences
                .Select(sequence => new PeptideLibraryKey(sequence, 0)));
            Crosslinks = ImmutableList.ValueOf(libraryKeyProto.Crosslinkers.Select(crosslinkProto =>
                new Crosslink(crosslinkProto.Name, crosslinkProto.Positions.Select(pos => pos.Position.ToArray()))));
            Charge = libraryKeyProto.Charge;
        }

        public override Target Target
        {
            get { return PeptideLibraryKeys.First().Target; }
        }

        public int Charge { get; private set; }

        public override Adduct Adduct
        {
            get { return Adduct.FromChargeProtonated(Charge); }
        }
        protected internal override LibraryKeyProto ToLibraryKeyProto()
        {
            var libraryKeyProto = new LibraryKeyProto()
            {
                KeyType = LibraryKeyProto.Types.KeyType.Crosslink,
                Charge = Charge
            };
            foreach (var peptideLibraryKey in PeptideLibraryKeys)
            {
                libraryKeyProto.CrosslinkedSequences.Add(peptideLibraryKey.ModifiedSequence);
            }
            foreach (var crosslinker in Crosslinks)
            {
                var crosslinkProto = new LibraryKeyProto.Types.Crosslinker()
                {
                    Name = crosslinker.Name
                };
                foreach (var positions in crosslinker.Positions)
                {
                    var positionsProto = new LibraryKeyProto.Types.Positions();
                    positionsProto.Position.AddRange(positions);
                    crosslinkProto.Positions.Add(positionsProto);
                }
                libraryKeyProto.Crosslinkers.Add(crosslinkProto);
            }
            return libraryKeyProto;
        }

        public override LibraryKey StripModifications()
        {
            return PeptideLibraryKeys.First().StripModifications();
        }

        public override Peptide CreatePeptideIdentityObj()
        {
            return PeptideLibraryKeys.First().CreatePeptideIdentityObj();
        }

        public ImmutableList<PeptideLibraryKey> PeptideLibraryKeys { get; private set; }

        public ImmutableList<Crosslink> Crosslinks { get; private set; }

        public class Crosslink : Immutable
        {
            public Crosslink(string name, IEnumerable<IEnumerable<int>> positions)
            {
                Name = name;
                Positions = MakePositions(positions);
            }

            public string Name { get; private set; }

            public ImmutableList<ImmutableList<int>> Positions { get; private set; }

            public IList<IEnumerable<int>> AaIndexes
            {
                get { return ReadOnlyList.Create(Positions.Count, i => Positions[i].Select(x=>x - 1)); }
            }

            protected bool Equals(Crosslink other)
            {
                return Name == other.Name && Positions.Equals(other.Positions);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Crosslink) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Name.GetHashCode() * 397) ^ Positions.GetHashCode();
                }
            }

            public override string ToString()
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(@"[");
                stringBuilder.Append(Name);
                stringBuilder.Append(@"@");
                string strComma = string.Empty;
                foreach (var list in Positions)
                {
                    stringBuilder.Append(strComma);
                    strComma = @",";
                    if (list.Count == 0)
                    {
                        stringBuilder.Append(@"*");
                    }
                    else if (list.Count == 1)
                    {
                        stringBuilder.Append(list[0].ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        stringBuilder.Append(string.Join(@"-", list));
                    }
                }

                stringBuilder.Append(@"]");
                return stringBuilder.ToString();
            }

            public IEnumerable<int> PeptideIndexesWithLinks
            {
                get
                {
                    for (int i = 0; i < Positions.Count; i++)
                    {
                        if (Positions[i].Any())
                        {
                            yield return i;
                        }
                    }
                }
            }

            public IEnumerable<CrosslinkSite> CrosslinkSites
            {
                get
                {
                    return Enumerable.Range(0, Positions.Count).SelectMany(peptideIndex =>
                        Positions[peptideIndex].Select(aaPosition => new CrosslinkSite(peptideIndex, aaPosition - 1)));
                }
            }
        }

        public static ImmutableList<ImmutableList<int>> MakePositions(IEnumerable<IEnumerable<int>> positions)
        {
            if (positions is ImmutableList<ImmutableList<int>> immutableList && !immutableList.Contains(null))
            {
                return immutableList;
            }

            return ImmutableList.ValueOf(positions.Select(entry => ImmutableList.ValueOfOrEmpty(entry)));
        }

        public override string ToString()
        {
            return string.Join(@"-", PeptideLibraryKeys) + @"-" + string.Concat(Crosslinks) + Transition.GetChargeIndicator(Adduct);
        }

        /// <summary>
        /// Returns true if Skyline can handle the structure of the crosslinks in this CrosslinkLibraryKey.
        /// </summary>
        public bool IsSupportedBySkyline()
        {
            var queue = new List<int>();
            var remainingCrosslinks = new List<ImmutableList<int>>();
            var consumedPeptides = new HashSet<int>();
            foreach (var crosslink in Crosslinks)
            {
                if (!HasValidIndexes(crosslink))
                {
                    return false;
                }
                var peptideIndexesWithLinks = ImmutableList.ValueOf(crosslink.PeptideIndexesWithLinks);
                if (peptideIndexesWithLinks.Count == 1)
                {
                    if (crosslink.AaIndexes[peptideIndexesWithLinks[0]].Count() != 2)
                    {
                        return false;
                    }
                }
                else if (peptideIndexesWithLinks.Count == 2)
                {
                    if (peptideIndexesWithLinks.Any(index => crosslink.AaIndexes[index].Count() != 1))
                    {
                        return false;
                    }
                    if (peptideIndexesWithLinks.Contains(0))
                    {
                        Assume.AreEqual(peptideIndexesWithLinks[0], 0);
                        queue.Add(peptideIndexesWithLinks[1]);
                    }
                    else
                    {
                        remainingCrosslinks.Add(peptideIndexesWithLinks);
                    }
                }
                else
                {
                    return false;
                }
            }

            consumedPeptides.Add(0);

            while (queue.Count != 0)
            {
                int currentPeptideIndex = queue[0];
                queue.RemoveAt(0);
                consumedPeptides.Add(currentPeptideIndex);
                for (int iCrosslink = remainingCrosslinks.Count - 1; iCrosslink >= 0; iCrosslink--)
                {
                    var crosslinkIndexes = remainingCrosslinks[iCrosslink];
                    if (!crosslinkIndexes.Contains(currentPeptideIndex))
                    {
                        continue;
                    }

                    int otherPeptideIndex = crosslinkIndexes.Except(new[] {currentPeptideIndex}).First();
                    queue.Add(otherPeptideIndex);
                    remainingCrosslinks.RemoveAt(iCrosslink);
                }
            }

            if (consumedPeptides.Count != PeptideLibraryKeys.Count)
            {
                return false;
            }
            return true;
        }

        private bool HasValidIndexes(Crosslink crosslink)
        {
            if (crosslink.Positions.Count > PeptideLibraryKeys.Count)
            {
                return false;
            }

            for (int iPeptide = 0; iPeptide < crosslink.Positions.Count; iPeptide++)
            {
                var peptideSequence = PeptideLibraryKeys[iPeptide].UnmodifiedSequence;
                foreach (var position in crosslink.Positions[iPeptide])
                {
                    if (position < 1 || position > peptideSequence.Length)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
