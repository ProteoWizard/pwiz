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
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;

using IDPicker.Forms;
using IDPicker.Controls;
using IDPicker.DataModel;

using DigitalRune.Windows.Docking;
using seems;
using NHibernate;
using NHibernate.Linq;
using NHibernate.Criterion;
using BrightIdeasSoftware;

namespace IDPicker
{
    public partial class IDPickerForm : Form
    {
        BreadCrumbControl breadCrumbControl;

        ProteinTableForm proteinTableForm;
        PeptideTableForm peptideTableForm;
        SpectrumTableForm spectrumTableForm;
        ModificationTableForm modificationTableForm;

        LogForm logForm;

        NHibernate.ISession session;

        Manager manager;

        private DataFilter dataFilter = new DataFilter();

        public IDPickerForm ()
        {
            InitializeComponent();

            manager = new Manager(dockPanel);

            logForm = new LogForm();
            Console.SetOut(logForm.LogWriter);
            logForm.Show(dockPanel, DockState.DockBottomAutoHide);

            Shown += new EventHandler(Form1_Load);

            breadCrumbControl = new BreadCrumbControl()
            {
                Top = 0,
                Left = 0,
                Width = this.ClientRectangle.Width,
                Height = dockPanel.Top + ClientRectangle.Top,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(breadCrumbControl);

            breadCrumbControl.BreadCrumbClicked += new EventHandler(breadCrumbControl_BreadCrumbClicked);

            proteinTableForm = new ProteinTableForm();
            proteinTableForm.Show(dockPanel, DockState.DockTop);

            peptideTableForm = new PeptideTableForm();
            peptideTableForm.Show(proteinTableForm.Pane, DockPaneAlignment.Right, 0.7);

            spectrumTableForm = new SpectrumTableForm();
            spectrumTableForm.Show(dockPanel, DockState.DockLeft);

            modificationTableForm = new ModificationTableForm();
            modificationTableForm.Show(dockPanel, DockState.Document);

            proteinTableForm.ProteinViewFilter += new ProteinViewFilterEventHandler(proteinTableForm_ProteinViewFilter);
            peptideTableForm.PeptideViewFilter += new PeptideViewFilterEventHandler(peptideTableForm_PeptideViewFilter); 
            spectrumTableForm.SpectrumViewFilter += new EventHandler<DataFilter>(spectrumTableForm_SpectrumViewFilter);
            spectrumTableForm.SpectrumViewVisualize += new EventHandler<SpectrumViewVisualizeEventArgs>(spectrumTableForm_SpectrumViewVisualize);
            modificationTableForm.ModificationViewFilter += new ModificationViewFilterEventHandler(modificationTableForm_ModificationViewFilter);
        }

        void openToolStripButton_Click (object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog()
            {
                Filter = "IDPicker Files|*.idpDB;*.idpXML;*.pepXML;*.pep.xml|" + 
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
                OpenFiles(openFileDialog.FileNames);
            }
        }

        void spectrumTableForm_SpectrumViewVisualize (object sender, SpectrumViewVisualizeEventArgs e)
        {
            var psm = e.PeptideSpectrumMatch;

            string psmString = DataModel.ExtensionMethods.ToModifiedString(psm);
            var annotation = new PeptideFragmentationAnnotation(psmString, 1, Math.Max(0, psm.Charge - 1),
                                                                false, true, false, false, true, false, false,
                                                                true, false, true);

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

            manager.OpenFile(sourcePath, spectrum.NativeID, annotation);

            var source = manager.DataSourceMap[sourcePath];
            source.ChromatogramListForm.Hide();
            source.SpectrumListForm.Hide();
        }

        #region Handling of events for basic filter toolstrip items
        public void qvalueTextBox_Leave (object sender, EventArgs e)
        {
            decimal value;
            if (decimal.TryParse((sender as ToolStripTextBox).Text, out value))
                if (value != dataFilter.MaximumQValue)
                {
                    dataFilter.MaximumQValue = value;
                    applyBasicFilter();
                }
        }

        public void peptidesTextBox_Leave (object sender, EventArgs e)
        {
            int value;
            if (int.TryParse((sender as ToolStripTextBox).Text, out value))
                if (value != dataFilter.MinimumDistinctPeptidesPerProtein)
                {
                    dataFilter.MinimumDistinctPeptidesPerProtein = value;
                    applyBasicFilter();
                }
        }

        public void spectraTextBox_Leave (object sender, EventArgs e)
        {
            int value;
            if (int.TryParse((sender as ToolStripTextBox).Text, out value))
                if (value != dataFilter.MinimumSpectraPerProtein)
                {
                    dataFilter.MinimumSpectraPerProtein = value;
                    applyBasicFilter();
                }
        }

        public void additionalPeptidesTextBox_Leave (object sender, EventArgs e)
        {
            int value;
            if (int.TryParse((sender as ToolStripTextBox).Text, out value))
                if (value != dataFilter.MinimumAdditionalPeptidesPerProtein)
                {
                    dataFilter.MinimumAdditionalPeptidesPerProtein = value;
                    applyBasicFilter();
                }
        }
        #endregion

        #region Handling of filter events from each view
        void proteinTableForm_ProteinViewFilter (ProteinTableForm sender, DataFilter proteinViewFilter)
        {
            var oldFilter = dataFilter;
            dataFilter = proteinViewFilter;

            if (breadCrumbControl.BreadCrumbs.Count > 1)
                breadCrumbControl.BreadCrumbs.RemoveAt(breadCrumbControl.BreadCrumbs.Count - 1);
            breadCrumbControl.BreadCrumbs.Add(dataFilter);

            if (oldFilter.FilterSource == peptideTableForm ||
                oldFilter.FilterSource == spectrumTableForm ||
                oldFilter.FilterSource == modificationTableForm)
                proteinTableForm.SetData(session, dataFilter);

            peptideTableForm.SetData(session, dataFilter);
            spectrumTableForm.SetData(session, dataFilter);
            modificationTableForm.SetData(session, dataFilter);
        }

        void peptideTableForm_PeptideViewFilter (PeptideTableForm sender, DataFilter peptideViewFilter)
        {
            var oldFilter = dataFilter;
            dataFilter = peptideViewFilter;

            if (breadCrumbControl.BreadCrumbs.Count > 1)
                breadCrumbControl.BreadCrumbs.RemoveAt(breadCrumbControl.BreadCrumbs.Count - 1);
            breadCrumbControl.BreadCrumbs.Add(dataFilter);

            proteinTableForm.SetData(session, dataFilter);

            if (oldFilter.FilterSource == proteinTableForm ||
                oldFilter.FilterSource == spectrumTableForm ||
                oldFilter.FilterSource == modificationTableForm)
                peptideTableForm.SetData(session, dataFilter);

            spectrumTableForm.SetData(session, dataFilter);
            modificationTableForm.SetData(session, dataFilter);
        }

        void spectrumTableForm_SpectrumViewFilter (object sender, DataFilter spectrumViewFilter)
        {
            var oldFilter = dataFilter;
            dataFilter = spectrumViewFilter;

            if (breadCrumbControl.BreadCrumbs.Count > 1)
                breadCrumbControl.BreadCrumbs.RemoveAt(breadCrumbControl.BreadCrumbs.Count - 1);
            breadCrumbControl.BreadCrumbs.Add(dataFilter);

            proteinTableForm.SetData(session, dataFilter);
            peptideTableForm.SetData(session, dataFilter);

            if (oldFilter.FilterSource == proteinTableForm ||
                oldFilter.FilterSource == peptideTableForm ||
                oldFilter.FilterSource == modificationTableForm)
                spectrumTableForm.SetData(session, dataFilter);

            modificationTableForm.SetData(session, dataFilter);
        }

        void modificationTableForm_ModificationViewFilter (ModificationTableForm sender, DataFilter modificationViewFilter)
        {
            var oldFilter = dataFilter;
            dataFilter = modificationViewFilter;

            if (breadCrumbControl.BreadCrumbs.Count > 1)
                breadCrumbControl.BreadCrumbs.RemoveAt(breadCrumbControl.BreadCrumbs.Count - 1);
            breadCrumbControl.BreadCrumbs.Add(dataFilter);

            proteinTableForm.SetData(session, dataFilter);
            peptideTableForm.SetData(session, dataFilter);
            spectrumTableForm.SetData(session, dataFilter);

            if (oldFilter.FilterSource == proteinTableForm ||
                oldFilter.FilterSource == peptideTableForm ||
                oldFilter.FilterSource == spectrumTableForm)
                modificationTableForm.SetData(session, dataFilter);
        }
        #endregion

        void breadCrumbControl_BreadCrumbClicked (object sender, EventArgs e)
        {
            var oldFilter = dataFilter;
            if (sender is DataFilter)
                dataFilter = sender as DataFilter;
            else
                dataFilter = (sender as IList<ToolStripItem>)[0].Tag as DataFilter;

            if (breadCrumbControl.BreadCrumbs.Count > 1)
                breadCrumbControl.BreadCrumbs.RemoveAt(breadCrumbControl.BreadCrumbs.Count - 1);

            if (oldFilter.Protein == null)
                proteinTableForm.SetData(session, dataFilter);

            if (oldFilter.Peptide == null && oldFilter.DistinctPeptideKey == null)
                peptideTableForm.SetData(session, dataFilter);

            if (oldFilter.Spectrum == null && oldFilter.SpectrumSource == null)
                spectrumTableForm.SetData(session, dataFilter);

            if (oldFilter.Modifications.Count == 0 && oldFilter.ModifiedSite == null)
                modificationTableForm.SetData(session, dataFilter);
        }

        SimpleProgressForm applyBasicFilterProgressForm;
        void applyBasicFilter ()
        {
            clearData();

            applyBasicFilterProgressForm = new SimpleProgressForm(this);
            applyBasicFilterProgressForm.Text = "Applying basic filters...";
            applyBasicFilterProgressForm.Show();
            dataFilter.FilteringProgress += new EventHandler<DataFilter.FilteringProgressEventArgs>(applyBasicFilterProgressForm.UpdateProgress);

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += new DoWorkEventHandler(applyBasicFilterAsync);
            workerThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(setData);
            workerThread.RunWorkerAsync();
        }

        void applyBasicFilterAsync (object sender, DoWorkEventArgs e)
        {
            try
            {
                lock (session)
                    dataFilter.ApplyBasicFilters(session);
            }
            catch (Exception ex)
            {
                HandleException(Thread.CurrentThread, ex);
            }
        }

        void clearData ()
        {
            proteinTableForm.ClearData();
            peptideTableForm.ClearData();
            spectrumTableForm.ClearData();
            modificationTableForm.ClearData();
        }

        void setData (object sender, RunWorkerCompletedEventArgs e)
        {
            dataFilter.FilteringProgress -= new EventHandler<DataFilter.FilteringProgressEventArgs>(applyBasicFilterProgressForm.UpdateProgress);
            applyBasicFilterProgressForm.Dispose();

            proteinTableForm.SetData(session, dataFilter);
            peptideTableForm.SetData(session, dataFilter);
            spectrumTableForm.SetData(session, dataFilter);
            modificationTableForm.SetData(session, dataFilter);
        }

        void Form1_Load (object sender, EventArgs e)
        {
            //var filepaths = Directory.GetFiles(@"c:\test\Goldenring_gastric", "*.pepXML", SearchOption.AllDirectories);
            //OpenFiles(filepaths.Shuffle().ToList());//.Take(10).Union(filepaths.Skip(200).Take(10)).Union(filepaths.Skip(400).Take(10)).ToList());
            openToolStripButton_Click(this, EventArgs.Empty);
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

        public class SimpleProgressForm : Form
        {
            private ProgressBar progressBar;
            public ProgressBar ProgressBar { get { return progressBar; } }

            private System.Diagnostics.Stopwatch timer;

            public SimpleProgressForm (Form parent)
            {
                Owner = parent;
                SizeGripStyle = SizeGripStyle.Show;
                ShowInTaskbar = true;
                TopLevel = true;
                TopMost = true;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowOnly;
                FormBorderStyle = FormBorderStyle.SizableToolWindow;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = true;
                Size = new System.Drawing.Size(450, 50);

                progressBar = new ProgressBar();
                progressBar.Dock = DockStyle.Fill;
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Minimum = 0;
                progressBar.Maximum = 1000;
                progressBar.Value = 0;
                Controls.Add(progressBar);

                timer = System.Diagnostics.Stopwatch.StartNew();
            }

            public void UpdateProgress (object sender, Parser.ParsingProgressEventArgs e)
            {
                if(InvokeRequired)
                {
                    BeginInvoke((MethodInvoker) (() => UpdateProgress(sender, e)));
                    return;
                }

                if (e.ParsingException != null)
                    throw new InvalidOperationException("parsing error", e.ParsingException);

                if (e.ParsedBytes == 0)
                    timer = System.Diagnostics.Stopwatch.StartNew();

                progressBar.Maximum = 1000;
                progressBar.Value = (int) Math.Round((double) e.ParsedBytes / e.TotalBytes * 1000.0);
                double progressRate = timer.Elapsed.TotalSeconds > 0 ? e.ParsedBytes / timer.Elapsed.TotalSeconds : 0;
                long bytesRemaining = e.TotalBytes - e.ParsedBytes;
                TimeSpan timeRemaining = progressRate == 0 ? TimeSpan.Zero
                                                           : TimeSpan.FromSeconds(bytesRemaining / progressRate);
                Text = String.Format("{0} ({1}/{2}) - {3} per second, {4}h{5}m{6}s remaining",
                                     e.ParsingStage,
                                     Util.GetFileSizeByteString(e.ParsedBytes),
                                     Util.GetFileSizeByteString(e.TotalBytes),
                                     Util.GetFileSizeByteString((long) progressRate),
                                     timeRemaining.Hours,
                                     timeRemaining.Minutes,
                                     timeRemaining.Seconds);

                Application.DoEvents();

                e.Cancel = !Visible || IsDisposed;
            }

            public void UpdateProgress (object sender, Merger.MergingProgressEventArgs e)
            {
                if (InvokeRequired)
                {
                    Invoke((MethodInvoker) (() => UpdateProgress(sender, e)));
                    return;
                }

                progressBar.Maximum = 1000;
                progressBar.Value = (int) Math.Round((double) e.MergedFiles / e.TotalFiles * 1000.0);
                double progressRate = timer.Elapsed.TotalSeconds > 0 ? e.MergedFiles / timer.Elapsed.TotalSeconds : 0;
                long bytesRemaining = e.TotalFiles - e.MergedFiles;
                TimeSpan timeRemaining = progressRate == 0 ? TimeSpan.Zero
                                                           : TimeSpan.FromSeconds(bytesRemaining / progressRate);
                Text = String.Format("Merging results... ({0}/{1}) - {2} per second, {3}h{4}m{5}s remaining",
                                     e.MergedFiles,
                                     e.TotalFiles,
                                     Math.Round(progressRate),
                                     timeRemaining.Hours,
                                     timeRemaining.Minutes,
                                     timeRemaining.Seconds);

                Application.DoEvents();

                e.Cancel = !Visible || IsDisposed;
            }

            public void UpdateProgress (object sender, StaticWeightQonverter.QonversionProgressEventArgs e)
            {
                if (InvokeRequired)
                {
                    Invoke((MethodInvoker) (() => UpdateProgress(sender, e)));
                    return;
                }

                progressBar.Maximum = e.TotalAnalyses;
                progressBar.Value = e.QonvertedAnalyses;

                Application.DoEvents();

                e.Cancel = !Visible || IsDisposed;
            }

            public void UpdateProgress (object sender, DataFilter.FilteringProgressEventArgs e)
            {
                if (InvokeRequired)
                {
                    Invoke((MethodInvoker) (() => UpdateProgress(sender, e)));
                    return;
                }

                progressBar.Maximum = e.TotalFilters;
                progressBar.Value = e.CompletedFilters;

                Text = String.Format("{0} ({1}/{2})",
                                     e.FilteringStage,
                                     e.CompletedFilters,
                                     e.TotalFilters);

                e.Cancel = !Visible || IsDisposed;
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
                    using (SimpleProgressForm pf = new SimpleProgressForm(this))
                    using (Parser parser = new Parser(commonFilename, ".", xml_filepaths.ToArray()))
                    {
                        pf.Text = "Initializing parser...";
                        pf.Show();
                        parser.DatabaseNotFound += new EventHandler<Parser.DatabaseNotFoundEventArgs>(databaseNotFoundHandler);
                        parser.SourceNotFound += new EventHandler<Parser.SourceNotFoundEventArgs>(sourceNotFoundOnImportHandler);
                        parser.ParsingProgress += new EventHandler<Parser.ParsingProgressEventArgs>(pf.UpdateProgress);
                        parser.Start();

                        if (!pf.Visible || pf.IsDisposed)
                            return;
                    }
                    idpDB_filepaths = idpDB_filepaths.Union(xml_filepaths.Select(o => Path.ChangeExtension(o, ".idpDB")));
                }

                if (idpDB_filepaths.Count() > 1)
                {
                    using (SimpleProgressForm pf = new SimpleProgressForm(this))
                    {
                        var merger = new Merger(commonFilename, idpDB_filepaths);
                        pf.Text = "Merging results...";
                        pf.Show();
                        merger.MergingProgress += new EventHandler<Merger.MergingProgressEventArgs>(pf.UpdateProgress);
                        merger.Start();

                        if (!pf.Visible || pf.IsDisposed)
                            return;

                        idpDB_filepaths = new List<string>() {commonFilename};
                    }
                }

                using (SimpleProgressForm pf = new SimpleProgressForm(this))
                {
                    pf.Text = "Initializing Qonverter...";
                    pf.Show();
                    var qonverter = new IDPicker.StaticWeightQonverter();
                    qonverter.ScoreWeights["mvh"] = 1;
                    //qonverter.ScoreWeights["mzFidelity"] = 1;
                    qonverter.QonversionProgress += new StaticWeightQonverter.QonversionProgressEventHandler(pf.UpdateProgress);

                    try
                    {
                        qonverter.Qonvert(commonFilename);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Error: " + e.Message, "Qonversion failed");
                    }

                    if (!pf.Visible || pf.IsDisposed)
                        return;
                }

                var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(commonFilename, false, true);
                session = sessionFactory.OpenSession();
                session.CreateSQLQuery("PRAGMA temp_store=MEMORY").ExecuteUpdate();

                bool hasFilterView = false;
                try
                {
                    session.CreateSQLQuery("CREATE TABLE FilteringCriteria ( MaximumQValue NUMERIC, MinimumDistinctPeptidesPerProtein INT, MinimumSpectraPerProtein INT, MinimumAdditionalPeptidesPerProtein INT )").ExecuteUpdate();
                }
                catch
                {
                    try
                    {
                        dataFilter.MaximumQValue = session.CreateSQLQuery("SELECT MaximumQValue FROM FilteringCriteria").List<decimal>()[0];
                        dataFilter.MinimumDistinctPeptidesPerProtein = session.CreateSQLQuery("SELECT MinimumDistinctPeptidesPerProtein FROM FilteringCriteria").List<int>()[0];
                        dataFilter.MinimumSpectraPerProtein = session.CreateSQLQuery("SELECT MinimumSpectraPerProtein FROM FilteringCriteria").List<int>()[0];
                        dataFilter.MinimumAdditionalPeptidesPerProtein = session.CreateSQLQuery("SELECT MinimumAdditionalPeptidesPerProtein FROM FilteringCriteria").List<int>()[0];
                        hasFilterView = true;
                    }
                    catch
                    { }
                }

                //make sure to ignore "Abandoned" group
                dataFilter.SpectrumSourceGroup = session.QueryOver<DataModel.SpectrumSourceGroup>().Where(g => g.Name == "/").SingleOrDefault();

                breadCrumbControl.BreadCrumbs.Clear();
                breadCrumbControl.BreadCrumbs.Add(dataFilter.GetBasicFilterControls(this));

                if (!hasFilterView)
                    applyBasicFilter();
                else
                {
                    proteinTableForm.SetData(session, dataFilter);
                    peptideTableForm.SetData(session, dataFilter);
                    spectrumTableForm.SetData(session, dataFilter);
                    modificationTableForm.SetData(session, dataFilter);
                }
            }
            catch (Exception ex)
            {
                HandleException(this, ex);
            }
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