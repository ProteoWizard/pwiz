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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
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

        [TestMethod, NoUnicodeTesting(TestExclusionReason.HARDKLOR_UNICODE_ISSUES), NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDdaFeatureDetection()
        {
            TestFilesZip = GetPerfTestDataURL("PerfImportResultsThermoDDAVsMz5.zip");

            TestFilesPersistent = new[]
            {
                "Thermo\\QE_DDA\\20130311_DDA_Pit01.raw"
            };
            TestDirectoryName = "DdaFeatureDetectionTest";

            RunFunctionalTest();
        }

        public bool IsRecordMode => false;

        private string GetTestPath(string path)
        {
            var fullpath = TestFilesDir.GetTestPath(path);
            if (!File.Exists(fullpath))
            {
                // Maybe it's one of the unzipped persistent files
                var unzip = TestFilesDir.GetTestPath(Path.Combine("DdaSearchMs1Filtering",path));
                if (File.Exists(unzip))
                    return unzip;
            }
            return fullpath;
        }

        private string[] SearchFiles
        {
            get
            {
                return new[]
                {
                    GetTestPath(TestFilesPersistent[0]),
                    GetTestPath("Thermo\\QE_DDA\\not_actually_there.raw") // Just for UI handling test
                };
            }
        }

        private string[] SearchFilesSameName
        {
            get
            {
                return new[]
                {
                    GetTestPath(TestFilesPersistent[0]),
                    GetTestPath(Path.Combine("subdir", TestFilesPersistent[0]))
                };
            }
        }

        protected override void DoTest()
        {
//IsPauseForScreenShots = true;
            PrepareDocument("TestFeatureDetection.sky");
            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowFeatureDetectionDlg);
            // We're on the "Select Files to Search" page of the wizard.

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                Assert.IsTrue(importPeptideSearchDlg.BuildPepSearchLibControl.PerformDDASearch);
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).Take(1).ToArray();
                AssertEx.AreEqual(ImportPeptideSearchDlg.Workflow.feature_detection, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType); 
                importPeptideSearchDlg.BuildPepSearchLibControl.CutOffScore = 0.95;
            });
            PauseForScreenShot();
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // With only 1 source, no add/remove prefix/suffix dialog

            // Test back/next buttons
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                // We're on the MS1 full scan settings page.
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3 };
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.orbitrap;
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 70000; // That's the value at 200m/z per this data set, probably close enough for the 400m/z usd by Hardklor
            });
            PauseForScreenShot();
            RunUI(() =>
            {
                // Run the search
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            bool? searchSucceeded = null;
            TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.DDASearchPage));   // Stop to show this form during form testing

            // Wait for the mzML conversion to complete before canceling
            var converted = Path.Combine(Path.GetDirectoryName(SearchFiles[0]) ?? string.Empty,
                @"converted",
                Path.ChangeExtension(Path.GetFileName(SearchFiles[0]), @"mzML"));
            WaitForCondition(() => File.Exists(converted));

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
            // Go back and test 2 input files with the same name in different directories
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());

                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFilesSameName.Select(o => (MsDataFileUri) new MsDataFilePath(o)).ToArray();
            });

            var removeSuffix = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            OkDialog(removeSuffix, removeSuffix.CancelDialog);

            // Test with 2 files (different name - one of these is bogus, just checking the name handling)
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).ToArray();
            });

            // With 2 sources, we get the remove prefix/suffix dialog; accept default behavior
            var removeSuffix2 = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            OkDialog(removeSuffix, () => removeSuffix2.YesDialog());

            // Go back and remove the bogus filename - we'll just process the one actual data set
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources =
                    importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = new []{(MsDataFileUri)new MsDataFilePath(SearchFiles[0])};
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            RunUI(() =>
            {
                // We're on the "Full Scan Settings" page again.
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.orbitrap;
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 70000; // That's the value at 200m/z per this data set, probably close enough for the 400m/z usd by Hardklor
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
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
            RunUI(() =>
            {
                // Click the "Finish" button.
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument());
//PauseTest();
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings", 
                doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(documentFile)));
        }
    }
}
