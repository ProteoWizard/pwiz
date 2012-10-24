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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Common.DataBinding.Controls
{
    /// <summary>
    /// User interface for choosing which columns should go in a view, and setting the filter and sort.
    /// </summary>
    public partial class CustomizeViewForm : Form
    {
        private static IList<ListSortDirection?> SortDirections =
            Array.AsReadOnly(new ListSortDirection?[] {null, ListSortDirection.Ascending, ListSortDirection.Descending});
        private ViewSpec _viewSpec = new ViewSpec();
        private Font _strikeThroughFont;
        private bool _inChangeView;
        private readonly int _advancedPanelWidth;
        private bool _editingControlListenerAdded;
        public CustomizeViewForm(IViewContext viewContext, ViewSpec viewSpec)
        {
            InitializeComponent();
            _advancedPanelWidth = splitContainerAdvanced.Width - splitContainerAdvanced.SplitterDistance;
            ViewContext = viewContext;
            ParentColumn = viewContext.ParentColumn;
            ViewInfo = new ViewInfo(ParentColumn, new ViewSpec());
            _strikeThroughFont = new Font(listViewColumns.Font, FontStyle.Strikeout);
            availableFieldsTreeColumns.RootColumn = ParentColumn;
            availableFieldsTreeFilter.RootColumn = ParentColumn;
            ViewSpec = OriginalViewSpec = viewSpec;
            tbxViewName.Text = viewSpec.Name;
            ExistingCustomViewSpec =
                viewContext.CustomViewSpecs.FirstOrDefault(customViewSpec => viewSpec.Name == customViewSpec.Name);
            listViewColumns.SmallImageList = AggregateFunctions.GetSmallIcons();
            AdvancedShowing = false;
            Icon = ViewContext.ApplicationIcon;
            colSortDirection.Items.Add(ListSortDirection.Ascending);
            colSortDirection.Items.Add(ListSortDirection.Descending);
        }

        public ColumnDescriptor ParentColumn { get; private set; }
        public IViewContext ViewContext { get; private set; }
        public ViewSpec OriginalViewSpec { get; private set; }
        public ViewSpec ExistingCustomViewSpec { get; private set; }
        public ViewSpec ViewSpec { 
            get
            {
                return _viewSpec;
            }
            
            private set
            {
                if (_inChangeView)
                {
                    return;
                }
                try
                {
                    _inChangeView = true;
                    if (Equals(_viewSpec, value))
                    {
                        return;
                    }
                    _viewSpec = value;
                    ViewInfo = new ViewInfo(ParentColumn, _viewSpec);
                    availableFieldsTreeColumns.CheckedColumns = ListColumnsInView();
                    ListViewHelper.ReplaceItems(listViewColumns, ViewInfo.DisplayColumns.Select(dc=>MakeListViewColumnItem(dc)).ToArray());
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
                    UpdatePropertySheet();
                    UpdateSort();
                }
                finally
                {
                    _inChangeView = false;
                }
            }
        }

        /// <summary>
        /// Returns the set of columns that should be checked in the Available Fields Tree.
        /// </summary>
        private IEnumerable<IdentifierPath> ListColumnsInView()
        {
            return ViewInfo.DisplayColumns.Where(IsCanonical).Select(dc => dc.IdentifierPath);
        }
        
        public ViewInfo ViewInfo { get; private set; }

        public bool AdvancedShowing
        {
            get
            {
                return !splitContainerAdvanced.Panel2Collapsed;
            }
            set
            {
                availableFieldsTreeColumns.ShowAdvancedFields = value;
                availableFieldsTreeFilter.ShowAdvancedFields = value;
                if (AdvancedShowing == value)
                {
                    return;
                }
                splitContainerAdvanced.Panel2Collapsed = !value;
                int newWidth;
                if (AdvancedShowing)
                {
                    btnAdvanced.Text = "<< Hide &Advanced";
                    newWidth = Width + _advancedPanelWidth;
                }
                else
                {
                    btnAdvanced.Text = "Show &Advanced >>";
                    newWidth = Width - _advancedPanelWidth;
                }
                
                if (IsHandleCreated)
                {
                    var screen = Screen.FromControl(this);
                    if (AdvancedShowing)
                    {
                        newWidth = Math.Max(Width, Math.Min(newWidth, screen.WorkingArea.Width));
                    }
                    else
                    {
                        newWidth = Math.Min(Width, Math.Max(newWidth, 200));
                    }
                }
                Width = newWidth;
                if (AdvancedShowing)
                {
                    splitContainerAdvanced.SplitterDistance = splitContainerAdvanced.Width - _advancedPanelWidth;
                }
            }
        }

        private void availableFieldsTreeColumns_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            var treeNode = e.Node;
            var columnDescriptor = availableFieldsTreeColumns.GetValueColumn(treeNode);
            if (columnDescriptor == null)
            {
                return;
            }
            if (treeNode.Checked)
            {
                AddColumn(columnDescriptor.IdPath);
            }
            else
            {
                RemoveColumn(columnDescriptor.IdPath);
            }
        }
        private void AddColumn(IdentifierPath identifierPath)
        {
            List<ColumnSpec> columnSpecs = new List<ColumnSpec>(ViewSpec.Columns);
            var newColumn = new ColumnSpec(identifierPath);
            if (listViewColumns.SelectedIndices.Count == 0)
            {
                columnSpecs.Add(newColumn);
            }
            else
            {
                columnSpecs.Insert(listViewColumns.SelectedIndices.Cast<int>().Min(), newColumn);
            }
            ViewSpec = ViewSpec.SetColumns(columnSpecs);
        }
        private void RemoveColumn(IdentifierPath identifierPath)
        {
            int index = IndexOfCanconical(identifierPath);
            if (index < 0)
            {
                return;
            }
            var newColumns = ViewSpec.Columns.ToList();
            newColumns.RemoveAt(index);
            ViewSpec = ViewSpec.SetColumns(newColumns);
        }
        private ListViewItem MakeListViewColumnItem(DisplayColumn displayColumn)
        {
            var listViewItem = new ListViewItem();
            if (displayColumn.ColumnDescriptor == null)
            {
                listViewItem.Text = displayColumn.ColumnCaption;
                listViewItem.Font = _strikeThroughFont;
            }
            else
            {
                listViewItem.Text = displayColumn.ColumnCaption;
            }
            if (displayColumn.ColumnSpec.Hidden)
            {
                listViewItem.ForeColor = Color.DarkGray;
            }
            else
            {
                listViewItem.ForeColor = listViewColumns.ForeColor;
            }
            return listViewItem;
        }
        private void SetFilterInfo(DataGridViewRow row, FilterInfo filterInfo)
        {
            if (filterInfo.ColumnDescriptor == null)
            {
                row.Cells[colFilterColumn.Index].Value = filterInfo.FilterSpec.Column;
                row.Cells[colFilterColumn.Index].Style.Font = new Font(dataGridViewFilter.Font, FontStyle.Strikeout);
                row.Cells[colFilterColumn.Index].ToolTipText = "This column does not exist";
            }
            else
            {
                row.Cells[colFilterColumn.Index].Value = filterInfo.ColumnDescriptor.DisplayName;
                row.Cells[colFilterColumn.Index].Style.Font = dataGridViewFilter.Font;
                row.Cells[colFilterColumn.Index].ToolTipText = null;
            }
            var filterOpCell = (DataGridViewComboBoxCell) row.Cells[colFilterOperation.Index];
            filterOpCell.Items.Clear();
            filterOpCell.DisplayMember = "DisplayName";
            filterOpCell.ValueMember = "Operation";
            foreach (var filterOperation in FilterOperations.ListOperations())
            {
                bool selected = filterInfo.FilterSpec.Operation == filterOperation;
                if (selected || filterInfo.ColumnDescriptor == null || filterOperation.IsValidFor(filterInfo.ColumnDescriptor))
                {
                    filterOpCell.Items.Add(new FilterOperationItem(filterOperation));
                    if (selected)
                    {
                        filterOpCell.Value = filterOperation;
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
            row.Cells[colFilterOperand.Index].Value = filterInfo.FilterSpec.Operand;
        }

        public ColumnSpec[] GetSelectedColumns()
        {
            return listViewColumns.SelectedIndices.Cast<int>()
                .Select(index => ViewSpec.Columns[index]).ToArray();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel || DialogResult == DialogResult.Cancel)
            {
                return;
            }
            ValidateViewName(e);
        }

        protected void ValidateViewName(FormClosingEventArgs formClosingEventArgs)
        {
            if (formClosingEventArgs.Cancel)
            {
                return;
            }
            ViewSpec = ViewSpec.SetName(tbxViewName.Text);
            var name = ViewSpec.Name;
            string errorMessage = null;
            if (string.IsNullOrEmpty(name))
            {
                errorMessage = "View name cannot be blank.";
            }
            else if (ViewContext.BuiltInViewSpecs.FirstOrDefault(viewSpec=>name==viewSpec.Name) != null)
            {
                errorMessage = string.Format("There is already a built in view named '{0}'.", name);
            }
            else
            {
                var currentExistingView = ViewContext.CustomViewSpecs.FirstOrDefault(viewSpec => name == viewSpec.Name);
                if (currentExistingView != null && name != OriginalViewSpec.Name)
                {
                    if (ViewContext.ShowMessageBox(this, string.Format("Do you want to overwrite the existing view named '{0}'", name), MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                    {
                        formClosingEventArgs.Cancel = true;
                    }
                }
            }
            if (errorMessage != null)
            {
                ViewContext.ShowMessageBox(this, errorMessage, MessageBoxButtons.OK);
                formClosingEventArgs.Cancel = true;
            }
            if (formClosingEventArgs.Cancel)
            {
                tbxViewName.Focus();
            }
        }

        private void UpdatePropertySheet()
        {
            var selectedColumns =
                listViewColumns.SelectedIndices.Cast<int>().Select(i => ViewInfo.DisplayColumns[i]).ToArray();
            if (selectedColumns.Length == 1)
            {
                groupBoxProperties.Text = "Column Properties";
                var selectedColumn = selectedColumns[0];

                if (selectedColumn.ColumnSpec.Caption != null)
                {
                    tbxCaption.Text = selectedColumn.ColumnSpec.Caption;
                    tbxCaption.Font = new Font(tbxCaption.Font, FontStyle.Bold);
                }
                else
                {
                    var columnDescriptor = ParentColumn.ResolveDescendant(selectedColumn.IdentifierPath);
                    if (columnDescriptor != null)
                    {
                        tbxCaption.Text = columnDescriptor.DefaultCaption;
                        tbxCaption.Font = new Font(tbxCaption.Font, FontStyle.Regular);
                    }
                    else
                    {
                        tbxCaption.Text = "";
                    }
                }
                comboSortOrder.SelectedIndex = SortDirections.IndexOf(selectedColumn.ColumnSpec.SortDirection);
                cbxHidden.Checked = selectedColumn.ColumnSpec.Hidden;

                groupBoxCaption.Visible = true;
                comboSortOrder.Visible = true;
                cbxHidden.Visible = CanBeHidden(selectedColumn);
            }
            else
            {
                groupBoxProperties.Text = selectedColumns.Length == 0 ? "No Selection" : "Multiple Selection";
                groupBoxCaption.Visible = false;
                cbxHidden.Visible = false;
                comboSortOrder.Visible = false;
            }
            btnRemove.Enabled = selectedColumns.Length > 0;
            btnUp.Enabled = ListViewHelper.IsMoveUpEnabled(listViewColumns);
            btnDown.Enabled = ListViewHelper.IsMoveDownEnabled(listViewColumns);
            listViewColumns_SizeChanged(listViewColumns, new EventArgs());
            UpdateSublistCombo();
        }

        private void UpdateSort()
        {
            clbAvailableSortColumns.BeginUpdate();
            for (int i = 0; i < ViewInfo.DisplayColumns.Count; i++)
            {
                var displayColumn = ViewInfo.DisplayColumns[i];
                if (i < clbAvailableSortColumns.Items.Count)
                {
                    clbAvailableSortColumns.Items[i] = displayColumn.ColumnCaption;
                }
                else
                {
                    clbAvailableSortColumns.Items.Add(displayColumn.ColumnCaption);
                }
                clbAvailableSortColumns.SetItemChecked(i, displayColumn.ColumnSpec.SortDirection != null);
            }
            while (clbAvailableSortColumns.Items.Count > ViewInfo.DisplayColumns.Count)
            {
                clbAvailableSortColumns.Items.RemoveAt(clbAvailableSortColumns.Items.Count - 1);
            }
            clbAvailableSortColumns.EndUpdate();
            if (dataGridViewSort.Rows.Count != ViewInfo.SortColumns.Count)
            {
                dataGridViewSort.Rows.Clear();
                if (ViewInfo.SortColumns.Count > 0)
                {
                    dataGridViewSort.Rows.Add(ViewInfo.SortColumns.Count);
                }
            }
            for (int i = 0; i < ViewInfo.SortColumns.Count; i++)
            {
                var row = dataGridViewSort.Rows[i];
                row.Cells[colSortColumn.Index].Value = ViewInfo.SortColumns[i].ColumnCaption;
                row.Cells[colSortDirection.Index].Value = ViewInfo.SortColumns[i].ColumnSpec.SortDirection;
            }
            UpdateSortButtons();
        }

        private void UpdateSortButtons()
        {
            var selectedIndexes = GetSelectedSortRowIndexes();
            btnSortRemove.Enabled = selectedIndexes.Count > 0;
            btnSortMoveUp.Enabled = ListViewHelper.IsMoveEnabled(dataGridViewSort.Rows.Count, selectedIndexes, true);
            btnSortMoveDown.Enabled = ListViewHelper.IsMoveEnabled(dataGridViewSort.Rows.Count, selectedIndexes, false);
        }

        private IList<int> GetSelectedSortRowIndexes()
        {
            return dataGridViewSort.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Index).ToArray();
        }

        /// <summary>
        /// Returns true if there is some reason the user would want to hide
        /// this column in the grid (either because it impacts the sort order,
        /// or the GroupBy
        /// </summary>
        private bool CanBeHidden(DisplayColumn displayColumn)
        {
            return displayColumn.ColumnSpec.Hidden
                   || null != displayColumn.ColumnSpec.SortDirection;
        }

        private bool IsCanonical(DisplayColumn displayColumn)
        {
            if (displayColumn.ColumnSpec.Hidden && null != displayColumn.ColumnSpec.SortDirection)
            {
                return false;
            }
            return true;
        }
        private int IndexOfCanconical(IdentifierPath identifierPath)
        {
            for (int i = 0; i < ViewInfo.DisplayColumns.Count; i++)
            {
                var displayColumn = ViewInfo.DisplayColumns[i];
                if (Equals(identifierPath, displayColumn.IdentifierPath) && IsCanonical(displayColumn))
                {
                    return i;
                }
            }
            return -1;
        }

        private void UpdateSublistCombo()
        {
            var availableSublists = new HashSet<IdentifierPath>();
            availableSublists.Add(IdentifierPath.Root);
            foreach (var columnSpec in ViewSpec.Columns)
            {
                for (IdentifierPath idPath = columnSpec.IdentifierPath; !idPath.IsRoot; idPath = idPath.Parent)
                {
                    if (idPath.Name == null)
                    {
                        if (!availableSublists.Add(idPath))
                        {
                            break;
                        }
                    }
                }
            }
            availableSublists.Add(ViewSpec.SublistId);
            if (availableSublists.Count == 1)
            {
                groupBoxSublist.Visible = false;
                return;
            }
            groupBoxSublist.Visible = true;
            var sublistIdArray = availableSublists.ToArray();
            Array.Sort(sublistIdArray);
            comboSublist.Items.Clear();
            foreach (var idPath in sublistIdArray)
            {
                string label = idPath.IsRoot ? "<None>" : GetSublistLabel(idPath);
                comboSublist.Items.Add(new SublistItem(label, idPath));
                if (Equals(idPath, ViewSpec.SublistId))
                {
                    comboSublist.SelectedIndex = comboSublist.Items.Count - 1;
                }
            }
        }

        private string GetSublistLabel(IdentifierPath identifierPath)
        {
            var parts = new List<string>();
            while (!identifierPath.IsRoot)
            {
                var treeNode = availableFieldsTreeColumns.FindTreeNode(identifierPath, true);
                if (treeNode == null)
                {
                    parts.Add(identifierPath.Name);
                    identifierPath = identifierPath.Parent;
                }
                else
                {
                    while (treeNode != null)
                    {
                        parts.Add(treeNode.Text);
                        treeNode = treeNode.Parent;
                    }
                    break;
                }
            }
            parts.Reverse();
            return string.Join(":", parts.ToArray());
        }

        private void tbxCaption_Leave(object sender, EventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            var selectedIndices = listViewColumns.SelectedIndices;
            if (selectedIndices.Count != 1)
            {
                return;
            }
            string newValue = tbxCaption.Text;
            var displayColumn = ViewInfo.DisplayColumns[selectedIndices[0]];
            if (Equals(newValue, displayColumn.ColumnSpec.Caption))
            {
                return;
            }
            if (Equals(newValue, displayColumn.DefaultDisplayName))
            {
                newValue = null;
            }
            if (Equals(newValue, displayColumn.ColumnSpec.Caption))
            {
                return;
            }
            var newColumns = ViewSpec.Columns.ToArray();
            newColumns[listViewColumns.SelectedIndices[0]] 
                = displayColumn.ColumnSpec.SetCaption(newValue);
            ViewSpec = ViewSpec.SetColumns(newColumns);
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            ViewSpec = ViewSpec.SetColumns(
                ViewSpec.Columns.Where((columnSpec, index) => !listViewColumns.Items[index].Selected));
        }

        private void listViewColumns_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            UpdatePropertySheet();
        }

        private void MoveColumns(bool upwards)
        {
            var selectedIndexes = listViewColumns.SelectedIndices.Cast<int>().ToArray();
            var newIndexes = ListViewHelper.MoveItems(
                Enumerable.Range(0, listViewColumns.Items.Count), selectedIndexes, upwards);
            var newSelection = ListViewHelper.MoveSelectedIndexes(listViewColumns.Items.Count, selectedIndexes, upwards);
            var newColumns = newIndexes.Select(i => ViewSpec.Columns[i]);
            ViewSpec = ViewSpec.SetColumns(newColumns);
            ListViewHelper.SelectIndexes(listViewColumns, newSelection);
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            MoveColumns(true);
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            MoveColumns(false);
        }

        private void listViewColumns_SizeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(AfterResizeListViewColumns));
            }
        }

        private void AfterResizeListViewColumns()
        {
            listViewColumns.Columns[0].Width = listViewColumns.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 1;
        }
        class AggregateItem
        {
            public AggregateItem(string displayName, IAggregateFunction function)
            {
                DisplayName = displayName;
                Function = function;
            }
            public string DisplayName { get; set; }
            public IAggregateFunction Function { get; set; }
            public override string ToString() { return DisplayName; }
        }
        class FilterOperationItem
        {
            public FilterOperationItem(IFilterOperation filterOperation)
            {
                Operation = filterOperation;
            }

            public IFilterOperation Operation { get; private set; }
            public string DisplayName { get { return Operation.DisplayName; } }
            public override string ToString() { return Operation.DisplayName; }
        }
        class SublistItem
        {
            public SublistItem(string displayName, IdentifierPath identifierPath)
            {
                DisplayName = displayName;
                IdentifierPath = identifierPath;
            }
            public string DisplayName { get; set; }
            public override string ToString()
            {
                return DisplayName;
            }
            public IdentifierPath IdentifierPath { get; private set; }
        }

        private void comboSublist_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            var sublistItem = comboSublist.SelectedItem as SublistItem;
            if (sublistItem != null)
            {
                ViewSpec = _viewSpec.SetSublistId(sublistItem.IdentifierPath);
            }
        }

        private void btnAdvanced_Click(object sender, EventArgs e)
        {
            AdvancedShowing = !AdvancedShowing;
        }

        private void availableFieldsTreeColumns_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var columnDescriptor = availableFieldsTreeColumns.GetValueColumn(e.Node);
            if (columnDescriptor == null)
            {
                return;
            }
            foreach (ListViewItem item in listViewColumns.SelectedItems)
            {
                var displayColumn = ViewInfo.DisplayColumns[item.Index];
                if (Equals(columnDescriptor.IdPath, displayColumn.IdentifierPath) && IsCanonical(displayColumn))
                {
                    item.Focused = true;
                    if (item.Selected && listViewColumns.SelectedIndices.Count == 1)
                    {
                        return;
                    }
                    listViewColumns.SelectedIndices.Clear();
                    item.Selected = true;
                    return;
                }
            }
        }

        private void listViewColumns_ItemActivate(object sender, EventArgs e)
        {
            availableFieldsTreeColumns.SelectColumn(ViewSpec.Columns[listViewColumns.FocusedItem.Index].IdentifierPath);
        }

        private void cbxHidden_CheckedChanged(object sender, EventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }

            var columns = ViewSpec.Columns.ToArray();
            foreach (var index in listViewColumns.SelectedIndices.Cast<int>())
            {
                columns[index] = columns[index].SetHidden(
                    cbxHidden.Checked && CanBeHidden(ViewInfo.DisplayColumns[index]));
            }
            ViewSpec = ViewSpec.SetColumns(columns);
        }

        private void availableFieldsTreeFilter_AfterSelect(object sender, TreeViewEventArgs e)
        {
            btnAddFilter.Enabled = availableFieldsTreeFilter.SelectedNode != null;
        }

        private void AddFilter(ColumnDescriptor columnDescriptor)
        {
            var newFilters = new List<FilterSpec>(ViewSpec.Filters);
            newFilters.Add(new FilterSpec(columnDescriptor.IdPath, FilterOperations.OpHasAnyValue, null));
            ViewSpec = ViewSpec.SetFilters(newFilters);
        }

        private void btnDeleteFilter_Click(object sender, EventArgs e)
        {
            var newFilters = ViewSpec.Filters.Where((filterSpec, index) => !dataGridViewFilter.Rows[index].Selected);
            ViewSpec = ViewSpec.SetFilters(newFilters);
        }

        private void btnAddFilter_Click(object sender, EventArgs e)
        {
            if (availableFieldsTreeFilter.SelectedNode == null)
            {
                return;
            }
            var columnDescriptor = availableFieldsTreeFilter.GetValueColumn(availableFieldsTreeFilter.SelectedNode);
            AddFilter(columnDescriptor);
        }

        private void dataGridViewFilter_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            if (e.ColumnIndex == colFilterOperation.Index)
            {
                var newFilters = ViewSpec.Filters.ToArray();
                var cell = dataGridViewFilter.Rows[e.RowIndex].Cells[e.ColumnIndex];
                newFilters[e.RowIndex] = newFilters[e.RowIndex].SetOperation(cell.Value as IFilterOperation);
                ViewSpec = ViewSpec.SetFilters(newFilters);
                return;
            }
            if (e.ColumnIndex == colFilterOperand.Index)
            {
                var newFilters = ViewSpec.Filters.ToArray();
                var cell = dataGridViewFilter.Rows[e.RowIndex].Cells[e.ColumnIndex];
                newFilters[e.RowIndex] = newFilters[e.RowIndex].SetOperand((string) cell.Value);
                ViewSpec = ViewSpec.SetFilters(newFilters);
                return;
            }
        }

        private void dataGridViewFilter_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                availableFieldsTreeFilter.SelectColumn(ViewInfo.Filters[e.RowIndex].FilterSpec.ColumnId);
            }
        }

        private void availableFieldsTreeFilter_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            AddFilter(availableFieldsTreeFilter.GetValueColumn(e.Node));
        }

        private void dataGridViewFilter_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_inChangeView)
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

        private void dataGridViewFilter_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_editingControlListenerAdded)
            {
                return;
            }
            var dataGridViewComboBoxEditingControl = e.Control as DataGridViewComboBoxEditingControl;
            if (dataGridViewComboBoxEditingControl != null)
            {
                dataGridViewComboBoxEditingControl.SelectedIndexChanged += dataGridViewComboBoxEditingControl_SelectedIndexChanged;
                _editingControlListenerAdded = true;
            }
        }

        void dataGridViewComboBoxEditingControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeView)
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

        private void comboSortOrder_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            if (comboSortOrder.SelectedIndex < 0)
            {
                return;
            }
            var columns = ViewSpec.Columns.ToArray();
            foreach (var index in listViewColumns.SelectedIndices.Cast<int>())
            {
                columns[index] = columns[index].SetSortDirection(SortDirections[comboSortOrder.SelectedIndex]);
            }
            ViewSpec = ViewSpec.SetColumns(columns);
        }

        private void dataGridViewSort_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            if (dataGridViewSort.CurrentCell.ColumnIndex == colSortDirection.Index)
            {
                if (dataGridViewSort.IsCurrentCellDirty)
                {
                    dataGridViewSort.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            }
        }

        private void dataGridViewSort_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            if (e.ColumnIndex == colSortDirection.Index)
            {
                var displayColumnIndex = ViewInfo.DisplayColumns.IndexOf(ViewInfo.SortColumns[e.RowIndex]);
                var newColumns = ViewSpec.Columns.ToArray();
                var newSortDirection = (ListSortDirection) Enum.Parse(typeof(ListSortDirection), Convert.ToString(dataGridViewSort.Rows[e.RowIndex].Cells[e.ColumnIndex].Value));
                newColumns[displayColumnIndex] = newColumns[displayColumnIndex].SetSortDirection(newSortDirection);
                ViewSpec = ViewSpec.SetColumns(newColumns);
            }
        }

        private void btnSortRemove_Click(object sender, EventArgs e)
        {
            var newColumns = ViewSpec.Columns.ToArray();
            foreach (var index in GetSelectedSortRowIndexes())
            {
                newColumns[index] = newColumns[index].SetSortDirection(null).SetSortIndex(null);
            }
            ViewSpec = ViewSpec.SetColumns(newColumns);
        }

        private void MoveSort(bool upwards)
        {
            var selectedIndexes = GetSelectedSortRowIndexes();
            var newIndexes = ListViewHelper.MoveItems(Enumerable.Range(0, ViewInfo.SortColumns.Count), selectedIndexes,
                                                      upwards);
            var newSelectedIndexes = ListViewHelper.MoveSelectedIndexes(ViewInfo.SortColumns.Count, selectedIndexes,
                                                                        upwards);
            var newColumns = ViewSpec.Columns.ToArray();
            for (int i = 0; i < newIndexes.Count(); i++)
            {
                var displayColumn = ViewInfo.SortColumns[newIndexes[i]];
                var displayColumnIndex = ViewInfo.DisplayColumns.IndexOf(displayColumn);
                newColumns[displayColumnIndex] = newColumns[displayColumnIndex].SetSortIndex(i);
            }
            ViewSpec = ViewSpec.SetColumns(newColumns);
            for (int i = 0; i < dataGridViewSort.Rows.Count; i++)
            {
                dataGridViewSort.Rows[i].Selected = newSelectedIndexes.Contains(i);
            }
        }

        private void btnSortMoveUp_Click(object sender, EventArgs e)
        {
            MoveSort(true);
        }

        private void btnSortMoveDown_Click(object sender, EventArgs e)
        {
            MoveSort(false);
        }

        private void dataGridViewSort_SelectionChanged(object sender, EventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            UpdateSortButtons();
        }

        private void clbAvailableSortColumns_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            var newColumns = ViewSpec.Columns.ToArray();
            var column = newColumns[e.Index];
            if (column.SortDirection == null)
            {
                column = column.SetSortDirection(ListSortDirection.Ascending);
            }
            else
            {
                column = column.SetSortDirection(null).SetSortIndex(null);
            }
            newColumns[e.Index] = column;
            ViewSpec = ViewSpec.SetColumns(newColumns);
        }

        private void dataGridViewFilter_CellEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            if (e.ColumnIndex == colFilterOperation.Index)
            {
                dataGridViewFilter.BeginEdit(true);
            }
        }
    }
}
