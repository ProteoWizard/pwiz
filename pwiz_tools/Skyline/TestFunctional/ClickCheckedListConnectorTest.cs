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
    /// Exercises AI Connector behaviors on the Define Annotation dialog:
    ///   * <see cref="JsonUiService.SetFormValue"/> matched against a label ("Name") sets the editable
    ///     control the label labels (the Name TextBox), not the label itself;
    ///   * checking an item in the "Applies to" CheckedListBox the way a user does -- a set_selected_index
    ///     action to the item, then a click that toggles the selected item's check;
    ///   * <see cref="JsonUiService.GetFormValue"/> reads the CheckedListBox's checked items.
    /// Matched by the English label/item text, so the test runs in en.
    /// </summary>
    [TestClass]
    public class ClickCheckedListConnectorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestClickCheckedListConnector()
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

            // SetFormValue against the "Name" LABEL sets the editable field it labels (the Name
            // TextBox, AnnotationName), not the label itself.
            JsonUiService.SetFormValue(dlgId, @"Name", @"ConnectorAnnotation");
            RunUI(() => Assert.AreEqual(@"ConnectorAnnotation", defineAnnotationDlg.AnnotationName,
                @"SetFormValue did not set the Name field through its label."));

            // SetFormValue into the multi-line value-list TextBox: newline-separated text becomes
            // separate list values (bare '\n' is normalized to the CRLF the dialog splits on). The
            // Values box is enabled only for a Value List, so set the type first.
            JsonUiService.SetFormValue(dlgId, @"Type", @"Value List");
            JsonUiService.SetFormValue(dlgId, @"Values", "Healthy\nDiseased");
            RunUI(() => CollectionAssert.AreEqual(new[] { @"Healthy", @"Diseased" },
                defineAnnotationDlg.Items.ToArray(),
                @"SetFormValue did not set the multi-line value list as separate values."));

            // The "Replicates" target starts unchecked.
            RunUI(() => Assert.IsFalse(
                defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate)));

            // The Applies-to CheckedListBox, addressed by its "Applies to" label. Find the index of the
            // "Replicates" item to select it.
            var appliesTo = new UiElementPath(
                new UiElementPath(null, dlgId, null, @"Form"), @"Applies to", null, null);
            int replicatesIndex = -1;
            RunUI(() =>
            {
                var checkedListBox = (CheckedListBox) defineAnnotationDlg.Controls
                    .Find(@"checkedListBoxAppliesTo", true).First();
                for (int i = 0; i < checkedListBox.Items.Count; i++)
                    if (checkedListBox.GetItemText(checkedListBox.Items[i]) == @"Replicates")
                        replicatesIndex = i;
            });
            Assert.IsTrue(replicatesIndex >= 0, @"Expected a Replicates item in the Applies-to list.");

            // Check it the way a user does: select the item, then click the CheckedListBox (a click
            // toggles the selected item's check).
            JsonUiService.PerformAction(appliesTo, @"set_selected_index", replicatesIndex.ToString());
            JsonUiService.PerformAction(appliesTo, @"click", null);
            WaitForConditionUI(() =>
                defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate));
            RunUI(() => Assert.IsTrue(
                defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate),
                @"Selecting and clicking did not check the Replicates item in the Applies-to list."));

            // GetFormValue on the CheckedListBox returns the checked items' text, one per line.
            Assert.AreEqual(@"Replicates", JsonUiService.GetFormValue(dlgId, @"Applies to"),
                @"GetFormValue did not return the checked Applies-to items.");

            // Clicking it again toggles it back off (it is still the selected item).
            JsonUiService.PerformAction(appliesTo, @"click", null);
            WaitForConditionUI(() =>
                !defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate));
            RunUI(() => Assert.IsFalse(
                defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate),
                @"Clicking again did not uncheck the Replicates item."));

            OkDialog(defineAnnotationDlg, () => defineAnnotationDlg.DialogResult = DialogResult.Cancel);
            OkDialog(editListDlg, () => editListDlg.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }
    }
}
