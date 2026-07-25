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
    /// <para>Cancelling is the whole point. The dialog is the only way to stop the operation, so a connector that
    /// cannot drive it cannot stop work it started -- and the main window will not be free again until that work
    /// finishes.</para>
    /// </summary>
    [TestClass]
    public class McpConnectorBackgroundDialogTest : McpConnectorTest
    {
        [TestMethod]
        public void TestMcpConnectorBackgroundDialog()
        {
            RunFunctionalTest();
        }

        // Set by the operation itself once it has SEEN the cancel, so the test can prove the work stopped BECAUSE it
        // was cancelled -- not merely that the dialog closed. A plain volatile field rather than a wait handle: on a
        // failing run the work is still going when the test gives up, and a handle disposed here would then be set
        // by it.
        private volatile bool _workSawCancel;

        // How long the work holds the main UI thread if the cancel never arrives. Comfortably longer than the
        // connector needs, and comfortably SHORTER than the wait for it below, so that on a failing run the main
        // thread is free again before the test gives up and teardown is clean.
        private const int WORK_GIVE_UP_SECONDS = 60;

        protected override void DoTest()
        {
            StartToolService();

            _workSawCancel = false;
            var runner = new LongOperationRunner
            {
                ParentControl = SkylineWindow,
                JobTitle = @"Background dialog test operation"
            };

            // Start it WITHOUT waiting: LongOperationRunner runs the work on the CALLING thread and puts only the
            // dialog on a thread of its own, so this occupies the MAIN UI thread -- a blocking call here would
            // leave no one to drive that dialog. The test thread goes on to drive it through the connector.
            //
            // The work is a plain wait rather than a real workload on purpose. What this test covers is the
            // THREADING -- a dialog pumping on its own thread while the thread that started the work is wedged --
            // and any actual work here would only add churn (an earlier version pasted 50,000 rows through a grid,
            // which created ~100,000 short-lived editing-control windows per run and made the test both slow and a
            // nightly heap-leak reporter, without testing anything more).
            SkylineWindow.BeginInvoke((Action) (() => runner.Run(broker =>
            {
                // Bounded, so that a cancel which never arrives FAILS the test instead of hanging it: the main
                // thread is wedged in here, so a loop with no exit of its own would leave nothing able to end the
                // run. Reaching the bound leaves _workSawCancel false, which the wait below reports.
                var giveUpAt = DateTime.UtcNow.AddSeconds(WORK_GIVE_UP_SECONDS);
                while (!broker.IsCanceled && DateTime.UtcNow < giveUpAt)
                    Thread.Sleep(20);
                _workSawCancel = broker.IsCanceled;
            })));

            // The dialog is a window like any other to the connector -- found by enumerating the top-level windows,
            // which needs no thread at all, NOT by asking the main window (which is busy running the operation).
            string dialogId = WaitForMcpConnectorForm(@"BackgroundThreadLongWaitDlg");

            // Read it: this must land on the DIALOG's thread. The main window's thread is inside the operation and
            // would never run the read.
            var controls = McpConnector.GetControls(dialogId);
            Assert.IsTrue(controls.Any(control => Equals(control.Path.Type, @"Button")),
                @"The background dialog's Cancel button was not read back.");

            // Cancel it, which is the only way to stop the operation.
            AssertComplete(McpConnector.DismissWithCancelButton(dialogId));

            // The operation SAW the cancel -- it did not just run to completion and look cancelled. Waited for on the
            // TEST thread: the main thread is still inside the operation until it observes the cancel.
            WaitForCondition(() => _workSawCancel, @"The background operation was not cancelled.");

            // The dialog is gone, which means the operation it was reporting on stopped -- and the main window's
            // thread is free again, which is what lets this read run at all.
            WaitForConditionUI(() => !McpConnector.GetOpenForms()
                .Any(form => Equals(form.Type, @"BackgroundThreadLongWaitDlg")));
        }
    }
}
