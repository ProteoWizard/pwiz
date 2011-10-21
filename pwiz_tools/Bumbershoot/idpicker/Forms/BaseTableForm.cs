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
    public partial class BaseTableForm<GroupByType, PivotByType> : DockableForm
    {
        public BaseTableForm()
            : base()
        {
            InitializeComponent();

            sortColumns = new List<SortColumn>();
            updateGroupings(new List<ColumnProperty>());
            updatePivots(new List<ColumnProperty>());

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

        public GroupingSetupControl<GroupByType> GroupingSetupControl
        {
            get { return groupingSetupControl; }
        }

        public PivotSetupControl<PivotByType> PivotSetupControl
        {
            get { return pivotSetupControl; }
        }

        public virtual void SetData(NHibernate.ISession session, DataFilter viewFilter) { throw new NotImplementedException(); }
        public virtual void ClearData() { throw new NotImplementedException(); }
        public virtual void ClearData(bool clearBasicFilter) { throw new NotImplementedException(); }

        protected void setGroupings(params Grouping<GroupByType>[] groupings)
        {
            groupingSetupControl = new GroupingSetupControl<GroupByType>(groupings);
            groupingSetupControl.GroupingChanged += groupingSetupControl_GroupingChanged;
            groupingSetupPopup = new Popup(groupingSetupControl) {FocusOnOpen = true};
            groupingSetupPopup.Closed += groupingSetupPopup_Closed;

            checkedGroupings = groupingSetupControl.CheckedGroupings;
        }

        protected void setPivots(params Pivot<PivotByType>[] pivots)
        {
            pivotSetupControl = new PivotSetupControl<PivotByType>(pivots);
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

        protected GroupingSetupControl<GroupByType> groupingSetupControl;
        protected Popup groupingSetupPopup;
        protected bool dirtyGroupings = false;
        protected IList<Grouping<GroupByType>> checkedGroupings;

        protected PivotSetupControl<PivotByType> pivotSetupControl;
        protected Popup pivotSetupPopup;
        protected bool dirtyPivots = false;
        protected IList<Pivot<PivotByType>> checkedPivots;

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

        protected virtual bool updatePivots (IList<ColumnProperty> listOfSettings) { throw new NotImplementedException(); }
        protected virtual bool updateGroupings (IList<ColumnProperty> listOfSettings) { throw new NotImplementedException(); }

        public void LoadLayout (IList<ColumnProperty> listOfSettings)
        {
            if (!listOfSettings.Any())
                return;

            //_unusedPivotSettings = listOfSettings.Where(x => x.Type == "PivotColumn").ToList();

            var columnlist = _columnSettings.Keys.ToList();
            var untouchedColumns = _columnSettings.Keys.ToList();

            var backColor = listOfSettings.Where(x => x.Name == "BackColor").SingleOrDefault();
            var textColor = listOfSettings.Where(x => x.Name == "TextColor").SingleOrDefault();

            foreach (var column in columnlist)
            {
                var rowSettings = listOfSettings.Where(x => x.Name == column.Name).SingleOrDefault();

                //if rowSettings is null it is likely an unsaved pivotColumn, keep defualt
                if (rowSettings == null)
                    continue;

                //if (_unusedPivotSettings.Contains(rowSettings))
                //    _unusedPivotSettings.Remove(rowSettings);

                _columnSettings[column] = new ColumnProperty
                                              {
                                                  Scope = this.Name,
                                                  Name = rowSettings.Name,
                                                  Type = rowSettings.Type,
                                                  DecimalPlaces = rowSettings.DecimalPlaces,
                                                  ColorCode = rowSettings.ColorCode,
                                                  Visible = rowSettings.Visible,
                                                  Locked = rowSettings.Locked
                                              };

                untouchedColumns.Remove(column);
            }

            //Set unspecified columns (most likely pivotColumns) to blend in better
            foreach (var column in untouchedColumns)
            {
                _columnSettings[column].Visible = true;
                _columnSettings[column].ColorCode = backColor.ColorCode;
            }

            treeDataGridView.DefaultCellStyle.BackColor = Color.FromArgb(backColor.ColorCode);
            treeDataGridView.DefaultCellStyle.ForeColor = Color.FromArgb(textColor.ColorCode);

            var sortColumnSettings = listOfSettings.SingleOrDefault(o => o.Name == "SortColumnSettings");
            if (sortColumnSettings != null && sortColumnSettings.Type.Length > 0)
            {
                var newSortColumns = new List<SortColumn>();
                foreach (string sortColumnSetting in sortColumnSettings.Type.Split(';'))
                {
                    string[] tokens = sortColumnSetting.Split('§');
                    newSortColumns.Add(new SortColumn()
                    {
                        Index = Int32.Parse(tokens[0]),
                        Order = (SortOrder) Enum.Parse(typeof(SortOrder), tokens[1])
                    });
                }
                Sort(newSortColumns.First().Index);
                sortColumns = newSortColumns;
            }

            bool pivotsChanged = updatePivots(listOfSettings);
            bool groupingChanged = updateGroupings(listOfSettings);

            foreach (var kvp in _columnSettings)
                kvp.Key.Visible = kvp.Value.Visible;
            treeDataGridView.Refresh();

            if (pivotsChanged || groupingChanged)
                resetData();
            else
                applySort();
        }

        public List<ColumnProperty> GetCurrentProperties (bool pivotToo)
        {
            var currentList = new List<ColumnProperty>();

            foreach (var kvp in _columnSettings)
                kvp.Value.Visible = false;
            foreach (var column in treeDataGridView.Columns.Cast<DataGridViewColumn>().Where(o => o.Visible))
                if (_columnSettings.ContainsKey(column))
                    _columnSettings[column].Visible = true;

            foreach (var kvp in _columnSettings)
            {
                currentList.Add(new ColumnProperty
                {
                    Scope = this.Name,
                    Name = kvp.Key.Name,
                    Type = kvp.Value.Type,
                    DecimalPlaces = kvp.Value.DecimalPlaces,
                    ColorCode = kvp.Value.ColorCode,
                    Visible = kvp.Value.Visible,
                    Locked = null
                });
            }

            if (!pivotToo)
                currentList.RemoveAll(x => x.Type == "PivotColumn");

            currentList.Add(new ColumnProperty
            {
                Scope = this.Name,
                Name = "BackColor",
                Type = "GlobalSetting",
                DecimalPlaces = -1,
                ColorCode = treeDataGridView.DefaultCellStyle.BackColor.ToArgb(),
                Visible = false,
                Locked = null
            });
            currentList.Add(new ColumnProperty
            {
                Scope = this.Name,
                Name = "TextColor",
                Type = "GlobalSetting",
                DecimalPlaces = -1,
                ColorCode = treeDataGridView.DefaultCellStyle.ForeColor.ToArgb(),
                Visible = false,
                Locked = null
            });
            currentList.Add(new ColumnProperty
            {
                Scope = this.Name,
                Name = "SortColumnSettings",
                Type = String.Join(";", sortColumns.Select(o => String.Format("{0}§{1}", o.Index, o.Order)).ToArray()),
                DecimalPlaces = 0,
                ColorCode = 0,
                Visible = false,
                Locked = null
            });
            currentList.Add(new ColumnProperty
            {
                Scope = this.Name,
                Name = "PivotModes",
                Type = String.Join(";", checkedPivots.Select(o => o.Mode.ToString()).ToArray()),
                DecimalPlaces = 0,
                ColorCode = 0,
                Visible = false,
                Locked = null
            });
            currentList.Add(new ColumnProperty
            {
                Scope = this.Name,
                Name = "GroupingModes",
                Type = String.Join(";", groupingSetupControl.Groupings.Select(o => String.Format("{0}§{1}§{2}", o.Checked, o.Mode.ToString(), o.Text)).ToArray()),
                DecimalPlaces = 0,
                ColorCode = 0,
                Visible = false,
                Locked = null
            });

            return currentList;
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
            /*Color[] currentColors = { treeDataGridView.DefaultCellStyle.BackColor, treeDataGridView.DefaultCellStyle.ForeColor };

            foreach (var kvp in _columnSettings)
                kvp.Value.Visible = kvp.Key.Visible;

            var ccf = new ColumnControlForm(_columnSettings, currentColors);

            if (ccf.ShowDialog() == DialogResult.OK)
            {
                _columnSettings = ccf.SavedSettings;

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = kvp.Value.Visible;

                treeListView.BackColor = ccf.WindowBackColorBox.BackColor;
                treeListView.ForeColor = ccf.WindowTextColorBox.BackColor;
                treeListView.HyperlinkStyle.Normal.ForeColor = ccf.WindowTextColorBox.BackColor;
                treeListView.HyperlinkStyle.Visited.ForeColor = ccf.WindowTextColorBox.BackColor;

                SetColumnAspectGetters();
                treeListView.RebuildColumns();

                if (session != null)
                    SetData(session, dataFilter);
            }*/
        }
    }
}
