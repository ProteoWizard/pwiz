using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace pwiz.Common.Collections
{
    public class BoundList<TItem> : IList<TItem>
    {
        private List<int> _deletedIndexes = new List<int>();
        private List<int> _newIndexes = new List<int>();
        private List<TItem> _newItems = new List<TItem>();
        private Dictionary<int, TItem> _modifiedItems = new Dictionary<int, TItem>();

        public event ListChangedEventHandler ListChanged;

        public BoundList(IList<TItem> originalList)
        {
            OriginalList = originalList;
        }
        public BoundList() : this(new TItem[0])
        {
        }

        public BoundList<TItem> Clone()
        {
            return new BoundList<TItem>(OriginalList)
                       {
                           _deletedIndexes = _deletedIndexes.ToList(),
                           _newIndexes = _newIndexes.ToList(),
                           _newItems = _newItems.ToList(),
                           _modifiedItems = new Dictionary<int, TItem>(_modifiedItems),
                       };
        }

        public IList<TItem> OriginalList { get; private set; }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<TItem> GetEnumerator()
        {
            int deletedIndex = 0;
            int newIndex = 0;
            for (int index = 0; index < Count; index ++)
            {
                int originalIndex = AdvanceToIndex(index, ref deletedIndex, ref newIndex);
                if (newIndex < _newIndexes.Count && originalIndex == _newIndexes[newIndex])
                {
                    yield return _newItems[newIndex];
                }
                else
                {
                    TItem item;
                    if (_modifiedItems.TryGetValue(originalIndex, out item))
                    {
                        yield return item;
                    }
                    else
                    {
                        yield return OriginalList[originalIndex];
                    }
                }
            }
        }

        private int AdvanceToIndex(int index, ref int deletedIndex, ref int newIndex)
        {
            while (true) 
            {
                int originalOffset = index + deletedIndex - newIndex;
                int nextNewIndex = newIndex < _newIndexes.Count ? _newIndexes[newIndex] : int.MaxValue;
                int nextDeletedIndex = deletedIndex < _deletedIndexes.Count
                                           ? _deletedIndexes[deletedIndex]
                                           : int.MaxValue;
                if (nextDeletedIndex < nextNewIndex)
                {
                    if (nextDeletedIndex <= originalOffset)
                    {
                        deletedIndex++;
                        continue;
                    }
                    return originalOffset;
                }
                if (nextNewIndex < originalOffset)
                {
                    newIndex++;
                    continue;
                }
                return originalOffset;
            }
        }

        private int FindIndex(int index, out int deletedIndex, out int newIndex)
        {
            deletedIndex = _deletedIndexes.BinarySearch(index - _newIndexes.Count);
            if (deletedIndex < 0)
            {
                deletedIndex = ~deletedIndex;
            }
            newIndex = 0;
            if (index + deletedIndex > _newIndexes.Count)
            {
                newIndex = _newIndexes.BinarySearch(index - _newIndexes.Count);
                if (newIndex < 0)
                {
                    newIndex = ~newIndex;
                }
            }
            return AdvanceToIndex(index, ref deletedIndex, ref newIndex);
        }

        public void Add(TItem item)
        {
            Insert(Count, item);
        }

        public void Clear()
        {
            _deletedIndexes = Enumerable.Range(0, OriginalList.Count).ToList();
            _newIndexes.Clear();
            _newItems.Clear();
            _modifiedItems.Clear();
            Debug.Assert(Validate());
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        public bool Contains(TItem item)
        {
            return IndexOf(item) >= 0;
        }

        public void CopyTo(TItem[] array, int arrayIndex)
        {
            foreach (var item in this)
            {
                array.SetValue(item, arrayIndex++);
            }
        }

        public bool Remove(TItem item)
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
            get { return OriginalList.Count - _deletedIndexes.Count + _newIndexes.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public int IndexOf(TItem item)
        {
            int index = 0;
            foreach (var myItem in this)
            {
                if (Equals(myItem, item))
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        public void Insert(int index, TItem item)
        {
            int deletedIndex;
            int newIndex;
            int originalIndex = FindIndex(index, out deletedIndex, out newIndex);
            _newIndexes.Insert(newIndex, originalIndex);
            _newItems.Insert(newIndex, item);
            OnListChanged(new ListChangedEventArgs(ListChangedType.ItemAdded, index));
        }

        public void RemoveAt(int index)
        {
            int deletedIndex;
            int newIndex;
            int originalIndex = FindIndex(index, out deletedIndex, out newIndex);
            if (newIndex < _newIndexes.Count && originalIndex == _newIndexes[newIndex])
            {
                _newIndexes.RemoveAt(newIndex);
                _newItems.RemoveAt(newIndex);
            }
            else
            {
                _deletedIndexes.Insert(deletedIndex, originalIndex);
                _modifiedItems.Remove(originalIndex);
            }
            OnListChanged(new ListChangedEventArgs(ListChangedType.ItemDeleted, index));
        }

        public TItem this[int index]
        {
            get
            {
                int deletedIndex;
                int newIndex;
                int originalIndex = FindIndex(index, out deletedIndex, out newIndex);
                if (newIndex < _newIndexes.Count && originalIndex == _newIndexes[newIndex])
                {
                    return _newItems[newIndex];
                }
                TItem item;
                if (_modifiedItems.TryGetValue(originalIndex, out item))
                {
                    return item;
                }
                return OriginalList[originalIndex];
            }
            set
            {
                int deletedIndex;
                int newIndex;
                int originalIndex = FindIndex(index, out deletedIndex, out newIndex);
                if (newIndex < _newIndexes.Count && originalIndex == _newIndexes[newIndex])
                {
                    _newItems[newIndex] = value;
                    return;
                }
                _modifiedItems[originalIndex] = value;
                OnListChanged(new ListChangedEventArgs(ListChangedType.ItemChanged, index));
            }
        }

        public bool Validate()
        {
            for (int i = 0; i < _deletedIndexes.Count; i++)
            {
                if (_deletedIndexes[i] < i || _deletedIndexes[i] >= OriginalList.Count)
                {
                    throw new InvalidDataException("Invalid deleted index");
                }
                if (i > 0 && _deletedIndexes[i] <= _deletedIndexes[i-1])
                {
                    throw new InvalidDataException("Deleted indexes must be increasing");
                }
            }
            for (int i = 0; i < _newIndexes.Count; i++)
            {
                if (_newIndexes[i] > OriginalList.Count || _newIndexes[i] < 0)
                {
                    throw new InvalidDataException("Invalid new index");
                }
                if (i > 0 && _newIndexes[i] < _newIndexes[i-1])
                {
                    throw new InvalidDataException("New indexes must be non-decreasing");
                }
            }
            foreach (var key in _modifiedItems.Keys)
            {
                if (key < 0 || key >= OriginalList.Count)
                {
                    throw new InvalidDataException("Invalid modified index");
                }
                if (_deletedIndexes.Contains(key))
                {
                    throw new InvalidDataException("Modified index has been deleted");
                }
            }
            return true;
        }

        protected void OnListChanged(ListChangedEventArgs listChangedEventArgs)
        {
            if (ListChanged != null)
            {
                ListChanged(this, listChangedEventArgs);
            }
        }
        public class ListChange
        {
            public ListChange(ListChangedType listChangedType, int oldIndex, TItem oldItem, int newIndex, TItem newItem)
            {
                ListChangedType = listChangedType;
                OldIndex = oldIndex;
                OldItem = oldItem;
                NewIndex = newIndex;
                NewItem = newItem;
            }
            public ListChangedType ListChangedType { get; private set;}
            public int OldIndex { get; private set; }
            public TItem OldItem { get; private set; }
            public int NewIndex { get; private set; }
            public TItem NewItem { get; private set; }
        }

        public IEnumerable<ListChange> ItemDeletions
        {
            get
            {
                return
                    _deletedIndexes.Select(
                        i => new ListChange(ListChangedType.ItemDeleted, i, OriginalList[i], -1, default(TItem)));
            }
        }

        public IEnumerable<ListChange> ItemAdditions
        {
            get
            {
                return
                    _newIndexes.Select(
                        (oldIndex, changeIndex) => new ListChange(
                            ListChangedType.ItemAdded, -1, default(TItem),
                            changeIndex + _newIndexes[changeIndex] - IndexOfSupremum(_deletedIndexes, _newIndexes[changeIndex]),
                            _newItems[changeIndex]
                        )
                    );
            }
        }

        public IEnumerable<ListChange> ItemChanges
        {
            get
            {
                return
                    _modifiedItems.Select(kvp => new ListChange(
                        ListChangedType.ItemChanged, kvp.Key, OriginalList[kvp.Key],
                        kvp.Key - IndexOfSupremum(_deletedIndexes, kvp.Key) + IndexOfSupremum(_newIndexes, kvp.Key + 1), 
                        kvp.Value
                    )
                );
            }
        }

        /// <summary>
        /// Returns the index of the first element that is greater than or equal to key,
        /// or the length of the list if key is greater than all items
        /// in the list.
        /// </summary>
        private static int IndexOfSupremum(List<int> keys, int key)
        {
            int index = keys.BinarySearch(key);
            while (index > 0 && keys[index - 1] == key)
            {
                index--;
            }
            return index < 0 ? ~index : index;
        }
    }
}
