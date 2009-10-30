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
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Fasta;
using pwiz.Topograph.MsData;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Properties;

namespace pwiz.Topograph.ui.Forms
{
    public partial class TurnoverForm : Form
    {
        public const string WorkspaceFilter = "Topograph Workspaces(*.tpg)|*.tpg"
                                               + "|All Files (*.*)|*.*";

        private Workspace _workspace;

        public TurnoverForm()
        {
            InitializeComponent();
            Instance = this;
        }

        private ToolStripMenuItem[] WorkspaceMenuItems
        {
            get
            {
                return new[]
                {
                        modificationsToolStripMenuItem,
                        addSearchResultsToolStripMenuItem,
                        closeWorkspaceToolStripMenuItem,
                        enrichmentToolStripMenuItem,
                        peptidesToolStripMenuItem,
                        peptideAnalysesToolStripMenuItem,
                        queryToolStripMenuItem,
                        dataFilesToolStripMenuItem,
                        saveWorkspaceToolStripMenuItem,
                        statusToolStripMenuItem,
                        updateProteinNamesToolStripMenuItem,
                        machineSettingsToolStripMenuItem,
                        mercuryToolStripMenuItem,
                };
            }
        }

        public static TurnoverForm Instance { get; private set; }

        public Workspace Workspace
        {
            get
            {
                return _workspace;
            }
            set
            {
                var openWorkspaceForms = new List<WorkspaceForm>();
                foreach (var form in Application.OpenForms)
                {
                    if (form is WorkspaceForm)
                    {
                        openWorkspaceForms.Add((WorkspaceForm) form);
                    }
                }
                foreach (var workspaceForm in openWorkspaceForms)
                {
                    if (!workspaceForm.IsDisposed)
                    {
                        workspaceForm.Close();
                    }
                }
                if (_workspace != null)
                {
                    _workspace.SetActionInvoker(null);
                    _workspace.EntitiesChange -= Workspace_EntitiesChange;
                    _workspace.WorkspaceDirty -= Workspace_WorkspaceDirty;
                }
                _workspace = value;
                if (_workspace != null)
                {
                    _workspace.SetActionInvoker(ActionInvoker);
                    int count;
                    using (var session = _workspace.OpenSession())
                    {
                        var query = session.CreateQuery("SELECT COUNT(*) FROM " + typeof (DbPeptideAnalysis));
                        count = Convert.ToInt32(query.UniqueResult());
                    }
                    if (count == 0)
                    {
                        new PeptidesForm(_workspace).Show(dockPanel, DockState.Document);
                    }
                    else
                    {
                        new PeptideAnalysesForm(_workspace).Show(dockPanel, DockState.Document);
                    }
                    foreach (var menuItem in WorkspaceMenuItems)
                    {
                        menuItem.Enabled = true;
                    }
                    _workspace.EntitiesChange += Workspace_EntitiesChange;
                    _workspace.WorkspaceDirty += Workspace_WorkspaceDirty;
                }
                else
                {
                    foreach (var menuItem in WorkspaceMenuItems)
                    {
                        menuItem.Enabled = false;
                    }
                }
                UpdateWindowTitle();
            }
        }

        void Workspace_WorkspaceDirty(Workspace workspace)
        {
            UpdateWindowTitle();
        }

        void UpdateWindowTitle()
        {
            if (Workspace == null)
            {
                Text = Program.AppName;
            }
            else
            {
                Text = Path.GetFileName(_workspace.DatabasePath) + 
                    (_workspace.IsDirty ? "(changed)" : "") +
                    " - " + Program.AppName;
            }
        }

        void Workspace_EntitiesChange(EntitiesChangedEventArgs entitiesChangedEventArgs)
        {
            UpdateWindowTitle();
        }

        private void newWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!PromptToSaveWorkspace())
            {
                return;
            }
            var fileDialog = new SaveFileDialog()
                                 {
                                     Filter = WorkspaceFilter,
                                     InitialDirectory = Settings.Default.WorkspaceDirectory
                                 };
            
