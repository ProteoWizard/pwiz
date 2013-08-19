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
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class ImportPeptideSearchDlg : FormEx
    {
        public enum Pages
        {
            spectra_page,
            chromatograms_page,
            match_modifications_page,
            ms1_full_scan_settings_page,
            import_fasta_page
        }

        public ImportPeptideSearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager)
        {
            SkylineWindow = skylineWindow;

            InitializeComponent();

            Icon = Resources.Skyline;

            CurrentPage = Pages.spectra_page;
            btnNext.Text = Resources.ImportPeptideSearchDlg_ImportPeptideSearchDlg_Next;
            AcceptButton = btnNext;
            btnNext.Enabled = false;

            // Create and add wizard pages
            BuildPepSearchLibControl = new BuildPeptideSearchLibraryControl(SkylineWindow, libraryManager)
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

            MatchModificationsControl = new MatchModificationsControl(SkylineWindow)
                                            {
                                                Anchor = (AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right),
                                                Location = new Point(2, 60)
                                            };
            matchModificationsPage.Controls.Add(MatchModificationsControl);

            FullScanSettingsControl = new FullScanSettingsControl(SkylineWindow)
                                           {
                                               Anchor = (AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right),
                                               Location = new Point(18, 50)
                                           };
            FullScanSettingsControl.ModifyOptionsForImportPeptideSearchWizard();
            ms1FullScanSettingsPage.Controls.Add(FullScanSettingsControl);

            ImportResultsControl = new ImportResultsControl(SkylineWindow)
                                        {
                                            Anchor = (AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right),
                                            Location = new Point(2, 60)
                                        };
            ImportResultsControl.ResultsFilesChanged += ImportResultsControl_OnResultsFilesChanged;
            getChromatogramsPage.Controls.Add(ImportResultsControl);
        }

        private SkylineWindow SkylineWindow { get; set; }
        private TransitionSettings TransitionSettings { get { return SkylineWindow.DocumentUI.Settings.TransitionSettings; } }
        public TransitionFullScan FullScan { get { return TransitionSettings.FullScan; } }

        public BuildPeptideSearchLibraryControl BuildPepSearchLibControl { get; private set; }
        public ImportFastaControl ImportFastaControl { get; private set; }
        public FullScanSettingsControl FullScanSettingsControl { get; private set; }
        public ImportResultsControl ImportResultsControl { get; private set; }
        public MatchModificationsControl MatchModificationsControl { get; private set; }

        private Library DocLib { get { return BuildPepSearchLibControl.DocLib; } }

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

                        // The next page is going to be the chromatograms page.
                        ImportResultsControl.InitializeChromatogramsPage(DocLib);
                    }
                    break;

                case Pages.chromatograms_page:
                    {
                        if (!BuildPepSearchLibControl.VerifyRetentionTimes(ImportResultsControl.FoundResultsFiles))
                        {
                            MessageDlg.Show(this, TextUtil.LineSeparate(Resources.ImportPeptideSearchDlg_NextPage_The_document_specific_spectral_library_does_not_have_valid_retention_times_,
                                Resources.ImportPeptideSearchDlg_NextPage_Please_check_your_peptide_search_pipeline_or_contact_Skyline_support_to_ensure_retention_times_appear_in_your_spectral_libraries_));
                            CloseWizard(DialogResult.Cancel);
                        }

                        if (ImportResultsControl.MissingResultsFiles.Count > 0)
                        {
                            if (MessageBox.Show(this, Resources.ImportPeptideSearchDlg_NextPage_Some_results_files_are_still_missing__Are_you_sure_you_want_to_continue_,
                                Program.Name, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                            {
                                return;
                            }
                        }
                    }
                    break;

                case Pages.match_modifications_page:
                    {
                        MatchModificationsControl.AddCheckedModifications();

                        // The next page is going to be the MS1 Full-Scan Settings
                        // page, so initialize it.
                        // FullScanSettingsControl.InitializeMs1FullScanSettingsPage();
                    }
                    break;

                case Pages.ms1_full_scan_settings_page:
                    // Try to accept changes to MS1 full-scan settings
                    if (!UpdateFullScanSettings())
                    {
                        // We can't allow the user to progress any further until
                        // we can verify that the MS1 full scan settings are valid.
                        return;
                    }
                    break;

                case Pages.import_fasta_page: // This is the last page
                    if (!ImportFastaControl.ImportFasta())
                    {
                        return;
                    }

                    // This is the last page, so go ahead and finish
                    WizardFinish();
                    return;
            }

            CurrentPage++;

            // Initialize modifications page or skip if no modifications.
            if (CurrentPage == Pages.match_modifications_page &&
                !MatchModificationsControl.Initialize(DocLib))
            {
                CurrentPage++;
            }
            if (CurrentPage == Pages.ms1_full_scan_settings_page &&
                SkylineWindow.DocumentUI.Settings.TransitionSettings.FullScan.IsEnabled)
            {
                // If the user has already set up full-scan settings, then just continue, since
                // they may have any kind of settings, like PRM or DIA
                CurrentPage++;
            }

            int lastPageIndex = wizardPagesImportPeptideSearch.TabCount - 1;
            if (CurrentPage == (Pages)lastPageIndex)
            {
                btnNext.Text = Resources.ImportPeptideSearchDlg_NextPage_Finish;
            }
        }

        private bool UpdateFullScanSettings()
        {
            var e = new CancelEventArgs();
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



            int[] precursorCharges;
            if (!helper.ValidateNumberListTextBox(e, FullScanSettingsControl.PrecursorChargesTextBox, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE, out precursorCharges))
                return false;
            precursorCharges = precursorCharges.Distinct().ToArray();
            var filter = TransitionSettings.Filter.ChangePrecursorCharges(precursorCharges);
            if (!filter.IonTypes.Contains(IonType.precursor))
                filter = filter.ChangeIonTypes(new[] {IonType.precursor});
            if (!filter.AutoSelect)
                filter = filter.ChangeAutoSelect(true);
            Helpers.AssignIfEquals(ref filter, TransitionSettings.Filter);

            TransitionFullScan fullScan;
            if (!FullScanSettingsControl.ValidateFullScanSettings(e, helper, out fullScan))
                return false;

            Helpers.AssignIfEquals(ref fullScan, TransitionSettings.FullScan);

            var prediction = TransitionSettings.Prediction.ChangePrecursorMassType(precursorMassType);
            Helpers.AssignIfEquals(ref prediction, TransitionSettings.Prediction);

            TransitionSettings settings = new TransitionSettings(prediction, filter,
                TransitionSettings.Libraries, TransitionSettings.Integration, TransitionSettings.Instrument, fullScan);

            // Only update, if anything changed
            if (!Equals(settings, TransitionSettings))
            {
                SrmSettings newSettings = SkylineWindow.DocumentUI.Settings.ChangeTransitionSettings(settings);
                if (!SkylineWindow.ChangeSettings(newSettings, true))
                {
                    e.Cancel = true;
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
            ImportResultsControl.GetPeptideSearchChromatograms();
            CloseWizard(DialogResult.OK);
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
            BuildPepSearchLibControl.ClosePeptideSearchLibraryStreams();
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
