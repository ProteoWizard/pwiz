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
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;
using NHibernate.Criterion;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
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
            foreach (var peptideFileAnalysis in PeptideAnalysis.FileAnalyses)
            {
                existing.Add(peptideFileAnalysis.MsDataFile);
            }

            var selected = new HashSet<long>();
            var dataFiles = new List<MsDataFile>();
            using (var session = Workspace.OpenSession())
            {
                var spectrumMatches = session.CreateCriteria<DbPeptideSpectrumMatch>()
                    .Add(Restrictions.Eq("Peptide", session.Load<DbPeptide>(PeptideAnalysis.Peptide.Id)))
                    .List<DbPeptideSpectrumMatch>();
                selected.UnionWith(spectrumMatches.Select(psm => psm.MsDataFile.Id.GetValueOrDefault()));
            }
            foreach (var dataFile in Workspace.MsDataFiles)
            {
                if (existing.Contains(dataFile))
                {
                    continue;
                }
                dataFiles.Add(dataFile);
            }
            dataFiles.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.CurrentCultureIgnoreCase));
            checkedListBox1.Items.Clear();
            foreach (var dataFile in dataFiles)
            {
                int index = checkedListBox1.Items.Count;
                checkedListBox1.Items.Add(dataFile.Label);
                checkedListBox1.SetItemChecked(index, selected.Contains(dataFile.Id));
            }
            MsDataFiles = new ReadOnlyCollection<MsDataFile>(dataFiles.ToArray());
        }

        public PeptideAnalysis PeptideAnalysis { get { return (PeptideAnalysis) EntityModel; } }
        public IList<MsDataFile> MsDataFiles { get; private set; }

        private void BtnOkOnClick(object sender, EventArgs e)
        {
            int unreadableFiles = 0;
            int checkedItems = 0;
            TopographForm.Instance.EnsureDataDirectory(Workspace);
            var msDataFiles = new List<MsDataFile>();
            for (int i = 0; i < MsDataFiles.Count; i++)
            {
                if (!checkedListBox1.GetItemChecked(i))
                {
                    continue;
                }
                checkedItems++;
                var msDataFile = MsDataFiles[i];
                string errorMessage;
                if (TopographForm.TryInitMsDataFile(this, msDataFile, out errorMessage)) 
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
                    new LongOperationBroker(job.Run, longWaitDialog).LaunchJob();
                }
                Workspace.DatabasePoller.Wake();
            }
            Close();
        }

        private void BtnCancelOnClick(object sender, EventArgs e)
        {
            Close();
        }

        private class Task
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
                using (var session = PeptideAnalysis.Workspace.OpenSession())
                {
                    var peptideAnalysis = session.Get<DbPeptideAnalysis>(PeptideAnalysis.Id);
                    var peptide = peptideAnalysis.Peptide;
                    var psmTimesByDataFileId = peptide.PsmTimesByDataFileId(session);
                    session.BeginTransaction();
                    foreach (var msDataFile in MsDataFiles)
                    {
                        var dbPeptideFileAnalysis = PeptideFileAnalysis.CreatePeptideFileAnalysis(
                            session, msDataFile, peptideAnalysis, psmTimesByDataFileId);
                        session.Save(dbPeptideFileAnalysis);
                        peptideAnalysis.FileAnalysisCount++;
                    }
                    session.Update(peptideAnalysis);
                    session.Save(new DbChangeLog(PeptideAnalysis));
                    session.Transaction.Commit();
                }
                PeptideAnalysis.Workspace.DatabasePoller.MergeChangesNow();
            }
        }

        private void BtnSelectAllOnClick(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                checkedListBox1.SetItemChecked(i, true);
            }
        }

        private void BtnDeselectAllOnClick(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                checkedListBox1.SetItemChecked(i, false);
            }
        }

    }
}
