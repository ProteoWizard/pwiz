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
using NHibernate.Linq;
using IDPicker.DataModel;
using IDPicker.Controls;
using PopupControl;
using pwiz.CLI.data;
using proteome = pwiz.CLI.proteome;

namespace IDPicker.Forms
{
    using DataFilter = DataModel.DataFilter;
    using System.Collections;

    public partial class ModificationTableForm : DockableForm, TableExporter.ITable
    {
        public DataGridView DataGridView { get { return dataGridView; } }

        public ModificationTableForm ()
        {
            InitializeComponent();

            // set Name of controls hosted in ToolStripControlHosts to the ToolStripItem's Name, for better UI Automation access
            this.InitializeAccessibleNames();

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                e.Cancel = true;
                DockState = DockState.DockBottomAutoHide;
            };

            Text = TabText = "Modification View";
            Icon = Properties.Resources.BlankIcon;

            dataGridView.PreviewCellClick += dataGridView_PreviewCellClick;
            dataGridView.CellDoubleClick += dataGridView_CellDoubleClick;
            dataGridView.KeyDown += dataGridView_KeyDown;
            dataGridView.CellFormatting += dataGridView_CellFormatting;
            dataGridView.DefaultCellStyleChanged += dataGridView_DefaultCellStyleChanged;
            dataGridView.RowTemplate = new DataGridViewRowWithAccessibleValues() { RowHeaderCellEmptyString = "Total" };
            dataGridView.RowTemplate.HeaderCell = new DataGridViewRowHeaderCellWithAccessibleValue();

            dataGridView.ShowCellToolTips = true;
            dataGridView.CellToolTipTextNeeded += dataGridView_CellToolTipTextNeeded;
            dataGridView.CellPainting += dataGridView_CellPainting;
            brush = new SolidBrush(dataGridView.ForeColor);

            // TODO: add display settings dialog like other forms have
            var style = dataGridView.DefaultCellStyle;
            filteredOutColor = style.ForeColor.Interpolate(style.BackColor, 0.5f);

            _unimodControl = new UnimodControl();
            _unimodPopup = new Popup(_unimodControl);
            _unimodPopup.AutoClose = true;
            _unimodPopup.FocusOnOpen = true;
            _unimodPopup.SizeChanged += (x, y) => { _unimodControl.Size = _unimodPopup.Size; };
            _unimodPopup.Closed += unimodPopup_Closed;

            pivotModeComboBox.SelectedIndex = 0;
            viewModeComboBox.SelectedIndex = 0;
            DistinctModificationFormat = new DistinctMatchFormat() { ModificationMassRoundToNearest = roundToNearestUpDown.Value };
        }

        const string deltaMassColumnName = "ΔMass";

        public decimal RoundToNearest
        {
            get { return roundToNearestUpDown.Value; }
            set { roundToNearestUpDown.Value = value; }
        }

        private string PivotMode { get; set; }
        private DistinctMatchFormat DistinctModificationFormat { get; set; }

        private string RoundedDeltaMassExpression
        {
            get
            {
                if (DistinctModificationFormat.ModificationMassRoundToNearest.HasValue)
                    return String.Format("ROUND(pm.Modification.MonoMassDelta/{0}, 0)*{0}", DistinctModificationFormat.ModificationMassRoundToNearest.Value);
                return "pm.Modification.MonoMassDelta";
            }
        }

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
            if (e.Clicks > 1 )
                return;

            var clientPoint = dataGridView.PointToScreen(e.Location);
            if (Math.Abs(clientPoint.X - dataGridView.RowHeadersWidth) < 5)
                return;

            if (e.RowIndex >= dataGridView.RowCount || e.ColumnIndex >= dataGridView.ColumnCount)
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

                applySort();

