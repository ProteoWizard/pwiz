/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ReportErrorDlgTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestReportErrorDlg()
        {
            TestFilesZip = @"TestFunctional\StartPageTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        { 
            var skylineWindow = WaitForOpenForm<SkylineWindow>();
            RunUI(() => skylineWindow.OpenFile(TestFilesDir.GetTestPath("StartPageTest.sky")));            
            ReportException(new Exception());
            var reportErrorDlg = WaitForOpenForm<ReportErrorDlg>();
            RunDlg<DetailedReportErrorDlg>(reportErrorDlg.OkDialog, skippedReportForm2 =>
            {
                Assert.IsNotNull(skippedReportForm2);
                Assert.IsTrue(skippedReportForm2.ScreenShots.Count > 0);
                skippedReportForm2.IsTest = true;
                skippedReportForm2.SetFormProperties(true, true, "yuval@uw.edu", "text");
                skippedReportForm2.OkDialog(false);
                Assert.IsNotNull(skippedReportForm2.SkylineFileBytes);
                Assert.AreNotEqual(0, skippedReportForm2.SkylineFileBytes.Length);
                Assert.IsTrue(skippedReportForm2.SkylineFileBytes.Length < ReportErrorDlg.MAX_ATTACHMENT_SIZE);
            });
            WaitForClosedForm(reportErrorDlg);

            ReportException(new Exception());
            var reportErrorDlg2 = WaitForOpenForm<ReportErrorDlg>();
            var skippedReportForm= ShowDialog<DetailedReportErrorDlg>(reportErrorDlg2.OkDialog);
            RunUI(()=>
            {
                Assert.IsNotNull(skippedReportForm);
                Assert.IsTrue(skippedReportForm.ScreenShots.Count > 0);
                skippedReportForm.IsTest = true;
                skippedReportForm.SetFormProperties(true, true, "yuval", "text");
            });
            var messageDlg = ShowDialog<MessageDlg>(() => skippedReportForm.OkDialog(false));
            RunUI(() => Assert.AreEqual(messageDlg.Message, Resources.SkippedReportErrorDlg_btnOK_Click_No_Email));
            OkDialog(messageDlg, messageDlg.OkDialog);
            RunUI(() =>
            {
                Assert.IsNotNull(skippedReportForm);
                Assert.IsTrue(skippedReportForm.ScreenShots.Count > 0);
                skippedReportForm.IsTest = true;
                skippedReportForm.SetFormProperties(true, true, "yuval@uw.edu", "text");
            });
            OkDialog(skippedReportForm, () => skippedReportForm.OkDialog(false));
            WaitForClosedForm(reportErrorDlg2);

            // Add 50,000 peptides to the document so that its size will exceed ReportErrorDlg.MAX_ATTACHMENT_SIZE
            var peptideSequences = RescoreInPlaceTest.PermuteString("ELVISLIVES").Distinct().Take(50_000);
            RunUI(() =>
            {
                SkylineWindow.Paste(TextUtil.LineSeparate(peptideSequences));
            });

            // Verify that the "Report Error" menu item on the Help menu is hidden unless the user holds down the shift key
            ToolStripMenuItem submitErrorReportMenuItem = null;
            ToolStripMenuItem crashSkylineMenuItem = null;
            RunUI(() =>
            {
                var helpMenuItem = skylineWindow.MainMenuStrip.Items.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(item => item.Name == "helpToolStripMenuItem");
                Assert.IsNotNull(helpMenuItem);

                SetShiftKeyState(false, false);
                Assert.AreEqual(Keys.None, Control.ModifierKeys & (Keys.Shift | Keys.Control));
                helpMenuItem.ShowDropDown();
                submitErrorReportMenuItem = helpMenuItem.DropDownItems.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(item => item.Name == "submitErrorReportMenuItem");
                Assert.IsNotNull(submitErrorReportMenuItem);
                Assert.IsFalse(submitErrorReportMenuItem.Visible);
                crashSkylineMenuItem = helpMenuItem.DropDownItems.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(item => item.Name == "crashSkylineMenuItem");
                Assert.IsNotNull(crashSkylineMenuItem);
                Assert.IsFalse(crashSkylineMenuItem.Visible);
                helpMenuItem.HideDropDown();
                
                SetShiftKeyState(true, false);
                Assert.AreEqual(Keys.Shift, Control.ModifierKeys & (Keys.Shift | Keys.Control));
                helpMenuItem.ShowDropDown();
                submitErrorReportMenuItem = helpMenuItem.DropDownItems.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(item => item.Name == "submitErrorReportMenuItem");
                Assert.IsNotNull(submitErrorReportMenuItem);
                Assert.IsTrue(submitErrorReportMenuItem.Visible);
                crashSkylineMenuItem = helpMenuItem.DropDownItems.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(item => item.Name == "crashSkylineMenuItem");
                Assert.IsNotNull(crashSkylineMenuItem);
                Assert.IsFalse(crashSkylineMenuItem.Visible);
                helpMenuItem.HideDropDown();

                SetShiftKeyState(true, true);
                Assert.AreEqual(Keys.Shift | Keys.Control, Control.ModifierKeys & (Keys.Shift | Keys.Control));
                helpMenuItem.ShowDropDown();
                submitErrorReportMenuItem = helpMenuItem.DropDownItems.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(item => item.Name == "submitErrorReportMenuItem");
                Assert.IsNotNull(submitErrorReportMenuItem);
                Assert.IsTrue(submitErrorReportMenuItem.Visible);
                crashSkylineMenuItem = helpMenuItem.DropDownItems.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(item => item.Name == "crashSkylineMenuItem");
                Assert.IsNotNull(crashSkylineMenuItem);
                Assert.IsTrue(crashSkylineMenuItem.Visible);
                helpMenuItem.HideDropDown();

                SetShiftKeyState(false, false);
                Assert.AreEqual(Keys.None, Control.ModifierKeys & Keys.Shift);
            });
            Assert.IsNotNull(submitErrorReportMenuItem);

            // Use the hidden help menu item to bring up the ReportErrorDlg and verify that the document bytes are
            // truncated to MAX_ATTACHMENT_SIZE
            using (new StoreExceptions())
            {
                SkylineWindow.BeginInvoke(new Action(() =>
                {
                    submitErrorReportMenuItem.PerformClick();
                }));
                WaitForCondition(10000, () => null != FindOpenForm<ReportErrorDlg>(), throwOnProgramException: false);
            }

            var reportErrorDlg3 = FindOpenForm<ReportErrorDlg>();
            Assert.IsNotNull(reportErrorDlg3);
            RunDlg<DetailedReportErrorDlg>(reportErrorDlg3.OkDialog, detailedDlg =>
            {
                Assert.IsNotNull(detailedDlg);
                Assert.IsTrue(detailedDlg.ScreenShots.Count > 0);
                detailedDlg.IsTest = true;
                detailedDlg.SetFormProperties(true, true, "yuval@uw.edu", "text");
                detailedDlg.OkDialog(false);
                Assert.IsNotNull(detailedDlg.SkylineFileBytes);
                Assert.AreEqual(ReportErrorDlg.MAX_ATTACHMENT_SIZE, detailedDlg.SkylineFileBytes.Length);
            });
            WaitForClosedForm(reportErrorDlg3);
        }

        [DllImport("user32.dll")]
        static extern bool SetKeyboardState(byte[] lpKeyState);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetKeyboardState(byte[] lpKeyState);
        private void SetShiftKeyState(bool shiftPressed, bool ctrlPressed)
        {
            var keyStates = new byte[256];
            Assert.IsTrue(GetKeyboardState(keyStates));
            var shiftKeyState = keyStates[(int)Keys.ShiftKey];
            if (shiftPressed)
            {
                shiftKeyState |= 0x80;
            }
            else
            {
                shiftKeyState = (byte)(shiftKeyState & ~0x80);
            }
            keyStates[(int)Keys.ShiftKey] = shiftKeyState;
            var ctrlKeyState = keyStates[(int)Keys.ControlKey];
            if (ctrlPressed)
            {
                ctrlKeyState |= 0x80;
            }
            else
            {
                ctrlKeyState = (byte)(ctrlKeyState & ~0x80);
            }

            keyStates[(int)Keys.ControlKey] = ctrlKeyState;
            Assert.IsTrue(SetKeyboardState(keyStates));
            Assert.AreEqual(shiftPressed, 0 != (Control.ModifierKeys & Keys.Shift));
            Assert.AreEqual(ctrlPressed, 0 != (Control.ModifierKeys & Keys.Control));
        }

        private void ReportException(Exception x)
        {
            RunUI(() =>
            {
                using (new StoreExceptions())
                {
                    Program.ReportException(x);
                }
            });
        }

        private class StoreExceptions : IDisposable
        {
            private readonly List<Exception> _programExceptions;
            public StoreExceptions()
            {
                _programExceptions = Program.TestExceptions;
                Program.TestExceptions = null;
            }

            public void Dispose()
            {
                Program.TestExceptions = _programExceptions;
            }
        }
    }
}
