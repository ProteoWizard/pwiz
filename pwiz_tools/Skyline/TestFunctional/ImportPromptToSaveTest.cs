/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that the user is prompted to save the document when importing results
    /// if the .skyd file contains replicates that are not in the loaded document.
    /// <see cref="SkylineWindow.CheckForExistingResultsBeforeImporting" />
    /// </summary>
    [TestClass]
    public class ImportPromptToSaveTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestImportPromptToSave()
        {
            TestFilesZip = @"TestFunctional\ImportPromptToSaveTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            string skyFilePath = TestFilesDir.GetTestPath("ImportPromptToSaveTest.sky");
            string skydFilePath = TestFilesDir.GetTestPath("ImportPromptToSaveTest.skyd");
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath(skyFilePath)));
            RunLongDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                RunDlg<OpenDataSourceDialog>(importResultsDlg.OkDialog, openDataSourceDialog =>
                {
                    openDataSourceDialog.SelectFile(TestFilesDir.GetTestPath("S_1.mzML"));
                    openDataSourceDialog.Open();
                });
            }, dlg=>{});
            WaitForDocumentLoaded();

            // Copy the .skyd file: we will use it later in the test
            File.Copy(skydFilePath, TestFilesDir.GetTestPath("skyd1.skyd"));
            
            // Change some transition settings, remove results and import the same file again
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettingsUi.PrecursorRes = 30000;
                transitionSettingsUi.OkDialog();
            });
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg=>
            {
                manageResultsDlg.RemoveAllReplicates();
                manageResultsDlg.OkDialog();
            });

            // Try to import "S_1.mzML" but click cancel when prompted to save
            RunDlg<AlertDlg>(SkylineWindow.ImportResults, alertDlg =>
            {
                Assert.AreEqual(SkylineResources.SkylineWindow_SaveDocumentBeforeImportingResults, alertDlg.Message);
                alertDlg.ClickCancel();
            });
            Assert.IsTrue(File.Exists(skydFilePath));

            // Import "S_1.mzML" and click Yes when prompted to save
            RunLongDlg<AlertDlg>(SkylineWindow.ImportResults, alertDlg =>
            {
                OkDialog(alertDlg, alertDlg.ClickYes);
                var importResultsDlg = WaitForOpenForm<ImportResultsDlg>();
                Assert.IsFalse(File.Exists(skydFilePath));
                RunDlg<OpenDataSourceDialog>(importResultsDlg.OkDialog, openDataSourceDialog =>
                {
                    openDataSourceDialog.SelectFile(TestFilesDir.GetTestPath("S_1.mzML"));
                    openDataSourceDialog.Open();
                });
            }, dlg=>{});
            WaitForDocumentLoaded();

            // Import "S_2.mzML"
            RunLongDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                RunDlg<OpenDataSourceDialog>(importResultsDlg.OkDialog, openDataSourceDialog =>
                {
                    openDataSourceDialog.SelectFile(TestFilesDir.GetTestPath("S_2.mzML"));
                    openDataSourceDialog.Open();
                });
            }, dlg => { });
            WaitForDocumentLoaded();

            double extractionWidth30K = GetExtractionWidth("S_2");
            // Create a new document and choose not to save
            RunDlg<AlertDlg>(SkylineWindow.NewDocument, alertDlg=>
            {
                alertDlg.ClickNo();
            });

            // Lock the .skyd file so that it cannot be deleted.
            // Because the document has no results, Skyline will try to delete the .skyd file
            // Verify that a message is shown saying the skyd file could not be deleted
            using (File.OpenRead(skydFilePath))
            {
                RunDlg<AlertDlg>(()=>SkylineWindow.OpenFile(skyFilePath), alertDlg=>
                {
                    alertDlg.ClickOk();
                });
            }

            WaitForDocumentLoaded();

            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettingsUi.PrecursorRes = 10000;
                transitionSettingsUi.OkDialog();
            });

            // Import "S_2.mzML" and click Yes when prompted to save
            RunLongDlg<AlertDlg>(SkylineWindow.ImportResults, alertDlg =>
            {
                OkDialog(alertDlg, alertDlg.ClickYes);
                var importResultsDlg = WaitForOpenForm<ImportResultsDlg>();
                Assert.IsFalse(File.Exists(skydFilePath));
                RunDlg<OpenDataSourceDialog>(importResultsDlg.OkDialog, openDataSourceDialog =>
                {
                    openDataSourceDialog.SelectFile(TestFilesDir.GetTestPath("S_2.mzML"));
                    openDataSourceDialog.Open();
                });
            }, dlg => { });
            WaitForDocumentLoaded();
            var extractionWidth10K = GetExtractionWidth("S_2");
            Assert.AreNotEqual(extractionWidth30K, extractionWidth10K);
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettingsUi.PrecursorRes = 30000;
                transitionSettingsUi.OkDialog();
            });

            Assert.AreEqual(extractionWidth10K, GetExtractionWidth("S_2"));
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.RemoveAllReplicates();
                manageResultsDlg.OkDialog();
            });

            // Import "S_2.mzML" and click No when prompted to save
            RunLongDlg<AlertDlg>(SkylineWindow.ImportResults, alertDlg =>
            {
                OkDialog(alertDlg, alertDlg.ClickNo);
                var importResultsDlg = WaitForOpenForm<ImportResultsDlg>();
                Assert.IsTrue(File.Exists(skydFilePath));
                RunDlg<OpenDataSourceDialog>(importResultsDlg.OkDialog, openDataSourceDialog =>
                {
                    openDataSourceDialog.SelectFile(TestFilesDir.GetTestPath("S_2.mzML"));
                    openDataSourceDialog.Open();
                });
            }, dlg => { });
            WaitForDocumentLoaded();
            // Extraction width is still the old value because the old chromatograms were used
            Assert.AreEqual(extractionWidth10K, GetExtractionWidth("S_2"));

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.RemoveAllReplicates();
                manageResultsDlg.OkDialog();
            });

            // Import "S_2.mzML" and click Yes when prompted to save
            RunLongDlg<AlertDlg>(SkylineWindow.ImportResults, alertDlg =>
            {
                OkDialog(alertDlg, alertDlg.ClickYes);
                var importResultsDlg = WaitForOpenForm<ImportResultsDlg>();
                Assert.IsFalse(File.Exists(skydFilePath));
                RunDlg<OpenDataSourceDialog>(importResultsDlg.OkDialog, openDataSourceDialog =>
                {
                    openDataSourceDialog.SelectFile(TestFilesDir.GetTestPath("S_2.mzML"));
                    openDataSourceDialog.Open();
                });
            }, dlg => { });
            WaitForDocumentLoaded();
            
            // Extraction width has the new value now
            Assert.AreEqual(extractionWidth30K, GetExtractionWidth("S_2"));

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.RemoveAllReplicates();
                manageResultsDlg.OkDialog();
            });

            Assert.IsTrue(File.Exists(skydFilePath));
            // Lock the .skyd file so that it cannot be deleted, then save the document
            // No error gets shown in this case
            using (File.OpenRead(skydFilePath))
            {
                RunUI(()=>SkylineWindow.SaveDocument());
            }
            Assert.IsTrue(File.Exists(skydFilePath));
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettingsUi.PrecursorRes = 10000;
                transitionSettingsUi.OkDialog();
            });

            // Import "S_2.mzML" and click Yes when prompted to save
            RunLongDlg<AlertDlg>(SkylineWindow.ImportResults, alertDlg =>
            {
                OkDialog(alertDlg, alertDlg.ClickYes);
                var importResultsDlg = WaitForOpenForm<ImportResultsDlg>();
                Assert.IsFalse(File.Exists(skydFilePath));
                RunDlg<OpenDataSourceDialog>(importResultsDlg.OkDialog, openDataSourceDialog =>
                {
                    openDataSourceDialog.SelectFile(TestFilesDir.GetTestPath("S_2.mzML"));
                    openDataSourceDialog.Open();
                });
            }, dlg => { });
            WaitForDocumentLoaded();
            Assert.AreEqual(extractionWidth10K, GetExtractionWidth("S_2"));
        }

        private double GetExtractionWidth(string replicate)
        {
            var document = SkylineWindow.Document;
            float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            Assert.IsTrue(document.MeasuredResults.TryGetChromatogramSet(replicate, out var chromatogramSet, out _));
            var peptideDocNode = document.Molecules.First();
            Assert.IsTrue(document.MeasuredResults.TryLoadChromatogram(chromatogramSet, peptideDocNode, peptideDocNode.TransitionGroups.First(), tolerance, out var chromatogramGroupInfos));
            return chromatogramGroupInfos.First().TransitionPointSets.First().ExtractionWidth.Value;
        }
    }
}
