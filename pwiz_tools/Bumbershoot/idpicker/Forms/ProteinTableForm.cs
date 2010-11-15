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
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
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
using BrightIdeasSoftware;
using PopupControl;
using IDPicker.DataModel;
using IDPicker.Controls;

namespace IDPicker.Forms
{
    using DataFilter = DataModel.DataFilter;

    public partial class ProteinTableForm : DockableForm
    {
        public TreeListView TreeListView { get { return treeListView; } }

        public enum PivotBy { Group, Source, Off }
        public PivotSetupControl<PivotBy> PivotSetupControl { get { return pivotSetupControl; } }
        private PivotSetupControl<PivotBy> pivotSetupControl;
        private Popup pivotSetupPopup;
        private bool dirtyPivots = false;

        #region Wrapper classes for encapsulating query results

        public class ProteinGroupRow
        {
            public string Proteins { get; private set; }
            public long DistinctPeptides { get; private set; }
            public long DistinctMatches { get; private set; }
            public long Spectra { get; private set; }
            public string ProteinGroup { get; private set; }
            public long FirstProteinId { get; private set; }
            public string FirstProteinDescription { get; set; }
            public long ProteinCount { get; private set; }
            public int? Cluster { get; private set; }
            public double MeanProteinCoverage { get; private set; }

            #region Constructor
            public ProteinGroupRow (object[] queryRow)
            {
                Proteins = (string) queryRow[0];
                DistinctPeptides = (long) queryRow[1];
                DistinctMatches = (long) queryRow[2];
                Spectra = (long) queryRow[3];
                ProteinGroup = (string) queryRow[4];
                FirstProteinId = (long) queryRow[5];
                FirstProteinDescription = (string) queryRow[6];
                ProteinCount = (long) queryRow[7];
                Cluster = (int?) queryRow[8];
                MeanProteinCoverage = (double) queryRow[9];
            }
            #endregion
        }

        public class ProteinRow
        {
            public DataModel.Protein Protein { get; private set; }

            #region Constructor
            public ProteinRow (object x)
            {
                Protein = (DataModel.Protein) x;
            }
            #endregion
        }

        public class ProteinStats
        {
            public long DistinctPeptides { get; private set; }
            public long Spectra { get; private set; }

            #region Constructor
            public ProteinStats () { }
            public ProteinStats (object[] queryRow)
            {
                DistinctPeptides = (long) queryRow[2];
                Spectra = (long) queryRow[3];
            }
            #endregion
        }

        #endregion

        Dictionary<OLVColumn, object[]> _columnSettings;

        public ProteinTableForm ()
        {
            InitializeComponent();

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                e.Cancel = true;
                DockState = DockState.DockBottomAutoHide;
            };

            Text = TabText = "Protein View";

            var pivots = new List<Pivot<PivotBy>>();
            pivots.Add(new Pivot<PivotBy>() { Mode = PivotBy.Group, Text = "Group" });
            pivots.Add(new Pivot<PivotBy>() { Mode = PivotBy.Source, Text = "Source" });

            pivotSetupControl = new PivotSetupControl<PivotBy>(pivots);
            pivotSetupControl.PivotChanged += pivotSetupControl_PivotChanged;
            pivotSetupPopup = new Popup(pivotSetupControl) { FocusOnOpen = true };
            pivotSetupPopup.Closed += new ToolStripDropDownClosedEventHandler(pivotSetupPopup_Closed);

            #region Column aspect getters
            var allLayouts = new List<string>(Util.StringCollectionToStringArray(Properties.Settings.Default.ProteinTableFormSettings));
            _columnSettings = new Dictionary<OLVColumn, object[]>();
            if (allLayouts.Count > 1)
            {
                var retrievedList = allLayouts[1].Split(System.Environment.NewLine.ToCharArray()).ToList();
                if (retrievedList.Count == 10)
                {
                    SetPropertyFromUserSettings(ref _columnSettings, accessionColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, clusterColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, proteinCountColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, coverageColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, distinctPeptidesColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, distinctMatchesColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, filteredSpectraColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, descriptionColumn, retrievedList);

                    treeListView.BackColor = Color.FromArgb(int.Parse(retrievedList[8]));
                    treeListView.ForeColor = Color.FromArgb(int.Parse(retrievedList[9]));

                    //foreach (var kvp in _columnSettings)
                    //    kvp.Key.IsVisible = (bool)kvp.Value[3];
                    //treeListView.RebuildColumns();
                }
                else
                    SetDefaults();
            }
            else
                SetDefaults();

