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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies the connector can get past a native Windows message box (the kind the common Save dialog
    /// raises to confirm replacing an existing file). It is enumerated as a native MessageBox form, its
    /// prompt and Yes/No buttons are listed by <see cref="NativeMessageBox.GetControls"/>, and clicking a
    /// button by its caption dismisses the box with that result.
    /// </summary>
    [TestClass]
    public class NativeMessageBoxTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestNativeMessageBox()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Show a native message box without blocking the test thread (it runs its own modal loop on
            // the UI thread until dismissed).
            var result = DialogResult.None;
            SkylineWindow.BeginInvoke((Action)(() =>
                result = MessageBox.Show(SkylineWindow, // Purposely using MessageBox here
                    "Yeast.protdb already exists.\nDo you want to replace it?",
                    "Confirm Save As", MessageBoxButtons.YesNo)));

            var messageBox = NativeDialog.WaitForDialog<NativeMessageBox>();

            // The connector enumerates it as a native MessageBox form.
            var nativeForm = JsonUiService.GetOpenForms().Single(form => form.IsNative);
            Assert.AreEqual(@"MessageBox", nativeForm.Type);

            // get_controls lists the prompt text and the Yes / No buttons.
            var labels = messageBox.GetControls().Select(control => control.Path.Text).ToArray();
            CollectionAssert.Contains(labels, @"Yes");
            CollectionAssert.Contains(labels, @"No");

            // Clicking "Yes" by its caption dismisses the box with that result.
            messageBox.ClickButton(@"Yes");
            WaitForCondition(() => !NativeDialog.GetOpenDialogs().Any());
            WaitForConditionUI(() => result == DialogResult.Yes);
            RunUI(() => Assert.AreEqual(DialogResult.Yes, result,
                @"Clicking 'Yes' on the message box should return DialogResult.Yes."));
        }
    }
}
