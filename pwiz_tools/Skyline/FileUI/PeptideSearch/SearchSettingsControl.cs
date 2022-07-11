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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class SearchSettingsControl : UserControl
    {
        private ImportPeptideSearch ImportPeptideSearch { get; set; }
        private readonly IModifyDocumentContainer _documentContainer;
    
        public SearchSettingsControl(IModifyDocumentContainer documentContainer, ImportPeptideSearch importPeptideSearch)
        {
            InitializeComponent();
            ImportPeptideSearch = importPeptideSearch;
            _documentContainer = documentContainer;

            searchEngineComboBox.SelectedIndexChanged += SearchEngineComboBox_SelectedIndexChanged;
            txtMS1Tolerance.LostFocus += txtMS1Tolerance_LostFocus;
            txtMS2Tolerance.LostFocus += txtMS2Tolerance_LostFocus;

            searchEngineComboBox.SelectedIndex = 0;

            LoadMassUnitEntries();
        }

        private void SearchEngineComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ImportPeptideSearch.SearchEngine?.Dispose();
            ImportPeptideSearch.SearchEngine = InitSelectedSearchEngine();
            InitializeEngine();
        }

        public enum SearchEngine
        {
            MSAmanda,
            MSGFPlus,
            MSFragger
        }

        public SearchEngine SelectedSearchEngine
        {
            get
            {
                return (SearchEngine) searchEngineComboBox.SelectedIndex;
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

            SimpleFileDownloaderDlg.Show(TopLevelControl, string.Format(Resources.SearchSettingsControl_EnsureRequiredFilesDownloaded_Download__0_, searchEngineComboBox.SelectedItem),  filesNotAlreadyDownloaded);

            return !SimpleFileDownloader.FilesNotAlreadyDownloaded(filesNotAlreadyDownloaded).Any();
        }

        public static bool HasRequiredFilesDownloaded(SearchEngine searchEngine)
        {
            FileDownloadInfo[] fileDownloadInfo;
            switch (searchEngine)
            {
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
                    return new MsFraggerSearchEngine(1 - ImportPeptideSearch.CutoffScore);
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
                control.FragmentTolerance, control.MaxVariableMods, control.FragmentIons, control.Ms2Analyzer)
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

            public DdaSearchSettings(SearchEngine searchEngine, MzTolerance precursorTolerance, MzTolerance fragmentTolerance, int maxVariableMods, string fragmentIons, string ms2Analyzer)
            {
                SearchEngine = searchEngine;
                PrecursorTolerance = precursorTolerance;
                FragmentTolerance = fragmentTolerance;
                MaxVariableMods = maxVariableMods;
                FragmentIons = fragmentIons;
                Ms2Analyzer = ms2Analyzer;
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
            btnAdditionalSettings.Enabled = ImportPeptideSearch.SearchEngine.AdditionalSettings != null;
        }

        private void LoadComboboxEntries()
        {
            LoadFragmentIonEntries();
            LoadMs2AnalyzerEntries();

            var modSettings = _documentContainer.Document.Settings.PeptideSettings.Modifications;
            cbMaxVariableMods.SelectedItem = modSettings.MaxVariableMods.ToString(LocalizationHelper.CurrentCulture);
            if (cbMaxVariableMods.SelectedIndex < 0)
                cbMaxVariableMods.SelectedIndex = 2; // default max = 2
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
   
        public bool SaveAllSettings()
        {
            bool valid = ValidateEntries();
            if (!valid)
                return false;

            var modSettings = _documentContainer.Document.Settings.PeptideSettings.Modifications;
            var allMods = modSettings.StaticModifications.Union(modSettings.AllHeavyModifications);
            ImportPeptideSearch.SearchEngine.SetModifications(allMods, Convert.ToInt32(cbMaxVariableMods.SelectedItem));
            return true;
        }

        private bool ValidateEntries()
        {
            var helper = new MessageBoxHelper(this.ParentForm);
            double ms1Tol;
            if (!helper.ValidateDecimalTextBox(txtMS1Tolerance, 0, 100, out ms1Tol))
            {
                helper.ShowTextBoxError(txtMS1Tolerance, 
                    Resources.DdaSearch_SearchSettingsControl_MS1_Tolerance_incorrect);
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetPrecursorMassTolerance(PrecursorTolerance);

            double ms2Tol;
            if (!helper.ValidateDecimalTextBox(txtMS2Tolerance, 0, 100, out ms2Tol))
            {
                helper.ShowTextBoxError(txtMS2Tolerance, 
                    Resources.DdaSearch_SearchSettingsControl_MS2_Tolerance_incorrect);
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetFragmentIonMassTolerance(FragmentTolerance);

            string fragmentIons;
            if (!ValidateCombobox(cbFragmentIons, out fragmentIons))
            {
                helper.ShowTextBoxError(cbFragmentIons, 
                    Resources.DdaSearch_SearchSettingsControl_Fragment_ions_must_be_selected);
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetFragmentIons(fragmentIons);

            string ms2Analyzer;
            if (!ValidateCombobox(cbMs2Analyzer, out ms2Analyzer))
            {
                helper.ShowTextBoxError(cbMs2Analyzer,
                    Resources.DdaSearch_SearchSettingsControl_MS2_analyzer_must_be_selected);
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetMs2Analyzer(ms2Analyzer);

            return true;
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

            KeyValueGridDlg.Show(Resources.SearchSettingsControl_Additional_Settings,
                ImportPeptideSearch.SearchEngine.AdditionalSettings,
                (setting) => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value));
        }
    }

}
