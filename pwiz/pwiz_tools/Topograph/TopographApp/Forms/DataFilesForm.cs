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
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using JetBrains.Annotations;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.RowSources;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;
using pwiz.Topograph.ui.DataBinding;
using pwiz.Topograph.ui.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class DataFilesForm : WorkspaceForm
    {
        public DataFilesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            bindingSource1.SetViewContext(new TopographViewContext(workspace, typeof (DataFileRow), new DataFileRow[0])
            {
                DeleteHandler = new DataFileDeleteHandler(this),
            });
        }

        private void Requery()
        {
            bindingSource1.RowSource =
                Workspace.MsDataFiles.Select(msDataFile => new DataFileRow(this, msDataFile));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Requery();
        }

        public static double? ToDouble(object value)
        {
            if (value == null)
            {
                return null;
            }
            try
            {
                return (double) Convert.ChangeType(value, typeof (double));
            }
            catch (Exception e)
            {
                Console.Out.WriteLine(e);
                return null;
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
                var connection = session.Connection;
                try
                {
                    Workspace.LockTables(connection);
                    using (var transaction = connection.BeginTransaction())
                    using (var cmd = connection.CreateCommand())
                    {
                        broker.UpdateStatusMessage("Deleting chromatograms");
                        cmd.CommandText = "DELETE FROM DbChromatogram WHERE ChromatogramSet IN " +
                                          sqlChromatogramSetIds;
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "DELETE FROM DbChromatogramSet WHERE PeptideFileAnalysis IN " +
                                          sqlFileAnalysisIds;
                        cmd.ExecuteNonQuery();
                        broker.UpdateStatusMessage("Deleting peaks");
                        cmd.CommandText = ("DELETE FROM DbPeak WHERE PeptideFileAnalysis IN " + sqlFileAnalysisIds);
                        cmd.ExecuteNonQuery();
                        broker.UpdateStatusMessage("Deleting file analyses");
                        cmd.CommandText = ("DELETE FROM DbPeptideFileAnalysis WHERE MsDataFile IN " + sqlDataFileIds);
                        cmd.ExecuteNonQuery();
                        broker.UpdateStatusMessage("Deleting search results");
                        cmd.CommandText = ("DELETE FROM DbPeptideSpectrumMatch WHERE MsDataFile IN " +
                                           sqlDataFileIds);
                        cmd.ExecuteNonQuery();
                        broker.UpdateStatusMessage("Deleting data files");
                        cmd.CommandText = ("DELETE FROM DbMsDataFile WHERE Id IN " + sqlDataFileIds);
                        broker.UpdateStatusMessage("Updating parent tables");
                        cmd.CommandText =
                            "UPDATE DbPeptideAnalysis SET FileAnalysisCount = (SELECT COUNT(Id) FROM DbPeptideFileAnalysis WHERE PeptideAnalysis = DbPeptideAnalysis.Id)";
                        cmd.ExecuteNonQuery();
                        //                session.CreateSQLQuery("UPDATE DbPeptide SET SearchResultCount = (SELECT Count(Id) FROM DbPeptideSearchResult WHERE Peptide = DbPeptide.Id)")
                        //                    .ExecuteUpdate();
                        broker.SetIsCancelleable(false);
                        broker.UpdateStatusMessage("Committing transaction");
                        transaction.Commit();
                    }
                }
                finally
                {
                    Workspace.UnlockTables(connection);
                }
            }
        }

        class DataFileRow : PropertyChangedSupport
        {
            private readonly DataFilesForm _form;
            private MsDataFile _dataFile;
            public DataFileRow(DataFilesForm form, MsDataFile dataFile)
            {
                _form = form;
                _dataFile = dataFile;
            }

            protected override IEnumerable<INotifyPropertyChanged> GetProperyChangersToPropagate()
            {
                return new[] {_dataFile};
            }

            public MsDataFile MsDataFile { get { return _dataFile; } }

            [UsedImplicitly]
            public LinkValue<string> Name { get { return new LinkValue<string>(_dataFile.Name, NameClickHandler); } }
            [UsedImplicitly]
            public string Label { get { return _dataFile.Label; } set { _dataFile.Label = value; } }
            [UsedImplicitly]
            public double? TimePoint { get { return _dataFile.TimePoint; } set { _dataFile.TimePoint = value; } }
            [UsedImplicitly]
            public string Cohort { get { return _dataFile.Cohort; } set { _dataFile.Cohort = value; } }
            [UsedImplicitly]
            public string Sample { get { return _dataFile.Sample; } set { _dataFile.Sample = value; } }
            [UsedImplicitly]
            public PrecursorPoolValue? PrecursorPool { get { return _dataFile.PrecursorPool; } set { _dataFile.PrecursorPool = value; } }
            private void NameClickHandler(object sender, EventArgs args)
            {
                DataFileSummary dataFileSummaryForm = null;
                foreach (var form in Application.OpenForms)
                {
                    if (form is DataFileSummary && _dataFile.Equals(((DataFileSummary)form).MsDataFile))
                    {
                        dataFileSummaryForm = (DataFileSummary)form;
                        break;
                    }
                }

                if (dataFileSummaryForm == null)
                {
                    dataFileSummaryForm = new DataFileSummary(_dataFile);
                    dataFileSummaryForm.Show(_form.DockPanel, DockState.Document);
                }
                else
                {
                    dataFileSummaryForm.Activate();
                }
                
            }
        }

        class DataFileDeleteHandler : DeleteHandler
        {
            private DataFilesForm _form;
            public DataFileDeleteHandler(DataFilesForm form)
            {
                _form = form;
            }
            public override void Delete()
            {
                IList<MsDataFile> dataFiles = GetSelectedRows<DataFileRow>(_form.gridView).Select(row=>row.MsDataFile).ToArray();
                if (dataFiles.Count == 0)
                {
                    return;
                }

                string message;
                if (dataFiles.Count == 1)
                {
                    message = "Are you sure you want to remove this data file from the workspace?";
                }
                else
                {
                    message = string.Format(
                        "Are you sure you want to remove these {0}  data files from the workspace?", dataFiles.Count);
                }
                message += "\nAll search results and analyses of these files will also be deleted.";
                if (MessageBox.Show(_form, message, Program.AppName, MessageBoxButtons.OKCancel) != DialogResult.OK)
                {
                    return;
                }
                using (var longWaitDialog = new LongWaitDialog(_form, "Deleting data files"))
                {
                    var dataFileIds = dataFiles.Select(dataFile => dataFile.Id).ToArray();
                    var longWaitBroker = new LongOperationBroker((b => _form.DeleteDataFiles(b, dataFileIds)), longWaitDialog);
                    longWaitBroker.LaunchJob();
                }
                _form.Workspace.DatabasePoller.LoadAndMergeChanges(null);
            }
        }
    }
}
