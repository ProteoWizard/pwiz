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
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises <see cref="JsonUiService.GetGridText"/> and <see cref="JsonUiService.SetGridText"/> on
    /// a plain <see cref="DataGridView"/> (not a DataboundGridControl): the "Rules" grid in the Rule Set
    /// Editor. The form has two grids, so the grid is chosen by control name; the Pattern column is
    /// found by its column Name so the test is translation-proof.
    /// </summary>
    [TestClass]
    public class PlainGridConnectorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPlainGridConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            RunUI(() => documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.metadata_rules));
            var ruleSetEditor = ShowDialog<MetadataRuleSetEditor>(documentSettingsDlg.AddMetadataRule);
            string editorId = JsonUiService.GetOpenForms()
                .First(form => form.Type == nameof(MetadataRuleSetEditor)).Id;

            // The visible index of the "Pattern" column (matched by its column Name, not its header).
            var rulesGrid = (DataGridView)ruleSetEditor.Controls.Find(@"dataGridViewRules", true).First();
            int patternColumn = -1;
            RunUI(() =>
            {
                var visibleColumns = rulesGrid.Columns.Cast<DataGridViewColumn>()
                    .Where(col => col.Visible).OrderBy(col => col.DisplayIndex).ToList();
                patternColumn = visibleColumns.FindIndex(col => col.Name == @"colPattern");
            });
            Assert.IsTrue(patternColumn >= 0);

            // GetGridText reads the plain grid -- its header row names the Pattern column.
            string gridText = JsonUiService.GetGridText(editorId, @"dataGridViewRules");
            StringAssert.Contains(gridText.Split('\n')[0], @"Pattern");

            // Move to the Pattern cell of the first row, then SetGridText types "D" there, like a user
            // editing it.
            JsonUiService.SetCurrentCellAddress(editorId, @"dataGridViewRules", patternColumn, 0);
            JsonUiService.SetGridText(editorId, @"dataGridViewRules", @"D");
            RunUI(() => Assert.AreEqual(@"D", rulesGrid.Rows[0].Cells[@"colPattern"].Value?.ToString(),
                @"SetGridText did not set the Pattern cell in the plain rules grid."));

            OkDialog(ruleSetEditor, () => ruleSetEditor.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }
    }
}
