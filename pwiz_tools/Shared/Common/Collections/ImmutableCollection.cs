using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Common.Collections
{
    public class ImmutableCollection<T> : ICollection<T>
    {
        private ICollection<T> _collection;
        public ImmutableCollection(ICollection<T> collection)
        {
            Collection = collection;
        }
        protected ImmutableCollection()
        {
        }

        protected ICollection<T> Collection
        {
            get { return _collection;} 
            set { 
                if (_collection != null)
                {
                    throw new InvalidOperationException();
                }
                _collection = value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Collection.GetEnumerator();
        }

        public void Add(T item)
        {
            throw new InvalidOperationException();
        }

        public void Clear()
        {
            throw new InvalidOperationException();
        }

        public bool Contains(T item)
        {
            return Collection.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Collection.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            throw new InvalidOperationException();
        }

        public int Count
        {
            get { return Collection.Count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }
    }
}
