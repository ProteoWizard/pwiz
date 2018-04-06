/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace pwiz.Common.DataBinding.RowSources
{
    /// <summary>
    /// Just like <see cref="Collection{T}" /> except that <see cref="WrappedCollection{T}.Items"/> is virtual, which enables 
    /// the collection to be lazy initialized.
    /// </summary>
    public class WrappedCollection<T> : IList<T>, IList
    {
        protected WrappedCollection()
        {
            Items = new List<T>();
        }

        protected virtual IList<T> Items { get; private set; }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        public void Add(T item)
        {
            Items.Add(item);
        }

        public void Clear()
        {
            Items.Clear();
        }

        public bool Contains(T item)
        {
            return Items.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Items.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            return Items.Remove(item);
        }

        public int Count
        {
            get { return Items.Count; }
        }

        public virtual bool IsReadOnly
        {
            get { return Items.IsReadOnly; }
        }

        public int IndexOf(T item)
        {
            return Items.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            Items.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            Items.RemoveAt(index);
        }

        public T this[int index]
        {
            get { return Items[index]; }
            set { Items[index] = value; }
        }

        void ICollection.CopyTo(Array array, int index)
        {
            for (int i = 0; i < Count; i++)
            {
                array.SetValue(this[i], i + index);
            }
        }

        object ICollection.SyncRoot
        {
            get { return this; }
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        int IList.Add(object value)
        {
            Add((T) value);
            return Count - 1;
        }

        bool IList.Contains(object value)
        {
            if (!IsCompatibleObject(value))
            {
                return false;
            }
            return Contains((T) value);
        }

        int IList.IndexOf(object value)
        {
            if (!IsCompatibleObject(value))
            {
                return -1;
            }
            return IndexOf((T) value);
        }

        void IList.Insert(int index, object value)
        {
            Insert(index, (T)value);
        }

        void IList.Remove(object value)
        {
            if (IsCompatibleObject(value))
            {
                Remove((T) value);
            }
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set { this[index] = (T) value; }
        }
        bool IList.IsFixedSize { get { return false; } }

        private static bool IsCompatibleObject(object value)
        {
            if (value is T)
                return true;
            if (value == null)
                return ReferenceEquals(default(T), null);
            return false;
        }
    }
}
