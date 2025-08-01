﻿/*
 * Original author: Tahmina Jahan <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.BiblioSpec;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public interface IModifyDocumentContainer : IDocumentContainer
    {
        void ModifyDocumentNoUndo(Func<SrmDocument, SrmDocument> act);
        void ModifyDocument(string description, Func<SrmDocument, SrmDocument> act, Func<SrmDocumentPair, AuditLogEntry> logFunc);
    }

    public sealed partial class ImportPeptideSearchDlg : FormEx, IAuditLogModifier<ImportPeptideSearchDlg.ImportPeptideSearchSettings>, IMultipleViewProvider, IModifyDocumentContainer
    {
        public enum Pages
        {
            spectra_page,
            chromatograms_page,
            match_modifications_page,
            transition_settings_page,
            full_scan_settings_page,
            import_fasta_page,
            converter_settings_page,
            dda_search_settings_page,
            dda_search_page
        }

        public enum InputFile
        {
            search_result,
            dda_raw,
            dia_raw
        }

        public enum Workflow
        {
            dda,
            prm,
            dia,
            feature_detection
        }

        public class SpectraPage : IFormView { }
        public class ChromatogramsPage : IFormView { }
        public class ChromatogramsDiaPage : IFormView { }
        public class MatchModsPage : IFormView { }
        public class TransitionSettingsPage : IFormView { }
        public class Ms1FullScanPage : IFormView { }
        public class Ms2FullScanPage : IFormView { }
        public class ImsFullScanPage : IFormView { }
        public class FastaPage : IFormView { }
        public class ConverterSettingsPage : IFormView { }
        public class DDASearchSettingsPage : IFormView { }
        public class DDASearchPage : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new SpectraPage(), new ChromatogramsPage(), new MatchModsPage(), new TransitionSettingsPage(), new Ms1FullScanPage(), new FastaPage(), new ConverterSettingsPage(), new DDASearchSettingsPage(), new DDASearchPage()
        };

        private readonly Stack<SrmDocument> _documents;
        public bool IsAutomatedTest; // Testing support

        public ImportPeptideSearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager, bool isRunPeptideSearch, Workflow? workflowType, bool useExistingLibrary = false)
        {
            SkylineWindow = skylineWindow;
            _documents = new Stack<SrmDocument>();
            IsAutomatedTest = false;
            SetDocument(skylineWindow.Document, null);

            ImportPeptideSearch = new ImportPeptideSearch()
                { IsFeatureDetection = workflowType == Workflow.feature_detection };

            InitializeComponent();

            // UI mode may change the indexing of tab pages, set up a map to deal with that
            _tabPageNames = new Dictionary<Pages, string>();
            for (int i = 0; i < wizardPagesImportPeptideSearch.TabPages.Count; i++)
            {
                _tabPageNames[(Pages)i] = wizardPagesImportPeptideSearch.TabPages[i].Name;
            }

            Icon = Resources.Skyline;

            btnEarlyFinish.Location = btnBack.Location;

            CurrentPage = Pages.spectra_page;
            btnNext.Text = PeptideSearchResources.ImportPeptideSearchDlg_ImportPeptideSearchDlg_Next;
            AcceptButton = btnNext;
            btnNext.Enabled = HasUnmatchedLibraryRuns(Document);

            // Create and add wizard pages
            BuildPepSearchLibControl = new BuildPeptideSearchLibraryControl(this, ImportPeptideSearch, libraryManager, isRunPeptideSearch)
            {
                Dock = DockStyle.Fill,
            };
            BuildPepSearchLibControl.InputFilesChanged += BuildPepSearchLibForm_OnInputFilesChanged;

            buildLibraryPanel.Controls.Add(BuildPepSearchLibControl);

            ImportFastaControl = new ImportFastaControl(this, SkylineWindow.SequenceTree);
            AddPageControl(ImportFastaControl, importFastaPage, 2, 60);

            MatchModificationsControl = new MatchModificationsControl(this, ImportPeptideSearch);
            AddPageControl(MatchModificationsControl, matchModificationsPage, 2, 60);

            TransitionSettingsControl = new TransitionSettingsControl(this);
            AddPageControl(TransitionSettingsControl, transitionSettingsUiPage, 18, 60);

            MakeFullScanSettingsControl(workflowType);

            ImportResultsDDAControl = new ImportResultsControl(ImportPeptideSearch, DocumentFilePath);
            AddPageControl(ImportResultsDDAControl, getChromatogramsPage, 2, 60);
            ImportResultsControl = ImportResultsDDAControl;

            ConverterSettingsControl = new ConverterSettingsControl(this, ImportPeptideSearch, () => FullScanSettingsControl);
            AddPageControl(ConverterSettingsControl, converterSettingsPage, 18, 50);

            var isFeatureDetection = workflowType is Workflow.feature_detection;

            if (!useExistingLibrary)
            {
                SearchSettingsControl = new SearchSettingsControl(this, ImportPeptideSearch);
                AddPageControl(SearchSettingsControl, ddaSearchSettingsPage, 18, isFeatureDetection ? this.buildSpectralLibraryTitlePanel.Bottom : 50);
            }

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

            _pagesToSkip = new HashSet<Pages>();

            if (workflowType.HasValue)
            {
                if (isFeatureDetection)
                {
                    this.Text = PeptideSearchResources.ImportPeptideSearchDlg_ImportPeptideSearchDlg_Feature_Detection;
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

            if (isFeatureDetection || isRunPeptideSearch)
            {
                label14.Text = PeptideSearchResources.BuildPeptideSearchLibraryControl_btnAddFile_Click_Select_Files_to_Search; // Was "Spectral Library"
            }
        }

        private void MakeFullScanSettingsControl(Workflow? workflowType)
        {
            if (FullScanSettingsControl != null)
            {
                ms1FullScanSettingsPage.Controls.Remove(FullScanSettingsControl);
            }

            var isFeatureDetection = workflowType is Workflow.feature_detection;
            FullScanSettingsControl = new FullScanSettingsControl(this,
                isFeatureDetection ? ImportPeptideSearch.eFeatureDetectionPhase.fullscan_settings : ImportPeptideSearch.eFeatureDetectionPhase.none);
            AddPageControl(FullScanSettingsControl, ms1FullScanSettingsPage, isFeatureDetection ? 0 : 18, isFeatureDetection ? 43 : 50);

            FullScanSettingsControl.FullScanEnabledChanged += OnFullScanEnabledChanged; // Adjusts ion settings when full scan settings change
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

        private UserControl GetPageControl(TabPage tabPage)
        {
            // Assume each tabPage only has a single UserControl; if that changes this function will need to be updated
            return tabPage.Controls.OfType<UserControl>().SingleOrDefault();
        }

        public SrmDocument Document
        {
            get
            {
                if (_documents.Count == 0)
                    return null;
                return _documents.Peek();
            }
        }

        public string DocumentFilePath { get { return SkylineWindow.DocumentFilePath; } }
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

        public bool IsClosing { get { throw new NotImplementedException(); } }
        public IEnumerable<BackgroundLoader> BackgroundLoaders { get { throw new NotImplementedException(); } }
        public void AddBackgroundLoader(BackgroundLoader loader)
        {
            throw new NotImplementedException();
        }

        public void RemoveBackgroundLoader(BackgroundLoader loader)
        {
            throw new NotImplementedException();
        }

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

        public ImportPeptideSearchSettings FormSettings
        {
            get
            {
                var skippedTransitionPage = _pagesToSkip.Contains(Pages.transition_settings_page);
                SearchSettingsControl.DdaSearchSettings ddaSearchSettings;
                if (ImportPeptideSearch.IsDDASearch && !BuildPepSearchLibControl.UseExistingLibrary &&
                    !ImportPeptideSearch.IsFeatureDetection)
                {
                    ddaSearchSettings = SearchSettingsControl.SearchSettings;
                }
                else
                {
                    ddaSearchSettings = null;
                }
                return new ImportPeptideSearchSettings(
                    ImportResultsControl.ImportSettings,
                    MatchModificationsControl.ModificationSettings,
                    skippedTransitionPage ? null : TransitionSettingsControl.FilterAndLibrariesSettings,
                    FullScanSettingsControl.FullScan,
                    ImportFastaControl.ImportSettings,
                    ImportFastaControl.AssociateProteinsSettings,
                    ddaSearchSettings,
                    ConverterSettingsControl.ConverterSettings,
                    ImportPeptideSearch.SettingsHardklor,
                    ModeUI);
            }
        }

        public class ImportPeptideSearchSettings : AuditLogOperationSettings<ImportPeptideSearchSettings>, IAuditLogComparable
        {
            private SrmDocument.DOCUMENT_TYPE _docType;
            public override MessageInfo MessageInfo
            {
                get { return new MessageInfo(MessageType.imported_peptide_search, _docType); }
            }

            public ImportPeptideSearchSettings(
                ImportResultsSettings importResultsSettings,
                MatchModificationsControl.MatchModificationsSettings modificationsSettings,
                TransitionSettingsControl.TransitionFilterAndLibrariesSettings filterAndLibSettings,
                TransitionFullScan fullScanSettings,
                ImportFastaControl.ImportFastaSettings importFastaSettings,
                AssociateProteinsSettings associateProteinsSettings,
                SearchSettingsControl.DdaSearchSettings ddaSearchSettings,
                ConverterSettingsControl.DdaConverterSettings ddaConverterSettings,
                ImportPeptideSearch.HardklorSettings hardklorSearchSettings,
                SrmDocument.DOCUMENT_TYPE docType)
            {
                ImportResultsSettings = importResultsSettings;
                ModificationsSettings = modificationsSettings;
                FilterAndLibrariesSettings = filterAndLibSettings;
                FullScanSettings = fullScanSettings;
                ImportFastaSettings = importFastaSettings;
                AssociateProteinsSettings = associateProteinsSettings;
                DdaSearchSettings = ddaSearchSettings;
                DdaConverterSettings = ddaConverterSettings;
                HardklorSearchSettings = hardklorSearchSettings;
                _docType = docType;
            }

            // Extract Chromatograms
            [TrackChildren]
            public ImportResultsSettings ImportResultsSettings { get; private set; }

            // Add Modifications
            [TrackChildren]
            public MatchModificationsControl.MatchModificationsSettings ModificationsSettings { get; private set; }

            // Transition
            [TrackChildren(defaultValues:typeof(DefaultValuesNull))]
            public TransitionSettingsControl.TransitionFilterAndLibrariesSettings FilterAndLibrariesSettings { get; private set; }

            // Full scan
            [TrackChildren]
            public TransitionFullScan FullScanSettings { get; private set; }

            // Import FASTA
            [TrackChildren]
            public ImportFastaControl.ImportFastaSettings ImportFastaSettings { get; private set; }

            // Associate proteins
            [TrackChildren(defaultValues:typeof(DefaultValuesNull))]
            public AssociateProteinsSettings AssociateProteinsSettings { get; private set; }

            // DDA search settings
            [TrackChildren]
            public SearchSettingsControl.DdaSearchSettings DdaSearchSettings { get; private set; }

            // DDA converter settings
            [TrackChildren]
            public ConverterSettingsControl.DdaConverterSettings DdaConverterSettings { get; private set; }

            // Hardklor settings
            [TrackChildren]
            public ImportPeptideSearch.HardklorSettings HardklorSearchSettings { get; private set; }

            public object GetDefaultObject(ObjectInfo<object> info)
            {
                var doc = info.OldRootObject as SrmDocument;
                if (doc == null)
                    return null;

                return new ImportPeptideSearchSettings(
                    ImportResultsSettings.DEFAULT,
                    MatchModificationsControl.MatchModificationsSettings.DEFAULT,
                    TransitionSettingsControl.TransitionFilterAndLibrariesSettings.GetDefault(doc.Settings.TransitionSettings),
                    doc.Settings.TransitionSettings.FullScan,
                    ImportFastaControl.ImportFastaSettings.GetDefault(doc.Settings.PeptideSettings),
                    AssociateProteinsSettings.DEFAULT,
                    null,
                    null,
                    null,
                    SrmDocument.DOCUMENT_TYPE.proteomic);
            }
        }

        public ImportPeptideSearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager, Workflow workflowType,
            IList<ImportPeptideSearch.FoundResultsFile> resultFiles, ImportFastaControl.ImportFastaSettings fastaSettings,
            IEnumerable<string> existingLibraryFilepaths)
            : this(skylineWindow, libraryManager, false, workflowType, true)
        {
            BuildPepSearchLibControl.ForceWorkflow(workflowType);

            BuildPepSearchLibControl.UseExistingLibrary = true;
            BuildPepSearchLibControl.ExistingLibraryPath = existingLibraryFilepaths.First();

            ImportFastaControl.SetFastaContent(fastaSettings.FastaFile.Path, true);
            ImportFastaControl.Enzyme = fastaSettings.Enzyme;
            ImportFastaControl.MaxMissedCleavages = fastaSettings.MaxMissedCleavages;

            NextPage(); // skip use existing library page

            ImportResultsControl.FoundResultsFiles = resultFiles;

            _pagesToSkip.Add(Pages.match_modifications_page);
        }

        private bool _heightAdjusted;
        public void AdjustHeightForFullScanSettings()
        {
            if (IsDdaWorkflow || _heightAdjusted)
                return;

            var tab = Controls.OfType<WizardPages>().First().TabPages[(int) Pages.full_scan_settings_page];
            var panel = tab.Controls.OfType<Control>().OrderBy(c => c.Top).First(); // Location of panel containing the FullScan control
            var tabHeight = tab.Height;
            var marginBottom = FullScanSettingsControl.Top - panel.Bottom;
            var neededHeight = FullScanSettingsControl.Bottom + marginBottom;
            var change = neededHeight - tabHeight; // May need more real estate to ask about using ion mobility data, or less if no MS2 settings

            MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height + change);
            if (change < 0)
            {
                Height += change;
            }
            _heightAdjusted = true;
        }

        private SkylineWindow SkylineWindow { get; set; }
        private ImportPeptideSearch ImportPeptideSearch { get; set; }
        public TransitionSettings TransitionSettings { get { return Document.Settings.TransitionSettings; } }
        public TransitionFullScan FullScan { get { return TransitionSettings.FullScan; } }

        private bool _modificationSettingsChanged;
        private bool _transitionSettingsChanged;
        private bool _fullScanSettingsChanged;
        private bool _expandedDdaSearchLog;
        private PeptideLibraries _existingLibraries;

        public bool HasPeakBoundaries { get; private set; }

        public BuildPeptideSearchLibraryControl BuildPepSearchLibControl { get; private set; }
        public ImportFastaControl ImportFastaControl { get; private set; }
        public TransitionSettingsControl TransitionSettingsControl { get; private set; }
        public FullScanSettingsControl FullScanSettingsControl { get; private set; }
        public IImportResultsControl ImportResultsControl { get; private set; }
        public MatchModificationsControl MatchModificationsControl { get; private set; }
        public ConverterSettingsControl ConverterSettingsControl { get; private set; }
        public SearchSettingsControl SearchSettingsControl { get; private set; }
        public SearchControl SearchControl { get; private set; }

        public ImportResultsControl ImportResultsDDAControl { get; private set; }
        public ImportResultsDIAControl ImportResultsDIAControl { get; private set; }


        public Workflow WorkflowType
        {
            get { return BuildPepSearchLibControl.WorkflowType; }
        }

        public bool IsImportingSearchResults => InputFileType == InputFile.search_result;
        public bool IsDdaWorkflow => WorkflowType == Workflow.dda || WorkflowType == Workflow.feature_detection;
        public bool IsFeatureDetectionWorkflow => WorkflowType == Workflow.feature_detection;

        public InputFile InputFileType => BuildPepSearchLibControl.InputFileType;

        private bool FastaOptional
        {
            get { return (!BuildPepSearchLibControl.FilterForDocumentPeptides && Document.PeptideCount > 0) &&
                         !BuildPepSearchLibControl.PerformDDASearch; }
        }

        private bool GetAreLibrarySpectraDIA()
        {
            if (!IsImportingSearchResults)
                return WorkflowType == Workflow.dia;

            var libraryFiles = ImportPeptideSearch.DocLib.LibraryDetails.DataFiles;
            return libraryFiles.All(d => d.WorkflowType == Model.Lib.WorkflowType.DIA);
        }

        private Pages LastPage
        {
            get
            {
                int lastPage = (int)(Enum.GetValues(typeof(Pages)).Cast<Pages>().Max()); 
                for (; lastPage >= (int) Pages.match_modifications_page; lastPage--)
                {
                    if (!_pagesToSkip.Contains((Pages) lastPage))
                        break;
                }
                return (Pages) lastPage;
            }
        }

        private readonly HashSet<Pages> _pagesToSkip;

        private static bool HasUnmatchedLibraryRuns(SrmDocument doc)
        {
            var libraries = doc.Settings.PeptideSettings.Libraries;
            if (!libraries.HasLibraries || !libraries.HasDocumentLibrary)
                return false;
            for (int i = 0; i < libraries.LibrarySpecs.Count; i++)
            {
                if (libraries.LibrarySpecs[i].IsDocumentLibrary)
                {
                    var documentLibrary = libraries.Libraries[i];
                    // CONSIDER: Load the library?
                    if (documentLibrary == null)
                        return false;
                    foreach (var dataFile in documentLibrary.LibraryFiles.FilePaths)
                    {
                        if (!doc.Settings.HasResults ||
                                doc.Settings.MeasuredResults.FindMatchingMSDataFile(MsDataFileUri.Parse(dataFile)) == null)
                            return true;
                    }
                }
            }
            return false;
        }

        private Dictionary<Pages, string> _tabPageNames; // In small mol UI mode, some pages go away and indexing is not straightforward
        public Pages CurrentPage
        {
            get
            {
                var index = wizardPagesImportPeptideSearch.SelectedIndex;
                var tabName = wizardPagesImportPeptideSearch.TabPages[index].Name;
                return _tabPageNames.FirstOrDefault(x => Equals(x.Value, tabName)).Key;
            }
            private set
            {
                var tabName = _tabPageNames[value];
                var index = wizardPagesImportPeptideSearch.TabPages.IndexOfKey(tabName);
                wizardPagesImportPeptideSearch.SelectedIndex = Math.Min(index, wizardPagesImportPeptideSearch.TabCount); 
            }
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            NextPage();
        }

        private void NextPage()
        {
            switch (CurrentPage)
            {
                case Pages.spectra_page:
                {
                    _pagesToSkip.Clear();
                    ImportPeptideSearch.IsDDASearch = BuildPepSearchLibControl.PerformDDASearch && !IsFeatureDetectionWorkflow;
                    ImportPeptideSearch.IsDIASearch = BuildPepSearchLibControl.PerformDDASearch && WorkflowType == Workflow.dia;
                    ImportFastaControl.IsDDASearch = BuildPepSearchLibControl.PerformDDASearch && !IsFeatureDetectionWorkflow;
                    if (!BuildPepSearchLibControl.UseExistingLibrary)
                    {
                        if (!BuildPepSearchLibControl.PerformDDASearch)
                        {
                            HasPeakBoundaries = BuildPepSearchLibControl.SearchFilenames.All(f =>
                                f.EndsWith(BiblioSpecLiteBuilder.EXT_TSV));
                            if (BuildPepSearchLibControl.SearchFilenames.Any(f =>
                                    f.EndsWith(BiblioSpecLiteBuilder.EXT_TSV)) && !HasPeakBoundaries)
                            {
                                MessageDlg.Show(this,
                                    PeptideSearchResources
                                        .ImportPeptideSearchDlg_NextPage_Cannot_build_library_from_OpenSWATH_results_mixed_with_results_from_other_tools_);
                                return;
                            }
                        }
                    }

                    var eCancel = new CancelEventArgs();
                    if (!BuildPepSearchLibControl.PerformDDASearch && !BuildPeptideSearchLibrary(eCancel, IsFeatureDetectionWorkflow))
                    {
                        if (eCancel.Cancel)
                        {
                            // Page has shown an error and canceled further progress
                            return;
                        }
                        // A failure has occurred
                        // CONSIDER(brendanx): This looks suspicious to me. It closes the entire wizard
                        // when BuildPeptideSearchLibrary has failed but eCancel.Cancel is not set, which
                        // makes it unclear if the user has seen an error message before the UI disappears.
                        // I am not ready to dig into this further as it has been this way for years,
                        // passing most tests. It is hard to imagine how the wizard could continue if
                        // it fails to create a library, and it is not performing a search.
                        CloseWizard(DialogResult.Cancel);
                        // Not a good idea to continue once the wizard has been closed
                        return;
                    }

                    // The user had the option to finish right after 
                    // building the peptide search library, but they
                    // did not, so hide the "early finish" button for
                    // the rest of the wizard pages.
                    ShowEarlyFinish(false);

                    lblFasta.Text = FastaOptional
                        ? PeptideSearchResources.ImportPeptideSearchDlg_NextPage_Import_FASTA__optional_
                        : PeptideSearchResources.ImportPeptideSearchDlg_NextPage_Import_FASTA__required_;

                    // The next page is going to be the chromatograms page.
                    var oldImportResultsControl = (Control) ImportResultsControl;
                    getChromatogramsPage.Controls.Remove(oldImportResultsControl);

                    if (WorkflowType != Workflow.dia || HasPeakBoundaries || (IsImportingSearchResults && GetAreLibrarySpectraDIA()))
                    {
                        if (!(ImportResultsControl is ImportResultsControl))
                        {
                            ImportResultsControl = new ImportResultsControl(ImportPeptideSearch, DocumentFilePath)
                            {
                                Anchor = oldImportResultsControl.Anchor,
                                Location = oldImportResultsControl.Location,
                                Size = oldImportResultsControl.Size
                            };
                        }

                        ((ImportResultsControl) ImportResultsControl).InitializeChromatogramsPage(Document);
                        if (IsDdaWorkflow)
                        {
                            _pagesToSkip.Add(Pages.transition_settings_page);
                        }

                        if (IsFeatureDetectionWorkflow)
                        {
                            _pagesToSkip.Add(Pages.match_modifications_page);
                            _pagesToSkip.Add(Pages.import_fasta_page);
                        }
                    }
                    else
                    {
                        // DIA workflow, replace old ImportResultsControl
                        if (!(ImportResultsControl is ImportResultsDIAControl))
                        {
                            ImportResultsDIAControl = new ImportResultsDIAControl(this)
                            {
                                Anchor = oldImportResultsControl.Anchor,
                                Location = oldImportResultsControl.Location,
                                Size = oldImportResultsControl.Size
                            };
                            ImportResultsControl = ImportResultsDIAControl;
                        }

                        if (BuildPepSearchLibControl.PerformDDASearch)
                            ImportResultsDIAControl.FoundResultsFiles = BuildPepSearchLibControl.DdaSearchDataSources.Select(o =>
                                new ImportPeptideSearch.FoundResultsFile(o.GetFileName(), o.GetFilePath())).ToList();
                    }
                    getChromatogramsPage.Controls.Add((Control)ImportResultsControl);

                    TransitionSettingsControl.Initialize(WorkflowType);

                    if (!BuildPepSearchLibControl.PerformDDASearch)
                    {
                        ImportResultsControl.ResultsFilesChanged += ImportResultsControl_OnResultsFilesChanged;
                    }
                    else
                    {
                        if (!IsImportingSearchResults && WorkflowType != Workflow.dia)
                        {
                            _pagesToSkip.Add(Pages.chromatograms_page);
                            _pagesToSkip.Add(Pages.converter_settings_page);
                        }

                        //ImportResultsDIAControl.HideFileAddRemoveButtons = true;

                        ImportPeptideSearch.SpectrumSourceFiles.Clear();

                        // in PerformDDA mode, set SpectrumSourceFiles and offer to remove prefix
                        var uniqueNames = Helpers.EnsureUniqueNames(BuildPepSearchLibControl.DdaSearchDataSources.Select(s => s.GetFileName()).ToList());
                        for (var i = 0; i < BuildPepSearchLibControl.DdaSearchDataSources.Length; i++)
                        {
                            var source = BuildPepSearchLibControl.DdaSearchDataSources[i];
                            ImportPeptideSearch.SpectrumSourceFiles.Add(uniqueNames[i],
                                    new ImportPeptideSearch.FoundResultsFilePossibilities(uniqueNames[i]) {ExactMatch = source.ToString()});
                        }

                        if (IsDdaWorkflow)
                            ShowRemovePrefixDialog();
                    }

                    // Set up full scan settings page
                    var lib = BuildPepSearchLibControl.ImportPeptideSearch.DocLib;
                    var libIonMobilities = lib != null && PeptideLibraries.HasIonMobilities(lib, null);
                    FullScanSettingsControl.ModifyOptionsForImportPeptideSearchWizard(WorkflowType, libIonMobilities);
                    AdjustHeightForFullScanSettings();

                    bool hasMatchedMods = MatchModificationsControl.Initialize(Document);
                        if (BuildPepSearchLibControl.FilterForDocumentPeptides && !BuildPepSearchLibControl.PerformDDASearch)
                        _pagesToSkip.Add(Pages.import_fasta_page);
                    if (!BuildPepSearchLibControl.PerformDDASearch)
                    {
                        _pagesToSkip.Add(Pages.converter_settings_page);
                        _pagesToSkip.Add(Pages.dda_search_page);
                        _pagesToSkip.Add(Pages.dda_search_settings_page);
                    }

                    // Decoy options enabled only for DIA
                    ImportFastaControl.RequirePrecursorTransition = WorkflowType != Workflow.dia;
                    ImportFastaControl.DecoyGenerationEnabled = WorkflowType == Workflow.dia && !HasPeakBoundaries;
                }
                    break;

                case Pages.chromatograms_page:
                {
                    if (IsImportingSearchResults && !ImportPeptideSearch.VerifyRetentionTimes(ImportResultsControl.FoundResultsFiles.Select(f => f.Path)))
                    {
                        MessageDlg.Show(this, TextUtil.LineSeparate(Resources.ImportPeptideSearchDlg_NextPage_The_document_specific_spectral_library_does_not_have_valid_retention_times_,
                                Resources.ImportPeptideSearchDlg_NextPage_Please_check_your_peptide_search_pipeline_or_contact_Skyline_support_to_ensure_retention_times_appear_in_your_spectral_libraries_));
                        CloseWizard(DialogResult.Cancel);
                    }
                    var anyResults = ImportResultsControl.FoundResultsFiles.Any();
                    if (!anyResults)
                    {
                        using (var dlg = new MultiButtonMsgDlg(
                            PeptideSearchResources.ImportPeptideSearchDlg_NextPage_No_results_files_were_specified__Are_you_sure_you_want_to_continue__Continuing_will_create_a_template_document_with_no_imported_results_,
                            MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                        {
                            if (dlg.ShowDialog(this) == DialogResult.No)
                            {
                                return;
                            }
                        }
                    }
                    else if (ImportResultsControl.ResultsFilesMissing)
                    {
                        using (var dlg = new MultiButtonMsgDlg(
                            PeptideSearchResources.ImportPeptideSearchDlg_NextPage_Some_results_files_are_still_missing__Are_you_sure_you_want_to_continue_,
                            MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                        {
                            if (dlg.ShowDialog(this) == DialogResult.No)
                            {
                                return;
                            }
                        }
                    }

                    if (ImportResultsControl is ImportResultsDIAControl diaControl)
                    {
                        ImportPeptideSearch.IsGpfData = diaControl.IsGpf;
                    }

                    ShowRemovePrefixDialog();
                    ImportFastaControl.IsImportingResults = anyResults;

                    if (ImportFastaControl.DecoyGenerationEnabled && WorkflowType != Workflow.dia)
                    {
                        if (anyResults)
                        {
                            ImportFastaControl.DecoyGenerationMethod = DecoyGeneration.SHUFFLE_SEQUENCE;
                            ImportFastaControl.NumDecoys = 1;
                        }
                        else
                        {
                            // template document, default to not generating decoys
                            ImportFastaControl.DecoyGenerationMethod = string.Empty;
                            ImportFastaControl.NumDecoys = 0;
                        }
                    }
                }
                    break;

                case Pages.match_modifications_page:
                    if (!UpdateModificationSettings())
                    {
                        return;
                    }
                    break;

                case Pages.transition_settings_page:
                    // Try to accept changes to transition settings
                    if (!UpdateTransitionSettings())
                    {
                        return;
                    }
                    if (!TransitionSettings.Filter.PeptideIonTypes.Contains(IonType.precursor))
                    {
                        FullScanSettingsControl.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.None;
                    }
                    break;

                case Pages.full_scan_settings_page:
                    // Try to accept changes to MS1 full-scan settings
                    if (!UpdateFullScanSettings())
                    {
                        // We can't allow the user to progress any further until
                        // we can verify that the MS1 full scan settings are valid.
                        return;
                    }
                    SearchSettingsControl?.UpdateControls(); // Feature Finding controls depend on FullScan settings
                    break;

                case Pages.import_fasta_page: // This is the last page (if there is no dda search)
                    if (ImportPeptideSearch.IsDDASearch || ImportPeptideSearch.IsDIASearch)
                    {
                        ImportPeptideSearch.CutoffScore = SearchSettingsControl.CutoffScore;

                        if (!File.Exists(ImportFastaControl.FastaFile))
                        {
                            MessageDlg.Show(this, PeptideSearchResources.ImportPeptideSearchDlg_NextPage_FastFileMissing_DDASearch);
                            return;
                        }

                        if (ImportPeptideSearch.IsDIASearch)
                            ConverterSettingsControl.InitializeProtocol(ConverterSettingsControl.Protocol.dia_umpire);
                        break;
                    }
                    else
                    {
                        if (FastaOptional && !ImportFastaControl.ContainsFastaContent || ImportFastaControl.ImportFasta(ImportPeptideSearch.IrtStandard))
                        {
                            WizardFinish();
                        }
                        return;
                    }
                case Pages.converter_settings_page:
                    if (WorkflowType == Workflow.dia)
                    {
                        if (ConverterSettingsControl.UseDiaUmpire)
                        {
                            if (FullScanSettingsControl.IsolationScheme.PrespecifiedIsolationWindows.Count == 0)
                            {
                                MessageDlg.Show(this, PeptideSearchResources.ImportPeptideSearchDlg_NextPage_No_isolation_windows_are_configured__);
                                return;
                            }
                            ImportPeptideSearch.DdaConverter = ConverterSettingsControl.GetDiaUmpireConverter();
                        }

                        ImportPeptideSearch.IsDIASearch = !ConverterSettingsControl.UseDiaUmpire;
                        SearchSettingsControl.InitializeControls();
                    }
                    break;
                case Pages.dda_search_settings_page:
                    bool valid = SearchSettingsControl.SaveAllSettings(!IsAutomatedTest);
                    if (!valid) return;
                    InitiateSearch();
                    break;

                case Pages.dda_search_page: // this is really the last page
                    var eCancel2 = new CancelEventArgs();
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

                    if (!BuildPeptideSearchLibrary(eCancel2, IsFeatureDetectionWorkflow))
                        return;

                    if (IsFeatureDetectionWorkflow)
                    {
                        // Load detected features after search
                        using (var longWaitDlg = new LongWaitDlg())
                        {
                            longWaitDlg.Text = PeptideSearchResources.ImportPeptideSearchDlg_ImportPeptideSearchDlg_Feature_Detection;
                            longWaitDlg.Message = PeptideSearchResources.ImportPeptideSearchDlg_NextPage_Adding_detected_features_to_document;
                            longWaitDlg.PerformWork(this, 1000, AddDetectedFeaturesToDocument);
                        }
                    }
                    //load proteins after search
                    else if (!ImportFastaControl.ImportFasta(ImportPeptideSearch.IrtStandard))
                        return;

                    ImportPeptideSearch.SearchEngine.Dispose();

                    WizardFinish();
                    return;

            }

            var newPage = CurrentPage + 1;
            while (_pagesToSkip.Contains(newPage))
                ++newPage;

            // Skip import FASTA if user filters for document peptides
            if (newPage > Pages.import_fasta_page && !ImportPeptideSearch.IsDDASearch && !ImportPeptideSearch.IsFeatureDetection)
            {
                WizardFinish();
                return;
            }

            CurrentPage = newPage;
            UpdateButtons();
        }

        private void AddDetectedFeaturesToDocument(IProgressMonitor progressMonitor)
        {
            // Add the library molecules to the document, in natural sort order
            var status = new ProgressStatus(PeptideSearchResources.ImportPeptideSearchDlg_NextPage_Adding_detected_features_to_document);
            progressMonitor.UpdateProgress(status);
            Assume.AreNotEqual(Document.Settings.PeptideSettings.Libraries.LibrarySpecs, _existingLibraries.LibrarySpecs);
            var docNew = Document;
            foreach (var lib in Document.Settings.PeptideSettings.Libraries.Libraries.Where(l =>
                         !_existingLibraries.Libraries.Contains(l)))
            {
                var adducts = new HashSet<Adduct>(Document.Settings.TransitionSettings.Filter.SmallMoleculeFragmentAdducts);
                if (lib != null)
                {
                    var nodes = new List<PeptideDocNode>();
                    var keyCount = lib.Keys.Count();
                    var keys = lib.Keys.OrderBy(k => ViewLibraryPepInfoList.MakeCompareKey(new ViewLibraryPepInfo(k))).ToArray();
                    for (var k = 0; k < keyCount;)
                    {
                        var key = keys[k];
                        var customMolecule = CustomMolecule.FromSmallMoleculeLibraryAttributes(key.SmallMoleculeLibraryAttributes);
                        var mass = customMolecule.GetMass(MassType.Monoisotopic);
                        var peptide = new Peptide(customMolecule);
                        var children = new List<TransitionGroupDocNode>();
                        // Look for multiple charges of same molecule
                        while (k < keyCount && Equals(key.SmallMoleculeLibraryAttributes, keys[k].SmallMoleculeLibraryAttributes))
                        {
                            key = keys[k];
                            progressMonitor.UpdateProgress(status.ChangePercentComplete(80 * (k++ / keyCount)));
                            // Filter on user-requested charge state
                            if (!TransitionSettings.Filter.PeptidePrecursorCharges.Any(charge => Equals(charge.AdductCharge, key.Adduct.AdductCharge)))
                            {
                                continue;
                            }
                            Assume.IsTrue(lib.TryLoadSpectrum(key, out SpectrumPeaksInfo spectrum));
                            var precursor = new TransitionGroup(peptide, key.Adduct, IsotopeLabelType.light);
                            var precursorTransition = new Transition(precursor, key.Adduct, null,
                                customMolecule, IonType.precursor);
                            var precursorTransitionDocNode = new TransitionDocNode(precursorTransition,
                                Annotations.EMPTY, null, mass,
                                TransitionDocNode.TransitionQuantInfo.DEFAULT, null,
                                null);
                            lib.TryGetLibInfo(key, out var libInfo);
                            var precursorDocNode = new TransitionGroupDocNode(precursor, Annotations.EMPTY,
                                Document.Settings, null, libInfo, null, null,
                                new[] { precursorTransitionDocNode },
                                false); // We will turn on autoManage in the next step
                            children.Add(precursorDocNode);
                        }
                        if (children.Any())
                        {
                            var peptideDocNode = new PeptideDocNode(peptide, Document.Settings, null, null,
                                null,
                                children.ToArray(), false); // We will turn on autoManage in the next step
                            nodes.Add(peptideDocNode);
                            adducts.Add(key.Adduct);
                        }
                    }

                    var newPeptideGroup = new PeptideGroup();
                    var newPeptideGroupDocNode = new PeptideGroupDocNode(newPeptideGroup,
                        Annotations.EMPTY,
                        lib.Name,
                        null, nodes.ToArray(), true);

                    // Make sure that transition settings filters include any newly used adduct types, and both precursor and fragment ions
                    var transitionSettings = Document.Settings.TransitionSettings;
                    var filter = transitionSettings.Filter
                        .ChangeSmallMoleculeFragmentAdducts(adducts.ToList())
                        .ChangeSmallMoleculePrecursorAdducts(adducts.ToList())
                        .ChangeSmallMoleculeIonTypes(new[] { IonType.custom, IonType.precursor });
                    var newTransitionSettings = transitionSettings.ChangeFilter(filter);
                    progressMonitor.UpdateProgress(status.ChangePercentComplete(90));

                    // Add new nodes with auto select enabled, being careful not to mess with any existing nodes
                    var docTmp = (SrmDocument)(Document.ChangeChildren(new List<DocNode>(){newPeptideGroupDocNode})); // Temp doc with just the new nodes
                    docTmp = docTmp.ChangeSettings(docTmp.Settings.ChangeTransitionSettings(newTransitionSettings)); // Update the settings
                    // Now apply auto pick so that M+1, M+2 etc precursors get created 
                    docTmp = ImportPeptideSearch.ChangeAutoManageChildren(docTmp, PickLevel.precursors | PickLevel.transitions, true);

                    // Copy the resulting nodes to the actual doc, and update its settings too
                    docNew = ((SrmDocument)docNew.Add(docTmp.MoleculeGroups.First())).
                        ChangeSettings(docTmp.Settings.ChangeTransitionSettings(newTransitionSettings));
                }

                progressMonitor.UpdateProgress(status.ChangePercentComplete(100));
                SetDocument(docNew, Document);

                // Update UI for new content type
                GetModeUIHelper().ModeUI = Document.HasPeptides
                    ? SrmDocument.DOCUMENT_TYPE.mixed
                    : SrmDocument.DOCUMENT_TYPE.small_molecules;
            }
        }

        private void InitiateSearch()
        {
            ImportFastaControl.UpdateDigestSettings();
            ImportPeptideSearch.SearchEngine.SetEnzyme(Document.Settings.PeptideSettings.Enzyme,
                Document.Settings.PeptideSettings.DigestSettings.MaxMissedCleavages);
            ImportPeptideSearch.SearchEngine.SetSpectrumFiles(BuildPepSearchLibControl.DdaSearchDataSources);
            ImportPeptideSearch.DdaConverter?.SetSpectrumFiles(BuildPepSearchLibControl.DdaSearchDataSources);
            ImportPeptideSearch.SearchEngine.SetFastaFiles(ImportFastaControl.FastaFile);
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

            AbstractDdaConverter.MsdataFileFormat requiredFormat =
                IsFeatureDetectionWorkflow
                    ? AbstractDdaConverter.MsdataFileFormat.mzML // Hardklor reads only mzML
                    : AbstractDdaConverter.MsdataFileFormat.mz5;
            if (ImportPeptideSearch.DdaConverter == null &&
                (BuildPepSearchLibControl.DdaSearchDataSources.Any(f => ImportPeptideSearch.SearchEngine.GetSearchFileNeedsConversion(f, out requiredFormat)))/* ||
                !FullScan.SpectrumClassFilter.IsEmpty*/) // CONSIDER(MCC): consider spectrum filters in GetSearchFileNeedsConversion and apply them in Converter
            {
                if (IsFeatureDetectionWorkflow)
                    ImportPeptideSearch.DdaConverter = ConverterSettingsControl.GetHardklorConverter();
                else if (ImportPeptideSearch.IsDIASearch)
                    ImportPeptideSearch.DdaConverter = ConverterSettingsControl.GetDiaConverter();
                else
                    ImportPeptideSearch.DdaConverter = ConverterSettingsControl.GetDdaConverter();
                ImportPeptideSearch.DdaConverter.SetSpectrumFiles(BuildPepSearchLibControl.DdaSearchDataSources);
                ImportPeptideSearch.DdaConverter.SetRequiredOutputFormat(requiredFormat);
            }
            else if (ImportPeptideSearch.DdaConverter != null &&
                     ImportPeptideSearch.DdaConverter.ConvertedSpectrumSources.Any(f => ImportPeptideSearch.SearchEngine.GetSearchFileNeedsConversion(f, out requiredFormat)))
            {
                ImportPeptideSearch.DdaConverter.SetRequiredOutputFormat(requiredFormat);
            }

            if (!_expandedDdaSearchLog)
            {
                // No longer necessary after widening the form for new library build grid
                // Width = Math.Min(Screen.FromControl(this).WorkingArea.Width, (int) (Width * 1.0)); // give more space for search log
                _expandedDdaSearchLog = true;
            }

            SearchControl.RunSearch();
        }

        private void ShowRemovePrefixDialog()
        {
            var foundResults = ImportResultsControl.FoundResultsFiles;
            if (foundResults.Count <= 1)
                return;

            // Older Resharper code inspection implementations insist on warning here
            // Resharper disable PossibleMultipleEnumeration
            string[] resultNames = foundResults.Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToArray();
            string prefix = ImportResultsDlg.GetCommonPrefix(resultNames);
            string suffix = ImportResultsDlg.GetCommonSuffix(resultNames);
            // Resharper restore PossibleMultipleEnumeration
            if (!string.IsNullOrEmpty(prefix) || !string.IsNullOrEmpty(suffix))
            {
                using (var dlgName = new ImportResultsNameDlg(prefix, suffix, resultNames))
                {
                    var result = dlgName.ShowDialog(this);
                    if (result != DialogResult.Cancel && dlgName.IsRemove)
                    {
                        ImportResultsControl.FoundResultsFiles = ImportResultsControl.FoundResultsFiles.Select(f =>
                            new ImportPeptideSearch.FoundResultsFile(dlgName.ApplyNameChange(Path.GetFileNameWithoutExtension(f.Name)), f.Path)).ToList();

                        ImportResultsControl.Prefix =
                            string.IsNullOrEmpty(prefix) ? null : prefix;
                        ImportResultsControl.Suffix =
                            string.IsNullOrEmpty(suffix) ? null : suffix;
                    }
                }
            }
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
            btnCancel.Enabled = true;
            btnBack.Enabled = true;
            btnNext.Enabled = success;
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            PreviousPage();
        }

        private void PreviousPage()
        {
            var newPage = CurrentPage - 1;
            while (_pagesToSkip.Contains(newPage) && newPage > Pages.spectra_page)
            {
                --newPage;
            }
            CurrentPage = newPage;
            UpdateButtons();

            switch (CurrentPage)
            {
                default:
                    return;
                case Pages.spectra_page:
                    MakeFullScanSettingsControl(WorkflowType); // reset UI to default
                    break;
                case Pages.chromatograms_page:
                    // This page doesn't modify the document, no undo needed
                    break;
                case Pages.match_modifications_page:
                    if (_modificationSettingsChanged)
                    {
                        _documents.Pop();
                        _modificationSettingsChanged = false;
                    }
                    break;
                case Pages.transition_settings_page:
                    if (_transitionSettingsChanged)
                    {
                        _documents.Pop();
                        _transitionSettingsChanged = false;
                    }
                    break;
                case Pages.full_scan_settings_page:
                    if (_fullScanSettingsChanged)
                    {
                        _documents.Pop();
                        _fullScanSettingsChanged = false;
                    }
                    btnNext.Enabled = true;
                    break;
                case Pages.dda_search_settings_page:
                    btnNext.Enabled = true;
                    break;
                case Pages.dda_search_page:
                    SearchControl.SearchFinished -= SearchControlSearchFinished;
                    break;
            }
        }

        private void UpdateButtons()
        {
            if (IsImportingSearchResults && CurrentPage <= Pages.chromatograms_page)
            {
                btnBack.Hide();
                btnEarlyFinish.Location = btnBack.Location;
            }
            else if (CurrentPage == Pages.spectra_page) // No "back" from page zero
            {
                btnBack.Hide();
            }
            else if (!btnBack.Visible)
            {
                btnEarlyFinish.Left = btnBack.Left - btnBack.Width - 6;
                btnBack.Show();
            }

            btnNext.Text = CurrentPage != LastPage
                ? PeptideSearchResources.ImportPeptideSearchDlg_ImportPeptideSearchDlg_Next
                : PeptideSearchResources.ImportPeptideSearchDlg_NextPage_Finish;
        }

        private bool UpdateModificationSettings()
        {
            var newSettings = MatchModificationsControl.AddCheckedModifications(Document);
            if (ReferenceEquals(Document.Settings, newSettings))
                return true;

            ModifyDocumentNoUndo(doc => doc.ChangeSettings(newSettings));
            Document.Settings.UpdateDefaultModifications(true, true);
            _modificationSettingsChanged = true;
            return true;
        }

        private bool UpdateTransitionSettings()
        {
            TransitionSettings newTransitionSettings = TransitionSettingsControl.GetTransitionSettings(this);
            if (newTransitionSettings == null)
                return false;

            TransitionSettingsControl.SetFields(newTransitionSettings);

            // Only update, if anything changed
            Helpers.AssignIfEquals(ref newTransitionSettings, TransitionSettings);
            if (Equals(newTransitionSettings, TransitionSettings))
                return true;

            ModifyDocumentNoUndo(doc => doc.ChangeSettings(doc.Settings.ChangeTransitionSettings(newTransitionSettings)));
            _transitionSettingsChanged = true;
            return true;
        }

        private bool UpdateFullScanSettings()
        {
            var helper = new MessageBoxHelper(this);

            // Validate and store MS1 full-scan settings

            // If high resolution MS1 filtering is enabled, make sure precursor m/z type
            // is monoisotopic and isotope enrichments are set
            var precursorIsotopes = FullScanSettingsControl.PrecursorIsotopesCurrent;
            var precursorAnalyzerType = FullScanSettingsControl.PrecursorMassAnalyzer;
            var precursorMassType = TransitionSettings.Prediction.PrecursorMassType;
            if (precursorIsotopes == FullScanPrecursorIsotopes.None)
            {
                if (IsDdaWorkflow)
                {
                    MessageDlg.Show(this, PeptideSearchResources.ImportPeptideSearchDlg_UpdateFullScanSettings_Full_scan_MS1_filtering_must_be_enabled_in_order_to_import_a_peptide_search_);
                    return false;
                }
                else if (FullScanSettingsControl.AcquisitionMethod == FullScanAcquisitionMethod.None)
                {
                    MessageDlg.Show(this, PeptideSearchResources.ImportPeptideSearchDlg_UpdateFullScanSettings_Full_scan_MS1_or_MS_MS_filtering_must_be_enabled_in_order_to_import_a_peptide_search_);
                    return false;
                }
            }
            else if (precursorAnalyzerType != FullScanMassAnalyzerType.qit)
            {
                precursorMassType = MassType.Monoisotopic;
                if (FullScanSettingsControl.Enrichments == null)
                {
                    MessageDlg.Show(GetParentForm(this), Resources.TransitionSettingsUI_OkDialog_Isotope_enrichment_settings_are_required_for_MS1_filtering_on_high_resolution_mass_spectrometers);
                    return false;
                }
            }

            if (FullScanSettingsControl.IsolationScheme == null && FullScanSettingsControl.AcquisitionMethod == FullScanAcquisitionMethod.DIA)
            {
                MessageDlg.Show(this, Resources.TransitionSettingsUI_OkDialog_An_isolation_scheme_is_required_to_match_multiple_precursors);
                return false;
            }

            // FUTURE(bspratt): deal with small mol iontypes etc if we extend this to non=peptide searches

            TransitionFilter filter = TransitionSettings.Filter;
            if (FullScanSettingsControl.PrecursorChargesTextBox.Visible)
            {
                Adduct[] precursorCharges;
                if (!TransitionSettingsControl.ValidateAdductListTextBox(helper, FullScanSettingsControl.PrecursorChargesTextBox, true, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE, out precursorCharges))
                    return false;
                precursorCharges = precursorCharges.Distinct().ToArray();
                FullScanSettingsControl.PrecursorChargesString = TransitionFilter.AdductListToString(precursorCharges);
                filter = TransitionSettings.Filter.ChangePeptidePrecursorCharges(precursorCharges);
            }

            TransitionLibraries libraries = TransitionSettings.Libraries;
            if (IsDdaWorkflow)
            {
                if (FullScanSettingsControl.AcquisitionMethod == FullScanAcquisitionMethod.None)
                {
                    filter = filter.ChangePeptideIonTypes(new[] { IonType.precursor });
                    if (libraries.MinIonCount > 0)
                        libraries = libraries.ChangeMinIonCount(0); // Avoid filtering due to lack of product ions
                }
                else if (!filter.PeptideIonTypes.Contains(IonType.precursor))
                {
                    var listIonTypes = filter.PeptideIonTypes.ToList();
                    listIonTypes.Add(IonType.precursor);
                    filter = filter.ChangePeptideIonTypes(listIonTypes);
                }
            }
            if (!filter.AutoSelect)
                filter = filter.ChangeAutoSelect(true);
            Helpers.AssignIfEquals(ref filter, TransitionSettings.Filter);

            if (FullScanSettingsControl.IsDIA() && filter.ExclusionUseDIAWindow && FullScanSettingsControl.IsolationScheme != null)
            {
                if (FullScanSettingsControl.IsolationScheme.IsAllIons)
                {
                    MessageDlg.Show(this, Resources.TransitionSettingsUI_OkDialog_Cannot_use_DIA_window_for_precusor_exclusion_when__All_Ions__is_selected_as_the_isolation_scheme___To_use_the_DIA_window_for_precusor_exclusion__change_the_isolation_scheme_in_the_Full_Scan_settings_);
                    return false;
                }
                if (FullScanSettingsControl.IsolationScheme.FromResults)
                {
                    MessageDlg.Show(this, Resources.TransitionSettingsUI_OkDialog_Cannot_use_DIA_window_for_precursor_exclusion_when_isolation_scheme_does_not_contain_prespecified_windows___Please_select_an_isolation_scheme_with_prespecified_windows_);
                    return false;
                }
            }

            TransitionFullScan fullScan;
            if (!FullScanSettingsControl.ValidateFullScanSettings(helper, out fullScan))
                return false;

            fullScan = fullScan.ChangeSpectrumFilter(TransitionSettingsControl.SpectrumFilter);

            Helpers.AssignIfEquals(ref fullScan, TransitionSettings.FullScan);

            var prediction = TransitionSettings.Prediction.ChangePrecursorMassType(precursorMassType);
            Helpers.AssignIfEquals(ref prediction, TransitionSettings.Prediction);

            // Did user change the "use spectral library ion mobility" values?
            var ionMobilityFilteringOriginal = Document.Settings.TransitionSettings.IonMobilityFiltering;
            var ionMobilityFiltering = ionMobilityFilteringOriginal;
            if (FullScanSettingsControl.IonMobilityFiltering.Visible)
            {
                try
                {
                    ionMobilityFiltering =
                        FullScanSettingsControl.IonMobilityFiltering.ValidateIonMobilitySettings(true);
                    if (ionMobilityFiltering == null)
                    {
                        return false;
                    }

                    Helpers.AssignIfEquals(ref ionMobilityFiltering, ionMobilityFilteringOriginal);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            TransitionSettings transitionSettings;
            try
            {
                transitionSettings = new TransitionSettings(prediction, filter,
                    libraries, TransitionSettings.Integration, TransitionSettings.Instrument, fullScan, ionMobilityFiltering);

                Helpers.AssignIfEquals(ref transitionSettings, TransitionSettings);
            }
            catch (Exception x)
            {
                MessageDlg.Show(this, x.Message);
                return false;
            }

            // Only update, if anything changed
            if (ReferenceEquals(transitionSettings, TransitionSettings))
                return true;

            ModifyDocumentNoUndo(doc =>
            {
                var settingsNew = doc.Settings;
                if (!ReferenceEquals(transitionSettings, TransitionSettings))
                    settingsNew = settingsNew.ChangeTransitionSettings(transitionSettings);
                return doc.ChangeSettings(settingsNew);
            });
            _fullScanSettingsChanged = true;
            return true;
        }

        private void ShowEarlyFinish(bool show)
        {
            btnEarlyFinish.Visible = show;
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
                    FullScanSettingsControl.HARDKLOR_PRECURSOR_RES_MZ : (double?)null);
            var newTransitionSettings = Document.Settings.TransitionSettings.ChangeFullScan(newFullScanSettings);
            var docNew = Document.ChangeSettings(Document.Settings.ChangeTransitionSettings(newTransitionSettings));
            SetDocument(docNew, Document);
        }

        public void WizardFinish()
        {
            Settings.Default.ImportResultsSimultaneousFiles = ImportResultsControl.SimultaneousFiles;
            Settings.Default.ImportResultsDoAutoRetry = ImportResultsControl.DoAutoRetry;

            // Import results only on "finish"
            var namedResults = ImportPeptideSearch.EnsureUniqueNames(ImportResultsControl.FoundResultsFiles)
                .Select(kvp => new KeyValuePair<string, MsDataFileUri[]>(kvp.Name, new[] { new MsDataFilePath(kvp.Path) }))
                .ToList();

            // Ask about lockmass correction, if needed - lockmass settings in namedResults will be updated by this call as needed
            if (!ImportResultsLockMassDlg.UpdateNamedResultsParameters(this, Document, ref namedResults))
            {
                CloseWizard(DialogResult.Cancel);  // User cancelled, no change
            }
            else
            {
                UpdateFullScanSettingsForFeatureDetection(); // Tweak full scan filter values if needed
                SkylineWindow.ModifyDocument(
                    PeptideSearchResources.ImportResultsControl_GetPeptideSearchChromatograms_Import_results,
                    doc => SkylineWindow.ImportResults(Document, namedResults, ExportOptimize.NONE), FormSettings.EntryCreator.Create);
                
                CloseWizard(DialogResult.OK);
            }
        }

        private bool BuildPeptideSearchLibrary(CancelEventArgs e, bool isFeatureDetection, bool showWarnings = true)
        {
            var result = BuildPepSearchLibControl.BuildOrUsePeptideSearchLibrary(e, showWarnings, isFeatureDetection);
            if (result)
            {
                Func<SrmDocumentPair, AuditLogEntry> logFunc;
                if (BuildPepSearchLibControl.UseExistingLibrary)
                {
                    logFunc = AuditLogEntry.SettingsLogFunction;
                }
                else
                {
                    logFunc = BuildPepSearchLibControl.BuildLibrarySettings.EntryCreator.Create;
                }
                SkylineWindow.ModifyDocument(!isFeatureDetection ?
                        PeptideSearchResources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Add_document_spectral_library:
                        PeptideSearchResources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Add_spectral_library
                    ,
                    doc => Document, logFunc);
                SetDocument(SkylineWindow.Document, _documents.Peek());
            }

            return result;
        }

        private bool AddExistingLibrary(string libraryPath)
        {
            var doc = SkylineWindow.Document;
            var peptideLibraries = doc.Settings.PeptideSettings.Libraries;
            var existingLib = peptideLibraries.LibrarySpecs.FirstOrDefault(spec => spec.FilePath == libraryPath);
            if (existingLib != null)
            {
                return true;
            }

            LibrarySpec librarySpec =
                Settings.Default.SpectralLibraryList.FirstOrDefault(spec => spec.FilePath == libraryPath);
            if (librarySpec == null)
            {
                var existingNames = new HashSet<string>();
                existingNames.UnionWith(Settings.Default.SpectralLibraryList.Select(spec => spec.Name));
                existingNames.UnionWith(peptideLibraries.LibrarySpecs.Select(spec => spec.Name));
                string libraryName =
                    Helpers.GetUniqueName(Path.GetFileNameWithoutExtension(libraryPath), existingNames);
                librarySpec = LibrarySpec.CreateFromPath(libraryName, libraryPath);
            }

            peptideLibraries =
                peptideLibraries.ChangeLibrarySpecs(peptideLibraries.LibrarySpecs.Append(librarySpec).ToArray());
            var newSettings =
                doc.Settings.ChangePeptideSettings(doc.Settings.PeptideSettings.ChangeLibraries(peptideLibraries));

            return SkylineWindow.ChangeSettings(newSettings, true);
        }

        public void WizardEarlyFinish()
        {
            if (CurrentPage == Pages.spectra_page)
            {
                if (BuildPepSearchLibControl.UseExistingLibrary)
                {
                    string path = BuildPepSearchLibControl.ValidateLibraryPath();
                    if (path == null)
                    {
                        return;
                    }

                    if (!AddExistingLibrary(path))
                    {
                        return;
                    }
                }
                else
                {
                    var eCancel = new CancelEventArgs();
                    if (!BuildPeptideSearchLibrary(eCancel, WorkflowType == Workflow.feature_detection))
                    {
                        if (eCancel.Cancel)
                            return;

                        CloseWizard(DialogResult.Cancel);
                    }
                }
            }

            CloseWizard(DialogResult.OK);
        }

        private void btnEarlyFinish_Click(object sender, EventArgs e)
        {
            WizardEarlyFinish();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            CloseWizard(DialogResult.Cancel);
        }

        public void CloseWizard(DialogResult result)
        {
            DialogResult = result;
        }

        private bool CanWizardClose()
        {
            var wizardPageControl = GetPageControl(wizardPagesImportPeptideSearch.SelectedTab) as WizardPageControl;
            return wizardPageControl == null || Program.ClosingForms || wizardPageControl.CanWizardClose();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Ask current WizardPageControl if wizard is in a good state to close
            if (!CanWizardClose())
            {
                e.Cancel = true;
                return;
            }

            // Close file handles to the peptide search library
            ImportPeptideSearch.ClosePeptideSearchLibraryStreams(Document);

            // Cancel and dispose DDA SearchEngine
            SearchControl?.Cancel();
            ImportPeptideSearch.SearchEngine?.Dispose();

            base.OnFormClosing(e);
        }

        private void BuildPepSearchLibForm_OnInputFilesChanged(object sender, EventArgs e)
        {
            var isReady = BuildPepSearchLibControl.IsReady;
            btnNext.Enabled = isReady;
            if (btnEarlyFinish.Visible)
            {
                btnEarlyFinish.Enabled = isReady && !BuildPepSearchLibControl.PerformDDASearch;
            }
        }

        private void ImportResultsControl_OnResultsFilesChanged(object sender,
            ImportResultsControl.ResultsFilesEventArgs e)
        {
            btnNext.Enabled = e.NumFoundFiles > 0;
        }

        //
        // Handler for Full Scan settings changes that require ion type changes
        //
        private void OnFullScanEnabledChanged(FullScanSettingsControl.FullScanEnabledChangeEventArgs e)
        {
            if (e.MS1Enabled.HasValue && 
                (TransitionSettingsControl.PeptideIonTypes.Contains(IonType.precursor) != e.MS1Enabled.Value)) // Full-Scan settings adjusted ion types to include or exclude "p"
            {
                var list = TransitionSettingsControl.PeptideIonTypes.ToList();
                if (e.MS1Enabled.Value)
                    list.Add(IonType.precursor); // MS1 full scan isn't possible without precursors enabled, so be helpful and do so
                else if (!TransitionSettingsControl.InitialPeptideIonTypes.Contains(IonType.precursor))
                    list.Remove(IonType.precursor); // Only remove this if it wasn't there at the start
                if (list.Count > 0)
                    TransitionSettingsControl.PeptideIonTypes = list.ToArray();
            }
            // FUTURE(bspratt): handle small mol ion types when this gets extended for UIModes
        }

        #region Functional testing support

        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = wizardPagesImportPeptideSearch.SelectedIndex));
                if (selectedIndex == (int)Pages.full_scan_settings_page && !IsDdaWorkflow)
                {
                    if (TransitionSettingsControl.IonFilter)
                        return new ImsFullScanPage();
                    else
                        return new Ms2FullScanPage();
                }
                if (selectedIndex == (int) Pages.chromatograms_page && WorkflowType == Workflow.dia)
                    return new ChromatogramsDiaPage();
                return TAB_PAGES[selectedIndex];
            }
        }

        public bool IsNextButtonEnabled
        {
            get { return btnNext.Enabled; }
        }

        public bool ClickNextButton()
        {
            if (IsNextButtonEnabled)
            {
                btnNext.PerformClick();
                return true;
            }
            return false;
        }

        public void ClickNextButtonNoCheck()
        {
            btnNext.PerformClick();
        }

        public bool IsBackButtonVisible
        {
            get { return btnBack.Visible; }
        }

        public bool ClickBackButton()
        {
            if (IsBackButtonVisible)
            {
                btnBack.PerformClick();
                return true;
            }
            return false;
        }

        public void ClickCancelButton()
        {
            CancelButton.PerformClick();
        }

        public bool IsEarlyFinishButtonEnabled
        {
            get { return btnEarlyFinish.Enabled; }
        }

        public bool ClickEarlyFinishButton()
        {
            if (IsEarlyFinishButtonEnabled)
            {
                btnEarlyFinish.PerformClick();
                return true;
            }

            return false;
        }

        #endregion
    }
}
