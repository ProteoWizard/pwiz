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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    public class LibKeyMap<TItem> : AbstractReadOnlyList<TItem>
    {
        private readonly ImmutableList<TItem> _allItems;
        private readonly LibKeyIndex _index;
        public LibKeyMap(ImmutableList<TItem> items, IEnumerable<LibraryKey> keys)
        {
            _allItems = items;
            _index = new LibKeyIndex(keys);
        }

        public static LibKeyMap<TItem> Create(IEnumerable<TItem> items, Func<TItem, LibraryKey> getKeyFunc)
        {
            var itemsList = ImmutableList.ValueOf(items);
            return new LibKeyMap<TItem>(itemsList, itemsList.Select(getKeyFunc));
        }

        public static LibKeyMap<TItem> FromDictionary(IDictionary<LibKey, TItem> items)
        {
            if (items == null)
            {
                return null;
            }
            return new LibKeyMap<TItem>(ImmutableList.ValueOf(items.Values), items.Keys.Select(key=>key.LibraryKey));
        }

        public override int Count
        {
            get { return _allItems.Count; }
        }

        public bool TryGetValue(Target target, out TItem item)
        {
            var libraryKey = new LibKey(target, Adduct.EMPTY).LibraryKey;
            foreach (var matchingItem in ItemsMatching(libraryKey, LibKeyIndex.LibraryMatchType.target))
            {
                item = matchingItem;
                return true;
            }
            item = default(TItem);
            return false;
        }

        public int Length { get { return Count; } }

        public override TItem this[int index]
        {
            get { return _allItems[index]; }
        }

        public IEnumerable<LibKey> LibKeys
        {
            get
            {
                return _index.Select(item => new LibKey(item.LibraryKey));
            }
        }

        public ImmutableList<TItem> Values => _allItems;

        public LibKeyIndex Index { get { return _index; } }

        public int IndexOf(LibraryKey libraryKey)
        {
            var indexItem = _index.Find(libraryKey);
            return indexItem.HasValue ? indexItem.Value.OriginalIndex : -1;
        }

        public IEnumerable<TItem> ItemsWithUnmodifiedSequence(Target target)
        {
            var libraryKey = new LibKey(target, Adduct.EMPTY).LibraryKey;
            var matches = _index.ItemsWithUnmodifiedSequence(libraryKey);
            return matches.Select(item => _allItems[item.OriginalIndex]);
        }

        public IEnumerable<TItem> ItemsMatching(LibraryKey libraryKey, LibKeyIndex.LibraryMatchType matchType)
        {
            return _index.ItemsMatching(libraryKey, matchType).Select(GetItem);
        }

        public IEnumerable<KeyValuePair<LibKey, TItem>> KeyPairsMatching(LibraryKey libraryKey, LibKeyIndex.LibraryMatchType matchType)
        {
            return _index.ItemsMatching(libraryKey, matchType).Select(GetKeyPair);
        }

        public IDictionary<LibKey, TItem> AsDictionary()
        {
            return new LibKeyDictionary(this);
        }

        private TItem GetItem(LibKeyIndex.IndexItem indexItem)
        {
            return _allItems[indexItem.OriginalIndex];
        }

        private KeyValuePair<LibKey, TItem> GetKeyPair(LibKeyIndex.IndexItem indexItem)
        {
            var item = _allItems[indexItem.OriginalIndex];
            return new KeyValuePair<LibKey, TItem>(new LibKey(indexItem.LibraryKey), item);
        }

        public bool TryGetValue(LibKey key, out TItem value)
        {
            foreach (TItem matchingValue in ItemsMatching(key.LibraryKey,
                         PrecursorFilter.IsNullOrEmpty(key.LibraryKey.PrecursorFilter) ?
                             LibKeyIndex.LibraryMatchType.ion : // Match on moleclue and adduct
                             LibKeyIndex.LibraryMatchType.details)) // // Match on moleclue and adduct and ion mobility
            {
                value = matchingValue;
                return true;
            }
            value = default(TItem);
            return false;
        }

        public LibKeyMap<TItem> OverrideWith(LibKeyMap<TItem> overrides)
        {
            var newKeys = _index.MergeKeys(overrides._index);
            var newItems = ImmutableList.ValueOf(newKeys.Select(key =>
            {
                TItem item;
                if (!overrides.TryGetValue(key, out item))
                {
                    bool b = TryGetValue(key, out item);
                    Assume.IsTrue(b);
                }
                return item;
            }));
            return new LibKeyMap<TItem>(newItems, newKeys);
        }

        private class LibKeyDictionary : AbstractReadOnlyDictionary<LibKey, TItem>
        {
            private readonly LibKeyMap<TItem> _libKeyMap;

            public LibKeyDictionary(LibKeyMap<TItem> libKeyMap)
            {
                _libKeyMap = libKeyMap;
            }

            public override ICollection<LibKey> Keys
            {
                get { return ReadOnlyList.CreateCollection(_libKeyMap.Count, _libKeyMap._index.Select(item=>new LibKey(item.LibraryKey))); }
            }

            public override ICollection<TItem> Values
            {
                get
                {
                    return ReadOnlyList.CreateCollection(_libKeyMap.Count,
                        _libKeyMap._index.Select(item => _libKeyMap[item.OriginalIndex]));
                }
            }
            public override bool TryGetValue(LibKey key, out TItem value)
            {
                return _libKeyMap.TryGetValue(key, out value);
            }
        }
    }
}
