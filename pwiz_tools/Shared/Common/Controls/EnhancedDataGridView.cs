/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace pwiz.Common.Controls
{
    /// <summary>
    /// Enhancement of the DataGridView class which has better performance when sorting
    /// large numbers of rows (especially when the column has many duplicate values)
    /// </summary>
    public class EnhancedDataGridView : DataGridView
    {
        /// <summary>
        /// Override the comparison function to be able to handle larger number of rows (>100,000).
        /// 
        /// I believe (nicksh) that the comparison function that the grid normally uses is inconsistent
        /// when there are duplicate values.  The function tries to use "rowIndex" as the tiebreaker
        /// if two keys are equal.  However, the rowIndexes of the rows end up changing during the
        /// course of the rows being sorted.  This can lead to infinite loops.
        /// 
        /// </summary>
        /// <param name="dataGridViewColumn"></param>
        /// <param name="direction"></param>
        public override void Sort(DataGridViewColumn dataGridViewColumn, ListSortDirection direction)
        {
            if (!UseStableSort)
            {
                base.Sort(dataGridViewColumn, direction);
            }
            var rows = new DataGridViewRow[Rows.Count];
            Rows.CopyTo(rows, 0);
            DataGridViewCell currentCell = CurrentCell;
            Array.Sort(rows, (r1,r2)=>
                                 {
                                     var result = CompareValues(r1.Cells[dataGridViewColumn.Index].Value,
                                                                r2.Cells[dataGridViewColumn.Index].Value);
                                     if (direction == ListSortDirection.Descending)
                                     {
                                         result = -result;
                                     }
                                     if (result == 0)
                                     {
                                         result = r1.Index.CompareTo(r2.Index);
                                     }
                                     return result;
                                 });
            Rows.Clear();
            Rows.AddRange(rows);
            CurrentCell = currentCell;
        }

        protected int CompareValues(object value1, object value2)
        {
            if (!(value1 is IComparable) && !(value2 is IComparable))
            {
                if (value1 == null)
                {
                    if (value2 == null)
                    {
                        return 0;
                    }
                    return 1;
                }
                if (value2 == null)
                {
                    return -1;
                }
                return Comparer.Default.Compare(value1.ToString(), value2.ToString());
            }
            return Comparer.Default.Compare(value1, value2);
        }

        public bool UseStableSort { get; set; }
    }
}
