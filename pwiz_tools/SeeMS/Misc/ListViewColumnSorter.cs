//
// $Id$
//

using System;
using System.Collections;
using System.Windows.Forms;

namespace seems
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

        public int Compare( object x, object y )
        {
            int compareResult;
            ListViewItem listviewX = x as ListViewItem;
            ListViewItem listviewY = y as ListViewItem;

            string xText = listviewX.SubItems[ColumnToSort].Text;
            string yText = listviewY.SubItems[ColumnToSort].Text;

            // Try to compare the tags
            if( listviewX.SubItems[ColumnToSort].Tag is IComparable &&
                listviewY.SubItems[ColumnToSort].Tag is IComparable )
            {
                IComparable xd = (IComparable) listviewX.SubItems[ColumnToSort].Tag;
                IComparable yd = (IComparable) listviewY.SubItems[ColumnToSort].Tag;
                compareResult = xd.CompareTo( yd );
            } else
            {
                // Try to compare the items as numeric
                decimal xd, yd;
                if( Decimal.TryParse( xText, out xd ) && Decimal.TryParse( yText, out yd ) )
                {
                    compareResult = xd.CompareTo( yd );
                } else
                {
                    // compare the items as strings
                    compareResult = xText.CompareTo( yText );
                }
            }

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
    }
}
