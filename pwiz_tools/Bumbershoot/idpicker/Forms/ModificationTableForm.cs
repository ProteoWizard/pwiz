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
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using DigitalRune.Windows.Docking;
using IDPicker.DataModel;
using IDPicker.Controls;
using pwiz.CLI.data;
using proteome = pwiz.CLI.proteome;

namespace IDPicker.Forms
{
    using DataFilter = DataModel.DataFilter;

    public partial class ModificationTableForm : DockableForm
    {
        public DataGridView DataGridView { get { return dataGridView; } }

        public ModificationTableForm ()
        {
            InitializeComponent();

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                e.Cancel = true;
                DockState = DockState.DockBottomAutoHide;
            };

            Text = TabText = "Modification View";

            dataGridView.PreviewCellClick += dataGridView_PreviewCellClick;
            dataGridView.CellDoubleClick += dataGridView_CellDoubleClick;
            dataGridView.KeyDown += dataGridView_KeyDown;
            dataGridView.CellFormatting += dataGridView_CellFormatting;
            dataGridView.DefaultCellStyleChanged += dataGridView_DefaultCellStyleChanged;

            dataGridView.ShowCellToolTips = true;
            dataGridView.CellToolTipTextNeeded += dataGridView_CellToolTipTextNeeded;
            dataGridView.CellPainting += new DataGridViewCellPaintingEventHandler(dataGridView_CellPainting);
            brush = new SolidBrush(dataGridView.ForeColor);

            // TODO: add display settings dialog like other forms have
            var style = dataGridView.DefaultCellStyle;
            filteredOutColor = style.ForeColor.Interpolate(style.BackColor, 0.5f);
        }

        const string deltaMassColumnName = "ΔMass";

        Brush brush;
        void dataGridView_CellPainting (object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 || e.ColumnIndex >= 0)
                return;

