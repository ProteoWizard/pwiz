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
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Enhancement ot a DataGridView which works well with a <see cref="BindingListView" />.
    /// Automatically handles columns of type <see cref="LinkValue{T}" />.
    /// 
    /// </summary>
    public class BoundDataGridView : DataGridView
    {
        private DataGridViewColumn[] _oldColumns = new DataGridViewColumn[0];
        protected override void OnDataBindingComplete(DataGridViewBindingCompleteEventArgs e)
        {
            AutoGenerateColumns = true;
            base.OnDataBindingComplete(e);
            if (DesignMode)
            {
                return;
            }
            if (_oldColumns.SequenceEqual(Columns.Cast<DataGridViewColumn>()))
            {
                return;
            }
            var bindingListView = GetBindingListView();
            if (bindingListView == null)
            {
                return;
            }
            if (!AutoGenerateColumns)
            {
                return;
            }
            var columnArray = new DataGridViewColumn[Columns.Count];
            Columns.CopyTo(columnArray, 0);
            if (bindingListView.DataSchema.UpdateGridColumns(bindingListView, columnArray))
            {
                Columns.Clear();
                Columns.AddRange(columnArray);
            }
            _oldColumns = Columns.Cast<DataGridViewColumn>().ToArray();
        }

        public BindingListView GetBindingListView()
        {
            var bindingSource = DataSource as BindingSource;
            if (bindingSource == null)
            {
                return null;
            }
            return bindingSource.DataSource as BindingListView;
        }

        protected override void OnCellContentClick(DataGridViewCellEventArgs e)
        {
            base.OnCellContentClick(e);
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0)
            {
                var value = Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                var linkValue = value as ILinkValue;
                if (linkValue != null)
                {
                    linkValue.ClickEventHandler.Invoke(this, e);
                }
            }
        }

        protected override void OnColumnDisplayIndexChanged(DataGridViewColumnEventArgs e)
        {
            base.OnColumnDisplayIndexChanged(e);
            var bindingListView = GetBindingListView();
            if (bindingListView == null)
            {
                return;
            }
            var columns = Columns.Cast<DataGridViewColumn>().ToArray();
            Array.Sort(columns, (c1,c2)=>c1.DisplayIndex.CompareTo(c2.DisplayIndex));
            bindingListView.SetColumnDisplayOrder(columns.Select(column => column.DataPropertyName));
        }

        protected override void OnDataError(bool displayErrorDialogIfNoHandler, DataGridViewDataErrorEventArgs e)
        {
            base.OnDataError(displayErrorDialogIfNoHandler, e);
        }
    }
}
