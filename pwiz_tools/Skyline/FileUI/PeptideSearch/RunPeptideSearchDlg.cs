/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.BiblioSpec;
using pwiz.Common.Chemistry;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    /// <summary>
    /// Runs a peptide search on spectrum files (or feature detection on them), builds a spectral
    /// library from the results and imports chromatograms for what was found.
    /// </summary>
    public class RunPeptideSearchDlg : PeptideSearchWizardDlg
    {
        private SettingsListComboDriver<SearchSettingsPreset> _settingsPresetDriver;
        private SearchSettingsPreset _lastAppliedPreset;
        private SearchSettingsPreset _modificationsPagePreset; // The preset whose modifications the modifications page has checked
        private bool _suppressPresetWarning;
        private MsDataFileUri[] _originalDdaSearchDataSources;
        private bool _expandedDdaSearchLog;
        private PeptideLibraries _existingLibraries;

        // True while a DDA/DIA/feature-detection search is running on its background thread and streaming
        // progress into the SearchControl's log text box (which has its own Cancel button, not a LongWaitDlg).
        // Set when the search is launched (InitiateSearch) and cleared when it finishes (SearchControlSearchFinished).
        private bool _searchRunning;

        public RunPeptideSearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager, Workflow? workflowType)
            : base(skylineWindow, libraryManager, true, workflowType)
        {
            ConverterSettingsControl = new ConverterSettingsControl(this, ImportPeptideSearch, () => FullScanSettingsControl);
            AddPageControl(ConverterSettingsControl, converterSettingsPage, 18, 50);

            var isFeatureDetection = workflowType is Workflow.feature_detection;

            SearchSettingsControl = new SearchSettingsControl(this, ImportPeptideSearch);
            AddPageControl(SearchSettingsControl, ddaSearchSettingsPage, 18, isFeatureDetection ? buildSpectralLibraryTitlePanel.Bottom : 50);

            if (isFeatureDetection)
            {
                SearchControl = new HardklorSearchControl(ImportPeptideSearch);
            }
            else
            {
                SearchControl = new DDASearchControl(ImportPeptideSearch);
            }
            AddPageControl(SearchControl, ddaSearchPage, isFeatureDetection ? 3 : 18, 50);
            if (isFeatureDetection)
            {
                SearchControl.SetProgressBarDisplayStyle(ProgressBarDisplayText.CustomText);
            }

            // Initialize settings preset controls at the bottom-left of the dialog
            if (!isFeatureDetection)
            {
                InitSettingsPresetControls();
            }
            else
            {
                lblSettingsPreset.Visible = false;
                cbSettingsPreset.Visible = false;
                btnSavePreset.Visible = false;
            }

            if (workflowType.HasValue)
            {
                if (isFeatureDetection)
                {
                    Text = PeptideSearchResources.ImportPeptideSearchDlg_ImportPeptideSearchDlg_Feature_Detection;
                    lblDDASearch.Text = PeptideSearchResources.ImportPeptideSearchDlg_ImportPeptideSearchDlg_Feature_Detection; // Was "DDA Search"
                    // Set some defaults
                    SearchSettingsControl.HardklorSignalToNoise = Settings.Default.FeatureFindingSignalToNoise;
                    SearchSettingsControl.HardklorMinIdotP = Settings.Default.FeatureFindingMinIdotP;
                    SearchSettingsControl.HardklorMinIntensityPPM = Settings.Default.FeatureFindingMinIntensityPPM;
                }

                BuildPepSearchLibControl.ForceWorkflow(workflowType.Value);

                if (isFeatureDetection)
                {
                    Height = BuildPepSearchLibControl.Bottom;
                }
            }

            label14.Text = PeptideSearchResources.BuildPeptideSearchLibraryControl_btnAddFile_Click_Select_Files_to_Search; // Was "Spectral Library"
            label19.Text = PeptideSearchResources.ImportPeptideSearchDlg_ConfigureFullScanChromatogramExtraction;

            SetWizardPages(new[]
            {
                new WizardPage(Pages.spectra_page, () => true, LeaveSpectraPage,
                    () => MakeFullScanSettingsControl(WorkflowType)), // reset UI to default
                new WizardPage(Pages.chromatograms_page, () => WorkflowType == Workflow.dia, LeaveChromatogramsPage),
                new WizardPage(Pages.match_modifications_page, () => !IsFeatureDetectionWorkflow, LeaveModificationsPage,
                    ReturnToModificationsPage),
                new WizardPage(Pages.transition_settings_page, () => !IsDdaWorkflow, LeaveTransitionSettingsPage,
                    () => RestoreDocumentBeforePage(Pages.transition_settings_page)),
                new WizardPage(Pages.full_scan_settings_page, () => true, LeaveFullScanSettingsPage,
                    ReturnToFullScanSettingsPage),
                new WizardPage(Pages.import_fasta_page, () => !IsFeatureDetectionWorkflow, LeaveFastaPage),
                new WizardPage(Pages.converter_settings_page, () => WorkflowType == Workflow.dia, LeaveConverterSettingsPage),
                new WizardPage(Pages.dda_search_settings_page, () => true, LeaveSearchSettingsPage,
                    ReturnToSearchSettingsPage),
                new WizardPage(Pages.dda_search_page, () => true, LeaveSearchPage)
            });
        }

        public ConverterSettingsControl ConverterSettingsControl { get; }
        public SearchSettingsControl SearchSettingsControl { get; }
        public SearchControl SearchControl { get; }

        protected override SearchSettingsControl.DdaSearchSettings DdaSearchSettings =>
            ImportPeptideSearch.IsDDASearch && !IsFeatureDetectionWorkflow ? SearchSettingsControl.SearchSettings : null;

        protected override ConverterSettingsControl.DdaConverterSettings DdaConverterSettings => ConverterSettingsControl.ConverterSettings;

        // ILongWaitForm: the one long operation the wizard drives in its own progress display, rather than in a
        // LongWaitDlg the connector's watchdog already rides through, is the search on the DDA search page.
        public override bool IsBusy => _searchRunning;

        #region Pages

        private bool LeaveSpectraPage()
        {
            ImportPeptideSearch.IsDDASearch = !IsFeatureDetectionWorkflow;
            ImportPeptideSearch.IsDIASearch = WorkflowType == Workflow.dia;
            ImportFastaControl.IsDDASearch = !IsFeatureDetectionWorkflow;

            PrepareFollowingPages(TakePresetForModificationsPage());

            ImportPeptideSearch.SpectrumSourceFiles.Clear();

            // Set SpectrumSourceFiles from the files to search and offer to remove a common prefix
            var uniqueNames = Helpers.EnsureUniqueNames(BuildPepSearchLibControl.DdaSearchDataSources.Select(s => s.GetFileName()).ToList());
            for (var i = 0; i < BuildPepSearchLibControl.DdaSearchDataSources.Length; i++)
            {
                var source = BuildPepSearchLibControl.DdaSearchDataSources[i];
                ImportPeptideSearch.SpectrumSourceFiles.Add(uniqueNames[i],
                        new ImportPeptideSearch.FoundResultsFilePossibilities(uniqueNames[i]) {ExactMatch = source.ToString()});
            }

            if (IsDdaWorkflow)
                ShowRemovePrefixDialog();
            return true;
        }

        private void ReturnToModificationsPage()
        {
            RestoreDocumentBeforePage(Pages.match_modifications_page);
            // Check the modifications of a preset applied on a later page
            var preset = TakePresetForModificationsPage();
            if (preset != null)
                MatchModificationsControl.Initialize(Document, preset);
        }

        /// <summary>
        /// The applied preset, when the modifications page has not yet checked its modifications;
        /// otherwise null, so the page keeps what the user checked.
        /// </summary>
        private SearchSettingsPreset TakePresetForModificationsPage()
        {
            if (ReferenceEquals(_lastAppliedPreset, _modificationsPagePreset))
                return null;
            _modificationsPagePreset = _lastAppliedPreset;
            return _lastAppliedPreset;
        }

        protected override bool LeaveFullScanSettingsPage()
        {
            if (!base.LeaveFullScanSettingsPage())
                return false;
            SearchSettingsControl.UpdateControls(); // Feature Finding controls depend on FullScan settings
            return true;
        }

        private bool LeaveFastaPage()
        {
            ImportPeptideSearch.CutoffScore = SearchSettingsControl.CutoffScore;

            if (!File.Exists(ImportFastaControl.FastaFile))
            {
                MessageDlg.Show(this, PeptideSearchResources.ImportPeptideSearchDlg_NextPage_FastFileMissing_DDASearch);
                return false;
            }

            if (ImportPeptideSearch.IsDIASearch)
                ConverterSettingsControl.InitializeProtocol(ConverterSettingsControl.Protocol.dia_umpire);
            return true;
        }

        private bool LeaveConverterSettingsPage()
        {
            if (ConverterSettingsControl.UseDiaUmpire)
            {
                if (FullScanSettingsControl.IsolationScheme.PrespecifiedIsolationWindows.Count == 0)
                {
                    MessageDlg.Show(this, PeptideSearchResources.ImportPeptideSearchDlg_NextPage_No_isolation_windows_are_configured__);
                    return false;
                }
                ImportPeptideSearch.DdaConverter = ConverterSettingsControl.GetDiaUmpireConverter();
            }

            ImportPeptideSearch.IsDIASearch = !ConverterSettingsControl.UseDiaUmpire;
            SearchSettingsControl.InitializeControls();
            return true;
        }

        private bool LeaveSearchSettingsPage()
        {
            if (!SearchSettingsControl.SaveAllSettings(!IsAutomatedTest))
                return false;
            InitiateSearch();
            return true;
        }

        private void ReturnToSearchSettingsPage()
        {
            btnNext.Enabled = true;
            // Restore original data sources if they were replaced by search result filenames
            if (_originalDdaSearchDataSources != null)
            {
                BuildPepSearchLibControl.DdaSearchDataSources = _originalDdaSearchDataSources;
                _originalDdaSearchDataSources = null;
            }
        }

        /// <summary>
        /// After the search: builds the library from the search results, then imports the FASTA or
        /// adds the detected features to the document. Leaving this page finishes the wizard.
        /// </summary>
        private bool LeaveSearchPage()
        {
            var eCancel = new CancelEventArgs();
            // Save original data sources before replacing with search result files
            _originalDdaSearchDataSources = BuildPepSearchLibControl.DdaSearchDataSources;
            //change search files to result files
            BuildPepSearchLibControl.Grid.IsFileOnly = false;
            var scoreThreshold = SearchSettingsControl.CutoffScore;
            var scoreType = IsFeatureDetectionWorkflow
                ? ScoreType.HardklorIdotp
                : ScoreType.GenericQValue;

            BuildPepSearchLibControl.Grid.Files = ImportPeptideSearch.SearchEngine.SpectrumFileNames.Select(f =>
                new BuildLibraryGridView.File(ImportPeptideSearch.SearchEngine.GetSearchResultFilepath(f), scoreType, scoreThreshold));
            BuildPepSearchLibControl.ImportPeptideSearch.SearchFilenames = BuildPepSearchLibControl.Grid.FilePaths.ToArray();
            _existingLibraries = Document.Settings.PeptideSettings.Libraries;
            if (IsFeatureDetectionWorkflow)
            {
                // Disable navigation while the library build is happening
                btnBack.Enabled = false;
                btnNext.Enabled = false;
            }

            if (!BuildPeptideSearchLibrary(eCancel, IsFeatureDetectionWorkflow))
                return false;

            if (IsFeatureDetectionWorkflow)
            {
                // Load detected features after search
                AddDetectedFeaturesToDocument();
            }
            //load proteins after search
            else if (!ImportFastaControl.ImportFasta(ImportPeptideSearch.IrtStandard))
                return false;

            ImportPeptideSearch.SearchEngine.Dispose();
            return true;
        }

        #endregion

        protected override void UpdateButtons()
        {
            base.UpdateButtons();

            // Enable save button only once search settings have been configured
            if (btnSavePreset.Visible)
                btnSavePreset.Enabled = CurrentPage >= Pages.dda_search_settings_page;
        }

        protected override void BeforeImportResults()
        {
            UpdateFullScanSettingsForFeatureDetection(); // Tweak full scan filter values if needed
        }

        protected override void OnWizardClosing()
        {
            // Cancel and dispose DDA SearchEngine
            SearchControl?.Cancel();
            ImportPeptideSearch.SearchEngine?.Dispose();
        }

        private void AddDetectedFeaturesToDocument()
        {
            SrmDocument docNew = null;
            bool noFeatures = false;
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.Text = PeptideSearchResources.ImportPeptideSearchDlg_ImportPeptideSearchDlg_Feature_Detection;
                longWaitDlg.Message = PeptideSearchResources.ImportPeptideSearchDlg_NextPage_Adding_detected_features_to_document;
                var document = Document;
                longWaitDlg.PerformWork(this, 1000, progressMonitor =>
                {
                    docNew = ImportPeptideSearch.AddDetectedFeatures(document, _existingLibraries, progressMonitor,
                        new ProgressStatus(PeptideSearchResources.ImportPeptideSearchDlg_NextPage_Adding_detected_features_to_document));
                    noFeatures = docNew == null;
                });
            }

            if (noFeatures)
                MessageDlg.Show(this, PeptideSearchResources.ImportPeptideSearchDlg_AddDetectedFeaturesToDocument_No_features_detected);
            if (docNew == null)
                return;

            SetDocument(docNew, Document);

            // Update UI for new content type
            GetModeUIHelper().ModeUI = Document.HasPeptides
                ? SrmDocument.DOCUMENT_TYPE.mixed
                : SrmDocument.DOCUMENT_TYPE.small_molecules;
        }
        private void InitiateSearch()
        {
            ImportFastaControl.UpdateDigestSettings();
            ImportPeptideSearch.PrepareSearch(Document, BuildPepSearchLibControl.DdaSearchDataSources, ImportFastaControl.FastaFile,
                CreateConverter);
            SearchControl.SearchFinished -= SearchControlSearchFinished;
            SearchControl.SearchFinished -= SearchControlSearchStepFinished;
            if (ImportPeptideSearch.RemainingStepsInSearch > 1)
            {
                SearchControl.SearchFinished += SearchControlSearchStepFinished; // Two step search, e.g. Hardklor then Bullseye
            }
            else
            {
                SearchControl.SearchFinished += SearchControlSearchFinished;
            }
            btnNext.Enabled = false;
            btnCancel.Enabled = false;
            btnBack.Enabled = false;

            if (!_expandedDdaSearchLog)
            {
                // No longer necessary after widening the form for new library build grid
                // Width = Math.Min(Screen.FromControl(this).WorkingArea.Width, (int) (Width * 1.0)); // give more space for search log
                _expandedDdaSearchLog = true;
            }

            _searchRunning = true;
            SearchControl.RunSearch();
        }

        private AbstractDdaConverter CreateConverter()
        {
            if (IsFeatureDetectionWorkflow)
                return ConverterSettingsControl.GetHardklorConverter();
            if (ImportPeptideSearch.IsDIASearch)
                return ConverterSettingsControl.GetDiaConverter();
            return ConverterSettingsControl.GetDdaConverter();
        }

        private void SearchControlSearchStepFinished(bool success)
        {
            if (success)
            {
                if (ImportPeptideSearch.RemainingStepsInSearch <= 1)
                {
                    SearchControl.SearchFinished -= SearchControlSearchStepFinished;
                    SearchControl.SearchFinished += SearchControlSearchFinished;
                    SearchControlSearchFinished(true);
                }
            }
            else
            {
                SearchControl.SearchFinished -= SearchControlSearchStepFinished;
                SearchControlSearchFinished(false);
            }
        }

        private void SearchControlSearchFinished(bool success)
        {
            _searchRunning = false;
            btnCancel.Enabled = true;
            btnBack.Enabled = true;
            btnNext.Enabled = success;
        }

        // In feature detection, the MS1 resolution values we solicit from the user are for Hardklor's peak finding
        // purposes. This function translates those settings to be give the same tolerance sense to Skyline's chromatogram
        // extraction logic.
        private void UpdateFullScanSettingsForFeatureDetection()
        {
            if (!IsFeatureDetectionWorkflow)
            {
                return; // No need
            }

            var precursorRes = TransitionSettings.FullScan.PrecursorRes ?? 0;
            if (Equals(TransitionSettings.FullScan.PrecursorMassAnalyzer, FullScanMassAnalyzerType.qit))
            {
                precursorRes = precursorRes / 5000.0; // per Hardklor source code CHardklor2::CalcFWHM(double mz, double res, int iType)
            }
            var newFullScanSettings = Document.Settings.TransitionSettings.FullScan.ChangePrecursorResolution(
                TransitionSettings.FullScan.PrecursorMassAnalyzer, precursorRes,
                TransitionSettings.FullScan.PrecursorMassAnalyzer == FullScanMassAnalyzerType.orbitrap ||
                TransitionSettings.FullScan.PrecursorMassAnalyzer == FullScanMassAnalyzerType.ft_icr ?
                    FullScanSettingsControl.HARDKLOR_PRECURSOR_RES_MZ : null);
            var newTransitionSettings = Document.Settings.TransitionSettings.ChangeFullScan(newFullScanSettings);
            var docNew = Document.ChangeSettings(Document.Settings.ChangeTransitionSettings(newTransitionSettings));
            SetDocument(docNew, Document);
        }

        #region Settings Preset

        private void InitSettingsPresetControls()
        {
            // DIA-NN presets are owned by Settings.Default.DiannSearchSettingsPresets and
            // surface only in DiannSearchDlg's dropdown; this list is DDA/DIA engines only.
            _settingsPresetDriver = new SettingsListComboDriver<SearchSettingsPreset>(
                cbSettingsPreset,
                Settings.Default.SearchSettingsPresets,
                true);
            LoadPresetList(null);

            cbSettingsPreset.SelectedIndexChanged += cbSettingsPreset_SelectedIndexChanged;
            btnSavePreset.Click += btnSavePreset_Click;
            btnSavePreset.Enabled = false; // Enable once search settings page is reached
        }

        private static void EnsureDefaultPresets()
        {
            var presets = Settings.Default.SearchSettingsPresets;
            foreach (var defaultPreset in presets.GetDefaults())
            {
                if (!presets.Any(p => p.Name == defaultPreset.Name))
                    presets.Insert(0, defaultPreset);
            }
        }

        private void LoadPresetList(string selectedName)
        {
            EnsureDefaultPresets();
            _suppressPresetWarning = true;
            try
            {
                _settingsPresetDriver.LoadList(selectedName);
            }
            finally
            {
                _suppressPresetWarning = false;
            }
        }

        private void cbSettingsPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_settingsPresetDriver.SelectedIndexChangedEvent(sender, e))
            {
                LoadPresetList(_settingsPresetDriver.SelectedItem?.Name);
                return;
            }

            var preset = _settingsPresetDriver.SelectedItem;
            if (preset != null)
            {
                // Warn when changing preset on a page other than the first
                if (CurrentPage != Pages.spectra_page && !WarnAndConfirmPresetChange())
                {
                    // User cancelled - restore previous selection
                    LoadPresetList(_lastAppliedPreset?.Name);
                    return;
                }
                ApplyPreset(preset);
            }
        }

        private bool WarnAndConfirmPresetChange()
        {
            if (!Settings.Default.WarnOnPresetChange || _suppressPresetWarning)
                return true;

            using (var dlg = new WarnOnPresetChangeDlg())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return false;

                if (dlg.DontShowAgain)
                    Settings.Default.WarnOnPresetChange = false;

                return true;
            }
        }

        /// <summary>
        /// Called by SearchSettingsControl when a params file is imported as a preset.
        /// </summary>
        public void AddImportedPreset(SearchSettingsPreset preset)
        {
            Settings.Default.SearchSettingsPresets.Add(preset);
            LoadPresetList(preset.Name);
            ApplyPreset(preset);
        }

        private void ApplyPreset(SearchSettingsPreset preset)
        {
            _lastAppliedPreset = preset;

            // Apply search engine settings
            SearchSettingsControl?.ApplySearchSettingsPreset(preset);

            // Apply FASTA settings.
            //   null  -> preset doesn't touch FASTA (legacy preset without the attribute)
            //   ""    -> preset explicitly cleared FASTA
            //   path  -> preset specifies a FASTA to load
            if (preset.FastaFilePath == string.Empty)
                ImportFastaControl.ClearFastaContent();
            else if (!string.IsNullOrEmpty(preset.FastaFilePath))
                ImportFastaControl.SetFastaContent(preset.FastaFilePath, true);
            if (!string.IsNullOrEmpty(preset.EnzymeName))
            {
                var enzyme = Settings.Default.GetEnzymeByName(preset.EnzymeName);
                if (enzyme != null && enzyme.Name == preset.EnzymeName)
                    ImportFastaControl.Enzyme = enzyme;
                else
                {
                    // Enzyme not in current list; look it up from defaults
                    var defaultEnzyme = new EnzymeList().GetDefaults(0)
                        .FirstOrDefault(e => e.Name == preset.EnzymeName);
                    if (defaultEnzyme != null)
                    {
                        if (!Settings.Default.EnzymeList.ContainsKey(preset.EnzymeName))
                            Settings.Default.EnzymeList.Add(defaultEnzyme);
                        ImportFastaControl.Enzyme = defaultEnzyme;
                    }
                }
            }
            ImportFastaControl.MaxMissedCleavages = preset.MaxMissedCleavages;

            // Apply modification settings immediately if we're on the mods page, otherwise the next time it is set up
            _modificationsPagePreset = null;
            if (CurrentPage == Pages.match_modifications_page)
                MatchModificationsControl.Initialize(Document, TakePresetForModificationsPage());

            // Apply workflow and IRT settings (null means reset to defaults)
            BuildPepSearchLibControl.WorkflowType = preset.Workflow.HasValue
                ? (Workflow)preset.Workflow.Value
                : Workflow.dda;
            if (!string.IsNullOrEmpty(preset.IrtStandardName))
            {
                var irtStandard = IrtStandard.ALL.FirstOrDefault(s => s.Name == preset.IrtStandardName);
                if (irtStandard != null)
                    BuildPepSearchLibControl.IrtStandards = irtStandard;
            }
            else
            {
                BuildPepSearchLibControl.IrtStandards = IrtStandard.EMPTY;
            }
        }

        private void btnSavePreset_Click(object sender, EventArgs e)
        {
            var currentPreset = _settingsPresetDriver.SelectedItem;
            var isDefault = currentPreset != null &&
                            Settings.Default.SearchSettingsPresets.GetDefaults(0).Any(d => d.Name == currentPreset.Name);
            var suggestedName = currentPreset != null && !isDefault
                ? currentPreset.Name
                : SearchSettingsControl != null
                    ? $@"{SearchSettingsControl.SelectedSearchEngine} - "
                    : string.Empty;

            string name;
            using (var dlg = new PresetNameDlg(suggestedName))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                name = dlg.PresetName;
            }

            if (string.IsNullOrWhiteSpace(name))
                return;

            var existingPreset = Settings.Default.SearchSettingsPresets.FirstOrDefault(w => w.Name == name);
            if (existingPreset != null)
            {
                var result = MessageDlg.Show(this,
                    string.Format(PeptideSearchResources.SearchSettingsControl_OverwriteSettingsPreset_A_settings_preset_named__0__already_exists__Do_you_want_to_replace_it_, name),
                    false, MessageBoxButtons.YesNo);
                if (result != DialogResult.Yes)
                    return;
                Settings.Default.SearchSettingsPresets.Remove(existingPreset);
            }

            var preset = BuildPresetFromCurrentSettings(name);
            Settings.Default.SearchSettingsPresets.Add(preset);
            LoadPresetList(name);
        }

        private static MzTolerance GetToleranceSafe(Func<MzTolerance> getter)
        {
            try { return getter() ?? new MzTolerance(0, MzTolerance.Units.ppm); }
            catch (FormatException) { return new MzTolerance(0, MzTolerance.Units.ppm); }
        }

        private SearchSettingsPreset BuildPresetFromCurrentSettings(string name)
        {
            // Determine mod state based on context:
            // - The mods page is set up and shows the applied preset's mods (or no preset is applied):
            //   read directly from the listbox, which keeps what the user checked
            // - A preset applied since the mods page was set up: use the preset's mods, which the
            //   mods page checks the next time it is set up
            // - Otherwise: use document mods (reflects last UpdateModificationSettings)
            IEnumerable<StaticMod> structuralMods;
            IEnumerable<StaticMod> heavyMods;
            if (CurrentPage == Pages.match_modifications_page ||
                (MatchModificationsControl.MatchedModifications.Any() && ReferenceEquals(_lastAppliedPreset, _modificationsPagePreset)))
            {
                var checkedModNames = MatchModificationsControl?.CheckedModificationNames?.ToList() ?? new List<string>();
                structuralMods = checkedModNames
                    .Select(modName => Settings.Default.StaticModList.FirstOrDefault(m => m.Name == modName))
                    .Where(m => m != null);
                heavyMods = checkedModNames
                    .Select(modName => Settings.Default.HeavyModList.FirstOrDefault(m => m.Name == modName))
                    .Where(m => m != null);
            }
            else if (_lastAppliedPreset != null && _lastAppliedPreset.HasExplicitModifications)
            {
                structuralMods = _lastAppliedPreset.StructuralModifications;
                heavyMods = _lastAppliedPreset.HeavyModifications;
            }
            else
            {
                var docMods = Document.Settings.PeptideSettings.Modifications;
                structuralMods = docMods.StaticModifications;
                heavyMods = docMods.AllHeavyModifications;
            }

            return new SearchSettingsPreset(
                name,
                SearchSettingsControl?.SelectedSearchEngine ?? SearchEngine.MSAmanda,
                GetToleranceSafe(() => SearchSettingsControl?.PrecursorTolerance),
                GetToleranceSafe(() => SearchSettingsControl?.FragmentTolerance),
                SearchSettingsControl?.MaxVariableMods ?? 2,
                SearchSettingsControl?.FragmentIons,
                SearchSettingsControl?.Ms2Analyzer,
                SearchSettingsControl?.CutoffScore ?? 0.01,
                SearchSettingsPreset.SerializeAdditionalSettings(ImportPeptideSearch.SearchEngine?.AdditionalSettings),
                ImportFastaControl?.FastaFile,
                ImportFastaControl?.Enzyme?.Name,
                ImportFastaControl?.MaxMissedCleavages ?? 0,
                structuralModifications: structuralMods,
                heavyModifications: heavyMods,
                workflowType: (SearchWorkflowType)BuildPepSearchLibControl.WorkflowType,
                irtStandardName: BuildPepSearchLibControl.IrtStandards?.Name,
                hasExplicitModifications: true);
        }

        // Test helpers
        public bool SettingsPresetVisible => cbSettingsPreset?.Visible ?? false;

        public void SelectEditList()
        {
            for (int i = 0; i < cbSettingsPreset.Items.Count; i++)
            {
                if (cbSettingsPreset.Items[i].ToString() ==
                    Resources.SettingsListComboDriver_Edit_list)
                {
                    cbSettingsPreset.SelectedIndex = i;
                    return;
                }
            }
        }

        public string SelectedPresetName
        {
            get => _settingsPresetDriver?.SelectedItem?.Name;
            set
            {
                if (_settingsPresetDriver == null)
                    return;
                for (int i = 0; i < cbSettingsPreset.Items.Count; i++)
                {
                    if (cbSettingsPreset.Items[i].ToString() == value)
                    {
                        cbSettingsPreset.SelectedIndex = i;
                        return;
                    }
                }
                if (string.IsNullOrEmpty(value))
                    _settingsPresetDriver.LoadList(null);
            }
        }

        public IEnumerable<string> PresetNames => Settings.Default.SearchSettingsPresets.Select(w => w.Name);

        public void SaveSettingsPreset(string name)
        {
            var existingPreset = Settings.Default.SearchSettingsPresets.FirstOrDefault(w => w.Name == name);
            if (existingPreset != null)
                Settings.Default.SearchSettingsPresets.Remove(existingPreset);

            var preset = BuildPresetFromCurrentSettings(name);
            Settings.Default.SearchSettingsPresets.Add(preset);
            LoadPresetList(name);
        }

        public void ImportSettingsPreset(string filePath, string presetName)
        {
            var existingPreset = Settings.Default.SearchSettingsPresets.FirstOrDefault(w => w.Name == presetName);
            if (existingPreset != null)
                Settings.Default.SearchSettingsPresets.Remove(existingPreset);

            var preset = SearchSettingsParamsFileParser.ImportFromFile(filePath, presetName);
            Settings.Default.SearchSettingsPresets.Add(preset);
            LoadPresetList(presetName);
            ApplyPreset(preset);
        }

        public SearchSettingsPreset LastAppliedPreset => _lastAppliedPreset;

        #endregion
    }
}
