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
    /// The native "replace it?" message box that the common Save dialog raises when Save As targets an existing
    /// file, driven through the in-process <see cref="SkylineTool.IJsonToolService"/> (as an external MCP client
    /// would). The point is that the interface handles a file dialog whose Accept opens a message box:
    /// <see cref="IJsonToolService.DismissWithAcceptButton"/> on the file dialog surfaces that box (it reports
    /// not-completed and names the box) rather than hanging, and both of the box's buttons can then be driven --
    /// declining ("No") leaves the file dialog open, and accepting (its default button) replaces the file.
    ///
    /// <para>Each of those is one nested test with its own DoTest, so each opens a SINGLE native file dialog per
    /// run. This keeps the per-run growth of Windows' native shell caches (which grow with the first native file
    /// dialogs a process shows) small enough that the nightly leak check clears it, and if one of them does
    /// report a leak it names exactly which aspect to look at. The shared setup lives on this abstract base.</para>
    /// </summary>
    public abstract class NativeMessageBoxTest : McpConnectorTest
    {
        /// <summary>A clean document-save path in the (writable) test results folder, with any leftover from an
        /// earlier run removed. No TestFilesZip is set for these tests, so there is no TestFilesDir; use the test
        /// results folder the same way PrmMcpConnectorTest does.</summary>
        protected string GetCleanSaveTargetPath()
        {
            var savePath = TestContext.GetTestResultsPath(@"MyDocument.sky");
            if (File.Exists(savePath))
                File.Delete(savePath);
            return savePath;
        }

        /// <summary>The File &gt; Save As menu path, localized so a click matches in any UI language.</summary>
        protected string SaveAsMenuPath => MenuPath<SkylineWindow>(@"fileToolStripMenuItem", @"saveAsMenuItem");

        /// <summary>The pre-show action count recorded for the native file dialog with the given id -- the modal
        /// bookkeeping the surfaced dialog is counted under (see <see cref="DialogWatcher"/>).</summary>
        protected int? GetModalNestingCount(string formId)
        {
            var formElement = JsonUiService.ResolveForm(formId, CancellationToken.None);
            Assert.IsNotNull(formElement);
            return DialogWatcher.TryGetPreShowActionCount(formElement);
        }

        /// <summary>
        /// Save As to a name that does not exist yet: accepting the file dialog completes synchronously -- no
        /// confirmation box -- and the document takes the new path.
        /// </summary>
        [TestClass]
        public class SaveAsWithoutConfirmation : NativeMessageBoxTest
        {
            [TestMethod]
            public void TestNativeMessageBoxSaveAsWithoutConfirmation()
            {
                RunFunctionalTest();
            }

            protected override void DoTest()
            {
                StartToolService();
                var savePath = GetCleanSaveTargetPath();

                McpConnector.ClickMainMenuItem(SaveAsMenuPath);
                var fileDialogId = WaitForNativeFileDialog();
                AssertComplete(McpConnector.SetFormValue(fileDialogId, @"FileName", savePath));
                // The name does not exist, so Accept commits with no "replace?" box and completes.
                AssertComplete(McpConnector.DismissWithAcceptButton(fileDialogId));
                WaitForCondition(() => !McpConnector.GetOpenForms().Any(form => form.IsNative));
                // Case-insensitive: the native Save dialog stores the path with the drive letter upper-cased
                // ("C:\..."), while savePath can be lower-cased ("c:\...") under the parallel Docker test runner.
                WaitForConditionUI(() =>
                    string.Equals(SkylineWindow.DocumentFilePath, savePath, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Save As over an existing file, then DECLINE the replace: accepting the file dialog surfaces the native
        /// "replace?" box (reported not-completed and named) rather than hanging, and pressing its "No" button
        /// closes the box and leaves the file dialog open -- nothing was overwritten.
        /// </summary>
        [TestClass]
        public class ReplaceConfirmationDeclined : NativeMessageBoxTest
        {
            [TestMethod]
            public void TestNativeMessageBoxReplaceConfirmationDeclined()
            {
                RunFunctionalTest();
            }

            protected override void DoTest()
            {
                StartToolService();
                var savePath = GetCleanSaveTargetPath();
                // A file already at the target path is all it takes for the common Save dialog to raise the
                // "replace?" box on Accept; the document itself has never been saved.
                File.WriteAllText(savePath, @"not a real Skyline document");

                int modalNestingCount = McpConnector.ModalNestingCount();
                var fileDialogId = ResolveModal(McpConnector.ClickMainMenuItem(SaveAsMenuPath));
                Assert.AreEqual(modalNestingCount, GetModalNestingCount(fileDialogId));
                AssertComplete(McpConnector.SetFormValue(fileDialogId, @"FileName", savePath));

                // Accept the file dialog: it must SURFACE the replace box (report not-completed and name it).
                var actionResult = McpConnector.DismissWithAcceptButton(fileDialogId);
                var messageBoxId = ResolveModal(actionResult);
                Assert.IsNotNull(actionResult.Message);
                var buttons = McpConnector.GetControls(messageBoxId)
                    .Where(control => control.Path.Type == nameof(System.Windows.Forms.Button)).ToList();
                Assert.AreEqual(2, buttons.Count);

                // Press "No": the box closes and the file dialog is still open (nothing overwritten).
                AssertComplete(McpConnector.DismissWithButton(messageBoxId, buttons[1].Path.Text));
                Assert.IsNotNull(GetNativeFileDialog());
                Assert.AreEqual(@"not a real Skyline document", File.ReadAllText(savePath));

                // Cancel the still-open file dialog to finish; no native form is left open.
                AssertComplete(McpConnector.DismissWithCancelButton(fileDialogId));
                WaitForConditionUI(() => modalNestingCount == McpConnector.ModalNestingCount());
                Assert.AreEqual(0, McpConnector.GetOpenForms().Count(form => form.IsNative));
            }
        }

        /// <summary>
        /// Save As over an existing file, then ACCEPT the replace: accepting the file dialog surfaces the native
        /// "replace?" box, and pressing its default button replaces the file -- the document is saved to the path.
        /// </summary>
        [TestClass]
        public class ReplaceConfirmationAccepted : NativeMessageBoxTest
        {
            [TestMethod]
            public void TestNativeMessageBoxReplaceConfirmationAccepted()
            {
                RunFunctionalTest();
            }

            protected override void DoTest()
            {
                StartToolService();
                var savePath = GetCleanSaveTargetPath();
                File.WriteAllText(savePath, @"not a real Skyline document");
                // The document has not been saved anywhere yet, so accepting the replace is what puts it here.
                Assert.AreNotEqual(savePath, SkylineWindow.DocumentFilePath);

                int modalNestingCount = McpConnector.ModalNestingCount();
                var fileDialogId = ResolveModal(McpConnector.ClickMainMenuItem(SaveAsMenuPath));
                AssertComplete(McpConnector.SetFormValue(fileDialogId, @"FileName", savePath));

                var actionResult = McpConnector.DismissWithAcceptButton(fileDialogId);
                var messageBoxId = ResolveModal(actionResult);
                Assert.IsNotNull(actionResult.Message);
                var buttons = McpConnector.GetControls(messageBoxId)
                    .Where(control => control.Path.Type == nameof(System.Windows.Forms.Button)).ToList();
                Assert.AreEqual(2, buttons.Count);

                // Press the default button (its first, "Yes"): the file is replaced and the dialogs close.
                AssertComplete(McpConnector.DismissWithButton(messageBoxId, buttons[0].Path.Text));
                WaitForConditionUI(() => modalNestingCount == McpConnector.ModalNestingCount());
                Assert.AreEqual(0, McpConnector.GetOpenForms().Count(form => form.IsNative));
                // The replace saved the real document over the placeholder, so the document now has the path.
                WaitForConditionUI(() =>
                    string.Equals(SkylineWindow.DocumentFilePath, savePath, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
