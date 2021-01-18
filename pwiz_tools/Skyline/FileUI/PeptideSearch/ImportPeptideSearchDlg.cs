/*
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
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.Results;
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
            dda_search_settings_page,
            dda_search_page
        }

        public enum Workflow
        {
            dda,
            prm,
            dia
        }

        public class SpectraPage : IFormView { }
        public class ChromatogramsPage : IFormView { }
        public class ChromatogramsDiaPage : IFormView { }
        public class MatchModsPage : IFormView { }
        public class TransitionSettingsPage : IFormView { }
        public class Ms1FullScanPage : IFormView { }
        public class Ms2FullScanPage : IFormView { }
        public class FastaPage : IFormView { }
        public class DDASearchSettingsPage : IFormView { }
        public class DDASearchPage : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new SpectraPage(), new ChromatogramsPage(), new MatchModsPage(), new TransitionSettingsPage(), new Ms1FullScanPage(), new FastaPage(), new DDASearchSettingsPage(), new DDASearchPage()
        };

        private readonly Stack<SrmDocument> _documents;

        public ImportPeptideSearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager)
        {
            SkylineWindow = skylineWindow;
            _documents = new Stack<SrmDocument>();
            SetDocument(skylineWindow.Document, null);

            ImportPeptideSearch = new ImportPeptideSearch();

            InitializeComponent();

            Icon = Resources.Skyline;

            btnEarlyFinish.Location = btnBack.Location;

            CurrentPage = Pages.spectra_page;
            btnNext.Text = Resources.ImportPeptideSearchDlg_ImportPeptideSearchDlg_Next;
            AcceptButton = btnNext;
            btnNext.Enabled = HasUnmatchedLibraryRuns(Document);

            // Create and add wizard pages
            BuildPepSearchLibControl = new BuildPeptideSearchLibraryControl(this, ImportPeptideSearch, libraryManager)
            {
                Dock = DockStyle.Fill,
            };
            BuildPepSearchLibControl.InputFilesChanged += BuildPepSearchLibForm_OnInputFilesChanged;

            buildLibraryPanel.Controls.Add(BuildPepSearchLibControl);

            ImportFastaControl = new ImportFastaControl(this, SkylineWindow.SequenceTree)
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(2, 60)
            };
            importFastaPage.Controls.Add(ImportFastaControl);

            MatchModificationsControl = new MatchModificationsControl(this, ImportPeptideSearch)
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(2, 60)
            };
            matchModificationsPage.Controls.Add(MatchModificationsControl);

            TransitionSettingsControl = new TransitionSettingsControl(this)
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(18, 60)
            };
            transitionSettingsUiPage.Controls.Add(TransitionSettingsControl);

            FullScanSettingsControl = new FullScanSettingsControl(this)
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(18, 50)
            };
            ms1FullScanSettingsPage.Controls.Add(FullScanSettingsControl);
            FullScanSettingsControl.FullScanEnabledChanged += OnFullScanEnabledChanged; // Adjusts ion settings when full scan settings change

            ImportResultsControl = new ImportResultsControl(ImportPeptideSearch, DocumentFilePath)
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(2, 60)
            };
            getChromatogramsPage.Controls.Add((Control) ImportResultsControl);

            SearchSettingsControl = new SearchSettingsControl(this, ImportPeptideSearch)
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(18, 50)
            };
            ddaSearchSettingsPage.Controls.Add(SearchSettingsControl);

            SearchControl = new DDASearchControl(ImportPeptideSearch)
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(18, 50)
            };
            ddaSearch.Controls.Add(SearchControl);

            _pagesToSkip = new HashSet<Pages>();
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
                return new ImportPeptideSearchSettings(
                    ImportResultsControl.ImportSettings,
                    MatchModificationsControl.ModificationSettings,
                    skippedTransitionPage ? null : TransitionSettingsControl.FilterAndLibrariesSettings, FullScanSettingsControl.FullScan,
                    ImportFastaControl.ImportSettings,
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
                TransitionFullScan fullScanSettings, ImportFastaControl.ImportFastaSettings importFastaSettings,
                SrmDocument.DOCUMENT_TYPE docType)
            {
                ImportResultsSettings = importResultsSettings;
                ModificationsSettings = modificationsSettings;
                FilterAndLibrariesSettings = filterAndLibSettings;
                FullScanSettings = fullScanSettings;
                ImportFastaSettings = importFastaSettings;
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
                    SrmDocument.DOCUMENT_TYPE.proteomic);
            }
        }

        public ImportPeptideSearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager, Workflow workflowType)
            : this(skylineWindow, libraryManager)
        {
            BuildPepSearchLibControl.ForceWorkflow(workflowType);
            if (workflowType == Workflow.dda)
            {
                AdjustHeight(-FullScanSettingsControl.GroupBoxMS2Height); // No MS2 control
            }
        }

        public void AdjustHeight(int change)
        {
            MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height + change);
            if (change < 0)
            {
                Height += change;
            }
        }

        private SkylineWindow SkylineWindow { get; set; }
        private ImportPeptideSearch ImportPeptideSearch { get; set; }
        private TransitionSettings TransitionSettings { get { return Document.Settings.TransitionSettings; } }
        public TransitionFullScan FullScan { get { return TransitionSettings.FullScan; } }

        private bool _modificationSettingsChanged;
        private bool _transitionSettingsChanged;
        private bool _fullScanSettingsChanged;

        public bool HasPeakBoundaries { get; private set; }

        public BuildPeptideSearchLibraryControl BuildPepSearchLibControl { get; private set; }
        public ImportFastaControl ImportFastaControl { get; private set; }
        public TransitionSettingsControl TransitionSettingsControl { get; private set; }
        public FullScanSettingsControl FullScanSettingsControl { get; private set; }
        public IImportResultsControl ImportResultsControl { get; private set; }
        public MatchModificationsControl MatchModificationsControl { get; private set; }
        public SearchSettingsControl SearchSettingsControl { get; private set; }
        public DDASearchControl SearchControl { get; private set; }


        public Workflow WorkflowType
        {
            get { return BuildPepSearchLibControl.WorkflowType; }
        }

        private bool FastaOptional
        {
            get { return (!BuildPepSearchLibControl.FilterForDocumentPeptides && Document.PeptideCount > 0) &&
                         !BuildPepSearchLibControl.PerformDDASearch; }
        }

        private Pages LastPage
        {
            get
            {
                int lastPage = wizardPagesImportPeptideSearch.TabCount - 1;
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

        public Pages CurrentPage
        {
            get { return (Pages)wizardPagesImportPeptideSearch.SelectedIndex; }
            private set { wizardPagesImportPeptideSearch.SelectedIndex = (int)value; }
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

                        ImportPeptideSearch.IsDDASearch = BuildPepSearchLibControl.PerformDDASearch;
                        ImportFastaControl.IsDDASearch = BuildPepSearchLibControl.PerformDDASearch;
                        if (!BuildPepSearchLibControl.UseExistingLibrary)
                        {
                            if (!BuildPepSearchLibControl.PerformDDASearch)
                            {
                                HasPeakBoundaries = BuildPepSearchLibControl.SearchFilenames.All(f => f.EndsWith(BiblioSpecLiteBuilder.EXT_TSV));
                                if (BuildPepSearchLibControl.SearchFilenames.Any(f => f.EndsWith(BiblioSpecLiteBuilder.EXT_TSV)) && !HasPeakBoundaries)
                                {
                                    MessageDlg.Show(this, Resources.ImportPeptideSearchDlg_NextPage_Cannot_build_library_from_OpenSWATH_results_mixed_with_results_from_other_tools_);
                                    return;
                                }
                            }
                        }

                        var eCancel = new CancelEventArgs();
                        if (!BuildPeptideSearchLibrary(eCancel))
                        {
                            // Page shows error
                            if (eCancel.Cancel)
                                return;
                            CloseWizard(DialogResult.Cancel);
                        }

                        // The user had the option to finish right after 
                        // building the peptide search library, but they
                        // did not, so hide the "early finish" button for
                        // the rest of the wizard pages.
                        ShowEarlyFinish(false);
                        
                        if (BuildPepSearchLibControl.PerformDDASearch)
                            _pagesToSkip.Add(Pages.chromatograms_page);

                        lblFasta.Text = FastaOptional
                            ? Resources.ImportPeptideSearchDlg_NextPage_Import_FASTA__optional_
                            : Resources.ImportPeptideSearchDlg_NextPage_Import_FASTA__required_;

                        // The next page is going to be the chromatograms page.
                        var oldImportResultsControl = (ImportResultsControl) ImportResultsControl;

                        if (WorkflowType != Workflow.dia || HasPeakBoundaries)
                        {
                            oldImportResultsControl.InitializeChromatogramsPage(Document);

                            if (WorkflowType == Workflow.dda)
                            {
                                _pagesToSkip.Add(Pages.transition_settings_page);
                            }
                        }
                        else
                        {
                            // DIA workflow, replace old ImportResultsControl
                            ImportResultsControl = new ImportResultsDIAControl(this)
                            {
                                Anchor = oldImportResultsControl.Anchor,
                                Location = oldImportResultsControl.Location
                            };
                            getChromatogramsPage.Controls.Remove(oldImportResultsControl);
                            getChromatogramsPage.Controls.Add((Control) ImportResultsControl);
                        }

                        if (!BuildPepSearchLibControl.PerformDDASearch)
                        {
                            ImportResultsControl.ResultsFilesChanged += ImportResultsControl_OnResultsFilesChanged;
                            TransitionSettingsControl.Initialize(WorkflowType);
                        }
                        else
                        {
                            ImportPeptideSearch.SpectrumSourceFiles.Clear();

                            // in PerformDDA mode, set SpectrumSourceFiles and offer to remove prefix
                            for (int i = 0; i < BuildPepSearchLibControl.DdaSearchDataSources.Length; ++i)
                            {
                                var ddaSource = BuildPepSearchLibControl.DdaSearchDataSources[i];
                                ImportPeptideSearch.SpectrumSourceFiles.Add(ddaSource.GetFileName(),
                                    new ImportPeptideSearch.FoundResultsFilePossibilities(ddaSource.GetFileName())
                                        {ExactMatch = ddaSource.ToString()});
                            }
                            ShowRemovePrefixDialog();
                        }

                        // Set up full scan settings page
                        var lib = BuildPepSearchLibControl.ImportPeptideSearch.DocLib;
                        var libIonMobilities = lib != null && PeptideLibraries.HasIonMobilities(lib, null);
                        FullScanSettingsControl.ModifyOptionsForImportPeptideSearchWizard(WorkflowType, libIonMobilities);
                        if (libIonMobilities)
                        {
                            AdjustHeight(FullScanSettingsControl.IonMobilityFiltering.Height + 2 * label1.Height); // Need real estate to ask about using ion mobility data found in imported spectral libraries
                        }

                        bool hasMatchedMods = MatchModificationsControl.Initialize(Document);
                        if (!hasMatchedMods && !BuildPepSearchLibControl.PerformDDASearch)
                            _pagesToSkip.Add(Pages.match_modifications_page);
                        if (BuildPepSearchLibControl.FilterForDocumentPeptides && !BuildPepSearchLibControl.PerformDDASearch)
                            _pagesToSkip.Add(Pages.import_fasta_page);
                        if (!BuildPepSearchLibControl.PerformDDASearch)
                        {
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
                        if (!ImportPeptideSearch.VerifyRetentionTimes(ImportResultsControl.FoundResultsFiles.Select(f => f.Path)))
                        {
                            MessageDlg.Show(this, TextUtil.LineSeparate(Resources.ImportPeptideSearchDlg_NextPage_The_document_specific_spectral_library_does_not_have_valid_retention_times_,
                                Resources.ImportPeptideSearchDlg_NextPage_Please_check_your_peptide_search_pipeline_or_contact_Skyline_support_to_ensure_retention_times_appear_in_your_spectral_libraries_));
                            CloseWizard(DialogResult.Cancel);
                        }

                        if (ImportResultsControl.ResultsFilesMissing)
                        {
                            if (MessageBox.Show(this, Resources.ImportPeptideSearchDlg_NextPage_Some_results_files_are_still_missing__Are_you_sure_you_want_to_continue_,
                                Program.Name, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                            {
                                return;
                            }
                        }

                        ShowRemovePrefixDialog();
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
                    break;

                case Pages.import_fasta_page: // This is the last page (if there is no dda search)
                    if (ImportPeptideSearch.IsDDASearch)
                    {
                        if (!File.Exists(ImportFastaControl.FastaFile)) 
                        {
                            MessageBox.Show(this, Resources.ImportPeptideSearchDlg_NextPage_FastFileMissing_DDASearch,
                                Program.Name, MessageBoxButtons.OK);
                            
                            return;
                        }

                        ImportPeptideSearch.SearchEngine?.Dispose();
                        ImportPeptideSearch.SearchEngine = new MSAmandaSearchWrapper();
                        SearchSettingsControl.InitializeEngine();
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
                case Pages.dda_search_settings_page:
                    bool valid = SearchSettingsControl.SaveAllSettings();
                    if (!valid) return;
                    ImportPeptideSearch.SearchEngine.SetEnzyme(Document.Settings.PeptideSettings.Enzyme, Document.Settings.PeptideSettings.DigestSettings.MaxMissedCleavages);
                    ImportPeptideSearch.SearchEngine.SetSpectrumFiles(BuildPepSearchLibControl.DdaSearchDataSources);
                    ImportPeptideSearch.SearchEngine.SetFastaFiles(ImportFastaControl.FastaFile);
                    SearchControl.OnSearchFinished += SearchControl_OnSearchFinished;
                    btnNext.Enabled = false;
                    btnCancel.Enabled = false;
                    btnBack.Enabled = false;
                    SearchControl.RunSearch();
                    break;

                case Pages.dda_search_page: // this is really the last page
                    var eCancel2 = new CancelEventArgs();
                    //change search files to result files
                    BuildPepSearchLibControl.PerformDDASearch = false;
                    ImportPeptideSearch.SearchFilenames = new string[BuildPepSearchLibControl.DdaSearchDataSources.Length];
                    for(int i=0; i < BuildPepSearchLibControl.DdaSearchDataSources.Length; ++i)
                    {
                        var ddaSource = BuildPepSearchLibControl.DdaSearchDataSources[i];
                        var outFilePath = ImportPeptideSearch.SearchEngine.GetSearchResultFilepath(ddaSource);
                        ImportPeptideSearch.SearchFilenames[i] = outFilePath;
                    }
                    BuildPepSearchLibControl.AddSearchFiles(ImportPeptideSearch.SearchFilenames);

                    if (!BuildPeptideSearchLibrary(eCancel2))
                        return;

                    //load proteins after search
                    if (!ImportFastaControl.ImportFasta(ImportPeptideSearch.IrtStandard))
                        return;

                    ImportPeptideSearch.SearchEngine.Dispose();

                    WizardFinish();
                    return;

            }

            var newPage = CurrentPage + 1;
            while (_pagesToSkip.Contains(newPage))
                ++newPage;

            // Skip import FASTA if user filters for document peptides
            if (newPage > Pages.import_fasta_page && !ImportPeptideSearch.IsDDASearch)
            {
                WizardFinish();
                return;
            }

            CurrentPage = newPage;
            UpdateButtons();
        }

        private void ShowRemovePrefixDialog()
        {
            var foundResults = ImportResultsControl.FoundResultsFiles;
            if (foundResults.Count <= 1)
                return;

            // Older Resharper code inspection implementations insist on warning here
            // Resharper disable PossibleMultipleEnumeration
            string[] resultNames = foundResults.Select(f => f.Name).ToArray();
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
                            new ImportPeptideSearch.FoundResultsFile(dlgName.ApplyNameChange(f.Name), f.Path)).ToList();

                        ImportResultsControl.Prefix =
                            string.IsNullOrEmpty(prefix) ? null : prefix;
                        ImportResultsControl.Suffix =
                            string.IsNullOrEmpty(suffix) ? null : suffix;
                    }
                }
            }
        }

        private void SearchControl_OnSearchFinished(bool success)
        {
            btnCancel.Enabled = true;
            btnBack.Enabled = true;
            if (success)
                btnNext.Enabled = true;
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
                    FullScanSettingsControl.Initialize(); // reset UI to default
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
                    break;
                case Pages.dda_search_settings_page:
                    btnNext.Enabled = true;
                    break;
                case Pages.dda_search_page:
                    SearchControl.OnSearchFinished -= SearchControl_OnSearchFinished;
                    break;
      }
        }

        private void UpdateButtons()
        {
            if (CurrentPage <= Pages.chromatograms_page)
            {
                btnBack.Hide();
                btnEarlyFinish.Location = btnBack.Location;
            }
            else if (!btnBack.Visible)
            {
                btnEarlyFinish.Left = btnBack.Left - btnBack.Width - 6;
                btnBack.Show();
            }

            btnNext.Text = CurrentPage != LastPage
                ? Resources.ImportPeptideSearchDlg_ImportPeptideSearchDlg_Next
                : Resources.ImportPeptideSearchDlg_NextPage_Finish;
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
                if (WorkflowType == Workflow.dda)
                {
                    MessageDlg.Show(this, Resources.ImportPeptideSearchDlg_UpdateFullScanSettings_Full_scan_MS1_filtering_must_be_enabled_in_order_to_import_a_peptide_search_);
                    return false;
                }
                else if (FullScanSettingsControl.AcquisitionMethod == FullScanAcquisitionMethod.None)
                {
                    MessageDlg.Show(this, Resources.ImportPeptideSearchDlg_UpdateFullScanSettings_Full_scan_MS1_or_MS_MS_filtering_must_be_enabled_in_order_to_import_a_peptide_search_);
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
            if (WorkflowType == Workflow.dda && !filter.PeptideIonTypes.Contains(IonType.precursor))
                filter = filter.ChangePeptideIonTypes(new[] {IonType.precursor});
            if (!filter.AutoSelect)
                filter = filter.ChangeAutoSelect(true);
            Helpers.AssignIfEquals(ref filter, TransitionSettings.Filter);

            if (FullScanSettingsControl.IsDIA() && filter.ExclusionUseDIAWindow)
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
                    TransitionSettings.Libraries, TransitionSettings.Integration, TransitionSettings.Instrument, fullScan, ionMobilityFiltering);

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

        public void WizardFinish()
        {
            Settings.Default.ImportResultsSimultaneousFiles = ImportResultsControl.SimultaneousFiles;
            Settings.Default.ImportResultsDoAutoRetry = ImportResultsControl.DoAutoRetry;

            // Import results only on "finish"
            var namedResults =
                ImportResultsControl.FoundResultsFiles.Select(
                    kvp => new KeyValuePair<string, MsDataFileUri[]>(kvp.Name, new[] { new MsDataFilePath(kvp.Path) }))
                    .ToList();

            // Ask about lockmass correction, if needed - lockmass settings in namedResults will be updated by this call as needed
            if (!ImportResultsLockMassDlg.UpdateNamedResultsParameters(this, Document, ref namedResults))
            {
                CloseWizard(DialogResult.Cancel);  // User cancelled, no change
            }
            else
            {
                SkylineWindow.ModifyDocument(
                    Resources.ImportResultsControl_GetPeptideSearchChromatograms_Import_results,
                    doc => SkylineWindow.ImportResults(Document, namedResults, ExportOptimize.NONE), FormSettings.EntryCreator.Create);
                
                CloseWizard(DialogResult.OK);
            }
        }

        private bool BuildPeptideSearchLibrary(CancelEventArgs e)
        {
            var result = BuildPepSearchLibControl.BuildOrUsePeptideSearchLibrary(e);
            if (result)
            {
                SkylineWindow.ModifyDocument(
                    Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Add_document_spectral_library,
                    doc => Document, BuildPepSearchLibControl.BuildLibrarySettings.EntryCreator.Create);
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
                    if (!BuildPeptideSearchLibrary(eCancel))
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
            // Close file handles to the peptide search library
            ImportPeptideSearch.ClosePeptideSearchLibraryStreams(Document);
            DialogResult = result;
        }

        private void BuildPepSearchLibForm_OnInputFilesChanged(object sender,
            EventArgs e)
        {
            bool isReady = BuildPepSearchLibControl.AnyInputFiles;
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
                if (selectedIndex == (int) Pages.full_scan_settings_page && WorkflowType != Workflow.dda)
                    return new Ms2FullScanPage();
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
