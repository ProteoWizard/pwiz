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
        public abstract void EfficientlyStore<T>(ValueCache valueCache, IList<T> transpositions, int columnIndex) where T : Transposition;
        public static ColumnDef<TRow, TCol> Define<TCol, TRow>(Func<TRow, TCol> getter, Action<TRow, TCol> setter)
        {
            return ColumnDef<TRow, TCol>.Define(getter, setter);
        }
    }

    public abstract class ColumnDef<TRow> : ColumnDef
    {
        public abstract ColumnData GetValues(IEnumerable<TRow> rows);
        public abstract void SetValues(IEnumerable<TRow> rows, ColumnData column, int start);
    }

    public abstract class ColumnDef<TRow, TCol> : ColumnDef<TRow>
    {
        public static ColumnDef<TRow, TCol> Define(Func<TRow, TCol> getter, Action<TRow, TCol> setter)
        {
            return new Impl(getter, setter);
        }
        public Type ValueType
        {
            get { return typeof(TCol); }
        }

        public override ColumnData GetValues(IEnumerable<TRow> rows)
        {
            var values = rows.Select(GetValue);
            return ColumnData.Immutable(values);
        }

        public override void SetValues(IEnumerable<TRow> rows, ColumnData column, int start)
        {
            int iRow = 0;
            foreach (var row in rows)
            {
                SetValue(row, column.GetValueAt<TCol>(iRow + start));
                iRow++;
            }
        }

        protected abstract TCol GetValue(TRow row);
        protected abstract void SetValue(TRow row, TCol value);

        public override void EfficientlyStore<T>(ValueCache valueCache, IList<T> transpositions, int columnIndex)
        {
            Optimize(GetColumnDataOptimizer(valueCache), transpositions, columnIndex);
        }

        protected void Optimize<T>(ColumnDataOptimizer<TCol> optimizer, IList<T> transpositions, int columnIndex) where T : Transposition
        {
            int iTransposition = 0;

            foreach (var newList in optimizer.OptimizeMemoryUsage(transpositions.Select(t => t.GetColumnValues(columnIndex))))
            {
                var transposition = transpositions[iTransposition];
                transposition = (T)transposition.ChangeColumnAt(columnIndex, newList);
                transpositions[iTransposition] = transposition;
                iTransposition++;
            }
        }
        
        public virtual ColumnDataOptimizer<TCol> GetColumnDataOptimizer(ValueCache valueCache)
        {
            return new ColumnDataOptimizer<TCol>();
        }

        /// <summary>
        /// Simple implementation of a ColumnDef with a supplied getter and setter.
        /// </summary>
        protected class Impl : ColumnDef<TRow, TCol>
        {
            private Func<TRow, TCol> _getter;
            private Action<TRow, TCol> _setter;

            public Impl(Func<TRow, TCol> getter, Action<TRow, TCol> setter)
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
}