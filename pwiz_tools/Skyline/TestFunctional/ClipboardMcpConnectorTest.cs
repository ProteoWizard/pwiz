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

using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises the clipboard verbs of the AI McpConnector on a text box (where the connector can verify the
    /// result): paste inserts text without using the clipboard, and select_all then paste replaces the whole
    /// content -- the gesture a tutorial paste step relies on (and which a client that cannot touch the
    /// clipboard, e.g. Google Antigravity, needs).
    /// </summary>
    [TestClass]
    public class ClipboardMcpConnectorTest : McpConnectorTest
    {
        [TestMethod]
        public void TestClipboardMcpConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Every verb below is driven through the running JSON tool server (torn down with the window).
            StartToolService();

            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                documentSettingsDlg.EditAnnotationList);
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(editListDlg.AddItem);
            string dlgId = GetOpenFormId<DefineAnnotationDlg>();
            var namePath = new UiElementPath(new UiElementPath(null, dlgId, null, @"Form"), @"Name", null, null);

            // paste into the empty name box inserts the text (no clipboard).
            McpConnector.PerformAction(namePath, @"paste", @"FirstName");
            WaitForConditionUI(() => NameTextBox(defineAnnotationDlg).Text == @"FirstName");

            // select_all then paste replaces the whole content (rather than appending).
            McpConnector.PerformAction(namePath, @"select_all", null);
            McpConnector.PerformAction(namePath, @"paste", @"SecondName");
            WaitForConditionUI(() => NameTextBox(defineAnnotationDlg).Text == @"SecondName");
            RunUI(() => Assert.AreEqual(@"SecondName", NameTextBox(defineAnnotationDlg).Text,
                @"select_all + paste should have replaced the box's contents."));

            OkDialog(defineAnnotationDlg, () => defineAnnotationDlg.DialogResult = DialogResult.Cancel);
            OkDialog(editListDlg, () => editListDlg.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }

        private static TextBox NameTextBox(DefineAnnotationDlg dlg) =>
            (TextBox) dlg.Controls.Find(@"tbxName", true).First();
    }
}
