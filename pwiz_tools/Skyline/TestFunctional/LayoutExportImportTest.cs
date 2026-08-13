/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests File &gt; Export &gt; Window Layout and File &gt; Import &gt; Window Layout: the round trip
    /// through the real menu handlers and their native file dialogs, the default file name the Export
    /// dialog offers, and the check that keeps a file which is not a layout from tearing down the
    /// windows the user arranged.
    /// </summary>
    [TestClass]
    public class LayoutExportImportTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLayoutExportImport()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var documentPath = TestContext.GetTestResultsPath(@"LayoutExportImport.sky");
            RunUI(() => SkylineWindow.SaveDocument(documentPath));

            // Arrange a window the default layout does not show, so the round trip has something to restore.
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            WaitForOpenForm<DocumentGridForm>();

            TestDefaultExportFileName(documentPath);

            // A name of its own, so the round trip cannot be satisfied by the ".sky.view" that
            // saving the document already wrote beside it.
            var layoutPath = ExportLayout(@"ExportedLayout");
            Assert.IsTrue(File.Exists(layoutPath));

            // Take the window away, then bring it back by importing what was exported.
            RunUI(() => SkylineWindow.ShowDocumentGrid(false));
            WaitForClosedForm<DocumentGridForm>();
            ImportLayout(layoutPath);
            WaitForOpenForm<DocumentGridForm>();

            TestImportNotALayoutFile(documentPath);
        }

        /// <summary>
        /// The Export dialog offers "&lt;document&gt;.sky.view", built from the document's base name
        /// plus the extension <see cref="SkylineWindow.FILTER_SKY_VIEW"/> carries. Handing the dialog
        /// a name that already ends in ".sky.view" instead makes it offer
        /// "&lt;document&gt;.sky.view.sky.view", which is what this pins.
        /// </summary>
        private void TestDefaultExportFileName(string documentPath)
        {
            RunLongNativeDlg<NativeSaveFileDialog>(SkylineWindow.ShowExportLayoutDlg, dlg =>
            {
                Assert.AreEqual(Path.GetFileName(SkylineWindow.GetViewFile(documentPath)), GetFileNameText(dlg));
                dlg.DismissWithCancelButton();
            });
        }

        /// <summary>
        /// Importing a file which is not a window layout reports it and leaves the windows alone, rather
        /// than destroying them on the way to failing.
        /// </summary>
        private void TestImportNotALayoutFile(string documentPath)
        {
            RunLongNativeDlg<NativeOpenFileDialog>(SkylineWindow.ShowImportLayoutDlg, dlg =>
            {
                dlg.EnterPath(documentPath);
                dlg.Accept();
                var messageDlg = WaitForOpenForm<MessageDlg>();
                // Case-insensitive: the dialog upper-cases the drive letter of the path it returns
                Assert.AreEqual(string.Format(
                    SkylineResources.SkylineWindow_UpdateGraphUI_Failure_attempting_to_load_the_window_layout_file__0__,
                    documentPath), messageDlg.Message, true);
                OkDialog(messageDlg, messageDlg.OkDialog);
            });
            // The layout survived the attempt
            WaitForOpenForm<DocumentGridForm>();
        }

        /// <summary>
        /// Exports to <paramref name="baseName"/> in the dialog's current folder (the test results
        /// folder, which saving the document made the active directory) and returns the path written.
        ///
        /// <para>A BASE name with no extension, deliberately. The dialog appends the extension
        /// <see cref="SkylineWindow.FILTER_SKY_VIEW"/> carries, so the returned path proves the name
        /// picked up ".sky.view" rather than a bare ".view". Typing a name that already ends in
        /// ".sky.view" would instead produce "Name.sky.view.sky.view" - the shell compares only the
        /// name's LAST extension (".view") against the filter's and appends when they differ, which
        /// no name ending in a two-part extension can satisfy.</para>
        /// </summary>
        private string ExportLayout(string baseName)
        {
            var layoutPath = TestContext.GetTestResultsPath(baseName + SkylineWindow.EXT_SKY_VIEW);
            FileEx.SafeDelete(layoutPath); // Or a second local run hits the dialog's own overwrite prompt
            RunLongNativeDlg<NativeSaveFileDialog>(SkylineWindow.ShowExportLayoutDlg, dlg =>
            {
                dlg.EnterPath(baseName);
                dlg.DismissWithAcceptButton();
            });
            return layoutPath;
        }

        private void ImportLayout(string layoutPath)
        {
            RunNativeDlg<NativeOpenFileDialog>(SkylineWindow.ShowImportLayoutDlg, dlg =>
            {
                dlg.EnterPath(layoutPath);
                dlg.Accept();
            });
        }

        /// <summary>
        /// The text in the file dialog's file-name box, which the dialog presents under the label
        /// <see cref="NativeFileDialog.FILE_NAME_FIELD"/>.
        /// </summary>
        private static string GetFileNameText(NativeFileDialog dlg)
        {
            return (string) dlg.EnumerateChildren()
                .First(child => Equals(child.Label, NativeFileDialog.FILE_NAME_FIELD))
                .GetValueNow();
        }
    }
}
