/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace pwiz.Common.Collections
{
    public static class ImmutableSortedList
    {
        public static ImmutableSortedList<TKey, TValue> FromValues<TKey,TValue>(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs, IComparer<TKey> keyComparer)
        {
            return ImmutableSortedList<TKey, TValue>.FromValues(keyValuePairs, keyComparer);
        }
        public static ImmutableSortedList<TKey, TValue> FromValues<TKey,TValue>(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            return FromValues(keyValuePairs, Comparer<TKey>.Default);
        }
    }
    [Pure]
    public class ImmutableSortedList<TKey, TValue> : IList<KeyValuePair<TKey, TValue>>
    {
        public static readonly ImmutableSortedList<TKey, TValue> EMPTY = new ImmutableSortedList<TKey, TValue>(ImmutableList.Empty<TKey>(), ImmutableList.Empty<TValue>(), Comparer<TKey>.Default);
        public static ImmutableSortedList<TKey, TValue> FromValues(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs, IComparer<TKey> keyComparer)
        {
            return new ImmutableSortedList<TKey, TValue>(keyValuePairs, keyComparer);
        }

        public ImmutableSortedList(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs, IComparer<TKey> keyComparer)
        {
            var array = keyValuePairs.ToArray();
            Array.Sort(array, (kvp1, kvp2) => keyComparer.Compare(kvp1.Key, kvp2.Key));
            KeyComparer = keyComparer;
            Keys = ImmutableList.ValueOf(array.Select(kvp => kvp.Key));
            Values = ImmutableList.ValueOf(array.Select(kvp => kvp.Value));
        }

        public ImmutableSortedList(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs) : this(keyValuePairs, Comparer<TKey>.Default)
        {
        }

        private ImmutableSortedList(IList<TKey> keys, IList<TValue> values, IComparer<TKey> keyComparer)
        {
            Keys = keys;
            Values = values;
            KeyComparer = keyComparer;
        } 
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return new KeyValuePair<TKey, TValue>(Keys[i],Values[i]);
            }
        }

        public IList<TKey> Keys
        {
            get; private set;
        }

        public IList<TValue> Values
        {
            get; private set;
        }

        public Range BinarySearch(TKey key)
        {
            Range result = new Range(0, Count);
            foreach (bool firstIndex in new[]{true, false})
            {
                int lo = result.Start;
                int hi = result.End;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;

                    int c = KeyComparer.Compare(Keys[mid], key);
                    if (c == 0)
                    {
                        if (firstIndex)
                        {
                            hi = mid;
                        }
                        else
                        {
                            lo = mid + 1;
                        }
                    }
                    else if (c < 0)
                    {
                        lo = mid + 1;
                        result = new Range(lo, result.End);
                    }
                    else
                    {
                        hi = mid;
                        result = new Range(result.Start, hi);
                    }
                }
            }
            return result;
        }

        public int BinarySearch(TKey key, bool firstIndex)
        {
            int lo = 0;
            int hi = Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;

                int c = KeyComparer.Compare(Keys[mid], key);
                if (c == 0)
                {
                    if (lo == hi)
                    {
                        return lo;
                    }
                    if (firstIndex)
                    {
                        hi = mid;
                    }
                    else
                    {
                        lo = mid;
                    }
                }
                else if (c < 0)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return ~lo;
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return IndexOf(item) > 0;
        }

        public bool ContainsKey(TKey key)
        {
            return BinarySearch(key).Length > 0;
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            for (int i = 0; i < Count; i++)
            {
                array.SetValue(this[i], arrayIndex + i);
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotSupportedException();
        }

        public int Count
        {
            get { return Keys.Count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public int IndexOf(KeyValuePair<TKey, TValue> item)
        {
            Range range = BinarySearch(item.Key);
            for (int index = range.Start; index < range.End; index ++)
            {
                if (Equals(Values[index], item.Value))
                {
                    return index;
                }
            }
            return -1;
        }

        public void Insert(int index, KeyValuePair<TKey, TValue> item)
        {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        public KeyValuePair<TKey, TValue> this[int index]
        {
            get { return new KeyValuePair<TKey, TValue>(Keys[index],Values[index]); }
            set { throw new NotSupportedException(); }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            var range = BinarySearch(key);
            if (range.Length > 0)
            {
                value = Values[range.Start];
                return true;
            }
            value = default(TValue);
            return false;
        }

        public IComparer<TKey> KeyComparer { get; private set; }

        public ImmutableSortedList<TKey, TValue> RemoveKey(TKey key)
        {
            var range = BinarySearch(key);
            if (range.Length == 0)
            {
                return this;
            }
            var keys = ImmutableList.ValueOf(Keys.Take(range.Start).Concat(Keys.Skip(range.End)));
            var values = ImmutableList.ValueOf(Values.Take(range.Start).Concat(Values.Skip(range.End)));
            return new ImmutableSortedList<TKey, TValue>(keys, values, KeyComparer);
        }

        public ImmutableSortedList<TKey, TValue> Replace(TKey key, TValue value)
        {
            var range = BinarySearch(key);
            IList<TKey> keys;
            if (range.Length == 1)
            {
                keys = Keys;
            }
            else
            {
                keys = ImmutableList.ValueOf(Keys.Take(range.Start).Concat(new[] { key }).Concat(Keys.Skip(range.End)));
            }
            var values =
                ImmutableList.ValueOf(Values.Take(range.Start).Concat(new[] {value}).Concat(Values.Skip(range.End)));
            return new ImmutableSortedList<TKey, TValue>(keys, values, KeyComparer);
        }

        #region object overrides
		public bool Equals(ImmutableSortedList<TKey, TValue> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Keys, other.Keys) && Equals(other.KeyComparer, KeyComparer) && Equals(other.Values, Values);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ImmutableSortedList<TKey, TValue>)) return false;
            return Equals((ImmutableSortedList<TKey, TValue>) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Keys.GetHashCode();
                result = (result*397) ^ KeyComparer.GetHashCode();
                result = (result*397) ^ Values.GetHashCode();
                return result;
            }
        }

        public IDictionary<TKey, TValue> AsDictionary()
        {
            return new DictionaryImpl(this);
        }
        #endregion    
        private class DictionaryImpl : IDictionary<TKey, TValue>
        {
            private readonly ImmutableSortedList<TKey, TValue> _immutableSortedList;
            public DictionaryImpl(ImmutableSortedList<TKey, TValue> immutableSortedList)
            {
                _immutableSortedList = immutableSortedList;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            {
                return _immutableSortedList.GetEnumerator();
            }

            public void Add(KeyValuePair<TKey, TValue> item)
            {
                throw new InvalidOperationException();
            }

            public void Clear()
            {
                throw new InvalidOperationException();
            }

            public bool Contains(KeyValuePair<TKey, TValue> item)
            {
                return _immutableSortedList.Contains(item);
            }

            public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
            {
                _immutableSortedList.CopyTo(array, arrayIndex);
            }

            public bool Remove(KeyValuePair<TKey, TValue> item)
            {
                throw new InvalidOperationException();
            }

            public int Count
            {
                get { return _immutableSortedList.Count; }
            }
            public bool IsReadOnly
            {
                get { return true; }
            }
            public bool ContainsKey(TKey key)
            {
                return _immutableSortedList.ContainsKey(key);
            }

            public bool Remove(TKey key)
            {
                throw new InvalidOperationException();
            }

            public void Add(TKey key, TValue value)
            {
                throw new InvalidOperationException();
            }

            public bool TryGetValue(TKey key, out TValue value)
            {
                return _immutableSortedList.TryGetValue(key, out value);
            }

            public ICollection<TKey> Keys { get { return _immutableSortedList.Keys; } }

            public TValue this[TKey key]
            {
                get
                {
                    TValue result; 
                    if (!TryGetValue(key, out result))
                    {
                        throw new KeyNotFoundException();
                    }
                    return result;
                }
                set { throw new InvalidOperationException(); }
            }

            public ICollection<TValue> Values
            {
                get { return _immutableSortedList.Values; }
            }
        }
    }
}