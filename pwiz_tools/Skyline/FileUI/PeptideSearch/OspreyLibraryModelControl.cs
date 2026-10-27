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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    /// <summary>
    /// The Library Model page of the Osprey Search wizard and of Build Library's AlphaPeptDeep
    /// source: the model Osprey predicts with (the pretrained models or a saved fine-tuned model
    /// from the list), used as it is or as the start of a new fine-tune on the runs listed below,
    /// and whether Osprey predicts on the GPU. The wizard offers its searched files for the tuning
    /// list through <see cref="SearchedFiles"/>; Build Library, which searches nothing itself,
    /// hides that choice and the runs are browsed in.
    /// </summary>
    public partial class OspreyLibraryModelControl : UserControl
    {
        private readonly CarafeModelCombo _modelCombo;
        private UseGpuDriver _useGpuDriver;
        private bool _useGpuUpdated;
        private bool _modelChoiceHidden;

        public OspreyLibraryModelControl()
        {
            InitializeComponent();
            _modelCombo = new CarafeModelCombo(comboModel);
            UpdateTuningControls();
        }

        private Form WizardForm => FormEx.GetParentForm(this);

        /// <summary>The Use GPU checkbox's driver, made once the control has a form to ask its questions from.</summary>
        private UseGpuDriver UseGpuDriver => _useGpuDriver ??= new UseGpuDriver(this, cbUseGpu);

        /// <summary>
        /// The files the host searches, offered for the tuning through "Choose from the searched
        /// files"; null when the host searches nothing of its own (Build Library), which hides the
        /// choice and leaves the list to Browse....
        /// </summary>
        public Func<IList<string>> SearchedFiles { get; set; }

        /// <summary>The document the runs are browsed for, whose folder the Browse dialog opens in.</summary>
        public string DocumentFilePath { get; set; }

        /// <summary>Raised when the model chosen in the combo changes.</summary>
        public event EventHandler ModelChanged;

        /// <summary>Raised when the choice between the model as it is and a new fine-tune changes.</summary>
        public event EventHandler UseExistingModelChanged;

        /// <summary>
        /// Takes the searched-files choice off the page, for a host that searches nothing itself,
        /// and gives its room to the list.
        /// </summary>
        public void HideSearchedFilesChoice()
        {
            if (!cbChooseFromSearchedFiles.Visible)
                return;
            cbChooseFromSearchedFiles.Visible = false;
            listTuningFiles.Height = cbChooseFromSearchedFiles.Bottom - listTuningFiles.Top;
        }

        /// <summary>
        /// Takes the model choice off the page, for a host whose first page already made it
        /// (Build Library: the model, Use GPU and whether to fine-tune), leaving the runs the
        /// model is fine-tuned on, which take the room.
        /// </summary>
        public void HideModelChoice()
        {
            if (_modelChoiceHidden)
                return;
            _modelChoiceHidden = true;
            foreach (var control in new Control[] { lblModel, comboModel, cbUseGpu, radioUseExistingModel, radioTuneModel })
                control.Visible = false;
            int delta = listTuningFiles.Top - lblModel.Top;
            listTuningFiles.Top -= delta;
            listTuningFiles.Height += delta;
            btnBrowseTuningFiles.Top -= delta;
            int indent = listTuningFiles.Left - lblModel.Left;
            listTuningFiles.Left -= indent;
            listTuningFiles.Width += indent;
        }

        /// <summary>
        /// The model to predict the library with: the default, the pretrained AlphaPeptDeep models,
        /// or a saved fine-tuned model from the list; the default while a new model is fine-tuned,
        /// since the tuning starts from the pretrained models. Setting it selects that model in the combo.
        /// </summary>
        public CarafeModelSpec Model
        {
            get => _modelCombo.Model;
            set => _modelCombo.Model = value;
        }

        private void comboModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_modelCombo.SelectedIndexChanged(sender, e))
                ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Fills the model combo, for a host showing the page.</summary>
        public void LoadModels()
        {
            _modelCombo.Load();
        }

        /// <summary>
        /// The combo's Add..., for tests: opens the model editor, which names a model file (the
        /// document's fine-tuned model, say) and adds it to the list, selected.
        /// </summary>
        public void AddModel()
        {
            _modelCombo.AddItem();
        }

        /// <summary>The combo's Edit list..., for tests: opens the grid of models.</summary>
        public void EditModelList()
        {
            _modelCombo.EditList();
        }

        /// <summary>
        /// The libtorch runtimes Osprey predicts with, which decide whether Use GPU can be checked and
        /// name the folder Osprey is pointed at (<c>--torch-runtimes</c>); a test gives its own.
        /// </summary>
        public TorchRuntimes TorchRuntimes
        {
            get => UseGpuDriver.TorchRuntimes;
            set => UseGpuDriver.TorchRuntimes = value;
        }

        /// <summary>
        /// Whether Osprey predicts on the GPU, for the saved model and the tuning alike
        /// (<c>--device gpu</c>, else <c>cpu</c>). Checking it says why when the GPU cannot be used,
        /// and otherwise downloads the NVIDIA CUDA runtime first, if it is not installed.
        /// </summary>
        public bool UseGpu
        {
            get => UseGpuDriver.UseGpu;
            set => UseGpuDriver.UseGpu = value;
        }

        private void cbUseGpu_CheckedChanged(object sender, EventArgs e)
        {
            UseGpuDriver.CheckedChanged();
        }

        /// <summary>
        /// Checks Use GPU as the user last left it, if the GPU can be used and the CUDA runtime is
        /// installed. Called when the page is first shown, so the NVIDIA driver is not loaded for a
        /// host that never reaches it, and only once, so Back and Next keep the user's choice.
        /// </summary>
        public void UpdateUseGpu()
        {
            if (_useGpuUpdated)
                return;
            UseGpuDriver.Update();
            _useGpuUpdated = true;
        }

        /// <summary>Remembers the Use GPU choice for the next form.</summary>
        public void SaveUseGpuSetting()
        {
            UseGpuDriver.SaveSetting();
        }

        /// <summary>
        /// True predicts with the chosen model as it is; false fine-tunes a new model on the tuning runs.
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

        private void radioModel_CheckedChanged(object sender, EventArgs e)
        {
            // Both radios fire; once is enough
            if (!((RadioButton)sender).Checked)
                return;
            UpdateTuningControls();
            UseExistingModelChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Fine-tuning starts from the pretrained models, which the combo then shows, disabled
        /// (<see cref="CarafeModelCombo.SetTuning"/>), and enables the tuning list.
        /// </summary>
        private void UpdateTuningControls()
        {
            bool tune = !UseExistingModel;
            // A hidden combo is never filled: each saved model's name reads its file, and the host's own combo did that
            if (!_modelChoiceHidden)
                _modelCombo.SetTuning(tune);
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

        private IList<string> SearchedFilePaths => SearchedFiles?.Invoke() ?? new List<string>();

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

        public static bool PathsEqual(string path1, string path2)
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
            if (dlgOpen.ShowDialog(WizardForm) != DialogResult.OK)
                return;

            Settings.Default.OpenDataSourceState = dlgOpen.GetState(DocumentFilePath);
            var dataSources = dlgOpen.DataSources;
            if (dataSources == null || !dataSources.Any())
            {
                MessageDlg.Show(WizardForm, Resources.ImportResultsDlg_GetDataSourcePathsFile_No_results_files_chosen);
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
        /// Brings the searched entries of the tuning list up to date with the host's searched
        /// files: the searched files not yet listed are added, unchecked, after the runs already
        /// there, and searched entries no longer searched (or no longer wanted) are removed.
        /// </summary>
        public void SyncSearchedTuningFiles()
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

        /// <summary>
        /// Next from the page: a saved model must open; a tuning needs at least one run, which must
        /// exist unless it is searched too, and no two runs of the search or the tuning with the
        /// same file name (Osprey keys its spectra cache by stem and refuses the clash).
        /// </summary>
        public bool ValidatePage()
        {
            if (!_modelChoiceHidden)
            {
                var model = Model;
                if (!model.IsDefault && !EditCarafeModelDlg.ValidateModelPath(this, model.FilePath))
                {
                    comboModel.Focus();
                    return false;
                }
            }
            if (UseExistingModel)
                return true;

            var tuningFiles = TuningFiles;
            if (tuningFiles.Count == 0)
            {
                MessageDlg.Show(WizardForm, PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_Choose_at_least_one_run_to_fine_tune_the_model_on_);
                return false;
            }
            var searched = SearchedFilePaths;
            foreach (var run in tuningFiles.Where(r => !searched.Any(s => PathsEqual(s, r))))
            {
                if (!File.Exists(run) && !Directory.Exists(run))
                {
                    MessageDlg.Show(WizardForm, string.Format(PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_The_tuning_run__0__does_not_exist_, run));
                    return false;
                }
            }
            return ValidateNoStemClash(WizardForm, searched.Concat(tuningFiles));
        }

        /// <summary>
        /// Osprey keeps one spectra cache per file stem and refuses two runs that share one, so the
        /// clash is refused here, before the run, naming the two paths.
        /// </summary>
        public static bool ValidateNoStemClash(Control parent, IEnumerable<string> runs)
        {
            var runsByStem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var run in runs)
            {
                string stem = Path.GetFileNameWithoutExtension(run);
                if (!runsByStem.TryGetValue(stem, out var other))
                    runsByStem.Add(stem, run);
                else if (!PathsEqual(other, run))
                {
                    MessageDlg.Show(parent, string.Format(
                        PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_The_runs__0__and__1__have_the_same_file_name__Osprey_keeps_one_spectra_cache_per_name__so_rename_one_of_them_,
                        other, run));
                    return false;
                }
            }
            return true;
        }
    }
}
