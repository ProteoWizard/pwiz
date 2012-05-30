/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class DataFilesForm : WorkspaceForm
    {
        private readonly Dictionary<MsDataFile, DataGridViewRow> _dataFileRows 
            = new Dictionary<MsDataFile, DataGridViewRow>();

        public DataFilesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            gridView.DataError += gridView_DataError;
        }

        private void Requery()
        {
            gridView.Rows.Clear();
            _dataFileRows.Clear();
            foreach (var row in AddRows(Workspace.MsDataFiles.ListChildren()))
            {
                UpdateRow(row);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Requery();
        }

        void gridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            Console.Out.WriteLine(e.Exception);
        }

        private IList<DataGridViewRow> AddRows(ICollection<MsDataFile> dataFiles)
        {
            List<DataGridViewRow> result = new List<DataGridViewRow>();
            foreach (var dataFile in dataFiles)
            {
                var row = new DataGridViewRow();
                row.Tag = dataFile;
                _dataFileRows.Add(dataFile, row);
                result.Add(row);
            }
            gridView.Rows.AddRange(result.ToArray());
            return result;
        }

        private DataGridViewRow AddRow(MsDataFile dataFile)
        {
            return AddRows(new[] {dataFile})[0];
        }

        private void UpdateRow(DataGridViewRow row)
        {
            var dataFile = (MsDataFile) row.Tag;
            row.Cells[colName.Index].Value = dataFile.Name;
            if (Workspace.IsRejected(dataFile))
            {
                row.Cells[colName.Index].Style.BackColor = Color.Red;
            }
            else
            {
                row.Cells[colName.Index].Style.BackColor = Color.Empty;
            }
            row.Cells[colLabel.Index].Value = dataFile.Label;
            row.Cells[colCohort.Index].Value = dataFile.Cohort;
            row.Cells[colTimePoint.Index].Value = dataFile.TimePoint;
            row.Cells[colSample.Index].Value = dataFile.Sample;
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            base.OnWorkspaceEntitiesChanged(args);
            foreach (var dataFile in args.GetEntities<MsDataFile>())
            {
                DataGridViewRow row;
                _dataFileRows.TryGetValue(dataFile, out row);
                if (args.IsRemoved(dataFile))
                {
                    if (row != null)
                    {
                        gridView.Rows.Remove(row);
                    }
                }
                else
                {
                    if (row == null)
                    {
                        row = AddRow(dataFile);
                    }
                    UpdateRow(row);
                }
            }
        }

        public static double? ToDouble(object value)
        {
            if (value == null)
            {
                return null;
            }
            try
            {
                return (double)Convert.ChangeType(value, typeof(double));
            }
            catch (Exception e)
            {
                Console.Out.WriteLine(e);
                return null;
            }
        }

        private void gridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var row = gridView.Rows[e.RowIndex];
            var msDataFile = (MsDataFile) row.Tag;
            var column = gridView.Columns[e.ColumnIndex];
            var cell = row.Cells[e.ColumnIndex];
            if (column == colCohort)
            {
                msDataFile.Cohort = Convert.ToString(cell.Value);
            }
            else if (column == colTimePoint)
            {
                msDataFile.TimePoint = ToDouble(cell.Value);
            }
            else if (column == colLabel)
            {
                msDataFile.Label = Convert.ToString(cell.Value);
            }
            else if (column == colSample)
            {
                msDataFile.Sample = Convert.ToString(cell.Value);
            }
        }

        private void gridView_RowHeaderMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
        }

        private void gridView_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            BeginInvoke(new Action(AfterRowsRemoved));
        }

        private bool inRowsRemoved;
        private void AfterRowsRemoved() {
            if (inRowsRemoved)
            {
                return;
            }
            try
            {
                inRowsRemoved = true;
                var deletedRows = new HashSet<DataGridViewRow>(_dataFileRows.Values);
                for (int iRow = 0; iRow < gridView.Rows.Count; iRow++)
                {
                    deletedRows.Remove(gridView.Rows[iRow]);
                }
                if (deletedRows.Count == 0)
                {
                    return;
                }
                string message = deletedRows.Count == 1
                                     ?
                                         "Are you sure you want to remove this data file from the workspace?  All search results and analyses of this file will be deleted."
                                     : "Are you sure you want to remove these " + deletedRows.Count +
                                       " data files from the workspace?  All search results and analyses of these files will also be deleted.";
                bool cancelled = MessageBox.Show(this, message, Program.AppName, MessageBoxButtons.OKCancel) ==
                                 DialogResult.Cancel;
                if (!cancelled)
                {
                    var dataFileIds = new List<long>();
                    foreach (var row in deletedRows)
                    {
                        dataFileIds.Add(((MsDataFile) row.Tag).Id.Value);
                    }
                    using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Deleting data files"))
                    {
                        var longWaitBroker = new LongOperationBroker((b => DeleteDataFiles(b, dataFileIds)),
                                                                     longWaitDialog);
                        longWaitBroker.LaunchJob();
                        cancelled = longWaitBroker.WasCancelled;
                    }
                }

                if (cancelled)
                {
                    gridView.Rows.AddRange(deletedRows.ToArray());
                }
                else
                {
                    foreach (var row in deletedRows)
                    {
                        _dataFileRows.Remove((MsDataFile) row.Tag);
                    }
                }
            }
            finally
            {
                inRowsRemoved = false;
            }
        }

        private void DeleteDataFiles(LongOperationBroker broker, ICollection<long> dataFileIds)
        {
            var sqlDataFileIds = new StringBuilder("(");
            var strComma = "";
            foreach (var id in dataFileIds)
            {
                sqlDataFileIds.Append(strComma);
                strComma = ",";
                sqlDataFileIds.Append(id);
            }
            sqlDataFileIds.Append(")");
            var sqlFileAnalysisIds = "(SELECT Id FROM DbPeptideFileAnalysis WHERE MsDataFile IN " + sqlDataFileIds + ")";
            var sqlChromatogramSetIds = "(SELECT ChromatogramSet FROM DbPeptideFileAnalysis WHERE MsDataFile IN " +
                                        sqlFileAnalysisIds + ")";
            using (var session = Workspace.OpenSession())
            {
                session.BeginTransaction();
                broker.UpdateStatusMessage("Deleting chromatograms");
                session.CreateSQLQuery("DELETE FROM DbChromatogram WHERE ChromatogramSet IN " + sqlChromatogramSetIds)
                    .ExecuteUpdate();
                session.CreateQuery("DELETE FROM DbChromatogramSet WHERE PeptideFileAnalysis IN " + sqlFileAnalysisIds)
                    .ExecuteUpdate();
                broker.UpdateStatusMessage("Deleting peaks");
                session.CreateSQLQuery("DELETE FROM DbPeak WHERE PeptideFileAnalysis IN " + sqlFileAnalysisIds)
                    .ExecuteUpdate();
                broker.UpdateStatusMessage("Deleting file analyses");
                session.CreateSQLQuery("DELETE FROM DbPeptideFileAnalysis WHERE MsDataFile IN " + sqlDataFileIds)
                    .ExecuteUpdate();
                broker.UpdateStatusMessage("Deleting search results");
                session.CreateSQLQuery("DELETE FROM DbPeptideSearchResult WHERE MsDataFile IN " + sqlDataFileIds)
                    .ExecuteUpdate();
                broker.UpdateStatusMessage("Deleting data files");
                session.CreateSQLQuery("DELETE FROM DbMsDataFile WHERE Id IN " + sqlDataFileIds)
                    .ExecuteUpdate();
                broker.UpdateStatusMessage("Updating parent tables");
                session.CreateSQLQuery(
                    "UPDATE DbPeptideAnalysis SET FileAnalysisCount = (SELECT COUNT(Id) FROM DbPeptideFileAnalysis WHERE PeptideAnalysis = DbPeptideAnalysis.Id)")
                    .ExecuteUpdate();
                session.CreateSQLQuery("UPDATE DbPeptide SET SearchResultCount = (SELECT Count(Id) FROM DbPeptideSearchResult WHERE Peptide = DbPeptide.Id)")
                    .ExecuteUpdate();
                session.CreateSQLQuery(
                    "Update DbWorkspace SET MsDataFileCount = (SELECT Count(Id) FROM DbMsDataFile WHERE Workspace = DbWorkspace.Id)")
                    .ExecuteUpdate();
                broker.SetIsCancelleable(false);
                broker.UpdateStatusMessage("Committing transaction");
                session.Transaction.Commit();
            }
            
        }

        private void gridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            if (e.ColumnIndex == colName.Index)
            {
                var dataFile = (MsDataFile)gridView.Rows[e.RowIndex].Tag;
                DataFileSummary dataFileSummaryForm = null;
                foreach (var form in Application.OpenForms)
                {
                    if (form is DataFileSummary && dataFile.Equals(((DataFileSummary)form).MsDataFile))
                    {
                        dataFileSummaryForm = (DataFileSummary)form;
                        break;
                    }
                }

                if (dataFileSummaryForm == null)
                {
                    dataFileSummaryForm = new DataFileSummary(dataFile);
                    dataFileSummaryForm.Show(DockPanel, DockState.Document);
                }
                else
                {
                    dataFileSummaryForm.Activate();
                }
            }
        }

        private void gridView_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            var s1 = e.CellValue1 as string;
            var s2 = e.CellValue2 as string;
            if (s1 != null && s2 != null)
            {
                e.SortResult = NameComparers.CompareReplicateNames(s1, s2);
                e.Handled = true;
            }
        }
    }
}
