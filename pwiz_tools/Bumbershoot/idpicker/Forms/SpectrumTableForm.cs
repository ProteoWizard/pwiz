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
using System.IO;
using System.Windows.Forms;
using System.Threading;
using DigitalRune.Windows.Docking;
using NHibernate.Linq;
using BrightIdeasSoftware;
using IDPicker.DataModel;

namespace IDPicker.Forms
{

    public partial class SpectrumTableForm : DockableForm
    {
        public TreeListView TreeListView { get { return treeListView; } }

        public class SpectrumSourceGroupRow
        {
            public DataModel.SpectrumSourceGroup SpectrumSourceGroup { get; private set; }
            public long Spectra { get; private set; }
            public long DistinctPeptides { get; private set; }
            public long DistinctMatches { get; private set; }

            #region Constructor
            public SpectrumSourceGroupRow (object[] queryRow)
            {
                SpectrumSourceGroup = (queryRow[0] as DataModel.SpectrumSourceGroupLink).Group;
                Spectra = (long) queryRow[1];
                DistinctPeptides = (long) queryRow[2];
                DistinctMatches = (long) queryRow[3];
            }
            #endregion
        }

        public class SpectrumSourceRow
        {
            public DataModel.SpectrumSource SpectrumSource { get; private set; }
            public long Spectra { get; private set; }
            public long DistinctPeptides { get; private set; }
            public long DistinctMatches { get; private set; }

            #region Constructor
            public SpectrumSourceRow (object[] queryRow)
            {
                SpectrumSource = (DataModel.SpectrumSource) queryRow[0];
                Spectra = (long) queryRow[1];
                DistinctPeptides = (long) queryRow[2];
                DistinctMatches = (long) queryRow[3];
            }
            #endregion
        }

        public class SpectrumRow
        {
            public DataModel.Spectrum Spectrum { get; private set; }

            #region Constructor
            public SpectrumRow (object queryRow)
            {
                Spectrum = (DataModel.Spectrum) queryRow;
            }
            #endregion
        }

        public class PeptideSpectrumMatchRow
        {
            public DataModel.PeptideSpectrumMatch PeptideSpectrumMatch { get; private set; }
            
            #region Constructor
            public PeptideSpectrumMatchRow (object queryRow)
            {
                PeptideSpectrumMatch = (DataModel.PeptideSpectrumMatch) queryRow;
            }
            #endregion
        }

        public SpectrumTableForm ()
        {
            InitializeComponent();

            HideOnClose = true;

            Text = TabText = "Spectrum View";

            #region Column aspect getters
            sourceOrScanColumn.AspectGetter += delegate(object x)
            {
                if (x is SpectrumSourceGroupRow)
                    return Path.GetFileName((x as SpectrumSourceGroupRow).SpectrumSourceGroup.Name) + '/';
                else if (x is SpectrumSourceRow)
                    return (x as SpectrumSourceRow).SpectrumSource.Name;
                else if (x is SpectrumRow)
                    try { return pwiz.CLI.msdata.id.abbreviate((x as SpectrumRow).Spectrum.NativeID); }
                    catch { return (x as SpectrumRow).Spectrum.NativeID; }
                else if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.Rank;
                return null;
            };

            totalSpectraColumn.AspectGetter += delegate(object x)
            {
                return null;
            };

            confidentSpectraColumn.AspectGetter += delegate(object x)
            {
                if (x is SpectrumSourceGroupRow)
                    return (x as SpectrumSourceGroupRow).Spectra;
                else if (x is SpectrumSourceRow)
                    return (x as SpectrumSourceRow).Spectra;
                return null;
            };

            confidentPeptidesColumn.AspectGetter += delegate(object x)
            {
                if (x is SpectrumSourceGroupRow)
                    return (x as SpectrumSourceGroupRow).DistinctPeptides;
                else if (x is SpectrumSourceRow)
                    return (x as SpectrumSourceRow).DistinctPeptides;
                return null;
            };

            precursorMzColumn.AspectGetter += delegate(object x)
            {
                if (x is SpectrumRow)
                    return (x as SpectrumRow).Spectrum.PrecursorMZ;
                return null;
            };

            chargeColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.Charge;
                return null;
            };

            observedMassColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                {
                    var psm = (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch;
                    return psm.Spectrum.PrecursorMZ * psm.Charge - psm.Charge * pwiz.CLI.chemistry.Proton.Mass;
                }
                return null;
            };

            exactMassColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMass;
                return null;
            };

            massErrorColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMassError;
                return null;
            };

            qvalueColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                {
                    var psm = (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch;
                    return psm.Rank > 1 ? "n/a" : psm.QValue.ToString();
                }
                return null;
            };

            sequenceColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.ToModifiedString();
                return null;
            };

            #endregion

            treeListView.CanExpandGetter += delegate(object x) { return !(x is PeptideSpectrumMatchRow); };
            treeListView.ChildrenGetter += delegate(object x)
            {
                if (x is SpectrumSourceGroupRow)
                {
                    var parentGroup = (x as SpectrumSourceGroupRow).SpectrumSourceGroup;

                    var childGroups = from r in rowsByGroup
                                      where r.SpectrumSourceGroup.IsImmediateChildOf(parentGroup)
                                      select r as object;

                    var childSources = from r in rowsBySource
                                       where r.SpectrumSource.Group.Id == parentGroup.Id
                                       select r as object;

                    return childGroups.Concat(childSources);
                }
                else if (x is SpectrumSourceRow)
                {
                    string whereClause = dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch);
                    whereClause += (whereClause.Contains("WHERE") ? "AND" : "WHERE") + " psm.Spectrum.Source.id = ";
                    return session.CreateQuery("SELECT DISTINCT psm.Spectrum " +
                                               whereClause +
                                               (x as SpectrumSourceRow).SpectrumSource.Id.ToString())
                                  .List<DataModel.Spectrum>()
                                  .Select(o => new SpectrumRow(o));
                }
                else if (x is SpectrumRow)
                {
                    string whereClause = dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch);
                    whereClause += (whereClause.Contains("WHERE") ? "AND" : "WHERE") + " psm.Spectrum.id = ";
                    return session.CreateQuery("SELECT psm " +
                                               whereClause +
                                               (x as SpectrumRow).Spectrum.Id.ToString())
                                  .List<DataModel.PeptideSpectrumMatch>()
                                  .Select(o => new PeptideSpectrumMatchRow(o));
                }
                return null;
            };

            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);

            treeListView.AfterExpanding += new EventHandler<AfterExpandingEventArgs>(treeListView_AfterExpanding);
            treeListView.AfterCollapsing += new EventHandler<AfterCollapsingEventArgs>(treeListView_AfterCollapsing);
        }

        void treeListView_AfterExpanding (object sender, AfterExpandingEventArgs e)
        {

        }

        void treeListView_AfterCollapsing (object sender, AfterCollapsingEventArgs e)
        {
            treeListView_setColumnVisibility();
        }

        void treeListView_setColumnVisibility ()
        {
            object deepestExpandedItem = null;
            for (int i = 0; i < treeListView.Items.Count; ++i)
            {
                var item = treeListView.Items[i] as OLVListItem;

                if (treeListView.IsExpanded(item.RowObject))
                    deepestExpandedItem = item.RowObject;

                // break iteration once maximum depth is reached
                if (deepestExpandedItem is SpectrumRow)
                    break;
            }

            bool showAggregateColumns = deepestExpandedItem == null || deepestExpandedItem is SpectrumSourceGroupRow;
            bool showSpectrumColumns = deepestExpandedItem is SpectrumSourceRow;
            bool showPsmColumns = deepestExpandedItem is SpectrumRow;

            totalSpectraColumn.IsVisible = false;// showAggregateColumns;
            confidentPeptidesColumn.IsVisible = showAggregateColumns;
            confidentSpectraColumn.IsVisible = showAggregateColumns;
            precursorMzColumn.IsVisible = showSpectrumColumns;
            chargeColumn.IsVisible = showPsmColumns;
            observedMassColumn.IsVisible = showPsmColumns;
            exactMassColumn.IsVisible = showPsmColumns;
            massErrorColumn.IsVisible = showPsmColumns;
            qvalueColumn.IsVisible = showPsmColumns;
            sequenceColumn.IsVisible = showPsmColumns;

            treeListView.RebuildColumns();
        }

        void treeListView_CellClick (object sender, CellClickEventArgs e)
        {
            if (e.ClickCount < 2 || e.Item == null || e.Item.RowObject == null ||
                e.HitTest.HitTestLocation == HitTestLocation.ExpandButton)
                return;

            var newDataFilter = new DataFilter()
            {
                MaximumQValue = dataFilter.MaximumQValue,
                FilterSource = this
            };

            if (e.Item.RowObject is SpectrumSourceGroupRow)
                newDataFilter.SpectrumSourceGroup = (e.Item.RowObject as SpectrumSourceGroupRow).SpectrumSourceGroup;
            else if (e.Item.RowObject is SpectrumSourceRow)
                newDataFilter.SpectrumSource = (e.Item.RowObject as SpectrumSourceRow).SpectrumSource;
            else if (e.Item.RowObject is SpectrumRow)
                newDataFilter.Spectrum = (e.Item.RowObject as SpectrumRow).Spectrum;
            else if (e.Item.RowObject is PeptideSpectrumMatchRow)
            {
                if (SpectrumViewVisualize != null)
                    SpectrumViewVisualize(this, new SpectrumViewVisualizeEventArgs()
                                                {
                                                    PeptideSpectrumMatch = (e.Item.RowObject as PeptideSpectrumMatchRow).PeptideSpectrumMatch
                                                });
                return;
            }

            if (SpectrumViewFilter != null)
                SpectrumViewFilter(this, newDataFilter);
        }

        public event EventHandler<DataFilter> SpectrumViewFilter;
        public event EventHandler<SpectrumViewVisualizeEventArgs> SpectrumViewVisualize;

        private NHibernate.ISession session;
        private DataFilter dataFilter, basicDataFilter;
        private IList<SpectrumSourceGroupRow> rowsByGroup, basicRowsByGroup;
        private IList<SpectrumSourceRow> rowsBySource, basicRowsBySource;

        // TODO: support multiple selected objects
        List<string> oldSelectionPath = new List<string>();

        public void SetData (NHibernate.ISession session, DataFilter dataFilter)
        {
            this.session = session;
            this.dataFilter = new DataFilter(dataFilter) {Spectrum = null, SpectrumSource = null, SpectrumSourceGroup = null};

            if (treeListView.SelectedObject is SpectrumSourceGroupRow)
            {
                oldSelectionPath = getGroupTreePath((treeListView.SelectedObject as SpectrumSourceGroupRow).SpectrumSourceGroup);
            }
            else if (treeListView.SelectedObject is SpectrumSourceRow)
            {
                var source = (treeListView.SelectedObject as SpectrumSourceRow).SpectrumSource;
                oldSelectionPath = getGroupTreePath(source.Group);
                oldSelectionPath.Add(source.Name);
            }
            else if (treeListView.SelectedObject is SpectrumRow)
            {
                var spectrum = (treeListView.SelectedObject as SpectrumRow).Spectrum;
                oldSelectionPath = getGroupTreePath(spectrum.Source.Group);
                oldSelectionPath.Add(spectrum.Source.Name);
                oldSelectionPath.Add(treeListView.SelectedItem.Text);
            }

            ClearData();

            Text = TabText = "Loading spectrum view...";

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
            Text = TabText = "Spectrum View";

            treeListView.DiscardAllState();
            treeListView.Roots = null;
            treeListView.Refresh();
            Refresh();
        }
            

        void setData(object sender, DoWorkEventArgs e)
        {
            lock(session)
            try
            {
                var groupQuery = session.CreateQuery("SELECT ssgl, COUNT(DISTINCT psm.Spectrum.id), COUNT(DISTINCT psm.Peptide.id), COUNT(DISTINCT psm.FullDistinctKey) " +
                                                     this.dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                            DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink) +
                                                     "GROUP BY ssgl.Group.id");

                var sourceQuery = session.CreateQuery("SELECT psm.Spectrum.Source, COUNT(DISTINCT psm.Spectrum.id), COUNT(DISTINCT psm.Peptide.id), COUNT(DISTINCT psm.FullDistinctKey) " +
                                                      this.dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                      "GROUP BY psm.Spectrum.Source.id");

                groupQuery.SetReadOnly(true);
                sourceQuery.SetReadOnly(true);

                if (dataFilter.IsBasicFilter ||
                    dataFilter.SpectrumSourceGroup != null ||
                    dataFilter.SpectrumSource != null ||
                    dataFilter.Spectrum != null)
                {
                    if (basicDataFilter == null || (dataFilter.IsBasicFilter && dataFilter != basicDataFilter))
                    {
                        basicDataFilter = new DataFilter(this.dataFilter);
                        basicRowsByGroup = groupQuery.List<object[]>().Select(o => new SpectrumSourceGroupRow(o)).ToList();
                        basicRowsBySource = sourceQuery.List<object[]>().Select(o => new SpectrumSourceRow(o)).ToList();
                    }

                    rowsByGroup = basicRowsByGroup;
                    rowsBySource = basicRowsBySource;
                }
                else
                {
                    rowsByGroup = groupQuery.List<object[]>().Select(o => new SpectrumSourceGroupRow(o)).ToList();
                    rowsBySource = sourceQuery.List<object[]>().Select(o => new SpectrumSourceRow(o)).ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            SpectrumSourceGroup rootGroup;
            lock (session) rootGroup = session.Query<SpectrumSourceGroup>().Where(o => o.Name == "/").Single();
            var rootGroupRow = rowsByGroup.Where(o => o.SpectrumSourceGroup.Id == rootGroup.Id).Single();

            long totalSpectrumCount = rootGroupRow.Spectra;
            long totalPeptideCount = rootGroupRow.DistinctPeptides;

            // show total counts in the form title
            Text = TabText = String.Format("Spectrum View: {0} groups, {1} sources, {2} spectra, {3} peptides", rowsByGroup.Count, rowsBySource.Count, totalSpectrumCount, totalPeptideCount);

            treeListView.Roots = new object[] {rootGroupRow};

            // by default, expand all groups
            foreach (var row in rowsByGroup)
                treeListView.Expand(row);

            // if the view is filtered, expand all sources
            // TODO: this isn't a good idea when the filter has hundreds/thousands of spectra!
            /*if (!ReferenceEquals(rowsByGroup, basicRowsByGroup))
                foreach (var row in rowsBySource)
                    treeListView.Expand(row);*/

            // try to (re)set selected item
            expandSelectionPath(oldSelectionPath);

            treeListView_setColumnVisibility();

            treeListView.Refresh();
        }

        private List<string> getGroupTreePath (DataModel.SpectrumSourceGroup group)
        {
            var result = new List<string>();
            string groupPath = group.Name;
            while (!String.IsNullOrEmpty(Path.GetDirectoryName(groupPath)))
            {
                result.Add(Path.GetFileName(groupPath) + '/');
                groupPath = Path.GetDirectoryName(groupPath);
            }
            result.Add("/");
            result.Reverse();
            return result;
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

        private void editGroupsButton_Click(object sender, EventArgs e)
        {
            var gcf = new GroupingControlForm(session);

            if (gcf.ShowDialog() == DialogResult.OK)
            {
                basicDataFilter = null;
                (this.ParentForm as IDPickerForm).ReloadSession(session);
            }

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

        private void showInExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var table = getFormTable();

            TableExporter.ShowInExcel(table);
        }

    }

    public class SpectrumViewVisualizeEventArgs : EventArgs
    {
        public DataModel.PeptideSpectrumMatch PeptideSpectrumMatch { get; internal set; }
    }
}
