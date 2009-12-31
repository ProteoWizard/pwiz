using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
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
            TurnoverForm.Instance.EnsureDataDirectory(Workspace);
            var msDataFiles = new List<MsDataFile>();
            for (int i = 0; i < MsDataFiles.Count; i++)
            {
                if (!checkedListBox1.GetItemChecked(i))
                {
                    continue;
                }
                var msDataFile = MsDataFiles[i];
                if (MsDataFileUtil.InitMsDataFile(Workspace, msDataFile))
                {
                    msDataFiles.Add(msDataFile);
                }
            }
            if (msDataFiles.Count > 0)
            {
                var job = new Task(PeptideAnalysis, msDataFiles);
                new LongOperationBroker(job, new LongWaitDialog(this, "Creating file analyses")).LaunchJob();
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
                            session, msDataFile, peptideAnalysis, searchResult);
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
