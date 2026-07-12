/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies <see cref="JsonUiService.RunWithDialogWatch{T}"/>: when connector work (e.g.
    /// RunCommand or SetFormValue) pops a modal alert, the call returns immediately by throwing with
    /// the alert's text, instead of blocking on the dialog. The alert is left open for the caller to
    /// dismiss. Also verifies <see cref="JsonToolServer.ClickMainMenuItem"/> fails fast while a modal
    /// dialog is blocking the main window.
    /// </summary>
    [TestClass]
    public class AlertWatchTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAlertWatch()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string alertMessage = @"Connector alert-watch test message";

            // Drive the verb through the running JSON tool server (torn down with the window).
            RunUI(() => Program.StartToolService());

            // The work pops a modal alert on the UI thread; RunWithAlertWatch (running on this test
            // thread) must detect it and throw with the alert text rather than block. The test cares only
            // that it throws with the alert's text, not what exception type carries it.
            AssertEx.ThrowsException<Exception>(
                () => JsonUiService.RunWithDialogWatch(() =>
                {
                    JsonUiService.InvokeOnUiThread(() =>
                    {
                        using (var dlg = new AlertDlg(alertMessage, MessageBoxButtons.OK))
                            dlg.ShowDialog(SkylineWindow);
                        return true;
                    });
                    return true;
                }),
                thrown => AssertEx.Contains(thrown.Message, alertMessage));

            // The alert is still open and blocking the main window. ClickMainMenuItem must fail fast --
            // throwing with the blocking alert's text rather than silently no-opping on the disabled menu.
            // (The blocked check runs before any menu lookup, so the menu path is immaterial.)
            var alert = WaitForOpenForm<AlertDlg>();
            AssertEx.ThrowsException<Exception>(
                () => Program.MainJsonToolServer.ClickMainMenuItem(@"File > New"),
                thrown => AssertEx.Contains(thrown.Message, alertMessage));

            // Dismiss the alert the way the model would over its connection, which also unblocks the
            // orphaned work thread.
            OkDialog(alert, alert.ClickOk);

            // A NATIVE message box (a Win32 #32770 with no managed Form, e.g. from MessageBox.Show) must surface
            // its message BODY -- not its caption -- through the dialog-watch. The work pops the box on the UI
            // thread (blocking it); RunWithDialogWatch (on this test thread) detects the native modal and throws
            // with the body text read from the box's child controls.
            const string nativeBody = @"The native message box body the connector should surface";
            const string nativeCaption = @"AlertWatchNativeBoxCaption";
            AssertEx.ThrowsException<Exception>(
                () => JsonUiService.RunWithDialogWatch(() =>
                {
                    JsonUiService.InvokeOnUiThread(() =>
                    {
                        MessageBox.Show(SkylineWindow, nativeBody, nativeCaption, MessageBoxButtons.OK);
                        return true;
                    });
                    return true;
                }),
                thrown =>
                {
                    AssertEx.Contains(thrown.Message, nativeBody);              // the BODY was surfaced
                    Assert.IsFalse(thrown.Message.Contains(nativeCaption),      // not the caption (old behavior)
                        string.Format(@"Expected the message body, not the caption, but got: {0}", thrown.Message));
                });

            // The native box has no managed Form (WaitForOpenForm cannot reach it); dismiss it by posting WM_CLOSE
            // to the #32770 window, which also unblocks the orphaned work thread.
            DismissNativeMessageBox(nativeCaption);
        }

        // Finds the process's native message-box window (#32770) with the given caption, waits for it to close
        // after posting WM_CLOSE, so nothing is left blocking at teardown.
        private static void DismissNativeMessageBox(string caption)
        {
            IntPtr found = IntPtr.Zero;
            for (int i = 0; i < 200 && found == IntPtr.Zero; i++)
            {
                found = FindNativeMessageBox(caption);
                if (found == IntPtr.Zero)
                    Thread.Sleep(50);
            }
            Assert.AreNotEqual(IntPtr.Zero, found, @"Native message box window not found to dismiss.");
            User32.PostMessageA(found, User32.WinMessageType.WM_CLOSE, 0, 0);
            for (int i = 0; i < 200 && FindNativeMessageBox(caption) != IntPtr.Zero; i++)
                Thread.Sleep(50);
        }

        // The handle of a top-level #32770 (native dialog box) window whose caption matches, or Zero if none.
        private static IntPtr FindNativeMessageBox(string caption)
        {
            return User32.EnumWindows().FirstOrDefault(hwnd =>
                User32.GetClassName(hwnd) == @"#32770" && User32.GetWindowText(hwnd) == caption);
        }
    }
}
