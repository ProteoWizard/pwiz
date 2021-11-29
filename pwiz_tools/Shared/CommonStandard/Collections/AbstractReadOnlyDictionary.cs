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
using System.Collections;
using System.Collections.Generic;

namespace pwiz.Common.Collections
{
    public abstract class AbstractReadOnlyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        public abstract ICollection<TKey> Keys { get; }
        public abstract ICollection<TValue> Values { get; }
        public abstract bool TryGetValue(TKey key, out TValue value);

        public virtual int Count
        {
            get { return Values.Count; }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            using (IEnumerator<TKey> enKeys = Keys.GetEnumerator())
            {
                using (IEnumerator<TValue> enValues = Values.GetEnumerator())
                {
                    while (true)
                    {
                        bool bKey = enKeys.MoveNext();
                        bool bValue = enValues.MoveNext();
                        if (bKey != bValue)
                        {
                            throw new InvalidOperationException();
                        }
                        if (!bKey)
                        {
                            yield break;
                        }
                        yield return new KeyValuePair<TKey, TValue>(enKeys.Current, enValues.Current);
                    }
                }
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (TryGetValue(key, out value))
                {
                    return value;
                }
                throw new KeyNotFoundException();
            }
        }


        public virtual bool Contains(KeyValuePair<TKey, TValue> item)
        {
            TValue value;
            if (!TryGetValue(item.Key, out value))
            {
                return false;
            }
            return Equals(value, item.Value);
        }

        public bool ContainsKey(TKey key)
        {
            TValue value;
            return TryGetValue(key, out value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            foreach (var item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool IsReadOnly { get { return true; } }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        {
            throw new InvalidOperationException();
        }

        void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
        {
            throw new InvalidOperationException();
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Clear()
        {
            throw new InvalidOperationException();
        }

        TValue IDictionary<TKey, TValue>.this[TKey key]
        {
            get { return this[key]; }
            set { throw new InvalidOperationException(); }
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new InvalidOperationException();
        }

        bool IDictionary<TKey, TValue>.Remove(TKey key)
        {
            throw new InvalidOperationException();
        }
    }
}
