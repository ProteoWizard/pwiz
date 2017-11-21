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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    public class LibKeyIndex : AbstractReadOnlyList<LibKeyIndex.IndexItem>
    {
        private readonly ImmutableList<KeyValuePair<ComparisonKey, IndexItem>> _allEntries;
        private readonly IDictionary<string, Range> _indexByUnmodifiedSequence;

        public LibKeyIndex(ValueCache valueCache, IEnumerable<IndexItem> items)
        {
            _allEntries = ImmutableList.ValueOf(items
                .Select(entry => new KeyValuePair<ComparisonKey, IndexItem>(
                    ComparisonKey.Get(entry.LibraryKey).Cache(valueCache), entry))
                .OrderBy(kvp => kvp.Key, MakeComparer(ComparisonLevel.Full)));
            _indexByUnmodifiedSequence = MakeIndexByUnmodifiedSequence();
        }

        public LibKeyIndex(IEnumerable<LibraryKey> keys) : this(new ValueCache(), keys.Select((key,index)=>new IndexItem(key, index)))
        {
        }

        public LibKeyIndex(ValueCache valueCache, IEnumerable<LibraryKey> keys)
            : this(valueCache, keys.Select((key, index) => new IndexItem(key, index)))
        {
        }

        public override int Count
        {
            get { return _allEntries.Count; }
        }

        public override IndexItem this[int index]
        {
            get { return _allEntries[index].Value; }
        }

        public IndexItem? Find(LibraryKey libraryKey)
        {
            var range = SearchEntries(libraryKey, ComparisonLevel.Full);
            foreach (var index in range)
            {
                if (Equals(libraryKey, _allEntries[index].Key.LibraryKey))
                {
                    return GetItem(index);
                }
            }
            return null;
        }

        public IEnumerable<IndexItem> ItemsMatching(LibraryKey libraryKey, bool matchAdductAlso)
        {
            var comparisonKey = ComparisonKey.Get(libraryKey);
            IList<int> matchingIndexes = SearchEntries(comparisonKey,
                matchAdductAlso
                    ? ComparisonLevel.Adduct
                    : ComparisonLevel.PrecisionIndependentModifications);
            return matchingIndexes.Where(i => _allEntries[i].Key.AllModificationsMatch(comparisonKey)).Select(GetItem);
        }

        public IList<IndexItem> ItemsWithUnmodifiedSequence(LibraryKey libraryKey)
        {
            var peptideLibraryKey = libraryKey as PeptideLibraryKey;
            if (peptideLibraryKey != null)
            {
                Range range;
                if (_indexByUnmodifiedSequence.TryGetValue(peptideLibraryKey.UnmodifiedSequence, out range))
                {
                    return new IndexedSubList<IndexItem>(this, new RangeList(range));
                }
                return ImmutableList<IndexItem>.EMPTY;
            }
            var matchingIndexes = SearchEntries(libraryKey, ComparisonLevel.UnmodifiedSequence);
            return new IndexedSubList<IndexItem>(this, matchingIndexes);
        }

        protected RangeList SearchEntries(LibraryKey libraryKey, ComparisonLevel level)
        {
            return SearchEntries(ComparisonKey.Get(libraryKey), level);
        }

        private RangeList SearchEntries(ComparisonKey comparisonKey, ComparisonLevel level)
        {
            return new RangeList(CollectionUtil.BinarySearch(_allEntries, item => item.Key.Compare(comparisonKey, level)));
        }

        private static Comparer<ComparisonKey> MakeComparer(ComparisonLevel comparisonLevel)
        {
            return Comparer<ComparisonKey>.Create((key1, key2)=>key1.Compare(key2, comparisonLevel));
        }

        protected enum ComparisonLevel
        {
            KeyType,
            UnmodifiedSequence,
            PrecisionIndependentModifications,
            Adduct,
            Full = Adduct
        }

        private IndexItem GetItem(int index)
        {
            return _allEntries[index].Value;
        }

        protected class ComparisonKey
        {
            public enum KeyTypeEnum : byte
            {
                peptide,
                small_molecule,
                precursor_mz,
                unknown
            }
            public static ComparisonKey Get(LibraryKey libraryKey)
            {
                var peptideLibraryKey = libraryKey as PeptideLibraryKey;
                if (peptideLibraryKey != null)
                {
                    return new Peptide(peptideLibraryKey);
                }
                var moleculeLibraryKey = libraryKey as MoleculeLibraryKey;
                if (moleculeLibraryKey != null)
                {
                    return new SmallMolecule(moleculeLibraryKey);
                }
                var precursorKey = libraryKey as PrecursorLibraryKey;
                if (precursorKey != null)
                {
                    return new Precursor(precursorKey);
                }
                return new ComparisonKey(KeyTypeEnum.unknown, libraryKey);
            }

            protected ComparisonKey(KeyTypeEnum keyType, LibraryKey libraryKey)
            {
                LibraryKey = libraryKey;
                KeyType = keyType;
            }

            public virtual ComparisonKey Cache(ValueCache valueCache)
            {
                return valueCache.CacheValue(this);
            }

            public KeyTypeEnum KeyType { get; private set; }
            public LibraryKey LibraryKey { get; private set; }
            public Adduct Adduct { get { return LibraryKey.Adduct; } }
            public int Compare(ComparisonKey that, ComparisonLevel level)
            {
                int result = KeyType.CompareTo(that.KeyType);
                if (result != 0 || level <= ComparisonLevel.KeyType)
                {
                    return result;
                }
                result = CompareSpecific(that, level);
                if (result != 0 || level < ComparisonLevel.Adduct)
                {
                    return result;
                }
                result = Adduct.CompareTo(that.Adduct);
                if (result != 0 || level <= ComparisonLevel.Adduct)
                {
                    return result;
                }
                return result;
            }

            /// <summary>
            /// Compares the portion of the key which is specific to the type of LibraryKey.
            /// </summary>
            protected virtual int CompareSpecific(ComparisonKey other, ComparisonLevel level)
            {
                return 0;
            }

            public virtual bool AllModificationsMatch(ComparisonKey that)
            {
                return true;
            }


            public class Peptide : ComparisonKey
            {
                public Peptide(PeptideLibraryKey peptideLibraryKey) : this((LibraryKey)peptideLibraryKey)
                {
                    var modifications = peptideLibraryKey.GetModifications();
                    if (modifications.Count > 0)
                    {
                        ModificationIndexes = ImmutableList.ValueOf(modifications.Select(mod => mod.Key));
                        ModificationNames = ImmutableList.ValueOf(modifications.Select(mod => mod.Value));
                    }
                }

                private Peptide(LibraryKey libraryKey) : base(KeyTypeEnum.peptide, libraryKey)
                {
                    
                }

                public PeptideLibraryKey PeptideLibraryKey { get { return (PeptideLibraryKey) LibraryKey; } }
                public ImmutableList<int> ModificationIndexes { get; private set; }
                public ImmutableList<string> ModificationNames { get; private set; }
                protected override int CompareSpecific(ComparisonKey other, ComparisonLevel level)
                {
                    var that = (Peptide) other;
                    int result = StringComparer.Ordinal.Compare(PeptideLibraryKey.UnmodifiedSequence, that.PeptideLibraryKey.UnmodifiedSequence);
                    if (result != 0 || level <= ComparisonLevel.UnmodifiedSequence)
                    {
                        return result;
                    }
                    result = CompareModificationIndexes(that);
                    if (result != 0 || level <= ComparisonLevel.PrecisionIndependentModifications)
                    {
                        return result;
                    }
                    return result;
                }

                private int CompareModificationIndexes(Peptide that)
                {
                    if (ModificationIndexes == null)
                    {
                        return that.ModificationIndexes == null ? 0 : -1;
                    }
                    if (that.ModificationIndexes == null)
                    {
                        return 1;
                    }
                    int thisCount = ModificationIndexes.Count;
                    int thatCount = that.ModificationIndexes.Count;
                    int result = thisCount.CompareTo(thatCount);
                    if (result != 0)
                    {
                        return result;
                    }
                    for (int i = 0; i < thisCount; i++)
                    {
                        result = ModificationIndexes[i].CompareTo(that.ModificationIndexes[i]);
                        if (result != 0)
                        {
                            return result;
                        }
                    }
                    return result;
                }

                public override ComparisonKey Cache(ValueCache valueCache)
                {
                    var comparisonKey = this;
                    if (valueCache.TryGetCachedValue(ref comparisonKey))
                    {
                        return comparisonKey;
                    }
                    ImmutableList<string> modificationNames = ModificationNames;
                    if (ModificationNames != null)
                    {
                        if (!valueCache.TryGetCachedValue(ref modificationNames))
                        {
                            modificationNames = ImmutableList.ValueOf(modificationNames.Select(valueCache.CacheValue));
                            modificationNames = valueCache.CacheValue(modificationNames);
                        }
                    }
                    comparisonKey = new Peptide(LibraryKey.ValueFromCache(valueCache))
                    {
                        ModificationIndexes = valueCache.CacheValue(ModificationIndexes),
                        ModificationNames = modificationNames
                    };
                    return valueCache.CacheValue(comparisonKey);
                }

                public override bool AllModificationsMatch(ComparisonKey comparisonKey)
                {
                    var that = (Peptide) comparisonKey;
                    if (ModificationNames == null || that.ModificationNames == null)
                    {
                        return ReferenceEquals(ModificationNames, that.ModificationNames);
                    }
                    if (ModificationNames.Count != that.ModificationNames.Count)
                    {
                        return false;
                    }
                    for (int i = 0; i < ModificationNames.Count; i++)
                    {
                        if (!ModificationsMatch(ModificationNames[i], that.ModificationNames[i]))
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            public class SmallMolecule : ComparisonKey
            {
                public SmallMolecule(MoleculeLibraryKey moleculeLibraryKey) : base(KeyTypeEnum.small_molecule, moleculeLibraryKey)
                {
                }

                protected override int CompareSpecific(ComparisonKey that, ComparisonLevel level)
                {
                    var thisPreferredKey = ((MoleculeLibraryKey) LibraryKey).PreferredKey;
                    var thatPreferredKey = ((MoleculeLibraryKey) that.LibraryKey).PreferredKey;
                    return string.CompareOrdinal(thisPreferredKey, thatPreferredKey);
                }
            }

            public class Precursor : ComparisonKey
            {
                public Precursor(PrecursorLibraryKey precursorLibraryKey) : base(KeyTypeEnum.precursor_mz, precursorLibraryKey)
                {
                    
                }

                protected override int CompareSpecific(ComparisonKey that, ComparisonLevel level)
                {
                    var thisMz = ((PrecursorLibraryKey) LibraryKey).Mz;
                    var thatMz = ((PrecursorLibraryKey) that.LibraryKey).Mz;
                    return thisMz.CompareTo(thatMz);
                }
            }
        }

        public struct IndexItem 
        {
            public IndexItem(LibraryKey libraryKey, int originalIndex) : this()
            {
                LibraryKey = libraryKey;
                OriginalIndex = originalIndex;
            }

            public LibraryKey LibraryKey {get; private set; }
            public int OriginalIndex { get; private set; }
        }


        public static bool ModificationsMatch(string strMod1, string strMod2)
        {
            if (strMod1 == strMod2)
            {
                return true;
            }
            var massMod1 = MassModification.Parse(strMod1);
            var massMod2 = MassModification.Parse(strMod2);
            if (massMod1 == null || massMod2 == null)
            {
                return false;
            }
            return massMod1.Matches(massMod2);
        }

        public static bool KeysMatch(LibraryKey key1, LibraryKey key2)
        {
            var comparisonKey1 = ComparisonKey.Get(key1);
            var comparisonKey2 = ComparisonKey.Get(key2);
            return comparisonKey1.Compare(comparisonKey2, ComparisonLevel.Full) == 0 &&
                   comparisonKey1.AllModificationsMatch(comparisonKey2);
        }

        private IDictionary<string, Range> MakeIndexByUnmodifiedSequence()
        {
            String unmodifiedSequenceLast = null;
            int? iStartLast = null;
            var dictionary = new Dictionary<string, Range>();
            for (int i = 0; i < _allEntries.Count; i++)
            {
                var peptideLibraryKey = _allEntries[i].Key.LibraryKey as PeptideLibraryKey;
                if (peptideLibraryKey == null || peptideLibraryKey.UnmodifiedSequence != unmodifiedSequenceLast)
                {
                    if (unmodifiedSequenceLast != null)
                    {
                        dictionary.Add(unmodifiedSequenceLast, new Range(iStartLast.Value, i));
                        unmodifiedSequenceLast = null;
                        iStartLast = null;
                    }
                }
                if (peptideLibraryKey != null)
                {
                    iStartLast = iStartLast ?? i;
                    unmodifiedSequenceLast = unmodifiedSequenceLast ?? peptideLibraryKey.UnmodifiedSequence;
                }
            }
            if (unmodifiedSequenceLast != null)
            {
                dictionary.Add(unmodifiedSequenceLast, new Range(iStartLast.Value, _allEntries.Count));
            }
            return dictionary;
        }
    }
}

