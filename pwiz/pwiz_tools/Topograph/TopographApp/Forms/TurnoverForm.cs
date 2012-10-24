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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Data.Snapshot;
using pwiz.Topograph.MsData;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Forms.Dashboard;
using pwiz.Topograph.ui.Properties;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class TurnoverForm : Form
    {
        public const string WorkspaceFilter = "Topograph Workspaces(*.tpg)|*.tpg";
        public const string OnlineWorkspaceFilter = "Topograph Online Workspaces(*.tpglnk)|*.tpglnk";
        public const string AnyWorkspaceFilter = "Topograph Workspaces(*.tpg,*.tpglnk)|*.tpg;*.tpglnk";

        private Workspace _workspace;

        public TurnoverForm()
        {
            InitializeComponent();
            Icon = Resources.TopographIcon;
            Instance = this;
            ShowDashboard();
            var activationArgs = AppDomain.CurrentDomain.SetupInformation.ActivationArguments;
            string[] args = (activationArgs != null ? activationArgs.ActivationData : null);
            if (args != null && args.Length != 0)
            {
                try
                {
                    Uri uri = new Uri(args[0]);
                    if (!uri.IsFile)
                        throw new UriFormatException("The URI " + uri + " is not a file.");

                    string pathOpen = Uri.UnescapeDataString(uri.AbsolutePath);

                    // Handle direct open from UNC path names
                    if (!string.IsNullOrEmpty(uri.Host))
                        pathOpen = "//" + uri.Host + pathOpen;

                    Workspace = OpenWorkspace(pathOpen);
                }
                catch (UriFormatException)
                {
                    MessageBox.Show(this, "Invalid file specified.", Program.AppName);
                }
            }
            UpdateWindowTitle();
        }

        private void ErrorHandler_ErrorAdded(Topograph.Util.Error error)
        {
            try
            {
                BeginInvoke(new Action(delegate
                                           {
                                               var form = Program.FindOpenForm<ErrorForm>();
                                               if (form != null)
                                               {
                                                   return;
                                               }
                                               new ErrorForm().Show(this);
                                           }));
            }
            catch
            {

            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ErrorHandler.ErrorAdded += ErrorHandler_ErrorAdded;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            ErrorHandler.ErrorAdded -= ErrorHandler_ErrorAdded;
            base.OnHandleDestroyed(e);
        }

        private IList<ToolStripMenuItem> WorkspaceOpenMenuItems
        {
            get
            {
                return new[]
                           {
                               closeWorkspaceToolStripMenuItem,
                               runningJobsToolStripMenuItem,
                               databaseLocksToolStripMenuItem,
                               outputWorkspaceSQLToolStripMenuItem,
                               recalculateResultsToolStripMenuItem,
                           };
            }
        }

        private ToolStripMenuItem[] WorkspaceLoadedMenuItems
        {
            get
            {
                return new[]
                           {
                               modificationsToolStripMenuItem,
                               addSearchResultsToolStripMenuItem,
                               enrichmentToolStripMenuItem,
                               peptidesToolStripMenuItem,
                               peptideAnalysesToolStripMenuItem,
                               queryToolStripMenuItem,
                               dataFilesToolStripMenuItem,
                               saveWorkspaceToolStripMenuItem,
                               updateProteinNamesToolStripMenuItem,
                               machineSettingsToolStripMenuItem,
                               mercuryToolStripMenuItem,
                               halfLivesToolStripMenuItem,
                               dataDirectoryToolStripMenuItem,
                               resultsPerGroupToolStripMenuItem,
                               precursorEnrichmentsToolStripMenuItem,
                               resultsByReplicateToolStripMenuItem,
                               alignmentToolStripMenuItem,
                               acceptanceCriteriaToolStripMenuItem,
                               precursorPoolSimulatorToolStripMenuItem,
                           };
            }
        }

        public static TurnoverForm Instance { get; private set; }

        public Workspace Workspace
        {
            get { return _workspace; }
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
                    _workspace.WorkspaceLoaded -= Workspace_WorkspaceLoaded;
                }
                _workspace = value;
                if (_workspace != null)
                {
                    _workspace.SetActionInvoker(ActionInvoker);
                    foreach (var menuItem in WorkspaceOpenMenuItems)
                    {
                        menuItem.Enabled = true;
                    }
                    foreach (var menuItem in WorkspaceLoadedMenuItems)
                    {
                        menuItem.Enabled = _workspace.IsLoaded;
                    }
                    databaseSizeToolStripMenuItem.Enabled
                        = _workspace.TpgLinkDef != null
                          && _workspace.TpgLinkDef.DatabaseTypeEnum == DatabaseTypeEnum.mysql;
                    _workspace.EntitiesChange += Workspace_EntitiesChange;
                    _workspace.WorkspaceDirty += Workspace_WorkspaceDirty;
                    _workspace.WorkspaceLoaded += Workspace_WorkspaceLoaded;
                }
                else
                {
                    foreach (var menuItem in WorkspaceOpenMenuItems)
                    {
                        menuItem.Enabled = false;
                    }
                    foreach (var menuItem in WorkspaceLoadedMenuItems)
                    {
                        menuItem.Enabled = false;
                    }
                    databaseSizeToolStripMenuItem.Enabled = false;
                }
                UpdateWindowTitle();
                if (_workspace != null)
                {
                    Settings.Default.Reload();
                    var path = _workspace.DatabasePath;
                    // Store the path in the MRU.
                    List<string> mruList = Settings.Default.MruList;
                    if (mruList.Count == 0 || !Equals(path, mruList[0]))
                    {
                        mruList.Remove(path);
                        mruList.Insert(0, path);
                        int len = Settings.Default.MruLength;
                        if (mruList.Count > len)
                            mruList.RemoveRange(len, mruList.Count - len);
                        Settings.Default.Save();
                    }
                }
                if (WorkspaceChange != null)
                {
                    WorkspaceChange.Invoke(this, new EventArgs());
                }
            }
        }

        public event EventHandler WorkspaceChange;

        private void Workspace_WorkspaceDirty(Workspace workspace)
        {
            UpdateWindowTitle();
        }

        public void EnsureDataDirectory(Workspace workspace)
        {
            if (!workspace.IsLoaded)
            {
                return;
            }
            if (workspace.MsDataFiles.ChildCount == 0)
            {
                return;
            }
            var dataDirectory = workspace.GetDataDirectory();
            if (dataDirectory == "")
            {
                return;
            }
            if (dataDirectory != null && Directory.Exists(dataDirectory))
            {
                return;
            }
            var message = dataDirectory == null ? "" : "The data directory '" + dataDirectory + "' does not exist.";
            message += "Do you want to browse for the directory containing the MS data files in this workspace?";
            if (MessageBox.Show(message, Program.AppName, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
            {
                workspace.SetDataDirectory("");
                return;
            }
            BrowseForDataDirectory();
        }

        private void UpdateWindowTitle()
        {
            var appName = Environment.Is64BitProcess ? "Topograph (64-bit)" : "Topograph";
            if (Workspace == null)
            {
                Text = appName;
                return;
            }
            var text = Path.GetFileName(_workspace.DatabasePath);
            if (_workspace.IsLoaded)
            {
                if (_workspace.WorkspaceVersion.Equals(_workspace.SavedWorkspaceVersion))
                {
                    if (_workspace.IsDirty)
                    {
                        text += "(changed)";
                    }
                }
                else
                {
                    text += "(settings changed)";
                }
                halfLivesToolStripMenuItem.Enabled =
                    precursorEnrichmentsToolStripMenuItem.Enabled = _workspace.GetTracerDefs().Count > 0;
            }
            else
            {
                text += "(loading)";
            }

            text += " - " + appName;
            Text = text;
        }

        private void Workspace_EntitiesChange(EntitiesChangedEventArgs entitiesChangedEventArgs)
        {
            UpdateWindowTitle();
        }

        private void Workspace_WorkspaceLoaded(Workspace workspace)
        {
            BeginInvoke(new Action(WorkspaceLoaded));
        }

        private void WorkspaceLoaded()
        {
            foreach (var menuItem in WorkspaceLoadedMenuItems)
            {
                menuItem.Enabled = true;
            }
            ShowDashboard();
            EnsureDataDirectory(_workspace);
        }

        public static void InitWorkspace(ISessionFactory sessionFactory)
        {
            using (var session = sessionFactory.OpenSession())
            {
                var transaction = session.BeginTransaction();
                var dbWorkspace = new DbWorkspace
                                      {
                                          ModificationCount = 1,
                                          TracerDefCount = 1,
                                          SchemaVersion = WorkspaceUpgrader.CurrentVersion,
                                      };
                session.Save(dbWorkspace);
                DbTracerDef dbTracerDef = TracerDef.GetD3LeuEnrichment();
                dbTracerDef.Workspace = dbWorkspace;
                dbTracerDef.Name = "Tracer";
                session.Save(dbTracerDef);

                var modification = new DbModification
                                       {
                                           DeltaMass = 57.021461,
                                           Symbol = "C",
                                           Workspace = dbWorkspace
                                       };
                session.Save(modification);
                transaction.Commit();
            }
        }

        private void newWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NewWorkspace();
        }

        public void NewWorkspace()
        {
            if (!PromptToSaveWorkspace())
            {
                return;
            }
            Settings.Default.Reload();
            using (var fileDialog = new SaveFileDialog()
                                        {
                                            Filter = WorkspaceFilter,
                                            InitialDirectory = Settings.Default.WorkspaceDirectory
                                        })
            {
                if (fileDialog.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }
                String filename = fileDialog.FileName;
                Settings.Default.WorkspaceDirectory = Path.GetDirectoryName(filename);
                Settings.Default.Save();
                if (File.Exists(filename))
                {
                    File.Delete(filename);
                }
                using (
                    ISessionFactory sessionFactory = SessionFactoryFactory.CreateSessionFactory(filename,
                                                                                                SessionFactoryFlags.
                                                                                                    create_schema))
                {
                    InitWorkspace(sessionFactory);
                }
                Workspace = new Workspace(filename);
                EditIsotopeLabels();
            }
        }

        public Workspace OpenWorkspace(String path)
        {
            var upgrader = new WorkspaceUpgrader(path);
            int version;
            try
            {
                using (var connection = upgrader.OpenConnection())
                {
                    try
                    {
                        version = upgrader.ReadSchemaVersion(connection);
                    }
                    catch (Exception e)
                    {
                        Console.Out.WriteLine(e);
                        MessageBox.Show(
                            "Unable to read version number from the workspace.  This workspace may be too old to be upgraded.");
                        return null;
                    }
                }
            }
            catch (Exception openException)
            {
                Console.Out.WriteLine(openException);
                MessageBox.Show("Unable to open the database:" + openException.Message);
                return null;
            }
            if (version > WorkspaceUpgrader.CurrentVersion || version < WorkspaceUpgrader.MinUpgradeableVersion)
            {
                MessageBox.Show("This workspace cannot be opened by this version of Topograph", Program.AppName);
                return null;
            }
            if (version < WorkspaceUpgrader.CurrentVersion)
            {
                var result =
                    MessageBox.Show(
                        "This workspace needs to be upgraded to this version of Topograph.  Do you want to do that now?",
                        Program.AppName, MessageBoxButtons.OKCancel);
                if (result == DialogResult.Cancel)
                {
                    return null;
                }
                using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Upgrading Workspace"))
                {
                    if (!new LongOperationBroker(upgrader, longWaitDialog).LaunchJob())
                    {
                        return null;
                    }
                }
            }
            return new Workspace(path);
        }

        private void openWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DisplayOpenWorkspaceDialog();
        }

        public DialogResult DisplayOpenWorkspaceDialog()
        {
            if (!PromptToSaveWorkspace())
            {
                return DialogResult.Cancel;
            }
            Settings.Default.Reload();
            using (var fileDialog = new OpenFileDialog
                                        {
                                            Filter = AnyWorkspaceFilter,
                                            InitialDirectory = Settings.Default.WorkspaceDirectory
                                        })
            {
                if (fileDialog.ShowDialog(this) == DialogResult.Cancel)
                {
                    return DialogResult.Cancel;
                }
                String filename = fileDialog.FileName;
                Settings.Default.WorkspaceDirectory = Path.GetDirectoryName(filename);
                Settings.Default.Save();
                var workspace = OpenWorkspace(filename);
                if (workspace != null)
                {
                    Workspace = workspace;
                }

            }
            return DialogResult.OK;
        }

        private void addSearchResultsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddSearchResults();
        }

        public void AddSearchResults()
        {
            new AddSearchResultsForm(Workspace).Show(this);
        }

        private void enrichmentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditIsotopeLabels();
        }

        public void EditIsotopeLabels()
        {
            if (Workspace.GetTracerDefs().Count == 0)
            {
                using (var dialog = new DefineLabelDialog(Workspace, null))
                {
                    dialog.ShowDialog(this);
                }
            }
            else
            {
                using (var dialog = new ManageLabelsDialog(Workspace))
                {
                    dialog.ShowDialog(this);
                }
            }
        }

        private void peptidesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var peptidesForm = Program.FindOpenForm<PeptidesForm>();
            if (peptidesForm != null)
            {
                RestoreAndActivate(peptidesForm);
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
                    return SaveWorkspace();
                }
            }
            return true;
        }

        private bool SaveWorkspace()
        {
            using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Saving Workspace"))
            {
                return Workspace.Save(longWaitDialog);
            }
        }

        private void closeWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CloseWorkspace();
        }

        public DialogResult CloseWorkspace()
        {
            if (!PromptToSaveWorkspace())
            {
                return DialogResult.Cancel;
            }
            Workspace = null;
            return DialogResult.OK;
        }

        public bool EnsureMsDataFile(MsDataFile msDataFile)
        {
            return EnsureMsDataFile(msDataFile, false);
        }

        public bool EnsureMsDataFile(MsDataFile msDataFile, bool alwaysPrompt)
        {
            String errorMessage;
            if (!alwaysPrompt && msDataFile.Workspace.IsRejected(msDataFile))
            {
                return false;
            }
            if (MsDataFileUtil.TryInitMsDataFile(Workspace, msDataFile, out errorMessage))
            {
                return true;
            }
            DialogResult dialogResult = MessageBox.Show(
                "Unable to open the data file for " + msDataFile.Name + ". " + errorMessage +
                " Do you want to look for this file?",
                Program.AppName, MessageBoxButtons.OKCancel);
            while (dialogResult == DialogResult.OK)
            {
                using (OpenFileDialog fileDialog = new OpenFileDialog
                                                       {
                                                           Filter = msDataFile.Name + ".*|" + msDataFile.Name + ".*"
                                                                    + "|All Files|*.*",
                                                           Title = "Browser for " + msDataFile.Name,
                                                           InitialDirectory = Settings.Default.RawFilesDirectory
                                                       })
                {
                    fileDialog.ShowDialog(this);

                    if (String.IsNullOrEmpty(fileDialog.FileName))
                    {
                        break;
                    }
                    Workspace.SetDataDirectory(Path.GetDirectoryName(fileDialog.FileName));
                }
                if (MsDataFileUtil.TryInitMsDataFile(Workspace, msDataFile, out errorMessage))
                {
                    return true;
                }
                dialogResult =
                    MessageBox.Show(errorMessage + " Do you want to keep looking for a different file?", Program.AppName,
                                    MessageBoxButtons.OKCancel);
            }
            Workspace.RejectMsDataFile(msDataFile);
            return false;
        }

        private void dataFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowDataFiles();
        }

        public void ShowDataFiles()
        {
            var dataFilesForm = Program.FindOpenForm<DataFilesForm>();
            if (dataFilesForm != null)
            {
                RestoreAndActivate(dataFilesForm);
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
            ShowPeptideAnalyses();
        }

        public void ShowPeptideAnalyses()
        {
            var peptideComparisonsForm = Program.FindOpenForm<PeptideAnalysesForm>();
            if (peptideComparisonsForm != null)
            {
                RestoreAndActivate(peptideComparisonsForm);
                return;
            }

            peptideComparisonsForm = new PeptideAnalysesForm(Workspace);
            peptideComparisonsForm.Show(dockPanel, DockState.Document);
        }

        private void modificationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditModifications();
        }

        public DialogResult EditModifications()
        {
            using (var form = new ModificationsForm(Workspace))
            {
                return form.ShowDialog(TopLevelControl);
            }
        }

        private void saveWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveWorkspace();
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
                RestoreAndActivate(form);
            }
        }

        private void RestoreAndActivate(Form form)
        {
            if (form.WindowState == FormWindowState.Minimized)
            {
                form.WindowState = FormWindowState.Normal;
            }
            form.Activate();
        }

        private void updateProteinNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChooseFastaFile();
        }

        public void ChooseFastaFile()
        {
            Settings.Default.Reload();
            using (var openFileDialog = new OpenFileDialog
            {
                Title = "Browse for FASTA file",
                Multiselect = true,
                InitialDirectory = Settings.Default.FastaDirectory,
            })
            {
                if (openFileDialog.ShowDialog(this) == DialogResult.Cancel || openFileDialog.FileNames.Count() == 0)
                {
                    return;
                }
                Settings.Default.FastaDirectory = Path.GetDirectoryName(openFileDialog.FileNames[0]);
                Settings.Default.Save();
                using (var updateProteinNames = new UpdateProteinNames(Workspace)
                {
                    FastaFilePaths = openFileDialog.FileNames
                })
                {
                    updateProteinNames.ShowDialog(TopLevelControl);
                }
            }
        }

    private void queriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = Program.FindOpenForm<QueriesForm>();
            if (form != null)
            {
                RestoreAndActivate(form);
                return;
            }
            form = new QueriesForm(Workspace);
            form.Show(dockPanel, DockState.Document);
        }

        private void machineSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditMachineSettings();
        }

        public DialogResult EditMachineSettings()
        {
            using (var form = new MiscSettingsForm(Workspace))
            {
                return form.ShowDialog(TopLevelControl);
            }
        }

        private void mercuryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new MercuryForm(Workspace).Show(this);
        }

        private void halfLivesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowHalfLivesForm();
        }

        public void ShowHalfLivesForm()
        {
            HalfLivesForm halfLivesForm = new HalfLivesForm(Workspace);
            halfLivesForm.Show(dockPanel, DockState.Document);
        }

        public DockPanel DocumentPanel { get { return dockPanel; } }

        private void newOnlineWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NewOnlineWorkspace();
        }

        public void NewOnlineWorkspace()
        {
            if (!PromptToSaveWorkspace())
            {
                return;
            }
            using (var dialog = new NewOnlineWorkspaceForm())
            {
                if (dialog.ShowDialog(TopLevelControl) == DialogResult.Cancel)
                {
                    return;
                }
                Workspace = OpenWorkspace(dialog.Filename);
            }
        }

        private void openOnlineWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConnectToOnlineWorkspace();
        }

        public void ConnectToOnlineWorkspace()
        {
            if (!PromptToSaveWorkspace())
            {
                return;
            }
            using (var dialog = new ConnectToOnlineWorkspaceForm())
            {
                while (true)
                {
                    if (dialog.ShowDialog(TopLevelControl) == DialogResult.Cancel)
                    {
                        return;
                    }
                    try
                    {
                        Workspace = OpenWorkspace(dialog.Filename);
                        return;
                    }
                    catch (Exception exception)
                    {
                        var result = MessageBox.Show(this,
                                                     "There was an error connecting to the database.  Do you want to try again?\n" +
                                                     exception, Program.AppName, MessageBoxButtons.OKCancel);
                        if (result == DialogResult.Cancel)
                        {
                            return;
                        }
                        continue;
                    }
                }
            }
        }

        public void BrowseForDataDirectory()
        {
            while (true)
            {
                using (var dialog = new FolderBrowserDialog()
                    {
                        ShowNewFolderButton = false
                    })
                {
                    var currentDataDirectory = Workspace.GetDataDirectory();
                    if (!string.IsNullOrEmpty(currentDataDirectory) && Directory.Exists(currentDataDirectory))
                    {
                        dialog.SelectedPath = currentDataDirectory;
                    }
                    var dialogResult = dialog.ShowDialog(this);
                    if (dialogResult == DialogResult.Cancel)
                    {
                        Workspace.SetDataDirectory("");
                        return;
                    }
                    if (!Workspace.IsValidDataDirectory(dialog.SelectedPath))
                    {
                        if (MessageBox.Show(
                            "That directory does not appear to contain any of the data files from this workspace.  Are you sure you want to use that directory?",
                            Program.AppName, MessageBoxButtons.YesNo) == DialogResult.No)
                        {
                            continue;
                        }
                    }
                    Workspace.SetDataDirectory(dialog.SelectedPath);
                    return;
                }
            }
        }

        private void dataDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BrowseForDataDirectory();
        }

        private void locksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var locksForm = Program.FindOpenForm<LocksForm>();
            if (locksForm != null)
            {
                RestoreAndActivate(locksForm);
                return;
            }
            locksForm = new LocksForm(Workspace);
            locksForm.Show(DocumentPanel, DockState.Floating); 
        }

        public PeptideAnalysis LoadPeptideAnalysis(long id)
        {
            PeptideAnalysis peptideAnalysis;
            LoadPeptideAnalyses(new[] {id}, true).TryGetValue(id, out peptideAnalysis);
            return peptideAnalysis;
        }

        public Dictionary<long,PeptideAnalysis> LoadPeptideAnalyses(ICollection<long> ids)
        {
            return LoadPeptideAnalyses(ids, false);
        }

        public Dictionary<long, PeptideAnalysis> LoadPeptideAnalyses(ICollection<long> ids, bool loadChromatograms)
        {
            var job = new LoadPeptideAnalysisSnapshot(Workspace, ids, loadChromatograms);
            var title = ids.Count == 1 ? "Loading peptide analysis" : "Loading " + ids.Count + " peptide analyses";
            using (var longWaitDialog = new LongWaitDialog(TopLevelControl, title))
            {
                new LongOperationBroker(job, longWaitDialog).LaunchJob();
            }
            return job.PeptideAnalyses;
        }

        private void outputWorkspaceSQLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!PromptToSaveWorkspace())
            {
                return;
            }
            using (var dumpWorkspaceDlg = new DumpWorkspaceDlg())
            {
                if (dumpWorkspaceDlg.ShowDialog(TopLevelControl) == DialogResult.Cancel)
                {
                    return;
                }
                Settings.Default.Reload();
                using (var fileDialog = new SaveFileDialog()
                    {
                        Title = "Export Workspace SQL",
                        Filter = "SQL Files (*.sql)|*.sql|All Files|*.*",
                        FileName = Path.GetFileNameWithoutExtension(Workspace.DatabasePath) + ".sql",
                        InitialDirectory = Settings.Default.ExportResultsDirectory,
                    })
                {
                    if (fileDialog.ShowDialog(this) == DialogResult.Cancel)
                    {
                        return;
                    }
                    Settings.Default.ExportResultsDirectory = Path.GetDirectoryName(fileDialog.FileName);
                    Settings.Default.Save();
                    var workspace = new Workspace(Workspace.DatabasePath);
                    try
                    {
                        Workspace = null;
                        using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Exporting SQL"))
                        {
                            var databaseDumper = new DatabaseDumper(workspace, dumpWorkspaceDlg.DatabaseTypeEnum, fileDialog.FileName);
                            new LongOperationBroker(databaseDumper, longWaitDialog).LaunchJob();
                        }
                    }
                    finally
                    {
                        Workspace = OpenWorkspace(workspace.DatabasePath);
                    }
                    
                }                
            }
        }

        private void errorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = Program.FindOpenForm<ErrorForm>();
            if (form != null)
            {
                RestoreAndActivate(form);
                return;
            }
            new ErrorForm().Show(this);
        }

        private void resultsPerGroupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowResultsPerGroup();
        }

        public void ShowResultsPerGroup()
        {
            var tracerAmountsForm = new ResultsPerGroupForm(Workspace);
            tracerAmountsForm.Show(dockPanel, DockState.Document);
        }

        private void precursorEnrichmentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = new PrecursorEnrichmentForm(Workspace);
            form.Show(dockPanel, DockState.Document);
        }

        private void recalculateResultsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var f = Program.FindOpenForm<RecalculateResultsForm>();
            if (f == null)
            {
                f = new RecalculateResultsForm(Workspace);
                f.Show(this);
            }
            else
            {
                RestoreAndActivate(f);
            }
        }

        private void displayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var settingsForm = new DisplaySettingsForm())
            {
                settingsForm.ShowDialog(TopLevelControl);
            }
        }

        private void fileToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = fileToolStripMenuItem;
            List<string> mruList = Settings.Default.MruList;
            string curDir = Settings.Default.WorkspaceDirectory;

            int start = menu.DropDownItems.IndexOf(mruBeforeToolStripSeparator) + 1;
            while (!ReferenceEquals(menu.DropDownItems[start], mruAfterToolStripSeparator))
                menu.DropDownItems.RemoveAt(start);
            for (int i = 0; i < mruList.Count; i++)
            {
                MruChosenHandler handler = new MruChosenHandler(this, mruList[i]);
                ToolStripMenuItem item = new ToolStripMenuItem(GetMruName(i, mruList[i], curDir), null,
                    handler.ToolStripMenuItemClick);
                menu.DropDownItems.Insert(start + i, item);
            }
            mruAfterToolStripSeparator.Visible = (mruList.Count > 0);

        }

        private static string GetMruName(int index, string path, string curDir)
        {
            string name = path;
            if (curDir == Path.GetDirectoryName(path))
                name = Path.GetFileName(path);
            // Make index 1-based
            index++;
            if (index < 9)
                name = string.Format("&{0} {1}", index, name);
            return name;
        }

        private class MruChosenHandler
        {
            private readonly TurnoverForm _turnoverForm;
            private readonly string _path;

            public MruChosenHandler(TurnoverForm turnoverForm, string path)
            {
                _turnoverForm = turnoverForm;
                _path = path;
            }

            public void ToolStripMenuItemClick(object sender, EventArgs e)
            {
                if (!_turnoverForm.PromptToSaveWorkspace())
                {
                    return;
                }
                var workspace = _turnoverForm.OpenWorkspace(_path);
                if (workspace != null)
                {
                    _turnoverForm.Workspace = workspace;
                }
            }
        }

        private void resultsByReplicateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowResultsByReplicate();
        }

        public void ShowResultsByReplicate()
        {
            var resultsPerReplicateForm = new ResultsPerReplicateForm(Workspace);
            resultsPerReplicateForm.Show(dockPanel, DockState.Document);
        }

        private void alignmentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AlignmentForm(Workspace).Show(dockPanel, DockState.Document);
        }

        private void databaseSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new DatabaseSizeForm(Workspace).Show(this);
        }

        private void acceptanceCriteriaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new AcceptanceCriteriaForm(Workspace))
            {
                form.ShowDialog(this);
            }
        }

        private void precursorPoolSimulatorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new PrecursorPoolSimulator(Workspace).Show(this);
        }

        private void dashboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowDashboard();
        }

        public void ShowDashboard()
        {
            var dashboardForm = Program.FindOpenForm<DashboardForm>();
            if (dashboardForm != null)
            {
                dashboardForm.Activate();
            }
            else
            {
                dashboardForm = new DashboardForm(this);
                dashboardForm.Show(dockPanel, DockState.Document);
            }
        }

        public void AnalyzePeptides()
        {
            new AnalyzePeptidesForm(Workspace).Show(this);
        }

        private void aboutTopographToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new AboutDlg())
            {
                dlg.ShowDialog(this);
            }
        }
    }
}
