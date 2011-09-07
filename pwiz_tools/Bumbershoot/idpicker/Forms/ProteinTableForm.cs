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
            public int Length { get; private set; }
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
                Length = (int) queryRow[6];
                FirstProteinDescription = (string) queryRow[7];
                ProteinCount = (long) queryRow[8];
                Cluster = (int?) queryRow[9];
                MeanProteinCoverage = (double) (queryRow[10] ?? 0.0);
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

        Dictionary<OLVColumn, ColumnProperty> _columnSettings;

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
            SetDefaults();
            var allLayouts = new List<string>(Util.StringCollectionToStringArray(Properties.Settings.Default.ProteinTableFormSettings));
            if (allLayouts.Count > 1)
            {
                //get User Defualt Layout
                var retrievedList = allLayouts[1].Split(System.Environment.NewLine.ToCharArray()).ToList();

                //Make sure layout has same number of columns as ObjectListView
                if (retrievedList.Count == _columnSettings.Count + 3)
                {
                    //Go through each column and assign properties
                    foreach (OLVColumn column in treeListView.Columns)
                    {
                        //Go through lines and find the one that matches current column
                        for (var x = 0; x < retrievedList.Count - 1; x++)
                        {
                            var splitSetting = retrievedList[x].Split('|');

                            if (splitSetting[0] == column.Text)
                            {
                                _columnSettings[column] =
                                    new ColumnProperty
                                    {
                                        Scope = "ProteinTableForm",
                                        Name = column.Text,
                                        Type = splitSetting[1],
                                        DecimalPlaces = int.Parse(splitSetting[2]),
                                        ColorCode = int.Parse(splitSetting[3]),
                                        Visible = bool.Parse(splitSetting[4]),
                                        Locked = null
                                    };
                                break;
                            }
                        }
                    }

                    treeListView.BackColor = Color.FromArgb(int.Parse(retrievedList[retrievedList.Count - 2]));
                    treeListView.ForeColor = Color.FromArgb(int.Parse(retrievedList[retrievedList.Count - 1]));
                }
            }

            SetColumnAspectGetters();
            #endregion

            treeListView.UseCellFormatEvents = true;
            treeListView.FormatCell += delegate(object sender, FormatCellEventArgs currentCell)
            {
                if (currentCell.Item.RowObject is ProteinGroupRow &&
                    (viewFilter.Protein != null && viewFilter.Protein.Count(x => x.Id == (currentCell.Item.RowObject as ProteinGroupRow).FirstProteinId) == 0||
                     viewFilter.Cluster != null && viewFilter.Cluster.Contains((currentCell.Item.RowObject as ProteinGroupRow).Cluster.Value)))
                    currentCell.SubItem.ForeColor = SystemColors.GrayText;
                else if (currentCell.Item.RowObject is ProteinRow &&
                         (viewFilter.Protein != null && viewFilter.Protein.Count(x => x.Id == (currentCell.Item.RowObject as ProteinRow).Protein.Id) == 0||
                          viewFilter.Cluster != null && viewFilter.Cluster.Contains((currentCell.Item.RowObject as ProteinRow).Protein.Cluster)))
                    currentCell.SubItem.ForeColor = SystemColors.GrayText;

                if (_columnSettings.ContainsKey(currentCell.Column))
                    currentCell.SubItem.BackColor = Color.FromArgb(_columnSettings[currentCell.Column].ColorCode);
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
            treeListView.KeyPress += new KeyPressEventHandler(treeListView_KeyPress);

            treeListView.HyperlinkClicked += new EventHandler<HyperlinkClickedEventArgs>(treeListView_HyperlinkClicked);
            treeListView.HyperlinkStyle.Normal.ForeColor = treeListView.ForeColor;
            treeListView.HyperlinkStyle.Visited.ForeColor = treeListView.ForeColor;
        }

        private void SetDefaults()
        {
            _columnSettings = new Dictionary<OLVColumn, ColumnProperty>();
            var columnType = new Dictionary<OLVColumn, string>
                                 {
                                     {accessionColumn, "Key"},
                                     {clusterColumn, "Integer"},
                                     {proteinCountColumn, "Integer"},
                                     {coverageColumn, "Float"},
                                     {distinctPeptidesColumn, "Integer"},
                                     {distinctMatchesColumn, "Integer"},
                                     {filteredSpectraColumn, "Integer"},
                                     {proteinLengthColumn, "Integer"},
                                     {descriptionColumn, "String"}
                                 };

            foreach (var kvp in columnType)
            {
                var tempColumnProperty = new ColumnProperty
                {
                    Scope = "ProteinTableForm",
                    Type = columnType[kvp.Key],
                    DecimalPlaces = -1,
                    Visible = true,
                    Locked = null,
                    ColorCode = treeListView.BackColor.ToArgb(),
                    Name = kvp.Key.Text
                };

                _columnSettings.Add(kvp.Key, tempColumnProperty);
            }
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

            if (_columnSettings[coverageColumn].DecimalPlaces == -1)
                coverageColumn.AspectToStringFormat = "{0:0%}";
            else
                coverageColumn.AspectToStringFormat = "{0:" + "p" + _columnSettings[coverageColumn].DecimalPlaces + "}";
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

            proteinLengthColumn.AspectGetter += delegate(object x)
            {
                Protein pro;
                if (x is ProteinGroupRow && (x as ProteinGroupRow).ProteinCount == 1)
                    return (x as ProteinGroupRow).Length;
                else if (x is ProteinRow)
                    return (x as ProteinRow).Protein.Length;

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

                if (newDataFilter.Cluster == null)
                    newDataFilter.Cluster = new List<long>();
                newDataFilter.Cluster.Add((e.Item.RowObject as ProteinGroupRow).Cluster.Value);
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

            var newDataFilter = new DataFilter()
                                    {
                                        FilterSource = this,
                                        Protein = new List<Protein>()
                                    };

            if (e.Item.RowObject is ProteinGroupRow)
                newDataFilter.Protein.Add(session.Get<DataModel.Protein>((e.Item.RowObject as ProteinGroupRow).FirstProteinId));
            else if (e.Item.RowObject is ProteinRow)
                newDataFilter.Protein.Add((e.Item.RowObject as ProteinRow).Protein);

            if (ProteinViewFilter != null)
                ProteinViewFilter(this, newDataFilter);
        }

        void treeListView_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != (char)Keys.Enter)
                return;

            e.Handled = true;
            var newDataFilter = new DataFilter {FilterSource = this};

            if (treeListView.SelectedObjects.Count > 0)
                newDataFilter.Protein = new List<Protein>();

            foreach (var item in treeListView.SelectedObjects)
            {
                if (item == null)
                    continue;

                if (item is ProteinGroupRow)
                    newDataFilter.Protein.Add(session.Get<DataModel.Protein>((item as ProteinGroupRow).FirstProteinId));
                else if (item is ProteinRow)
                    newDataFilter.Protein.Add((item as ProteinRow).Protein);
            }

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

        List<ColumnProperty> _unusedPivotSettings = new List<ColumnProperty>();

        internal void LoadLayout(IList<ColumnProperty> listOfSettings)
        {
            if (listOfSettings.Count > 0)
            {
                _unusedPivotSettings = listOfSettings.Where(x => x.Type == "PivotColumn").ToList();

                var columnlist = _columnSettings.Select(kvp => kvp.Key).ToList();
                var untouchedColumns = _columnSettings.Select(kvp => kvp.Key).ToList();

                var backColor = listOfSettings.Where(x => x.Name == "BackColor").SingleOrDefault();
                var textColor = listOfSettings.Where(x => x.Name == "TextColor").SingleOrDefault();

                foreach (var column in columnlist)
                {
                    var rowSettings = listOfSettings.Where(x => x.Name == column.Text).SingleOrDefault();

                    //if rowSettings is null it is likely an unsaved pivotColumn, keep defualt
                    if (rowSettings == null)
                        continue;

                    if (_unusedPivotSettings.Contains(rowSettings))
                        _unusedPivotSettings.Remove(rowSettings);

                    _columnSettings[column] = new ColumnProperty
                    {
                        Scope = "ProteinTableForm",
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

                SetColumnAspectGetters();

                treeListView.BackColor = Color.FromArgb(backColor.ColorCode);
                treeListView.ForeColor = Color.FromArgb(textColor.ColorCode);
                treeListView.HyperlinkStyle.Normal.ForeColor = Color.FromArgb(textColor.ColorCode);
                treeListView.HyperlinkStyle.Visited.ForeColor = Color.FromArgb(textColor.ColorCode);

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = kvp.Value.Visible;
                treeListView.RebuildColumns();

                if (session != null && session.IsOpen)
                    SetData(session, dataFilter);
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
                    "       MIN(pro.Length), " +
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

                var newProperties = new ColumnProperty()
                {
                    Scope = "ProteinTableForm",
                    Type = "PivotColumn",
                    Name = column.Text,
                    DecimalPlaces = -1,
                    ColorCode = treeListView.BackColor.ToArgb(),
                    Visible = true,
                    Locked = null
                };

                var previousForm =
                    _columnSettings.Where(x => x.Value.Name == column.Text && x.Value.Type == "PivotColumn").ToList();

                if (previousForm.Count == 1)
                {
                    _columnSettings.Remove(previousForm[0].Key);
                    newProperties.ColorCode = previousForm[0].Value.ColorCode;
                    newProperties.Visible = previousForm[0].Value.Visible;
                    newProperties.Locked = previousForm[0].Value.Locked;
                }
                else
                {
                    var possibleSaved =
                        _unusedPivotSettings.Where(x => x.Name == column.Text).SingleOrDefault();
                    if (possibleSaved != null)
                    {
                        newProperties.ColorCode = possibleSaved.ColorCode;
                        newProperties.Visible = possibleSaved.Visible;
                        newProperties.Locked = possibleSaved.Locked;
                    }
                }

                column.IsVisible = newProperties.Visible;
                _columnSettings.Add(column, newProperties);
                if (newProperties.Visible)
                {
                    pivotColumns.Add(column);
                }
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

                var newProperties = new ColumnProperty()
                {
                    Scope = "ProteinTableForm",
                    Type = "PivotColumn",
                    Name = column.Text,
                    DecimalPlaces = -1,
                    ColorCode = treeListView.BackColor.ToArgb(),
                    Visible = true,
                    Locked = null
                };

                var previousForm =
                    _columnSettings.Where(x => x.Value.Name == column.Text && x.Value.Type == "PivotColumn").ToList();

                if (previousForm.Count == 1)
                {
                    _columnSettings.Remove(previousForm[0].Key);
                    newProperties.ColorCode = previousForm[0].Value.ColorCode;
                    newProperties.Visible = previousForm[0].Value.Visible;
                    newProperties.Locked = previousForm[0].Value.Locked;
                }
                else
                {
                    var possibleSaved =
                        _unusedPivotSettings.Where(x => x.Name == column.Text).SingleOrDefault();
                    if (possibleSaved != null)
                    {
                        newProperties.ColorCode = possibleSaved.ColorCode;
                        newProperties.Visible = possibleSaved.Visible;
                        newProperties.Locked = possibleSaved.Locked;
                    }
                }


                column.IsVisible = newProperties.Visible;
                _columnSettings.Add(column, newProperties);
                if (newProperties.Visible)
                {
                    pivotColumns.Add(column);
                }
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

        internal List<List<string>> getFormTable()
        {
            return getFormTable(false, string.Empty);
        }

        internal List<List<string>> getFormTable(bool htmlFormat, string reportName)
        {
            //get groups and remove filtered entries
            var list = rowsByProteinGroup;
            for (var c = list.Count-1; c >= 0; c--)
                if (viewFilter.Protein != null && viewFilter.Protein.Count(x => x.Id == list[c].FirstProteinId) == 0 ||
                    viewFilter.Cluster != null && viewFilter.Cluster.Contains(list[c].Cluster.Value))
                    list.RemoveAt(c);

            var fullGroups = from ProteinGroupRow row in list where row.ProteinCount > 1 select row;
            var allGroupedProteinNames = string.Join(",", (from ProteinGroupRow p in fullGroups select p.Proteins).ToArray());
            var allGroupedProteins = session.CreateQuery(String.Format(
                "SELECT pro FROM Protein pro WHERE pro.Accession IN ('{0}')",
                allGroupedProteinNames.Replace(",", "','")))
                .List<Protein>();
            var stats = statsPerProteinGroupBySpectrumSource;
            var stats2 = statsPerProteinGroupBySpectrumSourceGroup;
            var sourceColumns = new HashSet<OLVColumn>();
            var groupColumns = new HashSet<OLVColumn>();
            foreach (var sourceId in stats.Keys)
            {
                var id = sourceId;
                var column = (from OLVColumn p in pivotColumns where (long)p.Tag == id select p).First();
                if (column == null) continue;
                sourceColumns.Add(column);
            }
            foreach (var sourceId in stats2.Keys)
            {
                var id = sourceId;
                var column = (from OLVColumn p in pivotColumns where (long)p.Tag == id select p).Last();
                if (column == null) continue;
                groupColumns.Add(column);
            }
            var exportTable = new List<List<string>>();
            var displayedColumns = new List<OLVColumn>();
            var columnRow = new List<string>();
            foreach (var column in treeListView.ColumnsInDisplayOrder)
            {
                columnRow.Add(column.Text);
                displayedColumns.Add(column);
            }
            exportTable.Add(columnRow);

            foreach (var row in list)
            {
                var rowText = new List<string>();
                foreach (var column in displayedColumns)
                {
                    if (column == clusterColumn)
                        rowText.Add(row.Cluster == null ? string.Empty : row.Cluster.ToString());
                    else if (column == accessionColumn)
                        rowText.Add(htmlFormat
                                        ? string.Format("<a href=\"{0}-cluster{1}.html\">{2}</a>", reportName,
                                                        row.Cluster, row.Proteins)
                                        : row.Proteins);
                    else if (column == proteinCountColumn)
                        rowText.Add(row.ProteinCount.ToString());
                    else if (column == coverageColumn)
                        rowText.Add((row.MeanProteinCoverage / 100.0).ToString());
                    else if (column == distinctPeptidesColumn)
                        rowText.Add(row.DistinctPeptides.ToString());
                    else if (column == distinctMatchesColumn)
                        rowText.Add(row.DistinctMatches.ToString());
                    else if (column == filteredSpectraColumn)
                        rowText.Add(row.Spectra.ToString());
                    else if (column == proteinLengthColumn)
                        rowText.Add(row.Length.ToString());
                    else if (column == descriptionColumn && row.ProteinCount == 1)
                        rowText.Add(row.FirstProteinDescription);
                    else if (sourceColumns.Contains(column))
                        rowText.Add(stats[(long)column.Tag].Contains(row.FirstProteinId)
                                        ? stats[(long)column.Tag][row.FirstProteinId].DistinctPeptides.ToString()
                                        : string.Empty);
                    else if (groupColumns.Contains(column))
                        rowText.Add(stats2[(long)column.Tag].Contains(row.FirstProteinId)
                                        ? stats2[(long)column.Tag][row.FirstProteinId].DistinctPeptides.ToString()
                                        : string.Empty);
                    else
                        rowText.Add(string.Empty);
                }

                exportTable.Add(rowText);

                //individual protein checking
                if (fullGroups.Contains(row))
                {
                    var currentRow = row;
                    var groupProteins = from Protein p in allGroupedProteins
                                        where currentRow.Proteins.Contains(p.Accession)
                                        select p;
                    foreach (var protein in groupProteins)
                    {
                        rowText = new List<string>();
                        foreach (var column in displayedColumns)
                        {
                            if (column == accessionColumn)
                                rowText.Add("     " +protein.Accession);
                            else if (column == coverageColumn)
                                rowText.Add((protein.Coverage/100.0).ToString());
                            else if (column == proteinLengthColumn)
                                rowText.Add(protein.Length.ToString());
                            else if (column == descriptionColumn)
                                rowText.Add(protein.Description);
                            else rowText.Add(string.Empty);
                        }
                        exportTable.Add(rowText);
                    }
                }
            }
            return exportTable;
        }

        private void showInExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var table = getFormTable();

            var exportWrapper = new Dictionary<string, List<List<string>>> { { this.Name, table } };

            TableExporter.ShowInExcel(exportWrapper, false);
        }

        private void displayOptionsButton_Click(object sender, EventArgs e)
        {
            Color[] currentColors = { treeListView.BackColor, treeListView.ForeColor };

            foreach (var kvp in _columnSettings)
                kvp.Value.Visible = kvp.Key.IsVisible;

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
            }
        }



        internal List<ColumnProperty> GetCurrentProperties(bool pivotToo)
        {
            foreach (var kvp in _columnSettings)
                kvp.Value.Visible = false;
            foreach (var column in treeListView.ColumnsInDisplayOrder)
                _columnSettings[column].Visible = true;
            var currentList = new List<ColumnProperty>();

            foreach (var kvp in _columnSettings)
            {
                currentList.Add(new ColumnProperty
                {
                    Scope = "ProteinTableForm",
                    Name = kvp.Key.Text,
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
