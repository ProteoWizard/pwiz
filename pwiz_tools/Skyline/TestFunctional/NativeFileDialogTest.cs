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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies the native common file dialog (the OpenFileDialog) end to end: it is visible to
    /// the MCP UI-introspection layer (enumerated by <see cref="JsonToolServer.GetOpenForms"/> as a
    /// native form, and captured by <see cref="JsonUiService.GetFormImage"/> by its reported id
    /// even though it is not a WinForms form), it can be dismissed, and a file can be opened
    /// through it with <see cref="NativeFileDialog.EnterPath"/> and <see cref="NativeOpenFileDialog.Accept"/>.
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

            // Show the native Open dialog, introspect it through the connector, then cancel it. RunLongNativeDlg
            // shows it and hands back its wrapper once it appears, and returns once ShowOpenFileDialog has come back
            // (i.e. the dialog has closed) -- so the test drives the middle and needs no waits of its own.
            var documentBefore = SkylineWindow.Document;
            RunLongNativeDlg<NativeOpenFileDialog>(SkylineWindow.ShowOpenFileDialog, fileDialog =>
            {
                // GetOpenForms includes the native dialog, flagged as native.
                var nativeForms = McpConnector.GetOpenForms().Where(form => form.IsNative).ToArray();
                Assert.AreEqual(1, nativeForms.Length,
                    @"Expected exactly one native form while the Open dialog is showing.");
                var nativeForm = nativeForms[0];

                // GetFormImage resolves the native id (rather than throwing "form not found" as it would for a
                // missing WinForms form) and captures the window by taking a screen copy of its rectangle. With a
                // live display that yields PNG bytes; on a machine whose Remote Desktop session has been
                // disconnected the app can still show windows but CopyFromScreen throws ("The handle is invalid",
                // which we cannot avoid), so ScreenCapture.IsDesktopAvailable() is false and GetFormImageBytes
                // returns the "capture unavailable" message with no Data (regression #4229). Which outcome we get is
                // exactly IsDesktopAvailable(); a thrown ArgumentException would instead mean the native id was not
                // routed to the capture path at all.
                var image = JsonUiService.GetFormImageBytes(nativeForm.Id);
                if (image.Data != null)
                {
                    Assert.IsTrue(ScreenCapture.IsDesktopAvailable());
                    AssertPngSignature(image.Data);
                }
                else
                {
                    Assert.IsFalse(ScreenCapture.IsDesktopAvailable());
                    Assert.IsNotNull(image.Message);
                }

                fileDialog.DismissWithCancelButton();
            });
            // Cancelling opened nothing, so the document is untouched.
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
            RunNativeDlg<NativeOpenFileDialog>(SkylineWindow.ShowOpenFileDialog, dlg =>
            {
                dlg.EnterPath(savePath);
                dlg.Accept();
            });
            // RunNativeDlg has already waited for ShowOpenFileDialog (which opens the file) to complete, so the
            // document has changed here; only its background loading remains to wait on.
            WaitForDocumentLoaded();
            Assert.AreEqual(savePath, SkylineWindow.DocumentFilePath);

            TestMultiselectNavigateThenSelect();
        }

        /// <summary>
        /// Drives a multiselect Open dialog the way an MCP client would: navigate to a folder and confirm the
        /// arrival by reading the dialog's current folder from GetControls (the read-only "AddressBar" control),
        /// then select several files in it by their quoted names. This is the focused, self-contained proof that
        /// the connector exposes enough to select multiple files -- no waiting is baked into the dialog; the caller
        /// observes the state and drives each step.
        /// </summary>
        private void TestMultiselectNavigateThenSelect()
        {
            // A folder holding several files to select.
            var selectDir = TestContext.GetTestResultsPath(@"MultiSelect");
            Directory.CreateDirectory(selectDir);
            var fileNames = new[] { @"alpha.txt", @"beta.txt", @"gamma.txt" };
            var filePaths = fileNames.Select(name => Path.Combine(selectDir, name)).ToArray();
            foreach (var path in filePaths)
                File.WriteAllText(path, @"x");

            // Show a MULTISELECT Open dialog starting in a DIFFERENT folder, so driving it needs a real navigation.
            // RunLongNativeDlg runs the exercise on the test thread, so it can read the dialog (GetControls) between
            // steps; it returns once the shown dialog has closed (here, once ShowDialog has returned).
            string[] selectedFiles = null;
            RunLongNativeDlg<NativeOpenFileDialog>(
                () =>
                {
                    using (var dlg = new System.Windows.Forms.OpenFileDialog
                        { Multiselect = true, InitialDirectory = Path.GetTempPath() })
                    {
                        if (dlg.ShowDialog(SkylineWindow) == System.Windows.Forms.DialogResult.OK)
                            selectedFiles = dlg.FileNames;
                    }
                },
                // Navigate to the folder (confirmed through GetControls) and select the files by name.
                fileDialog => SelectFilesInOpenDialog(fileDialog, selectDir, fileNames));

            CollectionAssert.AreEquivalent(filePaths, selectedFiles,
                @"The multiselect dialog did not return the files that were selected by name.");
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
