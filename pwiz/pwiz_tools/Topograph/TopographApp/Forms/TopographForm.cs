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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Data.Snapshot;
using pwiz.Topograph.MsData;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Forms.Dashboard;
using pwiz.Topograph.ui.Properties;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class TopographForm : Form
    {
        public const string WorkspaceFilter = "Topograph Workspaces(*.tpg)|*.tpg";
        public const string OnlineWorkspaceFilter = "Topograph Online Workspaces(*.tpglnk)|*.tpglnk";
        public const string AnyWorkspaceFilter = "Topograph Workspaces(*.tpg,*.tpglnk)|*.tpg;*.tpglnk";

        private Workspace _workspace;
        private bool _workspaceLoaded;
        private bool _workspaceOpen;

        public TopographForm()
        {
            InitializeComponent();
            Icon = Resources.TopographIcon;
            Instance = this;
            ShowDashboard();
            statusBarToolStripMenuItem.Checked = statusBar.Visible = Settings.Default.ShowStatusBar;
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

        private void ErrorHandler_ErrorAdded(Error error)
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
            catch(Exception exception)
            {
                Trace.TraceError("Error trying to display Error Window:{0}", exception);
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
                               isotopeLabelsToolStripMenuItem,
                               peptidesToolStripMenuItem,
                               peptideAnalysesToolStripMenuItem,
                               dataFilesToolStripMenuItem,
                               saveWorkspaceToolStripMenuItem,
                               updateProteinNamesToolStripMenuItem,
                               machineSettingsToolStripMenuItem,
                               viewIsotopeDistributionGraphToolsStripMenuItem,
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

        public static TopographForm Instance { get; private set; }

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
                    _workspace.SetTaskScheduler(null);
                    _workspace.Change -= WorkspaceOnChange;
                    _workspace.Dispose();
                }
                _workspace = value;
                if (_workspace != null)
                {
                    _workspace.Change += WorkspaceOnChange;
                    _workspace.SetTaskScheduler(TaskScheduler.FromCurrentSynchronizationContext());
                }
                UpdateAll();
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

        public void EnsureDataDirectory(Workspace workspace)
        {
            if (!workspace.IsLoaded)
            {
                return;
            }
            if (workspace.MsDataFiles.Count == 0)
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
                var savedWorkspaceChange = _workspace.SavedWorkspaceChange;
                if (!savedWorkspaceChange.HasSettingChange)
                {
                    if (_workspace.IsDirty)
                    {
                        text += "(changed)";
                    }
                }
                else
                {
                    bool anyChromatograms = _workspace.HasAnyChromatograms();
                    if (savedWorkspaceChange.HasChromatogramMassChange && anyChromatograms)
                    {
                        text += "(settings changed; chromatograms need regenerating)";
                    }
                    else if (savedWorkspaceChange.HasPeakPickingChange && anyChromatograms)
                    {
                        text += "(settings changed; results need recalculating)";
                    }
                    else
                    {
                        text += "(settings changed)";
                    }
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

        private void UpdateAll()
        {
            UpdateWindowTitle();
            if (_workspaceOpen != (Workspace != null))
            {
                _workspaceOpen = Workspace != null;
                foreach (var item in WorkspaceOpenMenuItems)
                {
                    item.Enabled = _workspaceOpen;
                }
                ShowDashboard();
            }
            var workspaceLoaded = Workspace != null && Workspace.IsLoaded;
            if (_workspaceLoaded != workspaceLoaded)
            {
                _workspaceLoaded = workspaceLoaded;
                foreach (var item in WorkspaceLoadedMenuItems)
                {
                    item.Enabled = workspaceLoaded;
                }
                if (workspaceLoaded)
                {
                    EnsureDataDirectory(Workspace);
                }
            }
            databaseSizeToolStripMenuItem.Enabled = Workspace != null && Workspace.TpgLinkDef != null &&
                                                    Workspace.TpgLinkDef.DatabaseTypeEnum == DatabaseTypeEnum.mysql;
        }

        private void WorkspaceOnChange(object sender, WorkspaceChangeArgs args)
        {
            UpdateAll();
        }

        public static void InitWorkspace(ISessionFactory sessionFactory)
        {
            using (var session = sessionFactory.OpenSession())
            {
                var transaction = session.BeginTransaction();
                var dbWorkspace = new DbWorkspace
                                      {
                                          ModificationCount = 1,
                                          TracerDefCount = 0,
                                          SchemaVersion = WorkspaceUpgrader.CurrentVersion,
                                      };
                session.Save(dbWorkspace);

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

        private void NewWorkspaceToolStripMenuItemOnClick(object sender, EventArgs e)
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
            using (var fileDialog = new SaveFileDialog
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
                    ISessionFactory sessionFactory = SessionFactoryFactory.CreateSessionFactory(filename, SessionFactoryFlags.CreateSchema))
                {
                    InitWorkspace(sessionFactory);
                }
                Workspace = new Workspace(filename);
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
                    if (!new LongOperationBroker(upgrader.Run, longWaitDialog).LaunchJob())
                    {
                        return null;
                    }
                }
            }
            return new Workspace(path);
        }

        private void OpenWorkspaceToolStripMenuItemOnClick(object sender, EventArgs e)
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

        private void AddSearchResultsToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            AddSearchResults();
        }

        public void AddSearchResults()
        {
            new AddSearchResultsForm(Workspace).Show(this);
        }

        private void IsotopeLabelsToolStripMenuItemOnClick(object sender, EventArgs e)
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

        private void PeptidesToolStripMenuItemOnClick(object sender, EventArgs e)
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

        private void ExitToolStripMenuItemOnClick(object sender, EventArgs e)
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
            if ((Workspace.SavedWorkspaceChange.HasChromatogramMassChange || Workspace.SavedWorkspaceChange.HasPeakPickingChange) 
                && Workspace.HasAnyChromatograms())
            {
                string message;
                if (Workspace.SavedWorkspaceChange.HasChromatogramMassChange)
                {
                    message =
                        "You have made changes to the settings which affect the calculated mass of peptides.  When you save this workspace, the chromatograms which have already been generated will be deleted, and will need to be regenerated.  Are you sure you want to save this workspace now?";
                }
                else
                {
                    message =
                        "You have made changes the the settings which affect how Topograph integrates peaks.  When you save this workspace, the results which Topograph has already calculated will be deleted, and they will need to be recalculated.  Are you sure you want to save this workspace now?";
                }
                if (MessageBox.Show(this, message, Program.AppName, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                {
                    return false;
                }
            }

            using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Saving Workspace"))
            {
                return Workspace.Save(longWaitDialog);
            }
        }

        private void CloseWorkspaceToolStripMenuItemOnClick(object sender, EventArgs e)
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
            if (TryInitMsDataFile(this, msDataFile, out errorMessage))
            {
                return true;
            }
            if (null == errorMessage)
            {
                return false;
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
                if (TryInitMsDataFile(this, msDataFile, out errorMessage))
                {
                    return true;
                }
                if (null == errorMessage)
                {
                    return false;
                }
                dialogResult =
                    MessageBox.Show(errorMessage + " Do you want to keep looking for a different file?", Program.AppName,
                                    MessageBoxButtons.OKCancel);
            }
            Workspace.RejectMsDataFile(msDataFile);
            return false;
        }

        private void DataFilesToolStripMenuItemOnClick(object sender, EventArgs e)
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

        private void PeptideAnalysesToolStripMenuItemOnClick(object sender, EventArgs e)
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

        private void ModificationsToolStripMenuItemOnClick(object sender, EventArgs e)
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

        private void SaveWorkspaceToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            SaveWorkspace();
        }

        private void StatusToolStripMenuItemOnClick(object sender, EventArgs e)
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

        private void UpdateProteinNamesToolStripMenuItemOnClick(object sender, EventArgs e)
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
                if (openFileDialog.ShowDialog(this) == DialogResult.Cancel || !openFileDialog.FileNames.Any())
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

        private void MachineSettingsToolStripMenuItemOnClick(object sender, EventArgs e)
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

        private void MercuryToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            new IsotopeDistributionForm(Workspace).Show(this);
        }

        private void HalfLivesToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            ShowHalfLivesForm();
        }

        public void ShowHalfLivesForm()
        {
            HalfLivesForm halfLivesForm = new HalfLivesForm(Workspace);
            halfLivesForm.Show(dockPanel, DockState.Document);
        }

        public DockPanel DocumentPanel { get { return dockPanel; } }

        private void NewOnlineWorkspaceToolStripMenuItemOnClick(object sender, EventArgs e)
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

        private void OpenOnlineWorkspaceToolStripMenuItemOnClick(object sender, EventArgs e)
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
                    }
                }
            }
        }

        public void BrowseForDataDirectory()
        {
            while (true)
            {
                using (var dialog = new FolderBrowserDialog
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

        private void DataDirectoryToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            BrowseForDataDirectory();
        }

        private void LocksToolStripMenuItemOnClick(object sender, EventArgs e)
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
            LoadPeptideAnalyses(new[] {id}).TryGetValue(id, out peptideAnalysis);
            return Workspace.PeptideAnalyses.FindByKey(id);
        }

        public Dictionary<long, PeptideAnalysis> LoadPeptideAnalyses(ICollection<long> ids)
        {
            var job = new LoadPeptideAnalysisSnapshot(Workspace, ids);
            var title = ids.Count == 1 ? "Loading peptide analysis" : "Loading " + ids.Count + " peptide analyses";
            using (var longWaitDialog = new LongWaitDialog(TopLevelControl, title))
            {
                new LongOperationBroker(job.Run, longWaitDialog).LaunchJob();
            }
            var result = new Dictionary<long, PeptideAnalysis>();
            foreach (var id in ids)
            {
                PeptideAnalysis peptideAnalysis;
                if (Workspace.PeptideAnalyses.TryGetValue(id, out peptideAnalysis))
                {
                    result[id] = peptideAnalysis;
                }
            }
            return result;
        }

        private void OutputWorkspaceSqlToolStripMenuItemOnClick(object sender, EventArgs e)
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
                using (var fileDialog = new SaveFileDialog
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
                            new LongOperationBroker(databaseDumper.Run, longWaitDialog).LaunchJob();
                        }
                    }
                    finally
                    {
                        Workspace = OpenWorkspace(workspace.DatabasePath);
                    }
                    
                }                
            }
        }

        private void ErrorsToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            var form = Program.FindOpenForm<ErrorForm>();
            if (form != null)
            {
                RestoreAndActivate(form);
                return;
            }
            new ErrorForm().Show(this);
        }

        private void ResultsPerGroupToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            ShowResultsPerGroup();
        }

        public void ShowResultsPerGroup()
        {
            var tracerAmountsForm = new ResultsPerGroupForm(Workspace);
            tracerAmountsForm.Show(dockPanel, DockState.Document);
        }

        private void PrecursorEnrichmentsToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            var form = new PrecursorEnrichmentForm(Workspace);
            form.Show(dockPanel, DockState.Document);
        }

        private void RecalculateResultsToolStripMenuItemOnClick(object sender, EventArgs e)
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

        private void DisplayToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            using (var settingsForm = new DisplaySettingsForm())
            {
                settingsForm.ShowDialog(TopLevelControl);
            }
        }

        private void FileToolStripMenuItemOnDropDownOpening(object sender, EventArgs e)
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
            private readonly TopographForm _topographForm;
            private readonly string _path;

            public MruChosenHandler(TopographForm topographForm, string path)
            {
                _topographForm = topographForm;
                _path = path;
            }

            public void ToolStripMenuItemClick(object sender, EventArgs e)
            {
                if (!_topographForm.PromptToSaveWorkspace())
                {
                    return;
                }
                var workspace = _topographForm.OpenWorkspace(_path);
                if (workspace != null)
                {
                    _topographForm.Workspace = workspace;
                }
            }
        }

        private void ResultsByReplicateToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            ShowResultsByReplicate();
        }

        public void ShowResultsByReplicate()
        {
            var resultsPerReplicateForm = new ResultsPerReplicateForm(Workspace);
            resultsPerReplicateForm.Show(dockPanel, DockState.Document);
        }

        private void AlignmentToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            new AlignmentForm(Workspace).Show(dockPanel, DockState.Document);
        }

        private void DatabaseSizeToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            new DatabaseSizeForm(Workspace).Show(this);
        }

        private void AcceptanceCriteriaToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            using (var form = new AcceptanceCriteriaForm(Workspace))
            {
                form.ShowDialog(this);
            }
        }

        private void PrecursorPoolSimulatorToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            new PrecursorPoolSimulator(Workspace).Show(this);
        }

        private void DashboardToolStripMenuItemOnClick(object sender, EventArgs e)
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

        private void AboutTopographToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            using (var dlg = new AboutDlg())
            {
                dlg.ShowDialog(this);
            }
        }

        public static bool TryInitMsDataFile(IWin32Window parent, MsDataFile msDataFile, out string errorMessage)
        {
            bool success = false;
            string message = null;
            using (var longWaitDialog = new LongWaitDialog(parent, Program.AppName))
            {
                new LongOperationBroker(broker=>
                                            {
                                                broker.UpdateStatusMessage(string.Format("Reading retention times for {0}", msDataFile.Name));
                                                success = MsDataFileUtil.TryInitMsDataFile(msDataFile.Workspace, msDataFile, broker.CancellationToken, out message);
                                            }, longWaitDialog).LaunchJob();
            }
            errorMessage = message;
            return success;
        }

        private void StatusBarToolStripMenuItemClick(object sender, EventArgs e)
        {
            statusBar.Visible = !statusBar.Visible;
            statusBarToolStripMenuItem.Checked = statusBar.Visible;
            Settings.Default.ShowStatusBar = statusBar.Visible;
        }
    }
}
