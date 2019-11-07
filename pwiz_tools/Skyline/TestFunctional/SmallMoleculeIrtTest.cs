/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SmallMoleculeIrtTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSmallMoleculeIrt()
        {
            TestFilesZip = @"TestFunctional\SmallMoleculeIrtTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("DextranLadder.sky")));
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(()=>SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));
            var irtCalc = ShowDialog<EditIrtCalcDlg>(peptideSettingsUi.AddCalculator);
            var calibrateDlg = ShowDialog<CalibrateIrtDlg>(irtCalc.Calibrate);

            // First, populate the iRT database with any set of standards from the results
            var addStandardsDlg = ShowDialog<AddIrtStandardsDlg>(calibrateDlg.UseResults);
            RunUI(() => addStandardsDlg.StandardCount = 10);
            OkDialog(addStandardsDlg, addStandardsDlg.OkDialog);
            RunUI(() => calibrateDlg.StandardName = "Document");
            OkDialog(calibrateDlg, calibrateDlg.OkDialog);
            var addIrtPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(irtCalc.AddResults);
            OkDialog(addIrtPeptidesDlg, addIrtPeptidesDlg.OkDialog);
            var alertDlg = WaitForOpenForm<AlertDlg>();
            OkDialog(alertDlg, alertDlg.ClickYes);

            // Now, change the set of standards to the ones that we want to use.
            var changeIrtPeptidesDlg = ShowDialog<ChangeIrtPeptidesDlg>(irtCalc.ChangeStandardPeptides);
            RunUI(() =>
                {
                    changeIrtPeptidesDlg.PeptidesText =
                        "(Glc)3\r\n(Glc)4\r\n(Glc)5\r\n(Glc)6\r\n(Glc)7\r\n(Glc)8\r\n(Glc)9\r\n(Glc)10\r\n";
                });
            OkDialog(changeIrtPeptidesDlg, changeIrtPeptidesDlg.OkDialog);
            RunUI(()=>
            {
                irtCalc.CalcName = "DextranLadderIrt";
                irtCalc.CreateDatabase(TestFilesDir.GetTestPath("DextranLadder.irtdb"));
            });
            var confirmStandardCountDlg = ShowDialog<MultiButtonMsgDlg>(irtCalc.OkDialog);
            OkDialog(confirmStandardCountDlg, confirmStandardCountDlg.ClickYes);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            RunUI(()=>SkylineWindow.SaveDocument());
        }
    }
}
