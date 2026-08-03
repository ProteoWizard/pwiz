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
using pwiz.Skyline;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Covers the two things a connector form must get right that every other connector test misses, because every
    /// other test drives the main window while nothing is blocking it:
    ///
    /// <para>1. A form that runs its OWN message loop on its OWN thread (as a BackgroundThreadLongWaitDlg does) must
    /// be read through THAT form's Invoke, not the main window's. Reading its controls from the main window's thread
    /// is a cross-thread touch and throws.</para>
    ///
    /// <para>2. A form BLOCKED by a modal must refuse to be driven, with a message naming the dialog in the way. A
    /// modal disables the windows it blocks at the Win32 level (EnableWindow) WITHOUT flipping their managed
    /// Control.Enabled, so a form that gates only on Form.Enabled cannot tell it is blocked and drives a dead
    /// window instead of failing.</para>
    /// </summary>
    [TestClass]
    public class McpConnectorFormThreadingTest : McpConnectorTest
    {
        private const string OWN_THREAD_TITLE = "McpConnector Own Thread Form";
        private const string TEXT_BOX_LABEL = "Own thread field";

        [TestMethod]
        public void TestMcpConnectorFormThreading()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            StartToolService();
            ReadFormOnItsOwnThread();
            RefuseToDriveABlockedForm();
        }

        /// <summary>Shows a form on its own thread, with its own message loop, and reads its controls through the
        /// connector. The read must be marshaled to the form's thread; done through the main window's thread instead,
        /// touching the form's controls throws a cross-thread InvalidOperationException.</summary>
        private void ReadFormOnItsOwnThread()
        {
            var shown = new ManualResetEventSlim(false);
            Form ownThreadForm = null;
            ThreadRecordingTextBox textBox = null;
            int formThreadId = 0;
            var thread = new Thread(() =>
            {
                formThreadId = Thread.CurrentThread.ManagedThreadId;
                textBox = new ThreadRecordingTextBox { Name = @"textBoxOwnThread", TabIndex = 1, Text = @"initial" };
                ownThreadForm = new Form { Text = OWN_THREAD_TITLE, Width = 300, Height = 200 };
                ownThreadForm.Controls.Add(new Label { Text = TEXT_BOX_LABEL, TabIndex = 0 });
                ownThreadForm.Controls.Add(textBox);
                ownThreadForm.Shown += (sender, args) => shown.Set();
                Application.Run(ownThreadForm);  // its own message loop, on this thread
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Assert.IsTrue(shown.Wait(TimeSpan.FromSeconds(20)), @"The own-thread form never appeared.");

            // WinForms only POLICES cross-thread control access when a debugger is attached (the flag defaults to
            // Debugger.IsAttached). Without this, touching this form's controls from the main window's thread quietly
            // succeeds under the test runner and the bug this test exists for is invisible -- the test would pass
            // whether or not the read is marshaled to the right thread. Turn the check on so a wrong-thread read throws.
            bool checkCrossThread = Control.CheckForIllegalCrossThreadCalls;
            Control.CheckForIllegalCrossThreadCalls = true;
            try
            {
                // The connector sees it as a top-level window like any other. It is the only plain Form open (the
                // Skyline windows are all their own types), so its type alone identifies it. Enumerating it at all
                // exercises GetOpenForms, which reads each form through ITS OWN thread -- reading this one's Handle
                // from the main window's thread throws (the cross-thread check is on, above).
                string formId = WaitForMcpConnectorForm(nameof(Form));
                Assert.AreEqual(nameof(Form) + @":" + OWN_THREAD_TITLE, formId);

                var controls = McpConnector.GetControls(formId);
                Assert.IsTrue(controls.Any(c => Equals(c.Path.Type, nameof(ThreadRecordingTextBox))),
                    @"The own-thread form's TextBox was not read back.");

                // THE point of this form: the read must land on the form's OWN thread. Asserted directly, because it
                // cannot be caught by the cross-thread check -- WinForms reads Control.Text inside a
                // MultithreadSafeCallScope, which SUPPRESSES that check, so a wrong-thread read of it quietly
                // succeeds. (The walk also touches Controls/Items/cells, which have no such guarantee.)
                textBox.ReadOnThreadId = 0;
                Assert.AreEqual(@"initial", McpConnector.GetFormValue(formId, TEXT_BOX_LABEL));
                Assert.AreEqual(formThreadId, textBox.ReadOnThreadId,
                    @"The own-thread form's TextBox was read on the wrong thread (the main window's, not its own).");

                // And a write, which routes by a different path (the gesture thread) than the read.
                AssertComplete(McpConnector.SetFormValue(formId, TEXT_BOX_LABEL, @"typed from the connector"));
                Assert.AreEqual(@"typed from the connector", McpConnector.GetFormValue(formId, TEXT_BOX_LABEL));
            }
            finally
            {
                Control.CheckForIllegalCrossThreadCalls = checkCrossThread;
                // Close it on its own thread, then let its message loop end.
                var form = ownThreadForm;
                if (form != null && form.IsHandleCreated)
                    form.BeginInvoke((Action) (() => form.Close()));
                Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)), @"The own-thread form did not shut down.");
            }
        }

        /// <summary>A TextBox that records which thread its Text was last read on -- the only way to prove the read
        /// was marshaled to the form's own thread, since WinForms deliberately allows Text to be read from any
        /// thread (MultithreadSafeCallScope) and so will not complain when it is not.</summary>
        private class ThreadRecordingTextBox : TextBox
        {
            public int ReadOnThreadId { get; set; }

            public override string Text
            {
                get
                {
                    ReadOnThreadId = Thread.CurrentThread.ManagedThreadId;
                    return base.Text;
                }
                set { base.Text = value; }
            }
        }

        /// <summary>Opens a modal over the main window, then asks the connector to accept the MAIN window. It must
        /// refuse, naming the dialog that is blocking it -- not click a dead accept button and wait.</summary>
        private void RefuseToDriveABlockedForm()
        {
            string mainFormId = GetOpenFormId<SkylineWindow>();
            // Peptide Settings is modal, so it blocks the main window while it is up.
            string settingsId = ResolveModal(McpConnector.ClickMainMenuItem(
                MenuPath<SkylineWindow>(@"settingsToolStripMenuItem", @"peptideSettingsMenuItem")));
            try
            {
                string message = null;
                try
                {
                    McpConnector.DismissWithAcceptButton(mainFormId);
                }
                catch (Exception exception)
                {
                    message = exception.Message;
                }
                // The point: it fails BECAUSE it is blocked. Gating only on Form.Enabled (which a modal leaves true)
                // would sail past this and fail later for an unrelated reason, or hang.
                Assert.IsNotNull(message, @"Accepting a form blocked by a modal should have been refused.");
                AssertEx.Contains(message, @"blocked by an open dialog");
            }
            finally
            {
                AssertComplete(McpConnector.DismissWithCancelButton(settingsId));
            }
        }
    }
}
