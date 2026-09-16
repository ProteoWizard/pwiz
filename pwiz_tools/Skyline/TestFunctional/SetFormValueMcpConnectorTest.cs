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
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises <see cref="JsonToolServer.SetFormValue"/> on plain controls, addressed by the visible
    /// text that names them: a text box by its "Name" label and a combo box by its "Type" label (the
    /// labels resolve to the caption-less fields they name), a count box by the unit label that TRAILS it
    /// ("3 Peptides"), and any control by the internal Name GetControls prints. Labels and the picked type
    /// value are read from the dialogs' resources, so the test runs in any UI language.
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
            TestLabeledFields();
            TestNameAndTrailingLabel();
        }

        private void TestLabeledFields()
        {
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                documentSettingsDlg.EditAnnotationList);
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(editListDlg.AddItem);
            string dlgId = GetOpenFormId<DefineAnnotationDlg>();

            // Address the fields by the localized labels that name them, read from the dialog's resources.
            string nameLabel = GetLocalizedText<DefineAnnotationDlg>(@"lblName");
            string typeLabel = GetLocalizedText<DefineAnnotationDlg>(@"lblType");
            // The Type combo's items are ListPropertyType objects whose text is the localized annotation
            // type name, so pick and assert with that same string.
            string numberType = ListPropertyType.GetAnnotationTypeName(AnnotationDef.AnnotationType.number);

            // The name box has no caption of its own; the "Name" label matches the field it names.
            McpConnector.SetFormValue(dlgId, nameLabel, @"MyAnnotation");
            RunUI(() => Assert.AreEqual(@"MyAnnotation", NameTextBox(defineAnnotationDlg).Text,
                @"SetFormValue did not set the name box via its 'Name' label."));

            // The type combo, addressed by its "Type" label, picks the item by its visible text.
            McpConnector.SetFormValue(dlgId, typeLabel, numberType);
            RunUI(() => Assert.AreEqual(numberType, TypeComboBox(defineAnnotationDlg).Text,
                @"SetFormValue did not select 'Number' in the type combo via its 'Type' label."));

            OkDialog(defineAnnotationDlg, () => defineAnnotationDlg.DialogResult = DialogResult.Cancel);
            OkDialog(editListDlg, () => editListDlg.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }

        // The Library tab's "Limit peptides per protein" count box has no caption before it: the only word that
        // names it is the "Peptides" label AFTER it on the same row. It resolves by that trailing label, and by
        // the internal Name GetControls prints, which any control can be addressed by.
        private void TestNameAndTrailingLabel()
        {
            // The pick panel holding the box only shows with a library checked. A spec to a file that need not
            // exist is enough for the dialog to list and check it: nothing loads it until OK, and the dialog is
            // cancelled.
            var librarySpec = new BiblioSpecLiteSpec(@"PlaceholderLibrary", @"C:\nonexistent\placeholder.blib");
            RunUI(() => Settings.Default.SpectralLibraryList.Add(librarySpec));
            try
            {
                var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(
                    () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Library));
                RunUI(() => peptideSettingsUi.PickedLibraries = new[] { librarySpec.Name });
                string dlgId = GetOpenFormId<PeptideSettingsUI>();
                string peptidesLabel = GetLocalizedText<PeptideSettingsUI>(@"labelPeptides");
                var countBox = (TextBox) peptideSettingsUi.Controls.Find(@"textPeptideCount", true).First();

                // The checkbox only enables once peptides are ranked (the rank combo, by its "Rank peptides by"
                // label, offers the spec's own rank ids without loading the file); the box is then enabled by the
                // checkbox, addressed by the checkbox's own caption.
                McpConnector.SetFormValue(dlgId, GetLocalizedText<PeptideSettingsUI>(@"label7"),
                    LibrarySpec.PEP_RANK_PICKED_INTENSITY.Label);
                McpConnector.SetFormValue(dlgId, GetLocalizedText<PeptideSettingsUI>(@"cbLimitPeptides"), @"true");
                RunUI(() => Assert.IsTrue(countBox.Enabled, @"Checking 'Limit peptides per protein' did not enable the count box."));

                McpConnector.SetFormValue(dlgId, @"textPeptideCount", @"3");
                RunUI(() => Assert.AreEqual(@"3", countBox.Text, @"SetFormValue did not set the count box via its Name."));

                McpConnector.SetFormValue(dlgId, peptidesLabel, @"4");
                RunUI(() => Assert.AreEqual(@"4", countBox.Text, @"SetFormValue did not set the count box via its trailing 'Peptides' label."));

                // GetControls names the box by that trailing label too, so a caller sees the name that works.
                var countInfo = McpConnector.GetControls(dlgId).FirstOrDefault(control => control.Name == @"textPeptideCount");
                Assert.IsNotNull(countInfo, @"GetControls did not list the count box.");
                Assert.AreEqual(peptidesLabel, countInfo.Path?.Text, @"GetControls did not name the count box by its trailing label.");

                OkDialog(peptideSettingsUi, () => peptideSettingsUi.DialogResult = DialogResult.Cancel);
            }
            finally
            {
                RunUI(() => Settings.Default.SpectralLibraryList.Remove(librarySpec));
            }
        }

        private static TextBox NameTextBox(DefineAnnotationDlg dlg) =>
            (TextBox) dlg.Controls.Find(@"tbxName", true).First();

        private static ComboBox TypeComboBox(DefineAnnotationDlg dlg) =>
            (ComboBox) dlg.Controls.Find(@"comboType", true).First();
    }
}
