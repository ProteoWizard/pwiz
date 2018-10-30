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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Controls;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding.Internal;
using pwiz.Common.DataBinding.Layout;

namespace pwiz.Common.DataBinding.Controls
{
    /// <summary>
    /// Enhancement ot a DataGridView which automatically creates a 
    /// <see cref="BindingListView" /> to use as its DataSource.
    /// Setting the DataSource of a BoundDataGridView to a BindingSource automatically causes
    /// the BindingSource's DataSource to be set to the BindingListView.
    /// 
    /// </summary>
    public class BoundDataGridView : CommonDataGridView
    {
        private BindingListSource _bindingListSource;
        private IViewContext _viewContext;
        private ImmutableList<DataPropertyDescriptor> _itemProperties;
        private ImmutableList<ColumnFormat> _columnFormats;

        public BoundDataGridView()
        {
            AutoGenerateColumns = false;
            MaximumColumnCount = 2000;
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
            if (!ReferenceEquals(_bindingListSource, bindingListSource))
            {
                if (_bindingListSource != null)
                {
                    _bindingListSource.AllRowsChanged -= BindingListSourceOnAllRowsChanged;
                    _bindingListSource.ColumnFormats.FormatsChanged -= OnFormatsChanged;
                }
                _bindingListSource = bindingListSource;
                if (_bindingListSource != null)
                {
                    _bindingListSource.AllRowsChanged += BindingListSourceOnAllRowsChanged;
                    _bindingListSource.ColumnFormats.FormatsChanged += OnFormatsChanged;
                }
            }
            UpdateColumns();
            base.OnDataBindingComplete(e);
        }

        private void BindingListSourceOnAllRowsChanged(object sender, EventArgs eventArgs)
        {
            Invalidate();
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
            var newItemProperties = bindingListSource.ItemProperties;
            if (!Equals(newItemProperties, _itemProperties))
            {
                var newColumns = new List<DataGridViewColumn>();
                for (int i = 0; i < newItemProperties.Count; i++)
                {
                    var propertyDescriptor = newItemProperties[i];
                    var column = _viewContext.CreateGridViewColumn(propertyDescriptor);
                    if (null != column)
                    {
                        newColumns.Add(column);
                    }
                }
				if (newColumns.Count > 0)
				{
					Columns.Clear();
					AddColumns(newColumns.ToArray());
				}
				_itemProperties = newItemProperties;
            }
            UpdateColumnFormats(false);
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

        protected virtual DataPropertyDescriptor GetPropertyDescriptor(DataGridViewColumn column)
        {
            var propertyName = column.DataPropertyName;
            if (string.IsNullOrEmpty(propertyName))
            {
                return null;
            }
            return _itemProperties.FirstOrDefault(pd => pd.Name == column.DataPropertyName);
        }

        protected override void OnColumnDividerDoubleClick(DataGridViewColumnDividerDoubleClickEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.ColumnIndex < Columns.Count)
            {
                var propertyDescriptor = GetPropertyDescriptor(Columns[e.ColumnIndex]);
                if (propertyDescriptor != null && propertyDescriptor.Attributes[typeof(ExpensiveAttribute)] != null)
                {
                    // If the property is expensive to calculate, then prevent double-clicking on 
                    // column header to resize.
                    e.Handled = true;
                    return;
                }
            }
            base.OnColumnDividerDoubleClick(e);
        }

        protected override void OnColumnWidthChanged(DataGridViewColumnEventArgs e)
        {
            base.OnColumnWidthChanged(e);
            var pd = GetPropertyDescriptor(e.Column);
            if (pd == null)
            {
                return;
            }
            var columnId = ColumnId.GetColumnId(pd);
            if (columnId != null)
            {
                var columnFormat = _bindingListSource.ColumnFormats.GetFormat(columnId);
                columnFormat = columnFormat.ChangeWidth(e.Column.Width);
                _bindingListSource.ColumnFormats.SetFormat(columnId, columnFormat);
            }
        }

        protected void UpdateColumnFormats(bool restoreDefaultFormats)
        {
            var bindingListSource = DataSource as BindingListSource;
            if (bindingListSource == null)
            {
                return;
            }
            var newColumnFormats = ImmutableList.ValueOf(_itemProperties.Select(prop=>bindingListSource.ColumnFormats.GetFormat(new ColumnId(prop.ColumnCaption))));
            if (Equals(newColumnFormats, _columnFormats))
            {
                return;
            }
            _columnFormats = newColumnFormats;
            foreach (var column in Columns.OfType<DataGridViewColumn>())
            {
                if (string.IsNullOrEmpty(column.DataPropertyName))
                {
                    continue;
                }
                DataPropertyDescriptor pd = null;
                ColumnFormat columnFormat = null;
                if (column.Index < _itemProperties.Count && _itemProperties[column.Index].Name == column.DataPropertyName)
                {
                    pd = _itemProperties[column.Index];
                    columnFormat = _columnFormats[column.Index];
                }
                else
                {
                    for (int i = 0; i < _itemProperties.Count; i++)
                    {
                        if (_itemProperties[i].Name == column.DataPropertyName)
                        {
                            pd = _itemProperties[i];
                            columnFormat = _columnFormats[i];
                        }
                    }
                }
                if (pd == null)
                {
                    continue;
                }
                if (null != columnFormat.Format)
                {
                    column.DefaultCellStyle.Format = columnFormat.Format;
                }
                else
                {
                    if (restoreDefaultFormats)
                    {
                        var originalColumn = _viewContext.CreateGridViewColumn(pd);
                        column.DefaultCellStyle.Format = originalColumn.DefaultCellStyle.Format;
                    }
                }
                if (columnFormat.Width.HasValue)
                {
                    column.Width = columnFormat.Width.Value;
                }
            }
        }

        protected void OnFormatsChanged()
        {
            UpdateColumnFormats(true);
        }

        protected override void OnRowValidating(DataGridViewCellCancelEventArgs e)
        {
            base.OnRowValidating(e);
            if (!e.Cancel)
            {
                bool cancelRowEdit;
                if (_bindingListSource != null && !_bindingListSource.ValidateRow(e.RowIndex, out cancelRowEdit))
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