            if (fileDialog.ShowDialog(this) == DialogResult.Cancel)
            {
                return;
            }
            String filename = fileDialog.FileName;
            Settings.Default.WorkspaceDirectory = Path.GetDirectoryName(filename);
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }
            using(ISessionFactory sessionFactory = SessionFactoryFactory.CreateSessionFactory(filename, true))
            {
                ISession session = sessionFactory.OpenSession();
                var transaction = session.BeginTransaction();
                var dbWorkspace = new DbWorkspace
                                      {
                                          ModificationCount = 1,
                                          TracerDefCount = 1,
                                      };
                session.Save(dbWorkspace);
                DbTracerDef dbTracerDef = TracerDef.GetD3LeuEnrichment();
                dbTracerDef.Workspace = dbWorkspace;
                dbTracerDef.Name = "Tracer";
                session.Save(dbTracerDef);

                var modification = new DbModification
                                       {
                                           DeltaMass = 57.02,
                                           Symbol = "C",
                                           Workspace = dbWorkspace
                                       };
                session.Save(modification);
                transaction.Commit();
            }
            Workspace = new Workspace(filename);
            enrichmentToolStripMenuItem_Click(sender, e);
        }

        private void openWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!PromptToSaveWorkspace())
            {
                return;
            }
            var fileDialog = new OpenFileDialog
                                 {
                                     Filter = WorkspaceFilter,
                                     InitialDirectory = Settings.Default.WorkspaceDirectory
                                 };
            if (fileDialog.ShowDialog(this) == DialogResult.Cancel)
            {
                return;
            }
            String filename = fileDialog.FileName;
            Settings.Default.WorkspaceDirectory = Path.GetDirectoryName(filename);
            Workspace = new Workspace(filename);
        }

        private void addSearchResultsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AddSearchResultsForm(Workspace).ShowDialog();
        }

        private void enrichmentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialog = new EnrichmentDialog(Workspace);
            dialog.Show(this);
        }

        private void peptidesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var peptidesForm = Program.FindOpenForm<PeptidesForm>();
            if (peptidesForm != null)
            {
                peptidesForm.Activate();
                return;
            }

            peptidesForm = new PeptidesForm(Workspace);
            peptidesForm.Show(dockPanel, DockState.Document);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        public bool PromptToSaveWorkspace()
        {
            if (Workspace == null)
            {
                return true;
            }
            if (Workspace.IsDirty)
            {
                var dialogResult = MessageBox.Show(this, "Do you want to save changes to this workspace?",
                                                       Program.AppName, MessageBoxButtons.YesNoCancel);
                if (dialogResult == DialogResult.Cancel)
                {
                    return false;
                }
                if (dialogResult == DialogResult.Yes)
                {
                    Workspace.Save();
                }
            }
            return true;
        }

        private void closeWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!PromptToSaveWorkspace())
            {
                return;
            }
            Workspace = null;
        }

        public bool EnsureMsDataFile(MsDataFile msDataFile)
        {
            String errorMessage;
            if (msDataFile.ValidationStatus == ValidationStatus.reject)
            {
                return false;
            }
            if (MsDataFileUtil.TryInitMsDataFile(Workspace, msDataFile, msDataFile.Path, out errorMessage))
            {
                return true;
            }
            String name;
            if (String.IsNullOrEmpty(msDataFile.Path))
            {
                String pathToTry = Path.Combine(Settings.Default.RawFilesDirectory, msDataFile.Name + ".RAW");
                String msgIgnore;
                if (File.Exists(pathToTry))
                {
                    if (MsDataFileUtil.TryInitMsDataFile(Workspace, msDataFile, pathToTry, out msgIgnore))
                    {
                        return true;
                    }
                }
                name = msDataFile.Name;
            }
            else
            {
                name = msDataFile.Path;
            }

            DialogResult dialogResult = MessageBox.Show(
                "Unable to open the data file for " + name + ". " + errorMessage + " Do you want to look for this file?",
                Program.AppName, MessageBoxButtons.OKCancel);
            while (dialogResult == DialogResult.OK)
            {
                OpenFileDialog fileDialog = new OpenFileDialog
                {
                    Filter = msDataFile.Name + ".*|" + msDataFile.Name + ".*"
                             + "|All Files|*.*",
                    Title = "Browser for " + msDataFile.Name,
                    InitialDirectory = Settings.Default.RawFilesDirectory
                };
                fileDialog.ShowDialog(this);

                if (String.IsNullOrEmpty(fileDialog.FileName))
                {
                    break;
                }

                Settings.Default.RawFilesDirectory = Path.GetDirectoryName(fileDialog.FileName);
                if (MsDataFileUtil.TryInitMsDataFile(Workspace, msDataFile, fileDialog.FileName, out errorMessage))
                {
                    return true;
                }
                dialogResult =
                    MessageBox.Show(errorMessage + " Do you want to keep looking for a different file?", Program.AppName, MessageBoxButtons.OKCancel);
            }
            msDataFile.ValidationStatus = ValidationStatus.reject;
            using (var session = Workspace.OpenWriteSession())
            {
                session.BeginTransaction();
                msDataFile.Save(session);
                session.Transaction.Commit();
            }
            return false;
        }

        private void dataFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dataFilesForm = Program.FindOpenForm<DataFilesForm>();
            if (dataFilesForm != null)
            {
                dataFilesForm.Activate();
                return;
            }
            dataFilesForm = new DataFilesForm(Workspace);
            dataFilesForm.Show(dockPanel, DockState.Document);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!PromptToSaveWorkspace())
            {
                e.Cancel = true;
                return;
            }
            Settings.Default.Save();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            Workspace = null;
            base.OnClosed(e);
        }

        private void ActionInvoker(Action action)
        {
            BeginInvoke(action);
        }

        private void peptideAnalysesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var peptideComparisonsForm = Program.FindOpenForm<PeptideAnalysesForm>();
            if (peptideComparisonsForm != null)
            {
                peptideComparisonsForm.Activate();
                return;
            }

            peptideComparisonsForm = new PeptideAnalysesForm(Workspace);
            peptideComparisonsForm.Show(dockPanel, DockState.Document);
        }

        private void modificationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new ModificationsForm(Workspace).ShowDialog(this);
        }

        private void saveWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Workspace.Save();
        }

        private void statusToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = Program.FindOpenForm<StatusForm>();
            if (form == null)
            {
                new StatusForm(Workspace).Show(this);
            }
            else
            {
                form.Activate();
            }
        }

        private void updateProteinNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog
                                     {
                                         Title = "Browse for FASTA file"
                                     };
            openFileDialog.ShowDialog(this);
            var fileName = openFileDialog.FileName;
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }
            var updateProteinNames = new UpdateProteinNames(Workspace)
                                         {
                                             FastaFilePath = openFileDialog.FileName
                                         };
            updateProteinNames.ShowDialog(this);
        }

        private void queriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = Program.FindOpenForm<QueriesForm>();
            if (form != null)
            {
                form.Activate();
                return;
            }
            form = new QueriesForm(Workspace);
            form.Show(dockPanel, DockState.Document);
        }

        private void machineSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new MiscSettingsForm(Workspace).ShowDialog(this);
        }

        private void mercuryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new MercuryForm(Workspace).Show(this);
        }
    }
}
