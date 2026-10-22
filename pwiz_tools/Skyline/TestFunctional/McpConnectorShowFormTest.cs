/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies <see cref="McpConnectorTest.ShowMcpConnectorForm{TForm}"/> against the race behind intermittent
    /// McpConnector test failures ("Expected a form of type DefineAnnotationDlg to be open, but it was not"), using
    /// a dialog whose OnLoad can be held open so the race happens every time instead of a few times in a thousand.
    /// </summary>
    [TestClass]
    public class McpConnectorShowFormTest : McpConnectorTest
    {
        // Only a safety limit: the test releases the held OnLoad as soon as it has checked the window.
        private const int HELD_LOAD_TIMEOUT_MILLIS = 30 * 1000;
        // Long enough that ShowDialog nearly always returns first, so the helper has the race to wait out.
        private const int SLOW_LOAD_MILLIS = 300;

        [TestMethod]
        public void TestMcpConnectorShowForm()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            StartToolService();

            VerifyShowDialogReturnsBeforeVisible();
            VerifyShowMcpConnectorFormWaitsForConnector();
        }

        // The precondition the helper exists for: ShowDialog hands back a dialog Windows has not shown yet. Without
        // this, the check below could pass only because the race had stopped happening.
        private void VerifyShowDialogReturnsBeforeVisible()
        {
            using var loadGate = new ManualResetEventSlim();
            var slowForm = ShowDialog<SlowLoadingForm>(() => ShowSlowLoadingForm(loadGate, HELD_LOAD_TIMEOUT_MILLIS));
            try
            {
                AssertEx.IsFalse(slowForm.LoadHandle == IntPtr.Zero);
                AssertEx.IsFalse(User32.IsWindowVisible(slowForm.LoadHandle),
                    @"Expected ShowDialog to return while the dialog was still loading and not yet visible.");
            }
            finally
            {
                loadGate.Set();
            }
            OkDialog(slowForm, slowForm.Close);
        }

        // Looking the dialog up with GetOpenFormId right after ShowDialog failed here every time.
        private void VerifyShowMcpConnectorFormWaitsForConnector()
        {
            using var neverSet = new ManualResetEventSlim();
            var slowForm = ShowMcpConnectorForm<SlowLoadingForm>(() => ShowSlowLoadingForm(neverSet, SLOW_LOAD_MILLIS),
                out var formId);
            Assert.IsNotNull(McpConnector.GetControls(formId));
            OkDialog(slowForm, slowForm.Close);
        }

        private void ShowSlowLoadingForm(ManualResetEventSlim loadGate, int loadTimeoutMillis)
        {
            using var form = new SlowLoadingForm(loadGate, loadTimeoutMillis);
            form.ShowDialog(SkylineWindow);
        }

        /// <summary>A dialog whose OnLoad waits on a gate (or a timeout), after it is Created and before Windows
        /// shows it.</summary>
        private class SlowLoadingForm : FormEx
        {
            private readonly ManualResetEventSlim _loadGate;
            private readonly int _loadTimeoutMillis;

            public SlowLoadingForm(ManualResetEventSlim loadGate, int loadTimeoutMillis)
            {
                _loadGate = loadGate;
                _loadTimeoutMillis = loadTimeoutMillis;
            }

            public IntPtr LoadHandle { get; private set; }

            // Captured as soon as the window exists, before Created can become true.
            protected override void OnHandleCreated(EventArgs e)
            {
                LoadHandle = Handle;
                base.OnHandleCreated(e);
            }

            protected override void OnLoad(EventArgs e)
            {
                base.OnLoad(e);
                _loadGate.Wait(_loadTimeoutMillis);
            }
        }
    }
}