            e.Paint(e.CellBounds, e.PaintParts);
            SizeF textSize = e.Graphics.MeasureString(deltaMassColumnName, dataGridView.Font);
            Rectangle textBounds = e.CellBounds;
            textBounds.Offset((int) Math.Round(textSize.Width / 2), (int) Math.Round(textSize.Height / 3));
            e.Graphics.DrawString(deltaMassColumnName, dataGridView.Font, brush, textBounds);
            e.Handled = true;
        }

        int rowSortColumnIndex = -1, columnSortRowIndex = -1;
        SortOrder rowSortOrder = SortOrder.Descending, columnSortOrder = SortOrder.None;
        void dataGridView_PreviewCellClick (object sender, DataGridViewPreviewCellClickEventArgs e)
        {
            // ignore double-clicks
            if (e.Clicks > 1)
                return;

            // clicking on top-left cell sorts by delta mass;
            // clicking on other column header sorts by count for the site
            if (e.RowIndex < 0)
            {
                // initial sort is descending except for delta mass
                SortOrder initialSortOrder = e.ColumnIndex < 0 ? SortOrder.Ascending : SortOrder.Descending;
                if (rowSortColumnIndex != e.ColumnIndex)
                    rowSortOrder = initialSortOrder;
                else if (rowSortOrder == SortOrder.Ascending)
                    rowSortOrder = SortOrder.Descending;
                else
                    rowSortOrder = SortOrder.Ascending;

                rowSortColumnIndex = e.ColumnIndex;

                var column = e.ColumnIndex < 0 ? dataGridView.Columns[0] : dataGridView.Columns[e.ColumnIndex];
                dataGridView.Sort(column, rowSortOrder == SortOrder.Ascending ? ListSortDirection.Ascending
                                                                              : ListSortDirection.Descending);

                foreach (DataGridViewRow row in dataGridView.Rows)
                    row.HeaderCell.Value = (row.DataBoundItem as DataRowView).Row[deltaMassColumnName].ToString();

                e.Handled = true;
            }
            // clicking on row header sorts by count for the delta mass
            else if (e.ColumnIndex < 0)
            {
                var row = dataGridView.Rows[e.RowIndex];

                // build a map of columns by spectrum count (skip mass and total columns)
                var columnsBySiteAndSpectrumCount = new Map<int, Map<string, DataGridViewColumn>>();
                for (int i = 2; i < dataGridView.Columns.Count; ++i)
                {
                    var site = dataGridView.Columns[i].Name;
                    var spectrumCount = row.Cells[i].Value is int ? (int) row.Cells[i].Value : 0;
                    columnsBySiteAndSpectrumCount[spectrumCount][site] = dataGridView.Columns[i];
                }

                // initial sort is descending
                if (columnSortRowIndex != e.RowIndex ||
                    columnSortOrder == SortOrder.None ||
                    columnSortOrder == SortOrder.Ascending)
                    columnSortOrder = SortOrder.Descending;
                else
                    columnSortOrder = SortOrder.Ascending;

                columnSortRowIndex = e.RowIndex;

                var columns = columnSortOrder == SortOrder.Descending ? columnsBySiteAndSpectrumCount.Values.Reverse()
                                                                      : columnsBySiteAndSpectrumCount.Values;

                // assign display index in order of spectrum count (site is tie-breaker)
                int displayIndex = 1; // start after mass and total columns
                foreach (var itr in columns)
                    foreach (var itr2 in itr)
                        itr2.Value.DisplayIndex = ++displayIndex;
                e.Handled = true;
            }
        }

        void dataGridView_CellDoubleClick (object sender, DataGridViewCellEventArgs e)
        {
            // if no one is listening, do nothing
            if (ModificationViewFilter == null)
                return;

            // ignore header cells
            if (e.ColumnIndex < 0 || e.RowIndex < 0)
                return;

            var cell = dataGridView[e.ColumnIndex, e.RowIndex];

            // if the clicked cell is blank, don't apply a filter
            if (cell.Value == DBNull.Value)
                return;

            var newDataFilter = new DataFilter() { FilterSource = this };

            char? site = null;
            if (e.ColumnIndex > 0 && this.siteColumnNameToSite.Contains(cell.OwningColumn.HeaderText))
                site = this.siteColumnNameToSite[cell.OwningColumn.HeaderText];

            if (site != null)
                newDataFilter.ModifiedSite = new List<char> { site.Value };

            newDataFilter.Modifications = session.CreateQuery(
                                                "SELECT pm.Modification " +
                                                "FROM PeptideSpectrumMatch psm JOIN psm.Modifications pm " +
                                                " WHERE ROUND(pm.Modification.MonoMassDelta)=" + cell.OwningRow.HeaderCell.Value.ToString() +
                                                (site != null ? " AND pm.Site='" + site + "'" : "") +
                                                " GROUP BY pm.Modification.id")
                                               .List<DataModel.Modification>();

            if (newDataFilter.Modifications.Count == 0)
                throw new InvalidDataException("no modifications found at the rounded mass");

            // send filter event
            ModificationViewFilter(this, newDataFilter);
        }

        void dataGridView_KeyDown (object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.Handled = true;
            var siteList = new List<string>();
            var modList = new List<string>();
            var newDataFilter = new DataFilter
            {
                FilterSource = this,
                ModifiedSite = new List<char>()
            };

            foreach (DataGridViewCell cell in dataGridView.SelectedCells)
            {
                // if the clicked cell is blank, don't apply a filter
                if (cell.Value == DBNull.Value)
                    continue;

                // ignore top-left cell
                if (cell.ColumnIndex == 0 && cell.RowIndex < 0)
                    continue;

                char? newSite = null;
                if (cell.ColumnIndex > 0 && siteColumnNameToSite.Contains(cell.OwningColumn.HeaderText))
                    newSite = siteColumnNameToSite[cell.OwningColumn.HeaderText];

                if (newSite != null && !newDataFilter.ModifiedSite.Contains(newSite.Value))
                {
                    siteList.Add("pm.Site='" + newSite.ToString() + "'");
                    newDataFilter.ModifiedSite.Add(newSite.Value);
                }
                if (!modList.Contains(cell.OwningRow.Cells[0].Value.ToString()))
                    modList.Add("ROUND(pm.Modification.MonoMassDelta)=" + cell.OwningRow.Cells[0].Value.ToString());
            }

            if (newDataFilter.ModifiedSite.Count == 0)
                newDataFilter.ModifiedSite = null;

            newDataFilter.Modifications = session.CreateQuery(
                "SELECT pm.Modification " +
                "FROM PeptideSpectrumMatch psm JOIN psm.Modifications pm " +
                " WHERE (" + string.Join(" OR ", modList.ToArray()) +
                (siteList.Count > 0 ? ") AND (" + string.Join(" OR ", siteList.ToArray()) + ")" : ")") +
                " GROUP BY pm.Modification.id")
                .List<DataModel.Modification>();

            // send filter event
            ModificationViewFilter(this, newDataFilter);
        }

        public event ModificationViewFilterEventHandler ModificationViewFilter;

        private NHibernate.ISession session;

        private DataFilter viewFilter; // what the user has filtered on
        private DataFilter dataFilter; // how this view is filtered (i.e. never on its own rows)
        private DataFilter basicDataFilter; // the basic filter without the user filtering on rows

        private Color filteredOutColor;

        private Map<string, char> siteColumnNameToSite;
        private DataTable deltaMassTable, basicDeltaMassTable;
        private int totalModifications, basicTotalModifications;

        private Map<string, Map<double, List<unimod.Modification>>> basicDeltaMassAnnotations;

        // TODO: support multiple selected cells
        Pair<double, string> oldSelectedAddress = null;

        public string GetSiteColumnName (char site)
        {
            if (site == '(')
                return "N-term";
            else if (site == ')')
                return "C-term";
            else
                return site.ToString();
        }

        public char GetSiteFromColumnName (string columnName)
        {
            if (columnName == "N-term")
                return '(';
            else if (columnName == "C-term")
                return ')';
            else
                return columnName[0];
        }

        private DataTable createDeltaMassTableFromQuery (IList<object[]> queryRows, out int totalModifications, out Map<string, char> siteColumnNameToSite)
        {
            DataTable deltaMassTable = new DataTable();
            deltaMassTable.BeginLoadData();
            deltaMassTable.Columns.Add(new DataColumn() { ColumnName = deltaMassColumnName, DataType = typeof(double) });
            deltaMassTable.PrimaryKey = new DataColumn[] { deltaMassTable.Columns[0] };
            deltaMassTable.DefaultView.Sort = deltaMassColumnName;

            siteColumnNameToSite = new Map<string, char>();

            totalModifications = 0;

            var totalColumn = new DataColumn() { ColumnName = "Total", DataType = typeof(int) };
            deltaMassTable.Columns.Add(totalColumn);

            foreach (var tuple in queryRows)
            {
                var mod = tuple[1] as DataModel.Modification;
                double roundedMass = Math.Round(mod.MonoMassDelta);
                char site = (char) tuple[0];
                string siteColumnName = GetSiteColumnName(site);

                if (!deltaMassTable.Columns.Contains(siteColumnName))
                {
                    deltaMassTable.Columns.Add(new DataColumn() { ColumnName = siteColumnName, DataType = typeof(int) });
                    siteColumnNameToSite[siteColumnName] = site;
                }

                DataRow row;
                if (!deltaMassTable.Rows.Contains(roundedMass))
                {
                    row = deltaMassTable.NewRow();
                    row[deltaMassColumnName] = roundedMass;
                    row[totalColumn] = 0;
                    deltaMassTable.Rows.Add(row);
                }
                else
                    row = deltaMassTable.Rows.Find(roundedMass);

                row[siteColumnName] = Convert.ToInt32(tuple[2]);
                row[totalColumn] = (int) row[totalColumn] + (int) row[siteColumnName];
                totalModifications += (int) row[siteColumnName];
            }
            deltaMassTable.AcceptChanges();
            deltaMassTable.EndLoadData();

            return deltaMassTable;
        }

        private void findDeltaMassAnnotations ()
        {
            basicDeltaMassAnnotations = new Map<string, Map<double, List<unimod.Modification>>>();

            foreach (DataRow deltaMassRow in basicDeltaMassTable.Rows)
                foreach (DataColumn siteColumn in basicDeltaMassTable.Columns)
                {
                    if (siteColumn.ColumnName == "Total")
                        continue;

                    double deltaMass = (double) deltaMassRow[deltaMassColumnName];

                    char deltaMassSite;
                    if (siteColumn.ColumnName == "N-term")
                        deltaMassSite = 'n';
                    else if (siteColumn.ColumnName == "C-term")
                        deltaMassSite = 'c';
                    else
                        deltaMassSite = siteColumn.ColumnName[0];

                    unimod.Filter filter = new unimod.Filter(deltaMass, 1) { site = unimod.site(deltaMassSite), approved = true };
                    var possibleAnnotations = unimod.modifications(filter);
                    if (possibleAnnotations.Count > 0)
                    {
                        var possibleAnnotationList = basicDeltaMassAnnotations[siteColumn.ColumnName][deltaMass];
                        foreach (var annotation in possibleAnnotations)
                            possibleAnnotationList.Add(annotation);
                    }
                }

            // this seems to prevent some intermittent crashes with the pwiz interop
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void SetData (NHibernate.ISession session, DataFilter dataFilter)
        {
            this.session = session;
            viewFilter = dataFilter;
            this.dataFilter = new DataFilter(dataFilter) { Modifications = null, ModifiedSite = null };

            if (dataGridView.SelectedCells.Count > 0)
                oldSelectedAddress = new Pair<double, string>()
                {
                    first = (double) dataGridView.SelectedCells[0].OwningRow.Cells[0].Value,
                    second = dataGridView.SelectedCells[0].OwningColumn.Name
                };

            ClearData();

            Text = TabText = "Loading modification view...";

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
            Text = TabText = "Modification View";

            dataGridView.DataSource = null;
            dataGridView.Columns.Clear();
            dataGridView.Refresh();
            Refresh();
        }

        public void ClearData (bool clearBasicFilter)
        {
            if (clearBasicFilter)
            {
                basicDataFilter = null;
                basicDeltaMassTable = null;
                basicDeltaMassAnnotations = null;
            }
            ClearData();
        }

        void setData (object sender, DoWorkEventArgs e)
        {
            try
            {
                var query = session.CreateQuery("SELECT pm.Site, pm.Modification, COUNT(DISTINCT psm.Spectrum) " +
                                                dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                  DataFilter.PeptideSpectrumMatchToPeptideModification) +
                                                "GROUP BY pm.Site, ROUND(pm.Modification.MonoMassDelta) " +
                                                "ORDER BY ROUND(pm.Modification.MonoMassDelta)");
                query.SetReadOnly(true);
                if (dataFilter.IsBasicFilter || viewFilter.Modifications != null || viewFilter.ModifiedSite != null)
                {
                    // refresh basic data when basicDataFilter is unset or when the basic filter values have changed
                    if (basicDataFilter == null || (dataFilter.IsBasicFilter && dataFilter != basicDataFilter))
                    {
                        basicDataFilter = new DataFilter(dataFilter);
                        IList<object[]> queryRows; lock (session) queryRows = query.List<object[]>();
                        basicDeltaMassTable = createDeltaMassTableFromQuery(queryRows, out basicTotalModifications, out siteColumnNameToSite);
                        findDeltaMassAnnotations();
                    }

                    deltaMassTable = basicDeltaMassTable;
                    totalModifications = basicTotalModifications;
                }
                else
                {
                    Map<string, char> dummy;
                    IList<object[]> queryRows; lock (session) queryRows = query.List<object[]>();
                    deltaMassTable = createDeltaMassTableFromQuery(queryRows, out totalModifications, out dummy);
                }
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Exception)
                Program.HandleException(e.Result as Exception);

            Text = TabText = String.Format("Modification View: {0} modifications", totalModifications);

            dataGridView.DataSource = deltaMassTable;
            dataGridView.Columns[deltaMassColumnName].Visible = false;

            try
            {
                if (deltaMassTable.Rows.Count > 0)
                {
                    dataGridView[0, 0].Selected = false;
                    if (oldSelectedAddress != null)
                    {
                        string columnName = oldSelectedAddress.second;
                        int rowIndex = deltaMassTable.DefaultView.Find(oldSelectedAddress.first);
                        if (dataGridView.Columns.Contains(columnName) && rowIndex != -1)
                        {
                            dataGridView.FirstDisplayedCell = dataGridView[columnName, rowIndex];
                            dataGridView.FirstDisplayedCell.Selected = true;
                        }
                    }
                }
            }
            catch
            {
            }

            dataGridView.Refresh();

            foreach (DataGridViewRow row in dataGridView.Rows)
                row.HeaderCell.Value = (row.DataBoundItem as DataRowView).Row[deltaMassColumnName].ToString();
        }

        private void dataGridView_DefaultCellStyleChanged (object sender, EventArgs e)
        {
        }

        /// <summary>
        /// Highlights cells with different colors based on their values. 
        /// TODO: User-configurable.
        /// </summary>
        private void dataGridView_CellFormatting (object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.Value != null && e.ColumnIndex > 0 && e.Value is int)
            {
                bool hasAnnotations = false;
                double deltaMass = (double) dataGridView.Rows[e.RowIndex].Cells[deltaMassColumnName].Value;
                string deltaMassSite = dataGridView.Columns[e.ColumnIndex].Name;

                bool isResidueMass = false;
                if (deltaMassSite.Length == 1)
                {
                    double residueMass = proteome.AminoAcidInfo.record(deltaMassSite[0]).residueFormula.monoisotopicMass();
                    isResidueMass = Math.Abs(Math.Abs(deltaMass) - residueMass) < 1;
                }

                // set background color based on mod prevalence
                int val = (int) e.Value;
                if (val > 10 && val < 50)
                    e.CellStyle.BackColor = Color.PaleGreen;
                else if (val >= 50 && val < 100)
                    e.CellStyle.BackColor = Color.DeepSkyBlue;
                else if (val >= 100)
                    e.CellStyle.BackColor = Color.OrangeRed;

                // set foreground color based on whether the cell is included in the current view filter
                bool filterIncludesMod = true;
                if (viewFilter.Modifications != null) filterIncludesMod = viewFilter.Modifications.Any(o => Math.Abs(Math.Abs(deltaMass) - Math.Abs(Math.Round(o.MonoMassDelta))) < 1);
                if (viewFilter.ModifiedSite != null) filterIncludesMod = filterIncludesMod && viewFilter.ModifiedSite.Contains(GetSiteFromColumnName(deltaMassSite));

                if (!filterIncludesMod)
                {
                    e.CellStyle.ForeColor = filteredOutColor;
                    e.CellStyle.BackColor = e.CellStyle.BackColor.Interpolate(dataGridView.DefaultCellStyle.BackColor, 0.5f);
                }

                if (basicDeltaMassAnnotations != null)
                {
                    var itr = basicDeltaMassAnnotations.Find(deltaMassSite);
                    if (itr.IsValid)
                    {
                        var itr2 = itr.Current.Value.Find(deltaMass);
                        if (itr2.IsValid)
                            hasAnnotations = true;
                    }
                }
                var style = FontStyle.Regular;
                if (hasAnnotations) style = FontStyle.Bold;
                if (isResidueMass) style |= FontStyle.Italic;
                e.CellStyle.Font = new Font(e.CellStyle.Font, style);
            }
        }

        void dataGridView_CellToolTipTextNeeded (object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0)
            {
                if (e.ColumnIndex < 0 && e.RowIndex < 0)
                    e.ToolTipText = "Left-click to sort by delta mass.";
                else if (e.RowIndex < 0)
                    e.ToolTipText = "Left-click to sort rows by this column.";
                else
                    e.ToolTipText = "Left-click to sort columns by this row.";
                return;
            }

            if (basicDeltaMassAnnotations == null)
                return;

            var cell = dataGridView[e.ColumnIndex, e.RowIndex];
            if (cell.Value == null || !(cell.Value is int))
                return;

            var annotation = new StringBuilder();
            var itr = basicDeltaMassAnnotations.Find(dataGridView.Columns[e.ColumnIndex].Name);
            if (itr.IsValid)
            {
                double deltaMass = (double) dataGridView.Rows[e.RowIndex].Cells[deltaMassColumnName].Value;
                var itr2 = itr.Current.Value.Find(deltaMass);
                if (itr2.IsValid)
                    foreach (var mod in itr2.Current.Value)
                        annotation.AppendFormat("{0} (monoisotopic Δmass={1})\r\n", mod.name, mod.deltaMonoisotopicMass);
            }
            e.ToolTipText = annotation.ToString();
        }

        private void clipboardToolStripMenuItem_Click (object sender, EventArgs e)
        {
            var table = GetFormTable(sender == copySelectedCellsToClipboardToolStripMenuItem);

            TableExporter.CopyToClipboard(table);
        }

        private void fileToolStripMenuItem_Click (object sender, EventArgs e)
        {
            var table = GetFormTable(sender == exportSelectedCellsToFileToolStripMenuItem);

            TableExporter.ExportToFile(table);
        }

        private void exportButton_Click (object sender, EventArgs e)
        {
            exportMenu.Show(Cursor.Position);
        }

        public virtual List<List<string>> GetFormTable (bool selected)
        {
            var exportTable = new List<List<string>>();
            IList<int> exportedRows, exportedColumns;

            if (selected && dataGridView.SelectedCells.Count > 0 && !dataGridView.AreAllCellsSelected(false))
            {
                var selectedRows = new Set<int>();
                var selectedColumns = new Map<int, int>(); // ordered by DisplayIndex

                foreach (DataGridViewCell cell in dataGridView.SelectedCells)
                {
                    selectedRows.Add(cell.RowIndex);
                    selectedColumns[cell.OwningColumn.DisplayIndex] = cell.ColumnIndex;
                }

                exportedRows = selectedRows.ToList();
                exportedColumns = selectedColumns.Values;
            }
            else
            {
                exportedRows = dataGridView.Rows.Cast<DataGridViewRow>().Select(o => o.Index).ToList();
                exportedColumns = dataGridView.GetVisibleColumnsInDisplayOrder().Select(o => o.Index).ToList();
            }

            // add column headers
            exportTable.Add(new List<string>());
            exportTable.Last().Add(deltaMassColumnName.Replace("Δ", "Delta "));
            foreach (var columnIndex in exportedColumns)
                exportTable.Last().Add(dataGridView.Columns[columnIndex].HeaderText);

            foreach (int rowIndex in exportedRows)
            {
                var rowText = new List<string>();
                rowText.Add(dataGridView.Rows[rowIndex].HeaderCell.Value.ToString());
                foreach (var columnIndex in exportedColumns)
                {
                    object value = dataGridView[columnIndex, rowIndex].Value ?? String.Empty;
                    rowText.Add(value.ToString());
                }

                exportTable.Add(rowText);
            }

            return exportTable;
        }

        internal List<TreeNode> getModificationTree (string reportName)
        {
            var groupNodes = new List<TreeNode>();

            var query = session.CreateQuery("SELECT pm.Site, pm.Modification, COUNT(DISTINCT psm.Spectrum) " +
                                                dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                  DataFilter.PeptideSpectrumMatchToPeptideModification) +
                                                "GROUP BY pm.Site, ROUND(pm.Modification.MonoMassDelta) " +
                                                "ORDER BY ROUND(pm.Modification.MonoMassDelta)");

            foreach (var tuple in query.List<object[]>())
            {
                var mod = tuple[1] as DataModel.Modification;
                var roundedMass = (int) Math.Round(mod.MonoMassDelta);
                var site = (char) tuple[0];
                var specCount = Convert.ToInt32(tuple[2]);

                var modFilter = new DataFilter(viewFilter)
                                    {
                                        Modifications = session.CreateQuery(
                                            "SELECT pm.Modification " +
                                            "FROM PeptideSpectrumMatch psm JOIN psm.Modifications pm " +
                                            " WHERE ROUND(pm.Modification.MonoMassDelta)=" +
                                            roundedMass +
                                            (" AND pm.Site='" + site + "'") +
                                            " GROUP BY pm.Modification.id")
                                            .List<DataModel.Modification>()
                                    };

                var peptideList = session.CreateQuery(PeptideTableForm.AggregateRow.Selection + ", psm.Peptide, psm, psm.DistinctMatchKey " +
                                                      modFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                      "GROUP BY psm.DistinctMatchKey")
                                    .List<object[]>().Select(o => new PeptideTableForm.DistinctMatchRow(o, dataFilter));
                if (!peptideList.Any()) continue;


                var newNode = new TreeNode
                {
                    Text = site + mod.AvgMassDelta.ToString(),
                    Tag = new[]
                                                    {
                                                        "'" +site + "'", mod.AvgMassDelta.ToString(),
                                                        peptideList.Count().ToString(),
                                                        specCount.ToString()
                                                    }
                };
                foreach (var peptide in peptideList)
                {
                    var cluster = peptide.PeptideSpectrumMatch.Peptide.Instances.First().Protein.Cluster;
                    var subNode = new TreeNode
                    {
                        Text = peptide.DistinctMatch.ToString(),
                        Tag = new[]
                                                        {
                                                            "'" + Text + "'",
                                                            string.Format("'<a href = \"{0}-cluster{1}.html\">{1}</a>'",
                                                                          reportName,cluster),
                                                            peptide.Spectra.ToString(),
                                                        }
                    };
                    newNode.Nodes.Add(subNode);
                }
                groupNodes.Add(newNode);
            }

            return groupNodes;
        }

        private void showInExcelToolStripMenuItem_Click (object sender, EventArgs e)
        {
            var table = GetFormTable(sender == showSelectedCellsInExcelToolStripMenuItem);

            var exportWrapper = new Dictionary<string, List<List<string>>> { { this.Name, table } };

            TableExporter.ShowInExcel(exportWrapper, false);
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
    }

    public delegate void ModificationViewFilterEventHandler (ModificationTableForm sender, DataFilter modificationViewFilter);
}
