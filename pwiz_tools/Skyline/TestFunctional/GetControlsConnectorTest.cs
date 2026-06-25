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
    /// Exercises <see cref="JsonUiService.GetControls"/> -- the form-introspection method that lists the
    /// interactive controls so a caller can discover what is on a form, and how to address it, without
    /// reading the source. Checks that a caption-less field is reported with the visible Label that names
    /// it, a list with its item action, and a button with its click action.
    /// </summary>
    [TestClass]
    public class GetControlsConnectorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestGetControlsConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                documentSettingsDlg.EditAnnotationList);
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(editListDlg.AddItem);
            string dlgId = JsonUiService.GetOpenForms()
                .First(form => form.Type == nameof(DefineAnnotationDlg)).Id;

            var formPath = new UiElementPath(null, dlgId, null, @"Form");
            var controls = JsonUiService.GetControls(dlgId);
            Assert.IsTrue(controls.Length > 0, @"GetControls returned nothing.");

            // GetControls reports each control's (parentless) Path and state but not its actions -- those
            // come from the get_actions action (Value likewise comes from get_value), so they are not
            // computed up front. A discovered path is re-parented under the form to act on it.
            UiElementPath Reparent(ControlInfo c) =>
                new UiElementPath(formPath, c.Path.Text, c.Path.Index, c.Path.Type);

            // The name field has no caption of its own -- it is discoverable by the "Name" label that
            // names it, and get_actions reports that it can be value-set.
            var nameField = controls.FirstOrDefault(c => c.Path.Type == @"TextBox" && c.Path.Text == @"Name");
            Assert.IsNotNull(nameField, @"Expected a TextBox discoverable by the label 'Name'.");
            CollectionAssert.Contains(
                (string[]) JsonUiService.PerformAction(Reparent(nameField), @"get_actions", null), @"set_value");

            // The Applies-to list is discoverable by its "Applies to" label and supports an item action.
            var appliesToList = controls.FirstOrDefault(c => c.Path.Type == @"CheckedListBox");
            Assert.IsNotNull(appliesToList, @"Expected the Applies-to CheckedListBox.");
            Assert.AreEqual(@"Applies to", appliesToList.Path.Text);
            CollectionAssert.Contains(
                (string[]) JsonUiService.PerformAction(Reparent(appliesToList), @"get_actions", null), @"check_item",
                @"The list should report the check_item action.");

            // The OK button is discoverable by its own caption and supports a click.
            var okButton = controls.FirstOrDefault(c => c.Path.Text == @"OK");
            Assert.IsNotNull(okButton, @"Expected an OK button discoverable by its caption.");
            CollectionAssert.Contains(
                (string[]) JsonUiService.PerformAction(Reparent(okButton), @"get_actions", null), @"click");

            OkDialog(defineAnnotationDlg, () => defineAnnotationDlg.DialogResult = DialogResult.Cancel);
            OkDialog(editListDlg, () => editListDlg.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }
    }
}
