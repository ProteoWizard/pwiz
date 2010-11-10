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
using BrightIdeasSoftware;

namespace IDPicker.Forms
{
    public partial class AnalysisTableForm : DockableForm
    {
        public TreeListView TreeListView { get { return treeListView; } }

        public class AnalysisRow
        {
            public DataModel.Analysis Analysis { get; private set; }

            #region Constructor
            public AnalysisRow (object queryRow)
            {
                Analysis = (DataModel.Analysis) queryRow;
            }
            #endregion
        }

        public class AnalysisParameterRow
        {
            public DataModel.AnalysisParameter AnalysisParameter { get; private set; }

            #region Constructor
            public AnalysisParameterRow (object queryRow)
            {
                AnalysisParameter = (DataModel.AnalysisParameter) queryRow;
            }
            #endregion
        }

        public AnalysisTableForm ()
        {
            InitializeComponent();

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                e.Cancel = true;
                DockState = DockState.DockBottomAutoHide;
            };

            Text = TabText = "Analysis View";

            #region Column aspect getters
            nameColumn.AspectGetter += delegate(object x)
            {
                if (x is AnalysisRow)
                    return (x as AnalysisRow).Analysis.Id;
                else if (x is AnalysisParameterRow)
                    return (x as AnalysisParameterRow).AnalysisParameter.Name;
                return null;
            };

            softwareColumn.AspectGetter += delegate(object x)
            {
                if (x is AnalysisRow)
                    return (x as AnalysisRow).Analysis.Software.Name + " " + (x as AnalysisRow).Analysis.Software.Version;
                return null;
            };

            parameterCountColumn.AspectGetter += delegate(object x)
            {
                if (x is AnalysisRow)
                    return (x as AnalysisRow).Analysis.Parameters.Count;
                return null;
            };

            parameterValue.AspectGetter += delegate(object x)
            {
                if (x is AnalysisParameterRow)
                    return (x as AnalysisParameterRow).AnalysisParameter.Value;
                return null;
            };
            #endregion

            treeListView.CanExpandGetter += delegate(object x) { return x is AnalysisRow; };
            treeListView.ChildrenGetter += delegate(object x)
            {
                return (x as AnalysisRow).Analysis.Parameters.Select(o => new AnalysisParameterRow(o));
            };

            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);
        }

        void treeListView_CellClick (object sender, CellClickEventArgs e)
        {
            if (e.ClickCount < 2 || e.Item == null || e.Item.RowObject == null)
                return;

            var newDataFilter = new DataFilter()
            {
                MaximumQValue = dataFilter.MaximumQValue,
                FilterSource = this
            };

            if (e.Item.RowObject is AnalysisRow)
                newDataFilter.Analysis = (e.Item.RowObject as AnalysisRow).Analysis;

            //if (PeptideViewFilter != null)
            //    PeptideViewFilter(this, newDataFilter);
        }

        //public event PeptideViewFilterEventHandler PeptideViewFilter;

        private NHibernate.ISession session;
        private DataFilter dataFilter, basicDataFilter;

        private IEnumerable<AnalysisRow> rowsByAnalysis, basicRowsByAnalysis;

        // TODO: support multiple selected objects
        string[] oldSelectionPath = new string[] { };

        public void SetData (NHibernate.ISession session, DataFilter dataFilter)
        {
            this.session = session;
            this.dataFilter = new DataFilter(dataFilter) { Peptide = null };

            /*if (treeListView.SelectedObject is PeptideRow)
                oldSelectionPath = new string[] { treeListView.SelectedItem.Text };
            else if (treeListView.SelectedObject is PeptideSpectrumMatchRow)
                oldSelectionPath = new string[] { (treeListView.SelectedObject as PeptideSpectrumMatchRow).PeptideSpectrumMatch.Peptide.Sequence, treeListView.SelectedItem.Text };*/

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

        void setData (object sender, DoWorkEventArgs e)
        {
            lock (session)
            try
            {
                var analysisQuery = session.CreateQuery("SELECT psm.Analysis " +
                                                        dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                        "GROUP BY psm.Analysis.id");

                analysisQuery.SetReadOnly(true);

                if (dataFilter.IsBasicFilter || dataFilter.Analysis != null)
                {
                    if (basicDataFilter == null || (dataFilter.IsBasicFilter && dataFilter != basicDataFilter))
                    {
                        basicDataFilter = new DataFilter(this.dataFilter);
                        basicRowsByAnalysis = analysisQuery.List<object>().Select(o => new AnalysisRow(o));
                    }

                    rowsByAnalysis = basicRowsByAnalysis;
                }
                else
                    rowsByAnalysis = analysisQuery.List<object>().Select(o => new AnalysisRow(o)).ToList();
            }
            catch (Exception)
            {
                throw;
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            // show total counts in the form title
            Text = TabText = String.Format("Analysis View: {0} distinct analyses", rowsByAnalysis.Count());

            treeListView.Roots = rowsByAnalysis;

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

        private void clipboardToolStripMenuItem_Click (object sender, EventArgs e)
        {
            var table = getFormTable();

            TableExporter.CopyToClipboard(table);
        }

        private void fileToolStripMenuItem_Click (object sender, EventArgs e)
        {
            var table = getFormTable();

            TableExporter.ExportToFile(table);
        }

        /*private void exportButton_Click (object sender, EventArgs e)
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
        }*/

        private List<List<string>> getFormTable ()
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

        private void showInExcelToolStripMenuItem_Click (object sender, EventArgs e)
        {
            var table = getFormTable();

            TableExporter.ShowInExcel(table);
        }
    }

    //public delegate void PeptideViewFilterEventHandler (PeptideTableForm sender, DataFilter peptideViewFilter);
}
