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
using pwiz.Skyline;
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
    /// <see cref="UiElementPath"/>, then act on it" method. Resolves a control by its label, by the Path
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
            // GetControls is exercised through the running JSON tool server (torn down with the window).
            RunUI(() => Program.StartToolService());

            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                documentSettingsDlg.EditAnnotationList);
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(editListDlg.AddItem);
            string dlgId = JsonUiService.GetOpenForms()
                .First(form => form.Type == nameof(DefineAnnotationDlg)).Id;

            // The form is the root of every path (Parent null, Text the form id, Type "Form"); controls
            // hang off it as Parent.
            var formPath = new UiElementPath(null, dlgId, null, @"Form");

            // set_value: locate the name box by the label that names it, set its value.
            var namePath = new UiElementPath(formPath, @"Name", null, null);
            JsonUiService.PerformAction(namePath, @"set_value", @"MyAnnotation");
            RunUI(() => Assert.AreEqual(@"MyAnnotation", NameTextBox(defineAnnotationDlg).Text,
                @"PerformAction set_value did not set the name box."));

            // get_value reads it back (the return is typed per the action -- a string here).
            Assert.AreEqual(@"MyAnnotation", (string) JsonUiService.PerformAction(namePath, @"get_value", null),
                @"PerformAction get_value did not return the name box value.");

            // get_actions: every control lists what it supports (an ActionInfo[] -- name + description).
            var actions = ((ActionInfo[]) JsonUiService.PerformAction(namePath, @"get_actions", null))
                .Select(a => a.Name).ToArray();
            CollectionAssert.Contains(actions, @"set_value");
            CollectionAssert.Contains(actions, @"get_children");
            Assert.IsTrue(((ActionInfo[]) JsonUiService.PerformAction(namePath, @"get_actions", null))
                .All(a => !string.IsNullOrEmpty(a.Description)), @"Every reported action should have a description.");

            // get_children: returns the children as ControlInfo[], each Path already parented onto the
            // element it was queried on (the form here), so it can be used directly.
            var formChildren = (ControlInfo[]) JsonUiService.PerformAction(formPath, @"get_children", null);
            Assert.IsTrue(formChildren.Length > 0, @"Expected the form to report child controls.");
            Assert.IsTrue(formChildren.All(c => c.Path.Parent != null && c.Path.Parent.Text == dlgId),
                @"Each child returned by get_children should be parented onto the form.");

            // A path with no selectors set resolves to its Parent itself -- so addressing a control
            // under a Form parent with nothing else (the way the MCP tool sends a form-only target)
            // returns the form's own children.
            var formViaParent = new UiElementPath(formPath, null, null, null);
            var childrenViaParent = (ControlInfo[]) JsonUiService.PerformAction(formViaParent, @"get_children", null);
            Assert.AreEqual(formChildren.Length, childrenViaParent.Length,
                @"get_children with no selectors under a Form should resolve to the form itself.");

            // Round-trip: the Path GetControls returns is already parented onto the form, so it resolves the
            // same control as-is (no re-parenting), and Name is echoed.
            var nameInfo = Program.MainJsonToolServer.GetControls(dlgId)
                .First(c => c.Path.Type == @"TextBox" && c.Path.Text == @"Name");
            Assert.IsNotNull(nameInfo.Path.Parent, @"GetControls should return a path parented onto the form.");
            Assert.IsFalse(string.IsNullOrEmpty(nameInfo.Name), @"Expected GetControls to echo the control Name.");
            Assert.AreEqual(@"MyAnnotation", (string) JsonUiService.PerformAction(nameInfo.Path, @"get_value", null),
                @"PerformAction did not resolve the control by the Path GetControls returned.");

            // An unsupported action on a control reports a clear error rather than acting.
            AssertEx.ThrowsException<System.Exception>(() =>
                JsonUiService.PerformAction(namePath, @"check_item", @"x"));

            // click: close the dialog by clicking its Cancel button, located by label. NOT inside an OkDialog
            // action: that runs on the UI thread, and a connector action posts its gesture to that thread and
            // waits for it -- from the thread itself, that deadlocks. Click from this (test) thread, the way a
            // connector client does, then wait for the dialog the click closed.
            JsonUiService.PerformAction(new UiElementPath(formPath, @"Cancel", null, null), @"click", null);
            WaitForClosedForm(defineAnnotationDlg);
            OkDialog(editListDlg, () => editListDlg.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }

        private static TextBox NameTextBox(DefineAnnotationDlg dlg) =>
            (TextBox) dlg.Controls.Find(@"tbxName", true).First();
    }
}
