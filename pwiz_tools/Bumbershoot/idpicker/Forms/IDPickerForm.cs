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
using System.Diagnostics;
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
using pwiz.CLI.chemistry;
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
        RescuePSMsForm reassignPSMsForm;

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

        IList<string> args;

        public IDPickerForm (IList<string> args)
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

            if (Program.IsHeadless)
                Load += IDPickerForm_Load;
            else
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
            fragmentationStatisticsForm.AutoHidePortion = 0.5;

            peakStatisticsForm = new PeakStatisticsForm(this);
            peakStatisticsForm.Show(dockPanel, DockState.DockBottomAutoHide);
            peakStatisticsForm.AutoHidePortion = 0.5;

            reassignPSMsForm = new RescuePSMsForm(this);
            reassignPSMsForm.Show(dockPanel, DockState.DockBottomAutoHide);
            reassignPSMsForm.AutoHidePortion = 0.5;

            spectrumTableForm = new SpectrumTableForm();
            spectrumTableForm.Show(dockPanel, DockState.DockLeft);
            spectrumTableForm.AutoHidePortion = 0.5;

            proteinTableForm = new ProteinTableForm();
            proteinTableForm.Show(dockPanel, DockState.DockTop);
            proteinTableForm.AutoHidePortion = 0.5;

            peptideTableForm = new PeptideTableForm();
            peptideTableForm.Show(proteinTableForm.Pane, DockPaneAlignment.Right, 0.7);
            peptideTableForm.AutoHidePortion = 0.5;

            modificationTableForm = new ModificationTableForm();
            modificationTableForm.Show(dockPanel, DockState.Document);
            modificationTableForm.AutoHidePortion = 0.5;

            analysisTableForm = new AnalysisTableForm();
            analysisTableForm.Show(dockPanel, DockState.Document);
            analysisTableForm.AutoHidePortion = 0.5;

            spectrumTableForm.SpectrumViewFilter += spectrumTableForm_SpectrumViewFilter;
            spectrumTableForm.SpectrumViewVisualize += spectrumTableForm_SpectrumViewVisualize;
            proteinTableForm.ProteinViewFilter += proteinTableForm_ProteinViewFilter;
            proteinTableForm.ProteinViewVisualize += proteinTableForm_ProteinViewVisualize;
            peptideTableForm.PeptideViewFilter += peptideTableForm_PeptideViewFilter;
            modificationTableForm.ModificationViewFilter += modificationTableForm_ModificationViewFilter;

            // hide DockPanel before initializing layout manager
            dockPanel.Visible = false;
            dockPanel.ShowDocumentIcon = true;

            _layoutManager = new LayoutManager(this, peptideTableForm, proteinTableForm, spectrumTableForm, dockPanel);

            // load last or default location and size
            _layoutManager.LoadMainFormSettings();

            // provide SQL logging for development builds
            if (Application.ExecutablePath.Contains("build-nt-x86"))
            {
                logForm = new LogForm();
                Console.SetOut(logForm.LogWriter);
            }
            else
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
                var folderHistory = FolderHistoryInterface.GetRecentFolders();
                if (folderHistory.Any() && Directory.Exists(folderHistory.Last()))
                    ofd.InitialDirectory = folderHistory.First();

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

        public static string LocateSpectrumSource (string spectrumSourceName)
        {
            if (String.IsNullOrEmpty(spectrumSourceName))
                return String.Empty;

            try
            {
                return Util.FindSourceInSearchPath(spectrumSourceName, ".");
            }
            catch
            {
                try
                {
                    return Util.FindSourceInSearchPath(spectrumSourceName, Properties.GUI.Settings.Default.LastSpectrumSourceDirectory);
                }
                catch
                {
                    var findDirectoryDialog = new FolderBrowserDialog()
                    {
                        SelectedPath = Properties.GUI.Settings.Default.LastSpectrumSourceDirectory,
                        ShowNewFolderButton = false,
                        Description = "Locate the directory containing the source \"" + spectrumSourceName + "\""
                    };

                    while (findDirectoryDialog.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            string sourcePath = Util.FindSourceInSearchPath(spectrumSourceName, findDirectoryDialog.SelectedPath);
                            Properties.GUI.Settings.Default.LastSpectrumSourceDirectory = findDirectoryDialog.SelectedPath;
                            Properties.GUI.Settings.Default.Save();
                            return sourcePath;
                        }
                        catch
                        {
                            // couldn't find the source in that directory; prompt user again
                        }
                    }

                    return String.Empty; // user canceled
                }
            }
        }

        Dictionary<GraphForm, bool> handlerIsAttached = new Dictionary<GraphForm, bool>();
        Dictionary<GraphForm, PeptideFragmentationAnnotation> annotationByGraphForm = new Dictionary<GraphForm, PeptideFragmentationAnnotation>();
        void spectrumTableForm_SpectrumViewVisualize (object sender, SpectrumViewVisualizeEventArgs e)
        {
            var spectrum = e.Spectrum;
            var source = e.SpectrumSource;

            string sourcePath;
            if (source.Metadata != null)
            {
                //BeginInvoke(new MethodInvoker(() => toolStripStatusLabel.Text = "Extracting embedded spectrum source: " + source.Name));

                // accessing the Metadata property creates a temporary mz5 file
                var mz5 = source.Metadata as TemporaryMSDataFile;
                sourcePath = mz5.Filepath;
            }
            else
            {
                sourcePath = LocateSpectrumSource(source.Name);
                if (String.IsNullOrEmpty(sourcePath))
                    return; // file still not found, abort the visualization
            }

            var param = e.Analysis.Parameters.Where(o => o.Name == "SpectrumListFilters").SingleOrDefault();
            string spectrumListFilters = param == null ? String.Empty : param.Value;
            spectrumListFilters = spectrumListFilters.Replace("0 ", "false ");

            var ionSeries = PeptideFragmentationAnnotation.IonSeries.Auto;
            if (sourcePath.ToLower().EndsWith(".mgf"))
                ionSeries = PeptideFragmentationAnnotation.IonSeries.b | PeptideFragmentationAnnotation.IonSeries.y;

            bool showFragmentationLadders = true;
            bool showMissedFragments = false;
            bool showLabels = true;
            bool showFragmentationSummary = false;
            MZTolerance tolerance = null;

            if (manager.CurrentGraphForm != null && annotationByGraphForm[manager.CurrentGraphForm] != null)
            {
                var panel = PeptideFragmentationAnnotation.annotationPanels;
                showFragmentationLadders = panel.showFragmentationLaddersCheckBox.Checked;
                showMissedFragments = panel.showMissesCheckBox.Checked;
                showFragmentationSummary = panel.showFragmentationSummaryCheckBox.Checked;
                if (panel.fragmentToleranceTextBox.Text.Length > 0)
                {
                    tolerance = new MZTolerance();
                    tolerance.value = Convert.ToDouble(panel.fragmentToleranceTextBox.Text);
                    tolerance.units = (MZTolerance.Units) panel.fragmentToleranceUnitsComboBox.SelectedIndex;
                }
                else
                    tolerance = null;
            }

            var annotation = new PeptideFragmentationAnnotation(e.ModifiedSequence, 1, Math.Max(1, e.Charge - 1),
                                                                tolerance,
                                                                ionSeries,
                                                                showFragmentationLadders,
                                                                showMissedFragments,
                                                                showLabels,
                                                                showFragmentationSummary);

            (manager.SpectrumAnnotationForm.Controls[0] as ToolStrip).Hide();
            (manager.SpectrumAnnotationForm.Controls[1] as SplitContainer).Panel1Collapsed = true;
            (manager.SpectrumAnnotationForm.Controls[1] as SplitContainer).Dock = DockStyle.Fill;

            //BeginInvoke(new MethodInvoker(() => toolStripStatusLabel.Text = toolStripStatusLabel.Text = "Opening spectrum source: " + sourcePath));

            manager.OpenFile(sourcePath, spectrum.NativeID, annotation, spectrumListFilters);
            manager.CurrentGraphForm.Focus();
            manager.CurrentGraphForm.Icon = Properties.Resources.SpectrumViewIcon;

            //BeginInvoke(new MethodInvoker(() => toolStripStatusLabel.Text = toolStripStatusLabel.Text = "Ready"));

            annotationByGraphForm[manager.CurrentGraphForm] = annotation;

            if (!handlerIsAttached.ContainsKey(manager.CurrentGraphForm))
            {
                handlerIsAttached[manager.CurrentGraphForm] = true;
                manager.CurrentGraphForm.ZedGraphControl.PreviewKeyDown += CurrentGraphForm_PreviewKeyDown;
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

            if (Program.IsHeadless)
                Close();
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
            if (reassignPSMsForm != null) reassignPSMsForm.ClearData(true);
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
            reassignPSMsForm.SetData(session, basicFilter);
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

            if (args.IsNullOrEmpty())
                return;

            var missingFiles = args.Where(o => !File.Exists(o));
            if (missingFiles.Any())
                throw new ArgumentException("some files do not exist: " + String.Join(" ", missingFiles.ToArray()));

            new Thread(() => { OpenFiles(args, null); }).Start();
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

                if (Program.IsHeadless && xml_filepaths.Any())
                    MessageBox.Show("Headless mode only supports merging idpDB files.");

                // warn if idpDBs already exist
                bool warnOnce = false, skipReconvert = false;
                var skipFiles = new List<string>();
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
                            skipReconvert = true;
                        warnOnce = true;
                        if (skipReconvert)
                            skipFiles.Add(filepath);
                        else
                            File.Delete(idpDB_filepath);
                    }
                }
                xml_filepaths = xml_filepaths.Where(o => !skipFiles.Contains(o));
                idpDB_filepaths = idpDB_filepaths.Union(skipFiles.Select(o => Path.ChangeExtension(o, ".idpDB")));


                // determine if merged filepath exists and that it's a valid idpDB
                string commonFilename = Util.GetCommonFilename(filepaths);
                bool workToBeDone = filepaths.Count > 1 || (filepaths.Count > 0 && !filepaths[0].ToLower().EndsWith(".idpdb"));
                bool fileExists = File.Exists(commonFilename);
                bool fileIsValid = fileExists && SessionFactoryFactory.IsValidFile(commonFilename);
                var potentialPaths = filepaths.Select(item =>
                                                      Path.Combine(Path.GetDirectoryName(item) ?? string.Empty,
                                                                   Path.GetFileNameWithoutExtension(item) ??

                                                                   string.Empty) + ".idpDB").ToList();
                if (fileExists && Program.IsHeadless)
                    File.Delete(commonFilename);
                else if ((fileExists ||
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
                            string randomTestFile = Path.Combine(possibleDirectory, Path.GetRandomFileName());
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

                //set up delayed messages so non-fatal errors that occur at the end arent lost
                var delayedMessages = new List<string[]>();
                if (xml_filepaths.Count() > 0)
                {
                    importCancelled = false;

                    Parser parser = new Parser();
                    Invoke(new MethodInvoker(() => parser.ImportSettings += importSettingsHandler));

                    var ilr = new IterationListenerRegistry();

                    var progressForm = new ProgressForm(xml_filepaths, ilr)
                    {
                        Text = "Import Progress",
                        StartPosition = FormStartPosition.CenterParent,
                    };
                    progressForm.NonFatalErrorCaught +=
                        (rawMessage, emptyargs) =>
                            {
                                var message = rawMessage as string[];
                                if (message == null || message.Length < 2)
                                    return;
                                delayedMessages.Add(new[]{message[0], message[1]});
                            };

                    Invoke(new MethodInvoker(() => progressForm.Show(this)));

                    try
                    {
                        parser.Parse(xml_filepaths, ilr);
                    }
                    catch (Exception ex)
                    {
                        importCancelled = true;

                        if (ex.Message.Contains("no peptides found mapping to a decoy protein"))
                            Program.HandleUserError(ex);
                        else
                            throw;
                    }
                    finally
                    {
                        importCancelled |= progressForm.Cancelled;
                        Invoke(new MethodInvoker(() => progressForm.Close()));
                    }

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

                // HACK: this needs to be handled more gracefully
                if (!IsHandleCreated)
                    return;

                // if the database is on a hard drive and can fit in the available RAM, populate the disk cache
                long ramBytesAvailable = (long) new System.Diagnostics.PerformanceCounter("Memory", "Available Bytes").NextValue();
                if (ramBytesAvailable > new FileInfo(commonFilename).Length &&
                    DriveType.Fixed == new DriveInfo(Path.GetPathRoot(commonFilename)).DriveType)
                {
                    toolStripStatusLabel.Text = "Precaching idpDB...";
                    using (var fs = new FileStream(commonFilename, FileMode.Open, FileSystemRights.ReadData, FileShare.ReadWrite, UInt16.MaxValue, FileOptions.SequentialScan))
                    {
                        var buffer = new byte[UInt16.MaxValue];
                        while (fs.Read(buffer, 0, UInt16.MaxValue) > 0) { }
                    }
                }

                if (!IsHandleCreated)
                    return;

                BeginInvoke(new MethodInvoker(() =>
                {
                    var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(commonFilename, new SessionFactoryConfig { WriteSqlToConsoleOut = true });

                    // reload qonverter settings because the ids may change after merging
                    toolStripStatusLabel.Text = "Loading qonverter settings...";
                    statusStrip.Refresh();
                    session = sessionFactory.OpenSession();
                    qonverterSettingsByAnalysis = session.Query<QonverterSettings>().ToDictionary(o => session.Get<Analysis>(o.Id));

                    session.CreateSQLQuery("PRAGMA temp_store=MEMORY").ExecuteUpdate();

                    _layoutManager.SetSession(session);

                    //set or save default layout
                    dockPanel.Visible = true;
                    _layoutManager.CurrentLayout = _layoutManager.GetCurrentDefault();

                    toolStripStatusLabel.Text = "Refreshing group structure...";
                    statusStrip.Refresh();
                    var usedGroups = GroupingControlForm.SetInitialStructure(rootNode, session);
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

                    // pick a default RoundToNearest based on number of distinct modifications
                    modificationTableForm.RoundToNearest = 1m;
                    var distinctModificationFormat = new DistinctMatchFormat();
                    var modMasses = session.CreateQuery("SELECT DISTINCT mod.MonoMassDelta FROM Modification mod").List<double>();
                    for (int i = 4; i > 0; --i)
                    {
                        distinctModificationFormat.ModificationMassRoundToNearest = (decimal) (1.0 / Math.Pow(10, i));
                        if (modMasses.Select(o => distinctModificationFormat.Round(o)).Distinct().Count() < 30)
                        {
                            modificationTableForm.RoundToNearest = distinctModificationFormat.ModificationMassRoundToNearest.Value;
                            break;
                        }
                    }

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

                        try
                        {
                            // check that the unfiltered tables exist
                            session.CreateSQLQuery("SELECT COUNT(*) FROM UnfilteredProtein").UniqueResult();

                            setData();
                        }
                        catch
                        {
                            ApplyBasicFilter();
                        }
                    }

                    toolStripStatusLabel.Text = "Ready";

                    if (logForm != null) logForm.Show(dockPanel, DockState.DockBottomAutoHide);
                }));

                //show list of delayed non-fatal errors
                if (delayedMessages.Any())
                {
                    var sb = new StringBuilder();
                    foreach (var message in delayedMessages)
                        sb.AppendLine(string.Format("{0}:{1}{2}{1}", message[0], Environment.NewLine, message[1]));
                    var messageString = sb.ToString();

                    ShowExpandedMessageBox(messageString);
                }
            }
            catch (Exception ex)
            {
                Program.HandleException(ex);
            }
        }

        private static void ShowExpandedMessageBox(string messageString)
        {
            var messageForm = new Form(){Size = new Size(500,300)};
            var splitContainer = new SplitContainer()
                                     {
                                         Dock = DockStyle.Fill,
                                         IsSplitterFixed = true,
                                         SplitterWidth = 1,
                                         SplitterDistance = 300,
                                         Orientation = Orientation.Horizontal,
                                         Panel2MinSize = 20
                                     };
            splitContainer.Resize +=
                (x, y) => { splitContainer.SplitterDistance = messageForm.Size.Height - 67; };
            var okButton = new Button
                               {
                                   DialogResult = DialogResult.OK,
                                   Text = "Ok",
                                   Anchor = (AnchorStyles.Right | AnchorStyles.Top),
                                   //Location = new System.Drawing.Point(414, 4),
                                   Size = new Size(75,23),
                                   Dock = DockStyle.Right
                               };
            splitContainer.Panel2.Controls.Add(okButton);
            var textBox = new TextBox
                              {
                                  Multiline = true,
                                  ScrollBars = ScrollBars.Both,
                                  Dock = DockStyle.Fill,
                                  Text = messageString.Trim(),
                                  ReadOnly = true,
                                  SelectionLength = 0,
                                  TabStop = false
                              };
            splitContainer.Panel1.Controls.Add(textBox);
            messageForm.Controls.Add(splitContainer);
            messageForm.ShowDialog();
            okButton.Focus();
        }

        internal void LoadLayout(LayoutProperty userLayout)
        {
            if (userLayout == null)
                return;

            var tempFilepath = Path.GetTempFileName();
            using (var tempFile = new StreamWriter(tempFilepath, false, Encoding.Unicode))
                tempFile.Write(userLayout.PaneLocations);

            try
            {
                dockPanel.SuspendLayout();
                dockPanel.LoadFromXml(tempFilepath, DeserializeForm);
                dockPanel.ResumeLayout(true, true);
            }
            finally
            {
                File.Delete(tempFilepath);
            }

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
            if (persistantString == typeof(RescuePSMsForm).ToString())
                return reassignPSMsForm;
            
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

        IDictionary<Analysis, QonverterSettings> qonverterSettingsHandler (IDictionary<Analysis, QonverterSettings> oldSettings, out bool cancel)
        {
            qonverterSettingsByAnalysis = new Dictionary<Analysis, QonverterSettings>();
            oldSettings.ForEach(o => qonverterSettingsByAnalysis.Add(o.Key, o.Value));
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

            lock (session)
            {
                var databaseAnalysis = session.QueryOver<Analysis>().List();
                var oldSettings = session.Query<QonverterSettings>().ToDictionary(o => session.Get<Analysis>(o.Id));

                bool cancel;
                var qonverterSettings = qonverterSettingsHandler(oldSettings, out cancel);
                if (cancel)
                    return;

                var qonverter = new Qonverter();
                qonverter.QonversionProgress += progressMonitor.UpdateProgress;
                qonverterSettingsByAnalysis = session.Query<QonverterSettings>().ToDictionary(o => session.Get<Analysis>(o.Id));
                foreach (var item in qonverterSettings)
                {
                    // TODO: move updating of QonverterSettings to native Qonverter?
                    qonverter.SettingsByAnalysis[(int) item.Key.Id] = item.Value.ToQonverterSettings();
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
            }

            var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(Text, new SessionFactoryConfig { WriteSqlToConsoleOut = true });
            session = sessionFactory.OpenSession();
            //session.CreateSQLQuery("PRAGMA temp_store=MEMORY").ExecuteUpdate();
            _layoutManager.SetSession(session);

            if (basicFilter == null)
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
                                     var table = modificationTableForm.GetFormTable(selected, false);
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
                            filterInfo.Add(new List<string> { "Modifications", item.MonoMassDelta.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.MonoMassDelta.ToString() });
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
                var alltables = new List<List<List<string>>> { modificationTableForm.GetFormTable(false, false) };
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
                //try to pre-cache data if possible
                //report scales horribly with cluster count (655 clusters took 40 minutes)
                //limiting calls to the database should speed this up considerably
                var clusterIDList = session.CreateSQLQuery("select distinct cluster from protein").List().Cast<int>().ToList();
                var tempFilter = new DataFilter(viewFilter) {Cluster = new List<int>(), Protein = new List<Protein>()};
                var clusterToProteinList = new Dictionary<int, List<ProteinTableForm.ProteinGroupRow>>();
                var proteinAccessionToPeptideList = new Dictionary<string, HashSet<int>>();
                List<PeptideTableForm.DistinctPeptideRow> peptideList;
                try
                {
                    //seperating proteinList namespace in order to try to conserve memory
                    {
                        var proteinList = ProteinTableForm.ProteinGroupRow.GetRows(session, tempFilter).ToList();
                        foreach (var protein in proteinList)
                        {
                            if (!clusterToProteinList.ContainsKey(protein.FirstProtein.Cluster))
                                clusterToProteinList.Add(protein.FirstProtein.Cluster,
                                                         new List<ProteinTableForm.ProteinGroupRow> {protein});
                            else
                                clusterToProteinList[protein.FirstProtein.Cluster].Add(protein);
                        }
                    }
                    
                    peptideList = PeptideTableForm.DistinctPeptideRow.GetRows(session, tempFilter).ToList();
                    for (var x = 0; x < peptideList.Count; x++)
                    {
                        var peptide = peptideList[x].Peptide;
                        foreach (var instance in peptide.Instances)
                        {
                            var proteinAccession = instance.Protein.Accession;
                            if (!proteinAccessionToPeptideList.ContainsKey(proteinAccession))
                                proteinAccessionToPeptideList.Add(proteinAccession, new HashSet<int> { x });
                            else
                                proteinAccessionToPeptideList[proteinAccession].Add(x);
                        }
                    }
                }
                catch (Exception e)
                {
                    peptideList = null;
                    var errorMessage =
                        "[ClusterInfo] Error when precaching data. " +
                        "Results may be processed slower than expected - " +
                        Environment.NewLine + e.Message;
                    if (InvokeRequired)
                        Invoke(new Action(() => MessageBox.Show(errorMessage)));
                    else
                        MessageBox.Show(errorMessage);
                }

                foreach (var clusterID in clusterIDList)
                {
                    ClusterInfo cluster;
                    if (peptideList == null)
                        cluster = getClusterInfo(clusterID);
                    else
                        cluster = getClusterInfo(clusterID, clusterToProteinList[clusterID],
                                                 proteinAccessionToPeptideList, peptideList);
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
            var tempFilter = new DataFilter(viewFilter) { Cluster = new List<int> {cluster}, Protein = new List<Protein>() };
            var proteinAccessionToPeptideList = new Dictionary<string, HashSet<int>>();
            var peptideList = new List<PeptideTableForm.DistinctPeptideRow>();
            var peptideFound = new HashSet<long?>();

            var proteinList = ProteinTableForm.ProteinGroupRow.GetRows(session, tempFilter).ToList();
            foreach (var proteinGroup in proteinList)
            {
                //get peptides in protein
                var allGroupedProteins = session.CreateQuery(String.Format(
                    "SELECT pro FROM Protein pro WHERE pro.Accession IN ('{0}')",
                    proteinGroup.Proteins.Replace(",", "','")))
                    .List<Protein>();
                var proteinFilter = new DataFilter(tempFilter) { Protein = allGroupedProteins };
                var peptides = PeptideTableForm.DistinctPeptideRow.GetRows(session, proteinFilter);
                foreach (var peptide in peptides)
                {
                    if (peptideFound.Add(peptide.Peptide.Id))
                        peptideList.Add(peptide);
                }
                for (var x = 0; x < peptideList.Count; x++)
                {
                    var peptide = peptideList[x].Peptide;
                    foreach (var instance in peptide.Instances)
                    {
                        var proteinAccession = instance.Protein.Accession;
                        if (!proteinAccessionToPeptideList.ContainsKey(proteinAccession))
                            proteinAccessionToPeptideList.Add(proteinAccession, new HashSet<int> { x });
                        else
                            proteinAccessionToPeptideList[proteinAccession].Add(x);
                    }
                }

            }

            return getClusterInfo(cluster, proteinList, proteinAccessionToPeptideList, peptideList);
        }

        private ClusterInfo getClusterInfo(int cluster, List<ProteinTableForm.ProteinGroupRow> proteinList,
            Dictionary<string, HashSet<int>> proteinAccessionToPeptideList,
            List<PeptideTableForm.DistinctPeptideRow> peptideList)
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
            ci.proteinGroupCount = proteinList.Count;

            for (int x = 0; x < proteinList.Count; x++)
            {
                var proteinGroup = proteinList[x];
                proteinTable.Add(new List<string>
                                     {
                                         TableExporter.IntToColumn(x + 1),
                                         proteinGroup.Proteins,
                                         proteinGroup.DistinctPeptides.ToString(),
                                         proteinGroup.Spectra.ToString(),
                                         proteinGroup.FirstProtein.Description
                                     });

                //get peptides in protein
                var usedPeptides = new HashSet<PeptideTableForm.DistinctPeptideRow>();
                var allGroupedProteins = session.CreateQuery(String.Format(
                    "SELECT pro FROM Protein pro WHERE pro.Accession IN ('{0}')",
                    proteinGroup.Proteins.Replace(",", "','")))
                    .List<Protein>();
                foreach (var protein in allGroupedProteins)
                    foreach (var peptideIndex in proteinAccessionToPeptideList[protein.Accession])
                        usedPeptides.Add(peptideList[peptideIndex]);

                foreach (var peptide in usedPeptides)
                {
                    if (sequence2Data.ContainsKey(peptide.Peptide.Sequence))
                        sequence2Group[peptide.Peptide.Sequence].Add(x);
                    else
                    {
                        sequence2Data.Add(peptide.Peptide.Sequence,
                                          new List<string>
                                              {
                                                  peptide.Spectra.ToString(),
                                                  Math.Round(peptide.Peptide.MonoisotopicMass, 4).ToString(),
                                                  Math.Round(peptide.Peptide.Matches.Min(n => n.QValue), 4).ToString()
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
                    group2Sequences.Add(groupName, new List<string> {kvp.Key});
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
                                             first ? (x + 1).ToString() : string.Empty,
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
            for (var x = 1; x <= peptideGroupList.Count; x++)
                tempList.Add(x.ToString());
            associationTable.Add(tempList);

            //second row
            tempList = new List<string>() {"Peptides"};
            for (var x = 0; x < peptideGroupList.Count; x++)
                tempList.Add(group2Sequences[peptideGroupList[x]].Count.ToString());
            associationTable.Add(tempList);

            //third row
            tempList = new List<string>() {"Spectra"};
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
            for (var x = 0; x < proteinList.Count; x++)
            {
                tempList = new List<string> {TableExporter.IntToColumn(x + 1)};
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
                                          "Copyright 2012 Vanderbilt University\r\n" +
                                          "Developers: Matt Chambers, Jay Holman, Surendra Dasari, Zeqiang Ma\r\n" +
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

        private void toQuasitelToolStripMenuItem_Click (object sender, EventArgs e)
        {
            if (session == null)
                return;

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesRoot = Path.GetPathRoot(programFiles);
            string rPath;
            if (Directory.Exists(Path.Combine(programFiles, "R")))
                rPath = Path.Combine(programFiles, "R");
            else if (Directory.Exists(Path.Combine(programFilesRoot, "Program Files/R")))
                rPath = Path.Combine(programFilesRoot, "Program Files/R");
            else if (Directory.Exists(Path.Combine(programFilesRoot, "Program Files (x86)/R")))
                rPath = Path.Combine(programFilesRoot, "Program Files (x86)/R");
            else
            {
                Program.HandleUserError(new FileNotFoundException("unable to find an installation of R in Program Files"));
                return;
            }

            string rFilepath = Directory.GetFiles(rPath, "Rscript.exe", SearchOption.AllDirectories).LastOrDefault();
            if (rFilepath == null)
            {
                Program.HandleUserError(new FileNotFoundException("unable to find an installation of R in Program Files"));
                return;
            }

            string quasitelFilepath = Path.Combine(Application.StartupPath, "QuasiTel V2.R");
            if (!File.Exists(quasitelFilepath))
            {
                Program.HandleUserError(new FileNotFoundException("unable to find QuasiTel R script"));
                return;
            }

            string idpDbFilepath = session.Connection.GetDataSource();

            var process = new System.Diagnostics.Process
            {
                StartInfo = new ProcessStartInfo(rFilepath, String.Format("\"{0}\" \"{1}\"", quasitelFilepath, idpDbFilepath))
            };
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = process.StartInfo.RedirectStandardError = true;
            process.Start();
            process.EnableRaisingEvents = true;
            process.Exited += (x, y) =>
            {
                if (process.ExitCode != 0)
                    Program.HandleException(new Exception(String.Format("Output:\r\n{0}\r\n\r\nError:\r\n{1}",
                                                          process.StandardOutput.ReadToEnd(),
                                                          process.StandardError.ReadToEnd())));
            };
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
}