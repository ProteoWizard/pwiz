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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
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
            TestAssayImportGeneral();
            TestModificationMatcher();
            TestBlankDocScenario();
            TestEmbeddedIrts();

            TestAssayImport2();
        }

        protected void TestAssayImportGeneral()
        {
            var documentExisting = TestFilesDir.GetTestPath("AQUA4_Human_Existing_Calc.sky");
            // 1. Import mass list with iRT's into document, then cancel
            LoadDocument(documentExisting);
            string textNoError = TestFilesDir.GetTestPath("OpenSWATH_SM4_NoError.csv");
            var docOld = SkylineWindow.Document;
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textNoError, importIrtMessage =>
            {
                Assert.AreEqual(importIrtMessage.Message,
                    Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_);
                importIrtMessage.BtnCancelClick();
            });
            WaitForDocumentLoaded();
            // Document should be reference equal to what it was before
            Assert.AreSame(SkylineWindow.Document, docOld);

            // 2. Skip iRT's, then cancel on library import prompt, leading to no document change
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textNoError, importIrtMessage =>
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
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textNoError, importIrtMessage =>
            {
                Assert.AreEqual(importIrtMessage.Message,
                    Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_);
                importIrtMessage.Btn1Click();
            });
            SkipLibraryDlg();
            WaitForDocumentLoaded();
            // Transitions have been imported, but not iRT
            ValidateDocAndIrt(294, 11, 10);

            // 4. Importing mass list with iRT's into document with existing iRT calculator, no conflicts
            LoadDocument(documentExisting);
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textNoError, importIrtMessage =>
            {
                Assert.AreEqual(importIrtMessage.Message,
                    Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_);
                importIrtMessage.Btn0Click();
            });
            SkipLibraryDlg();
            WaitForDocumentLoaded();
            ValidateDocAndIrt(294, 295, 10);

            // 5. Peptide iRT in document conflicts with peptide iRT in database, respond by canceling whole operation
            LoadDocument(documentExisting);
            var docOldImport = SkylineWindow.Document;
            string textConflict = TestFilesDir.GetTestPath("OpenSWATH_SM4_Overwrite.csv");
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textConflict, importIrt => importIrt.Btn0Click());
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
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textConflict, importIrt => importIrt.Btn0Click());
            var importIrtConflictOverwriteNo = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(importIrtConflictOverwriteNo.Message,
                                        TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ImportMassList_The_iRT_calculator_already_contains__0__of_the_imported_peptides_, 1),
                                                                Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_value_or_overwrite_with_the_imported_value_)));
            // Don't overwrite the iRT value
            OkDialog(importIrtConflictOverwriteNo, importIrtConflictOverwriteNo.Btn0Click);
            SkipLibraryDlg();
            WaitForDocumentLoaded();
            var calculator = ValidateDocAndIrt(355, 355, 10);
            RunUI(() =>
            {
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key.Sequence).ToList();
                int conflictIndex = peptides.IndexOf("YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 76.0, 0.1);
            });

            // 7. If mass list contains a peptide that is already an iRT standard, throw exception
            LoadDocument(documentExisting);
            var docOldStandard = SkylineWindow.Document;
            string textStandard = TestFilesDir.GetTestPath("OpenSWATH_SM4_StandardsConflict.csv");
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textStandard, importIrt => importIrt.Btn0Click());
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
            ImportTransitionListSkipColumnSelectWithMessage(textIrtConflict, 
                string.Format(Resources.PeptideGroupBuilder_FinalizeTransitionGroups_Two_transitions_of_the_same_precursor___0___m_z__1_____have_different_iRT_values___2__and__3___iRT_values_must_be_assigned_consistently_in_an_imported_transition_list_,
                                                    "YVPIHTIDDGYSVIK", 864.458, 49.8, 50.2), 1, false);
            WaitForDocumentLoaded();
            Assert.AreSame(docOldIrt, SkylineWindow.Document);

            // 8.1 Mass list contains different iRT values on two non-contiguous lines of the same transition group
            string textIrtGroupConflict = TestFilesDir.GetTestPath("InterleavedInconsistentIrt.csv");
            ImportTransitionListSkipColumnSelectWithMessage(textIrtGroupConflict, null, 59, false);
            WaitForDocumentLoaded();
            Assert.AreSame(docOldIrt, SkylineWindow.Document);
            // Now remove the modified column which is bogus and causing errors
            RemoveColumn(textIrtGroupConflict, 22);
            ImportTransitionListSkipColumnSelectWithMessage(textIrtGroupConflict,
                string.Format(Resources.PeptideGroupBuilder_FinalizeTransitionGroups_Two_transitions_of_the_same_precursor___0___m_z__1_____have_different_iRT_values___2__and__3___iRT_values_must_be_assigned_consistently_in_an_imported_transition_list_,
                    "AAAAAAAAAAAAAAAGAAGK", 492.9385, 53, 54), 2, false);
            WaitForDocumentLoaded();
            Assert.AreSame(docOldIrt, SkylineWindow.Document);

            // 8.2 Try again, this time click OK on the error dialog, accepting all transitions except the 3 with errors. Also, deal with ion mobility.
            string textIrtGroupConflictAccept = TestFilesDir.GetTestPath("InterleavedInconsistentIrtWithIonMobility.csv"); // Same as before but also has column of fake 1/K0 values as 1+(precursorMZ/2)
            ImportTransitionListSkipColumnSelectWithMessage(textIrtGroupConflictAccept,
                string.Format(Resources.PeptideGroupBuilder_FinalizeTransitionGroups_Two_transitions_of_the_same_precursor___0___m_z__1_____have_different_iRT_values___2__and__3___iRT_values_must_be_assigned_consistently_in_an_imported_transition_list_,
                    "AAAAAAAAAAAAAAAGAAGK", 492.9385, 53, 54), 2, true);
            var confirmIrtDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(confirmIrtDlg, confirmIrtDlg.Btn0Click);
            var libraryAcceptDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryAcceptDlg, libraryAcceptDlg.Btn0Click);
            ValidateDocAndIrt(16, 361, 10);
            RunUI(() =>
            {
                var docCurrent = SkylineWindow.DocumentUI;
                // All of the transitions are there except for the ones with errors
                Assert.AreEqual(docCurrent.PeptideTransitionCount, 109);
                Assert.AreEqual(docCurrent.PeptideTransitionGroupCount, 22);
                // Spectral library results are there
                var currentLibraries = docCurrent.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(currentLibraries.HasLibraries);
                Assert.IsTrue(currentLibraries.IsLoaded);
                Assert.AreEqual(1, currentLibraries.Libraries.Count);
                Assert.AreEqual(1, currentLibraries.LibrarySpecs.Count);
                Assert.AreEqual("AQUA4_Human_Existing_Calc-assay", currentLibraries.LibrarySpecs[0].Name);
                var currentLibrary = currentLibraries.Libraries[0];
                Assert.AreEqual(12, currentLibrary.SpectrumCount);
                Assert.AreEqual(12, docCurrent.MoleculeTransitionGroups.Count(tg => tg.ExplicitValues.IonMobility.HasValue));
                // The data has fake ion mobility values set as 1+(mz/2), this should appear in document and in library
                var mobilities = new HashSet<double>();
                foreach (var tg in docCurrent.MoleculeTransitionGroups.Where(n => n.ExplicitValues.IonMobility.HasValue))
                {
                    var imExpected = 1 + (0.5 * tg.PrecursorMz);
                    AssertEx.IsTrue(Math.Abs(imExpected - tg.ExplicitValues.IonMobility.Value) < 0.0001);
                    mobilities.Add(tg.ExplicitValues.IonMobility.Value);
                }
                foreach(var key in currentLibrary.Keys)
                {
                    var spec = currentLibrary.GetSpectra(key, IsotopeLabelType.light, LibraryRedundancy.all).First();
                    AssertEx.IsTrue(mobilities.Any(im => Math.Abs(im - spec.IonMobilityInfo.IonMobility.Mobility.Value) < 0.0001));
                }
            });

            // 9. iRT not a number leads to error
            LoadDocument(documentExisting);
            var docNanIrt = SkylineWindow.Document;
            WaitForDocumentLoaded();
            const string textIrtNan = "PrecursorMz\tProductMz\tTr_recalibrated\tLibraryIntensity\tdecoy\tPeptideSequence\tProteinName\n728.88\t924.539\tBAD_IRT\t3305.3\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n";
            RunUI(() => ClipboardEx.SetText(textIrtNan));
            PasteTransitionListSkipColumnSelectWithMessage(string.Format(
                Resources.MassListImporter_AddRow_Invalid_iRT_value_at_precusor_m_z__0__for_peptide__1_,
                728.88,
                "ADSTGTLVITDPTR"), 1, true);
            WaitForDocumentLoaded();
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 10. iRT blank leads to error
            const string textIrtBlank = "PrecursorMz\tProductMz\tiRT\tLibraryIntensity\tdecoy\tPeptideSequence\tProteinName\n728.88\t924.539\t\t3305.3\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n";
            RunUI(() => ClipboardEx.SetText(textIrtBlank));
            PasteTransitionListSkipColumnSelectWithMessage(string.Format(Resources.MassListImporter_AddRow_Invalid_iRT_value_at_precusor_m_z__0__for_peptide__1_,
                                                        728.88,
                                                        "ADSTGTLVITDPTR"), 1, true);
            WaitForDocumentLoaded();
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 11. Library not a number leads to error
            const string textLibraryNan = "PrecursorMz\tProductMz\tiRT\tLibraryIntensity\tdecoy\tPeptideSequence\tProteinName\n728.88\t924.539\t30.5\tBAD_LIBRARY\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n";
            RunUI(() => ClipboardEx.SetText(textLibraryNan));
            PasteTransitionListSkipColumnSelectWithMessage(string.Format(Resources.MassListImporter_AddRow_Invalid_library_intensity_at_precursor__0__for_peptide__1_,
                                                        728.88,
                                                        "ADSTGTLVITDPTR"), 1, true);
            WaitForDocumentLoaded();
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 12. Library blank leads to error
            const string textLibraryBlank = "PrecursorMz\tProductMz\tTr_recalibrated\tRelaTive_IntEnsity\tdecoy\tPeptideSequence\tProteinName\n728.88\t924.539\t30.5\t\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n";
            RunUI(() => ClipboardEx.SetText(textLibraryBlank));
            PasteTransitionListSkipColumnSelectWithMessage(string.Format(Resources.MassListImporter_AddRow_Invalid_library_intensity_at_precursor__0__for_peptide__1_,
                                                        728.88,
                                                        "ADSTGTLVITDPTR"), 1, true);
            WaitForDocumentLoaded();
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 13. Title column missing causes iRT's and library to be skipped
            ForgetPreviousImportColumnsSelection(); // For purposes of this test we don't want to use the preciously confirmed input columns
            const string textTitleMissing = "728.88\t924.539\t\t3305.3\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n";
            RunUI(() => ClipboardEx.SetText(textTitleMissing));

            using (new WaitDocumentChange(null, true))
            {
                PasteTransitionListSkipColumnSelect();
            }

            // Transition gets added but not iRT
            ValidateDocAndIrt(11, 361, 10);

            // 14. Same as 5 but this time do overwrite the iRT value
            LoadDocument(documentExisting);
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textConflict, importIrt => importIrt.Btn0Click());
            var importIrtConflictOverwriteYes = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(importIrtConflictOverwriteYes.Message,
                                        TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ImportMassList_The_iRT_calculator_already_contains__0__of_the_imported_peptides_, 1),
                                                                Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_value_or_overwrite_with_the_imported_value_)));
            OkDialog(importIrtConflictOverwriteYes, importIrtConflictOverwriteYes.Btn1Click);
            SkipLibraryDlg();
            WaitForDocumentLoaded();
            calculator = ValidateDocAndIrt(355, 361, 10);
            RunUI(() =>
            {
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key.Sequence).ToList();
                int conflictIndex = peptides.IndexOf("YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 49.8, 0.1);
            });

            // 15. Repeat 11, this time no dialog box should show up at all, and the iRT calculator should be unchanged
            var docLoaded = LoadDocument(documentExisting);
            var calculatorOld = docLoaded.Settings.PeptideSettings.Prediction.RetentionTime.Calculator;
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textConflict, libraryDlg => libraryDlg.Btn1Click());
            var docMassList = WaitForDocumentChangeLoaded(docLoaded);
            calculator = ValidateDocAndIrt(355, 361, 10);
            RunUI(() => 
            {
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key.Sequence).ToList();
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
            docCreateIrtCancel = AllowAllIonTypes();
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textConflict, importIrt =>
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
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textConflict, importIrt => importIrt.Btn0Click());
            var createIrtError = WaitForOpenForm<CreateIrtCalculatorDlg>();
            RunUI(() =>
            {
                createIrtError.IrtImportType = CreateIrtCalculatorDlg.IrtType.existing;
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
                createIrtError.IrtImportType = CreateIrtCalculatorDlg.IrtType.separate_list;
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

            // Test creation of iRT calculator from protein embedded in the imported transition list
            
            // 24.1 Empty database name for creating from protein shows error message
            RunUI(() =>
            {
                createIrtError.IrtImportType = CreateIrtCalculatorDlg.IrtType.protein;
            });
            RunDlg<MessageDlg>(createIrtError.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, Resources.CreateIrtCalculatorDlg_OkDialog_iRT_database_field_must_not_be_empty_);
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
            Assert.AreEqual(0, docCalcGood.PeptideTransitions.Count());
            AllowAllIonTypes();
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textConflict, importIrt => importIrt.Btn0Click());
            var createIrtCalcGood = WaitForOpenForm<CreateIrtCalculatorDlg>();
            RunUI(() =>
            {
                createIrtCalcGood.IrtImportType = CreateIrtCalculatorDlg.IrtType.separate_list;
                createIrtCalcGood.CalculatorName = "test1";
                createIrtCalcGood.TextFilename = textIrt;
                createIrtCalcGood.NewDatabaseName = newDatabase;
            });
            OkDialog(createIrtCalcGood, createIrtCalcGood.OkDialog);
            var libraryDlgAll = WaitForOpenForm<MultiButtonMsgDlg>();
            // Make small change to document to test robustness to concurrent document change
            RunUI(() => SkylineWindow.ModifyDocument("test change", doc =>
            {
                var settingsNew = doc.Settings.ChangeTransitionFilter(filter => filter.ChangePeptideProductCharges(Adduct.ProtonatedFromCharges(1, 2, 3)));
                doc = doc.ChangeSettings(settingsNew);
                return doc;
            }));
            OkDialog(libraryDlgAll, libraryDlgAll.Btn0Click);
            WaitForDocumentLoaded();
            ValidateDocAndIrt(355, 355, 10);
            RunUI(() =>
            {
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
            foreach (var groupNode in SkylineWindow.Document.PeptideTransitionGroups)
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
            var documentPeptides = SkylineWindow.Document.Peptides.Select(pep => pep.ModifiedTarget).ToList();
            var calc = SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator as RCalcIrt;
            Assert.IsNotNull(calc);
            var irtPeptides = calc.PeptideScores.Select(kvp => kvp.Key).ToList();
            Assert.AreEqual(documentPeptides.Count, irtPeptides.Count);
            Assert.AreEqual(documentPeptides.Count, documentPeptides.Intersect(irtPeptides).Count());

            // 26. Successful import and succesful load of existing database, with keeping of iRT's, plus successful library import
            var irtOriginal = TestFilesDir.GetTestPath("irtOriginal.irtdb");
            var docReload = LoadDocument(documentBlank);
            Assert.IsNull(docReload.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.AreEqual(0, SkylineWindow.Document.PeptideTransitionCount);
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textConflict, importIrt => importIrt.Btn0Click());
            var createIrtCalcExisting = WaitForOpenForm<CreateIrtCalculatorDlg>();
            RunUI(() =>
            {
                createIrtCalcExisting.IrtImportType = CreateIrtCalculatorDlg.IrtType.existing;
                createIrtCalcExisting.CalculatorName = "test2";
                createIrtCalcExisting.ExistingDatabaseName = irtOriginal;
            });
            OkDialog(createIrtCalcExisting, createIrtCalcExisting.OkDialog);
            var dlgKeep = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ImportMassList_The_iRT_calculator_already_contains__0__of_the_imported_peptides_, 1),
                Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_value_or_overwrite_with_the_imported_value_), dlgKeep.Message));
            OkDialog(dlgKeep, dlgKeep.Btn0Click);
            var libraryDlgYes = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_spectral_library_intensities___Create_a_document_library_from_these_intensities_, libraryDlgYes.Message));
            OkDialog(libraryDlgYes, libraryDlgYes.Btn0Click);
            var libraryDlgOverwriteYes = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => AssertEx.AreComparableStrings(Resources.SkylineWindow_ImportMassList_There_is_an_existing_library_with_the_same_name__0__as_the_document_library_to_be_created___Overwrite_this_library_or_skip_import_of_library_intensities_, libraryDlgOverwriteYes.Message));
            OkDialog(libraryDlgOverwriteYes, libraryDlgOverwriteYes.Btn0Click);
            WaitForDocumentLoaded();
            calculator = ValidateDocAndIrt(345, 355, 10);
            RunUI(() =>
            {
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key.Sequence).ToList();
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
            var oldPeptides = db.ReadPeptides().ToList();
            var standardSeq = from peptide in oldPeptides where peptide.Standard select peptide.Target;
            standardSeq = standardSeq.ToList();
            foreach (var groupNode in SkylineWindow.Document.PeptideTransitionGroups)
            {
                // Every node other than iRT standards now has library info
                if (standardSeq.Contains(groupNode.TransitionGroup.Peptide.Target))
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
            VerifyEmptyRTRegression();
            Assert.IsTrue(Settings.Default.RTScoreCalculatorList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator));
            Assert.IsTrue(Settings.Default.SpectralLibraryList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0]));

            // 27. Successful import and succesful load of existing database, with overwrite of iRT's, plus overwrite of library
            var docImport = LoadDocument(documentBlank);
            Assert.IsNull(docImport.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.AreEqual(0, SkylineWindow.Document.PeptideTransitions.Count());
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textConflict, importIrt => importIrt.Btn0Click());
            var createIrtCalcExistingOverwrite = WaitForOpenForm<CreateIrtCalculatorDlg>();
            RunUI(() =>
            {
                createIrtCalcExistingOverwrite.IrtImportType = CreateIrtCalculatorDlg.IrtType.existing;
                createIrtCalcExistingOverwrite.CalculatorName = "test2";
                createIrtCalcExistingOverwrite.ExistingDatabaseName = irtOriginal;
            });
            RunDlg<MultiButtonMsgDlg>(createIrtCalcExistingOverwrite.OkDialog, messageDlg =>
            {
                Assert.AreEqual(messageDlg.Message, Resources.CreateIrtCalculatorDlg_OkDialog_A_calculator_with_that_name_already_exists___Do_you_want_to_replace_it_);
                messageDlg.Btn1Click();
            });
            var dlgOverwrite = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ImportMassList_The_iRT_calculator_already_contains__0__of_the_imported_peptides_, 1),
                Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_value_or_overwrite_with_the_imported_value_), dlgOverwrite.Message));
            OkDialog(dlgOverwrite, dlgOverwrite.Btn1Click);
            var libraryDlgYesNew = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_spectral_library_intensities___Create_a_document_library_from_these_intensities_,
                libraryDlgYesNew.Message));
            OkDialog(libraryDlgYesNew, libraryDlgYesNew.Btn0Click);
            var libraryDlgOverwrite = WaitForOpenForm<MultiButtonMsgDlg>();
            string libraryName = Path.GetFileNameWithoutExtension(documentBlank) + BiblioSpecLiteSpec.ASSAY_NAME;
            RunUI(() => Assert.AreEqual(string.Format(Resources.SkylineWindow_ImportMassList_There_is_an_existing_library_with_the_same_name__0__as_the_document_library_to_be_created___Overwrite_this_library_or_skip_import_of_library_intensities_, libraryName),
                libraryDlgOverwrite.Message));
            OkDialog(libraryDlgOverwrite, libraryDlgOverwrite.Btn0Click);
            WaitForDocumentLoaded();
            var openAlert = FindOpenForm<AlertDlg>();
            if (openAlert != null)
                Assert.Fail("Found unexpected alert: {0}", openAlert.Message);

            calculator = ValidateDocAndIrt(345, 355, 10);
            RunUI(() =>
            {
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key.Sequence).ToList();
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
            foreach (var groupNode in SkylineWindow.Document.PeptideTransitionGroups)
            {
                // Every node other than iRT standards now has library info
                if (standardSeq.Contains(groupNode.TransitionGroup.Peptide.Target))
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
            VerifyEmptyRTRegression();
            Assert.IsTrue(Settings.Default.RTScoreCalculatorList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator));
            Assert.IsTrue(Settings.Default.SpectralLibraryList.Contains(SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0]));

            // 28.  Do exactly the same thing over again, should happen silently, with only a prompt to add library info
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textConflict, libraryDlgRepeat => libraryDlgRepeat.Btn0Click());
            WaitForDocumentLoaded();
            ValidateDocAndIrt(690, 355, 10);
            RunUI(() =>
            {
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
            Assert.AreEqual(0, SkylineWindow.Document.PeptideTransitions.Count());
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textNoError, importIrt => importIrt.Btn1Click());
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
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textConflict, importIrt => importIrt.Btn1Click());
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
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textConflict, importIrt => importIrt.Btn1Click());
            var libraryDlgCancelNew = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgCancelNew, libraryDlgCancelNew.Btn0Click);
            var libraryDlgOverwriteCancel = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgOverwriteCancel, libraryDlgOverwriteCancel.BtnCancelClick);
            WaitForDocumentLoaded();
            Assert.AreSame(SkylineWindow.Document, docCancel);

            // 32. Start with blank document, skip iRT's, decline to overwrite, only document should have changed
            var docInitial31 = LoadDocument(documentBlank);
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textConflict, importIrt => importIrt.Btn1Click());
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
            RemoveColumn(textInterleaved, 22);
            RunUI(() => SkylineWindow.OpenFile(documentInterleaved));
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textInterleaved, addIrtDlg => addIrtDlg.Btn0Click());
            var libraryInterleaved = WaitForOpenForm<MultiButtonMsgDlg>();
            // Change to document to test handling of concurrent change during mass list import
            RunUI(() => SkylineWindow.ModifyDocument("test change", doc =>
            {
                var settingsNew = doc.Settings.ChangeTransitionFilter(filter => filter.ChangePeptideProductCharges(Adduct.ProtonatedFromCharges(1, 2, 3 )));
                doc = doc.ChangeSettings(settingsNew);
                return doc;
            }));
            OkDialog(libraryInterleaved, libraryInterleaved.Btn0Click);
            TryWaitForConditionUI(6000, () => SkylineWindow.DocumentUI.PeptideCount == 6);  // Peptide count checked below
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                var docInterleaved = SkylineWindow.DocumentUI;
                Assert.AreEqual(6, docInterleaved.PeptideCount);
                Assert.AreEqual(60, docInterleaved.PeptideTransitionCount);
                var libraries = docInterleaved.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(libraries.HasLibraries);
                Assert.IsTrue(libraries.IsLoaded);
                Assert.AreEqual(1, libraries.Libraries.Count);
                Assert.AreEqual(1, libraries.LibrarySpecs.Count);
                Assert.AreEqual("Consensus_Dario_iRT_new_blank-assay", libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(12, library.SpectrumCount);
                foreach (var groupNode in docInterleaved.PeptideTransitionGroups)
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
            RemoveColumn(textInterleavedIrt, 22);
            RunUI(() => SkylineWindow.OpenFile(documentInterleavedIrt));
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textInterleavedIrt, addIrtDlg => addIrtDlg.Btn0Click());
            var irtOverwrite = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(irtOverwrite, irtOverwrite.Btn1Click);
            var libraryInterleavedIrt = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryInterleavedIrt, libraryInterleavedIrt.Btn0Click);
            var libraryDlgOverwriteIrt = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(libraryDlgOverwriteIrt.Btn0Click);
            TryWaitForConditionUI(6000, () => SkylineWindow.DocumentUI.PeptideCount == 6);
            WaitForDocumentLoaded();
            Assert.AreEqual(6, SkylineWindow.Document.PeptideCount);
            var irtValue = SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator.ScoreSequence(new Target("AAAAAAAAAAAAAAAGAAGK"));
            Assert.IsNotNull(irtValue);
            Assert.AreEqual(irtValue.Value, 52.407, 1e-3);

            // Make sure all the iRT magic works even when no spectral library info is present
            // (regression test in response to crash when libraries not present)
            var textInterleavedIrtNoLib = TestFilesDir.GetTestPath("InterleavedDiffIrt.csv");
            RunUI(() => SkylineWindow.OpenFile(documentInterleavedIrt));
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textInterleavedIrtNoLib, addIrtDlg => addIrtDlg.Btn0Click());
            var irtOverwriteNoLib = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(irtOverwriteNoLib, irtOverwriteNoLib.Btn1Click);
            TryWaitForConditionUI(6000, () => SkylineWindow.DocumentUI.PeptideCount == 6);
            WaitForDocumentLoaded();
            Assert.AreEqual(6, SkylineWindow.Document.PeptideCount);
            var irtValueNoLib = SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator.ScoreSequence(new Target("AAAAAAAAAAAAAAAGAAGK"));
            Assert.IsNotNull(irtValueNoLib);
            Assert.AreEqual(irtValueNoLib.Value, 52.407, 1e-3);

        }

        private static void ForgetPreviousImportColumnsSelection()
        {
            RunUI(() => Settings.Default.CustomImportTransitionListColumnTypesList = null);
        }

        private static SrmDocument AllowAllIonTypes()
        {
            RunUI(() => SkylineWindow.ModifyDocument("Allow all fragment ions - because test file contains a2", doc =>
            {
                return doc.ChangeSettings(doc.Settings.ChangeTransitionFilter(f =>
                    f.ChangePeptideIonTypes(Transition.PEPTIDE_ION_TYPES)));
            }));
            return SkylineWindow.Document;
        }

        private static void VerifyEmptyRTRegression()
        {
            RetentionTimeRegression rtRegression;
            Assert.IsTrue(
                Settings.Default.RetentionTimeList.TryGetValue(
                    SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Name, out rtRegression));
            Assert.IsNull(rtRegression.Conversion);
                // Probably not what was expected when this test was written, but no regression is created, because document lacks standard peptide targets
            Assert.AreEqual(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime,
                rtRegression.ClearEquations());
        }

        private static void RemoveColumn(string textIrtGroupConflict, int columnIndex)
        {
            // Now Skyline will choose the modified column which causes different errors. So we remove it.
            string[] lines = File.ReadAllLines(textIrtGroupConflict);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] =
                    string.Join(TextUtil.SEPARATOR_TSV.ToString(),
                        lines[i].ParseDsvFields(TextUtil.SEPARATOR_TSV)
                            .Where((s, j) => j != columnIndex).ToArray());
            }
            File.WriteAllLines(textIrtGroupConflict, lines);
        }

        public void TestModificationMatcher()
        {
            var documentModMatcher = TestFilesDir.GetTestPath("ETH_Ludo_Heavy.sky");
            LoadDocument(documentModMatcher);
            var docModMatcher = SkylineWindow.Document;

            // If the modifications are readable but don't match the precursor mass, throw an error
            string textModWrongMatch = "PrecursorMz\tProductMz\tPeptideSequence\tProteinName\n" + 1005.9 + "\t" + 868.39 + "\tPVIC[+57]ATQM[+16]LESMTYNPR\t1/YAL038W\n";
            RunUI(() => ClipboardEx.SetText(textModWrongMatch));
            PasteTransitionListSkipColumnSelectWithMessage(TextUtil.SpaceSeparate(string.Format(Resources.MassListRowReader_CalcPrecursorExplanations_,
                        1005.9, 1013.9734, 8.0734, "PVICATQMLESMTYNPR"),
                    Resources.MzMatchException_suggestion),
                1, false);
            WaitForDocumentLoaded();
            Assert.AreSame(docModMatcher, SkylineWindow.Document);

            // When mods are unreadable, default to the approach of deducing modified state from precursor mz
            const string textModifiedSeqExpected = "PVIC[+57.021464]ATQM[+15.994915]LESMTYNPR";
            double precursorMz = 1013.9, productMz = 868.39;
            const string textModPrefixFormat = "PrecursorMz\tProductMz\tPeptideSequence\tProteinName\n{0}\t{1}\t";
            string textModPrefix = string.Format(textModPrefixFormat, precursorMz, productMz);
            string textModUnreadMod = textModPrefix + "PVIC[CAM]ATQM[bad_mod_&^$]LESMTYNPR\t1/YAL038W\n";
            RunUI(() => ClipboardEx.SetText(textModUnreadMod));
            ForgetPreviousImportColumnsSelection();
            PasteOnePeptide(textModifiedSeqExpected);

            // When there are no mods, default to the approach of deducing modified state from precursor mz
            LoadDocument(documentModMatcher);
            string textModNone = textModPrefix + "PVICATQMLESMTYNPR\t1/YAL038W\n";
            RunUI(() => ClipboardEx.SetText(textModNone));
            PasteOnePeptide(textModifiedSeqExpected);

            // By specifying mods explicitly, we can distinguish between oxidations at two different sites
            LoadDocument(documentModMatcher);
            string textModFirst = textModPrefix + "PVIC[+57]ATQM[+16]LESMTYNPR\t1/YAL038W\n";
            RunUI(() => ClipboardEx.SetText(textModFirst));
            PasteOnePeptide(textModifiedSeqExpected);

            LoadDocument(documentModMatcher);
            textModPrefix = string.Format(textModPrefixFormat, precursorMz, productMz + 16); // Add +16 to product which now contains the mod
            string textModSecond = textModPrefix + "PVIC[+" + string.Format("{0:F01}", 57) + "]ATQMLESM[+" + string.Format("{0:F01}", 16) + "]TYNPR\t1/YAL038W\n";
            RunUI(() => ClipboardEx.SetText(textModSecond));
            PasteOnePeptide("PVIC[+57.021464]ATQMLESM[+15.994915]TYNPR");


            // Test a difficult case containing modifications of the same peptide at two different sites, make sure Skyline handles it correctly
            var documentToughCase = TestFilesDir.GetTestPath("ToughModCase.sky");
            var textToughCase = TestFilesDir.GetTestPath("ToughModCase.csv");
            for (int i = 0; i < 2; ++i)
            {
                LoadDocument(documentToughCase);
                if (i == 1)
                {
                    // Works even when none of these transitions are allowed by the settings
                    SkylineWindow.Document.Settings.TransitionSettings.Filter.ChangePeptideIonTypes(new [] {IonType.z});
                    SkylineWindow.Document.Settings.TransitionSettings.Filter.ChangePeptidePrecursorCharges(Adduct.ProtonatedFromCharges( 5 ));
                }
                using (new WaitDocumentChange())
                {
                    ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textToughCase, importIrt => importIrt.Btn1Click());
                    SkipLibraryDlg();
                }
                WaitForDocumentLoaded();
                RunUI(() =>
                {
                    var peptides = SkylineWindow.DocumentUI.Peptides.ToList();
                    Assert.AreEqual(2, peptides.Count);
                    Assert.AreEqual("AALIM[+15.994915]QVLQLTADQIAMLPPEQR", peptides[0].ModifiedSequence);
                    Assert.AreEqual("AALIMQVLQLTADQIAM[+15.994915]LPPEQR", peptides[1].ModifiedSequence);
                    Assert.AreEqual(1, peptides[0].TransitionGroupCount);
                    Assert.AreEqual(1, peptides[1].TransitionGroupCount);
                    Assert.AreEqual(6, peptides[0].TransitionCount);
                    Assert.AreEqual(6, peptides[1].TransitionCount);
                });    
            }
            RunUI(() => SkylineWindow.SaveDocument());

            // Show we can import data (response to issue preventing data import on assay libraries)
            // Import the raw data
            var askDecoysDlg = ShowDialog<MultiButtonMsgDlg>(SkylineWindow.ImportResults);
            var importResultsDlg = ShowDialog<ImportResultsDlg>(askDecoysDlg.ClickNo);
            RunUI(() =>
            {
                string fileName = TestFilesDir.GetTestPath("OverlapTest.mzML");
                importResultsDlg.RadioAddNewChecked = true;
                var path = new KeyValuePair<string, MsDataFileUri[]>[1];
                path[0] = new KeyValuePair<string, MsDataFileUri[]>(fileName,
                                            new[] { MsDataFileUri.Parse(fileName) });
                importResultsDlg.NamedPathSets = path;
            });
            OkDialog(importResultsDlg, importResultsDlg.OkDialog);
            WaitForConditionUI(2 * 60 * 1000, () => SkylineWindow.DocumentUI.Settings.MeasuredResults.IsLoaded);    // 2 minutes
            RunUI(() => SkylineWindow.SaveDocument());

            // Show data can be imported with modified sequence column following bare sequence column
            var skyChooseSeqCol = TestFilesDir.GetTestPath("AutoDetectModColumn.sky");
            string pathChooseSeqCol = TestFilesDir.GetTestPath("phl004_canonical_s64_osw-small-all.tsv");
            LoadDocument(skyChooseSeqCol);
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(pathChooseSeqCol, addIrtDlg => addIrtDlg.Btn1Click());
            var addLibraryDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => addLibraryDlg.Btn1Click());
            RunUI(() => SkylineWindow.SaveDocument());
            // Reload to test that appropriate modifications have actually been added to the document
            LoadDocument(skyChooseSeqCol);
        }

        private static void PasteOnePeptide(string textModifiedSeqExpected)
        {
            PasteTransitionListSkipColumnSelect();
            TryWaitForCondition(3000, () => SkylineWindow.Document.PeptideCount == 1);
            Assert.AreEqual(1, SkylineWindow.Document.PeptideCount);
            var peptideNode = SkylineWindow.Document.Peptides.First();
            Assert.AreEqual(textModifiedSeqExpected, peptideNode.ModifiedSequence);
        }

        protected void TestBlankDocScenario()
        {
            RunUI(() => SkylineWindow.NewDocument(true));
            var docOld = SkylineWindow.Document;
            string textNoError = TestFilesDir.GetTestPath("Interleaved.csv");
            ImportTransitionListSkipColumnSelectWithMessage(textNoError, null, 30, true);
            var irtDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(irtDlg, irtDlg.Btn1Click);
            var libraryAcceptDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryAcceptDlg, libraryAcceptDlg.Btn0Click);
            var saveDocumentDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(saveDocumentDlg, saveDocumentDlg.BtnCancelClick);
            WaitForDocumentLoaded();
            Assert.IsTrue(ReferenceEquals(docOld, SkylineWindow.Document));

            // TODO: test a successful save of the document.  Current issue is that SaveDocument Dlg can't be used in functional tests...
        }

        protected void TestEmbeddedIrts()
        {
            var documentBlank = TestFilesDir.GetTestPath("AQUA4_Human_Blank.sky");
            LoadDocument(documentBlank);
            AllowAllIonTypes();
            string textEmbedded = TestFilesDir.GetTestPath("OpenSWATH_SM4_Combined.csv");
            RemoveColumn(textEmbedded, 11); // Remove bad modified sequence column that import would use
            ImportTransitionListSkipColumnSelectWithMessage<MultiButtonMsgDlg>(textEmbedded, importIrt => importIrt.Btn0Click());
            var createIrtDlg = WaitForOpenForm<CreateIrtCalculatorDlg>();
            string newDatabase = TestFilesDir.GetTestPath("irtEmbedded.irtdb");
            RunUI(() =>
            {
                createIrtDlg.CalculatorName = "irtEmbedded";
                createIrtDlg.IrtImportType = CreateIrtCalculatorDlg.IrtType.protein;
                createIrtDlg.NewDatabaseNameProtein = newDatabase;
                Assert.AreEqual(createIrtDlg.GetProtein(0), "IRT");
                createIrtDlg.SelectedProtein = "IRT";
                Assert.AreEqual(createIrtDlg.CountProteins, 14);
            });
            OkDialog(createIrtDlg, createIrtDlg.OkDialog);
            SkipLibraryDlg();
            WaitForDocumentLoaded();
            ValidateDocAndIrt(294, 294, 10);
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void TestAssayImport2()
        {
            RunUI(() => SkylineWindow.NewDocument());

            var csvFile = TestFilesDir.GetTestPath("OpenSWATH_SM4_NoError.csv");
            var saveDlg = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.ImportAssayLibrary(csvFile));
            OkDialog(saveDlg, saveDlg.BtnCancelClick);
            var doc = SkylineWindow.Document;

            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editMods = ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettings.EditHeavyMods);
            RunUI(() =>
            {
                editMods.AddItem(new StaticMod("Label:13C(6)15N(2) (C-term K)", "K", ModTerminus.C, LabelAtoms.C13 | LabelAtoms.N15));
                editMods.AddItem(new StaticMod("Label:13C(6)15N(4) (C-term R)", "R", ModTerminus.C, LabelAtoms.C13 | LabelAtoms.N15));
            });
            OkDialog(editMods, editMods.OkDialog);
            RunUI(() =>
            {
                peptideSettings.SetIsotopeModifications(0, true);
                peptideSettings.SetIsotopeModifications(1, true);
            });
            OkDialog(peptideSettings, peptideSettings.OkDialog);
            doc = WaitForDocumentChange(doc);

            var skyFile = TestFilesDir.GetTestPath("assayimport.sky");
            RunUI(() => Assert.IsTrue(SkylineWindow.SaveDocument(skyFile)));
            doc = WaitForDocumentChange(doc);

            // Import assay library and choose a protein
            ImportAssayLibrarySkipColumnSelect(csvFile);
            var chooseIrt = WaitForOpenForm<ChooseIrtStandardPeptidesDlg>();
            const string irtProteinName = "AQUA4SWATH_HMLangeG";
            RunUI(() =>
            {
                var proteinNames = chooseIrt.ProteinNames.ToArray();
                AssertEx.AreEqualDeep(proteinNames, new List<string>{
                    "AQUA4SWATH_HMLangeA", "AQUA4SWATH_HMLangeB", "AQUA4SWATH_HMLangeC", "AQUA4SWATH_HMLangeD", "AQUA4SWATH_HMLangeE", "AQUA4SWATH_HMLangeF", "AQUA4SWATH_HMLangeG",
                    "AQUA4SWATH_HumanEbhardt", "AQUA4SWATH_Lepto", "AQUA4SWATH_MouseSabido", "AQUA4SWATH_MycoplasmaSchmidt", "AQUA4SWATH_PombeSchmidt", "AQUA4SWATH_Spyo"
                });
            });
            OkDialog(chooseIrt, () => chooseIrt.OkDialogProtein(irtProteinName));
            doc = WaitForDocumentChange(doc);
            AssertEx.IsDocumentState(doc, null, 14, 284, 1119);
            Assert.AreEqual(irtProteinName, doc.PeptideGroups.First().Name);
            CheckAssayLibrarySettings();

            // Undo import
            RunUI(SkylineWindow.Undo);
            WaitForDocumentChange(doc);

            // Import assay library and choose a file
            doc = AllowAllIonTypes();
            var irtCsvFile = TestFilesDir.GetTestPath("OpenSWATH_SM4_iRT.csv");
            var overwriteDlg = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.ImportAssayLibrary(csvFile)); 
            var transitionSelectdgl = ShowDialog<ImportTransitionListColumnSelectDlg>(overwriteDlg.BtnYesClick); // Expect a confirmation of column selections
            var chooseIrt2 = ShowDialog<ChooseIrtStandardPeptidesDlg>(transitionSelectdgl.OkDialog);
            OkDialog(chooseIrt2, () => chooseIrt2.OkDialogFile(irtCsvFile));
            doc = WaitForDocumentChange(doc);
            AssertEx.IsDocumentState(doc, null, 24, 294, 1170);
            CollectionUtil.ForEach(SkylineWindow.Document.PeptideGroups.Take(10), protein => Assert.IsTrue(protein.Name.StartsWith("AQRT_")));
            CheckAssayLibrarySettings();

            // Import assay library and choose CiRTs
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
                SkylineWindow.ResetDefaultSettings();
                Assert.IsTrue(SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("assay_import_cirt.sky")));
            });
            doc = SkylineWindow.Document;
            var errorList = new List<string>();
            ImportAssayLibrarySkipColumnSelect(TestFilesDir.GetTestPath("cirts.tsv"), errorList);
            var chooseIrt3 = WaitForOpenForm<ChooseIrtStandardPeptidesDlg>();
            var useCirtsDlg = ShowDialog<AddIrtStandardsDlg>(() => chooseIrt3.OkDialogStandard(IrtStandard.CIRT_SHORT));
            RunUI(() => useCirtsDlg.StandardCount = 12);
            OkDialog(useCirtsDlg, useCirtsDlg.OkDialog);
            doc = WaitForDocumentChange(doc);
            AssertEx.IsDocumentState(doc, null, 63, 120, 202, 1574);
            CheckAssayLibrarySettings();

            // Undo import
            RunUI(SkylineWindow.Undo);
            doc = WaitForDocumentChange(doc);

            // Import assay library and choose a standard
            var chooseStandard = IrtStandard.BIOGNOSYS_11;
            var overwriteDlg2 = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.ImportAssayLibrary(TestFilesDir.GetTestPath("cirts.tsv")));
            transitionSelectdgl = ShowDialog<ImportTransitionListColumnSelectDlg>(overwriteDlg2.BtnYesClick);  // Expect a confirmation of column selections
            var transitionErrs2 = ShowDialog<ImportTransitionListErrorDlg>(transitionSelectdgl.OkDialog); // Expect an error report
            RunUI(() => Assert.IsTrue(transitionErrs2.AcceptButton.DialogResult == DialogResult.OK));
            var chooseIrt4 = ShowDialog<ChooseIrtStandardPeptidesDlg>(transitionErrs2.AcceptButton.PerformClick);
            OkDialog(chooseIrt4, () => chooseIrt4.OkDialogStandard(chooseStandard));
            doc = WaitForDocumentChange(doc);
            // We should have an extra peptide group and extra peptides since the standard peptides should've been added to the document
            AssertEx.IsDocumentState(doc, null, 64, 120 + chooseStandard.Peptides.Count, null, null);
            var biognosysTargets = new TargetMap<bool>(chooseStandard.Peptides.Select(pep => new KeyValuePair<Target, bool>(pep.ModifiedTarget, true)));
            var standardGroup = doc.PeptideGroups.First();
            Assert.AreEqual(chooseStandard.Peptides.Count, standardGroup.PeptideCount);
            foreach (var nodePep in standardGroup.Peptides)
                Assert.IsTrue(biognosysTargets.ContainsKey(nodePep.ModifiedTarget));
            RunUI(() => SkylineWindow.SaveDocument());
        }

        // Expects a message dialog after import window closes
        public static void ImportTransitionListSkipColumnSelectWithMessage<TDlg>(string csvPath, Action<TDlg> messageAction)
            where TDlg : FormEx
        {
            ImportTransitionListSkipColumnSelect(csvPath);
            var messageDlg = WaitForOpenForm<TDlg>();
            OkDialog(messageDlg, () => messageAction(messageDlg));
        }

        // Expects a message dialog when user tries to close import window
        public static void ImportTransitionListSkipColumnSelectWithMessage(string csvPath, string expectedFirstMessage, int expectedMessageCount, bool proceedWithError)
        {
            var errors = new List<string>();
            ImportTransitionListSkipColumnSelect(csvPath, errors, proceedWithError);
            AssertEx.AreEqual(expectedMessageCount, errors.Count);
            if (expectedFirstMessage != null)
            {
                AssertEx.AreEqual(expectedFirstMessage, errors.First());
            }
        }

        // Expect paste to result in an error dialog when user tries to close it
        public static void PasteTransitionListSkipColumnSelectWithMessage(string expectedFirstMessage, int expectedMessageCount, bool proceedWithErrors)
        {

            var transitionSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(SkylineWindow.Paste);
            // We're expecting errors, collect them then move on
            bool errorsAccepted = false;
            RunDlg<ImportTransitionListErrorDlg>(transitionSelectDlg.OkDialog, errDlg =>
            {
                AssertEx.AreEqual(expectedMessageCount, errDlg.ErrorList.Count);
                AssertEx.AreEqual(expectedFirstMessage, errDlg.ErrorList.First().ErrorMessage);
                if (errDlg.AcceptButton.DialogResult == DialogResult.OK)
                    errorsAccepted = true;
                errDlg.AcceptButton.PerformClick();
            });
            if (errorsAccepted)
                WaitForClosedForm(transitionSelectDlg);
            else
                OkDialog(transitionSelectDlg, transitionSelectDlg.CancelDialog);
        }

        public static RCalcIrt ValidateDocAndIrt(int peptides, int irtTotal, int irtStandards)
        {
            TryWaitForConditionUI(6000, () => SkylineWindow.DocumentUI.PeptideCount == peptides); // Peptide count checked below
            RCalcIrt calculator = null;
            RunUI(() =>
            {
                var doc = SkylineWindow.DocumentUI;
                Assert.AreEqual(peptides, doc.PeptideCount);
                calculator = doc.Settings.PeptideSettings.Prediction.RetentionTime.Calculator as RCalcIrt;
                Assert.IsNotNull(calculator);
                var peptideSeqs = calculator.PeptideScores.Select(item => item.Key).ToList();
                Assert.AreEqual(irtTotal, peptideSeqs.Count);
                Assert.AreEqual(irtStandards, calculator.GetStandardPeptides(peptideSeqs).Count());
            });
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
            OkDialog(libraryDlg, libraryDlg.Btn1Click);
        }

        private static void CheckAssayLibrarySettings()
        {
            var doc = SkylineWindow.Document;

            var calc = (RCalcIrt) doc.Settings.PeptideSettings.Prediction.RetentionTime.Calculator;
            Assert.AreEqual(SkylineWindow.AssayLibraryName, calc.Name);
            Assert.AreEqual(SkylineWindow.AssayLibraryFileName, calc.DatabasePath);

            var lib = doc.Settings.PeptideSettings.Libraries.LibrarySpecs.FirstOrDefault(libSpec => libSpec.Name.Equals(SkylineWindow.AssayLibraryName));
            Assert.IsNotNull(lib);
            Assert.AreEqual(SkylineWindow.AssayLibraryFileName, lib.FilePath);

            foreach (var nodePepGroup in doc.PeptideGroups)
            {
                foreach (var nodePep in nodePepGroup.Peptides)
                {
                    Assert.IsNotNull(calc.ScoreSequence(nodePep.ModifiedTarget));
                    Assert.IsTrue(nodePep.HasLibInfo);
                }
            }
        }
    }
}
