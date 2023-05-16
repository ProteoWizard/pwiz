/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net >
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

//
// Test for Hardklor integration
// 
namespace TestPerf
{
    [TestClass]
    public class FeatureDetectionTest : AbstractFunctionalTest
    {
        public struct ExpectedResults
        {
            public int proteinCount;
            public int peptideCount;
            public int precursorCount;
            public int transitionCount;

            public ExpectedResults(int proteinCount, int peptideCount, int precursorCount, int transitionCount)
            {
                this.proteinCount = proteinCount;
                this.peptideCount = peptideCount;
                this.precursorCount = precursorCount;
                this.transitionCount = transitionCount;
            }
        }

        public struct DdaTestSettings
        {
            private SearchSettingsControl.SearchEngine _searchEngine;
            public SearchSettingsControl.SearchEngine SearchEngine
            {
                get => _searchEngine;
                set
                {
                    _searchEngine = value;
                    HasMissingDependencies = !SearchSettingsControl.HasRequiredFilesDownloaded(value);
                }
            }

            public string FragmentIons { get; set; }
            public string Ms2Analyzer { get; set; }
            public MzTolerance PrecursorTolerance { get; set; }
            public MzTolerance FragmentTolerance { get; set; }
            public List<KeyValuePair<string, string>> AdditionalSettings { get; set; }
            public ExpectedResults ExpectedResults { get; set; }
            public ExpectedResults ExpectedResultsFinal { get; set; }
            public bool HasMissingDependencies { get; private set; }
        }

        public bool UseWiff => false; // ExtensionTestContext.CanImportAbWiff; // Wiff reader fails in msconvert step due to brittle embedded DLL load order when run in RunProcess

        [TestMethod, NoUnicodeTesting(TestExclusionReason.HARDKLOR_UNICODE_ISSUES)]
        public void TestHardklorFeatureDetection()
        {
            TestFilesZipPaths = new[]
            {
                UseWiff
                    ? @"https://skyline.ms/tutorials/MS1Filtering_2.zip" 
                    : @"https://skyline.ms/tutorials/MS1FilteringMzml_2.zip", 
                @"TestPerf\FeatureDetectionTest.zip"
            };
            TestDirectoryName = "HardklorFeatureDetectionTest";

            RunFunctionalTest();
        }

        private string GetDataPath(string path)
        {
            var folderMs1Filtering = UseWiff ? "Ms1Filtering" : "Ms1FilteringMzml";
            return TestFilesDirs[0].GetTestPath(Path.Combine(folderMs1Filtering, path));
        }

        private string GetTestPath(string path)
        {
            return TestFilesDirs[1].GetTestPath(path);
        }

        private string[] SearchFiles
        {
            get
            {
                var ext = UseWiff ? ".wiff" : ".mzML";
                return new[]
                {
                    GetDataPath("100803_0001_MCF7_TiB_L")+ext,
                    GetDataPath("100803_0005b_MCF7_TiTip3")+ext
                };
            }
        }

        private string[] SearchFilesSameName
        {
            get
            {
                return new[]
                {
                    SearchFiles[0],
                    Path.Combine(Path.GetDirectoryName(SearchFiles[0]) ?? string.Empty, "subdir", Path.GetFileName(SearchFiles[0]))
                };
            }
        }

        protected override void DoTest()
        {
            // IsPauseForScreenShots = true; // enable for quick demo

            // Make sure we're testing the mzML conversion
            foreach (var file in SearchFiles)
            {
                AssertEx.IsTrue(File.Exists(file));
                var convertedFile = Path.Combine(Path.GetDirectoryName(file) ?? string.Empty,
                    MsconvertDdaConverter.OUTPUT_SUBDIRECTORY, (Path.GetFileNameWithoutExtension(file)+ @".mzML"));
                if (File.Exists(convertedFile))
                {
                    File.Delete(convertedFile);
                }
            }

            // Load the document that we have at the end of the MS1 fullscan tutorial
            RunUI(() => SkylineWindow.OpenFile(GetTestPath("Ms1FilteringTutorial-2min.sky")));
            WaitForDocumentLoaded();

            PauseForScreenShot("Ready to start Wizard (File > Import > Feature Detection...)");
            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowFeatureDetectionDlg);
            importPeptideSearchDlg.Testing = true; // Prevents form-called-by-form blockage TODO(bspratt) there must be a cleaner way

