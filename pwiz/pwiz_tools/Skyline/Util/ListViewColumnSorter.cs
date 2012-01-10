/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections;
using System.Windows.Forms;

namespace pwiz.Skyline.Util
{
    public class ListViewColumnSorter : IComparer
    {
        private int _columnToSort;
        private SortOrder _orderOfSort;

        public ListViewColumnSorter()
        {
            // Initialize the column to '0'.
            _columnToSort = 0;

            // Initialize the sort order to 'none'.
            _orderOfSort = SortOrder.None;
        }

        public int Compare( object x, object y )
        {
            int compareResult;
            ListViewItem listviewX = x as ListViewItem;
            ListViewItem listviewY = y as ListViewItem;
            if (listviewX == null || listviewY == null)
                return 0;

            string xText = listviewX.SubItems[_columnToSort].Text;
            string yText = listviewY.SubItems[_columnToSort].Text;

            // Try to compare the tags
            if( listviewX.SubItems[_columnToSort].Tag is IComparable &&
                listviewY.SubItems[_columnToSort].Tag is IComparable )
            {
                IComparable xd = (IComparable) listviewX.SubItems[_columnToSort].Tag;
                IComparable yd = (IComparable) listviewY.SubItems[_columnToSort].Tag;
                compareResult = xd.CompareTo( yd );
            }
            else
            {
                // Try to compare the items as numeric
                decimal xd, yd;
                if(Decimal.TryParse(xText, out xd) && Decimal.TryParse(yText, out yd))
                {
                    compareResult = xd.CompareTo(yd);
                }
                else
                {
                    // compare the items as strings
// ReSharper disable StringCompareToIsCultureSpecific
                    compareResult = xText.CompareTo(yText);
// ReSharper restore StringCompareToIsCultureSpecific
                }
            }

            // Calculate the correct return value based on the object comparison.
            switch (_orderOfSort)
            {
                case SortOrder.Ascending:
                    return compareResult;
                case SortOrder.Descending:
                    return -compareResult;
                default:
                    return 0;
            }
        }

        public int SortColumn
        {
            set { _columnToSort = value; }
            get { return _columnToSort; }
        }

        public SortOrder Order
        {
            set { _orderOfSort = value; }
            get { return _orderOfSort; }
        }
    }
}
