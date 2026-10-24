/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    /// <summary>
    /// The Osprey Search wizard: the DIA runs, the protein FASTA Osprey predicts its library from
    /// (or an existing library in its place), the modifications, the search settings, and the run.
    /// Osprey writes the document library directly, so Finish hands that .blib, the FASTA settings
    /// and the runs to the Import Peptide Search wizard's existing-library route, which adds the
    /// library to the document and imports the runs.
    /// </summary>
    public partial class OspreySearchDlg : FormEx, IModifyDocumentContainer, IMultipleViewProvider
    {
        public SkylineWindow SkylineWindow { get; }
        public OspreySearchControl SearchControl { get; }

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

        private readonly LibraryManager _libraryManager;
        private readonly Stack<SrmDocument> _documents;

        public OspreySearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager)
        {
            SkylineWindow = skylineWindow;
            _libraryManager = libraryManager;
            _documents = new Stack<SrmDocument>();
            SetDocument(skylineWindow.Document, null);

            InitializeComponent();

            Icon = SkylineWindow.Icon;

            DataFileResults = new ImportResultsDIAControl(this);
            AddPageControl(DataFileResults, dataFilesPage, 2, 60, 0);

            ImportFastaControl = new ImportFastaControl(this, skylineWindow.SequenceTree, false);
            ImportFastaControl.IsDDASearch = true;
            // Leave the bottom of the FASTA page to the existing-library alternative.
            AddPageControl(ImportFastaControl, fastaPage, 2, 60, fastaPage.Height - cbUseExistingLibrary.Top + 6);

            SearchControl = new OspreySearchControl(this);
            AddPageControl(SearchControl, runSearchPage, 2, 0, 0);
            SearchControl.SearchFinished += SearchControlSearchFinished;

            comboFragmentUnit.Items.AddRange(new object[]
            {
                PeptideSearchResources.OspreySearchDlg_FragmentUnit_ppm,
                PeptideSearchResources.OspreySearchDlg_FragmentUnit_m_z
            });
            comboResolution.Items.AddRange(new object[]
            {
                PeptideSearchResources.OspreySearchDlg_Resolution_High_resolution__Orbitrap__TOF_,
                PeptideSearchResources.OspreySearchDlg_Resolution_Unit_resolution__Stellar_
            });
            IsHighResolution = true; // Sets the tolerance and its unit to the high-resolution defaults
            numThreads.Maximum = MAX_THREAD_COUNT;
            numThreads.Value = Math.Min(Environment.ProcessorCount, MAX_THREAD_COUNT);

            InitializeModifications();
            UpdateExistingLibraryControls();
        }

        public ImportResultsDIAControl DataFileResults { get; }
        public ImportFastaControl ImportFastaControl { get; }

        /// <summary>
        /// The modification lists start as the document's structural modifications, fixed and
        /// variable, since the library is predicted for the document's peptides; the user may add
        /// or remove from there, and what the search used is added to the document at the hand-off.
        /// </summary>
        private void InitializeModifications()
        {
            foreach (var mod in Document.Settings.PeptideSettings.Modifications.StaticModifications)
            {
                if (mod.IsVariable)
                    listVariableMods.Items.Add(mod);
                else
                    listFixedMods.Items.Add(mod);
            }
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
            var existing = new HashSet<string>(FixedMods.Select(m => m.Name).Concat(VariableMods.Select(m => m.Name)));
            var newMod = Settings.Default.StaticModList.EditItem(this, null, Settings.Default.StaticModList, null);
            if (newMod != null && !existing.Contains(newMod.Name))
                listBox.Items.Add(newMod);
        }

        private static void RemoveSelectedMod(ListBox listBox)
        {
            if (listBox.SelectedIndex >= 0)
                listBox.Items.RemoveAt(listBox.SelectedIndex);
        }

        public bool UseExistingLibrary
        {
            get => cbUseExistingLibrary.Checked;
            set => cbUseExistingLibrary.Checked = value;
        }

        public string ExistingLibraryPath
        {
            get => tbxLibraryPath.Text;
            set => tbxLibraryPath.Text = value;
        }

        private void cbUseExistingLibrary_CheckedChanged(object sender, EventArgs e)
        {
            UpdateExistingLibraryControls();
        }

        private void UpdateExistingLibraryControls()
        {
            tbxLibraryPath.Enabled = btnBrowseLibrary.Enabled = UseExistingLibrary;
        }

        private void btnBrowseLibrary_Click(object sender, EventArgs e)
        {
            string newPath = EditLibraryDlg.GetLibraryPath(this, ExistingLibraryPath);
            if (newPath != null)
                tbxLibraryPath.Text = newPath;
        }

        public bool IsHighResolution
        {
            get => comboResolution.SelectedIndex == 0;
            set => comboResolution.SelectedIndex = value ? 0 : 1;
        }

        // Osprey's own defaults for each resolution: 10 ppm for high-resolution MS/MS, 0.5 m/z for
        // unit resolution.
        public const double DEFAULT_HRAM_FRAGMENT_TOLERANCE_PPM = 10;
        public const double DEFAULT_UNIT_FRAGMENT_TOLERANCE_MZ = 0.5;

        /// <summary>Choosing a resolution resets the fragment tolerance to Osprey's default for it.</summary>
        private void comboResolution_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (IsHighResolution)
            {
                FragmentTolerance = DEFAULT_HRAM_FRAGMENT_TOLERANCE_PPM;
                IsFragmentTolerancePpm = true;
            }
            else
            {
                FragmentTolerance = DEFAULT_UNIT_FRAGMENT_TOLERANCE_MZ;
                IsFragmentTolerancePpm = false;
            }
        }

        public double FragmentTolerance
        {
            get => (double)numFragmentTolerance.Value;
            set => numFragmentTolerance.Value = (decimal)value;
        }

        public bool IsFragmentTolerancePpm
        {
            get => comboFragmentUnit.SelectedIndex == 0;
            set => comboFragmentUnit.SelectedIndex = value ? 0 : 1;
        }

        public double RunFdr
        {
            get => double.TryParse(txtRunFdr.Text, out var v) ? v : 0.01;
            set => txtRunFdr.Text = value.ToString(@"G");
        }

        public double ExperimentFdr
        {
            get => double.TryParse(txtExperimentFdr.Text, out var v) ? v : 0.01;
            set => txtExperimentFdr.Text = value.ToString(@"G");
        }

        public double ProteinFdr
        {
            get => double.TryParse(txtProteinFdr.Text, out var v) ? v : 0.01;
            set => txtProteinFdr.Text = value.ToString(@"G");
        }

        public const int MAX_THREAD_COUNT = 128;

        public int Threads
        {
            get => (int)numThreads.Value;
            set => numThreads.Value = Math.Min(value, MAX_THREAD_COUNT);
        }

        /// <summary>The output Osprey writes: the document library, named after the document.</summary>
        public string OutputLibraryPath => OspreyHelpers.GetOutputLibraryPath(SkylineWindow.DocumentFilePath);

        /// <summary>
        /// The search as configured on the pages, for Osprey's command line. The digest and
        /// prediction settings the pages do not show come from the document: the peptide length and
        /// precursor charge filters and the variable modification limit, since the predicted library
        /// is for the document's peptides.
        /// </summary>
        public OspreyConfig GetSearchConfig()
        {
            var peptideSettings = Document.Settings.PeptideSettings;
            var charges = GetPrecursorCharges();
            return new OspreyConfig
            {
                DataFiles = DataFileResults.FoundResultsFiles.Select(f => f.Path).ToList(),
                FastaPath = ImportFastaControl.FastaFile,
                LibraryPath = UseExistingLibrary ? ExistingLibraryPath : null,
                OutputPath = OutputLibraryPath,
                WorkDirectory = OspreyHelpers.GetWorkDirectory(SkylineWindow.DocumentFilePath),
                TorchRuntimesDirectory = ToolDescriptionHelpers.GetToolsDirectory(),
                Resolution = IsHighResolution ? OspreyHelpers.RESOLUTION_HRAM : OspreyHelpers.RESOLUTION_UNIT,
                FragmentTolerance = FragmentTolerance,
                FragmentUnit = IsFragmentTolerancePpm ? OspreyHelpers.FRAGMENT_UNIT_PPM : OspreyHelpers.FRAGMENT_UNIT_MZ,
                RunFdr = RunFdr,
                ExperimentFdr = ExperimentFdr,
                ProteinFdr = ProteinFdr,
                Threads = Threads,
                EnzymeIndex = OspreyHelpers.GetCarafeEnzymeIndex(ImportFastaControl.Enzyme),
                MaxMissedCleavages = ImportFastaControl.MaxMissedCleavages,
                MinPeptideLength = peptideSettings.Filter.MinPeptideLength,
                MaxPeptideLength = peptideSettings.Filter.MaxPeptideLength,
                MinCharge = charges.Count > 0 ? charges.Min() : 2,
                MaxCharge = charges.Count > 0 ? charges.Max() : 3,
                MaxVariableMods = peptideSettings.Modifications.MaxVariableMods,
                FixedModificationIds = OspreyHelpers.GetCarafeModificationIds(FixedMods, out _),
                VariableModificationIds = OspreyHelpers.GetCarafeModificationIds(VariableMods, out _)
            };
        }

        /// <summary>
        /// The precursor charges the library is predicted for: the document's, except that a document
        /// still on Skyline's default transition filter (charge 2 alone) gets charges 2 and 3, the
        /// charges the Import Peptide Search wizard's DIA transition settings page will set for it
        /// after the search, so the library covers what the document will then look for.
        /// </summary>
        private IList<int> GetPrecursorCharges()
        {
            var filter = Document.Settings.TransitionSettings.Filter;
            if (Equals(filter, SrmSettingsList.GetDefault().TransitionSettings.Filter))
                return new[] { Adduct.DOUBLY_PROTONATED.AdductCharge, Adduct.TRIPLY_PROTONATED.AdductCharge };
            return filter.PeptidePrecursorCharges.Select(a => a.AdductCharge).ToList();
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

        public void Listen(EventHandler<DocumentChangedEventArgs> listener) =>
            ((IDocumentContainer)SkylineWindow).Listen(listener);
        public void Unlisten(EventHandler<DocumentChangedEventArgs> listener) =>
            ((IDocumentContainer)SkylineWindow).Unlisten(listener);
        public bool IsClosing => ((IDocumentContainer)SkylineWindow).IsClosing;
        public IEnumerable<BackgroundLoader> BackgroundLoaders =>
            ((IDocumentContainer)SkylineWindow).BackgroundLoaders;
        public void AddBackgroundLoader(BackgroundLoader loader) =>
            ((IDocumentContainer)SkylineWindow).AddBackgroundLoader(loader);
        public void RemoveBackgroundLoader(BackgroundLoader loader) =>
            ((IDocumentContainer)SkylineWindow).RemoveBackgroundLoader(loader);

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
                ImportOspreyLibrary();
                return;
            }

            if (!ValidateCurrentPage())
                return;

            if (wizardPages.SelectedIndex < wizardPages.TabPages.Count - 1)
                wizardPages.SelectTab(wizardPages.SelectedIndex + 1);

            switch (CurrentPage)
            {
                case Pages.search_settings_page:
                    btnNext.Text = PeptideSearchResources.EncyclopeDiaSearchDlg_NextPage_Run;
                    break;

                case Pages.run_page:
                    if (!StartSearch())
                    {
                        wizardPages.SelectTab(wizardPages.SelectedIndex - 1);
                        btnNext.Text = PeptideSearchResources.EncyclopeDiaSearchDlg_NextPage_Run;
                        break;
                    }
                    return;
            }

            btnBack.Enabled = wizardPages.SelectedIndex > 0;
            btnNext.Enabled = wizardPages.SelectedIndex < wizardPages.TabPages.Count - 1;
        }

        private bool ValidateCurrentPage()
        {
            switch (CurrentPage)
            {
                case Pages.data_files_page:
                    if (!DataFileResults.FoundResultsFiles.Any())
                    {
                        MessageDlg.Show(this, PeptideSearchResources.DiannSearchDlg_NextPage_Please_add_at_least_one_DIA_data_file_);
                        return false;
                    }
                    break;
                case Pages.fasta_page:
                    // The FASTA is needed either way: Osprey predicts the library from it, and the
                    // Import Peptide Search wizard matches the results to its proteins.
                    if (!File.Exists(ImportFastaControl.FastaFile))
                    {
                        MessageDlg.Show(this, PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_A_FASTA_file_is_required_for_an_Osprey_search_);
                        return false;
                    }
                    if (UseExistingLibrary)
                    {
                        if (!ValidateExistingLibraryPath())
                        {
                            tbxLibraryPath.Focus();
                            return false;
                        }
                    }
                    else if (!OspreyHelpers.GetCarafeEnzymeIndex(ImportFastaControl.Enzyme).HasValue)
                    {
                        MessageDlg.Show(this, string.Format(
                            PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_Osprey_cannot_predict_a_library_for_the_enzyme__0___Choose_an_enzyme_with_a_standard_cleavage_rule_,
                            ImportFastaControl.Enzyme?.Name));
                        return false;
                    }
                    break;
                case Pages.modifications_page:
                    if (!UseExistingLibrary)
                    {
                        OspreyHelpers.GetCarafeModificationIds(FixedMods.Concat(VariableMods), out var unsupported);
                        if (unsupported.Count > 0)
                        {
                            MessageDlg.Show(this, TextUtil.LineSeparate(
                                PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_Osprey_cannot_predict_a_library_with_these_modifications_,
                                TextUtil.LineSeparate(unsupported.Select(m => m.Name))));
                            return false;
                        }
                    }
                    break;
                case Pages.search_settings_page:
                    // A threshold of 0 is a search that reports nothing, so the range excludes it.
                    var helper = new MessageBoxHelper(this);
                    if (!helper.ValidateDecimalTextBox(txtRunFdr, 0, 1, out _, false) ||
                        !helper.ValidateDecimalTextBox(txtExperimentFdr, 0, 1, out _, false) ||
                        !helper.ValidateDecimalTextBox(txtProteinFdr, 0, 1, out _, false))
                    {
                        return false;
                    }
                    break;
            }
            return true;
        }

        /// <summary>
        /// An existing library must be a file in a format Osprey's <c>-l</c> reads (.blib or .tsv), and
        /// not the document library, which is the search's output: Run would delete it before Osprey read it.
        /// </summary>
        private bool ValidateExistingLibraryPath()
        {
            string path = ExistingLibraryPath;
            if (!EditLibraryDlg.ValidateLibraryPath(this, path))
                return false;
            if (!PathEx.HasExtension(path, BiblioSpecLiteSpec.EXT) && !PathEx.HasExtension(path, BiblioSpecLiteBuilder.EXT_TSV))
            {
                MessageDlg.Show(this, string.Format(
                    PeptideSearchResources.OspreySearchDlg_ValidateExistingLibraryPath_Osprey_searches__0__and__1__libraries__not__2__,
                    BiblioSpecLiteSpec.EXT, BiblioSpecLiteBuilder.EXT_TSV, path));
                return false;
            }
            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(OutputLibraryPath), StringComparison.OrdinalIgnoreCase))
            {
                MessageDlg.Show(this, string.Format(
                    PeptideSearchResources.OspreySearchDlg_ValidateExistingLibraryPath_The_existing_library_cannot_be_the_document_library__0___which_the_search_writes_,
                    path));
                return false;
            }
            return true;
        }

        /// <summary>
        /// Osprey writes the document library, so an existing one is replaced, with the user's
        /// consent, before the search starts: its streams closed and the cached library released,
        /// as the DIA-NN path does, so the file can be unlinked. Returns false when the user cancels.
        /// </summary>
        private bool StartSearch()
        {
            string docBlibPath = OutputLibraryPath;
            if (File.Exists(docBlibPath))
            {
                var choice = MultiButtonMsgDlg.Show(this,
                    string.Format(PeptideSearchResources.OspreySearchDlg_StartSearch_The_document_library__0__already_exists__Overwrite_it_with_the_results_of_this_search_, docBlibPath),
                    PeptideSearchResources.DiannSearchDlg_ImportDiannLibrary_Overwrite);
                if (choice != DialogResult.OK)
                    return false;
                ImportPeptideSearch.DeleteDocumentLibrary(SkylineWindow.Document, _libraryManager, docBlibPath);
            }

            btnNext.Text = Resources.BuildLibraryDlg_OkWizardPage_Finish;
            btnNext.Enabled = false;
            btnCancel.Enabled = false;
            btnBack.Enabled = false;
            ControlBox = false;
            SearchControl.SearchConfig = GetSearchConfig();
            SearchControl.RunSearch();
            return true;
        }

        /// <summary>
        /// Finish: the .blib Osprey wrote is the document library. The modifications the search
        /// used are added to the document first, so the Import FASTA page produces the peptides
        /// the library holds; then the Import Peptide Search wizard opens on its existing-library
        /// route with the library, the FASTA settings and the searched runs, and the DIA pages
        /// take it from there.
        /// </summary>
        private void ImportOspreyLibrary()
        {
            string blibPath = SearchControl.OutputLibraryPath;
            if (string.IsNullOrEmpty(blibPath) || !File.Exists(blibPath))
            {
                MessageDlg.Show(this, string.Format(
                    PeptideSearchResources.OspreySearchDlg_ImportOspreyLibrary_Osprey_did_not_write_the_spectral_library__0__,
                    blibPath ?? OutputLibraryPath));
                return;
            }

            // The modifications shaped the predicted library; a given library carries its own.
            if (!UseExistingLibrary)
                AddSearchModificationsToDocument();

            using var importPeptideSearchDlg = new ImportPeptideSearchDlg(SkylineWindow, _libraryManager,
                ImportPeptideSearchDlg.Workflow.dia, DataFileResults.FoundResultsFiles,
                ImportFastaControl.ImportSettings, new[] { blibPath });

            if (importPeptideSearchDlg.ShowDialog(this) == DialogResult.OK)
                DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// The Import Peptide Search wizard starts from the window's document, so the modifications
        /// are added to it before the wizard opens, as one undoable, audit-logged change: a cancelled
        /// import leaves them, and Undo takes them back.
        /// </summary>
        private void AddSearchModificationsToDocument()
        {
            var modifications = SkylineWindow.Document.Settings.PeptideSettings.Modifications;
            var staticMods = modifications.StaticModifications.ToList();
            var searchMods = FixedMods.Select(m => m.IsVariable ? m.ChangeVariable(false) : m)
                .Concat(VariableMods.Select(m => m.IsVariable ? m : m.ChangeVariable(true)));
            bool changed = false;
            foreach (var mod in searchMods)
            {
                if (staticMods.All(m => m.Name != mod.Name))
                {
                    staticMods.Add(mod);
                    changed = true;
                }
            }
            if (!changed)
                return;
            SkylineWindow.ModifyDocument(PeptideSearchResources.OspreySearchDlg_AddSearchModificationsToDocument_Added_the_modifications_of_an_Osprey_search,
                doc => doc.ChangeSettings(doc.Settings.ChangePeptideSettings(
                    doc.Settings.PeptideSettings.ChangeModifications(
                        doc.Settings.PeptideSettings.Modifications.ChangeStaticModifications(staticMods)))),
                AuditLogEntry.SettingsLogFunction);
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

        private void btnNext_Click(object sender, EventArgs e)
        {
            NextPage();
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            PreviousPage();
        }

        private static void AddPageControl<TControl>(TControl pageControl, TabPage tabPage, int border, int header, int footer)
            where TControl : UserControl
        {
            pageControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pageControl.Location = new Point(border, header);
            pageControl.Width = tabPage.Width - border * 2;
            pageControl.Height = tabPage.Height - header - border - footer;
            tabPage.Controls.Add(pageControl);
        }
    }

    /// <summary>
    /// The run page: Osprey's process under the wizard's progress monitor, its output the log.
    /// </summary>
    public class OspreySearchControl : SearchControl
    {
        public OspreySearchDlg HostDialog { get; }

        public OspreySearchControl(OspreySearchDlg hostControl)
        {
            Parent = HostDialog = hostControl;
        }

        public OspreyConfig SearchConfig { get; set; }
        public string OutputLibraryPath { get; private set; }

        private bool Search(CancellationTokenSource token, IProgressStatus status)
        {
            try
            {
                OutputLibraryPath = OspreyHelpers.RunSearch(SearchConfig, this, ref status);

                if (OutputLibraryPath == null)
                {
                    UpdateProgress(status.ChangeErrorException(
                        new IOException(PeptideSearchResources.OspreySearchControl_Search_Osprey_did_not_produce_a_spectral_library_)));
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

            ActionUtil.RunAsync(RunSearchAsync, @"Osprey Search thread");
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