            // We're on the "Select Files to Search" page of the wizard.

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                Assert.IsTrue(importPeptideSearchDlg.BuildPepSearchLibControl.PerformDDASearch);
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).ToArray();
                AssertEx.AreEqual(ImportPeptideSearchDlg.Workflow.feature_detection, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType); 
            });
            PauseForScreenShot("these are the MS1 Filtering Tutorial files");
            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            PauseForScreenShot<ImportResultsNameDlg>("Common prefix form");

            OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);

            // Test back/next buttons
            PauseForScreenShot("Testing back button");
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
            });
            PauseForScreenShot("and forward again");
            importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            PauseForScreenShot<ImportResultsNameDlg>("Common prefix form again");
            OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);

            RunUI(() =>
            {
                // We're on the MS1 full scan settings page.
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3 };
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.tof; // Per MS1 filtering tutorial
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 10000; // Per MS1 filtering tutorial
            });
            PauseForScreenShot(" MS1 full scan settings page - next we'll tweak the search settings");
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                importPeptideSearchDlg.SearchSettingsControl.HardklorCorrelationThreshold = 0.98; // Default is 0.95, so this should be a change
                importPeptideSearchDlg.SearchSettingsControl.HardklorSignalToNoise = 3.01; // Default is 3.0, so this should be a change
                // The instrument values should be settable since we set them in Full Scan.
                AssertEx.IsFalse(importPeptideSearchDlg.SearchSettingsControl.HardklorInstrumentSettingsAreEditable);

            });
            PauseForScreenShot(" Search settings page - next we'll start the mzML conversion if needed then cancel the search");
            RunUI(() =>
            {
                // Run the search
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            bool? searchSucceeded = null;
            TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.DDASearchPage));   // Stop to show this form during form testing

            if (UseWiff)
            {
                // Wait for the mzML conversion to complete before canceling
                foreach (var searchFile in SearchFiles)
                {
                    var converted = Path.Combine(Path.GetDirectoryName(searchFile) ?? string.Empty,
                        @"converted",
                        Path.ChangeExtension(Path.GetFileName(searchFile), @"mzML"));
                    WaitForCondition(() => File.Exists(converted));
                }
            }

            RunUI(() =>
            {
                importPeptideSearchDlg.SearchControl.OnSearchFinished += (success) => searchSucceeded = success;
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = true;

                // Cancel search
                importPeptideSearchDlg.SearchControl.Cancel();
            });
            WaitForConditionUI(60000, () => searchSucceeded.HasValue);
            Assert.IsFalse(searchSucceeded.Value);
            searchSucceeded = null;
            PauseForScreenShot("search cancelled, now go back and  test 2 input files with the same name in different directories");
            // Go back and test 2 input files with the same name in different directories
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());

                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFilesSameName.Select(o => (MsDataFileUri) new MsDataFilePath(o)).ToArray();
            });
            PauseForScreenShot("same name, different directories");
            var removeSuffix = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            PauseForScreenShot("expected dialog for name reduction - we'll cancel and go back to try unique names");
            OkDialog(removeSuffix, removeSuffix.CancelDialog);

            // Test with 2 files
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).ToArray();
            });

            // With 2 sources, we get the remove prefix/suffix dialog; accept default behavior
            var removeSuffix2 = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            PauseForScreenShot("expected dialog for name reduction ");
            OkDialog(removeSuffix, () => removeSuffix2.YesDialog());


            PauseForScreenShot("Full scan settings - not set Centroided (this data set isn't compatible with that), so instrument settings on next page should not be operable");
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            RunUI(() =>
            {
                // We're on the "Search Settings" page. These values should not be settable since we did not set "Centroided" in Full Scan.
                AssertEx.IsFalse(importPeptideSearchDlg.SearchSettingsControl.HardklorInstrumentSettingsAreEditable);
            });
            PauseForScreenShot("Search settings - set Centroided, so instrument settings are operable.");
            // Now check some value ranges
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorCorrelationThreshold = -1);
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorCorrelationThreshold = 1.1);
            RunUI(() => importPeptideSearchDlg.SearchSettingsControl.HardklorCorrelationThreshold = .98); // Legal
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorSignalToNoise = -1); // Illegal
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorSignalToNoise = 11);
            RunUI(() => importPeptideSearchDlg.SearchSettingsControl.HardklorSignalToNoise = 3.01); // Legal

            void ExpectError(Action act)
            {
                RunUI(() =>
                {                    
                    var page = importPeptideSearchDlg.CurrentPage;
                    act();
                    importPeptideSearchDlg.ClickNextButton();
                    AssertEx.AreEqual(page, importPeptideSearchDlg.CurrentPage); // Not expected to advance
                });
            }

            RunUI(() =>
            {
                var page = importPeptideSearchDlg.CurrentPage;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                AssertEx.AreNotEqual(page, importPeptideSearchDlg.CurrentPage, "stuck?"); // Expected to advance
            });

            try
            {
                WaitForConditionUI(60000, () => searchSucceeded.HasValue);
                RunUI(() => Assert.IsTrue(searchSucceeded.Value, importPeptideSearchDlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText("SearchControlLog.txt", importPeptideSearchDlg.SearchControl.LogText);
            }

            // We've had some strange hard-to-reproduce failures, see if Hardklor outout is stable
            var expectedHardklorFiles = @"expected_hardklor_files";
            foreach (var hkFile in Directory.EnumerateFiles(GetTestPath(expectedHardklorFiles)))
            {
                AssertEx.FileEquals(hkFile, hkFile.Replace(expectedHardklorFiles, Path.Combine(expectedHardklorFiles,@"..")), null, true);
            }

            RunUI(() =>
            {
                // Click the "Finish" button.
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument());
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 12, 665, 666, 2006);

            PauseForScreenShot("complete");
        }

    }
}
