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

using System.Globalization;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

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
                        AssertEx.Contains(messageDlg.Message, "Name cannot be empty");
                        messageDlg.OkDialog();
                    });

                // Create a scheme with default values.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.IsolationSchemeName, "");
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsFalse(editDlg.AsymmetricFilter);
                        Assert.AreEqual(editDlg.PrecursorFilter, "2");
                        Assert.AreEqual(editDlg.PrecursorRightFilter, "");
                        editDlg.IsolationSchemeName = "test1";
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
                        Assert.AreEqual(editDlg.IsolationSchemeName, "");
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsFalse(editDlg.AsymmetricFilter);
                        Assert.AreEqual(editDlg.PrecursorFilter, "2");
                        Assert.AreEqual(editDlg.PrecursorRightFilter, "");
                        editDlg.IsolationSchemeName = "test1";
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message,
                            "The isolation scheme named 'test1' already exists.");
                        messageDlg.OkDialog();
                    });
                RunUI(() => editDlg.CancelButton.PerformClick());
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test1"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Edit scheme, change name and isolation width.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.IsolationSchemeName, "test1");
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsFalse(editDlg.AsymmetricFilter);
                        Assert.AreEqual(editDlg.PrecursorFilter, "2");
                        Assert.AreEqual(editDlg.PrecursorRightFilter, "");
                        editDlg.IsolationSchemeName = "test2";
                        editDlg.PrecursorFilter = "50";
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Test asymmetric isolation width (automatic split).
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.IsolationSchemeName, "test2");
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsFalse(editDlg.AsymmetricFilter);
                        Assert.AreEqual(editDlg.PrecursorFilter, "50");
                        Assert.AreEqual(editDlg.PrecursorRightFilter, "");
                        editDlg.AsymmetricFilter = true;
                        Assert.AreEqual(editDlg.PrecursorFilter, "25");
                        Assert.AreEqual(editDlg.PrecursorRightFilter, "25");
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Test asymmetric isolation width (manually set).
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.IsolationSchemeName, "test2");
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsTrue(editDlg.AsymmetricFilter);
                        Assert.AreEqual(editDlg.PrecursorFilter, "25");
                        Assert.AreEqual(editDlg.PrecursorRightFilter, "25");
                        editDlg.PrecursorFilter = "1";
                        editDlg.PrecursorRightFilter = "2";
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Test return to symmetric isolation width.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.IsolationSchemeName, "test2");
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsTrue(editDlg.AsymmetricFilter);
                        Assert.AreEqual(editDlg.PrecursorFilter, "1");
                        Assert.AreEqual(editDlg.PrecursorRightFilter, "2");
                        editDlg.AsymmetricFilter = false;
                        Assert.AreEqual(editDlg.PrecursorFilter, "3");
                        Assert.AreEqual(editDlg.PrecursorRightFilter, "");
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Test return to symmetric isolation width with only left width specified.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.IsolationSchemeName, "test2");
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsFalse(editDlg.AsymmetricFilter);
                        Assert.AreEqual(editDlg.PrecursorFilter, "3");
                        Assert.AreEqual(editDlg.PrecursorRightFilter, "");
                        editDlg.AsymmetricFilter = true;
                        Assert.AreEqual(double.Parse(editDlg.PrecursorFilter), 1.5);
                        Assert.AreEqual(double.Parse(editDlg.PrecursorRightFilter), 1.5);
                        editDlg.PrecursorRightFilter = "";
                        editDlg.AsymmetricFilter = false;
                        Assert.AreEqual(editDlg.PrecursorFilter, "3");
                        editDlg.OkDialog();
                    });
                WaitForClosedForm(editDlg);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Test non-numeric isolation width.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.IsolationSchemeName, "test2");
                        Assert.IsTrue(editDlg.UseResults);
                        Assert.IsFalse(editDlg.AsymmetricFilter);
                        Assert.AreEqual(editDlg.PrecursorFilter, "3");
                        Assert.AreEqual(editDlg.PrecursorRightFilter, "");
                        editDlg.PrecursorFilter = "x";
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Isolation width must contain a decimal value.");
                        messageDlg.OkDialog();
                    });

                // Test minimum isolation width.
                RunUI(() => editDlg.PrecursorFilter = "0");
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message,
                            "Isolation width must be greater than or equal to");
                        messageDlg.OkDialog();
                    });

                // Test maximum isolation width.
                RunUI(() => editDlg.PrecursorFilter = "10001");
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message,
                            "Isolation width must be less than or equal to 10000.");
                        messageDlg.OkDialog();
                    });

                // Test maximum right isolation width.
                RunUI(() => editDlg.AsymmetricFilter = true);
                RunUI(() => editDlg.PrecursorFilter = "1");
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message,
                            "Isolation widths must be less than or equal to 5000.");
                        messageDlg.OkDialog();
                    });

                // Test minimum right isolation width.
                RunUI(() => editDlg.PrecursorRightFilter = "0");
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message,
                            "Isolation widths must be greater than or equal to");
                        messageDlg.OkDialog();
                    });

                // Test non-numeric right isolation width.
                RunUI(() => editDlg.PrecursorRightFilter = "");
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Isolation widths must contain a decimal value.");
                        messageDlg.OkDialog();
                    });

                // Test no prespecified windows.
                RunUI(() => editDlg.UseResults = false);
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message,
                            "Specify Start and End values for at least one isolation window.");
                        messageDlg.OkDialog();
                    });

                // Test minimum start value.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.start, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("0");
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("1");
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Isolation window Start must be between");
                        messageDlg.OkDialog();
                    });

                // Test maximum start value.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.start, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("10001");
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("10002");
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Isolation window Start must be between");
                        messageDlg.OkDialog();
                    });

                // Test delete cell.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.start, 0);
                        Assert.IsTrue(editDlg.IsolationWindowGrid.HandleKeyDown(Keys.Delete));
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 0);
                        Assert.IsTrue(editDlg.IsolationWindowGrid.HandleKeyDown(Keys.Delete));
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Specify Start for isolation window.");
                        messageDlg.OkDialog();
                    });

                // Test minimum end value.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.start, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("1");
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("0");
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Isolation window End must be between");
                        messageDlg.OkDialog();
                    });

                // Test maximum end value.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("10001");
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Isolation window End must be between");
                        messageDlg.OkDialog();
                    });

                // Test no start value.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.start, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("");
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("1");
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Specify Start for isolation window.");
                        messageDlg.OkDialog();
                    });

                // Test no end value.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.start, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("1");
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("");
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Specify End for isolation window.");
                        messageDlg.OkDialog();
                    });

                // Save simple isolation window.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.start, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("1");
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("5");
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
                        Assert.AreEqual(editDlg.IsolationSchemeName, "test2");
                        Assert.IsFalse(editDlg.UseResults);
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 1.0, 5.0 }
                            });
                        editDlg.SpecialHandling = IsolationScheme.SpecialHandlingType.MULTIPLEXED;
                        editDlg.WindowsPerScan = "5";
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
                        Assert.AreEqual(editDlg.IsolationSchemeName, "test2");
                        Assert.IsFalse(editDlg.UseResults);
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 1.0, 5.0 }
                            });
                        Assert.AreEqual(editDlg.SpecialHandling, IsolationScheme.SpecialHandlingType.MULTIPLEXED);
                        Assert.AreEqual(editDlg.WindowsPerScan, "5");
                        editDlg.WindowsPerScan = 
                            (IsolationScheme.MIN_MULTIPLEXED_ISOLATION_WINDOWS - 1).ToString(CultureInfo.CurrentCulture); // Below minimum value
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message,
                            "Windows per scan must be greater than or equal to");
                        messageDlg.OkDialog();
                    });

                // Test maximum windows per scan.
                RunUI(() => editDlg.WindowsPerScan = (IsolationScheme.MAX_MULTIPLEXED_ISOLATION_WINDOWS + 1).ToString(CultureInfo.CurrentCulture)); // Above maximum value
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message,
                            "Windows per scan must be less than or equal to");
                        messageDlg.OkDialog();
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

                // Test empty target.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.IsolationSchemeName, "test2");
                        Assert.IsFalse(editDlg.UseResults);
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 1.0, 5.0 }
                            });
                        Assert.AreEqual(editDlg.SpecialHandling, IsolationScheme.SpecialHandlingType.NONE);
                        Assert.AreEqual(editDlg.WindowsPerScan, "");
                        editDlg.SpecifyTarget = true;
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Specify Target for isolation window.");
                        messageDlg.OkDialog();
                    });

                // Save target.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.target, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("2");
                    });
                OkDialog(editDlg, editDlg.OkDialog);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Verify windows per scan, test minimum value.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.IsolationSchemeName, "test2");
                        Assert.IsFalse(editDlg.UseResults);
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 1.0, 5.0, 2.0 }
                            });
                        Assert.AreEqual(editDlg.SpecialHandling, IsolationScheme.SpecialHandlingType.NONE);
                        Assert.AreEqual(editDlg.WindowsPerScan, "");
                        Assert.IsTrue(editDlg.SpecifyTarget);
                        Assert.AreEqual(editDlg.MarginType, EditIsolationSchemeDlg.WindowMargin.NONE);
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.target, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("5"); // Outside window
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message,
                            "Target value is not within the range of the isolation window.");
                        messageDlg.OkDialog();
                    });

                // Test empty margin.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.target, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("2");
                        editDlg.MarginType = EditIsolationSchemeDlg.WindowMargin.SYMMETRIC;
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Specify Margin for isolation window.");
                        messageDlg.OkDialog();
                    });

                // Test empty margin without target.
                RunUI(() => editDlg.SpecifyTarget = false);
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Specify Margin for isolation window.");
                        messageDlg.OkDialog();
                    });

                // Test negative margin.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.start_margin, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("-1");
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Isolation window margin must be non-negative.");
                        messageDlg.OkDialog();
                    });

                // Test non-numeric margin.
                RunDlg<MessageDlg>(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.start_margin, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("x");
                    },
                    messageDlg =>
                        {
                            AssertEx.Contains(messageDlg.Message, "Margin must be a valid number.");
                            messageDlg.OkDialog();
                        });

                // Save margin.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.start_margin, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("1");
                    });
                OkDialog(editDlg, editDlg.OkDialog);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Verify margin.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.IsolationSchemeName, "test2");
                        Assert.IsFalse(editDlg.UseResults);
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 1.0, 5.0, 1.0 }
                            });
                        Assert.AreEqual(editDlg.SpecialHandling, IsolationScheme.SpecialHandlingType.NONE);
                        Assert.AreEqual(editDlg.WindowsPerScan, "");
                        Assert.IsFalse(editDlg.SpecifyTarget);
                        Assert.AreEqual(editDlg.MarginType, EditIsolationSchemeDlg.WindowMargin.SYMMETRIC);
                    });
                OkDialog(editDlg, editDlg.OkDialog);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Test empty end margin.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.IsolationSchemeName, "test2");
                        Assert.IsFalse(editDlg.UseResults);
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 1.0, 5.0, 1.0 }
                            });
                        Assert.AreEqual(editDlg.SpecialHandling, IsolationScheme.SpecialHandlingType.NONE);
                        Assert.AreEqual(editDlg.WindowsPerScan, "");
                        Assert.IsFalse(editDlg.SpecifyTarget);
                        Assert.AreEqual(editDlg.MarginType, EditIsolationSchemeDlg.WindowMargin.SYMMETRIC);
                        editDlg.MarginType = EditIsolationSchemeDlg.WindowMargin.ASYMMETRIC;
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Specify End margin for isolation window.");
                        messageDlg.OkDialog();
                    });

                // Test negative end margin.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end_margin, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("-1");
                    });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message, "Isolation window margin must be non-negative.");
                        messageDlg.OkDialog();
                    });

                // Test non-numeric margin.
                RunDlg<MessageDlg>(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end_margin, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("x");
                    },
                    messageDlg =>
                        {
                            AssertEx.Contains(messageDlg.Message, "End margin must be a valid number.");
                            messageDlg.OkDialog();
                        });

                // Save end margin.
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end_margin, 0);
                        editDlg.IsolationWindowGrid.SetCellValue("2");
                    });
                OkDialog(editDlg, editDlg.OkDialog);
            }

            RunUI(() => editList.SelectItem("test2"));
            {
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(editList.EditItem);

                // Verify margin.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.IsolationSchemeName, "test2");
                        Assert.IsFalse(editDlg.UseResults);
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 1.0, 5.0, 1.0, 2.0 }
                            });

                        Assert.AreEqual(editDlg.SpecialHandling, IsolationScheme.SpecialHandlingType.NONE);
                        Assert.AreEqual(editDlg.WindowsPerScan, "");
                        Assert.IsFalse(editDlg.SpecifyTarget);
                        Assert.AreEqual(editDlg.MarginType, EditIsolationSchemeDlg.WindowMargin.ASYMMETRIC);
                    });

                // Paste one number.
                const double pasteValue = 73.6789;
                ClipboardEx.SetText(pasteValue.ToString(CultureInfo.CurrentCulture));
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 0);
                        Assert.IsTrue(editDlg.IsolationWindowGrid.HandleKeyDown(Keys.V, true));
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 1.0, pasteValue, 1.0, 2.0 }
                            });
                    });

                // Paste unsorted list, start only (end calculated).
                ClipboardEx.SetText("35\n10\n5\n20");
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 0);
                        editDlg.IsolationWindowGrid.OnPaste();
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 5.0, 10.0, null, null },
                                new double?[] {10.0, 20.0, null, null  },
                                new double?[] {20.0, 35.0, null, null  }
                            });
                    });

                // Paste unsorted list, start, end, start margin and end margin.
                ClipboardEx.SetText("10\t20\t1\t1\n1\t5\t1\t2\n");
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 0);
                        editDlg.IsolationWindowGrid.OnPaste();
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 1.0,  5.0, 1.0, 2.0 },
                                new double?[] {10.0, 20.0, 1.0, 1.0  }
                            });
                    });

                // Paste list, calculate missing ends and targets.
                ClipboardEx.SetText("1\t10\t2\n  11\t\t\n  15\t\t16\n  17\t18\t\n  20\t\t\n  30\t\t\n");
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 1);
                        editDlg.MarginType = EditIsolationSchemeDlg.WindowMargin.NONE;
                        editDlg.SpecifyTarget = true;
                        editDlg.IsolationWindowGrid.OnPaste();
                        VerifyCellValues(editDlg, new[]
                            {
                                new double?[] { 1.0, 10.0,  2.0 },
                                new double?[] {11.0, 15.0, 13.0 },
                                new double?[] {15.0, 17.0, 16.0 },
                                new double?[] {17.0, 18.0, 17.5 },
                                new double?[] {20.0, 30.0, 25.0 }
                            });
                    });

                // Paste with non-numeric data.
                ClipboardEx.SetText("1\n10\n20x\n");
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 1);
                        editDlg.MarginType = EditIsolationSchemeDlg.WindowMargin.NONE;
                        editDlg.SpecifyTarget = true;
                    });
                RunDlg<MessageDlg>(() => editDlg.IsolationWindowGrid.OnPaste(), messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message,
                            @"An invalid number (""20x"") was specified for Start on line 3.");
                        messageDlg.OkDialog();
                    });
                RunUI(() => VerifyCellValues(editDlg, new[]
                    {
                        new double?[] { 1.0, 10.0,  2.0 },
                        new double?[] {11.0, 15.0, 13.0 },
                        new double?[] {15.0, 17.0, 16.0 },
                        new double?[] {17.0, 18.0, 17.5 },
                        new double?[] {20.0, 30.0, 25.0 }
                    }));

                // Paste below minimum start value.
                ClipboardEx.SetText("0\n10\n20\n");
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 1);
                        editDlg.MarginType = EditIsolationSchemeDlg.WindowMargin.NONE;
                        editDlg.SpecifyTarget = true;
                    });
                RunDlg<MessageDlg>(() => editDlg.IsolationWindowGrid.OnPaste(), messageDlg =>
                    {
                        AssertEx.Contains(messageDlg.Message,
                            @"On line 1, Isolation window Start must be between");
                        messageDlg.OkDialog();
                    });
                RunUI(() => VerifyCellValues(editDlg, new[]
                    {
                        new double?[] { 1.0, 10.0,  2.0 },
                        new double?[] {11.0, 15.0, 13.0 },
                        new double?[] {15.0, 17.0, 16.0 },
                        new double?[] {17.0, 18.0, 17.5 },
                        new double?[] {20.0, 30.0, 25.0 }
                    }));

                // Paste above maximum end value.
                ClipboardEx.SetText("1\n10\n10001\n");
                RunUI(() =>
                    {
                        editDlg.IsolationWindowGrid.SelectCell((int) EditIsolationSchemeDlg.GridColumns.end, 1);
                        editDlg.MarginType = EditIsolationSchemeDlg.WindowMargin.NONE;
                        editDlg.SpecifyTarget = true;
                    });
                RunDlg<MessageDlg>(() => editDlg.IsolationWindowGrid.OnPaste(), messageDlg =>
                    {
                        // NOTE: Because of the order of processing, the out of range value at the end
                        // of the list is flagged as being a Start value, when it is really only used
                        // as the end of the previous interval.  Fixing that would require some work.
                        AssertEx.Contains(messageDlg.Message,
                            @"On line 3, Isolation window Start must be between");
                        messageDlg.OkDialog();
                    });
                RunUI(() => VerifyCellValues(editDlg, new[]
                    {
                        new double?[] { 1.0, 10.0,  2.0 },
                        new double?[] {11.0, 15.0, 13.0 },
                        new double?[] {15.0, 17.0, 16.0 },
                        new double?[] {17.0, 18.0, 17.5 },
                        new double?[] {20.0, 30.0, 25.0 }
                    }));

                OkDialog(editDlg, editDlg.OkDialog);
                OkDialog(editList, editList.OkDialog);
                OkDialog(fullScanDlg, fullScanDlg.OkDialog);
            }
        }

        private static void VerifyCellValues(EditIsolationSchemeDlg editDlg, double?[][] expectedValues)
        {
            // Verify expected number of rows.
            Assert.AreEqual(editDlg.IsolationWindowGrid.RowCount, expectedValues.Length+1); // Grid always shows an extra row.
            
            var visibleColumns = editDlg.IsolationWindowGrid.VisibleColumnCount;

            for (int row = 0; row < expectedValues.Length; row++)
            {
                // Verify expected number of columns.
                Assert.AreEqual(visibleColumns, expectedValues[row].Length);

                for (int col = 0; col < expectedValues[row].Length; col++)
                {
                    var expectedValue = expectedValues[row][col];

                    // If not specifying target, adjust column index to access margins.
                    var adjustedCol = (col >= 2 && !editDlg.SpecifyTarget) ? col+1 : col;

                    // Verify cell value.
                    var actualValue = editDlg.IsolationWindowGrid.GetCellValue(adjustedCol, row);
                    if (expectedValue.HasValue)
                    {
                        Assert.AreEqual(double.Parse(actualValue), expectedValue);
                    }
                    else
                    {
                        Assert.AreEqual(actualValue, string.Empty);
                    }
                }
            }
        }
    }
}
