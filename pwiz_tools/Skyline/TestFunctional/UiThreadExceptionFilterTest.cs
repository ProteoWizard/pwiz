/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that the UI-thread exception handler ignores the benign ObjectDisposedException
    /// WinForms raises when it signals a marshaled call whose wait handle is already disposed,
    /// and still reports one thrown by a callback of ours.
    ///
    /// <para>Since .NET 9 (dotnet/winforms#10460) Control.MarshaledInvoke disposes that handle
    /// with a using as soon as WaitForWaitHandle returns, and WaitForWaitHandle has paths that
    /// stop waiting while the entry is still queued. The queued callback then runs normally and
    /// only ThreadMethodEntry.Complete() throws, from Control.InvokeMarshaledCallbacks, with no
    /// thread left waiting on the result. Under a test harness that reaches
    /// Program.TestExceptions and fails whatever test happened to be running, which is how eight
    /// unrelated tests failed in one .NET 10 nightly. Reported as dotnet/winforms#14996.</para>
    ///
    /// <para>Both halves assert something POSITIVE. The benign half waits for the handler's own
    /// debug message rather than for an absence, so it cannot pass by the repro silently
    /// ceasing to strand anything - which it would do if WinForms stopped lazily allocating
    /// ThreadMethodEntry's ManualResetEvent, the one undocumented internal this setup rests on.
    /// The reporting half provokes from a framework method group on purpose: that produces an
    /// all-framework stack, the case a whole-stack allowlist would wrongly swallow.</para>
    /// </summary>
    [TestClass]
    public class UiThreadExceptionFilterTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestUiThreadExceptionFilter()
        {
            RunFunctionalTest();
        }

        // Safety valve for the UI thread parked inside the marshaled callback. Deliberately not
        // WAIT_TIME: nothing should ever wait on it, and a stuck UI thread blocks teardown.
        private const int PARKED_CALLBACK_RELEASE_MILLIS = 10 * 1000;

        private readonly List<string> _debugMessages = new List<string>();

        protected override void DoTest()
        {
            var restoreWriteDebugMessage = Messages.WriteDebugMessage;
            Messages.WriteDebugMessage = (message, args) =>
            {
                lock (_debugMessages)
                {
                    _debugMessages.Add(args == null ? message : string.Format(message, args));
                }
                restoreWriteDebugMessage(message, args);
            };
            try
            {
                // SkylineWindow does not override WndProc; SequenceTree does. A control that
                // overrides it puts a Skyline frame BELOW Control.InvokeMarshaledCallbacks, so
                // only the second case catches a filter that judges the whole stack.
                AssertBenignCompletionFailureIgnored(SkylineWindow);
                AssertBenignCompletionFailureIgnored(SkylineWindow.SequenceTree);
                AssertCallbackFailureStillReported();
            }
            finally
            {
                Messages.WriteDebugMessage = restoreWriteDebugMessage;
            }
        }

        /// <summary>
        /// Strands a marshaled call the way MarshaledInvoke does on the paths that stop waiting
        /// before the entry completes, then verifies the completion failure was ignored.
        /// </summary>
        private void AssertBenignCompletionFailureIgnored(Control target)
        {
            lock (_debugMessages)
            {
                _debugMessages.Clear();
            }
            var callbackEntered = new ManualResetEventSlim(false);
            var releaseCallback = new ManualResetEventSlim(false);
            var strandedCallbackRan = false;

            // Park the UI thread inside Control.InvokeMarshaledCallbacks so the next call can be
            // queued behind it and have its wait handle disposed before it is dispatched.
            target.BeginInvoke(new Action(() =>
            {
                callbackEntered.Set();
                releaseCallback.Wait(PARKED_CALLBACK_RELEASE_MILLIS);
            }));
            try
            {
                AssertEx.IsTrue(callbackEntered.Wait(WAIT_TIME),
                    string.Format(@"The UI thread never entered the blocking marshaled callback on {0}.",
                        target.GetType().Name));

                var stranded = target.BeginInvoke(new Action(() => strandedCallbackRan = true));
                // What MarshaledInvoke does on the paths that stop waiting before the entry completes.
                stranded.AsyncWaitHandle.Dispose();
            }
            finally
            {
                releaseCallback.Set();
            }

            // Wait for the handler to say it ignored it, not merely for nothing to be recorded.
            // The exception names the disposed type, so this needs no copy of the handler's text.
            WaitForCondition(WAIT_TIME, () => HasDebugMessageNaming(typeof(SafeWaitHandle).FullName),
                null, true, false);

            AssertEx.IsTrue(strandedCallbackRan,
                string.Format(@"The marshaled callback on {0} did not run after its wait handle was disposed.",
                    target.GetType().Name));
            AssertEx.AreEqual(0, Program.TestExceptions.Count,
                string.Format(@"Completing a marshaled call on {0} reached the test harness.",
                    target.GetType().Name));
        }

        /// <summary>
        /// The other half of the filter: an ObjectDisposedException thrown by a callback of ours
        /// must still be reported. Provoked from a framework method group so every frame above
        /// Control.InvokeMarshaledCallbacks belongs to the framework - the case that a filter
        /// judging the whole stack, rather than the window below the throw, would swallow.
        /// </summary>
        private void AssertCallbackFailureStillReported()
        {
            AssertEx.AreEqual(0, Program.TestExceptions.Count,
                @"Something recorded an exception before the reporting half provoked one.");

            var disposedMutex = new Mutex();
            disposedMutex.Dispose();
            SkylineWindow.BeginInvoke(new Action(disposedMutex.ReleaseMutex));

            Exception reported = null;
            try
            {
                WaitForCondition(WAIT_TIME, () => Program.TestExceptions.Count > 0,
                    @"The filter swallowed an ObjectDisposedException thrown by a marshaled callback.",
                    true, false);
                reported = Program.TestExceptions[0];
                AssertEx.IsTrue(reported is ObjectDisposedException,
                    @"Expected the reported exception to be the one the callback threw.");
                // Identity, not just type: both candidates are ObjectDisposedException naming a
                // SafeWaitHandle, so only the throwing method separates them.
                AssertEx.Contains(reported.StackTrace, nameof(Mutex.ReleaseMutex));
            }
            finally
            {
                // Remove only what this test provoked. Clearing would discard an unrelated
                // failure recorded by a background thread while this test ran, and the harness
                // check at teardown would then pass with the real bug gone.
                if (reported != null)
                {
                    lock (Program.TestExceptions)
                    {
                        Program.TestExceptions.Remove(reported);
                    }
                }
            }
        }

        private bool HasDebugMessageNaming(string objectName)
        {
            lock (_debugMessages)
            {
                return _debugMessages.Any(message => message.Contains(objectName));
            }
        }
    }
}
