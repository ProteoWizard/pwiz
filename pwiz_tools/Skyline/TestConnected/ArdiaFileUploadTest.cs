/*
 * Copyright 2025 University of Washington - Seattle, WA
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

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected
{
    [TestClass]
    public class ArdiaFileUploadTest : AbstractFunctionalTestEx
    {
        private const string DEFAULT_DIRECTORY_NAME = @"ZZZ-Document-Upload";

        [TestMethod]
        public void TestArdiaFileUpload()
        {
            if (!ArdiaTestUtil.EnableArdiaTests)
            {   
                Console.Error.WriteLine("NOTE: skipping Ardia test because username/password for Ardia is not configured in environment variables");
                return;
            }

            TestFilesZip = @"TestConnected\ArdiaFileUploadTest.zip";

            RunFunctionalTest();
        }

        // TODO: verify file successfully uploaded
        protected override void DoTest()
        {
            Assert.IsFalse(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(0, Settings.Default.RemoteAccountList.Count);

            // Configure Ardia account
            var account = ArdiaTestUtil.GetTestAccount();

            OpenDocument("Basic.sky");

            RegisterRemoteServer(account);
            Assert.IsTrue(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(1, Settings.Default.RemoteAccountList.Count);

            account = (ArdiaAccount)Settings.Default.RemoteAccountList[0];
            AssertEx.IsTrue(!string.IsNullOrEmpty(account.Token));

            // Test scenarios 
            TestCreateFolder(account);

            TestSuccessfulUpload(account);
        }

        private static void TestCreateFolder(ArdiaAccount account)
        {
            var ardiaClient = ArdiaClient.Create(account);

            // ardiaClient.CreateFolder(@"/ZZZ-Document-Upload", @"NewFolder1", null);

            // TODO: fix. DELETE works in Python but not .NET?!
            // ardiaClient.DeleteFolder(@"/ZZZ-Document-Upload/NewFolder1");
        }

        private static void TestSuccessfulUpload(ArdiaAccount account) 
        {
            var publishDlg = ShowDialog<PublishDocumentDlgArdia>(() => SkylineWindow.PublishToArdia());

            // TODO: re-enable upload. Skipping for now. It works if the test account's role is Super Admin
            //       but tests run as Tester role.
            publishDlg.SkipUpload = true;

            WaitForConditionUI(() => publishDlg.IsLoaded);

            RunUI(() => publishDlg.FoldersTree.Nodes[0].Expand());
            WaitForConditionUI(() => !publishDlg.RemoteCallPending );

            RunUI(() => publishDlg.SelectItem(DEFAULT_DIRECTORY_NAME));

            var docUploadedDlg = ShowDialog<MessageDlg>(publishDlg.OkDialog);
            OkDialog(docUploadedDlg, docUploadedDlg.ClickOk);

            Assert.AreEqual(ArdiaClient.URL_PATH_SEPARATOR + DEFAULT_DIRECTORY_NAME, publishDlg.DestinationPath);
        }

        private static void RegisterRemoteServer(ArdiaAccount account) 
        {
            Assert.IsNotNull(account);

            var optionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Remote));
            var editRemoteAccountListDlg = ShowDialog<EditListDlg<SettingsListBase<RemoteAccount>, RemoteAccount>>(() => optionsDlg.EditRemoteAccounts());

            var addAccountDlg = ShowDialog<EditRemoteAccountDlg>(() => editRemoteAccountListDlg.AddItem());
            RunUI(() => addAccountDlg.SetRemoteAccount(account));

            var testSuccessfulDlg = ShowDialog<MessageDlg>(() => addAccountDlg.TestSettings());
            OkDialog(testSuccessfulDlg, testSuccessfulDlg.OkDialog);
            OkDialog(addAccountDlg, addAccountDlg.OkDialog);
            OkDialog(editRemoteAccountListDlg, editRemoteAccountListDlg.OkDialog);
            OkDialog(optionsDlg, optionsDlg.OkDialog);
        }
    }
}