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

using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises the AI Connector verbs that drive controls beyond plain Buttons:
    ///   * <see cref="JsonToolServer.ClickFormButton"/> on a CheckBox -- the Audit Log "Enable audit
    ///     logging" checkbox (AutoCheck=false, acts on its Click handler, so SetFormValue would not
    ///     toggle it but clicking it does);
    ///   * <see cref="JsonToolServer.ClickControlMenuItem"/> -- the Document Grid "Reports" dropdown,
    ///     whose items are built on demand and so are not reachable by ClickFormButton;
    ///   * a select_tab action on a TabControl -- selecting a Peptide Settings tab by its text.
    /// Forms are found by type name and controls/items matched by their localized visible text, read from
    /// resources, so the test runs in any UI language.
    /// </summary>
    [TestClass]
    public class ClickControlMcpConnectorTest : McpConnectorTest
    {
        [TestMethod]
        public void TestClickControlMcpConnector()
        {
            // Toggling audit logging modifies the document without a normal audit-log entry; suppress
            // the strict test-only audit-log check the same way LiveReportsTutorialTest does.
            using (new AuditLogList.IgnoreTestChecksScope())
                RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Drive the inlined verb(s) through the running JSON tool server (torn down with the window).
            StartToolService();

            // Start from a fresh document so its audit log has no entries -- toggling the checkbox in
            // either direction then does not raise the "this will clear the audit log" confirmation.
            RunUI(() => SkylineWindow.NewDocument());

            // Document Grid first: AuditLogForm derives from DocumentGridForm, so opening the audit
            // log before WaitForOpenForm<DocumentGridForm> would make that wait ambiguous.
            ClickToolStripDropDownItem();
            var auditLogForm = ClickCheckBox();
            ClickTab();

            // Close the forms we opened and return to a fresh document so nothing holds the modified
            // document (and its undo record) past teardown's leak check. Toggling audit logging dirtied the
            // document, so discard those changes rather than letting NewDocument raise the save prompt.
            RunUI(() =>
            {
                SkylineWindow.ShowDocumentGrid(false);
                auditLogForm.Close();
                SkylineWindow.DiscardChanges = true;
                SkylineWindow.NewDocument();
            });
        }

        // ClickFormButton clicks a CheckBox -- the AutoCheck=false "Enable audit logging" one.
        private AuditLogForm ClickCheckBox()
        {
            RunUI(() => SkylineWindow.ShowAuditLog());
            var auditLogForm = WaitForOpenForm<AuditLogForm>();
            string auditFormId = GetOpenFormId<AuditLogForm>();

            bool before = SkylineWindow.Document.Settings.DataSettings.AuditLogging;
            // The checkbox is created in code from a string-table entry, so match it by that same string.
            McpConnector.ClickFormButton(auditFormId, AuditLogStrings.AuditLogForm_AuditLogForm_Enable_audit_logging);
            WaitForConditionUI(() => SkylineWindow.Document.Settings.DataSettings.AuditLogging != before);
            RunUI(() => Assert.AreNotEqual(before, SkylineWindow.Document.Settings.DataSettings.AuditLogging,
                @"ClickFormButton did not toggle the Enable audit logging checkbox."));
            return auditLogForm;
        }

        // ClickControlMenuItem with no control names the form's own menu, which for the Document Grid is its
        // nav-bar toolstrip; it walks the "Reports" dropdown (items built on demand) to switch the report.
        private void ClickToolStripDropDownItem()
        {
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = WaitForOpenForm<DocumentGridForm>();
            string gridId = GetOpenFormId<DocumentGridForm>();

            // The dropdown button's caption comes from the NavBar's resources; the built-in report names
            // are the localized row-source names SkylineViewContext.GetDocumentGridRowSources assigns,
            // which is also what BindingListSource.ViewInfo.Name reports.
            string reports = GetLocalizedText<Common.DataBinding.Controls.NavBar>(@"navBarButtonViews");
            string proteins = Resources.SkylineViewContext_GetDocumentGridRowSources_Proteins;
            string peptides = Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides;

            McpConnector.ClickControlMenuItem(gridId, string.Empty, reports + @" > " + proteins);
            WaitForConditionUI(() => documentGrid.BindingListSource.ViewInfo?.Name == proteins);

            McpConnector.ClickControlMenuItem(gridId, string.Empty, reports + @" > " + peptides);
            WaitForConditionUI(() => documentGrid.BindingListSource.ViewInfo?.Name == peptides);
            RunUI(() => Assert.AreEqual(peptides, documentGrid.BindingListSource.ViewInfo.Name,
                @"ClickControlMenuItem did not switch the Document Grid report."));
        }

        // A select_tab action on the (caption-less) TabControl selects a tab by its visible text.
        private void ClickTab()
        {
            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            string settingsId = GetOpenFormId<PeptideSettingsUI>();

            RunUI(() => peptideSettings.SelectedTab = PeptideSettingsUI.TABS.Digest);
            var tabControl = new UiElementPath(
                new UiElementPath(null, settingsId, null, @"Form"), null, null, @"TabControl");
            McpConnector.PerformAction(tabControl, @"select_tab",
                GetLocalizedText<PeptideSettingsUI>(@"tabQuantification"));
            WaitForConditionUI(() => peptideSettings.SelectedTab == PeptideSettingsUI.TABS.Quantification);
            RunUI(() => Assert.AreEqual(PeptideSettingsUI.TABS.Quantification, peptideSettings.SelectedTab,
                @"select_tab did not select the Quantification tab."));

            OkDialog(peptideSettings, () => peptideSettings.DialogResult = DialogResult.Cancel);
        }
    }
}
