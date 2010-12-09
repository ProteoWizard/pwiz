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
    public partial class PeptideTableForm : DockableForm
    {
        public TreeListView TreeListView { get { return treeListView; } }

        public enum PivotBy { Group, Source, Off }
        public PivotSetupControl<PivotBy> PivotSetupControl { get { return pivotSetupControl; } }
        private PivotSetupControl<PivotBy> pivotSetupControl;
        private Popup pivotSetupPopup;
        private bool dirtyPivots = false;

        #region Wrapper classes for encapsulating query results

        public class PeptideRow
        {
            public DataModel.Peptide Peptide { get; private set; }
            public long DistinctMatchesWithRoundedMass { get; private set; }
            public long Spectra { get; private set; }

            #region Constructor
            public PeptideRow (object[] queryRow)
            {
                Peptide = (DataModel.Peptide) queryRow[0];
                DistinctMatchesWithRoundedMass = (long) queryRow[1];
                Spectra = (long) queryRow[2];
            }
            #endregion
        }

        public class PeptideStats
        {
            public long DistinctMatches { get; private set; }
            public long Spectra { get; private set; }

            #region Constructor
            public PeptideStats () { }
            public PeptideStats (object[] queryRow)
            {
                DistinctMatches = (long) queryRow[2];
                Spectra = (long) queryRow[3];
            }
            #endregion
        }

        public class PeptideSpectrumMatchRow
        {
            public DataModel.PeptideSpectrumMatch PeptideSpectrumMatch { get; private set; }
            public long Spectra { get; private set; }
            public DistinctPeptideFormat DistinctPeptide { get; private set; }

            #region Constructor
            public PeptideSpectrumMatchRow (object[] queryRow)
            {
                PeptideSpectrumMatch = (DataModel.PeptideSpectrumMatch) queryRow[0];
                DistinctPeptide = new DistinctPeptideFormat("(psm.Peptide || ' ' || ROUND(psm.MonoisotopicMass))",
                                                            PeptideSpectrumMatch.ToModifiedString(),
                                                            (string) queryRow[1]);
                Spectra = (long) queryRow[2];
            }
            #endregion
        }

        public class PeptideInstanceRow
        {
            public DataModel.PeptideInstance PeptideInstance { get; private set; }

            #region Constructor
            public PeptideInstanceRow (object queryRow)
            {
                PeptideInstance = (DataModel.PeptideInstance) queryRow;
            }
            #endregion
        }

        #endregion

        Dictionary<OLVColumn, ColumnProperty> _columnSettings;

        public PeptideTableForm ()
        {
            InitializeComponent();

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                e.Cancel = true;
                DockState = DockState.DockBottomAutoHide;
            };

            Text = TabText = "Peptide View";

            var pivots = new List<Pivot<PivotBy>>();
            pivots.Add(new Pivot<PivotBy>() { Mode = PivotBy.Group, Text = "Group" });
            pivots.Add(new Pivot<PivotBy>() { Mode = PivotBy.Source, Text = "Source" });

            pivotSetupControl = new PivotSetupControl<PivotBy>(pivots);
            pivotSetupControl.PivotChanged += pivotSetupControl_PivotChanged;
            pivotSetupPopup = new Popup(pivotSetupControl) { FocusOnOpen = true };
            pivotSetupPopup.Closed += new ToolStripDropDownClosedEventHandler(pivotSetupPopup_Closed);

            #region Column aspect getters
            SetDefaults();
            var allLayouts = new List<string>(Util.StringCollectionToStringArray(Properties.Settings.Default.PeptideTableFormSettings));

            if (allLayouts.Count > 1)
            {
                //get User Defualt Layout
                var retrievedList = allLayouts[1].Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();

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
                                        Scope = "PeptideTableForm",
                                        Name = column.Text,
                                        Type = splitSetting[1],
                                        DecimalPlaces = int.Parse(splitSetting[2]),
                                        ColorCode = int.Parse(splitSetting[3]),
                                        Visible = bool.Parse(splitSetting[4]),
                                        Locked = bool.Parse(splitSetting[5])
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
                if (currentCell.Item.RowObject is PeptideRow &&
                    (viewFilter.Peptide != null && viewFilter.Peptide.Count(x => x.Id == (currentCell.Item.RowObject as PeptideRow).Peptide.Id) == 0 ||
                     viewFilter.DistinctPeptideKey != null))
                    currentCell.SubItem.ForeColor = SystemColors.GrayText;
                else if (currentCell.Item.RowObject is PeptideSpectrumMatchRow &&
                         viewFilter.DistinctPeptideKey != null && viewFilter.DistinctPeptideKey.Count(x=> x.Sequence == (currentCell.Item.RowObject as PeptideSpectrumMatchRow).DistinctPeptide.Sequence) == 0)
                    currentCell.SubItem.ForeColor = SystemColors.GrayText;

                if (_columnSettings.ContainsKey(currentCell.Column))
                    currentCell.SubItem.BackColor = Color.FromArgb(_columnSettings[currentCell.Column].ColorCode);
                else
                    currentCell.SubItem.BackColor = treeListView.BackColor;
            };

            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);

            radioButton1.CheckedChanged += new EventHandler(radioButton1_CheckedChanged);
        }

        private void SetDefaults()
        {
            _columnSettings = new Dictionary<OLVColumn, ColumnProperty>();
            var columnType = new Dictionary<OLVColumn, string>
                                 {
                                     {sequenceColumn, "Key"},
                                     {distinctMatchesColumn, "Integer"},
                                     {filteredSpectraColumn, "Integer"},
                                     {monoisotopicMassColumn, "Float"},
                                     {molecularWeightColumn, "Float"},
                                     {offsetColumn, "Integer"},
                                     {terminalSpecificityColumn, "String"},
                                     {missedCleavagesColumn, "Integer"},
                                     {proteinsColumn, "String"}
                                 };

            foreach (var kvp in columnType)
            {
                var tempColumnProperty = new ColumnProperty
                                             {
                    Scope = "PeptideTableForm",
                    Type = columnType[kvp.Key],
                    DecimalPlaces = -1,
                    Visible = true,
                    Locked = false,
                    ColorCode = treeListView.BackColor.ToArgb(),
                    Name = kvp.Key.Text
                };

                _columnSettings.Add(kvp.Key, tempColumnProperty);
            }
        }

        private void SetColumnAspectGetters()
        {
            sequenceColumn.AspectGetter = null;
            distinctMatchesColumn.AspectGetter = null;
            filteredSpectraColumn.AspectGetter = null;
            monoisotopicMassColumn.AspectGetter = null;
            molecularWeightColumn.AspectGetter = null;
            offsetColumn.AspectGetter = null;
            terminalSpecificityColumn.AspectGetter = null;
            missedCleavagesColumn.AspectGetter = null;

            sequenceColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideRow)
                    return (x as PeptideRow).Peptide.Sequence;
                else if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).DistinctPeptide.Sequence;
                else if (x is PeptideInstanceRow)
                    return (x as PeptideInstanceRow).PeptideInstance.Protein.Accession;
                return null;
            };

            distinctMatchesColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideRow)
                    return (x as PeptideRow).DistinctMatchesWithRoundedMass;
                return null;
            };

            filteredSpectraColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideRow)
                    return (x as PeptideRow).Spectra;
                else if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).Spectra;
                return null;
            };

            if (_columnSettings[monoisotopicMassColumn].DecimalPlaces == -1)
            {
                monoisotopicMassColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideRow)
                        return (x as PeptideRow).Peptide.MonoisotopicMass;
                    else if (x is PeptideSpectrumMatchRow)
                        return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMass;
                    return null;
                };
            }
            else
            {
                monoisotopicMassColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideRow)
                        return Math.Round((x as PeptideRow).Peptide.MonoisotopicMass,_columnSettings[monoisotopicMassColumn].DecimalPlaces);
                    else if (x is PeptideSpectrumMatchRow)
                        return Math.Round((x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMass, _columnSettings[monoisotopicMassColumn].DecimalPlaces);
                    return null;
                };
            }

            if (_columnSettings[molecularWeightColumn].DecimalPlaces == -1)
            {
                molecularWeightColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideRow)
                        return (x as PeptideRow).Peptide.MolecularWeight;
                    else if (x is PeptideSpectrumMatchRow)
                        return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MolecularWeight;
                    return null;
                };
            }
            else
            {
                molecularWeightColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideRow)
                        return Math.Round((x as PeptideRow).Peptide.MolecularWeight,_columnSettings[molecularWeightColumn].DecimalPlaces);
                    else if (x is PeptideSpectrumMatchRow)
                        return Math.Round((x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MolecularWeight,_columnSettings[molecularWeightColumn].DecimalPlaces);
                    return null;
                };
            }


            offsetColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideInstanceRow)
                    return (x as PeptideInstanceRow).PeptideInstance.Offset;
                return null;
            };

            terminalSpecificityColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideInstanceRow)
                {
                    var specificTermini = new List<string>();
                    if ((x as PeptideInstanceRow).PeptideInstance.NTerminusIsSpecific)
                        specificTermini.Add("N");
                    if ((x as PeptideInstanceRow).PeptideInstance.CTerminusIsSpecific)
                        specificTermini.Add("C");
                    if (specificTermini.Count == 0)
                        specificTermini.Add("None");
                    return String.Join(",", specificTermini.ToArray());
                }
                return null;
            };

            missedCleavagesColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideInstanceRow)
                    return (x as PeptideInstanceRow).PeptideInstance.MissedCleavages;
                return null;
            };

            proteinsColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideRow)
                    return String.Join(";", (x as PeptideRow).Peptide.Instances.Select(o => o.Protein.Accession).Distinct().ToArray());
                return null;
            };

            treeListView.CanExpandGetter += delegate(object x) { return x is PeptideRow; };
            treeListView.ChildrenGetter += delegate(object x)
            {
                var childFilter = new DataFilter(dataFilter) { Peptide = new List<Peptide>() };
                childFilter.Peptide.Add((x as PeptideRow).Peptide);

                object result;
                if (radioButton1.Checked)
                    result = session.CreateQuery("SELECT psm, (psm.Peptide || ' ' || ROUND(psm.MonoisotopicMass)), COUNT(DISTINCT psm.Spectrum) " +
                                                 childFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                 "GROUP BY (psm.Peptide || ' ' || ROUND(psm.MonoisotopicMass))")
                                    .List<object[]>().Select(o => new PeptideSpectrumMatchRow(o));
                else
                    return session.CreateQuery("SELECT DISTINCT psm.Peptide.Instances " +
                                               childFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch)/* +
                                               " GROUP BY psm.Peptide"*/
                                                                        )
                                  .List<object>().Select(o => new PeptideInstanceRow(o));

                return result as System.Collections.IEnumerable;
            };

            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);
            treeListView.KeyPress += new KeyPressEventHandler(treeListView_KeyPress);

            radioButton1.CheckedChanged += new EventHandler(radioButton1_CheckedChanged);
        }


        void radioButton1_CheckedChanged (object sender, EventArgs e)
        {
            if (!(bool)_columnSettings[filteredSpectraColumn].Locked)
                filteredSpectraColumn.IsVisible = radioButton1.Checked;
            if (!(bool)_columnSettings[distinctMatchesColumn].Locked)
                distinctMatchesColumn.IsVisible = radioButton1.Checked;
            if (!(bool)_columnSettings[monoisotopicMassColumn].Locked)
                monoisotopicMassColumn.IsVisible = radioButton1.Checked;
            if (!(bool)_columnSettings[molecularWeightColumn].Locked)
                molecularWeightColumn.IsVisible = radioButton1.Checked;
            if (!(bool)_columnSettings[offsetColumn].Locked)
                offsetColumn.IsVisible = radioButton2.Checked;
            if (!(bool)_columnSettings[terminalSpecificityColumn].Locked)
                terminalSpecificityColumn.IsVisible = radioButton2.Checked;
            if (!(bool)_columnSettings[missedCleavagesColumn].Locked)
                missedCleavagesColumn.IsVisible = radioButton2.Checked;

            treeListView.RebuildColumns();
            if (session != null)
                SetData(session, dataFilter);
        }

        void treeListView_CellClick (object sender, CellClickEventArgs e)
        {
            if (e.ClickCount < 2 || e.Item == null || e.Item.RowObject == null)
                return;

            var newDataFilter = new DataFilter()
                                    {
                                        FilterSource = this,
                                    };

            if (e.Item.RowObject is PeptideRow)
            {
                newDataFilter.Peptide = new List<Peptide>
                                            {
                                                (e.Item.RowObject as PeptideRow).Peptide
                                            };
            }
            else if (e.Item.RowObject is PeptideSpectrumMatchRow)
            {
                newDataFilter.DistinctPeptideKey = new List<DistinctPeptideFormat>
                                                       {
                                                           (e.Item.RowObject as PeptideSpectrumMatchRow).DistinctPeptide
                                                       };
            }

            if (PeptideViewFilter != null)
                PeptideViewFilter(this, newDataFilter);
        }

        void treeListView_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != (char)Keys.Enter)
                return;

            e.Handled = true;
            var newDataFilter = new DataFilter {FilterSource = this,};

            foreach (var item in treeListView.SelectedObjects)
            {
                if (item == null)
                    continue;

                if (item is PeptideRow)
                {
                    if (newDataFilter.Peptide == null)
                        newDataFilter.Peptide = new List<Peptide>();
                    newDataFilter.Peptide.Add((item as PeptideRow).Peptide);
                }
                else if (item is PeptideSpectrumMatchRow)
                {
                    if (newDataFilter.DistinctPeptideKey == null)
                        newDataFilter.DistinctPeptideKey = new List<DistinctPeptideFormat>();
                    newDataFilter.DistinctPeptideKey.Add((item as PeptideSpectrumMatchRow).DistinctPeptide);
                }
            }

            if (PeptideViewFilter != null)
                PeptideViewFilter(this, newDataFilter);
        }

        public event PeptideViewFilterEventHandler PeptideViewFilter;

        private NHibernate.ISession session;

        private DataFilter viewFilter; // what the user has filtered on
        private DataFilter dataFilter; // how this view is filtered (i.e. never on its own rows)
        private DataFilter basicDataFilter; // the basic filter without the user filtering on rows

        private IList<PeptideRow> rowsByPeptide, basicRowsByPeptide;

        private List<OLVColumn> pivotColumns = new List<OLVColumn>();
        private Dictionary<long, SpectrumSource> sourceById;
        private Dictionary<long, SpectrumSourceGroup> groupById;
        private Map<long, Map<long, PeptideStats>> statsPerPeptideBySpectrumSource;
        private Map<long, Map<long, PeptideStats>> statsPerPeptideBySpectrumSourceGroup;
        private IList<Pivot<PivotBy>> checkedPivots;

        // TODO: support multiple selected objects
        string[] oldSelectionPath = new string[] { };

        public void SetData (NHibernate.ISession session, DataFilter dataFilter)
        {
            this.session = session;
            viewFilter = dataFilter;
            this.dataFilter = new DataFilter(dataFilter) {Peptide = null, DistinctPeptideKey = null};

            // stored to avoid cross-thread calls on the control
            checkedPivots = pivotSetupControl.CheckedPivots;

            if (treeListView.SelectedObject is PeptideRow)
                oldSelectionPath = new string[] { treeListView.SelectedItem.Text };
            else if (treeListView.SelectedObject is PeptideSpectrumMatchRow)
                oldSelectionPath = new string[] { (treeListView.SelectedObject as PeptideSpectrumMatchRow).PeptideSpectrumMatch.Peptide.Sequence, treeListView.SelectedItem.Text };

            ClearData();

            Text = TabText = "Loading peptide view...";

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
                        Scope = "PeptideTableForm",
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

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = kvp.Value.Visible;
                treeListView.RebuildColumns();

                if (session != null)
                    SetData(session, dataFilter);
            }
        }

        public void ClearData ()
        {
            Text = TabText = "Peptide View";

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
                var peptideQuery = session.CreateQuery("SELECT psm.Peptide, " +
                                                       "       COUNT(DISTINCT psm.SequenceAndMassDistinctKey), " +
                                                       "       COUNT(DISTINCT psm.Spectrum) " +
                                                       dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                       "GROUP BY psm.Peptide " +
                                                       "ORDER BY COUNT(DISTINCT psm.SequenceAndMassDistinctKey) DESC, COUNT(DISTINCT psm.Spectrum) DESC");

                peptideQuery.SetReadOnly(true);

                var statsPerPeptideBySpectrumSourceQuery = session.CreateQuery(
                    "SELECT s.Source.id, " +
                    "       psm.Peptide.id, " +
                    "       COUNT(DISTINCT psm.SequenceAndMassDistinctKey), " +
                    "       COUNT(DISTINCT psm.Spectrum.id) " +
                    dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                      DataFilter.PeptideSpectrumMatchToSpectrumSource) +
                    "GROUP BY psm.Peptide.id, s.Source.id");

                var statsPerPeptideBySpectrumSourceGroupQuery = session.CreateQuery(
                    "SELECT ssgl.Group.id, " +
                    "       psm.Peptide.id, " +
                    "       COUNT(DISTINCT psm.SequenceAndMassDistinctKey), " +
                    "       COUNT(DISTINCT psm.Spectrum.id) " +
                    dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                      DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink) +
                    "GROUP BY psm.Peptide.id, ssgl.Group.id");

                sourceById = session.Query<SpectrumSource>().Where(o => o.Group != null).ToDictionary(o => o.Id.Value);
                groupById = session.Query<SpectrumSourceGroup>().ToDictionary(o => o.Id.Value);

                var stats = statsPerPeptideBySpectrumSource = new Map<long, Map<long, PeptideStats>>();
                if (checkedPivots.Count(o => o.Mode == PivotBy.Source) > 0)
                    foreach (var queryRow in statsPerPeptideBySpectrumSourceQuery.List<object[]>())
                        stats[(long) queryRow[0]][(long) queryRow[1]] = new PeptideStats(queryRow);

                var stats2 = statsPerPeptideBySpectrumSourceGroup = new Map<long, Map<long, PeptideStats>>();
                if (checkedPivots.Count(o => o.Mode == PivotBy.Group) > 0)
                    foreach (var queryRow in statsPerPeptideBySpectrumSourceGroupQuery.List<object[]>())
                        stats2[(long) queryRow[0]][(long) queryRow[1]] = new PeptideStats(queryRow);

                if (dataFilter.IsBasicFilter || viewFilter.Peptide != null || viewFilter.DistinctPeptideKey != null)
                {
                    // refresh basic data when basicDataFilter is unset or when the basic filter values have changed
                    if (basicDataFilter == null || (dataFilter.IsBasicFilter && dataFilter != basicDataFilter))
                    {
                        basicDataFilter = new DataFilter(dataFilter);
                        basicRowsByPeptide = peptideQuery.List<object[]>().Select(o => new PeptideRow(o)).ToList();
                    }

                    rowsByPeptide = basicRowsByPeptide;
                }
                else
                    rowsByPeptide = peptideQuery.List<object[]>().Select(o => new PeptideRow(o)).ToList();
            }
            catch (Exception)
            {
                throw;
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            long totalDistinctMatches = rowsByPeptide.Sum(o => o.DistinctMatchesWithRoundedMass);

            // show total counts in the form title
            Text = TabText = String.Format("Peptide View: {0} distinct peptides, {1} distinct matches", rowsByPeptide.Count, totalDistinctMatches);

            treeListView.Roots = rowsByPeptide;
            treeListView.AutoResizeColumn(0, ColumnHeaderAutoResizeStyle.ColumnContent);

            treeListView.Freeze();
            foreach (var pivotColumn in pivotColumns)
                treeListView.Columns.Remove(pivotColumn);

            pivotColumns = new List<OLVColumn>();

            var sourceNames = sourceById.Select(o => o.Value.Name);
            var stats = statsPerPeptideBySpectrumSource;
            var stats2 = statsPerPeptideBySpectrumSourceGroup;

            int insertIndex = monoisotopicMassColumn.Index > 0 ? monoisotopicMassColumn.Index : treeListView.ColumnsInDisplayOrder.Count;

            foreach (long sourceId in stats.Keys)
            {
                string uniqueSubstring;
                Util.UniqueSubstring(sourceById[sourceId].Name, sourceNames, out uniqueSubstring);
                var column = new OLVColumn() { Text = uniqueSubstring, Tag = sourceId };
                column.AspectGetter += delegate(object x)
                {
                    if (x is PeptideRow &&
                        stats[(long) column.Tag].Contains((x as PeptideRow).Peptide.Id.Value))
                        return stats[(long) column.Tag][(x as PeptideRow).Peptide.Id.Value].Spectra;
                    return null;
                };
                

                var newProperties = new ColumnProperty()
                {
                    Scope = "PeptideTableForm",
                    Type = "PivotColumn",
                    Name = column.Text,
                    DecimalPlaces = -1,
                    ColorCode = treeListView.BackColor.ToArgb(),
                    Visible = true,
                    Locked = false
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
                    if (x is PeptideRow &&
                        stats2[(long) column.Tag].Contains((x as PeptideRow).Peptide.Id.Value))
                        return stats2[(long) column.Tag][(x as PeptideRow).Peptide.Id.Value].Spectra;
                    return null;
                };

                var newProperties = new ColumnProperty()
                {
                    Scope = "PeptideTableForm",
                    Type = "PivotColumn",
                    Name = column.Text,
                    DecimalPlaces = -1,
                    ColorCode = treeListView.BackColor.ToArgb(),
                    Visible = true,
                    Locked = false
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
            expandSelectionPath(oldSelectionPath);

            treeListView.Refresh();
        }

        private void expandSelectionPath (IEnumerable<string> selectionPath)
        {
            OLVListItem selectedItem = null;
            foreach (string branch in selectionPath)
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
                kvp.Value.Visible = kvp.Key.IsVisible;

            var ccf = new ColumnControlForm(_columnSettings, currentColors);

            if (ccf.ShowDialog() == DialogResult.OK)
            {
                _columnSettings = ccf.SavedSettings;

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = (bool)kvp.Value.Visible;

                treeListView.BackColor = ccf.WindowBackColorBox.BackColor;
                treeListView.ForeColor = ccf.WindowTextColorBox.BackColor;

                SetColumnAspectGetters();
                treeListView.Refresh();
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
                    Scope = "PeptideTableForm",
                    Name = kvp.Key.Text,
                    Type = kvp.Value.Type,
                    DecimalPlaces = kvp.Value.DecimalPlaces,
                    ColorCode = kvp.Value.ColorCode,
                    Visible = kvp.Value.Visible,
                    Locked = false
                });
            }

            if (!pivotToo)
                currentList.RemoveAll(x => x.Type == "PivotColumn");

            currentList.Add(new ColumnProperty
            {
                Scope = "PeptideTableForm",
                Name = "BackColor",
                Type = "GlobalSetting",
                DecimalPlaces = -1,
                ColorCode = treeListView.BackColor.ToArgb(),
                Visible = false,
                Locked = false
            });
            currentList.Add(new ColumnProperty
            {
                Scope = "PeptideTableForm",
                Name = "TextColor",
                Type = "GlobalSetting",
                DecimalPlaces = -1,
                ColorCode = treeListView.ForeColor.ToArgb(),
                Visible = false,
                Locked = false
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

    public delegate void PeptideViewFilterEventHandler (PeptideTableForm sender, DataFilter peptideViewFilter);
}
