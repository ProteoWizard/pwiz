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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    /// <summary>
    /// Imports the results of a peptide search that was already done: builds a spectral library
    /// from the search result files (or uses an existing library) as soon as the user leaves the
    /// first page, then finds the spectrum files to extract chromatograms from, matches
    /// modifications, configures transition and full-scan settings and imports a FASTA.
    /// </summary>
    public class ImportPeptideSearchDlg : PeptideSearchWizardDlg
    {
        private bool _skipModificationsPage;

        public ImportPeptideSearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager, Workflow? workflowType)
            : base(skylineWindow, libraryManager, false, workflowType)
        {
            lblSettingsPreset.Visible = false;
            cbSettingsPreset.Visible = false;
            btnSavePreset.Visible = false;

            if (workflowType.HasValue)
                BuildPepSearchLibControl.ForceWorkflow(workflowType.Value);

            SetWizardPages(new[]
            {
                new WizardPage(Pages.spectra_page, () => true, LeaveSpectraPage,
                    () => MakeFullScanSettingsControl(WorkflowType)), // reset UI to default
                new WizardPage(Pages.chromatograms_page, () => true, LeaveChromatogramsPageForImport),
                new WizardPage(Pages.match_modifications_page, () => !_skipModificationsPage, LeaveModificationsPage,
                    () => RestoreDocumentBeforePage(Pages.match_modifications_page)),
                new WizardPage(Pages.transition_settings_page, () => WorkflowType != Workflow.dda, LeaveTransitionSettingsPage,
                    () => RestoreDocumentBeforePage(Pages.transition_settings_page)),
                new WizardPage(Pages.full_scan_settings_page, () => true, LeaveFullScanSettingsPage,
                    ReturnToFullScanSettingsPage),
                new WizardPage(Pages.import_fasta_page, () => !BuildPepSearchLibControl.FilterForDocumentPeptides, LeaveFastaPage)
            });
        }

        /// <summary>
        /// Starts the wizard on the chromatograms page with a library another search just made
        /// (EncyclopeDIA, Osprey) and the spectrum files that search used.
        /// </summary>
        public ImportPeptideSearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager, Workflow workflowType,
            IList<ImportPeptideSearch.FoundResultsFile> resultFiles, ImportFastaControl.ImportFastaSettings fastaSettings,
            IEnumerable<string> existingLibraryFilepaths)
            : this(skylineWindow, libraryManager, workflowType)
        {
            BuildPepSearchLibControl.UseExistingLibrary = true;
            BuildPepSearchLibControl.ExistingLibraryPath = existingLibraryFilepaths.First();

            ImportFastaControl.SetFastaContent(fastaSettings.FastaFile.Path, true);
            ImportFastaControl.Enzyme = fastaSettings.Enzyme;
            ImportFastaControl.MaxMissedCleavages = fastaSettings.MaxMissedCleavages;

            _skipModificationsPage = true;

            NextPage(); // skip use existing library page

            ImportResultsControl.FoundResultsFiles = resultFiles;
        }

        /// <summary>
        /// Seed the chromatograms page's spectrum-source map with files we already know
        /// the paths of (e.g. DiannSearchDlg passes the same DIA files that were just
        /// searched). The key and display Name both use the filename without extension,
        /// matching the BlibBuild library's stored fileName so the doc-library scan in
        /// <see cref="ImportPeptideSearch.InitializeSpectrumSourceFiles"/> finds these
        /// entries (via ContainsKey) and leaves them in place rather than emitting a
        /// second, unpopulated entry that would trigger a rename / prefix-suffix dialog.
        /// </summary>
        public void PrefillFoundResultsFiles(IEnumerable<ImportPeptideSearch.FoundResultsFile> files)
        {
            foreach (var f in files)
            {
                string name = Path.GetFileNameWithoutExtension(f.Path);
                ImportPeptideSearch.SpectrumSourceFiles[name] =
                    new ImportPeptideSearch.FoundResultsFilePossibilities(name, f.Path);
            }
        }

        private bool LeaveSpectraPage()
        {
            ImportPeptideSearch.IsDDASearch = false;
            ImportPeptideSearch.IsDIASearch = false;
            ImportFastaControl.IsDDASearch = false;
            if (!BuildPepSearchLibControl.UseExistingLibrary)
            {
                HasPeakBoundaries = BuildPepSearchLibControl.SearchFilenames.All(f =>
                    f.EndsWith(BiblioSpecLiteBuilder.EXT_TSV));
                if (BuildPepSearchLibControl.SearchFilenames.Any(f =>
                        f.EndsWith(BiblioSpecLiteBuilder.EXT_TSV)) && !HasPeakBoundaries)
                {
                    MessageDlg.Show(this,
                        PeptideSearchResources
                            .ImportPeptideSearchDlg_NextPage_Cannot_build_library_from_OpenSWATH_results_mixed_with_results_from_other_tools_);
                    return false;
                }
            }

            if (!BuildPeptideSearchLibraryOrCloseWizard())
                return false;

            PrepareFollowingPages(null);
            SubscribeResultsFilesChanged();
            return true;
        }

        private bool LeaveChromatogramsPageForImport()
        {
            if (!ImportPeptideSearch.VerifyRetentionTimes(ImportResultsControl.FoundResultsFiles.Select(f => f.Path)))
            {
                MessageDlg.Show(this, TextUtil.LineSeparate(Resources.ImportPeptideSearchDlg_NextPage_The_document_specific_spectral_library_does_not_have_valid_retention_times_,
                        Resources.ImportPeptideSearchDlg_NextPage_Please_check_your_peptide_search_pipeline_or_contact_Skyline_support_to_ensure_retention_times_appear_in_your_spectral_libraries_));
                CloseWizard(DialogResult.Cancel);
                return false;
            }
            return LeaveChromatogramsPage();
        }

        private bool LeaveFastaPage()
        {
            return FastaOptional && !ImportFastaControl.ContainsFastaContent ||
                   ImportFastaControl.ImportFasta(ImportPeptideSearch.IrtStandard);
        }
    }
}
