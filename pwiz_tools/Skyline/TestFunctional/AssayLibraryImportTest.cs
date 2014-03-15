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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AssayLibraryImportTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAssayLibraryImport()
        {
            TestFilesZip = @"TestFunctional\AssayLibraryImportTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var documentExisting = TestFilesDir.GetTestPath("AQUA4_Human_Existing_Calc.sky");
            // 1. Import mass list with iRT's into document, then cancel
            LoadDocument(documentExisting);
            string textNoError = TestFilesDir.GetTestPath("OpenSWATH_SM4_NoError.csv");
            var docOld = SkylineWindow.Document;
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textNoError), importIrtMessage =>
            {
                Assert.AreEqual(importIrtMessage.Message,
                    Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_);
                importIrtMessage.BtnCancelClick();
            });
            WaitForDocumentLoaded();
            // Document should be reference equal to what it was before
            Assert.AreSame(SkylineWindow.Document, docOld);

            // 2. Import transitions but decline to import iRT's
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textNoError), importIrtMessage =>
            {
                Assert.AreEqual(importIrtMessage.Message,
                    Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_);
                importIrtMessage.Btn1Click();
            });
            WaitForDocumentLoaded();
            // Transitions have been imported, but not iRT
            RunUI(() => ValidateDocAndIrt(SkylineWindow.DocumentUI, 294, 11, 10));

            // 3. Importing mass list with iRT's into document with existing iRT calculator, no conflicts
            LoadDocument(documentExisting);
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textNoError), importIrtMessage =>
            {
                Assert.AreEqual(importIrtMessage.Message,
                    Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_);
                importIrtMessage.Btn0Click();
            });
            WaitForDocumentLoaded();
            RunUI(() => ValidateDocAndIrt(SkylineWindow.DocumentUI, 294, 295, 10));

            // 4. Peptide iRT in document conflicts with peptide iRT in database, respond by canceling whole operation
            LoadDocument(documentExisting);
            var docOldImport = SkylineWindow.Document;
            string textConflict = TestFilesDir.GetTestPath("OpenSWATH_SM4_Overwrite.csv");
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt => importIrt.Btn0Click());
            var importIrtConflictOverwrite = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(importIrtConflictOverwrite.Message,
                                        TextUtil.LineSeparate( string.Format(Resources.SkylineWindow_ImportMassList_The_iRT_calculator_already_contains__0__of_the_imported_peptides_, 1),
                                                                Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_value_or_overwrite_with_the_imported_value_)));
            OkDialog(importIrtConflictOverwrite, importIrtConflictOverwrite.BtnCancelClick);
            WaitForDocumentLoaded();
            // Document is reference equal to what it was before, even if we cancel at second menu
            Assert.AreSame(docOldImport, SkylineWindow.Document);

            // 5. Peptide iRT in document conflicts with peptide iRT in database, don't overwrite
            LoadDocument(documentExisting);
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt => importIrt.Btn0Click());
            var importIrtConflictOverwriteNo = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(importIrtConflictOverwrite.Message,
                                        TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ImportMassList_The_iRT_calculator_already_contains__0__of_the_imported_peptides_, 1),
                                                                Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_value_or_overwrite_with_the_imported_value_)));
            // Don't overwrite the iRT value
            OkDialog(importIrtConflictOverwriteNo, importIrtConflictOverwriteNo.Btn0Click);
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                var calculator = ValidateDocAndIrt(SkylineWindow.DocumentUI, 355, 355, 10);
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key).ToList();
                int conflictIndex = peptides.IndexOf("YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 76.0, 0.1);
            });

            // 6. If mass list contains a peptide that is already an iRT standard, throw exception
            LoadDocument(documentExisting);
            var docOldStandard = SkylineWindow.Document;
            string textStandard = TestFilesDir.GetTestPath("OpenSWATH_SM4_StandardsConflict.csv");
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textStandard), importIrt => importIrt.Btn0Click());
            var importIrtConflictOverwriteConflict = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(importIrtConflictOverwriteConflict, importIrtConflictOverwriteConflict.Btn1Click);
            var messageDlgStandard = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(messageDlgStandard.Message,
                string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__,
                              textStandard,
                              string.Format(Resources.SkylineWindow_AddIrtPeptides_Imported_peptide__0__with_iRT_library_value_is_already_being_used_as_an_iRT_standard_,
                                            "GTFIIDPGGVIR"))));
            messageDlgStandard.OkDialog();
            Assert.AreSame(docOldStandard, SkylineWindow.Document);

            // 7. Mass list contains different iRT times on same peptide
            LoadDocument(documentExisting);
            var docOldIrt = SkylineWindow.Document;
            string textIrtConflict = TestFilesDir.GetTestPath("OpenSWATH_SM4_InconsistentIrt.csv");
            RunDlg<MessageDlg>(() => SkylineWindow.ImportMassList(textIrtConflict), messageDlg =>
            {
                var expectedMessage = string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__, 
                                              textIrtConflict,
                                              string.Format(Resources.PeptideGroupBuilder_AppendTransition_Two_transitions_of_the_same_peptide___0____have_different_iRT_values___1__and__2___iRT_values_must_be_assigned_consistently_in_an_imported_transition_list_,
                                                            "YVPIHTIDDGYSVIK", 49.8, 50.2));
                expectedMessage = string.Format(Resources.LineColNumberedIoException_FormatMessage__0___line__1__, expectedMessage, 1361);
                Assert.AreEqual(messageDlg.Message, expectedMessage);
                messageDlg.OkDialog();
            });
            Assert.AreSame(docOldIrt, SkylineWindow.Document);

            // 8. iRT not a number leads to error
            LoadDocument(documentExisting);
            var docNanIrt = SkylineWindow.Document;
            WaitForDocumentLoaded();
            const string textIrtNan = "PrecursorMz\tProductMz\tTr_recalibrated\tLibraryIntensity\tdecoy\tPeptideSequence\tProteinName\n728.88\t924.539\tBAD_IRT\t3305.3\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n";
            RunUI(() => ClipboardEx.SetText(textIrtNan));
            RunDlg<MessageDlg>(() => SkylineWindow.Paste(), messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, string.Format(Resources.MassListImporter_AddRow_Invalid_iRT_value_in_column__0__on_line__1_, 2, 1));
                messageDlg.OkDialog();
            });
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 9. iRT blank leads to error
            const string textIrtBlank = "PrecursorMz\tProductMz\tTr_recalibrated\tLibraryIntensity\tdecoy\tPeptideSequence\tProteinName\n728.88\t924.539\t\t3305.3\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n";
            RunUI(() => ClipboardEx.SetText(textIrtBlank));
            RunDlg<MessageDlg>(() => SkylineWindow.Paste(), messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, string.Format(Resources.MassListImporter_AddRow_Invalid_iRT_value_in_column__0__on_line__1_, 2, 1));
                messageDlg.OkDialog();
            });
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 10. Title column missing causes iRT's to be skipped
            const string textTitleMissing = "728.88\t924.539\t\t3305.3\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n";
            RunUI(() => ClipboardEx.SetText(textTitleMissing));
            RunUI(() =>
            {
                SkylineWindow.Paste();
                // Transition gets added but not iRT
                ValidateDocAndIrt(SkylineWindow.DocumentUI, 11, 355, 10);
            });

            // 11. Same as 5 but this time do overwrite the iRT value
            LoadDocument(documentExisting);
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt => importIrt.Btn0Click());
            var importIrtConflictOverwriteYes = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(importIrtConflictOverwrite.Message,
                                        TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ImportMassList_The_iRT_calculator_already_contains__0__of_the_imported_peptides_, 1),
                                                                Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_value_or_overwrite_with_the_imported_value_)));
            OkDialog(importIrtConflictOverwriteYes, importIrtConflictOverwriteYes.Btn1Click);
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                var calculator = ValidateDocAndIrt(SkylineWindow.DocumentUI, 355, 355, 10);
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key).ToList();
                int conflictIndex = peptides.IndexOf("YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 49.8, 0.1);
            });

            // 12. Repeat 11, this time no dialog box should show up at all, and the iRT calculator should be unchanged
            LoadDocument(documentExisting);
            var calculatorOld = SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator;
            RunUI(() =>
            {
                SkylineWindow.ImportMassList(textConflict);
                var calculator = ValidateDocAndIrt(SkylineWindow.DocumentUI, 355, 355, 10);
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key).ToList();
                int conflictIndex = peptides.IndexOf("YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 49.8, 0.1);
            });
            Assert.AreSame(calculatorOld, SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator);

            // Test on-the-fly creation of iRT calculator as part of mass list import

            // 13. Attempt to create iRT calculator, then cancel, leaves the document the same
            var documentBlank = TestFilesDir.GetTestPath("AQUA4_Human_Blank.sky");
            LoadDocument(documentBlank);
            var docCreateIrtCancel = SkylineWindow.Document;
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt =>
            {
                Assert.AreEqual(importIrt.Message,
                                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_);
                importIrt.Btn0Click();
            });
            var createIrtCalc = WaitForOpenForm<CreateIrtCalculatorDlg>();
            OkDialog(createIrtCalc, createIrtCalc.CancelDialog);
            WaitForDocumentLoaded();
            // Document hasn't changed
            Assert.AreSame(docCreateIrtCancel, SkylineWindow.Document);

            // 14. Missing name in CreateIrtCalculatorDlg shows error message
            var docCreateIrtError = SkylineWindow.Document;
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt => importIrt.Btn0Click());
            var createIrtError = WaitForOpenForm<CreateIrtCalculatorDlg>();
            RunUI(() =>
            {
                createIrtError.UseExisting = true;
            });
            RunDlg<MessageDlg>(createIrtError.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, Resources.CreateIrtCalculatorDlg_OkDialog_Calculator_name_cannot_be_empty);
                messageDlg.OkDialog();
            });

            // 15. Missing existing database name shows error message
            RunUI(() =>
            {
                createIrtError.CalculatorName = "test1";
            });
            RunDlg<MessageDlg>(createIrtError.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, Resources.CreateIrtCalculatorDlg_OkDialog_iRT_database_field_must_contain_a_path_to_a_valid_file_);
                messageDlg.OkDialog();
            });

            // 16. Try to open existing database file that doesn't exist
            RunUI(() =>
            {
                createIrtError.ExistingDatabaseName = TestFilesDir.GetTestPath("bad_file_name");
            });
            RunDlg<MessageDlg>(createIrtError.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_iRT_database_field_must_contain_a_path_to_a_valid_file_));
                messageDlg.OkDialog();
            });

            // 17. Try to open existing database that isn't a database file
            RunUI(() =>
            {
                createIrtError.ExistingDatabaseName = textConflict;
            });
            RunDlg<MessageDlg>(createIrtError.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_Failed_to_open_the_database_file___0_,
                                                                  string.Format(Resources.IrtDb_GetIrtDb_The_file__0__could_not_be_opened, textConflict)));
                messageDlg.OkDialog();
            });

            // 18. Try to open corrupted database file
            var irtFileCorrupted = TestFilesDir.GetTestPath("irtAll_corrupted.irtdb");
            RunUI(() =>
            {
                createIrtError.ExistingDatabaseName = irtFileCorrupted;
            });
            RunDlg<MessageDlg>(createIrtError.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_Failed_to_open_the_database_file___0_,
                                                                  string.Format(Resources.IrtDb_GetIrtDb_The_file__0__could_not_be_opened, irtFileCorrupted)));
                messageDlg.OkDialog();
            });

            // 19. Missing new database name shows error message
            string newDatabase = TestFilesDir.GetTestPath("irtNew.irtdb");
            string textIrt = TestFilesDir.GetTestPath("OpenSWATH_SM4_iRT.csv");
            RunUI(() =>
            {
                createIrtError.UseExisting = false;
                createIrtError.TextFilename = textIrt;
            });
            RunDlg<MessageDlg>(createIrtError.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, Resources.CreateIrtCalculatorDlg_OkDialog_iRT_database_field_must_not_be_empty_);
                messageDlg.OkDialog();
            });

            // 20. Missing textIrt file shows error message
            RunUI(() =>
            {
                createIrtError.TextFilename = "";
                createIrtError.NewDatabaseName = newDatabase;
            });
            RunDlg<MessageDlg>(createIrtError.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, Resources.CreateIrtCalculatorDlg_OkDialog_Transition_list_field_must_contain_a_path_to_a_valid_file_);
                messageDlg.OkDialog();
            });

            OkDialog(createIrtError, createIrtError.CancelDialog);
            WaitForDocumentLoaded();
            // Document hasn't changed
            Assert.AreSame(docCreateIrtError, SkylineWindow.Document);

            // 21. Successful import and successful creation of database
            // Document starts empty with no transitions and no iRT calculator
            Assert.IsNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.AreEqual(0, SkylineWindow.Document.Transitions.Count());
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt => importIrt.Btn0Click());
            var createIrtCalcGood = WaitForOpenForm<CreateIrtCalculatorDlg>();
            RunUI(() =>
            {
                createIrtCalcGood.UseExisting = false;
                createIrtCalcGood.CalculatorName = "test1";
                createIrtCalcGood.TextFilename = textIrt;
                createIrtCalcGood.NewDatabaseName = newDatabase;
            });
            OkDialog(createIrtCalcGood, createIrtCalcGood.OkDialog);
            RunUI(() => ValidateDocAndIrt(SkylineWindow.DocumentUI, 355, 355, 10));
            Assert.IsNotNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.IsNotNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator);
            Assert.IsTrue(Settings.Default.RetentionTimeList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime));
            Assert.IsTrue(Settings.Default.RTScoreCalculatorList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator));
            // Check iRT peptides are the same as the ones in the document tree
            var documentPeptides = SkylineWindow.Document.Peptides.Select(pep => pep.ModifiedSequence).ToList();
            var calc = SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator as RCalcIrt;
            Assert.IsNotNull(calc);
            var irtPeptides = calc.PeptideScores.Select(kvp => kvp.Key).ToList();
            Assert.AreEqual(documentPeptides.Count, irtPeptides.Count);
            Assert.AreEqual(documentPeptides.Count, documentPeptides.Intersect(irtPeptides).Count());

            // 22. Successful import and succesful load of existing database, with keeping of iRT's
            var irtOriginal = TestFilesDir.GetTestPath("irtOriginal.irtdb");
            LoadDocument(documentBlank);
            Assert.IsNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.AreEqual(0, SkylineWindow.Document.Transitions.Count());
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt => importIrt.Btn0Click());
            var createIrtCalcExisting = WaitForOpenForm<CreateIrtCalculatorDlg>();
            RunUI(() =>
            {
                createIrtCalcExisting.UseExisting = true;
                createIrtCalcExisting.CalculatorName = "test2";
                createIrtCalcExisting.ExistingDatabaseName = irtOriginal;
            });
            OkDialog(createIrtCalcExisting, createIrtCalcExisting.OkDialog);
            var dlgKeep = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(dlgKeep, dlgKeep.Btn0Click);
            RunUI(() =>
            {
                var calculator = ValidateDocAndIrt(SkylineWindow.DocumentUI, 345, 355, 10);
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key).ToList();
                int conflictIndex = peptides.IndexOf("YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 76.0, 0.1);
            });
            Assert.IsNotNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.IsNotNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator);
            Assert.IsTrue(Settings.Default.RetentionTimeList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime));
            Assert.IsTrue(Settings.Default.RTScoreCalculatorList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator));

            // 23. Successful import and succesful load of existing database, with overwrite of iRT's
            LoadDocument(documentBlank);
            Assert.IsNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.AreEqual(0, SkylineWindow.Document.Transitions.Count());
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt => importIrt.Btn0Click());
            var createIrtCalcExistingOverwrite = WaitForOpenForm<CreateIrtCalculatorDlg>();
            RunUI(() =>
            {
                createIrtCalcExistingOverwrite.UseExisting = true;
                createIrtCalcExistingOverwrite.CalculatorName = "test2";
                createIrtCalcExistingOverwrite.ExistingDatabaseName = irtOriginal;
            });
            RunDlg<MultiButtonMsgDlg>(createIrtCalcExistingOverwrite.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, Resources.CreateIrtCalculatorDlg_OkDialog_A_calculator_with_that_name_already_exists___Do_you_want_to_replace_it_);
                messageDlg.Btn1Click();
            });
            var dlgOverwrite = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(dlgOverwrite, dlgOverwrite.Btn1Click);
            RunUI(() =>
            {
                var calculator = ValidateDocAndIrt(SkylineWindow.DocumentUI, 345, 355, 10);
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key).ToList();
                int conflictIndex = peptides.IndexOf("YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 49.8, 0.1);
            });
            Assert.IsNotNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.IsNotNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator);
            Assert.IsTrue(Settings.Default.RetentionTimeList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime));
            Assert.IsTrue(Settings.Default.RTScoreCalculatorList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator));
        }

        public static RCalcIrt ValidateDocAndIrt(SrmDocument doc, int peptides, int irtTotal, int irtStandards)
        {
            Assert.AreEqual(peptides, SkylineWindow.DocumentUI.PeptideCount);
            var calculator = doc.Settings.PeptideSettings.Prediction.RetentionTime.Calculator as RCalcIrt;
            Assert.IsNotNull(calculator);
            var peptideSeqs = calculator.PeptideScores.Select(item => item.Key).ToList();
            Assert.AreEqual(irtTotal, peptideSeqs.Count);
            Assert.AreEqual(irtStandards, calculator.GetStandardPeptides(peptideSeqs).Count());
            return calculator;
        }

        public void LoadDocument(string document)
        {
            RunUI(() => SkylineWindow.OpenFile(document));
            WaitForDocumentLoaded();
        }
    }
}
