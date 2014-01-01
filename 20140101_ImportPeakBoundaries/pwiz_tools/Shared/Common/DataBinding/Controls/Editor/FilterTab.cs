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
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Properties;

namespace pwiz.Common.DataBinding.Controls.Editor
{
    public partial class FilterTab : ViewEditorWidget
    {
        private bool _editingControlListenerAdded;
        public FilterTab()
        {
            InitializeComponent();
            btnDeleteFilter.Image = Resources.Delete;
        }

        protected override void OnViewChange()
        {
            base.OnViewChange();
            availableFieldsTreeFilter.ShowAdvancedFields = ViewEditor.ShowHiddenFields;
            availableFieldsTreeFilter.RootColumn = ViewEditor.ViewInfo.ParentColumn;
            if (dataGridViewFilter.Rows.Count != ViewInfo.Filters.Count)
            {
                dataGridViewFilter.Rows.Clear();
                if (ViewInfo.Filters.Count > 0)
                {
                    dataGridViewFilter.Rows.Add(ViewInfo.Filters.Count);
                }
            }
            for (int iFilter = 0; iFilter < ViewInfo.Filters.Count; iFilter++)
            {
                SetFilterInfo(dataGridViewFilter.Rows[iFilter], ViewInfo.Filters[iFilter]);
            }
        }

        private void SetFilterInfo(DataGridViewRow row, FilterInfo filterInfo)
        {
            if (filterInfo.ColumnDescriptor == null)
            {
                row.Cells[colFilterColumn.Index].Value = filterInfo.FilterSpec.Column;
                row.Cells[colFilterColumn.Index].Style.Font = new Font(dataGridViewFilter.Font, FontStyle.Strikeout);
                row.Cells[colFilterColumn.Index].ToolTipText = Resources.CustomizeViewForm_SetFilterInfo_This_column_does_not_exist;
            }
            else
            {
                row.Cells[colFilterColumn.Index].Value = filterInfo.ColumnDescriptor.DisplayName;
                row.Cells[colFilterColumn.Index].Style.Font = dataGridViewFilter.Font;
                row.Cells[colFilterColumn.Index].ToolTipText = null;
            }
            var filterOpCell = (DataGridViewComboBoxCell)row.Cells[colFilterOperation.Index];
            row.Cells[colFilterOperand.Index].Value = filterInfo.FilterSpec.Operand;
            filterOpCell.Value = null;
            filterOpCell.Items.Clear();
            foreach (var filterOperation in FilterOperations.ListOperations())
            {
                bool selected = filterInfo.FilterSpec.Operation == filterOperation;
                if (selected || filterInfo.ColumnDescriptor == null || filterOperation.IsValidFor(filterInfo.ColumnDescriptor))
                {
                    var item = new FilterOperationItem(filterOperation);
                    filterOpCell.Items.Add(item);
                    if (selected)
                    {
                        filterOpCell.Value = item;
                    }
                }
            }
            if (filterInfo.FilterSpec.Operation.GetOperandType(filterInfo.ColumnDescriptor) == null)
            {
                row.Cells[colFilterOperand.Index].ReadOnly = true;
                row.Cells[colFilterOperand.Index].Style.BackColor = Color.DarkGray;
            }
            else
            {
                row.Cells[colFilterOperand.Index].ReadOnly = false;
                row.Cells[colFilterOperand.Index].Style.BackColor = colFilterOperand.DefaultCellStyle.BackColor;
            }
        }
        class FilterOperationItem
        {
            public FilterOperationItem(IFilterOperation filterOperation)
            {
                Operation = filterOperation;
            }

            public IFilterOperation Operation { get; private set; }
            public override string ToString() { return Operation.DisplayName; }
        }

        private void AvailableFieldsTreeFilterOnAfterSelect(object sender, TreeViewEventArgs e)
        {
            btnAddFilter.Enabled = availableFieldsTreeFilter.SelectedNode != null;
        }

        private void AddFilter(ColumnDescriptor columnDescriptor)
        {
            var newFilters = new List<FilterSpec>(ViewSpec.Filters)
                {
                    new FilterSpec(columnDescriptor.PropertyPath, FilterOperations.OP_HAS_ANY_VALUE, null)
                };
            SetViewSpec(ViewSpec.SetFilters(newFilters), null);
            dataGridViewFilter.CurrentCell = dataGridViewFilter.Rows[dataGridViewFilter.Rows.Count - 1].Cells[colFilterOperation.Index];
        }

