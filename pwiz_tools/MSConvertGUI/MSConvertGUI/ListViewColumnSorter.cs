//
// $Id$
//

using System;
using System.Collections;
using System.Windows.Forms;

namespace MSConvertGUI
{
    public class ListViewColumnSorter : System.Collections.IComparer
    {
        private int ColumnToSort;
        private SortOrder OrderOfSort;
        private CaseInsensitiveComparer ObjectCompare;

        public ListViewColumnSorter()
        {
            // Initialize the column to '0'.
            ColumnToSort = 0;

            // Initialize the sort order to 'none'.
            OrderOfSort = SortOrder.None;

            // Initialize the CaseInsensitiveComparer object.
            ObjectCompare = new CaseInsensitiveComparer();
        }

        public virtual int Compare( object x, object y )
        {
            ListViewItem listviewX = x as ListViewItem;
            ListViewItem listviewY = y as ListViewItem;
            int compareResult = CompareSubItems(listviewX, listviewY, ColumnToSort);

            // Calculate the correct return value based on the object comparison.
            if( OrderOfSort == SortOrder.Ascending )
                // Ascending sort is selected, return typical result of compare operation.
                return compareResult;
            else if( OrderOfSort == SortOrder.Descending )
                // Descending sort is selected, return negative result of compare operation.
                return -compareResult;
            else
                // Return '0' to indicate that they are equal.
                return 0;
        }

        public int SortColumn
        {
            set { ColumnToSort = value; }
            get { return ColumnToSort; }
        }

        public SortOrder Order
        {
            set { OrderOfSort = value; }
            get { return OrderOfSort; }
        }
        
        protected int CompareSubItems(ListViewItem x, ListViewItem y, string key)
        {
            return CompareSubItems(x, y, x.ListView.Columns[key].Index);
        }

        protected int CompareSubItems(ListViewItem x, ListViewItem y, int columnToSort)
        {
            string xText = x.SubItems[columnToSort].Text;
            string yText = y.SubItems[columnToSort].Text;

            // Try to compare the tags
            if (x.SubItems[columnToSort].Tag is IComparable &&
                y.SubItems[columnToSort].Tag is IComparable)
            {
                IComparable xd = (IComparable)x.SubItems[columnToSort].Tag;
                IComparable yd = (IComparable)y.SubItems[columnToSort].Tag;
                return xd.CompareTo(yd);
            }
            else
            {
                // Try to compare the items as numeric
                decimal xd, yd;
                if (Decimal.TryParse(xText, out xd) && Decimal.TryParse(yText, out yd))
                {
                    return xd.CompareTo(yd);
                }
                else
                {
                    // compare the items as strings
                    return xText.CompareTo(yText);
                }
            }
        }
    }
}
