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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class ImportPeptideSearchDlg : FormEx, IMultipleViewProvider
    {
        public enum Pages
        {
            spectra_page,
            chromatograms_page,
            match_modifications_page,
            transition_settings_page,
            full_scan_settings_page,
            import_fasta_page
        }

        public enum Workflow
        {
            dda,
            prm,
            dia
        };

        public class SpectraPage : IFormView { }
        public class ChromatogramsPage : IFormView { }
        public class MatchModsPage : IFormView { }
        public class TransitionSettingsPage : IFormView { }
        public class Ms1FullScanPage : IFormView { }
        public class FastaPage : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new SpectraPage(), new ChromatogramsPage(), new MatchModsPage(), new TransitionSettingsPage(), new Ms1FullScanPage(), new FastaPage()
        };

        public ImportPeptideSearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager)
        {
            SkylineWindow = skylineWindow;
            ImportPeptideSearch = new ImportPeptideSearch();

            InitializeComponent();

            Icon = Resources.Skyline;

            CurrentPage = Pages.spectra_page;
            btnNext.Text = Resources.ImportPeptideSearchDlg_ImportPeptideSearchDlg_Next;
            AcceptButton = btnNext;
            btnNext.Enabled = HasUnmatchedLibraryRuns(SkylineWindow.DocumentUI);

            // Create and add wizard pages
            BuildPepSearchLibControl = new BuildPeptideSearchLibraryControl(SkylineWindow, ImportPeptideSearch, libraryManager)
            {
                Anchor = (AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right),
                Location = new Point(2, 50)
            };
            BuildPepSearchLibControl.InputFilesChanged += BuildPepSearchLibForm_OnInputFilesChanged;
            buildSearchSpecLibPage.Controls.Add(BuildPepSearchLibControl);

            ImportFastaControl = new ImportFastaControl(SkylineWindow)
            {
                Anchor = (AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right),
                Location = new Point(2, 60)
            };
            importFastaPage.Controls.Add(ImportFastaControl);

            MatchModificationsControl = new MatchModificationsControl(SkylineWindow, ImportPeptideSearch)
            {
                Anchor = (AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right),
                Location = new Point(2, 60)
            };
            matchModificationsPage.Controls.Add(MatchModificationsControl);

            TransitionSettingsControl = new TransitionSettingsControl(SkylineWindow)
            {
                Anchor = (AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right),
                Location = new Point(18, 60)
            };
            transitionSettingsUiPage.Controls.Add(TransitionSettingsControl);

            FullScanSettingsControl = new FullScanSettingsControl(SkylineWindow)
            {
                Anchor = (AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right),
                Location = new Point(18, 50)
            };
            ms1FullScanSettingsPage.Controls.Add(FullScanSettingsControl);

            ImportResultsControl = new ImportResultsControl(ImportPeptideSearch, SkylineWindow.DocumentFilePath)
            {
                Anchor = (AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right),
                Location = new Point(2, 60)
            };
            getChromatogramsPage.Controls.Add((Control) ImportResultsControl);

            _pagesToSkip = new HashSet<Pages>();
        }

        public ImportPeptideSearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager, Workflow workflowType)
            : this(skylineWindow, libraryManager)
        {
            BuildPepSearchLibControl.ForceWorkflow(workflowType);

            if (workflowType == Workflow.dda)
            {
                int shortHeight = MinimumSize.Height - 110;
                MinimumSize = new Size(MinimumSize.Width, shortHeight);
                Height = shortHeight;
            }
        }

        private SkylineWindow SkylineWindow { get; set; }
        private ImportPeptideSearch ImportPeptideSearch { get; set; }
        private TransitionSettings TransitionSettings { get { return SkylineWindow.DocumentUI.Settings.TransitionSettings; } }
        public TransitionFullScan FullScan { get { return TransitionSettings.FullScan; } }

        public BuildPeptideSearchLibraryControl BuildPepSearchLibControl { get; private set; }
        public ImportFastaControl ImportFastaControl { get; private set; }
        public TransitionSettingsControl TransitionSettingsControl { get; private set; }
        public FullScanSettingsControl FullScanSettingsControl { get; private set; }
        public IImportResultsControl ImportResultsControl { get; private set; }
        public MatchModificationsControl MatchModificationsControl { get; private set; }

        public Workflow WorkflowType { get { return BuildPepSearchLibControl.WorkflowType; } }

        private bool FastaOptional { get { return !BuildPepSearchLibControl.FilterForDocumentPeptides && SkylineWindow.Document.PeptideCount > 0; } }
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
                    foreach (var dataFile in documentLibrary.LibraryDetails.DataFiles)
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
                        var eCancel = new CancelEventArgs();
                        if (!BuildPepSearchLibControl.BuildPeptideSearchLibrary(eCancel))
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

                        if (FastaOptional)
                            lblFasta.Text = Resources.ImportPeptideSearchDlg_NextPage_Import_FASTA__optional_;

                        // The next page is going to be the chromatograms page.
                        var oldImportResultsControl = (ImportResultsControl) ImportResultsControl;

                        if (WorkflowType != Workflow.dia)
                        {
                            oldImportResultsControl.InitializeChromatogramsPage(SkylineWindow.DocumentUI);

                            if (WorkflowType == Workflow.dda)
                            {
                                _pagesToSkip.Add(Pages.transition_settings_page);
                            }
                        }
                        else
                        {
                            // DIA workflow, replace old ImportResultsControl
                            ImportResultsControl = new ImportResultsDIAControl(SkylineWindow)
                            {
                                Anchor = oldImportResultsControl.Anchor,
                                Location = oldImportResultsControl.Location
                            };
                            getChromatogramsPage.Controls.Remove(oldImportResultsControl);
                            getChromatogramsPage.Controls.Add((Control)ImportResultsControl);
                        }
                        ImportResultsControl.ResultsFilesChanged += ImportResultsControl_OnResultsFilesChanged;

                        // Set up full scan settings page
                        TransitionSettingsControl.Initialize(WorkflowType);
                        FullScanSettingsControl.ModifyOptionsForImportPeptideSearchWizard(WorkflowType);

                        if (!MatchModificationsControl.Initialize(SkylineWindow.Document))
                            _pagesToSkip.Add(Pages.match_modifications_page);
                        if (BuildPepSearchLibControl.FilterForDocumentPeptides)
                            _pagesToSkip.Add(Pages.import_fasta_page);
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

                        if (ImportResultsControl.FoundResultsFiles.Count > 1)
                        {
                            string prefix = ImportResultsDlg.GetCommonPrefix(ImportResultsControl.FoundResultsFiles.Select(f => f.Name));
                            if (prefix.Length >= ImportResultsDlg.MIN_COMMON_PREFIX_LENGTH)
                            {
                                using (var dlgName = new ImportResultsNameDlg(prefix))
                                {
                                    var result = dlgName.ShowDialog(this);
                                    if (result == DialogResult.Cancel)
                                    {
                                        return;
                                    }
                                    else if (result == DialogResult.Yes)
                                    {
                                        // Rename all the replicates to remove the specified prefix.
                                        ImportResultsControl.FoundResultsFiles = ImportResultsControl.FoundResultsFiles.Select(f =>
                                            new ImportPeptideSearch.FoundResultsFile(f.Name.Substring(dlgName.Prefix.Length), f.Path)).ToList();
                                    }
                                }
                            }
                        }
                    }
                    break;

                case Pages.match_modifications_page:
                    {
                        var newSettings = MatchModificationsControl.AddCheckedModifications(SkylineWindow.Document);
                        if (!ReferenceEquals(SkylineWindow.Document.Settings, newSettings))
                        {
                            SkylineWindow.ChangeSettings(newSettings, true, Resources.MatchModificationsControl_AddCheckedModifications_Add_checked_modifications);
                            SkylineWindow.Document.Settings.UpdateDefaultModifications(false);
                        }

                        // The next page is going to be the MS1 Full-Scan Settings
                        // page, so initialize it.
                        // FullScanSettingsControl.InitializeMs1FullScanSettingsPage();
                    }
                    break;

                case Pages.transition_settings_page:
                    // Try to accept changes to transition settings
                    if (!UpdateTransitionSettings())
                    {
                        return;
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

                case Pages.import_fasta_page: // This is the last page
                    if ((FastaOptional && !ImportFastaControl.ContainsFastaContent) ||
                        ImportFastaControl.ImportFasta())
                    {
                        WizardFinish();
                    }
                    return;
            }

            Pages newPage = CurrentPage + 1;

            while (_pagesToSkip.Contains(newPage))
            {
                ++newPage;
            }

            // Skip import FASTA if user filters for document peptides
            if (newPage > Pages.import_fasta_page)
            {
                WizardFinish();
                return;
            }

            CurrentPage = newPage;
            if (CurrentPage == LastPage)
            {
                btnNext.Text = Resources.ImportPeptideSearchDlg_NextPage_Finish;
            }
        }

        private bool UpdateTransitionSettings()
        {
            TransitionSettings newTransitionSettings = TransitionSettingsControl.GetTransitionSettings(this);
            if (newTransitionSettings == null)
                return false;

            // Only update, if anything changed
            Helpers.AssignIfEquals(ref newTransitionSettings, TransitionSettings);
            return Equals(newTransitionSettings, TransitionSettings) || SkylineWindow.ChangeSettings(SkylineWindow.DocumentUI.Settings.ChangeTransitionSettings(newTransitionSettings), true);
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
            if (precursorIsotopes != FullScanPrecursorIsotopes.None &&
                    precursorAnalyzerType != FullScanMassAnalyzerType.qit)
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

            TransitionFilter filter = TransitionSettings.Filter;
            if (FullScanSettingsControl.PrecursorChargesTextBox.Visible)
            {
                int[] precursorCharges;
                if (!helper.ValidateNumberListTextBox(FullScanSettingsControl.PrecursorChargesTextBox, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE, out precursorCharges))
                    return false;
                precursorCharges = precursorCharges.Distinct().ToArray();
                filter = TransitionSettings.Filter.ChangePrecursorCharges(precursorCharges);
            }
            if (WorkflowType == Workflow.dda && !filter.IonTypes.Contains(IonType.precursor))
                filter = filter.ChangeIonTypes(new[] {IonType.precursor});
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

            TransitionSettings settings;
            try
            {
                 settings = new TransitionSettings(prediction, filter,
                     TransitionSettings.Libraries, TransitionSettings.Integration, TransitionSettings.Instrument, fullScan);
            }
            catch (Exception x)
            {
                MessageDlg.Show(this, x.Message);
                return false;
            }

            // Only update, if anything changed
            if (!Equals(settings, TransitionSettings))
            {
                SrmSettings newSettings = SkylineWindow.DocumentUI.Settings.ChangeTransitionSettings(settings);
                if (!SkylineWindow.ChangeSettings(newSettings, true))
                {
                    return false;
                }
            }

            // MS1 filtering must be enabled
            if (!FullScan.IsEnabledMs)
            {
                MessageDlg.Show(this, Resources.ImportPeptideSearchDlg_UpdateFullScanSettings_Full_scan_MS1_filtering_must_be_enabled_in_order_to_import_a_peptide_search_);
                return false;
            }

            return true;
        }

        private void ShowEarlyFinish(bool show)
        {
            btnEarlyFinish.Visible = show;
        }

        public void WizardFinish()
        {
            // Import results only on "finish"

            var namedResults =
                ImportResultsControl.FoundResultsFiles.Select(
                    kvp => new KeyValuePair<string, MsDataFileUri[]>(kvp.Name, new[] { new MsDataFilePath(kvp.Path) }))
                    .ToList();

            // Ask about lockmass correction, if needed - lockmass settings in namedResults will be updated by this call as needed
            if (!ImportResultsLockMassDlg.UpdateNamedResultsParameters(this, SkylineWindow.DocumentUI, ref namedResults))
            {
                CloseWizard(DialogResult.Cancel);  // User cancelled, no change
            }
            else
            {
                SkylineWindow.ModifyDocument(Resources.ImportResultsControl_GetPeptideSearchChromatograms_Import_results,
                    doc => SkylineWindow.ImportResults(doc, namedResults, ExportOptimize.NONE));
                CloseWizard(DialogResult.OK);
            }
        }

        public void WizardEarlyFinish()
        {
            if (CurrentPage == Pages.spectra_page)
            {
                var eCancel = new CancelEventArgs();
                if (!BuildPepSearchLibControl.BuildPeptideSearchLibrary(eCancel))
                {
                    if (eCancel.Cancel)
                        return;

                    CloseWizard(DialogResult.Cancel);
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
            ImportPeptideSearch.ClosePeptideSearchLibraryStreams(SkylineWindow.DocumentUI);
            DialogResult = result;
        }

        private void BuildPepSearchLibForm_OnInputFilesChanged(object sender,
            BuildPeptideSearchLibraryControl.InputFilesChangedEventArgs e)
        {
            int numInputFiles = e.NumInputFiles;
            btnNext.Enabled = numInputFiles > 0;
            if (btnEarlyFinish.Visible)
            {
                btnEarlyFinish.Enabled = numInputFiles > 0;
            }
        }

        private void ImportResultsControl_OnResultsFilesChanged(object sender,
            ImportResultsControl.ResultsFilesEventArgs e)
        {
            btnNext.Enabled = e.NumFoundFiles > 0;
        }

        #region Functional testing support

        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = wizardPagesImportPeptideSearch.SelectedIndex));
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
