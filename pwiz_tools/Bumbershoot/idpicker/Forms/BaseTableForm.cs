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
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using DigitalRune.Windows.Docking;
using NHibernate.Linq;
using PopupControl;
using IDPicker;
using IDPicker.DataModel;
using IDPicker.Controls;
using pwiz.Common.Collections;

namespace IDPicker.Forms
{
    public partial class BaseTableForm : DockableForm, IPersistentForm, TableExporter.ITable
    {
        private class DataGridViewRowWithAccessibleValues : DataGridViewRow
        {
            public string RowHeaderCellEmptyString { get; set; }

            protected class DataGridViewRowWithAccessibleValuesAccessibleObject : DataGridViewRowAccessibleObject
            {
                public string RowHeaderCellEmptyString { get; set; }

                public DataGridViewRowWithAccessibleValuesAccessibleObject(DataGridViewRow owner) : base(owner) { RowHeaderCellEmptyString = String.Empty; }
                public override string Value
                {
                    get
                    {
                        return String.Format("{0};{1}", Owner.HeaderCell.Value ?? RowHeaderCellEmptyString, String.Join(";", Owner.Cells.OfType<DataGridViewCell>().Where(o => o.Visible).Select(o => o.FormattedValue ?? String.Empty)));
                    }
                }
            }

            protected override AccessibleObject CreateAccessibilityInstance()
            {
                return new DataGridViewRowWithAccessibleValuesAccessibleObject(this) { RowHeaderCellEmptyString = RowHeaderCellEmptyString };
            }
        }

        public BaseTableForm()
        {
            InitializeComponent();

            sortColumns = new List<SortColumn>();
            updateGroupings(new FormProperty());
            updatePivots(new FormProperty());

            treeDataGridView.DefaultCellStyleChanged += treeDataGridView_DefaultCellStyleChanged;
            treeDataGridView.DataError +=treeDataGridView_DataError;
            treeDataGridView.RowTemplate = new DataGridViewRowWithAccessibleValues();

            findTextBox.Tag = findTextBox.Text = "Find...";
            findTextBox.KeyUp += new KeyEventHandler(findTextBox_KeyUp);

            iTRAQ_ReporterIonColumns = new List<DataGridViewTextBoxColumn>();
            TMT_ReporterIonColumns = new List<DataGridViewTextBoxColumn>();

            foreach (int ion in new int[] { 113, 114, 115, 116, 117, 118, 119, 121 })
            {
                var column = new DataGridViewTextBoxColumn
                {
                    HeaderText = "iTRAQ-" + ion.ToString(),
                    Name = "iTRAQ-" + ion.ToString() + "Column",
                    ReadOnly = true,
                    Tag = iTRAQ_ReporterIonColumns.Count,
                    Width = 100
                };
                iTRAQ_ReporterIonColumns.Add(column);
            }

            foreach (string ion in new string[] { "126", "127N", "127C", "128N", "128C", "129N", "129C", "130N", "130C", "131N", "131C", "132N", "132C", "133N", "133C", "134" })
            {
                var column = new DataGridViewTextBoxColumn
                {
                    HeaderText = "TMT-" + ion,
                    Name = "TMT-" + ion + "Column",
                    ReadOnly = true,
                    Tag = TMT_ReporterIonColumns.Count,
                    Width = 100
                };
                TMT_ReporterIonColumns.Add(column);
            }
        }

