using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Collections.Transpositions
{
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
