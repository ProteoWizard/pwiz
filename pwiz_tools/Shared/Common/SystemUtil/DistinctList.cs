using System;
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
            _values.Add(value);
            index = _values.Count;
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
