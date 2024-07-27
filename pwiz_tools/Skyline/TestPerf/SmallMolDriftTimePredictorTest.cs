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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
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
        public void TestDriftTimePredictorSmallMolecules()  // N.B. the term "Drift Time Predictor" is a historical curiosity, leaving it alone for test history continuity
        {
            // RunPerfTests = true; // Enables perftests to run from the IDE (you don't want to commit this line without commenting it out)

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/SmallMoleculeIMSLibraries-20_2.pdf";

            TestFilesZipPaths = new[]
            {
                GetPerfTestDataURL(@"DriftTimePredictorSmallMoleculesTest.zip"),
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

            var docCurrent = SkylineWindow.Document;
            var transitionList = TestFilesDirs[0].GetTestPath(@"Skyline Transition List wo CCS.csv");
            // Transition list is suitably formatted with headers to just drop into the targets tree
            var document = PasteSmallMoleculeListNoAutoManage(transitionList); // Paste the clipboard text, dismiss the offer to enable automanage
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
            AssertEx.IsTrue(area > 0);

            // Locate drift peaks
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() => transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.IonMobility);
            RunUI(() => transitionSettingsUI.IonMobilityControl.WindowWidthType = IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power);
            RunUI(() => transitionSettingsUI.IonMobilityControl.IonMobilityFilterResolvingPower = 50);
            var editIonMobilityLibraryDlg = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsUI.IonMobilityControl.AddIonMobilityLibrary);
            const string libName = "Sulfa";
            var databasePath = TestFilesDir.GetTestPath(libName + IonMobilityDb.EXT);
            RunUI(() =>
            {
                editIonMobilityLibraryDlg.LibraryName = libName;
                editIonMobilityLibraryDlg.CreateDatabaseFile(databasePath); // Simulate user click on Create button
                editIonMobilityLibraryDlg.GetIonMobilitiesFromResults();
                // Make sure we haven't broken this dialog's use of TargetResolver (if we have, we'll probably get the serializable format)
                AssertEx.AreEqual(@"Sulfamethizole", editIonMobilityLibraryDlg.GetTargetDisplayName(0));
            });

            OkDialog(editIonMobilityLibraryDlg, () => editIonMobilityLibraryDlg.OkDialog());

            RunUI(() =>
            {
                Assert.AreEqual(libName, transitionSettingsUI.IonMobilityControl.SelectedIonMobilityLibrary);
            });
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);

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
            AssertEx.IsTrue(area > areaFiltered);
            AssertEx.IsTrue(areaFiltered > 0);

        }

    }
}
