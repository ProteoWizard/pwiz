/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// List which maintains a dictionary to implement fast <see cref="IList{T}.IndexOf" />.
    /// The list is not allowed to contain duplicates or null.
    /// </summary>
    public class IndexedList<T> : IList<T>
    {
        private Dictionary<T, int> _itemIndexes = new Dictionary<T, int>();
        private List<T> _items = new List<T>();
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        public void Add(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException();
            }
            var newIndex = _items.Count;
            _itemIndexes.Add(item, newIndex);
            _items.Add(item);
        }

        public void Clear()
        {
            _items.Clear();
            _itemIndexes.Clear();
        }

        public bool Contains(T item)
        {
            return IndexOf(item) >= 0;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index < 0)
            {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        public int Count
        {
            get { return _items.Count; }
        }
        public bool IsReadOnly
        {
            get { return false; }
        }
        public int IndexOf(T item)
        {
            if (item == null)
            {
                return -1;
            }
            if (_itemIndexes.TryGetValue(item, out int index))
            {
                return index;
            }

            return -1;
        }

        public void Insert(int index, T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
            if (index == Count)
            {
                Add(item);
            }
            else
            {
                _items.Insert(index, item);
                RebuildIndex();
            }
        }

        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
            RebuildIndex();
        }

        public T this[int index]
        {
            get
            {
                return _items[index];
            }
            set
            {
                if (null == value)
                {
                    throw new ArgumentNullException();
                }
                var oldValue = _items[index];
                _itemIndexes.Remove(oldValue);
                _itemIndexes.Add(value, index);
                _items[index] = value;
            }
        }

        private void RebuildIndex()
        {
            _itemIndexes.Clear();
            foreach (var item in _items)
            {
                _itemIndexes.Add(item, _itemIndexes.Count);
            }
        }
    }
}
