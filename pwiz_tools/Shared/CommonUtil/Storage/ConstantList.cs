using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.Storage
{
    public class ConstantList<T> : IReadOnlyList<T>
    {
        private readonly T _value;
        public ConstantList(T value, int count)
        {
            _value = value;
            Count = count;
        }
        
        public int Count { get; }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Enumerable.Repeat(_value, Count).GetEnumerator();
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _value;
            }
        }
    }
}
