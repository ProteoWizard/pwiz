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
            string[] diaFilePaths =
            {
                TestFilesDir.GetTestPath("23aug2017_hela_serum_timecourse_wide_1c.mzML"),
                TestFilesDir.GetTestPath("23aug2017_hela_serum_timecourse_wide_1d.mzML")
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

            // Verify speclib was created
            AssertEx.IsTrue(File.Exists(searchDlg.SearchControl.OutputSpecLibPath));

            // Transition to Import Peptide Search wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(searchDlg.NextPage);
            WaitForDocumentLoaded();

            // Should be on spectra page with the speclib loaded
            RunUI(() =>
            {
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
            });

            // Cancel out of the import wizard for now
            OkDialog(importPeptideSearchDlg, importPeptideSearchDlg.ClickCancelButton);
            OkDialog(searchDlg, () => searchDlg.DialogResult = DialogResult.Cancel);
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
