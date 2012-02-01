using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class CreateFileAnalysesForm : EntityModelForm
    {
        public CreateFileAnalysesForm(PeptideAnalysis peptideAnalysis) : base(peptideAnalysis)
        {
            InitializeComponent();
            UpdateListbox();
        }

        public void UpdateListbox()
        {
            var existing = new HashSet<MsDataFile>();
            foreach (var peptideFileAnalysis in PeptideAnalysis.FileAnalyses.ListChildren())
            {
                existing.Add(peptideFileAnalysis.MsDataFile);
            }

            var selected = new HashSet<long>();
            var dataFiles = new List<MsDataFile>();
            using (var session = Workspace.OpenSession())
            {
                var peptide = session.Get<DbPeptide>(PeptideAnalysis.Peptide.Id);
                foreach (var searchResult in peptide.SearchResults)
                {
                    selected.Add(searchResult.MsDataFile.Id.Value);
                }
            }
            foreach (var dataFile in Workspace.MsDataFiles.ListChildren())
            {
                if (existing.Contains(dataFile))
                {
                    continue;
                }
                dataFiles.Add(dataFile);
            }
            dataFiles.Sort((a, b) => a.Label.CompareTo(b.Label));
            checkedListBox1.Items.Clear();
            foreach (var dataFile in dataFiles)
            {
                int index = checkedListBox1.Items.Count;
                checkedListBox1.Items.Add(dataFile.Label);
                checkedListBox1.SetItemChecked(index, selected.Contains(dataFile.Id.Value));
            }
            MsDataFiles = new ReadOnlyCollection<MsDataFile>(dataFiles.ToArray());
        }

        public PeptideAnalysis PeptideAnalysis { get { return (PeptideAnalysis) EntityModel; } }
        public IList<MsDataFile> MsDataFiles { get; private set; }

        private void btnOK_Click(object sender, EventArgs e)
        {
            int unreadableFiles = 0;
            int checkedItems = 0;
            TurnoverForm.Instance.EnsureDataDirectory(Workspace);
            var msDataFiles = new List<MsDataFile>();
            for (int i = 0; i < MsDataFiles.Count; i++)
            {
                if (!checkedListBox1.GetItemChecked(i))
                {
                    continue;
                }
                checkedItems++;
                var msDataFile = MsDataFiles[i];
                if (MsDataFileUtil.InitMsDataFile(Workspace, msDataFile))
                {
                    msDataFiles.Add(msDataFile);
                }
                else
                {
                    unreadableFiles++;
                }
            }
            if (checkedItems == 0)
            {
                return;
            }
            if (unreadableFiles > 0)
            {
                if (msDataFiles.Count == 0)
                {
                    var dlgResult = MessageBox.Show(this,
                                                    unreadableFiles +
                                                    " of the data files could not be read.  Do you want to process the remaining files?",
                                                    Program.AppName, MessageBoxButtons.YesNoCancel);
                    if (dlgResult == DialogResult.Cancel)
                    {
                        return;
                    }
                    if (dlgResult == DialogResult.No)
                    {
                        Close();
                        return;
                    }
                }
                else
                {
                    var dlgResult = MessageBox.Show("None of the selected files could be read.  Make sure the data directory setting is correct.", Program.AppName, MessageBoxButtons.OKCancel);
                    if (dlgResult == DialogResult.OK)
                    {
                        Close();
                    }
                    return;
                }
            }
            if (msDataFiles.Count > 0)
            {
                var job = new Task(PeptideAnalysis, msDataFiles);
                using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Creating file analyses"))
                {
                    new LongOperationBroker(job, longWaitDialog).LaunchJob();
                }
                Workspace.Reconciler.Wake();
            }
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private class Task : ILongOperationJob
        {
            public Task(PeptideAnalysis peptideAnalysis, ICollection<MsDataFile> msDataFiles)
            {
                PeptideAnalysis = peptideAnalysis;
                MsDataFiles = msDataFiles;
            }

            public PeptideAnalysis PeptideAnalysis { get; private set; }
            public ICollection<MsDataFile> MsDataFiles { get; private set; }
            public void Run(LongOperationBroker longOperationBroker)
            {
                var searchResults = new Dictionary<long, DbPeptideSearchResult>();
                using (var session = PeptideAnalysis.Workspace.OpenSession())
                {
                    var peptideAnalysis = session.Get<DbPeptideAnalysis>(PeptideAnalysis.Id);
                    var peptide = peptideAnalysis.Peptide;
                    foreach (var searchResult in peptide.SearchResults)
                    {
                        searchResults[searchResult.MsDataFile.Id.Value] = searchResult;
                    }
                    session.BeginTransaction();
                    foreach (var msDataFile in MsDataFiles)
                    {
                        DbPeptideSearchResult searchResult;
                        searchResults.TryGetValue(msDataFile.Id.Value, out searchResult);
                        var dbPeptideFileAnalysis = PeptideFileAnalysis.CreatePeptideFileAnalysis(
                            session, msDataFile, peptideAnalysis, searchResult, true);
                        session.Save(dbPeptideFileAnalysis);
                        peptideAnalysis.FileAnalysisCount++;
                    }
                    session.Update(peptideAnalysis);
                    session.Save(new DbChangeLog(PeptideAnalysis));
                    session.Transaction.Commit();
                }
                PeptideAnalysis.Workspace.Reconciler.ReconcileNow();
            }

            public bool Cancel()
            {
                return true;
            }
        }

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                checkedListBox1.SetItemChecked(i, true);
            }
        }

        private void btnDeselectAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                checkedListBox1.SetItemChecked(i, false);
            }
        }

    }
}
