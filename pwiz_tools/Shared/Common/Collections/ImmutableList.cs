/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
    /// Factory methods for constructing <see cref="ImmutableList{T}"/>.
    /// </summary>
    public static class ImmutableList
    {
        /// <summary>
        /// Returns an <see cref="ImmutableList{T}"/> containing the passed
        /// in items, or null if <paramref name="values"/> is null.
        /// </summary>
        public static ImmutableList<T> ValueOf<T>(IEnumerable<T> values)
        {
            return ImmutableList<T>.ValueOf(values);
        }

        /// <summary>
        /// Behaves like <see cref="ImmutableList.ValueOf{T}"/>, but returns an empty ImmutableList
        /// if <see paramref="values"/> is null.
        /// </summary>
        public static ImmutableList<T> ValueOfOrEmpty<T>(IEnumerable<T> values)
        {
            if (null == values)
            {
                return ImmutableList<T>.EMPTY;
            }
            return ValueOf(values);
        }
        public static ImmutableList<T> Empty<T>()
        {
            return ImmutableList<T>.EMPTY;
        }
        public static ImmutableList<T> Singleton<T>(T value)
        {
            return ImmutableList<T>.Singleton(value);
        }
    }

    /// <summary>
    /// Read only list of elements.  
    /// This class differs from <see cref="System.Collections.ObjectModel.ReadOnlyCollection{T}" />
    /// which is only a read-only wrapper around a potentially modifiable collection.  ImmutableList
    /// guarantees that its contents cannot be modified by anyone.
    /// ImmutableList also overrides Equals and GetHashCode to provide list contents equality
    /// semantics.
    /// Instances of ImmutableList cannot be constructed directly, instead use the factory methods in
    /// <see cref="ImmutableList"/> which check whether the passed in collection is already
    /// an ImmutableList, and makes a copy of the collection as appropriate.
    /// </summary>
    public abstract class ImmutableList<T> : IReadOnlyList<T>, IList<T>
    {
        public static readonly ImmutableList<T> EMPTY = new Impl(new T[0]);
        public static ImmutableList<T> ValueOf(IEnumerable<T> values)
        {
            if (values == null)
            {
                return null;
            }
            var immutableList = values as ImmutableList<T>;
            if (immutableList != null)
            {
                return immutableList;
            }
            var arrayValues = values.ToArray();
            if (arrayValues.Length == 0)
            {
                return EMPTY;
            }
            if (arrayValues.Length == 1)
            {
                return Singleton(arrayValues[0]);
            }
            return new Impl(arrayValues);
        }

        public static ImmutableList<T> Singleton(T value)
        {
            return new SingletonImpl(value);
        }

        /// <summary>
        /// Private constructor to disallow any other implementations of this class.
        /// </summary>
        private ImmutableList()
        {
        }

        public override int GetHashCode()
        {
            return CollectionUtil.GetHashCodeDeep(this);
        }

        public override bool Equals(object o)
        {
            if (o == null)
            {
                return false;
            }
            if (o == this)
            {
                return true;
            }
            var that = o as ImmutableList<T>;
            if (null == that)
            {
                return false;
            }
            return this.SequenceEqual(that);
        }

        public abstract int Count { get; }

        void ICollection<T>.Add(T item)
        {
            throw new InvalidOperationException();
        }

        void ICollection<T>.Clear()
        {
            throw new InvalidOperationException();
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new InvalidOperationException();
        }

        public bool IsReadOnly { get { return true; } }
        void IList<T>.Insert(int index, T item)
        {
            throw new InvalidOperationException();
        }

        void IList<T>.RemoveAt(int index)
        {
            throw new InvalidOperationException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public abstract IEnumerator<T> GetEnumerator();

        public abstract bool Contains(T item);
        public abstract void CopyTo(T[] array, int arrayIndex);

        public abstract int IndexOf(T item);
        public abstract T this[int index] { get; }

        T IList<T>.this[int index]
        {
            get { return this[index]; }
            set { throw new InvalidOperationException(); }
        }

        private class Impl : ImmutableList<T>
        {
            private readonly IList<T> _items;
            public Impl(T[] items)
            {
                _items = items;
            }

            public override int Count
            {
                get { return _items.Count; }
            }

            public override IEnumerator<T> GetEnumerator()
            {
                return _items.GetEnumerator();
            }

            public override bool Contains(T item)
            {
                return _items.Contains(item);
            }

            public override void CopyTo(T[] array, int arrayIndex)
            {
                _items.CopyTo(array, arrayIndex);
            }

            public override int IndexOf(T item)
            {
                return _items.IndexOf(item);
            }

            public override T this[int index]
            {
                get { return _items[index]; }
            }
        }

        private class SingletonImpl : ImmutableList<T>
        {
            private readonly T _item;
            public SingletonImpl(T item)
            {
                _item = item;
            }

            public override int Count
            {
                get { return 1; }
            }

            public override IEnumerator<T> GetEnumerator()
            {
                yield return _item;
            }

            public override bool Contains(T item)
            {
                return Equals(_item, item);
            }

            public override void CopyTo(T[] array, int arrayIndex)
            {
                new[] {_item}.CopyTo(array, arrayIndex);
            }

            public override int IndexOf(T item)
            {
                return Equals(_item, item) ? 0 : -1;
            }

            public override T this[int index]
            {
                get
                {
                    if (index != 0)
                    {
                        throw new IndexOutOfRangeException();
                    }
                    return _item;
                }
            }
        }
    }
}
