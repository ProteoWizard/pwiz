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
using System.Diagnostics;
using System.Linq;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Collections.Transpositions
{
    /// <summary>
    /// Decomposes a list of objects (rows) into a series of lists representing the value of a particular
    /// field (column) across all the rows. The data structure used to represent the column values
    /// can often be optimized to take advantage of repeated values.
    /// </summary>
    public abstract class Transposer<TRow>
    {
        private List<ColumnDef<TRow>> _columnDefs = new List<ColumnDef<TRow>>();

        protected void AddColumn<TCol>(Func<TRow, TCol> getter, Action<TRow, TCol> setter)
        {
            AddColumn(DefineColumn(getter, setter));
        }

        protected ColumnDef<TRow, TCol> DefineColumn<TCol>(Func<TRow, TCol> getter, Action<TRow, TCol> setter)
        {
            return ColumnDef.Define(getter, setter);
        }

        protected void AddColumn<TCol>(ColumnDef<TRow, TCol> columnDef)
        {
            _columnDefs.Add(columnDef);
        }

        public IEnumerable<ColumnData> ToColumns(ICollection<TRow> rows)
        {
            return _columnDefs.Select(col => col.GetColumn(rows));
        }

        public abstract Transposition<TRow> Transpose(ICollection<TRow> rows);

        protected abstract TRow[] CreateRows(int rowCount);
        public TRow[] ToRows(IEnumerable<ColumnData> columns, int start, int count)
        {
            var rows = CreateRows(count);
            int iColumn = 0;
            foreach (var column in columns)
            {
                _columnDefs[iColumn++].SetValues(rows, column, start);
            }

            return rows;
        }

        public void EfficientlyStore<T>(ValueCache valueCache, IList<T> transpositions) where T : Transposition
        {
            for (int iCol = 0; iCol < _columnDefs.Count; iCol++)
            {
                _columnDefs[iCol].EfficientlyStore(valueCache, transpositions, iCol);
            }
        }

        public int ColumnCount
        {
            get { return _columnDefs.Count; }
        }

        public Type GetColumnValueType(int index)
        {
            return _columnDefs[index].ValueType;
        }

        public bool ColumnEquals(int columnIndex, ColumnData columnData, IEnumerable<TRow> rows)
        {
            return _columnDefs[columnIndex].EqualsColumn(rows, columnData);
        }
    }
}