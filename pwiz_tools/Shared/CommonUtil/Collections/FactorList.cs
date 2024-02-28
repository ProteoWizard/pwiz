/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
    /// <summary>
    /// Represents a list of values using two lists:<ol>
    /// <li>A list of the unique values ("Factors") that can be found in the list</li>
    /// <li>A list of integer indexes into the list of factors</li>
    /// </ol>
    /// This is an efficient way to store values if the integer indexes can be represented
    /// in a fewer number of bytes than the items themselves.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FactorList<T> : IReadOnlyList<T>
    {
        private IReadOnlyList<T> _levels;
        private IReadOnlyList<int> _levelIndexes;
        public FactorList(IReadOnlyList<T> levels, IReadOnlyList<int> levelIndexes)
        {
            _levels = levels;
            _levelIndexes = levelIndexes;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Enumerable.Range(0, _levelIndexes.Count).Select(i => this[i]).GetEnumerator();
        }

        public int Count
        {
            get { return _levelIndexes.Count; }
        }

        public T this[int index] {
            get
            {
                int levelIndex = _levelIndexes[index];
                if (levelIndex == 0)
                {
                    return default;
                }

                return _levels[levelIndex - 1];
            }
        }

        public class Builder
        {
            private Dictionary<T, int> _levelIndexes;
            public Builder(IEnumerable<T> allValues)
            {
                Levels = ImmutableList.ValueOf(allValues.Where(v => !Equals(default(T), v)).Distinct());
                _levelIndexes = new Dictionary<T, int>(Levels.Count);
                foreach (var item in Levels)
                {
                    _levelIndexes.Add(item, _levelIndexes.Count + 1);
                }
            }
            
            public ImmutableList<T> Levels { get; }

            public FactorList<T> MakeFactorList(IEnumerable<T> values)
            {
                var levelIndexes = new List<int>();
                int maxLevelIndex = 0;
                foreach (var item in values)
                {
                    if (Equals(default(T), item))
                    {
                        levelIndexes.Add(0);
                    }
                    else
                    {
                        int levelIndex = _levelIndexes[item];
                        levelIndexes.Add(levelIndex);
                        maxLevelIndex = Math.Max(maxLevelIndex, levelIndex);
                    }
                }

                IReadOnlyList<int> levelIndexList;
                if (maxLevelIndex <= byte.MaxValue)
                {
                    levelIndexList = ByteList.FromInts(levelIndexes);
                }
                else
                {
                    levelIndexList = levelIndexes.ToArray();
                }

                return new FactorList<T>(Levels, levelIndexList);
            }
        }
    }
}
