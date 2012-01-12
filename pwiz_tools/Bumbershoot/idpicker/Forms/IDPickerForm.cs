//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
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
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Resources;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using CustomDataSourceDialog;
using DigitalRune.Windows.Docking;
using IDPicker.Forms;
using IDPicker.Controls;
using IDPicker.DataModel;
using pwiz.CLI.cv;
using pwiz.CLI.proteome;
using pwiz.CLI.util;
using seems;
using NHibernate;
using NHibernate.Linq;
using NHibernate.Criterion;
using BrightIdeasSoftware;
using PopupControl;
using Microsoft.WindowsAPICodePack.Taskbar;
using BreadCrumbControl = IDPicker.Controls.BreadCrumbControl;
using Protein = IDPicker.DataModel.Protein;
using SpectrumSource = IDPicker.DataModel.SpectrumSource;

//using SpyTools;

namespace IDPicker
{
    public partial class IDPickerForm : Form
    {
        BreadCrumbControl breadCrumbControl;

        ProteinTableForm proteinTableForm;
        PeptideTableForm peptideTableForm;
        SpectrumTableForm spectrumTableForm;
        ModificationTableForm modificationTableForm;
        AnalysisTableForm analysisTableForm;
        FragmentationStatisticsForm fragmentationStatisticsForm;
        PeakStatisticsForm peakStatisticsForm;

        LogForm logForm = null;
        //SpyEventLogForm spyEventLogForm;

        NHibernate.ISession session;

        Manager manager;
        LayoutManager _layoutManager;
        ProgressMonitor progressMonitor;

        private BasicFilterControl basicFilterControl;
        private Popup dataFilterPopup;
        private bool dirtyFilterControls = false;

        private DataFilter basicFilter, viewFilter;
        private IDictionary<Analysis, QonverterSettings> qonverterSettingsByAnalysis;

        string[] args;

        public IDPickerForm (string[] args)
        {
            InitializeComponent();

            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;

            this.args = args;

            manager = new Manager(dockPanel)
            {
                ShowChromatogramListForNewSources = false,
                ShowSpectrumListForNewSources = false,
                OpenFileUsesCurrentGraphForm = true,
            };

            progressMonitor = new ProgressMonitor();
            progressMonitor.ProgressUpdate += progressMonitor_ProgressUpdate;

            Shown += IDPickerForm_Load;

            basicFilterControl = new BasicFilterControl();
            basicFilterControl.BasicFilterChanged += basicFilterControl_BasicFilterChanged;
            basicFilterControl.ShowQonverterSettings += ShowQonverterSettings;
            dataFilterPopup = new Popup(basicFilterControl) { FocusOnOpen = true };
            dataFilterPopup.Closed += dataFilterPopup_Closed;

            breadCrumbControl = new BreadCrumbControl() { Dock = DockStyle.Fill };
            breadCrumbControl.BreadCrumbClicked += breadCrumbControl_BreadCrumbClicked;
            breadCrumbPanel.Controls.Add(breadCrumbControl);

            fragmentationStatisticsForm = new FragmentationStatisticsForm(this);
            fragmentationStatisticsForm.Show(dockPanel, DockState.DockBottomAutoHide);

            peakStatisticsForm = new PeakStatisticsForm(this);
            peakStatisticsForm.Show(dockPanel, DockState.DockBottomAutoHide);

            spectrumTableForm = new SpectrumTableForm();
            spectrumTableForm.Show(dockPanel, DockState.DockLeft);

            proteinTableForm = new ProteinTableForm();
            proteinTableForm.Show(dockPanel, DockState.DockTop);

            peptideTableForm = new PeptideTableForm();
            peptideTableForm.Show(proteinTableForm.Pane, DockPaneAlignment.Right, 0.7);

            modificationTableForm = new ModificationTableForm();
            modificationTableForm.Show(dockPanel, DockState.Document);

            analysisTableForm = new AnalysisTableForm();
            analysisTableForm.Show(dockPanel, DockState.Document);

            spectrumTableForm.SpectrumViewFilter += spectrumTableForm_SpectrumViewFilter;
            spectrumTableForm.SpectrumViewVisualize += spectrumTableForm_SpectrumViewVisualize;
            proteinTableForm.ProteinViewFilter += proteinTableForm_ProteinViewFilter;
            proteinTableForm.ProteinViewVisualize += proteinTableForm_ProteinViewVisualize;
            peptideTableForm.PeptideViewFilter += peptideTableForm_PeptideViewFilter;
            modificationTableForm.ModificationViewFilter += modificationTableForm_ModificationViewFilter;

            // hide DockPanel before initializing layout manager
            dockPanel.Visible = false;

            _layoutManager = new LayoutManager(this, peptideTableForm, proteinTableForm, spectrumTableForm, dockPanel);

            // load last or default location and size
            _layoutManager.LoadMainFormSettings();

            //logForm = new LogForm();
            //Console.SetOut(logForm.LogWriter);
            Console.SetOut(TextWriter.Null);

            /*spyEventLogForm = new SpyEventLogForm();
            spyEventLogForm.AddEventSpy(new EventSpy("proteinTableForm", proteinTableForm));
            spyEventLogForm.AddEventSpy(new EventSpy("peptideTableForm", peptideTableForm));
            spyEventLogForm.AddEventSpy(new EventSpy("spectrumTableForm", spectrumTableForm));
            spyEventLogForm.AddEventSpy(new EventSpy("modificationTableForm", modificationTableForm));
            spyEventLogForm.Show(dockPanel, DockState.DockBottomAutoHide);*/
        }

