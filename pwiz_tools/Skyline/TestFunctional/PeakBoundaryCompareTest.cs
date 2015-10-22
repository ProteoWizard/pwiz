/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakBoundaryCompareTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakBoundaryCompare()
        {
            TestFilesZip = @"TestFunctional\PeakBoundaryCompareTest.zip";
            RunFunctionalTest();
        }

        protected string GetLocalizedFile(string fileName)
        {
            return (TextUtil.CsvSeparator == TextUtil.SEPARATOR_CSV)
                ? TestFilesDir.GetTestPath(fileName)
                : TestFilesDir.GetTestPath(Path.GetFileNameWithoutExtension(fileName) + "_intl" + Path.GetExtension(fileName));

        }

        protected override void DoTest()
        {
            var documentBase = TestFilesDir.GetTestPath("AQUA4_Human_picked_napedro2_short.sky");
            var peakBoundariesFile = GetLocalizedFile("OpenSwathPeaks.csv");
            RunUI(() => SkylineWindow.OpenFile(documentBase));
            WaitForDocumentLoaded();
            AddTrainedModels();

            var comparePeakPickingDlg = ShowDialog<ComparePeakPickingDlg>(SkylineWindow.ShowCompareModelsDlg);
            AddFile(comparePeakPickingDlg, "OpenSwath", peakBoundariesFile);
            AddModel(comparePeakPickingDlg, "skyline_default_plus");
            AddModel(comparePeakPickingDlg, "full model");
            CheckNumberComparisons(comparePeakPickingDlg, 3, 3, 3, 3);

            int numberResults = 0;
            RunUI(() =>
            {
                var resultsPeptides = SkylineWindow.Document.Peptides.Where(pep => !pep.IsDecoy && pep.GlobalStandardType == null);
                var resultsGroups = resultsPeptides.SelectMany(pep => pep.TransitionGroups);
                var results = resultsGroups.SelectMany(group => group.ChromInfos);
                var resultsList = results.Where(info => info != null).ToList();
                numberResults = resultsList.Count;
            });
            Assert.AreEqual(numberResults, 344);
            CheckNumberResults(comparePeakPickingDlg, numberResults);

            var addPeakCompareDlg = ShowDialog<AddPeakCompareDlg>(comparePeakPickingDlg.Add);
            // Test for various errors that occur when adding a model/file
            TestErrors(addPeakCompareDlg, peakBoundariesFile);
            OkDialog(addPeakCompareDlg, addPeakCompareDlg.CancelDialog);
            CheckNumberResults(comparePeakPickingDlg, numberResults);

            // Add a model through the edit list dialog
            var editListDlg = ShowDialog<EditListDlg<ComparePeakBoundariesList, ComparePeakBoundaries>>(comparePeakPickingDlg.EditList);
            var addPeakCompareDlgEdit = ShowDialog<AddPeakCompareDlg>(editListDlg.AddItem);
            RunUI(() => addPeakCompareDlgEdit.IsModel = true);
            RunUI(() => addPeakCompareDlgEdit.PeakScoringModelSelected = "skyline_default");
            OkDialog(addPeakCompareDlgEdit, addPeakCompareDlgEdit.OkDialog);
            OkDialog(editListDlg, editListDlg.OkDialog);
            CheckNumberComparisons(comparePeakPickingDlg, 4, 4, 4, 4);
            CheckNumberResults(comparePeakPickingDlg, numberResults);


            // Open the model comparison from the EditListDlg, regenerate it, get same results
            var editListDlgRedo = ShowDialog<EditListDlg<ComparePeakBoundariesList, ComparePeakBoundaries>>(comparePeakPickingDlg.EditList);
            RunUI(() => editListDlgRedo.SelectItem("skyline_default"));
            var addPeakCompareDlgRedo = ShowDialog<AddPeakCompareDlg>(editListDlgRedo.EditItem);
            OkDialog(addPeakCompareDlgRedo, addPeakCompareDlgRedo.OkDialog);
            OkDialog(editListDlgRedo, editListDlgRedo.OkDialog);
            CheckNumberComparisons(comparePeakPickingDlg, 4, 4, 4, 4);
            CheckNumberResults(comparePeakPickingDlg, numberResults);

            // Open the model comparison from the EditListDlg, rename it, get same results
            var editListDlgRename = ShowDialog<EditListDlg<ComparePeakBoundariesList, ComparePeakBoundaries>>(comparePeakPickingDlg.EditList);
            RunUI(() => editListDlgRename.SelectItem(string.Format(Resources.ComparePeakBoundaries_ComparePeakBoundaries__0___external_, "OpenSwath")));
            var addPeakCompareDlgRename = ShowDialog<AddPeakCompareDlg>(editListDlgRename.EditItem);
            RunUI(() =>
            {
                addPeakCompareDlgRename.FileName = "OpenSwathRename";
            });
            OkDialog(addPeakCompareDlgRename, addPeakCompareDlgRename.OkDialog);
            OkDialog(editListDlgRename, editListDlgRename.OkDialog);
            RunUI(() => Assert.IsTrue(comparePeakPickingDlg.ComparePeakBoundariesList.Select(comp => comp.Name).Contains(string.Format(Resources.ComparePeakBoundaries_ComparePeakBoundaries__0___external_, "OpenSwathRename"))));
            CheckNumberComparisons(comparePeakPickingDlg, 4, 4, 4, 4);
            CheckNumberResults(comparePeakPickingDlg, numberResults);

            // Train a new model from the AddPeakCompareDlg
            var addPeakCompareDlgNew = ShowDialog<AddPeakCompareDlg>(comparePeakPickingDlg.Add);
            RunDlg<EditPeakScoringModelDlg>(addPeakCompareDlgNew.AddPeakScoringModel, editModelDlg =>
            {
                editModelDlg.TrainModel();
                editModelDlg.PeakScoringModelName = "full model new";
                editModelDlg.OkDialog();
            });
            OkDialog(addPeakCompareDlgNew, addPeakCompareDlgNew.CancelDialog);
            // Add the new model in
            AddModel(comparePeakPickingDlg, "full model new");

            RunUI(() =>
            {
                CheckNumberComparisons(comparePeakPickingDlg, 5, 5, 5, 5);
                CheckNumberResults(comparePeakPickingDlg, numberResults);

                // Uncheck one of the models and make sure everything adjusts properly
                comparePeakPickingDlg.SetCheckedComparer(4, false);
                CheckNumberComparisons(comparePeakPickingDlg, 4, 4, 4, 4);
                CheckNumberResults(comparePeakPickingDlg, numberResults);
                // Recheck and show it goes back
                comparePeakPickingDlg.SetCheckedComparer(4, true);
                CheckNumberComparisons(comparePeakPickingDlg, 5, 5, 5, 5);
                CheckNumberResults(comparePeakPickingDlg, numberResults);
                // Uncheck all, show that graph, grids, etc are appropriately blank
                comparePeakPickingDlg.SetCheckedComparer(0, false);
                comparePeakPickingDlg.SetCheckedComparer(1, false);
                comparePeakPickingDlg.SetCheckedComparer(2, false);
                comparePeakPickingDlg.SetCheckedComparer(3, false);
                comparePeakPickingDlg.SetCheckedComparer(4, false);
                CheckNumberComparisons(comparePeakPickingDlg, 0, 0, 0, 0);
                CheckNumberResults(comparePeakPickingDlg, numberResults);
                Assert.AreEqual(0, comparePeakPickingDlg.CountCompareGridEntries);
                Assert.AreEqual(0, comparePeakPickingDlg.CountDetailsGridEntries);

                // Recheck a couple models
                comparePeakPickingDlg.SetCheckedComparer(0, true);
                comparePeakPickingDlg.SetCheckedComparer(1, true);
                CheckNumberComparisons(comparePeakPickingDlg, 2, 2, 2, 2);
                CheckNumberResults(comparePeakPickingDlg, numberResults);
                Assert.AreEqual(5, comparePeakPickingDlg.ComparePeakBoundariesList.Count);
            });
            // Remove an unchecked model using the EditListDlg, show that it doesn't affect the graph, etc
            RemoveComparer(comparePeakPickingDlg, "skyline_default_plus");
            RunUI(() =>
            {
                CheckNumberComparisons(comparePeakPickingDlg, 2, 2, 2, 2);
                CheckNumberResults(comparePeakPickingDlg, numberResults);
                Assert.AreEqual(4, comparePeakPickingDlg.ComparePeakBoundariesList.Count); 
            });

            // Q value missing ok but causes 1 of the Q value graphs to not be drawn
            var peakBoundariesFileNoQ = GetLocalizedFile("OpenSwathPeaksMissingQ.csv");
            AddFile(comparePeakPickingDlg, "OpenSwathBadQ", peakBoundariesFileNoQ);
            CheckNumberComparisons(comparePeakPickingDlg, 3, 2, 3, 3);
            CheckNumberResults(comparePeakPickingDlg, numberResults);
            RemoveComparer(comparePeakPickingDlg, string.Format(Resources.ComparePeakBoundaries_ComparePeakBoundaries__0___external_,"OpenSwathBadQ"));
            CheckNumberComparisons(comparePeakPickingDlg, 2, 2, 2, 2);
            CheckNumberResults(comparePeakPickingDlg, numberResults);
            
            // Score missing ok
            var peakBoundariesFileNoScore = GetLocalizedFile("OpenSwathPeaksMissingScores.csv");
            AddFile(comparePeakPickingDlg, "OpenSwathBadScore", peakBoundariesFileNoScore);
            CheckNumberComparisons(comparePeakPickingDlg, 3, 3, 3, 3);
            CheckNumberResults(comparePeakPickingDlg, numberResults);
            RemoveComparer(comparePeakPickingDlg, string.Format(Resources.ComparePeakBoundaries_ComparePeakBoundaries__0___external_, "OpenSwathBadScore"));
            CheckNumberComparisons(comparePeakPickingDlg, 2, 2, 2, 2);
            CheckNumberResults(comparePeakPickingDlg, numberResults);

            // If file does not contain peak boundaries for every result in the document, missing results get filled in with a notification
            var peakBoundariesFileFillInCancel = GetLocalizedFile("OpenSwathPeaksFillIn.csv");
            var addPeakCompareDlgFillInCancel = ShowDialog<AddPeakCompareDlg>(comparePeakPickingDlg.Add);
            RunUI(() =>
            {
                addPeakCompareDlgFillInCancel.IsModel = false;
                addPeakCompareDlgFillInCancel.FileName = "OpenSwathFillIn";
                addPeakCompareDlgFillInCancel.FilePath = peakBoundariesFileFillInCancel;
            });
            var messageDlgCancel = ShowDialog<MultiButtonMsgDlg>(addPeakCompareDlgFillInCancel.OkDialog);
            Assert.AreEqual(messageDlgCancel.Message, string.Format(Resources.AddPeakCompareDlg_OkDialog_The_imported_file_does_not_contain_any_peak_boundaries_for__0__transition_group___file_pairs___These_chromatograms_will_be_treated_as_if_no_boundary_was_selected_,
                                                              3));
            // Canceling leads to no change
            OkDialog(messageDlgCancel, messageDlgCancel.CancelDialog);
            OkDialog(addPeakCompareDlgFillInCancel, addPeakCompareDlgFillInCancel.CancelDialog);
            CheckNumberComparisons(comparePeakPickingDlg, 2, 2, 2, 2);
            CheckNumberResults(comparePeakPickingDlg, numberResults);

            // Try again
            var peakBoundariesFileFillIn = GetLocalizedFile("OpenSwathPeaksFillIn.csv");
            var addPeakCompareDlgFillIn = ShowDialog<AddPeakCompareDlg>(comparePeakPickingDlg.Add);
            RunUI(() =>
            {
                addPeakCompareDlgFillIn.IsModel = false;
                addPeakCompareDlgFillIn.FileName = "OpenSwathFillIn";
                addPeakCompareDlgFillIn.FilePath = peakBoundariesFileFillIn;
            });
            var messageDlg = ShowDialog<MultiButtonMsgDlg>(addPeakCompareDlgFillIn.OkDialog);
            Assert.AreEqual(messageDlg.Message, string.Format(Resources.AddPeakCompareDlg_OkDialog_The_imported_file_does_not_contain_any_peak_boundaries_for__0__transition_group___file_pairs___These_chromatograms_will_be_treated_as_if_no_boundary_was_selected_,
                                                              3));
            OkDialog(messageDlg, messageDlg.Btn1Click);
            RunUI(() =>
            {
                var comparerFillIn = comparePeakPickingDlg.ComparePeakBoundariesList.FirstOrDefault(comparer => comparer.FileName == "OpenSwathFillIn");
                Assert.IsNotNull(comparerFillIn);
                var matches = comparerFillIn.Matches;
                var missingMatches = matches.Where(match => match.IsMissingPickedPeak && match.QValue == null && match.Score == null).ToList();
                Assert.AreEqual(missingMatches.Count, 3);
                foreach (var match in missingMatches)
                {
                    Assert.IsTrue(match.QValue == null);
                    Assert.IsTrue(match.Score == null);
                }
            });
            CheckNumberComparisons(comparePeakPickingDlg, 3, 3, 3, 3);
            CheckNumberResults(comparePeakPickingDlg, numberResults);

            // Null q values and scores are OK when the peak boundaries are null too
            var peakBoundariesFileNull = GetLocalizedFile("OpenSwathPeaksNullPeaks.csv");
            var addPeakCompareDlgNull = ShowDialog<AddPeakCompareDlg>(comparePeakPickingDlg.Add);
            RunUI(() =>
            {
                addPeakCompareDlgNull.IsModel = false;
                addPeakCompareDlgNull.FileName = "OpenSwathNull";
                addPeakCompareDlgNull.FilePath = peakBoundariesFileNull;
            });
            var messageDlgNull = ShowDialog<MultiButtonMsgDlg>(addPeakCompareDlgNull.OkDialog);
            Assert.AreEqual(messageDlgNull.Message, string.Format(Resources.AddPeakCompareDlg_OkDialog_The_imported_file_does_not_contain_any_peak_boundaries_for__0__transition_group___file_pairs___These_chromatograms_will_be_treated_as_if_no_boundary_was_selected_,
                                                              342));
            OkDialog(messageDlgNull, messageDlgNull.Btn1Click);
            RunUI(() =>
            {
                CheckNumberComparisons(comparePeakPickingDlg, 4, 4, 4, 4);
                CheckNumberResults(comparePeakPickingDlg, numberResults);
            });

            // Unrecognized peptide leads to message box warning, but otherwise ok
            var peakBoundariesFilePeptide = GetLocalizedFile("OpenSwathPeaksBadPeptide.csv");
            var addPeakComparePeptide = ShowDialog<AddPeakCompareDlg>(comparePeakPickingDlg.Add);
            RunUI(() =>
            {
                addPeakComparePeptide.IsModel = false;
                addPeakComparePeptide.FileName = "OpenSwathBadPeptide";
                addPeakComparePeptide.FilePath = peakBoundariesFilePeptide;
            });
            var messageDlgPeptidePre = ShowDialog<MultiButtonMsgDlg>(addPeakComparePeptide.OkDialog);
            Assert.AreEqual(messageDlgPeptidePre.Message, string.Format(Resources.AddPeakCompareDlg_OkDialog_The_imported_file_does_not_contain_any_peak_boundaries_for__0__transition_group___file_pairs___These_chromatograms_will_be_treated_as_if_no_boundary_was_selected_,
                                                              343));
            OkDialog(messageDlgPeptidePre, messageDlgPeptidePre.Btn1Click);
            var messageDlgPeptide = WaitForOpenForm<MultiButtonMsgDlg>();
            Assert.AreEqual(messageDlgPeptide.Message, TextUtil.LineSeparate(Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_The_following_peptide_in_the_peak_boundaries_file_was_not_recognized_,
                                                                     "",
                                                                     "PEPTIDER",
                                                                     "",
                                                                     Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_Continue_peak_boundary_import_ignoring_this_peptide_));
            OkDialog(messageDlgPeptide, messageDlgPeptide.Btn1Click);
            RunUI(() =>
            {
                CheckNumberComparisons(comparePeakPickingDlg, 5, 5, 5, 5);
                CheckNumberResults(comparePeakPickingDlg, numberResults);
            });

            // Unrecognized file leads to message box warning, but otherwise ok
            var peakBoundariesFileFile = GetLocalizedFile("OpenSwathPeaksBadFile.csv");
            var addPeakCompareFile = ShowDialog<AddPeakCompareDlg>(comparePeakPickingDlg.Add);
            RunUI(() =>
            {
                addPeakCompareFile.IsModel = false;
                addPeakCompareFile.FileName = "OpenSwathBadFile";
                addPeakCompareFile.FilePath = peakBoundariesFileFile;
            });
            var messageDlgFilePre = ShowDialog<MultiButtonMsgDlg>(addPeakCompareFile.OkDialog);
            Assert.AreEqual(messageDlgFilePre.Message, string.Format(Resources.AddPeakCompareDlg_OkDialog_The_imported_file_does_not_contain_any_peak_boundaries_for__0__transition_group___file_pairs___These_chromatograms_will_be_treated_as_if_no_boundary_was_selected_,
                                                              343));
            OkDialog(messageDlgFilePre, messageDlgFilePre.Btn1Click);
            var messageDlgFile = WaitForOpenForm<MultiButtonMsgDlg>();
            Assert.AreEqual(messageDlgFile.Message, TextUtil.LineSeparate(Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_The_following_file_name_in_the_peak_boundaries_file_was_not_recognized_,
                                                                             "",
                                                                             "bad_file",
                                                                             "",
                                                                             Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_Continue_peak_boundary_import_ignoring_this_file_));
            OkDialog(messageDlgFile, messageDlgFile.Btn1Click);
            RunUI(() =>
            {
                CheckNumberComparisons(comparePeakPickingDlg, 6, 6, 6, 6);
                CheckNumberResults(comparePeakPickingDlg, numberResults);
            });

            // Import based on peak apex rather than boundaries works OK
            var peakBoundariesApex = GetLocalizedFile("OpenSwathPeaksApex.csv");
            AddFile(comparePeakPickingDlg, "OpenSwathApex", peakBoundariesApex);
            CheckNumberComparisons(comparePeakPickingDlg, 7, 7, 7, 7);
            CheckNumberResults(comparePeakPickingDlg, numberResults);
           
            // Check that the "conflicts only" checkbox works correctly
            RunUI(() =>
            {
                comparePeakPickingDlg.CheckBoxConflicts = true;
                comparePeakPickingDlg.ComboCompare1Selected = string.Format(Resources.ComparePeakBoundaries_ComparePeakBoundaries__0___external_, "OpenSwathApex");
                comparePeakPickingDlg.ComboCompare2Selected = string.Format(Resources.ComparePeakBoundaries_ComparePeakBoundaries__0___external_, "OpenSwathRename");
                Assert.AreEqual(comparePeakPickingDlg.CountCompareGridEntries, 6);
            });

            OkDialog(comparePeakPickingDlg, comparePeakPickingDlg.OkDialog);
        }

        private void TestErrors(AddPeakCompareDlg addPeakCompareDlg, string peakBoundariesFile)
        {
            // 1. Blank name gives error
            RunUI(() => addPeakCompareDlg.IsModel = false);
            MessageDlgError(addPeakCompareDlg.OkDialog, Resources.AddPeakCompareDlg_OkDialog_Comparison_name_cannot_be_empty_);
            // 2. Blank file path gives error
            RunUI(() => addPeakCompareDlg.FileName = "OpenSwathNew");
            MessageDlgError(addPeakCompareDlg.OkDialog, Resources.AddPeakCompareDlg_OkDialog_File_path_cannot_be_empty_);
            // 3. Non-existent file gives error
            RunUI(() => addPeakCompareDlg.FilePath = "bad_file_path");
            MessageDlgError(addPeakCompareDlg.OkDialog, Resources.AddPeakCompareDlg_OkDialog_File_path_field_must_contain_a_path_to_a_valid_file_);
            // 4. Same name as existing gives error
            RunUI(() => addPeakCompareDlg.FileName = "OpenSwath");
            RunUI(() => addPeakCompareDlg.FilePath = peakBoundariesFile);
            MessageDlgError(addPeakCompareDlg.OkDialog, Resources.AddPeakCompareDlg_OkDialog_There_is_already_an_imported_file_with_the_current_name___Please_choose_another_name);
            // 5. Attempt to re-load same model gives error
            RunUI(() => addPeakCompareDlg.IsModel = true);
            RunUI(() => addPeakCompareDlg.PeakScoringModelSelected = "full model");
            MessageDlgError(addPeakCompareDlg.OkDialog, Resources.AddPeakCompareDlg_OkDialog_The_selected_model_is_already_included_in_the_list_of_comparisons__Please_choose_another_model_);
            // 6. Selecting an untrained model leads to error
            RunUI(() => addPeakCompareDlg.PeakScoringModelSelected = "untrained");
            MessageDlgError(addPeakCompareDlg.OkDialog, Resources.AddPeakCompareDlg_OkDialog_Model_must_be_trained_before_it_can_be_used_for_peak_boundary_comparison_);
            // 7. File with unreadable q value produces error
            var peakBoundariesBadQ = GetLocalizedFile("OpenSwathPeaksBadQ.csv");
            RunUI(() => addPeakCompareDlg.IsModel = false);
            RunUI(() => addPeakCompareDlg.FileName = "OpenSwathBad");
            RunUI(() => addPeakCompareDlg.FilePath = peakBoundariesBadQ);
            MessageDlgError(addPeakCompareDlg.OkDialog, string.Format(Resources.AddPeakCompareDlg_OkDialog_Error_applying_imported_peak_boundaries___0_,
                                                                      string.Format(Resources.PeakBoundsMatch_QValue_Unable_to_read_q_value_annotation_for_peptide__0__of_file__1_,
                                                                                    "APIPTALDTDSSK",
                                                                                    "napedro_L120420_007_SW")));
            // 8. File with unreadable score produces error
            var peakBoundariesBadScore = GetLocalizedFile("OpenSwathPeaksBadScore.csv");
            RunUI(() => addPeakCompareDlg.IsModel = false);
            RunUI(() => addPeakCompareDlg.FileName = "OpenSwathBad");
            RunUI(() => addPeakCompareDlg.FilePath = peakBoundariesBadScore);
            MessageDlgError(addPeakCompareDlg.OkDialog, string.Format(Resources.AddPeakCompareDlg_OkDialog_Error_applying_imported_peak_boundaries___0_,
                                                                      string.Format(Resources.PeakBoundsMatch_QValue_Unable_to_read_q_value_annotation_for_peptide__0__of_file__1_,
                                                                                    "APIPTALDTDSSK",
                                                                                    "napedro_L120420_007_SW")));
            // 9. File with missing q value column and score column produces error
            var peakBoundariesMissing = GetLocalizedFile("OpenSwathPeaksMissingQScores.csv");
            RunUI(() => addPeakCompareDlg.IsModel = false);
            RunUI(() => addPeakCompareDlg.FileName = "OpenSwathMissing");
            RunUI(() => addPeakCompareDlg.FilePath = peakBoundariesMissing);

            MessageDlgError(addPeakCompareDlg.OkDialog, string.Format(Resources.AddPeakCompareDlg_OkDialog_Error_applying_imported_peak_boundaries___0_,
                                                                      Resources.AddPeakCompareDlg_OkDialog_The_current_file_or_model_has_no_q_values_or_scores_to_analyze___Either_q_values_or_scores_are_necessary_to_compare_peak_picking_tools_));
            // 10. Empty file leads to error
            var peakBoundariesEmpty = GetLocalizedFile("OpenSwathPeaksEmpty.csv");
            RunUI(() => addPeakCompareDlg.IsModel = false);
            RunUI(() => addPeakCompareDlg.FileName = "OpenSwathEmpty");
            RunUI(() => addPeakCompareDlg.FilePath = peakBoundariesEmpty);
            MessageDlgError(addPeakCompareDlg.OkDialog, string.Format(Resources.AddPeakCompareDlg_OkDialog_Error_applying_imported_peak_boundaries___0_,
                                                                      Resources.AddPeakCompareDlg_OkDialog_The_selected_file_or_model_does_not_assign_peak_boundaries_to_any_chromatograms_in_the_document___Please_select_a_different_model_or_file_));

            // 11. Apex-based file with bad apex entry leads to error
            var peakBoundariesApex = GetLocalizedFile("OpenSwathPeaksBadApex.csv");
            RunUI(() => addPeakCompareDlg.IsModel = false);
            RunUI(() => addPeakCompareDlg.FileName = "OpenSwathBadApex");
            RunUI(() => addPeakCompareDlg.FilePath = peakBoundariesApex);
            MessageDlgError(addPeakCompareDlg.OkDialog, string.Format(Resources.AddPeakCompareDlg_OkDialog_Error_applying_imported_peak_boundaries___0_,
                                                                      string.Format(Resources.PeakBoundsMatch_PeakBoundsMatch_Unable_to_read_apex_retention_time_value_for_peptide__0__of_file__1__,
                                                                                    "DITAFDETLFR",
                                                                                    "napedro_L120420_007_SW")));
        }

        private static void CheckNumberResults(ComparePeakPickingDlg comparePeakPickingDlg, int numResults)
        {
            foreach (var comparer in comparePeakPickingDlg.ComparePeakBoundariesList)
            {
                Assert.AreEqual(comparer.Matches.Count, numResults);
            }
        }

        private static void CheckNumberComparisons(ComparePeakPickingDlg comparePeakPickingDlg, 
                                            int rocCurves, 
                                            int qCurves,
                                            int detailsChoices, 
                                            int compareChoices)
        {
            WaitForConditionUI(() => Equals(Math.Max(comparePeakPickingDlg.CountRocCurves - 1, 0), rocCurves), "unexpected rocCurves count");  // Use WaitForCondition instead of Assert.AreEqual to avoid race conditions
            Assert.AreEqual(Math.Max(comparePeakPickingDlg.CountQCurves - 2, 0), qCurves);
            Assert.AreEqual(comparePeakPickingDlg.CountDetailsItems, detailsChoices);
            Assert.AreEqual(comparePeakPickingDlg.CountCompare1Items, compareChoices);
            Assert.AreEqual(comparePeakPickingDlg.CountCompare2Items, compareChoices);
        }

        private void AddTrainedModels()
        {
            // Load settings containing a trained model
            var skylineSettings = TestFilesDir.GetTestPath("settings_test.skys");
            RunUI(() => ShareListDlg<SrmSettingsList, SrmSettings>.ImportFile(SkylineWindow, Settings.Default.SrmSettingsList, skylineSettings));

            // Add an untrained model
            RunEditPeakScoringDlg(null, true, editPeakScoringModelDlg =>
            {
                editPeakScoringModelDlg.PeakScoringModelName = "untrained";
                editPeakScoringModelDlg.OkDialog();
            });
            // Train a couple additional models
            RunEditPeakScoringDlg(null, true, editPeakScoringModelDlg =>
            {
                editPeakScoringModelDlg.PeakScoringModelName = "skyline_default_plus";
                editPeakScoringModelDlg.SelectedModelItem = LegacyScoringModel.DEFAULT_NAME;
                editPeakScoringModelDlg.TrainModel();
                editPeakScoringModelDlg.OkDialog();
            });
            RunEditPeakScoringDlg(null, true, editPeakScoringModelDlg =>
            {
                editPeakScoringModelDlg.PeakScoringModelName = "skyline_default";
                editPeakScoringModelDlg.SelectedModelItem = LegacyScoringModel.DEFAULT_NAME;
                editPeakScoringModelDlg.PeakCalculatorsGrid.Items[4].IsEnabled = false;
                editPeakScoringModelDlg.PeakCalculatorsGrid.Items[5].IsEnabled = false;
                editPeakScoringModelDlg.PeakCalculatorsGrid.Items[6].IsEnabled = false;
                editPeakScoringModelDlg.TrainModel();
                editPeakScoringModelDlg.OkDialog();
            });
        }

        private static void MessageDlgError(Action action, string errorMessage)
        {
            var messageDlg = ShowDialog<MessageDlg>(action);
            Assert.AreEqual(messageDlg.Message, errorMessage);
            messageDlg.OkDialog();
        }

        private static void AddFile(ComparePeakPickingDlg dlg, string fileName, string filePath)
        {
            var addPeakCompareDlg = ShowDialog<AddPeakCompareDlg>(dlg.Add);
            RunUI(() =>
            {
                addPeakCompareDlg.IsModel = false;
                addPeakCompareDlg.FileName = fileName;
                addPeakCompareDlg.FilePath = filePath;
            });
            OkDialog(addPeakCompareDlg, addPeakCompareDlg.OkDialog);
        }

        private static void AddModel(ComparePeakPickingDlg dlg, string modelName)
        {
            var addPeakCompareDlg = ShowDialog<AddPeakCompareDlg>(dlg.Add);
            RunUI(() =>
            {
                addPeakCompareDlg.IsModel = true;
                addPeakCompareDlg.PeakScoringModelSelected = modelName;
            });
            OkDialog(addPeakCompareDlg, addPeakCompareDlg.OkDialog);
        }

        public static void RemoveComparer(ComparePeakPickingDlg dlg, string comparerName)
        {
            var editListDlgRemove = ShowDialog<EditListDlg<ComparePeakBoundariesList, ComparePeakBoundaries>>(dlg.EditList);
            RunUI(() =>
            {
                editListDlgRemove.SelectItem(comparerName);
                editListDlgRemove.RemoveItem();
            });
            OkDialog(editListDlgRemove, editListDlgRemove.OkDialog);
        }

        protected static void RunEditPeakScoringDlg(string editName, bool cancelReintegrate, Action<EditPeakScoringModelDlg> act)
        {
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            if (editName != null)
            {
                var editList = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    reintegrateDlg.EditPeakScoringModel);
                RunUI(() => editList.SelectItem(editName));
                RunDlg(editList.EditItem, act);
                OkDialog(editList, editList.OkDialog);
            }
            else
            {
                RunDlg(reintegrateDlg.AddPeakScoringModel, act);
            }
            if (cancelReintegrate)
            {
                OkDialog(reintegrateDlg, reintegrateDlg.CancelDialog);
            }
            else
            {
                OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
            }
        }
    }
}
