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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
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
            TestFilesZip = @"Test\EncyclopeDiaHelpersTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Try to find DIA-NN - check configured path, DIANN_PATH env var, then common locations
            string diannPath = DiannHelpers.DiannBinary;
            if (!File.Exists(diannPath))
                diannPath = Environment.GetEnvironmentVariable(@"DIANN_PATH");
            if (diannPath == null || !File.Exists(diannPath))
            {
                Console.Error.WriteLine(@"NOTE: skipping DIA-NN test because DIA-NN is not installed (configure path via Edit > Search Tools)");
                return;
            }

            // Configure the search tool path
            Settings.Default.SearchToolList.Add(new SearchTool(SearchToolType.DIANN, diannPath, string.Empty, Path.GetDirectoryName(diannPath), false));

            PrepareDocument("DiannSearchTest.sky");
            string fastaFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705.fasta");

            // Open DIA-NN search dialog
            var searchDlg = ShowDialog<DiannSearchDlg>(SkylineWindow.ShowDiannSearchDlg);

            // Page 0: Data files - add wide window DIA file
            RunUI(() => Assert.IsTrue(searchDlg.CurrentPage == DiannSearchDlg.Pages.data_files_page));
            var browseDlg = ShowDialog<OpenDataSourceDialog>(() => searchDlg.DataFileResults.Browse());
            RunUI(() => browseDlg.SelectFile("23aug2017_hela_serum_timecourse_wide_1d.mzML"));
            OkDialog(browseDlg, browseDlg.Open);

            // Page 1: FASTA
            RunUI(searchDlg.NextPage);
            RunUI(() =>
            {
                Assert.IsTrue(searchDlg.CurrentPage == DiannSearchDlg.Pages.fasta_page);
                searchDlg.ImportFastaControl.SetFastaContent(fastaFilepath);
            });

            // Page 2: Search settings
            RunUI(searchDlg.NextPage);
            RunUI(() =>
            {
                Assert.IsTrue(searchDlg.CurrentPage == DiannSearchDlg.Pages.search_settings_page);
                // Use auto-detect tolerances (0), lower thread count for test
                searchDlg.Ms1Tolerance = 0;
                searchDlg.Ms2Tolerance = 0;
                searchDlg.QValueThreshold = 0.01;
                searchDlg.Threads = Math.Min(4, Environment.ProcessorCount);
            });

            // Page 3: Start search
            RunUI(searchDlg.NextPage);

            // Wait for search to complete (DIA-NN can take a while)
            try
            {
                bool? searchSucceeded = null;
                searchDlg.SearchControl.SearchFinished += success => searchSucceeded = success;
                WaitForConditionUI(300000, () => searchSucceeded.HasValue); // 5 minute timeout
                RunUI(() => Assert.IsTrue(searchSucceeded.Value, searchDlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText("DiannSearchControlLog.txt", searchDlg.SearchControl.LogText);
            }

            // Verify speclib was created
            Assert.IsTrue(File.Exists(searchDlg.SearchControl.OutputSpecLibPath),
                @"DIA-NN output spectral library not found");

            // Transition to Import Peptide Search wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(searchDlg.NextPage);
            WaitForDocumentLoaded();

            // Should be on spectra page with the speclib loaded
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
            });

            // Cancel out of the import wizard for now
            OkDialog(importPeptideSearchDlg, importPeptideSearchDlg.ClickCancelButton);
            OkDialog(searchDlg, () => searchDlg.DialogResult = System.Windows.Forms.DialogResult.Cancel);
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
