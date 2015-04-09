/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class SmallMoleculesTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSmallMoleculesTutorial()
        {
            // Set true to look at tutorial screenshots.
            // IsPauseForScreenShots = true;

            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/SmallMolecule.pdf";

            TestFilesZipPaths = new [] { @"https://skyline.gs.washington.edu/tutorials/SmallMolecule.zip", @"TestTutorial\SmallMoleculeViews.zip" };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Inserting a Transition List, p. xxxx
            {
                var doc = SkylineWindow.Document;

                var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
                RunUI(() => { pasteDlg.IsMolecule = false; });  // Default peptide view
                PauseForScreenShot<PasteDlg>("Paste Dialog in peptide mode");

                RunUI(() =>
                {
                    pasteDlg.IsMolecule = true;
                    pasteDlg.SetSmallMoleculeColumns(null);  // Default columns
                });
                PauseForScreenShot<PasteDlg>("Paste Dialog in small molecule mode, default columns");

                var columnOrder = new[]
                    {
                        // Molecule List Name,Precursor Name,Precursor Formula,Precursor Charge,Precursor RT,Precursor CE,Product m/z,Product Charge
                        PasteDlg.SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                        PasteDlg.SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                        PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                        PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                        PasteDlg.SmallMoleculeTransitionListColumnHeaders.rtPrecursor,
                        PasteDlg.SmallMoleculeTransitionListColumnHeaders.cePrecursor,
                        PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzProduct,
                        PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargeProduct
                    };
                RunUI(() => { pasteDlg.SetSmallMoleculeColumns(columnOrder.ToList()); });
                WaitForConditionUI(() => pasteDlg.GetUsableColumnCount() == columnOrder.ToList().Count);
                PauseForScreenShot<PasteDlg>("Paste Dialog with selected and ordered columns");

                SetCsvFileClipboardText(TestFilesDirs[0].GetTestPath("SMTutorial_TransitionList.csv"), true);
                RunUI(pasteDlg.PasteTransitions);
                RunUI(pasteDlg.ValidateCells);
                PauseForScreenShot<PasteDlg>("Paste Dialog with validated contents");

                OkDialog(pasteDlg, pasteDlg.OkDialog);
                WaitForDocumentChange(doc);

                RunUI(() => SkylineWindow.SaveDocument(TestFilesDirs[0].GetTestPath("mydoc.sky")));

                var importResultsDlg1 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                PauseForScreenShot<ImportResultsDlg>("Import Results form");

                var openDataSourceDialog1 = ShowDialog<OpenDataSourceDialog>(() => importResultsDlg1.NamedPathSets =
                                                                            importResultsDlg1.GetDataSourcePathsFile(null));
                PauseForScreenShot<ImportResultsSamplesDlg>("Choose Samples form");
                RunUI(() =>
                {
                    openDataSourceDialog1.CurrentDirectory = new MsDataFilePath(TestFilesDirs[0].FullPath);
                    openDataSourceDialog1.SelectAllFileType(ExtensionTestContext.ExtWatersRaw);
                });
                OkDialog(openDataSourceDialog1,openDataSourceDialog1.Open);

                var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg1.OkDialog);
                PauseForScreenShot<ImportDocResultsDlg>("Import Results Common prefix form");

                OkDialog(importResultsNameDlg, importResultsNameDlg.NoDialog);

                WaitForCondition(() =>
                    SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);

                PauseForScreenShot();
                RestoreViewOnScreen(TestFilesDirs[1].GetTestPath("imported.view"));
                PauseForScreenShot();
            }

        }

    }
}
