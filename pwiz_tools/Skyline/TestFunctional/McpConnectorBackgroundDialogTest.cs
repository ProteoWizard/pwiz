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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Drives the ONE dialog Skyline shows on a thread of its own: the BackgroundThreadLongWaitDlg that
    /// <see cref="LongOperationRunner"/> puts up for a long operation started on the UI thread. It is the inverse of
    /// every other connector scenario -- the work runs on the MAIN UI thread, which is therefore not pumping, while
    /// the dialog runs its own message loop on its own thread. So the connector cannot reach it through the main
    /// window: it has to enumerate the window off any thread and then marshal to THAT dialog's thread.
    ///
    /// <para>Cancelling is the whole point. That dialog is the only thing a user can act on to stop the operation, so
    /// a connector that cannot drive it cannot stop work it started -- and the main window will not be free again
    /// until that work finishes. (The work here also stops when the test tells it to, which is only a safety net so
    /// that a failure cannot leave the UI thread wedged; nothing but the dialog stops it in the product.)</para>
    ///
    /// <para>The operation is started from inside the List Designer, itself opened from the Document Settings dialog,
    /// so the background dialog appears while two managed modals are already on the main thread. That nesting is part
    /// of what is under test: DismissWithCancelButton has to ride the modal count back to the level it was at before
    /// the gesture, which is only meaningful at a depth greater than zero.</para>
    /// </summary>
    [TestClass]
    public class McpConnectorBackgroundDialogTest : McpConnectorTest
    {
        // Set by the operation once it has SEEN the cancel, so the test can prove the work stopped BECAUSE it was
        // cancelled -- not merely that the dialog closed. Plain volatile fields rather than wait handles: on a failing
        // run the work is still going when the test gives up, and a handle disposed here would then be touched by it.
        private volatile bool _workSawCancel;

        // Set by the test to release the main UI thread however the test ended. Without it, a failure before the
        // cancel lands would leave the main thread wedged in the operation through teardown.
        private volatile bool _stopWork;

        // Last-resort bound, in case the test thread dies without running its finally. Deliberately far longer than
        // any real run: the stop flag is what normally ends the work, so this must never be the thing that fires
        // first. A short bound here would race the connector round-trip on a slow or heavily loaded agent -- the
        // harness scales its own waits (GetWaitCycles multiplies for Debug builds, stress runs and parallel
        // TestRunner processes) and a fixed timeout here would not scale with them.
        private const int WORK_BACKSTOP_MINUTES = 10;

        [TestMethod]
        public void TestMcpConnectorBackgroundDialog()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            StartToolService();

            // Two managed modals on the main thread, so the background dialog appears at a modal depth greater than
            // zero (see the class comment). The List Designer is also a natural owner for a long operation.
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var listDesigner = ShowDialog<ListDesigner>(documentSettingsDlg.AddList);
            RunUI(() => listDesigner.ListName = @"BigList");

            // No JobTitle: it would become the dialog's caption, i.e. an unlocalized user-visible string, and the
            // connector finds the dialog by type rather than by caption. Leaving it null keeps the default.
            var runner = new LongOperationRunner
            {
                ParentControl = listDesigner
            };

            try
            {
                // Start it WITHOUT waiting: LongOperationRunner runs the work on the CALLING thread and puts only the
                // dialog on a thread of its own, so this occupies the MAIN UI thread -- a blocking call here would
                // leave no one to drive that dialog. The test thread goes on to drive it through the connector.
                //
                // The work is a plain wait rather than a real workload on purpose. What this test covers is the
                // THREADING -- a dialog pumping on its own thread while the thread that started the work is wedged --
                // and a workload here would only add window churn (an earlier version pasted rows through the grid,
                // creating thousands of short-lived editing-control windows per run) without testing anything more.
                listDesigner.BeginInvoke((Action) (() => runner.Run(broker =>
                {
                    var backstop = DateTime.UtcNow.AddMinutes(WORK_BACKSTOP_MINUTES);
                    while (!broker.IsCanceled && !_stopWork && DateTime.UtcNow < backstop)
                        Thread.Sleep(20);
                    _workSawCancel = broker.IsCanceled;
                })));

                // The dialog is a window like any other to the connector -- found by enumerating the top-level
                // windows, which needs no thread at all, NOT by asking the main window (which is busy running the
                // operation).
                string dialogId = WaitForMcpConnectorForm(@"BackgroundThreadLongWaitDlg");

                // Read it: this must land on the DIALOG's thread. The main window's thread is inside the operation
                // and would never run the read.
                var controls = McpConnector.GetControls(dialogId);
                Assert.IsTrue(controls.Any(control => Equals(control.Path.Type, @"Button")),
                    @"The background dialog's Cancel button was not read back.");

                // Cancel it, which is the only way to stop the operation.
                AssertComplete(McpConnector.DismissWithCancelButton(dialogId));

                // The operation SAW the cancel -- it did not just stop for some other reason and look cancelled.
                // Waited for on the TEST thread: the main thread is still inside the operation until it observes the
                // cancel.
                WaitForCondition(() => _workSawCancel, @"The background operation was not cancelled.");

                // The dialog is gone, which means the operation it was reporting on stopped -- and the main window's
                // thread is free again, which is what lets this read run at all.
                WaitForConditionUI(() => !McpConnector.GetOpenForms()
                    .Any(form => Equals(form.Type, @"BackgroundThreadLongWaitDlg")));
            }
            finally
            {
                _stopWork = true;   // release the main UI thread however this ended
            }

            // The List Designer has no cancel button of its own (FormEx.CancelDialog would throw); just close it.
            OkDialog(listDesigner, listDesigner.Close);
            OkDialog(documentSettingsDlg, documentSettingsDlg.CancelDialog);
        }
    }
}
