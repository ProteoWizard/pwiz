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
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class ImportPeptideSearchDlg : Form
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

            FullScanSettingsControl = new FullScanSettingsControl(SkylineWindow)
                                           {
                                               Anchor = (AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right),
                                               Location = new Point(2, 60)
                                           };
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

        public BuildPeptideSearchLibraryControl BuildPepSearchLibControl { get; private set; }
        public ImportFastaControl ImportFastaControl { get; private set; }
        public FullScanSettingsControl FullScanSettingsControl { get; private set; }
        public ImportResultsControl ImportResultsControl { get; private set; }

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

                        // Todo: This needs to be moved under Pages.match_modifications_page when that page is finally enabled!
                        // The next page is going to be the MS1 Full-Scan Settings
                        // page, so initialize it.
                        FullScanSettingsControl.InitializeMs1FullScanSettingsPage();

                        // Todo: The next page should be the modifications page, but we skip past it for now. We'll revisit once everything else is working.
                        CurrentPage++;
                    }
                    break;

                case Pages.match_modifications_page:
                    break;

                case Pages.ms1_full_scan_settings_page:
                    // Try to accept changes to MS1 full-scan settings
                    if (!FullScanSettingsControl.UpdateMS1FullScanSettings())
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

            int lastPageIndex = wizardPagesImportPeptideSearch.TabCount - 1;
            if (CurrentPage == (Pages)lastPageIndex)
            {
                btnNext.Text = Resources.ImportPeptideSearchDlg_NextPage_Finish;
            }
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
            btnNext.Enabled = e.NumMissingFiles == 0;
        }

        #region Modifications page

        // Todo: We "hide" the modifications page for now so skip past it. Need to revisit this later!

        private void matchModsTextBox_Enter(object sender, EventArgs e)
        {
            // On the match modifications page, we don't want the text box
            // to get focus because we don't want the text to be selected.
            modificationsListBox.Focus();
        }

        #endregion

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
