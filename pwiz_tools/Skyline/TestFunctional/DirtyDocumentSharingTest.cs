/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DirtyDocumentSharingTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDirtyDocumentSharing()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestContext.EnsureTestResultsDir();
            var format201SkyZip = TestContext.GetTestResultsPath("Format201.sky.zip");
            RunUI(() =>
            {
                SkylineWindow.Paste(@"PEPTIDEK");
                // Show the audit log window so we can verify that the .view file got saved
                SkylineWindow.ShowAuditLog();
            });

            Assert.IsFalse(File.Exists(format201SkyZip));
            ConfirmAction<MultiButtonMsgDlg>(() => SkylineWindow.ShareDocument(format201SkyZip), dlg => dlg.ClickNo());
            {
                var shareTypeDlg = WaitForOpenForm<ShareTypeDlg>();
                RunUI(() => shareTypeDlg.SelectedSkylineVersion = SkylineVersion.V20_1);
                OkDialog(shareTypeDlg, shareTypeDlg.OkDialog);
            }
            WaitForCondition(() => File.Exists(format201SkyZip));

            var auditLogForm = FindOpenForm<AuditLogForm>();
            Assert.IsNotNull(auditLogForm);

            // Close the audit log form before creating Format202.sky.zip
            OkDialog(auditLogForm, auditLogForm.Close);
            
            var format202SkyZip = TestContext.GetTestResultsPath("Format202.sky.zip");
            Assert.IsFalse(File.Exists(format202SkyZip));
            ConfirmAction<MultiButtonMsgDlg>(() => SkylineWindow.ShareDocument(format202SkyZip), dlg => dlg.ClickNo());
            {
                var shareTypeDlg = WaitForOpenForm<ShareTypeDlg>();
                RunUI(() => shareTypeDlg.SelectedSkylineVersion = SkylineVersion.V20_2);
                OkDialog(shareTypeDlg, shareTypeDlg.OkDialog);
            }
            WaitForCondition(() => File.Exists(format202SkyZip));

            ConfirmAction<MultiButtonMsgDlg>(()=>SkylineWindow.NewDocument(), dlg=>dlg.ClickNo());
            Assert.IsNull(FindOpenForm<AuditLogForm>());
            RunUI(()=>SkylineWindow.LoadFile(format201SkyZip));
            WaitForDocumentLoaded();
            
            // Audit log should be showing
            Assert.IsNotNull(FindOpenForm<AuditLogForm>());
            
            Assert.AreEqual(SkylineVersion.V20_1.SrmDocumentVersion, SkylineWindow.SavedDocumentFormat);
            RunUI(()=>SkylineWindow.SaveDocument());

            // Saving should update the SavedDocumentFormat to the current version
            Assert.AreEqual(SkylineVersion.CURRENT.SrmDocumentVersion, SkylineWindow.SavedDocumentFormat);

            RunUI(()=>SkylineWindow.LoadFile(format202SkyZip));

            // Audit log should not be showing
            Assert.IsNull(FindOpenForm<AuditLogForm>());

            Assert.AreEqual(SkylineVersion.V20_2.SrmDocumentVersion, SkylineWindow.SavedDocumentFormat);
        }
    }
}
