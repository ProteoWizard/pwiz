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
using System.Linq;

namespace pwiz.Common.Collections.Transpositions
{
    public abstract class ColumnData
    {
        public static ColumnData<T> ForConstant<T>(T value)
        {
            return ColumnData<T>.ForConstant(value);
        }

        public static ColumnData<T> ForValues<T>(IEnumerable<T> values)
        {
            return ColumnData<T>.ForValues(ImmutableList<T>.ValueOf(values));
        }

        public static ColumnData<T> ForList<T>(IReadOnlyList<T> list)
        {
            return ColumnData<T>.ForList(list);
        }

        public abstract bool IsList { get; }
        public abstract bool IsImmutableList { get; }
        public abstract bool SequenceEqual(IEnumerable values);
        public abstract bool ContentsEqual(ColumnData columnData);
        public abstract int? RowCount { get; }

        public static bool ContentsEqual(ColumnData columnData1, ColumnData columnData2)
        {
            if (ReferenceEquals(columnData1, columnData2))
            {
                return true;
            }

            int count = columnData1?.RowCount ?? columnData2?.RowCount ?? 1;
            if (columnData1 != null)
            {
                return columnData1.ContentsEqual(columnData2);
            }

            return columnData2.ContentsEqual(columnData1);
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
        public abstract ImmutableList<T> ToImmutableList();

        public sealed override bool SequenceEqual(IEnumerable enumerable)
        {
            return SequenceEqual(enumerable.Cast<T>());
        }

        protected virtual bool SequenceEqual(IEnumerable<T> enumerable)
        {
            int i = 0;
            foreach (var item in enumerable)
            {
                if (!Equals(GetValue(i), item))
                {
                    return false;
                }
            }
            return true;
        }

        public sealed override bool ContentsEqual(ColumnData columnData)
        {
            return ContentsEqual((ColumnData<T>)columnData ?? new ConstantColumnData(default));
        }

        protected virtual bool ContentsEqual(ColumnData<T> columnData)
        {
            int count = RowCount ?? columnData.RowCount ?? 1;
            return Enumerable.Range(0, count).All(i => Equals(GetValue(i), columnData.GetValue(i)));
        }


        public static ColumnData<T> ForConstant(T value)
        {
            return Equals(default(T), value) ? null : new ConstantColumnData(value);
        }

        public static ColumnData<T> ForValues(IEnumerable<T> values)
        {
            var immutableList = ImmutableList.ValueOf(values);
            if (immutableList == null)
            {
                return ForConstant(default);
            }
            return new ListColumnData(ImmutableList.ValueOf(immutableList));
        }

        public static ColumnData<T> ForList(IReadOnlyList<T> list)
        {
            if (list == null)
            {
                return null;
            }

            return new ListColumnData(list);
        }


        private class ConstantColumnData : ColumnData<T>
        {
            private T _value;
            public ConstantColumnData(T value)
            {
                _value = value;
            }
            public override T GetValue(int row)
            {
                return _value;
            }

            public override bool IsList
            {
                get { return false; }
            }
            public override bool IsImmutableList
            {
                get { return false; }
            }

            public override ImmutableList<T> ToImmutableList()
            {
                return null;
            }

            protected override bool ContentsEqual(ColumnData<T> otherColumn)
            {
                if (otherColumn is ConstantColumnData otherConstant)
                {
                    return Equals(_value, otherConstant._value);
                }

                return base.ContentsEqual(otherColumn);
            }

            public override int? RowCount
            {
                get { return null; }
            }
        }

        public class ListColumnData : ColumnData<T>
        {
            private IReadOnlyList<T> _readOnlyList;
            public ListColumnData(IReadOnlyList<T> readOnlyList)
            {
                _readOnlyList = readOnlyList;
            }

            public override T GetValue(int row)
            {
                return _readOnlyList[row];
            }

            public override bool IsList
            {
                get { return true; }
            }

            public override bool IsImmutableList
            {
                get
                {
                    return _readOnlyList is ImmutableList<T>;
                }
            }

            public override ImmutableList<T> ToImmutableList()
            {
                return ImmutableList.ValueOf(_readOnlyList);
            }

            protected override bool ContentsEqual(ColumnData<T> otherColumn)
            {
                if (otherColumn is ListColumnData otherList)
                {
                    if (_readOnlyList is ImmutableList<T> && otherList._readOnlyList is ImmutableList<T>)
                    {
                        return Equals(_readOnlyList, otherList._readOnlyList);
                    }

                    return _readOnlyList.SequenceEqual(otherList._readOnlyList);
                }

                return base.ContentsEqual(otherColumn);
            }

            public override int? RowCount
            {
                get
                {
                    return _readOnlyList.Count;
                }
            }
        }
    }
}