            SetColumnAspectGetters();
            #endregion

            treeListView.UseCellFormatEvents = true;
            treeListView.FormatCell += delegate(object sender, FormatCellEventArgs currentCell)
            {
                if (currentCell.Item.RowObject is ProteinGroupRow &&
                    (viewFilter.Protein != null && viewFilter.Protein.Id != (currentCell.Item.RowObject as ProteinGroupRow).FirstProteinId ||
                     viewFilter.Cluster != null && viewFilter.Cluster != ((currentCell.Item.RowObject as ProteinGroupRow)).Cluster))
                    currentCell.SubItem.ForeColor = SystemColors.GrayText;
                else if (currentCell.Item.RowObject is ProteinRow &&
                         (viewFilter.Protein != null && viewFilter.Protein.Id != (currentCell.Item.RowObject as ProteinRow).Protein.Id ||
                          viewFilter.Cluster != null && viewFilter.Cluster != ((currentCell.Item.RowObject as ProteinRow)).Protein.Cluster))
                    currentCell.SubItem.ForeColor = SystemColors.GrayText;

                if (_columnSettings.ContainsKey(currentCell.Column))
                    currentCell.SubItem.BackColor = (Color) _columnSettings[currentCell.Column][2];
                else
                    currentCell.SubItem.BackColor = treeListView.BackColor;
            };

            treeListView.CanExpandGetter += delegate(object x) { return x is ProteinGroupRow && (x as ProteinGroupRow).ProteinCount > 1; };
            treeListView.ChildrenGetter += delegate(object x)
            {
                return session.CreateQuery(
                    String.Format("SELECT pro FROM Protein pro WHERE pro.Accession IN ('{0}')",
                                  (x as ProteinGroupRow).Proteins.Replace(",", "','")))
                              .List<object>().Select(o => new ProteinRow(o));
            };
            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);

