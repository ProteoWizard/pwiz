//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using DigitalRune.Windows.Docking;
using NHibernate.Linq;
using IDPicker.DataModel;
using IDPicker.Controls;

namespace IDPicker.Forms
{
    public partial class AnalysisTableForm : DockableForm, IPersistentForm
    {
        #region Wrapper classes for encapsulating query results

        public class Row
        {
            public DataFilter DataFilter { get; protected set; }
            public IList<Row> ChildRows { get; set; }
        }

        public class AnalysisRow : Row
        {
            public DataModel.Analysis Analysis { get; private set; }

            public static IList<Row> GetRows (NHibernate.ISession session, DataFilter dataFilter)
            {
                lock (session)
                return session.CreateQuery("SELECT psm.Analysis " +
                                           dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                           "GROUP BY psm.Analysis.id")
                              .List<Analysis>()
                              .Select(o => new AnalysisRow(o, dataFilter) as Row)
                              .ToList();
            }

            #region Constructor
            public AnalysisRow (object queryRow, DataFilter dataFilter)
            {
                DataFilter = dataFilter;
                Analysis = (DataModel.Analysis) queryRow;
            }
            #endregion
        }

        public class AnalysisParameterRow : Row
        {
            public DataModel.AnalysisParameter AnalysisParameter { get; private set; }

            public static IList<Row> GetRows (NHibernate.ISession session, DataFilter dataFilter)
            {
                lock (session)
                return dataFilter.Analysis.First().Parameters
                                 .Select(o => new AnalysisParameterRow(o, dataFilter) as Row)
                                 .ToList();
            }

            #region Constructor
            public AnalysisParameterRow (object queryRow, DataFilter dataFilter)
            {
                DataFilter = dataFilter;
                AnalysisParameter = (DataModel.AnalysisParameter) queryRow;
            }
            #endregion
        }

        #endregion

        public AnalysisTableForm ()
        {
            InitializeComponent();

            Text = TabText = "Analysis View";
            Icon = Properties.Resources.BlankIcon;

            SetDefaults();

            sortColumns = new List<SortColumn>();

            treeDataGridView.CellDoubleClick += treeDataGridView_CellDoubleClick;
            treeDataGridView.DefaultCellStyleChanged += treeDataGridView_DefaultCellStyleChanged;
            treeDataGridView.DataError += treeDataGridView_DataError;
            treeDataGridView.CellValueNeeded += treeDataGridView_CellValueNeeded;
            treeDataGridView.PreviewKeyDown += treeDataGridView_PreviewKeyDown;
        }

        public TreeDataGridView TreeDataGridView
        {
            get { return treeDataGridView; }
        }

        public IEnumerable<DataGridViewColumn> Columns
        {
            get { return treeDataGridView.Columns.Cast<DataGridViewColumn>(); }
        }

        void treeDataGridView_CellDoubleClick (object sender, TreeDataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndexHierarchy.Count > 1 || e.RowIndexHierarchy.First() < 0)
                return;

            var row = GetRowFromRowHierarchy(e.RowIndexHierarchy) as AnalysisRow;

            var newDataFilter = new DataFilter
            {
                FilterSource = this,
                Analysis = new List<Analysis> { row.Analysis }
            };

            if (AnalysisViewFilter != null)
                AnalysisViewFilter(this, new ViewFilterEventArgs(newDataFilter));
        }

        void treeDataGridView_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                treeDataGridView.ClearSelection();

            if (e.KeyCode != Keys.Enter)
                return;

            var newDataFilter = new DataFilter { FilterSource = this, Analysis = new List<Analysis>() };

            if (treeDataGridView.SelectedCells.Count == 0)
                return;

            var processedRows = new Set<int>();

            foreach (DataGridViewCell cell in treeDataGridView.SelectedCells)
            {
                if (!processedRows.Insert(cell.RowIndex).WasInserted)
                    continue;

                var rowIndexHierarchy = treeDataGridView.GetRowHierarchyForRowIndex(cell.RowIndex);
                var row = GetRowFromRowHierarchy(rowIndexHierarchy) as AnalysisRow;
                if (row != null) newDataFilter.Analysis.Add(row.Analysis);
            }

