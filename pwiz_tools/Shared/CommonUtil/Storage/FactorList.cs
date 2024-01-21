using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
    }
}
