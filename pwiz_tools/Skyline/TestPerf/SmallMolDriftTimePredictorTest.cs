/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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


using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf 
{
    /// <summary>
    /// Verify measured drift time operation for a small molecule document
    /// </summary>
    [TestClass]
    public class DriftTimePredictorSmallMoleculesTest : AbstractFunctionalTestEx
    {
        private const string SULFA_MIX = "Sulfa Mix 1.0ms.d";

        [TestMethod]
        public void TestDriftTimePredictorSmallMolecules()
        {
            // RunPerfTests = true; // Enables perftests to run from the IDE (you don't want to commit this line without commenting it out)

            TestFilesZipPaths = new[]
            {
                @"https://skyline.ms/perftests/DriftTimePredictorSmallMoleculesTest.zip",
            };

            TestFilesPersistent = new[] { SULFA_MIX };

            RunFunctionalTest();
        }

        private string DataPath { get { return TestFilesDirs.Last().PersistentFilesDir; } }

        protected override void DoTest()
        {
            // Empty doc with suitable full scan settings
            RunUI(() => SkylineWindow.OpenFile(
                TestFilesDirs[0].GetTestPath(@"DriftTimePredictorSmallMoleculesTest.sky")));

            var transitionList = TestFilesDirs[0].GetTestPath(@"Skyline Transition List wo CCS.csv");
            // Transition list is suitably formatted with headers to just drop into the targets tree
            SetCsvFileClipboardText(transitionList);
            RunUI(() => SkylineWindow.Paste());
            var document = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(document, null, 1, 4, 4, 4);
            {
                var importResults = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                RunUI(() => importResults.ImportSimultaneousIndex = 2);

                // Importing raw data

                var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResults.OkDialog);

                RunUI(() =>
                {
                    openDataSourceDialog.CurrentDirectory = new MsDataFilePath(DataPath);
                    openDataSourceDialog.SelectAllFileType(".d");
                });

                OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
            }
            document = WaitForDocumentLoaded();

            var area = document.MoleculePrecursorPairs.First().NodeGroup.Results.First().First().AreaMs1;

            // Locate drift peaks
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction);
            var driftPredictor = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsUI.AddDriftTimePredictor);
            const string predictorName = "Sulfa";
            RunUI(() =>
            {
                driftPredictor.SetPredictorName(predictorName);
                driftPredictor.SetResolvingPower(50);
                driftPredictor.GetDriftTimesFromResults();
            });

            // Check that a new value was calculated for all precursors
            RunUI(() => Assert.AreEqual(SkylineWindow.Document.MoleculeTransitionGroupCount, driftPredictor.Predictor.IonMobilityRows.Count));

            OkDialog(driftPredictor, () => driftPredictor.OkDialog());

            RunUI(() =>
            {
                Assert.IsTrue(peptideSettingsUI.IsUseMeasuredRT);
                Assert.AreEqual(2, peptideSettingsUI.TimeWindow);
                Assert.AreEqual(predictorName, peptideSettingsUI.SelectedDriftTimePredictor);
            });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);

            WaitForDocumentChangeLoaded(document);

            var docFiltered = SkylineWindow.Document;

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageDlg =>
            {
                manageDlg.SelectedChromatograms = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Take(1);
                manageDlg.ReimportResults();
                manageDlg.OkDialog();
            });

            docFiltered = WaitForDocumentChangeLoaded(docFiltered); 

            // If drift filtering was engaged, peak area should be less
            var areaFiltered = docFiltered.MoleculePrecursorPairs.First().NodeGroup.Results.First().First().AreaMs1;
            Assume.IsTrue(area > areaFiltered);

        }

    }
}
