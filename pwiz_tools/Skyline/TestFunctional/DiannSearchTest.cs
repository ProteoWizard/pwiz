/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DiannSearchTest : AbstractFunctionalTestEx
    {
        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE),
         NoLeakTesting(TestExclusionReason.EXCESSIVE_TIME)]
        public void TestDiannSearch()
        {
            TestFilesZip = @"TestFunctional\DiannSearchTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            PrepareDocument("DiannSearchTest.sky");

            // Decide which real DIA-NN binary to drive the search with. If the user has
            // already configured a working DIA-NN in SearchToolList (Edit > Search Tools),
            // honor that rather than re-downloading. Otherwise fall back to the Skyline
            // tool testing S3 mirror (cached across runs).
            SearchTool preConfigured = null;
            RunUI(() =>
            {
                if (Settings.Default.SearchToolList.ContainsKey(SearchToolType.DIANN))
                {
                    var candidate = Settings.Default.SearchToolList[SearchToolType.DIANN];
                    if (File.Exists(candidate.Path))
                        preConfigured = candidate;
                }
            });
            string realDiannPath;
            if (preConfigured != null)
            {
                realDiannPath = preConfigured.Path;
            }
            else
            {
                var progress = new SilentProgressMonitor();
                AssertEx.IsTrue(SimpleFileDownloader.DownloadRequiredFiles(DiannHelpers.FilesToDownload, progress));
                realDiannPath = DiannHelpers.DiannBinary;
            }
            AssertEx.IsTrue(File.Exists(realDiannPath));

            DiannSearchDlg searchDlg;
            try
            {
                // Point SearchToolList at a path that doesn't exist on disk so DiannBinary
                // resolves to a non-existent path (forcing EnsureDiannInstalled to prompt),
                // regardless of what's actually present in the default Tools directory.
                const string bogusPath = @"C:\__diann_not_installed__\diann.exe";
                RunUI(() =>
                {
                    if (Settings.Default.SearchToolList.ContainsKey(SearchToolType.DIANN))
                        Settings.Default.SearchToolList.Remove(
                            Settings.Default.SearchToolList[SearchToolType.DIANN]);
                    Settings.Default.SearchToolList.Add(new SearchTool(SearchToolType.DIANN,
                        bogusPath, string.Empty, Path.GetDirectoryName(bogusPath), false));
                });

                // Part A: no DIA-NN registered anywhere -> DiannDownloadDlg appears.
                DiannHelpers.RegisteredDiannPathOverride = () => null;
                var downloadDlg = ShowDialog<DiannDownloadDlg>(SkylineWindow.ShowDiannSearchDlg);
                OkDialog(downloadDlg, () => downloadDlg.DialogResult = DialogResult.Cancel);
                AssertEx.IsNull(FindOpenForm<DiannSearchDlg>());

                // Part B: an existing DIA-NN install is "discovered" via registry ->
                // MultiButtonMsgDlg asks the user to reuse it or download; choose "Use Existing".
                DiannHelpers.RegisteredDiannPathOverride = () => realDiannPath;
                var useExisting = ShowDialog<MultiButtonMsgDlg>(SkylineWindow.ShowDiannSearchDlg);
                OkDialog(useExisting, () => useExisting.DialogResult = DialogResult.Yes);
                searchDlg = WaitForOpenForm<DiannSearchDlg>();
                RunUI(() => AssertEx.AreEqual(realDiannPath, DiannHelpers.DiannBinary));
            }
            finally
            {
                DiannHelpers.RegisteredDiannPathOverride = null;
            }

            string fastaFilepath = TestFilesDir.GetTestPath("pan_human_library.fasta");
            // Single-file DIA-NN search exercises the no-MBR path. Multi-file coverage
            // belongs in a perf test; this functional test is intentionally minimal.
            string[] diaFilePaths =
            {
                TestFilesDir.GetTestPath("23aug2017_hela_serum_timecourse_wide_1c.mzML")
            };

            // Page 0: Data files - add wide window DIA file
            RunUI(() => AssertEx.IsTrue(searchDlg.CurrentPage == DiannSearchDlg.Pages.data_files_page));
            RunUI(() => searchDlg.DataFileResults.FoundResultsFiles = diaFilePaths
                .Select(p => new ImportPeptideSearch.FoundResultsFile(Path.GetFileName(p), p)).ToArray());

            // Page 1: FASTA
            RunUI(searchDlg.NextPage);
            RunUI(() =>
            {
                AssertEx.IsTrue(searchDlg.CurrentPage == DiannSearchDlg.Pages.fasta_page);
                searchDlg.ImportFastaControl.SetFastaContent(fastaFilepath);
            });

            // Page 2: Modifications (defaults: Carbamidomethyl C fixed, Oxidation M variable)
            RunUI(searchDlg.NextPage);
            RunUI(() => AssertEx.IsTrue(searchDlg.CurrentPage == DiannSearchDlg.Pages.modifications_page));

            // Page 3: Search settings
            RunUI(searchDlg.NextPage);
            RunUI(() =>
            {
                AssertEx.IsTrue(searchDlg.CurrentPage == DiannSearchDlg.Pages.search_settings_page);
                searchDlg.Ms1Tolerance = 10;
                searchDlg.Ms2Tolerance = 20;
                searchDlg.QValueThreshold = 0.01;
                searchDlg.Threads = Environment.ProcessorCount;
                searchDlg.DiannSearchConfig.AdditionalSettings[@"MinPrecursorCharge"].Value = 2;
                searchDlg.DiannSearchConfig.AdditionalSettings[@"MaxPrecursorCharge"].Value = 3;
            });

            // Page 4: Start search
            RunUI(searchDlg.NextPage);

            // Wait for search to complete (DIA-NN can take a while)
            try
            {
                bool? searchSucceeded = null;
                searchDlg.SearchControl.SearchFinished += success => searchSucceeded = success;
                WaitForConditionUI(300000, () => searchSucceeded.HasValue); // 5 minute timeout
                RunUI(() => AssertEx.IsTrue(searchSucceeded.Value, searchDlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText("DiannSearchControlLog.txt", searchDlg.SearchControl.LogText);
            }

            // Verify spectral library (parquet) was created
            AssertEx.IsTrue(File.Exists(searchDlg.SearchControl.OutputSpecLibPath));

            // Delete any leftover .blib from a previous run so the existing-blib
            // overwrite prompt doesn't fire (that path is covered in a dedicated test).
            string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(SkylineWindow.DocumentFilePath);
            if (File.Exists(docBlibPath))
                File.Delete(docBlibPath);

            // Transition to Import Peptide Search wizard. DiannSearchDlg pre-loads the
            // DIA-NN -lib.parquet as the search input; the wizard opens on the spectra
            // page and BlibBuild converts the parquet to a .blib via DiaNNSpecLibReader.
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(searchDlg.NextPage);
            WaitForDocumentLoaded();
            // BlibBuild score-type lookup on the parquet runs async on the spectra page;
            // wait for it to finish before the Next button can validate.
            WaitForConditionUI(() => importPeptideSearchDlg.BuildPepSearchLibControl.Grid.ScoreTypesLoaded);

            RunUI(() =>
            {
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton());
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);

                // DiannSearchDlg pre-populated the chromatograms page with the searched
                // files, so all should appear in Found and Missing should be empty
                // (no second entry under a different filename casing/extension).
                var importResults = importPeptideSearchDlg.ImportResultsControl;
                var foundPaths = importResults.FoundResultsFiles.Select(f => f.Path).ToHashSet();
                AssertEx.AreEqual(diaFilePaths.Length, foundPaths.Count,
                    $@"expected {diaFilePaths.Length} found result files, got {foundPaths.Count}");
                foreach (var p in diaFilePaths)
                    AssertEx.IsTrue(foundPaths.Contains(p), $@"missing pre-populated file in Found list: {p}");
                AssertEx.IsFalse(importResults.MissingResultsFiles.Any(),
                    $@"unexpected entries in Missing list: {string.Join(@", ", importResults.MissingResultsFiles)}");

                AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton()); // 1 file → no rename dialog
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                // DIA-NN default mods (Carbamidomethyl C, Oxidation M) are detected from
                // the library; add them all so library precursors match document targets.
                importPeptideSearchDlg.MatchModificationsControl.ChangeAll(true);
                AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton());
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
                AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton());
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.IsolationSchemeName =
                    PropertiesResources.IsolationSchemeList_GetDefaults_Results_only;
                AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton());
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled = false;
                importPeptideSearchDlg.ImportFastaControl.AutoTrain = false;
            });

            // Finish wizard → AssociateProteinsDlg. The truncated mzML may not yield enough
            // confident IDs to populate every target type, so we don't assert specific counts
            // here — the value of this test is that the wizard advances cleanly through every
            // page and BlibBuild successfully converts the DIA-NN parquet to a .blib.
            var associateProteinsDlg = ShowDialog<AssociateProteinsDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            WaitForConditionUI(() => associateProteinsDlg.DocumentFinalCalculated);
            using (new WaitDocumentChange(null, true))
            {
                OkDialog(associateProteinsDlg, associateProteinsDlg.OkDialog);
            }

            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(documentFile)));
        }
    }
}
