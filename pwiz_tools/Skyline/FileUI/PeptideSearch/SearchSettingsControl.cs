/*
 * Original author: Viktoria Dorfer <viktoria.dorfer .at. fh-hagenberg.at>,
 *                  Bioinformatics Research Group, University of Applied Sciences Upper Austria
 *
 * Copyright 2020 University of Applied Sciences Upper Austria
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
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class SearchSettingsControl : UserControl
    {
        private ImportPeptideSearch ImportPeptideSearch { get; set; }
        private readonly RunPeptideSearchDlg _documentContainer;
        private readonly FullScanSettingsControl _hardklorInstrumentSettingsControl;
        private SearchEngine? _searchEngine;
        private readonly Dictionary<SearchEngine, string> _engineNames = new Dictionary<SearchEngine, string>();
        private List<SearchEngine> _engineChoices = new List<SearchEngine>();
        private (bool IsDia, bool IsGpf) _engineContext; // What the search engine was made for
        private SearchSettingsPreset _preset; // Applied to its engine whenever that engine is made for the spectra

        /// <summary>
        /// The engines that search DDA spectra, including DIA spectra DIA-Umpire has turned into pseudo-DDA spectra.
        /// </summary>
        private static readonly SearchEngine[] DDA_SEARCH_ENGINES =
        {
            SearchEngine.MSAmanda, SearchEngine.MSGFPlus, SearchEngine.MSFragger, SearchEngine.Comet, SearchEngine.Tide
        };

        public SearchSettingsControl(RunPeptideSearchDlg documentContainer, ImportPeptideSearch importPeptideSearch)
        {
            InitializeComponent();
            ImportPeptideSearch = importPeptideSearch;
            _documentContainer = documentContainer;

            // The designer's items name the engines in SearchEngine order
            for (int i = 0; i < searchEngineComboBox.Items.Count; i++)
                _engineNames[(SearchEngine) i] = (string) searchEngineComboBox.Items[i];
            _engineNames[SearchEngine.DIANN] = @"DIA-NN";

            if (ImportPeptideSearch.IsFeatureDetection)
            {
                MakeSelectedSearchEngine(); // Hardklor
                // Hide all controls other than the logo picture box
                foreach (Control control in Controls)
                {
                    control.Enabled = control.Visible = false;
                }
                pBLogo.Visible = true;
                pBLogo.SizeMode = PictureBoxSizeMode.AutoSize;
                HandleSearchEngineBlurb();

                // Add the Hardklor full scan settings control, used only when the user set FullScan analyzer to "Centroided"
                // Otherwise just displays the previously designated Full Scan settings
                _hardklorInstrumentSettingsControl = new FullScanSettingsControl(_documentContainer, ImportPeptideSearch.eFeatureDetectionPhase.hardklor_settings);
                _hardklorInstrumentSettingsControl.ModifyOptionsForImportPeptideSearchWizard(PeptideSearchWizardDlg.Workflow.feature_detection, false, ImportPeptideSearch.eFeatureDetectionPhase.hardklor_settings);
                Controls.Add(_hardklorInstrumentSettingsControl);
                _hardklorInstrumentSettingsControl.Location = new System.Drawing.Point(0, 0);
                // And adjust location of the other settings controls
                groupBoxHardklor.Enabled = groupBoxHardklor.Visible = true;
                groupBoxHardklor.Location = new System.Drawing.Point(_hardklorInstrumentSettingsControl.GroupBoxMS1Bounds.Left, _hardklorInstrumentSettingsControl.GroupBoxMS1Bounds.Bottom + 10);
                groupBoxHardklor.Width = _hardklorInstrumentSettingsControl.GroupBoxMS1Bounds.Width;
                toolTip1.SetToolTip(labelHardklorMinIdotP, toolTip1.GetToolTip(textHardklorMinIdotP));
                toolTip1.SetToolTip(lblHardklorSignalToNoise, toolTip1.GetToolTip(textHardklorSignalToNoise));
                toolTip1.SetToolTip(labelMinIntensityPPM, toolTip1.GetToolTip(textHardklorMinIntensityPPM));
            }
            else
            {
                searchEngineComboBox.SelectedIndexChanged += SearchEngineComboBox_SelectedIndexChanged;
                txtMS1Tolerance.LostFocus += txtMS1Tolerance_LostFocus;
                txtMS2Tolerance.LostFocus += txtMS2Tolerance_LostFocus;
                groupBoxHardklor.Enabled = groupBoxHardklor.Visible = false;
            }

            InitializeControls();
        }

        public void InitializeControls()
        {
            if (!ImportPeptideSearch.IsFeatureDetection)
                LoadEngineChoices();

            LoadMassUnitEntries();
        }

        /// <summary>
        /// Offers the engines that can search the spectra: for DIA spectra searched directly, the one
        /// engine the DIA search method page chose; otherwise the DDA engines. Makes the engine again
        /// when the spectra it searches have changed (e.g. DDA to DIA), since engines are made for them.
        /// </summary>
        private void LoadEngineChoices()
        {
            var choices = ImportPeptideSearch.IsDIASearch
                ? new List<SearchEngine> { _documentContainer.ConverterSettingsControl.DirectDiaSearchEngine }
                : DDA_SEARCH_ENGINES.ToList();
            var engine = _preset != null && choices.Contains(_preset.SearchEngine)
                ? _preset.SearchEngine
                : _searchEngine.HasValue && choices.Contains(_searchEngine.Value) ? _searchEngine.Value : choices[0];

            searchEngineComboBox.SelectedIndexChanged -= SearchEngineComboBox_SelectedIndexChanged;
            try
            {
                if (!choices.SequenceEqual(_engineChoices))
                {
                    _engineChoices = choices;
                    searchEngineComboBox.Items.Clear();
                    searchEngineComboBox.Items.AddRange(choices.Select(choice => (object) _engineNames[choice]).ToArray());
                    searchEngineComboBox.Enabled = choices.Count > 1;
                }
                searchEngineComboBox.SelectedIndex = choices.IndexOf(engine);
            }
            finally
            {
                searchEngineComboBox.SelectedIndexChanged += SearchEngineComboBox_SelectedIndexChanged;
            }

            if (_searchEngine != engine || _engineContext != CurrentEngineContext || ImportPeptideSearch.SearchEngine?.IsDisposed != false)
            {
                MakeSelectedSearchEngine();
                if (_preset?.SearchEngine == engine)
                    ApplyPresetSettings(_preset);
            }
        }

        private (bool IsDia, bool IsGpf) CurrentEngineContext => (ImportPeptideSearch.IsDIASearch, ImportPeptideSearch.IsGpfData);

        /// <summary>
        /// The engines the search engine list offers.
        /// </summary>
        public IList<SearchEngine> SearchEngineChoices => _engineChoices;

        private void SearchEngineComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (searchEngineComboBox.SelectedIndex < 0 ||
                _searchEngine == SelectedSearchEngine && ImportPeptideSearch.SearchEngine?.IsDisposed == false)
                return; // nothing to do

            // An engine the user chose over the preset's is not set back to the preset's
            if (_preset != null && _preset.SearchEngine != SelectedSearchEngine)
                _preset = null;
            MakeSelectedSearchEngine();
        }

        private void MakeSelectedSearchEngine()
        {
            _searchEngine = SelectedSearchEngine;
            _engineContext = CurrentEngineContext;
            ImportPeptideSearch.SearchEngine?.Dispose();
            ImportPeptideSearch.SearchEngine = InitSelectedSearchEngine();
            InitializeEngine();
        }

        private bool _initializingEngine;

        private void cbMs2Analyzer_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_initializingEngine && cbMs2Analyzer.SelectedIndex >= 0 && ImportPeptideSearch.SearchEngine != null)
                ImportPeptideSearch.SearchEngine.SetMs2Analyzer(cbMs2Analyzer.SelectedItem.ToString());
        }

        public void UpdateControls()
        {
            if (ImportPeptideSearch.IsFeatureDetection)
            {
                // If user has set FullScan.PrecursorMassAnalyzer to Centroided, remember that and allow instrument details setup
                // Otherwise, show the setup but don't allow changes
                var needHardklorInstrumentSettings =
                    _documentContainer.Document.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer ==
                    FullScanMassAnalyzerType.centroided;
                _hardklorInstrumentSettingsControl.Enabled = needHardklorInstrumentSettings;
                _hardklorInstrumentSettingsControl.SetGroupBoxMS1TitleForHardklorUse(needHardklorInstrumentSettings);
                if (!needHardklorInstrumentSettings)
                {
                    // Just show what the user selected in the full scan settings
                    _hardklorInstrumentSettingsControl.PrecursorMassAnalyzer = 
                        _documentContainer.Document.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer;
                    _hardklorInstrumentSettingsControl.PrecursorRes = 
                        _documentContainer.Document.Settings.TransitionSettings.FullScan.PrecursorRes;
                    _hardklorInstrumentSettingsControl.PrecursorResMz = 
                        _documentContainer.Document.Settings.TransitionSettings.FullScan.PrecursorResMz;
                }
            }
        }


        public SearchEngine SelectedSearchEngine
        {
            get
            {
                if (ImportPeptideSearch.IsFeatureDetection)
                    return SearchEngine.Hardklor;
                int index = searchEngineComboBox.SelectedIndex;
                return index >= 0 ? _engineChoices[index] : _searchEngine ?? DDA_SEARCH_ENGINES[0];
            }

            set
            {
                int index = _engineChoices.IndexOf(value);
                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
                searchEngineComboBox.SelectedIndex = index;
            }
        }

        private bool ShowDownloadMsFraggerDialog()
        {
            if (SimpleFileDownloader.FileAlreadyDownloaded(MsFraggerSearchEngine.MsFraggerDownloadInfo))
                return true;

            using (var downloadDlg = new MsFraggerDownloadDlg())
            {
                if (downloadDlg.ShowDialog(TopLevelControl) == DialogResult.Cancel)
                    return false;
            }

            return true;
        }

        private bool EnsureRequiredFilesDownloaded(IEnumerable<FileDownloadInfo> requiredFiles, Func<bool> extraDownloadAction = null)
        {
            var requiredFilesList = requiredFiles.ToList();

            var filesNotAlreadyDownloaded = SimpleFileDownloader.FilesNotAlreadyDownloaded(requiredFilesList).ToList();
            if (!filesNotAlreadyDownloaded.Any())
                return true;

            while(true)
            {
                var missingTools = SimpleFileDownloader.SearchToolsConfiguredButMissing(requiredFilesList).ToList();
                if (missingTools.Any())
                {
                    var allTools = Settings.Default.SearchToolList.Select(i => i).ToList();
                    foreach (var missingTool in missingTools)
                    {
                        var updatedItem = Settings.Default.SearchToolList.EditItem(this, missingTool, allTools, null);
                        if (updatedItem == null)
                            return false; // user canceled
                        Settings.Default.SearchToolList.Add(updatedItem);
                    }
                }
                else
                    break;
            }

            if (extraDownloadAction != null && !extraDownloadAction())
                return false;

            filesNotAlreadyDownloaded = SimpleFileDownloader.FilesNotAlreadyDownloaded(requiredFilesList).ToList();
            if (!filesNotAlreadyDownloaded.Any())
                return true;

            try
            {
                SimpleFileDownloaderDlg.Show(TopLevelControl,
                    string.Format(PeptideSearchResources.SearchSettingsControl_EnsureRequiredFilesDownloaded_Download__0_,
                        searchEngineComboBox.SelectedItem), filesNotAlreadyDownloaded);

                return !SimpleFileDownloader.FilesNotAlreadyDownloaded(filesNotAlreadyDownloaded).Any();
            }
            catch (Exception exception)
            {
                MessageDlg.ShowWithException(this, exception.Message, exception);
                return false;
            }
        }

        private bool EnsureRequiredFilesDownloaded()
        {
            switch (_searchEngine)
            {
                case SearchEngine.MSGFPlus:
                    return EnsureRequiredFilesDownloaded(MsgfPlusSearchEngine.FilesToDownload);
                case SearchEngine.MSFragger:
                    return EnsureRequiredFilesDownloaded(MsFraggerSearchEngine.FilesToDownload, ShowDownloadMsFraggerDialog);
                case SearchEngine.Comet:
                    return EnsureRequiredFilesDownloaded(CometSearchEngine.FilesToDownload);
                case SearchEngine.Tide:
                    return EnsureRequiredFilesDownloaded(TideSearchEngine.FilesToDownload);
                case SearchEngine.MSAmanda:
                    return EnsureRequiredFilesDownloaded(MSAmandaSearchWrapper.FilesToDownload);
                case SearchEngine.DIANN:
                    return DiannDownloadDlg.EnsureDiannInstalled(TopLevelControl, ShowSearchToolsDlg);
                case SearchEngine.Hardklor:
                    return true;
                default:
                    throw new NotImplementedException();
            }
        }

        public static FileDownloadInfo[] GetSearchEngineRequiredFiles(SearchEngine searchEngine)
        {
            switch (searchEngine)
            {
                case SearchEngine.Hardklor:
                    return Array.Empty<FileDownloadInfo>();
                case SearchEngine.MSAmanda:
                    return MSAmandaSearchWrapper.FilesToDownload;
                case SearchEngine.MSGFPlus:
                    return MsgfPlusSearchEngine.FilesToDownload;
                case SearchEngine.MSFragger:
                    return MsFraggerSearchEngine.FilesToDownload;
                case SearchEngine.Comet:
                    return CometSearchEngine.FilesToDownload;
                case SearchEngine.Tide:
                    return TideSearchEngine.FilesToDownload;
                case SearchEngine.DIANN:
                    return DiannHelpers.FilesToDownload;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static bool HasRequiredFilesDownloaded(SearchEngine searchEngine)
        {
            return !SimpleFileDownloader.FilesNotAlreadyDownloaded(GetSearchEngineRequiredFiles(searchEngine)).Any();
        }

        private AbstractDdaSearchEngine InitSelectedSearchEngine()
        {
            pBLogo.Image = null;
            switch (SelectedSearchEngine)
            {
                case SearchEngine.MSAmanda:
                    return new MSAmandaSearchWrapper();
                case SearchEngine.MSGFPlus:
                    return new MsgfPlusSearchEngine();
                case SearchEngine.MSFragger:
                    MsFraggerSearchEngine.DataType dataType;
                    if (!ImportPeptideSearch.IsDIASearch)
                        dataType = MsFraggerSearchEngine.DataType.dda;
                    else
                        dataType = ImportPeptideSearch.IsGpfData ? MsFraggerSearchEngine.DataType.dia_gpf : MsFraggerSearchEngine.DataType.dia;
                    return new MsFraggerSearchEngine(dataType);
                case SearchEngine.Comet:
                    return new CometSearchEngine(CutoffScore);
                case SearchEngine.Tide:
                    return new TideSearchEngine(CutoffScore);
                case SearchEngine.Hardklor:
                    return new HardklorSearchEngine(ImportPeptideSearch);
                case SearchEngine.DIANN:
                    return new DiannSearchEngine(Path.GetDirectoryName(_documentContainer.DocumentFilePath));
                default:
                    throw new NotImplementedException();
            }
        }

        public DdaSearchSettings SearchSettings
        {
            get { return new DdaSearchSettings(this); }
        }

        public class DdaSearchSettings
        {
            public DdaSearchSettings(SearchSettingsControl control) : this(control.SelectedSearchEngine, control.PrecursorTolerance,
                control.FragmentTolerance, control.MaxVariableMods, control.FragmentIons, control.Ms2Analyzer, control.CutoffLabel, control.CutoffScore)
            {
                if (control.cbFragmentIons.Items.Count == 1)
                    FragmentIons = null;
                if (control.cbMs2Analyzer.Items.Count == 1)
                    Ms2Analyzer = null;
            }

            public static DdaSearchSettings GetDefault()
            {
                return new DdaSearchSettings();
            }

            public DdaSearchSettings()
            {
            }

            public DdaSearchSettings(SearchEngine searchEngine, MzTolerance precursorTolerance, MzTolerance fragmentTolerance, int maxVariableMods, string fragmentIons, string ms2Analyzer, string scoreType, double scoreThreshold)
            {
                SearchEngine = searchEngine;
                PrecursorTolerance = precursorTolerance;
                FragmentTolerance = fragmentTolerance;
                MaxVariableMods = maxVariableMods;
                FragmentIons = fragmentIons;
                Ms2Analyzer = ms2Analyzer;
                ScoreType = scoreType;
                ScoreThreshold = scoreThreshold;
            }

            private class SearchEngineDefault : DefaultValues
            {
                public override bool IsDefault(object obj, object parentObject)
                {
                    return ((DdaSearchSettings)parentObject).SearchEngine == SearchEngine.MSAmanda;
                }
            }

            [Track(defaultValues:typeof(SearchEngineDefault))]
            public SearchEngine SearchEngine { get; private set; }
            [Track]
            public MzTolerance PrecursorTolerance { get; private set; }
            [Track]
            public MzTolerance FragmentTolerance { get; private set; }
            [Track]
            public int MaxVariableMods { get; private set; }
            [Track(defaultValues:typeof(DefaultValuesNull))]
            public string FragmentIons { get; private set; }
            [Track(defaultValues:typeof(DefaultValuesNull))]
            public string Ms2Analyzer { get; private set; }
            [Track]
            public string ScoreType { get; private set; }
            [Track]
            public double ScoreThreshold { get; private set; }
        }
        
        private void txtMS1Tolerance_LostFocus(object sender, EventArgs e)
        {
            if (cbMS1TolUnit.SelectedItem != null)
                return;

            if (double.TryParse(txtMS1Tolerance.Text, out double tmp))
                cbMS1TolUnit.SelectedIndex = tmp <= 3 ? 0 : 1;
        }

        private void txtMS2Tolerance_LostFocus(object sender, EventArgs e)
        {
            if (cbMS2TolUnit.SelectedItem != null)
                return;

            if (double.TryParse(txtMS2Tolerance.Text, out double tmp))
                cbMS2TolUnit.SelectedIndex = tmp <= 3 ? 0 : 1;
        }

        public void InitializeEngine()
        {
            //lblSearchEngineName.Text = ImportPeptideSearch.SearchEngine.EngineName;
            _initializingEngine = true;
            try
            {
                LoadComboboxEntries();
            }
            finally
            {
                _initializingEngine = false;
            }
            pBLogo.Image = ImportPeptideSearch.SearchEngine.SearchEngineLogo;
            labelCutoff.Text = ImportPeptideSearch.SearchEngine.CutoffScoreLabel + @":";
            HandleSearchEngineBlurb();
            btnAdditionalSettings.Enabled = ImportPeptideSearch.SearchEngine.AdditionalSettings != null;
            btnImportSettings.Visible = SearchSettingsParamsFileParser.SupportsImport(SelectedSearchEngine);

            // Comet controls fragment tolerance via MS2 Analyzer (High/Low resolution)
            bool cometFragmentTol = SelectedSearchEngine == SearchEngine.Comet;
            lblMs2Tolerance.Enabled = !cometFragmentTol;
            txtMS2Tolerance.Enabled = !cometFragmentTol;
            label3.Enabled = !cometFragmentTol;
            cbMS2TolUnit.Enabled = !cometFragmentTol;
            if (cometFragmentTol)
                txtMS2Tolerance.Text = string.Empty;

            ImportPeptideSearch.RemainingStepsInSearch = ImportPeptideSearch.IsFeatureDetection ? 2 : 1; // Hardklor is followed by one or more BullseyeSharp calls
        }

        // Arrange and populate the search engine blurb, if any, below the search engine logo
        private void HandleSearchEngineBlurb()
        {
            lblSearchEngineBlurb.Left = pBLogo.Left;
            lblSearchEngineBlurb.Width = pBLogo.Width;
            lblSearchEngineBlurb.Top = pBLogo.Bottom + pBLogo.Margin.Top;
            lblSearchEngineBlurb.Text = ImportPeptideSearch.SearchEngine.SearchEngineBlurb ?? string.Empty;
            lblSearchEngineBlurb.Enabled = lblSearchEngineBlurb.Visible = !string.IsNullOrEmpty(ImportPeptideSearch.SearchEngine.SearchEngineBlurb);
        }

        private void LoadComboboxEntries()
        {
            if (ImportPeptideSearch.IsFeatureDetection)
            {
                return;
            }
            LoadMassUnitEntries();
            LoadFragmentIonEntries();
            LoadMs2AnalyzerEntries();

            var modSettings = _documentContainer.Document.Settings.PeptideSettings.Modifications;
            cbMaxVariableMods.SelectedItem = modSettings.MaxVariableMods.ToString(LocalizationHelper.CurrentCulture);
            if (cbMaxVariableMods.SelectedIndex < 0)
                cbMaxVariableMods.SelectedIndex = 2; // default max = 2

            CutoffScore =
                BiblioSpecLiteBuilder.GetDefaultScoreThreshold(ImportPeptideSearch.SearchEngine.CutoffScoreName) ??
                ImportPeptideSearch.SearchEngine.DefaultCutoffScore;
            ImportPeptideSearch.SearchEngine.SetCutoffScore(CutoffScore);

            // DIA-NN chooses its mass accuracies itself when they are 0
            if (SelectedSearchEngine == SearchEngine.DIANN)
            {
                if (string.IsNullOrEmpty(txtMS1Tolerance.Text))
                    txtMS1Tolerance.Text = 0.ToString(LocalizationHelper.CurrentCulture);
                if (string.IsNullOrEmpty(txtMS2Tolerance.Text))
                    txtMS2Tolerance.Text = 0.ToString(LocalizationHelper.CurrentCulture);
            }
        }

        private void LoadMassUnitEntries()
        {
            void ClearAndRestoreComboBoxItems(ComboBox cb, string[] newEntries)
            {
                string oldSelection = cb.SelectedItem as string;
                cb.Items.Clear();
                cb.Items.AddRange(newEntries);
                if (oldSelection != null && newEntries.Contains(oldSelection))
                    cb.SelectedIndex = newEntries.ToList().IndexOf(oldSelection);
                else if (newEntries.Length == 1)
                    cb.SelectedIndex = 0;
            }

            var ms1Entries = ImportPeptideSearch.SearchEngine.PrecursorIonToleranceUnitTypes.Select(t => t.Name).ToArray();
            ClearAndRestoreComboBoxItems(cbMS1TolUnit, ms1Entries);

            var ms2Entries = ImportPeptideSearch.SearchEngine.FragmentIonToleranceUnitTypes.Select(t => t.Name).ToArray();
            ClearAndRestoreComboBoxItems(cbMS2TolUnit, ms2Entries);
        }

        private void LoadFragmentIonEntries()
        {
            cbFragmentIons.Items.Clear();
            cbFragmentIons.Items.AddRange(ImportPeptideSearch.SearchEngine.FragmentIons);
            ComboHelper.AutoSizeDropDown(cbFragmentIons);
            cbFragmentIons.SelectedIndex = 0;
        }

        private void LoadMs2AnalyzerEntries()
        {
            cbMs2Analyzer.Items.Clear();
            cbMs2Analyzer.Items.AddRange(ImportPeptideSearch.SearchEngine.Ms2Analyzers);
            ComboHelper.AutoSizeDropDown(cbMs2Analyzer);
            cbMs2Analyzer.SelectedIndex = 0;
        }

        private Form WizardForm
        {
            get { return FormEx.GetParentForm(this); }
        }
    
        private bool ValidateCombobox(ComboBox comboBox, out string selectedElement)
        {
            selectedElement = "";
            if (comboBox.SelectedItem == null)
                return false;
            selectedElement = comboBox.SelectedItem.ToString();
            return true;
        }

        public bool ValidateCutoffScore()
        {
            var helper = new MessageBoxHelper(this.ParentForm);
            if (helper.ValidateDecimalTextBox(textCutoff, out var cutoffScore))
            {
                ImportPeptideSearch.SearchEngine.SetCutoffScore(cutoffScore);
                CutoffScore = cutoffScore;
                return true;
            }
            return false;
        }

        public bool SaveAllSettings(bool interactive)
        {
            bool valid = ValidateEntries(interactive);
            if (!valid)
                return false;

            if (!EnsureRequiredFilesDownloaded())
                return false;

            var modSettings = _documentContainer.Document.Settings.PeptideSettings.Modifications;
            var allMods = ImportPeptideSearch.SearchEngine.SearchesIsotopeLabels
                ? modSettings.StaticModifications.Union(modSettings.AllHeavyModifications)
                : modSettings.StaticModifications;
            ImportPeptideSearch.SearchEngine.SetModifications(allMods, Convert.ToInt32(cbMaxVariableMods.SelectedItem));

            if (ImportPeptideSearch.IsFeatureDetection)
            {
                if (_hardklorInstrumentSettingsControl.Enabled)
                {
                    // If the fullscan section was enabled, that's because user chose "Centroided" in actual FullScan settings, so set that again
                    _documentContainer.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.centroided;
                }
                BiblioSpecLiteBuilder.SetDefaultScoreThreshold(ImportPeptideSearch.SearchEngine.CutoffScoreName, CutoffScore);
                Settings.Default.FeatureFindingMinIntensityPPM = HardklorMinIntensityPPM;
                Settings.Default.FeatureFindingMinIdotP = HardklorMinIdotP;
                Settings.Default.FeatureFindingSignalToNoise = HardklorSignalToNoise;

            }
            return true;
        }

        private bool ValidateEntries(bool interactive)
        {
            var helper = new MessageBoxHelper(this.ParentForm, interactive);
            if (ImportPeptideSearch.IsFeatureDetection)
            {
                if (!helper.ValidateDecimalTextBox(this.textHardklorMinIdotP, 0, 1, out var minIdotP))
                {
                    return false;
                }

                if (!helper.ValidateDecimalTextBox(this.textHardklorSignalToNoise, 0, 10, out var signalToNoise))
                {
                    return false;
                }

                if (!helper.ValidateDecimalTextBox(this.textHardklorMinIntensityPPM, 0, 100, out _))
                {
                    return false;
                }
                // Note the Hardklor settings
                ImportPeptideSearch.SettingsHardklor = new ImportPeptideSearch.HardklorSettings(HardklorInstrument,
                    HardklorResolution,
                    minIdotP, signalToNoise, _documentContainer.TransitionSettings.Filter.PeptidePrecursorCharges.Select(a => a.AdductCharge).Distinct().ToArray(),
                    HardklorMinIntensityPPM,
                    _documentContainer.FullScanSettingsControl.FullScan.RetentionTimeFilterLength);
                CutoffScore = minIdotP;
                return true;
            }

            if (!helper.ValidateDecimalTextBox(txtMS1Tolerance, 0, 100, out _))
            {
                helper.ShowTextBoxError(txtMS1Tolerance, 
                    PeptideSearchResources.DdaSearch_SearchSettingsControl_MS1_Tolerance_incorrect);
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetPrecursorMassTolerance(PrecursorTolerance);

            // Comet controls fragment tolerance via MS2 Analyzer selection, not text box
            if (txtMS2Tolerance.Enabled)
            {
                if (!helper.ValidateDecimalTextBox(txtMS2Tolerance, 0, 100, out _))
                {
                    helper.ShowTextBoxError(txtMS2Tolerance,
                        PeptideSearchResources.DdaSearch_SearchSettingsControl_MS2_Tolerance_incorrect);
                    return false;
                }
                ImportPeptideSearch.SearchEngine.SetFragmentIonMassTolerance(FragmentTolerance);
            }

            string fragmentIons;
            if (!ValidateCombobox(cbFragmentIons, out fragmentIons))
            {
                helper.ShowTextBoxError(cbFragmentIons, 
                    PeptideSearchResources.DdaSearch_SearchSettingsControl_Fragment_ions_must_be_selected);
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetFragmentIons(fragmentIons);

            string ms2Analyzer;
            if (!ValidateCombobox(cbMs2Analyzer, out ms2Analyzer))
            {
                helper.ShowTextBoxError(cbMs2Analyzer,
                    PeptideSearchResources.DdaSearch_SearchSettingsControl_MS2_analyzer_must_be_selected);
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetMs2Analyzer(ms2Analyzer);

            if (!ValidateCutoffScore())
                return false;

            return true;
        }

        public bool HardklorInstrumentSettingsAreEditable => _hardklorInstrumentSettingsControl.Enabled;

        public FullScanMassAnalyzerType HardklorInstrument
        {
            get { return _hardklorInstrumentSettingsControl.PrecursorMassAnalyzer; }
            set { _hardklorInstrumentSettingsControl.PrecursorMassAnalyzer = value; }
        }

        public double HardklorResolution
        {
            get
            {
                return _hardklorInstrumentSettingsControl.PrecursorRes ?? 0;
            }
            set
            {
                _hardklorInstrumentSettingsControl.PrecursorRes = value;
            }
        }

        public double HardklorMinIdotP
        {
            get { return double.TryParse(textHardklorMinIdotP.Text, out var corr) ? corr : 0; }
            set { textHardklorMinIdotP.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public double HardklorMinIntensityPPM
        {
            get { return double.TryParse(textHardklorMinIntensityPPM.Text, out var cutoff) ? cutoff : 0; }
            set { textHardklorMinIntensityPPM.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public double HardklorSignalToNoise
        {
            get { return double.TryParse(textHardklorSignalToNoise.Text, out var sn) ? sn : 0; }
            set { textHardklorSignalToNoise.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public MzTolerance PrecursorTolerance
        {
            get
            {
                var unitTypes = ImportPeptideSearch.SearchEngine.PrecursorIonToleranceUnitTypes;
                var unit = cbMS1TolUnit.SelectedIndex >= 0 && cbMS1TolUnit.SelectedIndex < unitTypes.Length
                    ? unitTypes[cbMS1TolUnit.SelectedIndex].Unit
                    : MzTolerance.Units.ppm;
                return new MzTolerance(double.Parse(txtMS1Tolerance.Text), unit);
            }

            set
            {
                txtMS1Tolerance.Text = value.Value.ToString(LocalizationHelper.CurrentCulture);
                var unitTypes = ImportPeptideSearch.SearchEngine.PrecursorIonToleranceUnitTypes;
                int unitIndex = Array.FindIndex(unitTypes, u => u.Unit == value.Unit);
                cbMS1TolUnit.SelectedIndex = Math.Max(0, unitIndex);
            }
        }

        public MzTolerance FragmentTolerance
        {
            get
            {
                var unitTypes = ImportPeptideSearch.SearchEngine.FragmentIonToleranceUnitTypes;
                var unit = cbMS2TolUnit.SelectedIndex >= 0 && cbMS2TolUnit.SelectedIndex < unitTypes.Length
                    ? unitTypes[cbMS2TolUnit.SelectedIndex].Unit
                    : MzTolerance.Units.mz;
                if (!double.TryParse(txtMS2Tolerance.Text, out double value))
                    value = 0;
                return new MzTolerance(value, unit);
            }

            set
            {
                txtMS2Tolerance.Text = value.Value.ToString(LocalizationHelper.CurrentCulture);
                var unitTypes = ImportPeptideSearch.SearchEngine.FragmentIonToleranceUnitTypes;
                int unitIndex = Array.FindIndex(unitTypes, u => u.Unit == value.Unit);
                cbMS2TolUnit.SelectedIndex = Math.Max(0, unitIndex);
            }
        }

        public int MaxVariableMods
        {
            get { return Convert.ToInt32(cbMaxVariableMods.SelectedItem); }
            set { cbMaxVariableMods.SelectedIndex = cbMaxVariableMods.Items.IndexOf(value.ToString()); }
        }

        public string CutoffLabel => ImportPeptideSearch.SearchEngine.CutoffScoreLabel;
        public string CutoffScoreName => ImportPeptideSearch.SearchEngine.CutoffScoreName;

        public double CutoffScore
        {
            get { return Convert.ToDouble(textCutoff.Text, CultureInfo.CurrentCulture); }
            set { textCutoff.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public string FragmentIons
        {
            get { return cbFragmentIons.SelectedItem.ToString(); }

            set
            {
                int i = cbFragmentIons.Items.IndexOf(value);
                Assume.IsTrue(i >= 0, $@"fragmentIons value ""{value}"" not found in ComboBox items");
                cbFragmentIons.SelectedIndex = i;
                ImportPeptideSearch.SearchEngine.SetFragmentIons(value);
            }
        }

        public string Ms2Analyzer
        {
            get { return cbMs2Analyzer.SelectedItem.ToString(); }

            set
            {
                int i = cbMs2Analyzer.Items.IndexOf(value);
                Assume.IsTrue(i >= 0, $@"MS2 analyzer value ""{value}"" not found in ComboBox items");
                cbMs2Analyzer.SelectedIndex = i;
                ImportPeptideSearch.SearchEngine.SetMs2Analyzer(value);
            }
        }

        public IDictionary<string, AbstractDdaSearchEngine.Setting> AdditionalSettings
        {
            get => ImportPeptideSearch.SearchEngine.AdditionalSettings;
            set => ImportPeptideSearch.SearchEngine.AdditionalSettings = value;
        }

        public void SetAdditionalSetting(string name, string value)
        {
            Assume.IsNotNull(ImportPeptideSearch.SearchEngine.AdditionalSettings);
            Assume.IsTrue(ImportPeptideSearch.SearchEngine.AdditionalSettings.ContainsKey(name));

            ImportPeptideSearch.SearchEngine.AdditionalSettings[name].Value = value;
        }

        private void btnAdditionalSettings_Click(object sender, EventArgs e)
        {
            Assume.IsNotNull(ImportPeptideSearch.SearchEngine.AdditionalSettings);

            KeyValueGridDlg.Show(this, PeptideSearchResources.SearchSettingsControl_Additional_Settings,
                ImportPeptideSearch.SearchEngine.AdditionalSettings,
                (setting) => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value),
                setting => setting.ValidValues);

            InitializeControls();
        }

        private void btnImportSettings_Click(object sender, EventArgs e)
        {
            using (var openDlg = new OpenFileDialog())
            {
                // Comet and MSFragger .params files, and DIA-NN's .cfg argument lists
                openDlg.Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(
                    PeptideSearchResources.ImportPeptideSearchDlg_ImportPresetFromFile_Params_files,
                    @".params", @".cfg"));
                openDlg.Title = PeptideSearchResources.SearchSettingsControl_ImportFromParamsFile;
                if (openDlg.ShowDialog(this) != DialogResult.OK)
                    return;

                string filePath = openDlg.FileName;
                string defaultName = Path.GetFileNameWithoutExtension(filePath);

                try
                {
                    var preset = SearchSettingsParamsFileParser.ImportFromFile(filePath, defaultName);

                    // Check for existing preset with same name
                    var existingPreset = Settings.Default.SearchSettingsPresets.FirstOrDefault(w => w.Name == defaultName);
                    if (existingPreset != null)
                    {
                        var result = MessageDlg.Show(this,
                            string.Format(PeptideSearchResources.SearchSettingsControl_OverwriteSettingsPreset_A_settings_preset_named__0__already_exists__Do_you_want_to_replace_it_, defaultName),
                            false, MessageBoxButtons.YesNo);
                        if (result != DialogResult.Yes)
                            return;
                        Settings.Default.SearchSettingsPresets.Remove(existingPreset);
                    }

                    _documentContainer.AddImportedPreset(preset);
                }
                catch (Exception ex)
                {
                    MessageDlg.ShowWithException(this,
                        PeptideSearchResources.ImportPeptideSearchDlg_ImportPresetFromFile_Failed_to_import_settings_from_params_file,
                        ex);
                }
            }
        }

        private void btnEditSearchTools_Click(object sender, EventArgs e)
        {
            ShowSearchToolsDlg();
        }

        public void ShowSearchToolsDlg()
        {
            using var listbox = new ListBox();
            var driverTools = new SettingsListBoxDriver<SearchTool>(listbox, Settings.Default.SearchToolList);
            driverTools.LoadList();
            driverTools.EditList();
        }

        /// <summary>
        /// Applies a preset's search engine and settings. A preset whose engine the list does not offer yet
        /// (e.g. DIA-NN, before the DIA search method page chose it) is applied once it does.
        /// </summary>
        public void ApplySearchSettingsPreset(SearchSettingsPreset preset)
        {
            _preset = preset;
            if (!_engineChoices.Contains(preset.SearchEngine))
                return;

            // Set search engine first (triggers InitializeEngine via event handler)
            SelectedSearchEngine = preset.SearchEngine;
            ApplyPresetSettings(preset);
        }

        private void ApplyPresetSettings(SearchSettingsPreset preset)
        {
            // Set tolerances: zero means clear the fields (use engine defaults), except for DIA-NN,
            // which chooses them itself when they are 0
            if (preset.PrecursorToleranceValue > 0 || preset.SearchEngine == SearchEngine.DIANN)
                PrecursorTolerance = preset.PrecursorTolerance;
            else
                txtMS1Tolerance.Text = string.Empty;
            if (preset.FragmentToleranceValue > 0 || preset.SearchEngine == SearchEngine.DIANN)
                FragmentTolerance = preset.FragmentTolerance;
            else
                txtMS2Tolerance.Text = string.Empty;
            MaxVariableMods = preset.MaxVariableMods;

            // Set fragment ions if specified
            if (!string.IsNullOrEmpty(preset.FragmentIons))
            {
                var index = cbFragmentIons.Items.IndexOf(preset.FragmentIons);
                if (index >= 0)
                    cbFragmentIons.SelectedIndex = index;
            }

            // Set MS2 analyzer if specified
            if (!string.IsNullOrEmpty(preset.Ms2Analyzer))
            {
                var index = cbMs2Analyzer.Items.IndexOf(preset.Ms2Analyzer);
                if (index >= 0)
                    cbMs2Analyzer.SelectedIndex = index;
            }

            CutoffScore = preset.CutoffScore;

            // Apply additional settings
            preset.ApplyAdditionalSettings(ImportPeptideSearch.SearchEngine);
        }
    }

}
