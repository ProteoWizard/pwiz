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

        NHibernate.ISession session;

        Manager manager;

        private DataFilter dataFilter = new DataFilter();

        public IDPickerForm ()
        {
            InitializeComponent();

            manager = new Manager(dockPanel);

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
                Filter = "IDPickerForm Files|*.idpDB;*.idpXML;*.pepXML;*.pep.xml|" + 
                         "PepXML Files|*.pepXML;*.pep.xml|" +
                         "IDPickerForm XML|*.idpXML|" +
                         "IDPickerForm DB|*.idpDB|" +
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

        string lastSourcePathLocation;
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
                sourcePath = Path.GetTempFileName() + ".mzML.gz";
                pwiz.CLI.msdata.MSDataFile.write(spectrum.Source.Metadata, sourcePath,
                                                 new pwiz.CLI.msdata.MSDataFile.WriteConfig() { gzipped = true });
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
                        sourcePath = Util.FindSourceInSearchPath(spectrum.Source.Name, lastSourcePathLocation);
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
                            lastSourcePathLocation = Path.GetDirectoryName(eventArgs.SourcePath);
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
                    basicFilterChanged();
                }
        }

        public void peptidesTextBox_Leave (object sender, EventArgs e)
        {
            int value;
            if (int.TryParse((sender as ToolStripTextBox).Text, out value))
                if (value != dataFilter.MinimumDistinctPeptidesPerProtein)
                {
                    dataFilter.MinimumDistinctPeptidesPerProtein = value;
                    basicFilterChanged();
                }
        }

        public void spectraTextBox_Leave (object sender, EventArgs e)
        {
            int value;
            if (int.TryParse((sender as ToolStripTextBox).Text, out value))
                if (value != dataFilter.MinimumSpectraPerProtein)
                {
                    dataFilter.MinimumSpectraPerProtein = value;
                    basicFilterChanged();
                }
        }

        public void additionalPeptidesTextBox_Leave (object sender, EventArgs e)
        {
            int value;
            if (int.TryParse((sender as ToolStripTextBox).Text, out value))
                if (value != dataFilter.MinimumAdditionalPeptidesPerProtein)
                {
                    dataFilter.MinimumAdditionalPeptidesPerProtein = value;
                    basicFilterChanged();
                }
        }

        void basicFilterChanged ()
        {
            dataFilter.SetBasicFilterView(session);

            proteinTableForm.SetData(session, dataFilter);
            peptideTableForm.SetData(session, dataFilter);
            spectrumTableForm.SetData(session, dataFilter);
            modificationTableForm.SetData(session, dataFilter);
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

        void Form1_Load (object sender, EventArgs e)
        {
            openToolStripButton_Click(this, EventArgs.Empty);

            //Text = timer.Restart().TotalSeconds.ToString();

            // get all ambiguous interpretations of a peptide:
            // sequence is the same
            // spectrum is the same
            // rank is the same
            // total mass of modifications is the same
            // position of modifications is different


            // PEPTIDE/PROTEIN FILTERING

            // filter for QValue < 0.05
            // filter for spectra with only one top-ranked PSM
            // filter for peptides of at least 10 residues
            // filter for proteins with at least 2 distinct peptides (different sequences or the same sequence with different modifications)
        }

        void databaseNotFoundHandler (object sender, Parser.DatabaseNotFoundEventArgs e)
        {
            if (String.IsNullOrEmpty(e.DatabasePath))
                return;

            var findDirectoryDialog = new FolderBrowserDialog()
            {
                SelectedPath = @"h:\fasta",
                ShowNewFolderButton = false,
                Description = "Locate the directory containing the database \"" + e.DatabasePath + "\""
            };

            while (findDirectoryDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    e.DatabasePath = Util.FindDatabaseInSearchPath(e.DatabasePath, findDirectoryDialog.SelectedPath);
                    break;
                }
                catch
                {
                    // couldn't find the database in that directory; prompt user again
                }
            }
        }

        bool promptForSourceNotFound = true;
        void sourceNotFoundOnImportHandler (object sender, Parser.SourceNotFoundEventArgs e)
        {
            if (String.IsNullOrEmpty(e.SourcePath) || !promptForSourceNotFound)
                return;

            var findDirectoryDialog = new FolderBrowserDialog()
            {
                SelectedPath = @"C:\test\rpal-orbi-orbi\raw",
                ShowNewFolderButton = false,
                Description = "Locate the directory containing the source \"" + e.SourcePath + "\""
            };

            while (findDirectoryDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    e.SourcePath = Util.FindSourceInSearchPath(e.SourcePath, findDirectoryDialog.SelectedPath);
                    return;
                }
                catch
                {
                    // couldn't find the source in that directory; prompt user again
                }
            }

            // user cancelled; prompt them about whether to suppress this event
            if (MessageBox.Show("Source \"" + e.SourcePath + "\" not found: skipping subset mzML import to database.\r\n\r\n" +
                                "Do you want to skip all mzML imports for this session?",
                                "Skipping subset mzML import",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question) == DialogResult.Yes)
                promptForSourceNotFound = false;
        }

        void sourceNotFoundOnVisualizeHandler (object sender, Parser.SourceNotFoundEventArgs e)
        {
            if (String.IsNullOrEmpty(e.SourcePath))
                return;

            var findDirectoryDialog = new FolderBrowserDialog()
            {
                SelectedPath = @"C:\test\rpal-orbi-orbi\raw",
                ShowNewFolderButton = false,
                Description = "Locate the directory containing the source \"" + e.SourcePath + "\""
            };

            while (findDirectoryDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    e.SourcePath = Util.FindSourceInSearchPath(e.SourcePath, findDirectoryDialog.SelectedPath);
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

            public SimpleProgressForm ()
            {
                SizeGripStyle = SizeGripStyle.Show;
                ShowInTaskbar = true;
                TopLevel = true;
                TopMost = true;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowOnly;
                FormBorderStyle = FormBorderStyle.SizableToolWindow;
                StartPosition = FormStartPosition.CenterScreen;
                MaximizeBox = false;
                MinimizeBox = true;
                Size = new System.Drawing.Size(450, 50);

                progressBar = new ProgressBar();
                progressBar.Dock = DockStyle.Fill;
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Minimum = 0;
                progressBar.Maximum = 1;
                progressBar.Value = 0;
                Controls.Add(progressBar);
            }

            public void UpdateProgress (object sender, Parser.ParsingProgressEventArgs e)
            {
                progressBar.Maximum = (int) e.TotalBytes;
                progressBar.Value = (int) e.ParsedBytes;
                e.Cancel = !Visible || IsDisposed;
                Application.DoEvents();
            }

            public void UpdateProgress (object sender, StaticWeightQonverter.QonversionProgressEventArgs e)
            {
                progressBar.Maximum = e.TotalAnalyses;
                progressBar.Value = e.QonvertedAnalyses;
                e.Cancel = !Visible || IsDisposed;
                Application.DoEvents();
            }
        }

        void OpenFiles (IList<string> filepaths)
        {
            //try
            {
                var xml_filepaths = filepaths.Where(filepath => filepath.EndsWith(".idpXML") ||
                                                                filepath.EndsWith(".pepXML") ||
                                                                filepath.EndsWith(".pep.xml"));
                var idpDB_filepaths = filepaths.Where(filepath => filepath.EndsWith(".idpDB"));

                string commonFilename = "";
                Util.LongestCommonSubstring(filepaths, out commonFilename);
                if (String.IsNullOrEmpty(commonFilename))
                    commonFilename = Path.Combine(commonFilename, "idpicker-analysis-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssZ") + ".idpDB");
                else
                    commonFilename = Path.ChangeExtension(commonFilename.TrimEnd(' ', '_', '-'), ".idpDB");

                if (!idpDB_filepaths.Contains(commonFilename))
                    try { File.Delete(commonFilename); } catch { }

                if (xml_filepaths.Count() > 0)
                {
                    using (SimpleProgressForm pf = new SimpleProgressForm())
                    using (Parser parser = new Parser(commonFilename))
                    {
                        pf.Text = "Parsing...";
                        pf.Show();
                        parser.DatabaseNotFound += new EventHandler<Parser.DatabaseNotFoundEventArgs>(databaseNotFoundHandler);
                        if (xml_filepaths.Count() > 1)
                            parser.SourceNotFound += new EventHandler<Parser.SourceNotFoundEventArgs>(sourceNotFoundOnImportHandler);
                        else // if only importing one source, don't ask about skipping mzML import
                            parser.SourceNotFound += new EventHandler<Parser.SourceNotFoundEventArgs>(sourceNotFoundOnVisualizeHandler);
                        parser.ParsingProgress += new EventHandler<Parser.ParsingProgressEventArgs>(pf.UpdateProgress);
                        parser.ReadXml(".", xml_filepaths.ToArray());

                        if (!pf.Visible || pf.IsDisposed)
                            return;
                    }
                }

                using (SimpleProgressForm pf = new SimpleProgressForm())
                {
                    pf.Text = "Qonverting...";
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

                // read from file to memory database
                //http://www.sqlite.org/backup.html

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

                if (!hasFilterView)
                    dataFilter.SetBasicFilterView(session);

                breadCrumbControl.BreadCrumbs.Clear();
                breadCrumbControl.BreadCrumbs.Add(dataFilter.GetBasicFilterControls(this));

                proteinTableForm.SetData(session, dataFilter);
                spectrumTableForm.SetData(session, dataFilter);
                peptideTableForm.SetData(session, dataFilter);
                modificationTableForm.SetData(session, dataFilter);
            }
            /*catch (Exception ex)
            {
                string message = ex.ToString();
                if (ex.InnerException != null)
                    message += "\n\nAdditional information: " + ex.InnerException.ToString();
                MessageBox.Show(message,
                                "Unhandled Exception",
                                MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
                                0, false);
                Application.Exit();
            }*/
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