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
            var documentFile = TestFilesDir.GetTestPath("MProphetGold-rescore2.sky");
            RunUI(() => SkylineWindow.OpenFile(documentFile));
            WaitForDocumentLoaded();

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
                    Assert.AreEqual(calculators.Length, rows, "Unexpected count of peak calculators");  // Not L10N
                });

                // Test empty name.
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty, messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });

                RunUI(() => editDlg.PeakScoringModelName = "test1"); // Not L10N

                // Create a model with default values.
                RunUI(() =>
                {
                    editDlg.TrainModel();
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
                    editDlg.TrainModel(true);
                    VerifyCellValues(editDlg, cellValuesNew);
                    Assert.AreEqual(editDlg.PeakScoringModel.Parameters.Bias, -13.727219, 1e-5);
                    // Re-check the scores, show that model goes back to normal
                    editDlg.SetChecked(2, true);
                    editDlg.SetChecked(3, true);
                    editDlg.TrainModel(true);
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
            }

            OkDialog(editList, editList.OkDialog);
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
