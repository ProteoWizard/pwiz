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
    /// the runs.
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
        private readonly SettingsListComboDriver<CarafeModelSpec> _driverModel;
        private readonly UseGpuDriver _useGpuDriver;
        private bool _useGpuUpdated;

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
            // Set from a number, not resx text, so the decimal separator is the current culture's
            RunFdr = ExperimentFdr = ProteinFdr = DEFAULT_FDR;

            // The model list as Build Library offers it: the pretrained models, the saved ones, Add,
            // Edit current and Edit list
            _driverModel = new SettingsListComboDriver<CarafeModelSpec>(comboModel, Settings.Default.CarafeModelList);
            _driverModel.LoadList(null);
            // Osprey predicts in its own process, so the CPU or the GPU is open to choose every time
            _useGpuDriver = new UseGpuDriver(this, cbUseGpu, false);

            InitializeModifications();
            UpdateExistingLibraryControls();
            UpdateTuningControls();
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

        #region Library Model page

        /// <summary>
        /// The model to predict the library with: the default, the pretrained AlphaPeptDeep models,
        /// or a saved fine-tuned model from the list; the default while a new model is fine-tuned,
        /// since the tuning starts from the pretrained models. Setting it selects that model in the combo.
        /// </summary>
        public CarafeModelSpec Model
        {
            get => _driverModel?.SelectedItem ?? CarafeModelSpec.DEFAULT;
            // The driver selects by the combo's text, which is the display name, not the key
            set => _driverModel.LoadList(Settings.Default.CarafeModelList.GetDisplayName(value));
        }

        private void comboModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Add, Edit current and Edit list reload the list, which fires this event again from
            // within the driver's handling
            if (_driverModel == null || _driverModel.IsInSelectedIndexChangedEvent)
                return;
            _driverModel.SelectedIndexChangedEvent(sender, e);
        }

        /// <summary>
        /// The combo's Add..., for tests: opens the model editor, which names a model file (the
        /// document's fine-tuned model, say) and adds it to the list, selected.
        /// </summary>
        public void AddModel()
        {
            _driverModel.AddItem();
        }

        /// <summary>
        /// The libtorch runtimes Osprey predicts with, which decide whether Use GPU can be checked and
        /// name the folder Osprey is pointed at (<c>--torch-runtimes</c>); a test gives its own.
        /// </summary>
        public TorchRuntimes TorchRuntimes
        {
            get => _useGpuDriver.TorchRuntimes;
            set => _useGpuDriver.TorchRuntimes = value;
        }

        /// <summary>
        /// Whether Osprey predicts on the GPU, for the saved model and the tuning alike
        /// (<c>--device gpu</c>, else <c>cpu</c>). Checking it says why when the GPU cannot be used,
        /// and otherwise downloads the NVIDIA CUDA runtime first, if it is not installed.
        /// </summary>
        public bool UseGpu
        {
            get => _useGpuDriver.UseGpu;
            set => _useGpuDriver.UseGpu = value;
        }

        private void cbUseGpu_CheckedChanged(object sender, EventArgs e)
        {
            _useGpuDriver.CheckedChanged();
        }

        /// <summary>
        /// True predicts with the chosen model as it is; false fine-tunes a new model on the tuning
        /// runs and saves it beside the document (<see cref="DocumentModelPath"/>).
        /// </summary>
        public bool UseExistingModel
        {
            get => radioUseExistingModel.Checked;
            set
            {
                if (value)
                    radioUseExistingModel.Checked = true;
                else
                    radioTuneModel.Checked = true;
            }
        }

        /// <summary>A tuning search: the library is predicted from the FASTA with a model this search fine-tunes.</summary>
        public bool IsTuningSearch => !UseExistingLibrary && !UseExistingModel;

        private void radioModel_CheckedChanged(object sender, EventArgs e)
        {
            UpdateTuningControls();
        }

        private CarafeModelSpec _modelBeforeTuning;

        /// <summary>
        /// Fine-tuning starts from the pretrained models: the combo is set to them and disabled, so
        /// the page says what the tuning starts from, and the model chosen before comes back when
        /// "Use this model" is chosen again. (Osprey can tune a saved model further, but chaining
        /// fine-tunes compounds their drift, so it is not offered here.)
        /// </summary>
        private void UpdateTuningControls()
        {
            bool tune = !UseExistingModel;
            if (tune && comboModel.Enabled)
            {
                _modelBeforeTuning = Model;
                Model = CarafeModelSpec.DEFAULT;
                comboModel.Enabled = false;
            }
            else if (!tune && !comboModel.Enabled)
            {
                comboModel.Enabled = true;
                if (_modelBeforeTuning != null && Settings.Default.CarafeModelList.Contains(_modelBeforeTuning))
                    Model = _modelBeforeTuning;
            }
            listTuningFiles.Enabled = btnBrowseTuningFiles.Enabled = cbChooseFromSearchedFiles.Enabled = tune;
        }

        /// <summary>
        /// One run in the tuning list: browsed in by the user, or put there by the searched-files
        /// checkbox, which is what the checkbox takes out again. Shown by its name without the
        /// extension, as the data files page lists the searched runs.
        /// </summary>
        private class TuningRun
        {
            public TuningRun(string filePath, bool fromSearch)
            {
                FilePath = filePath;
                FromSearch = fromSearch;
            }

            public string FilePath { get; }
            public bool FromSearch { get; }

            public override string ToString()
            {
                return Path.GetFileNameWithoutExtension(FilePath);
            }
        }

        /// <summary>Every run in the tuning list, checked or not, in list order.</summary>
        public IList<string> TuningFileItems => listTuningFiles.Items.Cast<TuningRun>().Select(r => r.FilePath).ToList();

        /// <summary>The runs the model is fine-tuned on: the checked items, in list order.</summary>
        public IList<string> TuningFiles => listTuningFiles.CheckedItems.Cast<TuningRun>().Select(r => r.FilePath).ToList();

        public void SetTuningFileChecked(string path, bool isChecked)
        {
            int i = IndexOfTuningRun(path);
            if (i >= 0)
                listTuningFiles.SetItemChecked(i, isChecked);
        }

        private IList<string> SearchedFilePaths => DataFileResults.FoundResultsFiles.Select(f => f.Path).ToList();

        private TuningRun TuningRunAt(int index)
        {
            return (TuningRun)listTuningFiles.Items[index];
        }

        private int IndexOfTuningRun(string path)
        {
            for (int i = 0; i < listTuningFiles.Items.Count; i++)
            {
                if (PathsEqual(TuningRunAt(i).FilePath, path))
                    return i;
            }
            return -1;
        }

        private static bool PathsEqual(string path1, string path2)
        {
            return string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2), StringComparison.OrdinalIgnoreCase);
        }

        private void btnBrowseTuningFiles_Click(object sender, EventArgs e)
        {
            BrowseTuningFiles();
        }

        /// <summary>
        /// Browse... adds runs collected for the tuning, through the data source dialog the data
        /// files page uses, remembering its folder as that page does.
        /// </summary>
        public void BrowseTuningFiles()
        {
            using var dlgOpen = new OpenDataSourceDialog(Settings.Default.RemoteAccountList);
            dlgOpen.Text = PeptideSearchResources.OspreySearchDlg_BrowseTuningFiles_Browse_for_Runs_to_Fine_Tune_On;
            dlgOpen.RestoreState(DocumentFilePath, Settings.Default.OpenDataSourceState);
            if (dlgOpen.ShowDialog(this) != DialogResult.OK)
                return;

            Settings.Default.OpenDataSourceState = dlgOpen.GetState(DocumentFilePath);
            var dataSources = dlgOpen.DataSources;
            if (dataSources == null || !dataSources.Any())
            {
                MessageDlg.Show(this, Resources.ImportResultsDlg_GetDataSourcePathsFile_No_results_files_chosen);
                return;
            }
            AddTuningFiles(dataSources.Select(d => d.ToString()));
        }

        /// <summary>
        /// What Browse... does with the chosen runs: they go to the top of the list, checked, in
        /// the order chosen. One already listed as a searched file is listed once, at the top,
        /// checked, as a browsed one; one already browsed stays where it is.
        /// </summary>
        public void AddTuningFiles(IEnumerable<string> paths)
        {
            int insertAt = 0;
            foreach (var path in paths)
            {
                int existing = IndexOfTuningRun(path);
                if (existing >= 0)
                {
                    if (!TuningRunAt(existing).FromSearch)
                        continue;
                    listTuningFiles.Items.RemoveAt(existing);
                }
                listTuningFiles.Items.Insert(insertAt, new TuningRun(path, false));
                listTuningFiles.SetItemChecked(insertAt, true);
                insertAt++;
            }
        }

        /// <summary>
        /// Checked, the searched files are offered below any browsed runs, unchecked, in search
        /// order; unchecked, they and their checks are taken out again. Browsed runs are untouched
        /// either way, so a 446-file search with four tuning runs never scrolls the search list.
        /// </summary>
        public bool ChooseFromSearchedFiles
        {
            get => cbChooseFromSearchedFiles.Checked;
            set => cbChooseFromSearchedFiles.Checked = value;
        }

        private void cbChooseFromSearchedFiles_CheckedChanged(object sender, EventArgs e)
        {
            SyncSearchedTuningFiles();
        }

        /// <summary>
        /// Brings the searched entries of the tuning list up to date with the data files page: the
        /// searched files not yet listed are added, unchecked, after the runs already there, and
        /// searched entries no longer searched (or no longer wanted) are removed.
        /// </summary>
        private void SyncSearchedTuningFiles()
        {
            var searched = ChooseFromSearchedFiles ? SearchedFilePaths : new List<string>();
            for (int i = listTuningFiles.Items.Count - 1; i >= 0; i--)
            {
                var run = TuningRunAt(i);
                if (run.FromSearch && !searched.Any(p => PathsEqual(p, run.FilePath)))
                    listTuningFiles.Items.RemoveAt(i);
            }
            foreach (var path in searched)
            {
                if (IndexOfTuningRun(path) < 0)
                    listTuningFiles.Items.Add(new TuningRun(path, true));
            }
        }

        /// <summary>The model a tuning search writes: in the working folder, named after the output.</summary>
        public string TunedModelPath => OspreyHelpers.GetTunedModelPath(OutputLibraryPath, OspreyHelpers.GetWorkDirectory(SkylineWindow.DocumentFilePath));

        /// <summary>The document's fine-tuned model, the copy of that model beside the document.</summary>
        public string DocumentModelPath => OspreyHelpers.GetDocumentModelPath(SkylineWindow.DocumentFilePath);

        /// <summary>
        /// Next from the Library Model page: a saved model must open; a tuning search needs at
        /// least one run, which must exist unless it is searched too, and no two runs of the search
        /// or the tuning with the same file name (Osprey keys its spectra cache by stem and refuses
        /// the clash); and when the document already has a fine-tuned model, the user agrees to its
        /// being overwritten, or stays on the page.
        /// </summary>
        private bool ValidateLibraryModelPage()
        {
            var model = Model;
            if (!model.IsDefault && !EditCarafeModelDlg.ValidateModelPath(this, model.FilePath))
            {
                comboModel.Focus();
                return false;
            }
            if (UseExistingModel)
                return true;

            var tuningFiles = TuningFiles;
            if (tuningFiles.Count == 0)
            {
                MessageDlg.Show(this, PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_Choose_at_least_one_run_to_fine_tune_the_model_on_);
                return false;
            }
            var searched = SearchedFilePaths;
            foreach (var run in tuningFiles.Where(r => !searched.Any(s => PathsEqual(s, r))))
            {
                if (!File.Exists(run) && !Directory.Exists(run))
                {
                    MessageDlg.Show(this, string.Format(PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_The_tuning_run__0__does_not_exist_, run));
                    return false;
                }
            }
            if (!ValidateNoStemClash(searched.Concat(tuningFiles)))
                return false;
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
        /// Osprey keeps one spectra cache per file stem and refuses two runs that share one, so the
        /// clash is refused here, before the run, naming the two paths.
        /// </summary>
        private bool ValidateNoStemClash(IEnumerable<string> runs)
        {
            var runsByStem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var run in runs)
            {
                string stem = Path.GetFileNameWithoutExtension(run);
                if (!runsByStem.TryGetValue(stem, out var other))
                    runsByStem.Add(stem, run);
                else if (!PathsEqual(other, run))
                {
                    MessageDlg.Show(this, string.Format(
                        PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_The_runs__0__and__1__have_the_same_file_name__Osprey_keeps_one_spectra_cache_per_name__so_rename_one_of_them_,
                        other, run));
                    return false;
                }
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

        public const double DEFAULT_FDR = 0.01;

        public double RunFdr
        {
            get => double.TryParse(txtRunFdr.Text, out var v) ? v : DEFAULT_FDR;
            set => txtRunFdr.Text = value.ToString(CultureInfo.CurrentCulture);
        }

        public double ExperimentFdr
        {
            get => double.TryParse(txtExperimentFdr.Text, out var v) ? v : DEFAULT_FDR;
            set => txtExperimentFdr.Text = value.ToString(CultureInfo.CurrentCulture);
        }

        public double ProteinFdr
        {
            get => double.TryParse(txtProteinFdr.Text, out var v) ? v : DEFAULT_FDR;
            set => txtProteinFdr.Text = value.ToString(CultureInfo.CurrentCulture);
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
                DecoysInLibrary = UseExistingLibrary && OspreyHelpers.LibraryHasDecoyPairs(ExistingLibraryPath),
                ModelPath = !UseExistingLibrary && UseExistingModel && !Model.IsDefault ? Model.FilePath : null,
                TrainingInputs = IsTuningSearch ? TuningFiles : new List<string>(),
                OutputPath = OutputLibraryPath,
                WorkDirectory = OspreyHelpers.GetWorkDirectory(SkylineWindow.DocumentFilePath),
                TorchRuntimesDirectory = TorchRuntimes.RuntimesDirectory,
                Device = UseGpu ? OspreyHelpers.DEVICE_GPU : OspreyHelpers.DEVICE_CPU,
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
                VariableModificationIds = OspreyHelpers.GetCarafeModificationIds(VariableMods, out _),
                CultureName = CultureInfo.CurrentUICulture.Name
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
                wizardPages.SelectTab(GetNextPageIndex(wizardPages.SelectedIndex));

            switch (CurrentPage)
            {
                case Pages.library_model_page:
                    // The data files page may have changed since the list was last filled
                    SyncSearchedTuningFiles();
                    // Only now, so the NVIDIA driver is not loaded for a search of an existing library,
                    // and only once, so Back and Next keep the user's choice
                    if (!_useGpuUpdated)
                    {
                        _useGpuDriver.Update();
                        _useGpuUpdated = true;
                    }
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
                    if (!ValidateNoStemClash(SearchedFilePaths))
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
            _useGpuDriver.SaveSetting();
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

            using var importPeptideSearchDlg = new ImportPeptideSearchDlg(SkylineWindow, _libraryManager,
                ImportPeptideSearchDlg.Workflow.dia, DataFileResults.FoundResultsFiles,
                ImportFastaControl.ImportSettings, new[] { blibPath });

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
}
