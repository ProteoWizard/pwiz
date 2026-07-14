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
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises <see cref="JsonToolServer.SetFormValue"/> on plain controls, addressed by the visible
    /// text that names them: a text box by its "Name" label and a combo box by its "Type" label (the
    /// labels resolve to the caption-less fields they name). Runs in en (matches the English labels).
    /// </summary>
    [TestClass]
    public class SetFormValueMcpConnectorTest : McpConnectorTest
    {
        [TestMethod]
        public void TestSetFormValueMcpConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Drive the inlined verb(s) through the running JSON tool server (torn down with the window).
            StartToolService();

            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                documentSettingsDlg.EditAnnotationList);
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(editListDlg.AddItem);
            string dlgId = GetOpenFormId<DefineAnnotationDlg>();

            // The name box has no caption of its own; "Name" matches the label that names it.
            McpConnector.SetFormValue(dlgId, @"Name", @"MyAnnotation");
            RunUI(() => Assert.AreEqual(@"MyAnnotation", NameTextBox(defineAnnotationDlg).Text,
                @"SetFormValue did not set the name box via its 'Name' label."));

            // The type combo, addressed by its "Type" label, picks the item by its visible text.
            McpConnector.SetFormValue(dlgId, @"Type", @"Number");
            RunUI(() => Assert.AreEqual(@"Number", TypeComboBox(defineAnnotationDlg).Text,
                @"SetFormValue did not select 'Number' in the type combo via its 'Type' label."));

            OkDialog(defineAnnotationDlg, () => defineAnnotationDlg.DialogResult = DialogResult.Cancel);
            OkDialog(editListDlg, () => editListDlg.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }

        private static TextBox NameTextBox(DefineAnnotationDlg dlg) =>
            (TextBox) dlg.Controls.Find(@"tbxName", true).First();

        private static ComboBox TypeComboBox(DefineAnnotationDlg dlg) =>
            (ComboBox) dlg.Controls.Find(@"comboType", true).First();
    }
}
