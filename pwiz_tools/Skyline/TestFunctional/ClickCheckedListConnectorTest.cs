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

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises two AI Connector behaviors on the Define Annotation dialog:
    ///   * <see cref="JsonUiService.SetFormValue"/> matched against a label ("Name") sets the editable
    ///     control the label labels (the Name TextBox), not the label itself;
    ///   * <see cref="JsonUiService.ClickFormButton"/> on an item inside the "Applies to" CheckedListBox
    ///     toggles that item's check (the items are not controls, so they are matched by display text).
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

            // The "Replicates" target starts unchecked.
            RunUI(() => Assert.IsFalse(
                defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate)));

            // ClickFormButton toggles the "Replicates" item in the Applies-to CheckedListBox on.
            JsonUiService.ClickFormButton(dlgId, @"Replicates");
            WaitForConditionUI(() =>
                defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate));
            RunUI(() => Assert.IsTrue(
                defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate),
                @"ClickFormButton did not check the Replicates item in the Applies-to list."));

            // Clicking it again toggles it back off.
            JsonUiService.ClickFormButton(dlgId, @"Replicates");
            WaitForConditionUI(() =>
                !defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate));
            RunUI(() => Assert.IsFalse(
                defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate),
                @"ClickFormButton did not uncheck the Replicates item on the second click."));

            OkDialog(defineAnnotationDlg, () => defineAnnotationDlg.DialogResult = DialogResult.Cancel);
            OkDialog(editListDlg, () => editListDlg.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }
    }
}
