using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using pwiz.Common.Collections;

namespace pwiz.Common.Graph
{
    /// <summary>
    /// Holds a collection of <see cref="IColumnGroup"/> objects in the <see cref="ColumnGroups"/> that all
    /// have the same number of rows in them.
    /// In addition, the <see cref="RowHeader"/> property may null, or may contain a Column Group which
    /// is to be displayed before the other Column Groups.
    /// If two DataFrame objects have identical RowHeaders, they may be merged.
    /// </summary>
    public class DataFrame : IColumnGroup
    {
        /// <summary>
        /// Constructs a DataFrame with zero columns and the specified number of rows.
        /// </summary>
        public DataFrame(string title, int rowCount)
        {
            Title = title;
            RowCount = rowCount;
            ColumnGroups = new IColumnGroup[0];
        }
        public DataFrame(string title, IColumnGroup rowHeaders) : this(title, rowHeaders.RowCount)
        {
            RowHeader = rowHeaders;
        }
        public DataFrame AddColumns(IEnumerable<IColumnGroup> columnsToAdd)
        {
            var newColumns = new List<IColumnGroup>(ColumnGroups);
            foreach (var newColumn in columnsToAdd)
            {
                if (null == newColumn)
                {
                    continue;
                }
                VerifyRowCount(newColumn);
                newColumns.Add(newColumn);
            }
            return new DataFrame(Title, RowCount) { RowHeader = RowHeader, ColumnGroups = ImmutableList.ValueOf(newColumns)};
        }
        public DataFrame AddColumn(IColumnGroup column)
        {
            return AddColumns(new[] {column});
        }
        public DataFrame SetRowHeaders(IColumnGroup rowHeaders)
        {
            if (null == rowHeaders)
            {
                return (DataFrame) RemoveRowHeader();
            }
            VerifyRowCount(rowHeaders);
            return new DataFrame(Title, RowCount)
                       {
                           RowHeader = rowHeaders, 
                           ColumnGroups = ColumnGroups
                       };
        }
        /// <summary>
        /// Returns a two-dimensional array with the values that are to be displayed above the rows
        /// of data.  If the <see cref="Title"/> of this DataFrame is null, then the height of the
        /// returned array will be the height of the tallest ColumnHeaders of any of the <see cref="IColumnGroup"/>
        /// in this DataFrame.
        /// If the Title of this DataFrame is not null, then the ColumnHeaders will have an additional
        /// row at the beginning for this DataFrame's Title.
        /// </summary>
        public object[,] GetColumnHeaders()
        {
            var columnCount = ColumnCount;
            if (columnCount == 0)
            {
                return new object[0,0];
            }
            IList<object[,]> childColumnHeaders = AllColumnGroups.Select(columnSet => columnSet.GetColumnHeaders()).ToArray();
            Debug.Assert(columnCount == childColumnHeaders.Select(header=>header.GetLength(1)).Sum());
            int childHeaderHeight = childColumnHeaders.Select(header => header.GetLength(0)).Max();
            object[,] result;
            if (Title == null)
            {
                result = new object[childHeaderHeight,ColumnCount];
            }
            else
            {
                result = new object[childHeaderHeight + 1,ColumnCount];
                result[0, 0] = Title;
            }
            int columnIndex = 0;
            foreach (var childColumnHeader in childColumnHeaders)
            {
                for (int rowIndex = 0; rowIndex < childColumnHeader.GetLength(0); rowIndex++)
                {
                    for (int iCol = 0; iCol < childColumnHeader.GetLength(1); iCol++)
                    {
                        result[result.GetLength(0) - childColumnHeader.GetLength(0) + rowIndex, iCol + columnIndex] =
                            childColumnHeader[rowIndex, iCol];
                    }
                }
                columnIndex += childColumnHeader.GetLength(1);
            }
            return result;
        }

        public IColumnGroup RowHeader { get; private set; }
        public IList<IColumnGroup> ColumnGroups { get; private set; }
        public IEnumerable<IColumnGroup> AllColumnGroups
        {
            get
            {
                if (RowHeader == null)
                {
                    return ColumnGroups;
                }
                return new[] {RowHeader}.Concat(ColumnGroups);
            }
        }

        public IColumnGroup RemoveRowHeader()
        {
            if (RowHeader == null)
            {
                return this;
            }
            return new DataFrame(Title, RowCount) { ColumnGroups = ColumnGroups };
        }

        public object[] GetRow(int rowIndex)
        {
            return AllColumnGroups.SelectMany(columnSet => columnSet.GetRow(rowIndex)).ToArray();
        }

        public string Title { get; private set; }
        public int ColumnCount { get { return AllColumnGroups.Select(columnSet=>columnSet.ColumnCount).Sum(); } }
        public int RowCount { get; private set; }

        private void VerifyRowCount(IColumnGroup columnGroup)
        {
            if (RowCount != columnGroup.RowCount)
            {
                throw new ArgumentException(string.Format("Expected row count:{0} actual:{1}", RowCount, columnGroup.RowCount)); // Not L10N
            }
        }

        public void Write(TextWriter writer, string separator)
        {
            var columnHeaders = GetColumnHeaders();
            for (int iRow = 0; iRow < columnHeaders.GetLength(0); iRow++)
            {
                object[] row = null;
                for (int iCol = columnHeaders.GetLength(1) - 1; iCol >= 0; iCol--)
                {
                    var value = columnHeaders[iRow, iCol];
                    if (value == null)
                    {
                        continue;
                    }
                    row = row ?? new object[iCol + 1];
                    row[iCol] = value;
                }
                WriteLine(writer, separator, row);
            }
            for (int iRow = 0; iRow < RowCount; iRow++)
            {
                WriteLine(writer, separator, GetRow(iRow));
            }
        }

        private static void WriteLine(TextWriter writer, string separator, IEnumerable values)
        {
            bool first = true;
            foreach (var value in values)
            {
                if (!first)
                {
                    writer.Write(separator);
                }
                first = false;
                if (value != null)
                {
                    writer.Write(value);
                }
            }
            writer.WriteLine();
        }

        #region object overrides
        public bool Equals(DataFrame other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.ColumnGroups, ColumnGroups) && other.RowCount == RowCount && Equals(other.RowHeader, RowHeader) && Equals(other.Title, Title);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (DataFrame)) return false;
            return Equals((DataFrame) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = ColumnGroups.GetHashCode();
                result = (result*397) ^ RowCount;
                result = (result*397) ^ (RowHeader != null ? RowHeader.GetHashCode() : 0);
                result = (result*397) ^ (Title != null ? Title.GetHashCode() : 0);
                return result;
            }
        }
        #endregion
    }
}