        private void BtnDeleteFilterOnClick(object sender, EventArgs e)
        {
            var newFilters = ViewSpec.Filters.Where((filterSpec, index) => !dataGridViewFilter.Rows[index].Selected);
            ViewSpec = ViewSpec.SetFilters(newFilters);
        }

        private void BtnAddFilterOnClick(object sender, EventArgs e)
        {
            if (availableFieldsTreeFilter.SelectedNode == null)
            {
                return;
            }
            var columnDescriptor = availableFieldsTreeFilter.GetValueColumn(availableFieldsTreeFilter.SelectedNode);
            AddFilter(columnDescriptor);
        }

        private void DataGridViewFilterOnCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (InChangeView)
            {
                return;
            }
            if (e.ColumnIndex == colFilterOperation.Index)
            {
                var newFilters = ViewSpec.Filters.ToArray();
                var cell = dataGridViewFilter.Rows[e.RowIndex].Cells[e.ColumnIndex];
                newFilters[e.RowIndex] = newFilters[e.RowIndex].SetOperation(((FilterOperationItem)cell.Value).Operation);
                ViewSpec = ViewSpec.SetFilters(newFilters);
            }
            else if (e.ColumnIndex == colFilterOperand.Index)
            {
                var newFilters = ViewSpec.Filters.ToArray();
                var cell = dataGridViewFilter.Rows[e.RowIndex].Cells[e.ColumnIndex];
                newFilters[e.RowIndex] = newFilters[e.RowIndex].SetOperand((string)cell.Value);
                ViewSpec = ViewSpec.SetFilters(newFilters);
            }
        }

        private void DataGridViewFilterOnCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                availableFieldsTreeFilter.SelectColumn(ViewInfo.Filters[e.RowIndex].FilterSpec.ColumnId);
            }
        }

        private void AvailableFieldsTreeFilterOnNodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            AddFilter(availableFieldsTreeFilter.GetValueColumn(e.Node));
        }

        private void DataGridViewFilterOnCurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (InChangeView)
            {
                return;
            }
            if (dataGridViewFilter.CurrentCell.ColumnIndex == colFilterOperation.Index)
            {
                if (dataGridViewFilter.IsCurrentCellDirty)
                {
                    dataGridViewFilter.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            }
        }

        private void DataGridViewFilterOnEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_editingControlListenerAdded)
            {
                return;
            }
            var dataGridViewComboBoxEditingControl = e.Control as DataGridViewComboBoxEditingControl;
            if (dataGridViewComboBoxEditingControl != null)
            {
                dataGridViewComboBoxEditingControl.SelectedIndexChanged += DataGridViewComboBoxEditingControlOnSelectedIndexChanged;
                _editingControlListenerAdded = true;
            }
        }
        void DataGridViewComboBoxEditingControlOnSelectedIndexChanged(object sender, EventArgs e)
        {
            if (InChangeView)
            {
                return;
            }
            BeginInvoke(new Action(CommitFilterOpCombo));
        }

        void CommitFilterOpCombo()
        {
            var dataGridViewComboBoxEditingControl = dataGridViewFilter.EditingControl as DataGridViewComboBoxEditingControl;
            if (dataGridViewComboBoxEditingControl == null || dataGridViewFilter.CurrentRow == null)
            {
                return;
            }
            var rowIndex = dataGridViewFilter.CurrentRow.Index;
            var selectedItem = dataGridViewComboBoxEditingControl.SelectedItem as FilterOperationItem;
            if (selectedItem == null)
            {
                return;
            }
            if (selectedItem.Operation == ViewInfo.Filters[rowIndex].FilterSpec.Operation)
            {
                return;
            }
            var newFilters = ViewSpec.Filters.ToArray();
            newFilters[rowIndex] = newFilters[rowIndex].SetOperation(selectedItem.Operation);
            ViewSpec = ViewSpec.SetFilters(newFilters);
        }

        private void DataGridViewFilterOnCellEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (InChangeView)
            {
                return;
            }
            if (e.ColumnIndex == colFilterOperation.Index)
            {
                dataGridViewFilter.BeginEdit(true);
            }
        }

        private void dataGridViewFilter_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            Trace.TraceError("DataGridViewFilterOnDataError:{0}", e.Exception); // Not L10N
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            dataGridViewFilter.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        }
    }
}