        void findTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode.HasFlag(Keys.V))
            {
                string text = Clipboard.GetText(TextDataFormat.Text);
                if (text.IsNullOrEmpty())
                    return;

                string delimiter = text.Contains("\r") ? "\r\n" : "\n";

                // the paste operation will only take the first line, so override it by delimiting by ';'
                var allLines = text.Split(new string[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
                if (allLines.Count() > 1)
                    findTextBox.Text = String.Join(";", allLines);
                findTextBox.SelectAll();
            }
        }

        public TreeDataGridView TreeDataGridView
        {
            get { return treeDataGridView; }
        }

        public IEnumerable<DataGridViewColumn> Columns
        {
            get { return treeDataGridView.Columns.Cast<DataGridViewColumn>(); }
        }

        public GroupingSetupControl<GroupBy> GroupingSetupControl
        {
            get { return groupingSetupControl; }
        }

        public PivotSetupControl<PivotBy> PivotSetupControl
        {
            get { return pivotSetupControl; }
        }

        public virtual void SetData(NHibernate.ISession session, DataFilter viewFilter)
        {
            if (session != null && StartingSetData != null)
                StartingSetData(this, EventArgs.Empty);
        }

        public virtual void ClearData() { throw new NotImplementedException(); }
        public virtual void ClearData(bool clearBasicFilter) { throw new NotImplementedException(); }

        protected void setGroupings(params Grouping<GroupBy>[] groupings)
        {
            groupingSetupControl = new GroupingSetupControl<GroupBy>(groupings);
            groupingSetupControl.GroupingChanged += groupingSetupControl_GroupingChanged;
            groupingSetupPopup = new Popup(groupingSetupControl) {FocusOnOpen = true};
            groupingSetupPopup.Closed += groupingSetupPopup_Closed;

            checkedGroupings = groupingSetupControl.CheckedGroupings;
        }

        protected void setPivots(params Pivot<PivotBy>[] pivots)
        {
            pivotSetupControl = new PivotSetupControl<PivotBy>(pivots);
            pivotSetupControl.PivotChanged += pivotSetupControl_PivotChanged;
            pivotSetupPopup = new Popup(pivotSetupControl) {FocusOnOpen = true};
            pivotSetupPopup.Closed += pivotSetupPopup_Closed;

            checkedPivots = pivotSetupControl.CheckedPivots;
        }

        public List<DataGridViewTextBoxColumn> iTRAQ_ReporterIonColumns { get; }
        public List<DataGridViewTextBoxColumn> TMT_ReporterIonColumns { get; }

        protected NHibernate.ISession session;

        protected DataFilter viewFilter; // what the user has filtered on
        protected DataFilter dataFilter; // how this view is filtered (i.e. never on its own rows)
        protected DataFilter basicDataFilter; // the basic filter without the user filtering on rows

        public event EventHandler FinishedSetData;
        public event EventHandler StartingSetData;

        protected Color filteredOutColor, filteredPartialColor;

        protected class SortColumn
        {
            public int Index { get; set; }
            public SortOrder Order { get; set; }

            public void ToggleOrder()
            {
                if (Order == SortOrder.Ascending)
                    Order = SortOrder.Descending;
                else
                    Order = SortOrder.Ascending;
            }
        }

        protected Dictionary<DataGridViewColumn, ColumnProperty> _columnSettings;
        protected List<SortColumn> sortColumns;
        protected Map<int, SortOrder> initialColumnSortOrders;

        protected GroupingSetupControl<GroupBy> groupingSetupControl;
        protected Popup groupingSetupPopup;
        protected bool dirtyGroupings = false;
        protected IList<Grouping<GroupBy>> checkedGroupings;

        protected PivotSetupControl<PivotBy> pivotSetupControl;
        protected Popup pivotSetupPopup;
        protected bool dirtyPivots = false;
        protected IList<Pivot<PivotBy>> checkedPivots;

        protected List<DataGridViewColumn> pivotColumns = new List<DataGridViewColumn>();
        protected List<DataGridViewColumn> oldPivotColumns;

        // TODO: support multiple selected objects
        string[] oldSelectionPath;


        [Flags]
        protected enum RowFilterState
        {
            Unknown = 0,
            Out = 1,
            In = 2,
            Partial = 3
        };

        public class Row
        {
            public DataFilter DataFilter { get; protected set; }
            public IList<Row> ChildRows { get; set; }

            public static string RollupSQL { get { return GetQuantitationRollupSql(Properties.GUI.Settings.Default.QuantitationRollupMethod); } }
            protected static string GetQuantitationRollupSql(QuantitationRollupMethod method)
            {
                switch (method)
                {
                    case QuantitationRollupMethod.Sum: return "DISTINCT_DOUBLE_ARRAY_SUM";
                    case QuantitationRollupMethod.Mean: return "DISTINCT_DOUBLE_ARRAY_MEAN";
                    case QuantitationRollupMethod.Median: return "DISTINCT_DOUBLE_ARRAY_MEDIAN";
                    case QuantitationRollupMethod.Tukey: return "DISTINCT_DOUBLE_ARRAY_TUKEY_BIWEIGHT_AVERAGE";
                    case QuantitationRollupMethod.TukeyLog: return "DISTINCT_DOUBLE_ARRAY_TUKEY_BIWEIGHT_LOG_AVERAGE";
                    default:
                        throw new ArgumentException("unsupported rollup method " + method.ToString());
                }
            }
        }

        protected IList<Row> rows, basicRows;

        /// <summary>The row list before the find filter is applied.</summary>
        protected IList<Row> unfilteredRows;

        protected virtual RowFilterState getRowFilterState(Row parentRow) { throw new NotImplementedException(); }
        protected virtual IList<Row> getChildren (Row parentRow) { throw new NotImplementedException(); }

        public virtual Row GetRowFromRowHierarchy(IList<int> rowIndexHierarchy)
        {
            Row row = rows[rowIndexHierarchy.First()];
            for (int i = 1; i < rowIndexHierarchy.Count; ++i)
            {
                getChildren(row); // get child rows if necessary
                if (row.ChildRows == null)
                    continue;
                row = row.ChildRows[rowIndexHierarchy[i]];
            }
            return row;
        }

        protected override void OnLoad(EventArgs e)
        {
            treeDataGridView_DefaultCellStyleChanged(this, EventArgs.Empty);

            base.OnLoad(e);

            treeDataGridView.ClearSelection();
        }
        
        protected virtual void OnFinishedSetData()
        {
            if (FinishedSetData != null)
                FinishedSetData(this, EventArgs.Empty);
        }

        private void treeDataGridView_DefaultCellStyleChanged(object sender, EventArgs e)
        {
            var style = treeDataGridView.DefaultCellStyle;
            filteredOutColor = style.ForeColor.Interpolate(style.BackColor, 0.66f);
            filteredPartialColor = style.ForeColor.Interpolate(style.BackColor, 0.33f);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            DockState = DockState.DockBottomAutoHide;
            e.Cancel = true;
            base.OnFormClosing(e);
        }

        protected void pivotSetupButton_Click (object sender, EventArgs e) { pivotSetupPopup.Show(pivotSetupButton); }

        protected void pivotSetupControl_PivotChanged(object sender, EventArgs e)
        {
            if (pivotSetupPopup.Visible)
                dirtyPivots = true;
            else
            {
                checkedPivots = pivotSetupControl.CheckedPivots;
                OnPivotChanged(this, EventArgs.Empty);
            }
        }

        protected void pivotSetupPopup_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            if (dirtyPivots)
            {
                dirtyPivots = false;
                checkedPivots = pivotSetupControl.CheckedPivots;
                OnPivotChanged(this, EventArgs.Empty);
            }
        }

