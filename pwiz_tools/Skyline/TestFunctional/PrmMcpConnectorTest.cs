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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises the generic form-automation verbs of the AI Connector (<see cref="JsonToolServer"/>)
    /// against the first steps of the PRM tutorial's "Import Peptide Search" flow:
    ///   * <see cref="JsonToolServer.ClickMainMenuItem"/> -- "File > Import > Peptide Search".
    ///   * <see cref="JsonToolServer.ClickFormButton"/> -- the "Add Files" button, which opens the
    ///     native "Add Input Files" dialog (a dialog owned by the modal wizard).
    ///   * <see cref="JsonToolServer.SetFormValue"/> + ClickFormButton -- select two files and Open,
    ///     driving the native dialog through the wrapper.
    /// The menu path is read from the live (localized) menu so the test is translation-proof.
    /// </summary>
    [TestClass]
    public class PrmMcpConnectorTest : McpConnectorTest
    {
        private const string FILE_NAME_LABEL = @"File name";

        [TestMethod]
        public void TestPrmMcpConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Drive the inlined verb(s) through the running JSON tool server (torn down with the window).
            StartToolService();

            // The Import Peptide Search wizard bases its file dialog on the document's folder, so the
            // document must be saved. The two input files only need to exist (the dialog has
            // CheckPathExists=true) -- they are added to a list, not parsed, at this stage.
            var savePath = TestContext.GetTestResultsPath(@"PrmMcpConnector.sky");
            var file1 = TestContext.GetTestResultsPath(@"search1.perc.xml");
            var file2 = TestContext.GetTestResultsPath(@"search2.perc.xml");
            File.WriteAllText(file1, string.Empty);
            File.WriteAllText(file2, string.Empty);
            RunUI(() => SkylineWindow.SaveDocument(savePath));

            // 1) Open the wizard through the menu, using the menu's own localized text.
            string menuPath = null;
            RunUI(() => menuPath = BuildLocalizedMenuPath(@"importPeptideSearchMenuItem"));
            McpConnector.ClickMainMenuItem(menuPath);
            var wizard = WaitForOpenForm<ImportPeptideSearchDlg>();
            string wizardId = GetOpenFormId<ImportPeptideSearchDlg>();

            // 2) Click "Add Files" -> the native "Add Input Files" dialog appears. Wait for it by TYPE and take
            // its FormId: everything the test then does to it goes through the connector by that id, which is the
            // part an AI Connector client actually exercises. Waiting on GetOpenForms instead bought nothing --
            // the only thing it could match on is IsNative, which a message box and a folder browser wear too.
            McpConnector.ClickFormButton(wizardId, GetLocalizedText<BuildPeptideSearchLibraryControl>(@"btnAddFile"));
            var addFilesDlg = WaitForNativeDlg<NativeFileDialog>();
            string addFilesId = addFilesDlg.FormId;

            // 3) Select the two files and Open -- the tutorial's "hold Ctrl, click the two files, click Open".
            // As a person would: go to the files' folder first, then pick them there by their bare names. (A
            // list of full paths would overflow the MAX_PATH file-name box under a long results folder.)
            // Navigating leaves the dialog open, so it is the Open-button click (Accept), not the dismiss action
            // that waits for the dialog to close; Accept finds the button by control id, since Windows localizes
            // its caption. The arrival is confirmed from the dialog's "Address" control, and then the file-name box
            // going empty -- the shell clears it a moment after navigating, and names typed before that clear lands
            // would be wiped.
            string folder = Path.GetDirectoryName(file1);
            McpConnector.SetFormValue(addFilesId, FILE_NAME_LABEL, folder);
            AssertComplete(addFilesDlg.Accept());
            WaitForCondition(() => PathEx.SamePath(McpConnector.GetFormValue(addFilesId, @"Address"), folder),
                @"The Add Input Files dialog did not navigate to the files' folder.");
            WaitForCondition(() => string.IsNullOrEmpty(McpConnector.GetFormValue(addFilesId, FILE_NAME_LABEL)),
                @"The Add Input Files dialog did not clear the file-name box after navigating.");

            // Committed with the dismiss action (the connector's way to press the default button, then wait for the
            // dialog to close) rather than ClickFormButton, whose "Open" caption Windows localizes.
            McpConnector.SetFormValue(addFilesId, FILE_NAME_LABEL,
                QuoteNames(new[] { file1, file2 }.Select(Path.GetFileName)));
            McpConnector.PerformAction(new UiElementPath(null, addFilesId, null, @"Form"), @"dismiss", null);

            // The wizard's search-file list now holds both files. Compare case-insensitively: the native Open
            // dialog returns the drive letter upper-cased ("C:\..."), while the expected paths can be
            // lower-cased ("c:\...") -- e.g. the parallel Docker test runner's results path -- and Windows
            // file paths are case-insensitive.
            WaitForConditionUI(() => wizard.BuildPepSearchLibControl.SearchFilenames.Length == 2);
            string[] expectedFiles = { file1, file2 };
            string[] actualFiles = null;
            RunUI(() => actualFiles = wizard.BuildPepSearchLibControl.SearchFilenames);
            CollectionAssert.AreEquivalent(
                expectedFiles.Select(p => p.ToLowerInvariant()).ToArray(),
                actualFiles.Select(p => p.ToLowerInvariant()).ToArray(),
                "Selected files did not match.\r\nExpected: " + string.Join(" | ", expectedFiles) +
                "\r\nActual:   " + string.Join(" | ", actualFiles));

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

        // Builds the file name box value that selects several files in the dialog's current folder: each bare
        // name double-quoted and space-separated, the convention the common file dialog parses for a multiselect.
        private static string QuoteNames(IEnumerable<string> names)
        {
            return string.Join(@" ", names.Select(name => @"""" + name + @""""));
        }

    }
}
