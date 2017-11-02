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

        public override int Count
        {
            get { return _allItems.Count; }
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

        public LibKeyIndex Index { get { return _index; } }

        public int IndexOf(LibraryKey libraryKey)
        {
            var indexItem = _index.Find(libraryKey);
            return indexItem.HasValue ? indexItem.Value.OriginalIndex : -1;
        }

        public IList<TItem> ItemsWithUnmodifiedSequence(Target target)
        {
            var libraryKey = new LibKey(target, Adduct.EMPTY).LibraryKey;
            var matches = _index.ItemsWithUnmodifiedSequence(libraryKey);
            return new ItemIndexList(this, matches);
        }

        public IEnumerable<TItem> ItemsMatching(LibraryKey libraryKey, bool matchAdductAlso)
        {
            return _index.ItemsMatching(libraryKey, matchAdductAlso).Select(GetItem);
        }

        private TItem GetItem(LibKeyIndex.IndexItem indexItem)
        {
            return _allItems[indexItem.OriginalIndex];
        }

        private class ItemIndexList : AbstractReadOnlyList<TItem>
        {
            private IList<TItem> _allItems;
            private IList<LibKeyIndex.IndexItem> _indexItems;
            public ItemIndexList(IList<TItem> allItems, IList<LibKeyIndex.IndexItem> indexItems)
            {
                _allItems = allItems;
                _indexItems = indexItems;
            }

            public override int Count
            {
                get { return _indexItems.Count; }
            }

            public override TItem this[int index]
            {
                get { return _allItems[_indexItems[index].OriginalIndex]; }
            }
        }
    }
}
