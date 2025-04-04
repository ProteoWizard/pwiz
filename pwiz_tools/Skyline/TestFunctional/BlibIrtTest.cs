/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class BlibIrtTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void IrtBlibFunctionalTest()
        {
            TestFilesZip = @"TestFunctional\IrtTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, TestFilesZip);
            var searchResults = testFilesDir.GetTestPath("msms.txt");
            var outBlib = testFilesDir.GetTestPath("iRT-test.blib");

            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

            RunUI(() => peptideSettings.SelectedTab = PeptideSettingsUI.TABS.Library);

            TestLibraryBuild(peptideSettings, outBlib, searchResults, DialogResult.Cancel);
            TestLibraryBuild(peptideSettings, outBlib, searchResults, DialogResult.No);
            // CONSIDER: Ideally, we would also test "Retry" by putting files in place to have it succeed
            TestLibraryBuild(peptideSettings, outBlib, searchResults, DialogResult.Yes);

            // iRT calculator
            var editIrtCalc = ShowDialog<EditIrtCalcDlg>(peptideSettings.AddCalculator);
            RunUI(() => editIrtCalc.IrtStandards = IrtStandard.BIOGNOSYS_11);

            var addLibrary = ShowDialog<AddIrtSpectralLibrary>(editIrtCalc.AddLibrary);
            RunUI(() =>
            {
                addLibrary.Source = SpectralLibrarySource.file;
                addLibrary.FilePath = outBlib;
            });
            var addIrts2 = ShowDialog<AddIrtPeptidesDlg>(addLibrary.OkDialog);
            VerifyAddIrts(addIrts2);
            OkDialog(editIrtCalc, editIrtCalc.CancelDialog);
            OkDialog(peptideSettings, peptideSettings.CancelDialog);
            
            // Import peptide search
            RunUI(() => SkylineWindow.SaveDocument(testFilesDir.GetTestPath("test.sky")));
            TestImportPeptideSearch(searchResults, DialogResult.Cancel);
            TestImportPeptideSearch(searchResults, DialogResult.No);
            // CONSIDER: Ideally, we would also test "Retry" by putting files in place to have it succeed
            TestImportPeptideSearch(searchResults, DialogResult.Yes);
        }

        private static void TestLibraryBuild(PeptideSettingsUI peptideSettings, string outBlib, string searchResults, DialogResult dialogResult)
        {
            // Library build
            var buildLibrary = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);

            RunUI(() =>
            {
                buildLibrary.LibraryName = "iRT standard peptides test";
                buildLibrary.LibraryPath = outBlib;
                buildLibrary.IrtStandard = IrtStandard.BIOGNOSYS_11;
                buildLibrary.OkWizardPage();

                buildLibrary.InputFileNames = new[] {searchResults};
            });
            WaitForConditionUI(() => buildLibrary.Grid.ScoreTypesLoaded);
            RunUI(() => buildLibrary.Grid.SetScoreThreshold(0.05));

            OkDialog(buildLibrary, buildLibrary.OkWizardPage);
            ChooseEmbedding(WaitForOpenForm<MultiButtonMsgDlg>(), dialogResult);
        }

        private void TestImportPeptideSearch(string searchResults, DialogResult dialogResult)
        {
            var import = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            RunUI(() => { import.BuildPepSearchLibControl.AddSearchFiles(new[] { searchResults }); });
            WaitForConditionUI(() => import.BuildPepSearchLibControl.Grid.ScoreTypesLoaded);
            RunUI(() =>
            {
                import.BuildPepSearchLibControl.Grid.SetScoreThreshold(0.05);
                import.BuildPepSearchLibControl.IrtStandards = IrtStandard.BIOGNOSYS_11;
            });
            WaitForConditionUI(() => import.IsNextButtonEnabled);
            ChooseEmbedding(ShowDialog<MultiButtonMsgDlg>(() => import.ClickNextButton()), dialogResult);
            if (dialogResult == DialogResult.Yes)
                OkDialog(import, import.CancelDialog);
            else
                WaitForClosedForm(import);
        }

        private static void ChooseEmbedding(MultiButtonMsgDlg preferEmbeddedDlg, DialogResult dialogResult)
        {
            RunUI(() =>
            {
                AssertEx.AreComparableStrings(Resources.VendorIssueHelper_ShowLibraryMissingExternalSpectrumFilesError,
                    preferEmbeddedDlg.Message);
            });

            if (dialogResult == DialogResult.Cancel)
                OkDialog(preferEmbeddedDlg, preferEmbeddedDlg.BtnCancelClick);
            else
            {
                if (dialogResult == DialogResult.No)
                {
                    // Click Retry once, and then Cancel
                    OkDialog(preferEmbeddedDlg, preferEmbeddedDlg.Btn1Click);   // Retry
                    var preferEmbeddedRetry = WaitForOpenForm<MultiButtonMsgDlg>();
                    Assert.AreNotSame(preferEmbeddedDlg, preferEmbeddedRetry);
                    OkDialog(preferEmbeddedRetry, preferEmbeddedRetry.BtnCancelClick);
                }
                else
                {
                    OkDialog(preferEmbeddedDlg, preferEmbeddedDlg.BtnYesClick);
                    VerifyAddIrts(WaitForOpenForm<AddIrtPeptidesDlg>());
                }
            }
        }

        private static void VerifyAddIrts(AddIrtPeptidesDlg dlg)
        {
            RunUI(() =>
            {
                Assert.AreEqual(0, dlg.PeptidesCount);
                Assert.AreEqual(1, dlg.RunsConvertedCount);  // Libraries now convert through internal alignment to single RT scale
                Assert.AreEqual(0, dlg.RunsFailedCount);
            });

            VerifyRegression(dlg, 0, true, 11, 1, 0);

            OkDialog(dlg, dlg.OkDialog);
        }

        private static void VerifyRegression(AddIrtPeptidesDlg dlg, int index, bool converted, int numPoints, int numMissing, int numOutliers)
        {
            RunUI(() => Assert.AreEqual(converted, dlg.IsConverted(index)));
            var regression = ShowDialog<GraphRegression>(() => dlg.ShowRegression(index));
            RunUI(() =>
            {
                Assert.AreEqual(1, regression.RegressionGraphDatas.Count);
                var data = regression.RegressionGraphDatas.First();
                Assert.IsTrue(data.XValues.Length == data.YValues.Length);
                Assert.AreEqual(numPoints, data.XValues.Length);
                Assert.AreEqual(numMissing, data.MissingIndices.Count);
                Assert.AreEqual(numOutliers, data.OutlierIndices.Count);
            });
            OkDialog(regression, regression.CloseDialog);
        }
    }
}