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
        DistributionStatisticsForm distributionStatisticsForm;
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

        /// <summary>The default settings from either user settings or the command-line.</summary>
        private DataFilter defaultDataFilter { get; set; }

        /// <summary>
        /// The default filepath of the idpDB to merge the input idpDBs to.
        /// If null, a sensible default filepath will be calculated but the user will be able to override it.
        /// </summary>
        private string defaultMergedOutputFilepath = null;

        public IDPickerForm (IList<string> args)
        {
            InitializeComponent();

            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;

            this.args = args;

            defaultDataFilter = new DataFilter();

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

            distributionStatisticsForm = new DistributionStatisticsForm(this);
            distributionStatisticsForm.Show(dockPanel, DockState.DockBottomAutoHide);
            distributionStatisticsForm.AutoHidePortion = 0.5;

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

            spectrumTableForm.SpectrumViewFilter += handleViewFilter;
            spectrumTableForm.SpectrumViewVisualize += spectrumTableForm_SpectrumViewVisualize;
            spectrumTableForm.FinishedSetData += handleFinishedSetData;
            spectrumTableForm.StartingSetData += handleStartingSetData;
            proteinTableForm.ProteinViewFilter += handleViewFilter;
            proteinTableForm.ProteinViewVisualize += proteinTableForm_ProteinViewVisualize;
            proteinTableForm.FinishedSetData += handleFinishedSetData;
            proteinTableForm.StartingSetData += handleStartingSetData;
            peptideTableForm.PeptideViewFilter += handleViewFilter;
            peptideTableForm.FinishedSetData += handleFinishedSetData;
            peptideTableForm.StartingSetData += handleStartingSetData;
            modificationTableForm.ModificationViewFilter += handleViewFilter;
            modificationTableForm.FinishedSetData += handleFinishedSetData;
            modificationTableForm.StartingSetData += handleStartingSetData;
            analysisTableForm.AnalysisViewFilter += handleViewFilter;
            analysisTableForm.FinishedSetData += handleFinishedSetData;
            analysisTableForm.StartingSetData += handleStartingSetData;

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

        void clearProgress(string messageToClear)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker) (() => clearProgress(messageToClear)));
                return;
            }

            if (toolStripStatusLabel.Text != messageToClear)
                return;

            clearProgress();
        }

        void clearProgress()
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)(() => clearProgress()));
                return;
            }

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
                    "IDPicker files|*.idpDB;*.mzid;*.pepXML;*.pep.xml;*.xml",
                    "Importable files|*.mzid;*.pepXML;*.pep.xml;*.xml",
                    "MzIdentML files|*.mzid;*.xml",
                    "PepXML files|*.pepXML;*.pep.xml;*.xml",
                    //"IDPicker XML|*.idpXML",
                    "IDPicker DB|*.idpDB",
                    "All files|*.*"
                };

                var openFileDialog = new IDPOpenDialog(fileTypeList);

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    fileNames = openFileDialog.GetFileNames().Distinct().ToList();
					if (session != null && sender == importToolStripMenuItem)
						treeStructure = openFileDialog.GetTreeStructure(session);
					else
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

        public static string LocateSpectrumSource (string spectrumSourceName, string idpDbFilepath)
        {
            if (String.IsNullOrEmpty(spectrumSourceName))
                return String.Empty;

            try
            {
                return Util.FindSourceInSearchPath(spectrumSourceName, Path.GetDirectoryName(idpDbFilepath));
            }
            catch
            {
                try
                {
                    return Util.FindSourceInSearchPath(spectrumSourceName, Properties.GUI.Settings.Default.LastSpectrumSourceDirectory);
                }
                catch
                {
                    string result = String.Empty;

                    Program.MainWindow.Invoke(new MethodInvoker(() =>
                    {
                        var findDirectoryDialog = new FolderBrowserDialog()
                                                      {
                                                          SelectedPath = Properties.GUI.Settings.Default.LastSpectrumSourceDirectory,
                                                          ShowNewFolderButton = false,
                                                          Description = "Locate the directory containing the source \"" + spectrumSourceName + "\""
                                                      };

                        result = String.Empty;
                        while (findDirectoryDialog.ShowDialog() == DialogResult.OK)
                        {
                            try
                            {
                                string sourcePath = Util.FindSourceInSearchPath(spectrumSourceName, findDirectoryDialog.SelectedPath);
                                Properties.GUI.Settings.Default.LastSpectrumSourceDirectory = findDirectoryDialog.SelectedPath;
                                Properties.GUI.Settings.Default.Save();
                                result = sourcePath;
                                break;
                            }
                            catch
                            {
                                // couldn't find the source in that directory; prompt user again
                            }
                        }
                    }));

                    return result; // user canceled
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
                sourcePath = LocateSpectrumSource(source.Name, session.Connection.GetDataSource());
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
            form.SequenceCoverageFilter += handleViewFilter;
            form.FormClosed += (s, e2) => formSession.Dispose();
        }
        #endregion

        void handleViewFilter(object sender, DataFilter newViewFilter)
        {
            lock (this)
                if (mainViewsLoaded < 5)
                    return;

            if (breadCrumbControl.BreadCrumbs.Count(o => (DataFilter)o.Tag == newViewFilter) > 0)
                return;

            breadCrumbControl.BreadCrumbs.Add(new BreadCrumb(newViewFilter.ToString(), newViewFilter));

            // build a new DataFilter from the BreadCrumb list
            viewFilter = basicFilter + breadCrumbControl.BreadCrumbs.Select(o => o.Tag as DataFilter).Aggregate((x, y) => x + y);
            setData();
        }

        void breadCrumbControl_BreadCrumbClicked (object sender, BreadCrumbClickedEventArgs e)
        {
            lock (this)
                if (mainViewsLoaded < 5)
                    return;

			if (e.BreadCrumb == null && e.BreadCrumbList != null)
				foreach (var crumb in e.BreadCrumbList)
					breadCrumbControl.BreadCrumbs.Remove(crumb);
			else
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
            dataFiltersToolStripMenuRoot.Enabled = false;

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
                {
                    Program.HandleException(e.Result as Exception);
                    dataFiltersToolStripMenuRoot.Enabled = true;
                    return;
                }

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
            distributionStatisticsForm.ClearData();
            dockPanel.Contents.OfType<SequenceCoverageForm>().ForEach(o => o.ClearData());
            if (reassignPSMsForm != null) reassignPSMsForm.ClearData(true);
        }

        int mainViewsLoaded;
        void setData ()
        {
            mainViewsLoaded = 5;
            proteinTableForm.SetData(session, viewFilter);
            peptideTableForm.SetData(session.SessionFactory.OpenSession(), viewFilter);
            spectrumTableForm.SetData(session.SessionFactory.OpenSession(), viewFilter);
            modificationTableForm.SetData(session.SessionFactory.OpenSession(), viewFilter);
            analysisTableForm.SetData(session.SessionFactory.OpenSession(), viewFilter);

            fragmentationStatisticsForm.SetData(session, viewFilter);
            peakStatisticsForm.SetData(session, viewFilter);
            distributionStatisticsForm.SetData(session, viewFilter);
            dockPanel.Contents.OfType<SequenceCoverageForm>().ForEach(o => o.SetData(session, viewFilter));
            reassignPSMsForm.SetData(session, basicFilter);
        }

        void handleStartingSetData(object sender, EventArgs e)
        {
            BeginInvoke(new MethodInvoker(() =>
            {
                lock (this)
                {
                    --mainViewsLoaded;

                    if (mainViewsLoaded < 0)
                        throw new Exception("mainViewsLoaded < 0 after update from " + sender.ToString());

                    breadCrumbControl.Enabled = dataFiltersToolStripMenuRoot.Enabled = false;
                    progressMonitor_ProgressUpdate(this, new ProgressUpdateEventArgs()
                    {
                        Current = mainViewsLoaded,
                        Total = 5,
                        Message = String.Format("Loading main views ({0}/{1})...", mainViewsLoaded, 5)
                    });
                }
            }));
        }

        void handleFinishedSetData(object sender, EventArgs e)
        {
            BeginInvoke(new MethodInvoker(() =>
            {
                lock (this)
                {
                    ++mainViewsLoaded;

                    progressMonitor_ProgressUpdate(this, new ProgressUpdateEventArgs()
                    {
                        Current = mainViewsLoaded,
                        Total = 5,
                        Message = String.Format("Loading main views ({0}/{1})...", mainViewsLoaded, 5)
                    });

                    if (mainViewsLoaded == 5)
                        breadCrumbControl.Enabled = dataFiltersToolStripMenuRoot.Enabled = true;
                }
            }));
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
            distributionStatisticsForm.ClearSession();
            dockPanel.Contents.OfType<SequenceCoverageForm>().ForEach(o => { o.ClearSession(); o.Close(); });

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

        void showCommandLineHelp(object sender, EventArgs e)
        {
            string usage = "IDPicker.exe <idpDB/mzIdentML/pepXML import filemask>\r\n" +
                           "             [more import filemasks] ...\r\n" +
                           "             --headless (quits after merging/filtering)\r\n" +
                           "             -MaxQValue <real>\r\n" +
                           "             -MinDistinctPeptidesPerProtein <integer>\r\n" +
                           "             -MinSpectraPerProtein <integer>\r\n" +
                           "             -MinAdditionalPeptidesPerProtein <integer>\r\n" +
                           "             -MinSpectraPerDistinctMatch <integer>\r\n" +
                           "             -MinSpectraPerDistinctPeptide <integer>\r\n" +
                           "             -MaxProteinGroupsPerPeptide <integer>\r\n" +
                           "             -MergedOutputFilepath <string>\r\n";
            if (Program.IsHeadless)
                Console.Error.WriteLine("\r\nUsage:\r\n" + usage);
            else
                MessageBox.Show(usage, "Command-line Help");
        }

        void IDPickerForm_Load (object sender, EventArgs e)
        {
            checkForUpdatesAutomaticallyToolStripMenuItem.Checked = Properties.GUI.Settings.Default.AutomaticCheckForUpdates;

            //Get user layout profiles
            _layoutManager.CurrentLayout = _layoutManager.GetCurrentDefault();

            if (!Program.IsHeadless && args.IsNullOrEmpty())
                return;

            var filemasks = new List<string>();

            for (int i = 0; i < args.Count; ++i)
            {
                string arg = args[i];

                if (!arg.StartsWith("-"))
                {
                    filemasks.Add(arg);
                    continue;
                }

                if (arg == "--help")
                {
                    showCommandLineHelp(this, EventArgs.Empty);
                    Close();
                }

                try
                {
                    if (arg == "-MaxQValue")
                        defaultDataFilter.MaximumQValue = Convert.ToDouble(args[i + 1]);
                    else if (arg == "-MinDistinctPeptidesPerProtein")
                        defaultDataFilter.MinimumDistinctPeptidesPerProtein = Convert.ToInt32(args[i + 1]);
                    else if (arg == "-MinSpectraPerProtein")
                        defaultDataFilter.MinimumSpectraPerProtein = Convert.ToInt32(args[i + 1]);
                    else if (arg == "-MinAdditionalPeptidesPerProtein")
                        defaultDataFilter.MinimumAdditionalPeptidesPerProtein = Convert.ToInt32(args[i + 1]);
                    else if (arg == "-MinSpectraPerDistinctMatch")
                        defaultDataFilter.MinimumSpectraPerDistinctMatch = Convert.ToInt32(args[i + 1]);
                    else if (arg == "-MinSpectraPerDistinctPeptide")
                        defaultDataFilter.MinimumSpectraPerDistinctPeptide = Convert.ToInt32(args[i + 1]);
                    else if (arg == "-MaxProteinGroupsPerPeptide")
                        defaultDataFilter.MaximumProteinGroupsPerPeptide = Convert.ToInt32(args[i + 1]);
                    else if (arg == "-MergedOutputFilepath")
                        defaultMergedOutputFilepath = args[i + 1];
                    else
                    {
                        Program.HandleUserError(new Exception("unsupported parameter \"" + arg + "\""));
                        return;
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    Program.HandleUserError(new Exception("parameter \"" + arg + "\" requires an argument, e.g. \"" + arg + " 42\""));
                    return;
                }
                catch (FormatException)
                {
                    Program.HandleUserError(new Exception("unable to parse value \"" + args[i + 1] + "\" for parameter \"" + arg + "\""));
                    return;
                }

                ++i; // skip the next argument
            }

            var expandedFilepaths = new List<string>();
            foreach (string filemask in filemasks)
            {
                if (filemask.IndexOfAny("*?".ToCharArray()) != -1)
                    expandedFilepaths.AddRange(Directory.GetFiles(Path.GetDirectoryName(filemask), Path.GetFileName(filemask)));
                else
                    expandedFilepaths.Add(filemask);
            }

            var missingFiles = expandedFilepaths.Where(o => !File.Exists(o));
            if (missingFiles.Any())
            {
                Program.HandleUserError(new ArgumentException("some files do not exist: " + String.Join(" ", missingFiles.ToArray())));
                return;
            }

            new Thread(() => { OpenFiles(expandedFilepaths, null); }).Start();
        }

        /// <summary>
        /// Shows the user a SaveFileDialog (from the UI thread) for choosing a new location for an idpDB file.
        /// </summary>
        /// <returns>True if the dialog result is OK.</returns>
        bool saveFileDialog (ref string commonFilename, string title = null)
        {
            string filename = commonFilename;
            bool cancel = false;

            Invoke(new MethodInvoker(() =>
            {
                var sfd = new SaveFileDialog
                {
                    FileName = filename,
                    AddExtension = true,
                    RestoreDirectory = true,
                    DefaultExt = "idpDB",
                    Filter = "IDPicker Database|*.idpDB",
                    InitialDirectory = Path.GetDirectoryName(filename).IsNullOrEmpty() ? "" : Path.GetDirectoryName(filename)
                };

                if (!title.IsNullOrEmpty())
                    sfd.Title = title;

                if (sfd.ShowDialog() != DialogResult.OK)
                    cancel = true;
                else
                    filename = sfd.FileName;
            }));

            if (cancel)
                return false;

            commonFilename = filename;
            return true;
        }

        bool canReadWriteInDirectory(string path)
        {
            try
            {
                string randomTestFile = Path.Combine(path, Path.GetRandomFileName());
                using (var tmp = File.Create(randomTestFile))
                {
                }
                File.Delete(randomTestFile);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        void OpenFiles (IList<string> filepaths, TreeNode rootNode)
        {
            try
            {
                var xml_filepaths = filepaths.Where(filepath => !filepath.EndsWith(".idpDB"));
                var idpDB_filepaths = filepaths.Where(filepath => filepath.EndsWith(".idpDB"));
                bool openSingleFile = xml_filepaths.Count() + idpDB_filepaths.Count() == 1;

                if (xml_filepaths.Count() + idpDB_filepaths.Count() == 0)
                {
                    if (Program.IsHeadless)
                    {
                        Console.Error.WriteLine("Headless mode must be passed some idpDB files to merge.");
                        Close();
                    }
                    else
                        MessageBox.Show("Select one or more idpDB, mzIdentML, pepXML, or idpXML files to create an IDPicker report.", "No IDPicker files selected");
                    return;
                }

                if (Program.IsHeadless && xml_filepaths.Any())
                    Program.HandleUserError(new Exception("headless mode only supports merging idpDB files"));

                // warn if idpDBs already exist
                bool warnOnce = false, skipReconvert = false;
                var skipFiles = new List<string>();
                foreach (string filepath in xml_filepaths)
                {
                    string idpDB_filepath = Path.ChangeExtension(filepath.Replace(".pep.xml", ".pepXML"), ".idpDB");
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
                idpDB_filepaths = idpDB_filepaths.Union(skipFiles.Select(o => Path.ChangeExtension(o.Replace(".pep.xml", ".pepXML"), ".idpDB")));


                // determine if merged filepath exists and that it's a valid idpDB
                var potentialPaths = filepaths.Select(item =>
                                                      Path.Combine(Path.GetDirectoryName(item) ?? string.Empty,
                                                                   Path.GetFileNameWithoutExtension(item) ??

                                                                   string.Empty) + ".idpDB").ToList();

                string commonFilepath = Util.GetCommonFilename(filepaths);
                if (!openSingleFile && potentialPaths.Contains(commonFilepath))
                    commonFilepath = commonFilepath.Replace(".idpDB", " (merged).idpDB");
                string mergeTargetFilepath = defaultMergedOutputFilepath ?? commonFilepath;
                if (File.Exists(mergeTargetFilepath) && Program.IsHeadless)
                    File.Delete(mergeTargetFilepath);
                else
                {
                    // check that the single idpDB is writable; if not, it needs to be copied
                    if (openSingleFile)
                    {
                        string oldFilename = mergeTargetFilepath;

                        while (true)
                        {
                            if (canReadWriteInDirectory(Path.GetDirectoryName(mergeTargetFilepath)))
                                break;

                            MessageBox.Show("IDPicker files cannot be opened from a read-only location, pick a writable path to copy it to.");

                            if (!saveFileDialog(ref mergeTargetFilepath))
                                return;
                        }

                        // if location was changed, copy to the new location
                        if (oldFilename != mergeTargetFilepath)
                        {
                            toolStripStatusLabel.Text = "Copying idpDB...";
                            File.Copy(oldFilename, mergeTargetFilepath, true);
                        }
                    }
                    else
                    {
                        // if not headless and MergedOutputFilepath is unset,
                        // then give the user a chance to override the merge target location
                        if (!Program.IsHeadless &&
                            defaultMergedOutputFilepath == null &&
                            !saveFileDialog(ref mergeTargetFilepath, "Choose where to create the merged idpDB."))
                            return;

                        while (true)
                        {
                            if (!canReadWriteInDirectory(Path.GetDirectoryName(mergeTargetFilepath)))
                            {
                                MessageBox.Show("IDPicker files cannot be merged to a read-only location, pick a writable path.");

                                if (Program.IsHeadless || !saveFileDialog(ref mergeTargetFilepath, "Pick a writable path in which to create the merged idpDB."))
                                    return;

                                continue;
                            }

                            // the SaveFileDialog already asked the user to confirm overwriting an existing file
                            if (File.Exists(mergeTargetFilepath))
                                File.Delete(mergeTargetFilepath);

                            break;
                        }
                    }
                }

                // set main window title
                BeginInvoke(new MethodInvoker(() => Text = mergeTargetFilepath));

                //set up delayed messages so non-fatal errors that occur at the end arent lost
                var delayedMessages = new List<string[]>();
                if (xml_filepaths.Count() > 0)
                {
                    importCancelled = false;

                    // loop until the import settings don't result in any fatal errors, or user cancels
                    while (!importCancelled)
                    {
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
                                delayedMessages.Add(new[] { message[0], message[1] });
                            };

                        Invoke(new MethodInvoker(() => progressForm.Show(this)));

                        try
                        {
                            parser.Parse(xml_filepaths, ilr);
                            break; // no fatal errors, break the loop
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("no peptides found mapping to a decoy protein") ||
                                ex.Message.Contains("peptides did not map to the database") ||
                                ex.Message.Contains("duplicate protein id"))
                                Program.HandleUserError(ex);
                            else
                                throw;
                        }
                        finally
                        {
                            importCancelled |= progressForm.Cancelled;
                            Invoke(new MethodInvoker(() => progressForm.Close()));
                        }
                    }

                    if (importCancelled)
                        return;

                    idpDB_filepaths = idpDB_filepaths.Union(xml_filepaths.Select(o => Path.ChangeExtension(o.Replace(".pep.xml", ".pepXML"), ".idpDB")));
                }

                if (idpDB_filepaths.Count() > 1)
                {
                    var merger = new Merger(mergeTargetFilepath, idpDB_filepaths);
                    toolStripStatusLabel.Text = "Merging results...";
                    merger.MergingProgress += progressMonitor.UpdateProgress;
                    merger.Start();

                    idpDB_filepaths = new List<string>() {mergeTargetFilepath};
                }

                // HACK: this needs to be handled more gracefully
                if (!IsHandleCreated)
                    return;

                if (Properties.GUI.Settings.Default.WarnAboutNonFixedDrive &&
                    DriveType.Fixed != new DriveInfo(Path.GetPathRoot(mergeTargetFilepath)).DriveType)
                {
                    string oldFilename = mergeTargetFilepath;
                    bool copyLocal = true;
                    Invoke(new MethodInvoker(() =>
                                                 {
                                                     var form = new NonFixedDriveWarningForm();
                                                     if (form.ShowDialog(this) == DialogResult.Ignore)
                                                         copyLocal = false;
                                                 }));

                    if (copyLocal)
                    {
                        string newFilename = Path.GetFileName(mergeTargetFilepath);
                        if (!saveFileDialog(ref newFilename, "Pick a local path to copy the idpDB to."))
                            return;

                        toolStripStatusLabel.Text = "Copying idpDB...";
                        File.Copy(oldFilename, newFilename, true);
                        mergeTargetFilepath = newFilename;
                    }
                }

                if (!IsHandleCreated)
                    return;

                Util.PrecacheFile(mergeTargetFilepath, progressMonitor.UpdateProgress);

                if (!IsHandleCreated)
                    return;

                BeginInvoke(new MethodInvoker(() =>
                {
                    clearProgress();
                    toolStripStatusLabel.Text = "Upgrading schema and creating session factory...";
                    statusStrip.Refresh();
                }));

                var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(mergeTargetFilepath, new SessionFactoryConfig { WriteSqlToConsoleOut = true });

                BeginInvoke(new MethodInvoker(() =>
                {
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
                    decimal roundToNearest = 1m;
                    var distinctModificationFormat = new DistinctMatchFormat();
                    var modMasses = session.CreateQuery("SELECT DISTINCT mod.MonoMassDelta FROM Modification mod").List<double>();
                    for (int i = 4; i > 0; --i)
                    {
                        distinctModificationFormat.ModificationMassRoundToNearest = (decimal) (1.0 / Math.Pow(10, i));
                        if (modMasses.Select(o => distinctModificationFormat.Round(o)).Distinct().Count() < 30)
                        {
                            roundToNearest = distinctModificationFormat.ModificationMassRoundToNearest.Value;
                            break;
                        }
                    }
                    modificationTableForm.RoundToNearest = roundToNearest;

                    basicFilter = DataFilter.LoadFilter(session);

                    if (basicFilter == null)
                    {
                        basicFilter = new DataFilter(defaultDataFilter);
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

                    if (logForm != null) Invoke(new MethodInvoker(() => logForm.Show(dockPanel, DockState.DockBottomAutoHide)));
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

            while (true)
            {
                var result = UserDialog.Show(this, "Import Settings", new ImportSettingsControl(e.DistinctAnalyses, showQonverterSettingsManager));

                if (result == DialogResult.Cancel)
                {
                    importCancelled = e.Cancel = result == DialogResult.Cancel;
                    break;
                }

                if (e.DistinctAnalyses.Any(o => String.IsNullOrEmpty(o.importSettings.qonverterSettings.DecoyPrefix)))
                {
                    MessageBox.Show("Decoy prefix cannot be empty.", "Error");
                    continue;
                }

                var missingDatabases = e.DistinctAnalyses.Where(o => !File.Exists(o.importSettings.proteinDatabaseFilepath));
                if (missingDatabases.Any())
                {
                    MessageBox.Show("Protein database(s) not found:\r\n" +
                                    String.Join("\r\n", missingDatabases.Select(o => o.importSettings.proteinDatabaseFilepath).Distinct()));
                    continue;
                }

                break;
            }
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

            // HACK: until the "invalid string binding" error is resolved, this will prevent an error dialog at exit;
            //       but it also prevents automatic cleanup of TemporaryMSDataFile objects, which can build up in %TEMP%
            TemporaryMSDataFile.ForceCleanup();
            Process.GetCurrentProcess().Kill();
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
            var form = new DefaultSettingsManagerForm { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(this);
        }

        private void ShowQonverterSettings(object sender, EventArgs e)
        {
            if (session == null)
                return;

            lock (session)
            {
                var oldSettings = session.Query<QonverterSettings>().ToDictionary(o => session.Get<Analysis>(o.Id));

                bool cancel;
                var qonverterSettings = qonverterSettingsHandler(oldSettings, out cancel);
                if (cancel)
                    return;

                var qonverter = new Qonverter();
                qonverter.QonversionProgress += progressMonitor.UpdateProgress;
                qonverterSettingsByAnalysis = session.Query<QonverterSettings>().ToDictionary(o => session.Get<Analysis>(o.Id));
                foreach (var item in qonverterSettings)
                    qonverter.SettingsByAnalysis[(int) item.Key.Id] = item.Value.ToQonverterSettings();

                //qonverter.LogQonversionDetails = true;
                try
                {
                    clearData();
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
                var allGroupedProteins = proteinGroup.Proteins.Split(",".ToCharArray());
                foreach (var protein in allGroupedProteins)
                    foreach (var peptideIndex in proteinAccessionToPeptideList[protein])
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
            MessageBox.Show(String.Format("IDPicker {0} {1}\r\n" +
                                          "Copyright 2012 Vanderbilt University\r\n" +
                                          "Developers: Matt Chambers, Jay Holman, Surendra Dasari, Zeqiang Ma\r\n" +
                                          "Thanks to: David Tabb",
                                          Util.Version, Environment.Is64BitProcess ? "64-bit" : "32-bit"),
                            "About IDPicker");
        }

        private void embedSpectraToolStripMenuItem_Click (object sender, EventArgs e)
        {
            if (session == null)
                return;

            var form = new EmbedderForm(session) { StartPosition = FormStartPosition.CenterParent };
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                basicFilter.RecalculateAggregateQuantitationData(session);
                clearData();
                setData();
            }
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

            string quasitelFilepath = Path.Combine(Application.StartupPath, "QuasiTel.R");
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