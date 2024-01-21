using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.Storage
{
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