        protected void groupingSetupButton_Click (object sender, EventArgs e) { groupingSetupPopup.Show(groupingSetupButton); }

        protected void groupingSetupControl_GroupingChanged(object sender, EventArgs e) { dirtyGroupings = true; }

        protected void groupingSetupPopup_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            if (dirtyGroupings)
            {
                dirtyGroupings = false;
                checkedGroupings = groupingSetupControl.CheckedGroupings;

                // don't try to save selection, since a different grouping would invalidate it
                treeDataGridView.ClearSelection();
                oldSelectionPath = null;

                OnGroupingChanged(this, EventArgs.Empty);
            }
        }

        protected void resetData()
        {
            if (dataFilter != null && dataFilter.IsBasicFilter)
                basicDataFilter = null; // force refresh of basic rows

            if (session != null && session.IsOpen)
                SetData(session, viewFilter);
        }

        protected virtual object getCellValue(int columnIndex, Row row) { throw new NotImplementedException(); }

        protected virtual void OnPivotChanged(object sender, EventArgs e) { resetData(); }

        protected virtual void OnGroupingChanged(object sender, EventArgs e)
        {
            ClearData(true);
            resetData();
        }

        public void Sort(int columnIndex)
        {
            if (treeDataGridView.RowCount == 0)
                return;

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
                    sortColumn = new SortColumn() {Index = columnIndex, Order = initialColumnSortOrders[columnIndex]};
                else
                    sortColumn = new SortColumn() {Index = columnIndex, Order = SortOrder.Descending};
            }

            var column = treeDataGridView.Columns[columnIndex];
            column.HeaderCell.SortGlyphDirection = sortColumn.Order;
            sortColumns.Add(sortColumn);
            applySort();
            treeDataGridView.CollapseAll();
            treeDataGridView.Refresh();
        }

        protected void applySort()
        {
            if (rows == null || !sortColumns.Any())
                return;

            var sortColumn = sortColumns.Last();
            rows = rows.OrderBy(o => getCellValue(sortColumn.Index, o), sortColumn.Order).ToList();
        }

        protected void applyFindFilter()
        {
            if (findTextBox.Text.IsNullOrEmpty() || findTextBox.Text == "Find...")
            {
                if (unfilteredRows != null)
                    rows = unfilteredRows;
                return;
            }

            string filterText = findTextBox.Text;

            if (unfilteredRows != null)
                rows = unfilteredRows;
            else
                unfilteredRows = rows;

            if (!filterRowsOnText(findTextBox.Text))
                unfilteredRows = null;
        }

        protected virtual bool filterRowsOnText(string text) { throw new NotImplementedException(); }

        protected virtual bool updatePivots (FormProperty formProperty)
        {
            var oldCheckedPivots = new List<PivotBy>(checkedPivots.Select(o => o.Mode));

            pivotSetupControl.Pivots.ForEach(o => pivotSetupControl.SetPivot(o.Mode, false));
            foreach (var pivot in formProperty.PivotModes)
                pivotSetupControl.SetPivot(pivot.Mode, true);

            // determine if checked pivots changed
            return !oldCheckedPivots.SequenceEqual(checkedPivots.Select(o => o.Mode));
        }

        protected virtual bool updateGroupings (FormProperty formProperty)
        {
            var oldCheckedGroupings = new List<GroupBy>(checkedGroupings.Select(o => o.Mode));

            var groupings = new List<Grouping<GroupBy>>();
            foreach (var grouping in formProperty.GroupingModes)
                groupings.Add(new Grouping<GroupBy>(grouping.Enabled)
                {
                    Mode = grouping.Mode,
                    Text = grouping.Name
                });
            setGroupings(groupings.ToArray());

            // determine if checked groupings changed
            return !oldCheckedGroupings.SequenceEqual(checkedGroupings.Select(o => o.Mode));
        }

        protected void saveSelectionPath()
        {
            if (treeDataGridView.SelectedCells.Count == 0)
                return;

            int firstSelectedRowIndex = treeDataGridView.SelectedCells[0].RowIndex;
            var firstSelectedRowHierarchy = treeDataGridView.GetRowHierarchyForRowIndex(firstSelectedRowIndex);
            oldSelectionPath = new string[firstSelectedRowHierarchy.Count];
            for (int i = 0; i < oldSelectionPath.Length; ++i)
                oldSelectionPath[i] = treeDataGridView[0, firstSelectedRowHierarchy.Take(i + 1).ToArray()].Value.ToString();
        }

        protected void restoreSelectionPath()
        {
            //treeDataGridView.SetRedraw(false);

            treeDataGridView.ClearSelection();

            if (oldSelectionPath.IsNullOrEmpty())
                return;

            var visibleColumnIndexes = treeDataGridView.GetVisibleColumns().Select(o => o.Index);

            for (int i = 0; i < treeDataGridView.Rows.Count; ++i)
                if (treeDataGridView.Rows[i].Cells[0].Value.ToString() == oldSelectionPath[0])
                {
                    if (oldSelectionPath.Length > 1)
                    {
                        for (int j = 1; j < oldSelectionPath.Length; ++j)
                        {
                            int childRowCount = treeDataGridView.Expand(i);
                            for (int k = 1; k <= childRowCount; ++k)
                                if (treeDataGridView.Rows[i + k].Cells[0].Value.ToString() == oldSelectionPath[j])
                                {
                                    foreach (int column in visibleColumnIndexes)
                                    {
                                        treeDataGridView[column, i].Selected = false;
                                        treeDataGridView[column, i + k].Selected = true;
                                    }

                                    i += k;
                                    treeDataGridView.FirstDisplayedScrollingRowIndex = i;
                                    break;
                                }
                        }
                    }
                    else
                    {
                        foreach (int column in visibleColumnIndexes)
                            treeDataGridView[column, i].Selected = true;
                        treeDataGridView.FirstDisplayedScrollingRowIndex = i;
                    }
                    break;
                }

            //treeDataGridView.SetRedraw(true);
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

            //_unusedPivotSettings = listOfSettings.Where(x => x.Type == "PivotColumn").ToList();

            var columnlist = _columnSettings.Keys.ToList();
            var untouchedColumns = _columnSettings.Keys.ToList();

            foreach (var column in columnlist)
            {
                var columnProperty = formProperty.ColumnProperties.SingleOrDefault(x => x.Name == column.Name);

                //if rowSettings is null it is likely an unsaved pivotColumn, keep defualt
                if (columnProperty == null)
                    continue;

                //if (_unusedPivotSettings.Contains(rowSettings))
                //    _unusedPivotSettings.Remove(rowSettings);

                _columnSettings[column] = columnProperty;

                untouchedColumns.Remove(column);
            }

            //Set unspecified columns (most likely pivotColumns) to blend in better
            foreach (var column in untouchedColumns)
            {
                _columnSettings[column].Visible = null;
                _columnSettings[column].BackColor = formProperty.BackColor;
                _columnSettings[column].ForeColor = formProperty.ForeColor;
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

            bool pivotsChanged = updatePivots(formProperty);
            bool groupingChanged = updateGroupings(formProperty);

            setColumnVisibility();

            if (pivotsChanged || groupingChanged)
                resetData();
            else
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
                GroupingModes = groupingSetupControl.Groupings.Select(o => new DataModel.Grouping() { Enabled = o.Checked, Mode = o.Mode, Name = o.Text }).ToList(),
                PivotModes = checkedPivots.Select(o => new DataModel.Pivot() { Mode = o.Mode, Name = o.Text }).ToList(),
                SortColumns = sortColumns.Select(o => new DataModel.SortColumn() { Index = o.Index, Order = o.Order }).ToList(),
                ColumnProperties = _columnSettings.Values.ToList()
            };

            foreach (var kvp in _columnSettings)
            {
                kvp.Value.Index = kvp.Key.Index;
                kvp.Value.DisplayIndex = kvp.Key.Index;
            }

            if (!pivotToo)
                result.ColumnProperties.RemoveAll(o => !initialColumnSortOrders.Contains(o.Index));

            return result;
        }

        //public virtual IList<string> Headers { get; protected set; }

        private class ExportedTableRow : TableExporter.ITableRow
        {
            public virtual IList<string> Headers { get; set; }
            public virtual IList<string> Cells { get; set; }
        }

        private bool exportSelectedCellsOnly = false;

        public IEnumerator<TableExporter.ITableRow> GetEnumerator()
        {
            IEnumerable exportedRows;
            IList<int> exportedColumns;

            if (exportSelectedCellsOnly &&
                treeDataGridView.SelectedCells.Count > 0 &&
                !treeDataGridView.AreAllCellsSelected(false))
            {
                var selectedRows = new Set<DataGridViewRow>();
                var selectedColumns = new Map<int, int>(); // ordered by DisplayIndex

                foreach (DataGridViewCell cell in treeDataGridView.SelectedCells)
                {
                    selectedRows.Add(cell.OwningRow);
                    selectedColumns[cell.OwningColumn.DisplayIndex] = cell.ColumnIndex;
                }

                exportedRows = selectedRows;
                exportedColumns = selectedColumns.Values;
            }
            else
            {
                exportedRows = treeDataGridView.Rows;
                exportedColumns = treeDataGridView.GetVisibleColumnsInDisplayOrder().Select(o => o.Index).ToList();
            }

            // add column headers
            var Headers = new List<string>();
            foreach (var columnIndex in exportedColumns)
                Headers.Add(treeDataGridView.Columns[columnIndex].HeaderText);

            foreach (DataGridViewRow row in exportedRows)
            {
                var exportedRow = new ExportedTableRow();
                exportedRow.Headers = Headers;

                /* TODO: how to handle non-root rows?
                var row = rows[rowIndex];

                // skip non-root rows or filtered rows
                if (rowIndexHierarchy.Count > 1 || getRowFilterState(row) == RowFilterState.Out)
                    continue;*/

                if (rows.Count > row.Index)
                {
                    Row row2 = rows[row.Index];
                    if (getRowFilterState(row2) == RowFilterState.Out)
                        continue;
                }

                var rowIndexHierarchy = treeDataGridView.GetRowHierarchyForRowIndex(row.Index);

                exportedRow.Cells = new List<string>();
                foreach (var columnIndex in exportedColumns)
                {
                    // empty cells are exported as blank except for pivot columns which are exported as zeros
                    object value = row.Cells[columnIndex].Value ?? (treeDataGridView.Columns[columnIndex].Name.StartsWith("pivot") ? "0" : String.Empty);
                    var sb = new StringBuilder(value.ToString());

                    if (columnIndex == 0 && rowIndexHierarchy.Count > 1)
                    {
                        int indent = (rowIndexHierarchy.Count - 1) * 2;
                        sb.Insert(0, new string(' ', indent));
                    }
                    exportedRow.Cells.Add(sb.ToString());
                }

                yield return exportedRow;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator) GetEnumerator();
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

                if (rows.Count > rowIndex)
                {
                    Row row = rows[rowIndex];
                    if (getRowFilterState(row) == RowFilterState.Out)
                        continue;
                }

                var rowIndexHierarchy = treeDataGridView.GetRowHierarchyForRowIndex(rowIndex);

                var rowText = new List<string>();
                foreach (var columnIndex in exportedColumns)
                {
                    // empty cells are exported as blank except for pivot columns which are exported as zeros
                    object value = treeDataGridView[columnIndex, rowIndex].Value ?? (treeDataGridView.Columns[columnIndex].Name.StartsWith("pivot") ? "0" : String.Empty);
                    rowText.Add(value.ToString());

                    if (columnIndex == 0 && rowIndexHierarchy.Count > 1)
                    {
                        int indent = (rowIndexHierarchy.Count - 1)*2;
                        rowText[rowText.Count - 1] = new string(' ', indent) + rowText[rowText.Count - 1];
                    }
                }

                exportTable.Add(rowText);
            }

            return exportTable;
        }

        protected void ExportTable(object sender, EventArgs e)
        {
            exportSelectedCellsOnly = sender == copyToClipboardSelectedToolStripMenuItem ||
                                      sender == exportSelectedCellsToFileToolStripMenuItem ||
                                      sender == showInExcelSelectToolStripMenuItem;

            /*var progressWindow = new Form
            {
                Size = new Size(300, 60),
                Text = "Exporting...",
                StartPosition = FormStartPosition.CenterScreen,
                ControlBox = false
            };
            var progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee
            };
            progressWindow.Controls.Add(progressBar);
            progressWindow.Show();*/

            if (sender == clipboardToolStripMenuItem ||
                sender == copyToClipboardSelectedToolStripMenuItem)
                TableExporter.CopyToClipboard(this);
            else if (sender == fileToolStripMenuItem ||
                     sender == exportSelectedCellsToFileToolStripMenuItem)
                TableExporter.ExportToFile(this);
            else if (sender == showInExcelToolStripMenuItem ||
                     sender == showInExcelSelectToolStripMenuItem)
            {
                var exportWrapper = new Dictionary<string, TableExporter.ITable> { { Name, this } };
                TableExporter.ShowInExcel(exportWrapper, false);
            }
        }

        protected void clipboardToolStripMenuItem_Click (object sender, EventArgs e)
        {

        }

        protected void fileToolStripMenuItem_Click (object sender, EventArgs e)
        {

        }

        protected void exportButton_Click (object sender, EventArgs e)
        {
            copyToClipboardSelectedToolStripMenuItem.Enabled =
                exportSelectedCellsToFileToolStripMenuItem.Enabled =
                showInExcelSelectToolStripMenuItem.Enabled = treeDataGridView.SelectedCells.Count > 0;

            exportMenu.Show(Cursor.Position);
        }

        protected void showInExcelToolStripMenuItem_Click (object sender, EventArgs e)
        {

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
                treeDataGridView.BackgroundColor = treeDataGridView.GridColor = treeDataGridView.DefaultCellStyle.BackColor = ccf.DefaultBackColor ?? SystemColors.Window;

                treeDataGridView.Refresh();
            }
        }

        protected void treeDataGridView_DataError (object sender, DataGridViewDataErrorEventArgs e)
        {
            Program.HandleException(e.Exception);
            e.ThrowException = false;
        }

        public void ClearSession()
        {
            ClearData();
            if (session != null && session.IsOpen)
            {
                session.Close();
                session.Dispose();
                session = null;
            }
        }

        private void findTextBox_Leave(object sender, EventArgs e)
        {
            // add a "Find..." placeholder and make the text gray
            if (findTextBox.Text.IsNullOrEmpty())
            {
                findTextBox.Text = "Find...";
                findTextBox.ForeColor = SystemColors.GrayText;
                if (findTextBox.Tag.ToString().IsNullOrEmpty())
                    return;
            }

            if (findTextBox.Text.ToString() == findTextBox.Tag.ToString())
                return;

            findTextBox.Tag = findTextBox.Text;

            saveSelectionPath();
            applyFindFilter();
            treeDataGridView.RootRowCount = rows.Count();
            treeDataGridView.Refresh();
            restoreSelectionPath();
        }

        private void findTextBox_Enter(object sender, EventArgs e)
        {
            // delete the "Find..." placeholder and make the text color normal
            if (findTextBox.Text == "Find...")
            {
                findTextBox.Text = String.Empty;
                findTextBox.ForeColor = SystemColors.WindowText;
            }
        }

        private void findTextBox_EnterPressed(object sender, EventArgs e)
        {
            if (findTextBox.Text.IsNullOrEmpty() && findTextBox.Tag.ToString() == "Find..." ||
                findTextBox.Text.ToString() == findTextBox.Tag.ToString())
                return;

            findTextBox.Tag = findTextBox.Text;

            saveSelectionPath();
            applyFindFilter();
            treeDataGridView.RootRowCount = rows.Count();
            treeDataGridView.Refresh();
            restoreSelectionPath();
        }
    }

    public class PivotData
    {
        public bool IsArray { get; private set; }
        public object Value { get; private set; }

        #region Constructor
        public PivotData() { }
        public PivotData(object[] queryRow)
        {
            var value = queryRow[2]; // 3rd column is expected to contain the BLOB array
            IsArray = false;
            if (value is int || value is long)
                Value = Convert.ToInt32(value);
            else if (value is float || value is double)
                Value = Convert.ToDouble(value);
            else if (value == DBNull.Value || value == null)
                Value = null;
            else if (value is byte[])
            {
                byte[] arrayBytes = value as byte[];
                if (arrayBytes == null || arrayBytes.Length % 8 > 0)
                    throw new ArgumentException("PivotData assumes that BLOBs are arrays of double precision floats");

                int arrayLength = arrayBytes.Length / 8;

                double[] arrayValues = new double[arrayLength];
                var arrayStream = new BinaryReader(new MemoryStream(arrayBytes));
                for (int i = 0; i < arrayLength; ++i)
                    arrayValues[i] += arrayStream.ReadDouble();
                Value = arrayValues;
                IsArray = true;
            }
            else
                throw new ArgumentException("invalid pivot column type " + value.GetType().Name);
        }
        #endregion
    }
}
