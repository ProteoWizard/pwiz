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

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace pwiz.Common.Collections.Transpositions
{
    public abstract class ColumnData
    {
        [CanBeNull]
        public static ColumnData<T> ForConstant<T>(T value)
        {
            return ColumnData<T>.ForConstant(value);
        }

        public static ColumnData<T> ForValues<T>(IEnumerable<T> values)
        {
            return ForList(ImmutableList<T>.ValueOf(values));
        }

        public static ColumnData<T> ForList<T>(IReadOnlyList<T> list)
        {
            return ColumnData<T>.List.ForList(list);
        }
    }
    /// <summary>
    /// Holds data for one column in a <see cref="Transposition"/>.
    /// </summary>
    public abstract class ColumnData<T> : ColumnData
    {
        private ColumnData()
        {
        }
        public abstract T GetValue(int row);

        public static ColumnData<T> ForConstant(T value)
        {
            return Constant.ForValue(value);
        }

        public static ColumnData<T> ForValues(IEnumerable<T> values)
        {
            return List.ForReadOnlyList(ImmutableList.ValueOf(values));
        }

        public static ColumnData<T> ForList(IReadOnlyList<T> list)
        {
            return List.ForReadOnlyList(list);
        }

        /// <summary>
        /// ColumnData representing all values being the same.
        /// This class is only used if the value is not the default(T).
        /// Null should be used to represent the default constant.
        /// </summary>
        private class Constant : ColumnData<T>
        {
            public static ColumnData<T> ForValue(T value)
            {
                return Equals(value, default(T)) ? null : new Constant(value);
            }
            private T _value;
            private Constant(T value)
            {
                _value = value;
            }
            public override T GetValue(int row)
            {
                return _value;
            }

            protected bool Equals(Constant other)
            {
                return EqualityComparer<T>.Default.Equals(_value, other._value);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Constant)obj);
            }

            public override int GetHashCode()
            {
                return EqualityComparer<T>.Default.GetHashCode(_value);
            }

            public override string ToString()
            {
                return @"{" + _value + "}";
            }
        }

        /// <summary>
        /// Subclass for column data that is a list of values.
        /// This class is only used for lists that have more than one unique value.
        /// If all of the values in the list are the same, then <see cref="ColumnData{T}.Constant"/> is used instead.
        /// </summary>
        public class List : ColumnData<T>
        {
            public static ColumnData<T> ForReadOnlyList(IReadOnlyList<T> readOnlyList)
            {
                if (readOnlyList == null || readOnlyList.Count == 0)
                {
                    return null;
                }

                var firstValue = readOnlyList[0];
                if (Enumerable.Range(1, readOnlyList.Count - 1).All(i => Equals(firstValue, readOnlyList[i])))
                {
                    return Constant.ForValue(firstValue);
                }

                return new List(readOnlyList);
            }
            private IReadOnlyList<T> _readOnlyList;
            private List(IReadOnlyList<T> readOnlyList)
            {
                _readOnlyList = readOnlyList;
            }

            public override T GetValue(int row)
            {
                return _readOnlyList[row];
            }

            public bool IsImmutableList
            {
                get
                {
                    return _readOnlyList is ImmutableList<T>;
                }
            }

            public ImmutableList<T> ToImmutableList()
            {
                return ImmutableList.ValueOf(_readOnlyList);
            }

            public int RowCount
            {
                get
                {
                    return _readOnlyList.Count;
                }
            }

            protected bool Equals(List other)
            {
                return _readOnlyList.SequenceEqual(other._readOnlyList);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((List)obj);
            }

            public override int GetHashCode()
            {
                return ToImmutableList().GetHashCode();
            }

            public override string ToString()
            {
                return @"[" + string.Join(@",", _readOnlyList) + "]";
            }
        }
    }
}