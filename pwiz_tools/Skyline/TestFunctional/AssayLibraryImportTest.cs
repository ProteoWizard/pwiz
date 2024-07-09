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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
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
    public class AssayLibraryImportTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestAssayLibraryImport()
        {
            Preamble();
        }

        [TestMethod]
        public void TestAssayLibraryImportAsSmallMolecules()
        {
            if (SkipSmallMoleculeTestVersions())
            {
                return;
            }
            Preamble(true);
        }

        private void Preamble(bool asSmallMolecules = false)
        {
            _asSmallMolecules = asSmallMolecules;
            TestFilesZip = @"TestFunctional\AssayLibraryImportTest.zip";
            RunFunctionalTest();
        }

        private static bool _asSmallMolecules;
        private static bool _smallMolDemo; // Set true for a convenient interactive  demo of small mol UI

        private static void DemoPause(string message, Action action = null)
        {
            if (_smallMolDemo)
            {
                if (action != null)
                {
                    RunUI(action);
                }
                PauseForManualTutorialStep(message);
            }
        }

        protected override void DoTest()
        {
            _smallMolDemo = false; // _asSmallMolecules;
            if (_asSmallMolecules)
            {
                AsSmallMoleculeTestUtil.TranslateFilesToSmallMolecules(TestFilesDir.FullPath, false);
            }
            TestAssayImportGeneral();
            DemoPause("Done with assay library detection - now on to direct Assay Library Import");
            if (!_asSmallMolecules)
            {
                TestModificationMatcher();
            }
            if (!_smallMolDemo)
            {
                TestBlankDocScenario();
                TestEmbeddedIrts();
            }

            TestAssayImport2();
            if (_smallMolDemo)
            {
                DemoPause("done");
                return;
            }
            TestPaser();
            VerifyAuditLog();
        }

        protected void TestAssayImportGeneral()
        {
            var documentExisting = TestFilesDir.GetTestPath("AQUA4_Human_Existing_Calc.sky");
            var documentBlank = TestFilesDir.GetTestPath("AQUA4_Human_Blank.sky");
            string textConflict = TestFilesDir.GetTestPath("OpenSWATH_SM4_Overwrite.csv");
            string textIrt = TestFilesDir.GetTestPath("OpenSWATH_SM4_iRT.csv");
            string newDatabase = TestFilesDir.GetTestPath("irtNew.irtdb");
            var smDecorate = _asSmallMolecules ? RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator : string.Empty;
            string textNoError = TestFilesDir.GetTestPath("OpenSWATH_SM4_NoError.csv");
            RCalcIrt calculator;
            // 1. Import mass list with iRT's into document, then cancel
            LoadDocument(documentExisting);
            if (!_smallMolDemo) // Error checks, not interesting to watch
            {
            var docOld = SkylineWindow.Document;
            ImportTransitionListSkipColumnSelectWithMessage(textNoError,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_, 
                importIrtMessage => { importIrtMessage.BtnCancelClick(); });
            WaitForDocumentLoaded();
            // Document should be reference equal to what it was before
            Assert.AreSame(SkylineWindow.Document, docOld);

            // 2. Skip iRT's, then cancel on library import prompt, leading to no document change
            ImportTransitionListSkipColumnSelectWithMessage(textNoError, 
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_, 
                importIrtMessage => { importIrtMessage.Btn1Click(); });
            var libraryDlgCancel = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgCancel, libraryDlgCancel.BtnCancelClick);
            WaitForDocumentLoaded();
            Assert.AreSame(SkylineWindow.Document, docOld);

            // 3. Import transitions but decline to import iRT's
            ImportTransitionListSkipColumnSelectWithMessage(textNoError,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_,
                importIrtMessage => { importIrtMessage.Btn1Click(); });
            SkipLibraryDlg(); // Wait for dialog offering to add to library, click "Skip"
            WaitForDocumentLoaded();
            // Transitions have been imported, but not iRT
            ValidateDocAndIrt(294, 11, 10);

            // 4. Importing mass list with iRT's into document with existing iRT calculator, no conflicts
            LoadDocument(documentExisting);
            ImportTransitionListSkipColumnSelectWithMessage(textNoError,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_,
                importIrtMessage => { importIrtMessage.Btn0Click(); });
            SkipLibraryDlg();  // Wait for dialog offering to add to library, click "Skip"
            WaitForDocumentLoaded();
            ValidateDocAndIrt(294, 295, 10);

            // 5. Peptide iRT in document conflicts with peptide iRT in database, respond by canceling whole operation
            LoadDocument(documentExisting);
            var docOldImport = SkylineWindow.Document;
            ImportTransitionListSkipColumnSelectWithMessage(textConflict,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_,
                importIrt => importIrt.Btn0Click()); // Button0 is "Add"
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
            ImportTransitionListSkipColumnSelectWithMessage(textConflict,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_, 
                importIrt => importIrt.Btn0Click());
            var importIrtConflictOverwriteNo = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(importIrtConflictOverwriteNo.Message,
                                        TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ImportMassList_The_iRT_calculator_already_contains__0__of_the_imported_peptides_, 1),
                                                                Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_value_or_overwrite_with_the_imported_value_)));
            // Don't overwrite the iRT value
            OkDialog(importIrtConflictOverwriteNo, importIrtConflictOverwriteNo.Btn0Click);
            SkipLibraryDlg();  // Wait for dialog offering to add to library, click "Skip"
            WaitForDocumentLoaded();
            calculator = ValidateDocAndIrt(355, 355, 10);
            RunUI(() =>
            {
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => _asSmallMolecules? item.Key.Molecule.InvariantName:item.Key.Sequence).ToList();
                int conflictIndex = peptides.IndexOf(smDecorate+"YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 76.0, 0.1);
            });

            // 7. If mass list contains a peptide that is already an iRT standard, throw exception
            LoadDocument(documentExisting);
            var docOldStandard = SkylineWindow.Document;
            string textStandard = TestFilesDir.GetTestPath("OpenSWATH_SM4_StandardsConflict.csv");
            ImportTransitionListSkipColumnSelectWithMessage(textStandard,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_, 
                importIrt => importIrt.Btn0Click());
            var importIrtConflictOverwriteConflict = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(importIrtConflictOverwriteConflict, importIrtConflictOverwriteConflict.Btn1Click);
            var messageDlgStandard = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(messageDlgStandard.Message,
                string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__,
                              textStandard,
                              string.Format(Resources.SkylineWindow_AddIrtPeptides_Imported_peptide__0__with_iRT_library_value_is_already_being_used_as_an_iRT_standard_,
                                  smDecorate + "GTFIIDPGGVIR"))));
            OkDialog(messageDlgStandard, messageDlgStandard.OkDialog);
            WaitForDocumentLoaded();
            Assert.AreSame(docOldStandard, SkylineWindow.Document);

            // 8. Mass list contains different iRT times on same peptide
            LoadDocument(documentExisting);
            var docOldIrt = SkylineWindow.Document;
            string textIrtConflict = TestFilesDir.GetTestPath("OpenSWATH_SM4_InconsistentIrt.csv");
            ImportTransitionListSkipColumnSelectWithMessage(textIrtConflict, 
                string.Format(Resources.PeptideGroupBuilder_FinalizeTransitionGroups_Two_transitions_of_the_same_precursor___0___m_z__1_____have_different_iRT_values___2__and__3___iRT_values_must_be_assigned_consistently_in_an_imported_transition_list_,
                    smDecorate + "YVPIHTIDDGYSVIK", _asSmallMolecules ? 860.4512 : 864.458, 49.8, 50.2), 1, false);
            WaitForDocumentLoaded();
            Assert.AreSame(docOldIrt, SkylineWindow.Document);

            // 8.1 Mass list contains different iRT values on two non-contiguous lines of the same transition group
            // In peptide version, though, what actually gets complained about is that m/z values don't make sense
            // In small mol version we don't declare m/z, so it gets a chance to notice the inconsistent iRTs
            string textIrtGroupConflict = TestFilesDir.GetTestPath("InterleavedInconsistentIrt.csv");
            var inconsistentIRTsCount = 2;
            ImportTransitionListSkipColumnSelectWithMessage(textIrtGroupConflict, null, _asSmallMolecules ? inconsistentIRTsCount : 59, false);
            WaitForDocumentLoaded();
            Assert.AreSame(docOldIrt, SkylineWindow.Document);
            // Now remove the modified column which is bogus and causing errors
            RemoveColumn(textIrtGroupConflict, 22);
            ImportTransitionListSkipColumnSelectWithMessage(textIrtGroupConflict,
                string.Format(Resources.PeptideGroupBuilder_FinalizeTransitionGroups_Two_transitions_of_the_same_precursor___0___m_z__1_____have_different_iRT_values___2__and__3___iRT_values_must_be_assigned_consistently_in_an_imported_transition_list_,
                    smDecorate + "AAAAAAAAAAAAAAAGAAGK", _asSmallMolecules ? 490.6015 : 492.9385, 53, 54), inconsistentIRTsCount, false);
            WaitForDocumentLoaded();
            Assert.AreSame(docOldIrt, SkylineWindow.Document);

            // 8.2 Try again, this time click OK on the error dialog, accepting all transitions except the 2 with errors. Also, deal with ion mobility.
            string textIrtGroupConflictAccept = TestFilesDir.GetTestPath("InterleavedInconsistentIrtWithIonMobility.csv"); // Same as before but also has column of fake 1/K0 values as 1+(precursorMZ/2)
            ImportTransitionListSkipColumnSelectWithMessage(textIrtGroupConflictAccept,
                string.Format(Resources.PeptideGroupBuilder_FinalizeTransitionGroups_Two_transitions_of_the_same_precursor___0___m_z__1_____have_different_iRT_values___2__and__3___iRT_values_must_be_assigned_consistently_in_an_imported_transition_list_,
                    smDecorate + "AAAAAAAAAAAAAAAGAAGK", _asSmallMolecules ? 490.6015 : 492.9385, 53, 54), inconsistentIRTsCount, true);
            var confirmIrtDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(confirmIrtDlg, confirmIrtDlg.Btn0Click);
            var libraryAcceptDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryAcceptDlg, libraryAcceptDlg.Btn0Click);
            ValidateDocAndIrt(16, 361, 10);
            RunUI(() =>
            {
                var docCurrent = SkylineWindow.DocumentUI;
                // All of the transitions are there except for the ones with errors (mismatched iRT values on two fragments)
                Assert.AreEqual(109, docCurrent.MoleculeTransitionCount);
                Assert.AreEqual(22, docCurrent.MoleculeTransitionGroupCount);
                // Spectral library results are there
                var currentLibraries = docCurrent.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(currentLibraries.HasLibraries);
                Assert.IsTrue(currentLibraries.IsLoaded);
                Assert.AreEqual(1, currentLibraries.Libraries.Count);
                Assert.AreEqual(1, currentLibraries.LibrarySpecs.Count);
                var aqua4HumanExistingCalcAssay = 
                    $@"AQUA4_Human_Existing_Calc{(_asSmallMolecules ? AsSmallMoleculeTestUtil.SMALL_MOL_CONVERSION_TAG : string.Empty)}-assay" ;
                Assert.AreEqual(aqua4HumanExistingCalcAssay, currentLibraries.LibrarySpecs[0].Name);
                var currentLibrary = currentLibraries.Libraries[0];
                Assert.AreEqual(12, currentLibrary.SpectrumCount);
                // The data has fake ion mobility values set as 1+(mz/2), this should appear in library
                foreach(var key in currentLibrary.Keys)
                {
                    var spec = currentLibrary.GetSpectra(key, IsotopeLabelType.light, LibraryRedundancy.all).First();
                    AssertEx.IsTrue(spec.IonMobilityInfo.IonMobility.Mobility.HasValue);
                    AssertEx.AreEqual(eIonMobilityUnits.inverse_K0_Vsec_per_cm2, spec.IonMobilityInfo.IonMobility.Units);
                }
            });

            // 9. iRT not a number leads to error
            LoadDocument(documentExisting);
            var docNanIrt = SkylineWindow.Document;
            var textIrtNan = AsSmallMoleculeTestUtil.AdjustTransitionListForTestMode("PrecursorMz\tProductMz\tTr_recalibrated\tLibraryIntensity\tdecoy\tPeptideSequence\tProteinName\n728.88\t924.539\tBAD_IRT\t3305.3\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n", 2, _asSmallMolecules);
            RunUI(() => ClipboardEx.SetText(textIrtNan));
            var mzExpected = _asSmallMolecules ? 723.8753698625 : 728.88;
            PasteTransitionListSkipColumnSelectWithMessage(string.Format(
                Resources.MassListImporter_AddRow_Invalid_iRT_value_at_precusor_m_z__0__for_peptide__1_,
                mzExpected,
                smDecorate + "ADSTGTLVITDPTR"), 1, false);
            WaitForDocumentLoaded();
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 10. iRT blank leads to error
            var textIrtBlank = AsSmallMoleculeTestUtil.AdjustTransitionListForTestMode("PrecursorMz\tProductMz\tiRT\tLibraryIntensity\tdecoy\tPeptideSequence\tProteinName\n728.88\t924.539\t\t3305.3\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n", 2, _asSmallMolecules);
            RunUI(() => ClipboardEx.SetText(textIrtBlank));
            PasteTransitionListSkipColumnSelectWithMessage(string.Format(Resources.MassListImporter_AddRow_Invalid_iRT_value_at_precusor_m_z__0__for_peptide__1_,
                                                        mzExpected,
                                                        smDecorate + "ADSTGTLVITDPTR"), 1, false);
            WaitForDocumentLoaded();
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 11. Library not a number leads to error
            var textLibraryNan = AsSmallMoleculeTestUtil.AdjustTransitionListForTestMode("PrecursorMz\tProductMz\tiRT\tLibraryIntensity\tdecoy\tPeptideSequence\tProteinName\n728.88\t924.539\t30.5\tBAD_LIBRARY\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n", 2, _asSmallMolecules);
            RunUI(() => ClipboardEx.SetText(textLibraryNan));
            PasteTransitionListSkipColumnSelectWithMessage(string.Format(Resources.MassListImporter_AddRow_Invalid_library_intensity_at_precursor__0__for_peptide__1_,
                                                        mzExpected,
                                                        smDecorate + "ADSTGTLVITDPTR"), 1, false);
            WaitForDocumentLoaded();
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 12. Library blank leads to error
            var textLibraryBlank = AsSmallMoleculeTestUtil.AdjustTransitionListForTestMode("PrecursorMz\tProductMz\tTr_recalibrated\tRelaTive_IntEnsity\tdecoy\tPeptideSequence\tProteinName\n728.88\t924.539\t30.5\t\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n", 2, _asSmallMolecules);
            RunUI(() => ClipboardEx.SetText(textLibraryBlank));
            PasteTransitionListSkipColumnSelectWithMessage(string.Format(Resources.MassListImporter_AddRow_Invalid_library_intensity_at_precursor__0__for_peptide__1_,
                                                        mzExpected,
                                                        smDecorate + "ADSTGTLVITDPTR"), 1, false);
            WaitForDocumentLoaded();
            Assert.AreSame(docNanIrt, SkylineWindow.Document);

            // 13. Title column missing causes iRT's and library to be skipped
            ForgetPreviousImportColumnsSelection(); // For purposes of this test we don't want to use the preciously confirmed input columns
            if (!_asSmallMolecules) // We don't make as many guesses with headerless small mol lists
            {
                const string textTitleMissing = "728.88\t924.539\t\t3305.3\t0\tADSTGTLVITDPTR\tAQUA4SWATH_HMLangeA\n";
                RunUI(() => ClipboardEx.SetText(textTitleMissing));

                using (new WaitDocumentChange(null, true))
                {
                    PasteTransitionListSkipColumnSelect();
                }

                // Transition gets added but not iRT
                ValidateDocAndIrt(11, 361, 10);
            }

            // 14. Same as 5 but this time do overwrite the iRT value
            LoadDocument(documentExisting);
            ImportTransitionListSkipColumnSelectWithMessage(textConflict,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_,
                importIrt => importIrt.Btn0Click());
            var importIrtConflictOverwriteYes = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(importIrtConflictOverwriteYes.Message,
                                        TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ImportMassList_The_iRT_calculator_already_contains__0__of_the_imported_peptides_, 1),
                                                                Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_value_or_overwrite_with_the_imported_value_)));
            OkDialog(importIrtConflictOverwriteYes, importIrtConflictOverwriteYes.Btn1Click);
            SkipLibraryDlg(); // Wait for dialog offering to add to library, click "Skip"
            WaitForDocumentLoaded();
            calculator = ValidateDocAndIrt(355, 361, 10);
            RunUI(() =>
            {
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key.InvariantName).ToList();
                int conflictIndex = peptides.IndexOf(smDecorate + "YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 49.8, 0.1);
            });

            // 15. Repeat 11, this time no dialog box should show up at all, and the iRT calculator should be unchanged
            var docLoaded = LoadDocument(documentExisting);
            var calculatorOld = docLoaded.Settings.PeptideSettings.Prediction.RetentionTime.Calculator;
            ImportTransitionListSkipColumnSelectWithMessage(textConflict,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_spectral_library_intensities___Create_a_document_library_from_these_intensities_, 
                libraryDlg => libraryDlg.Btn1Click());
            var docMassList = WaitForDocumentChangeLoaded(docLoaded);
            calculator = ValidateDocAndIrt(355, 361, 10);
            RunUI(() => 
            {
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key.InvariantName).ToList();
                int conflictIndex = peptides.IndexOf(smDecorate + "YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 49.8, 0.1);
            });
            Assert.AreSame(calculatorOld, docMassList.Settings.PeptideSettings.Prediction.RetentionTime.Calculator);

            // Test on-the-fly creation of iRT calculator as part of mass list import

            // 16. Attempt to create iRT calculator, then cancel, leaves the document the same
            var docCreateIrtCancel = LoadDocument(documentBlank);
            docCreateIrtCancel = AllowAllIonTypes();
            ImportTransitionListSkipColumnSelectWithMessage(textConflict,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_,
                importIrt => { importIrt.Btn0Click(); });
            var createIrtCalc = WaitForOpenForm<CreateIrtCalculatorDlg>();
            OkDialog(createIrtCalc, createIrtCalc.CancelDialog);
            var docCreateIrtError = WaitForDocumentLoaded();
            // Document hasn't changed
            Assert.AreSame(docCreateIrtCancel, docCreateIrtError);

            // 17. Missing name in CreateIrtCalculatorDlg shows error message
            ImportTransitionListSkipColumnSelectWithMessage(textConflict, 
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_,
                importIrt => importIrt.Btn0Click());
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

            }

            // 25. Successful import and successful creation of iRT database and library
            // Document starts empty with no transitions and no iRT calculator
            // 355 transitions, libraries, and iRT times are imported, including libraries for the iRT times
            var docCalcGood = LoadDocument(documentBlank);
            Assert.AreEqual(0, docCalcGood.PeptideTransitions.Count());
            DemoPause("blank document loaded, import transition list with iRTs " + textConflict);
            AllowAllIonTypes();
            ImportTransitionListSkipColumnSelectWithMessage(textConflict,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_,
                importIrt => importIrt.Btn0Click(), _smallMolDemo);
            var createIrtCalcGood = WaitForOpenForm<CreateIrtCalculatorDlg>();
            RunUI(() =>
            {
                createIrtCalcGood.IrtImportType = CreateIrtCalculatorDlg.IrtType.separate_list;
                createIrtCalcGood.CalculatorName = "test1";
                createIrtCalcGood.TextFilename = textIrt;
                createIrtCalcGood.NewDatabaseName = newDatabase;
            });
            DemoPause("iRT calculator creation, we will use a lightly edited originally peptide-oriented transition list");
            if (_asSmallMolecules)
            {
                // Expect Skyline to not recognize column headers, and to offer user the chance to set headers
                var importTransitionListColumnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(createIrtCalcGood.OkDialog);
                DemoPause("Skyline does not recognize some column headers in the transition list, and offers the user the chance to set headers. In this test, the user is about to cancel out of the column picker.");

                // Verify that cancel of column picker does not cancel the import
                CancelDialog(importTransitionListColumnSelectDlg, importTransitionListColumnSelectDlg.CancelDialog);
                DemoPause("As expected, cancelling the column picker returns the user to the Create iRT window. Next the user clicks OK and returns to the column picker");
                // Proceed
                importTransitionListColumnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(createIrtCalcGood.OkDialog);
                DemoPause("Now the user will choose OK instead of Cancel and the import can proceed");
                OkDialog(importTransitionListColumnSelectDlg, importTransitionListColumnSelectDlg.OkDialog);
            }
            else
            {
                OkDialog(createIrtCalcGood, createIrtCalcGood.OkDialog);
            }

            var libraryDlgAll = WaitForOpenForm<MultiButtonMsgDlg>();
            // Make small change to document to test robustness to concurrent document change
            RunUI(() => SkylineWindow.ModifyDocument("test change", doc =>
            {
                var settingsNew = doc.Settings.ChangeTransitionFilter(filter => filter.ChangePeptideProductCharges(Adduct.ProtonatedFromCharges(1, 2, 3)));
                doc = doc.ChangeSettings(settingsNew);
                return doc;
            }));
            DemoPause("Skyline notices that this might actually be an assay library");
            OkDialog(libraryDlgAll, libraryDlgAll.Btn0Click);
            WaitForDocumentLoaded();
            ValidateDocAndIrt(355, 355, 10);
            var AQUA4_Human_Blank_Assay = $@"AQUA4_Human_Blank{(_asSmallMolecules ? AsSmallMoleculeTestUtil.SMALL_MOL_CONVERSION_TAG : string.Empty)}-assay";
            RunUI(() =>
            {
                var libraries = SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(libraries.HasLibraries);
                Assert.IsTrue(libraries.IsLoaded);
                Assert.AreEqual(1, libraries.Libraries.Count);
                Assert.AreEqual(1, libraries.LibrarySpecs.Count);
                Assert.AreEqual(AQUA4_Human_Blank_Assay, libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(355, library.SpectrumCount);
            });
            bool foundLibraryCheck = false;
            foreach (var groupNode in SkylineWindow.Document.MoleculeTransitionGroups)
            {
                Assert.IsTrue(groupNode.HasLibInfo);
                Assert.IsTrue(groupNode.HasLibRanks);
                foreach (var transition in groupNode.Transitions)
                {
                    // Every transition excpet a and z transitions (which are filtered out in the transition settings) has library info
                    if (Equals(transition.Transition.IonType, IonType.a) || Equals(transition.Transition.IonType, IonType.z))
                        continue;
                    Assert.IsTrue(transition.HasLibInfo);
                    if (String.Equals(groupNode.TransitionGroup.Peptide.Target.InvariantName, smDecorate+"DAVNDITAK") && String.Equals(transition.Transition.FragmentIonName, "y7"))
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
            var documentPeptides = SkylineWindow.Document.Molecules.Select(pep => pep.ModifiedTarget).ToList();
            var calc = SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator as RCalcIrt;
            Assert.IsNotNull(calc);
            var irtPeptides = calc.PeptideScores.Select(kvp => kvp.Key).ToList();
            Assert.AreEqual(documentPeptides.Count, irtPeptides.Count);
            Assert.AreEqual(documentPeptides.Count, documentPeptides.Intersect(irtPeptides).Count());
            if (_smallMolDemo) 
                return;
            // 26. Successful import and succesful load of existing database, with keeping of iRT's, plus successful library import
            var irtOriginal = TestFilesDir.GetTestPath("irtOriginal.irtdb");
            if (_asSmallMolecules)
            {
                irtOriginal = RCalcIrt.PersistAsSmallMolecules(irtOriginal);
            }
            var docReload = LoadDocument(documentBlank);
            Assert.IsNull(docReload.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.AreEqual(0, SkylineWindow.Document.MoleculeTransitionCount);
            ImportTransitionListSkipColumnSelectWithMessage(textConflict,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_,
                importIrt => importIrt.Btn0Click());
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
            WaitForDocumentChangeLoaded(docReload);
            calculator = ValidateDocAndIrt(345, 355, 10);
            RunUI(() =>
            {
                var scores = calculator.PeptideScores.ToList();
                var peptides = scores.Select(item => item.Key.InvariantName).ToList();
                int conflictIndex = peptides.IndexOf(smDecorate + "YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 76.0, 0.1);
                var libraries = SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(libraries.HasLibraries);
                Assert.IsTrue(libraries.IsLoaded);
                Assert.AreEqual(1, libraries.Libraries.Count);
                Assert.AreEqual(1, libraries.LibrarySpecs.Count);
                Assert.AreEqual(AQUA4_Human_Blank_Assay, libraries.LibrarySpecs[0].Name);
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
            foreach (var groupNode in SkylineWindow.Document.MoleculeTransitionGroups)
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
            Assert.AreEqual(0, SkylineWindow.Document.MoleculeTransitions.Count());
            ImportTransitionListSkipColumnSelectWithMessage(textConflict,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_,
                importIrt => importIrt.Btn0Click());
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
            string libraryName = Path.GetFileNameWithoutExtension(documentBlank) + (_asSmallMolecules ? AsSmallMoleculeTestUtil.SMALL_MOL_CONVERSION_TAG : string.Empty) + BiblioSpecLiteSpec.ASSAY_NAME;
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
                var peptides = scores.Select(item => item.Key.InvariantName).ToList();
                int conflictIndex = peptides.IndexOf(smDecorate+"YVPIHTIDDGYSVIK");
                Assert.AreNotEqual(-1, conflictIndex);
                double conflictIrt = scores[conflictIndex].Value;
                Assert.AreEqual(conflictIrt, 49.8, 0.1);
                var libraries = SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(libraries.HasLibraries);
                Assert.IsTrue(libraries.IsLoaded);
                Assert.AreEqual(1, libraries.Libraries.Count);
                Assert.AreEqual(1, libraries.LibrarySpecs.Count);
                Assert.AreEqual(AQUA4_Human_Blank_Assay, libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(345, library.SpectrumCount);
            });
            foreach (var groupNode in SkylineWindow.Document.MoleculeTransitionGroups)
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
            ImportTransitionListSkipColumnSelectWithMessage(textConflict,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_spectral_library_intensities___Create_a_document_library_from_these_intensities_,
                libraryDlgRepeat => libraryDlgRepeat.Btn0Click());
            WaitForDocumentLoaded();
            ValidateDocAndIrt(_asSmallMolecules ? 345 : 690 , 355, 10); // N.B. small mol transition list reader recognizes repeated transitions and ignores them
            RunUI(() =>
            {
                var libraries = SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(libraries.HasLibraries);
                Assert.IsTrue(libraries.IsLoaded);
                Assert.AreEqual(1, libraries.Libraries.Count);
                Assert.AreEqual(1, libraries.LibrarySpecs.Count);
                Assert.AreEqual(AQUA4_Human_Blank_Assay, libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(345, library.SpectrumCount);
            });


            // 29. Start with blank document, skip iRT's entirely but import library intensities, show this works out fine
            var docLibraryOnly = LoadDocument(documentBlank);
            Assert.IsNull(docLibraryOnly.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.AreEqual(0, SkylineWindow.Document.MoleculeTransitions.Count());
            ImportTransitionListSkipColumnSelectWithMessage(textNoError,
Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_, 
importIrt => importIrt.Btn1Click());
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
                Assert.AreEqual(AQUA4_Human_Blank_Assay, libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(284, library.SpectrumCount);
            });

            // 30. Repeat import with a larger library, show that there are no duplicates and it gets replaced cleanly
            ImportTransitionListSkipColumnSelectWithMessage(textConflict,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_, 
                importIrt => importIrt.Btn1Click());
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
                Assert.AreEqual(AQUA4_Human_Blank_Assay, libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(345, library.SpectrumCount);
            });


            // 31. Start with blank document, skip iRT's, cancel on library overwrite, document should be the same
            var docCancel = LoadDocument(documentBlank);
            ImportTransitionListSkipColumnSelectWithMessage(textConflict,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_,
                importIrt => importIrt.Btn1Click());
            var libraryDlgCancelNew = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgCancelNew, libraryDlgCancelNew.Btn0Click);
            var libraryDlgOverwriteCancel = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlgOverwriteCancel, libraryDlgOverwriteCancel.BtnCancelClick);
            WaitForDocumentLoaded();
            Assert.AreSame(SkylineWindow.Document, docCancel);

            // 32. Start with blank document, skip iRT's, decline to overwrite, only document should have changed
            var docInitial31 = LoadDocument(documentBlank);
            ImportTransitionListSkipColumnSelectWithMessage(textConflict,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_,
                importIrt => importIrt.Btn1Click());
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
                Assert.AreEqual(345, docComplete31.MoleculeCount);
            });

            // 33. Try a different document with interleaved heavy and light transitions of the same peptide
            // Tests mixing of transition groups within a peptide
            var documentInterleaved = TestFilesDir.GetTestPath("Consensus_Dario_iRT_new_blank.sky");
            var textInterleaved = TestFilesDir.GetTestPath("Interleaved.csv");
            RemoveColumn(textInterleaved, 22);
            RunUI(() => SkylineWindow.OpenFile(documentInterleaved));
            ImportTransitionListSkipColumnSelectWithMessage(textInterleaved,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_,
                addIrtDlg => addIrtDlg.Btn0Click());
            var libraryInterleaved = WaitForOpenForm<MultiButtonMsgDlg>();
            // Change to document to test handling of concurrent change during mass list import
            RunUI(() => SkylineWindow.ModifyDocument("test change", doc =>
            {
                var settingsNew = doc.Settings.ChangeTransitionFilter(filter => filter.ChangePeptideProductCharges(Adduct.ProtonatedFromCharges(1, 2, 3 )));
                doc = doc.ChangeSettings(settingsNew);
                return doc;
            }));
            OkDialog(libraryInterleaved, libraryInterleaved.Btn0Click);
            TryWaitForConditionUI(6000, () => SkylineWindow.DocumentUI.MoleculeCount == 6);  // Peptide count checked below
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                var docInterleaved = SkylineWindow.DocumentUI;
                Assert.AreEqual(6, docInterleaved.MoleculeCount);
                Assert.AreEqual(60, docInterleaved.MoleculeTransitionCount);
                var libraries = docInterleaved.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(libraries.HasLibraries);
                Assert.IsTrue(libraries.IsLoaded);
                Assert.AreEqual(1, libraries.Libraries.Count);
                Assert.AreEqual(1, libraries.LibrarySpecs.Count);
                Assert.AreEqual("Consensus_Dario_iRT_new_blank-assay", libraries.LibrarySpecs[0].Name);
                var library = libraries.Libraries[0];
                Assert.AreEqual(12, library.SpectrumCount);
                foreach (var groupNode in docInterleaved.MoleculeTransitionGroups)
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
            ImportTransitionListSkipColumnSelectWithMessage(textInterleavedIrt,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_,
                addIrtDlg => addIrtDlg.Btn0Click());
            var irtOverwrite = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(irtOverwrite, irtOverwrite.Btn1Click);
            var libraryInterleavedIrt = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryInterleavedIrt, libraryInterleavedIrt.Btn0Click);
            var libraryDlgOverwriteIrt = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(libraryDlgOverwriteIrt.Btn0Click);
            TryWaitForConditionUI(6000, () => SkylineWindow.DocumentUI.MoleculeCount == 6);
            WaitForDocumentLoaded();
            Assert.AreEqual(6, SkylineWindow.Document.MoleculeCount);
            var target = new Target(@"AAAAAAAAAAAAAAAGAAGK");
            if (_asSmallMolecules)
            {
                target = new Target(RefinementSettings.MoleculeFromPeptideSequence(target.Sequence));
            }
            var irtValue = SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator.ScoreSequence(target);
            Assert.IsNotNull(irtValue);
            Assert.AreEqual(irtValue.Value, 52.407, 1e-3);

            // Make sure all the iRT magic works even when no spectral library info is present
            // (regression test in response to crash when libraries not present)
            var textInterleavedIrtNoLib = TestFilesDir.GetTestPath("InterleavedDiffIrt.csv");
            RunUI(() => SkylineWindow.OpenFile(documentInterleavedIrt));
            ImportTransitionListSkipColumnSelectWithMessage(textInterleavedIrtNoLib,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_spectral_library_intensities___Create_a_document_library_from_these_intensities_,
                addIrtDlg => addIrtDlg.Btn0Click());
            var irtOverwriteNoLib = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(irtOverwriteNoLib, irtOverwriteNoLib.Btn1Click);
            TryWaitForConditionUI(6000, () => SkylineWindow.DocumentUI.MoleculeCount == 6);
            WaitForDocumentLoaded();
            Assert.AreEqual(6, SkylineWindow.Document.MoleculeCount);
            var irtValueNoLib = SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator.ScoreSequence(target);
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
            WaitForCondition(() => rtRegression.ClearEquations().Equals(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime));
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
                    ImportTransitionListSkipColumnSelectWithMessage(textToughCase,
                        Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_,
                        importIrt => importIrt.Btn1Click());
                    SkipLibraryDlg(); // Wait for dialog offering to add to library, click "Skip"
                }
                WaitForDocumentLoaded();
                RunUI(() =>
                {
                    var peptides = SkylineWindow.DocumentUI.Molecules.ToList();
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
            Assert.IsFalse(Skyline.SkylineWindow.ShouldPromptForDecoys(SkylineWindow.Document));
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
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
            ImportTransitionListSkipColumnSelectWithMessage(pathChooseSeqCol,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_,
                addIrtDlg => addIrtDlg.Btn1Click());
            var addLibraryDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => addLibraryDlg.Btn1Click());
            RunUI(() => SkylineWindow.SaveDocument());
            // Reload to test that appropriate modifications have actually been added to the document
            LoadDocument(skyChooseSeqCol);
        }

        private static void PasteOnePeptide(string textModifiedSeqExpected)
        {
            PasteTransitionListSkipColumnSelect();
            TryWaitForCondition(3000, () => SkylineWindow.Document.MoleculeCount == 1);
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeCount);
            var peptideNode = SkylineWindow.Document.Molecules.First();
            Assert.AreEqual(textModifiedSeqExpected, peptideNode.ModifiedSequence);
        }

        protected void TestBlankDocScenario()
        {
            RunUI(() => SkylineWindow.NewDocument(true));
            var docOld = SkylineWindow.Document;
            string textNoError = TestFilesDir.GetTestPath("Interleaved.csv");
            var expectedErrorCount = _asSmallMolecules ? 0 : 30; // Modifications issues don't apply to small molecule test
            ImportTransitionListSkipColumnSelectWithMessage(textNoError, null, expectedErrorCount, true);
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
            ImportTransitionListSkipColumnSelectWithMessage(textEmbedded,
                Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_,
                importIrt => importIrt.Btn0Click());
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
            SkipLibraryDlg(); // Wait for dialog offering to add to library, click "Skip"
            WaitForDocumentLoaded();
            ValidateDocAndIrt(294, 294, 10);
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void TestAssayImport2()
        {
            RunUI(() => SkylineWindow.NewDocument(true));

            var csvFile = TestFilesDir.GetTestPath("OpenSWATH_SM4_NoError.csv");
            var saveDlg = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.ImportAssayLibrary(csvFile));
            DemoPause("user will cancel");
            OkDialog(saveDlg, saveDlg.BtnCancelClick);
            var doc = SkylineWindow.Document;

            if (!_asSmallMolecules)
            {
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
            }

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
            DemoPause("choose a molecule list", () => chooseIrt.SetDialogProtein(irtProteinName));
            OkDialog(chooseIrt, () => chooseIrt.OkDialogProtein(irtProteinName));
            doc = WaitForDocumentChange(doc);
            AssertEx.IsDocumentState(doc, null, 14, 284, 1119);
            Assert.AreEqual(irtProteinName, doc.MoleculeGroups.First().Name);
            CheckAssayLibrarySettings();

            // Undo import
            DemoPause("and undo it, then go on to import an assay library");
            RunUI(SkylineWindow.Undo);
            WaitForDocumentChange(doc);

            // Import assay library and choose a file
            doc = AllowAllIonTypes();
            var irtCsvFile = TestFilesDir.GetTestPath("OpenSWATH_SM4_iRT.csv");
            var overwriteDlg = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.ImportAssayLibrary(csvFile)); // Expect to be asked about library overwrite
            var transitionSelectdgl = ShowDialog<ImportTransitionListColumnSelectDlg>(overwriteDlg.BtnYesClick); // Expect a confirmation of column selections
            DemoPause("confirm columns");
            var chooseIrt2 = ShowDialog<ChooseIrtStandardPeptidesDlg>(transitionSelectdgl.OkDialog);
            DemoPause("Irt standards select");
            if (_asSmallMolecules)
            {
                // Expect a chance to verify columns
                DemoPause("Choose an iRT transition list", () => chooseIrt2.SetDialogFile(irtCsvFile));
                var columnsDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => chooseIrt2.OkDialogFile(irtCsvFile));
                DemoPause("Confirm columns");
                OkDialog(columnsDlg, columnsDlg.OkDialog);
            }
            else
            {
                OkDialog(chooseIrt2, () => chooseIrt2.OkDialogFile(irtCsvFile));
            }
            doc = WaitForDocumentChange(doc);
            AssertEx.IsDocumentState(doc, null, 24, 294, 1170);
            CollectionUtil.ForEach(SkylineWindow.Document.MoleculeGroups.Take(10), protein => Assert.IsTrue(protein.Name.StartsWith("AQRT_")));
            CheckAssayLibrarySettings();

            DemoPause("Next, Import assay library and choose CiRTs");
            // Import assay library and choose CiRTs
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
                SkylineWindow.ResetDefaultSettings();

                // Prepare the document to rank all of the imported transitions                
                SkylineWindow.ModifyDocument("Change transition filter", docMod =>
                    docMod.ChangeSettings(docMod.Settings
                        .ChangeTransitionInstrument(ti => ti.ChangeMaxMz(1800))
                        .ChangeTransitionFilter(tf =>
                            tf.ChangePeptidePrecursorCharges(new[] { Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED })
                                .ChangePeptideProductCharges(new[] { Adduct.SINGLY_PROTONATED, Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED })
                                .ChangePeptideIonTypes(new[] { IonType.y, IonType.b, IonType.precursor }))));

                Assert.IsTrue(SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("assay_import_cirt.sky")));
            });
            string cirtsPath = TestFilesDir.GetTestPath("cirts.tsv");
            string cirtsMixedPath = TestFilesDir.GetTestPath("cirts-mixed.tsv");
            // Randomizing the order should not impact the resulting document
            var cirtLines = File.ReadAllLines(cirtsPath);
            var cirtMixedLines = new List<string>();
            cirtMixedLines.Add(cirtLines.First());
            cirtMixedLines.AddRange(cirtLines.Skip(1).ToArray().RandomOrder(ArrayUtil.RANDOM_SEED));
            File.WriteAllLines(cirtsMixedPath, cirtMixedLines);

            if (!_asSmallMolecules) // Modifications stuff
            {
                var errorList = new List<string>();
                ImportAssayLibrarySkipColumnSelect(cirtsPath, errorList, false);
                Assert.AreEqual(503, errorList.Count); // Peptides complain about nonsense fragments
                RunUI(() =>
                {
                    // Add neutral loss modifications
                    SkylineWindow.ModifyDocument("Add neutral loss mods", docMod =>
                        docMod.ChangeSettings(docMod.Settings.ChangePeptideModifications(m =>
                            m.ChangeModifications(IsotopeLabelType.light, new[]
                            {
                                m.GetModifications(IsotopeLabelType.light),
                                new [] { UniMod.GetModification("Water Loss (D, E, S, T)", true),
                                    UniMod.GetModification("Ammonia Loss (K, N, Q, R)", true) }
                            }.SelectMany(l => l).ToArray()))));

                    Assert.IsTrue(SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("assay_import_cirt.sky")));
                });
                ImportAssayLibrarySkipColumnSelect(cirtsMixedPath); // Expecting no errors now

                var chooseIrt3 = WaitForOpenForm<ChooseIrtStandardPeptidesDlg>();
                var useCirtsDlg = ShowDialog<AddIrtStandardsDlg>(() => chooseIrt3.OkDialogStandard(IrtStandard.CIRT_SHORT));
                RunUI(() => useCirtsDlg.StandardCount = 12);
                doc = SkylineWindow.Document;
                OkDialog(useCirtsDlg, useCirtsDlg.OkDialog);
                doc = WaitForDocumentChangeLoaded(doc);
                AssertEx.IsDocumentState(doc, null, 63, 113, 204, cirtMixedLines.Count - 6 /* why? */ - 1 /* header line */);
                // This assay library has rows that are out of order and need to be merged, so check that all
                // of the resulting transitions have library info
                var calc = doc.Settings.PeptideSettings.Prediction.NonNullRetentionTime.Calculator as RCalcIrt;
                Assert.IsNotNull(calc);
                Assert.AreEqual(doc.MoleculeCount, calc.PeptideScores.Count());
                AssertEx.IsTrue(doc.MoleculeTransitions.All(t => t.HasLibInfo),
                    string.Format("Found {0} transitions without lib info", doc.MoleculeTransitions.Count(t => !t.HasLibInfo)));
                CheckAssayLibrarySettings();

                // Undo import
                RunUI(() => SkylineWindow.UndoRestore(1));  // Before the losses were added
                doc = WaitForDocumentChange(doc);
            }

            // Import assay library and choose a standard
            var chooseStandard = IrtStandard.BIOGNOSYS_11;
            if (_asSmallMolecules)
            {
                doc = SkylineWindow.Document;
                var columnSelectDlg2 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.ImportAssayLibrary(cirtsPath));  // Expect a confirmation of column selections
                DemoPause("confirm columns");
                var chooseIrt4 = ShowDialog<ChooseIrtStandardPeptidesDlg>(columnSelectDlg2.OkDialog);
                DemoPause("choose standards", () => chooseIrt4.SetDialogStandard(chooseStandard));
                OkDialog(chooseIrt4, () => chooseIrt4.OkDialogStandard(chooseStandard));
            }
            else
            {
                var overwriteDlg2 = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.ImportAssayLibrary(cirtsPath));
                var columnSelectDlg2 = ShowDialog<ImportTransitionListColumnSelectDlg>(overwriteDlg2.BtnYesClick);  // Expect a confirmation of column selections
                var transitionErrs2 = ShowDialog<ImportTransitionListErrorDlg>(columnSelectDlg2.OkDialog); // Expect an error report
                RunUI(() => Assert.IsTrue(transitionErrs2.AcceptButton.DialogResult == DialogResult.OK));
                var chooseIrt4 = ShowDialog<ChooseIrtStandardPeptidesDlg>(transitionErrs2.AcceptButton.PerformClick);
                OkDialog(chooseIrt4, () => chooseIrt4.OkDialogStandard(chooseStandard));
            }
            doc = WaitForDocumentChangeLoaded(doc);
            // We should have an extra peptide group and extra peptides since the standard peptides should've been added to the document
            AssertEx.IsDocumentState(doc, null, 64, (_asSmallMolecules?104:113) + chooseStandard.Peptides.Count, null, null);
            var biognosysTargets = new TargetMap<bool>(chooseStandard.Peptides.Select(pep => new KeyValuePair<Target, bool>(pep.ModifiedTarget, true)));
            var standardGroup = doc.MoleculeGroups.First();
            Assert.AreEqual(chooseStandard.Peptides.Count, standardGroup.MoleculeCount);
            foreach (var nodePep in standardGroup.Molecules)
                Assert.IsTrue(biognosysTargets.ContainsKey(nodePep.ModifiedTarget));
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void TestPaser()
        {
            var txtPaser =
                "PrecursorMz\tProductMz\tAnnotation\tProteinId\tGeneName\tPeptideSequence\tModifiedPeptideSequence\tPrecursorCharge\tLibraryIntensity\tNormalizedRetentionTime\tPrecursorIonMobility\tFragmentType\tFragmentCharge\tFragmentSeriesNumber\tFragmentLossType\tDecoyMobility\r\n" +
                "455.740693\t611.325988\ty5^1\tP04196\tP04196\tIADAHLDR\tIADAHLDR\t2\t5214.866434\t20.49698273123607\t0.8467999999999987\ty\t1\t5\t\t1.0551737300670958\r\n" +
                "657.010784\t467.224876\tb4^1\tP04114\tP04114\tTIHDLHLFIENIDFNK\tTIHDLHLFIENIDFNK\t3\t1877.282403\t72.22338448248237\t0.8796639241517041\tb\t1\t4\t\t0.7898170321876461\r\n" +
                "455.740693\t621.33549\tb6^1\tP04196\tP04196\tIADAHLDR\tIADAHLDR\t2\t280.898876\t20.49698273123607\t0.8467999999999987\tb\t1\t6\t\t1.0551737300670958\r\n" +
                "657.010784\t489.263804\tb8^2\tP04114\tP04114\tTIHDLHLFIENIDFNK\tTIHDLHLFIENIDFNK\t3\t6017.323145\t72.22338448248237\t0.8796639241517041\tb\t2\t8\t\t0.7898170321876461\r\n" +
                "455.740693\t726.352932\ty6^1\tP04196\tP04196\tIADAHLDR\tIADAHLDR\t2\t7823.772915\t20.49698273123607\t0.8467999999999987\ty\t1\t6\t\t1.0551737300670958\r\n" +
                "657.010784\t496.75601\ty8^2\tP04114\tP04114\tTIHDLHLFIENIDFNK\tTIHDLHLFIENIDFNK\t3\t468.687675\t72.22338448248237\t0.8796639241517041\ty\t2\t8\t\t0.7898170321876461\r\n";

            var csvFile = TestFilesDir.GetTestPath("_ip2_ip2_data_paser_spectral_library__timsTOFHT_plasma_lib_dilution.tsv");
            File.WriteAllText(csvFile, txtPaser);

            LoadNewDocument(true);

            // Enable use of ion mobility values from spectral libraries, and set a nonzero resolving power
            RunUI(() => SkylineWindow.ModifyDocument("adjust ion mobility filter settings", skyDoc =>
                skyDoc.ChangeSettings(skyDoc.Settings.ChangeTransitionIonMobilityFiltering(im =>
                    im.ChangeUseSpectralLibraryIonMobilityValues(true)
                        .ChangeFilterWindowWidthCalculator(new IonMobilityWindowWidthCalculator(30.0))))));

            var skyFile = TestFilesDir.GetTestPath("PASeRimport.sky");
            RunUI(() => Assert.IsTrue(SkylineWindow.SaveDocument(skyFile)));

            // Import assay library
            ImportAssayLibrarySkipColumnSelect(csvFile);
            var chooseIrt = WaitForOpenForm<ChooseIrtStandardPeptidesDlg>();
            using (new WaitDocumentChange(null, true))
            {
                OkDialog(chooseIrt, () => chooseIrt.OkDialogStandard(IrtStandard.BIOGNOSYS_11));
            }

            // Make sure that things like RT and IM are library values rather than explicit values
            var doc = SkylineWindow.Document;
            foreach (var ppp in doc.MoleculePrecursorPairs)
            {
                AssertEx.AreEqual( ExplicitTransitionGroupValues.EMPTY, ppp.NodeGroup.ExplicitValues, "Expected no explicit values to be set, should all be in library");
                if (!Equals(ppp.NodePep.GlobalStandardType, StandardType.IRT))
                {
                    Assert.IsNull(ppp.NodeGroup.ExplicitValues.IonMobility);    // No explicit ion mobility

                    var libKey = ppp.NodeGroup.GetLibKey(doc.Settings, ppp.NodePep);
                    doc.Settings.PeptideSettings.Libraries.TryGetSpectralLibraryIonMobilities(new[] { libKey }, 
                        null, out var libraryIonMobilityInfo);
                    var imsFilter = doc.Settings.GetIonMobilityFilter(ppp.NodePep, ppp.NodeGroup, null,
                        libraryIonMobilityInfo, null, 0);
                    AssertEx.AreEqual(eIonMobilityUnits.inverse_K0_Vsec_per_cm2, imsFilter.IonMobilityUnits,
                        string.Format("Unexpected ion mobility filter {0}", imsFilter));
                    var expectedIM = Equals(libKey.Target.Sequence, "IADAHLDR") ? 0.8468 : 0.87966;
                    AssertEx.AreEqual(expectedIM, imsFilter.IonMobility.Mobility, 0.001);
                }
            }
        }

        // Expects a message dialog after import window closes
        public static void ImportTransitionListSkipColumnSelectWithMessage(string csvPath, string expectedMessage, Action<MultiButtonMsgDlg> messageAction, bool isDemo = false)
        {
            ImportTransitionListSkipColumnSelect(csvPath, null, true, isDemo);
            var messageDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            AssertEx.AreEqual(expectedMessage, messageDlg.Message);
            OkDialog(messageDlg, () => messageAction(messageDlg));
        }

        // Expects a message dialog when user tries to close import window
        public static void ImportTransitionListSkipColumnSelectWithMessage(string csvPath, string expectedFirstMessage, int expectedMessageCount, bool proceedWithError)
        {
            var errors = expectedMessageCount > 0 ? new List<string>() : null;
            ImportTransitionListSkipColumnSelect(csvPath, errors, proceedWithError);
            if (errors != null)
            {
                AssertEx.AreEqual(expectedMessageCount, errors.Count);
                if (expectedFirstMessage != null)
                {
                    AssertEx.IsTrue(errors.Contains(expectedFirstMessage));
                }
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
                if (!_asSmallMolecules || proceedWithErrors)
                {
                    if (errDlg.AcceptButton.DialogResult == DialogResult.OK)
                        errorsAccepted = true;
                    errDlg.AcceptButton.PerformClick();
                }
                else
                {
                    errDlg.CancelButton.PerformClick(); // Cancel out of the error dialog, should return to the column select dialog
                }
            });
            if (errorsAccepted)
                WaitForClosedForm(transitionSelectDlg);
            else
                OkDialog(transitionSelectDlg, transitionSelectDlg.CancelDialog);
        }

        public static RCalcIrt ValidateDocAndIrt(int peptides, int irtTotal, int irtStandards)
        {
            TryWaitForConditionUI(6000, () => SkylineWindow.DocumentUI.MoleculeCount == peptides); // Peptide count checked below
            RCalcIrt calculator = null;
            RunUI(() =>
            {
                var doc = SkylineWindow.DocumentUI;
                Assert.AreEqual(peptides, doc.MoleculeCount);
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
            if (_asSmallMolecules)
            {
                var smallMolDoc = AsSmallMoleculeTestUtil.DocPathConvertedToSmallMolecules(document);
                if (!File.Exists(smallMolDoc))
                {
                    RunUI(() => SkylineWindow.OpenFile(document));
                    WaitForDocumentLoaded();
                    ConvertDocumentToSmallMolecules();
                    RunUI(() => SkylineWindow.SaveDocument(smallMolDoc));
                    RunUI(() => SkylineWindow.NewDocument(true));
                }
                document = smallMolDoc;
            }
            RunUI(() => SkylineWindow.OpenFile(document));
            return WaitForDocumentLoaded();
        }

        public void SkipLibraryDlg()
        {
            var libraryDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(libraryDlg, libraryDlg.Btn1Click); // Button1 is "Skip"
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

            foreach (var nodePepGroup in doc.MoleculeGroups)
            {
                foreach (var nodePep in nodePepGroup.Molecules)
                {
                    Assert.IsNotNull(calc.ScoreSequence(nodePep.ModifiedTarget));
                    Assert.IsTrue(nodePep.HasLibInfo);
                }
            }
        }

        private void VerifyAuditLog()
        {
            var english = "en";
            if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName != english)
            {
                return; // Keep it simple, only worry about one language
            }
            var auditLogActual = Path.Combine(TestFilesDir.GetTestPath(@".."), this.TestContext.TestName, @"Auditlog", english, this.TestContext.TestName) + ".log";
            var auditLogExpected = TestFilesDir.GetTestPath(_asSmallMolecules ? @"TestAssayLibraryImportAsSmallMolecules.log" : @"TestAssayLibraryImport.log");
            AssertEx.FileEquals(auditLogExpected, auditLogActual, null, true);
        }
    }
}
