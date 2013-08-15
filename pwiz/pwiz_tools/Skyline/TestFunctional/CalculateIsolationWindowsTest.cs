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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class CalculateIsolationWindowsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCalculateIsolationWindows()
        {
            RunFunctionalTest();
        }

        private EditIsolationSchemeDlg _editDlg;
        private CalculateIsolationSchemeDlg _calcDlg;

        protected override void DoTest()
        {
            // Display full scan tab.
            var fullScanDlg = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
                {
                    fullScanDlg.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                    fullScanDlg.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                });

            // Open the isolation scheme dialog and calculate dialog.
            _editDlg = ShowDialog<EditIsolationSchemeDlg>(fullScanDlg.AddIsolationScheme);
            RunUI(() => _editDlg.UseResults = false);
            _calcDlg = ShowDialog<CalculateIsolationSchemeDlg>(_editDlg.Calculate);

            // Check Start values.
            CheckError(() => _calcDlg.Start = null, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, 1);
            CheckError(() => _calcDlg.Start = 49, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, 2);
            CheckError(() => _calcDlg.Start = 2001, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_less_than_or_equal_to__1__, 2);
            CheckError(() => _calcDlg.Start = 100, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, 1);

            // Check End values.
            CheckError(() => _calcDlg.End = 49, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, 2);
            CheckError(() => _calcDlg.End = 2001, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_less_than_or_equal_to__1__, 2);
            CheckError(() => _calcDlg.End = 100, Resources.CalculateIsolationSchemeDlg_OkDialog_Start_value_must_be_less_than_End_value);
            CheckError(() => _calcDlg.End = 101, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, 1);

            // Check WindowWidth values.
            CheckError(() => _calcDlg.WindowWidth = 0.99, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, 2);
            CheckError(() => _calcDlg.WindowWidth = 1951, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_less_than_or_equal_to__1__, 2);
            CheckError(() => _calcDlg.WindowWidth = 1950, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_less_than_or_equal_to__1__, 2);
            CheckError(() => _calcDlg.WindowWidth = 1);

            // Check Overlap values.
            CheckError(() =>
                {
                    _calcDlg.Start = 100;
                    _calcDlg.End = 101;
                    _calcDlg.WindowWidth = 1;
                    _calcDlg.Overlap = -1;
                },
                Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, 2);
            CheckError(() => _calcDlg.Overlap = 100, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_less_than_or_equal_to__1__, 2);
            CheckError(() => _calcDlg.Overlap = 50);

            // Check Overlap/Optimize conflict.
            RunDlg<MultiButtonMsgDlg>(() => 
                {
                    _calcDlg.Start = 100;
                    _calcDlg.End = 101;
                    _calcDlg.WindowWidth = 1;
                    _calcDlg.Overlap = 50;
                    _calcDlg.OptimizeWindowPlacement = true;
                },
                msgDlg =>
                {
                    AssertEx.Contains(msgDlg.Message, Resources.CalculateIsolationSchemeDlg_cbOptimizeWindowPlacement_CheckedChanged_Window_optimization_cannot_be_applied_to_overlapping_isolation_windows_Click_OK_to_remove_overlap_or_Cancel_to_cancel_optimization);
                    msgDlg.Btn1Click();
                });
            RunUI(() =>
                {
                    Assert.IsTrue(_calcDlg.OptimizeWindowPlacement);
                    Assert.AreEqual(_calcDlg.Overlap, null);
                    _calcDlg.OptimizeWindowPlacement = false;
                });

            RunDlg<MultiButtonMsgDlg>(() =>
                {
                    _calcDlg.Start = 100;
                    _calcDlg.End = 101;
                    _calcDlg.WindowWidth = 1;
                    _calcDlg.Overlap = 50;
                    _calcDlg.OptimizeWindowPlacement = true;
                },
                msgDlg =>
                {
                    AssertEx.Contains(msgDlg.Message, Resources.CalculateIsolationSchemeDlg_cbOptimizeWindowPlacement_CheckedChanged_Window_optimization_cannot_be_applied_to_overlapping_isolation_windows_Click_OK_to_remove_overlap_or_Cancel_to_cancel_optimization);
                    msgDlg.BtnCancelClick();
                });
            RunUI(() =>
            {
                Assert.IsFalse(_calcDlg.OptimizeWindowPlacement);
                Assert.AreEqual(_calcDlg.Overlap, 50);
            });


            // Check Margin values.
            CheckError(() =>
                {
                    _calcDlg.Start = 100;
                    _calcDlg.End = 101;
                    _calcDlg.WindowWidth = 1;
                    _calcDlg.Overlap = null;
                    _calcDlg.Margins = CalculateIsolationSchemeDlg.WindowMargin.SYMMETRIC;
                    _calcDlg.MarginLeft = null;
                },
                Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, 1);
            CheckError(() => _calcDlg.MarginLeft = 0, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, 2);
            CheckError(() => _calcDlg.MarginLeft = 1951, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_less_than_or_equal_to__1__, 2);
            CheckError(() => _calcDlg.MarginLeft = 1900, Resources.IsolationWindow_DoValidate_Isolation_window_margins_cover_the_entire_isolation_window_at_the_extremes_of_the_instrument_range);
            CheckError(() =>
                {
                    _calcDlg.Margins = CalculateIsolationSchemeDlg.WindowMargin.ASYMMETRIC;
                    _calcDlg.MarginLeft = 1;
                    _calcDlg.MarginRight = null;
                },
                Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, 1);
            CheckError(() => _calcDlg.MarginRight = 0, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, 2);
            CheckError(() => _calcDlg.MarginRight = 1951, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_less_than_or_equal_to__1__, 2);
            CheckError(() => _calcDlg.MarginRight = 1900, Resources.IsolationWindow_DoValidate_Isolation_window_margins_cover_the_entire_isolation_window_at_the_extremes_of_the_instrument_range);
            CheckError(() => _calcDlg.MarginRight = 3);

            // One simple window.
            CheckWindows(() =>
                {
                    _calcDlg.Start = 100;
                    _calcDlg.End = 101;
                    _calcDlg.WindowWidth = 1;
                },
                100, 101, null, null, null);

            // Two simple windows with overlap.
            CheckWindows(() =>
                {
                    _calcDlg.Start = 100;
                    _calcDlg.End = 101;
                    _calcDlg.WindowWidth = 1;
                    _calcDlg.Overlap = 50;
                },
                100, 101, null, null, null,
                100.5, 101.5, null, null, null);

            // One max-range window.
            CheckWindows(() =>
                {
                    _calcDlg.Start = 50;
                    _calcDlg.End = 2000;
                    _calcDlg.WindowWidth = 1950;
                },
                50, 2000, null, null, null);

            // One max-range window with asymmetric margins and centered target.
            CheckWindows(() =>
                {
                    _calcDlg.Start = 50;
                    _calcDlg.End = 2000;
                    _calcDlg.WindowWidth = 1950;
                    _calcDlg.Margins = CalculateIsolationSchemeDlg.WindowMargin.ASYMMETRIC;
                    _calcDlg.MarginLeft = 5;
                    _calcDlg.MarginRight = 25;
                    _calcDlg.GenerateTarget = true;
                },
                55, 1975, 1025, 5, 25);

            // Now with window optimization.
            CheckWindows(() =>
                {
                    _calcDlg.Start = 50;
                    _calcDlg.End = 1900;
                    _calcDlg.WindowWidth = 1850;
                    _calcDlg.Margins = CalculateIsolationSchemeDlg.WindowMargin.ASYMMETRIC;
                    _calcDlg.MarginLeft = 5;
                    _calcDlg.MarginRight = 25;
                    _calcDlg.GenerateTarget = true;
                    _calcDlg.OptimizeWindowPlacement = true;
                },
                55, 1901.1140, 988.0570, 5, 25);

            // Four windows that fit exactly.
            CheckWindows(() =>
                {
                    _calcDlg.Start = 100;
                    _calcDlg.End = 200;
                    _calcDlg.WindowWidth = 25;
                    _calcDlg.Margins = CalculateIsolationSchemeDlg.WindowMargin.ASYMMETRIC;
                    _calcDlg.MarginLeft = 1;
                    _calcDlg.MarginRight = 2;
                },
                100, 125, null, 1, 2,
                125, 150, null, 1, 2,
                150, 175, null, 1, 2,
                175, 200, null, 1, 2);

            // Four windows that don't fit exactly.
            CheckWindows(() =>
                {
                    _calcDlg.Start = 100;
                    _calcDlg.End = 200;
                    _calcDlg.WindowWidth = 33;
                    _calcDlg.Margins = CalculateIsolationSchemeDlg.WindowMargin.SYMMETRIC;
                    _calcDlg.MarginLeft = 1;
                },
                100, 133, null, 1, null,
                133, 166, null, 1, null,
                166, 199, null, 1, null,
                199, 232, null, 1, null);

            // One optimized window.
            CheckWindows(() =>
                {
                    _calcDlg.Start = 100;
                    _calcDlg.End = 101;
                    _calcDlg.WindowWidth = 1;
                    _calcDlg.OptimizeWindowPlacement = true;
                },
                100.2955, 101.2959, null, null, null);

            // More than max number of windows.
            RunUI(() =>
                {
                    _calcDlg.Start = 100;
                    _calcDlg.End = 2000;
                    _calcDlg.WindowWidth = 1;

                    // Cover miscellaneous Get methods.
                    string x = _calcDlg.Start + _calcDlg.End + _calcDlg.WindowWidth +
                        _calcDlg.Margins + _calcDlg.MarginLeft + _calcDlg.MarginRight;
                    bool t = _calcDlg.GenerateTarget;
                    Assert.IsTrue(t || x != null); // Just using these variables so ReSharper won't complain.
                });

            // Cancel all dialogs to conclude test.
            OkDialog(_calcDlg, _calcDlg.CancelButton.PerformClick);
            OkDialog(_editDlg, _editDlg.CancelButton.PerformClick);
            OkDialog(fullScanDlg, fullScanDlg.CancelButton.PerformClick);
        }

        // Set dialog values, and check for the expected error message.
        private void CheckError(Action func, string errorMessage = null, int replacement = 0)
        {
            RunUI(func);
            if (errorMessage == null)
            {
                OkDialog(_calcDlg, _calcDlg.OkDialog);
                _calcDlg = ShowDialog<CalculateIsolationSchemeDlg>(_editDlg.Calculate);
            }
            else
            {
                RunDlg<MessageDlg>(_calcDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(errorMessage,messageDlg.Message,replacement);
                    messageDlg.OkDialog();
                });
            }
        }

        // Set dialog values and check the calculated isolation window values.  Finally, OK the calculation dialog and open a fresh one.
        private void CheckWindows(Action act, params double?[] args)
        {
            RunUI(() =>
                {
                    act();
                    var isolationWindows = _calcDlg.IsolationWindows;
                    Assert.AreEqual(isolationWindows.Count*5, args.Length, "Expected {0} isolation windows, but got {1}.", args.Length / 5, isolationWindows.Count);
                    int i = 0;
                    foreach (var window in isolationWindows)
                    {
                        CheckValue(window.Start, args[i++], "Start");
                        CheckValue(window.End, args[i++], "End");
                        CheckValue(window.Target, args[i++], "Target");
                        CheckValue(window.StartMargin, args[i++], "Start margin");
                        CheckValue(window.EndMargin, args[i++], "End margin");
                    }
                });

            OkDialog(_calcDlg, _calcDlg.OkDialog);
            _calcDlg = ShowDialog<CalculateIsolationSchemeDlg>(_editDlg.Calculate);
        }

        // Check a nullable value for the expected result.
        private void CheckValue(double? actual, double? expected, string name)
        {
            if (!actual.HasValue)
            {
                Assert.IsTrue(!expected.HasValue, "Expected a value for {0}, but none was calculated.", name);
                return;
            }
            else
            {
                Assert.IsTrue(expected.HasValue, "No value expected for {0}, but one was calculated.", name);
            }
            Assert.AreEqual(expected.Value, actual.Value, 0.0001, "Value for {0} differs from expected value.", name);
        }
    }
}
