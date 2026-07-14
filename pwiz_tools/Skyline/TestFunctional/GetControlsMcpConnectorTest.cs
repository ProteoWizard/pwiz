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
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises <see cref="JsonToolServer.GetControls"/> -- the form-introspection method that lists the
    /// interactive controls so a caller can discover what is on a form, and how to address it, without
    /// reading the source. Checks that a caption-less field is reported with the visible Label that names
    /// it, a list with its item action, and a button with its click action.
    /// </summary>
    [TestClass]
    public class GetControlsMcpConnectorTest : McpConnectorTest
    {
        [TestMethod]
        public void TestGetControlsMcpConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Drive the verbs through the running JSON tool server (Program.MainJsonToolServer), the same
            // path an external MCP client uses; it is torn down with the window (SkylineWindow.OnHandleDestroyed).
            StartToolService();

            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                documentSettingsDlg.EditAnnotationList);
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(editListDlg.AddItem);
            string dlgId = GetOpenFormId<DefineAnnotationDlg>();

            var controls = McpConnector.GetControls(dlgId);
            Assert.IsTrue(controls.Length > 0, @"GetControls returned nothing.");

            // GetControls reports each control's Path -- already parented onto the form, so it can be passed
            // straight back to act on the control (no re-parenting) -- plus its state and current Value, but
            // not its actions (those come from the get_actions action).
            Assert.IsTrue(controls.All(c => c.Path.Parent != null && c.Path.Parent.Text == dlgId),
                @"Each control's Path should be parented onto the form.");

            // The name field has no caption of its own -- it is discoverable by the "Name" label that
            // names it, and get_actions reports that it can be value-set.
            var nameField = controls.FirstOrDefault(c => c.Path.Type == @"TextBox" && c.Path.Text == @"Name");
            Assert.IsNotNull(nameField, @"Expected a TextBox discoverable by the label 'Name'.");
            CollectionAssert.Contains(ActionNames(nameField.Path), @"set_value");

            // The Applies-to list is discoverable by its "Applies to" label and supports an item action.
            var appliesToList = controls.FirstOrDefault(c => c.Path.Type == @"CheckedListBox");
            Assert.IsNotNull(appliesToList, @"Expected the Applies-to CheckedListBox.");
            Assert.AreEqual(@"Applies to", appliesToList.Path.Text);
            CollectionAssert.Contains(ActionNames(appliesToList.Path), @"check_item",
                @"The list should report the check_item action.");

            // The OK button is discoverable by its own caption and supports a click.
            var okButton = controls.FirstOrDefault(c => c.Path.Text == @"OK");
            Assert.IsNotNull(okButton, @"Expected an OK button discoverable by its caption.");
            CollectionAssert.Contains(ActionNames(okButton.Path), @"click");

            OkDialog(defineAnnotationDlg, () => defineAnnotationDlg.DialogResult = DialogResult.Cancel);
            OkDialog(editListDlg, () => editListDlg.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }

        // The snake_case names of the actions get_actions reports for the element at the given path.
        private string[] ActionNames(UiElementPath path) =>
            ((ActionInfo[]) McpConnector.PerformAction(path, @"get_actions", null)).Select(a => a.Name).ToArray();
    }
}
