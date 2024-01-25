using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Collections.Transpositions
{
    public abstract class Transposer<TRow>
    {
        private List<ColumnDef<TRow>> _columnDefs = new List<ColumnDef<TRow>>();

        protected void DefineColumn<TCol>(Func<TRow, TCol> getter, Action<TRow, TCol> setter)
        {
            AddColumn(ColumnDef.Define(getter, setter));
        }

        protected void AddColumn<TCol>(ColumnDef<TRow, TCol> columnDef)
        {
            _columnDefs.Add(columnDef);
        }

        public IEnumerable<ColumnData> ToColumns(ICollection<TRow> rows)
        {
            return _columnDefs.Select(col => col.GetValues(rows));
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
    }
}