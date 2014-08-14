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

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
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
        private IViewContext _viewContext;
        private IList<PropertyDescriptor> _itemProperties;

        public BoundDataGridView()
        {
            AutoGenerateColumns = false;
        }

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
            UpdateColumns();
            base.OnDataBindingComplete(e);
        }

        protected virtual void UpdateColumns()
        {
            var bindingListSource = DataSource as BindingListSource;
            if (DesignMode)
            {
                return;
            }
            if (null == bindingListSource || null == _viewContext)
            {
                return;
            }
            var newItemProperties = ImmutableList.ValueOf(bindingListSource.GetItemProperties(null).Cast<PropertyDescriptor>());
            if (Equals(newItemProperties, _itemProperties))
            {
                return;
            }
            var newColumns = new List<DataGridViewColumn>();
            foreach (var propertyDescriptor in newItemProperties)
            {
                var column = _viewContext.CreateGridViewColumn(propertyDescriptor);
                if (null != column)
                {
                    newColumns.Add(column);
                }
            }
            if (newColumns.Count > 0)
            {
                Columns.Clear();
                Columns.AddRange(newColumns.ToArray());
            }
            _itemProperties = newItemProperties;
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
