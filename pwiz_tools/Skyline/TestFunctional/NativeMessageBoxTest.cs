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
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Drives the native "replace it?" message box that the common Save dialog raises when Save As targets an
    /// existing file, all through the in-process <see cref="SkylineTool.IJsonToolService"/> (as an external MCP
    /// client would). The point of the test is that the interface handles a file dialog whose Accept opens a
    /// message box: <see cref="IJsonToolService.DismissWithAcceptButton"/> on the file dialog surfaces that box (it
    /// reports not-completed and names the box) rather than hanging, and both of the box's buttons can then be
    /// driven through the interface -- declining ("No") leaves the file dialog open, and accepting (its default
    /// button) replaces the file.
    /// </summary>
    [TestClass]
    public class NativeMessageBoxTest : McpConnectorTest
    {
        [TestMethod]
        public void TestNativeMessageBox()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            StartToolService();

            // No TestFilesZip is set for this test, so there is no TestFilesDir; use the (writable) test results
            // folder for the document, the same way PrmMcpConnectorTest does.
            var savePath = TestContext.GetTestResultsPath(@"MyDocument.sky");
            // Start from a clean slate so the FIRST save has no file to replace (a leftover from an earlier run
            // would otherwise raise the replace-confirm box on the first save, before the test expects it).
            if (File.Exists(savePath))
                File.Delete(savePath);
            var saveAsMenu = MenuPath<SkylineWindow>(@"fileToolStripMenuItem", @"saveAsMenuItem");

            // 1) First Save As: save to a name that does not exist yet -- no confirmation.
            McpConnector.ClickMainMenuItem(saveAsMenu);
            var fileDialogId = WaitForNativeFileDialog();
            AssertComplete(McpConnector.SetFormValue(fileDialogId, @"FileName", savePath));
            AssertComplete(McpConnector.DismissWithAcceptButton(fileDialogId));
            WaitForCondition(() => !McpConnector.GetOpenForms().Any(form => form.IsNative));
            // Case-insensitive: the native Save dialog stores the path with the drive letter upper-cased
            // ("C:\..."), while savePath can be lower-cased ("c:\...") under the parallel Docker test runner
            // (a case-sensitive compare here waited out the full WaitForConditionUI timeout).
            WaitForConditionUI(() =>
                string.Equals(SkylineWindow.DocumentFilePath, savePath, StringComparison.OrdinalIgnoreCase));

            // 2) Save As over the existing file: accepting the file dialog raises the native "replace?" message
            // box. The interface must SURFACE it (report not-completed and name it) rather than hang.
            int modalNestingCount = McpConnector.ModalNestingCount();
            var actionResult = McpConnector.ClickMainMenuItem(saveAsMenu);
            Assert.IsFalse(actionResult.Completed);
            fileDialogId = actionResult.FormId;
            Assert.IsNotNull(fileDialogId);
            Assert.AreEqual(modalNestingCount, GetModalNestingCount(fileDialogId));
            AssertComplete(McpConnector.SetFormValue(fileDialogId, @"FileName", savePath));
            actionResult = McpConnector.DismissWithAcceptButton(fileDialogId);
            Assert.IsFalse(actionResult.Completed);
            Assert.IsNotNull(actionResult.FormId);
            Assert.IsNotNull(actionResult.Message);
            var buttons = McpConnector.GetControls(actionResult.FormId).Where(control => control.Path.Type == nameof(System.Windows.Forms.Button)).ToList();
            Assert.AreEqual(2, buttons.Count);
            // Press "No" (decline to overwrite the file): the MessageBox closes and the file dialog stays open
            AssertComplete(McpConnector.DismissWithButton(actionResult.FormId, buttons[1].Path.Text));
            actionResult = McpConnector.DismissWithAcceptButton(fileDialogId);
            Assert.IsFalse(actionResult.Completed);
            Assert.IsNotNull(actionResult.FormId);
            Assert.IsNotNull(actionResult.Message);
            buttons = McpConnector.GetControls(actionResult.FormId).Where(control => control.Path.Type == nameof(System.Windows.Forms.Button)).ToList();
            Assert.AreEqual(2, buttons.Count);
            AssertComplete(McpConnector.DismissWithButton(actionResult.FormId, buttons[0].Path.Text));
            WaitForConditionUI(() => modalNestingCount == McpConnector.ModalNestingCount());

            var nativeForms = McpConnector.GetOpenForms().Where(form => form.IsNative).ToList();
            Assert.AreEqual(0, nativeForms.Count);
        }

        private int? GetModalNestingCount(string formId)
        {
            var formElement = JsonUiService.ResolveForm(formId, CancellationToken.None);
            Assert.IsNotNull(formElement);
            return DialogWatcher.TryGetPreShowActionCount(formElement);
        }
    }
}
