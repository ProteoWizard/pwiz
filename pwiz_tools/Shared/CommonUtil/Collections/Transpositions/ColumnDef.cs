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
    public abstract class ColumnDef : Immutable
    {
        public abstract void EfficientlyStore<T>(ValueCache valueCache, IList<T> transpositions) where T : Transposition;
        public static ColumnDef<TRow, TCol> Define<TCol, TRow>(Func<TRow, TCol> getter, Action<TRow, TCol> setter, int columnIndex)
        {
            return ColumnDef<TRow, TCol>.Define(getter, setter, columnIndex);
        }
    }

    public abstract class ColumnDef<TRow> : ColumnDef
    {
        public abstract ColumnData GetColumn(IEnumerable<TRow> rows);
        public abstract void SetValues(IEnumerable<TRow> rows, ColumnData column, int start);
        public abstract Type ValueType { get; }
        public abstract bool EqualsColumn(IEnumerable<TRow> rows, ColumnData column);
    }

    public abstract class ColumnDef<TRow, TCol> : ColumnDef<TRow>
    {
        public static ColumnDef<TRow, TCol> Define(Func<TRow, TCol> getter, Action<TRow, TCol> setter, int columnIndex)
        {
            return new Impl(getter, setter, columnIndex);
        }

        protected ColumnDef(int columnIndex)
        {
            ColumnIndex = columnIndex;
        }

        public int ColumnIndex { get; }

        public override Type ValueType
        {
            get { return typeof(TCol); }
        }

        public override ColumnData GetColumn(IEnumerable<TRow> rows)
        {
            return ColumnData.ForValues(GetValues(rows));
        }

        public IEnumerable<TCol> GetValues(IEnumerable<TRow> rows)
        {
            return rows.Select(GetValue);
        }

        public override void SetValues(IEnumerable<TRow> rows, ColumnData columnData, int start)
        {
            if (columnData != null)
            {
                SetValues(rows, (ColumnData<TCol>) columnData, start);
            }

        }

        public virtual void SetValues(IEnumerable<TRow> rows, ColumnData<TCol> column, int start)
        {
            int iRow = 0;
            foreach (var row in rows)
            {
                SetValue(row, column.GetValue(iRow + start));
                iRow++;
            }
        }

        protected abstract TCol GetValue(TRow row);
        protected abstract void SetValue(TRow row, TCol value);
        public ColumnOptimizeOptions OptimizeOptions { get; private set; } = ColumnOptimizeOptions.Default;

        public ColumnDef<TRow,TCol> ChangeOptimizeOptions(ColumnOptimizeOptions value)
        {
            return ChangeProp(ImClone(this), im => im.OptimizeOptions = value);
        }

       
        public override void EfficientlyStore<T>(ValueCache valueCache, IList<T> transpositions)
        {
            Optimize(GetColumnDataOptimizer(valueCache), transpositions);
        }

        protected void Optimize<T>(ColumnDataOptimizer<TCol> optimizer, IList<T> transpositions) where T : Transposition
        {
            int iTransposition = 0;

            foreach (var newList in optimizer.OptimizeMemoryUsage(transpositions.Select(t => (ColumnData<TCol>)t.GetColumnData(ColumnIndex))))
            {
                var transposition = transpositions[iTransposition];
#if DEBUG
                var oldColumn = (ColumnData<TCol>) transposition.ColumnDatas.ElementAtOrDefault(ColumnIndex);
                if (!Equals(oldColumn, newList))
                {
                    throw new InvalidOperationException();
                }
#endif
                transposition = (T)transposition.ChangeColumnAt(ColumnIndex, newList);
                transpositions[iTransposition] = transposition;
                iTransposition++;
            }
        }
        
        public virtual ColumnDataOptimizer<TCol> GetColumnDataOptimizer(ValueCache valueCache)
        {
            return new ColumnDataOptimizer<TCol>(valueCache, OptimizeOptions);
        }

        public override bool EqualsColumn(IEnumerable<TRow> rows, ColumnData column)
        {
            var columnData = (ColumnData<TCol>)column;
            int iRow = 0;
            foreach (var rowValue in GetValues(rows))
            {
                var columnValue = columnData == null ? default : columnData.GetValue(iRow++);
                if (!Equals(rowValue, columnValue))
                {
                    return false;
                }
            }

            return true;
        }

        public IEnumerable<TCol> GetValuesFromColumn(ColumnData c, int start, int count)
        {
            var columnData = (ColumnData<TCol>) c;
            if (columnData == null)
            {
                return Enumerable.Repeat(default(TCol), count);
            }

            return Enumerable.Range(start, count).Select(i => columnData.GetValue(i));
        }

        /// <summary>
        /// Simple implementation of a ColumnDef with a supplied getter and setter.
        /// </summary>
        protected class Impl : ColumnDef<TRow, TCol>
        {
            private Func<TRow, TCol> _getter;
            private Action<TRow, TCol> _setter;

            public Impl(Func<TRow, TCol> getter, Action<TRow, TCol> setter, int columnIndex) : base(columnIndex)
            {
                _getter = getter;
                _setter = setter;
            }

            protected override TCol GetValue(TRow row)
            {
                return _getter(row);
            }

            protected override void SetValue(TRow row, TCol value)
            {
                _setter(row, value);
            }
        }
    }

    public static class ColumnReader
    {
        public static ColumnReader<TRow, TCol> Custom<TRow, TCol>(Func<IEnumerable<TRow>, IEnumerable<TCol>> rowGetter,
            Func<Transposition<TRow>, int, int, IEnumerable<TCol>> columnGetter)
        {
            return ColumnReader<TRow, TCol>.Custom(rowGetter, columnGetter);
        }

        public static ColumnReader<TRow, TCol> Simple<TRow, TCol>(ColumnDef<TRow, TCol> columnDef)
        {
            return ColumnReader<TRow, TCol>.Simple(columnDef);
        }
    }
    
    public abstract class ColumnReader<TRow, TCol>
    {
        public static ColumnReader<TRow, TCol> Simple(ColumnDef<TRow, TCol> columnDef)
        {
            return new SimpleImpl(columnDef);
        }

        public static ColumnReader<TRow, TCol> Custom(Func<IEnumerable<TRow>, IEnumerable<TCol>> rowGetter,
            Func<Transposition<TRow>, int, int, IEnumerable<TCol>> columnGetter)
        {
            return new CustomImpl(rowGetter, columnGetter);
        }

        public abstract IEnumerable<TCol> FromRows(IEnumerable<TRow> row);

        public abstract IEnumerable<TCol> FromTransposition(Transposition<TRow> transposition, int start, int count);

        private class SimpleImpl : ColumnReader<TRow, TCol>
        {
            private ColumnDef<TRow, TCol> _columnDef;
            public SimpleImpl(ColumnDef<TRow, TCol> columnDef)
            {
                _columnDef = columnDef;
            }

            public override IEnumerable<TCol> FromTransposition(Transposition<TRow> transposition, int start, int count)
            {
                return _columnDef.GetValuesFromColumn(transposition.GetColumnData(_columnDef.ColumnIndex), start,
                    count);
            }

            public override IEnumerable<TCol> FromRows(IEnumerable<TRow> rows)
            {
                return _columnDef.GetValues(rows);
            }
        }

        private class CustomImpl : ColumnReader<TRow, TCol>
        {
            private Func<IEnumerable<TRow>, IEnumerable<TCol>> _rowGetter;
            private Func<Transposition<TRow>, int, int, IEnumerable<TCol>> _columnGetter;

            public CustomImpl(Func<IEnumerable<TRow>, IEnumerable<TCol>> rowGetter,
                Func<Transposition<TRow>, int, int, IEnumerable<TCol>> columnGetter)
            {
                _rowGetter = rowGetter;
                _columnGetter = columnGetter;
            }

            public override IEnumerable<TCol> FromRows(IEnumerable<TRow> rows)
            {
                return _rowGetter(rows);
            }
            public override IEnumerable<TCol> FromTransposition(Transposition<TRow> transposition, int start, int count)
            {
                return _columnGetter(transposition, start, count);
            }

        }
    }
}