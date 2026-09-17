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
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.ToolsUI;
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

            TestImportLayoutWithoutTargets();

            TestImportLayoutNeedingResults();

            TestExportOntoReadOnlyFile();

            TestExportStartsBesideDocument();

            TestExportTypedFullName();
        }

        /// <summary>
        /// An imported layout must not show a window the user had no way to open. Without results,
        /// View &gt; Results Grid is disabled - but a layout can name the Results Grid and
        /// <see cref="SkylineWindow"/>'s DeserializeForm builds what it is told, so
        /// EnsureApplicableForms has to put it away again. Measured: the same layout DOES show it
        /// when that gating is removed.
        /// </summary>
        private void TestImportLayoutNeedingResults()
        {
            Assert.IsFalse(SkylineWindow.Document.Settings.HasResults);

            // Something to rewrite: the previous step closed every window
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            WaitForOpenForm<DocumentGridForm>();

            var layoutPath = ExportLayout(@"ResultsDependent");
            var layoutXml = File.ReadAllText(layoutPath);
            AssertEx.Contains(layoutXml, typeof(DocumentGridForm).ToString());
            layoutXml = layoutXml.Replace(typeof(DocumentGridForm).ToString(),
                typeof(LiveResultsGrid).ToString());
            AssertEx.Contains(layoutXml, typeof(LiveResultsGrid).ToString());
            File.WriteAllText(layoutPath, layoutXml);

            ImportLayout(layoutPath);
            RunUI(() =>
            {
                var resultsGrid = FormUtil.OpenForms.OfType<LiveResultsGrid>().FirstOrDefault();
                Assert.IsFalse(resultsGrid != null && resultsGrid.Visible,
                    @"Results Grid was shown for a document with no results");
            });
        }

        /// <summary>
        /// The Export dialog opens beside the document, not in <see cref="Settings.ActiveDirectory"/>.
        /// That setting is the last folder ANY file operation used - picking an iRT database moves
        /// it - so on its own it would write a file named after this document somewhere unrelated.
        /// </summary>
        private void TestExportStartsBesideDocument()
        {
            RunUI(() => Settings.Default.ActiveDirectory = Path.GetTempPath());
            // A bare name lands in whatever folder the dialog opened in, and ExportLayout expects
            // it beside the document
            Assert.IsTrue(File.Exists(ExportLayout(@"BesideDocument")));
        }

        /// <summary>
        /// A name TYPED with the full ".sky.view" is taken as typed, not doubled. The shell appends the
        /// selected file type's extension unless the name's LAST extension is the filter's, and the
        /// filter is the single ".view" that both "Name.sky.view" and "Name.view" end in - so neither
        /// gets anything appended. (A ".sky.view" filter used to append itself to "Name.view".)
        /// </summary>
        private void TestExportTypedFullName()
        {
            var typedPath = TestContext.GetTestResultsPath(@"TypedName.sky.view");
            FileEx.SafeDelete(typedPath);
            FileEx.SafeDelete(typedPath + @".sky.view");
            RunLongNativeDlg<NativeSaveFileDialog>(SkylineWindow.ShowExportLayoutDlg, dlg =>
            {
                dlg.EnterPath(typedPath);
                dlg.DismissWithAcceptButton();
            });
            Assert.IsTrue(File.Exists(typedPath));
            Assert.IsFalse(File.Exists(typedPath + @".sky.view"));

            var shortPath = TestContext.GetTestResultsPath(@"ShortName.view");
            FileEx.SafeDelete(shortPath);
            FileEx.SafeDelete(shortPath + @".sky.view");
            RunLongNativeDlg<NativeSaveFileDialog>(SkylineWindow.ShowExportLayoutDlg, dlg =>
            {
                dlg.EnterPath(shortPath);
                dlg.DismissWithAcceptButton();
            });
            Assert.IsTrue(File.Exists(shortPath));
            Assert.IsFalse(File.Exists(shortPath + @".sky.view"));
        }

        /// <summary>
        /// Exporting onto a read-only file reports it. FileSaver.CanSave catches read-only and
        /// access-denied itself, and shows nothing unless it is given a parent window - so without
        /// one the export would write nothing and say nothing.
        /// </summary>
        private void TestExportOntoReadOnlyFile()
        {
            var readOnlyPath = TestContext.GetTestResultsPath(@"ReadOnly.sky.view");
            // Clear it first: a run killed before the finally below leaves the file read-only, and
            // then neither this test nor the results-directory cleanup can touch it again
            if (File.Exists(readOnlyPath))
                File.SetAttributes(readOnlyPath, FileAttributes.Normal);
            File.WriteAllText(readOnlyPath, string.Empty);
            File.SetAttributes(readOnlyPath, FileAttributes.ReadOnly);
            try
            {
                RunDlg<MessageDlg>(() => SkylineWindow.ExportLayout(readOnlyPath), messageDlg =>
                {
                    Assert.AreEqual(string.Format(
                        UtilResources.FileSaver_CanSave_Cannot_save_to__0__The_file_is_read_only,
                        readOnlyPath), messageDlg.Message);
                    messageDlg.OkDialog();
                });
            }
            finally
            {
                File.SetAttributes(readOnlyPath, FileAttributes.Normal);
            }
        }

        /// <summary>
        /// A layout that names no windows at all still leaves Skyline usable. LoadLayoutLocked
        /// destroys the Targets window unconditionally, so without the repair
        /// <see cref="SkylineWindow"/>.SequenceTree stays null and the next document edit throws
        /// from UndoState. Uses the contents-free layout Skyline itself ships.
        /// </summary>
        private void TestImportLayoutWithoutTargets()
        {
            var emptyLayoutPath = TestContext.GetTestResultsPath(@"NoWindows.sky.view");
            using (var layoutStream = Assembly.GetAssembly(typeof(AbstractFunctionalTest))
                       .GetManifestResourceStream(typeof(AbstractFunctionalTest).Namespace + @".minimal.sky.view"))
            using (var fileStream = File.Create(emptyLayoutPath))
            {
                Assert.IsNotNull(layoutStream);
                layoutStream.CopyTo(fileStream);
            }

            ImportLayout(emptyLayoutPath);
            RunUI(() => Assert.IsNotNull(SkylineWindow.SequenceTree));

            // The edit that used to throw: UndoManager.BeginTransaction reads SequenceTree
            RunUI(() => SkylineWindow.ModifyDocument(@"Test edit", doc => doc.ChangeSettings(
                doc.Settings.ChangePeptideSettings(doc.Settings.PeptideSettings)), AuditLogEntry.SkipChange));
        }

        /// <summary>
        /// The Export dialog offers "&lt;document&gt;.sky.view" - the document's own layout name, handed
        /// over whole. The ".sky" is part of the name, not of the ".view" extension the dialog filters on,
        /// so the offer is neither trimmed to "&lt;document&gt;.view" nor doubled.
        /// </summary>
        private void TestDefaultExportFileName(string documentPath)
        {
            RunLongNativeDlg<NativeSaveFileDialog>(SkylineWindow.ShowExportLayoutDlg, dlg =>
            {
                string fileName = null;
                WaitForConditionUI(() => null != (fileName =
                    dlg.EnumerateChildren().OfType<NativeTextBox>().FirstOrDefault()?.GetValueNow() as string));
                Assert.AreEqual(Path.GetFileName(SkylineWindow.GetViewFile(documentPath)), fileName);
                // Captioned with the command, not the shell's generic "Save As"
                Assert.AreEqual(SkylineResources.SkylineWindow_ShowExportLayoutDlg_Export_Window_Layout, dlg.Title);
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
                Assert.AreEqual(SkylineResources.SkylineWindow_ShowImportLayoutDlg_Import_Window_Layout, dlg.Title);
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
        /// <para>A BASE name with no extension, deliberately: the dialog appends the ".view" that
        /// <see cref="SkylineWindow.FILTER_VIEW"/> carries, so the returned path proves which extension
        /// the dialog adds. (Full names are exercised by TestExportTypedFullName.)</para>
        /// </summary>
        private string ExportLayout(string baseName)
        {
            var layoutPath = TestContext.GetTestResultsPath(baseName + SkylineWindow.EXT_VIEW);
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
            RunLongNativeDlg<NativeOpenFileDialog>(SkylineWindow.ShowImportLayoutDlg, dlg =>
            {
                dlg.EnterPath(layoutPath);
                dlg.Accept();
            });
        }
    }
}
