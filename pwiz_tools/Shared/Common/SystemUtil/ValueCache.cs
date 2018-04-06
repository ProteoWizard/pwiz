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

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Maintains a dictionary of objects such as strings that should be shared.
    /// </summary>
    public class ValueCache
    {
        /// <summary>
        /// An empty cache which never stores anything in the cache.
        /// </summary>
        public static readonly ValueCache EMPTY = new ValueCache(null);
        private readonly Dictionary<object, object> _dictionary;

        public ValueCache() : this(new Dictionary<object, object>())
        {
            
        }
        private ValueCache(Dictionary<object, object> dictionary)
        {
            _dictionary = dictionary;
        }

        /// <summary>
        /// If the value has already been stored in this cache, then sets <paramref name="value"/> to 
        /// the value from the cache and returns true.
        /// </summary>
        public bool TryGetCachedValue<T>(ref T value)
        {
            if (_dictionary == null || ReferenceEquals(value, null))
            {
                return true;
            }
            object objectFromCache;
            if (_dictionary.TryGetValue(value, out objectFromCache))
            {
                value = (T) objectFromCache;
                return true;
            }
            return false;
        }

        /// <summary>
        /// If an equivalent value has already been stored in this cache, then returns the instance from
        /// the cache. Otherwise, adds the value to this cache and returns the same instance that was passed in.
        /// </summary>
        public T CacheValue<T>(T value)
        {
            if (!TryGetCachedValue(ref value))
            {
                _dictionary.Add(value, value);
            }
            return value;
        }
    }
}
