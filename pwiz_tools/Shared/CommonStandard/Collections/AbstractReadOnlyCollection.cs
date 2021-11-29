/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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

namespace pwiz.Common.Collections
{
    public abstract class AbstractReadOnlyCollection<T> : ICollection<T>
    {
        void ICollection<T>.Add(T item)
        {
            throw new InvalidOperationException();
        }

        void ICollection<T>.Clear()
        {
            throw new InvalidOperationException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        bool ICollection<T>.IsReadOnly
        {
            get { return true; }
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new InvalidOperationException();
        }

        public abstract int Count { get; }
        public abstract IEnumerator<T> GetEnumerator();

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
    }
}
