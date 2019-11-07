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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    public class LibKeyIndex : AbstractReadOnlyCollection<LibKeyIndex.IndexItem>
    {
        private readonly ImmutableList<ISubIndex> _subIndexes;

        public LibKeyIndex(IEnumerable<IndexItem> items)
        {
            var allItems = items.ToArray();
            _subIndexes = ImmutableList.ValueOf(new ISubIndex[]
            {
                new PeptideSubIndex(allItems),
                new MoleculeSubIndex(allItems),
                new PrecursorSubIndex(allItems)
            });
            Count = _subIndexes.Sum(index => index.Count);
        }

        public LibKeyIndex(IEnumerable<LibraryKey> keys) 
            : this(keys.Select((key, index) => new IndexItem(key, index)))
        {
        }

        public override IEnumerator<IndexItem> GetEnumerator()
        {
            return _subIndexes.SelectMany(index => index).GetEnumerator();
        }

        public override int Count { get; }

        public IndexItem? Find(LibraryKey libraryKey)
        {
            return _subIndexes.SelectMany(index => index.ItemsEqualTo(libraryKey)).FirstOrDefault();
        }

        public IEnumerable<IndexItem> ItemsMatching(LibraryKey libraryKey, bool matchAdductAlso)
        {
            return _subIndexes.SelectMany(index => index.ItemsMatching(libraryKey, matchAdductAlso));
        }

        public IEnumerable<IndexItem> ItemsWithUnmodifiedSequence(LibraryKey libraryKey)
        {
            return _subIndexes.SelectMany(index => index.ItemsMatchingWithoutModifications(libraryKey));
        }

        public struct IndexItem
        {
            public static IEnumerable<IndexItem> NONE =
                ImmutableList<IndexItem>.EMPTY;

            public IndexItem(LibraryKey libraryKey, int originalIndex) : this()
            {
                LibraryKey = libraryKey;
                OriginalIndex = originalIndex;
            }

            public LibraryKey LibraryKey { get; private set; }
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
            if (Equals(key1, key2))
            {
                return true;
            }
            if (!Equals(key1.Adduct, key2.Adduct))
            {
                return false;
            }
            var peptideKey1 = key1 as PeptideLibraryKey;
            var peptideKey2 = key2 as PeptideLibraryKey;
            if (peptideKey1 == null || peptideKey2 == null)
            {
                return false;
            }
            if (!Equals(peptideKey1.UnmodifiedSequence, peptideKey2.UnmodifiedSequence))
            {
                return false;
            }
            var mods1 = peptideKey1.GetModifications();
            var mods2 = peptideKey2.GetModifications();
            if (mods1.Count != mods2.Count)
            {
                return false;
            }
            if (!mods1.Select(mod => mod.Key).SequenceEqual(mods2.Select(mod => mod.Key)))
            {
                return false;
            }

            for (int i = 0; i < mods1.Count; i++)
            {
                if (!ModificationsMatch(mods1[i].Value, mods2[i].Value))
                {
                    return false;
                }
            }
            return true;
        }



        /// <summary>
        /// Return a set of library keys that are the most general of the ones found in this and that,
        /// and which covers all of the keys.
        /// <see cref="MostGeneralPeptideKey" />
        /// </summary>
        public IList<LibraryKey> MergeKeys(LibKeyIndex that)
        {
            var keysByUnmodifiedSequence = this.Select(item => item.LibraryKey)
                .OfType<PeptideLibraryKey>()
                .ToLookup(key => key.UnmodifiedSequence)
                .ToDictionary(grouping => grouping.Key, grouping => grouping.ToArray());
            var result = new List<LibraryKey>();
            var nonPeptideKeySet = new HashSet<LibraryKey>();
            foreach (var thatItem in that)
            {
                var thatPeptideKey = thatItem.LibraryKey as PeptideLibraryKey;
                if (thatPeptideKey == null)
                {
                    if (nonPeptideKeySet.Add(thatItem.LibraryKey))
                    {
                        result.Add(thatItem.LibraryKey); // First time we've seen it, add to list
                    }
                    continue;
                }
                PeptideLibraryKey[] thisKeysWithUnmodSeq;
                if (!keysByUnmodifiedSequence.TryGetValue(thatPeptideKey.UnmodifiedSequence, out thisKeysWithUnmodSeq))
                {
                    result.Add(thatPeptideKey);
                    continue;
                }
                keysByUnmodifiedSequence[thatPeptideKey.UnmodifiedSequence] =
                    MergePeptideLibraryKey(thisKeysWithUnmodSeq, thatPeptideKey).ToArray();
            }
            result.AddRange(this.Select(item => item.LibraryKey)
                .Where(key => !(key is PeptideLibraryKey) && !nonPeptideKeySet.Contains(key)));
            result.AddRange(keysByUnmodifiedSequence.SelectMany(entry => entry.Value));
            return result;
        }

        private IEnumerable<PeptideLibraryKey> MergePeptideLibraryKey(ICollection<PeptideLibraryKey> thisKeys,
            PeptideLibraryKey thatKey)
        {
            while (true)
            {
                PeptideLibraryKey mostGeneralKey = thatKey;
                foreach (var thisKey in thisKeys)
                {
                    if (KeysMatch(thisKey, mostGeneralKey))
                    {
                        mostGeneralKey = MostGeneralPeptideKey(thisKey, mostGeneralKey);
                    }
                }
                if (Equals(mostGeneralKey, thatKey))
                {
                    break;
                }
                thatKey = mostGeneralKey;
            }
            return new[] {thatKey}.Concat(thisKeys.Where(key => !KeysMatch(thatKey, key)));
        }

        /// <summary>
        /// Given two keys that match each other (i.e. the modification masses are within the other's margin of error)
        /// return a key which has the lower precision of the two.
        /// For instance, if one key is C[+57.021464]PEPTIDER[+10] and the is C[+57.02]PEPTIDEK[10.0083],
        /// the result be C[+57.02]PEPTIDER[+10].
        /// </summary>
        private PeptideLibraryKey MostGeneralPeptideKey(PeptideLibraryKey key1, PeptideLibraryKey key2)
        {
            Assume.AreEqual(key1.UnmodifiedSequence, key2.UnmodifiedSequence);
            var mods1 = key1.GetModifications();
            var mods2 = key2.GetModifications();
            Assume.AreEqual(mods1.Count, mods2.Count);
            var newMods = new List<KeyValuePair<int, string>>(mods1.Count);
            for (int i = 0; i < mods1.Count; i++)
            {
                var mod1 = mods1[i];
                var mod2 = mods2[i];
                Assume.AreEqual(mod1.Key, mod2.Key);
                if (mod1.Value == mod2.Value)
                {
                    newMods.Add(mod1);
                    continue;
                }
                MassModification massMod1 = MassModification.Parse(mod1.Value);
                MassModification massMod2 = MassModification.Parse(mod2.Value);
                if (massMod1.Precision <= massMod2.Precision)
                {
                    newMods.Add(mod1);
                }
                else
                {
                    newMods.Add(mod2);
                }
            }
            return new PeptideLibraryKey(MakeModifiedSequence(key1.UnmodifiedSequence, newMods), key1.Charge);
        }

        private string MakeModifiedSequence(string unmodifiedSequence,
            IEnumerable<KeyValuePair<int, string>> modifications)
        {
            StringBuilder modifiedSequence = new StringBuilder();
            int ichUnmodified = 0;
            foreach (var modification in modifications)
            {
                Assume.IsTrue(modification.Key >= ichUnmodified);
                modifiedSequence.Append(unmodifiedSequence.Substring(ichUnmodified,
                    modification.Key - ichUnmodified + 1));
                ichUnmodified = modification.Key + 1;
                modifiedSequence.Append(ModifiedSequence.Bracket(modification.Value));
            }
            modifiedSequence.Append(unmodifiedSequence.Substring(ichUnmodified));
            return modifiedSequence.ToString();
        }

        private interface ISubIndex : IEnumerable<IndexItem>
        {
            int Count { get; }
            /// <summary>
            /// Returns the set of items whose LibraryKey is exactly equal to the requested key.
            /// </summary>
            IEnumerable<IndexItem> ItemsEqualTo(LibraryKey libraryKey);
            /// <summary>
            /// Returns the set of items whose LibraryKey matches, using the fuzzy logic specific to
            /// the type of library key.
            /// </summary>
            IEnumerable<IndexItem> ItemsMatching(LibraryKey libraryKey, bool matchAdductAlso);
            /// <summary>
            /// For peptides, returns the set of items whose UnmodifiedSequence match, otherwise
            /// returns the same as ItemsMatching(libraryKey, false).
            /// </summary>
            IEnumerable<IndexItem> ItemsMatchingWithoutModifications(LibraryKey libraryKey);
        }

        private abstract class SubIndex<TKey> : ISubIndex where TKey : LibraryKey
        {
            public int Count { get; protected set; }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public abstract IEnumerator<IndexItem> GetEnumerator();
            public IEnumerable<IndexItem> ItemsEqualTo(LibraryKey libraryKey)
            {
                var key = libraryKey as TKey;
                if (key == null)
                {
                    return IndexItem.NONE;
                }
                return ExactMatches(key);
            }

            protected virtual IEnumerable<IndexItem> ExactMatches(TKey key)
            {
                return ItemsMatching(key, false).Where(indexItem => Equals(key, indexItem.LibraryKey));

            }

            public IEnumerable<IndexItem> ItemsMatching(LibraryKey libraryKey, bool matchAdductAlso)
            {
                var key = libraryKey as TKey;
                if (key == null)
                {
                    return IndexItem.NONE;
                }
                var matches = ItemsMatching(key);
                return matchAdductAlso ? matches.Where(item => Equals(item.LibraryKey.Adduct, libraryKey.Adduct)) : matches;
            }

            protected abstract IEnumerable<IndexItem> ItemsMatching(TKey key);
            public IEnumerable<IndexItem> ItemsMatchingWithoutModifications(LibraryKey libraryKey)
            {
                var key = libraryKey as TKey;
                if (key == null)
                {
                    return IndexItem.NONE;
                }
                return ItemsMatchingWithoutModifications(key);
            }

            protected virtual IEnumerable<IndexItem> ItemsMatchingWithoutModifications(TKey key)
            {
                return ItemsMatching(key);
            }
        }

        /// <summary>
        /// Holds the set of peptides in the LibKeyIndex. This maintains a Dictionary from
        /// unmodified sequence to the keys.
        /// For any particular unmodified sequence, the keys are sorted by the modification indexes 
        /// (the amino acid locations that are modified). The fuzzy modification comparison only
        /// has to compare pepties that have modifications in the same locations.
        /// </summary>
        private class PeptideSubIndex : SubIndex<PeptideLibraryKey>
        {
            private readonly IDictionary<string, ImmutableList<PeptideEntry>> _entries;
            public PeptideSubIndex(IEnumerable<IndexItem> items)
            {
                _entries = new Dictionary<string, ImmutableList<PeptideEntry>>();
                var singletonIndexes = new Dictionary<int, ImmutableList<int>>();
                var indexes = new Dictionary<ImmutableList<int>, ImmutableList<int>>();
                var modIndexComparer = Comparer<IList<int>>.Create(CompareModificationIndexes);
                foreach (var group in items.Where(item=>item.LibraryKey is PeptideLibraryKey)
                    .ToLookup(item => ((PeptideLibraryKey) item.LibraryKey).UnmodifiedSequence))
                {
                    _entries.Add(group.Key, ImmutableList.ValueOf(group
                        .Select(item => PeptideEntry.NewInstance(singletonIndexes, indexes, item))
                        .OrderBy(entry => entry.ModificationIndexes, modIndexComparer)));
                }
                Count = _entries.Values.Sum(list => list.Count);
            }

            public override IEnumerator<IndexItem> GetEnumerator()
            {
                return _entries.Values.SelectMany(list => list.Select(entry => entry.IndexItem)).GetEnumerator();
            }

            protected override IEnumerable<IndexItem> ItemsMatching(PeptideLibraryKey libraryKey)
            {
                var matchingEntries = ModificationIndexMatches(libraryKey, out var modifications);
                if (modifications != null && modifications.Count != 0)
                {
                    matchingEntries = matchingEntries.Where(item =>
                        ModificationListsMatch(item.ModificationNames, modifications.Select(mod => mod.Value)));
                }
                return matchingEntries.Select(item => item.IndexItem);
            }

            private static bool ModificationListsMatch(IEnumerable<string> list1, IEnumerable<string> list2)
            {
                return !list1.Zip(list2, ModificationsMatch).Contains(false);
            }

            /// <summary>
            /// Returns the set of entries with modifications on the same amino acids.
            /// </summary>
            private IEnumerable<PeptideEntry> ModificationIndexMatches(PeptideLibraryKey peptideLibraryKey,
                [CanBeNull] out IList<KeyValuePair<int, string>> modifications)
            {
                modifications = null;
                ImmutableList<PeptideEntry> entries;
                if (!_entries.TryGetValue(peptideLibraryKey.UnmodifiedSequence, out entries))
                {
                    return ImmutableList<PeptideEntry>.EMPTY;
                }
                modifications = peptideLibraryKey.GetModifications();

                var peptideEntry = new PeptideEntry(new IndexItem(peptideLibraryKey, -1), modifications);
                var range = CollectionUtil.BinarySearch(entries, item => item.CompareModIndexes(peptideEntry));
                return Enumerable.Range(range.Start, range.Length)
                    .Select(index => entries[index]);
            }

            protected override IEnumerable<IndexItem> ExactMatches(PeptideLibraryKey libraryKey)
            {
                return ModificationIndexMatches(libraryKey, out _)
                    .Where(entry => Equals(libraryKey, entry.PeptideLibraryKey))
                    .Select(entry => entry.IndexItem);
            }

            protected override IEnumerable<IndexItem> ItemsMatchingWithoutModifications(PeptideLibraryKey libraryKey)
            {
                ImmutableList<PeptideEntry> entries;
                if (!_entries.TryGetValue(libraryKey.UnmodifiedSequence, out entries))
                {
                    return IndexItem.NONE;
                }
                return entries.Select(entry=>entry.IndexItem);
            }

            public struct PeptideEntry
            {
                public PeptideEntry(IndexItem indexItem, IList<KeyValuePair<int, string>> modifications)
                {
                    IndexItem = indexItem;
                    ModificationIndexes = ImmutableList.ValueOf(modifications.Select(mod => mod.Key));
                }

                public PeptideLibraryKey PeptideLibraryKey
                {
                    get { return (PeptideLibraryKey) IndexItem.LibraryKey; }
                }

                [NotNull]
                public ImmutableList<int> ModificationIndexes { get; private set; }

                public IEnumerable<string> ModificationNames
                {
                    get
                    {
                        return PeptideLibraryKey.GetModifications().Select(mod => mod.Value);
                    }
                }

                public IndexItem IndexItem { get; private set; }

                public int CompareModIndexes(PeptideEntry that)
                {
                    return CompareModificationIndexes(ModificationIndexes, that.ModificationIndexes);
                }

                /// <summary>
                /// Constructs a new PeptideEntry, and reuses ImmutableList values from the 
                /// passed in dictionaries to prevent redundant object creation.
                /// </summary>
                public static PeptideEntry NewInstance(IDictionary<int, ImmutableList<int>> singletonIndexCache,
                    IDictionary<ImmutableList<int>, ImmutableList<int>> indexCache, IndexItem indexItem)
                {
                    var peptideEntry = new PeptideEntry(indexItem,
                        ((PeptideLibraryKey)indexItem.LibraryKey).GetModifications());
                    if (peptideEntry.ModificationIndexes.Count == 0)
                    {
                        return peptideEntry;
                    }
                    ImmutableList<int> newIndexes;
                    if (peptideEntry.ModificationIndexes.Count == 1)
                    {
                        if (singletonIndexCache.TryGetValue(peptideEntry.ModificationIndexes[0], out newIndexes))
                        {
                            peptideEntry.ModificationIndexes = newIndexes;
                        }
                        else
                        {
                            singletonIndexCache.Add(peptideEntry.ModificationIndexes[0], peptideEntry.ModificationIndexes);
                        }
                    }
                    else
                    {
                        if (indexCache.TryGetValue(peptideEntry.ModificationIndexes, out newIndexes))
                        {
                            peptideEntry.ModificationIndexes = newIndexes;
                        }
                        else
                        {
                            indexCache.Add(peptideEntry.ModificationIndexes, peptideEntry.ModificationIndexes);
                        }
                    }
                    return peptideEntry;
                }
            }

            private static int CompareModificationIndexes(IList<int> list1, IList<int> list2)
            {
                int count1 = list1.Count;
                int count2 = list2.Count;
                int result = count1.CompareTo(count2);
                if (result != 0)
                {
                    return result;
                }
                for (int i = 0; i < count1; i++)
                {
                    result = list1[i].CompareTo(list2[i]);
                    if (result != 0)
                    {
                        return result;
                    }
                }
                return result;
            }
        }

        private class MoleculeSubIndex : SubIndex<MoleculeLibraryKey>
        {
            private readonly ILookup<string, IndexItem> _entries;

            public MoleculeSubIndex(IEnumerable<IndexItem> indexItems)
            {
                _entries = indexItems.Where(indexItem => indexItem.LibraryKey is MoleculeLibraryKey)
                    .ToLookup(item => ((MoleculeLibraryKey) item.LibraryKey).PreferredKey);
                Count = _entries.Sum(entry => entry.Count());
            }

            public override IEnumerator<IndexItem> GetEnumerator()
            {
                return _entries.SelectMany(entry => entry).GetEnumerator();
            }

            protected override IEnumerable<IndexItem> ItemsMatching(MoleculeLibraryKey libraryKey)
            {
                return _entries[libraryKey.PreferredKey];
            }
        }

        private class PrecursorSubIndex : SubIndex<PrecursorLibraryKey>
        {
            private readonly ILookup<double, IndexItem> _entries;

            public PrecursorSubIndex(IEnumerable<IndexItem> indexItems)
            {
                _entries = indexItems.Where(item => item.LibraryKey is PrecursorLibraryKey)
                    .ToLookup(item => ((PrecursorLibraryKey) item.LibraryKey).Mz);
                Count = _entries.Sum(group => group.Count());
            }

            public override IEnumerator<IndexItem> GetEnumerator()
            {
                return _entries.SelectMany(group => group).GetEnumerator();
            }

            protected override IEnumerable<IndexItem> ItemsMatching(PrecursorLibraryKey libraryKey)
            {
                return _entries[libraryKey.Mz];
            }
        }
    }
}

