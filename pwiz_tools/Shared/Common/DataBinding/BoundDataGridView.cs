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
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Enhancement ot a DataGridView which automatically creates a <see cref="BindingListView" />
    /// to use as its DataSource.
    /// Setting the DataSource of a BoundDataGridView to a BindingSource automatically causes
    /// the BindingSource's DataSource to be set to the BindingListView.
    /// 
    /// </summary>
    public class BoundDataGridView : DataGridView
    {
        private DataGridViewColumn[] _oldColumns = new DataGridViewColumn[0];
        private BindingSource _bindingSource;
        public BoundDataGridView()
        {
            BindingListView = new BindingListView()
                                  {
                                      Owner = this
                                  };
        }

        protected override void OnDataSourceChanged(EventArgs e)
        {
            _bindingSource = DataSource as BindingSource;
            if (_bindingSource != null)
            {
                _bindingSource.DataSource = BindingListView;
            }
            base.OnDataSourceChanged(e);
        }

        public IEnumerable RowSource
        {
            get
            {
                return BindingListView.RowSource;
            }
            set
            {
                BindingListView.RowSource = value;
            }
        }

        public BindingListView BindingListView { get; private set; }

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
            if (!AutoGenerateColumns)
            {
                return;
            }
            var columnArray = new DataGridViewColumn[Columns.Count];
            Columns.CopyTo(columnArray, 0);
            BindingListView.ViewInfo.DataSchema.UpdateGridColumns(BindingListView, columnArray);
            Columns.Clear();
            Columns.AddRange(columnArray);
            _oldColumns = Columns.Cast<DataGridViewColumn>().ToArray();
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
                    linkValue.ClickEventHandler(this, e);
                }
            }
        }

        protected override void OnDataError(bool displayErrorDialogIfNoHandler, DataGridViewDataErrorEventArgs e)
        {
            base.OnDataError(displayErrorDialogIfNoHandler, e);
        }
    }
}
