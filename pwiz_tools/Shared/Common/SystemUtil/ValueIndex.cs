using System;
using System.Collections.Generic;

namespace pwiz.Common.SystemUtil
{
    public class ValueIndex<T>
    {
        private List<T> _values = new List<T>();
        private Dictionary<ValueTuple<T>, int> _dictionary;

        public ValueIndex()
        {
        }

        public ValueIndex(IEnumerable<T> values) : this()
        {
            _values.AddRange(values);
        }

        public static ValueIndex<T> WithDefaultAtZero()
        {
            return new ValueIndex<T>(new[]{default(T)});
        }

        public int IndexForValue(T value)
        {
            var dictionary = EnsureDictionary();
            var wrappedValue = new ValueTuple<T>(value);
            if (dictionary.TryGetValue(wrappedValue, out int index))
            {
                return index;
            }
            _values.Add(value);
            index = _values.Count;
            dictionary.Add(wrappedValue, index);
            return index;
        }

        public T ValueForIndex(int index)
        {
            if (index == 0)
            {
                return default;
            }
            return _values[index - 1];
        }

        public IEnumerable<T> Values
        {
            get { return _values; }
        }

        private Dictionary<ValueTuple<T>, int> EnsureDictionary()
        {
            if (_dictionary == null)
            {
                var dictionary = new Dictionary<ValueTuple<T>, int>();
                // Go through the values in reverse order so that if there are duplicates (where there should not be)
                // the duplicate with the lowest index will replace all the others.
                for (int i = _values.Count - 1; i >= 0; i--)
                {
                    dictionary[new ValueTuple<T>(_values[i])] = i;
                }

                _dictionary = dictionary;
            }

            return _dictionary;
        }
    }
}
