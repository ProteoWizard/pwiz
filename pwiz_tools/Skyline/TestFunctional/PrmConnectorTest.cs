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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises the generic form-automation verbs of the AI Connector (<see cref="JsonUiService"/>)
    /// against the first steps of the PRM tutorial's "Import Peptide Search" flow:
    ///   * <see cref="JsonToolServer.ClickMainMenuItem"/> -- "File > Import > Peptide Search".
    ///   * <see cref="JsonToolServer.ClickFormButton"/> -- the "Add Files" button, which opens the
    ///     native "Add Input Files" dialog (a dialog owned by the modal wizard).
    ///   * <see cref="JsonToolServer.SetFormValue"/> + ClickFormButton -- select two files and Open,
    ///     driving the native dialog through the wrapper.
    /// The menu path is read from the live (localized) menu so the test is translation-proof.
    /// </summary>
    [TestClass]
    public class PrmConnectorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPrmConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Drive the inlined verb(s) through the running JSON tool server (torn down with the window).
            RunUI(() => Program.StartToolService());

            // The Import Peptide Search wizard bases its file dialog on the document's folder, so the
            // document must be saved. The two input files only need to exist (the dialog has
            // CheckPathExists=true) -- they are added to a list, not parsed, at this stage.
            var savePath = TestContext.GetTestResultsPath(@"PrmConnector.sky");
            var file1 = TestContext.GetTestResultsPath(@"search1.perc.xml");
            var file2 = TestContext.GetTestResultsPath(@"search2.perc.xml");
            File.WriteAllText(file1, string.Empty);
            File.WriteAllText(file2, string.Empty);
            RunUI(() => SkylineWindow.SaveDocument(savePath));

            // 1) Open the wizard through the menu, using the menu's own localized text.
            string menuPath = null;
            RunUI(() => menuPath = BuildLocalizedMenuPath(@"importPeptideSearchMenuItem"));
            Program.MainJsonToolServer.ClickMainMenuItem(menuPath);
            var wizard = WaitForOpenForm<ImportPeptideSearchDlg>();
            string wizardId = FormIdOfType(nameof(ImportPeptideSearchDlg));

            // 2) Click "Add Files" -> the native "Add Input Files" dialog appears.
            // Wait for it the way the AI Connector would: poll GetOpenForms (the discovery method
            // IJsonToolService exposes) until the native FileDialog shows up, rather than the
            // internal NativeDialog helper.
            Program.MainJsonToolServer.ClickFormButton(wizardId, @"Add Files");
            string addFilesId = WaitForNativeFileDialogId();

            // 3) Select the two files and Open -- the tutorial's "hold Ctrl, click the two files,
            // click Open". A native dialog has no caption-addressable buttons, so it is confirmed with the
            // dismiss action (the connector's way to press its default button) rather than ClickFormButton.
            Program.MainJsonToolServer.SetFormValue(addFilesId, @"FileName",
                QuotePaths(new[] { file1, file2 }));
            JsonUiService.PerformAction(new UiElementPath(null, addFilesId, null, @"Form"), @"dismiss", null);

            // The wizard's search-file list now holds both files.
            WaitForConditionUI(() => wizard.BuildPepSearchLibControl.SearchFilenames.Length == 2);
            CollectionAssert.AreEquivalent(new[] { file1, file2 },
                wizard.BuildPepSearchLibControl.SearchFilenames);

            OkDialog(wizard, wizard.Close);
        }

        // Builds the visible (localized) menu path "File > Import > Peptide Search" for the menu item
        // with the given control name, by walking up its OwnerItem chain. Reading the text from the
        // live menu keeps the test independent of UI language.
        private string BuildLocalizedMenuPath(string menuItemName)
        {
            var item = FindMenuItemByName(SkylineWindow.MainMenuStrip.Items, menuItemName);
            Assert.IsNotNull(item, @"Menu item not found by name: " + menuItemName);
            var segments = new List<string>();
            for (ToolStripItem current = item; current is ToolStripMenuItem menuItem; current = menuItem.OwnerItem)
                segments.Insert(0, menuItem.Text);
            return string.Join(@" > ", segments);
        }

        private static ToolStripMenuItem FindMenuItemByName(ToolStripItemCollection items, string name)
        {
            foreach (var menuItem in items.OfType<ToolStripMenuItem>())
            {
                if (menuItem.Name == name)
                    return menuItem;
                var found = FindMenuItemByName(menuItem.DropDownItems, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        // Builds the file name box value that selects several files at once: each path double-quoted
        // and space-separated, the convention the common file dialog parses for a multiselect open.
        private static string QuotePaths(IEnumerable<string> paths)
        {
            return string.Join(@" ", paths.Select(p => @"""" + p + @""""));
        }

        private static string FormIdOfType(string typeName)
        {
            var form = JsonUiService.GetOpenForms().FirstOrDefault(f => f.Type == typeName);
            Assert.IsNotNull(form, @"Open form not found of type: " + typeName);
            return form.Id;
        }

        // Polls GetOpenForms -- the discovery method the AI Connector (IJsonToolService) exposes --
        // until the native file dialog appears, and returns its form id.
        private static string WaitForNativeFileDialogId()
        {
            string id = null;
            WaitForCondition(() => null != (id = JsonUiService.GetOpenForms()
                .FirstOrDefault(form => form.IsNative)?.Id));
            return id;
        }
    }
}
