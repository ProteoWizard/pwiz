/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
 */using System;
using System.Collections;
using System.Collections.Generic;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// A collection whose <see cref="Add"/> method prevents duplicates from being added.
    /// </summary>
    public class DistinctList<T> : IEnumerable<T>
    {
        private List<T> _values = new List<T>();
        /// <summary>
        /// Map from value to index in _values. Using "ValueTuple" as the key type allows nulls.
        /// </summary>
        private Dictionary<ValueTuple<T>, int> _dictionary = new Dictionary<ValueTuple<T>, int>();
        /// <summary>
        /// If the value is not already in this collection, then add it.
        /// Returns the index of the value in this collection.
        /// </summary>
        public int Add(T value)
        {
            var wrappedValue = ValueTuple.Create(value);
            if (_dictionary.TryGetValue(wrappedValue, out int index))
            {
                return index;
            }
            index = _values.Count;
            _values.Add(value);
            _dictionary.Add(wrappedValue, index);
            return index;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        public int Count
        {
            get { return _values.Count; }
        }

        public T this[int index]
        {
            get
            {
                return _values[index];
            }
        }
    }
}
