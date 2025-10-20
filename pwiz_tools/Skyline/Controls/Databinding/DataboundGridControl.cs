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
using pwiz.Skyline.Model.Databinding.Collections;
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
        private DataGridViewPasteHandler _boundDataGridViewPasteHandler;
        private bool _inColumnChange;
        private IViewContext _viewContext;
        private ReplicatePivotColumns _replicatePivotColumns;
        private Dictionary<PropertyPath, DataGridViewRow> _replicateGridRows;
        private Dictionary<ResultKey, DataGridViewColumn> _replicateGridColumns;
        private static readonly double _defaultDataGridSplitterRatio = .33;
        private static readonly int _defaultFrozenColumnMax = 3;

        public DataboundGridControl()
        {
            InitializeComponent();
            _boundDataGridViewPasteHandler = DataGridViewPasteHandler.Attach(DataGridView, bindingListSource);
            DataGridViewPasteHandler.Attach(ReplicatePivotDataGridView, bindingListSource);
            replicatePivotDataGridView.CellValueChanged += replicatePivotDataGridView_CellValueChanged;
            NavBar.ClusterSplitButton.Visible = true;
            NavBar.ClusterSplitButton.DropDownItems.Add(new ToolStripMenuItem(DatabindingResources.DataboundGridControl_DataboundGridControl_Show_Heat_Map, null,
                heatMapContextMenuItem_Click));
            NavBar.ClusterSplitButton.DropDownItems.Add(new ToolStripMenuItem(DatabindingResources.DataboundGridControl_DataboundGridControl_Show_PCA_Plot, null,
                pCAToolStripMenuItem_Click));

            // Attach the event handlers for the BindingListSource
            BindingListSource = bindingListSource;
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

        public IViewContext ViewContext
        {
            get
            {
                return _viewContext;
            }
            set
            {
                if (ReferenceEquals(ViewContext, value))
                {
                    return;
                }

                if (null != ViewContext)
                {
                    replicatePivotDataGridView.DataError -= ViewContext.OnDataError;
                }

                _viewContext = value;
                if (null != ViewContext)
                {
                    replicatePivotDataGridView.DataError += ViewContext.OnDataError;
                }
            }
        }

        private void BindingListSource_OnAllRowsChanged(object sender, EventArgs e)
        {
            MainGridPopulated();
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

                var skylineDataSchema = BindingListSource.ViewContext?.DataSchema as SkylineDataSchema;
                return true == skylineDataSchema?.IsDocumentUpToDate();
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
            return GetRectangularSelection(out _, out _, out _);
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

            _boundDataGridViewPasteHandler.PerformUndoableOperation(DatabindingResources.DataboundGridControl_FillDown_Fill_Down,
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
                longWaitBroker.Message = string.Format(DatabindingResources.DataboundGridControl_DoFillDown_Filling__0___1__rows, iRow - firstRowIndex, totalRows);
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
                        MessageDlg.ShowWithException(this, TextUtil.LineSeparate(DatabindingResources.DataboundGridControl_DoFillDown_Error_setting_value_, 
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
                    DatabindingResources.DataboundGridControl_DisplayError_An_error_occured_while_displaying_the_data_rows_,
                    e.Exception.Message,
                    DatabindingResources.DataboundGridControl_DisplayError_Do_you_want_to_continue_to_see_these_error_messages_
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
            MainGridPopulated();
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
                if (!dataGridSplitContainer.Panel1Collapsed)
                {
                    dendrogramTop += dataGridSplitContainer.Panel1.Height;
                    dendrogramTop += dataGridSplitContainer.SplitterWidth;
                }
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
            var frozenColumnWidth = DataGridView.Columns.Cast<DataGridViewColumn>()
                .Where(col => col.Frozen && col.Visible).Sum(col => col.Width);
            if (DataGridView.RowHeadersVisible)
            {
                currentPosition += DataGridView.RowHeadersWidth;
                frozenColumnWidth += DataGridView.RowHeadersWidth;
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
            columnDendrogramClipPanel.Bounds = new Rectangle(frozenColumnWidth, 0, Math.Max(0, splitContainerHorizontal.Panel1.Width - frozenColumnWidth),
                splitContainerHorizontal.Panel1.Height);
            columnDendrogram.Bounds = new Rectangle(-frozenColumnWidth, 0, splitContainerHorizontal.Panel1.Width, splitContainerHorizontal.Panel1.Height);
        }

        private void AlignPivotByReplicateGridColumns(ReplicatePivotColumns replicatePivotColumns)
        {
            var replicateTotalWidthMap = GetMainGridReplicateColumnWidths(replicatePivotColumns);
            var replicateColumnNames = replicatePivotColumns.GetReplicateColumnGroups()
                .SelectMany(group => group.Select(col => col.Name)).ToHashSet();

            // If columns are frozen we size the property column based on visible width
            var isMainGridFrozen = IsMainGridFrozen();
            var nonReplicateWidth = boundDataGridView.Columns.Cast<DataGridViewColumn>()
                .Where(col => !replicateColumnNames.Contains(col.DataPropertyName) && col.Visible)
                .Sum(col =>
                {
                    if (isMainGridFrozen)
                    {
                        return CalculateColumnVisibleWidth(boundDataGridView, col);
                    }
                    else
                    {
                        return col.Width;
                    }
                });

            // We need to consider a special case where the main grid vertical scroll bar width needs to be accounted for.
            var mainGridVScrollBar = FindVisibleVerticalScrollBar(boundDataGridView);
            var replicateGridVScrollBar = FindVisibleVerticalScrollBar(replicatePivotDataGridView);
            if (isMainGridFrozen && mainGridVScrollBar != null && replicateGridVScrollBar == null)
            {
                // If there is no visible replicate columns then the VScrollBar width should be added to the non-replicate width.  
                var visibleReplicateWidth = boundDataGridView.Columns.Cast<DataGridViewColumn>()
                    .Where(col => replicateColumnNames.Contains(col.DataPropertyName) && col.Visible)
                    .Sum(col => CalculateColumnVisibleWidth(boundDataGridView, col));
                if (visibleReplicateWidth <= 0)
                {
                    nonReplicateWidth += mainGridVScrollBar.Width;
                }
            }

            colReplicateProperty.Width = nonReplicateWidth;
            foreach (var entry in _replicateGridColumns)
            {
                if (replicateTotalWidthMap.TryGetValue(entry.Key, out var replicateTotalWidth))
                {
                    entry.Value.Width = replicateTotalWidth;
                }
            }
        }

        private static VScrollBar FindVisibleVerticalScrollBar(DataGridView dataGridView)
        {
            foreach (Control control in dataGridView.Controls)
            {
                if (control is VScrollBar { Visible: true } vScrollBar)
                {
                    return vScrollBar;
                }
            }
            return null;
        }

        private int CalculateColumnVisibleWidth(DataGridView view, DataGridViewColumn column)
        {
            Rectangle columnRect = view.GetColumnDisplayRectangle(column.Index, false);
            var visibleLeft = Math.Max(columnRect.Left, view.DisplayRectangle.Left);
            var visibleRight = Math.Min(columnRect.Right, view.DisplayRectangle.Right);
            var visibleWidth = Math.Max(0, visibleRight - visibleLeft);
            return visibleWidth;
        }

        private void AlignDataBoundGridColumns(ReplicatePivotColumns replicatePivotColumns)
        {
            var replicateTotalWidthMap = GetMainGridReplicateColumnWidths(replicatePivotColumns);
            var boundColumns = boundDataGridView.Columns.OfType<DataGridViewColumn>()
                .Where(col => col.Visible)
                .ToDictionary(col => col.DataPropertyName);
            foreach (var grouping in replicatePivotColumns.GetReplicateColumnGroups())
            {
                var replicateColumnsRounder = new AdjustingRounder();
                DataGridViewColumn lastColumn = null;
                foreach (var column in grouping)
                {
                    if (replicateTotalWidthMap.TryGetValue(grouping.Key, out var replicateTotalWidth)
                        && boundColumns.TryGetValue(column.Name, out var dataGridViewColumn))
                    {
                        var columnRatio = (double)dataGridViewColumn.Width / replicateTotalWidth;
                        var widthToAdd = (int)replicateColumnsRounder.Round(_replicateGridColumns[grouping.Key].Width * columnRatio);
                        dataGridViewColumn.Width = widthToAdd;
                        lastColumn = dataGridViewColumn;
                    }
                }
                if (lastColumn != null)
                {
                    lastColumn.Width -= replicateColumnsRounder.RoundRemainder();
                }
            }

            if (IsMainGridFrozen())
            {
                AlignFrozenDataBoundGridPropertyColumns(replicatePivotColumns);
            }
            else
            {
                AlignDataBoundGridPropertyColumns(replicatePivotColumns);
            }
        }

        private void AlignFrozenDataBoundGridPropertyColumns(ReplicatePivotColumns replicatePivotColumns)
        {
            // Resizing of non-replicate columns behaves differently if columns are frozen.
            // Only frozen columns are resized and non-froze columns are shown or hidden by scrolling.
            // The calculations in this scenario rely on visible widths as only some columns might be frozen.
            var replicateColumnNames = replicatePivotColumns.GetReplicateColumnGroups()
                .SelectMany(group => group.Select(col => col.Name)).ToHashSet();

            var mainGridNonReplicateVisibleWidth = boundDataGridView.Columns.Cast<DataGridViewColumn>()
                .Where(col => !replicateColumnNames.Contains(col.DataPropertyName)).Sum(col => CalculateColumnVisibleWidth(boundDataGridView, col));
            var mainGridNonReplicateNonFrozenVisibleWidth = boundDataGridView.Columns.Cast<DataGridViewColumn>()
                .Where(col => !replicateColumnNames.Contains(col.DataPropertyName) && !col.Frozen).Sum(col => CalculateColumnVisibleWidth(boundDataGridView, col));
            var mainGridNonReplicateNonFrozenWidth = boundDataGridView.Columns.Cast<DataGridViewColumn>()
                .Where(col => !replicateColumnNames.Contains(col.DataPropertyName) && !col.Frozen).Sum(col => col.Width);


            // Before updating the width of columns we check if it is possible to scroll based
            // on whether some of the non-frozen property columns can be scrolled to be 
            // hidden or shown more.
            var propertyWidthToAdd = colReplicateProperty.Width - mainGridNonReplicateVisibleWidth;
            var nonReplicateNonFrozenHiddenWidth = mainGridNonReplicateNonFrozenWidth - mainGridNonReplicateNonFrozenVisibleWidth;
            int scrollOffset = 0;
            if (propertyWidthToAdd < 0 && mainGridNonReplicateNonFrozenVisibleWidth > 0)
            {
                scrollOffset = Math.Max(propertyWidthToAdd, -mainGridNonReplicateNonFrozenVisibleWidth);
            }
            else if (propertyWidthToAdd > 0 && nonReplicateNonFrozenHiddenWidth > 0)
            {
                scrollOffset = Math.Min(propertyWidthToAdd, nonReplicateNonFrozenHiddenWidth);
            }

            if (scrollOffset != 0)
            {
                scrollOffset = Math.Min(scrollOffset, boundDataGridView.HorizontalScrollingOffset);
                boundDataGridView.HorizontalScrollingOffset -= scrollOffset;
                UpdateReplicateDataGridScrollPosition(boundDataGridView.HorizontalScrollingOffset);
                propertyWidthToAdd -= scrollOffset;
            }

            var propertyColumnRounder = new AdjustingRounder();
            DataGridViewColumn lastPropertyColumn = null;
            foreach (DataGridViewColumn dataGridViewColumn in boundDataGridView.Columns)
            {
                if (!replicateColumnNames.Contains(dataGridViewColumn.DataPropertyName))
                {
                        // Only resize columns which are not frozen
                        if (!dataGridViewColumn.Frozen)
                        {
                            continue;
                        }

                        var visibleWidth = CalculateColumnVisibleWidth(boundDataGridView, dataGridViewColumn);
                        if (visibleWidth > 0)
                        {
                            // Ratio denominator only counts for frozen columns
                            var columnRatio = (double)visibleWidth / (mainGridNonReplicateVisibleWidth - mainGridNonReplicateNonFrozenVisibleWidth);
                            dataGridViewColumn.Width += (int)propertyColumnRounder.Round(propertyWidthToAdd * columnRatio);
                            lastPropertyColumn = dataGridViewColumn;
                        }
                }
            }
            if (lastPropertyColumn != null)
            {
                lastPropertyColumn.Width -= propertyColumnRounder.RoundRemainder();
            }
        }

        private void AlignDataBoundGridPropertyColumns(ReplicatePivotColumns replicatePivotColumns)
        {
            var replicateColumnNames = replicatePivotColumns.GetReplicateColumnGroups()
                .SelectMany(group => group.Select(col => col.Name)).ToHashSet();
            var mainGridNonReplicateWidth = boundDataGridView.Columns.Cast<DataGridViewColumn>()
                .Where(col => !replicateColumnNames.Contains(col.DataPropertyName)).Sum(col => col.Width);

            var propertyColumnRounder = new AdjustingRounder();
            DataGridViewColumn lastPropertyColumn = null;
            foreach (DataGridViewColumn dataGridViewColumn in boundDataGridView.Columns)
            {
                if (!replicateColumnNames.Contains(dataGridViewColumn.DataPropertyName))
                {
                    var propertyWidth = colReplicateProperty.Width;
                    var columnRatio = (double)dataGridViewColumn.Width / mainGridNonReplicateWidth;
                    dataGridViewColumn.Width = (int)propertyColumnRounder.Round(propertyWidth * columnRatio);
                    lastPropertyColumn = dataGridViewColumn;
                }
            }
            if (lastPropertyColumn != null)
            {
                lastPropertyColumn.Width -= propertyColumnRounder.RoundRemainder();
            }
        }

        private Dictionary<ResultKey, int> GetMainGridReplicateColumnWidths(ReplicatePivotColumns replicatePivotColumns)
        {
            var boundColumns = boundDataGridView.Columns.OfType<DataGridViewColumn>()
                .Where(col => col.Visible)
                .ToDictionary(col => col.DataPropertyName);
            var replicateTotalWidthMap = replicatePivotColumns.GetReplicateColumnGroups()
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Where(column => boundColumns.ContainsKey(column.Name)).Sum(column => boundColumns[column.Name].Width));
            return replicateTotalWidthMap;
        }

        private void UpdateReplicateDataGridFrozenState()
        {
            if (IsMainGridFrozen())
            {
                colReplicateProperty.Frozen = true;
            }
            else
            {
                colReplicateProperty.Frozen = false;
            }
        }

        private bool IsMainGridFrozen()
        {
            return DataGridView.Columns.Cast<DataGridViewColumn>().Any(column => column.Frozen);
        }

        private void UpdateReplicateDataGridScrollPosition(int scrollPosition)
        {
            // The pivot by replicate grid can resize the property column depending on frozen state.
            // For this reason we have special logic to calculate the scroll offset based on the width offset
            var replicatePivotColumns = ReplicatePivotColumns.FromItemProperties(bindingListSource.ItemProperties);
            var replicateColumnNames = replicatePivotColumns.GetReplicateColumnGroups()
                .SelectMany(group => group.Select(col => col.Name)).ToHashSet();
            var nonReplicateColumnsWidth = boundDataGridView.Columns.Cast<DataGridViewColumn>()
                .Where(col => !replicateColumnNames.Contains(col.DataPropertyName) && col.Visible)
                .Sum(col => col.Width);
            var columnWidthOffset = nonReplicateColumnsWidth - colReplicateProperty.Width;
            var offsetScrollPosition = scrollPosition - columnWidthOffset;
            replicatePivotDataGridView.HorizontalScrollingOffset = (offsetScrollPosition > 0) ? offsetScrollPosition : 0;
        }

        private void PopulateReplicateDataGridView(ReplicatePivotColumns replicatePivotColumns)
        {
            var boundColumns = boundDataGridView.Columns.OfType<DataGridViewColumn>()
                .ToDictionary(col => col.DataPropertyName);
            UpdateReplicateGridRowsAndColumns(replicatePivotColumns, boundColumns);
            // Iterate through item properties to create columns and rows
            foreach (var group in replicatePivotColumns.GetReplicateColumnGroups())
            {
                if (!_replicateGridColumns.TryGetValue(group.Key, out var dataGridViewColumn))
                {
                    continue;
                }

                foreach (var column in group)
                {
                    if (!replicatePivotColumns.IsConstantColumn(column))
                    {
                        continue;
                    }

                    if (!boundColumns.TryGetValue(column.Name, out var boundColumn))
                    {
                        continue;
                    }

                    var propertyPath = column.DisplayColumn.PropertyPath;
                    if (!_replicateGridRows.TryGetValue(propertyPath, out var dataGridViewRow))
                    {
                        continue;
                    }

                    var cell = dataGridViewRow.Cells[dataGridViewColumn.Index];
                    cell.Value = replicatePivotColumns.GetConstantColumnValue(column);
                    if (replicatePivotColumns.IsConstantColumnReadOnly(column))
                    {
                        cell.ReadOnly = true;
                        cell.Style.BackColor = AbstractViewContext.DefaultReadOnlyCellColor;
                    }
                    else
                    {
                        cell.ReadOnly = boundColumn.ReadOnly;
                        cell.Style.BackColor = boundColumn.DefaultCellStyle.BackColor;
                    }
                }
            }
        }

        private void UpdateReplicateGridRowsAndColumns(ReplicatePivotColumns replicatePivotColumns, Dictionary<string, DataGridViewColumn> boundColumns)
        {
            if (Equals(replicatePivotColumns.ItemProperties, _replicatePivotColumns?.ItemProperties))
            {
                return;
            }
            replicatePivotDataGridView.Columns.Clear();
            replicatePivotDataGridView.Rows.Clear();
            _replicatePivotColumns = replicatePivotColumns;
            _replicateGridRows = new Dictionary<PropertyPath, DataGridViewRow>();
            _replicateGridColumns = new Dictionary<ResultKey, DataGridViewColumn>();
            // Add a column for property names
            replicatePivotDataGridView.Columns.Add(colReplicateProperty);

            foreach (var group in replicatePivotColumns.GetReplicateColumnGroups())
            {
                // Add a column for the replicate name if it doesn't already exist
                if (!_replicateGridColumns.TryGetValue(group.Key, out var dataGridViewColumn))
                {
                    dataGridViewColumn = new DataGridViewTextBoxColumn
                    {
                        HeaderText = group.Key.ToString(),
                        Name = group.Key.ToString(),
                        SortMode = DataGridViewColumnSortMode.NotSortable
                    };
                    replicatePivotDataGridView.Columns.Add(dataGridViewColumn);
                    _replicateGridColumns.Add(group.Key, dataGridViewColumn);
                }

                foreach (var column in group)
                {
                    if (!replicatePivotColumns.IsConstantColumn(column) || !boundColumns.TryGetValue(column.Name, out var mainGridColumn))
                    {
                        continue;
                    }

                    var propertyPath = column.DisplayColumn.PropertyPath;
                    if (!_replicateGridRows.TryGetValue(propertyPath, out var dataGridViewRow))
                    {
                        dataGridViewRow = replicatePivotDataGridView.Rows[replicatePivotDataGridView.Rows.Add()];
                        dataGridViewRow.Cells[colReplicateProperty.Index].Value = column.DisplayColumn.ColumnDescriptor?.GetColumnCaption(ColumnCaptionType.localized);
                        _replicateGridRows.Add(propertyPath, dataGridViewRow);
                    }

                    var cell = (DataGridViewCell)mainGridColumn.CellTemplate.Clone();
                    if (cell != null)
                    {
                        dataGridViewRow.Cells[dataGridViewColumn.Index] = cell;
                        cell.Style = mainGridColumn.DefaultCellStyle.Clone();
                        cell.ReadOnly = column.IsReadOnly;
                        cell.ValueType = column.PropertyType;
                    }
                }
            }

        }

        private void UpdateMainGridDefaultFrozenColumn()
        {
            var visibleColumns = boundDataGridView.Columns
                .Cast<DataGridViewColumn>()
                .Where(col => col.Visible)
                .ToList();

            var startingIndex = Math.Min(visibleColumns.Count, _defaultFrozenColumnMax - 1);
            for (var i = startingIndex; i >= 0; i--)
            {
                var column = visibleColumns[i];
                if (i == 0 || column is DataGridViewLinkColumn)
                {
                    BindingListSource.ColumnFormats.DefaultFrozenColumnCount = i + 1;
                    break;
                }
            }
        }

        private void InitializeDataGridSplitterDistance()
        {
            // Size splitter distance to the default ratio or up to a row which could fill the space.
            var replicateGridMaxHeight = dataGridSplitContainer.Height * _defaultDataGridSplitterRatio;
            var replicateGridHeight = replicatePivotDataGridView.ColumnHeadersHeight;
            for (var i = 0; i < replicatePivotDataGridView.Rows.Count && replicateGridHeight + replicatePivotDataGridView.Rows[i].Height < replicateGridMaxHeight; i++)
            {
                replicateGridHeight += replicatePivotDataGridView.Rows[i].Height;
            }
            dataGridSplitContainer.SplitterDistance = replicateGridHeight;

            AdjustDataGridSplitterSizing();
        }

        private void AdjustDataGridSplitterSizing()
        {
            // Adjust splitter to not allow it to extend past the replicate grid height.
            var replicateGridTableHeight = replicatePivotDataGridView.ColumnHeadersHeight + replicatePivotDataGridView.Rows.Cast<DataGridViewRow>().Sum(row => row.Height);
            var splitterDistance = Math.Min(replicateGridTableHeight, dataGridSplitContainer.SplitterDistance);
            dataGridSplitContainer.SplitterDistance = splitterDistance;

            // Resize elements as we have potentially adjusted the splitter manually.
            dataGridSplitContainer.SplitterWidth = 2;
            boundDataGridView.Height = dataGridSplitContainer.Height - dataGridSplitContainer.SplitterDistance - dataGridSplitContainer.SplitterWidth;
            replicatePivotDataGridView.Height = replicateGridTableHeight;
            
            // Only enable the replicate grid scrollbar if necessary. 
            replicatePivotDataGridView.ScrollBars = replicateGridTableHeight > replicatePivotDataGridView.Height ? ScrollBars.Vertical : ScrollBars.None;
        }

        private void replicatePivotDataGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_inColumnChange || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var cellValue = replicatePivotDataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            var rowPropertyPath = _replicateGridRows.FirstOrDefault(kvp => kvp.Value.Index == e.RowIndex).Key;
            if (rowPropertyPath == null)
            {
                return;
            }
            var resultKey = _replicateGridColumns.FirstOrDefault(kvp => kvp.Value.Index == e.ColumnIndex).Key;
            if (resultKey == null)
            {
                return;
            }

            var cellPropertyDescriptor = _replicatePivotColumns.GetReplicateColumnGroups()
                .FirstOrDefault(group => resultKey.Equals(group.Key))?.FirstOrDefault(pd =>
                    rowPropertyPath.Equals(pd.DisplayColumn.ColumnDescriptor?.PropertyPath));
            if (cellPropertyDescriptor == null)
            {
                return;
            }

            try
            {
                _replicatePivotColumns.SetConstantColumnValue(cellPropertyDescriptor, cellValue);
            }
            catch (Exception exception)
            {
                _viewContext.OnDataError(sender, new DataGridViewDataErrorEventArgs(exception, e.ColumnIndex, e.RowIndex, DataGridViewDataErrorContexts.Commit));
            }
        }

        private void replicatePivotDataGridView_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            ReplicatePivotGridResized();
        }

        public void replicatePivotDataGridView_OnCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0)
            {
                var value = replicatePivotDataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                var linkValue = value as ILinkValue;
                if (linkValue != null)
                {
                    linkValue.ClickEventHandler(this, e);
                }
            }
        }

        private void boundDataGridView_Resize(object sender, EventArgs e)
        {
            MainGridResized();
        }
        private void boundDataGridView_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            MainGridResized();
        }

        private void ReplicatePivotGridResized()
        {
            if (_inColumnChange)
            {
                return;
            }
            try
            {
                _inColumnChange = true;
                if (replicatePivotDataGridView.Visible && replicatePivotDataGridView.RowCount > 0)
                {
                    var replicatePivotColumns =
                        ReplicatePivotColumns.FromItemProperties(bindingListSource.ItemProperties);
                    AlignDataBoundGridColumns(replicatePivotColumns);
                    UpdateReplicateDataGridScrollPosition(boundDataGridView.HorizontalScrollingOffset);
                }

                UpdateDendrograms();
            }
            finally
            {
                _inColumnChange = false;
            }
            
        }

        private void MainGridPopulated()
        {
            if (_inColumnChange)
            {
                return;
            }

            ViewContext = BindingListSource.ViewContext;
            try
            {
                _inColumnChange = true;
                var replicatePivotColumns = ReplicatePivotColumns.FromItemProperties(bindingListSource.ItemProperties);
                if (replicatePivotColumns != null && replicatePivotColumns.HasConstantAndVariableColumns())
                {
                    replicatePivotDataGridView.Show();
                    dataGridSplitContainer.Panel1Collapsed = false;
                    PopulateReplicateDataGridView(replicatePivotColumns);
                    UpdateMainGridDefaultFrozenColumn();
                    InitializeDataGridSplitterDistance();
                    AlignPivotByReplicateGridColumns(replicatePivotColumns);
                }
                else
                {
                    dataGridSplitContainer.Panel1Collapsed = true;
                    if (replicatePivotDataGridView.Visible)
                    {
                        replicatePivotDataGridView.Hide();
                    }
                }

                UpdateDendrograms();
            }
            finally
            {
                _inColumnChange = false;
            }
        }

        private void MainGridResized()
        {
            if (_inColumnChange)
            {
                return;
            }

            try
            {
                _inColumnChange = true;
                var replicatePivotColumns = ReplicatePivotColumns.FromItemProperties(bindingListSource.ItemProperties);
                if (replicatePivotDataGridView.RowCount > 0 && replicatePivotColumns != null && replicatePivotColumns.HasConstantAndVariableColumns())
                {
                    UpdateReplicateDataGridFrozenState();
                    AdjustDataGridSplitterSizing();
                    AlignPivotByReplicateGridColumns(replicatePivotColumns);
                    UpdateReplicateDataGridScrollPosition(boundDataGridView.HorizontalScrollingOffset);
                }

                UpdateDendrograms();
            }
            finally
            {
                _inColumnChange = false;
            }
        }

        private void boundDataGridView_Scroll(object sender, ScrollEventArgs e)
        {
            MainGridResized();
        }
        
        private void boundDataGridView_ColumnStateChanged(object sender, DataGridViewColumnStateChangedEventArgs e)
        {
            MainGridResized();
        }

        private void bindingListSource_BindingComplete(object sender, BindingCompleteEventArgs e)
        {
            MainGridPopulated();
        }

        private void bindingListSource_ListChanged(object sender, ListChangedEventArgs e)
        {
            MainGridPopulated();
        }

        private void pCAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowPcaPlot();
        }

        public IDataboundGridForm DataboundGridForm
        {
            get
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                return (ParentForm as IDataboundGridForm);
            }
        }

        public DataGridViewEx ReplicatePivotDataGridView { get { return replicatePivotDataGridView; } }

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

        /*
         * Class used to round values and carry over remainders. This
         * is used when aligning replicate grid columns the width of a
         * column is split up into multiple which are could require
         * rounding as the value is divided. 
         */
        private class AdjustingRounder
        {
            private double _roundedDifference;
            public double Round(double value)
            {
                double roundedValue = Math.Round(value);
                double difference = roundedValue - value;

                if (_roundedDifference + difference >= 1)
                {
                    roundedValue -= 1;
                    difference = roundedValue - value;
                }
                else if (_roundedDifference + difference <= -1)
                {
                    roundedValue += 1;
                    difference = roundedValue - value;
                }
                _roundedDifference += difference;
                return roundedValue;
            }
            
            public int RoundRemainder()
            {
                // Allows for ignoring potential insignificant floating point deviations from calculations
                if (Math.Abs(_roundedDifference) < 1e-10)
                {
                    return 0;
                }
                else
                {
                    return _roundedDifference > 0 ? 1 : -1;
                }
            }
        }

        public DataGridViewColumn ReplicatePropertyColumn
        {
            get
            {
                return colReplicateProperty;
            }
        }
    }
}
