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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.BiblioSpec;
using pwiz.Common.Chemistry;
using pwiz.Common.Controls;
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
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

        private List<KeyValuePair<string, MsDataFileUri[]>> _namedResults; // The results to import, decided before the run
        private string _runSearchFingerprint; // The search inputs of the run in progress
        private string _searchFingerprint; // The search inputs of the last search that succeeded
        private string[] _searchResultFilenames; // The result files of that search
        private bool _runCompleted;

        // True from the start of the run until it finishes or fails: the search and the steps after it stream
        // progress into the SearchControl's log text box (which has its own Cancel button, not a LongWaitDlg).
        private bool _runRunning;

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

            var spectraPage = new WizardPage(Pages.spectra_page, () => true, LeaveSpectraPage,
                () => MakeFullScanSettingsControl(WorkflowType)); // reset UI to default
            var fullScanPage = new WizardPage(Pages.full_scan_settings_page, () => true, LeaveFullScanSettingsPage,
                ReturnToFullScanSettingsPage);
            var searchSettingsPage = new WizardPage(Pages.dda_search_settings_page, () => true, LeaveSearchSettingsPage,
                () => btnNext.Enabled = true);
            var runPage = new WizardPage(Pages.dda_search_page, () => true, () => _runCompleted, null, StartRun);
            if (isFeatureDetection)
            {
                // Hardklor's settings come from the full-scan settings, so they come first
                SetWizardPages(new[] { spectraPage, fullScanPage, searchSettingsPage, runPage });
            }
            else
            {
                // Everything the search needs comes before the chromatogram extraction settings
                SetWizardPages(new[]
                {
                    spectraPage,
                    new WizardPage(Pages.match_modifications_page, () => true, LeaveModificationsPage,
                        ReturnToModificationsPage),
                    new WizardPage(Pages.import_fasta_page, () => true, LeaveFastaPage),
                    new WizardPage(Pages.converter_settings_page, () => WorkflowType == Workflow.dia, LeaveConverterSettingsPage),
                    searchSettingsPage,
                    new WizardPage(Pages.transition_settings_page, () => !IsDdaWorkflow, LeaveTransitionSettingsPage,
                        () => RestoreDocumentBeforePage(Pages.transition_settings_page)),
                    fullScanPage,
                    runPage
                });
            }
        }

        public ConverterSettingsControl ConverterSettingsControl { get; }
        public SearchSettingsControl SearchSettingsControl { get; }
        public SearchControl SearchControl { get; }

        /// <summary>
        /// True once the run has finished and its document has been applied to Skyline.
        /// </summary>
        public bool IsRunCompleted => _runCompleted;

        /// <summary>
        /// Null while a run is in progress or none has started; otherwise whether the last run succeeded.
        /// </summary>
        public bool? RunSucceeded { get; private set; }

        /// <summary>
        /// What the steps after the search did, once a run has succeeded.
        /// </summary>
        public RunPeptideSearchPipeline.Result LastRunResult { get; private set; }

        /// <summary>
        /// Testing support: called when a search succeeds. Returning false stops the run as if the
        /// search had failed, so a test can go back and repeat a search that came up short.
        /// </summary>
        public Func<bool> AcceptSearchResults { get; set; }

        protected override SearchSettingsControl.DdaSearchSettings DdaSearchSettings =>
            ImportPeptideSearch.IsDDASearch && !IsFeatureDetectionWorkflow ? SearchSettingsControl.SearchSettings : null;

        protected override ConverterSettingsControl.DdaConverterSettings DdaConverterSettings => ConverterSettingsControl.ConverterSettings;

        protected override BuildPeptideSearchLibraryControl.BuildPeptideSearchLibrarySettings BuildLibrarySettings
        {
            get
            {
                var scoreType = IsFeatureDetectionWorkflow ? ScoreType.HardklorIdotp : ScoreType.GenericQValue;
                var files = ImportPeptideSearch.SearchFilenames?.Select(f =>
                    new BuildLibraryGridView.File(f, scoreType, SearchSettingsControl.CutoffScore));
                var control = BuildPepSearchLibControl;
                return new BuildPeptideSearchLibraryControl.BuildPeptideSearchLibrarySettings(files, control.IrtStandards,
                    control.IncludeAmbiguousMatches, control.FilterForDocumentPeptides, control.WorkflowType,
                    control.InputFileType, ModeUI);
            }
        }

        // ILongWaitForm: the one long operation the wizard drives in its own progress display, rather than in a
        // LongWaitDlg the connector's watchdog already rides through, is the run on the last page.
        public override bool IsBusy => _runRunning;

        #region Pages

        private bool LeaveSpectraPage()
        {
            ImportPeptideSearch.IsDDASearch = !IsFeatureDetectionWorkflow;
            ImportPeptideSearch.IsDIASearch = WorkflowType == Workflow.dia;
            ImportFastaControl.IsDDASearch = !IsFeatureDetectionWorkflow;
            ImportFastaControl.IsImportingResults = true;
            if (!IsFeatureDetectionWorkflow)
                ImportFastaControl.ShowProteinAssociationOptions();

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

        private bool LeaveFastaPage()
        {
            if (!File.Exists(ImportFastaControl.FastaFile))
            {
                MessageDlg.Show(this, PeptideSearchResources.ImportPeptideSearchDlg_NextPage_FastFileMissing_DDASearch);
                return false;
            }
            if (!ImportFastaControl.ValidateDecoySettings())
                return false;
            if (!ImportFastaControl.QValueFilterControls.TryGetFilter(this, out _))
                return false;

            if (ImportPeptideSearch.IsDIASearch)
                ConverterSettingsControl.InitializeProtocol(ConverterSettingsControl.Protocol.dia_umpire);
            return true;
        }

        private bool LeaveConverterSettingsPage()
        {
            ImportPeptideSearch.IsGpfData = ConverterSettingsControl.IsGpf;
            ImportPeptideSearch.IsDIASearch = !ConverterSettingsControl.UseDiaUmpire;
            SearchSettingsControl.InitializeControls();
            return true;
        }

        private bool LeaveSearchSettingsPage()
        {
            if (!SearchSettingsControl.SaveAllSettings(!IsAutomatedTest))
                return false;
            // Feature detection's last page before the run
            return !IsFeatureDetectionWorkflow || PrepareRun();
        }

        protected override bool LeaveFullScanSettingsPage()
        {
            if (!base.LeaveFullScanSettingsPage())
                return false;
            if (IsFeatureDetectionWorkflow)
            {
                SearchSettingsControl.UpdateControls(); // Feature Finding controls depend on FullScan settings
                return true;
            }
            // The last page before the run
            return PrepareRun();
        }

        #endregion

        #region Run

        /// <summary>
        /// Asks everything the run still needs to know before it starts, so that nothing stops it
        /// once it is running. False keeps the user on the page.
        /// </summary>
        private bool PrepareRun()
        {
            // DIA-Umpire splits the spectra by the isolation windows of the full-scan settings
            if (WorkflowType == Workflow.dia && ConverterSettingsControl.UseDiaUmpire &&
                (FullScanSettingsControl.IsolationScheme?.PrespecifiedIsolationWindows.Count ?? 0) == 0)
            {
                MessageDlg.Show(this, PeptideSearchResources.ImportPeptideSearchDlg_NextPage_No_isolation_windows_are_configured__);
                return false;
            }

            _namedResults = GetNamedResultsWithLockMass();
            return _namedResults != null;
        }

        /// <summary>
        /// Runs the search, then the steps after it, then applies the result to the document, all
        /// without asking the user anything. A search whose inputs have not changed since it last
        /// succeeded is not run again.
        /// </summary>
        private void StartRun()
        {
            _runRunning = true;
            RunSucceeded = null;
            btnNext.Enabled = false;
            btnCancel.Enabled = false;
            btnBack.Enabled = false;

            try
            {
                ImportFastaControl.UpdateDigestSettings();
                ImportPeptideSearch.DdaConverter = WorkflowType == Workflow.dia && ConverterSettingsControl.UseDiaUmpire
                    ? ConverterSettingsControl.GetDiaUmpireConverter()
                    : null;
                ImportPeptideSearch.PrepareSearch(Document, BuildPepSearchLibControl.DdaSearchDataSources, ImportFastaControl.FastaFile,
                    CreateConverter);
                _runSearchFingerprint = GetSearchFingerprint();
            }
            catch (Exception x)
            {
                RunFailed(x);
                return;
            }

            if (_runSearchFingerprint == _searchFingerprint && _searchResultFilenames != null && _searchResultFilenames.All(File.Exists))
            {
                SearchControl.AddLogLine(PeptideSearchResources.RunPeptideSearchDlg_StartRun_The_search_settings_and_files_have_not_changed_since_the_last_search__using_its_results_);
                RunAfterSearch();
                return;
            }

            _searchFingerprint = null;
            _searchResultFilenames = null;
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
            SearchControl.SearchFinished -= SearchControlSearchFinished;
            if (!success || AcceptSearchResults?.Invoke() == false)
            {
                RunFinished(false);
                return;
            }

            _searchFingerprint = _runSearchFingerprint;
            // Distinct, since some engines write one result file for all the spectrum files. Kept for a
            // rerun that skips the search: the next PrepareSearch resets the engine to the unconverted files.
            var searchEngine = ImportPeptideSearch.SearchEngine;
            _searchResultFilenames = searchEngine.SpectrumFileNames
                .Select(f => searchEngine.GetSearchResultFilepath(f)).Distinct().ToArray();
            RunAfterSearch();
        }

        /// <summary>
        /// Builds the library from the search results, then adds the proteins (or the detected
        /// features), on a background thread that reports into the run page.
        /// </summary>
        private void RunAfterSearch()
        {
            ImportPeptideSearch.SearchFilenames = _searchResultFilenames;
            ImportPeptideSearch.CutoffScore = SearchSettingsControl.CutoffScore;

            var pipeline = new RunPeptideSearchPipeline(ImportPeptideSearch, LibraryManager, new RunPeptideSearchPipeline.Options
            {
                IsFeatureDetection = IsFeatureDetectionWorkflow,
                CutoffScore = SearchSettingsControl.CutoffScore,
                IncludeAmbiguousMatches = BuildPepSearchLibControl.IncludeAmbiguousMatches,
                IrtStandard = BuildPepSearchLibControl.IrtStandards,
                CirtPeptideCount = BuildPepSearchLibControl.CirtPeptideCount,
                RecalibrateIrtStandard = BuildPepSearchLibControl.RecalibrateIrtStandard,
                FastaPath = ImportFastaControl.FastaFile,
                FastaTargetsPath = ImportFastaControl.FastaImportTargetsFile,
                InsertPath = ImportFastaControl.InsertPath,
                QValueFilter = ImportFastaControl.QValueFilterControls.Filter,
                ParsimonySettings = ImportFastaControl.ParsimonySettings,
                DecoyGenerationMethod = ImportFastaControl.DecoyGenerationMethod,
                DecoysPerTarget = ImportFastaControl.NumDecoys ?? 0,
                AutoTrain = ImportFastaControl.AutoTrain,
                RequirePrecursorTransition = ImportFastaControl.RequirePrecursorTransition
            });
            var document = Document;
            var documentPath = DocumentFilePath;
            // Protein association regroups every document peptide
            if (!IsFeatureDetectionWorkflow && document.PeptideGroups.Any(nodeGroup => nodeGroup.IsProtein))
                SearchControl.AddLogLine(EditUIResources.AssociateProteinsDlg_OnShown_Existing_protein_associations_will_be_discarded_);
            RunPeptideSearchPipeline.Result result = null;
            SearchControl.RunAfterSearch((monitor, cancellationToken) => result = pipeline.Run(document, documentPath, monitor, cancellationToken),
                success =>
                {
                    if (!success)
                    {
                        pipeline.ReleaseLibrary();
                        RunFinished(false);
                        return;
                    }
                    try
                    {
                        ApplyResult(result);
                    }
                    catch (Exception x)
                    {
                        RunFailed(x);
                    }
                });
        }

        /// <summary>
        /// Commits the run's document to Skyline as one change and starts importing the results,
        /// then writes a summary of what was done into the run page's log.
        /// </summary>
        private void ApplyResult(RunPeptideSearchPipeline.Result result)
        {
            LastRunResult = result;
            if (IsFeatureDetectionWorkflow)
                Settings.Default.SpectralLibraryList.Add(result.LibrarySpec); // Won't displace the current document library
            else
                Settings.Default.SpectralLibraryList.Insert(0, result.LibrarySpec);

            var docNew = result.Document;
            if (result.RetentionTimePredictor != null)
                ImportPeptideSearch.SaveRetentionTimePredictor(result.RetentionTimePredictor);

            var proteinAssociation = result.ProteinAssociation?.ProteinAssociation;
            if (proteinAssociation != null)
            {
                ImportFastaControl.AssociateProteinsSettings = new AssociateProteinsSettings(proteinAssociation, null, null);
                if (ImportFastaControl.ParsimonySettings.GeneLevelParsimony)
                    Settings.Default.ShowPeptidesDisplayMode = ProteinMetadataManager.ProteinDisplayMode.ByGene.ToString();
            }

            SetDocument(docNew, Document);
            if (IsFeatureDetectionWorkflow && !result.NoFeaturesDetected)
            {
                // Update UI for new content type
                GetModeUIHelper().ModeUI = Document.HasPeptides
                    ? SrmDocument.DOCUMENT_TYPE.mixed
                    : SrmDocument.DOCUMENT_TYPE.small_molecules;
            }

            ImportResults(SkylineResources.SkylineWindow_ShowRunPeptideSearchDlg_Run_Peptide_Search, _namedResults);
            ImportPeptideSearch.SearchEngine.Dispose();

            AddSummary(result);
            RunFinished(true);
        }

        private void AddSummary(RunPeptideSearchPipeline.Result result)
        {
            if (!string.IsNullOrEmpty(result.AmbiguousMatchesMessage))
                SearchControl.AddLogLine(result.AmbiguousMatchesMessage);
            SearchControl.AddLogLine(string.Format(PeptideSearchResources.RunPeptideSearchDlg_AddSummary_Built_the_spectral_library__0__,
                result.LibrarySpec.Name));
            if (result.IrtStandardChosen)
            {
                SearchControl.AddLogLine(string.Format(PeptideSearchResources.RunPeptideSearchDlg_AddSummary_Several_known_iRT_standards_matched_the_library__used__0___which_has_the_most_peptides_,
                    result.IrtStandard.Name));
            }
            if (result.IrtPeptideCount > 0)
            {
                // A standard made from library peptides (CiRT, or Auto with no known standard) has no name to show
                if (Equals(result.IrtStandard.Name, XmlNamedElement.NAME_INTERNAL))
                {
                    SearchControl.AddLogLine(string.Format(PeptideSearchResources.RunPeptideSearchDlg_AddSummary_Calibrated__0__library_peptides_to__1__standard_peptides_chosen_from_the_library_,
                        result.IrtPeptideCount, result.IrtStandard.Peptides.Count));
                }
                else
                {
                    SearchControl.AddLogLine(string.Format(PeptideSearchResources.RunPeptideSearchDlg_AddSummary_Calibrated__0__library_peptides_to_the_iRT_standard__1__,
                        result.IrtPeptideCount, result.IrtStandard.Name));
                }
            }
            if (result.NoFeaturesDetected)
                SearchControl.AddLogLine(PeptideSearchResources.ImportPeptideSearchDlg_AddDetectedFeaturesToDocument_No_features_detected);

            var mappingResults = result.ProteinAssociation?.ProteinAssociation?.FinalResults;
            if (mappingResults != null)
            {
                SearchControl.AddLogLine(string.Format(PeptideSearchResources.RunPeptideSearchDlg_AddSummary_Proteins___0__mapped___1__unmapped___2__in_the_document_,
                    mappingResults.ProteinsMapped, mappingResults.ProteinsUnmapped, mappingResults.FinalProteinCount));
                SearchControl.AddLogLine(string.Format(PeptideSearchResources.RunPeptideSearchDlg_AddSummary_Peptides___0__mapped___1__unmapped___2__in_the_document_,
                    mappingResults.PeptidesMapped, mappingResults.PeptidesUnmapped, mappingResults.FinalPeptideCount));
            }

            var document = Document;
            // Explains a protein count below the one mapped above
            var documentCountsFormat = result.ProteinAssociation?.IrtStandardProteinsRemoved > 0
                ? PeptideSearchResources.RunPeptideSearchDlg_AddSummary_After_dropping_proteins_in_the_iRT_standard__the_document_has__0__proteins___1__peptides___2__precursors_and__3__transitions_
                : PeptideSearchResources.RunPeptideSearchDlg_AddSummary_The_document_has__0__proteins___1__peptides___2__precursors_and__3__transitions_;
            SearchControl.AddLogLine(string.Format(documentCountsFormat,
                document.MoleculeGroupCount, document.MoleculeCount, document.MoleculeTransitionGroupCount, document.MoleculeTransitionCount));
            SearchControl.AddLogLine(string.Format(PeptideSearchResources.RunPeptideSearchDlg_AddSummary_Extracting_chromatograms_from__0__files__Click_Finish_to_close_this_wizard_,
                _namedResults.Count));
        }

        private void RunFailed(Exception x)
        {
            SearchControl.AddLogLine(x.Message);
            ExceptionUtil.DisplayOrReportException(this, x);
            RunFinished(false);
        }

        private void RunFinished(bool success)
        {
            _runRunning = false;
            _runCompleted = success;
            RunSucceeded = success;
            btnNext.Enabled = success;
            // Once the document is applied there is nothing to go back to
            btnBack.Enabled = !success;
            btnCancel.Enabled = !success;
            UpdateButtons();
        }

        protected override void Finish()
        {
            // The run has already applied its document to Skyline
            CloseWizard(DialogResult.OK);
        }

        /// <summary>
        /// Everything the search reads, so a search can be skipped when the user runs again with the
        /// same inputs (e.g. after going back to change the chromatogram extraction settings).
        /// </summary>
        private string GetSearchFingerprint()
        {
            var settings = Document.Settings.PeptideSettings;
            var fastaFile = ImportFastaControl.FastaFile;
            var parts = new List<object>
            {
                WorkflowType,
                string.Join(@",", BuildPepSearchLibControl.DdaSearchDataSources.Select(f => f.ToString())),
                fastaFile,
                File.Exists(fastaFile) ? File.GetLastWriteTimeUtc(fastaFile).Ticks : 0,
                settings.Enzyme.Name,
                settings.DigestSettings.MaxMissedCleavages,
                string.Join(@",", settings.Modifications.StaticModifications.Concat(settings.Modifications.AllHeavyModifications)
                    .Select(mod => mod.Name)),
                ConverterSettingsControl.UseDiaUmpire,
                ConverterSettingsControl.IsGpf,
            };
            if (ConverterSettingsControl.UseDiaUmpire)
            {
                parts.AddRange(new object[]
                {
                    ConverterSettingsControl.InstrumentPreset, ConverterSettingsControl.EstimateBackground,
                    SearchSettingsPreset.SerializeAdditionalSettings(ConverterSettingsControl.AdditionalSettings)
                });
            }
            if (IsFeatureDetectionWorkflow)
            {
                var hardklor = ImportPeptideSearch.SettingsHardklor;
                parts.AddRange(new object[]
                {
                    hardklor.Instrument, hardklor.Resolution, hardklor.MinIdotP, hardklor.SignalToNoise,
                    string.Join(@",", hardklor.Charges), hardklor.MinIntensityPPM, hardklor.RetentionTimeTolerance
                });
            }
            else
            {
                parts.AddRange(new object[]
                {
                    SearchSettingsControl.SelectedSearchEngine,
                    SearchSettingsControl.PrecursorTolerance, SearchSettingsControl.FragmentTolerance,
                    SearchSettingsControl.FragmentIons, SearchSettingsControl.Ms2Analyzer,
                    SearchSettingsControl.MaxVariableMods, SearchSettingsControl.CutoffScore,
                    SearchSettingsPreset.SerializeAdditionalSettings(ImportPeptideSearch.SearchEngine?.AdditionalSettings)
                });
            }
            return string.Join(@"|", parts.Select(p => Convert.ToString(p, CultureInfo.InvariantCulture)));
        }

        #endregion

        protected override void UpdateButtons()
        {
            base.UpdateButtons();

            if (NextPageInWorkflow == Pages.dda_search_page)
                btnNext.Text = PeptideSearchResources.RunPeptideSearchDlg_UpdateButtons_Run;

            // Enable save button only once search settings have been configured
            if (btnSavePreset.Visible)
                btnSavePreset.Enabled = IsAtOrAfterPage(Pages.dda_search_settings_page);
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
