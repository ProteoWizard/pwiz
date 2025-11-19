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
            var elements = values as IList<T>;
            T[] arrayCopy = null;
            if (elements == null)
                elements = arrayCopy = values.ToArray();
            int length = elements.Count;
            if (length == 0)
            {
                return EMPTY;
            }
            if (length == 1)
            {
                return Singleton(elements[0]);
            }
            return new Impl(arrayCopy ?? elements.ToArray());
        }

        public static ImmutableList<T> ValueOf(T[] values, bool takeBuffer)
        {
            return new Impl(values);
        }

        public static ImmutableList<T> ValueOf(int expectedCount, IEnumerable<T> values)
        {
            if (expectedCount == 0)
            {
                if (values.Any())
                {
                    throw new ArgumentException();
                }
                return EMPTY;
            }

            var array = new T[expectedCount];
            int index = 0;
            foreach (var value in values)
            {
                array[index++] = value;
            }
            if (index != expectedCount)
            {
                throw new ArgumentException();
            }
            if (array.Length == 1)
            {
                return Singleton(array[0]);
            }
            return new Impl(array);
        }

        public static ImmutableList<T> Singleton(T value)
        {
            return new SingletonImpl(value);
        }

        /// <summary>
        /// GetHashCode implementation which cannot be overridden
        /// in derived classes in order to preserve the semantics
        /// that ImmutableList instances are equal if and only if
        /// they contain the same items.
        /// </summary>
        public sealed override int GetHashCode()
        {
            return CollectionUtil.GetHashCodeDeep(this);
        }

        /// <summary>
        /// Equals implementation which cannot be overridden.
        /// </summary>
        public sealed override bool Equals(object o)
        {
            if (o == null)
            {
                return false;
            }
            if (o == this)
            {
                return true;
            }
            if (GetType() == o.GetType())
            {
                return SameTypeEquals((ImmutableList<T>)o);
            }

            var that = o as ImmutableList<T>;
            if (null == that)
            {
                return false;
            }

            if (Count != that.Count)
            {
                return false;
            }

            return this.SequenceEqual(that);
        }

        /// <summary>
        /// Virtual equals implementation which is only called if the list being
        /// compared against is the same type as this. Derived classes may override
        /// this if they have a more efficient way of comparing against other
        /// instances of themselves (e.g. <see cref="ConstantList{T}"/>)
        /// </summary>
        protected virtual bool SameTypeEquals(ImmutableList<T> list)
        {
            return Count == list.Count && this.SequenceEqual(list);
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

        public virtual bool Contains(T item)
        {
            return this.AsEnumerable().Contains(item);
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
            foreach (var v in this)
            {
                if (Equals(v, item))
                {
                    return index;
                }
                index++;
            }

            return -1;
        }
        public abstract T this[int index] { get; }

        T IList<T>.this[int index]
        {
            get { return this[index]; }
            set { throw new InvalidOperationException(); }
        }

        public virtual ImmutableList<T> ReplaceAt(int index, T value)
        {
            var array = this.ToArray();
            array[index] = value;
            return ImmutableList.ValueOf(array);
        }

        private class Impl : ImmutableList<T>
        {
            private readonly T[] _items;
            public Impl(T[] items)
            {
                _items = items;
            }

            public override int Count
            {
                get { return _items.Length; }
            }

            public override IEnumerator<T> GetEnumerator()
            {
                return ((IList<T>)_items).GetEnumerator();
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
                return ((IList<T>)_items).IndexOf(item);
            }

            public override T this[int index]
            {
                get { return _items[index]; }
            }

            public override ImmutableList<T> ReplaceAt(int index, T value)
            {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException();
                }
                var newArray = (T[])_items.Clone();
                newArray[index] = value;
                return new Impl(newArray);
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

            public override ImmutableList<T> ReplaceAt(int index, T value)
            {
                if (index != 0)
                {
                    throw new IndexOutOfRangeException();
                }
                return new SingletonImpl(value);
            }
        }
    }

    public class ConstantList<T> : ImmutableList<T>
    {
        private int _count;
        private T _value;
        public ConstantList(int count, T value)
        {
            _count = count;
            _value = value;
        }

        public override IEnumerator<T> GetEnumerator()
        {
            return Enumerable.Repeat(_value, _count).GetEnumerator();
        }

        public override int Count
        {
            get { return _count; }
        }

        public override T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _value;
            }
        }

        protected override bool SameTypeEquals(ImmutableList<T> list)
        {
            var that = (ConstantList<T>) list;
            return Count == list.Count && Equals(_value, that._value);
        }
    }

    /// <summary>
    /// Efficiently stores a list of <see cref="Nullable"/> by using
    /// <see cref="IntegerList.Bits"/>.
    /// </summary>
    public class NullableList<T> : ImmutableList<T?> where T : struct
    {
        private ImmutableList<int> _hasValueList;
        private ImmutableList<T> _values;

        public NullableList(IEnumerable<T?> items)
        {
            var values = new List<T>();
            var hasValues = new List<int>();
            foreach (var item in items)
            {
                values.Add(item.GetValueOrDefault());
                hasValues.Add(item.HasValue ? 1 : 0);
            }

            _hasValueList = IntegerList.FromIntegers(hasValues);
            if (typeof(T) == typeof(int))
            {
                _values = (ImmutableList<T>)(object)IntegerList.FromIntegers((List<int>)(object) values);
            }
            else
            {
                _values = values.ToImmutable();
            }
        }

        public override int Count
        {
            get { return _values.Count; }
        }
        public override IEnumerator<T?> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(i => this[i]).GetEnumerator();
        }

        public override T? this[int index]
        {
            get
            {
                return _hasValueList[index] == 0 ? (T?) null : _values[index];
            }
        }
    }
}
