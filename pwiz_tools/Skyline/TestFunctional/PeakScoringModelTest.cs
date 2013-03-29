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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakScoringModelTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakScoringModel()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Display full scan tab.
            var fullScanDlg = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                fullScanDlg.SelectedTab = TransitionSettingsUI.TABS.Peaks;
            });

            int calculatorCount = PeakFeatureCalculator.Calculators.Count();
            {
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(fullScanDlg.AddPeakScoringModel);

                // Check default values.
                RunUI(() =>
                {
                    Assert.AreEqual(editDlg.PeakScoringModelName, string.Empty);
                    Assert.AreEqual(editDlg.Mean, string.Empty);
                    Assert.AreEqual(editDlg.Stdev, string.Empty);
                    var rows = editDlg.PeakCalculatorsGrid.RowCount;
                    Assert.AreEqual(calculatorCount, rows, "Unexpected count of peak calculators");  // Not L10N
                    for (int i = 0; i < rows; i++)
                    {
                        var cellValue = editDlg.PeakCalculatorsGrid.GetCellValue(1, i);
                        Assert.AreEqual(string.Empty, cellValue);
                    }
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
                    editDlg.Stdev = "2"; // Not L10N
                });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });

                // Test empty stdev.
                RunUI(() =>
                {
                    editDlg.Mean = "1"; // Not L10N
                    editDlg.Stdev = string.Empty;
                });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });

                // Create a model with default values.
                RunUI(() =>
                {
                    editDlg.Mean = "1";
                    editDlg.Stdev = "2";
                    editDlg.OkDialog();
                });
                WaitForClosedForm(editDlg);
            }

            var editList =
                ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    fullScanDlg.EditPeakScoringModel);

            RunUI(() => editList.SelectItem("test1")); // Not L10N
            {
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(editList.EditItem);

                // Edit weights, change name.
                RunUI(() =>
                {
                    Assert.AreEqual(editDlg.PeakScoringModelName, "test1"); // Not L10N
                    Assert.AreEqual("1", editDlg.Mean); // Not L10N
                    Assert.AreEqual("2", editDlg.Stdev); // Not L10N
                    var rows = editDlg.PeakCalculatorsGrid.RowCount;
                    Assert.AreEqual(calculatorCount, rows, "Unexpected count of peak calculators");  // Not L10N
                    for (int i = 0; i < rows; i++)
                    {
                        var cellValue = editDlg.PeakCalculatorsGrid.GetCellValue(1, i);
                        Assert.AreEqual("0", cellValue); // Not L10N
                        editDlg.PeakCalculatorsGrid.SelectCell(1, i);
                        editDlg.PeakCalculatorsGrid.SetCellValue(i.ToString(CultureInfo.CurrentCulture));
                    }
                    editDlg.PeakScoringModelName = "test2"; // Not L10N
                    editDlg.OkDialog();
                });
                WaitForClosedForm(editDlg);
            }

            var listValues = new List<string>();
            RunUI(() => editList.SelectItem("test2")); // Not L10N
            {
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(editList.EditItem);

                // Verify saved weights.
                RunUI(() =>
                {
                    Assert.AreEqual(editDlg.PeakScoringModelName, "test2"); // Not L10N
                    Assert.AreEqual("1", editDlg.Mean); // Not L10N
                    Assert.AreEqual("2", editDlg.Stdev); // Not L10N
                    var rows = editDlg.PeakCalculatorsGrid.RowCount;
                    Assert.AreEqual(calculatorCount, rows, "Unexpected count of peak calculators");  // Not L10N
                    for (int i = 0; i < rows; i++)
                    {
                        var cellValue = editDlg.PeakCalculatorsGrid.GetCellValue(1, i);
                        string expectedValue = i.ToString(CultureInfo.CurrentCulture);
                        Assert.AreEqual(expectedValue, cellValue); // Not L10N
                        listValues.Add(expectedValue);
                    }
                    editDlg.OkDialog();
                });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2")); // Not L10N
            {
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(editList.EditItem);

                // Paste one number.
                var pasteValue = 173.6789.ToString(CultureInfo.CurrentCulture);
                ClipboardEx.SetText(pasteValue);
                listValues[0] = pasteValue;
                RunUI(() =>
                {
                    editDlg.PeakCalculatorsGrid.SelectCell(1, 0);
                    Assert.IsTrue(editDlg.PeakCalculatorsGrid.HandleKeyDown(Keys.V, true));
                    VerifyCellValues(editDlg, listValues.ToArray()); // Not L10N
                });

                // Paste weights only list.
                listValues.Clear();
                for (int i = 0; i < calculatorCount; i++)
                {
                    listValues.Insert(0, i.ToString(CultureInfo.CurrentCulture));
                }
                ClipboardEx.SetText(string.Join("\n", listValues)); // Not L10N
                RunUI(() =>
                {
                    editDlg.PeakCalculatorsGrid.SelectCell(1, 0);
                    editDlg.PeakCalculatorsGrid.OnPaste();
                    VerifyCellValues(editDlg, listValues.ToArray()); // Not L10N
                });

                // Paste weights list by name.
                var arrayCalc = PeakFeatureCalculator.Calculators.ToArray();
                ClipboardEx.SetText("Legacy unforced count\t-1\nmQuest weighted reference\t-99\n"); // Not L10N
                listValues[arrayCalc.IndexOf(c => string.Equals(c.Name, "Legacy unforced count"))] = "-1";
                listValues[arrayCalc.IndexOf(c => string.Equals(c.Name, "mQuest weighted reference"))] = "-99";
                RunUI(() =>
                {
                    editDlg.PeakCalculatorsGrid.SelectCell(1, 0);
                    editDlg.PeakCalculatorsGrid.OnPaste();
                    VerifyCellValues(editDlg, listValues.ToArray()); // Not L10N
                });

                // Paste unknown name.
                ClipboardEx.SetText("UNKNOWN NAME\t-1\n"); // Not L10N
                RunDlg<MessageDlg>(() =>
                {
                    editDlg.PeakCalculatorsGrid.SelectCell(1, 0);
                    editDlg.PeakCalculatorsGrid.OnPaste();
                },
                messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.PeakCalculatorWeight_Validate___0___is_not_a_known_name_for_a_peak_feature_calculator, messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });

                // Paste too many columns.
                ClipboardEx.SetText("1\t2\t3\n"); // Not L10N
                RunDlg<MessageDlg>(() =>
                {
                    editDlg.PeakCalculatorsGrid.SelectCell(1, 0);
                    editDlg.PeakCalculatorsGrid.OnPaste();
                },
                messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.SettingsUIUtil_DoPasteText_Incorrect_number_of_columns__0__found_on_line__1__, messageDlg.Message, 2);
                    messageDlg.OkDialog();
                });

                // Paste single bad value.
                ClipboardEx.SetText("x"); // Not L10N
                RunUI(() =>
                {
                    editDlg.PeakCalculatorsGrid.SelectCell(1, 0);
                    editDlg.PeakCalculatorsGrid.OnPaste();
                    VerifyCellValues(editDlg, listValues.ToArray()); // Not L10N
                });

                // Paste bad weight by name.
                ClipboardEx.SetText("Legacy unforced count\tx\n"); // Not L10N
                RunUI(() =>
                {
                    editDlg.PeakCalculatorsGrid.SelectCell(1, 0);
                    editDlg.PeakCalculatorsGrid.OnPaste();
                    VerifyCellValues(editDlg, listValues.ToArray()); // Not L10N
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
                RunUI(() => editDlg.CancelButton.PerformClick());
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2")); // Not L10N
            {
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(editList.EditItem);

                // Test buttons.
                RunUI(() =>
                {
                    editDlg.AddResults();
                    editDlg.ShowGraph();
                    editDlg.OkDialog();
                });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2")); // Not L10N
            {
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(editList.EditItem);

                // Test non-numeric input in grid cell.
                RunDlg<MessageDlg>(() =>
                {
                    editDlg.PeakCalculatorsGrid.SelectCell(1, 0);
                    editDlg.PeakCalculatorsGrid.SetCellValue("x");
                },
                    messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.GridViewDriver_GridView_DataError__0__must_be_a_valid_number, messageDlg.Message, 1);
                        messageDlg.OkDialog();
                    });
                OkDialog(editDlg, editDlg.OkDialog);
            }

            OkDialog(editList, editList.OkDialog);
            OkDialog(fullScanDlg, fullScanDlg.OkDialog);
        }

        private static void VerifyCellValues(EditPeakScoringModelDlg editDlg, string[] expectedValues)
        {
            var array = new string[expectedValues.Length][];
            for (int i = 0; i < expectedValues.Length; i++)
                array[i] = new[] {null, expectedValues[i]};
            VerifyCellValues(editDlg, array);
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
                    Assert.AreEqual(expectedValue, actualValue);
                }
            }
        }
    }
}
