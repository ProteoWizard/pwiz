using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms.VisualStyles;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Storage
{
    public abstract class Transposition : Immutable
    {
        private IEnumerable[] _columns;
        protected abstract ITransposer Transposer { get; }

        public int RowCount { get; private set; }

        public Array ToRows()
        {
            return ToRows(0, RowCount);
        }

        public Array ToRows(int start, int count)
        {
            return Transposer.ToRows(_columns, start, count);
        }

        public Transposition ChangeColumns(int rowCount, IEnumerable<IEnumerable> columns)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.RowCount = rowCount;
                im._columns = columns.ToArray();
            });
        }

        public Transposition ChangeColumnAt(int columnIndex, IEnumerable column)
        {
            IEnumerable currentColumn = null;
            if (columnIndex < _columns.Length)
            {
                currentColumn = _columns[columnIndex];
            }

            if (ReferenceEquals(currentColumn, column))
            {
                return this;
            }

            IEnumerable[] newColumns = new IEnumerable[Math.Max(_columns.Length, columnIndex + 1)];
            _columns.CopyTo(newColumns, 0);
            newColumns[columnIndex] = column;
            return ChangeProp(ImClone(this), im => im._columns = newColumns);
        }

        public ImmutableList<T> GetColumnValues<T>(int columnIndex)
        {
            IEnumerable<T> column = null;
            if (columnIndex < _columns.Length)
            {
                column = (IEnumerable<T>)_columns[columnIndex];
            }

            if (column == null)
            {
                return null;
            }

            if (column is ImmutableList<T> immutableList)
            {
                return immutableList;
            }

            return ImmutableList.ValueOf(column.Take(RowCount));
        }
    }

    public abstract class Transposition<TRow> : Transposition, IReadOnlyList<TRow>
    {
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<TRow> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(i => this[i]).GetEnumerator();
        }

        public int Count
        {
            get { return RowCount; }
        }

        public TRow this[int index] 
        {
            get
            {
                return ToRows(index, 1)[0];
            }
        }

        public new TRow[] ToRows(int start, int count)
        {
            return (TRow[])base.ToRows(start, count);
        }
    }

    public interface ITransposer
    {
        Array ToRows(IEnumerable<IEnumerable> columns, int start, int count);
    }

    public abstract class ColumnDef
    {
        public abstract void EfficientlyStore<T>(IList<T> transpositions, int columnIndex) where T : Transposition;
    }

    public abstract class ColumnDef<TRow> : ColumnDef
    {
        public abstract Array GetValues(IEnumerable<TRow> rows);
        public abstract void SetValues(IList<TRow> rows, IEnumerable column);
        public abstract void SetValues(IList<TRow> rows, IEnumerable column, int start);
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

        public override void SetValues(IList<TRow> rows, IEnumerable column)
        {
            if (column == null)
            {
                return;
            }
            int iRow = 0;
            foreach (var value in column.Cast<TCol>())
            {
                var row = rows[iRow++];
                _setter(row, value);
            }
        }

        public override void SetValues(IList<TRow> rows, IEnumerable column, int start)
        {
            if (column == null)
            {
                return;
            }

            IReadOnlyList<TCol> columnList = (IReadOnlyList<TCol>)column;
            int end = Math.Min(rows.Count, columnList.Count - start);
            for (int iRow = 0; iRow < end; iRow++)
            {
                var row = rows[iRow];
                _setter(row, columnList[iRow + start]);
            }
        }

        public override void EfficientlyStore<T>(IList<T> transpositions, int columnIndex)
        {
            int iTransposition = 0;
            foreach (var newList in EfficientListStorage<TCol>.StoreLists(transpositions.Select(t=>t.GetColumnValues<TCol>(columnIndex))))
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

        public IEnumerable<Array> ToColumns(ICollection<TRow> rows)
        {
            return _columnDefs.Select(col => col.GetValues(rows));
        }

        protected abstract TRow[] CreateRows(int rowCount);
        Array ITransposer.ToRows(IEnumerable<IEnumerable> columns, int start, int count)
        {
            return ToRows(columns, start, count);
        }

        public TRow[] ToRows(IEnumerable<IEnumerable> columns, int start, int count)
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
