/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that operations such as saving the document to a new name and restarting audit logging
    /// results in the document getting a new GUID.
    /// <br/>
    /// This test creates files:<ul>
    /// <li>Version1.sky.zip</li>
    /// <li>Version2.sky.zip</li>
    /// <li>NoAuditLog.sky.zip</li>
    /// <li>RestartedAuditLog.sky.zip</li>
    /// <li>Other.sky.zip</li>
    /// <li>Frankendoc.sky.zip</li>
    /// </ul>
    /// These files can all be uploaded to the same Panorama folder and "Version1" and "Version2" will
    /// be recognized as different versions of the same document.
    /// </summary>
    [TestClass]
    public class NewDocumentGuidTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestNewDocumentGuid()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            string testFolder = TestContext.GetTestPath("NewDocumentGuidTest");
            if (Directory.Exists(testFolder))
            {
                Helpers.TryTwice(() => Directory.Delete(testFolder, true));
            }
            
            Assert.IsTrue(SkylineWindow.Document.Settings.DataSettings.AuditLogging);

            SaveDocument(Path.Combine(testFolder, "Document.sky"));
            var originalDocumentGuid = SkylineWindow.Document.Settings.DataSettings.DocumentGuid;
            string version1SkyZip = Path.Combine(testFolder, "Version1.sky.zip");
            ShareDocument(version1SkyZip);

            // Saving to a new name should result in a new GUID
            SaveDocument(Path.Combine(testFolder, "OtherDocument.sky"));
            var otherDocumentGuid = SkylineWindow.Document.Settings.DataSettings.DocumentGuid;
            Assert.AreNotEqual(originalDocumentGuid, otherDocumentGuid);
            string otherSkyZip = Path.Combine(testFolder, "Other.sky.zip");
            ShareDocument(otherSkyZip);

            // Open the original document from the .sky.zip
            OpenSharedFile(version1SkyZip);
            Assert.AreEqual(originalDocumentGuid, SkylineWindow.Document.Settings.DataSettings.DocumentGuid);
            RunUI(()=>SkylineWindow.Paste(@"ELVISK"));
            string version2SkyZip = Path.Combine(testFolder, "Version2.sky.zip");
            ShareDocument(version2SkyZip);

            OpenSharedFile(version2SkyZip);
            Assert.AreEqual(originalDocumentGuid, SkylineWindow.Document.Settings.DataSettings.DocumentGuid);
            RunUI(SkylineWindow.ShowAuditLog);
            var auditLogForm = FindOpenForm<AuditLogForm>();
            RunDlg<AlertDlg>(()=>auditLogForm.EnableAuditLogging(false), alertDlg=>alertDlg.ClickYes());
            Assert.AreEqual(originalDocumentGuid, SkylineWindow.Document.Settings.DataSettings.DocumentGuid);
            string noAuditLogSkyZip = Path.Combine(testFolder, "NoAuditLog.sky.zip");
            ShareDocument(noAuditLogSkyZip);

            OpenSharedFile(noAuditLogSkyZip);
            Assert.AreEqual(originalDocumentGuid, SkylineWindow.Document.Settings.DataSettings.DocumentGuid);
            RunUI(SkylineWindow.ShowAuditLog);
            auditLogForm = FindOpenForm<AuditLogForm>();
            RunUI(() => auditLogForm.EnableAuditLogging(true));
            Assert.AreNotEqual(originalDocumentGuid, SkylineWindow.Document.Settings.DataSettings.DocumentGuid);
            string restartedAuditLogSkyZip = Path.Combine(testFolder, "RestartedAuditLog.sky.zip");
            ShareDocument(restartedAuditLogSkyZip);



            // Open "version2SkyZip" and "otherSkyZip" and remember the path to the Skyline documents
            OpenSharedFile(version2SkyZip);
            var version2Path = SkylineWindow.DocumentFilePath;
            Assert.AreEqual(originalDocumentGuid, SkylineWindow.Document.Settings.DataSettings.DocumentGuid);
            OpenSharedFile(otherSkyZip);
            var otherDocumentPath = SkylineWindow.DocumentFilePath;
            Assert.AreEqual(otherDocumentGuid, SkylineWindow.Document.Settings.DataSettings.DocumentGuid);
            
            // Create a folder which contains the document from "version2" and the audit log from "other"
            string frankenFolder = Path.Combine(testFolder, "frankenfolder");
            Directory.CreateDirectory(frankenFolder);
            string frankenDocumentPath = Path.Combine(frankenFolder, "FrankenDocument.sky");
            File.Copy(version2Path, frankenDocumentPath);
            File.Copy(SrmDocument.GetAuditLogPath(otherDocumentPath), SrmDocument.GetAuditLogPath(frankenDocumentPath));

            // Open the document which has the mismatched audit log
            RunDlg<AlertDlg>(()=>SkylineWindow.OpenFile(frankenDocumentPath), alertDlg=>alertDlg.ClickNo());

            // The document should have gotten a new GUID because the audit log was invalid
            var frankenDocumentGuid = SkylineWindow.Document.Settings.DataSettings.DocumentGuid;
            Assert.AreNotEqual(originalDocumentGuid, frankenDocumentGuid);
            Assert.AreNotEqual(otherDocumentGuid, frankenDocumentGuid);
            RunUI(()=>SkylineWindow.SaveDocument());
            Assert.AreEqual(frankenDocumentGuid, SkylineWindow.Document.Settings.DataSettings.DocumentGuid);
            string frankendocSkyZip = Path.Combine(testFolder, "Frankendoc.sky.zip");
            ShareDocument(frankendocSkyZip);
        }
        private void SaveDocument(string path)
        {
            RunUI(() => SkylineWindow.SaveDocument(path));
        }

        private void ShareDocument(string path)
        {
            if (SkylineWindow.Dirty)
            {
                RunUI(()=>SkylineWindow.SaveDocument());
            }
            RunDlg<ShareTypeDlg>(()=>SkylineWindow.ShareDocument(path), shareTypeDlg=>shareTypeDlg.OkDialog());
        }

        private void OpenSharedFile(string skyZipPath)
        {
            RunUI(()=>SkylineWindow.OpenSharedFile(skyZipPath));
        }
    }
}
