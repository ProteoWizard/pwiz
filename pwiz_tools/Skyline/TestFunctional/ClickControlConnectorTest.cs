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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises <see cref="JsonUiService.ClickFormButton"/>'s ability to click controls beyond plain
    /// Buttons -- here a CheckBox. The Audit Log form's "Enable audit logging" checkbox has
    /// AutoCheck=false and acts on its Click handler, so setting its Checked state (SetFormValue) does
    /// NOT toggle audit logging, but clicking it (ClickFormButton) does. The checkbox is matched by its
    /// visible text and the form by its type name, so the test is translation-proof.
    /// </summary>
    [TestClass]
    public class ClickControlConnectorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestClickControlConnector()
        {
            // Toggling audit logging modifies the document without a normal audit-log entry; suppress
            // the strict test-only audit-log check the same way LiveReportsTutorialTest does.
            using (new AuditLogList.IgnoreTestChecksScope())
                RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Start from a fresh document so its audit log has no entries -- toggling the checkbox in
            // either direction then does not raise the "this will clear the audit log" confirmation.
            RunUI(() => SkylineWindow.NewDocument());
            RunUI(() => SkylineWindow.ShowAuditLog());
            WaitForOpenForm<AuditLogForm>();

            string auditFormId = JsonUiService.GetOpenForms()
                .First(form => form.Type == nameof(AuditLogForm)).Id;

            bool before = SkylineWindow.Document.Settings.DataSettings.AuditLogging;
            JsonUiService.ClickFormButton(auditFormId, @"Enable audit logging");

            WaitForConditionUI(() => SkylineWindow.Document.Settings.DataSettings.AuditLogging != before);
            RunUI(() => Assert.AreNotEqual(before, SkylineWindow.Document.Settings.DataSettings.AuditLogging,
                @"ClickFormButton did not toggle the Enable audit logging checkbox."));
        }
    }
}
