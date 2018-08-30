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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class TargetMap<TValue> : IDictionary<Target, TValue>
    {
        private ImmutableList<Target> _targets;
        private LibKeyMap<TValue> _libKeyMap;

        public TargetMap(IEnumerable<KeyValuePair<Target, TValue>> entries)
        {
            IEnumerable<Target> targets;
            IEnumerable<TValue> values;
            var dictEntries = entries as IDictionary<Target, TValue>;
            if (dictEntries != null)
            {
                targets = dictEntries.Keys;
                values = dictEntries.Values;
            }
            else
            {
                var targetList = new List<Target>();
                var valueList = new List<TValue>();
                foreach (var entry in entries)
                {
                    targetList.Add(entry.Key);
                    valueList.Add(entry.Value);
                }
                targets = targetList;
                values = valueList;
            }
            _targets = ImmutableList.ValueOf(targets);
            _libKeyMap = new LibKeyMap<TValue>(ImmutableList.ValueOf(values), _targets.Select(MakeKey));
        }

        public TValue this[Target key]
        {
            get { return ItemsMatching(key).First(); }
        }

        public bool Contains(KeyValuePair<Target, TValue> item)
        {
            return _libKeyMap.ItemsMatching(MakeKey(item.Key), false).Contains(item.Value);
        }

        public bool ContainsKey(Target key)
        {
            return ItemsMatching(key).Any();
        }

        public void CopyTo(KeyValuePair<Target, TValue>[] array, int arrayIndex)
        {
            foreach (var kvp in this)
            {
                array[arrayIndex++] = kvp;
            }
        }

        public int Count { get { return _libKeyMap.Count; } }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<Target, TValue>> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(i => new KeyValuePair<Target, TValue>(_targets[i], _libKeyMap[i]))
                .GetEnumerator();
        }

        public bool IsReadOnly { get { return true; }}

        public ICollection<Target> Keys
        {
            get { return _targets; }
        }

        public bool TryGetValue(Target key, out TValue value)
        {
            foreach (var item in ItemsMatching(key))
            {
                value = item;
                return true;
            }
            value = default(TValue);
            return false;
        }

        public ICollection<TValue> Values { get { return _libKeyMap; }}

        private IEnumerable<TValue> ItemsMatching(Target target)
        {
            return _libKeyMap.ItemsMatching(MakeKey(target), false);
        }
        private static LibraryKey MakeKey(Target target)
        {
            return new LibKey(target, Adduct.EMPTY).LibraryKey;
        }

        void ICollection<KeyValuePair<Target, TValue>>.Add(KeyValuePair<Target, TValue> item)
        {
            throw new InvalidOperationException();
        }

        void IDictionary<Target, TValue>.Add(Target key, TValue value)
        {
            throw new InvalidOperationException();
        }

        void ICollection<KeyValuePair<Target, TValue>>.Clear()
        {
            throw new InvalidOperationException();
        }

        TValue IDictionary<Target, TValue>.this[Target key]
        {
            get { return this[key]; }
            set { throw new InvalidOperationException(); }
        }

        bool ICollection<KeyValuePair<Target, TValue>>.Remove(KeyValuePair<Target, TValue> item)
        {
            throw new InvalidOperationException();
        }

        bool IDictionary<Target, TValue>.Remove(Target key)
        {
            throw new InvalidOperationException();
        }
    }
}
