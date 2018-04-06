/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Linq;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// Implementation of a read only IList. Subclasses are only required
    /// to provide implementations for the methods "Count" and the
    /// indexer "this[int index]".
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class AbstractReadOnlyList<T> : IList<T>, IReadOnlyList<T>
    {
        public abstract int Count { get; }
        public abstract T this[int index] { get; }

#region IList<T> operations
        public virtual bool Contains(T item)
        {
            return IndexOf(item) >= 0;
        }

        public virtual void CopyTo(T[] array, int arrayIndex)
        {
            foreach (var item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public virtual int IndexOf(T item)
        {
            int index = 0;
            foreach (var x in this)
            {
                if (Equals(x, item))
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        public virtual bool IsReadOnly { get { return true; } }

        public virtual IEnumerator<T> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(i => this[i]).GetEnumerator();
        }
        T IList<T>.this[int index]
        {
            get { return this[index]; }
            set { throw new InvalidOperationException(); }
        }
#endregion

#region Unsupported IList operations which throw InvalidOperationException
        void ICollection<T>.Add(T item)
        {
            throw new InvalidOperationException();
        }

        void ICollection<T>.Clear()
        {
            throw new InvalidOperationException();
        }

        void IList<T>.Insert(int index, T item)
        {
            throw new InvalidOperationException();
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new InvalidOperationException();
        }

        void IList<T>.RemoveAt(int index)
        {
            throw new InvalidOperationException();
        }
#endregion
    }
}
