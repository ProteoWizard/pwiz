/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakScoringModelTest : AbstractFunctionalTest
    {
        private IList<IPeakFeatureCalculator> _defaultMProphetCalcs;
        
        [TestMethod]
        public void TestPeakScoringModel()
        {
            TestFilesZip = @"TestFunctional\PeakScoringModelTest.zip";
            RunFunctionalTest();

            Assert.IsFalse(IsRecordMode);   // Make sure this doesn't get committed as true
        }

        /// <summary>
        /// Change to true to write coefficient arrays
        /// </summary>
        private bool IsRecordMode { get { return false; } }

        private readonly string[] SCORES_AND_WEIGHTS =
        {
            "-14.1764344689507",
            "True|0.1532|5.7%;False||;True|3.4162|5.3%;True|2.8635|14.0%;True|-0.4625|22.4%;True|0.2158|5.8%;False||;False||;True|3.8557|11.6%;True|10.0079|48.3%;True|0.3344|-23.7%;True|0.2500|6.2%;True|0.2602|8.8%;True|-2.1446|-4.3%;False||;False||;False||;False||;",
            "True|0.5479|19.8%;False||;True|4.0247|6.1%;False||;True|-0.4378|26.3%;True|0.2784|6.2%;False||;False||;True|4.6641|13.6%;True|10.8428|55.4%;True|0.3545|-29.2%;True|0.3185|7.3%;True|-0.0349|-1.2%;True|-2.1477|-4.2%;False||;False||;False||;False||;",
            "-14.8785512519375",
            "-9.37936231171802",
            "True|0.9834|74.8%;True|0.9834|15.7%;True|19.6686|0.0%;True|2.9503|9.5%;True|3.9337|0.0%;True|-0.0492|0.0%;True|-0.6884|0.0%;",
            "-11.6384433459567",
            "True|1.1144|79.9%;True|1.1144|18.5%;True|22.2884|0.0%;True|3.3433|1.6%;True|4.4577|0.0%;True|-0.0557|0.0%;True|-0.7801|0.0%;",
            "True|0.8633|31.5%;True|2.0177|3.5%;True|6.2170|27.4%;False||;False||;False||;True|7.5352|31.7%;True|-0.1277|5.8%;False||;False||;",
            "True|0.5479|19.8%;False||;True|4.0247|6.1%;False||;True|-0.4378|26.3%;True|0.2784|6.2%;False||;False||;True|4.6641|13.6%;True|10.8428|55.4%;True|0.3545|-29.2%;True|0.3185|7.3%;True|-0.0349|-1.2%;True|-2.1477|-4.2%;False||;False||;False||;False||;",
            "True|0.5322|;False|-1.0352|;False||;True|1.4744|;True|0.0430|;True|0.0477|;False|-0.2740|;False||;True|2.0096|;True|7.7726|;True|-0.0566|;True|0.4751|;True|0.5000|;True|0.5000|;False||;False||;False||;False||;False||;False||;False||;False||;",
        };

        protected override void DoTest()
        {
            Settings.Default.PeakScoringModelList.Clear();

            var documentFile = TestFilesDir.GetTestPath("MProphetGold-rescore2.sky");
            RunUI(() => SkylineWindow.OpenFile(documentFile));
            var docLoaded = WaitForDocumentLoaded();
            _defaultMProphetCalcs = new MProphetPeakScoringModel("dummy", docLoaded).PeakFeatureCalculators.ToArray();

            TestDialog();
            TestModelChangesAndSave();
            TestBackwardCompatibility();
            TestIncompatibleDataSet();

            var documentMissingScores = TestFilesDir.GetTestPath("SRMCourse_DosR-hDP__20130501.sky");
            RunUI(() => SkylineWindow.OpenFile(documentMissingScores));
            WaitForDocumentLoaded();
            TestFindMissingScores();
        }

        protected void TestDialog()
        {

            // Display integration tab.
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);

            {
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(reintegrateDlg.AddPeakScoringModel);

                // Check default values.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.PeakScoringModelName, string.Empty);
                        var rows = editDlg.PeakCalculatorsGrid.RowCount;
                        Assert.AreEqual(_defaultMProphetCalcs.Count, rows, "Unexpected count of peak calculators"); // Not L10N
                    });

                // Test empty name.
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(
                            Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty, messageDlg.Message, 1);
                        messageDlg.OkDialog();
                    });

                RunUI(() => editDlg.PeakScoringModelName = "test1"); // Not L10N

                // Create a model with default values.
                RunUI(() =>
                    {
                        editDlg.TrainModelClick();
                        for (int i = 0; i < editDlg.PeakCalculatorsGrid.RowCount; i++)
                        {
                            editDlg.PeakCalculatorsGrid.SelectRow(i);
                        }
                        VerifyBias(editDlg, SCORES_AND_WEIGHTS[0]);
                    });
                OkDialog(editDlg, editDlg.OkDialog);
            }

            var editList =
                ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    reintegrateDlg.EditPeakScoringModel);

            RunUI(() => editList.SelectItem("test1")); // Not L10N

            {
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(editList.EditItem);
                // Verify weights, change name.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.PeakScoringModelName, "test1"); // Not L10N
                        VerifyCellValues(editDlg, SCORES_AND_WEIGHTS[1]);
                        VerifyBias(editDlg, SCORES_AND_WEIGHTS[0], false);
                        // Manually uncheck two of the scores
                        editDlg.SetChecked(3, false);
                        editDlg.SetChecked(6, false);
                        editDlg.TrainModelClick();
                        VerifyCellValues(editDlg, SCORES_AND_WEIGHTS[2]);
                        VerifyBias(editDlg, SCORES_AND_WEIGHTS[3]);
                        // Re-check the scores, show that model goes back to normal
                        editDlg.SetChecked(3, true);
                        editDlg.SetChecked(6, true);
                        editDlg.TrainModelClick();
                        VerifyCellValues(editDlg, SCORES_AND_WEIGHTS[1], 1.0, false);
                        VerifyBias(editDlg, SCORES_AND_WEIGHTS[0], false);
                        editDlg.PeakScoringModelName = "test2"; // Not L10N
                    });
               OkDialog(editDlg, editDlg.OkDialog);
            }
            {
                // Add conflicting name.
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(editList.AddItem);
                RunUI(() =>
                {
                    Assert.AreEqual(editDlg.PeakScoringModelName, "");
                    editDlg.PeakScoringModelName = "test2"; // Not L10N
                });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.EditPeakScoringModelDlg_OkDialog_The_peak_scoring_model__0__already_exists, messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });
                OkDialog(editDlg, editDlg.CancelDialog);
                OkDialog(editList, editList.OkDialog);
                RunUI(() => reintegrateDlg.ComboPeakScoringModelSelected = "test2");
                var docBefore = SkylineWindow.Document;
                OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
                WaitForDocumentChange(docBefore);
            }
        }


        /// <summary>
        /// Trains a legacy model, checks that it saves correctly in the list and the document, 
        /// modifies it without changing its name and checks that the list and document models update correctly
        /// </summary>
        protected void TestModelChangesAndSave()
        {
            LegacyScoringModel peakScoringModelBase = null;
            
            // Test legacy model
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            var editDlg = ShowDialog<EditPeakScoringModelDlg>(reintegrateDlg.AddPeakScoringModel);
            RunUI(() =>
            {
                Assert.AreEqual(editDlg.PeakScoringModelName, "");
                editDlg.PeakScoringModelName = "legacy1"; // Not L10N
                editDlg.SelectedModelItem = LegacyScoringModel.DEFAULT_NAME;
                Assert.AreEqual(editDlg.PeakScoringModelName, "legacy1");
                editDlg.TrainModelClick();
                VerifyBias(editDlg, SCORES_AND_WEIGHTS[4]);
                VerifyCellValues(editDlg, SCORES_AND_WEIGHTS[5]);
                editDlg.UsesSecondBest = true;
                editDlg.UsesDecoys = false;
                editDlg.TrainModelClick();
                VerifyBias(editDlg, SCORES_AND_WEIGHTS[6]);
                VerifyCellValues(editDlg, SCORES_AND_WEIGHTS[7]);
                editDlg.UsesSecondBest = false;
                peakScoringModelBase = editDlg.PeakScoringModel as LegacyScoringModel;
            });

            //  Unchecking decoys and second best leads to error on training
            RunDlg<MessageDlg>(editDlg.TrainModelClick, messageDlg =>
            {
                Assert.AreEqual(string.Format(Resources.EditPeakScoringModelDlg_btnTrainModel_Click_Cannot_train_model_without_either_decoys_or_second_best_peaks_included_),
                                messageDlg.Message);
                messageDlg.OkDialog();
            });
            OkDialog(editDlg, editDlg.OkDialog);
            RunUI(() => reintegrateDlg.ComboPeakScoringModelSelected = "legacy1");
            OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
            RunUI(() =>
            {
                // Test modification of legacy scoring model
                SkylineWindow.SaveDocument();
                var peakScoringModel = SkylineWindow.DocumentUI.Settings.PeptideSettings.Integration.PeakScoringModel as LegacyScoringModel;
                var listModels = Settings.Default.PeakScoringModelList;
                Assert.AreEqual(listModels.Count, 4);
                var peakScoringModelList = listModels[3] as LegacyScoringModel;
                // Check that model in EditList, document model, and model from the dialog are all the same
                AssertEqualNotNull(new List<LegacyScoringModel> { peakScoringModel, peakScoringModelList, peakScoringModelBase });
                // Check document model is the last model we trained
                // ReSharper disable PossibleNullReferenceException
                Assert.AreEqual(peakScoringModel.Name, "legacy1");
                // ReSharper restore PossibleNullReferenceException
            });
            LegacyScoringModel peakScoringModelBaseNew = null;
            RunEditPeakScoringDlg("legacy1", editDlgTemp =>
            {
                editDlgTemp.UsesDecoys = true;
                editDlgTemp.TrainModelClick();
                peakScoringModelBaseNew = editDlgTemp.PeakScoringModel as LegacyScoringModel;
                editDlgTemp.OkDialog();
            });
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                var peakScoringModelNew = SkylineWindow.DocumentUI.Settings.PeptideSettings.Integration.PeakScoringModel as LegacyScoringModel;
                var listModels = Settings.Default.PeakScoringModelList;
                var peakScoringModelListNew = listModels[3] as LegacyScoringModel;
                // Check that model in EditList, document model, and model from the dialog are all the same
                AssertEqualNotNull(new List<LegacyScoringModel> { peakScoringModelNew, peakScoringModelListNew, peakScoringModelBaseNew });
                // Check document model has changed
                // ReSharper disable PossibleNullReferenceException
                Assert.IsTrue(peakScoringModelNew.UsesDecoys);
                // ReSharper restore PossibleNullReferenceException
            });

            // Test changing legacy to mProphet model without changing name
            MProphetPeakScoringModel peakScoringModelMProphetBase = null;
            RunEditPeakScoringDlg("legacy1", editDlgTemp =>
            {
                // Switch to mProphet model
                editDlgTemp.SelectedModelItem = "mProphet";
                editDlgTemp.TrainModelClick();
                peakScoringModelMProphetBase = editDlgTemp.PeakScoringModel as MProphetPeakScoringModel;
                editDlgTemp.OkDialog();
            }
            );
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                var peakScoringModelMProphet = SkylineWindow.DocumentUI.Settings.PeptideSettings.Integration.PeakScoringModel as MProphetPeakScoringModel;
                var listModels = Settings.Default.PeakScoringModelList;
                var peakScoringModelMProphetList = listModels[3] as MProphetPeakScoringModel;
                // Check that model in EditList, document model, and model from the dialog are all the same
                AssertEqualNotNull(new List<MProphetPeakScoringModel> { peakScoringModelMProphet, 
                                                                        peakScoringModelMProphetList, 
                                                                        peakScoringModelMProphetBase });
                // Check document model has changed
                Assert.AreNotEqual(peakScoringModelBaseNew, peakScoringModelMProphet);
                // ReSharper disable PossibleNullReferenceException
                Assert.IsTrue(peakScoringModelMProphet.UsesDecoys);
                // ReSharper restore PossibleNullReferenceException
                Assert.AreEqual(MProphetPeakScoringModel.GetDefaultCalculators(SkylineWindow.Document).Length,
                    peakScoringModelMProphet.PeakFeatureCalculators.Count);
            });
        }

        // Opens up a model that was trained on an earlier (fictitious) version of skyline with half of the values in the default list missing,
        // and also two calculators which are no longer in the default list
        protected void TestBackwardCompatibility()
        {
            RunEditPeakScoringDlg("backward_compatibility_test", editDlg =>
                {
                    VerifyCellValues(editDlg, SCORES_AND_WEIGHTS[8]);
                    // Unchecking a calculator which is common to both models carries over
                    editDlg.SetChecked(2, false);
                    editDlg.TrainModelClick();
                    VerifyCellValues(editDlg, SCORES_AND_WEIGHTS[9]);
                    // Check for behind-by-1 errors
                    editDlg.TrainModelClick();
                    VerifyCellValues(editDlg, SCORES_AND_WEIGHTS[9], 1.0, false);
                    editDlg.OkDialog();
                });
        }

        // Test that the dialog behaves correctly when opening a model 
        // that is incompatible with the dataset (some or all composite scores are NaN's)
        protected void TestIncompatibleDataSet()
        {
            // Define an incompatible model
            var weights = new[] {0.5322, -1.0352, /* double.NaN, */ double.NaN, 1.4744, 0.0430, 0.0477, -0.2740, double.NaN, 
                                 2.0096, 7.7726, -0.0566, 0.4751, 0.5, 0.5, double.NaN, double.NaN, 
                                 double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN};
            var parameters = new LinearModelParams(weights, -2.5);
            var incompatibleModel = new MProphetPeakScoringModel("incompatible", parameters, null, true);
            Settings.Default.PeakScoringModelList.Add(incompatibleModel);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsDlg =>
                {
                    peptideSettingsDlg.ComboPeakScoringModelSelected = "incompatible";
                    peptideSettingsDlg.OkDialog();
                });

            var reintegrateDlgIncompatible = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);

            var editList = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    reintegrateDlgIncompatible.EditPeakScoringModel);
            RunUI(() => editList.SelectItem("incompatible")); // Not L10N           
            
            RunDlg<EditPeakScoringModelDlg>(editList.EditItem, editDlgTemp =>
            {
                // All of the percentage fields should be null
                VerifyCellValues(editDlgTemp, SCORES_AND_WEIGHTS[10], 0.0);
                editDlgTemp.TrainModelClick();
                // Cell values go back to the standard trained model after we train and enable calculators, 
                // despite having been loaded with weird values
                editDlgTemp.SetChecked(2, true);
                editDlgTemp.TrainModelClick();
                VerifyCellValues(editDlgTemp, SCORES_AND_WEIGHTS[1], 1.0, false);
                editDlgTemp.CancelDialog();
            });
            OkDialog(editList, editList.OkDialog);
            // Trying to reintegrate gives an error because the model is incompatible
            RunDlg<MessageDlg>(reintegrateDlgIncompatible.OkDialog, messageDlg =>
            {
                Assert.AreEqual(TextUtil.LineSeparate(string.Format(Resources.ReintegrateDlg_OkDialog_Failed_attempting_to_reintegrate_peaks_),
                                                      Resources.ReintegrateDlg_OkDialog_The_current_peak_scoring_model_is_incompatible_with_one_or_more_peptides_in_the_document___Please_train_a_new_model_),
                                messageDlg.Message);
                messageDlg.OkDialog();
            });
            OkDialog(reintegrateDlgIncompatible, reintegrateDlgIncompatible.CancelDialog);
        }

        /// <summary>
        /// Tests the missing scores finder for finding peptides that lack a particular score
        /// </summary>
        protected void TestFindMissingScores()
        {
            RunEditPeakScoringDlg(null, editDlgTemp =>
            {
                // Find missing scores
                editDlgTemp.PeakScoringModelName = "missing_scores";
                editDlgTemp.TrainModelClick();
                editDlgTemp.PeakCalculatorsGrid.SelectRow(2);   // Retention times
                editDlgTemp.FindMissingValues(2);
                editDlgTemp.OkDialog();
            });
            var missingPeptides = new List<string> { "LGGNEQVTR", "IPVDSIYSPVLK", "YFNDGDIVEGTIVK", 
                                                     "DFDSLGTLR", "GGYAGMLVGSVGETVAQLAR", "GGYAGMLVGSVGETVAQLAR"};
            var isDecoys = new List<bool> { false, false, false, false, false, true };
            RunUI(() =>
                {
                    var findResultsForm = FormUtil.OpenForms.OfType<FindResultsForm>().FirstOrDefault();
                    Assert.IsNotNull(findResultsForm);
                    // There are 6 peptides for which scores are missing
                    Assert.AreEqual(6, findResultsForm.ItemCount);
                    for (int i = 0; i < 6; ++i)
                    {
                        findResultsForm.ActivateItem(i);
                        Assert.AreEqual(SkylineWindow.SelectedPeptideSequence, missingPeptides[i]);
                    }
                });
            for (int i = 0; i < 6; ++i)
            {
                RemovePeptide(missingPeptides[i], isDecoys[i]);
            }
            RunEditPeakScoringDlg("missing_scores", editDlgTemp =>
            {
                // No missing values for these scores any more
                Assert.IsTrue(editDlgTemp.IsActiveCell(2, 0));
                Assert.IsTrue(editDlgTemp.IsActiveCell(8, 0));
                Assert.IsTrue(editDlgTemp.IsActiveCell(9, 0));
                Assert.IsTrue(editDlgTemp.IsActiveCell(10, 0));
               
                // But they aren't automatically enabled
                Assert.IsFalse(editDlgTemp.PeakCalculatorsGrid.Items[2].IsEnabled);
                Assert.IsFalse(editDlgTemp.PeakCalculatorsGrid.Items[8].IsEnabled);
                Assert.IsFalse(editDlgTemp.PeakCalculatorsGrid.Items[9].IsEnabled);
                Assert.IsFalse(editDlgTemp.PeakCalculatorsGrid.Items[10].IsEnabled);
                editDlgTemp.SetChecked(10, true);
                editDlgTemp.TrainModelClick();
                editDlgTemp.OkDialog();
            });
        }

        // Check that the items in the list are all equal and not null
        protected void AssertEqualNotNull(IList list)
        {
            if (list.Count == 0)
                return;
            var firstItem = list[0];
            foreach (var item in list)
            {
                Assert.IsNotNull(item);
                Assert.AreEqual(firstItem, item);
            }
        }

        // Conveniently opens/closes all the intermediate dialogs to open and run a EditPeakScoringModelDlg 
        protected static void RunEditPeakScoringDlg(string editName, Action<EditPeakScoringModelDlg> act)
        {
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);

            if (editName != null)
            {
                var editList = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    reintegrateDlg.EditPeakScoringModel);
                RunUI(() => editList.SelectItem(editName)); // Not L10N
                RunDlg(editList.EditItem, act);
                OkDialog(editList, editList.OkDialog);
            }
            else
            {
                RunDlg(reintegrateDlg.AddPeakScoringModel, act);
            }
            OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
        }

        private void VerifyCellValues(EditPeakScoringModelDlg editDlg, string expectedValueString, double sumWeights = 1.0, bool isRecording = true)
        {
            // Parse the expected values
            var expectedFields = expectedValueString.Split('|', ';');
            expectedFields = expectedFields.Take(expectedFields.Length - 1).ToArray();
            Assert.AreEqual(expectedFields.Length % 3, 0);
            int numRows = expectedFields.Length / 3;
            // Verify expected number of rows.
            if (IsRecordMode)
                numRows = editDlg.PeakCalculatorsGrid.RowCount;
            Assert.AreEqual(editDlg.PeakCalculatorsGrid.RowCount, numRows);
            // Verify normalized weights add to 1
            double sumNormWeights = 0;
            var readModeSb = new StringBuilder();
            int fieldNum = 0;
            string expectedValue = null;
            for (int row = 0; row < numRows; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    if(col != 1)
                        expectedValue = expectedFields[fieldNum++];

                    // Verify cell value.
                    var actualValue = editDlg.PeakCalculatorsGrid.GetCellValue(col, row);
                    if (IsRecordMode && isRecording)
                    {
                        if (col == 0)
                        {
                            readModeSb.Append(actualValue == null ? "" : actualValue + '|');
                        }
                        if (col == 2)
                        {
                            readModeSb.Append((string.IsNullOrEmpty(actualValue) ? "" : double.Parse(actualValue).ToString(editDlg.PeakCalculatorWeightFormat)) + '|');
                        }
                        if (col == 3)
                        {
                            readModeSb.Append((string.IsNullOrEmpty(actualValue) ? "" : double.Parse(actualValue).ToString(editDlg.PeakCalculatorPercentContributionFormat)) + ";");
                        }
                    }
                    if (col == 2  && !string.IsNullOrEmpty(actualValue))
                        actualValue = double.Parse(actualValue).ToString(editDlg.PeakCalculatorWeightFormat);
                    if (col == 3 && !string.IsNullOrEmpty(actualValue))
                    {
                        sumNormWeights += double.Parse(actualValue);
                        actualValue = double.Parse(actualValue).ToString(editDlg.PeakCalculatorPercentContributionFormat);
                    }
                    if(!IsRecordMode && col != 1)
                    {
                        // Normalize decimal separator
                        if (actualValue != null)
                        {
                            actualValue = actualValue.Replace(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator,
                                  CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);    
                        }
                        Assert.AreEqual(expectedValue, actualValue);
                    }
                }
            }
            if(IsRecordMode && isRecording)
                Console.WriteLine(@"""{0}"",", readModeSb);
            Assert.AreEqual(sumNormWeights, sumWeights, 0.005);
        }

        private void VerifyBias(EditPeakScoringModelDlg editDlg, string bias, bool isRecording = true)
        {
            if (IsRecordMode)
            {
                if(isRecording)
                    Console.WriteLine(@"""{0}"",", editDlg.PeakScoringModel.Parameters.Bias.ToString(CultureInfo.CurrentCulture)); 
            }
            else
            {
                double biasNum;
                Assert.IsTrue(double.TryParse(bias, NumberStyles.Float, CultureInfo.InvariantCulture, out biasNum));
                Assert.AreEqual(editDlg.PeakScoringModel.Parameters.Bias, biasNum, 1e-5);
            }
        }
    }
}
