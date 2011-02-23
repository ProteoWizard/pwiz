//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
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
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using DigitalRune.Windows.Docking;
using IDPicker.Forms;
using IDPicker.Controls;
using IDPicker.DataModel;
using pwiz.CLI.cv;
using seems;
using NHibernate;
using NHibernate.Linq;
using NHibernate.Criterion;
using BrightIdeasSoftware;
using PopupControl;
using Microsoft.WindowsAPICodePack.Taskbar;
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

        //LogForm logForm;
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

            this.args = args;

            manager = new Manager(dockPanel)
            {
                ShowChromatogramListForNewSources = false,
                ShowSpectrumListForNewSources = false,
                OpenFileUsesCurrentGraphForm = true,
            };

            progressMonitor = new ProgressMonitor();
            progressMonitor.ProgressUpdate += progressMonitor_ProgressUpdate;

            Shown += new EventHandler(IDPickerForm_Load);

            basicFilterControl = new BasicFilterControl();
            basicFilterControl.BasicFilterChanged += basicFilterControl_BasicFilterChanged;
            basicFilterControl.ShowQonverterSettings += ShowQonverterSettings;
            dataFilterPopup = new Popup(basicFilterControl) { FocusOnOpen = true };
            dataFilterPopup.Closed += dataFilterPopup_Closed;

            breadCrumbControl = new BreadCrumbControl() { Dock = DockStyle.Fill };
            breadCrumbControl.BreadCrumbClicked += breadCrumbControl_BreadCrumbClicked;
            breadCrumbPanel.Controls.Add(breadCrumbControl);

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

            _layoutManager = new LayoutManager(this, peptideTableForm, proteinTableForm, spectrumTableForm, dockPanel);

            // load last or default location and size
            _layoutManager.LoadMainFormSettings();


            //logForm = new LogForm();
            //Console.SetOut(logForm.LogWriter);
            //logForm.Show(dockPanel, DockState.DockBottomAutoHide);

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

            toolStripStatusLabel.Text = e.Message;
            toolStripProgressBar.Visible = true;
            toolStripProgressBar.Maximum = e.Total;
            toolStripProgressBar.Value = e.Current;

            if (TaskbarManager.IsPlatformSupported)
            {
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
                TaskbarManager.Instance.SetProgressValue(e.Current, e.Total);
            }

            Application.DoEvents();

            // TODO: add a cancel option: e.Cancel

            // if the work is done, schedule a delayed return to the "Ready" state
            if (e.Total == e.Current)
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

        void openToolStripMenuItem_Click (object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog()
            {
                Filter = sender == openToolStripMenuItem 
                         ? "IDPicker DB|*.idpDB"
                         : "IDPicker Files|*.idpDB;*.idpXML;*.pepXML;*.pep.xml|" +
                           "PepXML Files|*.pepXML;*.pep.xml|" +
                           "IDPicker XML|*.idpXML|" +
                           "IDPicker DB|*.idpDB|" +
                           "Any File|*.*",
                SupportMultiDottedExtensions = true,
                AddExtension = true,
                CheckFileExists = true,
                Multiselect = true  
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                clearData();
                progressMonitor = new ProgressMonitor();
                progressMonitor.ProgressUpdate += progressMonitor_ProgressUpdate;

                basicFilterControl = new BasicFilterControl();
                basicFilterControl.BasicFilterChanged += basicFilterControl_BasicFilterChanged;
                basicFilterControl.ShowQonverterSettings += ShowQonverterSettings;
                dataFilterPopup = new Popup(basicFilterControl) { FocusOnOpen = true };
                dataFilterPopup.Closed += dataFilterPopup_Closed;

                breadCrumbControl.ClearBreadcrumbs();

                var fileNames = openFileDialog.FileNames.ToList();

                if (session != null)
                {
                    if (sender == importToolStripMenuItem)
                    {
                        var filenameMatch = Regex.Match(session.Connection.ConnectionString, @"Data Source=(?:\w|:|\\| |/|\.)+").ToString().Remove(0,12);
                        if (!fileNames.Contains(filenameMatch))
                            fileNames.Add(filenameMatch);
                    }
                    session.Close();
                    session = null;
                }

                OpenFiles(fileNames.ToArray());
            }
        }

        #region Handling of events for spectrum/protein visualization
        Dictionary<GraphForm, bool> handlerIsAttached = new Dictionary<GraphForm, bool>();
        void spectrumTableForm_SpectrumViewVisualize (object sender, SpectrumViewVisualizeEventArgs e)
        {
            var psm = e.PeptideSpectrumMatch;
            var spectrum = psm.Spectrum;

            string sourcePath;
            if (spectrum.Source.Metadata != null)
            {
                // accessing the Metadata property creates a temporary mzML file;
                // here we access the path to that file
                var tmpSourceFile = spectrum.Source.Metadata.fileDescription.sourceFiles.Last();
                sourcePath = Path.Combine(new Uri(tmpSourceFile.location).LocalPath, tmpSourceFile.name);
            }
            else
            {
                try
                {
                    sourcePath = Util.FindSourceInSearchPath(spectrum.Source.Name, ".");
                }
                catch
                {
                    try
                    {
                        // try the last looked-in path
                        sourcePath = Util.FindSourceInSearchPath(spectrum.Source.Name, Properties.Settings.Default.LastSpectrumSourceDirectory);
                    }
                    catch
                    {
                        // prompt user to find the source
                        var eventArgs = new Parser.SourceNotFoundEventArgs() { SourcePath = spectrum.Source.Name };
                        sourceNotFoundOnVisualizeHandler(this, eventArgs);

                        if (eventArgs.SourcePath == spectrum.Source.Name)
                            return; // user canceled

                        if (File.Exists(eventArgs.SourcePath) || Directory.Exists(eventArgs.SourcePath))
                        {
                            Properties.Settings.Default.LastSpectrumSourceDirectory = Path.GetDirectoryName(eventArgs.SourcePath);
                            Properties.Settings.Default.Save();
                            sourcePath = eventArgs.SourcePath;
                        }
                        else
                            throw; // file still not found, abort the visualization
                    }
                }
            }

            var param = psm.Analysis.Parameters.Where(o => o.Name == "SpectrumListFilters").SingleOrDefault();
            string spectrumListFilters = param == null ? String.Empty : param.Value;
            spectrumListFilters = spectrumListFilters.Replace("0 ", "false ");

            string psmString = DataModel.ExtensionMethods.ToModifiedString(psm);
            var annotation = new PeptideFragmentationAnnotation(psmString, 1, Math.Max(1, psm.Charge - 1),
                                                                PeptideFragmentationAnnotation.IonSeries.Auto,
                                                                true, false, true);

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
            var tlv = spectrumTableForm.TreeListView;

            if (tlv.SelectedItem == null)
                return;

            int rowIndex = tlv.SelectedIndex;
            bool previousRowIsPSM = rowIndex > 0 && tlv.GetModelObject(rowIndex - 1) is SpectrumTableForm.PeptideSpectrumMatchRow;
            bool nextRowIsPSM = rowIndex + 1 < tlv.GetItemCount() && tlv.GetModelObject(rowIndex + 1) is SpectrumTableForm.PeptideSpectrumMatchRow;

            int key = (int) e.KeyCode;
            if ((key == (int) Keys.Left || key == (int) Keys.Up) && previousRowIsPSM)
                --tlv.SelectedIndex;
            else if ((key == (int) Keys.Right || key == (int) Keys.Down) && nextRowIsPSM)
                ++tlv.SelectedIndex;
            else
                return;

            //tlv.EnsureVisible(tlv.SelectedIndex);

            spectrumTableForm_SpectrumViewVisualize(this, new SpectrumViewVisualizeEventArgs()
            {
                PeptideSpectrumMatch = (tlv.GetSelectedObject() as SpectrumTableForm.PeptideSpectrumMatchRow).PeptideSpectrumMatch
            });
        }

        void proteinTableForm_ProteinViewVisualize (object sender, ProteinViewVisualizeEventArgs e)
        {
            var form = new SequenceCoverageForm(e.Protein);
            form.Show(modificationTableForm.Pane, null);
            //spyEventLogForm.AddEventSpy(new EventSpy(e.Protein.Accession.Replace(":","_"), form));
            //foreach(Control control in form.Controls)
            //    spyEventLogForm.AddEventSpy(new EventSpy(e.Protein.Accession.Replace(":", "_") + control.GetType().Name, control));
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

            workerThread.RunWorkerCompleted += delegate
            {
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
                HandleException(Thread.CurrentThread, ex);
            }
        }

        void clearData ()
        {
            if (proteinTableForm != null)
                proteinTableForm.ClearData(true);
            if (peptideTableForm != null)
                peptideTableForm.ClearData(true);
            if (spectrumTableForm != null)
                spectrumTableForm.ClearData(true);
            if (modificationTableForm != null)
                modificationTableForm.ClearData(true);
            if (analysisTableForm != null)
                analysisTableForm.ClearData(true);
        }

        void setData ()
        {
            proteinTableForm.SetData(session, viewFilter);
            peptideTableForm.SetData(session, viewFilter);
            spectrumTableForm.SetData(session, viewFilter);
            modificationTableForm.SetData(session, viewFilter);
            analysisTableForm.SetData(session, viewFilter);
        }

        void IDPickerForm_Load (object sender, EventArgs e)
        {
            //System.Data.SQLite.SQLiteConnection.SetConfigOption(SQLiteConnection.SQLITE_CONFIG.MULTITHREAD);
            //var filepaths = Directory.GetFiles(@"c:\test\Goldenring_gastric\Metaplasia", "klc*FFPE*.pepXML", SearchOption.AllDirectories);
            //OpenFiles(filepaths);//.Take(10).Union(filepaths.Skip(200).Take(10)).Union(filepaths.Skip(400).Take(10)).ToList());
            //return;

            //Get user layout profiles
            LoadLayout(_layoutManager.GetCurrentDefault());

            dockPanel.Visible = false;

            if (args != null && args.Length > 0 && args.All(o => File.Exists(o)))
                OpenFiles(args);
            //else
            //    openToolStripMenuItem_Click(this, EventArgs.Empty);
        }

        void databaseNotFoundHandler (object sender, Parser.DatabaseNotFoundEventArgs e)
        {
            if (String.IsNullOrEmpty(e.DatabasePath))
                return;

            if (InvokeRequired)
            {
                Invoke((MethodInvoker) delegate() { databaseNotFoundHandler(sender, e); });
                return;
            }

            var findDirectoryDialog = new FolderBrowserDialog()
            {
                SelectedPath = Properties.Settings.Default.LastProteinDatabaseDirectory,
                ShowNewFolderButton = false,
                Description = "Locate the directory containing the database \"" + e.DatabasePath + "\""
            };

            while (findDirectoryDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    e.DatabasePath = Util.FindDatabaseInSearchPath(e.DatabasePath, findDirectoryDialog.SelectedPath);
                    Properties.Settings.Default.LastProteinDatabaseDirectory = findDirectoryDialog.SelectedPath;
                    Properties.Settings.Default.Save();
                    break;
                }
                catch
                {
                    // couldn't find the database in that directory; prompt user again
                }
            }
        }

        bool promptForSourceNotFound = true;
        bool promptToSkipSourceImport = false;
        void sourceNotFoundOnImportHandler (object sender, Parser.SourceNotFoundEventArgs e)
        {
            if (String.IsNullOrEmpty(e.SourcePath) || !promptForSourceNotFound)
                return;

            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => sourceNotFoundOnImportHandler(sender, e)));
                return;
            }

            if (promptToSkipSourceImport)
            {
                // for the second source not found, give user option to suppress this event
                if (MessageBox.Show("Source \"" + e.SourcePath + "\" not found.\r\n\r\n" +
                                    "Do you want to skip importing of missing sources for this session?",
                                    "Skip missing sources",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    promptForSourceNotFound = false;
                    return;
                }
            }

            promptToSkipSourceImport = true;

            var findDirectoryDialog = new FolderBrowserDialog()
            {
                SelectedPath = Properties.Settings.Default.LastSpectrumSourceDirectory,
                ShowNewFolderButton = false,
                Description = "Locate the directory containing the source \"" + e.SourcePath + "\""
            };

            while (findDirectoryDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    e.SourcePath = Util.FindSourceInSearchPath(e.SourcePath, findDirectoryDialog.SelectedPath);
                    Properties.Settings.Default.LastSpectrumSourceDirectory = findDirectoryDialog.SelectedPath;
                    Properties.Settings.Default.Save();
                    promptToSkipSourceImport = false;
                    return;
                }
                catch
                {
                    // couldn't find the source in that directory; prompt user again
                }
            }
        }

        void sourceNotFoundOnVisualizeHandler (object sender, Parser.SourceNotFoundEventArgs e)
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
                SelectedPath = Properties.Settings.Default.LastSpectrumSourceDirectory,
                ShowNewFolderButton = false,
                Description = "Locate the directory containing the source \"" + e.SourcePath + "\""
            };

            while (findDirectoryDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    e.SourcePath = Util.FindSourceInSearchPath(e.SourcePath, findDirectoryDialog.SelectedPath);
                    Properties.Settings.Default.LastSpectrumSourceDirectory = findDirectoryDialog.SelectedPath;
                    Properties.Settings.Default.Save();
                    return;
                }
                catch
                {
                    // couldn't find the source in that directory; prompt user again
                }
            }
        }

        void OpenFiles (IList<string> filepaths)
        {
            try
            {
                var xml_filepaths = filepaths.Where(filepath => filepath.EndsWith(".idpXML") ||
                                                                filepath.EndsWith(".pepXML") ||
                                                                filepath.EndsWith(".pep.xml"));
                var idpDB_filepaths = filepaths.Where(filepath => filepath.EndsWith(".idpDB"));

                if (xml_filepaths.Count() + idpDB_filepaths.Count() == 0)
                {
                    MessageBox.Show("Select one or more idpXML, pepXML, or idpDB files to create an IDPicker report.", "No IDPicker files selected");
                    return;
                }

                string commonFilename = Util.GetCommonFilename(filepaths);
                
                if (!idpDB_filepaths.Contains(commonFilename) &&
                    File.Exists(commonFilename) &&
                    SessionFactoryFactory.IsValidFile(commonFilename))
                {
                    if (MessageBox.Show("The merged result \"" + commonFilename + "\" already exists. Do you want to overwrite it?",
                                        "Merged result already exists",
                                        MessageBoxButtons.YesNo,
                                        MessageBoxIcon.Exclamation,
                                        MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                        return;
                    File.Delete(commonFilename);
                }

                // set main window title
                Text = commonFilename;

                
                if (xml_filepaths.Count() > 0)
                {
                    using (Parser parser = new Parser(".", qonverterSettingsHandler, false, xml_filepaths.ToArray()))
                    {
                        toolStripStatusLabel.Text = "Initializing parser...";
                        parser.DatabaseNotFound += databaseNotFoundHandler;
                        parser.SourceNotFound += sourceNotFoundOnImportHandler;
                        parser.ParsingProgress += progressMonitor.UpdateProgress;
                        if (parser.Start())
                        {
                            if (parser.emergencyBreak)
                            {
                                MessageBox.Show(
                                    "An error occurred during parsing of file. " +
                                    "Please make sure file is valid " +
                                    "and the decoy prefix is correct.");
                                if (File.Exists(commonFilename))
                                    File.Delete(commonFilename);
                                Text = "IDPicker";
                            }
                            return;
                        }
                    }
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
                    using (var fs = new FileStream(commonFilename, FileMode.Open, FileSystemRights.ReadData, FileShare.Read, (1 << 15), FileOptions.SequentialScan))
                    {
                        var buffer = new byte[UInt16.MaxValue];
                        while (fs.Read(buffer, 0, UInt16.MaxValue) > 0) { }
                    }
                }

                var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(commonFilename, false, true);

                toolStripStatusLabel.Text = "Initializing Qonverter...";

                var qonverter = new Qonverter();

                // reload qonverter settings because the ids may change after merging
                session = sessionFactory.OpenSession();
                qonverterSettingsByAnalysis = session.Query<QonverterSettings>().ToDictionary(o => session.Get<Analysis>(o.Id));
                session.Close();

                qonverterSettingsByAnalysis.ForEach(o => qonverter.SettingsByAnalysis[(int) o.Key.Id] = o.Value.ToQonverterSettings());
                qonverter.QonversionProgress += progressMonitor.UpdateProgress;
                //qonverter.LogQonversionDetails = true;

                try
                {
                    qonverter.Qonvert(commonFilename);
                }
                catch (Exception e)
                {
                    MessageBox.Show("Error: " + e.Message, "Qonversion failed");
                }

                session = sessionFactory.OpenSession();
                session.CreateSQLQuery("PRAGMA temp_store=MEMORY").ExecuteUpdate();

                _layoutManager.SetSession(session);

                //set or save default layout
                dockPanel.Visible = true;
                LoadLayout(_layoutManager.GetCurrentDefault());

                //breadCrumbControl.BreadCrumbs.Clear();

                basicFilter = DataFilter.LoadFilter(session);

                if (basicFilter == null)
                {
                    basicFilter = new DataFilter()
                    {
                        MaximumQValue = 0.02M,
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
            }
            catch (Exception ex)
            {
                HandleException(this, ex);
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
                var columnList = userLayout.SettingsList.Where(o => o.Scope == "ProteinTableForm");
                proteinTableForm.LoadLayout(columnList.ToList());

                columnList = userLayout.SettingsList.Where(o => o.Scope == "PeptideTableForm");
                peptideTableForm.LoadLayout(columnList.ToList());

                columnList = userLayout.SettingsList.Where(o => o.Scope == "SpectrumTableForm");
                spectrumTableForm.LoadLayout(columnList.ToList());
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

        public void HandleException (object sender, Exception ex)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker) (() => HandleException(sender, ex)));
                return;
            }

            string message = ex.ToString();
            if (ex.InnerException != null)
                message += "\n\nAdditional information: " + ex.InnerException.ToString();
            MessageBox.Show(message,
                            "Unhandled Exception",
                            MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
                            0, false);
            Application.Exit();
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

        private void IDPickerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_layoutManager != null)
            {
                _layoutManager.SaveMainFormSettings();
                _layoutManager.SaveUserLayoutList();
            }
        }


        private void layoutButton_Click(object sender, EventArgs e)
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

            DataFilter.DropFilters(session.Connection);
            var qonverter = new Qonverter();
            qonverter.QonversionProgress += progressMonitor.UpdateProgress;
            qonverterSettingsByAnalysis = session.Query<QonverterSettings>().ToDictionary(o => session.Get<Analysis>(o.Id));
            session.CreateQuery(@"UPDATE PeptideSpectrumMatch SET QValue = 2").ExecuteUpdate();
            foreach (var item in qonverterSettings)
            {
                qonverter.SettingsByAnalysis[(int)item.Key.Id] = item.Value.ToQonverterSettings();
                qonverterSettingsByAnalysis[item.Key].DecoyPrefix = item.Value.DecoyPrefix;
                qonverterSettingsByAnalysis[item.Key].ScoreInfoByName = item.Value.ScoreInfoByName;
                qonverterSettingsByAnalysis[item.Key].QonverterMethod = item.Value.QonverterMethod;
                qonverterSettingsByAnalysis[item.Key].RerankMatches = item.Value.RerankMatches;
                session.Save(qonverterSettingsByAnalysis[item.Key]);
            }
            session.Flush();
            session.Close();

            //qonverter.LogQonversionDetails = true;
            try
            {
                qonverter.Qonvert(Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Qonversion failed");
            }

            var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(Text, false, true);
            session = sessionFactory.OpenSession();
            //session.CreateSQLQuery("PRAGMA temp_store=MEMORY").ExecuteUpdate();
            _layoutManager.SetSession(session);

            basicFilter = new DataFilter()
                              {
                                  MaximumQValue = 0.02M,
                                  MinimumDistinctPeptidesPerProtein = 2,
                                  MinimumSpectraPerProtein = 2,
                                  MinimumAdditionalPeptidesPerProtein = 1
                              };

            basicFilterControl.DataFilter = basicFilter;

            viewFilter = basicFilter;

            viewFilter.ApplyBasicFilters(session);

            session.Close();

            clearData();
            progressMonitor = new ProgressMonitor();
            progressMonitor.ProgressUpdate += progressMonitor_ProgressUpdate;
            basicFilterControl = new BasicFilterControl();
            basicFilterControl.BasicFilterChanged += basicFilterControl_BasicFilterChanged;
            basicFilterControl.ShowQonverterSettings += ShowQonverterSettings;
            dataFilterPopup = new Popup(basicFilterControl) { FocusOnOpen = true };
            dataFilterPopup.Closed += dataFilterPopup_Closed;
            OpenFiles(new List<string> {Text});
        }
    }

    public static class StopwatchExtensions
    {
        public static TimeSpan Restart (this System.Diagnostics.Stopwatch stopwatch)
        {
            TimeSpan timeSpan = stopwatch.Elapsed;
            stopwatch.Reset();
            stopwatch.Start();
            return timeSpan;
        }
    }
}