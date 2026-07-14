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
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies the native common file dialog (the OpenFileDialog) end to end: it is visible to
    /// the MCP UI-introspection layer (enumerated by <see cref="JsonToolServer.GetOpenForms"/> as a
    /// native form, and captured by <see cref="JsonUiService.GetFormImage"/> by its reported id
    /// even though it is not a WinForms form), it can be dismissed, and a file can be opened
    /// through it with <see cref="NativeOpenFileDialog.EnterPathAndAccept"/>.
    /// </summary>
    [TestClass]
    public class NativeFileDialogTest : McpConnectorTest
    {
        [TestMethod]
        public void TestNativeFileDialog()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Every verb below is driven through the running JSON tool server (torn down with the window).
            StartToolService();

            RunUI(() => Settings.Default.AllowMcpScreenCapture = true);

            var documentBefore = SkylineWindow.Document;
            // Show the native Open dialog without blocking the test thread, then wait for it to
            // appear and obtain its automation wrapper.
            SkylineWindow.BeginInvoke((Action)(() => SkylineWindow.ShowOpenFileDialog()));
            var fileDialog = NativeDialog.WaitForDialog<NativeOpenFileDialog>();

            // GetOpenForms includes the native dialog, flagged as native.
            var nativeForms = McpConnector.GetOpenForms().Where(form => form.IsNative).ToArray();
            Assert.AreEqual(1, nativeForms.Length,
                @"Expected exactly one native form while the Open dialog is showing.");
            var nativeForm = nativeForms[0];

            // GetFormImage resolves the native id (rather than throwing "form not found" as it
            // would for a missing WinForms form) and attempts a screen capture. When a desktop is
            // available it returns a PNG file; in a headless/offscreen environment it returns an
            // availability message instead. Either is acceptable; a thrown ArgumentException would
            // mean the native id was not routed to the UI Automation path.
            var image = JsonUiService.GetFormImageBytes(nativeForm.Id);
            if (image.Data != null)
                AssertPngSignature(image.Data);
            else
                Assert.IsNotNull(image.Message);

            // Dismiss the dialog and confirm it leaves the document unchanged.
            fileDialog.DismissWithCancelButton();
            WaitForCondition(() => !NativeDialog.GetOpenDialogs(CancellationToken.None).Any());
            Assert.AreSame(documentBefore, SkylineWindow.Document);

            // Exercise the open flow used by OpenDocument: save the current document, start a new
            // one, and reopen the saved file through the native dialog. ActiveDirectory is pointed
            // at an unrelated folder first, so the open must navigate to the file's folder by the
            // full path it is given.
            var savePath = TestContext.GetTestResultsPath(@"NativeFileDialogOpen.sky");
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(savePath);
                SkylineWindow.NewDocument();
                Settings.Default.ActiveDirectory = System.IO.Path.GetTempPath();
            });
            var documentBeforeOpen = SkylineWindow.Document;
            SkylineWindow.BeginInvoke((Action)(() => SkylineWindow.ShowOpenFileDialog()));
            NativeDialog.WaitForDialog<NativeOpenFileDialog>().EnterPathAndAccept(savePath);
            WaitForDocumentChangeLoaded(documentBeforeOpen);
            Assert.AreEqual(savePath, SkylineWindow.DocumentFilePath);
        }

        private static void AssertPngSignature(byte[] bytes)
        {
            // PNG files begin with the 8-byte signature 0x89 'P' 'N' 'G' 0x0D 0x0A 0x1A 0x0A.
            var signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            Assert.IsTrue(bytes.Length >= signature.Length, @"Image is too short to be a PNG.");
            for (int i = 0; i < signature.Length; i++)
                Assert.AreEqual(signature[i], bytes[i], @"Image is not a PNG (signature mismatch).");
        }
    }
}
