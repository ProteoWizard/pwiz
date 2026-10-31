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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    /// <summary>
    /// The Osprey Search wizard: the DIA runs, the protein FASTA Osprey predicts its library from
    /// (or an existing library in its place), the model the library is predicted with (a saved
    /// fine-tuned model, or one this search fine-tunes on chosen runs and saves for later searches),
    /// the modifications, the search settings, and the run. Osprey writes the document library
    /// directly, so Finish hands that .blib, the FASTA settings and the runs to the Import Peptide
    /// Search wizard's existing-library route, which adds the library to the document and imports
    /// the runs. The Library Model, Modifications and Search Settings pages are controls Build
    /// Library's fine-tune hosts too.
    /// </summary>
    public partial class OspreySearchDlg : FormEx, IModifyDocumentContainer, IMultipleViewProvider
    {
        public SkylineWindow SkylineWindow { get; }
        public OspreySearchControl SearchControl { get; }

        public enum Pages
        {
            data_files_page,
            fasta_page,
            library_model_page,
            modifications_page,
            search_settings_page,
            run_page
        }

        public class DataFilesPage : IFormView { }
        public class FastaPage : IFormView { }
        public class LibraryModelPage : IFormView { }
        public class ModificationsPage : IFormView { }
        public class SearchSettingsPage : IFormView { }
        public class RunPage : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new DataFilesPage(), new FastaPage(), new LibraryModelPage(), new ModificationsPage(), new SearchSettingsPage(), new RunPage()
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
            UpdateTitle();

            DataFileResults = new ImportResultsDIAControl(this);
            WizardPages.AddPageControl(DataFileResults, dataFilesPage, 2, 60, 0);

            ImportFastaControl = new ImportFastaControl(this, skylineWindow.SequenceTree, false);
            ImportFastaControl.IsDDASearch = true;
            // Leave the bottom of the FASTA page to the existing-library alternative.
            WizardPages.AddPageControl(ImportFastaControl, fastaPage, 2, 60,
                DpiUtil.ScaleToLogical(fastaPage, fastaPage.Height - cbUseExistingLibrary.Top) + 6);

            LibraryModelControl = new OspreyLibraryModelControl
            {
                SearchedFiles = () => SearchedFilePaths,
                DocumentFilePath = skylineWindow.DocumentFilePath
            };
            WizardPages.AddPageControl(LibraryModelControl, libraryModelPage, 10, 55, 4);

            ModificationsControl = new OspreyModificationsControl();
            WizardPages.AddPageControl(ModificationsControl, modificationsPage, 10, 55, 4);
            ModificationsControl.SetModifications(Document.Settings.PeptideSettings.Modifications.StaticModifications);

            SearchSettingsControl = new OspreySearchSettingsControl
            {
                // The grid shows the settings of the search the pages before it configured
                GetScopes = () => OspreyAdditionalSettings.ScopesFor(GetSearchConfig())
            };
            WizardPages.AddPageControl(SearchSettingsControl, searchSettingsPage, 20, 45, 4);

            SearchControl = new OspreySearchControl(this);
            WizardPages.AddPageControl(SearchControl, runSearchPage, 2, 0, 0);
            SearchControl.SearchFinished += SearchControlSearchFinished;

            UpdateExistingLibraryControls();
        }

        public ImportResultsDIAControl DataFileResults { get; }
        public ImportFastaControl ImportFastaControl { get; }
        public OspreyLibraryModelControl LibraryModelControl { get; }
        public OspreyModificationsControl ModificationsControl { get; }
        public OspreySearchSettingsControl SearchSettingsControl { get; }

        /// <summary>
        /// The modification lists start as the document's structural modifications, fixed and
        /// variable, since the library is predicted for the document's peptides; the user may add
        /// or remove from there, and what the search used is added to the document at the hand-off.
        /// </summary>
        public IEnumerable<StaticMod> FixedMods => ModificationsControl.FixedMods;
        public IEnumerable<StaticMod> VariableMods => ModificationsControl.VariableMods;

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

        #region Library Model page

        /// <summary>
        /// The model to predict the library with: the default, the pretrained AlphaPeptDeep models,
        /// or a saved fine-tuned model from the list; the default while a new model is fine-tuned,
        /// since the tuning starts from the pretrained models. Setting it selects that model in the combo.
        /// </summary>
        public CarafeModelSpec Model
        {
            get => LibraryModelControl.Model;
            set => LibraryModelControl.Model = value;
        }

        /// <summary>
        /// The combo's Add..., for tests: opens the model editor, which names a model file (the
        /// document's fine-tuned model, say) and adds it to the list, selected.
        /// </summary>
        public void AddModel()
        {
            LibraryModelControl.AddModel();
        }

        /// <summary>
        /// The libtorch runtimes Osprey predicts with, which decide whether Use GPU can be checked and
        /// name the folder Osprey is pointed at (<c>--torch-runtimes</c>); a test gives its own.
        /// </summary>
        public TorchRuntimes TorchRuntimes
        {
            get => LibraryModelControl.TorchRuntimes;
            set => LibraryModelControl.TorchRuntimes = value;
        }

        /// <summary>
        /// Whether Osprey predicts on the GPU, for the saved model and the tuning alike
        /// (<c>--device gpu</c>, else <c>cpu</c>).
        /// </summary>
        public bool UseGpu
        {
            get => LibraryModelControl.UseGpu;
            set => LibraryModelControl.UseGpu = value;
        }

        /// <summary>
        /// True predicts with the chosen model as it is; false fine-tunes a new model on the tuning
        /// runs and saves it beside the document (<see cref="DocumentModelPath"/>).
        /// </summary>
        public bool UseExistingModel
        {
            get => LibraryModelControl.UseExistingModel;
            set => LibraryModelControl.UseExistingModel = value;
        }

        /// <summary>A tuning search: the library is predicted from the FASTA with a model this search fine-tunes.</summary>
        public bool IsTuningSearch => !UseExistingLibrary && !UseExistingModel;

        /// <summary>Every run in the tuning list, checked or not, in list order.</summary>
        public IList<string> TuningFileItems => LibraryModelControl.TuningFileItems;

        /// <summary>The runs the model is fine-tuned on: the checked items, in list order.</summary>
        public IList<string> TuningFiles => LibraryModelControl.TuningFiles;

        public void SetTuningFileChecked(string path, bool isChecked)
        {
            LibraryModelControl.SetTuningFileChecked(path, isChecked);
        }

        private IList<string> SearchedFilePaths => DataFileResults.FoundResultsFiles.Select(f => f.Path).ToList();

        /// <summary>Browse... adds runs collected for the tuning.</summary>
        public void BrowseTuningFiles()
        {
            LibraryModelControl.BrowseTuningFiles();
        }

        /// <summary>What Browse... does with the chosen runs: they go to the top of the list, checked, in the order chosen.</summary>
        public void AddTuningFiles(IEnumerable<string> paths)
        {
            LibraryModelControl.AddTuningFiles(paths);
        }

        /// <summary>Checked, the searched files are offered below any browsed runs, unchecked, in search order.</summary>
        public bool ChooseFromSearchedFiles
        {
            get => LibraryModelControl.ChooseFromSearchedFiles;
            set => LibraryModelControl.ChooseFromSearchedFiles = value;
        }

        /// <summary>The model a tuning search writes: in the working folder, named after the output.</summary>
        public string TunedModelPath => OspreyHelpers.GetTunedModelPath(OutputLibraryPath, OspreyHelpers.GetWorkDirectory(SkylineWindow.DocumentFilePath));

        /// <summary>The document's fine-tuned model, the copy of that model beside the document.</summary>
        public string DocumentModelPath => OspreyHelpers.GetDocumentModelPath(SkylineWindow.DocumentFilePath);

        /// <summary>
        /// Next from the Library Model page: the page's own checks (a saved model must open; a
        /// tuning search needs at least one run, which must exist unless it is searched too, and no
        /// two runs with the same file name), and when the document already has a fine-tuned model,
        /// the user agrees to its being overwritten, or stays on the page.
        /// </summary>
        private bool ValidateLibraryModelPage()
        {
            if (!LibraryModelControl.ValidatePage())
                return false;
            if (UseExistingModel)
                return true;
            // The document's model is overwritten like its library, but asked about here, before the
            // run, since a model in the list may point at the file
            string documentModelPath = DocumentModelPath;
            if (File.Exists(documentModelPath))
            {
                var choice = MultiButtonMsgDlg.Show(this, string.Format(
                    PeptideSearchResources.OspreySearchDlg_ValidateLibraryModelPage_The_document_s_fine_tuned_model__0__already_exists__Continuing_will_overwrite_it_with_the_model_this_search_fine_tunes_,
                    documentModelPath), MessageBoxButtons.OKCancel);
                if (choice != DialogResult.OK)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// After a tuning search succeeds, the model Osprey saved in the working folder is copied
        /// beside the document as the document's fine-tuned model (<see cref="DocumentModelPath"/>),
        /// where the user finds it and can add it to the model list by name for other documents.
        /// </summary>
        private void CopyTunedModel()
        {
            string modelPath = TunedModelPath;
            if (!File.Exists(modelPath))
            {
                MessageDlg.Show(this, string.Format(PeptideSearchResources.OspreySearchDlg_CopyTunedModel_Osprey_did_not_write_the_fine_tuned_model__0__, modelPath));
                return;
            }
            string documentModelPath = DocumentModelPath;
            try
            {
                File.Copy(modelPath, documentModelPath, true);
            }
            catch (Exception x)
            {
                if (ExceptionUtil.IsProgrammingDefect(x))
                    throw;
                MessageDlg.ShowWithException(this, string.Format(PeptideSearchResources.OspreySearchDlg_CopyTunedModel_Failed_copying_the_fine_tuned_model_to__0__, documentModelPath), x);
            }
        }

        #endregion

        #region Search Settings page

        public bool IsHighResolution
        {
            get => SearchSettingsControl.IsHighResolution;
            set => SearchSettingsControl.IsHighResolution = value;
        }

        public double FragmentTolerance
        {
            get => SearchSettingsControl.FragmentTolerance;
            set => SearchSettingsControl.FragmentTolerance = value;
        }

        public bool IsFragmentTolerancePpm
        {
            get => SearchSettingsControl.IsFragmentTolerancePpm;
            set => SearchSettingsControl.IsFragmentTolerancePpm = value;
        }

        public double RunFdr
        {
            get => SearchSettingsControl.RunFdr;
            set => SearchSettingsControl.RunFdr = value;
        }

        public double ExperimentFdr
        {
            get => SearchSettingsControl.ExperimentFdr;
            set => SearchSettingsControl.ExperimentFdr = value;
        }

        public double ProteinFdr
        {
            get => SearchSettingsControl.ProteinFdr;
            set => SearchSettingsControl.ProteinFdr = value;
        }

        public int Threads
        {
            get => SearchSettingsControl.Threads;
            set => SearchSettingsControl.Threads = value;
        }

        #endregion

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
            var config = new OspreyConfig
            {
                DataFiles = DataFileResults.FoundResultsFiles.Select(f => f.Path).ToList(),
                FastaPath = ImportFastaControl.FastaFile,
                LibraryPath = UseExistingLibrary ? ExistingLibraryPath : null,
                DecoysInLibrary = UseExistingLibrary && OspreyHelpers.LibraryHasDecoyPairs(ExistingLibraryPath),
                ModelPath = !UseExistingLibrary && UseExistingModel && !Model.IsDefault ? Model.FilePath : null,
                TrainingInputs = IsTuningSearch ? TuningFiles : new List<string>(),
                OutputPath = OutputLibraryPath,
                WorkDirectory = OspreyHelpers.GetWorkDirectory(SkylineWindow.DocumentFilePath),
                TorchRuntimesDirectory = TorchRuntimes.RuntimesDirectory,
                Device = UseGpu ? OspreyHelpers.DEVICE_GPU : OspreyHelpers.DEVICE_CPU,
                EnzymeIndex = OspreyHelpers.GetCarafeEnzymeIndex(ImportFastaControl.Enzyme),
                MaxMissedCleavages = ImportFastaControl.MaxMissedCleavages,
                MinPeptideLength = peptideSettings.Filter.MinPeptideLength,
                MaxPeptideLength = peptideSettings.Filter.MaxPeptideLength,
                MinCharge = charges.Count > 0 ? charges.Min() : 2,
                MaxCharge = charges.Count > 0 ? charges.Max() : 3,
                MaxVariableMods = peptideSettings.Modifications.MaxVariableMods,
                CultureName = CultureInfo.CurrentUICulture.Name
            };
            SearchSettingsControl.ApplyTo(config);
            ModificationsControl.ApplyTo(config);
            return config;
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
                wizardPages.SelectTab(GetNextPageIndex(wizardPages.SelectedIndex));

            switch (CurrentPage)
            {
                case Pages.library_model_page:
                    // The data files page may have changed since the list was last filled
                    LibraryModelControl.SyncSearchedTuningFiles();
                    // Only now, so the NVIDIA driver is not loaded for a search of an existing library
                    LibraryModelControl.UpdateUseGpu();
                    break;

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

        /// <summary>
        /// The Library Model page belongs to a predicted library: an existing library is searched
        /// as it is, so the page is skipped in both directions.
        /// </summary>
        private int GetNextPageIndex(int index)
        {
            int next = index + 1;
            if ((Pages)next == Pages.library_model_page && UseExistingLibrary)
                next++;
            return next;
        }

        private int GetPreviousPageIndex(int index)
        {
            int previous = index - 1;
            if ((Pages)previous == Pages.library_model_page && UseExistingLibrary)
                previous--;
            return previous;
        }

        private bool ValidateCurrentPage()
        {
            switch (CurrentPage)
            {
                case Pages.library_model_page:
                    return ValidateLibraryModelPage();
                case Pages.data_files_page:
                    if (!DataFileResults.FoundResultsFiles.Any())
                    {
                        MessageDlg.Show(this, PeptideSearchResources.DiannSearchDlg_NextPage_Please_add_at_least_one_DIA_data_file_);
                        return false;
                    }
                    if (!OspreyLibraryModelControl.ValidateNoStemClash(this, SearchedFilePaths))
                        return false;
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
                    if (!UseExistingLibrary && !ModificationsControl.ValidatePage())
                        return false;
                    break;
                case Pages.search_settings_page:
                    if (!SearchSettingsControl.ValidatePage())
                        return false;
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
            LibraryModelControl.SaveUseGpuSetting();
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

            if (!ReloadDocumentLibrary())
                return;

            // The search's settings go in the import's audit log entry, as a DDA search engine's do.
            using var importPeptideSearchDlg = new ImportPeptideSearchDlg(SkylineWindow, _libraryManager,
                ImportPeptideSearchDlg.Workflow.dia, DataFileResults.FoundResultsFiles,
                ImportFastaControl.ImportSettings, new[] { blibPath });
            importPeptideSearchDlg.OspreySearchSettings = new OspreySearchSettings(SearchControl.SearchConfig);

            if (importPeptideSearchDlg.ShowDialog(this) == DialogResult.OK)
                DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// A document that already had the document library (a re-run) still holds the Library
        /// object loaded from the file Run deleted, which nothing reloads on its own: the library
        /// manager's cache was released at Run, but the document's object stays loaded. So the
        /// rewritten file is loaded here and put in the document in the old object's place, as the
        /// library manager does when it reloads a library: no settings change, nothing to log or
        /// undo. The Import Peptide Search wizard then finds the library in the document and
        /// leaves it. Returns false when the library could not be loaded, after reporting it.
        /// </summary>
        private bool ReloadDocumentLibrary()
        {
            var docLibSpec = SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs
                .FirstOrDefault(s => s != null && s.IsDocumentLibrary);
            if (docLibSpec == null)
                return true;

            Library libraryNew = null;
            using (var longWait = new LongWaitDlg())
            {
                longWait.Text = PeptideSearchResources.BuildPeptideSearchLibraryControl_LoadPeptideSearchLibrary_Loading_Library;
                var status = longWait.PerformWork(this, 800, monitor =>
                    libraryNew = _libraryManager.LoadLibrary(docLibSpec, () => new DefaultFileLoadMonitor(monitor)));
                if (status.IsError)
                {
                    MessageDlg.ShowException(this, status.ErrorException);
                    return false;
                }
            }
            if (libraryNew == null)
                return false;

            SkylineWindow.ModifyDocumentNoUndo(doc => doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(lib =>
            {
                int i = lib.LibrarySpecs.IndexOf(docLibSpec);
                if (i == -1 || ReferenceEquals(lib.Libraries[i], libraryNew))
                    return lib;
                var libraries = new List<Library>(lib.Libraries);
                libraries[i] = libraryNew;
                return lib.ChangeLibraries(libraries);
            })));
            return true;
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

            wizardPages.SelectTab(GetPreviousPageIndex(wizardPages.SelectedIndex));

            btnBack.Enabled = wizardPages.SelectedIndex > 0;
            btnNext.Enabled = wizardPages.SelectedIndex < wizardPages.TabPages.Count - 1;
        }

        private void SearchControlSearchFinished(bool success)
        {
            btnCancel.Enabled = true;
            btnBack.Enabled = true;
            ControlBox = true;
            if (success)
            {
                // The model is saved and the file on disk is the new library from here on, whether
                // or not the user goes on to Finish, so the model is copied beside the document and
                // the document's library object refreshed now rather than at the hand-off.
                if (IsTuningSearch)
                    CopyTunedModel();
                ReloadDocumentLibrary();
                btnNext.Enabled = true;
            }
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            NextPage();
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            PreviousPage();
        }

        private void wizardPages_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTitle();
        }

        /// <summary>
        /// The title above the pages is the current page's name, as the Import Peptide Search wizard
        /// names its pages, since the pages' own tabs are hidden.
        /// </summary>
        private void UpdateTitle()
        {
            lblTitle.Text = wizardPages.SelectedTab?.Text;
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
            // The bar names the task Osprey is on (OspreyProgressMonitor) while the run lasts, and
            // the Open Log button opens the console output teed to the working folder.
            SetProgressBarDisplayStyle(ProgressBarDisplayText.CustomText);
            SetLogFilePath(Path.Combine(SearchConfig.WorkDirectory, OspreyHelpers.STDOUT_LOG_NAME));

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

                Invoke(new MethodInvoker(() =>
                {
                    SetProgressBarDisplayStyle(ProgressBarDisplayText.Percentage);
                    UpdateSearchEngineProgressMilestone(status, success, status.SegmentCount,
                        Resources.DDASearchControl_SearchProgress_Search_canceled,
                        PeptideSearchResources.DDASearchControl_SearchProgress_Search_failed,
                        Resources.DDASearchControl_SearchProgress_Search_done);
                }));
            }

            Invoke(new MethodInvoker(() =>
            {
                UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.NoProgress, 0);
                btnCancel.Enabled = false;
                OnSearchFinished(success);
            }));
        }
    }

    /// <summary>
    /// The settings an Osprey search ran with, for the document's audit log. They are recorded
    /// inside the Import Peptide Search wizard's entry
    /// (<see cref="ImportPeptideSearchDlg.ImportPeptideSearchSettings"/>), as a DDA search
    /// engine's settings are, since that wizard's final import is the document change the search
    /// leads to. Each tracked property is named by its PropertyNames resource.
    /// </summary>
    public class OspreySearchSettings
    {
        public OspreySearchSettings(OspreyConfig config)
        {
            Resolution = config.Resolution;
            FragmentTolerance = config.FragmentTolerance;
            FragmentUnit = config.FragmentUnit;
            RunFdr = config.RunFdr;
            ExperimentFdr = config.ExperimentFdr;
            ProteinFdr = config.ProteinFdr;
            Threads = config.Threads;
            NonDefaultAdditionalSettings = GetNonDefaultAdditionalSettings(config);
        }

        [Track]
        public string Resolution { get; }
        [Track]
        public double FragmentTolerance { get; }
        [Track]
        public string FragmentUnit { get; }
        [Track]
        public double RunFdr { get; }
        [Track]
        public double ExperimentFdr { get; }
        [Track]
        public double ProteinFdr { get; }
        [Track]
        public int Threads { get; }

        /// <summary>
        /// The additional settings the run changed from their defaults, among those in the run's
        /// scopes (the only ones its command line carried); null when there are none.
        /// </summary>
        [Track(defaultValues: typeof(DefaultValuesNullOrEmpty))]
        public IEnumerable<AbstractDdaSearchEngine.Setting> NonDefaultAdditionalSettings { get; }

        private static IList<AbstractDdaSearchEngine.Setting> GetNonDefaultAdditionalSettings(OspreyConfig config)
        {
            var additionalSettings = config.AdditionalSettings;
            if (additionalSettings == null)
                return null;
            var scopes = OspreyAdditionalSettings.ScopesFor(config);
            var changedInScope = additionalSettings.ChangedSettings
                .Where(s => scopes.Contains(additionalSettings.GetProperty(s).Scope)).ToList();
            return changedInScope.Count > 0 ? changedInScope : null;
        }
    }
}
