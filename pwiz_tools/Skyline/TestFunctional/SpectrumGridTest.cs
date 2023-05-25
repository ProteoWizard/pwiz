/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Spectra;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SpectrumGridTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSpectrumGrid()
        {
            TestFilesZip = @"TestFunctional\SpectrumGridTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SpectrumGridTest.sky")));
            ImportResultsFiles(new[]{new MsDataFilePath(TestFilesDir.GetTestPath("20fmol.mzML")), new MsDataFilePath(TestFilesDir.GetTestPath("80fmol.mzML"))});
            var spectrumGrid = ShowDialog<SpectrumGridForm>(() => SkylineWindow.ViewMenu.ShowSpectrumGridForm());
            RunUI(()=>SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int) SrmDocument.Level.MoleculeGroups, 0));
            WaitForCondition(() => spectrumGrid.IsComplete());
            RunUI(()=>
            {
                Assert.AreEqual(3, spectrumGrid.DataGridView.RowCount);
                SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.Molecules, 1);
            });
            WaitForConditionUI(() => spectrumGrid.IsComplete());
            RunUI(()=>
            {
                AssertEx.AreEqual(2, spectrumGrid.DataGridView.RowCount);
                spectrumGrid.SetSpectrumClassColumnCheckState(SpectrumClassColumn.PresetScanConfiguration, CheckState.Unchecked);
                spectrumGrid.SetSpectrumClassColumnCheckState(SpectrumClassColumn.Ms2Precursors, CheckState.Unchecked);
                spectrumGrid.DataGridView.SelectAll();
            });
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeTransitionGroupCount);
            RunDlg<AlertDlg>(spectrumGrid.AddSpectrumFiltersForSelectedRows, alertDlg =>
            {
                string expectedMessage =
                    string.Format(
                        Skyline.Properties.Resources
                            .SpectraGridForm_AddSpectrumFilters__0__spectrum_filters_will_be_added_to_the_document_, 2);

                Assert.AreEqual(expectedMessage, alertDlg.Message);
                alertDlg.OkDialog();
            });
            Assert.AreEqual(4, SkylineWindow.Document.MoleculeTransitionGroupCount);
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.SelectedChromatograms = SkylineWindow.Document.MeasuredResults.Chromatograms;
                manageResultsDlg.ReimportResults();
                manageResultsDlg.OkDialog();
            });
            WaitForDocumentLoaded();

            OkDialog(spectrumGrid, spectrumGrid.Close);

            // Make sure that the document can be reopened
            RunUI(()=>SkylineWindow.OpenFile(SkylineWindow.DocumentFilePath));
            WaitForDocumentLoaded();
        }
    }
}
