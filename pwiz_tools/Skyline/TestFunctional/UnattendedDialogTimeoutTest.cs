/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that the unattended-dialog watchdog in CommonAlertDlg reports the dialog it
    /// actually timed out on, rather than being handed back to the UI in a second dialog that
    /// nobody dismisses either.
    /// <para>Before this was fixed, any generic handler that displayed a caught exception turned
    /// one missed dialog into a cascade: a second timeout whose message quoted the first, thrown
    /// out of a nested modal loop where WinForms bypasses the Application.ThreadException
    /// handler and pops its own ThreadExceptionDialog. Tests reported that as a hang, and the
    /// message naming the dialog they had missed was buried two layers deep. This test drives
    /// that exact shape: time a dialog out, then hand the failure back to the UI the way a
    /// catch handler does.</para>
    /// </summary>
    [TestClass]
    public class UnattendedDialogTimeoutTest : AbstractFunctionalTest
    {
        private const string MESSAGE_TEXT = @"UnattendedDialogTimeoutTest unattended message";
        private const int RETHROW_LIMIT_SECONDS = 5;

        [TestMethod]
        public void TestUnattendedDialogTimeout()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // This test deliberately leaves a dialog unattended, and only the watchdog closes it.
            // CommonAlertDlg.ShowWithTimeout disables the watchdog under a debugger and in any run
            // carrying a pause value, so in those modes the dialog would sit until the hang
            // detector fired. Skip instead of hanging; the nightly runs neither mode.
            if (CommonFormEx.PauseMode || Debugger.IsAttached)
                return;

            RunUI(() =>
            {
                // A dialog nobody dismisses fails with the watchdog timeout, and the message
                // says which dialog was missed.
                var firstTimeout = CatchDialogTimeout(() =>
                    MessageDlg.ShowWithException(SkylineWindow, MESSAGE_TEXT, new IOException(MESSAGE_TEXT)));
                Assert.IsNotNull(firstTimeout);
                AssertEx.Contains(firstTimeout.Message, MESSAGE_TEXT);

                // Handing that failure back to the UI, the way a generic catch handler does,
                // must rethrow it untouched instead of showing a dialog that times out in turn.
                var stopwatch = Stopwatch.StartNew();
                var rethrown = CatchDialogTimeout(() =>
                    MessageDlg.ShowWithException(SkylineWindow, firstTimeout.Message, firstTimeout));
                stopwatch.Stop();

                Assert.IsNotNull(rethrown);
                Assert.AreSame(firstTimeout, rethrown);     // Not a second timeout wrapping the first
                AssertEx.IsLessThan(stopwatch.Elapsed, TimeSpan.FromSeconds(RETHROW_LIMIT_SECONDS));

                // The watchdog must also REPORT the timeout, not only throw it. A throw out of a
                // dialog unwinds into a reentrant WndProc, where WinForms shows its own
                // ThreadExceptionDialog instead of routing to the harness, and the test then reports
                // a hang rather than the dialog it missed. Reporting makes this the first exception
                // the harness holds, which is the one it fails the test with.
                var reported = Program.TestExceptions.ToArray();
                Program.TestExceptions.Clear();  // Deliberately provoked, so do not fail this test
                AssertEx.IsTrue(reported.Length > 0, "The unattended dialog was never reported.");
                Assert.AreSame(firstTimeout, reported[0]);
                AssertEx.Contains(reported[0].Message, MESSAGE_TEXT);
            });
        }

        /// <summary>
        /// Runs an action expected to fail with the unattended-dialog watchdog timeout and
        /// returns that exception, or null if the action completed without one.
        /// </summary>
        private static TimeoutException CatchDialogTimeout(Action act)
        {
            try
            {
                act();
            }
            catch (TimeoutException e)
            {
                return e;
            }

            return null;
        }
    }
}
