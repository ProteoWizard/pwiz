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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
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

            // 2. Skip iRT's, then cancel on library import prompt, leading to no document change
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textNoError), importIrtMessage =>
            {
                Assert.AreEqual(importIrtMessage.Message,
                    Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_);
                importIrtMessage.Btn1Click();
            });
            var libraryDlgCancel = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgCancel, libraryDlgCancel.BtnCancelClick);
            WaitForDocumentLoaded();
            Assert.AreSame(SkylineWindow.Document, docOld);

            // 3. Import transitions but decline to import iRT's
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textNoError), importIrtMessage =>
            {
                Assert.AreEqual(importIrtMessage.Message,
                    Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_);
                importIrtMessage.Btn1Click();
            });
            SkipLibraryDlg();
            WaitForDocumentLoaded();
            // Transitions have been imported, but not iRT
            RunUI(() => ValidateDocAndIrt(SkylineWindow.DocumentUI, 294, 11, 10));

            // 4. Importing mass list with iRT's into document with existing iRT calculator, no conflicts
            LoadDocument(documentExisting);
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textNoError), importIrtMessage =>
            {
                Assert.AreEqual(importIrtMessage.Message,
                    Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_);
                importIrtMessage.Btn0Click();
            });
            SkipLibraryDlg();
            WaitForDocumentLoaded();
            RunUI(() => ValidateDocAndIrt(SkylineWindow.DocumentUI, 294, 295, 10));

            // 5. Peptide iRT in document conflicts with peptide iRT in database, respond by canceling whole operation
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

            // 6. Peptide iRT in document conflicts with peptide iRT in database, don't overwrite
            LoadDocument(documentExisting);
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt => importIrt.Btn0Click());
            var importIrtConflictOverwriteNo = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(importIrtConflictOverwrite.Message,
                                        TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ImportMassList_The_iRT_calculator_already_contains__0__of_the_imported_peptides_, 1),
                                                                Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_value_or_overwrite_with_the_imported_value_)));
            // Don't overwrite the iRT value
            OkDialog(importIrtConflictOverwriteNo, importIrtConflictOverwriteNo.Btn0Click);
            SkipLibraryDlg();
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

            // 7. If mass list contains a peptide that is already an iRT standard, throw exception
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
            OkDialog(messageDlgStandard, messageDlgStandard.OkDialog);
            WaitForDocumentLoaded();
            Assert.AreSame(docOldStandard, SkylineWindow.Document);

            // 8. Mass list contains different iRT times on same peptide
            LoadDocument(documentExisting);
            var docOldIrt = SkylineWindow.Document;
            string textIrtConflict = TestFilesDir.GetTestPath("OpenSWATH_SM4_InconsistentIrt.csv");
            RunDlg<MessageDlg>(() => SkylineWindow.ImportMassList(textIrtConflict), messageDlg =>
            {
                var expectedMessage = string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__, 
                                              textIrtConflict,
                                              string.Format(Resources.PeptideGroupBuilder_AppendTransition_Two_transitions_of_the_same_precursor___0____have_different_iRT_values___1__and__2___iRT_values_must_be_assigned_consistently_in_an_imported_transition_list_,
                                                            "YVPIHTIDDGYSVIK", 49.8, 50.2));
                expectedMessage = string.Format(Resources.LineColNumberedIoException_FormatMessage__0___line__1__, expectedMessage, 1361);
                Assert.AreEqual(messageDlg.Message, expectedMessage);
                messageDlg.OkDialog();
            });
            WaitForDocumentLoaded();
            Assert.AreSame(docOldIrt, SkylineWindow.Document);

            // 8.1 Mass list contains different iRT values on two non-contiguous lines of the same transition group
            string textIrtGroupConflict = TestFilesDir.GetTestPath("InterleavedInconsistentIrt.csv");
            RunDlg<MessageDlg>(() => SkylineWindow.ImportMassList(textIrtGroupConflict), messageDlg =>
            {
                var expectedMessage = string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__,
                                              textIrtGroupConflict,
                                              string.Format(Resources.PeptideGroupBuilder_AppendTransition_Two_transitions_of_the_same_precursor___0____have_different_iRT_values___1__and__2___iRT_values_must_be_assigned_consistently_in_an_imported_transition_list_,
                                                            "AAAAAAAAAAAAAAAGAAGK", 53, 54));
                Assert.AreEqual(messageDlg.Message, expectedMessage);
                messageDlg.OkDialog();
            });
            WaitForDocumentLoaded();
            Assert.AreSame(docOldIrt, SkylineWindow.Document);


            // 9. iRT not a number leads to error
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
            WaitForDocumentLoaded();
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 10. iRT blank leads to error
            const string textIrtBlank = "PrecursorMz\tProductMz\tTr_recalibrated\tLibraryIntensity\tdecoy\tPeptideSequence\tProteinName\n728.88\t924.539\t\t3305.3\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n";
            RunUI(() => ClipboardEx.SetText(textIrtBlank));
            RunDlg<MessageDlg>(() => SkylineWindow.Paste(), messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, string.Format(Resources.MassListImporter_AddRow_Invalid_iRT_value_in_column__0__on_line__1_, 2, 1));
                messageDlg.OkDialog();
            });
            WaitForDocumentLoaded();
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 11. Library not a number leads to error
            const string textLibraryNan = "PrecursorMz\tProductMz\tTr_recalibrated\tLibraryIntensity\tdecoy\tPeptideSequence\tProteinName\n728.88\t924.539\t30.5\tBAD_LIBRARY\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n";
            RunUI(() => ClipboardEx.SetText(textLibraryNan));
            RunDlg<MessageDlg>(() => SkylineWindow.Paste(), messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, string.Format(Resources.MassListImporter_AddRow_Invalid_library_intensity_value_in_column__0__on_line__1_, 3, 1));
                messageDlg.OkDialog();
            });
            WaitForDocumentLoaded();
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 12. Library blank leads to error
            const string textLibraryBlank = "PrecursorMz\tProductMz\tTr_recalibrated\tLibraryIntensity\tdecoy\tPeptideSequence\tProteinName\n728.88\t924.539\t30.5\t\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n";
            RunUI(() => ClipboardEx.SetText(textLibraryBlank));
            RunDlg<MessageDlg>(() => SkylineWindow.Paste(), messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, string.Format(Resources.MassListImporter_AddRow_Invalid_library_intensity_value_in_column__0__on_line__1_, 3, 1));
                messageDlg.OkDialog();
            });
            WaitForDocumentLoaded();
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 13. Title column missing causes iRT's and library to be skipped
            const string textTitleMissing = "728.88\t924.539\t\t3305.3\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n";
            RunUI(() => ClipboardEx.SetText(textTitleMissing));
            RunUI(() =>
            {
                SkylineWindow.Paste();
                WaitForDocumentLoaded();
                // Transition gets added but not iRT
                ValidateDocAndIrt(SkylineWindow.DocumentUI, 11, 355, 10);
            });

            // 14. Same as 5 but this time do overwrite the iRT value
            LoadDocument(documentExisting);
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt => importIrt.Btn0Click());
            var importIrtConflictOverwriteYes = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(importIrtConflictOverwrite.Message,
                                        TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ImportMassList_The_iRT_calculator_already_contains__0__of_the_imported_peptides_, 1),
                                                                Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_value_or_overwrite_with_the_imported_value_)));
            OkDialog(importIrtConflictOverwriteYes, importIrtConflictOverwriteYes.Btn1Click);
            SkipLibraryDlg();
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

            // 15. Repeat 11, this time no dialog box should show up at all, and the iRT calculator should be unchanged
            var docLoaded = LoadDocument(documentExisting);
            var calculatorOld = docLoaded.Settings.PeptideSettings.Prediction.RetentionTime.Calculator;
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), libraryDlg => libraryDlg.Btn1Click());
            var docMassList = WaitForDocumentChangeLoaded(docLoaded);
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
            Assert.AreSame(calculatorOld, docMassList.Settings.PeptideSettings.Prediction.RetentionTime.Calculator);

            // Test on-the-fly creation of iRT calculator as part of mass list import

            // 16. Attempt to create iRT calculator, then cancel, leaves the document the same
            var documentBlank = TestFilesDir.GetTestPath("AQUA4_Human_Blank.sky");
            var docCreateIrtCancel = LoadDocument(documentBlank);
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt =>
            {
                Assert.AreEqual(importIrt.Message,
                                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_);
                importIrt.Btn0Click();
            });
            var createIrtCalc = WaitForOpenForm<CreateIrtCalculatorDlg>();
            OkDialog(createIrtCalc, createIrtCalc.CancelDialog);
            var docCreateIrtError = WaitForDocumentLoaded();
            // Document hasn't changed
            Assert.AreSame(docCreateIrtCancel, docCreateIrtError);

            // 17. Missing name in CreateIrtCalculatorDlg shows error message
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

            // 18. Missing existing database name shows error message
            RunUI(() =>
            {
                createIrtError.CalculatorName = "test1";
            });
            RunDlg<MessageDlg>(createIrtError.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, Resources.CreateIrtCalculatorDlg_OkDialog_iRT_database_field_must_contain_a_path_to_a_valid_file_);
                messageDlg.OkDialog();
            });

            // 19. Try to open existing database file that doesn't exist
            RunUI(() =>
            {
                createIrtError.ExistingDatabaseName = TestFilesDir.GetTestPath("bad_file_name");
            });
            RunDlg<MessageDlg>(createIrtError.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_iRT_database_field_must_contain_a_path_to_a_valid_file_));
                messageDlg.OkDialog();
            });

            // 20. Try to open existing database that isn't a database file
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

            // 21. Try to open corrupted database file
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

            // 22. Missing new database name shows error message
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

            // 23. Missing textIrt file shows error message
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

            // 24. Blank textIrt file shows error message
            RunUI(() =>
            {
                createIrtError.TextFilename = TestFilesDir.GetTestPath("blank_file.txt");
                createIrtError.NewDatabaseName = newDatabase;
            });
            RunDlg<MessageDlg>(createIrtError.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_Error_reading_iRT_standards_transition_list___0_,
                                                                  Resources.SkylineWindow_importMassListMenuItem_Click_Data_columns_not_found_in_first_line));
                messageDlg.OkDialog();
            });

            OkDialog(createIrtError, createIrtError.CancelDialog);
            WaitForDocumentLoaded();
            // Document hasn't changed
            Assert.AreSame(docCreateIrtError, SkylineWindow.Document);

            // 25. Successful import and successful creation of iRT database and library
            // Document starts empty with no transitions and no iRT calculator
            // 355 transitions, libraries, and iRT times are imported, including libraries for the iRT times
            var docCalcGood = LoadDocument(documentBlank);
            Assert.AreEqual(0, docCalcGood.Transitions.Count());
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
            var libraryDlgAll = WaitForOpenForm<MultiButtonMsgDlg>();
            // Make small change to document to test robustness to concurrent document change
            RunUI(() => SkylineWindow.ModifyDocument("test change", doc =>
            {
                var settingsNew = doc.Settings.ChangeTransitionFilter(filter => filter.ChangeProductCharges(new List<int> { 1, 2, 3 }));
                doc = doc.ChangeSettings(settingsNew);
                return doc;
            }));
            OkDialog(libraryDlgAll, libraryDlgAll.Btn0Click);
            WaitForCondition(6000, () => SkylineWindow.Document.PeptideCount == 355); // Was 3sec wait, but that timed out under dotCover
            RunUI(() =>
            {
                ValidateDocAndIrt(SkylineWindow.DocumentUI, 355, 355, 10);
                var libraries = SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(libraries.HasLibraries);
                Assert.IsTrue(libraries.IsLoaded);
                Assert.AreEqual(1, libraries.Libraries.Count);
                Assert.AreEqual(1, libraries.LibrarySpecs.Count);
                Assert.AreEqual("AQUA4_Human_Blank-assay", libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(355, library.SpectrumCount);
            });
            bool foundLibraryCheck = false;
            foreach (var groupNode in SkylineWindow.Document.TransitionGroups)
            {
                Assert.IsTrue(groupNode.HasLibInfo);
                Assert.IsTrue(groupNode.HasLibRanks);
                foreach (var transition in groupNode.Transitions)
                {
                    // Every transition excpet a and z transitions (which are filtered out in the transition settings) has library info
                    if (Equals(transition.Transition.IonType, IonType.a) || Equals(transition.Transition.IonType, IonType.z))
                        continue;
                    Assert.IsTrue(transition.HasLibInfo);
                    if (String.Equals(groupNode.TransitionGroup.Peptide.Sequence, "DAVNDITAK") && String.Equals(transition.Transition.FragmentIonName, "y7"))
                    {
                        Assert.AreEqual(transition.LibInfo.Intensity, 7336, 1e-2);
                        Assert.AreEqual(transition.LibInfo.Rank, 2);
                        foundLibraryCheck = true;
                    }
                }
            }
            Assert.IsTrue(foundLibraryCheck);
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

            // 26. Successful import and succesful load of existing database, with keeping of iRT's, plus successful library import
            var irtOriginal = TestFilesDir.GetTestPath("irtOriginal.irtdb");
            var docReload = LoadDocument(documentBlank);
            Assert.IsNull(docReload.Settings.PeptideSettings.Prediction.RetentionTime);
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
            var libraryDlgYes = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgYes, libraryDlgYes.Btn0Click);
            var libraryDlgOverwriteYes = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(libraryDlgOverwriteYes.Btn0Click);
            WaitForCondition(3000, () => SkylineWindow.Document.PeptideCount == 345);
            RunUI(() =>
            {
                var calculator = ValidateDocAndIrt(SkylineWindow.DocumentUI, 345, 355, 10);
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key).ToList();
                int conflictIndex = peptides.IndexOf("YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 76.0, 0.1);
                var libraries = SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(libraries.HasLibraries);
                Assert.IsTrue(libraries.IsLoaded);
                Assert.AreEqual(1, libraries.Libraries.Count);
                Assert.AreEqual(1, libraries.LibrarySpecs.Count);
                Assert.AreEqual("AQUA4_Human_Blank-assay", libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(345, library.SpectrumCount);
            });
            // ReSharper disable once PossibleNullReferenceException
            var calcTemp = SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator as RCalcIrt;
            Assert.IsNotNull(calcTemp);
            string dbPath = calcTemp.DatabasePath;
            IrtDb db = IrtDb.GetIrtDb(dbPath, null);
            var oldPeptides = db.GetPeptides().ToList();
            var standardSeq = from peptide in oldPeptides where peptide.Standard select peptide.Sequence;
            standardSeq = standardSeq.ToList();
            foreach (var groupNode in SkylineWindow.Document.TransitionGroups)
            {
                // Every node other than iRT standards now has library info
                if (standardSeq.Contains(groupNode.TransitionGroup.Peptide.Sequence))
                    continue;
                Assert.IsTrue(groupNode.HasLibInfo);
                Assert.IsTrue(groupNode.HasLibRanks);
                foreach (var transition in groupNode.Transitions)
                {
                    Assert.IsTrue(transition.HasLibInfo);
                }
            }
            Assert.IsNotNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.IsNotNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator);
            Assert.IsTrue(Settings.Default.RetentionTimeList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime));
            Assert.IsTrue(Settings.Default.RTScoreCalculatorList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator));
            Assert.IsTrue(Settings.Default.SpectralLibraryList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0]));

            // 27. Successful import and succesful load of existing database, with overwrite of iRT's, plus overwrite of library
            var docImport = LoadDocument(documentBlank);
            Assert.IsNull(docImport.Settings.PeptideSettings.Prediction.RetentionTime);
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
            var libraryDlgYesNew = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgYesNew, libraryDlgYesNew.Btn0Click);
            var libraryDlgOverwrite = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(libraryDlgOverwrite.Btn0Click);
            WaitForCondition(3000, () => SkylineWindow.Document.PeptideCount == 345);
            RunUI(() =>
            {
                var calculator = ValidateDocAndIrt(SkylineWindow.DocumentUI, 345, 355, 10);
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key).ToList();
                int conflictIndex = peptides.IndexOf("YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 49.8, 0.1);
                var libraries = SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(libraries.HasLibraries);
                Assert.IsTrue(libraries.IsLoaded);
                Assert.AreEqual(1, libraries.Libraries.Count);
                Assert.AreEqual(1, libraries.LibrarySpecs.Count);
                Assert.AreEqual("AQUA4_Human_Blank-assay", libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(345, library.SpectrumCount);
            });
            foreach (var groupNode in SkylineWindow.Document.TransitionGroups)
            {
                // Every node other than iRT standards now has library info
                if (standardSeq.Contains(groupNode.TransitionGroup.Peptide.Sequence))
                    continue;
                Assert.IsTrue(groupNode.HasLibInfo);
                Assert.IsTrue(groupNode.HasLibRanks);
                foreach (var transition in groupNode.Transitions)
                {
                    Assert.IsTrue(transition.HasLibInfo);
                }
            }
            Assert.IsNotNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.IsNotNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator);
            Assert.IsTrue(Settings.Default.RetentionTimeList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime));
            Assert.IsTrue(Settings.Default.RTScoreCalculatorList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator));
            Assert.IsTrue(Settings.Default.SpectralLibraryList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0]));

            // 28.  Do exactly the same thing over again, should happen silently, with only a prompt to add library info
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), libraryDlgRepeat => libraryDlgRepeat.Btn0Click());
            WaitForCondition(3000, () => SkylineWindow.Document.PeptideCount == 690);
            RunUI(() =>
            {
                ValidateDocAndIrt(SkylineWindow.DocumentUI, 690, 355, 10);
                var libraries = SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(libraries.HasLibraries);
                Assert.IsTrue(libraries.IsLoaded);
                Assert.AreEqual(1, libraries.Libraries.Count);
                Assert.AreEqual(1, libraries.LibrarySpecs.Count);
                Assert.AreEqual("AQUA4_Human_Blank-assay", libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(345, library.SpectrumCount);
            });


            // 29. Start with blank document, skip iRT's entirely but import library intensities, show this works out fine
            var docLibraryOnly = LoadDocument(documentBlank);
            Assert.IsNull(docLibraryOnly.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.AreEqual(0, SkylineWindow.Document.Transitions.Count());
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textNoError), importIrt => importIrt.Btn1Click());
            var libraryDlgLibOnly = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgLibOnly, libraryDlgLibOnly.Btn0Click);
            var libraryDlgOverwriteLibOnly = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(libraryDlgOverwriteLibOnly.Btn0Click);
            var docLibraryOnlyComplete = WaitForDocumentChangeLoaded(docLibraryOnly);
            // Haven't created a calculator
            Assert.IsNull(docLibraryOnlyComplete.Settings.PeptideSettings.Prediction.RetentionTime);
            RunUI(() =>
            {
                var libraries = docLibraryOnlyComplete.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(libraries.HasLibraries);
                Assert.IsTrue(libraries.IsLoaded);
                Assert.AreEqual(1, libraries.Libraries.Count);
                Assert.AreEqual(1, libraries.LibrarySpecs.Count);
                Assert.AreEqual("AQUA4_Human_Blank-assay", libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(284, library.SpectrumCount);
            });

            // 30. Repeat import with a larger library, show that there are no duplicates and it gets replaced cleanly
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt => importIrt.Btn1Click());
            var libraryDlgBiggerLib = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgBiggerLib, libraryDlgBiggerLib.Btn0Click);
            var docLargeLibrary = WaitForDocumentChangeLoaded(docLibraryOnlyComplete);
            Assert.IsNull(docLargeLibrary.Settings.PeptideSettings.Prediction.RetentionTime);
            RunUI(() =>
            {
                var libraries = docLargeLibrary.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(libraries.HasLibraries);
                Assert.IsTrue(libraries.IsLoaded);
                Assert.AreEqual(1, libraries.Libraries.Count);
                Assert.AreEqual(1, libraries.LibrarySpecs.Count);
                Assert.AreEqual("AQUA4_Human_Blank-assay", libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(345, library.SpectrumCount);
            });


            // 31. Start with blank document, skip iRT's, cancel on library overwrite, document should be the same
            var docCancel = LoadDocument(documentBlank);
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt => importIrt.Btn1Click());
            var libraryDlgCancelNew = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgCancelNew, libraryDlgCancelNew.Btn0Click);
            var libraryDlgOverwriteCancel = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgOverwriteCancel, libraryDlgOverwriteCancel.BtnCancelClick);
            WaitForDocumentLoaded();
            Assert.AreSame(SkylineWindow.Document, docCancel);

            // 32. Start with blank document, skip iRT's, decline to overwrite, only document should have changed
            var docInitial31 = LoadDocument(documentBlank);
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textConflict), importIrt => importIrt.Btn1Click());
            var libraryDlgDecline = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgDecline, libraryDlgDecline.Btn0Click);
            var libraryDlgOverwriteDecline = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgOverwriteDecline, libraryDlgOverwriteDecline.Btn1Click);
            var docComplete31 = WaitForDocumentChangeLoaded(docInitial31);
            RunUI(() =>
            {
                Assert.IsNull(docComplete31.Settings.PeptideSettings.Prediction.RetentionTime);
                var libraries = docComplete31.Settings.PeptideSettings.Libraries;
                Assert.IsFalse(libraries.HasLibraries);
                Assert.AreEqual(345, docComplete31.PeptideCount);
            });

            // 33. Try a different document with interleaved heavy and light transitions of the same peptide
            // Tests mixing of transition groups within a peptide
            var documentInterleaved = TestFilesDir.GetTestPath("Consensus_Dario_iRT_new_blank.sky");
            var textInterleaved = TestFilesDir.GetTestPath("Interleaved.csv");
            RunUI(() => SkylineWindow.OpenFile(documentInterleaved));
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textInterleaved), addIrtDlg => addIrtDlg.Btn0Click());
            var libraryInterleaved = WaitForOpenForm<MultiButtonMsgDlg>();
            // Change to document to test handling of concurrent change during mass list import
            RunUI(() => SkylineWindow.ModifyDocument("test change", doc =>
            {
                var settingsNew = doc.Settings.ChangeTransitionFilter(filter => filter.ChangeProductCharges(new List<int> { 1, 2, 3 }));
                doc = doc.ChangeSettings(settingsNew);
                return doc;
            }));
            OkDialog(libraryInterleaved, libraryInterleaved.Btn0Click);
            WaitForCondition(3000, () => SkylineWindow.Document.PeptideCount == 6);
            // WaitForDocumentLoaded();    TODO: Fix loading with iRT calc but no iRT peptides
            RunUI(() =>
            {
                var docInterleaved = SkylineWindow.DocumentUI;
                Assert.AreEqual(6, docInterleaved.PeptideCount);
                Assert.AreEqual(60, docInterleaved.TransitionCount);
                var libraries = docInterleaved.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(libraries.HasLibraries);
                Assert.IsTrue(libraries.IsLoaded);
                Assert.AreEqual(1, libraries.Libraries.Count);
                Assert.AreEqual(1, libraries.LibrarySpecs.Count);
                Assert.AreEqual("Consensus_Dario_iRT_new_blank-assay", libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(12, library.SpectrumCount);
                foreach (var groupNode in docInterleaved.TransitionGroups)
                {
                    Assert.IsTrue(groupNode.HasLibInfo);
                    Assert.IsTrue(groupNode.HasLibRanks);
                    foreach (var transition in groupNode.Transitions)
                    {
                        Assert.IsTrue(transition.HasLibInfo);
                    }
                }
            });

            // 34. Interleaved transition groups within a peptide can have different iRT's, and they will be averaged together
            var documentInterleavedIrt = TestFilesDir.GetTestPath("Consensus_Dario_iRT_new_blank.sky");
            var textInterleavedIrt = TestFilesDir.GetTestPath("InterleavedDiffIrt.csv");
            RunUI(() => SkylineWindow.OpenFile(documentInterleavedIrt));
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textInterleavedIrt), addIrtDlg => addIrtDlg.Btn0Click());
            var irtOverwrite = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(irtOverwrite, irtOverwrite.Btn1Click);
            var libraryInterleavedIrt = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryInterleavedIrt, libraryInterleavedIrt.Btn0Click);
            var libraryDlgOverwriteIrt = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(libraryDlgOverwriteIrt.Btn0Click);
            WaitForCondition(3000, () => SkylineWindow.Document.PeptideCount == 6);
            var irtValue = SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator.ScoreSequence("AAAAAAAAAAAAAAAGAAGK");
            Assert.IsNotNull(irtValue);
            Assert.AreEqual(irtValue.Value, 52.407, 1e-3);

            TestModificationMatcher();
        }

        public void TestModificationMatcher()
        {
            var documentModMatcher = TestFilesDir.GetTestPath("ETH_Ludo_Heavy.sky");
            LoadDocument(documentModMatcher);
            var docModMatcher = SkylineWindow.Document;

            // If the modifications are readable but don't match the precursor mass, throw an error
            string textModWrongMatch = "PrecursorMz\tProductMz\tPeptideSequence\tProteinName\n" + 1005.9 + "\t" + 868.39 + "\tPVIC[+57]ATQM[+16]LESMTYNPR\t1/YAL038W\n";
            RunUI(() => ClipboardEx.SetText(textModWrongMatch));
            RunDlg<MessageDlg>(() => SkylineWindow.Paste(), messageDlg =>
            {
                string expectedMessage = TextUtil.LineSeparate(
                    string.Format(Resources.LineColNumberedIoException_FormatMessage__0___line__1___col__2__,
                                  string.Format(Resources.MassListRowReader_CalcPrecursorExplanations_Precursor_m_z__0__does_not_math_the_closest_possible_value__1__,
                                                1005.9, 1013.9734, 8.0734), 1, 1),
                                  Resources.MzMatchException_suggestion);
                Assert.AreEqual(expectedMessage, messageDlg.Message);
                messageDlg.OkDialog();
            });
            WaitForDocumentLoaded();
            Assert.AreSame(docModMatcher, SkylineWindow.Document);

            // When mods are unreadable, default to the approach of deducing modified state from precursor mz
            const string textModifiedSeqExpected = "PVIC[+57.0]ATQM[+16.0]LESMTYNPR";
            string textModPrefix = "PrecursorMz\tProductMz\tPeptideSequence\tProteinName\n" + 1013.9 + "\t" + 868.39 + "\t";
            string textModUnreadMod = textModPrefix + "PVIC[CAM]ATQM[bad_mod_&^$]LESMTYNPR\t1/YAL038W\n";
            RunUI(() => ClipboardEx.SetText(textModUnreadMod));
            RunUI(() =>
            {
                SkylineWindow.Paste();
                WaitForCondition(3000, () => SkylineWindow.DocumentUI.PeptideCount == 1);
                var peptideNode = SkylineWindow.DocumentUI.Peptides.First();
                Assert.AreEqual(peptideNode.ModifiedSequence, textModifiedSeqExpected);
            });

            // When there are no mods, default to the approach of deducing modified state from precursor mz
            LoadDocument(documentModMatcher);
            string textModNone = textModPrefix + "PVICATQMLESMTYNPR\t1/YAL038W\n";
            RunUI(() => ClipboardEx.SetText(textModNone));
            RunUI(() =>
            {
                SkylineWindow.Paste();
                WaitForCondition(3000, () => SkylineWindow.DocumentUI.PeptideCount == 1);
                var peptideNode = SkylineWindow.DocumentUI.Peptides.First();
                Assert.AreEqual(peptideNode.ModifiedSequence, textModifiedSeqExpected);
            });

            // By specifying mods explicitly, we can distinguish between oxidations at two different sites
            LoadDocument(documentModMatcher);
            string textModFirst = textModPrefix + "PVIC[+57]ATQM[+16]LESMTYNPR\t1/YAL038W\n";
            RunUI(() => ClipboardEx.SetText(textModFirst));
            RunUI(() =>
            {
                SkylineWindow.Paste();
                WaitForCondition(3000, () => SkylineWindow.DocumentUI.PeptideCount == 1);
                var peptideNode = SkylineWindow.DocumentUI.Peptides.First();
                Assert.AreEqual(peptideNode.ModifiedSequence, textModifiedSeqExpected);
            });

            LoadDocument(documentModMatcher);
            string textModSecond = textModPrefix + "PVIC[+" + string.Format("{0:F01}", 57) + "]ATQMLESM[+" + string.Format("{0:F01}", 16) + "]TYNPR\t1/YAL038W\n";
            RunUI(() => ClipboardEx.SetText(textModSecond));
            RunUI(() =>
            {
                SkylineWindow.Paste();
                WaitForCondition(3000, () => SkylineWindow.DocumentUI.PeptideCount == 1);
                var peptideNode = SkylineWindow.DocumentUI.Peptides.First();
                Assert.AreEqual(peptideNode.ModifiedSequence, "PVIC[+57.0]ATQMLESM[+16.0]TYNPR");
            });


            // Test a difficult case containing modifications of the same peptide at two different sites, make sure Skyline handles it correctly
            var documentToughCase = TestFilesDir.GetTestPath("ToughModCase.sky");
            var textToughCase = TestFilesDir.GetTestPath("ToughModCase.csv");
            LoadDocument(documentToughCase);
            RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.ImportMassList(textToughCase), importIrt => importIrt.Btn1Click());
            SkipLibraryDlg();
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                var peptides = SkylineWindow.DocumentUI.Peptides.ToList();
                Assert.AreEqual(peptides.Count, 2);
                Assert.AreEqual(peptides[0].ModifiedSequence, "AALIM[+16.0]QVLQLTADQIAMLPPEQR");
                Assert.AreEqual(peptides[1].ModifiedSequence, "AALIMQVLQLTADQIAM[+16.0]LPPEQR");
                Assert.AreEqual(peptides[0].TransitionGroupCount, 1);
                Assert.AreEqual(peptides[1].TransitionGroupCount, 1);
                Assert.AreEqual(peptides[0].TransitionCount, 6);
                Assert.AreEqual(peptides[1].TransitionCount, 6);
            });
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

        public SrmDocument LoadDocument(string document)
        {
            RunUI(() => SkylineWindow.OpenFile(document));
            return WaitForDocumentLoaded();
        }

        public void SkipLibraryDlg()
        {
            var libraryDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(libraryDlg.Btn1Click);
        }
    }
}
