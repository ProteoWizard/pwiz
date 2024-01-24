using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.Storage
{
    public class ConstantList<T> : IReadOnlyList<T>
    {
        private readonly T _value;
        public ConstantList(T value)
        {
            _value = value;
        }
        
        public int Count
        {
            get { return int.MaxValue; }
        }

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
                return _value;
            }
        }
    }
}
