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
using pwiz.Common.Collections;
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
        FilterHistoryForm filterHistoryForm;
        PTMAttestationForm ptmAttestationForm;

        IList<IPersistentForm> persistentForms;

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

        private string defaultApplySourceGroupHierarchy = null;

        public bool TestUILayout { get; private set; }

        public IDPickerForm (IList<string> args)
        {
            InitializeComponent();

            toolStripStatusLabel.Text = "Initializing...";

            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;

            this.args = args;

            if (Properties.GUI.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.Save();

                Properties.GUI.Settings.Default.Upgrade();
                Properties.GUI.Settings.Default.UpgradeRequired = false;
                Properties.GUI.Settings.Default.Save();
            }

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
            basicFilterControl.ApplyFilterChanges += basicFilterControl_ApplyFilterChanges;
            basicFilterControl.ShowQonverterSettings += ShowQonverterSettings;
            basicFilterControl.CropAssembly += CropAssembly;
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

            ptmAttestationForm = new PTMAttestationForm(this);
            ptmAttestationForm.Show(dockPanel, DockState.DockBottomAutoHide);
            ptmAttestationForm.AutoHidePortion = 0.5;

            spectrumTableForm = new SpectrumTableForm();
            spectrumTableForm.Show(dockPanel, DockState.DockLeft);
            spectrumTableForm.AutoHidePortion = 0.5;

            proteinTableForm = new ProteinTableForm();
            proteinTableForm.Show(dockPanel, DockState.DockTop);
            proteinTableForm.AutoHidePortion = 0.5;

            peptideTableForm = new PeptideTableForm();
            peptideTableForm.Show(dockPanel, DockState.DockTop);
            peptideTableForm.AutoHidePortion = 0.5;

            proteinTableForm.Activate();

            modificationTableForm = new ModificationTableForm();
            modificationTableForm.Show(dockPanel, DockState.Document);
            modificationTableForm.AutoHidePortion = 0.5;

            analysisTableForm = new AnalysisTableForm();
            analysisTableForm.Show(dockPanel, DockState.Document);
            analysisTableForm.AutoHidePortion = 0.5;

            filterHistoryForm = new FilterHistoryForm();
            filterHistoryForm.Show(dockPanel, DockState.Document);
            filterHistoryForm.AutoHidePortion = 0.5;

            spectrumTableForm.SpectrumViewFilter += handleViewFilter;
            spectrumTableForm.SpectrumViewVisualize += spectrumTableForm_SpectrumViewVisualize;
            spectrumTableForm.IsobaricMappingChanged += spectrumTableForm_IsobaricMappingChanged;
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
            filterHistoryForm.LoadPersistentDataFilter += handleLoadPersistentDataFilter;
            //filterHistoryForm.FinishedSetData += handleFinishedSetData;
            //filterHistoryForm.StartingSetData += handleStartingSetData;

            // hide DockPanel before initializing layout manager
            dockPanel.Visible = false;
            dockPanel.ShowDocumentIcon = true;

            persistentForms = new IPersistentForm[]
            {
                proteinTableForm,
                peptideTableForm,
                spectrumTableForm,
                analysisTableForm,
                filterHistoryForm
            };

            _layoutManager = new LayoutManager(this, dockPanel, persistentForms);

            // load last or default location and size
            _layoutManager.LoadMainFormSettings();

            // certain features are only enabled for development builds
            if (Application.ExecutablePath.Contains("build-nt-x86"))
            {
                // provide SQL logging for development builds
                logForm = new LogForm();
                logForm.AutoHidePortion = 0.25;
                Console.SetOut(logForm.LogWriter);

                developerToolStripMenuItem.ForeColor = SystemColors.MenuBar;
            }
            else
            {
                Console.SetOut(TextWriter.Null);

                developerToolStripMenuItem.Visible = false;
            }

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

            toolStripStatusLabel.Text = e.Message.Length > 0 ? (e.Message[0].ToString().ToUpper() + e.Message.Substring(1)) : String.Empty;
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
                    Thread.Sleep(200);
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
                    "IDPicker files|*.idpDB;*.mzid;*.pepXML;*.pep.xml;*.xml;*.dat",
                    "Importable files|*.mzid;*.pepXML;*.pep.xml;*.xml;*.dat",
                    "MzIdentML files|*.mzid;*.xml",
                    "PepXML files|*.pepXML;*.pep.xml;*.xml",
                    "Mascot files|*.dat",
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
            basicFilterControl.ApplyFilterChanges += basicFilterControl_ApplyFilterChanges;
            basicFilterControl.ShowQonverterSettings += ShowQonverterSettings;
            basicFilterControl.CropAssembly += CropAssembly;
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
                            catch (ArgumentException e)
                            {
                                // couldn't find the source in that directory; prompt user again
                                MessageBox.Show(e.Message);
                            }
                            catch(Exception e)
                            {
                                Program.HandleException(e);
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
            pwiz.CLI.chemistry.MZTolerance tolerance = null;

            if (manager.CurrentGraphForm != null && annotationByGraphForm[manager.CurrentGraphForm] != null)
            {
                var panel = PeptideFragmentationAnnotation.annotationPanels;
                showFragmentationLadders = panel.showFragmentationLaddersCheckBox.Checked;
                showMissedFragments = panel.showMissesCheckBox.Checked;
                showFragmentationSummary = panel.showFragmentationSummaryCheckBox.Checked;
                if (panel.fragmentToleranceTextBox.Text.Length > 0)
                {
                    tolerance = new pwiz.CLI.chemistry.MZTolerance();
                    tolerance.value = Convert.ToDouble(panel.fragmentToleranceTextBox.Text);
                    tolerance.units = (pwiz.CLI.chemistry.MZTolerance.Units) panel.fragmentToleranceUnitsComboBox.SelectedIndex;
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

            manager.OpenFile(sourcePath, new List<object> { spectrum.NativeID }, annotation, spectrumListFilters);
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

            spectrumTableForm_SpectrumViewVisualize(this, new SpectrumViewVisualizeEventArgs(session, psmRow));
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

        void spectrumTableForm_IsobaricMappingChanged(object sender, EventArgs e)
        {
            proteinTableForm.ClearData(true);
            peptideTableForm.ClearData(true);
            proteinTableForm.SetData(session, viewFilter);
            peptideTableForm.SetData(session.SessionFactory.OpenSession(), viewFilter);
        }

        void handleViewFilter(object sender, ViewFilterEventArgs e)
        {
            lock (this)
                if (mainViewsLoaded < 5)
                    return;

            if (breadCrumbControl.BreadCrumbs.Count(o => (DataFilter)o.Tag == e.ViewFilter) > 0)
                return;

            breadCrumbControl.BreadCrumbs.Add(new BreadCrumb(e.ViewFilter.ToString(), e.ViewFilter));

            // build a new DataFilter from the BreadCrumb list
            viewFilter = basicFilter + breadCrumbControl.BreadCrumbs.Select(o => o.Tag as DataFilter).Aggregate((x, y) => x + y);
            setData();
        }

        void handleLoadPersistentDataFilter(object sender, LoadPersistentDataFilterEventArgs e)
        {
            lock (this)
                if (mainViewsLoaded < 5)
                    return;

            basicFilter.PersistentDataFilter = e.PersistentDataFilter;
            basicFilterControl.DataFilter = basicFilter;
            ApplyBasicFilter();
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
                if (Program.IsHeadless)
                    Close();

                if (e.Result is Exception)
                {
                    Program.HandleException(e.Result as Exception);
                    setControlsWhenDatabaseLocked(false);
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
        }
        
        public void CropAssembly ()
        {
            if (MessageBox.Show("Cropping the assembly will permanently remove all proteins that are not in the current data filter, as well as the PSMs, peptides, and spectra mapping to those proteins. " +
                                "It will also clear the filter history, so you may wish to save a copy of that first.\r\n\r\n" +
                                "Because this action cannot be undone, we recommend making a backup copy of the idpDB first. Are you sure?",
                                "Crop assembly confirmation",
                                MessageBoxButtons.YesNo) == DialogResult.No)
                return;

            toolStripStatusLabel.Text = "Cropping assembly...";
            basicFilter.FilteringProgress += progressMonitor.UpdateProgress;

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += cropAssemblyAsync;

            workerThread.RunWorkerCompleted += (s, e) =>
            {
                if (Program.IsHeadless)
                    Close();

                if (e.Result is Exception)
                {
                    Program.HandleException(e.Result as Exception);
                    setControlsWhenDatabaseLocked(false);
                    return;
                }

                basicFilter.FilteringProgress -= progressMonitor.UpdateProgress;
                
                setControlsWhenDatabaseLocked(false);
            };
                
            setControlsWhenDatabaseLocked(true);
            workerThread.RunWorkerAsync();
        }

        void cropAssemblyAsync (object sender, DoWorkEventArgs e)
        {
            try
            {
                lock (session)
                    basicFilter.CropAssembly(session);
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        void clearData ()
        {
            setControlsWhenDatabaseLocked(true);

            proteinTableForm.ClearData(true);
            peptideTableForm.ClearData(true);
            spectrumTableForm.ClearData(true);
            modificationTableForm.ClearData(true);
            analysisTableForm.ClearData(true);
            filterHistoryForm.ClearData(true);
            reassignPSMsForm.ClearData(true);

            fragmentationStatisticsForm.ClearData(true);
            peakStatisticsForm.ClearData(true);
            distributionStatisticsForm.ClearData(true);

            dockPanel.Contents.OfType<SequenceCoverageForm>().ForEach(o => o.ClearData());
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
            filterHistoryForm.SetData(session.SessionFactory.OpenSession(), basicFilter);
            reassignPSMsForm.SetData(session.SessionFactory.OpenSession(), basicFilter);

            fragmentationStatisticsForm.SetData(session, viewFilter);
            peakStatisticsForm.SetData(session, viewFilter);
            distributionStatisticsForm.SetData(session, viewFilter);

            dockPanel.Contents.OfType<SequenceCoverageForm>().ForEach(o => o.SetData(session, viewFilter));
            ptmAttestationForm.SetData(session, basicFilter);
        }

        void setControlsWhenDatabaseLocked(bool isLocked)
        {
            breadCrumbControl.Enabled = !isLocked;
            basicFilterControl.Enabled = !isLocked;
            geneMetadataToolStripMenuItem.Enabled = !isLocked;
            layoutToolStripMenuRoot.Enabled = !isLocked;
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

                    setControlsWhenDatabaseLocked(true);
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
                        setControlsWhenDatabaseLocked(false);
                }
            }));
        }

        void clearSession()
        {
            setControlsWhenDatabaseLocked(true);

            proteinTableForm.ClearSession();
            peptideTableForm.ClearSession();
            spectrumTableForm.ClearSession();
            modificationTableForm.ClearSession();
            analysisTableForm.ClearSession();
            reassignPSMsForm.ClearSession();
            filterHistoryForm.ClearSession();

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
                           "             -MinDistinctPeptides <integer>\r\n" +
                           "             -MinSpectra <integer>\r\n" +
                           "             -MinAdditionalPeptides <integer>\r\n" +
                           "             -MinSpectraPerDistinctMatch <integer>\r\n" +
                           "             -MinSpectraPerDistinctPeptide <integer>\r\n" +
                           "             -MaxProteinGroupsPerPeptide <integer>\r\n" +
                           "             -MergedOutputFilepath <string>\r\n" +
                           "             -ApplySourceGroupHierarchy <filepath>\r\n";
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
                else if (arg == "--test")
                {
                    try
                    {
                        // test that calls to ProteoWizard work
                        var test = new pwiz.CLI.msdata.MSData();
                        Console.WriteLine("ProteoWizard MSData {0}", pwiz.CLI.msdata.Version.ToString());
                    }
                    catch
                    {
                        Console.Error.WriteLine("Unable to load ProteoWizard. Are some DLLs missing?");
                    }
                    Close();
                }
                else if (arg == "--test-ui-layout")
                {
                    TestUILayout = true;
                    continue;
                }

                try
                {
                    if (arg == "-MaxQValue")
                        defaultDataFilter.MaximumQValue = Convert.ToDouble(args[i + 1]);
                    else if (arg == "-MinDistinctPeptides")
                        defaultDataFilter.MinimumDistinctPeptides = Convert.ToInt32(args[i + 1]);
                    else if (arg == "-MinSpectra")
                        defaultDataFilter.MinimumSpectra = Convert.ToInt32(args[i + 1]);
                    else if (arg == "-MinAdditionalPeptides")
                        defaultDataFilter.MinimumAdditionalPeptides = Convert.ToInt32(args[i + 1]);
                    else if (arg == "-MinSpectraPerDistinctMatch")
                        defaultDataFilter.MinimumSpectraPerDistinctMatch = Convert.ToInt32(args[i + 1]);
                    else if (arg == "-MinSpectraPerDistinctPeptide")
                        defaultDataFilter.MinimumSpectraPerDistinctPeptide = Convert.ToInt32(args[i + 1]);
                    else if (arg == "-MaxProteinGroupsPerPeptide")
                        defaultDataFilter.MaximumProteinGroupsPerPeptide = Convert.ToInt32(args[i + 1]);
                    else if (arg == "-MergedOutputFilepath")
                        defaultMergedOutputFilepath = args[i + 1];
                    else if (arg == "-ApplySourceGroupHierarchy")
                        defaultApplySourceGroupHierarchy = args[i + 1];
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

            SchemaUpdater.SetGroupConcatSeparator(Properties.Settings.Default.GroupConcatSeparator);

            // if program is headless continue into OpenFiles even without any files; error will be issued there
            if (!Program.IsHeadless && filemasks.IsNullOrEmpty())
            {
                toolStripStatusLabel.Text = "Ready";
                return;
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

            new Thread(() =>
            {
                OpenFiles(expandedFilepaths, null);
            }).Start();
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
                while (true)
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

                    if (sfd.ShowDialog(this) != DialogResult.OK)
                        cancel = true;
                    else
                    {
                        filename = sfd.FileName;

                        // if the file exists, make sure it's not locked by trying to rename it (the dialog already asked the user if they want to overwrite it)
                        if (File.Exists(filename))
                            try
                            {
                                string randomFilename = filename + Path.GetRandomFileName();
                                File.Move(filename, randomFilename);
                                File.Move(randomFilename, filename);
                            }
                            catch (IOException)
                            {
                                MessageBox.Show("The existing file is locked and cannot be overwritten, please close the program(s) using it or pick another file name.", "Existing File Locked");
                                continue;
                            }
                    }

                    break;
                }
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

        string outputFilepath(string inputFilepath)
        {
            string outputFilename = Path.GetFileNameWithoutExtension(inputFilepath.Replace(".pep.xml", ".pepXML")) + ".idpDB";

            // for Mascot files (*.dat), use parseSource() to get the real filename, else save time by just using filename without extension
            if (inputFilepath.ToLowerInvariant().EndsWith(".dat"))
                outputFilename = Parser.ParseSource(inputFilepath) + ".idpDB";

            return Path.Combine(Path.GetDirectoryName(inputFilepath) ?? string.Empty, outputFilename);
        }

        void OpenFiles (IList<string> filepaths, TreeNode rootNode = null)
        {
            try
            {
                var xml_filepaths = filepaths.Where(filepath => !filepath.ToLower().EndsWith(".idpdb"));
                var idpDB_filepaths = filepaths.Where(filepath => filepath.ToLower().EndsWith(".idpdb"));
                bool openSingleFile = xml_filepaths.Count() + idpDB_filepaths.Count() == 1;

                if (xml_filepaths.Count() + idpDB_filepaths.Count() == 0)
                {
                    if (Program.IsHeadless)
                    {
                        Console.Error.WriteLine("Headless mode must be passed some idpDB files to merge.");
                        Close();
                        return;
                    }
                    else
                        throw new Exception("no filepaths to open");
                }

                if (Program.IsHeadless && xml_filepaths.Any())
                    Program.HandleUserError(new Exception("headless mode only supports merging and filtering idpDB files"));

                // warn if idpDBs already exist
                bool warnOnce = false, skipReconvert = false;
                var skipFiles = new List<string>();
                foreach (string filepath in xml_filepaths)
                {
                    string idpDB_filepath = outputFilepath(filepath);
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
                idpDB_filepaths = idpDB_filepaths.Union(skipFiles.Select(outputFilepath));


                // determine if merged filepath exists and that it's a valid idpDB
                var potentialPaths = filepaths.Select(item =>
                    Path.Combine(Path.GetDirectoryName(item) ?? string.Empty,
                        Path.GetFileNameWithoutExtension(item) ??

                        string.Empty) + ".idpDB").ToList();

                // for Mascot files (*.dat), use parseSource() to get the real filename, else save time by just using filename without extension
                var sourceNames = filepaths.Select(outputFilepath);

                string commonFilepath = Util.GetCommonFilename(sourceNames);
                if (!openSingleFile && potentialPaths.Contains(commonFilepath))
                    commonFilepath = commonFilepath.Replace(".idpDB", " (merged).idpDB");
                string mergeTargetFilepath = defaultMergedOutputFilepath ?? commonFilepath;
                if (!openSingleFile && File.Exists(mergeTargetFilepath) && Program.IsHeadless)
                    File.Delete(mergeTargetFilepath);
                else
                {
                    // check that the single idpDB is writable; if not, it needs to be copied
                    if (openSingleFile)
                    {
                        // sanity check that file exists after the path manipulation above
                        if (idpDB_filepaths.Count() == 1 && !File.Exists(mergeTargetFilepath))
                            throw new Exception(String.Format("error in internal path manipulation for opening single idpDB: {0} transformed to {1} which does not exist", sourceNames.First(), mergeTargetFilepath));

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
                            !saveFileDialog(ref mergeTargetFilepath, "Choose where to create the merged idpDB"))
                            return;

                        while (true)
                        {
                            if (!canReadWriteInDirectory(Path.GetDirectoryName(mergeTargetFilepath)))
                            {
                                MessageBox.Show("IDPicker files cannot be merged to a read-only location, pick a writable path.");

                                if (Program.IsHeadless || !saveFileDialog(ref mergeTargetFilepath, "Pick a writable path in which to create the merged idpDB"))
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

                        Invoke(new MethodInvoker(() => progressForm.Show(this)));

                        try
                        {
                            parser.Parse(xml_filepaths, 2, ilr);

                            // read log for non-fatal errors
                            //string log = Logger.Reader.ReadToEnd().Trim();
                            //if (log.Length > 0)
                            //    Invoke(new MethodInvoker(() => UserDialog.Show(this, "Log Messages", new TextBox {Multiline = true, Text = log.Replace("\n", "\r\n"), ReadOnly = true, Size = new Size(800, 600),  ScrollBars = ScrollBars.Both}, MessageBoxButtons.OK)));

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

                    idpDB_filepaths = idpDB_filepaths.Union(xml_filepaths.Select(outputFilepath));
                }

                if (idpDB_filepaths.Count() > 1)
                {
                    var merger = new MergerWrapper(mergeTargetFilepath, idpDB_filepaths);
                    toolStripStatusLabel.Text = "Merging results...";
                    merger.MergingProgress += progressMonitor.UpdateProgress;

                    try
                    {
                        merger.Start();
                        idpDB_filepaths = new List<string>() {mergeTargetFilepath};
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("same peptide maps to different sets of proteins"))
                        {
                            Program.HandleUserError(ex);
                            return;
                        }
                        else
                            throw;
                    }
                }

                // HACK: this needs to be handled more gracefully
                if (!IsHandleCreated)
                    return;

                if (Properties.GUI.Settings.Default.WarnAboutNonFixedDrive && !Util.IsPathOnFixedDrive(mergeTargetFilepath))
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
                        if (!saveFileDialog(ref newFilename, "Pick a local path to copy the idpDB to"))
                            return;

                        toolStripStatusLabel.Text = "Copying idpDB...";
                        File.Copy(oldFilename, newFilename, true);
                        mergeTargetFilepath = newFilename;

                        // set main window title
                        BeginInvoke(new MethodInvoker(() => Text = mergeTargetFilepath));
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

                var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(mergeTargetFilepath, new SessionFactoryConfig {WriteSqlToConsoleOut = true});
                if (logForm != null) logForm.SetSessionFactory(sessionFactory);

                BeginInvoke(new MethodInvoker(() =>
                {
                    // reload qonverter settings because the ids may change after merging
                    toolStripStatusLabel.Text = "Loading qonverter settings...";
                    statusStrip.Refresh();
                    session = sessionFactory.OpenSession();
                    session.DefaultReadOnly = true;

                    session.CreateSQLQuery("PRAGMA temp_store=MEMORY; PRAGMA mmap_size=70368744177664; -- 2^46").ExecuteUpdate();

                    toolStripStatusLabel.Text = "Refreshing group structure...";
                    statusStrip.Refresh();
                    var usedGroups = GroupingControlForm.SetInitialStructure(rootNode, session, defaultApplySourceGroupHierarchy);
                    if (usedGroups != null && usedGroups.Any())
                    {
                        var allGroupsByName = session.Query<SpectrumSourceGroup>().ToDictionary(o => o.Name);
                        var usedGroupsByName = usedGroups.ToDictionary(o => o.Name);

                        // if usedGroupsByName does not contain a key from allGroupsByName, delete the group
                        foreach (var unusedGroup in allGroupsByName.Where(o => !usedGroupsByName.ContainsKey(o.Key)))
                            session.Delete(unusedGroup);
                    }
                    session.Flush();

                    // check for embedded gene metadata;
                    // if it isn't there, ask the user if they want to embed it;
                    // if not, disable gene-related features
                    if (!Program.IsHeadless && !Embedder.HasGeneMetadata(mergeTargetFilepath) && Properties.GUI.Settings.Default.WarnAboutNoGeneMetadata)
                    {
                        bool embedGeneMetadata = true;
                        Invoke(new MethodInvoker(() =>
                        {
                            var form = new EmbedGeneMetadataWarningForm();
                            if (form.ShowDialog(this) == DialogResult.Ignore)
                                embedGeneMetadata = false;
                        }));

                        if (embedGeneMetadata)
                        {
                            loadRefSeqGeneMetadata(); // will call OpenFiles() after embedding, so return immediately
                            return;
                        }
                    }
                    else
                    {
                        // disable gene-related features
                    }


                    qonverterSettingsByAnalysis = session.Query<QonverterSettings>().ToDictionary(o => session.Get<Analysis>(o.Id));

                    _layoutManager.SetSession(session);

                    //set or save default layout
                    dockPanel.Visible = true;
                    _layoutManager.CurrentLayout = _layoutManager.GetCurrentDefault();

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

                    // if user has overridden filters from the command-line, make sure to reapply the filter
                    if (!defaultDataFilter.PersistentDataFilter.Equals(defaultDataFilter.OriginalPersistentDataFilter))
                        basicFilter = null;

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

                    if (TestUILayout)
                    {
                        int i = 0;
                        foreach (var form in dockPanel.Contents)
                        {
                            ++i;
                            form.DockingHandler.DockAreas = (form.DockingHandler.DockAreas | DockAreas.Float);
                            var rect = dockPanel.ClientRectangle;
                            rect.Offset(i * 15, i * 15);
                            rect.Size = new System.Drawing.Size(960, 600);
                            form.DockingHandler.Show(dockPanel, rect);
                        }
                    }

                    toolStripStatusLabel.Text = "Ready";
                    Activate();
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
            finally
            {
                BeginInvoke(new MethodInvoker(() => { clearProgress(); }));
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
            //catch(InvalidOperationException e)
            //catch(IndexOutOfRangeException e)
            catch(Exception e)
            {
                if (userLayout.Name == "System Default")
                    throw new IndexOutOfRangeException("error setting system default layout", e);

                MessageBox.Show(this, "Error loading layout. Reverting to system default.", "Layout Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _layoutManager.CurrentLayout = _layoutManager.DefaultSystemLayout;
            }
            finally
            {
                File.Delete(tempFilepath);
            }

            if (userLayout.HasCustomColumnSettings)
                foreach (var form in persistentForms)
                    form.LoadLayout(userLayout.FormProperties[form.Name]);
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
            if (persistantString == typeof(FilterHistoryForm).ToString())
                return filterHistoryForm;
            if (persistantString == typeof(RescuePSMsForm).ToString())
                return reassignPSMsForm;
            if (persistantString == typeof(FragmentationStatisticsForm).ToString())
                return fragmentationStatisticsForm;
            if (persistantString == typeof(PeakStatisticsForm).ToString())
                return peakStatisticsForm;
            if (persistantString == typeof(DistributionStatisticsForm).ToString())
                return distributionStatisticsForm;
            if (persistantString == typeof(PTMAttestationForm).ToString())
                return ptmAttestationForm;
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
                OpenFiles(database);
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
            //Process.GetCurrentProcess().Kill();
            Environment.Exit(0);
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

        private void basicFilterControl_ApplyFilterChanges (object sender, EventArgs e)
        {
            dataFilterPopup.Close(ToolStripDropDownCloseReason.ItemClicked);
        }

        void dataFilterPopup_Closed (object sender, ToolStripDropDownClosedEventArgs e)
        {
            // if user pressed escape or changed focus to an application other than IDPicker, don't apply filters and reset
            if (e.CloseReason == ToolStripDropDownCloseReason.Keyboard ||
                e.CloseReason == ToolStripDropDownCloseReason.AppFocusChange)
            {
                dirtyFilterControls = false;
                basicFilterControl.DataFilter = basicFilter;
                return;
            }

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
            // save old rollup method to check whether it changed
            var oldRollupMethod = Properties.GUI.Settings.Default.QuantitationRollupMethod;

            var form = new DefaultSettingsManagerForm { StartPosition = FormStartPosition.CenterParent };
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                // if rollup method changed, refresh protein and peptide views
                if (Properties.GUI.Settings.Default.QuantitationRollupMethod != oldRollupMethod)
                {
                    proteinTableForm.ClearData(true);
                    peptideTableForm.ClearData(true);
                    proteinTableForm.SetData(session, viewFilter);
                    peptideTableForm.SetData(session.SessionFactory.OpenSession(), viewFilter);
                }
            }
        }

        private void ShowQonverterSettings(object sender, EventArgs e)
        {
            if (session == null)
                return;

            IDictionary<Analysis, QonverterSettings> newSettings;

            lock (session)
            {
                var oldSettings = session.Query<QonverterSettings>().ToDictionary(o => session.Get<Analysis>(o.Id));

                bool cancel;
                newSettings = qonverterSettingsHandler(oldSettings, out cancel);
                if (cancel)
                    return;

                qonverterSettingsByAnalysis = session.Query<QonverterSettings>().ToDictionary(o => session.Get<Analysis>(o.Id));
            }

            ApplyQonverterSettings(newSettings);
        }

        public void ApplyQonverterSettings(IDictionary<Analysis, QonverterSettings> qonverterSettings)
        {
            clearData();
            setControlsWhenDatabaseLocked(true);

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += (s, e) =>
            {
                var qonverter = new Qonverter();
                qonverter.QonversionProgress += progressMonitor.UpdateProgress;
                foreach (var item in qonverterSettings)
                    qonverter.SettingsByAnalysis[(int) item.Key.Id] = item.Value.ToQonverterSettings();

                //qonverter.LogQonversionDetails = true;
                qonverter.Reset(Text);
                qonverter.Qonvert(Text);
            };

            workerThread.RunWorkerCompleted += (s, e) =>
            {
                if (e.Result is Exception)
                {
                    Program.HandleException(e.Result as Exception);
                    setControlsWhenDatabaseLocked(false);
                    return;
                }

                lock (session)
                {
                    var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(Text, new SessionFactoryConfig { WriteSqlToConsoleOut = true });
                    session = sessionFactory.OpenSession();
                    //session.CreateSQLQuery("PRAGMA temp_store=MEMORY").ExecuteUpdate();
                    _layoutManager.SetSession(session);

                    // delete old filters since they are not valid with different qonverter settings
                    session.CreateSQLQuery("DELETE FROM FilterHistory").ExecuteUpdate();
                    session.Clear();

                    if (basicFilter == null)
                        basicFilter = new DataFilter()
                        {
                            MaximumQValue = 0.02,
                            MinimumDistinctPeptides = 2,
                            MinimumSpectra = 2,
                            MinimumAdditionalPeptides = 1,
                            GeneLevelFiltering = false,
                            DistinctMatchFormat = new DistinctMatchFormat
                            {
                                IsChargeDistinct = true,
                                IsAnalysisDistinct = false,
                                AreModificationsDistinct = true,
                                ModificationMassRoundToNearest = 1.0m
                            }
                        };

                    basicFilterControl.DataFilter = basicFilter;

                    viewFilter = basicFilter;

                    session.Close();
                    sessionFactory.Close();
                }

                progressMonitor = new ProgressMonitor();
                progressMonitor.ProgressUpdate += progressMonitor_ProgressUpdate;
                basicFilterControl = new BasicFilterControl();
                basicFilterControl.BasicFilterChanged += basicFilterControl_BasicFilterChanged;
                basicFilterControl.ShowQonverterSettings += ShowQonverterSettings;
                dataFilterPopup = new Popup(basicFilterControl) { FocusOnOpen = true };
                dataFilterPopup.Closed += dataFilterPopup_Closed;
                new Thread(() => OpenFiles(new List<string> { Text }, null)).Start();
            };

            workerThread.RunWorkerAsync();
        }

        private void CropAssembly(object sender, EventArgs e)
        {
            CropAssembly();
        }

        private void toExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (session == null)
            {
                MessageBox.Show("No Report Loaded");
                return;
            }
            var selected = sender == toExcelSelectToolStripMenuItem;

            var exporter = new ExportForm();
            exporter.Show();

            var tables = new Dictionary<string, TableExporter.ITable>
            {
                { "Protein View", proteinTableForm },
                { "Peptide View", peptideTableForm },
                { "Spectrum View", spectrumTableForm },
                { "Modification View", modificationTableForm },
                { "Analysis View", analysisTableForm }
            };
            exporter.toExcel(selected, tables, viewFilter, basicFilter);
        }

        private void toHTMLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (session == null)
            {
                MessageBox.Show("No Report Loaded");
                return;
            }
            var selected = sender == toExcelSelectToolStripMenuItem;

            var exporter = new ExportForm();
            exporter.Show();

            var tables = new Dictionary<string, TableExporter.ITable>
            {
                { "Protein View", proteinTableForm },
                { "Peptide View", peptideTableForm },
                { "Analysis View", analysisTableForm }
            };

            var treeTables = new Dictionary<string, List<TableExporter.TableTreeNode>>
            {
                { "Modification View", modificationTableForm.getModificationTree() }
            };

            exporter.toHTML(selected, tables, treeTables, viewFilter, basicFilter, session);
        }


        private void spectralLibraryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (session == null)
            {
                MessageBox.Show("No Report Loaded");
                return;
            }
            var selected = sender == toExcelSelectToolStripMenuItem;

            var exporter = new ExportForm();
            exporter.Show();

            exporter.toLibrary(this,session, basicFilter.MinimumSpectraPerDistinctMatch, basicFilter.MinimumSpectraPerDistinctPeptide);
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
                                          "Copyright {2} Matt Chambers\r\n" +
                                          "Copyright 2008-2016 Vanderbilt University\r\n" +
                                          "Developers: Matt Chambers, Jay Holman, Surendra Dasari, Zeqiang Ma\r\n" +
                                          "Thanks to: David Tabb",
                                          Util.Version, Environment.Is64BitProcess ? "64-bit" : "32-bit", DateTime.Now.Year),
                            "About IDPicker");
        }

        private void visitWebsiteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://j.mp/idpicker-website");
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

            //Wait for process main handle be acquired
            while (process.MainWindowHandle == IntPtr.Zero)
                Thread.Sleep(1);

            WinAPI.SetWindowPos(process.MainWindowHandle, new IntPtr(0), this.Location.X + this.Width / 3, this.Location.Y + this.Height / 3, 0, 0,
                                WinAPI.SetWindowPosFlags.IgnoreResize | WinAPI.SetWindowPosFlags.IgnoreZOrder | WinAPI.SetWindowPosFlags.ShowWindow);

            process.Exited += (x, y) =>
            {
                if (process.ExitCode != 0)
                    Program.HandleException(new Exception(String.Format("Output:\r\n{0}\r\n\r\nError:\r\n{1}",
                                                          process.StandardOutput.ReadToEnd(),
                                                          process.StandardError.ReadToEnd())));
            };
        }

        #region Tutorial menu handlers
        private void glossaryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://j.mp/idpicker-tutorial-0-glossary");
        }

        private void dataImportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://j.mp/idpicker-tutorial-1-data-import");
        }

        private void proteinViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://j.mp/idpicker-tutorial-2-protein-view");
        }

        private void geneFeaturesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://j.mp/idpicker-tutorial-3-gene-features");
        }

        private void netGestaltToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://j.mp/idpicker-tutorial-4-netgestalt");
        }
        #endregion

        private void loadRefSeqGeneMetadata()
        {
            #region Check for updated gene2protein database and download it

            if (!File.Exists(Path.Combine(Application.StartupPath, "gene2protein.db3")))
                throw new Exception("missing gene2protein.db3 mapping file");

            /*string g2pTimestamp = null;

            string g2pPath = Path.Combine(Application.UserAppDataPath, "gene2protein.db3");
            string g2pPathAlternate = Path.Combine(Application.StartupPath, "gene2protein.db3");
            var copyUserToAlternate = new ProcessStartInfo("cmd.exe", String.Format("/C copy /Y \"{0}\" \"{1}\"", g2pPath, g2pPathAlternate));
            copyUserToAlternate.CreateNoWindow = true;

            if (!File.Exists(g2pPath) && File.Exists(g2pPathAlternate))
                File.Copy(g2pPathAlternate, g2pPath);
            else if (File.Exists(g2pPath) && !File.Exists(g2pPathAlternate))
            {
                var p = Process.Start(copyUserToAlternate);
                p.WaitForExit();
                if (p.ExitCode == 1)
                    MessageBox.Show("Unable to copy gene2protein.db3 from user-specific path to application path:\r\n" +
                                    Path.GetDirectoryName(g2pPath) +
                                    "\r\nto\r\n" +
                                    Path.GetDirectoryName(g2pPathAlternate),
                                    "Unable to copy");
            }

            if (File.Exists(g2pPath))
            {
                // if the file exists, check its timestamp and compare it to the timestamp on the server
                using (var con = new SQLiteConnection(@"Data Source=" + g2pPath + ";Version=3"))
                {
                    try
                    {
                        con.Open();
                        g2pTimestamp = con.ExecuteQuery("SELECT Timestamp FROM About").Single().GetString(0);
                    }
                    catch (Exception e)
                    {
                        throw new Exception("error getting timestamp of current gene2protein database (\"" + g2pPath + "\"): " + e.Message, e);
                    }
                }

                string g2pURL = "http://fenchurch.mc.vanderbilt.edu/bin/g2p";
                string g2pTimestampURL = String.Format("{0}/G2P_TIMESTAMP", g2pURL);

                string latestTimestamp;
                lock (Program.WebClient)
                {
                    latestTimestamp = Program.WebClient.DownloadString(g2pTimestampURL).Trim();
                }

                if (g2pTimestamp.CompareTo(latestTimestamp) < 0)
                {
                    Invoke(new MethodInvoker(() =>
                    {
                        var form = new NewVersionForm("RefSeq Gene to Protein Database",
                                                      g2pTimestamp,
                                                      latestTimestamp,
                                                      String.Empty)
                                                      {
                                                          Owner = this,
                                                          StartPosition = FormStartPosition.CenterParent
                                                      };

                        if (form.ShowDialog() == DialogResult.Yes)
                        {
                            var oldG2Ptime = File.GetLastWriteTimeUtc(g2pPath);
                            string backupG2Pname = String.Format("{0}.{1}.bak", g2pPath, oldG2Ptime.ToString("yyyyMMddHHmm"));
                            try
                            {
                                if (!File.Exists(backupG2Pname))
                                    File.Copy(g2pPath, backupG2Pname);
                                File.Delete(g2pPath);

                                string g2pDatabaseURL = String.Format("{0}/gene2protein.db3", g2pURL);
                                lock (Program.WebClient)
                                {
                                    Program.WebClient.DownloadFile(g2pDatabaseURL, g2pPath);

                                    var p = Process.Start(copyUserToAlternate);
                                    p.WaitForExit();
                                    if (p.ExitCode == 1)
                                        MessageBox.Show("Unable to copy gene2protein.db3 from user-specific path to application path:\r\n" +
                                                        Path.GetDirectoryName(g2pPath) +
                                                        "\r\nto\r\n" +
                                                        Path.GetDirectoryName(g2pPathAlternate),
                                                        "Unable to copy");
                                }
                                g2pTimestamp = latestTimestamp;
                            }
                            catch (Exception)
                            {
                                if (!File.Exists(g2pPath))
                                    File.Move(backupG2Pname, g2pPath);
                            }
                        }
                        else
                            g2pTimestamp = null; // no update
                    }));
                }
                else
                    g2pTimestamp = null; // no update
            }
            else // downloading database for the first time (or it has been deleted)
            {
                string g2pURL = "http://fenchurch.mc.vanderbilt.edu/bin/g2p";
                string g2pDatabaseURL = String.Format("{0}/gene2protein.db3", g2pURL);
                lock (Program.WebClient)
                {
                    Program.WebClient.DownloadFile(g2pDatabaseURL, g2pPath);

                    var p = Process.Start(copyUserToAlternate);
                    p.WaitForExit();
                    if (p.ExitCode == 1)
                        MessageBox.Show("Unable to copy gene2protein.db3 from user-specific path to application path:\r\n" +
                                        Path.GetDirectoryName(g2pPath) +
                                        "\r\nto\r\n" +
                                        Path.GetDirectoryName(g2pPathAlternate),
                                        "Unable to copy");
                }
            }*/
            #endregion

            if (session == null)
                return;

            try
            {
                clearSession();
                var ilr = new IterationListenerRegistry();
                ilr.addListener(progressMonitor.GetIterationListenerProxy(), 1);
                Embedder.EmbedGeneMetadata(Text, ilr);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "embedding gene metadata failed");
            }

            OpenFiles(new List<string> { Text }, null);
        }

        private void loadGeneMetadataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            loadRefSeqGeneMetadata();
        }

        private void dropGeneMetadataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (session == null)
                return;

            try
            {
                clearSession();
                Embedder.DropGeneMetadata(Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "dropping gene metadata failed");
            }

            OpenFiles(new List<string> { Text }, null);
        }

        private void showLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(() => showLogToolStripMenuItem_Click(sender, e)));
                return;
            }

            logForm.Show(dockPanel, DockState.DockBottomAutoHide);
        }

        private void IDPickerForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void IDPickerForm_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                var fileList = ((string[])e.Data.GetData(DataFormats.FileDrop)).ToList();

                if (fileList != null)
                {
                    for(var x = fileList.Count-1; x >= 0; x--)
                        if (!File.Exists(fileList[x]) || Path.GetExtension(fileList[x]).ToLower() != ".idpdb")
                            fileList.RemoveAt(x);
                    var bgw = new BackgroundWorker();
                    bgw.DoWork += (x, y) => OpenFiles((List<string>)y.Argument);
                    bgw.RunWorkerAsync(fileList);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error in DragDrop function: " + ex.Message);
            }
        }

        private void newSessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(Application.ExecutablePath);
        }

        private void garbageCollectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GC.Collect();
        }

        private void reapplyFiltersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            session.CreateSQLQuery("DELETE FROM FilterHistory").ExecuteUpdate();
            ApplyBasicFilter();
        }

        private void geneMetadataToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            bool hasGeneMetadata = Embedder.HasGeneMetadata(Text);
            loadGeneMetadataToolStripMenuItem.Text = hasGeneMetadata ? "Reload" : "Load";
            dropGeneMetadataToolStripMenuItem.Enabled = hasGeneMetadata;
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