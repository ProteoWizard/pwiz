/*
 * Original author: Tahmina Jahan <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public interface IModifyDocumentContainer : IDocumentContainer
    {
        void ModifyDocumentNoUndo(Func<SrmDocument, SrmDocument> act);
        void ModifyDocument(string description, Func<SrmDocument, SrmDocument> act, Func<SrmDocumentPair, AuditLogEntry> logFunc);
    }

    /// <summary>
    /// The wizard shared by <see cref="ImportPeptideSearchDlg"/>, which imports the results of a
    /// search that was already done, and <see cref="RunPeptideSearchDlg"/>, which runs the search.
    /// It holds the pages both use, the document the pages change as the user moves through them,
    /// and the navigation. Each derived wizard lists its pages with <see cref="SetWizardPages"/>:
    /// when each page applies, what leaving it with Next does, and what returning to it with Back
    /// undoes.
    /// </summary>
    public partial class PeptideSearchWizardDlg : FormEx, IAuditLogModifier<PeptideSearchWizardDlg.ImportPeptideSearchSettings>, IMultipleViewProvider, IModifyDocumentContainer, ILongWaitForm
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

        // Alias for the model-layer SearchWorkflowType enum to avoid churn in existing references
        public enum Workflow
        {
            dda = SearchWorkflowType.dda,
            prm = SearchWorkflowType.prm,
            dia = SearchWorkflowType.dia,
            feature_detection = SearchWorkflowType.feature_detection
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

        private static readonly IDictionary<Pages, Func<IFormView>> PAGE_VIEWS = new Dictionary<Pages, Func<IFormView>>
        {
            { Pages.spectra_page, () => new SpectraPage() },
            { Pages.chromatograms_page, () => new ChromatogramsPage() },
            { Pages.match_modifications_page, () => new MatchModsPage() },
            { Pages.transition_settings_page, () => new TransitionSettingsPage() },
            { Pages.full_scan_settings_page, () => new Ms1FullScanPage() },
            { Pages.import_fasta_page, () => new FastaPage() },
            { Pages.converter_settings_page, () => new ConverterSettingsPage() },
            { Pages.dda_search_settings_page, () => new DDASearchSettingsPage() },
            { Pages.dda_search_page, () => new DDASearchPage() },
        };

        private readonly Stack<SrmDocument> _documents;
        private readonly Dictionary<Pages, string> _tabPageNames; // In small mol UI mode, some pages go away and indexing is not straightforward
        private readonly Dictionary<Pages, int> _documentCountBeforePage = new Dictionary<Pages, int>();
        private HashSet<Pages> _workflowPages; // The pages that apply, fixed when the user leaves the first page
        private IList<WizardPage> _wizardPages = Array.Empty<WizardPage>();
        private bool _heightAdjusted;

        public bool IsAutomatedTest; // Testing support

        /// <summary>
        /// For the Windows Forms designer only.
        /// </summary>
        public PeptideSearchWizardDlg()
        {
            InitializeComponent();
        }

        protected PeptideSearchWizardDlg(SkylineWindow skylineWindow, LibraryManager libraryManager, bool isRunPeptideSearch, Workflow? workflowType)
        {
            SkylineWindow = skylineWindow;
            _documents = new Stack<SrmDocument>();
            IsAutomatedTest = false;
            SetDocument(skylineWindow.Document, null);

            ImportPeptideSearch = new ImportPeptideSearch
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
        }

        /// <summary>
        /// One page of the wizard: when it applies, what leaving it with Next does, and what
        /// coming back to it with Back does.
        /// </summary>
        protected class WizardPage
        {
            public WizardPage(Pages page, Func<bool> isApplicable, Func<bool> leave, Action returnTo = null)
            {
                Page = page;
                IsApplicable = isApplicable;
                Leave = leave;
                ReturnTo = returnTo ?? (() => { });
            }

            public Pages Page { get; }

            /// <summary>
            /// True when the page is shown in the current workflow.
            /// </summary>
            public Func<bool> IsApplicable { get; }

            /// <summary>
            /// Validates the page and applies its settings. False keeps the user on the page.
            /// </summary>
            public Func<bool> Leave { get; }

            /// <summary>
            /// Runs when the user comes back to the page with Back, after any document changes the
            /// page made when it was left have been undone.
            /// </summary>
            public Action ReturnTo { get; }
        }

        /// <summary>
        /// The pages of the wizard in the order they are shown. Next skips pages that do not apply;
        /// leaving the last page that applies finishes the wizard with <see cref="Finish"/>.
        /// </summary>
        protected void SetWizardPages(IEnumerable<WizardPage> pages)
        {
            _wizardPages = pages.ToList();
        }

        protected SkylineWindow SkylineWindow { get; }
        protected ImportPeptideSearch ImportPeptideSearch { get; }

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

        /// <summary>
        /// The settings of the Osprey search whose library this wizard imports, for the import's
        /// audit log entry (<see cref="FormSettings"/>); null on every other route.
        /// </summary>
        public OspreySearchSettings OspreySearchSettings { get; set; }

        public ImportPeptideSearchSettings FormSettings
        {
            get
            {
                var skippedTransitionPage = !IsPageApplicable(Pages.transition_settings_page);
                return new ImportPeptideSearchSettings(
                    ImportResultsControl.ImportSettings,
                    MatchModificationsControl.ModificationSettings,
                    skippedTransitionPage ? null : TransitionSettingsControl.FilterAndLibrariesSettings,
                    FullScanSettingsControl.FullScan,
                    ImportFastaControl.ImportSettings,
                    ImportFastaControl.AssociateProteinsSettings,
                    DdaSearchSettings,
                    DdaConverterSettings,
                    OspreySearchSettings,
                    ImportPeptideSearch.SettingsHardklor,
                    ModeUI);
            }
        }

        /// <summary>
        /// The search settings recorded in the audit log, when the wizard ran a search.
        /// </summary>
        protected virtual SearchSettingsControl.DdaSearchSettings DdaSearchSettings => null;

        /// <summary>
        /// The spectrum conversion settings recorded in the audit log, when the wizard converted spectra.
        /// </summary>
        protected virtual ConverterSettingsControl.DdaConverterSettings DdaConverterSettings => null;

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
                OspreySearchSettings ospreySearchSettings,
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
                OspreySearchSettings = ospreySearchSettings;
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

            // Osprey search settings (the Osprey Search wizard's import)
            [TrackChildren]
            public OspreySearchSettings OspreySearchSettings { get; private set; }

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
                    null,
                    SrmDocument.DOCUMENT_TYPE.proteomic);
            }
        }

        public void AdjustHeightForFullScanSettings()
        {
            if (IsDdaWorkflow || _heightAdjusted)
                return;

            var panel = ms1FullScanSettingsPage.Controls.OfType<Control>().OrderBy(c => c.Top).First(); // Location of panel containing the FullScan control
            var tabHeight = ms1FullScanSettingsPage.Height;
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

        public TransitionSettings TransitionSettings { get { return Document.Settings.TransitionSettings; } }
        public TransitionFullScan FullScan { get { return TransitionSettings.FullScan; } }

        public bool HasPeakBoundaries { get; protected set; }

        public BuildPeptideSearchLibraryControl BuildPepSearchLibControl { get; }
        public ImportFastaControl ImportFastaControl { get; }
        public TransitionSettingsControl TransitionSettingsControl { get; }
        public FullScanSettingsControl FullScanSettingsControl { get; private set; }
        public IImportResultsControl ImportResultsControl { get; private set; }
        public MatchModificationsControl MatchModificationsControl { get; }

        public ImportResultsControl ImportResultsDDAControl { get; }
        public ImportResultsDIAControl ImportResultsDIAControl { get; private set; }

        public Workflow WorkflowType
        {
            get { return BuildPepSearchLibControl.WorkflowType; }
        }

        public bool IsImportingSearchResults => InputFileType == InputFile.search_result;
        public bool IsDdaWorkflow => WorkflowType == Workflow.dda || WorkflowType == Workflow.feature_detection;
        public bool IsFeatureDetectionWorkflow => WorkflowType == Workflow.feature_detection;

        public InputFile InputFileType => BuildPepSearchLibControl.InputFileType;

        /// <summary>
        /// True when the chromatograms page uses <see cref="ImportResultsDIAControl"/> to choose the
        /// DIA files to extract, rather than finding the files named in the library.
        /// </summary>
        protected bool UsesDiaResultsControl =>
            WorkflowType == Workflow.dia && !HasPeakBoundaries && !(IsImportingSearchResults && GetAreLibrarySpectraDIA());

        protected bool FastaOptional
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

        /// <summary>
        /// True when the page is shown in the workflow, which is fixed when the user leaves the first page.
        /// </summary>
        protected bool IsPageApplicable(Pages page)
        {
            var wizardPage = _wizardPages.FirstOrDefault(p => p.Page == page);
            return wizardPage != null && Applies(wizardPage);
        }

        /// <summary>
        /// Leaves the current page for the next one that applies, or finishes the wizard when there
        /// is none.
        /// </summary>
        protected void NextPage()
        {
            var currentPage = CurrentPage;
            int documentCount = _documents.Count;
            if (!GetWizardPage(currentPage).Leave())
                return;
            _documentCountBeforePage[currentPage] = documentCount;

            // The choices on the first page decide which pages follow it. Later pages can change
            // some of those choices (e.g. a preset changes the workflow), but not the pages.
            if (currentPage == _wizardPages.First().Page)
                _workflowPages = _wizardPages.Where(p => p.IsApplicable()).Select(p => p.Page).ToHashSet();

            var nextPage = GetApplicablePages().SkipWhile(p => p.Page != currentPage).Skip(1).FirstOrDefault();
            if (nextPage == null)
            {
                Finish();
                return;
            }

            CurrentPage = nextPage.Page;
            UpdateButtons();
            UpdateMinimumSize();
        }

        /// <summary>
        /// Called when the user leaves the last page that applies.
        /// </summary>
        protected virtual void Finish()
        {
            WizardFinish();
        }

        private void PreviousPage()
        {
            var currentPage = CurrentPage;
            var previousPage = GetApplicablePages().TakeWhile(p => p.Page != currentPage).LastOrDefault() ??
                               _wizardPages.First();
            if (previousPage == _wizardPages.First())
                _workflowPages = null;
            CurrentPage = previousPage.Page;
            UpdateButtons();

            previousPage.ReturnTo();
            UpdateMinimumSize();
        }

        /// <summary>
        /// Undoes the document changes made since the user left the page with Next, for a page the
        /// user has come back to with Back.
        /// </summary>
        protected void RestoreDocumentBeforePage(Pages page)
        {
            if (!_documentCountBeforePage.TryGetValue(page, out var count))
                return;
            while (_documents.Count > count)
                _documents.Pop();
            _documentCountBeforePage.Remove(page);
        }

        private WizardPage GetWizardPage(Pages page)
        {
            return _wizardPages.First(p => p.Page == page);
        }

        private IEnumerable<WizardPage> GetApplicablePages()
        {
            return _wizardPages.Where(Applies);
        }

        private bool Applies(WizardPage page)
        {
            return _workflowPages?.Contains(page.Page) ?? page.IsApplicable();
        }

        private Pages LastPage => GetApplicablePages().Last().Page;

        private void btnNext_Click(object sender, EventArgs e)
        {
            NextPage();
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            PreviousPage();
        }

        protected virtual void UpdateButtons()
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

        private void UpdateMinimumSize()
        {
            var pageControl = GetPageControl(wizardPagesImportPeptideSearch.SelectedTab);
            if (pageControl != null && pageControl.MinimumSize.Height > 0)
            {
                var outsideHeight = Height - wizardPagesImportPeptideSearch.Height + addModsTitlePanel.Height;
                var outsideWidth = Width - wizardPagesImportPeptideSearch.Width;
                MinimumSize = new Size(
                    pageControl.MinimumSize.Width + outsideWidth,
                    pageControl.MinimumSize.Height + outsideHeight);
            }
        }

        #region Shared pages

        /// <summary>
        /// Sets up the pages after the first one from the choices on the first page: the files and
        /// workflow, and the library the import wizard has just built or loaded.
        /// </summary>
        protected void PrepareFollowingPages(SearchSettingsPreset preset)
        {
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

            if (!UsesDiaResultsControl)
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

            // Set up full scan settings page
            var lib = BuildPepSearchLibControl.ImportPeptideSearch.DocLib;
            var libIonMobilities = lib != null && PeptideLibraries.HasIonMobilities(lib, null);
            FullScanSettingsControl.ModifyOptionsForImportPeptideSearchWizard(WorkflowType, libIonMobilities);
            AdjustHeightForFullScanSettings();

            MatchModificationsControl.Initialize(Document, preset);

            // Decoy options enabled only for DIA
            ImportFastaControl.RequirePrecursorTransition = WorkflowType != Workflow.dia;
            ImportFastaControl.DecoyGenerationEnabled = WorkflowType == Workflow.dia && !HasPeakBoundaries;
            ImportFastaControl.UpdateQValueFilterControls();
        }

        protected void SubscribeResultsFilesChanged()
        {
            ImportResultsControl.ResultsFilesChanged += ImportResultsControl_OnResultsFilesChanged;
        }

        protected bool LeaveChromatogramsPage()
        {
            var anyResults = ImportResultsControl.FoundResultsFiles.Any();
            if (!anyResults)
            {
                using (var dlg = new MultiButtonMsgDlg(
                    PeptideSearchResources.ImportPeptideSearchDlg_NextPage_No_results_files_were_specified__Are_you_sure_you_want_to_continue__Continuing_will_create_a_template_document_with_no_imported_results_,
                    MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                {
                    if (dlg.ShowDialog(this) == DialogResult.No)
                    {
                        return false;
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
                        return false;
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
            return true;
        }

        protected bool LeaveModificationsPage()
        {
            var newSettings = MatchModificationsControl.AddCheckedModifications(Document);
            if (ReferenceEquals(Document.Settings, newSettings))
                return true;

            ModifyDocumentNoUndo(doc => doc.ChangeSettings(newSettings));
            Document.Settings.UpdateDefaultModifications(true, true);
            return true;
        }

        protected bool LeaveTransitionSettingsPage()
        {
            // Try to accept changes to transition settings
            if (!UpdateTransitionSettings())
            {
                return false;
            }
            if (!TransitionSettings.Filter.PeptideIonTypes.Contains(IonType.precursor))
            {
                FullScanSettingsControl.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.None;
            }
            return true;
        }

        /// <summary>
        /// Validates and applies the full-scan settings. The user cannot go any further until they are valid.
        /// </summary>
        protected virtual bool LeaveFullScanSettingsPage()
        {
            return UpdateFullScanSettings();
        }

        protected void ReturnToFullScanSettingsPage()
        {
            RestoreDocumentBeforePage(Pages.full_scan_settings_page);
            btnNext.Enabled = true;
        }

        protected void ShowRemovePrefixDialog()
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

        protected void MakeFullScanSettingsControl(Workflow? workflowType)
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

        protected static void AddPageControl<TControl>(TControl pageControl, TabPage tabPage, int border, int header)
            where TControl : UserControl
        {
            pageControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pageControl.Location = new Point(border, header);
            pageControl.Width = tabPage.Width - border * 2;
            pageControl.Height = tabPage.Height - header - border;
            tabPage.Controls.Add(pageControl);
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
            return true;
        }

        #endregion

        protected void ShowEarlyFinish(bool show)
        {
            btnEarlyFinish.Visible = show;
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
                BeforeImportResults(); // Tweak settings for the workflow if needed
                SkylineWindow.ModifyDocument(
                    PeptideSearchResources.ImportResultsControl_GetPeptideSearchChromatograms_Import_results,
                    doc => SkylineWindow.ImportResults(Document, namedResults, ExportOptimize.NONE), FormSettings.EntryCreator.Create);

                CloseWizard(DialogResult.OK);
            }
        }

        /// <summary>
        /// Called just before the results import is committed to the Skyline document.
        /// </summary>
        protected virtual void BeforeImportResults()
        {
        }

        /// <summary>
        /// Builds the peptide search library if not performing a DDA search.
        /// Closes the wizard on unexpected failure; returns true to continue to the next page.
        /// </summary>
        protected bool BuildPeptideSearchLibraryOrCloseWizard()
        {
            if (BuildPepSearchLibControl.PerformDDASearch)
                return true;

            var eCancel = new CancelEventArgs();
            if (BuildPeptideSearchLibrary(eCancel, IsFeatureDetectionWorkflow))
                return true;

            if (eCancel.Cancel)
                return false; // Page has shown an error and canceled further progress

            // CONSIDER(brendanx): This looks suspicious to me. It closes the entire wizard
            // when BuildPeptideSearchLibrary has failed but eCancel.Cancel is not set, which
            // makes it unclear if the user has seen an error message before the UI disappears.
            // I am not ready to dig into this further as it has been this way for years,
            // passing most tests. It is hard to imagine how the wizard could continue if
            // it fails to create a library, and it is not performing a search.
            CloseWizard(DialogResult.Cancel);
            return false;
        }

        protected bool BuildPeptideSearchLibrary(CancelEventArgs e, bool isFeatureDetection, bool showWarnings = true)
        {
            try
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
            catch (Exception x)
            {
                ExceptionUtil.DisplayOrReportException(this, x,
                    string.Format(PeptideSearchResources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Failed_to_build_the_library__0__,
                        Path.GetFileName(BiblioSpecLiteSpec.GetLibraryFileName(DocumentFilePath))));
                e.Cancel = true;
                return false;
            }
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

            OnWizardClosing();

            // Stop (and join) any background score-type detection the build-library grid started when files
            // were added, so no background work -- or the temp file it feeds to BlibBuild -- outlives this wizard.
            BuildPepSearchLibControl?.Grid?.CancelScoreTypeDetection();

            base.OnFormClosing(e);
        }

        /// <summary>
        /// Called when the wizard is about to close, to stop and release anything the wizard started.
        /// </summary>
        protected virtual void OnWizardClosing()
        {
        }

        // ILongWaitForm: the wizard is "busy" (and the connector's no-progress watchdog must not trip) while it
        // is driving a long-running background operation in its own progress display rather than a LongWaitDlg.
        public virtual bool IsBusy => false;

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

        private UserControl GetPageControl(TabPage tabPage)
        {
            // Assume each tabPage only has a single UserControl; if that changes this function will need to be updated
            return tabPage.Controls.OfType<UserControl>().SingleOrDefault();
        }

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

        #region Functional testing support

        public IFormView ShowingFormView
        {
            get
            {
                var currentPage = Pages.spectra_page;
                Invoke(new Action(() => currentPage = CurrentPage));
                if (currentPage == Pages.full_scan_settings_page && !IsDdaWorkflow)
                {
                    if (TransitionSettingsControl.IonFilter)
                        return new ImsFullScanPage();
                    else
                        return new Ms2FullScanPage();
                }
                if (currentPage == Pages.chromatograms_page && WorkflowType == Workflow.dia)
                    return new ChromatogramsDiaPage();
                return PAGE_VIEWS[currentPage]();
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
