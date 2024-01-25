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
using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Collections.Transpositions
{
    /// <summary>
    /// Represents a list of objects (rows) where the fields (columns) or the rows
    /// are represented in separate lists.
    /// </summary>
    public abstract class Transposition : Immutable
    {
        private ImmutableList<ColumnData> _columns;
        
        public Transposition ChangeColumns(IEnumerable<ColumnData> columns)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im._columns = ImmutableList.ValueOf(columns);
            });
        }

        public Transposition ChangeColumnAt(int columnIndex, ColumnData column)
        {
            ColumnData currentColumn = default;
            if (columnIndex < _columns.Count)
            {
                currentColumn = _columns[columnIndex];
            }

            if (Equals(currentColumn, column))
            {
                return this;
            }

            ColumnData[] newColumns = new ColumnData[Math.Max(_columns.Count, columnIndex + 1)];
            _columns.CopyTo(newColumns, 0);
            newColumns[columnIndex] = column;
            int nonEmptyCount = newColumns.Length;
            while (nonEmptyCount > 0 && newColumns[nonEmptyCount - 1].IsEmpty)
            {
                nonEmptyCount--;
            }

            ImmutableList<ColumnData> immutableList;
            if (nonEmptyCount == newColumns.Length)
            {
                immutableList = ImmutableList.ValueOf(newColumns);
            }
            else
            {
                immutableList = ImmutableList.ValueOf(newColumns.Take(nonEmptyCount));
            }
            return ChangeProp(ImClone(this), im => im._columns = immutableList);
        }

        public IEnumerable<ColumnData> ColumnDatas
        {
            get
            {
                return _columns;
            }
        }

        public ColumnData GetColumnValues(int columnIndex)
        {
            if (columnIndex < _columns.Count)
            {
                return _columns[columnIndex];
            }

            return default;
        }
    }

    public abstract class Transposition<TRow> : Transposition
    {
        public abstract Transposer<TRow> GetTransposer();

        public TRow[] ToRows(int start, int count)
        {
            return GetTransposer().ToRows(ColumnDatas, start, count);
        }

        public TRow GetRow(int index)
        {
            return ToRows(index, 1)[0];
        }

        public Transposition ChangeRows(ICollection<TRow> rows)
        {
            return ChangeColumns(GetTransposer().ToColumns(rows));
        }
    }
}
