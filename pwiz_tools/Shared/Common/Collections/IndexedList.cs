using System;
using System.Collections;
using System.Collections.Generic;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// List which maintains a dictionary to implement fast <see cref="IList{T}.IndexOf" />.
    /// </summary>
    public class IndexedList<T> : IList<T>
    {
        private Dictionary<T, int> _itemIndexes = new Dictionary<T, int>();
        private List<T> _items = new List<T>();
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        public void Add(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException();
            }
            var index = _items.Count;
            _itemIndexes.Add(item, index);
            _items.Add(item);
        }

        public void Clear()
        {
            _items.Clear();
            _itemIndexes.Clear();
        }

        public bool Contains(T item)
        {
            return IndexOf(item) >= 0;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index < 0)
            {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        public int Count
        {
            get { return _items.Count; }
        }
        public bool IsReadOnly
        {
            get { return false; }
        }
        public int IndexOf(T item)
        {
            if (item == null)
            {
                return -1;
            }
            if (_itemIndexes.TryGetValue(item, out int index))
            {
                return index;
            }

            return -1;
        }

        public void Insert(int index, T item)
        {
            if (index == Count)
            {
                Add(item);
            }
            else
            {
                _items.Insert(index, item);
                RebuildIndex();
            }
        }

        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
            RebuildIndex();
        }

        public T this[int index]
        {
            get
            {
                return _items[index];
            }
            set
            {
                var oldValue = _items[index];
                _itemIndexes.Remove(oldValue);
                _itemIndexes.Add(value, index);
                _items[index] = value;
            }
        }

        private void RebuildIndex()
        {
            _itemIndexes.Clear();
            foreach (var item in _items)
            {
                _itemIndexes.Add(item, _itemIndexes.Count);
            }
        }
    }
}
