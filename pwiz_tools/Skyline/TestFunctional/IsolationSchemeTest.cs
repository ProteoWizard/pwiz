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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class IsolationSchemeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestIsolationScheme()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Display full scan tab.
            var fullScanDlg = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
                {
                    fullScanDlg.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                    fullScanDlg.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                });

            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(fullScanDlg.AddIsolationScheme);

                // Test empty name.
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty, messageDlg.Message, 1);
                        messageDlg.OkDialog();
                    });

                // Create a scheme with default values.
                RunUI(() =>
                    {
                        Assert.AreEqual(string.Empty, editDlg.IsolationSchemeName);
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsFalse(editDlg.AsymmetricFilter);
                        Assert.AreEqual(2, editDlg.PrecursorFilter);
                        Assert.AreEqual(null, editDlg.PrecursorRightFilter);
                        editDlg.IsolationSchemeName = "test1"; // Not L10N
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            var editList =
                ShowDialog<EditListDlg<SettingsListBase<IsolationScheme>, IsolationScheme>>(
                    fullScanDlg.EditIsolationScheme);

            {
                // Add conflicting name.
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.AddItem);
                RunUI(() =>
                    {
                        Assert.AreEqual(string.Empty, editDlg.IsolationSchemeName);
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsFalse(editDlg.AsymmetricFilter);
                        Assert.AreEqual(2, editDlg.PrecursorFilter);
                        Assert.AreEqual(null, editDlg.PrecursorRightFilter);
                        editDlg.IsolationSchemeName = "test1"; // Not L10N
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.EditIsolationSchemeDlg_OkDialog_The_isolation_scheme_named__0__already_exists, messageDlg.Message, 1);
                        messageDlg.OkDialog();
                    });
                RunUI(() => editDlg.CancelButton.PerformClick());
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test1")); // Not L10N
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Edit scheme, change name and isolation width.
                RunUI(() =>
                    {
                        Assert.AreEqual("test1", editDlg.IsolationSchemeName); // Not L10N
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsFalse(editDlg.AsymmetricFilter);
                        Assert.AreEqual(2, editDlg.PrecursorFilter);
                        Assert.AreEqual(null, editDlg.PrecursorRightFilter);
                        editDlg.IsolationSchemeName = "test2"; // Not L10N
                        editDlg.PrecursorFilter = 50;
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2")); // Not L10N
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Test asymmetric isolation width (automatic split).
                RunUI(() =>
                    {
                        editDlg.CurrentWindowType = EditIsolationSchemeDlg.WindowType.EXTRACTION;
                        Assert.AreEqual("test2", editDlg.IsolationSchemeName); // Not L10N
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsFalse(editDlg.AsymmetricFilter);
                        Assert.AreEqual(50, editDlg.PrecursorFilter);
                        Assert.AreEqual(null, editDlg.PrecursorRightFilter);
                        editDlg.AsymmetricFilter = true;
                        Assert.AreEqual(25, editDlg.PrecursorFilter);
                        Assert.AreEqual(25, editDlg.PrecursorRightFilter);
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2")); // Not L10N
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Test asymmetric isolation width (manually set).
                RunUI(() =>
                    {
                        editDlg.CurrentWindowType = EditIsolationSchemeDlg.WindowType.EXTRACTION;
                        Assert.AreEqual("test2", editDlg.IsolationSchemeName); // Not L10N
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsTrue(editDlg.AsymmetricFilter);
                        Assert.AreEqual(25, editDlg.PrecursorFilter);
                        Assert.AreEqual(25, editDlg.PrecursorRightFilter);
                        editDlg.PrecursorFilter = 1;
                        editDlg.PrecursorRightFilter = 2;
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2")); // Not L10N
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Test return to symmetric isolation width.
                RunUI(() =>
                    {
                        editDlg.CurrentWindowType = EditIsolationSchemeDlg.WindowType.EXTRACTION;
                        Assert.AreEqual("test2", editDlg.IsolationSchemeName); // Not L10N
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsTrue(editDlg.AsymmetricFilter);
                        Assert.AreEqual(1, editDlg.PrecursorFilter);
                        Assert.AreEqual(2, editDlg.PrecursorRightFilter);
                        editDlg.AsymmetricFilter = false;
                        Assert.AreEqual(3, editDlg.PrecursorFilter);
                        Assert.AreEqual(null, editDlg.PrecursorRightFilter);
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2")); // Not L10N
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Test return to symmetric isolation width with only left width specified.
                RunUI(() =>
                    {
                        editDlg.CurrentWindowType = EditIsolationSchemeDlg.WindowType.EXTRACTION;
                        Assert.AreEqual("test2", editDlg.IsolationSchemeName); // Not L10N
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsFalse(editDlg.AsymmetricFilter);
                        Assert.AreEqual(3, editDlg.PrecursorFilter);
                        Assert.AreEqual(null, editDlg.PrecursorRightFilter);
                        editDlg.AsymmetricFilter = true;
                        Assert.AreEqual(1.5, editDlg.PrecursorFilter);
                        Assert.AreEqual(1.5, editDlg.PrecursorRightFilter);
                        editDlg.PrecursorRightFilter = null;
                        editDlg.AsymmetricFilter = false;
                        Assert.AreEqual(3, editDlg.PrecursorFilter);
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2")); // Not L10N
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Test non-numeric isolation width.
                RunUI(() =>
                    {
                        editDlg.CurrentWindowType = EditIsolationSchemeDlg.WindowType.EXTRACTION;
                        Assert.AreEqual("test2", editDlg.IsolationSchemeName); // Not L10N
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsFalse(editDlg.AsymmetricFilter);
                        Assert.AreEqual(3, editDlg.PrecursorFilter);
                        Assert.AreEqual(null, editDlg.PrecursorRightFilter);
                        editDlg.PrecursorFilter = null;
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, messageDlg.Message, 1);
                        messageDlg.OkDialog();
                    });

                // Test minimum isolation width.
                RunUI(() => editDlg.PrecursorFilter = 0);
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, messageDlg.Message, 2);
                        messageDlg.OkDialog();
                    });

                // Test maximum isolation width.
                RunUI(() => editDlg.PrecursorFilter = 10001);
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_less_than_or_equal_to__1__, messageDlg.Message, 2);
                        messageDlg.OkDialog();
                    });

                // Test maximum right isolation width.
                RunUI(() => editDlg.AsymmetricFilter = true);
                RunUI(() => editDlg.PrecursorFilter = 1);
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_less_than_or_equal_to__1__, messageDlg.Message, 2);
                        messageDlg.OkDialog();
                    });

                // Test minimum right isolation width.
                RunUI(() => editDlg.PrecursorRightFilter = 0);
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, messageDlg.Message, 2);
                        messageDlg.OkDialog();
                    });

                // Test non-numeric right isolation width.
                RunUI(() => editDlg.PrecursorRightFilter = null);
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, messageDlg.Message, 1);
                        messageDlg.OkDialog();
                    });

                // Test no prespecified windows.
                RunUI(() => editDlg.UseResults = false);
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.EditIsolationSchemeDlg_OkDialog_Specify_Start_and_End_values_for_at_least_one_isolation_window, messageDlg.Message, 0);
                        messageDlg.OkDialog();
                    });

                // Test minimum start value.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START, 0);
                        editDlg.IsolationWindowGrid.SetCellValue(0);
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 0);
                        editDlg.IsolationWindowGrid.SetCellValue(100);
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.IsolationWindow_DoValidate_Isolation_window_Start_must_be_between__0__and__1__,messageDlg.Message, 2);
                        messageDlg.OkDialog();
                    });

                // Test maximum start value.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START, 0);
                        editDlg.IsolationWindowGrid.SetCellValue(10001);
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 0);
                        editDlg.IsolationWindowGrid.SetCellValue(10002);
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.IsolationWindow_DoValidate_Isolation_window_Start_must_be_between__0__and__1__, messageDlg.Message, 2);
                        messageDlg.OkDialog();
                    });

                // Test delete cell.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START, 0);
                        Assert.IsTrue(editDlg.IsolationWindowGrid.HandleKeyDown(Keys.Delete));
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 0);
                        Assert.IsTrue(editDlg.IsolationWindowGrid.HandleKeyDown(Keys.Delete));
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.EditIsolationSchemeDlg_OkDialog_Specify__0__for_isolation_window, messageDlg.Message, 1);
                        messageDlg.OkDialog();
                    });

                // Test minimum end value.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START, 0);
                        editDlg.IsolationWindowGrid.SetCellValue(100);
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 0);
                        editDlg.IsolationWindowGrid.SetCellValue(0);
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.IsolationWindow_DoValidate_Isolation_window_End_must_be_between__0__and__1__, messageDlg.Message, 2);
                        messageDlg.OkDialog();
                    });

                // Test maximum end value.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 0);
                        editDlg.IsolationWindowGrid.SetCellValue(10001);
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.IsolationWindow_DoValidate_Isolation_window_End_must_be_between__0__and__1__, messageDlg.Message, 2);
                        messageDlg.OkDialog();
                    });

                // Test no start value.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("");
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 0);
                        editDlg.IsolationWindowGrid.SetCellValue(100);
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.EditIsolationSchemeDlg_OkDialog_Specify__0__for_isolation_window, messageDlg.Message, 1);
                        messageDlg.OkDialog();
                    });

                // Test no end value.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START, 0);
                        editDlg.IsolationWindowGrid.SetCellValue(100);
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("");
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.EditIsolationSchemeDlg_OkDialog_Specify__0__for_isolation_window, messageDlg.Message, 1);
                        messageDlg.OkDialog();
                    });

                // Save simple isolation window.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START, 0);
                        editDlg.IsolationWindowGrid.SetCellValue(100);
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 0);
                        editDlg.IsolationWindowGrid.SetCellValue(500);
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Verify simple isolation window, test windows per scan.
                RunUI(() =>
                    {
                        editDlg.CurrentWindowType = EditIsolationSchemeDlg.WindowType.EXTRACTION;
                        Assert.AreEqual("test2", editDlg.IsolationSchemeName); // Not L10N
                        Assert.IsFalse(editDlg.UseResults);
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 100.0, 500.0 }
                            });
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Verify windows per scan, test minimum value.
                RunUI(() =>
                    {
                        editDlg.CurrentWindowType = EditIsolationSchemeDlg.WindowType.MEASUREMENT;
                        Assert.AreEqual("test2", editDlg.IsolationSchemeName); // Not L10N
                        Assert.IsFalse(editDlg.UseResults);
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] {100.0, 500.0}
                            });
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START, 1);
                        editDlg.IsolationWindowGrid.SetCellValue(500);
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 1);
                        editDlg.IsolationWindowGrid.SetCellValue(1000);
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START, 2);
                        editDlg.IsolationWindowGrid.SetCellValue(1000);
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 2);
                        editDlg.IsolationWindowGrid.SetCellValue(1500);
                        editDlg.SpecialHandling = IsolationScheme.SpecialHandlingType.MULTIPLEXED;
                        editDlg.WindowsPerScan =
                            IsolationScheme.MIN_MULTIPLEXED_ISOLATION_WINDOWS - 1; // Below minimum value
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, messageDlg.Message, 2);
                        messageDlg.OkDialog();
                    });

                // Test maximum windows per scan.
                RunUI(() => editDlg.WindowsPerScan = IsolationScheme.MAX_MULTIPLEXED_ISOLATION_WINDOWS + 1); // Above maximum value
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_less_than_or_equal_to__1__, messageDlg.Message, 2);
                        messageDlg.OkDialog();
                    });

                RunUI(() => editDlg.WindowsPerScan = 3);
                OkDialog(editDlg, editDlg.OkDialog);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Verify windows per scan, test minimum value.
                RunUI(() =>
                    {
                        editDlg.CurrentWindowType = EditIsolationSchemeDlg.WindowType.EXTRACTION;
                        Assert.AreEqual("test2", editDlg.IsolationSchemeName); // Not L10N
                        Assert.IsFalse(editDlg.UseResults);
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] {100.0, 500.0},
                                new double?[] {500.0, 1000.0},
                                new double?[] {1000.0, 1500.0}
                            });
                        Assert.AreEqual(3, editDlg.WindowsPerScan);
                        Assert.AreEqual(IsolationScheme.SpecialHandlingType.MULTIPLEXED, editDlg.SpecialHandling);
                    });

                // Test windows per scan without special handling.
                RunUI(() =>
                    {
                        editDlg.SpecialHandling = IsolationScheme.SpecialHandlingType.NONE;
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Test negative margin.
                RunUI(() =>
                    {
                        editDlg.SpecifyMargin = true;
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START_MARGIN, 0);
                        editDlg.IsolationWindowGrid.SetCellValue(-1);
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.IsolationWindow_DoValidate_Isolation_window_margin_must_be_non_negative, messageDlg.Message, 0);
                        messageDlg.OkDialog();
                    });

                // Test non-numeric margin.
                RunDlg<MessageDlg>(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START_MARGIN, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("x");
                    },
                    messageDlg =>
                        {
                            AssertEx.AreComparableStrings(Resources.GridViewDriver_GridView_DataError__0__must_be_a_valid_number, messageDlg.Message, 1);
                            messageDlg.OkDialog();
                        });

                // Save margin.
                RunUI(() =>
                    {
                        editDlg.CurrentWindowType = EditIsolationSchemeDlg.WindowType.MEASUREMENT;
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START, 1);
                        editDlg.IsolationWindowGrid.SetCellValue(497);
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 1);
                        editDlg.IsolationWindowGrid.SetCellValue(1000);
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START, 2);
                        editDlg.IsolationWindowGrid.SetCellValue(995);
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 2);
                        editDlg.IsolationWindowGrid.SetCellValue(1500);

                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START_MARGIN, 0);
                        editDlg.IsolationWindowGrid.SetCellValue(1);
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START_MARGIN, 1);
                        editDlg.IsolationWindowGrid.SetCellValue(2);
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START_MARGIN, 2);
                        editDlg.IsolationWindowGrid.SetCellValue(3);
                    });
                OkDialog(editDlg, editDlg.OkDialog);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Verify margin.
                RunUI(() =>
                    {
                        editDlg.CurrentWindowType = EditIsolationSchemeDlg.WindowType.MEASUREMENT;
                        Assert.AreEqual("test2", editDlg.IsolationSchemeName); // Not L10N
                        Assert.IsFalse(editDlg.UseResults);
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 100.0,  500.0, 1.0},
                                new double?[] { 497.0, 1000.0, 2.0},
                                new double?[] { 995.0, 1500.0, 3.0}
                            });
                        Assert.AreEqual(IsolationScheme.SpecialHandlingType.NONE, editDlg.SpecialHandling);
                        Assert.AreEqual(null, editDlg.WindowsPerScan);
                    });
                OkDialog(editDlg, editDlg.OkDialog);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Test non-numeric margin.
                RunDlg<MessageDlg>(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START_MARGIN, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("x");
                    },
                    messageDlg =>
                        {
                            AssertEx.AreComparableStrings(Resources.GridViewDriver_GridView_DataError__0__must_be_a_valid_number, messageDlg.Message, 1);
                            messageDlg.OkDialog();
                        });

                OkDialog(editDlg, editDlg.OkDialog);
            }

            RunUI(() => editList.SelectItem("test2")); // Not L10N
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Paste one number.
                const double pasteValue = 173.6789;
                ClipboardEx.SetText(pasteValue.ToString(LocalizationHelper.CurrentCulture));
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 0);
                        Assert.IsTrue(editDlg.IsolationWindowGrid.HandleKeyDown(Keys.V, true));
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 100.0, pasteValue, 1.0},
                                new double?[] { 497.0, 1000.0, 2.0},
                                new double?[] { 995.0, 1500.0, 3.0}
                            });
                    });

                // Paste unsorted list, start only (end calculated).
                ClipboardEx.SetText("350\n100\n50\n200");
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 0);
                        editDlg.IsolationWindowGrid.OnPaste();
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 50.0, 100.0, null },
                                new double?[] {100.0, 200.0, null },
                                new double?[] {200.0, 350.0, null }
                            });
                    });

                // Paste unsorted list, start, end, and margin.
                ClipboardEx.SetText("100\t200\t1\n50\t100\t2\n"); // Not L10N
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 0);
                        editDlg.IsolationWindowGrid.OnPaste();
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 50.0, 100.0, 2.0 },
                                new double?[] {100.0, 200.0, 1.0  }
                            });
                    });

                // Paste list, calculate missing ends.
                ClipboardEx.SetText("100\t110\n  111\t\n  115\t\n  117\t118\n  200\t\n  300\t\n"); // Not L10N
                RunUI(() =>
                    {
                        editDlg.SpecifyMargin = false;
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 1);
                        editDlg.IsolationWindowGrid.OnPaste();
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] {100.0, 110.0 },
                                new double?[] {111.0, 115.0 },
                                new double?[] {115.0, 117.0 },
                                new double?[] {117.0, 118.0 },
                                new double?[] {200.0, 300.0 }
                            });
                    });

                // Paste with non-numeric data. 
                ClipboardEx.SetText("100\n110\n200x\n"); // Not L10N
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 1);
                    });
                RunDlg<MessageDlg>(() => editDlg.IsolationWindowGrid.OnPaste(), messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.GridViewDriver_GetValue_An_invalid_number__0__was_specified_for__1__2__, messageDlg.Message, 3);
                        messageDlg.OkDialog();
                    });
                RunUI(() => VerifyCellValues(editDlg, new[]
                    {
                        new double?[] {100.0, 110.0 },
                        new double?[] {111.0, 115.0 },
                        new double?[] {115.0, 117.0 },
                        new double?[] {117.0, 118.0 },
                        new double?[] {200.0, 300.0 }
                    }));

                // Paste below minimum start value.
                ClipboardEx.SetText("0\n100\n200\n"); // Not L10N
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 1);
                    });
                RunDlg<MessageDlg>(() => editDlg.IsolationWindowGrid.OnPaste(), messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.GridViewDriver_ValidateRow_On_line__0__1__, messageDlg.Message, 2);
                        messageDlg.OkDialog();
                    });
                RunUI(() => VerifyCellValues(editDlg, new[]
                    {
                        new double?[] {100.0, 110.0 },
                        new double?[] {111.0, 115.0 },
                        new double?[] {115.0, 117.0 },
                        new double?[] {117.0, 118.0 },
                        new double?[] {200.0, 300.0 }
                    }));

                // Paste above maximum end value.
                ClipboardEx.SetText("100\n110\n10001\n"); // Not L10N
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 1);
                    });
                RunDlg<MessageDlg>(() => editDlg.IsolationWindowGrid.OnPaste(), messageDlg =>
                    {
                        // NOTE: Because of the order of processing, the out of range value at the end
                        // of the list is flagged as being a Start value, when it is really only used
                        // as the end of the previous interval.  Fixing that would require some work.
                        AssertEx.AreComparableStrings(Resources.GridViewDriver_ValidateRow_On_line__0__1__, messageDlg.Message, 2);
                        messageDlg.OkDialog();
                    });
                RunUI(() => VerifyCellValues(editDlg, new[]
                    {
                        new double?[] {100.0, 110.0 },
                        new double?[] {111.0, 115.0 },
                        new double?[] {115.0, 117.0 },
                        new double?[] {117.0, 118.0 },
                        new double?[] {200.0, 300.0 }
                    }));

                RunDlg<MultiButtonMsgDlg>(editDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.EditIsolationSchemeDlg_OkDialog_There_are_gaps_in_a_single_cycle_of_your_extraction_windows__Do_you_want_to_continue_, messageDlg.Message);
                    messageDlg.BtnYesClick();
                });
                WaitForClosedForm(editDlg);
            }
            RunUI(() => editList.SelectItem("test1")); // Not L10N
            {
                // Test Extraction/Isolation Alternation
                const int rows = 5;
                const int startMargin = 5;
                const int firstRangeStart = 100;
                const int firstRangeEnd = 120;
                const int rangeInterval = 100;

                double?[][] expectedValues = new double?[rows][];
                {
                    var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);
                    RunUI(() =>
                    {
                        editDlg.IsolationSchemeName = "test3";
                        editDlg.UseResults = false;
                        editDlg.SpecifyMargin = true;
                   
                        for (int row = 0; row < rows; row++)
                        {
                            editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START,row);
                            editDlg.IsolationWindowGrid.SetCellValue(firstRangeStart + rangeInterval*row);
                            editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, row);
                            editDlg.IsolationWindowGrid.SetCellValue(firstRangeEnd + rangeInterval*row);
                            editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START_MARGIN, row);
                            editDlg.IsolationWindowGrid.SetCellValue(startMargin);
                        }
                        for (int row = 0; row < rows; row ++)
                        {
                            expectedValues[row] = new double?[]
                            {
                                firstRangeStart + rangeInterval*row,
                                firstRangeEnd + rangeInterval*row,
                                startMargin
                            };
                        }
                        VerifyCellValues(editDlg, expectedValues);
                        editDlg.CurrentWindowType = EditIsolationSchemeDlg.WindowType.EXTRACTION;
                        // Test extraction alternation
                        for (int row = 0; row < rows; row ++)
                        {
                            expectedValues[row][0] += startMargin;
                            expectedValues[row][1] -= startMargin;
                        }
                        VerifyCellValues(editDlg, expectedValues);
                    });
                    RunDlg<MultiButtonMsgDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.EditIsolationSchemeDlg_OkDialog_There_are_gaps_in_a_single_cycle_of_your_extraction_windows__Do_you_want_to_continue_, messageDlg.Message);
                        messageDlg.BtnYesClick();
                    });
                    WaitForClosedForm(editDlg);
                }
                RunUI(() => editList.SelectItem("test3")); // Not L10N
                {
                    var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);
                    int row = 0;
                    RunUI(() =>
                    {
                        // Test that the isolation windows were saved correctly as extraction windows
                        foreach (IsolationWindow isolationWindow in editDlg.IsolationScheme.PrespecifiedIsolationWindows)
                        {
                            Assert.AreEqual(expectedValues[row][0], isolationWindow.Start);
                            Assert.AreEqual(expectedValues[row][1], isolationWindow.End);
                            Assert.AreEqual(expectedValues[row][2], isolationWindow.StartMargin);
                            row++;
                        }
                    });
                    
                    // Test Graph to make sure it has the right lines
                    RunDlg<DiaIsolationWindowsGraphForm>(editDlg.OpenGraph, diaGraph =>
                    {
                        int windowCount = diaGraph.Windows.Count;
                        int isolationCount = windowCount/diaGraph.CyclesPerGraph;
                        for (int i = 0; i < isolationCount; i ++)
                        {
                            for (int j = 0; j < 3; j ++)
                            {
                                int index = j*isolationCount + i;
                                Location locWindow = diaGraph.Windows.ElementAt(index).Location;
                                Location locLMargin = diaGraph.LeftMargins.ElementAt(index).Location;
                                Location locRMargin = diaGraph.RightMargins.ElementAt(index).Location;
                                Assert.AreEqual(locWindow.X1, expectedValues[i][0]);
                                Assert.AreEqual(locWindow.X2, expectedValues[i][1]);
                                Assert.AreEqual(locWindow.Y1, j + (double) i / expectedValues.Length);
                                Assert.AreEqual(locLMargin.X1, expectedValues[i][0] - expectedValues[i][2]);
                                Assert.AreEqual(locLMargin.X2, expectedValues[i][0]);
                                Assert.AreEqual(locLMargin.Y1, j + (double) i/expectedValues.Length);
                                Assert.AreEqual(locRMargin.X1, expectedValues[i][1]);
                                Assert.AreEqual(locRMargin.X2, expectedValues[i][1] + expectedValues[i][2]);
                                Assert.AreEqual(locRMargin.Y1, j + (double)i / expectedValues.Length);
                            }
                        }
                        diaGraph.CloseButton();
                    });

                    RunDlg<CalculateIsolationSchemeDlg>(editDlg.Calculate, calc =>
                    {
                        calc.Deconvolution = EditIsolationSchemeDlg.DeconvolutionMethod.OVERLAP;
                        calc.Start = 100;
                        calc.End = 1000;
                        calc.WindowWidth = 50;
                        calc.OkDialog();
                    });
                    //Make sure overlap graphs correctly
                    RunDlg<DiaIsolationWindowsGraphForm>(editDlg.OpenGraph, diaGraph =>
                    {
                        const int windowSize = 19;
                        for (int cycle = 0; cycle < diaGraph.CyclesPerGraph - 1; cycle ++)
                        {
                            int startDifferenceMult = cycle%2 == 0 ? -1 : 1;
                            for (int i = 0; i < windowSize; i ++)
                            {
                                Location window = diaGraph.Windows.ElementAt(cycle * windowSize + i).Location;
                                Location nextWindow = diaGraph.Windows.ElementAt((cycle + 1)*windowSize + i).Location;
                                Assert.AreEqual(window.X1 + startDifferenceMult * 0.5 * window.Width, nextWindow.X1);
                                Assert.AreEqual(window.X2 + startDifferenceMult * 0.5 * window.Width, nextWindow.X2);
                            }
                        }
                        diaGraph.CloseButton();
                    });
                    
                    //Make sure gaps and overlaps work correctly
                    RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(1,1);
                        editDlg.IsolationWindowGrid.SetCellValue(210);
                        editDlg.IsolationWindowGrid.SelectCell(1,2);
                        editDlg.IsolationWindowGrid.SetCellValue(240);
                    });
                    RunDlg<DiaIsolationWindowsGraphForm>(editDlg.OpenGraph, diaGraph =>
                    {
                        int expectedGaps = diaGraph.CyclesPerGraph - diaGraph.CyclesPerGraph/2;
                        int expectedOverlaps = expectedGaps;
                        Assert.AreEqual(expectedOverlaps,diaGraph.Overlaps.Count);
                        Assert.AreEqual(expectedGaps,diaGraph.Gaps.Count);
                        for (int i = 0; i < expectedGaps; i ++)
                        {
                            Location overlap = diaGraph.Overlaps.ElementAt(i).Location;
                            Location gap = diaGraph.Gaps.ElementAt(i).Location;
                            Assert.AreEqual(i*2,overlap.Y1);
                            Assert.AreEqual(i*2,gap.Y1);
                            Assert.AreEqual(200,overlap.X1);
                            Assert.AreEqual(210,overlap.X2);
                            Assert.AreEqual(240,gap.X1);
                            Assert.AreEqual(250,gap.X2);
                            Assert.AreEqual(1,gap.Height);
                            Assert.AreEqual(1,overlap.Height);
                        }
                        diaGraph.CloseButton();
                    });
                    RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell(1, 1);
                        editDlg.IsolationWindowGrid.SetCellValue(200);
                        editDlg.IsolationWindowGrid.SelectCell(1, 2);
                        editDlg.IsolationWindowGrid.SetCellValue(250);
                    });
                    OkDialog(editDlg,editDlg.OkDialog);
                }
                OkDialog(editList, editList.OkDialog);
                OkDialog(fullScanDlg, fullScanDlg.OkDialog);
            }

            TestDiaTransitionExclusionVisual();
        }

        private static void VerifyCellValues(EditIsolationSchemeDlg editDlg, double?[][] expectedValues)
        {
            // Verify expected number of rows.
            Assert.AreEqual(expectedValues.Length+1, editDlg.IsolationWindowGrid.RowCount); // Grid always shows an extra row.
            
            var visibleColumns = editDlg.IsolationWindowGrid.VisibleColumnCount;

            for (int row = 0; row < expectedValues.Length; row++)
            {
                // Verify expected number of columns.
                Assert.AreEqual(expectedValues[row].Length, visibleColumns);

                int visibleCol = 0;
                for (int col = 0; col < expectedValues[row].Length; col++)
                {
                    var expectedValue = expectedValues[row][col];
                    while (!editDlg.IsolationWindowGrid.IsColumnVisible(visibleCol))
                        visibleCol++;

                    // Verify cell value.
                    var actualValue = editDlg.IsolationWindowGrid.GetCellValue(visibleCol++, row);
                    if (expectedValue.HasValue)
                    {
                        Assert.AreEqual(expectedValue, double.Parse(actualValue));
                    }
                    else
                    {
                        Assert.AreEqual(string.Empty, actualValue);
                    }
                }
            }
        }

        private static void TestDiaTransitionExclusionVisual()
        {
            // Open a new document, edit transition settings, change to DIA and observe visual changes 
            {
                RunDlg<MultiButtonMsgDlg>(SkylineWindow.NewDocument, saveDlg => saveDlg.Btn1Click());
                var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);

                RunUI(() =>
                {
                    // Set the acquisition method to none 
                    transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.None;

                    // Due to strange behavior of C# we need to first make the Form visible before the changes we 
                    // made to the visibility of the elements take any effect
                    // http://stackoverflow.com/questions/11161160/c-sharp-usercontrol-visible-property-not-changing
                    // A different way of doing this would be:
                    // http://stackoverflow.com/questions/5980343/how-do-i-determine-visibility-of-a-control
                    transitionSettings.SelectedTab = TransitionSettingsUI.TABS.Filter;
                });

                // Test that the precursor exclusion text box is visible (and the check box is not)   
                RunUI(() =>
                {
                    TextBox textBox = (TextBox) transitionSettings.Controls.Find("textExclusionWindow", true).First();
                    Assert.IsTrue(textBox.Visible);
                    CheckBox checkbox = (CheckBox)transitionSettings.Controls.Find("cbExclusionUseDIAWindow", true).First();
                    Assert.IsFalse(checkbox.Visible);
                });

                // Switch to DIA and fill out the isolation scheme
                RunUI(() => transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.DIA);
                {
                    var editDlg = ShowDialog<EditIsolationSchemeDlg>(transitionSettings.AddIsolationScheme);
                    FillInIsolationSchemeDialogPreSelected(editDlg, "test1_diaexl_v");
                }

                // Test that the precursor exclusion text box is not visible (and the check box is)
                RunUI(() =>
                {
                    TextBox textBox = (TextBox) transitionSettings.Controls.Find("textExclusionWindow", true).First();
                    Assert.IsFalse(textBox.Visible);
                    CheckBox checkbox = (CheckBox) transitionSettings.Controls.Find("cbExclusionUseDIAWindow", true).First();
                    Assert.IsTrue(checkbox.Visible);
                });

                // Switching to back to DIA but using an isolation scheme from the results
                RunUI(() => transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.DIA);
                {
                    var editDlg = ShowDialog<EditIsolationSchemeDlg>(transitionSettings.AddIsolationScheme);
                    FillInIsolationSchemeDialogFromResults(editDlg, "test2_diaexl_v");
                }
                // The DIA checkbox is still visible
                RunUI(() =>
                {
                    TextBox textBox = (TextBox)transitionSettings.Controls.Find("textExclusionWindow", true).First();
                    Assert.IsFalse(textBox.Visible);
                    CheckBox checkbox = (CheckBox)transitionSettings.Controls.Find("cbExclusionUseDIAWindow", true).First();
                    Assert.IsTrue(checkbox.Visible);
                });

                // Switch to DIA and fill out the isolation scheme
                RunUI(() => transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.DIA);
                {
                    var editDlg = ShowDialog<EditIsolationSchemeDlg>(transitionSettings.AddIsolationScheme);
                    FillInIsolationSchemeDialogPreSelected(editDlg, "test3_diaexl_v");
                }
                RunUI(() =>
                {
                    TextBox textBox = (TextBox)transitionSettings.Controls.Find("textExclusionWindow", true).First();
                    Assert.IsFalse(textBox.Visible);
                    CheckBox checkbox = (CheckBox)transitionSettings.Controls.Find("cbExclusionUseDIAWindow", true).First();
                    Assert.IsTrue(checkbox.Visible);
                });

                // and back again ... 
                RunUI(() =>
                {
                    transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.None;
                    TextBox textBox = (TextBox)transitionSettings.Controls.Find("textExclusionWindow", true).First();
                    Assert.IsTrue(textBox.Visible);
                    CheckBox checkbox = (CheckBox)transitionSettings.Controls.Find("cbExclusionUseDIAWindow", true).First();
                    Assert.IsFalse(checkbox.Visible);
                });

                OkDialog(transitionSettings, transitionSettings.CancelDialog);
            }

            // Switching the FullScanAcquisitionMethod to DIA and back should set the GUI input fields properly 
            // to their default values (checked for DIAExclusionWindow and empty for ExclusionWindow)
            {
                RunUI(() => SkylineWindow.NewDocument());
                var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                //PauseTest();
                    
                RunUI(() =>
                {
                    transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                    // Check that the DIAExclusionWindow setting is true by default
                    Assert.AreEqual(false, transitionSettings.SetDIAExclusionWindow);
                    Assert.IsNull(transitionSettings.ExclusionWindow);
                });

                // Switch to DIA and fill out the isolation scheme
                RunUI(() => transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.DIA);
                {
                    var editDlg = ShowDialog<EditIsolationSchemeDlg>(transitionSettings.AddIsolationScheme);
                    FillInIsolationSchemeDialogPreSelected(editDlg, "test2_diaexl");
                }
                RunUI(() =>
                {
                    // Check that the DIAExclusionWindow setting is true by default when using an isolation scheme
                    Assert.AreEqual(true, transitionSettings.SetDIAExclusionWindow);
                    Assert.IsNull(transitionSettings.ExclusionWindow);
                });

                // Switching to None should turn off the settting
                RunUI(() =>
                {
                    transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.None;
                    Assert.AreEqual(false, transitionSettings.SetDIAExclusionWindow);
                    Assert.IsNull(transitionSettings.ExclusionWindow);
                });

                // Set the exclusion window and check it is set
                RunUI(() =>
                {
                    transitionSettings.ExclusionWindow = 3.0;
                    Assert.AreEqual(false, transitionSettings.SetDIAExclusionWindow);
                    Assert.AreEqual(3.0, transitionSettings.ExclusionWindow);
                });

                // Switching to back to DIA and fill out the isolation scheme should set it to the default (true) again
                RunUI(() => transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.DIA);
                {
                    var editDlg = ShowDialog<EditIsolationSchemeDlg>(transitionSettings.AddIsolationScheme);
                    FillInIsolationSchemeDialogPreSelected(editDlg, "test3_diaexl");
                }
                RunUI(() =>
                {
                    transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                    // Check that the setting is there by default
                    Assert.AreEqual(true, transitionSettings.SetDIAExclusionWindow);
                    Assert.IsNull(transitionSettings.ExclusionWindow);
                });

                // Switching to Targeted should turn off the settting
                RunUI(() =>
                {
                    transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.Targeted;
                    Assert.AreEqual(false, transitionSettings.SetDIAExclusionWindow);
                    Assert.IsNull(transitionSettings.ExclusionWindow);
                });

                // Switching to back to DIA should set it to the default (true) again
                RunUI(() => transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.DIA);
                {
                    var editDlg = ShowDialog<EditIsolationSchemeDlg>(transitionSettings.AddIsolationScheme);
                    FillInIsolationSchemeDialogPreSelected(editDlg, "test4_diaexl");
                }
                RunUI(() =>
                {
                    // Check that the setting is there by default
                    Assert.AreEqual(true, transitionSettings.SetDIAExclusionWindow);
                    Assert.IsNull(transitionSettings.ExclusionWindow);
                });

                // Switching to back to DIA should but using an isolation scheme from the results should behave like non-DIA
                RunUI(() => transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.DIA);
                {
                    var editDlg = ShowDialog<EditIsolationSchemeDlg>(transitionSettings.AddIsolationScheme);
                    FillInIsolationSchemeDialogFromResults(editDlg, "test5_diaexl");
                }
                RunUI(() =>
                {
                    // Check that the setting is there by default
                    Assert.IsFalse(transitionSettings.SetDIAExclusionWindow);
                    Assert.IsNull(transitionSettings.ExclusionWindow);
                });
                
                // Check the use DIA window checkbox
                RunUI(() =>
                {
                    transitionSettings.SetDIAExclusionWindow = true;
                });
                // Try to close the dialog
                // Error message will appear because checkbox can't be used with "From Results"
                RunDlg<MessageDlg>(transitionSettings.OkDialog, messageDlg =>
                {
                    Assert.AreEqual(messageDlg.Message, 
                        Resources.TransitionSettingsUI_OkDialog_Cannot_use_DIA_window_for_precursor_exclusion_when_isolation_scheme_does_not_contain_prespecified_windows___Please_select_an_isolation_scheme_with_prespecified_windows_);
                    messageDlg.OkDialog();
                });

                // Switch to "All Ions" but check the DIA exclusion box
                RunUI(() =>
                {
                    ComboBox isolationSchemeBox = (ComboBox)transitionSettings.Controls.Find("comboIsolationScheme", true).First();
                    isolationSchemeBox.SelectedIndex = 0;
                    Assert.AreEqual(isolationSchemeBox.SelectedItem, IsolationScheme.SpecialHandlingType.ALL_IONS);
                    transitionSettings.SetDIAExclusionWindow = true;
                });
                // Try to close the dialog
                // Error message will appear because checkbox can't be used with "All Ions"
                RunDlg<MessageDlg>(transitionSettings.OkDialog, messageDlg =>
                {
                    Assert.AreEqual(messageDlg.Message,
                        Resources.TransitionSettingsUI_OkDialog_Cannot_use_DIA_window_for_precusor_exclusion_when__All_Ions__is_selected_as_the_isolation_scheme___To_use_the_DIA_window_for_precusor_exclusion__change_the_isolation_scheme_in_the_Full_Scan_settings_);
                    messageDlg.OkDialog();
                });

                // Uncheck the use DIA window checkbox
                RunUI(() =>
                {
                    Assert.IsTrue(transitionSettings.SetDIAExclusionWindow);
                    transitionSettings.SetDIAExclusionWindow = false;
                });

                OkDialog(transitionSettings, transitionSettings.CancelDialog);
            }
        }

        /// <summary>
        /// Private helper method to fill out the Isolation scheme dialog with a simple isolation scheme
        /// </summary>
        /// <param name="editDlg">The edit dialog to be filled out</param>
        /// <param name="name">The name of the isolation scheme</param>
        private static void FillInIsolationSchemeDialogPreSelected(EditIsolationSchemeDlg editDlg, string name)
        {
            RunUI(() =>
            {
                AssertDefault(editDlg);
                editDlg.IsolationSchemeName = name;

                editDlg.UseResults = false;
                editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START, 0);
                editDlg.IsolationWindowGrid.SetCellValue(100);
                editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 0);
                editDlg.IsolationWindowGrid.SetCellValue(250);
                editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_START, 1);
                editDlg.IsolationWindowGrid.SetCellValue(250);
                editDlg.IsolationWindowGrid.SelectCell(EditIsolationSchemeDlg.COLUMN_END, 1);
                editDlg.IsolationWindowGrid.SetCellValue(500);
                editDlg.OkDialog();
            });
        }

        /// <summary>
        /// Private helper method to configure the Isolation scheme dialog to obtain windows from results
        /// </summary>
        /// <param name="editDlg">The edit dialog to be filled out</param>
        /// <param name="name">The name of the isolation scheme</param>
        private static void FillInIsolationSchemeDialogFromResults(EditIsolationSchemeDlg editDlg, string name)
        {
            RunUI(() =>
            {
                AssertDefault(editDlg);
                editDlg.IsolationSchemeName = name;

                editDlg.UseResults = true;
                editDlg.PrecursorFilter = 1;
                editDlg.PrecursorRightFilter = 2;
            });
            OkDialog(editDlg, editDlg.OkDialog);
        }

        /// <summary>
        /// Check that the isolation scheme has default values
        /// </summary>
        /// <param name="editDlg"></param>
        private static void AssertDefault(EditIsolationSchemeDlg editDlg)
        {
            Assert.AreEqual(string.Empty, editDlg.IsolationSchemeName);
            Assert.IsTrue(editDlg.UseResults);
            Assert.IsFalse(editDlg.AsymmetricFilter);
            Assert.AreEqual(2, editDlg.PrecursorFilter);
            Assert.AreEqual(null, editDlg.PrecursorRightFilter);
        }
    }
}