        void progressMonitor_ProgressUpdate (object sender, ProgressUpdateEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker) (() => progressMonitor_ProgressUpdate(sender, e)));
                return;
            }

            if (IsDisposed || toolStripProgressBar.IsDisposed)
            {
                e.Cancel = true;
                return;
            }

            toolStripStatusLabel.Text = e.Message;
            toolStripProgressBar.Visible = true;

            if (e.Total == 0)
                toolStripProgressBar.Style = ProgressBarStyle.Marquee;
            else
            {
                toolStripProgressBar.Style = ProgressBarStyle.Continuous;
                toolStripProgressBar.Maximum = e.Total;
                toolStripProgressBar.Value = e.Current;
            }

            if (TaskbarManager.IsPlatformSupported)
            {
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
                TaskbarManager.Instance.SetProgressValue(e.Current, e.Total);
            }

            Application.DoEvents();

            // TODO: add a cancel option: e.Cancel

            // if the work is done, schedule a delayed return to the "Ready" state
            if (e.Total == 0 || e.Total == e.Current)
            {
                var clearProgressInvoker = new BackgroundWorker();
                clearProgressInvoker.DoWork += delegate
                {
                    Thread.Sleep(2000);
                    clearProgress(e.Message);
                };
                clearProgressInvoker.RunWorkerAsync();
            }
        }

        void clearProgress (string messageToClear)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker) (() => clearProgress(messageToClear)));
                return;
            }

            if (toolStripStatusLabel.Text != messageToClear)
                return;

            toolStripStatusLabel.Text = "Ready";
            toolStripProgressBar.Visible = false;

            if (TaskbarManager.IsPlatformSupported)
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
        }

        void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> fileNames;
            TreeNode treeStructure = null;

            if (sender == openToolStripMenuItem)
            {
                var ofd = new OpenFileDialog
                              {
                                  Filter = "IDPicker DB|*.idpDB",
                                  Multiselect = true,
                                  SupportMultiDottedExtensions = true
                              };
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    fileNames = ofd.FileNames.ToList();
                    if (fileNames.Any())
                    {
                        var baseDirectory = Path.GetDirectoryName(fileNames[0]);
                        foreach (var file in fileNames)
                            if (Path.GetDirectoryName(file) != baseDirectory)
                                baseDirectory = null;
                        if (baseDirectory != null)
                            FolderHistoryInterface.AddFolderToHistory(baseDirectory);
                    }
                }
                else return;
            }
            else
            {
                var fileTypeList = new List<string>
                {
                    "IDPicker files|*.idpDB;*.mzid;*.pepXML;*.pep.xml",
                    "MzIdentML files|*.mzid",
                    "PepXML files|*.pepXML;*.pep.xml",
                    //"IDPicker XML|*.idpXML",
                    "IDPicker DB|*.idpDB",
                    "All files|*.*"
                };

                var openFileDialog = new IDPOpenDialog(fileTypeList);

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    fileNames = openFileDialog.GetFileNames().Distinct().ToList();
                    treeStructure = openFileDialog.GetTreeStructure();
                    if (!fileNames.Any())
                        return;
                }
                else return;
            }

            clearData();
            progressMonitor = new ProgressMonitor();
            progressMonitor.ProgressUpdate += progressMonitor_ProgressUpdate;

            basicFilterControl = new BasicFilterControl();
            basicFilterControl.BasicFilterChanged += basicFilterControl_BasicFilterChanged;
            basicFilterControl.ShowQonverterSettings += ShowQonverterSettings;
            dataFilterPopup = new Popup(basicFilterControl) {FocusOnOpen = true};
            dataFilterPopup.Closed += dataFilterPopup_Closed;

            breadCrumbControl.ClearBreadcrumbs();

            if (session != null)
            {
                if (sender == importToolStripMenuItem)
                {
                    //add current session to file list
                    var idpDbFilepath = session.Connection.GetDataSource();
                    if (!fileNames.Contains(idpDbFilepath))
                        fileNames.Add(idpDbFilepath);
                }
                clearSession();
            }
            //System.Data.SQLite.SQLiteConnection.ClearAllPools();

            new Thread(() => { OpenFiles(fileNames, treeStructure); }).Start();
        }

        #region Handling of events for spectrum/protein visualization
        Dictionary<GraphForm, bool> handlerIsAttached = new Dictionary<GraphForm, bool>();
        void spectrumTableForm_SpectrumViewVisualize (object sender, SpectrumViewVisualizeEventArgs e)
        {
            var spectrum = e.Spectrum;
            var source = e.SpectrumSource;

            string sourcePath;
            if (source.Metadata != null)
            {
                // accessing the Metadata property creates a temporary mzML file;
                // here we access the path to that file
                var tmpSourceFile = source.Metadata.fileDescription.sourceFiles.Last();
                sourcePath = Path.Combine(new Uri(tmpSourceFile.location).LocalPath, tmpSourceFile.name);
            }
            else
            {
                try
                {
                    sourcePath = Util.FindSourceInSearchPath(source.Name, ".");
                }
                catch
                {
                    try
                    {
                        // try the last looked-in path
                        sourcePath = Util.FindSourceInSearchPath(source.Name, Properties.GUI.Settings.Default.LastSpectrumSourceDirectory);
                    }
                    catch
                    {
                        // prompt user to find the source
                        var eventArgs = new SourceNotFoundEventArgs() { SourcePath = source.Name };
                        sourceNotFoundOnVisualizeHandler(this, eventArgs);

                        if (eventArgs.SourcePath == source.Name)
                            return; // user canceled

                        if (File.Exists(eventArgs.SourcePath) || Directory.Exists(eventArgs.SourcePath))
                        {
                            Properties.GUI.Settings.Default.LastSpectrumSourceDirectory = Path.GetDirectoryName(eventArgs.SourcePath);
                            Properties.GUI.Settings.Default.Save();
                            sourcePath = eventArgs.SourcePath;
                        }
                        else
                            throw; // file still not found, abort the visualization
                    }
                }
            }

            var param = e.Analysis.Parameters.Where(o => o.Name == "SpectrumListFilters").SingleOrDefault();
            string spectrumListFilters = param == null ? String.Empty : param.Value;
            spectrumListFilters = spectrumListFilters.Replace("0 ", "false ");

            var ionSeries = PeptideFragmentationAnnotation.IonSeries.Auto;
            if (sourcePath.ToLower().EndsWith(".mgf"))
                ionSeries = PeptideFragmentationAnnotation.IonSeries.b | PeptideFragmentationAnnotation.IonSeries.y;

            var annotation = new PeptideFragmentationAnnotation(e.ModifiedSequence, 1, Math.Max(1, e.Charge - 1),
                                                                ionSeries, true, false, true, false);

            (manager.SpectrumAnnotationForm.Controls[0] as ToolStrip).Hide();
            (manager.SpectrumAnnotationForm.Controls[1] as SplitContainer).Panel1Collapsed = true;
            (manager.SpectrumAnnotationForm.Controls[1] as SplitContainer).Dock = DockStyle.Fill;

            manager.OpenFile(sourcePath, spectrum.NativeID, annotation, spectrumListFilters);
            manager.CurrentGraphForm.Focus();

            if (!handlerIsAttached.ContainsKey(manager.CurrentGraphForm))
            {
                handlerIsAttached[manager.CurrentGraphForm] = true;
                manager.CurrentGraphForm.ZedGraphControl.PreviewKeyDown += new PreviewKeyDownEventHandler(CurrentGraphForm_PreviewKeyDown);
            }
        }

        void CurrentGraphForm_PreviewKeyDown (object sender, PreviewKeyDownEventArgs e)
        {
            var tlv = spectrumTableForm.TreeDataGridView;

            if (tlv.SelectedCells.Count != 1)
                return;

            int rowIndex = tlv.SelectedCells[0].RowIndex;
            int columnIndex = tlv.SelectedCells[0].ColumnIndex;

            var rowIndexHierarchy = new List<int>(tlv.GetRowHierarchyForRowIndex(rowIndex));
            if (!(spectrumTableForm.GetRowFromRowHierarchy(rowIndexHierarchy) is SpectrumTableForm.PeptideSpectrumMatchRow))
                return;

            var parentIndexHierachy = new List<int>(rowIndexHierarchy.Take(rowIndexHierarchy.Count-1));
            int siblingCount = parentIndexHierachy.Any() ? spectrumTableForm.GetRowFromRowHierarchy(parentIndexHierachy).ChildRows.Count : tlv.RowCount;
            bool previousRowIsPSM = rowIndexHierarchy.Last() > 0;
            bool nextRowIsPSM = rowIndexHierarchy.Last() + 1 < siblingCount;

            int key = (int) e.KeyCode;
            if ((key == (int) Keys.Left || key == (int) Keys.Up) && previousRowIsPSM)
            {
                tlv.SelectedCells[0].Selected = false;
                --rowIndexHierarchy[rowIndexHierarchy.Count - 1];
            }
            else if ((key == (int) Keys.Right || key == (int) Keys.Down) && nextRowIsPSM)
            {
                tlv.SelectedCells[0].Selected = false;
                ++rowIndexHierarchy[rowIndexHierarchy.Count - 1];
            }
            else
                return;

            tlv[columnIndex, rowIndexHierarchy].Selected = true;

            //tlv.EnsureVisible(tlv.SelectedIndex);

            var psmRow = spectrumTableForm.GetRowFromRowHierarchy(rowIndexHierarchy) as SpectrumTableForm.PeptideSpectrumMatchRow;

            spectrumTableForm_SpectrumViewVisualize(this, new SpectrumViewVisualizeEventArgs(psmRow));
        }

        void proteinTableForm_ProteinViewVisualize (object sender, ProteinViewVisualizeEventArgs e)
        {
            var formSession = session.SessionFactory.OpenSession();
            var form = new SequenceCoverageForm(formSession, e.Protein, viewFilter);
            form.Show(modificationTableForm.Pane, null);
            form.SequenceCoverageFilter += sequenceCoverageForm_SequenceCoverageFilter;
            form.FormClosed += (s, e2) => formSession.Dispose();
        }
        #endregion

        #region Handling of filter events from each view
        void proteinTableForm_ProteinViewFilter (ProteinTableForm sender, DataFilter proteinViewFilter)
        {
            var newFilter = proteinViewFilter;

            if (breadCrumbControl.BreadCrumbs.Count(o => (DataFilter) o.Tag == newFilter) > 0)
                return;

            breadCrumbControl.BreadCrumbs.Add(new BreadCrumb(newFilter.ToString(), newFilter));

            // build a new DataFilter from the BreadCrumb list
            viewFilter = basicFilter + breadCrumbControl.BreadCrumbs.Select(o => o.Tag as DataFilter).Aggregate((x, y) => x + y);
            setData();
        }

        void peptideTableForm_PeptideViewFilter (PeptideTableForm sender, DataFilter peptideViewFilter)
        {
            var newFilter = peptideViewFilter;

            if (breadCrumbControl.BreadCrumbs.Count(o => (DataFilter) o.Tag == newFilter) > 0)
                return;

            breadCrumbControl.BreadCrumbs.Add(new BreadCrumb(newFilter.ToString(), newFilter));

            // build a new DataFilter from the BreadCrumb list
            viewFilter = basicFilter + breadCrumbControl.BreadCrumbs.Select(o => o.Tag as DataFilter).Aggregate((x, y) => x + y);
            setData();
        }

        void spectrumTableForm_SpectrumViewFilter (object sender, DataFilter spectrumViewFilter)
        {
            var newFilter = spectrumViewFilter;

            if (breadCrumbControl.BreadCrumbs.Count(o => (DataFilter) o.Tag == newFilter) > 0)
                return;

            breadCrumbControl.BreadCrumbs.Add(new BreadCrumb(newFilter.ToString(), newFilter));

            // build a new DataFilter from the BreadCrumb list
            viewFilter = basicFilter + breadCrumbControl.BreadCrumbs.Select(o => o.Tag as DataFilter).Aggregate((x, y) => x + y);
            setData();
        }

        void modificationTableForm_ModificationViewFilter (ModificationTableForm sender, DataFilter modificationViewFilter)
        {
            var newFilter = modificationViewFilter;

            if (breadCrumbControl.BreadCrumbs.Count(o => (DataFilter) o.Tag == newFilter) > 0)
                return;

            breadCrumbControl.BreadCrumbs.Add(new BreadCrumb(newFilter.ToString(), newFilter));

            // build a new DataFilter from the BreadCrumb list
            viewFilter = basicFilter + breadCrumbControl.BreadCrumbs.Select(o => o.Tag as DataFilter).Aggregate((x, y) => x + y);
            setData();
        }

        void sequenceCoverageForm_SequenceCoverageFilter (SequenceCoverageControl sender, DataFilter sequenceCoverageFilter)
        {
            var newFilter = sequenceCoverageFilter;

            if (breadCrumbControl.BreadCrumbs.Count(o => (DataFilter) o.Tag == newFilter) > 0)
                return;

            breadCrumbControl.BreadCrumbs.Add(new BreadCrumb(newFilter.ToString(), newFilter));

            // build a new DataFilter from the BreadCrumb list
            viewFilter = basicFilter + breadCrumbControl.BreadCrumbs.Select(o => o.Tag as DataFilter).Aggregate((x, y) => x + y);
            setData();
        }
        #endregion

        void breadCrumbControl_BreadCrumbClicked (object sender, BreadCrumbClickedEventArgs e)
        {
            breadCrumbControl.BreadCrumbs.Remove(e.BreadCrumb);

            // start with the basic filter
            viewFilter = basicFilter;

            // create the view filter from the BreadCrumb list
            if (breadCrumbControl.BreadCrumbs.Count > 0)
                viewFilter += breadCrumbControl.BreadCrumbs.Select(o => o.Tag as DataFilter).Aggregate((x, y) => x + y);
            setData();
        }

        public void ApplyBasicFilter ()
        {
            clearData();

            toolStripStatusLabel.Text = "Applying basic filters...";
            basicFilter.FilteringProgress += progressMonitor.UpdateProgress;

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += applyBasicFilterAsync;

            workerThread.RunWorkerCompleted += (s, e) =>
            {
                if (e.Result is Exception)
                    Program.HandleException(e.Result as Exception);

                basicFilter.FilteringProgress -= progressMonitor.UpdateProgress;

                // start with the basic filter
                viewFilter = basicFilter;

                // create the view filter from the BreadCrumb list
                if (breadCrumbControl.BreadCrumbs.Count > 0)
                    viewFilter += breadCrumbControl.BreadCrumbs.Select(o => o.Tag as DataFilter).Aggregate((x, y) => x + y);
                setData();
            };

            workerThread.RunWorkerAsync();
        }

        void applyBasicFilterAsync (object sender, DoWorkEventArgs e)
        {
            try
            {
                lock (session)
                    basicFilter.ApplyBasicFilters(session);
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        void clearData ()
        {
            if (proteinTableForm != null) proteinTableForm.ClearData(true);
            if (peptideTableForm != null) peptideTableForm.ClearData(true);
            if (spectrumTableForm != null) spectrumTableForm.ClearData(true);
            if (modificationTableForm != null) modificationTableForm.ClearData(true);
            if (analysisTableForm != null) analysisTableForm.ClearData(true);
            fragmentationStatisticsForm.ClearData(true);
            peakStatisticsForm.ClearData(true);
            dockPanel.Contents.OfType<SequenceCoverageForm>().ForEach(o => o.ClearData());
        }

        void setData ()
        {
            proteinTableForm.SetData(session, viewFilter);
            peptideTableForm.SetData(session.SessionFactory.OpenSession(), viewFilter);
            spectrumTableForm.SetData(session.SessionFactory.OpenSession(), viewFilter);
            modificationTableForm.SetData(session.SessionFactory.OpenSession(), viewFilter);
            analysisTableForm.SetData(session.SessionFactory.OpenSession(), viewFilter);
            fragmentationStatisticsForm.SetData(session, viewFilter);
            peakStatisticsForm.SetData(session, viewFilter);
            dockPanel.Contents.OfType<SequenceCoverageForm>().ForEach(o => o.SetData(session, viewFilter));
        }

        void clearSession()
        {
            proteinTableForm.ClearSession();
            peptideTableForm.ClearSession();
            spectrumTableForm.ClearSession();
            modificationTableForm.ClearSession();
            analysisTableForm.ClearSession();
            fragmentationStatisticsForm.ClearSession();
            peakStatisticsForm.ClearSession();
            dockPanel.Contents.OfType<SequenceCoverageForm>().ForEach(o => o.ClearSession());

            if (session != null)
            {
                var factory = session.SessionFactory;
                if (session.IsOpen)
                    session.Dispose();
                session = null;
                if (!factory.IsClosed)
                    factory.Dispose();
            }
        }

        void IDPickerForm_Load (object sender, EventArgs e)
        {
            //System.Data.SQLite.SQLiteConnection.SetConfigOption(SQLiteConnection.SQLITE_CONFIG.MULTITHREAD);
            //var filepaths = Directory.GetFiles(@"c:\test\Goldenring_gastric\Metaplasia", "klc*FFPE*.pepXML", SearchOption.AllDirectories);
            //OpenFiles(filepaths);//.Take(10).Union(filepaths.Skip(200).Take(10)).Union(filepaths.Skip(400).Take(10)).ToList());
            //return;

            checkForUpdatesAutomaticallyToolStripMenuItem.Checked = Properties.GUI.Settings.Default.AutomaticCheckForUpdates;

            //Get user layout profiles
            _layoutManager.CurrentLayout = _layoutManager.GetCurrentDefault();

            if (args != null && args.Length > 0 && args.All(o => File.Exists(o)))
            {
                new Thread(() => { OpenFiles(args, null); }).Start();
            }
        }

        void sourceNotFoundOnVisualizeHandler (object sender, SourceNotFoundEventArgs e)
        {
            if (String.IsNullOrEmpty(e.SourcePath))
                return;

            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => sourceNotFoundOnVisualizeHandler(sender, e)));
                return;
            }

            var findDirectoryDialog = new FolderBrowserDialog()
            {
                SelectedPath = Properties.GUI.Settings.Default.LastSpectrumSourceDirectory,
                ShowNewFolderButton = false,
                Description = "Locate the directory containing the source \"" + e.SourcePath + "\""
            };

            while (findDirectoryDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    e.SourcePath = Util.FindSourceInSearchPath(e.SourcePath, findDirectoryDialog.SelectedPath);
                    Properties.GUI.Settings.Default.LastSpectrumSourceDirectory = findDirectoryDialog.SelectedPath;
                    Properties.GUI.Settings.Default.Save();
                    return;
                }
                catch
                {
                    // couldn't find the source in that directory; prompt user again
                }
            }
        }

        void OpenFiles (IList<string> filepaths, TreeNode rootNode)
        {
            try
            {
                var xml_filepaths = filepaths.Where(filepath => !filepath.EndsWith(".idpDB"));
                var idpDB_filepaths = filepaths.Where(filepath => filepath.EndsWith(".idpDB"));

                if (xml_filepaths.Count() + idpDB_filepaths.Count() == 0)
                {
                    MessageBox.Show("Select one or more idpDB, mzIdentML, pepXML, or idpXML files to create an IDPicker report.", "No IDPicker files selected");
                    return;
                }

                // warn if idpDBs already exist
                bool warnOnce = false;
                foreach (string filepath in xml_filepaths)
                {
                    string idpDB_filepath = Path.ChangeExtension(filepath, ".idpDB");
                    if (File.Exists(idpDB_filepath))
                    {
                        if (!warnOnce && MessageBox.Show("Some of these files have already been converted. Do you want to reconvert them?",
                                                         "Result already converted",
                                                         MessageBoxButtons.YesNo,
                                                         MessageBoxIcon.Exclamation,
                                                         MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                            return;
                        warnOnce = true;
                        File.Delete(idpDB_filepath);
                    }
                }

                // determine if merged filepath exists and that it's a valid idpDB
                string commonFilename = Util.GetCommonFilename(filepaths);
                bool workToBeDone = filepaths.Count > 1 || (filepaths.Count > 0 && !filepaths[0].ToLower().EndsWith(".idpdb"));
                bool fileExists = File.Exists(commonFilename);
                bool fileIsValid = fileExists && SessionFactoryFactory.IsValidFile(commonFilename);
                var potentialPaths = filepaths.Select(item =>
                                                      Path.Combine(Path.GetDirectoryName(item) ?? string.Empty,
                                                                   Path.GetFileNameWithoutExtension(item) ??
                                                                   string.Empty) + ".idpDB").ToList();

                if ((fileExists ||
                    potentialPaths.Contains(commonFilename)) && filepaths.Count > 1 && workToBeDone)
                {
                    if(filepaths.Contains(commonFilename))
                    {
                        bool cancel = false;
                        MessageBox.Show("File list contains the default output name, please select a new name.");
                        Invoke(new MethodInvoker(() =>
                        {
                            var sfd = new SaveFileDialog
                            {
                                AddExtension = true,
                                RestoreDirectory = true,
                                DefaultExt = "idpDB",
                                Filter = "IDPicker Database|*.idpDB"
                            };
                            if (sfd.ShowDialog() != DialogResult.OK)
                            {
                                cancel = true;
                                return;
                            }
                            commonFilename = sfd.FileName;
                        }));
                        if (cancel)
                            return;
                    }
                    else
                    {
                        switch (
                            MessageBox.Show(
                                string.Format(
                                    "The merged result \"{0}\" already exists, or will exist after processing files. " +
                                    "Do you want to overwrite it?{1}{1}" +
                                    "Click 'Yes' to overwrite file{1}" +
                                    "Click 'No' to merge to a different file{1}" +
                                    "Click 'Cancel' to abort"
                                    , commonFilename, Environment.NewLine),
                                "Merged result already exists",
                                MessageBoxButtons.YesNoCancel,
                                MessageBoxIcon.Exclamation,
                                MessageBoxDefaultButton.Button2))
                        {
                            case DialogResult.Yes:
                                if (!potentialPaths.Contains(commonFilename) && fileExists)
                                {
                                        File.Delete(commonFilename);
                                }
                                break;
                            case DialogResult.No:
                                bool cancel = false;
                                Invoke(new MethodInvoker(() =>
                                                             {
                                                                 var sfd = new SaveFileDialog
                                                                               {
                                                                                   AddExtension = true,
                                                                                   RestoreDirectory = true,
                                                                                   DefaultExt = "idpDB",
                                                                                   Filter = "IDPicker Database|*.idpDB"
                                                                               };
                                                                 if (sfd.ShowDialog() != DialogResult.OK)
                                                                 {
                                                                     cancel = true;
                                                                     return;
                                                                 }
                                                                 commonFilename = sfd.FileName;
                                                             }));
                                if (cancel)
                                    return;
                                break;
                            case DialogResult.Cancel:
                                return;
                        }
                    }
                }

                // determine if merged filepath can be written, get new path if not
                while (true)
                {
                    DirectoryInfo directoryInfo = null;
                    var possibleDirectory = Path.GetDirectoryName(commonFilename);
                    if (possibleDirectory != null && Directory.Exists(possibleDirectory))
                        directoryInfo = new DirectoryInfo(possibleDirectory);

                    if (directoryInfo == null)
                        MessageBox.Show("Automatic output folder cannot be found, please specify a new output name and location.");
                    else
                    {
                        try
                        {
                            string randomTestFile = Path.GetRandomFileName();
                            using (var tmp = File.Create(randomTestFile)) { }
                            File.Delete(randomTestFile);
                            break;
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Output folder is read-only, please specify a new output name and location.");
                        }
                    }

                    bool cancel = false;
                    Invoke(new MethodInvoker(() =>
                    {
                        var sfd = new SaveFileDialog
                                   {
                                       AddExtension = true,
                                       RestoreDirectory = true,
                                       DefaultExt = "idpDB",
                                       Filter = "IDPicker Database|*.idpDB"
                                   };
                        if (sfd.ShowDialog() != DialogResult.OK)
                        {
                            cancel = true;
                            return;
                        }
                        commonFilename = sfd.FileName;
                    }));

                    if (cancel)
                        return;
                }
                //Environment.CurrentDirectory = possibleDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                // set main window title
                BeginInvoke(new MethodInvoker(() => Text = commonFilename));

                if (xml_filepaths.Count() > 0)
                {
                    importCancelled = false;

                    Parser parser = new Parser();
                    Invoke(new MethodInvoker(() => parser.ImportSettings += importSettingsHandler));

                    var ilr = new IterationListenerRegistry();

                    var progressForm = new ProgressForm(xml_filepaths, ilr)
                    {
                        Text = "Import Progress",
                        StartPosition = FormStartPosition.CenterParent
                    };

                    Invoke(new MethodInvoker(() => progressForm.Show(this)));

                    parser.Parse(xml_filepaths, ilr);

                    importCancelled |= progressForm.Cancelled;

                    Invoke(new MethodInvoker(() => progressForm.Close()));

                    if (importCancelled)
                        return;

                    idpDB_filepaths = idpDB_filepaths.Union(xml_filepaths.Select(o => Path.ChangeExtension(o, ".idpDB")));
                }

                if (idpDB_filepaths.Count() > 1)
                {
                    var merger = new Merger(commonFilename, idpDB_filepaths);
                    toolStripStatusLabel.Text = "Merging results...";
                    merger.MergingProgress += progressMonitor.UpdateProgress;
                    merger.Start();

                    idpDB_filepaths = new List<string>() {commonFilename};
                }

                // if the database can fit in the available RAM, populate the disk cache
                long ramBytesAvailable = (long) new System.Diagnostics.PerformanceCounter("Memory", "Available Bytes").NextValue();
                if (ramBytesAvailable > new FileInfo(commonFilename).Length)
                {
                    toolStripStatusLabel.Text = "Precaching idpDB...";
                    using (var fs = new FileStream(commonFilename, FileMode.Open, FileSystemRights.ReadData, FileShare.ReadWrite, (1 << 15), FileOptions.SequentialScan))
                    {
                        var buffer = new byte[UInt16.MaxValue];
                        while (fs.Read(buffer, 0, UInt16.MaxValue) > 0) { }
                    }
                }

                BeginInvoke(new MethodInvoker(() =>
                {
                    var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(commonFilename, new SessionFactoryConfig { WriteSqlToConsoleOut = true });

                    // reload qonverter settings because the ids may change after merging
                    session = sessionFactory.OpenSession();
                    qonverterSettingsByAnalysis = session.Query<QonverterSettings>().ToDictionary(o => session.Get<Analysis>(o.Id));

                    session.CreateSQLQuery("PRAGMA temp_store=MEMORY").ExecuteUpdate();

                    _layoutManager.SetSession(session);

                    //set or save default layout
                    dockPanel.Visible = true;
                    _layoutManager.CurrentLayout = _layoutManager.GetCurrentDefault();

                    toolStripStatusLabel.Text = "Refreshing group structure...";
                    var usedGroups = GroupingControlForm.SetStructure(rootNode, new List<SpectrumSourceGroup>(), session);
                    if (usedGroups != null && usedGroups.Any())
                    {
                        var unusedGroups = session.QueryOver<SpectrumSourceGroup>().List();
                        foreach (var item in usedGroups)
                            unusedGroups.Remove(item);
                        foreach (var item in unusedGroups)
                            session.Delete(item);
                    }
                    session.Flush();

                    //breadCrumbControl.BreadCrumbs.Clear();

                    basicFilter = DataFilter.LoadFilter(session);

                    if (basicFilter == null)
                    {
                        basicFilter = new DataFilter()
                        {
                            MaximumQValue = 0.02,
                            MinimumDistinctPeptidesPerProtein = 2,
                            MinimumSpectraPerProtein = 2,
                            MinimumAdditionalPeptidesPerProtein = 1
                        };

                        basicFilterControl.DataFilter = basicFilter;

                        viewFilter = basicFilter;

                        ApplyBasicFilter();
                    }
                    else
                    {
                        basicFilterControl.DataFilter = basicFilter;

                        viewFilter = basicFilter;

                        setData();
                    }

                    toolStripStatusLabel.Text = "Ready";

                    if (logForm != null) logForm.Show(dockPanel, DockState.DockBottomAutoHide);
                }));
            }
            catch (Exception ex)
            {
                Program.HandleException(ex);
            }
        }

        internal void LoadLayout(LayoutProperty userLayout)
        {
            if (userLayout == null)
                return;

            var tempFilepath = Path.GetTempFileName();
            using (var tempFile = new StreamWriter(tempFilepath, false, Encoding.Unicode))
                tempFile.Write(userLayout.PaneLocations);

            dockPanel.SuspendLayout();
            dockPanel.LoadFromXml(tempFilepath, DeserializeForm);
            dockPanel.ResumeLayout(true, true);
            File.Delete(tempFilepath);

            if (userLayout.HasCustomColumnSettings &&
                proteinTableForm != null &&
                peptideTableForm != null &&
                spectrumTableForm != null)
            {
                proteinTableForm.LoadLayout(userLayout.FormProperties["ProteinTableForm"]);
                peptideTableForm.LoadLayout(userLayout.FormProperties["PeptideTableForm"]);
                spectrumTableForm.LoadLayout(userLayout.FormProperties["SpectrumTableForm"]);
            }
        }

        private IDockableForm DeserializeForm(string persistantString)
        {
            if (persistantString == typeof(ProteinTableForm).ToString())
                return proteinTableForm;
            if (persistantString == typeof(PeptideTableForm).ToString())
                return peptideTableForm;
            if (persistantString == typeof(SpectrumTableForm).ToString())
                return spectrumTableForm;
            if (persistantString == typeof(ModificationTableForm).ToString())
                return modificationTableForm;
            if (persistantString == typeof(AnalysisTableForm).ToString())
                return analysisTableForm;
            
            return null;
        }

        bool importCancelled = false;
        void importSettingsHandler (object sender, Parser.ImportSettingsEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => importSettingsHandler(sender, e)));
                return;
            }

            var result = UserDialog.Show(this, "Import Settings", new ImportSettingsControl(e.DistinctAnalyses, showQonverterSettingsManager));

            if (e.DistinctAnalyses.Any(o => String.IsNullOrEmpty(o.importSettings.qonverterSettings.DecoyPrefix)))
            {
                MessageBox.Show("Decoy prefix cannot be empty.", "Error");
                result = DialogResult.Cancel;
            }

            importCancelled = e.Cancel = result == DialogResult.Cancel;
        }

        IDictionary<Analysis, QonverterSettings> qonverterSettingsHandler (IList<Analysis> analyses, out bool cancel)
        {
            qonverterSettingsByAnalysis = new Dictionary<Analysis, QonverterSettings>();
            analyses.ForEach(o => qonverterSettingsByAnalysis.Add(o, null));
            var result = UserDialog.Show(this, "Qonverter Settings", new QonverterSettingsByAnalysisControl(qonverterSettingsByAnalysis, showQonverterSettingsManager));
            cancel = result == DialogResult.Cancel;
            return qonverterSettingsByAnalysis;
        }

        void showQonverterSettingsManager ()
        {
            new QonverterSettingsManagerForm().ShowDialog(this);
        }

        public void ReloadSession(NHibernate.ISession ses)
        {
            IList<string> database = new List<string>();
            database.Add((from System.Text.RegularExpressions.Match m 
                              in (new System.Text.RegularExpressions.Regex(@"\w:(?:\\(?:\w| |_|-)+)+.idpDB"))
                              .Matches(ses.Connection.ConnectionString) select m.Value).SingleOrDefault<string>());
            if (File.Exists(database[0]))
                OpenFiles(database, null);
        }

        private void IDPickerForm_FormClosing (object sender, FormClosingEventArgs e)
        {
            if (_layoutManager != null)
            {
                _layoutManager.SaveMainFormSettings();
                _layoutManager.SaveUserLayoutList();
            }
        }


        private void layoutButton_Click (object sender, EventArgs e)
        {
            layoutToolStripMenuRoot.DropDownItems.Clear();
            if (dockPanel.Visible)
            {
                var items = _layoutManager.LoadLayoutMenu();
                foreach (var item in items)
                    layoutToolStripMenuRoot.DropDownItems.Add(item);
            }
        }

        private void dataFilterButton_Click (object sender, EventArgs e)
        {
            if (session == null)
                return;

            if (!dataFilterPopup.Visible)
                dataFilterPopup.Show(new Point(Location.X + 141, Location.Y + 50));
            else
                dataFilterPopup.Visible = false;
        }

        private void basicFilterControl_BasicFilterChanged (object sender, EventArgs e)
        {
            dirtyFilterControls = basicFilter != basicFilterControl.DataFilter;
        }

        void dataFilterPopup_Closed (object sender, ToolStripDropDownClosedEventArgs e)
        {
            if (dirtyFilterControls)
            {
                dirtyFilterControls = false;
                basicFilter = basicFilterControl.DataFilter;
                ApplyBasicFilter();
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
    }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var QOptions = new QonverterSettingsManagerForm();
            QOptions.ShowDialog();
        }

        private void ShowQonverterSettings(object sender, EventArgs e)
        {
            if (session == null)
                return;

            var databaseAnalysis = session.QueryOver<Analysis>().List();

            bool cancel;
            var qonverterSettings = qonverterSettingsHandler(databaseAnalysis, out cancel);
            if (cancel)
                return;

            var qonverter = new Qonverter();
            qonverter.QonversionProgress += progressMonitor.UpdateProgress;
            qonverterSettingsByAnalysis = session.Query<QonverterSettings>().ToDictionary(o => session.Get<Analysis>(o.Id));
            foreach (var item in qonverterSettings)
            {
                // TODO: move updating of QonverterSettings to native Qonverter?
                qonverter.SettingsByAnalysis[(int)item.Key.Id] = item.Value.ToQonverterSettings();
                qonverterSettingsByAnalysis[item.Key].DecoyPrefix = item.Value.DecoyPrefix;
                qonverterSettingsByAnalysis[item.Key].ScoreInfoByName = item.Value.ScoreInfoByName;
                qonverterSettingsByAnalysis[item.Key].QonverterMethod = item.Value.QonverterMethod;
                qonverterSettingsByAnalysis[item.Key].Kernel = item.Value.Kernel;
                qonverterSettingsByAnalysis[item.Key].MassErrorHandling = item.Value.MassErrorHandling;
                qonverterSettingsByAnalysis[item.Key].MissedCleavagesHandling = item.Value.MissedCleavagesHandling;
                qonverterSettingsByAnalysis[item.Key].TerminalSpecificityHandling = item.Value.TerminalSpecificityHandling;
                qonverterSettingsByAnalysis[item.Key].ChargeStateHandling = item.Value.ChargeStateHandling;
                qonverterSettingsByAnalysis[item.Key].RerankMatches = item.Value.RerankMatches;
                session.Save(qonverterSettingsByAnalysis[item.Key]);
            }
            session.Flush();
            session.Close();

            //qonverter.LogQonversionDetails = true;
            try
            {
                qonverter.Reset(Text);
                qonverter.Qonvert(Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Qonversion failed");
            }

            var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(Text, new SessionFactoryConfig { WriteSqlToConsoleOut = true });
            session = sessionFactory.OpenSession();
            //session.CreateSQLQuery("PRAGMA temp_store=MEMORY").ExecuteUpdate();
            _layoutManager.SetSession(session);

            basicFilter = new DataFilter()
                              {
                                  MaximumQValue = 0.02,
                                  MinimumDistinctPeptidesPerProtein = 2,
                                  MinimumSpectraPerProtein = 2,
                                  MinimumAdditionalPeptidesPerProtein = 1
                              };

            basicFilterControl.DataFilter = basicFilter;

            viewFilter = basicFilter;

            viewFilter.ApplyBasicFilters(session);

            session.Close();
            sessionFactory.Close();

            clearData();
            progressMonitor = new ProgressMonitor();
            progressMonitor.ProgressUpdate += progressMonitor_ProgressUpdate;
            basicFilterControl = new BasicFilterControl();
            basicFilterControl.BasicFilterChanged += basicFilterControl_BasicFilterChanged;
            basicFilterControl.ShowQonverterSettings += ShowQonverterSettings;
            dataFilterPopup = new Popup(basicFilterControl) { FocusOnOpen = true };
            dataFilterPopup.Closed += dataFilterPopup_Closed;
            OpenFiles(new List<string> {Text}, null);
        }

        private void toExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (session == null)
            {
                MessageBox.Show("No Report Loaded");
                return;
            }
            var selected = sender == toExcelSelectToolStripMenuItem;

			 var progressWindow = new Form
                                     {
                                         Size = new Size(300, 60),
                                         Text = "Generating Excel Report pages (1 of 6)",
                                         StartPosition = FormStartPosition.CenterScreen,
                                         ControlBox = false
                                     };
            var progressBar = new ProgressBar
                                  {
                                      Dock = DockStyle.Fill,
                                      Style = ProgressBarStyle.Marquee
                                  };
            progressWindow.Controls.Add(progressBar);
            progressWindow.Show();

            var bg = new BackgroundWorker { WorkerReportsProgress = true };
            bg.ProgressChanged += (x, y) =>
                                      {
                                          switch (y.ProgressPercentage)
                                          {
                                              case 1:
                                                  progressWindow.Text = "Generating Excel Report pages (2 of 6)";
                                                  break;
                                              case 2:
                                                  progressWindow.Text = "Generating Excel Report pages (3 of 6)";
                                                  break;
                                              case 3:
                                                  progressWindow.Text = "Generating Excel Report pages (4 of 6)";
                                                  break;
                                              case 4:
                                                  progressWindow.Text = "Generating Excel Report pages (5 of 6)";
                                                  break;
                                              case 5:
                                                  progressWindow.Text = "Generating Excel Report pages (6 of 6)";
                                                  break;
                                              default:
                                                  break;
                                          }
                                      };
            bg.RunWorkerCompleted += (x, y) =>
                                         {
                                             if (y.Error != null) Program.HandleException(y.Error);
                                             progressWindow.Close();
                                         };
            bg.DoWork += (x, y) =>
                             {
                                 var reportDictionary = new Dictionary<string, List<List<string>>>();

                                 if (modificationTableForm != null)
                                 {
                                     var table = modificationTableForm.GetFormTable(selected);
                                     if (table.Count > 1)
                                         reportDictionary.Add("Modification Table", table);
                                 }
                                 bg.ReportProgress(1);
                                 if (proteinTableForm != null)
                                 {
                                     var table = proteinTableForm.GetFormTable(selected);
                                     if (table.Count > 1)
                                         reportDictionary.Add("Protein Table", table);
                                 }
                                 bg.ReportProgress(2);
                                 if (peptideTableForm != null)
                                 {
                                     var table = peptideTableForm.GetFormTable(selected);
                                     if (table.Count > 1)
                                         reportDictionary.Add("Peptide Table", table);
                                 }
                                 bg.ReportProgress(3);
                                 if (spectrumTableForm != null)
                                 {
                                     var table = spectrumTableForm.GetFormTable(selected);
                                     if (table.Count > 1)
                                         reportDictionary.Add("Spectrum Table", table);
                                 }
                                 bg.ReportProgress(4);
                                 if (analysisTableForm != null)
                                 {
                                     var table = analysisTableForm.GetFormTable(selected);
                                     if (table.Count > 1)
                                         reportDictionary.Add("Analysis Settings", table);
                                 }
                                 bg.ReportProgress(5);
                                 var summaryList = getSummaryList();
                                 if (summaryList.Count > 0)
                                     reportDictionary.Add("Summary", summaryList);


                                 if (reportDictionary.Count > 0)
                                     TableExporter.ShowInExcel(reportDictionary, false);
                                 else
                                     MessageBox.Show("Could not gather report information");
                             };

            bg.RunWorkerAsync();
        }

        private List<List<string>> getSummaryList()
        {
            var summaryList = new List<List<string>>();

            if (modificationTableForm != null)
            {
                var modMatches = Regex.Matches(modificationTableForm.Text.ToLower(), @"\d* mod");
                if (modMatches.Count > 0)
                {
                    var modNumber = modMatches[0].ToString().TrimEnd(" mod".ToCharArray());
                    summaryList.Add(new List<string> { "Modifications", modNumber });
                }
            }
            if (proteinTableForm != null)
            {
                var proMatches = Regex.Matches(proteinTableForm.Text.ToLower(), @"\d* protein groups");
                if (proMatches.Count > 0)
                {
                    var proGroupNumber = proMatches[0].ToString().TrimEnd(" protein groups".ToCharArray());
                    summaryList.Add(new List<string> { "Protein Groups", proGroupNumber });
                }
                proMatches = Regex.Matches(proteinTableForm.Text.ToLower(), @"\d* proteins");
                if (proMatches.Count > 0)
                {
                    var proNumber = proMatches[0].ToString().TrimEnd(" proteins".ToCharArray());
                    summaryList.Add(new List<string> { "Proteins", proNumber });
                }
            }
            if (peptideTableForm != null)
            {
                var pepMatches = Regex.Matches(peptideTableForm.Text.ToLower(), @"\d* distinct peptides");
                if (pepMatches.Count > 0)
                {
                    var pepNumber = pepMatches[0].ToString().TrimEnd(" distinct peptides".ToCharArray());
                    summaryList.Add(new List<string> { "Distinct Peptides", pepNumber });
                }
                pepMatches = Regex.Matches(peptideTableForm.Text.ToLower(), @"\d* distinct matches");
                if (pepMatches.Count > 0)
                {
                    var matchNumber = pepMatches[0].ToString().TrimEnd(" distinct matches".ToCharArray());
                    summaryList.Add(new List<string> { "Distinct Peptide Matches", matchNumber });
                }
            }
            if (spectrumTableForm != null)
            {
                var matches = Regex.Matches(spectrumTableForm.Text.ToLower(), @"\d* spectra");
                if (matches.Count > 0)
                {
                    var matchNumber = matches[0].ToString().TrimEnd(" spectra".ToCharArray());
                    summaryList.Add(new List<string> { "Spectra", matchNumber });
                }
                matches = Regex.Matches(spectrumTableForm.Text.ToLower(), @"\d* sources");
                if (matches.Count > 0)
                {
                    var matchNumber = matches[0].ToString().TrimEnd(" sources".ToCharArray());
                    summaryList.Add(new List<string> { "Sources", matchNumber });
                }
                matches = Regex.Matches(spectrumTableForm.Text.ToLower(), @"\d* groups");
                if (matches.Count > 0)
                {
                    var pepNumber = matches[0].ToString().TrimEnd(" groups".ToCharArray());
                    summaryList.Add(new List<string> { "Groups", pepNumber });
                }
            }

            //Summary page
            summaryList.Reverse();

            //Summary Filters
            var filterInfo = new List<List<string>>();
            if (basicFilter != null)
            {
                filterInfo.Add(new List<string> { "Max Q Value %", (viewFilter.MaximumQValue * 100).ToString() });
                filterInfo.Add(new List<string> { "Min Distinct Peptides", viewFilter.MinimumDistinctPeptidesPerProtein.ToString() });
                filterInfo.Add(new List<string>
                                   {
                                       "Min Additional Peptides",
                                       viewFilter.MinimumAdditionalPeptidesPerProtein.ToString()
                                   });
                filterInfo.Add(new List<string> { "Min Spectra", viewFilter.MinimumSpectraPerProtein.ToString() });

                #region In-depth filters

                if (viewFilter.SpectrumSourceGroup != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.SpectrumSourceGroup)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Spectrum Source Group", item.Name });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.Name });
                    }
                }
                if (viewFilter.SpectrumSource != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.SpectrumSource)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Spectrum Source", item.Name });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.Name });
                    }
                }
                if (viewFilter.Spectrum != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.Spectrum)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Spectrum", item.NativeID });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.NativeID });
                    }
                }
                if (viewFilter.Charge != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.Charge)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Charge", item.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.ToString() });
                    }
                }
                if (viewFilter.PeptideGroup != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.PeptideGroup)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Peptide Group", item.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.ToString() });
                    }
                }
                if (viewFilter.Peptide != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.Peptide)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Peptide", item.Sequence });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.Sequence });
                    }
                }
                if (viewFilter.DistinctMatchKey != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.DistinctMatchKey)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Distinct Match", item.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.ToString() });
                    }
                }
                if (viewFilter.ModifiedSite != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.ModifiedSite)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Modified Site", item.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.ToString() });
                    }
                }
                if (viewFilter.Modifications != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.Modifications)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Modifications", item.Name });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.Name });
                    }
                }
                if (viewFilter.ProteinGroup != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.ProteinGroup)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Protein Group", item.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.ToString() });
                    }
                }
                if (viewFilter.Protein != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.Protein)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Protein", item.Accession });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.Accession });
                    }
                }
                if (viewFilter.Cluster != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.Cluster)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Cluster", item.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.ToString() });
                    }
                }

                #endregion
            }

            if (filterInfo.Count > 0)
            {
                summaryList.Add(new List<string> { string.Empty });
                summaryList.Add(new List<string> { string.Empty });
                summaryList.Add(new List<string> { " --- Filters --- " });
                summaryList.AddRange(filterInfo);
            }
            return summaryList;
        }

        private void toHTMLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (session == null)
            {
                MessageBox.Show("No Report Loaded");
                return;
            }

            string outFolder;
            var fbd = new FolderBrowserDialog() { Description = "Select destination folder" };
            if (fbd.ShowDialog() == DialogResult.OK)
                outFolder = fbd.SelectedPath;
            else return;
            var textDialog = new TextInputPrompt("Report folder name", false, Path.GetFileNameWithoutExtension(Text));
            while (true)
            {
                if (textDialog.ShowDialog() == DialogResult.OK)
                {
                    var result = textDialog.GetText();
                    if (Directory.Exists(Path.Combine(outFolder, result)))
                    {
                        var response = MessageBox.Show("Report folder path already exists, overwrite?",
                                                       "Overwrite path?", MessageBoxButtons.YesNoCancel);
                        if (response == DialogResult.Yes)
                        {
                            outFolder = Path.Combine(outFolder, result);
                            var di = new DirectoryInfo(outFolder);
                            try
                            {
                                foreach (var file in di.GetFiles())
                                    File.Delete(file.FullName);
                            }
                            catch
                            {
                                MessageBox.Show("Could not overwrite. Please enter a new name" +
                                                " or make sure the report is closed and try again.");
                                continue;
                            }

                            break;
                        }
                        if (response == DialogResult.Cancel) return;
                    }
                    else
                    {
                        outFolder = Path.Combine(outFolder, result);
                        break;
                    }

                }
                else return;
            }

            var progressWindow = new Form
            {
                Size = new Size(300, 60),
                Text = "Generating HTML Report",
                StartPosition = FormStartPosition.CenterScreen,
                ControlBox = false
            };
            var progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee
            };
            progressWindow.Controls.Add(progressBar);
            progressWindow.Show();

            var bg = new BackgroundWorker { WorkerReportsProgress = true };
            bg.DoWork += delegate { CreateHtmlReport(bg, outFolder); };
            bg.ProgressChanged += delegate { progressWindow.Close(); };
            bg.RunWorkerCompleted += (x, y) => { if (y.Error != null) Program.HandleException(y.Error); };
            bg.RunWorkerAsync();
        }

        private void CreateHtmlReport(BackgroundWorker bg, string outFolder)
        {
            if (!Directory.Exists(outFolder))
                Directory.CreateDirectory(outFolder);
            var reportName = Path.GetFileName(outFolder);

            //generate resource files
            var css = Properties.Resources.idpicker_style;
            var jsFunctions = Properties.Resources.idpicker_scripts;
            var cssStream = new StreamWriter(Path.Combine(outFolder, "idpicker-style.css"));
            var jsSream = new StreamWriter(Path.Combine(outFolder, "idpicker-scripts.js"));
            cssStream.Write(css);
            cssStream.Flush();
            cssStream.Close();
            jsSream.Write(jsFunctions);
            jsSream.Flush();
            jsSream.Close();

            //generate html Files););
            if (proteinTableForm != null)
            {
                var alltables = new List<List<List<string>>> { proteinTableForm.GetFormTable(false) };
                ;
                if (alltables.Count > 0 && (alltables.Count > 1 || alltables[0].Count > 1))
                    TableExporter.CreateHTMLTablePage(alltables, Path.Combine(outFolder, reportName + "-protein.html"), reportName + "- Proteins",
                                                      true, false, false);
            }
            if (peptideTableForm != null)
            {
                var alltables = new List<List<List<string>>> { peptideTableForm.GetFormTable(false) };
                if (alltables.Count > 0 && (alltables.Count > 1 || alltables[0].Count > 1))
                    TableExporter.CreateHTMLTablePage(alltables, Path.Combine(outFolder, reportName + "-peptide.html"), reportName + "- Peptides",
                                                      true, false, false);
            }
            if (modificationTableForm != null)
            {
                var alltables = new List<List<List<string>>> { modificationTableForm.GetFormTable(false) };
                if (alltables.Count > 0 && (alltables.Count > 1 || alltables[0].Count > 1))
                    TableExporter.CreateHTMLTablePage(alltables, Path.Combine(outFolder, reportName + "-modificationTable.html"),
                                                      reportName + "- Modification Summary Table",
                                                      true, false, false);

                var modTree = modificationTableForm.getModificationTree(reportName);
                TableExporter.CreateHTMLTreePage(modTree, Path.Combine(outFolder, reportName + "-modificationList.html"),
                                                 reportName + "- Modification List",
                                                 new List<string> { "'Modified Site'", "'Mass'", "'Peptides'", "'Spectra'" },
                                                 new List<string> { "'Sequence'", "'Cluster'", "'Spectra'" });
            }
            if (analysisTableForm != null)
            {
                var alltables = new List<List<List<string>>> { analysisTableForm.GetFormTable(false) };
                if (alltables.Count > 0 && (alltables.Count > 1 || alltables[0].Count > 1))
                    TableExporter.CreateHTMLTablePage(alltables, Path.Combine(outFolder, reportName + "-analyses.html"),
                                                      reportName + "- Analyses",
                                                      true, false, false);
            }
            var clusterList = new List<string[]>();
            if (session != null)
            {
                var clusterIDList = session.CreateSQLQuery("select distinct cluster from protein").List().Cast<int>().ToList();
                foreach (var clusterID in clusterIDList)
                {
                    var cluster = getClusterInfo(clusterID);
                    if (cluster.proteinGroupCount > 0)
                        TableExporter.CreateHTMLTablePage(cluster.clusterTables,
                                                          Path.Combine(outFolder,
                                                                       reportName + "-cluster" + cluster.clusterID + ".html"),
                                                          reportName + "- Cluster" + cluster.clusterID,
                                                          true, false, true);
                    clusterList.Add(new[]
                                        {
                                            cluster.clusterID.ToString(),
                                            "'<a href=\"" + reportName + "-cluster" +
                                            cluster.clusterID + ".html\" target=\"mainFrame\">"
                                            + cluster.clusterID + "</a>'",
                                            cluster.proteinGroupCount.ToString(),
                                            cluster.peptideCount.ToString(),
                                            cluster.spectraCount.ToString()
                                        });
                }
            }

            //generate Tree HTML Files
            if (spectrumTableForm != null)
            {
                var sources = spectrumTableForm.getSourceContentsForHTML();
                var groups = spectrumTableForm.getSpectrumSourceGroupTree();
                var firstRowHeaders = new List<string>
                                          {
                                              "'Name'",
                                              "'Distinct Peptides'",
                                              "'Distinct Analyses'",
                                              "'Distinct Charges'",
                                              "'Precursor m/z'"
                                          };

                foreach (var kvp in sources)
                {
                    var name = kvp.Key[0];
                    var fileName = kvp.Key[1];
                    var secondHeaders = kvp.Key[2].Split('|').ToList();
                    if (kvp.Value.Any())
                        TableExporter.CreateHTMLTreePage(kvp.Value, Path.Combine(outFolder, fileName),
                                                         name, firstRowHeaders, secondHeaders);
                }
                var groupTreeHeaders = new List<string>
                                           {
                                               "'Name'",
                                               "'Filtered Spectra'",
                                               "'Distinct Peptides'",
                                               "'Distinct Matches'",
                                               "'Distinct Analyses'",
                                               "'Distinct Charges'"
                                           };
                if (groups.Any())
                    TableExporter.CreateHTMLTreePage(groups, Path.Combine(outFolder, reportName + "-groups.html"),
                                                    reportName + "- SpectrumSourceGroups", groupTreeHeaders, groupTreeHeaders);
            }

            //generate Sumamry Page
            var fullSummaryList = getSummaryList();
            var summaryList = new List<List<string>>();
            var summaryList2 = new List<List<string>>();
            var filtersFound = false;
            foreach (var row in fullSummaryList)
            {
                if (filtersFound)
                    summaryList2.Add(row);
                else
                {
                    if (row[0] == " --- Filters --- ")
                    {
                        filtersFound = true;
                        summaryList2.Add(row);
                    }
                    else if (row[0] != string.Empty)
                        summaryList.Add(row);
                }
            }
            if (summaryList.Count + summaryList2.Count > 0)
                TableExporter.CreateHTMLTablePage(new List<List<List<string>>> { summaryList, summaryList2 },
                                                  Path.Combine(outFolder, reportName + "-summary.html"),
                                                  reportName + "- Summary", false, true, false);

            //generate navigation page
            TableExporter.CreateNavigationPage(clusterList, outFolder, reportName);
            TableExporter.CreateIndexPage(outFolder, reportName);
            bg.ReportProgress(0);
            if (File.Exists(Path.Combine(outFolder, "index.html")))
                System.Diagnostics.Process.Start(Path.Combine(outFolder, "index.html"));
        }

        private ClusterInfo getClusterInfo(int cluster)
        {
            var ci = new ClusterInfo {peptideCount = 0, spectraCount = 0, clusterID = cluster};
            var allTables = new List<List<List<string>>>();

            var sequence2Data = new Dictionary<string, List<string>>();
            var sequence2Group = new Dictionary<string, List<int>>();
            var group2Sequences = new Dictionary<string, List<string>>();
            var peptideGroupList = new List<string>();

            var proteinTable = new List<List<string>>
                                   {
                                       new List<string>
                                           {
                                               "Group",
                                               "Accession",
                                               "Peptides",
                                               "Spectra",
                                               "Description"
                                           }
                                   };
            var clusterFilter = new DataFilter(viewFilter) {Cluster = new List<int> {cluster}};
            //var proteinGroupQuery = session.CreateQuery(
            //        ProteinTableForm.AggregateRow.Selection + ", " +
            //        "       DISTINCT_GROUP_CONCAT(pro.Accession), " +
            //        "       pro.ProteinGroup, " +
            //        "       MIN(pro.Id), " +
            //        "       MIN(pro.Length), " +
            //        "       MIN(pro.Description), " +
            //        "       COUNT(DISTINCT pro.Id), " +
            //        "       pro.Cluster, " +
            //        "       AVG(pro.Coverage) " +
            //        clusterFilter.GetFilteredQueryString(DataFilter.FromProtein,
            //                                          DataFilter.ProteinToPeptideSpectrumMatch) +
            //        "GROUP BY pro.ProteinGroup " +
            //        "ORDER BY COUNT(DISTINCT psm.Peptide.id) DESC");//, COUNT(DISTINCT psm.id) DESC, COUNT(DISTINCT psm.Spectrum.id) DESC");

            //proteinGroupQuery.SetReadOnly(true);
            //var proteinGroupList = proteinGroupQuery.List<object[]>().Select(o => new ProteinTableForm.ProteinGroupRow(o, viewFilter)).ToList();
            var proteinGroupList = ProteinTableForm.ProteinGroupRow.GetRows(session, clusterFilter);
            //var proteinGroupList = genericProteinGroupList.Cast<ProteinTableForm.ProteinGroupRow>().ToList();
            ci.proteinGroupCount = proteinGroupList.Count;

            for (int x = 0; x < proteinGroupList.Count ;x++ )
            {
                var proteinGroup = proteinGroupList[x];
                proteinTable.Add(new List<string>
                                     {
                                         TableExporter.IntToColumn(x+1),
                                         proteinGroup.Proteins,
                                         proteinGroup.DistinctPeptides.ToString(),
                                         proteinGroup.Spectra.ToString(),
                                         proteinGroup.FirstProtein.Description
                                     });

                //get peptides in protein
                var allGroupedProteins = session.CreateQuery(String.Format(
                "SELECT pro FROM Protein pro WHERE pro.Accession IN ('{0}')",
                proteinGroup.Proteins.Replace(",", "','")))
                .List<Protein>();
                var proteinFilter = new DataFilter(clusterFilter) { Protein = allGroupedProteins };
                var peptides = PeptideTableForm.DistinctPeptideRow.GetRows(session, proteinFilter);

                foreach (var peptide in peptides)
                {
                    if (sequence2Data.ContainsKey(peptide.Peptide.Sequence))
                        sequence2Group[peptide.Peptide.Sequence].Add(x);
                    else
                    {
                        sequence2Data.Add(peptide.Peptide.Sequence,
                                          new List<string>
                                              {
                                                  peptide.Spectra.ToString(),
                                                  Math.Round(peptide.Peptide.MonoisotopicMass,4).ToString(),
                                                  Math.Round(peptide.Peptide.Matches.Min(n => n.QValue),4).ToString()
                                              });
                        sequence2Group.Add(peptide.Peptide.Sequence, new List<int> {x});
                        ci.spectraCount += peptide.Spectra;
                        ci.peptideCount++;
                    }
                }
            }
            allTables.Add(proteinTable);

            foreach (var kvp in sequence2Group)
            {
                kvp.Value.Sort();
                var value = new List<string>();
                foreach (var group in kvp.Value)
                    value.Add(group.ToString());
                var groupName = string.Join(",", value.ToArray());

                if (group2Sequences.ContainsKey(groupName))
                    group2Sequences[groupName].Add(kvp.Key);
                else
                {
                    group2Sequences.Add(groupName,new List<string>{kvp.Key});
                    peptideGroupList.Add(groupName);
                }
            }

            peptideGroupList.Sort();
            var peptideTable = new List<List<string>>
                                   {
                                       new List<string>
                                           {
                                               "PeptideGroup",
                                               "Unique",
                                               "Sequence",
                                               "Spectra",
                                               "Mass",
                                               "Best Q-Value"
                                           }
                                   };
            for (var x = 0; x < peptideGroupList.Count; x++)
            {
                var first = true;
                var unique = peptideGroupList[x].Length == 1;
                foreach (var peptide in group2Sequences[peptideGroupList[x]])
                {
                    peptideTable.Add(new List<string>
                                         {
                                             first ? (x+1).ToString() : string.Empty,
                                             unique ? "*" : string.Empty,
                                             peptide,
                                             sequence2Data[peptide][0],
                                             sequence2Data[peptide][1],
                                             sequence2Data[peptide][2]
                                         });
                    first = false;
                }
            }
            allTables.Add(peptideTable);

            var associationTable = new List<List<string>>();

            //first row
            var tempList = new List<string> {string.Empty};
            for (var x = 1; x <= peptideGroupList.Count;x++)
                tempList.Add(x.ToString());
            associationTable.Add(tempList);

            //second row
            tempList = new List<string>(){"Peptides"};
            for (var x = 0; x < peptideGroupList.Count; x++)
                tempList.Add(group2Sequences[peptideGroupList[x]].Count.ToString());
            associationTable.Add(tempList);

            //third row
            tempList = new List<string>() { "Spectra" };
            for (var x = 0; x < peptideGroupList.Count; x++)
            {
                var spectraCount = 0;
                foreach (var sequence in group2Sequences[peptideGroupList[x]])
                {
                    int peptideSpectra;
                    int.TryParse(sequence2Data[sequence][0], out peptideSpectra);
                    spectraCount += peptideSpectra;
                }
                tempList.Add(spectraCount.ToString());
            }
            associationTable.Add(tempList);

            //protein rows
            for (var x = 0; x < proteinGroupList.Count; x++)
            {
                tempList = new List<string>{TableExporter.IntToColumn(x+1)};
                for (var y = 0; y < peptideGroupList.Count; y++)
                {
                    var containedProGroups = peptideGroupList[y].Split(",".ToCharArray());
                    var containNumbers = containedProGroups.Select(item => int.Parse(item));
                    tempList.Add(containNumbers.Contains(x) ? "x" : string.Empty);
                }
                associationTable.Add(tempList);
            }
            allTables.Add(associationTable);

            ci.clusterTables = allTables;
            return ci;
        }

        private void fileToolStripMenuRoot_DropDownOpening(object sender, EventArgs e)
        {
            // the "Import" submenu is shown if a session is open
            newToolStripMenuItem.Visible = (session != null);
            importToolStripMenuItem.Visible = (session != null);

            // the "Export" and "Embed" options are enabled if a session is open
            embedSpectraToolStripMenuItem.Enabled = (session != null);
            exportToolStripMenuItem.Enabled = (session != null);
        }

        private void importToToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // if no session is open, treat clicks on the "Import" option as clicks on the "Import to New" option
            if (session == null)
                openToolStripMenuItem_Click(newToolStripMenuItem, e);
        }

        private void checkForUpdatesAutomaticallyToolStripMenuItem_CheckedChanged (object sender, EventArgs e)
        {
            Properties.GUI.Settings.Default.AutomaticCheckForUpdates = checkForUpdatesAutomaticallyToolStripMenuItem.Checked;
            Properties.GUI.Settings.Default.Save();
        }

        private void checkForUpdatesToolStripMenuItem_Click (object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            if (!Program.CheckForUpdates())
                MessageBox.Show("You are running the latest version.", "No Update Available");
            Cursor = Cursors.Default;
        }

        private void aboutToolStripMenuItem_Click (object sender, EventArgs e)
        {
            MessageBox.Show(String.Format("IDPicker {0}\r\n" +
                                          "Copyright 2011 Vanderbilt University\r\n" +
                                          "Developers: Matt Chambers, Jay Holman, Surendra Dasari\r\n" +
                                          "Thanks to: David Tabb",
                                          Util.Version),
                            "About IDPicker");
        }

        private void embedSpectraToolStripMenuItem_Click (object sender, EventArgs e)
        {
            if (session == null)
                return;

            var form = new EmbedderForm(session) { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(this);
        }

        private void exportSubsetFASTAToolStripMenuItem_Click (object sender, EventArgs e)
        {
            if (session == null)
                return;

            using (var exporter = new Exporter(session) { DataFilter = viewFilter })
            using (var saveDialog = new SaveFileDialog())
            {
                string dataSource = session.Connection.GetDataSource();
                saveDialog.InitialDirectory = Path.GetDirectoryName(dataSource);
                saveDialog.FileName = Path.GetFileNameWithoutExtension(dataSource) + ".fasta";
                saveDialog.Filter = "FASTA|*.fasta";
                saveDialog.AddExtension = true;

                bool addDecoys = MessageBox.Show("Do you want to add a decoy for each target protein?",
                                                 "Add Decoys",
                                                 MessageBoxButtons.YesNo,
                                                 MessageBoxIcon.Question,
                                                 MessageBoxDefaultButton.Button2) == DialogResult.Yes;

                if (saveDialog.ShowDialog(this) == DialogResult.OK)
                    exporter.WriteProteins(saveDialog.FileName, addDecoys);
            }
        }
    }

    internal class ClusterInfo
    {
        public int clusterID { get; set; }
        public int proteinGroupCount { get; set; }
        public int peptideCount { get; set; }
        public long spectraCount { get; set; }
        public List<List<List<string>>> clusterTables { get; set; }
    }

    public class SourceNotFoundEventArgs : EventArgs
    {
        public string SourcePath { get; set; }
    }
}