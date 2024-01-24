using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Storage
{
    public abstract class Transposition : Immutable
    {
        private ImmutableList<ColumnData> _columns;
        protected abstract ITransposer Transposer { get; }
        
        public Array ToRows(int start, int count)
        {
            return Transposer.ToRows(_columns, start, count);
        }

        public Transposition ChangeColumns(IEnumerable<ColumnData> columns)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im._columns = ImmutableList.ValueOf(columns);
            });
        }

        public Transposition ChangeColumns(IEnumerable<Array> columns)
        {
            return ChangeColumns(columns.Select(array => new ColumnData(array)));
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
            return ChangeProp(ImClone(this), im => im._columns = ImmutableList.ValueOf(newColumns));
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
        public Transposer<TRow> GetTransposer()
        {
            return (Transposer<TRow>) Transposer;
        }

        public new TRow[] ToRows(int start, int count)
        {
            return (TRow[])base.ToRows(start, count);
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

    public interface ITransposer
    {
        Array ToRows(IEnumerable<ColumnData> columns, int start, int count);
    }

    public abstract class ColumnDef
    {
        public abstract void EfficientlyStore<T>(IList<T> transpositions, int columnIndex) where T : Transposition;
    }

    public abstract class ColumnDef<TRow> : ColumnDef
    {
        public abstract Array GetValues(IEnumerable<TRow> rows);
        public abstract void SetValues(IEnumerable<TRow> rows, ColumnData column, int start);
    }

    public sealed class ColumnDef<TRow, TCol> : ColumnDef<TRow>
    {
        private Func<TRow, TCol> _getter;
        private Action<TRow, TCol> _setter;
        public ColumnDef(Func<TRow, TCol> getter, Action<TRow, TCol> setter)
        {
            _getter = getter;
            _setter = setter;
        }

        public Type ValueType
        {
            get { return typeof(TCol); }
        }

        public override Array GetValues(IEnumerable<TRow> rows)
        {
            return rows.Select(_getter).ToArray();
        }

        public override void SetValues(IEnumerable<TRow> rows, ColumnData column, int start)
        {
            int iRow = 0;
            foreach (var row in rows)
            {
                _setter(row, column.GetValueAt<TCol>(iRow + start));
                iRow++;
            }
        }

        public override void EfficientlyStore<T>(IList<T> transpositions, int columnIndex)
        {
            int iTransposition = 0;
            foreach (var newList in EfficientListStorage<TCol>.StoreLists(transpositions.Select(t=>t.GetColumnValues(columnIndex))))
            {
                var transposition = transpositions[iTransposition];
                transposition = (T) transposition.ChangeColumnAt(columnIndex, newList);
                transpositions[iTransposition] = transposition;
                iTransposition++;
            }
        }
    }


    public abstract class Transposer<TRow> : ITransposer
    {
        private List<ColumnDef<TRow>> _columnDefs = new List<ColumnDef<TRow>>();

        protected void DefineColumn<TCol>(Func<TRow, TCol> getter, Action<TRow, TCol> setter)
        {
            _columnDefs.Add(new ColumnDef<TRow,TCol>(getter, setter));
        }

        public IEnumerable<ColumnData> ToColumns(ICollection<TRow> rows)
        {
            return _columnDefs.Select(col => new ColumnData(col.GetValues(rows)));
        }

        public abstract Transposition<TRow> Transpose(ICollection<TRow> rows);

        protected abstract TRow[] CreateRows(int rowCount);
        Array ITransposer.ToRows(IEnumerable<ColumnData> columns, int start, int count)
        {
            return ToRows(columns, start, count);
        }

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

        public void EfficientlyStore<T>(IList<T> transpositions) where T : Transposition
        {
            for (int iCol = 0; iCol < _columnDefs.Count; iCol++)
            {
                _columnDefs[iCol].EfficientlyStore(transpositions, iCol);
            }
        }
    }
}
