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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies <see cref="JsonUiService.RunWithDialogWatch{T}"/>: when connector work (e.g.
    /// RunCommand or SetFormValue) pops a modal alert, the call returns immediately by throwing with
    /// the alert's text, instead of blocking on the dialog. The alert is left open for the caller to
    /// dismiss. Also verifies <see cref="JsonUiService.InvokeMenuItem"/> fails fast while a modal
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

            // The alert is still open and blocking the main window. InvokeMenuItem must fail fast --
            // throwing with the blocking alert's text rather than silently no-opping on the disabled menu.
            // (The blocked check runs before any menu lookup, so the menu path is immaterial.)
            var alert = WaitForOpenForm<AlertDlg>();
            AssertEx.ThrowsException<Exception>(
                () => JsonUiService.InvokeMenuItem(@"File > New"),
                thrown => AssertEx.Contains(thrown.Message, alertMessage));

            // Dismiss the alert the way the model would over its connection, which also unblocks the
            // orphaned work thread.
            OkDialog(alert, alert.ClickOk);
        }
    }
}
