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
                    Assert.AreEqual(1.376, editDlg.Mean);
                    Assert.AreEqual(0.088, editDlg.Stdev);
                    var rows = editDlg.PeakCalculatorsGrid.RowCount;
                    Assert.AreEqual(calculators.Length, rows, "Unexpected count of peak calculators");  // Not L10N
                });

                // Test empty name.
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty, messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });

                // Test empty mean.
                RunUI(() =>
                {
                    editDlg.PeakScoringModelName = "test1"; // Not L10N
                    editDlg.Mean = null;
                });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });

                // Test empty stdev.
                RunUI(() =>
                {
                    editDlg.Mean = 1;
                    editDlg.Stdev = null;
                });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });

                // Create a model with default values.
                RunUI(() =>
                {
                    editDlg.Mean = 1;
                    editDlg.Stdev = 2;
                    editDlg.TrainModel();
                    Assert.AreEqual(1.376, editDlg.Mean);
                    Assert.AreEqual(0.088, editDlg.Stdev);
                    for (int i = 0; i < editDlg.PeakCalculatorsGrid.RowCount; i++)
                    {
                        editDlg.PeakCalculatorsGrid.SelectRow(i);
                    }
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

                // Verify weights, change name.
                RunUI(() =>
                {
                    Assert.AreEqual(editDlg.PeakScoringModelName, "test1"); // Not L10N
                    Assert.AreEqual(1.376, editDlg.Mean);
                    Assert.AreEqual(0.088, editDlg.Stdev);
                    var format = editDlg.PeakCalculatorWeightFormat;
                    VerifyCellValues(editDlg, new[]
                        {
                            new[] {calculators[0].Name, (0.0428).ToString(format)},
                            new[] {calculators[1].Name, string.Empty},
                            new[] {calculators[2].Name, (0.2340).ToString(format)},
                            new[] {calculators[3].Name, (0.3759).ToString(format)},
                            new[] {calculators[4].Name, (0.1320).ToString(format)},
                            new[] {calculators[5].Name, (-0.0256).ToString(format)},
                            new[] {calculators[6].Name, (0.8841).ToString(format)},
                            new[] {calculators[7].Name, (0.0234).ToString(format)},
                            new[] {calculators[8].Name, (0.0397).ToString(format)},
                            new[] {calculators[9].Name, (0.0169).ToString(format)},
                            new[] {calculators[10].Name, string.Empty},
                        });
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

            for (int row = 0; row < expectedValues.Length; row++)
            {
                // Verify expected number of columns.
                Assert.AreEqual(2, expectedValues[row].Length);

                for (int col = 0; col < expectedValues[row].Length; col++)
                {
                    var expectedValue = expectedValues[row][col];
                    if (expectedValue == null)
                        continue;

                    // Verify cell value.
                    var actualValue = editDlg.PeakCalculatorsGrid.GetCellValue(col, row);
                    if (col == 1 && !string.IsNullOrEmpty(actualValue))
                        actualValue = double.Parse(actualValue).ToString(editDlg.PeakCalculatorWeightFormat);
                    Assert.AreEqual(expectedValue, actualValue);
                }
            }
        }
    }
}
