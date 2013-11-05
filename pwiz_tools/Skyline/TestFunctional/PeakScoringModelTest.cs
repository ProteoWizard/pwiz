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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakScoringModelTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakScoringModel()
        {
            TestFilesZip = @"TestFunctional\PeakScoringModelTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            Settings.Default.PeakScoringModelList.Clear();

            var documentFile = TestFilesDir.GetTestPath("MProphetGold-rescore2.sky");
            RunUI(() => SkylineWindow.OpenFile(documentFile));
            WaitForDocumentLoaded();

            TestDialog();
            TestModelChangesAndSave();
            TestBackwardCompatibility();
            TestIncompatibleDataSet();
        }

        protected void TestDialog()
        {

            // Display integration tab.
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsDlg.SelectedTab = PeptideSettingsUI.TABS.Integration;
            });

            var calculators = (new MProphetPeakScoringModel("dummy")).PeakFeatureCalculators.ToArray();     // Not L10N
            {
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(peptideSettingsDlg.AddPeakScoringModel);

                // Check default values.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.PeakScoringModelName, string.Empty);
                        var rows = editDlg.PeakCalculatorsGrid.RowCount;
                        Assert.AreEqual(calculators.Length, rows, "Unexpected count of peak calculators"); // Not L10N
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
                        Assert.AreEqual(editDlg.PeakScoringModel.Parameters.Bias, -15.704713, 1e-5);
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            var editList =
                ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    peptideSettingsDlg.EditPeakScoringModel);

            RunUI(() => editList.SelectItem("test1")); // Not L10N

            {
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(editList.EditItem);
                var format = editDlg.PeakCalculatorWeightFormat;
                var percentFormat = editDlg.PeakCalculatorPercentContributionFormat;
                var cellValuesOriginal = new[]
                    {
                        new[] {"True", calculators[0].Name, (0.4886).ToString(format), (0.1710).ToString(percentFormat)},
                        new[] {"False", calculators[1].Name, string.Empty, string.Empty},
                        new[] {"True", calculators[2].Name, (2.6711).ToString(format), (0.0408).ToString(percentFormat)},
                        new[] {"True", calculators[3].Name, (0.4533).ToString(format), (0.0791).ToString(percentFormat)},
                        new[] {"True", calculators[4].Name, (1.5071).ToString(format), (0.0795).ToString(percentFormat)},
                        new[] {"True", calculators[5].Name, (-0.2925).ToString(format), (0.1850).ToString(percentFormat)},
                        new[] {"False", calculators[6].Name, string.Empty, string.Empty},  
                        new[] {"False", calculators[7].Name, string.Empty, string.Empty},
                        new[] {"False", calculators[8].Name, string.Empty, string.Empty},                      
                        new[] {"True", calculators[9].Name, (4.2907).ToString(format), (0.1145).ToString(percentFormat)},
                        new[] {"True", calculators[10].Name, (10.0902).ToString(format), (0.5022).ToString(percentFormat)},
                        new[] {"True", calculators[11].Name, (0.2666).ToString(format), (-0.2189).ToString(percentFormat)},
                        new[] {"True", calculators[12].Name, (0.1933).ToString(format), (0.0467).ToString(percentFormat)},
                        new[] {"False", calculators[13].Name, string.Empty, string.Empty},
                        new[] {"False", calculators[14].Name, string.Empty, string.Empty},
                        new[] {"False", calculators[15].Name, string.Empty, string.Empty},
                    };
                var cellValuesNew = new[]
                    {
                        new[] {"True", calculators[0].Name, (0.5569).ToString(format), (0.2111).ToString(percentFormat)},
                        new[] {"False", calculators[1].Name, string.Empty, string.Empty},
                        new[] {"False", calculators[2].Name, string.Empty, string.Empty},
                        new[] {"False", calculators[3].Name, string.Empty, string.Empty},
                        new[] {"True", calculators[4].Name, (2.7995).ToString(format), (0.1416).ToString(percentFormat)},
                        new[] {"True", calculators[5].Name, (-0.5237).ToString(format), (0.2518).ToString(percentFormat)},
                        new[] {"False", calculators[6].Name, string.Empty, string.Empty},
                        new[] {"False", calculators[7].Name, string.Empty, string.Empty},
                        new[] {"False", calculators[8].Name, string.Empty, string.Empty},
                        new[] {"True", calculators[9].Name, (4.9975).ToString(format), (0.1395).ToString(percentFormat)},
                        new[] {"True", calculators[10].Name, (9.0669).ToString(format), (0.4515).ToString(percentFormat)},
                        new[] {"True", calculators[11].Name, (0.3698).ToString(format), (-0.2679).ToString(percentFormat)},
                        new[] {"True", calculators[12].Name, (0.2942).ToString(format), (0.0723).ToString(percentFormat)},
                        new[] {"False", calculators[13].Name, string.Empty, string.Empty},
                        new[] {"False", calculators[14].Name, string.Empty, string.Empty},
                        new[] {"False", calculators[15].Name, string.Empty, string.Empty},
                    };
                // Verify weights, change name.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.PeakScoringModelName, "test1"); // Not L10N
                        VerifyCellValues(editDlg, cellValuesOriginal);
                        Assert.AreEqual(editDlg.PeakScoringModel.Parameters.Bias, -15.704713, 1e-5);
                        // Manually uncheck two of the scores
                        editDlg.SetChecked(2, false);
                        editDlg.SetChecked(3, false);
                        editDlg.TrainModelClick();
                        VerifyCellValues(editDlg, cellValuesNew);
                        Assert.AreEqual(editDlg.PeakScoringModel.Parameters.Bias, -13.727219, 1e-5);
                        // Re-check the scores, show that model goes back to normal
                        editDlg.SetChecked(2, true);
                        editDlg.SetChecked(3, true);
                        editDlg.TrainModelClick();
                        VerifyCellValues(editDlg, cellValuesOriginal);
                        Assert.AreEqual(editDlg.PeakScoringModel.Parameters.Bias, -15.704713, 1e-5);
                        editDlg.PeakScoringModelName = "test2"; // Not L10N
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
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
                RunUI(() => editDlg.CancelButton.PerformClick());
                WaitForClosedForm(editDlg);
                OkDialog(editList, editList.OkDialog);
                OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);
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
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsDlg.SelectedTab = PeptideSettingsUI.TABS.Integration);
            var editDlg = ShowDialog<EditPeakScoringModelDlg>(peptideSettingsDlg.AddPeakScoringModel);

            var calculatorsLegacy = (new LegacyScoringModel("dummy")).PeakFeatureCalculators.ToArray();
            var format = editDlg.PeakCalculatorWeightFormat;
            var percentFormat = editDlg.PeakCalculatorPercentContributionFormat;
            var cellValuesLegacy = new[]
                {
                    new[] {"True", calculatorsLegacy[0].Name, (0.5954).ToString(format), (0.5708).ToString(percentFormat)},
                    new[] {"True", calculatorsLegacy[1].Name, (0.5954).ToString(format), (0.2602).ToString(percentFormat)},
                    new[] {"True", calculatorsLegacy[2].Name, (0.7144).ToString(format), (0.1690).ToString(percentFormat)},
                    new[] {"True", calculatorsLegacy[3].Name, (11.9072).ToString(format), (0.0000).ToString(percentFormat)},
                };
            var cellValuesLegacyNew = new[]
                {
                    new[] {"True", calculatorsLegacy[0].Name, (0.5716).ToString(format), (0.6172).ToString(percentFormat)},
                    new[] {"True", calculatorsLegacy[1].Name, (0.5716).ToString(format), (0.2237).ToString(percentFormat)},
                    new[] {"True", calculatorsLegacy[2].Name, (0.6859).ToString(format), (0.1591).ToString(percentFormat)},
                    new[] {"True", calculatorsLegacy[3].Name, (11.4310).ToString(format), (0.000).ToString(percentFormat)},
                };

            RunUI(() =>
            {
                Assert.AreEqual(editDlg.PeakScoringModelName, "");
                editDlg.PeakScoringModelName = "legacy1"; // Not L10N
                editDlg.SelectedModelItem = "Skyline Legacy";
                Assert.AreEqual(editDlg.PeakScoringModelName, "legacy1");
                editDlg.TrainModelClick();
                Assert.AreEqual(editDlg.PeakScoringModel.Parameters.Bias, -6.603204, 1e-5);
                VerifyCellValues(editDlg, cellValuesLegacy);
                editDlg.UsesSecondBest = true;
                editDlg.UsesDecoys = false;
                editDlg.TrainModelClick();
                Assert.AreEqual(editDlg.PeakScoringModel.Parameters.Bias, -7.175833, 1e-5);
                VerifyCellValues(editDlg, cellValuesLegacyNew);
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
            RunUI(() => peptideSettingsDlg.ComboPeakScoringModelSelected = "legacy1");
            OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);
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
                Assert.AreEqual(peakScoringModelMProphet.PeakFeatureCalculators.Count, 16);
            });
        }

        // Opens up a model that was trained on an earlier (fictitious) version of skyline with half of the values in the default list missing,
        // and also two calculators which are no longer in the default list
        protected void TestBackwardCompatibility()
        {
            RunEditPeakScoringDlg("backward_compatibility_test", editDlg =>
                {
                    var format = editDlg.PeakCalculatorWeightFormat;
                    var percentFormat = editDlg.PeakCalculatorPercentContributionFormat;
                    var calculatorsMprophet = (new MProphetPeakScoringModel("dummy")).PeakFeatureCalculators.ToArray();
                    var cellValuesOld = new[]
                    {
                        new[] {"True", new MQuestIntensityCalc().Name, (0.8633).ToString(format), (0.3150).ToString(percentFormat)},
                        new[] {"True", new MQuestIntensityCorrelationCalc().Name, (2.0177).ToString(format), (0.0350).ToString(percentFormat)},
                        new[] {"True", new MQuestWeightedShapeCalc().Name, (6.2170).ToString(format), (0.2740).ToString(percentFormat)},
                        new[] {"False", new NextGenSignalNoiseCalc().Name, string.Empty, string.Empty},
                        new[] {"False", new NextGenProductMassErrorCalc().Name, string.Empty, string.Empty},
                        new[] {"False", new LegacyIdentifiedCountCalc().Name, string.Empty, string.Empty},
                        new[] {"True", new MQuestWeightedReferenceShapeCalc().Name, (7.5352).ToString(format), (0.3170).ToString(percentFormat)},
                        new[] {"True", new MQuestWeightedReferenceCoElutionCalc().Name, (-0.1277).ToString(format), (0.0580).ToString(percentFormat)},
                        new[] {"False", new MQuestShapeCalc().Name, string.Empty, string.Empty},
                        new[] {"False", new MQuestCoElutionCalc().Name, string.Empty, string.Empty},
                    };
                    var cellValuesNew = new[]
                    {
                        new[] {"True", calculatorsMprophet[0].Name, (0.4690).ToString(format), (0.1680).ToString(percentFormat)},
                        new[] {"False", calculatorsMprophet[1].Name, string.Empty, string.Empty},
                        new[] {"True", calculatorsMprophet[2].Name, (3.1637).ToString(format), (0.0500).ToString(percentFormat)},
                        new[] {"True", calculatorsMprophet[3].Name, (0.3440).ToString(format), (0.0730).ToString(percentFormat)},
                        new[] {"False", calculatorsMprophet[4].Name,  string.Empty, string.Empty},
                        new[] {"True", calculatorsMprophet[5].Name, (-0.3664).ToString(format), (0.2210).ToString(percentFormat)},
                        new[] {"False", calculatorsMprophet[6].Name, string.Empty, string.Empty},
                        new[] {"False", calculatorsMprophet[7].Name, string.Empty, string.Empty},
                        new[] {"False", calculatorsMprophet[8].Name, string.Empty, string.Empty},
                        new[] {"True", calculatorsMprophet[9].Name, (4.8301).ToString(format), (0.1240).ToString(percentFormat)},
                        new[] {"True", calculatorsMprophet[10].Name, (10.2590).ToString(format), (0.5080).ToString(percentFormat)},
                        new[] {"True", calculatorsMprophet[11].Name, (0.2756).ToString(format), (-0.2200).ToString(percentFormat)},
                        new[] {"True", calculatorsMprophet[12].Name, (0.3640).ToString(format), (0.0760).ToString(percentFormat)},
                        new[] {"False", calculatorsMprophet[13].Name, string.Empty, string.Empty},
                        new[] {"False", calculatorsMprophet[14].Name, string.Empty, string.Empty},
                        new[] {"False", calculatorsMprophet[15].Name, string.Empty, string.Empty},
                    };
                    VerifyCellValues(editDlg, cellValuesOld);
                    // Unchecking a calculator which is common to both models carries over
                    editDlg.SetChecked(2, false);
                    editDlg.TrainModelClick();
                    VerifyCellValues(editDlg, cellValuesNew);
                    // Check for behind-by-1 errors
                    editDlg.TrainModelClick();
                    VerifyCellValues(editDlg, cellValuesNew);
                    editDlg.OkDialog();
                });
        }

        // Test that the dialog behaves correctly when opening a model that is incompatible with the dataset (composite scores all NaN's)
        protected void TestIncompatibleDataSet()
        {
            
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
            // Display integration tab.
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsDlg.SelectedTab = PeptideSettingsUI.TABS.Integration;
            });

            if (editName != null)
            {
                var editList = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    peptideSettingsDlg.EditPeakScoringModel);
                RunUI(() => editList.SelectItem(editName)); // Not L10N
                RunDlg(editList.EditItem, act);
                OkDialog(editList, editList.OkDialog);
            }
            else
            {
                RunDlg(peptideSettingsDlg.AddPeakScoringModel, act);
            }
            OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);
        }

        private static void VerifyCellValues(EditPeakScoringModelDlg editDlg, string[][] expectedValues)
        {
            // Verify expected number of rows.
            Assert.AreEqual(editDlg.PeakCalculatorsGrid.RowCount, expectedValues.Length);
            // Verify normalized weights add to 1
            double sumNormWeights = 0;
            for (int row = 0; row < expectedValues.Length; row++)
            {
                // Verify expected number of columns.
                Assert.AreEqual(4, expectedValues[row].Length);

                for (int col = 0; col < expectedValues[row].Length; col++)
                {
                    var expectedValue = expectedValues[row][col];
                    if (expectedValue == null)
                        continue;

                    // Verify cell value.
                    var actualValue = editDlg.PeakCalculatorsGrid.GetCellValue(col, row);
                    if (col == 2  && !string.IsNullOrEmpty(actualValue))
                        actualValue = double.Parse(actualValue).ToString(editDlg.PeakCalculatorWeightFormat);
                    if (col == 3 && !string.IsNullOrEmpty(actualValue))
                    {
                        sumNormWeights += double.Parse(actualValue);
                        actualValue = double.Parse(actualValue).ToString(editDlg.PeakCalculatorPercentContributionFormat);
                    }
                    Assert.AreEqual(expectedValue, actualValue);
                }
            }
            Assert.AreEqual(sumNormWeights, 1.0, 0.005);
        }
    }
}
