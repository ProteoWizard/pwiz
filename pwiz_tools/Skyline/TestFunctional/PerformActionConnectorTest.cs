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
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises <see cref="JsonUiService.PerformAction"/> -- the general "locate a control by a
    /// <see cref="ControlId"/>, then act on it" method. Resolves a control by its label, by the Name
    /// echoed from GetControls, and performs set_value, get_value, and click.
    /// </summary>
    [TestClass]
    public class PerformActionConnectorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPerformActionConnector()
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

            // The form is referred to by a ControlId of Type "Form"; controls hang off it as Parent.
            var formId = new ControlId { Type = @"Form", Name = dlgId };

            // set_value: locate the name box by the label that names it, set its value.
            var nameById = new ControlId { Parent = formId, Label = @"Name" };
            JsonUiService.PerformAction(nameById, @"set_value", @"MyAnnotation");
            RunUI(() => Assert.AreEqual(@"MyAnnotation", NameTextBox(defineAnnotationDlg).Text,
                @"PerformAction set_value did not set the name box."));

            // get_value reads it back.
            Assert.AreEqual(@"MyAnnotation", JsonUiService.PerformAction(nameById, @"get_value", null),
                @"PerformAction get_value did not return the name box value.");

            // Round-trip: the ControlId GetControls returns (carrying the Name) resolves the same control.
            var nameInfo = JsonUiService.GetControls(dlgId)
                .First(c => c.Id.Type == @"TextBox" && c.Id.Label == @"Name");
            Assert.IsFalse(string.IsNullOrEmpty(nameInfo.Id.Name), @"Expected GetControls to echo the control Name.");
            Assert.AreEqual(@"MyAnnotation", JsonUiService.PerformAction(nameInfo.Id, @"get_value", null),
                @"PerformAction did not resolve the control by its Name.");

            // An unsupported action on a control reports a clear error rather than acting.
            AssertEx.ThrowsException<System.Exception>(() =>
                JsonUiService.PerformAction(nameById, @"set_item_checked", @"x"));

            // click: close the dialog by clicking its Cancel button, located by label.
            OkDialog(defineAnnotationDlg, () =>
                JsonUiService.PerformAction(new ControlId { Parent = formId, Label = @"Cancel" }, @"click", null));
            OkDialog(editListDlg, () => editListDlg.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }

        private static TextBox NameTextBox(DefineAnnotationDlg dlg) =>
            (TextBox) dlg.Controls.Find(@"tbxName", true).First();
    }
}
