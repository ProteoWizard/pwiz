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
using System.Text;

namespace pwiz.Topograph.Util
{
    public class WeakDictionary<K,V> : IDictionary<K,V>
    {
        private readonly Dictionary<K, WeakReference> _dict = new Dictionary<K, WeakReference>();
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            foreach (var entry in _dict)
            {
                var value = entry.Value.Target;
                yield return new KeyValuePair<K, V>(entry.Key, (V) value);
            }
        }

        public void Add(KeyValuePair<K, V> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            _dict.Clear();
        }

        public bool Contains(KeyValuePair<K, V> item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<K, V> item)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return _dict.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool ContainsKey(K key)
        {
            return _dict.ContainsKey(key);
        }

        public void Add(K key, V value)
        {
            WeakReference weakReference;
            if (_dict.TryGetValue(key, out weakReference))
            {
                if (weakReference.Target != null)
                {
                    throw new ArgumentException(key + " already exists");
                }
                weakReference.Target = value;
            }
            else
            {
                _dict.Add(key, new WeakReference(value));
            }
        }

        public bool Remove(K key)
        {
            return _dict.Remove(key);
        }

        public bool TryGetValue(K key, out V value)
        {
            value = default(V);
            WeakReference weakReference;
            if (!_dict.TryGetValue(key, out weakReference))
            {
                return false;
            }
            value = (V) weakReference.Target;
            return true;
        }

        public V this[K key]
        {
            get
            {
                V value;
                if (!TryGetValue(key, out value))
                {
                    throw new ArgumentException(key + " not found");
                }
                return value;
            }
            set
            {
                Remove(key);
                Add(key,value);
            }
        }

        public ICollection<K> Keys
        {
            get
            {
                return _dict.Keys;
            }
        }

        public ICollection<V> Values
        {
            get
            {
                var result = new List<V>();
                foreach (var entry in this)
                {
                    result.Add(entry.Value);
                }
                return result;
            }
        }
    }
}
