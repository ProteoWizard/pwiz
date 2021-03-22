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
using pwiz.Common.Collections;
using pwiz.Common.Controls.Clustering;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding.Clustering;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Layout;
using pwiz.Skyline.Alerts;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Clustering;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

// This code is associated with the DocumentGrid.

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class DataboundGridControl: UserControl
    {
        private DataPropertyDescriptor _columnFilterPropertyDescriptor;
        private readonly object _errorMessageLock = new object();
        private bool _errorMessagePending;
        private bool _suppressErrorMessages;
        private DataGridViewPasteHandler _dataGridViewPasteHandler;

        public DataboundGridControl()
        {
            InitializeComponent();
            _dataGridViewPasteHandler = DataGridViewPasteHandler.Attach(DataGridView);
            NavBar.ClusterSplitButton.Visible = true;
            NavBar.ClusterSplitButton.DropDownItems.Add(new ToolStripMenuItem(Resources.DataboundGridControl_DataboundGridControl_Show_Heat_Map, null,
                heatMapContextMenuItem_Click));
            NavBar.ClusterSplitButton.DropDownItems.Add(new ToolStripMenuItem(Resources.DataboundGridControl_DataboundGridControl_Show_PCA_Plot, null,
                pCAToolStripMenuItem_Click));
        }

        public BindingListSource BindingListSource
        {
            get { return bindingListSource; }
            set
            {
                if (null != BindingListSource)
                {
                    BindingListSource.DataError -= bindingListSource_DataError;
                    BindingListSource.AllRowsChanged -= BindingListSource_OnAllRowsChanged;
                }
                bindingListSource = value;
                if (null != BindingListSource)
                {
                    BindingListSource.DataError += bindingListSource_DataError;
                    BindingListSource.AllRowsChanged += BindingListSource_OnAllRowsChanged;
                }
                NavBar.BindingListSource = bindingListSource;
                boundDataGridView.DataSource = bindingListSource;
            }
        }

        private void BindingListSource_OnAllRowsChanged(object sender, EventArgs e)
        {
            UpdateDendrograms();
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

        private SkylineWindow DataSchemaSkylineWindow
        {
            get
            {
                return (BindingListSource.ViewContext?.DataSchema as SkylineDataSchema)?.SkylineWindow;
            }
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            ClipboardControlGotLostFocus(true);
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            ClipboardControlGotLostFocus(false);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            ClipboardControlGotLostFocus(false);
        }

        protected void ClipboardControlGotLostFocus(bool gettingFocus)
        {
            var skylineWindow = FindParentSkylineWindow();
            if (skylineWindow != null)
            {
                if (gettingFocus)
                {
                    skylineWindow.ClipboardControlGotFocus(this);
                }
                else
                {
                    skylineWindow.ClipboardControlLostFocus(this);
                }
            }
        }

        #region Methods exposed for testing
        public BoundDataGridViewEx DataGridView { get { return boundDataGridView; } }
        public NavBar NavBar { get { return navBar; } }

        public DataGridViewColumn FindColumn(PropertyPath propertyPath)
        {
            // Get the list separately for debugging, since this helps in figuring out what
            // the propertyPath should be.
            var propertyDescriptorList = BindingListSource.GetItemProperties(null)
                .OfType<ColumnPropertyDescriptor>();
            var propertyDescriptor = propertyDescriptorList
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
                if (!BindingListSource.IsComplete)
                {
                    return false;
                }

                var skylineWindow = DataSchemaSkylineWindow;
                if (skylineWindow == null)
                {
                    return false;
                }

                bool complete = false;
                if (skylineWindow.InvokeRequired)
                {
                    skylineWindow.Invoke(new Action(() =>
                    {
                        complete = ReferenceEquals(skylineWindow.DocumentUI, skylineWindow.Document);
                    }));
                }
                else
                {
                    complete = ReferenceEquals(skylineWindow.DocumentUI, skylineWindow.Document);
                }

                return complete;
            }
        }

        public ViewName? GetViewName()
        {
            return BindingListSource?.ViewInfo?.ViewGroup?.Id.ViewName(BindingListSource.ViewInfo.Name);
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
            throw new InvalidOperationException(string.Format(@"No view named {0}", viewName));
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
            _columnFilterPropertyDescriptor = BindingListSource.FindDataProperty(column.DataPropertyName);
            filterToolStripMenuItem_Click(filterToolStripMenuItem, new EventArgs());
        }

        public void ShowFormatDialog(DataGridViewColumn column)
        {
            _columnFilterPropertyDescriptor = BindingListSource.FindDataProperty(column.DataPropertyName);
            formatToolStripMenuItem_Click(formatToolStripMenuItem, new EventArgs());
        }

        #endregion

        protected virtual void boundDataGridView_CellContextMenuStripNeeded(object sender, DataGridViewCellContextMenuStripNeededEventArgs e)
        {
            DataPropertyDescriptor propertyDescriptor = null;
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
                var columnId = ColumnId.GetColumnId(_columnFilterPropertyDescriptor);
                var rowFilter = BindingListSource.RowFilter;
                rowFilter = rowFilter.SetColumnFilters(
                    rowFilter.ColumnFilters.Where(spec => !Equals(spec.ColumnId, columnId)));
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

        public DataPropertyDescriptor GetPropertyDescriptor(DataGridViewColumn column)
        {
            return bindingListSource.FindDataProperty(column.DataPropertyName);
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
            var skylineWindow = DataSchemaSkylineWindow;
            if (null == skylineWindow)
            {
                return false;
            }

            _dataGridViewPasteHandler.PerformUndoableOperation(Resources.DataboundGridControl_FillDown_Fill_Down,
                longWaitBroker => DoFillDown(longWaitBroker, propertyDescriptors, firstRowIndex, lastRowIndex),
                new DataGridViewPasteHandler.BatchModifyInfo(DataGridViewPasteHandler.BatchModifyAction.FillDown,
                    BindingListSource.ViewInfo.Name, BindingListSource.RowFilter));
            return false;
        }
        
        private bool DoFillDown(ILongWaitBroker longWaitBroker, PropertyDescriptor[] propertyDescriptors, int firstRowIndex, int lastRowIndex)
        {
            bool anyChanges = false;
            var firstRowValues = propertyDescriptors.Select(pd => pd.GetValue(BindingListSource[firstRowIndex])).ToArray();
            int totalRows = lastRowIndex - firstRowIndex + 1;
            for (int iRow = firstRowIndex + 1; iRow <= lastRowIndex; iRow++)
            {
                if (longWaitBroker.IsCanceled)
                {
                    return anyChanges;
                }
                longWaitBroker.ProgressValue = 100*(iRow - firstRowIndex)/totalRows;
                longWaitBroker.Message = string.Format(Resources.DataboundGridControl_DoFillDown_Filling__0___1__rows, iRow - firstRowIndex, totalRows);
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
                        MessageDlg.ShowWithException(this, TextUtil.LineSeparate(Resources.DataboundGridControl_DoFillDown_Error_setting_value_, 
                            e.Message), e);
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

        private bool IsSortable(PropertyDescriptor propertyDescriptor)
        {
            return propertyDescriptor != null && !propertyDescriptor.Attributes.OfType<ExpensiveAttribute>().Any();
        }

        private bool IsFormattable(PropertyDescriptor propertyDescriptor)
        {
            if (propertyDescriptor == null)
            {
                return false;
            }
            var type = BindingListSource.ViewInfo.DataSchema.GetWrappedValueType(propertyDescriptor.PropertyType);
            if (typeof(IFormattable).IsAssignableFrom(type))
            {
                return true;
            }
            return false;
        }

        private void UpdateContextMenuItems()
        {
            clearAllFiltersToolStripMenuItem.Enabled = !BindingListSource.RowFilter.IsEmpty;
            if (null != _columnFilterPropertyDescriptor && IsSortable(_columnFilterPropertyDescriptor))
            {
                var columnId = ColumnId.GetColumnId(_columnFilterPropertyDescriptor);
                clearFilterToolStripMenuItem.Enabled =
                    BindingListSource.RowFilter.ColumnFilters.Any(
                        filter => Equals(columnId, filter.ColumnId));
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
                clearSortToolStripMenuItem.Enabled = false;
                clearFilterToolStripMenuItem.Enabled = false;
                filterToolStripMenuItem.Enabled = false;
                sortAscendingToolStripMenuItem.Enabled = false;
                sortDescendingToolStripMenuItem.Enabled = false;
                sortAscendingToolStripMenuItem.Checked = false;
                sortDescendingToolStripMenuItem.Checked = false;
            }
            if (IsFormattable(_columnFilterPropertyDescriptor))
            {
                formatToolStripMenuItem.Enabled = true;
            }
            else
            {
                formatToolStripMenuItem.Enabled = false;
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
            try
            {
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
            finally
            {
                lock (_errorMessageLock)
                {
                    _errorMessagePending = false;
                }
            }
        }

        private void formatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_columnFilterPropertyDescriptor == null)
            {
                return;
            }
            using (var dlg = new ChooseFormatDlg(BindingListSource.ViewInfo.DataSchema.DataSchemaLocalizer))
            {
                var columnId = ColumnId.GetColumnId(_columnFilterPropertyDescriptor);
                var columnFormat =
                    BindingListSource.ColumnFormats.GetFormat(ColumnId.GetColumnId(_columnFilterPropertyDescriptor));
                if (null != columnFormat.Format)
                {
                    dlg.FormatText = columnFormat.Format;
                }
                if (dlg.ShowDialog(FormUtil.FindTopLevelOwner(this)) == DialogResult.OK)
                {
                    if (string.IsNullOrEmpty(dlg.FormatText))
                    {
                        columnFormat = columnFormat.ChangeFormat(null);
                    }
                    else
                    {
                        columnFormat = columnFormat.ChangeFormat(dlg.FormatText);
                    }
                    BindingListSource.ColumnFormats.SetFormat(columnId, columnFormat);
                }
            }
        }

        private void heatMapContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowHeatMap();
        }

        public ClusterInput CreateClusterInput()
        {
            return new ClusterInput(BindingListSource.ViewInfo.DataSchema, BindingListSource.ReportResults,
                BindingListSource.ClusteringSpec, DataGridView.ReportColorScheme);
        }

        public bool ShowHeatMap()
        {
            var formGroup = FormGroup.FromControl(this);
            var heatMap = formGroup.SiblingForms.OfType<HeatMapGraph>().FirstOrDefault();
            if (heatMap != null)
            {
                heatMap.OwnerGridForm = DataboundGridForm;
                heatMap.RefreshData();
                heatMap.Activate();
                return true;
            }
            heatMap = new HeatMapGraph
            {
                SkylineWindow = DataSchemaSkylineWindow,
                OwnerGridForm = DataboundGridForm,
            };
            heatMap.RefreshData();

            formGroup.ShowSibling(heatMap);
            return true;
        }

        private void boundDataGridView_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            UpdateDendrograms();
        }

        public void UpdateDendrograms()
        {
            var reportResults = BindingListSource.ReportResults as ClusteredReportResults ?? ClusteredReportResults.EMPTY;
            var colorScheme = boundDataGridView.ReportColorScheme;
            UpdateColumnDendrograms(reportResults.ClusteredProperties, colorScheme,
                reportResults.ColumnGroupDendrogramDatas?.Select(d => d?.DendrogramData).ToList());
            if (reportResults?.RowDendrogramData == null || DataGridView.RowCount == 0)
            {
                splitContainerVertical.Panel1Collapsed = true;
            }
            else
            {
                splitContainerVertical.Panel1Collapsed = false;
                int dendrogramTop = 0;
                if (!splitContainerHorizontal.Panel1Collapsed)
                {
                    dendrogramTop += splitContainerHorizontal.Panel1.Height;
                }
                if (DataGridView.ColumnHeadersVisible)
                {
                    dendrogramTop += DataGridView.ColumnHeadersHeight;
                }
                rowDendrogram.Bounds = new Rectangle(0, dendrogramTop, splitContainerVertical.Panel1.Width,
                    splitContainerVertical.Panel1.Height - dendrogramTop);
                var firstDisplayedCell = DataGridView.FirstDisplayedCell;
                var rowHeight = firstDisplayedCell?.Size.Height ?? DataGridView.Rows[0].Height;
                int firstDisplayedRowIndex = firstDisplayedCell?.RowIndex ?? 0;
                var firstLocation = 3.5;
                var rowLocations = ImmutableList.ValueOf(Enumerable.Range(0, reportResults.RowCount).Select(rowIndex =>
                        (rowIndex - firstDisplayedRowIndex) * rowHeight + firstLocation))
                    .Select(d => new KeyValuePair<double, double>(d, d + rowHeight));
                IEnumerable<IEnumerable<Color>> rowColors = null;
                if (colorScheme != null)
                {
                    rowColors = ImmutableList.ValueOf(reportResults.RowItems.Select(rowItem => colorScheme.GetRowColors(rowItem).Select(c=>c?? Color.Transparent)));
                }
                rowDendrogram.SetDendrogramDatas(new[]
                {
                    new DendrogramFormat(reportResults.RowDendrogramData.DendrogramData, rowLocations, rowColors)
                });
            }
        }

        public void UpdateColumnDendrograms(ClusteredProperties clusteredProperties, ReportColorScheme reportColorScheme, IList<DendrogramData> columnDendrogramDatas)
        {
            var pivotedProperties = clusteredProperties.PivotedProperties;
            if (columnDendrogramDatas == null || columnDendrogramDatas.All(data=>null == data))
            {
                splitContainerHorizontal.Panel1Collapsed = true;
                return;
            }
            List<KeyValuePair<double, double>> columnLocations = new List<KeyValuePair<double, double>>();
            Dictionary<string, int> nameToIndex = new Dictionary<string, int>();
            double currentPosition = 0 - DataGridView.HorizontalScrollingOffset;
            if (DataGridView.RowHeadersVisible)
            {
                currentPosition += DataGridView.RowHeadersWidth;
            }
            foreach (var column in DataGridView.Columns.Cast<DataGridViewColumn>())
            {
                if (!string.IsNullOrEmpty(column.DataPropertyName))
                {
                    nameToIndex[column.DataPropertyName] = columnLocations.Count;
                }

                double width = column.Visible ? column.Width : 0;
                columnLocations.Add(new KeyValuePair<double, double>(currentPosition, currentPosition + width));
                currentPosition += width;
            }

            if (columnLocations.Count == 0)
            {
                return;
            }
            var datas = new List<DendrogramFormat>();
            for (int i = 0; i < columnDendrogramDatas.Count; i++)
            {
                var dendrogramData = columnDendrogramDatas[i];
                if (dendrogramData == null)
                {
                    continue;
                }

                var seriesGroup = pivotedProperties.SeriesGroups[i];
                if (seriesGroup == null)
                {
                    continue;
                }
                KeyValuePair<double, double> lastLocation = columnLocations[0];
                List<KeyValuePair<double, double>> leafLocatons = new List<KeyValuePair<double, double>>(seriesGroup.PivotKeys.Count);
                List<List<Color>> leafColors = new List<List<Color>>();
                for (int iPivotKey = 0; iPivotKey < seriesGroup.PivotKeys.Count; iPivotKey++)
                {
                    var colors = new List<Color>();
                    var locs = new List<KeyValuePair<double, double>>();
                    foreach (var series in seriesGroup.SeriesList)
                    {
                        var propertyDescriptor = series.PropertyDescriptors[iPivotKey];
                        int columnIndex;
                        if (nameToIndex.TryGetValue(propertyDescriptor.Name, out columnIndex))
                        {
                            locs.Add(columnLocations[columnIndex]);
                        }

                        if (reportColorScheme != null)
                        {
                            if (clusteredProperties.GetColumnRole(series) == ClusterRole.COLUMNHEADER)
                            {
                                colors.Add(reportColorScheme.GetColumnColor(propertyDescriptor) ?? Color.Transparent);
                            }
                        }
                    }

                    if (locs.Count != 0)
                    {
                        lastLocation = new KeyValuePair<double, double>(locs.Min(kvp=>kvp.Key), locs.Max(kvp=>kvp.Value));
                    }
                    leafLocatons.Add(lastLocation);
                    leafColors.Add(colors);
                }
                datas.Add(new DendrogramFormat(dendrogramData, ImmutableList.ValueOf(leafLocatons), leafColors));
            }
            columnDendrogram.SetDendrogramDatas(datas);
            splitContainerHorizontal.Panel1Collapsed = false;
        }

        private void boundDataGridView_Resize(object sender, EventArgs e)
        {
            UpdateDendrograms();
        }

        private void boundDataGridView_Scroll(object sender, ScrollEventArgs e)
        {
            UpdateDendrograms();
        }

        private void bindingListSource_BindingComplete(object sender, BindingCompleteEventArgs e)
        {
            UpdateDendrograms();
        }

        private void bindingListSource_ListChanged(object sender, ListChangedEventArgs e)
        {
            UpdateDendrograms();
        }

        private void pCAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowPcaPlot();
        }

        public IDataboundGridForm DataboundGridForm
        {
            get
            {
                return (ParentForm as IDataboundGridForm);
            }
        }

        public void ShowPcaPlot()
        {
            var formGroup = FormGroup.FromControl(this);
            var pcaPlot = formGroup.SiblingForms.OfType<PcaPlot>()
                .FirstOrDefault(form => ReferenceEquals(form.DataboundGridControl, this));
            if (pcaPlot != null)
            {
                pcaPlot.OwnerGridForm = DataboundGridForm;
                pcaPlot.ClusterInput = CreateClusterInput();
                pcaPlot.Activate();
                return;
            }
            pcaPlot = new PcaPlot
            {
                SkylineWindow = DataSchemaSkylineWindow,
                OwnerGridForm = DataboundGridForm,
                ClusterInput = CreateClusterInput()
            };
            formGroup.ShowSibling(pcaPlot);
            pcaPlot.ClusterInput = CreateClusterInput();
        }
    }
}
