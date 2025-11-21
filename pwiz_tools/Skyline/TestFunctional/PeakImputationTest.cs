/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakImputationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakImputation()
        {
            TestFilesZip = @"TestFunctional\PeakImputationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PeakImputationTest.sky"));
            });
            WaitForDocumentLoaded();
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                var editIrtCalc = ShowDialog<EditIrtCalcDlg>(peptideSettingsUi.AddCalculator);
                RunUI(() =>
                {
                    editIrtCalc.CalcName = "PeakApexes";
                    editIrtCalc.CalcPath = TestFilesDir.GetTestPath("PeakApexes.irtdb");
                    editIrtCalc.SelectedRegressionType = IrtRegressionType.LOWESS;
                });
                var addLibrary = ShowDialog<AddIrtSpectralLibrary>(editIrtCalc.AddLibrary);
                RunUI(()=>
                {
                    addLibrary.Source = SpectralLibrarySource.file;
                    addLibrary.FilePath = TestFilesDir.GetTestPath("Bereman_5proteins_spikein.blib");
                });
                RunDlg<AddIrtPeptidesDlg>(addLibrary.OkDialog, addIrtPeptidesDlg=>addIrtPeptidesDlg.OkDialog());
                OkDialog(editIrtCalc, editIrtCalc.OkDialog);
                peptideSettingsUi.MaxRtShift = 1;
            }, peptideSettingsUi=>peptideSettingsUi.OkDialog());
            Assert.AreEqual(1.0, SkylineWindow.Document.Settings.PeptideSettings.Imputation.MaxRtShift);
            RunDlg<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog, reintegrateDlg=>{
                reintegrateDlg.OverwriteManual = true;
                reintegrateDlg.OkDialog();
            });
        }
    }
}