                e.Handled = true;
            }
            // clicking on row header sorts by count for the delta mass
            else if (e.ColumnIndex < 0)
            {
                // initial sort is descending
                if (columnSortRowIndex != e.RowIndex ||
                    columnSortOrder == SortOrder.None ||
                    columnSortOrder == SortOrder.Ascending)
                    columnSortOrder = SortOrder.Descending;
                else
                    columnSortOrder = SortOrder.Ascending;

                columnSortRowIndex = e.RowIndex;

                applySort();

                e.Handled = true;
            }
        }

        void applySort ()
        {
            // the row header is index -1, but sorts on column 0
            int sortColumnIndex = Math.Max(0, rowSortColumnIndex);
            int sortMultiplier = rowSortOrder == SortOrder.Ascending ? 1 : -1;
            dataGridView.DataSource = deltaMassTable.ApplySort((x, y) =>
            {
                // the total row is always first
                bool xIsTotalRow = Double.IsInfinity((double) x[0]);
                bool yIsTotalRow = Double.IsInfinity((double) y[0]);
                if (xIsTotalRow && yIsTotalRow) return 0;
                if (xIsTotalRow) return -1;
                if (yIsTotalRow) return 1;

                bool xNull = x[sortColumnIndex] == DBNull.Value;
                bool yNull = y[sortColumnIndex] == DBNull.Value;
                if (xNull && yNull) return -((IComparable) x[0]).CompareTo((IComparable) y[0]); // tie-breaker is delta mass (descending)
                if (xNull) return sortMultiplier * -1;
                if (yNull) return sortMultiplier * 1;

                int compare = ((IComparable) x[sortColumnIndex]).CompareTo((IComparable) y[sortColumnIndex]);
                if (compare == 0)
                    return -((IComparable) x[0]).CompareTo((IComparable) y[0]); // tie-breaker is delta mass (descending)
                return sortMultiplier * compare;
            });

            // after setting DataSource, table must be refiltered
            trimModificationGrid();

            if (dataGridView.Rows.Count == 0)
                return; // shouldn't happen

            if (columnSortRowIndex > -1)
            {
                var row = dataGridView.Rows[columnSortRowIndex];

                // build a map of columns by spectrum count (skip mass and total columns)
                var columnsBySiteAndSpectrumCount = new Map<int, Map<string, DataGridViewColumn>>();
                for (int i = 2; i < dataGridView.Columns.Count; ++i)
                {
                    var site = dataGridView.Columns[i].Name;
                    var spectrumCount = row.Cells[i].Value is int ? (int) row.Cells[i].Value : 0;
                    columnsBySiteAndSpectrumCount[spectrumCount][site] = dataGridView.Columns[i];
                }

                var columns = columnSortOrder == SortOrder.Descending ? columnsBySiteAndSpectrumCount.Values.Reverse()
                                                                      : columnsBySiteAndSpectrumCount.Values;

                // assign display index in order of spectrum count (site is tie-breaker)
                int displayIndex = 1; // start after mass and total columns
                foreach (var itr in columns)
                    foreach (var itr2 in itr)
                        itr2.Value.DisplayIndex = ++displayIndex;
                //dataGridView.Rows.OfType<DataGridViewRowWithAccessibleValues>().ForEach(o => o);
            }
        }

        void dataGridView_CellDoubleClick (object sender, DataGridViewCellEventArgs e)
        {
            // if no one is listening, do nothing
            if (ModificationViewFilter == null)
                return;

            // ignore header cells and the top-left total cell
            if (e.ColumnIndex < 0 || e.RowIndex < 0 || e.ColumnIndex == 1 && e.RowIndex == 0)
                return;

            var clientPoint = dataGridView.PointToClient(MousePosition);
            if (Math.Abs(clientPoint.X - dataGridView.RowHeadersWidth) < 5)
                return;

            var cell = dataGridView[e.ColumnIndex, e.RowIndex];

            // if the clicked cell is blank, don't apply a filter
            if (cell.Value == DBNull.Value)
                return;

            var newDataFilter = new DataFilter() { FilterSource = this };

            char? site = null;
            if (e.ColumnIndex > 0 && this.siteColumnNameToSite.Contains(cell.OwningColumn.HeaderText))
            {
                site = this.siteColumnNameToSite[cell.OwningColumn.HeaderText];
                newDataFilter.ModifiedSite = new List<char> {site.Value};
            }

            string massDeltaExpression = null;
            if (e.RowIndex > 0)
                massDeltaExpression = String.Format("ABS({0}-{1}) <= 0.0001", RoundedDeltaMassExpression, (cell.OwningRow.DataBoundItem as DataRowView)[0]);

            string whereExpression = String.Empty;
            if (massDeltaExpression != null && site != null)
                whereExpression = String.Format("WHERE {0} AND pm.Site='{1}' ", massDeltaExpression, site);
            else if (massDeltaExpression != null)
                whereExpression = String.Format("WHERE {0} ", massDeltaExpression);
            else if (site != null)
                whereExpression = String.Format("WHERE pm.Site='{0}' ", site);

            if (massDeltaExpression != null)
                newDataFilter.Modifications = session.CreateQuery(
                                                    "SELECT pm.Modification " +
                                                    "FROM PeptideSpectrumMatch psm JOIN psm.Modifications pm " +
                                                    whereExpression +
                                                    "GROUP BY pm.Modification.id")
                                                   .List<DataModel.Modification>();

            //if (newDataFilter.Modifications.Count == 0)
            //    throw new InvalidDataException("no modifications found at the rounded mass");

            // send filter event
            ModificationViewFilter(this, new ViewFilterEventArgs(newDataFilter));
        }

        void dataGridView_KeyDown (object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.Handled = true;
            var siteList = new Set<string>();
            var massDeltaExpressions = new Set<string>();
            var newDataFilter = new DataFilter
            {
                FilterSource = this,
                ModifiedSite = new List<char>()
            };

            string massDeltaFormat = String.Format("ABS({0}-{{0}}) <= 0.0001", RoundedDeltaMassExpression);

            foreach (DataGridViewCell cell in dataGridView.SelectedCells)
            {
                // if the clicked cell is blank, don't apply a filter
                if (cell.Value == DBNull.Value)
                    continue;

                // ignore header cells and the top-left total cell
                if (cell.ColumnIndex < 0 || cell.RowIndex < 0 || cell.ColumnIndex == 1 && cell.RowIndex == 0)
                    continue;

                char? newSite = null;
                if (cell.ColumnIndex > 0 && siteColumnNameToSite.Contains(cell.OwningColumn.HeaderText))
                    newSite = siteColumnNameToSite[cell.OwningColumn.HeaderText];

                if (newSite != null && !newDataFilter.ModifiedSite.Contains(newSite.Value))
                {
                    siteList.Add("pm.Site='" + newSite.ToString() + "'");
                    newDataFilter.ModifiedSite.Add(newSite.Value);
                }

                if (cell.RowIndex > 0)
                    massDeltaExpressions.Add(String.Format(massDeltaFormat, cell.OwningRow.Cells[0].Value));
            }

            if (newDataFilter.ModifiedSite.Count == 0)
                newDataFilter.ModifiedSite = null;
            
            string whereExpression = String.Empty;
            if (massDeltaExpressions.Count > 0 && siteList.Count > 0)
                whereExpression = String.Format("WHERE ({0}) AND ({1})' ",
                                                String.Join(" OR ", massDeltaExpressions.ToArray()),
                                                String.Join(" OR ", siteList.ToArray()));
            else if (massDeltaExpressions.Count > 0)
                whereExpression = String.Format("WHERE {0} ", String.Join(" OR ", massDeltaExpressions.ToArray()));
            else if (siteList.Count > 0)
                whereExpression = String.Format("WHERE {0} ", String.Join(" OR ", siteList.ToArray()));

            if (massDeltaExpressions.Count > 0)
                newDataFilter.Modifications = session.CreateQuery(
                    "SELECT pm.Modification " +
                    "FROM PeptideSpectrumMatch psm JOIN psm.Modifications pm " +
                    whereExpression +
                    " GROUP BY pm.Modification.id")
                    .List<DataModel.Modification>();

            // send filter event
            ModificationViewFilter(this, new ViewFilterEventArgs(newDataFilter));
        }

        public event EventHandler<ViewFilterEventArgs> ModificationViewFilter;
        public event EventHandler FinishedSetData;
        public event EventHandler StartingSetData;

        private NHibernate.ISession session;

        private DataFilter viewFilter; // what the user has filtered on
        private DataFilter dataFilter; // how this view is filtered (i.e. never on its own rows)
        private DataFilter basicDataFilter; // the basic filter without the user filtering on rows

        private Color filteredOutColor;

        private Map<string, char> siteColumnNameToSite;
        private DataTable deltaMassTable, basicDeltaMassTable;
        private int totalModifications, basicTotalModifications;

        private Map<string, Map<double, List<unimod.Modification>>> basicDeltaMassAnnotations;

        private Popup _unimodPopup;
        private UnimodControl _unimodControl;

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
            var siteColumnToTotal = new Map<string, int>();

            totalModifications = 0;

            var totalColumn = new DataColumn() { ColumnName = "Total", DataType = typeof(int) };
            deltaMassTable.Columns.Add(totalColumn);

            var totalRow = deltaMassTable.NewRow();
            totalRow[deltaMassColumnName] = Double.PositiveInfinity;
            deltaMassTable.Rows.Add(totalRow);

            string pivotMode = null;
            Invoke(new MethodInvoker(() => pivotMode = pivotModeComboBox.Text));

            int cellValueIndexInQueryRow;
            if (pivotMode == "Spectra")
                cellValueIndexInQueryRow = 2;
            else if (pivotMode == "Distinct Matches")
                cellValueIndexInQueryRow = 3;
            else if (pivotMode == "Distinct Peptides")
                cellValueIndexInQueryRow = 4;
            else
                throw new InvalidDataException("unknown modification view pivot type");

            foreach (var tuple in queryRows)
            {
                var mod = tuple[1] as DataModel.Modification;
                double roundedMass = DistinctModificationFormat.Round(mod.MonoMassDelta);
                char site = (char) tuple[0];
                string siteColumnName = GetSiteColumnName(site);

                if (!deltaMassTable.Columns.Contains(siteColumnName))
                {
                    deltaMassTable.Columns.Add(new DataColumn() { ColumnName = siteColumnName, DataType = typeof(int) });
                    siteColumnNameToSite[siteColumnName] = site;
                    totalRow[siteColumnName] = 0;
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

                int siteMods = Convert.ToInt32(tuple[cellValueIndexInQueryRow]);
                row[siteColumnName] = siteMods;
                row[totalColumn] = (int) row[totalColumn] + siteMods;
                totalRow[siteColumnName] = (int) totalRow[siteColumnName] + siteMods;
                totalModifications += siteMods;
            }
            totalRow[totalColumn] = totalModifications;
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
                    if (siteColumn.ColumnName == "Total" || Double.IsInfinity((double) deltaMassRow[deltaMassColumnName]))
                        continue;

                    double deltaMass = (double) deltaMassRow[deltaMassColumnName];

                    char deltaMassSite;
                    if (siteColumn.ColumnName == "N-term")
                        deltaMassSite = 'n';
                    else if (siteColumn.ColumnName == "C-term")
                        deltaMassSite = 'c';
                    else
                        deltaMassSite = siteColumn.ColumnName[0];

                    double tolerance = (double) dataFilter.DistinctMatchFormat.ModificationMassRoundToNearest.Value;
                    var filter = new unimod.Filter(deltaMass, tolerance)
                    {
                        site = unimod.site(deltaMassSite),
                        approved = null,
                        hidden = null
                    };
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
            if (session == null)
                return;

            if (StartingSetData != null)
                StartingSetData(this, EventArgs.Empty);

            Controls.OfType<Control>().ForEach(o => o.Enabled = false);

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

            bool phosphositeViewMode = viewModeComboBox.SelectedIndex == 2;
            accessionColumn.Visible = phosphositeViewMode;
            offsetColumn.Visible = phosphositeViewMode;
            probabilityColumn.Visible = phosphositeViewMode;
            massColumn.Visible = !phosphositeViewMode;
            unimodColumn.Visible = !phosphositeViewMode;

            Text = TabText = "Loading modification view...";

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += phosphositeViewMode ? new DoWorkEventHandler(setDataForPhosphositeView) : setData;
            workerThread.RunWorkerCompleted += renderData;
            workerThread.RunWorkerAsync();
        }

        public void ClearData ()
        {
            Text = TabText = "Modification View";

            Controls.OfType<Control>().ForEach(o => o.Enabled = false);

            detailDataGridView.Rows.Clear();
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
                if (dataFilter.IsBasicFilter || viewFilter.Modifications != null || viewFilter.ModifiedSite != null)
                {
                    var query = session.CreateQuery("SELECT pm.Site, pm.Modification, COUNT(DISTINCT psm.Spectrum.id), COUNT(DISTINCT psm.DistinctMatchId), COUNT(DISTINCT psm.Peptide.id) " +
                                                    dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                      DataFilter.PeptideSpectrumMatchToPeptideModification) +
                                                    "GROUP BY pm.Site, " + RoundedDeltaMassExpression);
                    query.SetReadOnly(true);

                    // refresh basic data when basicDataFilter is unset or when the basic filter values have changed
                    if (basicDataFilter == null || (dataFilter.IsBasicFilter && !dataFilter.Equals(basicDataFilter)))
                    {
                        basicDataFilter = new DataFilter(dataFilter);
                        IList<object[]> queryRows; lock (session) queryRows = query.List<object[]>();
                        basicDeltaMassTable = createDeltaMassTableFromQuery(queryRows, out basicTotalModifications, out siteColumnNameToSite);
                        findDeltaMassAnnotations();
                        deltaMassTable = basicDeltaMassTable;
                        SetUnimodDefaults(queryRows);
                        PopulateModificationDetailView(queryRows);
                    }

                    deltaMassTable = basicDeltaMassTable;
                    totalModifications = basicTotalModifications;
                }
                else
                {
                    var query = session.CreateQuery("SELECT pm.Site, pm.Modification, COUNT(DISTINCT psm.Spectrum.id), COUNT(DISTINCT psm.DistinctMatchId), COUNT(DISTINCT psm.Peptide.id) " +
                                                    dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                      DataFilter.PeptideSpectrumMatchToPeptideModification) +
                                                    "GROUP BY pm.Site, " + RoundedDeltaMassExpression);
                    query.SetReadOnly(true);

                    Map<string, char> dummy;
                    IList<object[]> queryRows; lock (session) queryRows = query.List<object[]>();
                    deltaMassTable = createDeltaMassTableFromQuery(queryRows, out totalModifications, out dummy);
                    SetUnimodDefaults(queryRows);
                    PopulateModificationDetailView(queryRows);
                }

                if (FinishedSetData != null)
                    FinishedSetData(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        void setDataForPhosphositeView(object sender, DoWorkEventArgs e)
        {
            // get the modifications grouped by protein and protein offset, using the additional detailTableGridDataView columns for this view mode
            // don't populate the delta mass table or main detail view table; if the view mode is switched, those queries will have to be run
            try
            {
                var phosphoFilter = new DataFilter(dataFilter);
                phosphoFilter.Modifications = session.Query<Modification>().Where(o => Math.Round(o.MonoMassDelta) == 80.0).ToList();
                var query = session.CreateQuery("SELECT pro.Accession, pi.Offset+pm.Offset+1, pm.Site, pm.Modification, COUNT(DISTINCT psm.Spectrum.id), COUNT(DISTINCT psm.DistinctMatchId), COUNT(DISTINCT psm.Peptide.id) " +
                                                phosphoFilter.GetFilteredQueryString(DataFilter.FromProtein,
                                                                                     DataFilter.ProteinToPeptideModification) +
                                                " AND pro.IsDecoy = 0 " +
                                                "GROUP BY pro.Id, pm.Modification.Id, pi.Offset+pm.Offset");
                query.SetReadOnly(true);
                IList<object[]> queryRows = null;

                if (dataFilter.IsBasicFilter || viewFilter.Modifications != null || viewFilter.ModifiedSite != null)
                {

                    // refresh basic data when basicDataFilter is unset or when the basic filter values have changed
                    //if (basicDataFilter == null || (dataFilter.IsBasicFilter && !dataFilter.Equals(basicDataFilter)))
                    {
                        basicDataFilter = new DataFilter(dataFilter);
                        lock (session) queryRows = query.List<object[]>();
                        //basicDeltaMassTable = createDeltaMassTableFromQuery(queryRows, out basicTotalModifications, out siteColumnNameToSite);
                        //findDeltaMassAnnotations();
                        //deltaMassTable = basicDeltaMassTable;
                        //SetUnimodDefaults(queryRows);
                    }

                    //deltaMassTable = basicDeltaMassTable;
                    //totalModifications = basicTotalModifications;
                }
                else
                {
                    //Map<string, char> dummy;
                    lock (session) queryRows = query.List<object[]>();
                    //deltaMassTable = createDeltaMassTableFromQuery(queryRows, out totalModifications, out dummy);
                    //SetUnimodDefaults(queryRows);
                }

                detailDataGridView.Rows.Clear();
                foreach (var tuple in queryRows)
                {
                    try
                    {
                        var mod = (tuple[3] as DataModel.Modification);
                        var roundedMass = DistinctModificationFormat.Round(mod.MonoMassDelta);
                        var explanations = _unimodControl.GetPossibleDescriptions((char)tuple[2], roundedMass).Distinct();

                        var newRow = new object[]
                        {
                            tuple[0], // accession
                            Convert.ToInt32(tuple[1]), // offset
                            tuple[2], // site
                            roundedMass, // mass
                            null, // probability
                            Convert.ToInt32(tuple[6]), // spectra
                            Convert.ToInt32(tuple[5]), // matches
                            Convert.ToInt32(tuple[4]), // peptides
                            String.Join("; ", explanations) // description
                        };

                        var rowIndex = detailDataGridView.Rows.Add(newRow);
                    }
                    catch { } //if row gives errors dont add it
                }

                if (FinishedSetData != null)
                    FinishedSetData(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        private void PopulateModificationDetailView(IEnumerable<object[]> queryRows)
        {
            detailDataGridView.Rows.Clear();
            foreach (var tuple in queryRows)
            {
                try
                {
                    var mod = (tuple[1] as DataModel.Modification);
                    var roundedMass = DistinctModificationFormat.Round(mod.MonoMassDelta);
                    var explanations = _unimodControl.GetPossibleDescriptions((char)tuple[0], roundedMass).Distinct();

                    var newRow = new object[]
                    {
                        null, // accession
                        null, // offset
                        tuple[0], // site
                        roundedMass, // mass
                        null, // probability
                        Convert.ToInt32(tuple[4]), // spectra
                        Convert.ToInt32(tuple[3]), // matches
                        Convert.ToInt32(tuple[2]), // peptides
                        String.Join("; ", explanations) // description
                    };

                    var rowIndex = detailDataGridView.Rows.Add(newRow);
                }
                catch { } //if row gives errors dont add it
            }
        }

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
                        return String.Format("{0};{1}", Owner.HeaderCell.Value ?? RowHeaderCellEmptyString, String.Join(";", Owner.Cells.OfType<DataGridViewCell>().Where(o => o.Visible).Select(o => o.Value ?? String.Empty)));
                    }
                }
            }

            protected override AccessibleObject CreateAccessibilityInstance()
            {
                return new DataGridViewRowWithAccessibleValuesAccessibleObject(this) { RowHeaderCellEmptyString = RowHeaderCellEmptyString };
            }
        }

        private class DataGridViewRowHeaderCellWithAccessibleValue : DataGridViewRowHeaderCell
        {
            public void SnapToUnimod(double tolerance)
            {
                double deltaMass = (double) (OwningRow.DataBoundItem as DataRowView).Row[deltaMassColumnName];
                var headerText = new StringBuilder(deltaMass.ToString());

                // see if row snaps to a single Unimod delta mass at a very tight tolerance (note that a single exact mass can match multiple mods)
                var filter = new unimod.Filter(deltaMass, tolerance) { approved = null, hidden = null };
                var mods = unimod.modifications(filter);
                //if (mods.Select(o => o.deltaMonoisotopicMass).Distinct().Count() == 1)
                {
                    foreach (var mod in mods)
                        headerText.AppendFormat("\n{0}", mod.name);
                }
                Value = headerText.ToString();
            }

            protected class DataGridViewRowHeaderCellWithAccessibleValueAccessibleObject : DataGridViewRowHeaderCellAccessibleObject
            {
                public DataGridViewRowHeaderCellWithAccessibleValueAccessibleObject(DataGridViewRowHeaderCell owner) : base(owner) {}
                public override string Name
                {
                    get
                    {
                        var row = Owner.OwningRow;
                        if (Double.IsInfinity((double) (row.DataBoundItem as DataRowView).Row[deltaMassColumnName]))
                            return "Total";
                        else
                        {
                            return row.HeaderCell.Value.ToString();
                        }
                    }
                }
            }

            protected override AccessibleObject CreateAccessibilityInstance()
            {
                return new DataGridViewRowHeaderCellWithAccessibleValueAccessibleObject(this);
            }
        }

        private void trimModificationGrid()
        {
            int minColumns, minRows;
            if (!Int32.TryParse(minColumnTextBox.Text, out minColumns) ||
                !Int32.TryParse(minRowTextBox.Text, out minRows))
                return;

            (dataGridView.DataSource as DataTable).DefaultView.RowFilter = "";

            bool unimodFilter = false;
            List<double> unimodMasses = null;
            List<char> unimodSites = null;

            if (_unimodControl != null)
            {
                unimodMasses = _unimodControl.GetUnimodMasses();
                unimodSites = _unimodControl.GetUnimodSites();
                if (unimodMasses.Count > 0 && unimodSites.Count > 0)
                    unimodFilter = true;
            }

            dataGridView.SuspendLayout();
            foreach (DataGridViewColumn column in dataGridView.Columns)
                if (column.Index > 0) // skip hidden delta mass column
                {
                    column.Visible = (int) deltaMassTable.Rows[0][column.Index] >= minColumns;
                    if (unimodFilter && column.Index > 1) // Total column is always visible
                        column.Visible = column.Visible && unimodSites.Contains(siteColumnNameToSite[column.HeaderText]);
                }

            var rowFilter = new StringBuilder();
            rowFilter.AppendFormat("[{0}] = 'Infinity' OR Total >= {1}", deltaMassColumnName, minRows);
            if (unimodFilter)
                rowFilter.AppendFormat(" AND ({0})", String.Join(" OR ", unimodMasses.Select(o => String.Format("([{0}]-{1} <= 0.0001 AND [{0}]-{1} >= -0.0001)", deltaMassColumnName, o)).ToArray()));
            (dataGridView.DataSource as DataTable).DefaultView.RowFilter = rowFilter.ToString();

            var unimodRowStyle = new DataGridViewCellStyle(dataGridView.RowHeadersDefaultCellStyle)
            {
                Font = new Font(dataGridView.RowHeadersDefaultCellStyle.Font, FontStyle.Bold)
            };

            dataGridView.Columns["Total"].HeaderCell.Style = unimodRowStyle;

            // set the row headers from the invisible delta mass column
            var g = dataGridView.CreateGraphics();
            float maxRowHeaderWidth = g.MeasureString(deltaMassColumnName, unimodRowStyle.Font).Width;
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                if (Double.IsInfinity((double) (row.DataBoundItem as DataRowView).Row[deltaMassColumnName]))
                {
                    row.HeaderCell.Value = "Total";
                    row.HeaderCell.Style = unimodRowStyle;
                }
                else
                {
                    (row.HeaderCell as DataGridViewRowHeaderCellWithAccessibleValue).SnapToUnimod((double)roundToNearestUpDown.Value);
                    string[] lines = row.HeaderCell.Value.ToString().Split('\n');
                    if (lines.Length > 1)
                    {
                        row.HeaderCell.Style = unimodRowStyle;
                        maxRowHeaderWidth = Math.Max(maxRowHeaderWidth, (float) lines.Max(line => g.MeasureString(line, unimodRowStyle.Font).Width));
                        row.Height = (int) Math.Ceiling(lines.Sum(line => g.MeasureString(line, unimodRowStyle.Font).Height)) + 12;
                    }
                }
                maxRowHeaderWidth = Math.Max(maxRowHeaderWidth, g.MeasureString((string) row.HeaderCell.Value, unimodRowStyle.Font).Width);
            }
            dataGridView.RowHeadersWidth = (int) Math.Ceiling(maxRowHeaderWidth * 1.3) + 25;

            dataGridView.ResumeLayout();

            trimModificationDetailTable();
        }

        private void trimModificationDetailTable()
        {
            if (_unimodControl == null || detailDataGridView == null || detailDataGridView.Rows.Count == 0)
                return;

            var pairs = _unimodControl.GetUnimodPairs();
            var minMatches = Int32.Parse(minMatchesTextBox.Text);

            if (!pairs.Any())
            {
                detailDataGridView.SuspendLayout();
                foreach (DataGridViewRow row in detailDataGridView.Rows)
                {
                    int matches = (int) row.Cells[matchesColumn.Index].Value;
                    row.Visible = matches >= minMatches;
                }
                detailDataGridView.ResumeLayout();
                return;
            }

            detailDataGridView.SuspendLayout();
            foreach (DataGridViewRow row in detailDataGridView.Rows)
            {
                var residue = (char)row.Cells[0].Value;
                var mass = (double)row.Cells[1].Value;
                int matches = (int)row.Cells[matchesColumn.Index].Value;
                if (!pairs.ContainsKey(residue) || !pairs[residue].Contains(mass) || matches < minMatches)
                    row.Visible = false;
                else
                    row.Visible = true;
            }
            detailDataGridView.ResumeLayout();
        }

        private void SetUnimodDefaults(IList<object[]> queryRows)
        {
            var roundedDeltaMasses = deltaMassTable.Rows.Cast<DataRow>().Select(o => DistinctModificationFormat.Round((double) o[0]));
            var massSet = new HashSet<double>(roundedDeltaMasses);

            var siteSet = new HashSet<char>();
            foreach (var item in queryRows)
                siteSet.Add((char) item[0]);

            if (_unimodControl.InvokeRequired)
                _unimodControl.Invoke(new MethodInvoker(() => _unimodControl.SetUnimodDefaults(siteSet, massSet, DistinctModificationFormat)));
            else
                _unimodControl.SetUnimodDefaults(siteSet, massSet, DistinctModificationFormat);
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Exception)
            {
                Program.HandleException(e.Result as Exception);
                return;
            }

            Controls.OfType<Control>().ForEach(o => o.Enabled = true);

            Text = TabText = String.Format("Modification View: {0} modified {1}", totalModifications, PivotMode.ToLower());

            var currentView = viewModeComboBox.SelectedIndex == 0 ? dataGridView : detailDataGridView;
            if (totalModifications > 0)
            {
                currentView.Visible = true;
                dataGridView.DataSource = deltaMassTable;
                dataGridView.Columns[deltaMassColumnName].Visible = false;

                applySort();

                if (deltaMassTable.Rows.Count > 0)
                {
                    dataGridView[0, 0].Selected = false;
                    if (oldSelectedAddress != null)
                    {
                        string columnName = oldSelectedAddress.second;
                        int massColumnIndex = dataGridView.Columns[deltaMassColumnName].Index;
                        var oldSelectedRow = dataGridView.Rows.Cast<DataGridViewRow>().SingleOrDefault(o => (double) o.Cells[massColumnIndex].Value == oldSelectedAddress.first);
                        if (dataGridView.Columns.Contains(columnName) && oldSelectedRow != null)
                        {
                            var oldSelectedCell = dataGridView[columnName, oldSelectedRow.Index];
                            if (oldSelectedCell.Visible)
                            {
                                dataGridView.FirstDisplayedCell = oldSelectedCell;
                                oldSelectedCell.Selected = true;
                            }
                        }
                    }
                }
            }
            else
                currentView.Visible = false;

            currentView.Refresh();
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

        private void exportButton_Click (object sender, EventArgs e)
        {
            if (sender == exportButton)
                exportMenu.Show(Cursor.Position);
            else
                exportDetailMenu.Show(Cursor.Position);
        }

        #region Export methods

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
            var currentTable = detailDataGridView.Visible ? detailDataGridView : dataGridView;

            if (exportSelectedCellsOnly &&
                currentTable.SelectedCells.Count > 0 &&
                !currentTable.AreAllCellsSelected(false))
            {
                var selectedRows = new Set<int>();
                var selectedColumns = new Map<int, int>(); // ordered by DisplayIndex

                foreach (DataGridViewCell cell in currentTable.SelectedCells)
                {
                    selectedRows.Add(cell.RowIndex);
                    selectedColumns[cell.OwningColumn.DisplayIndex] = cell.ColumnIndex;
                }

                exportedRows = selectedRows.ToList();
                exportedColumns = selectedColumns.Values;
            }
            else
            {
                exportedRows = currentTable.Rows.Cast<DataGridViewRow>().Select(o => o.Index).ToList();
                exportedColumns = currentTable.GetVisibleColumnsInDisplayOrder().Select(o => o.Index).ToList();
            }

            // add column headers
            var headers = new List<string>();
            if (currentTable.RowHeadersVisible)
                headers.Add(deltaMassColumnName.Replace("Δ", "Delta "));
            foreach (var columnIndex in exportedColumns)
                headers.Add(currentTable.Columns[columnIndex].HeaderText);

            foreach (int rowIndex in exportedRows)
            {
                var exportedRow = new ExportedTableRow();
                exportedRow.Headers = headers;
                exportedRow.Cells = new List<string>();

                if (currentTable.Rows[rowIndex].HeaderCell.Value != null)
                    exportedRow.Cells.Add(currentTable.Rows[rowIndex].HeaderCell.Value.ToString().Replace("\n", " ; "));
                foreach (var columnIndex in exportedColumns)
                {
                    object value = currentTable[columnIndex, rowIndex].Value ?? String.Empty;
                    exportedRow.Cells.Add(value.ToString());
                }

                yield return exportedRow;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator) GetEnumerator();
        }

        public virtual List<List<string>> GetFormTable (bool selected, bool detail)
        {
            var exportTable = new List<List<string>>();
            IList<int> exportedRows, exportedColumns;
            var currentTable = detail ? detailDataGridView : dataGridView;

            if (selected && currentTable.SelectedCells.Count > 0 && !currentTable.AreAllCellsSelected(false))
            {
                var selectedRows = new Set<int>();
                var selectedColumns = new Map<int, int>(); // ordered by DisplayIndex

                foreach (DataGridViewCell cell in currentTable.SelectedCells)
                {
                    selectedRows.Add(cell.RowIndex);
                    selectedColumns[cell.OwningColumn.DisplayIndex] = cell.ColumnIndex;
                }

                exportedRows = selectedRows.ToList();
                exportedColumns = selectedColumns.Values;
            }
            else
            {
                exportedRows = currentTable.Rows.Cast<DataGridViewRow>().Select(o => o.Index).ToList();
                exportedColumns = currentTable.GetVisibleColumnsInDisplayOrder().Select(o => o.Index).ToList();
            }

            // add column headers
            exportTable.Add(new List<string>());
            if (currentTable.RowHeadersVisible)
                exportTable.Last().Add(deltaMassColumnName.Replace("Δ", "Delta "));
            foreach (var columnIndex in exportedColumns)
                exportTable.Last().Add(currentTable.Columns[columnIndex].HeaderText);

            foreach (int rowIndex in exportedRows)
            {
                var rowText = new List<string>();
                if (currentTable.Rows[rowIndex].HeaderCell.Value != null)
                    rowText.Add(currentTable.Rows[rowIndex].HeaderCell.Value.ToString().Replace("\n"," ; "));
                foreach (var columnIndex in exportedColumns)
                {
                    object value = currentTable[columnIndex, rowIndex].Value ?? String.Empty;
                    rowText.Add(value.ToString());
                }

                exportTable.Add(rowText);
            }

            return exportTable;
        }

        internal List<TableExporter.TableTreeNode> getModificationTree()
        {
            var groupNodes = new List<TableExporter.TableTreeNode>();

            var query = session.CreateQuery("SELECT pm.Site, pm.Modification, COUNT(DISTINCT psm.Spectrum) " +
                                                dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                  DataFilter.PeptideSpectrumMatchToPeptideModification) +
                                                "GROUP BY pm.Site, ROUND(pm.Modification.MonoMassDelta) " +
                                                "ORDER BY ROUND(pm.Modification.MonoMassDelta)");

            var firstLevelHeaders = new List<string> { "'Modified Site'", "'Mass'", "'Peptides'", "'Spectra'" };
            var secondLevelHeaders = new List<string> { "'Sequence'", "'Cluster'", "'Spectra'" };

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

                var peptideList = PeptideTableForm.DistinctMatchRow.GetRows(session, modFilter);
                if (!peptideList.Any()) continue;


                var newNode = new TableExporter.TableTreeNode
                {
                    Text = site + mod.AvgMassDelta.ToString(),
                    Headers = firstLevelHeaders,
                    Cells = new List<string>
                            {
                                "'" +site + "'",
                                mod.AvgMassDelta.ToString(),
                                peptideList.Count().ToString(),
                                specCount.ToString()
                            }
                };
                foreach (var peptide in peptideList)
                {
                    var cluster = peptide.PeptideSpectrumMatch.Peptide.Instances.First().Protein.Cluster;
                    var subNode = new TableExporter.TableTreeNode
                    {
                        Text = peptide.DistinctMatch.ToString(),
                        Headers = secondLevelHeaders,
                        Cells = new List<string>
                                {
                                    "'" + peptide.Peptide.Sequence + "'",
                                    String.Format("'<a href = \"cluster{0}.html\">{0}</a>'", cluster),
                                    peptide.Spectra.ToString(),
                                }
                    };
                    newNode.Nodes.Add(subNode);
                }
                groupNodes.Add(newNode);
            }

            return groupNodes;
        }


        protected void ExportTable(object sender, EventArgs e)
        {
            exportSelectedCellsOnly = sender == copySelectedCellsToClipboardToolStripMenuItem ||
                                      sender == exportSelectedCellsToFileToolStripMenuItem ||
                                      sender == showSelectedCellsInExcelToolStripMenuItem;

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
            progressWindow.Show();

            tempTable = new List<List<string>>();
            var bg = new BackgroundWorker();
            bg.RunWorkerCompleted += (x, y) =>
            {
                if (y.Error != null) Program.HandleException(y.Error);
                progressWindow.Close();
                if (sender == copyToClipboardToolStripMenuItem ||
                    sender == copySelectedCellsToClipboardToolStripMenuItem ||
                    sender == copyToClipboardDetailToolStripMenuItem ||
                    sender == copySelectedCellsToClipboardDetailToolStripMenuItem)
                    TableExporter.CopyToClipboard(tempTable);
                else if (sender == exportToFileToolStripMenuItem ||
                         sender == exportSelectedCellsToFileToolStripMenuItem ||
                         sender == exportToFileDetailToolStripMenuItem ||
                         sender == exportSelectedCellsToFileDetailToolStripMenuItem)
                    TableExporter.ExportToFile(tempTable);
                else if (sender == showInExcelToolStripMenuItem ||
                         sender == showSelectedCellsInExcelToolStripMenuItem ||
                         sender == showInExcelDetailToolStripMenuItem ||
                         sender == showSelectedCellsInExcelDetailToolStripMenuItem)
                {
                    var exportWrapper = new Dictionary<string, List<List<string>>> { { Name, tempTable } };
                    TableExporter.ShowInExcel(exportWrapper, false);
                }
            };
            bg.DoWork += (x, y) =>
            {
                tempTable = GetFormTable(selected, detail);
            };
            bg.RunWorkerAsync();*/
        }
        #endregion

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

        private void MinCountFilter_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                ModFilter_Leave(sender,e);
                e.Handled = true;
            }
            else if (!Char.IsDigit(e.KeyChar) && !Char.IsControl(e.KeyChar))
                e.Handled = true;
        }

        private void ModFilter_Leave(object sender, EventArgs e)
        {
            var textbox = sender as TextBox;
            if (textbox != null)
            {
                //try
                {
                    int value;
                    if (!Int32.TryParse(textbox.Text, out value) || value < 1)
                        textbox.Text = "2";

                    trimModificationGrid();
                }
                /*catch
                {
                    textbox.Text = "1";
                }*/
            }
        }

        private void unimodButton_Click (object sender, EventArgs e)
        {
            _unimodPopup.Show(unimodButton);
        }

        private void unimodPopup_Closed (object sender, EventArgs e)
        {
            try
            {
                if (!_unimodControl.ChangesMade(true))
                    return;

                trimModificationGrid();
            }
            catch (Exception ex)
            {
                Program.HandleException(ex);
            }
        }

        // override the default increment mechanism:
        // increment by multiplying by 10, decrement by dividing by 10
        bool roundToNearestUpDownChanging;
        private decimal roundToNearestValue = 1;
        private void roundToNearestUpDown_ValueChanged (object sender, EventArgs e)
        {
            if (roundToNearestUpDownChanging)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(roundToNearestUpDown.Value.ToString(), @"^10*\.?0*$") &&
                !System.Text.RegularExpressions.Regex.IsMatch(roundToNearestUpDown.Value.ToString(), @"^0?\.0*10*$"))
                    roundToNearestUpDown.Value = roundToNearestValue;
                return;
            }

            //make sure not rounding to strange numbers
            var valueAsString = roundToNearestUpDown.Value.ToString();
            decimal oldValue = DistinctModificationFormat.ModificationMassRoundToNearest.Value;
            roundToNearestUpDownChanging = true;

            if (!System.Text.RegularExpressions.Regex.IsMatch(valueAsString, @"^10*\.?0*$") &&
                !System.Text.RegularExpressions.Regex.IsMatch(valueAsString, @"^0?\.0*10*$") &&
                (System.Text.RegularExpressions.Regex.IsMatch(oldValue.ToString(), @"^10*\.?0*$") ||
                System.Text.RegularExpressions.Regex.IsMatch(oldValue.ToString(), @"^0?\.0*10*$")))
            {
                roundToNearestUpDown.Value = roundToNearestUpDown.Value < oldValue
                                           ? Math.Max(roundToNearestUpDown.NumericUpDownControl.Minimum, oldValue / 10)
                                           : Math.Min(roundToNearestUpDown.NumericUpDownControl.Maximum, oldValue * 10);
            }
            else
            {
                if (roundToNearestUpDown.Value - roundToNearestUpDown.NumericUpDownControl.Increment == oldValue)
                    roundToNearestUpDown.Value = Math.Min(roundToNearestUpDown.NumericUpDownControl.Maximum, oldValue * 10);
                else if (roundToNearestUpDown.Value + roundToNearestUpDown.NumericUpDownControl.Increment == oldValue)
                    roundToNearestUpDown.Value = Math.Max(roundToNearestUpDown.NumericUpDownControl.Minimum, oldValue / 10);
            }
            roundToNearestValue = roundToNearestUpDown.Value;
            roundToNearestUpDownChanging = false;

            basicDataFilter = null;
            basicDeltaMassTable = null;
            basicDeltaMassAnnotations = null;

            DistinctModificationFormat = new DistinctMatchFormat() { ModificationMassRoundToNearest = roundToNearestUpDown.Value };

            if (session != null)
                SetData(session, viewFilter);
        }

        private void pivotModeComboBox_SelectedIndexChanged (object sender, EventArgs e)
        {
            basicDataFilter = null;
            basicDeltaMassTable = null;
            basicDeltaMassAnnotations = null;

            PivotMode = (string) pivotModeComboBox.SelectedItem;

            if (session != null)
                SetData(session, viewFilter);
        }

        private void switchViewButton_Click(object sender, EventArgs e)
        {
            bool needDataRefresh = false;

            switch(viewModeComboBox.SelectedIndex)
            {
                // Grid view
                case 0:
                    dataGridView.Visible = true;
                    needDataRefresh = accessionColumn.Visible;

                    minRowLabel.Visible = true;
                    minRowTextBox.Visible = true;

                    minColumnLabel.Visible = true;
                    minColumnTextBox.Visible = true;

                    detailDataGridView.Visible = false;
                    pivotModeComboBox.Visible = true;

                    minMatchesTextBox.Visible = false;
                    minMatchesLabel.Visible = false;
                    break;

                // Detail table view
                case 1:
                    dataGridView.Visible = false;
                    needDataRefresh = accessionColumn.Visible;

                    minRowLabel.Visible = false;
                    minRowTextBox.Visible = false;

                    minColumnLabel.Visible = false;
                    minColumnTextBox.Visible = false;

                    detailDataGridView.Visible = true;
                    pivotModeComboBox.Visible = false;

                    minMatchesTextBox.Visible = true;
                    minMatchesLabel.Visible = true;
                    break;

                // Phosphosite view
                case 2:
                    dataGridView.Visible = false;
                    needDataRefresh = !accessionColumn.Visible;

                    minRowLabel.Visible = false;
                    minRowTextBox.Visible = false;

                    minColumnLabel.Visible = false;
                    minColumnTextBox.Visible = false;

                    detailDataGridView.Visible = true;
                    pivotModeComboBox.Visible = false;

                    minMatchesTextBox.Visible = false;
                    minMatchesLabel.Visible = false;
                    break;
            }

            if (needDataRefresh && session != null)
            {
                ClearData(true);
                SetData(session, viewFilter);
            }
        }
    }
}
