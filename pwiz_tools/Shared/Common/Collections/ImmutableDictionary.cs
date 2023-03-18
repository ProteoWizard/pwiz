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
using System.Linq;

namespace pwiz.Common.Collections
{
    public class ImmutableDictionary<TKey,TValue> : ImmutableCollection<KeyValuePair<TKey,TValue>>, IDictionary<TKey,TValue>, IEquatable<ImmutableDictionary<TKey, TValue>>
    {
        public ImmutableDictionary(IDictionary<TKey,TValue> dict) : base(dict)
        {
        }
        protected ImmutableDictionary()
        {
        }

        protected IDictionary<TKey, TValue> Dictionary { get { return (IDictionary<TKey, TValue>) Collection;} set { Collection = value;} }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool ContainsKey(TKey key)
        {
            return Dictionary.ContainsKey(key);
        }

        public void Add(TKey key, TValue value)
        {
            throw new InvalidOperationException();
        }

        public bool Remove(TKey key)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return Dictionary.TryGetValue(key, out value);
        }

        public TValue this[TKey key]
        {
            get { return Dictionary[key]; }
            set { throw new InvalidOperationException(); }
        }

        public ICollection<TKey> Keys
        {
            get { return new ImmutableCollection<TKey>(Dictionary.Keys); }
        }

        public ICollection<TValue> Values
        {
            get { return new ImmutableCollection<TValue>(Dictionary.Values); }
        }

        public ImmutableDictionary<TKey, TValue> RemoveKey(TKey key)
        {
            if (!ContainsKey(key))
            {
                return this;
            }
            return new ImmutableDictionary<TKey, TValue>(new Dictionary<TKey, TValue>(Dictionary
                .Where(kvp => !Equals(key, kvp.Key)).ToDictionary(kv => kv.Key, kv => kv.Value)));
        }

        public ImmutableDictionary<TKey, TValue> Replace(TKey key, TValue value)
        {
            if (TryGetValue(key, out var oldValue) && Equals(oldValue, value))
            {
                return this;
            }
            var newDict = new Dictionary<TKey, TValue>(Dictionary);
            newDict[key] = value;
            return new ImmutableDictionary<TKey, TValue>(new Dictionary<TKey, TValue>(newDict));
        }

        public bool Equals(ImmutableDictionary<TKey, TValue> other)
        {
            if (other == null || Count != other.Count)
            {
                return false;
            }
            foreach (var kvp in this)
            {
                if (!other.TryGetValue(kvp.Key, out var otherValue) || !Equals(kvp.Value, otherValue))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ImmutableDictionary<TKey, TValue>)obj);
        }

        public override int GetHashCode()
        {
            return this.Aggregate(0, (current, kvp) => current ^ kvp.GetHashCode());
        }
    }
}
