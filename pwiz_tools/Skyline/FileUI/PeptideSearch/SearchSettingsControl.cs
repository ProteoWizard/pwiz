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

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class SearchSettingsControl : UserControl
    {
        private ImportPeptideSearch ImportPeptideSearch { get; set; }
        private readonly ImportPeptideSearchDlg _documentContainer;
        private readonly FullScanSettingsControl _hardklorInstrumentSettingsControl;

        public SearchSettingsControl(ImportPeptideSearchDlg documentContainer, ImportPeptideSearch importPeptideSearch)
        {
            InitializeComponent();
            ImportPeptideSearch = importPeptideSearch;
            _documentContainer = documentContainer;

            if (importPeptideSearch.IsFeatureDetection)
            {
                SearchEngineComboBox_SelectedIndexChanged(null, null); // Initialize
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
                _hardklorInstrumentSettingsControl = new FullScanSettingsControl(documentContainer, ImportPeptideSearch.eFeatureDetectionPhase.hardklor_settings);
                _hardklorInstrumentSettingsControl.ModifyOptionsForImportPeptideSearchWizard(ImportPeptideSearchDlg.Workflow.feature_detection, false, ImportPeptideSearch.eFeatureDetectionPhase.hardklor_settings);
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

                searchEngineComboBox.SelectedIndex = 0;
                groupBoxHardklor.Enabled = groupBoxHardklor.Visible = false;
            }

            LoadMassUnitEntries();
        }

        private void SearchEngineComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ImportPeptideSearch.SearchEngine?.Dispose();
            ImportPeptideSearch.SearchEngine = InitSelectedSearchEngine();
            InitializeEngine();
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

        public enum SearchEngine
        {
            MSAmanda,
            MSGFPlus,
            MSFragger,
            Hardklor
        }

        public SearchEngine SelectedSearchEngine
        {
            get
            {
                return ImportPeptideSearch.IsFeatureDetection ? SearchEngine.Hardklor : (SearchEngine) searchEngineComboBox.SelectedIndex;
            }

            set
            {
                searchEngineComboBox.SelectedIndex = (int) value;
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
            }
            catch (Exception exception)
            {
                MessageDlg.ShowWithException(this, exception.Message, exception);
                return false;
            }

            return !SimpleFileDownloader.FilesNotAlreadyDownloaded(filesNotAlreadyDownloaded).Any();
        }

        public static bool HasRequiredFilesDownloaded(SearchEngine searchEngine)
        {
            FileDownloadInfo[] fileDownloadInfo;
            switch (searchEngine)
            {
                case SearchEngine.Hardklor:
                case SearchEngine.MSAmanda:
                    return true;
                case SearchEngine.MSGFPlus:
                    fileDownloadInfo = MsgfPlusSearchEngine.FilesToDownload;
                    break;
                case SearchEngine.MSFragger:
                    fileDownloadInfo = MsFraggerSearchEngine.FilesToDownload;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return !SimpleFileDownloader.FilesNotAlreadyDownloaded(fileDownloadInfo).Any();
        }

        private AbstractDdaSearchEngine InitSelectedSearchEngine()
        {
            pBLogo.Image = null;
            switch (SelectedSearchEngine)
            {
                case SearchEngine.MSAmanda:
                    return new MSAmandaSearchWrapper();
                case SearchEngine.MSGFPlus:
                    if (!EnsureRequiredFilesDownloaded(MsgfPlusSearchEngine.FilesToDownload))
                        SelectedSearchEngine = SearchEngine.MSAmanda;
                    return new MsgfPlusSearchEngine();
                case SearchEngine.MSFragger:
                    if (!EnsureRequiredFilesDownloaded(MsFraggerSearchEngine.FilesToDownload, ShowDownloadMsFraggerDialog))
                        SelectedSearchEngine = SearchEngine.MSAmanda;
                    return new MsFraggerSearchEngine(CutoffScore, ImportPeptideSearch.IsDIASearch);
                case SearchEngine.Hardklor:
                    return new HardklorSearchEngine(ImportPeptideSearch);
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
            LoadComboboxEntries();
            pBLogo.Image = ImportPeptideSearch.SearchEngine.SearchEngineLogo;
            labelCutoff.Text = ImportPeptideSearch.SearchEngine.CutoffScoreLabel + @":";
            HandleSearchEngineBlurb();
            btnAdditionalSettings.Enabled = ImportPeptideSearch.SearchEngine.AdditionalSettings != null;
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
            LoadFragmentIonEntries();
            LoadMs2AnalyzerEntries();

            var modSettings = _documentContainer.Document.Settings.PeptideSettings.Modifications;
            cbMaxVariableMods.SelectedItem = modSettings.MaxVariableMods.ToString(LocalizationHelper.CurrentCulture);
            if (cbMaxVariableMods.SelectedIndex < 0)
                cbMaxVariableMods.SelectedIndex = 2; // default max = 2

            CutoffScore =
                BiblioSpecLiteBuilder.GetDefaultScoreThreshold(ImportPeptideSearch.SearchEngine.CutoffScoreName) ??
                ImportPeptideSearch.SearchEngine.DefaultCutoffScore;
        }

        private void LoadMassUnitEntries()
        {
            cbMS1TolUnit.Items.Clear();
            cbMS2TolUnit.Items.Clear();

            string[] entries = {@"Da", @"ppm"};
            cbMS1TolUnit.Items.AddRange(entries);
            cbMS2TolUnit.Items.AddRange(entries);
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

            var modSettings = _documentContainer.Document.Settings.PeptideSettings.Modifications;
            var allMods = modSettings.StaticModifications.Union(modSettings.AllHeavyModifications);
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

            if (!helper.ValidateDecimalTextBox(txtMS2Tolerance, 0, 100, out _))
            {
                helper.ShowTextBoxError(txtMS2Tolerance, 
                    PeptideSearchResources.DdaSearch_SearchSettingsControl_MS2_Tolerance_incorrect);
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetFragmentIonMassTolerance(FragmentTolerance);

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
            get { return new MzTolerance(double.Parse(txtMS1Tolerance.Text), (MzTolerance.Units) cbMS1TolUnit.SelectedIndex); }

            set
            {
                txtMS1Tolerance.Text = value.Value.ToString(LocalizationHelper.CurrentCulture);
                cbMS1TolUnit.SelectedIndex = (int)value.Unit;
            }
        }

        public MzTolerance FragmentTolerance
        {
            get { return new MzTolerance(double.Parse(txtMS2Tolerance.Text), (MzTolerance.Units) cbMS2TolUnit.SelectedIndex); }

            set
            {
                txtMS2Tolerance.Text = value.Value.ToString(LocalizationHelper.CurrentCulture);
                cbMS2TolUnit.SelectedIndex = (int)value.Unit;
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

            KeyValueGridDlg.Show(PeptideSearchResources.SearchSettingsControl_Additional_Settings,
                ImportPeptideSearch.SearchEngine.AdditionalSettings,
                (setting) => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value));
        }
    }

}
