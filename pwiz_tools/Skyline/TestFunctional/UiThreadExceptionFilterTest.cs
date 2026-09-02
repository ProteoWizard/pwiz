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
    /// Verifies what the UI-thread exception handler ignores when WinForms signals a marshaled
    /// call whose wait handle is already disposed - and, just as importantly, what it does not.
    ///
    /// <para>Since .NET 9 (dotnet/winforms#10460) Control.MarshaledInvoke disposes that handle
    /// with a using as soon as WaitForWaitHandle returns, and WaitForWaitHandle has paths that
    /// stop waiting while the entry is still queued. The queued callback then runs normally and
    /// only ThreadMethodEntry.Complete() throws, from Control.InvokeMarshaledCallbacks, with no
    /// thread left waiting on the result. Under a test harness that reaches
    /// Program.TestExceptions and fails whatever test happened to be running, which is how eight
    /// unrelated tests failed in one .NET 10 nightly. Reported as dotnet/winforms#14996.</para>
    ///
    /// <para>The filter is deliberately narrow, so the second case here asserts that an
    /// exception IS still reported. Failing to silence a benign one costs a visible test
    /// failure; silencing a real one hides a defect. Change these expectations only with that
    /// asymmetry in mind.</para>
    ///
    /// <para>Not covered: a completion failure marshaled through a control that overrides
    /// WndProc, which puts a Skyline frame in the pump and is deliberately not filtered.
    /// Reproduced in isolation against .NET 10.0.11, but not from inside Skyline - stranding a
    /// call on SkylineWindow.SequenceTree still produced an all-framework stack, so the
    /// marshaling control in practice was not the tree. Worth revisiting if a nightly ever
    /// fails that way.</para>
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
                AssertCompletionFailureIgnored();
                AssertCallbackFailureStillReported();
            }
            finally
            {
                Messages.WriteDebugMessage = restoreWriteDebugMessage;
            }
        }

        /// <summary>
        /// The case the filter exists for: SkylineWindow does not override WndProc, so every
        /// frame belongs to WinForms or the core library and the completion failure is ignored.
        /// </summary>
        private void AssertCompletionFailureIgnored()
        {
            var strandedCallbackRan = StrandMarshaledCall(SkylineWindow);

            // Wait for the handler to say it ignored it, not merely for nothing to be recorded.
            // The exception names the disposed type, so this needs no copy of the handler's text.
            WaitForCondition(WAIT_TIME, () => HasDebugMessageNaming(typeof(SafeWaitHandle).FullName),
                null, true, false);

            AssertEx.IsTrue(strandedCallbackRan(),
                @"The marshaled callback did not run after its wait handle was disposed.");
            AssertEx.AreEqual(0, Program.TestExceptions.Count,
                @"Completing a marshaled call on SkylineWindow reached the test harness.");
        }

        /// <summary>
        /// An ObjectDisposedException thrown by a callback of ours must still be reported.
        /// Provoked from a framework method group so every frame above
        /// Control.InvokeMarshaledCallbacks belongs to the framework - the case a filter that
        /// only asked whether any frame above it was ours would wrongly swallow.
        /// </summary>
        private void AssertCallbackFailureStillReported()
        {
            var disposedMutex = new Mutex();
            disposedMutex.Dispose();
            SkylineWindow.BeginInvoke(new Action(disposedMutex.ReleaseMutex));

            var reported = AssertReportedObjectDisposedException(
                @"The filter swallowed an ObjectDisposedException thrown by a marshaled callback.");
            // Identity, not just type: both candidates are ObjectDisposedException naming a
            // SafeWaitHandle, so only the throwing method separates them.
            AssertEx.Contains(reported.StackTrace, nameof(Mutex.ReleaseMutex));
        }

        /// <summary>
        /// Queues a marshaled call behind a parked one and disposes its wait handle before it can
        /// be dispatched - what MarshaledInvoke leaves behind on the paths that stop waiting
        /// before the entry completes. Returns a func reporting whether the callback ran.
        /// </summary>
        private Func<bool> StrandMarshaledCall(Control target)
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
                stranded.AsyncWaitHandle.Dispose();
            }
            finally
            {
                releaseCallback.Set();
            }
            return () => strandedCallbackRan;
        }

        /// <summary>
        /// Waits for one ObjectDisposedException to reach the harness, then removes it. Removes
        /// only what was observed: clearing would discard an unrelated failure recorded by a
        /// background thread, and the harness check at teardown would then pass with the real
        /// bug gone.
        /// </summary>
        private Exception AssertReportedObjectDisposedException(string timeoutMessage)
        {
            Exception reported = null;
            try
            {
                WaitForCondition(WAIT_TIME, () => Program.TestExceptions.Count > 0,
                    timeoutMessage, true, false);
                reported = Program.TestExceptions[0];
                AssertEx.IsTrue(reported is ObjectDisposedException,
                    @"Expected the reported exception to be an ObjectDisposedException.");
                return reported;
            }
            finally
            {
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
