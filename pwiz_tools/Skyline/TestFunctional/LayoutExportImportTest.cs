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
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.Model.Lists;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests File > Export > Window Layout and File > Import > Window Layout, including
    /// importing a layout which was saved for a different document than the one now open,
    /// and the different strictness of the two ways a layout gets loaded.
    /// </summary>
    [TestClass]
    public class LayoutExportImportTest : AbstractFunctionalTestEx
    {
        private const string LIST_NAME = "TestLayoutList";

        [TestMethod]
        public void TestLayoutExportImport()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestMenuItems();
            TestFileDialogFilters();
            TestLayoutRoundTrip();
            TestUnrecognizedWindowSkipped();
            TestListWindowNotInDocument();
            TestOpenDocumentStrictness();
        }

        /// <summary>
        /// Verifies that the menu items exist where they belong and have text, which
        /// catches a menu item added to the designer but left out of the .resx file.
        /// </summary>
        private void TestMenuItems()
        {
            RunUI(() =>
            {
                AssertMenuItem(@"importToolStripMenuItem", @"importLayoutMenuItem");
                AssertMenuItem(@"exportToolStripMenuItem", @"exportLayoutMenuItem");
            });
        }

        private static void AssertMenuItem(string parentName, string itemName)
        {
            var parent = FindMenuItem(SkylineWindow.MainMenuStrip.Items, parentName);
            Assert.IsNotNull(parent, parentName);
            var item = FindMenuItem(parent.DropDownItems, itemName);
            Assert.IsNotNull(item, itemName);
            AssertEx.IsFalse(string.IsNullOrEmpty(item.Text), itemName);
        }

        private static ToolStripMenuItem FindMenuItem(ToolStripItemCollection items, string name)
        {
            foreach (var item in items.OfType<ToolStripMenuItem>())
            {
                if (Equals(item.Name, name))
                    return item;
                var found = FindMenuItem(item.DropDownItems, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Saving a layout must offer only the .sky.view extension. With an all-files entry
        /// the file name Windows suggests as the user types could be a document, which the
        /// layout would then overwrite.
        /// </summary>
        private static void TestFileDialogFilters()
        {
            AssertEx.Contains(SkylineWindow.FILTER_SKY_VIEW, @"*" + SkylineWindow.EXT_SKY_VIEW);
            AssertEx.IsFalse(SkylineWindow.FILTER_SKY_VIEW.Contains(@"*.*"));
            // Opening also matches the p01.view files that ship with the tutorial tests
            AssertEx.Contains(SkylineWindow.FILTER_VIEW, @"*" + SkylineWindow.EXT_VIEW);
        }

        /// <summary>
        /// Exports a layout, takes the layout apart, and verifies that importing puts the
        /// windows back where they were.
        /// </summary>
        private void TestLayoutRoundTrip()
        {
            var viewPath = TestContext.GetTestResultsPath("RoundTrip.sky.view");

            ArrangeTestLayout();
            RunUI(() => SkylineWindow.ExportLayout(viewPath));
            AssertEx.FileExists(viewPath);

            // Take the layout apart, so that a successful import cannot be a no-op
            RunUI(() =>
            {
                SkylineWindow.ShowDocumentGrid(false);
                SkylineWindow.ImmediateWindow.Close();
            });
            WaitForConditionUI(() => FindOpenForm<DocumentGridForm>() == null &&
                                     FindOpenForm<ImmediateWindow>() == null);

            RunUI(() => SkylineWindow.ImportLayout(viewPath));
            WaitForGraphs();
            AssertDockState<DocumentGridForm>(DockState.DockRight);
            AssertDockState<ImmediateWindow>(DockState.DockBottom);
        }

        /// <summary>
        /// Verifies that a window the current Skyline cannot restore is left out of the
        /// layout, rather than aborting the rest of the import.
        /// </summary>
        private void TestUnrecognizedWindowSkipped()
        {
            var viewPath = TestContext.GetTestResultsPath("Unrecognized.sky.view");
            var editedViewPath = TestContext.GetTestResultsPath("UnrecognizedEdited.sky.view");

            ArrangeTestLayout();
            RunUI(() => SkylineWindow.ExportLayout(viewPath));

            // Rename the Immediate Window to something this Skyline knows nothing about
            var layoutXml = File.ReadAllText(viewPath);
            AssertEx.Contains(layoutXml, typeof(ImmediateWindow).ToString());
            File.WriteAllText(editedViewPath,
                layoutXml.Replace(typeof(ImmediateWindow).ToString(), @"pwiz.Skyline.Controls.NoSuchWindow"));

            var messageDlg = ShowDialog<MessageDlg>(() => SkylineWindow.ImportLayout(editedViewPath));
            AssertLayoutProblemMessage(messageDlg, editedViewPath, @"pwiz.Skyline.Controls.NoSuchWindow");
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForGraphs();
            // The unknown window is skipped, but the rest of the layout is restored
            Assert.IsNull(FindOpenForm<ImmediateWindow>());
            AssertDockState<DocumentGridForm>(DockState.DockRight);
        }

        /// <summary>
        /// Verifies that a layout naming a list window is safe to import into a document
        /// which does not define that list.
        /// </summary>
        private void TestListWindowNotInDocument()
        {
            var viewPath = TestContext.GetTestResultsPath("ListWindow.sky.view");

            AddListWithWindow();
            RunUI(() => SkylineWindow.ExportLayout(viewPath));

            // Start a document which does not have the list, and import the layout anyway
            RunUI(() => SkylineWindow.NewDocument(true));
            var messageDlg = ShowDialog<MessageDlg>(() => SkylineWindow.ImportLayout(viewPath));
            AssertLayoutProblemMessage(messageDlg, viewPath, LIST_NAME);
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForGraphs();
            Assert.IsNull(FindOpenForm<ListGridForm>());
        }

        /// <summary>
        /// Opening a document is deliberately more forgiving than importing a layout. The
        /// layout beside a document describes that same document, so a window which does not
        /// apply is not worth interrupting for, while a window Skyline does not recognize at
        /// all means the file is wrong. Importing complains about both, because the user just
        /// picked the file and every window left out is something they asked for.
        /// </summary>
        private void TestOpenDocumentStrictness()
        {
            var docPath = TestContext.GetTestResultsPath("NoList.sky");
            var docViewPath = SkylineWindow.GetViewFile(docPath);

            // Save a document whose layout has a list window, then a document without the list
            RunUI(() => SkylineWindow.NewDocument(true));
            AddListWithWindow();
            RunUI(() => SkylineWindow.SaveDocument(TestContext.GetTestResultsPath("WithList.sky")));
            var withListViewPath = SkylineWindow.GetViewFile(TestContext.GetTestResultsPath("WithList.sky"));

            RunUI(() => SkylineWindow.NewDocument(true));
            RunUI(() => SkylineWindow.SaveDocument(docPath));

            // Give the list-free document the layout that names the list window
            File.Copy(withListViewPath, docViewPath, true);

            // Opening says nothing: the list window simply does not apply to this document
            RunUI(() => SkylineWindow.NewDocument(true));
            OpenDocument(docPath);
            WaitForGraphs();
            Assert.IsNull(FindOpenForm<ListGridForm>());

            // Importing the very same file does complain
            var messageDlg = ShowDialog<MessageDlg>(() => SkylineWindow.ImportLayout(docViewPath));
            AssertLayoutProblemMessage(messageDlg, docViewPath, LIST_NAME);
            OkDialog(messageDlg, messageDlg.OkDialog);

            // A window Skyline does not recognize is severe enough to report on open too
            File.WriteAllText(docViewPath, File.ReadAllText(docViewPath)
                .Replace(typeof(ListGridForm).ToString(), @"pwiz.Skyline.Controls.NoSuchWindow"));
            RunUI(() => SkylineWindow.NewDocument(true));
            var openMessageDlg = ShowDialog<MessageDlg>(() => SkylineWindow.OpenFile(docPath));
            AssertLayoutProblemMessage(openMessageDlg, docViewPath, @"pwiz.Skyline.Controls.NoSuchWindow");
            OkDialog(openMessageDlg, openMessageDlg.OkDialog);
            WaitForDocumentLoaded();
        }

        private void AddListWithWindow()
        {
            RunUI(() => SkylineWindow.ModifyDocument("Add list", doc => doc.ChangeSettings(
                doc.Settings.ChangeDataSettings(doc.Settings.DataSettings.ChangeListDefs(
                    new[] { new ListData(new ListDef(LIST_NAME)) })))));
            RunUI(() => SkylineWindow.ShowList(LIST_NAME));
            WaitForConditionUI(() => FindOpenForm<ListGridForm>() != null);
        }

        /// <summary>
        /// Verifies the warning names the layout file and every window left out of it.
        /// </summary>
        private static void AssertLayoutProblemMessage(MessageDlg messageDlg, string viewFilePath, string windowName)
        {
            AssertEx.Contains(messageDlg.Message, string.Format(
                SkylineResources.SkylineWindow_ShowLayoutProblems_The_following_windows_in_the_window_layout_file__0__could_not_be_restored_,
                viewFilePath));
            AssertEx.Contains(messageDlg.Message, windowName);
        }

        /// <summary>
        /// Shows two windows which do not depend on imported results, and docks them in
        /// places they would never end up by default.
        /// </summary>
        private void ArrangeTestLayout()
        {
            RunUI(() =>
            {
                SkylineWindow.ShowDocumentGrid(true);
                SkylineWindow.ShowImmediateWindow();
            });
            WaitForConditionUI(() => FindOpenForm<DocumentGridForm>() != null &&
                                     FindOpenForm<ImmediateWindow>() != null);
            RunUI(() =>
            {
                FindOpenForm<DocumentGridForm>().DockState = DockState.DockRight;
                FindOpenForm<ImmediateWindow>().DockState = DockState.DockBottom;
            });
            WaitForGraphs();
        }

        private static void AssertDockState<TForm>(DockState dockState) where TForm : DockableForm
        {
            var form = FindOpenForm<TForm>();
            Assert.IsNotNull(form);
            RunUI(() => AssertEx.AreEqual(dockState, form.DockState));
        }
    }
}
