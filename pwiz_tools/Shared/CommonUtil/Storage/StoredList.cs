using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.Storage
{
    public struct StoredList
    {
        private IEnumerable _list;
        public StoredList(IEnumerable list)
        {
            _list = list;
        }

        public T GetValue<T>(int index)
        {
            var list = (IReadOnlyList<T>)_list;
            if (list == null || index >= list.Count)
            {
                return default;
            }

            return list[index];
        }

        public IEnumerable<T> GetValues<T>()
        {
            if (_list is ImmutableList<T> immutableList)
            {
                return immutableList;
            }

            return _list.Cast<T>();
        }
    }
}
