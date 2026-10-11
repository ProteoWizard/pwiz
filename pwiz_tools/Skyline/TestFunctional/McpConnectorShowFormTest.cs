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
    /// McpConnector test failures ("Expected a form of type DefineAnnotationDlg to be open, but it was not").
    /// ShowDialog returns as soon as a form is Created, which is before Windows shows it, while the connector lists
    /// only visible windows. A form that takes a while in OnLoad holds that gap open, so the race happens every
    /// time instead of a few times in a thousand.
    /// </summary>
    [TestClass]
    public class McpConnectorShowFormTest : McpConnectorTest
    {
        // Long enough to outlast the 100 ms poll by which ShowDialog notices the form, with margin for a busy machine.
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

        // The precondition the helper exists for: ShowDialog hands back a form Windows has not shown yet. Without
        // this, the check below could pass only because the race had stopped happening.
        private void VerifyShowDialogReturnsBeforeVisible()
        {
            var slowForm = ShowDialog<SlowLoadingForm>(ShowSlowLoadingForm);
            Assert.IsTrue(slowForm.Created);
            Assert.IsFalse(User32.IsWindowVisible(slowForm.LoadHandle),
                @"Expected ShowDialog to return while the form was still loading and not yet visible.");
            OkDialog(slowForm, slowForm.Close);
        }

        // Looking the form up with GetOpenFormId right after ShowDialog failed here every time.
        private void VerifyShowMcpConnectorFormWaitsForConnector()
        {
            var slowForm = ShowMcpConnectorForm<SlowLoadingForm>(ShowSlowLoadingForm, out var formId);
            Assert.IsTrue(User32.IsWindowVisible(slowForm.LoadHandle));
            Assert.IsNotNull(McpConnector.GetControls(formId));
            OkDialog(slowForm, slowForm.Close);
        }

        private void ShowSlowLoadingForm()
        {
            using var form = new SlowLoadingForm();
            form.ShowDialog(SkylineWindow);
        }

        /// <summary>A dialog that spends <see cref="SLOW_LOAD_MILLIS"/> in OnLoad, after it is Created and before
        /// Windows shows it.</summary>
        private class SlowLoadingForm : FormEx
        {
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
                Thread.Sleep(SLOW_LOAD_MILLIS);
            }
        }
    }
}
