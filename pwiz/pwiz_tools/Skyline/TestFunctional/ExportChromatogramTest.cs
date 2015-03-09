/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for ExportChromatogramTest
    /// </summary>
    [TestClass]
    public class ExportChromatogramTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestExportChromatogram()
        {
            TestFilesZip = @"TestFunctional\ExportChromatogramTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // 1. Document with no imported results throws exception
            var documentNoFiles = TestFilesDir.GetTestPath("ChromNoFiles.sky");
            RunUI(() => SkylineWindow.OpenFile(documentNoFiles));
            WaitForDocumentLoaded();
            RunDlg<MessageDlg>(SkylineWindow.ShowChromatogramFeaturesDialog,
                missingFileMessage =>
                {
                    Assert.AreEqual(Resources.SkylineWindow_ShowChromatogramFeaturesDialog_The_document_must_have_imported_results_,
                        missingFileMessage.Message);
                    missingFileMessage.OkDialog();
                });

            // 2. Document with no peptides throws exception
            var save = TestSmallMolecules;  // This test is about missing nodes, don't add any
            TestSmallMolecules = false;
            var documentNoPeptides = TestFilesDir.GetTestPath("ChromNoPeptides.sky");
            RunUI(() => SkylineWindow.OpenFile(documentNoPeptides));
            WaitForDocumentLoaded();
            var missingPeptidesMessage = ShowDialog<MessageDlg>(SkylineWindow.ShowChromatogramFeaturesDialog);
            Assert.AreEqual(Resources.SkylineWindow_ShowChromatogramFeaturesDialog_The_document_must_have_targets_for_which_to_export_chromatograms_,
                            missingPeptidesMessage.Message);
            RunUI(missingPeptidesMessage.OkDialog);
            TestSmallMolecules = save;

            // 3. Exporting with no files selected throws exception
            var documentExportNoPeptides = TestFilesDir.GetTestPath("ChromToExport.sky");
            RunUI(() => SkylineWindow.OpenFile(documentExportNoPeptides));
            WaitForDocumentLoaded();
            var exportChromDlg = ShowDialog<ExportChromatogramDlg>(SkylineWindow.ShowChromatogramFeaturesDialog);
            RunUI(() => exportChromDlg.UpdateCheckedAll(true));
            RunDlg<MessageDlg>(exportChromDlg.OkDialog, messageDlg =>
            {
                Assert.AreEqual(Resources.ExportChromatogramDlg_OkDialog_At_least_one_chromatogram_type_must_be_selected,
                                messageDlg.Message);
                messageDlg.OkDialog();
            });
            // 4. Exporting with no chromatogram types selected throws exception
            RunUI(() =>
            {
                exportChromDlg.UpdateCheckedAll(false);
                exportChromDlg.UpdateChromSources(true, false);
                exportChromDlg.UpdateChromExtractors(false, true);
            });
            RunDlg<MessageDlg>(exportChromDlg.OkDialog, messageDlg =>
            {
                Assert.AreEqual(Resources.ExportChromatogramDlg_OkDialog_At_least_one_file_must_be_selected,
                                              messageDlg.Message);
                messageDlg.OkDialog();
            });
            // Check that exporting the file through the UI gives the correct result
            string exportExpected = TestFilesDir.GetTestPathLocale("ChromToExportAll.tsv");
            var exportActual = TestFilesDir.GetTestPath("ActualFile.tsv");
            RunUI(() =>
            {
                exportChromDlg.UpdateCheckedAll(true);
                exportChromDlg.UpdateChromSources(true, false);
                exportChromDlg.UpdateChromExtractors(true, true);
                exportChromDlg.WriteChromatograms(exportActual);
            });
            AssertEx.FileEquals(exportExpected, exportActual);
            RunUI(exportChromDlg.CancelDialog);
            WaitForClosedForm(exportChromDlg);
        }
    }
}
