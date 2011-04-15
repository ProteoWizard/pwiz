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

namespace pwiz.Common.Collections
{
    public class ImmutableDictionary<TKey,TValue> : ImmutableCollection<KeyValuePair<TKey,TValue>>, IDictionary<TKey,TValue>
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
    }
}
