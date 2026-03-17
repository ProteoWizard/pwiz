/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net >
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.SkylineTestUtil;

//
// Test for Hardklor/BullseyeSharp feature detection on SIM (boxcar) scan data.
// Verifies that features are not fragmented across non-overlapping SIM windows.
//
namespace TestPerf
{
    [TestClass]
    public class PerfFeatureDetectionSIMscansTest : AbstractFunctionalTestEx
    {
        private const string RAW_FILE = "FU2_2026_0223_RJ_10_box20.raw";
        private const string SKY_ZIP  = "FU2_2026_0223_RJ_boxcarDDA_features.sky.zip";

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestFeatureDetectionSIMscans()
        {
            TestFilesZip = GetPerfTestDataURL(@"PerfFeatureDetectionSIMscans.zip");
            TestFilesPersistent = new[] { RAW_FILE }; // keep the large raw file between runs
            TestDirectoryName = "FeatureDetectionSIMscansTest";
            RunFunctionalTest();
        }

        private string GetTestPath(string path)
        {
            return TestFilesDir.GetTestPath(path);
        }

        protected override void DoTest()
        {
            // Converted mzML file is created in the persistent directory alongside the raw file
            // and intentionally left for reuse on subsequent runs. Declare it so the end-of-test
            // file change detection doesn't flag it as unexpected.
            TestFilesDir.PotentialAdditionalPersistentFileSet = new HashSet<string>(
                new[] { Path.Combine(MsconvertDdaConverter.OUTPUT_SUBDIRECTORY, Path.ChangeExtension(RAW_FILE, ".mzML")) });

            OpenDocument(GetTestPath(SKY_ZIP));

            // Launch the Feature Detection wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowFeatureDetectionDlg);
            importPeptideSearchDlg.IsAutomatedTest = true;

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                Assert.IsTrue(importPeptideSearchDlg.BuildPepSearchLibControl.PerformDDASearch);
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources =
                    new MsDataFileUri[] { new MsDataFilePath(GetTestPath(RAW_FILE)) };
            });

            // Accept single file name
            RunUI(() => importPeptideSearchDlg.ClickNextButton());

            RunUI(() =>
            {
                // Full scan settings page - document already has Orbitrap 45000 settings, accept as-is
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                              ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                // Search settings page - accept defaults
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            bool? searchSucceeded = null;
            RunUI(() =>
            {
                importPeptideSearchDlg.SearchControl.SearchFinished += success => searchSucceeded = success;
            });

            try
            {
                WaitForConditionUI(30 * 60000, () => searchSucceeded.HasValue);
                RunUI(() => Assert.IsTrue(searchSucceeded.Value,
                    importPeptideSearchDlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText("SearchControlLog.txt",
                    importPeptideSearchDlg.SearchControl.LogText);
            }

            // Click Finish
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument());

            // Verify SIM scan feature detection results
            var doc = SkylineWindow.Document;
            var actualFeatures = doc.CustomMolecules.Count();
            var actualTransitionGroups = doc.MoleculeTransitionGroupCount - doc.PeptideTransitionGroupCount;
            var actualTransitions = doc.MoleculeTransitionCount - doc.PeptideTransitionCount;

            // TODO: fill in expected counts after first successful test run
            const int EXPECTED_FEATURES = 1451;
            const int EXPECTED_TRANSITION_GROUPS = 1959;
            const int EXPECTED_TRANSITIONS = 5877;

            AssertEx.AreEqual(EXPECTED_FEATURES, actualFeatures);
            AssertEx.AreEqual(EXPECTED_TRANSITION_GROUPS, actualTransitionGroups);
            AssertEx.AreEqual(EXPECTED_TRANSITIONS, actualTransitions);

            // Verify that every detected feature transition group has an extracted chromatogram.
            // This is the key regression check for SIM scan support: without the fix, features
            // spanning non-overlapping SIM windows would be fragmented or dropped entirely.
            var featureTGs = doc.MoleculeTransitionGroups.Where(tg => tg.IsCustomIon).ToArray();
            var featuresWithoutChrom = featureTGs
                .Count(tg => !tg.HasResults || !tg.ChromInfos.Any(ci => ci.Area.HasValue));
            AssertEx.AreEqual(0, featuresWithoutChrom,
                $"{featuresWithoutChrom} of {featureTGs.Length} detected features have no extracted chromatogram");
        }
    }
}
