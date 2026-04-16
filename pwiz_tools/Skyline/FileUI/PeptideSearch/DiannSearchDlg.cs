/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class DiannSearchDlg : FormEx, IModifyDocumentContainer, IMultipleViewProvider
    {
        public SkylineWindow SkylineWindow { get; set; }
        public DiannSearchControl SearchControl { get; private set; }

        public enum Pages
        {
            data_files_page,
            fasta_page,
            modifications_page,
            search_settings_page,
            run_page
        }

        public class DataFilesPage : IFormView { }
        public class FastaPage : IFormView { }
        public class ModificationsPage : IFormView { }
        public class SearchSettingsPage : IFormView { }
        public class RunPage : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new DataFilesPage(), new FastaPage(), new ModificationsPage(), new SearchSettingsPage(), new RunPage()
        };

        public DiannSearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager)
        {
            SkylineWindow = skylineWindow;
            _libraryManager = libraryManager;
            _documents = new Stack<SrmDocument>();
            SetDocument(skylineWindow.Document, null);

            InitializeComponent();

            Icon = SkylineWindow.Icon;

            DataFileResults = new ImportResultsDIAControl(this);
            AddPageControl(DataFileResults, dataFilesPage, 2, 60);

            ImportFastaControl = new ImportFastaControl(this, skylineWindow.SequenceTree, false);
            ImportFastaControl.IsDDASearch = true;
            AddPageControl(ImportFastaControl, fastaPage, 2, 60);

            SearchControl = new DiannSearchControl(this);
            AddPageControl(SearchControl, runSearchPage, 2, 0);
            SearchControl.SearchFinished += SearchControlSearchFinished;

            // Set default config values
            numMs1Tolerance.Value = 0; // auto-detect
            numMs2Tolerance.Value = 0; // auto-detect
            numThreads.Value = Math.Min(Environment.ProcessorCount, numThreads.Maximum);

            // Populate modifications page with common defaults
            InitializeModifications();
        }

        private void InitializeModifications()
        {
            // Default fixed mod: Carbamidomethyl (C)
            var defaultFixed = UniMod.GetModification(@"Carbamidomethyl (C)", true);
            if (defaultFixed != null)
                listFixedMods.Items.Add(defaultFixed);

            // Default variable mod: Oxidation (M)
            var defaultVar = UniMod.GetModification(@"Oxidation (M)", true);
            if (defaultVar != null)
                listVariableMods.Items.Add(defaultVar);
        }

        public IEnumerable<StaticMod> FixedMods => listFixedMods.Items.Cast<StaticMod>();
        public IEnumerable<StaticMod> VariableMods => listVariableMods.Items.Cast<StaticMod>();

        private void btnAddFixedMod_Click(object sender, EventArgs e)
        {
            AddModification(listFixedMods);
        }

        private void btnAddVariableMod_Click(object sender, EventArgs e)
        {
            AddModification(listVariableMods);
        }

        private void btnRemoveFixedMod_Click(object sender, EventArgs e)
        {
            RemoveSelectedMod(listFixedMods);
        }

        private void btnRemoveVariableMod_Click(object sender, EventArgs e)
        {
            RemoveSelectedMod(listVariableMods);
        }

        private void AddModification(ListBox listBox)
        {
            var existing = new HashSet<string>(listFixedMods.Items.Cast<StaticMod>().Select(m => m.Name)
                .Concat(listVariableMods.Items.Cast<StaticMod>().Select(m => m.Name)));
            var newMod = Settings.Default.StaticModList.EditItem(this, null, Settings.Default.StaticModList, null);
            if (newMod != null && !existing.Contains(newMod.Name))
                listBox.Items.Add(newMod);
        }

        private static void RemoveSelectedMod(ListBox listBox)
        {
            if (listBox.SelectedIndex >= 0)
                listBox.Items.RemoveAt(listBox.SelectedIndex);
        }

        public ImportResultsDIAControl DataFileResults { get; set; }
        public ImportFastaControl ImportFastaControl { get; set; }

        public double Ms1Tolerance
        {
            get => (double)numMs1Tolerance.Value;
            set => numMs1Tolerance.Value = (decimal)value;
        }

        public double Ms2Tolerance
        {
            get => (double)numMs2Tolerance.Value;
            set => numMs2Tolerance.Value = (decimal)value;
        }

        public double QValueThreshold
        {
            get => double.TryParse(txtQValue.Text, out var v) ? v : 0.01;
            set => txtQValue.Text = value.ToString(@"G");
        }

        public int Threads
        {
            get => (int)numThreads.Value;
            set => numThreads.Value = value;
        }

        private readonly DiannConfig _diannConfig = new DiannConfig();

        public DiannConfig DiannSearchConfig
        {
            get
            {
                _diannConfig.Ms1Accuracy = Ms1Tolerance;
                _diannConfig.Ms2Accuracy = Ms2Tolerance;
                _diannConfig.QValue = QValueThreshold;
                _diannConfig.Threads = Threads;
                _diannConfig.MetExcision = cbMetExcision.Checked;
                _diannConfig.MaxMissedCleavages = (int)numMissedCleavages.Value;
                _diannConfig.ApplyAdditionalSettings();
                return _diannConfig;
            }
        }

        public void ShowAdditionalSettings()
        {
            KeyValueGridDlg.Show(this,
                PeptideSearchResources.SearchSettingsControl_Additional_Settings,
                _diannConfig.AdditionalSettings,
                setting => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value),
                setting => setting.ValidValues);
        }

        #region IMultipleViewProvider
        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = wizardPages.SelectedIndex));
                return TAB_PAGES[selectedIndex];
            }
        }
        #endregion

        #region IModifyDocumentContainer
        private readonly LibraryManager _libraryManager;
        private readonly Stack<SrmDocument> _documents;

        public SrmDocument Document => _documents.Count == 0 ? null : _documents.Peek();

        public string DocumentFilePath
        {
            get => SkylineWindow.DocumentFilePath;
            set => SkylineWindow.DocumentFilePath = value;
        }

        public bool SetDocument(SrmDocument docNew, SrmDocument docOriginal)
        {
            if (!ReferenceEquals(Document, docOriginal))
                return false;

            _documents.Push(docNew);
            return true;
        }

        public void Listen(EventHandler<DocumentChangedEventArgs> listener)
        {
            throw new NotImplementedException();
        }

        public void Unlisten(EventHandler<DocumentChangedEventArgs> listener)
        {
            throw new NotImplementedException();
        }

        public bool IsClosing => throw new NotImplementedException();
        public IEnumerable<BackgroundLoader> BackgroundLoaders => throw new NotImplementedException();
        public void AddBackgroundLoader(BackgroundLoader loader) => throw new NotImplementedException();
        public void RemoveBackgroundLoader(BackgroundLoader loader) => throw new NotImplementedException();

        public void ModifyDocumentNoUndo(Func<SrmDocument, SrmDocument> act)
        {
            ModifyDocument(null, act, null);
        }

        public void ModifyDocument(string description, Func<SrmDocument, SrmDocument> act, Func<SrmDocumentPair, AuditLogEntry> logFunc)
        {
            var docNew = act(Document);
            if (ReferenceEquals(docNew, Document))
                return;

            SetDocument(docNew, Document);
        }
        #endregion

        public Pages CurrentPage => (Pages)wizardPages.SelectedIndex;

        public void NextPage()
        {
            btnNext.Text = Resources.BuildLibraryDlg_btnPrevious_Click__Next__;

            if (CurrentPage == Pages.run_page)
            {
                ImportDiannLibrary();
                return;
            }

            // Validate current page before advancing
            switch (CurrentPage)
            {
                case Pages.data_files_page:
                    if (!DataFileResults.FoundResultsFiles.Any())
                    {
                        MessageDlg.Show(this, PeptideSearchResources.DiannSearchDlg_NextPage_Please_add_at_least_one_DIA_data_file_);
                        return;
                    }
                    break;
                case Pages.fasta_page:
                    if (!File.Exists(ImportFastaControl.FastaFile))
                    {
                        MessageDlg.Show(this, PeptideSearchResources.EncyclopeDiaSearchDlg_NextPage_A_FASTA_file_is_required_for_an_EncyclopeDia_Koina_search_);
                        return;
                    }
                    break;
            }

            if (wizardPages.SelectedIndex < wizardPages.TabPages.Count - 1)
                wizardPages.SelectTab(wizardPages.SelectedIndex + 1);

            switch (CurrentPage)
            {
                case Pages.search_settings_page:
                    btnNext.Text = PeptideSearchResources.EncyclopeDiaSearchDlg_NextPage_Run;
                    break;

                case Pages.run_page:
                    // Verify DIA-NN binary exists
                    if (!File.Exists(DiannHelpers.DiannBinary))
                    {
                        MessageDlg.Show(this, string.Format(
                            PeptideSearchResources.DiannSearchDlg_NextPage_DIA_NN_executable_not_found_at___0____Please_configure_the_path_via_Edit___Search_Tools_,
                            DiannHelpers.DiannBinary));
                        wizardPages.SelectTab((int)Pages.search_settings_page);
                        return;
                    }

                    btnNext.Text = Resources.BuildLibraryDlg_OkWizardPage_Finish;
                    btnNext.Enabled = false;
                    btnCancel.Enabled = false;
                    btnBack.Enabled = false;
                    ControlBox = false;
                    SearchControl.DiannConfig = DiannSearchConfig;
                    SearchControl.FastaFilePath = ImportFastaControl.FastaFile;
                    SearchControl.DataFiles = DataFileResults.FoundResultsFiles.Select(f => f.Path).ToList();
                    SearchControl.FixedMods = FixedMods.ToList();
                    SearchControl.VariableMods = VariableMods.ToList();
                    SearchControl.RunSearch();
                    return;
            }

            btnBack.Enabled = wizardPages.SelectedIndex > 0;
            btnNext.Enabled = wizardPages.SelectedIndex < wizardPages.TabPages.Count - 1;
        }

        private void ImportDiannLibrary()
        {
            string specLibPath = SearchControl.OutputSpecLibPath;

            if (string.IsNullOrEmpty(specLibPath) || !File.Exists(specLibPath))
            {
                MessageDlg.Show(this, PeptideSearchResources.DiannSearchDlg_ImportDiannLibrary_DIA_NN_output_spectral_library_not_found_);
                return;
            }

            // Launch Import Peptide Search dialog. The DIA-NN .parquet.skyline.speclib is
            // passed as a search file so BiblioSpec builds a .blib from it.
            using var importPeptideSearchDlg = new ImportPeptideSearchDlg(SkylineWindow, _libraryManager,
                false, ImportPeptideSearchDlg.Workflow.dia);
            importPeptideSearchDlg.BuildPepSearchLibControl.ForceWorkflow(ImportPeptideSearchDlg.Workflow.dia);
            importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(new[] { specLibPath });
            importPeptideSearchDlg.ImportFastaControl.SetFastaContent(ImportFastaControl.ImportSettings.FastaFile.Path, true);
            importPeptideSearchDlg.ImportFastaControl.Enzyme = ImportFastaControl.ImportSettings.Enzyme;
            importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = ImportFastaControl.ImportSettings.MaxMissedCleavages;

            if (importPeptideSearchDlg.ShowDialog(this) == DialogResult.OK)
                DialogResult = DialogResult.OK;
        }

        public void PreviousPage()
        {
            if (wizardPages.SelectedIndex == 0)
                return;

            btnNext.Text = Resources.BuildLibraryDlg_btnPrevious_Click__Next__;

            if (CurrentPage == Pages.run_page)
                btnNext.Text = PeptideSearchResources.EncyclopeDiaSearchDlg_NextPage_Run;

            wizardPages.SelectTab(wizardPages.SelectedIndex - 1);

            btnBack.Enabled = wizardPages.SelectedIndex > 0;
            btnNext.Enabled = wizardPages.SelectedIndex < wizardPages.TabPages.Count - 1;
        }

        private void SearchControlSearchFinished(bool success)
        {
            btnCancel.Enabled = true;
            btnBack.Enabled = true;
            ControlBox = true;
            if (success)
                btnNext.Enabled = true;
        }

        private void btnAdditionalSettings_Click(object sender, EventArgs e)
        {
            ShowAdditionalSettings();
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            NextPage();
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            PreviousPage();
        }

        private static void AddPageControl<TControl>(TControl pageControl, TabPage tabPage, int border, int header)
            where TControl : UserControl
        {
            pageControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pageControl.Location = new Point(border, header);
            pageControl.Width = tabPage.Width - border * 2;
            pageControl.Height = tabPage.Height - header - border;
            tabPage.Controls.Add(pageControl);
        }
    }

    public class DiannSearchControl : SearchControl
    {
        public DiannSearchDlg HostDialog { get; }

        public DiannSearchControl(DiannSearchDlg hostControl)
        {
            Parent = HostDialog = hostControl;
        }

        public DiannConfig DiannConfig { get; set; }
        public string FastaFilePath { get; set; }
        public List<string> DataFiles { get; set; }
        public List<StaticMod> FixedMods { get; set; }
        public List<StaticMod> VariableMods { get; set; }
        public string OutputSpecLibPath { get; private set; }

        private bool Search(CancellationTokenSource token, IProgressStatus status)
        {
            try
            {
                string outputDir = Path.GetDirectoryName(HostDialog.SkylineWindow.DocumentFilePath) ?? string.Empty;

                OutputSpecLibPath = DiannHelpers.RunSearch(
                    DataFiles, FastaFilePath, outputDir, DiannConfig,
                    this, ref status, token.Token,
                    FixedMods, VariableMods);

                if (OutputSpecLibPath == null)
                {
                    UpdateProgress(status.ChangeErrorException(
                        new IOException(PeptideSearchResources.DiannSearchControl_Search_DIA_NN_search_did_not_produce_a_spectral_library_output_)));
                    return false;
                }
            }
            catch (OperationCanceledException e)
            {
                UpdateProgress(status.ChangeWarningMessage(e.InnerException?.Message ?? e.Message));
                return false;
            }
            catch (Exception e)
            {
                UpdateProgress(status.ChangeErrorException(e));
                return false;
            }

            return !token.IsCancellationRequested;
        }

        public override void RunSearch()
        {
            txtSearchProgress.Text = string.Empty;
            _progressTextItems.Clear();
            btnCancel.Enabled = progressBar.Visible = true;

            _cancelToken = new CancellationTokenSource();

            ActionUtil.RunAsync(RunSearchAsync, @"DIA-NN Search thread");
        }

        public void RunSearchAsync()
        {
            IProgressStatus status = new ProgressStatus();
            bool success = true;

            if (!_cancelToken.IsCancellationRequested)
            {
                Invoke(new MethodInvoker(() => UpdateSearchEngineProgress(
                    status.ChangeMessage(PeptideSearchResources.DDASearchControl_SearchProgress_Starting_search))));

                success = Search(_cancelToken, status);

                Invoke(new MethodInvoker(() => UpdateSearchEngineProgressMilestone(status, success, status.SegmentCount,
                    Resources.DDASearchControl_SearchProgress_Search_canceled,
                    PeptideSearchResources.DDASearchControl_SearchProgress_Search_failed,
                    Resources.DDASearchControl_SearchProgress_Search_done)));
            }

            Invoke(new MethodInvoker(() =>
            {
                UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.NoProgress, 0);
                btnCancel.Enabled = false;
                OnSearchFinished(success);
            }));
        }
    }
}
