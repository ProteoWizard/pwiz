/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// Implement on an element for use with <see cref="MappedList{TKey,TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">Key type in the map</typeparam>
    public interface IKeyContainer<out TKey>
    {
        TKey GetKey();
    }

    public interface IEquivalenceTestable<in T>
    {
        bool IsEquivalent(T other);
    }

    /// <summary>
    /// Base class for use with elements to be stored in
    /// <see cref="MappedList{TKey,TValue}"/>.
    /// </summary>
    public abstract class NamedElement : IKeyContainer<string>
    {
        protected NamedElement(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        public virtual string GetKey()
        {
            return Name;
        }

        #region object overrides

        public bool Equals(NamedElement obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.Name, Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(NamedElement)) return false;
            return Equals((NamedElement)obj);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        #endregion
    }

    /// <summary>
    /// A generic ordered list based on Collection&lt;TValue>, with
    /// elements also stored in a private dictionary for fast lookup.
    /// Sort of a substitute for LinkedHashMap in Java.
    /// </summary>
    /// <typeparam name="TKey">Type of the key used in the map</typeparam>
    /// <typeparam name="TValue">Type stored in the collection</typeparam>
    public class MappedList<TKey, TValue>
        : Collection<TValue>
        where TValue : IKeyContainer<TKey>
    {
        private readonly Dictionary<TKey, TValue> _dict = new Dictionary<TKey, TValue>();

        public TValue this[TKey name]
        {
            get
            {
                return _dict[name];
            }
        }

        public IEnumerable<TKey> Keys
        {
            get
            {
                foreach (TValue value in this)
                    yield return value.GetKey();
            }
        }

        public bool ContainsKey(TKey key)
        {
            return _dict.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _dict.TryGetValue(key, out value);
        }

        public void SetValue(TValue value)
        {
            TValue valueCurrent;
            if (TryGetValue(value.GetKey(), out valueCurrent))
            {
                SetItem(IndexOf(valueCurrent), value);
            }
            else
            {
                Add(value);
            }
        }

        /// <summary>
        /// Replaces an existing value in the list by an original name, maintaining the same position.
        /// This is useful when you need to replace a specific instance rather than matching by key.
        /// For example, when an element may be renamed.
        /// </summary>
        /// <param name="oldValue">The existing value to replace (based on its key)</param>
        /// <param name="newValue">The new value to replace it with</param>
        /// <returns>True if the old value was found and replaced, false otherwise</returns>
        public bool ReplaceValue(TValue oldValue, TValue newValue)
        {
            TValue valueCurrent;
            if (TryGetValue(oldValue.GetKey(), out valueCurrent))
            {
                SetItem(IndexOf(valueCurrent), newValue);
                return true;
            }
            return false;
        }

        public void AddRange(IEnumerable<TValue> collection)
        {
            foreach (TValue item in collection)
                Add(item);
        }

        #region Collection<TValue> Overrides

        protected override void ClearItems()
        {
            _dict.Clear();
            base.ClearItems();
        }

        protected override void InsertItem(int index, TValue item)
        {
            int i = RemoveExisting(item);
            if (i != -1 && i < index)
                index--;
            // ReSharper disable once PossibleNullReferenceException
            _dict.Add(item.GetKey(), item);
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            _dict.Remove(this[index].GetKey());
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, TValue item)
        {
            TKey key = this[index].GetKey();

            // If setting to a list item that has a different key
            // from what is at this location currently, then any
            // existing value with the same key must be removed
            // from its current location.
            // ReSharper disable once PossibleNullReferenceException
            if (!Equals(key, item.GetKey()))
            {
                int i = RemoveExisting(item);
                if (i != -1 && i < index)
                    index--;

                // If the index pointed at an item with a different
                // key, then removing some other item cannot leave
                // the index out of range.
                Debug.Assert(index < Items.Count);
            }
            _dict.Remove(key);
            _dict.Add(item.GetKey(), item);
            base.SetItem(index, item);                
        }

        /// <summary>
        /// Used to help ensure that only one copy of the keyed elements
        /// can exist in the list at any time.
        /// </summary>
        /// <param name="item">An item to remove</param>
        /// <returns>The index from which it was removed, or -1 if not found</returns>
        private int RemoveExisting(TValue item)
        {
            TKey key = item.GetKey();
            if (_dict.ContainsKey(key))
            {
                _dict.Remove(key);
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Equals(Items[i].GetKey(), item.GetKey()))
                    {
                        RemoveAt(i);
                        return i;
                    }
                }
            }
            return -1;
        }

        #endregion // Collection<TValue> Overrides
    }

    public class MultiMap<TKey, TValue>
    {
        readonly Dictionary<TKey, List<TValue>> _dict;

        public MultiMap()
        {
            _dict = new Dictionary<TKey, List<TValue>>();
        }

        public MultiMap(int capacity)
        {
            _dict = new Dictionary<TKey, List<TValue>>(capacity);
        }

        public void Add(TKey key, TValue value)
        {
            List<TValue> values;
            if (_dict.TryGetValue(key, out values))
                values.Add(value);
            else
                _dict[key] = new List<TValue> { value };
        }

        public IEnumerable<TKey> Keys { get { return _dict.Keys; } }

        public IList<TValue> this[TKey key] { get { return _dict[key]; } }

        public bool TryGetValue(TKey key, out IList<TValue> values)
        {
            List<TValue> listValues;
            if (_dict.TryGetValue(key, out listValues))
            {
                values = listValues;
                return true;
            }
            values = null;
            return false;
        }
    }

    public static class MapUtil
    {
        public static MultiMap<TKey, TValue> ToMultiMap<TKey, TValue>(this IEnumerable<TValue> values, Func<TValue, TKey> keySelector)
        {
            MultiMap<TKey, TValue> map = new MultiMap<TKey, TValue>();
            foreach (TValue value in values)
                map.Add(keySelector(value), value);
            return map;
        }
    }
}
