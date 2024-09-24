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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

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
            availableFieldsTreeFilter.ViewEditor = ViewEditor;
            availableFieldsTreeFilter.RootColumn = ViewInfo.ParentColumn;
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

        protected override bool UseTransformedView
        {
            get { return true; }
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
                row.Cells[colFilterColumn.Index].Value = filterInfo.ColumnDescriptor.GetColumnCaption(ColumnCaptionType.localized);
                row.Cells[colFilterColumn.Index].Style.Font = dataGridViewFilter.Font;
                row.Cells[colFilterColumn.Index].ToolTipText = null;
                row.Cells[colFilterOperand.Index].Value = filterInfo.FilterSpec.Predicate.GetOperandDisplayText(filterInfo.ColumnDescriptor);
            }
            var filterOpCell = (DataGridViewComboBoxCell)row.Cells[colFilterOperation.Index];
            filterOpCell.Value = filterInfo.FilterSpec.Predicate.FilterOperation.DisplayName;
            var filterOpItems = FilterOperations.ListOperations()
                .Where(filterOp => filterOp == filterInfo.FilterSpec.Predicate.FilterOperation
                                   || filterInfo.ColumnDescriptor == null
                                   || filterOp.IsValidFor(filterInfo.ColumnDescriptor))
                .Select(filterOp => filterOp.DisplayName)
                .ToArray();
            filterOpCell.DataSource = filterOpItems;
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
            foreach (DataGridViewCell cell in row.Cells)
            {
                if (filterInfo.Error != null)
                {
                    cell.Style.BackColor = Color.Red;
                    cell.ToolTipText = filterInfo.Error;
                }
                else
                {
                    cell.Style.BackColor = dataGridViewFilter.DefaultCellStyle.BackColor;
                    cell.ToolTipText = null;
                }
            }
        }

        private IFilterOperation FilterOperationFromDisplayName(string displayName)
        {
            return FilterOperations.ListOperations().FirstOrDefault(filterOp => filterOp.DisplayName == displayName);
        }

        protected override IEnumerable<PropertyPath> GetSelectedPaths()
        {
            IEnumerable<int> selectedRows;
            if (dataGridViewFilter.SelectedRows.Count > 0)
            {
                selectedRows = dataGridViewFilter.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Index);
            }
            else if (dataGridViewFilter.CurrentRow != null)
            {
                selectedRows = new[] {dataGridViewFilter.CurrentRow.Index};
            }
            else
            {
                return new PropertyPath[0];
            }
            var propertyPaths = new List<PropertyPath>();
            var filters = ViewInfo.Filters;
            foreach (var rowIndex in selectedRows)
            {
                if (rowIndex < filters.Count)
                {
                    propertyPaths.Add(filters[rowIndex].FilterSpec.ColumnId);
                }
            }
            return propertyPaths;
        }

        public AvailableFieldsTree AvailableFieldsTree { get { return availableFieldsTreeFilter; } }

        private void AvailableFieldsTreeFilterOnAfterSelect(object sender, TreeViewEventArgs e)
        {
            btnAddFilter.Enabled = availableFieldsTreeFilter.SelectedNode != null;
        }

        private void AddFilter(ColumnDescriptor columnDescriptor)
        {
            var newFilters = new List<FilterSpec>(ViewSpec.Filters)
                {
                    new FilterSpec(columnDescriptor.PropertyPath, FilterPredicate.HAS_ANY_VALUE)
                };
            SetViewSpec(ViewSpec.SetFilters(newFilters), null);
            dataGridViewFilter.CurrentCell = dataGridViewFilter.Rows[dataGridViewFilter.Rows.Count - 1].Cells[colFilterOperation.Index];
        }

        private void BtnDeleteFilterOnClick(object sender, EventArgs e)
        {
            DeleteSelectedFilters();
        }

        public void DeleteSelectedFilters()
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
            if (e.ColumnIndex == colFilterOperation.Index || e.ColumnIndex == colFilterOperand.Index)
            {
                var newFilters = ViewSpec.Filters.ToArray();
                var filterInfo = ViewInfo.Filters[e.RowIndex];
                var filterPredicate = SafeCreateFilterPredicate(
                    filterInfo.ColumnDescriptor.DataSchema,
                    filterInfo.ColumnDescriptor.PropertyType,
                    FilterOperationFromDisplayName(
                        dataGridViewFilter.Rows[e.RowIndex].Cells[colFilterOperation.Index].Value as string),
                    dataGridViewFilter.Rows[e.RowIndex].Cells[colFilterOperand.Index].Value as string);
                newFilters[e.RowIndex] = new FilterSpec(filterInfo.ColumnDescriptor.PropertyPath, filterPredicate);
                ViewSpec = ViewSpec.SetFilters(newFilters);
            }
        }

        private FilterPredicate SafeCreateFilterPredicate(DataSchema dataSchema, Type columnType,
            IFilterOperation filterOperation, string operand)
        {
            try
            {
                return FilterPredicate.CreateFilterPredicate(dataSchema, columnType, filterOperation, operand);
            }
            catch
            {
                return FilterPredicate.CreateFilterPredicate(dataSchema, typeof(string), filterOperation, operand);
            }
        }

        private void DataGridViewFilterOnCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                ActivatePropertyPath(ViewInfo.Filters[e.RowIndex].FilterSpec.ColumnId);
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
            var filterOperation = FilterOperationFromDisplayName(dataGridViewComboBoxEditingControl.Text);
            if (filterOperation == null)
            {
                return;
            }
            if (filterOperation == ViewInfo.Filters[rowIndex].FilterSpec.Operation)
            {
                return;
            }
            var newFilters = ViewSpec.Filters.ToArray();
            var columnDescriptor = ViewInfo.Filters[rowIndex].ColumnDescriptor;
            newFilters[rowIndex] = newFilters[rowIndex].SetPredicate(
                FilterPredicate.CreateFilterPredicate(columnDescriptor.DataSchema, columnDescriptor.PropertyType, filterOperation,
                    dataGridViewFilter.CurrentRow.Cells[colFilterOperand.Index].Value as string));
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
            Messages.WriteAsyncDebugMessage(@"DataGridViewFilterOnDataError:{0}", e.Exception);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            dataGridViewFilter.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        protected override void OnActivatePropertyPath(PropertyPath propertyPath)
        {
            availableFieldsTreeFilter.SelectColumn(propertyPath);
        }

        #region Methods exposed for testing

        public bool TrySelectColumn(PropertyPath propertyPath)
        {
            availableFieldsTreeFilter.SelectColumn(propertyPath);
            if (null == availableFieldsTreeFilter.SelectedNode)
            {
                return false;
            }
            var columnDescriptor = availableFieldsTreeFilter.GetTreeColumn(availableFieldsTreeFilter.SelectedNode);
            return null != columnDescriptor && Equals(propertyPath, columnDescriptor.PropertyPath);
        }
        public void AddSelectedColumn()
        {
            AddFilter(availableFieldsTreeFilter.GetValueColumn(availableFieldsTreeFilter.SelectedNode));
        }

        public bool SetFilterOperation(int iRow, IFilterOperation filterOperation)
        {
            dataGridViewFilter.CurrentCell = dataGridViewFilter.Rows[iRow].Cells[colFilterOperation.Index];
            var dataGridViewComboBoxEditingControl = (DataGridViewComboBoxEditingControl) dataGridViewFilter.EditingControl;
            bool found = false;
            for (int i = 0; i < dataGridViewComboBoxEditingControl.Items.Count; i++)
            {
                if (Equals(dataGridViewComboBoxEditingControl.Items[i], filterOperation.DisplayName))
                {
                    dataGridViewComboBoxEditingControl.SelectedIndex = i;
                    found = true;
                    break;
                }
            }
            dataGridViewFilter.EndEdit();
            return found;
        }

        public void SetFilterOperand(int iRow, string operand)
        {
            dataGridViewFilter.CurrentCell = dataGridViewFilter.Rows[iRow].Cells[colFilterOperand.Index];
            dataGridViewFilter.BeginEdit(true);
            var dataGridViewTextBoxEditingControl =
                (DataGridViewTextBoxEditingControl) dataGridViewFilter.EditingControl;
            dataGridViewTextBoxEditingControl.Text = operand;
            dataGridViewFilter.EndEdit();
        }
        #endregion
    }
}
