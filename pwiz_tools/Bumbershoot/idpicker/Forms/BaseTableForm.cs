//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
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
using DigitalRune.Windows.Docking;
using NHibernate.Linq;
using PopupControl;
using IDPicker;
using IDPicker.DataModel;
using IDPicker.Controls;

namespace IDPicker.Forms
{
    public partial class BaseTableForm : DockableForm
    {
        public BaseTableForm()
            : base()
        {
            InitializeComponent();

            sortColumns = new List<SortColumn>();
            updateGroupings(new FormProperty());
            updatePivots(new FormProperty());

            treeDataGridView.DefaultCellStyleChanged += treeDataGridView_DefaultCellStyleChanged;
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

        public virtual void SetData(NHibernate.ISession session, DataFilter viewFilter) { throw new NotImplementedException(); }
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

        protected NHibernate.ISession session;

        protected DataFilter viewFilter; // what the user has filtered on
        protected DataFilter dataFilter; // how this view is filtered (i.e. never on its own rows)
        protected DataFilter basicDataFilter; // the basic filter without the user filtering on rows

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
        }

        protected IList<Row> rows, basicRows;

        protected virtual RowFilterState getRowFilterState(Row parentRow) { throw new NotImplementedException(); }

        protected override void OnLoad(EventArgs e)
        {
            treeDataGridView_DefaultCellStyleChanged(this, EventArgs.Empty);

            base.OnLoad(e);

            treeDataGridView.ClearSelection();
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

        protected void pivotSetupButton_Click(object sender, EventArgs e) { pivotSetupPopup.Show(sender as Button); }

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

        protected void groupingSetupButton_Click(object sender, EventArgs e) { groupingSetupPopup.Show(sender as Button); }

        protected void groupingSetupControl_GroupingChanged(object sender, EventArgs e) { dirtyGroupings = true; }

        protected void groupingSetupPopup_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            if (dirtyGroupings)
            {
                dirtyGroupings = false;
                checkedGroupings = groupingSetupControl.CheckedGroupings;

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

        protected virtual void OnGroupingChanged(object sender, EventArgs e) { resetData(); }

        public void Sort(int columnIndex)
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
                _columnSettings[column].Visible = true;
                _columnSettings[column].BackColor = formProperty.BackColor;
                _columnSettings[column].ForeColor = formProperty.ForeColor;
            }

            treeDataGridView.ForeColor = treeDataGridView.DefaultCellStyle.ForeColor = formProperty.ForeColor ?? SystemColors.WindowText;
            treeDataGridView.BackgroundColor = treeDataGridView.DefaultCellStyle.BackColor = formProperty.BackColor ?? SystemColors.Window;

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

        public virtual List<List<string>> GetFormTable ()
        {
            return GetFormTable(false, String.Empty);
        }

        public virtual List<List<string>> GetFormTable (bool htmlFormat, string reportName)
        {
            var exportTable = new List<List<string>>();
            IList<int> exportedRows, exportedColumns;

            if (treeDataGridView.SelectedCells.Count > 0 && !treeDataGridView.AreAllCellsSelected(false))
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
                var row = rows[rowIndex];
                var rowIndexHierarchy = treeDataGridView.GetRowHierarchyForRowIndex(rowIndex);

                // skip non-root rows or filtered rows
                if (rowIndexHierarchy.Count > 1 || getRowFilterState(row) == RowFilterState.Out)
                    continue;

                var rowText = new List<string>();
                foreach (var columnIndex in exportedColumns)
                {
                    object value = treeDataGridView[columnIndex, rowIndex].Value ?? String.Empty;
                    rowText.Add(value.ToString());
                }

                exportTable.Add(rowText);
            }

            return exportTable;
        }

        protected void clipboardToolStripMenuItem_Click (object sender, EventArgs e)
        {
            var table = GetFormTable();

            TableExporter.CopyToClipboard(table);
        }

        protected void fileToolStripMenuItem_Click (object sender, EventArgs e)
        {
            var table = GetFormTable();

            TableExporter.ExportToFile(table);
        }

        protected void exportButton_Click (object sender, EventArgs e)
        {
            if (treeDataGridView.SelectedRows.Count > 1)
            {
                exportMenu.Items[0].Text = "Copy Selected to Clipboard";
                exportMenu.Items[1].Text = "Export Selected to File";
                exportMenu.Items[2].Text = "Show Selected in Excel";
            }
            else
            {
                exportMenu.Items[0].Text = "Copy to Clipboard";
                exportMenu.Items[1].Text = "Export to File";
                exportMenu.Items[2].Text = "Show in Excel";
            }

            exportMenu.Show(Cursor.Position);
        }

        protected void showInExcelToolStripMenuItem_Click (object sender, EventArgs e)
        {
            var table = GetFormTable();

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
    }
}
