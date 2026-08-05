using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model
{
    public class DocNodeChildren : IList<DocNode>
    {
        private readonly int _itemCount;    // Necessary for knowing the count after items have been freed
        private ImmutableList<DocNode> _items;
        private IdentityIndex _indexes;

        public DocNodeChildren(IEnumerable<DocNode> items, IList<DocNode> previous)
        {
            _items = ImmutableList.ValueOf(items);
            _itemCount = _items.Count;
            var previousChildren = previous as DocNodeChildren;
            if (previousChildren != null && previousChildren._items != null && IsOrderSame(previousChildren))
                _indexes = previousChildren._indexes;
            else
                _indexes = new IdentityIndex(_items.Select(item => item.Id));
        }

        private DocNodeChildren(IdentityIndex indexes, ImmutableList<DocNode> items)
        {
            _itemCount = items.Count;
            _items = items;
            _indexes = indexes;
        }

        /// <summary>
        /// Where each child's <see cref="Identity"/> sits, which anything holding values in the
        /// same order as these children can hold onto to address them by identity instead of by
        /// position. Null once <see cref="ReleaseChildren"/> has let the children go.
        /// </summary>
        public IdentityIndex IdentityIndex
        {
            get { return _indexes; }
        }

        private bool IsOrderSame(DocNodeChildren previousChildren)
        {
            if (_itemCount != previousChildren._itemCount)
                return false;
            for (int i = 0; i < _itemCount; i++)
            {
                if (!ReferenceEquals(_items[i].Id, previousChildren._items[i].Id))
                    return false;
            }
            return true;
        }

        public DocNodeChildren ReplaceAt(int index, DocNode child)
        {
            var newItems = _items.ReplaceAt(index, child);
            if (ReferenceEquals(child.Id, _items[index].Id))
            {
                return new DocNodeChildren(_indexes, newItems);
            }
            return new DocNodeChildren(newItems, null);
        }

        /// <summary>
        /// This breaks immutability, but is necessary for keeping
        /// memory under control during command-line processing of very large files
        /// </summary>
        public void ReleaseChildren()
        {
            _items = null;
            _indexes = null;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<DocNode> GetEnumerator()
        {
            if (_items == null)
                return GetEmptyEnumerator();
            return _items.GetEnumerator();
        }

        private IEnumerator<DocNode> GetEmptyEnumerator()
        {
            for (int i = 0; i < _itemCount; i++)
                yield return null;
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
            if (ReferenceEquals(null, item))
            {
                return false;
            }
            int index = IndexOf(item.Id);
            return index >= 0 && Equals(item, _items[index]);
        }

        public void CopyTo(DocNode[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _itemCount; }
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
            if (ReferenceEquals(item, null))
            {
                return -1;
            }
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
            return _indexes?.IndexOf(id) ?? -1;
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
            get { return _items != null ? _items[index] : null; }
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
            return _items != null ? _items.GetHashCode() : 0;
        }
        #endregion
    }
}
