/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Linq;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// Stores items in a list as integer indexes into a list of possible values.
    /// The list of possible values are <see cref="Levels"/>.
    /// The indexes into the Levels list are stored in <see cref="LevelIndices"/> 
    /// which may be stored as bits, bytes, shorts or integers depending on
    /// the highest value required.
    /// </summary>
    public class Factor<T> : ImmutableList<T>
    {
        public static Factor<T> FromItems(IEnumerable<T> items)
        {
            return FromItemsWithLevels(items, ImmutableList<T>.EMPTY);
        }

        public static Factor<T> FromItemsWithLevels(IEnumerable<T> items, ImmutableList<T> startingLevels)
        {
            if (items is Factor<T> factor && factor.Levels.Take(startingLevels.Count).SequenceEqual(startingLevels))
            {
                return factor;
            }
            var levelsDict = new Dictionary<ValueTuple<T>, int>();
            foreach (var level in startingLevels)
            {
                levelsDict.Add(ValueTuple.Create(level), levelsDict.Count);
            }
            var levelIndices = new List<int>();
            foreach (var item in items)
            {
                var key = ValueTuple.Create(item);
                if (!levelsDict.TryGetValue(key, out int levelIndex))
                {
                    levelIndex = levelsDict.Count;
                    levelsDict.Add(key, levelIndex);
                }

                levelIndices.Add(levelIndex);
            }

            ImmutableList<T> levels;
            if (levelsDict.Count == startingLevels.Count)
            {
                levels = startingLevels;
            }
            else
            {
                levels = levelsDict.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key.Item1).ToImmutable();
            }
            return new Factor<T>(levels, IntegerList.FromIntegers(levelIndices));
        }

        public Factor(ImmutableList<T> levels, ImmutableList<int> levelIndices)
        {
            Levels = levels;
            LevelIndices = levelIndices;
        }

        public ImmutableList<T> Levels { get; }
        public ImmutableList<int> LevelIndices { get; }

        public override int Count
        {
            get { return LevelIndices.Count; }
        }
        public override IEnumerator<T> GetEnumerator()
        {
            return LevelIndices.Select(i => Levels[i]).GetEnumerator();
        }

        public override T this[int index]
        {
            get
            {
                return Levels[LevelIndices[index]];
            }
        }
    }
}