            treeListView.HyperlinkClicked += new EventHandler<HyperlinkClickedEventArgs>(treeListView_HyperlinkClicked);
            treeListView.HyperlinkStyle.Normal.ForeColor = treeListView.ForeColor;
            treeListView.HyperlinkStyle.Visited.ForeColor = treeListView.ForeColor;
        }

        private void SetDefaults()
        {
            _columnSettings.Add(accessionColumn, new object[] { "Key", -99, treeListView.BackColor, false });
            _columnSettings.Add(clusterColumn, new object[] { "Integer", -99, treeListView.BackColor, false });
            _columnSettings.Add(proteinCountColumn, new object[] { "Integer", -99, treeListView.BackColor, false });
            _columnSettings.Add(coverageColumn, new object[] { "Float", -1, treeListView.BackColor, false });
            _columnSettings.Add(distinctPeptidesColumn, new object[] { "Integer", -99, treeListView.BackColor, false });
            _columnSettings.Add(distinctMatchesColumn, new object[] { "Integer", -99, treeListView.BackColor, false });
            _columnSettings.Add(filteredSpectraColumn, new object[] { "Integer", -99, treeListView.BackColor, false });
            _columnSettings.Add(descriptionColumn, new object[] { "String", -99, treeListView.BackColor, false });
        }

        private void SetColumnAspectGetters()
        {
            clusterColumn.AspectGetter = null;
            accessionColumn.AspectGetter = null;
            proteinCountColumn.AspectGetter = null;
            coverageColumn.AspectGetter = null;
            distinctPeptidesColumn.AspectGetter = null;
            distinctMatchesColumn.AspectGetter = null;
            filteredSpectraColumn.AspectGetter = null;
            descriptionColumn.AspectGetter = null;

            clusterColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow)
                    return (x as ProteinGroupRow).Cluster;
                return null;
            };

            accessionColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow)
                    return (x as ProteinGroupRow).Proteins;
                else if (x is ProteinRow)
                    return (x as ProteinRow).Protein.Accession;
                return null;
            };

            proteinCountColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow)
                    return (x as ProteinGroupRow).ProteinCount;
                return null;
            };

            if ((int)_columnSettings[coverageColumn][1] == -1)
                coverageColumn.AspectToStringFormat = "{0:0%}";
            else
                coverageColumn.AspectToStringFormat = "{0:" + "p" + (int)_columnSettings[coverageColumn][1] + "}";
            coverageColumn.AspectGetter += delegate(object x)
            {
                // the AspectToStringFormat formatter multiplies by 100
                if (x is ProteinGroupRow)
                    return (x as ProteinGroupRow).MeanProteinCoverage / 100.0;
                else if (x is ProteinRow)
                    return (x as ProteinRow).Protein.Coverage / 100.0;
                return null;
            };

            distinctPeptidesColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow)
                    return (x as ProteinGroupRow).DistinctPeptides;
                return null;
            };

            distinctMatchesColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow)
                    return (x as ProteinGroupRow).DistinctMatches;
                return null;
            };

            filteredSpectraColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow)
                    return (x as ProteinGroupRow).Spectra;
                return null;
            };

            descriptionColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow && (x as ProteinGroupRow).ProteinCount == 1)
                    return (x as ProteinGroupRow).FirstProteinDescription;
                else if (x is ProteinRow)
                    return (x as ProteinRow).Protein.Description;
                return null;
            };
        }

        void treeListView_HyperlinkClicked (object sender, HyperlinkClickedEventArgs e)
        {
            if (ReferenceEquals(e.Column, clusterColumn))
            {
                e.Handled = true; // do not treat as a URL

                var newDataFilter = new DataFilter()
                {
                    MaximumQValue = dataFilter.MaximumQValue,
                    FilterSource = this
                };

                newDataFilter.Cluster = (e.Item.RowObject as ProteinGroupRow).Cluster;
                if (ProteinViewFilter != null)
                    ProteinViewFilter(this, newDataFilter);
            }
            else if (ReferenceEquals(e.Column, coverageColumn))
            {
                e.Handled = true; // do not treat as a URL

                Protein pro;
                if (e.Item.RowObject is ProteinGroupRow)
                    pro = session.Get<Protein>((e.Item.RowObject as ProteinGroupRow).FirstProteinId);
                else
                    pro = (e.Item.RowObject as ProteinRow).Protein;

                if (ProteinViewVisualize != null)
                    ProteinViewVisualize(this, new ProteinViewVisualizeEventArgs() {Protein = pro});
            }
        }

        void treeListView_CellClick (object sender, CellClickEventArgs e)
        {
            if (e.ClickCount < 2 || e.Item == null || e.Item.RowObject == null)
                return;

            var newDataFilter = new DataFilter() { FilterSource = this };

            if (e.Item.RowObject is ProteinGroupRow)
                newDataFilter.Protein = session.Get<DataModel.Protein>((e.Item.RowObject as ProteinGroupRow).FirstProteinId);
            else if (e.Item.RowObject is ProteinRow)
                newDataFilter.Protein = (e.Item.RowObject as ProteinRow).Protein;

            if (ProteinViewFilter != null)
                ProteinViewFilter(this, newDataFilter);
        }

        public event ProteinViewFilterEventHandler ProteinViewFilter;
        public event EventHandler<ProteinViewVisualizeEventArgs> ProteinViewVisualize;

        private NHibernate.ISession session;

        private DataFilter viewFilter; // what the user has filtered on
        private DataFilter dataFilter; // how this view is filtered (i.e. never on its own rows)
        private DataFilter basicDataFilter; // the basic filter without the user filtering on rows

        private IList<ProteinGroupRow> rowsByProteinGroup, basicRowsByProteinGroup;

        private List<OLVColumn> pivotColumns = new List<OLVColumn>();
        private Dictionary<long, SpectrumSource> sourceById;
        private Dictionary<long, SpectrumSourceGroup> groupById;
        private Map<long, Map<long, ProteinStats>> statsPerProteinGroupBySpectrumSource;
        private Map<long, Map<long, ProteinStats>> statsPerProteinGroupBySpectrumSourceGroup;
        private IList<Pivot<PivotBy>> checkedPivots;

        // TODO: support multiple selected objects
        string[] oldSelectionPath = new string[] { };

        public void SetData (NHibernate.ISession session, DataFilter viewFilter)
        {
            this.session = session;
            this.viewFilter = viewFilter;
            this.dataFilter = new DataFilter(viewFilter) { Protein = null, Cluster = null };

            // stored to avoid cross-thread calls on the control
            checkedPivots = pivotSetupControl.CheckedPivots;

            if (treeListView.SelectedObject is ProteinGroupRow)
            {
                oldSelectionPath = new string[] { treeListView.SelectedItem.Text };
            }
            else if (treeListView.SelectedObject is ProteinRow)
            {
                var proteinGroup = (treeListView.GetParent(treeListView.SelectedObject) as ProteinGroupRow).Proteins;
                oldSelectionPath = new string[] { proteinGroup, treeListView.SelectedItem.Text };
            }

            ClearData();

            Text = TabText = "Loading protein view...";

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += new DoWorkEventHandler(setData);
            workerThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(renderData);
            workerThread.RunWorkerAsync();
        }

        internal void LoadLayout(IList<ColumnProperty> listOfSettings)
        {
            if (listOfSettings.Count > 0)
            {
                _columnSettings = new Dictionary<OLVColumn, object[]>();

                SetPropertyFromDatabase(ref _columnSettings, accessionColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, clusterColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, proteinCountColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, coverageColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, distinctPeptidesColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, distinctMatchesColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, filteredSpectraColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, descriptionColumn, listOfSettings);

                SetColumnAspectGetters();
                var backColor = listOfSettings.Where(x => x.Name == "BackColor").SingleOrDefault();
                var textColor = listOfSettings.Where(x => x.Name == "TextColor").SingleOrDefault();

                treeListView.BackColor = Color.FromArgb(backColor.ColorCode);
                treeListView.ForeColor = Color.FromArgb(textColor.ColorCode);
                treeListView.HyperlinkStyle.Normal.ForeColor = Color.FromArgb(textColor.ColorCode);
                treeListView.HyperlinkStyle.Visited.ForeColor = Color.FromArgb(textColor.ColorCode);

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = (bool)kvp.Value[3];
                treeListView.RebuildColumns();
            }
        }

        public void ClearData ()
        {
            Text = TabText = "Protein View";

            treeListView.DiscardAllState();
            treeListView.Roots = null;
            treeListView.Refresh();
            Refresh();
        }

        public void ClearData (bool clearBasicFilter)
        {
            if (clearBasicFilter)
                basicDataFilter = null;
            ClearData();
        }

        void setData(object sender, DoWorkEventArgs e)
        {
            lock (session)
            try
            {
                var proteinGroupQuery = session.CreateQuery(
                    "SELECT DISTINCT_GROUP_CONCAT(pro.Accession), " +
                    "       COUNT(DISTINCT psm.Peptide.id), " +
                    "       COUNT(DISTINCT psm.id), " +
                    "       COUNT(DISTINCT psm.Spectrum.id), " +
                    "       pro.ProteinGroup, " +
                    "       MIN(pro.Id), " +
                    "       MIN(pro.Description), " +
                    "       COUNT(DISTINCT pro.Id), " +
                    "       pro.Cluster, " +
                    "       AVG(pro.Coverage) " +
                    dataFilter.GetFilteredQueryString(DataFilter.FromProtein,
                                                      DataFilter.ProteinToPeptideSpectrumMatch) +
                    "GROUP BY pro.ProteinGroup " +
                    "ORDER BY COUNT(DISTINCT psm.Peptide.id) DESC");//, COUNT(DISTINCT psm.id) DESC, COUNT(DISTINCT psm.Spectrum.id) DESC");

                proteinGroupQuery.SetReadOnly(true);

                var statsPerProteinGroupBySpectrumSourceQuery = session.CreateQuery(
                    "SELECT DISTINCT_GROUP_CONCAT(pro.Accession), " +
                    "       s.Source.id, " +
                    "       COUNT(DISTINCT psm.Peptide), " +
                    "       COUNT(DISTINCT psm.Spectrum), " +
                    "       MIN(pro.id) " +
                    dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                      DataFilter.PeptideSpectrumMatchToSpectrumSource,
                                                      DataFilter.PeptideSpectrumMatchToProtein) +
                    "GROUP BY pro.ProteinGroup, s.Source.id");

                var statsPerProteinGroupBySpectrumSourceGroupQuery = session.CreateQuery(
                    "SELECT DISTINCT_GROUP_CONCAT(pro.Accession), " +
                    "       ssgl.Group.id, " +
                    "       COUNT(DISTINCT psm.Peptide.id), " +
                    "       COUNT(DISTINCT psm.Spectrum.id), " +
                    "       MIN(pro.id) " +
                    dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                      DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink,
                                                      DataFilter.PeptideSpectrumMatchToProtein) +
                    "GROUP BY pro.ProteinGroup, ssgl.Group");

                sourceById = session.Query<SpectrumSource>().Where(o => o.Group != null).ToDictionary(o => o.Id.Value);
                groupById = session.Query<SpectrumSourceGroup>().ToDictionary(o => o.Id.Value);

                var stats = statsPerProteinGroupBySpectrumSource = new Map<long, Map<long,ProteinStats>>();
                if (checkedPivots.Count(o => o.Mode == PivotBy.Source) > 0)
                    foreach (var queryRow in statsPerProteinGroupBySpectrumSourceQuery.List<object[]>())
                        stats[(long) queryRow[1]][(long) queryRow[4]] = new ProteinStats(queryRow);

                var stats2 = statsPerProteinGroupBySpectrumSourceGroup = new Map<long, Map<long, ProteinStats>>();
                if (checkedPivots.Count(o => o.Mode == PivotBy.Group) > 0)
                    foreach (var queryRow in statsPerProteinGroupBySpectrumSourceGroupQuery.List<object[]>())
                        stats2[(long) queryRow[1]][(long) queryRow[4]] = new ProteinStats(queryRow);

                if (dataFilter.IsBasicFilter || viewFilter.Protein != null)
                {
                    // refresh basic data when basicDataFilter is unset or when the basic filter values have changed
                    if (basicDataFilter == null || (dataFilter.IsBasicFilter && dataFilter != basicDataFilter))
                    {
                        basicDataFilter = new DataFilter(dataFilter);
                        basicRowsByProteinGroup = proteinGroupQuery.List<object[]>().Select(o => new ProteinGroupRow(o)).ToList();
                    }

                    rowsByProteinGroup = basicRowsByProteinGroup;
                }
                else
                {
                    rowsByProteinGroup = proteinGroupQuery.List<object[]>().Select(o => new ProteinGroupRow(o)).ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            long totalProteins = rowsByProteinGroup.Sum(o => o.ProteinCount);

            // show total counts in the form title
            Text = TabText = String.Format("Protein View: {0} protein groups, {1} proteins", rowsByProteinGroup.Count, totalProteins);

            treeListView.Roots = rowsByProteinGroup;

            treeListView.Freeze();
            foreach (var pivotColumn in pivotColumns)
                treeListView.Columns.Remove(pivotColumn);

            pivotColumns = new List<OLVColumn>();

            var sourceNames = sourceById.Select(o => o.Value.Name);
            var stats = statsPerProteinGroupBySpectrumSource;
            var stats2 = statsPerProteinGroupBySpectrumSourceGroup;

            int insertIndex = descriptionColumn.Index > 0 ? descriptionColumn.Index : treeListView.ColumnsInDisplayOrder.Count;

            foreach (long sourceId in stats.Keys)
            {
                string uniqueSubstring;
                Util.UniqueSubstring(sourceById[sourceId].Name, sourceNames, out uniqueSubstring);
                var column = new OLVColumn() { Text = uniqueSubstring, Tag = sourceId };
                column.AspectGetter += delegate(object x)
                {
                    if (x is ProteinGroupRow &&
                        stats[(long) column.Tag].Contains((x as ProteinGroupRow).FirstProteinId))
                        return stats[(long) column.Tag][(x as ProteinGroupRow).FirstProteinId].DistinctPeptides;
                    return null;
                };
                pivotColumns.Add(column);
            }

            foreach (long groupId in stats2.Keys)
            {
                var column = new OLVColumn() { Text = groupById[groupId].Name, Tag = groupId };
                column.AspectGetter += delegate(object x)
                {
                    if (x is ProteinGroupRow &&
                        stats2[(long) column.Tag].Contains((x as ProteinGroupRow).FirstProteinId))
                        return stats2[(long) column.Tag][(x as ProteinGroupRow).FirstProteinId].DistinctPeptides;
                    return null;
                };
                pivotColumns.Add(column);
            }

            foreach (var column in pivotColumns.OrderBy(o => o.Text))
                treeListView.Columns.Insert(insertIndex++, column);
            treeListView.Unfreeze();

            // try to (re)set selected item
            OLVListItem selectedItem = null;
            foreach (string branch in oldSelectionPath)
            {
                int index = 0;
                if (selectedItem != null)
                {
                    treeListView.Expand(selectedItem.RowObject);
                    index = selectedItem.Index;
                }

                index = treeListView.FindMatchingRow(branch, index, SearchDirectionHint.Down);
                if (index < 0)
                    break;
                selectedItem = treeListView.Items[index] as OLVListItem;
            }

            if (selectedItem != null)
            {
                treeListView.SelectedItem = selectedItem;
                selectedItem.EnsureVisible();
            }

            treeListView.Refresh();
        }

        private void clipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var table = getFormTable();

            TableExporter.CopyToClipboard(table);
        }

        private void fileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var table = getFormTable();

            TableExporter.ExportToFile(table);
        }

        private void exportButton_Click(object sender, EventArgs e)
        {
            if (treeListView.SelectedIndices.Count > 1)
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

        private List<List<string>> getFormTable()
        {
            var table = new List<List<string>>();
            var row = new List<string>();
            int numColumns;

            //get column names
            foreach (var column in treeListView.ColumnsInDisplayOrder)
                row.Add(column.Text);

            table.Add(row);
            numColumns = row.Count;
            row = new List<string>();

            //Retrieve all items
            if (treeListView.SelectedIndices.Count > 1)
            {
                foreach (int tableRow in treeListView.SelectedIndices)
                {
                    string indention = string.Empty;
                    for (int tabs = 0; tabs < treeListView.Items[tableRow].IndentCount; tabs++)
                        indention += "     ";

                    row.Add(indention + treeListView.Items[tableRow].SubItems[0].Text);

                    for (int x = 1; x < numColumns; x++)
                    {
                        row.Add(treeListView.Items[tableRow].SubItems[x].Text);
                    }
                    table.Add(row);
                    row = new List<string>();
                }
            }
            else
            {
                for (int tableRow = 0; tableRow < treeListView.Items.Count; tableRow++)
                {
                    string indention = string.Empty;
                    for (int tabs = 0; tabs < treeListView.Items[tableRow].IndentCount; tabs++)
                        indention += "     ";

                    row.Add(indention + treeListView.Items[tableRow].SubItems[0].Text);

                    for (int x = 1; x < numColumns; x++)
                    {
                        row.Add(treeListView.Items[tableRow].SubItems[x].Text);
                    }
                    table.Add(row);
                    row = new List<string>();
                }
            }

            return table;
        }

        private void showInExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var table = getFormTable();

            TableExporter.ShowInExcel(table);
        }

        private void displayOptionsButton_Click(object sender, EventArgs e)
        {
            Color[] currentColors = { treeListView.BackColor, treeListView.ForeColor };

            foreach (var kvp in _columnSettings)
                kvp.Value[3] = kvp.Key.IsVisible;

            var ccf = new ColumnControlForm(_columnSettings, currentColors);

            if (ccf.ShowDialog() == DialogResult.OK)
            {
                _columnSettings = ccf._savedSettings;

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = (bool)kvp.Value[3];

                treeListView.BackColor = ccf.WindowBackColorBox.BackColor;
                treeListView.ForeColor = ccf.WindowTextColorBox.BackColor;
                treeListView.HyperlinkStyle.Normal.ForeColor = ccf.WindowTextColorBox.BackColor;
                treeListView.HyperlinkStyle.Visited.ForeColor = ccf.WindowTextColorBox.BackColor;

                SetColumnAspectGetters();
                treeListView.RebuildColumns();
            }
        }

        private void SetPropertyFromUserSettings(ref Dictionary<OLVColumn, object[]> testDictionary, OLVColumn targetColumn, List<string> columnProperties)
        {
            for (int x = 0; x < columnProperties.Count - 1; x++)
            {
                var splitSetting = columnProperties[x].Split('|');

                if (splitSetting[0] == targetColumn.Text)
                {
                    testDictionary.Add(targetColumn,
                        new object[4]{splitSetting[1], int.Parse(splitSetting[2]),
                            Color.FromArgb(int.Parse(splitSetting[3])),
                            bool.Parse(splitSetting[4])});
                    break;
                }
            }
        }

        private static void SetPropertyFromDatabase(ref Dictionary<OLVColumn, object[]> testDictionary, OLVColumn targetColumn, IList<ColumnProperty> formSettings)
        {
            ColumnProperty rowSettings = formSettings.Where(x => x.Name == targetColumn.Text).SingleOrDefault();

            testDictionary.Add(targetColumn,
                new object[4]{rowSettings.Type, rowSettings.DecimalPlaces,
                    Color.FromArgb(rowSettings.ColorCode),
                    rowSettings.Visible});

        }

        internal List<ColumnProperty> GetCurrentProperties()
        {
            foreach (var kvp in _columnSettings)
                kvp.Value[3] = false;
            foreach (var column in treeListView.ColumnsInDisplayOrder)
                _columnSettings[column][3] = true;
            var currentList = new List<ColumnProperty>();

            foreach (var kvp in _columnSettings)
            {
                currentList.Add(new ColumnProperty
                {
                    Scope = "ProteinTableForm",
                    Name = kvp.Key.Text,
                    Type = kvp.Value[0].ToString(),
                    DecimalPlaces = (int)kvp.Value[1],
                    ColorCode = ((Color)kvp.Value[2]).ToArgb(),
                    Visible = (bool)kvp.Value[3],
                    Locked = null
                });
            }

            currentList.Add(new ColumnProperty
            {
                Scope = "ProteinTableForm",
                Name = "BackColor",
                Type = "GlobalSetting",
                DecimalPlaces = -1,
                ColorCode = treeListView.BackColor.ToArgb(),
                Visible = false,
                Locked = null
            });
            currentList.Add(new ColumnProperty
            {
                Scope = "ProteinTableForm",
                Name = "TextColor",
                Type = "GlobalSetting",
                DecimalPlaces = -1,
                ColorCode = treeListView.ForeColor.ToArgb(),
                Visible = false,
                Locked = null
            });

            return currentList;
        }

        private void pivotSetupButton_Click (object sender, EventArgs e)
        {
            pivotSetupPopup.Show(sender as Button);
        }

        private void pivotSetupControl_PivotChanged (object sender, EventArgs e)
        {
            dirtyPivots = true;
        }

        void pivotSetupPopup_Closed (object sender, ToolStripDropDownClosedEventArgs e)
        {
            if (dirtyPivots)
            {
                dirtyPivots = false;

                if (dataFilter != null && dataFilter.IsBasicFilter)
                    basicDataFilter = null; // force refresh of basic rows

                SetData(session, viewFilter);
            }
        }
    }

    public delegate void ProteinViewFilterEventHandler (ProteinTableForm sender, DataFilter proteinViewFilter);

    public class ProteinViewVisualizeEventArgs : EventArgs
    {
        public DataModel.Protein Protein { get; internal set; }
    }
}
