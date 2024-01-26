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
    }
    /// <summary>
    /// Holds data for one column in a <see cref="Transposition"/>.
    /// 
    /// </summary>
    public abstract class ColumnData<T> : ColumnData
    {

        private ColumnData()
        {
        }
        public abstract T GetValue(int row);
        public abstract ImmutableList<T> ToImmutableList();

        public static ColumnData<T> ForConstant(T value)
        {
            return Equals(default(T), value) ? null : new ConstantColumnData(value);
        }

        public static ColumnData<T> ForValues(IEnumerable<T> values)
        {
            var immutableList = ImmutableList.ValueOf(values);
            if (immutableList == null)
            {
                return null;
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
        }
    }
}