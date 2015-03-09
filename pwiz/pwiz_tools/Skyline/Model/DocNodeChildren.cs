using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model
{
    public class DocNodeChildren : IList<DocNode>
    {
        private readonly IList<DocNode> _items;
        private readonly Dictionary<Identity, int> _indexes;

        public DocNodeChildren(IEnumerable<DocNode> items)
        {
            _items = ImmutableList.ValueOf(items);
            _indexes = new Dictionary<Identity, int>(_items.Count, IDENTITY_EQUALITY_COMPARER);
            for (int i = 0; i < _items.Count; i++)
            {
                _indexes.Add(_items[i].Id, i);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<DocNode> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        void ICollection<DocNode>.Add(DocNode item)
        {
            throw new InvalidOperationException();
        }

        void ICollection<DocNode>.Clear()
        {
            throw new InvalidOperationException();
        }

        bool ICollection<DocNode>.Contains(DocNode item)
        {
            int index = IndexOf(item.Id);
            return index >= 0 && Equals(item, _items[index]);
        }

        public void CopyTo(DocNode[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _items.Count; }
        }

        bool ICollection<DocNode>.Remove(DocNode item)
        {
            throw new InvalidOperationException();
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        int IList<DocNode>.IndexOf(DocNode item)
        {
            int index = IndexOf(item.Id);
            if (index < 0)
            {
                return -1;
            }
            if (!Equals(_items[index], item))
            {
                return -1;
            }
            return index;
        }

        public int IndexOf(Identity id)
        {
            int index;
            if (!_indexes.TryGetValue(id, out index))
            {
                return -1;
            }
            return index;
        }

        void IList<DocNode>.Insert(int index, DocNode item)
        {
            throw new InvalidOperationException();
        }

        void IList<DocNode>.RemoveAt(int index)
        {
            throw new InvalidOperationException();
        }

        public DocNode this[int index]
        {
            get { return _items[index]; }
            set { throw new InvalidOperationException(); }
        }

        #region Equality members
        protected bool Equals(DocNodeChildren other)
        {
            return _items.Equals(other._items);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DocNodeChildren) obj);
        }

        public override int GetHashCode()
        {
            return _items.GetHashCode();
        }
        #endregion

        private static readonly IdentityEqualityComparer IDENTITY_EQUALITY_COMPARER = new IdentityEqualityComparer();
        private class IdentityEqualityComparer : IEqualityComparer<Identity>
        {
            public bool Equals(Identity x, Identity y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(Identity obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
