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
            CheckError(() => _calcDlg.Start = null, "Start m/z must contain a decimal value.");
            CheckError(() => _calcDlg.Start = 49, "Start m/z must be greater than or equal to 50.");
            CheckError(() => _calcDlg.Start = 2001, "Start m/z must be less than or equal to 2000.");
            CheckError(() => _calcDlg.Start = 100, "End m/z must contain a decimal value.");

            // Check End values.
            CheckError(() => _calcDlg.End = 49, "End m/z must be greater than or equal to 50.");
            CheckError(() => _calcDlg.End = 2001, "End m/z must be less than or equal to 2000.");
            CheckError(() => _calcDlg.End = 100, "Start value must be less than End value.");
            CheckError(() => _calcDlg.End = 101, "Window width must contain a decimal value.");

            // Check WindowWidth values.
            CheckError(() => _calcDlg.WindowWidth = 0.99, "Window width must be greater than or equal to 1.");
            CheckError(() => _calcDlg.WindowWidth = 1951, "Window width must be less than or equal to 1950.");
            CheckError(() => _calcDlg.WindowWidth = 1950, "Window width must be less than or equal to the isolation range.");
            CheckError(() => _calcDlg.WindowWidth = 1);

            // Check Overlap values.
            CheckError(() =>
                {
                    _calcDlg.Start = 100;
                    _calcDlg.End = 101;
                    _calcDlg.WindowWidth = 1;
                    _calcDlg.Overlap = -1;
                },
                "Overlap must be greater than or equal to 0.");
            CheckError(() => _calcDlg.Overlap = 100, "Overlap must be less than or equal to 99.");
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
                    AssertEx.Contains(msgDlg.Message, "Window optimization cannot be applied to overlapping isolation windows.");
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
                    AssertEx.Contains(msgDlg.Message, "Window optimization cannot be applied to overlapping isolation windows.");
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
                "Margin width must contain a decimal value.");
            CheckError(() => _calcDlg.MarginLeft = 0, "Margin width must be greater than or equal to ");
            CheckError(() => _calcDlg.MarginLeft = 1951, "Margin width must be less than or equal to 1950.");
            CheckError(() => _calcDlg.MarginLeft = 1900, "Isolation window margins cover the entire isolation window at the extremes of the instrument range.");
            CheckError(() =>
                {
                    _calcDlg.Margins = CalculateIsolationSchemeDlg.WindowMargin.ASYMMETRIC;
                    _calcDlg.MarginLeft = 1;
                    _calcDlg.MarginRight = null;
                },
                "Margin width must contain a decimal value.");
            CheckError(() => _calcDlg.MarginRight = 0, "Margin width must be greater than or equal to ");
            CheckError(() => _calcDlg.MarginRight = 1951, "Margin width must be less than or equal to 1950.");
            CheckError(() => _calcDlg.MarginRight = 1900, "Isolation window margins cover the entire isolation window at the extremes of the instrument range.");
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
                    _calcDlg.End = 2000;
                    _calcDlg.WindowWidth = 1950;
                    _calcDlg.Margins = CalculateIsolationSchemeDlg.WindowMargin.ASYMMETRIC;
                    _calcDlg.MarginLeft = 5;
                    _calcDlg.MarginRight = 25;
                    _calcDlg.GenerateTarget = true;
                    _calcDlg.OptimizeWindowPlacement = true;
                },
                55, 1951.1368, 1013.0684, 5, 25,
                1951.1368, 1975, 1973.0684, 5, 25);

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

            // One optimized window (becomes two).
            CheckWindows(() =>
                {
                    _calcDlg.Start = 100;
                    _calcDlg.End = 101;
                    _calcDlg.WindowWidth = 1;
                    _calcDlg.OptimizeWindowPlacement = true;
                },
                99.2950, 100.2955, null, null, null,
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
        private void CheckError(Action func, string errorMessage = null)
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
                    AssertEx.Contains(messageDlg.Message, errorMessage);
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