            if (AnalysisViewFilter != null)
                AnalysisViewFilter(this, new ViewFilterEventArgs(newDataFilter));
        }

        public event EventHandler<ViewFilterEventArgs> AnalysisViewFilter;
        public event EventHandler FinishedSetData;
        public event EventHandler StartingSetData;

        private NHibernate.ISession session;

        private DataFilter viewFilter; // what the user has filtered on
        private DataFilter dataFilter; // how this view is filtered (i.e. never on its own rows)
        private DataFilter basicDataFilter; // the basic filter without the user filtering on rows

        private Color filteredOutColor, filteredPartialColor;

        private class SortColumn
        {
            public int Index { get; set; }
            public SortOrder Order { get; set; }

            public void ToggleOrder ()
            {
                if (Order == SortOrder.Ascending)
                    Order = SortOrder.Descending;
                else
                    Order = SortOrder.Ascending;
            }
        }

        private Dictionary<DataGridViewColumn, ColumnProperty> _columnSettings;
        private List<SortColumn> sortColumns;
        private Map<int, SortOrder> initialColumnSortOrders;

        [Flags]
        private enum RowFilterState
        {
            Unknown = 0,
            Out = 1,
            In = 2,
            Partial = 3
        };

        private IList<Row> rows, basicRows;

        private void SetDefaults ()
        {
            _columnSettings = new Dictionary<DataGridViewColumn, ColumnProperty>()
            {
                { nameColumn, new ColumnProperty() {Type = typeof(string)}},
                { softwareColumn, new ColumnProperty() {Type = typeof(string)}},
                { parameterValue, new ColumnProperty() {Type = typeof(string)}},
            };

            foreach (var kvp in _columnSettings)
            {
                kvp.Value.Name = kvp.Key.Name;
                kvp.Value.Index = kvp.Key.Index;
                kvp.Value.DisplayIndex = kvp.Key.DisplayIndex;
            }

            initialColumnSortOrders = new Map<int, SortOrder>()
            {
                {nameColumn.Index, SortOrder.Ascending},
                {softwareColumn.Index, SortOrder.Ascending},
                {parameterValue.Index, SortOrder.Ascending},
            };
        }

        public void SetData (NHibernate.ISession session, DataFilter dataFilter)
        {
            if (session == null)
                return;

            if (StartingSetData != null)
                StartingSetData(this, EventArgs.Empty);

            this.session = session;
            viewFilter = dataFilter;
            this.dataFilter = new DataFilter(dataFilter) { Analysis = null };

            ClearData();

            Text = TabText = "Loading analysis view...";

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += new DoWorkEventHandler(setData);
            workerThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(renderData);
            workerThread.RunWorkerAsync();
        }

        public void ClearData ()
        {
            Text = TabText = "Analysis View";

            Controls.OfType<Control>().ForEach(o => o.Enabled = false);

            treeDataGridView.RootRowCount = 0;
            Refresh();
        }

        public void ClearData (bool clearBasicFilter)
        {
            if (clearBasicFilter)
                basicDataFilter = null;
            ClearData();
        }

        void setData (object sender, DoWorkEventArgs e)
        {
            try
            {
                if (dataFilter.IsBasicFilter)
                {
                    if (basicDataFilter == null || (dataFilter.IsBasicFilter && dataFilter != basicDataFilter))
                    {
                        basicDataFilter = new DataFilter(this.dataFilter);
                        basicRows = AnalysisRow.GetRows(session, dataFilter);
                    }

                    rows = basicRows;
                }
                else
                    rows = AnalysisRow.GetRows(session, dataFilter);

                applySort();

                if (FinishedSetData != null)
                    FinishedSetData(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Exception)
            {
                Program.HandleException(e.Result as Exception);
                return;
            }

            Controls.OfType<Control>().ForEach(o => o.Enabled = true);

            treeDataGridView.RootRowCount = rows.Count();

            Text = TabText = "Analysis View";

            treeDataGridView.Refresh();
        }

        private RowFilterState getRowFilterState (Row parentRow) { return RowFilterState.In; }
        private IList<Row> getChildren (Row parentRow)
        {
            if (parentRow.ChildRows != null)
            {
            }
            else if (parentRow is AnalysisRow)
            {
                var row = parentRow as AnalysisRow;
                var childFilter = new DataFilter(parentRow.DataFilter) {Analysis = new List<Analysis> {row.Analysis}};
                parentRow.ChildRows = AnalysisParameterRow.GetRows(session, childFilter);
            }

            if (!sortColumns.IsNullOrEmpty())
            {
                var sortColumn = sortColumns.Last();
                parentRow.ChildRows = parentRow.ChildRows.OrderBy(o => getCellValue(sortColumn.Index, o), sortColumn.Order).ToList();
            }

            return parentRow.ChildRows;
        }

        public Row GetRowFromRowHierarchy (IList<int> rowIndexHierarchy)
        {
            Row row = rows[rowIndexHierarchy.First()];
            for (int i = 1; i < rowIndexHierarchy.Count; ++i)
            {
                getChildren(row); // get child rows if necessary
                row = row.ChildRows[rowIndexHierarchy[i]];
            }
            return row;
        }

        protected override void OnLoad (EventArgs e)
        {
            treeDataGridView_DefaultCellStyleChanged(this, EventArgs.Empty);

            base.OnLoad(e);

            treeDataGridView.ClearSelection();
        }

        private void treeDataGridView_DefaultCellStyleChanged (object sender, EventArgs e)
        {
            var style = treeDataGridView.DefaultCellStyle;
            filteredOutColor = style.ForeColor.Interpolate(style.BackColor, 0.66f);
            filteredPartialColor = style.ForeColor.Interpolate(style.BackColor, 0.33f);
        }

        protected override void OnFormClosing (FormClosingEventArgs e)
        {
            DockState = DockState.DockBottomAutoHide;
            e.Cancel = true;
            base.OnFormClosing(e);
        }

        private void resetData ()
        {
            if (dataFilter != null && dataFilter.IsBasicFilter)
                basicDataFilter = null; // force refresh of basic rows

            if (session != null && session.IsOpen)
                SetData(session, viewFilter);
        }

        private void treeDataGridView_CellValueNeeded (object sender, TreeDataGridViewCellValueEventArgs e)
        {
            if (e.RowIndexHierarchy.First() >= rows.Count)
            {
                e.Value = null;
                return;
            }

            Row baseRow = rows[e.RowIndexHierarchy.First()];
            for (int i = 1; i < e.RowIndexHierarchy.Count; ++i)
            {
                getChildren(baseRow); // populate ChildRows if necessary
                baseRow = baseRow.ChildRows[e.RowIndexHierarchy[i]];
            }

            if (baseRow is AnalysisRow)
            {
                var row = baseRow as AnalysisRow;
                e.ChildRowCount = row.Analysis.Parameters.Count;
            }

            e.Value = getCellValue(e.ColumnIndex, baseRow);
        }

        protected virtual object getCellValue (int columnIndex, Row baseRow)
        {
            if (baseRow is AnalysisRow)
            {
                var row = baseRow as AnalysisRow;
                if (columnIndex == nameColumn.Index) return row.Analysis.Name;
                else if (columnIndex == softwareColumn.Index) return row.Analysis.Software.Name + " " + row.Analysis.Software.Version;
            }
            else if (baseRow is AnalysisParameterRow)
            {
                var row = baseRow as AnalysisParameterRow;
                if (columnIndex == nameColumn.Index) return row.AnalysisParameter.Name;
                else if (columnIndex == parameterValue.Index) return row.AnalysisParameter.Value;
            }
            return null;
        }

        public void Sort (int columnIndex)
        {
            SortColumn sortColumn;

            // if already sorting by the clicked column, reverse sort order
            if (sortColumns.Any(o => o.Index == columnIndex))
            {
                int index = sortColumns.FindIndex(o => o.Index == columnIndex);
                sortColumn = sortColumns[index];
                sortColumn.ToggleOrder();
            }
            else
            {
                //if (e.Button == MouseButtons.Left)
                {
                    foreach (var c in sortColumns)
                        treeDataGridView.Columns[c.Index].HeaderCell.SortGlyphDirection = SortOrder.None;
                    sortColumns.Clear();
                }
                //else if (e.Button != MouseButtons.Middle)
                //    return;
                if (initialColumnSortOrders.Contains(columnIndex))
                    sortColumn = new SortColumn() { Index = columnIndex, Order = initialColumnSortOrders[columnIndex] };
                else
                    sortColumn = new SortColumn() { Index = columnIndex, Order = SortOrder.Descending };
            }

            var column = treeDataGridView.Columns[columnIndex];
            column.HeaderCell.SortGlyphDirection = sortColumn.Order;
            sortColumns.Add(sortColumn);
            applySort();
            treeDataGridView.CollapseAll();
            treeDataGridView.Refresh();
        }

        protected void applySort ()
        {
            if (rows == null || !sortColumns.Any())
                return;

            var sortColumn = sortColumns.Last();
            rows = rows.OrderBy(o => getCellValue(sortColumn.Index, o), sortColumn.Order).ToList();
        }

        protected virtual void setColumnVisibility ()
        {
            // restore display index
            foreach (var kvp in _columnSettings)
            {
                kvp.Key.DisplayIndex = kvp.Value.DisplayIndex;

                // restore column size

                // update number formats
                if (kvp.Value.Type == typeof(float) && kvp.Value.Precision.HasValue)
                    kvp.Key.DefaultCellStyle.Format = "f" + kvp.Value.Precision.Value.ToString();

                // update link columns
                var linkColumn = kvp.Key as DataGridViewLinkColumn;
                if (linkColumn != null)
                    linkColumn.ActiveLinkColor = linkColumn.LinkColor = treeDataGridView.ForeColor;
            }
        }

        public void LoadLayout (FormProperty formProperty)
        {
            if (formProperty == null)
                return;

            var columnlist = _columnSettings.Keys.ToList();

            foreach (var column in columnlist)
            {
                var columnProperty = formProperty.ColumnProperties.SingleOrDefault(x => x.Name == column.Name);

                //if rowSettings is null it is likely an unsaved pivotColumn, keep defualt
                if (columnProperty == null)
                    continue;

                _columnSettings[column] = columnProperty;
            }

            treeDataGridView.ForeColor = treeDataGridView.DefaultCellStyle.ForeColor = formProperty.ForeColor ?? SystemColors.WindowText;
            treeDataGridView.BackgroundColor = treeDataGridView.GridColor = treeDataGridView.DefaultCellStyle.BackColor = formProperty.BackColor ?? SystemColors.Window;

            var sortColumnSettings = formProperty.SortColumns;
            if (formProperty.SortColumns != null && formProperty.SortColumns.Count > 0)
            {
                var newSortColumns = new List<SortColumn>();
                foreach (var sortColumn in formProperty.SortColumns)
                    newSortColumns.Add(new SortColumn()
                    {
                        Index = sortColumn.Index,
                        Order = sortColumn.Order
                    });
                Sort(newSortColumns.First().Index);
                sortColumns = newSortColumns;
            }

            setColumnVisibility();
            applySort();

            treeDataGridView.Refresh();
        }

        public FormProperty GetCurrentProperties (bool pivotToo)
        {
            var result = new FormProperty()
            {
                Name = this.Name,
                BackColor = treeDataGridView.DefaultCellStyle.BackColor,
                ForeColor = treeDataGridView.DefaultCellStyle.ForeColor,
                SortColumns = sortColumns.Select(o => new DataModel.SortColumn() { Index = o.Index, Order = o.Order }).ToList(),
                ColumnProperties = _columnSettings.Values.ToList()
            };

            foreach (var kvp in _columnSettings)
            {
                kvp.Value.Index = kvp.Key.Index;
                kvp.Value.DisplayIndex = kvp.Key.Index;
            }

            return result;
        }

        public virtual List<List<string>> GetFormTable (bool selected)
        {
            var exportTable = new List<List<string>>();
            IList<int> exportedRows, exportedColumns;

            if (selected && treeDataGridView.SelectedCells.Count > 0 && !treeDataGridView.AreAllCellsSelected(false))
            {
                var selectedRows = new Set<int>();
                var selectedColumns = new Map<int, int>(); // ordered by DisplayIndex

                foreach (DataGridViewCell cell in treeDataGridView.SelectedCells)
                {
                    selectedRows.Add(cell.RowIndex);
                    selectedColumns[cell.OwningColumn.DisplayIndex] = cell.ColumnIndex;
                }

                exportedRows = selectedRows.ToList();
                exportedColumns = selectedColumns.Values;
            }
            else
            {
                exportedRows = treeDataGridView.Rows.Cast<DataGridViewRow>().Select(o => o.Index).ToList();
                exportedColumns = treeDataGridView.GetVisibleColumnsInDisplayOrder().Select(o => o.Index).ToList();
            }

            // add column headers
            exportTable.Add(new List<string>());
            foreach (var columnIndex in exportedColumns)
                exportTable.Last().Add(treeDataGridView.Columns[columnIndex].HeaderText);

            foreach (int rowIndex in exportedRows)
            {
                /* TODO: how to handle non-root rows?
                var row = rows[rowIndex];

                // skip non-root rows or filtered rows
                if (rowIndexHierarchy.Count > 1 || getRowFilterState(row) == RowFilterState.Out)
                    continue;*/

                var rowIndexHierarchy = treeDataGridView.GetRowHierarchyForRowIndex(rowIndex);

                var rowText = new List<string>();
                foreach (var columnIndex in exportedColumns)
                {
                    object value = treeDataGridView[columnIndex, rowIndex].Value ?? String.Empty;
                    rowText.Add(value.ToString());

                    if (columnIndex == 0 && rowIndexHierarchy.Count > 1)
                    {
                        int indent = (rowIndexHierarchy.Count - 1) * 2;
                        rowText[rowText.Count - 1] = new string(' ', indent) + rowText[rowText.Count - 1];
                    }
                }

                exportTable.Add(rowText);
            }

            return exportTable;
        }

        protected void clipboardToolStripMenuItem_Click (object sender, EventArgs e)
        {
            var table = GetFormTable(sender == copyToClipboardSelectedToolStripMenuItem);

            TableExporter.CopyToClipboard(table);
        }

        protected void fileToolStripMenuItem_Click (object sender, EventArgs e)
        {
            var table = GetFormTable(sender == exportSelectedCellsToFileToolStripMenuItem);

            TableExporter.ExportToFile(table);
        }

        protected void exportButton_Click (object sender, EventArgs e)
        {
            exportMenu.Show(Cursor.Position);
        }

        protected void showInExcelToolStripMenuItem_Click (object sender, EventArgs e)
        {
            var table = GetFormTable(sender == showInExcelSelectToolStripMenuItem);

            var exportWrapper = new Dictionary<string, List<List<string>>> { { this.Name, table } };

            TableExporter.ShowInExcel(exportWrapper, false);
        }

        protected void displayOptionsButton_Click (object sender, EventArgs e)
        {
            using (var ccf = new ColumnControlForm())
            {
                ccf.ColumnProperties = _columnSettings.ToDictionary(o => o.Key.HeaderText, o => o.Value);

                if (treeDataGridView.DefaultCellStyle.ForeColor.ToArgb() != SystemColors.WindowText.ToArgb())
                    ccf.DefaultForeColor = treeDataGridView.DefaultCellStyle.ForeColor;
                if (treeDataGridView.DefaultCellStyle.BackColor.ToArgb() != SystemColors.Window.ToArgb())
                    ccf.DefaultBackColor = treeDataGridView.DefaultCellStyle.BackColor;

                if (ccf.ShowDialog() != DialogResult.OK)
                    return;

                // update column properties
                foreach (var kvp in ccf.ColumnProperties)
                    _columnSettings[Columns.Single(o => o.HeaderText == kvp.Key)] = kvp.Value;

                setColumnVisibility();

                // update default cell style
                treeDataGridView.ForeColor = treeDataGridView.DefaultCellStyle.ForeColor = ccf.DefaultForeColor ?? SystemColors.WindowText;
                treeDataGridView.BackgroundColor = treeDataGridView.DefaultCellStyle.BackColor = ccf.DefaultBackColor ?? SystemColors.Window;

                treeDataGridView.Refresh();
            }
        }

        protected void treeDataGridView_DataError (object sender, DataGridViewDataErrorEventArgs e)
        {
            Program.HandleException(e.Exception);
            e.ThrowException = false;
        }

        public void ClearSession ()
        {
            ClearData();
            if (session != null && session.IsOpen)
            {
                session.Close();
                session.Dispose();
                session = null;
            }
        }
    }
}
