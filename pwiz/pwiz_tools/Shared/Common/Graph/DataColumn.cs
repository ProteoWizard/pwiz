using System.Collections.Generic;
using pwiz.Common.Collections;

namespace pwiz.Common.Graph
{
    /// <summary>
    /// Implemented by either a single DataColumn, or a DataFrame containing multiple
    /// columns which all have the same number of rows.
    /// </summary>
    public interface IColumnGroup
    {
        int ColumnCount { get; }
        int RowCount { get; }
        /// <summary>
        /// Returns a two-dimensional array with the values that should be displayed 
        /// above the rows of data.  The first dimension length is the number of rows of
        /// headers that there are.  The second dimension length must be equal to
        /// <see cref="ColumnCount"/>.
        /// </summary>
        object[,] GetColumnHeaders();
        /// <summary>
        /// Returns the array of values for a particular row.
        /// The length of the array of values must be equal to <see cref="ColumnCount"/>
        /// </summary>
        object[] GetRow(int rowIndex);
    }

    /// <summary>
    /// Non-generic base class of <see cref="DataColumn{T}"/>.
    /// </summary>
    public abstract class DataColumn : IColumnGroup
    {
        protected DataColumn(string title)
        {
            Title = title;
        }

        public string Title { get; private set; }
        public abstract DataColumn SetTitle(string title);
        
        #region IColumnGroup members
        public int ColumnCount { get { return 1; }}
        public abstract int RowCount { get; }
        public object[,] GetColumnHeaders()
        {
            if (Title == null)
            {
                return new object[0,1];
            }
            return new object[,] {{Title}};
        }
        public abstract object[] GetRow(int rowIndex);
        #endregion
    }

    /// <summary>
    /// A single column of data.
    /// </summary>
    /// <typeparam name="T">The type of the values in this column</typeparam>
    public class DataColumn<T> : DataColumn
    {
        public DataColumn(string title, IEnumerable<T> cells) : base(title)
        {
            Cells = ImmutableList.ValueOf(cells);
        }

        public override DataColumn SetTitle(string title)
        {
            return new DataColumn<T>(title, Cells);
        }

        public override int RowCount
        {
            get { return Cells.Count; }
        }

        public IList<T> Cells { get; private set; }

        public override object[] GetRow(int rowIndex)
        {
            return new object[]{Cells[rowIndex]};
        }

        #region object overrides
		public bool Equals(DataColumn<T> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Cells, Cells) && Equals(other.Title, Title);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (DataColumn<T>)) return false;
            return Equals((DataColumn<T>) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Cells.GetHashCode()*397) ^ (Title != null ? Title.GetHashCode() : 0);
            }
        }
 	    #endregion    
    }
}
