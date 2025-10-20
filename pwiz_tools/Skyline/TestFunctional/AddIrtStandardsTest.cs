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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AddIrtStandardsTest : AbstractFunctionalTest
    {
        private const int CIRT_STANDARD_COUNT = 20;
        private const string CIRT_STANDARDS_NAME = "20 CiRT Peptides";
        private const string CIRT_DATABASE_NAME = "20Cirt.irtdb";
        private const string CIRT_CALCULATOR_NAME = "CirtCalculator";

        private const int DOC_STANDARD_COUNT = 50;
        private const string DOC_STANDARDS_NAME = "50 Document Peptides";
        private const string DOC_DATABASE_NAME = "50Doc.irtdb";
        private const string DOC_CALCULATOR_NAME = "DocCalculator";
        [TestMethod]
        public void TestAddIrtStandards()
        {
            TestFilesZip = @"TestFunctional\AddIrtStandardsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AddIrtStandardsTest.sky"));
            });
            WaitForDocumentLoaded();
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunUI(()=>peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction);
                CreateCalculatorWithCirtPeptides(peptideSettingsUi);
                Assert.IsTrue(Settings.Default.IrtStandardList.ContainsKey(CIRT_STANDARDS_NAME));
                Assert.IsTrue(Settings.Default.RTScoreCalculatorList.ContainsKey(CIRT_CALCULATOR_NAME));
                CreateCalculatorWithDocumentPeptides(peptideSettingsUi);
                Assert.IsTrue(Settings.Default.IrtStandardList.ContainsKey(DOC_STANDARDS_NAME));
                Assert.IsTrue(Settings.Default.RTScoreCalculatorList.ContainsKey(DOC_CALCULATOR_NAME));
            }, peptideSettingsUi=>peptideSettingsUi.OkDialog());
            RunUI(() =>
            {
                SkylineWindow.NewDocument();
            });

            // Create a new document using the CIRT calculator and make sure the correct number of peptides get added
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunDlg<EditRTDlg>(peptideSettingsUi.AddRTRegression, editRtDlg =>
                {
                    editRtDlg.ChooseCalculator(CIRT_CALCULATOR_NAME);
                    editRtDlg.OkDialog();
                });
                RunDlg<AddIrtStandardsToDocumentDlg>(peptideSettingsUi.OkDialog, addIrtStandardsToDocumentDlg =>
                {
                    addIrtStandardsToDocumentDlg.BtnYesClick();
                });
            }, peptideSettingsUi=>{});
            Assert.AreEqual(CIRT_STANDARD_COUNT, SkylineWindow.Document.PeptideCount);
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("CirtStandards.sky"));
                SkylineWindow.NewDocument();
            });

            // Create a new document using the other iRT calculator and make sure the correct number of peptides get added
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunDlg<EditRTDlg>(peptideSettingsUi.AddRTRegression, editRtDlg =>
                {
                    editRtDlg.ChooseCalculator(DOC_CALCULATOR_NAME);
                    editRtDlg.OkDialog();
                });
                RunDlg<AddIrtStandardsToDocumentDlg>(peptideSettingsUi.OkDialog, addIrtStandardsToDocumentDlg =>
                {
                    addIrtStandardsToDocumentDlg.BtnYesClick();
                });
            }, peptideSettingsUi => { });
            Assert.AreEqual(DOC_STANDARD_COUNT, SkylineWindow.Document.PeptideCount);
        }

        /// <summary>
        /// Create an iRT database using 20 peptides from the document, and say "Yes" when asked
        /// if you want to use the CiRT peptides that were found in the document
        /// </summary>
        private void CreateCalculatorWithCirtPeptides(PeptideSettingsUI peptideSettingsUi)
        {
            var editIrtCalcDlg = ShowDialog<EditIrtCalcDlg>(peptideSettingsUi.EditCalculator);
            var calibrateDlg = ShowDialog<CalibrateIrtDlg>(editIrtCalcDlg.Calibrate);
            var countDlg = ShowDialog<AddIrtStandardsDlg>(calibrateDlg.UseResults);
            RunUI(() =>
            {
                countDlg.StandardCount = CIRT_STANDARD_COUNT;
            });
            var useCirtPeptidesDlg = ShowDialog<MultiButtonMsgDlg>(countDlg.OkDialog);
            Assert.AreEqual(string.Format(IrtResources
                    .CalibrationGridViewDriver_FindEvenlySpacedPeptides_This_document_contains__0__CiRT_peptides__Would_you_like_to_use__1__of_them_as_your_iRT_standards_,
                27, 20), useCirtPeptidesDlg.Message);
            OkDialog(useCirtPeptidesDlg, useCirtPeptidesDlg.ClickYes);
            var usePredefinedCirtValuesDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            Assert.AreEqual(IrtResources.CalibrationGridViewDriver_FindEvenlySpacedPeptides_Would_you_like_to_use_the_predefined_iRT_values_, usePredefinedCirtValuesDlg.Message);
            OkDialog(usePredefinedCirtValuesDlg, usePredefinedCirtValuesDlg.ClickYes);

            RunUI(() =>
            {
                calibrateDlg.StandardName = CIRT_STANDARDS_NAME;
            });
            OkDialog(calibrateDlg, calibrateDlg.OkDialog);
            var addIrtPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(editIrtCalcDlg.AddResults);
            OkDialog(addIrtPeptidesDlg, addIrtPeptidesDlg.OkDialog);
            var recalibrateIrtDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            StringAssert.StartsWith(recalibrateIrtDlg.Message, Resources.LibraryGridViewDriver_AddToLibrary_Do_you_want_to_recalibrate_the_iRT_standard_values_relative_to_the_peptides_being_added_);
            OkDialog(recalibrateIrtDlg, recalibrateIrtDlg.ClickNo);
            
            RunUI(() =>
            {
                editIrtCalcDlg.CreateDatabase(TestFilesDir.GetTestPath(CIRT_DATABASE_NAME));
                editIrtCalcDlg.CalcName = CIRT_CALCULATOR_NAME;
            });
            OkDialog(editIrtCalcDlg, editIrtCalcDlg.OkDialog);
        }

        /// <summary>
        /// Create an iRT database using 50 peptides from the document. Because there are only
        /// 27 available CiRT peptides in the document, the questions about using CiRT will not
        /// be asked.
        /// </summary>
        private void CreateCalculatorWithDocumentPeptides(PeptideSettingsUI peptideSettingsUi)
        {
            var editIrtCalcDlg = ShowDialog<EditIrtCalcDlg>(peptideSettingsUi.EditCalculator);
            var calibrateDlg = ShowDialog<CalibrateIrtDlg>(editIrtCalcDlg.Calibrate);
            var countDlg = ShowDialog<AddIrtStandardsDlg>(calibrateDlg.UseResults);
            RunUI(() =>
            {
                countDlg.StandardCount = DOC_STANDARD_COUNT;
            });
            OkDialog(countDlg, countDlg.OkDialog);
            RunUI(() =>
            {
                calibrateDlg.StandardName = DOC_STANDARDS_NAME;
            });
            OkDialog(calibrateDlg, calibrateDlg.OkDialog);
            var addIrtPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(editIrtCalcDlg.AddResults);
            OkDialog(addIrtPeptidesDlg, addIrtPeptidesDlg.OkDialog);
            var recalibrateIrtDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            StringAssert.StartsWith(recalibrateIrtDlg.Message, Resources.LibraryGridViewDriver_AddToLibrary_Do_you_want_to_recalibrate_the_iRT_standard_values_relative_to_the_peptides_being_added_);
            OkDialog(recalibrateIrtDlg, recalibrateIrtDlg.ClickNo);

            RunUI(() =>
            {
                editIrtCalcDlg.CreateDatabase(TestFilesDir.GetTestPath(DOC_DATABASE_NAME));
                editIrtCalcDlg.CalcName = DOC_CALCULATOR_NAME;
            });
            OkDialog(editIrtCalcDlg, editIrtCalcDlg.OkDialog);
        }
    }
}
