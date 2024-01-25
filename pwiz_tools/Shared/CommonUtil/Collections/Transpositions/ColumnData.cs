/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;

namespace pwiz.Common.Collections.Transpositions
{
    /// <summary>
    /// Holds data for one column in a <see cref="Transposition"/>.
    /// 
    /// </summary>
    public struct ColumnData
    {
        /// <summary>
        /// The data in the column. It can be one of the following types of values:
        /// <ul>null: every value in the column is equal to the default value for the column's datatype</ul>
        /// <ul><see cref="Constant{T}"/>: every value in the column is equal to that constant value</ul>
        /// <ul>A list of length N: The first N values in the column are equal to the items in the list. Any values beyond that are equal to the default value of the datatype.</ul>
        /// </summary>
        private object _data;

        public static ColumnData Constant<T>(T value)
        {
            return new ColumnData(new ConstantValue<T>(value));
        }

        public static ColumnData Immutable<T>(IEnumerable<T> immutableList)
        {
            return new ColumnData(ImmutableList.ValueOf(immutableList));
        }

        public static ColumnData FactorList<T>(FactorList<T> list)
        {
            return new ColumnData(list);
        }

        private ColumnData(IEnumerable list)
        {
            _data = list;
        }

        public bool IsEmpty
        {
            get { return _data == null; }
        }

        public bool IsConstant<T>()
        {
            return _data is ConstantValue<T>;
        }

        public T GetValueAt<T>(int index)
        {
            if (_data == null)
            {
                return default;
            }

            if (_data is ConstantValue<T> constant)
            {
                return constant.Value;
            }

            var readOnlyList = (IReadOnlyList<T>)_data;
            if (index < 0 || index >= readOnlyList.Count)
            {
                return default;
            }
            return readOnlyList[index];
        }

        public ImmutableList<T> TryGetValues<T>()
        {
            return ImmutableList.ValueOf(_data as IReadOnlyList<T>);
        }

        public bool IsImmutableList<T>()
        {
            return _data is ImmutableList<T>;
        }

        private class ConstantValue<T> : IEnumerable
        {
            public ConstantValue(T value)
            {
                Value = value;
            }
            public T Value { get; }
            public IEnumerator GetEnumerator()
            {
                yield break;
            }

            protected bool Equals(ConstantValue<T> other)
            {
                return EqualityComparer<T>.Default.Equals(Value, other.Value);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ConstantValue<T>)obj);
            }

            public override int GetHashCode()
            {
                return EqualityComparer<T>.Default.GetHashCode(Value);
            }
        }

        public bool Equals(ColumnData other)
        {
            return Equals(_data, other._data);
        }

        public override bool Equals(object obj)
        {
            return obj is ColumnData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _data?.GetHashCode() ?? 0;
        }
    }
}