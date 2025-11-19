/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.SkylineTestUtil;
using IrtResources = pwiz.Skyline.SettingsUI.Irt.IrtResources;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class IrtCalibrationTest : AbstractFunctionalTestEx
    {
        private const string CALCULATOR_NAME = "CoralEggs-Cirt";
        private const int STANDARD_PEP_COUNT = 20;

        [TestMethod]
        public void TestIrtCalibration()
        {
            TestFilesZip = @"TestFunctional\IrtCalibrationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            CalibrateIrtCalculator();
            UseIrtCalculator();
        }

        private void CalibrateIrtCalculator()
        {
            var doc = OpenDocument(TestFilesDir.GetTestPath("CoralEggs-dda-min.sky"));
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction);
            var irtCalcDlg = ShowDialog<EditIrtCalcDlg>(peptideSettingsUI.AddCalculator);
            RunUI(() =>
            {
                irtCalcDlg.CreateDatabase(TestFilesDir.GetTestPath(CALCULATOR_NAME + IrtDb.EXT));
                irtCalcDlg.CalcName = CALCULATOR_NAME;
            });
            var calibrateDlg = ShowDialog<CalibrateIrtDlg>(irtCalcDlg.Calibrate);
            RunUI(() => calibrateDlg.StandardName = CALCULATOR_NAME);
            var pickPeptidesDlg = ShowDialog<AddIrtStandardsDlg>(calibrateDlg.UseResults);
            RunUI(() => pickPeptidesDlg.StandardCount = 20);
            var useCirtMessage = ShowDialog<MultiButtonMsgDlg>(pickPeptidesDlg.OkDialog);
            RunUI(() => Assert.AreEqual(string.Format(
                IrtResources
                    .CalibrationGridViewDriver_FindEvenlySpacedPeptides_This_document_contains__0__CiRT_peptides__Would_you_like_to_use__1__of_them_as_your_iRT_standards_,
                33, STANDARD_PEP_COUNT), useCirtMessage.Message));
            OkDialog(useCirtMessage,
                useCirtMessage.BtnYesClick); // Make sure this form goes away before checking for the next MultiButtonMsgDlg
            var recalibrateMessage = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(
                IrtResources
                    .CalibrationGridViewDriver_FindEvenlySpacedPeptides_Would_you_like_to_use_the_predefined_iRT_values_,
                recalibrateMessage.Message));
            OkDialog(recalibrateMessage, recalibrateMessage.Btn1Click); // Don't recalibrate
            RunUI(() => { Assert.AreEqual(STANDARD_PEP_COUNT, calibrateDlg.StandardPeptideCount); });
            RunDlg<GraphRegression>(calibrateDlg.GraphRegression, dlgRegression =>
            {
                Assert.AreEqual(1, dlgRegression.RegressionGraphDatas.Count);
                var regressionData = dlgRegression.RegressionGraphDatas.First();
                Assert.AreEqual(33, regressionData.XValues.Length); // Uses all available CiRT values - should it?
                Assert.AreEqual(2.162, regressionData.RegressionLine.Slope, 0.001);
                Assert.AreEqual(-85.602, regressionData.RegressionLine.Intercept, 0.001);
                dlgRegression.CloseDialog();
            });
            OkDialog(calibrateDlg, calibrateDlg.OkDialog);
            RunUI(() => Assert.AreEqual(STANDARD_PEP_COUNT, irtCalcDlg.StandardPeptideCount));
            var addIrtPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(irtCalcDlg.AddResults);
            RunUI(() =>
            {
                Assert.AreEqual(57, addIrtPeptidesDlg.PeptidesCount);
                Assert.AreEqual(2, addIrtPeptidesDlg.RunsConvertedCount); // 2 replicates
                Assert.AreEqual(0, addIrtPeptidesDlg.RunsFailedCount);
            });
            var recalibratePepsMessage = ShowDialog<MultiButtonMsgDlg>(addIrtPeptidesDlg.OkDialog);
            OkDialog(recalibratePepsMessage, recalibratePepsMessage.ClickNo);
            RunUI(() => Assert.AreEqual(57, irtCalcDlg.LibraryPeptideCount));
            OkDialog(irtCalcDlg, irtCalcDlg.OkDialog);
            OkDialog(peptideSettingsUI,
                peptideSettingsUI.CancelDialog); // The point was to create the new calculator not to use it here

            RunUI(() =>
            {
                var calibratedStandard = Settings.Default.IrtStandardList[CALCULATOR_NAME];
                Assert.IsNotNull(calibratedStandard);
                Assert.AreEqual(STANDARD_PEP_COUNT, calibratedStandard.Peptides.Count);
                Assert.IsNotNull(calibratedStandard.DocXml);
                AssertEx.RoundTrip(calibratedStandard);
                var docStandard = calibratedStandard.GetDocument();
                ValidateDocWithCalibratedIrts(docStandard);
            });
        }

        private static void UseIrtCalculator()
        {
            // Create a new default document and add the new calculator to it
            var docNew = new SrmDocument(SrmSettingsList.GetDefault());
            RunUI(() => SkylineWindow.SwitchDocument(docNew, null));

            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUI.SelectedRTPredictor = CALCULATOR_NAME;
            });
            var addIrtPepsToDoc = ShowDialog<AddIrtStandardsToDocumentDlg>(peptideSettingsUI.OkDialog);
            RunUI(() => addIrtPepsToDoc.NumTransitions = 6);
            OkDialog(addIrtPepsToDoc, addIrtPepsToDoc.BtnYesClick);
            var docWithStandards = WaitForDocumentChangeLoaded(docNew);
            ValidateDocWithCalibratedIrts(docWithStandards);
        }

        private static void ValidateDocWithCalibratedIrts(SrmDocument docWithStandards)
        {
            // Not all standards have 6 transitions, but the fact that there are 94 below shows there are more than 3 each (60 total)
            AssertEx.IsDocumentState(docWithStandards, null, 1, STANDARD_PEP_COUNT, STANDARD_PEP_COUNT, 94);
            // Now make sure they are all fragment ions
            Assert.IsFalse(docWithStandards.PeptideTransitions.Any(t => t.IsMs1));
        }
    }
}