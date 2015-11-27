/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Internal;
using pwiz.Skyline.Alerts;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

// This code is associated with the DocumentGrid.

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class DataboundGridControl: UserControl
    {
        private PropertyDescriptor _columnFilterPropertyDescriptor;
        private readonly object _errorMessageLock = new object();
        private bool _errorMessagePending;
        private bool _suppressErrorMessages;

        public DataboundGridControl()
        {
            InitializeComponent();
        }

        public BindingListSource BindingListSource
        {
            get { return bindingListSource; }
            set
            {
                if (null != BindingListSource)
                {
                    BindingListSource.DataError -= bindingListSource_DataError;
                }
                bindingListSource = value;
                if (null != BindingListSource)
                {
                    BindingListSource.DataError += bindingListSource_DataError;
                }
                NavBar.BindingListSource = bindingListSource;
                boundDataGridView.DataSource = bindingListSource;
            }
        }

        /// <summary>
        /// If this control is a child of the SkylineWindow (not a popup), then returns the
        /// SkylineWindow.  Otherwise returns null.
        /// </summary>
        protected SkylineWindow FindParentSkylineWindow()
        {
            for (Control control = this; control != null; control = control.Parent)
            {
                var skylineWindow = control as SkylineWindow;
                if (skylineWindow != null)
                {
                    return skylineWindow;
                }
            }
            return null;
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            var skylineWindow = FindParentSkylineWindow();
            if (skylineWindow != null)
            {
                skylineWindow.ClipboardControlGotFocus(this);
            }
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            var skylineWindow = FindParentSkylineWindow();
            if (skylineWindow != null)
            {
                skylineWindow.ClipboardControlLostFocus(this);
            }
        }

        /// <summary>
        /// Testing method: Sends Ctrl-V to this control.
        /// </summary>
        public void SendPaste()
        {
            OnKeyDown(new KeyEventArgs(Keys.V | Keys.Control));
        }

        #region Methods exposed for testing
        public BoundDataGridViewEx DataGridView { get { return boundDataGridView; } }
        public NavBar NavBar { get { return navBar; } }

        public DataGridViewColumn FindColumn(PropertyPath propertyPath)
        {
            var propertyDescriptor =
                BindingListSource.GetItemProperties(null)
                    .OfType<ColumnPropertyDescriptor>()
                    .FirstOrDefault(colPd => Equals(propertyPath, colPd.PropertyPath));
            if (null == propertyDescriptor)
            {
                return null;
            }
            return DataGridView.Columns.Cast<DataGridViewColumn>().FirstOrDefault(col => col.DataPropertyName == propertyDescriptor.Name);
        }
        public bool IsComplete
        {
            get
            {
                return BindingListSource.IsComplete;
            }
        }

        public void ChooseView(string viewName)
        {
            var groups = new[] {ViewGroup.BUILT_IN}.Concat(BindingListSource.ViewContext.ViewGroups);
            foreach (var viewGroup in groups)
            {
                foreach (var viewSpec in BindingListSource.ViewContext.GetViewSpecList(viewGroup.Id).ViewSpecs)
                {
                    if (viewSpec.Name == viewName)
                    {
                        ChooseView(viewGroup.Id.ViewName(viewSpec.Name));
                        return;
                    }
                }
            }
            throw new InvalidOperationException(string.Format("No view named {0}", viewName)); // Not L10N
        }

        public bool ChooseView(ViewName viewName)
        {
            var viewInfo = BindingListSource.ViewContext.GetViewInfo(viewName);
            if (null == viewInfo)
            {
                return false;
            }
            BindingListSource.SetViewContext(BindingListSource.ViewContext, viewInfo);
            return true;
        }

        public int RowCount
        {
            get { return DataGridView.RowCount; }
        }

        public int ColumnCount
        {
            get { return DataGridView.ColumnCount; }
        }

        public string[] ColumnHeaderNames
        {
            get
            {
                return DataGridView.Columns.Cast<DataGridViewColumn>().Select(col => col.HeaderText).ToArray();
            }
        }

        public void ManageViews()
        {
            BindingListSource.ViewContext.ManageViews(NavBar);
        }

        public void QuickFilter(DataGridViewColumn column)
        {
            _columnFilterPropertyDescriptor = BindingListSource.GetItemProperties(null)[column.DataPropertyName];
            filterToolStripMenuItem_Click(filterToolStripMenuItem, new EventArgs());
        }

        #endregion

        protected virtual void boundDataGridView_CellContextMenuStripNeeded(object sender, DataGridViewCellContextMenuStripNeededEventArgs e)
        {
            PropertyDescriptor propertyDescriptor = null;
            if (e.ColumnIndex >= 0)
            {
                var column = boundDataGridView.Columns[e.ColumnIndex];
                propertyDescriptor = GetPropertyDescriptor(column);
            }
            e.ContextMenuStrip = contextMenuStrip;
            _columnFilterPropertyDescriptor = propertyDescriptor;
            UpdateContextMenuItems();
        }

        public bool IsEnableFillDown()
        {
            PropertyDescriptor[] propertyDescriptors;
            int firstRowIndex;
            int lastRowIndex;
            return GetRectangularSelection(out propertyDescriptors, out firstRowIndex, out lastRowIndex);
        }

        /// <summary>
        /// Returns true if the selection in the DataGridView is rectangular (i.e. a continuous set of rows),
        /// and that none of the columns in the selection are read-only.
        /// </summary>
        private bool GetRectangularSelection(
            out PropertyDescriptor[] propertyDescriptors,
            out int firstRowIndex,
            out int lastRowIndex)
        {
            propertyDescriptors = null;
            firstRowIndex = lastRowIndex = -1;
            if (DataGridView.SelectedRows.Count > 0)
            {
                return false;
            }
            var cellsByColumn = DataGridView.SelectedCells.Cast<DataGridViewCell>().ToLookup(cell => cell.ColumnIndex);
            int? firstRow = null;
            int? lastRow = null;
            foreach (var grouping in cellsByColumn)
            {
                int minRow = grouping.Min(cell => cell.RowIndex);
                int maxRow = grouping.Max(cell => cell.RowIndex);
                if (grouping.Count() != maxRow - minRow + 1)
                {
                    return false;
                }
                if (minRow == maxRow)
                {
                    return false;
                }
                firstRow = firstRow ?? minRow;
                lastRow = lastRow ?? maxRow;
                if (firstRow != minRow || lastRow != maxRow)
                {
                    return false;
                }
            }
            if (!firstRow.HasValue)
            {
                return false;
            }
            
            var columnIndexes = cellsByColumn.Select(grouping => grouping.Key).ToArray();
            Array.Sort(columnIndexes);
            propertyDescriptors = columnIndexes.Select(colIndex => GetPropertyDescriptor(DataGridView.Columns[colIndex])).ToArray();
            foreach (var propertyDescriptor in propertyDescriptors)
            {
                if (propertyDescriptor == null || propertyDescriptor.IsReadOnly)
                {
                    return false;
                }
            }
            firstRowIndex = firstRow.Value;
            lastRowIndex = lastRow.Value;
            return true;
        }

        private void clearAllFiltersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BindingListSource.RowFilter = RowFilter.Empty;
        }

        private void clearFilterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (null != _columnFilterPropertyDescriptor)
            {
                var rowFilter = BindingListSource.RowFilter;
                rowFilter = rowFilter.SetColumnFilters(
                    rowFilter.ColumnFilters.Where(spec => !Equals(spec.ColumnCaption, _columnFilterPropertyDescriptor.DisplayName)));
                BindingListSource.RowFilter = rowFilter;
            }
        }

        private void filterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (null == _columnFilterPropertyDescriptor)
            {
                return;
            }
            using (var quickFilterForm = new QuickFilterForm()
            {
                ViewContext = BindingListSource.ViewContext
            })
            {
                quickFilterForm.SetFilter(BindingListSource.ViewInfo.DataSchema, _columnFilterPropertyDescriptor, BindingListSource.RowFilter);
                if (FormUtil.ShowDialog(this, quickFilterForm) == DialogResult.OK)
                {
                    BindingListSource.RowFilter = quickFilterForm.RowFilter;
                }
            }
        }

        private void sortAscendingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSortDirection(_columnFilterPropertyDescriptor, ListSortDirection.Ascending);
        }

        private void sortDescendingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSortDirection(_columnFilterPropertyDescriptor, ListSortDirection.Descending);
        }

        public void SetSortDirection(PropertyDescriptor propertyDescriptor, ListSortDirection direction)
        {
            if (null == propertyDescriptor)
            {
                return;
            }
            List<ListSortDescription> sortDescriptions = new List<ListSortDescription>();
            sortDescriptions.Add(new ListSortDescription(propertyDescriptor, direction));
            if (null != BindingListSource.SortDescriptions)
            {
                sortDescriptions.AddRange(
                    BindingListSource.SortDescriptions.OfType<ListSortDescription>()
                        .Where(sortDescription => sortDescription.PropertyDescriptor.Name != propertyDescriptor.Name));
            }
            BindingListSource.ApplySort(new ListSortDescriptionCollection(sortDescriptions.ToArray()));
        }

        private void clearSortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BindingListSource.ApplySort(new ListSortDescriptionCollection());
        }

        /// <summary>
        /// Displays the context menu if the user left-clicks on a column header.
        /// </summary>
        private void boundDataGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex < 0)
            {
                return;
            }
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            var args = new DataGridViewCellContextMenuStripNeededEventArgs(e.ColumnIndex, e.RowIndex);
            boundDataGridView_CellContextMenuStripNeeded(sender, args);
            if (null != args.ContextMenuStrip)
            {
                var rcCell = boundDataGridView.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                args.ContextMenuStrip.Show(boundDataGridView, new Point(rcCell.X + e.X, rcCell.Y + e.Y));
            }
        }

        public PropertyDescriptor GetPropertyDescriptor(DataGridViewColumn column)
        {
            return bindingListSource.GetItemProperties(null)[column.DataPropertyName];
        }

        private void fillDownToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FillDown();
        }

        public bool FillDown()
        {
            PropertyDescriptor[] propertyDescriptors;
            int firstRowIndex;
            int lastRowIndex;
            if (!GetRectangularSelection(out propertyDescriptors, out firstRowIndex, out lastRowIndex))
            {
                return false;
            }
            var skylineWindow = ((SkylineDataSchema) BindingListSource.ViewInfo.DataSchema).SkylineWindow;
            if (null == skylineWindow)
            {
                return false;
            }
            using (var undoTransaction = skylineWindow.BeginUndo(Resources.DataboundGridControl_FillDown_Fill_Down))
            {
                if (DoFillDown(propertyDescriptors, firstRowIndex, lastRowIndex))
                {
                    undoTransaction.Commit();
                    return true;
                }
            }
            return false;
        }

        private bool DoFillDown(PropertyDescriptor[] propertyDescriptors, int firstRowIndex, int lastRowIndex)
        {
            bool anyChanges = false;
            var firstRowValues = propertyDescriptors.Select(pd => pd.GetValue(BindingListSource[firstRowIndex])).ToArray();
            for (int iRow = firstRowIndex + 1; iRow <= lastRowIndex; iRow++)
            {
                var row = BindingListSource[iRow];
                for (int icol = 0; icol < propertyDescriptors.Length; icol++)
                {
                    var propertyDescriptor = propertyDescriptors[icol];
                    try
                    {
                        propertyDescriptor.SetValue(row, firstRowValues[icol]);
                        anyChanges = true;
                    }
                    catch (Exception e)
                    {
                        MessageDlg.Show(this, TextUtil.LineSeparate(Resources.DataboundGridControl_DoFillDown_Error_setting_value_, 
                            e.Message));
                        var column = DataGridView.Columns.OfType<DataGridViewColumn>()
                            .FirstOrDefault(col => col.DataPropertyName == propertyDescriptor.Name);
                        if (null != column)
                        {
                            DataGridView.CurrentCell = DataGridView.Rows[iRow].Cells[column.Index];
                        }
                        return anyChanges;
                    }
                }
            }
            return anyChanges;
        }

        private void contextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            UpdateContextMenuItems();
        }

        private void UpdateContextMenuItems()
        {
            clearAllFiltersToolStripMenuItem.Enabled = !BindingListSource.RowFilter.IsEmpty;
            if (null != _columnFilterPropertyDescriptor)
            {

                clearFilterToolStripMenuItem.Enabled =
                    BindingListSource.RowFilter.ColumnFilters.Any(
                        filter => Equals(_columnFilterPropertyDescriptor.DisplayName, filter.ColumnCaption));
                filterToolStripMenuItem.Enabled = true;
                ListSortDirection? sortDirection = null;
                if (null != BindingListSource.SortDescriptions && BindingListSource.SortDescriptions.Count > 0)
                {
                    var sortDescription = BindingListSource.SortDescriptions.OfType<ListSortDescription>().First();
                    if (sortDescription.PropertyDescriptor.Name == _columnFilterPropertyDescriptor.Name)
                    {
                        sortDirection = sortDescription.SortDirection;
                    }
                    clearSortToolStripMenuItem.Enabled = true;
                }
                else
                {
                    clearSortToolStripMenuItem.Enabled = false;
                }
                sortAscendingToolStripMenuItem.Enabled = true;
                sortDescendingToolStripMenuItem.Enabled = true;
                sortAscendingToolStripMenuItem.Checked = ListSortDirection.Ascending == sortDirection;
                sortDescendingToolStripMenuItem.Checked = ListSortDirection.Descending == sortDirection;
            }
            else
            {
                clearFilterToolStripMenuItem.Enabled = false;
                filterToolStripMenuItem.Enabled = false;
                sortAscendingToolStripMenuItem.Enabled = false;
                sortDescendingToolStripMenuItem.Enabled = false;
                sortAscendingToolStripMenuItem.Checked = false;
                sortDescendingToolStripMenuItem.Checked = false;
            }
            fillDownToolStripMenuItem.Enabled = IsEnableFillDown();
        }

        private void bindingListSource_DataError(object sender, BindingManagerDataErrorEventArgs e)
        {
            lock (_errorMessageLock)
            {
                if (_suppressErrorMessages || _errorMessagePending)
                {
                    return;
                }
                if (IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(() => DisplayError(e)));
                        _errorMessagePending = true;
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                }
            }
        }

        private void DisplayError(BindingManagerDataErrorEventArgs e)
        {
            lock (_errorMessageLock)
            {
                _errorMessagePending = false;
            }
            if (_suppressErrorMessages)
            {
                return;
            }
            string message = TextUtil.LineSeparate(
                Resources.DataboundGridControl_DisplayError_An_error_occured_while_displaying_the_data_rows_,
                e.Exception.Message,
                Resources.DataboundGridControl_DisplayError_Do_you_want_to_continue_to_see_these_error_messages_
                );

            var alertDlg = new AlertDlg(message, MessageBoxButtons.YesNo) {Exception = e.Exception};
            if (alertDlg.ShowAndDispose(this) == DialogResult.No)
            {
                _suppressErrorMessages = true;
            }
        }
    }
}
