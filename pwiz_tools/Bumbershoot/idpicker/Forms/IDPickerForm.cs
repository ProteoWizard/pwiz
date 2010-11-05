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
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;

using IDPicker.Forms;
using IDPicker.Controls;
using IDPicker.DataModel;

using DigitalRune.Windows.Docking;
using pwiz.CLI.cv;
using seems;
using NHibernate;
using NHibernate.Linq;
using NHibernate.Criterion;
using BrightIdeasSoftware;
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

        private DataFilter dataFilter = new DataFilter();
        private IDictionary<Analysis, QonverterSettings> qonverterSettingsByAnalysis;

        string[] args;
        private List<LayoutProperty> _userLayoutList;

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

            analysisTableForm = new AnalysisTableForm();
            analysisTableForm.Show(dockPanel, DockState.Document);

            proteinTableForm.ProteinViewFilter += new ProteinViewFilterEventHandler(proteinTableForm_ProteinViewFilter);
            proteinTableForm.ProteinViewVisualize += new EventHandler<ProteinViewVisualizeEventArgs>(proteinTableForm_ProteinViewVisualize);
            peptideTableForm.PeptideViewFilter += new PeptideViewFilterEventHandler(peptideTableForm_PeptideViewFilter); 
            spectrumTableForm.SpectrumViewFilter += new EventHandler<DataFilter>(spectrumTableForm_SpectrumViewFilter);
            spectrumTableForm.SpectrumViewVisualize += new EventHandler<SpectrumViewVisualizeEventArgs>(spectrumTableForm_SpectrumViewVisualize);
            modificationTableForm.ModificationViewFilter += new ModificationViewFilterEventHandler(modificationTableForm_ModificationViewFilter);

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

        #region Handling of events for basic filter toolstrip items

        IList<ToolStripItem> GetBasicFilterControls ()
        {
            var result = new List<ToolStripItem>();

            result.Add(new ToolStripLabel() { Text = "Q-value ≤ ", RightToLeft = RightToLeft.No });
            var qvalueTextBox = new ToolStripTextBox() { Width = 40, RightToLeft = RightToLeft.No, BorderStyle = BorderStyle.FixedSingle, Text = dataFilter.MaximumQValue.ToString() };
            qvalueTextBox.KeyDown += new KeyEventHandler(doubleTextBox_KeyDown);
            qvalueTextBox.Leave += new EventHandler(qvalueTextBox_Leave);
            result.Add(qvalueTextBox);

            result.Add(new ToolStripLabel() { Text = "  Distinct Peptides ≥ ", RightToLeft = RightToLeft.No });
            var peptidesTextBox = new ToolStripTextBox() { Width = 20, RightToLeft = RightToLeft.No, BorderStyle = BorderStyle.FixedSingle, Text = dataFilter.MinimumDistinctPeptidesPerProtein.ToString() };
            peptidesTextBox.KeyDown += new KeyEventHandler(integerTextBox_KeyDown);
            peptidesTextBox.Leave += new EventHandler(peptidesTextBox_Leave);
            result.Add(peptidesTextBox);

            result.Add(new ToolStripLabel() { Text = "  Spectra ≥ ", RightToLeft = RightToLeft.No });
            var spectraTextBox = new ToolStripTextBox() { Width = 20, RightToLeft = RightToLeft.No, BorderStyle = BorderStyle.FixedSingle, Text = dataFilter.MinimumSpectraPerProtein.ToString() };
            spectraTextBox.KeyDown += new KeyEventHandler(integerTextBox_KeyDown);
            spectraTextBox.Leave += new EventHandler(spectraTextBox_Leave);
            result.Add(spectraTextBox);

            result.Add(new ToolStripLabel() { Text = "  Additional Peptides ≥ ", RightToLeft = RightToLeft.No });
            var additionalPeptidesTextBox = new ToolStripTextBox() { Width = 20, RightToLeft = RightToLeft.No, BorderStyle = BorderStyle.FixedSingle, Text = dataFilter.MinimumAdditionalPeptidesPerProtein.ToString() };
            additionalPeptidesTextBox.KeyDown += new KeyEventHandler(integerTextBox_KeyDown);
            additionalPeptidesTextBox.Leave += new EventHandler(additionalPeptidesTextBox_Leave);
            result.Add(additionalPeptidesTextBox);

            result[0].Tag = dataFilter;

            return result;
        }

        static void doubleTextBox_KeyDown (object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Decimal || e.KeyCode == Keys.OemPeriod)
            {
                if ((sender as ToolStripTextBox).Text.Contains('.'))
                    e.SuppressKeyPress = true;
            }
            else if (!(e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9 ||
                    e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9 ||
                    e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back ||
                    e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
                e.SuppressKeyPress = true;
        }

        static void integerTextBox_KeyDown (object sender, KeyEventArgs e)
        {
            if (!(e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9 ||
                e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9 ||
                e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back ||
                e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
                e.SuppressKeyPress = true;
        }

        void qvalueTextBox_Leave (object sender, EventArgs e)
        {
            decimal value;
            if (decimal.TryParse((sender as ToolStripTextBox).Text, out value))
                if (value != dataFilter.MaximumQValue)
                {
                    dataFilter.MaximumQValue = value;
                    applyBasicFilter();
                }
        }

        void peptidesTextBox_Leave (object sender, EventArgs e)
        {
            int value;
            if (int.TryParse((sender as ToolStripTextBox).Text, out value))
                if (value != dataFilter.MinimumDistinctPeptidesPerProtein)
                {
                    dataFilter.MinimumDistinctPeptidesPerProtein = value;
                    applyBasicFilter();
                }
        }

        void spectraTextBox_Leave (object sender, EventArgs e)
        {
            int value;
            if (int.TryParse((sender as ToolStripTextBox).Text, out value))
                if (value != dataFilter.MinimumSpectraPerProtein)
                {
                    dataFilter.MinimumSpectraPerProtein = value;
                    applyBasicFilter();
                }
        }

        void additionalPeptidesTextBox_Leave (object sender, EventArgs e)
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
                oldFilter.FilterSource == modificationTableForm ||
                oldFilter.FilterSource == analysisTableForm)
                proteinTableForm.SetData(session, dataFilter);

            peptideTableForm.SetData(session, dataFilter);
            spectrumTableForm.SetData(session, dataFilter);
            modificationTableForm.SetData(session, dataFilter);
            analysisTableForm.SetData(session, dataFilter);
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
                oldFilter.FilterSource == modificationTableForm ||
                oldFilter.FilterSource == analysisTableForm)
                peptideTableForm.SetData(session, dataFilter);

            spectrumTableForm.SetData(session, dataFilter);
            modificationTableForm.SetData(session, dataFilter);
            analysisTableForm.SetData(session, dataFilter);
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
                oldFilter.FilterSource == modificationTableForm ||
                oldFilter.FilterSource == analysisTableForm)
                spectrumTableForm.SetData(session, dataFilter);

            modificationTableForm.SetData(session, dataFilter);
            analysisTableForm.SetData(session, dataFilter);
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
                oldFilter.FilterSource == spectrumTableForm ||
                oldFilter.FilterSource == analysisTableForm)
                modificationTableForm.SetData(session, dataFilter);

            analysisTableForm.SetData(session, dataFilter);
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

            if (oldFilter.Analysis == null)
                analysisTableForm.SetData(session, dataFilter);
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
            analysisTableForm.ClearData();
        }

        void setData (object sender, RunWorkerCompletedEventArgs e)
        {
            dataFilter.FilteringProgress -= new EventHandler<DataFilter.FilteringProgressEventArgs>(applyBasicFilterProgressForm.UpdateProgress);
            applyBasicFilterProgressForm.Dispose();

            proteinTableForm.SetData(session, dataFilter);
            peptideTableForm.SetData(session, dataFilter);
            spectrumTableForm.SetData(session, dataFilter);
            modificationTableForm.SetData(session, dataFilter);
            analysisTableForm.SetData(session, dataFilter);
        }

        void Form1_Load (object sender, EventArgs e)
        {
            //System.Data.SQLite.SQLiteConnection.SetConfigOption(SQLiteConnection.SQLITE_CONFIG.MULTITHREAD);
            //var filepaths = Directory.GetFiles(@"c:\test\Goldenring_gastric\Metaplasia", "klc*FFPE*.pepXML", SearchOption.AllDirectories);
            //OpenFiles(filepaths);//.Take(10).Union(filepaths.Skip(200).Take(10)).Union(filepaths.Skip(400).Take(10)).ToList());
            //return;

            //Get user layout profiles
            RefreshUserLayoutList();
            if (_userLayoutList.Count < 2 || _userLayoutList[1].Name != "User Default")
                ResetUserLayoutSettings();
            LoadLayout(_userLayoutList[1]);
            var temp = Properties.Settings.Default.UserLayouts;

            if (args != null && args.Length > 0 && args.All(o => File.Exists(o)))
                OpenFiles(args);
            else
                openToolStripButton_Click(this, EventArgs.Empty);
        }

        private void LoadLayout(LayoutProperty userLayout)
        {
            try
            {
                var tempFilepath = Path.GetTempFileName();
                using(var tempFile = new StreamWriter(tempFilepath, false, Encoding.Unicode))
                    tempFile.Write(userLayout.PaneLocations);

                dockPanel.SuspendLayout();
                dockPanel.LoadFromXml(tempFilepath, DeserializeForm);
                dockPanel.ResumeLayout(true, true);
                File.Delete(tempFilepath);

                if (userLayout.HasCustomColumnSettings && proteinTableForm != null &&
                    peptideTableForm != null && spectrumTableForm != null)
                {
                    var columnList = userLayout.SettingsList.Where(o => o.Scope == "ProteinTableForm");
                    proteinTableForm.LoadLayout(columnList.ToList());

                    columnList = userLayout.SettingsList.Where(o => o.Scope == "PeptideTableForm");
                    peptideTableForm.LoadLayout(columnList.ToList());

                    columnList = userLayout.SettingsList.Where(o => o.Scope == "SpectrumTableForm");
                    spectrumTableForm.LoadLayout(columnList.ToList());
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Error encountered while trying to load saved layout.");
            }

        }

        private IDockableForm DeserializeForm(string persistantString)
        {
            return null;
        }

        private void ResetUserLayoutSettings()
        {
            _userLayoutList = new List<LayoutProperty>();

            SaveNewLayout("System Default", true, false);
            SaveNewLayout("User Default", true, false);

            SaveUserLayoutList();
        }

        private void RefreshUserLayoutList()
        {
            var retrievedList = new List<string>(Util.StringCollectionToStringArray(Properties.Settings.Default.UserLayouts));
            _userLayoutList = new List<LayoutProperty>();

            //stick with an empty list if not in the correct format
            if (retrievedList.Count == 0 || !retrievedList[0].StartsWith("System Default"))
                return;

            for (var x = 0; x < retrievedList.Count; x++)
            {
                var items = retrievedList[x].Split('|');
                var customColumnList = new List<ColumnProperty>();
                if (bool.Parse(items[2]))
                {
                    //ProteinForm
                    customColumnList.AddRange(
                        ColumnSettingStringToIdpColumnPropertyList(
                        Util.StringCollectionToStringArray(Properties.Settings.Default.ProteinTableFormSettings),
                        "ProteinTableForm", x)
                        );

                    //PeptideForm
                    customColumnList.AddRange(
                        ColumnSettingStringToIdpColumnPropertyList(
                        Util.StringCollectionToStringArray(Properties.Settings.Default.PeptideTableFormSettings),
                        "PeptideTableForm", x)
                        );

                    //SpectrumForm
                    customColumnList.AddRange(
                        ColumnSettingStringToIdpColumnPropertyList(
                        Util.StringCollectionToStringArray(Properties.Settings.Default.SpectrumTableFormSettings),
                        "SpectrumTableForm", x)
                        );
                }

                var newLayout = new LayoutProperty
                {
                    Name = items[0],
                    PaneLocations = items[1],
                    HasCustomColumnSettings = bool.Parse(items[2]),
                    SettingsList = customColumnList
                };
                foreach (var item in newLayout.SettingsList)
                    item.Layout = newLayout;

                _userLayoutList.Add(newLayout);
            }
        }

        private List<ColumnProperty> ColumnSettingStringToIdpColumnPropertyList(string[] settings, string associatedForm, int associatedLayout)
        {
            //User properties will be in format:
            //"(int)LayoutIndex
            //(string)Column1Name|(string)Type|(int)DecimalPlaces|(int)CellColorCode|(bool)Visible|(bool)Locked
            //(string)Column2Name|(string)Type|(int)DecimalPlaces|(int)CellColorCode|(bool)Visible|(bool)Locked
            //(string)Column3Name|(string)Type|(int)DecimalPlaces|(int)CellColorCode|(bool)Visible|(bool)Locked
            //(string)Column4Name|(string)Type|(int)DecimalPlaces|(int)CellColorCode|(bool)Visible|(bool)Locked
            //(int)BackColorCode
            //(int)TextColorCode"

            var columnList = new List<ColumnProperty>();

            foreach (var setting in settings)
            {
                var lines = setting.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                if (int.Parse(lines[0]) == associatedLayout)
                {
                    for (var x = 1; x < lines.Count() - 2; x++)
                    {
                        var items = lines[x].Split('|');
                        bool tempbool;
                        var canParse = bool.TryParse(items[5], out tempbool);

                        columnList.Add(new ColumnProperty
                        {
                            Name = items[0],
                            Type = items[1],
                            DecimalPlaces = int.Parse(items[2]),
                            ColorCode = int.Parse(items[3]),
                            Visible = bool.Parse(items[4]),
                            Locked = canParse ? bool.Parse(items[5]) : (bool?)null,
                            Scope = associatedForm
                        });
                    }

                    columnList.Add(new ColumnProperty
                    {
                        Name = "BackColor",
                        Type = "GlobalSetting",
                        DecimalPlaces = -1,
                        ColorCode = int.Parse(lines[lines.Count() - 2]),
                        Visible = false,
                        Locked = null,
                        Scope = associatedForm
                    });

                    columnList.Add(new ColumnProperty
                    {
                        Name = "TextColor",
                        Type = "GlobalSetting",
                        DecimalPlaces = -1,
                        ColorCode = int.Parse(lines[lines.Count() - 1]),
                        Visible = false,
                        Locked = null,
                        Scope = associatedForm
                    });

                    break;
                }
            }
            return columnList;
        }

        private void SaveNewLayout(string layoutName, bool saveColumns, bool isDatabase)
        {
            var customColumnList = new List<ColumnProperty>();
            if (saveColumns)
            {
                customColumnList.AddRange(proteinTableForm.GetCurrentProperties());
                customColumnList.AddRange(peptideTableForm.GetCurrentProperties());
                customColumnList.AddRange(spectrumTableForm.GetCurrentProperties());
            }

            var tempLayout = new LayoutProperty()
            {
                Name = layoutName,
                PaneLocations = GetPanelLocations(),
                HasCustomColumnSettings = saveColumns,
            };
            foreach (var item in customColumnList)
                item.Layout = tempLayout;

            tempLayout.SettingsList = customColumnList;

            if (isDatabase)
            {
                lock (session)
                {
                    session.Save(tempLayout);
                    session.Flush();
                }
            }
            else
            {
                tempLayout.SettingsList = customColumnList;
                _userLayoutList.Add(tempLayout);
                SaveUserLayoutList();
            }
        }


        private void UpdateLayout(LayoutProperty layoutProperty, bool saveColumns, bool isDatabase)
        {
            var customColumnList = new List<ColumnProperty>();
            if (saveColumns)
            {
                customColumnList.AddRange(proteinTableForm.GetCurrentProperties());
                customColumnList.AddRange(peptideTableForm.GetCurrentProperties());
                customColumnList.AddRange(spectrumTableForm.GetCurrentProperties());
            }

            layoutProperty.PaneLocations = GetPanelLocations();
            layoutProperty.HasCustomColumnSettings = saveColumns;
            foreach (var item in customColumnList)
                item.Layout = layoutProperty;

            if (isDatabase)
            {
                lock (session)
                {
                    if (layoutProperty.SettingsList.Count > 0)
                        foreach (var item in layoutProperty.SettingsList)
                            session.Delete(item);

                    layoutProperty.SettingsList = customColumnList;
                    session.Save(layoutProperty);
                    session.Flush();
                }
            }
            else
            {
                layoutProperty.SettingsList = customColumnList;
                SaveUserLayoutList();
            }
        }

        private void SaveUserLayoutList()
        {
            Properties.Settings.Default.UserLayouts.Clear();
            Properties.Settings.Default.ProteinTableFormSettings.Clear();
            Properties.Settings.Default.PeptideTableFormSettings.Clear();
            Properties.Settings.Default.SpectrumTableFormSettings.Clear();

            //Layout properties will be in format:
            //"(string)Name|(string)XML|(bool)CustomColumns"
            for (var x = 0; x < _userLayoutList.Count; x++)
            {
                //Save Layout
                Properties.Settings.Default.UserLayouts.Add(string.Format("{0}|{1}|{2}{3}",
                    _userLayoutList[x].Name, _userLayoutList[x].PaneLocations,
                    _userLayoutList[x].HasCustomColumnSettings, Environment.NewLine));
                Properties.Settings.Default.Save();

                //Save column settings
                if (_userLayoutList[x].HasCustomColumnSettings)
                {
                    //Protein Form
                    var columnSettings = _userLayoutList[x].SettingsList.Where(o => o.Scope == "ProteinTableForm");
                    proteinTableForm.SaveUserSettings(columnSettings.ToList(), x);

                    //Peptide Form
                    columnSettings = _userLayoutList[x].SettingsList.Where(o => o.Scope == "PeptideTableForm");
                    peptideTableForm.SaveUserSettings(columnSettings.ToList(), x);

                    //Spectrum Form
                    columnSettings = _userLayoutList[x].SettingsList.Where(o => o.Scope == "SpectrumTableForm");
                    spectrumTableForm.SaveUserSettings(columnSettings.ToList(), x);
                }
            }
        }

        private string GetPanelLocations()
        {
            var tempFilepath = Path.GetTempFileName();
            dockPanel.SaveAsXml(tempFilepath);
            string locationXml = File.ReadAllText(tempFilepath);
            File.Delete(tempFilepath);
            return locationXml;
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
                progressBar.Value = e.TotalBytes == 0 ? 0 : Math.Min(progressBar.Maximum, (int) Math.Round((double) e.ParsedBytes / e.TotalBytes * 1000.0));
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
                progressBar.Value = Math.Min(progressBar.Maximum, (int) Math.Round((double) e.MergedFiles / e.TotalFiles * 1000.0));
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

            public void UpdateProgress (object sender, Qonverter.QonversionProgressEventArgs e)
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
                    using (Parser parser = new Parser(".", qonverterSettingsHandler, false, xml_filepaths.ToArray()))
                    {
                        pf.Text = "Initializing parser...";
                        pf.Show();
                        parser.DatabaseNotFound += new EventHandler<Parser.DatabaseNotFoundEventArgs>(databaseNotFoundHandler);
                        parser.SourceNotFound += new EventHandler<Parser.SourceNotFoundEventArgs>(sourceNotFoundOnImportHandler);
                        parser.ParsingProgress += new EventHandler<Parser.ParsingProgressEventArgs>(pf.UpdateProgress);

                        if (parser.Start() || !pf.Visible || pf.IsDisposed)
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

                var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(commonFilename, false, true);

                using (SimpleProgressForm pf = new SimpleProgressForm(this))
                {
                    pf.Text = "Initializing Qonverter...";
                    pf.Show();

                    var qonverter = new Qonverter();

                    // reload qonverter settings because the ids may change after merging
                    session = sessionFactory.OpenSession();
                    qonverterSettingsByAnalysis = session.Query<QonverterSettings>().ToDictionary(o => session.Get<Analysis>(o.Id));
                    session.Close();

                    qonverterSettingsByAnalysis.ForEach(o => qonverter.SettingsByAnalysis[(int) o.Key.Id] = o.Value.ToQonverterSettings());
                    qonverter.QonversionProgress += new Qonverter.QonversionProgressEventHandler(pf.UpdateProgress);

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

                session = sessionFactory.OpenSession();
                session.CreateSQLQuery("PRAGMA temp_store=MEMORY").ExecuteUpdate();

                //make sure to ignore "Abandoned" group
                dataFilter.SpectrumSourceGroup = session.QueryOver<DataModel.SpectrumSourceGroup>().Where(g => g.Name == "/").SingleOrDefault();

                //set or save default layout
                var defaultLayout = session.QueryOver<LayoutProperty>().Where(x => x.Name == "Database Default").SingleOrDefault();
                if (defaultLayout == null)
                    SaveNewLayout("Database Default", true, true);
                else
                    LoadLayout(defaultLayout);

                breadCrumbControl.BreadCrumbs.Clear();
                breadCrumbControl.BreadCrumbs.Add(GetBasicFilterControls());

                var savedFilter = DataFilter.LoadFilter(session);

                if (savedFilter == null)
                    applyBasicFilter();
                else
                {
                    dataFilter.MaximumQValue = savedFilter.MaximumQValue;
                    dataFilter.MinimumDistinctPeptidesPerProtein = savedFilter.MinimumDistinctPeptidesPerProtein;
                    dataFilter.MinimumSpectraPerProtein = savedFilter.MinimumSpectraPerProtein;
                    dataFilter.MinimumAdditionalPeptidesPerProtein = savedFilter.MinimumAdditionalPeptidesPerProtein;

                    proteinTableForm.SetData(session, dataFilter);
                    peptideTableForm.SetData(session, dataFilter);
                    spectrumTableForm.SetData(session, dataFilter);
                    modificationTableForm.SetData(session, dataFilter);
                    analysisTableForm.SetData(session, dataFilter);
                }
            }
            catch (Exception ex)
            {
                HandleException(this, ex);
            }
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
            SaveUserLayoutList();
        }

        private void LoadLayoutMenu()
        {
            var noDatabase = session == null;
            var currentUserMenuLevel = new List<ToolStripMenuItem>();
            ToolStripMenuItem saveMenu;
            ToolStripMenuItem loadMenu;
            ToolStripMenuItem deleteMenu;

            #region Load Options
            //set up user load options
            foreach (var item in _userLayoutList)
            {
                var tempItem = item;
                var newOption = new ToolStripMenuItem { Text = item.Name };
                newOption.Click += (s, e) => LoadLayout(tempItem);
                currentUserMenuLevel.Add(newOption);
            }

            //check if more needs to be done
            if (noDatabase)
                loadMenu = new ToolStripMenuItem("Load", null, currentUserMenuLevel.ToArray());
            else
            {
                var currentDatabaseMenuLevel = new List<ToolStripMenuItem>();
                var userMenu = new ToolStripMenuItem("User", null, currentUserMenuLevel.ToArray());

                IList<LayoutProperty> databaseLayouts;
                lock (session)
                    databaseLayouts = session.QueryOver<LayoutProperty>().List();

                foreach (var item in databaseLayouts)
                {
                    var tempItem = item;
                    var newOption = new ToolStripMenuItem { Text = item.Name };
                    newOption.Click += (s, e) => LoadLayout(tempItem);
                    currentDatabaseMenuLevel.Add(newOption);
                }
                var databaseMenu = new ToolStripMenuItem("Database", null, currentDatabaseMenuLevel.ToArray());

                loadMenu = new ToolStripMenuItem("Load", null, userMenu, databaseMenu);
            }
            #endregion

            #region Save Options

            //create user save list
            currentUserMenuLevel = new List<ToolStripMenuItem>();
            foreach (var item in _userLayoutList)
            {
                var tempItem = item;
                var newOption = new ToolStripMenuItem { Text = item.Name };
                newOption.Click += (s, e) =>
                {
                    var saveColumns = false;
                    if (MessageBox.Show("Save column settings as well?", "Save", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        saveColumns = true;
                    UpdateLayout(tempItem, saveColumns, false);
                };
                currentUserMenuLevel.Add(newOption);
            }

            //replace system default (not editable) with new layout option
            {
                var newLayout = new ToolStripMenuItem("New Layout");
                newLayout.Click +=
                    (s, e) =>
                    {
                        var textInput = new TextInputBox();
                        if (textInput.ShowDialog() == DialogResult.OK)
                            SaveNewLayout(textInput.inputTextBox.Text, textInput.inputCheckBox.Checked, false);
                    };
                currentUserMenuLevel.RemoveAt(0);
                currentUserMenuLevel.Insert(0, newLayout);
            }

            //check if more needs to be done
            if (noDatabase)
                saveMenu = new ToolStripMenuItem("Save", null, currentUserMenuLevel.ToArray());
            else
            {
                var currentDatabaseMenuLevel = new List<ToolStripMenuItem>();
                var userMenu = new ToolStripMenuItem("User", null, currentUserMenuLevel.ToArray());

                IList<LayoutProperty> databaseLayouts;
                lock (session)
                    databaseLayouts = session.QueryOver<LayoutProperty>().List();

                foreach (var item in databaseLayouts)
                {
                    var newOption = new ToolStripMenuItem { Text = item.Name };
                    LayoutProperty tempItem = item;
                    newOption.Click += (s, e) =>
                    {
                        var saveColumns = false;
                        if (MessageBox.Show("Save column settings as well?", "Save", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            saveColumns = true;
                        UpdateLayout(tempItem, saveColumns, true);
                    };
                    currentDatabaseMenuLevel.Add(newOption);
                }

                //Add new layout option
                {
                    var newLayout = new ToolStripMenuItem("New Layout");
                    newLayout.Click +=
                        (s, e) =>
                        {
                            var textInput = new TextInputBox();
                            if (textInput.ShowDialog() == DialogResult.OK)
                                SaveNewLayout(textInput.inputTextBox.Text, textInput.inputCheckBox.Checked, true);
                        };
                    currentDatabaseMenuLevel.Insert(0, newLayout);
                }
                var databaseMenu = new ToolStripMenuItem("Database", null, currentDatabaseMenuLevel.ToArray());

                saveMenu = new ToolStripMenuItem("Save", null, userMenu, databaseMenu);
            }

            #endregion

            #region Delete Options
            //set up user delete options
            currentUserMenuLevel = new List<ToolStripMenuItem>();
            foreach (var item in _userLayoutList)
            {
                var tempItem = item;
                var newOption = new ToolStripMenuItem { Text = item.Name };
                newOption.Click += (s, e) =>
                {
                    if (MessageBox.Show(string.Format("Are you sure you want to delete '{0}'?", tempItem.Name), "Delete", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        _userLayoutList.Remove(tempItem);
                        SaveUserLayoutList();
                    }
                };
                currentUserMenuLevel.Add(newOption);
            }
            //Dont allow user to delete defaults
            currentUserMenuLevel.RemoveRange(0, 2);
            //dont delete if nothing to delete, but check for database first
            if (noDatabase)
                deleteMenu = currentUserMenuLevel.Count > 0 ?
                    new ToolStripMenuItem("Delete", null, currentUserMenuLevel.ToArray()) :
                    null;
            else
            {
                var currentDatabaseMenuLevel = new List<ToolStripMenuItem>();
                var userMenu = currentUserMenuLevel.Count > 0 ?
                    new ToolStripMenuItem("User", null, currentUserMenuLevel.ToArray()) :
                    null;

                IList<LayoutProperty> databaseLayouts;
                lock (session)
                    databaseLayouts = session.QueryOver<LayoutProperty>().List();

                foreach (var item in databaseLayouts)
                {
                    var tempItem = item;
                    var newOption = new ToolStripMenuItem { Text = item.Name };
                    newOption.Click += (s, e) =>
                    {
                        if (MessageBox.Show(string.Format("Are you sure you want to delete '{0}'?", tempItem.Name), "Delete", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        {
                            session.Delete(tempItem);
                            session.Flush();
                        }
                    };
                    currentDatabaseMenuLevel.Add(newOption);
                }
                currentDatabaseMenuLevel.RemoveAt(0);
                var databaseMenu = currentDatabaseMenuLevel.Count > 0 ?
                    new ToolStripMenuItem("Database", null, currentDatabaseMenuLevel.ToArray()) :
                    null;

                if (userMenu == null)
                    if (databaseMenu == null)
                        deleteMenu = null;
                    else
                        deleteMenu = new ToolStripMenuItem("Delete", null, databaseMenu);
                else if (databaseMenu == null)
                    deleteMenu = new ToolStripMenuItem("Delete", null, userMenu);
                else
                    deleteMenu = new ToolStripMenuItem("Delete", null, userMenu, databaseMenu);


            }

            #endregion

            layoutMenuStrip.Items.Clear();
            layoutMenuStrip.Items.Add(saveMenu);
            layoutMenuStrip.Items.Add(loadMenu);
            if (deleteMenu != null)
                layoutMenuStrip.Items.Add(deleteMenu);
        }

        private void layoutButton_Click(object sender, EventArgs e)
        {
            LoadLayoutMenu();
            layoutMenuStrip.Show(Cursor.Position);
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