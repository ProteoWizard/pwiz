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

using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding.Internal;

namespace pwiz.Common.DataBinding.Controls
{
    /// <summary>
    /// Enhancement ot a DataGridView which automatically creates a 
    /// <see cref="BindingListView" /> to use as its DataSource.
    /// Setting the DataSource of a BoundDataGridView to a BindingSource automatically causes
    /// the BindingSource's DataSource to be set to the BindingListView.
    /// 
    /// </summary>
    public class BoundDataGridView : DataGridView
    {
        private DataGridViewColumn[] _oldColumns = new DataGridViewColumn[0];
        private IViewContext _viewContext;

        protected override void OnDataBindingComplete(DataGridViewBindingCompleteEventArgs e)
        {
            var bindingListSource = DataSource as BindingListSource;
            var newViewContext = bindingListSource == null ? null : bindingListSource.ViewContext;
            if (!ReferenceEquals(_viewContext, newViewContext))
            {
                if (_viewContext != null)
                {
                    DataError -= _viewContext.OnDataError;
                }
                _viewContext = newViewContext;
                if (_viewContext != null)
                {
                    DataError += _viewContext.OnDataError;
                }
            }
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
            if (bindingListSource == null)
            {
                return;
            }
            var columnArray = new DataGridViewColumn[Columns.Count];
            Columns.CopyTo(columnArray, 0);
            bindingListSource.BindingListView.ViewInfo.DataSchema.UpdateGridColumns(bindingListSource, columnArray);
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
    }
}
