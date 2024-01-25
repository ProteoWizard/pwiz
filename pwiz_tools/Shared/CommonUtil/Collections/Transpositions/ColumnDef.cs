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

            foreach (var newList in optimizer.StoreLists(transpositions.Select(t => t.GetColumnValues(columnIndex))))
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